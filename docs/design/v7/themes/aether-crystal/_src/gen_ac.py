"""Aether Crystal, v7 theme: faces, the Silver frame kit, and composites.

Writes into docs/design/v7/themes/aether-crystal:
- faces/<state>-under.svg and faces/<state>-over.svg (hero faces: well content, and the overhangs drawn above the
  frame), and faces/row/... (the row-tier faces);
- kit/ (the Silver kit): frame-<tier>-<finish>.svg for act-now, resting, finished, ghost at full and quiet,
  badge-frame.svg, badge-seat-<kind>.svg, badge-open.svg, badge-closed.svg, badge-journal.svg;
- <state>.svg: composites (face under + kit frame for the state's tier + face over + badge), as designed;
- _row/<state>.svg: row composites (row face + frame, no badge) and _row/badge-*.svg (the badge glyphs alone).

Every face sits on one common circular well: centre (64, 64), radius 52.4 (the shipped Medallion well). The kit's
frame covers the well's edge, so any face sits in any kit.

The moon is cut moonstone. Every cut surface is flat facets, and every facet takes one flat value from its own normal:
- moons are lit by their own sun (the phase), Lambert per facet, so the facets brighten toward the lit limb;
- everything else (frame, badge, emblems, the frost bank, Dalamud) is lit by the one UI key light from the upper left
  (azimuth 135 deg, elevation 45 deg), with a Blinn specular.
Aether blue appears only with a physical cause: the moonstone's adularescence, the thin transmitted rim on a back-lit
crescent's dark limb, and the inner edge of Done's arrow. There is no glow around any glyph.
"""
import base64, math, pathlib
import numpy as np

OUT = pathlib.Path(__file__).resolve().parent.parent
REPO = OUT.parents[4]
JOBDIR = REPO / "docs/design/moon-v6/round5/medallion-r5/_src/jobs"     # the game's job glyphs (read only)

# ---------------- palette tokens ----------------
KEY = "#070A15"
WELL_T, WELL_B = "#1C2856", "#0E1533"                                     # night crystal well
# Silver kit: moonstone silver, low chroma so it reads as metal beside brass and lead (critic 11)
SILVER_KIT = [(0, "#12172A"), (.3, "#353D56"), (.55, "#646F90"), (.78, "#AEB7CC"), (.92, "#E2E8F4"), (1, "#F8FAFD")]
# act-now gilt: the shared medal bezel ramp, in every kit (supervisor S2)
GILT_KIT = [(0, "#2A1F0C"), (.3, "#5C4724"), (.5, "#7C6236"), (.7, "#9A7E4A"), (.88, "#E6CF98"), (1, "#FFF4D6")]
MOONSTONE = [(0, "#4A587F"), (.3, "#7E8DB3"), (.6, "#BAC6E0"), (.85, "#E2E8F4"), (1, "#F7F8FB")]
EARTH = [(0, "#18214A"), (.5, "#253264"), (1, "#34437A")]
AETHER, AETHER_HI, AETHER_DEEP = "#7FAEEA", "#C2DAFB", "#3D64A8"
GOLD = [(0, "#4A3818"), (.3, "#7C6236"), (.55, "#B0904F"), (.78, "#D9BE82"), (.92, "#F0DDA8"), (1, "#FFF6DC")]
SILVER = [(0, "#232B48"), (.3, "#4E5C82"), (.55, "#8292B8"), (.8, "#B8C4DE"), (1, "#E6ECF8")]
FROST = [(0, "#2A3352"), (.4, "#4E5C82"), (.72, "#7C89B0"), (1, "#9AA6C8")]   # capped at #9AA6C8, about 80% of the limb (review round 2)
DALAMUD = [(0, "#2A0C14"), (.3, "#6E2232"), (.55, "#AE3A48"), (.78, "#D65A62"), (.92, "#EC8C88"), (1, "#F8CCC6")]
SOCKET = "#0B0408"
TIDE, TIDE_HI, TIDE_DEEP = "#6F8FD0", "#A9BEEA", "#3F5A98"
ROLE = {"tank": ("#5878C2", "#2C417E"), "healer": ("#5C9A68", "#2B5A38"), "dps": ("#B25A64", "#5E2632")}
JOBS = {"paladin": ("Paladin", "tank", 62019), "bard": ("Bard", "dps", 62023), "white-mage": ("White Mage", "healer", 62024)}
DEFAULT_JOB = "paladin"

C = 64.0
R_FACE = 52.4                                     # the common circular well every face sits in
NSEG, SEG_ROT = 16, -90 + 11.25                   # the Silver kit's frame is a 16-sided cut; a flat sits at the top
_SEC = 1 / math.cos(math.radians(180 / NSEG))
# vertex radii. The inner edge's apothem is 50.8, so it covers the r 52.4 well by 1.6 at the flats (S1)
R_KEY, R_OUT, R_MID, R_IN = 63.4, 62.0, 56.9, 50.8 * _SEC
QUIET_IN, QUIET_OUT = 50.8 * _SEC, 53.6 * _SEC   # the quiet frame: a thin cut band, apothem 50.8 -> 53.6
BADGE = dict(cx=95.0, cy=95.0, r_key=24.0, r_out=23.1, r_mid=21.5, r_in=19.9, icon=35.5)   # same slot as medallion-r5
BADGE_N, BADGE_ROT = 12, -90 + 15
LIGHT = np.array([-0.42, -0.57, 0.71]); LIGHT = LIGHT / np.linalg.norm(LIGHT)
HALF = LIGHT + np.array([0, 0, 1.0]); HALF = HALF / np.linalg.norm(HALF)
SHADOW_DX, SHADOW_DY = 1.1, 1.6
ROW_TIER = False
TIER = {"ready": "act-now", "ready-on-another-job": "resting", "in-journal": "resting", "blocked": "resting",
        "done-this-cycle": "resting", "completed": "finished", "locked-out": "resting", "not-checked": "ghost"}
BADGE_KIND = {"ready": "open", "ready-on-another-job": "job", "in-journal": "journal", "blocked": "closed"}


# ---------------- helpers ----------------
def f(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def pt(r, a, cx=C, cy=C):
    a = math.radians(a)
    return cx + r * math.cos(a), cy + r * math.sin(a)


def poly(points):
    return "M" + "L".join(f"{f(x)} {f(y)}" for x, y in points) + "Z"


def hex2rgb(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], dtype=float)


def rgb2hex(c):
    c = np.clip(np.round(c), 0, 255).astype(int)
    return "#%02X%02X%02X" % tuple(c)


def ramp(stops, t):
    t = min(max(t, 0.0), 1.0)
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t <= t1:
            u = (t - t0) / (t1 - t0) if t1 > t0 else 0
            return rgb2hex(hex2rgb(c0) * (1 - u) + hex2rgb(c1) * u)
    return stops[-1][1]


def norm(v):
    v = np.asarray(v, dtype=float)
    return v / (np.linalg.norm(v) or 1)


def key_value(n, amb=.16, dif=.66, spec=.5, p=24):
    """Flat value of a facet with normal n under the UI key light: ambient + Lambert + Blinn specular."""
    n = norm(n)
    return amb + dif * max(0.0, float(n @ LIGHT)) + spec * max(0.0, float(n @ HALF)) ** p


def phase(cx, cy, r, k, lit="right", rot=0.0):
    rx = abs(k) * r
    sweep = 1 if k > 0 else 0
    if abs(k) < 1e-6:
        d = f"M{f(cx)} {f(cy - r)}A{f(r)} {f(r)} 0 0 1 {f(cx)} {f(cy + r)}Z"
    else:
        d = (f"M{f(cx)} {f(cy - r)}A{f(r)} {f(r)} 0 0 1 {f(cx)} {f(cy + r)}"
             f"A{f(rx)} {f(r)} 0 0 {sweep} {f(cx)} {f(cy - r)}Z")
    tr = []
    if lit == "left":
        tr.append(f"translate({f(2 * cx)} 0) scale(-1 1)")
    if rot:
        tr.insert(0, f"rotate({f(rot)} {f(cx)} {f(cy)})")
    return d, " ".join(tr)


def lit_mask_np(cx, cy, r, k, lit, rot, xs, ys):
    a = math.radians(-rot)
    dx, dy = xs - cx, ys - cy
    u = dx * math.cos(a) - dy * math.sin(a)
    v = dx * math.sin(a) + dy * math.cos(a)
    if lit == "left":
        u = -u
    inside = u * u + v * v <= r * r
    rx = abs(k) * r
    half = np.sqrt(np.clip(1 - (v / r) ** 2, 0, 1))
    term = -rx * half if k > 0 else rx * half
    return inside & (u >= term)


