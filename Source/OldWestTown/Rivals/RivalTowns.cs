using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace OldWestTown.Rivals
{
    /// <summary>One rival town's live, persisted state. Lives on the world, not a map — see
    /// <see cref="RivalTowns"/> for why regional rivals are shared rather than per-colony.</summary>
    public class RivalTown : IExposable
    {
        public RivalTownDef def;

        /// <summary>This rival's live appeal. Monotonically non-decreasing toward
        /// <c>def.maxAppeal</c> — there is no decline mechanic; see docs/DESIGN.md.</summary>
        public float currentAppeal;

        /// <summary><see cref="GenDate.DaysPassed"/> on which the current undercutting swing
        /// ends; -1 while not undercutting.</summary>
        public int undercutEndDay = -1;

        public bool Undercutting => undercutEndDay >= 0 && GenDate.DaysPassed < undercutEndDay;

        /// <summary>This rival's price-competitiveness number right now — <c>def.undercutPriceIndex</c>
        /// while undercutting, else a flat 1.0 ("an honest, unremarkable competitor"). Guards
        /// against a null <see cref="def"/> — an orphaned instance from a def a modder has
        /// removed mid-save — the same defensive shape a missing def gets everywhere else in
        /// this mod.</summary>
        public float PriceIndex => Undercutting && def != null ? def.undercutPriceIndex : 1f;

        /// <summary>What this rival actually pulls against the player's own
        /// <c>TownEconomy.MarketPull</c> — appeal weighted by price competitiveness, exactly the
        /// way the player's own pull already is.</summary>
        public float Pull => currentAppeal * PriceIndex;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref currentAppeal, "currentAppeal");
            Scribe_Values.Look(ref undercutEndDay, "undercutEndDay", -1);
        }
    }

    /// <summary>
    /// World-scoped roster of rival towns — this mod's first <see cref="WorldComponent"/>, and
    /// the "opponent" side of regional trade that <c>TownEconomy</c> reads from the map side.
    /// Deliberately abstract: no <c>Faction</c>, no world-tile object, no pawns — a rival is a
    /// number that grows and occasionally undercuts, nothing more. See docs/DESIGN.md for why.
    ///
    /// Shared by every loaded map: rivals are regional, not per-colony, so two simultaneously
    /// loaded player colonies read the identical roster. What differs per map is whether, and
    /// how, that map's own town is currently ahead of it — see
    /// <c>TownEconomy.CheckRegionalLeadChange</c> for why that tracking has to live per-map
    /// rather than here.
    /// </summary>
    public class RivalTowns : WorldComponent
    {
        private List<RivalTown> rivals = new List<RivalTown>();

        /// <summary>Throttle field, mirrors <c>TownEconomy.lastDayRolled</c>'s own shape.</summary>
        private int lastProcessedDay = -1;

        public RivalTowns(World world) : base(world) { }

        public IReadOnlyList<RivalTown> Rivals => rivals;

        /// <summary>
        /// Every rival's own pull, summed. Deliberately settings-agnostic itself — this getter
        /// never reads <c>OldWestTownMod.Settings</c>. Scaling by the player's rivalStrength
        /// setting happens exactly once, in <c>TownEconomy.CompetingPull</c>, so a given sum here
        /// means the same thing regardless of any one map's settings. <see cref="WorldComponentTick"/>
        /// does read the master on/off switch, to freeze growth and undercut rolling while it's
        /// off — see there for why that doesn't also need this sum to change shape.
        /// </summary>
        public float TotalRivalPull
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < rivals.Count; i++) total += rivals[i].Pull;
                return total;
            }
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            // EnsureRivalRoster's own emptiness/reference check makes a fresh game and an old
            // save's first load under this version the identical code path -- nothing here needs
            // to branch on fromLoad.
            EnsureRivalRoster();
        }

        /// <summary>
        /// Seeds one <see cref="RivalTown"/> per loaded <see cref="RivalTownDef"/> with no
        /// matching instance yet. Idempotent, so calling it again after a modder adds a third def
        /// to an in-progress save just adds the new rival and leaves every existing one's grown
        /// state untouched.
        /// </summary>
        private void EnsureRivalRoster()
        {
            foreach (RivalTownDef def in DefDatabase<RivalTownDef>.AllDefsListForReading)
            {
                bool exists = false;
                for (int i = 0; i < rivals.Count; i++)
                {
                    if (rivals[i].def == def) { exists = true; break; }
                }
                if (exists) continue;

                rivals.Add(new RivalTown { def = def, currentAppeal = def.baseAppeal, undercutEndDay = -1 });
            }
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            int today = GenDate.DaysPassed;
            if (today == lastProcessedDay) return;

            // A save that hasn't ticked in a while catches up in one step rather than needing one
            // call per elapsed day -- correct for a memoryless MTB process, and the same
            // "recompute the gap, don't replay it" shape TownEconomy's own arrival clock already
            // leans on. lastProcessedDay advances every day regardless of the gate below, so a
            // long stretch with rivals disabled is zero days of growth, never a debt that lands
            // as one giant jump (and a surprise undercut roll) the moment the player re-enables it.
            int daysPassed = lastProcessedDay < 0 ? 1 : today - lastProcessedDay;
            lastProcessedDay = today;

            // A gate on today's growth/roll, not on `rivals` itself or on the day bookkeeping
            // above -- disabling the setting freezes every rival exactly where it stood, rather
            // than pausing the clock and dumping the missed days on it all at once later.
            if (!OldWestTownMod.Settings.rivalTownsEnabled) return;

            for (int i = 0; i < rivals.Count; i++) ProcessDay(rivals[i], daysPassed);
        }

        private void ProcessDay(RivalTown rival, int daysPassed)
        {
            RivalTownDef def = rival.def;
            if (def == null) return;   // orphaned instance from a since-removed def; nothing to grow or roll

            rival.currentAppeal = Mathf.Min(def.maxAppeal, rival.currentAppeal + def.growthPerDay * daysPassed);

            // wasUndercutting reads the raw field, not the Undercutting property -- that
            // property collapses "never started" and "just expired" into the same false, which
            // would either miss the end-of-swing message or immediately re-roll a new swing on
            // the same day the old one lapsed.
            int today = GenDate.DaysPassed;
            bool wasUndercutting = rival.undercutEndDay >= 0;

            if (wasUndercutting && today >= rival.undercutEndDay)
            {
                rival.undercutEndDay = -1;
                Messages.Message("OWT_RivalUndercutEndMessage".Translate(def.LabelCap), MessageTypeDefOf.NeutralEvent);
            }
            else if (!wasUndercutting
                && Rand.MTBEventOccurs(def.undercutMTBDays, 60000f, daysPassed * 60000f))
            {
                rival.undercutEndDay = today + Mathf.RoundToInt(def.undercutDurationDays);
                Messages.Message("OWT_RivalUndercutStartMessage".Translate(def.LabelCap), MessageTypeDefOf.NeutralEvent);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref rivals, "rivals", LookMode.Deep);
            Scribe_Values.Look(ref lastProcessedDay, "lastProcessedDay", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && rivals == null)
            {
                rivals = new List<RivalTown>();
            }
        }
    }
}
