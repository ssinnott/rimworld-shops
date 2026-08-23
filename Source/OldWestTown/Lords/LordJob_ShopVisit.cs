using System.Collections.Generic;
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

        public void ExposeData()
        {
            Scribe_Values.Look(ref spent, "spent");
            Scribe_Values.Look(ref purchases, "purchases");
            Scribe_Values.Look(ref arrivedTick, "arrivedTick");
            Scribe_Values.Look(ref walkouts, "walkouts");
            Scribe_Collections.Look(ref refusedShops, "refusedShops", LookMode.Reference);
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

        private List<Pawn> recordPawns = new List<Pawn>();
        private List<CustomerRecord> recordValues = new List<CustomerRecord>();
        private Dictionary<Pawn, CustomerRecord> records = new Dictionary<Pawn, CustomerRecord>();

        public LordJob_ShopVisit() { }

        public LordJob_ShopVisit(Faction faction, IntVec3 townCenter, int durationTicks)
        {
            this.faction = faction;
            this.townCenter = townCenter;
            this.durationTicks = durationTicks;
        }

        public IntVec3 TownCenter => townCenter;

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

            // Business hours are up.
            Transition timeUp = new Transition(shopping, leave);
            timeUp.AddTrigger(new Trigger_TicksPassed(durationTicks));
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
            Scribe_Collections.Look(ref records, "records",
                LookMode.Reference, LookMode.Deep, ref recordPawns, ref recordValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && records == null)
            {
                records = new Dictionary<Pawn, CustomerRecord>();
            }
        }
    }
}
