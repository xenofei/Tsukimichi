"""The fuller-board checks (level-method.md section 8), measured on the output, at 1x (board units). dress2.py runs
them on every board and refuses to write a scene that fails.

  F2   framing stays in its regions: at most 12% of the opening, under 0.2% of the open middle, none in the
       launcher's swing (within 100 units of the pivot)
  F3a  clearance: no framing pixel (mask > 0.5) within 6 units of any piece's edge (pegs, bricks, every mover's whole
       path)
  F3b  no rim-lit run longer than 30 units: every connected piece of rim light (> 0.12) fits in a 30-unit diagonal
  F3c  no straight edge: no run of framing outline that stays within 0.75 units of a straight line for 36 units or
       more, further than 15 units from a wall or the top
  F3d  no peg-sized disc: no closed round framing shape (or hole) between 5 and 13 units in radius inside the opening
  F5   no small light within 8 units of a piece's edge (dress2.points, and the motion's whole paths)
"""
import math

import numpy as np

import r2lib  # noqa: F401  (puts the rich pass kit on the path)
from board import load_level, pieces, mover_pos, sd_arc_capsule, sd_line_capsule

WALL_L, WALL_R, TOP, FOOT = 75.0, 725.0, 41.0, 594.0
PIVOT = (400.0, 87.0)


def piece_distance(level, S=1.0):
    """Distance (units) from each pixel to the nearest piece's edge, every mover sampled along its whole path."""
    h, w = int(600 * S), int(800 * S)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    d = np.full((h, w), 1e9, np.float32)
    for kind, p in pieces(level):
        if kind == "brick":
            ht = p.get("thickness", 20) / 2
            if p["kind"] == "line":
                d = np.minimum(d, sd_line_capsule(X, Y, p["x1"], p["y1"], p["x2"], p["y2"], ht))
            else:
                d = np.minimum(d, sd_arc_capsule(X, Y, p["x"], p["y"], p["r"], p["start"], p["sweep"], ht))
        else:
            r = p.get("r", 10)
            pts = [mover_pos(p, p["move"]["period"] * k / 48) for k in range(48)] if p.get("move") else [(p["x"], p["y"])]
            for (mx, my) in pts:
                d = np.minimum(d, np.sqrt((X - mx) ** 2 + (Y - my) ** 2) - r)
    return d


def peg_list(level):
    out = []
    for kind, p in pieces(level):
        if kind == "peg":
            pts = [mover_pos(p, p["move"]["period"] * k / 48) for k in range(48)] if p.get("move") else [(p["x"], p["y"])]
            out += [(x, y, p.get("r", 10)) for (x, y) in pts]
    return out


def _components(mask):
    """Connected components (4-neighbour) of a boolean mask: list of (ys, xs) arrays."""
    h, w = mask.shape
    seen = np.zeros_like(mask)
    comps = []
    for y0, x0 in zip(*np.nonzero(mask)):
        if seen[y0, x0]:
            continue
        st = [(y0, x0)]
        seen[y0, x0] = True
        ys, xs = [], []
        while st:
            y, x = st.pop()
            ys.append(y)
            xs.append(x)
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    st.append((ny, nx))
        comps.append((np.array(ys), np.array(xs)))
    return comps


def straight_runs(mask, inner=15.0, span=36.0, tol=0.75):
    """F3c (round 3, after the level critic's round 2: the round-2 version could never fire). Outline pixels of the
    framing (at 1x) are split by the side they face (left, right, up, down), so the two edges of a thin post are never
    fitted together; in each class, every third point's neighbourhood of radius span/2 + 2 is fitted with a line, and
    a run is straight when its points span at least 0.95 x span and 95% of them lie within `tol` of the line, more than
    `inner` units inside the opening. Returns the longest run's centre and length, or None."""
    m = mask > 0.5
    pad = np.pad(m, 1)
    classes = {"left": m & ~pad[1:-1, :-2], "right": m & ~pad[1:-1, 2:], "up": m & ~pad[:-2, 1:-1],
               "down": m & ~pad[2:, 1:-1]}
    R = span / 2 + 2
    worst = None
    for edge in classes.values():
        ys, xs = np.nonzero(edge)
        X, Y = xs + 0.5, ys + 0.5
        keep = (X > WALL_L + inner) & (X < WALL_R - inner) & (Y > TOP + inner) & (Y < FOOT - inner)
        X, Y = X[keep], Y[keep]
        for i in range(0, len(X), 3):
            d2 = (X - X[i]) ** 2 + (Y - Y[i]) ** 2
            nb = d2 < R * R
            if nb.sum() < span * 0.8:
                continue
            P = np.stack([X[nb] - X[nb].mean(), Y[nb] - Y[nb].mean()], 1)
            _u, _sv, vt = np.linalg.svd(P, full_matrices=False)
            along = P @ vt[0]
            resid = np.abs(P @ vt[1])
            # the run: the points within tol of the line, and their extent along it (gaps over 2 units break it)
            on = np.sort(along[resid < tol])
            if len(on) < 2:
                continue
            best, start = 0.0, on[0]
            for k in range(1, len(on)):
                if on[k] - on[k - 1] > 2.0:
                    start = on[k]
                best = max(best, on[k] - start)
            if best >= span * 0.95 and (resid < tol).mean() >= 0.95:
                if worst is None or best > worst[1]:
                    worst = ((float(X[i]), float(Y[i])), float(best))
    return worst


