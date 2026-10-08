"""3-1 "The Airship Road" (The Night Skyway, Cid): a trail on a map.

Subject: the airship's lane on the game's own world painting "The Three Great Continents" (its north-west, Aldenard
under the moon): from Gridania north-west over the Coerthas highlands to Ishgard, then north across the coast past the
chart's perched dragon to the chart's own airship sailing the Northern Sea, with a branch west to the hold on
Coerthas's west coast and the lane in from the south toward Gridania. The cloud sea's western shore, its floating isles
and humps, and the dragons on the wing (one under the launcher, one at the left wall) frame it.

Technique: each city is a ring of six moons (Gridania, Ishgard, the western hold) and the airship's berth is three moons
under its keel, where the lane meets it; the lanes between are dotted with small moons (r 8) over the gilt route
engraved on the chart (the scene's overlays, where the runtime's glints run). The chart around the lane: a moon on each
dragon's wing, the floating isles, a dotted cloud shore and the Aldenard coast, the compass rose's heart, a brick crown
on Coerthas's snowy peak and two on its ridges, and two cloud humps.

Candidates (29, the charter's count): the cities' upper moons, the berth, the lane north over the coast, the dragons,
the isles, the coast and the cloud shore, chosen with `mfl.py ease` on the tuning seeds and kept to the spread rule
(10 per 200 x 200, one at y 400 or lower on each half). Gridania's lower moons and the castle stay blue: they were the
places left most often (`ease`, 0.14-0.26). Tuning seeds 1-864 and the second block (20001-23456) both read 27.2 per
48 against the opener's target of 27 (charter, MR 3). Stage integration (mr3): the pooled step to 3-2 read 0.88 +- 0.41,
not resolvably 0.5, so the western hold's lower-left moon (163, 367), left in 0.21 of lost games, gave its candidate to
the ring's upper-left moon (163, 333) (`ease` 0.04): tuning 28.4, second block 28.1 (from 27.2 and 27.2). That is inside
`stagecheck`'s easy end for an opener (27 + 1.8, three standard errors; the charter's +-1.2 is the hard side).

MR 3 round 1 (critic m3): the south lane's (410, 421) sat 9 units above the cheap gate's line and was left in 26% of lost
games, so its candidate moved to Gridania's upper-right moon (514, 400), a city stop; the lane keeps (378, 424), the left
half's one candidate below y 400 (the spread rule). Held out 27.1.
"""
import math

LEVEL = dict(id="base-11", name="The Airship Road", stage=3, number=11, scene="aldenard-airship-lane",
             subject="the airship's lane on the old world chart, from Gridania north over Coerthas past the chart's "
                     "own airship and dragons",
             technique="a trail on a map")


def build(b):
    # the cities: a ring of six moons each round its banner, never green
    for name in ("gridania", "ishgard", "western hold"):
        (x, y), = b.f(name)
        b.stop(x, y, R=34, n=6, r=9, oranges=(), tag=name)
    # the airship's berth: three moons under its keel, where the lane meets it
    cx, cy, R, a0, sw = b.circle("airship")
    for k in range(3):
        a = math.radians(a0 + sw * k / 2)
        b.place(cx + R * math.cos(a), cy + R * math.sin(a), r=9, green=False, tag="airship")
    # the lanes
    b.trace("lane gridania", spacing=32, r=8, start=46, end_trim=46, tag="lane")
    b.trace("lane north", spacing=32, r=8, start=46, end_trim=10, tag="lane")
    b.trace("lane west", spacing=32, r=8, start=46, end_trim=46, tag="lane west")
    b.trace("lane south", spacing=32, r=8, start=10, tag="lane south")
    # the chart round the lane
    for (x, y) in b.f("dragon"):
        b.place(x, y, r=9, green=False, tag="dragon")
    for (x, y) in b.f("flying dragon"):
        b.place(x, y, r=8, tag="flying dragon")
    for (x, y) in b.f("isles"):
        b.place(x, y, r=9, tag="floating isle")
    b.trace("cloud shore", spacing=34, r=8, tag="cloud shore")
    b.trace("north coast", spacing=34, r=8, tag="north coast")
    b.trace("west coast", spacing=34, r=8, tag="west coast")      # every first shot meets the chart (no dead aim)
    (cx, cy), = b.f("compass")
    b.place(cx, cy, r=9, green=False, tag="compass")
    for (x, y) in b.f("gridania castle"):
        b.place(x, y, r=9, tag="gridania castle")
    # Coerthas: a crown of brick on the snowy peak, and its ridges toward the cloud sea
    (px, py), = b.f("peak")
    b.arc_bricks(px, py + 26, 28, 215, 110, n=1, tag="peak", green=False)
    for (x, y) in b.f("ridges"):
        b.arc_bricks(x, y + 20, 24, 225, 90, n=1, tag="ridge")
    # the cloud sea's humps, east of its shore
    for (x, y) in b.f("cloud humps"):
        b.arc_bricks(x, y + 24, 28, 220, 100, n=1, tag="cloud hump")
    # the candidates (see the docstring): set last, by place
    for p in b.pegs:
        p["canBeOrange"] = (round(p["x"]), round(p["y"])) in CANDS
    b.greens_in_reach()


CANDS = {(365, 288), (394, 305), (336, 305),                          # Ishgard
         (485, 383), (456, 400), (514, 400),                           # Gridania
         (192, 316), (221, 367), (163, 333),                           # the western hold
         (222, 148), (255, 160), (288, 148),                           # the airship's berth
         (207, 222), (276, 252), (110, 272),                           # the dragons
         (472, 168), (422, 228), (528, 216),                           # the floating isles
         (290, 202), (308, 229), (262, 353), (378, 424),               # the lanes
         (468, 281), (534, 291),                                       # the Aldenard coast
         (566, 160), (561, 194), (558, 227), (572, 258), (591, 286)}   # the cloud sea's shore
