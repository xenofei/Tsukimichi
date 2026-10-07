"""3-5 "The Sky Whale" (The Night Skyway, Cid): a creature in outline.

Subject: the whale of the game's own world painting "The Three Great Continents", swimming the cloud sea north of the
Rhotano under the great cloud vortex, with two chevrons of birds flying its way (read as a chart under the moon).

Technique: an even ring of twelve moons runs 18 units outside the whale's silhouette (never green: the subject's own
shape), so the ball reads its shape before the first shot; six of them are candidates. Each flock's chevron is a pair
of dotted lanes of small moons from its lead bird, and the chart's dashed lanes (the recipe's overlays, where the glints
run) lead from both flocks to the whale. The vortex's lower rim is an arc of moons under the launcher; the cloud sea's
billows wear short arcs of brick on their crowns (a low row of three above the bucket's lane eases nothing and fills
the lower sea), with moons on the open cloud.

The finale's difficulty comes from the whole board, not its bottom: no candidate sits at y 430 or lower. The hardest
places are the south flock's west wing (413, 351), the right edge's (700, 290) and (620, 290), and the whale's tail
(105, 232). Tuned on seeds 1-864 and then the second tuning block (seeds 20001-23456): the blue moons in the open sky,
(545, 215) and (185, 165), placed last so the deal stays, take the board from 20.9 to 18.5 per 48 on that block.
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
    even_ring(b, "whale ring", 18, 12, 9, oranges={0, 2, 4, 6, 8, 10}, tag="whale")
    # the flocks: each chevron a pair of dotted lanes from its lead bird
    b.trace("north flock upper wing", spacing=28, r=7, orange={0, 1, 2}, green=False, tag="north flock")
    b.trace("north flock lower wing", spacing=28, r=7, start=28, orange={1}, tag="north flock")
    b.trace("south flock east wing", spacing=28, r=7, orange={0, 2, 4}, green=False, tag="south flock")
    b.trace("south flock west wing", spacing=28, r=7, start=28, orange={1}, tag="south flock")
    # the vortex's lower rim under the launcher
    cx, cy, rx, ry = b.features["vortex"]["ellipse"]
    for k, a in enumerate(range(20, 170, 15)):
        t = math.radians(a)
        b.place(cx + rx * math.cos(t), cy + ry * math.sin(t), r=9, orange=k in (0, 3, 5, 6, 7, 8, 9), tag="vortex")
    # the cloud sea's billows: short arcs of brick on their crowns, concave down, and moons on the open cloud
    for (x, y) in BILLOWS:
        b.arc(x, y + 30, 30, 235, 70, t=10, tag="billow")
    # the lanes the flocks fly toward the whale: a moon on each
    b.place(340, 278, r=8, tag="lane")
    b.place(575, 300, r=8, orange=True, tag="lane")
    for (x, y, o) in CLOUD:
        b.place(x, y, r=8, orange=o, tag="cloud sea")
    b.place(545, 215, r=8, tag="cloud sea")
    b.place(185, 165, r=8, tag="cloud sea")
    b.greens_in_reach()


BILLOWS = [(150, 340), (250, 420), (140, 460), (320, 480), (480, 440), (600, 400), (680, 320), (560, 480), (200, 505),
           (420, 510), (640, 505)]
CLOUD = [(300, 320, True), (620, 290, True), (470, 380, True), (350, 410, True), (650, 400, True), (520, 410, True),
         (500, 245, False), (110, 400, False), (230, 345, False), (180, 400, True), (560, 340, False),
         (600, 450, False), (430, 480, False), (380, 470, False), (260, 470, False), (200, 470, False),
         (680, 470, False), (700, 360, False), (260, 380, False), (590, 370, False), (700, 290, True),
         (300, 400, True), (440, 425, True)]
