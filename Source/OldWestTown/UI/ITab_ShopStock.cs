using OldWestTown.Shops;
using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.UI
{
    /// <summary>
    /// Lets the player decide what a counter will sell. This reuses vanilla's storage filter
    /// widget, so "what this shop stocks" reads and behaves exactly like "what this stockpile
    /// accepts" — one less thing for a player to learn.
    /// </summary>
    public class ITab_ShopStock : ITab
    {
        private static readonly Vector2 WinSize = new Vector2(320f, 480f);

        private readonly ThingFilterUI.UIState filterState = new ThingFilterUI.UIState();

        public ITab_ShopStock()
        {
            size = WinSize;
            labelKey = "OWT_TabStock";
        }

        private CompBusiness Shop => (SelThing as ThingWithComps)?.TryGetComp<CompBusiness>();

        public override bool IsVisible => Shop != null;

        protected override void FillTab()
        {
            CompBusiness shop = Shop;
            if (shop == null) return;

            Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            GUI.BeginGroup(outer);

            Rect header = new Rect(0f, 0f, outer.width, 28f);
            Text.Font = GameFont.Small;
            Widgets.Label(header, "OWT_TabStockHeader".Translate(shop.StockOnDisplay.Count));

            Rect resetRect = new Rect(outer.width - 80f, 0f, 80f, 24f);
            if (Widgets.ButtonText(resetRect, "OWT_ResetStock".Translate()))
            {
                shop.ResetStockFilterToDefault();
            }

            Rect filterRect = new Rect(0f, 34f, outer.width, outer.height - 34f);
            int allowedBefore = shop.StockFilter.AllowedDefCount;
            ThingFilterUI.DoThingFilterConfigWindow(filterRect, filterState, shop.StockFilter);

            GUI.EndGroup();

            // Refresh the shelves the moment the player changes the filter — but only then.
            // Invalidating every frame would rescan the whole room on every repaint.
            if (shop.StockFilter.AllowedDefCount != allowedBefore) shop.RefreshStock();
        }
    }
}
