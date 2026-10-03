"""Menphina's Medallion, round 5. Writes the 8 state SVGs, the job-badge variants,
plugin-icon.svg and the badge-less row tier (_row/) into round5/medallion-r5.

One light for everything: a UI key light from the upper left (azimuth 135 deg, elevation about 45 deg).
Raised metal and raised relief (clouds, shards, the arrow, the badge) are bright on their upper-left edges and dark on
their lower-right ones; recesses are the reverse; raised emblems cast a short soft shadow down and to the right.
Inside the scenes (Ready, In journal, the icon) the moon is the light and every reflection falls directly below its source.
Moon detail is smooth tone only: a soft terminator, a brighter limb, and blurred albedo (maria) shifts. Never craters.
"""
import base64, math, pathlib, random
import numpy as np

OUT = pathlib.Path(__file__).resolve().parent.parent
JOBDIR = pathlib.Path(__file__).resolve().parent / "jobs"

# ---------------- palette tokens ----------------
KEY = "#080B16"                     # Abyss: keylines, grooves, shadow
# one metal for every rim, the badge ring and the icon frame: FFXIV-style champagne gilt
GILT_SPEC, GILT_HI, GILT_LIGHT, GILT, GILT_MID, GILT_DEEP, GILT_DARK = (
    "#FFF4D6", "#E6CF98", "#D9BE82", "#9A7E4A", "#7C6236", "#5C4724", "#33260F")
# lit lapis (Ready only): brighter than round 3, the sky brightening toward the horizon, the sea darker than the sky
LAPIS_SKY_T, LAPIS_SKY_H, LAPIS_SEA_H, LAPIS_SEA_B = "#6090DE", "#8AB2F3", "#5480C8", "#30529A"
AFTERGLOW = "#EDCB94"               # warm twilight on the horizon under the crescent's lit limb (Ready only)
NIGHT_SKY_T, NIGHT_SKY_H, NIGHT_SEA_H, NIGHT_SEA_B = "#1B2856", "#3C528A", "#2A4080", "#18244E"   # In journal
DUSK_T, DUSK_B = "#1D2B5A", "#131C40"                                               # resting enamel
MOON_HI, MOON, MOON_MID, MOON_LOW, MOON_DEEP = "#F4F2EA", "#E2E8F4", "#C3CEE4", "#95A5C8", "#5E6E97"
DARKSIDE = "#17224A"
CLOUD_HI, CLOUD, CLOUD_LOW, CLOUD_DEEP = "#BAC3DB", "#6F7BA2", "#46507A", "#262E4E"     # Blocked (Mist family)
TIDE, TIDE_HI, TIDE_DEEP = "#6F8FD0", "#A9BEEA", "#3F5A98"                         # journal ribbon (Moon Road Tide)
RED_HI, RED, RED_DEEP, RED_EDGE = "#DA6470", "#C24A58", "#7A2838", "#F4AAB2"         # Dalamud (rose-crimson)
CRACK = "#0B0408"
JADE_SPEC, JADE_HI, JADE, JADE_LOW = "#E4F6DC", "#A9DCA2", "#6FAE79", "#3F7A50"       # green-check variant
LANTERN_GOLD = "#E0B860"
CHOCHIN = "#E27A4C"                 # Kugane's red-orange paper lanterns (icon only; OKLCH C under .15)
CHOCHIN_AT = (1, 4, 8, 11)
# role enamels for the job badge (FFXIV's tank blue / healer green / DPS red, held under OKLCH C 0.13)
ROLE = {"tank": ("#5878C2", "#2C417E"), "healer": ("#5C9A68", "#2B5A38"), "dps": ("#B25A64", "#5E2632")}
JOBS = {  # file stem -> (display name, role, game icon id)
    "paladin": ("Paladin", "tank", 62019),
    "bard": ("Bard", "dps", 62023),
    "white-mage": ("White Mage", "healer", 62024),
}
DEFAULT_JOB = "paladin"

C = 64.0
R_KEY, R_OUT, R_MID, R_IN, R_WELL = 63.2, 61.5, 57.2, 53.0, 52.4
LIGHT_DX, LIGHT_DY = 1.1, 1.6       # emblem shadow offset (down-right, away from the key light)
LIGHT_V = (-0.6, -0.8)              # unit vector toward the key light (screen space)


def f(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


def pt(r, a, cx=C, cy=C):
    a = math.radians(a)
    return cx + r * math.cos(a), cy + r * math.sin(a)


def poly(points):
    return "M" + "L".join(f"{f(x)} {f(y)}" for x, y in points) + "Z"


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


def centroid(cx, cy, r, k, side, rot, step=0.25, extent=512):
    ys, xs = np.mgrid[0:extent:step, 0:extent:step]
    m = lit_mask_np(cx, cy, r, k, side, rot, xs, ys)
    return float(xs[m].mean()), float(ys[m].mean())


def taper_arc(rm, a0, a1, w, cx=C, cy=C, n=40, power=1.0):
    """A crescent-shaped band along a circle of radius rm from angle a0 to a1, width 0 -> w -> 0."""
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        a = a0 + (a1 - a0) * t
        hw = w / 2 * math.sin(math.pi * t) ** power
        outer.append(pt(rm + hw, a, cx, cy))
        inner.append(pt(rm - hw, a, cx, cy))
    return poly(outer + inner[::-1])


def sector(r0, r1, a0, a1, cx=C, cy=C):
    large = 1 if (a1 - a0) % 360 > 180 else 0
    x0, y0 = pt(r1, a0, cx, cy); x1, y1 = pt(r1, a1, cx, cy); x2, y2 = pt(r0, a1, cx, cy); x3, y3 = pt(r0, a0, cx, cy)
    return (f"M{f(x0)} {f(y0)}A{f(r1)} {f(r1)} 0 {large} 1 {f(x1)} {f(y1)}L{f(x2)} {f(y2)}"
            f"A{f(r0)} {f(r0)} 0 {large} 0 {f(x3)} {f(y3)}Z")


def ring(r0, r1, cx=C, cy=C):
    return (f"M{f(cx + r1)} {f(cy)}A{f(r1)} {f(r1)} 0 1 1 {f(cx - r1)} {f(cy)}A{f(r1)} {f(r1)} 0 1 1 {f(cx + r1)} {f(cy)}Z"
            f"M{f(cx + r0)} {f(cy)}A{f(r0)} {f(r0)} 0 1 0 {f(cx - r0)} {f(cy)}A{f(r0)} {f(r0)} 0 1 0 {f(cx + r0)} {f(cy)}Z")


def streak(x0, x1, yc, h, skew=0.44, n=20):
    """A horizontal ripple streak: a needle of light, flat through the middle and drawn to fine points at both ends,
    with a flatter lower edge (a wave facet catching a low light)."""
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


def tapered_curve(p0, p1, p2, w, n=36, head=0.42):
    """A brush wisp along a quadratic Bezier: width 0 -> w (at `head`) -> 0, used for mist."""
    pts_l, pts_r = [], []
    for i in range(n + 1):
        t = i / n
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
        y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
        dx = 2 * (1 - t) * (p1[0] - p0[0]) + 2 * t * (p2[0] - p1[0])
        dy = 2 * (1 - t) * (p1[1] - p0[1]) + 2 * t * (p2[1] - p1[1])
        ln = math.hypot(dx, dy) or 1
        nx, ny = -dy / ln, dx / ln
        tt = t ** (math.log(0.5) / math.log(head))
        hw = w / 2 * math.sin(math.pi * tt) ** 0.7
        pts_l.append((x + nx * hw, y + ny * hw))
        pts_r.append((x - nx * hw, y - ny * hw))
    return poly(pts_l + pts_r[::-1])


def tapered_path(points, widths):
    """A filled stroke along a polyline with a per-point width (used for the question mark and the repeat arrow)."""
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


def svg(title, defs, body, vb=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {vb} {vb}" width="{vb}" height="{vb}"><title>{title}</title>'
            f'<defs>{"".join(defs)}</defs>{"".join(body)}</svg>\n')


def write(name, text):
    (OUT / name).write_text(text, encoding="utf-8")


# ---------------- shared medal parts ----------------

def shadow_filter(p, sd=".9", op=".6", dx=LIGHT_DX, dy=LIGHT_DY, name="ds"):
    return (f'<filter id="{p}{name}" x="-25%" y="-25%" width="150%" height="150%" color-interpolation-filters="sRGB">'
            f'<feDropShadow dx="{f(dx)}" dy="{f(dy)}" stdDeviation="{sd}" flood-color="{KEY}" flood-opacity="{op}"/></filter>')


def blur_filter(p, name, sd, ext=128):
    return f'<filter id="{p}{name}" filterUnits="userSpaceOnUse" x="-20" y="-20" width="{ext + 40}" height="{ext + 40}" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="{f(sd)}"/></filter>'


def well(p, top=DUSK_T, bot=DUSK_B, span=None):
    """Enamel well: vertical enamel gradient. Returns (defs, base, overlay): the overlay (a faint vitreous sheen toward
    the light and the soft shadow the raised bezel casts on the upper-left inner edge) is drawn over flush scenes too."""
    gl = (f'x1="0" y1="{f(span[0])}" x2="0" y2="{f(span[1])}" gradientUnits="userSpaceOnUse"' if span else 'x1="0" y1="0" x2="0" y2="1"')
    d = [f'<linearGradient id="{p}wl" {gl}><stop offset="0" stop-color="{top}"/>'
         f'<stop offset="1" stop-color="{bot}"/></linearGradient>',
         f'<clipPath id="{p}wc"><circle cx="64" cy="64" r="{f(R_WELL)}"/></clipPath>',
         f'<radialGradient id="{p}sh" cx="44" cy="40" r="40" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fff" stop-opacity=".09"/>'
         f'<stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>',
         f'<filter id="{p}ib" x="-10%" y="-10%" width="120%" height="120%" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="1.3"/></filter>',
         f'<mask id="{p}im"><rect width="128" height="128" fill="#fff"/><circle cx="{f(64 + 2.6)}" cy="{f(64 + 3.6)}" r="{f(R_WELL)}" fill="#000"/></mask>']
    base = [f'<path d="M0 0H128V128H0Z" fill="url(#{p}wl)" clip-path="url(#{p}wc)"/>']
    over = [f'<circle cx="64" cy="64" r="{f(R_WELL)}" fill="url(#{p}sh)"/>',
            f'<g clip-path="url(#{p}wc)"><circle cx="64" cy="64" r="{f(R_WELL + 1)}" fill="{KEY}" fill-opacity=".55" mask="url(#{p}im)" filter="url(#{p}ib)"/></g>']
    return d, base, over


def bezel(p, hero=True):
    """The one complete medal bezel shared by all eight states: a rounded gilt rim lit from the upper left.
    Outer slope: light upper-left -> dark lower-right. Inner slope: the reverse. A specular streak on the crest at
    upper left, a weaker one on the inner slope at lower right."""
    d = [f'<linearGradient id="{p}bo" x1="14" y1="10" x2="114" y2="118" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{GILT_HI}"/><stop offset=".42" stop-color="{GILT}"/><stop offset=".78" stop-color="{GILT_MID}"/>'
         f'<stop offset="1" stop-color="{GILT_DEEP}"/></linearGradient>',
         f'<linearGradient id="{p}bi" x1="14" y1="10" x2="114" y2="118" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{GILT_DEEP}"/><stop offset=".55" stop-color="{GILT_MID}"/><stop offset="1" stop-color="{GILT_LIGHT}"/></linearGradient>']
    s = [f'<path d="{ring(R_IN - 0.9, R_KEY)}" fill="{KEY}" fill-opacity=".92" fill-rule="evenodd"/>',
         f'<path d="{ring(R_MID, R_OUT)}" fill="url(#{p}bo)" fill-rule="evenodd"/>',
         f'<path d="{ring(R_IN, R_MID)}" fill="url(#{p}bi)" fill-rule="evenodd"/>',
         f'<path d="{ring(R_MID - 0.25, R_MID + 0.25)}" fill="{GILT_DARK}" fill-opacity=".45" fill-rule="evenodd"/>',
         f'<path d="{taper_arc(R_MID + 2.3, -168, -102, 2.4)}" fill="{GILT_SPEC}" fill-opacity=".95"/>',
         f'<path d="{taper_arc(R_IN + 1.9, 18, 52, 1.6)}" fill="{GILT_SPEC}" fill-opacity=".45"/>']
    if hero:
        # four incised moon-daisy rosettes on the diagonals (review 1 polish 3): each petal is a small pit, so its
        # upper-left wall is in shade and its lower-right wall catches the light
        dark, light = [], []
        for i in range(4):
            a = i * 90 - 45
            x, y = pt((R_MID + R_OUT) / 2, a)
            for j in range(6):
                ex, ey = x + 1.35 * math.cos(math.radians(j * 60)), y + 1.35 * math.sin(math.radians(j * 60))
                dark.append(f'<ellipse cx="{f(ex - .18)}" cy="{f(ey - .24)}" rx="1.05" ry=".5" transform="rotate({j * 60} {f(ex)} {f(ey)})"/>')
                light.append(f'<ellipse cx="{f(ex + .22)}" cy="{f(ey + .3)}" rx=".8" ry=".3" transform="rotate({j * 60} {f(ex)} {f(ey)})"/>')
        s.append(f'<g fill="{GILT_DARK}" opacity=".5">{"".join(dark)}</g><g fill="{GILT_SPEC}" opacity=".35">{"".join(light)}</g>')
        s.append(f'<path d="{ring(R_OUT - 1.2, R_OUT - 0.65)}" fill="{GILT_DARK}" fill-opacity=".35" fill-rule="evenodd"/>')
    return d, s


def maria(p, clip, blobs, sd, color=MOON_LOW, transform="", ext=128):
    """Smooth albedo shifts (lunar maria) inside a moon's clip: heavily blurred ellipses at low opacity, so the face
    gains tone without a single edge (no craters, no blotches)."""
    t = f' transform="{transform}"' if transform else ""
    els = "".join(f'<ellipse cx="{f(x)}" cy="{f(y)}" rx="{f(rx)}" ry="{f(ry)}" transform="rotate({f(a)} {f(x)} {f(y)})" fill-opacity="{o}"/>'
                  for x, y, rx, ry, a, o in blobs)
    return ([blur_filter(p, "mb", sd, ext)],
            [f'<g clip-path="url(#{clip})"><g filter="url(#{p}mb)"><g fill="{color}"{t}>{els}</g></g></g>'])


def crescent(p, cx, cy, r, k, rot, body=(MOON_MID, MOON, MOON_HI), soft=0.8, term=MOON_LOW, limb=MOON_HI,
             mar=True, mar_op=1.0, shadow=None):
    """A crescent moon with hero detail: the lit body brightens from a soft terminator to the limb, a soft limb band and
    a soft terminator band (one terminator only), and the near-limb maria a young crescent really shows (Crisium,
    Fecunditatis, Nectaris), as blurred tone."""
    D, S = [], []
    dpath, tr = phase(cx, cy, r, k, "right", rot)
    t = f' transform="{tr}"' if tr else ""
    rx = abs(k) * r
    D.append(f'<linearGradient id="{p}cg" x1="{f(cx + rx * 0.6)}" y1="0" x2="{f(cx + r)}" y2="0" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{body[0]}"/><stop offset=".45" stop-color="{body[1]}"/><stop offset="1" stop-color="{body[2]}"/></linearGradient>')
    D.append(f'<clipPath id="{p}lc"><path d="{dpath}"{t}/></clipPath>')
    D.append(blur_filter(p, "sb", soft))
    fl = f' filter="url(#{shadow})"' if shadow else ""
    S.append(f'<path d="{dpath}" fill="url(#{p}cg)"{t}{fl}/>')
    if mar:
        blobs = [(cx + 0.72 * r, cy - 0.40 * r, 0.15 * r, 0.12 * r, 0, f(0.20 * mar_op)),
                 (cx + 0.68 * r, cy + 0.08 * r, 0.12 * r, 0.19 * r, 0, f(0.17 * mar_op)),
                 (cx + 0.52 * r, cy + 0.40 * r, 0.11 * r, 0.12 * r, 0, f(0.14 * mar_op)),
                 (cx + 0.50 * r, cy - 0.12 * r, 0.14 * r, 0.12 * r, 0, f(0.10 * mar_op))]
        dd, ss = maria(p, f"{p}lc", blobs, max(r * 0.06, 0.8), transform=tr)
        D += dd; S += ss
    S.append(f'<g clip-path="url(#{p}lc)"><g filter="url(#{p}sb)"><g{t}>'
             f'<path d="M{f(cx)} {f(cy - r)}A{f(rx)} {f(r)} 0 0 1 {f(cx)} {f(cy + r)}" fill="none" stroke="{term}" stroke-width="{f(r * 0.09)}" stroke-opacity=".8"/></g>'
             f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - r * 0.04)}" fill="none" stroke="{limb}" stroke-width="{f(r * 0.08)}"/></g></g>')
    return D, S


