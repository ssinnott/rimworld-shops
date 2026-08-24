using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// A colonist being served at one of the colony's own businesses.
    ///
    /// It deliberately does NOT extend JobDriver_PatronizeBusiness. That base is a VISITOR's visit:
    /// it ends itself the moment the group's duty is swapped away, and a colonist has no duty, so
    /// it would end on the first tick; and its patience branch is a walkout — a line in the shop's
    /// ledger, a row in the town's patron table and a reputation hit in one call. A colonist must
    /// produce none of those, and the cheapest guarantee of that is a driver with no path to them:
    /// nothing below names ShopTransaction, TownEconomy, CustomerRecord or a lord. There is nothing
    /// to leak through, and a reviewer can check that with a grep instead of by reasoning about
    /// branches.
    ///
    /// What is shared is everything worth sharing: the ServiceDef, its worker's effect, the
    /// counter's staffing flag and its customer cell. Both pawns here are in the player's faction
    /// for the first time in this mod, and they still never wait on each other — the patron reads
    /// CompBusiness.Staffed exactly as a stranger does, and the shopkeeper never learns who they
    /// are serving.
    /// </summary>
    public class JobDriver_ColonistUseService : JobDriver, IBusinessPatron
    {
        private const TargetIndex CounterInd = TargetIndex.B;
        private const TargetIndex StandInd = TargetIndex.C;

        private int waitedTicks;
        private int servedTicks;
        private ServiceDef resolvedService;

        /// <summary>Not saved: re-derived from job.def, the same way Shop is re-derived from the
        /// job's targets rather than cached across a save.</summary>
        private ServiceDef Service => resolvedService ??= ServiceDef.ForJob(job.def);

        private CompBusiness Shop => job.GetTarget(CounterInd).Thing?.TryGetComp<CompBusiness>();

        private int ServeTicksRequired => Mathf.Max(1, Service?.serveTicks ?? 180);

        /// <summary>The same clock a stranger is given. A colonist standing about longer than a
        /// paying customer would is worse, not kinder.</summary>
        private int PatienceTicks => Shop?.Kind?.customerPatienceTicks ?? 2500;

        /// <summary>Answered honestly; what to do with the answer is the reader's business. The
        /// unattended-counter alert chooses to ignore a colonist — see Alert_CustomersWaiting.</summary>
        public bool WaitingForService { get; private set; }

        public bool BeingServed => servedTicks > 0;

        /// <summary>The standing cell, deliberately, and not the counter.
        ///
        /// Claiming the counter Thing would have been tidier to read and would have quietly taken
        /// away the player's only lever in this job's one real failure mode: vanilla's work-order
        /// menu asks whether the pawn can reserve the thing that was right-clicked, so a patron
        /// holding the counter greys out "prioritize shopkeeping here" for every other colonist —
        /// exactly the order a player reaches for when nobody has come to serve the patron. The
        /// shopkeeper claims the staff cell, this claims the customer cell, and neither can lock
        /// the other out. No visitor path in this mod reserves anything at all, so a traveller
        /// walking up to the same counter is unaffected either way.
        ///
        /// What stops the colony queueing ten deep at one chair is therefore the order menu, which
        /// refuses a second order while somebody is already waiting at this counter.</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(StandInd), job, 1, -1, null, errorOnFailed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref waitedTicks, "waitedTicks");
            Scribe_Values.Look(ref servedTicks, "servedTicks");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(CounterInd);
            this.FailOn(() => Shop == null || !Shop.Open || Service?.worker == null);
            AddFinishAction(condition => { WaitingForService = false; Shop?.LeaveLine(pawn); });

            yield return Toils_Goto.GotoCell(StandInd, PathEndMode.OnCell);
            yield return WaitToBeServed();
            yield return ReceiveService();
        }

        private Toil WaitToBeServed()
        {
            Toil toil = ToilMaker.MakeToil("WaitToBeServed");
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.socialMode = RandomSocialMode.Normal;
            toil.handlingFacing = true;
            toil.initAction = () => { waitedTicks = 0; servedTicks = 0; WaitingForService = false; };
            toil.tickAction = () =>
            {
                CompBusiness shop = Shop;
                ServiceDef service = Service;
                if (shop == null || service?.worker == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.rotationTracker.FaceTarget(job.GetTarget(CounterInd));

                // Neither of the two clocks below is monotone — each is zeroed by the branch the
                // other one runs in — so a counter that keeps being staffed and abandoned advances
                // neither of them to its threshold, and the colonist stands there for the rest of
                // the day holding the chair. JobDriver's own startTick is the one clock here that
                // only goes forwards, and the base already saves it, so this costs no new state.
                //
                // Never while they are actually in the chair — a running serve is the one state that
                // is not flicker — and generous enough to outlast any queue the door lets them join,
                // or it would fire on a colonist who was simply waiting their turn.
                if (startTick > 0 && !BeingServed
                    && Find.TickManager.TicksGame - startTick
                        > PatienceTicks + JobGiver_BuyFromShop.MaxQueueWaitTicks + 2 * ServeTicksRequired)
                {
                    GiveUp(shop, service);
                    return;
                }

                int place = shop.TakePlaceInLine(pawn);

                // Somebody ELSE has to be behind the counter, and the chair has to be free: it serves
                // one at a time and a colonist queues behind paying customers like anybody else. There
                // is no self-service branch here and no reading of allowsSelfService. The honesty box
                // is a bargain priced in reputation, and a colonist leaves no reputation to pay it
                // with, so for the colony's own it would simply be free and no counter would ever be
                // staffed for them again. The keeper-is-not-the-patron test is not paranoia either:
                // Staffed forgives a 60-tick gap on purpose, and 60 ticks is comfortably long enough
                // for a colonist to stop minding this counter, walk three tiles and cut their own hair.
                if (shop.Staffed && shop.Shopkeeper != pawn && place == 0)
                {
                    WaitingForService = false;
                    waitedTicks = 0;
                    // Continuous, like a stranger's: a barber who drifts off mid-cut starts the cut
                    // over rather than resuming it.
                    if (++servedTicks >= ServeTicksRequired) ReadyForNextToil();
                    return;
                }

                servedTicks = 0;

                if (shop.Staffed && shop.Shopkeeper != pawn)
                {
                    // Behind a stranger, and treated as one: the give-up clock is about a counter
                    // nobody is working, and this counter is being worked. What bounds this instead is
                    // the absolute clock above, the only one here that goes only forwards.
                    WaitingForService = false;
                    waitedTicks = 0;
                    return;
                }

                WaitingForService = true;
                if (++waitedTicks < PatienceTicks) return;
                GiveUp(shop, service);
            };
            // The serve is the only part of this with a visible cost, so it is the part that gets a
            // bar. It empties when a shopkeeper walks away, which is exactly what the player needs
            // to see.
            toil.WithProgressBar(CounterInd, () => (float)servedTicks / ServeTicksRequired);
            return toil;
        }

        /// <summary>A colonist gives up the way a neighbour would — they go and do something else.
        /// No walkout on the counter, no row in the town's books, no reputation: the books record
        /// customers and this was never one. The message is for the player because the fix is
        /// theirs, and it is not rate-limited the way a walkout's is because the player caused each
        /// one by ordering it.</summary>
        private void GiveUp(CompBusiness shop, ServiceDef service)
        {
            // Two different pieces of news, and the fix is different for each: an empty counter
            // wants somebody standing at it, while a counter that never got round to them wants a
            // second one. Telling a player to check the work tab when a colonist was already at
            // work behind that counter sends them looking for a problem that isn't there.
            string key = shop.Staffed ? "OWT_ColonistNotReached" : "OWT_ColonistGaveUp";
            Messages.Message(
                key.Translate(pawn.LabelShort, service.label, shop.parent.Label),
                new LookTargets(shop.parent), MessageTypeDefOf.NeutralEvent);
            EndJobWith(JobCondition.Incompletable);
        }

        private Toil ReceiveService()
        {
            Toil toil = ToilMaker.MakeToil("ReceiveService");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                ServiceWorker worker = Service?.worker;
                if (worker == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // The whole colonist transaction, in one line. No price, no till, no ledger, no
                // patron row, no Social XP for the barber: the effect is the only thing a
                // stranger's visit and a neighbour's have in common, and it is the only thing that
                // happens here. The shop and the service go in because a worker that claims
                // something for longer than the transaction says so through its return — nothing a
                // colonist can be ordered into does, so the return is deliberately dropped.
                worker.ApplyEffect(Shop, Service, pawn, null);
            };
            return toil;
        }
    }
}
