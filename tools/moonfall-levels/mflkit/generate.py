"""The layout generator: `mfl.py generate <id> [--seed N] [--variants K]` writes candidate boards an author curates
instead of placing a hundred pieces by hand.

Inputs: the level's LEVEL (its layout file, or its charter row for a level not started, with `--scene`), the scene
recipe's features (the painter's FEATURES merged) and charter section 1's rules. Each variant:

1. reads the features as shapes: circles, closed silhouettes, polylines, point sets, segment sets, and regions (a sky,
   a sea: closed shapes named so, where the fill is drawn first). A feature sharing a word with the subject or the
   level's name is the subject; the others are the painting's secondary lines;
2. lays the subject by the technique (charter 1.5): crowns of curved brick on circles (concave down, an odd count so
   no joint sits on the apex), a moon over each crown; an even ring of moons 20 outside a silhouette (all round for a
   creature, the upper part for a landmark) with brick on its steep straight edges and its face's features inside;
   ring stops on a trail's or a set's points; dotted moons on routes and a constellation's lines; brick on steep
   crests and crowns fitted to convex ones (terrain, skylines, landmarks, lattices); movers where the technique moves
   (OR orbits its circles, DR drifts a line in slide movers, RF reflects the subject below its waterline);
3. traces the secondary lines while the budget allows, then fills the open board with structures (rings of moons,
   crowns of brick with a moon over them, chevrons opened at the keystone, brick runs, arcs and rows of moons), each
   mirrored about the middle when the variant is symmetric, until the piece count and the brick share are met;
4. covers every first shot (the Python free flights; the engine's sweep confirms), marks the sure greens and the
   candidates (60-90%, at most 30% in the lower third, the spread rule) and checks the pre-flight;
5. writes the variant as an ordinary layout file: explicit pieces in laying order, tagged with what each traces, and
   `b.candidates(exclude=EXCLUDE)` last, so an author edits it like a hand-written one. With the engine it then
   repairs what the sweep finds among the fill (dead aims get a moon in their lane, stuck rests lose the fill piece
   that holds them, unreached fill goes), and reports the loader, the sweep, reach, the rule's 48 games, the tuning
   seeds' figure and a preview composite.

Priors (piece count, brick share, symmetry) are the Peggle corpus's stage means: statistics only, no Peggle layout is
read or copied. Every random choice comes from random.Random(seed * 1000 + variant): the same arguments write the
same files. Every placement goes through the pre-flight's own rules (`Board.why_not`, `Board.brick_why_not`), so a
variant is built legal rather than repaired into legality.
"""
import json
import math
import random
import re

import numpy as np

from . import author, paths, scene
from .author import BRICK_T, DOTTED_GAP, PEG_R, Board, offset_polyline, point_in_poly, signed_area
from layout import PIVOT, brick_samples, free_flights, point_at, resample, smooth_path

OUT = paths.BUILD / "generate"
Y_MAX_PEG = 500.0          # the bucket's approach stays open (the lane rule counts y 520-560)
Y_MAX_BRICK = 490.0        # a brick's centre line
SITE_Y = (125.0, 485.0)    # where fill structures may be centred
SPACING = 2 * PEG_R + DOTTED_GAP

# Peggle's campaign stage means on the new engine (corpus summary, 9 October 2026): pieces, brick share, the share of
# pieces with a mirror image. Counts only, used as priors for a variant's targets.
PRIORS = {"mr": {"n": [97.2, 106.6, 102.4, 91.0, 107.0, 113.6, 99.2, 103.4, 109.4, 112.8, 116.8],
                 "bricks": [0.14, 0.41, 0.30, 0.58, 0.20, 0.22, 0.72, 0.67, 0.57, 0.33, 0.40],
                 "sym": [0.38, 0.41, 0.41, 0.62, 0.49, 0.76, 0.43, 0.67, 0.70, 0.36, 0.28]},
          "fs": {"n": [89.6, 84.0, 118.6, 83.2, 84.0, 105.8, 99.4, 97.4, 83.2, 101.4, 128.0, 116.8],
                 "bricks": [0.33, 0.51, 0.49, 0.42, 0.55, 0.47, 0.54, 0.51, 0.52, 0.49, 0.23, 0.34],
                 "sym": [0.38, 0.27, 0.29, 0.36, 0.22, 0.51, 0.20, 0.18, 0.25, 0.55, 0.57, 1.00]}}

TECHNIQUES = [("TR", ("trail",)), ("TE", ("terrain",)), ("LM", ("landmark",)), ("CR", ("creature", "in outline")),
              ("CO", ("constellation",)), ("SP", ("set of points",)), ("OR", ("orbit",)), ("SB", ("silhouette band", "skyline")),
              ("RF", ("reflection",)), ("LS", ("light and shadow",)), ("PR", ("procession",)), ("LA", ("lattice",)),
              ("DR", ("drift", "slide movers"))]
CODES = [c for c, _ in TECHNIQUES]
OUTLINED = {"CR", "LM"}                                # a closed subject is outlined and its face kept open
# the words a technique's own features go by (a constellation's stars and lines, a trail's road and stops)
TECH_WORDS = {"CO": {"star", "line", "constellation"}, "TR": {"road", "route", "trail", "path", "stop", "walk"},
              "SP": {"point", "star", "city"}, "LS": {"lamp", "light", "beam", "glint", "pool", "reflection"},
              "SB": {"roof", "roofline", "skyline"}, "TE": {"ridge", "crest", "summit", "wall"},
              "OR": {"orbit", "track", "ring"}, "DR": {"crest", "wave", "streak", "cloud"},
              "RF": {"reflection"}, "LA": {"ring", "spoke", "lattice"}, "PR": {"lantern", "cord", "street"}}
BRICKY = {"TE", "SB", "LM", "LA", "CR"}               # steep lines of the subject in brick; the others dot them
STOPS = {"TR", "SP", "PR"}                             # a point of the subject is a ring stop
STOP_WORDS = {"the", "and", "with", "from", "over", "under", "into", "onto", "its", "his", "her", "their", "each",
              "one", "two", "three", "great", "own", "old", "new", "night", "beneath", "above", "below", "along",
              "round", "around", "through", "partly", "drawn", "traced", "outline", "line", "lines"}
SET_WORDS = {"feet", "teeth", "people", "eyes", "stars", "tips", "lamps", "posts", "columns", "windows", "stones"}
REGION_WORDS = ("sky", "sea", "water", "field", "region", "area", "ground", "desert", "plain")
WATER_WORDS = ("water", "sea", "shore", "horizon", "lake", "reflect", "harbour", "bay", "river")


def technique_code(text):
    """A charter technique code (TR, LM, ...) from a code or a LEVEL's technique sentence."""
    t = (text or "").strip()
    if t.upper() in CODES:
        return t.upper()
    low = t.lower()
    for code, keys in TECHNIQUES:
        if any(k in low for k in keys):
            return code
    return "LM"


def words(text):
    out = set()
    for w in re.findall(r"[a-z]+", str(text).lower().replace("'s", "")):
        if len(w) >= 3 and w not in STOP_WORDS:
            out.add(w[:-1] if w.endswith("s") and len(w) > 4 else w)
    return out


def charter_row(level_id):
    """The level's row of charter section 2 (code, id, name, subject, technique code), or None."""
    for line in (paths.LEVELS / "charter.md").read_text(encoding="utf-8").splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) >= 5 and cells[1] == level_id:
            return {"code": cells[0], "id": cells[1], "name": cells[2], "subject": cells[3], "tech": cells[4]}
    return None


def level_meta(level_id, scene_name=None):
    """LEVEL for the id: its layout file's, else its charter row's (the scene then comes from `scene_name`)."""
    from .build import load_layout
    row = charter_row(level_id)
    if (paths.LAYOUTS / f"{level_id}.py").exists():
        meta = dict(load_layout(level_id).LEVEL)
    elif row:
        stage = int(re.findall(r"(\d+)-\d+", row["code"])[0])
        meta = dict(id=level_id, name=row["name"], stage=stage, number=int(level_id.split("-")[1]), scene=None,
                    subject=row["subject"], technique=row["tech"])
    else:
        raise SystemExit(f"{level_id}: no layout and no charter row")
    if scene_name:
        meta["scene"] = scene_name
    if not meta.get("scene"):
        raise SystemExit(f"{level_id}: name its scene recipe with --scene <name>")
    meta["code"] = technique_code(row["tech"] if row and row["tech"] in CODES else meta.get("technique"))
    return meta


# ---- the recipe's features as shapes
def _num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def _pt(v):
    return isinstance(v, (list, tuple)) and 2 <= len(v) <= 4 and _num(v[0]) and _num(v[1])


