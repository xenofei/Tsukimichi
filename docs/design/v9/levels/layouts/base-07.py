"""2-2 "Moonpath on the Bay" (Vesper Bay, the twins): light and shadow.

Subject: the moon's road of light on the sea at Vesper Bay, from the horizon to the shore (our own painting,
painters/moonpath.py), with the pier and its lamps, a moored ferry and the town on the shore.

Technique: the moons follow the light. Rows of moons lie on the glints of the moonpath, one at the horizon and widening
toward the shore, and they carry most of the oranges (never green: they are the subject). The pier is a deck of brick
with a moon on each of its three lamps; the ferry's masts are short vertical runs; the town's roofline and the
headland are dotted lines on the horizon; the swell either side of the road is a scatter of wave crests (short arcs of
brick, concave down) in the dark water.
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
            b.place(x, y, r=9, orange=n < 3 or (k == 1 if y < 460 or y > 500 else k != 1), green=False, tag="moonpath")
    b.trace("pier deck", spacing=36, r=9, tag="pier deck")
    for (x, y) in b.f("lamps"):
        b.place(x, y - 14, r=9, orange=True, tag="pier lamp")      # (above the painted lamp: clear of the deck)
    for (x, top, foot) in b.features["masts"]:
        b.trace([(x, top + 24), (x, foot - 30)], spacing=36, r=8, orange={1}, tag="mast")
    b.trace("town", spacing=36, r=9, orange={1, 2, 4, 5}, tag="town")
    b.trace([(100, 262), (170, 286)], spacing=36, r=9, orange={1}, tag="headland")
    b.trace([(220, 306), (250, 306)], spacing=34, r=8, tag="horizon")
    b.trace([(340, 306), (520, 306)], spacing=40, r=8, orange={1, 3}, tag="horizon")
    # the swell: wave crests in the dark water either side of the road
    for (x, y, o) in ((140, 390, True), (210, 446, False), (120, 488, False), (196, 518, False), (400, 400, False),
                      (444, 470, False), (500, 520, False), (560, 470, True), (650, 504, True)):
        b.arc(x, y + 30, 30, 235, 70, t=10, orange=o, tag="wave crest")
    for (x, y) in ((110, 430), (250, 400), (160, 456), (470, 430), (620, 470), (250, 486), (530, 400), (380, 340)):
        b.place(x, y, r=8, orange=(x, y) == (620, 470), tag="swell")
    for k, (x, y) in enumerate(((500, 150), (580, 190), (650, 140), (690, 210), (540, 236), (180, 210), (230, 240))):
        b.place(x, y, r=8, orange=k in (1, 4), tag="star")
    b.greens_in_reach()
