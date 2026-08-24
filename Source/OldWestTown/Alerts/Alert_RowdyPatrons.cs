using System.Collections.Generic;
using OldWestTown.Roles;
using RimWorld;
using Verse;

namespace OldWestTown.Alerts
{
    /// <summary>
    /// Raised while a patron is "getting loud" and still calmable — the window in which an
    /// assigned sheriff can still step in before the disturbance fires unattended. Mirrors
    /// Alert_CustomersWaiting's shape: a count, and a camera jump to the patrons themselves.
    /// </summary>
    public class Alert_RowdyPatrons : Alert
    {
        private readonly List<Pawn> culprits = new List<Pawn>();

        public Alert_RowdyPatrons()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel() => "OWT_AlertRowdyPatrons".Translate(culprits.Count);

        public override TaggedString GetExplanation() => "OWT_AlertRowdyPatronsDesc".Translate();

        public override AlertReport GetReport()
        {
            culprits.Clear();

            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = maps[m].mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    if (TroubleUtility.IsWorthCalming(pawns[i])) culprits.Add(pawns[i]);
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
