using System.Collections.Generic;
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
    /// a walkout. Silent for a patron whose service honors the global self-service setting
    /// while it's on, since nobody is actually stuck there — but a service that opts out of the
    /// setting (a haircut, always) still leaves its patron stuck and still raises this.
    /// </summary>
    public class Alert_CustomersWaiting : Alert
    {
        private readonly List<GlobalTargetInfo> culprits = new List<GlobalTargetInfo>();

        /// <summary>Pawns waiting, as distinct from counters affected — the label counts
        /// customers, the culprit list jumps the camera between counters.</summary>
        private int waitingCustomers;

        public Alert_CustomersWaiting()
        {
            defaultPriority = AlertPriority.High;
        }

        public override string GetLabel() => "OWT_AlertUnattended".Translate(waitingCustomers);

        public override TaggedString GetExplanation() => "OWT_AlertUnattendedDesc".Translate();

        public override AlertReport GetReport()
        {
            culprits.Clear();
            waitingCustomers = 0;

            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                TownEconomy econ = map.GetComponent<TownEconomy>();
                if (econ == null || econ.Shops.Count == 0) continue;

                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    if (!(pawns[i].jobs?.curDriver is IBusinessPatron patronizing)) continue;
                    if (!patronizing.WaitingForService) continue;

                    Thing counter = pawns[i].CurJob?.GetTarget(TargetIndex.B).Thing;
                    if (counter == null) continue;

                    waitingCustomers++;
                    GlobalTargetInfo target = counter;
                    if (!culprits.Contains(target)) culprits.Add(target);
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
