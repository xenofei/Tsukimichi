"""Select clean free-flight arcs from pegfit.py output and summarise gravity.

Clean arc: duration >= --dur, RMS residual <= --rms, |horizontal acceleration|
<= --ax (rolling/sliding contact shows up as horizontal acceleration), and not
inside an excluded window (Fever slow motion: give "start:end" pairs in s).

Usage:
  uv run --with numpy python peggravity.py ARCS.csv [--exclude 205.4:218 365.7:380] [--dur 0.4 --rms 1.0 --ax 50]
"""
import argparse
import csv

import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument("arcs")
ap.add_argument("--exclude", nargs="*", default=[])
ap.add_argument("--dur", type=float, default=0.4)
ap.add_argument("--rms", type=float, default=1.0)
ap.add_argument("--ax", type=float, default=50.0)
ap.add_argument("--list", action="store_true")
a = ap.parse_args()
ex = [tuple(map(float, e.split(":"))) for e in a.exclude]
rows = list(csv.DictReader(open(a.arcs)))
sel = []
for r in rows:
    t0, dur = float(r["t0"]), float(r["dur"])
    if dur < a.dur or float(r["rms"]) > a.rms or abs(float(r["ax"])) > a.ax:
        continue
    if any(s <= t0 <= e for s, e in ex):
        continue
    sel.append(r)
g = np.array([float(r["g"]) for r in sel])
se = np.array([float(r["g_se"]) for r in sel])
dur = np.array([float(r["dur"]) for r in sel])
print(f"{len(rows)} arcs, {len(sel)} clean (dur>={a.dur}s, rms<={a.rms}px, |ax|<={a.ax})")
if a.list:
    for r in sel:
        print(f"  t={float(r['t0']):8.2f} n={r['n']:>3} dur={float(r['dur']):.2f} g={float(r['g']):7.1f}+-{float(r['g_se']):5.1f} "
              f"ax={float(r['ax']):6.1f} vx={float(r['vx']):6.0f} vy0={float(r['vy0']):6.0f} phi={r['phi']} rms={r['rms']}")
if len(g):
    q1, med, q3 = np.percentile(g, [25, 50, 75])
    w = 1 / np.maximum(se, 1.0) ** 2
    wm = np.sum(w * g) / np.sum(w)
    # outlier-robust: arcs within 3 robust sigma of the median
    mad = 1.4826 * np.median(np.abs(g - med))
    ok = np.abs(g - med) <= 3 * max(mad, 1e-6)
    wm2 = np.sum(w[ok] * g[ok]) / np.sum(w[ok])
    sd = np.std(g[ok], ddof=1) if ok.sum() > 1 else float("nan")
    print(f"g: median {med:.1f}  IQR {q1:.1f}-{q3:.1f}  MAD-sigma {mad:.1f}")
    print(f"   weighted mean (all) {wm:.1f};  after 3-sigma cut N={ok.sum()}: weighted mean {wm2:.1f}, "
          f"unweighted mean {g[ok].mean():.1f} +- {sd / np.sqrt(ok.sum()):.1f} (SE), SD {sd:.1f}")
    phis = [float(r["phi"]) for r in sel]
    print(f"   tick phase phi values: {sorted(set(phis))} (counts {[phis.count(p) for p in sorted(set(phis))]})")
