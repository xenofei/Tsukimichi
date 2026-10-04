"""Measure the Extreme Fever camera zoom and the ball's slow motion (measurement only).

For each frame in [START, END], ORB features are matched to a reference frame
taken before the zoom (full board visible) and a similarity transform is fitted
with RANSAC.  zoom = 1/scale of the frame->reference mapping (1.0 = normal view).
The ball (gold blob, see pegtrack.py) is located in each frame and mapped back
into native board coordinates, so its motion can be compared with normal-speed
physics.

Usage:
  uv run --with opencv-python --with numpy --with scipy python pegzoom.py VIDEO REF_S START_S END_S OUT.csv
         [--scale S] [--ball X Y]   (approximate native position of the ball at START, to seed tracking)

Output columns: frame, t, zoom, cx, cy (native board point at the frame centre),
inliers, bx, by (ball centre in native board px, blank if not found).
"""
import argparse
import csv

import cv2
import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument("video"); ap.add_argument("ref", type=float); ap.add_argument("start", type=float); ap.add_argument("end", type=float)
ap.add_argument("out")
ap.add_argument("--scale", type=float, default=None)
ap.add_argument("--ball", type=float, nargs=2, default=None)
a = ap.parse_args()

cap = cv2.VideoCapture(a.video)
fps = cap.get(cv2.CAP_PROP_FPS)
cap.set(cv2.CAP_PROP_POS_FRAMES, int(a.ref * fps)); ok, ref = cap.read()
sc = a.scale or ref.shape[0] / 600.0
H, W = ref.shape[:2]
orb = cv2.ORB_create(4000)
gref = cv2.cvtColor(ref, cv2.COLOR_BGR2GRAY)
# mask the HUD and side tubes in the reference: only the playfield is zoomed
mref = np.zeros_like(gref); mref[int(40 * sc):int(560 * sc), int(80 * sc):int(720 * sc)] = 255
kr, dr = orb.detectAndCompute(gref, mref)
bf = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=True)

s, e = int(a.start * fps), int(a.end * fps)
cap.set(cv2.CAP_PROP_POS_FRAMES, s)
rows = []
ball = None if a.ball is None else np.array(a.ball)
for i in range(s, e + 1):
    ok, f = cap.read()
    if not ok:
        break
    g = cv2.cvtColor(f, cv2.COLOR_BGR2GRAY)
    k, d = orb.detectAndCompute(g, None)
    zoom = cx = cy = float("nan"); inl = 0; M = None
    if d is not None and len(k) > 20:
        m = bf.match(d, dr)
        if len(m) > 20:
            src = np.float32([k[q.queryIdx].pt for q in m]); dst = np.float32([kr[q.trainIdx].pt for q in m])
            M, mask = cv2.estimateAffinePartial2D(src, dst, method=cv2.RANSAC, ransacReprojThreshold=3.0, maxIters=4000)
            if M is not None:
                inl = int(mask.sum())
                scl = np.hypot(M[0, 0], M[1, 0])
                zoom = 1.0 / scl
                c = M @ np.array([W / 2, H / 2, 1.0])
                cx, cy = c[0] / sc, c[1] / sc
    bx = by = ""
    if M is not None and inl >= 15:
        hsv = cv2.cvtColor(f, cv2.COLOR_BGR2HSV)
        gm = cv2.inRange(hsv, (14, 110, 90), (34, 255, 255))
        gm = cv2.morphologyEx(gm, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
        n, lab, st, ce = cv2.connectedComponentsWithStats(gm)
        cands = []
        for j in range(1, n):
            area_native = st[j, 4] / (sc * zoom) ** 2
            if 12 <= area_native <= 110:
                p = M @ np.array([ce[j][0], ce[j][1], 1.0]) / sc
                cands.append(p)
        if cands:
            if ball is not None:
                p = min(cands, key=lambda q: np.hypot(*(q - ball)))
                if np.hypot(*(p - ball)) < 25:
                    ball = p; bx, by = round(p[0], 2), round(p[1], 2)
            elif len(cands) == 1:
                ball = cands[0]; bx, by = round(ball[0], 2), round(ball[1], 2)
    rows.append([i, round(i / fps, 4), round(zoom, 4), round(cx, 1), round(cy, 1), inl, bx, by])
with open(a.out, "w", newline="") as fh:
    w = csv.writer(fh); w.writerow(["frame", "t", "zoom", "cx", "cy", "inliers", "bx", "by"]); w.writerows(rows)
print(f"{len(rows)} frames written")
