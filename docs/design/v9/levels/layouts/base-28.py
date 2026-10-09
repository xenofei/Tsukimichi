"""6-3 "The Seer's Chart" (The Silent Stars, Urianger): a set of points.

Subject: Eorzea's cities read as stars on the game's own world map (the Adventure map's painting), joined by the ley
lines Urianger reads: Mor Dhona at the heart, a line to each city, and the cities joined round the rim.

Technique: each city is a ring of moons round its shield (Gridania six at 36, clear of its banner; Mor Dhona, the
heart, seven at 40), and a small star of light sits where its ley lines meet. The ley lines are drawn on the chart
(the scene's overlays, where the runtime's glints run) and dotted with small moons (r 8) wherever a line is long enough
to hold one clear of the rings; the spokes from the heart run on past Ishgard, Gridania, Ala Mhigo and Limsa as rays
out to sea. Round them, the chart's own figures: a moon on each flying dragon and each ship, a ring of four round the
kraken and the southern compass rose, a moon on the eastern rose's heart, a brick crown on Coerthas's peak, and the
cloud sea's edge dotted.

Candidates (30, the charter's count): the cities' upper moons and the heart's ring, the ley lines, the rays, and the
chart's figures, kept to the spread rule (10 per 200 x 200; Limsa's two side moons and the eastern ship are the
candidates in y 400-430). Ul'dah's ring stays blue: its upper moons sit at y 422 and were left in 22-27% of lost games
(MR 3 round 1, m3), so the city's star is carried by the lines alone. Tuned on the tuning seeds and the second block
(seeds 20001-23456): 22.7 and 21.4 per 48 against the target of 22; held out 23.3 (the three blocks pooled, 22.1).
"""

LEVEL = dict(id="base-28", name="The Seer's Chart", stage=6, number=28, scene="seers-chart",
             subject="Eorzea's cities as stars joined by the ley lines Urianger reads",
             technique="a set of points (each city a ring stop) joined by dotted ley lines")

CITIES = ("ishgard", "gridania", "ala mhigo", "mor dhona", "uldah", "limsa")
LINES = ("ley ishgard", "ley gridania", "ley ala mhigo", "ley uldah", "ley limsa",
         "rim north", "rim east", "rim south", "rim west")
START = {"ishgard": -54, "uldah": -54}
RADIUS = {"mor dhona": 40, "gridania": 36, "limsa": 34}
COUNT = {"mor dhona": 7, "gridania": 6}
RAYS = ("ray ishgard", "ray gridania", "ray ala mhigo", "ray limsa")


def build(b):
    for name in CITIES:
        (x, y), = b.f(name)
        n, R = COUNT.get(name, 5), RADIUS.get(name, 30)
        b.stop(x, y, R=R, n=n, r=9, oranges=(), start_deg=START.get(name, -90), tag=name)
    # a line's moons start clear of the rings at its ends (ring radius + the ring moon's 9 + a dotted gap + 8)
    ring = {tuple(b.f(c)[0]): RADIUS.get(c, 30) + 31 for c in CITIES}
    for name in LINES:
        pts = b.f(name)
        b.trace(name, spacing=32, r=8, start=ring[pts[0]], end_trim=ring[pts[-1]], tag=name)
    for name in RAYS:
        b.trace(name, spacing=32, r=8, start=ring[b.f(name)[0]], tag=name)
    for (x, y) in b.f("dragons"):
        b.place(x, y, r=9, tag="dragon")
    for (x, y) in b.f("ships"):
        b.place(x, y, r=9, tag="ship")
    (x, y), = b.f("kraken")
    b.stop(x, y, R=26, n=4, r=9, oranges=(), start_deg=-45, tag="kraken")
    (x, y), = b.f("compass")
    b.place(x, y, r=9, tag="compass")
    (x, y), = b.f("south compass")
    b.stop(x, y, R=24, n=4, r=8, oranges=(), start_deg=-45, green=True, tag="south compass")
    (px, py), = b.f("peak")
    b.arc_bricks(px, py + 26, 28, 215, 110, n=1, tag="peak", green=False)
    b.trace("cloud edge", spacing=34, r=8, tag="cloud edge")
    for p in b.pegs:
        p["canBeOrange"] = (round(p["x"]), round(p["y"])) in CANDS
    b.greens_in_reach()


CANDS = {(314, 172), (350, 172), (360, 205),                         # Ishgard
         (472, 236),                                                 # Gridania
         (559, 257), (588, 278), (530, 278),                         # Ala Mhigo
         (392, 312), (361, 327), (431, 361), (409, 388), (353, 361),  # Mor Dhona, the heart
         (148, 410), (212, 410), (180, 386),                         # Limsa Lominsa
         (288, 260), (264, 393), (488, 315),                         # the ley lines
         (311, 139), (501, 138), (646, 253), (676, 242),             # the rays beyond the cities
         (200, 150), (105, 302), (574, 422), (662, 342), (698, 342),  # the chart's dragon, ships and kraken
         (582, 204), (694, 280), (610, 334)}                         # the cloud sea's edge and the compass rose
