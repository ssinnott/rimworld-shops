---
title: Changelog
summary: What changed, when, and what it means for a save in progress.
---

All notable changes to Old West Town are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/): changes are grouped under **Added**,
**Changed**, **Fixed**, **Removed** and **Save compatibility**.

> **On version numbers.** The mod has not been published yet, so there are no release tags. Work
> is grouped by the [roadmap](roadmap.md) stage that produced it. The first published build takes
> a version number, and the **Unreleased** section becomes it.

**Every change to the mod belongs here.** Add a line under *Unreleased* in the same commit as the
change itself, and update the [wiki page](contributing.md#the-workflow) it affects.

---

## Unreleased

### Added

- **The wiki now also covers stage 4** — town roles. It shipped on this branch before the wiki
  caught up to it, the same way the batch below did:
  [sheriff's office](buildings.md#sheriffs-office), [sheriffing](shopkeeping.md#sheriffing) and
  how it differs from the Shopkeeping work type, and
  [trouble at the saloon](customers.md#trouble-at-the-saloon-and-the-gambling-hall) — the
  rowdiness hediff, the disturbance it fires, and the two ways to suppress it — plus the matching rows in the
  [reference tables](reference.md), the [code map](architecture.md) and its
  [known risks](architecture.md#known-risks), and a new [gallery](art.md) entry for the sheriff's
  office art. `tools/validate_docs.py`'s `ART_SOURCES` never watched
  `Textures/Things/Building/Roles` at all, which is why that art shipped invisible to the gallery
  check instead of failing loudly — it now does.
- **The wiki now fully covers stages 2, 3, 5 and 6** — services, lodging, per-faction standing,
  and the main-street content pass. The last three shipped on this branch before the wiki
  existed, so this is the wiki catching up rather than the mod changing: [hotel desk and hotel
  bed](buildings.md#hotel-desk), the [hotel business kind](businesses.md#hotel) and its
  [lodging service](services.md#lodging), [overnight guests](customers.md#which-faction-turns-up)
  and evictions, [standing with a faction](economy.md#standing-with-a-faction), and
  [main-street buildings and terrain](buildings.md#main-street) including
  [curb appeal](economy.md#curb-appeal) — plus the matching rows in the
  [reference tables](reference.md), the [code map](architecture.md) and its
  [known risks](architecture.md#known-risks), and new [gallery](art.md) entries for every
  texture involved. `tools/validate_docs.py`'s `ART_SOURCES` now also watches
  `Textures/Things/Building/MainStreet` and `Textures/Terrain/Surfaces`, which is what
  caught the gallery gap in the first place.
- **This wiki.** A GitHub Pages site under `docs/`, covering every building, business, service
  and system in the mod — [buildings](buildings.md), [business kinds](businesses.md),
  [services](services.md), [the town economy](economy.md), [customers](customers.md),
  [shopkeeping](shopkeeping.md), a full [reference table](reference.md) of defs, tunable
  constants, settings and translation keys, a [code map](architecture.md), an
  [extension guide](extending.md), and this changelog.
- **An [art gallery](art.md)**: every shipped texture at every facing, each building's palette
  with the reasoning behind it, the five-step recipe `tools/make_textures.py` draws from, the
  mod-listing preview, and an explicit account of what the mod *doesn't* draw — it adds no items
  of its own, and reuses vanilla's gizmo icons deliberately.
- `tools/validate_docs.py`, wired into CI: fails the build if a def, source file or translation
  key exists without being documented, if an internal wiki link is broken, if a page is missing
  from the sidebar, if the changelog has no *Unreleased* section, or if the art gallery is
  missing a texture or showing a stale copy of one. `--sync-art` applies the one fix that is
  mechanical. See [keeping the wiki honest](contributing.md#keeping-the-wiki-honest).
- `.github/workflows/pages.yml`, which builds the wiki on every pull request and deploys it on
  every push to `main`.
- `CLAUDE.md`, recording the repository's layout, commands and conventions — including the rule
  that a change to the mod updates the wiki and this changelog in the same commit.
- **The Hospitality bridge** (`Compat/`) — the last staged item, and the first one built against
  a mod this codebase has never had installed, decompiled, or run against even once.
  `HospitalityInterop` detects a loaded Hospitality install by scanning loaded assemblies for one
  named `"Hospitality"` (a guess, not a verified fact) and recognizes its guests structurally —
  by which assembly owns a pawn's `Lord`/`LordJob`, or any of their `ThingComp`s — rather than by
  naming a single Hospitality type or member. `HospitalityBridge`, a new per-map
  `MapComponent`, periodically offers one shopping trip (goods, a drink, a meal or a haircut —
  never a room; Hospitality is already housing them) to an idle Hospitality guest through
  `Pawn_JobTracker.TryTakeOrderedJob`, the same door a player's own forced order already uses,
  gated on the guest's own AI having already declared it has nothing better to do. Reuses
  `JobGiver_BuyFromShop`'s scoring (now `PickShoppingJob`, extracted with a new
  `lodgingAllowed` parameter) and `IncidentWorker_ShopCustomers.GivePurse` completely unmodified.
  Two new settings, `hospitalityBridgeEnabled` and `hospitalityGuestsCarrySilver` (both default
  on, both hidden unless Hospitality is actually detected), and an always-visible settings-window
  status line reporting detection either way. If the assembly-name guess is wrong, the bridge is
  permanently and silently inert — indistinguishable from Hospitality not being installed at all.
  See [Hospitality guests](customers.md#hospitality-guests),
  [the design notes](DESIGN.md#the-hospitality-bridge) and
  [the code map's known risks](architecture.md#known-risks) for the full account of what is, and
  isn't, verified here.
- **[Outlaws and the law](outlaws.md)** — a rich town becomes a target. `StickupWatch`, a new
  per-map `MapComponent`, sums every registered business's uncollected till silver and rolls an
  MTB clock that shortens as that total climbs past 300, firing `OWT_Stickup` through the
  storyteller exactly the way `TownEconomy` already fires its own arrival incident.
  `Alert_StickupRisk` shows the risk building well before the clock is even live. `OWT_Stickup`
  (`IncidentWorker_Stickup`) subclasses `IncidentWorker_RaidEnemy` rather than hand-rolling a
  raid — faction resolution, pawn generation, gear and the arrival letter's send-off all stay
  vanilla's own `base.TryExecuteWorker`, untouched — and overrides only what turns an ordinary
  raid into a stickup: crew size and gear scaled off silver at risk rather than colony wealth
  (capped small either way), a forced `OWT_StickupStrategy` (`RaidStrategyWorker_Stickup`) and
  walk-in arrival, and the letter's own copy. `LordJob_Stickup`/`LordToil_Stickup` are a
  near-twin of `LordJob_ShopVisit`'s own flat graph, hostile instead of paying: `OWT_StickupDuty`
  runs vanilla's own `JobGiver_AIFightEnemies` ahead of the new `JobGiver_RobTill`/
  `JobDriver_RobTill` (job `OWT_RobTill`), so self-defense always wins first, and any pawn
  getting shot at routs the whole crew rather than finishing the job.
  `ShopTransaction.RobTill` empties a till completely into the thief's inventory, structurally
  incapable of over-drawing it the same way every other till primitive already is;
  `CompBusiness` gains a robbery ledger (`robberiesToday`/`lifetimeRobberies`/`stolenToday`/
  `lifetimeStolen`) alongside its existing sale and shortfall figures.
  `JobDriver_RobTill` deliberately does **not** implement `IBusinessPatron` — the marker that
  would otherwise make a colonist get dispatched to staff the very counter being robbed, and
  make the waiting-customers alert misread an active robbery as an ordinary queue. The
  step-4 [sheriff](shopkeeping.md#sheriffing) is the mechanic's only counterplay lever beyond
  ordinary self-defense: being on duty roughly halves both how often a stickup happens
  (`StickupWatch`'s own clock) and how long one lasts once it starts
  (`RaidStrategyWorker_Stickup`, read once at raid creation) — two passive reads of the same
  `TroubleUtility.AnySheriffOnDuty` flag that already suppresses saloon rowdiness, never a new
  job or a reference to any raider. A downed raider's `LordJob_Stickup.GuiltyOnDowned` is what
  makes capturing and ransoming one completely ordinary, unmodified vanilla prisoner mechanics —
  deliberately the entire "jail" story this mechanic tells; a wanted board with bounty quests on
  a recurring outlaw leader was cut for the same reason RimWorld's quest system was weighed and
  declined elsewhere in this project. A new `stickupsEnabled` setting (default on) turns the
  whole mechanic off. See [outlaws and the law](outlaws.md) and
  [the code map's known risks](architecture.md#known-risks) for what's tuned but untested.

### Changed

- `WorkGiver_ManShop.AnyCustomerNear` now also recognizes a customer by the `IBusinessPatron`
  marker interface, not only by the `OWT_Shop` duty — what a bridged Hospitality guest actually
  carries, since the bridge never assigns one. `CompBusiness.CellFreeFor` and
  `Alert_CustomersWaiting` already keyed off `IBusinessPatron` directly and needed no change.
- `JobGiver_BuyFromShop.TryGiveJob`'s scoring pass is now `PickShoppingJob`, a separate
  `internal static` method taking a `lodgingAllowed` parameter (default `true`) — so
  `HospitalityBridge` can reuse the identical scan instead of duplicating it. No behavior change
  for the duty-driven caller.
- `IncidentWorker_ShopCustomers.GivePurse` is now `internal` rather than `private`, so
  `HospitalityBridge` can give a bridged guest the identical arrival top-up a native customer
  gets. Same formula, same existing callers, same behavior for them.

- **Gambling hall** (`OWT_GamblingHall`) — a fifth business kind, and the first where the customer
  can walk away with *more* silver than they sat down with. Sells one new service, **wager**
  (`OWT_Wager`, `ServiceWorker_Wager`, job `OWT_ServeWager`): a hand priced and paid for exactly
  like a haircut, then resolved as a win, a loss, or — rarely — a shortfall. **House edge**, a new
  dial on `ShopKindDef`/`CompBusiness` living right next to markup, is exactly the fraction of
  every silver wagered the house keeps on average, by construction, for any payout multiple. A win
  pays straight out of the business's own till (`ShopTransaction.PayOutFromTill`,
  `CompBusiness.TakeFromTill`) — the first place in the mod money leaves a till rather than
  entering it, structurally capped at whatever the till actually holds. A loss makes that gambler
  a little rowdier, the same `OWT_Rowdy` hediff a saloon's drink already uses — every rowdiness-
  capable service is now gated on the new `ServiceWorker.CanCauseTrouble` rather than a fixed
  `RowdinessPerUse`, since a wager's outcome-dependent rowdiness can't be read off one constant —
  and an unlucky loss can additionally draw a Social-skill-gated **cheating accusation** against
  the dealer. A shortfall — the table winning a hand and then not being able to pay it in full —
  is the worst reputation and standing hit anywhere in the mod, and force-closes the table until
  reopened by hand. See [businesses](businesses.md#gambling-hall), [services](services.md#wager)
  and [the till as a bankroll](economy.md#the-till-as-a-bankroll).
- **A played hand relieves boredom, not just money.** `ServiceWorker_Wager.ApplyEffect` grants a
  flat `joyGainPerHand` to the customer's Joy need on every hand — win, loss or shortfall — the
  same unconditional shape `ServiceWorker_Ingest` already uses for nutrition. `Desirability`
  already scored a wager against Joy; nothing previously satisfied it, so repeat play now tapers
  off the same way another round at the bar already does.
- **`OWT_FaroTable` is promoted, not recreated.** The faro table shipped in stage 6 as pure street
  furniture (`Buildings_MainStreet.xml`, vanilla's own `CompGatherSpot`, no wager); it now lives in
  `Buildings_Commerce.xml` as a real gambling-hall business, with a new **300-silver** cost on top
  of its stuff cost that seeds its own till (`startingTillSilver`) so its first customer isn't a
  coin flip to be shorted. Same defName throughout, same art — one faro table in the build menu,
  not two.

### Changed

- `OWT_AlertRowdyPatronsDesc`, `OWT_CmdAssignSheriffDesc` and `OWT_Rowdy`'s description dropped
  their saloon-specific wording now that a gambling hall can generate the same trouble a saloon
  does — see [trouble at the saloon and the gambling
  hall](customers.md#trouble-at-the-saloon-and-the-gambling-hall).

- `docs/DESIGN.md` is now the *why* only. Its component map, roadmap and known-risk list moved
  into the wiki so each has a single home: [code map](architecture.md),
  [roadmap](roadmap.md), [known risks](architecture.md#known-risks). The design reasoning itself
  is unchanged.
- `README.md` now points at the wiki rather than restating it.
- **The player-facing wiki pages are written in the player's language.** [Home](index.md),
  [getting started](getting-started.md), [buildings](buildings.md),
  [business kinds](businesses.md), [services](services.md),
  [the town economy](economy.md), [customers](customers.md), [shopkeeping](shopkeeping.md), the
  [art gallery](art.md) and the [roadmap](roadmap.md) no longer quote defNames, C# class names,
  field names or tick counts at a reader who only wants to run a shop: durations are given in
  game time, buttons by their labels, and mechanics by what they do. The technical detail moved
  to the pages written for it — the business-kind and service field tables and the worker classes
  to [adding content](extending.md#add-a-business-kind), the texture recipe and the art
  generator's table to [contributing](contributing.md#generating-building-art) — and the
  [reference tables](reference.md) still list every internal name, now under **Under the hood**
  in the sidebar rather than **Systems**.
- The [town economy](economy.md#reputation) page now describes what reputation actually does to
  prices — a good name sells 10% under the markup you set, a bad one 15% over — rather than the
  inverse the page previously claimed.

- Copies of the shipped art under `docs/assets/textures/`, checked byte-for-byte against
  `Textures/` and `About/`. One copy makes the gallery work both on the published site and when
  someone reads `docs/art.md` on GitHub; the check is what stops it going stale.

### Save compatibility

- **Outlaws and the law** is additive throughout, with a zero-migration precedent already
  established elsewhere in this codebase for every piece of it. `StickupWatch` is a brand-new
  `MapComponent` — RimWorld auto-instantiates it on any map, old or new, and it carries no
  persisted state at all (every value is a live sum), so there's nothing to migrate, not even the
  "defaults to false" story a stateful comp would need. `CompBusiness` gains four new `int`
  fields (`robberiesToday`/`lifetimeRobberies`/`stolenToday`/`lifetimeStolen`), each defaulting to
  `0` on a save with no such node — identical to how `shortfallsToday`/`lifetimePayouts` already
  behaved for a save from before the gambling hall existed. `LordJob_Stickup` is only ever
  created going forward, so no old save can have one running. The new `stickupsEnabled` setting
  defaults to `true`, the same precedent `hospitalityBridgeEnabled` already set.
- No gameplay change from the documentation work above. Safe to drop into a save in progress.
- The Hospitality bridge adds exactly one new persisted field: `HospitalityBridge.hasAnnouncedBridge`,
  a plain `bool` on a brand-new per-map `MapComponent`. An existing save gets a fresh instance
  with this defaulted to `false`, the same way `TownEconomy` and `FalseFrontRegistry` were both
  introduced with no migration needed. The bridge's own per-`(pawn, shop)` cooldown table, and
  its per-guest "already given a silver top-up" set, are both deliberately never persisted at
  all. Safe to drop into a save in progress either way, with or without Hospitality installed.

- No gameplay change from the wiki and tooling work above. Safe to drop into a save in progress.
- **Except a save with an `OWT_FaroTable` already placed.** It now loads as a live, staffable
  gambling-hall business instead of decoration — every `CompBusiness` field it gains initializes
  to its correct, safe default the same way `Markup` already does for any table that never had one
  before, but `startingTillSilver` only seeds a *fresh* spawn, not a respawn from a load, so a
  pre-existing table opens with no bankroll of its own. That's the same "first winner might be
  shorted" situation seed capital exists to avoid, but only for a table placed before this update.
  Given the mod has no players yet, accepted as a documented, pre-release-only risk rather than
  patched with bespoke migration code — see [known risks](architecture.md#known-risks).

---

## Stage 4 — Town roles — 2026-08-23

The saloon generated no trouble at all before this stage — a sheriff needed something to
suppress before the badge itself could mean anything.

### Added

- **[Sheriff's office](buildings.md#sheriffs-office)** (`OWT_SheriffOffice`) — a post, not a
  fifth business kind: no `CompProperties_Business`, so it never registers with `TownEconomy`
  and never enters the appeal or reputation math. Assigned through `CompRolePost`
  (`CompProperties_RolePost`), built on vanilla's own `CompAssignableToPawn` — the same idiom a
  throne or a grave already uses for "this pawn, and only this pawn, owns this."
- **[Sheriffing](shopkeeping.md#sheriffing)** (`OWT_Sheriffing`): a work type that, unlike
  Shopkeeping, only ever does anything for the one colonist actually assigned to the post.
  `WorkGiver_Patrol` / `JobDriver_Patrol` (job `OWT_Patrol`) send the sheriff to stand the post —
  the ambient half of suppression, halving how fast rowdiness accrues town-wide while they're on
  duty. `WorkGiver_CalmTrouble` / `JobDriver_CalmTrouble` (job `OWT_CalmTrouble`) are the reactive
  half: they target one specific rowdy patron directly and walk the sheriff over to calm them
  down, granting the same 35 Social XP a shopkeeper earns for a served sale.
- **[Trouble at the saloon](customers.md#trouble-at-the-saloon-and-the-gambling-hall)**: a new `OWT_Rowdy` hediff that
  a round of [drink](services.md#drink) — never a meal — nudges upward, decaying on its own via
  vanilla's own `HediffCompProperties_SeverityPerDay`. Crossing its top stage fires a scripted
  **disturbance** (`TroubleUtility.Notify_ServiceRound`): a message, a reputation hit worse than
  a walkout (`CompBusiness.RecordDisturbance`, −0.05 against a walkout's −0.02), and the
  offending patron stops buying for the rest of their visit. A skilled shopkeeper behind the bar
  slows the climb on their own; an on-duty sheriff slows it further, town-wide; an unstaffed bar
  gets neither discount.
- **`Alert_RowdyPatrons`**: fires while a patron is "getting loud" and still calmable — the
  sheriff's real window before a disturbance fires unattended. Mirrors `Alert_CustomersWaiting`'s
  shape.

### Deliberately deferred

- **Barkeep** and **banker**, the other two roles this stage originally named. Barkeep folded
  into the existing Shopkeeping loop as the skilled-shopkeeper discount above, rather than a
  second post — there was nothing left for a separate badge to do. Banker is cut outright: there
  is no bank yet for one to run.

---

## Stage 2 — Services — 2026-08-23

Businesses that sell **time**, not just goods. The interesting half of a town.

### Added

- **[Services](services.md).** A `ServiceDef` is a thing a business sells that isn't an item off
  a shelf, priced and paid through the same seam as a sale.
  - **Drink** (`OWT_Drink`) — a Liquor item off the saloon's own shelves, feeding Joy.
  - **Meal** (`OWT_Meal`) — any meal, feeding Food. Same worker class as Drink, parameterized
    differently, because a drink and a meal are one mechanic with a different filter.
  - **Haircut** (`OWT_Haircut`) — pure time, no stock at all: a `+5` mood thought for 1.5 days
    **and a visibly different hairstyle**.
- **[Barber chair](buildings.md#barber-chair)** (`OWT_BarberChair`) and the
  **[barber shop](businesses.md#barber-shop)** business kind — the first business that stocks
  nothing.
- Three `ServiceWorker` classes: `ServiceWorker_Ingest`, `ServiceWorker_Thought` and
  `ServiceWorker_Haircut`. [Adding a service](extending.md#add-a-service) usually needs no new
  code.
- `JobDriver_PatronizeBusiness`: the shared walk / wait / patience / walkout shape, with the
  goods purchase and the service visit as its two concrete forms.
- `IBusinessPatron`, a marker interface that lets the business layer recognize "a pawn is
  patronizing something" [without depending on the AI namespace](architecture.md#boundaries-worth-keeping).
- `ServiceValue` in town appeal, weighted ×30 so a stock-free service is not
  [drowned out](economy.md#appeal) by a normalization tuned for physical stock.
- Customers now arrive with food need randomized to 40%–90%, so a meal service has genuinely
  hungry customers to sell to.
- The **Town ledger** gizmo: appeal, reputation, today's sales and walkouts, and every business's
  takings, in one dialog.
- `tools/refdump`, which reads RimWorld's reference assemblies so `validate_defs.py` can check
  vanilla type names for real instead of downgrading to a note.
- `tools/make_textures.py`, which draws every building's art from one table — and a CI job that
  fails if a building in that table has no art on disk.
- Five thematic expansions added to the [roadmap](roadmap.md#beyond-the-staged-plan--thematic-expansions):
  gambling hall, outlaws and the law, stagecoach line, gold rush, rival towns.

### Changed

- `CompShopCounter` generalised into **`CompBusiness`**: a building can now offer goods,
  services, or both. Staffing, appeal and the ledger all became kind-agnostic in the process.
- `WorkGiver_ManShop` staffs any business with something to offer, rather than only a stocked
  counter.
- Queueing customers now fan out to free cells around the customer cell, filtered to the same
  room indoors, instead of stacking on one tile.
- The *Customers waiting* alert counts waiting **customers** while jumping the camera between
  **counters**, and stays silent for a patron whose service honours the self-service setting.

---

## Stage 6 — Old west content pass — 2026-08-23

Boardwalk terrain and five pieces of street furniture, dressing a main street that stages 1–5
already made functional.

### Added

- **[Boardwalk](buildings.md#boardwalk)** (`OWT_Boardwalk`) terrain: no movement penalty, a
  couple of points of Beauty, 2 wood a tile.
- **[False front](buildings.md#false-front)** (`OWT_FalseFront`, `CompFalseFront`,
  `FalseFrontRegistry`) — the only mechanical piece in the set. Folds a small, capped
  [curb-appeal](economy.md#curb-appeal) bonus (+0.10 for one qualifying facade near a shop's
  customer-facing side, +0.15 for two or more) into `ShopPricing.ValueAppeal`, the same function
  `JobGiver_BuyFromShop` already calls to score every candidate. `FalseFrontRegistry` is a
  `MapComponent` roster of spawned facades, registered the same way `TownEconomy` registers shops,
  so the scoring hot path never scans the whole map.
- **[Hitching post](buildings.md#hitching-post)** (`OWT_HitchingPost`), **[gallows](buildings.md#gallows)**
  (`OWT_Gallows`, the one deliberately Beauty-negative piece), and **[faro table](buildings.md#faro-table)**
  (`OWT_FaroTable`, with vanilla's own `CompGatherSpot` so idle colonists gather at it) — all
  honestly decorative. The faro table deliberately does not gamble; wagering is reserved for the
  [gambling hall](roadmap.md#beyond-the-staged-plan--thematic-expansions).
- **[Batwing doors](buildings.md#batwing-doors)** (`OWT_BatwingDoor`) — a reskin of vanilla's own
  `Door` that also undercuts its `costStuffCount`, so the swinging half-doors genuinely cost less
  lumber, not just less light-blocking.

---

## Stage 5 — Reputation with depth — 2026-08-23

Split the town's single reputation number into a per-faction dimension, so a faction can become a
regular.

### Added

- **[Standing with a faction](economy.md#standing-with-a-faction)**: a sparse per-`Faction`
  `standings` dictionary on `TownEconomy`, alongside the untouched, town-wide `reputation` float.
  An untracked faction reads as the town's own reputation (`StandingWith`), so an existing save
  needs nothing seeded. A staffed sale nudges that customer's own faction's standing sharply
  upward (`FactionStandingSaleDelta`, +0.05); any walkout, hotel eviction included, nudges it
  sharply downward (`FactionStandingWalkoutDelta`, −0.10); a self-service sale touches only the
  town-wide number, exactly as before. Standing decays toward the town's own name at the same 5%
  daily rate reputation itself does.
- **Which faction arrives is now biased by standing.** `IncidentWorker_ShopCustomers` lets
  vanilla's own `TryResolveParms` run to completion untouched, then re-picks the faction
  afterward by weighted draw over `ArrivalWeight` (`lerp(0.15, 3, standing)`) — never by
  pre-seeding `parms.faction`, since `TryResolveParms` is confirmed non-virtual and whether it
  would honor a pre-set faction can't be proven from reference-assembly metadata alone.
  `IsEligibleFaction` excludes the player, hostile factions, and anyone with no settlement to
  actually send customers from.
- **The town ledger** names the single best and worst *recorded* relationship
  (`OWT_LedgerRegularLine`, `OWT_LedgerColdLine`) once either has genuinely diverged from the
  town's own reputation by more than `LedgerStandingDivergenceThreshold` (0.1), and stays silent
  otherwise — a fresh game's ledger is unchanged.

### Save compatibility

- An existing save has no `standings` node at all; every faction it already knew about simply
  starts exactly where its own reputation number already put it.

---

## Stage 3 — Lodging — 2026-08-23

Rentable beds: guests who pay for a room up front and stay past midnight.

### Added

- **[Hotel desk](buildings.md#hotel-desk)** (`OWT_HotelDesk`) and **[hotel bed](buildings.md#hotel-bed)**
  (`OWT_HotelBed`, `CompRentableBed`) — a fourth business kind, **[hotel](businesses.md#hotel)**
  (`OWT_Hotel`), staffed by the existing Shopkeeping work type with no new staffing code at all.
- **[Lodging](services.md#lodging)** (`OWT_Lodging`, `ServiceWorker_Lodging`): the first service
  whose effect outlives the transaction. Check-in reuses `JobDriver_UseService` /
  `ShopTransaction.TryServe` completely unmodified; `ServiceWorker_Lodging.ApplyEffect` is the
  first worker to return a `Thing` — the bed it just booked via `ShopStock.ChooseVacantBed` — for
  `JobDriver_UseService.CompleteService` to hand to the guest's own `CustomerRecord.rentedBed`.
  One paid night per transaction; no multi-night pre-booking, no unstaffed nightly billing.
- **`JobGiver_SleepInRentedBed` / `JobDriver_SleepInRentedBed`** (jobs `OWT_ServeLodging`,
  `OWT_SleepInRentedBed`): a checked-in guest heads to bed once genuinely tired, sleeps until
  rested (or a hard tick cap), and gains the **`OWT_SleptAtHotel`** mood thought — staged by the
  room's own Impressiveness, and granted on waking rather than at check-in, the only service in
  the mod whose experience is deferred past payment.
- **`Trigger_VisitComplete`** replaces `LordJob_ShopVisit`'s flat `Trigger_TicksPassed`: the group
  can't leave town while any member's `CustomerRecord.rentedBed` is still set. For a group with
  nobody lodging — still the overwhelming majority of visits — this is bit-for-bit the trigger it
  replaces. New check-ins are cut off once the group's base visit duration has elapsed
  (`PastCheckInCutoff`), so this can't stretch a visit indefinitely.
- **Eviction**: a bed's own **Evict guest** gizmo, a deconstructed bed, or a colonist simply
  climbing into an occupied one all end a stay the same way — no refund, and the same reputation
  hit as a walkout (`OWT_GuestEvicted`).
- Inspect-pane additions: a hotel desk's **Rooms** line (`OWT_RoomsLine`, vacant of total), and a
  hotel bed's occupant / **Evict guest** button (`OWT_BedVacant`, `OWT_BedOccupiedBy`,
  `OWT_CmdEvictGuest`).

### Deliberately deferred

- Multi-night pre-booking, unstaffed nightly billing, per-desk room association (any hotel desk
  currently offers any vacant bed on its own sales floor), vanilla bed ownership, and private
  suites.

---

## Stage 1 — Vertical slice — 2026-08-23

A shop that a customer can walk into and buy something from.

### Added

- **[Shop counter](buildings.md#shop-counter)** and **[saloon bar](buildings.md#saloon-bar)**
  under a new **Commerce** build category, unlocked by the
  **[Frontier commerce](getting-started.md#before-anything-else)** research.
- **A sales floor is a room.** Anything sellable in the counter's room is on display; outdoors it
  falls back to a radius, so a market stall trades too.
- **Per-business stock control** through a Stock tab that reuses vanilla's storage-filter widget.
- **[Player-set prices](economy.md#pricing).** A markup slider over each business kind's own band.
  Undercutting a rival genuinely pulls customers away from it.
- **A [Shopkeeping work type](shopkeeping.md).** Colonists stand the counter and serve; serving
  trains Social.
- **[Real transactions](architecture.md#boundaries-worth-keeping).** Silver moves out of the
  customer's inventory and into the counter's till. Collect the takings with a gizmo;
  deconstructing a counter drops its till rather than voiding it.
- **[Walkouts](customers.md#walkouts).** An unattended counter makes shoppers wait, then leave —
  dropping any goods and costing town reputation. An alert fires while they are still queueing.
- **[A town economy](economy.md).** Appeal from distinct stocked businesses, goods on display and
  reputation; appeal drives its own arrival clock and how much silver customers carry.
- `LordJob_ShopVisit`, a deliberately [flat lord graph](DESIGN.md) — shopping, then exit — with
  per-customer records living on the lord rather than the pawn.
- Three [mod settings](reference.md#mod-settings): allow self-service, customer volume, customer
  wealth.
- `tools/validate_defs.py` and a CI workflow that runs it alongside a full build.

### Known at ship

- **None of this has been run in RimWorld.** It compiles against the 1.6 reference assemblies and
  passes the static checks, but job drivers, lord graphs and duty think trees are exactly the code
  static checking can't validate. See [known risks](architecture.md#known-risks).
- Textures are programmer-art placeholders.