# ---------------- Ready and In journal: the moon road scene ----------------
GLYPH_MOON = (49.0, 42.0, 29.0, -0.18, 28.0)    # cx, cy, r, k, rotation (lit limb faces 28 deg below horizontal)
GLYPH_H = 80.0
GLYPH_LIT_C = centroid(*GLYPH_MOON[:4], "right", GLYPH_MOON[4], step=0.25, extent=128)
# where the sun is: the lit limb points 28 deg below the horizontal, so the sun is under the horizon at this x
SUN_X = GLYPH_MOON[0] + (GLYPH_H - GLYPH_MOON[1]) / math.tan(math.radians(GLYPH_MOON[4]))
# road rows (y, height, [(dx0, dx1, opacity)]) relative to the lit centroid's x. They foreshorten toward the horizon and
# brighten toward the viewer: the mirror point (H + (H - lit y)) falls at the bottom edge of the well. Lengths vary.
# Each dash is (dx0, dx1, opacity, height scale); every dash keeps an aspect of 4.5 or more (thin chips, never pebbles),
# and secondary chips are at most 0.8x their row's height.
GLYPH_ROWS = [
    (81.5, 1.2, [(-4.5, 4.0, .46, 1)]),
    (84.0, 1.6, [(-7.5, 4.5, .52, 1), (7.5, 13.5, .40, .7)]),
    (87.4, 2.0, [(-10.5, -1.0, .56, 1), (2.0, 10.5, .44, .8)]),
    (91.6, 2.5, [(-10.0, 10.0, .62, 1)]),
    (96.4, 3.0, [(-15.0, 0.5, .66, 1), (4.0, 14.0, .52, .6)]),
    (101.8, 3.5, [(-13.0, 13.0, .72, 1)]),
    (107.7, 4.0, [(-24.0, -14.5, .46, .45), (-11.0, 9.0, .76, 1), (12.0, 23.0, .58, .6)]),
    (113.8, 4.5, [(-15.5, 15.5, .74, 1)]),
]


