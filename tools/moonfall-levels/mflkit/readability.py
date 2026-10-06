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

Print (round 2, all three supervisors' G1): the dress must not print anything round a peg, as hue or as lightness, that
the painting does not have. On the dressed scene with no pieces and no veil, the dress's own change (OKLab, dressed
minus undressed: the painting's detail cancels) 5-12 units outside each still peg against 20-36 units out, sector by
sector, the median over eight sectors (so an edge that crosses the peg is not a print). Every still peg with enough
samples counts, isolated or not; with fewer than three such pegs the board as a whole is measured (5-12 from any piece
against 30 or more from all), so the check is never vacuous. Rule: the median at most PRINT_MED and the 90th percentile at most PRINT_P90.
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
F9_MIN, F9_PILOT = 0.12, 0.101
GHOST_MAX = 0.066
PRINT_MED, PRINT_P90 = 0.010, 0.026


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
    return {"coloured_px_pct": round(float(col.mean()) * 100, 1), "mean_chroma": round(float(C[col].mean()), 3),
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


def dress_print(dressed1, undressed1, level):
    """The dress's print round the pegs (see the module's docstring): (median, p90, worst (value, at), samples, mode).

    Per still peg, in eight sectors round it: the dress's change (dressed minus undressed, OKLab) 5-12 units outside
    the peg against 20-36 units out (16 or more from every piece). The peg's value is the median over its sectors, so
    an edge of the painting or of a region that crosses the peg moves only the sectors it crosses; a disc printed round
    the peg moves them all."""
    diff = srgb_to_oklab(dressed1) - srgb_to_oklab(undressed1)
    import framecheck as fc
    dist = fc.piece_distance(level)
    yy, xx = np.mgrid[0:600, 0:800]
    inside = (xx > 80) & (xx < 720) & (yy > 45) & (yy < 515)
    vals = []
    for d in level["pegs"]:
        if "move" in d:
            continue
        x, y, r = d["x"], d["y"], d.get("r", 10)
        x0, x1, y0, y1 = int(max(0, x - r - 40)), int(min(800, x + r + 40)), int(max(0, y - r - 40)), int(min(600, y + r + 40))
        X, Y = xx[y0:y1, x0:x1] + 0.5 - x, yy[y0:y1, x0:x1] + 0.5 - y
        rr = np.sqrt(X ** 2 + Y ** 2) - r
        sec = ((np.degrees(np.arctan2(Y, X)) + 360) // 45).astype(int) % 8
        df, ins, ds = diff[y0:y1, x0:x1], inside[y0:y1, x0:x1], dist[y0:y1, x0:x1]
        near_all = (rr >= 5) & (rr <= 12) & ins
        mid_all = (rr >= 20) & (rr <= 36) & (ds >= 16) & ins
        per = []
        for k in range(8):
            n, m = near_all & (sec == k), mid_all & (sec == k)
            if n.sum() >= 4 and m.sum() >= 8:
                per.append(float(np.linalg.norm(df[n].mean(0) - df[m].mean(0))))
        if len(per) >= 4:
            vals.append((float(np.median(per)), (round(x), round(y))))
    if len(vals) >= 3:
        v = np.array([a for a, _ in vals])
        w = max(vals)
        return round(float(np.median(v)), 4), round(float(np.percentile(v, 90)), 4), (round(w[0], 4), w[1]), len(vals), "pegs"
    near, open_ = (dist >= 5) & (dist <= 12) & inside, (dist >= 30) & inside
    if near.sum() < 20 or open_.sum() < 40:
        return 9.0, 9.0, (9.0, None), 0, "no open ground"
    g = round(float(np.linalg.norm(diff[near].mean(0) - diff[open_].mean(0))), 4)
    return g, g, (g, None), 1, "board"


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
        if pr[0] > PRINT_MED or pr[1] > PRINT_P90:
            fails.append(f"dress print round the pegs: median {pr[0]} ({PRINT_MED}), p90 {pr[1]} ({PRINT_P90}), "
                         f"worst {pr[2]}, over {pr[3]} {pr[4]}")
    rep = {"faces": {s: {k: round(v, 3) for k, v in f.items()} for s, f in faces.items()}, "worst_case": rows,
           "worst_margin": min(r["worst"] for r in rows.values()), "largest_drop": max(r["drop"] for r in rows.values()),
           "bricks_min_margin": bm, "protan_orange_p10": p10, "protan_places": len(seps), "protan_worst_at": worst_place,
           "protan_min": round(min(seps), 3) if seps else None, "jewels": jw,
           "ghost_median": g_med, "ghost_worst": g_worst, "ghost_isolated_pegs": g_n,
           "print": None if pr is None else {"median": pr[0], "p90": pr[1], "worst": pr[2], "samples": pr[3],
                                             "mode": pr[4]}}
    return rep, fails


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
        elif kind != "even":
            for (x, y) in pts:
                m = (xx - x) ** 2 + (yy - y) ** 2 < 24 ** 2
                dl[m, 1], dl[m, 2] = 0.03, -0.04
        lvl = {"pegs": [{"x": x, "y": y, "r": 9} for (x, y) in pts], "bricks": []}
        med, p90, _w, _n, _m = dress_print(np.clip(oklab_to_srgb(dl), 0, 1), und, lvl)
        return med <= PRINT_MED and p90 <= PRINT_P90
    cases = [("F6: pegs on a dark ground", w_dark >= F6_MIN, True), ("F6: pegs on a bright ground", w_bright >= F6_MIN, False),
             ("F7: one hue", not f7_fails(jewels(hue_img([255]))), False),
             ("F7: two hues 32 degrees apart (critic P9)", not f7_fails(jewels(hue_img([269, 301, 301]))), False),
             ("F7: two jewels 90 degrees apart", not f7_fails(jewels(hue_img([255, 255, 345]))), True),
             ("F9: orange on an orange ground", orange_on((0.55, 0.30, 0.12)) >= F9_PILOT, False),
             ("F9: orange on lapis", orange_on((0.10, 0.16, 0.42)) >= F9_MIN, True),
             ("ghost: a dark disc printed round each peg", (board_with(True) or 0) <= GHOST_MAX, False),
             ("ghost: a plain field", (board_with(False) or 0) <= GHOST_MAX, True),
             ("print: a hue disc round each peg on a flat rose sky (UX G2)", printed("disc"), False),
             ("print: the same discs on a board with no isolated peg (critic G1)", printed("cluster"), False),
             ("print: an even dress over the whole sky", printed("even"), True),
             ("print: a region's edge running through the pegs", printed("edge"), True)]
    ok = True
    for name, passed, want in cases:
        ok &= passed == want
        if verbose:
            print(f"  {'ok ' if passed == want else 'BAD'} readcheck: {name}: passes={passed}, expected {want}")
    return ok
