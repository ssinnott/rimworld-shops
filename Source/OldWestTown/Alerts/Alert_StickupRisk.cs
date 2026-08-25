using System.Collections.Generic;
using System.Text;
using OldWestTown.Shops;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace OldWestTown.Alerts
{
    /// <summary>
    /// Rising stickup risk, visible before it ever bites: fires once a map's silver exposed
    /// beyond what its own businesses need to operate — each shop's own declared
    /// startingTillSilver carved out first, see CompBusiness.TillSilverAboveCapital — crosses
    /// AlertThreshold (150), still deliberately below StickupWatch.MinSilverAtRisk (300), so the
    /// number climbing is visible well before the clock behind it is even live. For an ordinary
    /// shop (no declared capital) this is the identical number the risk clock itself watches; a
    /// gambling hall's seeded bankroll is the one case where alert and clock now deliberately
    /// diverge — the clock still counts it (a robber can still take it), the alert doesn't (it
    /// isn't silver the player can safely remove without breaking the table). Mirrors
    /// Alert_CustomersWaiting's shape: a running total in the label, a camera jump to whichever
    /// counters are actually holding the silver — and a dynamic per-shop till/floor breakdown in
    /// the explanation, because a player who can no longer defuse this risk by clicking Collect
    /// needs to be told what to do instead.
    /// </summary>
    public class Alert_StickupRisk : Alert
    {
        /// <summary>Deliberately below StickupWatch.MinSilverAtRisk (300): the player should see
        /// this climbing well before the clock behind it starts rolling at all.</summary>
        private const int AlertThreshold = 150;

        private readonly List<GlobalTargetInfo> culprits = new List<GlobalTargetInfo>();

        /// <summary>Parallel to culprits, same index per shop — the per-shop till/floor
        /// breakdown GetExplanation reads. Scratch UI state only: rebuilt by every GetReport
        /// call, never scribed, the same as culprits/totalSilver themselves.</summary>
        private readonly List<string> culpritLabels = new List<string>();
        private readonly List<int> culpritTill = new List<int>();
        private readonly List<int> culpritFloor = new List<int>();

        private int totalSilver;

        public Alert_StickupRisk()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel() => "OWT_AlertStickupRisk".Translate(((float)totalSilver).ToStringMoney());

        /// <summary>Depends on GetReport having already populated culpritLabels/culpritTill/
        /// culpritFloor for this same poll — the identical trusted mechanism GetLabel already
        /// relies on for totalSilver, not a new pattern for this class.</summary>
        public override TaggedString GetExplanation()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("OWT_AlertStickupRiskDesc".Translate());
            for (int i = 0; i < culpritLabels.Count; i++)
            {
                sb.AppendLine();
                sb.Append("OWT_AlertStickupRiskShopLine".Translate(
                    culpritLabels[i], ((float)culpritTill[i]).ToStringMoney(), ((float)culpritFloor[i]).ToStringMoney()));
            }
            return sb.ToString();
        }

        public override AlertReport GetReport()
        {
            // Mirrors the same gate StickupWatch.MapComponentTick and IncidentWorker_Stickup.
            // CanFireNowSub already apply: with the setting off, the risk this alert warns about
            // cannot fire at all, so the warning shouldn't be able to outlive it either.
            if (!OldWestTownMod.Settings.stickupsEnabled) return AlertReport.Inactive;

            culprits.Clear();
            culpritLabels.Clear();
            culpritTill.Clear();
            culpritFloor.Clear();
            totalSilver = 0;

            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                TownEconomy econ = map.GetComponent<TownEconomy>();
                if (econ == null) continue;

                // Built into fresh per-map locals rather than straight into the member fields,
                // so a map that never clears the threshold below (see mapNetSilver) can be
                // thrown away wholesale instead of committed and then somehow un-committed.
                List<GlobalTargetInfo> mapTargets = new List<GlobalTargetInfo>();
                List<string> mapLabels = new List<string>();
                List<int> mapTills = new List<int>();
                List<int> mapFloors = new List<int>();
                int mapNetSilver = 0;

                // The same by-Thing-reference dedup StickupWatch.TotalSilverAtRisk applies to
                // its own map-wide total, applied again here: two counters sharing one sales
                // floor (or two open-air stalls whose radii overlap) both see the SAME floor
                // stack through their own FloorSilverStacks. Crediting its value to every shop
                // that sees it would make this breakdown's own lines sum to more than
                // mapNetSilver below — only the first shop to claim a given stack, in the order
                // econ.Shops is walked, gets to count it.
                HashSet<Thing> creditedFloorSilver = new HashSet<Thing>();
                foreach (CompBusiness shop in econ.Shops)
                {
                    if (shop?.parent == null || !shop.parent.Spawned) continue;

                    // Above this shop's own declared starting capital, not raw till silver — a
                    // gambling hall's seeded bankroll has to stay in the till to cover a payout,
                    // so it isn't silver the player can safely pull out, and shouldn't trip a
                    // "you have silver at risk you should collect" warning. See
                    // CompBusiness.TillSilverAboveCapital.
                    int till = shop.TillSilverAboveCapital;
                    int floor = 0;
                    List<Thing> floorStacks = shop.FloorSilverStacks;
                    for (int i = 0; i < floorStacks.Count; i++)
                    {
                        Thing t = floorStacks[i];
                        if (t == null || !t.Spawned || t.Destroyed) continue;
                        if (creditedFloorSilver.Add(t)) floor += t.stackCount;
                    }

                    // Both have to be zero for a shop to genuinely carry none at risk — a shop
                    // just Collected still has its floor fully loaded and just as much at risk.
                    // Reads the deduped floor total above, so a shop whose only floor silver
                    // already got credited to an earlier same-floor shop correctly sees zero
                    // here rather than double-claiming it.
                    if (till <= 0 && floor <= 0) continue;

                    GlobalTargetInfo target = shop.parent;
                    if (!mapTargets.Contains(target))
                    {
                        mapTargets.Add(target);
                        mapLabels.Add(shop.parent.LabelCap);
                        mapTills.Add(till);
                        mapFloors.Add(floor);
                        mapNetSilver += till + floor;
                    }
                }

                // A map that hasn't reached the threshold contributes nothing — not to the
                // total, and not to the culprit list. Below the line, there's no risk yet worth
                // pointing the camera at. Gated on the same netted number the breakdown above
                // was built from, so the header total and the per-shop lines can never disagree.
                if (mapNetSilver < AlertThreshold) continue;

                totalSilver += mapNetSilver;
                culprits.AddRange(mapTargets);
                culpritLabels.AddRange(mapLabels);
                culpritTill.AddRange(mapTills);
                culpritFloor.AddRange(mapFloors);
            }
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
