"""3-4 "The Lane by Starlight" (the Night Skyway, Cid): drift.

Subject: what an airship passes by night on the lane, with no hull in sight (re-subjected to our own painting by
sources-plan.md, 3-4: no airship art exists at a usable size). The cloud sea's banks slide past, their crests lined by a
moon set beyond the upper right, the lane's beacon lamps close in two rows toward the horizon, and the star river climbs
the sky over it (painters/cloudlane.py).

Technique (DR): the two nearest banks' crests are carried by slide movers, each row drifting east and back on the
engine's clock, the nearer one further and faster (parallax); the rows part round the beacons' posts, so they read as
billows, not a fence. The far banks stand still: the horizon's crest and the middle bank's crest are rows of moons along
their billows, and the bank between them shows a few moons where it is glimpsed. The two far cloud towers have brick
flanks and moons on their crowns. The lane is dotted in perspective: the two near beacons are rings of moons round their
lamps, the others a moon on each lamp (never green: the subject's own lights). The star river's spine is a dotted line of moons; stars
stand in the open sky.

Peggle-match refit (9 October 2026 rules): densified from 72 to about 105 pieces at pegs r 10 and bricks t 20, with the
far banks and the towers' flanks in brick for a brick share near a third; every piece a candidate except the lamps and
the exclusions below (b.candidates), then tuned into the MR 3 band.

Hardened for the MR 3 band (22 October 2026): the far banks' brick crowns gave the ball a run of domes to roll along
and were the board's readiest oranges (held out 12.2 of 48 against the band's 3.04-5.24); their crests are traced in
moons instead, so a falling ball meets single moons rather than domes, and the near beacons' upper moons may now be
orange. Brick share falls to about an eighth; the bricks left are the towers' flanks, the near banks' steep sides, the
lane's centre line and the star river's spine.
"""
import numpy as np

LEVEL = dict(id="base-14", name="The Lane by Starlight", stage=3, number=14, scene="cloudlane",
             subject="the airship lane by starlight: cloud banks sliding past, the lane's beacons closing toward the "
                     "horizon and the star river over it, no hull",
             technique="drift (the cloud banks' crests carried by slide movers) with the lane dotted in perspective")

# the drifting rows, the nearer drifting further and faster: (crest feature, x range, least spacing, drift dx, period, lift above the crest)
ROWS = (
    ("crest 4", (96, 704), 34, 14.0, 8.5, 2.0),
    ("crest 5", (96, 704), 34, 18.0, 6.5, 4.0),
)

# the far banks stand still: the horizon's crest and the middle bank's crest traced in moons (crest, spacing, start)
TRACED = (("crest 1", 34, 10), ("crest 3", 36, 10))

# never orange: the sure greens' pool (pieces that may be green and are never orange), then the near bank's moons at
# the walls, the board's hardest low places (cheap difficulty)
GREENS = ((158, 283), (476, 242), (328, 370), (240, 396), (601, 276), (512, 230), (564, 350), (646, 405))
EASY = ((493, 111), (104, 86))
LOW = ((380, 477), (454, 473), (336, 461), (313, 443), (556, 392))
# the near beacons' rings: their upper moons may be orange, their lower ones never (the lowest places, cheap difficulty)
NEAR_LOW = ((216, 495), (614, 495), (185, 477), (248, 477), (583, 477), (645, 477))
EXCLUDE = tuple(g for g in GREENS if g not in ((158, 283), (601, 276), (564, 350), (512, 230))) + EASY + LOW + ((680, 464), (680, 406), (522, 491), (488, 481), (134, 462), (100, 450), (100, 400)) + NEAR_LOW

# the towers' steep flanks in brick: (feature, x from, x to)
FLANKS = (("tower left", 126, 180), ("tower left", 270, 296), ("tower right", 652, 700))

# the near banks' steep billow sides, where a drifting moon would leave its crest, in brick: (crest, x from, x to, lift)
SIDES = (("crest 5", 300, 356, 4.0), ("crest 5", 652, 712, 4.0), ("crest 4", 250, 292, 4.0), ("crest 4", 318, 350, 4.0))

