"""Menphina's Medallion, round 3. Writes the 8 state SVGs and plugin-icon.svg into round3/medallion-r3.

One light for everything: a UI key light from the upper left (azimuth 135 deg, elevation about 45 deg).
Raised metal is bright on its upper-left slopes and dark on its lower-right ones; recesses are the reverse;
raised emblems cast a short soft shadow down and to the right. Inside the icon's scene the moon is the light.
"""
import math, pathlib
import numpy as np

OUT = pathlib.Path(__file__).resolve().parent.parent

# ---------------- palette tokens ----------------
KEY = "#080B16"                     # Abyss: keylines, grooves, shadow
# one metal for every rim and the icon frame: FFXIV-style champagne gilt, built on Moon Road's GiltHigh #D9BE82, with the
# base and mid stops set ~8 % below Moon Road's Gilt #A88B52 so the emblems, not the rims, lead (supervisor polish 1)
GILT_SPEC, GILT_HI, GILT_LIGHT, GILT, GILT_MID, GILT_DEEP, GILT_DARK = (
    "#FFF4D6", "#E6CF98", "#D9BE82", "#9A7E4A", "#7C6236", "#5C4724", "#33260F")
LAPIS_T, LAPIS_B, LAPIS_SEA, LAPIS_SEA_B = "#628EDC", "#5079C9", "#4067AF", "#2A4888"   # lit enamel (Ready only)
DUSK_T, DUSK_B = "#1D2B5A", "#131C40"                                               # resting enamel
MOON_HI, MOON, MOON_MID, MOON_LOW, MOON_DEEP = "#F4F2EA", "#E2E8F4", "#C3CEE4", "#95A5C8", "#5E6E97"
DARKSIDE = "#17224A"                # the unlit part of a moon disc (a shade darker than enamel)
ASH, ASH_LIMB = "#4C5779", "#DCE2EF"
SOUL_HI, SOUL, SOUL_MID, SOUL_DEEP = "#E3D6FF", "#A88CEB", "#7A5CC8", "#43307E"   # soul crystal (amethyst)
TIDE, TIDE_HI, TIDE_DEEP = "#6F8FD0", "#A9BEEA", "#3F5A98"                         # journal ribbon (Moon Road Tide)
RED_HI, RED, RED_DEEP = "#DA808A", "#BA5462", "#742C3C"                            # Dalamud, rose-biased toward Eclipse #B25C7F
CRACK = "#0B0408"
MIST, MIST_HI = "#A9B2CC", "#D3D9E8"
LANTERN_GOLD = "#E0B860"

C = 64.0
R_KEY, R_OUT, R_MID, R_IN, R_WELL = 63.2, 61.5, 57.2, 53.0, 52.4
LIGHT_DX, LIGHT_DY = 1.1, 1.6       # emblem shadow offset (down-right, away from the key light)


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


def inlay_band(cx, cy, r_out, a0, a1, apeak, wmax, wmin, n=48):
    """A band inlaid inside a disc: outer edge flush on the limb (r_out), inner edge at r_out - w(theta);
    w eases from wmin at a0 up to wmax at apeak and back to wmin at a1."""
    outer, inner = [], []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        t = (a - a0) / (apeak - a0) if a <= apeak else (a1 - a) / (a1 - apeak)
        w = wmin + (wmax - wmin) * (0.5 - 0.5 * math.cos(math.pi * t))
        outer.append(pt(r_out, a, cx, cy))
        inner.append(pt(r_out - w, a, cx, cy))
    return poly(outer + inner[::-1])


def sector(r0, r1, a0, a1):
    """Annular sector from angle a0 to a1 (clockwise in screen space, a1 > a0)."""
    large = 1 if (a1 - a0) % 360 > 180 else 0
    x0, y0 = pt(r1, a0); x1, y1 = pt(r1, a1); x2, y2 = pt(r0, a1); x3, y3 = pt(r0, a0)
    return (f"M{f(x0)} {f(y0)}A{f(r1)} {f(r1)} 0 {large} 1 {f(x1)} {f(y1)}L{f(x2)} {f(y2)}"
            f"A{f(r0)} {f(r0)} 0 {large} 0 {f(x3)} {f(y3)}Z")


def streak(x0, x1, yc, h, skew=0.44):
    """A horizontal ripple streak: a long needle of light, flat through the middle and drawn to fine points at both
    ends, with a flatter lower edge (a wave facet catching a low light). Aspect >= 3.5 so it never reads as a leaf."""
    L = x1 - x0
    top, bot = [], []
    n = 32
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


def svg(title, defs, body, vb=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {vb} {vb}" width="{vb}" height="{vb}"><title>{title}</title>'
            f'<defs>{"".join(defs)}</defs>{"".join(body)}</svg>\n')


def write(name, text):
    (OUT / name).write_text(text, encoding="utf-8")


# ---------------- shared medal parts ----------------

def shadow_filter(p):
    return (f'<filter id="{p}ds" x="-20%" y="-20%" width="140%" height="140%" color-interpolation-filters="sRGB">'
            f'<feDropShadow dx="{LIGHT_DX}" dy="{LIGHT_DY}" stdDeviation=".9" flood-color="{KEY}" flood-opacity=".6"/></filter>')


def well(p, top=DUSK_T, bot=DUSK_B, extra_clip="", span=None):
    """Enamel well: vertical enamel gradient, a soft shadow cast by the raised bezel on the upper-left inner edge,
    and a faint vitreous sheen toward the light."""
    gl = (f'x1="0" y1="{f(span[0])}" x2="0" y2="{f(span[1])}" gradientUnits="userSpaceOnUse"' if span else 'x1="0" y1="0" x2="0" y2="1"')
    d = [f'<linearGradient id="{p}wl" {gl}><stop offset="0" stop-color="{top}"/>'
         f'<stop offset="1" stop-color="{bot}"/></linearGradient>',
         f'<clipPath id="{p}wc"><circle cx="64" cy="64" r="{f(R_WELL)}"/>{extra_clip}</clipPath>',
         f'<radialGradient id="{p}sh" cx="44" cy="40" r="40" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fff" stop-opacity=".09"/>'
         f'<stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>',
         f'<filter id="{p}ib" x="-10%" y="-10%" width="120%" height="120%"><feGaussianBlur stdDeviation="1.3"/></filter>',
         f'<mask id="{p}im"><rect width="128" height="128" fill="#fff"/><circle cx="{f(64 + 2.6)}" cy="{f(64 + 3.6)}" r="{f(R_WELL)}" fill="#000"/></mask>']
    s = [f'<path d="M0 0H128V128H0Z" fill="url(#{p}wl)" clip-path="url(#{p}wc)"/>',
         f'<circle cx="64" cy="64" r="{f(R_WELL)}" fill="url(#{p}sh)"/>',
         f'<g clip-path="url(#{p}wc)"><circle cx="64" cy="64" r="{f(R_WELL + 1)}" fill="{KEY}" fill-opacity=".55" mask="url(#{p}im)" filter="url(#{p}ib)"/></g>']
    return d, s


