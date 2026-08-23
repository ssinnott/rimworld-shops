using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown
{
    [DefOf]
    public static class OWTDefOf
    {
        public static JobDef OWT_BuyFromShop;
        public static JobDef OWT_ManShop;

        public static DutyDef OWT_Shop;

        public static IncidentDef OWT_ShopCustomers;

        static OWTDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OWTDefOf));
        }
    }
}
