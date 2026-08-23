---
title: Design notes
summary: The reasoning behind the mod's shape — the trade-offs considered, and why each one landed where it did.
---

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

## Where the parts live

The file-by-file component map moved to the wiki, so it sits next to the reference tables and
stays under CI's eye: **[Code map](architecture.md)**. That page says what each source file
owns; the sections below are the reasoning behind the shape it describes.

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

Moved to the wiki: **[Roadmap](roadmap.md)** — the staged plan, and the larger thematic
expansions that build on top of it.

## Known risks

Moved to the wiki, where they sit beside the code they apply to:
**[Code map → Known risks](architecture.md#known-risks)**.