def bezel(p, gap=None, hero=True):
    """The one medal bezel shared by all eight states: a rounded gilt rim lit from the upper left.
    Outer slope: light upper-left -> dark lower-right. Inner slope: the reverse. A specular streak on the crest at
    upper left, a weaker one on the inner slope at lower right. `gap` = (a0, a1) opens the rim (screen angles, deg)."""
    d = [f'<linearGradient id="{p}bo" x1="14" y1="10" x2="114" y2="118" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{GILT_HI}"/><stop offset=".42" stop-color="{GILT}"/><stop offset=".78" stop-color="{GILT_MID}"/>'
         f'<stop offset="1" stop-color="{GILT_DEEP}"/></linearGradient>',
         f'<linearGradient id="{p}bi" x1="14" y1="10" x2="114" y2="118" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{GILT_DEEP}"/><stop offset=".55" stop-color="{GILT_MID}"/><stop offset="1" stop-color="{GILT_LIGHT}"/></linearGradient>']
    if gap:
        a0, a1 = gap[1], gap[0] + 360
        ring = lambda r0, r1: sector(r0, r1, a0, a1)
    else:
        ring = lambda r0, r1: (f"M{f(64 + r1)} 64A{f(r1)} {f(r1)} 0 1 1 {f(64 - r1)} 64A{f(r1)} {f(r1)} 0 1 1 {f(64 + r1)} 64Z"
                               f"M{f(64 + r0)} 64A{f(r0)} {f(r0)} 0 1 0 {f(64 - r0)} 64A{f(r0)} {f(r0)} 0 1 0 {f(64 + r0)} 64Z")
    s = [f'<path d="{ring(R_IN - 0.9, R_KEY)}" fill="{KEY}" fill-opacity=".92" fill-rule="evenodd"/>',
         f'<path d="{ring(R_MID, R_OUT)}" fill="url(#{p}bo)" fill-rule="evenodd"/>',
         f'<path d="{ring(R_IN, R_MID)}" fill="url(#{p}bi)" fill-rule="evenodd"/>',
         # crest line between the two slopes: a fine dark seam that makes the rim read as turned metal
         f'<path d="{ring(R_MID - 0.25, R_MID + 0.25)}" fill="{GILT_DARK}" fill-opacity=".45" fill-rule="evenodd"/>',
         # specular streaks: strong on the outer slope at upper left, weak on the inner slope at lower right
         f'<path d="{taper_arc(R_MID + 2.3, -168, -102, 2.4)}" fill="{GILT_SPEC}" fill-opacity=".95"/>',
         f'<path d="{taper_arc(R_IN + 1.9, 18, 52, 1.6)}" fill="{GILT_SPEC}" fill-opacity=".45"/>']
    if hero:
        # engraved hairline on the outer slope and four small moon-daisy rosettes on the diagonals (Menphina's flower)
        g = []
        for i in range(4):
            a = i * 90 - 45
            x, y = pt((R_MID + R_OUT) / 2, a)
            for j in range(6):
                ex, ey = x + 1.35 * math.cos(math.radians(j * 60)), y + 1.35 * math.sin(math.radians(j * 60))
                g.append(f'<ellipse cx="{f(ex)}" cy="{f(ey)}" rx="1.1" ry=".55" transform="rotate({j * 60} {f(ex)} {f(ey)})"/>')
        s.append(f'<g fill="{GILT_DARK}" opacity=".45">{"".join(g)}</g>')
        s.append(f'<path d="{ring(R_OUT - 1.2, R_OUT - 0.65)}" fill="{GILT_DARK}" fill-opacity=".35" fill-rule="evenodd"/>')
    if gap:
        # clean radial end-faces where the rim opens, with a keyline
        for a in gap:
            x0, y0 = pt(R_IN - 0.9, a); x1, y1 = pt(R_KEY, a)
            s.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{KEY}" stroke-width="1.4" stroke-linecap="round"/>')
        # end faces: the right arm's face turns toward the upper-left light (lit hairline); the left arm's turns away
        a = gap[0] - 1.6
        x0, y0 = pt(R_IN + 0.3, a); x1, y1 = pt(R_OUT - 0.3, a)
        s.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{GILT_LIGHT}" stroke-width=".9" stroke-linecap="round"/>')
        a = gap[1] + 1.6
        x0, y0 = pt(R_IN + 0.3, a); x1, y1 = pt(R_OUT - 0.3, a)
        s.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{GILT_DARK}" stroke-width=".9" stroke-linecap="round" stroke-opacity=".8"/>')
    return d, s


def lit_body(p, cx, cy, r, k, side="right", rot=0, fill=MOON, limb=MOON_HI, term=MOON_MID, tw=3.0, lw=2.0, soft=0.0):
    """Moonstone lit body: body fill, a terminator band and a limb band (no ball gradient, no craters). With `soft`, both
    bands are blurred inside the lit clip so brightness falls off smoothly from limb to terminator (no rind)."""
    dpath, tr = phase(cx, cy, r, k, side, rot)
    t = f' transform="{tr}"' if tr else ""
    out = [f'<path d="{dpath}" fill="{fill}"{t}/>', f'<clipPath id="{p}lc"><path d="{dpath}"{t}/></clipPath>']
    if abs(k) > 1e-6:
        term_el = f'<ellipse cx="{f(cx)}" cy="{f(cy)}" rx="{f(abs(k) * r)}" ry="{f(r)}" fill="none" stroke="{term}" stroke-width="{f(tw)}"/>'
    else:
        term_el = f'<path d="M{f(cx)} {f(cy - r)}V{f(cy + r)}" stroke="{term}" stroke-width="{f(tw)}"/>'
    fl = ""
    if soft:
        out.append(f'<filter id="{p}sb" filterUnits="userSpaceOnUse" x="0" y="0" width="128" height="128"><feGaussianBlur stdDeviation="{f(soft)}"/></filter>')
        fl = f' filter="url(#{p}sb)"'
    out.append(f'<g clip-path="url(#{p}lc)"><g{fl}><g{t}>{term_el}</g>'
               f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - lw / 2)}" fill="none" stroke="{limb}" stroke-width="{f(lw)}"/></g></g>')
    return out


