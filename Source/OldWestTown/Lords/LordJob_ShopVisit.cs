using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.Lords
{
    /// <summary>Per-customer state for one visit. Lives on the lord, so it saves and dies with the group.</summary>
    public class CustomerRecord : IExposable
    {
        public int spent;
        public int purchases;
        public int arrivedTick;
        public int walkouts;

        /// <summary>Shops this customer has already given up on, so they don't queue there again.</summary>
        public List<Thing> refusedShops = new List<Thing>();

        /// <summary>The bed this customer is currently checked into, or null. The single source
        /// of truth for "is this customer mid-stay" — Trigger_VisitComplete reads it directly
        /// to decide whether the whole group can leave yet.</summary>
        public Thing rentedBed;

        /// <summary>Set once by TroubleUtility.Notify_ServiceRound if this customer tipped a
        /// saloon into a disturbance. JobGiver_BuyFromShop reads it to stop offering them
        /// anything else for the rest of the visit — the same legible consequence a walkout
        /// already has, just for a different cause.</summary>
        public bool causedTrouble;

        public void ExposeData()
        {
            Scribe_Values.Look(ref spent, "spent");
            Scribe_Values.Look(ref purchases, "purchases");
            Scribe_Values.Look(ref arrivedTick, "arrivedTick");
            Scribe_Values.Look(ref walkouts, "walkouts");
            Scribe_Collections.Look(ref refusedShops, "refusedShops", LookMode.Reference);
            Scribe_References.Look(ref rentedBed, "rentedBed");
            Scribe_Values.Look(ref causedTrouble, "causedTrouble");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && refusedShops == null)
            {
                refusedShops = new List<Thing>();
            }
        }
    }

    /// <summary>
    /// A group of travellers who come to town, spend a while doing business, and leave.
    ///
    /// The graph is deliberately flat — one shopping state plus an exit — rather than the
    /// travel/chill/exit chain vanilla visitors use. Customers already have a reason to walk
    /// somewhere specific (the shop they picked), so an extra travel state would only fight
    /// the shopping AI for control of where they stand.
    /// </summary>
    public class LordJob_ShopVisit : LordJob
    {
        private Faction faction;
        private IntVec3 townCenter;
        private int durationTicks = 30000;

        /// <summary>Stamped once, at arrival. Not CustomerRecord.arrivedTick — that field is
        /// stamped on a pawn's first purchase *attempt*, not on arrival, so a pawn who never
        /// shops either never gets one or gets one stamped arbitrarily late. This is the
        /// group's own clock, independent of any individual customer's behaviour.</summary>
        private int groupArrivedTick;

        private List<Pawn> recordPawns = new List<Pawn>();
        private List<CustomerRecord> recordValues = new List<CustomerRecord>();
        private Dictionary<Pawn, CustomerRecord> records = new Dictionary<Pawn, CustomerRecord>();

        public LordJob_ShopVisit() { }

        public LordJob_ShopVisit(Faction faction, IntVec3 townCenter, int durationTicks)
        {
            this.faction = faction;
            this.townCenter = townCenter;
            this.durationTicks = durationTicks;
            groupArrivedTick = Find.TickManager.TicksGame;
        }

        public IntVec3 TownCenter => townCenter;

        /// <summary>Past the base visit duration. Read by JobGiver_BuyFromShop to stop offering
        /// new check-ins once business hours are up — goods and every other service are
        /// unaffected — and by Trigger_VisitComplete as one half of "can the group leave yet."
        /// Combined with each sleep job's own hard tick cap, this guarantees a lodger already
        /// checked in can only ever finish and clear out, never re-book indefinitely.</summary>
        public bool PastCheckInCutoff => Find.TickManager.TicksGame - groupArrivedTick >= durationTicks;

        public CustomerRecord RecordFor(Pawn pawn)
        {
            if (pawn == null) return null;
            if (!records.TryGetValue(pawn, out CustomerRecord rec))
            {
                rec = new CustomerRecord { arrivedTick = Find.TickManager.TicksGame };
                records[pawn] = rec;
            }
            return rec;
        }

        public override bool AddFleeToil => true;

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();

            LordToil_Shop shopping = new LordToil_Shop(townCenter);
            graph.AddToil(shopping);
            graph.StartingToil = shopping;

            LordToil_ExitMap leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);

            // Business hours are up — and, if anyone rented a room, they've woken up and
            // checked out. See Trigger_VisitComplete: for a group with nobody lodging this is
            // exactly Trigger_TicksPassed(durationTicks), unchanged from before lodging existed.
            Transition timeUp = new Transition(shopping, leave);
            timeUp.AddTrigger(new Trigger_VisitComplete(this));
            timeUp.AddPreAction(new TransitionAction_Message(
                "OWT_CustomersLeaving".Translate(faction?.def?.pawnsPlural ?? "customers", faction?.Name ?? ""),
                MessageTypeDefOf.NeutralEvent));
            graph.AddTransition(timeUp);

            // Somebody shot at the customers — nobody stays to browse through that.
            Transition harmed = new Transition(shopping, leave);
            harmed.AddTrigger(new Trigger_PawnHarmed());
            harmed.AddPreAction(new TransitionAction_Message(
                "OWT_CustomersScared".Translate(faction?.Name ?? ""),
                MessageTypeDefOf.NegativeEvent));
            harmed.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(harmed);

            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref townCenter, "townCenter");
            Scribe_Values.Look(ref durationTicks, "durationTicks", 30000);
            Scribe_Values.Look(ref groupArrivedTick, "groupArrivedTick");
            Scribe_Collections.Look(ref records, "records",
                LookMode.Reference, LookMode.Deep, ref recordPawns, ref recordValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && records == null)
            {
                records = new Dictionary<Pawn, CustomerRecord>();
            }
        }

        /// <summary>
        /// The group's real exit condition: past the base visit duration, AND nobody still in
        /// the group is mid-stay. A nested class so it can read <see cref="records"/> directly
        /// — no new public accessor on the lord job, and no second counter that could drift out
        /// of sync with CustomerRecord.rentedBed, which is already the one source of truth for
        /// "is anyone still checked in".
        ///
        /// Records are filtered to the lord's current ownedPawns rather than checked wholesale:
        /// a customer's record is never removed from the dictionary (it
        /// dies with the lord, not before), so a guest who died mid-stay — holding a claim
        /// nothing will ever clear, since their sleep job's finish action never runs for a pawn
        /// that's simply gone — would otherwise hold the entire group hostage forever. A pawn
        /// no longer in the lord can't ever check out properly, so their stale claim shouldn't
        /// count against the rest of the group leaving.
        ///
        /// For a group with zero lodgers — the overwhelming common case — this is true from the
        /// first tick past durationTicks, exactly like the flat Trigger_TicksPassed it replaces.
        /// An old save predating this field has groupArrivedTick default to 0, which makes
        /// PastCheckInCutoff true immediately on load; since such a save can have no
        /// CustomerRecord.rentedBed set either (lodging didn't exist yet), the group leaves at
        /// the first opportunity after loading — safe, if a little eager, and never stranded.
        /// </summary>
        private class Trigger_VisitComplete : Trigger
        {
            private readonly LordJob_ShopVisit owner;

            public Trigger_VisitComplete(LordJob_ShopVisit owner)
            {
                this.owner = owner;
            }

            public override bool ActivateOn(Lord lord, TriggerSignal signal) =>
                owner.PastCheckInCutoff
                && owner.records.All(kv => !lord.ownedPawns.Contains(kv.Key) || kv.Value.rentedBed == null);
        }
    }
}
