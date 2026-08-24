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
| `ServiceWorker.cs` | `ServiceNeedHook`, `ServiceWorker`, `ServiceWorker_Ingest`, `ServiceWorker_Thought`, `ServiceWorker_Haircut`, `ServiceWorker_Lodging`, `ServiceWorker_Wager` | The pluggable behaviour behind a service: what it can act on, how much a customer wants it, what it does once paid for. `ServiceWorker_Lodging` is the first to widen `ApplyEffect`'s return into a `Thing` a service claims for longer than the sale itself — the [hotel bed](buildings.md#hotel-bed) it just booked. `ServiceWorker_Wager` is the first whose "sale" is a bet: `ApplyEffect` now also takes the price already paid and hands back an outcome-dependent rowdiness, both threaded through the widened `ShopTransaction.TryServe`. |
| `ShopStock.cs` | `ShopStock` | [What's on the shelves](economy.md#what-counts-as-stock), what a given customer would buy, and which item a service can currently consume. |
| `ShopPricing.cs` | `ShopPricing` | The only place a [price](economy.md#pricing) is decided, so UI, AI and transaction can't disagree. |
| `ShopTransaction.cs` | `ShopTransaction` | The single point where silver, goods and service effects move. Re-validates everything. `PayOutFromTill` is the one place money leaves a till instead of entering it — a wager's payout, built on `CompBusiness.TakeFromTill`, which structurally can never hand back more than the till holds. |
| `CompRentableBed.cs` | `CompProperties_RentableBed`, `CompRentableBed` | A [hotel bed](buildings.md#hotel-bed) a guest has paid to sleep in for one night. Purely passive shared state, mirroring `CompBusiness`'s own staff flag on purpose — the guest's own sleep job is what notices it and acts on it, never a handshake with the desk that sold the stay. |
| `CompFalseFront.cs` | `CompProperties_FalseFront`, `CompFalseFront` | A [false front](buildings.md#false-front)'s one mechanical hook: `CurbAppealBonus` folds a small, capped bonus for a nearby dressed-up storefront into `ShopPricing.ValueAppeal`. Walks `FalseFrontRegistry` rather than the map, since `ValueAppeal` is a customer-AI hot path. Nothing here is persisted. |
| `FalseFrontRegistry.cs` | `FalseFrontRegistry` | `MapComponent`. Live roster of spawned `CompFalseFront`s, registered the same way `TownEconomy` registers shops — so curb-appeal scoring never has to scan every Thing on the map. |
| `TownEconomy.cs` | `TownEconomy` | `MapComponent`: business register, daily ledger, reputation, [appeal](economy.md#appeal), arrival clock, and — per faction — a sparse [standing](economy.md#standing-with-a-faction) dictionary that biases which faction's customers arrive next. `RecordShortfall` is the worst single-event reputation and standing hit the mod has — a gambling hall unable to pay out a win. |
| `StickupWatch.cs` | `StickupWatch` | `MapComponent`: sums every registered business's `TillSilver`, open or closed, into the [stickup](outlaws.md) clock — an MTB roll that shortens as uncollected silver climbs, halved in frequency by an on-duty sheriff, firing `OWT_Stickup` through the storyteller the same way `TownEconomy` fires its own arrival incident. Read-only against `TownEconomy`; carries no persisted state of its own. |
| `TownEconomy.cs` | `TownEconomy` | `MapComponent`: business register, daily ledger, reputation, [appeal](economy.md#appeal), arrival clock, and — per faction — a sparse [standing](economy.md#standing-with-a-faction) dictionary that biases which faction's customers arrive next. `RecordShortfall` is the worst single-event reputation and standing hit the mod has — a gambling hall unable to pay out a win. Also owns the [stagecoach line](economy.md#the-stagecoach-line)'s guarantee clock — `RouteTier`, `TicksSinceLastArrival`, `GuaranteedArrivalDue`, `NotifyArrival` — an OR folded into the existing MTB roll below, not a second, independent clock. |

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
| `WorkGiver_Patrol.cs` | `WorkGiver_Patrol` | Sends the assigned [sheriff](shopkeeping.md#sheriffing) to stand watch at their own office — the ambient half of suppression. Skips entirely if the town has no rowdiness-capable business to patrol for — a saloon or a gambling hall, gated on `ServiceWorker.CanCauseTrouble` rather than a fixed `RowdinessPerUse`, since a wager's rowdiness is outcome-dependent and can't be read off one constant. |
| `JobDriver_Patrol.cs` | `JobDriver_Patrol` | Stands the post and pings `CompRolePost.NotifyOnDuty` every tick, mirroring `JobDriver_ManShop`. Also polls `TroubleUtility.AnyoneWorthCalming` and breaks the patrol off early when there's someone to calm. |
| `WorkGiver_CalmTrouble.cs` | `WorkGiver_CalmTrouble` | The reactive half: sends the assigned sheriff to one specific [rowdy patron](customers.md#trouble-at-the-saloon-and-the-gambling-hall) rather than a building. |
| `JobDriver_CalmTrouble.cs` | `JobDriver_CalmTrouble` | Walks to the target and unilaterally zeroes their rowdiness. A no-op, not a false success, if the target's walked off mid-wait. |
| `JobGiver_RobTill.cs` | `JobGiver_RobTill` | A [stickup](outlaws.md) raider's own scoring pass: every registered business, open or closed, by `TillSilver` against distance — no staffed bonus, no combat awareness. Runs beneath vanilla's own `JobGiver_AIFightEnemies` in the `OWT_StickupDuty` think tree, so self-defense always wins first. |
| `JobDriver_RobTill.cs` | `JobDriver_RobTill` | Cracking a till: walk up, a short delay, take everything in it via `ShopTransaction.RobTill`. A plain `JobDriver`, deliberately not `IBusinessPatron` — see [boundaries worth keeping](#boundaries-worth-keeping). |

### `Roles/` — town roles

| File | Type | Job |
| --- | --- | --- |
| `CompRolePost.cs` | `CompProperties_RolePost`, `CompRolePost` | A named post one specific colonist holds — the [sheriff's office](buildings.md#sheriffs-office)'s assignment, on `CompRolePost.OnDuty` other code reads. Built on vanilla's own `CompAssignableToPawn`, the same idiom a throne or a grave already uses. |
| `TroubleUtility.cs` | `TroubleUtility` | The one hook a rowdiness-capable business has into town roles: bumps a customer's `OWT_Rowdy` hediff by whatever `ServiceWorker.ApplyEffect` hands back (a fixed amount for a saloon's drink, an outcome-dependent one for a gambling hall's wager), applies the sheriff/shopkeeper suppression factors, and fires the scripted [disturbance](customers.md#trouble-at-the-saloon-and-the-gambling-hall) when it tops out. |

### `Compat/` — soft dependencies

| File | Type | Job |
| --- | --- | --- |
| `HospitalityInterop.cs` | `HospitalityInterop` | Detects a loaded Hospitality install and recognizes its guests, entirely by reflection — no reference, hard or optional, to Hospitality's assembly. `Present` is a guessed assembly name; `IsHospitalityGuest` is two structural signals (Lord/LordJob assembly, or any ThingComp's assembly) OR'd together. Also this mod's first `[DebugAction]`, for dumping detection state against a real Hospitality install. |
| `HospitalityBridge.cs` | `HospitalityBridge` | `MapComponent`. The active half: every 250 ticks, offers one shopping job to an idle Hospitality guest via `Pawn_JobTracker.TryTakeOrderedJob` — never a duty, never an interrupt. Reuses `JobGiver_BuyFromShop.PickShoppingJob` and `IncidentWorker_ShopCustomers.GivePurse` unmodified. |

### `Stagecoach/` — the coach depot and route tiers

| File | Type | Job |
| --- | --- | --- |
| `CoachTierDef.cs` | `CoachTierDef` | Data-driven rung of the [route ladder](economy.md#the-stagecoach-line): the appeal it activates at, its arrival ceiling, purse multiplier and VIP chance. Adding a tier is XML, not code, the same "kind is a stanza" idiom `ShopKindDef` and `ServiceDef` already use. |
| `CoachTierUtility.cs` | `CoachTierUtility` | Stateless reads off the map, nothing cached or registered: `HasDepot` (a `ListerThings` scan, mirroring `TroubleUtility.AnySheriffOnDuty`'s own "ask, don't track" shape), `CurrentTier`/`NextTier` (which rung is active and which is next), `CeilingTicks` (a tier's arrival ceiling in ticks, at the player's own Customer volume setting). |
| `CompCoachDepot.cs` | `CompProperties_CoachDepot`, `CompCoachDepot` | The [coach depot](buildings.md#coach-depot)'s only behaviour: an inspect string reading the town's current tier, next tier and countdown live off `TownEconomy` and `CoachTierUtility`. A passive marker like `CompRolePost` and `CompFalseFront` — never staffed, never targeted by a job, nothing persisted on the comp itself. |

### `Lords/`, `Incidents/`, `Alerts/`, `UI/`

| File | Type | Job |
| --- | --- | --- |
| `Lords/LordJob_ShopVisit.cs` | `CustomerRecord`, `LordJob_ShopVisit` | The [visiting group](customers.md#the-visit) and its per-customer records — including, now, who's checked into a bed. Deliberately a flat graph: shopping, then exit; `Trigger_VisitComplete` additionally waits for every rented bed to empty before the group can leave. |
| `Lords/LordToil_Shop.cs` | `LordToil_Shop` | Hands every group member the `OWT_Shop` duty. |
| `Lords/LordJob_Stickup.cs` | `LordJob_Stickup` | A [stickup](outlaws.md) crew's own flat graph — near-twin of `LordJob_ShopVisit`'s shape, hostile instead of paying. Exits either on its own (the duration cap, sheriff-halved, or every till already emptied) or into `LordToil_PanicFlee` the instant anyone shoots back. `GuiltyOnDowned` is what makes capturing a downed raider ordinary vanilla prisoner mechanics rather than anything this mod builds. |
| `Lords/LordToil_Stickup.cs` | `LordToil_Stickup` | Hands every crew member the `OWT_StickupDuty` duty. Byte-for-byte mirror of `LordToil_Shop`. |
| `Incidents/IncidentWorker_ShopCustomers.cs` | `IncidentWorker_ShopCustomers` | Turns town appeal into [arrivals](customers.md#arrival), purses and group size, and — per faction standing — biases [which faction](customers.md#which-faction-turns-up) actually shows up. |
| `Incidents/IncidentWorker_Stickup.cs` | `IncidentWorker_Stickup` | Subclasses `IncidentWorker_RaidEnemy` rather than hand-rolling a raid: `base.TryExecuteWorker` (untouched) resolves the faction, generates pawns and gear, and sends the letter. Five overrides turn that into a [stickup](outlaws.md) — `ResolveRaidPoints` scales a small, capped band off silver at risk rather than colony wealth; `ResolveRaidStrategy`/`ResolveRaidArriveMode` force `OWT_StickupStrategy` and a walk-in arrival; `GetLetterLabel`/`GetLetterText` supply the arrival letter's own copy. |
| `Incidents/RaidStrategyWorker_Stickup.cs` | `RaidStrategyWorker_Stickup` | The one hook a raid strategy has to supply: `MakeLordJob` builds a `LordJob_Stickup`, reading `TroubleUtility.AnySheriffOnDuty` once, at raid creation, to fix the raid's duration for its whole lifetime. `CanUseWith` returns false — a second, independent guard (alongside `IncidentWorker_Stickup` never consulting it for its own firing) against an unrelated ordinary raid ever picking this non-combat strategy. |
| `Incidents/IncidentWorker_ShopCustomers.cs` | `IncidentWorker_ShopCustomers` | Turns town appeal into [arrivals](customers.md#arrival), purses and group size, and — per faction standing — biases [which faction](customers.md#which-faction-turns-up) actually shows up. When `TownEconomy.GuaranteedArrivalDue` is what triggered the firing, also applies the active route tier's purse multiplier and rolls a [VIP passenger](customers.md#scheduled-coach-arrivals) — the same firing, the same `LordJob_ShopVisit`, no second pawn loop. |
| `Alerts/Alert_CustomersWaiting.cs` | `Alert_CustomersWaiting` | Raised while customers burn patience at an unattended business. |
| `Alerts/Alert_RowdyPatrons.cs` | `Alert_RowdyPatrons` | Raised while a patron is "getting loud" and still calmable — the sheriff's real window before a disturbance fires unattended. Mirrors `Alert_CustomersWaiting`'s shape. |
| `Alerts/Alert_StickupRisk.cs` | `Alert_StickupRisk` | Raised once a map's uncollected till total crosses a threshold below `StickupWatch.MinSilverAtRisk` itself — the [risk](outlaws.md#how-the-risk-builds) is visible climbing before the clock behind it is even live. |
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
- **The entire Hospitality bridge (`Compat/`) is built against an assembly this sandbox has never
  had, decompiled, or run against.** `refdump` reads RimWorld's own reference assemblies only —
  it cannot check a single Hospitality type or member name, and nothing here names one directly
  for exactly that reason. What it actually leans on, and how sure this is of each:
  - **Medium confidence:** Hospitality's compiled assembly has the simple name `"Hospitality"`
    (case-insensitive). A widely-repeated convention among RimWorld compatibility patches, not a
    verified fact. If it's wrong, `HospitalityInterop.Present` is false forever and the whole
    bridge is permanently, silently inert — indistinguishable from Hospitality not being
    installed, and no more expensive than that to carry.
  - **Low-to-medium confidence:** a Hospitality guest is governed by a `Verse.AI.Group.Lord`
    whose `LordJob` lives in that same assembly. Structurally plausible — it's the standard
    vanilla idiom for any coordinated pawn group, and this mod's own customers use exactly this
    shape — but unconfirmed.
  - **Low confidence:** Hospitality attaches at least one `ThingComp`, of any name, to a guest
    pawn. A guess about whether such a comp exists at all, not about what it's called.
  - The two structural signals above are OR'd, not ANDed, so detection only fails completely if
    *both* guesses are wrong — a meaningfully more forgiving bar than leaning on either alone,
    and it costs nothing extra to check.
  - **Deliberately not guessed either way:** whether a Hospitality guest carries any silver by
    default. The silver top-up setting (`hospitalityGuestsCarrySilver`) reuses
    `IncidentWorker_ShopCustomers.GivePurse` unmodified rather than assuming an answer, and
    defaults on — if guests turn out to already carry plenty, the top-up simply tops up nothing
    (`GivePurse` only ever adds the shortfall).
  - A `[DebugAction]` (`HospitalityInterop.LogDetectionState`, Dev Mode only) dumps every
    non-player humanlike pawn's detection result, `Lord`/`LordJob` type and full comp list on
    every map — the tool a maintainer with a real Hospitality install needs to correct the
    guesses above without decompiling anything blind.
- **`HospitalityBridge` hands a job to a foreign-`Lord`-owned pawn through
  `Pawn_JobTracker.TryTakeOrderedJob`, gated on `Pawn_MindState.IsIdle`.** Both members are real,
  confirmed vanilla API, but this specific interaction — a second mod's per-tick AI governing a
  pawn whose `IsIdle` this mod is reading and whose job tracker this mod is calling into — has
  never been observed in a live game, because no Hospitality install exists in this sandbox to
  test against. This compounds the mod's own pre-existing "none of this has run in RimWorld"
  risk at the top of this list with a second, larger unknown neither `refdump` nor
  `tools/validate_defs.py` can touch, since both only ever see this mod's own assembly and
  RimWorld's reference assemblies, never a third mod's.
- **A bridged guest is recognized by `WorkGiver_ManShop.AnyCustomerNear` only after the bridge
  has already dispatched them** — up to `HospitalityBridge`'s own 250-tick scan interval of
  latency before a colonist is prompted to staff up for them, versus instant recognition for a
  duty-driven native customer. Disclosed, not fixed: closing the gap would mean duplicating
  Hospitality-detection logic inside a hot per-colonist work-search path, which is worse than the
  latency it would remove.
- **A bridged guest has no `CustomerRecord`** (that lives on this mod's own `LordJob_ShopVisit`,
  which a Hospitality-owned pawn structurally can't be running), so two of its usual protections
  don't apply to them the same way. `HospitalityBridge`'s own per-`(pawn, shop)` cooldown stands
  in for `refusedShops` — bounding, not eliminating, repeat visits to a chronically unstaffed
  shop, at the disclosed cost of also throttling legitimate repeat business at a good one. There
  is no stand-in for `causedTrouble` at all: a bridged guest who tips a saloon into a disturbance
  can, in principle, be offered another round later in the same stay. Both are accepted,
  bounded-but-not-eliminated gaps — see `docs/DESIGN.md` for the full reasoning behind not
  closing either one.
- **`HospitalityInterop`'s assembly-reference-equality check assumes Hospitality ships every
  guest-relevant type from one assembly named `"Hospitality"`.** If a future Hospitality version
  splits guest-related types across a second assembly (a shared library, say), types living
  there would be invisible to `IsHospitalityGuest` even though `Present` correctly detects
  Hospitality itself. Undiscoverable from this sandbox either way, and stated plainly rather than
  papered over.

- **A gambling hall's `WaitForService` toil gates on the shared `Staffed` flag, not a per-customer
  lock** — the same architecture every business already has, confirmed by reading it: every
  queued gambler accrues served ticks in parallel as long as the table is staffed at all. Several
  patrons can therefore resolve a winning hand in the same tick window, each independently
  drawing on the till before it's replenished — `startingTillSilver`'s sizing implicitly assumes
  roughly sequential play. This is **not a race condition** — RimWorld ticks pawns sequentially,
  and `CompBusiness.TakeFromTill` re-reads the till fresh on every call, so no draw can ever
  overdraw it — just a reason a busy table can burn through its bankroll and self-close sooner
  than a sequential-play estimate would suggest. The hard per-round till cap is what keeps that
  graceful rather than catastrophic: worst case under heavy concurrent play is the hall shutting
  itself down sooner and more visibly, never a negative till. Solving it for real would mean
  adding an exclusivity lock to the shared-`Staffed`-flag architecture the whole mod's
  non-synchronising-loops guarantee rests on — out of scope for a step-2 `ServiceWorker`.
- **A save from before the gambling hall existed, with a `OWT_FaroTable` already placed, loads as
  an unseeded business.** The def reshape (decorative → `CompProperties_Business`) means vanilla
  simply instantiates whatever comps the new def declares on load, and every `CompBusiness` field
  on a comp that never existed on that Thing before takes its declared default correctly and
  safely — `HouseEdge`/`Markup` both lazy-init from the kind's defaults on first read. The one
  gap: `PostSpawnSetup`'s till-seeding is guarded by `!respawningAfterLoad`, and loading a save
  *is* a respawn, so a pre-existing table gets no seed silver (and was never charged for it
  either, since it was built under the old, cheaper def). It behaves exactly like a freshly built
  table with an empty bankroll — the same "first winner might be shorted" situation seed capital
  exists to avoid, but only for tables placed before this update. Given the mod has no players yet
  (see Known Context in `CLAUDE.md`), this is accepted as a documented, pre-release-only risk
  rather than something worth bespoke migration code.
- **The gambling hall's own numbers are first-pass guesses, untested in a live game** — the
  25%→2% cheating-accusation curve across dealer Social 0–20, the win/loss/shortfall rowdiness
  ordering, `defaultHouseEdge` (0.15), `startingTillSilver` (300) and `joyGainPerHand` (0.1)
  specifically. The thing most worth playtesting: whether the accusation-frequency swing between
  an unskilled and a max-Social dealer is actually noticeable over a normal evening — that visible
  swing is the entire point of the Social-skill hook the brief asked for. `joyGainPerHand` wants
  its own playtest too: it's a round number chosen in the same spirit as this file's other
  constants, not measured against how fast a real customer's Joy need actually decays while
  sitting at a table.
- **[Outlaws and the law](outlaws.md) is entirely new pawn AI** — a lord graph, a duty think
  tree, two job drivers — exactly the category this list opens with as impossible to validate
  statically. Unproven in a live game like everything else raid-side in this mod.
- **`IncidentWorker_Stickup`'s five `IncidentWorker_Raid` overrides, and
  `RaidStrategyWorker_Stickup.MakeLordJob`, are confirmed genuine overrides by the compiler, not
  by refdump** — refdump reports member existence and signature only, never accessibility or
  virtual/override modifiers. Two of the five (`ResolveRaidStrategy`, `ResolveRaidArriveMode`)
  first failed to compile as `protected override` with exactly the `CS0507` access-modifier
  error this risk was expected to surface as, and compiled clean once corrected to `public
  override`, matching the base class's own accessibility; `MakeLordJob` needed the opposite fix,
  from `public` down to `protected`. `GetLetterLabel`, `GetLetterText` and `CanUseWith` compiled
  as overrides on the first attempt. What the compiler *can't* confirm, because reference
  assemblies carry no IL: whether `IncidentWorker_Raid`'s internal `TryExecuteWorker` genuinely
  resolves strategy, arrival mode and points *before* generating pawns and gear, so overriding
  them actually reaches the same raid rather than one generated a step earlier. If that
  ordering assumption is wrong, the realistic worst case is a stickup whose gear reflects
  different defaults while `LordJob_Stickup` still runs the actual encounter — degraded, not
  broken, since `MakeLordJob` still receives whatever `parms.faction`/`points` ended up resolved.
- **Whether a duty-hooked hostile pawn genuinely retains full vanilla self-preservation/combat
  behavior alongside a custom think-tree objective beneath it** can't be proven from this
  sandbox — no Core Defs XML is available to see how vanilla's own raid duties are actually
  composed, and reference assemblies carry no IL. Mitigated by putting vanilla's own
  `JobGiver_AIFightEnemies` at the top of `OWT_StickupDuty`'s priority list — the identical
  composition shape (a reactive concern first, falling through on null) already proven to work in
  `OWT_Shop`'s own duty, for lodging rather than combat.
- **`LordToil_PanicFlee`'s parameterless constructor compiles clean** (`new
  LordToil_PanicFlee()`), which resolves the one construction question refdump couldn't answer —
  it doesn't inspect constructors, only fields, properties and methods. What compiling clean
  can't confirm is what a fleeing raider actually looks like in play, or whether vanilla expects
  this toil to be reached only through raid machinery this mod doesn't otherwise touch.
- A robber, a gambling-hall payout, the player's own Collect gizmo, and deconstruction can all
  touch the same till in overlapping windows. This degrades exactly the way the existing
  gambling-hall concurrency note above already documents: `TakeFromTill` re-reads the till fresh
  on every call, so it can never be over-drawn or duplicated — worst case is one party finding
  less than expected, never a negative till or silver created from nothing. A robber is simply a
  new third party to a race this file previously only reasoned about for customers and gamblers.
- Two raiders can converge on the same till — best-effort `Pawn.Reserve` on the standing cell, not
  a hard lock on the business itself, the same accepted, gracefully-degrading race this codebase
  already documents for stock and hotel beds. `ShopTransaction.RobTill`'s own re-validate-before-
  taking discipline means the loser gets nothing rather than duplicating or voiding silver.
- **`JobDriver_RobTill` deliberately does not implement `IBusinessPatron`**, the one place this
  mechanic actively *prevents* a synchronization the business layer would otherwise fall into —
  see [boundaries worth keeping](#boundaries-worth-keeping). The accepted cost: a robber standing
  at, but not reserving, a customer cell isn't recognized by an *approaching* customer's own
  queue-fanout check the way another queueing customer would be — only physical occupancy is
  checked. A minor, purely cosmetic edge case, the same category of imprecision `CustomerCellFor`
  already discloses for ordinary queueing.
- **Every stickup tunable is a first-pass guess, untested in a live game** — the MTB curve (six
  days down to 0.75, past a 300-silver floor), the alert threshold (150), the points-scaling curve
  and its 80–400 cap, and the sheriff's frequency and duration factors specifically. The thing most
  worth playtesting: whether the MTB curve actually reads as "risk visibly building" rather than
  "never happens" or "constant harassment," and whether the points cap produces a band that feels
  small and focused rather than trivial or overwhelming.
- `RaidStrategyDef`'s `SimpleCurve` XML shape (`selectionWeightPerPointsCurve`,
  `pointsFactorCurve`, authored as `<points><li>(x, y)</li>...</points>`) is well-established
  modding convention, not confirmed against a Core Defs example this sandbox can check — same
  disclosed-but-unverifiable category as `OWT_BatwingDoor`'s `ParentName="Door"` assumption above.
- **Every stagecoach-line number is a first-pass, untested guess** — the three tiers' appeal
  thresholds (0.5/1.5/3.5), arrival ceilings (8/4/2 days), purse multipliers (×1.25/×1.6/×2.0)
  and VIP chances (0%/8%/20%), the flat ×5 VIP purse multiplier, and the depot's own footprint,
  cost and research numbers (3×2, 140 stuff, 2000 work, 800 research) — sized by eye against the
  sheriff's office and the faro table, in the same spirit as this file's other untested
  constants. The weekly-coach tier's own entry is the single largest engineered uplift anywhere
  in this feature (see [the stagecoach line](economy.md#the-stagecoach-line)) and the first
  number worth re-measuring after a playtest.
- **The stagecoach guarantee's own sanity check models `Rand.MTBEventOccurs` as an exact
  memoryless process** — an approximation of its real behaviour, not a proof, inherited from the
  arrival clock's own pre-existing use of it above rather than a new risk this feature
  introduces. The qualitative shape (bounded, peaks at a tier's own entry, tapers toward the
  ceiling) should hold regardless of the exact percentages; the percentages themselves are worth
  confirming by logging real inter-arrival gaps in play.
- **`IncidentWorker_ShopCustomers.TryExecuteWorker` re-derives `GuaranteedArrivalDue` and
  `RouteTier` fresh rather than threading a flag in from `TownEconomy.TryAttractCustomers`** —
  the same assumption `ChooseWeightedFaction`'s own comment above already documents and depends
  on for `ResolveParmsPoints`, that `Storyteller.TryFire` invokes this worker synchronously with
  nothing else mutating `TownEconomy` state in between. Not a new risk, just one more property
  read leaning on it.
- **`Find.LetterStack.ReceiveLetter` and `Scribe_Defs.Look` are both confirmed to exist and
  match this feature's call shapes via `refdump`, but this is the first place in the codebase
  either is actually used.** Worth a first-play check that a route promotion letter genuinely
  renders, and that reloading a save genuinely doesn't re-announce a tier the player has already
  seen.
