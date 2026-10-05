"""The layout-authoring helper: trace a scene's features with pegs and bricks at legal spacing, and the pre-flight.

A scene recipe carries `features`: polylines drawn over the scene image in board units (800 x 600), named for what
they are ("dome crown", "road", "cactuar outline"). `py -3 mfl.py trace <scene>` draws them on the graded scene over a
50-unit grid so they can be read off and corrected. A layout script then turns them into pieces:

    b = Board(recipe)
    b.trace("road", spacing=40, r=9, orange="stops")       # dotted pegs along a feature, snapped to legal spacing
    b.outline("cactuar", offset=18, spacing=36)            # an even ring just outside a closed silhouette
    b.bricks_along("ridge", length=26, gap=2.5)             # a continuous line of bricks (a crest, a deck)
    b.arc_bricks(400, 300, 120, 200, 140, n=5)              # bricks along an arc (a dome's crown)
    b.key("eye", orange=True, green=False)                  # the subject's features: orange candidates, never green

Every placement is checked as it is made (`fits`): bounds, the launcher's swing, the bucket, overlaps, saddles,
cradles at a brick's end and wall pinches. A point that does not fit is skipped and reported, so a traced line parts
round what is already there instead of breaking a rule. `check()` is the full pre-flight (level-method.md section 4,
plus format v2's greens and the bucket's lane); the build refuses a level it faults.
"""
import json
import math

import numpy as np

from . import paths  # noqa: F401  (the design kit on the path)
from layout import (LEFT, RIGHT, Layout, brick_samples, direct_reach, in_bounds, point_at, resample,  # noqa: F401
                    smooth_path)


def saddle_deg(gap, r=10.0, ball=6.0):
    """How steep a line of two pegs `gap` apart must be before a ball resting on both rolls out of the dip between
    them (layout.saddle_deg with the pegs' own radius)."""
    half = r + gap / 2
    c = r + ball
    return 0.0 if half >= c else math.degrees(math.asin(half / c))


def point_in_poly(x, y, poly):
    inside = False
    n = len(poly)
    for i in range(n):
        (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % n]
        if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
            inside = not inside
    return inside

BALL = 12.0
DOTTED_GAP = 14.0             # pegs on a dotted line: 14 or more between surfaces (34 centre to centre at r 10)


def _r1(v):
    return float(round(v, 1))


def offset_polyline(pts, d, closed=False):
    """The polyline moved `d` units along its left normal (for a closed shape drawn clockwise on screen, outward)."""
    P = np.asarray(pts, np.float64)
    n = len(P)
    out = []
    for i in range(n):
        a = P[(i - 1) % n] if (closed or i > 0) else P[i]
        b = P[(i + 1) % n] if (closed or i < n - 1) else P[i]
        t = b - a
        ln = math.hypot(*t) or 1.0
        nx, ny = -t[1] / ln, t[0] / ln
        out.append((P[i][0] + nx * d, P[i][1] + ny * d))
    return out


def signed_area(pts):
    P = np.asarray(pts, np.float64)
    return 0.5 * float(np.sum(P[:, 0] * np.roll(P[:, 1], -1) - np.roll(P[:, 0], -1) * P[:, 1]))


