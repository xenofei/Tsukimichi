"""Pilot base-p1 "The Airship Road" (technique: a trail on a map), layout.

The scene (scene_airship_road.py) is the official map of Aldenard under the moon, with the airship's route engraved on
it in gilt. The layout is that route: a dotted line of slightly smaller pegs (r 9) along the dashes, from Limsa
Lominsa across the strait to Ul'dah, north by Mor Dhona's lake to Ishgard, down to Gridania and east to Ala Mhigo.
Each city is a stop: a ring of moons round its banner, and those rings are where the oranges come from, so clearing
the level is visiting every city. The sea gets the compass rose (a ring of eight with its heart), the edge of the sea of
clouds to the north-east, the east coast of Vylbrand along the strait, and a few lights on the southern sea.
"""
import math

from layout import Layout, in_bounds, resample, smooth_path
from rich_lib import LEVELS, level_json, write_level
from scene_airship_road import ROUTE_SRC, STOPS, to_board

LEVEL_ID = "base-p1"
NAME = "The Airship Road"
RING_R = 29.0


def build():
    L = Layout()
    stops = {k: to_board(*v) for k, v in STOPS.items()}
    # nudge two stops off the frame's keep-outs: Limsa Lominsa off the left wall, Ishgard out of the launcher's swing
    stops["Limsa Lominsa"] = (stops["Limsa Lominsa"][0] + 14, stops["Limsa Lominsa"][1])
    stops["Ishgard"] = (stops["Ishgard"][0] - 8, stops["Ishgard"][1] + 14)
    route = smooth_path([to_board(*p) for p in ROUTE_SRC], 8)
    placed = []

    def free(x, y, r, gap=13.0):
        return in_bounds(x, y, r) and all(math.hypot(x - px, y - py) - r - pr >= gap for (px, py, pr) in placed)

    def put(x, y, r=10.0, orange=False, tag=""):
        if free(x, y, r):
            L.peg(x, y, orange=orange, r=r, tag=tag)
            placed.append((x, y, r))
            return True
        return False

    # ---- the stops: a ring of five round each city, turned so the trail runs in and out through its gaps
    for name, (cx, cy) in stops.items():
        for k in range(5):
            a = math.radians(-90 + 72 * k + 36)
            put(cx + RING_R * math.cos(a), cy + RING_R * math.sin(a), orange=True, tag=f"stop: {name}")
    # ---- the trail: smaller moons every 32 along the route, kept clear of the rings
    for (x, y) in resample(route, 32.0, 8.0):
        if all(math.hypot(x - cx, y - cy) > RING_R + 14 for (cx, cy) in stops.values()):
            put(x, y, r=9.0, tag="trail")
    # ---- the compass rose at sea: eight points and its heart
    cx, cy = 568.0, 343.0
    for k in range(8):
        a = math.radians(-90 + 45 * k)
        put(cx + 44 * math.cos(a), cy + 44 * math.sin(a), orange=(k % 2 == 0), tag="compass point")
    put(cx, cy, r=11.0, orange=True, tag="compass heart")
    # ---- the edge of the sea of clouds, north-east
    for (x, y) in ((480, 158), (514, 168), (548, 180), (582, 194), (616, 210), (650, 230), (684, 252), (700, 286)):
        put(x, y, tag="cloud edge")
    # ---- Vylbrand's east coast along the strait, and the dragons' sky above it
    for (x, y) in ((228, 300), (226, 336), (220, 372), (212, 406)):
        put(x, y, tag="Vylbrand coast")
    for (x, y) in ((110, 150), (146, 136), (184, 132), (222, 140), (258, 156)):
        put(x, y, tag="northern sea")
    # ---- the southern sea and the sea lane south-east of the compass
    for (x, y) in ((126, 506), (172, 524), (218, 540), (400, 536), (446, 518), (498, 470), (540, 500), (592, 526),
                   (640, 506), (686, 486), (620, 420), (660, 446), (700, 400), (690, 330), (650, 300)):
        put(x, y, tag="sea")
    return L


if __name__ == "__main__":
    L = build()
    print(L.counts())
    L.check()
    pegs, bricks = L.as_level()
    scene = {"source": "game", "texture": "ui/loadingimage/-nowloading_base05.tex", "crop": [-30, 370, 840, 630],
             "padLeft": 64, "grade": "medallion-night", "overlay": "route", "veil": 0.30}
    write_level(level_json(LEVEL_ID, NAME, pegs, bricks, scene=scene), LEVELS / f"{LEVEL_ID}.json")
