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

        /// <summary>True if any non-hostile visitor is close enough to be a prospective customer.</summary>
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

                // A native customer is recognized by duty; a bridged Hospitality guest never has
                // one (Compat/HospitalityBridge.cs never touches PawnDuty), so it's recognized
                // by the same IBusinessPatron marker CompBusiness.CellFreeFor and
                // Alert_CustomersWaiting already key off instead of the duty.
                bool isCustomer = p.mindState?.duty?.def == OWTDefOf.OWT_Shop || p.jobs?.curDriver is IBusinessPatron;
                if (!isCustomer) continue;

                if (p.Position.DistanceTo(shop.parent.Position) <= CustomerScanRadius) return true;
            }
            return false;
        }
    }
}
