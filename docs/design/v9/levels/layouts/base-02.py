"""1-2 "Horizon by Night" (The Waking Sands, Minfilia): a silhouette band.

Subject: Horizon, the Western Thanalan town on its mesa where the road from the Waking Sands first climbs: its flat
sandstone roofs, the great water tower on its stilts and the mine's derrick against the stars (our own painting,
painters/horizon.py). It replaces round 1's Ul'dah skyline, which took stage 4's home and repeated 1-4's painting.

Technique: the skyline is traced along its roofline as one continuous band of moons 18 units above every roof block.
The structures are brick (the Peggle refit, 9 October 2026: a dense board): the derrick's two timber
legs meet in an A over its wheel, the water tower is drawn in brick from its domed crown down its sides and outer stilts
(the crown never green, a moon on its finial), and the Milky Way's arch rides over the town in two runs of arc brick,
parted where the launcher swings. Below the rim the cliff's two switchback roads, west and east, are laid in brick along
their legs (open at the hairpins), the cliff's lit edge and a lower ledge are dotted between them, and the caravan's
three campfires sit at the foot. Stars stand over and under the arch, and lit windows and lanterns hang in the town.

Candidates (author.candidates): every piece but the sure greens (the stars and roofline moons straight under the launcher
and the arch's runs nearest it, which a first shot clears most readily: `ease`), the pieces no first flight touches and
the lowest, held to the lower-third share. Tuned on seeds 20001-23456 to 9.6-9.8 per 48; held out 9.0 (MR 1's band
7.47-9.67, mean 8.57). About 34% brick.
"""
LEVEL = dict(id="base-02", name="Horizon by Night", stage=1, number=2, scene="horizon-by-night",
             subject="Horizon on its mesa: roofs, the water tower and the derrick under the Milky Way",
             technique="a silhouette band (the roofline traced), its structures in brick")

# the roofline band, 18 units above each roof (34 apart; where two roofs meet, between their heights), parted by the
# derrick and the water tower
BAND = [(100, 334), (134, 334), (270, 322), (304, 330), (338, 340), (372, 314), (406, 314), (440, 332),
        (626, 326), (660, 326), (694, 344)]
# stars over the arch (toward the top corners) and under it (above the town)
STARS_OVER = [(100, 200), (120, 140), (160, 90), (215, 55), (255, 92), (104, 104)]
# left corner only: their mirrors would hang under the Ace chip (top right)
STARS_LEFT = []
# beside the tower and over the arch's shoulders
STARS_EXTRA = [(205, 110), (595, 110), (540, 300), (578, 304)]
STARS_UNDER = [(170, 262), (256, 256), (260, 200), (300, 170), (340, 200), (300, 245), (150, 300), (400, 215),
               (460, 200), (500, 170), (466, 236), (612, 240), (650, 290), (340, 280), (400, 268), (440, 282)]
# the sure greens (never orange): the stars straight under the launcher and the middle of the roofline, the moons a
# first shot clears most readily, so the deal falls on the town's edges, its windows and the cliff (the 1-2 refit's
# difficulty lever, measured with `ease`)
SURE_GREENS = [(300, 170), (340, 200), (400, 215), (460, 200), (500, 170), (300, 245), (466, 236),
               (338, 340), (372, 314), (406, 314), (340, 280), (400, 268), (440, 282), (440, 332), (304, 330),
               (650, 290),
               (273, 125), (225, 150), (182, 182), (527, 125), (575, 150), (618, 182)]  # the arch nearest the launcher
# lit windows and lanterns in the town
WINDOWS = [(110, 378), (288, 378), (330, 380), (382, 362), (420, 380), (652, 380), (690, 378)]
LANTERNS = [(463, 374), (616, 372)]
TOWER_IN = [(540, 345), (540, 384)]
DERRICK_APEX = (214, 262)


def mirror(pts):
    return [(800 - x, y) for (x, y) in pts]


def build(b):
    # ---- brick first, so every moon keeps its distance from it
    # the Milky Way's arch: two runs of arc brick on its crest, parted where the launcher swings
    cx, cy, R, _a0, _sw = b.circle("milky way")
    b.arc_bricks(cx, cy, R, 205, 47, n=5, tag="milky way")
    b.arc_bricks(cx, cy, R, 288, 47, n=5, tag="milky way")
    # the derrick's timber legs, an A over its wheel
    # (a long upper brick each, so the legs are a ball's width apart before the next pair begins)
    ax, ay = DERRICK_APEX
    for fx in (176, 252):
        L = ((fx - ax) ** 2 + (388 - ay) ** 2) ** 0.5
        at = lambda s: (ax + (fx - ax) * s / L, ay + (388 - ay) * s / L)
        b.line(*at(0), *at(72), tag="derrick legs")
        b.line(*at(74.5), *at(L), tag="derrick legs")
    # the water tower: its domed crown (never green), down the tank's sides and the outer stilts
    tx, ty, tR, t0, tsw = b.circle("tank")
    b.arc_bricks(tx, ty, tR, t0, tsw, n=2, tag="tank crown", green=False)
    for side in ([(496, 340), (500, 388)], [(584, 340), (580, 388)]):
        b.bricks_along(side, length=36, tag="tower stilts")
    # the switchback roads up the cliff, brick along both legs of each, open at the hairpins
    for road in ("road west", "road east"):
        pts = b.f(road)
        b.bricks_along([pts[0], pts[1]], length=36, trim0=16, trim1=20, tag="switchback road")
        b.bricks_along([pts[4], pts[5]], length=36, trim1=4, tag="switchback road")
    # ---- moons
    b.place(tx, ty - tR - 40, orange=True, green=False, tag="tank finial")
    (dx, _dy), = b.f("derrick")
    b.place(dx, ay - 38, orange=True, green=False, tag="derrick head")
    for (x, y) in BAND:
        b.place(x, y, tag="roofline")
    for (x, y) in TOWER_IN:
        b.place(x, y, tag="tower lamp")
    for (x, y) in LANTERNS:
        b.place(x, y, tag="lantern")
    for (x, y) in WINDOWS:
        b.place(x, y, tag="window")
    # the cliff's lit edge, and a ledge below it, between the roads
    for k in range(7):
        b.place(298 + 34 * k, 424, tag="cliff edge")
    for k in range(4):
        b.place(349 + 34 * k, 462, tag="cliff ledge")
    for (x, y) in b.f("campfires"):
        b.place(x, min(y, 504), tag="campfire")
    for (x, y) in STARS_OVER + mirror(STARS_OVER) + STARS_LEFT + STARS_EXTRA + STARS_UNDER:
        b.place(x, y, tag="star")
    b.greens_in_reach()
    b.candidates(exclude=SURE_GREENS + [(542, 487)])  # (542, 487): the weakest protan brick


# ---- tuning (written by `mfl.py tune`; held out 9.2/48, tuning seeds 8.6/48, band 8.10-9.30)
TUNING = {
    "blue": [[451.0, 462.0], [700.0, 200.0]],
    "orange": [[300.0, 170.0], [340.0, 200.0]]
}
