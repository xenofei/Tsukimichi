"""The fuller-board dress from a recipe (level-method.md section 8): the jewel palette, the light, the framing and the
small lights, drawn with the approved primitives of docs/design/v9/rich2/src/dress2.py. Each pilot's dress was a
branch of code; here it is data, the recipe's `dress` object, so the plugin can draw it at load (spec-rich2.md 6):

  "dress": {
    "palette": {"name": ..., "sky": "#..", "deep": "#..", "jewel1": "#..", "jewel2": "#..", "warm": "#..", "rim": "#.."},
    "jewel":   {"bands": [[y, "#hex"], ...], "valueHues": [[L, "#hex"], ...], "chroma": 0.9, "floor": 0.03,
                "keep": 0.30, "keepHi": 0.75, "mix": 0.5,
                "regions": [{"hex": "#..", "chroma": 0.1, "mask": [["y", 400, 470], ["luma", 0.3, 0.42, 3], ...]}],
                "quiet": [40, 18], "quietBlur": 40,      # quieten colour round the layout (F9): runtime `near` form
                "regionQuiet": 0.6},                     # how much of the quiet the regions take (the term's scale)
    "shafts":  [{"origin": [x, y], "angles": [...], "widths": [...], "k": 0.07, "col": "#..", "reach": 900, "near": 150}],
    "glows":   [{"x":.., "y":.., "r":.., "col": "#..", "k": 0.05}],
    "framing": [{"kind": "frond", ...}, {"kind": "pines", ...}, ...],
    "silhouette": {"body": "#..", "inner": "#..", "rim": "#..", "rimK": 0.45, "snow": null},
    "lights":  [{"points": [[x, y, s], ...]} or {"auto": {"n": 9, "box": [x0, y0, x1, y1], "seed": 5}},
                 "col": "#FFC86E", "r": 1.4, "k": 0.8, "halo": 5, "hk": 0.24}],
    "extras":  [{"kind": "compass_rose", "x":.., "y":.., "R":..}, {"kind": "chart_border"}]
  }

A mask is a product of ramps: ["y", a, b] rises from 0 at y=a to 1 at y=b (a > b falls), the same for "x"; ["luma",
lo, hi, blur] ramps on the scene's OKLab L; ["dist", a, b] on the distance from the pieces; ["disc", cx, cy, r, f].
"""
import math

import numpy as np
from PIL import Image

from . import paths  # noqa: F401
import dress2 as D
import framecheck as fc
from r2lib import blur, hexc, screen, smooth, srgb_to_oklab

WALL_L, WALL_R, TOP, FOOT = D.WALL_L, D.WALL_R, D.TOP, D.FOOT
# the jewel's quiet, in the runtime's own form (main's scene-recipe `near` mask term, MoonfallSceneBuilder.Palette):
# the clearance (capped at MoonfallClearance.Far) blurred by `quietBlur` units (40 at most, the format's limit), then
# the smoothstep `quiet: [a, b]`; a region keeps 1 - 0.6 of it (`near` with invert and scale 0.6) and the jewel
# 1 - 0.5 (the palette's `where`). Blurring the distance first is what keeps it from printing a coin round a lone peg
# (round-2 supervision G1); drawing it exactly as the runtime will is round 3's UX G3. The blur is pinned at 40: the
# quiet is the dress's only per-peg term, and with less blur it prints a coin round every peg (critic round 4, M1), so
# a recipe asking for less is refused rather than drawn.
QUIET_BLUR, QUIET_BLUR_MIN, QUIET_BLUR_MAX, CLEARANCE_FAR = 40.0, 40.0, 40.0, 96.0
QUIET_REGION, QUIET_KEEP = 0.6, 0.5


class Ctx:
    """The board being dressed (dress2.Ctx for any level): S, the distance from every piece's edge, the framing."""

    def __init__(self, level, S, features=None):
        self.id, self.S, self.level = level.get("id", "?"), S, level
        self.features = features or {}
        self.d1 = fc.piece_distance(level, 1.0)
        h, w = int(600 * S), int(800 * S)
        self.cover = np.zeros((h, w), np.float32)
        self.rim = np.zeros((h, w), np.float32)
        self.lights = []
        self.dropped = 0

    clear = D.Ctx.clear
    ok = D.Ctx.ok

    def dist_at_S(self):
        return np.asarray(Image.fromarray(self.d1).resize((int(800 * self.S), int(600 * self.S))))