def moon_scene(p, night=False):
    """Ready's scene (and In journal's quieter night version): sky over a calm sea, the crescent, and its road."""
    D, S = [], []
    H = GLYPH_H
    mx, my, mr, mk, rot = GLYPH_MOON
    cxr, cyr = GLYPH_LIT_C
    sky_t, sky_h, sea_h, sea_b = ((NIGHT_SKY_T, NIGHT_SKY_H, NIGHT_SEA_H, NIGHT_SEA_B) if night
                                  else (LAPIS_SKY_T, LAPIS_SKY_H, LAPIS_SEA_H, LAPIS_SEA_B))
    dd, base, over = well(p, sky_t, sky_h, span=(12, H)); D += dd; S += base
    D.append(f'<linearGradient id="{p}sea" x1="0" y1="{f(H)}" x2="0" y2="117" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{sea_h}"/><stop offset="1" stop-color="{sea_b}"/></linearGradient>')
    D.append(f'<clipPath id="{p}skc"><rect x="0" y="0" width="128" height="{f(H)}"/></clipPath>')
    D.append(f'<clipPath id="{p}sec"><rect x="0" y="{f(H)}" width="128" height="{f(128 - H)}"/></clipPath>')
    S.append(f'<g clip-path="url(#{p}wc)">')
    S.append(f'<rect x="0" y="{f(H)}" width="128" height="{f(128 - H)}" fill="url(#{p}sea)"/>')
    # Fresnel: near the horizon the sea mirrors the sky, so a band just under it carries the sky's horizon colour
    D.append(f'<linearGradient id="{p}sr" x1="0" y1="{f(H)}" x2="0" y2="{f(H + 12)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{sky_h}" stop-opacity="{".55" if not night else ".3"}"/><stop offset="1" stop-color="{sky_h}" stop-opacity="0"/></linearGradient>')
    S.append(f'<rect x="0" y="{f(H)}" width="128" height="12" fill="url(#{p}sr)"/>')
    if not night:
        # warm afterglow where the sun has set (under the lit limb's direction), and its reflection directly below it
        D.append(f'<radialGradient id="{p}ag" cx="{f(SUN_X)}" cy="{f(H)}" r="1" gradientUnits="userSpaceOnUse" '
                 f'gradientTransform="translate({f(SUN_X)} {f(H)}) scale(52 30) translate({f(-SUN_X)} {f(-H)})">'
                 f'<stop offset="0" stop-color="{AFTERGLOW}" stop-opacity=".55"/><stop offset=".55" stop-color="{AFTERGLOW}" stop-opacity=".16"/>'
                 f'<stop offset="1" stop-color="{AFTERGLOW}" stop-opacity="0"/></radialGradient>')
        S.append(f'<g clip-path="url(#{p}skc)"><rect width="128" height="{f(H)}" fill="url(#{p}ag)"/></g>')
        D.append(f'<radialGradient id="{p}agr" cx="{f(SUN_X)}" cy="{f(H)}" r="1" gradientUnits="userSpaceOnUse" '
                 f'gradientTransform="translate({f(SUN_X)} {f(H)}) scale(40 9) translate({f(-SUN_X)} {f(-H)})">'
                 f'<stop offset="0" stop-color="{AFTERGLOW}" stop-opacity=".3"/><stop offset="1" stop-color="{AFTERGLOW}" stop-opacity="0"/></radialGradient>')
        S.append(f'<g clip-path="url(#{p}sec)"><rect y="{f(H)}" width="128" height="20" fill="url(#{p}agr)"/></g>')
    # the moon's halo in the sky (cool bloom), then the moon
    D.append(f'<radialGradient id="{p}bl" cx="{f(cxr)}" cy="{f(cyr)}" r="40" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity="{".26" if not night else ".12"}"/>'
             f'<stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    S.append(f'<g clip-path="url(#{p}skc)"><rect width="128" height="{f(H)}" fill="url(#{p}bl)"/></g>')
    if night:
        S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#3A4A78" fill-opacity=".3"/>')   # earthshine
        dd, ss = crescent(p, mx, my, mr, mk, rot, body=(MOON_LOW, MOON_MID, MOON), term=MOON_DEEP, limb=MOON, mar_op=0.8)
    else:
        dd, ss = crescent(p, mx, my, mr, mk, rot, body=(MOON_MID, MOON, MOON_HI))
    D += dd; S += ss
    # horizon: a fine moonlit line, brightest where the road meets it
    D.append(f'<linearGradient id="{p}hz" x1="12" y1="0" x2="116" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".06"/>'
             f'<stop offset="{f((cxr - 12) / 104)}" stop-color="{MOON}" stop-opacity="{".7" if not night else ".4"}"/><stop offset="1" stop-color="{MOON}" stop-opacity=".06"/></linearGradient>')
    S.append(f'<rect x="10" y="{f(H - 0.5)}" width="108" height="1" fill="url(#{p}hz)"/>')
    # a soft moon column under the road, widest and brightest toward the mirror point at the bottom
    D.append(f'<radialGradient id="{p}col" cx="{f(cxr)}" cy="118" r="1" gradientUnits="userSpaceOnUse" '
             f'gradientTransform="translate({f(cxr)} 118) scale(22 40) translate({f(-cxr)} -118)">'
             f'<stop offset="0" stop-color="{MOON}" stop-opacity="{".26" if not night else ".12"}"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    S.append(f'<g clip-path="url(#{p}sec)"><rect y="{f(H)}" width="128" height="{f(128 - H)}" fill="url(#{p}col)"/></g>')
    # the road: ripple streaks in the moon's colour under the lit centroid, each lower row with a brighter crest glint
    road_col, crest_col, k = (MOON, MOON, 1.0) if not night else (MOON_MID, MOON_MID, 1.0)
    cores = []
    for y, rh, dashes in GLYPH_ROWS:
        for x0, x1, o, hs in dashes:
            h = rh * hs
            S.append(f'<path d="{streak(cxr + x0, cxr + x1, y, h)}" fill="{road_col}" fill-opacity="{f(o * k)}"/>')
            L = x1 - x0
            if h >= 2.5 and L >= 12:
                # a soft hot core, centred in the ripple: no edge of its own (supervisor round 1, fix 2)
                cores.append(f'<path d="{streak(cxr + x0 + L * .18, cxr + x1 - L * .18, y, h * .45, skew=.5)}" fill="{crest_col}" fill-opacity="{f(o * .3 * k)}"/>')
    D.append(blur_filter(p, "cb", .5))
    S.append(f'<g filter="url(#{p}cb)">{"".join(cores)}</g>')
    S.append('</g>')
    S += over
    return D, S


def ready():
    p = "r5r-"
    D, S = moon_scene(p)
    dd, ss = bezel(p); D += dd; S += ss
    dd, ss = badge(p, "open"); D += dd; S += ss
    return svg("Ready", D, S)


def in_journal():
    p = "r5j-"
    D, S = moon_scene(p, night=True)
    dd, ss = bezel(p); D += dd; S += ss
    # journal ribbon in Tide silk: its top wraps behind the medal along an arc concentric with it (r 66), it lies over the
    # rim and drops into the well; lit edge on the left (toward the light); a tight soft shadow down-right
    x0, w, y1 = 24.5, 17.5, 76.0
    ya = 64 - math.sqrt(66 ** 2 - (64 - x0) ** 2)
    yb = 64 - math.sqrt(66 ** 2 - (64 - x0 - w) ** 2)
    rib = f"M{f(x0)} {f(ya)}A66 66 0 0 1 {f(x0 + w)} {f(yb)}V{f(y1)}L{f(x0 + w / 2)} {f(y1 - 7)}L{f(x0)} {f(y1)}Z"
    D.append(f'<linearGradient id="{p}rb" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="{TIDE_HI}"/><stop offset=".3" stop-color="{TIDE}"/>'
             f'<stop offset="1" stop-color="{TIDE_DEEP}"/></linearGradient>')
    D.append(f'<clipPath id="{p}rc"><path d="{rib}"/></clipPath>')
    D.append(f'<filter id="{p}rs" x="-30%" y="-10%" width="160%" height="120%" color-interpolation-filters="sRGB">'
             f'<feDropShadow dx="1.2" dy="1.0" stdDeviation="1.1" flood-color="{KEY}" flood-opacity=".6"/></filter>')
    S.append(f'<g filter="url(#{p}rs)"><path d="{rib}" fill="url(#{p}rb)" stroke="{KEY}" stroke-width="2.2" stroke-linejoin="round" paint-order="stroke"/></g>')
    S.append(f'<g clip-path="url(#{p}rc)">'
             f'<path d="{ring(R_KEY, 70)}" fill="{TIDE_DEEP}" fill-rule="evenodd"/>'
             f'<path d="{ring(58.5, 60.5)}" fill="{TIDE_HI}" fill-opacity=".5" fill-rule="evenodd"/>'
             f'<path d="{ring(53, 56)}" fill="{KEY}" fill-opacity=".3" fill-rule="evenodd"/></g>')
    S.append(f'<g class="hero" opacity=".5"><path d="M{f(x0 + 2.4)} 14V{f(y1 - 5.5)}M{f(x0 + w - 2.4)} 14V{f(y1 - 4)}" stroke="{TIDE_HI}" stroke-width=".55" stroke-dasharray="1.6 1.2"/></g>')
    dd, ss = badge(p, "journal"); D += dd; S += ss
    return svg("In journal", D, S)


# ---------------- Ready on another job: the crescent at rest, plus the job badge ----------------
ROW_TIER = False
BADGE = dict(cx=95.0, cy=95.0, r_key=24.0, r_out=23.1, r_mid=21.5, r_in=19.9, icon=35.5)
# seat enamels per badge kind (light stop, dark stop); job badges use their role colour
SEAT = {"open": ("#4A72C4", "#1E3470"),      # Ready: deep lapis under a warm gilt lock
        "closed": ("#2E3A66", "#111832"),    # Blocked: night enamel under a cool, muted lock
        "journal": ("#5674B8", "#243C78")}   # In journal: Tide silk blue, the ribbon's colour


def job_png(job):
    return "data:image/png;base64," + base64.b64encode((JOBDIR / f"{job}.png").read_bytes()).decode()


def job_optical_offset(job):
    """Where the job glyph's optical centre sits in its own canvas, measured from its visible pixels (alpha > 0.5, so
    the soft glow is ignored): the mean of the alpha bounding-box centre and the alpha-weighted centroid. Returns the
    offset from the canvas centre as a fraction of the canvas size."""
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


LOCK_OY = -0.2   # one body position for both locks: the closed lock's optical centre (bbox -11.9..11, body-weighted)


def metal_defs(p, hi, mid, lo):
    """Shared defs for a raised-metal glyph in the job-glyph idiom: a face gradient lit from the upper left, and the
    soft dark under-glow the game's job glyphs carry (no ink keyline)."""
    return [f'<linearGradient id="{p}mf" x1="-12" y1="-14" x2="12" y2="12" gradientUnits="userSpaceOnUse">'
            f'<stop offset="0" stop-color="{hi}"/><stop offset=".5" stop-color="{mid}"/><stop offset="1" stop-color="{lo}"/></linearGradient>',
            f'<filter id="{p}ug" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">'
            f'<feDropShadow dx=".6" dy=".9" stdDeviation=".8" flood-color="{KEY}" flood-opacity=".5"/></filter>']


def bevel(p, n, shape, light, dark, w=1.0):
    """A 1-unit lit bevel on a shape's upper-left edges and a shaded one on its lower-right edges (raised metal)."""
    D = [f'<mask id="{p}{n}bl"><g fill="#fff">{shape}</g><g fill="#000" transform="translate({f(w * .7)} {f(w * .9)})">{shape}</g></mask>',
         f'<mask id="{p}{n}bd"><g fill="#fff">{shape}</g><g fill="#000" transform="translate({f(-w * .7)} {f(-w * .9)})">{shape}</g></mask>']
    S = [f'<rect x="-20" y="-20" width="40" height="40" fill="{light}" fill-opacity=".85" mask="url(#{p}{n}bl)"/>',
         f'<rect x="-20" y="-20" width="40" height="40" fill="{dark}" fill-opacity=".7" mask="url(#{p}{n}bd)"/>']
    return D, S


def lock_glyph(p, cx, cy, open_):
    """An original padlock as raised metal, lit from the upper left: a slab body with lit and shaded bevels, a
    cylinder-shaded shackle, and a recessed keyhole. Open (Ready): warm gilt, the shackle lifted so its free leg clears
    the body by 4.2 units. Closed (Blocked): cool, muted moonstone pewter. Both share one body position."""
    if open_:
        hi, mid, lo, spec, shade = "#F4DFA6", GILT_LIGHT, "#9A7A40", GILT_SPEC, "#5C4724"
        # left leg rises from the body, the arc is lifted 5, the free right leg ends 4.2 above the body (cap included)
        shackle = "M-4.6 0V-11A4.6 4.6 0 0 1 4.6 -11V-5.5"
    else:
        hi, mid, lo, spec, shade = "#B4C0DA", "#8292B8", "#4E5C82", "#DCE2EE", "#2E3858"
        shackle = "M-4.6 0V-6A4.6 4.6 0 0 1 4.6 -6V0"
    t = f'transform="translate({f(cx)} {f(cy + LOCK_OY)})"'
    D = metal_defs(p, hi, mid, lo)
    body_d = "M-7.5 1.9A1.9 1.9 0 0 1 -5.6 0H5.6A1.9 1.9 0 0 1 7.5 1.9V9.1A1.9 1.9 0 0 1 5.6 11H-5.6A1.9 1.9 0 0 1 -7.5 9.1Z"
    body = f'<path d="{body_d}"/>'
    key_d = "M0 3.1a1.55 1.55 0 0 1 .95 2.8L1.4 8.3H-1.4L-.95 5.9A1.55 1.55 0 0 1 0 3.1Z"
    key = f'<path d="{key_d}"/>'
    D.append(f'<mask id="{p}sm"><path d="{shackle}" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"/></mask>')
    dd, bv = bevel(p, "b", body, spec, shade); D += dd
    # keyhole recess: dark fill, and its lower-right inner wall catches the light (a 0.4-unit hairline)
    D.append(f'<mask id="{p}km"><g fill="#fff">{key}</g><g fill="#000" transform="translate(-.4 -.4)">{key}</g></mask>')
    S = [f'<g {t} filter="url(#{p}ug)">'
         # shackle: a cylinder; a light stripe on the side toward the upper left that follows the bend, a dark band
         # on the far side; the stroke's round cap gives the free leg a lit, rounded end
         f'<path d="{shackle}" fill="none" stroke="{mid}" stroke-width="2.6" stroke-linecap="round"/>'
         f'<g mask="url(#{p}sm)"><path d="{shackle}" fill="none" stroke="{shade}" stroke-opacity=".75" stroke-width="1.1" transform="translate(.75 .75)" stroke-linecap="round"/>'
         f'<path d="{shackle}" fill="none" stroke="{spec}" stroke-opacity=".9" stroke-width=".8" transform="translate(-.6 -.6)" stroke-linecap="round"/></g>'
         f'<path d="{body_d}" fill="url(#{p}mf)"/>' + "".join(bv) +
         f'<path d="{key_d}" fill="#0E0B08" fill-opacity=".88"/>'
         f'<rect x="-20" y="-20" width="40" height="40" fill="{spec}" fill-opacity=".8" mask="url(#{p}km)"/>'
         '</g>']
    return D, S


def book_glyph(p, cx, cy):
    """An original journal as raised gilt metal: an open book, two curved pages lit from the upper left with lit and
    shaded bevels, a darker metal board behind, incised text lines (a fine dark line with a light hairline on its
    lower-right side), an incised spine and a small silk ribbon."""
    D = metal_defs(p, "#F4DFA6", GILT_LIGHT, "#9A7A40")
    left = "M0 -5C-3 -7.4 -6.6 -7.9 -10 -6.7V7C-6.6 6 -3.2 6.5 0 8.8Z"
    right = "M0 -5C3 -7.4 6.6 -7.9 10 -6.7V7C6.6 6 3.2 6.5 0 8.8Z"
    board = "M-10.8 -6.4V8.1C-7.1 7 -3.4 7.6 0 10C3.4 7.6 7.1 7 10.8 8.1V-6.4L10 -6.7V7C6.6 6 3.2 6.5 0 8.8C-3.2 6.5 -6.6 6 -10 7V-6.7Z"
    pages = f'<path d="{left}"/><path d="{right}"/>'
    dd, bv = bevel(p, "p", pages, GILT_SPEC, "#5C4724", .9); D += dd
    lines = ("M-8.2 -3.9C-6 -4.6 -3.8 -4.3 -1.7 -3M-8.2 -1C-6 -1.7 -3.8 -1.4 -1.7 -.1M-8.2 1.9C-6 1.2 -3.8 1.5 -1.7 2.8"
             "M8.2 -3.9C6 -4.6 3.8 -4.3 1.7 -3M8.2 -1C6 -1.7 3.8 -1.4 1.7 -.1M8.2 1.9C6 1.2 3.8 1.5 1.7 2.8")
    t = f'transform="translate({f(cx)} {f(cy - 1.3)})"'
    S = [f'<g {t} filter="url(#{p}ug)">'
         f'<path d="{board}" fill="#7C6236"/>'
         f'<path d="{left}" fill="url(#{p}mf)"/><path d="{right}" fill="url(#{p}mf)"/>'
         f'<path d="{right}" fill="#33260F" fill-opacity=".12"/>' + "".join(bv) +
         # incised lines: a fine darker groove, its lower-right wall catching the light
         f'<path d="{lines}" fill="none" stroke="{GILT_SPEC}" stroke-opacity=".55" stroke-width=".3" transform="translate(.25 .3)" stroke-linecap="round"/>'
         f'<path d="{lines}" fill="none" stroke="#6A5228" stroke-opacity=".8" stroke-width=".5" stroke-linecap="round"/>'
         f'<path d="M.25 -4.7V8.9" stroke="{GILT_SPEC}" stroke-opacity=".5" stroke-width=".3"/><path d="M0 -4.9V8.7" stroke="#5C4724" stroke-width=".6"/>'
         f'<path d="M-.8 8.5V12.6L0 11.7L.8 12.6V8.5Z" fill="{TIDE_HI}"/><path d="M.8 8.5V12.6L0 11.7Z" fill="{TIDE}"/>'
         '</g>']
    return D, S


def badge(p, kind, job=None):
    """The one badge frame shared by Ready, Ready on another job, In journal and Blocked: a raised mini-medal in the
    lower right that breaks the medal's silhouette. Keyline disc, the medal's two-slope gilt ring, an enamel seat
    (role colour for jobs, a kind colour otherwise), and the content centred optically on the seat."""
    if ROW_TIER:
        return [], []          # below 32 px the medal is drawn without its badge (the row fallback)
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    hi, lo = ROLE[JOBS[job][1]] if kind == "job" else SEAT[kind]
    x0, y0 = cx - b["r_key"], cy - b["r_key"]
    x1, y1 = cx + b["r_key"], cy + b["r_key"]
    D = [f'<linearGradient id="{p}go" x1="{f(x0)}" y1="{f(y0)}" x2="{f(x1)}" y2="{f(y1)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{GILT_HI}"/><stop offset=".45" stop-color="{GILT}"/><stop offset="1" stop-color="{GILT_DEEP}"/></linearGradient>',
         f'<linearGradient id="{p}gi" x1="{f(x0)}" y1="{f(y0)}" x2="{f(x1)}" y2="{f(y1)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{GILT_DEEP}"/><stop offset=".55" stop-color="{GILT_MID}"/><stop offset="1" stop-color="{GILT_LIGHT}"/></linearGradient>',
         f'<radialGradient id="{p}en" cx="{f(cx - 5)}" cy="{f(cy - 6)}" r="{f(b["r_in"] * 1.5)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{hi}"/><stop offset="1" stop-color="{lo}"/></radialGradient>',
         f'<clipPath id="{p}ec"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}"/></clipPath>',
         f'<mask id="{p}em"><rect width="128" height="128" fill="#fff"/><circle cx="{f(cx + 1.4)}" cy="{f(cy + 1.9)}" r="{f(b["r_in"])}" fill="#000"/></mask>',
         blur_filter(p, "eb", 0.8),
         shadow_filter(p, sd="1.1", op=".65", dx=1.4, dy=1.9, name="bs")]
    s = [f'<g filter="url(#{p}bs)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_key"])}" fill="{KEY}"/></g>',
         f'<path d="{ring(b["r_mid"], b["r_out"], cx, cy)}" fill="url(#{p}go)" fill-rule="evenodd"/>',
         f'<path d="{ring(b["r_in"], b["r_mid"], cx, cy)}" fill="url(#{p}gi)" fill-rule="evenodd"/>',
         f'<path d="{ring(b["r_mid"] - .2, b["r_mid"] + .2, cx, cy)}" fill="{GILT_DARK}" fill-opacity=".45" fill-rule="evenodd"/>',
         f'<path d="{taper_arc(b["r_mid"] + .75, -170, -100, 1.2, cx, cy)}" fill="{GILT_SPEC}" fill-opacity=".9"/>',
         f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}" fill="url(#{p}en)"/>',
         f'<g clip-path="url(#{p}ec)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"] + 1)}" fill="{KEY}" fill-opacity=".5" mask="url(#{p}em)" filter="url(#{p}eb)"/></g>']
    if kind == "job":
        name, role, icon_id = JOBS[job]
        ox, oy = job_optical_offset(job)
        sz = b["icon"]
        s.append(f'<image href="{job_png(job)}" x="{f(cx - sz / 2 - ox * sz)}" y="{f(cy - sz / 2 - oy * sz)}" width="{f(sz)}" height="{f(sz)}" '
                 f'preserveAspectRatio="xMidYMid meet" clip-path="url(#{p}ec)"><title>{name} (game icon {icon_id:06d})</title></image>')
    elif kind in ("open", "closed"):
        dd, ss = lock_glyph(p, cx, cy, kind == "open"); D += dd; s += ss
    else:
        dd, ss = book_glyph(p, cx, cy); D += dd; s += ss
    return D, s


def ready_other_job(job=DEFAULT_JOB):
    p = f"r5o{job[:3]}-"
    D, S = [shadow_filter(p)], []
    dd, base, over = well(p); D += dd; S += base + over
    mx, my, mr, mk, rot = GLYPH_MOON
    # the same crescent as Ready, at the same place and tilt, resting at night: no sea and no road (the road opens
    # when you are on that job). Earthshine keeps the disc readable.
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#2A3966" fill-opacity=".3"/>')
    dd, ss = crescent(p, mx, my, mr, mk, rot, body=(MOON_MID, MOON, MOON_HI), shadow=f"{p}ds")
    D += dd; S += ss
    dd, ss = bezel(p); D += dd; S += ss
    dd, ss = badge(p, "job", job); D += dd; S += ss
    return svg(f"Ready on another job: {JOBS[job][0]}", D, S)


# ---------------- Blocked: clouds drift across the moon ----------------

def cloud_markup(bumps, base):
    """A cumulus silhouette: billows (circles) on a flat, rounded base bar. Same fill for all parts = their union."""
    x0, x1, yt, yb = base
    return ("".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}"/>' for x, y, r in bumps)
            + f'<rect x="{f(x0)}" y="{f(yt)}" width="{f(x1 - x0)}" height="{f(yb - yt)}" rx="{f((yb - yt) / 2)}"/>')


def cloud(p, n, bumps, base, tone=1.0, lining=None, lit=1.0):
    """A raised cloud in relief, lit by the key light: a body that is lighter on top, each billow rounded by its own
    upper-left highlight, a lit rim on its upper-left edges, a shaded rim on its lower-right edges, and a soft shadow cast
    down and to the right (onto the moon and the enamel)."""
    m = cloud_markup(bumps, base)
    x0, x1, yt, yb = base
    ytop = min(y - r for x, y, r in bumps)
    D = [f'<clipPath id="{p}{n}c">{m}</clipPath>',
         f'<linearGradient id="{p}{n}g" x1="0" y1="{f(ytop)}" x2="0" y2="{f(yb)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{CLOUD}"/><stop offset=".7" stop-color="{CLOUD_LOW}"/><stop offset="1" stop-color="{CLOUD_DEEP}"/></linearGradient>',
         f'<mask id="{p}{n}L"><g fill="#fff">{m}</g><g fill="#000" transform="translate(.75 1.0)">{m}</g></mask>',
         f'<mask id="{p}{n}S"><g fill="#fff">{m}</g><g fill="#000" transform="translate(-1.4 -1.9)">{m}</g></mask>']
    hl = []
    for i, (x, y, r) in enumerate(bumps):
        D.append(f'<radialGradient id="{p}{n}h{i}" cx="{f(x - .35 * r)}" cy="{f(y - .42 * r)}" r="{f(r * 1.05)}" gradientUnits="userSpaceOnUse">'
                 f'<stop offset="0" stop-color="{CLOUD_HI}" stop-opacity="{f(.62 * tone * lit)}"/><stop offset="1" stop-color="{CLOUD_HI}" stop-opacity="0"/></radialGradient>')
        hl.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="url(#{p}{n}h{i})"/>')
    S = [f'<g fill="{KEY}" fill-opacity=".55" filter="url(#{p}cs)" transform="translate(1.7 2.3)">{m}</g>']
    if lining:
        # a faint cool silver lining where the cloud's edge crosses the moon's *lit* part (a 0.6-unit ring just
        # outside the cloud's outline, clipped to `lining`: markup for the lit area, never the unlit disc)
        D.append(f'<filter id="{p}{n}sl" x="-10%" y="-20%" width="120%" height="140%" color-interpolation-filters="sRGB">'
                 f'<feMorphology in="SourceAlpha" operator="dilate" radius=".6" result="d"/><feComposite in="d" in2="SourceAlpha" operator="out" result="r"/>'
                 f'<feFlood flood-color="#DCE2EE" flood-opacity=".25"/><feComposite in2="r" operator="in"/></filter>')
        D.append(f'<clipPath id="{p}{n}mc">{lining}</clipPath>')
        S.append(f'<g clip-path="url(#{p}{n}mc)"><g filter="url(#{p}{n}sl)">{m}</g></g>')
    S += [
         f'<g fill="url(#{p}{n}g)">{m}</g>',
         f'<g clip-path="url(#{p}{n}c)">{"".join(hl)}</g>',
         f'<rect width="128" height="128" fill="{CLOUD_DEEP}" fill-opacity=".75" mask="url(#{p}{n}S)"/>',
         f'<rect width="128" height="128" fill="{CLOUD_HI}" fill-opacity="{f(.7 * lit)}" mask="url(#{p}{n}L)" filter="url(#{p}rb)"/>']
    return D, S


def full_moon(p, cx, cy, r, stops=("#EEF1F7", "#C9D2E6", "#9AA8C8"), mar_op=1.0, limb_op=.95, shadow=True):
    """A full moon with hero detail: a body that follows the key light (upper left to lower right), the maria as
    smooth blurred tone in their real layout (Imbrium, Serenitatis, Tranquillitatis, Crisium, Fecunditatis, Nectaris,
    Nubium, Procellarum), and a key-lit limb band that fades toward the lower right."""
    D = [f'<radialGradient id="{p}fc" cx="{f(cx - .38 * r)}" cy="{f(cy - .42 * r)}" r="{f(1.55 * r)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{stops[0]}"/><stop offset=".55" stop-color="{stops[1]}"/><stop offset="1" stop-color="{stops[2]}"/></radialGradient>',
         f'<clipPath id="{p}fcl"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}"/></clipPath>',
         f'<linearGradient id="{p}lr" x1="0.2" y1="0.15" x2="0.8" y2="0.9"><stop offset="0" stop-color="{MOON_HI}" stop-opacity="{limb_op}"/>'
         f'<stop offset=".5" stop-color="{MOON_MID}" stop-opacity=".45"/><stop offset="1" stop-color="{MOON_MID}" stop-opacity="0"/></linearGradient>']
    fl = f' filter="url(#{p}ds)"' if shadow else ""
    S = [f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="url(#{p}fc)"{fl}/>']
    u = r
    blobs = [(cx - .30 * u, cy - .36 * u, .30 * u, .22 * u, -18, f(.36 * mar_op)),    # Imbrium
             (cx + .12 * u, cy - .30 * u, .17 * u, .15 * u, 0, f(.34 * mar_op)),     # Serenitatis
             (cx + .28 * u, cy - .02 * u, .22 * u, .17 * u, 20, f(.32 * mar_op)),    # Tranquillitatis
             (cx + .70 * u, cy - .18 * u, .10 * u, .08 * u, 0, f(.36 * mar_op)),     # Crisium
             (cx + .56 * u, cy + .24 * u, .10 * u, .17 * u, -15, f(.28 * mar_op)),   # Fecunditatis
             (cx + .26 * u, cy + .36 * u, .09 * u, .10 * u, 0, f(.24 * mar_op)),     # Nectaris
             (cx - .22 * u, cy + .30 * u, .17 * u, .13 * u, 10, f(.24 * mar_op)),    # Nubium
             (cx - .58 * u, cy - .02 * u, .22 * u, .36 * u, 8, f(.26 * mar_op))]     # Procellarum
    dd, ss = maria(p, f"{p}fcl", blobs, max(r * .07, .8)); D += dd; S += ss
    D.append(blur_filter(p, "lb", r * .022))
    S.append(f'<g clip-path="url(#{p}fcl)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - r * .05)}" fill="none" stroke="url(#{p}lr)" stroke-width="{f(r * .09)}" filter="url(#{p}lb)"/></g>')
    return D, S


BLOCKED_MOON = (72.0, 44.0, 25.0)
DIM_MOON = ("#BAC4DA", "#8F9CBB", "#64729A")       # round 5: the moon behind cloud is a step darker than round 4
BACK_CLOUD = ([(45.0, 34.0, 6.5), (55.5, 28.5, 9.0), (66.5, 31.0, 8.0), (77.0, 34.5, 6.0)], (38.0, 84.0, 31.0, 41.0))
FRONT_CLOUD = ([(24.0, 70.0, 9.0), (37.0, 60.0, 12.5), (53.0, 55.0, 13.5), (68.5, 58.0, 11.5), (81.0, 64.0, 9.0), (91.5, 69.5, 6.5)],
               (15.0, 98.0, 62.0, 78.0))


BLOCKED_LIMB = (-0.62, -12.0)                    # the new moon's thin sunlit limb: terminator and tilt
CLOUD_LIT = 0.75                                  # cloud highlights and lit rims at 75% (lit tops about 6% darker in L; more breaks the 16 px guard)


def blocked():
    """Blocked (round 5, variant b, chosen by the supervisor and the critic): the moon is there but you can't reach it.
    A new moon (an ashen, earthlit disc with only a thin sunlit limb) behind two cloud banks, the clouds' lit tops held
    down because a new moon is the only light in the sky, and one thin translucent wisp breaking the limb a third of
    the way down, so the sliver is glimpsed rather than shown. The closed-lock badge sits in the lower right."""
    p = "r5b-"
    D, S = [shadow_filter(p), blur_filter(p, "cs", 1.3), blur_filter(p, "rb", .3)], []
    dd, base, over = well(p); D += dd; S += base + over
    mx, my, mr = BLOCKED_MOON
    D.append(f'<radialGradient id="{p}as" cx="{f(mx - 8)}" cy="{f(my - 9)}" r="{f(mr * 1.5)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="#4A5682"/><stop offset="1" stop-color="#252E52"/></radialGradient>')
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="url(#{p}as)" filter="url(#{p}ds)"/>')
    dd, ss = crescent(p, mx, my, mr, *BLOCKED_LIMB, body=(MOON_LOW, MOON_MID, MOON), term=MOON_DEEP, limb=MOON, mar=False)
    D += dd; S += ss
    dpath, tr = phase(mx, my, mr, BLOCKED_LIMB[0], "right", BLOCKED_LIMB[1])
    limb = f'<path d="{dpath}" transform="{tr}"/>'      # silver linings only where the sunlit limb is behind
    # the wisp: thin stratus, translucent (no key-lit top, no shadow), drawn *under* the small bank so it emerges from
    # the bank's lower right and trails across the limb a third of the way down. Its edges are soft (blur .5, sRGB) so
    # it reads as vapour, not a slash, and it fades to nothing within 3 units outside the lit limb. A faint lining sits
    # on its upper edge only where it crosses the lit limb. (Supervisor fix 9.)
    wisp = tapered_curve((64.0, 38.0), (82.0, 33.0), (98.5, 35.0), 2.4, head=.5)
    limb_x = mx + math.sqrt(mr ** 2 - (35.0 - my) ** 2)      # the lit limb's outer edge at the wisp's height
    D.append(blur_filter(p, "wb", .5))
    D.append(f'<linearGradient id="{p}wf" x1="{f(limb_x)}" y1="0" x2="{f(limb_x + 3)}" y2="0" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="#fff"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>')
    D.append(f'<mask id="{p}wfm"><rect width="128" height="128" fill="url(#{p}wf)"/></mask>')
    D.append(f'<clipPath id="{p}wlc">{limb}</clipPath>')
    D.append(f'<mask id="{p}wm"><path d="{wisp}" fill="#fff" transform="translate(0 -.7)"/><path d="{wisp}" fill="#000"/></mask>')
    S.append(f'<g mask="url(#{p}wfm)"><g filter="url(#{p}wb)"><path d="{wisp}" fill="#46507A" fill-opacity=".55"/>'
             f'<g clip-path="url(#{p}wlc)"><rect width="128" height="128" fill="#DCE2EE" fill-opacity=".25" mask="url(#{p}wm)"/></g></g></g>')
    dd, ss = cloud(p, "a", *BACK_CLOUD, tone=.9, lining=limb, lit=CLOUD_LIT); D += dd; S += ss
    dd, ss = cloud(p, "b", *FRONT_CLOUD, lining=limb, lit=CLOUD_LIT); D += dd; S += ss
    dd, ss = bezel(p); D += dd; S += ss
    dd, ss = badge(p, "closed"); D += dd; S += ss
    return svg("Blocked", D, S)


# ---------------- Done this cycle: a moon that comes round again ----------------

def spiral_r(r0, r1, a0, a1, a):
    return r0 + (r1 - r0) * (a - a0) / (a1 - a0)


def repeat_arrow(cx, cy, r0, r1, a0, a1, w, head_len, head_w, n=90):
    """A repeat arrow that spirals outward clockwise from a0 (radius r0) to a1 (radius r1), screen degrees: its tail
    tapers in from a fine point and its head points on round. The spiral reads as motion and never as a second ring
    concentric with the rim (supervisor round 1, doubt 3)."""
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        a = a0 + (a1 - a0) * t
        rm = spiral_r(r0, r1, a0, a1, a)
        hw = w / 2 * (0.12 + 0.88 * min(t / 0.42, 1) ** 0.8)
        outer.append(pt(rm + hw, a, cx, cy))
        inner.append(pt(rm - hw, a, cx, cy))
    da = math.degrees(head_len / r1)
    head = [pt(r1 + head_w / 2, a1, cx, cy), pt(r1 + .4, a1 + da * .55, cx, cy), pt(r1, a1 + da, cx, cy),
            pt(r1 - .4, a1 + da * .55, cx, cy), pt(r1 - head_w / 2, a1, cx, cy)]
    return poly(outer + head + inner[::-1])


def spiral_taper(cx, cy, r0, r1, a0, a1, off, b0, b1, w, n=40):
    """A tapered highlight band riding the spiral at a radial offset, from angle b0 to b1."""
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        a = b0 + (b1 - b0) * t
        rm = spiral_r(r0, r1, a0, a1, a) + off
        hw = w / 2 * math.sin(math.pi * t)
        outer.append(pt(rm + hw, a, cx, cy))
        inner.append(pt(rm - hw, a, cx, cy))
    return poly(outer + inner[::-1])


def done():
    p = "r5d-"
    D, S = [shadow_filter(p)], []
    dd, base, over = well(p); D += dd; S += base + over
    mx, my, mr = 64.0, 64.0, 22.5
    # a waning half moon (lit on the left): this cycle's light is spent and will return
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#2B3966" filter="url(#{p}ds)"/>')
    dpath, tr = phase(mx, my, mr, 0.0, "left")
    D.append(f'<clipPath id="{p}hc"><path d="{dpath}" transform="{tr}"/></clipPath>')
    D.append(f'<linearGradient id="{p}hg" x1="{f(mx - mr)}" y1="{f(my - mr)}" x2="{f(mx)}" y2="{f(my + mr)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{MOON_HI}"/><stop offset=".55" stop-color="{MOON}"/><stop offset="1" stop-color="{MOON_MID}"/></linearGradient>')
    S.append(f'<path d="{dpath}" transform="{tr}" fill="url(#{p}hg)"/>')
    blobs = [(mx - .30 * mr, my - .36 * mr, .34 * mr, .26 * mr, -18, ".3"), (mx - .22 * mr, my + .30 * mr, .2 * mr, .16 * mr, 10, ".22"),
             (mx - .58 * mr, my - .02 * mr, .24 * mr, .38 * mr, 8, ".24"), (mx - .02 * mr, my - .30 * mr, .16 * mr, .17 * mr, 0, ".24")]
    dd, ss = maria(p, f"{p}hc", blobs, 1.8); D += dd; S += ss
    D.append(blur_filter(p, "tb", .7))
    S.append(f'<g clip-path="url(#{p}hc)"><g filter="url(#{p}tb)"><path d="M{f(mx)} {f(my - mr)}V{f(my + mr)}" stroke="{MOON_LOW}" stroke-width="2.4"/>'
             f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - .9)}" fill="none" stroke="{MOON_HI}" stroke-width="1.8"/></g></g>')
    # the repeat arrow: gilt, raised, sweeping clockwise round the moon from the lower left over the top to the lower
    # right, its head pointing on round. A wide gap at the bottom keeps it from closing into an orbit ring.
    r0, r1, a0, a1, w = 33.0, 39.0, 132.0, 385.0, 8.0
    arrow = repeat_arrow(64, 64, r0, r1, a0, a1, w, 13.5, 17.5)
    sp = lambda off, b0, b1, bw: spiral_taper(64, 64, r0, r1, a0, a1, off, b0, b1, bw)
    D.append(f'<linearGradient id="{p}ag" x1="20" y1="18" x2="104" y2="106" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{GILT_HI}"/>'
             f'<stop offset=".5" stop-color="{GILT_LIGHT}"/><stop offset="1" stop-color="{GILT}"/></linearGradient>')
    D.append(f'<clipPath id="{p}ac"><path d="{arrow}"/></clipPath>')
    S.append(f'<g filter="url(#{p}ds)"><path d="{arrow}" fill="url(#{p}ag)" stroke="{KEY}" stroke-width="1.3" stroke-linejoin="round" paint-order="stroke"/></g>')
    # bevel: the outer edge catches the light where it faces up-left, the inner edge where it faces down-right
    S.append(f'<g clip-path="url(#{p}ac)"><path d="{sp(w / 2 - .5, 175, 268, 1.6)}" fill="{GILT_SPEC}" fill-opacity=".9"/>'
             f'<path d="{sp(-w / 2 + .4, 320, 384, 1.1)}" fill="{GILT_SPEC}" fill-opacity=".4"/>'
             f'<path d="{sp(-w / 2 + .5, 175, 268, 1.4)}" fill="{GILT_DEEP}" fill-opacity=".55"/></g>')
    dd, ss = bezel(p); D += dd; S += ss
    return svg("Done this cycle", D, S)


# ---------------- Completed: the full moon, with the check ----------------

def completed(green=False):
    p = "r5g-" if green else "r5c-"
    D, S = [shadow_filter(p)], []
    dd, base, over = well(p); D += dd; S += base + over
    # Menphina's full moon, bright and detailed but held a step under Ready's lit lapis in value
    dd, ss = full_moon(p, 60.0, 60.0, 35.0, stops=("#D8DFED", "#A9B5D0", "#7B8AAF")); D += dd; S += ss
    dd, ss = bezel(p); D += dd; S += ss
    chk = "M64 86L78 100L117.5 40"
    if green:
        stops, spec, edge = (JADE_HI, JADE, JADE_LOW), JADE_SPEC, "#24402C"
    else:
        stops, spec, edge = (GILT_HI, GILT_LIGHT, "#A88B52"), GILT_SPEC, KEY
    D.append(f'<linearGradient id="{p}ck" x1="64" y1="40" x2="117" y2="100" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{stops[0]}"/>'
             f'<stop offset=".5" stop-color="{stops[1]}"/><stop offset="1" stop-color="{stops[2]}"/></linearGradient>')
    S.append(f'<g filter="url(#{p}ds)"><path d="{chk}" fill="none" stroke="{KEY}" stroke-width="15" stroke-linecap="round" stroke-linejoin="round"/>'
             f'<path d="{chk}" fill="none" stroke="{edge}" stroke-width="12.4" stroke-linecap="round" stroke-linejoin="round" stroke-opacity="{".0" if not green else ".9"}"/>'
             f'<path d="{chk}" fill="none" stroke="url(#{p}ck)" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/></g>')
    S.append(f'<path d="M62.3 83.3L76.5 97.4L115.1 38.3" fill="none" stroke="{spec}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" opacity=".85"/>')
    return svg("Completed" + (" (green check)" if green else ""), D, S)


# ---------------- Locked out: Dalamud, shattered ----------------

def ray_hit(ix, iy, ang, cx=64.0, cy=64.0, R=37.0):
    ux, uy = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    dx, dy = ix - cx, iy - cy
    b = dx * ux + dy * uy
    c = dx * dx + dy * dy - R * R
    t = -b + math.sqrt(b * b - c)
    return ix + ux * t, iy + uy * t


def locked_out():
    p = "r5l-"
    D, S = [shadow_filter(p, sd=".7", op=".7")], []
    dd, base, over = well(p); D += dd; S += base + over
    cx, cy, R = 64.0, 64.0, 37.5
    ix, iy = 59.0, 57.0                         # the point of impact, up and left of centre (no pizza spokes)
    angs = [-122.0, -64.0, -14.0, 31.0, 84.0, 141.0, 197.0]
    disp = [2.0, 2.8, 2.2, 2.6, 6.0, 2.4, 2.0]  # each shard pushed out along its own bisector; one is drifting away
    turn = [-1.5, 1.5, -1.0, 2.0, 9.0, -2.0, 1.5]
    jog = [1.8, -2.2, 2.0, -1.6, 2.4, -2.0, 1.6]  # each crack bends twice, so none is ruled
    n = len(angs)
    D.append(f'<linearGradient id="{p}rd" x1="{f(cx - R)}" y1="{f(cy - R)}" x2="{f(cx + R)}" y2="{f(cy + R)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{RED_HI}"/><stop offset=".55" stop-color="{RED}"/><stop offset="1" stop-color="{RED_DEEP}"/></linearGradient>')
    D.append(f'<linearGradient id="{p}lr" x1="{f(cx - R)}" y1="{f(cy - R)}" x2="{f(cx + R)}" y2="{f(cy + R)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{RED_EDGE}" stop-opacity=".75"/><stop offset=".45" stop-color="{RED_EDGE}" stop-opacity=".15"/><stop offset=".7" stop-color="{RED_EDGE}" stop-opacity="0"/></linearGradient>')
    D.append(f'<clipPath id="{p}dc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}"/></clipPath>')
    cracks = []
    for a, j in zip(angs, jog):
        e = ray_hit(ix, iy, a, cx, cy, R + 3)
        ux, uy = math.cos(math.radians(a)), math.sin(math.radians(a))
        L = math.hypot(e[0] - ix, e[1] - iy)
        m1 = (ix + ux * L * .38 - j * uy, iy + uy * L * .38 + j * ux)
        m2 = (ix + ux * L * .7 + j * .6 * uy, iy + uy * L * .7 - j * .6 * ux)
        cracks.append(((ix, iy), m1, m2, e))
    # the empty socket the moon sat in: a dark recess, its lower-right wall faintly lit
    S.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R + .4)}" fill="{CRACK}"/>')
    S.append(f'<path d="{taper_arc(R - .5, 15, 140, 1.8, cx, cy)}" fill="{RED_EDGE}" fill-opacity=".42"/>')
    body = []
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
        pts = [(ix, iy), c0[1], c0[2]] + arc + [c1[2], c1[1]]
        mid = math.radians((a0 + a1) / 2)
        tx, ty = disp[i] * math.cos(mid), disp[i] * math.sin(mid)
        gx = sum(x for x, _ in pts) / len(pts); gy = sum(y for _, y in pts) / len(pts)
        sid = f"{p}s{i}"
        D.append(f'<clipPath id="{sid}"><path d="{poly(pts)}"/></clipPath>')
        g = [f'<g clip-path="url(#{sid})"><g clip-path="url(#{p}dc)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="url(#{p}rd)"/>'
             f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R - 1.3)}" fill="none" stroke="url(#{p}lr)" stroke-width="2.6"/>']
        # fracture faces: an edge whose outward normal turns toward the key light catches it (a fine pale edge);
        # one turned away is in shade
        area = sum(pts[k][0] * pts[(k + 1) % len(pts)][1] - pts[(k + 1) % len(pts)][0] * pts[k][1] for k in range(len(pts)))
        sgn = 1 if area > 0 else -1
        for k in range(len(pts)):
            (x0, y0), (x1, y1) = pts[k], pts[(k + 1) % len(pts)]
            if math.hypot(x0 - cx, y0 - cy) > R + 1.5 and math.hypot(x1 - cx, y1 - cy) > R + 1.5:
                continue   # the limb arc: lit by the limb gradient instead
            dx, dy = x1 - x0, y1 - y0
            ln = math.hypot(dx, dy) or 1
            nx, ny = sgn * dy / ln, -sgn * dx / ln
            facing = nx * LIGHT_V[0] + ny * LIGHT_V[1]
            if facing > 0.15:
                g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{RED_EDGE}" stroke-width="{f(.9 + .9 * facing)}" stroke-opacity="{f(.4 + .4 * facing)}" stroke-linecap="round"/>')
            elif facing < -0.15:
                g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{KEY}" stroke-width="{f(.9 - .8 * facing)}" stroke-opacity=".55" stroke-linecap="round"/>')
        g.append('</g></g>')
        body.append(f'<g transform="translate({f(tx)} {f(ty)}) rotate({f(turn[i])} {f(gx)} {f(gy)})">{"".join(g)}</g>')
    S.append(f'<g clip-path="url(#{p}wc)"><g filter="url(#{p}ds)">{"".join(body)}</g></g>')
    dd, ss = bezel(p); D += dd; S += ss
    return svg("Locked out", D, S)


# ---------------- Not checked: a veiled moon whose sliver is a question mark ----------------

def not_checked():
    p = "r5n-"
    D, S = [shadow_filter(p)], []
    dd, base, over = well(p); D += dd; S += base + over
    vx, vy, vr = 64.0, 53.0, 21.0                 # the question mark's bowl is the veiled moon's lit limb
    # the veiled moon: the question mark's bowl is this moon's lit limb; the rest of its disc is only earthshine, a dim
    # body with a soft edge and faint maria, so the mark reads as a moon you can't yet make out
    D.append(f'<radialGradient id="{p}vm" cx="{f(vx - 4)}" cy="{f(vy - 5)}" r="{f(vr + 10)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="#4A5B90"/><stop offset="1" stop-color="#2A3866"/></radialGradient>')
    D.append(f'<clipPath id="{p}vc"><circle cx="{f(vx)}" cy="{f(vy)}" r="{f(vr + 4.5)}"/></clipPath>')
    D.append(blur_filter(p, "vb", .9))
    S.append(f'<circle cx="{f(vx)}" cy="{f(vy)}" r="{f(vr + 4.5)}" fill="url(#{p}vm)" fill-opacity=".9" filter="url(#{p}vb)"/>')
    blobs = [(vx - 7, vy - 6, 7.5, 5.5, -18, ".5"), (vx + 3, vy - 6, 4.5, 4, 0, ".4"), (vx - 9, vy + 6, 5.5, 7.5, 8, ".4")]
    dd, ss = maria(p, f"{p}vc", blobs, 1.6, color="#1E2A52"); D += dd; S += ss
    # centreline: the bowl runs clockwise round the veiled moon from its left horn, over the top, down the right, then
    # turns in to the stem
    pts, ws = [], []
    a0, a1 = 196.0, 398.0
    n = 48
    for i in range(n + 1):
        t = i / n
        a = a0 + (a1 - a0) * t
        pts.append(pt(vr, a, vx, vy))
        ws.append(13.0 * math.sin(math.pi / 2 * min(t / 0.55, 1)) ** 0.85 if t < 0.55 else 13.0 - 3.6 * (t - .55) / .45)
    ex, ey = pts[-1]
    tx, ty = -math.sin(math.radians(a1)), math.cos(math.radians(a1))
    P0, P1, P2, P3 = (ex, ey), (ex + tx * 7, ey + ty * 7), (vx, vy + vr + 3), (vx, vy + vr + 10)
    for i in range(1, 17):
        t = i / 16
        x = (1 - t) ** 3 * P0[0] + 3 * (1 - t) ** 2 * t * P1[0] + 3 * (1 - t) * t * t * P2[0] + t ** 3 * P3[0]
        y = (1 - t) ** 3 * P0[1] + 3 * (1 - t) ** 2 * t * P1[1] + 3 * (1 - t) * t * t * P2[1] + t ** 3 * P3[1]
        pts.append((x, y)); ws.append(9.4 - 0.5 * t)
    for i in range(1, 5):
        pts.append((vx, P3[1] + 1.2 * i)); ws.append(8.9)
    qm = tapered_path(pts, ws)
    end = pts[-1]
    qm_full = f'{qm}M{f(end[0] - 4.45)} {f(end[1])}a4.45 4.45 0 0 0 8.9 0Z'
    dot = (vx, end[1] + 14.5, 6.3)
    # moon-silver, lit like a crescent: brightest along the outer (limb) edge, a soft terminator on the inner edge,
    # and the key light falling across the whole mark from the upper left
    D.append(f'<linearGradient id="{p}qg" x1="{f(vx - vr)}" y1="{f(vy - vr - 6)}" x2="{f(vx + vr)}" y2="{f(dot[1])}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{MOON_HI}"/><stop offset=".5" stop-color="{MOON}"/><stop offset="1" stop-color="{MOON_LOW}"/></linearGradient>')
    D.append(f'<clipPath id="{p}qc"><path d="{qm_full}"/></clipPath>')
    D.append(blur_filter(p, "qb", 1.0))
    S.append(f'<g filter="url(#{p}ds)"><path d="{qm_full}" fill="url(#{p}qg)"/></g>')
    S.append(f'<g clip-path="url(#{p}qc)"><g filter="url(#{p}qb)"><circle cx="{f(vx)}" cy="{f(vy)}" r="{f(vr - 5.2)}" fill="none" stroke="{MOON_LOW}" stroke-width="3.2" stroke-opacity=".85"/>'
             f'<circle cx="{f(vx)}" cy="{f(vy)}" r="{f(vr + 5.2)}" fill="none" stroke="{MOON_HI}" stroke-width="2.2"/></g></g>')
    # the dot is a tiny full moon
    D.append(f'<radialGradient id="{p}dg" cx="{f(dot[0] - 2)}" cy="{f(dot[1] - 2.2)}" r="{f(dot[2] * 1.7)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{MOON_HI}"/><stop offset=".6" stop-color="{MOON_MID}"/><stop offset="1" stop-color="{MOON_LOW}"/></radialGradient>')
    S.append(f'<circle cx="{f(dot[0])}" cy="{f(dot[1])}" r="{f(dot[2])}" fill="url(#{p}dg)" filter="url(#{p}ds)"/>')
    dd, ss = bezel(p); D += dd; S += ss
    return svg("Not checked", D, S)


# ---------------- plugin icon ----------------
# ---- round 5 icon: the moon and its road on the centre axis, over Kugane's far shore ----
ICON_H = 245.0                                   # horizon a little above the middle (supervisor fix 7)
_IR, _IK, _IROT = 63.0, -0.22, 30.0              # crescent radius, terminator, tilt (lit limb faces 30 deg below level, as round 4)
_off = centroid(200.0, 200.0, _IR, _IK, "right", _IROT, step=0.5, extent=512)
_off = (_off[0] - 200.0, _off[1] - 200.0)        # lit-centroid offset from the disc centre
ICON_LIT_Y = 182.0                               # lit centroid height: the mirror row 2H - 182 = 308 sits above the Installed corner
ICON_MOON = (256.0 - _off[0], ICON_LIT_Y - _off[1], _IR, _IK, _IROT)   # lit centroid on x 256
ICON_LIT_C = centroid(*ICON_MOON[:4], "right", ICON_MOON[4], step=0.5, extent=512)
ROAD_IX = ICON_LIT_C[0]
BRIGHT_Y = ICON_H + (ICON_H - ICON_LIT_C[1])     # the mirror point: the road's brightest row
LANTERN_X = 72.0
INSTALLED = (248.0, 312.0, 488.0, 488.0)         # Dalamud's Installed check at 512 (x0, y0, x1, y1)


def _road_rows():
    """Road rows from the horizon to the bottom edge, symmetric about the moon's azimuth (x 256). Heights and gaps grow
    toward the viewer (height capped so every streak keeps an aspect of 4.5 or more). Opacity is high from the first
    rows (they carry the road at 32 px), peaks at the mirror point (y 308, above the Installed corner) and fades toward
    the viewer; that vertical fade, not any lateral bias, keeps the corner quiet. The lens is about the lit width at
    the horizon, widest (2.2x) at the mirror point and 15% narrower in front, +-12% jitter. Chips sit on both sides,
    alternating, their x staggered +-8 row to row so no column forms."""
    import random as _r
    rnd = _r.Random(11)
    lit_w = (1 + _IK) * _IR
    rows, y, i = [], ICON_H + 2.4, 0
    while y < 506:
        d = y - ICON_H
        dm = BRIGHT_Y - ICON_H
        if d <= dm:
            o = 0.60 + 0.22 * (d / dm) ** 0.6
            hw = lit_w / 2 * (1 + 1.2 * (d / dm) ** 0.7)
        else:
            t = (d - dm) / (512 - BRIGHT_Y)
            o = 0.82 - 0.46 * t ** 0.6
            hw = lit_w / 2 * 2.2 * (1 - 0.15 * t)
        hw *= 1 + rnd.uniform(-0.12, 0.12)
        shift = rnd.uniform(-0.08, 0.08) * hw
        h = min(1.7 + d * 0.06, hw * 2 * 0.8 / 4.8)
        x0, x1 = -hw * rnd.uniform(.76, .92) + shift, hw * rnd.uniform(.76, .92) + shift
        segs = []
        if h >= 5 and rnd.random() < 0.4:
            cut = rnd.uniform(-.25, .25) * hw
            g = rnd.uniform(5, 9)
            for a_, b_ in ((x0, cut - g / 2), (cut + g / 2, x1)):
                hh = min(h, (b_ - a_) / 4.6)
                if b_ - a_ > 10:
                    segs.append((a_, b_, hh, o))
        else:
            segs.append((x0, x1, h, o))
        if h >= 4:
            ch = max(h * 0.4, 1.5)
            stagger = 8 if i % 2 else -8
            sides = (-1, 1) if i % 3 == 0 else ((-1,) if i % 2 else (1,))
            for sd in sides:
                L = rnd.uniform(5.2, 7.5) * ch
                gap = rnd.uniform(5, 10) + 4 + stagger * sd * 0.5
                if sd < 0:
                    e = x0 - gap
                    segs.append((e - L, e, ch, o * 0.74))
                else:
                    b0 = x1 + gap
                    segs.append((b0, b0 + L, ch, o * 0.74))
            i += 1
        rows.append((y, segs))
        y += h * 1.45 + 2.6
    return rows


ROAD_ROWS = _road_rows()
# re-centre: glitter is symmetric about the moon's azimuth, so the light-weighted centre of all streaks sits on x 256
_wc = sum((a + b) / 2 * (b - a) * h * o for _, segs in ROAD_ROWS for a, b, h, o in segs) / sum((b - a) * h * o for _, segs in ROAD_ROWS for a, b, h, o in segs)
ROAD_ROWS = [(y, [(a - _wc, b - _wc, h, o) for a, b, h, o in segs]) for y, segs in ROAD_ROWS]
ROAD = [(ROAD_IX + a, ROAD_IX + b, y, h, o) for y, segs in ROAD_ROWS for a, b, h, o in segs]


def kugane_shore(H):
    """Kugane's far shore, in silhouette (after the official Stormblood art of Kugane: the castle's tiered tenshu over
    the bay, pagoda roofs, and the waterfront's lantern line). Left: rooftops and a five-storey pagoda. Right: Kugane
    Castle on its stone base, with rooftops. A gap in the middle leaves open horizon where the moon road meets it.
    Returns (far layer, near layer, lantern points)."""
    def roof(cx, y, w, h, lift=3.0):
        """One curved Far Eastern roof: eaves sweeping up at the tips (warabite)."""
        return (f"M{f(cx - w / 2 - lift)} {f(y - lift)}C{f(cx - w / 2 + 2)} {f(y)} {f(cx - w * .28)} {f(y - h * .35)} {f(cx - w * .16)} {f(y - h)}"
                f"H{f(cx + w * .16)}C{f(cx + w * .28)} {f(y - h * .35)} {f(cx + w / 2 - 2)} {f(y)} {f(cx + w / 2 + lift)} {f(y - lift)}Z")
    near = []
    # left waterfront: low rooftops (machiya) along the quay, then the pagoda
    near.append(f"M18 {f(H)}V{f(H - 6)}H40V{f(H - 9)}H58V{f(H - 7)}H76V{f(H - 11)}H92V{f(H - 8)}H200L206 {f(H)}Z")
    for cx, y, w, h in ((30, H - 6, 24, 6), (50, H - 9, 22, 6), (84, H - 11, 20, 6), (150, H - 8, 26, 7), (178, H - 8, 22, 6)):
        near.append(roof(cx, y, w, h, 2.2))
    px, base = 118.0, H - 8
    tiers = [(30, 8.5), (26, 8.5), (22.5, 8), (19, 7.5), (16, 7)]   # pagoda: five storeys, each roof narrower
    y = base
    for w, h in tiers:
        near.append(f"M{f(px - w * .3)} {f(y)}V{f(y - h * .45)}H{f(px + w * .3)}V{f(y)}Z")   # storey wall
        near.append(roof(px, y - h * .4, w, h * .6, 2.6))
        y -= h
    near.append(f"M{f(px - 1)} {f(y)}V{f(y - 13)}H{f(px + 1)}V{f(y)}Z")                    # sorin spire
    # right: Kugane Castle on a battered stone base, three roofs, a crowning gable; rooftops to the frame
    cx, base = 392.0, H - 6
    near.append(f"M306 {f(H)}L312 {f(H - 6)}H{f(cx - 34)}L{f(cx - 28)} {f(base - 12)}H{f(cx + 28)}L{f(cx + 34)} {f(H - 6)}H512V{f(H)}Z")
    y = base - 12
    for w, h in ((50, 15), (40, 13), (30, 12)):
        near.append(f"M{f(cx - w * .36)} {f(y)}V{f(y - h * .5)}H{f(cx + w * .36)}V{f(y)}Z")
        near.append(roof(cx, y - h * .45, w, h * .62, 3.4))
        y -= h
    near.append(roof(cx, y + 1, 18, 8, 2.4))
    near.append(f"M{f(cx - 7.5)} {f(y - 6)}l-2 -3.5l2.6 1.2ZM{f(cx + 7.5)} {f(y - 6)}l2 -3.5l-2.6 1.2Z")   # shachihoko finials
    for rx, ry, w, h in ((334, H - 6, 22, 6), (446, H - 7, 26, 7), (476, H - 6, 22, 6), (500, H - 7, 24, 6)):
        near.append(roof(rx, ry, w, h, 2.2))
    # far layer: the hills behind the city, paler (atmospheric perspective), framing the bay on both sides
    far = (f"M0 {f(H)}V{f(H - 22)}C24 {f(H - 30)} 44 {f(H - 26)} 66 {f(H - 21)}C92 {f(H - 16)} 140 {f(H - 19)} 170 {f(H - 12)}C188 {f(H - 8)} 204 {f(H - 3)} 214 {f(H)}Z"
           f"M300 {f(H)}C318 {f(H - 6)} 336 {f(H - 12)} 352 {f(H - 13)}C384 {f(H - 16)} 420 {f(H - 30)} 452 {f(H - 36)}C474 {f(H - 40)} 496 {f(H - 34)} 512 {f(H - 30)}V{f(H)}Z")
    lights = [(26, H - 3), (46, H - 4.5), (64, H - 3.5), (86, H - 5), (143, H - 3.5), (164, H - 4), (186, H - 3),
              (322, H - 3.5), (360, H - 4), (392, H - 21), (426, H - 4), (458, H - 3.5), (490, H - 4)]
    return far, "".join(near), lights


def icon():
    p = "r5i-"
    D, S = [], []
    H = ICON_H
    mx, my, mr, mk, rot = ICON_MOON
    D.append(f'<clipPath id="{p}t"><rect width="512" height="512" rx="112"/></clipPath>')
    D.append(f'<linearGradient id="{p}sky" x1="0" y1="0" x2="0" y2="{f(H)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#0C1330"/>'
             f'<stop offset=".6" stop-color="#1A2650"/><stop offset="1" stop-color="#33456F"/></linearGradient>')
    D.append(f'<linearGradient id="{p}sea" x1="0" y1="{f(H)}" x2="0" y2="512" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#2E3E68"/>'
             f'<stop offset=".1" stop-color="#1A2549"/><stop offset=".55" stop-color="#0E1530"/><stop offset="1" stop-color="#070B19"/></linearGradient>')
    D.append(f'<radialGradient id="{p}bl" cx="{f(ICON_LIT_C[0])}" cy="{f(ICON_LIT_C[1])}" r="170" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".14"/>'
             f'<stop offset=".5" stop-color="{MOON}" stop-opacity=".04"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    D.append(f'<radialGradient id="{p}blr" cx="{f(ROAD_IX)}" cy="{f(BRIGHT_Y)}" r="1" gradientUnits="userSpaceOnUse" '
             f'gradientTransform="translate({f(ROAD_IX)} {f(BRIGHT_Y)}) scale(120 170) translate({f(-ROAD_IX)} {f(-BRIGHT_Y)})">'
             f'<stop offset="0" stop-color="{MOON}" stop-opacity=".06"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    D.append(f'<radialGradient id="{p}col" cx="{f(ROAD_IX)}" cy="{f(BRIGHT_Y)}" r="1" gradientUnits="userSpaceOnUse" gradientTransform="translate({f(ROAD_IX)} {f(BRIGHT_Y)}) scale(62 190) translate({f(-ROAD_IX)} {f(-BRIGHT_Y)})">'
             f'<stop offset="0" stop-color="{MOON}" stop-opacity=".15"/><stop offset=".6" stop-color="{MOON}" stop-opacity=".05"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    D.append(f'<linearGradient id="{p}sr" x1="0" y1="{f(H)}" x2="0" y2="{f(H + 26)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#3A4C7A" stop-opacity=".32"/>'
             f'<stop offset="1" stop-color="#3A4C7A" stop-opacity="0"/></linearGradient>')
    D.append(f'<linearGradient id="{p}hz" x1="0" y1="0" x2="512" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".06"/>'
             f'<stop offset="{f(ROAD_IX / 512)}" stop-color="{MOON}" stop-opacity=".62"/><stop offset="1" stop-color="{MOON}" stop-opacity=".06"/></linearGradient>')
    D.append(f'<linearGradient id="{p}rim" x1="40" y1="20" x2="472" y2="492" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{GILT_HI}"/>'
             f'<stop offset=".42" stop-color="{GILT}"/><stop offset=".78" stop-color="{GILT_MID}"/><stop offset="1" stop-color="{GILT_DEEP}"/></linearGradient>')
    D.append(f'<linearGradient id="{p}rin" x1="40" y1="20" x2="472" y2="492" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{GILT_DEEP}"/>'
             f'<stop offset=".55" stop-color="{GILT_MID}"/><stop offset="1" stop-color="{GILT_LIGHT}"/></linearGradient>')
    D.append(f'<linearGradient id="{p}spec" x1="0" y1="0" x2="512" y2="512" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{GILT_SPEC}" stop-opacity=".95"/>'
             f'<stop offset=".35" stop-color="{GILT_SPEC}" stop-opacity=".2"/><stop offset=".6" stop-color="{GILT_SPEC}" stop-opacity="0"/></linearGradient>')
    D.append(f'<radialGradient id="{p}lg" cx="{f(LANTERN_X)}" cy="350" r="50" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{LANTERN_GOLD}" stop-opacity=".26"/>'
             f'<stop offset="1" stop-color="{LANTERN_GOLD}" stop-opacity="0"/></radialGradient>')
    D.append(f'<filter id="{p}fs" x="-10%" y="-10%" width="120%" height="120%" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="5"/></filter>')
    D.append(f'<mask id="{p}fm"><rect width="512" height="512" fill="#fff"/><rect x="{f(14 + 7)}" y="{f(14 + 10)}" width="484" height="484" rx="98" fill="#000"/></mask>')
    S.append(f'<g clip-path="url(#{p}t)">')
    S.append(f'<rect width="512" height="{f(H)}" fill="url(#{p}sky)"/><rect y="{f(H)}" width="512" height="{f(512 - H)}" fill="url(#{p}sea)"/>')
    S.append(f'<rect width="512" height="{f(H)}" fill="url(#{p}bl)"/>')
    S.append(f'<rect y="{f(H)}" width="512" height="26" fill="url(#{p}sr)"/>')
    S.append(f'<rect y="{f(H)}" width="512" height="{f(512 - H)}" fill="url(#{p}blr)"/>')
    # moon: earthshine disc, the crescent with its terminator-to-limb body, soft bands and near-limb maria
    dpath, tr = phase(mx, my, mr, mk, "right", rot)
    t = f' transform="{tr}"'
    rx = abs(mk) * mr
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#3A4A78" fill-opacity=".22"/>')
    D.append(f'<linearGradient id="{p}cg" x1="{f(mx + rx * .5)}" y1="0" x2="{f(mx + mr)}" y2="0" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{MOON_MID}"/><stop offset=".4" stop-color="{MOON}"/><stop offset="1" stop-color="{MOON_HI}"/></linearGradient>')
    S.append(f'<path d="{dpath}" fill="url(#{p}cg)"{t}/>')
    D.append(f'<clipPath id="{p}lc"><path d="{dpath}"{t}/></clipPath>')
    D.append(f'<filter id="{p}mbl" filterUnits="userSpaceOnUse" x="0" y="0" width="512" height="512" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="2.4"/></filter>')
    blobs = [(mx + 0.72 * mr, my - 0.40 * mr, 0.15 * mr, 0.12 * mr, 0, ".32"),
             (mx + 0.68 * mr, my + 0.08 * mr, 0.12 * mr, 0.19 * mr, 0, ".27"),
             (mx + 0.52 * mr, my + 0.40 * mr, 0.11 * mr, 0.12 * mr, 0, ".22"),
             (mx + 0.50 * mr, my - 0.12 * mr, 0.14 * mr, 0.12 * mr, 0, ".16")]
    dd, ss = maria(p, f"{p}lc", blobs, 3.6, transform=tr, ext=512); D += dd; S += ss
    S.append(f'<g clip-path="url(#{p}lc)"><g filter="url(#{p}mbl)"><g{t}><path d="M{f(mx)} {f(my - mr)}A{f(rx)} {f(mr)} 0 0 1 {f(mx)} {f(my + mr)}" fill="none" stroke="{MOON_LOW}" stroke-width="6" stroke-opacity=".7"/></g>'
             f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 2.2)}" fill="none" stroke="{MOON_HI}" stroke-width="4.4"/></g></g>')
    # horizon line (drawn before the shore so the land hides it), Kugane's far shore, and its dark mirror below
    HW = H + 1.5                             # the far shore's waterline sits just below the true horizon
    far, near, lights = kugane_shore(HW)
    # the horizon glint is far water behind the shore: draw it only in the open gap (supervisor fix 6)
    D.append(f'<clipPath id="{p}gap"><rect x="206" y="0" width="100" height="512"/></clipPath>')
    S.append(f'<rect x="0" y="{f(H - 1)}" width="512" height="2" fill="url(#{p}hz)" clip-path="url(#{p}gap)"/>')
    D.append(f'<path id="{p}far" d="{far}"/><path id="{p}near" d="{near}"/>')     # defined once, used five times
    S.append(f'<use href="#{p}far" fill="#1B2549"/><use href="#{p}near" fill="#0B1126"/>')
    # moonlight catches the shore's edges that face the moon (the castle's left faces and the pagoda's right faces are
    # turned toward it); kept to a faint hairline
    D.append(f'<clipPath id="{p}sc"><use href="#{p}near"/></clipPath>')
    D.append(f'<clipPath id="{p}sL"><rect width="256" height="512"/></clipPath><clipPath id="{p}sR"><rect x="256" width="256" height="512"/></clipPath>')
    for side, dx in (("L", -1.2), ("R", 1.2)):   # left shore: the moon is to its right; right shore: to its left
        S.append(f'<g clip-path="url(#{p}s{side})"><g clip-path="url(#{p}sc)"><rect width="512" height="{f(H)}" fill="{MOON}" fill-opacity=".2"/>'
                 f'<use href="#{p}near" fill="#0B1126" transform="translate({f(dx)} .8)"/></g></g>')
    D.append(f'<clipPath id="{p}mc"><rect x="0" y="{f(HW)}" width="512" height="70"/></clipPath>')
    S.append(f'<g clip-path="url(#{p}mc)"><g transform="translate(0 {f(2 * HW)}) scale(1 -1)" opacity=".3"><use href="#{p}far" fill="#0E1636"/><use href="#{p}near" fill="#070B1C"/></g></g>')
    # the lantern line: warm points on the waterfront, each with a short broken warm column directly below it
    lt = []
    for li, (lx, ly) in enumerate(lights):
        # a few chochin (red-orange paper lanterns, Kugane's night colour); each reflection takes its light's hue
        col = CHOCHIN if li in CHOCHIN_AT else LANTERN_GOLD
        lt.append(f'<circle cx="{f(lx)}" cy="{f(ly)}" r="1.5" fill="{col}" fill-opacity=".9"/>')
        dy0 = (HW - ly) + HW                     # the light's mirror image lies as far below the waterline as it is above
        for k, (yy, ww, oo) in enumerate(((HW + 1.6, 3.4, .5), (dy0 + 1.0, 4.6, .42), (dy0 + 5.0, 5.4, .3))):
            if yy < HW + 1.2:
                continue
            lt.append(f'<path d="M{f(lx - ww / 2)} {f(yy)}Q{f(lx)} {f(yy - 1.1)} {f(lx + ww / 2)} {f(yy)}Q{f(lx)} {f(yy + .8)} {f(lx - ww / 2)} {f(yy)}Z" fill="{col}" fill-opacity="{oo}"/>')
    S.append("".join(lt))
    # water texture: faint wind ripples (pale crest over dark trough), foreshortened; quieter in the Installed corner
    rnd = random.Random(7)
    needle = lambda x0, x1, yc, h: f"M{f(x0)} {f(yc)}Q{f((x0 + x1) / 2)} {f(yc - h)} {f(x1)} {f(yc)}Q{f((x0 + x1) / 2)} {f(yc + h * .7)} {f(x0)} {f(yc)}Z"
    crest_l, trough_l, crest_r, trough_r = [], [], [], []
    y = H + 6
    while y < 508:
        h = 0.8 + (y - H) / 250 * 3.2
        x = -20 + rnd.uniform(0, 40)
        while x < 520:
            L = rnd.uniform(26, 70) * (0.6 + (y - H) / 250 * 0.7)
            quiet = x >= INSTALLED[0] - 10 and y >= INSTALLED[1] - 10
            (crest_r if quiet else crest_l).append(needle(x, x + L, y, h * .5))
            (trough_r if quiet else trough_l).append(needle(x + 3, x + L + 3, y + h * .9, h * .4))
            x += L + rnd.uniform(18, 50)
        y += 4 + (y - H) / 250 * 14
    S.append(f'<path d="{"".join(crest_l)}" fill="#5A6C9C" fill-opacity=".16"/><path d="{"".join(trough_l)}" fill="#03060F" fill-opacity=".22"/>'
             f'<path d="{"".join(crest_r)}" fill="#5A6C9C" fill-opacity=".07"/><path d="{"".join(trough_r)}" fill="#03060F" fill-opacity=".1"/>')
    S.append(f'<rect x="0" y="{f(H)}" width="512" height="{f(512 - H)}" fill="url(#{p}col)"/>')
    cores = []
    for (x0, x1, y, h, o) in ROAD:
        S.append(f'<path d="{streak(x0, x1, y, h)}" fill="{MOON}" fill-opacity="{f(o)}"/>')
        L = x1 - x0
        if h >= 6 and L >= 26:
            cores.append(f'<path d="{streak(x0 + L * .18, x1 - L * .18, y, h * .45, skew=.5)}" fill="{MOON}" fill-opacity="{f(o * .35)}"/>')
    D.append(blur_filter(p, "cb", 2, 512))
    S.append(f'<g filter="url(#{p}cb)">{"".join(cores)}</g>')
    S.append(f'<g transform="translate({f(LANTERN_X)} 300) scale(.86) translate({f(-LANTERN_X)} -300)">{lantern()}</g>')
    S.append(f'<circle cx="{f(LANTERN_X)}" cy="350" r="50" fill="url(#{p}lg)"/>')
    for y, w, h, o in ((446, 12, 2.5, .40), (457, 16, 3, .52), (469, 22, 4, .62), (482, 28, 5, .70)):
        S.append(f'<path d="{streak(LANTERN_X - w / 2, LANTERN_X + w / 2, y, h, skew=0.5)}" fill="{LANTERN_GOLD}" fill-opacity="{o}"/>')
    S.append(f'<rect x="0" y="0" width="512" height="512" fill="{KEY}" fill-opacity=".55" mask="url(#{p}fm)" filter="url(#{p}fs)"/>')
    S.append('</g>')
    S.append(f'<rect x="4" y="4" width="504" height="504" rx="108" fill="none" stroke="{KEY}" stroke-width="8"/>')
    S.append(f'<rect x="8.5" y="8.5" width="495" height="495" rx="103.5" fill="none" stroke="url(#{p}rim)" stroke-width="7"/>')
    S.append(f'<rect x="14.5" y="14.5" width="483" height="483" rx="97.5" fill="none" stroke="url(#{p}rin)" stroke-width="5"/>')
    S.append(f'<rect x="12" y="12" width="488" height="488" rx="100" fill="none" stroke="{GILT_DARK}" stroke-opacity=".5" stroke-width="1"/>')
    S.append(f'<rect x="17.5" y="17.5" width="477" height="477" rx="94.5" fill="none" stroke="{KEY}" stroke-opacity=".8" stroke-width="1.5"/>')
    S.append(f'<rect x="7.5" y="7.5" width="497" height="497" rx="104.5" fill="none" stroke="url(#{p}spec)" stroke-width="2.2"/>')
    S.append(filigree())
    return svg("Tsukimichi", D, S, 512)


