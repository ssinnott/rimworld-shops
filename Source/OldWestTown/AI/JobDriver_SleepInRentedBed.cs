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
                // Resolved from the guest's own record, not the bed's claim — see
                // JobGiver_SleepInRentedBed's identical read (and CustomerRecord.rentedFrom's own
                // doc comment) for why: a shared bunkroom's other desk can have already re-let
                // this same bed to somebody else by the time a stale job reaches this finish
                // action, and billing off the bed would name the wrong hotel.
                CompBusiness shop = record?.rentedFrom?.TryGetComp<CompBusiness>();
                // Only release if the claim is still ours. A stale, late-processed sleep job
                // (see JobGiver_SleepInRentedBed's own staleness check) can still reach here
                // targeting a bed some other guest has since legitimately rented — releasing
                // unconditionally would evict them mid-sleep instead of no-oping on a booking
                // that already moved on.
                if (claim != null && claim.IsRentedBy(pawn)) claim.Release();
                if (record != null)
                {
                    record.rentedBed = null;
                    record.rentedFrom = null;
                }

                // Covers a bed reclaimed by a colonist, a deconstructed bed, and a raid ending
                // the job early alike — one path, matching how the shopping walkout is one path
                // regardless of what made the shopkeeper stop showing up. No refund: the room
                // was already paid for and used up to this point. ChargeEviction is shared with
                // JobGiver_SleepInRentedBed's stale-claim branch, which charges the identical
                // cost for a claim broken before the guest ever got this far.
                if (condition != JobCondition.Succeeded) ChargeEviction(pawn, record, shop);
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

        /// <summary>Charges the identical walkout-shaped cost an eviction carries, regardless of
        /// whether it happens mid-sleep (this driver's own finish action, above) or before the
        /// guest ever got this far (JobGiver_SleepInRentedBed's stale-claim branch, the job never
        /// having started). Shared rather than duplicated so the two sites can't drift out of
        /// sync about what "a walkout" writes — the same bookkeeping
        /// JobDriver_PatronizeBusiness.WalkOut already does for an ordinary shopping walkout:
        /// shop and town ledgers, this guest's own walkout count, and the shop refusal that keeps
        /// them from queueing here again while it's still unstaffed. A guest evicted before ever
        /// reaching the bed already paid in full at check-in — ShopTransaction.TryServe takes the
        /// silver before service.worker.ApplyEffect ever claims the bed — so charging this path
        /// nothing, as it once did, had the economics backwards: nothing paid, nothing gotten,
        /// and nothing charged. No-ops if the shop can't be resolved, so both call sites can pass
        /// a possibly-null shop without their own guard.</summary>
        internal static void ChargeEviction(Pawn guest, CustomerRecord record, CompBusiness shop)
        {
            if (shop == null) return;

            shop.RecordWalkout();
            shop.parent.Map?.GetComponent<TownEconomy>()?.RecordWalkout(guest);
            if (record != null)
            {
                record.walkouts++;
                record.RefuseShop(shop.parent);
            }
            Messages.Message("OWT_GuestEvicted".Translate(guest.LabelShort, shop.parent.Label),
                new LookTargets(guest), MessageTypeDefOf.NegativeEvent);
        }
    }
}
