# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

## What this is

**Old West Town** is a RimWorld 1.6 mod: colonies run actual businesses — shop counters with
stock, prices and a till, colonists working behind them, and travellers who arrive with silver
and spend it. C# against the 1.6 reference assemblies, plus XML defs.

The full documentation is the wiki in `docs/`, published to GitHub Pages. **Read it before
changing anything you don't already understand** — in particular
[`docs/architecture.md`](docs/architecture.md) (what every source file owns) and
[`docs/DESIGN.md`](docs/DESIGN.md) (why the mod is shaped the way it is).

## Keep the wiki current — this is not optional

`docs/` is the mod's documentation, and it is checked by CI. A change to the mod is not finished
until the wiki describes it.

**Every change to the mod updates, in the same commit:**

1. **The wiki page it affects.**

   | You changed | Update |
   | --- | --- |
   | A building | `docs/buildings.md` |
   | A `ShopKindDef` | `docs/businesses.md` |
   | A `ServiceDef` or `ServiceWorker` | `docs/services.md` |
   | Appeal, reputation, pricing, arrivals | `docs/economy.md` |
   | Customer AI, the incident, the lord | `docs/customers.md` |
   | The work type, work giver, staffing | `docs/shopkeeping.md` |
   | Any C# file added, removed or renamed | `docs/architecture.md` |
   | Player-facing behaviour | `docs/getting-started.md` |
   | How content is added | `docs/extending.md` |
   | Tooling, CI, build steps | `docs/contributing.md` |

2. **`docs/reference.md`**, if a defName, tunable constant, mod setting or translation key was
   added, removed or changed. It is the exhaustive table; keep it exhaustive.

3. **`docs/changelog.md`** — a line under `## Unreleased`, in Keep a Changelog style
   (**Added** / **Changed** / **Fixed** / **Removed** / **Save compatibility**). Note save
   compatibility explicitly whenever saved state changes.

4. **`docs/_config.yml`**, if you added a page — `wiki_nav` is the sidebar, and a page missing
   from it fails CI.

`python3 tools/validate_docs.py` enforces the mechanical half of this: it fails if a def, source
file or translation key is undocumented, if an internal wiki link or anchor is broken, if a page
is missing from the nav, or if the changelog has no *Unreleased* section. It cannot check that
the prose is *true* — that part is on you.

Do not add a "docs" commit afterwards. The docs change belongs in the commit that made it true.

## Commands

```sh
# Build the mod assembly (output goes straight to 1.6/Assemblies/OldWestTown.dll)
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release

# The four checks CI runs
python3 tools/validate_defs.py        # C# types named in XML, def references, .Translate() keys
python3 tools/make_textures.py --check # every building in the texture table has art on disk
python3 tools/validate_docs.py        # the wiki has not drifted from the code
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release

# One-time, so validate_defs.py can check vanilla types for real instead of noting them
dotnet build tools/refdump/refdump.csproj -c Release

# Ask whether a vanilla API exists, without the game installed
dotnet tools/refdump/bin/Release/net8.0/refdump.dll Thing.Ingested '=CompPowerTrader' '~Hediff'

# Draw art for a newly added building (needs: pip install Pillow)
python3 tools/make_textures.py

# Preview the wiki locally
jekyll serve --source docs --destination _site_local --baseurl ''
```

**The compiled assembly is committed** at `1.6/Assemblies/OldWestTown.dll`, so the repo can be
dropped straight into `RimWorld/Mods/` without a toolchain. **Rebuild and commit it in the same
commit as any C# change.** CI warns (does not fail) when the committed DLL differs from a fresh
build, because SDK differences can move bytes.

There is no test suite. The static checks above and a careful read are what stand in for it.

## Layout

| Path | What's in it |
| --- | --- |
| `About/` | Mod metadata, preview image |
| `Defs/` | XML defs, one folder per def type |
| `Languages/English/Keyed/` | Translation strings |
| `Textures/Things/Building/Commerce/` | Building art, generated from `tools/make_textures.py` |
| `Source/OldWestTown/` | C# — `Shops/`, `AI/`, `Lords/`, `Incidents/`, `Alerts/`, `UI/` |
| `1.6/Assemblies/` | The committed build output |
| `tools/` | Validators, the texture generator, `refdump` |
| `docs/` | The wiki (GitHub Pages source) |

## Design rules that hold across the codebase

These are load-bearing. Breaking one is a design decision, not a refactor — say so explicitly.

**Pawn loops never synchronise.** A customer and a shopkeeper never run paired jobs and never
reference each other. They read and write shared state on `CompBusiness`. This is why a
shopkeeper who is drafted, breaks, or dies cannot strand a customer in a broken job — the
customer's wait toil just runs out of patience and they leave, which is a *game mechanic*, not
an error state. Anything added later (a hotel clerk, a bank teller, a dealer) uses this shape.

**One place decides a price: `ShopPricing`.** One place moves silver: `ShopTransaction`. Goods
and services both go through them. A new business type extends those rather than routing around
them.

**Re-validate at the point of exchange.** The walk from shelf to counter gives the world time to
invalidate whatever the customer decided a minute ago. `ShopTransaction` re-checks the filter,
the forbidden flag, the shop's open and staffed state, and the purse before anything moves.

**`Shops/` must not depend on `AI/`.** The business layer recognizes "a pawn is patronizing
something" through the `IBusinessPatron` marker interface, never by naming a driver type or
`JobDef`.

**Prefer XML to code.** `ShopKindDef` and `ServiceDef` exist so that a new business or service is
a stanza, not a class. Reach for a new `ServiceWorker` subclass only when the *effect* is
genuinely new. See `docs/extending.md`.

**Never start a job from inside `ServiceWorker.ApplyEffect`.** It runs inside the service job's
own toil; starting a second job tears the running driver down mid-toil.

## Conventions

- Everything this mod adds is prefixed `OWT_` — defNames and translation keys alike.
- Every user-visible string goes through `.Translate()` with a key in
  `Languages/English/Keyed/OldWestTown.xml`. `validate_defs.py` fails otherwise.
- Comments explain **why**, not what. A comment restating the line below it is noise; one
  recording a trade-off someone already worked through is the most valuable thing in the file.
  Match the surrounding density — this codebase comments the reasoning behind non-obvious
  choices and leaves the obvious alone.
- Defensive guards on anything touching vanilla APIs this mod has not proven in a live game;
  say so in a comment where you do it.
- Prose in this repo (comments, design notes, wiki) is plain and specific. No marketing voice.

## Known context

**This mod has never been run in RimWorld.** It compiles against the 1.6 reference assemblies and
passes the static checks, but job drivers, lord graphs and duty think trees are exactly the code
static checking cannot validate. Treat "it compiles and validates" as necessary, not sufficient,
and prefer defensive code in the pawn AI. The current list is in
[`docs/architecture.md`](docs/architecture.md#known-risks) — add to it when you introduce a new
unproven dependency.
