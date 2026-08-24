using System.Collections.Generic;
using OldWestTown.AI;
using OldWestTown.Incidents;
using OldWestTown.Shops;
using RimWorld;
using Verse;
using Verse.AI;

namespace OldWestTown.Compat
{
    /// <summary>
    /// The active half of the Hospitality bridge (see <see cref="HospitalityInterop"/> for
    /// detection). Every ScanIntervalTicks, while Hospitality is present and the bridge is
    /// switched on, offers one shopping job to any idle Hospitality guest on this map — through
    /// the same Pawn_JobTracker.TryTakeOrderedJob door a player's own forced order already uses,
    /// gated on the guest's own AI having already declared it has nothing better to do.
    ///
    /// This never touches a guest's Lord or PawnDuty, never interrupts a running job, and never
    /// waits on a response: it hands out one independent job and its involvement with that
    /// specific pawn ends the moment TryTakeOrderedJob returns. That is what keeps this from
    /// becoming a second synchronizing loop layered on top of the one already described in
    /// docs/architecture.md — see docs/DESIGN.md for the full reasoning, and
    /// docs/customers.md#hospitality-guests for what this looks like in play.
    /// </summary>
    public class HospitalityBridge : MapComponent
    {
        private const int ScanIntervalTicks = 250;

        /// <summary>
        /// Deliberately unpersisted, mirroring CompBusiness.lastStaffedTick's own "reload clears
        /// it, and it re-establishes itself within moments, harmlessly" precedent — a reload
        /// just starts every guest's cooldown fresh, which can only make the bridge briefly more
        /// generous, never stuck.
        ///
        /// Bounds one specific failure mode: a bridged guest has no CustomerRecord (that lives
        /// on OUR OWN LordJob_ShopVisit, which a Hospitality-owned pawn structurally can't be
        /// running — see HospitalityInterop), so JobDriver_PatronizeBusiness.WalkOut's
        /// refusedShops write silently no-ops for them, and without this they could be offered
        /// the same chronically-unstaffed shop on every single scan. Rather than rebuilding
        /// refusedShops/causedTrouble semantics for a pawn that structurally can't have either,
        /// this applies one flat rule: once a (pawn, shop) pair has been dispatched — success or
        /// failure, this doesn't distinguish — that pair is off the table for one shop's own
        /// customerPatienceTicks. The honest cost: a guest who successfully buys from a good,
        /// staffed shop is throttled from immediately re-buying at that same shop too. See
        /// docs/DESIGN.md for the full accounting, including what this deliberately does NOT
        /// cover (a bridged guest's causedTrouble is likewise unreachable, and is not separately
        /// mitigated — the same reasoning is there too).
        /// </summary>
        private readonly Dictionary<(Pawn Pawn, Thing Shop), int> coolingDownUntil =
            new Dictionary<(Pawn Pawn, Thing Shop), int>();

        /// <summary>
        /// Guests already granted the one-time arrival-equivalent silver top-up (see
        /// TryOfferShopping) — GivePurse itself has no memory of having already run, so without
        /// this it would fire again on every single scan a guest is still idle, re-rolling and
        /// ratcheting their purse toward the top of the range instead of granting the single
        /// top-up a native customer's arrival gets. Deliberately unpersisted for the same reason
        /// coolingDownUntil is: a reload just makes a guest eligible for one more top-up, which
        /// GivePurse's own "only ever tops up a shortfall" shape already bounds to something
        /// small, never unbounded. Pruned the same way, in PruneCooldowns.
        /// </summary>
        private readonly HashSet<Pawn> fundedPawns = new HashSet<Pawn>();

        /// <summary>Fires the one in-game message the first time this save's bridge actually
        /// dispatches a guest — the player's one confirmation that the entire unverified
        /// detection chain in HospitalityInterop worked at least once. See docs/DESIGN.md.</summary>
        private bool hasAnnouncedBridge;

        public HospitalityBridge(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Cheapest checks first: with Hospitality absent (the common case, and every
            // install until a player adds it) this is one modulo and one static bool read per
            // tick, the same shape TownEconomy's own arrival clock already uses.
            if (Find.TickManager.TicksGame % ScanIntervalTicks != 0) return;
            if (!HospitalityInterop.Present) return;
            if (!map.IsPlayerHome) return;
            if (!OldWestTownMod.Settings.hospitalityBridgeEnabled) return;

            TownEconomy econ = map.GetComponent<TownEconomy>();
            if (econ == null) return;

            bool anythingToOffer = false;
            foreach (CompBusiness shop in econ.OpenShops())
            {
                if (!shop.HasAnythingToOffer) continue;
                anythingToOffer = true;
                break;
            }
            if (!anythingToOffer) return;

            PruneCooldowns();

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (IsBridgeCandidate(p)) TryOfferShopping(p, econ);
            }
        }

