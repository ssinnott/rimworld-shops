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
            NotServed,
            NotAvailable
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
                // Trim the order to what they can actually pay for rather than sending them away
                // empty-handed over a rounding difference. Same rule the AI sized the order with,
                // which is why this converges instead of re-deriving the same unaffordable total.
                // Whatever is trimmed off stays unpaid in the customer's hands, and the driver's
                // finish action puts it back on the floor.
                count = ShopPricing.MaxAffordable(shop, goods, purse, count);
                if (count <= 0) return Result.CannotAfford;
                price = ShopPricing.PriceFor(shop, goods, count);
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
            shop.RefreshStock();
            TownEconomy econ = shop.parent.Map?.GetComponent<TownEconomy>();
            econ?.RecordSale(customer, price, selfService);
            econ?.RecordGouge(customer, shop);
            TrainShopkeeper(shop);

            pricePaid = price;
            return Result.Sold;
        }

        /// <summary>The service equivalent of TrySell: pays for one visit and applies the
        /// service's effect. <paramref name="consumed"/> is the item a stock-consuming service
        /// (Drink, Meal) picked up off the shelf, already carried by the customer; null for a
        /// service that consumes nothing but time (Haircut). <paramref name="claimed"/> is
        /// whatever ApplyEffect claimed for longer than this transaction — the bed Lodging just
        /// booked — or null for every other service. <paramref name="roundRowdiness"/> is how
        /// much this one round should nudge the customer's own rowdiness — a plain echo of the
        /// service's RowdinessPerUse for every service before Wager, but outcome-dependent for
        /// a wager; see ServiceWorker.ApplyEffect. Every early-return path below leaves it at
        /// its 0f default, since nothing happened for any of them to be rowdy about.</summary>
        public static Result TryServe(CompBusiness shop, Pawn customer, ServiceDef service, Thing consumed, out int pricePaid, out Thing claimed, out float roundRowdiness)
        {
            pricePaid = 0;
            claimed = null;
            roundRowdiness = 0f;
            if (shop == null || customer == null || service?.worker == null) return Result.NoStock;
            if (!shop.Open) return Result.ShopClosed;

            bool selfService = !shop.Staffed;
            if (selfService && !(service.allowsSelfService && OldWestTownMod.Settings.allowSelfService))
                return Result.NotServed;

            // Re-checked here, right before silver moves, not just when the customer picked
            // this service a moment ago: a stock-free service can still run out from under a
            // payment (Lodging's last bed taken by a faster customer, or the only reachable one
            // going unreachable) the way a shelf item already could. Passing customer through,
            // not just shop, means this uses the same reachability filter ApplyEffect itself
            // applies a moment later — a bed vacant only for somebody else must not read as
            // available here. Catching it here, not after, is what keeps "paid but got nothing"
            // structurally impossible.
            if (!service.worker.IsAvailable(shop, customer)) return Result.NotAvailable;

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

            claimed = service.worker.ApplyEffect(shop, service, customer, served, price, out roundRowdiness);

            shop.RecordSale(price);
            shop.RefreshStock();
            TownEconomy econ = shop.parent.Map?.GetComponent<TownEconomy>();
            econ?.RecordSale(customer, price, selfService);
            econ?.RecordGouge(customer, shop);
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

        /// <summary>The one place money leaves a till instead of entering it — TakeSilver's
        /// mirror image, for a wager's payout. Moves up to <paramref name="amount"/> silver out
        /// of the till and into <paramref name="recipient"/>'s purse, and returns the amount
        /// actually paid, which may be less than <paramref name="amount"/> if the till came up
        /// short — CompBusiness.TakeFromTill can structurally never hand back more than the
        /// till holds. The caller (ServiceWorker_Wager) is what decides a shortfall like that is
        /// a failure and reacts to it; this only ever moves what silver exists.</summary>
        public static int PayOutFromTill(CompBusiness shop, Pawn recipient, int amount)
        {
            if (shop == null || recipient?.inventory == null || amount <= 0) return 0;

            List<Thing> stacks = shop.TakeFromTill(amount);
            int total = 0;
            foreach (Thing stack in stacks)
            {
                total += stack.stackCount;
                if (!recipient.inventory.innerContainer.TryAdd(stack, true))
                {
                    // Inventory refused it (rare) — leave it on the floor rather than voiding
                    // it, mirroring TrySell's own goods handoff.
                    GenPlace.TryPlaceThing(stack, recipient.Position, recipient.Map, ThingPlaceMode.Near);
                }
            }

            shop.RecordPayout(total);
            return total;
        }

        /// <summary>The other place money leaves a till by force rather than by the player's own
        /// hand — a stickup emptying it. Unlike PayOutFromTill, which moves a specific amount, a
        /// robbery takes everything currently there: nobody making change for a holdup. Returns
        /// the amount actually taken, which is exactly shop.TillSilver at the moment this runs
        /// (zero if the till came up empty in the meantime — the same graceful "somebody beat you
        /// to it" no-op every other race in this file already resolves to, not a failure).</summary>
        public static int RobTill(CompBusiness shop, Pawn thief)
        {
            if (shop == null || thief?.inventory == null) return 0;

            List<Thing> stacks = shop.TakeFromTill(shop.TillSilver);
            int total = 0;
            foreach (Thing stack in stacks)
            {
                total += stack.stackCount;
                if (!thief.inventory.innerContainer.TryAdd(stack, true))
                {
                    // Inventory refused it (rare) — leave it on the floor rather than voiding
                    // it, mirroring PayOutFromTill's own fallback.
                    GenPlace.TryPlaceThing(stack, thief.Position, thief.Map, ThingPlaceMode.Near);
                }
            }

            shop.RecordRobbery(total);
            return total;
        }

        /// <summary>The other place a raider can take silver that was never in a till at all —
        /// a loose stack sitting on a shop's own sales floor, exactly where Collect takings just
        /// left it. Unlike RobTill this never touches CompBusiness.TakeFromTill/AddToTill: a
        /// floor stack is an ordinary spawned Thing, not something living inside a till's
        /// ThingOwner, so none of the till-container primitives apply — it moves straight from
        /// the floor into the thief's inventory. <paramref name="shop"/> may be null (the silver
        /// is real whether or not its originating business still exists); when it isn't, this
        /// records the theft against the same ledger bucket a till robbery uses and refreshes
        /// the floor cache immediately, so it doesn't go on crediting a stack that's already
        /// gone. Returns the amount actually taken, 0 if the stack was already gone (somebody
        /// else got there first, or a hauler beat the thief to it) — the same graceful no-op
        /// every other race in this file resolves to.</summary>
        public static int GrabFloorSilver(CompBusiness shop, Thing stack, Pawn thief)
        {
            if (stack == null || !stack.Spawned || stack.Destroyed) return 0;
            if (stack.def != ThingDefOf.Silver || stack.stackCount <= 0) return 0;
            if (thief?.inventory == null) return 0;

            int amount = stack.stackCount;
            stack.DeSpawn();
            if (!thief.inventory.innerContainer.TryAdd(stack, true))
            {
                // Inventory refused it (rare) — leave it on the floor rather than voiding it,
                // mirroring RobTill's own fallback.
                GenPlace.TryPlaceThing(stack, thief.Position, thief.Map, ThingPlaceMode.Near);
            }

            shop?.RecordRobbery(amount);
            shop?.RefreshStock();
            return amount;
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
