---
title: Reference tables
summary: Every defName, tunable constant, mod setting and translation key the mod ships, in one place.
---

> **This page is for people editing the files.** It lists the mod's internal names, the constants
> behind the numbers, and every translation key — the things you need when patching, translating
> or extending it. If you are here to *play*, the pages under **Playing** and **Systems** explain
> all of the same behaviour without the internal names: [buildings](buildings.md),
> [business kinds](businesses.md), [services](services.md), [the town economy](economy.md),
> [customers](customers.md), [shopkeeping](shopkeeping.md).

## Defs

Everything this mod adds is prefixed `OWT_`.

### Buildings

| defName | Label | Business kind | File |
| --- | --- | --- | --- |
| `OWT_ShopCounter` | shop counter | `OWT_GeneralStore` | `Defs/ThingDefs_Buildings/Buildings_Commerce.xml` |
| `OWT_SaloonBar` | saloon bar | `OWT_Saloon` | *same file* |
| `OWT_BarberChair` | barber chair | `OWT_Barber` | *same file* |
| `OWT_HotelDesk` | hotel desk | `OWT_Hotel` | *same file* |
| `OWT_HotelBed` | hotel bed | — (`CompRentableBed`, not a business) | *same file* |
| `OWT_CounterBase` | *(abstract parent)* | — | *same file* |
| `OWT_FalseFront` | false front | — | `Defs/ThingDefs_Buildings/Buildings_MainStreet.xml` |
| `OWT_HitchingPost` | hitching post | — | *same file* |
| `OWT_Gallows` | gallows | — | *same file* |
| `OWT_FaroTable` | faro table | — | *same file* |
| `OWT_BatwingDoor` | batwing doors | — (`ParentName="Door"`) | *same file* |
| `OWT_StreetFurnitureBase` | *(abstract parent)* | — | *same file* |
| `OWT_SheriffOffice` | sheriff's office | — (not a business; `CompRolePost`) | `Defs/ThingDefs_Buildings/Buildings_Roles.xml` |

### Terrain

| defName | Label | File |
| --- | --- | --- |
| `OWT_Boardwalk` | boardwalk | `Defs/TerrainDefs/Terrain_MainStreet.xml` |

### Hediffs

| defName | Label | Stages | File |
| --- | --- | --- | --- |
| `OWT_Rowdy` | rowdy | feeling good / getting loud / spoiling for a fight | `Defs/HediffDefs/Hediffs_Commerce.xml` |

### Business kinds

| defName | Label | Markup | Appeal | Patience | Services |
| --- | --- | --- | --- | --- | --- |
| `OWT_GeneralStore` | general store | 1.35 (0.5–3.0) | 1.0 | 2500 | — |
| `OWT_Saloon` | saloon | 1.80 (0.5–4.0) | 1.4 | 1500 | Drink, Meal |
| `OWT_Barber` | barber shop | 1.50 (0.5–3.0) | 1.1 | 2200 | Haircut |
| `OWT_Hotel` | hotel | 1.60 (0.5–3.5) | 1.3 | 2800 | Lodging |

### Services

| defName | Label | Worker | Serve ticks | Consumes stock | Self-service |
| --- | --- | --- | --- | --- | --- |
| `OWT_Drink` | drink | `ServiceWorker_Ingest` (Liquor / Joy) | 150 | yes | yes |
| `OWT_Meal` | meal | `ServiceWorker_Ingest` (IsMeal / Food) | 150 | yes | yes |
| `OWT_Haircut` | haircut | `ServiceWorker_Haircut` | 2200 | no | **never** |
| `OWT_Lodging` | lodging | `ServiceWorker_Lodging` | 200 | no (claims a `CompRentableBed` instead) | **never** |

### Jobs

| defName | Driver | Report string |
| --- | --- | --- |
| `OWT_BuyFromShop` | `JobDriver_BuyFromShop` | buying TargetA. |
| `OWT_ManShop` | `JobDriver_ManShop` | minding TargetA. |
| `OWT_ServeDrink` | `JobDriver_UseService` | getting a drink at TargetB. |
| `OWT_ServeMeal` | `JobDriver_UseService` | getting a meal at TargetB. |
| `OWT_ServeHaircut` | `JobDriver_UseService` | getting a haircut at TargetB. |
| `OWT_ServeLodging` | `JobDriver_UseService` | checking in at TargetB. |
| `OWT_SleepInRentedBed` | `JobDriver_SleepInRentedBed` | sleeping at TargetA. |
| `OWT_Patrol` | `JobDriver_Patrol` | patrolling TargetA. |
| `OWT_CalmTrouble` | `JobDriver_CalmTrouble` | calming down TargetA. |

