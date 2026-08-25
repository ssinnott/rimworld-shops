using System.Collections.Generic;
using Verse;
using OldWestTown.Shops;

namespace OldWestTown.DevTools
{
    /// <summary>
    /// Opt-in logging for the numbers docs/architecture.md's known risks keep asking to have
    /// confirmed in play — real inter-arrival gaps chief among them. Gated on
    /// <c>OldWestTownMod.Settings.telemetryLoggingEnabled</c>, off by default; every method's
    /// first line is that same check, so a normal game pays for this at most one bool read per
    /// already-infrequent call site (an arrival, a midnight settlement, a stickup roll — never
    /// a per-tick path). No saved state of its own: this only ever prints what its caller already
    /// computed or already holds.
    /// </summary>
    internal static class Telemetry
    {
        /// <summary>One line per customer arrival: ticks since the last one (organic or
        /// guaranteed), group size, total purse, and which of the two ways it fired. The purse
        /// sum is computed in here, not at the call site, so it's never paid for while telemetry
        /// is off.</summary>
        internal static void LogArrival(Map map, List<Pawn> pawns, int ticksSinceLast, bool guaranteed)
        {
            if (!OldWestTownMod.Settings.telemetryLoggingEnabled) return;

            int purse = 0;
            for (int i = 0; i < pawns.Count; i++) purse += ShopTransaction.SilverCarriedBy(pawns[i]);

            Log.Message($"[OldWestTown] [telemetry] arrival on {map}: ticksSinceLast={ticksSinceLast}, "
                + $"groupSize={pawns.Count}, totalPurse={purse}, guaranteed={guaranteed}");
        }

        /// <summary>One line per nightly settlement: the day's verdict figures, captured by the
        /// caller before <c>JudgeTheDay</c> clears them, and how far reputation moved.</summary>
        internal static void LogSettlement(Map map, int patronsToday, int unservedToday,
            float serviceScoreToday, float reputationBefore, float reputationAfter)
        {
            if (!OldWestTownMod.Settings.telemetryLoggingEnabled) return;

            Log.Message($"[OldWestTown] [telemetry] settlement on {map}: patrons={patronsToday}, "
                + $"unserved={unservedToday}, serviceScore={serviceScoreToday:0.00}, "
                + $"reputation {reputationBefore:0.00} -> {reputationAfter:0.00}");
        }

        /// <summary>One line per stickup MTB roll, fired or not — the roadmap's own explicit
        /// requirement, since "never fires" and "never rolls" are indistinguishable without
        /// logging every attempt rather than only the successful ones.</summary>
        internal static void LogStickupRoll(Map map, int tillSilver, float mtbDays, bool fired)
        {
            if (!OldWestTownMod.Settings.telemetryLoggingEnabled) return;

            Log.Message($"[OldWestTown] [telemetry] stickup roll on {map}: tillSilver={tillSilver}, "
                + $"mtbDays={mtbDays:0.00}, fired={fired}");
        }
    }
}
