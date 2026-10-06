"""Readability and colour (level-method.md section 8: F6, F7, F9), and the ghost check, for any level.

F6 (the approved measurement, rich2/src/readcheck.py round 3): each peg kind's lit face against every position it can
be dealt to, on the piece-free board (scene, veil, chrome, bucket and spill), at 1x and 0.8x: orange at its candidates,
green at the pieces that may be green (format v2), blue and purple at every peg, movers at 24 points along their path,
bricks against the band round them. The margin is the face's 80th percentile less the ring's 90th (2-9 units out); the
faces are measured on the composite at each scale (UX round 1, n2). Rule: every margin >= 0.20, and none more than
0.02 below the same board with the scene undressed (graded painting and veil, no dress): for F6 that reference is sound,
since the jewel keeps lightness by construction and the 0.20 floor is absolute (UX round 1).

F7 (round 1, critic P9): two jewels on the dressed scene. Coloured pixels (OKLab chroma > 0.03) are binned by hue; the
first jewel is the three-bin window round the fullest bin, the second the fullest three-bin window centred 60 degrees or
more away, counting only bins outside the first. Rule: the two windows' mean hues (circular means of their pixels) at
least 60 degrees apart, the second holding at least 15% of the coloured pixels, mean chroma at least 0.06.

F9 (round 1, critic P9 and UX M1): orange as a protanope sees it (Machado 2009, severity 1), at every place an orange
can be dealt: every candidate, movers at four points of their cycle. Per place, the OKLab a/b distance from the peg's
core to the ground 12-20 px round it. Rule: the 10th percentile at least 0.12, or at least 0.101, the lowest approved
pilot (exp-p1). (The level's own undressed board is no reference for F9: nobody approved it.)

Ghost (round 1, game designer M3 and UX m1): the veil and the jewel's quietening must not print a disc round each peg
that stays when the peg clears. On the piece-free dressed board, for isolated pegs (no other piece within 40 units of
their edge), the OKLab distance between the mean colour 5-12 units outside the peg and the open ground 32-48 units out.
Rule: the median at most 0.066, the approved pilots' highest. (The runtime draws the veil per piece and fades it with
the piece, so this is the board in play; the print below is what stays when the pieces clear.)

Print (round 2, all three supervisors' G1; round 3, UX m4 and critic N3): the dress must not print anything round a
peg, as hue or as lightness, that the painting does not have and that shows. On the dressed scene with no pieces and no
veil, the dress's own change (OKLab, dressed minus undressed: the painting's detail cancels), per peg (a mover at its
home), sector by sector: 5-12 units outside the peg against 20-36 out, and 5-20 against 45-70 out (a ring between the
first pair, or a wide disc, shows in the second); the median over eight sectors (an edge crossing the peg is not a
print), the larger pair. A peg counts what its print exceeds 1.2 times the painting's own fine grain round it (the
undressed scene less its 4-unit blur, as a robust spread so stars do not inflate it): a textured game painting hides
part of what a flat night sky shows. Every peg counts, isolated or not; with fewer than three measurable the board as a
whole is measured, so it is never vacuous. Rule: the median at most PRINT_MED and the 90th percentile at most
PRINT_P90, and the hue part's median (the a/b change less 1.2 times the painting's a/b grain) at most PRINT_HUE_MED.
Set between known cases, all self-tested (round 4, critic M1 and UX m5; round 5, UX m7): the six approved pilots measure
median 0-0.003, p90 0-0.032 and hue 0-0.0074, the ten levels as built p90 at most 0.034 and hue at most 0.0143; round
2's per-peg quiet (`[22, 12]`, no blur) put back on the real boards measures median 0.010-0.088 on most, and on the
textured 1-4 and 2-1, where the median stays near 0, p90 0.074 and 0.045 (2-1 is the narrowest margin); UX's syn05 hue
coin measures hue 0.029-0.046 on all ten. What it cannot see (README, step 9): one peg singled out, a coin round a
minority of the pegs (the candidates alone, say), `tone`, hue coins on 1-4 up to OKLab 0.05, weaker hue coins and
lightness coins on the most textured boards. The dress's routes to such prints are closed by their shape instead
(`dress.lint`: the quiet's blur pinned, no `dist` term, no positional mask, tone or glow at a peg's scale over a
piece), not by this check.
"""
import math

import numpy as np
from PIL import Image

from . import paths  # noqa: F401
import readcheck as rk
from board import mover_pos, pieces
from r2lib import LUM, srgb_to_oklab

