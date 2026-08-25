using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace OldWestTown.Lords
{
    /// <summary>
    /// A stickup crew: walk in, empty tills, leave — on their own once there's nothing left to
    /// take or the clock runs out, or in a rout the moment anyone shoots back. Structurally a
    /// near-twin of LordJob_ShopVisit's own flat graph; the two toils just play a hostile role
    /// instead of a paying one.
    /// </summary>
    public class LordJob_Stickup : LordJob
    {
        private Faction faction;
        private IntVec3 townCenter;
        private int durationTicks = 20000;

        /// <summary>Stamped once, at construction — this raid's own clock, read by
        /// Trigger_StickupComplete alongside StickupWatch.TotalTillSilver.</summary>
        private int groupArrivedTick;

        public LordJob_Stickup() { }

        public LordJob_Stickup(Faction faction, IntVec3 townCenter, int durationTicks)
        {
            this.faction = faction;
            this.townCenter = townCenter;
            this.durationTicks = durationTicks;
            groupArrivedTick = Find.TickManager.TicksGame;
        }

        /// <summary>Marks a downed member as guilty for whatever vanilla systems key off that —
        /// the mechanical piece that makes "capture and ransom a stickup raider" ordinary,
        /// unmodified vanilla prisoner mechanics rather than anything this mod has to build. See
        /// docs/outlaws.md for why that's the whole "jail" story this mod tells.</summary>
        public override bool GuiltyOnDowned => true;

        public override bool AddFleeToil => true;

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();

            LordToil_Stickup robbing = new LordToil_Stickup(townCenter);
            graph.AddToil(robbing);
            graph.StartingToil = robbing;

            LordToil_ExitMap leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);

            LordToil_PanicFlee flee = new LordToil_PanicFlee();
            graph.AddToil(flee);

            // Nothing left worth taking, or the clock ran out — the crew clears out on its own.
            Transition complete = new Transition(robbing, leave);
            complete.AddTrigger(new Trigger_StickupComplete(this));
            complete.AddPreAction(new TransitionAction_Message(
                "OWT_StickupDeparted".Translate(faction?.Name ?? ""), MessageTypeDefOf.NeutralEvent));
            graph.AddTransition(complete);

            // Somebody shot back. A stickup crew routs — it doesn't calmly walk out the way
            // unarmed customers do when a raid's own harmed trigger fires on them.
            Transition harmed = new Transition(robbing, flee);
            harmed.AddTrigger(new Trigger_PawnHarmed());
            harmed.AddPreAction(new TransitionAction_Message(
                "OWT_StickupResisted".Translate(faction?.Name ?? ""), MessageTypeDefOf.NegativeEvent));
            harmed.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(harmed);

            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref townCenter, "townCenter");
            Scribe_Values.Look(ref durationTicks, "durationTicks", 20000);
            Scribe_Values.Look(ref groupArrivedTick, "groupArrivedTick");
        }

        /// <summary>The raid's real exit condition: the duration cap has elapsed, or every till
        /// on the map has already been emptied. A nested class so it can read the owner's fields
        /// directly — mirrors LordJob_ShopVisit's own Trigger_VisitComplete idiom exactly.</summary>
        private class Trigger_StickupComplete : Trigger
        {
            private readonly LordJob_Stickup owner;

            public Trigger_StickupComplete(LordJob_Stickup owner)
            {
                this.owner = owner;
            }

            public override bool ActivateOn(Lord lord, TriggerSignal signal)
            {
                if (Find.TickManager.TicksGame - owner.groupArrivedTick >= owner.durationTicks) return true;
                Map map = lord.Map;
                return map != null && map.GetComponent<StickupWatch>()?.TotalTillSilver <= 0;
            }
        }
    }
}
