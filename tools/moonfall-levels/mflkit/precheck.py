"""Precheck: the findings that came back round after round in the supervisors' records (MR 3 to MR 6), measured before
review so a stage reaches the reviewers clean. `mfl.py precheck <level-id>...` (and `stage` prints it, report only).

Every check reads the committed artefacts of a level (the level file, the scene recipe, the 1x composite and the build
report), so it runs on a branch checkout without the game install or a build. `--rev <git rev>` reads the same files from a
past commit instead (how the thresholds were calibrated: the round-1 and the approved states of MR 3 to MR 6).

A check reports PASS, WARN, FAIL or N/A with its evidence (coordinates, figures, a crop written to
docs/design/v9/levels/build/precheck/). FAIL is a finding the reviewers raised as a major or one a rule already states; WARN
is a finding they raised as a minor or a nit. Nothing here is a gate: the build's own checks stay the gates.

The checks (README.md, "Precheck", has the table of findings each one answers):

  legibility  the traced subject reads at the 640 x 480 board scale (0.735): size and colour contrast against its surround
  axis        no bright disc, light or engraved route behind the launcher's axis or the aim guide's first 200 units
  repeats     near-repeats: crop overlap, the same jewels, the same technique back to back, similar layout and picture
  patches     empty circles and crowded windows of the board, strays, pieces that trace nothing, wall ledges
  play        stuck-ball hot spots, holdout concentration (from the build report's greedy games)
  margins     F6, F7 and F9 margins that pass by a hair (the pilot fallbacks)
"""
import json
import math
import subprocess

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from . import paths

PASS, WARN, FAIL, NA = "PASS", "WARN", "FAIL", "N/A"
ORDER = {NA: -1, PASS: 0, WARN: 1, FAIL: 2}

VIEW = 0.735                                  # the board's scale in a 640 x 480 window
PIVOT = (400.0, 87.0)                         # the launcher's pivot (layout.py)
LEFT, RIGHT, TOP, LOWEST = 75.5, 724.5, 41.0, 560.0
PLAY = (77, 43, 723, 592)                     # the playfield in the 800 x 600 composite (the chrome lies outside)

# ---- thresholds, calibrated on MR 3 to MR 6 (README, "Precheck: calibration")
LEG_FAIL_SIDE, LEG_FAIL_AREA = 50.0, 2000.0   # the subject's shorter side (px) and area (px^2) at 0.735 (the whale, round 1: 43 px, 1406)
LEG_WARN_SIDE, LEG_WARN_AREA = 60.0, 2500.0   # (the whale, approved: 68 px, 3583)
LEG_FAIL_DE, LEG_WARN_DE = 0.025, 0.045       # subject-to-surround colour distance in OKLab
LEG_BACKDROP = 45000.0                        # a region over this many px^2 at 0.735 (a quarter of the board) is a backdrop, not a subject
AXIS_L_MID, AXIS_L_HI = 0.72, 0.80            # OKLab L of what lies behind the guide: the ball is 0.885, the dots 0.96 (UX MR 3 M1)
AXIS_HI_WARN, AXIS_HI_FAIL, AXIS_MID_WARN = 10.0, 400.0, 150.0   # units^2 of it within the guide's first 200 units
GUIDE_LEN = 200.0
CROP_WARN, CROP_FAIL = 0.20, 0.25             # share of the later crop's own area (charter: at most 25%)
LAYOUT_R_WARN, LAYOUT_R_FAIL = 0.80, 0.90     # correlation of the piece density over a 10 x 8 grid
SYM_BOTH = 0.60                               # both boards this mirror-symmetric (grid against its flip): the same centred picture
EMPTY_WARN, EMPTY_FAIL = 100.0, 130.0         # radius of the largest circle on the board that holds no piece
EMPTY_FLOOR = 540.0                           # the board's usable floor: a circle stops here (the bucket's approach is open by design)
EMPTY_Y = (110.0, 500.0)                      # circle centres are looked for between these rows (the launcher's and the bucket's ends are empty by design)
CROWD_WARN, CROWD_FAIL = 0.36, 0.45           # share of a 90 x 90 window covered by pieces (a hex lattice at the 14-unit spacing rule is 0.31)
STRAY_GAP = 75.0                              # a piece whose nearest piece is this far (surface to surface) is a stray
UNTRACED_DIST, UNTRACED_WARN, UNTRACED_FAIL = 34.0, 0.30, 0.50
LEDGE_GAP = (12.5, 14.0)                      # the wall gap a 12-unit ball only just passes (5-3 jetty: 13.5)
STUCK_WARN, STUCK_FAIL = 70, 100              # stuck-rule fires per 1728 games; the shipped maximum is 88 (1-1)
HOLD_WARN, HOLD_FAIL = 0.25, 0.33             # the top holdout's share of lost games
HOLD3_WARN = 0.70                             # the top three together
THIN_WARN, THIN_FAIL = 7.5, 6.5               # mean pieces a first shot touches (sweep); the earlier 25 levels ranged 8 to 25 (6-2, round 1: 6.4)
F6_WARN, F9_P10_WARN, F9_MIN_WARN, F9_MIN_FAIL, SECOND_WARN = 0.225, 0.105, 0.100, 0.090, 0.16   # F9 FAIL: p10 under the guard and a minimum under 0.09 (4-4, round 1)


# ---------------------------------------------------------------------------------------------------------------- data
class Source:
    """The level files of the working tree, or of a git revision."""

    def __init__(self, rev=None):
        self.rev = rev
        self.rel = paths.LEVELS.relative_to(paths.REPO).as_posix()

    def _git(self, rel):
        out = subprocess.run(["git", "-C", str(paths.REPO), "show", f"{self.rev}:{rel}"], capture_output=True)
        return out.stdout if out.returncode == 0 else None

    def read(self, rel):
        """bytes of a file under levels/, or None."""
        if self.rev is None:
            p = paths.LEVELS / rel
            return p.read_bytes() if p.exists() else None
        return self._git(f"{self.rel}/{rel}")

    def ids(self):
        if self.rev is None:
            return sorted(p.stem for p in paths.LAYOUTS.glob("*.py"))
        out = subprocess.run(["git", "-C", str(paths.REPO), "ls-tree", "--name-only", f"{self.rev}:{self.rel}/report"],
                             capture_output=True, text=True)
        return sorted(n[:-5] for n in out.stdout.split() if n.endswith(".json"))

    def json(self, rel):
        b = self.read(rel)
        return json.loads(b.decode("utf-8")) if b else None

    def image(self, rel):
        b = self.read(rel)
        if not b:
            return None
        import io
        return np.asarray(Image.open(io.BytesIO(b)).convert("RGB"), np.float32) / 255.0


class Level:
    """Everything a check reads about one level."""

    def __init__(self, src, lid):
        self.id = lid
        self.report = src.json(f"report/{lid}.json") or {}
        self.file = src.json(f"json/{lid}.json")
        scene = self.report.get("scene") or (self.file or {}).get("scene")
        self.recipe = src.json(f"scenes/{scene}.json") if scene else None
        self.features = merged_features(src, self.recipe)
        self.composite = src.image(f"composites/{lid}.png")
        self.stage = self.report.get("stage")
        self.number = self.report.get("number")
        self.name = self.report.get("name") or (self.file or {}).get("name") or lid
        self.subject = self.report.get("subject") or ""
        self.technique = self.report.get("technique") or ""
        self.checks = self.report.get("checks") or {}
        self.pieces = pieces_of(self.file) if self.file else []

    @property
    def ok(self):
        return self.file is not None


