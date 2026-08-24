using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// One place where a shelf price gets decided, so the customer AI, the tooltip and the
    /// transaction can never disagree about what something costs.
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
        /// Also folds in a dressed-up storefront's curb appeal — see
        /// <see cref="CompFalseFront.CurbAppealBonus"/> — so a false front nudges close calls
        /// between similarly-priced rivals without ever outweighing price itself.
        /// </summary>
        public static float ValueAppeal(CompBusiness shop)
        {
            if (shop == null) return 0f;
            float effective = shop.Markup * shop.ReputationPriceFactor;
            // 1.0x markup -> 1.0, 2.0x -> ~0.5, 3.0x -> ~0.33.
            float priceAppeal = Mathf.Clamp(1f / Mathf.Max(0.25f, effective), 0.1f, 2f);
            return priceAppeal + CompFalseFront.CurbAppealBonus(shop);
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
