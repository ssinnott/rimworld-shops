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
            ShopClosed,
            NotServed
        }

        public static Result TrySell(CompBusiness shop, Pawn customer, Thing goods, int count, out int pricePaid)
        {
            pricePaid = 0;
            if (shop == null || customer == null || goods == null) return Result.NoStock;
            if (!shop.Open) return Result.ShopClosed;

            // No one behind the counter, no sale — unless the player runs an honesty box.
            bool selfService = !shop.Staffed;
            if (selfService && !OldWestTownMod.Settings.allowSelfService) return Result.NotServed;

            // The walk from shelf to counter gives the player time to change their mind:
            // an item pulled from the stock filter or forbidden mid-carry is not for sale.
            if (goods.def == ThingDefOf.Silver) return Result.NoStock;
            if (!shop.StockFilter.Allows(goods)) return Result.NoStock;
            if (goods.IsForbidden(Faction.OfPlayer)) return Result.NoStock;

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
            shop.parent.Map?.GetComponent<TownEconomy>()?.RecordSale(price, selfService);
            TrainShopkeeper(shop);

            pricePaid = price;
            return Result.Sold;
        }

        /// <summary>The service equivalent of TrySell: pays for one visit and applies the
        /// service's effect. <paramref name="consumed"/> is the item a stock-consuming service
        /// (Drink, Meal) picked up off the shelf, already carried by the customer; null for a
        /// service that consumes nothing but time (Haircut).</summary>
        public static Result TryServe(CompBusiness shop, Pawn customer, ServiceDef service, Thing consumed, out int pricePaid)
        {
            pricePaid = 0;
            if (shop == null || customer == null || service?.worker == null) return Result.NoStock;
            if (!shop.Open) return Result.ShopClosed;

            bool selfService = !shop.Staffed;
            if (selfService && !(service.allowsSelfService && OldWestTownMod.Settings.allowSelfService))
                return Result.NotServed;

            bool consumesStock = service.worker.ConsumesStock;
            if (consumesStock)
            {
                if (consumed == null || !service.worker.CanUse(consumed)) return Result.NoStock;
                if (!shop.StockFilter.Allows(consumed)) return Result.NoStock;
                if (consumed.IsForbidden(Faction.OfPlayer)) return Result.NoStock;
            }

            int price = consumesStock
                ? ShopPricing.PriceFor(shop, consumed, 1)
                : ShopPricing.PriceForService(shop, service);

            int purse = SilverCarriedBy(customer);
            // Unlike TrySell, a service is one unit -- no affordable-partial-stack trimming.
            if (purse < price) return Result.CannotAfford;
            if (!TakeSilver(customer, price, shop)) return Result.CannotAfford;

            Thing served = null;
            if (consumesStock)
            {
                // Exactly one unit changes hands, because exactly one unit was priced. The job giver
                // already asks for a single item, so this normally splits nothing; making it true here
                // means a future caller can't quietly hand over a stack for the price of one drink.
                // A remainder left in the customer's hands is unpaid, and the driver's finish action
                // puts it back on the floor.
                served = consumed.stackCount > 1 ? consumed.SplitOff(1) : consumed;

                // Into the customer's inventory before the effect lands -- mirrors TrySell's goods handoff.
                if (served.Spawned) served.DeSpawn();
                if (customer.carryTracker?.CarriedThing == served)
                {
                    customer.carryTracker.innerContainer.TryTransferToContainer(
                        served, customer.inventory.innerContainer, served.stackCount, out _);
                }
                else if (!customer.inventory.innerContainer.TryAdd(served, true))
                {
                    GenPlace.TryPlaceThing(served, customer.Position, customer.Map, ThingPlaceMode.Near);
                }
            }

            service.worker.ApplyEffect(customer, served);

            shop.RecordSale(price);
            shop.DirtyStock();
            shop.parent.Map?.GetComponent<TownEconomy>()?.RecordSale(price, selfService);
            TrainShopkeeper(shop);

            pricePaid = price;
            return Result.Sold;
        }

        /// <summary>Serving a customer — goods or a service — is social work, and it should
        /// train the skill that gates it. Shared by TrySell and TryServe so the two
        /// customer-facing drivers don't each need their own copy.</summary>
        private static void TrainShopkeeper(CompBusiness shop)
        {
            Pawn keeper = shop.Shopkeeper;
            if (keeper != null) keeper.skills?.Learn(SkillDefOf.Social, 35f);
        }

        public static int SilverCarriedBy(Pawn pawn)
        {
            return pawn?.inventory?.innerContainer?.TotalStackCountOfDef(ThingDefOf.Silver) ?? 0;
        }

        /// <summary>Moves <paramref name="amount"/> silver out of the customer's purse and into the till.</summary>
        private static bool TakeSilver(Pawn customer, int amount, CompBusiness shop)
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
