"""The fuller-board framing rules (level-method.md section 8: F2, F3a, F3a pixels, F3b, F3c, F3d, F5), for any level.

The measurements are the approved ones (docs/design/v9/rich2/src/framecheck.py, round 3); this module only lifts them off
the six pilots so they take a level dictionary. Its self-test runs the approved synthetic cases plus whole-board cases:
framing on a peg, framing over the open middle, a long rim run and a lamp beside a peg must fail; a clean corner must
pass.
"""
import math

import numpy as np
from PIL import Image

from . import paths  # noqa: F401
import framecheck as fc                      # the approved checks (rich2)

WALL_L, WALL_R, TOP, FOOT = fc.WALL_L, fc.WALL_R, fc.TOP, fc.FOOT
PIVOT = fc.PIVOT


def to1x(a, S):
    return np.asarray(Image.fromarray(a.astype(np.float32), "F").resize((800, 600), Image.BOX)) if S != 1 else a


def check(level, mask, rim, lights=(), S=2, dist=None):
    """mask, rim: the framing's coverage and rim light at S. lights: (x, y, r). Returns (report, fails)."""
    m1, r1 = to1x(mask, S), to1x(rim, S)
    yy, xx = np.mgrid[0:600, 0:800].astype(np.float32)
    X, Y = xx + 0.5, yy + 0.5
    opening = (X > WALL_L) & (X < WALL_R) & (Y > TOP) & (Y < FOOT)
    mid = opening & (X > WALL_L + 120) & (X < WALL_R - 120) & (Y > TOP + 120) & (Y < 470)
    fails, rep = [], {}
    rep["cover_pct"] = round(float((m1 * opening).sum() / opening.sum()) * 100, 2)
    rep["middle_pct"] = round(float((m1 * mid).sum() / mid.sum()) * 100, 3)
    swing = np.sqrt((X - PIVOT[0]) ** 2 + (Y - PIVOT[1]) ** 2) < 100
    rep["in_swing_px"] = int(((m1 > 0.5) & swing & opening).sum())
    if rep["cover_pct"] > 12 or rep["middle_pct"] > 0.2 or rep["in_swing_px"]:
        fails.append("F2")
    d = fc.piece_distance(level) if dist is None else dist
    on = (m1 > 0.5) & opening
    rep["min_clearance"] = round(float(d[on].min()), 2) if on.any() else None
    if on.any() and d[on].min() < 6:
        fails.append("F3a")
        yx = np.unravel_index(np.argmin(np.where(on, d, 1e9)), d.shape)
        rep["clearance_at"] = [int(yx[1]), int(yx[0])]
    comps = [c for c in fc._components((r1 > 0.12) & opening) if len(c[0]) >= 3]
    longest, where = 0.0, None
    for ys, xs in comps:
        ln = math.hypot(xs.max() - xs.min() + 1, ys.max() - ys.min() + 1)
        if ln > longest:
            longest, where = ln, [int(xs.mean()), int(ys.mean())]
    rep["longest_rim_run"] = round(longest, 1)
    rep["longest_rim_at"] = where
    if longest > 30:
        fails.append("F3b")
    st = fc.straight_runs(fc.without_thin(m1))
    rep["straight_run"] = None if st is None else [[round(st[0][0]), round(st[0][1])], round(st[1], 1)]
    if st is not None:
        fails.append("F3c")
    discs = fc.round_shapes(m1, opening)
    rep["peg_sized_discs"] = discs
    if discs:
        fails.append("F3d")
    close = [(round(x), round(y)) for (x, y, *_r) in lights
             if d[min(599, max(0, int(y))), min(799, max(0, int(x)))] - (_r[0] if _r else 0) * 0.6 < 8]
    rep["lights_too_close"] = close
    if close:
        fails.append("F5")
    # F5's "never a row": at most three small lights in any 40-unit band of height, 30 units or more apart
    pts = [(x, y) for (x, y, *_r) in lights]
    crowd = max((sum(1 for (_, b) in pts if y0 <= b < y0 + 40) for y0 in range(0, 600, 5)), default=0)
    near = min((math.hypot(a[0] - c[0], a[1] - c[1]) for i, a in enumerate(pts) for c in pts[i + 1:]), default=99)
    rep["lights_per_band_max"], rep["lights_nearest"] = crowd, round(near, 1)
    if crowd > 3 or near < 30:
        fails.append("F5-row")
    return rep, fails


def darkened_near_pieces(level, before, after, S=2):
    return fc.darkened_near_pieces(level, before, after, S)


def selftest(verbose=True):
    ok = fc.selftest() if verbose else _quiet(fc.selftest)
    lvl = {"pegs": [{"x": 300, "y": 300}, {"x": 500, "y": 300}], "bricks": []}
    S = 1
    yy, xx = np.mgrid[0:600, 0:800].astype(np.float32)
    X, Y = xx + 0.5, yy + 0.5
    corner = ((np.sqrt((X - 75) ** 2 + (Y - 41) ** 2) < 60)).astype(np.float32)
    cases = [
        ("a clean corner shape", check(lvl, corner, np.zeros_like(corner), [], S)[1], []),
        ("framing over a peg", check(lvl, corner + (np.sqrt((X - 300) ** 2 + (Y - 300) ** 2) < 30), np.zeros_like(corner), [], S)[1], ["F2", "F3a"]),
        ("a slab across the middle", check(lvl, ((Y > 395) & (Y < 420) & (X > 100) & (X < 700)).astype(np.float32), np.zeros_like(corner), [], S)[1], ["F2"]),
        ("a rim run of 60 units", check(lvl, corner, ((np.abs(Y - 60) < 1.5) & (X > 90) & (X < 150)).astype(np.float32), [], S)[1], ["F3b"]),
        ("a lamp 4 units from a peg", check(lvl, corner, np.zeros_like(corner), [(314, 300, 2)], S)[1], ["F5"]),
        ("four lamps in a row", check(lvl, corner, np.zeros_like(corner), [(120, 500, 1), (200, 505, 1), (600, 502, 1), (680, 508, 1)], S)[1], ["F5-row"]),
    ]
    for name, got, want in cases:
        good = all(w in got for w in want) and (want or not got)
        ok &= bool(good)
        if verbose:
            print(f"  {'ok ' if good else 'BAD'} framecheck: {name}: fails={got}, expected {want or 'none'}")
    return ok


def _quiet(fn):
    import contextlib
    import io
    with contextlib.redirect_stdout(io.StringIO()):
        return fn()
