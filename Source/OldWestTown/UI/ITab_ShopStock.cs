using System.Collections.Generic;
using OldWestTown.Shops;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.UI
{
    /// <summary>
    /// Where the player decides what a counter sells and what it charges. It reuses vanilla's
    /// storage filter widget, so "what this shop stocks" reads and behaves exactly like "what
    /// this stockpile accepts" — one less thing to learn — and puts the price above the tree,
    /// beside the two figures a price is a choice between: what the shelves are worth, and what
    /// this counter is asking for them.
    /// </summary>
    public class ITab_ShopStock : ITab
    {
        private static readonly Vector2 WinSize = new Vector2(320f, 480f);

        /// <summary>Slider granularity, matching the whole-percent dialog this control replaces,
        /// so no markup an existing save can hold sits off the grid of the control that now
        /// edits it.</summary>
        private const float MarkupStep = 0.01f;

        /// <summary>RimWorld builds one ITab per tab TYPE and shares it across every building
        /// that lists it, so this scroll position and quick-search box are one object serving
        /// every counter in town.</summary>
        private readonly ThingFilterUI.UIState filterState = new ThingFilterUI.UIState();

        /// <summary>thingIDNumber of the counter <see cref="filterState"/> was last drawn for, so
        /// selecting a different counter starts on a clean tree instead of inheriting a search
        /// typed at another shop — which the player cannot see they left there, because the box
        /// holding it belongs to the tab and not to the counter. An id rather than the Thing, so
        /// a demolished counter is not kept alive by the tab that last drew it.</summary>
        private int filterStateOwner = -1;

        public ITab_ShopStock()
        {
            size = WinSize;
            labelKey = "OWT_TabStock";
        }

        private CompBusiness Shop => (SelThing as ThingWithComps)?.TryGetComp<CompBusiness>();

        public override bool IsVisible => Shop != null;

        /// <summary>Thing ids restart at zero in a new game while this tab instance lives as long
        /// as the process, so without this a counter in the next save could inherit a search by
        /// sharing an id with one in the last.</summary>
        public override void Notify_ClearingAllMapsMemory()
        {
            base.Notify_ClearingAllMapsMemory();
            filterStateOwner = -1;
        }

        /// <summary>A focused search box goes on eating keystrokes after the player clicks back
        /// onto the map. Vanilla's ITab_Storage, which holds the same UIState, does the same.</summary>
        public override void Notify_ClickOutsideWindow()
        {
            base.Notify_ClickOutsideWindow();
            filterState.quickSearch.Unfocus();
        }

        /// <summary>Reopening the tab scrubs the search too. The owner check below only fires when
        /// the selection MOVES, so without this a player who searched, clicked away and came back to
        /// the same counter would find their tree still filtered by a search they had forgotten.</summary>
        public override void OnOpen()
        {
            base.OnOpen();
            filterStateOwner = -1;
        }

        protected override void FillTab()
        {
            CompBusiness shop = Shop;
            if (shop == null) return;

            if (filterStateOwner != shop.parent.thingIDNumber)
            {
                filterStateOwner = shop.parent.thingIDNumber;
                filterState.quickSearch.Reset();
                filterState.scrollPosition = Vector2.zero;
            }

            Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            GUI.BeginGroup(outer);

            // What is out and what it is worth, because the price below is a choice between those
            // two figures. Both come off the counter's memoised totals: pricing a shelf costs a
            // MarketValue lookup per stack, and this draws every frame.
            //
            // The whole width, and the Reset button below it rather than beside it: an established
            // store's line runs past forty characters, and the field that fell off the end when it
            // had to share the row was the asking price — the one number this tab exists to show.
            Rect shelfRect = new Rect(0f, 0f, outer.width, 20f);
            List<Thing> stock = shop.StockOnDisplay;
            Widgets.LabelFit(shelfRect, stock.Count > 0
                ? "OWT_TabStockShelves".Translate(
                    stock.Count,
                    ((float)shop.StockMarketValue).ToStringMoney(),
                    ((float)shop.StockValue).ToStringMoney())
                : "OWT_TabStockEmpty".Translate());
            TooltipHandler.TipRegionByKey(shelfRect, "OWT_TabStockShelvesTip");

            // The price lives here rather than behind a gizmo's modal slider, because this is the
            // one screen that shows what moving it does.
            string asking = shop.Markup.ToStringPercent();
            float markup = shop.Markup;
            Rect sliderRect = new Rect(0f, 22f, outer.width, 30f);
            Widgets.HorizontalSlider(sliderRect, ref markup,
                shop.MarkupRange, "OWT_MarkupSlider".Translate(asking), MarkupStep);
            // What the price does, on the control that sets it — the sentence the deleted gizmo's
            // tooltip carried, and the only place the game says price decides WHICH counter a
            // customer walks to rather than merely what they pay at it.
            TooltipHandler.TipRegionByKey(sliderRect, "OWT_MarkupSliderTip");
            // Only a drag writes: looking at a shop must not change it. Compared with a tolerance
            // because the slider hands back its own snapped copy of the value, which does not
            // round-trip bit for bit — an exact compare rewrote a saved field on the first frame
            // the tab was drawn, for a player who had touched nothing.
            if (!Mathf.Approximately(markup, shop.Markup)) shop.Markup = markup;

            Text.Font = GameFont.Tiny;
            float curY = 56f;

            // The slider sets a markup; the till charges that markup moved by the town's name.
            // Drawn only when those are two different numbers, so this can never be a line
            // repeating what the slider just said.
            string paid = (shop.Markup * shop.ReputationPriceFactor).ToStringPercent();
            if (paid != asking)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, curY, outer.width, 20f),
                    "OWT_TabStockReputation".Translate(paid));
                GUI.color = Color.white;
                curY += 20f;
            }

            // A counter whose whole trade is a service has no shelf to read a price off, and a
            // saloon's drink is priced off a shelf nobody would think to look at.
            string services = ServicePrices(shop);
            if (services != null)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, curY, outer.width, 20f),
                    "OWT_TabStockServices".Translate(services));
                GUI.color = Color.white;
                curY += 20f;
            }

            Text.Font = GameFont.Small;
            Rect resetRect = new Rect(outer.width - 80f, curY, 80f, 24f);
            if (Widgets.ButtonText(resetRect, "OWT_ResetStock".Translate()))
            {
                shop.ResetStockFilterToDefault();
            }
            curY += 28f;

            Rect filterRect = new Rect(0f, curY, outer.width, outer.height - curY);
            int allowedBefore = shop.StockFilter.AllowedDefCount;
            ThingFilterUI.DoThingFilterConfigWindow(filterRect, filterState, shop.StockFilter);

            GUI.EndGroup();

            // Refresh the shelves the moment the player changes the filter — but only then.
            // Invalidating every frame would rescan the whole room on every repaint.
            if (shop.StockFilter.AllowedDefCount != allowedBefore) shop.RefreshStock();
        }

        /// <summary>What this kind sells that is not on a shelf, and what it costs — or null if it
        /// sells no services at all. A service that comes off the shelves is named without a price
        /// because it has not got one of its own: it is sold at the shelf price of whatever it is
        /// poured from, which is the one pricing rule in this mod nothing else states.</summary>
        private static string ServicePrices(CompBusiness shop)
        {
            List<ServiceDef> services = shop.Kind?.services;
            if (services == null || services.Count == 0) return null;

            List<string> parts = new List<string>();
            List<string> fromStock = new List<string>();
            for (int i = 0; i < services.Count; i++)
            {
                ServiceDef sd = services[i];
                if (sd?.worker == null) continue;
                if (sd.worker.ConsumesStock) { fromStock.Add(sd.label); continue; }
                parts.Add("OWT_TabStockServiceFixed".Translate(
                    sd.label,
                    ((float)ShopPricing.PriceForService(shop, sd)).ToStringMoney()).Resolve());
            }
            if (fromStock.Count > 0)
            {
                parts.Add("OWT_TabStockServiceStock".Translate(fromStock.ToCommaList(true)).Resolve());
            }
            return parts.Count > 0 ? parts.ToCommaList() : null;
        }
    }
}
