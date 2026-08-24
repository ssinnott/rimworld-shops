using System.Linq;
using UnityEngine;
using Verse;
using OldWestTown.Shops;

namespace OldWestTown.Stagecoach
{
    /// <summary>
    /// Everything about "what is the town's route doing right now" in one place. No registry and
    /// nothing cached — a depot's existence and the active tier are both read live off the map,
    /// the same "recompute rather than track" shape <see cref="TownEconomy.Appeal"/> itself
    /// already uses for a number read about as often.
    /// </summary>
    public static class CoachTierUtility
    {
        /// <summary>
        /// True if any coach depot is standing on this map. Mirrors
        /// <c>TroubleUtility.AnySheriffOnDuty</c>'s own "ask ListerThings, keep no registry"
        /// shape — there is never more than a handful of depots on a map, and this is read at
        /// most twice per <see cref="TownEconomy"/> arrival check.
        /// </summary>
        public static bool HasDepot(Map map) =>
            map?.listerThings.ThingsOfDef(OWTDefOf.OWT_CoachDepot).Any() == true;

        /// <summary>
        /// The active tier for this appeal, or null if there's no depot on the map, or appeal
        /// hasn't reached even the lowest tier. Live, not cached or ratcheted — a town whose
        /// reputation slides can watch its own tier demote, the same way <c>Appeal</c> itself can
        /// fall.
        /// </summary>
        public static CoachTierDef CurrentTier(Map map, float appeal)
        {
            // Below the floor that lets a customer group set out at all, a route can't mean
            // anything. Redundant with TryAttractCustomers's own early-out when a firing is
            // actually being considered, but RouteTier is also read independently by the depot's
            // inspect string and the tier-announcement check, neither of which shares that
            // early-out — this stops a modder-authored tier with too low a minAppeal from
            // reading as "active" for a town that can't attract customers at all.
            if (appeal < TownEconomy.MinAppealForCustomers) return null;
            if (!HasDepot(map)) return null;

            CoachTierDef best = null;
            foreach (CoachTierDef tier in DefDatabase<CoachTierDef>.AllDefsListForReading)
            {
                if (tier.minAppeal > appeal) continue;
                if (best == null || tier.minAppeal > best.minAppeal) best = tier;
            }
            return best;
        }

        /// <summary>
        /// The lowest rung not yet reached — the next milestone. Pass null for
        /// <paramref name="current"/> to ask for the lowest tier that exists at all. Null once
        /// the town is already running at the top of the ladder.
        /// </summary>
        public static CoachTierDef NextTier(CoachTierDef current)
        {
            float floor = current?.minAppeal ?? float.NegativeInfinity;
            CoachTierDef best = null;
            foreach (CoachTierDef tier in DefDatabase<CoachTierDef>.AllDefsListForReading)
            {
                if (tier.minAppeal <= floor) continue;
                if (best == null || tier.minAppeal < best.minAppeal) best = tier;
            }
            return best;
        }

        /// <summary>
        /// How many ticks this tier lets pass between arrivals before forcing one, at the
        /// player's own Customer volume setting — the identical clamp-floor and unit convention
        /// <c>TownEconomy.TryAttractCustomers</c> already uses for its own MTB days.
        /// </summary>
        public static float CeilingTicks(CoachTierDef tier) =>
            tier.arrivalCeilingDays * 60000f / Mathf.Max(0.25f, OldWestTownMod.Settings.customerVolume);
    }
}
