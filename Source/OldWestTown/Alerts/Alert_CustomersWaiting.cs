using System.Collections.Generic;
using OldWestTown.AI;
using OldWestTown.Shops;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace OldWestTown.Alerts
{
    /// <summary>
    /// Raised while customers stand at an unattended counter burning patience — the window in
    /// which assigning a shopkeeper still saves the sale, and the reputation hit that follows
    /// a walkout. Silent when self-service is on, because then nobody is actually stuck.
    /// </summary>
    public class Alert_CustomersWaiting : Alert
    {
        private readonly List<GlobalTargetInfo> culprits = new List<GlobalTargetInfo>();

        public Alert_CustomersWaiting()
        {
            defaultPriority = AlertPriority.High;
        }

        public override string GetLabel() => "OWT_AlertUnattended".Translate(culprits.Count);

        public override TaggedString GetExplanation() => "OWT_AlertUnattendedDesc".Translate();

        public override AlertReport GetReport()
        {
            culprits.Clear();
            if (OldWestTownMod.Settings.allowSelfService) return false;

            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                TownEconomy econ = map.GetComponent<TownEconomy>();
                if (econ == null || econ.Shops.Count == 0) continue;

                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    if (!(pawns[i].jobs?.curDriver is JobDriver_BuyFromShop buying)) continue;
                    if (!buying.WaitingForService) continue;

                    Thing counter = pawns[i].CurJob?.GetTarget(TargetIndex.B).Thing;
                    if (counter == null) continue;

                    GlobalTargetInfo target = counter;
                    if (!culprits.Contains(target)) culprits.Add(target);
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
