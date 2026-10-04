"""Visual size of round orange pegs and of the ball sprite in a still frame.

Usage:
  uv run --with opencv-python --with numpy python pegsize.py VIDEO FRAME [--scale S]
  uv run --with opencv-python --with numpy python pegsize.py VIDEO FRAME --ball X Y --bg BGFRAME [--scale S]

Pegs: orange-hue blobs (H<=22, S>=120, V>=120), filled; prints the equivalent
radius (sqrt(area/pi)) and the min enclosing circle radius in native px.
Ball: |frame - bgframe| in a window around native (X,Y), thresholded; same stats.
The ball's drop shadow is excluded by keeping only pixels brighter than the
background or with a large colour change.
"""
import argparse
import cv2
import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument("video"); ap.add_argument("frame", type=int)
ap.add_argument("--scale", type=float, default=None)
ap.add_argument("--ball", type=float, nargs=2, default=None)
ap.add_argument("--bg", type=int, default=None)
a = ap.parse_args()
cap = cv2.VideoCapture(a.video)
cap.set(cv2.CAP_PROP_POS_FRAMES, a.frame); ok, f = cap.read()
sc = a.scale or f.shape[0] / 600.0
if a.ball is None:
    hsv = cv2.cvtColor(f, cv2.COLOR_BGR2HSV)
    m = cv2.inRange(hsv, (0, 120, 120), (22, 255, 255))
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    cnts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    req, rmec = [], []
    for c in cnts:
        ar = cv2.contourArea(c) / sc**2
        if ar < 100 or ar > 600:
            continue
        (x, y), r = cv2.minEnclosingCircle(c)
        x_, y_, w, h = cv2.boundingRect(c)
        if max(w, h) > 1.25 * min(w, h):
            continue  # bricks or merged
        req.append(np.sqrt(ar / np.pi)); rmec.append(r / sc)
    req, rmec = np.array(req), np.array(rmec)
    print(f"orange round pegs: N={len(req)}  r_eq={np.median(req):.2f} (IQR {np.percentile(req,25):.2f}-{np.percentile(req,75):.2f})  "
          f"r_enclosing={np.median(rmec):.2f} (IQR {np.percentile(rmec,25):.2f}-{np.percentile(rmec,75):.2f}) native px")
else:
    cap.set(cv2.CAP_PROP_POS_FRAMES, a.bg); ok, b = cap.read()
    x, y = int(a.ball[0] * sc), int(a.ball[1] * sc); h = int(14 * sc)
    fw = f[y - h:y + h, x - h:x + h].astype(np.int16); bw = b[y - h:y + h, x - h:x + h].astype(np.int16)
    d = np.abs(fw - bw).max(axis=2)
    lum_up = fw.mean(axis=2) - bw.mean(axis=2)
    m = ((d > 40) & (lum_up > -15)).astype(np.uint8) * 255
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    n, lab, st, ce = cv2.connectedComponentsWithStats(m)
    i = 1 + np.argmax(st[1:, 4])
    cnts, _ = cv2.findContours((lab == i).astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    (cx, cy), r = cv2.minEnclosingCircle(cnts[0])
    print(f"ball: r_eq={np.sqrt(st[i,4]/np.pi)/sc:.2f}  r_enclosing={r/sc:.2f}  bbox {st[i,2]/sc:.1f}x{st[i,3]/sc:.1f} native px")
