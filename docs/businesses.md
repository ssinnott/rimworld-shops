---
title: Business kinds
summary: What a business kind decides, and the five that ship — general store, saloon, barber shop, hotel, gambling hall.
---

A **business kind** is what a counter *is*: a general store, a saloon, a barber shop, a hotel, a
gambling hall. The building is just furniture; the kind supplies everything that makes one behave
differently from another — what it stocks by default, what it may charge, how much it draws
customers to town, how patient those customers are, and which [services](services.md) it offers.

Business kinds are data rather than code, so adding one is a matter of editing files rather than
writing any. See [adding content](extending.md#add-a-business-kind).

## What a kind decides

| | What it means |
| --- | --- |
| **Default stock** | Which categories and items are switched on in a newly built counter's Stock tab. You can change any of it. |
| **Default markup** | The price a fresh counter starts at, as a percentage of market value. |
| **Markup range** | How far the price slider will move in either direction. |
| **Appeal** | How much one open, stocked business of this kind adds to town [appeal](economy.md#appeal). |
| **What its customers are called** | "Customers" at a store, "patrons" at a saloon. Cosmetic. |
| **Patience** | How long a customer waits at an unattended counter before [walking out](customers.md#walkouts). |
| **Services** | The [services](services.md) it offers alongside its stock, if any. |

Two things are true of every kind regardless of its Stock tab: **silver is never sellable**, and
an item that is forbidden, reserved by a colonist, on fire, or worth nothing is not on display.

## General store

*"Dry goods, tools, cloth and provisions. The backbone of any frontier town."*

| | |
| --- | --- |
| Building | [Shop counter](buildings.md#shop-counter) |
| Customers called | customers |
| Default markup | **135%**, adjustable 50%–300% |
| Appeal | 1.0 |
| Patience | about an in-game hour |
| Services | none |
| Default stock | Foods, manufactured goods, raw resources, medicine, apparel, textiles, leathers |

The broadest selection of the three, and the one that turns surplus production into silver.
Because appeal rewards [breadth over depth](economy.md#appeal), a second general store is worth
only 35% of the first — a street with a store, a saloon and a barber beats three stores.

## Saloon

*"Drink and a hot meal. Patrons are less patient than shoppers and pay well for the privilege."*

| | |
| --- | --- |
| Building | [Saloon bar](buildings.md#saloon-bar) |
| Customers called | patrons |
| Default markup | **180%**, adjustable 50%–400% |
| Appeal | 1.4 |
| Patience | **about 35 in-game minutes** — a thirsty patron will not stand at an empty bar for long |
| Services | [Drink](services.md#drink), [Meal](services.md#meal) |
| Default stock | Drugs (which is where liquor lives), cooked meals |

The highest-earning kind and the least forgiving. Its wider markup range (up to 400%) means a
well-run saloon can charge what a general store cannot, but its short patience makes an
unstaffed bar bleed reputation faster than any other business.

Both its services are served off its own shelves, so a saloon that sells nothing but drink still
needs liquor in stock. See [services that use up stock](services.md#services-that-consume-stock).

## Barber shop

*"A chair, a mirror, and a steady hand. Customers pay for a haircut and a bit of conversation."*

| | |
| --- | --- |
| Building | [Barber chair](buildings.md#barber-chair) |
| Customers called | customers |
| Default markup | **150%**, adjustable 50%–300% |
| Appeal | 1.1 |
| Patience | about 50 in-game minutes |
| Services | [Haircut](services.md#haircut) |
| Default stock | **none** |

The only kind that stocks nothing. Its entire trade is one service that uses up no goods, which
makes it the cheapest distinct business you can add to a town — no supply chain, just a
colonist's time. That also makes it the clearest demonstration of the appeal rule: adding a
barber to a town of general stores raises appeal more than another store would.

## Hotel

*"A front desk and a row of rented beds. Travellers with nowhere else to sleep pay by the
night."*

| | |
| --- | --- |
| Building | [Hotel desk](buildings.md#hotel-desk) |
| Customers called | guests |
| Default markup | **160%**, adjustable 50%–350% |
| Appeal | 1.3 |
| Patience | **just over an hour** — the most patient of the five kinds, nearly twice a saloon patron's |
| Services | [Lodging](services.md#lodging) |
| Default stock | none — it sells nothing off a shelf |

Like the barber shop, a hotel stocks nothing of its own; unlike every other kind, what it sells
isn't consumed the moment it's paid for. A guest checks in and pays at the desk, then keeps
shopping and only heads for a [hotel bed](buildings.md#hotel-bed) once they're actually tired —
see [lodging](services.md#lodging) for the rest of how a stay plays out. A hotel desk needs a
vacant bed somewhere on its own sales floor to have anything to sell at all.

## Gambling hall

*"A faro table and a house that always has an edge. Gamblers come to bet, not to browse — what
they leave with is down to the cards and the house's own nerve."*

| | |
| --- | --- |
| Building | [Faro table](buildings.md#faro-table) |
| Customers called | gamblers |
| Default markup | **100%**, adjustable 50%–300% |
| Appeal | 1.3 |
| Patience | about 40 in-game minutes |
| Services | [Wager](services.md#wager) |
| Default stock | none — it sells nothing off a shelf |

Like the barber shop and the hotel, a gambling hall stocks nothing; its entire trade is one
service. What makes it different from every other kind is that the service is a bet, not a
purchase — the customer can walk away with *more* silver than they sat down with, not less. See
[the wager](services.md#wager) for how a hand actually resolves.

A gambling hall carries a second dial alongside its markup: **house edge**, set from the table's
own **Set house edge** button, the same slider idiom as **Set prices** and living right next to
it in the counter's gizmo row. Markup sets the stakes — it prices a hand exactly like a haircut,
through the same value-times-markup-times-reputation formula every other price in the mod uses.
House edge sets the odds: it is, by construction, the fraction of every silver wagered the house
keeps on average, nothing more and nothing less. A low edge keeps a table fair and its gamblers
staying all evening; a greedy one wins the house more per hand but pays out less often, which
burns through patience — and through the till's own bankroll — much faster too. See [the till as a
bankroll](economy.md#the-till-as-a-bankroll) for the numbers behind both.
