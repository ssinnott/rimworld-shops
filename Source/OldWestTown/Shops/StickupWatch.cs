using System.Collections.Generic;
using OldWestTown.Roles;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// The clock behind a stickup: how much silver is exposed to a robbery anywhere on this map
    /// — sitting in a till, or already loose on a sales floor where Collect takings left it — and
    /// how that turns into rising risk the longer it stays exposed. Read-only against
    /// TownEconomy — this file never adds to or changes it, only sums what CompBusiness already
    /// tracks per counter. Collecting a till does not lower this total: it only moves silver from
    /// one half of the sum to the other. Only a hauler actually carrying it off the map's shops
    /// lowers it.
    ///
    /// Every registered business counts toward the total, open or closed: a robber doesn't check
    /// the sign on the door before cracking the till or grabbing a floor pile, and neither does
    /// the risk this clock tracks.
    /// </summary>
    public class StickupWatch : MapComponent
    {
        /// <summary>Below this much silver exposed — till and floor combined — the clock never
        /// rolls at all — chosen to land near the faro table's own starting bankroll (see
        /// CompProperties_Business.startingTillSilver on OWT_FaroTable in
        /// Buildings_Commerce.xml), so one well-stocked gambling hall alone can just clear the
        /// floor on its own.</summary>
        public const int MinSilverAtRisk = 300;

        /// <summary>Same cadence TownEconomy's own arrival clock checks itself on.</summary>
        private const int ArrivalCheckInterval = 600;

        /// <summary>Silver-at-risk band the MTB curve spans, past the floor above. Not a hard
        /// ceiling on risk — MTB keeps shortening a little past this, just slowly — only where
        /// the curve bottoms out at MinMtbDays.</summary>
        private const float MtbCurveRange = 2000f;

        private const float MaxMtbDays = 6f;
        private const float MinMtbDays = 0.75f;

        /// <summary>An on-duty sheriff doubles the average gap between rolls — the same
        /// suppression TroubleUtility.AnySheriffOnDuty already gives rowdiness, applied to a
        /// second, unrelated bad outcome. Nothing about this is a job or a reference to any
        /// raider; it's a passive read of the same flag, at clock-tick time.</summary>
        private const float SheriffOnDutyMtbFactor = 2f;

        public StickupWatch(Map map) : base(map) { }

        /// <summary>Every silver currently exposed anywhere on this map, across every registered
        /// business — not just the open ones: each shop's till, plus everything loose on its own
        /// sales floor. A stack on a floor two counters both credit as their own is counted once,
        /// the same "don't count one physical pile twice" dedup TownEconomy.TakeStock already
        /// uses for appeal — by bare Thing reference, which can never collide with till silver
        /// either, since till contents live in a ThingOwner and are never spawned on the map.
        /// Recomputed on every read: the shop list is short (one entry per counter), and a
        /// robber's scoring, the alert, and the incident's own point-sizing all need the exact
        /// current figure, not something a tick stale.</summary>
        public int TotalSilverAtRisk
        {
            get
            {
                TownEconomy econ = map.GetComponent<TownEconomy>();
                if (econ == null) return 0;

                int total = 0;
                HashSet<Thing> countedFloorSilver = new HashSet<Thing>();
                IReadOnlyList<CompBusiness> shops = econ.Shops;
                for (int i = 0; i < shops.Count; i++)
                {
                    CompBusiness shop = shops[i];
                    if (shop?.parent == null || !shop.parent.Spawned) continue;
                    total += shop.TillSilver;

                    List<Thing> floor = shop.FloorSilverStacks;
                    for (int j = 0; j < floor.Count; j++)
                    {
                        Thing t = floor[j];
                        if (t == null || !t.Spawned || t.Destroyed) continue;
                        if (countedFloorSilver.Add(t)) total += t.stackCount;
                    }
                }
                return total;
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!map.IsPlayerHome || !OldWestTownMod.Settings.stickupsEnabled) return;
            if (Find.TickManager.TicksGame % ArrivalCheckInterval != 0) return;

            int silver = TotalSilverAtRisk;
            if (silver < MinSilverAtRisk) return;

            float mtbDays = Mathf.Lerp(MaxMtbDays, MinMtbDays,
                Mathf.Clamp01((silver - MinSilverAtRisk) / MtbCurveRange));
            if (TroubleUtility.AnySheriffOnDuty(map)) mtbDays *= SheriffOnDutyMtbFactor;

            if (!Rand.MTBEventOccurs(mtbDays, 60000f, ArrivalCheckInterval)) return;

            // Mirrors TownEconomy.TryAttractCustomers exactly: fire through the storyteller so
            // OWT_Stickup's own minRefireDays still applies, rather than this clock forcing a
            // raid the moment it rolls.
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                OWTDefOf.OWT_Stickup.category, map);
            Find.Storyteller.TryFire(new FiringIncident(OWTDefOf.OWT_Stickup, null, parms));
        }
    }
}
