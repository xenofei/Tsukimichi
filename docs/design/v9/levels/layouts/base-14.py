"""3-4 "The Lane by Starlight" (the Night Skyway, Cid): drift.

Subject: what an airship passes by night on the lane, with no hull in sight (re-subjected to our own painting by
sources-plan.md, 3-4: no airship art exists at a usable size). The cloud sea's banks slide past, their crests lined by a
moon set beyond the upper right, the lane's beacon lamps close in two rows toward the horizon, and the star river climbs
the sky over it (painters/cloudlane.py).

Technique (DR): the cloud banks' crests are carried by slide movers, three rows riding the three nearest banks, each
bank drifting a little further and faster than the one behind it (parallax), all east and back on the engine's clock;
the rows part round the beacons, so they read as billows, not a fence. The lane is dotted in perspective: the two near
beacons are rings of five moons round their lamps, the others a moon on each lamp, smaller with distance (never green:
the subject's own lights). The star river's spine is a dotted line of small moons; the two far cloud towers wear moons
on their crowns; the far horizon's crest is a light dotted line, parted where the lane meets it; a few stars stand in the
open sky.

MR 3 round 1 (game designer M2, m5; critic M1, m4, m5; UX n3): renamed from "The Airship Under Way", which promised a
hull the board never shows; the moon and its ringed limb gave way to the star river (its two candidates moved onto the
river). Each mover now slides along its crest, from a point on the crest to the crest's own height further east (a
straight slide in x left crest 5's movers up to 24 units off it at the ends), and a mover whose run would cross a
beacon's post is left out, so no moon looks stemmed on a post (UX n3's three moons on the lamps are still pegs, the
lamps' own). Mist along the slid crests cannot be had: the runtime keeps every mist band clear of the pieces across the
board's width. The lamp at (301, 392), the board's lowest protan place and its top holdout, is no longer a candidate,
and the candidates were retuned toward the step critic M1 asked for (3-4 at least 0.7 harder than 3-3): tuning 23.7,
second block 22.4, held out 22.4; pooled with the stage's fresh games 21.88, 2.44 below 3-3.
"""
import numpy as np

LEVEL = dict(id="base-14", name="The Lane by Starlight", stage=3, number=14, scene="cloudlane",
             subject="the airship lane by starlight: cloud banks sliding past, the lane's beacons closing toward the "
                     "horizon and the star river over it, no hull",
             technique="drift (the cloud banks' crests carried by slide movers) with the lane dotted in perspective")

# the cloud rows: (crest feature, x range, spacing, drift dx, period, lift above the crest)
ROWS = (
    ("crest 3", (96, 704), 44, 20.0, 11.0, 2.0),
    ("crest 4", (100, 700), 46, 26.0, 8.5, 1.0),
    ("crest 5", (104, 700), 50, 32.0, 6.5, 4.0),
)

# the orange candidates (31), by place (a mover by its home): the lamps of the middle distance, the moon's two inner
# moons, the left tower's crown, both upper cloud rows across their run, the horizon's dotted crest either side of the
# lane, and three stars; the far lamps stay blue, so the lane's end does not crowd the spread rule's 200 x 200
CANDIDATES = (
    (355, 349), (395, 318), (556, 392), (519, 349),                                                    # lamps
    (390, 198), (448, 173), (507, 148),                                                                # the river
    (195, 140), (227, 146), (261, 142), (276, 173),                                                    # the tower
    (140, 356), (184, 350), (448, 353), (580, 353), (624, 350), (668, 349),                            # crest 3
    (376, 407), (422, 403), (468, 391), (606, 396),                                                    # crest 4
    (129, 287), (169, 287), (208, 283), (287, 290), (536, 288), (575, 280), (654, 279),                # horizon
    (330, 214), (520, 214), (300, 250),                                                                # stars
)


def _posts():
    """Each beacon's post: (x, lamp y, foot x, foot y), from the painter's beacons."""
    import importlib.util
    from pathlib import Path
    spec = importlib.util.spec_from_file_location("painter_cloudlane", Path(__file__).parents[1] / "painters" / "cloudlane.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return [(b["base"][0], b["lamp"][1], b["base"][0], b["base"][1]) for b in mod.BEACONS]


POSTS = _posts()
OFF_CREST = 9.0                 # the most a mover may sit off its crest over its run (a peg's radius and one)


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
    # the star river's spine, and the far towers' crowns
    b.trace("star river", spacing=64, r=8, start=170, end_trim=170, tag="star river")
    b.trace([(x, y - 12) for (x, y) in b.f("tower left")], spacing=36, r=8, start=14, end_trim=10, tag="tower")
    b.trace([(x, y - 12) for (x, y) in b.f("tower right")], spacing=36, r=8, start=10, end_trim=4, tag="tower")
    # the cloud banks sliding past: one row on each of the three nearest crests
    for (name, (x0, x1), sp, dx, period, lift) in ROWS:
        pts = b.f(name)
        for x in range(x0, x1 + 1, sp):
            # the row slides as one (a drift group), so each mover rides at its run's mean crest height, and a run
            # whose crest rises or falls more than a peg's radius either side of that is left out (crest 5's movers sat
            # up to 24 units off it at their ends); so is a run that crosses a beacon's post (between its lamp and its
            # foot), where the moon would look stemmed on the post
            run = [_y_at(pts, x + dx * k / 8) for k in range(9)]
            y0 = float(np.mean(run)) - lift
            if max(abs(v - lift - y0) for v in run) > OFF_CREST:
                continue
            if any(x - 12 <= px_ <= x + dx + 12 and ly < y0 + 8 and y0 - 8 < by for (px_, ly, _bx, by) in POSTS):
                continue
            b.slide(x, y0, dx, 0.0, period, r=8, tag=name)
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
