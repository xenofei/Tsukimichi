"""Readability and colour (level-method.md section 8: F6, F7, F9), for any level, with the approved measurements
(docs/design/v9/rich2/src/readcheck.py, round 3).

F6: each peg kind's lit face against every position it can be dealt to, on the piece-free board (scene, veil, chrome,
bucket and spill), at 1x and 0.8x: orange at its candidates, green at the pegs that may be green (format v2), blue and
purple at every peg, movers at 24 points along their path, bricks against the band round them. The margin is the
face's 80th percentile less the ring's 90th (2-9 units out). Rule: every margin >= 0.20, and none more than 0.02 below
the same board with the scene undressed (the rich pass's board: graded painting and veil, no dress). For a new level
that undressed board is its "approved" reference: the dress may cost readability no more than the pilots' did.

F7: two jewels on the dressed scene (30-degree hue bins, exclusive windows): second jewel >= 15% of the coloured
pixels and mean chroma >= 0.06.

F9: orange pegs as a protanope sees them: the 10th percentile of the a/b distance from core to ground >= 0.12, or no
worse than the undressed board's less 0.02.
"""
import numpy as np
from PIL import Image

from . import paths  # noqa: F401
import readcheck as rk
from board import mover_pos, pieces
from r2lib import LUM

KINDS = ("blue", "orange", "green", "purple")
# the approved pilots' faces (rich2/supervisor/readcheck.json, the median of six boards): used for a kind a level
# does not deal (no greens before level 3) or deals only on movers
PILOT_FACES = {"blue": 0.689, "orange": 0.620, "green": 0.718, "purple": 0.593}
F6_MIN, F6_DROP, F7_SECOND, F7_CHROMA, F9_MIN = 0.20, 0.02, 0.15, 0.06, 0.12


def worst_cases(bd2, level, faces):
    out = {}
    for scale_name, s in (("1x", 1.0), ("0.8x", 0.8)):
        im = Image.fromarray((np.clip(bd2, 0, 1) * 255).astype(np.uint8)).resize((int(800 * s), int(600 * s)), Image.LANCZOS)
        Y = np.asarray(im, np.float32) / 255 @ LUM
        for kind in KINDS:
            face = faces[kind]
            worst = (9.0, None)
            for (k, d) in pieces(level):
                if k != "peg":
                    continue
                if kind == "orange" and not d.get("canBeOrange", True):
                    continue
                if kind == "green" and not d.get("canBeGreen", True):
                    continue
                pts = [mover_pos(d, d["move"]["period"] * j / 24) for j in range(24)] if d.get("move") else [(d["x"], d["y"])]
                for (x, y) in pts:
                    mg = face - rk.ring_p90(Y, x, y, d.get("r", 10), s)
                    if mg < worst[0]:
                        worst = (mg, (round(x), round(y)))
            out[f"{kind} {scale_name}"] = [round(worst[0], 3), worst[1]]
    return out


def faces(comp1x, level, colours):
    f = rk.faces_from(comp1x, level, colours)
    return {k: f.get(k, PILOT_FACES[k]) for k in KINDS}


