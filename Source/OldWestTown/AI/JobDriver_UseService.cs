using System.Collections.Generic;
using System.Linq;
using OldWestTown.Lords;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.AI
{
    /// <summary>
    /// One service visit, start to finish. For a stock-consuming service (Drink, Meal) that's
    /// fetch the item, carry it to the stand, wait to be served, pay, drink/eat it. For a
    /// stock-free one (Haircut) the fetch step is skipped entirely — there's nothing to carry.
    /// </summary>
    public class JobDriver_UseService : JobDriver_PatronizeBusiness
    {
        private const TargetIndex ConsumableInd = TargetIndex.A;
        private const int BrowseTicks = 120;

        private ServiceDef resolvedService;

        /// <summary>Which ServiceDef this job is for, recovered from the JobDef — Job has no
        /// generic Def-reference slot, so a JobDef-to-ServiceDef lookup is how a service driver
        /// knows what it's running. Not saved: re-derived from job.def, the same way Shop is
        /// re-derived from job targets rather than cached across saves.</summary>
        private ServiceDef ResolvedService =>
            resolvedService ??= DefDatabase<ServiceDef>.AllDefsListForReading
                .FirstOrDefault(sd => sd.jobDef == job.def);

        protected override int ServeTicksRequired => ResolvedService?.serveTicks ?? 180;

        protected override bool SelfServiceAllowed => ResolvedService?.allowsSelfService ?? false;

        private Thing Consumable => job.GetTarget(ConsumableInd).Thing;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SetupCommonFailConditions();
            this.FailOn(() => ResolvedService == null);
            DropCarriedOnFinish();

            // Guard the dereference, not just the FailOn: FailOn only ends the job on the next
            // tick check, and a null ResolvedService here would otherwise throw before that
            // check ever runs. In practice ResolvedService is never null for a job the
            // JobGiver actually created; CompleteService() re-checks and ends the job cleanly
            // if this defensive branch is ever hit some other way.
            ServiceDef service = ResolvedService;
            if (service?.worker?.ConsumesStock == true)
            {
                // 1. Go and look at the item on the shelf.
                yield return Toils_Goto.GotoThing(ConsumableInd, PathEndMode.ClosestTouch)
                    .FailOnDespawnedNullOrForbidden(ConsumableInd);

                // 2. Browse — shorter than a goods purchase; ordering a drink is quicker than
                // picking over the shelves.
                Toil browse = Toils_General.Wait(BrowseTicks, ConsumableInd);
                browse.FailOnDespawnedNullOrForbidden(ConsumableInd);
                browse.WithProgressBarToilDelay(ConsumableInd);
                browse.socialMode = RandomSocialMode.Normal;
                yield return browse;

                // 3. Pick it up and carry it to the stand.
                yield return Toils_Haul.StartCarryThing(ConsumableInd, false, true, false, false, false)
                    .FailOnDespawnedNullOrForbidden(ConsumableInd);
            }

            yield return Toils_Goto.GotoCell(StandInd, PathEndMode.OnCell);

            // Wait to be served.
            yield return WaitForService();

            // Pay up and receive the effect.
            yield return CompleteService();
        }

        private Toil CompleteService()
        {
            Toil toil = ToilMaker.MakeToil("CompleteService");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                CompBusiness shop = Shop;
                ServiceDef service = ResolvedService;
                bool consumesStock = service?.worker?.ConsumesStock == true;
                Thing consumed = consumesStock ? (pawn.carryTracker?.CarriedThing ?? Consumable) : null;
                if (shop == null || service?.worker == null || (consumesStock && consumed == null))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                ShopTransaction.Result result =
                    ShopTransaction.TryServe(shop, pawn, service, consumed, out int price, out Thing claimed);

                if (result != ShopTransaction.Result.Sold)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);
                if (record != null)
                {
                    record.spent += price;
                    record.purchases++;
                    // The one place a Shops-layer output (what ApplyEffect claimed) crosses
                    // into Lords-layer state (whose stay this is) — already the file that
                    // depends on both, so it's the natural seam rather than a new one.
                    if (claimed is Building_Bed bed) record.rentedBed = bed;
                }
            };
            return toil;
        }
    }
}
