using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.Lords
{
    /// <summary>
    /// Hands every member of the crew the "rob the town" duty. Mirrors LordToil_Shop exactly —
    /// same shape, a hostile duty in place of a shopping one.
    /// </summary>
    public class LordToil_Stickup : LordToil
    {
        /// <summary>Same value as LordToil_Shop.ShoppingRadius — the town's geographic footprint
        /// doesn't change based on who's walking through it.</summary>
        private const float RobRadius = 30f;

        private IntVec3 center;

        public LordToil_Stickup(IntVec3 center)
        {
            this.center = center;
        }

        public override IntVec3 FlagLoc => center;

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(OWTDefOf.OWT_StickupDuty, center, RobRadius);
            }
        }
    }
}
