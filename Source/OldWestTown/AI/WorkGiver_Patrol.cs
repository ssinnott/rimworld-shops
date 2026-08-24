using System.Collections.Generic;
using System.Linq;
using OldWestTown.Roles;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// Sends the assigned sheriff to stand watch at their own office — the ambient half of
    /// suppression: CompRolePost.OnDuty is all any other code ever reads, the same "write a flag,
    /// let someone else read it" shape WorkGiver_ManShop already uses for staffing. Only asks for
    /// a patrol while the town actually has something worth patrolling for, so a sheriff assigned
    /// in a town with no rowdiness-capable saloon doesn't stand around for nothing.
    /// </summary>
    public class WorkGiver_Patrol : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) =>
            pawn.Map?.listerThings.ThingsOfDef(OWTDefOf.OWT_SheriffOffice) ?? Enumerable.Empty<Thing>();

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!TroubleUtility.IsAssignedSheriff(pawn)) return true;

            TownEconomy econ = pawn.Map?.GetComponent<TownEconomy>();
            if (econ == null) return true;
            return !econ.OpenShops().Any(s =>
                s.Kind != null && s.Kind.services.Any(sd => sd.worker.RowdinessPerUse > 0f));
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompRolePost post = t.TryGetComp<CompRolePost>();
            if (post == null || post.Assigned != pawn) return false;

            IntVec3 cell = post.PostCell;
            if (!cell.InBounds(t.Map) || !cell.Standable(t.Map)) return false;
            return pawn.CanReserveAndReach(cell, PathEndMode.OnCell, pawn.NormalMaxDanger(), 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompRolePost post = t.TryGetComp<CompRolePost>();
            if (post == null) return null;
            return JobMaker.MakeJob(OWTDefOf.OWT_Patrol, t, post.PostCell);
        }
    }
}
