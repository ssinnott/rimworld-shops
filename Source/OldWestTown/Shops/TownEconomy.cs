using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// The town's books. Keeps the live shop register, rolls the day over, and turns trading
    /// history into the two numbers the rest of the mod cares about: <see cref="Appeal"/>
    /// (how many customers the town pulls in) and <see cref="Reputation"/> (what they'll pay,
    /// and how many bother coming).
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
                float standing = Mathf.Lerp(0.5f, 1.5f, Reputation);   // a good name draws half again as much
                return kindScore * Mathf.Clamp(goods, 0.25f, 3f) * standing;
            }
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
        }
    }
}
