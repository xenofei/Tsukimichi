"""Ishgard Glass (v7 theme). Writes the 8 state SVGs, the job-badge variants, the badge-less row tier (_row/) and the
badge glyphs into docs/design/v7/themes/ishgard-glass.

Every state is a small leaded-glass roundel from the Holy See: jewel glass lit from behind, held in lead came, set in a
cusped stone tracery ring. The badge system and its slot are Menphina's Medallion round 5's, so a player can mix states
from both themes.

Light, two sources, never mixed up:
- The glass is lit from BEHIND (transmitted light). Its brightness is the glass colour itself: pale glass glows, deep
  glass is dark. Grisaille (vitreous paint, a brown-black matt) is painted on to tone the glass, always soft.
- The lead came and the stone frame are lit from the FRONT by the UI key light (upper left, azimuth 135 deg): raised
  lead is lit on its upper-left side and shaded on its lower-right side; the badge casts a shadow down and to the right.
Two tiers: hero (32 px and up) with fine secondary came, grisaille, halation and cusps; row (under 32 px) with only the
primary came, drawn about twice as bold so the glass structure survives at 16-20 px.
"""
import base64, math, pathlib
import numpy as np

OUT = pathlib.Path(__file__).resolve().parent.parent
JOBDIR = OUT.parents[3] / "design" / "moon-v6" / "round5" / "medallion-r5" / "_src" / "jobs"
ROW = False
MID = False    # the mid tier (32-64 px): the hero art minus details that fall to a pixel there (Completed only)

# ---------------- palette tokens ----------------
KEY = "#080B16"                                     # keyline, shadow
LEAD, LEAD_HI, LEAD_LO = "#2A2E3B", "#A2A9BC", "#0C0E15"     # lead came (dull grey metal)
STONE_HI, STONE, STONE_MID, STONE_LO, STONE_DK = "#C3C9D6", "#8C93A8", "#626A84", "#3E4560", "#232839"  # Ishgard stone
GLINT = "#EEF1F8"
GRIS = "#2A2430"                                    # grisaille (vitreous paint): a warm brown-black
MOON_HI, MOON, MOON_MID, MOON_LOW, MOON_DEEP = "#F7F4EA", "#E4E9F4", "#C6D0E6", "#98A8CB", "#62729B"
DAY_SKY_T, DAY_SKY_H, DAY_SEA_H, DAY_SEA_B = "#5687DA", "#97BBF3", "#6292D8", "#375DA8"       # Ready: lit azure glass
NIGHT_SKY_T, NIGHT_SKY_H, NIGHT_SEA_H, NIGHT_SEA_B = "#22346E", "#3E5A9E", "#33509A", "#1B2B66"  # In journal
DUSK_T, DUSK_B = "#1C2A5A", "#121B40"               # resting night glass
ASH_HI, ASH_LO = "#46528A", "#252E55"               # earthlit (ashen) moon glass
AMBER_HI, AMBER, AMBER_MID, AMBER_LO = "#FBE3A0", "#E9BE68", "#C4913F", "#86591E"   # silver-stain amber
RUBY_HI, RUBY, RUBY_LO, RUBY_EDGE = "#EA7A89", "#CC4E62", "#7A2034", "#F7B6BE"     # Dalamud glass
VOID = "#06070C"                                    # night behind an empty light
CLOUD_HI, CLOUD, CLOUD_LOW, CLOUD_DEEP = "#BEC5DD", "#9199BC", "#646D93", "#2C3355"  # smoky streaky glass
TIDE_HI, TIDE, TIDE_DEEP = "#B4C5EE", "#7E9BDC", "#45609E"                         # ribbon silk
CLOUD_BODY, CLOUD_LIGHT = "#5A6690", "#8A96BE"     # Blocked: flat smoky glass, and its thinner edge toward the moon
MARE, HIGHLAND = "#9AA6C6", "#C8D2E8"             # Completed: flat mare and highland glass
ROLE = {"tank": ("#5878C2", "#2C417E"), "healer": ("#5C9A68", "#2B5A38"), "dps": ("#B25A64", "#5E2632")}
JOBS = {"paladin": ("Paladin", "tank", 62019), "bard": ("Bard", "dps", 62023), "white-mage": ("White Mage", "healer", 62024)}
DEFAULT_JOB = "paladin"

C = 64.0
R_KEY, R_OUT, R_MID, R_IN = 63.2, 61.6, 57.0, 50.9      # silhouette, frame outer edge, frame crest, frame inner edge
R_WELL = 52.4             # every face's well (Medallion's): the kit frame's inner edge covers it by 1.5 units
GILT_SPEC, GILT_HI, GILT_LIGHT, GILT, GILT_MID, GILT_DEEP = "#FFF4D6", "#E6CF98", "#D9BE82", "#9A7E4A", "#7C6236", "#5C4724"
# the Came kit's four urgency tiers: act now is the shared medal gilt (every kit); the rest are Ishgard stone, each a
# step quieter (hi, base, mid, lo, dark, glint opacity)
TIERS = {
    "act-now":  (GILT_HI, GILT, GILT_MID, GILT_DEEP, "#33260F", .9),
    "resting":  ("#C3C9D6", "#8C93A8", "#626A84", "#3E4560", "#232839", .55),
    "finished": ("#9EA4B4", "#70778C", "#50576F", "#343A51", "#1E2232", .3),
    "ghost":    ("#848BA0", "#60677E", "#474E66", "#2E344B", "#1A1E2C", .0),
}
STATE_TIER = {"ready": "act-now", "ready-on-another-job": "resting", "in-journal": "resting", "blocked": "resting",
              "done-this-cycle": "resting", "completed": "finished", "locked-out": "resting", "not-checked": "ghost"}
LIGHT_V = (-0.6, -0.8)                                    # unit vector toward the key light


def W1():   # primary came (outlines of the subject, the horizon)
    return 3.4 if ROW else 2.5


def W2():   # secondary came (divisions inside a subject, the rose spokes, the waves); hero only
    return None if ROW else 1.45


# ---------------- geometry helpers ----------------

