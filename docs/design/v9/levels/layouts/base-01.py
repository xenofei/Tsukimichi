"""1-1 "Road to Horizon" (The Waking Sands, Minfilia): a trail on a map.

Subject: the road a new adventurer walks out of the Waking Sands, on Western Thanalan's own area map: east through
the Footfalls' oasis to Horizon, south over the bridge at Horizon's Edge to Scorpion Crossing and on to the gate toward
Ul'dah, with the side roads to Crescent Cove, Copperbell Mines and down through the Hammerlea toward the Silver Bazaar.

Technique: each place is a ring of moons (six at the five main places, five at the others) over the gilt road engraved
on the chart, and the roads between are dotted with moons wherever they leave a ring room, so a cleared board leaves
the road behind (`road` tries a dash of brick first, but at r 10 and t 20 the rings leave no stretch of road both long
and steep enough for one, and the path north to Vesper Bay is dotted too, clear of the west hill). The chart's terrain
is drawn in brick: its two cliff coasts (the land's west shore down the middle, and the east shore under Copperbell with its
upper cliff beside it) as solid runs, and the two hills either side of the launcher as contour crowns of short arc
bricks with a dotted inner contour and a cairn at the summit. The sea lane down the west is dashed in brick with two
ships' lights riding it, and the chart's compass rose in the empty desert to the south-west is two brick needles
(north and south) with six moons round them: both are the chart's, not the road's, so they never read as places.

Refit to the Peggle-match rules (owner's decision, 9 October 2026): pegs r 10 and bricks t 20, 98 pieces, and every
piece an orange candidate (`b.candidates`) except the compass rose, the sea lane and its lights (blue, and the board's
sure greens) and the HARD places below, so 65 of 98 can be orange. Bricks are 22% of the board, under the 30-45%
guidance: the subject is a trail of places and roads, and the bricks it allows are the chart's coasts, hills, lane and
compass. The first level of the campaign, tuned to stage 1's band (charter 1.1: 7.47-9.67 per 48, mean 8.57).
"""
import math

LEVEL = dict(id="base-01", name="Road to Horizon", stage=1, number=1, scene="thanalan-road-chart",
             subject="the road from the Waking Sands to Horizon and on toward Ul'dah",
             technique="a trail on a map")

# (ring of moons: count, radius, first moon's angle)
STOPS = {"waking sands": (6, 35, -90), "footfalls": (6, 35, -90), "horizon": (6, 35, -90), "bridge": (6, 35, -90),
         "scorpion crossing": (6, 35, -90), "crescent cove": (5, 30, -90), "copperbell mines": (5, 30, -90),
         "east gate": (5, 30, -90), "hammerlea": (5, 30, -90)}
BRICK_CLEAR = 18.0        # a brick's face to another brick's (never a notch of 3.5-17)
MIN_SLOPE = 11.0          # a brick flatter than this is a level deck: dot the road there instead
# Left blue (never orange): the places a player clears least readily, ranked by `ease`-style play with every piece a
# candidate on the tuning seeds (1-864): the far corner of the east hill, the Waking Sands' west moons, the low moons
# of Scorpion Crossing, the Hammerlea and Crescent Cove, and the Silver Bazaar road in the bucket's approach. The pick
# (65 of 98 candidates) read 8.5 per 48 on the second tuning block (seeds 20001-23456): stage 1's mean is 8.57.
HARD = [(663, 152), (626, 134), (628, 178), (120, 168), (120, 202), (150, 220), (377, 506), (413, 506), (520, 490),
        (550, 507), (580, 490), (520, 454), (314, 547), (346, 526), (228, 329)]


def _walk(pts, s):
    """The point at arc length s along a polyline (clamped to its ends)."""
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        L = math.hypot(x1 - x0, y1 - y0)
        if s <= L:
            t = s / L if L else 0.0
            return x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        s -= L
    return pts[-1]


def _length(pts):
    return sum(math.hypot(x1 - x0, y1 - y0) for (x0, y0), (x1, y1) in zip(pts, pts[1:]))


def _samples(o, n=12):
    if o["kind"] == "line":
        return [(o["x1"] + (o["x2"] - o["x1"]) * k / n, o["y1"] + (o["y2"] - o["y1"]) * k / n) for k in range(n + 1)]
    return [(o["x"] + o["r"] * math.cos(math.radians(o["start"] + o["sweep"] * k / n)),
             o["y"] + o["r"] * math.sin(math.radians(o["start"] + o["sweep"] * k / n))) for k in range(n + 1)]


def _brick_fits(b, x1, y1, x2, y2, touching=None):
    """A brick from (x1, y1) to (x2, y2) keeps the pre-flight's clearances: 13 from a moon's edge (16 when the moon
    is above it, the wedge band), and no notch (3.5-17) to a brick of another run; plus a small margin."""
    n = max(2, int(math.hypot(x2 - x1, y2 - y1) / 2))
    seg = [(x1 + (x2 - x1) * k / n, y1 + (y2 - y1) * k / n) for k in range(n + 1)]
    for p in b.pegs:
        d, (nx, ny) = min((math.hypot(p["x"] - sx, p["y"] - sy), (sx, sy)) for sx, sy in seg)
        g = d - p.get("r", 10) - 10
        if g < 13.5 or (g < 16.5 and p["y"] < ny - 2):
            return False
    for o in b.bricks:
        if o is touching:
            continue
        os_ = _samples(o)
        if min(math.hypot(ax - sx, ay - sy) for ax, ay in os_ for sx, sy in seg) - 20 < BRICK_CLEAR:
            return False
    return all(10 <= sx <= 790 and sy <= 505 for sx, sy in seg)