### Everything else

| defName | Type | Notes |
| --- | --- | --- |
| `OWT_Commerce` | `DesignationCategoryDef` | Build-menu category, sort order 410 |
| `OWT_FrontierCommerce` | `ResearchProjectDef` | 500 cost, Medieval, no prerequisites |
| `OWT_Shopkeeping` | `WorkTypeDef` | Natural priority 460, Social skill |
| `OWT_ManShopCounter` | `WorkGiverDef` | Priority 100 in type; needs Manipulation + Talking |
| `OWT_Sheriffing` | `WorkTypeDef` | Natural priority 460, Social skill; `alwaysStartActive` false — only the assigned pawn ever has anything to do here |
| `OWT_PatrolPost` | `WorkGiverDef` | Priority 100 in type; ambient half of suppression; needs Manipulation + Talking |
| `OWT_CalmDownPatron` | `WorkGiverDef` | Priority 200 in type (outranks `OWT_PatrolPost`); reactive half; needs Manipulation + Talking |
| `OWT_Shop` | `DutyDef` | HighPriority hook; buy-then-wander think tree |
| `OWT_ShopCustomers` | `IncidentDef` | Misc, baseChance 3, minRefireDays 0.6 |
| `OWT_FreshHaircut` | `ThoughtDef` | +5 mood, 1.5 days, stack limit 1 |
| `OWT_SleptAtHotel` | `ThoughtDef` | 3 stages (room Impressiveness `< 20` / `< 60` / else), +2/+4/+7 mood, 1.5 days, stack limit 1, granted on waking |

## Tunable numbers

The constants worth knowing when balancing. Where a number lives in XML it is a def field;
where it lives in C# it is a `const` in the named file.

### Economy — `Shops/TownEconomy.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `MinAppealForCustomers` | 0.5 | Below this, no customer group sets out |
| `ServiceValueWeight` | 30 | Multiplier on stock-free service value before it joins the wealth curve |
| `ArrivalCheckInterval` | 600 ticks | How often the arrival clock is consulted |
| Repeat-kind weight | 0.35 | Value of a second business of a kind already present |
| Wealth normalization | `sqrt(x / 1000)`, clamped 0.25–3.0 | Diminishing returns on stock value |
| Reputation-appeal multiplier | `lerp(0.5, 1.5, reputation)` | A good name triples your draw over a bad one |
| MTB band | 3.5 → 0.8 days | Mean time between groups, from appeal 0.5 to 4.0 |
| Sale reputation | +0.01 | Staffed sale or service |
| Self-service reputation | −0.005 | Unattended sale |
| Walkout reputation | −0.02 | Customer gives up |
| Daily decay | 5% toward 0.5 | Applied at local midnight |

### Faction standing — `Shops/TownEconomy.cs`, `Incidents/IncidentWorker_ShopCustomers.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `StandingWith(faction)` | `standings[faction]`, or `Reputation` if untracked | A faction's own 0–1 standing with the town |
| `FactionStandingSaleDelta` | +0.05 | A staffed sale or service, for that customer's own faction |
| `FactionStandingWalkoutDelta` | −0.10 | A walkout, or a hotel eviction, for that customer's own faction |
| Daily decay | 5% toward `Reputation` | Applied at local midnight, same as reputation's own decay |
| `ArrivalWeight(faction)` | `lerp(0.15, 3, StandingWith(faction))` | Weight `ChooseWeightedFaction` draws this faction with once vanilla's own `TryResolveParms` has already picked a candidate pool |
| `LedgerStandingDivergenceThreshold` (`CompBusiness.cs`) | 0.1 | How far a faction's standing must clear `Reputation` before the ledger names it as a regular or a lost cause |

### Pricing — `Shops/ShopPricing.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `MinPrice` | 1 | Nothing is ever free |
| Price formula | `MarketValue × markup × reputationFactor` | The only price basis in the mod |
| `ReputationPriceFactor` | `lerp(1.15, 0.9, reputation)` | Price tolerance, in `CompBusiness` |
| `ValueAppeal` | `clamp(1 / effectiveMarkup, 0.1, 2.0)`, plus `CompFalseFront.CurbAppealBonus` | How attractive this shop's prices are |

