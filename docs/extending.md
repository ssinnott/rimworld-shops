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
  <customerNoun>customer</customerNoun>
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
| `defaultHouseEdge` | float | 0.15 | Markup's twin dial for a kind offering a wager: the house's average take. Inert unless the kind's `services` includes one. |
| `houseEdgeRange` | `FloatRange` | 0.0~0.5 | The band the player's house-edge slider may move within. |
| `appeal` | float | 1.0 | How much one open, stocked business of this kind adds to town [appeal](economy.md#appeal). |
| `customerNoun` | string | "customer" | The word the UI uses for this business's customers. |
| `customerPatienceTicks` | int | 2500 | How long a customer waits at an unattended counter before [walking out](customers.md#walkouts). |
| `services` | list of `ServiceDef` | empty | [Services](services.md) this business offers alongside its stock. |

**Tuning notes.** `appeal` is per *distinct* kind, and a repeat of an existing kind is worth 35%
of the first, so a new kind is worth adding for its own sake — pick a value near 1.0 unless the
business is genuinely a bigger draw. `customerPatienceTicks` is the main difficulty lever: a
short fuse (the saloon's 1500) makes staffing urgent.

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
| `serveTicks` | int | 180 | Continuous **staffed** ticks required to complete one visit. |
| `basePrice` | float | 10 | Price basis, used **only** when nothing on the shelf backs the service. |
| `allowsSelfService` | bool | false | Whether the *Allow self-service* setting applies to this service at all. |

Both `jobDef` and `worker` are validated at load time: a def missing either surfaces as a red
error naming the def, rather than as a null reference from inside a pawn's think tree an hour
into the game.

### The worker classes

`ServiceWorker` is the pluggable behaviour behind a service. Three concrete classes ship, and a
new service usually needs no new code at all — just XML pointing at one of them.

| Class | Consumes stock | What it does |
| --- | --- | --- |
| `ServiceWorker_Ingest` | yes | Consumes one matching item off the display and resolves its effect through that item's own vanilla ingestion outcome. Filtered by `foodType` and/or `requireMeal`; scored against a `needHook` of `Food`, `Joy` or `None`. Ships parameterized twice: [drink](services.md#drink) (Liquor / Joy) and [meal](services.md#meal) (any meal / Food). |
| `ServiceWorker_Thought` | no | A bare "grant a thought" primitive. Deliberately does nothing else — reusable by any future stock-free service. |
| `ServiceWorker_Haircut` | no | `ServiceWorker_Thought` plus a visible hair change, using the same helper vanilla's own automatic styling uses. |
| `ServiceWorker_Wager` | no | Rolls a win/loss against the shop's own house edge, pays a winner straight out of the till (never more than it holds), and rolls a rowdiness/cheating-accusation outcome on a loss. The one worker whose `ApplyEffect` moves silver *out* rather than in — see [the wager](services.md#wager). |

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
| `Desirability(Pawn)` | If demand should vary by pawn state. **Floor it above zero** so a satisfied customer still occasionally buys. |
| `ApplyEffect(Pawn, Thing, int, out float)` | Always. The `Thing` is null for a stock-free service; the `int` is the price already charged, so a worker whose effect depends on the stake never has to recompute it; the `out float` is how much this round should nudge the customer's rowdiness — echo back `RowdinessPerUse` unless the outcome genuinely varies, the way `ServiceWorker_Wager` does. |

> Do not start a new job from inside `ApplyEffect`. It runs inside the service job's own toil,
> and starting a second job tears the current driver down mid-toil. Apply the effect directly, as
> `ServiceWorker_Ingest` does with `Thing.Ingested`.

## Add a coach tier

A rung of the [stagecoach line](economy.md#the-stagecoach-line)'s route ladder is pure data, the
same shape as a [business kind](#add-a-business-kind). Add it to
`Defs/CoachTierDefs/CoachTiers.xml`:

```xml
<OldWestTown.Stagecoach.CoachTierDef>
  <defName>OWT_RouteOvernightMail</defName>
  <label>overnight mail run</label>
  <minAppeal>2.5</minAppeal>
  <arrivalCeilingDays>3</arrivalCeilingDays>
  <purseMultiplier>1.8</purseMultiplier>
  <vipChance>0.12</vipChance>
</OldWestTown.Stagecoach.CoachTierDef>
```

That's the whole addition — **no building change is needed**. Any [coach
depot](buildings.md#coach-depot) already standing on a map picks up a new tier automatically once
the town's appeal reaches it: `CoachTierUtility.CurrentTier` reads the full set of loaded tiers
live on every check, never a fixed list baked into the depot itself.

**The fields.**

| Field | Type | Default | What it does |
| --- | --- | --- | --- |
| `minAppeal` | float | 0 | Appeal at or above which this tier can be the active one. |
| `arrivalCeilingDays` | float | 7 | Longest gap, in days at 1.0× Customer volume, this tier lets pass between arrivals of any kind — organic or scheduled — before forcing one. |
| `purseMultiplier` | float | 1.25 | Multiplies every ordinary customer's purse in a group this tier's ceiling forced into being. Inert for an organically-rolled group. |
| `vipChance` | float | 0 | Chance, once this tier forces an arrival, that one pawn in that group is a VIP carrying a much larger purse. The purse multiplier itself is a flat constant shared by every tier — see the [reference tables](reference.md). |

**Tuning notes.** Tiers don't have to be evenly spaced, and nothing requires exactly three of
them — the active tier is always whichever loaded `CoachTierDef` has the highest `minAppeal` at
or below current appeal, whatever else happens to be defined. A tier's own `arrivalCeilingDays`
only ever adds a firing attempt where the ordinary [arrival clock](economy.md#the-arrival-clock)
would otherwise have stayed quiet past it — see [a ceiling, not a second
clock](economy.md#a-ceiling-not-a-second-clock) for why that structurally can't double up with,
or land on top of, an organic arrival, however aggressively a tier gets tuned.

## Add a new kind of business entirely

If the new business isn't "sell an item" or "sell a service" — a rentable bed, a gambling table,
a bank — reuse the seam rather than routing around it:

- Keep the **shared-state** rule: the customer and the colonist read and write `CompBusiness`,
  never each other.
- Build the customer job on `JobDriver_PatronizeBusiness` so you inherit the walk/wait/patience/
  walkout shape and the [alert](customers.md#the-alert) for free.
- Move money through `ShopTransaction` and decide prices in `ShopPricing`.
- Implement `IBusinessPatron` on the driver so queue spacing and the alert see it.

The [roadmap](roadmap.md) sketches several of these.

## Bridging to another mod

The [Hospitality bridge](DESIGN.md#the-hospitality-bridge) (`Compat/`) is this mod's first soft
dependency on another mod, and the pattern is worth reusing rather than reinventing if a future
bridge needs the same shape: recognizing and lightly interacting with pawns another mod's own
`Lord` governs, without a hard reference to that mod's assembly.

- **No hard or `MayRequire`-gated reference, no compiled stub, no XML patch, no Harmony —
  reflection only.** A reference needs a second `.csproj` and a `loadFolders.xml` this mod has
  never shipped, for a guarantee an in-process boolean already gives for free. A stub typed
  against recalled signatures *looks* verified when it isn't. An XML patch has nothing to patch
  unless the other mod's own Defs genuinely need changing. Harmony is the one this mod has never
  taken on at all — see [the design notes](DESIGN.md#the-hospitality-bridge) for why the one
  thing it would buy here isn't worth its cost.
- **Detect the other mod by assembly simple name, once, behind a single cached bool.** A
  `private static readonly` field, initialized by a method that wraps the whole lookup in
  `try/catch` and returns `null` on any failure — see `HospitalityInterop.FindHospitalityAssembly`.
  C#'s own static-initialization guarantee gives "compute once, safely" for free; there is no
  separate `Init()` to remember to call.
- **Recognize the other mod's pawns structurally, never by a guessed type or member name.**
  `HospitalityInterop.IsHospitalityGuest` never calls a member the other mod declares — it only
  ever compares `System.Type.Assembly` against the assembly resolved above, on this mod's own
  already-proven vanilla API (`GetLord()`, `LordJob`, `AllComps`). A rename or restructure on the
  other side degrades this to "never matches," not a crash.
- **Act through a generic vanilla door, gated on idle, never a duty or an interrupt.**
  `HospitalityBridge` hands out a job with `Pawn_JobTracker.TryTakeOrderedJob` — the same
  mechanism a player's own forced order already uses — and only when the pawn's own
  `Pawn_MindState.IsIdle` is already true. It never touches the pawn's `Lord` or `PawnDuty`.
  That's what keeps a bridge from becoming a second version of
  [the one thing](architecture.md#the-one-rule) this mod's whole architecture exists to avoid —
  two pawn loops synchronizing with each other — now against a partner whose code can't even be
  inspected.
- **Ship a `[DebugAction]` that dumps what detection actually saw.** This mod's first one
  (`HospitalityInterop.LogDetectionState`) — the tool whoever eventually tests against a real
  copy of the other mod needs to correct the guesses above, without decompiling anything blind.
- **Say, in the wiki, exactly which facts are guesses and how confident each one is.** See
  [the code map's known risks](architecture.md#known-risks) for the shape this took for
  Hospitality — a bridge with no assembly to test against is only as trustworthy as its own
  honesty about what it couldn't check.

## Before you commit

```sh
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release   # rebuild the shipped assembly
python3 tools/validate_defs.py                                  # types, def refs, translation keys
python3 tools/make_textures.py --check                          # every building has art
python3 tools/validate_docs.py                                  # every def and texture is documented here
```

See [contributing](contributing.md) for what each of those actually checks.
