using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using OldWestTown.DevTools;
using OldWestTown.GoldRush;
using OldWestTown.Rivals;
using OldWestTown.Stagecoach;

namespace OldWestTown.Shops
{
    /// <summary>
    /// The town's books. Keeps the live shop register, rolls the day over, surveys what the town
    /// has to offer, and settles what people think of it — <see cref="Appeal"/> (how many
    /// customers the town pulls in) and <see cref="Reputation"/> (what they'll pay, and how many
    /// bother coming). It also keeps, per <see cref="Faction"/>, that faction's own standing with
    /// the town — the thing that decides who is likelier to visit next — settled from the same
    /// nightly verdicts the town's own name is.
    /// </summary>
    public class TownEconomy : MapComponent
    {
        /// <summary>Appeal below which no customer group will set out for this town.</summary>
        public const float MinAppealForCustomers = 0.5f;

        /// <summary>What one stock-free service (a haircut) is worth on the same scale as a shelf of
        /// goods. A service has no "quantity on the shelf" — only a base price, an order of magnitude
        /// below a shelf's total value — so it is scaled before joining the same wealth curve.
        /// Multiplies ServiceDef.basePrice, NOT a marked-up price: a barber alone offers
        /// 16 x 30 = 480, giving a goods factor of 0.69 against its kind's 1.1, so a barber shop
        /// still clears <see cref="MinAppealForCustomers"/> on its own.</summary>
        private const float ServiceOfferWeight = 30f;

        /// <summary>Market value on display at which the goods term is worth exactly 1.0. Square root
        /// above that: four times the goods for twice the draw.</summary>
        private const float GoodsFactorBasis = 1000f;

        /// <summary>Floor and ceiling on the goods term. The floor is what keeps a business that sells
        /// time rather than things from surveying at nothing; the ceiling is what stops one enormous
        /// warehouse standing in for a town.</summary>
        private const float MinGoodsFactor = 0.25f;
        private const float MaxGoodsFactor = 3f;

        /// <summary>The thinnest purse a traveller sets out with. Floored well above the goods term's
        /// own floor because a town's first customers must be able to afford its first shelf.</summary>
        private const float MinPurseFactor = 0.9f;

        /// <summary>What the n-th shop-front of a kind the town already has is worth against the one
        /// before it. Geometric, not a flat repeat discount: the flat version summed without limit, so
        /// five general stores out-earned the shipped three-kind main street — the exact rule it was
        /// written to enforce, inverted. 0.35^n converges, so a kind is worth at most 1.54x its first
        /// front however many of it you build.</summary>
        private const float RepeatFrontFactor = 0.35f;

        /// <summary>How often the town takes stock of itself, and — since the survey is what
        /// retakes them — how often every shop's shelves are re-read, and its priced totals with
        /// them. Surveying faster would only re-derive the same answer from the same shelves.</summary>
        private const int SurveyInterval = 60;

        /// <summary>How often the arrival clock is consulted. MTB math corrects for the interval.
        /// A multiple of <see cref="SurveyInterval"/>, so an arrival roll always reads a survey taken
        /// on the same tick.</summary>
        private const int ArrivalCheckInterval = 600;

        /// <summary>What a town with no history has, and what an idle town drifts back toward.</summary>
        private const float NeutralReputation = 0.5f;

        /// <summary>How far one day of trade can pull the town's name toward that day's service
        /// record, at full traffic. A fifth of the gap: a busy main street earns a strong name in a
        /// week and a moderate town over a month, and no single afternoon settles the question
        /// either way.</summary>
        private const float MaxDayWeight = 0.2f;

        /// <summary>Customers in a day that count as a full day's evidence. A thinner day still moves
        /// the number, proportionally less — one traveller giving up on a dead Tuesday is bad luck,
        /// not a scandal, and must not read as one.</summary>
        private const float FullEvidencePatrons = 6f;

        /// <summary>How far a day with no custom at all slides the town back toward neutral. Unchanged
        /// rate from when this ran every night; it now runs only on the nights it was written for.</summary>
        private const float IdleDrift = 0.05f;

        /// <summary>A served customer's nudge to their own faction's standing — five times the
        /// town-wide scale, deliberately: standing has to move fast enough over normal play for a
        /// "regular" to read as one within a reasonable number of visits.</summary>
        private const float FactionStandingSaleDelta = 0.05f;

        /// <summary>Twice the sale delta, the same 2:1 ratio the town's own verdicts use.</summary>
        private const float FactionStandingWalkoutDelta = -0.10f;

        /// <summary>The worst standing hit in the mod, deliberately worse than a walkout's —
        /// the house winning a hand for a customer and then not paying it out is a sharper
        /// trust break than slow service ever is.</summary>
        private const float FactionStandingShortfallDelta = -0.20f;

        /// <summary>Extra reputation/standing hit, scaled by ShopPricing.GougeSeverity and
        /// accumulated once per gouged customer rather than once per sale (see
        /// <see cref="PatronGouged"/>), applied only while a gold rush's boom is active — the
        /// direct, necessary companion to the demand basket. Demand swings the shop-choice score
        /// roughly 10x, structurally overpowering ValueAppeal's own ~2x price sensitivity, so the
        /// existing "customers avoid pricey shops" self-correction needs an explicit extra brake
        /// here while a rush makes price barely matter. See docs/DESIGN.md.</summary>
        private const float GougeReputationPenalty = 0.03f;
        private const float GougeStandingDelta = -0.03f;

        /// <summary>
        /// Structural cap on the arrival clock's regional-competition slowdown — see
        /// <see cref="RegionalShare"/>. <c>Mathf.Lerp</c> clamps its own interpolant to [0,1], so
        /// this is a provable bound, not a tuning promise: never faster than today, never more
        /// than 60% slower, for any rival appeal, any rivalStrength setting, any number of rivals.
        /// </summary>
        private const float MaxRegionalSlowdown = 1.6f;

