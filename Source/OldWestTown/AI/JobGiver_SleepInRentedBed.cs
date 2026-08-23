using OldWestTown.Lords;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.AI
{
    /// <summary>
    /// Sends an already-checked-in, tired guest to the bed they paid for. Runs ahead of
    /// JobGiver_BuyFromShop in the OWT_Shop duty's think tree: a guest who's rented a room
    /// keeps browsing until they're actually tired, then goes to bed before anything else.
    /// </summary>
    public class JobGiver_SleepInRentedBed : ThinkNode_JobGiver
    {
        /// <summary>Below this fraction of the Rest need, a housed guest heads for their bed
        /// instead of continuing to browse. Tuning guess — untested in a live game.</summary>
        private const float TiredThreshold = 0.4f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null || pawn.Downed || pawn.InMentalState) return null;

            CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);
            if (record?.rentedBed == null) return null;

            // A claim can go stale before this guest ever starts sleeping — the bed destroyed,
            // or its claim simply released out from under them (the evict gizmo; the room since
            // rented to a different guest), while they were still out shopping. Unlike staleness
            // that develops *during* the sleep job (a colonist taking the bed, which
            // JobDriver_SleepInRentedBed's own FailOn already catches from its very first tick),
            // this has to be cleared right here, before a job is ever created: handing a
            // despawned bed to the driver as a job target risks the toil setup (which reads the
            // bed's position to find a sleeping slot) faulting before FailOn ever gets a chance
            // to run, and until it's cleared this customer reads as permanently checked in —
            // which would leave the whole group unable to satisfy Trigger_VisitComplete and never
            // go home, for as long as it takes this pawn to grow tired rather than for as long as
            // the claim has actually been stale. No message or reputation hit either way: the
            // guest never actually occupied the room under this stale booking, so there's
            // nothing to be legible about beyond it quietly evaporating.
            if (!(record.rentedBed is Building_Bed bed) || !bed.Spawned
                || bed.TryGetComp<CompRentableBed>()?.IsRentedBy(pawn) != true)
            {
                record.rentedBed = null;
                return null;
            }

            if ((pawn.needs?.rest?.CurLevelPercentage ?? 1f) > TiredThreshold) return null;

            // This is a one-time freshness check at job-creation, not a substitute for the
            // driver's own continuous one: staleness that develops *after* this job starts
            // (someone else claims the bed mid-walk-over, a colonist climbs in) is still left
            // entirely to JobDriver_SleepInRentedBed's own FailOn, the only place that
            // re-validates every tick the job is actually live.
            return JobMaker.MakeJob(OWTDefOf.OWT_SleepInRentedBed, bed);
        }
    }
}
