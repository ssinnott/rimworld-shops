using System.Collections.Generic;
using OldWestTown.Roles;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// The sheriff talking a patron down before they boil over. Targets one specific pawn and
    /// unilaterally resets their rowdiness (TroubleUtility.CalmDown) — the target's own job never
    /// references the sheriff, never waits on them, and has no idea a sheriff exists. If the
    /// sheriff is drafted, downed, or this job is interrupted mid-walk, the target's rowdiness
    /// simply keeps accruing or decaying on its own passive schedule; nothing about the target
    /// depends on this job ever completing — the same failure shape an unattended counter
    /// already has for a waiting customer.
    /// </summary>
    public class JobDriver_CalmTrouble : JobDriver
    {
        private const TargetIndex TargetInd = TargetIndex.A;
        private const int CalmTicks = 200;

        private Pawn Target => job.GetTarget(TargetInd).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetInd), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetInd);
            this.FailOn(() => Target == null || Target.Dead || Target.Downed || !TroubleUtility.IsWorthCalming(Target));

            yield return Toils_Goto.GotoThing(TargetInd, PathEndMode.Touch);

            Toil calm = Toils_General.Wait(CalmTicks, TargetInd);
            calm.socialMode = RandomSocialMode.Normal;
            yield return calm;

            Toil finish = ToilMaker.MakeToil("CalmTrouble");
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            finish.initAction = () =>
            {
                // Wait is time-based, not proximity-maintained — this is the first job in this
                // mod that targets a moving Pawn rather than a fixed counter or bed, so a target
                // whose own job walked them off mid-wait is a real possibility, not just a
                // theoretical one. A no-op here (no reset, no XP) is the graceful failure; it is
                // never a false "success" credited from across the map.
                if (Target != null && pawn.Position.DistanceTo(Target.Position) <= 3f)
                {
                    TroubleUtility.CalmDown(Target);
                    pawn.skills?.Learn(SkillDefOf.Social, 35f);
                }
            };
            yield return finish;
        }
    }
}
