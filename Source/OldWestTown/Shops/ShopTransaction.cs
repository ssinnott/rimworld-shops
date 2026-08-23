using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// The single point where goods and silver actually change hands. Everything is
    /// re-validated here, because the walk from shelf to counter gives the world plenty of
    /// time to invalidate whatever the customer decided a minute ago.
    /// </summary>
    public static class ShopTransaction
    {
        public enum Result
        {
            Sold,
            NoStock,
            CannotAfford,
            ShopClosed
        }

        public static Result TrySell(CompShopCounter shop, Pawn customer, Thing goods, int count, out int pricePaid)
        {
            pricePaid = 0;
            if (shop == null || customer == null || goods == null) return Result.NoStock;
            if (!shop.Open) return Result.ShopClosed;

            count = Mathf.Clamp(count, 1, goods.stackCount);
            if (count <= 0) return Result.NoStock;

            int price = ShopPricing.PriceFor(shop, goods, count);
            int purse = SilverCarriedBy(customer);

            if (purse < price)
            {
                // Trim the order down to what they can actually pay for rather than
                // sending them away empty-handed over a rounding difference.
                int unit = ShopPricing.PriceFor(shop, goods, 1);
                if (unit <= 0 || purse < unit) return Result.CannotAfford;
                count = Mathf.Min(count, purse / unit);
                price = ShopPricing.PriceFor(shop, goods, count);
                if (count <= 0 || purse < price) return Result.CannotAfford;
            }

            if (!TakeSilver(customer, price, shop)) return Result.CannotAfford;

            // Hand over the goods. The customer may already be carrying them from the shelf.
            Thing sold = goods.stackCount == count ? goods : goods.SplitOff(count);
            if (sold.Spawned) sold.DeSpawn();
            if (customer.carryTracker?.CarriedThing == sold)
            {
                customer.carryTracker.innerContainer.TryTransferToContainer(
                    sold, customer.inventory.innerContainer, sold.stackCount, out _);
            }
            else if (!customer.inventory.innerContainer.TryAdd(sold, true))
            {
                // Inventory refused it (rare) — leave it on the floor rather than voiding it.
                GenPlace.TryPlaceThing(sold, customer.Position, customer.Map, ThingPlaceMode.Near);
            }

            shop.RecordSale(price);
            shop.DirtyStock();
            shop.parent.Map?.GetComponent<TownEconomy>()?.RecordSale(price);

            pricePaid = price;
            return Result.Sold;
        }

        public static int SilverCarriedBy(Pawn pawn)
        {
            return pawn?.inventory?.innerContainer?.TotalStackCountOfDef(ThingDefOf.Silver) ?? 0;
        }

        /// <summary>Moves <paramref name="amount"/> silver out of the customer's purse and into the till.</summary>
        private static bool TakeSilver(Pawn customer, int amount, CompShopCounter shop)
        {
            if (amount <= 0) return true;
            ThingOwner<Thing> purse = customer.inventory.innerContainer;
            int remaining = amount;

            // Copy first: taking from the container mutates it while we iterate.
            List<Thing> coins = new List<Thing>();
            for (int i = 0; i < purse.Count; i++)
            {
                if (purse[i].def == ThingDefOf.Silver) coins.Add(purse[i]);
            }

            foreach (Thing coin in coins)
            {
                if (remaining <= 0) break;
                int take = Mathf.Min(remaining, coin.stackCount);
                Thing taken = coin.stackCount == take ? coin : coin.SplitOff(take);
                if (taken.holdingOwner != null) taken.holdingOwner.Remove(taken);
                shop.AddToTill(taken);
                remaining -= take;
            }

            return remaining <= 0;
        }
    }
}
