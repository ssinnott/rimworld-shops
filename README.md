# Old West Town

A RimWorld 1.6 mod that lets your colony run actual businesses: shop counters with stock,
prices and a till, colonists working behind them, and travellers who show up with silver and
spend it.

Hospitality gets guests onto your map. This is about what they do once they're there.

**Status: goods and services both work.** A customer arrives, then either picks something off
your shelves or orders a drink, a meal or a haircut — queues, and pays either way. The breadth
(hotels, banks, stables, town roles) is designed but not built.

📖 **[Read the wiki](https://ssinnott.github.io/rimworld-shops/)** — every building, business,
service and system in the mod, plus the [code map](https://ssinnott.github.io/rimworld-shops/architecture.html),
the [design notes](https://ssinnott.github.io/rimworld-shops/DESIGN.html), the
[roadmap](https://ssinnott.github.io/rimworld-shops/roadmap.html) and the
[changelog](https://ssinnott.github.io/rimworld-shops/changelog.html). Its source is `docs/`.

This mod is standalone. It does not require Hospitality, and is written to sit alongside it.

## What works right now

- **Shop counters and service posts** (`shop counter`, `saloon bar`, `barber chair`) under a
  new *Commerce* build category, unlocked by the *Frontier commerce* research.
- **A sales floor is a room.** Anything sellable in the same room as the counter is on
  display. Outdoors, it falls back to a radius, so a market stall on the boardwalk trades too.
- **Per-shop stock control** via a Stock tab that reuses vanilla's storage filter widget.
- **Player-set prices.** A markup slider from 50% to 300%+ of market value. Undercutting a
  rival shop genuinely pulls customers away from it. Services are priced the same way.
- **Services, not just goods.** The saloon bar also serves a **drink** or a **meal**,
  consuming a matching item already on its own shelves; the **barber shop** sells a
  **haircut**, which needs no stock at all — just a colonist's time. Each produces a real
  effect: a drink or meal is actually eaten (mood, nutrition, the usual vanilla outcomes), and
  a haircut leaves a customer with a fresh mood thought and a visibly different hairstyle.
- **A Shopkeeping work type.** Colonists stand the counter and serve, goods or services alike;
  serving trains Social.
- **Real transactions.** Silver moves out of the customer's inventory and into the counter's
  till. Collect the takings with a gizmo. Deconstructing a counter drops its till rather than
  voiding it.
- **Customers who react to how you run the place.** An unattended counter makes shoppers wait,
  then walk out — leaving any goods by the counter — which costs you town reputation. An alert
  fires while customers are still queueing, so you can staff up before they give up.
- **A town economy that compounds.** Appeal is computed from how many *distinct* stocked or
  serviced businesses you run, what's on display or on offer, and your reputation. Appeal
  directly drives how often customers arrive — the town runs its own arrival clock, which
  shortens as appeal grows — and how much silver they bring, so investment pays forward.
- **A town ledger.** Every counter has a *Town ledger* gizmo showing appeal, reputation,
  today's sales and walkouts, and each shop's takings — the numbers the economy runs on,
  readable in one place. Counters also show appeal, reputation and their services on offer in
  their inspect pane.

## Installing

Copy this repository into `RimWorld/Mods/OldWestTown` and enable it in the mod list. The
compiled assembly is committed at `1.6/Assemblies/OldWestTown.dll`, so no build step is needed
to play.

## Playing it

1. Research **Frontier commerce**.
2. Build a **shop counter**. Rotate it so its interaction cell — the staff side — faces into
   the shop's back room. Customers stand on the opposite side.
3. Put goods in the same room. Shelves work; so does the floor.
4. Open the counter's **Stock** tab and choose what it sells.
5. Set a price with **Set prices**.
6. Give a colonist a **Shopkeep** priority in the Work tab.
7. Wait for the *Customers arriving* event, then watch the till.

If nobody is behind the counter, nothing sells. That's deliberate — it's what makes the
Shopkeeping assignment matter. You can turn it off with the *Allow self-service* mod setting,
at a price: every unattended sale quietly erodes the town's reputation (a haircut never allows
self-service, no matter the setting — an empty chair can't cut anyone's hair).

A **saloon bar** works the same way but also serves drinks and meals straight from its own
shelves — stock it with liquor or meals as well as (or instead of) general goods. A **barber
chair** needs no stock at all: build one, staff it, and a passing customer with money to spare
will sit for a haircut.

## Building from source

Requires the .NET SDK (8.0 is fine — the project targets `net472` and pulls the RimWorld
reference assemblies from NuGet, so you do **not** need the game or Mono installed to compile).

```sh
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release
```

Output goes straight to `1.6/Assemblies/OldWestTown.dll`.

### Checking your defs without launching the game

RimWorld resolves XML into C# types and def references at load time and reports failures as
red errors in-game, which is a slow way to find a typo. `tools/validate_defs.py` catches most
of them statically:

```sh
python3 tools/validate_defs.py
```

It verifies that every C# type named in XML exists, that every def reference resolves (or is
on an explicit known-vanilla list), and that every `.Translate()` key has an English string.

Checking a *vanilla* type needs something that can read RimWorld's assemblies. `tools/refdump`
does that, using the same reference assemblies the build already restores from NuGet, so it
needs neither the game nor a running RimWorld:

```sh
dotnet build tools/refdump/refdump.csproj -c Release   # once
python3 tools/validate_defs.py                         # now checks vanilla types for real
```

Without it the validator still runs, but downgrades "is this a real vanilla type?" to a note.
`refdump` is also useful on its own when you are unsure an API exists:

```sh
dotnet tools/refdump/bin/Release/net8.0/refdump.dll Thing.Ingested '=CompPowerTrader' '~Hediff'
```

### Making art for a new building

Textures are flat programmer art in a shared frontier palette. `tools/make_textures.py` draws
them from one table, so adding a building is a row rather than an art task — and CI fails if a
building in that table has no texture on disk:

```sh
pip install Pillow
python3 tools/make_textures.py     # draw art for anything that has none
```

It never overwrites existing art unless you pass `--force`.

CI (`.github/workflows/ci.yml`) runs all of the above plus a full build on every push.
`.github/workflows/pages.yml` builds the wiki on every pull request and publishes it on
every push to `main`.

### Keeping the wiki current

The wiki is checked, not just written: `tools/validate_docs.py` fails the build if a def, source
file or translation key is undocumented, if an internal wiki link is broken, or if the changelog
has no *Unreleased* section. A change to the mod updates the page it affects and the changelog in
the same commit — see
[Contributing](https://ssinnott.github.io/rimworld-shops/contributing.html).

```sh
python3 tools/validate_docs.py
```

## Caveats

- **This has not been run in RimWorld.** It compiles against the 1.6 reference assemblies and
  passes the static checks above, but the pawn AI — job drivers, the lord graph, the duty
  think tree — is the kind of code that only really proves itself in game. Expect to shake out
  bugs on first play. The full list is in the
  [wiki's known risks](https://ssinnott.github.io/rimworld-shops/architecture.html#known-risks).
- Textures are programmer-art placeholders.

## Licence

Code is MIT. RimWorld is © Ludeon Studios; this mod is not affiliated with Ludeon.