def centroid(cx, cy, r, k, side, rot, step=0.25, extent=128):
    ys, xs = np.mgrid[0:extent:step, 0:extent:step]
    m = lit_mask_np(cx, cy, r, k, side, rot, xs, ys)
    return float(xs[m].mean()), float(ys[m].mean())


def ngon(r, n=NSEG, rot=SEG_ROT, cx=C, cy=C):
    return [pt(r, rot + 360 / n * i, cx, cy) for i in range(n)]


def ring_path(r0, r1, n=NSEG, rot=SEG_ROT, cx=C, cy=C):
    return poly(ngon(r1, n, rot, cx, cy)) + poly(ngon(r0, n, rot, cx, cy)[::-1])


def circle_ring(r0, r1, cx=C, cy=C):
    return (f"M{f(cx + r1)} {f(cy)}A{f(r1)} {f(r1)} 0 1 1 {f(cx - r1)} {f(cy)}A{f(r1)} {f(r1)} 0 1 1 {f(cx + r1)} {f(cy)}Z"
            f"M{f(cx + r0)} {f(cy)}A{f(r0)} {f(r0)} 0 1 0 {f(cx - r0)} {f(cy)}A{f(r0)} {f(r0)} 0 1 0 {f(cx + r0)} {f(cy)}Z")


def svg(title, defs, body, vb="0 0 128 128", w=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{vb}" width="{w}" height="{w}"><title>{title}</title>'
            f'<defs>{"".join(defs)}</defs>{"".join(body)}</svg>\n')


def write(rel, text):
    path = OUT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def shadow_filter(p, sd=".9", op=".6", dx=SHADOW_DX, dy=SHADOW_DY, name="ds"):
    return (f'<filter id="{p}{name}" x="-25%" y="-25%" width="150%" height="150%" color-interpolation-filters="sRGB">'
            f'<feDropShadow dx="{f(dx)}" dy="{f(dy)}" stdDeviation="{sd}" flood-color="{KEY}" flood-opacity="{op}"/></filter>')


def blur_filter(p, name, sd, ext=128):
    return (f'<filter id="{p}{name}" filterUnits="userSpaceOnUse" x="-20" y="-20" width="{ext + 40}" height="{ext + 40}" '
            f'color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="{f(sd)}"/></filter>')


class Face:
    """One state's face: defs, the under layer (well content) and the over layer (overhangs above the frame)."""

    def __init__(self, state, D=None, under=None, over=None):
        self.state, self.D, self.under, self.over = state, D or [], under or [], over or []


def face_well(p, top=WELL_T, bot=WELL_B, span=None):
    """The common circular well (r 52.4). Returns (defs, base); the clip id is {p}wc."""
    gl = (f'x1="0" y1="{f(span[0])}" x2="0" y2="{f(span[1])}" gradientUnits="userSpaceOnUse"' if span else 'x1="0" y1="0" x2="0" y2="1"')
    d = [f'<linearGradient id="{p}wl" {gl}><stop offset="0" stop-color="{top}"/><stop offset="1" stop-color="{bot}"/></linearGradient>',
         f'<clipPath id="{p}wc"><circle cx="64" cy="64" r="{f(R_FACE)}"/></clipPath>']
    return d, [f'<circle cx="64" cy="64" r="{f(R_FACE)}" fill="url(#{p}wl)"/>']


# ======================= the Silver kit =======================

def _tier_ramp(tier):
    """Act now is the shared gilt; resting is moonstone silver; finished is the silver a step dimmer; ghost the
    dimmest and flattest."""
    if tier == "act-now":
        return GILT_KIT, (lambda v: v)
    if tier == "resting":
        return SILVER_KIT, (lambda v: v)
    if tier == "finished":
        return SILVER_KIT, (lambda v: .08 + .8 * v)
    return SILVER_KIT, (lambda v: .14 + .55 * v)


def crystal_ring(p, cx, cy, n, rot, r_key, r_out, r_mid, r_in, stops, tone=lambda v: v, hero=True, spec_w=1.6,
                 keyline=True):
    """A cut ring: n planar crown facets sloping outward and n planar pavilion facets sloping inward, each one flat
    value from its own normal under the key light (the crown bright at the upper left, the pavilion bright at the lower
    right: a convex cut rim). A dark keyline, a crest seam and a specular flash on the upper-left crown."""
    S = []
    if keyline:
        S.append(f'<path d="{ring_path(r_in - .9, r_key, n, rot, cx, cy)}" fill="{KEY}" fill-opacity=".94" fill-rule="evenodd"/>')
    zc, zi = (r_out - r_mid) * 1.2, (r_out - r_mid) * .45
    P3 = lambda r, a, z: np.array([cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a)), z])
    facets = []

    def tri(*q):
        nrm = np.cross(q[1] - q[0], q[2] - q[0])
        nrm = nrm if nrm[2] > 0 else -nrm
        c = ramp(stops, tone(key_value(nrm, amb=.12, dif=.7, spec=.4, p=36)))
        facets.append(f'<path d="{poly([(x[0], x[1]) for x in q])}" fill="{c}" stroke="{c}" stroke-width=".3"/>')

    for i in range(n):
        a0 = rot + 360 / n * i
        a1 = a0 + 360 / n
        O0, O1 = P3(r_out, a0, 0), P3(r_out, a1, 0)
        M0, M1 = P3(r_mid, a0, zc), P3(r_mid, a1, zc)
        I0, I1 = P3(r_in, a0, zi), P3(r_in, a1, zi)
        tri(O0, O1, M1); tri(O0, M1, M0)
        tri(M0, M1, I1); tri(M0, I1, I0)
    S.append("".join(facets))
    S.append(f'<path d="{ring_path(r_mid - .18, r_mid + .18, n, rot, cx, cy)}" fill="{KEY}" fill-opacity=".35" fill-rule="evenodd"/>')
    if hero:
        segs = []
        for i in range(n):
            a = rot + 360 / n * i
            ux, uy = math.cos(math.radians(a)), math.sin(math.radians(a))
            facing = -(ux * LIGHT[0] + uy * LIGHT[1]) / math.hypot(LIGHT[0], LIGHT[1])
            if facing > 0.2:
                x0, y0 = pt(r_mid, a, cx, cy); x1, y1 = pt(r_out, a, cx, cy)
                segs.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke-opacity="{f(.15 + .45 * facing)}"/>')
        S.append(f'<g stroke="{stops[-1][1]}" stroke-width=".4" stroke-linecap="round">{"".join(segs)}</g>')
    a_l = math.degrees(math.atan2(LIGHT[1], LIGHT[0]))
    flash = []
    for i in range(n):
        a0 = rot + 360 / n * i
        a1 = a0 + 360 / n
        d = abs((((a0 + a1) / 2 - a_l) + 180) % 360 - 180)
        if d < 40:
            w = 1 - d / 40
            q = [pt(r_out - .2, a0 + 1.5, cx, cy), pt(r_out - .2, a1 - 1.5, cx, cy),
                 pt(r_out - .2 - spec_w * w, a1 - 3, cx, cy), pt(r_out - .2 - spec_w * w, a0 + 3, cx, cy)]
            flash.append(f'<path d="{poly(q)}" fill-opacity="{f((.25 + .65 * w) * tone(1.0))}"/>')
    S.append(f'<g fill="{stops[-1][1]}">{"".join(flash)}</g>')
    return S


def kit_frame(p, tier, finish="full"):
    """The Silver kit's frame for one urgency tier. It carries the shadow its raised rim casts on the well's upper-left
    edge (a recess), so faces carry none."""
    stops, tone = _tier_ramp(tier)
    inner = R_IN if finish == "full" else QUIET_IN
    wpath = poly(ngon(inner))
    D = [f'<clipPath id="{p}fwc"><circle cx="64" cy="64" r="{f(R_FACE)}"/></clipPath>',
         f'<filter id="{p}fib" x="-10%" y="-10%" width="120%" height="120%" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="{"1.3" if finish == "full" else ".8"}"/></filter>',
         f'<mask id="{p}fim"><rect width="128" height="128" fill="#fff"/><path d="{wpath}" transform="translate({"2.4 3.3" if finish == "full" else "1.2 1.6"})" fill="#000"/></mask>']
    S = [f'<g clip-path="url(#{p}fwc)"><rect x="-4" y="-4" width="136" height="136" fill="{KEY}" fill-opacity="{".55" if finish == "full" else ".4"}" mask="url(#{p}fim)" filter="url(#{p}fib)"/></g>']
    if finish == "full":
        S += crystal_ring(p, C, C, NSEG, SEG_ROT, R_KEY, R_OUT, R_MID, R_IN, stops, tone)
    else:
        # quiet: a thin cut band (one crown and one pavilion facet per side, 2.8 units) inside a hairline keyline
        S += crystal_ring(p, C, C, NSEG, SEG_ROT, QUIET_OUT + .9, QUIET_OUT, (QUIET_IN + QUIET_OUT) / 2, QUIET_IN,
                          stops, tone, hero=False, spec_w=.8)
    return D, S


