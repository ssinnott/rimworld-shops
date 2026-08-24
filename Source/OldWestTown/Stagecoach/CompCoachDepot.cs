using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using OldWestTown.Shops;

namespace OldWestTown.Stagecoach
{
    public class CompProperties_CoachDepot : CompProperties
    {
        public CompProperties_CoachDepot()
        {
            compClass = typeof(CompCoachDepot);
        }
    }

    /// <summary>
    /// A passive marker, the same shape as <c>CompRolePost</c> and <see cref="CompFalseFront"/>:
    /// never staffed, never targeted by a job, never read by another pawn's loop. Its only
    /// behaviour is telling the player what the town's route is doing right now, read live off
    /// <see cref="TownEconomy"/> and <see cref="CoachTierUtility"/> — there is nothing here to
    /// persist.
    /// </summary>
    public class CompCoachDepot : ThingComp
    {
        public override string CompInspectStringExtra()
        {
            TownEconomy econ = parent.Map?.GetComponent<TownEconomy>();
            if (econ == null) return null;   // shouldn't happen on a player map; defensive only

            CoachTierDef tier = econ.RouteTier;
            CoachTierDef next = CoachTierUtility.NextTier(tier);

            // Only reachable if a mod strips every CoachTierDef out from under an already-built
            // depot -- degrade to one honest line rather than also claiming to be "at the top"
            // of a ladder that no longer exists.
            if (tier == null && next == null) return "OWT_DepotNoTiers".Translate();

            StringBuilder sb = new StringBuilder();

            if (tier == null)
            {
                sb.AppendLine("OWT_DepotNoRoute".Translate(next.minAppeal.ToString("0.0")));
            }
            else
            {
                sb.AppendLine("OWT_DepotTierLine".Translate(tier.LabelCap));
                int ticksLeft = Mathf.Max(0, Mathf.RoundToInt(
                    CoachTierUtility.CeilingTicks(tier) - econ.TicksSinceLastArrival));
                sb.AppendLine("OWT_DepotNextArrivalLine".Translate(GenDate.ToStringTicksToPeriod(ticksLeft)));
            }

            if (next != null)
            {
                sb.Append("OWT_DepotNextTierLine".Translate(
                    next.LabelCap, next.minAppeal.ToString("0.0"), econ.Appeal.ToString("0.0")));
            }
            else
            {
                sb.Append("OWT_DepotMaxTierLine".Translate());
            }

            return sb.ToString().TrimEndNewlines();
        }
    }
}
