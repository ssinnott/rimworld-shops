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

`JobDriver_BuyFromShop` and `JobDriver_UseService` share this wait/patience/walkout shape from
a common base, `JobDriver_PatronizeBusiness` — see "Services" below. Everything added later (a
hotel clerk, a bank teller, a gambling dealer) should use the same shape.

## Component map

| Piece | File | Job |
| --- | --- | --- |
| `ShopKindDef` | `Shops/ShopKindDef.cs` | Data-driven business type: default stock, price band, appeal, patience, and which services it offers. Adding a business kind is XML, not code. |
| `CompBusiness` | `Shops/CompBusiness.cs` | Makes a building a business — goods, services, or both. Owns the till, filter, markup, ledger, staff flag, and the staff/customer cell pair. |
| `ServiceDef` / `ServiceWorker` | `Shops/ServiceDef.cs`, `Shops/ServiceWorker.cs` | A thing a business sells that isn't a shelf item — a drink, a meal, a haircut, a night's stay. The embedded worker supplies the type-specific behaviour: what it can act on, how much a customer wants it, what effect it applies once paid for — including, for a stay, a `Thing` it claims for longer than the sale itself. |
| `CompRentableBed` | `Shops/CompRentableBed.cs` | A bed a guest has paid to sleep in. Passive shared state, exactly like `CompBusiness`'s own staff flag — the guest's own sleep job is what notices it and acts on it, never a handshake with the desk that sold the stay. |
| `ShopStock` | `Shops/ShopStock.cs` | What's on the shelves, what a given customer would buy, which service a shop can currently perform, and which of its beds are free. |
| `ShopPricing` | `Shops/ShopPricing.cs` | The only place a price is decided — for goods or a service — so UI, AI and transaction can't disagree. |
| `ShopTransaction` | `Shops/ShopTransaction.cs` | The single point where silver, goods and service effects move. Re-validates everything. |
| `CompFalseFront` | `Shops/CompFalseFront.cs` | A false front's one mechanical hook: `CurbAppealBonus` folds a small, capped bonus for a nearby dressed-up storefront into `ShopPricing.ValueAppeal`. Walks `FalseFrontRegistry` rather than the map, since `ValueAppeal` is a customer-AI hot path. Nothing here is persisted. |
| `FalseFrontRegistry` | `Shops/FalseFrontRegistry.cs` | `MapComponent`. Live roster of spawned `CompFalseFront`s, registered the same way `TownEconomy` registers shops — so curb-appeal scoring never has to scan every Thing on the map. |
| `TownEconomy` | `Shops/TownEconomy.cs` | `MapComponent`. Shop register, daily ledger, reputation, and `Appeal`. |
| `JobGiver_BuyFromShop`, `JobDriver_PatronizeBusiness` (`JobDriver_BuyFromShop`, `JobDriver_UseService`) | `AI/` | Customer side: picks a business — goods or a service, whichever scores best — and runs the shared walk/wait/patience shape. |
| `JobGiver_SleepInRentedBed`, `JobDriver_SleepInRentedBed` | `AI/` | A checked-in guest's other job: go to bed once tired, sleep until rested. Never references the desk or colonist that sold the stay — only `CompRentableBed` and its own `CustomerRecord`. |
| `WorkGiver_/JobDriver_ManShop` | `AI/` | Colonist side. Kind-agnostic: it staffs any `CompBusiness` with something to offer, a hotel desk included. |
| `LordJob_ShopVisit`, `LordToil_Shop` | `Lords/` | The visiting group, and per-customer records — including, now, who's checked into a bed. |
| `IncidentWorker_ShopCustomers` | `Incidents/` | Turns town appeal into arrivals. |

### Why the lord graph is flat

Vanilla visitor groups run travel → chill → exit. This one is shopping → exit. A travel state
would fight the shopping AI: customers already have a specific place to walk to (the shop they
chose), so a `LordToil_Travel` duty would just drag them back to a chill spot between
purchases. The group state machine only handles *leaving* — because of time, or because
someone started shooting.

