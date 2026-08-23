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
doesn't show. See [the recipe](art.md#the-recipe) for what each palette colour draws.

## Add a service

If an existing [worker class](services.md#the-worker-classes) covers the behaviour, a service is
two XML stanzas and no code.

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

### When it needs code

Write a new `ServiceWorker` subclass only when the *effect* is genuinely new. Override:

| Member | When |
| --- | --- |
| `ConsumesStock` | Return true if the service eats an item off the shelf. Changes pricing, appeal accounting and whether the customer fetches anything. |
| `CanUse(Thing)` | Required if `ConsumesStock` is true — which shelf items qualify. |
| `Desirability(Pawn)` | If demand should vary by pawn state. **Floor it above zero** so a satisfied customer still occasionally buys. |
| `ApplyEffect(Pawn, Thing)` | Always. The `Thing` is null for a stock-free service. |

> Do not start a new job from inside `ApplyEffect`. It runs inside the service job's own toil,
> and starting a second job tears the current driver down mid-toil. Apply the effect directly, as
> `ServiceWorker_Ingest` does with `Thing.Ingested`.

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

## Before you commit

```sh
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release   # rebuild the shipped assembly
python3 tools/validate_defs.py                                  # types, def refs, translation keys
python3 tools/make_textures.py --check                          # every building has art
python3 tools/validate_docs.py                                  # every def and texture is documented here
```

See [contributing](contributing.md) for what each of those actually checks.
