using System.Collections.Generic;
using OldWestTown.Lords;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.AI
{
    /// <summary>
    /// Shared shape for "walk up to a business, wait to be served, either get served or give
    /// up and leave" — the half of a visit that a goods purchase and a service both need.
    ///
    /// A concrete subclass never assumes the shopkeeper is coming: it waits out the shop's
    /// patience window and, if nobody serves the customer, they leave annoyed — which is the
    /// signal the player needs that the business wants staffing.
    /// </summary>
    public abstract class JobDriver_PatronizeBusiness : JobDriver, IBusinessPatron
    {
        protected const TargetIndex CounterInd = TargetIndex.B;
        protected const TargetIndex StandInd = TargetIndex.C;

        private int waitedTicks;
        private int servedTicks;

        /// <summary>True while this customer stands at an unattended business burning patience.
        /// The unattended-counter alert reads it; not saved, since the wait toil re-derives it
        /// within a tick of loading.</summary>
        public bool WaitingForService { get; private set; }

        /// <summary>Somebody is working this customer's transaction. The wait toil zeroes
        /// <c>servedTicks</c> on any tick the counter is not attended, so this reads "a sale is in
        /// progress", not "was once" — which is what makes it safe for closing time to spare a
        /// customer on the strength of it. Saved, because the serve it measures is.</summary>
        public bool BeingServed => servedTicks > 0;

        /// <summary>The group has been called home — the hours running out, gunfire, or a lord
        /// that no longer exists. Read off the duty rather than the lord's clock, because the duty
        /// swap is the one thing every ending has in common.</summary>
        private bool VisitOver => pawn.mindState?.duty?.def != OWTDefOf.OWT_Shop;

        protected CompBusiness Shop => job.GetTarget(CounterInd).Thing?.TryGetComp<CompBusiness>();

        /// <summary>Continuous staffed ticks this visit needs: 180 for a goods sale, a
        /// per-ServiceDef value for a service.</summary>
        protected abstract int ServeTicksRequired { get; }

        /// <summary>Whether the self-service SETTING applies to this visit at all, on top of
        /// the setting itself being on. Goods: always true (unchanged behavior). A service:
        /// its own ServiceDef.allowsSelfService.</summary>
        protected virtual bool SelfServiceAllowed => true;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref waitedTicks, "waitedTicks");
            Scribe_Values.Look(ref servedTicks, "servedTicks");
        }

        protected void SetupCommonFailConditions()
        {
            this.FailOnDespawnedOrNull(CounterInd);
            this.FailOn(() => Shop == null || !Shop.Open);

            // The group is leaving and nobody is serving this customer: go home, quietly. This is
            // what bounds the grace closing time hands a serve in progress — without it a spared
            // customer whose shopkeeper stepped away would drop back into the wait branch and burn
            // the rest of their patience at the counter of a town that has already closed, and be
            // charged a walkout for a departure the clock caused.
            this.FailOn(() => VisitOver && !BeingServed);
        }

        /// <summary>Paid-for goods (or a fetched-but-unpaid-for consumable) go into the
        /// customer's inventory as part of completing the visit, so anything still in their
        /// hands when the job ends is unpaid — whether the visit was cut short by a raid, or
        /// they walked out. Put it back.</summary>
        protected void AddCommonFinishActions()
        {
            AddFinishAction(condition =>
            {
                WaitingForService = false;
                // Give the place back on the tick this job ends, however it ends. Nothing has to
                // notice: the counter would drop them on its next read anyway, but doing it here means
                // the next customer is served on the tick this one finishes.
                Shop?.LeaveLine(pawn);
                if (pawn.carryTracker?.CarriedThing != null && pawn.Map != null)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
            });
        }

        protected Toil WaitForService()
        {
            Toil toil = ToilMaker.MakeToil("WaitForService");
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.socialMode = RandomSocialMode.Normal;
            toil.handlingFacing = true;

            toil.initAction = () => { waitedTicks = 0; servedTicks = 0; WaitingForService = false; };

            toil.tickAction = () =>
            {
                CompBusiness shop = Shop;
                if (shop == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.rotationTracker.FaceTarget(job.GetTarget(CounterInd));

                // Neither clock below is monotone: being served zeroes the patience one, and being
                // ignored zeroes the serve one. A shopkeeper who takes the post and loses it over
                // and over — each spell shorter than a serve, each gap shorter than the patience
                // window — advances neither to its end, and the customer stands there for the rest
                // of the visit holding the head of the line behind them. JobDriver's own startTick
                // is the one clock here that only goes forwards, and the base already saves it.
                // Generous on purpose: it must outlast the longest queue a customer would join,
                // or it would fire on somebody who was simply waiting their turn.
                if (startTick > 0 && Find.TickManager.TicksGame - startTick
                    > PatienceFor(shop) + JobGiver_BuyFromShop.MaxQueueWaitTicks + 2 * ServeTicksRequired)
                {
                    // Counted as a walkout, because that is what it was: they stood there for hours
                    // and nobody finished serving them.
                    WalkOut(shop);
                    return;
                }

                // The honesty box is never queued. What a line rations is one shopkeeper's attention,
                // and a counter with nobody behind it has none to divide — everybody standing at it
                // helps themselves at once, which is exactly what the setting has always done.
                if (!shop.Staffed && SelfServiceAllowed && OldWestTownMod.Settings.allowSelfService)
                {
                    shop.LeaveLine(pawn);
                    WaitingForService = false;
                    waitedTicks = 0;
                    servedTicks++;
                    if (servedTicks >= ServeTicksRequired) ReadyForNextToil();
                    return;
                }

                int place = shop.TakePlaceInLine(pawn);

                if (shop.Staffed && place == 0)
                {
                    // Being attended restores patience; service has to be continuous, so a
                    // shopkeeper who drifts off mid-sale starts the serve over, not resumes it.
                    WaitingForService = false;
                    waitedTicks = 0;
                    servedTicks++;
                    if (servedTicks >= ServeTicksRequired) ReadyForNextToil();
                    return;
                }

                servedTicks = 0;

                if (shop.Staffed)
                {
                    // Somebody is being served and it is not me. The shop is doing its job, so no
                    // clock runs here at all: patience is a promise about being IGNORED, and a counter
                    // busy with somebody else is not ignoring anyone. What bounds this wait is not a
                    // stopwatch but the line — the head is never queued, so it always runs a clock
                    // that ends, and my place always comes. Charging reputation for being popular
                    // would demand a fix the player has already made.
                    WaitingForService = false;
                    waitedTicks = 0;
                    return;
                }

                WaitingForService = true;
                waitedTicks++;
                if (waitedTicks >= PatienceFor(shop)) WalkOut(shop);
            };

            return toil;
        }

        private static int PatienceFor(CompBusiness shop) => shop.Kind?.customerPatienceTicks ?? 2500;

        private void WalkOut(CompBusiness shop)
        {
            shop.RecordWalkout();
            shop.parent.Map?.GetComponent<TownEconomy>()?.RecordWalkout(pawn);

            CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);
            if (record != null)
            {
                record.walkouts++;
                // Don't queue at this counter again while it still isn't being worked. The sale
                // and the reputation are already gone; the customer isn't.
                record.RefuseShop(shop.parent);
            }

            // One message per counter per patience-window: a whole group giving up at once
            // is one piece of news, not a screenful.
            if (shop.TryClaimWalkoutMessage())
            {
                bool tookSomething = pawn.carryTracker?.CarriedThing != null;
                TaggedString msg = tookSomething
                    ? "OWT_CustomerWalkedOut".Translate(pawn.LabelShort, shop.parent.Label)
                    : "OWT_CustomerWalkedOutService".Translate(pawn.LabelShort, shop.parent.Label);
                Messages.Message(msg, new LookTargets(shop.parent), MessageTypeDefOf.NegativeEvent);
            }

            EndJobWith(JobCondition.Incompletable);
        }
    }
}
