using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using OldWestTown.GoldRush;
using OldWestTown.Incidents;
using OldWestTown.Rivals;
using OldWestTown.Roles;
using OldWestTown.Shops;

namespace OldWestTown.DevTools
{
    /// <summary>
    /// Dev Mode debug actions, category "Old West Town" — one lever per system a short session
    /// can't otherwise reach in reasonable time: a stickup needs an hour of real-time till
    /// accumulation, a gold rush is an MTB roll, a route-tier promotion takes in-game days, a
    /// rival undercut is a ~12-day MTB. Every lever below either fires the mod's own production
    /// incident through <c>Storyteller.TryFire</c> (so a debug-spawned customer group is a
    /// completely ordinary <see cref="Lords.LordJob_ShopVisit"/>, nothing downstream the wiser)
    /// or writes directly to state a real transaction would write to anyway (a till, a pawn's
    /// inventory, an economy field) — the same shared board both pawn loops already read and
    /// write independently. None of this ever assigns a job or hand-builds a duty: see
    /// docs/DESIGN.md for why that's the one lever shape deliberately not built here.
    ///
    /// All feedback goes through <see cref="Log.Message(string)"/>, never <c>Messages.Message</c>
    /// — following the precedent <see cref="Compat.HospitalityInterop.LogDetectionState"/> set as
    /// this codebase's first Dev Mode use of <c>Log.Message</c>. This file and
    /// <see cref="Telemetry"/> are what make it more than one; every method here still only ever
    /// runs from the Debug Actions menu on a developer's own request, never during ordinary play.
    /// </summary>
    internal static class DebugActions
    {
        private const string Category = "Old West Town";

        // ------------------------------------------------------------ arrivals

