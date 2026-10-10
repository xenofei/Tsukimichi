"""1-3 "The Cactuar" (The Waking Sands, Minfilia): a creature in outline.

Subject: a cactuar, the running cactus of the Thanalan sands, mid-stride on a dune crest under the full moon (our own
painting, painters/cactuar.py).

Technique: the creature drawn in outline, in brick and moon (the Peggle-density refit of 9 October 2026: 98 pieces,
30 bricks, pegs r 10 and bricks t 20). Its straight edges wear brick: the two flanks of its body, the left side of its
head, the right side of its head running on into the top of the lowered arm, the top of the raised arm and the outer
edge of each striding leg; moons packed 20 units outside the silhouette close the outline everywhere else (the crown,
the raised hand, under the arms, the crotch and the feet), so the ball reads its shape before the first shot. The face's
three dark holes (two eyes and the open mouth) and a moon over the middle spine are never green. Two of the dress's
moonbeams are laid in brick. The far and the middle dune are crowns of curved brick (the format's arcs run to r 400,
so the crest is bent a little more), the two western crowns opened at the summit with a moon standing in the gap (a
closed crown's apex holds a ball); the near dune's slopes are brick and its flat floor dotted; the cactuar's track
crosses the trough behind it in two staggered rows of footprints, and ripples cross the trough ahead of it. Three
twinkling stars (a moon ringed by six) and single stars fill the open sky.

Candidates by share (author.candidates): every piece but the near dune, the track, the feet (and every moon below
y 480), what no first flight touches, and the sky's moons nearest the launcher (EASY: the ones a first shot clears
most readily on the tuning seeds, left blue so the oranges sit in the subject and the outer sky). 62% of the pieces,
11% of them in the lower third (the track and the low ring left them over-represented among the oranges left).
Tuned on seeds 1-864 and the second tuning block (20001-23456, 9.0 per 48); held out 8.8 per 48 against stage 1's
band of 7.47-9.67 (mean 8.57).
"""
import math

from layout import resample
from mflkit.author import offset_polyline, signed_area

LEVEL = dict(id="base-03", name="The Cactuar", stage=1, number=3, scene="sagolii-cactuar",
             subject="a cactuar mid-stride on a dune under the full moon",
             technique="a creature in outline")


# the sky's moons nearest the launcher, the ones a first shot clears most readily (tuning seeds 1-864): blue
EASY = [(505, 140), (368, 214), (296, 132), (332, 156), (536, 123), (310, 186), (332, 214), (408, 221), (506, 234),
        (566, 106), (536, 192)]


def fit_circle(pts):
    """The least-squares circle through a dune's crest points: (cx, cy, R)."""
    n = len(pts)
    sx = sum(p[0] for p in pts) / n
    sy = sum(p[1] for p in pts) / n
    u = [(p[0] - sx, p[1] - sy) for p in pts]
    suu = sum(a * a for a, _ in u)
    svv = sum(b * b for _, b in u)
    suv = sum(a * b for a, b in u)
    suuu = sum(a ** 3 for a, _ in u)
    svvv = sum(b ** 3 for _, b in u)
    suvv = sum(a * b * b for a, b in u)
    svuu = sum(b * a * a for a, b in u)
    rhs1, rhs2 = 0.5 * (suuu + suvv), 0.5 * (svvv + svuu)
    det = suu * svv - suv * suv
    uc = (rhs1 * svv - rhs2 * suv) / det
    vc = (suu * rhs2 - suv * rhs1) / det
    R = math.sqrt(uc * uc + vc * vc + (suu + svv) / n)
    return sx + uc, sy + vc, R


def crown(b, pts, x0, x1, gap, tag, length=38.0):
    """A dune's crest as a crown of curved brick from x0 to x1 (bricks about `length` long), opened at its summit by
    `gap` units; returns the summit (x, y) on the crest."""
    cx, cy, R = fit_circle(pts)
    if R > 390.0:                               # the format's arcs run to r 400: keep the summit, bend a little more
        cy, R = cy - R + 390.0, 390.0

    def ang(x):
        return math.degrees(math.atan2(-math.sqrt(max(R * R - (x - cx) ** 2, 0.0)), x - cx)) % 360.0
    a0, a1, top = ang(x0), ang(x1), 270.0
    half = math.degrees(gap / 2 / R)
    for s0, s1 in ((a0, min(a1, top - half)), (max(a0, top + half), a1)):
        if s1 - s0 > 1e-6:
            n = max(1, int(math.radians(s1 - s0) * R / length))
            b.arc_bricks(cx, cy, R, s0, s1 - s0, n=n, tag=tag)
    return cx, cy - R


def beam(b, ang, r0, r1, n, origin=(150, 108)):
    """A moonbeam in brick: n bricks along the dress's shaft at `ang` degrees from the moon, r0 to r1 from it."""
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    b.bricks_along([(origin[0] + c * r0, origin[1] + s * r0), (origin[0] + c * r1, origin[1] + s * r1)],
                   length=(r1 - r0 - 2.5 * (n - 1)) / n - 0.01, gap=2.5, tag="moonbeam")