def merged_features(src, recipe):
    """The recipe's features; for our own paintings the painter's FEATURES under them (as scene.load_recipe does)."""
    if not recipe:
        return {}
    feats = {}
    s = recipe.get("source") or {}
    if s.get("kind") == "painting":
        b = src.read(f"painters/{s.get('painter')}.py")
        if b:
            ns = {"__name__": "precheck_painter"}
            try:
                exec(compile(b.decode("utf-8"), f"painters/{s.get('painter')}.py", "exec"), ns)
                feats.update(ns.get("FEATURES", {}))
            except Exception:                                  # a painter that needs the kit's imports: no features
                pass
    feats.update(recipe.get("features") or {})
    return feats


def pieces_of(level):
    """[(kind, geom...)] as dicts: {"kind": "peg"|"line"|"arc", "pts": [(x, y)...], "r": radius (half thickness for
    bricks), "orange": bool, "green": bool, "mover": bool}. A brick's pts are samples along its centre line."""
    out = []
    for p in level.get("pegs", []):
        out.append(dict(kind="peg", pts=[(p["x"], p["y"])], r=p.get("r", 10.0), orange=p.get("canBeOrange", False),
                        green=p.get("canBeGreen", True), mover="move" in p))
    for b in level.get("bricks", []):
        if b["kind"] == "line":
            n = max(1, int(math.hypot(b["x2"] - b["x1"], b["y2"] - b["y1"]) / 4))
            pts = [(b["x1"] + (b["x2"] - b["x1"]) * k / n, b["y1"] + (b["y2"] - b["y1"]) * k / n) for k in range(n + 1)]
        else:
            n = max(1, int(b["r"] * math.radians(abs(b["sweep"])) / 4))
            pts = [(b["x"] + b["r"] * math.cos(math.radians(b["start"] + b["sweep"] * k / n)),
                    b["y"] + b["r"] * math.sin(math.radians(b["start"] + b["sweep"] * k / n))) for k in range(n + 1)]
        out.append(dict(kind=b["kind"], pts=pts, r=b["thickness"] / 2.0, orange=b.get("canBeOrange", False),
                        green=b.get("canBeGreen", True), mover=False))
    return out


class R:
    """One check's result for one level."""

    def __init__(self, check, level, status, msg, evidence=(), crop=None):
        self.check, self.level, self.status, self.msg, self.evidence, self.crop = check, level, status, msg, list(evidence), crop

    def line(self):
        s = f"  {self.status:4} {self.check:10} {self.level}: {self.msg}"
        for e in self.evidence:
            s += f"\n         {e}"
        if self.crop:
            s += f"\n         crop: {self.crop}"
        return s


def worst(results):
    return max((r.status for r in results), key=lambda s: ORDER[s], default=PASS)


# ------------------------------------------------------------------------------------------------------------ colour
def oklab(rgb):
    """float RGB (..., 3) in 0..1 (sRGB) -> OKLab (L, a, b)."""
    c = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    l = 0.4122214708 * c[..., 0] + 0.5363325363 * c[..., 1] + 0.0514459929 * c[..., 2]
    m = 0.2119034982 * c[..., 0] + 0.6806995451 * c[..., 1] + 0.1073969566 * c[..., 2]
    s = 0.0883024619 * c[..., 0] + 0.2817188376 * c[..., 1] + 0.6299787005 * c[..., 2]
    l, m, s = np.cbrt(l), np.cbrt(m), np.cbrt(s)
    return np.stack([0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                     1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                     0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s], -1)


def to_view(img, scale=VIEW):
    """The composite as the 640 x 480 window shows it: 800 x 600 scaled by 0.735 (box-filtered)."""
    im = Image.fromarray(np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8))
    return np.asarray(im.resize((round(800 * scale), round(600 * scale)), Image.BOX), np.float32) / 255.0


# ----------------------------------------------------------------------------------------------------------- masks
def _canvas(scale):
    W, H = round(800 * scale), round(600 * scale)
    im = Image.new("L", (W, H), 0)
    return im, ImageDraw.Draw(im)


def _dilate(mask, r_units, scale):
    k = int(round(r_units * scale))
    if k <= 0:
        return mask
    im = Image.fromarray((mask * 255).astype(np.uint8))
    for _ in range(max(1, k // 6)):                       # MaxFilter is square: a few small rounds approximate a disc
        step = min(k, 6)
        im = im.filter(ImageFilter.MaxFilter(2 * step + 1))
        k -= step
        if k <= 0:
            break
    return np.asarray(im) > 127


def piece_mask(pieces, scale=VIEW, pad=3.0):
    """Where the pieces are (their surfaces plus `pad` units), at `scale`."""
    im, d = _canvas(scale)
    for p in pieces:
        r = (p["r"] + pad) * scale
        pts = [(x * scale, y * scale) for x, y in p["pts"]]
        if p["kind"] == "peg":
            x, y = pts[0]
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
        else:
            d.line(pts, fill=255, width=max(1, int(2 * r)))
            for x, y in (pts[0], pts[-1]):
                d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    return np.asarray(im) > 127


def playfield_mask(scale=VIEW):
    im, d = _canvas(scale)
    x0, y0, x1, y1 = PLAY
    d.rectangle([x0 * scale, y0 * scale, x1 * scale, y1 * scale], fill=255)
    return np.asarray(im) > 127


def feature_mask(feat, scale=VIEW):
    """A recipe feature as a mask: a filled polygon (a closed outline), a filled ellipse, a disc (one point) or a polyline
    dilated to 10 units. Returns (mask, kind)."""
    im, d = _canvas(scale)
    kind = "line"
    if isinstance(feat, dict) and "ellipse" in feat:
        cx, cy, rx, ry = feat["ellipse"]
        d.ellipse([(cx - rx) * scale, (cy - ry) * scale, (cx + rx) * scale, (cy + ry) * scale], fill=255)
        kind = "region"
    elif isinstance(feat, dict) and "circle" in feat:
        cx, cy, Rr = feat["circle"]
        a0, sw = feat.get("from", 180.0), feat.get("sweep", 180.0)
        pts = [((cx + Rr * math.cos(math.radians(a0 + sw * k / 48))) * scale, (cy + Rr * math.sin(math.radians(a0 + sw * k / 48))) * scale)
               for k in range(49)]
        d.line(pts, fill=255, width=max(1, int(10 * scale)))
    else:
        pts = [(float(p[0]), float(p[1])) for p in (feat["points"] if isinstance(feat, dict) else feat)]
        if len(pts) == 1:
            x, y = pts[0][0] * scale, pts[0][1] * scale
            r = 14 * scale
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
            kind = "point"
        elif len(pts) >= 2:
            length = sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))
            gap = math.dist(pts[0], pts[-1])
            if len(pts) >= 6 and gap < 0.12 * length:
                d.polygon([(x * scale, y * scale) for x, y in pts], fill=255)
                kind = "region"
            else:
                d.line([(x * scale, y * scale) for x, y in pts], fill=255, width=max(1, int(10 * scale)))
    return np.asarray(im) > 127, kind


_STOP = {"the", "and", "its", "with", "from", "over", "under", "that", "their", "this", "into", "each", "one", "two",
         "own", "all", "old", "board", "level", "moons", "moon", "pegs", "bricks", "lane", "road", "line", "lines",
         "light", "lights", "great", "traced", "outline", "ring", "rings"}


def _tokens(text):
    out = set()
    for w in "".join(ch if ch.isalpha() else " " for ch in text.lower()).split():
        if len(w) >= 4 and w not in _STOP:
            out.add(w[:-1] if w.endswith("s") and not w.endswith("ss") else w)
    return out