def without_thin(mask, width=5):
    """The framing less its strokes 4 units wide or narrower (a morphological opening by a 5 x 5 px square at 1x):
    F3c exempts thin unlit strokes such as ropes (round 3, the level critic's R1 question), since they cannot read as a
    wall and catch no rim light."""
    m = mask > 0.5
    h, w = m.shape
    er = np.ones_like(m)
    for dy in range(width):
        for dx in range(width):
            er &= np.pad(m, ((0, width), (0, width)))[dy:dy + h, dx:dx + w]
    op = np.zeros_like(m)
    for dy in range(width):
        for dx in range(width):
            op |= np.pad(er, ((width, 0), (width, 0)))[width - dy:width - dy + h, width - dx:width - dx + w]
    return op.astype(np.float32)


def round_shapes(m1, opening):
    """F3d: peg-sized discs and holes (10-26 units across, near square, filling a disc's area): components of the
    framing, and the framing's holes (components of the background that do not reach the opening's open space)."""
    found = []
    targets = [(m1 > 0.5) & opening]
    bg = ~(m1 > 0.5)
    for ys, xs in _components(bg):
        if len(ys) > 2500:
            continue                                         # the open board (or a large clearing), not a hole
        hole = np.zeros_like(bg)
        hole[ys, xs] = True
        targets.append(hole & opening)
    for t in targets:
        for ys, xs in _components(t):
            wbox, hbox = xs.max() - xs.min() + 1, ys.max() - ys.min() + 1
            if 10 <= wbox <= 26 and 10 <= hbox <= 26 and abs(wbox - hbox) <= 3:
                fill = len(ys) / (math.pi * (wbox / 2) * (hbox / 2))
                if 0.85 <= fill <= 1.12:
                    found.append([int(xs.mean()), int(ys.mean())])
    return found


def darkened_near_pieces(level, before, after, S=2):
    """F3a's pixel backstop (round 3, critic R5): wherever the dressed scene is 0.06 or more darker (OKLab L) than the
    graded scene, there is framing; none of it may lie within 6 units of a piece, whatever drew it."""
    from r2lib import srgb_to_oklab
    from PIL import Image
    drop = srgb_to_oklab(before)[..., 0] - srgb_to_oklab(after)[..., 0]
    d1 = np.asarray(Image.fromarray(drop.astype(np.float32), "F").resize((800, 600), Image.BOX))
    dist = piece_distance(level)
    yy, xx = np.mgrid[0:600, 0:800]
    opening = (xx > WALL_L) & (xx < WALL_R) & (yy > TOP) & (yy < FOOT)
    bad = (d1 >= 0.06) & (dist < 6) & opening
    return float(dist[(d1 >= 0.06) & opening].min()) if ((d1 >= 0.06) & opening).any() else None, int(bad.sum())


