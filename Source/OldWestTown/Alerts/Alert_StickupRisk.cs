using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace OldWestTown.Alerts
{
    /// <summary>
    /// Rising stickup risk, visible before it ever bites: fires once a map's uncollected till
    /// total crosses AlertThreshold, deliberately below StickupWatch.MinSilverAtRisk — so the
    /// number climbing is visible well before the clock behind it is even live. Mirrors
    /// Alert_CustomersWaiting's shape: a running total in the label, a camera jump to whichever
    /// counters are actually holding the silver.
    /// </summary>
    public class Alert_StickupRisk : Alert
    {
        /// <summary>Deliberately below StickupWatch.MinSilverAtRisk (300): the player should see
        /// this climbing well before the clock behind it starts rolling at all.</summary>
        private const int AlertThreshold = 150;

        private readonly List<GlobalTargetInfo> culprits = new List<GlobalTargetInfo>();

        private int totalSilver;

        public Alert_StickupRisk()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel() => "OWT_AlertStickupRisk".Translate(((float)totalSilver).ToStringMoney());

        public override TaggedString GetExplanation() => "OWT_AlertStickupRiskDesc".Translate();

        public override AlertReport GetReport()
        {
            // Mirrors the same gate StickupWatch.MapComponentTick and IncidentWorker_Stickup.
            // CanFireNowSub already apply: with the setting off, the risk this alert warns about
            // cannot fire at all, so the warning shouldn't be able to outlive it either.
            if (!OldWestTownMod.Settings.stickupsEnabled) return AlertReport.Inactive;

            culprits.Clear();
            totalSilver = 0;

            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                StickupWatch watch = map.GetComponent<StickupWatch>();
                if (watch == null) continue;

                // A map that hasn't reached the threshold contributes nothing — not to the
                // total, and not to the culprit list. Below the line, there's no risk yet worth
                // pointing the camera at.
                int mapSilver = watch.TotalTillSilver;
                if (mapSilver < AlertThreshold) continue;
                totalSilver += mapSilver;

                TownEconomy econ = map.GetComponent<TownEconomy>();
                if (econ == null) continue;
                foreach (CompBusiness shop in econ.Shops)
                {
                    if (shop?.parent == null || !shop.parent.Spawned || shop.TillSilver <= 0) continue;
                    GlobalTargetInfo target = shop.parent;
                    if (!culprits.Contains(target)) culprits.Add(target);
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
