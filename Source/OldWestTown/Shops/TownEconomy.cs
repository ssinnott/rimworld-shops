using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// The town's books. Keeps the live shop register, rolls the day over, and turns trading
    /// history into the two numbers the rest of the mod cares about: <see cref="Appeal"/>
    /// (how many customers the town pulls in) and <see cref="Reputation"/> (what they'll pay).
    /// Also keeps, per <see cref="Faction"/>, that faction's own standing with the town — the
    /// thing that decides who is likelier to visit next.
    /// </summary>
    public class TownEconomy : MapComponent
    {
        /// <summary>Appeal below which no customer group will set out for this town.</summary>
        public const float MinAppealForCustomers = 0.5f;

        /// <summary>
        /// A stock-free service (a haircut) has no "quantity on the shelf" the way physical
        /// stock does — ServiceValue is just the price of one visit, an order of magnitude
        /// below a shelf's total value. Scaled up before it joins the same wealth curve, so one
        /// instance of a shipped stock-free service can clear <see cref="MinAppealForCustomers"/>
        /// on its own, the way a modestly-stocked general store already can, instead of being
        /// drowned out by a /1000 normalization tuned for physical stock.
        /// </summary>
        private const float ServiceValueWeight = 30f;

        /// <summary>How often the arrival clock is consulted. MTB math corrects for the interval.</summary>
        private const int ArrivalCheckInterval = 600;

        /// <summary>
        /// A staffed sale's nudge to a customer's own faction — five times the town-wide sale
        /// delta below, deliberately: standing has to move fast enough over normal play for a
        /// "regular" to actually read as one within a reasonable number of sales.
        /// </summary>
        private const float FactionStandingSaleDelta = 0.05f;

        /// <summary>Mirrors the global formula's own 2:1 walkout:sale ratio, at standing's sharper scale.</summary>
        private const float FactionStandingWalkoutDelta = -0.10f;

        /// <summary>The worst standing hit in the mod, deliberately worse than a walkout's —
        /// the house winning a hand for a customer and then not paying it out is a sharper
        /// trust break than slow service ever is.</summary>
        private const float FactionStandingShortfallDelta = -0.20f;

        private readonly List<CompBusiness> shops = new List<CompBusiness>();

        private int lastDayRolled = -1;

        public int revenueToday;
        public int customersServedToday;
        public int walkoutsToday;
        public int lifetimeRevenue;

        /// <summary>Rolling satisfaction, 0..1. Served customers push it up, walkouts push it down.</summary>
        private float reputation = 0.5f;

        /// <summary>
        /// Per-faction standing, 0..1, layered beside <see cref="reputation"/> rather than
        /// replacing it. Sparse on purpose: a faction with no entry here hasn't diverged from
        /// the town's own name yet, which is also the whole migration story for a save written
        /// before this existed — see <see cref="StandingWith"/>.
        /// </summary>
        private List<Faction> standingFactions = new List<Faction>();
        private List<float> standingValues = new List<float>();
        private Dictionary<Faction, float> standings = new Dictionary<Faction, float>();

        public TownEconomy(Map map) : base(map) { }

        public float Reputation => Mathf.Clamp01(reputation);

        public IReadOnlyList<CompBusiness> Shops => shops;

        public void Register(CompBusiness shop)
        {
            if (shop != null && !shops.Contains(shop)) shops.Add(shop);
        }

        public void Deregister(CompBusiness shop)
        {
            shops.Remove(shop);
        }

        /// <summary>Shops that could serve a customer right now.</summary>
        public IEnumerable<CompBusiness> OpenShops()
        {
            for (int i = 0; i < shops.Count; i++)
            {
                CompBusiness s = shops[i];
                if (s != null && s.parent != null && s.parent.Spawned && s.Open) yield return s;
            }
        }

        /// <summary>
        /// How much trade the town attracts, roughly 0..3+. Built from the number of distinct
        /// stocked businesses (breadth matters more than one huge store), the goods on display,
        /// and how well past customers were treated.
        /// </summary>
        public float Appeal
        {
            get
            {
                float kindScore = 0f;
                float stockScore = 0f;
                HashSet<ShopKindDef> kinds = new HashSet<ShopKindDef>();

                foreach (CompBusiness shop in OpenShops())
                {
                    if (!shop.HasAnythingToOffer) continue;
                    // Each additional business of a kind already present is worth less.
                    float weight = kinds.Add(shop.Kind) ? 1f : 0.35f;
                    kindScore += (shop.Kind?.appeal ?? 1f) * weight;
                    stockScore += shop.StockValue + shop.ServiceValue * ServiceValueWeight;
                }

                if (kindScore <= 0f) return 0f;

                float goods = Mathf.Sqrt(stockScore / 1000f);          // diminishing returns on wealth
                float standing = Mathf.Lerp(0.5f, 1.5f, Reputation);   // a good name doubles your draw
                return kindScore * Mathf.Clamp(goods, 0.25f, 3f) * standing;
            }
        }

        /// <summary>
        /// This faction's own standing with the town, 0..1. An untracked faction reads as the
        /// town's own <see cref="Reputation"/> — the honest answer for "we've never given them
        /// any reason to feel differently than everyone else does," and the entire migration
        /// story for a save written before this field existed: nothing needs seeding, because
        /// an untracked faction and a fresh save's empty dictionary look identical.
        /// </summary>
        public float StandingWith(Faction faction) =>
            faction != null && standings.TryGetValue(faction, out float value) ? value : Reputation;

        /// <summary>
        /// Whether standing can mean anything for this faction at all: not the player, not
        /// hidden or defeated, a humanlike faction, not already at war with us, and one that
        /// actually has somewhere in the world to send customers from. The one predicate reused
        /// everywhere standing is written or read, so a faction that stops qualifying (a
        /// relations crash turns them hostile mid-visit, say) drops out of all of them at once.
        /// </summary>
        public bool IsEligibleFaction(Faction faction)
        {
            if (faction == null) return false;
            if (faction.IsPlayer) return false;
            if (faction.Hidden) return false;
            if (faction.defeated) return false;
            if (faction.def == null || !faction.def.humanlikeFaction) return false;
            if (faction.HostileTo(Faction.OfPlayer)) return false;
            return Find.WorldObjects.Settlements.Any(s => s.Faction == faction);
        }

        /// <summary>
        /// How hard this faction's standing should tilt the arrival draw — roughly a 20x spread
        /// floor to ceiling. Lives here rather than on the incident worker so it sits next to
        /// <see cref="MinAppealForCustomers"/> and the MTB bounds in
        /// <see cref="TryAttractCustomers"/>, this file's other arrival-shaping numbers.
        /// </summary>
        public float ArrivalWeight(Faction faction) => Mathf.Lerp(0.15f, 3f, StandingWith(faction));

        /// <summary>Tracked standings worth a player's attention — the town ledger's source.</summary>
        public IEnumerable<KeyValuePair<Faction, float>> TrackedStandings =>
            standings.Where(kv => IsEligibleFaction(kv.Key));

        /// <summary>
        /// No-op for a faction standing can't mean anything for right now (see
        /// IsEligibleFaction) — this codebase degrades silently rather than logging, and a
        /// faction going briefly ineligible mid-visit isn't a bug worth warning about.
        /// </summary>
        private void NudgeStanding(Faction customerFaction, float delta)
        {
            if (!IsEligibleFaction(customerFaction)) return;
            standings[customerFaction] = Mathf.Clamp01(StandingWith(customerFaction) + delta);
        }

        public void RecordSale(int price, bool selfService = false, Faction customerFaction = null)
        {
            revenueToday += price;
            lifetimeRevenue += price;
            customersServedToday++;
            // A staffed sale builds the town's name. An honesty-box sale slowly erodes it —
            // customers remember a town where nobody stood behind the counter.
            reputation = Mathf.Clamp01(reputation + (selfService ? -0.005f : 0.01f));
            // Nobody chose to serve THIS customer at an honesty box — there's no relationship
            // to credit, only the town-wide "nobody minds the till" fact above.
            if (!selfService) NudgeStanding(customerFaction, FactionStandingSaleDelta);
        }

        public void RecordWalkout(Faction customerFaction = null)
        {
            walkoutsToday++;
            reputation = Mathf.Clamp01(reputation - 0.02f);
            // Unlike a self-service sale, a walkout is always personal — patience ran out, or a
            // stay got cut short, and it happened to this customer specifically, whether or not
            // anyone was actually staffing the counter at the moment it did.
            NudgeStanding(customerFaction, FactionStandingWalkoutDelta);
        }

        /// <summary>A saloon boiling over costs the town more than one shrugged-off walkout —
        /// word of an actual disturbance travels further than word of slow service.</summary>
        public void RecordDisturbance()
        {
            reputation = Mathf.Clamp01(reputation - 0.05f);
        }

        /// <summary>The house winning a wager for a customer and then not being able to pay it
        /// out — the worst single-event reputation and standing hit the mod has, on purpose:
        /// reneging on a paid bet is a sharper trust break than slow service, a walkout, or even
        /// a saloon disturbance.</summary>
        public void RecordShortfall(Faction customerFaction = null)
        {
            reputation = Mathf.Clamp01(reputation - 0.08f);
            NudgeStanding(customerFaction, FactionStandingShortfallDelta);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!map.IsPlayerHome) return;

            // Roll the ledger at midnight.
            int day = GenLocalDate.DayOfYear(map);
            if (day != lastDayRolled)
            {
                if (lastDayRolled >= 0) RollOverDay();
                lastDayRolled = day;
            }

            TryAttractCustomers();
        }

        /// <summary>
        /// Word of a good town spreads: appeal directly drives how often customer groups set
        /// out, rather than leaving frequency to the storyteller's flat random roll. Firing
        /// goes through the storyteller so the incident's own minRefireDays still applies —
        /// a booming town gets frequent groups, never a flood of them.
        /// </summary>
        private void TryAttractCustomers()
        {
            if (Find.TickManager.TicksGame % ArrivalCheckInterval != 0) return;

            float appeal = Appeal;
            if (appeal < MinAppealForCustomers) return;

            // A town scraping past the threshold sees a group every few days; a booming main
            // street sees one most days. The volume setting scales the clock as well as the
            // group size, since "more customers" should mean both.
            float mtbDays = Mathf.Lerp(3.5f, 0.8f,
                Mathf.Clamp01((appeal - MinAppealForCustomers) / 3.5f));
            mtbDays /= Mathf.Max(0.25f, OldWestTownMod.Settings.customerVolume);
            if (!Rand.MTBEventOccurs(mtbDays, 60000f, ArrivalCheckInterval)) return;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                OWTDefOf.OWT_ShopCustomers.category, map);
            Find.Storyteller.TryFire(new FiringIncident(OWTDefOf.OWT_ShopCustomers, null, parms));
        }

        private void RollOverDay()
        {
            for (int i = 0; i < shops.Count; i++) shops[i]?.RollOverDay();
            revenueToday = 0;
            customersServedToday = 0;
            walkoutsToday = 0;

            // Reputation decays toward neutral so a town has to keep earning its name.
            reputation = Mathf.Lerp(reputation, 0.5f, 0.05f);

            // Standing decays toward the town's own name at the same rate — a specific
            // faction's regard drifts back to "just another stranger" if nothing keeps
            // happening between them and this town.
            List<Faction> trackedFactions = new List<Faction>(standings.Keys);
            for (int i = 0; i < trackedFactions.Count; i++)
            {
                Faction f = trackedFactions[i];
                standings[f] = Mathf.Lerp(standings[f], reputation, 0.05f);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastDayRolled, "lastDayRolled", -1);
            Scribe_Values.Look(ref revenueToday, "revenueToday");
            Scribe_Values.Look(ref customersServedToday, "customersServedToday");
            Scribe_Values.Look(ref walkoutsToday, "walkoutsToday");
            Scribe_Values.Look(ref lifetimeRevenue, "lifetimeRevenue");
            Scribe_Values.Look(ref reputation, "reputation", 0.5f);
            // An old save has no "standings" node at all; whatever Scribe does with a missing
            // collection node, the PostLoadInit guard below makes the outcome deterministic
            // either way. Every faction then simply reads as Reputation (see StandingWith)
            // until it individually earns something different — no version check needed.
            Scribe_Collections.Look(ref standings, "standings", LookMode.Reference, LookMode.Value,
                ref standingFactions, ref standingValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (standings == null)
                {
                    standings = new Dictionary<Faction, float>();
                }
                else if (standings.ContainsKey(null))
                {
                    // A reference key that failed to resolve comes back null, and a Dictionary
                    // indexer throws on a null key -- which would take out RollOverDay every
                    // midnight from then on. Standing for a faction that no longer exists means
                    // nothing anyway, so drop it here rather than guard every write site.
                    standings.Remove(null);
                }
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Comps register on spawn, but a loaded map spawns them before this component exists.
            shops.Clear();
            foreach (Thing t in map.listerThings.AllThings)
            {
                CompBusiness comp = t.TryGetComp<CompBusiness>();
                if (comp != null) Register(comp);
            }
        }
    }
}
