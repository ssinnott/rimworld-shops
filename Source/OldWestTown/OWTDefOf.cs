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
        public static JobDef OWT_SleepInRentedBed;
        public static JobDef OWT_Patrol;
        public static JobDef OWT_CalmTrouble;
        public static JobDef OWT_RobTill;

        public static DutyDef OWT_Shop;
        public static DutyDef OWT_StickupDuty;

        public static IncidentDef OWT_ShopCustomers;
        public static IncidentDef OWT_Stickup;
        public static IncidentDef OWT_GoldRushStrike;

        public static RaidStrategyDef OWT_StickupStrategy;

        public static ThoughtDef OWT_SleptAtHotel;

        public static HediffDef OWT_Rowdy;

        /// <summary>Looked up directly (not a comp reference) by TroubleUtility's ListerThings
        /// scans, which is the one place C# needs this ThingDef by name.</summary>
        public static ThingDef OWT_SheriffOffice;

        /// <summary>Looked up directly, the same way, by CoachTierUtility.HasDepot's own
        /// ListerThings scan — a depot keeps no registry either.</summary>
        public static ThingDef OWT_CoachDepot;

        /// <summary>The one gold-rush condition ever created, looked up by name from
        /// IncidentWorker_GoldRushStrike; read back live everywhere else via
        /// GoldRushUtility/GameConditionManager.GetActiveCondition rather than a second
        /// reference to this field.</summary>
        public static GameConditionDef OWT_GoldRushCondition;

        static OWTDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OWTDefOf));
        }
    }
}
