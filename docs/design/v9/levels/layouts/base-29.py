"""6-4 "Meteor Shower" (The Silent Stars, Urianger; set nowhere): drift.

Subject: a meteor shower over the mountains on a still night (painters/meteors.py). Nine bright meteors streak away
from the radiant high in the upper left (above the reach of any flight, so it holds no pieces: the eye finds it by the
spray), a fan of faint ones between them; the Milky Way rises up the right side and the airglow lights the mountains.

Technique (DR): each bright meteor is a row of slide movers dotted along its own streak, the row sliding together down
the streak toward the meteor's head and back on the engine's clock, so the meteors fall across a still sky; the longer
and brighter a meteor, the further and faster it falls (the fireball fastest). The still sky round them is still pegs:
moons on its bright stars. The far range's three snow peaks wear a moon on each summit and bricks along their flanks,
opened at the summit, so the board closes on the mountains the shower falls toward.

Candidates: every meteor carries at least one (four on the fireball), about a third stand on the still stars, and the
summits stay blue, since the second jewel (the airglow's aqua) lies on the mountains and orange on it fails F9 for a
protanope. The meteors near the left wall (7 and 9) and the low stars are the hardest places; the ramp was tuned on
seeds 1-864 and the second tuning block (20001-23456: 20.5 per 48), the last step one blue star at (540, 380).
"""
import math

from layout import direct_reach

LEVEL = dict(id="base-29", name="Meteor Shower", stage=6, number=29, scene="meteor-shower",
             subject="a meteor shower over the mountains: nine bright meteors streaking from the radiant across a "
                     "still sky, the Milky Way and the airglow over three ranges of snow peaks",
             technique="drift (each meteor a row of slide movers falling along its own streak) over a still sky of "
                       "moons on the bright stars, with the snow peaks in brick")

# the meteors' rows: (feature, spacing, slide length along the streak, period); the fireball first, longest and fastest
ROWS = (
    ("meteor 1", 34, 34.0, 6.0),
    ("meteor 2", 34, 30.0, 7.0),
    ("meteor 3", 34, 28.0, 7.5),
    ("meteor 4", 34, 26.0, 8.0),
    ("meteor 5", 34, 28.0, 7.25),
    ("meteor 6", 34, 24.0, 8.5),
    ("meteor 7", 34, 24.0, 9.0),
    ("meteor 8", 34, 26.0, 8.25),
    ("meteor 9", 34, 24.0, 9.5),
)

# the orange candidates (31), by place (a mover by its home)
CANDIDATES = (
    (439, 204), (501, 232), (563, 260), (625, 287),                                                    # meteor 1
    (353, 229), (408, 269), (463, 309),                                                                # meteor 2
    (268, 225), (314, 276), (359, 327),                                                                # meteor 3
    (275, 332), (308, 391), (185, 266), (204, 331),                                                    # meteors 4, 5
    (608, 190), (148, 359), (146, 325), (476, 402), (114, 270),                                        # meteors 6-9
    (470, 178), (600, 398), (560, 150), (250, 152), (640, 160), (474, 262), (556, 336), (640, 350),    # stars
    (240, 300), (380, 420), (180, 160), (684, 414),
)
LOW = 502.0                     # a flank brick ends above this, clear of the bucket's lane (y 520) with its thickness


def _above(pts, xmin=112.0, xmax=688.0):
    """A flank from its summit down, cut where it falls below LOW or comes within a wall pinch of a wall."""
    out = [pts[0]]
    for (a, c) in zip(pts, pts[1:]):
        if c[1] <= LOW and xmin <= c[0] <= xmax:
            out.append(c)
            continue
        t = 1.0
        if c[1] > LOW:
            t = min(t, (LOW - a[1]) / (c[1] - a[1]))
        if c[0] < xmin:
            t = min(t, (xmin - a[0]) / (c[0] - a[0]))
        if c[0] > xmax:
            t = min(t, (xmax - a[0]) / (c[0] - a[0]))
        out.append((a[0] + (c[0] - a[0]) * t, a[1] + (c[1] - a[1]) * t))
        break
    return out


def build(b):
    # the snow peaks: bricks down each flank (opened at the summit), a moon on each summit
    crest = b.f("far crest")
    for (px_, py_) in b.features["peaks"]:
        i = crest.index((px_, py_))
        left = _above([(x, y - 9) for (x, y) in crest[max(0, i - 2):i + 1]][::-1])[::-1]
        right = _above([(x, y - 9) for (x, y) in crest[i:i + 3]])
        b.bricks_along(left, length=26, gap=2.5, t=11, trim0=0, trim1=20, tag="flank")
        b.bricks_along(right, length=26, gap=2.5, t=11, trim0=20, trim1=0, tag="flank")
        b.place(px_, py_ - 29, r=9, green=False, tag="summit")
    # the meteors: each a row of movers along its streak, sliding toward its head; a row's points run from the tail
    # to where a mover's run ends on the head
    for (name, sp, L, period) in ROWS:
        (ax, ay), (hx, hy) = b.f(name)
        n = math.hypot(hx - ax, hy - ay)
        ux, uy = (hx - ax) / n, (hy - ay) / n
        # the row's one drift, at the level file's precision, so its movers are one group
        dx, dy = round(ux * L, 1), round(uy * L, 1)
        s = n - L
        while s > 8:
            x, y = round(ax + ux * s, 1), round(ay + uy * s, 1)
            if direct_reach(x, y, 8.0) or direct_reach(x + dx, y + dy, 8.0):
                b.slide(x, y, dx, dy, period, r=8, tag=name)
            s -= sp
    # the still sky
    for (x, y) in b.features["bright stars"]:
        if direct_reach(x, y, 8.0):
            b.place(x, y, r=8, tag="star")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True)
    b.greens_in_reach()