def lantern():
    """A Hingashi-style stone toro: hoju finial, kasa roof with upturned corners, hibukuro firebox with a lit window,
    chudai platform, sao post and kiso base, on a rock islet. Moonlit edges face the moon (upper right); the flame lights
    the window jambs and a pool on the islet in front of the base; the islet's reflection lies directly below it."""
    g = "#060914"
    rim = f'stroke="{MOON}" stroke-opacity=".34" stroke-width="2" fill="none" stroke-linecap="round"'
    bx = 0.0
    P = []
    islet = "M-70 454C-60 440 -30 431 0 431C18 429 30 433 36 442C38 446 39.5 450 40 454Z"
    P.append(f'<path d="{islet}" fill="{g}"/>')
    P.append(f'<path d="M-70 455H40C32 466 18 476 0 477C-26 478 -52 470 -70 455Z" fill="{g}" fill-opacity=".5"/>')
    P.append(f'<path d="M{f(bx + 20)} 431C{f(bx + 28)} 432 {f(bx + 34)} 436 {f(bx + 38)} 441" stroke="{MOON}" stroke-opacity=".22" stroke-width="2" fill="none" stroke-linecap="round"/>')
    P.append(f'<clipPath id="r5i-ic"><path d="{islet}"/></clipPath>')
    P.append(f'<radialGradient id="r5i-pool"><stop offset="0" stop-color="{LANTERN_GOLD}" stop-opacity=".32"/><stop offset="1" stop-color="{LANTERN_GOLD}" stop-opacity="0"/></radialGradient>')
    P.append(f'<ellipse cx="0" cy="441.5" rx="22" ry="2.6" fill="url(#r5i-pool)" clip-path="url(#r5i-ic)"/>')
    P.append(f'<path d="M{f(bx - 27)} 436H{f(bx + 27)}L{f(bx + 21)} 424H{f(bx - 21)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 8)} 424H{f(bx + 8)}L{f(bx + 6.5)} 388H{f(bx - 6.5)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 25)} 388H{f(bx + 25)}L{f(bx + 20)} 378H{f(bx - 20)}Z" fill="{g}"/>')
    P.append(f'<rect x="{f(bx - 19)}" y="340" width="38" height="38" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 46)} 330C{f(bx - 40)} 331 {f(bx - 36)} 327 {f(bx - 33)} 324C{f(bx - 26)} 318 {f(bx - 22)} 312 {f(bx - 17)} 305H{f(bx + 17)}'
             f'C{f(bx + 22)} 312 {f(bx + 26)} 318 {f(bx + 33)} 324C{f(bx + 36)} 327 {f(bx + 40)} 331 {f(bx + 46)} 330L{f(bx + 41)} 341H{f(bx - 41)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 7)} 305C{f(bx - 9)} 298 {f(bx - 6)} 291 {f(bx)} 284C{f(bx + 6)} 291 {f(bx + 9)} 298 {f(bx + 7)} 305Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx + 17)} 305C{f(bx + 22)} 312 {f(bx + 26)} 318 {f(bx + 33)} 324C{f(bx + 36)} 327 {f(bx + 40)} 331 {f(bx + 46)} 330" {rim}/>')
    P.append(f'<path d="M{f(bx + 19)} 343V376M{f(bx + 7)} 391V422M{f(bx + 5)} 295C{f(bx + 7)} 299 {f(bx + 7.5)} 302 {f(bx + 6.5)} 304M{f(bx + 21)} 380L{f(bx + 25)} 387" {rim}/>')
    P.append(f'<rect x="{f(bx - 11)}" y="348" width="22" height="23" rx="2" fill="{LANTERN_GOLD}"/>')
    P.append(f'<path d="M{f(bx)} 348V371" stroke="{g}" stroke-width="3"/>')
    P.append(f'<path d="M{f(bx - 12)} 348V372H{f(bx + 12)}V348" fill="none" stroke="{LANTERN_GOLD}" stroke-opacity=".5" stroke-width="1"/>')
    return f'<g transform="translate({f(LANTERN_X)} 0)">' + "".join(P) + '</g>'


