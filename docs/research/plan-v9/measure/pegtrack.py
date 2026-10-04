"""Ball candidate detector for 4:3 Peggle gameplay video (measurement only).

The Peggle ball sprite has a gold lower half and a pale cyan upper half. A
candidate is a gold blob (HSV) that is also moving (wide-gap frame
differencing), inside the playfield. Every candidate in every frame is
written; linking into trajectories is done by pegfit.py.

Usage:
  uv run --with opencv-python --with numpy --with scipy python pegtrack.py VIDEO START_S END_S OUT.csv
         [--scale S] [--ox X --oy Y] [--k 5]

  --scale  video pixels per native 800x600 pixel (default: frame height / 600)
  --ox/oy  video-pixel offset of native (0,0) (pillarboxed or cropped video)

Output columns:
  frame, t      frame index in the file and t = frame / fps (s)
  dup           1 if the frame is an exact repeat of the previous (capture stall)
  gx, gy        centroid of the gold half of the ball, native px
  mx, my        centroid of the moving blob that contains it (ball + shadow), native px
  garea, marea  blob areas in native px^2
"""
import argparse
import collections
import csv

import cv2
import numpy as np


def gold_mask(bgr):
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    return cv2.inRange(hsv, (14, 110, 90), (34, 255, 255))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("video")
    ap.add_argument("start", type=float)
    ap.add_argument("end", type=float)
    ap.add_argument("out")
    ap.add_argument("--scale", type=float, default=None)
    ap.add_argument("--ox", type=float, default=0.0)
    ap.add_argument("--oy", type=float, default=0.0)
    ap.add_argument("--k", type=int, default=4, help="frame gap for differencing (non-duplicate frames)")
    ap.add_argument("--xmin", type=float, default=75.0)
    ap.add_argument("--xmax", type=float, default=725.0)
    ap.add_argument("--ymin", type=float, default=40.0)
    ap.add_argument("--ymax", type=float, default=600.0)
    ap.add_argument("--gmin", type=float, default=12.0, help="min gold area, native px^2")
    ap.add_argument("--gmax", type=float, default=110.0, help="max gold area, native px^2")
    a = ap.parse_args()

    cap = cv2.VideoCapture(a.video)
    fps = cap.get(cv2.CAP_PROP_FPS)
    s = int(round(a.start * fps))
    e = int(round(a.end * fps))
    cap.set(cv2.CAP_PROP_POS_FRAMES, s)
    ok, f0 = cap.read()
    H, W = f0.shape[:2]
    sc = a.scale or H / 600.0
    x0, x1 = int(a.ox + a.xmin * sc), int(a.ox + a.xmax * sc)
    y0, y1 = int(a.oy + a.ymin * sc), min(H, int(a.oy + a.ymax * sc))
    K = a.k

    # ring buffer of non-duplicate frames: (frame_index, bgr_crop, gray_crop)
    buf = collections.deque()
    rows = []
    ndup = 0
    prev_gray = None
    idx = s
    f = f0

    def process(center):
        i, bgr, gray = buf[center]
        g = gold_mask(bgr)
        d = cv2.min(cv2.absdiff(gray, buf[center - K][2]), cv2.absdiff(gray, buf[center + K][2]))
        mot = (d > 18).astype(np.uint8) * 255
        mot = cv2.morphologyEx(mot, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
        cand = cv2.bitwise_and(g, cv2.dilate(mot, np.ones((3, 3), np.uint8)))
        cand = cv2.morphologyEx(cand, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
        n, lab, st, ce = cv2.connectedComponentsWithStats(cand)
        if n <= 1:
            return
        nm, mlab, mst, mce = cv2.connectedComponentsWithStats(mot)
        for c in range(1, n):
            ga = st[c, 4] / sc**2
            if ga < a.gmin or ga > a.gmax:
                continue
            w, h = st[c, 2], st[c, 3]
            if max(w, h) > 3.2 * min(w, h):
                continue
            cx, cy = ce[c]
            ml = mlab[int(round(cy)), int(round(cx))]
            if ml > 0 and mst[ml, 4] / sc**2 < 6 * a.gmax:
                mx, my = mce[ml]
                ma = mst[ml, 4] / sc**2
            else:
                mx, my, ma = cx, cy, 0.0
            rows.append([i, round(i / fps, 5), 0,
                         round((cx + x0 - a.ox) / sc, 2), round((cy + y0 - a.oy) / sc, 2),
                         round((mx + x0 - a.ox) / sc, 2), round((my + y0 - a.oy) / sc, 2),
                         round(ga, 1), round(ma, 1)])

    while ok and idx <= e:
        crop = f[y0:y1, x0:x1]
        gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
        if prev_gray is not None and cv2.countNonZero(cv2.absdiff(gray, prev_gray) > 6) == 0:
            ndup += 1
            rows.append([idx, round(idx / fps, 5), 1, "", "", "", "", "", ""])
        else:
            buf.append((idx, crop, gray))
            if len(buf) == 2 * K + 1:
                process(K)
                buf.popleft()
        prev_gray = gray
        ok, f = cap.read()
        idx += 1

    rows.sort(key=lambda r: (r[0], r[2] == 0))
    with open(a.out, "w", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["frame", "t", "dup", "gx", "gy", "mx", "my", "garea", "marea"])
        w.writerows(rows)
    print(f"frames {s}-{idx - 1}, {len(rows) - ndup} candidates, {ndup} duplicate frames, scale {sc:.3f}, fps {fps}")


if __name__ == "__main__":
    main()