def subject_features(level):
    """The recipe features the subject text names (a feature "whale" for "the chart's whale"); the largest first."""
    want = _tokens(level.subject + " " + level.name)
    out = []
    for name, feat in level.features.items():
        if _tokens(name) & want:
            try:
                m, kind = feature_mask(feat)
            except (KeyError, ValueError, TypeError, IndexError):
                continue
            if m.any():
                out.append((int(m.sum()), name, m, kind))
    out.sort(key=lambda t: -t[0])
    return out


def pick_subject(subs):
    """The region to judge: the smallest named closed region of at least 300 px^2 at the view scale (the whale itself, not
    the ring of moons round it) that is not a backdrop; None when the subject is a path, points or the whole sky."""
    regions = [t for t in subs if t[3] == "region" and 300 <= t[0] <= LEG_BACKDROP]
    return min(regions, key=lambda t: t[0]) if regions else None


def save_crop(level, name, img, boxes=(), pts=(), out_dir=None, scale=2):
    """A crop of the composite with outlines (boxes: (x0, y0, x1, y1) units, pts: (x, y)) written to build/precheck/."""
    if img is None:
        return None
    try:
        out_dir = out_dir or (paths.BUILD / "precheck")
        out_dir.mkdir(parents=True, exist_ok=True)
        im = Image.fromarray(np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8))
        im = im.resize((im.width * scale, im.height * scale), Image.LANCZOS)
        d = ImageDraw.Draw(im)
        for (x0, y0, x1, y1) in boxes:
            d.rectangle([x0 * scale, y0 * scale, x1 * scale, y1 * scale], outline=(255, 40, 40), width=2)
        for (x, y) in pts:
            d.ellipse([x * scale - 6, y * scale - 6, x * scale + 6, y * scale + 6], outline=(255, 40, 40), width=2)
        p = out_dir / f"{level}-{name}.png"
        im.save(p)
        try:
            return p.relative_to(paths.REPO).as_posix()
        except ValueError:
            return str(p)
    except OSError:
        return None


# ------------------------------------------------------------------------------------------------------ 1. legibility
def measure_subject(view, pmask, mask, scale=VIEW):
    """The subject region against its surround in the 0.735 view, with the pieces masked out. Returns a dict (or None when
    too few pixels remain): de (OKLab distance of the medians), dL, side and area in view px, valid share."""
    valid = playfield_mask(scale) & ~pmask
    inner = mask & valid
    ring = _dilate(mask, 36, scale) & ~_dilate(mask, 6, scale) & valid
    if mask.sum() == 0 or inner.sum() < 120 or ring.sum() < 200 or inner.sum() < 0.12 * mask.sum():
        return None
    lab = oklab(view)
    mi, mo = np.median(lab[inner], 0), np.median(lab[ring], 0)
    ys, xs = np.nonzero(mask)
    side = float(min(xs.max() - xs.min() + 1, ys.max() - ys.min() + 1))
    return dict(de=float(np.linalg.norm(mi - mo)), dL=float(mi[0] - mo[0]), side=side, area=float(mask.sum()),
                valid=float(inner.sum() / mask.sum()), mean_in=mi.tolist(), mean_out=mo.tolist(), bbox=(xs.min() / scale, ys.min() / scale, xs.max() / scale, ys.max() / scale))


def check_legibility(L):
    if L.composite is None or not L.pieces:
        return [R("legibility", L.id, NA, "no composite or level file")]
    pick = pick_subject(subject_features(L))
    if pick is None:
        return [R("legibility", L.id, NA, "the subject is a path, a set of points or a backdrop, not a closed shape to measure: judge it on the render")]
    _a, name, mask, _kind = pick
    m = measure_subject(to_view(L.composite), piece_mask(L.pieces), mask)
    if m is None:
        return [R("legibility", L.id, NA, f'"{name}": too little of the shape is free of pieces to measure')]
    ev = [f'"{name}": shorter side {m["side"]:.0f} px, area {m["area"]:.0f} px^2 at 0.735; colour distance to its surround '
          f'{m["de"]:.3f} OKLab (dL {m["dL"]:+.3f}); bbox {tuple(round(v) for v in m["bbox"])}']
    status, why = PASS, []

    def raise_to(st, text):
        nonlocal status
        status = max(status, st, key=lambda x: ORDER[x])
        why.append(text)
    if m["side"] < LEG_FAIL_SIDE and m["area"] < LEG_FAIL_AREA:
        raise_to(FAIL, f"tiny at 640: {m['side']:.0f} px across, {m['area']:.0f} px^2 (under {LEG_FAIL_SIDE:.0f} and {LEG_FAIL_AREA:.0f})")
    elif m["side"] < LEG_WARN_SIDE or m["area"] < LEG_WARN_AREA:
        raise_to(WARN, f"small at 640: {m['side']:.0f} px across, {m['area']:.0f} px^2 (under {LEG_WARN_SIDE:.0f} or {LEG_WARN_AREA:.0f})")
    if m["de"] < LEG_FAIL_DE:
        raise_to(FAIL, f"{m['de']:.3f} from its surround (under {LEG_FAIL_DE}): it will not read")
    elif m["de"] < LEG_WARN_DE:
        raise_to(WARN, f"{m['de']:.3f} from its surround (under {LEG_WARN_DE}): faint")
    if not why:
        return [R("legibility", L.id, PASS, f'"{name}" reads at 640 ({m["side"]:.0f} px, distance {m["de"]:.3f})')]
    return [R("legibility", L.id, status, f'"{name}" ' + "; ".join(why), ev, save_crop(L.id, "legibility", L.composite, boxes=[m["bbox"]]))]


# ------------------------------------------------------------------------------------------------------------- 2. axis
def axis_zone():
    """What lies behind the aim guide's first 200 units: the cone of aims of +-12 degrees (the straight-down axis in its
    middle), from 95 to 85 + 200 units out of the pivot, at 1x."""
    im, d = _canvas(1.0)
    px, py = PIVOT
    pts = []
    for a in np.linspace(-12, 12, 13):
        t = math.radians(a)
        pts.append((px + math.sin(t) * 95, py + math.cos(t) * 95))
    for a in np.linspace(12, -12, 13):
        t = math.radians(a)
        pts.append((px + math.sin(t) * (85 + GUIDE_LEN), py + math.cos(t) * (85 + GUIDE_LEN)))
    d.polygon(pts, fill=255)
    return np.asarray(im) > 127


def check_axis(L):
    if L.composite is None:
        return [R("axis", L.id, NA, "no composite")]
    pm = piece_mask(L.pieces, scale=1.0, pad=2.0) if L.pieces else np.zeros((600, 800), bool)
    zone = axis_zone() & ~pm
    lab = oklab(L.composite)[..., 0]
    hi, mid = (lab >= AXIS_L_HI) & zone, (lab >= AXIS_L_MID) & zone
    n_hi, n_mid = float(hi.sum()), float(mid.sum())
    res = []
    ys, xs = np.nonzero(mid)
    peak = float(lab[zone].max())
    if len(xs):
        ev = [f"{n_hi:.0f} units^2 at OKLab L >= {AXIS_L_HI} and {n_mid:.0f} at >= {AXIS_L_MID} (the ball is 0.885, the dots 0.96); centre "
              f"({xs.mean():.0f}, {ys.mean():.0f}), bbox ({xs.min()}, {ys.min()})-({xs.max()}, {ys.max()}); peak L {peak:.3f}"]
    else:
        ev = []
    status, msg = PASS, f"nothing bright behind the launcher's axis or the aim guide's first 200 units (peak L {peak:.2f})"
    if n_hi >= AXIS_HI_FAIL:
        status, msg = FAIL, "a bright disc behind the aim guide: the dots and the ball vanish over it (MR 3 3-2's moon)"
    elif n_hi >= AXIS_HI_WARN or n_mid >= AXIS_MID_WARN:
        status, msg = WARN, "a light or a pale patch behind the aim guide: the dots lose contrast there (a lamp, a city light)"
    crop = None
    if status != PASS:
        crop = save_crop(L.id, "axis", L.composite, pts=[(xs.mean(), ys.mean())] if len(xs) else [],
                         boxes=[(PIVOT[0] - 60, PIVOT[1] + 85, PIVOT[0] + 60, PIVOT[1] + 85 + GUIDE_LEN)])
    res.append(R("axis", L.id, status, msg, ev if status != PASS else [], crop))
    # an engraved route (the recipe's overlay) that ends or crosses on the guide column inside the first 250 units
    routes = [o for o in (L.recipe or {}).get("overlays", []) if o.get("kind") == "route"]
    hits = []
    for o in routes:
        for (x, y) in (o["points"][0], o["points"][-1]):
            if abs(x - PIVOT[0]) <= 12 and PIVOT[1] + 85 <= y <= PIVOT[1] + 85 + GUIDE_LEN + 50:
                hits.append((x, y))
    if hits:
        res.append(R("axis", L.id, WARN, f"an engraved route ends on the guide column at {hits[:3]}: its dashes read as aim dots (UX MR 4 m2, MR 5 m2)"))
    return res


