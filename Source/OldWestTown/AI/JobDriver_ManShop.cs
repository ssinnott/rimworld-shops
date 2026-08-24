using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// A colonist standing the counter. The job holds them there and reports their presence to
    /// the shop each tick; the customer driver reads that flag. Neither pawn waits on the other,
    /// so a shopkeeper who breaks for lunch just makes the shop unattended, not broken.
    /// </summary>
    public class JobDriver_ManShop : JobDriver
    {
        private const TargetIndex CounterInd = TargetIndex.A;
        private const TargetIndex PostInd = TargetIndex.B;

        /// <summary>Stop manning the counter after this long with no customer in sight.</summary>
        private const int IdlePatienceTicks = 1250;

        /// <summary>How often to re-ask whether anyone is still shopping here. The scan walks every
        /// pawn on the map and checks each candidate's purse — not worth doing every tick for an
        /// answer that changes on the scale of a walk across the room.</summary>
        private const int CustomerScanInterval = 30;

        private int idleTicks;

        /// <summary>Last scan's answer, and whether one has run yet on this shift. Neither is
        /// scribed: a shopkeeper resuming from a save re-scans on their first tick rather than
        /// trusting a remembered answer, which also keeps the scribed idleTicks meaningful — a
        /// cached "somebody's here" would zero the idle counter the save just restored.</summary>
        private bool customerNear;
        private bool scanned;

        private CompBusiness Shop => job.GetTarget(CounterInd).Thing?.TryGetComp<CompBusiness>();

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
            this.FailOnDespawnedOrNull(CounterInd);
            this.FailOn(() => Shop == null || !Shop.Open);

            yield return Toils_Goto.GotoCell(PostInd, PathEndMode.OnCell);

            Toil tend = ToilMaker.MakeToil("TendCounter");
            tend.defaultCompleteMode = ToilCompleteMode.Never;
            tend.handlingFacing = true;
            tend.socialMode = RandomSocialMode.Normal;
            tend.initAction = () => { idleTicks = 0; scanned = false; };
            tend.tickAction = () =>
            {
                CompBusiness shop = Shop;
                if (shop == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                shop.NotifyStaffedBy(pawn);
                pawn.rotationTracker.FaceCell(shop.parent.Position);

                if (!scanned || pawn.IsHashIntervalTick(CustomerScanInterval))
                {
                    customerNear = WorkGiver_ManShop.AnyCustomerNear(shop);
                    scanned = true;
                }

                if (customerNear)
                {
                    idleTicks = 0;
                }
                else if (++idleTicks >= IdlePatienceTicks)
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };

            yield return tend;
        }
    }
}
