"""Pilot base-p3 "The Moonlit Post" (technique: a creature in outline), layout.

The scene (scene_moogle.py) is a moogle courier flying over the Black Shroud by night, a dark silhouette rimmed by the
moon behind it. The layout draws the creature: a ring of moons follows its whole outline 18 units outside the fur
(head, ears, pom-pom, wings, feet, the letter), evenly spaced, so the shape reads before a single peg is lit; the wing's
struts run as lines of pegs from root to tip; its eye is a moon. Oranges come from the parts a child would draw first:
the pom-pom, the ear tips, the wing tips, the eye, the letter and the feet. The forest's crowns and the empty sky to the
right get a loose scatter so the ball always has somewhere to go; the moon itself is left clear.
"""
import math

import numpy as np

from layout import Layout, in_bounds
from rich_lib import LEVELS, blur, level_json, write_level
import scene_moogle as sm

LEVEL_ID = "base-p3"
NAME = "The Moonlit Post"
SPACING = 34.0
OFFSET = 18.0


def outline_points():
    """Points 18 units outside the silhouette's edge, about 34 apart (a greedy even pick round the outline)."""
    W, H = 800, 600
    Mk = sm.moogle_masks(W, H, 1)
    sil = np.zeros((H, W), np.float32)
    for k in ("head", "body", "feet", "pom", "stalk", "wing_near", "wing_far", "letter", "satchel"):
        sil = np.maximum(sil, Mk[k])
    grown = blur((sil > 0.5).astype(np.float32), OFFSET * 0.62) > 0.08
    gy, gx = np.gradient(grown.astype(np.float32))
    edge = np.sqrt(gx * gx + gy * gy) > 0
    ys, xs = np.nonzero(edge)
    cx, cy = xs.mean(), ys.mean()
    order = np.argsort(np.arctan2(ys - cy, xs - cx))
    pts = []
    for i in order:
        x, y = float(xs[i]) + 0.5, float(ys[i]) + 0.5
        if all(math.hypot(x - px, y - py) >= SPACING for (px, py) in pts):
            pts.append((x, y))
    return pts, sil


def build():
    L = Layout()
    placed = []
    mx, my, mr = sm.MOON

    def ok(x, y, r=10.0, gap=13.0):
        return (in_bounds(x, y, r) and math.hypot(x - mx, y - my) > mr + 26
                and all(math.hypot(x - px, y - py) - r - pr >= gap for (px, py, pr) in placed))

    def put(x, y, orange=False, r=10.0, tag=""):
        if ok(x, y, r):
            L.peg(x, y, orange=orange, r=r, tag=tag)
            placed.append((x, y, r))
            return True
        return False

    features = {
        "pom-pom": (sm.POM[0], sm.POM[1], 48), "ear tip": (sm.EAR_R[2][0], sm.EAR_R[2][1], 36),
        "left ear": (sm.EAR_L[2][0], sm.EAR_L[2][1], 36),
        "wing tip": [(x, y, 26) for (x, y) in sm.WING_NEAR["tips"]],
        "letter": (sum(p[0] for p in sm.LETTER) / 4, sum(p[1] for p in sm.LETTER) / 4, 48),
        "foot": [(b[0], b[1], 30) for (_, b, _, _) in sm.FEET],
        "nose": (sm.NOSE[0], sm.NOSE[1], 34),
    }

    def tag_for(x, y):
        for name, f in features.items():
            for (fx, fy, rad) in (f if isinstance(f, list) else [f]):
                if math.hypot(x - fx, y - fy) < rad:
                    return name
        return None

    # the eye first, then the outline round the whole creature
    put(*sm.EYE, orange=False, tag="the eye")
    pts, _ = outline_points()
    for (x, y) in pts:
        t = tag_for(x, y)
        # nothing at the launcher's height is a candidate (a first flight cannot touch it: level critic round 1)
        put(x, y, orange=t is not None and y >= (150 if t == "pom-pom" else 112), tag=t or "outline")
    # the wing's struts, root to tip, inside the membrane
    w = sm.WING_NEAR
    for tip in w["tips"][1:]:
        rx, ry = w["root"]
        for f in (0.45, 0.72):
            put(rx + (tip[0] - rx) * f, ry + (tip[1] - ry) * f, orange=False, tag="wing strut")
    # the forest: a wavy treeline along the crowns, then the wood's crowns as staggered rows below it, dense enough
    # that a falling ball always meets them (round 2: the low board was too sparse, its oranges were left behind)
    for x in range(104, 710, 38):  # noqa
        put(x, 404 + 14 * math.sin(x / 47.0), orange=((x - 104) // 38) in (1, 5, 9, 12, 14), tag="treetops")
    for row, y0 in enumerate((452, 496)):           # (round 4: the lowest row cut, so the lantern has its lane)
        for k in range(17):
            x = 100 + k * 38 + (19 if row % 2 else 0)
            y = y0 + 8 * math.sin(x / 41.0 + row)
            if x > 702:
                continue
            lamp = row == 0 and k % 2 == 1     # cottage lamps, on the upper row of the wood
            put(x, y, orange=lamp, tag="lamp in the wood" if lamp else "forest")
    # the sky's scatter to the right and the left of the courier
    for (x, y) in ((660, 200), (700, 236), (640, 300), (690, 330), (650, 360),
                   (120, 230), (170, 262), (222, 230), (130, 300), (180, 340), (110, 360), (240, 120), (276, 150),
                   (300, 196)):
        put(x, y, tag="sky")
    return L


if __name__ == "__main__":
    L = build()
    print(L.counts())
    if L.check():
        raise SystemExit("pre-flight failed: the level was not written")
    pegs, bricks = L.as_level()
    scene = {"source": "asset", "file": "scenes/base-p3-moogle.jpg", "file2x": "scenes/base-p3-moogle@2x.jpg",
             "veil": 0.22}
    write_level(level_json(LEVEL_ID, NAME, pegs, bricks, scene=scene), LEVELS / f"{LEVEL_ID}.json")
