---
title: Adding content
summary: Adding a business, a service or a building — what is XML, what needs code, and the shape of each.
---

Most additions to this mod are XML. The two data-driven defs — [`ShopKindDef`](businesses.md)
and [`ServiceDef`](services.md) — exist precisely so that a new business or a new service is a
stanza rather than a class.

> **Whatever you add, update this wiki in the same change.** A new business kind belongs in
> [Business kinds](businesses.md) and the [reference tables](reference.md); a new service in
> [Services](services.md); a new building in [Buildings](buildings.md) and the
> [art gallery](art.md). Then add a line to the [changelog](changelog.md). See
> [contributing](contributing.md#keeping-the-wiki-honest) — CI checks that every def is
> mentioned somewhere in these pages, and that the gallery shows every shipped texture.

## Add a business kind

A kind with no services is pure data. Add it to `Defs/ShopKindDefs/ShopKinds.xml`:

```xml
<OldWestTown.Shops.ShopKindDef>
  <defName>OWT_Gunsmith</defName>
  <label>gunsmith</label>
  <description>Powder, shot and iron. Nothing a frontier town needs more of.</description>
  <defaultMarkup>1.6</defaultMarkup>
  <markupRange>0.5~3.5</markupRange>
  <appeal>1.2</appeal>
  <customerPatienceTicks>2500</customerPatienceTicks>
  <defaultStockCategories>
    <li>Weapons</li>
  </defaultStockCategories>
</OldWestTown.Shops.ShopKindDef>
```

Then point a building at it (below). Nothing else is required — the work giver, customer AI,
pricing, appeal and ledger are all kind-agnostic.

**The fields.**

| Field | Type | Default | What it does |
| --- | --- | --- | --- |
| `defaultStockCategories` | list of `ThingCategoryDef` | empty | Categories switched on in a newly built counter's Stock filter. |
| `defaultStockThings` | list of `ThingDef` | empty | Individual defs switched on beyond those categories. |
| `defaultMarkup` | float | 1.35 | Markup a fresh counter starts at. |
| `markupRange` | `FloatRange` | 0.5~3.0 | The band the player's price slider may move within. |
| `appeal` | float | 1.0 | How much one open, stocked business of this kind adds to town [appeal](economy.md#appeal). |
| `customerNoun` | string | "customer" | Reserved. Nothing in the UI reads it yet — set it if you like, but it will not show up anywhere. |
| `customerPatienceTicks` | int | 2500 | How long a customer waits at an unattended counter before [walking out](customers.md#walkouts). |
| `services` | list of `ServiceDef` | empty | [Services](services.md) this business offers alongside its stock. |

**Tuning notes.** `appeal` is counted per shop-*front*, and repeats fall away geometrically: the
second front of a kind is worth 35% of the first, the third 12%, and the whole kind never adds up
to more than 1.54 times one front. A second counter of the same kind on the same sales floor is
not a repeat at all — it is a second till, and adds nothing to the businesses term; what it buys
the player is serving two customers at once. So a new kind is worth adding for its own sake — pick
a value near 1.0 unless the business is genuinely a bigger draw. `customerPatienceTicks` is the
main difficulty lever: a short fuse (the saloon's 1500) makes staffing urgent.

## Add a building

Buildings inherit `OWT_CounterBase` in `Defs/ThingDefs_Buildings/Buildings_Commerce.xml`, which
supplies the size, interaction cell, stuff, research prerequisite and Stock tab. A new one needs
a graphic, an icon, and a `CompProperties_Business`:

```xml
<ThingDef ParentName="OWT_CounterBase">
  <defName>OWT_GunsmithBench</defName>
  <label>gunsmith's counter</label>
  <description>A counter with a rack behind it.</description>
  <graphicData>
    <texPath>Things/Building/Commerce/GunsmithBench</texPath>
    <graphicClass>Graphic_Multi</graphicClass>
    <drawSize>(2,1)</drawSize>
    <damageData><rect>(0,0.05,2,0.9)</rect></damageData>
  </graphicData>
  <uiIconPath>Things/Building/Commerce/GunsmithBench_north</uiIconPath>
  <comps>
    <li Class="OldWestTown.Shops.CompProperties_Business">
      <shopKind>OWT_Gunsmith</shopKind>
      <openAirRadius>9.9</openAirRadius>
    </li>
  </comps>
</ThingDef>
```

`openAirRadius` is the fallback sales floor for a counter with no room around it — a stall, a
boardwalk table. Indoors it is ignored and the room is the shop; outdoors it decides both what is
on the shelves and whether two counters of the same kind standing near each other are one
shop-front or two. It defaults to 9.9, so leave it out unless the building trades from a smaller
patch.

Then add a row to the `BUILDINGS` table in `tools/make_textures.py` and run it. CI fails if a
building in that table has no art on disk, so this is not optional:

```sh
pip install Pillow
python3 tools/make_textures.py                     # draw the art
python3 tools/validate_docs.py --sync-art          # copy it into the wiki's gallery
```

Then add the four facings to the [art gallery](art.md) — CI fails on a texture the gallery
doesn't show. See [generating building art](contributing.md#generating-building-art) for what each
palette colour draws.

## Add a service

If an existing [worker class](#the-worker-classes) covers the behaviour, a service is two XML
stanzas and no code.

**1. A JobDef** in `Defs/JobDefs/Jobs_Commerce.xml`. Every service needs its own — `Job` has no
generic slot to carry a `Def` reference, so this is how the driver recovers which service it is
running. It **must** use `JobDriver_UseService`; `ServiceDef.ConfigErrors` rejects anything else.

```xml
<JobDef>
  <defName>OWT_ServeBath</defName>
  <driverClass>OldWestTown.AI.JobDriver_UseService</driverClass>
  <reportString>taking a bath at TargetB.</reportString>
  <casualInterruptible>false</casualInterruptible>
</JobDef>
```

**2. The ServiceDef** in `Defs/ServiceDefs/Services_Commerce.xml`:

```xml
<OldWestTown.Shops.ServiceDef>
  <defName>OWT_Bath</defName>
  <label>hot bath</label>
  <jobDef>OWT_ServeBath</jobDef>
  <serveTicks>1800</serveTicks>
  <basePrice>12</basePrice>
  <allowsSelfService>false</allowsSelfService>
  <worker Class="OldWestTown.Shops.ServiceWorker_Thought">
    <thoughtDef>OWT_HotBath</thoughtDef>
  </worker>
</OldWestTown.Shops.ServiceDef>
```

**3. List it** on a business kind's `<services>`, and add the `ThoughtDef` if you referenced one.

**The fields.**

| Field | Type | Default | What it does |
| --- | --- | --- | --- |
| `jobDef` | `JobDef` | *required* | The job a customer runs to receive this service. One per service, never shared. |
| `worker` | `ServiceWorker` | *required* | Pluggable behaviour, with its own XML-configurable fields. |
| `colonistJobDef` | `JobDef` | *(none)* | A second job, running `JobDriver_ColonistUseService`, that lets the player send their own colonists for this service. They pay nothing and leave no row in the town's books. Leave it out and the service stays a stranger's. |
| `serveTicks` | int | 180 | Continuous **staffed** ticks required to complete one visit — and, through it, how deep a line will form for it. Nobody joins a wait longer than 6000 ticks, so a counter holds `ceil(6000 / serveTicks)`: 40 at the saloon's 150, three at the barber's 2200. A long serve is what makes a queue a real thing. |
| `basePrice` | float | 10 | Price basis, used **only** when nothing on the shelf backs the service. |
| `allowsSelfService` | bool | false | Whether the *Allow self-service* setting applies to this service at all. |

Both `jobDef` and `worker` are validated at load time: a def missing either surfaces as a red
error naming the def, rather than as a null reference from inside a pawn's think tree an hour
into the game.

To let the colony use the service too, add a second JobDef with `driverClass`
`OldWestTown.AI.JobDriver_ColonistUseService` and name it in `colonistJobDef`. `ConfigErrors`
rejects any other driver there, and rejects it outright on a stock-consuming service — a colonist
pays nothing, so nobody would be charged for the item taken off the shelf.

### The worker classes

`ServiceWorker` is the pluggable behaviour behind a service. Three concrete classes ship, and a
new service usually needs no new code at all — just XML pointing at one of them.

| Class | Consumes stock | What it does |
| --- | --- | --- |
| `ServiceWorker_Ingest` | yes | Consumes one matching item off the display and resolves its effect through that item's own vanilla ingestion outcome. Filtered by `foodType` and/or `requireMeal`; scored against a `needHook` of `Food`, `Joy` or `None`. Ships parameterized twice: [drink](services.md#drink) (Liquor / Joy) and [meal](services.md#meal) (any meal / Food). |
| `ServiceWorker_Thought` | no | Grants a thought, and refuses anyone who already carries it — so the rate limit lives on the thought's own `durationDays`, in XML. Reusable by any future stock-free service. |
| `ServiceWorker_Haircut` | no | `ServiceWorker_Thought` plus a visible hair change, using the same helper vanilla's own automatic styling uses. |

An ingest worker's `Desirability` is `Lerp(2.5, 1, need%)`: a hungry customer is likelier to
order, but the value is **floored above zero**, so a satisfied one still occasionally will.

> `ServiceWorker_Ingest` calls `Thing.Ingested` directly rather than handing off to
> `FoodUtility.IngestFromInventoryNow`, which would start a fresh job and tear down the running
> service driver mid-toil. `Ingested` is the call vanilla's own ingest driver finishes with, so a
> beer still lands its hediff — the customer just drinks it at the bar, where they paid for it.

### When it needs code

Write a new `ServiceWorker` subclass only when the *effect* is genuinely new. Override:

| Member | When |
| --- | --- |
| `ConsumesStock` | Return true if the service eats an item off the shelf. Changes pricing, appeal accounting and whether the customer fetches anything. |
| `CanUse(Thing)` | Required if `ConsumesStock` is true — which shelf items qualify. |
| `Desirability(Pawn)` | If demand should vary by pawn state. Return zero only when the service genuinely would do nothing for this pawn — that is the answer that takes the shop off their list and greys out the colonist order. Where it is a want that never quite goes away, floor it above zero so a satisfied customer still occasionally buys. |
| `ApplyEffect(Pawn, Thing)` | Always. The `Thing` is null for a stock-free service. |

> Do not start a new job from inside `ApplyEffect`. It runs inside the service job's own toil,
> and starting a second job tears the current driver down mid-toil. Apply the effect directly, as
> `ServiceWorker_Ingest` does with `Thing.Ingested`.

## Add a new kind of business entirely

If the new business isn't "sell an item" or "sell a service" — a rentable bed, a gambling table,
a bank — reuse the seam rather than routing around it:

- Keep the **shared-state** rule: the customer and the colonist read and write `CompBusiness`,
  never each other.
- Build a *visitor's* job on `JobDriver_PatronizeBusiness` and you inherit the walk/wait/patience/
  walkout shape, a place in the counter's line, and the [alert](customers.md#the-alert) for free.
  A job for the colony's own people should not use it: its patience branch is a walkout, which
  writes a shop ledger line, a row in the town's patron table and a reputation cost, and none of
  those should ever be about a colonist. See `JobDriver_ColonistUseService`.
- Move money through `ShopTransaction` and decide prices in `ShopPricing`.
- Implement `IBusinessPatron` on the driver, and put the counter at `TargetIndex.B` and the
  standing cell at `TargetIndex.C`. That is how queue spacing, the line at the counter, the
  waiting-customers alert, the shopkeeper's customer scan and closing time's reprieve all find
  your patron without the business layer naming your type. Answer `BeingServed` honestly — it is
  what stops a sale being torn up one tick from the till.

The [roadmap](roadmap.md) sketches several of these.

## Before you commit

```sh
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release   # rebuild the shipped assembly
python3 tools/validate_defs.py                                  # types, def refs, translation keys
python3 tools/make_textures.py --check                          # every building has art
python3 tools/validate_docs.py                                  # every def and texture is documented here
```

See [contributing](contributing.md) for what each of those actually checks.
