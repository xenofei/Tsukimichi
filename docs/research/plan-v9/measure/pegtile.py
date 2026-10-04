"""Contact sheet of frames for reading values by eye (score counter, tally text, popups).

Usage: uv run --with opencv-python --with numpy python pegtile.py VIDEO START_S END_S EVERY_N_FRAMES TILE_W COLS OUT.png [X0 Y0 X1 Y1]
X0..Y1 crop in native 800x600 px of a full-frame 4:3 video. Each tile is labelled "frame time".
"""
import sys

import cv2
import numpy as np

vid, s, e, n, w, cols, out = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5]), int(sys.argv[6]), sys.argv[7]
roi = list(map(float, sys.argv[8:12])) if len(sys.argv) > 11 else None
cap = cv2.VideoCapture(vid); fps = cap.get(5); fs = int(s * fps); fe = int(e * fps)
cap.set(1, fs); tiles = []
for i in range(fs, fe + 1):
    ok, f = cap.read()
    if not ok:
        break
    if (i - fs) % n:
        continue
    if roi:
        sc = f.shape[0] / 600; x0, y0, x1, y1 = [int(v * sc) for v in roi]; f = f[y0:y1, x0:x1]
    h = int(f.shape[0] * w / f.shape[1]); t = cv2.resize(f, (w, h), interpolation=cv2.INTER_AREA)
    for col, th in (((0, 0, 0), 3), ((0, 255, 255), 1)):
        cv2.putText(t, f"{i} {i / fps:.2f}", (4, 16), 0, 0.5, col, th)
    tiles.append(t)
while len(tiles) % cols:
    tiles.append(np.zeros_like(tiles[0]))
cv2.imwrite(out, np.vstack([np.hstack(tiles[i:i + cols]) for i in range(0, len(tiles), cols)]))