def poly_mask(features, name, S, feather=8.0):
    """A closed feature polygon (board units) as a soft mask at S."""
    feat = features[name]
    pts = feat["points"] if isinstance(feat, dict) else feat
    ss = 2
    im = Image.new("L", (int(800 * S * ss), int(600 * S * ss)), 0)
    from PIL import ImageDraw
    ImageDraw.Draw(im).polygon([(x * S * ss, y * S * ss) for (x, y) in pts], fill=255)
    m = np.asarray(im.resize((int(800 * S), int(600 * S)), Image.BOX), np.float32) / 255
    return blur(m, feather * S / 2)


def mask_of(spec, ctx, X, Y, Lsc):
    m = np.ones_like(X)
    for term in spec:
        kind = term[0]
        if kind == "y":
            m = m * smooth(term[1], term[2], Y)
        elif kind == "x":
            m = m * smooth(term[1], term[2], X)
        elif kind == "luma":
            b = term[3] if len(term) > 3 else 3
            m = m * smooth(term[1], term[2], blur(Lsc, b * ctx.S))
        elif kind == "dist":
            m = m * smooth(term[1], term[2], ctx.dist_at_S())
        elif kind == "disc":
            _, cx, cy, r, f = term
            m = m * smooth(r + f, r - f, np.sqrt((X - cx) ** 2 + (Y - cy) ** 2))
        elif kind == "poly":
            m = m * poly_mask(ctx.features, term[1], ctx.S, term[2] if len(term) > 2 else 8.0)
        elif kind == "not-poly":
            m = m * (1 - poly_mask(ctx.features, term[1], ctx.S, term[2] if len(term) > 2 else 8.0))
        elif kind in ("mask", "not-mask"):
            from .scene import derived_mask
            dm = blur(derived_mask(ctx.recipe, term[1], ctx.S), (term[2] if len(term) > 2 else 6.0) * ctx.S / 2)
            m = m * (dm if kind == "mask" else 1 - dm)
        elif kind == "near":
            # within a band round a feature polyline: full inside d0 units, fading to nothing at d1
            feat = ctx.features[term[1]]
            pts = np.asarray(feat["points"] if isinstance(feat, dict) else feat, np.float32)
            dist = np.full_like(X, 1e9)
            for k in range(len(pts) - 1):
                (ax, ay), (bx, by) = pts[k], pts[k + 1]
                t = np.clip(((X - ax) * (bx - ax) + (Y - ay) * (by - ay)) / max((bx - ax) ** 2 + (by - ay) ** 2, 1e-6), 0, 1)
                dist = np.minimum(dist, np.sqrt((X - ax - (bx - ax) * t) ** 2 + (Y - ay - (by - ay) * t) ** 2))
            m = m * smooth(term[3], term[2], dist)
        elif kind == "k":
            m = m * term[1]
        else:
            raise ValueError(f"unknown mask term {term}")
    return m


# ------------------------------------------------------------------------------------------------ framing shapes
def festoon(ctx, cover_px, px, spans, top=TOP - 4, col="#2A1E58", lit=0.5):
    """Cloth hung in swags from above the board (an awning, a furled sail): spans [[xa, xb, sag], ...]. It is lit
    cloth, not a silhouette; it is drawn only if every part keeps 6.5 units from the pieces (F3a)."""
    S = ctx.S
    X, Y = D.grid(S)
    cloth = np.zeros_like(X)
    for (xa, xb, sag) in spans:
        fr_ = np.clip((X - xa) / (xb - xa), 0, 1)
        inside = (X > xa) & (X < xb)
        bottom = top + sag * np.sin(np.pi * fr_) + 3 * np.sin(X / 5.0) * np.sin(np.pi * fr_)
        cloth = np.maximum(cloth, D.cov(Y - bottom, S) * inside)
    on = cloth > 0.5
    dd = ctx.dist_at_S()
    if not (dd[on & (X > WALL_L) & (Y > TOP)] >= 6.5).all():
        ctx.dropped += 1
        return px
    folds = 0.5 + 0.5 * np.sin((X * 0.9 + Y * 1.6) / 4.0)
    body = (hexc(col) * (0.7 + lit * folds)[..., None]) * (0.8 + 0.4 * smooth(80, 40, Y))[..., None]
    ctx.cover = np.maximum(ctx.cover, cloth)
    return px * (1 - cloth[..., None]) + body * cloth[..., None]


