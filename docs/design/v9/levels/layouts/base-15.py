"""3-5 "The Sky Whale" (The Night Skyway, Cid): a creature in outline.

Subject: the whale of the game's own world painting "The Three Great Continents", swimming the cloud sea north of the
Rhotano under the great cloud vortex, with a chevron of birds flying its way (read as a chart under the moon).

MR 3 round 1 (game designer M1, m1, m2, n4; critic M2, m6): the finale zooms in. The crop is [940, 330, 960, 720], so the
whale spans about 200 of the opening's 650 units; it overlaps 3-1's crop by 24.3% of its own area (the rule's 25%), and
the north flock is left beyond the right wall.

Peggle-match refit (rules of 9 October 2026: 95-115 pieces, pegs r 10 and bricks t 20, 60-90% candidates): the board
keeps its subject and densifies it at full size.
- The whale: an even ring of 14 moons 20 units outside its own silhouette (the flukes, the tail stock, the dorsal fin,
  the head and the belly; never green), so the ball reads the creature, not an oval.
- The south flock: its lead bird a moon, the chevron's two wings in brick (east along the lead line, west down to the
  third bird), and its flight line to the whale's head dotted with moons.
- The vortex: its rim in brick where it falls steeply (both sides) with moons along its flat south; its inner wall in
  brick round the north-east turn and down the east side, moons along its south (east of the isle's banner: F6); the
  eye east of the isle ringed by six moons inside an arc of its own inner swirl; the outer band streaming in on the west
  in brick, curling over the west eddy, which is ringed by six moons round a seventh on its curl.
- The cloud sea: dotted lines of moons along the lit crests of its lobes (each found on the graded painting; the high
  bank's crest runs under the chart's title), the west arm's crest a crown of curved brick.
The brick share is about 19%: the subject is a ring of moons and a sea of dotted crests, so the bricks go where the
painting has steep edges (the vortex's walls, the chevron's wings, the arm's crown); crowns over the crests were tried
and made the board easier (fewer pieces, more hits a shot). The finale's difficulty comes from the whole board's
density (111 pieces) and from where its oranges fall: every reachable piece is a candidate but the sure greens and the
cloud sea's low crests, and the tuning (below) takes the candidates a player clears most readily out of the deal (the
open middle under the vortex and the high bank's crest), so the 25 oranges land more often on the whale's ring, the
east bank's wall and the corners.
Authoring pass of 22 October 2026: the whale's ring is as many moons as the silhouette's offset holds (14 between
surfaces) and the fill keeps 52 units from it (`clear_moat`), so the ring stands alone and reads as the creature; the
crests that ran close under and over the whale are gone. Held out 4.9 per 48 (MR 3's band 3.04-5.24): inside the band
but above the stage's centre, so a stage tune must still take it lower to be the finale (adding or removing fill
pieces only eased it; the tuner stops at the 61% candidate floor).
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

    def spots(n, phase):
        out = []
        for k in range(n):
            s = (cum[-1] * (k + phase) / n) % cum[-1]
            i = min(np.searchsorted(cum, s, side="right") - 1, len(seg) - 1)
            out.append(loop[i] + (loop[i + 1] - loop[i]) * (s - cum[i]) / max(seg[i], 1e-9))
        return np.asarray(out)

    def closest(P):
        d = np.linalg.norm(P[:, None, :] - P[None, :, :], axis=2) + np.eye(len(P)) * 1e9
        return d.min()

    # the most moons the loop holds with 14 between their surfaces (a chord can run shorter than the arc on a turn)
    best = None
    for m in range(n, 8, -1):
        for phase in (0.0, 0.25, 0.5, 0.75):
            P = spots(m, phase)
            if closest(P) >= 2 * r + 14.2:
                best = P
                break
        if best is not None:
            break
    out = []
    for k, (x, y) in enumerate(best):
        out.append(b.place(x, y, r=r, orange=k in oranges, green=False, tag=tag))
    return out


def clear_moat(b, ring, d, keep):
    """Takes out the fill's pegs that stand nearer than `d` (centre to centre) to a ring moon, so the whale's ring
    stands alone and reads as its outline; the pegs tagged in `keep` stay."""
    ring = [p for p in ring if p]
    pegs, tags, k, dropped = [], [], 0, 0
    for kind, tag in b.tags:
        if kind == "peg":
            p = b.pegs[k]
            k += 1
            if tag not in keep and min(math.hypot(p["x"] - q["x"], p["y"] - q["y"]) for q in ring) < d:
                dropped += 1
                continue
            pegs.append(p)
        tags.append((kind, tag))
    b.pegs[:] = pegs
    b.tags[:] = tags
    return dropped


def fill_gaps(b, ring, n, tag, box=(100, 640, 250, 520), clear=50):
    """n more moons, each in the emptiest spot of `box` (farthest from every piece, `clear` from the whale's ring)."""
    placed = []
    for _ in range(n):
        best, bd = None, 0.0
        for x in range(box[0], box[1] + 1, 6):
            for y in range(box[2], box[3] + 1, 6):
                if min(math.hypot(x - q["x"], y - q["y"]) for q in ring if q) < clear or not b.fits(x, y):
                    continue
                d = min(math.hypot(x - q["x"], y - q["y"]) for q in b.pegs)
                if d > bd:
                    best, bd = (x, y), d
        if best is None:
            break
        placed.append(b.place(*best, tag=tag))
    return placed


def chord(b, x1, y1, x2, y2, tag, green=True, prev=None):
    """A line brick laid only when it keeps the pre-flight's brick rules (2.5 from `prev`, its run's last brick)."""
    spec = dict(kind="line", x1=x1, y1=y1, x2=x2, y2=y2, thickness=20.0)
    why = b.brick_why_not(spec, touching=[prev] if prev else ())
    if why:
        b.skipped.append(((round((x1 + x2) / 2), round((y1 + y2) / 2)), tag, why))
        return None
    return b.line(x1, y1, x2, y2, tag=tag, green=green)


def ell_run(b, scale, a0, a1, n, tag, green=True):
    """n chord bricks along the vortex's ellipse (scaled about its eye) from a0 to a1 degrees, 2.5 apart."""
    cx, cy, rx, ry = b.features["vortex"]["ellipse"]
    rx, ry = rx * scale, ry * scale
    pts = [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * k / n)), cy + ry * math.sin(math.radians(a0 + (a1 - a0) * k / n)))
           for k in range(n + 1)]
    prev, out = None, []
    for (x1, y1), (x2, y2) in zip(pts, pts[1:]):
        L = math.hypot(x2 - x1, y2 - y1)
        ux, uy = (x2 - x1) / L, (y2 - y1) / L
        br = chord(b, x1 + ux * 1.25, y1 + uy * 1.25, x2 - ux * 1.25, y2 - uy * 1.25, tag, green, prev)
        prev = br
        if br:
            out.append(br)
    return out


