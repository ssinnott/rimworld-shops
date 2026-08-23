using System.Collections.Generic;
using OldWestTown.Lords;
using OldWestTown.Shops;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace OldWestTown.Incidents
{
    /// <summary>
    /// Word gets around: a town with well-stocked businesses draws people who want to spend.
    /// Unlike a vanilla visitor group, the size and purse of this group are a direct function of
    /// what the player has actually built — see <see cref="TownEconomy.Appeal"/>.
    /// </summary>
    public class IncidentWorker_ShopCustomers : IncidentWorker_NeutralGroup
    {
        /// <summary>Silver a single customer carries, before appeal and settings scaling.</summary>
        private static readonly IntRange BasePurse = new IntRange(120, 450);

        private const int VisitDurationTicks = 40000; // a bit over two-thirds of a day

        protected override PawnGroupKindDef PawnGroupKindDef => PawnGroupKindDefOf.Peaceful;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) return false;

            Map map = parms.target as Map;
            TownEconomy econ = map?.GetComponent<TownEconomy>();
            if (econ == null) return false;

            // No shop worth walking to means no incident — this is what makes the event
            // feel earned rather than random.
            return econ.Appeal >= TownEconomy.MinAppealForCustomers;
        }

        protected override void ResolveParmsPoints(IncidentParms parms)
        {
            Map map = parms.target as Map;
            float appeal = map?.GetComponent<TownEconomy>()?.Appeal ?? 1f;
            parms.points = Mathf.Clamp(
                appeal * 60f * OldWestTownMod.Settings.customerVolume,
                40f, 900f);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            TownEconomy econ = map.GetComponent<TownEconomy>();
            if (econ == null) return false;

            if (!TryResolveParms(parms)) return false;
            if (parms.faction.HostileTo(Faction.OfPlayer)) return false;

            List<Pawn> pawns = SpawnPawns(parms);
            if (pawns.Count == 0) return false;

            IntVec3 townCenter = FindTownCentre(econ, map);
            float appeal = econ.Appeal;

            for (int i = 0; i < pawns.Count; i++)
            {
                GivePurse(pawns[i], appeal);
                if (pawns[i].needs?.food != null) pawns[i].needs.food.CurLevelPercentage = 1f;
            }

            LordMaker.MakeNewLord(
                parms.faction,
                new LordJob_ShopVisit(parms.faction, townCenter, VisitDurationTicks),
                map,
                pawns);

            SendStandardLetter(
                "OWT_LetterCustomersLabel".Translate(parms.faction.Name),
                "OWT_LetterCustomersText".Translate(pawns.Count, parms.faction.Name),
                LetterDefOf.PositiveEvent,
                parms,
                pawns[0]);

            return true;
        }

        /// <summary>The middle of the shopping district — where customers head when idle.</summary>
        private static IntVec3 FindTownCentre(TownEconomy econ, Map map)
        {
            IntVec3 sum = IntVec3.Zero;
            int count = 0;
            foreach (CompShopCounter shop in econ.OpenShops())
            {
                sum += shop.parent.Position;
                count++;
            }

            if (count == 0) return map.Center;

            IntVec3 centre = new IntVec3(sum.x / count, 0, sum.z / count);
            if (centre.InBounds(map) && centre.Walkable(map)) return centre;

            // The average of several shops can land inside a wall; fall back to a real shop door.
            foreach (CompShopCounter shop in econ.OpenShops()) return shop.parent.Position;
            return map.Center;
        }

        /// <summary>
        /// Gives a customer money to spend. Richer towns attract richer custom, so investment
        /// in the town compounds rather than just adding more footfall.
        /// </summary>
        private static void GivePurse(Pawn pawn, float appeal)
        {
            if (pawn?.inventory == null) return;

            float scale = Mathf.Lerp(0.7f, 2.2f, Mathf.Clamp01(appeal / 4f))
                          * OldWestTownMod.Settings.customerWealth;
            int amount = Mathf.Max(20, Mathf.RoundToInt(BasePurse.RandomInRange * scale));

            // Top up rather than replace — generated pawns sometimes already carry silver.
            int already = ShopTransaction.SilverCarriedBy(pawn);
            if (already >= amount) return;

            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = amount - already;
            pawn.inventory.innerContainer.TryAdd(silver, true);
        }
    }
}