### Business — `Shops/CompBusiness.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `StaffPresenceGraceTicks` | 60 | How stale a staffing ping may be and still count |
| `StockCacheTicks` | 60 | How long a scanned display list is reused |
| Queue fan-out radius | 3.9 | How far a queueing customer will stand from the customer cell |

### Curb appeal — `Shops/CompFalseFront.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `AdRadius` | 7 tiles | How far a false front's bonus reaches from a shop's customer-facing side |
| Curb-appeal bonus | +0.10 (one qualifying facade), +0.15 (two or more) | Folded into `ValueAppeal`; diminishing, capped at two |

### Customers — `AI/`, `Incidents/`

| Name | Value | Where |
| --- | --- | --- |
| `BrowseTicks` | 240 | `JobDriver_BuyFromShop` |
| `ServeTicks` (goods) | 180 | `JobDriver_BuyFromShop` |
| `CustomerScanRadius` | 25 | `WorkGiver_ManShop` |
| `ShoppingRadius` | 30 | `LordToil_Shop` |
| Wander radius | 12 | `Duties_Customer.xml` |
| `VisitDurationTicks` | 40000 | `IncidentWorker_ShopCustomers` |
| `BasePurse` | 120–450 | " |
| Purse scale | `lerp(0.7, 2.2, appeal/4)` | " |
| Arrival food need | 40%–90% | " |
| Points | `clamp(appeal × 60 × volume, 40, 900)` | " |
| Max units per purchase | `stackLimit / 4`, or 1 if unstackable | `ShopStock` |
| Item choice tie-break | `× random(0.6, 1.4)` | " |

### Lodging — `Shops/ServiceWorker.cs`, `AI/JobGiver_SleepInRentedBed.cs`, `AI/JobDriver_SleepInRentedBed.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| Desirability | `lerp(2.5, 0.5, restNeed%)` | Floored, not gated — even a well-rested guest occasionally books |
| `TiredThreshold` | 0.4 | Below this fraction of the Rest need, a checked-in guest heads for bed |
| `RestedThreshold` | 0.9 | Rest level at which a sleeping guest wakes and checks out |
| `MaxSleepTicks` | 30000 | Hard cap on one sleep job, independent of `Need_Rest` |

### Town roles — `Roles/CompRolePost.cs`, `Roles/TroubleUtility.cs`, `Defs/HediffDefs/Hediffs_Commerce.xml`, `Defs/ServiceDefs/Services_Commerce.xml`

| Name | Value | Meaning |
| --- | --- | --- |
| `rowdinessPerServing` (`OWT_Drink`) | 0.2 | Severity a round of drinks adds to `OWT_Rowdy`. Meal leaves this at its default of zero |
| `OWT_Rowdy` severity per day | −0.5 | Vanilla `HediffCompProperties_SeverityPerDay`; decays on its own, no custom `HediffComp` |
| `OWT_Rowdy` stage thresholds | 0 / 0.5 / 1.0 | feeling good / getting loud / spoiling for a fight |
| `SheriffOnDutyFactor` | 0.5 | An on-duty sheriff roughly halves rowdiness accrual, map-wide |
| `MaxShopkeeperSocialFactor` | 0.5 | What a max-Social shopkeeper behind the bar discounts it to; an unstaffed bar gets no discount |
| `OnDutyGraceTicks` (`CompRolePost`) | 60 | How stale a patrol ping may be and still count as on duty — mirrors `StaffPresenceGraceTicks` |
| `IdlePatienceTicks` (`JobDriver_Patrol`) | 1250 | Safety valve that ends a patrol so the next think-tree tick can reconsider it |
| `TroubleCheckIntervalTicks` (`JobDriver_Patrol`) | 30 | How often a standing patrol polls for a patron worth calming |
| `CalmTicks` (`JobDriver_CalmTrouble`) | 200 | How long the sheriff spends talking a patron down |
| Social XP for a calm-down | 35 | Same as a shopkeeper's XP for a served sale |

## Mod settings

`OldWestTownSettings`, saved in RimWorld's mod-settings file.

| Field | Default | Range | Effect |
| --- | --- | --- | --- |
| `allowSelfService` | false | on/off | Customers buy from unattended counters, at −0.005 reputation per sale |
| `customerVolume` | 1.0 | 0.25–3.0 | Scales both the arrival clock and group size |
| `customerWealth` | 1.0 | 0.25–3.0 | Scales the silver each customer carries |

## Translation keys

