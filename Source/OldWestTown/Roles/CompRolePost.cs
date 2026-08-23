using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OldWestTown.Roles
{
    public class CompProperties_RolePost : CompProperties_AssignableToPawn
    {
        public CompProperties_RolePost()
        {
            compClass = typeof(CompRolePost);
        }
    }

    /// <summary>
    /// A named post one specific colonist holds — "who is the sheriff", distinct from "who's
    /// currently staffing this" (CompBusiness.Staffed, which any Shopkeeping-priority colonist
    /// can satisfy). Built on vanilla's own CompAssignableToPawn, the same idiom a throne room
    /// or a grave already uses for "this pawn, and only this pawn, owns this" — not a bespoke
    /// assignment system. CompAssignableToPawn is not abstract and needs no overrides to
    /// compile; AssigningCandidates (narrowed to free colonists) and CanAssignTo (enforcing the
    /// XML-configured MaxAssignedPawnsCount, which the base class reads but never itself checks
    /// against AssignedPawnsForReading) are the two worth adding here.
    /// PostExposeData is likewise left alone: the base class already persists the assignment
    /// itself, which is the only thing here that needs to survive a save.
    /// </summary>
    public class CompRolePost : CompAssignableToPawn
    {
        /// <summary>Mirrors CompBusiness.StaffPresenceGraceTicks exactly — a short buffer so a
        /// one-tick gap between patrol toils doesn't flicker OnDuty off and back. Deliberately
        /// unpersisted, like CompBusiness's own lastStaffedTick: a reload re-establishes on-duty
        /// status within moments once the assigned pawn's patrol job re-ticks.</summary>
        private const int OnDutyGraceTicks = 60;

        private int lastOnDutyTick = -99999;
        private Pawn lastOnDutyPawn;

        public override IEnumerable<Pawn> AssigningCandidates =>
            parent.Map?.mapPawns.FreeColonistsSpawned ?? Enumerable.Empty<Pawn>();

        /// <summary>The base class never actually checks AssignedPawnsForReading.Count against
        /// MaxAssignedPawnsCount itself — that enforcement is opt-in per subclass (vanilla's own
        /// bed/grave comps do it by delegating to a pawn-side ownership tracker instead). Without
        /// this override, <maxAssignedPawnsCount>1</maxAssignedPawnsCount> in XML sets a number
        /// nothing ever reads, and a second "Assign" click quietly makes a second pawn count as
        /// the sheriff. Dialog_AssignBuildingOwner already greys out a row and shows the reason
        /// when CanAssignTo rejects it, so this alone closes the gap.</summary>
        public override AcceptanceReport CanAssignTo(Pawn pawn)
        {
            AcceptanceReport baseResult = base.CanAssignTo(pawn);
            if (!baseResult.Accepted) return baseResult;

            if (!AssignedPawnsForReading.Contains(pawn) && AssignedPawnsForReading.Count >= MaxAssignedPawnsCount)
            {
                return "OWT_PostAlreadyFilled".Translate();
            }
            return true;
        }

        /// <summary>The one pawn holding this post, or null. Reads AssignedPawnsForReading
        /// rather than caching, so unassigning through the vanilla gizmo is picked up
        /// immediately.</summary>
        public Pawn Assigned => AssignedPawnsForReading.Count > 0 ? AssignedPawnsForReading[0] : null;

        /// <summary>Where the post-holder stands watch — the building's own interaction cell,
        /// the same "rotate the building to place the post" idiom CompBusiness.StaffCell already
        /// uses.</summary>
        public IntVec3 PostCell => parent.InteractionCell;

        /// <summary>True while the assigned pawn is actively on patrol here right now. Passive
        /// shared state — TroubleUtility reads it, nothing here ever waits on the job that sets
        /// it.</summary>
        public bool OnDuty =>
            lastOnDutyPawn != null
            && !lastOnDutyPawn.Dead
            && Find.TickManager.TicksGame - lastOnDutyTick <= OnDutyGraceTicks;

        /// <summary>Called every tick by JobDriver_Patrol while the assigned pawn stands the
        /// post. Mirrors CompBusiness.NotifyStaffedBy exactly.</summary>
        public void NotifyOnDuty(Pawn pawn)
        {
            lastOnDutyPawn = pawn;
            lastOnDutyTick = Find.TickManager.TicksGame;
        }

        // Both are protected on the base class — not public, despite being called from outside
        // it (CompGetGizmosExtra calls them internally). Confirmed by the compiler, not just by
        // reflection: refdump's own member dump reports virtual/override flags but not
        // accessibility, so this only surfaced once the build actually ran.
        protected override string GetAssignmentGizmoLabel() => "OWT_CmdAssignSheriff".Translate();

        protected override string GetAssignmentGizmoDesc() => "OWT_CmdAssignSheriffDesc".Translate();

        public override string CompInspectStringExtra()
        {
            Pawn assigned = Assigned;
            if (assigned == null) return "OWT_PostVacant".Translate();
            return OnDuty
                ? "OWT_PostOnDuty".Translate(assigned.LabelShort)
                : "OWT_PostOffDuty".Translate(assigned.LabelShort);
        }
    }
}
