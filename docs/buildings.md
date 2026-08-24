---
title: Buildings
summary: Everything you can build, what it costs, and how a counter decides which way a shop faces.
---

Everything this mod adds to the build menu lives under the **Commerce** category, and is unlocked
by the [Frontier commerce](getting-started.md#before-anything-else) research.

The shop counter, saloon bar, barber chair and hotel desk are the same piece of furniture
underneath — same size, same cost, same way of being placed — and differ only in what business
they run. The [faro table](#faro-table) is built the same way, but keeps its own lighter stats
and a silver cost on top, carried over from when it was pure street furniture — see below. The
hotel bed and the main-street furniture further down are their own shapes, built for their own
jobs.

## What the counters have in common

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

## Hotel desk

*"A front desk with a rack of room keys. A colonist assigned to Shopkeeping will check guests
into any vacant bed in the same room."*

| | |
| --- | --- |
| Business kind | [Hotel](businesses.md#hotel) |
| Work to build | 1400 |
| Beauty | 4 |
| Open-air sales radius | about 10 tiles |

Sells one [service](services.md#lodging): a night in whichever [hotel bed](#hotel-bed) is free.
The desk doesn't own a particular bed — put it in the same room as the beds you want it booking,
and it will hand out any vacant one on that sales floor. A room with a desk and no bed can't check
anyone in; a desk sees stock the same way any other counter does, so it needs an actual vacant
bed to have "something to offer" at all.

Its inspect pane adds a **Rooms** line — how many beds on its floor are vacant out of how many
total — right alongside the usual markup and till.

## Faro table

*"Green baize, a dealing box, and a case-keeper's abacus. The cards change hands for real money
now. A colonist assigned to Shopkeeping will deal hands to anyone willing to sit down and bet.
Stakes follow the price slider like anything else; the house edge decides how often the table
wins. Winnings come out of the till — an empty one can't pay a debt it doesn't have, and a table
that welches on a bet closes until you reopen it."*

| | |
| --- | --- |
| Business kind | [Gambling hall](businesses.md#gambling-hall) |
| Work to build | 900 |
| Beauty | 7 |
| Materials | Wood or stone — 70 units, plus **300 silver** |

Unlike every other counter here, a faro table is lighter to build and doesn't share the common
statistics above — it kept its own numbers from when it was purely decorative street furniture
(see [the roadmap](roadmap.md)), and turning it into a real business doesn't make the physical
object any heavier, just staffed. What's new is the **silver** in its cost: a fresh table needs a
bankroll of its own before its first customer ever sits down, so part of what you pay to build one
goes straight into its till rather than into lumber — see [the till as a
bankroll](economy.md#the-till-as-a-bankroll).

Sells one [service](services.md#wager): a hand of cards, priced and staffed exactly like any
other counter, with one dial nobody else has — the table's own **Set house edge** button, right
next to **Set prices**. See the [gambling hall](businesses.md#gambling-hall) for what it does.

## Hotel bed

*"A simple bed for a paying guest. Travellers with nowhere else to sleep rent one by the night at
the hotel desk, then sleep until morning or until the room is needed again."*

| | |
| --- | --- |
| Size | 1 × 2, rotatable |
| Materials | Wood, metal or stone — 50 units |
| Comfort / rest quality | The same as a decent vanilla bed |
| Research | Frontier commerce |

Not a business in its own right — it's the [hotel desk](#hotel-desk) that sells the stay, and a
bed does nothing until a desk shares its room. A guest who's checked in shows up on the bed's
inspect pane (*Occupied by so-and-so*, or *Vacant* between guests), alongside an **Evict guest**
button. Evicting drops the guest's booking immediately: they lose the room they already paid for,
there's no refund, and the town's [reputation](economy.md#reputation) takes the same hit as any
other walkout. A bed that's deconstructed while occupied, or one a colonist simply climbs into,
evicts its guest exactly the same way — nobody has to click anything for it to happen.

## Sheriff's office

*"A desk, a rifle rack, and a cell nobody's used yet. Doesn't sell anything — assign a colonist
to it from the building's own gizmo, the same way you'd assign an owner to a throne or a grave.
They also need a Sheriffing priority in the Work tab, same as any other job."*

| | |
| --- | --- |
| Size | 2 × 1, rotatable |
| Materials | Wood, metal or stone — 75 units |
| Work to build | 1400 |
| Beauty | 4 |
| Research | Frontier commerce |

Not a business — it sells nothing, and never enters the town's [appeal or
reputation](economy.md) math. It's a **post**: right-click it and **Assign sheriff** picks one
free colonist, the same idiom a throne room or a grave already uses for "this pawn, and only
this pawn, owns this." That's a separate step from putting them to work — the assigned colonist
also needs a priority on the **Sheriffing** work type in the Work tab before they actually patrol.
See [sheriffing](shopkeeping.md#sheriffing) for how the two combine, and
[trouble at the saloon and the gambling
hall](customers.md#trouble-at-the-saloon-and-the-gambling-hall) for what the sheriff is actually
suppressing.

Rotating the office places the **post** — the one tile the sheriff stands watch at — the same
"rotate the building to place it" idiom a shop counter's staff side already uses. The inspect
pane names the current sheriff and whether they're on duty right now (*Sheriff: so-and-so (on
duty)* / *(off duty)*), or *No sheriff assigned* if the post is vacant.

## Main street

*Boardwalk terrain, and four pieces of street furniture — hitching post, gallows, batwing doors,
and the false front that dresses up whatever it's bolted to.* All unlocked by the same Frontier
commerce research, all under the Commerce build category. The faro table used to live here too;
it's since been promoted into a real business — see [above](#faro-table).

Only the false front does anything mechanical. Everything else earns its keep by existing: it
adds (or, in one deliberate case, subtracts) Beauty like any other floor or furniture, the same
way a rug or a plant pot does.

### Boardwalk

*"Raised planking along the street, kept dry above the mud. Doesn't slow anyone down, doesn't
grow anything, and looks like the town intends to stay."*

A floor, not a building — lay it like any other terrain. It costs 2 wood a tile, carries no
movement penalty, and adds a couple of points of Beauty. A market stall standing on boardwalk
trades exactly like one standing on bare dirt; the only thing it changes is what the street looks
like underfoot.

### False front

*"A second storey painted on lath and studding, taller than the room behind it. Every
prosperous-looking storefront in this town is lying a little; a fresh coat and a tall sign make a
business look established before it is."*

| | |
| --- | --- |
| Size | 3 × 1 (drawn taller than the room it fronts) |
| Materials | Wood or stone — 90 units |
| Beauty | 10 |

The one piece of street dressing with a real effect: a false front standing within about 7 tiles
of a shop's customer-facing side gives that shop a small, capped edge in how appealing its prices
look to a passing customer — see [curb appeal](economy.md#curb-appeal). One qualifying facade is
worth something; a second is worth a little more; a street with one on every building is worth no
more than a street with two. It's enough to win a close call between similarly-priced rivals,
never enough to sell a shop that's genuinely overpriced.

### Hitching post

*"A rail and a couple of posts, worn smooth by reins. Doesn't do anything a colonist can point
to — it just makes the street look like somewhere people actually stop."* Purely decorative.

### Gallows

*"Cut lumber and a good rope. Nobody has been hanged from it and, with any luck, nobody will
be — but it sits there anyway, and everyone who sees it knows what it's for."* The one
deliberately ugly piece in the set: everything else here adds Beauty, the gallows subtracts it,
so putting one in a nice room costs you something on purpose.

### Batwing doors

*"Two waist-high slabs on spring hinges, cut low enough to see who's coming and high enough to
keep most of the weather out. Cheaper than a full door and just as willing to let anyone
through — which is rather the point in a saloon."*

A reskinned door, not a piece of furniture: it hangs in a doorway exactly like a vanilla door, and
genuinely costs less lumber than one — not just less light-blocking, less material.

## What they look like

**[See every building in the art gallery](art.md)** — every one of them, at all four facings, with
the colours behind each one.

The art is deliberately flat and simple, in a shared frontier palette: blocks of oiled wood,
readable at RimWorld's zoom, consistent from one building to the next.
