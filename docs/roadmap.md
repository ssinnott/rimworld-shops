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

**2. Services (no goods change hands) — done.** The interesting half of a town sells *time*,
not items. `CompShopCounter` is generalised into `CompBusiness` with a pluggable
`ServiceWorker`; the customer job becomes walk to the service point (skipped for a service
that consumes nothing), wait to be served, pay, receive a hediff or a thought instead of an
item — see "Services" above. Shipped: `_Ingest`, parameterized as both **Drink** (a Liquor item
off the saloon's own shelves, feeding Joy) and **Meal** (any meal, feeding Food) — the
interesting hybrid case, since a service that still moves stock has to answer to both the
goods loop and the service loop without double-counting; and `_Haircut` (a new **barber shop**
business, pure time, a mood thought plus a visible hair change). Bath and Doctor are left as
XML-only additions once their buildings exist — same seam, no new lesson to teach.

**3. Lodging.** A `CompRentableBed`: customers with no bed of their own pay per night and stay
across the day boundary. Requires extending the visit duration past one day and giving the
lord a "settled in town" state.

**4. Town roles.** Sheriff, barkeep, banker as assignable posts with their own work givers and
gizmos. A sheriff suppresses the drunk/brawl events a saloon starts generating.

**5. Reputation with depth.** Split the single reputation float into per-faction standing, so
specific factions become regulars. Feeds arrival frequency by faction.

**6. Old west content pass.** Boardwalk terrain, false-front facades, hitching posts, batwing
doors, faro tables, a gallows. Mostly XML; the point is that step 1–5 already make a town
*function*, and this makes it *look* like one.

**7. Hospitality bridge (optional).** A soft-dependency assembly that gives Hospitality guests
the `OWT_Shop` duty, so a single group can both lodge and shop.

## Beyond the staged plan — thematic expansions

Larger directions that build on the finished stages rather than slotting between them. Each is
listed with what it reuses, roughly cheapest first.

**Gambling hall.** A faro/poker table as the first business where the "transaction" is a wager
rather than a purchase: patrons buy in, and a player-set house edge (the markup slider's twin)
determines the expected take. Set it greedy and patrons lose fast, get angry, and reputation
drops; set it fair and they stay all evening buying drinks. Colonist dealers use the
Shopkeeping work type, with Social skill reducing cheating accusations. Mostly a step-2
`ServiceWorker` plus a payout roll — it reuses the wait-to-be-served toil and the till
wholesale, and adds the first income stream that isn't stock-driven.

**Outlaws and the law.** A rich town becomes a target: the more silver sitting in tills
(already tracked per counter), the higher the chance of a *stickup* — a small raider band that
heads for counters instead of colonists, empties tills, and leaves unless resisted.
Counterplay is the step-4 sheriff, plus a wanted board (bounty quests on recurring outlaw
leaders) and a jail that converts captured outlaws into silver or reputation. Turns "collect
the takings" from a chore into a real risk-management decision. New incident and lord job on
the existing shapes.

**Stagecoach line.** A coach depot that puts the town on a scheduled route: guaranteed
high-budget customers every few days, outgoing mail contracts (deliver parcels for silver),
and the occasional VIP passenger — a quest-giver or a shopper with a 5× budget. Appeal raises
the route's tier, from irregular freight wagons up to a daily express, giving the compounding
economy a visible milestone ladder on top of the shortening MTB clock.

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
becomes a ghost town you can salvage. The most ambitious of the five (it adds world-map
state), but the one that most directly deepens the pricing-and-appeal loop — it gives
`TownEconomy`'s single appeal float an external yardstick and makes pricing genuinely
competitive rather than solitaire.
