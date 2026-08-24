using System.Collections.Generic;
using Verse;

namespace OldWestTown.Shops
{
    public class CompProperties_FalseFront : CompProperties
    {
        public CompProperties_FalseFront()
        {
            compClass = typeof(CompFalseFront);
        }
    }

    /// <summary>
    /// A taller-than-it-is false front on a storefront's street side. Pure set dressing with one
    /// small hook into the pricing seam: a customer walking past a dressed-up storefront is a
    /// little more inclined to walk in.
    ///
    /// Nothing here is persisted — the per-instance state is just registration with the map's
    /// <see cref="FalseFrontRegistry"/>, which is rebuilt from spawned comps on load exactly the
    /// way TownEconomy rebuilds its own shop list, so there's nothing for a save to migrate.
    /// </summary>
    public class CompFalseFront : ThingComp
    {
        /// <summary>How far a facade's advertising reaches, in tiles, from a shop's customer-facing side.</summary>
        private const float AdRadius = 7f;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            parent.Map?.GetComponent<FalseFrontRegistry>()?.Register(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            map?.GetComponent<FalseFrontRegistry>()?.Deregister(this);
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            CompBusiness shop = NearestOpenShop(parent.Position, parent.Map);
            return shop != null
                ? "OWT_FalseFrontAdvertising".Translate(shop.parent.LabelCap)
                : "OWT_FalseFrontIdle".Translate();
        }

        /// <summary>
        /// The curb-appeal bonus <see cref="ShopPricing.ValueAppeal"/> folds in for
        /// <paramref name="shop"/>: one qualifying nearby facade is worth something, a second is
        /// worth a little more, capped there — a street with a false front on every building
        /// shouldn't out-advertise one with just a single dressed-up storefront.
        ///
        /// <see cref="JobGiver_BuyFromShop"/> calls this (via ValueAppeal) once per open shop
        /// per candidate, so it walks the map's <see cref="FalseFrontRegistry"/> — a short list
        /// of spawned facades — rather than every Thing on the map, the same way CompBusiness
        /// avoids re-scanning for StockOnDisplay on the same hot path.
        /// </summary>
        public static float CurbAppealBonus(CompBusiness shop)
        {
            if (shop?.parent?.Map == null) return 0f;

            Map map = shop.parent.Map;
            IntVec3 myCell = shop.CustomerCell;
            IReadOnlyList<CompFalseFront> facades = map.GetComponent<FalseFrontRegistry>()?.Facades;
            if (facades == null) return 0f;

            int qualifying = 0;
            for (int i = 0; i < facades.Count; i++)
            {
                Thing t = facades[i].parent;
                if (t == null || !t.Spawned) continue;
                if (t.Position.DistanceTo(myCell) > AdRadius) continue;
                if (NearestOpenShop(t.Position, map) == shop) qualifying++;
            }

            if (qualifying <= 0) return 0f;
            return qualifying == 1 ? 0.10f : 0.15f;
        }

        /// <summary>
        /// The open shop whose customer-facing side is nearest <paramref name="pos"/>, within
        /// advertising range. Shared by the inspect string (what a facade is advertising for)
        /// and the appeal bonus (which shop a nearby facade actually counts toward), so the two
        /// can never disagree about which storefront a given facade belongs to.
        /// </summary>
        private static CompBusiness NearestOpenShop(IntVec3 pos, Map map)
        {
            TownEconomy econ = map?.GetComponent<TownEconomy>();
            if (econ == null) return null;

            CompBusiness nearest = null;
            float nearestDist = 0f;
            foreach (CompBusiness shop in econ.OpenShops())
            {
                float d = pos.DistanceTo(shop.CustomerCell);
                if (d > AdRadius) continue;
                // Strict less-than: the first shop encountered keeps an exact tie, deterministic
                // by TownEconomy's own registration order — the same tie-break style
                // WorkGiver_ManShop.AnyCustomerNear already uses for its own radius check.
                if (nearest == null || d < nearestDist)
                {
                    nearest = shop;
                    nearestDist = d;
                }
            }
            return nearest;
        }
    }
}