def rope(ctx, m, a, b, sag, width=1.6):
    pts = [(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t + sag * 4 * t * (1 - t)) for t in np.linspace(0, 1, 160)]
    for (rx, ry) in pts:
        if ctx.ok(rx, ry, width + 0.5):
            D.stamp(ctx, m, lambda X_, Y_, rx=rx, ry=ry: np.sqrt((X_ - rx) ** 2 + (Y_ - ry) ** 2) - width, rx, ry, width + 2)


def lantern_post(ctx, m, x, base=600.0, height=60.0, w=2.6):
    """A post rising from below the board with a lantern on it; it stops where it would come near a piece. Returns
    the lantern's centre (for its light)."""
    k = 0
    for k in range(int(height / 2)):
        yy = base - k * 2.0
        if not ctx.ok(x, yy - 8, 9):
            break
        D.stamp(ctx, m, lambda X_, Y_, yy=yy: np.abs(X_ - x - 0.5 * np.sin(Y_ / 6)) - w + 0 * Y_, x, yy, 6)
    top_y = base - k * 2.0
    D.stamp(ctx, m, lambda X_, Y_: D.sd_ellipse(X_, Y_, x, top_y - 6, 5, 7), x, top_y - 6, 10)
    return (x, top_y - 6)


def rocks(ctx, m, items, seed=23):
    """Irregular rocks (x, y, r): each a lumpy polygon; dropped when it would come near a piece."""
    rng = np.random.default_rng(seed)
    for (cx, cy, r) in items:
        if not ctx.ok(cx, cy, r * 1.6):
            continue
        k = rng.integers(5, 8)
        angs = np.sort(rng.uniform(0, 2 * np.pi, k))
        rads = r * rng.uniform(0.6, 1.4, k) * (1 + 0.4 * np.cos(angs - rng.uniform(0, np.pi)))

        def rock(X_, Y_, cx=cx, cy=cy, angs=angs, rads=rads):
            a_ = np.arctan2(Y_ - cy, X_ - cx) % (2 * np.pi)
            rr = np.interp(a_, np.concatenate([angs - 2 * np.pi, angs, angs + 2 * np.pi]), np.tile(rads, 3))
            return np.sqrt((X_ - cx) ** 2 + (Y_ - cy) ** 2) - rr
        D.stamp(ctx, m, rock, cx, cy, r * 1.5)


def palm(ctx, m, x, y, fronds, length=120.0, leaf=24.0, seed=1, width=2.2):
    """A date palm's crown from (x, y), usually beyond the wall: fronds of long paired leaflets at the given angles,
    drooping; each leaflet is dropped near a piece, so the crown parts round the pegs."""
    for k, ang in enumerate(fronds):
        D.frond(ctx, m, x, y, length * (0.85 + 0.3 * ((k * 0.618) % 1)), ang, 0.55, leaf, 22, style="fern",
                seed=seed * 7 + k, width=width)


def draw_framing(ctx, px, shapes, m):
    """Draws `shapes` into the silhouette mask m (or, for lit shapes, straight onto px). Returns (px, lights)."""
    lights = []
    for s in shapes:
        k = s["kind"]
        if k == "frond":
            D.frond(ctx, m, s["x"], s["y"], s["length"], s["angle"], s.get("droop", 0.5), s.get("leaf", 16),
                    s.get("n", 14), style=s.get("style", "laurel"), seed=s.get("seed", 1), width=s.get("width", 1.8),
                    twigs=s.get("twigs", 0))
        elif k == "pines":
            D.pines(ctx, m, [tuple(t) for t in s["trees"]], seed=s.get("seed", 4))
        elif k == "trunk":
            D.trunk(ctx, m, s["x"], s["y0"], s["y1"], s["w0"], s["w1"], lean=s.get("lean", 0.0), seed=s.get("seed", 2))
        elif k == "outcrop":
            D.outcrop(ctx, m, [tuple(p) for p in s["points"]], seed=s.get("seed", 7), rough=s.get("rough", 10.0))
        elif k == "rocks":
            rocks(ctx, m, [tuple(r) for r in s["items"]], seed=s.get("seed", 23))
        elif k == "palm":
            palm(ctx, m, s["x"], s["y"], s["fronds"], s.get("length", 120), s.get("leaf", 24), s.get("seed", 1),
                 s.get("width", 2.2))
        elif k == "rope":
            rope(ctx, m, s["a"], s["b"], s.get("sag", 20), s.get("width", 1.6))
        elif k == "lantern_post":
            lx, ly = lantern_post(ctx, m, s["x"], s.get("base", 600.0), s.get("height", 60.0))
            lights.append((lx, ly, s.get("s", 1.4)))
        elif k == "festoon":
            px = festoon(ctx, ctx.cover, px, s["spans"], s.get("top", TOP - 4), s.get("col", "#2A1E58"))
        elif k == "crystal":
            if all(ctx.ok(s["x"] + math.sin(s.get("tilt", 0)) * s["h"] * f, s["y"] - math.cos(s.get("tilt", 0)) * s["h"] * f,
                          s["w"] + 1) for f in (0, 0.35, 0.7, 1.0)):
                D.crystal(ctx, px, ctx.cover, s["x"], s["y"], s["h"], s["w"], s.get("tilt", 0.0), s.get("body", "#2A1E6A"),
                          s.get("lit", "#D6C8FF"))
        else:
            raise ValueError(f"unknown framing kind {k}")
    return px, lights


