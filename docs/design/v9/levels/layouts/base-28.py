"""6-3 "The Seer's Chart" (The Silent Stars, Urianger; set nowhere): a set of points.

Subject: Eorzea's cities as stars on the game's own world map (`ui/map/world/01/world01_m.tex`, the Adventure map's
painting, read from the install at runtime), joined by the ley lines Urianger reads. Each city is a star: a small light
at its banner (the dress's lights) ringed by five moons (Ishgard, Gridania, Ala Mhigo, Ul'dah, Limsa Lominsa), and
Mor Dhona, where the lines meet, a ring of seven round its lake. The ley lines run from Mor Dhona to the far cities
and round the five as one loop; each is engraved on the chart in pale lilac (the scene's overlays, where the runtime's
glints run) and dotted with small moons where it is long enough to hold them (Mor Dhona's lines to Gridania and
Ul'dah are only engraved: the rings all but touch there).

Technique (SP): a set of points, each a ring stop, joined by dotted lines. The chart round them carries the rest:
a brick crown on Coerthas's snowy peak, the wing of the dragon over the Northern Sea and the compass rose under it,
the compass rose and the kraken in the eastern sea, the ship off the coast, the isle south of Ala Mhigo, and three
brick humps on the cloud sea's billows (the sea-green second jewel). The isle, the kraken and the middle cloud hump
stand in the lanes that the first sweep found dead (every first shot now meets the chart).

Candidates (30, the charter's count): `mfl.py ease --pick 30` on the tuning seeds, among the cities' rings, the ley
lines and the chart's places, kept to the spread rule (10 per 200 x 200, which the dense middle of the map binds, so
the eastern sea's rose, kraken and ship carry candidates; the isle (536, 400) is the right half's one at y 400-430),
then one Mor Dhona moon (346, 310) gave its candidate to the west ley line's (214, 306) to bring the ramp to the
target: tuning seeds 1-864 22.3 and the second block (20001-23456) 22.6 per 48, against the charter's 22. Held out
22.3. No candidate is at y 430 or lower.
"""
import math

LEVEL = dict(id="base-28", name="The Seer's Chart", stage=6, number=28, scene="seers-chart",
             subject="Eorzea's cities as stars on the world map, joined by the ley lines Urianger reads",
             technique="a set of points (each city a ring of moons round its star) joined by dotted ley lines")

# (feature, ring radius, moons, first moon's angle)
CITIES = (("ishgard", 28, 5, -90), ("gridania", 28, 5, -90), ("ala mhigo", 28, 5, -90),
          ("uldah", 28, 5, -90), ("limsa", 28, 5, -90))
LEYS = ("ley limsa", "ley ishgard", "ley ala mhigo", "ley west", "ley north", "ley east", "ley south-east",
        "ley south")


def build(b):
    # Mor Dhona, where the ley lines meet: a ring of seven moons round the lake
    (x, y), = b.f("mor dhona")
    b.stop(x, y, R=36, n=7, r=8, oranges=(), start_deg=-90, tag="mor dhona")
    # the cities: a ring of five moons round each city's star
    for (name, R, n, a0) in CITIES:
        (x, y), = b.f(name)
        b.stop(x, y, R=R, n=n, r=8, oranges=(), start_deg=a0, tag=name)
    # the ley lines between them, dotted where they are long enough
    for name in LEYS:
        b.trace(name, spacing=28, r=7, start=50, end_trim=50, tag=name)
    # the chart round them
    (px, py), = b.f("peak")
    b.arc_bricks(px, py + 26, 28, 215, 110, n=1, tag="peak", green=False)
    for (x, y) in b.f("dragon"):
        b.place(x, y, r=9, tag="dragon")
    for (cx, cy) in b.f("compass"):
        for k in range(4):
            a = math.radians(90 * k - 90)
            b.place(cx + 20 * math.cos(a), cy + 20 * math.sin(a), r=7, tag="compass")
    for (x, y) in b.f("cloud humps"):
        b.arc_bricks(x, y + 24, 28, 220, 100, n=1, tag="cloud hump")
    for (x, y) in b.f("kraken") + b.f("isles"):
        b.place(x, y, r=9, tag="sea")
    for (x, y) in b.f("ships"):
        b.place(x, y, r=9, tag="ship")
    # the candidates (see the docstring): set last, by place
    for p in b.pegs:
        p["canBeOrange"] = (round(p["x"]), round(p["y"])) in CANDS
    b.greens_in_reach()


CANDS = {(275, 183), (302, 164), (329, 183), (318, 215), (286, 215),            # Ishgard
         (414, 234), (387, 253),                                                 # Gridania
         (540, 248), (567, 267), (513, 267), (556, 299), (524, 299),             # Ala Mhigo
         (338, 428),                                                             # Ul'dah
         (130, 388), (157, 407), (103, 407),                                     # Limsa Lominsa
         (362, 378),                                                             # Mor Dhona
         (317, 244), (348, 221), (476, 299),                                     # the ley lines north and east
         (160, 376), (179, 352), (214, 306), (190, 397), (218, 387),             # the ley lines west and south-west
         (536, 400), (570, 343), (590, 323), (660, 358), (700, 300)}             # the isle, the rose, kraken, ship