def hatch(p, cx, cy, r, side, color=MOON_LOW, op=".26", reach=0.6):
    """Hero-tier engraved hatching in a dark part, fading away from the terminator."""
    d = [f'<clipPath id="{p}hc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - 1.2)}"/></clipPath>',
         f'<linearGradient id="{p}hg" x1="{0 if side < 0 else 1}" y1="0" x2="{1 if side < 0 else 0}" y2="0">'
         f'<stop offset="{f(1 - reach)}" stop-color="#fff" stop-opacity="0"/><stop offset="1" stop-color="#fff"/></linearGradient>',
         f'<mask id="{p}hm"><rect x="{f(cx - r)}" y="{f(cy - r)}" width="{f(2 * r)}" height="{f(2 * r)}" fill="url(#{p}hg)"/></mask>']
    lines, t = [], -2 * r
    while t < 2 * r:
        lines.append(f"M{f(cx + t - r)} {f(cy + r)}L{f(cx + t + r)} {f(cy - r)}")
        t += 3.2
    s = [f'<g clip-path="url(#{p}hc)"><path d="{"".join(lines)}" stroke="{color}" stroke-width=".55" fill="none" '
         f'opacity=".15"/></g>']
    return d, s


# ---------------- the eight states ----------------
GLYPH_MOON = (51.0, 43.0, 27.5, -0.24, 28.0)    # Ready: cx, cy, r, k, rotation (lit limb faces 28 deg below horizontal)
GLYPH_H = 80.0
GLYPH_LIT_C = centroid(*GLYPH_MOON[:4], "right", GLYPH_MOON[4], step=0.25, extent=128)
READY_GAP = (58.0, 122.0)                        # bezel opening at the bottom, screen degrees


def ready():
    p = "r3r-"
    D, S = [], []
    H = GLYPH_H
    gap_clip = f'<path d="{sector(R_WELL - 1, R_KEY + 0.4, READY_GAP[0] - 1, READY_GAP[1] + 1)}"/>'
    # fix 9: the sky darkens upward from the horizon, as in the icon
    dd, ss = well(p, "#4F78C8", "#6A95E0", extra_clip=gap_clip, span=(12, H)); D += dd; S += ss
    # sea below the horizon (also fills the opened bottom of the bezel)
    D.append(f'<linearGradient id="{p}sea" x1="0" y1="{f(H)}" x2="0" y2="128" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{LAPIS_SEA}"/><stop offset="1" stop-color="{LAPIS_SEA_B}"/></linearGradient>')
    S.append(f'<rect x="0" y="{f(H)}" width="128" height="{f(128 - H)}" fill="url(#{p}sea)" clip-path="url(#{p}wc)"/>')
    mx, my, mr, mk, rot = GLYPH_MOON
    cxr = GLYPH_LIT_C[0]
    S.append(f'<g clip-path="url(#{p}wc)">')
    S += lit_body(p, mx, my, mr, mk, "right", rot, tw=3.2, lw=2.2, soft=0.8)
    # horizon: a fine moonlit line, brightest where the road meets it
    D.append(f'<linearGradient id="{p}hz" x1="12" y1="0" x2="116" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".08"/>'
             f'<stop offset="{f((cxr - 12) / 104)}" stop-color="{MOON}" stop-opacity=".6"/><stop offset="1" stop-color="{MOON}" stop-opacity=".08"/></linearGradient>')
    S.append(f'<rect x="10" y="{f(H - 0.6)}" width="108" height="1.2" fill="url(#{p}hz)"/>')
    # the road: horizontal ripple streaks in the moon's own colour, below its brightness, under the lit centroid
    rows = [
        (H + 3.4, 2.6, [(-6.5, 6.5, .62)]),
        (H + 9.0, 3.0, [(-12.0, 4.0, .72)]),
        (H + 15.2, 3.3, [(-15.5, -2.0, .66), (2.5, 14.5, .78)]),
        (H + 22.2, 3.7, [(-11.5, 13.0, .82)]),
        (H + 29.6, 4.0, [(-17.5, 0.0, .72), (4.0, 17.0, .66)]),
        (H + 37.4, 4.3, [(-8.0, 14.5, .74)]),
        (H + 45.2, 4.6, [(-16.5, 1.5, .64), (5.5, 18.5, .5)]),
    ]
    for y, h, dashes in rows:
        for x0, x1, o in dashes:
            S.append(f'<path d="{streak(cxr + x0, cxr + x1, y, h)}" fill="{MOON}" fill-opacity="{o}"/>')
    S.append('</g>')
    dd, ss = bezel(p, gap=READY_GAP); D += dd; S += ss
    return svg("Ready", D, S)


def soul_crystal(p, cx, cy, s=1.0):
    """An original job soul crystal: an irregular cut shard in a thin gilt setting, three facets lit from the upper left."""
    P = [(-10.5, -14.5), (3.5, -17.5), (12.0, -9.5), (11.0, 6.5), (1.5, 17.5), (-9.5, 9.0), (-12.5, -3.5)]
    pts = [(cx + x * s, cy + y * s) for x, y in P]
    c = (cx + 0.5 * s, cy - 1.5 * s)
    out = []
    # gilt setting (a thin collet) under the stone, and the keyline
    D = [f'<linearGradient id="{p}sc" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{GILT_HI}"/><stop offset=".6" stop-color="{GILT}"/><stop offset="1" stop-color="{GILT_DEEP}"/></linearGradient>']
    out.append(f'<path d="{poly(pts)}" fill="url(#{p}sc)" stroke="{KEY}" stroke-width="5.2" stroke-linejoin="round" paint-order="stroke"/>')
    out.append(f'<path d="{poly(pts)}" fill="url(#{p}sc)" stroke="url(#{p}sc)" stroke-width="2.6" stroke-linejoin="round"/>')
    out.append(f'<path d="{poly(pts)}" fill="{SOUL_MID}"/>')
    # facets: the table catches the key light (upper left), the lower-right pavilion falls into shade
    out.append(f'<path d="{poly([pts[6], pts[0], pts[1], c])}" fill="{SOUL_HI}" fill-opacity=".92"/>')
    out.append(f'<path d="{poly([pts[1], pts[2], c])}" fill="{SOUL}"/>')
    out.append(f'<path d="{poly([pts[2], pts[3], pts[4], c])}" fill="{SOUL_DEEP}" fill-opacity=".9"/>')
    out.append(f'<path d="{poly([pts[4], pts[5], pts[6], c])}" fill="{SOUL}" fill-opacity=".85"/>')
    # hero: a tiny engraved crescent on the table (the plugin's own mark) and a hairline facet edge
    hx, hy = cx - 3.5 * s, cy - 5 * s
    out.append(f'<g class="hero" opacity=".5"><path d="M{f(hx + 1.2)} {f(hy - 3.2)}a3.4 3.4 0 1 0 0 6.4a2.6 2.6 0 1 1 0 -6.4Z" fill="{SOUL_DEEP}"/>'
               f'<path d="M{f(pts[1][0])} {f(pts[1][1])}L{f(c[0])} {f(c[1])}L{f(pts[4][0])} {f(pts[4][1])}" stroke="{SOUL_HI}" stroke-width=".5" fill="none"/></g>')
    return D, out


