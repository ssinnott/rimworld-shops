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
        private const int ServeTicks = 180;

        protected override int ServeTicksRequired => ServeTicks;

        private Thing Goods => job.GetTarget(GoodsInd).Thing;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SetupCommonFailConditions();
            DropCarriedOnFinish();

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

                // Sell the order the job giver sized, not whatever the customer happens to be
                // holding. They are normally carrying exactly that, but on the path where they
                // reach the counter empty-handed `goods` is the whole shelf stack, and the trim
                // above would happily sell all of it — past the per-visit cap ShopStock applies.
                int count = job.count > 0 && job.count < goods.stackCount ? job.count : goods.stackCount;

                ShopTransaction.Result result =
                    ShopTransaction.TrySell(shop, pawn, goods, count, out int price);

                if (result != ShopTransaction.Result.Sold)
                {
                    // Remember it only when the refusal is about these goods and this purse. A
                    // shop that shut or went unstaffed is not the stack's fault — and neither can
                    // reach here anyway, since the job fails on a closed shop and an unserved
                    // customer walks out before this toil runs.
                    if (result == ShopTransaction.Result.CannotAfford || result == ShopTransaction.Result.NoStock)
                    {
                        (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn)
                            ?.RefuseGoods(shop.parent, goods.def);
                    }

                    // CannotAfford here means the two sides of the price disagreed, which is the
                    // one failure that should never happen — worth a line in a dev log, and worth
                    // saying what it means rather than just naming the enum.
                    if (Prefs.DevMode && result == ShopTransaction.Result.CannotAfford)
                    {
                        Log.Warning("[OldWestTown] " + pawn.LabelShort + " could not pay for an order sized "
                            + "for their purse at " + shop.parent.LabelCap
                            + " — ShopPricing.MaxAffordable and PriceFor disagree.");
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
