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
    /// A guest sleeping off a paid-for night. Never references the desk that sold the stay, the
    /// colonist who staffed it, or any other pawn — only CompRentableBed (shared state on the
    /// bed) and this pawn's own CustomerRecord. A colonist who takes the bed, or a bed that's
    /// deconstructed, is noticed here, on this driver's own FailOn — exactly the way an
    /// unattended counter is noticed by a shopper's own wait toil, never by a handshake with
    /// whoever caused it.
    /// </summary>
    public class JobDriver_SleepInRentedBed : JobDriver
    {
        private const TargetIndex BedInd = TargetIndex.A;

        /// <summary>Wake up once rested to this fraction. Tuning guess — untested in a live game.</summary>
        private const float RestedThreshold = 0.9f;

        /// <summary>Defensive hard cap independent of Need_Rest ever reporting rested — a
        /// generous night. Guarantees this job always ends in finite time, which is what makes
        /// Trigger_VisitComplete's wait for "everyone checked out" bounded.</summary>
        private const int MaxSleepTicks = 30000;

        private int ticksAsleep;

        private Building_Bed Bed => job.GetTarget(BedInd).Thing as Building_Bed;

        private CompRentableBed Claim => Bed?.TryGetComp<CompRentableBed>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksAsleep, "ticksAsleep");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(BedInd);
            this.FailOn(() =>
            {
                CompRentableBed claim = Claim;
                Building_Bed bed = Bed;
                // The second half catches a colonist who climbed into the same bed with no
                // vanilla ownership involved at all — CurOccupants is live occupancy, not
                // assignment, so it sees a casual nap that OwnersForReading never would.
                return claim == null || !claim.IsRentedBy(pawn)
                    || (bed != null && bed.CurOccupants.Any(p => p != pawn));
            });

            AddFinishAction(condition =>
            {
                CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);
                CompRentableBed claim = Claim;
                CompBusiness shop = claim?.Desk;
                // Only release if the claim is still ours. A stale, late-processed sleep job
                // (see JobGiver_SleepInRentedBed's own staleness check) can still reach here
                // targeting a bed some other guest has since legitimately rented — releasing
                // unconditionally would evict them mid-sleep instead of no-oping on a booking
                // that already moved on.
                if (claim != null && claim.IsRentedBy(pawn)) claim.Release();
                if (record != null) record.rentedBed = null;

                // Covers a bed reclaimed by a colonist, a deconstructed bed, and a raid ending
                // the job early alike — one path, matching how the shopping walkout is one path
                // regardless of what made the shopkeeper stop showing up. No refund: the room
                // was already paid for and used up to this point.
                if (condition != JobCondition.Succeeded && shop != null)
                {
                    shop.RecordWalkout();
                    shop.parent.Map?.GetComponent<TownEconomy>()?.RecordWalkout(pawn.Faction);
                    if (record != null) record.refusedShops.Add(shop.parent);
                    Messages.Message("OWT_GuestEvicted".Translate(pawn.LabelShort, shop.parent.Label),
                        new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);
                }
            });

            yield return Toils_Goto.GotoCell(RestUtility.GetBedSleepingSlotPosFor(pawn, Bed), PathEndMode.OnCell);

            // lookForOtherJobs stays false. With it on, the toil re-consults the think tree
            // every couple of hundred ticks — and JobGiver_SleepInRentedBed sits at the top of
            // that tree, still handing back a fresh sleep job for as long as the guest is tired.
            // Restarting the job would run the finish action below with a condition other than
            // Succeeded, which is the eviction path: reputation hit, message, booking cleared.
            // A guest would evict themselves on a loop while asleep. Nothing should pull a
            // customer out of a night they have already paid for anyway — the FailOn above
            // covers the bed going away, and the lord's own harmed-transition ends all jobs
            // when violence starts, which is the one interruption that should win.
            Toil sleep = Toils_LayDown.LayDown(BedInd, hasBed: true, lookForOtherJobs: false, canSleep: true, gainRestAndHealth: true);
            sleep.AddPreTickAction(() =>
            {
                ticksAsleep++;
                if ((pawn.needs?.rest?.CurLevelPercentage ?? 1f) >= RestedThreshold)
                {
                    GrantSleptThought();
                    EndJobWith(JobCondition.Succeeded);
                }
                else if (ticksAsleep >= MaxSleepTicks)
                {
                    // Safety net only, not the expected path — no thought, since nothing about
                    // this stay actually finished the way it was supposed to.
                    EndJobWith(JobCondition.Succeeded);
                }
            });
            yield return sleep;
        }

        /// <summary>Rewarded on waking, not at check-in: unlike every other service, lodging's
        /// experience is deferred, so paying for it isn't the moment that earns the mood — a
        /// full night's sleep is.</summary>
        private void GrantSleptThought()
        {
            Room room = Bed?.GetRoom();
            if (room == null) return;

            // Tuning guess: no reference for what a modest bunkroom vs. a lavish suite scores.
            float impressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
            int stage = impressiveness < 20f ? 0 : impressiveness < 60f ? 1 : 2;
            Thought_Memory thought = ThoughtMaker.MakeThought(OWTDefOf.OWT_SleptAtHotel, stage);
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
        }
    }
}
