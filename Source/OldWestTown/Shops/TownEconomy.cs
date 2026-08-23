using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// The town's books. Keeps the live shop register, rolls the day over, and turns trading
    /// history into the two numbers the rest of the mod cares about: <see cref="Appeal"/>
    /// (how many customers the town pulls in) and <see cref="Reputation"/> (what they'll pay).
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

        private readonly List<CompBusiness> shops = new List<CompBusiness>();

        private int lastDayRolled = -1;

        public int revenueToday;
        public int customersServedToday;
        public int walkoutsToday;
        public int lifetimeRevenue;

        /// <summary>Rolling satisfaction, 0..1. Served customers push it up, walkouts push it down.</summary>
        private float reputation = 0.5f;

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

        public void RecordSale(int price, bool selfService = false)
        {
            revenueToday += price;
            lifetimeRevenue += price;
            customersServedToday++;
            // A staffed sale builds the town's name. An honesty-box sale slowly erodes it —
            // customers remember a town where nobody stood behind the counter.
            reputation = Mathf.Clamp01(reputation + (selfService ? -0.005f : 0.01f));
        }

        public void RecordWalkout()
        {
            walkoutsToday++;
            reputation = Mathf.Clamp01(reputation - 0.02f);
        }

        /// <summary>A saloon boiling over costs the town more than one shrugged-off walkout —
        /// word of an actual disturbance travels further than word of slow service.</summary>
        public void RecordDisturbance()
        {
            reputation = Mathf.Clamp01(reputation - 0.05f);
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
