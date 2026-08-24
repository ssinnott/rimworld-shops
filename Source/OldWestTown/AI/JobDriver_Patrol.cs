using System.Collections.Generic;
using OldWestTown.Roles;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// The sheriff standing their post. Purely ambient, mirroring JobDriver_ManShop exactly:
    /// this only ever writes a shared flag (CompRolePost.NotifyOnDuty) for other code to read —
    /// nothing here waits on, or even knows about, any other pawn. The one addition beyond that
    /// mirror is the trouble poll below, and it keeps the same shape: a passive, map-wide "is
    /// there anyone worth calming" query, never a reference to a specific rowdy pawn's job.
    /// </summary>
    public class JobDriver_Patrol : JobDriver
    {
        private const TargetIndex OfficeInd = TargetIndex.A;
        private const TargetIndex PostInd = TargetIndex.B;

        /// <summary>A safety valve, not a meaningful gameplay lever: ends the patrol after this
        /// long so the pawn's next think-tree tick always gets a chance to reconsider a
        /// reassignment or the office being lost. Matches JobDriver_ManShop's own
        /// IdlePatienceTicks — noticing a rowdy patron is no longer this constant's job at all,
        /// see the trouble poll below.</summary>
        private const int IdlePatienceTicks = 1250;

        /// <summary>How often the tick loop checks for a patron worth calming. OWT_CalmDownPatron
        /// outranking OWT_PatrolPost by priorityInType only decides which WorkGiver wins once
        /// this pawn is jobless again — it does nothing to preempt a toil already running with
        /// ToilCompleteMode.Never, so this ends the patrol itself the moment there's someone to
        /// calm, rather than waiting out the full idle safety valve above. Throttled rather than
        /// checked every tick, since it's a full pawn-list scan (TroubleUtility.AnyoneWorthCalming)
        /// and half a second of extra latency is nothing against the alternative.</summary>
        private const int TroubleCheckIntervalTicks = 30;

        private int idleTicks;

        private CompRolePost Post => job.GetTarget(OfficeInd).Thing?.TryGetComp<CompRolePost>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(PostInd), job, 1, -1, null, errorOnFailed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref idleTicks, "idleTicks");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(OfficeInd);
            // A mid-job reassignment (or the office itself being lost) ends the stale patrol
            // promptly, rather than leaving a ghost "on duty" flag ticking from a pawn who's no
            // longer the badge-holder.
            this.FailOn(() => Post == null || !Post.AssignedPawnsForReading.Contains(pawn));

            yield return Toils_Goto.GotoCell(PostInd, PathEndMode.OnCell);

            Toil watch = ToilMaker.MakeToil("Patrol");
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            watch.handlingFacing = true;
            watch.socialMode = RandomSocialMode.Normal;
            watch.initAction = () => idleTicks = 0;
            watch.tickAction = () =>
            {
                CompRolePost post = Post;
                if (post == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                post.NotifyOnDuty(pawn);
                pawn.rotationTracker.FaceCell(post.parent.Position);

                if (Find.TickManager.TicksGame % TroubleCheckIntervalTicks == 0
                    && TroubleUtility.AnyoneWorthCalming(pawn.Map))
                {
                    // JobCondition.InterruptForced, not Succeeded: this patrol didn't finish,
                    // it's stepping aside. The pawn goes jobless immediately after, and ordinary
                    // WorkGiver reselection — which does respect priorityInType — picks
                    // OWT_CalmDownPatron up from there.
                    EndJobWith(JobCondition.InterruptForced);
                    return;
                }

                if (++idleTicks >= IdlePatienceTicks) EndJobWith(JobCondition.Succeeded);
            };

            yield return watch;
        }
    }
}