All in `Languages/English/Keyed/OldWestTown.xml`. Every `.Translate()` key used in C# must have
an entry here — [`tools/validate_defs.py`](contributing.md#static-checks) fails the build
otherwise.

| Group | Keys |
| --- | --- |
| Mod settings | `OWT_ModTitle`, `OWT_SettingSelfService`, `OWT_SettingSelfServiceDesc`, `OWT_SettingVolume`, `OWT_SettingWealth` |
| Inspect pane | `OWT_StatusOpen`, `OWT_StatusClosed`, `OWT_Unattended`, `OWT_StockLine`, `OWT_ServicesLine`, `OWT_RoomsLine`, `OWT_MarkupLine`, `OWT_TillLine`, `OWT_TownLine` |
| Gizmos | `OWT_CmdOpen`, `OWT_CmdOpenDesc`, `OWT_CmdMarkup`, `OWT_CmdMarkupDesc`, `OWT_MarkupSlider`, `OWT_CmdCollect`, `OWT_CmdCollectDesc`, `OWT_CmdLedger`, `OWT_CmdLedgerDesc` |
| Town ledger | `OWT_LedgerTitle`, `OWT_LedgerAppealLine`, `OWT_LedgerReputationLine`, `OWT_LedgerTodayLine`, `OWT_LedgerLifetimeLine`, `OWT_LedgerShopLine`, `OWT_LedgerRegularLine`, `OWT_LedgerColdLine` |
| Stock tab | `OWT_TabStock`, `OWT_TabStockHeader`, `OWT_ResetStock` |
| Events | `OWT_LetterCustomersLabel`, `OWT_LetterCustomersText`, `OWT_CustomersLeaving`, `OWT_CustomersScared`, `OWT_CustomerWalkedOut`, `OWT_CustomerWalkedOutService`, `OWT_GuestEvicted` |
| Alerts | `OWT_AlertUnattended`, `OWT_AlertUnattendedDesc` |
| Rentable bed inspect panel and gizmo | `OWT_BedVacant`, `OWT_BedOccupiedBy`, `OWT_CmdEvictGuest`, `OWT_CmdEvictGuestDesc` |
| False front | `OWT_FalseFrontAdvertising`, `OWT_FalseFrontIdle` |
| Sheriff's office gizmo and inspect panel | `OWT_CmdAssignSheriff`, `OWT_CmdAssignSheriffDesc`, `OWT_PostAlreadyFilled`, `OWT_PostVacant`, `OWT_PostOnDuty`, `OWT_PostOffDuty` |
| Saloon trouble | `OWT_SaloonTrouble`, `OWT_DisturbanceLine`, `OWT_AlertRowdyPatrons`, `OWT_AlertRowdyPatronsDesc` |

## Saved state

What survives a save/load, and where it lives.

| Data | Owner | Notes |
| --- | --- | --- |
| Till contents | `CompBusiness` | Deep-saved `ThingOwner`; dropped on destroy |
| Stock filter, open flag, markup | `CompBusiness` | Per business |
| Per-business ledger | `CompBusiness` | Daily figures rolled over at midnight |
| Town reputation, daily + lifetime figures | `TownEconomy` | One per map |
| Per-faction standing | `TownEconomy` | Sparse `Dictionary<Faction, float>`; a save with no `standings` node reads every faction as `Reputation`, exactly like the untracked case |
| Per-customer records, including a checked-in guest's rented bed | `LordJob_ShopVisit` | Saves and dies with the visiting group |
| Wait/serve progress | `JobDriver_PatronizeBusiness` | So a mid-sale save resumes correctly |
| Sleep progress (`ticksAsleep`) | `JobDriver_SleepInRentedBed` | So a mid-stay save resumes correctly |
| Current guest, selling desk | `CompRentableBed` | References only; released if the guest is dead on load |
| Sheriff assignment | `CompRolePost` (vanilla `CompAssignableToPawn`) | Persisted by the base class itself; `CompRolePost` adds no `ExposeData` of its own |
| On-duty flag (`lastOnDutyTick`/`lastOnDutyPawn`) | `CompRolePost` | Deliberately **not** persisted, mirroring `CompBusiness`'s own staff flag — re-established within moments once the sheriff's patrol job re-ticks after a reload |
| Mod settings | `OldWestTownSettings` | Global, not per save |

`TownEconomy` rebuilds its business register in `FinalizeInit`, because comps register on spawn
and a loaded map spawns them before the map component exists. `FalseFrontRegistry` rebuilds its
own facade list the same way, for the same reason.
