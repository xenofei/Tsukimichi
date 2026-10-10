"""3-4 "The Lane by Starlight" (the Night Skyway, Cid): drift.

Subject: what an airship passes by night on the lane, with no hull in sight (re-subjected to our own painting by
sources-plan.md, 3-4: no airship art exists at a usable size). The cloud sea's banks slide past, their crests lined by a
moon set beyond the upper right, the lane's beacon lamps close in two rows toward the horizon, and the star river climbs
the sky over it (painters/cloudlane.py).

Technique (DR): the two nearest banks' crests are carried by slide movers, each row drifting east and back on the
engine's clock, the nearer one further and faster (parallax); the rows part round the beacons' posts, so they read as
billows, not a fence. The far banks stand still: the horizon's crest and the middle bank's crest are dashed brick runs
along their billows, parted where the lane runs through them. The two far cloud towers have brick flanks and moons on
their crowns. The lane is dotted in perspective: the two near beacons are rings of moons round their lamps, the others a
moon on each lamp (never green: the subject's own lights). The star river's spine is a dotted line of moons; stars
stand in the open sky.

Peggle-match refit (9 October 2026 rules): densified from 72 to about 105 pieces at pegs r 10 and bricks t 20, with the
far banks and the towers' flanks in brick for a brick share near a third; every piece a candidate except the lamps and
the exclusions below (b.candidates), then tuned into the MR 3 band.
"""
import math

import numpy as np

LEVEL = dict(id="base-14", name="The Lane by Starlight", stage=3, number=14, scene="cloudlane",
             subject="the airship lane by starlight: cloud banks sliding past, the lane's beacons closing toward the "
                     "horizon and the star river over it, no hull",
             technique="drift (the cloud banks' crests carried by slide movers) with the lane dotted in perspective")

# the drifting rows, the nearer drifting further and faster: (crest feature, x range, least spacing, drift dx, period, lift above the crest)
ROWS = (
    ("crest 3", (96, 704), 36, 10.0, 11.0, 4.0),
    ("crest 4", (96, 704), 36, 14.0, 8.5, 2.0),
    ("crest 5", (96, 704), 36, 18.0, 6.5, 4.0),
)

# the middle bank stands still: a crown of brick on each of its billows (x of the billow's top, the crown's radius)
CROWNS = ((104, 32), (228, 40), (420, 30), (516, 30), (636, 40))
CROWN_LIFT = 4.0

# never orange: the sure greens' pool (pieces that may be green and are never orange), then the near bank's moons at
# the walls, the board's hardest low places (cheap difficulty)
GREENS = ((169, 287), (300, 130), (292, 349), (240, 395), (602, 282), (520, 214), (556, 349), (644, 405))
EXCLUDE = GREENS + ((680, 464), (680, 406), (524, 492), (488, 481), (136, 463), (100, 450), (100, 400))

# the towers' steep flanks in brick: (feature, x from, x to)
FLANKS = (("tower left", 126, 180), ("tower left", 270, 296), ("tower right", 630, 690))

# the near banks' steep billow sides, where a drifting moon would leave its crest, in brick: (crest, x from, x to, lift)
SIDES = (("crest 5", 300, 356, 4.0),)

# the current down the lane's middle: motes at these heights, closer together toward the horizon
LANE_MOTES = (300, 336, 374, 418)

STARS = ((520, 214), (590, 240), (250, 100), (300, 130), (640, 150), (420, 230), (470, 250), (540, 180), (600, 200),
         (296, 82), (500, 82), (542, 94), (140, 148), (98, 178), (86, 220), (674, 160), (572, 154),
         (326, 154))


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


def _crown(b, x, top, R, sweep=90.0, n=2, tag=""):
    """A crown of n arc bricks over a billow whose top is (x, top), laid only when every brick keeps the pre-flight's
    rules against what is laid already (a crown that does not fit is left out whole)."""
    g = math.degrees(2.5 / R)
    each = (sweep - g * (n - 1)) / n
    start = 270.0 - sweep / 2
    trial = [{"kind": "arc", "x": x, "y": top + R, "r": R, "start": start + k * (each + g), "sweep": each,
              "thickness": 20.0} for k in range(n)]
    if any(b.brick_why_not(t, touching=trial) for t in trial):
        return []
    return b.arc_bricks(x, top + R, R, start, sweep, n=n, tag=tag)


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


def build(b):
    # the lane's beacons: the near pair as rings of moons round their lamps, the rest a moon on each lamp
    for (x, y, s, side) in b.features["beacons"]["lamps"]:
        if s > 0.7:
            b.stop(x, y, R=36, n=6, r=10, oranges=(), green=False, tag="near beacon")
        elif s >= 0.15:
            b.place(x, y, r=10, green=False, tag="beacon")
    # the lane's current: motes down its middle, from the vanishing point toward us
    (vx, vy), (nx, ny) = LANE_VP, LANE_MID
    for y in LANE_MOTES:
        b.place(vx + (nx - vx) * (y - vy) / (ny - vy), y, r=10, tag="lane")
    # the far towers' steep flanks in brick
    for (name, x0, x1) in FLANKS:
        _flank(b, [p for p in b.f(name) if x0 <= p[0] <= x1], tag=name + " flank")
    # the middle bank stands still: a crown of brick over each billow
    mid = b.f("crest 3")
    for (x, R) in CROWNS:
        _crown(b, x, _y_at(mid, x) - CROWN_LIFT, R, tag="crest 3")
    for (name, x0, x1, lift) in SIDES:
        _flank(b, [(x, y - lift) for (x, y) in b.f(name) if x0 <= x <= x1], tag=name + " side")
    # the star river's spine, and the far towers' crowns
    b.trace("star river", spacing=42, r=10, start=40, end_trim=10, tag="star river")
    b.trace([(x, y - 16) for (x, y) in b.f("tower left") if 180 <= x <= 264], spacing=36, r=10, tag="tower")
    b.trace([(x, y - 16) for (x, y) in b.f("tower right") if x >= 688], spacing=36, r=10, start=6, tag="tower")
    # the far horizon's crest, a light dotted line parted where the lane meets it
    far = b.f("crest 1")
    b.trace([p for p in far if p[0] < 400], spacing=40, r=10, start=30, tag="horizon")
    b.trace([p for p in far if p[0] > 500], spacing=40, r=10, start=20, end_trim=20, tag="horizon")
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
    # stars in the open sky
    for (x, y) in STARS:
        b.place(x, y, r=10, tag="star")
    b.greens_in_reach()
    b.candidates(exclude=EXCLUDE, exclude_tags=("near beacon",))
