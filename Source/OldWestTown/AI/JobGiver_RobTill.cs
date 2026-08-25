using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// Decides which till a raider goes for next — the hostile mirror of
    /// JobGiver_BuyFromShop's own scoring pass, stripped down to what a robbery actually cares
    /// about. Runs from the OWT_StickupDuty think tree, below vanilla's own
    /// JobGiver_AIFightEnemies, so self-defense always wins first.
    /// </summary>
    public class JobGiver_RobTill : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null || pawn.Downed || pawn.InMentalState) return null;

            TownEconomy econ = pawn.Map.GetComponent<TownEconomy>();
            if (econ == null) return null;

            CompBusiness bestShop = null;
            float bestScore = 0f;

            // Scans every registered business directly, not TownEconomy.OpenShops() — a robber
            // doesn't check the sign on the door. No staffed bonus either: unlike a customer's
            // own scoring, a shopkeeper standing at the counter is neither a deterrent nor an
            // attraction here. The interesting decision belongs to the player (stock, staff,
            // arm, collect), not to a robber clever enough to avoid a manned till.
            IReadOnlyList<CompBusiness> shops = econ.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                CompBusiness shop = shops[i];
                if (shop?.parent == null || !shop.parent.Spawned || shop.TillSilver <= 0) continue;
                if (!pawn.CanReach(shop.parent, PathEndMode.Touch, Danger.Deadly)) continue;

                float distanceFactor = 1f + pawn.Position.DistanceTo(shop.parent.Position) / 40f;
                float score = shop.TillSilver / distanceFactor;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestShop = shop;
                }
            }

            // Nothing left worth taking — falls through to the duty's own wander node, the same
            // way a shopped-out customer does.
            if (bestShop == null) return null;

            return JobMaker.MakeJob(OWTDefOf.OWT_RobTill, null, bestShop.parent, bestShop.CustomerCellFor(pawn));
        }
    }
}
