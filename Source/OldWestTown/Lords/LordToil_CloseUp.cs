using System.Collections.Generic;
using OldWestTown.Shops;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.Lords
{
    /// <summary>
    /// Closing time: the town stops taking customers, rather than emptying itself.
    ///
    /// Vanilla's exit toil can end every pawn's job as it hands out the exit duty, and that
    /// blanket interrupt is right for everyone except the customer already at the counter with
    /// a colonist working their sale — it throws that sale away one tick from the till, drops
    /// the goods on the floor and wastes however long the serve had already run. This keeps the
    /// interrupt for everybody else, so a customer asleep or eating at closing is no more likely
    /// to linger than before.
    ///
    /// Nothing here waits on the shopkeeper: the exemption is read off the customer's own driver,
    /// and the customer's own end condition retires it the moment nobody is serving them.
    /// </summary>
    public class LordToil_CloseUp : LordToil_ExitMap
    {
        public LordToil_CloseUp(LocomotionUrgency locomotion)
            : base(locomotion, false, false)
        {
        }

        public override void UpdateAllDuties()
        {
            // Asserted here, not just handed to the constructor: this flag lives on the toil's
            // LordToilData, which is saved with the lord and restored onto the graph the job
            // rebuilds on load. A group already in town when the mod updated would otherwise
            // carry the old blanket interrupt for the rest of its visit, and the sale this
            // whole toil exists to spare would be torn up with nothing to show it happened.
            Data.interruptCurrentJob = false;

            // Duties first, so that the jobs ended below are ended against the exit duty rather
            // than the shopping one.
            base.UpdateAllDuties();

            // Backwards: ending a job starts a new one, which runs a think pass before this loop
            // resumes. Nothing on that path drops a pawn from the lord today, but if one ever
            // did, a forward index would step over the pawn that took its place.
            List<Pawn> pawns = lord.ownedPawns;
            for (int i = pawns.Count - 1; i >= 0; i--)
            {
                Pawn p = pawns[i];
                if (p.jobs?.curJob == null) continue;
                if (p.jobs.curDriver is IBusinessPatron patron && patron.BeingServed) continue;
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }
}
