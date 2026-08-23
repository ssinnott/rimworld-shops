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
- `tools/validate_docs.py`, wired into CI: fails the build if a def, source file or translation
  key exists without being documented, if an internal wiki link is broken, or if the changelog
  has no *Unreleased* section. See [keeping the wiki honest](contributing.md#keeping-the-wiki-honest).
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

### Save compatibility

- No gameplay change. Safe to drop into a save in progress.

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
