using System.Collections.Generic;
using System.Linq;
using OldWestTown.Roles;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// Sends the assigned sheriff to defuse one specific patron before they boil over — the
    /// reactive half of suppression. Targets a Pawn directly rather than a building, the same
    /// way JobDriver_SleepInRentedBed's own JobGiver targets a bed: nothing here is a handshake
    /// with the patron's own job, which never references the sheriff and has no idea one exists.
    /// </summary>
    public class WorkGiver_CalmTrouble : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map?.mapPawns.AllPawnsSpawned.Where(TroubleUtility.IsWorthCalming)
                ?? Enumerable.Empty<Pawn>();
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false) =>
            !TroubleUtility.IsAssignedSheriff(pawn);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            t is Pawn target
            && TroubleUtility.IsWorthCalming(target)
            && pawn.CanReserveAndReach(target, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced);

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            t is Pawn ? JobMaker.MakeJob(OWTDefOf.OWT_CalmTrouble, t) : null;
    }
}