# the star river's spine: a dashed run of brick climbing the sky (dash, gap, where it starts along the spine)
RIVER_DASH, RIVER_GAP, RIVER_START = 32.0, 20.0, 30.0
# its two banks of crowded stars, dotted either side of the spine (offsets), and their spacing
RIVER_BANKS, RIVER_STEP = (-40.0, 40.0), 36.0
SKY_TOP = 50.0                  # no star above this

# the lane's centre line: dashes of brick down its middle (y from, y to), shorter and closer toward the horizon
LANE_DASHES = ((290, 312), (352, 384), (424, 452))

STARS = ((120, 150), (150, 120), (190, 100), (240, 104), (290, 96), (600, 200), (640, 176), (560, 226),
         (300, 150), (660, 230), (196, 60), (252, 66), (300, 50), (700, 246), (340, 246), (96, 236),
         (700, 140), (520, 250), (104, 86), (512, 230), (476, 242), (620, 242), (104, 182), (524, 50),
         # more stars, the open sky's hard places (tried where the pre-flight allows)
         (130, 224), (176, 246), (222, 240), (262, 236), (330, 266), (372, 262), (412, 266), (452, 262), (492, 268),
         (606, 76), (646, 92), (680, 70), (430, 58), (474, 66), (340, 72), (600, 120), (560, 150))


