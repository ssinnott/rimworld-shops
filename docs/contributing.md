---
title: Contributing
summary: Building the assembly, the static checks that stand in for launching the game, CI, and how this wiki is kept honest.
---

## Repository layout

| Path | What's in it |
| --- | --- |
| `About/` | Mod metadata and preview image |
| `Defs/` | All XML defs, one folder per def type |
| `Languages/English/Keyed/` | Translation strings |
| `Textures/Things/Building/Commerce/` | Building art (generated — see below) |
| `docs/assets/textures/` | Copies of that art, so the [gallery](art.md) can show it |
| `Source/OldWestTown/` | C# source ([code map](architecture.md)) |
| `1.6/Assemblies/OldWestTown.dll` | **Committed** compiled assembly |
| `tools/` | Static validators and the texture generator |
| `docs/` | This wiki |

The compiled assembly is committed deliberately, so the repository can be dropped straight into
`RimWorld/Mods/` without a toolchain. **Rebuild and commit it whenever you change C#.**

## Building

Requires the .NET SDK — 8.0 is fine. The project targets `net472` and pulls the RimWorld
reference assemblies from NuGet, so you do **not** need the game or Mono installed to compile.

```sh
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release
```

Output goes straight to `1.6/Assemblies/OldWestTown.dll`.

## Static checks

RimWorld resolves XML into C# types and def references at load time and reports failures as red
errors in-game, which is a slow way to find a typo. Three scripts catch most of it without
launching anything.

### `tools/validate_defs.py`

```sh
python3 tools/validate_defs.py
```

Verifies that every C# type named in XML exists, that every def reference resolves (or is on an
explicit known-vanilla list), and that every `.Translate()` key has an English string.

Checking a *vanilla* type needs something that can read RimWorld's assemblies. `tools/refdump`
does that, using the same reference assemblies the build already restores from NuGet:

```sh
dotnet build tools/refdump/refdump.csproj -c Release   # once
python3 tools/validate_defs.py                         # now checks vanilla types for real
```

Without it the validator still runs, but downgrades "is this a real vanilla type?" to a note.
`refdump` is also useful on its own when you are unsure an API exists:

```sh
dotnet tools/refdump/bin/Release/net8.0/refdump.dll Thing.Ingested '=CompPowerTrader' '~Hediff'
```

### `tools/make_textures.py`

Building art is flat programmer art in a shared frontier palette, drawn from one table so that
adding a building is a row rather than an art task.

```sh
pip install Pillow
python3 tools/make_textures.py            # draw art for anything that has none
python3 tools/make_textures.py --check    # fail if a building in the table has no art
python3 tools/make_textures.py --force    # restyle: redraw everything
```

It never overwrites existing art unless you pass `--force`.

### `tools/validate_docs.py`

```sh
python3 tools/validate_docs.py
```

Checks that this wiki has not drifted from the code. See below.

## Continuous integration

`.github/workflows/ci.yml` runs on every push and pull request:

| Job | What it does |
| --- | --- |
| **Validate defs and translation keys** | Restores reference assemblies, builds `refdump`, runs `validate_defs.py` |
| **Check every building has art** | `make_textures.py --check` |
| **Check the wiki matches the code** | `validate_docs.py` |
| **Build assembly** | Full Release build; warns (does not fail) if the committed DLL differs from a fresh build |

`.github/workflows/pages.yml` builds and deploys this wiki to GitHub Pages on every push to
`main`. It also builds on pull requests — without deploying — so a broken site fails review
rather than production.

## Keeping the wiki honest

Documentation that lives beside the code still rots unless something checks it. `validate_docs.py`
is that check. It fails the build if:

1. **A def is undocumented.** Every `defName` in `Defs/` must appear somewhere under `docs/`.
   Add a business kind, a service, a building or a job, and the page describing it — plus the
   [reference tables](reference.md) — must mention it.
2. **A source file is unmapped.** Every `.cs` file under `Source/` must be named in
   [the code map](architecture.md).
3. **A translation key is undocumented.** Every key in the English keyed file must appear in the
   reference tables.
4. **An internal link is broken.** Every relative Markdown link between wiki pages must resolve
   to a file that exists, and every `#anchor` must match a heading on the target page.
5. **The changelog has no Unreleased section.** So there is always somewhere to write the next
   line.
6. **The art gallery is out of date.** Every texture in `Textures/` and `About/` must appear in
   [the gallery](art.md), and its copy under `docs/assets/textures/` must be **byte-identical**
   to the original. A regenerated texture that never reached the gallery would otherwise leave
   the wiki quietly showing the old art.

It does **not** check that the prose is accurate — nothing can. It checks that nothing was added
without anyone thinking about the docs, which is the failure mode that actually happens.

The art check is the one failure with a mechanical fix, so the tool can apply it:

```sh
python3 tools/validate_docs.py --sync-art
```

That refreshes the copies from the originals, then runs the checks as usual. **Commit the copies**
— they are what make the gallery work both on the published site and when someone reads
`docs/art.md` on GitHub.

### The workflow

When you change the mod:

1. Make the change.
2. Update the wiki page it affects, and the [reference tables](reference.md) if a number,
   def or key moved.
3. If you changed art: run `python3 tools/validate_docs.py --sync-art`, and add the new
   textures to the [gallery](art.md).
4. Add a line under **Unreleased** in the [changelog](changelog.md).
5. Rebuild the assembly if you touched C#.
6. Run all four checks.

```sh
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release
python3 tools/validate_defs.py
python3 tools/make_textures.py --check
python3 tools/validate_docs.py
```

## Working on the wiki locally

The site is plain Jekyll with no theme gem — the layout in `docs/_layouts/` and the stylesheet in
`docs/assets/css/` are the whole design, so a build needs nothing but Jekyll core.

```sh
gem install bundler jekyll
jekyll serve --source docs --destination _site_local --baseurl ''
```

Adding a page is a Markdown file in `docs/` with `title` and `summary` front matter, plus a line
in the `wiki_nav` list in `docs/_config.yml`. Links between pages are written as ordinary
relative Markdown links (`[services](services.md)`) — `jekyll-relative-links` rewrites them for
the built site, so the same link works both on GitHub and on the wiki.

## Style

The prose in this repository — code comments, design notes, wiki — explains **why**, not what.
A comment that restates the line below it is noise; one that records the trade-off someone
already thought through is the most valuable thing in the file. Match the surrounding density.

## Licence

Code is MIT. RimWorld is © Ludeon Studios; this mod is not affiliated with Ludeon.
