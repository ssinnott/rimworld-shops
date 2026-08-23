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
        }

        /// <summary>Paid-for goods (or a fetched-but-unpaid-for consumable) go into the
        /// customer's inventory as part of completing the visit, so anything still in their
        /// hands when the job ends is unpaid — whether the visit was cut short by a raid, or
        /// they walked out. Put it back.</summary>
        protected void DropCarriedOnFinish()
        {
            AddFinishAction(condition =>
            {
                WaitingForService = false;
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

                if (shop.Staffed || (SelfServiceAllowed && OldWestTownMod.Settings.allowSelfService))
                {
                    // Being attended restores patience; service has to be continuous, so a
                    // shopkeeper who drifts off mid-sale starts the serve over, not resumes it.
                    WaitingForService = false;
                    waitedTicks = 0;
                    servedTicks++;
                    if (servedTicks >= ServeTicksRequired) ReadyForNextToil();
                    return;
                }

                WaitingForService = true;
                servedTicks = 0;
                waitedTicks++;
                int patience = shop.Kind?.customerPatienceTicks ?? 2500;
                if (waitedTicks >= patience) WalkOut(shop);
            };

            return toil;
        }

        private void WalkOut(CompBusiness shop)
        {
            shop.RecordWalkout();
            shop.parent.Map?.GetComponent<TownEconomy>()?.RecordWalkout();

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
