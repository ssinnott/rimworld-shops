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
 colonist                       CompShopCounter                      customer
 ────────                       ───────────────                      ────────
 WorkGiver_ManShop              open / closed                        JobGiver_BuyFromShop
   ↓  picks a counter           markup                                 ↓  picks a counter + item
 JobDriver_ManShop              stock filter          ← reads →      JobDriver_BuyFromShop
   └─ every tick:                                                      ├─ walk to shelf
      NotifyStaffedBy(pawn) ──→ lastStaffedTick                        ├─ browse
                                     │                                 ├─ carry to counter
                                     └──────── Staffed ───────────────→├─ wait to be served
                                                                       └─ ShopTransaction.TrySell
                                                                              ↓
                                                                          till + ledger
```

Neither driver can strand the other. A shopkeeper who wanders off just flips `Staffed` to
false; the customer's wait toil notices, runs down its patience, drops the goods and leaves
annoyed. That failure is *legible to the player* — it's a message and a reputation hit — which
turns a robustness measure into a game mechanic.

Everything added later (a hotel clerk, a bank teller, a barber) should use the same shape.

## Component map

| Piece | File | Job |
| --- | --- | --- |
| `ShopKindDef` | `Shops/ShopKindDef.cs` | Data-driven business type: default stock, price band, appeal, patience. Adding a business kind is XML, not code. |
| `CompShopCounter` | `Shops/CompShopCounter.cs` | Makes a building a storefront. Owns the till, filter, markup, ledger, staff flag, and the staff/customer cell pair. |
| `ShopStock` | `Shops/ShopStock.cs` | What's on the shelves, and what a given customer would buy. |
| `ShopPricing` | `Shops/ShopPricing.cs` | The only place a price is decided, so UI, AI and transaction can't disagree. |
| `ShopTransaction` | `Shops/ShopTransaction.cs` | The single point where silver and goods move. Re-validates everything. |
| `TownEconomy` | `Shops/TownEconomy.cs` | `MapComponent`. Shop register, daily ledger, reputation, and `Appeal`. |
| `JobGiver_/JobDriver_BuyFromShop` | `AI/` | Customer side. |
| `WorkGiver_/JobDriver_ManShop` | `AI/` | Colonist side. |
| `LordJob_ShopVisit`, `LordToil_Shop` | `Lords/` | The visiting group, and per-customer records. |
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
              customers arrive ──── buy things ───────────────┘
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

**2. Services (no goods change hands).** The interesting half of a town sells *time*, not
items. Generalise `CompShopCounter` into a `CompBusiness` with a pluggable
`ServiceWorker` — `ServiceWorker_Drink`, `_Meal`, `_Room`, `_Haircut`, `_Bath`, `_Doctor`.
The customer job becomes: walk to the service point, wait to be served, pay, receive a hediff
or a thought instead of an item. The wait-to-be-served toil is already the shape this needs.

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
  the churn, but a hauler can still start a job on goods a customer is mid-walk toward — and
  two customers can race for the same stack. The loser's job fails gracefully.
- `Appeal` walks every open shop's stock. It's cached per shop for a second, which is fine for
  a main street and would want revisiting for a hundred counters.
