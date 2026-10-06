"""A level's scene from its recipe (docs/design/v9/levels/scenes/<scene>.json): the graded painting before the dress.

The recipe's `source` is either

  {"kind": "game", "texture": "ui/loadingimage/-nowloading_base02_hr1.tex", "mirror": false,
   "crop": [x, y, w, h], "pad": [top, left], "padMode": "edge", "erase": [[x, y, w, h], ...],
   "grade": {...night_lab parameters...}, "hour": "night" | "far", "skyDrop": 0.3, "glow": [x, y]}

which is what the plugin runs at load (read the texture from the install, crop, grade, cache), or

  {"kind": "painting", "painter": "cactuar", "params": {...}}

our own painting, made in code by docs/design/v9/levels/painters/<painter>.py and shipped as a JPEG.

`overlays` are ours and drawn in board units on top: an engraved route ("route"). Crop and erase rectangles are in
source pixels; everything else is in board units (800 x 600).
"""
import importlib.util
import json

import numpy as np

from . import paths
from .paths import CACHE, PAINTERS, SCENES, cache_name
import rich_lib as RL


def load_recipe(name, merged=True):
    """The recipe; for our own paintings the painter's FEATURES (the shapes it paints) are merged under the recipe's
    own, so the painting and the layout read one set of coordinates."""
    r = json.loads((SCENES / f"{name}.json").read_text(encoding="utf-8"))
    if merged and r["source"]["kind"] == "painting":
        feats = dict(getattr(painter(r["source"]["painter"]), "FEATURES", {}))
        feats.update(r.get("features") or {})
        r["features"] = feats
    return r


def _erase(src, boxes, feather=6):
    """Fills each box (source px) from its surroundings: a heavy blur of the box's own border, so a map's exit arrows
    and stray marks leave plain parchment behind."""
    out = src.copy()
    H, W, _ = src.shape
    for (x, y, w, h) in boxes:
        x0, y0, x1, y1 = max(0, int(x)), max(0, int(y)), min(W, int(x + w)), min(H, int(y + h))
        pad = 12
        X0, Y0, X1, Y1 = max(0, x0 - pad), max(0, y0 - pad), min(W, x1 + pad), min(H, y1 + pad)
        patch = out[Y0:Y1, X0:X1].copy()
        m = np.zeros(patch.shape[:2], np.float32)
        m[y0 - Y0:y1 - Y0, x0 - X0:x1 - X0] = 1
        # diffuse the border inward (a few rounds of blur, keeping the outside fixed)
        fill = patch.copy()
        fill[m > 0] = patch[m == 0].mean(0)
        for _ in range(40):
            fill = RL.blur(fill, 3.0)
            fill[m == 0] = patch[m == 0]
        mm = RL.blur(m, feather / 2)[..., None]
        out[Y0:Y1, X0:X1] = patch * (1 - mm) + fill * mm
    return out


def graded(recipe, S=1):
    """The graded scene at S device px per unit, without the dress (float RGB)."""
    src = recipe["source"]
    W, H = int(800 * S), int(600 * S)
    if src["kind"] == "game":
        p = CACHE / cache_name(src["texture"])
        if not p.exists():
            raise SystemExit(f"missing {p}: run `py -3 mfl.py fetch` (texdump reads it from the game install)")
        img = RL.load_rgb(p)
        if src.get("erase"):
            img = _erase(img, src["erase"])
        if src.get("mirror"):
            img = img[:, ::-1]
        top, left = src.get("pad", (0, 0))
        if top or left:
            img = np.pad(img, ((top, 0), (left, 0), (0, 0)), mode=src.get("padMode", "edge"))
        x, y, w, h = src["crop"]
        px = RL.crop_to(img, (x + left, y + top, w, h), (W, H))
        g = dict(RL.VIOLET) if src.get("hour") == "far" else {}
        g.update(src.get("grade", {}))
        sky = None
        if src.get("skyDrop"):
            yy = np.mgrid[0:H, 0:W][0] / H
            sky = RL.blur(RL.smooth(0.02, 0.15, px[..., 2] - px[..., 0]) * RL.smooth(0.75, 0.30, yy), 3 * S)
            g["sky_drop"] = src["skyDrop"]
        out = RL.night_lab(px, sky=sky, S=S, **g)
        if src.get("glow"):
            gx, gy = src["glow"]
            out = RL.moon_glow(out, gx * S, gy * S, 330 * S, 900 * S, 0.11, 0.05, col="#C4C0EE")
    else:
        out = painter(src["painter"]).paint(S, **src.get("params", {}))
    for tn in recipe.get("tone", []):
        # ours, on the graded painting: a region made lighter or darker (a chart's unexplored desert receding)
        from .dress import mask_of
        import dress2

        class _C:
            pass
        c = _C()
        c.S, c.features, c.recipe = S, recipe.get("features", {}), recipe
        X, Y = dress2.grid(S)
        m = mask_of(tn["mask"], c, X, Y, None)
        out = out * (1 - (1 - tn["mul"]) * m)[..., None]
    for ov in recipe.get("overlays", []):
        if ov["kind"] == "route":
            import scene_airship_road as sar
            pts = RL.catmull([tuple(p) for p in ov["points"]], 8) if ov.get("smooth", True) else ov["points"]
            out = sar.dashed(out, S, pts, ov.get("color", "#D9BE82"), width=ov.get("width", 2.0),
                             dash=ov.get("dash", 5.0), gap=ov.get("gap", 4.5), alpha=ov.get("alpha", 0.6))
    if src["kind"] == "game":
        out = RL.vignette(out, recipe.get("vignette", 0.28))
        out = RL.grain(out, 0.008, seed=recipe.get("seed", 11))
    return np.clip(out, 0, 1)


