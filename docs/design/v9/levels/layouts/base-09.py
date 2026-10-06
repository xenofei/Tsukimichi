"""2-4 "The Ferry Under Sail" (Vesper Bay, the twins): a vessel's rigging over a moving sea.

Subject: the Vesper Bay ferry mid-strait by night under full sail, bound for Limsa Lominsa, the far coasts low on either
side (our own painting, painters/ferry.py; it replaces round 1's chart of the Strait of Merlthor, which is parked for
stage 5, Limsa's own waters).

Technique: the ship is drawn the way a child draws one, by its lines. The two masts are vertical runs of moons; a moon
hangs at each yardarm's tip and a pennant flies at each masthead (never green); the jib's stay runs dotted from the
foremast to the bowsprit; the gunwale is a dotted line from the stern lantern to the bow lantern (both never green).
Below the hull the swell moves: two rows of moons slide east and back on the engine's clock (slide movers), each
broken into runs so they read as waves, the lower row above the bucket's lane; the wake trails dotted from the stern
and the bow throws a little spray (both low, so blue). The far coasts on the horizon are light dotted lines and a few
stars stand over them.
"""
LEVEL = dict(id="base-09", name="The Ferry Under Sail", stage=2, number=9, scene="ferry-under-sail",
             subject="the Vesper Bay ferry under full sail in the strait at night",
             technique="a vessel's rigging over a moving sea (slide movers)")


def build(b):
    # the pennants and the yardarm tips: the ship's own points
    for (x, y) in b.f("pennants"):
        b.place(x, y, r=9, orange=True, green=False, tag="pennant")
    for (x, y, h) in b.features["yards"]:
        for side in (-1, 1):
            b.place(x + side * (h + 6), y - 10, r=9, orange=True, green=False, tag="yardarm")
    for (x, y) in b.f("lanterns"):
        b.place(x, y, r=9, orange=True, green=False, tag="lantern")
    # the masts: vertical runs through the sails (blue: the yardarms carry the rig's oranges)
    for (x, top, foot) in b.features["masts"]:
        b.trace([(x, top + 34), (x, foot - 20)], spacing=34, r=8, orange={2}, tag="mast")
    b.trace("stay", spacing=34, r=8, start=70, end_trim=40, tag="stay")
    b.trace("deck", spacing=36, r=9, start=40, end_trim=20, orange={1, 5}, tag="gunwale")
    b.trace("wake", spacing=40, r=8, tag="wake")                         # low: blue
    b.trace("west coast", spacing=40, r=8, start=40, orange={0, 2, 4}, tag="coast")
    b.trace("east coast", spacing=40, r=8, start=100, end_trim=60, orange=True, tag="coast")
    # a few stars in the open sky either side of the rig, and the spray at the bow and the stern
    for (x, y) in ((250, 270), (130, 250), (620, 190), (650, 260), (700, 380), (300, 140), (560, 120), (608, 270)):
        b.place(x, y, r=8, orange=True, tag="star")
    for (x, y) in ((200, 200), (100, 390), (690, 430)):
        b.place(x, y, r=8, tag="star")
    for (x, y) in ((646, 404), (672, 432), (206, 404)):
        b.place(x, y, r=8, orange=(x, y) == (206, 404), tag="spray")      # the bow's spray is low: blue (GD G3)
    # the swell: rows sliding east and back (one drift each row, so a row keeps its spacing), each broken into runs
    # under the hull's ends and the open sea so they read as waves, not a fence (critic L13)
    for (y, runs, dx, period) in ((468, ((258, 420), (528, 690)), 30, 7.0), (500, ((150, 330), (420, 520), (600, 680)), -30, 9.0)):
        for (x0, x1) in runs:
            for x in range(x0, x1 + 1, 46):
                b.slide(x, y, dx, 0, period, r=8, tag="swell")
    for (x, y) in ((140, 290), (150, 150), (650, 150), (700, 200)):       # stars over the coasts, blue (placed last)
        b.place(x, y, r=8, tag="star")
    b.greens_in_reach()
