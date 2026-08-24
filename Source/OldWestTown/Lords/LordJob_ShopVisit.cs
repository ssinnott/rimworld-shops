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

        /// <summary>Counters this customer gave up waiting at this visit. Private because an entry
        /// is a standing refusal, not a ban — read it through <see cref="WillQueueAt"/>, which is
        /// where that distinction lives.</summary>
        private List<Thing> refusedShops = new List<Thing>();

        /// <summary>
        /// Goods this customer tried to buy at a given business and couldn't, as (business, def)
        /// pairs, so a rejected sale costs one trip instead of repeating for the rest of the visit.
        ///
        /// Keyed on the def rather than the stack: the goods a customer carries to the counter are
        /// split off the shelf stack, so the Thing that fails the sale is not the Thing the shelf
        /// scan offers up next time — and a split-off pile is routinely absorbed back into its
        /// parent and destroyed, which a saved Thing reference would not survive. The def is
        /// deliberately coarser than the price: quality and stuff move market value, so a refused
        /// masterwork knife also hides the plain one beside it for the rest of the visit. That is
        /// the accepted cost of a key that survives the split — and it only ever costs a customer
        /// who was already refused once at that counter.
        /// </summary>
        private List<Thing> refusedGoodsShops = new List<Thing>();
        private List<ThingDef> refusedGoodsDefs = new List<ThingDef>();

        /// <summary>Remember that <paramref name="def"/> would not sell at <paramref name="business"/>.</summary>
        public void RefuseGoods(Thing business, ThingDef def)
        {
            if (business == null || def == null) return;
            for (int i = 0; i < refusedGoodsShops.Count; i++)
            {
                if (refusedGoodsShops[i] == business && refusedGoodsDefs[i] == def) return;
            }
            refusedGoodsShops.Add(business);
            refusedGoodsDefs.Add(def);
        }

        /// <summary>What this customer has given up on at <paramref name="business"/>, or null if
        /// nothing — null rather than an empty list because the job giver asks once per shop per
        /// decision and the answer is almost always "nothing".</summary>
        public List<ThingDef> RefusedGoodsAt(Thing business)
        {
            List<ThingDef> refused = null;
            for (int i = 0; i < refusedGoodsShops.Count; i++)
            {
                if (refusedGoodsShops[i] != business) continue;
                if (refused == null) refused = new List<ThingDef>();
                refused.Add(refusedGoodsDefs[i]);
            }
            return refused;
        }

        /// <summary>Remember that this customer gave up waiting at <paramref name="business"/>.</summary>
        public void RefuseShop(Thing business)
        {
            if (business == null || refusedShops.Contains(business)) return;
            refusedShops.Add(business);
        }

        /// <summary>Whether this customer would walk up to <paramref name="business"/> again.
        ///
        /// A counter they gave up on is off their list only while it is STILL unattended: the refusal
        /// is scoped to the condition that caused it, not to the visit and not to a stopwatch. Somebody
        /// standing at the counter is the one thing that fixes what drove them off, and it is exactly
        /// what the unattended-counter alert asks the player for, so it is the only thing that lifts
        /// the refusal. Time was rejected as the key: it is not something the player did, so it marches
        /// the same customer back to the same empty counter to walk out and pay for it a second time.
        ///
        /// The caller passes the counter's grace-free staffing reading, not the forgiving one the
        /// serve loop uses: a keeper who left a second ago must not pull anybody back across town.
        /// The honesty-box setting deliberately doesn't lift a refusal either — telling a
        /// self-service-eligible visit from a haircut needs per-service knowledge this layer
        /// doesn't have, and a haircut still needs a body behind the chair.
        ///
        /// Takes a plain bool rather than the comp — a rule about which way the layers point, not
        /// about keeping them apart: Shops names neither AI nor Lords, and the record itself asks
        /// for an answer rather than reaching for a comp to work it out.</summary>
        public bool WillQueueAt(Thing business, bool staffedNow)
        {
            if (staffedNow) return true;
            return business == null || !refusedShops.Contains(business);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref spent, "spent");
            Scribe_Values.Look(ref purchases, "purchases");
            Scribe_Values.Look(ref arrivedTick, "arrivedTick");
            Scribe_Values.Look(ref walkouts, "walkouts");
            Scribe_Collections.Look(ref refusedShops, "refusedShops", LookMode.Reference);
            Scribe_Collections.Look(ref refusedGoodsShops, "refusedGoodsShops", LookMode.Reference);
            Scribe_Collections.Look(ref refusedGoodsDefs, "refusedGoodsDefs", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (refusedShops == null) refusedShops = new List<Thing>();
                if (refusedGoodsShops == null) refusedGoodsShops = new List<Thing>();
                if (refusedGoodsDefs == null) refusedGoodsDefs = new List<ThingDef>();

                // The two lists are one table read down the middle. A counter deconstructed
                // mid-visit comes back as a null reference, so drop any pair that lost half of
                // itself rather than letting the columns drift out of step.
                int pairs = refusedGoodsShops.Count < refusedGoodsDefs.Count
                    ? refusedGoodsShops.Count
                    : refusedGoodsDefs.Count;
                refusedGoodsShops.RemoveRange(pairs, refusedGoodsShops.Count - pairs);
                refusedGoodsDefs.RemoveRange(pairs, refusedGoodsDefs.Count - pairs);
                for (int i = pairs - 1; i >= 0; i--)
                {
                    if (refusedGoodsShops[i] != null && refusedGoodsDefs[i] != null) continue;
                    refusedGoodsShops.RemoveAt(i);
                    refusedGoodsDefs.RemoveAt(i);
                }
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

            // A serve already under way finishes; everyone else is interrupted as vanilla would.
            LordToil_CloseUp leave = new LordToil_CloseUp(LocomotionUrgency.Walk);
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
