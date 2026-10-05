"""Pilot exp-p3 "The Sea of Sorrows" (technique, our own: orbit, the layout moves the way the painting implies), layout.

The scene (scene_official.py, exp-p3-mare-lamentorum) is Mare Lamentorum: the world hanging in the black over the
moon's ruins, the field of drifting rocks above it, the curved rib and sphere of the lunar tower on the left. The
painting implies motion (a world in the sky, rocks adrift), so the layout moves: a ring of moons orbits the world,
slowly, clockwise; the drifting rocks are two loose rows along the belt; a few still moons sit on the world's darker
storms so its middle is never dead; the tower's rib and its sphere are traced on the left. The orbit carries half the
oranges, so the player times shots to the ring: the turning ring is the level's idea.
"""
import math

from layout import Layout
from rich_lib import LEVELS, level_json, write_level

LEVEL_ID = "exp-p3"
NAME = "The Sea of Sorrows"
WORLD = (507.0, 345.0)
ORBIT_R = 172.0
ORBIT_N = 26
PERIOD = 26.0


def build():
    L = Layout()
    cx, cy = WORLD
    # ---- the orbit: a ring of moons turning round the world (clockwise, one turn in 26 s, 42 px/s)
    for k in range(ORBIT_N):
        a = math.radians(-90 + 360 * k / ORBIT_N)
        L.peg(cx + ORBIT_R * math.cos(a), cy + ORBIT_R * math.sin(a), orange=(k % 2 == 0),
              move={"kind": "orbit", "x": cx, "y": cy, "period": PERIOD, "clockwise": True}, tag="orbit")
    # ---- the world's storms: a few still moons on its darker swirls, inside the orbit
    # (round 2: on the planet's night side only, its right limb, never on its lit face)
    for k, (x, y) in enumerate(((598, 300), (612, 348), (600, 396), (566, 436))):
        L.peg(x, y, orange=(k % 2 == 0), tag="storm on the night side")
    # ---- the drifting rocks: two loose rows along the belt above the world (none in the launcher's swing)
    rocks = ((100, 150), (132, 168), (168, 152), (204, 172), (240, 158), (276, 178), (312, 166),
             (110, 206), (150, 222), (190, 208), (232, 226), (272, 214),
             (520, 126), (560, 132), (600, 146), (640, 134), (680, 150), (700, 196))
    for k, (x, y) in enumerate(rocks):
        L.peg(x, y, orange=(k in (2, 5, 9, 11, 13)), tag="drifting rock")
    # ---- the tower on the left: its curved rib, the sphere beside it, the spires at its foot
    for (x, y) in ((140, 270), (150, 306), (156, 342), (152, 378), (138, 412)):
        L.peg(x, y, orange=(y in (270, 306, 378)), tag="tower rib")
    L.peg(110, 330, r=12, orange=True, tag="the sphere")
    for (x, y) in ((128, 470), (172, 456), (150, 512), (210, 500), (254, 520), (290, 470)):
        L.peg(x, y, tag="tower foot")
    # ---- the low field and the crystal flower at the foot
    for (x, y) in ((280, 540), (330, 512), (380, 540), (614, 540), (660, 516), (700, 548), (700, 482), (700, 430)):
        L.peg(x, y, orange=(x, y) in ((330, 512), (660, 516), (700, 430)), tag="low field")
    for (x, y) in ((250, 330), (290, 380), (250, 424), (286, 296)):
        L.peg(x, y, orange=(x == 290), tag="between")
    return L


if __name__ == "__main__":
    L = build()
    print(L.counts())
    if L.check():
        raise SystemExit("pre-flight failed: the level was not written")
    pegs, bricks = L.as_level()
    scene = {"source": "game", "texture": "ui/loadingimage/-nowloading_base25.tex", "mirror": True,
             "crop": [557, -108, 1166, 875], "padTop": 120, "padMode": "reflect", "grade": "medallion-night-violet",
             "veil": 0.40}
    write_level(level_json(LEVEL_ID, NAME, pegs, bricks, scene=scene), LEVELS / f"{LEVEL_ID}.json")