def badge_frame(p, kind, job=None):
    """The badge's keyline, cut silver ring and seat (role colour for jobs), with its drop shadow. Same slot as
    medallion-r5: centre (95, 95), keyline 24, ring 19.9-23.1, seat 19.9."""
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    hi, lo = ROLE[JOBS[job][1]] if kind == "job" else SEAT[kind]
    seat = poly(ngon(b["r_in"], BADGE_N, BADGE_ROT, cx, cy))
    D = [f'<radialGradient id="{p}en" cx="{f(cx - 5)}" cy="{f(cy - 6)}" r="{f(b["r_in"] * 1.5)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{hi}"/><stop offset="1" stop-color="{lo}"/></radialGradient>',
         f'<clipPath id="{p}ec"><path d="{seat}"/></clipPath>',
         f'<mask id="{p}em"><rect width="128" height="128" fill="#fff"/><path d="{seat}" transform="translate(1.4 1.9)" fill="#000"/></mask>',
         blur_filter(p, "eb", 0.8),
         shadow_filter(p, sd="1.1", op=".65", dx=1.4, dy=1.9, name="bs")]
    S = [f'<g filter="url(#{p}bs)"><path d="{poly(ngon(b["r_key"], BADGE_N, BADGE_ROT, cx, cy))}" fill="{KEY}"/></g>']
    S += crystal_ring(p + "b", cx, cy, BADGE_N, BADGE_ROT, b["r_key"], b["r_out"], b["r_mid"], b["r_in"], SILVER_KIT,
                      hero=False, spec_w=1.0, keyline=False)
    S += [f'<path d="{seat}" fill="url(#{p}en)"/>',
          f'<g clip-path="url(#{p}ec)"><rect x="60" y="60" width="70" height="70" fill="{KEY}" fill-opacity=".5" mask="url(#{p}em)" filter="url(#{p}eb)"/></g>']
    return D, S


SEAT = {"open": ("#4A72C4", "#1E3470"), "closed": ("#2E3A66", "#111832"), "journal": ("#5674B8", "#243C78")}


def job_png(job):
    return "data:image/png;base64," + base64.b64encode((JOBDIR / f"{job}.png").read_bytes()).decode()


def job_optical_offset(job):
    from PIL import Image
    a = np.asarray(Image.open(JOBDIR / f"{job}.png").convert("RGBA"), dtype=float)[..., 3] / 255
    n = a.shape[0]
    ys, xs = np.mgrid[0:n, 0:n]
    m = a > 0.5
    w = a * m
    bx, by = (xs[m].min() + xs[m].max()) / 2, (ys[m].min() + ys[m].max()) / 2
    mx, my = (xs * w).sum() / w.sum(), (ys * w).sum() / w.sum()
    c = (n - 1) / 2
    return ((bx + mx) / 2 - c) / n, ((by + my) / 2 - c) / n


def underglow(p):
    return (f'<filter id="{p}ug" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">'
            f'<feDropShadow dx=".6" dy=".9" stdDeviation=".8" flood-color="{KEY}" flood-opacity=".5"/></filter>')


def slab(rect, stops, bev=1.3, amb=.14, dif=.7, spec=.4):
    x0, y0, x1, y1 = rect
    b = bev
    face = key_value((0, 0, 1), amb, dif, spec)
    st, ct = math.sin(math.radians(45)), math.cos(math.radians(45))
    sides = {"t": ((0, -st, ct), [(x0, y0), (x1, y0), (x1 - b, y0 + b), (x0 + b, y0 + b)]),
             "l": ((-st, 0, ct), [(x0, y0), (x0 + b, y0 + b), (x0 + b, y1 - b), (x0, y1)]),
             "b": ((0, st, ct), [(x0, y1), (x0 + b, y1 - b), (x1 - b, y1 - b), (x1, y1)]),
             "r": ((st, 0, ct), [(x1, y0), (x1, y1), (x1 - b, y1 - b), (x1 - b, y0 + b)])}
    out = [f'<path d="{poly([(x0 + b, y0 + b), (x1 - b, y0 + b), (x1 - b, y1 - b), (x0 + b, y1 - b)])}" fill="{ramp(stops, face)}"/>']
    for n, q in sides.values():
        c = ramp(stops, key_value(n, amb, dif, spec))
        out.append(f'<path d="{poly(q)}" fill="{c}" stroke="{c}" stroke-width=".2"/>')
    return "".join(out)


def lock_glyph(p, cx, cy, open_):
    """A padlock cut from metal: a slab body with four flat bevels, a ridged shackle and a recessed keyhole. Open
    (Ready): act-now gilt, the shackle lifted so its free leg clears the body by 4.2. Closed (Blocked): the kit's silver.
    Both share one body position."""
    stops = GOLD if open_ else SILVER
    lift = 5.0 if open_ else 0.0
    pts, ws = [], []
    for i in range(4):
        pts.append((-4.6, .4 - (6.4 + lift) * i / 3)); ws.append(2.7)
    for i in range(1, 12):
        a = math.radians(180 + 180 * i / 12)
        pts.append((4.6 * math.cos(a), -6 - lift + 4.6 * math.sin(a))); ws.append(2.7)
    end = -5.0 if open_ else .4
    for i in range(4):
        pts.append((4.6, -6 - lift + (end + 6 + lift) * i / 3)); ws.append(2.7)
    outline, facets = ridge(pts, ws, stops, tilt=50)
    cap = f'<path d="M3.25 {f(end)}H5.95L5.6 {f(end + .9)}H3.6Z" fill="{ramp(stops, .55)}"/>' if open_ else ""
    body = slab((-7.5, 0, 7.5, 11), stops, bev=1.35)
    key_d = "M0 3.1a1.55 1.55 0 0 1 .95 2.8L1.4 8.3H-1.4L-.95 5.9A1.55 1.55 0 0 1 0 3.1Z"
    D = [underglow(p), f'<mask id="{p}km"><path d="{key_d}" fill="#fff"/><path d="{key_d}" transform="translate(-.45 -.45)" fill="#000"/></mask>']
    S = [f'<g transform="translate({f(cx)} {f(cy - .2)})" filter="url(#{p}ug)">{facets}{cap}{body}'
         f'<path d="{key_d}" fill="#0E0B08" fill-opacity=".9"/>'
         f'<rect x="-3" y="2" width="6" height="8" fill="{ramp(stops, 1)}" fill-opacity=".8" mask="url(#{p}km)"/></g>']
    return D, S


def book_glyph(p, cx, cy):
    """An open journal cut from the kit's silver: each page is four flat strips that turn from the light toward the
    spine, a darker board behind, incised text lines and a small Tide silk ribbon."""
    D = [underglow(p)]
    out = [f'<path d="M-10.8 -6.4V8.1C-7.1 7 -3.4 7.6 0 10C3.4 7.6 7.1 7 10.8 8.1V-6.4L10 -6.7V7C6.6 6 3.2 6.5 0 8.8C-3.2 6.5 -6.6 6 -10 7V-6.7Z" fill="{ramp(SILVER, .25)}"/>']
    for side in (-1, 1):
        xs = [10, 7.4, 4.8, 2.3, 0]
        tilts = [-28, -10, 8, 26]
        for i in range(4):
            xa, xb = xs[i] * side, xs[i + 1] * side
            yt = lambda x: -6.7 - 1.0 * math.sin(math.pi * abs(x) / 10) + 1.7 * (1 - abs(x) / 10) ** 3
            yb = lambda x: 7 - .9 * math.sin(math.pi * abs(x) / 10) + 1.8 * (1 - abs(x) / 10) ** 3
            tt = math.radians(tilts[i])
            c = ramp(SILVER, key_value((side * math.sin(tt), -.18, math.cos(tt)), .24, .62, .35))
            out.append(f'<path d="{poly([(xa, yt(xa)), (xb, yt(xb)), (xb, yb(xb)), (xa, yb(xa))])}" fill="{c}" stroke="{c}" stroke-width=".2"/>')
    lines = ("M-8.2 -3.9C-6 -4.6 -3.8 -4.3 -1.7 -3M-8.2 -1C-6 -1.7 -3.8 -1.4 -1.7 -.1M-8.2 1.9C-6 1.2 -3.8 1.5 -1.7 2.8"
             "M8.2 -3.9C6 -4.6 3.8 -4.3 1.7 -3M8.2 -1C6 -1.7 3.8 -1.4 1.7 -.1M8.2 1.9C6 1.2 3.8 1.5 1.7 2.8")
    out.append(f'<path d="{lines}" fill="none" stroke="#F4F7FD" stroke-opacity=".5" stroke-width=".3" transform="translate(.25 .3)" stroke-linecap="round"/>'
               f'<path d="{lines}" fill="none" stroke="#3A4566" stroke-opacity=".8" stroke-width=".5" stroke-linecap="round"/>'
               f'<path d="M0 -4.6V8.7" stroke="#2E3858" stroke-width=".6"/>'
               f'<path d="M-.8 8.5V12.6L0 11.7L.8 12.6V8.5Z" fill="{TIDE_HI}"/><path d="M.8 8.5V12.6L0 11.7Z" fill="{TIDE}"/>')
    return D, [f'<g transform="translate({f(cx)} {f(cy - 1.3)})" filter="url(#{p}ug)">{"".join(out)}</g>']


