using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using OldWestTown.Lords;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace OldWestTown.Compat
{
    /// <summary>
    /// Detects a loaded Hospitality installation and recognizes its guests, entirely by
    /// reflection: this mod carries no reference to Hospitality's assembly, hard or optional,
    /// and nothing below names one of its types or members. Every fact this class leans on is
    /// unverified against a real Hospitality build — this sandbox has no such assembly, no
    /// decompile of one, and no way to launch the game against one. See docs/DESIGN.md's
    /// "Hospitality bridge" section for the full confidence accounting; the short version:
    ///
    /// <see cref="Present"/> is true iff some loaded assembly's simple name is "Hospitality"
    /// (case-insensitive) — a guess about Hospitality's own build output. If it's wrong, this
    /// stays false forever and the whole bridge is permanently, silently inert: indistinguishable
    /// from Hospitality not being installed, and no more expensive than that to carry.
    ///
    /// <see cref="IsHospitalityGuest"/> only looks at STRUCTURE once <see cref="Present"/> is
    /// true: does this pawn's Lord run a LordJob from Hospitality's assembly, or does it carry
    /// any ThingComp from that assembly? Neither check names a Hospitality type, so a rename or
    /// a restructure inside a future Hospitality version degrades this to "never matches" rather
    /// than a crash.
    /// </summary>
    internal static class HospitalityInterop
    {
        /// <summary>
        /// Resolved once, the first time anything in this class is touched — either the
        /// settings window or the first map tick, whichever comes first. C#'s own
        /// static-initialization guarantee (exactly once, before first use) already gives
        /// "compute once, safely" for free, so there is no separate Init() to call from
        /// OldWestTownMod's constructor. Guarded so that whatever GetAssemblies() or a
        /// misbehaving loaded assembly's GetName() might throw leaves this null — "Hospitality
        /// not detected" — rather than leaving the type permanently broken for the rest of the
        /// game's lifetime (a static initializer that throws poisons every later access with
        /// TypeInitializationException).
        /// </summary>
        private static readonly Assembly HospitalityAssembly = FindHospitalityAssembly();

        /// <summary>True once a loaded assembly named exactly "Hospitality" (case-insensitive)
        /// has been found. See class remarks: this is the one unverifiable guess everything
        /// else here sits behind.</summary>
        internal static bool Present => HospitalityAssembly != null;

        private static Assembly FindHospitalityAssembly()
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "Hospitality", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// True if <paramref name="pawn"/> belongs to Hospitality — recognized structurally, by
        /// which assembly owns the Lord/LordJob governing them or any ThingComp attached to
        /// them, never by a guessed type or member name. Either signal alone is enough; both
        /// being wrong at once is the only way this returns a false negative for a real
        /// Hospitality guest.
        ///
        /// Never true for one of this mod's own customers, but for two different reasons
        /// depending on the signal. A pawn has exactly one Lord, and a Lord has exactly one
        /// LordJob, so the LordJob signal alone structurally cannot match one of our own
        /// customers — their LordJob is OldWestTown.Lords.LordJob_ShopVisit, declared in this
        /// assembly, not Hospitality's. The ThingComp signal has no such guarantee on its own:
        /// nothing says a comp Hospitality attaches, if it attaches one at all (see class
        /// remarks), is scoped to guests it personally invited rather than to every humanlike
        /// pawn on the map. So this checks explicitly first, unconditionally, before either
        /// signal runs: a pawn already running LordJob_ShopVisit is never a Hospitality guest,
        /// full stop. That explicit check, not an inference from the single-Lord invariant, is
        /// what actually makes the two mods structurally unable to fight over the same guest;
        /// see HospitalityBridge and docs/DESIGN.md.
        /// </summary>
        internal static bool IsHospitalityGuest(Pawn pawn)
        {
            Assembly hospitality = HospitalityAssembly;
            if (hospitality == null || pawn == null) return false;

            // Nothing below calls a member Hospitality declares — only System.Type.Assembly and
            // this mod's own already-proven vanilla API (GetLord, LordJob, AllComps). There is
            // no realistic path for a future Hospitality version to make this throw; the guard
            // is defense-in-depth matching this codebase's style for anything touching a pawn
            // it doesn't own, not a load-bearing requirement.
            try
            {
                LordJob lordJob = pawn.GetLord()?.LordJob;

                // See doc comment above: checked first and unconditionally, because the
                // ThingComp signal below has no structural guarantee against matching one of
                // this mod's own customers the way the LordJob signal does.
                if (lordJob is LordJob_ShopVisit) return false;

                if (lordJob != null && lordJob.GetType().Assembly == hospitality) return true;

                List<ThingComp> comps = pawn.AllComps;
                if (comps != null)
                {
                    for (int i = 0; i < comps.Count; i++)
                    {
                        if (comps[i] != null && comps[i].GetType().Assembly == hospitality) return true;
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        /// <summary>
        /// Dev-mode-only diagnostic: dumps, for every non-player humanlike pawn on every map,
        /// whether <see cref="IsHospitalityGuest"/> recognizes them, their Lord's LordJob type,
        /// and every comp they carry — everything a maintainer with a real Hospitality install
        /// needs to correct the guesses in docs/DESIGN.md without decompiling anything blind.
        /// The one deliberate exception to this codebase's zero-Log.Message history: it only
        /// ever runs from the debug-actions menu on a developer's own request, never during
        /// ordinary play.
        /// </summary>
        [DebugAction("Old West Town", "Hospitality bridge state", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void LogDetectionState()
        {
            Log.Message(Present
                ? $"[OldWestTown] Hospitality assembly present: {HospitalityAssembly.FullName}"
                : "[OldWestTown] Hospitality assembly present: false");

            foreach (Map map in Find.Maps)
            {
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p.Faction == Faction.OfPlayer || p.RaceProps?.Humanlike != true) continue;

                    string lordJobName = p.GetLord()?.LordJob?.GetType().FullName ?? "(no Lord)";
                    string comps = p.AllComps == null || p.AllComps.Count == 0
                        ? "(none)"
                        : string.Join(", ", p.AllComps.Select(c => c?.GetType().FullName ?? "(null)"));

                    Log.Message($"[OldWestTown]  {p.LabelShort} on {map}: IsHospitalityGuest={IsHospitalityGuest(p)}, "
                        + $"LordJob={lordJobName}, comps=[{comps}]");
                }
            }
        }
    }
}
