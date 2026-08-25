---
title: Home
summary: The wiki for Old West Town — every building, business, service and system in the mod.
---

# Old West Town

**Old West Town** is a RimWorld 1.6 mod that lets your colony run actual businesses: shop
counters with stock, prices and a till; colonists working behind them; and travellers who show
up with silver and spend it.

Hospitality gets guests onto your map. This is about what they do once they're there.

> **Status:** the whole staged plan is built, and so are every one of the thematic expansions on
> top of it — a gambling hall, outlaws and the law, a stagecoach line that puts the town on a
> scheduled route with its own visible tier ladder, a gold rush that floods the town with
> prospectors for a season then leaves it quiet again, and one or two rival towns competing for
> the same regional trade. A customer arrives, then either picks something off your shelves,
> orders a drink, a meal or a haircut, sits for a hand of cards, or checks into a room for the
> night — queues, and pays either way — and your own colonists can be sent to use a service too.
> A sheriff can be posted to keep rowdy saloon and gambling-hall patrons in line, and to slow how
> often an armed band comes to empty an unwatched till. If you also run Hospitality, an idle guest
> it's already housing can wander over and shop too — see [Hospitality
> guests](customers.md#hospitality-guests). See the [roadmap](roadmap.md) for how each piece got
> there.

This mod is standalone. It does not require Hospitality, and is written to sit alongside it.

## Start here

<ul class="cards">
  <li><a href="getting-started.html">Getting started</a>
    <p>Research it, build a counter, stock it, price it, staff it — the seven steps from nothing to a working shop.</p></li>
  <li><a href="buildings.html">Buildings</a>
    <p>The shop counter, saloon bar and barber chair: costs, stats, and how rotation decides which way a shop faces.</p></li>
  <li><a href="businesses.html">Business kinds</a>
    <p>General store, saloon, barber shop — what each one stocks by default, charges, and contributes to the town.</p></li>
  <li><a href="services.html">Services</a>
    <p>Drink, meal and haircut: businesses that sell time rather than goods.</p></li>
  <li><a href="art.html">Art gallery</a>
    <p>Every picture the mod ships, at every facing, with the colours behind each building.</p></li>
</ul>

## How the mod works

<ul class="cards">
  <li><a href="economy.html">The town economy</a>
    <p>Appeal, reputation, pricing and the arrival clock — the numbers that turn a shop into a town.</p></li>
  <li><a href="customers.html">Customers</a>
    <p>Where they come from, what they carry, how they choose a shop, and why they walk out.</p></li>
  <li><a href="shopkeeping.html">Shopkeeping</a>
    <p>The work type, when a counter asks for staff, and what self-service costs you.</p></li>
</ul>

## Working on the mod

<ul class="cards">
  <li><a href="architecture.html">Code map</a>
    <p>What every source file does, and the one rule the whole design follows.</p></li>
  <li><a href="extending.html">Adding content</a>
    <p>Adding a business, a service or a building — mostly XML, and here is the shape of it.</p></li>
  <li><a href="reference.html">Reference tables</a>
    <p>Internal names, tunable numbers and translation keys, for anyone editing the files.</p></li>
  <li><a href="contributing.html">Contributing</a>
    <p>Building the assembly, the static validators, CI, and how to keep this wiki honest.</p></li>
  <li><a href="changelog.html">Changelog</a>
    <p>What changed, when, and what it means for a save in progress.</p></li>
</ul>

## The shape of a game

The loop this mod adds is short enough to state in one paragraph. You build businesses and
decide what each one sells and charges. Distinct, stocked, open businesses raise the town's
**appeal**. Appeal drives how often customer groups arrive and how many are in them; how much
silver they carry is a separate question, answered by what you have on the shelves at market
value. Customers pick whichever reachable business best balances price, distance and having
somebody behind the counter, and join the line at it — a counter serves one at a time, in the
order they arrived, and a line too long to be worth waiting in sends them elsewhere. How you
treat them settles the town's **reputation** at midnight, one verdict per customer per day rather
than one per sale; a good name raises appeal again and puts a little on every price you charge,
while letting people walk out drags it down. The whole thing compounds, in either direction.

```
     you stock, price and staff your shops
                     │
                     ▼
              the town's appeal  ◄──── reputation ◄──── customers served vs. walked out
                     │                                            ▲
                     ▼                                            │
      how often groups arrive and how many are in them            │
                     │                                            │
                     ▼                                            │
        customers arrive ──── buy goods or use a service ─────────┘
                     │
                     ▼
                silver in the till
```

## Installing

Copy the repository into `RimWorld/Mods/OldWestTown` and enable it in the mod list. The mod ships
already compiled, so there is no build step and nothing else to install.