def filigree():
    out = []
    for sx, tx in ((1, 0), (-1, 512)):
        tr = f'transform="translate({tx} 0) scale({sx} 1)"'
        out.append(f'<g {tr} fill="none" stroke="{GILT_SPEC}" stroke-opacity=".28" stroke-width="2" stroke-linecap="round">'
                   f'<path d="M30 110C30 64 64 30 110 30"/>'
                   f'<path d="M30 116a6 6 0 1 0 0 12a4.5 4.5 0 1 1 0 -12Z" fill="{GILT_SPEC}" fill-opacity=".28" stroke="none"/>'
                   f'<path d="M116 30a6 6 0 1 0 12 0a4.5 4.5 0 1 1 -12 0Z" fill="{GILT_SPEC}" fill-opacity=".28" stroke="none"/></g>')
    return "".join(out)




def write_all():
    write("ready.svg", ready())
    for job in JOBS:
        write(f"ready-on-another-job-{job}.svg", ready_other_job(job))
    write("ready-on-another-job.svg", ready_other_job(DEFAULT_JOB))
    write("in-journal.svg", in_journal())
    write("blocked.svg", blocked())
    write("done-this-cycle.svg", done())
    write("completed.svg", completed())
    write("locked-out.svg", locked_out())
    write("not-checked.svg", not_checked())
    write("plugin-icon.svg", icon())


