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
from layout import (Layout, brick_samples, direct_reach, in_bounds, point_at, resample, saddle_deg,  # noqa: F401
                    smooth_path)

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
        self.runs = []              # brick runs, for the cup check

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
    def why_not(self, x, y, r=10.0, gap=DOTTED_GAP):
        """None when a still round peg of radius r fits at (x, y); otherwise the reason."""
        if not in_bounds(x, y, r):
            return "off the board (walls, launcher's swing or bucket)"
        g = min(x - r - 75.5, 724.5 - x - r)
        if 0.5 < g < 12.5:
            return f"wall pinch {g:.1f}"
        for p in self.pegs:
            if "move" in p:
                continue
            d = math.hypot(p["x"] - x, p["y"] - y) - p.get("r", 10) - r
            if d < gap:
                return f"{d:.1f} from the peg at ({p['x']:.0f}, {p['y']:.0f})"
        for b in self.bricks:
            dmin = min(math.hypot(x - bx, y - by) for (bx, by) in brick_samples(b, 1.0))
            gb = dmin - r - b["thickness"] / 2
            if gb < 13.0:
                return f"{gb:.1f} from a brick"
        for m in self.pegs:
            if "move" in m and self._mover_near(m, x, y, r):
                return "in a mover's path"
        return None

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

    def place(self, x, y, r=10.0, gap=DOTTED_GAP, **kw):
        """A peg at (x, y) if it fits; else None (and the reason is kept in `skipped`)."""
        why = self.why_not(x, y, r, gap)
        if why:
            self.skipped.append(((round(x), round(y)), kw.get("tag", ""), why))
            return None
        return self.peg(x, y, r=r, **kw)

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
    def check(self, verbose=True, number=1):
        """The rich pass's pre-flight (bounds, overlaps, saddles, cradles, notches, wall pinches, candidates in reach,
        spread) plus: no cups, format v2's greens, the bucket's lane, the piece count."""
        problems = super().check(verbose=False)
        n = len(self.pegs) + len(self.bricks)
        if not 60 <= n <= 160:
            problems.append(f"{n} pieces (a full board is 60 to 160)")
        ncand = sum(p["canBeOrange"] for p in self.pegs) + sum(b["canBeOrange"] for b in self.bricks)
        if ncand > 35:
            problems.append(f"{ncand} orange candidates (25 to 35, so the subject always shows)")
        # never a cup: an arc brick that dips below its centre, or a run of bricks with a low point between two higher
        for j, b in enumerate(self.bricks):
            if b["kind"] == "arc":
                for (x, y) in brick_samples(b, 2.0):
                    if y > b["y"] + 0.2 * b["r"]:
                        problems.append(f"cup: arc brick {j} at ({x:.0f}, {y:.0f}) hangs below its centre (a bowl traps the ball)")
                        break
        for run in self.runs:
            mids = []
            for b in run:
                s = brick_samples(b, 2.0)
                mids.append(s[len(s) // 2][1])
            for k in range(1, len(mids) - 1):
                if mids[k] > mids[k - 1] + 1.0 and mids[k] > mids[k + 1] + 1.0:
                    problems.append(f"cup: brick run dips at y {mids[k]:.0f} (bricks only on concave-down lines)")
        # greens (format v2): enough pegs that may be green and are never orange, each in a direct flight's reach
        greenable = [p for p in self.pegs if p.get("canBeGreen", True) and "move" not in p]
        sure = [p for p in greenable if not p["canBeOrange"]]
        if len(sure) < 8:
            problems.append(f"only {len(sure)} still pegs may be green and are never orange (8 or more)")
        for p in greenable:
            if not direct_reach(p["x"], p["y"], p.get("r", 10.0)):
                problems.append(f"green candidate ({p['x']:.0f}, {p['y']:.0f}) is out of every first free flight")
        # the bucket's lane: never a full row in y 520-560
        lane = [p for p in self.pegs if p["y"] + p.get("r", 10) > 520]
        if len(lane) > 5:
            problems.append(f"{len(lane)} pegs reach into the bucket's lane (y 520-560; at most 5)")
        if verbose:
            print(f"  pieces {n} (pegs {len(self.pegs)}, bricks {len(self.bricks)}); candidates {ncand}; "
                  f"green-only pegs {len(sure)}; skipped while tracing {len(self.skipped)}")
            for s_ in problems:
                print("  !", s_)
        return problems

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
    """Known-bad layouts must be faulted and a known-good one must pass (the reviewers' rule for every checker)."""
    def grid_board():
        b = Board()
        k = 0
        for row, y in enumerate(range(200, 500, 50)):
            for x in range(110 + (row % 2) * 25, 700, 60):
                if b.fits(x, y):
                    b.peg(x, y, orange=(k % 2 == 0), green=(k % 2 == 1))
                    k += 1
        return b
    cases = []                  # (name, problems, the fault expected: a substring of one problem, or None)
    cases.append(("a spread grid of 60-odd pegs", grid_board().check(verbose=False), None))
    b = grid_board(); b.peg(110, 205)
    cases.append(("two pegs overlapping", b.check(verbose=False), "overlap"))
    b = grid_board(); b.peg(300, 512); b.peg(324, 513)
    cases.append(("a saddle (gap 4, level)", b.check(verbose=False), "saddle"))
    b = grid_board(); b.arc(150, 120, 30, 30, 120, t=10)
    cases.append(("a cup (an arc hanging below its centre)", b.check(verbose=False), "cup: arc"))
    b = grid_board(); b.runs.append([b.line(150, 140, 176, 150), b.line(178, 151, 204, 161), b.line(206, 160, 232, 150)])
    cases.append(("a cup in a brick run", b.check(verbose=False), "cup: brick run"))
    b = grid_board()
    for p in b.pegs:
        p["canBeGreen"] = False
    cases.append(("no peg may be green", b.check(verbose=False), "may be green"))
    b = grid_board()
    for x in (130, 200, 270, 340, 410, 480):
        b.peg(x, 548, r=8)
    cases.append(("a row in the bucket's lane", b.check(verbose=False), "bucket's lane"))
    b = grid_board(); b.peg(84.5, 300, r=6)
    cases.append(("a wall pinch", b.check(verbose=False), "wall pinch"))
    b = grid_board()
    for p in b.pegs[:12]:
        p["canBeOrange"] = False
    for p in b.pegs[12:]:
        p["canBeOrange"] = p["y"] < 330
    cases.append(("candidates only in the upper half", b.check(verbose=False), "spread"))
    b = Board()
    placed = b.trace([(120 + 40 * k, 300) for k in range(15)], r=10)
    ok_trace = all(math.hypot(a["x"] - c["x"], a["y"] - c["y"]) >= 34 - 1e-6 for a in placed for c in placed if a is not c)
    cases.append(("trace keeps legal spacing", [] if ok_trace else ["spacing"], None))
    b = Board(); b.peg(300, 300)
    got = b.trace([(240, 300), (360, 300)], spacing=20)
    cases.append(("trace skips points too close to a peg", [] if all(abs(p["x"] - 300) >= 34 for p in got) else ["too close"], None))
    ok = True
    for name, probs, want in cases:
        if want is None:
            good_ = not probs
        else:
            good_ = any(want in q for q in probs)
        ok &= good_
        if verbose:
            print(f"  {'ok ' if good_ else 'BAD'} preflight: {name}: "
                  f"{'faulted: ' + probs[0] if probs else 'passed'}{'' if want is None else ' (expected: ' + want + ')'}")
    return ok
