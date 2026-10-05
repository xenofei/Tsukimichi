"""Diagnostic (not a deliverable): each pilot's approved composite with the framing-free zone shaded, to place the
re-dress shapes. Green: framing allowed (6+ units from every piece, in the F2 regions). Writes to the given dir."""
import sys
import pathlib

import numpy as np
from PIL import Image

from r2lib import RICH, load_rgb
from board import load_level
from framecheck import piece_distance, WALL_L, WALL_R, TOP, FOOT, PIVOT

out = pathlib.Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
for lid in ("base-p1", "base-p2", "base-p3", "exp-p1", "exp-p2", "exp-p3"):
    lvl = load_level(RICH / "levels" / f"{lid}.json")
    d = piece_distance(lvl)
    comp = load_rgb(RICH / "composites" / f"{lid}.png")
    yy, xx = np.mgrid[0:600, 0:800].astype(np.float32)
    X, Y = xx + 0.5, yy + 0.5
    edge = (np.minimum(X - WALL_L, WALL_R - X) < 60) | (Y > 470) & (np.minimum(X - WALL_L, WALL_R - X) < 170) | \
           ((Y < TOP + 290) & (np.minimum(X - WALL_L, WALL_R - X) < 140))
    ok = (d >= 6) & edge & (np.sqrt((X - PIVOT[0]) ** 2 + (Y - PIVOT[1]) ** 2) > 100)
    img = comp.copy()
    img[ok] = img[ok] * 0.6 + np.array([0.1, 0.6, 0.2]) * 0.4
    for gx in range(100, 800, 50):
        img[:, gx] = img[:, gx] * 0.7 + 0.3
    for gy in range(50, 600, 50):
        img[gy, :] = img[gy, :] * 0.7 + 0.3
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(out / f"free-{lid}.png")
    print(lid)