        /// <summary>Everything that has to be true before a pawn is even considered: not ours,
        /// not incapacitated, actually a Hospitality guest, and — the load-bearing one — idle by
        /// its own AI's own reckoning, so this never interrupts anything.</summary>
        private static bool IsBridgeCandidate(Pawn p)
        {
            if (p.Faction == Faction.OfPlayer || p.Dead || p.Downed || p.IsPrisoner) return false;
            if (p.RaceProps?.Humanlike != true) return false;
            if (p.InMentalState) return false;
            if (p.HostileTo(Faction.OfPlayer)) return false;
            if (p.jobs?.curDriver is IBusinessPatron) return false; // already mid-purchase
            if (p.mindState?.IsIdle != true) return false;
            return HospitalityInterop.IsHospitalityGuest(p);
        }

        private void TryOfferShopping(Pawn pawn, TownEconomy econ)
        {
            if (OldWestTownMod.Settings.hospitalityGuestsCarrySilver && fundedPawns.Add(pawn))
            {
                // The identical top-up a native customer gets on arrival (IncidentWorker_ShopCustomers.GivePurse),
                // applied here instead, since a Hospitality guest doesn't arrive through our own incident.
                // fundedPawns.Add returns false once this guest has already been funded, so this
                // can only ever fire once per guest -- see fundedPawns' own doc comment for why
                // that gate has to live here rather than inside GivePurse itself.
                IncidentWorker_ShopCustomers.GivePurse(
                    pawn, econ.PurseFactor * OldWestTownMod.Settings.customerWealth);
            }

            // lodgingAllowed: false — Hospitality is already housing this pawn. See
            // HospitalityInterop and docs/DESIGN.md for why the two mods can't end up fighting
            // over the same guest even without this guard.
            Job job = JobGiver_BuyFromShop.PickShoppingJob(pawn, lodgingAllowed: false);
            if (job == null) return;

            Thing shopThing = job.GetTarget(TargetIndex.B).Thing;

            if (shopThing != null
                && coolingDownUntil.TryGetValue((pawn, shopThing), out int until)
                && until > Find.TickManager.TicksGame)
            {
                return;
            }

            // The same sanctioned door a player's own forced order already uses. Only ever
            // reached once IsBridgeCandidate has confirmed this pawn is idle, so there is never
            // anything running here to interrupt.
            if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc)) return;

            if (shopThing != null)
            {
                int cooldown = shopThing.TryGetComp<CompBusiness>()?.Kind?.customerPatienceTicks ?? 2500;
                coolingDownUntil[(pawn, shopThing)] = Find.TickManager.TicksGame + cooldown;
            }

            if (!hasAnnouncedBridge)
            {
                hasAnnouncedBridge = true;
                Messages.Message(
                    "OWT_HospitalityBridgeEngaged".Translate(pawn.LabelShort, shopThing?.Label ?? pawn.LabelShort),
                    new LookTargets(pawn),
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>Drops any cooldown entry that has already lapsed, or whose pawn has left
        /// this map (Hospitality's own guest, so this mod has no other hook to clean up on) —
        /// keeps the table from growing across a long save with many guests coming and going.
        /// Also drops a funded-guest entry once its pawn has left the map, so a guest who
        /// checks out and later checks back in again reads as a fresh arrival, eligible for one
        /// more top-up, rather than a returning one this mod remembers funding forever.</summary>
        private void PruneCooldowns()
        {
            if (coolingDownUntil.Count > 0)
            {
                int now = Find.TickManager.TicksGame;
                List<(Pawn Pawn, Thing Shop)> stale = new List<(Pawn Pawn, Thing Shop)>();
                foreach (KeyValuePair<(Pawn Pawn, Thing Shop), int> kv in coolingDownUntil)
                {
                    bool lapsed = kv.Value <= now;
                    bool gone = kv.Key.Pawn == null || !kv.Key.Pawn.Spawned || kv.Key.Pawn.Map != map;
                    if (lapsed || gone) stale.Add(kv.Key);
                }
                for (int i = 0; i < stale.Count; i++) coolingDownUntil.Remove(stale[i]);
            }

            if (fundedPawns.Count > 0)
            {
                fundedPawns.RemoveWhere(p => p == null || !p.Spawned || p.Map != map);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hasAnnouncedBridge, "hasAnnouncedBridge");
        }
    }
}