def straight_run(b, x1, y1, x2, y2, n, tag, green=True):
    """n bricks along a straight stretch, 2.5 apart."""
    L = math.hypot(x2 - x1, y2 - y1)
    ux, uy = (x2 - x1) / L, (y2 - y1) / L
    each = (L - 2.5 * (n - 1)) / n
    prev, out = None, []
    for k in range(n):
        s0 = k * (each + 2.5)
        br = chord(b, x1 + ux * s0, y1 + uy * s0, x1 + ux * (s0 + each), y1 + uy * (s0 + each), tag, green, prev)
        prev = br
        if br:
            out.append(br)
    return out


def crown(b, name, sag, n, tag, green=True):
    """A crown of n arc bricks over a cloud lobe's lit crest: the circle through the crest's two ends rising `sag` above
    them at its middle, its bricks 2.5 apart."""
    pts = b.f(name)
    (x0, y0), (x1, y1) = pts[0], pts[-1]
    xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
    h = math.hypot(x1 - x0, y1 - y0) / 2
    R = (h * h + sag * sag) / (2 * sag)
    ux, uy = (x1 - x0) / (2 * h), (y1 - y0) / (2 * h)
    nx, ny = uy, -ux                                    # the normal pointing up (screen y down)
    cx, cy = xm - nx * (R - sag), ym - ny * (R - sag)
    a0 = math.degrees(math.atan2(y0 - cy, x0 - cx))
    a1 = math.degrees(math.atan2(y1 - cy, x1 - cx))
    sweep = (a1 - a0) % 360
    out, prev = [], None
    g = math.degrees(2.5 / R)
    each = (sweep - g * (n - 1)) / n
    for k in range(n):
        st = a0 + k * (each + g)
        spec = dict(kind="arc", x=cx, y=cy, r=R, start=st, sweep=each, thickness=20.0)
        why = b.brick_why_not(spec, touching=[prev] if prev else ())
        if why:
            b.skipped.append(((round(cx + R * math.cos(math.radians(st + each / 2))),
                               round(cy + R * math.sin(math.radians(st + each / 2)))), tag, why))
            prev = None
            continue
        prev = b.arc(cx, cy, R, st, each, tag=tag, green=green)
        out.append(prev)
    return out


def ell_dots(b, scale, angles, tag, r=10):
    cx, cy, rx, ry = b.features["vortex"]["ellipse"]
    return [b.place(cx + rx * scale * math.cos(math.radians(a)), cy + ry * scale * math.sin(math.radians(a)), r=r, tag=tag)
            for a in angles]