def road(b, name, length=30.0, gap=40.0, solid=False, tag=None, bricks=True, pegs=True):
    """A road (or coast) along a feature: dashed bricks `length` long with `gap` between them (solid: 2.5 apart, a
    continuous run); where a brick would be a level deck or would crowd a place, a moon dots the road instead."""
    pts = b.f(name) if isinstance(name, str) else [tuple(p) for p in name]
    total, s, out = _length(pts), 0.0, []
    tag = tag or name
    step = gap if not solid else 2.5
    while s <= total - 10:
        (x1, y1), (x2, y2) = _walk(pts, s), _walk(pts, s + length)
        slope = math.degrees(math.atan2(abs(y2 - y1), abs(x2 - x1)))
        touching = out[-1] if (solid and out) else None       # a solid run's previous brick touches this one
        if bricks and s + length <= total + 0.1 and slope >= MIN_SLOPE and _brick_fits(b, x1, y1, x2, y2, touching):
            out.append(b.line(x1, y1, x2, y2, tag=tag))
            s += length + step
            continue
        mx, my = _walk(pts, s + 10)
        if pegs and b.fits(mx, my, 10):
            b.place(mx, my, r=10, tag=tag)
            out.clear()                 # a moon breaks the run: the next brick is a new run
            s += 34.0
        else:
            s += 4.0
    return out


def hill(b, name, inner=40.0):
    """One of the chart's hills, engraved as contours: a crown of short arc bricks along the outer contour, a dotted
    inner contour of moons and a cairn at the summit."""
    cx, cy, R, fr, sw = b.circle(name)
    b.arc_bricks(cx, cy, R, fr, sw, n=max(3, round(math.radians(sw) * R / 38)), tag=name)
    Ri, sw, fr = R - inner, sw + 50, fr - 25          # the inner contour runs a little wider than the crown
    n = max(2, int(math.radians(sw) * Ri // 36))
    for k in range(n):
        a = math.radians(fr + sw * (k + 0.5) / n)
        b.place(cx + Ri * math.cos(a), cy + Ri * math.sin(a), r=10, tag=name)
    b.place(cx, cy, r=10, tag=name)


def build(b):
    # the chart's two hills to the north, either side of the launcher
    hill(b, "hill west")
    hill(b, "hill east")
    for name, (n, R, a0) in STOPS.items():
        (x, y), = b.f(name)
        b.stop(x, y, R=R, n=n, r=10, oranges=(), start_deg=a0, tag=name)
    # the chart's compass rose, engraved rather than a place: north and south needles in brick, east, west and the
    # four quarter points as moons (blue scenery and green-able: the board's sure greens)
    (cx, cy), = b.f("compass")
    b.line(cx, cy - 26, cx, cy - 58, tag="compass")
    b.line(cx, cy + 26, cx, cy + 58, tag="compass")
    for k in range(8):
        if k in (0, 4):
            continue
        a = math.radians(-90 + 45 * k)
        R = 44 if k % 2 == 0 else 52
        b.place(cx + R * math.cos(a), cy + R * math.sin(a), r=10, tag="compass")
    # the coasts: solid cliff runs
    road(b, "west shore", length=36, solid=True, tag="west shore")
    road(b, "east shore", length=36, solid=True, tag="east shore")
    road(b, "east cliff", length=36, solid=True, tag="east cliff")
    # the sea lane down the west, dashed, with two ships' lights riding it
    lane = b.f("sea lane")
    for (x, y) in (_walk(lane, 120), _walk(lane, 300)):
        b.place(x, y, r=10, tag="ship's light")
    road(b, "sea lane", length=32, gap=40, tag="sea lane", pegs=False)
    # the roads: dashed where they run steep enough (laid first), then dotted with moons wherever a dash would not go
    roads = ("road main", "road south", "road east", "road copperbell", "road cove", "road hammerlea", "road bazaar",
             "road vesper")
    for name in roads:
        road(b, name, pegs=False)
    for name in roads + ("south cliff", "road north"):
        road(b, name, bricks=False)
    b.greens_in_reach()
    b.candidates(exclude=HARD, exclude_tags=["compass", "ship's light", "sea lane"])


# ---- tuning (written by `mfl.py tune`; held out 7.9/48, tuning seeds 8.4/48, band 7.80-9.00)
TUNING = {
    "blue": [[150.0, 150.0], [534.3, 399.5], [504.0, 417.0], [550.0, 437.0], [263.6, 329.3], [217.5, 295.7]],
    "orange": [[628.0, 178.0], [150.0, 220.0], [114.5, 264.9], [250.0, 398.0], [130.9, 319.6], [151.3, 387.5]]
}
