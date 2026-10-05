"""1-2 "Horizon by Night" (The Waking Sands, Minfilia): a silhouette band.

Subject: Horizon, the Western Thanalan town on its mesa where the road from the Waking Sands first climbs: its flat
sandstone roofs, the great water tower on its stilts and the mine's derrick against the stars (our own painting,
painters/horizon.py). It replaces round 1's Ul'dah skyline, which took stage 4's home and repeated 1-4's painting.

Technique: the skyline is traced along its roofline, low enough to trace whole: a dotted line of moons rides 18 units
above the roofs, rising over the derrick's head and the tank, whose domed cap wears a crown of brick (never green) with
a moon on its finial. Below the town the road switchbacks down the cliff, dotted, and the cliff's ledges are a light
dotted line; the open sky holds a loose field of stars, a few of them candidates.
"""
LEVEL = dict(id="base-02", name="Horizon by Night", stage=1, number=2, scene="horizon-by-night",
             subject="Horizon on its mesa: roofs, the water tower and the derrick against the stars",
             technique="a silhouette band (the roofline traced)")

STARS = [(130, 200), (180, 150), (260, 210), (330, 170), (300, 260), (470, 190), (610, 170), (660, 230), (700, 290),
         (420, 270), (150, 280), (380, 220), (560, 210), (240, 290), (645, 264)]


def build(b):
    cx, cy, R, a0, sw = b.circle("tank")
    b.arc_bricks(cx, cy, R, a0, sw, n=2, t=12, tag="tank crown", green=False)
    b.place(cx, cy - R - 34, r=9, orange=True, green=False, tag="tank finial")
    (dx, dy), = b.f("derrick")
    b.place(dx, dy - 22, r=9, orange=True, green=False, tag="derrick head")
    # the roofline, 18 units above the roofs (the band); two in three of its moons are candidates
    line = [(x, y - 18) for (x, y) in b.f("roofline")]
    for k, p in enumerate(b.trace(line, spacing=36, r=9, tag="roofline")):
        p["canBeOrange"] = k % 3 != 2 or p["x"] > 450
    b.trace("cliff", spacing=40, r=8, start=30, orange={1, 4, 7, 10, 13}, tag="cliff ledge")
    b.trace([(250, 444), (420, 466), (262, 490), (404, 512)], spacing=34, r=8, orange={1, 5}, tag="switchback road")
    for (x, y) in ((600, 472), (690, 498), (130, 470), (520, 500)):
        b.place(x, y, r=8, tag="campfire")
    for k, (x, y) in enumerate(STARS):
        b.place(x, y, r=8, orange=k in (1, 2, 3, 5, 7, 9, 10, 11, 12, 13, 14), tag="star")
    b.greens_in_reach()
