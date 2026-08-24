---
title: Art gallery
summary: Every picture the mod ships, at every facing, and the colours behind each building.
---

Every image below is the real file the game loads, shown at its full size.

> **These are placeholders.** The art is deliberately flat and simple: blocks in a shared
> frontier palette, readable at RimWorld's zoom, consistent from one building to the next. It is
> drawn to a recipe rather than by hand, which is why every building has art at every facing and
> none of them borrow a picture from something else.

## Buildings

Each building has four pictures, one per direction it can face. The one shown in the build menu
is the north view.

### Shop counter

2 × 1 tiles · [details](buildings.md#shop-counter)

<div class="art-row">
  <figure class="art-tile"><img src="assets/textures/Commerce/ShopCounter_north.png" alt="Shop counter, north facing"><figcaption>north · 256 × 128<br><span class="art-note">also the build-menu icon</span></figcaption></figure>
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

2 × 1 tiles · [details](buildings.md#saloon-bar)

<div class="art-row">
  <figure class="art-tile"><img src="assets/textures/Commerce/SaloonBar_north.png" alt="Saloon bar, north facing"><figcaption>north · 256 × 128<br><span class="art-note">also the build-menu icon</span></figcaption></figure>
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

2 × 1 tiles · [details](buildings.md#barber-chair)

<div class="art-row">
  <figure class="art-tile"><img src="assets/textures/Commerce/BarberChair_north.png" alt="Barber chair, north facing"><figcaption>north · 256 × 128<br><span class="art-note">also the build-menu icon</span></figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/BarberChair_east.png" alt="Barber chair, east facing"><figcaption>east · 128 × 256</figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/BarberChair_south.png" alt="Barber chair, south facing"><figcaption>south · 256 × 128</figcaption></figure>
  <figure class="art-tile"><img src="assets/textures/Commerce/BarberChair_west.png" alt="Barber chair, west facing"><figcaption>west · 128 × 256</figcaption></figure>
</div>

The odd one out, and deliberately so. Red leather rather than wood, a neutral charcoal outline
rather than a brown one, and a pale band that reads as a **mirror** rather than as lit timber.
This is why each building's colours are written down separately instead of being shaded
automatically from one base colour: automatic shading would have made the barber a red counter.

<div class="swatches">
  <span class="swatch" style="background:#C4C4CA"></span><code>#C4C4CA</code> surface (mirror)
  <span class="swatch" style="background:#6E2028"></span><code>#6E2028</code> body
  <span class="swatch" style="background:#241E20"></span><code>#241E20</code> edge
</div>

## Mod listing

What the mod looks like in RimWorld's mod list and on the Workshop.

<div class="art-row">
  <figure class="art-tile art-wide"><img src="assets/textures/Preview.png" alt="Old West Town mod preview image"><figcaption>640 × 360</figcaption></figure>
</div>

## How a building is drawn

Every building picture is the same five-step drawing in four colours, which is why a new building
needs a palette rather than an artist:

| Step | What it draws |
| --- | --- |
| 1 | A transparent border, so adjacent buildings don't visually fuse |
| 2 | A dark outline in the **edge** colour |
| 3 | A lighter band along the far edge, in the **surface** colour |
| 4 | The rest in the **body** colour, with an optional **accent** stripe just inside the top |
| 5 | Plank seams down the body, in the edge colour |

The seams are spaced so the last one lands exactly on the outline. That alignment is what makes a
long counter read as planks rather than as one flat slab.

Modders: the generator and its table of palettes are covered in
[contributing](contributing.md#generating-building-art).

## Items

**The mod adds no items of its own**, and therefore no item art. That is deliberate rather than
unfinished: a business sells whatever your colony already produces and whatever the base game
already defines, priced from [market value](economy.md#pricing). Adding bespoke trade goods would
mean adding a supply chain to make them, which is a different mod.

The one thing that changes hands and is *not* a shelf item is a [service](services.md) — a drink,
a meal, a haircut — and a service has nothing to draw, by definition. Its visible proof is the
effect it leaves behind: the buzz from a drink, a mood boost, or in the
[haircut's](services.md#haircut) case a new hairstyle.

## Everything else you see

For completeness, the visible surface that isn't a picture file:

| What | Where it comes from |
| --- | --- |
| Build-menu category icon | The game's default for a build category |
| Research tab entry | The game's default |
| **Open for business** button | The game's own "unforbid" icon |
| **Collect takings** button | The silver icon |
| **Town ledger** button | The game's own info icon |
| Stock tab | The stockpile filter list, unmodified |
| Fresh-haircut mood | No icon; the game draws mood thoughts from text |

Reusing the game's own button icons is a choice, not a shortcut: you already know what the
unforbid icon means, and a bespoke icon for "open for business" would be one more thing to learn
for no gain.
