#!/usr/bin/env python3
"""
Generates the mod's placeholder building and terrain textures.

The art in Textures/ is deliberately programmer art: flat blocks in a frontier palette,
readable at RimWorld's zoom, consistent between buildings. It was originally drawn by hand
and there was no way to make more of it, which meant every new building either shipped with
a texture path pointing at nothing or reused an unrelated one. This regenerates the whole
set from one table, so adding a building is a row here rather than an art task.

The recipe matches the existing counters exactly: a transparent margin, a dark outline, a
lighter "far side" surface band, a body in the building's own colour, plank seams, and an
optional accent stripe (the saloon's brass rail). Rotations are derived, so a def can use
Graphic_Multi without four hand-drawn files.

Usage:
    python3 tools/make_textures.py            # draw art for anything that has none
    python3 tools/make_textures.py --check    # fail if something in the tables has no art
    python3 tools/make_textures.py --force    # restyle: redraw everything from the tables

It never overwrites existing art unless asked. The committed originals were drawn by hand and
phase their plank seams per facing, which a rotation-derived set does not reproduce exactly;
that difference is invisible in game and not worth rewriting shipped art over. What matters,
and what --check enforces, is that no building ships pointing at a texture that isn't there.

Requires Pillow (pip install Pillow). Only needed when adding or changing art — the
generated PNGs are committed, so neither the game nor CI needs this script to run.
"""

import argparse
import io
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("This script needs Pillow: pip install Pillow")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Textures")

CELL = 128          # pixels per world cell, matching the existing art
MARGIN = 7          # transparent border, so adjacent buildings don't visually fuse
OUTLINE = 2
SURFACE = 35        # depth of the lighter band along the far edge
PLANK = 16          # seam spacing


# name -> dict of the four colours the recipe needs, plus size.
#
# The palettes are explicit rather than derived from the body colour. Deriving them was
# tempting and wrong: the hand-drawn originals do not use a uniform shade factor — the
# barber's dark is a neutral charcoal against a red body, and its pale band is a mirror, not
# lit wood. Writing the real values down reproduces the committed art exactly and leaves each
# building free to be its own thing.
#
# "accent" is the optional stripe just inside the top edge (the saloon's brass rail); where a
# building has none, it is set to the surface colour so the stripe disappears into the band.
BUILDINGS = {
    "Commerce/ShopCounter": dict(
        cells=(2, 1), body=(122, 84, 52), edge=(74, 49, 30),
        surface=(163, 118, 74), accent=None),
    "Commerce/SaloonBar": dict(
        cells=(2, 1), body=(86, 55, 38), edge=(48, 30, 20),
        surface=(120, 76, 48), accent=(198, 158, 74)),
    "Commerce/BarberChair": dict(
        cells=(2, 1), body=(110, 32, 40), edge=(36, 30, 32),
        surface=(196, 196, 202), accent=None),
    "Commerce/HotelDesk": dict(
        cells=(2, 1), body=(133, 94, 51), edge=(76, 52, 28),
        surface=(176, 134, 82), accent=None),
    "Commerce/HotelBed": dict(
        cells=(1, 2), body=(96, 66, 40), edge=(54, 36, 22),
        surface=(214, 205, 186), accent=None),

    # Town roles (stage 4). A gunmetal body with a badge-gold accent stripe.
    "Roles/SheriffOffice": dict(
        cells=(2, 1), body=(70, 65, 60), edge=(35, 32, 28),
        surface=(110, 102, 92), accent=(180, 150, 60)),

    # Street furniture (stage 6). cells is the def's actual footprint; draw_cells, where
    # given, is what gets drawn instead — the false front is drawn taller than it stands on.
    "MainStreet/FalseFront": dict(
        cells=(3, 1), draw_cells=(3, 2), body=(196, 176, 140), edge=(90, 70, 45),
        surface=(224, 208, 176), accent=(120, 40, 36)),
    "MainStreet/HitchingPost": dict(
        cells=(2, 1), body=(101, 74, 51), edge=(60, 42, 28),
        surface=(140, 110, 80), accent=None),
    "MainStreet/Gallows": dict(
        cells=(3, 2), body=(64, 58, 54), edge=(28, 24, 22),
        surface=(96, 88, 82), accent=None),
    "MainStreet/FaroTable": dict(
        cells=(2, 1), body=(52, 92, 58), edge=(34, 26, 20),
        surface=(80, 124, 84), accent=(180, 150, 60)),
    # band="middle" instead of the default top band: a batwing door's light gap sits between
    # the two half-doors, not along the top edge.
    "MainStreet/BatwingDoor": dict(
        cells=(1, 1), band="middle", body=(140, 96, 56), edge=(70, 46, 28),
        surface=(190, 150, 96), accent=None),

    # Stagecoach line. Saddle-leather brown with a dusty road-tan surface band and an
    # oxide-red accent -- distinct from Commerce's oiled wood and Roles' gunmetal.
    "Stagecoach/CoachDepot": dict(
        cells=(3, 2), body=(96, 68, 42), edge=(52, 36, 22),
        surface=(168, 150, 120), accent=(140, 50, 36)),
}

# name -> dict of the two colours a terrain tile needs. Unlike BUILDINGS, a terrain texture is
# one fully-opaque CELL x CELL tile with no rotations — see draw_terrain.
TERRAIN = {
    "OWT_Boardwalk": dict(body=(150, 112, 74), edge=(96, 68, 44)),
}


