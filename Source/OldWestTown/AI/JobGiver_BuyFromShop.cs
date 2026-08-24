using OldWestTown.GoldRush;
using OldWestTown.Lords;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.AI
{
    /// <summary>
    /// Decides which business a customer patronizes next — buying goods or using a service,
    /// whichever scores best. Runs from the OWT_Shop duty's think tree, above the wander node,
    /// so a customer with money and somewhere to spend it always shops and only loiters when
    /// there's nothing to buy or use.
    /// </summary>
    public class JobGiver_BuyFromShop : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => PickShoppingJob(pawn);

        /// <summary>
        /// The scoring pass itself, pulled out of TryGiveJob so Compat/HospitalityBridge.cs can
        /// force-start the identical job on an idle Hospitality guest via
        /// Pawn_JobTracker.TryTakeOrderedJob, rather than duplicating this ~80-line scan.
        /// <paramref name="lodgingAllowed"/> defaults to true, so the duty-driven caller above
        /// is byte-for-byte unchanged; the bridge passes false, since Hospitality is already
        /// housing its own guests — see docs/DESIGN.md.
        /// </summary>
        internal static Job PickShoppingJob(Pawn pawn, bool lodgingAllowed = true)
        {
            if (pawn.Map == null || pawn.Downed || pawn.InMentalState) return null;

            int purse = ShopTransaction.SilverCarriedBy(pawn);
            if (purse <= 0) return null;

            TownEconomy econ = pawn.Map.GetComponent<TownEconomy>();
            if (econ == null) return null;

            LordJob_ShopVisit lordJob = pawn.GetLord()?.LordJob as LordJob_ShopVisit;
            CustomerRecord record = lordJob?.RecordFor(pawn);

            // A customer who caused a disturbance is done spending for this visit — the same
            // "legible consequence, no second pawn involved" shape a walkout already has. They
            // fall through to the duty's own wander node for the rest of their stay.
            if (record != null && record.causedTrouble) return null;

            CompBusiness bestShop = null;
            Thing bestTarget = null;
            int bestCount = 0;
            JobDef bestJobDef = null;
            float bestScore = 0f;

            foreach (CompBusiness shop in econ.OpenShops())
            {
                if (record != null && record.refusedShops.Contains(shop.parent)) continue;
                if (!shop.Open || !pawn.CanReach(shop.parent, PathEndMode.Touch, Danger.Deadly)) continue;

                float distanceFactor = 1f + pawn.Position.DistanceTo(shop.parent.Position) / 40f;
                float staffBonus = shop.Staffed ? 1.5f : 1f;

                // Goods candidate. A gold rush's demand basket (a no-op outside an active boom)
                // is folded in here too, not just into ShopStock.ChoosePurchase's own item pick —
                // otherwise a shop stocked entirely with what prospectors want would never pull a
                // customer through the door any harder than one stocked with none of it, even
                // though picking an item within a shop already favours it once they're inside.
                Thing goods = ShopStock.ChoosePurchase(shop, pawn, purse, out int count);
                if (goods != null && count > 0)
                {
                    float score = ShopPricing.ValueAppeal(shop) * staffBonus / distanceFactor
                                  * GoldRushUtility.DemandFactor(pawn.Map, goods);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestShop = shop;
                        bestTarget = goods;
                        bestCount = count;
                        bestJobDef = OWTDefOf.OWT_BuyFromShop;
                    }
                }

                // Service candidates — scored on the same footing as goods, no ordering bias.
                foreach (ServiceDef service in shop.AvailableServices)
                {
                    if (service.worker is ServiceWorker_Lodging)
                    {
                        // A bridged Hospitality guest is never offered a room at all —
                        // Hospitality is already housing them. See Compat/HospitalityBridge.cs
                        // and docs/DESIGN.md for why the two mods can't end up fighting over the
                        // same guest even without this guard.
                        if (!lodgingAllowed) continue;

                        // Unconditional, independent of lodgingAllowed above: nothing downstream
                        // of a Lodging job (ServiceWorker_Lodging, ShopStock.ChooseVacantBed,
                        // CompRentableBed) ever consults CustomerRecord, so this scoring loop is
                        // the ONLY thing standing between a Lord-less pawn and a real bed claim —
                        // and a pawn with no LordJob_ShopVisit has no CustomerRecord for
                        // JobGiver_SleepInRentedBed to ever find later, so it would never run the
                        // job that checks them back out. A caller who forgets lodgingAllowed:false
                        // (the default is true) must not be able to strand a bed this way, so this
                        // is checked regardless of that parameter, not instead of it.
                        if (lordJob == null) continue;

                        // An already-housed guest is never offered a second room, and nobody is
                        // offered a *new* stay once the visit's base duration has elapsed —
                        // goods and every other service are unaffected by either guard.
                        if (record?.rentedBed != null || lordJob?.PastCheckInCutoff == true) continue;

                        // AvailableServices only confirms *some* bed on this floor is vacant,
                        // customer-agnostically (there's no specific pawn to check reachability
                        // against at that call site) — the same shape ChooseService already
                        // has. Goods and stock-consuming services both re-check reachability for
                        // *this* customer before ever being scored; Lodging needs the same
                        // check, or paying at the desk could still leave a guest with no bed
                        // they can actually reach.
                        if (ShopStock.ChooseVacantBed(shop, pawn) == null) continue;
                    }

                    Thing consumable = null;
                    int price;
                    if (service.worker.ConsumesStock)
                    {
                        consumable = ShopStock.ChooseService(shop, service, pawn);
                        if (consumable == null) continue;
                        price = ShopPricing.PriceFor(shop, consumable, 1);
                    }
                    else
                    {
                        price = ShopPricing.PriceForService(shop, service);
                    }
                    if (price > purse) continue;

                    // consumable is null for a stock-free service (Haircut, Lodging, Wager);
                    // DemandFactor treats a null Thing as neutral by design, so this only ever
                    // moves the score for a Drink or a Meal actually pouring an in-demand item.
                    float score = ShopPricing.ValueAppeal(shop) * service.worker.Desirability(pawn) * staffBonus / distanceFactor
                                  * GoldRushUtility.DemandFactor(pawn.Map, consumable);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestShop = shop;
                        bestTarget = consumable;
                        bestCount = 1;
                        bestJobDef = service.jobDef;
                    }
                }
            }

            if (bestShop == null) return null;

            // bestTarget is null for a Haircut-shaped service (nothing to fetch); Thing's
            // implicit conversion to LocalTargetInfo turns that into an invalid-but-harmless
            // target A, which the service driver never dereferences in that case.
            Job job = JobMaker.MakeJob(bestJobDef, bestTarget, bestShop.parent, bestShop.CustomerCellFor(pawn));
            job.count = bestCount;
            return job;
        }
    }
}
