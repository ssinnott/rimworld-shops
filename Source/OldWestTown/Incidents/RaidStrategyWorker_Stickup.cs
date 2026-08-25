using System.Collections.Generic;
using OldWestTown.Lords;
using OldWestTown.Roles;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace OldWestTown.Incidents
{
    /// <summary>
    /// The one thing a raid strategy actually has to supply: the LordJob that runs the raid.
    /// Every arrival-mode and letter concern is already force-set by
    /// IncidentWorker_Stickup's own overrides, so this file's whole job is building the raid's
    /// state machine and handing it a duration.
    /// </summary>
    public class RaidStrategyWorker_Stickup : RaidStrategyWorker
    {
        private const int BaseDurationTicks = 20000;

        /// <summary>An on-duty sheriff halves how long a stickup sticks around, on top of
        /// halving how often one starts at all (StickupWatch's own MTB roll) — the sheriff's
        /// second, independent lever on this mechanic.</summary>
        private const int SheriffOnDutyDurationTicks = 10000;

        // Protected, not public: confirmed by the compiler (CS0507) — refdump reports member
        // existence and signature but never accessibility, and the base RaidStrategyWorker
        // declares this one protected, unlike CanUseWith below, which is public.
        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {
            // Read once, here, and fixed for the raid's whole lifetime — deliberately not
            // re-read live, so a sheriff walking on or off duty mid-raid can't retroactively
            // lengthen or shorten an already-running encounter.
            int durationTicks = TroubleUtility.AnySheriffOnDuty(map)
                ? SheriffOnDutyDurationTicks
                : BaseDurationTicks;

            return new LordJob_Stickup(parms.faction, TownCentre(map), durationTicks);
        }

        /// <summary>"The middle of the shopping district" — a small, deliberately independent
        /// reimplementation of IncidentWorker_ShopCustomers.FindTownCentre's own averaging.
        /// That file belongs to the parallel arrivals work; this raid has no reason to couple
        /// into it for ten lines of position math.</summary>
        private static IntVec3 TownCentre(Map map)
        {
            TownEconomy econ = map.GetComponent<TownEconomy>();
            if (econ == null) return map.Center;

            IntVec3 sum = IntVec3.Zero;
            int count = 0;
            foreach (CompBusiness shop in econ.Shops)
            {
                if (shop?.parent == null || !shop.parent.Spawned) continue;
                sum += shop.parent.Position;
                count++;
            }
            if (count == 0) return map.Center;

            IntVec3 centre = new IntVec3(sum.x / count, 0, sum.z / count);
            if (centre.InBounds(map) && centre.Walkable(map)) return centre;

            // The average of several counters can land inside a wall; fall back to a real one.
            foreach (CompBusiness shop in econ.Shops)
            {
                if (shop?.parent != null && shop.parent.Spawned) return shop.parent.Position;
            }
            return map.Center;
        }

        /// <summary>Safe regardless of what this returns: IncidentWorker_Stickup.ResolveRaidStrategy
        /// force-assigns OWT_StickupStrategy directly and never consults CanUseWith for its own
        /// firing. Returning false here is a second, independent door that additionally keeps an
        /// unrelated ordinary raid from ever randomly picking this non-combat strategy on its
        /// own.</summary>
        public override bool CanUseWith(IncidentParms parms, PawnGroupKindDef groupKind) => false;
    }
}