def check(level_id, mask, rim, lights=(), S=2):
    """mask, rim: the framing coverage and its rim light at S. lights: (x, y, r) small lights. Returns (report, fails)."""
    level = load_level(__import__("r2lib").RICH / "levels" / f"{level_id}.json")
    from PIL import Image
    m1 = np.asarray(Image.fromarray(mask.astype(np.float32), "F").resize((800, 600), Image.BOX)) if S != 1 else mask
    r1 = np.asarray(Image.fromarray(rim.astype(np.float32), "F").resize((800, 600), Image.BOX)) if S != 1 else rim
    yy, xx = np.mgrid[0:600, 0:800].astype(np.float32)
    X, Y = xx + 0.5, yy + 0.5
    opening = (X > WALL_L) & (X < WALL_R) & (Y > TOP) & (Y < FOOT)
    mid = opening & (X > WALL_L + 120) & (X < WALL_R - 120) & (Y > TOP + 120) & (Y < 470)
    fails = []
    rep = {}
    rep["cover_pct"] = round(float((m1 * opening).sum() / opening.sum()) * 100, 2)
    rep["middle_pct"] = round(float((m1 * mid).sum() / mid.sum()) * 100, 3)
    swing = np.sqrt((X - PIVOT[0]) ** 2 + (Y - PIVOT[1]) ** 2) < 100
    rep["in_swing_px"] = int(((m1 > 0.5) & swing & opening).sum())
    if rep["cover_pct"] > 12 or rep["middle_pct"] > 0.2 or rep["in_swing_px"]:
        fails.append("F2")
    d = piece_distance(level)
    on = (m1 > 0.5) & opening
    rep["min_clearance"] = round(float(d[on].min()), 2) if on.any() else None
    if on.any() and d[on].min() < 6:
        fails.append("F3a")
        yx = np.unravel_index(np.argmin(np.where(on, d, 1e9)), d.shape)
        rep["clearance_at"] = [int(yx[1]), int(yx[0])]
    comps = [c for c in _components((r1 > 0.12) & opening) if len(c[0]) >= 3]
    longest, where = 0.0, None
    for ys, xs in comps:
        ln = math.hypot(xs.max() - xs.min() + 1, ys.max() - ys.min() + 1)
        if ln > longest:
            longest, where = ln, [int(xs.mean()), int(ys.mean())]
    rep["longest_rim_run"] = round(longest, 1)
    rep["longest_rim_at"] = where
    if longest > 30:
        fails.append("F3b")
    st = straight_runs(without_thin(m1))
    rep["straight_run"] = None if st is None else [[round(st[0][0]), round(st[0][1])], round(st[1], 1)]
    if st is not None:
        fails.append("F3c")
    discs = round_shapes(m1, opening)
    rep["peg_sized_discs"] = discs
    if discs:
        fails.append("F3d")
    close = [(x, y) for (x, y, *_r) in lights if d[min(599, max(0, int(y))), min(799, max(0, int(x)))] < 8]
    rep["lights_too_close"] = close
    if close:
        fails.append("F5")
    return rep, fails


def selftest():
    """Synthetic shapes that must fail and must pass (round 3: the checker is tested, critic R1 and R3)."""
    yy, xx = np.mgrid[0:600, 0:800].astype(np.float32)
    X, Y = xx + 0.5, yy + 0.5
    opening = (X > WALL_L) & (X < WALL_R) & (Y > TOP) & (Y < FOOT)
    out = []
    for w in (4, 8, 11, 20, 30, 40, 60):
        post = ((np.abs(X - 300) < w / 2) & (Y > 200) & (Y < 300)).astype(np.float32)
        out.append((f"post {w} wide", straight_runs(post) is not None, True))
    slab = ((np.abs((X - 300) - (Y - 300)) < 10) & (np.abs((X - 300) + (Y - 300)) < 60)).astype(np.float32)
    out.append(("slab at 45 deg", straight_runs(slab) is not None, True))
    disc = (np.sqrt((X - 300) ** 2 + (Y - 300) ** 2) < 9).astype(np.float32)
    out.append(("disc r 9", bool(round_shapes(disc, opening)), True))
    hole = (((np.abs(X - 300) < 40) & (np.abs(Y - 300) < 40)) & ~(np.sqrt((X - 300) ** 2 + (Y - 300) ** 2) < 9)).astype(np.float32)
    out.append(("hole r 9 in a slab", bool(round_shapes(hole, opening)), True))
    wavy = (np.abs(X - 300 - 6 * np.sin(Y / 7.0)) < 5) & (Y > 200) & (Y < 300)
    out.append(("wavy stem (amplitude 6, period 44)", straight_runs(wavy.astype(np.float32)) is not None, False))
    rope = (np.abs(X - 300) < 1.6) & (Y > 200) & (Y < 300)
    out.append(("straight rope 3.2 wide (exempt)", straight_runs(without_thin(rope.astype(np.float32))) is not None, False))
    post4 = ((np.abs(X - 300) < 2) & (Y > 200) & (Y < 300)).astype(np.float32)
    out.append(("post 4 wide, after the thin exemption (exempt)", straight_runs(without_thin(post4)) is not None, False))
    post6 = ((np.abs(X - 300) < 3) & (Y > 200) & (Y < 300)).astype(np.float32)
    out.append(("post 6 wide, after the thin exemption", straight_runs(without_thin(post6)) is not None, True))
    blob = (np.sqrt(((X - 300) / 30) ** 2 + ((Y - 300) / 22) ** 2) < 1).astype(np.float32)
    out.append(("ellipse 60 x 44", straight_runs(blob) is not None or bool(round_shapes(blob, opening)), False))
    ok = True
    for name, got, want in out:
        print(f"  {'ok ' if got == want else 'BAD'} {name}: flagged={got}, expected {want}")
        ok &= got == want
    return ok


if __name__ == "__main__":
    raise SystemExit(0 if selftest() else 1)