# --------------------------------------------------------------------------------------------------------- 3. repeats
def crop_rect(L):
    s = (L.recipe or {}).get("source") or {}
    if s.get("kind") != "game" or "crop" not in s:
        return None
    x, y, w, h = s["crop"]
    return s["texture"], (x, y, x + w, y + h), bool(s.get("mirror"))


def crop_overlap(a, b):
    """The share of `a`'s own area that `b`'s crop covers (same texture)."""
    ia = a[1]
    ib = b[1]
    w = max(0.0, min(ia[2], ib[2]) - max(ia[0], ib[0]))
    h = max(0.0, min(ia[3], ib[3]) - max(ia[1], ib[1]))
    return w * h / max((ia[2] - ia[0]) * (ia[3] - ia[1]), 1)


def technique_key(t):
    t = (t or "").lower().strip()
    for cut in (" (", " on the ", " with ", ", "):
        if cut in t:
            t = t.split(cut)[0]
    return t.replace("a ", "", 1) if t.startswith("a ") else t


def jewel_pair(L):
    jw = ((L.checks.get("readcheck") or {}).get("report") or {}).get("jewels") or {}
    return jw.get("first_jewel_hue"), jw.get("second_jewel_hue")


def hue_dist(a, b):
    return 360 if a is None or b is None else min(abs(a - b), 360 - abs(a - b))


def jewels_alike(a, b):
    """Within 30 degrees in both hues, either way round (the stage check's F7)."""
    return min(max(hue_dist(a[0], b[0]), hue_dist(a[1], b[1])), max(hue_dist(a[0], b[1]), hue_dist(a[1], b[0]))) < 30


def occupancy(L, nx=10, ny=8):
    """Pieces per cell of a 10 x 8 grid of the playable board (surface samples counted, so a brick spreads)."""
    g = np.zeros((ny, nx))
    for p in L.pieces:
        for (x, y) in (p["pts"] if p["kind"] != "peg" else p["pts"]):
            w = 1.0 if p["kind"] == "peg" else 4.0 / max(1, 2 * len(p["pts"]))
            cx = min(nx - 1, max(0, int((x - LEFT) / (RIGHT - LEFT) * nx)))
            cy = min(ny - 1, max(0, int((y - TOP) / (LOWEST - TOP) * ny)))
            g[cy, cx] += w
    return g


def symmetry(L):
    """How mirror-symmetric the piece density is: the 10 x 8 grid against its left-right flip (1 is a mirror)."""
    if not L.pieces:
        return None
    g = occupancy(L)
    return pearson(g, g[:, ::-1])


def pearson(a, b):
    a, b = a.ravel() - a.mean(), b.ravel() - b.mean()
    d = math.sqrt(float((a * a).sum() * (b * b).sum()))
    return float((a * b).sum() / d) if d > 0 else 0.0


def pair_compare(a, b, adjacent, full=True):
    """Findings between level a (the later) and b: a list of (status, text)."""
    out = []
    ca, cb = crop_rect(a), crop_rect(b)
    if ca and cb and ca[0] == cb[0]:
        ov = crop_overlap(ca, cb)
        if ov <= CROP_WARN and a.stage == b.stage:
            out.append((WARN, f"{a.id} and {b.id} are crops of the same painting ({ca[0].split('/')[-1]}), {ov:.0%} overlap: one look twice in a stage "
                              f"(MR 5 round 1, 5-1 and 5-4)"))
        if ov > CROP_WARN:
            out.append((FAIL if ov > CROP_FAIL else WARN,
                        f"crops of {ca[0].split('/')[-1]} overlap {ov:.0%} of {a.id}'s own area ({a.id} {list(a.recipe['source']['crop'])}, "
                        f"{b.id} {list(b.recipe['source']['crop'])}; the charter allows 25%)"))
    if not full:
        return out
    same_stage = a.stage == b.stage
    pa, pb = jewel_pair(a), jewel_pair(b)
    if None not in pa and None not in pb and jewels_alike(pa, pb):
        sym_a, sym_b = symmetry(a), symmetry(b)
        if adjacent:
            out.append((FAIL, f"the same jewel pair as {b.id}, the next level: {pa} against {pb} (within 30 degrees in both)"))
        elif same_stage and sym_a is not None and sym_b is not None and sym_a >= SYM_BOTH and sym_b >= SYM_BOTH:
            out.append((FAIL, f"the same jewel pair as {b.id} ({pa} against {pb}) and both boards are mirror-symmetric ({sym_a:.2f}, {sym_b:.2f}): "
                              f"one centred picture twice (MR 6 round 1, 6-1 and 6-5)"))
        else:
            out.append((WARN, f"the same jewel pair as {b.id}: {pa} against {pb} (within 30 degrees in both)"))
    ta, tb = technique_key(a.technique), technique_key(b.technique)
    if adjacent and ta and ta == tb:
        out.append((FAIL if same_stage else WARN, f'the same technique as {b.id} back to back{"" if same_stage else " across the stage boundary"}: "{ta}"'))
    if a.pieces and b.pieces:
        r = pearson(occupancy(a), occupancy(b))
        if r >= LAYOUT_R_WARN:
            out.append((FAIL if r >= LAYOUT_R_FAIL else WARN, f"layout statistics like {b.id}: piece density over a 10 x 8 grid correlates {r:.2f}"))
    return out


def neighbours(L, everything):
    """(other level, adjacent?, full?) for the levels the player meets near this one: the stage's others and the levels
    next to it (all findings: `full`), plus every earlier level of the campaign on the same texture (crop overlap only)."""
    out = []
    for o in everything:
        if o.id == L.id or not o.ok or o.id.split("-")[0] != L.id.split("-")[0]:
            continue
        adjacent = abs((o.number or 0) - (L.number or 0)) == 1
        if o.stage == L.stage or adjacent:
            out.append((o, adjacent, True))
        else:
            ca, cb = crop_rect(L), crop_rect(o)
            if ca and cb and ca[0] == cb[0]:
                out.append((o, False, False))
    return out


def check_repeats(L, everything):
    found = []
    for o, adjacent, full in neighbours(L, everything):
        later, earlier = (L, o) if (L.number or 0) >= (o.number or 0) else (o, L)
        for status, text in pair_compare(later, earlier, adjacent, full):
            found.append((status, f"{later.id} vs {earlier.id}: {text}"))
    if not found:
        return [R("repeats", L.id, PASS, "no near-repeat with the stage's other levels or the level before it")]
    return [R("repeats", L.id, status, text) for status, text in sorted(found, key=lambda t: -ORDER[t[0]])]