_MASKS = {}


def derived_mask(recipe, name, S):
    """A mask derived from the source (`masks` in the recipe), at S, unblurred (0 or 1).

    rim-fill: the source crop's OKLab L (blurred 1.5 px at 1x) below `threshold` is a wall (a map's dark rim), grown by
    `grow` px; everything a flood from the `outside` seeds (board units) cannot reach is inside (a map's land)."""
    from collections import deque
    key = (recipe["name"], name, S)
    if key in _MASKS:
        return _MASKS[key]
    spec = recipe["masks"][name]
    src = recipe["source"]
    img = RL.load_rgb(CACHE / cache_name(src["texture"]))
    if src.get("erase"):
        img = _erase(img, src["erase"])
    if src.get("mirror"):
        img = img[:, ::-1]
    x, y, w, h = src["crop"]
    px = RL.crop_to(img, (x, y, w, h), (800, 600))
    from r2lib import srgb_to_oklab
    L = RL.blur(srgb_to_oklab(px)[..., 0], 1.5)
    wall = L < spec.get("threshold", 0.75)
    for _ in range(spec.get("grow", 5)):
        wall = wall | np.roll(wall, 1, 0) | np.roll(wall, -1, 0) | np.roll(wall, 1, 1) | np.roll(wall, -1, 1)
    out = np.zeros_like(wall)
    q = deque()
    for (sx, sy) in spec.get("outside", [[5, 5], [795, 5], [5, 595], [795, 595]]):
        if not wall[int(sy), int(sx)]:
            out[int(sy), int(sx)] = True
            q.append((int(sy), int(sx)))
    while q:
        yy, xx = q.popleft()
        for ny, nx in ((yy + 1, xx), (yy - 1, xx), (yy, xx + 1), (yy, xx - 1)):
            if 0 <= ny < 600 and 0 <= nx < 800 and not out[ny, nx] and not wall[ny, nx]:
                out[ny, nx] = True
                q.append((ny, nx))
    m = (~out).astype(np.float32)
    from PIL import Image
    m = np.asarray(Image.fromarray(m, "F").resize((int(800 * S), int(600 * S)), Image.BILINEAR), np.float32)
    _MASKS[key] = m
    return m


def painter(name):
    spec = importlib.util.spec_from_file_location(f"painter_{name}", PAINTERS / f"{name}.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def ceiling(px, exempt=()):
    """The 99th-percentile luma of the board's opening (method section 5: about 0.46 or less). `exempt`: discs
    (x, y, r) left out, for a painted moon, which the method allows to be the light itself where no peg goes."""
    S = px.shape[1] / 800
    H, W = px.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    keep = (X > 75) & (X < 725) & (Y > 41) & (Y < 594)
    for (cx, cy, r) in exempt:
        keep &= (X - cx) ** 2 + (Y - cy) ** 2 > r * r
    return float(np.percentile((px @ RL.LUM)[keep], 99))


def sources(recipe):
    """The game files this scene reads (for sources.json)."""
    src = recipe["source"]
    return [src["texture"]] if src["kind"] == "game" else []


def dump_recipe(recipe, path):
    """Writes a recipe with short numeric lists on one line, so points stay readable in review."""
    import re
    text = json.dumps(recipe, indent=1, ensure_ascii=False)
    text = re.sub(r"\[\s+([^\[\]{}]*?)\s+\]", lambda m: "[" + re.sub(r",\s+", ", ", m.group(1)) + "]", text)
    text = re.sub(r"\[\s+((?:\[[^\[\]]*\],?\s*)+)\]", lambda m: "[" + re.sub(r"\],\s+\[", "], [", m.group(1).strip()) + "]", text)
    path.write_text(text + "\n", encoding="utf-8")


def edit_recipe(name, fn):
    """Loads a recipe, applies fn(recipe) and writes it back compactly."""
    r = load_recipe(name, merged=False)
    fn(r)
    dump_recipe(r, SCENES / f"{name}.json")
    return r
