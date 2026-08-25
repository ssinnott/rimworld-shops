using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// Cracking a till: walk to the counter, a short delay, take everything in it. A plain
    /// JobDriver, not JobDriver_PatronizeBusiness — there's no fetch step, no wait-for-service
    /// toil, no patience or walkout logic, because none of that shape fits a robbery.
    ///
    /// Deliberately does NOT implement IBusinessPatron. That marker is what WorkGiver_ManShop,
    /// CompBusiness.CellFreeFor and Alert_CustomersWaiting all use to recognize "a paying
    /// customer is here" — implementing it here would make an unarmed colonist get dispatched to
    /// staff the counter a robber is actively cracking, and would make the waiting-customers
    /// alert and the queue fan-out logic misread an active robbery as an ordinary queue. None of
    /// that machinery should ever see a robber as a customer.
    /// </summary>
    public class JobDriver_RobTill : JobDriver
    {
        private const TargetIndex CounterInd = TargetIndex.B;
        private const TargetIndex StandInd = TargetIndex.C;

        private const int CrackTicks = 180;

        private CompBusiness Shop => job.GetTarget(CounterInd).Thing?.TryGetComp<CompBusiness>();

        // Reserving the standing cell (mirroring JobDriver_ManShop's own reservation of its post
        // cell) is what stops two raiders scored onto the same best-till in the same tick from
        // literally standing on each other: the loser's reservation fails, the job aborts
        // gracefully, and the think tree re-scores next tick — the same accepted race this
        // codebase already documents for stock and hotel beds.
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(StandInd), job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(CounterInd);
            this.FailOn(() => Shop == null);
            // Deliberately no !Shop.Open check, unlike JobDriver_PatronizeBusiness's own common
            // fail conditions — robbing a closed till is the entire point.

            yield return Toils_Goto.GotoCell(StandInd, PathEndMode.OnCell);

            // A short "cracking the till" delay, facing the counter itself (not the standing
            // cell — Toils_General.Wait's second argument is who to face, and a pawn "facing"
            // the cell it's already standing on is meaningless). Also gives a colonist or a
            // turret a visible window to react before any silver actually moves.
            yield return Toils_General.Wait(CrackTicks, CounterInd);

            yield return Rob();
        }

        private Toil Rob()
        {
            Toil toil = ToilMaker.MakeToil("Rob");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                CompBusiness shop = Shop;
                // Re-validated immediately before silver moves, the same discipline
                // ShopTransaction applies everywhere else: the till may have come up empty in
                // the meantime — the player collected first, or another raider got there first.
                // A graceful no-op, not a failure, the same way an emptied shelf already is for
                // an ordinary customer.
                if (shop == null || shop.TillSilver <= 0) return;

                int taken = ShopTransaction.RobTill(shop, pawn);
                if (taken > 0 && shop.TryClaimRobberyMessage())
                {
                    Messages.Message(
                        "OWT_TillRobbed".Translate(pawn.LabelShort, shop.parent.Label, ((float)taken).ToStringMoney()),
                        new LookTargets(shop.parent), MessageTypeDefOf.NegativeEvent);
                }
            };
            return toil;
        }
    }
}