        /// <summary>What a night of disturbances costs the town's name, per disturbance. Applied
        /// once at settling rather than as each brawl breaks out, so that the town's name still
        /// moves exactly once a day and from one place.</summary>
        private const float DisturbanceNightlyCost = 0.05f;

        // What befell one customer today. Flags, not an enum: the same person can be served at one
        // counter and give up at another on the same trip.
        private const int PatronServed = 1;      // somebody stood behind the counter for them
        private const int PatronSelfServed = 2;  // honesty box: goods, no welcome
        private const int PatronWalkedOut = 4;   // waited, gave up, left

        /// <summary>Sold to at a gouging markup at least once today, while a gold rush boom was
        /// active — see <see cref="RecordGouge"/>. Kept alongside the other per-patron flags so a
        /// gouged customer's own faction takes its standing hit from the same nightly settlement
        /// pass (<see cref="SettleStandings"/>) as a served or walked-out customer's, rather than
        /// from a second, sale-time write to reputation.</summary>
        private const int PatronGouged = 8;

        private readonly List<CompBusiness> shops = new List<CompBusiness>();

        private int lastDayRolled = -1;

        public int revenueToday;
        public int lifetimeRevenue;

        /// <summary>The town's name, 0..1 — its SERVICE record, not its sales record. Settled once a
        /// night from the day's callers; see <see cref="JudgeTheDay"/>. Scribed with the same label
        /// and default it always had, so an existing save's value keeps loading and keeps meaning
        /// "how well this town is thought of".</summary>
        private float reputation = 0.5f;

        /// <summary>Today's customers, one row each — an id in <see cref="patronIds"/> and what befell
        /// them in <see cref="patronFlags"/> at the same index.
        ///
        /// Ints and not Pawn references deliberately: the customer walks off the map long before the
        /// save that mentions them is loaded again, and a stale id is simply an id that never matches
        /// where a dangling reference is a load error. It also keeps this layer honest — the per-visit
        /// record the Lords layer keeps dies with the lord, and Shops may not name it.
        ///
        /// Scribed because a player who saves at three in the afternoon and reloads must not have the
        /// group already in town counted a second time, on the exact screen this commit adds to explain
        /// the mechanic. Cleared at midnight, so it is never more than a day of ints.</summary>
        private List<int> patronIds = new List<int>();
        private List<int> patronFlags = new List<int>();

        /// <summary>Severity of gouging (<see cref="ShopPricing.GougeSeverity"/>) a given today's
        /// patron was sold at, summed across however many gouged sales they suffered — at the same
        /// index as <see cref="patronIds"/>. Zero for everyone else. This is what lets
        /// <see cref="RecordGouge"/> keep the per-sale severity scaling the gouge penalty always
        /// had while still only ever writing reputation once, at <see cref="JudgeTheDay"/>: the
        /// severity is banked here through the day and spent on settlement night, the same as
        /// every other verdict in this table.</summary>
        private List<float> patronGougeSeverity = new List<float>();

        /// <summary>Who each of today's callers came with, at the same index. Kept so that a
        /// faction's standing settles from the same verdicts the town's own name does, rather than
        /// from a nudge per receipt — a customer who opened their purse six times is one satisfied
        /// customer to their faction too.</summary>
        private List<Faction> patronFactions = new List<Faction>();

        /// <summary>Disturbances anywhere in town today. Charged at settling; see JudgeTheDay.</summary>
        private int disturbancesToday;

        /// <summary>
        /// Per-faction standing, 0..1, layered beside <see cref="reputation"/> rather than
        /// replacing it. Sparse on purpose: a faction with no entry here hasn't diverged from
        /// the town's own name yet, which is also the whole migration story for a save written
        /// before this existed — see <see cref="StandingWith"/>.
        /// </summary>
        private List<Faction> standingFactions = new List<Faction>();
        private List<float> standingValues = new List<float>();
        private Dictionary<Faction, float> standings = new Dictionary<Faction, float>();

        /// <summary>
        /// Tick of the last successful customer arrival, organic or guaranteed — the clock the
        /// stagecoach line's ceiling counts against. See <see cref="TicksSinceLastArrival"/>.
        /// </summary>
        private int lastArrivalTick;

        /// <summary>The tier <see cref="CheckRouteTierChange"/> last announced, so a reload
        /// can't re-announce a tier the player has already been told about.</summary>
        private CoachTierDef lastAnnouncedTier;

        /// <summary>Whether this map's own town was leading the region the last time
        /// <see cref="CheckRegionalLeadChange"/> actually evaluated it.</summary>
        private bool lastRegionLead = true;

        /// <summary>False until <see cref="CheckRegionalLeadChange"/> has evaluated the lead at
        /// least once on this map. Kept separate from <see cref="lastRegionLead"/> itself so the
        /// very first evaluation — a fresh colony crossing the threshold, or an old save loading
        /// under this version for the first time — can silently record state rather than
        /// announcing a spurious change.</summary>
        private bool regionLeadKnown = false;

        public TownEconomy(Map map) : base(map) { }

        public float Reputation => Mathf.Clamp01(reputation);

        /// <summary>Dev Mode lever: writes reputation directly, bypassing the nightly verdict
        /// that ordinarily settles it. Unlocks route-tier promotion, gold-rush bust recovery and
        /// a regional-lead flip from one slider rather than three narrow ones — see
        /// DevTools/DebugActions.cs.</summary>
        internal void DebugSetReputation(float pct01) => reputation = Mathf.Clamp01(pct01);

        /// <summary>Distinct people who did business today, or tried to and gave up.</summary>
        public int PatronsToday => patronIds.Count;

        /// <summary>Distinct people today who gave up at a counter — the number the player can act on.
        /// One person who gave up at three counters is one disappointed customer.</summary>
        public int UnservedToday
        {
            get
            {
                int n = 0;
                for (int i = 0; i < patronFlags.Count; i++)
                {
                    if ((patronFlags[i] & PatronWalkedOut) != 0) n++;
                }
                return n;
            }
        }

