"""2-4 "The Ferry Under Sail" (Vesper Bay, the twins): a vessel's rigging over a moving sea.

Subject: the Vesper Bay ferry mid-strait by night under full sail, bound for Limsa Lominsa, the far coasts low on either
side (our own painting, painters/ferry.py; it replaces round 1's chart of the Strait of Merlthor, which is parked for
stage 5, Limsa's own waters).

Technique: the ship is drawn the way a child draws one, by its lines. The two masts are vertical runs of moons; a moon
hangs at each yardarm's tip and a pennant flies at each masthead (never green); the jib's stay runs dotted from the
foremast to the bowsprit; the gunwale is a dotted line from the stern lantern to the bow lantern (both never green).
Below the hull the swell moves: two rows of moons slide east and back on the engine's clock (slide movers), the lower
row above the bucket's lane, and the wake trails dotted from the stern. The far coasts on the horizon are light dotted
lines.
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
    b.trace("wake", spacing=40, r=8, orange={0, 2}, tag="wake")
    b.trace("west coast", spacing=40, r=8, start=40, orange={0, 2, 4}, tag="coast")
    b.trace("east coast", spacing=40, r=8, start=100, end_trim=60, orange={0, 1}, tag="coast")
    # a few stars in the open sky either side of the rig, and the spray at the bow and the stern
    for (x, y) in ((250, 270), (130, 250), (620, 190), (650, 260), (700, 380)):
        b.place(x, y, r=8, orange=True, tag="star")
    for (x, y) in ((300, 140), (560, 120), (608, 270), (200, 200), (100, 390)):
        b.place(x, y, r=8, tag="star")
    for (x, y) in ((646, 404), (672, 432), (206, 404)):
        b.place(x, y, r=8, orange=(x, y) != (646, 404), tag="spray")
    # the swell: two rows sliding east and back (one drift each row, so a row keeps its spacing)
    for (y, x0, x1, dx, period) in ((468, 258, 690, 30, 7.0), (500, 150, 680, -30, 9.0)):
        for x in range(x0, x1 + 1, 54):
            b.slide(x, y, dx, 0, period, r=8, tag="swell")
    b.place(140, 290, r=8, tag="star")       # (placed last: a star over the west coast)
    b.greens_in_reach()
