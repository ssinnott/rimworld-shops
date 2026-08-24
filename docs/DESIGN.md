# Old West Town — design notes

## The goal

Hospitality answers "guests are on my map, now what?" with *hospitality*: beds, comfort,
relations. This mod answers it with *commerce*: your colony is a town on a trade road, and the
buildings you put up are businesses that strangers walk into and spend money in.

The target end state is a main street — general store, saloon, hotel, bank, barber, stable,
sheriff's office — where each business is a thing colonists staff, customers use, and the
player prices and stocks.

## The one decision everything else follows from

**Pawn loops never synchronise with each other.**

The obvious way to model a sale is a handshake: the customer picks a shopkeeper, the
shopkeeper accepts, both pawns run paired jobs. That design is where multiplayer-style
mod bugs come from in RimWorld — the shopkeeper gets drafted, breaks, gets shot, or simply
finishes their shift, and the customer is left in a job whose partner no longer exists.

Instead, the two loops only ever touch shared state on the counter:

```
 colonist                       CompBusiness                         customer
 ────────                       ────────────                         ────────
 WorkGiver_ManShop              open / closed                        JobGiver_BuyFromShop
   ↓  picks a business          markup                                 ↓  picks goods or a service
 JobDriver_ManShop              stock filter, services   ← reads →   JobDriver_BuyFromShop /
   └─ every tick:                                                    JobDriver_UseService
      NotifyStaffedBy(pawn) ──→ lastStaffedTick                        ├─ walk to shelf (goods/
                                     │                                 │  a stock-consuming service)
                                     └──────── Staffed ───────────────→├─ wait to be served
                                                                       └─ ShopTransaction.TrySell /
                                                                          TryServe
                                                                              ↓
                                                                    till, ledger, hediff/thought
```

Neither driver can strand the other. A shopkeeper who wanders off just flips `Staffed` to
false; the customer's wait toil notices, runs down its patience, drops whatever they're
carrying (if anything) and leaves annoyed. That failure is *legible to the player* — it's a
message and a reputation hit — which turns a robustness measure into a game mechanic.

A walkout ends the sale, not the relationship. The customer stops queueing at that counter, and
the refusal still dies with the visit like the rest of their record — but what *lifts* it is
neither the visit running out nor a timer: it is the counter being worked again. That is what
makes the alert honest. The one thing it asks the player for, a colonist behind the counter, has
to be the thing that lifts the refusal, or the alert is asking for a reaction that can no longer
work. Time was rejected as the key precisely because it isn't something the player did: a
cooldown marches the same customer back to the same empty counter to walk out and pay for it
again, turning the alert into a slow drain. For the same reason the dispatch side reads staffing
without the grace window the serve loop allows — a keeper who left a second ago is not somebody
to walk across town for. Nothing is refunded either: the reputation and the sale stay lost, so
serving customers before their patience runs out remains strictly better than winning them back
afterwards. The same scoping is what un-pins the shopkeeper — a refusal that holds only while the
counter is empty is one a colonist arriving actually resolves, instead of leaving them posted
while ex-customers who can never buy anything drift past inside the scan radius.

`JobDriver_BuyFromShop` and `JobDriver_UseService` share this wait/patience/walkout shape from
a common base, `JobDriver_PatronizeBusiness` — see "Services" below. Everything added later (a
hotel clerk, a bank teller, a gambling dealer) should use the same shape.

## Component map