KINDS = ("blue", "orange", "green", "purple")
# the approved pilots' faces (rich2/supervisor/readcheck.json, the median of six boards): for a kind a deal leaves out
PILOT_FACES = {"blue": 0.689, "orange": 0.620, "green": 0.718, "purple": 0.593}
F6_MIN, F6_DROP, F7_SECOND, F7_CHROMA, F7_APART = 0.20, 0.02, 0.15, 0.06, 60.0
F7_WALLS = 1.5               # the second jewel in the wall strips at most 1.5 times their share of the area (G18)
F7_WALL_WINDOW = 5.0         # and in any 100-unit tall window (UX round 6, m9: round 4's strips read 5.7-6.5)
F9_MIN, F9_PILOT = 0.12, 0.101
GHOST_MAX = 0.066
PRINT_MED, PRINT_P90 = 0.006, 0.040
PRINT_HUE_MED = 0.022        # the hue part's median (UX round 5, m7)


def down(px2, s):
    return np.asarray(Image.fromarray((np.clip(px2, 0, 1) * 255).astype(np.uint8)).resize((int(800 * s), int(600 * s)),
                                                                                          Image.LANCZOS), np.float32) / 255


def faces_at(comp2, level, colours, s):
    f = rk.faces_from(down(comp2, s), level, colours, s)
    return {k: f.get(k, PILOT_FACES[k]) for k in KINDS}


def worst_cases(bd2, level, faces_by_scale):
    out = {}
    for scale_name, s in (("1x", 1.0), ("0.8x", 0.8)):
        Y = down(bd2, s) @ LUM
        for kind in KINDS:
            face = faces_by_scale[scale_name][kind]
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


