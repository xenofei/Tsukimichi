"""Level-layout kit for the scene method (docs/design/v9/rich/level-method.md): place pegs and bricks along the
lines a scene gives (a ridge, a coast, an outline, a trail) and check them before the engine's loader does.

Every piece is in the engine's 800 x 600 units. Bricks default to 12 thick (the art's slim moonstone slabs) and 28
long with 3 between, so a traced line reads as one line and a ball can roll along it.
"""
import math

import numpy as np

PIVOT = (400.0, 87.0)
CLEAR = 85.0                    # MoonfallLevelLoader.LauncherClearance
LOWEST = 560.0                  # MoonfallLevelLoader.LowestEdge
LEFT, RIGHT = 75.5, 724.5


def _r1(v):
    return float(round(v, 1))


class Layout:
    def __init__(self):
        self.pegs = []
        self.bricks = []
        self.tags = []              # what each piece means in the scene (pegs, then bricks), for the design notes

    # ---- pieces
    def peg(self, x, y, orange=False, r=10.0, move=None, tag=""):
        p = {"x": _r1(x), "y": _r1(y)}
        if r != 10.0:
            p["r"] = r
        p["canBeOrange"] = bool(orange)
        if move:
            p["move"] = move
        self.pegs.append(p)
        self.tags.append(("peg", tag))
        return p

    def line(self, x1, y1, x2, y2, t=12.0, orange=False, tag=""):
        b = {"kind": "line", "x1": _r1(x1), "y1": _r1(y1), "x2": _r1(x2), "y2": _r1(y2), "thickness": t,
             "canBeOrange": bool(orange)}
        self.bricks.append(b)
        self.tags.append(("brick", tag))
        return b

    def arc(self, cx, cy, R, start, sweep, t=12.0, orange=False, tag=""):
        b = {"kind": "arc", "x": _r1(cx), "y": _r1(cy), "r": _r1(R), "start": _r1(start), "sweep": _r1(sweep),
             "thickness": t, "canBeOrange": bool(orange)}
        self.bricks.append(b)
        self.tags.append(("brick", tag))
        return b

    # ---- lines of pieces
    def pegs_along(self, pts, spacing, orange_every=0, start=0.0, end_trim=0.0, tag="", orange_at=()):
        """Pegs every `spacing` along a polyline; orange_every=n makes every n-th one an orange candidate."""
        out = []
        for k, (x, y) in enumerate(resample(pts, spacing, start, end_trim)):
            o = (orange_every and k % orange_every == 0) or k in orange_at
            out.append(self.peg(x, y, orange=o, tag=tag))
        return out

    def bricks_along(self, pts, length=28.0, gap=3.0, t=12.0, trim0=0.0, trim1=0.0, tag="", orange=False):
        """Straight bricks laid end to end along a polyline (each `length` long, `gap` apart)."""
        P_ = np.asarray(pts, np.float64)
        seg = np.sqrt(((P_[1:] - P_[:-1]) ** 2).sum(1))
        total = seg.sum()
        usable = total - trim0 - trim1
        n = max(1, int((usable + gap) // (length + gap)))
        L_ = (usable - gap * (n - 1)) / n
        s = trim0
        out = []
        for _ in range(n):
            a, b = point_at(P_, s), point_at(P_, s + L_)
            out.append(self.line(a[0], a[1], b[0], b[1], t=t, tag=tag, orange=orange))
            s += L_ + gap
        return out

    def ring(self, cx, cy, R, n, start_deg=-90.0, span_deg=360.0, orange=False, tag="", skip=()):
        out = []
        closed = abs(span_deg) >= 359.9
        for k in range(n):
            if k in skip:
                continue
            a = math.radians(start_deg + span_deg * k / (n if closed else max(1, n - 1)))
            out.append(self.peg(cx + R * math.cos(a), cy + R * math.sin(a), orange=orange, tag=tag))
        return out

    # ---- export and checks
    def as_level(self):
        return self.pegs, self.bricks

    def counts(self):
        o = sum(p["canBeOrange"] for p in self.pegs) + sum(b["canBeOrange"] for b in self.bricks)
        return {"pegs": len(self.pegs), "bricks": len(self.bricks), "maybeOrange": o,
                "movers": sum(1 for p in self.pegs if "move" in p)}

    def check(self, verbose=True):
        """Pre-flight: bounds and overlaps (the loader's rules), plus cradling gaps a ball would rest in."""
        problems = []
        for i, p in enumerate(self.pegs):
            r = p.get("r", 10.0)
            if not in_bounds(p["x"], p["y"], r):
                problems.append(f"peg {i} ({p['x']}, {p['y']}) out of bounds")
        for j, b in enumerate(self.bricks):
            for (x, y) in brick_samples(b):
                if not in_bounds(x, y, b["thickness"] / 2):
                    problems.append(f"brick {j} out of bounds at ({x:.0f}, {y:.0f})")
                    break
        still = [p for p in self.pegs if "move" not in p]
        for i in range(len(still)):
            for k in range(i + 1, len(still)):
                a, c = still[i], still[k]
                d = math.hypot(a["x"] - c["x"], a["y"] - c["y"]) - a.get("r", 10) - c.get("r", 10)
                slope = math.degrees(math.atan2(abs(a["y"] - c["y"]), abs(a["x"] - c["x"]) + 1e-9))
                if d < -0.5:
                    problems.append(f"pegs overlap at ({a['x']}, {a['y']}) and ({c['x']}, {c['y']})")
                elif 0.5 < d < 11.8 and slope < saddle_deg(d):
                    # two pegs closer than a ball, nearly level: the dip between them is a saddle a ball rests in
                    problems.append(f"saddle (gap {d:.1f}, {slope:.0f} deg) between ({a['x']}, {a['y']}) and ({c['x']}, {c['y']})")
        movers = [p for p in self.pegs if "move" in p]
        for m in movers:
            mv = m["move"]
            for k in range(72):
                ph = 2 * math.pi * k / 72
                if mv["kind"] == "orbit":
                    R = math.hypot(m["x"] - mv["x"], m["y"] - mv["y"])
                    a = math.atan2(m["y"] - mv["y"], m["x"] - mv["x"]) + ph
                    x, y = mv["x"] + R * math.cos(a), mv["y"] + R * math.sin(a)
                else:
                    s = (1 - math.cos(ph)) / 2
                    x, y = m["x"] + (mv["x"] - m["x"]) * s, m["y"] + (mv["y"] - m["y"]) * s
                if not in_bounds(x, y, m.get("r", 10)):
                    problems.append(f"mover from ({m['x']}, {m['y']}) leaves the board at ({x:.0f}, {y:.0f})")
                    break
                hit = [p for p in still if math.hypot(p["x"] - x, p["y"] - y) - p.get("r", 10) - m.get("r", 10) < 12]
                if hit:
                    problems.append(f"mover from ({m['x']}, {m['y']}) passes within a ball of ({hit[0]['x']}, {hit[0]['y']})")
                    break
        for p in still:
            for j, b in enumerate(self.bricks):
                dmin = min(math.hypot(p["x"] - x, p["y"] - y) for (x, y) in brick_samples(b, 1.0))
                gap = dmin - p.get("r", 10) - b["thickness"] / 2
                if gap < -0.5:
                    problems.append(f"peg ({p['x']}, {p['y']}) overlaps brick {j}")
                elif 0.5 < gap < 11.8:
                    problems.append(f"cradle gap {gap:.1f} between peg ({p['x']}, {p['y']}) and brick {j}")
        if verbose:
            for s in problems:
                print("  !", s)
        return problems


def saddle_deg(gap, r=10.0, ball=6.0):
    """How steep a line of two pegs `gap` apart must be before a ball resting on both rolls out of the dip between
    them. Resting on both, the two contact normals sit asin(half / c) either side of the line's normal; with no
    friction the ball stays while the line's tilt is inside that angle."""
    half = r + gap / 2
    c = r + ball
    if half >= c:
        return 0.0
    return math.degrees(math.asin(half / c))


def in_bounds(x, y, r):
    return (x - r >= LEFT and x + r <= RIGHT and y + r <= LOWEST and y - r >= 0
            and math.hypot(x - PIVOT[0], y - PIVOT[1]) - r >= CLEAR)


def brick_samples(b, step=2.0):
    if b["kind"] == "line":
        L_ = math.hypot(b["x2"] - b["x1"], b["y2"] - b["y1"])
        n = max(1, int(L_ / step))
        return [(b["x1"] + (b["x2"] - b["x1"]) * k / n, b["y1"] + (b["y2"] - b["y1"]) * k / n) for k in range(n + 1)]
    L_ = b["r"] * math.radians(b["sweep"])
    n = max(1, int(L_ / step))
    return [(b["x"] + b["r"] * math.cos(math.radians(b["start"] + b["sweep"] * k / n)),
             b["y"] + b["r"] * math.sin(math.radians(b["start"] + b["sweep"] * k / n))) for k in range(n + 1)]


def point_at(P_, s):
    seg = np.sqrt(((P_[1:] - P_[:-1]) ** 2).sum(1))
    cum = np.concatenate([[0], np.cumsum(seg)])
    s = min(max(s, 0), cum[-1])
    i = min(np.searchsorted(cum, s, side="right") - 1, len(seg) - 1)
    t = (s - cum[i]) / max(seg[i], 1e-9)
    return tuple(P_[i] + (P_[i + 1] - P_[i]) * t)


def resample(pts, spacing, start=0.0, end_trim=0.0):
    P_ = np.asarray(pts, np.float64)
    seg = np.sqrt(((P_[1:] - P_[:-1]) ** 2).sum(1))
    total = seg.sum() - end_trim
    out = []
    s = start
    while s <= total + 1e-6:
        out.append(point_at(P_, s))
        s += spacing
    return out


def smooth_path(pts, n=12):
    """Catmull-Rom through the control points (a ridge or a coast drawn as a few clicks)."""
    P_ = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(P_) - 2):
        p0, p1, p2, p3 = (np.asarray(P_[i + k], np.float64) for k in (-1, 0, 1, 2))
        for k in range(n):
            t = k / n
            out.append(tuple(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                    + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)))
    out.append(tuple(P_[-2]))
    return out
