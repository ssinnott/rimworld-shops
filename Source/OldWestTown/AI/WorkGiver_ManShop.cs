using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.AI
{
    /// <summary>
    /// Sends a colonist to work a shop counter. A counter only asks for staff once it is open,
    /// stocked and has customers in town, so nobody stands behind an empty store all day.
    /// </summary>
    public class WorkGiver_ManShop : WorkGiver_Scanner
    {
        /// <summary>How far from the counter a customer counts as "in the shop".</summary>
        private const float CustomerScanRadius = 25f;

        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            TownEconomy econ = pawn.Map?.GetComponent<TownEconomy>();
            if (econ == null) yield break;
            foreach (CompBusiness shop in econ.OpenShops()) yield return shop.parent;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            TownEconomy econ = pawn.Map?.GetComponent<TownEconomy>();
            if (econ == null) return true;
            foreach (CompBusiness _ in econ.OpenShops()) return false;
            return true;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompBusiness shop = t.TryGetComp<CompBusiness>();
            if (shop == null || !shop.Open) return false;
            if (shop.Shopkeeper != null && shop.Shopkeeper != pawn) return false;

            IntVec3 post = shop.StaffCell;
            if (!post.InBounds(t.Map) || !post.Standable(t.Map)) return false;
            if (!pawn.CanReserveAndReach(post, PathEndMode.OnCell, pawn.NormalMaxDanger(), 1, -1, null, forced)) return false;

            // Forced by the player: always allowed, even with the shop empty.
            if (forced) return true;

            if (!shop.HasAnythingToOffer) return false;
            return AnyCustomerNear(shop);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompBusiness shop = t.TryGetComp<CompBusiness>();
            if (shop == null) return null;
            return JobMaker.MakeJob(OWTDefOf.OWT_ManShop, t, shop.StaffCell);
        }

        /// <summary>True if any non-hostile visitor nearby could still spend money here. Being nearby
        /// is not enough on its own: a customer who has spent their last silver keeps the shopping duty
        /// and keeps wandering the town centre until the visit clock runs out, and a customer taking a
        /// nap is not shopping either — counting them is how a colonist ends up posted all day at a
        /// counter that cannot make a sale.
        ///
        /// A customer who once gave up here still counts, deliberately. Their refusal lifts the moment
        /// somebody stands at the counter, so they are precisely who staffing it wins back — filtering
        /// them out would mean nobody is ever sent to staff the counter they are waiting to see
        /// staffed.</summary>
        internal static bool AnyCustomerNear(CompBusiness shop)
        {
            Map map = shop.parent.Map;
            if (map == null) return false;

            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p.Faction == Faction.OfPlayer || p.Dead || p.Downed) continue;
                if (p.HostileTo(Faction.OfPlayer)) continue;
                if (p.Position.DistanceTo(shop.parent.Position) > CustomerScanRadius) continue;

                // Somebody at THIS counter counts whatever their purse or their duty says: silver
                // leaves the customer partway through the transaction, and the group's exit
                // transition swaps the shopping duty out from under a serve that is still running,
                // so both of the tests below would drop a customer mid-sale. Walking off on one is
                // never right. TargetIndex.B is the counter in every JobDriver_PatronizeBusiness.
                if (p.jobs?.curDriver is IBusinessPatron
                    && p.CurJob?.GetTarget(TargetIndex.B).Thing == shop.parent) return true;

                if (p.mindState?.duty?.def != OWTDefOf.OWT_Shop) continue;
                if (!p.Awake()) continue;
                if (ShopTransaction.SilverCarriedBy(p) > 0) return true;
            }
            return false;
        }
    }
}