def build(b):
    b.subject("whale")
    ring = even_ring(b, "whale", WHALE_OFF, WHALE_N, 10, oranges=set(), tag="whale")
    # the south flock: its lead bird a moon, its chevron's two wings in brick
    b.place(542.5, 412.3, tag="lead bird")
    for (x1, y1, x2, y2, n) in WINGS:
        straight_run(b, x1, y1, x2, y2, n, tag="south flock")
    # its flight line to the whale's head: moons on the route
    b.trace("flight line", spacing=36, start=24, tag="flight line")
    # the vortex's rim: brick where it falls steeply, moons along its flat south
    ell_run(b, 1.0, *RIM_EAST, tag="vortex")
    ell_dots(b, 1.0, RIM_DOTS, tag="vortex")
    ell_run(b, 1.0, *RIM_WEST, tag="vortex")
    # the vortex's inner wall, from its north-east turn down the east side; moons along its south
    ell_run(b, INNER, *WALL_NE, tag="vortex wall")
    ell_run(b, INNER, *WALL_EAST, tag="vortex wall")
    ell_dots(b, INNER, WALL_DOTS, tag="vortex wall")
    # the eye's own inner swirl, east of the isle, and a ring of moons round the eye itself
    ell_dots(b, EYE, EYE_DOTS, tag="vortex eye")
    b.stop(*EYE_RING, oranges=(), green=True, tag="vortex eye")
    # the vortex's outer band streaming in on the west
    ell_run(b, OUTER, *BAND_WEST, tag="vortex band")
    # the inner band curling round the eddy, above and below it
    for run in BAND_CURL:
        ell_run(b, CURL, *run, tag="vortex band")
    # the eddies the vortex throws off on its west side: a ring of moons round each painted curl
    for (x, y, R, n) in EDDIES:
        b.stop(x, y, R=R, n=n, oranges=(), green=True, tag="eddy")
        b.place(x, y, tag="eddy")                               # and its eye
    # the cloud sea: dotted lines along the lit crests of its lobes (each found on the graded painting)
    for name, sag, n in CROWNS:
        crown(b, name, sag, n, tag="crest")
    for name, st in CRESTS:
        if name in {c[0] for c in CROWNS}:
            continue
        b.trace(name, spacing=CREST_SP, start=st, nudge=CREST_NUDGE, tag="low crest" if name in LOW_CRESTS else "crest")
    # the west arm's lit crest, a crown over its lobe
    b.arc_bricks(*ARM_CROWN, tag="crest")
    clear_moat(b, ring, MOAT, keep=("whale", "flight line"))
    # the first shots that skim the whale's back (a lane from the vortex's west rim down past its dorsal fin) meet a moon
    b.place(290, 258, tag="cloud lane")
    fill_gaps(b, ring, FILL_N, tag="cloud fill")
    b.greens_in_reach()
    # the whale's lowest moons stay blue too
    b.candidates(exclude=[(p["x"], p["y"]) for p in ring if p and p["y"] >= 405], exclude_tags=("low crest", "cloud fill"))


WHALE_N, WHALE_OFF = 20, 18
FILL_N = 0
MOAT = 52                       # the fill keeps this far (centre to centre) from the whale's ring
CREST_SP, CREST_NUDGE = 35.0, 4.0
# the chevron's wings from 36 units off the lead bird: east along its lead line, west down to the third bird
WINGS = ((542.5 + 40 * 0.864, 412.3 + 40 * 0.504, 700.0, 504.2, 3), (542.5, 412.3 + 40, 544.0, 512.0, 2))
INNER = 0.66                    # the vortex's inner wall, as a share of its rim's radii
OUTER = 1.62                    # the outer band
RIM_EAST = (44, 66, 2)
RIM_DOTS = (76, 86, 96, 106, 116, 126)
RIM_WEST = (134, 206, 4)
WALL_NE = (-74, -30, 3)
WALL_EAST = (-14, 52, 3)
WALL_DOTS = (64, 77, 90, 103)
EYE = 0.42                      # the eye's inner swirl
EYE_DOTS = (-76, -54, -24, 8, 40, 66)
EYE_RING = (525, 150, 34, 6)
BAND_WEST = (162, 203, 3)
CURL = 1.30
BAND_CURL = ((196, 212.5, 1),)
EDDIES = ((186, 176, 36, 6),)
CRESTS = (("crest high east", 0), ("crest high middle", 0), ("crest middle", 0), ("crest south", 0), ("crest south-west", 0),
          ("crest low west", 0), ("crest low middle", 0), ("crest mid east", 0),
          ("crest flock east", 0),
          ("crest flight wake", 0), ("crest low south", 0), ("crest east lobe", 0), ("crest far west", 0),
          ("crest under title", 0), ("crest high bank", 0), ("crest east bank", 0), ("crest flock lee", 0))
CROWNS = ()
ARM_CROWN = (168, 409.6, 161.6, 245.3, 49.7, 3)        # through the west arm's crest (100, 262), (168, 248), (236, 264)
# never orange: the cloud sea's low crests, so the lower third holds few candidates (no cheap difficulty)
LOW_CRESTS = {"crest south", "crest low west", "crest low middle"}


# ---- tuning (written by `mfl.py tune`; held out 3.9/48, tuning seeds 3.9/48, band 3.04-4.29)
TUNING = {
    "blue": [[497.6, 278.8], [452.6, 274.4], [409.9, 266.2], [370.7, 254.4], [600.9, 226.1], [564.0, 232.7], [525.0, 234.9], [486.0, 232.7], [525.0, 116.0], [554.4, 167.0], [495.6, 167.0], [442.1, 333.2], [675.7, 400.1], [578.0, 512.0], [305.7, 293.7], [340.6, 290.8], [375.4, 291.8], [290.0, 258.0], [279.9, 190.5], [265.7, 149.5], [277.6, 109.6], [215.2, 255.1]]
}