| Piece | File | Job |
| --- | --- | --- |
| `ShopKindDef` | `Shops/ShopKindDef.cs` | Data-driven business type: default stock, price band, appeal, patience, and which services it offers. Adding a business kind is XML, not code. |
| `CompBusiness` | `Shops/CompBusiness.cs` | Makes a building a business — goods, services, or both. Owns the till, filter, markup, ledger, staff flag, and the staff/customer cell pair. |
| `ServiceDef` / `ServiceWorker` | `Shops/ServiceDef.cs`, `Shops/ServiceWorker.cs` | A thing a business sells that isn't a shelf item — a drink, a meal, a haircut. The embedded worker supplies the type-specific behaviour: what it can act on, how much a customer wants it, what effect it applies once paid for. |
| `ShopStock` | `Shops/ShopStock.cs` | What's on the shelves, what a given customer would buy, which service a shop can currently perform, and whether a service has anything to work with, asked without a roll. |
| `ShopPricing` | `Shops/ShopPricing.cs` | The only place a price is decided — for goods or a service — so the inspect pane, AI and transaction can't disagree. `MaxAffordable` settles a purse against `PriceFor` itself, so the order the AI sizes is always one the counter can charge for. |
| `ShopTransaction` | `Shops/ShopTransaction.cs` | The single point where silver, goods and service effects move. Re-validates everything. |
| `TownEconomy` | `Shops/TownEconomy.cs` | `MapComponent`. Shop register, daily ledger, reputation, and `Appeal` — surveys the town every 60 ticks. |
| `JobGiver_BuyFromShop`, `JobDriver_PatronizeBusiness` (`JobDriver_BuyFromShop`, `JobDriver_UseService`) | `AI/` | Customer side: picks a business — goods or a service, whichever scores best — and runs the shared walk/wait/patience shape. |
| `WorkGiver_/JobDriver_ManShop` | `AI/` | Colonist side. Kind-agnostic: it staffs any `CompBusiness` with something to offer. |
| `LordJob_ShopVisit`, `LordToil_Shop`, `LordToil_CloseUp` | `Lords/` | The visiting group, the per-customer records, and what closing time does to a customer part-way through a purchase. |
| `IncidentWorker_ShopCustomers` | `Incidents/` | Turns town appeal into arrivals. |

### Why the lord graph is flat

Vanilla visitor groups run travel → chill → exit. This one is shopping → exit. A travel state
would fight the shopping AI: customers already have a specific place to walk to (the shop they
chose), so a `LordToil_Travel` duty would just drag them back to a chill spot between
purchases. The group state machine only handles *leaving* — because of time, or because
someone started shooting.

Per-customer state (budget spent, purchases, counters they've given up on for as long as those
stay unattended) hangs off `LordJob_ShopVisit` rather than the pawn. That means it saves and
loads with the group, needs no def patching of humanlike pawns, and disappears when the visit
does.

### Closing time

The visit clock is the only clock here that can end something the player is in the middle of, so
it is the only one that has to decide what "in the middle of" is worth. Vanilla's exit toil takes
an `interruptCurrentJob` flag and, set, ends every pawn's job the instant the group is sent home.
That is right for a group that has to be gone and wrong for a shop: it tears up the sale at the
counter one tick from the till, drops the goods on the floor, and throws away however long a
colonist had already spent on it — worst at the barber's, whose 2200-tick haircut is the longest
serve in the mod and the whole of its shop's patience window.

`LordToil_CloseUp` keeps that interrupt for everyone except the customer being served. It hands
out the same exit duty, then ends the same jobs itself, skipping any customer whose transaction is
being worked that tick. Closing is "the town stops taking new customers", not "the town empties":
the barber finishes the head in the chair, and does not start another. Keeping the interrupt for
everybody else is not incidental — customers are allowed to eat, drink and sleep between
purchases, and a sleeping visitor left to wake up on its own is a group that never leaves.

Nothing about closing is written down. A customer sent home was not served and did not give up: no
sale, no walkout, no row in the day's patron table, no weight in the nightly verdict. A walkout has
to stay a thing the player did — a counter left unstaffed for a whole patience window, with the
alert up and the time to answer it — and charging the same halved verdict because the hours ran out
would make it a tax on a clock the player cannot see, levied hardest on the counters the alert was
already pointing at. That is also why the grace has a floor under it. A spared customer is holding
a job that never completes on its own and does not yield to a duty, so a shopkeeper who walked away
would drop them back into the wait branch to run their patience down to exactly that walkout. Their
own end condition — the group has been called home and nobody is serving me — is what prevents it,
and it is what bounds the whole exemption. What it bounds is per customer, not per town: a serve
advances for everyone whose transaction is running at that counter, so a bar with three at it
spares all three. And "being served" means the same thing at closing as it does at noon — with the
honesty-box setting on, a counter with nobody behind it counts, so such a sale runs itself out in
its own 180 ticks and lands as the self-served half-mark it would have been at any other hour.
Each spared customer is bounded by one serve, and a keeper who walks away ends theirs inside the
staffing grace.

The grace runs the other way once. A customer who gave up at some counter earlier carries a halved
verdict all day; if the last thing that happens to them is being served out at closing, the sale
lands and their verdict is a half rather than a nought. Standing a counter at closing time is the
one thing that can partly repair a bad day.

Violence is still the exception, with one edge worth naming: the harm transition leaves the
shopping state, so once the clock has already sent the group home it cannot fire again. A raid that
starts during business hours ends every job including a serve — `TransitionAction_EndAllJobs` never
consults the exemption — while one that starts after closing leaves the last serve running until
the colonist stops working it, which drops the customer within a tick or two of the staffing grace.