def jewels(sc2):
    """Two jewels, by hue distance (see the module's docstring)."""
    lab = srgb_to_oklab(sc2[82:1188, 150:1450])
    a, b = lab[..., 1], lab[..., 2]
    C = np.sqrt(a * a + b * b)
    hue = (np.degrees(np.arctan2(b, a)) + 360) % 360
    col = C > 0.03
    h = hue[col]
    bins = np.bincount((h // 30).astype(int), minlength=12).astype(float)
    tot = max(bins.sum(), 1)
    win = lambda i: {(i - 1) % 12, i % 12, (i + 1) % 12}
    first = int(np.argmax(bins))
    taken = win(first)
    far = [i for i in range(12) if min(abs(i - first), 12 - abs(i - first)) >= 2]
    second = max(far, key=lambda i: sum(bins[j] for j in win(i) - taken))

    def cmean(binset):
        m = np.isin((h // 30).astype(int), list(binset))
        if not m.any():
            return None
        r = np.radians(h[m])
        return float((np.degrees(math.atan2(np.sin(r).mean(), np.cos(r).mean())) + 360) % 360)
    h1, h2 = cmean(taken), cmean(win(second) - taken)
    apart = None if h1 is None or h2 is None else min(abs(h1 - h2), 360 - abs(h1 - h2))
    # where the second jewel lies: its share in the 50-unit strips along the walls against their share of the area
    # (game designer round 4, G18: with the runtime's quiet a region survives mainly where the layout is not, and at
    # the walls it reads as coloured light leaking in, the layout's envelope)
    W = C.shape[1]
    sec_px = np.zeros(C.shape, bool)
    sec_px[col] = np.isin((h // 30).astype(int), list(win(second) - taken))
    strip = np.zeros(C.shape, bool)
    strip[:, :100], strip[:, W - 100:] = True, True
    wall_x = float(sec_px[strip].sum() / max(sec_px.sum(), 1) / strip.mean())
    # the same figure chroma-weighted in 100-unit tall windows, the worst window holding 5% or more of the second jewel
    # (UX rounds 5 and 6, n9 and m9; critic N18: a leak confined to a corner is diluted by the full height). Reported as
    # [figure, the window's centre y in board units] and gated at F7_WALL_WINDOW
    wgt = np.zeros(C.shape, np.float32)
    wgt[sec_px] = C[sec_px]
    wtot, worst_win = float(wgt.sum()), (0.0, None)
    for y0 in range(0, C.shape[0] - 199, 50):
        ww, ss = wgt[y0:y0 + 200], strip[y0:y0 + 200]
        if wtot > 0 and ww.sum() >= 0.05 * wtot:
            worst_win = max(worst_win, (float(ww[ss].sum() / ww.sum() / ss.mean()), (y0 + 82) // 2 + 50),
                            key=lambda t: t[0])
    return {"coloured_px_pct": round(float(col.mean()) * 100, 1), "mean_chroma": round(float(C[col].mean()), 3),
            "second_at_walls_x": round(wall_x, 2),
            "second_at_walls_worst_window": [round(worst_win[0], 2), worst_win[1]],
            "first_jewel_hue": None if h1 is None else round(h1), "first_share": round(float(sum(bins[j] for j in taken) / tot), 3),
            "second_jewel_hue": None if h2 is None else round(h2),
            "second_share": round(float(sum(bins[j] for j in win(second) - taken) / tot), 3),
            "apart_deg": None if apart is None else round(apart), "bins_pct": [round(float(v / tot) * 100, 1) for v in bins]}


def f7_fails(jw):
    out = []
    if jw["second_share"] < F7_SECOND:
        out.append(f"second jewel holds {jw['second_share']} of the coloured pixels (0.15)")
    if jw["apart_deg"] is None or jw["apart_deg"] < F7_APART:
        out.append(f"the jewels' hues are {jw['apart_deg']} degrees apart (60)")
    if jw["mean_chroma"] < F7_CHROMA:
        out.append(f"mean chroma {jw['mean_chroma']} (0.06)")
    if jw.get("second_at_walls_x", 0) > F7_WALLS:
        out.append(f"the second jewel sits at the walls: {jw['second_at_walls_x']} times the strips' share of the area "
                   f"({F7_WALLS}; fade the region within 60 units of the walls)")
    wwin = jw.get("second_at_walls_worst_window", [0, None])
    if wwin[0] > F7_WALL_WINDOW:
        out.append(f"the second jewel sits at the walls in the window round y {wwin[1]}: {wwin[0]} times the strips' "
                   f"share ({F7_WALL_WINDOW}; fade the region within 60 units of the walls)")
    return out


def protan_seps(comp1, places):
    """The protan a/b separation at each place: a peg (x, y, r), core (inner half) against the ground 12-20 px round
    it; or an orange-candidate brick ("brick", brick), its core (the inner half of its thickness) against the ground
    3-11 px outside its edge (round 2, critic G6 and UX n2)."""
    lin = np.where(comp1 <= 0.04045, comp1 / 12.92, ((comp1 + 0.055) / 1.055) ** 2.4)
    sim = np.clip(lin @ rk.PROTAN.T, 0, 1)
    sim = np.where(sim <= 0.0031308, sim * 12.92, 1.055 * np.power(sim, 1 / 2.4) - 0.055).astype(np.float32)
    lab = srgb_to_oklab(sim)
    out = []
    for place in places:
        if place[0] == "brick":
            out.append(_brick_sep(lab, place[1]))
            continue
        (x, y, r) = place
        yy, xx = np.mgrid[int(y - r - 22):int(y + r + 22), int(x - r - 22):int(x + r + 22)]
        ok = (xx >= 0) & (yy >= 0) & (xx < comp1.shape[1]) & (yy < comp1.shape[0])
        yy, xx = yy[ok], xx[ok]
        dd = np.sqrt((xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2)
        core = lab[yy[dd < r * 0.5], xx[dd < r * 0.5]][:, 1:].mean(0)
        rm = (dd >= 12) & (dd <= 20)
        ring = lab[yy[rm], xx[rm]][:, 1:].mean(0)
        out.append(float(np.sqrt(((ring - core) ** 2).sum())))
    return out


def _brick_sep(lab, b):
    import framecheck as fc
    h, w = lab.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    X, Y = xx + 0.5, yy + 0.5
    ht = b.get("thickness", 20) / 2
    if b["kind"] == "line":
        d = fc.sd_line_capsule(X, Y, b["x1"], b["y1"], b["x2"], b["y2"], ht)
    else:
        d = fc.sd_arc_capsule(X, Y, b["x"], b["y"], b["r"], b["start"], b["sweep"], ht)
    core, ring = d < -ht * 0.5, (d >= 3) & (d <= 11)
    return float(np.sqrt(((lab[ring][:, 1:].mean(0) - lab[core][:, 1:].mean(0)) ** 2).sum()))


def ghost(bd1, level):
    """The median ghost-disc distance over isolated pegs (None when no peg is isolated), and the worst place."""
    lab = srgb_to_oklab(bd1)
    peg_list = [(d["x"], d["y"], d.get("r", 10)) for k, d in pieces(level) if k == "peg" and "move" not in d]
    import framecheck as fc
    dist = fc.piece_distance(level)
    yy, xx = np.mgrid[0:600, 0:800]
    vals = []
    for i, (x, y, r) in enumerate(peg_list):
        others = [math.hypot(x - a, y - b) - r - ra for j, (a, b, ra) in enumerate(peg_list) if j != i]
        if min(others, default=99) < 40 or x < 75 + r + 48 or x > 725 - r - 48 or y < 41 + r + 48 or y > 520:
            continue
        d = np.sqrt((xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2) - r
        near = (d >= 5) & (d <= 12)
        open_ = (d >= 32) & (d <= 48) & (dist >= 30)
        if near.sum() < 20 or open_.sum() < 40:
            continue
        vals.append((float(np.sqrt(((lab[near].mean(0) - lab[open_].mean(0)) ** 2).sum())), (round(x), round(y))))
    if not vals:
        return None, None, 0
    vals.sort()
    return round(vals[len(vals) // 2][0], 3), vals[-1], len(vals)


PRINT_BANDS = (((5, 12), (20, 36), 16), ((5, 20), (45, 70), 30))   # (near, far, far's clearance from every piece)
PRINT_TEXTURE = 1.2          # a print within this many times the painting's own local variation does not show


def dress_print(dressed1, undressed1, level):
    """The dress's print round the pegs (see the module's docstring): (median, p90, worst (value, at), samples, mode).

    Per peg (a mover at its home), in eight sectors round it: the dress's change (dressed minus undressed, OKLab) near
    the peg against further out, for two pairs of bands (5-12 against 20-36 units from its edge, and 5-20 against
    45-70: a ring between the first pair or a wide disc both show in the second). Per pair the median over the sectors
    (so an edge that crosses the peg moves only the sectors it crosses; a disc moves them all), and the larger pair.
    A print shows only where the ground is flat: a peg counts what its print exceeds PRINT_TEXTURE times the undressed
    painting's own fine grain round it (the undressed scene less its 4-unit blur, as a robust spread, 1.4826 times its
    median absolute deviation, so point stars and sparkles do not inflate it): a textured game painting hides part of
    what a flat night sky shows. The excess counts, not all or nothing (critic round 4 M1, UX m5: an all-or-nothing
    rule on the plain spread zeroed most pegs and let round 2's coin through on 2-2).

    The hue part on its own (the a/b change, less 1.2 times the painting's a/b grain): a painting's lightness texture
    does not hide a shift of hue, so a hue coin on a textured game painting shows although the whole change is within
    its grain (UX round 5, m7). Returned last: (median, p90, worst, samples, mode, hue median)."""
    lab_d, lab_u = srgb_to_oklab(dressed1), srgb_to_oklab(undressed1)
    diff = lab_d - lab_u
    from r2lib import blur
    # the painting's fine grain (what hides a print): the undressed scene less its 4-unit blur
    grain = lab_u - np.stack([blur(lab_u[..., c], 4.0) for c in range(3)], -1)
    import framecheck as fc
    dist = fc.piece_distance(level)
    yy, xx = np.mgrid[0:600, 0:800]
    inside = (xx > 80) & (xx < 720) & (yy > 45) & (yy < 515)
    vals = []
    for d in level["pegs"]:
        x, y, r = d["x"], d["y"], d.get("r", 10)
        R_ = r + 72
        x0, x1, y0, y1 = int(max(0, x - R_)), int(min(800, x + R_)), int(max(0, y - R_)), int(min(600, y + R_))
        X, Y = xx[y0:y1, x0:x1] + 0.5 - x, yy[y0:y1, x0:x1] + 0.5 - y
        rr = np.sqrt(X ** 2 + Y ** 2) - r
        sec = ((np.degrees(np.arctan2(Y, X)) + 360) // 45).astype(int) % 8
        df, ins, ds, lu = diff[y0:y1, x0:x1], inside[y0:y1, x0:x1], dist[y0:y1, x0:x1], grain[y0:y1, x0:x1]
        best = hue = None
        for (n0, n1), (f0, f1), clear in PRINT_BANDS:
            near_all = (rr >= n0) & (rr <= n1) & ins
            far_all = (rr >= f0) & (rr <= f1) & (ds >= clear) & ins
            per, per_h = [], []
            for k in range(8):
                n, m = near_all & (sec == k), far_all & (sec == k)
                if n.sum() >= 4 and m.sum() >= 8:
                    dv = df[n].mean(0) - df[m].mean(0)
                    per.append(float(np.linalg.norm(dv)))
                    per_h.append(float(np.hypot(dv[1], dv[2])))
            if len(per) >= 4:
                v, vh = float(np.median(per)), float(np.median(per_h))
                best = v if best is None else max(best, v)
                hue = vh if hue is None else max(hue, vh)
        if best is None:
            continue
        ring = (rr >= 5) & (rr <= 70) & ins
        texture = texture_h = 0.0
        if ring.sum() > 20:
            s = lu[ring]
            mad = 1.4826 * np.median(np.abs(s - np.median(s, 0)), 0)
            texture, texture_h = float(np.linalg.norm(mad)), float(np.hypot(mad[1], mad[2]))
        vals.append((max(0.0, best - PRINT_TEXTURE * texture), (round(x), round(y)),
                     max(0.0, hue - PRINT_TEXTURE * texture_h)))
    if len(vals) >= 3:
        v = np.array([a for a, _, _ in vals])
        w = max(vals)
        hm = float(np.median([h for _, _, h in vals]))
        return (round(float(np.median(v)), 4), round(float(np.percentile(v, 90)), 4), (round(w[0], 4), w[1]), len(vals),
                "pegs", round(hm, 4))
    near, open_ = (dist >= 5) & (dist <= 12) & inside, (dist >= 30) & inside
    if near.sum() < 20 or open_.sum() < 40:
        return 9.0, 9.0, (9.0, None), 0, "no open ground", 9.0
    g = round(float(np.linalg.norm(diff[near].mean(0) - diff[open_].mean(0))), 4)
    return g, g, (g, None), 1, "board", g


def print_ok(pr):
    """The print rule on a dress_print result: median, p90, and the hue part's median."""
    return pr[0] <= PRINT_MED and pr[1] <= PRINT_P90 and pr[5] <= PRINT_HUE_MED


def measure(level, colours, comp2, base2, bd_new, bd_old, dressed2, orange_views, undressed2=None):
    """comp2/base2: the dealt composite with the scene dressed/undressed (2x); bd_*: the piece-free boards (2x);
    dressed2: the dressed scene (2x); orange_views: [(composite 2x with every candidate orange, [(x, y, r) ...])] at
    a few moments of the movers' cycle. Returns (report, fails)."""
    faces = {"1x": faces_at(comp2, level, colours, 1.0), "0.8x": faces_at(comp2, level, colours, 0.8)}
    new, old = worst_cases(bd_new, level, faces), worst_cases(bd_old, level, faces)
    rows, fails = {}, []
    for key in new:
        drop = round(old[key][0] - new[key][0], 3)
        rows[key] = {"worst": new[key][0], "at": new[key][1], "undressed": old[key][0], "drop": drop}
        if new[key][0] < F6_MIN or drop > F6_DROP:
            fails.append(f"F6 {key}: worst {new[key][0]} at {new[key][1]} (undressed {old[key][0]})")
    comp1, base1 = down(comp2, 1.0), down(base2, 1.0)
    bm = rk.brick_margins(bd_new, comp1, level, colours)
    if bm is not None and bm[0] < F6_MIN:
        bm_old = rk.brick_margins(bd_old, base1, level, colours)
        if bm_old is None or bm_old[0] - bm[0] > F6_DROP:
            fails.append(f"F6 bricks: {bm} (undressed {bm_old})")
    seps = []
    for (view2, places) in orange_views:
        seps += protan_seps(down(view2, 1.0), places)
    p10 = round(float(np.percentile(seps, 10)), 3) if seps else None
    worst_place = None
    if seps:
        flat = [pl for (_v, places) in orange_views for pl in places]
        wp = flat[int(np.argmin(seps))]
        worst_place = [round(v) for v in wp[:2]] if wp[0] != "brick" else ["brick", round(wp[1].get("x", wp[1].get("x1"))),
                                                                              round(wp[1].get("y", wp[1].get("y1")))]
    if p10 is not None and p10 < F9_MIN and p10 < F9_PILOT:
        fails.append(f"F9 protan orange separation p10 {p10} over {len(seps)} places (0.12, or 0.101 the lowest approved pilot)")
    jw = jewels(dressed2)
    fails += [f"F7 {f}" for f in f7_fails(jw)]
    g_med, g_worst, g_n = ghost(down(bd_new, 1.0), level)
    if g_med is not None and g_med > GHOST_MAX:
        fails.append(f"ghost discs: median {g_med} over {g_n} isolated pegs (0.066), worst {g_worst}")
    pr = None
    if undressed2 is not None:
        pr = dress_print(down(dressed2, 1.0), down(undressed2, 1.0), level)
        if not print_ok(pr):
            fails.append(f"dress print round the pegs: median {pr[0]} ({PRINT_MED}), p90 {pr[1]} ({PRINT_P90}), "
                         f"hue median {pr[5]} ({PRINT_HUE_MED}), worst {pr[2]}, over {pr[3]} {pr[4]}")
    rep = {"faces": {s: {k: round(v, 3) for k, v in f.items()} for s, f in faces.items()}, "worst_case": rows,
           "worst_margin": min(r["worst"] for r in rows.values()), "largest_drop": max(r["drop"] for r in rows.values()),
           "bricks_min_margin": bm, "protan_orange_p10": p10, "protan_places": len(seps), "protan_worst_at": worst_place,
           "protan_min": round(min(seps), 3) if seps else None, "jewels": jw,
           "ghost_median": g_med, "ghost_worst": g_worst, "ghost_isolated_pegs": g_n,
           "print": None if pr is None else {"median": pr[0], "p90": pr[1], "worst": pr[2], "samples": pr[3],
                                             "mode": pr[4], "hue_median": pr[5]}}
    return rep, fails


def _syn_coin(img1, level, da, db, dL=0.0):
    """UX round 5's synthetic coin: an OKLab shift, radius r + 18 with a 6-unit feather, round every peg (1x)."""
    from r2lib import oklab_to_srgb
    lab = srgb_to_oklab(img1).copy()
    yy, xx = np.mgrid[0:600, 0:800].astype(np.float32) + 0.5
    m = np.zeros((600, 800), np.float32)
    for d in level["pegs"]:
        r = d.get("r", 10)
        m = np.maximum(m, np.clip((r + 18 + 3 - np.hypot(xx - d["x"], yy - d["y"])) / 6, 0, 1))
    lab[..., 0] += dL * m
    lab[..., 1] += da * m
    lab[..., 2] += db * m
    return np.clip(oklab_to_srgb(lab), 0, 1).astype(np.float32)


def _corner_img():
    """A lapis board (2x) with a rose band in the middle of its upper part and rose only in the wall strips lower down."""
    from r2lib import oklab_to_srgb
    lab = np.zeros((1200, 1600, 3), np.float32)
    lab[..., 0] = 0.35

    def paint(sl, hd):
        a = np.radians(hd)
        lab[sl + (1,)], lab[sl + (2,)] = 0.09 * np.cos(a), 0.09 * np.sin(a)
    paint((slice(None), slice(None)), 255)
    paint((slice(0, 800), slice(650, 1250)), 345)
    paint((slice(900, 1100), slice(150, 250)), 345)
    paint((slice(900, 1100), slice(1350, 1450)), 345)
    return np.clip(oklab_to_srgb(lab), 0, 1)


def selftest(verbose=True):
    """Synthetic boards: a bright ground round the pegs must fail F6 and a dark one pass; one hue, and two hues 32
    degrees apart, must fail F7 and two jewels pass; orange on an orange ground must fail F9 and on lapis pass; a dark
    disc printed round each peg must fail the ghost check and a plain field pass."""
    level = {"pegs": [{"x": 300, "y": 300, "canBeOrange": True}, {"x": 500, "y": 300, "canBeOrange": True}], "bricks": []}
    faces = {"1x": dict(PILOT_FACES), "0.8x": dict(PILOT_FACES)}
    dark = np.full((1200, 1600, 3), 0.08, np.float32)
    bright = np.full((1200, 1600, 3), 0.62, np.float32)
    w_dark = min(v[0] for v in worst_cases(dark, level, faces).values())
    w_bright = min(v[0] for v in worst_cases(bright, level, faces).values())
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

    def orange_on(ground):
        comp = np.empty((600, 800, 3), np.float32)
        comp[:] = ground
        yy, xx = np.mgrid[0:600, 0:800]
        for p in level["pegs"]:
            comp[(xx - p["x"]) ** 2 + (yy - p["y"]) ** 2 < 100] = (0.95, 0.55, 0.20)
        return float(np.percentile(protan_seps(comp, [(p["x"], p["y"], 10) for p in level["pegs"]]), 10))

    def board_with(disc):
        px = np.full((600, 800, 3), 0.16, np.float32) * np.array([0.6, 0.7, 1.4], np.float32)
        yy, xx = np.mgrid[0:600, 0:800]
        if disc:
            for (x, y) in ((250, 250), (420, 330), (560, 240)):
                px[(xx - x) ** 2 + (yy - y) ** 2 < 30 ** 2] = (0.07, 0.07, 0.08)     # a grey disc, as on 2-3
        lvl = {"pegs": [{"x": 250, "y": 250}, {"x": 420, "y": 330}, {"x": 560, "y": 240}], "bricks": []}
        return ghost(px, lvl)[0]

    def printed(kind):
        """A flat rose sky; the dress shifts it evenly, or swings the hue back to blue in a disc round each peg (UX
        G2's synthetic case), or does so on a board whose pegs are all clustered (no isolated peg: critic G1)."""
        lab = np.zeros((600, 800, 3), np.float32)
        lab[..., 0], lab[..., 1], lab[..., 2] = 0.38, 0.06, 0.0
        und = np.clip(oklab_to_srgb(lab), 0, 1)
        dl = lab.copy()
        dl[..., 1] += 0.02
        yy, xx = np.mgrid[0:600, 0:800]
        pts = [(250, 250), (420, 330), (560, 240)] if kind != "cluster" else [
            (300, 300), (330, 300), (360, 300), (300, 330), (330, 330), (360, 330)]
        if kind == "edge":
            pts = [(400, 200), (400, 300), (400, 400)]
            dl[:, :400, 1] += 0.05                      # a region's edge running through the pegs
        elif kind == "ring":                            # a ring 13-19 units from the peg's edge (critic N3)
            for (x, y) in pts:
                rr = np.sqrt((xx - x) ** 2 + (yy - y) ** 2) - 9
                m = (rr >= 13) & (rr <= 19)
                dl[m, 1], dl[m, 2] = 0.03, -0.04
        elif kind == "wide":                            # a crisp disc 40 units out (critic N3, UX m4)
            for (x, y) in pts:
                m = (xx - x) ** 2 + (yy - y) ** 2 < 49 ** 2
                dl[m, 1], dl[m, 2] = 0.03, -0.04
        elif kind != "even":
            for (x, y) in pts:
                m = (xx - x) ** 2 + (yy - y) ** 2 < 24 ** 2
                dl[m, 1], dl[m, 2] = 0.03, -0.04
        lvl = {"pegs": [{"x": x, "y": y, "r": 9} for (x, y) in pts], "bricks": []}
        return print_ok(dress_print(np.clip(oklab_to_srgb(dl), 0, 1), und, lvl))
    cases = [("F6: pegs on a dark ground", w_dark >= F6_MIN, True), ("F6: pegs on a bright ground", w_bright >= F6_MIN, False),
             ("F7: one hue", not f7_fails(jewels(hue_img([255]))), False),
             ("F7: two hues 32 degrees apart (critic P9)", not f7_fails(jewels(hue_img([269, 301, 301]))), False),
             ("F7: two jewels 90 degrees apart", not f7_fails(jewels(hue_img([255, 345, 255]))), True),
             ("F7: a second jewel on the middle third measures its share of the measured window (533 of 1300 px)",
              abs(jewels(hue_img([255, 345, 255]))["second_share"] - 533 / 1300) < 0.02, True),
             ("F7: a second jewel in the middle above, and only at the walls in one lower window (UX m9)",
              not [f for f in f7_fails(jewels(_corner_img())) if "window" in f], False),
             ("F7: the second jewel only along the walls (game designer G18)",
              not [f for f in f7_fails(jewels(hue_img([345] * 5 + [255] * 22 + [345] * 5))) if "walls" in f], False),
             ("F9: orange on an orange ground", orange_on((0.55, 0.30, 0.12)) >= F9_PILOT, False),
             ("F9: orange on lapis", orange_on((0.10, 0.16, 0.42)) >= F9_MIN, True),
             ("ghost: a dark disc printed round each peg", (board_with(True) or 0) <= GHOST_MAX, False),
             ("ghost: a plain field", (board_with(False) or 0) <= GHOST_MAX, True),
             ("print: a hue disc round each peg on a flat rose sky (UX G2)", printed("disc"), False),
             ("print: the same discs on a board with no isolated peg (critic G1)", printed("cluster"), False),
             ("print: an even dress over the whole sky", printed("even"), True),
             ("print: a region's edge running through the pegs", printed("edge"), True),
             ("print: a ring 13-19 units out (critic N3)", printed("ring"), False),
             ("print: a crisp disc 40 units out (critic N3)", printed("wide"), False)]
    # the six approved pilots, dressed (rich2) against undressed (rich): known-good
    import json as _json
    from .paths import RICH
    v9 = RICH.parent
    for pid, stem in (("base-p1", "base-p1-airship-road"), ("base-p2", "base-p2-holy-see"), ("base-p3", "base-p3-moogle"),
                      ("exp-p1", "exp-p1-sharlayan"), ("exp-p2", "exp-p2-lantern-ferry"),
                      ("exp-p3", "exp-p3-mare-lamentorum")):
        lv = _json.loads((v9 / "rich" / "levels" / f"{pid}.json").read_text(encoding="utf-8"))
        import rich_lib as _RL
        pr = dress_print(_RL.load_rgb(v9 / "rich2" / "scenes" / f"{stem}.png"),
                         _RL.load_rgb(v9 / "rich" / "scenes" / f"{stem}.png"), lv)
        cases.append((f"print: the approved pilot {pid} (median {pr[0]}, p90 {pr[1]}, hue {pr[5]})", print_ok(pr), True))
    # real boards, known-bad: round 2's per-peg coin put back (quiet [22, 12], no blur: critic round 4 M1, UX m5), and
    # UX round 5's syn05 hue coin (a/b +0.035 each, radius r + 18, round every peg) on the shipped dress (m7). 2-2 is
    # our own painting and always runs; 2-1 and 1-4 are game art and run where the textures have been fetched
    from . import scene as _scene
    from .dress import dress as _dress, lint as _lint
    from .paths import JSON_OUT
    for lid, name in (("base-07", "2-2"), ("base-06", "2-1"), ("base-04", "1-4")):
        lv = _json.loads((JSON_OUT / f"{lid}.json").read_text(encoding="utf-8"))
        rec = _scene.load_recipe(lv["scene"])
        src = rec["source"]
        if src["kind"] == "game" and not (_scene.CACHE / _scene.cache_name(src["texture"])).exists():
            if verbose:
                print(f"  --- readcheck: print: {name} with round 2's coin and syn05: not run (game texture not fetched)")
            continue
        coin = _json.loads(_json.dumps(rec))
        coin["dress"]["jewel"]["quiet"], coin["dress"]["jewel"]["quietBlur"] = [22, 12], 0
        und = _scene.graded(rec, 1)
        dd, _c = _dress(coin, und, lv, 1, unpinned=True)
        pr = dress_print(np.clip(dd, 0, 1).astype(np.float32), und, lv)
        cases.append((f"print: {name} with round 2's per-peg coin (median {pr[0]}, p90 {pr[1]}, hue {pr[5]})",
                      print_ok(pr), False))
        if lid in ("base-07", "base-04"):
            shipped = np.clip(_dress(rec, und, lv, 1)[0], 0, 1).astype(np.float32)
            pr = dress_print(_syn_coin(shipped, lv, 0.0354, 0.0354), und, lv)
            cases.append((f"print: {name} with UX's syn05 hue coin (median {pr[0]}, p90 {pr[1]}, hue {pr[5]})",
                          print_ok(pr), False))
    # the dress's structural guard (dress.lint, round 5): known-bad recipes on real levels, and the ten as shipped
    lv7 = _json.loads((JSON_OUT / "base-07.json").read_text(encoding="utf-8"))
    lv8 = _json.loads((JSON_OUT / "base-08.json").read_text(encoding="utf-8"))
    r7, r8 = _scene.load_recipe(lv7["scene"]), _scene.load_recipe(lv8["scene"])

    def with_(rec, fn):
        r = _json.loads(_json.dumps(rec))
        fn(r["dress"]["jewel"], r)
        return r
    bad = [("2-2 asking for less quiet blur than 40 (critic round 4 M1)", lv7,
            with_(r7, lambda j, r: j.update(quiet=[22, 12], quietBlur=0))),
           ("2-2 with a NaN quietBlur (critic N13)", lv7, with_(r7, lambda j, r: j.update(quietBlur=float("nan")))),
           ("2-2 with a `dist` term in a region (critic N13)", lv7,
            with_(r7, lambda j, r: j["regions"][0]["mask"].append(["dist", 12, 22]))),
           ("2-2 with a `dist` keep mask (critic N13)", lv7, with_(r7, lambda j, r: j.update(keepMask=[["dist", 22, 12]]))),
           ("2-2 with regionQuiet 1.5 (critic N18)", lv7, with_(r7, lambda j, r: j.update(regionQuiet=1.5))),
           ("2-3 with round 5's disc on the lead bird (critic M2, UX G4)", lv8,
            with_(r8, lambda j, r: j["regions"][0]["mask"].append(["disc", 512, 118, 34, -12]))),
           ("2-3 with a keep disc on a peg (UX m6's wrong fix)", lv8,
            with_(r8, lambda j, r: j.update(keepMasks=[[["disc", 512, 118, 14, 4]]]))),
           ("2-3 with a tone disc on a peg (critic N14)", lv8,
            with_(r8, lambda j, r: r.setdefault("tone", []).append({"mask": [["disc", 331, 418, 30, 10]], "mul": 0.85}))),
           # round 6's bypasses of the centre rule (critic N20, UX m8, game designer G23), all at the lead bird (512, 118)
           ("2-3 with round 5's disc nudged 20 units off the bird (UX m8, GD G23)", lv8,
            with_(r8, lambda j, r: j["regions"][0]["mask"].append(["disc", 515.5, 137.7, 34, -12]))),
           ("2-3 with a not-poly octagon round the bird (critic N20)", lv8,
            with_(r8, lambda j, r: (r["features"].update(octo=[[512 + 34 * math.cos(math.radians(45 * k)),
                                                                 118 + 34 * math.sin(math.radians(45 * k))] for k in range(8)]),
                                    j["regions"][0]["mask"].append(["not-poly", "octo", 24])))),
           ("2-3 with a keep box round the bird (critic N20)", lv8,
            with_(r8, lambda j, r: j.update(keepMasks=[[["x", 482, 494], ["x", 542, 530], ["y", 88, 100], ["y", 148, 136]]]))),
           ("2-3 with a one-point `near` keep mask on the bird (critic N20, UX m8)", lv8,
            with_(r8, lambda j, r: (r["features"].update(spot={"points": [[512, 118], [512.5, 118]]}),
                                    j.update(keepMasks=[[["near", "spot", 22, 46]]])))),
           ("2-3 with a small glow on the bird (critic N20, UX m8)", lv8,
            with_(r8, lambda j, r: r["dress"].setdefault("glows", []).append({"x": 512, "y": 118, "r": 22, "k": 0.22}))),
           ("2-3 with a tone box on the bird (critic N20)", lv8,
            with_(r8, lambda j, r: r.setdefault("tone", []).append(
                {"mask": [["x", 482, 494], ["x", 542, 530], ["y", 88, 100], ["y", 148, 136]], "mul": 0.8}))),
           ("2-3 with a lens of four large discs under the bird (critic N20)", lv8,
            with_(r8, lambda j, r: j["regions"][0]["mask"].extend(
                [["disc", 607, 118, 100, 4], ["disc", 417, 118, 100, 4], ["disc", 512, 213, 100, 4], ["disc", 512, 23, 100, 4]])))]
    for name, lv, rec in bad:
        cases.append((f"lint: {name}", not _lint(rec, lv), False))
    for i in range(1, 11):
        lv = _json.loads((JSON_OUT / f"base-{i:02d}.json").read_text(encoding="utf-8"))
        probs = _lint(_scene.load_recipe(lv["scene"]), lv)
        cases.append((f"lint: base-{i:02d} as shipped{(' ' + probs[0][:80]) if probs else ''}", not probs, True))
    ok = True
    for name, passed, want in cases:
        ok &= passed == want
        if verbose:
            print(f"  {'ok ' if passed == want else 'BAD'} readcheck: {name}: passes={passed}, expected {want}")
    return ok
