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
        /// <summary>The longest wait a customer will join a line for. A different clock from
        /// ShopKindDef.customerPatienceTicks and deliberately not derived from it: that one measures
        /// being IGNORED, which is the shop failing and costs the town its name, and this one measures
        /// how long a person will stand in a line that is moving, which is the shop working and costs
        /// nobody anything but the sale. It is a property of a person, not of a trade, so it is one
        /// number here rather than a field on every kind.
        ///
        /// 6000 is a fifth of a 30000-tick visit, and it is what makes the shipped counters behave: a
        /// counter holds ceil(6000 / serveTicks), which is 34 at a shelf and 40 at a saloon bar —
        /// no line anyone will ever see — and exactly 3 at the barber's 2200-tick chair, the one
        /// counter in this mod where a queue is a real thing. Depth is therefore tuned by serve time,
        /// in XML that already exists, and not by a new def field. Also read by the patron driver,
        /// whose backstop clock has to outlast any queue this permits.</summary>
        internal const int MaxQueueWaitTicks = 6000;

        /// <summary>How much more a customer is drawn to a counter somebody is standing at. Written as
        /// 1 + this rather than 1.5 because a crowd eats into exactly this term and nothing else.</summary>
        private const float StaffDrawBonus = 0.5f;

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
            CompBusiness turnedAway = null;
            Thing bestTarget = null;
            int bestCount = 0;
            JobDef bestJobDef = null;
            float bestScore = 0f;

            foreach (CompBusiness shop in econ.OpenShops())
            {
                // A counter this customer gave up on is off their list only while nobody is behind it —
                // see CustomerRecord.WillQueueAt. Staff it and they come back on their next think tick,
                // which is what makes the waiting-customers alert honest.
                if (record != null && !record.WillQueueAt(shop.parent, shop.StaffedNow)) continue;
                if (!shop.Open || !pawn.CanReach(shop.parent, PathEndMode.Touch, Danger.Deadly)) continue;

                float distanceFactor = 1f + pawn.Position.DistanceTo(shop.parent.Position) / 40f;

                // Everybody already committed to this counter, counted once for the whole decision.
                int ahead = shop.PatronsHeadedHere;

                // A crowd discounts exactly one thing: the advantage having somebody behind the counter
                // gave this shop in the first place. Never more than that. A busy staffed counter must
                // never score below an unworked one, or the rule that spreads custom between tills
                // becomes a rule that sends people to counters nobody is standing at, and manufactures
                // the walkouts it exists to prevent. Idle and staffed is still exactly the 1.5 it
                // always was.
                float staffBonus = shop.Staffed ? 1f + StaffDrawBonus / (1f + ahead) : 1f;

                // Goods candidate. Anything that already failed to sell here would fail again
                // identically, so it is excluded from the pick — the customer moves on to the
                // next-best stack at this counter rather than writing the whole shop off. A gold
                // rush's demand basket (a no-op outside an active boom) is folded into the score
                // here too, not just into ShopStock.ChoosePurchase's own item pick — otherwise a
                // shop stocked entirely with what prospectors want would never pull a customer
                // through the door any harder than one stocked with none of it, even though
                // picking an item within a shop already favours it once they're inside.
                Thing goods = ShopStock.ChoosePurchase(shop, pawn, purse, out int count,
                    record?.RefusedGoodsAt(shop.parent));
                if (goods != null && count > 0)
                {
                    if (ahead * JobDriver_BuyFromShop.GoodsServeTicks >= MaxQueueWaitTicks)
                    {
                        // Only a counter somebody is WORKING can be described as busy. At an
                        // unattended one the people ahead are being neglected, not served, and the
                        // alert is already saying so in stronger terms.
                        if (shop.Staffed) turnedAway = shop;
                    }
                    else
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
                    if (ahead * service.serveTicks >= MaxQueueWaitTicks)
                    {
                        if (shop.Staffed) turnedAway = shop;
                        continue;
                    }

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

            // A counter that had something this customer wanted and could pay for, and could not take
            // them. That is a lost sale whether or not they found somewhere else to spend it, and a
            // lost sale is the whole of what a bottleneck costs — so it is what gets said out loud,
            // naming the counter rather than the town. Rate-limited on the counter itself.
            if (turnedAway != null && turnedAway != bestShop && turnedAway.TryClaimBusyMessage())
            {
                Messages.Message("OWT_CounterBusy".Translate(turnedAway.parent.Label),
                    new LookTargets(turnedAway.parent), MessageTypeDefOf.NeutralEvent);
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
