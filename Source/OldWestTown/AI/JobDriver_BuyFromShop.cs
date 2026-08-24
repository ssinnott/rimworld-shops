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
    /// The driver never assumes the shopkeeper is coming — see JobDriver_PatronizeBusiness for
    /// the shared wait/patience/walkout shape.
    /// </summary>
    public class JobDriver_BuyFromShop : JobDriver_PatronizeBusiness
    {
        private const TargetIndex GoodsInd = TargetIndex.A;

        private const int BrowseTicks = 240;

        /// <summary>Internal because the customer's think tick has to price a queue at a counter it has
        /// not walked to yet, and the length of a goods serve is what that estimate is made of. One
        /// home for the number.</summary>
        internal const int GoodsServeTicks = 180;

        protected override int ServeTicksRequired => GoodsServeTicks;

        private Thing Goods => job.GetTarget(GoodsInd).Thing;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SetupCommonFailConditions();
            AddCommonFinishActions();

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

        private Toil CompleteSale()
        {
            Toil toil = ToilMaker.MakeToil("CompleteSale");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                CompBusiness shop = Shop;
                Thing goods = pawn.carryTracker?.CarriedThing ?? Goods;
                if (shop == null || goods == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Sell what they are holding, not what the job asked for: the carry toil passes
                // subtractNumTakenFromJobCount, so by now job.count is the part of the order that
                // could NOT be picked up — a customer who lost half the stack to a hauler mid-walk
                // would otherwise pay for the wrong half. The carried stack is already capped at
                // the order ShopStock sized.
                ShopTransaction.Result result =
                    ShopTransaction.TrySell(shop, pawn, goods, goods.stackCount, out int price);

                if (result != ShopTransaction.Result.Sold)
                {
                    // Only a purse that came up short is worth remembering. The other refusals
                    // undo themselves: goods pulled from the filter or forbidden drop off the
                    // shelf scan until they are put back, and a shop that shut or went unstaffed
                    // cannot reach this toil at all — the job fails on a closed shop, and an
                    // unserved customer walks out first.
                    if (result == ShopTransaction.Result.CannotAfford)
                    {
                        (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn)
                            ?.RefuseGoods(shop.parent, goods.def);

                        // Sized against this purse minutes ago and refused now: either the price
                        // moved under them — the markup slider, or the nightly reputation roll
                        // catching a visit that spans midnight — or the two sides of the price
                        // disagree, which is the failure this pairing exists to prevent. Worth
                        // telling them apart by hand, so name them rather than accusing the
                        // pricing code.
                        if (Prefs.DevMode)
                        {
                            Log.Warning("[OldWestTown] " + pawn.LabelShort + " could not pay at "
                                + shop.parent.LabelCap + " for an order sized to their purse — the price"
                                + " moved between the shelf and the counter (the markup slider, or the"
                                + " midnight reputation roll), or MaxAffordable and PriceFor disagree.");
                        }
                    }

                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);
                if (record != null)
                {
                    record.spent += price;
                    record.purchases++;
                }
            };
            return toil;
        }
    }
}