def auto_lights(ctx, n=9, box=(90, 380, 712, 516), seed=5, halo=5.0, wander=(9, 4)):
    """Small lights where their halo and their whole wander keep 8 units from every piece (F5), at most three in any
    40-unit band of height and 30 or more apart, picked from a shuffled list (a ranked pick lines up in a row)."""
    d = ctx.d1
    rng = np.random.default_rng(seed)
    cands = []
    x0, y0, x1, y1 = box
    for y in range(int(y0), int(y1), 5):
        for x in range(int(x0), int(x1), 5):
            s_ = 0.7 + 0.3 * rng.random()
            need = 8 + halo * s_ * 2.2
            if all(d[min(599, int(y + math.sin(2 * a) * wander[1])), min(799, int(x + math.cos(a) * wander[0]))] >= need
                   for a in np.linspace(0, 2 * math.pi, 12)):
                cands.append((x, y, s_))
    rng.shuffle(cands)
    out = []
    for x, y, s_ in cands:
        band = [b for (_a, b, _s) in out if abs(b - y) < 40]
        if len(band) < 2 and all(abs(b - y) >= 6 for b in band) and \
                all((x - a) ** 2 + (y - b) ** 2 >= 34 ** 2 for (a, b, _s) in out):
            out.append((x + rng.uniform(-2, 2), y + rng.uniform(-2, 2), s_))
        if len(out) >= n:
            break
    return out


# ------------------------------------------------------------------------------------------------ the dress
PEG_SCALE = 100.0           # a positional mask, glow or tone smaller than this (board units) that reaches a piece
POSITIONAL = ("x", "y", "disc", "poly", "not-poly", "near")     # the mask terms that draw a shape where the author puts it


def _piece_points(level):
    """(x, y, radius) samples of every piece: a peg at its home and along a mover's path, a brick along its length."""
    from board import mover_pos, pieces
    from .author import brick_samples
    pts = []
    for kind, p in pieces(level):
        if kind == "peg":
            ts = [p["move"]["period"] * k / 24 for k in range(24)] if p.get("move") else [0.0]
            pts += [(*mover_pos(p, t_), p.get("r", 10.0)) for t_ in ts]
        else:
            pts += [(x, y, p["thickness"] / 2) for (x, y) in brick_samples(p, 4.0)]
    return pts


def _footprint(pts):
    """Every piece's footprint at 1x (a mover along its path, a brick along its length) as a boolean board."""
    from PIL import ImageDraw
    im = Image.new("L", (800, 600), 0)
    dr = ImageDraw.Draw(im)
    for (x, y, r) in pts:
        dr.ellipse([x - r, y - r, x + r, y + r], fill=255)
    return np.asarray(im) > 0


class _Shape:
    """What mask_of needs to draw positional terms at 1x."""

    def __init__(self, features):
        self.S, self.features = 1, features or {}


def _singled(spec, features):
    """The part of the board a mask's positional terms single out, at 1x: their product thresholded at 0.5, or its
    complement when that is the smaller (an inverted term singles out its hole). None when the mask has no positional
    term (luma and painting-derived masks follow the painting)."""
    terms = [t for t in spec if t[0] in POSITIONAL]
    if not terms:
        return None
    X, Y = D.grid(1)
    m = mask_of(terms, _Shape(features), X, Y, np.zeros_like(X)) > 0.5
    return m if m.sum() <= (~m).sum() else ~m


