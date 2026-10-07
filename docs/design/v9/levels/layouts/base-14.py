"""3-4 "The Airship Under Way" (the Night Skyway, Cid): drift.

Subject: what an airship passes by night on the lane, with no hull in sight (re-subjected to our own painting by
sources-plan.md, 3-4: no airship art exists at a usable size). The cloud sea's banks slide past beneath the moon and the
lane's beacon lamps close in two rows toward the horizon (painters/cloudlane.py).

Technique (DR): the cloud banks' crests are carried by slide movers, three rows riding the three nearest banks, each
bank drifting a little further and faster than the one behind it (parallax), all east and back on the engine's clock;
the rows part round the beacons, so they read as billows, not a fence. The lane is dotted in perspective: the two near
beacons are rings of five moons round their lamps, the others a moon on each lamp, smaller with distance (never green:
the subject's own lights). The moon's lower limb is ringed by an arc of moons; the two far cloud towers wear moons on
their crowns; the far horizon's crest is a light dotted line, parted where the lane meets it; a few stars stand in the
open sky.
"""
import numpy as np

LEVEL = dict(id="base-14", name="The Airship Under Way", stage=3, number=14, scene="cloudlane",
             subject="the airship lane by night: cloud banks sliding past under the moon, the lane's beacons closing "
                     "toward the horizon, no hull",
             technique="drift (the cloud banks' crests carried by slide movers) with the lane dotted in perspective")

# the cloud rows: (crest feature, x range, spacing, drift dx, period, lift above the crest)
ROWS = (
    ("crest 3", (96, 704), 44, 20.0, 11.0, 2.0),
    ("crest 4", (100, 700), 46, 26.0, 8.5, 3.0),
    ("crest 5", (104, 700), 50, 32.0, 6.5, 4.0),
)

# the orange candidates (31), by place (a mover by its home): the lamps of the middle distance, the moon's two inner
# moons, the left tower's crown, both upper cloud rows across their run, the horizon's dotted crest either side of the
# lane, and three stars; the far lamps stay blue, so the lane's end does not crowd the spread rule's 200 x 200
CANDIDATES = (
    (301, 392), (355, 349), (395, 318), (556, 392), (519, 349),                                        # lamps
    (583, 170), (546, 137),                                                                            # the moon
    (195, 140), (227, 146), (261, 142), (276, 173), (291, 205),                                        # the tower
    (140, 356), (184, 350), (448, 353), (580, 353), (624, 350),                                        # crest 3
    (376, 407), (422, 403), (468, 391), (606, 396),                                                    # crest 4
    (129, 287), (169, 287), (208, 283), (287, 290), (536, 288), (575, 280), (654, 279),                # horizon
    (330, 214), (520, 214), (300, 250),                                                                # stars
)


def _y_at(pts, x):
    xs, ys = zip(*pts)
    return float(np.interp(x, xs, ys))


def build(b):
    # the lane's beacons: the near pair as rings of moons round their lamps, the rest a moon on each lamp
    for (x, y, s, side) in b.features["beacons"]["lamps"]:
        if s > 0.7:
            b.stop(x, y, R=28, n=5, r=9, oranges=(), green=False, tag="near beacon")
        else:
            b.place(x, y, r=9 if s >= 0.3 else 8, green=False, tag="beacon")
    # the moon's lower limb, and the far towers' crowns
    cx, cy, R, a0, sw = b.circle("moon")
    arc = [(cx + R * np.cos(np.radians(a0 + sw * k / 24)), cy + R * np.sin(np.radians(a0 + sw * k / 24))) for k in range(25)]
    b.trace(arc, spacing=28, r=8, green=False, tag="moon")
    b.trace([(x, y - 12) for (x, y) in b.f("tower left")], spacing=36, r=8, start=14, end_trim=10, tag="tower")
    b.trace([(x, y - 12) for (x, y) in b.f("tower right")], spacing=36, r=8, start=10, end_trim=4, tag="tower")
    # the cloud banks sliding past: one row on each of the three nearest crests
    for (name, (x0, x1), sp, dx, period, lift) in ROWS:
        pts = b.f(name)
        for x in range(x0, x1 + 1, sp):
            b.slide(x, _y_at(pts, x) - lift, dx, 0.0, period, r=8, tag=name)
    # the far horizon's crest, parted where the lane meets it
    far = b.f("crest 1")
    b.trace([p for p in far if p[0] < 390], spacing=40, r=8, start=30, tag="horizon")
    b.trace([p for p in far if p[0] > 510], spacing=40, r=8, start=20, tag="horizon")
    b.place(338, 322, r=8, tag="cloud top")             # a billow in the lane's left edge (no first shot slips past)
    # stars in the open sky
    for (x, y) in ((330, 214), (520, 214), (300, 250), (560, 250)):
        b.place(x, y, r=8, tag="star")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True)
    b.greens_in_reach()
