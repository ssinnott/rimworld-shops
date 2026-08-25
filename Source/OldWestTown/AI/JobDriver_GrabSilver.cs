using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// Grabbing a loose silver stack off a shop's own sales floor — exactly where Collect
    /// takings leaves it, and exactly why collecting a till was never the same as clearing the
    /// risk it carries. A plain JobDriver, the floor-silver twin of JobDriver_RobTill: walk up, a
    /// short delay, take the stack.
    ///
    /// Deliberately does NOT implement IBusinessPatron, for the identical reason JobDriver_RobTill
    /// doesn't: that marker is what WorkGiver_ManShop, CompBusiness.CellFreeFor and
    /// Alert_CustomersWaiting all use to recognize "a paying customer is here", and none of that
    /// machinery should ever mistake a raider grabbing loot for one.
    /// </summary>
    public class JobDriver_GrabSilver : JobDriver
    {
        private const TargetIndex StackInd = TargetIndex.A;
        private const TargetIndex ShopInd = TargetIndex.B;

        /// <summary>Half JobDriver_RobTill's own CrackTicks (180) — grabbing a pile already sitting
        /// out in the open is quicker than working a lock. A first-pass guess, wants the same
        /// playtest every stickup constant does.</summary>
        private const int SnatchTicks = 90;

        /// <summary>Bookkeeping and message flavor only — never what this job reserves or walks
        /// to. May legitimately be null by the time this reads it (the counter deconstructed
        /// mid-job); the silver is real, and worth taking, whether or not its originating
        /// business still exists. See ShopTransaction.GrabFloorSilver.</summary>
        private CompBusiness Shop => job.GetTarget(ShopInd).Thing?.TryGetComp<CompBusiness>();

        // Reserving the stack itself, not a cell — the correct vanilla idiom for a contested
        // loose item, and what lets a raider and a legitimate hauler race for the same pile with
        // the loser failing gracefully, the same accepted race this codebase already documents
        // for stock and hotel beds.
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(StackInd), job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(StackInd);
            // Deliberately no forbidden check, mirroring JobDriver_RobTill's own "robbing a
            // closed till is the entire point": forbidding a stack must not be a way to make it
            // stop counting toward risk without actually moving it.

            yield return Toils_Goto.GotoThing(StackInd, PathEndMode.ClosestTouch);

            // A short "snatching it up" delay, facing the stack itself — unlike RobTill's
            // standing-cell-plus-counter pair, this job walks straight to the one thing it
            // cares about, so there's no second target to face instead.
            yield return Toils_General.Wait(SnatchTicks, StackInd);

            yield return Grab();
        }

        private Toil Grab()
        {
            Toil toil = ToilMaker.MakeToil("Grab");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                Thing stack = job.GetTarget(StackInd).Thing;
                CompBusiness shop = Shop;

                int taken = ShopTransaction.GrabFloorSilver(shop, stack, pawn);
                if (taken > 0 && shop != null && shop.TryClaimFloorRobberyMessage())
                {
                    Messages.Message(
                        "OWT_FloorSilverGrabbed".Translate(pawn.LabelShort, shop.parent.Label, ((float)taken).ToStringMoney()),
                        new LookTargets(shop.parent), MessageTypeDefOf.NegativeEvent);
                }
            };
            return toil;
        }
    }
}
