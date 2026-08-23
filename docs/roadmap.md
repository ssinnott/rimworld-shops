---
title: Roadmap
summary: What is built, what is next, and the larger directions that build on top of it.
---

The plan is staged so each step is playable on its own. Stages 1, 2, 3, 5 and 6 are shipped;
stages 4 and 7 are designed, not built.

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

**3. Lodging — done.** Rentable beds, sold by a new **hotel desk** business. A guest pays for the
night up front at the desk, keeps shopping, and only heads for whichever bed is free once they're
genuinely tired — there's no specific room nailed down at booking, just any vacant bed in the
same room as the desk. They sleep until rested and wake with a mood boost that scales with how
nice the room is. The rest of the group won't head home until every rented room is empty, which is
what lets a visit run past its usual length without a second lord state to manage it. Losing a
room early — the bed taken apart, or given to someone else by hand — costs the same reputation a
walkout does, with no refund. Deliberately left for later: booking several nights in advance,
unstaffed nightly billing, and a private suite tied to one specific desk.

**4. Town roles.** Sheriff, barkeep, banker as posts you assign a colonist to, each with its own
work and its own controls on the building. A sheriff suppresses the drunk/brawl events a saloon starts generating.

**5. Reputation with depth — done.** The town's one reputation number is unchanged, and still the
honest answer to "should anyone bother setting out for this town at all." Alongside it, each
faction now keeps its own **standing** with the town — untouched until that faction's own
customers are actually served or turned away. Treat one faction's customers well often enough and
they become **regulars**, showing up more than everybody else; mistreat them and they taper off,
without punishing anyone you haven't dealt with. The town ledger names your best and worst
relationship once either has genuinely pulled away from the town's own name. A faction you've
never dealt with specifically just reads as the town's reputation, so an existing save needs
nothing seeded for this to make sense on the very first load.

**6. Old west content pass — done.** Boardwalk terrain, false-front facades, a hitching post,
batwing doors, a faro table and a gallows — mostly content rather than code, dressing a street
that steps 1–5 already made *function*. The one exception is the **false front**: standing near a
shop, it gives that shop's prices a small, capped edge in how appealing they look to a passing
customer — enough to win a close call between two similarly-priced rivals, never enough to sell a
shop that's genuinely overpriced. The faro table deliberately does *not* gamble; a real wager
waits for the gambling hall below.

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
