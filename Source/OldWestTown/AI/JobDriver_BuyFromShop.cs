using System.Collections.Generic;
using OldWestTown.Lords;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.AI
{
    /// <summary>
    /// One purchase, start to finish: walk to the goods, look them over, carry them to the
    /// counter, wait to be served, pay.
    ///
    /// The driver never assumes the shopkeeper is coming. It waits out the shop's patience
    /// window and, if nobody serves them, the customer puts the goods back and leaves annoyed —
    /// which is the signal the player needs that the counter wants staffing.
    /// </summary>
    public class JobDriver_BuyFromShop : JobDriver
    {
        private const TargetIndex GoodsInd = TargetIndex.A;
        private const TargetIndex CounterInd = TargetIndex.B;
        private const TargetIndex StandInd = TargetIndex.C;

        private const int BrowseTicks = 240;
        private const int ServeTicks = 180;

        private int waitedTicks;
        private int servedTicks;

        /// <summary>True while this customer stands at an unattended counter burning patience.
        /// The unattended-counter alert reads it; not saved, since the wait toil re-derives it
        /// within a tick of loading.</summary>
        public bool WaitingForService { get; private set; }

        private Thing Goods => job.GetTarget(GoodsInd).Thing;
        private CompShopCounter Shop => job.GetTarget(CounterInd).Thing?.TryGetComp<CompShopCounter>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref waitedTicks, "waitedTicks");
            Scribe_Values.Look(ref servedTicks, "servedTicks");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(CounterInd);
            this.FailOn(() => Shop == null || !Shop.Open);

            // Paid-for goods go straight into the customer's inventory, so anything still in
            // their hands when this job ends is unpaid — whether the visit was cut short by a
            // raid, or they could only afford part of the stack they picked up. Put it back.
            AddFinishAction(condition =>
            {
                WaitingForService = false;
                if (pawn.carryTracker?.CarriedThing != null && pawn.Map != null)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
            });

            // 1. Go and look at the item on the shelf.
            yield return Toils_Goto.GotoThing(GoodsInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(GoodsInd);

            // 2. Browse — this is what makes a busy shop read as busy.
            Toil browse = Toils_General.Wait(BrowseTicks, GoodsInd);
            browse.FailOnDespawnedNullOrForbidden(GoodsInd);
            browse.WithProgressBarToilDelay(GoodsInd);
            browse.socialMode = RandomSocialMode.Normal;
            yield return browse;

            // 3. Pick the goods up and carry them to the counter.
            yield return Toils_Haul.StartCarryThing(GoodsInd, false, true, false, false, false)
                .FailOnDespawnedNullOrForbidden(GoodsInd);

            yield return Toils_Goto.GotoCell(StandInd, PathEndMode.OnCell);

            // 4. Wait to be served.
            yield return WaitForService();

            // 5. Pay up.
            yield return CompleteSale();
        }

        private Toil WaitForService()
        {
            Toil toil = ToilMaker.MakeToil("WaitForService");
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.socialMode = RandomSocialMode.Normal;
            toil.handlingFacing = true;

            toil.initAction = () => { waitedTicks = 0; servedTicks = 0; WaitingForService = false; };

            toil.tickAction = () =>
            {
                CompShopCounter shop = Shop;
                if (shop == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.rotationTracker.FaceTarget(job.GetTarget(CounterInd));

                if (shop.Staffed || OldWestTownMod.Settings.allowSelfService)
                {
                    // Being attended restores patience; service has to be continuous, so a
                    // shopkeeper who drifts off mid-sale starts the serve over, not resumes it.
                    WaitingForService = false;
                    waitedTicks = 0;
                    servedTicks++;
                    if (servedTicks >= ServeTicks) ReadyForNextToil();
                    return;
                }

                WaitingForService = true;
                servedTicks = 0;
                waitedTicks++;
                int patience = shop.Kind?.customerPatienceTicks ?? 2500;
                if (waitedTicks >= patience) WalkOut(shop);
            };

            return toil;
        }

        private void WalkOut(CompShopCounter shop)
        {
            shop.RecordWalkout();
            shop.parent.Map?.GetComponent<TownEconomy>()?.RecordWalkout();

            CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);
            if (record != null)
            {
                record.walkouts++;
                // Don't queue at this counter again this visit — it clearly isn't being worked.
                record.refusedShops.Add(shop.parent);
            }

            // One message per counter per patience-window: a whole group giving up at once
            // is one piece of news, not a screenful.
            if (shop.TryClaimWalkoutMessage())
            {
                Messages.Message(
                    "OWT_CustomerWalkedOut".Translate(pawn.LabelShort, shop.parent.Label),
                    new LookTargets(shop.parent),
                    MessageTypeDefOf.NegativeEvent);
            }

            EndJobWith(JobCondition.Incompletable);
        }

        private Toil CompleteSale()
        {
            Toil toil = ToilMaker.MakeToil("CompleteSale");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                CompShopCounter shop = Shop;
                Thing goods = pawn.carryTracker?.CarriedThing ?? Goods;
                if (shop == null || goods == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                ShopTransaction.Result result =
                    ShopTransaction.TrySell(shop, pawn, goods, goods.stackCount, out int price);

                if (result != ShopTransaction.Result.Sold)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);
                if (record != null)
                {
                    record.spent += price;
                    record.purchases++;
                }

                Pawn keeper = shop.Shopkeeper;
                if (keeper != null)
                {
                    // Serving customers is social work, and it should train the skill that gates it.
                    keeper.skills?.Learn(SkillDefOf.Social, 35f);
                }
            };
            return toil;
        }
    }
}
