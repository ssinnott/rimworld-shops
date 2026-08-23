using RimWorld;
using UnityEngine;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>Which pawn need, if any, a service's desirability is weighted against.
    /// A raw NeedDef reference isn't used here because the two needs this stage cares about
    /// (Food, Joy) are proven fields on Pawn_NeedsTracker, whereas NeedDefOf doesn't expose
    /// a Joy entry to reference by name.</summary>
    public enum ServiceNeedHook { None, Food, Joy }

    /// <summary>
    /// Pluggable behaviour for a ServiceDef: what it can act on, how much a given customer
    /// wants it, and what happens once it's paid for.
    /// </summary>
    public abstract class ServiceWorker
    {
        /// <summary>True for a service that consumes a specific Thing off the shelf (Drink,
        /// Meal); false for one that consumes nothing but time (Haircut). Drives whether the
        /// customer job fetches an item first, whether TownEconomy counts it in ServiceValue
        /// or leaves it to StockValue, and the staffing/appeal gate.</summary>
        public virtual bool ConsumesStock => false;

        /// <summary>For a stock-consuming service: does this Thing qualify? Never called
        /// otherwise.</summary>
        public virtual bool CanUse(Thing t) => false;

        /// <summary>How much this customer wants it right now, as a multiplier alongside
        /// ShopPricing.ValueAppeal. Deliberately floored above zero — see subclass.</summary>
        public virtual float Desirability(Pawn customer) => 1f;

        /// <summary>Applies the effect once payment has cleared. <paramref name="consumed"/>
        /// is the Thing handed over for a stock-consuming service (already moved into the
        /// customer's inventory by ShopTransaction.TryServe), or null otherwise.</summary>
        public abstract void ApplyEffect(Pawn customer, Thing consumed);
    }

    /// <summary>Drink and Meal: consumes one matching item already on the counter's display,
    /// and resolves its effect through that item's own vanilla ingestion outcome instead of a
    /// bespoke one.</summary>
    public class ServiceWorker_Ingest : ServiceWorker
    {
        /// <summary>Required IngestibleProperties.foodType flag(s); FoodTypeFlags.None to skip.</summary>
        public FoodTypeFlags foodType = FoodTypeFlags.None;

        /// <summary>Require IngestibleProperties.IsMeal.</summary>
        public bool requireMeal;

        /// <summary>Which need this feeds, for Desirability scoring.</summary>
        public ServiceNeedHook needHook = ServiceNeedHook.None;

        public override bool ConsumesStock => true;

        public override bool CanUse(Thing t)
        {
            IngestibleProperties ing = t?.def?.ingestible;
            if (ing == null) return false;
            if (requireMeal && !ing.IsMeal) return false;
            if (foodType != FoodTypeFlags.None && (ing.foodType & foodType) == 0) return false;
            return true;
        }

        public override float Desirability(Pawn customer)
        {
            Need need = needHook switch
            {
                ServiceNeedHook.Food => customer?.needs?.food,
                ServiceNeedHook.Joy => customer?.needs?.joy,
                _ => null
            };
            if (need == null) return 1f;
            // Floored, not gated: a hungry customer is likelier to order, but a satisfied one
            // still occasionally will. See IncidentWorker_ShopCustomers' relaxed arrival
            // topup, changed alongside this so Meal has genuinely hungry customers to sell to.
            return Mathf.Lerp(2.5f, 1f, need.CurLevelPercentage);
        }

        public override void ApplyEffect(Pawn customer, Thing consumed)
        {
            if (customer == null || consumed == null) return;

            // Resolve the ingestion here rather than handing off to FoodUtility.IngestFromInventoryNow,
            // which starts a fresh job: this runs inside the service job's own toil, and starting a
            // second job from inside a running one tears the current driver down mid-toil. Thing.Ingested
            // is the call vanilla's own ingest driver finishes with, so a beer still lands its hediff and
            // a meal still lands its thoughts — the customer just drinks it at the bar, where they paid
            // for it, instead of wandering off with it.
            float nutrition = consumed.Ingested(customer, customer.needs?.food?.NutritionWanted ?? 0f);
            if (!customer.Dead && customer.needs?.food != null)
            {
                customer.needs.food.CurLevel += nutrition;
            }
        }
    }

    /// <summary>Generic "grant a thought" primitive — reusable by any future stock-free
    /// service. Deliberately does nothing else.</summary>
    public class ServiceWorker_Thought : ServiceWorker
    {
        public ThoughtDef thoughtDef;

        public override void ApplyEffect(Pawn customer, Thing consumed)
        {
            if (thoughtDef == null) return;
            customer.needs?.mood?.thoughts?.memories?.TryGainMemory(thoughtDef);
        }
    }

    /// <summary>Haircut: the mood thought above, plus a visible hair change — a stronger
    /// "legible effect" than a thought alone, using the same helper vanilla's own automatic
    /// styling uses for age/gender-appropriate selection.</summary>
    public class ServiceWorker_Haircut : ServiceWorker_Thought
    {
        public override void ApplyEffect(Pawn customer, Thing consumed)
        {
            base.ApplyEffect(customer, consumed);

            // Best-effort cosmetic flourish. Guarded defensively: a generated visitor's style
            // tracker population is not something this mod has run in-game.
            if (customer?.RaceProps?.Humanlike != true || customer.story == null) return;
            HairDef hair = PawnStyleItemChooser.RandomHairFor(customer);
            if (hair == null) return;
            customer.story.hairDef = hair;
            customer.Drawer?.renderer?.SetAllGraphicsDirty();
        }
    }
}
