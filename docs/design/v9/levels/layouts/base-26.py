"""6-1 "The Armillary" (The Silent Stars, Urianger): a lattice.

Subject: a celestial globe of light: a great armillary sphere on a terrace under the violet night, its brass frame
(the fixed meridian seen face on, the broad horizon ring at its waist) holding a cage of rings of light about the
tilted polar axis, a small golden globe at its heart throwing ten spokes of light out through the rings (our
own painting, painters/armillary.py, whose FEATURES this layout reads: the rings are true circles in 3D, projected, so
the pegs sit on the painted rings). Unlike 6-2's flat zodiac wheel next door, it is a sphere seen in depth: crossing
tilted rings, a pole axis, a stand.

Technique (rings and spokes of brick): the meridian's crown is brick (concave down), opened under the launcher; its
lower round, where a brick ring would be a cup, is dotted. The four diagonal spokes of light run out past the meridian
as brick spokes (22.5 degrees: a ball slides off them), a star at each tip and a bead on the upper west spoke; the two
level spokes, along the horizon ring's plane, are dotted out toward the walls from the horizon ring's two ends. Inside,
the equator is a dotted ring of moons (its whole round), the ecliptic's front a row of smaller moons, the heart a large
moon (never green) in a ring of six, and a moon stands on each pole and on the north rod's finial.

Candidates (29, the charter's count): the level spokes and horizon ends, the four spoke tips and the bead, the upper
meridian either side, the poles and the finial, the heart ring's three inner places, the equator and the ecliptic's
front, picked from `ease --pick` (the places most readily cleared) and the subject's own places under the spread rule
(the cage's two 200 x 200 squares hold 10 each, its maximum). Two are low (the lower spoke tips, y 454): low oranges are
about 8% of those left in lost games against 7% of the candidates.

Tuning (seeds 1-864 and 20001-23456; target 25): the first full board read 18.6, from the meridian's low moons as
candidates (left in up to 34% of lost games); with them blue and the set rebalanced it read 30.5, then 26.6. The
stuck rule fired about 130 times per 1728 games: the level spokes' rows (14 units between moons) and the lower spoke
tips (a 17-unit slot past the brick's end) held slow balls, so the rows went to 40 apart and the tips 36 past the
bricks (stuck fires 24 per 864). The bead on the upper west spoke and one moon on the ecliptic's back (294, 274) bring
the board to 25.1 on seeds 1-864 and 24.9 on the second block; both ecliptic back moons together read 23.8.
"""
import math

LEVEL = dict(id="base-26", name="The Armillary", stage=6, number=26, scene="armillary",
             subject="a celestial globe of light: an armillary sphere's rings and spokes about its glowing heart",
             technique="a lattice (rings and spokes of brick)")

CANDIDATES = [(116, 330), (156, 330), (196, 333), (230, 304), (101, 206), (101, 454), (321, 140),            # the west
              (250, 414), (266, 307), (314, 307), (320, 238),
              (362, 330), (419, 297), (419, 363), (456, 212), (489, 144),                        # the heart, the north pole
              (373, 399), (407, 412), (442, 420), (457, 277), (487, 298), (533, 352),  # the equator
              (557, 400), (569, 364), (572, 326), (644, 330), (684, 330), (699, 206), (699, 454)]  # the east


def _front(b, name, lo=0.0, back=False):
    """The feature's points in front of depth `lo` (the sphere's centre at 0), as one run (behind it with back=True)."""
    pts, depth = b.f(name), b.features[name + " depth"]
    if back:
        depth = [-d for d in depth]
    n = len(pts)
    k0 = next(k for k in range(n) if depth[k] < lo and depth[(k + 1) % n] >= lo)
    run = []
    for j in range(1, n + 1):
        k = (k0 + j) % n
        if depth[k] < lo:
            break
        run.append(pts[k])
    return run


def build(b):
    cx, cy, RM, _, _ = b.circle("meridian")
    # ---- the heart: a large moon in a ring of six; a moon on each pole, and the north rod's star finial
    b.place(cx, cy, r=12, green=False, tag="heart")
    b.stop(cx, cy, R=38, n=6, r=8, oranges=(), green=True, start_deg=-60, tag="heart ring")
    for name in ("north pole", "south pole"):
        x, y = b.features[name]
        b.place(x, y, r=9, green=False, tag=name)
    (x0, y0), (x1, y1) = b.f("rod north")
    L = math.hypot(x1 - x0, y1 - y0)
    b.place(x1 + (x1 - x0) / L * 8, y1 + (y1 - y0) / L * 8, r=8, green=False, tag="north finial")
    # ---- the meridian: a brick crown either side of the launcher, its lower round dotted
    b.arc_bricks(cx, cy, RM, 200, 50, n=2, t=10, green=False, tag="meridian crown")
    b.arc_bricks(cx, cy, RM, 290, 50, n=2, t=10, green=False, tag="meridian crown")
    ring = [(cx + RM * math.cos(math.radians(a)), cy + RM * math.sin(math.radians(a))) for a in range(-14, 195, 2)]
    b.trace(ring, spacing=38, r=9, tag="meridian")
    # ---- the four flank spokes of light, as brick spokes beyond the meridian, a star at each tip; the level spokes
    # (the horizon's line) dotted out to the walls
    for a in (-157.5, -22.5, 22.5, 157.5):
        t = math.radians(a)
        r0, r1 = RM + 32, RM + 116
        b.line(cx + r0 * math.cos(t), cy + r0 * math.sin(t), cx + r1 * math.cos(t), cy + r1 * math.sin(t), t=10,
               tag="spoke")
        b.place(cx + (r1 + 36) * math.cos(t), cy + (r1 + 36) * math.sin(t), r=8, tag="spoke tip")
    t = math.radians(-112.5)                  # a bead of light on the upper west spoke, just past the meridian
    b.place(cx + (RM + 34) * math.cos(t), cy + (RM + 34) * math.sin(t), r=8, tag="spoke bead")
    for x in (644, 684, 156, 116):
        b.place(x, cy, r=9, tag="level spoke")
    # ---- the equator (whole) and the ecliptic's front, dotted, one moon on its back; the horizon ring's two ends
    b.trace("equator", spacing=36, r=8, tag="equator", closed=True)
    b.trace(_front(b, "ecliptic"), spacing=32, r=7, tag="ecliptic")
    b.place(294, 274, r=7, tag="ecliptic (back)")
    ring = b.f("horizon ring")
    for (x, y) in (min(ring), max(ring)):     # its two ends, where it passes out through the meridian
        b.place(x, y, r=8, tag="horizon ring")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=6)
    b.greens_in_reach()
