"""Composite: a pilot level as the player sees it (scene, veil, pegs and bricks, frame, launcher, HUD, bucket).

  py -3 composite.py <level-id> <scene-stem> [boat|cart] [seed]

Writes composites/<level-id>.png (800 x 600) and @2x (1600 x 1200); the 2x is painted natively and the 1x is reduced
from it. The colours are the engine's own pick for level 5 (greens on) and the seed.
"""
import sys

import numpy as np

from board import draw_pieces, engine_colours, load_level, pieces, veil
from rich_lib import LEVELS, OUT_COMP, OUT_SCENES, Img, load_rgb, save_rgb
import frame_rich as fr


def lantern_spill(img, level, gone, bucket_x):
    """Round 4 (realism round 3): the bucket's paper lantern is a light, so a peg within about 30 units of it takes a
    faint warm light on the side of its moon that faces the lantern, falling off with distance (and the peg is drawn
    under the lantern, never over it)."""
    import math as _m
    from rich_lib import hexc, screen
    lx, ly = bucket_x + 50.0, 541.0
    for i, (k, d) in enumerate(pieces(level)):
        if k != "peg" or i in gone or "move" in d:
            continue
        x, y, r = d["x"], d["y"], d.get("r", 10)
        dist = _m.hypot(x - lx, y - ly)
        if dist > r + 34:
            continue
        w = img.win(x, y, r + 1)
        if w is None:
            continue
        sl, X, Y = w
        u, v = (X - x) / r, (Y - y) / r
        d2 = u * u + v * v
        nz = np.sqrt(np.clip(1 - d2, 0, 1))
        tx, ty, tz = (lx - x) / dist, (ly - y) / dist, 0.25
        tn = _m.sqrt(tx * tx + ty * ty + tz * tz)
        facing = np.clip((u * tx + v * ty + nz * tz) / tn, 0, 1)
        fall = 1.0 / (1.0 + ((dist - r) / 14.0) ** 2)
        disc = np.clip((1 - np.sqrt(d2)) * r * img.S + 0.5, 0, 1)
        img.px[sl] = screen(img.px[sl], hexc("#FFB060") * (facing * fall * disc * 0.55)[..., None])


def render(level_id, scene_stem, bucket="boat", seed=1, S=2, hud_kw=None, lit=(), gone=(), aim=None, extra=None,
           after=None, bucket_x=None):
    path = LEVELS / f"{level_id}.json"
    level = load_level(path)
    colours = engine_colours(path, 5, seed)
    scene = load_rgb(OUT_SCENES / f"{scene_stem}{'@2x' if S == 2 else ''}.png")
    k = level.get("scene", {}).get("veil", 0.30)
    scene = veil(scene, level, S, k=k)
    img = Img(800 * S, 600 * S, S, px=scene.copy())
    draw_pieces(img, level, colours, lit=lit, gone=gone)
    if extra:
        extra(img)
    if bucket_x is None and bucket in ("cart", "boat"):
        # round 4 (realism round 3): the bucket sweeps, so for the still the sweep's moment is chosen where its
        # lantern (at bucket x + 50, y 529-553, its glow about 14 more) stands clear of every peg still on the board
        import math as _m
        live = [(d["x"], d["y"], d.get("r", 10)) for i, (k, d) in enumerate(pieces(level))
                if k == "peg" and i not in gone and "move" not in d]
        def clearance(bx):
            lx = bx + 50
            return min((_m.hypot(x - lx, max(abs(y - 541) - 12, 0)) - r for (x, y, r) in live), default=99)
        cands = [fr.BUCKET_X_DEFAULT] + [float(x) for x in range(200, 621, 10)]
        good = [c for c in cands if clearance(c) >= 12]
        bucket_x = min(good, key=lambda c: abs(c - fr.BUCKET_X_DEFAULT)) if good else max(cands, key=clearance)
    aimed = fr.playfield_chrome(img, level, bucket=bucket, bucket_x=bucket_x or fr.BUCKET_X_DEFAULT, aim=aim,
                                hud_kw=hud_kw or {})
    if bucket in ("cart", "boat"):
        lantern_spill(img, level, gone, bucket_x)
    if after:
        after(img, level, colours, aimed)
    return img, colours


if __name__ == "__main__":
    lid, stem = sys.argv[1], sys.argv[2]
    bucket = sys.argv[3] if len(sys.argv) > 3 else "boat"
    seed = int(sys.argv[4]) if len(sys.argv) > 4 else 1
    img, colours = render(lid, stem, bucket, seed)
    save_rgb(img.px, OUT_COMP / f"{lid}@2x.png")
    save_rgb(img.px, OUT_COMP / f"{lid}.png", size=(800, 600))
    print(lid, {c: colours.count(c) for c in set(colours)})