def badge(p, kind, job=None):
    """Badge frame plus its content: a lock, the book, or the game's job icon (optically centred)."""
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    D, S = badge_frame(p, kind, job)
    if kind == "job":
        name, role, icon_id = JOBS[job]
        ox, oy = job_optical_offset(job)
        sz = b["icon"]
        S.append(f'<image href="{job_png(job)}" x="{f(cx - sz / 2 - ox * sz)}" y="{f(cy - sz / 2 - oy * sz)}" width="{f(sz)}" height="{f(sz)}" '
                 f'preserveAspectRatio="xMidYMid meet" clip-path="url(#{p}ec)"><title>{name} (game icon {icon_id:06d})</title></image>')
    elif kind in ("open", "closed"):
        dd, ss = lock_glyph(p, cx, cy, kind == "open"); D += dd; S += ss
    else:
        dd, ss = book_glyph(p, cx, cy); D += dd; S += ss
    return D, S


# ======================= faces =======================

def _mesh(rot_deg=7.0, row=False):
    """A brilliant cut seen from above, in the unit disc. Hero: a flat octagonal table, 8 stars, 8 kites and 16 upper
    girdle facets (33). Row: a hexagonal table and 6 sectors (7 facets, 12 internal edges), so 16-20 px reads as a
    stone, not speckle (critic 10, rule R5). Each facet is (points, kind)."""
    A = math.radians(rot_deg)
    P = lambda r, a: (r * math.cos(math.radians(a) + A), r * math.sin(math.radians(a) + A))
    if row:
        T = [P(.5, 60 * i + 30) for i in range(6)]
        out = [(T, "table")]
        for i in range(6):
            arc = [P(1, 60 * i - 30 + 60 * t / 6) for t in range(7)]
            out.append(([T[i - 1]] + arc + [T[i]], "girdle"))
        return out
    rt, rs = .46, .74
    T = [P(rt, 45 * i + 22.5) for i in range(8)]
    St = [P(rs, 45 * i) for i in range(8)]
    out = [(T, "table")]
    for i in range(8):
        out.append(([T[i - 1], St[i], T[i]], "star"))
    for i in range(8):
        out.append(([T[i], St[i], P(1, 45 * i + 22.5), St[(i + 1) % 8]], "kite"))
    for i in range(8):
        for a0, a1 in ((45 * i - 22.5, 45 * i), (45 * i, 45 * i + 22.5)):
            out.append(([St[i]] + [P(1, a0 + (a1 - a0) * t / 4) for t in range(5)], "girdle"))
    return out