The shopkeeper's side needed no change at all, which is the sign the seam is in the right place.
The customer scan already counts a pawn patronizing *this* counter whatever their purse or their
duty says — it was written for a serve outliving the shopping duty, and until now that was the one
case it could never actually see — so the colonist stays posted while the last customer is served
out and knocks off on the same idle timer as always. Neither pawn waits on the other.

Under fire none of this applies: the violence transition still ends every job after the duty swap,
mid-serve included.

### Why the sales floor is a room

A shop is defined by walls you already built, not by a zone you have to paint. It reads
naturally ("this room is the store"), it costs nothing to set up, and it makes the room-quality
stats you already care about matter commercially. Outdoors it falls back to a radius so market
stalls still work. Two counters in one room are two tills on one shop-front: the room's goods
count once, and only the first counter of a kind earns that kind's draw.

### Services: the same seam, without a `Thing` changing hands

A service (a drink, a meal, a haircut) is priced and served through the identical seam as a
sale — there just isn't a `Thing` changing hands at the end of it, or, for a haircut, no
`Thing` involved at all. `ShopKindDef.services` is a list of `ServiceDef`s alongside its stock
categories. Each `ServiceDef` embeds one `ServiceWorker` — the same "instance carries its own
XML fields" idiom the customer duty's think tree already uses for `DutyDef.thinkNode` — which
decides what it can act on (`CanUse`), how much a given customer wants it right now
(`Desirability`, weighed against a need like Food or Joy), and what happens once it's paid for
(`ApplyEffect`: a hediff via vanilla's own ingestion outcome, a thought, a visible hair change).

Two core worker classes cover the three services this stage ships, plus a three-line subclass
for Haircut's visual flourish. `ServiceWorker_Ingest` is one class parameterized two ways —
Drink wants `FoodTypeFlags.Liquor`, Meal wants `IngestibleProperties.IsMeal` — because a drink
and a meal are the same mechanic (consume a matching item already on the shelf, then let
`FoodUtility.IngestFromInventoryNow` do what vanilla already does for anyone eating from their
inventory) with different filters and a different need behind the demand curve.
`ServiceWorker_Thought` is a bare "grant a thought" primitive; `ServiceWorker_Haircut` adds a
visible hair change on top of it, because a business that changes nothing visible is a weaker
proof that a service happened.

Every `ServiceDef` gets its own `JobDef` rather than sharing one — `Verse.AI.Job` has no
generic slot to carry a `Def` reference, so a service driver has no other reliable way to
recover which service it's running. The cost is one small XML stanza per service; the
alternative is a driver that has to guess.

The customer side factors the shared "walk up, wait to be served, get served or walk out" shape
into `JobDriver_PatronizeBusiness`, with `JobDriver_BuyFromShop` and `JobDriver_UseService` as
its two concrete shapes — a goods sale (or a stock-consuming service) fetches an item first; a
stock-free service (Haircut) skips straight to waiting. A small business-facing view of a patron,
`IBusinessPatron`, lets code outside the AI namespace ask the two things about a patron that are
not its own business to work out — are they waiting, and is their transaction running — for
queue-spacing, the waiting-customers alert and closing time, without the Shops layer ever depending
on the AI namespace.

A service business counts toward town appeal the same way a stocked one does
(`CompBusiness.HasAnythingToOffer`, `AvailableServices`), with one wrinkle: a stock-consuming
service's value is already counted once, as stock — the town's survey only adds services with
no `Thing` behind them at all, so a saloon's beer isn't counted twice for being sellable two
ways.

## The economy loop

```
     player stocks + prices + staffs shops
                     │
                     ▼
         TownEconomy.Appeal  ◄──── reputation ◄──── one verdict per customer, settled nightly
                     │                                        ▲
                     ▼                                        │
   IncidentWorker_ShopCustomers: how often, how many ◄ appeal │
                             how rich ◄ goods on offer        │
                     │                                        │
                     ▼                                        │
        customers arrive ──── buy goods or use a service ─────┘
                     │
                     ▼
                silver in the till
```

Appeal is the compounding-investment number, and it measures one thing: what a traveller would
find here. Three terms multiplied — the businesses, the goods on offer, the town's standing —
which is how the ledger shows it, because a number the player is meant to grow has to say which
lever moves it.

The unit of a business is a **sales floor**, not a counter. Walls already define a shop (see "Why
the sales floor is a room"); a second counter in the same room is a second till, and what it buys
is serving two customers at once, not a second reason to come to town. Goods are counted the same
way — every stack on sale counts once, however many counters can see it. Before, a shared room was
added once per counter: a second counter in the general store was worth +91% appeal for nothing.
A counter of a *different* kind sharing a floor does still earn its kind's draw — a bar in the
corner of the trading post genuinely offers a drink where there was none — but the goods under it
are counted once, so what it adds is the kind, never the shelves.

Breadth still beats depth, and now at every scale. A kind the town does not have is worth its full
draw; each further front of a kind you already run is worth 35% of *the one before it*. The flat
"35% of the first, forever" it replaces summed without limit — five general stores out-earned the
shipped three-kind main street, inverting the rule it was written to enforce. Geometric decay
converges: a kind is worth at most 1.54x its first front however many you build, so no amount of
one trade catches a street with three, and depth still pays honestly through the goods term.

Goods count at **market value, not the shelf price**. Appeal is what the town offers; markup
decides what it asks. Pricing appeal through `ShopPricing` swung a lone store's appeal by 2.45x
across the markup slider's range — gouging drew more and richer customers as well as taking more
from each — and being generous could switch the town's trade off entirely by dropping it under the
0.5 threshold. Price still decides which shop a customer walks into: that is
`ShopPricing.ValueAppeal`, settled at the counter, which is the right altitude for it.

### Two numbers, not one

How *often* travellers come and how *rich* they are are different questions and now read
different numbers. Appeal — breadth, goods, standing — drives the arrival clock and the size of
the group. The goods term alone drives what arrivals carry: three trades do not fatten a purse
and a good name does not either, but a rack of rifles does. Scaling purses off appeal meant every
town above appeal 4 sat pinned at the top of the range, so the investment the number was supposed
to teach became invisible exactly when the player started making it. Both are market value, so
neither can be bought with the markup slider.

### Appeal is surveyed, not computed on demand

The expensive, map-walking half is something the town does every 60 ticks; what is left is a
square root and a lerp, cheap enough for a counter's inspect pane to draw every frame. On demand
it re-walked and re-priced the whole town per rendered frame while a counter was selected — and
rolled the shared seeded game stream while doing it, because it asked "is there stock for this
service" through the picker that rolls a tie-break, so whether a counter was selected changed what
the storyteller did next. The existence question is now answered without a roll, and the picker
can no longer be called without a customer to roll for. A survey rather than a cache because a
cache would need an invalidation contract — stock, walls, room merges, markup, opening and closing
— and hauling a crate into a store fires none of those; the survey is authoritative by definition,
the same way reputation is authoritative because it settles at midnight.

Arrival frequency is the town's own doing, not the storyteller's. `TownEconomy` runs an MTB
clock that shortens as appeal grows (roughly one group every 3.5 days at the 0.5 threshold,
most days at high appeal) and fires the incident through the storyteller, so `minRefireDays`
still caps the rate. The `IncidentDef` keeps a small `baseChance` as a background trickle.

Reputation is a record of *service*, not of sales. Each caller leaves exactly one verdict a day
however many times they opened their purse — full marks for being served by somebody, half for
helping themselves off an unwatched counter, and whatever they were owed is halved again if they
gave up waiting anywhere in town. The day's average is blended into the town's name at midnight,
weighted by how many people the day actually heard from, so one traveller giving up on a dead
Tuesday is bad luck rather than a scandal. Counting receipts instead made reputation a function
of how much stock was on the shelves: one group of four crossed the whole range in an afternoon,
after which both of the things that read reputation were reading a constant. No per-sale constant
survives that, however it is tuned, because the granularity is the defect and not the size of the
step. A day with no custom at all drifts back toward neutral instead: a town nobody trades with is
forgotten, not hated — and that branch is also what stops a ruined name being a trap, since bad
standing thins the crowd and a thin crowd stops producing walkouts.

The direction is the obvious one and was, for a long time, backwards in the code: a good name
buys higher prices and more footfall, while a town with a bad one has to discount to move goods.
That discount is deliberately the way back rather than a second punishment — a cheaper shelf
stretches a purse, so the few customers a poor town still draws buy more of what is on it, and
each one served in person earns the name back. (It does *not* work by making the town look better
value: the factor is town-wide, so it scales every shop's `ValueAppeal` alike and cancels out of
the comparison a customer actually makes between them.) The two consequences are not the same
size. The price band is ±10% while the arrival term runs 0.5x–1.5x, so what a good name mostly
buys is *who comes*, not what they pay. Folding reputation into the price at all is only
defensible because the number now moves once a night rather than on every sale in town: a price
can still shift under a customer mid-walk when a visit spans midnight, but no longer because
somebody else bought a beer while they were choosing.

## Roadmap

Staged so each step is playable on its own.

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

### Beyond the staged plan — thematic expansions

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

## Known risks

- **None of this has run in RimWorld.** It compiles against the 1.6 reference assemblies and
  passes `tools/validate_defs.py`, but job drivers, lord graphs and duty think trees are
  exactly the code that static checking can't validate. First-play bugs are expected.
- `CustomerCell` mirrors the interaction cell through the counter. For an unusually shaped or
  awkwardly placed counter this can pick a cell the player didn't intend; there's a fallback to
  any standable neighbour, and queueing customers fan out to free cells around it
  (`CustomerCellFor`), but a dedicated "customer side" marker would still be better.
- Customers can't reserve items against colonists (RimWorld reservations are per-faction).
  Goods a colonist has already reserved are excluded from the shelves, which removes most of
  the churn, but a hauler can still start a job on goods (or a service's consumable) a customer
  is mid-walk toward — and two customers can race for the same stack. The loser's job fails
  gracefully: it ends on the goto/carry fail conditions, before the counter. A customer whose
  purse comes up short *at* the counter — the markup slider moved while they walked, or their
  visit spanned the midnight reputation roll — remembers "this kind of goods, at this counter,
  this visit", so it costs them one trip instead of repeating. It is not a walkout and costs no
  reputation. The other refusals need no memory: goods pulled from the filter or forbidden simply
  leave the shelf scan until the player puts them back.
- Staffing a counter lifts every refusal standing against it at once, so a colonist who takes the
  post and then leaves for a full patience window can cost a second walkout from the same
  customer. It is bounded rather than prevented: the customer is only ever dispatched to a counter
  somebody is standing at *that tick*, the wait toil resets on any staffed tick after that, so the
  colonist has to be continuously absent for a whole window, and each further walkout needs a
  further staffing episode the player caused. Capping it would mean scribed per-(customer,
  counter) state, which is a worse trade than an outcome the player can see themselves producing.
  At the town level a second walkout from the same customer on the same day now costs nothing
  extra, because the ledger records the disappointed customer rather than the number of times they
  were disappointed. The per-shop count and the walkout message still fire, so the player still
  sees it happen.
- A customer who has spent their last silver, or who is asleep, no longer holds a colonist at a
  counter — but nothing sends them home either. They keep the shopping duty and wander the town
  centre until the visit clock runs out. Letting a spent-out customer leave early is a lord-graph
  change, and belongs in its own commit.
- The town's survey (`TownEconomy.TakeStock`) re-reads every sales floor and prices every
  stack. It runs once every 60 ticks, and it is the only thing that takes that snapshot, so
  appeal and the shelves can lag the world by up to a second of game time but never disagree
  with each other. While the game is paused nothing surveys at all, so a filter edit refreshes
  its own shop's shelves directly rather than waiting for a tick that will not come. It
  everything that wants appeal reads the two numbers it recorded. This is a new steady cost:
  before, a shop nobody was looking at or shopping at never scanned its room. Fine for a main
  street; worth revisiting at a hundred counters.
- Appeal fell about 18% and customer purses were moved onto the goods term, so a stocked
  three-kind street draws groups about 18% smaller carrying about 31% less each. Both changes are
  deliberate — the old goods term counted the player's own markup, and the old purse scale read
  2.2 for every town above appeal 4, which is every functioning town — but neither has been
  played. The knobs are `BasePurse` and `MinPurseFactor`, not the appeal terms.
- Two vanilla calls the services path leans on — `FoodUtility.IngestFromInventoryNow` for
  Drink/Meal, and the `PawnStyleItemChooser.RandomHairFor` + `SetAllGraphicsDirty` pair for a
  Haircut's visible hair change — are exercised by this mod for the first time. Every signature
  involved is confirmed against the real 1.6 reference assembly, but the exact in-game outcome
  (whether a customer visibly gets `AlcoholHigh`, whether a hair change reliably repaints a
  transient visitor) hasn't been confirmed in a live game.
- Closing lets every customer whose transaction is already running outlive the group's "heading
  home" line while they are served out — a whole queue at one counter, and up to a haircut's 2200
  ticks each — so the lord and its per-customer records live that much longer than the message
  suggests. Each one is bounded by its own serve and by the counter staying staffed (or, with the
  honesty box on, by the 180 ticks a self-served sale takes), but it has not been watched in a
  live game.
