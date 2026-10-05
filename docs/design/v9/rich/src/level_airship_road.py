"""Pilot base-p1 "The Airship Road" (technique: a trail on a map), layout.

The scene (scene_airship_road.py) is the official map of Aldenard under the moon, with the airship's route engraved on
it in gilt. The layout is that route: a dotted line of slightly smaller pegs (r 9) along the dashes, from Limsa
Lominsa across the strait to Ul'dah, north by Mor Dhona's lake to Ishgard, down to Gridania and east to Ala Mhigo.
Each city is a stop: a ring of moons round its banner, and those rings are where the oranges come from, so clearing
the level is visiting every city. The sea gets the compass rose (a ring of eight with its heart), the edge of the sea of
clouds to the north-east, the east coast of Vylbrand along the strait, and a few lights on the southern sea.
"""
import math

import numpy as np

from layout import Layout, in_bounds, resample, smooth_path
from rich_lib import LEVELS, level_json, write_level
from scene_airship_road import ROUTE_SRC, STOPS, to_board

LEVEL_ID = "base-p1"
NAME = "The Airship Road"
RING_R = 24.0


def build():
    L = Layout()
    stops = {k: to_board(*v) for k, v in STOPS.items()}
    # nudge two stops off the frame's keep-outs: Limsa Lominsa off the left wall, Ishgard out of the launcher's swing
    stops["Limsa Lominsa"] = (stops["Limsa Lominsa"][0] + 14, stops["Limsa Lominsa"][1])
    stops["Ishgard"] = (stops["Ishgard"][0] - 8, stops["Ishgard"][1] + 14)
    route = smooth_path([to_board(*p) for p in ROUTE_SRC], 8)
    placed = []

    def free(x, y, r, gap=12.5):
        return in_bounds(x, y, r) and all(math.hypot(x - px, y - py) - r - pr >= gap for (px, py, pr) in placed)

    def put(x, y, r=10.0, orange=False, tag=""):
        if free(x, y, r):
            L.peg(x, y, orange=orange, r=r, tag=tag)
            placed.append((x, y, r))
            return True
        return False

    # ---- the stops: a ring of five round each city, turned so the trail runs in and out through its gaps. The
    # southern and eastern cities carry five candidates; the three close together in the north carry three each, so
    # the oranges do not crowd the middle of the board
    # (round 3: in these rings the lowest moon is blue; it is the one a falling ball most often misses)
    crowded = ("Mor Dhona", "Ishgard", "Gridania", "Ul'dah", "Limsa Lominsa")
    R_ = np.asarray(route)
    for name, (cx, cy) in stops.items():
        # the ring is four moons turned so the route runs in and out through two opposite gaps
        i = int(np.argmin(((R_ - (cx, cy)) ** 2).sum(1)))
        a0, a1 = R_[max(0, i - 3)], R_[min(len(R_) - 1, i + 3)]
        tangent = math.atan2(a1[1] - a0[1], a1[0] - a0[0])
        ring = [(cx + RING_R * math.cos(tangent + math.radians(45 + 90 * k)),
                 cy + RING_R * math.sin(tangent + math.radians(45 + 90 * k))) for k in range(4)]
        lowest = max(range(4), key=lambda k: ring[k][1])
        for k, (x, y) in enumerate(ring):
            low2 = sorted(range(4), key=lambda j: ring[j][1])[2:] if name == "Ul'dah" else [lowest]
            put(x, y, r=9.0, orange=(name not in crowded or k not in low2), tag=f"stop: {name}")
    # ---- the trail: the main stroke of the board, smaller moons every 30 along the whole route, running right up to
    # each ring's gaps (the ring's own pegs turn away the dots that would crowd it)
    for (x, y) in resample(route, 30.0, 2.0):
        if all(math.hypot(x - cx, y - cy) > 20 for (cx, cy) in stops.values()):
            put(x, y, r=8.0, tag="trail")
    # ---- the compass rose at sea: eight points and its heart
    cx, cy = 568.0, 343.0
    for k in range(8):
        a = math.radians(-90 + 45 * k)
        put(cx + 44 * math.cos(a), cy + 44 * math.sin(a), orange=(k in (0, 6)), tag="compass point")
    put(cx, cy, r=11.0, orange=True, tag="compass heart")
    # ---- the edge of the sea of clouds, north-east (a short run), and Vylbrand's east coast along the strait
    for (x, y) in ((560, 186), (600, 202), (640, 222), (680, 246)):
        put(x, y, orange=(x == 600), tag="cloud edge")
    for (x, y) in ((228, 300), (226, 336), (220, 372)):
        put(x, y, tag="Vylbrand coast")
    # ---- the ships' lanterns on the southern sea lane: candidates, so the lower right always holds oranges
    for (x, y) in ((592, 526), (640, 500), (688, 474)):
        put(x, y, orange=True, tag="ship's lantern")
    for (x, y) in ((140, 512), (196, 530), (250, 546), (360, 540), (420, 532), (480, 510), (540, 500), (440, 470),
                   (390, 440), (640, 420), (500, 420), (690, 380), (110, 380), (150, 350)):
        light = (x, y) in ((500, 420), (440, 470))         # two more ships' lights, mid-sea
        put(x, y, orange=light, tag="ship's light" if light else "sea")
    # ---- the land between the stops: the Shroud's forest, the Thanalan hills, the Coerthas snows (scenery, blue)
    for (x, y) in ((380, 300), (420, 330), (460, 300), (470, 360), (420, 390), (360, 260), (250, 240), (260, 290),
                   (190, 270), (180, 220), (130, 260), (250, 400), (200, 420), (470, 200), (520, 210), (640, 290),
                   (700, 200), (680, 160)):
        beacon = False
        put(x, y, orange=beacon, tag="beacon" if beacon else "land")
    return L


if __name__ == "__main__":
    L = build()
    print(L.counts())
    if L.check():
        raise SystemExit("pre-flight failed: the level was not written")
    pegs, bricks = L.as_level()
    scene = {"source": "game", "texture": "ui/loadingimage/-nowloading_base05.tex", "crop": [-30, 370, 840, 630],
             "padLeft": 64, "grade": "medallion-night", "overlay": "route", "veil": 0.40}
    write_level(level_json(LEVEL_ID, NAME, pegs, bricks, scene=scene), LEVELS / f"{LEVEL_ID}.json")
