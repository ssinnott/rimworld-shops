using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OldWestTown.Shops
{
    public class CompProperties_RentableBed : CompProperties
    {
        public CompProperties_RentableBed()
        {
            compClass = typeof(CompRentableBed);
        }
    }

    /// <summary>
    /// A bed a guest pays to sleep in for one night. Purely passive shared state — mirrors
    /// CompBusiness's lastShopkeeper/lastStaffedTick pair exactly: this comp never runs a job
    /// or reserves anything, it just remembers who currently holds the room.
    /// JobDriver_SleepInRentedBed is the active side that notices this state changing
    /// underneath it (bed destroyed, claim released, someone else in the bed) and ends its own
    /// job accordingly — the same "no handshake" split as a shopkeeper and a customer.
    /// </summary>
    public class CompRentableBed : ThingComp
    {
        private Pawn currentGuest;

        /// <summary>Mirrors CompBusiness.Staffed's own dead-shopkeeper guard: a guest who died
        /// mid-stay (of anything — old age, a stray shot from a raid) doesn't hold the room
        /// hostage forever.</summary>
        public bool Vacant => currentGuest == null || currentGuest.Dead;

        public Pawn CurrentGuest => Vacant ? null : currentGuest;

        public bool IsRentedBy(Pawn p) => !Vacant && currentGuest == p;

        public void Claim(Pawn guest)
        {
            currentGuest = guest;
        }

        public void Release()
        {
            currentGuest = null;
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            // A despawned bed can't hold a guest. Releasing here (rather than waiting for the
            // sleep job's own FailOn to notice next tick) means a save taken in the same tick
            // a bed is destroyed never has a claim pointing at nothing.
            Release();
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            Pawn guest = CurrentGuest;
            return guest == null
                ? "OWT_BedVacant".Translate()
                : "OWT_BedOccupiedBy".Translate(guest.LabelShort);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;

            if (Vacant) yield break;

            yield return new Command_Action
            {
                defaultLabel = "OWT_CmdEvictGuest".Translate(),
                defaultDesc = "OWT_CmdEvictGuestDesc".Translate(),
                icon = TexCommand.ForbidOn,
                action = Release
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref currentGuest, "currentGuest");
        }
    }
}
