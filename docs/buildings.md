---
title: Buildings
summary: The three things you can build, what they cost, and how one of them decides which way a shop faces.
---

Everything this mod adds to the build menu lives under the **Commerce** category, and is unlocked
by the [Frontier commerce](getting-started.md#before-anything-else) research.

All three buildings are the same piece of furniture underneath — same size, same cost, same way
of being placed — and differ only in what business they run.

## What they have in common

| Property | Value |
| --- | --- |
| Size | 2 × 1, rotatable |
| Blocks movement | Yes, but only half-height — it doesn't block sight |
| Materials | Wood, metal or stone — 75 units |
| Max hit points | 180 |
| Flammability | Burns readily |
| Inspect tab | **Stock** |
| Research | Frontier commerce |

Each one runs a [business kind](businesses.md), which is what decides how it trades.

### Rotation is the whole placement rule

A counter has a **staff** side and a **customer** side, facing each other across it. Rotating the
counter is how you choose which side of the room is "behind the counter" — there is no separate
zone or marker to paint.

```
        ▓▓▓▓▓▓▓▓▓▓▓▓  wall
        ░  back room ░      ← rotate so the staff side lands in here
        ░  [shopkeeper]░
        ██████████████      ← the counter
           [customer]
           sales floor      ← goods stored in this room are on display
```

If the customer side is blocked, customers fall back to any free tile beside the counter. When
several queue at once they fan out to free tiles within about 4 of it, keeping to the same room
indoors so a queue's tail doesn't end up outside the shop.

### The till

Silver a customer pays is held by the counter itself. It shows in the inspect pane, and the
**Collect takings** button drops it on the floor beside the counter for a hauler to pick up.
Deconstructing or destroying a counter drops the till rather than voiding it.

## Shop counter

*"A serving counter for a general store. Goods laid out in the same room are offered for sale
across it."*

| | |
| --- | --- |
| Business kind | [General store](businesses.md#general-store) |
| Work to build | 1400 |
| Beauty | 4 |
| Open-air sales radius | about 10 tiles |

The default business, and the one to build first. It sells goods only — no services.

## Saloon bar

*"A long bar with a brass rail. Drink and hot food stored in the same room are served across
it."*

| | |
| --- | --- |
| Business kind | [Saloon](businesses.md#saloon) |
| Work to build | 1800 |
| Beauty | 8 |
| Open-air sales radius | about 8 tiles |

Sells goods *and* two [services](services.md): a **drink** and a **meal**, both consumed off its
own shelves. Stock it with liquor and meals as well as (or instead of) general goods. Patrons
pay well — the saloon's default markup is 180% — but they are markedly less patient than
shoppers.

## Barber chair

*"A padded chair and a mirror. A colonist assigned to Shopkeeping will cut hair here for anyone
willing to wait and pay."*

| | |
| --- | --- |
| Business kind | [Barber shop](businesses.md#barber-shop) |
| Work to build | 1400 |
| Beauty | 4 |
| Open-air sales radius | about 10 tiles |

Needs **no stock at all**. Build one, staff it, and a passing customer with money to spare will
sit for a [haircut](services.md#haircut) — pure time, sold for silver. Because it has no stock to
consume, it is also the cheapest way to add a *distinct* business kind to the town and push
appeal up.

## What they look like

**[See every building in the art gallery](art.md)** — all three, at all four facings, with the
colours behind each one.

The art is deliberately flat and simple, in a shared frontier palette: blocks of oiled wood,
readable at RimWorld's zoom, consistent from one building to the next.
