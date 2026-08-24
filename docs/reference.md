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
| `OWT_CounterBase` | *(abstract parent)* | — | *same file* |

### Business kinds

| defName | Label | Markup | Appeal | Patience | Services |
| --- | --- | --- | --- | --- | --- |
| `OWT_GeneralStore` | general store | 1.35 (0.5–3.0) | 1.0 | 2500 | — |
| `OWT_Saloon` | saloon | 1.80 (0.5–4.0) | 1.4 | 1500 | Drink, Meal |
| `OWT_Barber` | barber shop | 1.50 (0.5–3.0) | 1.1 | 2200 | Haircut |

### Services

| defName | Label | Worker | Serve ticks | Consumes stock | Self-service |
| --- | --- | --- | --- | --- | --- |
| `OWT_Drink` | drink | `ServiceWorker_Ingest` (Liquor / Joy) | 150 | yes | yes |
| `OWT_Meal` | meal | `ServiceWorker_Ingest` (IsMeal / Food) | 150 | yes | yes |
| `OWT_Haircut` | haircut | `ServiceWorker_Haircut` | 2200 | no | **never** |

### Jobs

| defName | Driver | Report string |
| --- | --- | --- |
| `OWT_BuyFromShop` | `JobDriver_BuyFromShop` | buying TargetA. |
| `OWT_ManShop` | `JobDriver_ManShop` | minding TargetA. |
| `OWT_ServeDrink` | `JobDriver_UseService` | getting a drink at TargetB. |
| `OWT_ServeMeal` | `JobDriver_UseService` | getting a meal at TargetB. |
| `OWT_ServeHaircut` | `JobDriver_UseService` | getting a haircut at TargetB. |
| `OWT_GetHaircut` | `JobDriver_ColonistUseService` | getting a haircut at TargetB. *(your own colonists)* |

### Everything else

| defName | Type | Notes |
| --- | --- | --- |
| `OWT_Commerce` | `DesignationCategoryDef` | Build-menu category, sort order 410 |
| `OWT_FrontierCommerce` | `ResearchProjectDef` | 500 cost, Medieval, no prerequisites |
| `OWT_Shopkeeping` | `WorkTypeDef` | Natural priority 460, Social skill |
| `OWT_ManShopCounter` | `WorkGiverDef` | Priority 100 in type; needs Manipulation + Talking |
| `OWT_Shop` | `DutyDef` | HighPriority hook; buy-then-wander think tree |
| `OWT_ShopCustomers` | `IncidentDef` | Misc, baseChance 3, minRefireDays 0.6 |
| `OWT_FreshHaircut` | `ThoughtDef` | +5 mood, 1.5 days, stack limit 1 |

## Tunable numbers

The constants worth knowing when balancing. Where a number lives in XML it is a def field;
where it lives in C# it is a `const` in the named file.

### Economy — `Shops/TownEconomy.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `MinAppealForCustomers` | 0.5 | Below this, no customer group sets out |
| `ServiceOfferWeight` | 30 | Multiplier on a stock-free service's `basePrice` before it joins the town's offer total. Discounted by `RepeatFrontFactor` like the shop-front itself, so a street of barber chairs doesn't out-draw a stocked one |
| `SurveyInterval` | 60 ticks | How often the town takes stock of itself — and so how often every shop's shelves are re-read |
| `ArrivalCheckInterval` | 600 ticks | How often the arrival clock is consulted; deliberately a multiple of `SurveyInterval`, so an arrival roll always reads a survey taken the same tick |
| `RepeatFrontFactor` | 0.35 | The n-th shop-front of a kind the town already has is worth 0.35^n of the first, converging at 1.54×. A second counter of the same kind on the same sales floor is a second till, and is worth nothing extra |
| Wealth normalization | `sqrt(offerValue / 1000)`, clamped 0.25–3.0 | Diminishing returns on what the town has out, priced at market value and counted once per stack — the markup slider does not move it |
| `MinPurseFactor` | 0.9 | Floor on the purse multiplier, above the goods term's own floor: a town's first customers must afford its first shelf |
| Standing multiplier | `lerp(0.5, 1.5, reputation)` | A good name triples your draw over a bad one |
| MTB band | 3.5 → 0.8 days | Mean time between groups, from appeal 0.5 to 4.0 |
| Verdict: served | 1.0 | Somebody stood behind the counter for them |
| Verdict: honesty box | 0.5 | Goods off an unwatched shelf |
| Verdict: gave up | ×0.5 | Halves whatever else that customer's day was worth |
| `MaxDayWeight` | 0.2 | Most of the gap one full day of trade can close, at midnight |
| `FullEvidencePatrons` | 6 | Customers in a day that count as a full day's evidence; a thinner day moves the number proportionally less |
| `IdleDrift` | 5% toward 0.5 | Applied at local midnight, but only on a day nobody came to a counter. A trading day moves the number toward that day's verdict instead |

