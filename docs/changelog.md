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

### Changed

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
- The [town economy](economy.md#reputation) page's account of reputation and prices was corrected
  twice, in opposite directions, and the second correction is the one that stands. The page had
  described a good name as charging *more*; it was rewritten to match the code, which charged
  10% *less*; and the code was then fixed, because the direction it ran was a transcription error
  against the intent stated in its own comment. A good name charges more. See **Fixed** below.

- Copies of the shipped art under `docs/assets/textures/`, checked byte-for-byte against
  `Textures/` and `About/`. One copy makes the gallery work both on the published site and when
  someone reads `docs/art.md` on GitHub; the check is what stops it going stale.

### Fixed

- **A purse and a price list are the same rule now.** The customer AI sized an order against a
  per-unit price rounded to whole silver while the counter charged the rounded whole-order total,
  so a sale the AI had judged affordable could be refused — and the trim meant to rescue it
  re-derived the same order and refused again, forever. Simulated over 45,000 visits, 22% of
  customers ended a visit shuttling one stack between shelf and counter without ever spending
  their silver; at the default markup it reached 58%.
- **Reputation runs the right way, and moves at all.** It was credited per receipt, so fifty
  sales — one busy afternoon — crossed its whole range and pinned it near the ceiling by day
  three, leaving both things that read it reading a constant. It is now a nightly verdict on how
  the town treated the people who actually turned up: one verdict per customer per day whatever
  they bought. The price factor's direction was also inverted against its own stated intent, so a
  well-liked town quietly discounted 10% and a hated one surcharged 15%. A good name now charges
  up to 10% more, and mostly buys *who comes* rather than what they pay.
- **Looking at a shop no longer changes the game.** Town appeal was recomputed on demand, and the
  counter's inspect pane asks every rendered frame — a path that reached `Rand`, RimWorld's shared
  seeded stream. Having a saloon selected advanced it sixty times a second. Appeal is now a survey
  the town takes every 60 ticks.
- **Appeal counts what the town offers.** A sales floor shared by two counters was counted once
  per counter; goods were priced at the marked-up shelf price, so raising prices drew more and
  richer customers for free; and a repeat shop-front's service escaped the repeat discount, so
  eight empty barber chairs out-drew a stocked three-trade street.
- **Reacting to the customers-waiting alert works.** A walkout used to strike that counter off
  for the rest of the visit, so staffing it afterwards could not win anyone back — while the
  ex-customers still held a shopkeeper there. The refusal now lifts the moment somebody stands at
  the counter, and the staffing scan ignores visitors who have spent their last coin or are asleep.
- **Closing time finishes the sale it is working** instead of evicting a customer a tick from
  paying — worst at the barber's, where a 2,200-tick haircut could be thrown away in its last
  moment.
- **A counter serves one customer at a time.** Staffing was a property of the shop, so one barber
  cut five heads at once and a second till bought nothing.

### Added

- **Your own colonists can use the town's businesses.** Right-click a barber chair with a colonist
  selected. They pay nothing and leave no mark on the town's books — see
  [services](services.md#your-own-colonists).
- **The Stock tab shows the money.** What is on the shelves, what it would fetch at market, what
  this counter is asking for it, and the markup slider itself. The separate *Set prices* gizmo is
  gone.
- **A queue at a counter is visible** on the counter's inspect pane, and customers who would face
  too long a wait take their trade elsewhere rather than stand in it.

### Removed

- The *Set prices* gizmo. The markup slider lives in the Stock tab, beside the figures it changes.

### Save compatibility

- The wiki and tooling changes are documentation only. The gameplay fixes above are safe to drop
  into a save in progress, with one deliberate exception: a save written before reputation became
  a service record has almost certainly been pinned near 100% by the defect that is being fixed,
  and under the corrected price direction that would hand it a 10% raise it never earned. Such a
  save — recognisable because it has no patron table — starts again from no opinion, and a week of
  trading settles it honestly. A town that had not pinned keeps what it had.
- New saved state is additive only: a per-visit record of goods a customer could not afford, and
  the day's patron table. Older saves load without it.

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
