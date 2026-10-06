"""2-3 "The Kraken's Sea" (Vesper Bay, the twins): a set of points, with an orbit.

Subject: the Rhotano Sea the Vesper Bay ferry sails, as the game's own world painting "The Three Great Continents"
draws its wonders: a kraken wrapped round a ship, a galleon under full sail, a flock of birds in a long skein, a
whirlpool, a mermaid with her harp, and a field of wrecks (read as a chart under the moon).

Technique: each wonder is marked as the chart marks it. The kraken is ringed by nine moons, seven of them candidates;
the galleon's hull is a dotted line (it hangs, so it is never brick) with a moon at each masthead; every bird of the
skein is a small moon, a set of points like a constellation; the whirlpool is an orbit, a ring of eight moons turning
slowly round its eye (an engine orbit mover); the mermaid is traced down her side with a moon on the harp's crown; the
wrecks' masts are moons in the field of wrecks. The open sea holds pairs of moons and the chart's engraved swell marks
(short arcs of brick); round 1's rhumb lines are gone (critic L8: they crossed the wonders without reading as
anything). The kraken's ring, the mastheads and the harp are oranges that are never green.
"""
LEVEL = dict(id="base-08", name="The Kraken's Sea", stage=2, number=8, scene="rhotano-wonders",
             subject="the sea's wonders on the world chart: kraken, galleon, birds, whirlpool, mermaid, wrecks",
             technique="a set of points, with an orbit (the whirlpool)")


def build(b):
    import math
    cx, cy, R, _, _ = b.circle("kraken")
    for k in range(9):
        a = math.radians(-90 + 40 * k)
        b.place(cx + R * math.cos(a), cy + R * math.sin(a), r=9, orange=k in (1, 2, 3, 4, 5, 6, 7), green=False,
                tag="kraken")
    b.place(cx, cy + 6, r=11, orange=True, green=False, tag="kraken's eye")
    b.trace("galleon hull", spacing=30, r=9, orange={0, 2}, tag="galleon hull")
    for (x, y) in b.f("galleon masts"):
        b.place(x, y, r=9, orange=True, green=False, tag="masthead")
    # the lead bird (512, 118) is blue: it sits in the chart's green-teal top band, where an orange separates least
    # for protan eyes (UX round 4 m6; round 5 G4: no mask term may single it out); the right wing's (558, 156) is blue
    # too, the skill shot left in over a third of lost games (game designer G17); the open sea's (331, 418) takes one
    # place, and a blue star at (300, 140) holds the ramp (tuned on a 3456-game tuning block, seeds 20001-23456)
    for k, (x, y) in enumerate(b.f("flock")):
        b.place(x, y, r=7, orange=k in (4, 7, 8, 9, 10, 11, 13), tag="bird")
    wx, wy, wr = b.features["whirlpool"]
    for k in range(8):
        a = math.radians(45 * k)
        b.peg(wx + wr * math.cos(a), wy + wr * math.sin(a), r=9, orange=k % 4 == 2, tag="whirlpool",
              move={"kind": "orbit", "x": wx, "y": wy, "period": 20, "clockwise": True})
    b.trace("mermaid", spacing=32, r=9, tag="mermaid")
    for (x, y) in b.f("harp"):
        b.place(x, y - 4, r=9, orange=True, green=False, tag="harp")
    for k, (x, y) in enumerate(b.f("wrecks")):
        b.place(x, y, r=9, orange=k in (1, 2), tag="wreck")
    for (x, y) in ((120, 430), (250, 400), (330, 470), (90, 470)):
        b.place(x, y, r=8, tag="wreck")
    b.trace("current", spacing=32, r=8, orange={6}, tag="current")
    for (x, y) in b.f("islets"):
        b.place(x, y, r=9, tag="islet")
    # the open sea: pairs of moons, one of each a candidate, and the chart's engraved swell marks as short arcs of
    # brick, concave down (no rhumb lines: they crossed the wonders without reading as anything, critic L8)
    for (x, y), (u, v) in (((300, 400), (331, 418)), ((430, 420), (466, 420)), ((240, 300), (204, 300)),
                           ((360, 230), (396, 230)), ((600, 300), (636, 300)), ((160, 370), (160, 406)),
                           ((700, 420), (700, 456))):
        b.place(x, y, r=8, orange=True, tag="open sea")
        b.place(u, v, r=8, orange=(u, v) == (331, 418), tag="open sea")
    for (x, y) in SWELL:
        b.arc(x, y + 30, 30, 235, 70, t=10, tag="swell mark")
    b.place(300, 140, r=8, tag="open sea")     # a blue star in the open sky, last so the deal stays: it hardens 2-3 by
    # about 0.7 per 48 on the 3456-game tuning block (seeds 20001-23456), keeping 2-2 -> 2-3 a step
    b.greens_in_reach()


SWELL = [(120, 260), (210, 330), (290, 310), (420, 280), (460, 350), (350, 380), (380, 430), (200, 480),
         (640, 150), (460, 230)]
