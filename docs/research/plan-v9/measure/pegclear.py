"""Time the end-of-turn peg clearing (measurement only).

Given a frame A (lit pegs still on the board, ball already gone or going) and a
frame B (clearing finished), the pegs that vanished are the peg-sized blobs of
|A - B|.  For each, the clear time is the first frame after A where the disc's
mean difference from A passes half of its A->B difference.

Usage:
  uv run --with opencv-python --with numpy --with scipy python pegclear.py VIDEO TA_S TB_S [--scale S]

Prints each cleared peg's position (native px) and clear time, the intervals
between successive clears, and the order (e.g. left-to-right, by hit order...).
"""
import argparse

import cv2
import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument("video"); ap.add_argument("ta", type=float); ap.add_argument("tb", type=float)
ap.add_argument("--scale", type=float, default=None)
ap.add_argument("--ymax", type=float, default=555.0, help="ignore the bucket strip")
ap.add_argument("--xmin", type=float, default=75.0)
ap.add_argument("--xmax", type=float, default=725.0)
ap.add_argument("--rmin", type=float, default=5.5, help="min blob equivalent radius, native px")
a = ap.parse_args()
cap = cv2.VideoCapture(a.video)
fps = cap.get(cv2.CAP_PROP_FPS)
fa, fb = int(a.ta * fps), int(a.tb * fps)
cap.set(cv2.CAP_PROP_POS_FRAMES, fa)
frames = []
for i in range(fa, fb + 1):
    ok, f = cap.read()
    if not ok:
        break
    frames.append(cv2.cvtColor(f, cv2.COLOR_BGR2GRAY).astype(np.int16))
sc = a.scale or frames[0].shape[0] / 600.0
A, B = frames[0], frames[-1]
d = np.abs(A - B).astype(np.uint8)
m = (d > 35).astype(np.uint8)
m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
n, lab, st, ce = cv2.connectedComponentsWithStats(m)
pegs = []
for i in range(1, n):
    x, y = ce[i] / sc
    r = np.sqrt(st[i, 4] / np.pi) / sc
    if r < a.rmin or r > 14 or y > a.ymax or not (a.xmin < x < a.xmax):
        continue
    if max(st[i, 2], st[i, 3]) > 1.6 * min(st[i, 2], st[i, 3]):
        continue
    pegs.append((x, y, r))
H, W = A.shape
yy, xx = np.mgrid[0:H, 0:W]
res = []
for x, y, r in pegs:
    cx, cy, rr = x * sc, y * sc, 0.7 * r * sc
    y0, y1, x0, x1 = int(cy - rr), int(cy + rr) + 1, int(cx - rr), int(cx + rr) + 1
    disc = ((xx[y0:y1, x0:x1] - cx) ** 2 + (yy[y0:y1, x0:x1] - cy) ** 2) <= rr * rr
    series = np.array([np.abs(fr[y0:y1, x0:x1] - A[y0:y1, x0:x1])[disc].mean() for fr in frames])
    full = series[-1]
    if full < 20:
        continue
    k = int(np.argmax(series > 0.5 * full))
    res.append((fa + k, x, y, r))
res.sort()
print(f"{len(res)} cleared pegs between {a.ta}s and {a.tb}s (fps {fps})")
prev = None
for f, x, y, r in res:
    dt = "" if prev is None else f"  +{(f - prev) / fps * 1000:.0f} ms"
    print(f"  frame {f} t={f / fps:.3f}  at ({x:.0f},{y:.0f}) r={r:.1f}{dt}")
    prev = f
if len(res) > 2:
    iv = np.diff([r[0] for r in res]) / fps * 1000
    print(f"interval median {np.median(iv):.0f} ms  mean {iv.mean():.1f} ms  min {iv.min():.0f}  max {iv.max():.0f}  "
          f"total {(res[-1][0] - res[0][0]) / fps:.3f} s for {len(res)} pegs")
