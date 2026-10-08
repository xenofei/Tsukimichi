"""3-5 "The Sky Whale" (The Night Skyway, Cid): a creature in outline.

Subject: the whale of the game's own world painting "The Three Great Continents", swimming the cloud sea north of the
Rhotano under the great cloud vortex, with a chevron of birds flying its way (read as a chart under the moon).

MR 3 round 1 (game designer M1, m1, m2, n4; critic M2, m6): the finale zooms in. The crop is [940, 330, 960, 720] (it was
[870, 312, 1400, 1050], wider than 3-1's 1280), so the whale spans about 200 of the opening's 650 units and the board
no longer shows the opener's chart zoomed out: it overlaps 3-1's crop by 24.3% of its own area (the rule's 25%), and
the north flock is left beyond the right wall. The eleven identical billow arcs of the lower half, which traced nothing,
are gone.

Technique: an even ring of moons runs 16 units outside the whale's own silhouette (taken from the painting's dark mask:
the flukes, the tail stock, the dorsal fin, the head and the belly; never green), so the ball reads the creature, not an
oval. The south flock's chevron is a pair of dotted lanes from its lead bird, and its flight line to the whale's head
(the recipe's engraved route, where the glints run) carries two moons. The vortex is ringed along its whole visible rim,
from the right wall round under the isle and up its left side. The lower half traces the cloud sea itself: dotted lines
of small moons along the lit crests of the cloud lobes where the chart's dark gaps open beneath them (found on the
graded painting, so each line is a painted crest).

The finale's difficulty comes from the whole board, not its bottom: no candidate sits at y 430 or lower.
"""
import math

import numpy as np

LEVEL = dict(id="base-15", name="The Sky Whale", stage=3, number=15, scene="sky-whale",
             subject="the chart's whale swimming the cloud sea, two flocks of birds flying its way",
             technique="a creature in outline")


def _closed_smooth(pts, n=10):
    """Catmull-Rom through a closed loop of control points."""
    P = [np.asarray(p, np.float64) for p in pts]
    m, out = len(P), []
    for i in range(m):
        p0, p1, p2, p3 = P[(i - 1) % m], P[i], P[(i + 1) % m], P[(i + 2) % m]
        for k in range(n):
            t = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    return np.asarray(out)


def even_ring(b, name, offset, n, r, oranges, tag):
    """n moons evenly round a closed silhouette, `offset` units outside it (a true offset: points the vertex normals
    push nearer than the offset to the silhouette, at its concave turns, are dropped before the even spacing)."""
    S = _closed_smooth(b.f(name))
    area = 0.5 * np.sum(S[:, 0] * np.roll(S[:, 1], -1) - np.roll(S[:, 0], -1) * S[:, 1])
    T = np.roll(S, -1, 0) - np.roll(S, 1, 0)
    N = np.stack([-T[:, 1], T[:, 0]], 1) / np.linalg.norm(T, axis=1)[:, None]
    O = S + N * (-offset if area > 0 else offset)
    d = np.min(np.linalg.norm(O[:, None, :] - S[None, :, :], axis=2), axis=1)
    O = O[d > offset - 1.5]
    loop = np.vstack([O, O[:1]])
    seg = np.linalg.norm(np.diff(loop, axis=0), axis=1)
    cum = np.concatenate([[0], np.cumsum(seg)])
    out = []
    for k in range(n):
        s = cum[-1] * k / n
        i = min(np.searchsorted(cum, s, side="right") - 1, len(seg) - 1)
        x, y = loop[i] + (loop[i + 1] - loop[i]) * (s - cum[i]) / max(seg[i], 1e-9)
        out.append(b.place(x, y, r=r, orange=k in oranges, green=False, tag=tag))
    return out


def build(b):
    b.subject("whale")
    even_ring(b, "whale", 16, 15, 8, oranges=set(), tag="whale")
    # the south flock: its chevron a pair of dotted lanes from its lead bird
    b.trace("south flock east wing", spacing=28, r=7, green=False, tag="south flock")
    b.trace("south flock west wing", spacing=28, r=7, start=28, tag="south flock")
    # its flight line to the whale's head: moons on the route
    for (x, y) in ((480, 403), (450, 398), (420, 394)):
        b.place(x, y, r=8, tag="flight line")
    # the vortex's rim, from the right wall round under the isle and up its left side
    cx, cy, rx, ry = b.features["vortex"]["ellipse"]
    for k, a in enumerate(range(30, 200, 14)):
        t = math.radians(a)
        b.place(cx + rx * math.cos(t), cy + ry * math.sin(t), r=9, tag="vortex")
    # the vortex's inner wall, where its clouds turn down into the eye round the isle
    for k, a in enumerate(range(-10, 200, 15)):
        t = math.radians(a)
        x, y = cx + rx * INNER * math.cos(t), cy + ry * INNER * math.sin(t)
        if x > 440:                     # (west of the isle the wall runs behind its bright banner: F6)
            b.place(x, y, r=9, tag="vortex wall")
    # the eddies the vortex throws off on its west side: a ring of moons round each painted curl
    for (x, y, R) in EDDIES:
        b.stop(x, y, R=R, n=5, r=8, oranges=(), green=True, tag="eddy")
    # the cloud sea: dotted lines along the lit crests of its lobes (each found on the graded painting)
    for name in CRESTS:
        # (the high middle crest's moons are set so one stands on the launcher's axis: no first shot falls clear)
        b.trace(name, spacing=30, r=7, start=6 if name == "crest high middle" else 0, tag="crest")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=6)
    b.greens_in_reach()


EDDIES = ((178, 176, 28),)
INNER = 0.70                    # the vortex's inner wall, as a share of its rim's radii
CRESTS = ("crest high east", "crest high middle", "crest west", "crest middle", "crest south", "crest south-west",
          "crest east low")
# the orange candidates (29), by place: none at y 430 or lower
CANDIDATES = (
    (111, 340), (131, 309), (166, 296), (213, 341), (282, 350), (339, 390), (268, 419), (183, 409),     # the whale
    (148, 362), (224, 418),
    (542, 412), (568, 428), (480, 403), (420, 394),                                                     # the flock's line
    (664, 260), (606, 273), (543, 279), (363, 252), (285, 202), (263, 139), (275, 108),                 # the vortex's rim
    (643, 219), (509, 240),                                                                             # its inner wall
    (510, 321), (570, 323), (628, 314), (340, 327), (370, 332), (428, 330),                             # the high crests
)
