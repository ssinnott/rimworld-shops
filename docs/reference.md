---
title: Reference tables
summary: Every defName, tunable constant, mod setting and translation key the mod ships, in one place.
---

> **This page is for people editing the files.** It lists the mod's internal names, the constants
> behind the numbers, and every translation key — the things you need when patching, translating
> or extending it. If you are here to *play*, the pages under **Playing** and **Systems** explain
> all of the same behaviour without the internal names: [buildings](buildings.md),
> [business kinds](businesses.md), [services](services.md), [the town economy](economy.md),
> [customers](customers.md), [shopkeeping](shopkeeping.md), [outlaws and the law](outlaws.md).

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
| `OWT_FaroTable` | faro table | `OWT_GamblingHall` | *same file* — promoted from `Buildings_MainStreet.xml`; see [changelog](changelog.md) |
| `OWT_CounterBase` | *(abstract parent)* | — | *same file* |
| `OWT_FalseFront` | false front | — | `Defs/ThingDefs_Buildings/Buildings_MainStreet.xml` |
| `OWT_HitchingPost` | hitching post | — | *same file* |
| `OWT_Gallows` | gallows | — | *same file* |
| `OWT_BatwingDoor` | batwing doors | — (`ParentName="Door"`) | *same file* |
| `OWT_StreetFurnitureBase` | *(abstract parent)* | — | *same file* |
| `OWT_SheriffOffice` | sheriff's office | — (not a business; `CompRolePost`) | `Defs/ThingDefs_Buildings/Buildings_Roles.xml` |
| `OWT_CoachDepot` | coach depot | — (not a business; `CompCoachDepot`) | `Defs/ThingDefs_Buildings/Buildings_Stagecoach.xml` |

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
| `OWT_GamblingHall` | gambling hall | 1.00 (0.5–3.0), house edge 0.15 (0.0–0.5) | 1.3 | 1800 | Wager |

### Services

| defName | Label | Worker | Serve ticks | Consumes stock | Self-service |
| --- | --- | --- | --- | --- | --- |
| `OWT_Drink` | drink | `ServiceWorker_Ingest` (Liquor / Joy) | 150 | yes | yes |
| `OWT_Meal` | meal | `ServiceWorker_Ingest` (IsMeal / Food) | 150 | yes | yes |
| `OWT_Haircut` | haircut | `ServiceWorker_Haircut` | 2200 | no | **never** |
| `OWT_Lodging` | lodging | `ServiceWorker_Lodging` | 200 | no (claims a `CompRentableBed` instead) | **never** |
| `OWT_Wager` | wager | `ServiceWorker_Wager` (Joy) | 200 | no | **never** |

### Coach tiers

