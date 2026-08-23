---
title: Roadmap
summary: What is built, what is next, and the larger directions that build on top of it.
---

The plan is staged so each step is playable on its own. Stages 1 and 2 are shipped; everything
below them is designed, not built.

For the reasoning behind the shipped stages, see the [design notes](DESIGN.md).

## Staged plan

**1. Vertical slice — done.** Counter, stock, pricing, till, shopkeeping work type, customer
arrival and purchase, appeal and reputation.

**2. Services — done.** The interesting half of a town sells *time*, not items. A counter became
a general "business" that can sell goods, services, or both: the customer walks up (fetching an
item first, if the service uses one), waits to be served, pays, and gets an effect instead of
something to carry home. Shipped: **drink** and **meal** at the saloon, both poured from its own
shelves — the interesting case, since a service that still moves stock has to count as both
without being counted twice — and the **barber shop**, which sells nothing but a colonist's time
and hands back a mood boost and a new hairstyle. A bath house and a doctor's office are already
possible without new code, and wait only on the buildings to put them in.

**3. Lodging.** Rentable beds: customers with no bed of their own pay per night and stay past
midnight. Needs visits that can run longer than a day, and travellers who count as settled in
town rather than passing through.

**4. Town roles.** Sheriff, barkeep, banker as posts you assign a colonist to, each with its own
work and its own controls on the building. A sheriff suppresses the drunk/brawl events a saloon starts generating.

**5. Reputation with depth.** Split the town's single reputation into standing with each faction,
so particular factions become regulars — and arrive more often than the rest.

**6. Old west content pass.** Boardwalk terrain, false-front facades, hitching posts, batwing
doors, faro tables, a gallows. Mostly content rather than code; the point is that steps 1–5
already make a town *function*, and this makes it *look* like one.

**7. Hospitality bridge (optional).** An optional add-on that sends Hospitality's guests shopping
too, so one group can both lodge and spend.

## Beyond the staged plan — thematic expansions

Larger directions that build on the finished stages rather than slotting between them. Each is
listed with what it reuses, roughly cheapest first.

**Gambling hall.** A faro/poker table as the first business where the "transaction" is a wager
rather than a purchase: patrons buy in, and a player-set house edge (the markup slider's twin)
determines the expected take. Set it greedy and patrons lose fast, get angry, and reputation
drops; set it fair and they stay all evening buying drinks. Colonist dealers work it through the
Shopkeeping work type, with Social skill reducing cheating accusations. Mechanically it is a
step-2 service plus a payout roll — it reuses the queueing and the till wholesale, and adds the
first income that isn't driven by stock.

**Outlaws and the law.** A rich town becomes a target: the more silver sitting in tills
(already tracked per counter), the higher the chance of a *stickup* — a small raider band that
heads for counters instead of colonists, empties tills, and leaves unless resisted.
Counterplay is the step-4 sheriff, plus a wanted board (bounty quests on recurring outlaw
leaders) and a jail that converts captured outlaws into silver or reputation. Turns "collect the
takings" from a chore into a real risk-management decision. A new event and a new kind of raid,
both built on shapes the mod already has.

**Stagecoach line.** A coach depot that puts the town on a scheduled route: guaranteed
high-budget customers every few days, outgoing mail contracts (deliver parcels for silver),
and the occasional VIP passenger — a quest-giver or a shopper with a 5× budget. Appeal raises the
route's tier, from irregular freight wagons up to a daily express, giving the compounding economy
a visible ladder of milestones on top of the quietly shortening arrival clock.

**Gold rush.** A map-wide *strike nearby* event that floods the town with prospectors for a
quadrum: arrivals triple and budgets rise, but they only want a specific demand basket (tools,
meals, booze, medicine) and they bring brawls and claim disputes. Price-gouging during the
boom decays reputation faster; when the vein dries up, arrivals crash below baseline until
reputation recovers. Exercises the markup slider and the breadth-over-depth appeal math
dramatically, and gives long saves a narrative arc.

**Rival towns.** One or two NPC towns as world-map neighbours with their own abstract appeal
score. Customer groups *choose* between towns — your share of regional traffic is your appeal
relative to theirs, so the arrival clock has an opponent. Rivals undercut prices, poach your
best shopkeeper with a job-offer event, or send saboteurs; out-compete one long enough and it
becomes a ghost town you can salvage. The most ambitious of the five, since it adds state to the world map,
but the one that most directly deepens the pricing-and-appeal loop — it gives your town's appeal
something to be measured against, and makes pricing genuinely competitive rather than
solitaire.