# --------------------------------------------------------------------------------------------------------- 4. patches
def surface_samples(pieces):
    """(N, 3) array of x, y, r over every piece's surface samples."""
    rows = []
    for p in pieces:
        for (x, y) in p["pts"]:
            rows.append((x, y, p["r"]))
    return np.asarray(rows, float).reshape(-1, 3)


def free_distance(pieces, step=5.0):
    """Distance from each grid point of the playable area to the nearest piece's surface (negative inside a piece), over
    x LEFT..RIGHT, y TOP..LOWEST. Points inside the launcher's clearance or beyond the walls are excluded by the caller."""
    xs = np.arange(LEFT, RIGHT + 0.1, step)
    ys = np.arange(TOP, LOWEST + 0.1, step)
    X, Y = np.meshgrid(xs, ys)
    S = surface_samples(pieces)
    D = np.full(X.shape, 1e9)
    for (x, y, r) in S:
        D = np.minimum(D, np.hypot(X - x, Y - y) - r)
    return xs, ys, X, Y, D


def empty_circles(pieces):
    """The largest empty circle (centre, radius) among the board's free points. The launcher's clearance, the walls (a
    circle may touch them) and the bucket's strip (y > 520) are not counted as empty."""
    xs, ys, X, Y, D = free_distance(pieces)
    # a circle's room also stops at the walls and the top, so the wall counts as a surface
    wall = np.minimum.reduce([X - LEFT, RIGHT - X, Y - TOP])
    room = np.minimum(np.minimum(D, wall), EMPTY_FLOOR - Y)
    room[np.hypot(X - PIVOT[0], Y - PIVOT[1]) < 85 + 20] = 0
    room[(Y > EMPTY_Y[1]) | (Y < EMPTY_Y[0])] = 0
    j, i = np.unravel_index(np.argmax(room), room.shape)
    return float(room[j, i]), (float(X[j, i]), float(Y[j, i])), room, (X, Y)


