using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// One place where a shelf price gets decided — and where a purse is settled against that
    /// same price — so the shop's inspect pane, the customer AI and the transaction can never
    /// disagree about what something costs, or about how much of it a customer can pay for.
    /// </summary>
    public static class ShopPricing
    {
        /// <summary>Nothing is ever free; a customer always parts with at least this much.</summary>
        public const int MinPrice = 1;

        /// <summary>Price for the whole stack of <paramref name="count"/> units.</summary>
        public static int PriceFor(CompBusiness shop, Thing thing, int count)
        {
            if (shop == null || thing == null || count <= 0) return 0;
            float unit = UnitValue(thing) * shop.Markup * shop.ReputationPriceFactor;
            return Mathf.Max(MinPrice, Mathf.RoundToInt(unit * count));
        }

        /// <summary>The largest number of units, up to <paramref name="maxCount"/>, that
        /// <paramref name="purse"/> can actually pay for AT THE PRICE PriceFor WILL CHARGE.
        /// Defined in terms of PriceFor on purpose: the order the AI sizes and the bill the
        /// counter charges must never come from two different roundings.</summary>
        public static int MaxAffordable(CompBusiness shop, Thing thing, int purse, int maxCount)
        {
            if (shop == null || thing == null || purse <= 0 || maxCount <= 0) return 0;
            float unit = UnitValue(thing) * shop.Markup * shop.ReputationPriceFactor;
            int count = unit > 0f
                ? Mathf.Clamp(Mathf.FloorToInt(purse / unit), 0, maxCount)
                : maxCount;
            // The float estimate can sit one unit either side of the truth (rounding, or the
            // MinPrice floor on near-worthless goods). Settle it against the real price. At most
            // one of these loops runs and both are bounded by maxCount, which MaxUnitsPerPurchase
            // already keeps small.
            while (count > 0 && PriceFor(shop, thing, count) > purse) count--;
            while (count < maxCount && PriceFor(shop, thing, count + 1) <= purse) count++;
            return count;
        }

        /// <summary>
        /// Per-unit value before markup. <see cref="Thing.MarketValue"/> already folds in
        /// quality, stuff and remaining hit points, which is exactly what a shopper would judge.
        /// </summary>
        public static float UnitValue(Thing thing)
        {
            return thing == null ? 0f : Mathf.Max(0f, thing.MarketValue);
        }

        /// <summary>
        /// How appealing this price is to a shopper: 1 at market value, falling as markup rises.
        /// Customers use it to choose between shops, so undercutting a rival actually wins trade.
        /// </summary>
        public static float ValueAppeal(CompBusiness shop)
        {
            if (shop == null) return 0f;
            float effective = shop.Markup * shop.ReputationPriceFactor;
            // 1.0x markup -> 1.0, 2.0x -> ~0.5, 3.0x -> ~0.33.
            return Mathf.Clamp(1f / Mathf.Max(0.25f, effective), 0.1f, 2f);
        }

        /// <summary>Price for a service with no backing Thing to read a market value from (Haircut).
        /// A stock-consuming service (Drink, Meal) is priced with the existing PriceFor against
        /// whatever it actually consumes — there is exactly one place a price basis is decided
        /// either way.</summary>
        public static int PriceForService(CompBusiness shop, ServiceDef service)
        {
            if (shop == null || service == null) return 0;
            float unit = service.basePrice * shop.Markup * shop.ReputationPriceFactor;
            return Mathf.Max(MinPrice, Mathf.RoundToInt(unit));
        }
    }
}