### Pricing — `Shops/ShopPricing.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `MinPrice` | 1 | Nothing is ever free |
| Price formula | `MarketValue × markup × reputationFactor` | Goods, and any service poured off a shelf |
| Service price formula | `basePrice × markup × reputationFactor` | Only for a service with nothing on the shelf behind it (a haircut) |
| `MaxAffordable` | `PriceFor`, walked down until the purse covers it | The one answer to "how much of this can they pay for". Both the order the customer picks and the bill the counter trims come from it |
| `ReputationPriceFactor` | `lerp(0.9, 1.1, reputation)` | What the town's name does to every price, in `CompBusiness`. A town nobody thinks well of has to discount; a well-liked one charges more. Neutral is exactly 1.0 |
| `ValueAppeal` | `clamp(1 / effectiveMarkup, 0.1, 2.0)` | How attractive this shop's prices are |

### Business — `Shops/CompBusiness.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `StaffPresenceGraceTicks` | 60 | How stale a staffing ping may be and still count |
| Shelf re-read | Every 60 ticks | Done by the town's survey, plus at once after a sale or a filter edit. Nothing on a drawing path ever triggers one |
| `BusyMessageIntervalTicks` | 15000 | How often a counter may say out loud that it is turning trade away |
| Queue fan-out radius | 3.9 | How far a queueing customer will stand from the customer cell |

### Customers — `AI/`, `Incidents/`

| Name | Value | Where |
| --- | --- | --- |
| `BrowseTicks` | 240 | `JobDriver_BuyFromShop` |
| `GoodsServeTicks` | 180 | `JobDriver_BuyFromShop` |
| `MaxQueueWaitTicks` | 6000 | `JobGiver_BuyFromShop` — the longest wait anybody will join a line for. Depth is therefore `ceil(6000 / serveTicks)`: 34 at a shelf, 3 at the barber's chair |
| `StaffDrawBonus` | 0.5 | " — how much more a staffed counter draws, thinned by the crowd already headed there |
| `CustomerScanRadius` | 25 | `WorkGiver_ManShop` |
| `CustomerScanInterval` | 30 ticks | `JobDriver_ManShop` — how often a shopkeeper re-checks whether anybody is still shopping |
| `IdlePatienceTicks` | 1250 | " — how long they hold the post with nobody in sight |
| `ShoppingRadius` | 30 | `LordToil_Shop` |
| Wander radius | 12 | `Duties_Customer.xml` |
| `VisitDurationTicks` | 40000 | `IncidentWorker_ShopCustomers` |
| `BasePurse` | 120–450 | " |
| Purse scale | `max(0.9, GoodsFactor) × customerWealth` | " — reads the goods on offer and nothing else: not appeal, not the town's name, not the markup |
| Arrival food need | 40%–90% | " |
| Points | `clamp(appeal × 60 × volume, 40, 900)` | " |
| Max units per purchase | `stackLimit / 4`, or 1 if unstackable | `ShopStock` |
| Item choice tie-break | `× random(0.6, 1.4)` | " |

## Mod settings

`OldWestTownSettings`, saved in RimWorld's mod-settings file.

| Field | Default | Range | Effect |
| --- | --- | --- | --- |
| `allowSelfService` | false | on/off | Customers buy from unattended counters. Goods off an unwatched shelf count as half a proper welcome in the night's reputation verdict |
| `customerVolume` | 1.0 | 0.25–3.0 | Scales both the arrival clock and group size |
| `customerWealth` | 1.0 | 0.25–3.0 | Scales the silver each customer carries |