def _small_at_piece(sing, foot):
    """The extent (board units) of the smallest connected part of `sing` that overlaps a piece, if under PEG_SCALE."""
    from PIL import ImageDraw
    hit = sing & foot
    if not hit.any():
        return None
    im = Image.fromarray((sing * 255).astype(np.uint8))
    worst = None
    ys, xs = np.nonzero(hit)
    seen = np.zeros_like(sing)
    for (y, x) in zip(ys[::7], xs[::7]):
        if seen[y, x]:
            continue
        work = im.copy()
        ImageDraw.floodfill(work, (int(x), int(y)), 128, thresh=0)
        comp = np.asarray(work) == 128
        seen |= comp
        cy, cx = np.nonzero(comp)
        ext = max(cx.max() - cx.min() + 1, cy.max() - cy.min() + 1)
        if ext < PEG_SCALE and (worst is None or ext < worst):
            worst = ext
    return worst


def lint(recipe, level):
    """The structural guard on the dress (rounds 5 and 6: game designer G21 and G23, UX G4, m7 and m8, critic M2 and
    N13, N14, N18 and N20). The print check judges a board's median and 90th percentile, so it cannot see one peg singled
    out, and it compares the dress with the graded scene, so it cannot see `tone` at all; these rules refuse such terms by
    their shape instead:
    - the quiet's blur pinned at QUIET_BLUR_MIN (NaN refused too), and `regionQuiet` within 0-1 (the runtime's scale);
    - no `dist` term in a jewel region or keep mask: it is the unblurred clearance, a coin round every peg;
    - by coverage, not centre: in a jewel region, keep mask or `tone`, the part the positional terms (disc, x, y, poly,
      not-poly, near, and any product of them) single out must not be smaller than PEG_SCALE across where it covers a
      piece (a disc nudged off a peg, a box, a one-point `near` or a lens of large discs are all coins round it);
    - a glow smaller than PEG_SCALE in radius must not reach a piece.
    The problems, empty if none."""
    P = []
    name = recipe.get("name")
    j = (recipe.get("dress") or {}).get("jewel") or {}
    if j.get("quiet"):
        qb = float(j.get("quietBlur", QUIET_BLUR))
        if not (qb >= QUIET_BLUR_MIN):
            P.append(f"{name}: quietBlur {qb:g} (the quiet's blur is pinned at {QUIET_BLUR_MIN:g}: less prints a coin "
                     f"round every peg)")
    rq = float(j.get("regionQuiet", QUIET_REGION))
    if not (0.0 <= rq <= 1.0):
        P.append(f"{name}: regionQuiet {rq:g} (the runtime's scale runs 0-1)")
    masks = [("jewel region", r.get("mask", [])) for r in j.get("regions", [])]
    masks += [("keep mask", k) for k in ([j["keepMask"]] if j.get("keepMask") else []) + list(j.get("keepMasks", []))]
    for where, spec in masks:
        if any(term[0] == "dist" for term in spec):
            P.append(f"{name}: a `dist` term in a {where} (the unblurred clearance: a coin round every peg)")
    masks += [("tone", tn.get("mask", [])) for tn in recipe.get("tone", [])]
    pts = _piece_points(level)
    foot = _footprint(pts)
    for where, spec in masks:
        sing = _singled(spec, recipe.get("features"))
        if sing is None:
            continue
        ext = _small_at_piece(sing, foot)
        if ext is not None:
            P.append(f"{name}: the {where} {spec} singles out a shape {ext} units across over a piece (under "
                     f"{PEG_SCALE:g}): a coin round it")
    for g in (recipe.get("dress") or {}).get("glows", []):
        if g.get("r", 0) < PEG_SCALE:
            near = min(math.hypot(g["x"] - x, g["y"] - y) - pr for (x, y, pr) in pts)
            if near < g["r"]:
                P.append(f"{name}: a glow of radius {g['r']:g} at ({g['x']}, {g['y']}) reaches a piece ({near:.1f} from "
                         f"its edge): a halo round it")
    return P


