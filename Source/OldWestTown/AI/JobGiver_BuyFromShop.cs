using OldWestTown.Lords;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.AI
{
    /// <summary>
    /// Decides which shop a customer walks into next. Runs from the OWT_Shop duty's think tree,
    /// above the wander node, so a customer with money and somewhere to spend it always shops
    /// and only loiters when there's nothing to buy.
    /// </summary>
    public class JobGiver_BuyFromShop : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null || pawn.Downed || pawn.InMentalState) return null;

            int purse = ShopTransaction.SilverCarriedBy(pawn);
            if (purse <= 0) return null;

            TownEconomy econ = pawn.Map.GetComponent<TownEconomy>();
            if (econ == null) return null;

            CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);

            CompShopCounter bestShop = null;
            Thing bestGoods = null;
            int bestCount = 0;
            float bestScore = 0f;

            foreach (CompShopCounter shop in econ.OpenShops())
            {
                if (record != null && record.refusedShops.Contains(shop.parent)) continue;
                if (!shop.Open) continue;
                if (!pawn.CanReach(shop.parent, PathEndMode.Touch, Danger.Deadly)) continue;

                Thing goods = ShopStock.ChoosePurchase(shop, pawn, purse, out int count);
                if (goods == null || count <= 0) continue;

                // Prefer a well-priced shop that is actually staffed, and one that's close by.
                float score = ShopPricing.ValueAppeal(shop);
                if (shop.Staffed) score *= 1.5f;
                score /= 1f + pawn.Position.DistanceTo(shop.parent.Position) / 40f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestShop = shop;
                    bestGoods = goods;
                    bestCount = count;
                }
            }

            if (bestShop == null) return null;

            Job job = JobMaker.MakeJob(OWTDefOf.OWT_BuyFromShop, bestGoods, bestShop.parent, bestShop.CustomerCellFor(pawn));
            job.count = bestCount;
            return job;
        }
    }
}
