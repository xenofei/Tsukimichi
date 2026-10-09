"""5-2 "The Strait of Merlthor" (Harbour Lights, Merlwyb): a trail on a map.

Subject: the ferry's crossing on the game's own world painting "The Three Great Continents" (its south-west: Vylbrand,
the Strait of Merlthor and the west of Aldenard under the moon). The road runs from Ul'dah to Vesper Bay on Thanalan's
western cape, and the ferry crosses the strait's mouth to Limsa Lominsa; the two coasts the strait runs between,
Vylbrand's east shore and Aldenard's west shore (below the launcher's swing), are dotted either side of it.

Technique: each port is a ring of moons (Limsa Lominsa round its spires, Ul'dah round its domes, a smaller ring of five
on Vesper Bay's cape), and the crossing, the road from Ul'dah and the road south-west out of Ul'dah's gate are dotted
with small moons over the gilt routes engraved on the chart (the scene's overlays, where the runtime's glints run). The
chart round them: both coasts of the strait and Vylbrand's west shore, Thanalan's coast south to Ul'dah's cape, the bay
east of Ul'dah, the eastern lobe's coast to the north, the river under the Shroud, the Rhotano Sea's isles (a crown of
brick on the long island), the eastern isle and the galleon, the two compass roses as rings, a crown on Vylbrand's snowy
peak and two on the Sagolii's ridges.

Candidates (30, the charter's count): the ports' upper moons, the crossing and the roads, both shores of the strait, the
eastern lobe and the river, the galleon, the compass's top moon, and three orange-able crowns (the two ridges and the
Rhotano island): the board's other places held its oranges too long (tuning 14-19 per 48 with pegs only; the crowns are
broad and sit in the open lanes). The spread rule (10 per 200 x 200, bricks by their midpoints) binds in the middle and
the north-east, so the Ul'dah and Vesper rings keep their lower moons blue. The low pair for the rule's y 400 clause is
the road south's two moons, in the lanes either side of Ul'dah's ring where balls fall (the Thanalan coast's (325, 401)
and Ul'dah's bay's (479, 403) were left in about 0.3 of lost games). The Vylbrand peak's crown stays blue (as a
candidate it was left in 0.52 of lost games). Accepted on the tuning seeds: 1-864 read 23.8 and the second block
(20001-23456) 24.5, pooled 24.4 against the charter's 24.5; the (397, 315) variant read 25.3 / 25.8 and the (134, 174)
one 24.1 / 23.8. Held out 23.5.
"""

LEVEL = dict(id="base-22", name="The Strait of Merlthor", stage=5, number=22, scene="merlthor-strait",
             subject="the ferry's crossing between two dotted coasts: from Ul'dah by Vesper Bay over the strait's "
                     "mouth to Limsa Lominsa",
             technique="a trail on a map")


def build(b):
    # the ports: rings of moons, never green
    (x, y), = b.f("limsa lominsa")
    b.stop(x, y, R=34, n=6, r=9, oranges=(), tag="limsa lominsa")
    (x, y), = b.f("uldah")
    b.stop(x, y, R=34, n=6, r=9, oranges=(), tag="uldah")
    (x, y), = b.f("vesper bay")
    b.stop(x, y, R=26, n=5, r=8, oranges=(), tag="vesper bay")
    # the crossing and the roads
    b.trace("ferry lane", spacing=32, r=8, start=34, end_trim=34, tag="lane")
    b.trace("road", spacing=30, r=8, start=40, end_trim=30, tag="road")
    # the strait's two coasts, and the coasts round them
    b.trace("vylbrand coast", spacing=34, r=8, tag="vylbrand coast")
    b.trace("vylbrand west", spacing=34, r=8, tag="vylbrand west")
    b.trace("aldenard coast", spacing=34, r=8, tag="aldenard coast")
    b.trace("thanalan coast", spacing=34, r=8, start=20, tag="thanalan coast")
    b.trace("uldah bay", spacing=34, r=8, start=6, tag="uldah bay")
    b.trace("east lobe", spacing=34, r=8, tag="east lobe")
    b.trace("river", spacing=34, r=8, tag="river")
    for (x, y) in b.f("road south"):
        b.place(x, y, r=8, tag="road south")
    # the chart round the crossing
    for name in ("rhotano isles", "east isles", "galleon"):
        for (x, y) in b.f(name):
            b.place(x, y, r=9, tag=name)
    for name in ("compass", "south compass"):
        (cx, cy), = b.f(name)
        b.stop(cx, cy, R=34, n=6, r=8, oranges=(), tag=name)
    # bricks: crowns on Vylbrand's peak, the long Rhotano island and the Sagolii's ridges
    for (x, y) in b.f("vylbrand peaks"):
        b.arc_bricks(x, y + 26, 28, 215, 110, n=1, tag="vylbrand peak", green=False)
    for (x, y) in b.f("rhotano island"):
        b.arc_bricks(x, y + 24, 30, 220, 100, n=1, tag="rhotano island", orange=True)
    for (x, y) in b.f("sagolii ridges"):
        b.arc_bricks(x, y + 24, 26, 220, 100, n=1, tag="ridge", orange=True)
    # the candidates (see the docstring): set last, by place
    for p in b.pegs:
        p["canBeOrange"] = (round(p["x"]), round(p["y"])) in CANDS
    b.greens_in_reach()


CANDS = {(134, 242), (163, 191), (163, 225),                           # Limsa Lominsa
         (316, 242), (341, 260),                                       # Vesper Bay
         (397, 281), (426, 264), (455, 281), (397, 315),               # Ul'dah
         (192, 251), (220, 265), (251, 273), (362, 284),               # the crossing and the road
         (384, 404), (418, 408),                                       # the road south
         (258, 189), (283, 166),                                       # Vylbrand's shore of the strait
         (346, 186),                                                   # Aldenard's shore
         (612, 292), (639, 273), (657, 244), (668, 184),               # the eastern lobe
         (506, 160), (570, 154), (603, 146),                           # the river
         (694, 298),                                                   # the galleon
         (610, 373)}                                                   # the compass rose