def dress(recipe, sc, level, S, t=0.0, unpinned=False):
    """The dressed scene (float RGB at S) and the context (framing coverage, rim light, small lights). Refuses a
    recipe `lint` faults. `unpinned` is for the self-tests alone: it lets a known-bad dress (a per-peg coin) be drawn so
    the print check can be shown to catch it."""
    if not unpinned:
        probs = lint(recipe, level)
        if probs:
            raise ValueError("; ".join(probs))
    d = recipe.get("dress") or {}
    ctx = Ctx(level, S, recipe.get("features"))
    ctx.recipe = recipe
    X, Y = D.grid(S)
    Lsc = srgb_to_oklab(sc)[..., 0]
    px = sc
    j = d.get("jewel")
    if j:
        regions = [(mask_of(r.get("mask", []), ctx, X, Y, Lsc), r["hex"], r.get("chroma", 0.1)) for r in j.get("regions", [])]
        keepm = None
        if j.get("quiet"):
            a, b = j["quiet"]
            sigma = min(float(j.get("quietBlur", QUIET_BLUR)), QUIET_BLUR_MAX)
            dist = np.minimum(ctx.dist_at_S().astype(np.float32), CLEARANCE_FAR)
            near = smooth(a, b, blur(dist, sigma * S) if sigma > 0.3 else dist)
            # a region takes `regionQuiet` of it (0.6 by default): less keeps a region from surviving only where the
            # layout is not, which prints the layout's envelope on the cleared board (game designer round 4, G18)
            rq = float(j.get("regionQuiet", QUIET_REGION))
            regions = [(rm * (1 - rq * near), hx, cr) for (rm, hx, cr) in regions]
            keepm = 1 - QUIET_KEEP * near
        keeps = ([j["keepMask"]] if j.get("keepMask") else []) + list(j.get("keepMasks", []))
        if keeps:
            # regions the jewel leaves alone (a lamp's warm window, a creature's own colour): their union
            km = 1 - np.maximum.reduce([mask_of(k, ctx, X, Y, Lsc) for k in keeps])
            keepm = km if keepm is None else keepm * km
        px = D.jewel(px, S, [tuple(b) for b in j["bands"]], chroma=j.get("chroma", 0.9),
                     value_hues=[tuple(v) for v in j["valueHues"]] if j.get("valueHues") else None,
                     keep_hi=j.get("keepHi", 0.75), keep=j.get("keep", 0.30), mask=keepm, mix=j.get("mix", 0.5),
                     floor=j.get("floor", 0.03), regions=regions)
    sway = 0.6 * math.sin(t * 2 * math.pi / 6.0)
    for sh in d.get("shafts", []):
        px = D.shafts(px, S, origin=tuple(sh.get("origin", (-140, -220))), angles=tuple(sh["angles"]),
                      widths=tuple(sh["widths"]), k=min(0.08, sh.get("k", 0.07)), col=sh.get("col", "#BFD2FF"),
                      seed=sh.get("seed", 3), reach=sh.get("reach", 900.0), sway=sway, near=sh.get("near", 150.0))
    for g in d.get("glows", []):
        px = D.glow(px, S, g["x"], g["y"], g["r"], g["col"], g["k"])
    for e in d.get("extras", []):
        if e["kind"] == "compass_rose":
            px = D.compass_rose(ctx, px, e["x"], e["y"], e["R"])
        elif e["kind"] == "chart_border":
            px = D.chart_border(px, S)
    m = np.zeros_like(X)
    px, post_lights = draw_framing(ctx, px, d.get("framing", []), m)
    if m.any():
        sil = d.get("silhouette", {})
        px = D.silhouette(ctx, px, m, body=sil.get("body", "#05060E"), rim=sil.get("rim", "#9EB4FF"),
                          rim_k=sil.get("rimK", 0.45), inner=sil.get("inner", "#0C1230"), inner_k=sil.get("innerK", 0.5),
                          seed=sil.get("seed", 5), snow=sil.get("snow"))
    for li in d.get("lights", []):
        pts = [tuple(p) for p in li.get("points", [])]
        if li.get("auto"):
            a = li["auto"]
            pts += auto_lights(ctx, a.get("n", 9), tuple(a.get("box", (90, 380, 712, 516))), a.get("seed", 5),
                               li.get("halo", 5.0))
        if li.get("posts"):
            pts += post_lights
        px = D.points(ctx, px, pts, li.get("col", "#FFC86E"), r=li.get("r", 1.4), k=li.get("k", 0.8),
                      halo=li.get("halo", 5.0), hk=li.get("hk", 0.24))
    return np.clip(px, 0, 1), ctx