def _normal(face, dome=1.0):
    pts, kind = face
    if kind == "table":
        return np.array([0.0, 0.0, 1.0])
    pts3 = [np.array([x, y, dome * math.sqrt(max(0.0, 1 - x * x - y * y))]) for x, y in pts]
    if len(pts3) == 4:
        n = np.cross(pts3[2] - pts3[0], pts3[3] - pts3[1])
    else:
        a, b, c = pts3[0], pts3[len(pts3) // 2], pts3[-1]
        n = np.cross(b - a, c - a)
    if n[2] < 0:
        n = -n
    return norm(n)


def facet_moon(p, cx, cy, r, k, rot=0.0, lit="right", stones=MOONSTONE, earth=EARTH, lift=0.0,
               mesh_rot=7.0, adul=0.42, rim=0.0, edges=True, glint=True, shadow=True, dark_op=1.0, floor=.42,
               gloss=.22, maria=0.0):
    """A moon cut from moonstone. Lit side: Lambert per facet under the phase's own sun (k is the sun's z, so a
    crescent is lit from behind). Dark side: earthshine at half the facet contrast, with only the girdle catching the
    key light's gloss, so the table's star pattern stays below 64 px (critic 9). Adularescence and the transmitted
    rim as before."""
    row = ROW_TIER
    if row:
        edges, glint = False, False
    D, S = [], []
    th = math.radians(rot + (180 if lit == "left" else 0))
    sxy = math.sqrt(max(0.0, 1 - k * k))
    sun = np.array([sxy * math.cos(th), sxy * math.sin(th), k])
    dpath, tr = phase(cx, cy, r, k, lit, rot)
    t = f' transform="{tr}"' if tr else ""
    D.append(f'<clipPath id="{p}lc"><path d="{dpath}"{t}/></clipPath>')
    D.append(f'<clipPath id="{p}dc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}"/></clipPath>')
    faces = _mesh(mesh_rot, row)
    dark, light, eds = [], [], []
    for face in faces:
        n = _normal(face)
        d = poly([(cx + x * r, cy + y * r) for x, y in face[0]])
        e = 0.48 + 0.3 * float(n[2]) ** 2
        ec = hex2rgb(ramp(earth, e))
        if face[1] == "girdle":
            g = gloss * max(0.0, float(n @ HALF)) ** 6
            ec = ec * (1 - g) + hex2rgb("#A9B9E2") * g
        ec = rgb2hex(ec)
        dark.append(f'<path d="{d}" fill="{ec}" stroke="{ec}" stroke-width=".3"/>')
        v = max(0.0, float(n @ sun))
        col = ramp(stones, floor + (1 - floor) * v ** .8 + lift)
        light.append(f'<path d="{d}" fill="{col}" stroke="{col}" stroke-width=".3"/>')
        eds.append(d)
    fl = f' filter="url(#{p}ds)"' if shadow else ""
    S.append(f'<g{fl}><g clip-path="url(#{p}dc)" opacity="{f(dark_op)}">{"".join(dark)}</g></g>')
    if edges:
        S.append(f'<g clip-path="url(#{p}dc)" fill="none" stroke="#8EA4DA" stroke-opacity=".06" stroke-width=".4" stroke-linejoin="round">'
                 + "".join(f'<path d="{d}"/>' for d in eds) + '</g>')
    S.append(f'<g clip-path="url(#{p}lc)">{"".join(light)}')
    if maria > 0:
        u = r
        blobs = [(-.30, -.36, .30, .22, -18, .36), (.12, -.30, .17, .15, 0, .34), (.28, -.02, .22, .17, 20, .32),
                 (.70, -.18, .10, .08, 0, .36), (.56, .24, .10, .17, -15, .28), (.26, .36, .09, .10, 0, .24),
                 (-.22, .30, .17, .13, 10, .24), (-.58, -.02, .22, .36, 8, .26)]
        D.append(blur_filter(p, "mb", max(r * .07, .8)))
        els = "".join(f'<ellipse cx="{f(cx + x * u)}" cy="{f(cy + y * u)}" rx="{f(rx * u)}" ry="{f(ry * u)}" transform="rotate({a} {f(cx + x * u)} {f(cy + y * u)})" fill-opacity="{f(o * maria)}"/>'
                      for x, y, rx, ry, a, o in blobs)
        S.append(f'<g filter="url(#{p}mb)" fill="#5E6E97">{els}</g>')
    if edges:
        S.append(f'<g fill="none" stroke="#FFFFFF" stroke-opacity=".16" stroke-width=".4" stroke-linejoin="round">'
                 + "".join(f'<path d="{d}"/>' for d in eds) + '</g>')
    if adul > 0:
        lx, ly = cx + .55 * r * math.cos(th), cy + .55 * r * math.sin(th)
        D.append(blur_filter(p, "ab", r * .16))
        S.append(f'<g filter="url(#{p}ab)"><ellipse cx="{f(lx)}" cy="{f(ly - .12 * r)}" rx="{f(.34 * r)}" ry="{f(.5 * r)}" '
                 f'transform="rotate({f(math.degrees(th))} {f(lx)} {f(ly)})" fill="{AETHER_HI}" fill-opacity="{f(adul)}"/></g>')
    D.append(blur_filter(p, "lb", max(.35, r * .02)))
    S.append(f'<g filter="url(#{p}lb)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - .9)}" fill="none" stroke="#FFFFFF" stroke-opacity=".55" stroke-width="1.3"/></g>')
    S.append('</g>')
    if rim > 0:
        hx, hy = cx - r * math.cos(th), cy - r * math.sin(th)
        D.append(f'<radialGradient id="{p}rg" cx="{f(hx)}" cy="{f(hy)}" r="{f(2 * r)}" gradientUnits="userSpaceOnUse">'
                 f'<stop offset=".25" stop-color="{AETHER}" stop-opacity="0"/><stop offset="1" stop-color="{AETHER}" stop-opacity="{f(rim)}"/></radialGradient>')
        D.append(blur_filter(p, "rb", .45))
        S.append(f'<g clip-path="url(#{p}dc)"><g filter="url(#{p}rb)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - .7)}" fill="none" stroke="url(#{p}rg)" stroke-width="1.5"/></g></g>')
    if glint:
        best = max((fc for fc in faces if fc[1] == "girdle"), key=lambda fc: float(_normal(fc) @ HALF))
        S.append(f'<path d="{poly([(cx + x * r, cy + y * r) for x, y in best[0]])}" fill="#FFFFFF" fill-opacity=".16" clip-path="url(#{p}dc)"/>')
    return D, S


def ridge(points, widths, stops, tilt=48, amb=.14, dif=.7, spec=.45, stops_r=None):
    """A stroke cut as a ridge along its centreline: two flat slopes per segment, each one flat value from its normal
    under the key light. The left slope (left of the direction of travel) takes `stops`, the right one `stops_r`."""
    n = len(points)
    L, R, out = [], [], []
    st, ct = math.sin(math.radians(tilt)), math.cos(math.radians(tilt))
    for i, ((x, y), w) in enumerate(zip(points, widths)):
        x0, y0 = points[max(i - 1, 0)]
        x1, y1 = points[min(i + 1, n - 1)]
        dx, dy = x1 - x0, y1 - y0
        ln = math.hypot(dx, dy) or 1
        nx, ny = -dy / ln, dx / ln
        L.append((x + nx * w / 2, y + ny * w / 2))
        R.append((x - nx * w / 2, y - ny * w / 2))
    for i in range(n - 1):
        (x0, y0), (x1, y1) = points[i], points[i + 1]
        dx, dy = x1 - x0, y1 - y0
        ln = math.hypot(dx, dy) or 1
        nx, ny = -dy / ln, dx / ln
        cl = ramp(stops, key_value((nx * st, ny * st, ct), amb, dif, spec))
        cr = ramp(stops_r or stops, key_value((-nx * st, -ny * st, ct), amb, dif, spec))
        out.append(f'<path d="{poly([points[i], points[i + 1], L[i + 1], L[i]])}" fill="{cl}" stroke="{cl}" stroke-width=".25"/>')
        out.append(f'<path d="{poly([points[i], points[i + 1], R[i + 1], R[i]])}" fill="{cr}" stroke="{cr}" stroke-width=".25"/>')
    return poly(L + R[::-1]), "".join(out)


def shard(x0, x1, yc, h, j=0.0):
    """A sliver of light on water: one flat tone, straight edges drawn out to long fine tips."""
    L = x1 - x0
    return poly([(x0, yc + h * .1), (x0 + L * (.4 + j), yc - h * .5), (x1 - L * (.42 - j), yc - h * .5),
                 (x1, yc + h * .06), (x1 - L * (.46 + j), yc + h * .42), (x0 + L * (.44 - j), yc + h * .42)])


# ---------------- Ready and In journal ----------------
GLYPH_MOON = (49.0, 42.0, 29.0, -0.18, 28.0)
GLYPH_H = 80.0
GLYPH_LIT_C = centroid(*GLYPH_MOON[:4], "right", GLYPH_MOON[4])
GLYPH_ROWS = [
    (81.4, 1.1, [(-4.0, 4.5, .50, 1, 0)]),
    (83.8, 1.5, [(-7.0, 3.5, .56, 1, .04), (6.0, 11.5, .40, .75, 0)]),
    (87.0, 1.9, [(-9.5, -.5, .60, 1, -.03), (2.5, 10.0, .48, .8, .03)]),
    (91.0, 2.4, [(-10.5, 9.5, .66, 1, .02)]),
    (95.8, 2.9, [(-15.5, -.5, .70, 1, -.04), (3.0, 13.5, .56, .65, .04)]),
    (101.2, 3.4, [(-12.5, 13.5, .76, 1, .03)]),
    (107.2, 3.9, [(-24.5, -15.0, .50, .5, 0), (-10.5, 9.0, .80, 1, -.03), (12.5, 22.0, .60, .6, .04)]),
    (113.4, 4.4, [(-16.0, 15.0, .78, 1, .02)]),
]


def moon_scene(p, night=False):
    D, S = [], []
    H = GLYPH_H
    mx, my, mr, mk, rot = GLYPH_MOON
    cxr, cyr = GLYPH_LIT_C
    if night:
        sky_t, sky_h, sea_h, sea_b = "#1A2654", "#3A508A", "#2A3F7E", "#141F48"
    else:
        sky_t, sky_h, sea_h, sea_b = "#5A86D8", "#A2C4F6", "#6E98DC", "#2C4C92"
    dd, base = face_well(p, sky_t, sky_h, span=(12, H)); D += dd; S += base
    D.append(f'<linearGradient id="{p}sea" x1="0" y1="{f(H)}" x2="0" y2="117" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{sea_h}"/><stop offset="1" stop-color="{sea_b}"/></linearGradient>')
    D.append(f'<clipPath id="{p}skc"><rect x="0" y="0" width="128" height="{f(H)}"/></clipPath>')
    D.append(f'<clipPath id="{p}sec"><rect x="0" y="{f(H)}" width="128" height="{f(128 - H)}"/></clipPath>')
    S.append(f'<g clip-path="url(#{p}wc)">')
    S.append(f'<rect x="0" y="{f(H)}" width="128" height="{f(128 - H)}" fill="url(#{p}sea)"/>')
    D.append(f'<linearGradient id="{p}sr" x1="0" y1="{f(H)}" x2="0" y2="{f(H + 12)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{sky_h}" stop-opacity="{".45" if not night else ".28"}"/><stop offset="1" stop-color="{sky_h}" stop-opacity="0"/></linearGradient>')
    S.append(f'<rect x="0" y="{f(H)}" width="128" height="12" fill="url(#{p}sr)"/>')
    D.append(f'<radialGradient id="{p}bl" cx="{f(cxr)}" cy="{f(cyr)}" r="40" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#E2E8F4" stop-opacity="{".24" if not night else ".1"}"/>'
             f'<stop offset="1" stop-color="#E2E8F4" stop-opacity="0"/></radialGradient>')
    S.append(f'<g clip-path="url(#{p}skc)"><rect width="128" height="{f(H)}" fill="url(#{p}bl)"/></g>')
    if night:
        dd, ss = facet_moon(p, mx, my, mr, mk, rot, lift=-.16, adul=.26, rim=.32, shadow=False, dark_op=.55,
                            earth=[(0, "#1F2C5C"), (.5, "#2A3A70"), (1, "#3A4C86")])
    else:
        dd, ss = facet_moon(p, mx, my, mr, mk, rot, lift=.04, adul=.45, rim=.45, shadow=False, dark_op=.55, gloss=.1,
                            earth=[(0, "#3F64AE"), (.5, "#4A70BA"), (1, "#5579C2")])
    D += dd; S += ss
    D.append(f'<linearGradient id="{p}hz" x1="12" y1="0" x2="116" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#E2E8F4" stop-opacity=".06"/>'
             f'<stop offset="{f((cxr - 12) / 104)}" stop-color="#E2E8F4" stop-opacity="{".7" if not night else ".4"}"/><stop offset="1" stop-color="#E2E8F4" stop-opacity=".06"/></linearGradient>')
    S.append(f'<rect x="8" y="{f(H - 0.5)}" width="112" height="1" fill="url(#{p}hz)"/>')
    D.append(f'<radialGradient id="{p}col" cx="{f(cxr)}" cy="118" r="1" gradientUnits="userSpaceOnUse" '
             f'gradientTransform="translate({f(cxr)} 118) scale(22 40) translate({f(-cxr)} -118)">'
             f'<stop offset="0" stop-color="#E2E8F4" stop-opacity="{".24" if not night else ".1"}"/><stop offset="1" stop-color="#E2E8F4" stop-opacity="0"/></radialGradient>')
    S.append(f'<g clip-path="url(#{p}sec)"><rect y="{f(H)}" width="128" height="{f(128 - H)}" fill="url(#{p}col)"/></g>')
    col = "#E8EDF7" if not night else "#B9C5E0"
    k = 1.0 if not night else .85
    for y, rh, dashes in GLYPH_ROWS:
        for x0, x1, o, hs, j in dashes:
            S.append(f'<path d="{shard(cxr + x0, cxr + x1, y, rh * hs * .78, j)}" fill="{col}" fill-opacity="{f(o * k)}"/>')
    S.append('</g>')
    return D, S


def ready():
    p = "acr-"
    D, S = moon_scene(p)
    return Face("ready", D, S)


def in_journal():
    p = "acj-"
    D, S = moon_scene(p, night=True)
    # the bookmark (over layer): a Tide silk ribbon whose top folds behind the medal along an arc (r 66), lying over the
    # rim and dropping into the well, covering the crescent's lower horn. Its fold bands are circular (crest at
    # r 57-59.5, inner slope at r 52.4-55.5), so it lies right over any kit's rim.
    x0, w, y1 = 24.5, 17.5, 76.0
    ya = 64 - math.sqrt(66 ** 2 - (64 - x0) ** 2)
    yb = 64 - math.sqrt(66 ** 2 - (64 - x0 - w) ** 2)
    rib = f"M{f(x0)} {f(ya)}A66 66 0 0 1 {f(x0 + w)} {f(yb)}V{f(y1)}L{f(x0 + w / 2)} {f(y1 - 7)}L{f(x0)} {f(y1)}Z"
    D.append(f'<linearGradient id="{p}rbn" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="{TIDE_HI}"/><stop offset=".3" stop-color="{TIDE}"/>'
             f'<stop offset="1" stop-color="{TIDE_DEEP}"/></linearGradient>')
    D.append(f'<clipPath id="{p}rc"><path d="{rib}"/></clipPath>')
    D.append(f'<filter id="{p}rs" x="-30%" y="-10%" width="160%" height="120%" color-interpolation-filters="sRGB">'
             f'<feDropShadow dx="1.2" dy="1.0" stdDeviation="1.1" flood-color="{KEY}" flood-opacity=".6"/></filter>')
    over = [f'<g filter="url(#{p}rs)"><path d="{rib}" fill="url(#{p}rbn)" stroke="{KEY}" stroke-width="2.2" stroke-linejoin="round" paint-order="stroke"/></g>',
            f'<g clip-path="url(#{p}rc)">'
            f'<path d="{circle_ring(63.4, 72)}" fill="{TIDE_DEEP}" fill-rule="evenodd"/>'
            f'<path d="{circle_ring(57, 59.5)}" fill="{TIDE_HI}" fill-opacity=".5" fill-rule="evenodd"/>'
            f'<path d="{circle_ring(R_FACE, 55.5)}" fill="{KEY}" fill-opacity=".28" fill-rule="evenodd"/></g>']
    return Face("in-journal", D, S, over)


def ready_other_job():
    p = "aco-"
    D, S = [shadow_filter(p)], []
    dd, base = face_well(p); D += dd; S += base
    mx, my, mr, mk, rot = GLYPH_MOON
    dd, ss = facet_moon(p, mx, my, mr, mk, rot, lift=.24, adul=.4, rim=.4, earth=[(0, "#18214A"), (.5, "#202B58"), (1, "#2A376A")])
    D += dd; S += ss
    return Face("ready-on-another-job", D, S)


# ---------------- Blocked: a new moon behind a frost bank ----------------
BLOCKED_MOON = (62.0, 49.0, 30.0)
BLOCKED_LIMB = (-0.82, -25.0)        # about 9% lit (supervisor A1); the sun is to the right and a little up
# the frost bank: three low-poly billows (cx, base y, width, height), back to front, and the bank's underside
FROST_BILLOWS = [(50.0, 76.0, 42.0, 26.0), (27.0, 78.0, 28.0, 15.0), (80.0, 76.0, 36.0, 22.0)]
FROST_BASE = (17.0, 96.0, 78.0, 89.0)


def frost_bank(p):
    """A low-poly frost bank (cloud cut in the set's own crystal): each billow is a faceted dome cut into two flat
    facets, a broad upper face turned toward the light and a lower-right face turned from it, over a flat underside
    with a scalloped lower edge; 7 facets. Each takes one flat value from its normal under the key light (#B8C4DE upper
    left to #4E5C82 lower right). The top edge is three chamfered domes, so it reads as cloud, not a ledge or a ridge.
    It casts the soft emblem shadow onto the disc."""
    facets = []
    x0, x1, yt, yb = FROST_BASE
    under = [(x0, yt)] + [(x0 + (x1 - x0) * t / 6, yt + (yb - yt) * (.55 + .45 * abs(math.sin(math.pi * t / 2)))) for t in range(1, 6)] + [(x1, yt)]
    under = [(x0 + 2, yt), (x1 - 2, yt), (x1 - 4, yt + 3), (x1 - 13, yb - .5), (x1 - 24, yb - 2.5), (x1 - 37, yb),
             (x1 - 52, yb - 2), (x1 - 64, yb - .5), (x0 + 5, yt + 3.5)]
    c = ramp(FROST, key_value((0, .62, .78), .12, .72, .2))
    facets.append(f'<path d="{poly(under)}" fill="{c}" stroke="{c}" stroke-width=".3"/>')
    for i, (bx, by, bw, bh) in enumerate(FROST_BILLOWS):
        angs = [180, 158, 135, 112, 90, 68, 45, 22, 0]
        top = [(bx + bw / 2 * math.cos(math.radians(a)), by - bh * math.sin(math.radians(a))) for a in angs]
        split = (bx - bw * .12, by)                # the cut runs from the upper-right shoulder down to the base
        upper = top[:7] + [split, (bx - bw / 2, by)]
        lower = top[6:] + [split]
        vu = key_value((-.36, -.5, .79), .12, .72, .2)
        vl = key_value((.5, -.1, .86), .12, .72, .2)
        cu, cl = ramp(FROST, vu), ramp(FROST, vl)
        facets.append(f'<path d="{poly(upper)}" fill="{cu}" stroke="{cu}" stroke-width=".3"/>'
                      f'<path d="{poly(lower)}" fill="{cl}" stroke="{cl}" stroke-width=".3"/>'
                      f'<path d="M{f(top[6][0])} {f(top[6][1])}L{f(split[0])} {f(split[1])}" stroke="#9AA6C8" stroke-opacity=".25" stroke-width=".4"/>')
    return facets, []


def blocked():
    """Blocked: an ashen new moon (dark crystal turned from its sun, only a 9% sunlit limb) behind a low-poly frost
    bank that hides the disc's lower part and the lower limb. Closed-lock badge from the kit."""
    p = "acb-"
    D, S = [shadow_filter(p)], []
    dd, base = face_well(p); D += dd; S += base
    mx, my, mr = BLOCKED_MOON
    dd, ss = facet_moon(p, mx, my, mr, *BLOCKED_LIMB, adul=0, rim=.4, lift=.3, gloss=.2,
                        earth=[(0, "#141C42"), (.5, "#19234F"), (1, "#212D60")])
    D += dd; S += ss
    facets, outline = frost_bank(p)
    D.append(f'<filter id="{p}fs" x="-10%" y="-20%" width="130%" height="150%" color-interpolation-filters="sRGB">'
             f'<feDropShadow dx="{f(SHADOW_DX)}" dy="{f(SHADOW_DY)}" stdDeviation=".9" flood-color="{KEY}" flood-opacity=".6"/></filter>')
    S.append(f'<g clip-path="url(#{p}wc)"><g filter="url(#{p}fs)">{"".join(facets)}</g></g>')
    return Face("blocked", D, S)


# ---------------- Done this cycle ----------------

def done():
    """A waning half moon (lit left) inside a cut repeat arrow: the outer slope in moonstone, only the inner slope in
    aether (critic 8), about 15% lower in value than before; the tail tapers to a fine point and the head spans 13."""
    p = "acd-"
    D, S = [shadow_filter(p)], []
    dd, base = face_well(p); D += dd; S += base
    mx, my, mr = 64.0, 64.0, 25.0
    dd, ss = facet_moon(p, mx, my, mr, 0.0, 0.0, lit="left", adul=.3, mesh_rot=-11, maria=.5, lift=.22)
    D += dd; S += ss
    r0, r1, a0, a1, w = 33.0, 39.0, 132.0, 384.0, 8.0
    n = 12 if ROW_TIER else 26
    pts, ws = [], []
    for i in range(n + 1):
        t = i / n
        pts.append(pt(r0 + (r1 - r0) * t, a0 + (a1 - a0) * t))
        ws.append(w * (0.04 + 0.96 * min(t / 0.45, 1) ** 0.8))
    inner = [(0, "#1E2C58"), (.3, AETHER_DEEP), (.62, AETHER), (.9, AETHER_HI), (1, "#EAF3FF")]
    outer = [(0, "#262E4C"), (.3, "#56618A"), (.62, "#929EC0"), (.9, "#CED6E8"), (1, "#EEF2F8")]
    amb, dif, spec = .27, .56, .32
    outline, facets = ridge(pts, ws, inner, tilt=46, amb=amb, dif=dif, spec=spec, stops_r=outer)
    ah = math.radians(a1)
    tx, ty = -math.sin(ah), math.cos(ah)
    hx, hy = pts[-1]
    ox, oy = math.cos(ah), math.sin(ah)
    hw, hl = 6.5, 11.5
    tip = (hx + tx * hl, hy + ty * hl)
    po, pi_ = (hx + ox * hw - tx * 1.2, hy + oy * hw - ty * 1.2), (hx - ox * hw - tx * 1.2, hy - oy * hw - ty * 1.2)
    co = ramp(outer, key_value((ox * .66, oy * .66, .75), amb, dif, spec))
    ci = ramp(inner, key_value((-ox * .66, -oy * .66, .75), amb, dif, spec))
    head = (f'<path d="{poly([po, tip, (hx, hy)])}" fill="{co}" stroke="{co}" stroke-width=".25"/>'
            f'<path d="{poly([pi_, tip, (hx, hy)])}" fill="{ci}" stroke="{ci}" stroke-width=".25"/>')
    S.append(f'<g filter="url(#{p}ds)"><path d="{outline + poly([po, tip, pi_])}" fill="{KEY}" stroke="{KEY}" stroke-width="2" stroke-linejoin="round"/></g>')
    S.append(f'<g>{facets}{head}</g>')
    return Face("done-this-cycle", D, S)


# ---------------- Completed ----------------

def completed():
    """A full moonstone with its maria, and the gold check (over layer) cut as a two-facet ridge: the upper-left face
    #F0DDA8, the lower-right face #7C6236, a crisp crest (supervisor optional 1). It crosses the lower right and out
    past the frame."""
    p = "acc-"
    D, S = [shadow_filter(p)], []
    dd, base = face_well(p); D += dd; S += base
    dd, ss = facet_moon(p, 60.0, 60.0, 35.0, 0.92, -130.0, adul=.3, lift=-.12, mesh_rot=12, maria=1.0)
    D += dd; S += ss
    chk = [(64.0, 86.0), (78.0, 100.0), (117.5, 40.0)]
    over = [f'<g filter="url(#{p}ds)"><path d="M64 86L78 100L117.5 40" fill="none" stroke="{KEY}" stroke-width="13.4" stroke-linecap="round" stroke-linejoin="round"/></g>']
    # each arm: two flat faces split on its centreline; the face whose outward normal turns to the light is lit
    lit, shade = "#F0DDA8", "#7C6236"
    hw = 5.0
    for (xa, ya), (xb, yb) in zip(chk, chk[1:]):
        dx, dy = xb - xa, yb - ya
        ln = math.hypot(dx, dy)
        nx, ny = -dy / ln, dx / ln
        ex, ey = dx / ln * hw * .0, dy / ln * hw * .0
        L = [(xa + nx * hw, ya + ny * hw), (xb + nx * hw, yb + ny * hw)]
        R = [(xa - nx * hw, ya - ny * hw), (xb - nx * hw, yb - ny * hw)]
        lface = (nx * LIGHT[0] + ny * LIGHT[1]) > 0
        over.append(f'<path d="{poly([(xa, ya), (xb, yb), L[1], L[0]])}" fill="{lit if lface else shade}"/>'
                    f'<path d="{poly([(xa, ya), (xb, yb), R[1], R[0]])}" fill="{shade if lface else lit}"/>')
    # rounded, faceted ends and the elbow: small flat caps in the lit and shaded tones
    for (x, y) in chk:
        over.append(f'<path d="M{f(x - hw)} {f(y)}A{f(hw)} {f(hw)} 0 0 1 {f(x + hw * .7)} {f(y - hw * .7)}L{f(x)} {f(y)}Z" fill="{lit}"/>'
                    f'<path d="M{f(x + hw * .7)} {f(y - hw * .7)}A{f(hw)} {f(hw)} 0 0 1 {f(x - hw)} {f(y)}L{f(x)} {f(y)}Z" fill="{shade}"/>')
    # redraw the arms over the caps so the elbow's faces stay continuous, then the crisp crest
    for (xa, ya), (xb, yb) in zip(chk, chk[1:]):
        dx, dy = xb - xa, yb - ya
        ln = math.hypot(dx, dy)
        nx, ny = -dy / ln, dx / ln
        lface = (nx * LIGHT[0] + ny * LIGHT[1]) > 0
        L = [(xa + nx * hw, ya + ny * hw), (xb + nx * hw, yb + ny * hw)]
        R = [(xa - nx * hw, ya - ny * hw), (xb - nx * hw, yb - ny * hw)]
        over.append(f'<path d="{poly([(xa, ya), (xb, yb), L[1], L[0]])}" fill="{lit if lface else shade}"/>'
                    f'<path d="{poly([(xa, ya), (xb, yb), R[1], R[0]])}" fill="{shade if lface else lit}"/>')
    over.append(f'<path d="M64 86L78 100L117.5 40" fill="none" stroke="#FFF6DC" stroke-opacity=".75" stroke-width=".6" stroke-linejoin="round"/>')
    return Face("completed", D, S, over)


# ---------------- Locked out ----------------

def locked_out():
    """Dalamud as a red crystal sphere split along seven kinked straight cleavage lines from an off-centre impact; each
    shard pushed out and turned, one falling away. Lit by the key light (a full disc: the medal convention)."""
    p = "acl-"
    D, S = [shadow_filter(p, sd=".7", op=".7")], []
    dd, base = face_well(p); D += dd; S += base
    cx, cy, R = 64.0, 64.0, 37.5
    ix, iy = 59.0, 56.0
    angs = [-118.0, -62.0, -12.0, 34.0, 86.0, 142.0, 196.0]
    disp = [2.2, 3.0, 2.4, 2.8, 6.5, 2.6, 2.2]
    turn = [-1.5, 1.5, -1.0, 2.0, 9.0, -2.0, 1.5]
    n = len(angs)
    faces = _mesh(9.0, ROW_TIER)
    facet_svg = []
    for face in faces:
        v = key_value(_normal(face), amb=.57, dif=.42, spec=.45, p=18)
        c = ramp(DALAMUD, v)
        facet_svg.append(f'<path d="{poly([(cx + x * R, cy + y * R) for x, y in face[0]])}" fill="{c}" stroke="{c}" stroke-width=".3"/>')
    edges = "" if ROW_TIER else ('<g fill="none" stroke="#F8CCC6" stroke-opacity=".16" stroke-width=".4" stroke-linejoin="round">'
                                 + "".join(f'<path d="{poly([(cx + x * R, cy + y * R) for x, y in face[0]])}"/>' for face in faces) + '</g>')
    D.append(f'<g id="{p}sp">{"".join(facet_svg)}{edges}</g>')
    D.append(f'<clipPath id="{p}dc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}"/></clipPath>')

    def hit(a):
        ux, uy = math.cos(math.radians(a)), math.sin(math.radians(a))
        dx, dy = ix - cx, iy - cy
        b = dx * ux + dy * uy
        c = dx * dx + dy * dy - (R + 3) ** 2
        t = -b + math.sqrt(b * b - c)
        return ix + ux * t, iy + uy * t

    ends = [hit(a) for a in angs]
    jog = [2.2, -2.6, 2.4, -2.0, 2.8, -2.4, 2.0]
    kinks = []
    for a, e, j in zip(angs, ends, jog):
        ux, uy = math.cos(math.radians(a)), math.sin(math.radians(a))
        L = math.hypot(e[0] - ix, e[1] - iy)
        kinks.append((ix + ux * L * .48 - j * uy, iy + uy * L * .48 + j * ux))
    S.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R + .4)}" fill="{SOCKET}"/>')
    lip = [pt(R - .5 + .9 * math.sin(math.pi * i / 40), 15 + 125 * i / 40, cx, cy) for i in range(41)]
    lip += [pt(R - .5 - .9 * math.sin(math.pi * i / 40), 15 + 125 * i / 40, cx, cy) for i in range(40, -1, -1)]
    S.append(f'<path d="{poly(lip)}" fill="#EE97A0" fill-opacity=".4"/>')
    body = []
    for i in range(n):
        e0, e1 = ends[i], ends[(i + 1) % n]
        b0 = math.degrees(math.atan2(e0[1] - cy, e0[0] - cx))
        b1 = math.degrees(math.atan2(e1[1] - cy, e1[0] - cx))
        while b1 < b0:
            b1 += 360
        arc = [pt(R + 3, b0 + (b1 - b0) * k / 16, cx, cy) for k in range(17)]
        pts = [(ix, iy), kinks[i]] + arc + [kinks[(i + 1) % n]]
        a0, a1 = angs[i], angs[(i + 1) % n]
        if a1 < a0:
            a1 += 360
        mid = math.radians((a0 + a1) / 2)
        tx, ty = disp[i] * math.cos(mid), disp[i] * math.sin(mid)
        gx = sum(x for x, _ in pts) / len(pts); gy = sum(y for _, y in pts) / len(pts)
        sid = f"{p}s{i}"
        D.append(f'<clipPath id="{sid}"><path d="{poly(pts)}"/></clipPath>')
        g = [f'<g clip-path="url(#{sid})"><g clip-path="url(#{p}dc)"><use href="#{p}sp"/>']
        area = sum(pts[k][0] * pts[(k + 1) % len(pts)][1] - pts[(k + 1) % len(pts)][0] * pts[k][1] for k in range(len(pts)))
        sgn = 1 if area > 0 else -1
        for (x0, y0), (x1, y1) in (((ix, iy), kinks[i]), (kinks[i], e0), (e1, kinks[(i + 1) % n]), (kinks[(i + 1) % n], (ix, iy))):
            dx, dy = x1 - x0, y1 - y0
            ln = math.hypot(dx, dy) or 1
            nx, ny = sgn * dy / ln, -sgn * dx / ln
            facing = (nx * LIGHT[0] + ny * LIGHT[1]) / math.hypot(LIGHT[0], LIGHT[1])
            if facing > 0.15:
                g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="#FAD0D4" stroke-width="{f(1 + 1 * facing)}" stroke-opacity="{f(.35 + .45 * facing)}"/>')
            elif facing < -0.15:
                g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{KEY}" stroke-width="{f(1 - .9 * facing)}" stroke-opacity=".6"/>')
        g.append('</g></g>')
        body.append(f'<g transform="translate({f(tx)} {f(ty)}) rotate({f(turn[i])} {f(gx)} {f(gy)})">{"".join(g)}</g>')
    S.append(f'<g clip-path="url(#{p}wc)"><g filter="url(#{p}ds)">{"".join(body)}</g></g>')
    return Face("locked-out", D, S)


