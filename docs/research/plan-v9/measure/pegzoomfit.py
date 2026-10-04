"""Summarise a pegzoom.py CSV: zoom-in start/end, hold level, zoom-out start/end, rates.

Frames whose transform has fewer than --minin RANSAC inliers (fireworks, banner)
are ignored.  Rates come from straight-line fits to the ramps.

Usage: uv run --with numpy python pegzoomfit.py ZOOM.csv [ZOOM.csv ...] [--minin 15]
"""
import argparse
import csv

import numpy as np

ap = argparse.ArgumentParser(); ap.add_argument("files", nargs="+"); ap.add_argument("--minin", type=int, default=15)
a = ap.parse_args()
for fn in a.files:
    t, z = [], []
    for r in csv.DictReader(open(fn)):
        if r["zoom"] in ("", "nan") or int(r["inliers"]) < a.minin:
            continue
        zz = float(r["zoom"])
        if not (0.9 < zz < 2.3):
            continue
        t.append(float(r["t"])); z.append(zz)
    t, z = np.array(t), np.array(z)
    up = np.flatnonzero(z > 1.01)
    if len(up) == 0:
        print(f"{fn}: no zoom"); continue
    t_in0 = t[up[0]]
    zmax = np.percentile(z, 98)
    full = np.flatnonzero(z >= zmax - 0.03)
    t_in1 = t[full[0]]
    ramp = (t >= t_in0) & (t <= t_in1)
    rin = np.polyfit(t[ramp], z[ramp], 1)[0] if ramp.sum() > 3 else float("nan")
    t_hold_end = t[full[-1]]
    low = (z <= 1.01) & (t > t_hold_end)
    # require 10 consecutive normal-view frames (single mis-registered frames happen under fireworks)
    run = np.convolve(low.astype(int), np.ones(10, int), "full")[9:]
    back = np.flatnonzero(run == 10)
    t_out1 = t[back[0]] if len(back) else float("nan")
    down = (t > t_hold_end) & (t < t_out1) & (z < zmax - 0.1) & (z > 1.05)
    if down.sum() > 3:
        td, zd = t[down], z[down]
        keep = np.ones(len(td), bool)
        for _ in range(10):  # drop mis-registered frames (residual > 0.04)
            p = np.polyfit(td[keep], zd[keep], 1)
            r = np.abs(zd - np.polyval(p, td))
            if (r[keep] <= 0.04).all():
                break
            keep &= r <= max(0.04, np.percentile(r[keep], 80))
        rout = p[0]
        t_out0 = (zmax - p[1]) / p[0]   # where the zoom-out line leaves the hold level
    else:
        rout = t_out0 = float("nan")
    print(f"{fn}: zoom-in {t_in0:.3f}->{t_in1:.3f} s ({t_in1 - t_in0:.2f} s, {rin:+.2f}/s) to {zmax:.3f}x; "
          f"hold until ~{t_out0:.2f}; zoom-out back to 1.0 at {t_out1:.3f} ({rout:+.3f}/s, {t_out1 - t_out0:.2f} s)")
