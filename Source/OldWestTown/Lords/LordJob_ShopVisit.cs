using System.Collections.Generic;
using System.Linq;
using System.Text;
using OldWestTown.Shops;
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

        /// <summary>The bed this customer is currently checked into, or null. The single source
        /// of truth for "is this customer mid-stay" — Trigger_VisitComplete reads it directly
        /// to decide whether the whole group can leave yet.</summary>
        public Thing rentedBed;

        /// <summary>Which desk sold the stay named by <see cref="rentedBed"/>, captured once at
        /// check-in. Eviction billing (JobGiver_SleepInRentedBed's stale-claim branch, and
        /// JobDriver_SleepInRentedBed's own finish action) reads this rather than the bed's own
        /// CompRentableBed claim: two Lodging desks can share one room
        /// (CompBusiness.SalesFloorRoom), so a bed this guest lost is free for a *different* desk
        /// to sell before this guest's own stale claim is ever noticed — and billing off the bed
        /// would then charge whichever desk sold it *next*, not whichever desk actually evicted
        /// this guest. Per-guest state can't be clobbered by a different guest's booking the way
        /// a per-bed pointer can, because nothing but this guest's own check-in and check-out ever
        /// touches it.</summary>
        public Thing rentedFrom;

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
            Scribe_Collections.Look(ref refusedGoodsShops, "refusedGoodsShops", LookMode.Reference);
            Scribe_Collections.Look(ref refusedGoodsDefs, "refusedGoodsDefs", LookMode.Def);
            Scribe_References.Look(ref rentedBed, "rentedBed");
            Scribe_References.Look(ref rentedFrom, "rentedFrom");
            Scribe_Values.Look(ref causedTrouble, "causedTrouble");
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

            // A serve already under way finishes; everyone else is interrupted as vanilla would.
            LordToil_CloseUp leave = new LordToil_CloseUp(LocomotionUrgency.Walk);
            graph.AddToil(leave);

            // Business hours are up — and, if anyone rented a room, they've woken up and
            // checked out. See Trigger_VisitComplete: for a group with nobody lodging this is
            // exactly Trigger_TicksPassed(durationTicks), unchanged from before lodging existed.
            Transition timeUp = new Transition(shopping, leave);
            timeUp.AddTrigger(new Trigger_VisitComplete(this));
            timeUp.AddPreAction(new TransitionAction_Custom(AnnounceDeparture));
            graph.AddTransition(timeUp);

            // Somebody shot at the customers — nobody stays to browse through that. Deliberately
            // still flavour-only, unlike the timeUp exit below: an interruption minutes into a
            // visit measures how early the raid landed, not what the shelves offered, so held-
            // vs-spent here would be exactly the confident-wrong-explanation the accounting on
            // the ordinary exit is built to avoid. See DESIGN.md#the-departure-report.
            Transition harmed = new Transition(shopping, leave);
            harmed.AddTrigger(new Trigger_PawnHarmed());
            harmed.AddPreAction(new TransitionAction_Message(
                "OWT_CustomersScared".Translate(faction?.Name ?? ""),
                MessageTypeDefOf.NegativeEvent));
            harmed.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(harmed);

            return graph;
        }

        /// <summary>Held silver below this is just the ordinary change ShopPricing.MaxAffordable
        /// always leaves on the table, not a signal — set above a single non-VIP customer's
        /// smallest possible purse (BasePurse's 120 floor × TownEconomy.MinPurseFactor's 0.9
        /// floor ≈ 108), so one idle solo customer's whole purse stays under it on its own and
        /// does not, by itself, read as money left on the table. A first-cut guess from the
        /// purse-generation code, not from play.</summary>
        private const int MinUnspentSilverToReport = 150;

        /// <summary>Shared floor for both headcount clauses in AnnounceDeparture: the affected
        /// pawn count must clear this *and* be at least half the group before a non-buying or
        /// walked-out minority gets named. The absolute floor keeps one unlucky pawn from
        /// reading as a town-wide verdict; the proportional half keeps it scaled to group size
        /// instead of firing on two-out-of-twenty. A group of one can never clear the floor,
        /// which is what keeps the clause out of a solo visitor's message without needing
        /// singular-safe phrasing.</summary>
        private const int MinAffectedForVerdict = 2;

        /// <summary>Fires once, at the instant the shopping toil hands off to LordToil_CloseUp on
        /// the ordinary (timeUp) exit — the harmed exit stays flavour-only; see CreateGraph's
        /// comment there. Builds the existing OWT_CustomersLeaving line, then — only when the
        /// numbers say something worth saying — one figures sentence and at most one attribution
        /// clause, and posts a single combined message.
        ///
        /// Reads lord.ownedPawns and records directly (via TryGetValue, not RecordFor) so that
        /// producing a report never has the side effect of creating a record for a pawn that
        /// doesn't have one.</summary>
        private void AnnounceDeparture()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("OWT_CustomersLeaving".Translate(faction?.def?.pawnsPlural ?? "customers", faction?.Name ?? ""));

            // Defensive, matching this file's existing stance on unproven vanilla call order
            // (see architecture.md#known-risks): this runs inside vanilla's own
            // Transition.DoAction, and nothing here can prove lord is still non-null at that
            // point. A null lord just means the plain departure line runs alone.
            if (lord != null)
            {
                List<Pawn> pawns = lord.ownedPawns;
                int groupSize = pawns.Count;

                int spentTotal = 0;
                int heldTotal = 0;
                int neverBought = 0;
                int walkedOut = 0;

                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    // Unconditional and live — needs no record, unlike the three tallies below.
                    heldTotal += ShopTransaction.SilverCarriedBy(p);

                    if (records.TryGetValue(p, out CustomerRecord rec))
                    {
                        spentTotal += rec.spent;

                        // causedTrouble excluded defensively: its only gameplay write site
                        // (TroubleUtility.Notify_ServiceRound, via JobDriver_UseService) always
                        // follows a paid service round, so this is currently a no-op — but it
                        // stops a future trouble-triggering path from being misread as "never
                        // found anything to buy", which is a different, false claim.
                        if (rec.purchases == 0 && !rec.causedTrouble) neverBought++;
                        if (rec.walkouts > 0) walkedOut++;
                    }
                    else
                    {
                        // No record at all means this pawn never even got as far as a recorded
                        // attempt — at least as strong an instance of "never bought" as a record
                        // with purchases == 0.
                        neverBought++;
                    }
                }

                // The ÷-free form of "held at least half what the group spent".
                bool moneyLeftOnTable = heldTotal >= spentTotal && heldTotal >= MinUnspentSilverToReport;
                bool neverBoughtQualifies = neverBought >= MinAffectedForVerdict && neverBought * 2 >= groupSize;
                bool walkoutsQualify = walkedOut >= MinAffectedForVerdict && walkedOut * 2 >= groupSize;

                if (moneyLeftOnTable || neverBoughtQualifies || walkoutsQualify)
                {
                    sb.Append(' ');
                    sb.Append("OWT_VisitFigures".Translate(
                        ((float)spentTotal).ToStringMoney(), ((float)heldTotal).ToStringMoney()));

                    if (neverBoughtQualifies || walkoutsQualify)
                    {
                        sb.Append(' ');
                        // A single comparison is enough to choose between the two clauses:
                        // whenever either candidate clears its own bar, the larger of the two
                        // always also clears it, since both the floor and the proportional test
                        // are monotonic in the count. Ties favor walkouts — the more specific,
                        // more confidently-attributable cause: a walkout has an identified fix
                        // (staff the counter, the same one OWT_ColonistGaveUp already points at),
                        // where "never bought" is vaguer and, for a purse outscaling a modest
                        // shelf, may have no single clean cause at all.
                        sb.Append(walkedOut >= neverBought
                            ? "OWT_VisitWalkouts".Translate(walkedOut)
                            : "OWT_VisitNeverBought".Translate(neverBought));
                    }
                }
            }

            // Same message type as today — this is "here's a number", not inherently bad news:
            // a VIP group leaving with cash still in hand is a growth signal, not a complaint.
            Messages.Message(sb.ToString(), MessageTypeDefOf.NeutralEvent);
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