        /// <summary>How well the town has treated today's callers, 0..1 — the value tonight's roll
        /// pulls <see cref="Reputation"/> toward. Meaningless until somebody has actually come to a
        /// counter; ask <see cref="PatronsToday"/> first. Read by the ledger so the player can watch
        /// the verdict form while there is still time to change it, which is the whole reason it
        /// settles at midnight rather than sale by sale.</summary>
        public float ServiceScoreToday
        {
            get
            {
                if (patronFlags.Count == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < patronFlags.Count; i++) sum += Verdict(patronFlags[i]);
                return sum / patronFlags.Count;
            }
        }

        /// <summary>
        /// Unweighted mean of every open, stocked shop's own <see cref="ShopPricing.ValueAppeal"/>
        /// — the identical score a customer already uses to pick between your own shops, now
        /// averaged into one town-wide number. Shares <see cref="Appeal"/>'s exact gate
        /// (<c>HasAnythingToOffer</c>), so the two can never disagree about whether there's
        /// anything here worth pricing at all; 1f (neutral) when there isn't.
        /// </summary>
        public float PriceIndex
        {
            get
            {
                float total = 0f;
                int count = 0;
                foreach (CompBusiness shop in OpenShops())
                {
                    if (!shop.HasAnythingToOffer) continue;
                    total += ShopPricing.ValueAppeal(shop);
                    count++;
                }
                return count > 0 ? total / count : 1f;
            }
        }

        /// <summary>What this town actually pulls: <see cref="Appeal"/> weighted by how
        /// competitively it's priced. The player-side half of <see cref="RegionalShare"/>.</summary>
        public float MarketPull => Appeal * PriceIndex;

        /// <summary>
        /// Every rival's combined pull, at the player's own rivalStrength setting. 0f with rivals
        /// disabled, or no world component to read — defensive, degrading silently the way this
        /// file always has. The floor is 0f, not the 0.25f the volume/wealth sliders use:
        /// rivalStrength is a multiplier on a sum here, not a divisor, so there's no near-zero
        /// denominator to guard against — 0f only stops a corrupted negative setting from ever
        /// making this negative, which would break <see cref="RegionalShare"/>'s arithmetic.
        /// </summary>
        private float CompetingPull
        {
            get
            {
                if (!OldWestTownMod.Settings.rivalTownsEnabled) return 0f;
                RivalTowns rivalsComp = Find.World?.GetComponent<RivalTowns>();
                if (rivalsComp == null) return 0f;
                return rivalsComp.TotalRivalPull * Mathf.Max(0f, OldWestTownMod.Settings.rivalStrength);
            }
        }

