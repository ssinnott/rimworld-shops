using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace OldWestTown.Shops
{
    /// <summary>Decides what counts as "on the shelves" for a given counter.</summary>
    public static class ShopStock
    {
        /// <summary>
        /// Indoors, the sales floor is the counter's room — build walls and you have defined a
        /// shop. Outdoors (a market stall, a boardwalk table) it falls back to a radius, so an
        /// open-air town square still trades.
        /// </summary>
        public static IEnumerable<Thing> ScanFor(CompShopCounter shop)
        {
            Thing counter = shop?.parent;
            Map map = counter?.Map;
            if (map == null) yield break;

            Room room = counter.GetRoom();
            if (room != null && !room.PsychologicallyOutdoors && !room.TouchesMapEdge)
            {
                List<Thing> contained = room.ContainedAndAdjacentThings;
                for (int i = 0; i < contained.Count; i++)
                {
                    Thing t = contained[i];
                    // ContainedAndAdjacentThings also lists things merely next to the room —
                    // in a doorway, or just outside the walls. Only what is inside is on sale.
                    if (t.GetRoom() != room) continue;
                    if (Sellable(shop, t, map)) yield return t;
                }
                yield break;
            }

            float radius = shop.Props.openAirRadius;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(counter.Position, radius, true))
            {
                if (!cell.InBounds(map)) continue;
                List<Thing> here = cell.GetThingList(map);
                for (int i = 0; i < here.Count; i++)
                {
                    if (Sellable(shop, here[i], map)) yield return here[i];
                }
            }
        }

        private static bool Sellable(CompShopCounter shop, Thing t, Map map)
        {
            if (t == null || !t.Spawned || t.Destroyed) return false;
            if (t.def.category != ThingCategory.Item) return false;
            if (t.def == ThingDefOf.Silver) return false;
            if (t.stackCount <= 0) return false;

            // Forbidding an item is the player's way of saying "not for sale".
            if (t.IsForbidden(Faction.OfPlayer)) return false;

            // Goods a colonist has reserved are already promised elsewhere — a hauler is on the
            // way. Selling them out from under the job would churn both sides.
            if (map.reservationManager.IsReservedByAnyoneOf(t, Faction.OfPlayer)) return false;

            if (t.IsBurning()) return false;
            if (ShopPricing.UnitValue(t) <= 0f) return false;

            return shop.StockFilter.Allows(t);
        }

        /// <summary>
        /// Picks what a given customer would like to buy from this shop, or null if nothing here
        /// tempts them or nothing is within their means.
        /// </summary>
        public static Thing ChoosePurchase(CompShopCounter shop, Pawn customer, int budget, out int count)
        {
            count = 0;
            List<Thing> stock = shop.StockOnDisplay;
            if (stock.Count == 0) return null;

            Thing best = null;
            int bestCount = 0;
            float bestScore = 0f;

            for (int i = 0; i < stock.Count; i++)
            {
                Thing t = stock[i];
                if (!t.Spawned || t.Destroyed) continue;

                int unitPrice = ShopPricing.PriceFor(shop, t, 1);
                if (unitPrice > budget) continue;

                // Buy as much of the stack as they can afford, capped so one shopper can't
                // strip a shelf in a single visit.
                int affordable = unitPrice > 0 ? budget / unitPrice : 0;
                int wanted = Mathf.Clamp(affordable, 1, Mathf.Min(t.stackCount, MaxUnitsPerPurchase(t)));
                if (wanted <= 0) continue;

                if (!customer.CanReach(t, PathEndMode.ClosestTouch, Danger.Deadly)) continue;

                // Prefer things that are worth the walk, then break ties randomly so a queue of
                // customers doesn't all converge on the single most expensive item.
                float score = ShopPricing.UnitValue(t) * wanted * Rand.Range(0.6f, 1.4f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                    bestCount = wanted;
                }
            }

            count = bestCount;
            return best;
        }

        private static int MaxUnitsPerPurchase(Thing t)
        {
            // Stackable consumables move in bulk; gear goes one at a time.
            return t.def.stackLimit > 1 ? Mathf.Max(1, t.def.stackLimit / 4) : 1;
        }
    }
}
