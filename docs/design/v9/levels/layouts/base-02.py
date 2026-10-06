"""1-2 "Horizon by Night" (The Waking Sands, Minfilia): a silhouette band.

Subject: Horizon, the Western Thanalan town on its mesa where the road from the Waking Sands first climbs: its flat
sandstone roofs, the great water tower on its stilts and the mine's derrick against the stars (our own painting,
painters/horizon.py). It replaces round 1's Ul'dah skyline, which took stage 4's home and repeated 1-4's painting.

Technique: the skyline is traced along its roofline as one continuous band: a dotted line of moons rides 18 units
above every roof block, rising over the derrick's head and the tank, whose domed cap wears a crown of brick (never
green) with a moon on its finial; most of the oranges are on the band. Below the town the mesa's cliff falls in three
stepped ledges, dotted, the road switchbacks down it in blue (round 2: its low oranges decided lost games), and a few
stars stand high in the open sky, three of them candidates.
"""
LEVEL = dict(id="base-02", name="Horizon by Night", stage=1, number=2, scene="horizon-by-night",
             subject="Horizon on its mesa: roofs, the water tower and the derrick against the stars",
             technique="a silhouette band (the roofline traced)")

STARS = [(130, 200), (180, 150), (330, 170), (470, 190), (610, 170), (260, 210), (690, 230), (560, 150)]
BAND = [[(84, 334), (148, 334), (170, 346), (192, 346)],
        [(236, 322), (298, 322), (318, 340), (342, 340), (356, 314), (408, 314), (424, 332), (456, 332)],
        [(612, 326), (660, 326), (680, 344), (712, 344)]]
LEDGES = [[(110, 410), (300, 416)], [(350, 444), (520, 450)], [(570, 418), (702, 412)]]


def build(b):
    cx, cy, R, a0, sw = b.circle("tank")
    b.arc_bricks(cx, cy, R, a0, sw, n=2, t=12, tag="tank crown", green=False)
    b.place(cx, cy - R - 34, r=9, orange=True, green=False, tag="tank finial")
    (dx, dy), = b.f("derrick")
    b.place(dx, dy - 22, r=9, orange=True, green=False, tag="derrick head")
    # the roofline, 18 units above the roofs: one band at even spacing in three runs (west of the derrick, between the
    # derrick and the tank, east of the tank), with a moon either side of the derrick's legs; nearly all candidates
    for run in BAND:
        for k, p in enumerate(b.trace(run, spacing=32, r=8, tag="roofline")):
            p["canBeOrange"] = k % 4 != 3
    for (x, y, o) in ((190, 282, True), (238, 282, True), (180, 336, False), (248, 336, True)):
        b.place(x, y, r=8, orange=o, tag="derrick legs")
    # the water tower's stilts, a moon outside each pair
    for (x, y, o) in ((486, 340, True), (594, 340, True), (482, 380, True), (598, 380, True)):
        b.place(x, y, r=8, orange=o, tag="tower stilts")
    # the lantern strings slung between the roofs: a lantern hangs at the middle of each
    for (x, y) in ((194, 366), (474, 352), (600, 350)):
        b.place(x, y, r=8, tag="lantern string")
    # the cliff falls in three stepped ledges (the middle one low on the board: blue)
    for k, run in enumerate(LEDGES):
        b.trace(run, spacing=36, r=8, orange=({1}, False, True)[k], tag="cliff ledge")
    b.trace([(250, 474), (420, 488)], spacing=34, r=8, tag="switchback road")
    b.trace([(300, 512), (430, 518)], spacing=34, r=8, tag="switchback road")
    for (x, y) in ((600, 472), (690, 498), (130, 470), (520, 500)):
        b.place(x, y, r=8, tag="campfire")
    for k, (x, y) in enumerate(STARS):
        b.place(x, y, r=8, orange=k in (1, 2, 3, 7), tag="star")
    for (x, y) in ((300, 250), (420, 250), (130, 250), (700, 300)):         # four fainter stars, blue (placed last)
        b.place(x, y, r=8, tag="star")
    for (x, y) in ((458, 360), (515, 388)):          # lanterns under the eaves, in the last dead lanes (critic G4)
        b.place(x, y, r=8, tag="lantern")
    b.greens_in_reach()
