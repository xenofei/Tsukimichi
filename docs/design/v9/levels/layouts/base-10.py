"""2-5 "Twin Lanterns" (Vesper Bay, the twins): reflection.

Subject: Vesper Bay's harbour gate between two lit stone lanterns, and all of it again in the glass-still water below
(our own painting, painters/twin_lanterns.py). Two of everything, for the twins and their Multiball.

Technique: above the waterline the gate's arch wears a crown of brick, its pillars are short dotted runs just outside
their faces, and each lantern's head is a ring of five moons over a dotted post. Below the waterline the same figure
stands mirrored, drawn with slow slide movers that drift 12 units and back, so the reflection ripples while the gate
stands still. The mirrored arch hangs (concave up), so it is dotted, never brick. The lanterns and their reflections
hold the oranges; the lantern heads are never green. The stage's last level, back at Vesper Bay's own gate: the twins'
own board, the moving reflection holding more of the oranges than the still gate.
"""
import math

LEVEL = dict(id="base-10", name="Twin Lanterns", stage=2, number=10, scene="vesper-twin-lanterns",
             subject="a harbour gate and twin lanterns mirrored in still water",
             technique="reflection (the mirror image drawn with slide movers)")

RIPPLE = 12.0          # how far the reflection drifts (units)
PERIOD = 5.0           # seconds for a drift and back


def build(b):
    water = b.features["waterline"]
    cx, cy, R, a0, sw = b.circle("arch")
    b.arc_bricks(cx, cy, R, a0, sw, n=3, t=12, tag="arch crown")
    for (x, top, foot) in b.features["pillars"]:
        side = -1 if x < cx else 1
        b.trace([(x + side * 30, top + 8), (x + side * 30, foot)], spacing=36, r=9, orange={1}, tag="pillar")
    for (lx, ly) in b.f("lanterns"):
        b.stop(lx, ly + 2, R=30, n=5, r=9, oranges=(0, 2, 3), green=False, tag="lantern")
        b.place(lx, ly + 66, r=9, tag="post")
        b.place(lx, ly + 100, r=9, orange=True, tag="post")
    # the reflection: everything mirrored across the waterline, drifting together
    mirror = lambda y: 2 * water - y
    for k in range(7):
        a = math.radians(200 + 140 * k / 6)
        x, y = cx + R * math.cos(a), mirror(cy + R * math.sin(a))
        b.slide(x, y, RIPPLE, 0, PERIOD, r=9, orange=k in (1, 2, 3, 4, 5), tag="arch reflection")
    for (x, top, foot) in b.features["pillars"]:
        side = -1 if x < cx else 1
        b.slide(x + side * 30, mirror(top + 44), RIPPLE, 0, PERIOD, r=9, orange=True, tag="pillar reflection")
    for (lx, ly) in b.f("lanterns"):
        b.slide(lx, mirror(ly + 66), RIPPLE, 0, PERIOD, r=9, tag="post reflection")
        for k in range(5):
            a = math.radians(90 + 72 * k)                                # the ring turned over: its top is now below
            b.slide(lx + 30 * math.cos(a), mirror(ly + 2) + 30 * math.sin(a), RIPPLE, 0, PERIOD, r=9,
                    orange=k in (0, 1, 4), green=False, tag="lantern reflection")
    # the night sky and the far shore
    for k, (x, y) in enumerate(((240, 190), (560, 190), (660, 170), (130, 300), (680, 300), (250, 290), (550, 290),
                                (150, 230), (650, 230), (120, 360), (690, 360), (300, 220), (500, 220))):
        b.place(x, y, r=8, orange=k in (5, 6, 11), tag="star")
    for (x, y) in ((110, 400), (690, 400), (120, 480), (680, 480), (300, 500), (400, 440), (240, 410), (560, 410)):
        b.place(x, y, r=8, orange=False, tag="still water")
    b.greens_in_reach()