The [stagecoach line](economy.md#the-stagecoach-line)'s route ladder, `OldWestTown.Stagecoach.CoachTierDef` — pure data, in `Defs/CoachTierDefs/CoachTiers.xml`.

| defName | Label | Min appeal | Arrival ceiling | Purse multiplier | VIP chance |
| --- | --- | --- | --- | --- | --- |
| `OWT_RouteFreightWagons` | irregular freight wagons | 0.5 | 8 days | ×1.25 | 0% |
| `OWT_RouteWeeklyCoach` | weekly coach | 1.5 | 4 days | ×1.6 | 8% |
| `OWT_RouteDailyExpress` | daily express | 3.5 | 2 days | ×2.0 | 20% |

### Jobs

| defName | Driver | Report string |
| --- | --- | --- |
| `OWT_BuyFromShop` | `JobDriver_BuyFromShop` | buying TargetA. |
| `OWT_ManShop` | `JobDriver_ManShop` | minding TargetA. |
| `OWT_ServeDrink` | `JobDriver_UseService` | getting a drink at TargetB. |
| `OWT_ServeMeal` | `JobDriver_UseService` | getting a meal at TargetB. |
| `OWT_ServeHaircut` | `JobDriver_UseService` | getting a haircut at TargetB. |
| `OWT_ServeLodging` | `JobDriver_UseService` | checking in at TargetB. |
| `OWT_ServeWager` | `JobDriver_UseService` | playing a hand at TargetB. |
| `OWT_SleepInRentedBed` | `JobDriver_SleepInRentedBed` | sleeping at TargetA. |
| `OWT_Patrol` | `JobDriver_Patrol` | patrolling TargetA. |
| `OWT_CalmTrouble` | `JobDriver_CalmTrouble` | calming down TargetA. |
| `OWT_RobTill` | `JobDriver_RobTill` | cracking the till at TargetB. |

### Raid strategies

| defName | Label | Worker |
| --- | --- | --- |
| `OWT_StickupStrategy` | stickup | `RaidStrategyWorker_Stickup` |

### Everything else

| defName | Type | Notes |
| --- | --- | --- |
| `OWT_Commerce` | `DesignationCategoryDef` | Build-menu category, sort order 410 |
| `OWT_FrontierCommerce` | `ResearchProjectDef` | 500 cost, Medieval, no prerequisites |
| `OWT_StagecoachLine` | `ResearchProjectDef` | 800 cost, Medieval, prerequisite `OWT_FrontierCommerce` |
| `OWT_Shopkeeping` | `WorkTypeDef` | Natural priority 460, Social skill |
| `OWT_ManShopCounter` | `WorkGiverDef` | Priority 100 in type; needs Manipulation + Talking |
| `OWT_Sheriffing` | `WorkTypeDef` | Natural priority 460, Social skill; `alwaysStartActive` false — only the assigned pawn ever has anything to do here |
| `OWT_PatrolPost` | `WorkGiverDef` | Priority 100 in type; ambient half of suppression; needs Manipulation + Talking |
| `OWT_CalmDownPatron` | `WorkGiverDef` | Priority 200 in type (outranks `OWT_PatrolPost`); reactive half; needs Manipulation + Talking |
| `OWT_Shop` | `DutyDef` | HighPriority hook; buy-then-wander think tree |
| `OWT_ShopCustomers` | `IncidentDef` | Misc, baseChance 3, minRefireDays 0.6 |
| `OWT_StickupDuty` | `DutyDef` | HighPriority hook; fight-then-rob-then-wander think tree — see [outlaws and the law](outlaws.md) |
| `OWT_Stickup` | `IncidentDef` | ThreatBig, baseChance 2, minRefireDays 1.0; mainly fired by `StickupWatch`'s own clock, not this `baseChance` |
| `OWT_FreshHaircut` | `ThoughtDef` | +5 mood, 1.5 days, stack limit 1 |
| `OWT_SleptAtHotel` | `ThoughtDef` | 3 stages (room Impressiveness `< 20` / `< 60` / else), +2/+4/+7 mood, 1.5 days, stack limit 1, granted on waking |
| `OWT_GoldRushCondition` | `GameConditionDef` | `OldWestTown.GoldRush.GameCondition_GoldRush`; see [gold rush](economy.md#gold-rush) |
| `OWT_GoldRushStrike` | `IncidentDef` | Misc, baseChance 1, minRefireDays 45, durationDays 70~80 (an outer safety cap on the bust, not the intended exit — see [gold rush](economy.md#gold-rush)) |

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

### Hospitality bridge — `Compat/HospitalityInterop.cs`, `Compat/HospitalityBridge.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `ScanIntervalTicks` | 250 | How often `HospitalityBridge` scans the map for an idle guest to offer a job to |

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

### Gambling — `Shops/ServiceWorker.cs`, `Shops/CompBusiness.cs`, `Shops/ShopKindDef.cs`, `Shops/TownEconomy.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `defaultHouseEdge` / `houseEdgeRange` (`OWT_GamblingHall`) | 0.15 / 0.0–0.5 | House edge a fresh table starts at, and the band the slider may move within — Markup's twin dial |
| Win-chance formula | `clamp01((1 - HouseEdge) / payoutMultiplier)` | House edge alone decides win probability; the player's expected return per silver staked is exactly `-HouseEdge` for any `payoutMultiplier` |
| `payoutMultiplier` (`ServiceWorker_Wager`) | 2.0 | What a win pays, as a multiple of the stake. XML-tunable, never overridden in the shipped def |
| `joyGainPerHand` (`ServiceWorker_Wager`) | 0.1 | Joy granted for playing a hand at all, win, lose or shortfall — so the Joy need `Desirability` scores a wager against is also the need a wager actually satisfies |
| `lossRowdiness` (`ServiceWorker_Wager`) | 0.2 | Severity an ordinary loss adds to `OWT_Rowdy` — identical to `rowdinessPerServing` on `OWT_Drink` |
| `accusationRowdinessBonus` (`ServiceWorker_Wager`) | 0.15 | Extra severity on top of `lossRowdiness` when a loss also draws a cheating accusation |
| `shortPayRowdinessMultiplier` (`ServiceWorker_Wager`) | 2.0 | Multiplies `lossRowdiness` for a shortfall — the worst rowdiness outcome the mechanic has |
| `baseAccusationChance` / `minAccusationChance` (`ServiceWorker_Wager`) | 0.25 / 0.02 | Chance an unlucky loss draws a cheating accusation, at dealer Social 0 and Social 20 respectively — `lerp`'d between by skill, mirroring `MaxShopkeeperSocialFactor`'s own shape |
| `startingTillSilver` (`OWT_FaroTable`'s `CompProperties_Business`) | 300 | Silver seeded into the till once, on first spawn — without it, a fresh table's first-ever bet has close to a coin-flip chance of winning a payout the till can't cover |
| `costList` Silver (`OWT_FaroTable`) | 300 | What the table costs to build, on top of its stuff cost — pays for the seed above |
| `AccusationMessageCooldownTicks` (`CompBusiness`) | 400 | At most one cheating-accusation message per table in this window — shorter than the walkout-message throttle on purpose, so a skilled-vs-unskilled dealer's accusation frequency stays visible |
| Shortfall reputation hit (`TownEconomy.RecordShortfall`) | −0.08 | The worst single-event reputation hit in the mod — worse than a disturbance's −0.05 |
| `FactionStandingShortfallDelta` (`TownEconomy`) | −0.20 | The worst single-event standing hit in the mod — worse than a walkout's −0.10 |

### Outlaws — `Shops/StickupWatch.cs`, `Incidents/IncidentWorker_Stickup.cs`, `Incidents/RaidStrategyWorker_Stickup.cs`, `Lords/LordToil_Stickup.cs`, `AI/JobDriver_RobTill.cs`, `Shops/CompBusiness.cs`, `Alerts/Alert_StickupRisk.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `MinSilverAtRisk` (`StickupWatch`) | 300 | Below this much silver sitting uncollected across every till, the stickup clock never rolls at all |
| MTB curve (`StickupWatch`) | `lerp(6, 0.75, clamp01((silver - 300) / 2000))` days, ×2 with a sheriff on duty | How the average gap between attempts shortens as uncollected silver climbs |
| `ArrivalCheckInterval` (`StickupWatch`) | 600 ticks | How often the clock is consulted — same cadence as `TownEconomy`'s own arrival clock |
| `AlertThreshold` (`Alert_StickupRisk`) | 150 | Deliberately below `MinSilverAtRisk` — the alert fires before the risk itself is even live |
| Points scaling (`IncidentWorker_Stickup.ResolveRaidPoints`) | `clamp(silver × 0.6, 80, 400)` | Crew size and gear, scaled off silver at risk rather than colony wealth |
| `baseChance` / `minRefireDays` (`OWT_Stickup`) | 2 / 1.0 | The small background trickle on top of `StickupWatch`'s own clock |
| Duration cap (`RaidStrategyWorker_Stickup`) | 20000 ticks, 10000 with a sheriff on duty | Fixed once at raid creation; a sheriff coming on or off duty mid-raid can't retroactively change it |
| `RobRadius` (`LordToil_Stickup`) | 30 | Same value as `LordToil_Shop.ShoppingRadius` |
| `CrackTicks` (`JobDriver_RobTill`) | 180 | The "cracking the till" delay before silver actually moves |
| `RobberyMessageCooldownTicks` (`CompBusiness`) | 400 | At most one robbery message per till in this window — same shape as `AccusationMessageCooldownTicks` |
### Stagecoach — `Shops/TownEconomy.cs`, `Incidents/IncidentWorker_ShopCustomers.cs`, `Stagecoach/CoachTierDef.cs`, `Stagecoach/CoachTierUtility.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| `minAppeal` / `arrivalCeilingDays` / `purseMultiplier` / `vipChance` (`CoachTierDef`) | see [coach tiers](#coach-tiers) | One rung of the route ladder — pure data, per tier |
| `GuaranteedArrivalDue` (`TownEconomy`) | `TicksSinceLastArrival >= CoachTierUtility.CeilingTicks(RouteTier)` | The OR added to the existing MTB roll in `TryAttractCustomers` — never a second, independent roll |
| `CeilingTicks` (`CoachTierUtility`) | `tier.arrivalCeilingDays * 60000 / max(0.25, customerVolume)` | A tier's arrival ceiling in ticks, at the player's own Customer volume setting — same clamp-floor and unit convention as the MTB clock's own `mtbDays` scaling |
| `VipPurseMultiplier` (`IncidentWorker_ShopCustomers`) | 5.0 | Flat multiplier on a VIP passenger's purse, on top of the ordinary appeal-scaled amount — one number for every tier, not an escalating one |

### Gold rush — `GoldRush/GameCondition_GoldRush.cs`, `GoldRush/GoldRushUtility.cs`, `Shops/TownEconomy.cs`, `Shops/ShopPricing.cs`, `Shops/CompBusiness.cs`, `Incidents/IncidentWorker_ShopCustomers.cs`

| Name | Value | Meaning |
| --- | --- | --- |
| Boom duration | 1 quadrum (15 days) | Fixed; `GenDate.DaysPerQuadrum * 60000` ticks, checked every 600 ticks alongside every other clock in this file |
| `BustRecoveryReputation` (`GameCondition_GoldRush`) | 0.45 | Reputation the bust needs to clear before it ends on its own — just under the 0.5 neutral point `RollOverDay` already decays every reputation toward |
| Duration cap (`OWT_GoldRushStrike`) | 70~80 days total | An outer safety net if reputation never clears the bar above; `GameCondition_GoldRush.recoveredByReputation` is what keeps this path from sending a "recovered" letter that isn't true |
| `BoomArrivalMtbDivisor` (`GoldRushUtility`) | 3.0 | Arrivals roughly triple during the boom — divides straight into `TownEconomy.TryAttractCustomers`'s own `mtbDays` |
| `BustArrivalMtbMultiplier` (`GoldRushUtility`) | 2.5 | Arrivals slow, not stop, during the bust — multiplies the same `mtbDays` |
| `ArrivalMtbMultiplier` (`GoldRushUtility`) | see above | 1f (a no-op) whenever no rush is active; folded into `mtbDays` alongside, never into, the stagecoach guarantee's own `CeilingTicks` |
| `BoomPurseMultiplier` (`GoldRushUtility`) | 1.5 | Extra factor on every arriving customer's purse during the boom, stacked with any stagecoach tier or VIP multiplier already applying |
| `InBasketDemandFactor` / `OutOfBasketDemandFactor` (`GoldRushUtility`) | 4.0 / 0.4 | The [demand basket](customers.md#the-demand-basket)'s score multiplier — a 10× spread between an item prospectors want and one they don't, during the boom only |
| `InDemandBasket` (`GoldRushUtility`) | Manufactured, Medicine, a meal, or Liquor | Read loosely against the general store's own flavor text — no confirmed literal "Tools" category exists to check against from this sandbox, so Manufactured stands in for it |
| `GougeSeverity` (`ShopPricing`) | `clamp01((Markup - Kind.defaultMarkup) / (Kind.markupRange.max - Kind.defaultMarkup))` | 0 at a shop's own kind's default markup, 1 at that kind's own ceiling — gouging is the player's choice to push a shop above what's normal for *its* kind, never a flat number that would penalize a saloon just for being a saloon |
| `GougeReputationPenalty` / `GougeStandingDelta` (`TownEconomy`) | −0.03 / −0.03 | Extra reputation and standing cost per sale, scaled by `GougeSeverity`, while a boom is active — the brake on the demand basket swinging shop choice hard enough that price stops mattering on its own |
| `GougeMessageCooldownTicks` (`CompBusiness`) | 60000 (1 day) | At most one gouging warning per shop per day — longer than `AccusationMessageCooldownTicks`/`RobberyMessageCooldownTicks` on purpose, since gouging is a standing choice, not a discrete burst of events |

## Mod settings

`OldWestTownSettings`, saved in RimWorld's mod-settings file.

| Field | Default | Range | Effect |
| --- | --- | --- | --- |
| `allowSelfService` | false | on/off | Customers buy from unattended counters, at −0.005 reputation per sale |
| `customerVolume` | 1.0 | 0.25–3.0 | Scales both the arrival clock and group size |
| `customerWealth` | 1.0 | 0.25–3.0 | Scales the silver each customer carries |
| `hospitalityBridgeEnabled` | true | on/off | Master switch for the [Hospitality bridge](customers.md#hospitality-guests). Only shown, and only consulted, while `HospitalityInterop.Present` is true |
| `hospitalityGuestsCarrySilver` | true | on/off | Whether the bridge tops up a Hospitality guest's purse the way an arriving customer's is. Only shown while the bridge itself is enabled |
| `stickupsEnabled` | true | on/off | Master switch for [the stickup incident](outlaws.md). Off removes the risk from an uncollected till entirely |
| `goldRushEnabled` | true | on/off | Master switch for [the gold rush event](economy.md#gold-rush). Off removes the event from the storyteller entirely |

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
| Hospitality bridge | `OWT_HospitalityDetected`, `OWT_HospitalityNotDetected`, `OWT_SettingHospitalityEnabled`, `OWT_SettingHospitalityEnabledDesc`, `OWT_SettingHospitalitySilver`, `OWT_SettingHospitalitySilverDesc`, `OWT_HospitalityBridgeEngaged` |
| Outlaws | `OWT_LetterStickupLabel`, `OWT_LetterStickupText`, `OWT_TillRobbed`, `OWT_StickupResisted`, `OWT_StickupDeparted`, `OWT_AlertStickupRisk`, `OWT_AlertStickupRiskDesc`, `OWT_RobberyLine`, `OWT_SettingStickupsEnabled`, `OWT_SettingStickupsEnabledDesc` |

| Trouble | `OWT_SaloonTrouble`, `OWT_DisturbanceLine`, `OWT_AlertRowdyPatrons`, `OWT_AlertRowdyPatronsDesc` |
| Gambling hall | `OWT_CmdHouseEdge`, `OWT_CmdHouseEdgeDesc`, `OWT_HouseEdgeSlider`, `OWT_HouseEdgeLine`, `OWT_ShortfallLine`, `OWT_PayoutLine`, `OWT_CheatingAccusation`, `OWT_HouseCantCover`, `OWT_CmdCollectDescWager` |
| Stagecoach line letters and route-tier announcements | `OWT_LetterCoachLabel`, `OWT_LetterCoachText`, `OWT_LetterCoachVIPLabel`, `OWT_LetterCoachVIPText`, `OWT_RouteTierUpLabel`, `OWT_RouteTierUpText`, `OWT_RouteTierDownMessage`, `OWT_RouteTierLostMessage` |
| Coach depot inspect panel | `OWT_DepotTierLine`, `OWT_DepotNextArrivalLine`, `OWT_DepotNextTierLine`, `OWT_DepotMaxTierLine`, `OWT_DepotNoRoute`, `OWT_DepotNoTiers` |
| Gold rush letters, status lines and setting | `OWT_LetterGoldRushLabel`, `OWT_LetterGoldRushText`, `OWT_GoldRushBustBegins`, `OWT_GoldRushBoomStatus`, `OWT_GoldRushBustStatus`, `OWT_GoldRushGougeWarning`, `OWT_LetterGoldRushRecoveredLabel`, `OWT_LetterGoldRushRecoveredText`, `OWT_SettingGoldRushEnabled`, `OWT_SettingGoldRushEnabledDesc` |

## Saved state

What survives a save/load, and where it lives.

| Data | Owner | Notes |
| --- | --- | --- |
| Till contents | `CompBusiness` | Deep-saved `ThingOwner`; dropped on destroy |
| Stock filter, open flag, markup, house edge | `CompBusiness` | Per business; house edge is inert for every kind but a gambling hall |
| Per-business ledger, including shortfalls, payouts, robberies and silver stolen | `CompBusiness` | Daily figures rolled over at midnight |
| Town reputation, daily + lifetime figures | `TownEconomy` | One per map |
| Per-faction standing | `TownEconomy` | Sparse `Dictionary<Faction, float>`; a save with no `standings` node reads every faction as `Reputation`, exactly like the untracked case |
| Per-customer records, including a checked-in guest's rented bed | `LordJob_ShopVisit` | Saves and dies with the visiting group |
| Stickup crew state (faction, town center, duration, arrival tick) | `LordJob_Stickup` | Only ever created going forward — no old save can have one running |
| Uncollected-till total | `StickupWatch` | Not persisted at all — a live sum over `TownEconomy.Shops`, recomputed on every read |
| Wait/serve progress | `JobDriver_PatronizeBusiness` | So a mid-sale save resumes correctly |
| Sleep progress (`ticksAsleep`) | `JobDriver_SleepInRentedBed` | So a mid-stay save resumes correctly |
| Current guest, selling desk | `CompRentableBed` | References only; released if the guest is dead on load |
| Sheriff assignment | `CompRolePost` (vanilla `CompAssignableToPawn`) | Persisted by the base class itself; `CompRolePost` adds no `ExposeData` of its own |
| On-duty flag (`lastOnDutyTick`/`lastOnDutyPawn`) | `CompRolePost` | Deliberately **not** persisted, mirroring `CompBusiness`'s own staff flag — re-established within moments once the sheriff's patrol job re-ticks after a reload |
| Whether the bridge has announced itself yet (`hasAnnouncedBridge`) | `HospitalityBridge` | One per map. Absent (reads `false`) on any save from before this stage — the same "a new sparse field just reads as the honest default" story `TownEconomy`'s per-faction standing already tells |
| Per-`(pawn, shop)` cooldown table | `HospitalityBridge` | Deliberately **not** persisted, same reasoning as `CompRolePost`'s on-duty flag above — a reload starts every guest's cooldown fresh, which can only make the bridge briefly more generous, never stuck |
| Guarantee clock (`lastArrivalTick`) | `TownEconomy` | Absent on an old save, reads as `0` — `TicksSinceLastArrival` then reads as the entire elapsed game time, the same "safe, a little eager, never stranded" shape `LordJob_ShopVisit.groupArrivedTick` already ships with. The next arrival, organic or guaranteed, re-anchors it |
| Last-announced route tier (`lastAnnouncedTier`) | `TownEconomy` | `Scribe_Defs.Look`. Absent on an old save, reads as `null` — indistinguishable from "no depot has ever changed tier," so a reload can never spuriously re-announce one |
| Bust phase flag (`bustStarted`) | `GameCondition_GoldRush` | Only ever created going forward, like `LordJob_Stickup` — no old save can have one. `recoveredByReputation` is deliberately **not** persisted alongside it: it is only ever true for the instant between being set and the `End()` call two lines below it, in the same synchronous method, so no save can land on a tick where its value matters |
| Mod settings | `OldWestTownSettings` | Global, not per save |

`TownEconomy` rebuilds its business register in `FinalizeInit`, because comps register on spawn
and a loaded map spawns them before the map component exists. `FalseFrontRegistry` rebuilds its
own facade list the same way, for the same reason. Route tier and depot existence need no such
rebuild — both are computed live off `CoachTierUtility`, never cached or registered.