def _pairs(v):
    """Every coordinate pair inside a painter's nested data (lamps, signs, heads), in order; index pairs and depths
    fall outside the opening and are left out."""
    if _pt(v):
        return [(float(v[0]), float(v[1]))] if 60 <= v[0] <= 740 and 20 <= v[1] <= 580 else []
    if isinstance(v, dict):
        return [q for k in v for q in _pairs(v[k])]
    if isinstance(v, (list, tuple)):
        return [q for x in v for q in _pairs(x)]
    return []


def _length(pts):
    return sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(pts, pts[1:]))


def _poly(name, pts, plain):
    if len(pts) == 1:
        return dict(kind="points", pts=pts)
    per = _length(pts) + math.hypot(pts[-1][0] - pts[0][0], pts[-1][1] - pts[0][1])
    gap = math.hypot(pts[-1][0] - pts[0][0], pts[-1][1] - pts[0][1])
    area = abs(signed_area(pts))
    if len(pts) >= 4 and gap <= 0.25 * per and area >= 0.25 * (per / 4) ** 2:
        big = area > 0.35 * 650 * 550
        return dict(kind="region" if big or any(w in name.lower() for w in REGION_WORDS) else "silhouette", pts=pts)
    last = name.lower().split()[-1] if name.split() else ""
    if plain and (last in SET_WORDS or (last.endswith("s") and not last.endswith("ss"))):
        return dict(kind="points", pts=pts)
    return dict(kind="polyline", pts=pts)


def shape(name, v):
    """One feature as a shape dict (kind: circle, polyline, silhouette, region, points, segments), or None."""
    if isinstance(v, dict) and "circle" in v:
        cx, cy, R = v["circle"]
        return dict(kind="circle", c=(float(cx), float(cy), float(R)), a0=float(v.get("from", 180.0)),
                    sw=float(v.get("sweep", 180.0)))
    if isinstance(v, dict) and "ellipse" in v:
        cx, cy, rx, ry = v["ellipse"][:4]
        return dict(kind="polyline", pts=[(cx + rx * math.cos(math.radians(a)), cy + ry * math.sin(math.radians(a)))
                                          for a in range(190, 351, 10)])
    if isinstance(v, dict) and "points" in v and all(_pt(p) for p in v["points"]):
        pts = [(float(p[0]), float(p[1])) for p in v["points"]]
        sm = v.get("smooth", 0)
        return _poly(name, smooth_path(pts, sm) if sm and len(pts) > 2 else pts, plain=False)
    if isinstance(v, (list, tuple)) and v:
        if all(_num(c) for c in v):
            return dict(kind="points", pts=[(float(v[0]), float(v[1]))]) if len(v) in (2, 3) else None
        if all(_pt(p) for p in v):
            return _poly(name, [(float(p[0]), float(p[1])) for p in v], plain=True)
        if all(isinstance(s, (list, tuple)) and len(s) == 2 and all(_pt(q) for q in s) for s in v):
            return dict(kind="segments", segs=[[(float(a[0]), float(a[1])), (float(c[0]), float(c[1]))] for a, c in v])
        return None
    if isinstance(v, dict):
        pts = _pairs(v)
        return dict(kind="points", pts=pts) if pts else None
    return None


def shapes(recipe, subject_words):
    out = []
    for name, v in (recipe.get("features") or {}).items():
        sh = shape(name, v)
        if sh is None:
            continue
        sh["name"] = name
        sh["subject"] = sh["kind"] != "region" and bool(words(name) & subject_words)
        out.append(sh)
    return out


def rdp(pts, eps):
    """Ramer-Douglas-Peucker: the polyline's corners."""
    if len(pts) < 3:
        return list(pts)
    (x0, y0), (x1, y1) = pts[0], pts[-1]
    L = math.hypot(x1 - x0, y1 - y0) or 1e-9
    d = [abs((x1 - x0) * (y0 - y) - (x0 - x) * (y1 - y0)) / L for (x, y) in pts[1:-1]]
    i = int(np.argmax(d)) + 1
    if d[i - 1] <= eps:
        return [pts[0], pts[-1]]
    return rdp(pts[:i + 1], eps)[:-1] + rdp(pts[i:], eps)


def fit_circle(pts):
    """Least-squares circle (cx, cy, R) through points."""
    P = np.asarray(pts, np.float64)
    A = np.c_[2 * P[:, 0], 2 * P[:, 1], np.ones(len(P))]
    sol, *_ = np.linalg.lstsq(A, (P ** 2).sum(1), rcond=None)
    cx, cy, c = sol
    return float(cx), float(cy), float(math.sqrt(max(c + cx * cx + cy * cy, 0.0)))


def _r1(v):
    return float(round(v, 1))


def line_brick(a, c, t=BRICK_T):
    return {"kind": "line", "x1": _r1(a[0]), "y1": _r1(a[1]), "x2": _r1(c[0]), "y2": _r1(c[1]), "thickness": t,
            "canBeOrange": False}


def arc_brick(cx, cy, R, start, sweep, t=BRICK_T):
    return {"kind": "arc", "x": _r1(cx), "y": _r1(cy), "r": _r1(R), "start": _r1(start), "sweep": _r1(sweep),
            "thickness": t, "canBeOrange": False}


def crown_specs(cx, cy, R, a0, a1, n, gap=2.5):
    """n arc bricks over [a0, a1] degrees, 2.5 units apart."""
    g = math.degrees(gap / R)
    each = (a1 - a0 - g * (n - 1)) / n
    return [arc_brick(cx, cy, R, a0 + k * (each + g), each) for k in range(n)]


