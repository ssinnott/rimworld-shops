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
    /// nothing here waits on, or even knows about, any other pawn.
    /// </summary>
    public class JobDriver_Patrol : JobDriver
    {
        private const TargetIndex OfficeInd = TargetIndex.A;
        private const TargetIndex PostInd = TargetIndex.B;

        /// <summary>A safety valve, not a meaningful gameplay lever: ends the patrol after this
        /// long so the pawn's next think-tree tick always gets a chance to reconsider (a
        /// reassignment, a rowdy patron needing the reactive job instead), the same role
        /// IdlePatienceTicks plays for JobDriver_ManShop.</summary>
        private const int IdlePatienceTicks = 2500;

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

                if (++idleTicks >= IdlePatienceTicks) EndJobWith(JobCondition.Succeeded);
            };

            yield return watch;
        }
    }
}
