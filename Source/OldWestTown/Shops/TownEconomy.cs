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
        private readonly List<CompShopCounter> shops = new List<CompShopCounter>();

        private int lastDayRolled = -1;

        public int revenueToday;
        public int customersServedToday;
        public int walkoutsToday;
        public int lifetimeRevenue;

        /// <summary>Rolling satisfaction, 0..1. Served customers push it up, walkouts push it down.</summary>
        private float reputation = 0.5f;

        public TownEconomy(Map map) : base(map) { }

        public float Reputation => Mathf.Clamp01(reputation);

        public IReadOnlyList<CompShopCounter> Shops => shops;

        public void Register(CompShopCounter shop)
        {
            if (shop != null && !shops.Contains(shop)) shops.Add(shop);
        }

        public void Deregister(CompShopCounter shop)
        {
            shops.Remove(shop);
        }

        /// <summary>Shops that could serve a customer right now.</summary>
        public IEnumerable<CompShopCounter> OpenShops()
        {
            for (int i = 0; i < shops.Count; i++)
            {
                CompShopCounter s = shops[i];
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

                foreach (CompShopCounter shop in OpenShops())
                {
                    if (shop.StockOnDisplay.Count == 0) continue;
                    // Each additional business of a kind already present is worth less.
                    float weight = kinds.Add(shop.Kind) ? 1f : 0.35f;
                    kindScore += (shop.Kind?.appeal ?? 1f) * weight;
                    stockScore += shop.StockValue;
                }

                if (kindScore <= 0f) return 0f;

                float goods = Mathf.Sqrt(stockScore / 1000f);          // diminishing returns on wealth
                float standing = Mathf.Lerp(0.5f, 1.5f, Reputation);   // a good name doubles your draw
                return kindScore * Mathf.Clamp(goods, 0.25f, 3f) * standing;
            }
        }

        public void RecordSale(int price)
        {
            revenueToday += price;
            lifetimeRevenue += price;
            customersServedToday++;
            reputation = Mathf.Clamp01(reputation + 0.01f);
        }

        public void RecordWalkout()
        {
            walkoutsToday++;
            reputation = Mathf.Clamp01(reputation - 0.02f);
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
                CompShopCounter comp = t.TryGetComp<CompShopCounter>();
                if (comp != null) Register(comp);
            }
        }
    }
}
