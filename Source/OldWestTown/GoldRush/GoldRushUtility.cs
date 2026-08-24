using RimWorld;
using Verse;

namespace OldWestTown.GoldRush
{
    /// <summary>
    /// Everything about "is a gold rush doing anything to this map right now" in one place —
    /// live reads off the active <see cref="GameCondition_GoldRush"/>, the same "ask, don't
    /// track" shape CoachTierUtility already uses. No persisted "prospector" flag anywhere:
    /// during an active boom, every arriving customer already is the flood of prospectors the
    /// roadmap describes, so there's nothing to distinguish a subset of them for.
    /// </summary>
    public static class GoldRushUtility
    {
        private const float BoomArrivalMtbDivisor = 3f;      // arrivals triple during the boom
        private const float BustArrivalMtbMultiplier = 2.5f; // arrivals slow, not stop, during the bust
        private const float BoomPurseMultiplier = 1.5f;
        private const float InBasketDemandFactor = 4f;
        private const float OutOfBasketDemandFactor = 0.4f;

        public static GameCondition_GoldRush ActiveCondition(Map map) =>
            map?.gameConditionManager?.GetActiveCondition<GameCondition_GoldRush>();

        public static bool Active(Map map) => ActiveCondition(map) != null;

        public static bool BoomActive(Map map)
        {
            GameCondition_GoldRush cond = ActiveCondition(map);
            return cond != null && !cond.BustActive;
        }

        public static bool BustActive(Map map)
        {
            GameCondition_GoldRush cond = ActiveCondition(map);
            return cond != null && cond.BustActive;
        }

        public static float ArrivalMtbMultiplier(Map map)
        {
            GameCondition_GoldRush cond = ActiveCondition(map);
            if (cond == null) return 1f;
            return cond.BustActive ? BustArrivalMtbMultiplier : 1f / BoomArrivalMtbDivisor;
        }

        public static float PurseMultiplier(Map map) => BoomActive(map) ? BoomPurseMultiplier : 1f;

        /// <summary>Tools (read loosely, matching the general store's own flavor text, as
        /// ThingCategoryDefOf.Manufactured -- no confirmed literal "Tools" category exists to
        /// check against from this sandbox), medicine, meals, booze. IsWithinCategory walks the
        /// category tree itself, so this needs no cached ThingFilter and no static-init-order
        /// reasoning against DefDatabase readiness.</summary>
        public static bool InDemandBasket(Thing t)
        {
            ThingDef def = t?.def;
            if (def == null) return false;
            if (def.IsWithinCategory(ThingCategoryDefOf.Manufactured)) return true;
            if (def.IsWithinCategory(ThingCategoryDefOf.Medicine)) return true;

            IngestibleProperties ing = def.ingestible;
            if (ing == null) return false;
            if (ing.IsMeal) return true;
            return (ing.foodType & FoodTypeFlags.Liquor) != 0;
        }

        /// <summary>Exactly 1f whenever no rush is active -- provably a no-op for ordinary
        /// customers. t == null is treated as neutral (a stock-free service, e.g. Haircut) --
        /// this factor is goods-only by design; see docs/DESIGN.md.</summary>
        public static float DemandFactor(Map map, Thing t)
        {
            if (t == null || !BoomActive(map)) return 1f;
            return InDemandBasket(t) ? InBasketDemandFactor : OutOfBasketDemandFactor;
        }
    }
}
