"""2-2 "Moonpath on the Bay" (Vesper Bay, the twins): light and shadow.

Subject: the moon's road of light on the sea at Vesper Bay, from the horizon to the shore (our own painting,
painters/moonpath.py), with the pier and its lamps, a moored ferry and the town on the shore.

Technique: the moons follow the light. Rows of moons lie on the glints of the moonpath, one at the horizon and widening
toward the shore, with a fainter glint either side of each wide row (never green: they are the subject); the upper rows
carry oranges and the two shore rows are blue, out of the bucket's approach. The pier's deck is dotted, a moon on each
of its three lamps and its pilings dotted below; the ferry's masts are short vertical runs; the town's roofline and the
headland are dotted lines on the horizon, and a few stars stand in the rose dusk; the swell either side of the road is
a scatter of wave crests (short arcs of brick, concave down, all above y 490) in the dark water.
"""
LEVEL = dict(id="base-07", name="Moonpath on the Bay", stage=2, number=7, scene="vesper-moonpath",
             subject="the moon's road of light on the sea at Vesper Bay",
             technique="light and shadow (pegs on the moon's glints)")


def build(b):
    path = b.features["moonpath"]
    axis = path["axis"]
    for (y, n, sp) in path["rows"]:
        for k in range(n):
            x = axis + (k - (n - 1) / 2) * sp
            # the two shore rows are blue: an orange in the bucket's approach decided lost games (game designer m4)
            o = False if y > 460 else (n < 3 or k != 1)
            b.place(x, y, r=9, orange=o, green=False, tag="moonpath")
        if n > 1:
            # the road widens toward the shore: a fainter glint either side, blue
            for side in (-1, 1):
                b.place(axis + side * (n + 1) / 2 * sp, y, r=8, tag="glint")
    b.trace("pier deck", spacing=36, r=9, tag="pier deck")
    for (x, y) in b.f("lamps"):
        b.place(x, y - 14, r=9, orange=True, tag="pier lamp")      # (above the painted lamp: clear of the deck)
    for (x, foot) in ((566, 520), (608, 520), (650, 520), (692, 520)):
        b.trace([(x, 468), (x, foot)], spacing=34, r=8, tag="piling")
    for (x, top, foot) in b.features["masts"]:
        b.trace([(x, top + 24), (x, foot - 30)], spacing=36, r=8, orange={1}, tag="mast")
    b.trace("town", spacing=36, r=9, orange={1, 2, 4, 5}, tag="town")
    b.trace([(100, 262), (170, 286)], spacing=36, r=9, orange={0, 1}, tag="headland")
    b.trace([(196, 306), (250, 306)], spacing=34, r=8, tag="horizon")
    b.trace([(340, 306), (520, 306)], spacing=40, r=8, orange={1, 3}, tag="horizon")
    # the swell: wave crests in the dark water either side of the road, all above y 490 and clear of the road's moons
    for (x, y, o) in ((140, 390, True), (140, 452, False), (130, 505, False), (420, 404, False), (446, 470, True),
                      (500, 490, False)):
        b.arc(x, y + 30, 30, 235, 70, t=10, orange=o, tag="wave crest")
    for (x, y) in ((110, 430), (470, 430), (380, 340), (520, 380), (100, 340), (200, 350), (496, 350),
                   (440, 520)):
        b.place(x, y, r=8, orange=(x, y) in ((100, 340), (520, 380), (200, 350), (470, 430)), tag="swell")
    # a few stars, most of them candidates; the rest of the sky is the painting's
    for (x, y) in ((580, 190), (540, 236), (180, 210), (230, 240), (330, 240), (288, 262), (450, 200), (620, 150)):
        b.place(x, y, r=8, orange=(x, y) in ((580, 190), (540, 236), (180, 210), (330, 240)), tag="star")
    b.greens_in_reach()
