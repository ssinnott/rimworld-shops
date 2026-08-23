---
title: Art gallery
summary: Every texture the mod ships, at every facing, with the palette and the recipe that draws it.
---

Every image below is a real file in this repository, shown at its committed size. The copies
under `docs/assets/textures/` are checked byte-for-byte against the originals in `Textures/`, so
what you see here is what the game loads — [CI fails](contributing.md#keeping-the-wiki-honest)
if the two ever diverge.

> **These are placeholders.** The art is deliberately flat programmer art: blocks in a shared
> frontier palette, readable at RimWorld's zoom, consistent between buildings. It was originally
> drawn by hand, which meant every new building either shipped pointing at nothing or reused an
> unrelated texture. It is now drawn from [one table](#the-recipe), so adding a building is a row
> rather than an art task.

## Buildings

Each building uses `Graphic_Multi`, so it needs four files — one per facing. Only the
**north** file is drawn; east, south and west are derived from it by rotation.

The UI icon in the build menu is the north file, named explicitly by `uiIconPath`.

### Shop counter

`OWT_ShopCounter` · 2 × 1 cells · [details](buildings.md#shop-counter)

<div class="art-row">
  <figure class="art-tile"><img src="assets/textures/Commerce/ShopCounter_north.png" alt="Shop counter, north facing"><figcaption>north · 256 × 128<br><span class="art-note">also the UI icon</span></figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/ShopCounter_east.png" alt="Shop counter, east facing"><figcaption>east · 128 × 256</figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/ShopCounter_south.png" alt="Shop counter, south facing"><figcaption>south · 256 × 128</figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/ShopCounter_west.png" alt="Shop counter, west facing"><figcaption>west · 128 × 256</figcaption></figure>
</div>

Plain oiled wood, no accent stripe. The lighter band along the far edge is the counter's
serving surface; the vertical seams are planks.

<div class="swatches">
  <span class="swatch" style="background:#A3764A"></span><code>#A3764A</code> surface
  <span class="swatch" style="background:#7A5434"></span><code>#7A5434</code> body
  <span class="swatch" style="background:#4A311E"></span><code>#4A311E</code> edge
</div>

### Saloon bar

`OWT_SaloonBar` · 2 × 1 cells · [details](buildings.md#saloon-bar)

<div class="art-row">
  <figure class="art-tile"><img src="assets/textures/Commerce/SaloonBar_north.png" alt="Saloon bar, north facing"><figcaption>north · 256 × 128<br><span class="art-note">also the UI icon</span></figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/SaloonBar_east.png" alt="Saloon bar, east facing"><figcaption>east · 128 × 256</figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/SaloonBar_south.png" alt="Saloon bar, south facing"><figcaption>south · 256 × 128</figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/SaloonBar_west.png" alt="Saloon bar, west facing"><figcaption>west · 128 × 256</figcaption></figure>
</div>

Darker wood than the shop counter, and the only building with an **accent stripe** — the brass
rail running just inside the top edge. That stripe is the one thing distinguishing a bar from a
counter at a glance, which is why it exists.

<div class="swatches">
  <span class="swatch" style="background:#C69E4A"></span><code>#C69E4A</code> accent (brass rail)
  <span class="swatch" style="background:#784C30"></span><code>#784C30</code> surface
  <span class="swatch" style="background:#563726"></span><code>#563726</code> body
  <span class="swatch" style="background:#301E14"></span><code>#301E14</code> edge
</div>

### Barber chair

`OWT_BarberChair` · 2 × 1 cells · [details](buildings.md#barber-chair)

<div class="art-row">
  <figure class="art-tile"><img src="assets/textures/Commerce/BarberChair_north.png" alt="Barber chair, north facing"><figcaption>north · 256 × 128<br><span class="art-note">also the UI icon</span></figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/BarberChair_east.png" alt="Barber chair, east facing"><figcaption>east · 128 × 256</figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/BarberChair_south.png" alt="Barber chair, south facing"><figcaption>south · 256 × 128</figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/BarberChair_west.png" alt="Barber chair, west facing"><figcaption>west · 128 × 256</figcaption></figure>
</div>

The odd one out, and deliberately so. Red leather rather than wood, a neutral charcoal outline
rather than a brown one, and a pale band that reads as a **mirror** rather than as lit timber.
This is why the palettes are written down per building instead of derived from the body colour
by a uniform shade factor: derived shading would have made the barber a red counter.

<div class="swatches">
  <span class="swatch" style="background:#C4C4CA"></span><code>#C4C4CA</code> surface (mirror)
  <span class="swatch" style="background:#6E2028"></span><code>#6E2028</code> body
  <span class="swatch" style="background:#241E20"></span><code>#241E20</code> edge
</div>

## Mod listing

`About/Preview.png` — what the mod looks like in RimWorld's mod list and on the Workshop.

<div class="art-row">
  <figure class="art-tile art-wide"><img src="assets/textures/Preview.png" alt="Old West Town mod preview image"><figcaption>640 × 360</figcaption></figure>
</div>

## The recipe

Every building texture is the same five-step drawing, parameterized by four colours. That is the
whole reason a new building is a table row: there is no step here that needs an artist's
judgement, only a palette.

| Step | What it draws | Constant |
| --- | --- | --- |
| 1 | A transparent border, so adjacent buildings don't visually fuse | `MARGIN` = 7 px |
| 2 | A dark outline in the **edge** colour | `OUTLINE` = 2 px |
| 3 | A lighter band along the far edge, in the **surface** colour | `SURFACE` = 35 px |
| 4 | The remaining area in the **body** colour, with an optional **accent** stripe just inside the top outline | 4 px stripe |
| 5 | Plank seams down the body, in the edge colour | `PLANK` = 16 px spacing |

One world cell is **128 px** (`CELL`), so a 2 × 1 building is 256 × 128.

The first seam sits `PLANK - 2` in from the inner edge, which lands the last one exactly on the
right outline. That alignment is what the hand-drawn originals have, and it is why a long counter
reads as planks rather than as one flat slab.

### Adding art for a new building

Add a row to `BUILDINGS` in `tools/make_textures.py` and run it — see
[adding a building](extending.md#add-a-building).

```python
"Commerce/GunsmithBench": dict(
    cells=(2, 1), body=(90, 88, 96), edge=(38, 36, 40),
    surface=(140, 138, 148), accent=(150, 120, 60)),
```

```sh
pip install Pillow
python3 tools/make_textures.py            # draw art for anything that has none
python3 tools/make_textures.py --check    # CI: fail if a building in the table has no art
python3 tools/make_textures.py --force    # restyle: redraw everything from the table
```

It never overwrites existing art unless you pass `--force`. Set `accent` to `None` where a
building has no stripe — the generator collapses it into the surface band.

> The committed originals were drawn by hand and phase their plank seams per facing, which a
> rotation-derived set does not reproduce exactly. That difference is invisible in game and not
> worth rewriting shipped art over. What matters, and what `--check` enforces, is that no
> building ships pointing at a texture that isn't there.

## Items

**The mod adds no items of its own** — no `ThingDef`s of category `Item`, and therefore no item
art. That is deliberate rather than unfinished: a business sells whatever your colony already
produces and whatever vanilla already defines, priced from
[`Thing.MarketValue`](economy.md#pricing). Adding bespoke trade goods would mean adding a supply
chain to make them, which is a different mod.

The one thing that changes hands and is *not* a shelf item is a
[service](services.md) — a drink, a meal, a haircut — and a service has nothing to draw, by
definition. Its visible proof is the effect it leaves behind: a hediff, a mood thought, or in the
[haircut's](services.md#haircut) case a new hairstyle picked from vanilla's own hair defs.

## Everything else the mod draws

For completeness, the visible surface that is *not* a texture file:

| What | Where it comes from |
| --- | --- |
| Build-menu category icon | None — vanilla's default for a `DesignationCategoryDef` |
| Research tab entry | Vanilla's default; the project is placed at `(1.0, 4.4)` |
| **Open for business** gizmo | `TexCommand.ForbidOff` |
| **Set prices** gizmo | `TexCommand.DesirePower` |
| **Collect takings** gizmo | `ThingDefOf.Silver.uiIcon` |
| **Town ledger** gizmo | `TexButton.Info` |
| Stock tab | Vanilla's storage-filter widget, unmodified |
| Fresh-haircut thought | No icon; vanilla renders mood thoughts from text |

Reusing vanilla's own gizmo icons is a choice, not a shortcut: a player already knows what
`ForbidOff` means, and a bespoke icon for "open for business" would be one more thing to learn
for no gain.
