"""Composite, rich pass 2: a pilot level as the player sees it: the re-dressed scene (dress2.py), the veil, the
pegs and bricks exactly as the approved level files place them, and the chrome built from FFXIV's UI art.

  py -3 composite2.py [<level-id> ...]

Writes composites/<level-id>.png (800 x 600) and @2x (1600 x 1200). Pegs, layouts and engine colours are the rich
pass's: the level files are read from ../rich/levels, unchanged.
"""
import math
import sys

import numpy as np

from r2lib import OUT_COMP, OUT_SCENES, RICH, Img, load_rgb, save_rgb, write_sources
from board import draw_pieces, engine_colours, load_level, pieces, veil
import chrome2
import composite as rc                       # the rich pass's lantern spill

LEVELS = RICH / "levels"
PILOTS = {  # id: (scene stem, bucket, seed, stage label, carrier); the base pilots are stage 3, The Night Skyway (r2state)
    "base-p1": ("base-p1-airship-road", "cart", 1, "3-3", "Wings"),
    "base-p2": ("base-p2-holy-see", "cart", 1, "3-2", "Wings"),
    "base-p3": ("base-p3-moogle", "cart", 1, "3-1", "Wings"),
    "exp-p1": ("exp-p1-sharlayan", "boat", 1, "7-4", "Bloom"),
    "exp-p2": ("exp-p2-lantern-ferry", "boat", 1, "9-2", "Fireball"),
    "exp-p3": ("exp-p3-mare-lamentorum", "boat", 1, "12-5", "Bolt"),
}


def scene_for(level_id, S):
    stem = PILOTS[level_id][0]
    p = OUT_SCENES / f"{stem}{'@2x' if S == 2 else ''}.png"
    if not p.exists():
        p = RICH / "scenes" / f"{stem}{'@2x' if S == 2 else ''}.png"
    return load_rgb(p)


def pick_bucket_x(level, gone):
    live = [(d["x"], d["y"], d.get("r", 10)) for i, (k, d) in enumerate(pieces(level))
            if k == "peg" and i not in gone and "move" not in d]

    def clearance(bx):
        lx = bx + 50
        return min((math.hypot(x - lx, max(abs(y - 541) - 12, 0)) - r for (x, y, r) in live), default=99)
    cands = [chrome2.BUCKET_X_DEFAULT] + [float(x) for x in range(200, 621, 10)]
    good = [c for c in cands if clearance(c) >= 12]
    return min(good, key=lambda c: abs(c - chrome2.BUCKET_X_DEFAULT)) if good else max(cands, key=clearance)


def render(level_id, bucket=None, seed=None, S=2, hud_kw=None, lit=(), gone=(), aim=None, extra=None, after=None,
           bucket_x=None, scene=None, over_scene=None, small=False):
    """small: the board as the 640 x 480 window shows it (0.8 display px per unit): labels that cannot meet the text
    floor there are left out, and every other text is checked against the floor."""
    import r2lib
    chrome2.SMALL["on"] = small
    prev = r2lib.DISPLAY["scale"]
    r2lib.DISPLAY["scale"] = 0.8 if small else None
    try:
        return _render(level_id, bucket, seed, S, hud_kw, lit, gone, aim, extra, after, bucket_x, scene, over_scene)
    finally:
        chrome2.SMALL["on"] = False
        r2lib.DISPLAY["scale"] = prev


def _render(level_id, bucket=None, seed=None, S=2, hud_kw=None, lit=(), gone=(), aim=None, extra=None, after=None,
            bucket_x=None, scene=None, over_scene=None):
    stem, bk, sd, stage, carrier = PILOTS[level_id]
    from r2lib import record
    src = {"base-p1": "ui/loadingimage/-nowloading_base05.tex", "base-p2": "ui/loadingimage/-nowloading_base03.tex",
           "exp-p1": "ui/loadingimage/-nowloading_base21.tex", "exp-p3": "ui/loadingimage/-nowloading_base25.tex"}.get(level_id)
    if src:
        record(src, f"level scene {level_id} (the approved recipe, graded at load, re-dressed by dress2)")
    bucket = bucket or bk
    seed = sd if seed is None else seed
    path = LEVELS / f"{level_id}.json"
    level = load_level(path)
    colours = engine_colours(path, 5, seed)
    sc = scene if scene is not None else scene_for(level_id, S)
    k = level.get("scene", {}).get("veil", 0.30)
    sc = veil(sc, level, S, k=k)
    img = Img(800 * S, 600 * S, S, px=sc.copy())
    if over_scene:
        over_scene(img)
    draw_pieces(img, level, colours, lit=lit, gone=gone)
    if extra:
        extra(img)
    if bucket_x is None and bucket in ("cart", "boat"):
        bucket_x = pick_bucket_x(level, gone)
    kw = dict(stage=stage, carrier=carrier)
    if carrier == "Wings":                       # Brass Wings not running: no gems lit, no glow
        kw.update(turns=0, active=False)
    else:
        kw.update(turns=1, active=True)
    kw.update(hud_kw or {})
    aimed = chrome2.playfield_chrome(img, level, pal=level_id, bucket=bucket, bucket_x=bucket_x or chrome2.BUCKET_X_DEFAULT,
                                     aim=aim, hud_kw=kw)
    if bucket in ("cart", "boat"):
        rc.lantern_spill(img, level, gone, bucket_x)
    if after:
        after(img, level, colours, aimed)
    return img, colours


if __name__ == "__main__":
    ids = sys.argv[1:] or list(PILOTS)
    for lid in ids:
        img, colours = render(lid)
        save_rgb(img.px, OUT_COMP / f"{lid}@2x.png")
        save_rgb(img.px, OUT_COMP / f"{lid}.png", size=(800, 600))
        print(lid, {c: colours.count(c) for c in set(colours)})
    write_sources()
