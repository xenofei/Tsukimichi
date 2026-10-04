"""Ball track in board coordinates through the Fever zoom, and the slow-motion factor.

Each frame is mapped back to native board coordinates with the per-frame zoom
transform from pegzoom.py, the ball's gold half is found near a hand-given guide
path (frame:x:y points read off a pegtile/warp contact sheet), and the vertical
motion is fitted before and during slow motion:
  factor_v = v_y(just before) / v_y(slow)      factor_g = sqrt(g / a_y(slow))

Usage:
  uv run --with opencv-python --with numpy python pegslowmo.py VIDEO ZOOM.csv F0 F1 X0 Y0 X1 Y1 "f:x:y f:x:y ..." PRE_F0 PRE_F1 SLOW_F0 SLOW_F1
Example (Nights level 1-1 Fever, see peg-measurements.md):
  ... nt_dos_0.webm zoom_nt1.csv 12300 12381 100 280 190 400 "12300:159:287 12311:151:327 12315:142:316 12322:134:333 12326:131:345 12346:129:354 12378:127.5:368 12381:127.5:369" 12316 12325 12328 12378
"""
import csv
import sys

import cv2
import numpy as np

G = 500.0
vid, zc, f0, f1 = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
x0, y0, x1, y1 = map(float, sys.argv[5:9])
guide = np.array([list(map(float, s.split(":"))) for s in sys.argv[9].split()])
pre0, pre1, sl0, sl1 = map(int, sys.argv[10:14])
K = 8
Z = {int(r["frame"]): r for r in csv.DictReader(open(zc))}
cap = cv2.VideoCapture(vid); cap.set(1, f0)
F, X, Y = [], [], []
for i in range(f0, f1 + 1):
    ok, f = cap.read(); r = Z.get(i)
    if r is None or not ok:
        continue
    H, W = f.shape[:2]; sc = H / 600; z = float(r["zoom"]); cx = float(r["cx"]); cy = float(r["cy"])
    M = np.array([[z * sc / K, 0, (x0 - cx) * z * sc + W / 2], [0, z * sc / K, (y0 - cy) * z * sc + H / 2]])
    c = cv2.warpAffine(f, M, (int((x1 - x0) * K), int((y1 - y0) * K)), flags=cv2.WARP_INVERSE_MAP | cv2.INTER_LINEAR)
    g = cv2.inRange(cv2.cvtColor(c, cv2.COLOR_BGR2HSV), (14, 110, 90), (34, 255, 255))
    n, lab, st, ce = cv2.connectedComponentsWithStats(g)
    gx = np.interp(i, guide[:, 0], guide[:, 1]); gy = np.interp(i, guide[:, 0], guide[:, 2])
    best = None
    for j in range(1, n):
        if st[j, 4] < 15 * K * K / 4:
            continue
        bx = x0 + ce[j][0] / K; by = y0 + ce[j][1] / K; d = np.hypot(bx - gx, by - gy)
        if d < 6 and (best is None or d < best[0]):
            best = (d, bx, by)
    if best:
        F.append(i); X.append(best[1]); Y.append(best[2])
        print(i, f"zoom {z:.3f}", f"{best[1]:.2f} {best[2]:.2f}")
F, X, Y = map(np.array, (F, X, Y)); T = F / 60.0


def fit(m):
    tt = T[m] - T[m][0]; A = np.vstack([np.ones_like(tt), tt, 0.5 * tt * tt]).T
    py, *_ = np.linalg.lstsq(A, Y[m], rcond=None); r = Y[m] - A @ py
    cov = np.linalg.inv(A.T @ A) * np.sum(r ** 2) / max(1, len(tt) - 3)
    return py, np.sqrt(np.diag(cov)), tt[-1]


pp, _, tl = fit((F >= pre0) & (F <= pre1)); v_pre = pp[1] + pp[2] * tl
ps, se, _ = fit((F >= sl0) & (F <= sl1))
print(f"vy just before slow motion {v_pre:.1f} px/s; during: vy {ps[1]:.2f}+-{se[1]:.2f}, ay {ps[2]:.2f}+-{se[2]:.2f} px/s^2")
print(f"slow-motion factor from velocity {v_pre / ps[1]:.2f}; from acceleration {np.sqrt(G / ps[2]):.2f}")
