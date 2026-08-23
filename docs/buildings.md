---
title: Buildings
summary: The three things you can build, what they cost, and how one of them decides which way a shop faces.
---

Everything in this mod's build menu lives under the **Commerce** designation category
(`OWT_Commerce`, sort order 410) and is unlocked by the [Frontier
commerce](getting-started.md#before-anything-else) research.

All three buildings share one abstract parent, `OWT_CounterBase`, so they behave identically as
furniture and differ only in what business they run.

## What they have in common

| Property | Value |
| --- | --- |
| Size | 2 × 1, rotatable |
| Passability | Impassable, 50% fill, path cost 50 |
| Interaction cell | `(0,0,-1)` — the **staff** side |
| Stuff | Woody, Metallic or Stony — 75 units |
| Max hit points | 180 |
| Flammability | 1.0 |
| Inspector tab | **Stock** (`ITab_ShopStock`) |
| Research | `OWT_FrontierCommerce` |

Every one of them carries a `CompProperties_Business` pointing at a [business
kind](businesses.md), which is what actually makes it trade.

### Rotation is the whole placement rule

A counter has one interaction cell, and that cell is where the **shopkeeper** stands. The
**customer** cell is that cell mirrored through the counter, so the two face each other across
it. Rotating the counter is therefore how you choose which side of the room is "behind the
counter" — there is no separate zone or marker to paint.

```
        ▓▓▓▓▓▓▓▓▓▓▓▓  wall
        ░  back room ░      ← rotate so the interaction cell lands in here
        ░  [shopkeeper]░
        ██████████████      ← the counter
           [customer]
           sales floor      ← goods stored in this room are on display
```

If the mirrored cell is blocked, the customer falls back to any standable neighbour of the
counter. When several customers queue at once they fan out to free cells within about 4 tiles,
filtered to the same room indoors so a queue's tail doesn't end up outside the shop.

### The till

Silver a customer pays goes into a container owned by the building's `CompBusiness`, not into
the world. It shows in the inspect pane, and the **Collect takings** gizmo drops it on the floor
beside the counter for a hauler to pick up. Deconstructing or destroying a counter drops the
till rather than voiding it.

## Shop counter

`OWT_ShopCounter` — *"A serving counter for a general store. Goods laid out in the same room are
offered for sale across it."*

| | |
| --- | --- |
| Business kind | [General store](businesses.md#general-store) |
| Work to build | 1400 |
| Beauty | 4 |
| Open-air sales radius | 9.9 |

The default business, and the one to build first. It sells goods only — no services.

## Saloon bar

`OWT_SaloonBar` — *"A long bar with a brass rail. Drink and hot food stored in the same room are
served across it."*

| | |
| --- | --- |
| Business kind | [Saloon](businesses.md#saloon) |
| Work to build | 1800 |
| Beauty | 8 |
| Open-air sales radius | 7.9 |

Sells goods *and* two [services](services.md): a **drink** and a **meal**, both consumed off its
own shelves. Stock it with liquor and meals as well as (or instead of) general goods. Patrons
pay well — the saloon's default markup is 180% — but they are markedly less patient than
shoppers.

## Barber chair

`OWT_BarberChair` — *"A padded chair and a mirror. A colonist assigned to Shopkeeping will cut
hair here for anyone willing to wait and pay."*

| | |
| --- | --- |
| Business kind | [Barber shop](businesses.md#barber-shop) |
| Work to build | 1400 |
| Beauty | 4 |
| Open-air sales radius | default (9.9) |

Needs **no stock at all**. Build one, staff it, and a passing customer with money to spare will
sit for a [haircut](services.md#haircut) — pure time, sold for silver. Because it has no stock to
consume, it is also the cheapest way to add a *distinct* business kind to the town and push
appeal up.

## Textures

The art is deliberately flat programmer art in a shared frontier palette, drawn from one table
by `tools/make_textures.py`. Adding a building is a row in that table rather than an art task,
and [CI fails](contributing.md#continuous-integration) if a building in the table has no texture
on disk. Each building uses `Graphic_Multi`, so it needs four files — `_north`, `_south`, `_east`,
`_west` — under `Textures/Things/Building/Commerce/`.