def draw(cells, body, edge, surface, accent, band="top"):
    """One north-facing texture. East/south/west are derived by rotation.

    band picks where the lighter surface stripe sits: "top" (every existing building, and the
    default) puts it right below the top outline, the far-side-of-the-counter look. "middle"
    centres it in the inner rectangle instead — a batwing door's gap between its two halves,
    not a counter's far side.
    """
    w = cells[0] * CELL
    h = cells[1] * CELL
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()

    dark = edge
    light = surface

    x0, y0 = MARGIN, MARGIN
    x1, y1 = w - MARGIN - 1, h - MARGIN - 1
    inner_top, inner_bottom = y0 + OUTLINE, y1 - OUTLINE

    if band == "top":
        band_top = inner_top
    elif band == "middle":
        band_top = inner_top + (inner_bottom - inner_top + 1 - SURFACE) // 2
    else:
        raise ValueError("unknown band %r" % (band,))
    band_bottom = band_top + SURFACE - 1

    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            on_edge = (
                x < x0 + OUTLINE or x > x1 - OUTLINE
                or y < y0 + OUTLINE or y > y1 - OUTLINE
            )
            if on_edge:
                px[x, y] = dark + (255,)
            elif band_top <= y <= band_bottom:
                px[x, y] = light + (255,)
            else:
                px[x, y] = body + (255,)

    # Accent stripe sits just inside the top outline — the brass rail along a bar.
    if accent:
        for y in range(y0 + OUTLINE, min(y0 + OUTLINE + 4, y1)):
            for x in range(x0 + OUTLINE, x1 - OUTLINE + 1):
                px[x, y] = accent + (255,)

    # Plank seams down the body, so a long counter doesn't read as one flat slab. The first
    # seam sits PLANK-2 in from the inner edge, which lands the last one exactly on the right
    # outline — that alignment is what the hand-drawn originals have. Rows inside the band are
    # skipped so a seam never cuts across it — for band="top" that reproduces the previous
    # hardcoded start exactly; for band="middle" it seams above and below the gap instead.
    for x in range(x0 + OUTLINE + PLANK - 2, x1 - OUTLINE, PLANK):
        for y in range(inner_top, inner_bottom + 1):
            if band_top <= y <= band_bottom:
                continue
            px[x, y] = dark + (255,)

    return img


def draw_terrain(body, edge):
    """One CELL x CELL floor tile, fully opaque edge-to-edge. A building's MARGIN and OUTLINE
    exist so it doesn't visually fuse with its neighbour; a terrain tile is *supposed* to fuse
    with the identical tile beside it, so neither applies here — just a body fill and plank
    seams, spaced so CELL/PLANK divides evenly and a seam never lands as a partial sliver at
    the tile's own edge."""
    img = Image.new("RGBA", (CELL, CELL), body + (255,))
    px = img.load()
    for seam_x in range(0, CELL, PLANK):
        for y in range(CELL):
            px[seam_x, y] = edge + (255,)
            px[seam_x + 1, y] = edge + (255,)
    return img


def rotations(north):
    return {
        "north": north,
        "east": north.rotate(90, expand=True),
        "south": north.rotate(180, expand=True),
        "west": north.rotate(-90, expand=True),
    }


def encode(img):
    buf = io.BytesIO()
    img.save(buf, "PNG", optimize=True)
    return buf.getvalue()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true",
                    help="restyle: rewrite every texture from the table, replacing what is there")
    ap.add_argument("--check", action="store_true",
                    help="exit non-zero if a building or terrain tile in the table has no texture on disk")
    args = ap.parse_args()

    missing = []
    written = []

    for name, spec in sorted(BUILDINGS.items()):
        # draw_cells, where a row has one, is what actually gets drawn; cells stays the row's
        # footprint (matching the def's <size>) and is otherwise unused by draw().
        draw_spec = dict(spec)
        if "draw_cells" in draw_spec:
            draw_spec["cells"] = draw_spec.pop("draw_cells")

        facings = None
        for facing in ("north", "east", "south", "west"):
            path = os.path.join(TEX, "Things", "Building", "%s_%s.png" % (name, facing))
            if os.path.exists(path) and not args.force:
                continue
            if args.check:
                missing.append(os.path.relpath(path, ROOT))
                continue

            # Only pay for the draw once a facing actually needs writing.
            if facings is None:
                facings = rotations(draw(**draw_spec))
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "wb") as fh:
                fh.write(encode(facings[facing]))
            written.append(os.path.relpath(path, ROOT))

    for name, spec in sorted(TERRAIN.items()):
        # One tile, no rotations — a floor looks the same from every side.
        path = os.path.join(TEX, "Terrain", "Surfaces", "%s.png" % name)
        if os.path.exists(path) and not args.force:
            continue
        if args.check:
            missing.append(os.path.relpath(path, ROOT))
            continue

        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as fh:
            fh.write(encode(draw_terrain(**spec)))
        written.append(os.path.relpath(path, ROOT))

    if args.check:
        if missing:
            print("Buildings or terrain in the table with no texture on disk:")
            for p in missing:
                print("  " + p)
            return 1
        print("Every building and terrain tile in the table has art.")
        return 0

    for p in written:
        print("wrote " + p)
    if not written:
        print("Nothing to do (use --force to rewrite).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