def measure(level, colours, comp1x, comp1x_base, bd_new, bd_base, dressed2x):
    """comp1x: the composite at 1x; *_base: the same with the scene undressed; bd_*: piece-free boards at 2x;
    dressed2x: the dressed scene at 2x. Returns (report, fails)."""
    fc_ = faces(comp1x, level, colours)
    new, old = worst_cases(bd_new, level, fc_), worst_cases(bd_base, level, fc_)
    rows, fails = {}, []
    for key in new:
        drop = round(old[key][0] - new[key][0], 3)
        rows[key] = {"worst": new[key][0], "at": new[key][1], "undressed": old[key][0], "drop": drop}
        if new[key][0] < F6_MIN or drop > F6_DROP:
            fails.append(f"F6 {key}: worst {new[key][0]} at {new[key][1]} (undressed {old[key][0]})")
    bm = rk.brick_margins(bd_new, comp1x, level, colours)
    if bm is not None and bm[0] < F6_MIN:
        bm_old = rk.brick_margins(bd_base, comp1x_base, level, colours)
        if bm_old is None or bm_old[0] - bm[0] > F6_DROP:
            fails.append(f"F6 bricks: {bm} (undressed {bm_old})")
    pr = rk.protan_orange(comp1x, level, colours)
    pr_old = rk.protan_orange(comp1x_base, level, colours)
    if pr is not None and pr < F9_MIN and pr < (pr_old or 0) - 0.02:
        fails.append(f"F9 protan orange separation {pr} (undressed {pr_old})")
    jw = rk.jewels(dressed2x)
    if jw["second_share"] < F7_SECOND or jw["mean_chroma"] < F7_CHROMA:
        fails.append(f"F7 jewels: second {jw['second_jewel_hue']} at {jw['second_share']}, chroma {jw['mean_chroma']}")
    rep = {"faces": {k: round(v, 3) for k, v in fc_.items()}, "worst_case": rows,
           "worst_margin": min(r["worst"] for r in rows.values()), "largest_drop": max(r["drop"] for r in rows.values()),
           "bricks_min_margin": bm, "protan_orange_p10": pr, "protan_orange_p10_undressed": pr_old, "jewels": jw}
    return rep, fails


def selftest(verbose=True):
    """Synthetic boards: a bright ground round the pegs must fail F6 and a dark one pass; one hue must fail F7 and two
    jewels pass; orange on an orange ground must fail F9 and on blue pass."""
    level = {"pegs": [{"x": 300, "y": 300, "canBeOrange": True}, {"x": 500, "y": 300, "canBeOrange": True}], "bricks": []}
    fc_ = dict(PILOT_FACES)
    dark = np.full((1200, 1600, 3), 0.08, np.float32)
    bright = np.full((1200, 1600, 3), 0.62, np.float32)
    w_dark = min(v[0] for v in worst_cases(dark, level, fc_).values())
    w_bright = min(v[0] for v in worst_cases(bright, level, fc_).values())
    from r2lib import oklab_to_srgb

    def hue_img(hues):
        h, w = 1200, 1600
        lab = np.zeros((h, w, 3), np.float32)
        lab[..., 0] = 0.35
        band = np.repeat(np.arange(w)[None, :] * len(hues) // w, h, 0)
        for i, hd in enumerate(hues):
            a = np.radians(hd)
            lab[band == i, 1], lab[band == i, 2] = 0.09 * np.cos(a), 0.09 * np.sin(a)
        return np.clip(oklab_to_srgb(lab), 0, 1)
    one = rk.jewels(hue_img([255]))
    two = rk.jewels(hue_img([255, 255, 330]))

    def orange_on(ground):
        comp = np.empty((600, 800, 3), np.float32)
        comp[:] = ground
        yy, xx = np.mgrid[0:600, 0:800]
        for p in level["pegs"]:
            comp[(xx - p["x"]) ** 2 + (yy - p["y"]) ** 2 < 100] = (0.95, 0.55, 0.20)
        return rk.protan_orange(comp, level, ["orange", "orange"])
    p_bad, p_good = orange_on((0.55, 0.30, 0.12)), orange_on((0.10, 0.16, 0.42))
    cases = [("F6: pegs on a dark ground", w_dark >= F6_MIN, True), ("F6: pegs on a bright ground", w_bright >= F6_MIN, False),
             ("F7: one hue", one["second_share"] >= F7_SECOND, False), ("F7: two jewels", two["second_share"] >= F7_SECOND, True),
             ("F9: orange on an orange ground", p_bad >= F9_MIN, False), ("F9: orange on lapis", p_good >= F9_MIN, True)]
    ok = True
    for name, passed, want in cases:
        ok &= passed == want
        if verbose:
            print(f"  {'ok ' if passed == want else 'BAD'} readcheck: {name}: passes={passed}, expected {want}")
    return ok