def ready_other_job():
    p = "r3o-"
    D, S = [shadow_filter(p)], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 68.0, 64.0, 35.0
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="{DARKSIDE}"/>')
    dd, ss = hatch(p, mx, my, mr, -1); D += dd; S += ss
    S.append(f'<g filter="url(#{p}ds)">')
    S += lit_body(p, mx, my, mr, -0.3, "right", 34, fill=MOON, limb=MOON_HI, term=MOON_MID, tw=2.6, soft=0.8)
    S.append('</g>')
    dd, ss = soul_crystal(p, 44.0, 52.0, 1.25); D += dd
    S.append(f'<g filter="url(#{p}ds)">' + "".join(ss) + '</g>')
    dd, ss = bezel(p); D += dd; S += ss
    return svg("Ready on another job", D, S)


def in_journal():
    p = "r3j-"
    D, S = [shadow_filter(p)], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 70.0, 66.0, 32.0
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="{DARKSIDE}"/>')
    dd, ss = hatch(p, mx, my, mr, -1); D += dd; S += ss
    k = 0.42
    rx = k * mr
    dpath, _ = phase(mx, my, mr, k, "right")
    D.append(f'<linearGradient id="{p}gb" x1="{f(mx - rx)}" y1="0" x2="{f(mx + mr)}" y2="0" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{MOON_MID}"/><stop offset="1" stop-color="{MOON}"/></linearGradient>')
    D.append(f'<clipPath id="{p}lc"><path d="{dpath}"/></clipPath>')
    D.append(f'<filter id="{p}sb" filterUnits="userSpaceOnUse" x="0" y="0" width="128" height="128"><feGaussianBlur stdDeviation=".8"/></filter>')
    S.append(f'<g filter="url(#{p}ds)"><path d="{dpath}" fill="url(#{p}gb)"/></g>')
    # one terminator only: the front (left) half of the ellipse, softened; and a soft limb band
    S.append(f'<g clip-path="url(#{p}lc)"><g filter="url(#{p}sb)"><path d="M{f(mx)} {f(my - mr)}A{f(rx)} {f(mr)} 0 0 0 {f(mx)} {f(my + mr)}" fill="none" stroke="{MOON_LOW}" stroke-width="2"/>'
             f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 0.9)}" fill="none" stroke="{MOON_HI}" stroke-width="1.8"/></g></g>')
    dd, ss = bezel(p); D += dd; S += ss
    # journal ribbon in Tide silk: its top wraps behind the medal along an arc concentric with it (r 66), it lies over the
    # rim and drops into the well; lit edge on the left (toward the light); a soft shadow falls down-right onto the rim
    x0, w, y1 = 24.0, 15.0, 70.0
    ya = 64 - math.sqrt(66 ** 2 - (64 - x0) ** 2)
    yb = 64 - math.sqrt(66 ** 2 - (64 - x0 - w) ** 2)
    rib = f"M{f(x0)} {f(ya)}A66 66 0 0 1 {f(x0 + w)} {f(yb)}V{f(y1)}L{f(x0 + w / 2)} {f(y1 - 7.5)}L{f(x0)} {f(y1)}Z"
    D.append(f'<linearGradient id="{p}rb" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="{TIDE_HI}"/><stop offset=".3" stop-color="{TIDE}"/>'
             f'<stop offset="1" stop-color="{TIDE_DEEP}"/></linearGradient>')
    D.append(f'<clipPath id="{p}rc"><path d="{rib}"/></clipPath>')
    D.append(f'<filter id="{p}rs" x="-30%" y="-10%" width="160%" height="120%" color-interpolation-filters="sRGB">'
             f'<feDropShadow dx="1.2" dy="1.0" stdDeviation="1.1" flood-color="{KEY}" flood-opacity=".6"/></filter>')
    S.append(f'<g filter="url(#{p}rs)"><path d="{rib}" fill="url(#{p}rb)" stroke="{KEY}" stroke-width="2.2" stroke-linejoin="round" paint-order="stroke"/></g>')
    ann = lambda r0, r1: (f"M{f(64 + r1)} 64A{f(r1)} {f(r1)} 0 1 1 {f(64 - r1)} 64A{f(r1)} {f(r1)} 0 1 1 {f(64 + r1)} 64Z"
                          f"M{f(64 + r0)} 64A{f(r0)} {f(r0)} 0 1 0 {f(64 - r0)} 64A{f(r0)} {f(r0)} 0 1 0 {f(64 + r0)} 64Z")
    S.append(f'<g clip-path="url(#{p}rc)">'
             # the fold turning behind the medal (outside the silhouette) is in shade
             f'<path d="{ann(R_KEY, 70)}" fill="{TIDE_DEEP}" fill-rule="evenodd"/>'
             # over the rim crest the silk faces the light; on the inner slope it drops into the well and shades
             f'<path d="{ann(58.5, 60.5)}" fill="{TIDE_HI}" fill-opacity=".5" fill-rule="evenodd"/>'
             f'<path d="{ann(53, 56)}" fill="{KEY}" fill-opacity=".3" fill-rule="evenodd"/></g>')
    S.append(f'<g class="hero" opacity=".5"><path d="M{f(x0 + 2.6)} 14V{f(y1 - 6)}M{f(x0 + w - 2.6)} 14V{f(y1 - 4)}" stroke="{TIDE_HI}" stroke-width=".55" stroke-dasharray="1.6 1.2"/></g>')
    return svg("In journal", D, S)


