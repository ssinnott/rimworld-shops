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
| `ServiceWorker.cs` | `ServiceNeedHook`, `ServiceWorker`, `ServiceWorker_Ingest`, `ServiceWorker_Thought`, `ServiceWorker_Haircut` | The pluggable behaviour behind a service: what it can act on, how much a customer wants it, what it does once paid for. |
| `ShopStock.cs` | `ShopStock` | [What's on the shelves](economy.md#what-counts-as-stock), what a given customer would buy, and which item a service can currently consume. |
| `ShopPricing.cs` | `ShopPricing` | The only place a [price](economy.md#pricing) is decided, so UI, AI and transaction can't disagree. |
| `ShopTransaction.cs` | `ShopTransaction` | The single point where silver, goods and service effects move. Re-validates everything. |
| `TownEconomy.cs` | `TownEconomy` | `MapComponent`: business register, daily ledger, reputation, [appeal](economy.md#appeal), arrival clock. |

### `AI/` — the pawn loops

| File | Type | Job |
| --- | --- | --- |
| `JobGiver_BuyFromShop.cs` | `JobGiver_BuyFromShop` | Customer side: scores every open business, goods and services on the same footing, and picks one. |
| `JobDriver_PatronizeBusiness.cs` | `JobDriver_PatronizeBusiness` | The shared "walk up, wait to be served, get served or walk out" shape, plus the patience and walkout logic. |
| `JobDriver_BuyFromShop.cs` | `JobDriver_BuyFromShop` | A goods purchase: fetch, browse, carry, wait, pay. |
| `JobDriver_UseService.cs` | `JobDriver_UseService` | A [service visit](services.md#how-a-service-visit-runs). Skips the fetch step for a stock-free service. |
| `WorkGiver_ManShop.cs` | `WorkGiver_ManShop` | Colonist side. Kind-agnostic: [staffs any business](shopkeeping.md#when-a-counter-asks-for-staff) with something to offer. |
| `JobDriver_ManShop.cs` | `JobDriver_ManShop` | Stands at the staff cell and pings `NotifyStaffedBy` every tick. Re-scans for customers every 30 ticks rather than every one. |
| `JobDriver_ColonistUseService.cs` | `JobDriver_ColonistUseService` | The colony's own side of a counter. Waits on the same staffing flag a stranger does and receives the same effect, and names no price, till, ledger or lord — so [a colonist](services.md#your-own-colonists) can never reach the town's books. |

### `Lords/`, `Incidents/`, `Alerts/`, `UI/`

| File | Type | Job |
| --- | --- | --- |
| `Lords/LordJob_ShopVisit.cs` | `CustomerRecord`, `LordJob_ShopVisit` | The [visiting group](customers.md#the-visit) and its per-customer records. Deliberately a flat graph: shopping, then exit. |
| `Lords/LordToil_Shop.cs` | `LordToil_Shop` | Hands every group member the `OWT_Shop` duty. |
| `Lords/LordToil_CloseUp.cs` | `LordToil_CloseUp` | Closing time. Sends the group home the way vanilla's exit toil does, except for a customer whose sale is being worked that moment. |
| `Incidents/IncidentWorker_ShopCustomers.cs` | `IncidentWorker_ShopCustomers` | Turns town appeal into [arrivals](customers.md#arrival), purses and group size. |
| `Alerts/Alert_CustomersWaiting.cs` | `Alert_CustomersWaiting` | Raised while customers burn patience at an unattended business. |
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
