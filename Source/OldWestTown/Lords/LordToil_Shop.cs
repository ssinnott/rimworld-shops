using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.Lords
{
    /// <summary>
    /// Hands every pawn in the group the "go do business in town" duty. The duty's think tree
    /// (see Duties_Customer.xml) is what actually decides between buying something and loitering.
    /// </summary>
    public class LordToil_Shop : LordToil
    {
        private const float ShoppingRadius = 30f;

        private IntVec3 center;

        public LordToil_Shop(IntVec3 center)
        {
            this.center = center;
        }

        public override IntVec3 FlagLoc => center;

        // Customers are here for a day out — let them eat, drink and rest between purchases.
        public override bool AllowSatisfyLongNeeds => true;

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(OWTDefOf.OWT_Shop, center, ShoppingRadius);
            }
        }
    }
}
