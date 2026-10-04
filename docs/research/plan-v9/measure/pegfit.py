"""Find free-flight ball arcs in pegtrack.py candidates and fit gravity.

Model per arc:  x(t) = x0 + vx*t,  y(t) = y0 + vy*t + g*t^2/2  (native px, s).

Game-tick time: Peggle's simulation advances in fixed ticks. The per-frame
displacement of the ball in 60 fps video follows a 2,2,1 tick pattern, i.e.
100 ticks/s.  With --tick 100 each frame time is snapped to the last game tick
before it, tau = floor(t*100 + phi)/100, and the phase phi is fitted per arc.
--tick 0 uses raw frame times.

Usage:
  uv run --with numpy --with scipy python pegfit.py CANDIDATES.csv OUT_ARCS.csv [--tick 100]
         [--minlen 12] [--tol 2.5] [--col g|m]

Writes one row per arc and prints a summary (median, IQR, robust SE of g).
"""
import argparse
import csv
import json
from collections import defaultdict

import numpy as np

YMAX = 565.0


def load(path, col):
    by = defaultdict(list)
    fps_t = {}
    with open(path) as fh:
        for r in csv.DictReader(fh):
            if r["dup"] == "1" or r[col + "x"] == "":
                continue
            f = int(r["frame"])
            fps_t[f] = float(r["t"])
            x, y = float(r[col + "x"]), float(r[col + "y"])
            if y > YMAX:  # bucket strip: the gold bucket is not the ball
                continue
            by[f].append((x, y))
    return by, fps_t


def fit(ts, xs, ys, quad_x=False):
    t0 = ts[0]
    t = np.asarray(ts) - t0
    A2 = np.vstack([np.ones_like(t), t, 0.5 * t * t]).T
    A1 = A2[:, :2]
    Ax = A2 if quad_x else A1
    px, *_ = np.linalg.lstsq(Ax, xs, rcond=None)
    py, *_ = np.linalg.lstsq(A2, ys, rcond=None)
    rx = xs - Ax @ px
    ry = ys - A2 @ py
    return px, py, np.sqrt(rx**2 + ry**2), t0


def tick_times(ts, phi, rate):
    if rate <= 0:
        return np.asarray(ts)
    return np.floor(np.asarray(ts) * rate + phi) / rate


def best_phase(ts, xs, ys, rate):
    if rate <= 0:
        px, py, res, t0 = fit(ts, xs, ys)
        return 0.0, px, py, res
    best = None
    for phi in np.arange(0, 1, 0.02):
        tt = tick_times(ts, phi, rate)
        if len(set(tt)) < len(tt):  # two frames on one tick: allowed only if same position
            pass
        px, py, res, _ = fit(tt, xs, ys)
        s = float(np.sum(res**2))
        if best is None or s < best[0]:
            best = (s, phi, px, py, res)
    return best[1], best[2], best[3], best[4]


