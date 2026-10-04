"""Launch speed and launch angle from the first free-flight arc of each shot.

An arc counts as a launch arc when its first point lies within --rmax native px
of the launcher pivot.  Its fitted parabola is extrapolated backwards to the
moment the ball centre was --nozzle px from the pivot (where the ball leaves the
barrel); the speed and direction there are the launch speed and aim angle
(0 deg = straight down, positive = to the player's right).

The pivot and nozzle radius were read from still frames (see peg-measurements.md):
pivot ~ (400, 87), nozzle ~ 73 px.  The result is insensitive to the exact values
because speed changes slowly near the top of the arc (dv = g*dt).

Usage: uv run --with numpy python peglaunch.py ARCS.csv [--pivot 400 87] [--nozzle 73] [--rmax 140]
"""
import argparse
import csv
import json

import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument("arcs")
ap.add_argument("--pivot", type=float, nargs=2, default=(400.0, 87.0))
ap.add_argument("--nozzle", type=float, default=73.0)
ap.add_argument("--rmax", type=float, default=140.0)
ap.add_argument("--rms", type=float, default=1.2)
ap.add_argument("--minn", type=int, default=12)
ap.add_argument("--g", type=float, default=500.0, help="fix gravity to this value for the extrapolation")
a = ap.parse_args()
P = np.array(a.pivot)
out = []
for r in csv.DictReader(open(a.arcs)):
    pts = json.loads(r["pts"])
    if int(r["n"]) < a.minn or float(r["rms"]) > a.rms:
        continue
    f, x, y = np.array(pts).T
    if np.hypot(x[0] - P[0], y[0] - P[1]) > a.rmax or y[0] < P[1]:
        continue
    t = (f - f[0]) / 60.0
    # fit with gravity fixed: y - g t^2/2 = y0 + vy t ; x = x0 + vx t
    A = np.vstack([np.ones_like(t), t]).T
    (x0, vx), *_ = np.linalg.lstsq(A, x, rcond=None)
    (y0, vy), *_ = np.linalg.lstsq(A, y - 0.5 * a.g * t * t, rcond=None)
    res = np.hypot(x - (x0 + vx * t), y - (y0 + vy * t + 0.5 * a.g * t * t))
    if res.max() > 3.0:
        continue
    # go back in time until the ball is at the nozzle radius
    ts = np.linspace(-0.6, 0, 6001)
    d = np.hypot(x0 + vx * ts - P[0], y0 + vy * ts + 0.5 * a.g * ts * ts - P[1])
    inside = np.flatnonzero(d <= a.nozzle)
    if len(inside) == 0:
        continue
    tl = ts[inside[-1]]
    if -tl > 0.35:  # too far to extrapolate safely (arc started after a bounce?)
        continue
    vxl, vyl = vx, vy + a.g * tl
    sp = np.hypot(vxl, vyl)
    ang = np.degrees(np.arctan2(vxl, vyl))
    out.append((float(r["t0"]), sp, ang, -tl, int(r["n"]), float(r["rms"])))
print(f"{len(out)} launch arcs")
for t0, sp, ang, back, n, rms in out:
    print(f"  t={t0:8.2f}  speed {sp:6.1f} px/s  angle {ang:+6.1f} deg  extrapolated {back * 1000:4.0f} ms  n={n} rms={rms}")
if out:
    s = np.array([o[1] for o in out]); an = np.array([o[2] for o in out])
    med = np.median(s); mad = 1.4826 * np.median(np.abs(s - med))
    ok = np.abs(s - med) <= 3 * max(mad, 1)
    print(f"speed median {med:.1f}, mean {s[ok].mean():.1f} +- {s[ok].std(ddof=1) / np.sqrt(ok.sum()):.1f} (SE), SD {s[ok].std(ddof=1):.1f}, N={ok.sum()}")
    print(f"angle range {an.min():+.1f} .. {an.max():+.1f} deg")