class Board(Layout):
    """A layout under construction (pegs, then bricks, in engine order) with format v2's canBeGreen."""

    def __init__(self, recipe=None):
        super().__init__()
        self.recipe = recipe or {}
        self.features = (self.recipe.get("features") or {})
        self.skipped = []           # (where, why) for points a trace could not place
        self.runs = []              # brick runs (kept for the authors' notes; the cup check is geometric)
        self.subjects = []          # (name, polygon): silhouettes whose interior stays free of pegs

    # ---- features
    def f(self, name, smooth=None):
        """A feature polyline from the scene recipe (board units). smooth=n passes it through Catmull-Rom."""
        feat = self.features[name]
        pts = [tuple(p) for p in (feat["points"] if isinstance(feat, dict) else feat)]
        sm = smooth if smooth is not None else (feat.get("smooth", 0) if isinstance(feat, dict) else 0)
        return smooth_path(pts, sm) if sm else pts

    def circle(self, name):
        """A circle feature: (cx, cy, R, from, sweep), the arc's start and sweep in degrees (default the upper half)."""
        feat = self.features[name]
        cx, cy, R = feat["circle"]
        return cx, cy, R, feat.get("from", 180.0), feat.get("sweep", 180.0)

    def _pts(self, src):
        return self.f(src) if isinstance(src, str) else [tuple(p) for p in src]

    # ---- pieces (the rich pass's Layout with canBeGreen added)
    def peg(self, x, y, orange=False, r=10.0, move=None, tag="", green=True):
        p = super().peg(x, y, orange=orange, r=r, move=move, tag=tag)
        p["canBeGreen"] = bool(green)
        return p

    def line(self, x1, y1, x2, y2, t=12.0, orange=False, tag="", green=True):
        b = super().line(x1, y1, x2, y2, t=t, orange=orange, tag=tag)
        b["canBeGreen"] = bool(green)
        return b

    def arc(self, cx, cy, R, start, sweep, t=12.0, orange=False, tag="", green=True):
        b = super().arc(cx, cy, R, start, sweep, t=t, orange=orange, tag=tag)
        b["canBeGreen"] = bool(green)
        return b

    # ---- legality of one placement
    def why_not(self, x, y, r=10.0, gap=DOTTED_GAP, group=None, skip_group=False, inside_ok=False):
        """None when a still round peg of radius r fits at (x, y); otherwise the reason. `group`: a mover's drift
        (dx, dy, period); movers drifting the same way keep a fixed spacing, so they are checked as still pegs (or,
        with skip_group, not at all: a point further along the drift, where they have moved too)."""
        if not in_bounds(x, y, r):
            return "off the board (walls, launcher's swing or bucket)"
        g = min(x - r - 75.5, 724.5 - x - r)
        if 0.5 < g < 12.5:
            return f"wall pinch {g:.1f}"
        for p in self.pegs:
            if "move" in p and (self._drift(p) != group or skip_group):
                continue
            d = math.hypot(p["x"] - x, p["y"] - y) - p.get("r", 10) - r
            if d < gap:
                return f"{d:.1f} from the peg at ({p['x']:.0f}, {p['y']:.0f})"
        for b in self.bricks:
            dmin, near = min((math.hypot(x - bx, y - by), (bx, by)) for (bx, by) in brick_samples(b, 1.0))
            gb = dmin - r - b["thickness"] / 2
            if gb < 13.0 or (gb < 16.0 and y < near[1] - 2):
                return f"{gb:.1f} from a brick"
        if not inside_ok:
            for name, poly in self.subjects:
                if point_in_poly(x, y, poly):
                    return f"inside the subject '{name}'"
        for m in self.pegs:
            if "move" in m and self._drift(m) != group and self._mover_near(m, x, y, r):
                return "in a mover's path"
        return None

    @staticmethod
    def _drift(p):
        """A mover's group: movers in one group keep fixed spacing (the engine moves every mover on one clock). A still
        peg is None; an orbit is keyed by its centre, period and turn, so a still placement still sees its path."""
        mv = p.get("move")
        if not mv:
            return None
        if mv["kind"] == "slide":
            return ("slide", round(mv["x"] - p["x"], 1), round(mv["y"] - p["y"], 1), mv["period"])
        return ("orbit", mv["x"], mv["y"], mv["period"], mv.get("clockwise", True))

    def _mover_near(self, m, x, y, r):
        mv = m["move"]
        for k in range(72):
            ph = 2 * math.pi * k / 72
            if mv["kind"] == "orbit":
                R = math.hypot(m["x"] - mv["x"], m["y"] - mv["y"])
                a = math.atan2(m["y"] - mv["y"], m["x"] - mv["x"]) + ph
                px, py = mv["x"] + R * math.cos(a), mv["y"] + R * math.sin(a)
            else:
                s = (1 - math.cos(ph)) / 2
                px, py = m["x"] + (mv["x"] - m["x"]) * s, m["y"] + (mv["y"] - m["y"]) * s
            if math.hypot(px - x, py - y) - m.get("r", 10) - r < BALL:
                return True
        return False

    def fits(self, x, y, r=10.0, gap=DOTTED_GAP):
        return self.why_not(x, y, r, gap) is None

    def place(self, x, y, r=10.0, gap=DOTTED_GAP, inside=False, **kw):
        """A peg at (x, y) if it fits; else None (and the reason is kept in `skipped`). inside=True places a feature of
        the subject inside its silhouette (an eye, a mouth)."""
        why = self.why_not(x, y, r, gap, inside_ok=inside)
        if why:
            self.skipped.append(((round(x), round(y)), kw.get("tag", ""), why))
            return None
        p = self.peg(x, y, r=r, **kw)
        if inside:
            p["_inside"] = True
        return p

    def subject(self, src):
        """Declares a closed feature as the subject's silhouette: traces and placements keep out of its interior and
        the pre-flight faults any peg inside it that is not a placed feature."""
        self.subjects.append((src if isinstance(src, str) else "subject", self._pts(src)))

    # ---- tracing
    def trace(self, src, spacing=None, r=10.0, orange=False, green=True, offset=0.0, start=0.0, end_trim=0.0,
              tag="", closed=False, gap=DOTTED_GAP, nudge=4.0):
        """Dotted pegs along a feature (name or points). spacing defaults to the legal minimum for r (2r + 14).
        orange: False, True, "every:n" (every n-th), or a set of indices along the line. A point that does not fit is
        nudged up to `nudge` units along the line, then skipped."""
        pts = self._pts(src)
        if closed and pts[0] != pts[-1]:
            pts = pts + [pts[0]]
        if offset:
            pts = offset_polyline(pts, offset, closed=closed)
        sp = spacing or (2 * r + gap)
        placed = []
        for k, (x, y) in enumerate(resample(pts, sp, start, end_trim)):
            if closed and k > 0 and placed and math.hypot(x - placed[0]["x"], y - placed[0]["y"]) < sp * 0.6:
                continue
            o = orange is True or (isinstance(orange, str) and orange.startswith("every:") and k % int(orange[6:]) == 0) \
                or (isinstance(orange, (set, tuple, list)) and k in orange)
            p = None
            for dn in (0.0, nudge / 2, -nudge / 2, nudge, -nudge) if nudge else (0.0,):
                q = point_at(np.asarray(pts, np.float64), min(max(0.0, start + k * sp + dn), 1e9))
                if self.fits(q[0], q[1], r, gap):
                    p = self.peg(q[0], q[1], orange=o, r=r, tag=tag, green=green)
                    break
            if p is None:
                self.skipped.append(((round(x), round(y)), tag, self.why_not(x, y, r, gap)))
            else:
                placed.append(p)
        return placed

    def stop(self, x, y, R=28.0, n=5, r=9.0, oranges=(0, 1, 4), green=False, start_deg=-90.0, tag="stop"):
        """A stop on a route or a star: a ring of n moons round (x, y). `oranges` are the ring's indices (clockwise from
        `start_deg`) that are orange candidates. A ring member that does not fit is skipped and reported."""
        out = []
        for k in range(n):
            a = math.radians(start_deg + 360.0 * k / n)
            p = self.place(x + R * math.cos(a), y + R * math.sin(a), r=r, orange=k in oranges, green=green, tag=tag)
            if p is not None:
                out.append(p)
        return out

    def outline(self, src, offset=18.0, spacing=None, r=10.0, **kw):
        """An even ring of pegs `offset` units outside a closed silhouette (its edge, never its face)."""
        pts = self._pts(src)
        # screen y points down: a shape drawn clockwise on screen has positive signed area, and its left normal points
        # in (the square (0,0) (1,0) (1,1) (0,1): the top edge runs +x, its left normal is +y)
        d = -offset if signed_area(pts) > 0 else offset
        return self.trace(pts, spacing=spacing, r=r, offset=d, closed=True, **kw)

    def bricks_along(self, src, length=26.0, gap=2.5, t=12.0, trim0=0.0, trim1=0.0, tag="", orange=False, green=True):
        out = super().bricks_along(self._pts(src), length=length, gap=gap, t=t, trim0=trim0, trim1=trim1, tag=tag,
                                   orange=orange)
        for b in out:
            b["canBeGreen"] = bool(green)
        self.runs.append(out)
        return out

    def arc_bricks(self, cx, cy, R, start, sweep, n=1, gap_deg=None, t=12.0, tag="", orange=False, green=True):
        """n arc bricks along one circle from `start` through `sweep` degrees (0 along +x, turning toward +y), with
        gaps of about 2.5 units between them (bricks either touch or leave a ball's width: no notches)."""
        g = gap_deg if gap_deg is not None else math.degrees(2.5 / R)
        each = (sweep - g * (n - 1)) / n
        out = []
        for k in range(n):
            a0 = start + k * (each + g)
            out.append(self.arc(cx, cy, R, a0, each, t=t, tag=tag, orange=orange, green=green))
        self.runs.append(out)
        return out

    def slide(self, x, y, dx, dy, period, r=9.0, gap=DOTTED_GAP, **kw):
        """A slide mover from (x, y) to (x + dx, y + dy) and back over `period` seconds, placed only if it keeps
        `gap` from every still piece along its whole path (sampled at nine points). Movers that slide together (same
        offset and period) keep their spacing, so a reflection drawn with them ripples as one."""
        group = ("slide", round(dx, 1), round(dy, 1), period)
        for k in range(9):
            t = k / 8
            why = self.why_not(x + dx * t, y + dy * t, r, gap, group=group, skip_group=k > 0)
            if why:
                self.skipped.append(((round(x), round(y)), kw.get("tag", ""), "mover: " + why))
                return None
        move = {"kind": "slide", "x": _r1(x + dx), "y": _r1(y + dy), "period": period}
        return self.peg(x, y, r=r, move=move, **kw)

    def greens_in_reach(self):
        """Pieces no first free flight touches (high in a corner) may never be green: a green should be a direct shot
        (method section 3). Pegs, movers (anywhere on their path) and bricks. Returns how many were changed."""
        n = 0
        for p in self.pegs + self.bricks:
            if p.get("canBeGreen", True) and not self.piece_in_reach(p):
                p["canBeGreen"] = False
                n += 1
        return n

    def key(self, x, y, orange=None, green=None, within=24.0):
        """Marks the piece nearest (x, y) as one of the subject's features: an orange candidate and/or never green."""
        best, bd = None, within
        for p in self.pegs:
            d = math.hypot(p["x"] - x, p["y"] - y)
            if d < bd:
                best, bd = p, d
        for b in self.bricks:
            sx, sy = brick_samples(b)[len(brick_samples(b)) // 2]
            d = math.hypot(sx - x, sy - y)
            if d < bd:
                best, bd = b, d
        if best is None:
            raise ValueError(f"no piece within {within} of ({x}, {y})")
        if orange is not None:
            best["canBeOrange"] = bool(orange)
        if green is not None:
            best["canBeGreen"] = bool(green)
        return best

    # ---- the pre-flight
    def mover_points(self, p, n=72):
        """A mover's centre at n points of its cycle (equal phase for every mover: the engine moves them all on one
        clock), or its one position if it is still."""
        mv = p.get("move")
        if not mv:
            return [(p["x"], p["y"])] * n
        out = []
        for k in range(n):
            ph = 2 * math.pi * k / n
            if mv["kind"] == "orbit":
                R = math.hypot(p["x"] - mv["x"], p["y"] - mv["y"])
                sgn = 1 if mv.get("clockwise", True) else -1
                a = math.atan2(p["y"] - mv["y"], p["x"] - mv["x"]) + sgn * ph
                out.append((mv["x"] + R * math.cos(a), mv["y"] + R * math.sin(a)))
            else:
                s = (1 - math.cos(ph)) / 2
                out.append((p["x"] + (mv["x"] - p["x"]) * s, p["y"] + (mv["y"] - p["y"]) * s))
        return out

    def piece_in_reach(self, piece):
        """Whether some first free flight touches the piece: a still peg, any point of a mover's path, any point of a
        brick."""
        if "kind" in piece:
            return any(direct_reach(x, y, piece["thickness"] / 2) for (x, y) in brick_samples(piece, 6.0))
        return any(direct_reach(x, y, piece.get("r", 10.0)) for (x, y) in self.mover_points(piece, 24))

    @staticmethod
    def _slope_deg(b):
        return math.degrees(math.atan2(abs(b["y2"] - b["y1"]), abs(b["x2"] - b["x1"]) + 1e-9))

    def check(self, verbose=True, number=1):
        """The pre-flight (level-method.md section 4 and section 2's line weights, format v2's greens, and the
        reviewers' round-1 cases). Every rule is geometric, so a layout cannot slip past it by how it was built."""
        P = []
        pegs, bricks = self.pegs, self.bricks
        still = [p for p in pegs if "move" not in p]
        movers = [p for p in pegs if "move" in p]
        n = len(pegs) + len(bricks)
        if not 60 <= n <= 160:
            P.append(f"{n} pieces (a full board is 60 to 160)")
        # bounds and wall pinches (pegs along their whole path, bricks along their length)
        for p in pegs:
            r = p.get("r", 10.0)
            for (x, y) in self.mover_points(p, 72 if "move" in p else 1):
                if not in_bounds(x, y, r):
                    P.append(f"peg ({p['x']}, {p['y']}) is off the board at ({x:.0f}, {y:.0f})")
                    break
            g = min(p["x"] - r - LEFT, RIGHT - p["x"] - r)
            if "move" not in p and 0.5 < g < 12.5:
                P.append(f"wall pinch {g:.1f} at ({p['x']}, {p['y']})")
        for j, b in enumerate(bricks):
            for (x, y) in brick_samples(b):
                if not in_bounds(x, y, b["thickness"] / 2):
                    P.append(f"brick {j} is off the board at ({x:.0f}, {y:.0f})")
                    break
            for (x, y) in brick_samples(b):
                g = min(x - b["thickness"] / 2 - LEFT, RIGHT - x - b["thickness"] / 2)
                if 0.5 < g < 12.5:
                    P.append(f"wall pinch {g:.1f} at brick {j}")
                    break
        # still pegs against each other: overlaps and saddles (touching pegs count: a ball rests on two touching level
        # pegs too), with the pegs' real radii
        for i in range(len(still)):
            for k in range(i + 1, len(still)):
                a, c = still[i], still[k]
                ra, rc = a.get("r", 10.0), c.get("r", 10.0)
                d = math.hypot(a["x"] - c["x"], a["y"] - c["y"]) - ra - rc
                slope = math.degrees(math.atan2(abs(a["y"] - c["y"]), abs(a["x"] - c["x"]) + 1e-9))
                if d < -0.5:
                    P.append(f"pegs overlap at ({a['x']}, {a['y']}) and ({c['x']}, {c['y']})")
                elif d < 11.8 and slope < saddle_deg(max(d, 0.0), (ra + rc) / 2):
                    P.append(f"saddle (gap {d:.1f}, {slope:.0f} deg) between ({a['x']}, {a['y']}) and ({c['x']}, {c['y']})")
        # pegs against bricks: overlap, cradle (under 13), and the wedge band above a brick (13-16: a ball rolling along
        # the brick jams under the peg)
        bs = [brick_samples(b, 1.0) for b in bricks]
        for p in still:
            r = p.get("r", 10.0)
            for j, b in enumerate(bricks):
                dmin, near = min((math.hypot(p["x"] - x, p["y"] - y), (x, y)) for (x, y) in bs[j])
                gap = dmin - r - b["thickness"] / 2
                if gap < -0.5:
                    P.append(f"peg ({p['x']}, {p['y']}) overlaps brick {j}")
                elif 0.5 < gap < 13.0:
                    P.append(f"cradle gap {gap:.1f} between peg ({p['x']}, {p['y']}) and brick {j}")
                elif 13.0 <= gap < 16.0 and p["y"] < near[1] - 2:
                    P.append(f"wedge band: peg ({p['x']}, {p['y']}) stands {gap:.1f} above brick {j} (16 or more)")
        # bricks against each other: notches, and cups judged by geometry: where two bricks touch, the joint must not
        # be a low point with both bricks rising away from it; an arc must not hang below its centre
        for i in range(len(bricks)):
            for k in range(i + 1, len(bricks)):
                a, c = bricks[i], bricks[k]
                d, pa, pc = min((math.hypot(x0 - x1, y0 - y1), (x0, y0), (x1, y1)) for (x0, y0) in bs[i][::2] for (x1, y1) in bs[k][::2])
                g = d - a["thickness"] / 2 - c["thickness"] / 2
                if 3.5 < g < 12.5:
                    P.append(f"notch {g:.1f} between brick {i} and brick {k}")
                elif g <= 3.5:
                    jy = (pa[1] + pc[1]) / 2
                    rise = []
                    for (smp, pt) in ((bs[i], pa), (bs[k], pc)):
                        far = [q for q in smp if 10.0 <= math.hypot(q[0] - pt[0], q[1] - pt[1]) <= 16.0]
                        rise.append(min(far, key=lambda q: q[1])[1] < jy - 1.5 if far else False)
                    if all(rise):
                        P.append(f"cup: a V where brick {i} and brick {k} meet at ({pa[0]:.0f}, {jy:.0f}) (both rise away)")
        for j, b in enumerate(bricks):
            if b["kind"] == "arc":
                for (x, y) in brick_samples(b, 2.0):
                    if y > b["y"] + 0.2 * b["r"]:
                        P.append(f"cup: arc brick {j} at ({x:.0f}, {y:.0f}) hangs below its centre (a bowl traps the ball)")
                        break
        # level decks: a run of line bricks under 6 degrees longer than 30 units holds a resting ball
        lines = [(j, b) for j, b in enumerate(bricks) if b["kind"] == "line" and self._slope_deg(b) < 6.0]
        seen = set()
        for j, b in lines:
            if j in seen:
                continue
            chain, todo = {j}, [j]
            while todo:
                q = todo.pop()
                for k, c in lines:
                    if k not in chain:
                        d = min(math.hypot(x0 - x1, y0 - y1) for (x0, y0) in bs[q][::3] for (x1, y1) in bs[k][::3])
                        if d - bricks[q]["thickness"] / 2 - c["thickness"] / 2 <= 3.5:
                            chain.add(k)
                            todo.append(k)
            seen |= chain
            span = sum(math.hypot(bricks[q]["x2"] - bricks[q]["x1"], bricks[q]["y2"] - bricks[q]["y1"]) for q in chain)
            if span > 30.0:
                P.append(f"level deck: bricks {sorted(chain)} lie under 6 degrees for {span:.0f} units (30 at most)")
        # movers: a ball's width (12) from every still peg, every brick, and every mover of another drift group at
        # the same moment
        for m in movers:
            pts = self.mover_points(m)
            rm = m.get("r", 10.0)
            hit = next(((x, y, p) for (x, y) in pts for p in still
                        if math.hypot(p["x"] - x, p["y"] - y) - p.get("r", 10.0) - rm < 12.0), None)
            if hit:
                P.append(f"mover from ({m['x']}, {m['y']}) passes within a ball of the peg at ({hit[2]['x']}, {hit[2]['y']})")
            for j, b in enumerate(bricks):
                if any(min(math.hypot(x - bx, y - by) for (bx, by) in bs[j][::2]) - rm - b["thickness"] / 2 < 12.0 for (x, y) in pts[::2]):
                    P.append(f"mover from ({m['x']}, {m['y']}) passes within a ball of brick {j}")
                    break
        for i in range(len(movers)):
            for k in range(i + 1, len(movers)):
                a, c = movers[i], movers[k]
                if self._drift(a) == self._drift(c):
                    continue
                pa, pc = self.mover_points(a), self.mover_points(c)
                if any(math.hypot(x0 - x1, y0 - y1) - a.get("r", 10.0) - c.get("r", 10.0) < 12.0 for (x0, y0), (x1, y1) in zip(pa, pc)):
                    P.append(f"movers from ({a['x']}, {a['y']}) and ({c['x']}, {c['y']}) come within a ball of each other")
        # the subject's interior stays open (section 2): no peg inside a declared silhouette unless it is a feature
        for name, poly in self.subjects:
            for p in pegs:
                if not p.get("_inside") and point_in_poly(p["x"], p["y"], poly):
                    P.append(f"peg ({p['x']}, {p['y']}) stands inside the subject '{name}' (keep its face open)")
        # orange candidates: 28-35 (25 and 3-7 more that carry the subject), every one in some first free flight
        cands = [p for p in pegs if p["canBeOrange"]] + [b for b in bricks if b["canBeOrange"]]
        if not 28 <= len(cands) <= 35:
            P.append(f"{len(cands)} orange candidates (28 to 35: the 25 and 3-7 more, so the deal varies)")
        for c in cands:
            if not self.piece_in_reach(c):
                P.append(f"orange candidate at ({c.get('x', c.get('x1'))}, {c.get('y', c.get('y1'))}) is out of every first free flight")
        # the spread rule, on still positions (a mover at its start) and brick midpoints
        allc = [(c["x"], c["y"]) if "kind" not in c else brick_samples(c)[len(brick_samples(c)) // 2] for c in cands]
        worst, where = 0, None
        for sx in range(75, 530, 5):
            for sy in range(40, 400, 5):
                k = sum(1 for (x, y) in allc if sx <= x < sx + 200 and sy <= y < sy + 200)
                if k > worst:
                    worst, where = k, (sx, sy)
        if worst > 10:
            P.append(f"spread: {worst} candidates in the 200 x 200 square at {where} (limit 10)")
        lo_l = sum(1 for (x, y) in allc if y >= 400 and x < 400)
        lo_r = sum(1 for (x, y) in allc if y >= 400 and x >= 400)
        if lo_l == 0 or lo_r == 0:
            P.append(f"spread: candidates below y 400: left {lo_l}, right {lo_r}")
        # greens (format v2: pegs, movers and bricks may all be green): 8 or more that may be green and are never
        # orange, and every piece that may be green within some first free flight
        greenable = [p for p in pegs + bricks if p.get("canBeGreen", True)]
        sure = [p for p in greenable if not p["canBeOrange"]]
        if len(sure) < 8:
            P.append(f"only {len(sure)} pieces may be green and are never orange (8 or more)")
        for p in greenable:
            if not self.piece_in_reach(p):
                P.append(f"green candidate at ({p.get('x', p.get('x1'))}, {p.get('y', p.get('y1'))}) is out of every first free flight")
        # the bucket's lane: never a full row in y 520-560 (pegs at their lowest, bricks too)
        lane = sum(1 for p in pegs if max(y for (_x, y) in self.mover_points(p, 24)) + p.get("r", 10.0) > 520)
        lane += sum(1 for b in bricks if max(y for (_x, y) in brick_samples(b)) + b["thickness"] / 2 > 520)
        if lane > 5:
            P.append(f"{lane} pieces reach into the bucket's lane (y 520-560; at most 5)")
        if verbose:
            print(f"  pieces {n} (pegs {len(pegs)}, bricks {len(bricks)}, movers {len(movers)}); candidates {len(cands)}; "
                  f"green-only {len(sure)}; skipped while tracing {len(self.skipped)}")
            for s_ in P:
                print("  !", s_)
        return P

    # ---- export (format v2)
    def level_json(self, level_id, name, scene):
        pegs = []
        for p in self.pegs:
            q = {"x": p["x"], "y": p["y"]}
            if p.get("r", 10.0) != 10.0:
                q["r"] = p["r"]
            q["canBeOrange"] = p["canBeOrange"]
            q["canBeGreen"] = p.get("canBeGreen", True)
            if "move" in p:
                q["move"] = p["move"]
            pegs.append(q)
        bricks = [dict(b) for b in self.bricks]
        return {"format": "moonfall-level", "version": 2, "id": level_id, "name": name, "scene": scene,
                "playfield": {"width": 800, "height": 600}, "pegs": pegs, "bricks": bricks}


def write_json(d, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(d, indent=2) + "\n", encoding="utf-8")


def selftest(verbose=True):
    """Known-bad layouts must be faulted for the right reason and a known-good one must pass (the reviewers' rule for
    every checker; round 1 added the critic's cases P1-P8 and C1)."""
    def grid_board():
        b = Board()
        k = 0
        for row, y in enumerate(range(200, 500, 50)):
            for x in range(110 + (row % 2) * 25, 700, 60):
                if b.fits(x, y):
                    b.peg(x, y, orange=(k % 2 == 0), green=(k % 2 == 1))
                    k += 1
        return b

    def faults(b):
        return b.check(verbose=False)
    cases = []                  # (name, problems, the fault expected: a substring of one problem, or None)
    cases.append(("a spread grid of 60-odd pegs", faults(grid_board()), None))
    b = grid_board(); b.peg(110, 205)
    cases.append(("two pegs overlapping", faults(b), "overlap"))
    b = grid_board(); b.peg(300, 512); b.peg(324, 513)
    cases.append(("a saddle (gap 4, level)", faults(b), "saddle"))
    b = grid_board(); b.peg(300, 513); b.peg(320.3, 513)
    cases.append(("two touching level pegs (P7)", faults(b), "saddle"))
    b = grid_board(); b.arc(150, 120, 30, 30, 120, t=10)
    cases.append(("a cup (an arc hanging below its centre)", faults(b), "cup: arc"))
    b = grid_board(); b.line(150, 140, 176, 152); b.line(178, 152, 204, 140)
    cases.append(("a V of two touching bricks (P1)", faults(b), "cup: a V"))
    b = grid_board(); b.line(150, 140, 176, 150); b.line(178, 151, 204, 161); b.line(206, 160, 232, 150)
    cases.append(("a three-brick cup laid with line(), outside any run (P1)", faults(b), "cup: a V"))
    b = grid_board(); b.arc(180, 175, 30, 200, 140, t=10); b.arc(236, 175, 30, 200, 140, t=10)
    cases.append(("a valley where two crowns' feet touch (P1)", faults(b), "cup: a V"))
    b = grid_board(); b.arc(400, 330, 150, 220, 100, t=12)
    cases.append(("a crown over open ground (no cup, no deck)", [q for q in faults(b) if "cup" in q or "deck" in q], None))
    b = grid_board()
    for k in range(7):
        b.line(150 + 30 * k, 150, 177 + 30 * k, 150)
    cases.append(("a level deck of seven bricks (P4)", faults(b), "level deck"))
    b = grid_board(); b.line(150, 156, 210, 136); b.peg(180, 115.5, r=9)
    cases.append(("a peg 14 above an 18-degree brick (P4, the wedge band)", faults(b), "wedge band"))
    b = grid_board()
    for x in (130, 200, 270, 340, 410, 480):
        b.peg(x, 548, r=8)
    cases.append(("a row of pegs in the bucket's lane", faults(b), "bucket's lane"))
    b = grid_board()
    for x in (110, 180, 250, 320, 390, 460):
        b.line(x, 548, x + 40, 548)
    cases.append(("a row of bricks in the bucket's lane (P5)", faults(b), "bucket's lane"))
    b = grid_board(); b.peg(84.5, 300, r=6)
    cases.append(("a wall pinch", faults(b), "wall pinch"))
    b = grid_board()
    for p in b.pegs[:12]:
        p["canBeOrange"] = False
    for p in b.pegs[12:]:
        p["canBeOrange"] = p["y"] < 330
    cases.append(("candidates only in the upper half", faults(b), "spread"))
    b = grid_board()
    for p in b.pegs:
        p["canBeGreen"] = False
    cases.append(("no piece may be green", faults(b), "may be green"))
    b = grid_board(); b.peg(110, 60, r=8, orange=True, move={"kind": "slide", "x": 130, "y": 60, "period": 4})
    cases.append(("an orange mover out of every flight (P6)", faults(b), "out of every first free flight"))
    b = grid_board(); b.line(110, 140, 128, 136, t=10, green=True)
    cases.append(("a green-able brick out of every flight (P8)", faults(b), "green candidate"))
    b = grid_board(); b.line(570, 100, 630, 100, t=10)
    b.peg(580, 135, r=9, move={"kind": "orbit", "x": 600, "y": 135, "period": 12})
    cases.append(("an orbit passing 4 from a brick (P2)", faults(b), "of brick"))
    b = grid_board()
    b.peg(560, 150, r=9, move={"kind": "slide", "x": 640, "y": 150, "period": 4})
    b.peg(640, 150, r=9, move={"kind": "slide", "x": 560, "y": 150, "period": 4})
    cases.append(("two slides that swap places (P2)", faults(b), "of each other"))
    b = Board(); b.peg(400, 300, r=9, move={"kind": "orbit", "x": 400, "y": 340, "period": 12})
    cases.append(("a placement on an orbit's path is refused (P2)", [] if b.why_not(400, 380) else ["accepted"], None))
    b = grid_board(); b.subject([(380, 120), (470, 120), (470, 190), (380, 190)]); b.peg(425, 150, r=9)
    cases.append(("a peg inside the subject's silhouette (C1)", faults(b), "inside the subject"))
    b = Board(); b.subject([(380, 230), (470, 230), (470, 330), (380, 330)])
    got = b.trace([(300, 280), (560, 280)], spacing=34, r=9)
    cases.append(("a trace parts round the subject (C1)", [] if all(not (380 < p["x"] < 470) for p in got) else ["inside"], None))
    b = Board()
    placed = b.trace([(120 + 40 * k, 300) for k in range(15)], r=10)
    ok_trace = all(math.hypot(a["x"] - c["x"], a["y"] - c["y"]) >= 34 - 1e-6 for a in placed for c in placed if a is not c)
    cases.append(("trace keeps legal spacing", [] if ok_trace else ["spacing"], None))
    b = Board(); b.peg(300, 300)
    got = b.trace([(240, 300), (360, 300)], spacing=20)
    cases.append(("trace skips points too close to a peg", [] if all(abs(p["x"] - 300) >= 34 for p in got) else ["too close"], None))
    ok = True
    for name, probs, want in cases:
        good_ = (not probs) if want is None else any(want in q for q in probs)
        ok &= good_
        if verbose:
            shown = next((q for q in probs if want and want in q), probs[0] if probs else None)
            print(f"  {'ok ' if good_ else 'BAD'} preflight: {name}: "
                  f"{'faulted: ' + shown if probs else 'passed'}{'' if want is None else ' (expected: ' + want + ')'}")
    return ok
