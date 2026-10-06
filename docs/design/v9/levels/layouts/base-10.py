"""2-5 "Twin Lanterns" (Vesper Bay, the twins): reflection.

Subject: Vesper Bay's harbour gate between two lit stone lanterns, and all of it again in the glass-still water below
(our own painting, painters/twin_lanterns.py). Two of everything, for the twins and their Multiball.

Technique: above the waterline the gate's arch wears a crown of brick (never green: the subject's own shape), its pillars
are short dotted runs just outside their faces, and each lantern's head is a ring of five moons over a dotted post.
Below the waterline the same figure stands mirrored, drawn with slow slide movers that drift 12 units and back, so the
reflection ripples while the gate stands still. The mirrored arch hangs (concave up), so it is dotted, never brick.
Both lantern heads are candidates; each reflected head only at its two upper moons, and the reflected arch at the U's
second pair (y 486), so the finale's difficulty comes from the whole board, not its bottom (game designer G11, the
owner's answer of 6 October 2026: recommendation taken). Six stars in the sky lanes take the other oranges; more stars,
the far shore's low hills and two ripple marks of brick in the still water fill the rest.

What carries the finale's gap (game designer round 6, G24; measured on the tuning block, seeds 20001-23456): the corner
stars (110, 200) and (690, 200), each left in up to a fifth of lost games; the U's second pair at y 486 (about 1.5 per
48 against the outermost pair at y 456); and the two blue stars seen through the gate, (350, 250) and (450, 250), about
2 per 48. The right corner star once sat behind a blue star on the launcher's line to it (critic N21): that star moved
from (660, 170) to (640, 150), off the line, but the right field still shielded it ((640, 150) took 13 first touches, the
corner star 3 against its mirror's 15: critic runtime round 1, m5). The right field is now the left's mirror: (660, 150)
for (140, 150), and (660, 120), which had no mirror, is gone, so both corner stars are fair first-touch shots. The palette turns warm for the stage's last board:
a true rose over the water and the lower sky, the high sky's lapis second (game designer n2 and G8). The stage's last level, back at Vesper
Bay's own gate: the twins' own board, and the stage's hardest.
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
    b.arc_bricks(cx, cy, R, a0, sw, n=3, t=12, green=False, tag="arch crown")     # the subject's own shape
    for (x, top, foot) in b.features["pillars"]:
        side = -1 if x < cx else 1
        b.trace([(x + side * 30, top + 8), (x + side * 30, foot)], spacing=36, r=9, orange={1}, tag="pillar")
    for (lx, ly) in b.f("lanterns"):
        b.stop(lx, ly + 2, R=30, n=5, r=9, oranges=(0, 1, 2, 3, 4), green=False, tag="lantern")
        b.place(lx, ly + 66, r=9, tag="post")
        b.place(lx, ly + 100, r=9, orange=True, tag="post")
    # the reflection: everything mirrored across the waterline, drifting together
    mirror = lambda y: 2 * water - y
    for k in range(7):
        a = math.radians(200 + 140 * k / 6)
        x, y = cx + R * math.cos(a), mirror(cy + R * math.sin(a))
        b.slide(x, y, RIPPLE, 0, PERIOD, r=9, orange=k in (1, 5), tag="arch reflection")   # the U's second pair only
    for (x, top, foot) in b.features["pillars"]:
        side = -1 if x < cx else 1
        b.slide(x + side * 30, mirror(top + 44), RIPPLE, 0, PERIOD, r=9, orange=True, tag="pillar reflection")
    for (lx, ly) in b.f("lanterns"):
        b.slide(lx, mirror(ly + 66), RIPPLE, 0, PERIOD, r=9, tag="post reflection")
        for k in range(5):
            a = math.radians(90 + 72 * k)                                # the ring turned over: its top is now below
            b.slide(lx + 30 * math.cos(a), mirror(ly + 2) + 30 * math.sin(a), RIPPLE, 0, PERIOD, r=9,
                    orange=k in (2, 3), green=False, tag="lantern reflection")    # its two upper moons
    # the night sky and the far shore: stars, and the shore's low hills dotted either side
    SKY = {(110, 200), (690, 200), (250, 290), (550, 290), (220, 150), (580, 150)}     # the stars that are candidates
    for (x, y) in ((240, 190), (560, 190), (660, 150), (130, 300), (680, 300), (250, 290), (550, 290), (140, 150)):
        b.place(x, y, r=8, orange=(x, y) in SKY, tag="star")
    for line in ([(92, 332), (150, 338)], [(650, 338), (708, 332)]):
        b.trace(line, spacing=30, r=7, tag="far shore")
    for (x, y) in ((110, 400), (690, 400), (120, 480), (680, 480), (400, 440), (240, 410), (560, 410), (110, 440),
                   (690, 440)):
        b.place(x, y, r=8, tag="still water")
    # ripple marks in the still water, in pairs: short arcs of brick, concave down
    for (x, y) in ((278, 498), (530, 498)):
        b.arc(x, y + 30, 30, 235, 70, t=10, tag="ripple")
    for (x, y) in ((220, 150), (580, 150), (110, 200), (690, 200), (300, 180), (500, 180)):     # high stars
        b.place(x, y, r=8, orange=(x, y) in SKY, tag="star")
    for (x, y) in ((350, 250), (450, 250)):        # two stars seen through the gate, blue, last so the deal stays
        b.place(x, y, r=8, tag="star")
    b.greens_in_reach()
