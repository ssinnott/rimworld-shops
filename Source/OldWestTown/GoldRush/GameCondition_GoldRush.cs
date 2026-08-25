using RimWorld;
using UnityEngine;
using Verse;
using OldWestTown.Shops;

namespace OldWestTown.GoldRush
{
    /// <summary>
    /// One instance per active rush, boom then bust, self-phasing rather than two chained
    /// conditions or a second incident's own clock — see docs/DESIGN.md. Boom lasts a quadrum;
    /// bust follows and lasts until town reputation clears <see cref="BustRecoveryReputation"/>,
    /// or until the firing IncidentDef's own generous duration cap ends things regardless (a
    /// safety net, not the intended exit).
    /// </summary>
    public class GameCondition_GoldRush : GameCondition
    {
        private const int TickCheckInterval = 600;          // same cadence as TownEconomy/StickupWatch
        private const float BustRecoveryReputation = 0.45f; // just under the 0.5 neutral point RollOverDay already decays toward

        private bool bustStarted;

        // Set only by GameConditionTick's own reputation check just below, immediately before it
        // calls End() itself -- never by the hard-duration-cap path, which reaches End() through
        // vanilla's own automatic end-of-life cleanup (GameConditionManager noticing Expired)
        // without ever running this file's code again first. That is what lets End() tell "the
        // bust genuinely recovered" apart from "the safety net ran out while reputation was still
        // under the bar" -- bustStarted alone is true on both paths, which is exactly the bug
        // this flag exists to not repeat. Deliberately not persisted (see ExposeData): it is only
        // ever true for the instant between being set and the unconditional End() call two lines
        // below it, in the same synchronous method -- never across a tick boundary, so there is
        // no save/load window where its value could matter.
        private bool recoveredByReputation;

        public bool BustActive => bustStarted;

        /// <summary>Dev Mode lever: forces the boom straight into its bust phase — byte-for-byte
        /// the same transition GameConditionTick performs once the boom's own duration elapses,
        /// so the debug path and the real one are indistinguishable afterward. See
        /// DevTools/DebugActions.cs.</summary>
        internal void DebugForceBust()
        {
            if (bustStarted) return;
            bustStarted = true;
            Messages.Message("OWT_GoldRushBustBegins".Translate(), MessageTypeDefOf.NegativeEvent);
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if (Find.TickManager.TicksGame % TickCheckInterval != 0) return;

            if (!bustStarted)
            {
                int boomDurationTicks = GenDate.DaysPerQuadrum * 60000; // a quadrum, in this codebase's own tick-per-day convention
                if (TicksPassed < boomDurationTicks) return;
                bustStarted = true;
                Messages.Message("OWT_GoldRushBustBegins".Translate(), MessageTypeDefOf.NegativeEvent);
                return;
            }

            TownEconomy econ = SingleMap?.GetComponent<TownEconomy>();
            if (econ != null && econ.Reputation >= BustRecoveryReputation)
            {
                recoveredByReputation = true;
                End();
            }
        }

        // Reachable either from GameConditionTick's own End() call above (reputation genuinely
        // recovered) or from vanilla's own automatic end-of-duration cleanup (the hard safety cap
        // on OWT_GoldRushStrike's durationDays forcing things closed regardless) -- one method
        // both paths pass through, but only the first of them earns the "recovered" letter:
        // recoveredByReputation is the one thing that tells them apart, since bustStarted alone
        // is true on both. The timeout path gets no letter at all -- an honest "this just quietly
        // ran out" rather than a claim that reputation recovered when it didn't.
        public override void End()
        {
            if (recoveredByReputation)
            {
                Find.LetterStack.ReceiveLetter(
                    "OWT_LetterGoldRushRecoveredLabel".Translate(),
                    "OWT_LetterGoldRushRecoveredText".Translate(),
                    LetterDefOf.PositiveEvent);
            }
            base.End();
        }

        public override string Description =>
            bustStarted
                ? "OWT_GoldRushBustStatus".Translate(
                    (SingleMap?.GetComponent<TownEconomy>()?.Reputation ?? 0f).ToStringPercent(),
                    BustRecoveryReputation.ToStringPercent())
                : "OWT_GoldRushBoomStatus".Translate(
                    Mathf.Max(0, (GenDate.DaysPerQuadrum * 60000 - TicksPassed) / 60000));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bustStarted, "bustStarted");
            // recoveredByReputation deliberately has no Scribe_Values entry of its own — see its
            // own doc comment for why a save can never land on a tick where its value matters.
        }
    }
}
