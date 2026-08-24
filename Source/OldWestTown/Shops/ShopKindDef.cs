using System.Collections.Generic;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// Describes a category of business — general store, saloon, gunsmith and so on.
    /// A <see cref="CompBusiness"/> points at one of these to get its default stock
    /// selection, its pricing band, and how much it contributes to the town's draw.
    /// </summary>
    public class ShopKindDef : Def
    {
        /// <summary>Categories switched on in a newly built counter's stock filter.</summary>
        public List<ThingCategoryDef> defaultStockCategories = new List<ThingCategoryDef>();

        /// <summary>Individual defs switched on beyond the categories above.</summary>
        public List<ThingDef> defaultStockThings = new List<ThingDef>();

        /// <summary>Markup a freshly built counter starts at, and the range the player may set.</summary>
        public float defaultMarkup = 1.35f;
        public FloatRange markupRange = new FloatRange(0.5f, 3f);

        /// <summary>Markup's twin dial for a business whose services include a wager
        /// (ServiceWorker_Wager): the fraction of every silver wagered the house expects to
        /// keep on average, and the range the player may set it in. Inert for every kind that
        /// doesn't offer a wager — the same "unused for most kinds" precedent
        /// defaultStockCategories and services already set.</summary>
        public float defaultHouseEdge = 0.15f;
        public FloatRange houseEdgeRange = new FloatRange(0f, 0.5f);

        /// <summary>
        /// How much a single open, stocked shop of this kind adds to town appeal.
        /// Appeal drives how often — and how rich — the customer incident is.
        /// </summary>
        public float appeal = 1f;

        /// <summary>Word used in the UI for a customer of this business ("customer", "patron", ...).</summary>
        public string customerNoun = "customer";

        /// <summary>Ticks a customer will wait at the counter before giving up and leaving unhappy.</summary>
        public int customerPatienceTicks = 2500;

        /// <summary>Services this business offers, alongside whatever it stocks.</summary>
        public List<ServiceDef> services = new List<ServiceDef>();
    }
}
