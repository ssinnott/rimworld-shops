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
        public static IEnumerable<Thing> ScanFor(CompBusiness shop)
        {
            Map map = shop?.parent?.Map;
            foreach (Thing t in ThingsOnFloor(shop))
            {
                if (Sellable(shop, t, map)) yield return t;
            }
        }

        /// <summary>
        /// The raw candidate set a shop's sales floor contains — everything in its room, or
        /// within its open-air radius outdoors — before any per-Thing filter is applied. Shared
        /// by ScanFor (goods and stock-backed services, filtered through Sellable) and
        /// ChooseVacantBed/CountBeds (rentable beds, filtered by type instead): both care about
        /// "what's on this floor", they just disagree about what counts.
        /// </summary>
        private static IEnumerable<Thing> ThingsOnFloor(CompBusiness shop)
        {
            Thing counter = shop?.parent;
            Map map = counter?.Map;
            if (map == null) yield break;

            Room room = shop.SalesFloorRoom;
            if (room != null)
            {
                List<Thing> contained = room.ContainedAndAdjacentThings;
                for (int i = 0; i < contained.Count; i++)
                {
                    Thing t = contained[i];
                    // ContainedAndAdjacentThings also lists things merely next to the room —
                    // in a doorway, or just outside the walls. Only what is inside is on sale.
                    if (t.GetRoom() != room) continue;
                    yield return t;
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
                    yield return here[i];
                }
            }
        }

        private static bool Sellable(CompBusiness shop, Thing t, Map map)
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
        /// tempts them or nothing is within their means. <paramref name="skipDefs"/> is what this
        /// particular customer has already given up on here — passing it in, rather than filtering
        /// the winner afterwards, is what lets the runner-up stack win instead of the shop dropping
        /// out of the running entirely.
        /// </summary>
        public static Thing ChoosePurchase(CompBusiness shop, Pawn customer, int budget, out int count,
            List<ThingDef> skipDefs = null)
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
                if (skipDefs != null && skipDefs.Contains(t.def)) continue;

                // Buy as much of the stack as they can afford, capped so one shopper can't
                // strip a shelf in a single visit.
                int cap = Mathf.Min(t.stackCount, MaxUnitsPerPurchase(t));
                int wanted = ShopPricing.MaxAffordable(shop, t, budget, cap);
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

        /// <summary>Whether anything on display could back this service right now.
        ///
        /// Separate from <see cref="ChooseService"/> because it is a different question. "Is there
        /// any" has no tie to break, and the roll ChooseService uses to break one is a draw from the
        /// shared, seeded game stream — the same stream that rolls raids, damage and quality.
        /// Answering an availability check with it made selecting a counter change what the
        /// storyteller did next.</summary>
        public static bool HasStockFor(CompBusiness shop, ServiceDef service)
        {
            if (service?.worker == null || !service.worker.ConsumesStock) return false;
            List<Thing> stock = shop.StockOnDisplay;
            for (int i = 0; i < stock.Count; i++)
            {
                Thing t = stock[i];
                if (t.Spawned && !t.Destroyed && service.worker.CanUse(t)) return true;
            }
            return false;
        }

        /// <summary>The item on display this service can use for a given customer, or null if nothing
        /// currently qualifies or they cannot path to any of it. Which stack is a choice, and the
        /// spread below is what keeps a queue of customers off one stack — which is also why this may
        /// only be called for a customer to roll for. Code that only wants to know whether the
        /// service is on offer at all asks <see cref="HasStockFor"/> instead.</summary>
        public static Thing ChooseService(CompBusiness shop, ServiceDef service, Pawn customer)
        {
            if (service?.worker == null || !service.worker.ConsumesStock) return null;
            List<Thing> stock = shop.StockOnDisplay;

            Thing best = null;
            float bestScore = 0f;

            for (int i = 0; i < stock.Count; i++)
            {
                Thing t = stock[i];
                if (!t.Spawned || t.Destroyed || !service.worker.CanUse(t)) continue;
                if (!customer.CanReach(t, PathEndMode.ClosestTouch, Danger.Deadly)) continue;

                // Same tie-break as ChoosePurchase: without it, every customer scoring this
                // service against the same shelf snapshot picks the identical first-qualifying
                // stack and queues for it instead of spreading across equally good ones.
                float score = ShopPricing.UnitValue(t) * Rand.Range(0.6f, 1.4f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best;
        }

        /// <summary>The first vacant rentable bed on this shop's sales floor, or null. A bed
        /// has no "better" the way a priced stack does, so unlike ChoosePurchase/ChooseService
        /// there's no score to tie-break on — first vacant one found is as good as any other.
        /// Pass <paramref name="customer"/> to also require they can actually reach it —
        /// ServiceWorker_Lodging.IsAvailable calls this customer-agnostically, the same
        /// customer=null shape ChooseService already uses.</summary>
        public static Thing ChooseVacantBed(CompBusiness shop, Pawn customer = null)
        {
            foreach (Thing t in ThingsOnFloor(shop))
            {
                if (!(t is Building_Bed bed) || !t.Spawned || t.Destroyed) continue;
                CompRentableBed comp = bed.TryGetComp<CompRentableBed>();
                if (comp == null || !comp.Vacant) continue;
                if (customer != null && !customer.CanReach(bed, PathEndMode.OnCell, Danger.Deadly)) continue;
                return bed;
            }
            return null;
        }

        /// <summary>Total rentable beds on this shop's sales floor, with how many are vacant
        /// right now — feeds the inspect string and town ledger's occupancy line.</summary>
        public static int CountBeds(CompBusiness shop, out int vacant)
        {
            int total = 0;
            vacant = 0;
            // ThingsOnFloor's outdoor fallback scans cell by cell, so a bed (unlike the 1x1
            // goods that traversal was written for) would otherwise be counted once per cell
            // of its footprint. Unlike ChooseVacantBed, which returns on its first match and
            // never notices, this method sums across every match, so it needs the dedup.
            HashSet<Thing> seen = new HashSet<Thing>();
            foreach (Thing t in ThingsOnFloor(shop))
            {
                if (!(t is Building_Bed bed) || !t.Spawned || t.Destroyed) continue;
                if (!seen.Add(bed)) continue;
                CompRentableBed comp = bed.TryGetComp<CompRentableBed>();
                if (comp == null) continue;
                total++;
                if (comp.Vacant) vacant++;
            }
            return total;
        }
    }
}