def ring(b, name, offset=20.0, step=3.0, tag="outline"):
    """An even ring of moons `offset` units outside a closed silhouette, packed greedily: walking the ring, a moon goes
    down wherever it fits (so the ring closes up to the bricks that carry the straight edges, at legal spacing)."""
    pts = b.f(name)
    d = -offset if signed_area(pts) > 0 else offset
    path = offset_polyline(pts + [pts[0]], d, closed=True)
    out = []
    for (x, y) in resample(path, step, 0.0, 0.0):
        if b.fits(x, y):
            out.append(b.peg(x, y, tag=tag))
    return out


def build(b):
    # ---- brick first (bricks are laid unchecked; every peg after them keeps its distance)
    # the cactuar's straight edges, 18 units outside the silhouette (centre line)
    b.bricks_along([(383, 350), (385, 392), (388, 428)], length=38, gap=2.5, tag="flank")          # left flank
    b.bricks_along([(521, 350), (515, 386), (512, 426)], length=36, gap=2.5, tag="flank")          # right flank
    b.bricks_along([(418, 206), (399, 236)], length=34, gap=2.5, tag="head")                         # head, left side
    b.bricks_along([(343, 262), (382, 296)], length=24, gap=2.5, tag="raised arm")                  # raised arm's top
    # the head's right side runs on into the top of the lowered arm
    b.bricks_along([(497, 214), (514, 244), (521, 286), (555, 306), (580, 334), (592, 366)], length=36, gap=2.5,
                   tag="lowered arm")
    b.bricks_along([(384, 444), (370, 470), (357, 490)], length=26, gap=2.5, tag="leg")             # left leg, outer
    b.bricks_along([(520, 440), (538, 462), (556, 478)], length=26, gap=2.5, tag="leg")             # right leg, outer
    # the moon's light: two of the dress's shafts in brick
    beam(b, 42, 88, 200, 3)
    beam(b, 66, 92, 176, 2)
    # the dunes: crowns of curved brick on the crests, opened at the summit
    mid_w = crown(b, [(60, 400), (140, 380), (220, 372), (300, 384), (380, 410)], 100, 345, 70, "mid dune")
    far_w = crown(b, [(60, 330), (160, 312), (250, 318), (330, 336)], 112, 292, 70, "far dune")
    crown(b, [(560, 420), (620, 396), (680, 384), (740, 392)], 625, 700, 0, "mid dune")
    crown(b, [(520, 344), (640, 322), (720, 316), (800, 324)], 624, 696, 0, "far dune")
    # the near dune's two slopes (13 to 19 degrees), its flat floor dotted below
    b.bricks_along([(104, 464), (150, 480), (198, 491)], length=34, tag="near dune")
    b.bricks_along([(594, 494), (700, 469)], length=34, tag="near dune")

    # ---- the subject: its silhouette stays open inside, but for the face (r 9: the eyes are 32 apart)
    b.subject("cactuar")
    for (x, y) in b.f("eyes") + b.f("mouth"):
        b.place(x, y, r=9, green=False, inside=True, tag="face")
    b.place(458, 166, green=False, tag="crown")                 # one moon over the middle spine
    ring(b, "cactuar")
    for name in ("raised hand", "lowered hand"):
        for (x, y) in b.f(name):
            b.key(x, y, green=False, within=40)

    # ---- the dunes' summit moons, the near dune dotted, the cactuar's track in the trough behind it
    for (x, y) in (mid_w, far_w):
        b.place(x, y, tag="summit")
    b.trace([(232, 497), (320, 499)], spacing=36, tag="near dune")
    for x in range(112, 340, 36):              # its track: left and right footprints, staggered
        b.place(x, 420, tag="track")
        b.place(x + 18, 452, tag="near track")
    b.trace([(604, 446), (660, 436), (716, 428)], spacing=34, tag="ripple")

    # ---- the sky: three twinkling stars and single stars
    for (x, y) in ((640, 222), (134, 240), (566, 140)):     # a twinkling star: a moon ringed by six
        b.place(x, y, tag="star")
        b.stop(x, y, R=34.5, n=6, oranges=(), green=True, tag="star")
    for (x, y) in [(690, 280), (590, 276), (310, 186), (700, 150), (270, 160), (250, 120), (650, 120), (556, 232),
                   (505, 140), (260, 252), (332, 214), (368, 214), (296, 132), (332, 156), (536, 192), (656, 156)]:
        b.place(x, y, tag="star")

    b.greens_in_reach()
    b.candidates(exclude_tags=["near dune", "near track", "track"],
                 exclude=[(p["x"], p["y"]) for p in b.pegs if p["y"] > 480] + EASY)
    for (x, y) in b.f("feet"):                  # the feet stand at the bucket's approach: blue, never green
        b.key(x, y, orange=False, green=False, within=40)