def find_arcs(by, ft, tol, minlen, rate):
    frames = sorted(by)
    fset = set(frames)
    used = set()
    arcs = []
    pos = {f: i for i, f in enumerate(frames)}

    def pts_near(f, x, y, r):
        out = []
        for j, (cx, cy) in enumerate(by.get(f, [])):
            if (f, j) in used:
                continue
            d = np.hypot(cx - x, cy - y)
            if d < r:
                out.append((d, j, cx, cy))
        return sorted(out)

    failed = set()
    covered = set()
    for f in frames:
        if f in covered:
            continue
        best = None
        f2, f3 = f + 3, f + 6
        if f2 not in fset or f3 not in fset:
            continue
        for j1, c1 in enumerate(by[f]):
            if (f, j1) in used:
                continue
            for j2, c2 in enumerate(by[f2]):
                if np.hypot(c2[0] - c1[0], c2[1] - c1[1]) < 3:  # stationary
                    continue
                for j3, c3 in enumerate(by[f3]):
                    ts = [ft[f], ft[f2], ft[f3]]
                    px, py, _, _ = fit(ts, np.array([c1[0], c2[0], c3[0]]), np.array([c1[1], c2[1], c3[1]]))
                    g = py[2]
                    if not (-500 < g < 4000):
                        continue
                    if abs(px[1]) + abs(py[1]) < 40:
                        continue
                    # count support in the intermediate frames
                    sup = 0
                    for fi in (f + 1, f + 2, f + 4, f + 5):
                        if fi not in ft:
                            continue
                        t = ft[fi] - ft[f]
                        x = px[0] + px[1] * t
                        y = py[0] + py[1] * t + 0.5 * g * t * t
                        if pts_near(fi, x, y, tol + 2):
                            sup += 1
                    if sup >= 3 and (best is None or sup > best[0]):
                        best = (sup, [(f, j1, c1), (f2, j2, c2), (f3, j3, c3)])
        if best is None:
            continue
        pts = {p[0]: (p[1], p[2][0], p[2][1]) for p in best[1]}
        # fill intermediate and extend both ways
        for direction in (1, -1):
            misses = 0
            fi = f if direction == -1 else f
            fi = min(pts) if direction == -1 else f
            while misses < 3 and abs(fi - f) < 150:
                fi += direction
                if fi in pts:
                    continue
                if fi not in ft:
                    if fi < frames[0] or fi > frames[-1]:
                        break
                    misses += 1
                    continue
                ks = sorted(pts)
                ts = [ft[k] for k in ks]
                xs = np.array([pts[k][1] for k in ks])
                ys = np.array([pts[k][2] for k in ks])
                # local fit on the nearest 12 points
                if direction == 1:
                    sel = slice(max(0, len(ks) - 12), len(ks))
                else:
                    sel = slice(0, min(12, len(ks)))
                px, py, _, t0 = fit(ts[sel], xs[sel], ys[sel])
                t = ft[fi] - t0
                x = px[0] + px[1] * t
                y = py[0] + py[1] * t + 0.5 * py[2] * t * t
                near = pts_near(fi, x, y, tol + 2)
                if near:
                    d, j, cx, cy = near[0]
                    pts[fi] = (j, cx, cy)
                    misses = 0
                else:
                    misses += 1
        ks = sorted(pts)
        if len(ks) < minlen:
            continue
        # trim ends where a single-parabola fit breaks (bounces)
        ts = np.array([ft[k] for k in ks])
        xs = np.array([pts[k][1] for k in ks])
        ys = np.array([pts[k][2] for k in ks])
        keep = np.ones(len(ks), bool)
        # trim with raw frame times (fast), then fit the tick phase once
        for _ in range(len(ks)):
            if keep.sum() < minlen:
                break
            _, _, res, _ = fit(ts[keep], xs[keep], ys[keep])
            worst = np.argmax(res)
            if res[worst] <= tol + 1.0:
                break
            idx = np.flatnonzero(keep)
            keep[idx[worst]] = False
        if keep.sum() < minlen:
            failed.add(f)
            continue
        phi, px, py, res = best_phase(ts[keep], xs[keep], ys[keep], rate)
        for k in np.array(ks)[keep]:
            used.add((k, pts[k][0]))
        covered.update(range(int(np.array(ks)[keep][0]), int(np.array(ks)[keep][-1]) + 1))
        tt = tick_times(ts[keep], phi, rate)
        px, py, res, t0 = fit(tt, xs[keep], ys[keep])
        # parameter standard errors
        t = tt - t0
        A = np.vstack([np.ones_like(t), t, 0.5 * t * t]).T
        dof = max(1, len(t) - 3)
        s2 = float(np.sum((ys[keep] - A @ py) ** 2)) / dof
        cov = s2 * np.linalg.inv(A.T @ A)
        g_se = float(np.sqrt(cov[2, 2]))
        # quadratic-in-x check: horizontal acceleration (drag)
        pxq, _, _, _ = fit(tt, xs[keep], ys[keep], quad_x=True)
        fr = np.array(ks)[keep]
        arcs.append(dict(f0=int(fr[0]), f1=int(fr[-1]), t0=round(float(ft[fr[0]]), 4), n=int(keep.sum()),
                         dur=round(float(tt[-1] - tt[0]), 4), g=round(float(py[2]), 2), g_se=round(g_se, 2),
                         ax=round(float(pxq[2]), 2), vx=round(float(px[1]), 2), vy0=round(float(py[1]), 2),
                         x0=round(float(px[0]), 2), y0=round(float(py[0]), 2), phi=round(float(phi), 2),
                         rms=round(float(np.sqrt(np.mean(res**2))), 3),
                         pts=json.dumps([[int(k), round(float(x), 2), round(float(y), 2)] for k, x, y in zip(fr, xs[keep], ys[keep])])))
    return arcs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cands")
    ap.add_argument("out")
    ap.add_argument("--tick", type=float, default=100.0)
    ap.add_argument("--minlen", type=int, default=12)
    ap.add_argument("--tol", type=float, default=2.5)
    ap.add_argument("--col", default="g", choices=["g", "m"])
    ap.add_argument("--gmin", type=float, default=50.0, help="keep arcs with g above this (rejects rolling/sliding)")
    a = ap.parse_args()
    by, ft = load(a.cands, a.col)
    arcs = find_arcs(by, ft, a.tol, a.minlen, a.tick)
    with open(a.out, "w", newline="") as fh:
        if arcs:
            w = csv.DictWriter(fh, fieldnames=list(arcs[0].keys()))
            w.writeheader()
            w.writerows(arcs)
    good = [r for r in arcs if r["g"] > a.gmin and r["dur"] >= 0.2]
    print(f"{len(arcs)} arcs, {len(good)} with g>{a.gmin} and dur>=0.2 s")
    for r in arcs:
        print(f"  f{r['f0']}-{r['f1']} t={r['t0']:.2f} n={r['n']} dur={r['dur']:.3f} g={r['g']:.1f}+-{r['g_se']:.1f} ax={r['ax']:.1f} "
              f"v=({r['vx']:.0f},{r['vy0']:.0f}) phi={r['phi']} rms={r['rms']}")
    if good:
        g = np.array([r["g"] for r in good])
        w = np.array([1 / max(r["g_se"], 1) ** 2 for r in good])
        q1, med, q3 = np.percentile(g, [25, 50, 75])
        print(f"g median {med:.1f}  IQR {q1:.1f}-{q3:.1f}  weighted mean {np.sum(w * g) / np.sum(w):.1f}  "
              f"robust SE {1.253 * (q3 - q1) / 1.349 / np.sqrt(len(g)):.1f}  N={len(g)}")


if __name__ == "__main__":
    main()
