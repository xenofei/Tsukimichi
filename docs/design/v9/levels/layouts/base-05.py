"""1-5 "The Crystal's Call" (The Waking Sands, Minfilia): a constellation, the stage's finale.

Subject: the Mother Crystal's call, which Minfilia hears through the Echo, written in the night sky over the Waking Sands
as a constellation in the figure of a tall faceted crystal (our own painting, painters/crystal.py), its light spreading
through the sky in four rings.

Technique: a moon sits on every star of the figure (r 12, never green); the figure's upper and outer edges and its front
ridge are drawn in brick along the atlas's lines, its lower cross line and lower facets each carry a moon at the middle.
The call's two middle rings are crowns of arc brick, the inner and the far one rings of moons. A field of sky stars thickens along the Milky Way; on the
horizon, the Waking Sands' dome wears a brick crown, a moon hangs in each date palm's crown and the dunes' crests are
low brick crowns.

Peggle refit (owner's rules of 9 October 2026): 95 pieces (30 bricks, 32%), pegs r 10 (the figure's stars r 12, its
line moons r 9) and bricks t 20; candidates by exclusion (`b.candidates`), 63 of 95. The finale takes its difficulty
from the whole board: the places the greedy player cleared most readily on the tuning seeds (the top star, the figure's
upper edges, the rings' upper ends beside the launcher) stay blue, so the oranges fall on the rings' flanks, the field
and the figure's lower half. Tuned on seeds 1-864 (5.9 per 48) and the second tuning block (20001-23456: 6.0); held out
5.8. Lost games leave no single place orange in more than 13% of them (the crystal's foot and the star beside it).
"""
import math

LEVEL = dict(id="base-05", name="The Crystal's Call", stage=1, number=5, scene="crystal-call",
             subject="a constellation in the figure of a crystal over the Waking Sands",
             technique="a constellation")

STAR_R = 12
TRIM = STAR_R + 16 + 10 + 2          # a brick's end clears a star by 16 or more (the wedge band above a brick)
# the figure's edges drawn in brick: (from star, to star, bricks)
BRICK_EDGES = [(0, 1, 1), (0, 2, 1), (1, 5, 2), (2, 6, 2), (3, 4, 2)]
# the figure's edges dotted with moons: (from star, to star)
DOT_EDGES = [(5, 4), (6, 4), (5, 7), (6, 7)]
# the field: sky stars, thicker along the Milky Way (lower left to upper right)
FIELD = [(134, 93), (177, 95), (111, 130), (102, 168), (99, 243), (180, 310), (308, 314), (200, 348), (327, 353),
         (170, 378), (228, 372), (324, 428), (370, 455),
         (563, 311), (690, 315), (582, 350), (667, 352), (559, 387), (687, 391), (579, 425), (622, 426), (664, 428),
         (599, 463)]
# never orange (green-able, blue). The finale's difficulty comes from the whole board: the places the greedy player
# clears most readily stay blue (the crystal's top star and its two upper edges, the upper ends of the call's brick
# rings by the launcher, the inner ring's moons beside it), and so do a few sky stars beside the crystal
EXCLUDE = [(440, 186), (398, 219), (482, 219),                                      # the top star and upper edges
           (303, 153), (278, 116), (243, 148), (270, 185), (207, 120), (191, 227),  # the rings' west arcs, upper
           (518, 121), (563, 143), (543, 184),                                      # the rings' east arcs, upper
           (321, 196), (297, 223), (477, 156), (512, 167),                          # the inner ring by the launcher
           (308, 314), (563, 311), (327, 353), (559, 387)]                          # sky stars beside the crystal
DUNES = [(320, 548, 60), (545, 540, 56), (655, 546, 58)]     # the dunes' crests: crown centres and radii


def arc_pts(cx, cy, R, a0, sweep, step=4.0):
    n = max(2, int(abs(sweep) * math.pi / 180 * R / step))
    return [(cx + R * math.cos(math.radians(a0 + sweep * k / n)), cy + R * math.sin(math.radians(a0 + sweep * k / n)))
            for k in range(n + 1)]


def build(b):
    S = [tuple(p) for p in b.f("stars")]
    for (x, y) in S:
        b.place(x, y, r=STAR_R, green=False, tag="star")
    for (i, j, n) in BRICK_EDGES:
        (x0, y0), (x1, y1) = S[i], S[j]
        L = math.hypot(x1 - x0, y1 - y0)
        ux, uy = (x1 - x0) / L, (y1 - y0) / L
        span, gap = L - 2 * TRIM, 2.5
        each = (span - gap * (n - 1)) / n
        for k in range(n):
            s0 = TRIM + k * (each + gap)
            b.line(x0 + ux * s0, y0 + uy * s0, x0 + ux * (s0 + each), y0 + uy * (s0 + each), green=False, tag="edge")
    for (i, j) in DOT_EDGES:       # a moon at each line's middle
        b.place((S[i][0] + S[j][0]) / 2, (S[i][1] + S[j][1]) / 2, r=9, tag="line")
    # the call's rings: the two outer ones crowns of brick, the inner one dotted with moons
    for name in ("call middle", "call outer"):
        cx, cy, R, _f, _s = b.circle(name)
        for (a0, sw) in b.features[name]["arcs"]:
            b.arc_bricks(cx, cy, R, a0, sw, n=max(2, round(R * math.radians(sw) / 46)), tag=name)
    for name in ("call inner", "call far"):
        cx, cy, R, _f, _s = b.circle(name)
        for (a0, sw) in b.features[name]["arcs"]:
            b.trace(arc_pts(cx, cy, R, a0, sw), spacing=36, r=10, tag=name)
    # the Waking Sands: the dome's crown, the palms' moons, the dunes' crests
    b.arc(173, 458, 30, 205, 130, tag="dome")
    b.place(102, 424, r=10, tag="palm crown")
    b.place(258, 428, r=10, tag="palm crown")
    for (x, y, R) in DUNES:
        b.arc(x, y, R, 245, 50, tag="dune")
    for (x, y) in FIELD:
        b.place(x, y, r=10, tag="field star")
    b.greens_in_reach()
    b.candidates(exclude=EXCLUDE, exclude_tags=("line",))
