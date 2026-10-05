"""Pilot exp-p2 "The Ferry in the Stars" (technique: a constellation), layout.

The scene (scene_constellation.py) is open sea at the Far Shore's violet hour under the Lantern Ferry, the constellation
the sailors steer by. The layout is connect-the-dots: a moon on every star of the figure (the lantern's star the
largest), smaller moons dotted along the atlas's lines between them, and a loose field of sky stars round it, thicker
along the Milky Way. The figure's stars are the orange candidates, with a few of the brightest field stars, so clearing
the level lights the ferry star by star. The sea gets a few low reflections.
"""
import math

import numpy as np

from layout import Layout, in_bounds
from rich_lib import LEVELS, level_json, write_level
from scene_constellation import HORIZON, LINES, STARS, star_xy

LEVEL_ID = "exp-p2"
NAME = "The Ferry in the Stars"


def build():
    L = Layout()
    placed = []

    def put(x, y, r=10.0, orange=False, tag="", gap=13.0):
        if in_bounds(x, y, r) and all(math.hypot(x - px, y - py) - r - pr >= gap for (px, py, pr) in placed):
            L.peg(x, y, orange=orange, r=r, tag=tag)
            placed.append((x, y, r))
            return True
        return False

    # ---- the figure's stars (the lantern largest), then the dots along its lines
    for name, (x, y, mag) in STARS.items():
        put(x, y, r={1: 12.0, 2: 10.0, 3: 9.0, 4: 8.0}[mag], orange=True, tag=f"star: {name}")
    for a, b in LINES:
        (x0, y0), (x1, y1) = star_xy(a), star_xy(b)
        L_ = math.hypot(x1 - x0, y1 - y0)
        n = int(L_ // 30)
        for k in range(1, n):
            t = k / n
            put(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, r=7.0, tag="atlas line")
    # ---- the field of sky stars: a jittered grid, thicker along the Milky Way, clear of the figure
    rng = np.random.default_rng(5)
    ang = math.radians(-28)
    cands = []
    for gy in range(70, int(HORIZON) - 10, 34):
        for gx in range(96, 712, 34):
            x = gx + rng.uniform(-9, 9) + (17 if (gy // 34) % 2 else 0)
            y = gy + rng.uniform(-9, 9)
            v = -(x - 400) * math.sin(ang) + (y - 300) * math.cos(ang)
            p = 0.18 + 0.30 * math.exp(-(v / 80) ** 2)
            if rng.random() < p:
                cands.append((x, y))
    for (x, y) in cands:
        if all(math.hypot(x - px, y - py) > 40 for (px, py, _) in placed):
            put(x, y, r=9.0, tag="sky star")
    # the brightest field stars: spread over the sky (farthest-point picks), so oranges are never all in the ferry
    field = [p for p, (kind, tag) in zip(L.pegs, L.tags) if tag == "sky star"]
    chosen = [field[0]]
    while len(chosen) < 14 and len(chosen) < len(field):
        best = max(field, key=lambda p: min(math.hypot(p["x"] - c["x"], p["y"] - c["y"]) for c in chosen))
        chosen.append(best)
    for p in chosen:
        p["canBeOrange"] = True
    # ---- the sea: a few low reflections
    for (x, y) in ((120, 500), (190, 520), (260, 500), (330, 530), (470, 520), (540, 500), (610, 528), (680, 506),
                   (150, 546), (400, 548), (650, 548)):
        put(x, y, r=9.0, tag="reflection")
    return L


if __name__ == "__main__":
    L = build()
    print(L.counts())
    L.check()
    pegs, bricks = L.as_level()
    scene = {"source": "asset", "file": "scenes/exp-p2-lantern-ferry.jpg", "file2x": "scenes/exp-p2-lantern-ferry@2x.jpg",
             "veil": 0.20}
    write_level(level_json(LEVEL_ID, NAME, pegs, bricks, scene=scene), LEVELS / f"{LEVEL_ID}.json")