def f(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def pt(r, a, cx=C, cy=C):
    a = math.radians(a)
    return cx + r * math.cos(a), cy + r * math.sin(a)


def poly(points, close=True):
    return "M" + "L".join(f"{f(x)} {f(y)}" for x, y in points) + ("Z" if close else "")


def ring(r0, r1, cx=C, cy=C):
    return (f"M{f(cx + r1)} {f(cy)}A{f(r1)} {f(r1)} 0 1 1 {f(cx - r1)} {f(cy)}A{f(r1)} {f(r1)} 0 1 1 {f(cx + r1)} {f(cy)}Z"
            f"M{f(cx + r0)} {f(cy)}A{f(r0)} {f(r0)} 0 1 0 {f(cx - r0)} {f(cy)}A{f(r0)} {f(r0)} 0 1 0 {f(cx + r0)} {f(cy)}Z")


def circle_d(cx, cy, r):
    return f"M{f(cx + r)} {f(cy)}A{f(r)} {f(r)} 0 1 1 {f(cx - r)} {f(cy)}A{f(r)} {f(r)} 0 1 1 {f(cx + r)} {f(cy)}Z"


def sector(cx, cy, r0, r1, a0, a1):
    large = 1 if (a1 - a0) % 360 > 180 else 0
    x0, y0 = pt(r1, a0, cx, cy); x1, y1 = pt(r1, a1, cx, cy); x2, y2 = pt(r0, a1, cx, cy); x3, y3 = pt(r0, a0, cx, cy)
    return (f"M{f(x0)} {f(y0)}A{f(r1)} {f(r1)} 0 {large} 1 {f(x1)} {f(y1)}L{f(x2)} {f(y2)}"
            f"A{f(r0)} {f(r0)} 0 {large} 0 {f(x3)} {f(y3)}Z")


def phase(cx, cy, r, k, lit="right", rot=0.0):
    """Moon phase path: lit semicircle + elliptical terminator. k>0 gibbous, k<0 crescent, k=0 half."""
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


def terminator(cx, cy, r, k, rot=0.0):
    """The terminator alone (an open path), for the came that divides the lit glass from the earthlit glass."""
    rx = abs(k) * r
    sweep = 1 if k > 0 else 0
    d = (f"M{f(cx)} {f(cy + r)}A{f(rx)} {f(r)} 0 0 {sweep} {f(cx)} {f(cy - r)}" if abs(k) > 1e-6
         else f"M{f(cx)} {f(cy + r)}L{f(cx)} {f(cy - r)}")
    return d, (f"rotate({f(rot)} {f(cx)} {f(cy)})" if rot else "")


def lit_mask_np(cx, cy, r, k, rot, xs, ys):
    a = math.radians(-rot)
    dx, dy = xs - cx, ys - cy
    u = dx * math.cos(a) - dy * math.sin(a)
    v = dx * math.sin(a) + dy * math.cos(a)
    inside = u * u + v * v <= r * r
    half = np.sqrt(np.clip(1 - (v / r) ** 2, 0, 1))
    term = -abs(k) * r * half if k > 0 else abs(k) * r * half
    return inside & (u >= term)


def centroid(cx, cy, r, k, rot):
    ys, xs = np.mgrid[0:128:0.25, 0:128:0.25]
    m = lit_mask_np(cx, cy, r, k, rot, xs, ys)
    return float(xs[m].mean()), float(ys[m].mean())


def wave(y, x0, x1, amp, wl, ph=0.0, n=48):
    pts = []
    for i in range(n + 1):
        x = x0 + (x1 - x0) * i / n
        pts.append((x, y + amp * math.sin(2 * math.pi * (x / wl) + ph)))
    return poly(pts, close=False)


def tapered_path(points, widths):
    L, R = [], []
    n = len(points)
    for i, ((x, y), w) in enumerate(zip(points, widths)):
        x0, y0 = points[max(i - 1, 0)]
        x1, y1 = points[min(i + 1, n - 1)]
        dx, dy = x1 - x0, y1 - y0
        ln = math.hypot(dx, dy) or 1
        nx, ny = -dy / ln, dx / ln
        L.append((x + nx * w / 2, y + ny * w / 2))
        R.append((x - nx * w / 2, y - ny * w / 2))
    return poly(L + R[::-1])


def streak(x0, x1, yc, h, skew=0.44, n=20):
    """A horizontal glint: a needle of light drawn to fine points at both ends, with a flatter lower edge."""
    L = x1 - x0
    top, bot = [], []
    e = math.log(0.5) / math.log(skew)
    for i in range(n + 1):
        t = i / n
        u = 2 * t ** e - 1
        prof = (1 - abs(u) ** 3.2) ** 1.1
        x = x0 + L * t
        top.append((x, yc - h * 0.56 * prof))
        bot.append((x, yc + h * 0.44 * prof))
    return poly(top + bot[::-1])


def svg(title, defs, body, vb=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {vb} {vb}" width="{vb}" height="{vb}"><title>{title}</title>'
            f'<defs>{"".join(defs)}</defs>{"".join(body)}</svg>\n')


def write(name, text):
    (OUT / name).write_text(text, encoding="utf-8")


def blur_filter(p, name, sd):
    return (f'<filter id="{p}{name}" filterUnits="userSpaceOnUse" x="-20" y="-20" width="168" height="168" '
            f'color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="{f(sd)}"/></filter>')


def shadow_filter(p, name, dx, dy, sd, op):
    return (f'<filter id="{p}{name}" x="-25%" y="-25%" width="150%" height="150%" color-interpolation-filters="sRGB">'
            f'<feDropShadow dx="{f(dx)}" dy="{f(dy)}" stdDeviation="{f(sd)}" flood-color="{KEY}" flood-opacity="{f(op)}"/></filter>')


def lin_grad(p, name, x1, y1, x2, y2, stops):
    return (f'<linearGradient id="{p}{name}" x1="{f(x1)}" y1="{f(y1)}" x2="{f(x2)}" y2="{f(y2)}" gradientUnits="userSpaceOnUse">'
            + "".join(f'<stop offset="{f(o)}" stop-color="{c}"' + (f' stop-opacity="{f(a)}"' if a is not None else "") + "/>"
                      for o, c, a in [(s + (None,))[:3] for s in stops]) + "</linearGradient>")


def rad_grad(p, name, cx, cy, r, stops, fx=None, fy=None):
    fo = f' fx="{f(fx)}" fy="{f(fy)}"' if fx is not None else ""
    return (f'<radialGradient id="{p}{name}" cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}"{fo} gradientUnits="userSpaceOnUse">'
            + "".join(f'<stop offset="{f(o)}" stop-color="{c}"' + (f' stop-opacity="{f(a)}"' if a is not None else "") + "/>"
                      for o, c, a in [(s + (None,))[:3] for s in stops]) + "</radialGradient>")


# ---------------- lead came ----------------

class Came:
    """Collects came lines (path, width, transform) and draws them as raised lead lit from the upper left:
    a dull grey body, a soft light stripe on its upper-left flank and a dark one on its lower-right flank."""

    def __init__(self, p):
        self.p, self.items = p, []

    def add(self, d, w, tr="", clip=None):
        if w:
            self.items.append((d, w, tr, clip))

    def render(self):
        p = self.p
        D, S = [], []
        groups = {}
        for d, w, tr, clip in self.items:
            groups.setdefault((w, clip), []).append((d, tr))
        for gi, ((w, clip), items) in enumerate(groups.items()):
            paths = "".join(f'<path d="{d}"' + (f' transform="{tr}"' if tr else "") + "/>" for d, tr in items)
            cl = f' clip-path="url(#{clip})"' if clip else ""
            base = (f'<g fill="none" stroke="{LEAD}" stroke-width="{f(w)}" stroke-linecap="round" stroke-linejoin="round"{cl}>'
                    f'{paths}</g>')
            S.append(base)
            if not ROW:
                mid = f"{p}cm{gi}"
                D.append(f'<mask id="{mid}"><g fill="none" stroke="#fff" stroke-width="{f(w)}" stroke-linecap="round" '
                         f'stroke-linejoin="round">{paths}</g></mask>')
                lo = (f'<g transform="translate({f(w * .22)} {f(w * .28)})" fill="none" stroke="{LEAD_LO}" stroke-opacity=".75" '
                      f'stroke-width="{f(w * .42)}" stroke-linecap="round" stroke-linejoin="round">{paths}</g>')
                hi = (f'<g transform="translate({f(-w * .2)} {f(-w * .26)})" fill="none" stroke="{LEAD_HI}" stroke-opacity=".62" '
                      f'stroke-width="{f(w * .3)}" stroke-linecap="round" stroke-linejoin="round">{paths}</g>')
                S.append(f'<g mask="url(#{mid})"{cl}>{lo}{hi}</g>')
        return D, S


# ---------------- the roundel: glass opening, frame, badge ----------------

def opening(p):
    return f'<clipPath id="{p}op"><circle cx="64" cy="64" r="{f(R_WELL)}"/></clipPath>'


def kit_frame(p, tier="resting", quiet=False):
    """The Came kit's medal frame, one per urgency tier. Full: a turned ring of Ishgard stone lit from the upper left
    (outer slope light at the upper left, inner slope the reverse), the came that seats the glass, and on the hero tier
    twelve gothic cusps on its inner edge. Act now is the same shape in the shared medal gilt, with a gilt inner came.
    Quiet: the keyline and one hairline in the tier's metal. The inner edge (r 50.9) covers every face's well (r 52.4)."""
    hi, base, mid, lo, dk, glint = TIERS[tier]
    gilt = tier == "act-now"
    seat = GILT_LIGHT if gilt else LEAD
    if quiet:
        return [], [f'<path d="{ring(R_IN, 55.6)}" fill="{KEY}" fill-rule="evenodd"/>',
                    f'<path d="{ring(53.4, 54.8)}" fill="{base if not gilt else GILT_LIGHT}" fill-rule="evenodd"/>']
    D = [lin_grad(p, "fo", 14, 10, 114, 118, [(0, hi), (.45, base), (.8, mid), (1, lo)]),
         lin_grad(p, "fi", 14, 10, 114, 118, [(0, lo), (.55, mid), (1, hi if gilt else base)])]
    S = [f'<path d="{ring(R_OUT, R_KEY)}" fill="{KEY}" fill-rule="evenodd"/>',
         f'<path d="{ring(R_MID, R_OUT)}" fill="url(#{p}fo)" fill-rule="evenodd"/>',
         f'<path d="{ring(R_IN, R_MID)}" fill="url(#{p}fi)" fill-rule="evenodd"/>',
         f'<path d="{ring(R_MID - .2, R_MID + .2)}" fill="{dk}" fill-opacity=".5" fill-rule="evenodd"/>']
    if not ROW:
        if glint:
            # the key-light catch on the upper-left outer slope: broad and faint on matte stone, crisp on gilt
            D.append(blur_filter(p, "fb", .6 if not gilt else .25))
            a0, a1, rr = -165, -100, R_MID + 2.0
            x0, y0 = pt(rr, a0); x1, y1 = pt(rr, a1)
            S.append(f'<path d="M{f(x0)} {f(y0)}A{f(rr)} {f(rr)} 0 0 1 {f(x1)} {f(y1)}" fill="none" stroke="{GILT_SPEC if gilt else GLINT}" '
                     f'stroke-opacity="{f(glint)}" stroke-width="{1.4 if not gilt else 1.6}" stroke-linecap="round" filter="url(#{p}fb)"/>')
        # twelve cusps: pointed foils on the opening's edge, lit on the face toward the light
        cusp = []
        for i in range(12):
            a = i * 30 + 15
            s0, s1 = pt(R_IN + .4, a - 10), pt(R_IN + .4, a + 10)
            tip = pt(R_IN - 2.6, a)
            c0, c1 = pt(R_IN - .3, a - 3.6), pt(R_IN - .3, a + 3.6)
            cusp.append(f"M{f(s0[0])} {f(s0[1])}Q{f(c0[0])} {f(c0[1])} {f(tip[0])} {f(tip[1])}"
                        f"Q{f(c1[0])} {f(c1[1])} {f(s1[0])} {f(s1[1])}Z")
        S.append(f'<path d="{"".join(cusp)}" fill="url(#{p}fi)" stroke="{LEAD}" stroke-width=".9" stroke-linejoin="round"/>')
    # the came that seats the glass in the frame (gilt on act now)
    S.append(f'<circle cx="64" cy="64" r="{f(R_IN)}" fill="none" stroke="{seat}" stroke-width="{f(1.3 if not ROW else 2.2)}"/>')
    return D, S


BADGE = dict(cx=95.0, cy=95.0, r_key=24.0, r_out=23.1, r_mid=21.5, r_in=19.9, icon=35.5)
SEAT = {"open": ("#6592E0", "#22407E"),      # Ready: azure glass under a warm amber lock
        "closed": ("#33406E", "#121A36"),    # Blocked: night glass under a cool, muted lock
        "journal": ("#6A86C8", "#26407C")}   # In journal: the ribbon's tide glass


def job_png(job):
    return "data:image/png;base64," + base64.b64encode((JOBDIR / f"{job}.png").read_bytes()).decode()


def job_optical_offset(job):
    """Optical centre of the game glyph in its canvas (alpha > 0.5): the mean of the bbox centre and the centroid."""
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


LOCK_OY = -0.2


def glass_piece(fill, d, lw=.95, tr=""):
    t = f' transform="{tr}"' if tr else ""
    return f'<path d="{d}" fill="{fill}"{t}/>', f'<path d="{d}" fill="none" stroke="{LEAD}" stroke-width="{f(lw)}" stroke-linejoin="round"{t}/>'


def lock_glyph(p, cx, cy, open_, lw=.95):
    """A padlock leaded in glass: the body a single slab of glass, the shackle a bent strip of the same glass, both held
    in lead; the keyhole is painted in grisaille. Open (Ready): silver-stain amber, the shackle lifted so its free leg
    clears the body by 4.2. Closed (Blocked): cool, muted moonstone glass. One body position for both."""
    if open_:
        hi, mid, lo = AMBER_HI, AMBER, AMBER_MID
        shackle = "M-4.6 0V-11A4.6 4.6 0 0 1 4.6 -11V-5.5"
    else:
        hi, mid, lo = "#C9D2E6", "#97A5C6", "#66749C"
        shackle = "M-4.6 0V-6A4.6 4.6 0 0 1 4.6 -6V0"
    body_d = "M-7.6 1.9A1.9 1.9 0 0 1 -5.7 0H5.7A1.9 1.9 0 0 1 7.6 1.9V9.1A1.9 1.9 0 0 1 5.7 11H-5.7A1.9 1.9 0 0 1 -7.6 9.1Z"
    key_d = "M0 3.1a1.55 1.55 0 0 1 .95 2.8L1.4 8.3H-1.4L-.95 5.9A1.55 1.55 0 0 1 0 3.1Z"
    t = f'translate({f(cx)} {f(cy + LOCK_OY)})'
    D = [rad_grad(p, "lg", -2.5, 2.5, 13, [(0, hi), (.55, mid), (1, lo)]),
         shadow_filter(p, "ug", .5, .7, .6, .55)]
    # shackle: the glass strip is drawn as a stroke inside a slightly wider lead stroke (came on both edges)
    S = [f'<g transform="{t}" filter="url(#{p}ug)">'
         f'<path d="{shackle}" fill="none" stroke="{LEAD}" stroke-width="{f(3.0 + 2 * lw * .8)}" stroke-linecap="round"/>'
         f'<path d="{shackle}" fill="none" stroke="url(#{p}lg)" stroke-width="3.0" stroke-linecap="round"/>'
         f'<path d="{body_d}" fill="url(#{p}lg)" stroke="{LEAD}" stroke-width="{f(lw * 1.6)}" stroke-linejoin="round"/>'
         # grisaille keyhole: painted dark, its edge softened by the brush
         f'<path d="{key_d}" fill="{GRIS}" fill-opacity=".85"/>'
         # the lead's lit upper-left flank on the body's top edge
         + ('' if ROW else f'<path d="M-6.4 -.6H6.2" stroke="{LEAD_HI}" stroke-opacity=".45" stroke-width=".35" stroke-linecap="round"/>') +
         '</g>']
    return D, S


def book_glyph(p, cx, cy, lw=.95):
    """An open journal leaded in glass: two pages of pale amber glass curving to the spine, a spine came, a few painted
    grisaille lines of script, and a small tide-glass ribbon at the foot."""
    left = "M0 -5C-3 -7.4 -6.6 -7.9 -10 -6.7V7C-6.6 6 -3.2 6.5 0 8.8Z"
    right = "M0 -5C3 -7.4 6.6 -7.9 10 -6.7V7C6.6 6 3.2 6.5 0 8.8Z"
    lines = ("M-8.2 -3.6C-6 -4.3 -3.8 -4 -1.8 -2.8M-8.2 -.8C-6 -1.5 -3.8 -1.2 -1.8 0M-8.2 2C-6 1.3 -3.8 1.6 -1.8 2.8"
             "M8.2 -3.6C6 -4.3 3.8 -4 1.8 -2.8M8.2 -.8C6 -1.5 3.8 -1.2 1.8 0M8.2 2C6 1.3 3.8 1.6 1.8 2.8")
    t = f'translate({f(cx)} {f(cy - 1.3)})'
    D = [rad_grad(p, "bg", -3, -2, 14, [(0, AMBER_HI), (.6, AMBER), (1, AMBER_MID)]),
         shadow_filter(p, "ug", .5, .7, .6, .55)]
    rib = "M-1 8.4V12.8L0 11.8L1 12.8V8.4Z"
    S = [f'<g transform="{t}" filter="url(#{p}ug)">'
         f'<path d="{rib}" fill="{TIDE}" stroke="{LEAD}" stroke-width="{f(lw * .8)}" stroke-linejoin="round"/>'
         f'<path d="{left}" fill="url(#{p}bg)"/><path d="{right}" fill="url(#{p}bg)"/>'
         f'<path d="{right}" fill="{GRIS}" fill-opacity=".1"/>'
         f'<path d="{lines}" fill="none" stroke="{GRIS}" stroke-opacity=".55" stroke-width=".55" stroke-linecap="round"/>'
         f'<path d="{left}{right}" fill="none" stroke="{LEAD}" stroke-width="{f(lw * 1.6)}" stroke-linejoin="round"/>'
         '</g>']
    return D, S


def badge_ring(p, tier="resting"):
    """The shared badge slot (Medallion r5 geometry): centre (95, 95), keyline r 24 with the standard down-right shadow,
    ring r 19.9-23.1 in the tier's metal (gilt on act now, stone otherwise)."""
    hi, base, mid, lo, dk, glint = TIERS[tier]
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    x0, y0, x1, y1 = cx - 24, cy - 24, cx + 24, cy + 24
    D = [lin_grad(p, "go", x0, y0, x1, y1, [(0, hi), (.45, base), (1, lo)]),
         lin_grad(p, "gi", x0, y0, x1, y1, [(0, lo), (.55, mid), (1, base)]),
         shadow_filter(p, "bs", 1.4, 1.9, 1.1, .65)]
    S = [f'<g filter="url(#{p}bs)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_key"])}" fill="{KEY}"/></g>',
         f'<path d="{ring(b["r_mid"], b["r_out"], cx, cy)}" fill="url(#{p}go)" fill-rule="evenodd"/>',
         f'<path d="{ring(b["r_in"], b["r_mid"], cx, cy)}" fill="url(#{p}gi)" fill-rule="evenodd"/>']
    return D, S


def badge_seat(p, kind):
    """The seat: a disc of glass glowing from behind (brightest toward its centre), shaded on its upper-left edge by the
    ring in front of it, with a lead seat line. kind: open / closed / journal, or a role (tank / healer / dps)."""
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    hi, lo = ROLE[kind] if kind in ROLE else SEAT[kind]
    D = [rad_grad(p, "en", cx - 2, cy - 3, b["r_in"] * 1.25, [(0, hi), (1, lo)]),
         f'<clipPath id="{p}ec"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}"/></clipPath>',
         f'<mask id="{p}em"><rect width="128" height="128" fill="#fff"/><circle cx="{f(cx + 1.2)}" cy="{f(cy + 1.6)}" r="{f(b["r_in"])}" fill="#000"/></mask>',
         blur_filter(p, "eb", .8)]
    S = [f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}" fill="url(#{p}en)"/>',
         f'<g clip-path="url(#{p}ec)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"] + 1)}" fill="{KEY}" fill-opacity=".45" mask="url(#{p}em)" filter="url(#{p}eb)"/></g>',
         f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}" fill="none" stroke="{LEAD}" stroke-width="1.1"/>']
    return D, S


def badge_content(p, kind, job=None):
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    if kind == "job":
        name, role, icon_id = JOBS[job]
        ox, oy = job_optical_offset(job)
        sz = b["icon"]
        return ([f'<clipPath id="{p}jc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}"/></clipPath>'],
                [f'<image href="{job_png(job)}" x="{f(cx - sz / 2 - ox * sz)}" y="{f(cy - sz / 2 - oy * sz)}" width="{f(sz)}" height="{f(sz)}" '
                 f'preserveAspectRatio="xMidYMid meet" clip-path="url(#{p}jc)"><title>{name} (game icon {icon_id:06d})</title></image>'])
    if kind in ("open", "closed"):
        return lock_glyph(p, cx, cy, kind == "open")
    return book_glyph(p, cx, cy)


def badge(p, kind, job=None, tier="resting"):
    """The Came kit's whole badge: ring, seat and content. The row tier draws none (the glyph goes beside the row)."""
    if ROW:
        return [], []
    D, S = badge_ring(p + "r", tier)
    dd, ss = badge_seat(p + "s", JOBS[job][1] if kind == "job" else kind); D += dd; S += ss
    dd, ss = badge_content(p + "c", kind, job); D += dd; S += ss
    return D, S


# ---------------- shared glass parts ----------------

def grisaille_blobs(p, clip, blobs, sd, op=1.0, color=GRIS):
    """Grisaille matting: vitreous paint stippled on and badgered soft, so it reads as tone, never as a spot."""
    if ROW:
        return [], []
    els = "".join(f'<ellipse cx="{f(x)}" cy="{f(y)}" rx="{f(rx)}" ry="{f(ry)}" transform="rotate({f(a)} {f(x)} {f(y)})" fill-opacity="{f(o * op)}"/>'
                  for x, y, rx, ry, a, o in blobs)
    return ([blur_filter(p, "gm", sd)], [f'<g clip-path="url(#{clip})"><g filter="url(#{p}gm)" fill="{color}">{els}</g></g>'])


def halation(p, markup, op=.3, sd=1.0):
    """Irradiation: bright glass seen against dark lead spreads over the lead's edges, so the came over the brightest
    glass look thinner. A soft copy of the bright glass over its came (hero only)."""
    if ROW:
        return [], []
    return [blur_filter(p, "hl", sd)], [f'<g filter="url(#{p}hl)" opacity="{f(op)}">{markup}</g>']


def pane_tints(p, clip, cx, cy, angles, r, colors):
    """Alternate panes of a fan (spokes from cx, cy) take slightly different glass: (fill, opacity) per pane."""
    out = []
    for i in range(len(angles)):
        a0, a1 = angles[i], angles[(i + 1) % len(angles)]
        if a1 <= a0:
            a1 += 360
        fill, op = colors[i % len(colors)]
        if op <= 0:
            continue
        out.append(f'<path d="{sector(cx, cy, 0.01, r, a0, a1)}" fill="{fill}" fill-opacity="{f(op)}"/>')
    return f'<g clip-path="url(#{clip})">{"".join(out)}</g>'


# ---------------- Ready, Ready on another job, In journal: the moon road window ----------------
MOON_G = (49.0, 42.0, 29.0, -0.18, 28.0)    # cx, cy, r, k, rot: Medallion's crescent, so Ready and RoJ match across sets
H = 80.0
LIT_C = centroid(*MOON_G)
SUN_X = MOON_G[0] + (H - MOON_G[1]) / math.tan(math.radians(MOON_G[4]))
HALO_R = 42.0
# road rows (y, height, [(dx0, dx1, opacity, height scale)]) about the lit centroid's x; every glint keeps aspect >= 4.6
ROAD_ROWS = [
    (82.3, 1.3, [(-4.0, 3.5, .5, 1)]),
    (85.4, 1.7, [(-7.0, 4.5, .56, 1), (7.0, 12.5, .42, .7)]),
    (92.8, 2.2, [(-9.5, -.5, .62, 1), (2.0, 10.0, .5, .8)]),
    (95.9, 2.5, [(-11.0, 9.5, .68, 1)]),
    (103.6, 3.0, [(-14.0, 1.0, .74, 1), (4.0, 14.0, .6, .65)]),
    (107.3, 3.3, [(-12.0, 12.5, .8, 1)]),
    (114.2, 4.0, [(-22.0, -13.0, .55, .5), (-10.5, 9.5, .9, 1), (12.0, 21.0, .66, .6)]),
]
# the row tier: fewer, fuller glints, so the road survives as one bright broken column at 16-20 px
ROAD_ROWS_ROW = [
    (84.0, 2.6, [(-6.5, 6.0, .62, 1)]),
    (94.5, 3.6, [(-10.5, 9.5, .74, 1)]),
    (105.5, 4.4, [(-13.5, 13.0, .84, 1)]),
    (114.6, 5.0, [(-16.0, 15.0, .92, 1)]),
]
SPOKES = [-140.0, -95.0, -50.0, -5.0, 40.0, 130.0, 175.0]      # seven (critic change 5: pays for the skyline)
LONG_SPOKES = [a for i, a in enumerate(SPOKES) if i % 2 == 0]   # these run on past the halo to the tracery


def crescent_glass(p, mode):
    """The crescent as leaded glass. mode: 'day' (Ready), 'rest' (RoJ), 'night' (In journal).
    Two came cross the crescent along moon radii, so it is cut into three lights, as a glazier would cut it; the glass
    brightens from the terminator to the limb by grisaille matting (soft); near-limb maria are painted soft."""
    mx, my, mr, mk, rot = MOON_G
    d, tr = phase(mx, my, mr, mk, "right", rot)
    D = [f'<clipPath id="{p}lc"><path d="{d}" transform="{tr}"/></clipPath>']
    lit_dir = rot                                   # the lit limb's direction (screen degrees)
    lx, ly = pt(mr, lit_dir, mx, my)
    tx, ty = pt(mr * abs(mk), lit_dir, mx, my)
    if mode == "night":
        stops = [(0, MOON_LOW), (.55, MOON_MID), (1, MOON)]
    else:
        stops = [(0, MOON_MID), (.5, MOON), (1, MOON_HI)]
    D.append(lin_grad(p, "lg0", tx, ty, lx, ly, stops))
    S = [f'<path d="{d}" transform="{tr}" fill="url(#{p}lg0)"/>']
    # the three lights take slightly different glass (pane-to-pane variation): the middle one a touch warmer
    cuts = [lit_dir - 52, lit_dir + 40]
    if not ROW:
        S.append(f'<g clip-path="url(#{p}lc)"><path d="{sector(mx, my, 0.01, mr + 2, cuts[0], cuts[1])}" fill="#FFF6E0" fill-opacity=".16"/>'
                 f'<path d="{sector(mx, my, 0.01, mr + 2, cuts[1], cuts[0] + 360)}" fill="#C9D6F0" fill-opacity=".12"/></g>')
    blobs = [(mx + .55 * mr, my - .15 * mr, .13 * mr, .10 * mr, 0, .32),      # Crisium
             (mx + .48 * mr, my + .28 * mr, .10 * mr, .17 * mr, -15, .26),    # Fecunditatis
             (mx + .22 * mr, my + .42 * mr, .09 * mr, .10 * mr, 0, .22),      # Nectaris
             (mx + .22 * mr, my - .05 * mr, .18 * mr, .14 * mr, 20, .22)]     # Tranquillitatis' edge
    rb = [(x - mx, y - my) for x, y, *_ in blobs]
    ca, sa = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    blobs = [(mx + u * ca - v * sa, my + u * sa + v * ca, rx, ry, a + rot, o) for (u, v), (_, _, rx, ry, a, o) in zip(rb, blobs)]
    dd, ss = grisaille_blobs(p, f"{p}lc", blobs, 1.6, op=.55); D += dd; S += ss
    return D, S, (d, tr), cuts


# The Holy See on the far shore (critic change 5): Saint Reymanaud's spire between two west towers, the Vault's lower
# roofs, and a gate tower; heights in units above the horizon, all left of the road and clear of the badge
SKYLINE = [(15.0, 0.0), (15.0, 2.2), (18.0, 2.2), (18.0, 4.6), (19.4, 6.4), (20.8, 4.6), (20.8, 2.6), (24.6, 2.6),
           (24.6, 3.6), (27.2, 3.6), (27.2, 6.6), (28.2, 8.0), (29.2, 6.6), (29.2, 4.8), (31.4, 4.8), (31.4, 6.2),
           (32.3, 6.2), (33.2, 9.8), (34.1, 6.2), (35.0, 6.2), (35.0, 4.8), (37.2, 4.8), (37.2, 6.6), (38.2, 8.0),
           (39.2, 6.6), (39.2, 3.4), (42.4, 3.4), (42.4, 4.4), (43.6, 5.6), (44.8, 4.4), (44.8, 1.8), (48.0, 1.8),
           (48.0, 0.0)]
ROSE = (33.2, 3.0, 1.05)       # the cathedral's small rose window (x, height, r), lit from inside


def skyline(p, came, mode):
    """The Ishgard skyline (hero tier only) as one piece of dark slate glass leaded on its outline, its small rose
    window a bead of lit amber glass. Its dark mirror is painted below it in grisaille (same height, softened by the
    swell), and the window's warm reflection hangs directly below it at mirror distance."""
    pts = [(x, H - h) for x, h in SKYLINE]
    mir = [(x, H + h) for x, h in SKYLINE]
    sil = poly(pts)
    rx, rh, rr = ROSE
    if mode == "day":
        fill, win, wop, mop = "#33447A", AMBER_HI, .95, .3
    else:
        fill, win, wop, mop = "#24336A", AMBER, .7, .22
    D = [blur_filter(p, "sm", .5)]
    G = [f'<g filter="url(#{p}sm)"><path d="{poly(mir)}" fill="{GRIS}" fill-opacity="{f(mop)}"/></g>',
         f'<path d="{sil}" fill="{fill}"/>',
         f'<circle cx="{f(rx)}" cy="{f(H - rh)}" r="{f(rr)}" fill="{win}" fill-opacity="{f(wop)}"/>',
         f'<path d="{streak(rx - 1.6, rx + 1.6, H + rh, .7)}" fill="{win}" fill-opacity="{f(wop * .5)}"/>']
    came.add(sil, W2() * .8)
    return D, G


def moon_window(p, mode):
    """mode 'day' = Ready, 'rest' = Ready on another job (night sky, no sea), 'night' = In journal."""
    D, S = [opening(p)], []
    mx, my, mr, mk, rot = MOON_G
    cxr, cyr = LIT_C
    came = Came(p)
    sea = mode != "rest"
    if mode == "day":
        sky_t, sky_h, sea_h, sea_b = DAY_SKY_T, DAY_SKY_H, DAY_SEA_H, DAY_SEA_B
    elif mode == "night":
        sky_t, sky_h, sea_h, sea_b = NIGHT_SKY_T, NIGHT_SKY_H, NIGHT_SEA_H, NIGHT_SEA_B
    else:
        sky_t, sky_h, sea_h, sea_b = "#18244E", "#121B40", None, None
    hz = H if sea else 118.0
    D.append(lin_grad(p, "sky", 0, 10, 0, hz, [(0, sky_t), (1, sky_h)]))
    D.append(f'<clipPath id="{p}skc"><rect x="0" y="0" width="128" height="{f(H if sea else 128)}"/></clipPath>')
    G = [f'<rect width="128" height="128" fill="url(#{p}sky)"/>']
    # the halo: a rose of lighter glass round the moon, cut by spokes into lights that alternate in tint
    halo_on = {"day": .15, "rest": .13, "night": .1}[mode]
    alt = {"day": .1, "rest": .05, "night": .05}[mode]
    D.append(f'<clipPath id="{p}hc"><circle cx="{f(mx)}" cy="{f(my)}" r="{f(HALO_R)}"/></clipPath>')
    G.append(f'<g clip-path="url(#{p}skc)"><circle cx="{f(mx)}" cy="{f(my)}" r="{f(HALO_R)}" fill="{MOON}" fill-opacity="{f(halo_on)}"/>'
             + pane_tints(p, f"{p}hc", mx, my, SPOKES, HALO_R + 1, [("#FFFFFF", alt), ("#FFFFFF", 0)]) + '</g>')
    # outside the halo, every other spoke runs on to the tracery, and those sky panes alternate faintly too
    G.append(f'<g clip-path="url(#{p}skc)">' + pane_tints(p, f"{p}op", mx, my, LONG_SPOKES, 130,
                                                         [("#FFFFFF", alt * .45), ("#000000", alt * .5)]) + '</g>')
    if sea:
        D.append(lin_grad(p, "sea", 0, H, 0, 118, [(0, sea_h), (1, sea_b)]))
        G.append(f'<rect y="{f(H)}" width="128" height="{f(128 - H)}" fill="url(#{p}sea)"/>')
        # sea bands between the wave came alternate in tint; the first band mirrors the sky (Fresnel)
        waves = [(89.0, 1.1, 21, .4), (99.5, 1.5, 25, 1.9), (111.0, 1.9, 30, 3.1)]
        D.append(lin_grad(p, "fr", 0, H, 0, 89, [(0, sky_h, .55 if mode == "day" else .3), (1, sky_h, 0)]))
        G.append(f'<rect y="{f(H)}" width="128" height="10" fill="url(#{p}fr)"/>')
        G.append(f'<path d="M0 {f(99.5)}{wave(99.5, 0, 128, 1.5, 25, 1.9)[1:].replace("M", "L")}L128 111L0 111Z" fill="#000" fill-opacity=".08"/>')
        # the road: the sea is FLASHED glass (a thin blue layer on pale glass), and the moon's glints are abraded
        # through the flash, revealing the pale glass beneath, as medieval glaziers did. So the road needs no lead of
        # its own: thin streaks under the lit centroid, foreshortened toward the horizon, brightest toward the viewer
        # (the mirror point falls at the bottom edge), each kept between the wave came.
        rows = ROAD_ROWS_ROW if ROW else ROAD_ROWS
        if mode == "day":
            rc, kk = MOON_HI, 1.12
        else:
            rc, kk = MOON_MID, .78
        st = []
        for y, rh, dashes in rows:
            for x0, x1, o, hs in dashes:
                st.append(f'<path d="{streak(cxr + x0, cxr + x1, y, rh * hs)}" fill="{rc}" fill-opacity="{f(min(o * kk, .88))}"/>')
        if not ROW:
            D.append(blur_filter(p, "ab", .22))
            G.append(f'<g filter="url(#{p}ab)">{"".join(st)}</g>')
        else:
            G.append("".join(st))
        for y, a, wl, ph in waves:
            came.add(wave(y, 0, 128, a, wl, ph), W2() or (W1() * .85 if y == 99.5 else None))
        came.add(f"M0 {f(H)}H128", W1())
        if not ROW:
            dd, gg = skyline(p, came, mode); D += dd; G += gg
    # the moon: in the night window the dark part is earthlit glass; in the day window it is the halo's glass
    if mode != "day":
        G.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="{"#3C4882" if mode == "rest" else "#36467F"}"/>')   # one piece of earthlit glass
    else:
        # by day the unlit disc passes the sky's own light: one piece of the halo's glass, untinted by the spokes
        G.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="url(#{p}sky)"/>'
                 f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="{MOON}" fill-opacity="{f(halo_on)}"/>')
    dd, ss, (cd, ctr), cuts = crescent_glass(p, mode); D += dd; G += ss
    # spokes (secondary): inside the halo from the moon's limb, and every other one on to the tracery
    for i, a in enumerate(SPOKES):
        r1 = 140 if a in LONG_SPOKES else HALO_R
        x0, y0 = pt(mr, a, mx, my); x1, y1 = pt(r1, a, mx, my)
        came.add(f"M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}", W2(), clip=f"{p}skc")
    came.add(circle_d(mx, my, HALO_R), W2(), clip=f"{p}skc")
    came.add(circle_d(mx, my, mr), W2() if not ROW else None)                     # the disc's dark limb
    came.add(cd, W1() if not ROW else W1() * .6, ctr)                              # the crescent
    for a in (cuts if not ROW else []):           # the row tier drops the cuts: the disc and crescent came carry it
        x0, y0 = pt(mr * .2, a, mx, my); x1, y1 = pt(mr + 3, a, mx, my)
        came.add(f"M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}", W2() or W1() * .7, clip=f"{p}lc")
    if ROW:
        came.add(circle_d(mx, my, mr), W1() * .55)    # the whole disc stays leaded, so the crescent reads as a moon
    dd, cs = came.render(); D += dd
    glow = f'<path d="{cd}" transform="{ctr}" fill="{MOON_HI if mode != "night" else MOON_MID}"/>'
    dd, hs = halation(p, glow, .28 if mode != "night" else .18); D += dd
    S.append(f'<g clip-path="url(#{p}op)">{"".join(G)}{"".join(cs)}{"".join(hs)}</g>')
    return D, S


def face(title, key, under, over=([], []), badge_kind=None, job=None):
    """A face: the well and its glass ('under', clipped to r 52.4), and anything that overhangs the rim ('over', drawn
    above the kit's frame). The frame tier and the badge come from the kit, by state."""
    return dict(title=title, key=key, under=under, over=over, badge=badge_kind, job=job, tier=STATE_TIER[key])


def ready():
    return face("Ready", "ready", moon_window("igr-", "day"), badge_kind="open")


def ready_other_job(job=DEFAULT_JOB):
    return face(f"Ready on another job: {JOBS[job][0]}", "ready-on-another-job", moon_window(f"igo{job[:3]}-", "rest"),
                badge_kind="job", job=job)


def ribbon(p):
    """The bookmark: a silk ribbon (applied, so front-lit, unlike the glass) whose top wraps behind the medal along an
    arc concentric with it (r 66), lies over the frame and drops into the well over the moon's lower horn, ending in a
    swallowtail. Lit edge on the left; it shades the frame where it drops into the well; its shadow falls on the frame
    only (glass takes no cast shadow)."""
    x0, w, y1 = 24.5, 17.5, 76.0
    ya = 64 - math.sqrt(66 ** 2 - (64 - x0) ** 2)
    yb = 64 - math.sqrt(66 ** 2 - (64 - x0 - w) ** 2)
    rib = f"M{f(x0)} {f(ya)}A66 66 0 0 1 {f(x0 + w)} {f(yb)}V{f(y1)}L{f(x0 + w / 2)} {f(y1 - 7)}L{f(x0)} {f(y1)}Z"
    D = [lin_grad(p, "rb", x0, 0, x0 + w, 0, [(0, TIDE_HI), (.3, TIDE), (1, TIDE_DEEP)]),
         f'<clipPath id="{p}rc"><path d="{rib}"/></clipPath>',
         f'<mask id="{p}fm"><rect width="128" height="128" fill="#fff"/><circle cx="64" cy="64" r="{f(R_IN)}" fill="#000"/></mask>',
         blur_filter(p, "rs", 1.1)]
    S = [f'<g mask="url(#{p}fm)"><path d="{rib}" fill="{KEY}" fill-opacity=".6" transform="translate(1.2 1)" filter="url(#{p}rs)"/></g>',
         f'<path d="{rib}" fill="url(#{p}rb)" stroke="{KEY}" stroke-width="{2.2 if not ROW else 3.0}" stroke-linejoin="round" paint-order="stroke"/>',
         f'<g clip-path="url(#{p}rc)"><path d="{ring(R_KEY, 70)}" fill="{TIDE_DEEP}" fill-rule="evenodd"/>']
    if not ROW:
        S[-1] += (f'<path d="{ring(R_MID - .9, R_MID + 1.1)}" fill="{TIDE_HI}" fill-opacity=".5" fill-rule="evenodd"/>'
                  f'<path d="{ring(R_IN - .5, R_IN + 2.6)}" fill="{KEY}" fill-opacity=".3" fill-rule="evenodd"/>')
    S[-1] += '</g>'
    if not ROW:
        S.append(f'<g opacity=".5"><path d="M{f(x0 + 2.4)} 14V{f(y1 - 5.5)}M{f(x0 + w - 2.4)} 14V{f(y1 - 4)}" stroke="{TIDE_HI}" '
                 f'stroke-width=".55" stroke-dasharray="1.6 1.2"/></g>')
    return D, S


def in_journal():
    return face("In journal", "in-journal", moon_window("igj-", "night"), over=ribbon("igj-o"), badge_kind="journal")


# ---------------- background panes for the other states ----------------

def night_field(p, came, spokes=(-135, -75, -15, 45, 105, 165), tint=.05, cx=64.0, cy=64.0):
    """Night glass behind a subject, cut by a few spokes from the centre into lights that alternate faintly."""
    D = [lin_grad(p, "nf", 0, 10, 0, 118, [(0, DUSK_T), (1, DUSK_B)])]
    G = [f'<rect width="128" height="128" fill="url(#{p}nf)"/>',
         pane_tints(p, f"{p}op", cx, cy, list(spokes), 130, [("#FFFFFF", tint), ("#000000", tint)])]
    bg = Came(p + "n")       # the field's own came, drawn now so the subject's glass lies over them
    for a in spokes:
        x1, y1 = pt(70, a, cx, cy)
        bg.add(f"M{f(cx)} {f(cy)}L{f(x1)} {f(y1)}", W2())
    dd, cs = bg.render()
    return D + dd, G + cs


# ---------------- Blocked: a new moon behind cloud ----------------
BLOCKED_MOON = (72.0, 44.0, 25.0)
BLOCKED_LIMB = (-0.62, -12.0)
BACK_CLOUD = ([(48.0, 42.0, 6.5), (58.5, 36.5, 9.0), (69.5, 39.0, 8.0), (80.0, 42.5, 6.0)], (41.0, 87.0, 39.0, 49.0))
# the front bank sits low and wide: it fills the lower window, where no other state puts any light
FRONT_CLOUD = ([(20.0, 82.0, 9.0), (33.0, 72.0, 12.5), (49.0, 67.0, 13.5), (64.5, 70.0, 11.5), (77.0, 76.0, 9.0), (88.5, 81.5, 6.5)],
               (11.0, 98.0, 74.0, 92.0))


def cloud_outline(bumps, base, n=240):
    """The outer boundary of a cumulus (union of billows and a rounded base bar) as one path, for its came."""
    x0, x1, yt, yb = base
    rr = (yb - yt) / 2
    def inside(x, y):
        if any((x - bx) ** 2 + (y - by) ** 2 <= br ** 2 for bx, by, br in bumps):
            return True
        if x0 + rr <= x <= x1 - rr and yt <= y <= yb:
            return True
        for ex in (x0 + rr, x1 - rr):
            if (x - ex) ** 2 + (y - (yt + rr)) ** 2 <= rr ** 2:
                return True
        return False
    cx = (x0 + x1) / 2
    cy = (min(by - br for bx, by, br in bumps) + yb) / 2
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        lo, hi = 0.0, 80.0
        for _ in range(26):
            m = (lo + hi) / 2
            if inside(cx + m * math.cos(a), cy + m * math.sin(a)):
                lo = m
            else:
                hi = m
        pts.append((cx + lo * math.cos(a), cy + lo * math.sin(a)))
    return poly(pts)


def cloud_glass(p, n, bumps, base, came, light):
    """A cloud of smoky glass, as glass and not relief (supervisor G1): two flat tones, no shading and no shadow. The
    body is one piece; where the cloud is thinnest, along the edge nearest the moon's lit limb, the backlight comes
    through brighter, so that edge is cut as a second, lighter piece along a curved came. light = (cx, cy, r): the
    circle whose inside is the lighter piece."""
    out = cloud_outline(bumps, base)
    lx, ly, lr = light
    D = [f'<clipPath id="{p}c{n}c"><path d="{out}"/></clipPath>']
    G = [f'<path d="{out}" fill="{CLOUD_BODY}"/>',
         f'<g clip-path="url(#{p}c{n}c)"><circle cx="{f(lx)}" cy="{f(ly)}" r="{f(lr)}" fill="{CLOUD_LIGHT}"/></g>']
    came.add(circle_d(lx, ly, lr), W2() or None, clip=f"{p}c{n}c")
    came.add(out, W1() if not ROW else W1() * .6)
    return D, G


def blocked():
    p = "igb-"
    D, S = [opening(p)], []
    came = Came(p)
    dd, G = night_field(p, came, spokes=(-150, -100, -40, 20, 80, 140)); D += dd
    mx, my, mr = BLOCKED_MOON
    # the new moon: ashen, earthlit glass with only a thin sunlit limb of moon glass
    D.append(rad_grad(p, "as", mx - 8, my - 9, mr * 1.5, [(0, ASH_HI), (1, ASH_LO)]))
    G.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="url(#{p}as)"/>')
    d, tr = phase(mx, my, mr, *BLOCKED_LIMB)
    D.append(lin_grad(p, "lb", mx, my, mx + mr * .98, my - mr * .2, [(0, MOON_LOW), (.6, MOON_MID), (1, MOON)]))
    G.append(f'<path d="{d}" transform="{tr}" fill="url(#{p}lb)"/>')
    came.add(circle_d(mx, my, mr), W1())
    td, ttr = terminator(mx, my, mr, BLOCKED_LIMB[0], BLOCKED_LIMB[1])
    came.add(td, W2() or W1() * .7, ttr)
    dd, cs = came.render(); D += dd; G += cs          # the moon is leaded before the clouds are set over it
    glow = f'<path d="{d}" transform="{tr}" fill="{MOON_MID}"/>'
    dd, hs = halation(p, glow, .2); D += dd; G += hs
    for n, cl, light in (("a", BACK_CLOUD, (97.0, 38.0, 21.0)), ("b", FRONT_CLOUD, (72.0, 44.0, 48.0))):
        cc = Came(p + n)
        dd, gg = cloud_glass(p, n, *cl, cc, light); D += dd; G += gg
        dd, cs = cc.render(); D += dd; G += cs
    S.append(f'<g clip-path="url(#{p}op)">{"".join(G)}</g>')
    return face("Blocked", "blocked", (D, S), badge_kind="closed")


# ---------------- Done this cycle: a waning half moon inside a repeat arrow ----------------

def spiral_r(r0, r1, a0, a1, a):
    return r0 + (r1 - r0) * (a - a0) / (a1 - a0)


def repeat_arrow(cx, cy, r0, r1, a0, a1, w, head_len, head_w, n=90):
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        a = a0 + (a1 - a0) * t
        rm = spiral_r(r0, r1, a0, a1, a)
        hw = w / 2 * (0.16 + 0.84 * min(t / 0.42, 1) ** 0.8)
        outer.append(pt(rm + hw, a, cx, cy))
        inner.append(pt(rm - hw, a, cx, cy))
    da = math.degrees(head_len / r1)
    head = [pt(r1 + head_w / 2, a1, cx, cy), pt(r1 + .4, a1 + da * .55, cx, cy), pt(r1, a1 + da, cx, cy),
            pt(r1 - .4, a1 + da * .55, cx, cy), pt(r1 - head_w / 2, a1, cx, cy)]
    return poly(outer + head + inner[::-1])


def done():
    p = "igd-"
    D, S = [opening(p)], []
    came = Came(p)
    # no spokes behind Done (critic change 3): spokes round a half moon inside a circular arrow read as a clock with a
    # wrap-around arrow (the "history" icon) and turn the half moon into a pie chart. The field is two plain lights,
    # left as one quiet light: the arrow and the half moon carry the glass.
    dd, G = night_field(p, came, spokes=()); D += dd
    mx, my, mr = 64.0, 64.0, 23.0
    # a waning half moon: the lit (left) half is moon glass, the right half earthlit ash, a straight came between them
    G.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="{ASH_HI}"/>')
    d, tr = phase(mx, my, mr, 0.0, "left")
    D.append(f'<clipPath id="{p}hc"><path d="{d}" transform="{tr}"/></clipPath>')
    D.append(lin_grad(p, "hg", mx, my, mx - mr, my - mr * .2, [(0, MOON_MID), (.5, MOON), (1, MOON_HI)]))
    G.append(f'<path d="{d}" transform="{tr}" fill="url(#{p}hg)"/>')
    blobs = [(mx - .30 * mr, my - .36 * mr, .34 * mr, .26 * mr, -18, .3), (mx - .22 * mr, my + .30 * mr, .2 * mr, .16 * mr, 10, .22),
             (mx - .58 * mr, my - .02 * mr, .24 * mr, .38 * mr, 8, .24), (mx - .1 * mr, my - .1 * mr, .16 * mr, .17 * mr, 0, .2)]
    dd, ss = grisaille_blobs(p, f"{p}hc", blobs, 1.7, op=.55); D += dd; G += ss
    came.add(circle_d(mx, my, mr), W1())
    came.add(f"M{f(mx)} {f(my - mr)}V{f(my + mr)}", W1())
    # the repeat arrow: a band of silver-stain amber glass, leaded, spiralling clockwise from the lower left over the
    # top to the lower right, its head pointing on round; open at the bottom, so it never closes into an orbit ring
    r0, r1, a0, a1, w = 31.0, 38.5, 132.0, 385.0, (8.4 if not ROW else 10.0)
    arrow = repeat_arrow(64, 64, r0, r1, a0, a1, w, 13.5, 17.0)
    D.append(lin_grad(p, "ag", 22, 18, 106, 106, [(0, AMBER_HI), (.5, AMBER), (1, AMBER_MID)]))
    D.append(f'<clipPath id="{p}ac"><path d="{arrow}"/></clipPath>')
    G.append(f'<path d="{arrow}" fill="url(#{p}ag)"/>')
    if not ROW:
        # streaky silver stain: the colour is denser along the band's inner edge (painted on the back, soft)
        D.append(blur_filter(p, "ab", 1.2))
        G.append(f'<g clip-path="url(#{p}ac)"><circle cx="64" cy="64" r="33.5" fill="none" stroke="{AMBER_LO}" stroke-opacity=".35" stroke-width="3" filter="url(#{p}ab)"/></g>')
        for a in (205.0, 290.0):        # the band is cut into three lengths of glass
            ri = spiral_r(r0, r1, a0, a1, a)
            x0, y0 = pt(ri - w, a); x1, y1 = pt(ri + w, a)
            came.add(f"M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}", W2(), clip=f"{p}ac")
    came.add(arrow, W1() if not ROW else W1() * .6)
    dd, cs = came.render(); D += dd
    glow = f'<path d="{d}" transform="{tr}" fill="{MOON_HI}"/><path d="{arrow}" fill="{AMBER_HI}"/>'
    dd, hs = halation(p, glow, .22); D += dd
    S.append(f'<g clip-path="url(#{p}op)">{"".join(G)}{"".join(cs)}{"".join(hs)}</g>')
    return face("Done this cycle", "done-this-cycle", (D, S))


# ---------------- Completed: the full moon with the gold check ----------------

def bez(p0, p1, p2, p3, n=24):
    return [((1 - t) ** 3 * p0[0] + 3 * (1 - t) ** 2 * t * p1[0] + 3 * (1 - t) * t * t * p2[0] + t ** 3 * p3[0],
             (1 - t) ** 3 * p0[1] + 3 * (1 - t) ** 2 * t * p1[1] + 3 * (1 - t) * t * t * p2[1] + t ** 3 * p3[1])
            for t in (i / n for i in range(n + 1))]


def arc_pts(cx, cy, r, a0, a1, n=24):
    return [pt(r, a0 + (a1 - a0) * i / n, cx, cy) for i in range(n + 1)]


def check_over(p):
    """The gilt check (applied metal, so front-lit and keylined), struck across the lower right and out past the rim,
    as in every set. Its shadow falls on the frame only: glass takes no cast shadow."""
    chk = "M64 86L78 100L117.5 40"
    D = [lin_grad(p, "ck", 64, 40, 117, 100, [(0, GILT_HI), (.5, GILT_LIGHT), (1, "#A88B52")]),
         f'<mask id="{p}fm"><rect width="128" height="128" fill="#fff"/><circle cx="64" cy="64" r="{f(R_IN)}" fill="#000"/></mask>',
         blur_filter(p, "cs", .9)]
    S = [f'<g mask="url(#{p}fm)"><path d="{chk}" fill="none" stroke="{KEY}" stroke-opacity=".6" stroke-width="15" stroke-linecap="round" '
         f'stroke-linejoin="round" transform="translate(1.1 1.6)" filter="url(#{p}cs)"/></g>',
         f'<path d="{chk}" fill="none" stroke="{KEY}" stroke-width="15" stroke-linecap="round" stroke-linejoin="round"/>',
         f'<path d="{chk}" fill="none" stroke="url(#{p}ck)" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/>']
    if not ROW:
        S.append(f'<path d="M62.3 83.3L76.5 97.4L115.1 38.3" fill="none" stroke="{GILT_SPEC}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" opacity=".85"/>')
    return D, S


def catmull(points, n=8):
    out = []
    m = len(points)
    for i in range(m):
        p0, p1, p2, p3 = points[i - 1], points[i], points[(i + 1) % m], points[(i + 2) % m]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(.5 * (2 * p1[j] + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                   + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    return out




# The moon's face as a glazier would cut it (owner, round 3; supervisor round 3): the maria in their real connectivity,
# as three leaded pieces, never as scattered islands. Points in fractions of r about the moon's centre, joined by a
# smooth closed curve (Catmull-Rom); the west piece runs off the limb and is clipped by it.
MARE_DEEP, MARE_PALE = "#8E9ABD", "#A6B1CD"     # with MARE between them: three related mare glasses
WEST = [(-.88, -.5), (-.6, -.68), (-.3, -.7), (-.1, -.56), (-.08, -.38), (-.2, -.22), (-.32, -.06), (-.34, .18),
        (-.28, .42), (-.4, .62), (-.64, .64), (-.88, .46), (-1.08, .1), (-1.08, -.28)]      # Imbrium + Procellarum
EAST = [(.06, -.5), (.2, -.56), (.33, -.44), (.37, -.24), (.5, -.12), (.57, .08), (.67, .2), (.67, .44), (.53, .55),
        (.41, .43), (.3, .47), (.16, .45), (.12, .29), (.18, .14), (.1, .02), (.03, -.14), (.04, -.34)]  # Serenitatis to Nectaris
CRISIUM = (.72, -.24, .1, .13)
# tone shifts inside the pieces: (piece, divider polyline, tone above or below it)
WEST_DIV = [(-1.1, -.26), (-.7, -.2), (-.4, -.26), (-.1, -.3)]       # Imbrium (deep) over Procellarum (pale)
EAST_DIV = [[(-.05, -.2), (.25, -.24), (.6, -.14)],                  # Serenitatis (mid) over Tranquillitatis (deep)
            [(.05, .22), (.4, .2), (.75, .16)]]                       # Tranquillitatis over Fecunditatis + Nectaris (pale)
TYCHO = (-.1, .68, .105)                                              # r about 3.4: one bright crater, hero only


def completed():
    """The full moon in leaded glass. Highland glass #C8D2E8, flat. The maria in their real connectivity, as three
    leaded pieces: the west (Imbrium joined to Oceanus Procellarum), the east chain (Serenitatis, Tranquillitatis,
    Fecunditatis, Nectaris) and Crisium alone by the east limb. Inside the two big pieces the glass shifts tone across a
    lead (three related mare glasses), so they read as seas, not islands. Tycho is one bright roundel, hero only.
    The row tier shows only the two big pieces, one tone each, with no lead and no Crisium or crater."""
    p = "igc-"
    D, S = [opening(p)], []
    came = Came(p)
    cx, cy, r = 60.0, 60.0, 32.0
    dd, G = night_field(p, came, spokes=(-150, -105, -60, -15, 30, 75, 120, 165), cx=cx, cy=cy); D += dd
    P = lambda pts: [(cx + u * r, cy + v * r) for u, v in pts]
    D.append(f'<clipPath id="{p}fc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}"/></clipPath>')
    G.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="{HIGHLAND}"/>')
    west, east = poly(P(catmull(WEST, 6))), poly(P(catmull(EAST, 6)))
    D.append(f'<clipPath id="{p}wc"><path d="{west}"/></clipPath><clipPath id="{p}ec"><path d="{east}"/></clipPath>')
    if ROW:
        G.append(f'<g clip-path="url(#{p}fc)"><path d="{west}" fill="{MARE_PALE}"/><path d="{east}" fill="{MARE_DEEP}"/></g>')
    else:
        # west: Procellarum's pale glass, with Imbrium's deeper glass above the dividing lead
        wd = P(WEST_DIV)
        above = poly(wd + [(cx + 1.2 * r, cy - 1.2 * r), (cx - 1.2 * r, cy - 1.2 * r)])
        G.append(f'<g clip-path="url(#{p}fc)"><path d="{west}" fill="{MARE_PALE}"/>'
                 f'<g clip-path="url(#{p}wc)"><path d="{above}" fill="{MARE_DEEP}"/></g></g>')
        # east: Serenitatis (mid) / Tranquillitatis (deep) / Fecunditatis and Nectaris (pale)
        d1, d2 = P(EAST_DIV[0]), P(EAST_DIV[1])
        top = poly(d1 + [(cx + 1.2 * r, cy - 1.2 * r), (cx - 1.2 * r, cy - 1.2 * r)])
        bot = poly(d2 + [(cx + 1.2 * r, cy + 1.2 * r), (cx - 1.2 * r, cy + 1.2 * r)])
        G.append(f'<g clip-path="url(#{p}fc)"><path d="{east}" fill="{MARE_DEEP}"/>'
                 f'<g clip-path="url(#{p}ec)"><path d="{top}" fill="{MARE}"/><path d="{bot}" fill="{MARE_PALE}"/></g></g>')
        u, v, a, b = CRISIUM
        cr = f'<ellipse cx="{f(cx + u * r)}" cy="{f(cy + v * r)}" rx="{f(a * r)}" ry="{f(b * r)}"/>'
        G.append(cr.replace("/>", f' fill="{MARE}"/>'))
        D.append(f'<clipPath id="{p}mc"><path d="{west}"/><path d="{east}"/>{cr}</clipPath>')
        hatch = "".join(f"M{f(cx - r + i * 2.4)} {f(cy - r)}l{f(-r * .9)} {f(2 * r)}" for i in range(int(2 * r / 2.4) + 16))
        G.append(f'<g clip-path="url(#{p}fc)"><g clip-path="url(#{p}mc)"><path d="{hatch}" stroke="{GRIS}" stroke-opacity=".15" stroke-width=".35"/></g></g>')
        came.add(west, W2(), clip=f"{p}fc")
        came.add(east, W2(), clip=f"{p}fc")
        came.add(poly(wd, close=False), W2(), clip=f"{p}wc")
        came.add(poly(d1, close=False), W2(), clip=f"{p}ec")
        came.add(poly(d2, close=False), W2(), clip=f"{p}ec")
        if not MID:
            # from 96 px only: Crisium's own lead and Tycho (at 32-64 px they fall to about a pixel and read as specks;
            # there Crisium is only a tone, with no lead, and Tycho is left out)
            came.add(f"M{f(cx + (u + a) * r)} {f(cy + v * r)}A{f(a * r)} {f(b * r)} 0 1 1 {f(cx + (u - a) * r)} {f(cy + v * r)}"
                     f"A{f(a * r)} {f(b * r)} 0 1 1 {f(cx + (u + a) * r)} {f(cy + v * r)}Z", W2())
            tx, ty, tr = cx + TYCHO[0] * r, cy + TYCHO[1] * r, TYCHO[2] * r
            G.append(f'<circle cx="{f(tx)}" cy="{f(ty)}" r="{f(tr)}" fill="{MOON_HI}"/>'
                     f'<circle cx="{f(tx)}" cy="{f(ty)}" r="{f(tr * .38)}" fill="none" stroke="{MOON_LOW}" stroke-opacity=".5" stroke-width=".45"/>')
            came.add(circle_d(tx, ty, tr), .8)
    came.add(circle_d(cx, cy, r), W1())
    dd, cs = came.render(); D += dd
    S.append(f'<g clip-path="url(#{p}op)">{"".join(G)}{"".join(cs)}</g>')
    return face("Completed", "completed", (D, S), over=check_over(p + "o"))


# ---------------- Locked out: Dalamud's glass, shattered ----------------

def ray_hit(ix, iy, ang, cx=64.0, cy=64.0, R=37.0):
    ux, uy = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    dx, dy = ix - cx, iy - cy
    b = dx * ux + dy * uy
    c = dx * dx + dy * dy - R * R
    t = -b + math.sqrt(b * b - c)
    return ix + ux * t, iy + uy * t


def locked_out():
    """Dalamud in red glass, cracked (owner, round 3: no hole): the impact point up and left of centre, the glass split
    along cracks that bend, every shard still held in its lead at the limb, the night showing only through the crack
    gaps. What makes it Ishgard's: one pane has sagged in its lead (pushed further out of plane and turned), and one
    pane is a darker, strained ruby. Glass edges catch the key light where they face it."""
    p = "igl-"
    D, S = [opening(p)], []
    came = Came(p)
    dd, G = night_field(p, came, spokes=(-140, -80, -20, 40, 100, 160), tint=.04); D += dd
    cx, cy, R = 64.0, 64.0, 37.5
    ix, iy = 52.0, 50.0                          # the impact, well up and left of centre (off every axis)
    # three through-cracks, all oblique (at least 25 degrees from vertical and horizontal), split the ruby into three
    # panes; the hero tier adds hairline cracks that did not run through. Nothing crosses the centre on an axis.
    angs = [-62.0, 35.0, 128.0]
    HAIRLINES = [(-155.0, 1.4), (-108.0, -1.6), (-16.0, 1.2), (78.0, -1.2)]
    # pane 0 (right) has sagged in its lead, further out and turned; pane 1 (below) is a darker, strained ruby
    disp = [3.6, 2.2, 4.0]
    turn = [3.5, -1.0, 1.0]
    FALLING = None
    jog = [2.0, -2.2, 1.8]                       # each crack bends twice, so none is ruled
    gone = ()                                    # no pane has fallen out (owner, round 3)
    DARK = 1
    STRAINED_ROW = "#B04456"                     # row tier: one step under the ruby body, not the hero's deep step
    HOLE_R = 0.70
    n = len(angs)
    # flashed ruby is thinnest (palest) at the top of the sheet and densest at the foot
    # (the row tier keeps the density step small, so at 20-28 px the ruby reads as one body split by cracks, never as a
    # lit half over a dark one, which would echo Done's half moon)
    D.append(lin_grad(p, "rd", cx - R * .3, cy - R, cx + R * .3, cy + R,
                      [(0, RUBY_HI), (.5, RUBY), (1, RUBY_LO)] if not ROW else [(0, RUBY_HI), (.6, RUBY), (1, "#B04456")]))
    D.append(f'<clipPath id="{p}dc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}"/></clipPath>')
    cracks = []
    for a, j in zip(angs, jog):
        e = ray_hit(ix, iy, a, cx, cy, R + 3)
        ux, uy = math.cos(math.radians(a)), math.sin(math.radians(a))
        L = math.hypot(e[0] - ix, e[1] - iy)
        m1 = (ix + ux * L * .38 - j * uy, iy + uy * L * .38 + j * ux)
        m2 = (ix + ux * L * .7 + j * .6 * uy, iy + uy * L * .7 - j * .6 * ux)
        cracks.append(((ix, iy), m1, m2, e))
    def cross_pt(a):
        return ray_hit(ix, iy, a, cx, cy, R * HOLE_R)

    # behind the glass: the night, seen through the broken light
    G.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R + .5)}" fill="{VOID}"/>')
    body = []
    D.append(shadow_filter(p, "fs", 1.4, 2.0, 1.0, .7))
    D.append(blur_filter(p, "sk", 3.0))
    for i in range(n):
        a0, a1 = angs[i], angs[(i + 1) % n]
        if a1 < a0:
            a1 += 360
        c0, c1 = cracks[i], cracks[(i + 1) % n]
        b0 = math.degrees(math.atan2(c0[3][1] - cy, c0[3][0] - cx))
        b1 = math.degrees(math.atan2(c1[3][1] - cy, c1[3][0] - cx))
        while b1 < b0:
            b1 += 360
        arc = [pt(R + 3, b0 + (b1 - b0) * k / 16, cx, cy) for k in range(17)]
        if i in gone:
            # what is left of this light: the outer strip, still in its lead. Its inner edge is one gently curved cross
            # break from crack to crack at about 0.66 R (bowed a little toward the impact point), never a zigzag.
            q0, q1 = cross_pt(angs[i]), cross_pt(angs[(i + 1) % n])
            qm = ((q0[0] + q1[0]) / 2, (q0[1] + q1[1]) / 2)
            ux, uy = qm[0] - ix, qm[1] - iy
            ln = math.hypot(ux, uy)
            ctrl = (qm[0] - ux / ln * 2.2, qm[1] - uy / ln * 2.2)
            chord = [((1 - t) ** 2 * q0[0] + 2 * (1 - t) * t * ctrl[0] + t * t * q1[0],
                      (1 - t) ** 2 * q0[1] + 2 * (1 - t) * t * ctrl[1] + t * t * q1[1]) for t in (k / 10 for k in range(11))]
            pts = [q0, c0[3]] + arc + [c1[3]] + chord[::-1][:-1]
            tx, ty, rot_ = .5, .7, .4
        else:
            pts = [(ix, iy), c0[1], c0[2]] + arc + [c1[2], c1[1]]
            mid = math.radians((a0 + a1) / 2)
            k = 2.2 if ROW else 1.4          # the row tier opens the cracks wider, so they survive at 16-20 px
            tx, ty, rot_ = k * disp[i] * math.cos(mid), k * disp[i] * math.sin(mid), turn[i]
        gx = sum(x for x, _ in pts) / len(pts); gy = sum(y for _, y in pts) / len(pts)
        if i == FALLING:
            # the falling shard hinges down from near the impact point and drops: its outer end swings out past the
            # rim while the gap it leaves stays a thin wedge at the centre (no dark arm joins the hole)
            mid = math.radians((a0 + a1) / 2)
            gx, gy = ix + 7 * math.cos(mid), iy + 7 * math.sin(mid)
            tx, ty, rot_ = -.8, 2.6, -12.0
        sid = f"{p}s{i}"
        D.append(f'<clipPath id="{sid}"><path d="{poly(pts)}"/></clipPath>')
        g = [f'<g clip-path="url(#{sid})"><g clip-path="url(#{p}dc)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="url(#{p}rd)"/>']
        if not ROW:
            hl = ""
            for ha, hj in HAIRLINES:
                if not (a0 <= ha <= a1 or a0 <= ha + 360 <= a1):
                    continue
                he = ray_hit(ix, iy, ha, cx, cy, R - 4)
                hux, huy = math.cos(math.radians(ha)), math.sin(math.radians(ha))
                hL = math.hypot(he[0] - ix, he[1] - iy)
                h1 = (ix + hux * hL * .4 - hj * huy, iy + huy * hL * .4 + hj * hux)
                h2 = (ix + hux * hL * .72 + hj * .6 * huy, iy + huy * hL * .72 - hj * .6 * hux)
                hl += poly([(ix + hux * 3, iy + huy * 3), h1, h2, he], close=False)
            g.append(f'<path d="{hl}" fill="none" stroke="{RUBY_EDGE}" stroke-opacity=".45" stroke-width=".5" transform="translate(.45 .55)"/>'
                     f'<path d="{hl}" fill="none" stroke="{VOID}" stroke-opacity=".7" stroke-width=".55"/>')
            g.append(f'<path d="M{f(cx - R)} {f(cy + 8)}C{f(cx - 10)} {f(cy - 4)} {f(cx + 8)} {f(cy + 18)} {f(cx + R)} {f(cy + 2)}" fill="none" stroke="{RUBY_LO}" stroke-opacity=".22" stroke-width="9" filter="url(#{p}sk)"/>')
            g.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="{"#FFFFFF" if i % 2 else "#000000"}" fill-opacity="{f(.06 + .03 * (i % 3))}"/>')
        if i == DARK:
            g.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="{RUBY_LO}" fill-opacity=".85"/>' if not ROW
                     else f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="{STRAINED_ROW}"/>')
        area = sum(pts[k][0] * pts[(k + 1) % len(pts)][1] - pts[(k + 1) % len(pts)][0] * pts[k][1] for k in range(len(pts)))
        sgn = 1 if area > 0 else -1
        for k in range(len(pts)):
            (x0, y0), (x1, y1) = pts[k], pts[(k + 1) % len(pts)]
            if math.hypot(x0 - cx, y0 - cy) > R + 1.5 and math.hypot(x1 - cx, y1 - cy) > R + 1.5:
                continue
            dx, dy = x1 - x0, y1 - y0
            ln = math.hypot(dx, dy) or 1
            nx, ny = sgn * dy / ln, -sgn * dx / ln
            facing = nx * LIGHT_V[0] + ny * LIGHT_V[1]
            if facing > 0.15 and not ROW:
                g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{RUBY_EDGE}" stroke-width="{f(.8 + .9 * facing)}" stroke-opacity="{f(.45 + .4 * facing)}" stroke-linecap="round"/>')
            elif facing < -0.15 and not ROW:
                g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{KEY}" stroke-width="{f(.9 - .8 * facing)}" stroke-opacity=".5" stroke-linecap="round"/>')
        g.append('</g></g>')
        # the leaded rim of each shard: only the limb arc keeps its lead (the cracks are raw glass)
        rim = "M" + "L".join(f"{f(x)} {f(y)}" for x, y in [pt(R, b0 + (b1 - b0) * k / 16, cx, cy) for k in range(17)])
        g.append(f'<path d="{rim}" fill="none" stroke="{LEAD}" stroke-width="{f(W1())}" stroke-linecap="butt"/>')
        tr = f'translate({f(tx)} {f(ty)}) rotate({f(rot_)} {f(gx)} {f(gy)})'
        body.append(f'<g transform="{tr}">{"".join(g)}</g>')
    G.append(f'<g filter="url(#{p}fs)">{"".join(body)}</g>')
    dd, cs = came.render(); D += dd
    S.append(f'<g clip-path="url(#{p}op)">{"".join(G)}{"".join(cs)}</g>')
    return face("Locked out", "locked-out", (D, S))


# ---------------- Not checked: a veiled moon whose lit sliver is a question mark ----------------

def not_checked():
    p = "ign-"
    D, S = [opening(p)], []
    came = Came(p)
    dd, G = night_field(p, came, spokes=(-150, -90, -30, 30, 90, 150)); D += dd
    vx, vy, vr = 64.0, 52.0, 21.0
    # the veiled moon: a roundel of smoky, seedy glass, hardly passing any light
    D.append(rad_grad(p, "vm", vx - 4, vy - 5, vr + 10, [(0, "#43528A"), (1, "#242F5C")]))
    G.append(f'<circle cx="{f(vx)}" cy="{f(vy)}" r="{f(vr + 4.5)}" fill="url(#{p}vm)"/>')
    came.add(circle_d(vx, vy, vr + 4.5), W2() or W1() * .6)
    pts, ws = [], []
    a0, a1 = 196.0, 398.0
    nseg = 48
    for i in range(nseg + 1):
        t = i / nseg
        a = a0 + (a1 - a0) * t
        pts.append(pt(vr, a, vx, vy))
        ws.append(12.5 * math.sin(math.pi / 2 * min(t / 0.55, 1)) ** 0.85 if t < 0.55 else 12.5 - 3.4 * (t - .55) / .45)
    ex, ey = pts[-1]
    tx, ty = -math.sin(math.radians(a1)), math.cos(math.radians(a1))
    P0, P1, P2, P3 = (ex, ey), (ex + tx * 7, ey + ty * 7), (vx, vy + vr + 3), (vx, vy + vr + 10)
    for i in range(1, 17):
        t = i / 16
        x = (1 - t) ** 3 * P0[0] + 3 * (1 - t) ** 2 * t * P1[0] + 3 * (1 - t) * t * t * P2[0] + t ** 3 * P3[0]
        y = (1 - t) ** 3 * P0[1] + 3 * (1 - t) ** 2 * t * P1[1] + 3 * (1 - t) * t * t * P2[1] + t ** 3 * P3[1]
        pts.append((x, y)); ws.append(9.1 - 0.4 * t)
    for i in range(1, 5):
        pts.append((vx, P3[1] + 1.2 * i)); ws.append(8.7)
    qm = tapered_path(pts, [w * (1.18 if ROW else 1.08) for w in ws])
    end = pts[-1]
    qm_full = f'{qm}M{f(end[0] - 4.35)} {f(end[1])}a4.35 4.35 0 0 0 8.7 0Z'
    dot = (vx, end[1] + 14.0, 6.0 if not ROW else 6.8)
    D.append(lin_grad(p, "qg", vx - vr, vy - vr - 6, vx + vr, dot[1], [(0, MOON_HI), (.6, MOON), (1, MOON_MID)]))
    D.append(f'<clipPath id="{p}qc"><path d="{qm_full}"/></clipPath>')
    G.append(f'<path d="{qm_full}" fill="url(#{p}qg)"/>')
    if not ROW:
        D.append(blur_filter(p, "qb", 1.0))
        G.append(f'<g clip-path="url(#{p}qc)"><g filter="url(#{p}qb)"><circle cx="{f(vx)}" cy="{f(vy)}" r="{f(vr - 5)}" fill="none" stroke="{GRIS}" stroke-width="3" stroke-opacity=".28"/></g></g>')
        # the bowl is cut once (where it turns down into the stem)
        sx, sy = pt(vr, 330, vx, vy)
        x0, y0 = pt(vr - 8, 330, vx, vy); x1, y1 = pt(vr + 8, 330, vx, vy)
        came.add(f"M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}", W2(), clip=f"{p}qc")
    came.add(qm_full, W1() if not ROW else W1() * .65)
    # the dot: a bullseye roundel of crown glass, thick at its centre (the pontil mark), a tiny full moon
    D.append(rad_grad(p, "dg", dot[0] - 1.6, dot[1] - 1.8, dot[2] * 1.6, [(0, MOON_HI), (.6, MOON_MID), (1, MOON_LOW)]))
    G.append(f'<circle cx="{f(dot[0])}" cy="{f(dot[1])}" r="{f(dot[2])}" fill="url(#{p}dg)"/>')
    if not ROW:
        G.append(f'<circle cx="{f(dot[0])}" cy="{f(dot[1])}" r="2.1" fill="none" stroke="{MOON_DEEP}" stroke-opacity=".35" stroke-width=".9"/>')
    came.add(circle_d(*dot), W1() if not ROW else W1() * .65)
    dd, cs = came.render(); D += dd
    glow = f'<path d="{qm_full}" fill="{MOON_HI}"/><circle cx="{f(dot[0])}" cy="{f(dot[1])}" r="{f(dot[2])}" fill="{MOON_HI}"/>'
    dd, hs = halation(p, glow, .22); D += dd
    S.append(f'<g clip-path="url(#{p}op)">{"".join(G)}{"".join(cs)}{"".join(hs)}</g>')
    return face("Not checked", "not-checked", (D, S))


# ---------------- write: faces, the Came kit, composites ----------------
STATES = (("ready", ready), ("ready-on-another-job", ready_other_job), ("in-journal", in_journal),
          ("blocked", blocked), ("done-this-cycle", done), ("completed", completed),
          ("locked-out", locked_out), ("not-checked", not_checked))
BADGE_TIER = {"open": "act-now"}


def part_svg(title, part):
    D, S = part
    return svg(title, D, S)


def compose(fc, quiet=False):
    """Face (under) + the Came kit's frame for the state's tier + the kit's badge + the face's overhangs."""
    D, S = list(fc["under"][0]), list(fc["under"][1])
    dd, ss = kit_frame("kf-", fc["tier"], quiet); D += dd; S += ss
    if fc["badge"]:
        dd, ss = badge("kb-", fc["badge"], fc["job"], BADGE_TIER.get(fc["badge"], "resting")); D += dd; S += ss
    D += fc["over"][0]; S += fc["over"][1]
    return svg(fc["title"], D, S)


def write_tier(sub):
    """Faces (under and over), and composites, for the current tier into OUT/<sub>."""
    base = OUT / sub if sub else OUT
    (base / "faces").mkdir(parents=True, exist_ok=True)
    for key, fn in STATES:
        fc = fn()
        (base / "faces" / f"{key}-under.svg").write_text(part_svg(fc["title"] + " (face)", fc["under"]), encoding="utf-8")
        (base / "faces" / f"{key}-over.svg").write_text(part_svg(fc["title"] + " (face overhangs)", fc["over"]), encoding="utf-8")
        (base / f"{key}.svg").write_text(compose(fc), encoding="utf-8")
    if not ROW:
        for job in JOBS:
            (base / f"ready-on-another-job-{job}.svg").write_text(compose(ready_other_job(job)), encoding="utf-8")


def write_kit():
    """The Came kit: four urgency tiers at Full and Quiet (hero and row), the badge rings, seats and glyphs."""
    global ROW
    kit = OUT / "kit"
    for row in (False, True):
        ROW = row
        d = kit / "_row" if row else kit
        d.mkdir(parents=True, exist_ok=True)
        for tier in TIERS:
            for quiet in (False, True):
                D, S = kit_frame("kf-", tier, quiet)
                (d / f"frame-{tier}-{'quiet' if quiet else 'full'}.svg").write_text(
                    svg(f"Came kit frame: {tier}, {'Quiet' if quiet else 'Full'}", D, S), encoding="utf-8")
    ROW = False
    for tier in ("act-now", "resting"):
        D, S = badge_ring("kr-", tier)
        (kit / f"badge-ring-{tier}.svg").write_text(svg(f"Came kit badge ring: {tier}", D, S), encoding="utf-8")
    for kind in ("open", "closed", "journal", "tank", "healer", "dps"):
        D, S = badge_seat("ks-", kind)
        (kit / f"badge-seat-{kind}.svg").write_text(svg(f"Came kit badge seat: {kind}", D, S), encoding="utf-8")
    for kind in ("open", "closed", "journal"):
        D, S = badge_content("kc-", kind)
        (kit / f"glyph-{kind}.svg").write_text(svg(f"Came kit badge glyph: {kind}", D, S), encoding="utf-8")


if __name__ == "__main__":
    write_tier("")
    MID = True
    write_tier("_mid")
    MID = False
    ROW = True
    write_tier("_row")
    ROW = False
    write_kit()
    # the badge glyphs alone at text height, for drawing beside a row-tier medal (fine lead: drawn ~3x a badge's size)
    for kind in ("open", "closed", "journal"):
        pp = f"igk{kind[0]}-"
        dd, ss = lock_glyph(pp, 0, 0, kind == "open", lw=1.25) if kind != "journal" else book_glyph(pp, 0, 0, lw=1.25)
        (OUT / "_row" / f"badge-{kind}.svg").write_text(
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="-17 -18 34 34" width="34" height="34"><title>{kind} badge glyph</title>'
            f'<defs>{"".join(dd)}</defs>{"".join(ss)}</svg>\n', encoding="utf-8")
    print("lit centroid", LIT_C)