Per-customer state (budget spent, purchases, shops they've given up on) hangs off
`LordJob_ShopVisit` rather than the pawn. That means it saves and loads with the group, needs
no def patching of humanlike pawns, and disappears when the visit does.

### Why the sales floor is a room

A shop is defined by walls you already built, not by a zone you have to paint. It reads
naturally ("this room is the store"), it costs nothing to set up, and it makes the room-quality
stats you already care about matter commercially. Outdoors it falls back to a radius so market
stalls still work.

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
stock-free service (Haircut) skips straight to waiting. A small marker interface,
`IBusinessPatron`, lets `CompBusiness` recognize "a pawn is patronizing something" for
queue-spacing and the waiting-customers alert without the Shops layer ever depending on the AI
namespace.

A service business counts toward town appeal the same way a stocked one does
(`CompBusiness.HasAnythingToOffer`, `AvailableServices`), with one wrinkle: a stock-consuming
service's value is already counted once, as stock — `ServiceValue` only adds the services with
no `Thing` behind them at all, so a saloon's beer isn't counted twice for being sellable two
ways.

### Lodging: a service whose effect outlives the transaction

Renting a room (`OWT_Lodging`, worked by `ServiceWorker_Lodging`) is a service for exactly the
same reason a haircut is — no `Thing` changes hands, a colonist's time behind the desk is what's
being sold, and check-in reuses `JobDriver_UseService` / `ShopTransaction.TryServe` completely
unmodified. What makes it different from every service before it is that paying for it isn't the
whole experience: paying for a haircut *is* the haircut, but paying for a room is just the
booking — the stay happens later, unattended, quite possibly after the shopkeeper who sold it
has gone home for the night.

That gap is bridged by widening `ServiceWorker.ApplyEffect` to return a `Thing` a service has
claimed for longer than the sale itself. Every worker from before this stage returns `null`;
`ServiceWorker_Lodging` is the first to return something — the bed it just booked, found by
`ShopStock.ChooseVacantBed`, which generalizes the same room-or-radius traversal `ScanFor`
already used for sellable goods into "everything on this floor," filtered by type instead of by
stock rules. `JobDriver_UseService.CompleteService` is the one place a Shops-layer output
crosses into Lords-layer state: whatever got claimed, if it's a bed, goes straight onto the
guest's own `CustomerRecord.rentedBed`. Nothing else in the mod needs to know a stay is a
two-part transaction.

`CompRentableBed` is the passive comp that remembers the claim — it mirrors `CompBusiness`'s own
`lastShopkeeper` / `lastStaffedTick` pair on purpose: a plain fact for other code to read, never
a job, never a reservation. `JobGiver_SleepInRentedBed` and `JobDriver_SleepInRentedBed` are the
active side: a guest goes to bed once tired, and the driver re-checks every tick that the claim
is still theirs, using the bed's live occupancy (`Building_Bed.CurOccupants`) rather than
vanilla bed ownership — deliberately, so a colonist who simply climbs into the same bed is
caught with no assignment mechanism involved at all. No handshake exists anywhere in this: a bed
that's destroyed, a bed a colonist takes, and a guest harmed in a raid all end the sleep job the
same one way — the claim releases, a reputation cost lands if the stay was cut short, and the
group's own exit trigger notices the claim is gone. A claim that goes stale *before* the guest
ever starts sleeping (the bed destroyed while they're still out shopping) is cleared by
`JobGiver_SleepInRentedBed` itself rather than handed to the driver — creating a job whose very
first toil reads a despawned bed's position is a crash risk, not just a stale-claim one.

A stay is exactly one paid night per transaction. There is no multi-night booking and no
unstaffed nightly billing: a guest who wants another night simply queues and pays again once
awake, through the same purchase-repeat machinery every other service already gets for free.
That also means there is no "can't afford night two" failure mode to design around — it isn't
reachable.

### Settled in town: the day boundary without a second lord state

An overnight guest needs the group's visit to survive past its base duration, which is the one
genuinely load-bearing change: `LordJob_ShopVisit` needed a "settled in town" state distinct
from shopping. It turns out not to need a second `LordToil` — staffing, duties and the harmed
transition are all identical whether anyone's asleep or not, so a second toil would do nothing a
`Trigger` couldn't already decide by itself. Instead, the single existing toil is untouched; only
its exit condition changed. `Trigger_VisitComplete` replaces the flat `Trigger_TicksPassed` and
additionally requires every currently-owned pawn's `CustomerRecord.rentedBed` to be null. For a
group with nobody lodging — still the overwhelming majority of visits — this is bit-for-bit the
same trigger it replaces, since that condition is vacuously true from the first tick. New check-
ins are cut off once the group's base visit duration has elapsed (`PastCheckInCutoff`), and each
sleep job carries its own hard tick cap independent of `Need_Rest` ever reporting rested — between
the two, the trigger is always reachable in finite time. A departed or dead guest's own stale
claim is excluded from the check entirely (records are never removed from the lord, so a pawn
who died holding one would otherwise hold the whole group hostage), and `ServiceWorker_Lodging`
refuses to sell a room to a non-humanlike pawn at all, since nothing would ever make it tired
enough to check out.

This is also the first time a customer group has members doing genuinely different things at
once — one pawn asleep in a bed while another is still haggling over the price of a shirt.
Nothing about that needs the duty think tree to know: `JobGiver_SleepInRentedBed` simply runs
ahead of `JobGiver_BuyFromShop` in the same `OWT_Shop` duty every pawn in the group already
carries, and a pawn who isn't tired, or hasn't rented anywhere, falls through to the existing
logic completely unchanged.

## The economy loop

```
     player stocks + prices + staffs shops
                     │
                     ▼
         TownEconomy.Appeal  ◄──── reputation ◄──── served vs. walked-out customers
                     │                                        ▲
                     ▼                                        │
   IncidentWorker_ShopCustomers: how often, how many, how rich│
                     │                                        │
                     ▼                                        │
        customers arrive ──── buy goods or use a service ─────┘
                     │
                     ▼
                silver in the till
```

Appeal deliberately rewards **breadth over depth**: a second shop of a kind you already run is
worth 35% of the first. One giant general store should not out-earn a street with a store, a
saloon and a hotel. That's the pressure that turns a colony into a town.

Arrival frequency is the town's own doing, not the storyteller's. `TownEconomy` runs an MTB
clock that shortens as appeal grows (roughly one group every 3.5 days at the 0.5 threshold,
most days at high appeal) and fires the incident through the storyteller, so `minRefireDays`
still caps the rate. The `IncidentDef` keeps a small `baseChance` as a background trickle.

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

**3. Lodging — done.** A hotel is both a `ServiceWorker` (the priced, staffed check-in) and a
bed comp (`CompRentableBed`, the durable claim no single transaction can hold) — see "Lodging"
and "Settled in town" above for why it needs both and how the two stay out of each other's way.
Shipped: `OWT_Hotel`, a fourth business kind (desk + beds), staffed by the existing Shopkeeping
work type with zero new staffing code; one paid night per transaction, no multi-night
pre-booking; occupancy on the inspect string and the town ledger, in the same vocabulary as
everything else there; and a staged `OWT_SleptAtHotel` mood thought, keyed to the room's own
Impressiveness stat and granted on waking rather than at check-in — the only service in the mod
whose experience is deferred past payment. Deliberately deferred: multi-night pre-booking,
unstaffed nightly billing, per-desk room association (any hotel desk currently offers any vacant
bed on its own sales floor), vanilla bed ownership, and private suites.

**4. Town roles.** Sheriff, barkeep, banker as assignable posts with their own work givers and
gizmos. A sheriff suppresses the drunk/brawl events a saloon starts generating.

**5. Reputation with depth.** Split the single reputation float into per-faction standing, so
specific factions become regulars. Feeds arrival frequency by faction.

**6. Old west content pass — done.** Boardwalk terrain, false-front facades, a hitching post,
batwing doors, a faro table and a gallows dress the street stages 1–5 already made functional.
Only the false front is mechanical: `CompFalseFront.CurbAppealBonus` folds a small, capped
"curb appeal" bonus (+0.10 for one qualifying facade near a shop's customer-facing side, +0.15
for two or more — diminishing, and capped there) into `ShopPricing.ValueAppeal`, the same
function `JobGiver_BuyFromShop` already calls to score every candidate shop and service, so a
dressed-up storefront wins close calls between similarly-priced rivals without ever outweighing
price itself. Everything else earns its place for free through systems that already exist:
boardwalk, false front and faro table add Beauty like any floor or furniture (the gallows
subtracts it, on purpose — the one deliberately ugly thing in the set), the faro table's Beauty
comes with vanilla's own `CompGatherSpot` so idle colonists actually gather at it, and batwing
doors are a reskin of vanilla's `Door` that also undercuts its `costStuffCount` — the swinging
half-doors it's advertising as genuinely cost less lumber, not just less light-blocking. The
hitching post and the faro table's cards stay honestly decorative — a real wager is the
roadmap's own future Gambling Hall below, and biasing where a visiting group gathers would mean
editing the incident/lord-job files the Lodging stage is already extending.

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
  gracefully.
- `Appeal` walks every open shop's stock. It's cached per shop for a second, which is fine for
  a main street and would want revisiting for a hundred counters.
- Two vanilla calls the services path leans on — `FoodUtility.IngestFromInventoryNow` for
  Drink/Meal, and the `PawnStyleItemChooser.RandomHairFor` + `SetAllGraphicsDirty` pair for a
  Haircut's visible hair change — are exercised by this mod for the first time. Every signature
  involved is confirmed against the real 1.6 reference assembly, but the exact in-game outcome
  (whether a customer visibly gets `AlcoholHigh`, whether a hair change reliably repaints a
  transient visitor) hasn't been confirmed in a live game.
- Lodging is what makes `Need_Rest`, `Toils_LayDown.LayDown` and `Building_Bed` load-bearing for
  the first time. Every signature is confirmed against the reference assembly, but not what a
  non-colonist, lord-controlled pawn's rest gain actually looks like in play, or whether vanilla's
  own long-need rest-seeking (already enabled for this duty since stage 1) ever wins a race
  against `JobGiver_SleepInRentedBed` for a tired, housed guest and sends them to some other bed
  first. `JobDriver_SleepInRentedBed`'s own rested-threshold check and hard tick cap are the
  backstop either way, but expect to retune both after first play.
- `OWT_HotelBed`'s `statBases` (`Comfort`, `BedRestEffectiveness`, `RestRateMultiplier`) and
  `building` fields are plausible values, not ones checked against a real single bed def — this
  sandbox has no access to Core's Defs XML, only confirmation that the field *names* are real.
  Worth eyeballing against the player's own installed vanilla bed before shipping.
- `OWT_SleptAtHotel`'s three stage thresholds (room Impressiveness `< 20` / `< 60` / else) are a
  tuning guess with no reference point for what a bare bunkroom versus a lavish suite actually
  scores.
- A hotel desk reads "a customer is near" (`WorkGiver_ManShop.AnyCustomerNear`) for as long as
  *any* pawn on the `OWT_Shop` duty is nearby — including one who's currently asleep elsewhere in
  the same building. That's a pre-existing level of imprecision in that scan (it never checked
  whether a nearby customer wants *this* shop specifically), not a new correctness bug.
- Two hotel desks sharing one bunkroom can both offer the same vacant bed to two different guests
  in the same scoring pass; two guests' JobGivers can likewise both pick the same bed in the same
  tick. Both are the same class of race this file already accepts for stock — the pre-payment
  availability recheck in `ShopTransaction.TryServe` closes the paid-then-nothing version of it,
  and the loser's job fails gracefully with no refund.
- `OWT_BatwingDoor`'s `ParentName="Door"` assumes vanilla's own door `ThingDef` is genuinely
  named `Door` — extremely well-established modding knowledge, but unverifiable in this sandbox
  either way, since the reference assemblies carry compiled C# only, never Def XML. The faro
  table's `RimWorld.CompGatherSpot` is confirmed to exist and expose the expected members, but
  its actual pull on idle colonists is unconfirmed in a live game. The false front's curb-appeal
  numbers (+0.10 / +0.15, a 7-tile radius) are a first-pass estimate and want a playtest to
  confirm they nudge trade rather than doing nothing or dominating price.
