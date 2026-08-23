---
title: Home
summary: The wiki for Old West Town — every building, business, service and system in the mod.
---

# Old West Town

**Old West Town** is a RimWorld 1.6 mod that lets your colony run actual businesses: shop
counters with stock, prices and a till; colonists working behind them; and travellers who show
up with silver and spend it.

Hospitality gets guests onto your map. This is about what they do once they're there.

> **Status:** goods and services both work. A customer arrives, then either picks something off
> your shelves or orders a drink, a meal or a haircut — queues, and pays either way. The breadth
> (hotels, banks, stables, town roles) is designed but not built. See the [roadmap](roadmap.md).

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
</ul>

## How the mod works

<ul class="cards">
  <li><a href="economy.html">The town economy</a>
    <p>Appeal, reputation, pricing and the arrival clock — the numbers that turn a shop into a town.</p></li>
  <li><a href="customers.html">Customers</a>
    <p>Where they come from, what they carry, how they choose a shop, and why they walk out.</p></li>
  <li><a href="shopkeeping.html">Shopkeeping</a>
    <p>The work type, when a counter asks for staff, and what self-service costs you.</p></li>
  <li><a href="reference.html">Reference tables</a>
    <p>Every defName, tunable number, mod setting and translation key in one place.</p></li>
</ul>

## Working on the mod

<ul class="cards">
  <li><a href="architecture.html">Code map</a>
    <p>What every source file does, and the one rule the whole design follows.</p></li>
  <li><a href="extending.html">Adding content</a>
    <p>Adding a business, a service or a building — mostly XML, and here is the shape of it.</p></li>
  <li><a href="contributing.html">Contributing</a>
    <p>Building the assembly, the static validators, CI, and how to keep this wiki honest.</p></li>
  <li><a href="changelog.html">Changelog</a>
    <p>What changed, when, and what it means for a save in progress.</p></li>
</ul>

## The shape of a game

The loop this mod adds is short enough to state in one paragraph. You build businesses and
decide what each one sells and charges. Distinct, stocked, open businesses raise the town's
**appeal**. Appeal drives how often customer groups arrive and how much silver they carry.
Customers pick a business — the cheapest reachable one that has something they want — queue at
it, and pay if somebody is behind the counter. Serving them raises the town's **reputation**,
which raises appeal again and lets you charge more; letting them walk out lowers it. The whole
thing compounds, in either direction.

```
     player stocks + prices + staffs shops
                     │
                     ▼
             TownEconomy.Appeal  ◄──── reputation ◄──── served vs. walked-out customers
                     │                                            ▲
                     ▼                                            │
   IncidentWorker_ShopCustomers: how often, how many, how rich    │
                     │                                            │
                     ▼                                            │
        customers arrive ──── buy goods or use a service ─────────┘
                     │
                     ▼
                silver in the till
```

## Installing

Copy the repository into `RimWorld/Mods/OldWestTown` and enable it in the mod list. The compiled
assembly is committed at `1.6/Assemblies/OldWestTown.dll`, so no build step is needed to play.