def _posts():
    """Each beacon's post: (x, lamp y, foot x, foot y), from the painter's beacons."""
    import importlib.util
    from pathlib import Path
    spec = importlib.util.spec_from_file_location("painter_cloudlane", Path(__file__).parents[1] / "painters" / "cloudlane.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return [(b["base"][0], b["lamp"][1], b["base"][0], b["base"][1]) for b in mod.BEACONS]


POSTS = _posts()
LANE_VP, LANE_MID = (452.0, 272.0), (405.0, 576.0)     # the painter's vanishing point, and the lane's middle at the foot
OFF_CREST = 8.0                 # the most a mover may sit off its crest over its run (a peg's radius and one)


def _y_at(pts, x):
    xs, ys = zip(*pts)
    return float(np.interp(x, xs, ys))


def _flank(b, pts, length=30.0, tag=""):
    """Straight bricks of about `length` along a steep stretch, each laid only if it keeps the pre-flight's rules."""
    from layout import resample
    pts = resample(pts, length)
    out = []
    for (p, q) in zip(pts, pts[1:]):
        t = {"kind": "line", "x1": p[0], "y1": p[1], "x2": q[0], "y2": q[1], "thickness": 20.0}
        if not b.brick_why_not(t, touching=out[-1:]):
            out.append(b.line(p[0], p[1], q[0], q[1], tag=tag))
    return out


def _dashes(b, pts, length, gap, start=0.0, end_trim=0.0, tag=""):
    """A dashed run of straight bricks of `length` along a polyline, at least `gap` apart, packed from its start: where
    a dash would break the pre-flight's rules the run moves on 4 units and tries again."""
    from layout import point_at
    P = np.asarray(pts, np.float64)
    total = float(np.sum(np.hypot(*np.diff(P, axis=0).T)))
    out, s = [], start
    while s + length <= total - end_trim:
        p, q = point_at(P, s), point_at(P, s + length)
        t = {"kind": "line", "x1": p[0], "y1": p[1], "x2": q[0], "y2": q[1], "thickness": 20.0}
        if b.brick_why_not(t):
            s += 4.0
            continue
        out.append(b.line(p[0], p[1], q[0], q[1], tag=tag))
        s += length + gap
    return out


def build(b):
    # the lane's beacons: the near pair as rings of moons round their lamps, the rest a moon on each lamp
    for (x, y, s, side) in b.features["beacons"]["lamps"]:
        if s > 0.7:
            b.stop(x, y, R=36, n=6, r=10, oranges=(), green=False, tag="near beacon")
        elif s >= 0.15:
            b.place(x, y, r=10, green=False, tag="beacon")
    # the lane's edges in perspective: a moon halfway between each pair of lamps on a side
    for side in (0, 1):
        lamps = sorted((l for l in b.features["beacons"]["lamps"] if l[3] == side), key=lambda l: -l[2])
        for (p, q) in zip(lamps, lamps[1:]):
            b.place((p[0] + q[0]) / 2, (p[1] + q[1]) / 2, r=10, tag="lane edge")
    # the lane's centre line: dashes down its middle, from the vanishing point toward us
    (vx, vy), (nx, ny) = LANE_VP, LANE_MID
    for (y0, y1) in LANE_DASHES:
        (x0, x1) = (vx + (nx - vx) * (y - vy) / (ny - vy) for y in (y0, y1))
        if not b.brick_why_not({"kind": "line", "x1": x0, "y1": y0, "x2": x1, "y2": y1, "thickness": 20.0}):
            b.line(x0, y0, x1, y1, tag="lane")
    # the far towers' steep flanks in brick
    for (name, x0, x1) in FLANKS:
        _flank(b, [p for p in b.f(name) if x0 <= p[0] <= x1], tag=name + " flank")
    # the far banks stand still: their crests traced in moons
    for (name, sp, st) in TRACED:
        b.trace(name, spacing=sp, r=10, start=st, end_trim=10, tag=name)
    for (name, x0, x1, lift) in SIDES:
        _flank(b, [(x, y - lift) for (x, y) in b.f(name) if x0 <= x <= x1], tag=name + " side")
    # the star river's spine, and the far towers' crowns
    _dashes(b, b.f("star river"), RIVER_DASH, RIVER_GAP, start=RIVER_START, end_trim=10, tag="star river")
    from mflkit.author import offset_polyline
    for off in RIVER_BANKS:
        from layout import resample
        for (x, y) in resample(offset_polyline(b.f("star river"), off), RIVER_STEP, 10.0, 0.0):
            if y >= SKY_TOP:
                b.place(x, y, r=10, tag="star river bank")
    # the far towers' crowns: moons over their tops
    b.trace([(x, y - 16) for (x, y) in b.f("tower left") if 180 <= x <= 264], spacing=36, r=10, tag="tower")
    b.trace([(x, y - 16) for (x, y) in b.f("tower right") if x >= 688], spacing=36, r=10, start=6, tag="tower")
    # the cloud banks sliding past: a row on each of the nearer crests, packed from the west: each mover rides at its
    # run's mean crest height; a run whose crest rises or falls more than OFF_CREST either side of that is passed
    # over, and so is a run that crosses a beacon's post (between its lamp and its foot), where the moon would look
    # stemmed on the post
    for (name, (x0, x1), sp, dx, period, lift) in ROWS:
        pts = b.f(name)
        group = ("slide", round(dx, 1), 0.0, period)
        x = x0
        while x <= x1:
            run = [_y_at(pts, x + dx * k / 8) for k in range(9)]
            y0 = float(np.mean(run)) - lift
            ok = max(abs(v - lift - y0) for v in run) <= OFF_CREST and not any(
                x - 14 <= px_ <= x + dx + 14 and ly < y0 + 10 and y0 - 10 < by for (px_, ly, _bx, by) in POSTS)
            ok = ok and not any(b.why_not(x + dx * k / 8, y0, 10.0, group=group, skip_group=k > 0) for k in range(9))
            if ok:
                b.slide(x, y0, dx, 0.0, period, r=10, tag=name)
                x += sp
            else:
                x += 4
    # the second bank's crest, glimpsed between the brick crowns of its neighbours: moons where they fit
    b.trace("crest 2", spacing=36, r=10, start=20, end_trim=20, tag="crest 2")
    # stars in the open sky
    for (x, y) in STARS:
        b.place(x, y, r=10, tag="star")
    b.greens_in_reach()
    b.candidates(exclude=EXCLUDE)


# ---- tuning (written by `mfl.py tune`; held out 4.4/48, tuning seeds 3.6/48, band 3.04-5.24)
TUNING = {
    "blue": [[327.4, 181.2], [455.3, 213.5], [488.5, 199.5], [521.7, 185.5], [290.0, 96.0], [274.6, 175.8], [285.0, 203.6], [345.4, 217.1], [412.6, 188.4], [477.9, 160.6], [545.5, 133.0]],
    "orange": [[556.0, 392.0], [328.2, 370.4], [240.0, 395.5], [476.0, 242.0]]
}