def blocked():
    p = "r3b-"
    D, S = [shadow_filter(p)], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, 35.0
    # a new moon: an ashen disc lit only by earthshine, with the thinnest sunlit limb on the right ("not lit yet")
    D.append(f'<linearGradient id="{p}as" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#424D70"/><stop offset="1" stop-color="#2B3352"/></linearGradient>')
    S.append(f'<g filter="url(#{p}ds)"><circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="url(#{p}as)"/></g>')
    dd, ss = hatch(p, mx, my, mr, -1, color="#8C96B4", op=".24", reach=0.95); D += dd; S += ss
    S += lit_body(p, mx, my, mr, -0.62, "right", 0, fill=MOON, limb=MOON_HI, term=ASH_LIMB, tw=1.0, lw=1.8)
    dd, ss = bezel(p); D += dd; S += ss
    return svg("Blocked", D, S)


def done():
    p = "r3d-"
    D, S = [shadow_filter(p)], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 62.0, 64.0, 34.0
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#26335F"/>')
    dd, ss = hatch(p, mx, my, mr, 1); D += dd; S += ss
    S.append(f'<g filter="url(#{p}ds)">')
    S += lit_body(p, mx, my, mr, 0.0, "left", fill=MOON_MID, limb=MOON, term=MOON_LOW, tw=2.4)
    S.append('</g>')
    # "comes back": a gilt inlay tracing the dark limb where the light will return; tapered at both ends, no arrowhead
    D.append(f'<linearGradient id="{p}cb" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{GILT_HI}"/><stop offset="1" stop-color="{GILT}"/></linearGradient>')
    S.append(f'<g filter="url(#{p}ds)"><path d="{inlay_band(mx, my, mr, -50, 80, 15, 6.0, 0.6)}" fill="url(#{p}cb)" stroke="{KEY}" stroke-width="1.1" stroke-opacity=".8"/></g>')
    dd, ss = bezel(p); D += dd; S += ss
    return svg("Done this cycle", D, S)


