using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// Decides what a raider goes for next — the hostile mirror of JobGiver_BuyFromShop's own
    /// scoring pass, stripped down to what a robbery actually cares about. Scores every
    /// registered business's till AND every loose silver stack on its sales floor in the one
    /// pass below, so a bigger, closer floor pile can win over a smaller, farther till exactly
    /// the way it should — a stickup crew is willing to grab a loose pile as readily as it
    /// cracks a till, since the risk clock (StickupWatch) counts both the same way. Runs from
    /// the OWT_StickupDuty think tree, below vanilla's own JobGiver_AIFightEnemies, so
    /// self-defense always wins first.
    /// </summary>
    public class JobGiver_RobTill : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null || pawn.Downed || pawn.InMentalState) return null;

            TownEconomy econ = pawn.Map.GetComponent<TownEconomy>();
            if (econ == null) return null;

            float bestScore = 0f;
            CompBusiness bestTillShop = null;
            CompBusiness bestFloorShop = null;
            Thing bestFloorStack = null;

            // Scans every registered business directly, not TownEconomy.OpenShops() — a robber
            // doesn't check the sign on the door. No staffed bonus either: unlike a customer's
            // own scoring, a shopkeeper standing at the counter is neither a deterrent nor an
            // attraction here. The interesting decision belongs to the player (stock, staff,
            // arm, collect), not to a robber clever enough to avoid a manned till.
            //
            // A till and every floor stack share this one running bestScore rather than each
            // getting their own JobGiver in the duty's think tree: OWT_StickupDuty is a
            // ThinkNode_Priority, which takes the first non-null job and never compares scores
            // across siblings, so a second JobGiver would let a small, far till always beat a
            // bigger, closer floor pile purely by XML order. Same distance-decay formula for
            // both candidate kinds — a robber has no in-fiction reason to prefer one over the
            // other, only whichever is worth the walk.
            IReadOnlyList<CompBusiness> shops = econ.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                CompBusiness shop = shops[i];
                if (shop?.parent == null || !shop.parent.Spawned) continue;

                if (shop.TillSilver > 0 && pawn.CanReach(shop.parent, PathEndMode.Touch, Danger.Deadly))
                {
                    float distanceFactor = 1f + pawn.Position.DistanceTo(shop.parent.Position) / 40f;
                    float score = shop.TillSilver / distanceFactor;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTillShop = shop;
                        bestFloorShop = null;
                        bestFloorStack = null;
                    }
                }

                List<Thing> floor = shop.FloorSilverStacks;
                for (int j = 0; j < floor.Count; j++)
                {
                    Thing stack = floor[j];
                    if (stack == null || !stack.Spawned || stack.Destroyed) continue;
                    if (!pawn.CanReach(stack, PathEndMode.ClosestTouch, Danger.Deadly)) continue;

                    float distanceFactor = 1f + pawn.Position.DistanceTo(stack.Position) / 40f;
                    float score = stack.stackCount / distanceFactor;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestFloorShop = shop;
                        bestFloorStack = stack;
                        bestTillShop = null;
                    }
                }
            }

            // Nothing left worth taking — falls through to the duty's own wander node, the same
            // way a shopped-out customer does.
            if (bestFloorStack != null)
            {
                return JobMaker.MakeJob(OWTDefOf.OWT_GrabSilver, bestFloorStack, bestFloorShop.parent);
            }
            if (bestTillShop != null)
            {
                return JobMaker.MakeJob(OWTDefOf.OWT_RobTill, null, bestTillShop.parent, bestTillShop.CustomerCellFor(pawn));
            }
            return null;
        }
    }
}
