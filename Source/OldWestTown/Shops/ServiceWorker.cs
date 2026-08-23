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

        /// <summary>How much one round of this service riles up a rowdy customer (see
        /// TroubleUtility.Notify_ServiceRound), before the sheriff/shopkeeper mitigation factors
        /// apply. Zero for every service but Drink.</summary>
        public virtual float RowdinessPerUse => 0f;

        /// <summary>For a stock-consuming service: does this Thing qualify? Never called
        /// otherwise.</summary>
        public virtual bool CanUse(Thing t) => false;

        /// <summary>How much this customer wants it right now, as a multiplier alongside
        /// ShopPricing.ValueAppeal. Deliberately floored above zero — see subclass.</summary>
        public virtual float Desirability(Pawn customer) => 1f;

        /// <summary>True if this service can currently be performed at all, beyond just having
        /// a matching item on the shelf (ConsumesStock/CanUse already cover that — this is for
        /// a different kind of scarcity). Lodging is the first service that needs this: a
        /// stock-free service with a finite, contended resource behind it — a vacant bed —
        /// rather than an always-available one (Haircut). <paramref name="customer"/> is null for
        /// a customer-agnostic check (CompBusiness.AvailableServices, deciding whether to
        /// advertise the service at all); ShopTransaction.TryServe passes the actual paying
        /// customer, so a customer-aware override can require that the specific resource on
        /// offer is reachable to them, not just to someone.</summary>
        public virtual bool IsAvailable(CompBusiness shop, Pawn customer = null) => true;

        /// <summary>Applies the effect once payment has cleared. <paramref name="consumed"/>
        /// is the Thing handed over for a stock-consuming service (already moved into the
        /// customer's inventory by ShopTransaction.TryServe), or null otherwise. Returns a
        /// Thing this service claimed for longer than the transaction itself — Lodging hands
        /// back the bed it just booked, so the caller can hand that to whatever tracks a stay
        /// (CustomerRecord.rentedBed) — or null for a service whose effect is complete the
        /// instant this returns.</summary>
        public abstract Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed);
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

        /// <summary>XML dial for how much one round riles up a rowdy customer — only OWT_Drink's
        /// worker sets this above zero; OWT_Meal leaves it at the default.</summary>
        public float rowdinessPerServing = 0f;

        public override bool ConsumesStock => true;

        public override float RowdinessPerUse => rowdinessPerServing;

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

        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed)
        {
            if (customer == null || consumed == null) return null;

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
            return null;
        }
    }

    /// <summary>Generic "grant a thought" primitive — reusable by any future stock-free
    /// service. Deliberately does nothing else.</summary>
    public class ServiceWorker_Thought : ServiceWorker
    {
        public ThoughtDef thoughtDef;

        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed)
        {
            if (thoughtDef == null) return null;
            customer.needs?.mood?.thoughts?.memories?.TryGainMemory(thoughtDef);
            return null;
        }
    }

    /// <summary>Haircut: the mood thought above, plus a visible hair change — a stronger
    /// "legible effect" than a thought alone, using the same helper vanilla's own automatic
    /// styling uses for age/gender-appropriate selection.</summary>
    public class ServiceWorker_Haircut : ServiceWorker_Thought
    {
        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed)
        {
            base.ApplyEffect(shop, service, customer, consumed);

            // Best-effort cosmetic flourish. Guarded defensively: a generated visitor's style
            // tracker population is not something this mod has run in-game.
            if (customer?.RaceProps?.Humanlike != true || customer.story == null) return null;
            HairDef hair = PawnStyleItemChooser.RandomHairFor(customer);
            if (hair == null) return null;
            customer.story.hairDef = hair;
            customer.Drawer?.renderer?.SetAllGraphicsDirty();
            return null;
        }
    }

    /// <summary>Renting a bed for the night. The desirable-when-tired half of a stay, and the
    /// only ApplyEffect that returns a claimed Thing — the bed itself, handed back so the
    /// caller (JobDriver_UseService.CompleteService) can pass it on to
    /// CustomerRecord.rentedBed. The other half — actually sleeping in it, across however much
    /// of the visit that takes — is CompRentableBed and JobDriver_SleepInRentedBed.</summary>
    public class ServiceWorker_Lodging : ServiceWorker
    {
        public override bool IsAvailable(CompBusiness shop, Pawn customer = null) =>
            ShopStock.ChooseVacantBed(shop, customer) != null;

        public override float Desirability(Pawn customer)
        {
            // Hard-gated to humanlike pawns, unlike the floored-not-gated shape below — the
            // same restriction ServiceWorker_Haircut already applies for its own visible-effect
            // reasons. Here it's load-bearing: JobGiver_SleepInRentedBed decides "tired enough
            // to sleep" from Need_Rest, and a pawn with no such need (or one that's somehow
            // exempt from it) would book a room and then never grow tired enough to ever check
            // out — permanently blocking Trigger_VisitComplete for the whole group. A score of
            // exactly zero here is a hard exclusion, not just a discouragement: the scoring
            // loop this feeds only replaces its running best on a strictly-greater comparison
            // starting from zero, so a zero score can never win.
            if (customer?.RaceProps?.Humanlike != true) return 0f;

            // Same hard exclusion, same reason: a humanlike pawn with no Rest need at all (a
            // race/pawnkind variant that omits it) is exactly as permanently untireable as a
            // non-humanlike one, so it gets the same zero rather than the floored-not-gated
            // treatment below.
            Need_Rest rest = customer.needs?.rest;
            if (rest == null) return 0f;
            // Floored, not gated, the same shape as Ingest — but with a lower floor: booking a
            // room while wide awake is a much weaker impulse than an occasional drink, so a
            // well-rested customer only rarely bothers.
            return Mathf.Lerp(2.5f, 0.5f, rest.CurLevelPercentage);
        }

        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed)
        {
            Thing bed = ShopStock.ChooseVacantBed(shop, customer);
            // An extremely narrow same-tick race — another customer's ApplyEffect claimed the
            // last vacant bed between this job's IsAvailable check and now. No refund: payment
            // already cleared in TryServe. Accepted, same class of race DESIGN.md's Known Risks
            // already covers for stock.
            if (bed == null) return null;
            bed.TryGetComp<CompRentableBed>()?.Claim(customer, shop);
            return bed;
        }
    }
}