## Translation keys

All in `Languages/English/Keyed/OldWestTown.xml`. Every `.Translate()` key used in C# must have
an entry here — [`tools/validate_defs.py`](contributing.md#static-checks) fails the build
otherwise.

| Group | Keys |
| --- | --- |
| Mod settings | `OWT_ModTitle`, `OWT_SettingSelfService`, `OWT_SettingSelfServiceDesc`, `OWT_SettingVolume`, `OWT_SettingWealth` |
| Inspect pane | `OWT_StatusOpen`, `OWT_StatusClosed`, `OWT_Unattended`, `OWT_AtCounterLine`, `OWT_QueueLine`, `OWT_StockLine`, `OWT_ServicesLine`, `OWT_MarkupLine`, `OWT_TillLine`, `OWT_TownLine` |
| Gizmos | `OWT_CmdOpen`, `OWT_CmdOpenDesc`, `OWT_CmdCollect`, `OWT_CmdCollectDesc`, `OWT_CmdLedger`, `OWT_CmdLedgerDesc` |
| Town ledger | `OWT_LedgerTitle`, `OWT_LedgerAppealLine`, `OWT_LedgerAppealBusinessesLine`, `OWT_LedgerAppealGoodsLine`, `OWT_LedgerAppealStandingLine`, `OWT_LedgerAppealMissingLine`, `OWT_LedgerPurseLine`, `OWT_LedgerReputationLine`, `OWT_LedgerTodayLine`, `OWT_LedgerServiceLine`, `OWT_LedgerQuietLine`, `OWT_LedgerLifetimeLine`, `OWT_LedgerShopLine`, `OWT_LedgerShopWalkouts` |
| Stock tab | `OWT_TabStock`, `OWT_TabStockShelves`, `OWT_TabStockEmpty`, `OWT_TabStockShelvesTip`, `OWT_MarkupSlider`, `OWT_MarkupSliderTip`, `OWT_TabStockReputation`, `OWT_TabStockServices`, `OWT_TabStockServiceFixed`, `OWT_TabStockServiceStock`, `OWT_ResetStock` |
| Sending a colonist | `OWT_OrderService`, `OWT_OrderServiceDisabled`, `OWT_ReasonClosed`, `OWT_ReasonRecently`, `OWT_ReasonReserved`, `OWT_ReasonBusy`, `OWT_ReasonUnreachable` |
| Events | `OWT_LetterCustomersLabel`, `OWT_LetterCustomersText`, `OWT_CustomersLeaving`, `OWT_CustomersScared`, `OWT_CustomerWalkedOut`, `OWT_CustomerWalkedOutService`, `OWT_ColonistGaveUp`, `OWT_ColonistNotReached`, `OWT_CounterBusy` |
| Alerts | `OWT_AlertUnattended`, `OWT_AlertUnattendedDesc` |

## Saved state

What survives a save/load, and where it lives.

| Data | Owner | Notes |
| --- | --- | --- |
| Till contents | `CompBusiness` | Deep-saved `ThingOwner`; dropped on destroy |
| Stock filter, open flag, markup | `CompBusiness` | Per business |
| Per-business ledger | `CompBusiness` | Daily figures rolled over at midnight |
| Town reputation, daily + lifetime figures | `TownEconomy` | One per map |
| Today's patron table | `TownEconomy` | Who came today and what befell them, as plain ints. Cleared at midnight, so it is never more than a day of them |
| Per-customer records | `LordJob_ShopVisit` | Saves and dies with the visiting group |
| The line at a counter | *(not saved)* | Rebuilds within a tick of loading, from the patrons' own jobs |
| Wait/serve progress | `JobDriver_PatronizeBusiness` | So a mid-sale save resumes correctly |
| Mod settings | `OldWestTownSettings` | Global, not per save |

`TownEconomy` rebuilds its business register in `FinalizeInit`, because comps register on spawn
and a loaded map spawns them before the map component exists.