if __name__ == "__main__":
    write_all()
    # the row tier (under 32 px): the same medals without their badges, for the plugin's small rows and for metrics
    ROW_TIER = True
    (OUT / "_row").mkdir(exist_ok=True)
    _out = OUT
    OUT = OUT / "_row"
    for name, fn in (("ready.svg", ready), ("ready-on-another-job.svg", ready_other_job), ("in-journal.svg", in_journal),
                     ("blocked.svg", blocked), ("done-this-cycle.svg", done), ("completed.svg", completed),
                     ("locked-out.svg", locked_out), ("not-checked.svg", not_checked)):
        write(name, fn())
    # the badge content alone, for drawing at text height beside a row-tier medal
    for kind in ("open", "closed", "journal"):
        dd, ss = (lock_glyph(f"r5k{kind[0]}-", 0, 0, kind == "open") if kind != "journal" else book_glyph("r5kj-", 0, 0))
        write(f"badge-{kind}.svg", f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="-17 -18 34 34" width="34" height="34"><title>{kind} badge glyph</title>'
                                   f'<defs>{"".join(dd)}</defs>{"".join(ss)}</svg>' + "\n")
    OUT = _out
    print("glyph lit centroid", GLYPH_LIT_C, "sun x", SUN_X, "icon lit centroid", ICON_LIT_C, "bright y", BRIGHT_Y)
    print("road x range", min(r[0] for r in ROAD), max(r[1] for r in ROAD), "glints", len(ROAD))
