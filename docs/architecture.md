---
title: Code map
summary: What every source file does, and the one rule the whole design follows.
---

For the *why* behind these choices — the trade-offs considered and rejected — see the [design
notes](DESIGN.md). This page is the map: what exists, and where.

## The one rule

**Pawn loops never synchronise with each other.**

The obvious way to model a sale is a handshake: the customer picks a shopkeeper, the shopkeeper
accepts, both pawns run paired jobs. That is where this class of RimWorld mod bug comes from —
the shopkeeper gets drafted, breaks, gets shot, or simply finishes their shift, and the customer
is left in a job whose partner no longer exists.

Instead, the two loops only ever touch **shared state on the business**. Neither driver can
strand the other, and the failure mode — a customer waiting, running out of patience and leaving
— is [legible to the player](shopkeeping.md#what-staffed-means) rather than a stuck pawn.

Everything added later — a hotel clerk, a bank teller, a gambling dealer — should use the same
shape.

## Source layout

All under `Source/OldWestTown/`, namespace `OldWestTown`.

### `Shops/` — the business layer

| File | Type | Job |
| --- | --- | --- |
| `ShopKindDef.cs` | `ShopKindDef` | Data-driven [business type](businesses.md): default stock, price band, appeal, patience, services. Adding a kind is XML, not code. |
| `CompBusiness.cs` | `CompProperties_Business`, `IBusinessPatron`, `CompBusiness` | Makes a building a business. Owns the till, filter, markup, ledger, staff flag, and the staff/customer cell pair. The largest file in the mod, and the hub the two pawn loops meet at. |
| `ServiceDef.cs` | `ServiceDef` | A [thing a business sells](services.md) that isn't a shelf item. Validates its own required fields at load. |
| `ServiceWorker.cs` | `ServiceNeedHook`, `ServiceWorker`, `ServiceWorker_Ingest`, `ServiceWorker_Thought`, `ServiceWorker_Haircut`, `ServiceWorker_Lodging` | The pluggable behaviour behind a service: what it can act on, how much a customer wants it, what it does once paid for. `ServiceWorker_Lodging` is the first to widen `ApplyEffect`'s return into a `Thing` a service claims for longer than the sale itself — the [hotel bed](buildings.md#hotel-bed) it just booked. |
| `ShopStock.cs` | `ShopStock` | [What's on the shelves](economy.md#what-counts-as-stock), what a given customer would buy, and which item a service can currently consume. |
| `ShopPricing.cs` | `ShopPricing` | The only place a [price](economy.md#pricing) is decided, so UI, AI and transaction can't disagree. |
| `ShopTransaction.cs` | `ShopTransaction` | The single point where silver, goods and service effects move. Re-validates everything. |
| `CompRentableBed.cs` | `CompProperties_RentableBed`, `CompRentableBed` | A [hotel bed](buildings.md#hotel-bed) a guest has paid to sleep in for one night. Purely passive shared state, mirroring `CompBusiness`'s own staff flag on purpose — the guest's own sleep job is what notices it and acts on it, never a handshake with the desk that sold the stay. |
| `CompFalseFront.cs` | `CompProperties_FalseFront`, `CompFalseFront` | A [false front](buildings.md#false-front)'s one mechanical hook: `CurbAppealBonus` folds a small, capped bonus for a nearby dressed-up storefront into `ShopPricing.ValueAppeal`. Walks `FalseFrontRegistry` rather than the map, since `ValueAppeal` is a customer-AI hot path. Nothing here is persisted. |
| `FalseFrontRegistry.cs` | `FalseFrontRegistry` | `MapComponent`. Live roster of spawned `CompFalseFront`s, registered the same way `TownEconomy` registers shops — so curb-appeal scoring never has to scan every Thing on the map. |
| `TownEconomy.cs` | `TownEconomy` | `MapComponent`: business register, daily ledger, reputation, [appeal](economy.md#appeal), arrival clock, and — per faction — a sparse [standing](economy.md#standing-with-a-faction) dictionary that biases which faction's customers arrive next. |

### `AI/` — the pawn loops

| File | Type | Job |
| --- | --- | --- |
| `JobGiver_BuyFromShop.cs` | `JobGiver_BuyFromShop` | Customer side: scores every open business, goods and services on the same footing, and picks one. |
| `JobDriver_PatronizeBusiness.cs` | `JobDriver_PatronizeBusiness` | The shared "walk up, wait to be served, get served or walk out" shape, plus the patience and walkout logic. |
| `JobDriver_BuyFromShop.cs` | `JobDriver_BuyFromShop` | A goods purchase: fetch, browse, carry, wait, pay. |
| `JobDriver_UseService.cs` | `JobDriver_UseService` | A [service visit](services.md#how-a-service-visit-runs). Skips the fetch step for a stock-free service. |
| `WorkGiver_ManShop.cs` | `WorkGiver_ManShop` | Colonist side. Kind-agnostic: [staffs any business](shopkeeping.md#when-a-counter-asks-for-staff) with something to offer — a hotel desk included. |
| `JobDriver_ManShop.cs` | `JobDriver_ManShop` | Stands at the staff cell and pings `NotifyStaffedBy` every tick. |
| `JobGiver_SleepInRentedBed.cs` | `JobGiver_SleepInRentedBed` | A checked-in [guest](services.md#lodging)'s other job: sends them to the bed they paid for once they're actually tired. Runs ahead of `JobGiver_BuyFromShop` in the same `OWT_Shop` duty. |
| `JobDriver_SleepInRentedBed.cs` | `JobDriver_SleepInRentedBed` | Sleeps a guest until rested, or a hard tick cap. Never references the desk or colonist that sold the stay — only `CompRentableBed` and the guest's own `CustomerRecord`. |
| `WorkGiver_Patrol.cs` | `WorkGiver_Patrol` | Sends the assigned [sheriff](shopkeeping.md#sheriffing) to stand watch at their own office — the ambient half of suppression. Skips entirely if the town has no rowdiness-capable saloon to patrol for. |
| `JobDriver_Patrol.cs` | `JobDriver_Patrol` | Stands the post and pings `CompRolePost.NotifyOnDuty` every tick, mirroring `JobDriver_ManShop`. Also polls `TroubleUtility.AnyoneWorthCalming` and breaks the patrol off early when there's someone to calm. |
| `WorkGiver_CalmTrouble.cs` | `WorkGiver_CalmTrouble` | The reactive half: sends the assigned sheriff to one specific [rowdy patron](customers.md#trouble-at-the-saloon) rather than a building. |
| `JobDriver_CalmTrouble.cs` | `JobDriver_CalmTrouble` | Walks to the target and unilaterally zeroes their rowdiness. A no-op, not a false success, if the target's walked off mid-wait. |

### `Roles/` — town roles

| File | Type | Job |
| --- | --- | --- |
| `CompRolePost.cs` | `CompProperties_RolePost`, `CompRolePost` | A named post one specific colonist holds — the [sheriff's office](buildings.md#sheriffs-office)'s assignment, on `CompRolePost.OnDuty` other code reads. Built on vanilla's own `CompAssignableToPawn`, the same idiom a throne or a grave already uses. |
| `TroubleUtility.cs` | `TroubleUtility` | The saloon's one hook into town roles: bumps a customer's `OWT_Rowdy` hediff per drink served, applies the sheriff/shopkeeper suppression factors, and fires the scripted [disturbance](customers.md#trouble-at-the-saloon) when it tops out. |

### `Lords/`, `Incidents/`, `Alerts/`, `UI/`

| File | Type | Job |
| --- | --- | --- |
| `Lords/LordJob_ShopVisit.cs` | `CustomerRecord`, `LordJob_ShopVisit` | The [visiting group](customers.md#the-visit) and its per-customer records — including, now, who's checked into a bed. Deliberately a flat graph: shopping, then exit; `Trigger_VisitComplete` additionally waits for every rented bed to empty before the group can leave. |
| `Lords/LordToil_Shop.cs` | `LordToil_Shop` | Hands every group member the `OWT_Shop` duty. |
| `Incidents/IncidentWorker_ShopCustomers.cs` | `IncidentWorker_ShopCustomers` | Turns town appeal into [arrivals](customers.md#arrival), purses and group size, and — per faction standing — biases [which faction](customers.md#which-faction-turns-up) actually shows up. |
| `Alerts/Alert_CustomersWaiting.cs` | `Alert_CustomersWaiting` | Raised while customers burn patience at an unattended business. |
| `Alerts/Alert_RowdyPatrons.cs` | `Alert_RowdyPatrons` | Raised while a patron is "getting loud" and still calmable — the sheriff's real window before a disturbance fires unattended. Mirrors `Alert_CustomersWaiting`'s shape. |
| `UI/ITab_ShopStock.cs` | `ITab_ShopStock` | The Stock tab. Reuses vanilla's storage-filter widget, so it reads like a stockpile. |

### Root

| File | Type | Job |
| --- | --- | --- |
| `OldWestTownMod.cs` | `OldWestTownSettings`, `OldWestTownMod` | The three [mod settings](reference.md#mod-settings) and their window. |
| `OWTDefOf.cs` | `OWTDefOf` | Static def references. |
| `AssemblyInfo.cs` | — | Assembly metadata. |

## Boundaries worth keeping

**The `Shops` layer never depends on `AI`.** `CompBusiness` recognizes "a pawn is patronizing
something" through a small marker interface, `IBusinessPatron`, rather than by naming a concrete
driver type or `JobDef`. That is what lets queue-spacing and the waiting-customers alert work
without the business layer knowing the AI namespace exists.

**One price basis, one transaction point.** `ShopPricing` is the only thing that decides what
something costs; `ShopTransaction` is the only thing that moves silver. Both goods and services
go through them. A new business type that needs its own pricing rule should extend those, not
route around them.

**Re-validate at the point of exchange.** The walk from shelf to counter gives the world plenty
of time to invalidate whatever the customer decided a minute ago. `ShopTransaction` re-checks the
filter, the forbidden flag, the shop's open and staffed state, and the customer's purse before
anything changes hands.

## Data flow

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

## Known risks

- **None of this has run in RimWorld.** It compiles against the 1.6 reference assemblies and
  passes the [static checks](contributing.md#static-checks), but job drivers, lord graphs and
  duty think trees are exactly the code static checking can't validate. First-play bugs are
  expected.
- `CustomerCell` mirrors the interaction cell through the counter. For an unusually shaped or
  awkwardly placed counter this can pick a cell the player didn't intend. There's a fallback to
  any standable neighbour, and queueing customers fan out to free cells, but a dedicated
  "customer side" marker would still be better.
- **Customers can't reserve items against colonists** — RimWorld reservations are per-faction.
  Goods a colonist has already reserved are excluded from the shelves, which removes most of the
  churn, but a hauler can still start a job on goods a customer is mid-walk toward, and two
  customers can race for the same stack. The loser's job fails gracefully.
- `Appeal` walks every open business's stock. It's cached per business for a second, which is
  fine for a main street and would want revisiting for a hundred counters.
- Two vanilla calls the services path leans on — `Thing.Ingested` for drink and meal, and the
  `PawnStyleItemChooser.RandomHairFor` + `SetAllGraphicsDirty` pair for a haircut's visible hair
  change — are exercised by this mod for the first time. Every signature is confirmed against the
  real 1.6 reference assembly, but the exact in-game outcome hasn't been confirmed in a live game.
- **Lodging is what makes `Need_Rest`, `Toils_LayDown.LayDown` and `Building_Bed` load-bearing for
  the first time.** Every signature is confirmed against the reference assembly, but not what a
  non-colonist, lord-controlled pawn's rest gain actually looks like in play, or whether vanilla's
  own long-need rest-seeking (already enabled for this duty since stage 1) ever wins a race against
  `JobGiver_SleepInRentedBed` for a tired, housed guest and sends them to some other bed first.
  `JobDriver_SleepInRentedBed`'s own rested-threshold check and hard tick cap are the backstop
  either way, but expect to retune both after first play.
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
  in the same scoring pass; two guests' `JobGiver`s can likewise both pick the same bed in the same
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
- Whether `IncidentWorker_PawnsArrive.CandidateFactions`, called independently of its one
  presumed normal call site inside `TryResolveParms`, is genuinely side-effect-free can't be
  proven from reference-assembly metadata alone — there's no IL to inspect. `ChooseWeightedFaction`
  wraps the whole redraw in a silent try/catch that falls back to today's fully-random pick on
  any failure, and, more importantly, never depends on the separate, equally unprovable question
  of whether `TryResolveParms` would have honored a pre-set `parms.faction` — it only overwrites
  the field after that method has already run to completion.
- The per-faction standing deltas (±0.05 / ±0.10), the arrival-weight curve
  (`Lerp(0.15, 3)`), and the ledger's 0.1 divergence threshold are first-pass guesses, in the
  same spirit as this file's existing constants — untested in a live game. Worth a specific
  playtest: push one faction's standing to an extreme with repeated staffed sales and watch
  whether they visibly show up more (or less) often over the next several arrivals.
- A customer's faction can rarely turn hostile mid-visit — an unrelated relations swing while
  their group is still in town. `IsEligibleFaction` silently stops recording standing for them
  at that moment, which is a deliberate no-op matching this codebase's no-logging style, not a
  bug to "fix" on a later read.
- **`OWT_CalmDownPatron`'s higher `priorityInType` does not preempt a running patrol.**
  RimWorld's `priorityInType` only decides which `WorkGiver` wins once a pawn is jobless again —
  it has no power to interrupt a toil already running with `ToilCompleteMode.Never`. That's why
  `JobDriver_Patrol` itself polls `TroubleUtility.AnyoneWorthCalming` every 30 ticks and ends
  itself with `JobCondition.InterruptForced` the moment there's someone to calm, rather than
  relying on the WorkGiver priority order alone. Anyone touching either WorkGiver's priority
  should know the polling loop, not the number, is what actually makes the handoff happen.
- **The rowdiness numbers are first-pass guesses, untested in a live game** — `rowdinessPerServing`
  (0.2 per drink), the hediff's own −0.5/day decay, the 0.5/1.0 stage thresholds, and both
  suppression factors (`SheriffOnDutyFactor` and `MaxShopkeeperSocialFactor`, both 0.5). Whether a
  saloon actually reaches "spoiling for a fight" at a sane pace, and whether an on-duty sheriff or
  a skilled barkeep visibly changes that pace, wants a specific playtest, in the same spirit as
  the false front's curb-appeal numbers above.
