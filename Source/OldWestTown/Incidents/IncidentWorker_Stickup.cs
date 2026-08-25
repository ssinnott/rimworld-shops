using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.Incidents
{
    /// <summary>
    /// Silver sitting exposed anywhere — in a till, or loose on a sales floor — is a target.
    /// Built on IncidentWorker_RaidEnemy rather than a bespoke worker: base.TryExecuteWorker,
    /// left completely untouched, already does faction resolution, pawn generation, gear and
    /// the arrival letter — everything an ordinary raid needs. The five hooks below are what
    /// turn that into a stickup instead: a small band, sized off the silver actually at risk
    /// rather than colony wealth, that walks in on foot and takes whatever it can reach, till
    /// or floor.
    /// </summary>
    public class IncidentWorker_Stickup : IncidentWorker_RaidEnemy
    {
        /// <summary>Points scale off the silver actually at risk — till and sales-floor
        /// combined, see StickupWatch.TotalSilverAtRisk (ResolveRaidPoints) — not colony wealth,
        /// hard-capped at both ends so a very rich town's stickup stays a small, focused hit
        /// rather than ballooning into an ordinary raid's own wealth-scaled size.</summary>
        private const float MinPoints = 80f;
        private const float MaxPoints = 400f;
        private const float PointsPerSilverAtRisk = 0.6f;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) return false;

            // Defensive backstop against the IncidentDef's own small residual baseChance firing
            // while the player has opted out entirely.
            if (!OldWestTownMod.Settings.stickupsEnabled) return false;

            Map map = parms.target as Map;
            StickupWatch watch = map?.GetComponent<StickupWatch>();
            if (watch == null || watch.TotalSilverAtRisk < StickupWatch.MinSilverAtRisk) return false;

            // Don't stack a robbery onto an existing crisis — this is meant to be a deliberate
            // risk the player can see coming, not pile-on during an unrelated raid or mech cluster.
            return !GenHostility.AnyHostileActiveThreatToPlayer(map);
        }

        protected override void ResolveRaidPoints(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            float silver = map.GetComponent<StickupWatch>()?.TotalSilverAtRisk ?? 0f;
            parms.points = Mathf.Clamp(silver * PointsPerSilverAtRisk, MinPoints, MaxPoints);
        }

        // Public, not protected: confirmed by the compiler (CS0507), not by refdump — it
        // reports member existence and signature but never accessibility. The base
        // IncidentWorker_Raid declares both of these public, unlike CanFireNowSub/
        // ResolveRaidPoints/the letter hooks below, which are protected.
        public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            // Force-assigned, never delegated to base or to RaidStrategyWorker.CanUseWith: this
            // raid is a stickup, full stop, not whatever an ordinary raid's own strategy roll
            // might otherwise land on.
            parms.raidStrategy = OWTDefOf.OWT_StickupStrategy;
        }

        public override void ResolveRaidArriveMode(IncidentParms parms)
        {
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
        }

        protected override string GetLetterLabel(IncidentParms parms)
        {
            return "OWT_LetterStickupLabel".Translate();
        }

        protected override string GetLetterText(IncidentParms parms, List<Pawn> pawns)
        {
            // The "leader" named here is flavor only — nothing about this pawn is tracked beyond
            // this one letter. See docs/outlaws.md for why a recurring, named outlaw leader (the
            // roadmap's original wanted-board idea) was cut rather than built.
            Pawn leader = pawns != null && pawns.Count > 0 ? pawns[0] : null;
            return "OWT_LetterStickupText".Translate(parms.faction?.Name ?? "", leader?.LabelShort ?? "");
        }
    }
}