# ---------------- Not checked ----------------

def not_checked():
    p = "acn-"
    D, S = [shadow_filter(p)], []
    dd, base = face_well(p); D += dd; S += base
    vx, vy, vr = 64.0, 53.0, 21.0
    D.append(blur_filter(p, "vb", 1.1))
    dd, ss = facet_moon(p, vx, vy, vr + 4.5, -0.999, 0.0, adul=0, rim=0, glint=False, shadow=False,
                        earth=[(0, "#26336A"), (.5, "#33447E"), (1, "#41548E")])
    D += dd
    S.append(f'<g filter="url(#{p}vb)" opacity=".45">{"".join(ss)}</g>')
    pts, ws = [], []
    a0, a1, n = 196.0, 398.0, (10 if ROW_TIER else 22)
    for i in range(n + 1):
        t = i / n
        pts.append(pt(vr, a0 + (a1 - a0) * t, vx, vy))
        ws.append(14.5 * math.sin(math.pi / 2 * min(t / 0.55, 1)) ** 0.85 + .8 if t < 0.55 else 14.5 - 4.0 * (t - .55) / .45)
    ex, ey = pts[-1]
    tx, ty = -math.sin(math.radians(a1)), math.cos(math.radians(a1))
    P0, P1, P2, P3 = (ex, ey), (ex + tx * 7, ey + ty * 7), (vx, vy + vr + 3), (vx, vy + vr + 10)
    m = 4 if ROW_TIER else 8
    for i in range(1, m + 1):
        t = i / m
        x = (1 - t) ** 3 * P0[0] + 3 * (1 - t) ** 2 * t * P1[0] + 3 * (1 - t) * t * t * P2[0] + t ** 3 * P3[0]
        y = (1 - t) ** 3 * P0[1] + 3 * (1 - t) ** 2 * t * P1[1] + 3 * (1 - t) * t * t * P2[1] + t ** 3 * P3[1]
        pts.append((x, y)); ws.append(10.5 - 0.4 * t)
    pts.append((vx, P3[1] + 4)); ws.append(10.1)
    outline, facets = ridge(pts, ws, MOONSTONE, tilt=44, amb=.62, dif=.4, spec=.35)
    end = pts[-1]
    S.append(f'<g filter="url(#{p}ds)"><path d="{outline}" fill="{KEY}" stroke="{KEY}" stroke-width="1.2" stroke-linejoin="round"/></g>')
    S.append(facets)
    S.append(f'<path d="M{f(end[0] - 5.05)} {f(end[1])}H{f(end[0] + 5.05)}L{f(end[0] + 3.9)} {f(end[1] + 1.6)}H{f(end[0] - 3.9)}Z" fill="{ramp(MOONSTONE, .4)}"/>')
    dot = (vx, end[1] + 13.6, 7.0)
    dd, ss = facet_moon(p + "o", *dot, 0.9, -130.0, adul=0, rim=0, glint=True, lift=0, edges=False, mesh_rot=20)
    D += dd + [shadow_filter(p + "o")]
    S += ss
    return Face("not-checked", D, S)


