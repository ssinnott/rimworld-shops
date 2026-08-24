using System.Collections.Generic;
using System.Linq;
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
    /// what the player has actually built — the size from <see cref="TownEconomy.Appeal"/>, the
    /// purse from <see cref="TownEconomy.PurseFactor"/>. Which faction actually shows up is
    /// separately biased by that faction's own standing with the town — see
    /// <see cref="ChooseWeightedFaction"/> — so treating one faction well pulls them back more
    /// often without touching anyone else's arrivals.
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

            // TryResolveParms is non-virtual and can't be intercepted, so the only safe way to
            // bias which faction shows up is to let it run to completion, unmodified, and
            // overwrite its result afterward -- never to pre-seed parms.faction and hope a
            // method we can't inspect happens to honor it.
            Faction picked = ChooseWeightedFaction(parms, econ);
            if (picked != null) parms.faction = picked;

            if (parms.faction.HostileTo(Faction.OfPlayer)) return false;

            List<Pawn> pawns = SpawnPawns(parms);
            if (pawns.Count == 0) return false;

            IntVec3 townCenter = FindTownCentre(econ, map);
            // How many came is appeal; what they carry is what is actually on the shelves.
            float purseScale = econ.PurseFactor * OldWestTownMod.Settings.customerWealth;

            for (int i = 0; i < pawns.Count; i++)
            {
                GivePurse(pawns[i], purseScale);
                // A band, not a flat top-up: a Meal service wants genuinely hungry customers to
                // sell to, not a group who all arrive fully fed.
                if (pawns[i].needs?.food != null)
                    pawns[i].needs.food.CurLevelPercentage = Rand.Range(0.4f, 0.9f);
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

        /// <summary>
        /// Re-picks <see cref="IncidentParms.faction"/> by weighted draw over each candidate's
        /// standing with the town, so a faction the player treats well is more likely to be the
        /// one that shows up next. Draws from vanilla's own CandidateFactions pool (never a
        /// hand-rolled filter over every known faction) so this can't silently drift out of sync
        /// with whatever vanilla itself considers a valid source of visitors, layering
        /// <see cref="TownEconomy.IsEligibleFaction"/> on top for the standing-specific
        /// exclusions (hostile, the player, no settlements). Returns null — leave vanilla's own
        /// pick alone — whenever nothing qualifies, or anything about this goes wrong:
        /// CandidateFactions is normally only ever called from inside TryResolveParms itself, and
        /// nothing here can prove it has no expectations about ambient state that call site sets
        /// up first.
        /// </summary>
        private Faction ChooseWeightedFaction(IncidentParms parms, TownEconomy econ)
        {
            try
            {
                // Idempotent (a pure recomputation from current appeal and settings, read
                // straight out of this file's own override above) -- calling it again costs
                // nothing and removes any dependency on whether TryResolveParms's internals are
                // guaranteed to have populated parms.points before CandidateFactions might read it.
                ResolveParmsPoints(parms);

                List<Faction> candidates = CandidateFactions(parms, false).Where(econ.IsEligibleFaction).ToList();
                if (candidates.Count == 0)
                {
                    // Vanilla's own "desperate" widening, applied the same way it presumably is internally.
                    candidates = CandidateFactions(parms, true).Where(econ.IsEligibleFaction).ToList();
                }
                if (candidates.Count == 0) return null;

                candidates.TryRandomElementByWeight(f => econ.ArrivalWeight(f), out Faction result);
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The middle of the shopping district — where customers head when idle.</summary>
        private static IntVec3 FindTownCentre(TownEconomy econ, Map map)
        {
            IntVec3 sum = IntVec3.Zero;
            int count = 0;
            foreach (CompBusiness shop in econ.OpenShops())
            {
                sum += shop.parent.Position;
                count++;
            }

            if (count == 0) return map.Center;

            IntVec3 centre = new IntVec3(sum.x / count, 0, sum.z / count);
            if (centre.InBounds(map) && centre.Walkable(map)) return centre;

            // The average of several shops can land inside a wall; fall back to a real shop door.
            foreach (CompBusiness shop in econ.OpenShops()) return shop.parent.Position;
            return map.Center;
        }

        /// <summary>Gives a customer money to spend. How rich they are tracks what is actually ON the
        /// shelves at market value — not the town's draw, and not the player's markup. Scaling this
        /// off appeal meant a good name and a third trade fattened purses, and meant every
        /// functioning town sat pinned at the top of the range, where further investment bought
        /// nothing.</summary>
        private static void GivePurse(Pawn pawn, float scale)
        {
            if (pawn?.inventory == null) return;

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