def crowded_windows(pieces, win=90.0, step=15.0):
    """The piece area as a share of the window, for each 90 x 90 window; the densest (share, x, y)."""
    scale = 1.0
    im, d = _canvas(scale)
    for p in pieces:
        r = p["r"]
        pts = p["pts"]
        if p["kind"] == "peg":
            x, y = pts[0]
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
        else:
            d.line(pts, fill=255, width=max(1, int(2 * r)))
            for x, y in (pts[0], pts[-1]):
                d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    m = np.asarray(im) > 127
    best = (0.0, 0, 0)
    ii = np.cumsum(np.cumsum(m, 0), 1)
    ii = np.pad(ii, ((1, 0), (1, 0)))
    w = int(win)
    for y in range(int(TOP), int(LOWEST - w) + 1, int(step)):
        for x in range(int(LEFT), int(RIGHT - w) + 1, int(step)):
            s = ii[y + w, x + w] - ii[y, x + w] - ii[y + w, x] + ii[y, x]
            share = s / (w * w)
            if share > best[0]:
                best = (float(share), x + w // 2, y + w // 2)
    return best


def strays(pieces):
    """Pieces with no other piece within STRAY_GAP (surface to surface): [(x, y, gap)]."""
    out = []
    for i, p in enumerate(pieces):
        if p["mover"]:
            continue
        gap = 1e9
        for j, q in enumerate(pieces):
            if i == j:
                continue
            g = min(math.dist(a, b) for a in p["pts"] for b in q["pts"]) - p["r"] - q["r"]
            gap = min(gap, g)
        if gap > STRAY_GAP:
            out.append((round(p["pts"][len(p["pts"]) // 2][0]), round(p["pts"][len(p["pts"]) // 2][1]), round(gap)))
    return out


def feature_distance_fn(L):
    """Distance from a point to the nearest traced feature (recipe features and painter features), or None."""
    segs, discs = [], []
    for name, feat in L.features.items():
        try:
            if isinstance(feat, dict) and "ellipse" in feat:
                cx, cy, rx, ry = feat["ellipse"]
                pts = [(cx + rx * math.cos(a), cy + ry * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 48)]
            elif isinstance(feat, dict) and "circle" in feat:
                cx, cy, Rr = feat["circle"]
                a0, sw = feat.get("from", 180.0), feat.get("sweep", 180.0)
                pts = [(cx + Rr * math.cos(math.radians(a0 + sw * k / 48)), cy + Rr * math.sin(math.radians(a0 + sw * k / 48))) for k in range(49)]
            else:
                pts = [(float(p[0]), float(p[1])) for p in (feat["points"] if isinstance(feat, dict) else feat)]
        except (KeyError, TypeError, ValueError, IndexError):
            continue
        if len(pts) == 1:
            discs.append(pts[0])
        for a, b in zip(pts, pts[1:]):
            segs.append((a, b))
    if not segs and not discs:
        return None
    A = np.asarray([s[0] for s in segs], float).reshape(-1, 2)
    B = np.asarray([s[1] for s in segs], float).reshape(-1, 2)
    Dp = np.asarray(discs, float).reshape(-1, 2)

    def dist(x, y):
        best = 1e9
        if len(A):
            ab = B - A
            t = np.clip(((x - A[:, 0]) * ab[:, 0] + (y - A[:, 1]) * ab[:, 1]) / np.maximum((ab ** 2).sum(1), 1e-9), 0, 1)
            best = min(best, float(np.hypot(A[:, 0] + t * ab[:, 0] - x, A[:, 1] + t * ab[:, 1] - y).min()))
        if len(Dp):
            best = min(best, float(np.hypot(Dp[:, 0] - x, Dp[:, 1] - y).min()))
        return best
    return dist


def untraced(L):
    """The share of pieces further than UNTRACED_DIST from every traced feature, and the list: (share, [(x, y)])."""
    fn = feature_distance_fn(L)
    if fn is None or len(L.features) < 4:
        return None
    far = []
    for p in L.pieces:
        x, y = p["pts"][len(p["pts"]) // 2]
        if fn(x, y) - p["r"] > UNTRACED_DIST:
            far.append((round(x), round(y)))
    return len(far) / max(len(L.pieces), 1), far


def wall_ledges(pieces):
    """Pieces that leave a gap of 12.5 to 16 units to a side wall (a 12-unit ball passes by a hair): [(x, y, gap)]."""
    out = []
    for p in pieces:
        if p["mover"]:
            continue
        for (x, y) in p["pts"]:
            g = min(x - p["r"] - LEFT, RIGHT - x - p["r"])
            if LEDGE_GAP[0] <= g < LEDGE_GAP[1]:
                out.append((round(x), round(y), round(g, 1)))
                break
    return out


def check_patches(L):
    if not L.pieces:
        return [R("patches", L.id, NA, "no level file")]
    res = []
    rad, at, room, (X, Y) = empty_circles(L.pieces)
    st = FAIL if rad >= EMPTY_FAIL else WARN if rad >= EMPTY_WARN else PASS
    crop = None
    if st != PASS:
        crop = save_crop(L.id, "empty", L.composite, boxes=[(at[0] - rad, at[1] - rad, at[0] + rad, at[1] + rad)])
    res.append(R("patches", L.id, st, f"largest empty circle r {rad:.0f} at ({at[0]:.0f}, {at[1]:.0f})" +
                 (f": a hole players will see (WARN {EMPTY_WARN:.0f}, FAIL {EMPTY_FAIL:.0f})" if st != PASS else ""), [], crop))
    share, cx, cy = crowded_windows(L.pieces)
    st = FAIL if share >= CROWD_FAIL else WARN if share >= CROWD_WARN else PASS
    crop = save_crop(L.id, "crowded", L.composite, boxes=[(cx - 45, cy - 45, cx + 45, cy + 45)]) if st != PASS else None
    res.append(R("patches", L.id, st, f"densest 90 x 90 window is {share:.0%} pieces at ({cx}, {cy})" +
                 (f" (WARN {CROWD_WARN:.0%}, FAIL {CROWD_FAIL:.0%})" if st != PASS else ""), [], crop))
    sy = strays(L.pieces)
    if sy:
        res.append(R("patches", L.id, WARN, f"{len(sy)} stray piece(s) with nothing within {STRAY_GAP:.0f} units: {sy[:6]}",
                     [], save_crop(L.id, "strays", L.composite, pts=[(x, y) for x, y, _ in sy])))
    u = untraced(L)
    if u is not None:
        sh, far = u
        st = FAIL if sh >= UNTRACED_FAIL else WARN if sh >= UNTRACED_WARN else PASS
        res.append(R("patches", L.id, st, f"{len(far)} of {len(L.pieces)} pieces ({sh:.0%}) are over {UNTRACED_DIST:.0f} units from any traced feature"
                     + (f" (WARN {UNTRACED_WARN:.0%}, FAIL {UNTRACED_FAIL:.0%})" if st != PASS else ""),
                     [f"e.g. {far[:8]}"] if far and st != PASS else [], save_crop(L.id, "untraced", L.composite, pts=far) if st != PASS else None))
    le = wall_ledges(L.pieces)
    if le:
        res.append(R("patches", L.id, WARN, f"{len(le)} piece(s) leave a wall gap of {LEDGE_GAP[0]}-{LEDGE_GAP[1]} units (a ball passes by a hair): {le[:4]}"))
    return res


# ------------------------------------------------------------------------------------------------------------ 5. play
def check_play(L):
    pl = L.checks.get("play")
    if not pl:
        return [R("play", L.id, NA, "no play figures in the build report")]
    res = []
    fires = pl.get("stuck_fires")
    sw = L.checks.get("sweep") or {}
    if fires is not None:
        st = FAIL if fires >= STUCK_FAIL else WARN if fires >= STUCK_WARN else PASS
        res.append(R("play", L.id, st, f"the stuck rule fires {fires} times in {pl.get('ramp_games', 1728)} games (sweep: {sw.get('stuck', '?')} of "
                     f"{sw.get('angles', '?')} aims); shipped maximum 88" + (f" (WARN {STUCK_WARN}, FAIL {STUCK_FAIL}); `mfl.py stuck {L.id}` shows where the balls rest" if st != PASS else "")))
    mean = sw.get("pegs_mean")
    if mean is not None:
        st = FAIL if mean < THIN_FAIL else WARN if mean < THIN_WARN else PASS
        res.append(R("play", L.id, st, f"a first shot touches {mean} pieces on average (median {sw.get('pegs_median')}, most {sw.get('pegs_max')})"
                     + (f": a thin board, the ball falls through open middles (WARN under {THIN_WARN}, FAIL under {THIN_FAIL}; 6-2, round 1: 6.4)" if st != PASS else "")))
    hs = pl.get("holdouts") or []
    if hs:
        hs = sorted(hs, key=lambda h: -h["share_of_lost_games"])
        top = hs[0]
        top3 = sum(h["share_of_lost_games"] for h in hs[:3])
        near_wall = [h for h in hs[:5] if min(h["at"][0] - LEFT, RIGHT - h["at"][0]) < 40 and h["share_of_lost_games"] >= 0.20]
        st = FAIL if top["share_of_lost_games"] >= HOLD_FAIL else WARN if (top["share_of_lost_games"] >= HOLD_WARN or top3 >= HOLD3_WARN or near_wall) else PASS
        ev = [f"top holdouts {[(tuple(h['at']), h['share_of_lost_games']) for h in hs[:4]]}"]
        why = []
        if top["share_of_lost_games"] >= HOLD_WARN:
            why.append(f"{tuple(top['at'])} is the last orange in {top['share_of_lost_games']:.0%} of lost games")
        if top3 >= HOLD3_WARN:
            why.append(f"the top three hold {top3:.0%}")
        if near_wall:
            why.append(f"wall-side holdouts {[(tuple(h['at']), h['share_of_lost_games']) for h in near_wall]} (losing to a peg tucked against a wall feels cheap)")
        res.append(R("play", L.id, st, "; ".join(why) if why else f"holdouts spread: top {top['share_of_lost_games']:.0%}, top three {top3:.0%}", ev))
    return res


# --------------------------------------------------------------------------------------------------------- 6. margins
def check_margins(L):
    rc = ((L.checks.get("readcheck") or {}).get("report")) or {}
    if not rc:
        return [R("margins", L.id, NA, "no readability figures in the build report")]
    w, status = [], PASS
    if rc.get("worst_margin") is not None and rc["worst_margin"] < F6_WARN:
        w.append(f"F6 worst margin {rc['worst_margin']} (floor 0.20; under {F6_WARN} leaves a hair)")
    p10, mn = rc.get("protan_orange_p10"), rc.get("protan_min")
    if p10 is not None and p10 < F9_P10_WARN:
        w.append(f"F9 protan p10 {p10} (under the 0.105 guard UX set in MR 5; it passes only on the 0.101 pilot fallback)")
    if mn is not None and mn < F9_MIN_WARN:
        w.append(f"F9 protan minimum {mn} at {rc.get('protan_worst_at')} (under {F9_MIN_WARN})")
    if p10 is not None and mn is not None and p10 < F9_P10_WARN and mn < F9_MIN_FAIL:
        status = FAIL
        w.append("both: orange is the hardest to separate for a protanope of any approved level (UX MR 4 M1)")
    jw = rc.get("jewels") or {}
    if jw.get("second_share") is not None and jw["second_share"] < SECOND_WARN:
        w.append(f"the second jewel's share is {jw['second_share']} (floor 0.15)")
    if w:
        return [R("margins", L.id, status if status == FAIL else WARN, "; ".join(w))]
    return [R("margins", L.id, PASS, f"F6 {rc.get('worst_margin')}, protan p10 {p10} / min {mn}, second jewel {jw.get('second_share')}")]


# -------------------------------------------------------------------------------------------------------------- run
CHECKS = ("legibility", "axis", "repeats", "patches", "play", "margins")


def run_level(L, everything, only=None):
    """All results for one level."""
    if not L.ok:
        return [R("precheck", L.id, NA, "no built level file or report (build it first)")]
    fns = dict(legibility=lambda: check_legibility(L), axis=lambda: check_axis(L), repeats=lambda: check_repeats(L, everything),
               patches=lambda: check_patches(L), play=lambda: check_play(L), margins=lambda: check_margins(L))
    out = []
    for name in (only or CHECKS):
        out += fns[name]()
    return out


def load_all(src):
    return [Level(src, i) for i in src.ids()]


def precheck(ids, rev=None, only=None, print_=print, all_levels=None):
    """Runs the checks for `ids`; prints the report; returns (results, worst status)."""
    src = Source(rev)
    everything = all_levels or load_all(src)
    by = {L.id: L for L in everything}
    results = []
    for lid in ids:
        L = by.get(lid) or Level(src, lid)
        rs = run_level(L, everything, only)
        results += rs
        print_(f"[{lid}] {L.name}: " + ", ".join(f"{c} {worst([r for r in rs if r.check == c])}" for c in dict.fromkeys(r.check for r in rs)))
        for r in rs:
            if r.status in (WARN, FAIL) or r.status == NA:
                print_(r.line())
    w = worst(results)
    print_(f"precheck: {sum(r.status == FAIL for r in results)} FAIL, {sum(r.status == WARN for r in results)} WARN, "
           f"{sum(r.status == PASS for r in results)} PASS, {sum(r.status == NA for r in results)} N/A over {len(ids)} level(s)"
           + (f" (read from {rev})" if rev else "") + " - report only, not a gate")
    return results, w


# ----------------------------------------------------------------------------------------------------------- selftest
def _synthetic_level(pegs, bricks=(), name="Synthetic", subject="", features=None, composite=None, report=None, recipe=None, lid="selftest-pc"):
    L = Level.__new__(Level)
    L.id, L.name, L.subject, L.technique = lid, name, subject, (report or {}).get("technique", "")
    L.file = {"pegs": list(pegs), "bricks": list(bricks)}
    L.pieces = pieces_of(L.file)
    L.features = features or {}
    L.recipe = recipe or {}
    L.composite = composite
    L.report = report or {}
    L.checks = (report or {}).get("checks", {})
    L.stage, L.number = (report or {}).get("stage", 1), (report or {}).get("number", 1)
    return L


def selftest(verbose=True):
    """Every precheck function against known-bad and known-good synthetic cases (no files, no game, no build). The cases
    mirror the supervisors' findings: the whale at 640, the moon on the launcher's axis, the same jewels, a hole, a jam."""
    cases = []

    def add(name, good):
        cases.append((name, bool(good)))

    # a plausible board: a 9 x 8 lattice of pegs (spacing 66 x 50, r 10) over x 110..638, y 150..500, clear of the launcher
    grid = [{"x": 110 + 66 * i, "y": 150 + 50 * j, "r": 10, "canBeOrange": True} for i in range(9) for j in range(8)]
    sky = (0.10, 0.12, 0.25)
    lopsided = [{"x": 110 + 40 * k, "y": 150 + 30 * k, "r": 10, "canBeOrange": True} for k in range(12)]

    def scene(box=None, rgb=None, bg=sky):
        img = np.zeros((600, 800, 3), np.float32)
        img[:] = bg
        if box:
            x0, y0, x1, y1 = box
            img[y0:y1, x0:x1] = rgb
        return img

    def outline(x0, y0, x1, y1):
        """A closed outline of 8 points: a region to the measure."""
        xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
        return [[x0, y0], [xm, y0], [x1, y0], [x1, y1], [xm, y1], [x0, y1], [x0, ym], [x0, y0 + 6]]
    far = [{"x": 650, "y": 500, "r": 10, "canBeOrange": True}]
    # legibility (the whale of MR 3, round 1: 43 px across, 1406 px^2, 'small and dark on dark')
    for nm, box, rgb, want in (
            ("a large lit subject reads", (150, 250, 380, 380), (0.55, 0.62, 0.85), PASS),
            ("a large subject the colour of its surround will not read", (150, 250, 380, 380), (0.10, 0.12, 0.25), FAIL),
            ("a 70 x 30 lit subject is tiny at 640", (200, 300, 270, 330), (0.55, 0.62, 0.85), FAIL),
            ("a 140 x 52 lit subject is small at 640", (150, 300, 290, 352), (0.55, 0.62, 0.85), WARN)):
        L = _synthetic_level(far, subject="the whale", features={"whale": outline(*box)}, composite=scene(box, rgb))
        r = check_legibility(L)[0]
        add(f"legibility: {nm} -> {r.status}", r.status == want)
    L = _synthetic_level(far, subject="the whale", features={"whale": [[150, 300], [290, 300]]}, composite=scene())
    add("legibility: a subject that is a line is N/A, not a false finding", check_legibility(L)[0].status == NA)
    L = _synthetic_level(far, subject="nothing named here", features={"zzz": outline(150, 250, 380, 380)}, composite=scene())
    add("legibility: no feature named by the subject is N/A", check_legibility(L)[0].status == NA)
    # axis: a moon behind the guide (MR 3, 3-2: moon [400, 220, 30]), a lamp, a moon off to the side
    yy, xx = np.mgrid[0:600, 0:800]

    def disc(cx, cy, r, rgb=(0.95, 0.95, 0.9)):
        img = scene()
        img[(xx - cx) ** 2 + (yy - cy) ** 2 < r * r] = rgb
        return img
    for nm, img, want in (("a moon of r 30 on the launcher's axis", disc(400, 220, 30), FAIL),
                          ("a lamp of r 5 behind the guide", disc(404, 230, 5), WARN),
                          ("the same moon 200 units to the side", disc(600, 220, 30), PASS),
                          ("a plain sky", scene(), PASS)):
        r = check_axis(_synthetic_level([], composite=img))[0]
        add(f"axis: {nm} -> {r.status}", r.status == want)
    rt = _synthetic_level([], composite=scene(), recipe={"overlays": [{"kind": "route", "points": [[300, 200], [395, 300]]}]})
    add("axis: an engraved route ending on the guide column", any("route" in x.msg for x in check_axis(rt)))
    rt = _synthetic_level([], composite=scene(), recipe={"overlays": [{"kind": "route", "points": [[300, 200], [398, 300], [500, 340]]}]})
    add("axis: a route that only crosses the column is not flagged", not any("route" in x.msg for x in check_axis(rt)))

    # repeats
    def mk(i, crop, tex="t.tex", jew=(280, 350), tech="a trail on a map", stage=1, pegs=None):
        rep = {"stage": stage, "number": i, "technique": tech,
               "checks": {"readcheck": {"report": {"jewels": {"first_jewel_hue": jew[0], "second_jewel_hue": jew[1]}}}}}
        return _synthetic_level(pegs or grid, report=rep, recipe={"source": {"kind": "game", "texture": tex, "crop": crop}}, lid=f"selftest-r{i}")
    a, b = mk(1, [0, 0, 1000, 800]), mk(2, [600, 0, 1000, 800], jew=(120, 20), tech="terrain emphasis")
    add("repeats: crops overlapping 40% of the later one's area (FAIL)", any(s_ == FAIL and "overlap" in t for s_, t in pair_compare(b, a, True)))
    c = mk(3, [1100, 0, 1000, 800], jew=(120, 20), tech="terrain emphasis")
    add("repeats: the same painting, disjoint crops, in one stage (WARN)", any(s_ == WARN and "same painting" in t for s_, t in pair_compare(c, a, False)))
    add("repeats: a crop of the same painting in another stage is not a repeat",
        not pair_compare(mk(3, [1100, 0, 1000, 800], jew=(120, 20), tech="x", stage=2, pegs=lopsided), a, False))
    add("repeats: disjoint crops, other jewels and technique: nothing about jewels or technique",
        not any(k in t for s_, t in pair_compare(c, a, True) for k in ("jewel", "technique")))
    far_crop = [3000, 0, 100, 100]
    add("repeats: the same jewels next to each other (FAIL), two apart (WARN)",
        any(s_ == FAIL and "jewel" in t for s_, t in pair_compare(mk(2, far_crop), a, True))
        and any(s_ == WARN and "jewel" in t for s_, t in pair_compare(mk(3, far_crop, pegs=lopsided), a, False)))
    add("repeats: the same technique back to back (FAIL), not when apart, only a WARN across a stage boundary",
        any(s_ == FAIL and "technique" in t for s_, t in pair_compare(mk(2, far_crop, jew=(120, 20)), a, True))
        and not any("technique" in t for s_, t in pair_compare(mk(3, far_crop, jew=(120, 20)), a, False))
        and any(s_ == WARN and "technique" in t for s_, t in pair_compare(mk(2, far_crop, jew=(120, 20), stage=2), a, True)))
    add("repeats: the same technique, text after the bracket ignored",
        technique_key("a landmark partly outlined (the city's wall)") == technique_key("a landmark partly outlined"))
    # 6-1 and 6-5, round 1: the same jewels and both a centred, mirror-symmetric board
    mirror = [{"x": 400 + s_ * dx, "y": y, "r": 10, "canBeOrange": True} for dx in (60, 140, 220, 300) for s_ in (-1, 1) for y in (200, 300, 400)]
    m1, m2 = mk(1, far_crop, jew=(284, 214), pegs=mirror), mk(5, far_crop, jew=(313, 214), pegs=mirror)
    add("repeats: the same jewels on two mirror-symmetric boards in a stage (FAIL)",
        any(s_ == FAIL and "centred" in t for s_, t in pair_compare(m2, m1, False)))
    add("repeats: the same jewels, one lopsided board, is only a WARN",
        all(s_ == WARN for s_, t in pair_compare(mk(5, far_crop, jew=(313, 214), pegs=lopsided), m1, False) if "jewel" in t))
    other = _synthetic_level([{"x": 120 + 40 * k, "y": 480, "r": 10, "canBeOrange": True} for k in range(15)], lid="selftest-ro")
    add("repeats: a lattice against a single row has a different layout", pearson(occupancy(other), occupancy(a)) < LAYOUT_R_WARN)
    add("repeats: the same lattice twice has the same layout, and is flagged",
        pearson(occupancy(a), occupancy(mk(9, far_crop))) > 0.99 and any("layout" in t for s_, t in pair_compare(mk(9, far_crop, jew=(1, 2)), a, False)))

    # patches
    holey = [p for p in grid if not (230 < p["x"] < 560 and 150 < p["y"] < 500)]
    add("patches: a board with a 330 x 350 hole has a big empty circle", empty_circles(pieces_of({"pegs": holey}))[0] >= EMPTY_WARN)
    add("patches: an even lattice has no hole", empty_circles(pieces_of({"pegs": grid}))[0] < EMPTY_WARN)
    jam = [{"x": 300 + 14 * i, "y": 300 + 14 * j, "r": 10} for i in range(6) for j in range(6)]
    add("patches: a jam of overlapping pegs is crowded", crowded_windows(pieces_of({"pegs": jam}))[0] >= CROWD_FAIL)
    add("patches: an even lattice is not crowded", crowded_windows(pieces_of({"pegs": grid}))[0] < CROWD_WARN)
    add("patches: one peg alone in a field is a stray",
        len(strays(pieces_of({"pegs": [{"x": 120, "y": 480, "r": 10}, {"x": 700, "y": 150, "r": 10}, {"x": 650, "y": 170, "r": 10}]}))) == 1)
    add("patches: a lattice has no strays", not strays(pieces_of({"pegs": grid})))
    add("patches: a brick ending 13.5 from the wall is a ledge (5-3's jetty)",
        bool(wall_ledges(pieces_of({"bricks": [{"kind": "line", "x1": 500, "y1": 373, "x2": 706, "y2": 373, "thickness": 10}]}))))
    add("patches: a brick 30 from the wall is not",
        not wall_ledges(pieces_of({"bricks": [{"kind": "line", "x1": 500, "y1": 373, "x2": 680, "y2": 373, "thickness": 10}]})))
    ft = {"a": [[100, 100], [700, 100]], "b": [[100, 300], [700, 300]], "c": [[100, 400], [700, 400]], "d": [[100, 500], [700, 500]]}
    on = [{"x": 100 + 60 * k, "y": 100, "r": 10} for k in range(10)]
    between = [{"x": 100 + 60 * k, "y": 200, "r": 10} for k in range(14)]
    add("patches: pieces on the traced lines are traced", untraced(_synthetic_level(on + on, features=ft))[0] < UNTRACED_WARN)
    add("patches: half the pieces between the lines trace nothing (3-5, round 1: 51%)",
        untraced(_synthetic_level(on + between, features=ft))[0] >= UNTRACED_FAIL - 0.05)
    add("patches: a level with fewer than four features is not judged", untraced(_synthetic_level(on, features={"a": ft["a"]})) is None)

    # play
    def rep(fires, hold, mean=9.0):
        return {"checks": {"play": {"stuck_fires": fires, "ramp_games": 1728, "holdouts": [{"at": a_, "share_of_lost_games": s_} for a_, s_ in hold]},
                           "sweep": {"stuck": 5, "angles": 681, "pegs_mean": mean, "pegs_median": round(mean)}}}
    flat = [((300, 300), 0.08), ((400, 300), 0.07), ((500, 300), 0.06)]

    def by(rp, text):
        return [r for r in check_play(_synthetic_level([], report=rp)) if text in r.msg]
    add("play: 155 stuck fires (4-3, round 1) -> FAIL", by(rep(155, flat), "stuck rule")[0].status == FAIL)
    add("play: 73 stuck fires (6-3) -> WARN", by(rep(73, flat), "stuck rule")[0].status == WARN)
    add("play: 54 stuck fires -> PASS", by(rep(54, flat), "stuck rule")[0].status == PASS)
    add("play: holdouts of 25% and 23% against the right wall (3-3) -> WARN",
        by(rep(40, [((702, 228), 0.25), ((714, 292), 0.23), ((300, 300), 0.1)]), "holdout")[0].status == WARN
        or by(rep(40, [((702, 228), 0.25), ((714, 292), 0.23), ((300, 300), 0.1)]), "last orange")[0].status == WARN)
    add("play: one orange left in 40% of lost games -> FAIL", by(rep(40, [((400, 300), 0.40)] + flat), "last orange")[0].status == FAIL)
    add("play: flat holdouts -> PASS", [r for r in check_play(_synthetic_level([], report=rep(40, flat))) if r.msg.startswith("holdouts spread")][0].status == PASS)
    add("play: a first shot touching 6.4 pieces (6-2, round 1) -> FAIL, 7.0 -> WARN, 9.0 -> PASS",
        [by(rep(40, flat, m), "first shot")[0].status for m in (6.4, 7.0, 9.0)] == [FAIL, WARN, PASS])

    # margins
    def rr(margin, p10, mn, share):
        return {"checks": {"readcheck": {"report": {"worst_margin": margin, "protan_orange_p10": p10, "protan_min": mn, "jewels": {"second_share": share}}}}}
    add("margins: 4-4, round 1 (p10 0.101, minimum 0.083) -> FAIL",
        check_margins(_synthetic_level([], report=rr(0.30, 0.101, 0.083, 0.25)))[0].status == FAIL)
    add("margins: 6-1 (F6 0.205) and 3-1 (second jewel 0.159) -> WARN",
        all(check_margins(_synthetic_level([], report=rr(*v)))[0].status == WARN for v in ((0.205, 0.13, 0.12, 0.25), (0.27, 0.13, 0.12, 0.159))))
    add("margins: comfortable figures pass", check_margins(_synthetic_level([], report=rr(0.30, 0.13, 0.115, 0.25)))[0].status == PASS)
    # the colour space: white is L 1, black L 0; the reporter ranks FAIL over WARN over PASS
    add("oklab: white L 1.0 and black L 0.0", abs(oklab(np.ones(3))[0] - 1) < 1e-3 and abs(oklab(np.zeros(3))[0]) < 1e-3)
    add("worst: FAIL over WARN over PASS", worst([R("c", "l", PASS, ""), R("c", "l", FAIL, ""), R("c", "l", WARN, "")]) == FAIL)
    ok = True
    for name, good in cases:
        ok &= good
        if verbose:
            print(f"  {'ok ' if good else 'BAD'} precheck: {name}")
    return ok
