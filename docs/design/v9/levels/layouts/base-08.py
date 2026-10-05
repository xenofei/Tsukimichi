"""2-3 "The Kraken's Sea" (Vesper Bay, the twins): a set of points, with an orbit.

Subject: the Rhotano Sea the Vesper Bay ferry sails, as the game's own world painting "The Three Great Continents"
draws its wonders: a kraken wrapped round a ship, a galleon under full sail, a flock of birds in a long skein, a
whirlpool, a mermaid with her harp, and a field of wrecks (read as a chart under the moon).

Technique: each wonder is marked as the chart marks it. The kraken is ringed by nine moons; the galleon's hull is a
dotted line (it hangs, so it is never brick) with a moon at each masthead; every bird of the skein is a small moon, a
set of points like a constellation; the whirlpool is an orbit, a ring of eight moons turning slowly round its eye (an
engine orbit mover); the mermaid is traced down her side with a moon on the harp's crown; the wrecks' masts are moons
in the field of wrecks. The kraken's ring, the mastheads and the harp are oranges that are never green.
"""
LEVEL = dict(id="base-08", name="The Kraken's Sea", stage=2, number=8, scene="rhotano-wonders",
             subject="the sea's wonders on the world chart: kraken, galleon, birds, whirlpool, mermaid, wrecks",
             technique="a set of points, with an orbit (the whirlpool)")


def build(b):
    import math
    cx, cy, R, _, _ = b.circle("kraken")
    for k in range(9):
        a = math.radians(-90 + 40 * k)
        b.place(cx + R * math.cos(a), cy + R * math.sin(a), r=9, orange=k in (2, 3, 4, 5), green=False, tag="kraken")
    b.place(cx, cy + 6, r=11, orange=True, green=False, tag="kraken's eye")
    b.trace("galleon hull", spacing=34, r=9, orange={0, 2}, tag="galleon hull")
    for (x, y) in b.f("galleon masts"):
        b.place(x, y, r=9, orange=True, green=False, tag="masthead")
    for k, (x, y) in enumerate(b.f("flock")):
        b.place(x, y, r=7, orange=k in (7, 8, 9, 10, 11, 13), tag="bird")
    wx, wy, wr = b.features["whirlpool"]
    for k in range(8):
        a = math.radians(45 * k)
        p = b.peg(wx + wr * math.cos(a), wy + wr * math.sin(a), r=9, orange=k % 4 == 2, tag="whirlpool",
                  move={"kind": "orbit", "x": wx, "y": wy, "period": 20, "clockwise": True})
    b.trace("mermaid", spacing=38, r=9, tag="mermaid")
    for (x, y) in b.f("harp"):
        b.place(x, y - 4, r=9, orange=True, green=False, tag="harp")
    for k, (x, y) in enumerate(b.f("wrecks")):
        b.place(x, y, r=9, orange=k in (1, 2), tag="wreck")
    b.trace("current", spacing=38, r=8, orange={5}, tag="current")
    # the chart's rhumb lines: straight lines radiating from its compass roses, dotted across the open sea
    b.trace([(250, 250), (520, 520)], spacing=30, r=7, orange={1, 3, 5}, tag="rhumb line")
    b.trace([(480, 150), (250, 540)], spacing=30, r=7, orange={4, 7}, tag="rhumb line")
    for (x, y) in b.f("islets"):
        b.place(x, y, r=9, tag="islet")
    for (x, y) in ((280, 210), (300, 400), (430, 420), (450, 480), (240, 300), (680, 270), (130, 300), (360, 230),
                   (470, 250), (160, 370), (600, 300), (700, 420)):
        b.place(x, y, r=8, orange=(x, y) in ((300, 400), (430, 420), (240, 300), (360, 230), (600, 300), (160, 370), (700, 420)), tag="open sea")
    b.greens_in_reach()