FACES = [("ready", ready), ("ready-on-another-job", ready_other_job), ("in-journal", in_journal), ("blocked", blocked),
         ("done-this-cycle", done), ("completed", completed), ("locked-out", locked_out), ("not-checked", not_checked)]


# ======================= composites and output =======================

def compose(face, frame, badge_parts=None, title=None):
    """face under + kit frame + face over + badge. `frame` and `badge_parts` are (defs, body)."""
    D = face.D + frame[0] + (badge_parts[0] if badge_parts else [])
    S = face.under + frame[1] + face.over + (badge_parts[1] if badge_parts else [])
    return svg(title or face.state, D, S)


def silver_frame(state, finish="full"):
    return kit_frame(f"kf-{state[:4]}-", TIER[state], finish)


def silver_badge(state, job=DEFAULT_JOB):
    kind = BADGE_KIND.get(state)
    if not kind:
        return None
    return badge(f"kb-{state[:4]}-", kind, job if kind == "job" else None)


def write_all():
    global ROW_TIER
    # hero faces and composites
    ROW_TIER = False
    for state, fn in FACES:
        fc = fn()
        write(f"faces/{state}-under.svg", svg(f"{state} face (under)", fc.D, fc.under))
        write(f"faces/{state}-over.svg", svg(f"{state} face (over)", fc.D, fc.over))
        write(f"{state}.svg", compose(fc, silver_frame(state), silver_badge(state), state))
        if state == "ready-on-another-job":
            for job in JOBS:
                write(f"ready-on-another-job-{job}.svg", compose(fc, silver_frame(state), silver_badge(state, job), f"{state}: {job}"))
    # row faces and row composites (no badge)
    ROW_TIER = True
    for state, fn in FACES:
        fc = fn()
        write(f"faces/row/{state}-under.svg", svg(f"{state} row face (under)", fc.D, fc.under))
        write(f"faces/row/{state}-over.svg", svg(f"{state} row face (over)", fc.D, fc.over))
        write(f"_row/{state}.svg", compose(fc, silver_frame(state), None, f"{state} (row)"))
    ROW_TIER = False
    # the Silver kit
    for tier in ("act-now", "resting", "finished", "ghost"):
        for finish in ("full", "quiet"):
            D, S = kit_frame(f"k{tier[:3]}{finish[0]}-", tier, finish)
            write(f"kit/frame-{tier}-{finish}.svg", svg(f"Silver kit: {tier} frame ({finish})", D, S))
    for kind, job in (("open", None), ("closed", None), ("journal", None), ("job", "paladin"), ("job", "bard"), ("job", "white-mage")):
        name = kind if kind != "job" else JOBS[job][1]
        D, S = badge_frame(f"kbs{name[:3]}-", kind, job)
        write(f"kit/badge-seat-{name}.svg", svg(f"Silver kit: badge frame and {name} seat", D, S))
    for kind in ("open", "closed", "journal"):
        dd, ss = (lock_glyph(f"kg{kind[0]}-", 0, 0, kind == "open") if kind != "journal" else book_glyph("kgj-", 0, 0))
        g = svg(f"Silver kit: {kind} badge glyph", dd, ss, vb="-17 -18 34 34", w=34)
        write(f"kit/badge-{kind}.svg", g)
        write(f"_row/badge-{kind}.svg", g)


if __name__ == "__main__":
    write_all()
    print("lit centroid", GLYPH_LIT_C, "frame inner apothem", R_IN * math.cos(math.radians(180 / NSEG)))