        /// <summary>Fires <c>OWT_ShopCustomers</c> through the storyteller exactly the way
        /// <see cref="TownEconomy.TryAttractCustomers"/> already does — a debug-spawned group is
        /// indistinguishable from an organic one downstream. Does NOT by itself exercise the
        /// stagecoach tier's purse multiplier or VIP roll: those only apply when
        /// <see cref="TownEconomy.GuaranteedArrivalDue"/> reads true at the moment this fires —
        /// see <see cref="ExpireStagecoachArrivalClock"/> for the lever that guarantees that.</summary>
        [DebugAction(Category, "Spawn customer group now", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void SpawnCustomerGroupNow()
        {
            Map map = Find.CurrentMap;
            TownEconomy econ = map?.GetComponent<TownEconomy>();
            if (econ == null)
            {
                Log.Message("[OldWestTown] No TownEconomy on this map.");
                return;
            }

            Log.Message($"[OldWestTown] Appeal={econ.Appeal:0.00} vs MinAppealForCustomers={TownEconomy.MinAppealForCustomers:0.00} "
                + $"(the incident's own CanFireNowSub gate — a low appeal can still make TryFire report true and then do nothing).");

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(OWTDefOf.OWT_ShopCustomers.category, map);
            bool fired = Find.Storyteller.TryFire(new FiringIncident(OWTDefOf.OWT_ShopCustomers, null, parms));
            Log.Message($"[OldWestTown] SpawnCustomerGroupNow: fired={fired}.");
        }

        /// <summary>Expires the stagecoach guarantee clock so the very next arrival — this lever
        /// doesn't spawn one itself — reads <see cref="TownEconomy.GuaranteedArrivalDue"/> as
        /// true and picks up its tier's purse multiplier and VIP roll. Relative to the current
        /// tick rather than a flat sentinel, so it stays sufficient however early in a session
        /// this is called; see <see cref="TownEconomy.DebugExpireArrivalClock"/>.</summary>
        [DebugAction(Category, "Expire stagecoach arrival clock", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void ExpireStagecoachArrivalClock()
        {
            TownEconomy econ = Find.CurrentMap?.GetComponent<TownEconomy>();
            if (econ == null) return;

            econ.DebugExpireArrivalClock();
            Log.Message("[OldWestTown] Stagecoach arrival clock expired — the next arrival, organic or forced, "
                + "will read GuaranteedArrivalDue as true (needs a coach depot and a qualifying route tier to matter).");
        }

        // ------------------------------------------------------------ stickup

        /// <summary>Fires <c>OWT_Stickup</c> the same way <see cref="StickupWatch.MapComponentTick"/>
        /// already does, per the roadmap's explicit instruction to call the incident rather than
        /// duplicate its internals — the parallel stickup-risk fix owns that file's insides.</summary>
        [DebugAction(Category, "Fire a stickup", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void FireStickup()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            StickupWatch watch = map.GetComponent<StickupWatch>();
            int silver = watch?.TotalSilverAtRisk ?? 0;
            Log.Message($"[OldWestTown] stickupsEnabled={OldWestTownMod.Settings.stickupsEnabled}, "
                + $"{SilverAtRiskBreakdown(map, silver)} vs MinSilverAtRisk={StickupWatch.MinSilverAtRisk}.");

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(OWTDefOf.OWT_Stickup.category, map);
            bool fired = Find.Storyteller.TryFire(new FiringIncident(OWTDefOf.OWT_Stickup, null, parms));
            Log.Message($"[OldWestTown] FireStickup: fired={fired}.");
        }

        // ------------------------------------------------------------ gold rush

        /// <summary>Fires <c>OWT_GoldRushStrike</c> the same way <see cref="TownEconomy.TryAttractCustomers"/>
        /// fires its own incident — through the storyteller, so the 45-day <c>minRefireDays</c>
        /// still applies. A second attempt shortly after any recent firing will honestly report
        /// <c>fired=false</c> rather than silently doing nothing.</summary>
        [DebugAction(Category, "Start a gold rush", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void StartGoldRush()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            Log.Message($"[OldWestTown] goldRushEnabled={OldWestTownMod.Settings.goldRushEnabled}, "
                + $"already active={GoldRushUtility.Active(map)}.");

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(OWTDefOf.OWT_GoldRushStrike.category, map);
            bool fired = Find.Storyteller.TryFire(new FiringIncident(OWTDefOf.OWT_GoldRushStrike, null, parms));
            Log.Message($"[OldWestTown] StartGoldRush: fired={fired}.");
        }

        /// <summary>Forces the active rush straight into its bust phase — byte-for-byte the same
        /// transition <see cref="GameCondition_GoldRush.GameConditionTick"/> performs once the
        /// boom's own duration elapses, so the debug path and the real one are indistinguishable
        /// afterward. See <see cref="GameCondition_GoldRush.DebugForceBust"/>, whose own no-op
        /// guard this checks first, so a second click reports "already busted" honestly rather
        /// than repeating the first click's success message.</summary>
        [DebugAction(Category, "Force gold rush to bust", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void ForceGoldRushBust()
        {
            GameCondition_GoldRush cond = GoldRushUtility.ActiveCondition(Find.CurrentMap);
            if (cond == null)
            {
                Log.Message("[OldWestTown] No active gold rush on this map.");
                return;
            }

            if (cond.BustActive)
            {
                Log.Message("[OldWestTown] Already in its bust phase — DebugForceBust is a no-op from here.");
                return;
            }

            cond.DebugForceBust();
            Log.Message("[OldWestTown] Forced the active gold rush into its bust phase.");
        }

        // ------------------------------------------------------------ rival towns

        /// <summary>Puts every rival straight into an undercutting swing — the identical
        /// assignment <see cref="RivalTowns"/>'s own organic roll makes, just skipping the MTB
        /// wait. Still writes the swing even with the master switch off, since freezing growth
        /// while off (see <c>RivalTowns.WorldComponentTick</c>) must not also make this lever a
        /// silent no-op; it just won't show up in <c>RegionalShare</c> until re-enabled.</summary>
        [DebugAction(Category, "Force a rival undercut", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void ForceRivalUndercut()
        {
            RivalTowns rivals = Find.World?.GetComponent<RivalTowns>();
            if (rivals == null || rivals.Rivals.Count == 0)
            {
                Log.Message("[OldWestTown] No rival towns registered.");
                return;
            }

            foreach (RivalTown r in rivals.Rivals)
            {
                if (r.def == null) continue;
                r.undercutEndDay = GenDate.DaysPassed + Mathf.RoundToInt(r.def.undercutDurationDays);
            }

            Log.Message(OldWestTownMod.Settings.rivalTownsEnabled
                ? "[OldWestTown] Forced every rival into an undercutting swing."
                : "[OldWestTown] Forced every rival into an undercutting swing, but rivalTownsEnabled is off — "
                  + "CompetingPull reads 0 everywhere until it's back on.");
        }

        /// <summary>Maxes every rival's live appeal out at its own ceiling — the single slowest
        /// clock in the mod (0.002-0.003/day; each <c>RivalTownDef</c>'s own
        /// (maxAppeal-baseAppeal)/growthPerDay puts the shipped rivals at roughly 570 days to cap
        /// on their own — ~567 for Two Forks, ~575 for Prospect Junction) and one
        /// <see cref="ForceRivalUndercut"/> alone never touches, since undercutting only changes
        /// <c>PriceIndex</c>, not <c>currentAppeal</c>.</summary>
        [DebugAction(Category, "Max out rival appeal", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void MaxRivalAppeal()
        {
            RivalTowns rivals = Find.World?.GetComponent<RivalTowns>();
            if (rivals == null || rivals.Rivals.Count == 0)
            {
                Log.Message("[OldWestTown] No rival towns registered.");
                return;
            }

            foreach (RivalTown r in rivals.Rivals)
            {
                if (r.def == null) continue;
                r.currentAppeal = r.def.maxAppeal;
            }

            Log.Message("[OldWestTown] Every rival's appeal maxed out at its own ceiling.");
        }

        // ------------------------------------------------------------ settlement / reputation

        /// <summary>Rolls the day over on demand — the same <c>RollOverDay</c> every real
        /// midnight calls. Reads the day's own verdict figures before rolling, since judging the
        /// day is also what clears them. Does not touch <c>lastDayRolled</c>: the day-of-year
        /// gate that guards the next real midnight changes regardless of anything this writes,
        /// so there is nothing here that could make it double-fire.</summary>
        [DebugAction(Category, "Advance to nightly settlement", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void AdvanceToNightlySettlement()
        {
            TownEconomy econ = Find.CurrentMap?.GetComponent<TownEconomy>();
            if (econ == null) return;

            int patrons = econ.PatronsToday;
            int unserved = econ.UnservedToday;
            float serviceScore = econ.ServiceScoreToday;
            float repBefore = econ.Reputation;

            econ.DebugForceSettlement();

            Log.Message($"[OldWestTown] Nightly settlement forced. Patrons={patrons}, Unserved={unserved}, "
                + $"ServiceScore={serviceScore:0.00}, Reputation {repBefore:0.00} -> {econ.Reputation:0.00}.");
        }

        /// <summary>Sets town reputation directly — unlocks route-tier promotion (<c>RouteTier</c>
        /// reads <c>Appeal</c>, which factors <c>StandingFactor</c> off reputation), gold-rush
        /// bust recovery (<c>BustRecoveryReputation</c> = 0.45), and a regional-lead flip, all
        /// from one lever rather than three narrow ones.</summary>
        [DebugAction(Category, "Set town reputation", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void SetTownReputation()
        {
            TownEconomy econ = Find.CurrentMap?.GetComponent<TownEconomy>();
            if (econ == null) return;

            Find.WindowStack.Add(new Dialog_Slider(
                pct => "OWT_DevReputationSlider".Translate(pct),
                0, 100,
                pct =>
                {
                    econ.DebugSetReputation(pct / 100f);
                    Log.Message($"[OldWestTown] Town reputation set to {econ.Reputation:P0}.");
                },
                Mathf.RoundToInt(econ.Reputation * 100f)));
        }

        // ------------------------------------------------------------ till / purse

        /// <summary>Tops up the selected counter's till. A slider, not a flat constant, so a
        /// tester can manufacture both "the house can cover it" and "the house can't" —
        /// deliberately, since a shop running out of something to give (here, silver) is a
        /// legible outcome this mod treats as a feature, not a bug to route around.</summary>
        [DebugAction(Category, "Fill selected till", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void FillSelectedTill()
        {
            Thing sel = Find.Selector.SingleSelectedThing;
            CompBusiness shop = sel?.TryGetComp<CompBusiness>();
            if (shop == null)
            {
                Log.Message("[OldWestTown] Select a business counter first.");
                return;
            }

            string shopLabel = sel.LabelCap;
            Find.WindowStack.Add(new Dialog_Slider(
                amount => "OWT_DevFillTillSlider".Translate(amount, shopLabel),
                0, 3000,
                amount =>
                {
                    if (amount <= 0) return;
                    Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = amount;
                    shop.AddToTill(silver);
                    Log.Message($"[OldWestTown] Added {amount} silver to {shopLabel}'s till — now {shop.TillSilver}.");
                },
                0));
        }

        /// <summary>Tops up the selected pawn's purse, reusing <see cref="IncidentWorker_ShopCustomers.GivePurse"/>
        /// unmodified — same <c>scale</c> expression <c>TryExecuteWorker</c> already computes, so
        /// the slider is a multiplier on the identical formula a real arrival uses, not a second
        /// one. <c>GivePurse</c>'s own floor (never less than 20 silver) still applies even at 0%
        /// — that is production behaviour, not a bug in this lever.</summary>
        [DebugAction(Category, "Give selected pawn a purse", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void GiveSelectedPawnPurse()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null)
            {
                Log.Message("[OldWestTown] Select a pawn first.");
                return;
            }

            TownEconomy econ = pawn.Map?.GetComponent<TownEconomy>();
            float scale = (econ?.PurseFactor ?? 1f) * OldWestTownMod.Settings.customerWealth;

            Find.WindowStack.Add(new Dialog_Slider(
                pct => "OWT_DevPurseMultiplierSlider".Translate(pct),
                0, 500,
                pct =>
                {
                    IncidentWorker_ShopCustomers.GivePurse(pawn, scale, pct / 100f);
                    Log.Message($"[OldWestTown] {pawn.LabelShort} now carries {ShopTransaction.SilverCarriedBy(pawn)} silver.");
                },
                100));
        }

        // ------------------------------------------------------------ dump

        /// <summary>Dumps the full town-economy state for every map, then the world-scoped rival
        /// roster once. Everything read here is already public/internal — this composes existing
        /// getters into one printout rather than adding a new one.</summary>
        [DebugAction(Category, "Dump town economy state", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void DumpTownEconomyState()
        {
            foreach (Map map in Find.Maps)
            {
                TownEconomy econ = map.GetComponent<TownEconomy>();
                if (econ == null) continue;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[OldWestTown] Town economy on {map}:");
                sb.AppendLine($"  Appeal={econ.Appeal:0.00} (Businesses={econ.BusinessScore:0.00}, "
                    + $"GoodsFactor={econ.GoodsFactor:0.00}, StandingFactor={econ.StandingFactor:0.00})");
                sb.AppendLine($"  Reputation={econ.Reputation:P0}, PriceIndex={econ.PriceIndex:0.00}, "
                    + $"MarketPull={econ.MarketPull:0.00}, RegionalShare={econ.RegionalShare:P0}");

                var tier = econ.RouteTier;
                string tierLabel = tier != null ? tier.LabelCap.ToString() : "none";
                sb.AppendLine($"  RouteTier={tierLabel}, TicksSinceLastArrival={econ.TicksSinceLastArrival}, "
                    + $"GuaranteedArrivalDue={econ.GuaranteedArrivalDue}");

                List<KeyValuePair<Faction, float>> standings = econ.TrackedStandings.ToList();
                if (standings.Count > 0)
                {
                    sb.AppendLine("  Standings: " + string.Join(", ",
                        standings.Select(kv => $"{kv.Key.Name}={kv.Value:P0}")));
                }

                StickupWatch watch = map.GetComponent<StickupWatch>();
                if (watch != null)
                {
                    sb.AppendLine($"  StickupWatch: {SilverAtRiskBreakdown(map, watch.TotalSilverAtRisk)} "
                        + $"vs MinSilverAtRisk={StickupWatch.MinSilverAtRisk}");
                }

                GameCondition_GoldRush rush = GoldRushUtility.ActiveCondition(map);
                sb.AppendLine(rush == null
                    ? "  Gold rush: inactive"
                    : $"  Gold rush: active (boom={GoldRushUtility.BoomActive(map)}, "
                      + $"bust={GoldRushUtility.BustActive(map)}) — {rush.Description}");

                foreach (CompBusiness shop in econ.Shops)
                {
                    if (shop?.parent == null) continue;
                    sb.Append($"  {shop.parent.LabelCap} [{shop.Kind?.defName ?? "?"}]: ")
                      .Append(shop.Open ? "open" : "closed")
                      .Append(shop.Staffed ? ", staffed" : ", unstaffed")
                      .Append($", till={shop.TillSilver}, revenueToday={shop.revenueToday}, "
                          + $"walkoutsToday={shop.walkoutsToday}");
                    if (shop.HasWager)
                    {
                        sb.Append($", payoutsToday={shop.payoutsToday}, shortfallsToday={shop.shortfallsToday}");
                    }
                    if (shop.lifetimeStolen > 0)
                    {
                        sb.Append($", lifetimeStolen={shop.lifetimeStolen} ({shop.lifetimeRobberies} robberies)");
                    }
                    sb.AppendLine();
                }

                Log.Message(sb.ToString());
            }

            RivalTowns rivals = Find.World?.GetComponent<RivalTowns>();
            if (rivals != null && rivals.Rivals.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[OldWestTown] Rival towns:");
                foreach (RivalTown r in rivals.Rivals)
                {
                    if (r.def == null) continue;
                    sb.AppendLine($"  {r.def.LabelCap}: appeal={r.currentAppeal:0.00}, "
                        + $"undercutting={r.Undercutting}, priceIndex={r.PriceIndex:0.00}, pull={r.Pull:0.00}");
                }
                Log.Message(sb.ToString());
            }
        }

        // ------------------------------------------------------------ trouble (optional lever)

        /// <summary>Nudges the selected pawn's rowdiness at the first rowdiness-capable business
        /// found on their map — exercises the sheriff-dispatch/disturbance loop, which otherwise
        /// needs many real service rounds to build up. Falls back to a flat nudge when the
        /// matched service's own <see cref="ServiceWorker.RowdinessPerUse"/> reads zero: a wager's
        /// rowdiness is outcome-dependent and never flows through that constant at all (see
        /// <see cref="ServiceWorker.CanCauseTrouble"/>), so a gambling-hall-only map would
        /// otherwise make this lever a silent no-op for the one business it exists to test.
        /// Refuses a player-faction pawn outright: <c>TroubleUtility.Notify_ServiceRound</c> only
        /// checks that its target is humanlike, never whose faction they're in, because in
        /// production nothing ever hands it a colonist — <c>JobDriver_ColonistUseService</c> is a
        /// deliberately separate driver that never names <c>TroubleUtility</c> at all. Without
        /// this check, this lever would be the one caller that could still do it, banking a real,
        /// persisted disturbance and reputation hit against a colonist who was never a
        /// customer.</summary>
        [DebugAction(Category, "Spike selected pawn's rowdiness", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.Action)]
        private static void SpikeSelectedPawnRowdiness()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn?.Map == null)
            {
                Log.Message("[OldWestTown] Select a pawn first.");
                return;
            }

            if (pawn.Faction == Faction.OfPlayer)
            {
                Log.Message("[OldWestTown] Select a visiting customer, not a colonist — a colonist's "
                    + "rowdiness never reaches the town's books in play (see JobDriver_ColonistUseService), "
                    + "and this lever shouldn't be the one path that lets it.");
                return;
            }

            TownEconomy econ = pawn.Map.GetComponent<TownEconomy>();
            if (econ == null)
            {
                Log.Message("[OldWestTown] No TownEconomy on this map.");
                return;
            }

            CompBusiness target = null;
            ServiceDef targetService = null;
            foreach (CompBusiness shop in econ.Shops)
            {
                ServiceDef sd = shop?.Kind?.services?.FirstOrDefault(s => s?.worker != null && s.worker.CanCauseTrouble);
                if (sd == null) continue;
                target = shop;
                targetService = sd;
                break;
            }

            if (target == null)
            {
                Log.Message("[OldWestTown] No rowdiness-capable business on this map.");
                return;
            }

            float rowdiness = targetService.worker.RowdinessPerUse;
            if (rowdiness <= 0f) rowdiness = 0.5f; // see doc comment above: a wager's own rowdiness
                                                    // never reads through this constant at all.

            TroubleUtility.Notify_ServiceRound(pawn, target, rowdiness);
            Log.Message($"[OldWestTown] Nudged {pawn.LabelShort}'s rowdiness at {target.parent.Label}.");
        }

        /// <summary>The at-risk total split into where the silver actually is. Since the fix that
        /// made risk follow the silver rather than the till, "how much is exposed" and "how much
        /// is collected" are different questions, and a dev staring at one number cannot tell
        /// whether a hauler is behind or a till is simply full. Till silver lives in each shop's
        /// own ThingOwner and so cannot be double-counted; the floor share is derived from the
        /// deduplicated total rather than re-summed, so it can never disagree with the number the
        /// stickup clock is actually reading.</summary>
        private static string SilverAtRiskBreakdown(Map map, int atRisk)
        {
            TownEconomy econ = map.GetComponent<TownEconomy>();
            int inTills = 0;
            if (econ != null)
            {
                IReadOnlyList<CompBusiness> shops = econ.Shops;
                for (int i = 0; i < shops.Count; i++)
                {
                    CompBusiness shop = shops[i];
                    if (shop?.parent != null && shop.parent.Spawned) inTills += shop.TillSilver;
                }
            }
            return $"TotalSilverAtRisk={atRisk} (tills={inTills}, floors={atRisk - inTills})";
        }

    }
}
