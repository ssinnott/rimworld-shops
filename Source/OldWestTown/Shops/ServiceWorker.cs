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
        /// customer job fetches an item first, whether the town's survey counts it as an offering
        /// in its own right or leaves it to the stack it would consume, and the staffing/appeal
        /// gate.</summary>
        public virtual bool ConsumesStock => false;

        /// <summary>How much one round of this service riles up a rowdy customer (see
        /// TroubleUtility.Notify_ServiceRound), before the sheriff/shopkeeper mitigation factors
        /// apply. Zero for every service but Drink; Wager's own rowdiness is outcome-dependent
        /// and does not go through this at all — see CanCauseTrouble.</summary>
        public virtual float RowdinessPerUse => 0f;

        /// <summary>True for a service whose ApplyEffect can ever hand back positive rowdiness.
        /// The default just reads RowdinessPerUse, which is enough for every service through
        /// Lodging; Wager overrides this outright, since a win/loss/accusation outcome can't be
        /// read off one constant the way "0.2 per drink" can. WorkGiver_Patrol and the
        /// disturbance-line UI gate on this rather than RowdinessPerUse directly, so a
        /// gambling-only town still has something for a sheriff to patrol for.</summary>
        public virtual bool CanCauseTrouble => RowdinessPerUse > 0f;

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
        /// customer's inventory by ShopTransaction.TryServe), or null otherwise. <paramref
        /// name="pricePaid"/> is the exact amount TryServe already charged the customer two
        /// lines above this call — a worker whose effect depends on the stake (Wager's payout)
        /// gets it passed in rather than recomputing it, so "what was charged" and "what the
        /// effect pays out against" are the same value by construction. Returns a Thing this
        /// service claimed for longer than the transaction itself — Lodging hands back the bed
        /// it just booked, so the caller can hand that to whatever tracks a stay
        /// (CustomerRecord.rentedBed) — or null for a service whose effect is complete the
        /// instant this returns. <paramref name="roundRowdiness"/> is how much this one round
        /// should nudge the customer's own OWT_Rowdy severity (see
        /// TroubleUtility.Notify_ServiceRound) — every worker before Wager just echoes back its
        /// own RowdinessPerUse; Wager is the first whose value depends on what actually
        /// happened this round.</summary>
        public abstract Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed, int pricePaid, out float roundRowdiness);

        /// <summary>Shared "floored, not gated" desirability curve against a Food or Joy need: a
        /// hungry or bored customer is likelier to want it, but never so unlikely that a
        /// satisfied one won't occasionally indulge anyway. See IncidentWorker_ShopCustomers'
        /// relaxed arrival topup, which exists so Meal has genuinely hungry customers to sell
        /// to. Shared by Ingest and Wager, whose demand curves are the same shape against
        /// different needs.</summary>
        protected static float NeedDesirability(Pawn customer, ServiceNeedHook hook)
        {
            Need need = hook switch
            {
                ServiceNeedHook.Food => customer?.needs?.food,
                ServiceNeedHook.Joy => customer?.needs?.joy,
                _ => null
            };
            if (need == null) return 1f;
            return Mathf.Lerp(2.5f, 1f, need.CurLevelPercentage);
        }
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

        public override float Desirability(Pawn customer) => NeedDesirability(customer, needHook);

        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed, int pricePaid, out float roundRowdiness)
        {
            roundRowdiness = RowdinessPerUse;
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

        /// <summary>Nobody wants a thought they already carry. Zero and not merely low: the score
        /// this multiplies is compared against a floor of zero, so a customer who was in the chair
        /// an hour ago simply stops picking the barber, and the colonist order menu reads the same
        /// answer to refuse an order that would do nothing. One question, answered in one place —
        /// and the rate limit therefore lives on the thought's own durationDays, in XML, where a
        /// modder retuning the reward retunes the pacing with it.</summary>
        public override float Desirability(Pawn customer)
        {
            if (thoughtDef == null) return 0f;
            // A pawn with no mood need reads as "has never had this thought" through the null
            // chain, which is the wrong answer twice over: they cannot receive it either, so the
            // honest answer to "would this do anything for you" is no.
            MemoryThoughtHandler memories = customer?.needs?.mood?.thoughts?.memories;
            if (memories == null) return 0f;
            return memories.GetFirstMemoryOfDef(thoughtDef) == null ? 1f : 0f;
        }

        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed, int pricePaid, out float roundRowdiness)
        {
            roundRowdiness = 0f;
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
        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed, int pricePaid, out float roundRowdiness)
        {
            base.ApplyEffect(shop, service, customer, consumed, pricePaid, out roundRowdiness);

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

        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed, int pricePaid, out float roundRowdiness)
        {
            roundRowdiness = 0f;
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

    /// <summary>A hand of faro: the first business where the "sale" is a wager rather than a
    /// purchase. Payment already happened in TryServe — the ante, priced exactly like a
    /// haircut, through the same Markup/ReputationPriceFactor formula every other price in the
    /// mod uses. Everything here decides whether that stake comes back doubled, doesn't come
    /// back at all, or — on the one outcome worse than either — costs the house more than it
    /// can pay.
    ///
    /// The maths: win chance is (1 - HouseEdge) / payoutMultiplier, so a win pays
    /// pricePaid * payoutMultiplier and the customer's expected return per silver staked is
    /// exactly -HouseEdge, for any payoutMultiplier — HouseEdge alone is, by construction, the
    /// fraction of every wager the house keeps on average. See docs/economy.md for the numbers
    /// this lands on at the shipped defaults.</summary>
    public class ServiceWorker_Wager : ServiceWorker
    {
        /// <summary>Which need this feeds, for Desirability scoring — Joy, the same hook Drink
        /// uses: a hand of cards is recreation exactly like a round at the bar.</summary>
        public ServiceNeedHook needHook = ServiceNeedHook.None;

        /// <summary>What a win pays, as a multiple of the stake. HouseEdge alone decides win
        /// probability against this value (see the class doc), so the player's expected return
        /// per silver wagered never depends on it — it's XML-tunable rather than hardcoded only
        /// so a future higher-multiplier/lower-probability table variant needs no new worker
        /// class. The shipped def never overrides it; HouseEdge stays the only player-facing dial.</summary>
        public float payoutMultiplier = 2f;

        /// <summary>Severity OWT_Rowdy gains on an ordinary loss.</summary>
        public float lossRowdiness = 0.2f;

        /// <summary>Extra severity on top of lossRowdiness when a loss also draws a cheating
        /// accusation.</summary>
        public float accusationRowdinessBonus = 0.15f;

        /// <summary>Multiplies lossRowdiness for the worst outcome the mechanic has: the house
        /// wins the hand for the customer, then can't fully pay it out.</summary>
        public float shortPayRowdinessMultiplier = 2f;

        /// <summary>Chance an unlucky loss draws a cheating accusation, at dealer Social 0.
        /// Mirrors TroubleUtility.ShopkeeperSocialFactor's own Lerp-by-skill shape, but as a
        /// probability rather than a rowdiness multiplier — a skilled dealer doesn't just calm
        /// patrons faster once they're rowdy, they get accused of cheating less often to begin
        /// with, which is the observable, round-to-round signal the brief asks for.</summary>
        public float baseAccusationChance = 0.25f;

        /// <summary>Floor on the accusation chance at max Social — never truly zero, since even
        /// a smooth dealer loses a hand ugly sometimes.</summary>
        public float minAccusationChance = 0.02f;

        /// <summary>Joy granted for playing a hand at all, win, lose or shortfall — the same
        /// unconditional shape ServiceWorker_Ingest uses for nutrition. needHook above scores
        /// Desirability against Joy, so something here has to actually move it, or a bored
        /// customer's pull toward the table would never taper off with play the way it does for
        /// every other need-scored service. A flat constant rather than something read off a
        /// consumed Thing, since a wager has no Thing to read one from.</summary>
        public float joyGainPerHand = 0.1f;

        // ConsumesStock, CanUse and IsAvailable all stay at ServiceWorker's own defaults
        // (false / false / true): a wager consumes nothing off a shelf and needs nothing beyond
        // a staffed table to be on offer.

        public override bool CanCauseTrouble => true;

        public override float Desirability(Pawn customer) => NeedDesirability(customer, needHook);

        public override Thing ApplyEffect(CompBusiness shop, ServiceDef service, Pawn customer, Thing consumed, int pricePaid, out float roundRowdiness)
        {
            roundRowdiness = 0f;

            // Playing a hand is the recreation, independent of how it comes out — same reasoning
            // as paying for the ante itself being unconditional. Without this, Desirability's own
            // Joy scoring above never responds to the thing it's supposedly satisfying.
            if (!customer.Dead && customer.needs?.joy != null)
            {
                customer.needs.joy.CurLevel += joyGainPerHand;
            }

            // Guards a modder setting payoutMultiplier to zero in XML; the shipped default is
            // 2 and nothing here ever changes it.
            float winChance = Mathf.Clamp01((1f - shop.HouseEdge) / Mathf.Max(0.01f, payoutMultiplier));

            if (Rand.Chance(winChance))
            {
                int owed = Mathf.RoundToInt(pricePaid * payoutMultiplier);
                int paid = ShopTransaction.PayOutFromTill(shop, customer, owed);
                if (paid < owed)
                {
                    // The one outcome worse than a loss: the house won the hand for the customer
                    // and then couldn't make good on it. Closing the table is the same legible
                    // failure every other business already has for running out of something to
                    // sell — a bare shelf just never comes with a debt attached to it.
                    shop.RecordShortfall();
                    shop.parent.Map?.GetComponent<TownEconomy>()?.RecordShortfall(customer.Faction);
                    shop.Open = false;
                    roundRowdiness = lossRowdiness * shortPayRowdinessMultiplier;
                    Messages.Message(
                        "OWT_HouseCantCover".Translate(customer.LabelShort, shop.parent.Label,
                            ((float)paid).ToStringMoney(), ((float)owed).ToStringMoney()),
                        new LookTargets(shop.parent), MessageTypeDefOf.NegativeEvent);
                }
                return null;
            }

            roundRowdiness = lossRowdiness;

            // shop.Shopkeeper is guaranteed non-null here — OWT_Wager.allowsSelfService is
            // false, so TryServe already refused an unattended round before ApplyEffect ever
            // ran. The ?. is defensive anyway, mirroring the same guard on the same read in
            // TroubleUtility.ShopkeeperSocialFactor.
            int dealerSocial = shop.Shopkeeper?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            float accusationChance = Mathf.Clamp01(Mathf.Lerp(
                baseAccusationChance, minAccusationChance, (float)dealerSocial / SkillRecord.MaxLevel));

            if (Rand.Chance(accusationChance))
            {
                // The rowdiness bump always lands; the message is throttled separately —
                // mirrors JobDriver_PatronizeBusiness.WalkOut's own split between "the
                // consequence always applies" and "the message is rate-limited so a burst of
                // them reads as one event in the log."
                roundRowdiness += accusationRowdinessBonus;
                if (shop.TryClaimAccusationMessage())
                {
                    Messages.Message(
                        "OWT_CheatingAccusation".Translate(customer.LabelShort, shop.parent.Label),
                        new LookTargets(shop.parent), MessageTypeDefOf.NegativeEvent);
                }
            }
            return null;
        }
    }
}