def run_specs(pts, length, gap=2.5, trim0=0.0, trim1=0.0):
    """Straight bricks end to end along a polyline (Layout.bricks_along's spacing), not laid."""
    P = np.asarray(pts, np.float64)
    usable = _length(pts) - trim0 - trim1
    if usable < 0.6 * length:
        return []
    n = max(1, int((usable + gap) // (length + gap)))
    L = (usable - gap * (n - 1)) / n
    s, out = trim0, []
    for _ in range(n):
        out.append(line_brick(point_at(P, s), point_at(P, s + L)))
        s += L + gap
    return out


def mirror_spec(sp):
    if sp["kind"] == "line":
        return line_brick((800 - sp["x1"], sp["y1"]), (800 - sp["x2"], sp["y2"]), sp["thickness"])
    if sp["kind"] == "arc":
        return arc_brick(800 - sp["x"], sp["y"], sp["r"], 540 - (sp["start"] + sp["sweep"]), sp["sweep"], sp["thickness"])
    return dict(sp, x=800 - sp["x"])


def cup(a, c):
    """The pre-flight's cup by geometry for two touching bricks: their joint a low point both rise away from."""
    sa, sc = brick_samples(a, 1.0), brick_samples(c, 1.0)
    d, pa, pc = min((math.hypot(p[0] - q[0], p[1] - q[1]), p, q) for p in sa for q in sc)
    if d - a["thickness"] / 2 - c["thickness"] / 2 > 3.5:
        return False
    jy = (pa[1] + pc[1]) / 2
    rise = []
    for smp, pt in ((sa, pa), (sc, pc)):
        far = [q for q in smp if 10.0 <= math.hypot(q[0] - pt[0], q[1] - pt[1]) <= 16.0]
        rise.append(min(far, key=lambda q: q[1])[1] < jy - 1.5 if far else False)
    return all(rise)


class Generator:
    def __init__(self, meta, recipe, seed=1, variant=0):
        self.meta, self.recipe = meta, recipe
        self.code = meta["code"]
        self.tech = (meta.get("technique") or "").lower()
        self.rng = random.Random(seed * 1000 + variant)
        self.seed, self.variant = seed, variant
        self.b = Board(recipe)
        self.b.level_id = meta["id"]
        self.used = []                         # (feature, how, pieces) for the file's notes
        self.declared = []                     # silhouettes declared as the subject
        camp = "fs" if str(meta["id"]).startswith("expansion") else "mr"
        k = min(max(int(meta.get("stage", 1)) - 1, 0), len(PRIORS[camp]["n"]) - 1)
        rng = self.rng
        self.target_n = int(round(min(max(PRIORS[camp]["n"][k], 98), 112) + rng.uniform(-4, 4)))
        self.brick_target = min(max(PRIORS[camp]["bricks"][k], 0.32), 0.42) + rng.uniform(-0.02, 0.02)
        self.sym = rng.random() < max(PRIORS[camp]["sym"][k], 0.35)
        self.spacing = rng.choice((34.0, 35.0, 36.0, 38.0))
        self.cand_share = rng.uniform(0.64, 0.74)
        self.star_rings = rng.random() < 0.4
        self.low_target = rng.uniform(0.03, 0.07)
        self.fill_kind = rng.choices(["arcs", "lattice", "grid", "clusters"], weights=[0.35, 0.25, 0.2, 0.2])[0]
        sw = words(meta.get("subject", "")) | words(meta.get("name", "")) | TECH_WORDS.get(self.code, set())
        self.shapes = shapes(recipe, sw)
        self.regions = [s["pts"] for s in self.shapes if s["kind"] == "region"]

    # ---- counting
    @property
    def n(self):
        return len(self.b.pegs) + len(self.b.bricks)

    def note(self, name, how, before):
        k = self.n - before
        if k:
            self.used.append((name, how, k))

    # ---- legal placement
    def kept_out(self, x, y, rad, tag):
        """Fill stays off a painted moon (the recipe's ceilingExempt discs, at 0.7 of their radius)."""
        return tag.startswith(("fill", "lane")) and any(math.hypot(x - cx, y - cy) < 0.7 * r + rad
                                                        for (cx, cy, r) in self.recipe.get("ceilingExempt", ()))

    def put(self, x, y, tag, green=True, r=PEG_R, inside=False, gap=DOTTED_GAP, log=True):
        if not (30 <= y <= Y_MAX_PEG) or self.kept_out(x, y, r, tag):
            return None
        why = self.b.why_not(x, y, r, gap, inside_ok=inside)
        if why:
            if log:
                self.b.skipped.append(((round(x), round(y)), tag, why))
            return None
        return self.b.peg(x, y, r=r, tag=tag, green=green, inside=inside)

    def in_subject(self, brick):
        return any(point_in_poly(x, y, poly) for (_n, poly) in self.b.subjects for (x, y) in brick_samples(brick, 4.0))

    def put_bricks(self, specs, tag, green=True, run=True):
        laid = []
        for sp in specs:
            if max(y for (_x, y) in brick_samples(sp)) > Y_MAX_BRICK or self.in_subject(sp) \
                    or any(self.kept_out(x, y, sp["thickness"] / 2, tag) for (x, y) in brick_samples(sp, 6.0)):
                continue
            why = self.b.brick_why_not(sp, touching=laid[-1:] if run else ())
            if why is None and run and laid and cup(laid[-1], sp):
                why = "cup"
            if why:
                continue
            if sp["kind"] == "line":
                laid.append(self.b.line(sp["x1"], sp["y1"], sp["x2"], sp["y2"], t=sp["thickness"], tag=tag, green=green))
            else:
                laid.append(self.b.arc(sp["x"], sp["y"], sp["r"], sp["start"], sp["sweep"], t=sp["thickness"], tag=tag,
                                       green=green))
        return laid

    def dots(self, pts, tag, green=True, spacing=None, start=0.0, end_trim=0.0, nudge=6.0):
        """Dotted moons along a polyline at legal spacing, nudged along it or skipped where a point breaks a rule."""
        sp = spacing or self.spacing
        if _length(pts) < 1:
            return [p for p in [self.put(pts[0][0], pts[0][1], tag, green)] if p]
        P = np.asarray(pts, np.float64)
        out = []
        for k, _q in enumerate(resample(pts, sp, start, end_trim)):
            for dn in (0.0, nudge / 2, -nudge / 2, nudge, -nudge):
                x, y = point_at(P, max(0.0, start + k * sp + dn))
                p = self.put(x, y, tag, green, log=dn == -nudge)
                if p:
                    out.append(p)
                    break
        return out

    def ring(self, cx, cy, R, n, tag, green=True, start=-90.0, centre=False, log=False):
        out = [self.put(cx, cy, tag, green, log=log)] if centre else []
        for k in range(n):
            a = math.radians(start + 360.0 * k / n)
            out.append(self.put(cx + R * math.cos(a), cy + R * math.sin(a), tag, green, log=log))
        return [p for p in out if p]

    def snapshot(self):
        return len(self.b.pegs), len(self.b.bricks), len(self.b.tags)

    def rollback(self, snap):
        np_, nb, nt = snap
        del self.b.pegs[np_:], self.b.bricks[nb:], self.b.tags[nt:]

    # ---- the subject and the painting's lines
    def crown(self, sh, tag, green=False):
        cx, cy, R = sh["c"]
        a0, a1 = max(sh["a0"], 180.0), min(sh["a0"] + sh["sw"], 360.0)
        if a1 - a0 < 20 or R > 400 or R < 18:
            return []
        n = max(1, int(round(math.radians(a1 - a0) * R / 42.0)))
        n += 1 - n % 2
        laid = self.put_bricks(crown_specs(cx, cy, R, a0, a1, n), tag, green=green)
        if laid and a0 <= 270 <= a1:
            self.put(cx, cy - R - BRICK_T / 2 - 17 - PEG_R, tag, green=False)
        return laid

    def crest(self, pts, tag, green=True):
        """A convex-up run as a crown fitted to it (the format's arcs run to r 400), else None."""
        cx, cy, R = fit_circle(pts)
        ys = [p[1] for p in pts]
        if not (60 <= R <= 400) or cy <= sum(ys) / len(ys):
            return None
        angs = [math.degrees(math.atan2(y - cy, x - cx)) % 360 for (x, y) in pts]
        a0, a1 = max(min(angs), 182.0), min(max(angs), 358.0)
        if a1 - a0 < 12:
            return None
        # a wide crown's top is a deck in all but name (balls rest on it: the sweep's stuck rule found them on the
        # dunes): brick only where the crest slopes 12 degrees or more, its top dotted
        flat = (258.0, 282.0) if R > 100 else (270.0, 270.0)
        laid = []
        for s0, s1 in ((a0, min(a1, flat[0])), (max(a0, flat[1]), a1)):
            if s1 - s0 >= 6:
                n = max(1, int(round(math.radians(s1 - s0) * R / 40.0)))
                n += 1 - n % 2
                laid += self.put_bricks(crown_specs(cx, cy, R, s0, s1, n), tag, green=green)
        if R > 100 and a0 < flat[1] and a1 > flat[0]:
            top = [(cx + R * math.cos(math.radians(a)), cy + R * math.sin(math.radians(a)))
                   for a in np.linspace(max(a0, flat[0]), min(a1, flat[1]), 12)]
            laid += self.dots(top, tag, green)
        return laid

    def line_feature(self, pts, tag, bricky, green=True):
        """A polyline: its convex crests (90 or longer, gentler than 35 degrees: a dune, a roof, a dome's shoulder) as
        crowns fitted to them in every technique; steep straight stretches (14 degrees or more, 50 or longer) in brick
        when the technique lays brick; the rest dotted."""
        if _length(pts) < 24:
            return self.dots(pts, tag, green)
        D = resample(pts, 6.0)
        sgn = 1.0 if D[-1][0] >= D[0][0] else -1.0
        turn = []
        for i in range(len(D)):                   # the turn over 60 units each way, so a drawn polyline's corners spread
            a, m, c = D[max(i - 10, 0)], D[i], D[min(i + 10, len(D) - 1)]
            turn.append(((m[0] - a[0]) * (c[1] - m[1]) - (m[1] - a[1]) * (c[0] - m[0])) * sgn)
        lab = [sum(turn[max(i - 4, 0):i + 5]) > 300.0 for i in range(len(D))]
        i = 0
        while i < len(D):
            j = i
            while j + 1 < len(D) and lab[j + 1] == lab[i]:
                j += 1
            st = D[max(i - 1, 0):j + 2]
            (x0, y0), (x1, y1) = st[0], st[-1]
            gentle = math.degrees(math.atan2(abs(y1 - y0), abs(x1 - x0) + 1e-9)) < 35
            if not (lab[i] and gentle and _length(st) >= 90 and self.crest(st, tag, green)):
                self._runs(st, tag, bricky, green)
            i = j + 1

    def _runs(self, pts, tag, bricky, green):
        if not bricky:
            return self.dots(pts, tag, green)
        corners = rdp(pts, 5.0)
        runs, cur = [], [corners[0]]
        for a, c in zip(corners, corners[1:]):
            if len(cur) >= 2:
                d0 = math.atan2(cur[-1][1] - cur[-2][1], cur[-1][0] - cur[-2][0])
                d1 = math.atan2(c[1] - a[1], c[0] - a[0])
                if abs((d1 - d0 + math.pi) % (2 * math.pi) - math.pi) > math.radians(25):
                    runs.append(cur)
                    cur = [a]
            cur.append(c)
        runs.append(cur)
        for run in runs:
            (x0, y0), (x1, y1) = run[0], run[-1]
            L = _length(run)
            slope = math.degrees(math.atan2(abs(y1 - y0), abs(x1 - x0) + 1e-9))
            if slope >= 14 and L >= 50:
                if self.put_bricks(run_specs(run, self.rng.uniform(32, 40), trim0=10, trim1=10), tag, green=green):
                    continue
            self.dots(run, tag, green)

    def silhouette(self, sh, tag):
        """An even ring of moons 20 outside the silhouette, brick on its steep straight edges; all round for a
        creature, the upper part for a landmark (charter 1.5: CR, LM). The silhouette is declared: its face stays open."""
        pts = sh["pts"]
        if pts[0] == pts[-1]:
            pts = pts[:-1]
        self.b.subjects.append((sh["name"], pts))
        self.declared.append(sh["name"])
        d = -20.0 if signed_area(pts) > 0 else 20.0
        ring = offset_polyline(pts + [pts[0]], d, closed=True)
        ys = [p[1] for p in pts]
        top, h = min(ys), max(ys) - min(ys)
        limit = top + (0.62 * h if self.code == "LM" else 2 * h)
        corners = rdp(ring, 5.0)
        for a, c in zip(corners, corners[1:]):
            L = math.hypot(c[0] - a[0], c[1] - a[1])
            slope = math.degrees(math.atan2(abs(c[1] - a[1]), abs(c[0] - a[0]) + 1e-9))
            if L >= 48 and slope >= 20 and max(a[1], c[1]) <= limit:
                self.put_bricks(run_specs([a, c], self.rng.uniform(32, 38), trim0=14, trim1=14), tag, green=True)
        for (x, y) in resample(ring, 3.0):
            if y <= limit:
                self.put(x, y, tag, log=False)

    def points(self, sh, tag, subject):
        inside = [p for p in sh["pts"] if any(point_in_poly(p[0], p[1], poly) for (_n, poly) in self.outlines)]
        for (x, y) in inside:
            self.put(x, y, tag, green=False, r=9.0, inside=True)
        rest = [p for p in sh["pts"] if p not in inside]
        for (x, y) in rest:
            if subject and self.code in STOPS:
                self.ring(x, y, 30.0, 5, tag, green=False, log=True)
            elif subject and self.code == "CO" and self.star_rings and self.rng.random() < 0.4:
                self.ring(x, y, 36.0, 6, tag, green=False, centre=True, log=True)
            else:
                self.put(x, y, tag, green=not subject)

    def orbits(self):
        """OR (and a set of points with an orbit): moons orbiting the subject's circles (or its first point)."""
        circles = [s for s in self.shapes if s["kind"] == "circle" and s["subject"] and s["c"][2] <= 130]
        if not circles:
            pts = [s for s in self.shapes if s["kind"] == "points" and s["subject"]]
            circles = [dict(name=pts[0]["name"], c=(*pts[0]["pts"][0], 46.0))] if pts else []
        for i, sh in enumerate(circles[:3]):
            cx, cy, R = sh["c"]
            k = max(3, min(6, int(2 * math.pi * R / 90)))
            period = round(self.rng.uniform(9, 14), 1)
            cw = i % 2 == 0
            before = self.n
            for j in range(k):
                a = 2 * math.pi * j / k
                x, y = cx + R * math.cos(a), cy + R * math.sin(a)
                move = {"kind": "orbit", "x": _r1(cx), "y": _r1(cy), "period": period, "clockwise": cw}
                if self._mover_fits(x, y, move):
                    self.b.peg(x, y, move=move, tag=sh["name"] + " orbit")
            self.note(sh["name"], "orbit movers", before)

    def _mover_fits(self, x, y, move):
        p = {"x": _r1(x), "y": _r1(y), "move": move}
        g = Board._drift(p)
        for k, (px, py) in enumerate(self.b.mover_points(p, 36)):
            if not (30 <= py <= Y_MAX_PEG):
                return False
            if self.b.why_not(px, py, PEG_R, DOTTED_GAP, group=g, skip_group=k > 0, inside_ok=True):
                return False
        return True

    def drift(self):
        """DR: the most horizontal line of the subject (else of the painting) carried by slide movers."""
        lines = [s for s in self.shapes if s["kind"] == "polyline"]
        lines.sort(key=lambda s: (not s["subject"], abs(s["pts"][-1][1] - s["pts"][0][1]) / max(_length(s["pts"]), 1)))
        for i, sh in enumerate(lines[:2]):
            dx = (36.0 if i % 2 == 0 else -36.0)
            period = round(self.rng.uniform(7, 10), 1)
            before = self.n
            for (x, y) in resample(sh["pts"], 44.0):
                if self.n - before >= 8:
                    break
                if y <= Y_MAX_PEG:
                    self.b.slide(x, y, dx, 0.0, period, tag=sh["name"] + " drift")
            self.note(sh["name"], "slide movers (drift)", before)

    def reflect(self):
        """RF: the subject's moons above its waterline mirrored below it in slide movers that ripple as one."""
        water = [s for s in self.shapes if s["kind"] == "polyline" and any(w in s["name"].lower() for w in WATER_WORDS)]
        subj = [p for p, (_k, t) in zip(self.b.pegs, [t for t in self.b.tags if t[0] == "peg"])
                if "move" not in p and not t.startswith("fill")]
        if not subj:
            return
        yw = (sum(q[1] for q in water[0]["pts"]) / len(water[0]["pts"])) if water else max(p["y"] for p in subj) + 24
        before = self.n
        for p in sorted(subj, key=lambda p: p["y"], reverse=True):
            y2 = 2 * yw - p["y"]
            if p["y"] < yw and y2 <= Y_MAX_PEG and self.n - before < 12:
                self.b.slide(p["x"], y2, 16.0, 0.0, 5.0, tag="reflection")
        self.note("reflection", "slide movers (mirror)", before)

    def lay_features(self):
        sub = [s for s in self.shapes if s["subject"]]
        sec = [s for s in self.shapes if not s["subject"] and s["kind"] != "region"]
        if self.code == "OR" or "orbit" in self.tech:
            self.orbits()
        if self.code == "DR":
            self.drift()
        # an outlined subject's silhouettes, known before anything is laid, so its face's points go inside it
        self.outlines = [(s["name"], s["pts"][:-1] if s["pts"][0] == s["pts"][-1] else s["pts"])
                         for s in sub if s["kind"] == "silhouette" and self.code in OUTLINED]
        for group, is_sub in ((sub, True), (sec, False)):
            # brick first (crowns), then the points (each exactly on its painted spot), then the outlines and lines
            for sh in [s for s in group if s["kind"] == "circle"] + [s for s in group if s["kind"] == "points"] \
                    + [s for s in group if s["kind"] == "silhouette"]:
                if not is_sub and self.n >= 0.8 * self.target_n:
                    break
                before = self.n
                if sh["kind"] == "circle":
                    self.crown(sh, sh["name"], green=not is_sub)
                    self.note(sh["name"], "crown of brick", before)
                elif sh["kind"] == "points":
                    self.points(sh, sh["name"], is_sub)
                    self.note(sh["name"], "ring stops" if is_sub and self.code in STOPS else "moons", before)
                elif is_sub and self.code in OUTLINED:
                    self.silhouette(sh, sh["name"])
                    self.note(sh["name"], "outline (ring 20 outside, brick on steep edges)", before)
                else:
                    self.line_feature(sh["pts"] + [sh["pts"][0]], sh["name"], self.code in BRICKY)
                    self.note(sh["name"], "traced", before)
            for sh in group:
                if not is_sub and self.n >= 0.8 * self.target_n:
                    break
                before = self.n
                if sh["kind"] == "polyline":
                    pts = [(x, y) for (x, y) in sh["pts"]]
                    self.line_feature(pts, sh["name"], self.code in BRICKY)
                    self.note(sh["name"], "brick and dotted" if self.code in BRICKY else "dotted", before)
                elif sh["kind"] == "segments":
                    for a, c in sh["segs"]:
                        L = math.hypot(c[0] - a[0], c[1] - a[1])
                        slope = math.degrees(math.atan2(abs(c[1] - a[1]), abs(c[0] - a[0]) + 1e-9))
                        if self.code == "LA" and slope >= 14 and L >= 60:
                            self.put_bricks(run_specs([a, c], 36.0, trim0=14, trim1=14), sh["name"])
                        else:
                            self.dots([a, c], sh["name"], start=min(28.0, L / 3), end_trim=min(28.0, L / 3))
                    self.note(sh["name"], "dotted lines", before)
        if self.code == "RF":
            self.reflect()

    # ---- the fill
    def clearance(self, G):
        """Each grid point's clearance: distance to the nearest piece's edge, the walls and the launcher's swing;
        -1 inside a declared silhouette."""
        obs = []
        for p in self.b.pegs:
            for (x, y) in (self.b.mover_points(p, 24) if "move" in p else [(p["x"], p["y"])]):
                obs.append((x, y, p.get("r", PEG_R)))
        for bk in self.b.bricks:
            obs += [(x, y, bk["thickness"] / 2) for (x, y) in brick_samples(bk, 5.0)]
        D = np.minimum(G[:, 0] - 75.5, 724.5 - G[:, 0])
        D = np.minimum(D, np.hypot(G[:, 0] - PIVOT[0], G[:, 1] - PIVOT[1]) - 85.0)
        if obs:
            O = np.asarray(obs, np.float64)
            for i in range(0, len(O), 400):
                o = O[i:i + 400]
                d = np.hypot(G[:, 0:1] - o[None, :, 0], G[:, 1:2] - o[None, :, 1]) - o[None, :, 2]
                D = np.minimum(D, d.min(1))
        D[self.inside_mask] = -1.0
        return D

    def template(self, kind):
        """A fill structure's pieces round (0, 0): (bricks, pegs). Its parameters are drawn here, so a structure is
        drawn first and then given a site with room for it."""
        rng = self.rng
        if kind == "ring":
            n = rng.choice((5, 6, 6, 7, 8))
            R = {5: 32.0, 6: 36.0, 7: 41.0, 8: 46.0}[n]
            st = rng.choice((-90.0, -90.0 + 180.0 / n))
            pegs = [(R * math.cos(math.radians(st + 360.0 * k / n)), R * math.sin(math.radians(st + 360.0 * k / n)))
                    for k in range(n)]
            return [], pegs + ([(0.0, 0.0)] if R >= 36 and rng.random() < 0.5 else [])
        if kind == "arc":
            R = rng.uniform(70, 100)
            k = rng.choice((4, 5, 5, 6))
            step = math.degrees(37.0 / R)
            up = rng.random() < 0.6
            oy, base = (R * 0.8, 270.0) if up else (-R * 0.8, 90.0)
            return [], [(R * math.cos(math.radians(base + (j - (k - 1) / 2) * step)),
                         oy + R * math.sin(math.radians(base + (j - (k - 1) / 2) * step))) for j in range(k)]
        if kind == "row":
            k = rng.choice((3, 3, 4))
            a = math.radians(rng.choice((-1, 1)) * rng.uniform(18, 55))
            return [], [((j - (k - 1) / 2) * 37 * math.cos(a), (j - (k - 1) / 2) * 37 * math.sin(a)) for j in range(k)]
        if kind == "crown":
            R = rng.uniform(46, 78)
            sweep = rng.uniform(110, 150)
            n = 3 if math.radians(sweep) * R < 170 else 5
            oy = R * 0.55
            pegs = [(0.0, oy - R - BRICK_T / 2 - 17 - PEG_R)] if rng.random() < 0.6 else []
            return crown_specs(0.0, oy, R, 270 - sweep / 2, 270 + sweep / 2, n), pegs
        if kind == "chevron":
            L = rng.uniform(36, 46)
            th = math.radians(rng.uniform(28, 42))
            ay = -L * math.sin(th) / 2
            br = [line_brick((-22, ay), (-22 - L * math.cos(th), ay + L * math.sin(th))),
                  line_brick((22, ay), (22 + L * math.cos(th), ay + L * math.sin(th)))]
            return br, ([(0.0, ay - 38)] if rng.random() < 0.5 else [])
        if kind == "diag":
            n = rng.choice((2, 2, 3))
            L = rng.uniform(32, 38)
            a = math.radians(rng.choice((-1, 1)) * rng.uniform(24, 48))
            half = (n * L + (n - 1) * 2.5) / 2
            return run_specs([(-half * math.cos(a), -half * math.sin(a)), (half * math.cos(a), half * math.sin(a))], L), []
        return [], [(0.0, 0.0)]

    @staticmethod
    def extent(br, pegs):
        """How far a structure's pieces reach from its centre, edges included."""
        e = [math.hypot(x, y) + PEG_R for (x, y) in pegs]
        e += [math.hypot(x, y) + b["thickness"] / 2 for b in br for (x, y) in brick_samples(b, 4.0)]
        return max(e)

    @staticmethod
    def shifted(br, pegs, cx, cy):
        out = []
        for b in br:
            if b["kind"] == "line":
                out.append(line_brick((b["x1"] + cx, b["y1"] + cy), (b["x2"] + cx, b["y2"] + cy), b["thickness"]))
            else:
                out.append(arc_brick(b["x"] + cx, b["y"] + cy, b["r"], b["start"], b["sweep"], b["thickness"]))
        return out, [(x + cx, y + cy) for (x, y) in pegs]

    def place_template(self, kind, br, pegs, mirror=False):
        if mirror:
            br = [mirror_spec(b) for b in br]
            pegs = [(800 - x, y) for (x, y) in pegs]
        snap = self.snapshot()
        tag = f"fill {kind}"
        laid = self.put_bricks(br, tag, run=kind != "chevron")
        if len(laid) < len(br):
            self.rollback(snap)
            return False
        got = sum(1 for (x, y) in pegs if self.put(x, y, tag, log=False))
        if pegs and got < max(1, math.ceil(0.75 * len(pegs))):
            self.rollback(snap)
            return False
        return True

    # A pattern field fills the open board with one structure repeated at a spacing d (searched from wide to tight
    # until the piece count is met), so the fill reads as a design and parts round the subject, which keeps 18 clear.
    def pattern(self, kind, d):
        """The pattern's elements in placing order: ("peg", x, y) or ("brick", spec)."""
        p = self.pat
        out = []
        if kind == "arcs":                        # concentric rings round a focus: brick and moon in turn above it
            fx, fy = p["focus"]
            for k, R in enumerate(np.arange(1.7 * d, 470.0, d)):
                dash = p["dash"]
                step = math.degrees((dash + 54.0) / R)
                a = 196.0 + (step / 2 if k % 2 == p["parity"] else 0.0)
                while a + math.degrees(dash / R) <= 344.0:
                    out.append(("brick", arc_brick(fx, fy, R, a, math.degrees(dash / R))))
                    m = math.radians(a + math.degrees((dash + 27.0) / R))
                    out.append(("peg", fx + R * math.cos(m), fy + R * math.sin(m)))
                    a += step
                # below the focus: straight chords where the ring is steep enough (20 degrees or more: never a deck,
                # never a cup), moons between them and along the flat bottom
                n = max(6, int(2 * math.pi * R / p["peg_step"]))
                for j in range(n):
                    ang = 2 * math.pi * (j + 0.5 * (k % 2)) / n
                    if math.sin(ang) <= -0.2:
                        continue
                    steep = abs(90.0 - math.degrees(ang) % 360.0) >= 20.0 + math.degrees(dash / 2 / R) \
                        and math.sin(ang) > 0.1 and j % 2 == 0
                    if steep:
                        a0, a1 = ang - dash / 2 / R, ang + dash / 2 / R
                        out.append(("brick", line_brick((fx + R * math.cos(a0), fy + R * math.sin(a0)),
                                                        (fx + R * math.cos(a1), fy + R * math.sin(a1)))))
                    else:
                        out.append(("peg", fx + R * math.cos(ang), fy + R * math.sin(ang)))
        elif kind == "lattice":                   # dashed brick lines on a slope, moons between them (a V when mirrored)
            th = math.radians(p["angle"])
            for side in ((-1, 1) if self.sym else (1,)):
                ux, uy = side * math.cos(th), math.sin(th)
                nx, ny = -uy, ux
                for k in range(-14, 15):
                    ox, oy = 400 + nx * k * d, 300 + ny * k * d
                    for j in range(-12, 13):
                        s = j * (p["dash"] + 40.0)
                        a = (ox + ux * s, oy + uy * s)
                        c = (a[0] + ux * p["dash"], a[1] + uy * p["dash"])
                        if self.sym and (side * (a[0] - 400) < 0 or side * (c[0] - 400) < 0):
                            continue
                        out.append(("brick", line_brick(a, c)))
                        m = (ox + nx * d / 2 + ux * (s + p["dash"] / 2), oy + ny * d / 2 + uy * (s + p["dash"] / 2))
                        if not self.sym or side * (m[0] - 400) >= 0:
                            out.append(("peg", m[0], m[1]))
        else:                                     # "grid": staggered rows of moons, every other row a zigzag of brick
            dy = 0.87 * d
            for r_ in range(-8, 9):
                y = 300 + r_ * dy
                if r_ % 2 == p["parity"] % 2:
                    for j in range(-10, 11, 2):
                        x0 = 400 + j * d + (d / 2 if r_ % 2 else 0)
                        sg = 1 if (j // 2) % 2 else -1
                        out.append(("brick", line_brick((x0 - 16, y - sg * 9), (x0 + 16, y + sg * 9))))
                        out.append(("peg", x0 + d, y))
                else:
                    for j in range(-10, 11):
                        out.append(("peg", 400 + j * d + (d / 2 if r_ % 2 else 0), y))
        fx, fy = p["focus"]
        mid = lambda e: (e[1], e[2]) if e[0] == "peg" else self.b.home(e[1])  # noqa: E731
        out = [e for e in out if SITE_Y[0] - 20 <= mid(e)[1] <= SITE_Y[1] + 10]
        out.sort(key=lambda e: math.hypot(mid(e)[0] - fx, mid(e)[1] - fy))
        # the bricks first (they need the room; the moons fit round them), up to the brick share at the target
        nb = max(int(self.brick_target * self.target_n) - len(self.b.bricks), 0)
        bricks = [e for e in out if e[0] == "brick"]
        return bricks[:nb + 4] + [e for e in out if e[0] == "peg"] + bricks[nb + 4:]

    def lay_pattern(self, kind, d):
        for e in self.pattern(kind, d):
            if self.n >= self.target_n + 6:
                break
            if e[0] == "peg":
                self.put(e[1], e[2], f"fill {kind}", gap=18.0, log=False)
            else:
                self.put_bricks([e[1]], f"fill {kind}", run=False)

    def fill(self):
        gx, gy = np.meshgrid(np.arange(90.0, 711.0, 10.0), np.arange(SITE_Y[0], SITE_Y[1] + 1, 10.0))
        G = np.c_[gx.ravel(), gy.ravel()]
        self.inside_mask = np.array([any(point_in_poly(x, y, poly) for (_n, poly) in self.b.subjects) for (x, y) in G])
        reg = np.array([any(point_in_poly(x, y, poly) for poly in self.regions) for (x, y) in G]) if self.regions else None
        w_y = np.where(G[:, 1] >= 400, 0.7, 1.0)
        if reg is not None:
            w_y = w_y * np.where(reg, 1.0, 0.6)
        fails = 0
        before = self.n
        rng = self.rng
        kind = self.fill_kind
        self.pat = {"focus": (400.0 if self.sym else rng.uniform(340, 460), rng.uniform(250, 330)),
                    "parity": rng.randrange(2) if kind == "arcs" else rng.randrange(3), "dash": rng.uniform(34, 42),
                    "peg_step": rng.uniform(38, 44), "angle": rng.uniform(24, 38)}
        if kind != "clusters":
            for d in (64.0, 60.0, 56.0, 52.0, 48.0, 45.0, 42.0):
                snap = self.snapshot()
                self.lay_pattern(kind, d)
                if self.n >= self.target_n - 2 or d == 42.0:
                    break
                self.rollback(snap)
            while self.n > author.PIECES_TARGET[1] - 2:          # the pattern's outermost pieces go first
                fx, fy = self.pat["focus"]
                last = max((p for p, t in zip(self.b.pegs + self.b.bricks, self.tags()) if t == f"fill {kind}"),
                           key=lambda p: math.hypot(self.b.home(p)[0] - fx, self.b.home(p)[1] - fy))
                self.remove(last)
        D = self.clearance(G)
        while self.n < self.target_n and fails < 80:
            # what is still owed: bricks to the brick share at the target count, pegs to the count (a brick
            # structure lays about 2.5 bricks, a peg structure about 5 moons)
            owed_b = max(self.brick_target * self.target_n - len(self.b.bricks), 0.0) / 2.5
            owed_p = max(self.target_n - self.n - 2.5 * owed_b, 0.0) / 5.0
            need_brick = self.rng.random() * (owed_b + owed_p) < owed_b
            kind = self.rng.choice(["crown", "chevron", "diag", "diag"] if need_brick else ["ring", "ring", "arc", "row"])
            br, pegs = self.template(kind)
            rho = self.extent(br, pegs) + (24.0 if br else 18.0)
            ok = np.nonzero(D >= rho)[0]
            if not len(ok):
                fails += 1
                continue
            w = (D[ok] - rho + 6.0) ** 2 * w_y[ok]
            i = self.rng.choices(list(ok), weights=list(w))[0]
            cx, cy = float(G[i, 0]), float(G[i, 1])
            br, pegs = self.shifted(br, pegs, cx, cy)
            if not self.place_template(kind, br, pegs):
                fails += 1
                continue
            if self.sym and abs(cx - 400) > rho + 4 and self.n < self.target_n + 4:
                self.place_template(kind, br, pegs, mirror=True)
            D = self.clearance(G)
        self.G, self.w_y = G, w_y
        self.brickify()
        self.top_up()
        self.note("(open board)", "fill structures", before)

    def top_up(self):
        """What room is left takes single moons, to the target count."""
        G, w_y = self.G, self.w_y
        while self.n < self.target_n:
            D = self.clearance(G)
            order = [i for i in np.argsort(-(D * w_y), kind="stable") if D[i] >= 24]
            if not any(self.put(float(G[i, 0]), float(G[i, 1]), "fill peg", log=False) for i in order[:40]):
                break

    def brickify(self):
        """Below the brick share, two neighbouring moons of one fill structure become one sloped brick between them
        (a brick run in the pattern's own grain), while the brick still keeps every rule."""
        pegs = [(p, t) for p, t in zip(self.b.pegs, [t for (k, t) in self.b.tags if k == "peg"])
                if t.startswith("fill") and "move" not in p]
        pairs = []
        for i in range(len(pegs)):
            for j in range(i + 1, len(pegs)):
                (a, ta), (c, tc) = pegs[i], pegs[j]
                d = math.hypot(a["x"] - c["x"], a["y"] - c["y"])
                slope = math.degrees(math.atan2(abs(a["y"] - c["y"]), abs(a["x"] - c["x"]) + 1e-9))
                if ta == tc and d <= 50 and slope >= 16:
                    pairs.append((a, c))
        self.rng.shuffle(pairs)
        gone = set()
        for a, c in pairs:
            if len(self.b.bricks) >= self.brick_target * self.n - 0.5:
                break
            if id(a) in gone or id(c) in gone:
                continue
            ux, uy = c["x"] - a["x"], c["y"] - a["y"]
            L = math.hypot(ux, uy)
            k = (L - 34.0) / 2 / L if L > 34 else 0.0
            sp = line_brick((a["x"] + ux * k, a["y"] + uy * k), (c["x"] - ux * k, c["y"] - uy * k))
            ia = next(i for i, p in enumerate(self.b.pegs) if p is a)
            ic = next(i for i, p in enumerate(self.b.pegs) if p is c)
            tags = list(self.b.tags)
            self.remove(a)
            self.remove(c)
            if self.put_bricks([sp], "fill brick run", run=False):
                gone |= {id(a), id(c)}
            else:
                self.b.tags[:] = tags
                for i_, p in sorted(((ia, a), (ic, c)), key=lambda q: q[0]):
                    self.b.pegs.insert(i_, p)

    # ---- every first shot meets something
    def cover_first_shots(self):
        Z = np.zeros((600, 800), bool)
        yy, xx = np.mgrid[0:600, 0:800]

        def mark(x, y, rad):
            x0, x1, y0, y1 = int(max(x - rad, 0)), int(min(x + rad + 1, 800)), int(max(y - rad, 0)), int(min(y + rad + 1, 600))
            Z[y0:y1, x0:x1] |= (xx[y0:y1, x0:x1] - x) ** 2 + (yy[y0:y1, x0:x1] - y) ** 2 <= rad * rad
        for p in self.b.pegs:
            for (x, y) in (self.b.mover_points(p, 24) if "move" in p else [(p["x"], p["y"])]):
                mark(x, y, p.get("r", PEG_R) + 6)
        for bk in self.b.bricks:
            for (x, y) in brick_samples(bk, 3.0):
                mark(x, y, bk["thickness"] / 2 + 6)
        added = 0
        for f in free_flights():
            pts = f[(f[:, 1] < 599) & (f[:, 0] >= 0) & (f[:, 0] < 800) & (f[:, 1] >= 0)]
            if Z[pts[:, 1].astype(int), pts[:, 0].astype(int)].any():
                continue
            cand = sorted((q for q in pts if 170 <= q[1] <= 470), key=lambda q: abs(q[1] - 330))
            for (x, y) in cand[::3]:
                p = self.put(float(x), float(y), "lane", log=False)
                if p:
                    mark(p["x"], p["y"], PEG_R + 6)
                    added += 1
                    break
        return added

    # ---- greens and candidates
    def tags(self):
        return [t for (k, t) in self.b.tags if k == "peg"] + [t for (k, t) in self.b.tags if k == "brick"]

    def mark(self):
        b = self.b
        for p in b.pegs + b.bricks:
            p["canBeOrange"] = False
        b.greens_in_reach()
        pieces = list(zip(b.pegs + b.bricks, self.tags()))
        fill = [(p, t) for (p, t) in pieces if t.startswith(("fill", "lane"))]
        pool = [p for (p, t) in fill if "kind" not in p and "move" not in p and p.get("canBeGreen", True)
                and 160 <= p["y"] <= 440 and b.piece_in_reach(p)]
        greens = []
        if pool:
            greens.append(pool[self.rng.randrange(len(pool))])
            while len(greens) < 10 and len(greens) < len(pool):
                greens.append(max((p for p in pool if p not in greens),
                                  key=lambda p: min(math.hypot(p["x"] - g["x"], p["y"] - g["y"]) for g in greens)))
        extras = [p for (p, _t) in fill if p not in greens]
        self.rng.shuffle(extras)
        excl = list(greens)
        b.candidates(exclude=[b.home(p) for p in excl])
        # the lower third well under its 30% (the hand refits ran 6-11%, 1-4 one low candidate each side): low
        # candidates are the ones a greedy player leaves, so more of them fail the cheap-difficulty ratio; the
        # fill's lowest go blue first, keeping a candidate below y 400 on each side
        fillset = {id(p) for (p, _t) in fill}
        low = sorted((p for (p, t) in pieces if b.home(p)[1] >= 400), key=lambda p: (id(p) not in fillset, -b.home(p)[1]))
        for p in low:
            if self.low_share() <= self.low_target:
                break
            side = [q for q in low if q["canBeOrange"] and (b.home(q)[0] < 400) == (b.home(p)[0] < 400)]
            if p["canBeOrange"] and len(side) > 1 and p not in excl:
                excl.append(p)
                b.candidates(exclude=[b.home(q) for q in excl])
        extras = [p for p in extras if p not in excl]
        while self.share() > self.cand_share and extras:
            excl.append(extras.pop())
            b.candidates(exclude=[b.home(p) for p in excl])
        tag_of = {id(p): t for (p, t) in pieces}
        for _ in range(30):                               # the spread rule and a low candidate on each side
            probs = b.check(verbose=False)
            sq = next((re.findall(r"square at \((\d+), (\d+)\)", q) for q in probs if q.startswith("spread: ") and "square" in q), None)
            if sq:
                sx, sy = map(int, sq[0])
                inside = [p for (p, t) in pieces if p["canBeOrange"] and sx <= b.home(p)[0] < sx + 200 and sy <= b.home(p)[1] < sy + 200]
                inside.sort(key=lambda p: (not tag_of[id(p)].startswith(("fill", "lane")), -b.home(p)[1]))
                excl.append(inside[0])
                b.candidates(exclude=[b.home(p) for p in excl])
                continue
            low = next((q for q in probs if "candidates below y 400" in q), None)
            if low:
                side = (lambda x: x < 400) if "left 0" in low else (lambda x: x >= 400)
                back = [p for p in excl if p not in greens and side(b.home(p)[0]) and b.home(p)[1] >= 400]
                if back:
                    excl.remove(back[0])
                    b.candidates(exclude=[b.home(p) for p in excl])
                    continue
            if self.share() < 0.62 and len(excl) > len(greens):
                excl.pop()
                b.candidates(exclude=[b.home(p) for p in excl])
                continue
            break
        self.exclude = excl

    def low_share(self):
        c = [p for p in self.b.pegs + self.b.bricks if p["canBeOrange"]]
        return sum(1 for p in c if self.b.home(p)[1] >= 400) / max(len(c), 1)

    def share(self):
        n = self.n
        return sum(p["canBeOrange"] for p in self.b.pegs + self.b.bricks) / max(n, 1)

    def remove(self, piece):
        """Takes one piece off the board (its entry in the laying order too)."""
        kind = "brick" if "kind" in piece else "peg"
        lst = self.b.bricks if kind == "brick" else self.b.pegs
        i = next(k for k, p in enumerate(lst) if p is piece)
        seen = -1
        for j, (k, _t) in enumerate(self.b.tags):
            if k == kind:
                seen += 1
                if seen == i:
                    del self.b.tags[j]
                    break
        del lst[i]

    def build(self):
        self.lay_features()
        self.fill()
        self.cover_first_shots()
        self.mark()
        return self.b.check(verbose=False)

    # ---- the engine: repairs among the fill, then the figures
    def engine_pass(self, path, rounds=4, log=print):
        from . import engine
        number = self.meta["number"]
        rep = {}
        for rnd in range(rounds):
            author.write_json(self.level(), path)
            ok, out = engine.validate(path)
            if not ok:
                rep["loader"] = out.splitlines()[:4]
                return rep
            dead, stuck = engine.sweep_angles(path, number)
            rc = engine.reach(path, number)
            pieces = list(zip(self.b.pegs + self.b.bricks, self.tags()))
            changed, gone = False, set()
            for a in dead:
                pts = engine.trace(path, a, number, 900)
                for (x, y) in sorted((q for q in pts if 170 <= q[1] <= 470), key=lambda q: abs(q[1] - 330))[::3]:
                    if self.put(float(x), float(y), "lane", log=False):
                        changed = True
                        break
            if len(stuck) >= 0.03 * 777:
                from collections import Counter
                cells = Counter()
                for a in stuck:
                    pts = engine.trace(path, a, number, 3000)
                    c = Counter((round(x / 5) * 5, round(y / 5) * 5) for x, y in pts)
                    cells[c.most_common(1)[0][0]] += 1
                for (cx, cy), _k in cells.most_common(3):
                    near = sorted(((math.hypot(self.b.home(p)[0] - cx, self.b.home(p)[1] - cy), i)
                                   for i, (p, t) in enumerate(pieces) if t.startswith("fill") and id(p) not in gone))
                    if near and near[0][0] < 40:
                        gone.add(id(pieces[near[0][1]][0]))
                        self.remove(pieces[near[0][1]][0])
                        changed = True
            for i in rc["never"]:
                if i < len(pieces) and pieces[i][1].startswith(("fill", "lane")) and id(pieces[i][0]) not in gone:
                    gone.add(id(pieces[i][0]))
                    self.remove(pieces[i][0])
                    changed = True
            if not changed:
                rep.update(dead=len(dead), stuck=len(stuck), never=len(rc["never"]), repair_rounds=rnd)
                break
            pieces = None
            self.mark()
        else:
            author.write_json(self.level(), path)
            dead, stuck = engine.sweep_angles(path, number)
            rep.update(dead=len(dead), stuck=len(stuck), never=len(engine.reach(path, number)["never"]), repair_rounds=rounds)
        rep["preflight"] = self.b.check(verbose=False)
        author.write_json(self.level(), path)
        rule = engine.play(path, number, games=engine.GREEDY_GAMES)
        tun = engine.play(path, number, games=engine.RAMP_GAMES)
        rep.update(wins48=rule["greedy_won"], tuning_per_48=tun["ramp_per_48"],
                   cheap=engine.cheap(tun, engine.piece_homes(path)))
        return rep

    def level(self):
        m = self.meta
        return self.b.level_json(m["id"], m["name"], m["scene"])

    # ---- the layout file
    def emit(self, report=None):
        b, m = self.b, self.meta
        n = self.n
        cands = sum(p["canBeOrange"] for p in b.pegs + b.bricks)
        low = sum(1 for p in b.pegs + b.bricks if p["canBeOrange"] and b.home(p)[1] >= 400) / max(cands, 1)
        movers = sum(1 for p in b.pegs if "move" in p)
        used = "\n".join(f"  {name}: {how} ({k})" for (name, how, k) in self.used)
        doc = (f'"""{m["id"]} "{m["name"]}": generated layout, variant {self.variant} of `mfl.py generate {m["id"]} '
               f'--seed {self.seed}`.\n\nSubject: {m.get("subject", "")}\nTechnique: {self.code} '
               f'({m.get("technique", "")})\n\nFrom the scene recipe\'s features ({m["scene"]}):\n{used}\n\n'
               f'{n} pieces ({len(b.bricks)} bricks, {len(b.bricks) / max(n, 1):.0%}; {movers} movers), {cands} candidates '
               f'({cands / max(n, 1):.0%}, {low:.0%} of them in the lower third).\n'
               f'Hand-polish the subject, then `mfl.py tune`, then `mfl.py build` (README, "Author workflow").\n"""')
        level = {k: m[k] for k in ("id", "name", "stage", "number", "scene", "subject", "technique") if k in m}
        out = [doc, "", "LEVEL = dict(" + ", ".join(f"{k}={v!r}" for k, v in level.items()) + ")", "",
               "GENERATED = " + repr(dict(seed=self.seed, variant=self.variant, technique=self.code, target_pieces=self.target_n,
                                          brick_share=round(self.brick_target, 3), mirrored=self.sym, spacing=self.spacing,
                                          candidate_share=round(self.cand_share, 3))), "",
               "# never orange: the sure greens and the fill left blue (b.candidates leaves out the unreachable and the",
               "# lowest too, to hold the lower third at 30%)",
               "EXCLUDE = [" + ", ".join(f"({_r1(b.home(p)[0])}, {_r1(b.home(p)[1])})" for p in self.exclude) + "]",
               "", "", "def build(b):"]
        for name in self.declared:
            out.append(f"    b.subject({name!r})")
        ip = ib = 0
        last = None
        for kind, tag in b.tags:
            if tag != last:
                out.append(f"    # ---- {tag}")
                last = tag
            if kind == "peg":
                p = b.pegs[ip]
                ip += 1
                g = ("" if p.get("canBeGreen", True) else ", green=False") + (", inside=True" if p.get("_inside") else "")
                r = f", r={p['r']}" if p.get("r", PEG_R) != PEG_R else ""
                mv = f", move={p['move']!r}" if "move" in p else ""
                out.append(f"    b.peg({p['x']}, {p['y']}{r}{mv}, tag={tag!r}{g})")
            else:
                q = b.bricks[ib]
                ib += 1
                g = "" if q.get("canBeGreen", True) else ", green=False"
                if q["kind"] == "line":
                    out.append(f"    b.line({q['x1']}, {q['y1']}, {q['x2']}, {q['y2']}, t={q['thickness']}, tag={tag!r}{g})")
                else:
                    out.append(f"    b.arc({q['x']}, {q['y']}, {q['r']}, {q['start']}, {q['sweep']}, t={q['thickness']}, "
                               f"tag={tag!r}{g})")
        out += ["", "    b.greens_in_reach()", "    return b.candidates(exclude=EXCLUDE)", ""]
        return "\n".join(out)


def subject_used(gen):
    return sum(k for (name, _how, k) in gen.used if name != "(open board)")


def generate(level_id, seed=1, variants=4, scene_name=None, out_dir=None, use_engine=True, preview=True, log=print):
    """Writes `variants` layouts for the level to out_dir (default build/generate/<id>/): v<k>.py, and with the
    engine v<k>.json, v<k>.png and summary.txt. Returns [(path, problems, report)]."""
    from .build import board_of, load_layout
    meta = level_meta(level_id, scene_name)
    recipe = scene.load_recipe(meta["scene"])
    out_dir = out_dir or OUT / level_id
    out_dir.mkdir(parents=True, exist_ok=True)
    results = []
    for v in range(variants):
        gen = None
        for attempt in range(3):                         # a variant whose pre-flight still faults is redrawn
            gen = Generator(meta, recipe, seed, v + 100 * attempt)
            probs = gen.build()
            if not probs:
                break
        gen.variant = v
        rep = {}
        path = out_dir / f"v{v}.py"
        if use_engine:
            rep = gen.engine_pass(out_dir / f"v{v}.json", log=log)
            probs = rep["preflight"]
        text = gen.emit(rep)
        path.write_text(text, encoding="utf-8")
        # the file must rebuild exactly the board the generator checked
        _m, _r, b2 = board_of(load_layout(level_id, path))
        same = b2.level_json(meta["id"], meta["name"], meta["scene"]) == gen.level()
        if not same:
            probs = probs + ["the written file does not rebuild the generated board"]
        n = gen.n
        cands = sum(p["canBeOrange"] for p in gen.b.pegs + gen.b.bricks)
        row = dict(pieces=n, bricks=len(gen.b.bricks), brick_share=round(len(gen.b.bricks) / n, 2),
                   candidates=cands, candidate_share=round(cands / n, 2),
                   lower_third=round(sum(1 for p in gen.b.pegs + gen.b.bricks if p["canBeOrange"] and gen.b.home(p)[1] >= 400)
                                     / max(cands, 1), 2),
                   movers=sum(1 for p in gen.b.pegs if "move" in p), subject_pieces=subject_used(gen),
                   mirrored=gen.sym, preflight=probs, warnings=list(gen.b.warnings), **{k: v_ for k, v_ in rep.items() if k != "preflight"})
        if use_engine and preview:
            row["preview"] = str(render_preview(gen, out_dir / f"v{v}.json", out_dir / f"v{v}.png"))
        log(f"  v{v}: {n} pieces ({row['brick_share']:.0%} brick, {row['movers']} movers), candidates {row['candidate_share']:.0%} "
            f"(lower third {row['lower_third']:.0%}), subject {row['subject_pieces']}; pre-flight "
            f"{'PASS' if not probs else 'FAULTS: ' + '; '.join(probs)}"
            + (f"; dead {rep.get('dead')}, stuck {rep.get('stuck')}, unreached {rep.get('never')}, wins {rep.get('wins48')}/48, "
               f"tuning {rep.get('tuning_per_48')}/48" + (f", cheap: {rep['cheap']}" if rep.get("cheap") else "") if use_engine else ""))
        results.append((path, probs, row))
    (out_dir / "summary.json").write_text(json.dumps({str(p.name): r for (p, _pr, r) in results}, indent=1) + "\n",
                                          encoding="utf-8")
    if use_engine and preview:
        contact(out_dir, [r for (_p, _pr, r) in results])
    return results


def render_preview(gen, level_path, png):
    """The variant as the player sees it at 1x over the undressed scene, in the engine's own deal at seed 1."""
    from PIL import Image
    from . import engine
    from .build import _scene_cached
    from .composite import render
    import rich_lib as RL
    m = gen.meta
    cols = engine.colours(level_path, m["number"], 1)
    px = render(gen.level(), gen.recipe, _scene_cached(gen.recipe, 2), cols, S=2, stage=m["stage"], number=m["number"])
    Image.fromarray(RL.to_u8(px)).resize((800, 600), Image.LANCZOS).save(png)
    return png


def contact(out_dir, rows):
    """Every variant's preview on one sheet (two across), labelled with its figures."""
    from PIL import Image, ImageDraw
    ims = [Image.open(r["preview"]) for r in rows if r.get("preview")]
    if not ims:
        return None
    cols = 2 if len(ims) > 1 else 1
    rows_n = math.ceil(len(ims) / cols)
    sheet = Image.new("RGB", (800 * cols, 620 * rows_n), (10, 10, 20))
    dr = ImageDraw.Draw(sheet)
    for k, (im, r) in enumerate(zip(ims, [r for r in rows if r.get("preview")])):
        x, y = 800 * (k % cols), 620 * (k // cols)
        sheet.paste(im, (x, y + 20))
        dr.text((x + 6, y + 4), f"v{k}: {r['pieces']} pieces, {r['brick_share']:.0%} brick, cand {r['candidate_share']:.0%}, "
                f"wins {r.get('wins48')}/48, tuning {r.get('tuning_per_48')}/48", fill=(240, 220, 160))
    p = out_dir / "contact.png"
    sheet.save(p)
    return p


def selftest(verbose=True):
    """Determinism and rule compliance without the engine: the same seed writes the same file, and every variant of a
    game painting's landmark (1-4's recipe) and of our painting's creature (1-3's) passes the pre-flight with the
    density rules (85-160 pieces, 60-90% candidates, the lower third at most 30%)."""
    cases = []
    for scene_name, tech, subj in (("uldah-gilded-dome", "a landmark partly outlined", "Ul'dah's great dome"),
                                   ("sagolii-cactuar", "a creature in outline", "a cactuar mid-stride on a dune")):
        meta = dict(id="selftest-gen", name="Selftest", stage=2, number=6, scene=scene_name, subject=subj, technique=tech,
                    code=technique_code(tech))
        recipe = scene.load_recipe(scene_name)
        texts = []
        for _ in range(2):
            g = Generator(meta, recipe, 3, 0)
            probs = g.build()
            texts.append(g.emit())
        n = g.n
        share = g.share()
        low = sum(1 for p in g.b.pegs + g.b.bricks if p["canBeOrange"] and g.b.home(p)[1] >= 400) / max(
            sum(p["canBeOrange"] for p in g.b.pegs + g.b.bricks), 1)
        cases.append((f"{scene_name}: the same seed writes the same file", texts[0] == texts[1]))
        cases.append((f"{scene_name}: {n} pieces, candidates {share:.0%}, lower third {low:.0%}, pre-flight {probs or 'clean'}",
                      not probs and author.PIECES_MIN <= n <= author.PIECES_MAX and 0.6 <= share <= 0.9 and low <= 0.3))
        cases.append((f"{scene_name}: the subject is traced ({subject_used(g)} pieces on its features)", subject_used(g) >= 20))
        g2 = Generator(meta, recipe, 4, 0)
        g2.build()
        cases.append((f"{scene_name}: another seed writes another board", g2.emit() != texts[0]))
    ok = True
    for name, good in cases:
        ok &= good
        if verbose:
            print(f"  {'ok ' if good else 'BAD'} generate: {name}")
    return ok