        /// <summary>
        /// This town's share of regional trade: this town's own <see cref="MarketPull"/> against
        /// the combined pull of every rival. Exactly 1f — "as good as no competition exists" —
        /// whenever there is nothing to compare against: rivals disabled, no rival has grown past
        /// zero yet, or this town has no appeal yet. Otherwise a proper fraction strictly less
        /// than 1f. Feeds the arrival clock's slowdown in <see cref="TryAttractCustomers"/>, and
        /// the ledger and inspect-pane display.
        /// </summary>
        public float RegionalShare
        {
            get
            {
                float pull = MarketPull;
                float competing = CompetingPull;
                if (pull <= 0f || competing <= 0f) return 1f;
                return pull / (pull + competing);
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

        private void NudgeStanding(Faction customerFaction, float delta)
        {
            if (!IsEligibleFaction(customerFaction)) return;
            standings[customerFaction] = Mathf.Clamp01(StandingWith(customerFaction) + delta);
        }

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

        // ------------------------------------------------------------------ appeal

        /// <summary>The town's last survey of itself — what its businesses are worth as businesses,
        /// and the market value it has on offer. Recorded by <see cref="TakeStock"/>.
        ///
        /// Settled on the town's own clock rather than computed on demand, for the same reason
        /// reputation settles at midnight: walking every sales floor and pricing every stack is
        /// something the TOWN does, at a defined moment, not something a UI panel does incidentally
        /// while it draws. On demand it meant a selected counter re-surveyed the town every rendered
        /// frame — and, through the service scan's tie-break roll, advanced the shared seeded game
        /// stream while it did, so whether the player had a counter selected changed what the
        /// storyteller rolled next.
        ///
        /// Not scribed. It is derived from things that are, FinalizeInit takes the first survey
        /// before anything can read it, and a field that cannot be stale is not a field that has to
        /// be invalidated.</summary>
        private float businessScore;
        private float offerValue;

        // One row per credited shop-front: its kind, the counter, and the floor it stands on. Members
        // rather than locals so a survey does not allocate its tables afresh every pass (the walk
        // itself still allocates its iterators); frontShops, frontFloors and countedStacks are cleared at the end
        // of every survey because a Room is rebuilt when a wall changes and a Thing can burn —
        // neither is meaningful outside the pass that read it. frontKinds holds Defs, which the
        // ledger may read until the next survey.
        private readonly List<ShopKindDef> frontKinds = new List<ShopKindDef>();
        private readonly List<CompBusiness> frontShops = new List<CompBusiness>();
        private readonly List<Room> frontFloors = new List<Room>();
        private readonly HashSet<Thing> countedStacks = new HashSet<Thing>();

        /// <summary>What the town's businesses are worth as businesses, before anything on their
        /// shelves is counted: the sum of each shop-front's kind appeal, discounted for repeats.</summary>
        public float BusinessScore => businessScore;

        /// <summary>Market value of everything the town has on offer, each stack counted once.</summary>
        public float OfferValue => offerValue;

        /// <summary>Diminishing returns on wealth: four times the goods for twice the draw.</summary>
        public float GoodsFactor =>
            Mathf.Clamp(Mathf.Sqrt(offerValue / GoodsFactorBasis), MinGoodsFactor, MaxGoodsFactor);

        /// <summary>A good name draws half again as much custom; a bad one half as much.</summary>
        public float StandingFactor => Mathf.Lerp(0.5f, 1.5f, Reputation);

        /// <summary>How rich the travellers a town like this attracts are. Reads the goods on offer
        /// and nothing else — not the town's breadth, not its name, and at market value rather than
        /// shelf price. Stock a rack of rifles and you do not get more customers, you get customers
        /// who can afford a rifle; raising the markup slider gets you neither.</summary>
        public float PurseFactor => Mathf.Max(MinPurseFactor, GoodsFactor);

        /// <summary>Does the town run this trade at all? True while any credited shop-front of that
        /// kind is open with something to offer. The ledger's one genuinely actionable line.</summary>
        public bool HasTrade(ShopKindDef kind) => kind != null && frontKinds.Contains(kind);

        /// <summary>How much trade the town attracts: 0 for a town with nothing to sell, about 5 for
        /// a stocked three-kind main street. Decides how OFTEN customer groups set out and how large
        /// they are; how much silver they carry is <see cref="PurseFactor"/>, which is a different
        /// question.
        ///
        /// Three terms multiplied, which is exactly how the ledger shows it: what the town IS
        /// (businesses), what it HAS OUT (goods), and what it is THOUGHT OF (standing). The first
        /// two come from the last survey; standing is live, so a night that moves the town's name
        /// moves appeal with it and the ledger's product always multiplies out.</summary>
        public float Appeal
        {
            get
            {
                if (businessScore <= 0f) return 0f;
                return businessScore * GoodsFactor * StandingFactor;
            }
        }

        /// <summary>Walks the town and records what a traveller would find. Three rules decide what
        /// counts, and each is a decision about what appeal measures:
        ///
        /// - A sales floor is a shop; a counter is not. A second counter of the same kind in the same
        ///   room is a second till — what it buys the player is serving two customers at once, which
        ///   is its own reward — and it adds nothing to the businesses term.
        /// - Every stack on sale counts once, however many counters can see it. A shared room used to
        ///   be added once per counter, and two stalls with overlapping ground still would be.
        /// - Goods count at market value, not at the shelf price. Appeal is what the town OFFERS; the
        ///   markup slider decides what it asks. Pricing this through ShopPricing meant dragging that
        ///   slider up drew more and richer customers for free, on top of charging them more.</summary>
        private void TakeStock()
        {
            float business = 0f;
            float offer = 0f;
            frontKinds.Clear();
            frontShops.Clear();
            frontFloors.Clear();
            countedStacks.Clear();

            // One snapshot of the shelves per survey, taken here so that nothing else — least of all
            // drawing an inspect pane — decides when it happens. Closed shops are refreshed too:
            // they are out of the running for appeal, but their Stock tab still has to tell the
            // truth while the player decides what to put on them.
            for (int i = 0; i < shops.Count; i++)
            {
                CompBusiness shop = shops[i];
                if (shop?.parent != null && shop.parent.Spawned) shop.RefreshStock();
            }

            foreach (CompBusiness shop in OpenShops())
            {
                if (!shop.HasAnythingToOffer) continue;

                // A null floor is an open-air stall, which is a floor of its own: two stalls in the
                // same outdoor "room" are two shop-fronts, and whatever ground they share is settled
                // by countedStacks below rather than by pretending they are one shop.
                Room floor = shop.SalesFloorRoom;
                int repeats = 0;
                bool secondTill = false;
                // Parallel lists and a linear scan, like the patron table above — a main street is a
                // handful of counters, and this stays a table small enough to read.
                for (int i = 0; i < frontKinds.Count; i++)
                {
                    if (frontKinds[i] != shop.Kind) continue;
                    if (SameSalesFloor(frontShops[i], frontFloors[i], shop, floor)) { secondTill = true; break; }
                    repeats++;
                }

                // Before the second-till test, not after: a second till earns its kind nothing, but
                // its stock filter may admit goods the first counter's does not, and those really are
                // on sale in this town whichever till rings them up.
                List<Thing> stock = shop.StockOnDisplay;
                for (int i = 0; i < stock.Count; i++)
                {
                    Thing t = stock[i];
                    if (countedStacks.Add(t)) offer += ShopPricing.UnitValue(t) * t.stackCount;
                }
                if (secondTill) continue;

                frontKinds.Add(shop.Kind);
                frontShops.Add(shop);
                frontFloors.Add(floor);
                business += (shop.Kind?.appeal ?? 1f) * Mathf.Pow(RepeatFrontFactor, repeats);

                // Only services with no Thing behind them at all. A drink or a meal is already
                // counted as the stack it would consume; counting it again here would sell the same
                // beer twice. A stock-free service is available whenever its kind offers it, so this
                // asks the kind directly rather than running the availability scan and throwing the
                // answer away.
                List<ServiceDef> services = shop.Kind?.services;
                for (int i = 0; services != null && i < services.Count; i++)
                {
                    ServiceDef sd = services[i];
                    if (sd?.worker == null || sd.worker.ConsumesStock) continue;
                    // Discounted by the same repeat factor the front was, or depth in a service
                    // trade would buy through the goods term exactly what the businesses term
                    // refuses it: eight barber chairs and no stock would otherwise out-draw a
                    // stocked street with three trades on it.
                    offer += sd.basePrice * ServiceOfferWeight * Mathf.Pow(RepeatFrontFactor, repeats);
                }
            }

            businessScore = business;
            offerValue = offer;

            // Rooms and Things mean nothing outside the survey that read them: a wall knocked through
            // rebuilds every Room, and a stack can burn between passes.
            frontShops.Clear();
            frontFloors.Clear();
            countedStacks.Clear();
        }

        /// <summary>Whether two counters of one kind trade off the same ground, and so are one
        /// shop-front with two tills rather than two businesses.
        ///
        /// Indoors that is simply the same room. Outdoors there is no room to compare — every stall
        /// answers null — so it is whether their sales floors are the same patch of ground. Without
        /// that second half the player is paid to knock the walls down: the same two counters beside
        /// the same pile of goods would count as one business indoors and two in the open.</summary>
        private static bool SameSalesFloor(CompBusiness a, Room aFloor, CompBusiness b, Room bFloor)
        {
            if (aFloor != null || bFloor != null) return aFloor == bFloor;
            if (a?.parent == null || b?.parent == null || a.parent.Map != b.parent.Map) return false;
            float reach = Mathf.Max(a.Props.openAirRadius, b.Props.openAirRadius);
            return a.parent.Position.DistanceTo(b.parent.Position) <= reach;
        }

        /// <summary>The town's own people are not its customers. Nothing that happens to a colonist
        /// at a counter belongs in the day's books: reputation is a verdict per person per day, so
        /// one colonist filed as a patron would let a player move the town's name — and with it its
        /// prices and its arrivals — using pawns they control, and the takings would count silver
        /// the colony paid itself. Asked at every door into the books rather than at each caller, so
        /// it stays true of the next path into a business as well as today's.</summary>
        private static bool IsOwnColonist(Pawn customer) => customer?.Faction == Faction.OfPlayer;

        public void RecordSale(Pawn customer, int price, bool selfService = false)
        {
            if (IsOwnColonist(customer)) return;
            revenueToday += price;
            lifetimeRevenue += price;
            NotePatron(customer, selfService ? PatronSelfServed : PatronServed);
        }

        /// <summary>A saloon boiling over costs the town more than one shrugged-off walkout —
        /// word of an actual disturbance travels further than word of slow service. Filed for
        /// tonight rather than charged now, because the town's name moves once a day, from
        /// <see cref="JudgeTheDay"/> and nowhere else.</summary>
        public void RecordDisturbance()
        {
            disturbancesToday++;
        }

        public void RecordWalkout(Pawn customer)
        {
            if (IsOwnColonist(customer)) return;
            NotePatron(customer, PatronWalkedOut);
        }

        /// <summary>Selling to this customer above this shop's own kind's usual markup, while a
        /// gold rush's boom is active — the direct, necessary companion to the demand basket; see
        /// <see cref="GougeReputationPenalty"/>. Filed against the customer for tonight's
        /// settlement exactly like a sale or a walkout, rather than writing reputation
        /// immediately: the old per-sale write moved the town's name from a second place, the
        /// exact thing settling it once a day at <see cref="JudgeTheDay"/> exists to prevent.
        /// The warning message is the one part of this that still happens at the counter, not at
        /// midnight — it is a per-shop notice, throttled by <see cref="CompBusiness.TryClaimGougeMessage"/>,
        /// not a reputation write, so nothing about settling once a day requires delaying it
        /// too.</summary>
        public void RecordGouge(Pawn customer, CompBusiness shop)
        {
            if (shop == null || !GoldRushUtility.BoomActive(map)) return;

            float severity = ShopPricing.GougeSeverity(shop);
            if (severity <= 0f) return;

            NotePatron(customer, PatronGouged, severity);

            if (shop.TryClaimGougeMessage())
            {
                Messages.Message("OWT_GoldRushGougeWarning".Translate(shop.parent.Label),
                    new LookTargets(shop.parent), MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>Files today's outcome against the person it happened to, not against the till.
        /// The table is one day of customers — a linear scan is cheaper than a dictionary at that
        /// size and saves as two plain int lists. <paramref name="gougeSeverity"/> adds onto
        /// whatever this patron already banked today rather than overwriting it, so a customer
        /// gouged at two different counters (or twice at one) is charged for both — see
        /// <see cref="patronGougeSeverity"/>.</summary>
        private void NotePatron(Pawn customer, int flag, float gougeSeverity = 0f)
        {
            if (customer == null) return;

            if (IsOwnColonist(customer)) return;

            int id = customer.thingIDNumber;
            for (int i = 0; i < patronIds.Count; i++)
            {
                if (patronIds[i] != id) continue;
                patronFlags[i] |= flag;
                patronGougeSeverity[i] += gougeSeverity;
                return;
            }
            patronIds.Add(id);
            patronFlags.Add(flag);
            patronFactions.Add(customer.Faction);
            patronGougeSeverity.Add(gougeSeverity);
        }

        /// <summary>What one customer thinks of the town at the end of their day, 0..1.
        ///
        /// One rule, applied twice: a customer nobody looked after thinks half as well of the town.
        /// Goods off an unwatched shelf are half of being served, and giving up at any counter halves
        /// whatever else the day was worth. Volume is deliberately absent — a customer who opened
        /// their purse six times is one satisfied customer, not six, and counting the six is what
        /// pinned this number at its ceiling on the first trading day.</summary>
        private static float Verdict(int flags)
        {
            float v = (flags & PatronServed) != 0 ? 1f
                    : (flags & PatronSelfServed) != 0 ? 0.5f
                    : 0f;
            if ((flags & PatronWalkedOut) != 0) v *= 0.5f;
            return v;
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

            // Ahead of the IsPlayerHome gate: a counter on any map shows the town line, and
            // surveying a map with no businesses is a walk over an empty list.
            if (Find.TickManager.TicksGame % SurveyInterval == 0) TakeStock();

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
        /// This town's current rung on the stagecoach route ladder, or null if there's no coach
        /// depot on the map, or appeal hasn't reached even the lowest tier. Recomputed from
        /// current <see cref="Appeal"/> on every read, exactly like Appeal itself is recomputed
        /// from current stock — not cached, and never ratcheting, so a town whose reputation
        /// slides can watch its own tier demote, a legible consequence with a name, on top of
        /// the arrival clock's own quiet slowdown.
        /// </summary>
        public CoachTierDef RouteTier => CoachTierUtility.CurrentTier(map, Appeal);

        /// <summary>
        /// Ticks since the last successful customer arrival of any kind, organic or guaranteed.
        /// A save from before this field existed reads it as the entire elapsed game time, which
        /// is the same "safe, a little eager, never stranded" shape LordJob_ShopVisit's own
        /// groupArrivedTick already ships with — the very next arrival re-anchors the clock.
        /// </summary>
        public int TicksSinceLastArrival => Find.TickManager.TicksGame - lastArrivalTick;

        /// <summary>
        /// True once the active tier's own ceiling has elapsed with no arrival of any kind. This
        /// is the OR <see cref="TryAttractCustomers"/> adds to its existing MTB roll below — it
        /// can only ever add a firing attempt where the roll would otherwise stay quiet past the
        /// ceiling, never suppress or duplicate one, and either path fires the identical
        /// OWT_ShopCustomers incident, so the incident's own minRefireDays stays a hard cap on
        /// the combined rate regardless of which condition actually triggered a given firing.
        /// </summary>
        public bool GuaranteedArrivalDue
        {
            get
            {
                CoachTierDef tier = RouteTier;
                return tier != null && TicksSinceLastArrival >= CoachTierUtility.CeilingTicks(tier);
            }
        }

        /// <summary>Called for every successful customer arrival, organic or guaranteed, so the
        /// guarantee clock reflects reality no matter which condition actually fired it.</summary>
        public void NotifyArrival() => lastArrivalTick = Find.TickManager.TicksGame;

        /// <summary>Dev Mode lever: expires the guarantee clock so the very next arrival reads
        /// <see cref="GuaranteedArrivalDue"/> as true. Relative to the current tick rather than a
        /// flat sentinel — at the customerVolume floor of 0.25 the largest tier ceiling is
        /// 1,920,000 ticks, and a fixed negative constant is only guaranteed sufficient once
        /// TicksGame has already passed roughly that many ticks itself, a real gap very early in
        /// a session that computing this relative to "now" closes at zero extra cost.</summary>
        internal void DebugExpireArrivalClock() => lastArrivalTick = Find.TickManager.TicksGame - 999_000_000;

        /// <summary>Dev Mode lever: rolls the day over on demand — the same RollOverDay a real
        /// midnight calls. Doesn't touch lastDayRolled: the day-of-year gate in MapComponentTick
        /// changes at the next real midnight regardless of what this writes mid-day, so there is
        /// nothing here that could make that gate double-fire.</summary>
        internal void DebugForceSettlement() => RollOverDay();

        /// <summary>
        /// Announces a change in <see cref="RouteTier"/> the moment <see cref="TryAttractCustomers"/>
        /// notices one: a promotion gets a letter, a demotion or an outright loss of the route
        /// gets a quieter message. Checked ahead of the MinAppealForCustomers early-out below so
        /// a route lost outright — appeal falling under even the lowest tier — is still
        /// announced rather than silently dropped.
        /// </summary>
        private void CheckRouteTierChange()
        {
            CoachTierDef current = RouteTier;
            if (current == lastAnnouncedTier) return;

            bool promoted = current != null
                && (lastAnnouncedTier == null || current.minAppeal > lastAnnouncedTier.minAppeal);

            if (promoted)
            {
                Find.LetterStack.ReceiveLetter(
                    "OWT_RouteTierUpLabel".Translate(),
                    "OWT_RouteTierUpText".Translate(current.LabelCap),
                    LetterDefOf.PositiveEvent);
            }
            else if (current != null)
            {
                Messages.Message(
                    "OWT_RouteTierDownMessage".Translate(current.LabelCap),
                    MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("OWT_RouteTierLostMessage".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            lastAnnouncedTier = current;
        }

        /// <summary>
        /// Announces a change in who's ahead regionally — this town's own <see cref="MarketPull"/>
        /// against every rival's combined pull — the moment <see cref="RollOverDay"/> notices
        /// one. Silent below <see cref="MinAppealForCustomers"/>, or with no qualifying rival to
        /// be ahead of at all (both mirror <see cref="RegionalShare"/>'s own guard), and silent on
        /// the very first evaluation on a given map — a fresh colony crossing the threshold, or
        /// an old save loading under this version for the first time — so the feature turning on
        /// can never itself read as "you've fallen behind." A Message, not a Letter: unlike a
        /// route-tier promotion (rare, close to monotonic), this can flip more than once across a
        /// single undercut swing near parity, and a Letter for a potentially flip-floppy signal
        /// would be disproportionate.
        /// </summary>
        private void CheckRegionalLeadChange()
        {
            if (Appeal < MinAppealForCustomers || CompetingPull <= 0f) return;

            bool leading = MarketPull >= CompetingPull;
            if (!regionLeadKnown)
            {
                regionLeadKnown = true;
                lastRegionLead = leading;
                return;
            }
            if (leading == lastRegionLead) return;

            lastRegionLead = leading;
            if (leading)
            {
                Messages.Message("OWT_RegionalLeadGainedMessage".Translate(), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("OWT_RegionalLeadLostMessage".Translate(), MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// Word of a good town spreads: appeal directly drives how often customer groups set
        /// out, rather than leaving frequency to the storyteller's flat random roll. Firing
        /// goes through the storyteller so the incident's own minRefireDays still applies —
        /// a booming town gets frequent groups, never a flood of them.
        ///
        /// A coach depot layers a guarantee on top, not a second clock: once the active route
        /// tier's own ceiling has elapsed with no arrival at all, GuaranteedArrivalDue forces an
        /// attempt through the exact same OR below, so the ceiling can only ever fire the
        /// identical incident the MTB roll already fires — never a second, independent one with
        /// its own cooldown. See docs/DESIGN.md for the reasoning and the worked-out numbers.
        /// </summary>
        private void TryAttractCustomers()
        {
            if (Find.TickManager.TicksGame % ArrivalCheckInterval != 0) return;

            float appeal = Appeal;
            CheckRouteTierChange();
            if (appeal < MinAppealForCustomers) return;

            // A town scraping past the threshold sees a group every few days; a booming main
            // street sees one most days. The volume setting scales the clock as well as the
            // group size, since "more customers" should mean both.
            float mtbDays = Mathf.Lerp(3.5f, 0.8f,
                Mathf.Clamp01((appeal - MinAppealForCustomers) / 3.5f));
            mtbDays /= Mathf.Max(0.25f, OldWestTownMod.Settings.customerVolume);

            // A gold rush's own boom/bust multiplier (1f, a no-op, whenever no rush is active)
            // rides on top of the same MTB roll rather than touching GuaranteedArrivalDue below —
            // the coach line's ceiling stays exactly what it always promises, so a rush can only
            // ever speed up or slow down the organic clock the ceiling is a backstop for, never
            // compound with it. See docs/DESIGN.md#gold-rush-one-condition-not-two-clocks.
            mtbDays *= GoldRushUtility.ArrivalMtbMultiplier(map);
            // Regional competition stretches the gap, never shrinks it: RegionalShare is 1f
            // (Lerp's own t=0) whenever there's no qualifying rival, so this is a no-op until one
            // exists. See RegionalShare and MaxRegionalSlowdown for the provable [1.0x, 1.6x] cap.
            mtbDays *= Mathf.Lerp(1f, MaxRegionalSlowdown, 1f - RegionalShare);
            if (!Rand.MTBEventOccurs(mtbDays, 60000f, ArrivalCheckInterval) && !GuaranteedArrivalDue) return;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                OWTDefOf.OWT_ShopCustomers.category, map);
            Find.Storyteller.TryFire(new FiringIncident(OWTDefOf.OWT_ShopCustomers, null, parms));
        }

        private void RollOverDay()
        {
            for (int i = 0; i < shops.Count; i++) shops[i]?.RollOverDay();

            // Judge the day before its evidence is swept up. Captured ahead of JudgeTheDay so the
            // telemetry line below can report how far it moved, not just where it landed.
            float reputationBefore = reputation;
            JudgeTheDay();
            Telemetry.LogSettlement(map, PatronsToday, UnservedToday, ServiceScoreToday, reputationBefore, reputation);

            revenueToday = 0;
            disturbancesToday = 0;
            patronIds.Clear();
            patronFlags.Clear();
            patronFactions.Clear();
            patronGougeSeverity.Clear();

            // Standing decays toward the town's own name at the same rate — a specific faction's
            // regard drifts back to "just another stranger" if nothing keeps happening between
            // them and this town.
            List<Faction> tracked = new List<Faction>(standings.Keys);
            for (int i = 0; i < tracked.Count; i++)
            {
                standings[tracked[i]] = Mathf.Lerp(standings[tracked[i]], reputation, 0.05f);
            }
        }

        /// <summary>Turns a day of outcomes into one move on the town's name, at midnight.
        ///
        /// Nightly rather than per-event so the number is a reputation and not a scoreboard: nothing
        /// one customer does can swing it, and nothing the player does is invisible either — the
        /// ledger shows the day's record forming as it happens.</summary>
        private void JudgeTheDay()
        {
            // Before the early return below: a brawl on a day when nobody reached a counter is
            // still a brawl, and the counter is cleared either way when the day rolls. Gouging
            // can never happen without a sale, so it can never actually fire before there is a
            // patron on the books — but it is charged alongside disturbances, for the same
            // reason: both are town-wide costs settled once, independent of the per-patron
            // service score below.
            ChargeDisturbances();
            ChargeGouging();

            if (patronFlags.Count == 0)
            {
                // Nobody came to a counter, so nobody has anything to say. A town no one trades with
                // is forgotten, not hated — and this branch is also what keeps a ruined name from
                // being a trap: bad standing thins the crowd, a thin crowd stops producing walkouts,
                // and the town drifts back toward having no name rather than sitting at the bottom.
                reputation = Mathf.Lerp(reputation, NeutralReputation, IdleDrift);
                return;
            }

            float weight = MaxDayWeight * Mathf.Clamp01(patronFlags.Count / FullEvidencePatrons);
            reputation = Mathf.Lerp(reputation, ServiceScoreToday, weight);
            SettleStandings();
        }

        /// <summary>Trouble in town is charged once, tonight, however many brawls there were.</summary>
        private void ChargeDisturbances()
        {
            if (disturbancesToday <= 0) return;
            reputation = Mathf.Clamp01(reputation - DisturbanceNightlyCost * disturbancesToday);
        }

        /// <summary>Gouging during today's gold rush is charged once, tonight — the settlement-time
        /// equivalent of the penalty this used to apply at each sale. Same GougeReputationPenalty,
        /// same per-sale ShopPricing.GougeSeverity inputs; just summed across the whole day's
        /// gouged patrons (<see cref="patronGougeSeverity"/>) instead of written to reputation one
        /// sale at a time, so the town's name still moves exactly once a day and from one place.
        /// Faction standing takes the matching per-patron hit in <see cref="SettleStandings"/>.</summary>
        private void ChargeGouging()
        {
            float total = 0f;
            for (int i = 0; i < patronGougeSeverity.Count; i++) total += patronGougeSeverity[i];
            if (total <= 0f) return;
            reputation = Mathf.Clamp01(reputation - GougeReputationPenalty * total);
        }

        /// <summary>Moves each visiting faction's own standing from the same table the town's name
        /// was just settled from — one verdict per person per day, not one per receipt. Nudging on
        /// every sale would put a faction's regard on the granularity the town's own name was taken
        /// off: a group of four buying a few things each would cross the whole range in an
        /// afternoon, and every faction would read as a regular by the end of the first visit.
        ///
        /// An honesty-box sale earns nothing here, deliberately: nobody chose to serve that
        /// customer, so there is no relationship to credit — only the half-verdict the town's own
        /// name already took for it.
        ///
        /// A gouged patron's own faction takes its hit here too, scaled by exactly the severity
        /// that patron was sold at (<see cref="patronGougeSeverity"/>) — the same per-sale scaling
        /// the town-wide charge in <see cref="ChargeGouging"/> uses, just kept per-faction rather
        /// than summed town-wide.</summary>
        private void SettleStandings()
        {
            // Bounded by all three columns: they are written together, and the load path pads a
            // save from before the faction or gouge-severity columns existed, but a loop that
            // trusts one length to index another is one bad save away from throwing every
            // midnight.
            int rows = Mathf.Min(patronFactions.Count, patronFlags.Count);
            rows = Mathf.Min(rows, patronGougeSeverity.Count);
            for (int i = 0; i < rows; i++)
            {
                Faction faction = patronFactions[i];
                if (!IsEligibleFaction(faction)) continue;

                int flags = patronFlags[i];
                if ((flags & PatronServed) != 0) NudgeStanding(faction, FactionStandingSaleDelta);
                if ((flags & PatronWalkedOut) != 0) NudgeStanding(faction, FactionStandingWalkoutDelta);
                if ((flags & PatronGouged) != 0)
                {
                    NudgeStanding(faction, GougeStandingDelta * patronGougeSeverity[i]);
                }
            }

            CheckRegionalLeadChange();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastDayRolled, "lastDayRolled", -1);
            Scribe_Values.Look(ref revenueToday, "revenueToday");
            Scribe_Values.Look(ref lifetimeRevenue, "lifetimeRevenue");
            Scribe_Values.Look(ref reputation, "reputation", 0.5f);
            Scribe_Collections.Look(ref patronIds, "patronIds", LookMode.Value);
            Scribe_Collections.Look(ref patronFlags, "patronFlags", LookMode.Value);
            Scribe_Collections.Look(ref patronFactions, "patronFactions", LookMode.Reference);
            Scribe_Collections.Look(ref patronGougeSeverity, "patronGougeSeverity", LookMode.Value);
            Scribe_Values.Look(ref disturbancesToday, "disturbancesToday");
            Scribe_Collections.Look(ref standings, "standings", LookMode.Reference, LookMode.Value,
                ref standingFactions, ref standingValues);
            // Absent on any save from before the stagecoach line existed: lastArrivalTick reads
            // as 0 (see TicksSinceLastArrival), and lastAnnouncedTier reads as null, which is
            // indistinguishable from "no depot has ever changed tier" — so an old save can never
            // spuriously re-announce a tier on its first load with this feature.
            Scribe_Values.Look(ref lastArrivalTick, "lastArrivalTick");
            Scribe_Defs.Look(ref lastAnnouncedTier, "lastAnnouncedTier");
            // Absent on any save from before rival towns existed: regionLeadKnown reads false,
            // so CheckRegionalLeadChange's first call on that map silently records the current
            // lead rather than announcing one — an upgraded save can never itself produce a
            // spurious "you've fallen behind" message.
            Scribe_Values.Look(ref lastRegionLead, "lastRegionLead", true);
            Scribe_Values.Look(ref regionLeadKnown, "regionLeadKnown", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // An absent patron table means a save written before reputation was a service
                // record. Under the old per-receipt rule fifty sales crossed the whole range, so
                // any such town is sitting at or near the ceiling whether or not it earned it —
                // and the ceiling is now worth a tenth on every price. Start those towns from no
                // opinion rather than handing them a raise they never earned; a week of trading
                // settles it honestly. A town that had NOT pinned keeps whatever it had.
                bool preServiceSave = patronIds == null;
                if (patronIds == null) patronIds = new List<int>();
                if (patronFlags == null) patronFlags = new List<int>();
                if (patronFactions == null) patronFactions = new List<Faction>();
                if (patronGougeSeverity == null) patronGougeSeverity = new List<float>();

                if (standings == null)
                {
                    standings = new Dictionary<Faction, float>();
                }
                else if (standings.ContainsKey(null))
                {
                    // A reference key that failed to resolve comes back null, and a Dictionary
                    // indexer throws on a null key -- which would take out the nightly settling
                    // from then on. Standing for a faction that no longer exists means nothing
                    // anyway, so drop it here rather than guard every write site.
                    standings.Remove(null);
                }

                // The faction column is additive, so a save from before it existed has the other
                // two and not this one. Pad rather than drop the day: an unknown faction simply
                // earns nobody anything tonight.
                while (patronFactions.Count < patronIds.Count) patronFactions.Add(null);
                // Same story for the gouge-severity column: a save from before the gold rush
                // settled its penalty this way just pads in zeroes, which is exactly "nobody
                // gouged this patron yet" — the honest answer for a day this column never saw.
                while (patronGougeSeverity.Count < patronIds.Count) patronGougeSeverity.Add(0f);
                if (preServiceSave && reputation > 0.9f) reputation = NeutralReputation;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Scribe's post-load pass is where the day's table is normally restored. Belt and
            // braces here because the cost of being wrong about that is a null reference on the
            // first customer of the day rather than a number that reads oddly.
            if (patronIds == null) patronIds = new List<int>();
            if (patronFlags == null) patronFlags = new List<int>();
            if (patronGougeSeverity == null) patronGougeSeverity = new List<float>();

            // Comps register on spawn, but a loaded map spawns them before this component exists.
            shops.Clear();
            foreach (Thing t in map.listerThings.AllThings)
            {
                CompBusiness comp = t.TryGetComp<CompBusiness>();
                if (comp != null) Register(comp);
            }

            // First survey before anything can read appeal: the game is paused on load, so a player
            // who clicks a counter before unpausing must not be shown a town worth nothing.
            TakeStock();
        }
    }
}
