---
title: Services
summary: Drink, meal and haircut — businesses that sell time rather than goods, and the worker classes behind them.
---

A **service** is something a business sells that isn't an item off a shelf. It is priced, queued
for and paid through exactly the same seam as a sale — there just isn't a `Thing` changing hands
at the end of it, or, for a haircut, no `Thing` involved at all.

Services are `ServiceDef`s. A [business kind](businesses.md) lists the ones it offers in its
`services` field, and each `ServiceDef` embeds one `ServiceWorker` that supplies the
type-specific behaviour.

## What a ServiceDef controls

| Field | Type | Default | What it does |
| --- | --- | --- | --- |
| `jobDef` | `JobDef` | *required* | The job a customer runs to receive this service. One per service, never shared. |
| `worker` | `ServiceWorker` | *required* | Pluggable behaviour, with its own XML-configurable fields. |
| `serveTicks` | int | 180 | Continuous **staffed** ticks required to complete one visit. |
| `basePrice` | float | 10 | Price basis, used **only** when nothing on the shelf backs the service. |
| `allowsSelfService` | bool | false | Whether the *Allow self-service* setting applies to this service at all. |

Both `jobDef` and `worker` are validated at load time: a def missing either surfaces as a red
error naming the def, rather than as a null reference from inside a pawn's think tree an hour
into the game.

### Why every service gets its own JobDef

`Verse.AI.Job` has no generic slot to carry a `Def` reference, so a service driver has no other
reliable way to recover which service it is running. The cost is one small XML stanza per
service; the alternative is a driver that has to guess.

## The three that ship

### Drink

`OWT_Drink` — a round at the [saloon bar](buildings.md#saloon-bar).

| | |
| --- | --- |
| Worker | `ServiceWorker_Ingest` (`foodType` = Liquor, need = **Joy**) |
| Serve time | 150 ticks |
| Consumes stock | yes — one Liquor-flagged item off the bar's own shelves |
| Price | the consumed item's market value × the shop's markup |
| Self-service | allowed (if the setting is on) |
| Effect | the item is actually ingested — mood, the alcohol hediff, the usual vanilla outcomes |

### Meal

`OWT_Meal` — a hot meal at the bar.

| | |
| --- | --- |
| Worker | `ServiceWorker_Ingest` (`requireMeal`, need = **Food**) |
| Serve time | 150 ticks |
| Consumes stock | yes — any item whose `IngestibleProperties.IsMeal` is true |
| Price | the consumed meal's market value × the shop's markup |
| Self-service | allowed (if the setting is on) |
| Effect | eaten for real — nutrition and the meal's own thoughts |

Drink and Meal are the *same class* parameterized two ways, because a drink and a meal are the
same mechanic with a different filter and a different need behind the demand curve.

Customers arrive with food need between 40% and 90% — a band rather than a flat top-up,
specifically so a meal service has genuinely hungry customers to sell to.

### Haircut

`OWT_Haircut` — the [barber shop](businesses.md#barber-shop)'s whole trade.

| | |
| --- | --- |
| Worker | `ServiceWorker_Haircut` (thought = `OWT_FreshHaircut`) |
| Serve time | **2200 ticks** — much longer than a drink |
| Consumes stock | **no** — nothing but time and chair space |
| Price | `basePrice` **16** × the shop's markup × the reputation factor |
| Self-service | **never**, whatever the setting says — an empty chair can't cut anyone's hair |
| Effect | a `+5` mood thought for 1.5 days, **and a visibly different hairstyle** |

The hair change is a deliberate design choice: a business that changes nothing visible is a
weaker proof that a service happened. It uses the same helper vanilla's own automatic styling
uses, so the new hair is age- and gender-appropriate.

## The worker classes

`ServiceWorker` is the pluggable behaviour behind a service. Three concrete classes ship, and a
new service usually needs no new code at all — just XML pointing at one of them.

| Class | Consumes stock | What it does |
| --- | --- | --- |
| `ServiceWorker_Ingest` | yes | Consumes one matching item off the display and resolves its effect through that item's own vanilla ingestion outcome. Filtered by `foodType` and/or `requireMeal`; scored against a `needHook` of `Food`, `Joy` or `None`. |
| `ServiceWorker_Thought` | no | A bare "grant a thought" primitive. Deliberately does nothing else — reusable by any future stock-free service. |
| `ServiceWorker_Haircut` | no | `ServiceWorker_Thought` plus a visible hair change. |

A worker answers three questions:

- **`CanUse(thing)`** — for a stock-consuming service, does this item on the shelf qualify?
- **`Desirability(customer)`** — how much does this customer want it *right now*? For an
  ingest-type service this is `Lerp(2.5, 1, need%)`: a hungry customer is likelier to order, but
  the value is **floored above zero**, so a satisfied one still occasionally will.
- **`ApplyEffect(customer, consumed)`** — what happens once payment clears.

> `ServiceWorker_Ingest` calls `Thing.Ingested` directly rather than handing off to
> `FoodUtility.IngestFromInventoryNow`, which would start a fresh job and tear down the running
> service driver mid-toil. `Ingested` is the call vanilla's own ingest driver finishes with, so a
> beer still lands its hediff — the customer just drinks it at the bar, where they paid for it.

## Services that consume stock

A drink is the interesting hybrid case: a service that still moves stock, and therefore has to
answer to both the goods loop and the service loop without double-counting.

The rules that fall out of it:

- **Availability.** A stock-free service is available whenever its business kind offers it. A
  stock-consuming one additionally needs a matching item on display *right now* — filtered by
  the same Stock filter the player already curates for goods.
- **Pricing.** A stock-consuming service is priced through the ordinary goods path against
  whatever it actually consumes. Only a stock-free service uses `basePrice`. There is exactly
  one place a price basis is decided, either way.
- **Appeal.** A stock-consuming service's value is already counted once, as stock. Town appeal's
  `ServiceValue` term only adds services with no `Thing` behind them at all — so a saloon's beer
  isn't counted twice for being sellable two ways.
- **The customer job.** A stock-consuming service fetches the item first and carries it to the
  stand; a stock-free one skips straight to waiting.

## How a service visit runs

Both service and goods visits share one shape, `JobDriver_PatronizeBusiness`:

1. **Fetch** — walk to the item and pick it up. *(Skipped entirely for a haircut.)*
2. **Walk** to the customer cell at the business.
3. **Wait to be served.** Patience burns down while the business is unstaffed; being attended
   restores it. Service must be **continuous** — a shopkeeper who drifts off mid-service starts
   the serve over rather than resuming it.
4. **Pay**, then the worker's effect lands.

If patience runs out first, the customer [walks out](customers.md#walkouts): the shop takes a
reputation hit, the customer refuses to queue there again this visit, and anything they were
carrying is dropped on the floor unpaid.