def completed():
    p = "r3c-"
    D, S = [shadow_filter(p)], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 61.0, 61.0, 38.0
    # a quiet full moon: soft moonstone face, a pale edge band (limb) and a darker inner rim of the face for volume
    D.append(f'<linearGradient id="{p}fc" x1="0.2" y1="0.15" x2="0.8" y2="0.9"><stop offset="0" stop-color="#7C89AF"/><stop offset="1" stop-color="#56628C"/></linearGradient>')
    S.append(f'<g filter="url(#{p}ds)"><circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="url(#{p}fc)"/></g>')
    D.append(f'<linearGradient id="{p}lr" x1="0.2" y1="0.15" x2="0.8" y2="0.9"><stop offset="0" stop-color="{MOON}" stop-opacity=".95"/>'
             f'<stop offset=".5" stop-color="{MOON_MID}" stop-opacity=".5"/><stop offset="1" stop-color="{MOON_MID}" stop-opacity="0"/></linearGradient>')
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 1.6)}" fill="none" stroke="url(#{p}lr)" stroke-width="3"/>')
    # hero: Menphina's moon-daisy, engraved very lightly (a medal emblem, not lunar surface)
    petals = "".join(f'<ellipse cx="{f(mx)}" cy="{f(my - 13)}" rx="4" ry="10.5" transform="rotate({i * 45} {f(mx)} {f(my)})"/>' for i in range(8))
    S.append(f'<g class="hero" fill="none" stroke="{MOON_LOW}" stroke-width=".6" opacity=".45">{petals}<circle cx="{f(mx)}" cy="{f(my)}" r="3"/></g>')
    dd, ss = bezel(p); D += dd; S += ss
    # the gilt check, struck over the lower-right edge of the moon and out across the rim
    chk = "M64 86L78 100L117.5 40"
    D.append(f'<linearGradient id="{p}ck" x1="64" y1="40" x2="117" y2="100" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{GILT_HI}"/>'
             f'<stop offset=".5" stop-color="{GILT_LIGHT}"/><stop offset="1" stop-color="#A88B52"/></linearGradient>')
    S.append(f'<g filter="url(#{p}ds)"><path d="{chk}" fill="none" stroke="{KEY}" stroke-width="15" stroke-linecap="round" stroke-linejoin="round"/>'
             f'<path d="{chk}" fill="none" stroke="url(#{p}ck)" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/></g>')
    # bevel on the check: a bright hairline along the upper-left edges (toward the light)
    S.append(f'<path d="M62.3 83.3L76.5 97.4L115.1 38.3" fill="none" stroke="{GILT_SPEC}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" opacity=".85"/>')
    return svg("Completed", D, S)


def locked_out():
    p = "r3l-"
    D, S = [shadow_filter(p)], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, 37.0
    # Dalamud: the red moon, split top to bottom by a wide dark fracture; the halves are pushed apart
    left_clip = "M0 0H59L66 22L55 44L68 62L54 82L63 104L56 128H0Z"
    right_clip = "M128 0H73L80 22L69 44L82 62L68 82L77 104L70 128H128Z"
    D.append(f'<clipPath id="{p}cl"><path d="{left_clip}"/></clipPath><clipPath id="{p}cr"><path d="{right_clip}"/></clipPath>')
    D.append(f'<linearGradient id="{p}rd" x1="0.15" y1="0.1" x2="0.85" y2="0.95"><stop offset="0" stop-color="{RED_HI}"/>'
             f'<stop offset=".55" stop-color="{RED}"/><stop offset="1" stop-color="{RED_DEEP}"/></linearGradient>')
    disc = f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="url(#{p}rd)"/>'
    # the dark fracture between the halves (it shows through as an abyss, not as enamel)
    S.append(f'<g clip-path="url(#{p}wc)"><circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 1)}" fill="{CRACK}"/></g>')
    S.append(f'<g filter="url(#{p}ds)"><g clip-path="url(#{p}cl)" transform="translate(-3 0)">{disc}</g>'
             f'<g clip-path="url(#{p}cr)" transform="translate(3 0)">{disc}</g></g>')
    # fracture walls: the right half's wall faces the light (lit edge), the left half's wall faces away (dark edge)
    D.append(f'<clipPath id="{p}dc"><circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}"/></clipPath>')
    S.append(f'<g clip-path="url(#{p}cr)" transform="translate(3 0)"><g clip-path="url(#{p}dc)"><path d="M73 0L80 22L69 44L82 62L68 82L77 104L70 128" fill="none" stroke="#E8A0AA" stroke-width="2.2" opacity=".55"/></g></g>')
    S.append(f'<g clip-path="url(#{p}cl)" transform="translate(-3 0)"><g clip-path="url(#{p}dc)"><path d="M59 0L66 22L55 44L68 62L54 82L63 104L56 128" fill="none" stroke="{CRACK}" stroke-width="2.6" opacity=".7"/></g></g>')
    dd, ss = bezel(p); D += dd; S += ss
    return svg("Locked out", D, S)


def not_checked():
    p = "r3n-"
    D, S = [shadow_filter(p)], []
    dd, ss = well(p); D += dd; S += ss
    # oborozuki: a hazy moon with a soft edge, behind one drifting wisp of mist that spills over the left rim
    D.append(f'<filter id="{p}hb" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="1.6"/></filter>')
    D.append(f'<radialGradient id="{p}hm" cx="64" cy="66" r="40" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON_MID}" stop-opacity=".55"/>'
             f'<stop offset=".7" stop-color="{MOON_LOW}" stop-opacity=".35"/><stop offset="1" stop-color="{MOON_LOW}" stop-opacity="0"/></radialGradient>')
    S.append(f'<circle cx="64" cy="66" r="31" fill="url(#{p}hm)" filter="url(#{p}hb)"/>')
    dd, ss = bezel(p); D += dd; S += ss
    D.append(f'<filter id="{p}mb" x="-10%" y="-40%" width="120%" height="180%"><feGaussianBlur stdDeviation="1.25"/></filter>')
    # the wisp is lit only by the moon, so it is brightest where it crosses the moon (x 64) and fades toward both ends;
    # its spill end tapers to a point inside the viewBox (x 3)
    wisp = tapered_curve((3, 60), (52, 38), (108, 50), 16.0, head=0.45)
    D.append(f'<linearGradient id="{p}ws" x1="3" y1="0" x2="108" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MIST}" stop-opacity=".35"/>'
             f'<stop offset="{f((64 - 3) / 105)}" stop-color="{MIST_HI}"/><stop offset="1" stop-color="{MIST}" stop-opacity=".3"/></linearGradient>')
    S.append(f'<path d="{wisp}" transform="translate({LIGHT_DX} {LIGHT_DY})" fill="{KEY}" fill-opacity=".35" filter="url(#{p}mb)"/>')
    # a wider, fainter haze under the wisp so its edge reads as vapour, not a blade
    D.append(f'<filter id="{p}mh" x="-10%" y="-60%" width="120%" height="220%"><feGaussianBlur stdDeviation="2.6"/></filter>')
    D.append(f'<radialGradient id="{p}hg" cx="64" cy="48" r="52" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MIST}" stop-opacity=".34"/>'
             f'<stop offset="1" stop-color="{MIST}" stop-opacity=".06"/></radialGradient>')
    S.append(f'<path d="{tapered_curve((6, 61), (52, 40), (110, 51), 26.0, head=0.45)}" fill="url(#{p}hg)" filter="url(#{p}mh)"/>')
    S.append(f'<path d="{wisp}" fill="url(#{p}ws)" filter="url(#{p}mb)"/>')
    return svg("Not checked", D, S)


# ---------------- plugin icon ----------------
ICON_MOON = (132.0, 146.0, 80.0, -0.36, 30.0)     # cx, cy, r, k, rotation
ICON_H = 300.0
ICON_LIT_C = centroid(*ICON_MOON[:4], "right", ICON_MOON[4], step=0.5, extent=512)
ROAD_IX = ICON_LIT_C[0]
BRIGHT_Y = min(ICON_H + (ICON_H - ICON_LIT_C[1]), 470)
LANTERN_X = 72.0

# icon road rows (supervisor fix 2/3): (y centre, [(dx0, dx1, height, opacity)]) relative to ROAD_IX.
# Twelve rows below the horizon, foreshortened toward it (heights 2.5 -> 18, gaps growing monotonically), brightest at
# y 437 (rule 431). Each row has one main streak; secondary glints are thin dashes (height <= 8 and <= half the row,
# aspect >= 5, opacity 0.75x the main) in an irregular rhythm. Lens half-widths as before; the left edge stays >= 10
# units clear of the lantern islet.
ROAD_ROWS = [
    (302.5, [(-20, 18, 2.5, .40)]),
    (307.0, [(-24, 21, 3.5, .44)]),
    (314.0, [(-29, 24, 5.0, .49)]),
    (323.0, [(-30, 33, 7.0, .54)]),
    (335.0, [(-40, 14, 9.0, .59), (20, 42, 4.0, .44)]),
    (350.0, [(-34, 30, 11.0, .64), (36, 52, 3.0, .48)]),
    (368.0, [(-50, 16, 13.0, .69), (22, 48, 5.0, .52)]),
    (389.0, [(-44, 50, 14.5, .73)]),
    (412.0, [(-55, -27, 5.5, .58), (-21, 57, 15.5, .77)]),
    (437.0, [(-55, 61, 16.5, .80)]),
    (464.0, [(-52, 22, 17.0, .70), (28, 54, 5.0, .52)]),
    (492.0, [(-36, 34, 18.0, .55)]),
]
ROAD = [(ROAD_IX + a, ROAD_IX + b, y, h, o) for y, segs in ROAD_ROWS for a, b, h, o in segs]


def icon():
    p = "r3i-"
    D, S = [], []
    H = ICON_H
    mx, my, mr, mk, rot = ICON_MOON
    D.append(f'<clipPath id="{p}t"><rect width="512" height="512" rx="112"/></clipPath>')
    D.append(f'<linearGradient id="{p}sky" x1="0" y1="0" x2="0" y2="{f(H)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#0C1330"/>'
             f'<stop offset=".62" stop-color="#1A2650"/><stop offset="1" stop-color="#31426C"/></linearGradient>')
    D.append(f'<linearGradient id="{p}sea" x1="0" y1="{f(H)}" x2="0" y2="512" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#2F3F69"/>'
             f'<stop offset=".1" stop-color="#1A2549"/><stop offset=".55" stop-color="#0E1530"/><stop offset="1" stop-color="#070B19"/></linearGradient>')
    D.append(f'<radialGradient id="{p}bl" cx="{f(ICON_LIT_C[0])}" cy="{f(ICON_LIT_C[1])}" r="180" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".13"/>'
             f'<stop offset=".5" stop-color="{MOON}" stop-opacity=".04"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    D.append(f'<radialGradient id="{p}col" cx="{f(ROAD_IX)}" cy="{f(BRIGHT_Y)}" r="1" gradientUnits="userSpaceOnUse" gradientTransform="translate({f(ROAD_IX)} {f(BRIGHT_Y)}) scale(80 150) translate({f(-ROAD_IX)} {f(-BRIGHT_Y)})">'
             f'<stop offset="0" stop-color="{MOON}" stop-opacity=".12"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
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
    D.append(f'<filter id="{p}fs" x="-10%" y="-10%" width="120%" height="120%"><feGaussianBlur stdDeviation="5"/></filter>')
    D.append(f'<mask id="{p}fm"><rect width="512" height="512" fill="#fff"/><rect x="{f(14 + 7)}" y="{f(14 + 10)}" width="484" height="484" rx="98" fill="#000"/></mask>')
    S.append(f'<g clip-path="url(#{p}t)">')
    S.append(f'<rect width="512" height="{f(H)}" fill="url(#{p}sky)"/><rect y="{f(H)}" width="512" height="{f(512 - H)}" fill="url(#{p}sea)"/>')
    S.append(f'<rect width="512" height="{f(H)}" fill="url(#{p}bl)"/>')
    # moon: earthshine disc (kept under 1.15:1 so the road stays on the lit centroid), thick crescent, and soft terminator
    # and limb bands (brightness falls off smoothly from the limb to the single terminator)
    dpath, tr = phase(mx, my, mr, mk, "right", rot)
    t = f' transform="{tr}"'
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#3A4A78" fill-opacity=".22"/>')
    S.append(f'<path d="{dpath}" fill="{MOON}"{t}/>')
    D.append(f'<clipPath id="{p}lc"><path d="{dpath}"{t}/></clipPath>')
    D.append(f'<filter id="{p}mbl" filterUnits="userSpaceOnUse" x="0" y="0" width="512" height="512"><feGaussianBlur stdDeviation="3"/></filter>')
    S.append(f'<g clip-path="url(#{p}lc)"><g filter="url(#{p}mbl)"><g{t}><ellipse cx="{f(mx)}" cy="{f(my)}" rx="{f(abs(mk) * mr)}" ry="{f(mr)}" fill="none" stroke="{MOON_MID}" stroke-width="10"/></g>'
             f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 3)}" fill="none" stroke="{MOON_HI}" stroke-width="6"/></g></g>')
    # far headland on the right horizon (two layers, the farther one paler: atmospheric perspective), and its dark mirror
    far = (f"M300 {f(H)}C318 {f(H - 8)} 338 {f(H - 13)} 356 {f(H - 14)}C372 {f(H - 20)} 388 {f(H - 26)} 404 {f(H - 26)}"
           f"C420 {f(H - 25)} 432 {f(H - 18)} 448 {f(H - 16)}C468 {f(H - 13)} 490 {f(H - 9)} 512 {f(H - 7)}V{f(H)}Z")
    near = (f"M392 {f(H)}C410 {f(H - 6)} 430 {f(H - 10)} 452 {f(H - 11)}C474 {f(H - 12)} 494 {f(H - 8)} 512 {f(H - 6)}V{f(H)}Z")
    S.append(f'<rect x="0" y="{f(H - 1)}" width="512" height="2.2" fill="url(#{p}hz)"/>')
    S.append(f'<path d="{far}" fill="#1A2448"/><path d="{near}" fill="#0C1226"/>')
    S.append(f'<g transform="translate(0 {f(2 * H)}) scale(1 -1)" opacity=".35"><path d="{far}" fill="#0A1024"/></g>')
    S.append(f'<rect x="0" y="{f(H)}" width="512" height="{f(512 - H)}" fill="url(#{p}col)"/>')
    for (x0, x1, y, h, o) in ROAD:
        S.append(f'<path d="{streak(x0, x1, y, h)}" fill="{MOON}" fill-opacity="{o}"/>')
    S.append(f'<g transform="translate({f(LANTERN_X)} 300) scale(.86) translate({f(-LANTERN_X)} -300)">{lantern()}</g>')
    S.append(f'<circle cx="{f(LANTERN_X)}" cy="350" r="50" fill="url(#{p}lg)"/>')
    # the window's reflection: a warm column directly below the window, brightening and lengthening toward the viewer
    # (its mirror point is at the bottom edge), in the road's needle shape
    for y, w, h, o in ((446, 12, 2.5, .40), (457, 16, 3, .52), (469, 22, 4, .62), (482, 28, 5, .70)):
        S.append(f'<path d="{streak(LANTERN_X - w / 2, LANTERN_X + w / 2, y, h, skew=0.5)}" fill="{LANTERN_GOLD}" fill-opacity="{o}"/>')
    # the raised frame casts a soft shadow onto the scene along its upper and left inner edges
    S.append(f'<rect x="0" y="0" width="512" height="512" fill="{KEY}" fill-opacity=".55" mask="url(#{p}fm)" filter="url(#{p}fs)"/>')
    S.append('</g>')
    # gilt frame: keyline, outer slope (light upper-left), inner slope (reverse), crest seam, specular toward the light
    S.append(f'<rect x="4" y="4" width="504" height="504" rx="108" fill="none" stroke="{KEY}" stroke-width="8"/>')
    S.append(f'<rect x="8.5" y="8.5" width="495" height="495" rx="103.5" fill="none" stroke="url(#{p}rim)" stroke-width="7"/>')
    S.append(f'<rect x="14.5" y="14.5" width="483" height="483" rx="97.5" fill="none" stroke="url(#{p}rin)" stroke-width="5"/>')
    S.append(f'<rect x="12" y="12" width="488" height="488" rx="100" fill="none" stroke="{GILT_DARK}" stroke-opacity=".5" stroke-width="1"/>')
    S.append(f'<rect x="17.5" y="17.5" width="477" height="477" rx="94.5" fill="none" stroke="{KEY}" stroke-opacity=".8" stroke-width="1.5"/>')
    S.append(f'<rect x="7.5" y="7.5" width="497" height="497" rx="104.5" fill="none" stroke="url(#{p}spec)" stroke-width="2.2"/>')
    S.append(filigree())
    return svg("Tsukimichi", D, S, 512)


def lantern():
    """A Hingashi-style stone toro (after the Kugane and Stone Toro Lantern furnishings): hoju finial, kasa roof with
    upturned warabite corners, hibukuro firebox with a lit window, chudai platform, sao post and kiso base, on a rock islet.
    Moonlit edges face the moon (upper right); the flame lights the window jambs and a pool on the islet; the islet's
    reflection lies directly below it (the window's reflection is drawn in scene coordinates by icon())."""
    g = "#060914"
    rim = f'stroke="{MOON}" stroke-opacity=".34" stroke-width="2" fill="none" stroke-linecap="round"'
    bx = 0.0
    P = []
    # islet: runs in under the frame on the left, stops short of the road on the right (>= 10 units of open water);
    # its dark reflection directly below, as tall as the islet; moonlight on its right shoulder
    islet = "M-70 454C-60 440 -30 431 0 431C18 429 30 433 36 442C38 446 39.5 450 40 454Z"
    P.append(f'<path d="{islet}" fill="{g}"/>')
    P.append(f'<path d="M-70 455H40C32 466 18 476 0 477C-26 478 -52 470 -70 455Z" fill="{g}" fill-opacity=".5"/>')
    P.append(f'<path d="M{f(bx + 20)} 431C{f(bx + 28)} 432 {f(bx + 34)} 436 {f(bx + 38)} 441" stroke="{MOON}" stroke-opacity=".22" stroke-width="2" fill="none" stroke-linecap="round"/>')
    # the flame's warm pool on the islet's top surface (the viewer looks down on it), clipped to the islet
    P.append(f'<clipPath id="r3i-ic"><path d="{islet}"/></clipPath>')
    # Supervisor review 2: the pool lies in front of the base, not under it (the window ledge shades the foot).
    P.append(f'<radialGradient id="r3i-pool"><stop offset="0" stop-color="{LANTERN_GOLD}" stop-opacity=".32"/><stop offset="1" stop-color="{LANTERN_GOLD}" stop-opacity="0"/></radialGradient>')
    P.append(f'<ellipse cx="0" cy="441.5" rx="22" ry="2.6" fill="url(#r3i-pool)" clip-path="url(#r3i-ic)"/>')
    # kiso base, sao post, chudai platform, hibukuro firebox, kasa roof, hoju finial
    P.append(f'<path d="M{f(bx - 27)} 436H{f(bx + 27)}L{f(bx + 21)} 424H{f(bx - 21)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 8)} 424H{f(bx + 8)}L{f(bx + 6.5)} 388H{f(bx - 6.5)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 25)} 388H{f(bx + 25)}L{f(bx + 20)} 378H{f(bx - 20)}Z" fill="{g}"/>')
    P.append(f'<rect x="{f(bx - 19)}" y="340" width="38" height="38" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 46)} 330C{f(bx - 40)} 331 {f(bx - 36)} 327 {f(bx - 33)} 324C{f(bx - 26)} 318 {f(bx - 22)} 312 {f(bx - 17)} 305H{f(bx + 17)}'
             f'C{f(bx + 22)} 312 {f(bx + 26)} 318 {f(bx + 33)} 324C{f(bx + 36)} 327 {f(bx + 40)} 331 {f(bx + 46)} 330L{f(bx + 41)} 341H{f(bx - 41)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 7)} 305C{f(bx - 9)} 298 {f(bx - 6)} 291 {f(bx)} 284C{f(bx + 6)} 291 {f(bx + 9)} 298 {f(bx + 7)} 305Z" fill="{g}"/>')
    # moonlit edges on the side facing the moon
    P.append(f'<path d="M{f(bx + 17)} 305C{f(bx + 22)} 312 {f(bx + 26)} 318 {f(bx + 33)} 324C{f(bx + 36)} 327 {f(bx + 40)} 331 {f(bx + 46)} 330" {rim}/>')
    P.append(f'<path d="M{f(bx + 19)} 343V376M{f(bx + 7)} 391V422M{f(bx + 5)} 295C{f(bx + 7)} 299 {f(bx + 7.5)} 302 {f(bx + 6.5)} 304M{f(bx + 21)} 380L{f(bx + 25)} 387" {rim}/>')
    # lit window: two panes
    P.append(f'<rect x="{f(bx - 11)}" y="348" width="22" height="23" rx="2" fill="{LANTERN_GOLD}"/>')
    P.append(f'<path d="M{f(bx)} 348V371" stroke="{g}" stroke-width="3"/>')
    # jambs and sill of the window opening, lit by the flame
    P.append(f'<path d="M{f(bx - 12)} 348V372H{f(bx + 12)}V348" fill="none" stroke="{LANTERN_GOLD}" stroke-opacity=".5" stroke-width="1"/>')
    return f'<g transform="translate({f(LANTERN_X)} 0)">' + "".join(P) + '</g>'


def filigree():
    """Corner filigree for the two top corners: one hairline following the frame, ending in small crescents, held at
    least 27 units clear of the moon's earthshine disc."""
    out = []
    for sx, tx in ((1, 0), (-1, 512)):
        tr = f'transform="translate({tx} 0) scale({sx} 1)"'
        out.append(f'<g {tr} fill="none" stroke="{GILT_SPEC}" stroke-opacity=".28" stroke-width="2" stroke-linecap="round">'
                   f'<path d="M30 110C30 64 64 30 110 30"/>'
                   f'<path d="M30 116a6 6 0 1 0 0 12a4.5 4.5 0 1 1 0 -12Z" fill="{GILT_SPEC}" fill-opacity=".28" stroke="none"/>'
                   f'<path d="M116 30a6 6 0 1 0 12 0a4.5 4.5 0 1 1 -12 0Z" fill="{GILT_SPEC}" fill-opacity=".28" stroke="none"/></g>')
    return "".join(out)


if __name__ == "__main__":
    write("ready.svg", ready())
    write("ready-on-another-job.svg", ready_other_job())
    write("in-journal.svg", in_journal())
    write("blocked.svg", blocked())
    write("done-this-cycle.svg", done())
    write("completed.svg", completed())
    write("locked-out.svg", locked_out())
    write("not-checked.svg", not_checked())
    write("plugin-icon.svg", icon())
    print("glyph lit centroid", GLYPH_LIT_C, "icon lit centroid", ICON_LIT_C, "bright y", BRIGHT_Y)
    print("road x range", min(r[0] for r in ROAD), max(r[1] for r in ROAD), "glints", len(ROAD))
