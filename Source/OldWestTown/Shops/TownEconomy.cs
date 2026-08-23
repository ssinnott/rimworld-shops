using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// The town's books. Keeps the live shop register, rolls the day over, surveys what the town
    /// has to offer, and settles what people think of it — <see cref="Appeal"/> (how many
    /// customers the town pulls in) and <see cref="Reputation"/> (what they'll pay, and how many
    /// bother coming).
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

        /// <summary>How often the town takes stock of itself. No faster than the per-shop shelf scan
        /// it reads (CompBusiness.StockCacheTicks, also 60): surveying more often just re-derives the
        /// same answer from the same cached shelves.</summary>
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

        // What befell one customer today. Flags, not an enum: the same person can be served at one
        // counter and give up at another on the same trip.
        private const int PatronServed = 1;      // somebody stood behind the counter for them
        private const int PatronSelfServed = 2;  // honesty box: goods, no welcome
        private const int PatronWalkedOut = 4;   // waited, gave up, left

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

        public TownEconomy(Map map) : base(map) { }

        public float Reputation => Mathf.Clamp01(reputation);

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

        public void RecordSale(Pawn customer, int price, bool selfService = false)
        {
            revenueToday += price;
            lifetimeRevenue += price;
            NotePatron(customer, selfService ? PatronSelfServed : PatronServed);
        }

        public void RecordWalkout(Pawn customer)
        {
            NotePatron(customer, PatronWalkedOut);
        }

        /// <summary>Files today's outcome against the person it happened to, not against the till.
        /// The table is one day of customers — a linear scan is cheaper than a dictionary at that
        /// size and saves as two plain int lists.</summary>
        private void NotePatron(Pawn customer, int flag)
        {
            if (customer == null) return;
            int id = customer.thingIDNumber;
            for (int i = 0; i < patronIds.Count; i++)
            {
                if (patronIds[i] != id) continue;
                patronFlags[i] |= flag;
                return;
            }
            patronIds.Add(id);
            patronFlags.Add(flag);
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

            // Judge the day before its evidence is swept up.
            JudgeTheDay();

            revenueToday = 0;
            patronIds.Clear();
            patronFlags.Clear();
        }

        /// <summary>Turns a day of outcomes into one move on the town's name, at midnight.
        ///
        /// Nightly rather than per-event so the number is a reputation and not a scoreboard: nothing
        /// one customer does can swing it, and nothing the player does is invisible either — the
        /// ledger shows the day's record forming as it happens.</summary>
        private void JudgeTheDay()
        {
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
