using RimWorld;
using UnityEngine;
using Verse;
using OldWestTown.GoldRush;
using OldWestTown.Shops;

namespace OldWestTown.Incidents
{
    /// <summary>
    /// A strike nearby: creates and registers <see cref="OWTDefOf.OWT_GoldRushCondition"/>. Every
    /// hook is written explicitly rather than trusted to an unread base implementation -- the
    /// same "never trust a base class for the letter" precedent IncidentWorker_ShopCustomers and
    /// IncidentWorker_Stickup already set for this codebase.
    /// </summary>
    public class IncidentWorker_GoldRushStrike : IncidentWorker_MakeGameCondition
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) return false;
            if (!OldWestTownMod.Settings.goldRushEnabled) return false;

            Map map = parms.target as Map;
            TownEconomy econ = map?.GetComponent<TownEconomy>();
            if (econ == null) return false;
            if (GoldRushUtility.Active(map)) return false; // defensive, alongside base's own presumed check

            return econ.Appeal >= TownEconomy.MinAppealForCustomers;
        }

        // Written explicitly rather than calling base.TryExecuteWorker -- see docs/DESIGN.md.
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            if (map == null || GoldRushUtility.Active(map)) return false;

            int durationTicks = Mathf.RoundToInt(def.durationDays.RandomInRange * 60000f);
            GameCondition cond = GameConditionMaker.MakeCondition(OWTDefOf.OWT_GoldRushCondition, durationTicks);
            map.gameConditionManager.RegisterCondition(cond);

            // No LookTargets: a rush has no single arriving pawn or building to point the camera
            // at, the same "town-wide, nothing specific to jump to" shape
            // TownEconomy.CheckRouteTierChange's own route-tier letter already uses.
            Find.LetterStack.ReceiveLetter(
                "OWT_LetterGoldRushLabel".Translate(),
                "OWT_LetterGoldRushText".Translate(),
                LetterDefOf.PositiveEvent);

            return true;
        }
    }
}
