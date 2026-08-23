---
title: Business kinds
summary: What a ShopKindDef controls, and the three that ship — general store, saloon, barber shop.
---

A **business kind** (`ShopKindDef`) is the data-driven half of a business. A building points at
one through its `CompProperties_Business`, and the kind supplies everything that makes a general
store behave differently from a saloon: what it stocks by default, what it may charge, how much
it draws customers to town, how patient those customers are, and which
[services](services.md) it offers.

Adding a business kind is XML, not code. See [adding content](extending.md#add-a-business-kind).

## What a kind controls

| Field | Type | Default | What it does |
| --- | --- | --- | --- |
| `defaultStockCategories` | list of `ThingCategoryDef` | empty | Categories switched on in a newly built counter's Stock filter. |
| `defaultStockThings` | list of `ThingDef` | empty | Individual defs switched on beyond those categories. |
| `defaultMarkup` | float | 1.35 | Markup a fresh counter starts at. |
| `markupRange` | `FloatRange` | 0.5~3.0 | The band the player's price slider may move within. |
| `appeal` | float | 1.0 | How much one open, stocked business of this kind adds to town [appeal](economy.md#appeal). |
| `customerNoun` | string | "customer" | The word the UI uses for this business's customers. |
| `customerPatienceTicks` | int | 2500 | How long a customer will wait at an unattended counter before [walking out](customers.md#walkouts). |
| `services` | list of `ServiceDef` | empty | [Services](services.md) this business offers alongside its stock. |

Two things are true of every kind regardless of its filter: **silver is never sellable**, and an
item that is forbidden, reserved by a colonist, on fire, or worth nothing is not on display.

## General store

`OWT_GeneralStore` — *"Dry goods, tools, cloth and provisions. The backbone of any frontier
town."*

| | |
| --- | --- |
| Building | [Shop counter](buildings.md#shop-counter) |
| Customers called | customers |
| Default markup | **135%**, adjustable 50%–300% |
| Appeal | 1.0 |
| Patience | 2500 ticks (~42 in-game seconds of waiting) |
| Services | none |
| Default stock | Foods, Manufactured, ResourcesRaw, Medicine, Apparel, Textiles, Leathers |

The broadest filter of the three, and the one that turns surplus production into silver. Because
appeal rewards [breadth over depth](economy.md#appeal), a second general store is worth only 35%
of the first — a street with a store, a saloon and a barber beats three stores.

## Saloon

`OWT_Saloon` — *"Drink and a hot meal. Patrons are less patient than shoppers and pay well for
the privilege."*

| | |
| --- | --- |
| Building | [Saloon bar](buildings.md#saloon-bar) |
| Customers called | patrons |
| Default markup | **180%**, adjustable 50%–400% |
| Appeal | 1.4 |
| Patience | **1500 ticks** — a thirsty patron will not stand at an empty bar for long |
| Services | [Drink](services.md#drink), [Meal](services.md#meal) |
| Default stock | Drugs, FoodMeals |

The highest-earning kind and the least forgiving. Its wider markup band (up to 400%) means a
well-run saloon can charge what a general store cannot, but its short patience makes an
unstaffed bar bleed reputation faster than any other business.

Its two services both consume stock, so a saloon that sells nothing but drink still needs liquor
on its shelves. See [the hybrid case](services.md#services-that-consume-stock).

## Barber shop

`OWT_Barber` — *"A chair, a mirror, and a steady hand. Customers pay for a haircut and a bit of
conversation."*

| | |
| --- | --- |
| Building | [Barber chair](buildings.md#barber-chair) |
| Customers called | customers |
| Default markup | **150%**, adjustable 50%–300% |
| Appeal | 1.1 |
| Patience | 2200 ticks |
| Services | [Haircut](services.md#haircut) |
| Default stock | **none** |

The only kind that stocks nothing. Its entire trade is one stock-free service, which makes it
the cheapest distinct business you can add to a town — no supply chain, just a colonist's time.
That also makes it the clearest demonstration of the appeal rule: adding a barber to a town of
general stores raises appeal more than another store would.
