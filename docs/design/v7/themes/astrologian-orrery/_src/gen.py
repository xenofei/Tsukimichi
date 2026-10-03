"""Astrologian's Orrery (v7 theme), review round 1: faces split from frames.

Writes, into docs/design/v7/themes/astrologian-orrery:
  hero/<state>.svg, hero/<state>.over.svg    unframed faces (well r 52.4 at 64,64) and their overhangs: the 96 and 128
                                             atlas tiers (and 2x), with the engraved hatching
  mid/<state>.svg,  mid/<state>.over.svg     the 48 and 64 tiers: hero craft, but the row tier's smooth tone on lit moons
  row/<state>.svg,  row/<state>.over.svg     the same at row tier (no hairline craft)
  kit/                                       the Astrolabe frame kit: 4 urgency tiers x Full/Quiet (+ kit/row/), the
                                             badge ring (resting and act-now), seats, and the lock and book glyphs
  <state>.svg, ready-on-another-job-*.svg    composites: face + Astrolabe kit + badge (what the sheet shows)
  _row/<state>.svg                           row composites, no badge (what the metrics measure)
  _row/badge-*.svg                           the badge glyphs alone, for text height beside a row medal
  _mix/                                      Orrery faces in the Brass kit, and Medallion faces in the Astrolabe kit

The set's idea, shared by every state: a moon read on a Sharlayan astrolabe. The moon is an engraved silver moon (its
maria cut as hatching, as in a 17th-century selenography, at hero size); its dark side carries an engraved star chart;
the marks are drawn as an astronomer would draw them (a constellation "?", a graduated scale arc for "comes back", a
dawn sighting with a limb scale for Ready). Brass and starlight on Prussian blue; nothing radiates outside the rim.

One key light from the upper left. Raised metal is bright upper-left and dark lower-right; recesses are the reverse;
raised emblems cast a short soft shadow down and to the right. Phases are lit by their sun; in the scenes the moon is
the light and the road falls directly below it. Stars are light (a soft glow, no shadow); engraving is not (no glow).
"""
import base64, math, pathlib, sys
import numpy as np

OUT = pathlib.Path(__file__).resolve().parent.parent
MED_SRC = OUT.parents[2] / "moon-v6" / "round5" / "medallion-r5" / "_src"
JOBDIR = MED_SRC / "jobs"                           # the game's job icons (read only)

# ---------------- palette tokens ----------------
KEY = "#070A15"
# the Astrolabe kit's resting brass (old brass: a bright lip, a darker face)
B_SPEC, B_HI, B_LIGHT, BRASS, B_MID, B_DEEP, B_DARK = (
    "#FFF0C6", "#EDCB82", "#D6A957", "#B5863A", "#8F6526", "#644515", "#2C1C07")
ENGRAVE = "#3A2709"
# the shared medal gilt: act now in every kit (supervisor S2), and the Completed check
G_SPEC, G_HI, G_BASE, G_MID, G_DEEP, G_LIGHT = "#FFF4D6", "#E6CF98", "#9A7E4A", "#7C6236", "#5C4724", "#D9BE82"
STAR, STAR_GLOW = "#F1F4FF", "#B9CAFF"
CHART = "#CDB57C"
PLATE_C, PLATE_E = "#15355A", "#08162C"             # Prussian-blue night lacquer, centre -> edge
MOON_HI, MOON, MOON_MID, MOON_LOW, MOON_DEEP = "#F5F6FA", "#E3E8F3", "#C4CCE0", "#94A0C2", "#5C6890"
HATCH = "#4E5A82"                                   # the engraver's line on the silver moon
# Ready: the hour before sunrise (the Dawn palette): a periwinkle zenith falling to a rose-gold horizon
DAWN_SKY = (("0", "#4A5BA8"), (".52", "#9389C6"), ("1", "#EDB79E"))
DAWN_SUN = "#F7CF9C"
DAWN_SEA_H, DAWN_SEA_B = "#6E68A6", "#2C3170"
NI_SKY_T, NI_SKY_H, NI_SEA_H, NI_SEA_B = "#10294A", "#2E5884", "#244A72", "#0C1F3C"   # In journal: night
EMBER, EMBER_HI, EMBER_DEEP = "#C98A52", "#E6B582", "#86501F"     # ribbon, a step down (critic 14)
DAL_HI, DAL, DAL_DEEP, DAL_EDGE = "#DE8C86", "#C25C60", "#8E2A2E", "#F6C0B6"   # rose Dalamud (plan 4.3)
STEEL = ("#C2CBE0", "#8C99BA", "#4E5A80", "#E6EBF6", "#2A3354")
PATINA_HI, PATINA, PATINA_LOW, PATINA_DARK = "#B8A578", "#7A6A45", "#4E4330", "#2A2418"   # Blocked's cloud (O1)
ROLE = {"tank": ("#5878C2", "#2C417E"), "healer": ("#5C9A68", "#2B5A38"), "dps": ("#B25A64", "#5E2632")}
JOBS = {"paladin": ("Paladin", "tank", 62019), "bard": ("Bard", "dps", 62023), "white-mage": ("White Mage", "healer", 62024)}
DEFAULT_JOB = "paladin"
SEAT = {"open": ("#4384C4", "#163F72"), "closed": ("#22415F", "#0A1A30"), "journal": ("#2E4F7E", "#0F2142")}

C = 64.0
R_FACE = 52.4                                       # the one well every face sits in (shared with Medallion)
R_KEY, R_LIP_O, R_LIP_I, R_TOP_O, R_TOP_I = 63.2, 62.1, 60.2, 59.8, 54.0
R_RULE_O, R_RULE_I = 58.4, 55.5
LIGHT_DX, LIGHT_DY = 1.1, 1.6
LIGHT_V = (-0.6, -0.8)
HERO = True                                         # hero craft (hairlines) on; False for the row faces
# engraved hatching on the lit moons (supervisor round 2): "on" only for the 96/128 atlas tiers (and 2x), "off" for 48/64
# (they take the row tier's smooth tone), "auto" for the one-file composites, which switch by their rendered size
HATCH = "auto"
HATCH_STYLE = ('<style>.v7o-hx{display:none}@media (min-width:80px){.v7o-hx{display:inline}.v7o-sm{display:none}}</style>')

# urgency tiers of the Astrolabe kit: (lip ramp 4, top-face ramp 4, step ramp 3, spec, spec opacity, engraving opacity)
TIERS = {
    "act-now": ((G_HI, G_BASE, G_MID, G_DEEP), ("#B39A62", G_BASE, G_MID, G_DEEP), ("#33260F", G_DEEP, G_LIGHT), G_SPEC, .95, .7),
    "resting": ((B_HI, BRASS, B_MID, B_DEEP), ("#B98636", "#9C6C28", "#7C531C", "#5A3B12"), (B_DARK, B_DEEP, B_LIGHT), B_SPEC, .92, .7),
    "finished": (("#CDB07A", "#9A7840", "#775A2A", "#54401E"), ("#9A7A42", "#82642F", "#684E22", "#4A3716"), ("#261A08", "#54401E", "#B89660"), "#F2E2BC", .7, .55),
    "ghost": (("#9C8C6C", "#6E6048", "#564A36", "#3E3526"), ("#6E5E40", "#5C4E36", "#4A3E2C", "#362D20"), ("#1C160E", "#3E3526", "#8A7A5C"), "#CFC3A6", .45, .4),
}
STATE_TIER = {"ready": "act-now", "ready-on-another-job": "resting", "in-journal": "resting", "blocked": "resting",
              "done-this-cycle": "resting", "completed": "finished", "locked-out": "resting", "not-checked": "ghost"}
STATE_BADGE = {"ready": "open", "ready-on-another-job": "job", "in-journal": "journal", "blocked": "closed"}
STATES = list(STATE_TIER)


def f(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


def pt(r, a, cx=C, cy=C):
    a = math.radians(a)
    return cx + r * math.cos(a), cy + r * math.sin(a)


def poly(points):
    return "M" + "L".join(f"{f(x)} {f(y)}" for x, y in points) + "Z"


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


def taper_arc(rm, a0, a1, w, cx=C, cy=C, n=40, power=1.0):
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        a = a0 + (a1 - a0) * t
        hw = w / 2 * math.sin(math.pi * t) ** power
        outer.append(pt(rm + hw, a, cx, cy))
        inner.append(pt(rm - hw, a, cx, cy))
    return poly(outer + inner[::-1])


def ring(r0, r1, cx=C, cy=C):
    return (f"M{f(cx + r1)} {f(cy)}A{f(r1)} {f(r1)} 0 1 1 {f(cx - r1)} {f(cy)}A{f(r1)} {f(r1)} 0 1 1 {f(cx + r1)} {f(cy)}Z"
            f"M{f(cx + r0)} {f(cy)}A{f(r0)} {f(r0)} 0 1 0 {f(cx - r0)} {f(cy)}A{f(r0)} {f(r0)} 0 1 0 {f(cx + r0)} {f(cy)}Z")


def circ(r, cx=C, cy=C):
    return f"M{f(cx + r)} {f(cy)}A{f(r)} {f(r)} 0 1 1 {f(cx - r)} {f(cy)}A{f(r)} {f(r)} 0 1 1 {f(cx + r)} {f(cy)}"


def streak(x0, x1, yc, h, skew=0.44, n=20):
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


def svg(title, defs, body, vb="0 0 128 128", size=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{vb}" width="{size}" height="{size}"><title>{title}</title>'
            f'<defs>{"".join(defs)}</defs>{"".join(body)}</svg>\n')


def write(rel, text):
    path = OUT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def shadow_filter(p, sd=".9", op=".6", dx=LIGHT_DX, dy=LIGHT_DY, name="ds"):
    return (f'<filter id="{p}{name}" x="-25%" y="-25%" width="150%" height="150%" color-interpolation-filters="sRGB">'
            f'<feDropShadow dx="{f(dx)}" dy="{f(dy)}" stdDeviation="{sd}" flood-color="{KEY}" flood-opacity="{op}"/></filter>')


def blur_filter(p, name, sd, ext=128):
    return (f'<filter id="{p}{name}" filterUnits="userSpaceOnUse" x="-20" y="-20" width="{ext + 40}" height="{ext + 40}" '
            f'color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="{f(sd)}"/></filter>')


def incised(d, w=.5, op=.8, light_op=.45, dark=ENGRAVE, light=B_SPEC):
    """An engraved line: a fine dark groove, and its lower-right wall catching the light."""
    return (f'<path d="{d}" fill="none" stroke="{light}" stroke-opacity="{light_op}" stroke-width="{f(w * .6)}" transform="translate(.25 .3)" stroke-linecap="round" stroke-linejoin="round"/>'
            f'<path d="{d}" fill="none" stroke="{dark}" stroke-opacity="{op}" stroke-width="{f(w)}" stroke-linecap="round" stroke-linejoin="round"/>')


# =====================================================================================================================
# FACES: the well (r 52.4) and the emblem. Each face builder returns (defs, under, over).
# =====================================================================================================================

def plate(p, sky=None):
    """The astrolabe plate in the well: Prussian-blue lacquer (or a scene's sky gradient), a faint glaze sheen toward the
    light, and the soft shadow any raised rim casts on the well's upper-left edge (so the face looks set in whatever kit
    frames it)."""
    d = [f'<clipPath id="{p}wc"><circle cx="64" cy="64" r="{f(R_FACE)}"/></clipPath>',
         f'<radialGradient id="{p}sh" cx="46" cy="40" r="42" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fff" stop-opacity=".07"/>'
         f'<stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>',
         f'<filter id="{p}ib" x="-10%" y="-10%" width="120%" height="120%" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="1.2"/></filter>',
         f'<mask id="{p}im"><rect width="128" height="128" fill="#fff"/><circle cx="66.3" cy="67.2" r="{f(R_FACE)}" fill="#000"/></mask>']
    if sky:
        stops, y0, y1 = sky
        d.append(f'<linearGradient id="{p}wl" x1="0" y1="{f(y0)}" x2="0" y2="{f(y1)}" gradientUnits="userSpaceOnUse">'
                 + "".join(f'<stop offset="{o}" stop-color="{c}"/>' for o, c in stops) + '</linearGradient>')
    else:
        d.append(f'<radialGradient id="{p}wl" cx="58" cy="54" r="64" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{PLATE_C}"/>'
                 f'<stop offset="1" stop-color="{PLATE_E}"/></radialGradient>')
    base = [f'<circle cx="64" cy="64" r="{f(R_FACE)}" fill="url(#{p}wl)"/>']
    over = [f'<circle cx="64" cy="64" r="{f(R_FACE)}" fill="url(#{p}sh)"/>',
            f'<g clip-path="url(#{p}wc)"><circle cx="64" cy="64" r="{f(R_FACE + 1)}" fill="{KEY}" fill-opacity=".6" mask="url(#{p}im)" filter="url(#{p}ib)"/></g>']
    return d, base, over


def engraved_chart(stars, links, w=.6, op=.5, pit=.7):
    """A star chart engraved into a surface: incised hairlines and brass-inlaid pits. Engraving, so nothing glows."""
    lines = "".join(f"M{f(stars[a][0])} {f(stars[a][1])}L{f(stars[b][0])} {f(stars[b][1])}" for a, b in links)
    pits = "".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}"/>' for x, y, r in stars)
    lit = "".join(f'<circle cx="{f(x + .3)}" cy="{f(y + .35)}" r="{f(r * .55)}"/>' for x, y, r in stars)
    return (incised(lines, w, op, op * .6, light=CHART) + f'<g fill="{ENGRAVE}" fill-opacity="{f(pit)}">{pits}</g>'
            f'<g fill="{CHART}" fill-opacity="{f(pit + .15)}">{lit}</g>')


def constellation(stars, links, line_op=.38, star_op=.8, w=.5):
    """Stars in an open sky: hairlines between them and round points with a soft glow."""
    lines = "".join(f"M{f(stars[a][0])} {f(stars[a][1])}L{f(stars[b][0])} {f(stars[b][1])}" for a, b in links)
    glow = "".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r * 2.4)}"/>' for x, y, r in stars)
    dots = "".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}"/>' for x, y, r in stars)
    return (f'<path d="{lines}" fill="none" stroke="{CHART}" stroke-opacity="{line_op}" stroke-width="{f(w)}" stroke-linecap="round"/>'
            f'<g fill="{STAR_GLOW}" fill-opacity="{f(star_op * .16)}">{glow}</g><g fill="{STAR}" fill-opacity="{star_op}">{dots}</g>')


def maria(p, clip, blobs, sd, color=MOON_LOW, transform="", hatch_op=.38):
    """The engraved silver moon. Every tier: the maria as smooth blurred tone. Hero tier: the tone is lighter and the
    maria are cut as fine parallel hatching (2-unit pitch, one direction for the whole set), faded in through the same
    blurred shapes, so they read as an engraver's selenography, never as spots or craters."""
    t = f' transform="{transform}"' if transform else ""
    els = lambda k: "".join(f'<ellipse cx="{f(x)}" cy="{f(y)}" rx="{f(rx)}" ry="{f(ry)}" transform="rotate({f(a)} {f(x)} {f(y)})" fill-opacity="{f(min(1, float(o) * k))}"/>'
                            for x, y, rx, ry, a, o in blobs)
    D = [blur_filter(p, "mb", sd)]
    hatch = HERO and HATCH != "off"
    smooth = (not hatch) or HATCH == "auto"
    S = []
    if smooth:      # the row tier's smooth tone (all of row, 48 and 64 px)
        cls = ' class="v7o-sm"' if hatch else ""
        S.append(f'<g{cls} clip-path="url(#{clip})"><g filter="url(#{p}mb)"><g fill="{color}"{t}>{els(1)}</g></g></g>')
    if hatch:       # 96 px and up: lighter tone, and the maria cut as hatching
        cls = ' class="v7o-hx"' if HATCH == "auto" else ""
        S.append(f'<g{cls}><g clip-path="url(#{clip})"><g filter="url(#{p}mb)"><g fill="{color}"{t}>{els(.6)}</g></g></g>')
        if HATCH == "auto":
            D.append(HATCH_STYLE)
        D.append(f'<pattern id="{p}hp" patternUnits="userSpaceOnUse" width="2" height="2" patternTransform="rotate(-32)">'
                 f'<path d="M0 1H2" stroke="{HATCH}" stroke-width=".5"/></pattern>')
        D.append(f'<mask id="{p}hm" maskUnits="userSpaceOnUse" x="0" y="0" width="128" height="128"><g filter="url(#{p}mb)"><g fill="#fff"{t}>{els(2.6)}</g></g></mask>')
        S.append(f'<g clip-path="url(#{clip})"><rect width="128" height="128" fill="url(#{p}hp)" opacity="{f(hatch_op)}" mask="url(#{p}hm)"/></g></g>')
    return D, S


CRESCENT_MARIA = lambda cx, cy, r, op: [(cx + 0.72 * r, cy - 0.40 * r, 0.15 * r, 0.12 * r, 0, f(0.22 * op)),
                                        (cx + 0.68 * r, cy + 0.08 * r, 0.12 * r, 0.19 * r, 0, f(0.19 * op)),
                                        (cx + 0.52 * r, cy + 0.40 * r, 0.11 * r, 0.12 * r, 0, f(0.15 * op)),
                                        (cx + 0.50 * r, cy - 0.12 * r, 0.14 * r, 0.12 * r, 0, f(0.11 * op))]


def crescent(p, cx, cy, r, k, rot, body=(MOON_MID, MOON, MOON_HI), soft=0.8, term=MOON_LOW, limb_c=MOON_HI,
             mar=True, mar_op=1.0, shadow=None):
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
        dd, ss = maria(p, f"{p}lc", CRESCENT_MARIA(cx, cy, r, mar_op), max(r * 0.06, 0.8), transform=tr)
        D += dd; S += ss
    S.append(f'<g clip-path="url(#{p}lc)"><g filter="url(#{p}sb)"><g{t}>'
             f'<path d="M{f(cx)} {f(cy - r)}A{f(rx)} {f(r)} 0 0 1 {f(cx)} {f(cy + r)}" fill="none" stroke="{term}" stroke-width="{f(r * 0.09)}" stroke-opacity=".8"/></g>'
             f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - r * 0.04)}" fill="none" stroke="{limb_c}" stroke-width="{f(r * 0.08)}"/></g></g>')
    return D, S


def dark_side_chart(p, cx, cy, r, k, rot, side, stars, links, op=.5):
    """The star chart engraved on a moon's dark side (hero only): masked to the disc minus its lit part."""
    if not HERO:
        return [], []
    dpath, tr = phase(cx, cy, r, k, side, rot)
    t = f' transform="{tr}"' if tr else ""
    D = [f'<mask id="{p}dsm" maskUnits="userSpaceOnUse" x="0" y="0" width="128" height="128"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - .8)}" fill="#fff"/>'
         f'<path d="{dpath}"{t} fill="#000" stroke="#000" stroke-width="2.4"/></mask>']
    return D, [f'<g mask="url(#{p}dsm)">' + engraved_chart(stars, links, op=op, pit=.6) + '</g>']


# ---------------- Ready and In journal: the moon road ----------------
GLYPH_MOON = (49.0, 42.0, 29.0, -0.18, 28.0)
GLYPH_H = 80.0
GLYPH_LIT_C = centroid(*GLYPH_MOON[:4], "right", GLYPH_MOON[4])
SUN_X = GLYPH_MOON[0] + (GLYPH_H - GLYPH_MOON[1]) / math.tan(math.radians(GLYPH_MOON[4]))
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
SKY_STARS = [(79.5, 22.0, 1.15), (88.5, 27.5, .9), (97.0, 24.0, 1.0), (99.5, 36.0, .85), (90.0, 41.5, 1.2)]
SKY_LINKS = [(0, 1), (1, 2), (2, 3), (3, 4), (4, 1)]
# the chart on the crescent's dark side (Ready on another job, In journal)
DS_STARS = [(31.5, 30.0, 1.15), (39.5, 22.5, .95), (44.0, 35.5, 1.1), (35.0, 45.0, .9), (42.5, 55.0, 1.0)]
DS_LINKS = [(0, 1), (1, 2), (2, 0), (2, 3), (3, 4)]


def limb_scale(p):
    """Ready's signature (hero only): the sighting. A hairline limb scale engraved along the sky's edge, 6-degree
    divisions with a longer mark every 30, and one small brass index at the moon's azimuth: the instrument has the moon
    in its sight. Partial (the sky only) and at .3, so it is an instrument's scale, never a ray ring."""
    if not HERO:
        return []
    ticks = []
    for a in range(198, 343, 6):
        r0 = 47.0 if a % 30 == 0 else 48.8
        x0, y0 = pt(r0, a); x1, y1 = pt(51.2, a)
        ticks.append(f"M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}")
    arc0, arc1 = pt(51.2, 198), pt(51.2, 342)
    rule = f"M{f(arc0[0])} {f(arc0[1])}A51.2 51.2 0 0 1 {f(arc1[0])} {f(arc1[1])}"
    cx, cy = GLYPH_LIT_C
    az = math.degrees(math.atan2(cy - 64, cx - 64))
    i1, i2, tip = pt(51.0, az - 2.2), pt(51.0, az + 2.2), pt(47.6, az)
    return [f'<g opacity=".3">' + incised("".join(ticks) + rule, .4, 1, .6, dark="#241C3A", light="#FFE9D6") + '</g>',
            f'<path d="{poly([i1, tip, i2])}" fill="{B_LIGHT}" fill-opacity=".85" stroke="{ENGRAVE}" stroke-opacity=".5" stroke-width=".3"/>']


def moon_scene(p, night=False):
    D, S = [], []
    H = GLYPH_H
    mx, my, mr, mk, rot = GLYPH_MOON
    cxr, cyr = GLYPH_LIT_C
    if night:
        sky = ((("0", NI_SKY_T), ("1", NI_SKY_H)), 12, H)
        sea_h, sea_b, sky_h = NI_SEA_H, NI_SEA_B, NI_SKY_H
    else:
        sky = (DAWN_SKY, 12, H)
        sea_h, sea_b, sky_h = DAWN_SEA_H, DAWN_SEA_B, DAWN_SKY[-1][1]
    dd, base, over = plate(p, sky); D += dd; S += base
    D.append(f'<linearGradient id="{p}sea" x1="0" y1="{f(H)}" x2="0" y2="117" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{sea_h}"/><stop offset="1" stop-color="{sea_b}"/></linearGradient>')
    D.append(f'<clipPath id="{p}skc"><rect x="0" y="0" width="128" height="{f(H)}"/></clipPath>')
    D.append(f'<clipPath id="{p}sec"><rect x="0" y="{f(H)}" width="128" height="{f(128 - H)}"/></clipPath>')
    S.append(f'<g clip-path="url(#{p}wc)">')
    S.append(f'<rect x="0" y="{f(H)}" width="128" height="{f(128 - H)}" fill="url(#{p}sea)"/>')
    D.append(f'<linearGradient id="{p}sr" x1="0" y1="{f(H)}" x2="0" y2="{f(H + 12)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{sky_h}" stop-opacity="{".5" if not night else ".3"}"/><stop offset="1" stop-color="{sky_h}" stop-opacity="0"/></linearGradient>')
    S.append(f'<rect x="0" y="{f(H)}" width="128" height="12" fill="url(#{p}sr)"/>')
    if not night:
        # the sun is just under the horizon, below the lit limb's direction: the dawn glow is brightest there, and the
        # sea mirrors it directly below
        D.append(f'<radialGradient id="{p}ag" cx="{f(SUN_X)}" cy="{f(H)}" r="1" gradientUnits="userSpaceOnUse" '
                 f'gradientTransform="translate({f(SUN_X)} {f(H)}) scale(60 34) translate({f(-SUN_X)} {f(-H)})">'
                 f'<stop offset="0" stop-color="{DAWN_SUN}" stop-opacity=".75"/><stop offset=".55" stop-color="{DAWN_SUN}" stop-opacity=".22"/>'
                 f'<stop offset="1" stop-color="{DAWN_SUN}" stop-opacity="0"/></radialGradient>')
        S.append(f'<g clip-path="url(#{p}skc)"><rect width="128" height="{f(H)}" fill="url(#{p}ag)"/></g>')
        D.append(f'<radialGradient id="{p}agr" cx="{f(SUN_X)}" cy="{f(H)}" r="1" gradientUnits="userSpaceOnUse" '
                 f'gradientTransform="translate({f(SUN_X)} {f(H)}) scale(46 10) translate({f(-SUN_X)} {f(-H)})">'
                 f'<stop offset="0" stop-color="{DAWN_SUN}" stop-opacity=".38"/><stop offset="1" stop-color="{DAWN_SUN}" stop-opacity="0"/></radialGradient>')
        S.append(f'<g clip-path="url(#{p}sec)"><rect y="{f(H)}" width="128" height="20" fill="url(#{p}agr)"/></g>')
    else:
        S.append(f'<g clip-path="url(#{p}skc)">' + (constellation(SKY_STARS, SKY_LINKS, line_op=.42, star_op=.85) if HERO else "") + '</g>')
    D.append(f'<radialGradient id="{p}bl" cx="{f(cxr)}" cy="{f(cyr)}" r="40" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity="{".24" if not night else ".12"}"/>'
             f'<stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    S.append(f'<g clip-path="url(#{p}skc)"><rect width="128" height="{f(H)}" fill="url(#{p}bl)"/></g>')
    if night:
        S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#38587E" fill-opacity=".3"/>')
        dd, ss = dark_side_chart(p, mx, my, mr, mk, rot, "right", DS_STARS, DS_LINKS, op=.45); D += dd; S += ss
        dd, ss = crescent(p, mx, my, mr, mk, rot, body=(MOON_LOW, MOON_MID, MOON), term=MOON_DEEP, limb_c=MOON, mar_op=0.8)
    else:
        dd, ss = crescent(p, mx, my, mr, mk, rot)
    D += dd; S += ss
    D.append(f'<linearGradient id="{p}hz" x1="12" y1="0" x2="116" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".06"/>'
             f'<stop offset="{f((cxr - 12) / 104)}" stop-color="{MOON}" stop-opacity="{".7" if not night else ".4"}"/><stop offset="1" stop-color="{MOON}" stop-opacity=".06"/></linearGradient>')
    S.append(f'<rect x="10" y="{f(H - 0.5)}" width="108" height="1" fill="url(#{p}hz)"/>')
    D.append(f'<radialGradient id="{p}col" cx="{f(cxr)}" cy="118" r="1" gradientUnits="userSpaceOnUse" '
             f'gradientTransform="translate({f(cxr)} 118) scale(22 40) translate({f(-cxr)} -118)">'
             f'<stop offset="0" stop-color="{MOON}" stop-opacity="{".26" if not night else ".12"}"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    S.append(f'<g clip-path="url(#{p}sec)"><rect y="{f(H)}" width="128" height="{f(128 - H)}" fill="url(#{p}col)"/></g>')
    road_col = MOON if not night else MOON_MID
    cores = []
    for y, rh, dashes in GLYPH_ROWS:
        for x0, x1, o, hs in dashes:
            h = rh * hs
            S.append(f'<path d="{streak(cxr + x0, cxr + x1, y, h)}" fill="{road_col}" fill-opacity="{f(o)}"/>')
            L = x1 - x0
            if h >= 2.5 and L >= 12:
                cores.append(f'<path d="{streak(cxr + x0 + L * .18, cxr + x1 - L * .18, y, h * .45, skew=.5)}" fill="{road_col}" fill-opacity="{f(o * .3)}"/>')
    D.append(blur_filter(p, "cb", .5))
    S.append(f'<g filter="url(#{p}cb)">{"".join(cores)}</g>')
    if not night:
        S += limb_scale(p)
    S.append('</g>')
    S += over
    return D, S


def face_ready():
    p = "v7or-"
    D, S = moon_scene(p)
    return D, S, []


def face_in_journal():
    p = "v7oj-"
    D, S = moon_scene(p, night=True)
    # the bookmark (over layer): ember silk wrapping behind the rim along r 66, over the lip and the face, down the step
    x0, w, y1 = 24.5, 17.5, 76.0
    ya = 64 - math.sqrt(66 ** 2 - (64 - x0) ** 2)
    yb = 64 - math.sqrt(66 ** 2 - (64 - x0 - w) ** 2)
    rib = f"M{f(x0)} {f(ya)}A66 66 0 0 1 {f(x0 + w)} {f(yb)}V{f(y1)}L{f(x0 + w / 2)} {f(y1 - 7)}L{f(x0)} {f(y1)}Z"
    D.append(f'<linearGradient id="{p}rb" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="{EMBER_HI}"/><stop offset=".3" stop-color="{EMBER}"/>'
             f'<stop offset="1" stop-color="{EMBER_DEEP}"/></linearGradient>')
    D.append(f'<clipPath id="{p}rc"><path d="{rib}"/></clipPath>')
    D.append(f'<filter id="{p}rs" x="-30%" y="-10%" width="160%" height="120%" color-interpolation-filters="sRGB">'
             f'<feDropShadow dx="1.2" dy="1.0" stdDeviation="1.1" flood-color="{KEY}" flood-opacity=".6"/></filter>')
    O = [f'<g filter="url(#{p}rs)"><path d="{rib}" fill="url(#{p}rb)" stroke="{KEY}" stroke-width="2.2" stroke-linejoin="round" paint-order="stroke"/></g>',
         f'<g clip-path="url(#{p}rc)">'
         f'<path d="{ring(R_KEY, 70)}" fill="{EMBER_DEEP}" fill-rule="evenodd"/>'
         f'<path d="{ring(R_LIP_I, R_LIP_O)}" fill="{EMBER_HI}" fill-opacity=".4" fill-rule="evenodd"/>'
         f'<path d="{ring(R_FACE - 1.5, R_TOP_I)}" fill="{KEY}" fill-opacity=".32" fill-rule="evenodd"/></g>']
    if HERO:
        O.append(f'<g opacity=".5"><path d="M{f(x0 + 2.4)} 14V{f(y1 - 5.5)}M{f(x0 + w - 2.4)} 14V{f(y1 - 4)}" stroke="{EMBER_HI}" stroke-width=".55" stroke-dasharray="1.6 1.2"/></g>')
    return D, S, O


def face_ready_other_job():
    p = "v7oo-"
    D, S = [shadow_filter(p)], []
    dd, base, over = plate(p); D += dd; S += base + over
    mx, my, mr, mk, rot = GLYPH_MOON
    # Ready's crescent at Ready's place and tilt, resting on the night plate: no sea, no road. Earthshine shows the
    # disc, and its dark side carries the engraved chart.
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#2B4C72" fill-opacity=".38"/>')
    dd, ss = dark_side_chart(p, mx, my, mr, mk, rot, "right", DS_STARS, DS_LINKS); D += dd; S += ss
    dd, ss = crescent(p, mx, my, mr, mk, rot, shadow=f"{p}ds"); D += dd; S += ss
    return D, S, []


# ---------------- Blocked: a charted new moon behind a patinated brass scroll-cloud ----------------
BLK_MOON = (69.0, 45.0, 27.0)
BLK_LIMB = (-0.62, -26.0)          # k -0.62, as Medallion's Blocked, so the two match in a mix
BLK_STARS = [(50.5, 37.0, 1.4), (59.0, 27.5, 1.1), (66.0, 39.5, 1.3), (57.0, 49.0, 1.05), (71.0, 53.0, 1.3)]
BLK_LINKS = [(0, 1), (1, 2), (2, 3), (3, 4), (3, 0)]
KUMO = ([(28.0, 72.0, 8.5), (41.0, 63.0, 11.5), (56.0, 59.0, 12.0), (70.5, 62.0, 10.0), (82.5, 68.5, 7.5)],
        (19.0, 93.0, 66.0, 84.0))


def kumo(p, bumps, base):
    """A scroll-cloud in raised, patinated brass: an old brass that has darkened, so it obscures the moon without
    becoming the loudest mass (gilt means act now). Lit from the upper left, with the patina's highlight only on the
    upper-left scroll crests, a shaded lower-right bevel, engraved scroll spirals, and a soft cast shadow."""
    x0, x1, yt, yb = base
    m = ("".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}"/>' for x, y, r in bumps)
         + f'<rect x="{f(x0)}" y="{f(yt)}" width="{f(x1 - x0)}" height="{f(yb - yt)}" rx="{f((yb - yt) / 2)}"/>')
    tail = []
    for i in range(25):
        t = i / 24
        x = (1 - t) ** 2 * (x0 + 8) + 2 * (1 - t) * t * (x0 - 2) + t * t * (x0 - 6.5)
        y = (1 - t) ** 2 * (yb - 2.2) + 2 * (1 - t) * t * (yb + 1.2) + t * t * (yb - 3.5)
        tail.append((x, y, 3.4 * (1 - t) ** .8))
    m += '<path d="' + poly([(x, y - w / 2) for x, y, w in tail] + [(x, y + w / 2) for x, y, w in tail[::-1]]) + '"/>'
    ytop = min(y - r for x, y, r in bumps)
    crests = "".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}"/>' for x, y, r in bumps)
    D = [f'<linearGradient id="{p}kg" x1="{f(x0)}" y1="{f(ytop)}" x2="{f(x1)}" y2="{f(yb)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{PATINA}"/><stop offset="1" stop-color="{PATINA_LOW}"/></linearGradient>',
         f'<clipPath id="{p}kc">{m}</clipPath>',
         # the highlight: the upper-left bevel of the scroll crests only, fading out toward the lower right
         f'<linearGradient id="{p}kf" x1="{f(x0)}" y1="{f(ytop)}" x2="{f(x1 - 10)}" y2="{f(yt + 4)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="#fff"/><stop offset=".7" stop-color="#fff" stop-opacity=".35"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>',
         f'<mask id="{p}kL" maskUnits="userSpaceOnUse" x="0" y="0" width="128" height="128"><g fill="url(#{p}kf)">{crests}</g>'
         f'<g fill="#000" transform="translate(.85 1.1)">{m}</g><rect x="0" y="{f(yt + 1)}" width="128" height="60" fill="#000"/></mask>',
         f'<mask id="{p}kS"><g fill="#fff">{m}</g><g fill="#000" transform="translate(-1.1 -1.4)">{m}</g></mask>',
         blur_filter(p, "ks", 1.2), blur_filter(p, "kb", .3)]
    curls = []
    for x, y, r in bumps[1:-1]:
        cx_, cy_ = x + .08 * r, y + .12 * r
        pts = [pt(r * (.66 - .5 * i / 60), 175.0 + 450.0 * i / 60, cx_, cy_) for i in range(61)]
        curls.append("M" + "L".join(f"{f(px)} {f(py)}" for px, py in pts))
    for (x, y, r), (a0, a1) in ((bumps[0], (150.0, 330.0)), (bumps[-1], (210.0, 390.0))):
        pts = [pt(r * (.6 - .25 * i / 30), a0 + (a1 - a0) * i / 30, x, y + .1 * r) for i in range(31)]
        curls.append("M" + "L".join(f"{f(px)} {f(py)}" for px, py in pts))
    S = [f'<g fill="{KEY}" fill-opacity=".6" filter="url(#{p}ks)" transform="translate(1.5 2.0)">{m}</g>',
         f'<g fill="url(#{p}kg)" stroke="{KEY}" stroke-width="1.1" paint-order="stroke">{m}</g>',
         f'<rect width="128" height="128" fill="{PATINA_DARK}" fill-opacity=".75" mask="url(#{p}kS)"/>',
         f'<rect width="128" height="128" fill="{PATINA_HI}" fill-opacity=".9" mask="url(#{p}kL)" filter="url(#{p}kb)"/>']
    if HERO:
        S.append(f'<g clip-path="url(#{p}kc)">' + incised("".join(curls), .6, .75, .35, dark=PATINA_DARK, light=PATINA_HI) + '</g>')
    return D, S, m


def face_blocked():
    """A new moon (an ashen disc barely lighter than the plate, with a thin sunlit limb) whose dark face carries an
    engraved constellation, half hidden behind a patinated brass scroll-cloud."""
    p = "v7ob-"
    D, S = [shadow_filter(p)], []
    dd, base, over = plate(p); D += dd; S += base + over
    mx, my, mr = BLK_MOON
    D.append(f'<radialGradient id="{p}as" cx="{f(mx - 9)}" cy="{f(my - 10)}" r="{f(mr * 1.5)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="#22305A"/><stop offset="1" stop-color="#1A2448"/></radialGradient>')
    D.append(f'<clipPath id="{p}dc"><circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}"/></clipPath>')
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="url(#{p}as)" filter="url(#{p}ds)"/>')
    if HERO:
        S.append(f'<g clip-path="url(#{p}dc)">' + engraved_chart(BLK_STARS, BLK_LINKS, op=.5, pit=.55) + '</g>')
    dd, ss = crescent(p, mx, my, mr, *BLK_LIMB, body=(MOON_LOW, MOON_MID, MOON), term=MOON_DEEP, limb_c=MOON, mar=False)
    D += dd; S += ss
    dd, ss, _ = kumo(p, *KUMO); D += dd; S += ss
    return D, S, []


# ---------------- Done this cycle: a waning half and a graduated scale arc ----------------

def spiral_r(r0, r1, a0, a1, a):
    return r0 + (r1 - r0) * (a - a0) / (a1 - a0)


def scale_arc(cx, cy, r0, r1, a0, a1, w, head_len, head_w, n=90):
    """'Comes back', drawn as a short graduated limb segment: a flat band of constant width with a square-cut tail,
    spiralling out (never concentric with the rim) to an astrolabe star-pointer head. No bead anywhere on it."""
    outer, inner = [], []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        rm = spiral_r(r0, r1, a0, a1, a)
        outer.append(pt(rm + w / 2, a, cx, cy))
        inner.append(pt(rm - w / 2, a, cx, cy))
    da = math.degrees(head_len / r1)
    head = [pt(r1 + head_w / 2, a1, cx, cy),
            pt(r1 + head_w * .26, a1 + da * .38, cx, cy), pt(r1 + .5, a1 + da * .8, cx, cy), pt(r1, a1 + da, cx, cy),
            pt(r1 - .5, a1 + da * .8, cx, cy), pt(r1 - head_w * .26, a1 + da * .38, cx, cy),
            pt(r1 - head_w / 2, a1, cx, cy)]
    return poly(outer + head + inner[::-1])


def spiral_taper(cx, cy, r0, r1, a0, a1, off, b0, b1, w, n=40):
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        a = b0 + (b1 - b0) * t
        rm = spiral_r(r0, r1, a0, a1, a) + off
        hw = w / 2 * math.sin(math.pi * t)
        outer.append(pt(rm + hw, a, cx, cy))
        inner.append(pt(rm - hw, a, cx, cy))
    return poly(outer + inner[::-1])


DONE_DS_STARS = [(70.5, 52.0, 1.0), (77.5, 60.0, .9), (71.5, 70.0, 1.05), (79.0, 75.5, .85)]
DONE_DS_LINKS = [(0, 1), (1, 2), (2, 3)]


def face_done():
    p = "v7od-"
    D, S = [shadow_filter(p)], []
    dd, base, over = plate(p); D += dd; S += base + over
    mx, my, mr = 64.0, 64.0, 22.5
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="#2A4468" filter="url(#{p}ds)"/>')
    dd, ss = dark_side_chart(p, mx, my, mr, 0.0, 0.0, "left", DONE_DS_STARS, DONE_DS_LINKS); D += dd; S += ss
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
    r0, r1, a0, a1, w = 33.5, 39.5, 140.0, 378.0, 6.6
    arc = scale_arc(64, 64, r0, r1, a0, a1, w, 15.5, 17.5)
    sp = lambda off, b0, b1, bw: spiral_taper(64, 64, r0, r1, a0, a1, off, b0, b1, bw)
    D.append(f'<linearGradient id="{p}ag" x1="20" y1="18" x2="104" y2="106" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{B_HI}"/>'
             f'<stop offset=".5" stop-color="{B_LIGHT}"/><stop offset="1" stop-color="{BRASS}"/></linearGradient>')
    D.append(f'<clipPath id="{p}ac"><path d="{arc}"/></clipPath>')
    S.append(f'<g filter="url(#{p}ds)"><path d="{arc}" fill="url(#{p}ag)" stroke="{KEY}" stroke-width="1.3" stroke-linejoin="round" paint-order="stroke"/></g>')
    g = [f'<g clip-path="url(#{p}ac)"><path d="{sp(w / 2 - .5, 175, 268, 1.4)}" fill="{B_SPEC}" fill-opacity=".9"/>'
         f'<path d="{sp(-w / 2 + .4, 320, 376, 1.0)}" fill="{B_SPEC}" fill-opacity=".4"/>'
         f'<path d="{sp(-w / 2 + .5, 175, 268, 1.2)}" fill="{B_DEEP}" fill-opacity=".5"/>']
    if HERO:
        # the graduations: a division every 15 degrees across the inner half of the band, a full-width one every 45
        ticks = []
        for a in range(150, 376, 15):
            rm = spiral_r(r0, r1, a0, a1, a)
            ri = rm - w / 2 + .6
            ro = rm + w / 2 - .6 if (a - 150) % 45 == 0 else rm + .2
            (xa, ya), (xb, yb) = pt(ri, a), pt(ro, a)
            ticks.append(f"M{f(xa)} {f(ya)}L{f(xb)} {f(yb)}")
        g.append(incised("".join(ticks), .45, .7, .4))
        # the square-cut tail's end rule
        (xa, ya), (xb, yb) = pt(r0 - w / 2 + .5, a0 + 1.6), pt(r0 + w / 2 - .5, a0 + 1.6)
        g.append(incised(f"M{f(xa)} {f(ya)}L{f(xb)} {f(yb)}", .5, .7, .4))
    g.append('</g>')
    S += g
    return D, S, []


# ---------------- Completed: the engraved full moon and the gilt check ----------------

def full_moon(p, cx, cy, r, stops, limb_op=.95):
    D = [f'<radialGradient id="{p}fc" cx="{f(cx - .38 * r)}" cy="{f(cy - .42 * r)}" r="{f(1.55 * r)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{stops[0]}"/><stop offset=".55" stop-color="{stops[1]}"/><stop offset="1" stop-color="{stops[2]}"/></radialGradient>',
         f'<clipPath id="{p}fcl"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}"/></clipPath>',
         f'<linearGradient id="{p}lr" x1="0.2" y1="0.15" x2="0.8" y2="0.9"><stop offset="0" stop-color="{MOON_HI}" stop-opacity="{limb_op}"/>'
         f'<stop offset=".5" stop-color="{MOON_MID}" stop-opacity=".45"/><stop offset="1" stop-color="{MOON_MID}" stop-opacity="0"/></linearGradient>']
    S = [f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="url(#{p}fc)" filter="url(#{p}ds)"/>']
    u = r
    blobs = [(cx - .30 * u, cy - .36 * u, .30 * u, .22 * u, -18, ".44"), (cx + .12 * u, cy - .30 * u, .17 * u, .15 * u, 0, ".42"),
             (cx + .28 * u, cy - .02 * u, .22 * u, .17 * u, 20, ".40"), (cx + .70 * u, cy - .18 * u, .10 * u, .08 * u, 0, ".44"),
             (cx + .56 * u, cy + .24 * u, .10 * u, .17 * u, -15, ".34"), (cx + .26 * u, cy + .36 * u, .09 * u, .10 * u, 0, ".30"),
             (cx - .22 * u, cy + .30 * u, .17 * u, .13 * u, 10, ".30"), (cx - .58 * u, cy - .02 * u, .22 * u, .36 * u, 8, ".32")]
    dd, ss = maria(p, f"{p}fcl", blobs, max(r * .07, .8), color="#7C89AE", hatch_op=.42); D += dd; S += ss
    D.append(blur_filter(p, "lb", r * .022))
    S.append(f'<g clip-path="url(#{p}fcl)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - r * .05)}" fill="none" stroke="url(#{p}lr)" stroke-width="{f(r * .09)}" filter="url(#{p}lb)"/></g>')
    return D, S


def face_completed():
    p = "v7oc-"
    D, S = [shadow_filter(p)], []
    dd, base, over = plate(p); D += dd; S += base + over
    cx, cy, r = 60.0, 60.0, 35.0
    D.append(f'<radialGradient id="{p}gl" cx="{f(cx)}" cy="{f(cy)}" r="{f(r + 13)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="{f(r / (r + 13))}" stop-color="{STAR_GLOW}" stop-opacity=".2"/><stop offset="1" stop-color="{STAR_GLOW}" stop-opacity="0"/></radialGradient>')
    S.append(f'<g clip-path="url(#{p}wc)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r + 13)}" fill="url(#{p}gl)"/></g>')
    dd, ss = full_moon(p, cx, cy, r, stops=("#D8DFED", "#A9B5D0", "#7B8AAF")); D += dd; S += ss
    # the check (over layer): the shared gilt, struck out through the rim
    chk = "M64 86L78 100L117.5 40"
    D.append(f'<linearGradient id="{p}ck" x1="64" y1="40" x2="117" y2="100" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{G_HI}"/>'
             f'<stop offset=".5" stop-color="{G_LIGHT}"/><stop offset="1" stop-color="#A88B52"/></linearGradient>')
    O = [f'<g filter="url(#{p}ds)"><path d="{chk}" fill="none" stroke="{KEY}" stroke-width="15" stroke-linecap="round" stroke-linejoin="round"/>'
         f'<path d="{chk}" fill="none" stroke="url(#{p}ck)" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/></g>',
         f'<path d="M62.3 83.3L76.5 97.4L115.1 38.3" fill="none" stroke="{G_SPEC}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" opacity=".85"/>']
    if HERO:
        O.append(incised("M80.6 95.2L112.8 46.6", .45, .55, .3))
    return D, S, O


# ---------------- Locked out: rose Dalamud, broken apart ----------------

def ray_hit(ix, iy, ang, cx, cy, R):
    ux, uy = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    dx, dy = ix - cx, iy - cy
    b = dx * ux + dy * uy
    c = dx * dx + dy * dy - R * R
    t = -b + math.sqrt(b * b - c)
    return ix + ux * t, iy + uy * t


def face_locked_out():
    p = "v7ol-"
    D, S = [shadow_filter(p, sd=".8", op=".75")], []
    dd, base, over = plate(p); D += dd; S += base + over
    cx, cy, R = 64.0, 64.0, 38.0
    ix, iy = 58.0, 58.5
    angs = [-112.0, -38.0, 22.0, 96.0, 162.0]
    disp = [3.0, 3.4, 3.0, 7.5, 3.2]
    turn = [-2.0, 2.0, -1.5, 10.0, -2.5]
    jog = [2.2, -2.6, 2.4, -2.0, 2.4]
    n = len(angs)
    D.append(f'<linearGradient id="{p}rd" x1="{f(cx - R)}" y1="{f(cy - R)}" x2="{f(cx + R)}" y2="{f(cy + R)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{DAL_HI}"/><stop offset=".55" stop-color="{DAL}"/><stop offset="1" stop-color="{DAL_DEEP}"/></linearGradient>')
    D.append(f'<linearGradient id="{p}lr" x1="{f(cx - R)}" y1="{f(cy - R)}" x2="{f(cx + R)}" y2="{f(cy + R)}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{DAL_EDGE}" stop-opacity=".75"/><stop offset=".45" stop-color="{DAL_EDGE}" stop-opacity=".15"/><stop offset=".7" stop-color="{DAL_EDGE}" stop-opacity="0"/></linearGradient>')
    D.append(f'<clipPath id="{p}dc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}"/></clipPath>')
    D.append(blur_filter(p, "pm", 1.6))
    cracks = []
    for a, j in zip(angs, jog):
        e = ray_hit(ix, iy, a, cx, cy, R + 3)
        ux, uy = math.cos(math.radians(a)), math.sin(math.radians(a))
        L = math.hypot(e[0] - ix, e[1] - iy)
        m1 = (ix + ux * L * .36 - j * uy, iy + uy * L * .36 + j * ux)
        m2 = (ix + ux * L * .68 + j * .6 * uy, iy + uy * L * .68 - j * .6 * ux)
        cracks.append(((ix, iy), m1, m2, e))
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
        arc = [pt(R + 3, b0 + (b1 - b0) * k / 20, cx, cy) for k in range(21)]
        pts = [(ix, iy), c0[1], c0[2]] + arc + [c1[2], c1[1]]
        mid = math.radians((a0 + a1) / 2)
        tx, ty = disp[i] * math.cos(mid), disp[i] * math.sin(mid)
        gx = sum(x for x, _ in pts) / len(pts); gy = sum(y for _, y in pts) / len(pts)
        sid = f"{p}s{i}"
        D.append(f'<clipPath id="{sid}"><path d="{poly(pts)}"/></clipPath>')
        g = [f'<g clip-path="url(#{sid})"><g clip-path="url(#{p}dc)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="url(#{p}rd)"/>']
        if HERO:
            g.append(f'<g filter="url(#{p}pm)" fill="none" stroke="{DAL_DEEP}" stroke-opacity=".3" stroke-width="1.6">'
                     f'<path d="M{f(cx - R)} {f(cy + 8)}C{f(cx - 10)} {f(cy + 2)} {f(cx + 12)} {f(cy + 16)} {f(cx + R)} {f(cy + 6)}"/>'
                     f'<path d="M{f(cx - 14)} {f(cy - R)}C{f(cx - 4)} {f(cy - 10)} {f(cx - 20)} {f(cy + 12)} {f(cx - 8)} {f(cy + R)}"/></g>')
        g.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R - 1.3)}" fill="none" stroke="url(#{p}lr)" stroke-width="2.6"/>')
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
            if facing > 0.15:
                g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{DAL_EDGE}" stroke-width="{f(.9 + .9 * facing)}" stroke-opacity="{f(.4 + .4 * facing)}" stroke-linecap="round"/>')
            elif facing < -0.15:
                g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{KEY}" stroke-width="{f(.9 - .8 * facing)}" stroke-opacity=".55" stroke-linecap="round"/>')
        g.append('</g></g>')
        body.append(f'<g transform="translate({f(tx)} {f(ty)}) rotate({f(turn[i])} {f(gx)} {f(gy)})">{"".join(g)}</g>')
    if HERO:
        gap = [cracks[0][2], cracks[2][1], cracks[3][2]]
        S.append("".join(f'<circle cx="{f(gx)}" cy="{f(gy)}" r="2.2" fill="{STAR_GLOW}" fill-opacity=".18"/>'
                         f'<circle cx="{f(gx)}" cy="{f(gy)}" r=".85" fill="{STAR}" fill-opacity=".85"/>' for gx, gy in gap))
    S.append(f'<g clip-path="url(#{p}wc)"><g filter="url(#{p}ds)">{"".join(body)}</g></g>')
    return D, S, []


# ---------------- Not checked: the constellation "?" ----------------
Q_C = (64.0, 49.0, 19.0)
Q_LINE, Q_LINE_OP = 7.4, .9
Q_SIZES = [5.0, 5.6, 5.6, 5.4, 5.0, 5.4]
Q_DOT = 7.6


def q_stars():
    vx, vy, vr = Q_C
    bowl = [pt(vr, a, vx, vy) for a in (200.0, 258.0, 316.0, 14.0)]
    return bowl + [(vx + 2.0, vy + vr + 0.5), (vx, vy + vr + 11.5)], (vx, vy + vr + 28.0)


def face_not_checked():
    p = "v7on-"
    D, S = [], []
    dd, base, over = plate(p); D += dd; S += base + over
    vx, vy, vr = Q_C
    pts, dot = q_stars()
    line = "M" + "L".join(f"{f(x)} {f(y)}" for x, y in pts)
    D.append(f'<linearGradient id="{p}lg" x1="{f(vx - vr)}" y1="{f(vy - vr)}" x2="{f(vx + vr)}" y2="{f(dot[1])}" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{MOON_HI}"/><stop offset=".55" stop-color="{MOON_MID}"/><stop offset="1" stop-color="{MOON_LOW}"/></linearGradient>')
    D.append(blur_filter(p, "lgb", 2.2))
    S.append(f'<g filter="url(#{p}lgb)"><path d="{line}" fill="none" stroke="{STAR_GLOW}" stroke-width="{f(Q_LINE + 5)}" stroke-linecap="round" stroke-linejoin="round" stroke-opacity=".16"/></g>')
    S.append(f'<path d="{line}" fill="none" stroke="url(#{p}lg)" stroke-width="{f(Q_LINE)}" stroke-linecap="round" stroke-linejoin="round" stroke-opacity="{f(Q_LINE_OP)}"/>')
    D.append(f'<radialGradient id="{p}sg"><stop offset="0" stop-color="{STAR_GLOW}" stop-opacity=".5"/><stop offset="1" stop-color="{STAR_GLOW}" stop-opacity="0"/></radialGradient>')
    D.append(f'<radialGradient id="{p}sc"><stop offset="0" stop-color="#FFFFFF"/><stop offset=".6" stop-color="{MOON}"/>'
             f'<stop offset="1" stop-color="{MOON_MID}"/></radialGradient>')
    for (x, y), r in list(zip(pts, Q_SIZES)) + [(dot, Q_DOT)]:
        S.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r * 2.1)}" fill="url(#{p}sg)"/>')
        S.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="url(#{p}sc)"/>')
    return D, S, []


FACES = {"ready": face_ready, "ready-on-another-job": face_ready_other_job, "in-journal": face_in_journal,
         "blocked": face_blocked, "done-this-cycle": face_done, "completed": face_completed,
         "locked-out": face_locked_out, "not-checked": face_not_checked}


def build_face(state, hero, hatch="auto"):
    """(defs, under, over) for a state, the under layer clipped to the well (no pixel outside r 52.4: mixing rule R1)."""
    global HERO, HATCH
    HERO, HATCH = hero, hatch
    D, U, O = FACES[state]()
    HERO, HATCH = True, "auto"
    fid = f"v7of{STATES.index(state)}{'h' if hero else 'r'}-w"
    D = D + [f'<clipPath id="{fid}"><circle cx="64" cy="64" r="{f(R_FACE)}"/></clipPath>']
    return "".join(D), f'<g clip-path="url(#{fid})">' + "".join(U) + '</g>', "".join(O)


# =====================================================================================================================
# THE ASTROLABE KIT: frames (4 tiers x Full/Quiet), badge ring and seats, badge glyphs
# =====================================================================================================================

def frame(p, tier, finish="full", hero=True):
    """The astrolabe limb in a tier's metal. Full: a rounded outer lip (light upper-left), a groove, a flat top face of
    darker metal (the two-tone band), and a step down to the well (a recess wall: dark upper-left, lit lower-right).
    Hero only: an engraved double rule on the face, four cardinal notches (the moon's four quarters) and eight hairline
    marks between them (the twelve moons of the year), all inside the band. Quiet: a thin hairline ring."""
    lip, top, step, spec, spec_op, eng_op = TIERS[tier]
    g = 'x1="14" y1="10" x2="114" y2="118" gradientUnits="userSpaceOnUse"'
    if finish == "quiet":
        d = [f'<linearGradient id="{p}q" {g}><stop offset="0" stop-color="{lip[0]}"/><stop offset=".5" stop-color="{lip[1]}"/><stop offset="1" stop-color="{lip[3]}"/></linearGradient>']
        return d, [f'<path d="{ring(R_FACE - .8, R_FACE + 3.0)}" fill="{KEY}" fill-opacity=".9" fill-rule="evenodd"/>',
                   f'<path d="{ring(R_FACE, R_FACE + 1.8)}" fill="url(#{p}q)" fill-rule="evenodd"/>']
    d = [f'<linearGradient id="{p}bo" {g}><stop offset="0" stop-color="{lip[0]}"/><stop offset=".4" stop-color="{lip[1]}"/><stop offset=".78" stop-color="{lip[2]}"/>'
         f'<stop offset="1" stop-color="{lip[3]}"/></linearGradient>',
         f'<linearGradient id="{p}bf" {g}><stop offset="0" stop-color="{top[0]}"/><stop offset=".32" stop-color="{top[1]}"/><stop offset=".66" stop-color="{top[2]}"/>'
         f'<stop offset="1" stop-color="{top[3]}"/></linearGradient>',
         f'<linearGradient id="{p}bs" {g}><stop offset="0" stop-color="{step[0]}"/><stop offset=".55" stop-color="{step[1]}"/><stop offset="1" stop-color="{step[2]}"/></linearGradient>',
         blur_filter(p, "bb", 1.2)]
    s = [f'<path d="{ring(R_FACE - .8, R_KEY)}" fill="{KEY}" fill-opacity=".92" fill-rule="evenodd"/>',
         f'<path d="{ring(R_LIP_I, R_LIP_O)}" fill="url(#{p}bo)" fill-rule="evenodd"/>',
         f'<path d="{ring(R_TOP_O, R_LIP_I)}" fill="{KEY}" fill-opacity=".6" fill-rule="evenodd"/>',
         f'<path d="{ring(R_TOP_I, R_TOP_O)}" fill="url(#{p}bf)" fill-rule="evenodd"/>',
         f'<g filter="url(#{p}bb)"><path d="{taper_arc((R_TOP_I + R_TOP_O) / 2, -172, -98, 3.0)}" fill="{spec}" fill-opacity="{f(.32 * spec_op)}"/></g>',
         f'<path d="{ring(R_FACE, R_TOP_I)}" fill="url(#{p}bs)" fill-rule="evenodd"/>',
         f'<path d="{taper_arc((R_LIP_I + R_LIP_O) / 2 + .2, -166, -104, 1.3)}" fill="{spec}" fill-opacity="{f(spec_op)}"/>',
         f'<path d="{taper_arc((R_FACE + R_TOP_I) / 2, 18, 70, .8)}" fill="{spec}" fill-opacity="{f(.5 * spec_op)}"/>']
    if hero:
        s += [incised(circ(R_RULE_O), .5, eng_op, eng_op * .55, light=spec), incised(circ(R_RULE_I), .5, eng_op, eng_op * .55, light=spec)]
        for a in range(-90, 270, 30):
            if a % 90 == 0:
                o1, o2 = pt(R_RULE_O, a - 1.9), pt(R_RULE_O, a + 1.9)
                tip = pt(R_RULE_I + .2, a)
                s.append(f'<path d="{poly([o1, tip, o2])}" fill="{ENGRAVE}" fill-opacity="{f(eng_op + .05)}"/>')
                wall = (o2, tip) if math.cos(math.radians(a)) + math.sin(math.radians(a)) > 0 else (o1, tip)
                s.append(f'<path d="M{f(wall[0][0])} {f(wall[0][1])}L{f(wall[1][0])} {f(wall[1][1])}" stroke="{spec}" stroke-opacity="{f(eng_op * .6)}" stroke-width=".35"/>')
            else:
                (xa, ya), (xb, yb) = pt(R_RULE_O, a), pt(R_RULE_O - 1.4, a)
                s.append(f'<g opacity=".55">' + incised(f"M{f(xa)} {f(ya)}L{f(xb)} {f(yb)}", .4, eng_op, eng_op * .5, light=spec) + '</g>')
    return d, s


BADGE = dict(cx=95.0, cy=95.0, r_key=24.0, r_out=23.1, r_in=19.9, icon=35.5)


def badge_ring(p, tier="resting"):
    """The badge rim at the shared slot: keyline r 24 with the drop shadow, a lip, a flat face and a step to the seat
    (r 19.9). Act-now (Ready) takes the shared gilt like the act-now frame."""
    lip, top, step, spec, spec_op, _ = TIERS[tier]
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    x0, y0, x1, y1 = cx - 24, cy - 24, cx + 24, cy + 24
    g = f'x1="{f(x0)}" y1="{f(y0)}" x2="{f(x1)}" y2="{f(y1)}" gradientUnits="userSpaceOnUse"'
    D = [f'<linearGradient id="{p}go" {g}><stop offset="0" stop-color="{lip[0]}"/><stop offset=".45" stop-color="{lip[1]}"/><stop offset="1" stop-color="{lip[3]}"/></linearGradient>',
         f'<linearGradient id="{p}gf" {g}><stop offset="0" stop-color="{top[0]}"/><stop offset=".5" stop-color="{top[1]}"/><stop offset="1" stop-color="{top[3]}"/></linearGradient>',
         f'<linearGradient id="{p}gi" {g}><stop offset="0" stop-color="{step[0]}"/><stop offset=".55" stop-color="{step[1]}"/><stop offset="1" stop-color="{step[2]}"/></linearGradient>',
         shadow_filter(p, sd="1.1", op=".65", dx=1.4, dy=1.9, name="bs")]
    S = [f'<g filter="url(#{p}bs)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_key"])}" fill="{KEY}"/></g>',
         f'<path d="{ring(22.2, b["r_out"], cx, cy)}" fill="url(#{p}go)" fill-rule="evenodd"/>',
         f'<path d="{ring(20.6, 22.0, cx, cy)}" fill="url(#{p}gf)" fill-rule="evenodd"/>',
         f'<path d="{ring(b["r_in"], 20.6, cx, cy)}" fill="url(#{p}gi)" fill-rule="evenodd"/>',
         f'<path d="{taper_arc(22.65, -168, -102, .8, cx, cy)}" fill="{spec}" fill-opacity="{f(spec_op)}"/>']
    return D, S


def badge_seat(p, hi, lo):
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    D = [f'<radialGradient id="{p}en" cx="{f(cx - 5)}" cy="{f(cy - 6)}" r="{f(b["r_in"] * 1.5)}" gradientUnits="userSpaceOnUse">'
         f'<stop offset="0" stop-color="{hi}"/><stop offset="1" stop-color="{lo}"/></radialGradient>',
         f'<clipPath id="{p}ec"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}"/></clipPath>',
         f'<mask id="{p}em"><rect width="128" height="128" fill="#fff"/><circle cx="{f(cx + 1.4)}" cy="{f(cy + 1.9)}" r="{f(b["r_in"])}" fill="#000"/></mask>',
         blur_filter(p, "eb", 0.8)]
    S = [f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}" fill="url(#{p}en)"/>',
         f'<g clip-path="url(#{p}ec)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"] + 1)}" fill="{KEY}" fill-opacity=".5" mask="url(#{p}em)" filter="url(#{p}eb)"/></g>']
    return D, S


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


def metal_defs(p, hi, mid, lo):
    return [f'<linearGradient id="{p}mf" x1="-12" y1="-14" x2="12" y2="12" gradientUnits="userSpaceOnUse">'
            f'<stop offset="0" stop-color="{hi}"/><stop offset=".5" stop-color="{mid}"/><stop offset="1" stop-color="{lo}"/></linearGradient>',
            f'<filter id="{p}ug" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">'
            f'<feDropShadow dx=".6" dy=".9" stdDeviation=".8" flood-color="{KEY}" flood-opacity=".55"/></filter>']


def bevel(p, n, shape, light, dark, w=1.0):
    D = [f'<mask id="{p}{n}bl"><g fill="#fff">{shape}</g><g fill="#000" transform="translate({f(w * .7)} {f(w * .9)})">{shape}</g></mask>',
         f'<mask id="{p}{n}bd"><g fill="#fff">{shape}</g><g fill="#000" transform="translate({f(-w * .7)} {f(-w * .9)})">{shape}</g></mask>']
    S = [f'<rect x="-20" y="-20" width="40" height="40" fill="{light}" fill-opacity=".85" mask="url(#{p}{n}bl)"/>',
         f'<rect x="-20" y="-20" width="40" height="40" fill="{dark}" fill-opacity=".7" mask="url(#{p}{n}bd)"/>']
    return D, S


LOCK_OY = -0.2


def lock_glyph(p, cx, cy, open_):
    if open_:
        hi, mid, lo, spec, shade = "#F6DC9A", B_LIGHT, "#916422", B_SPEC, B_DEEP
        shackle = "M-4.6 0V-11A4.6 4.6 0 0 1 4.6 -11V-5.5"
        eng = ENGRAVE
    else:
        hi, mid, lo, spec, shade = STEEL
        shackle = "M-4.6 0V-6A4.6 4.6 0 0 1 4.6 -6V0"
        eng = "#1E2440"
    t = f'transform="translate({f(cx)} {f(cy + LOCK_OY)})"'
    D = metal_defs(p, hi, mid, lo)
    body_d = "M-7.5 1.9A1.9 1.9 0 0 1 -5.6 0H5.6A1.9 1.9 0 0 1 7.5 1.9V9.1A1.9 1.9 0 0 1 5.6 11H-5.6A1.9 1.9 0 0 1 -7.5 9.1Z"
    body = f'<path d="{body_d}"/>'
    key_d = "M0 3.1a1.55 1.55 0 0 1 .95 2.8L1.4 8.3H-1.4L-.95 5.9A1.55 1.55 0 0 1 0 3.1Z"
    key = f'<path d="{key_d}"/>'
    D.append(f'<mask id="{p}sm"><path d="{shackle}" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"/></mask>')
    dd, bv = bevel(p, "b", body, spec, shade); D += dd
    D.append(f'<mask id="{p}km"><g fill="#fff">{key}</g><g fill="#000" transform="translate(-.4 -.4)">{key}</g></mask>')
    border = "M-5.6 1.6H5.6A.6 .6 0 0 1 6.2 2.2V8.8A.6 .6 0 0 1 5.6 9.4H-5.6A.6 .6 0 0 1 -6.2 8.8V2.2A.6 .6 0 0 1 -5.6 1.6Z"
    S = [f'<g {t} filter="url(#{p}ug)">'
         f'<path d="{shackle}" fill="none" stroke="{mid}" stroke-width="2.6" stroke-linecap="round"/>'
         f'<g mask="url(#{p}sm)"><path d="{shackle}" fill="none" stroke="{shade}" stroke-opacity=".75" stroke-width="1.1" transform="translate(.75 .75)" stroke-linecap="round"/>'
         f'<path d="{shackle}" fill="none" stroke="{spec}" stroke-opacity=".9" stroke-width=".8" transform="translate(-.6 -.6)" stroke-linecap="round"/></g>'
         f'<path d="{body_d}" fill="url(#{p}mf)"/>' + "".join(bv) +
         f'<path d="{border}" fill="none" stroke="{spec}" stroke-opacity=".4" stroke-width=".25" transform="translate(.2 .25)"/>'
         f'<path d="{border}" fill="none" stroke="{eng}" stroke-opacity=".55" stroke-width=".4"/>'
         f'<path d="{key_d}" fill="#0E0B08" fill-opacity=".88"/>'
         f'<rect x="-20" y="-20" width="40" height="40" fill="{spec}" fill-opacity=".8" mask="url(#{p}km)"/>'
         '</g>']
    return D, S


def book_glyph(p, cx, cy):
    D = metal_defs(p, "#F6DC9A", B_LIGHT, "#916422")
    left = "M0 -5C-3 -7.4 -6.6 -7.9 -10 -6.7V7C-6.6 6 -3.2 6.5 0 8.8Z"
    right = "M0 -5C3 -7.4 6.6 -7.9 10 -6.7V7C6.6 6 3.2 6.5 0 8.8Z"
    board = "M-10.8 -6.4V8.1C-7.1 7 -3.4 7.6 0 10C3.4 7.6 7.1 7 10.8 8.1V-6.4L10 -6.7V7C6.6 6 3.2 6.5 0 8.8C-3.2 6.5 -6.6 6 -10 7V-6.7Z"
    pages = f'<path d="{left}"/><path d="{right}"/>'
    dd, bv = bevel(p, "p", pages, B_SPEC, B_DEEP, .9); D += dd
    ls = [(-7.6, -3.6), (-4.2, -1.6), (-6.6, 1.8), (-2.8, 3.4)]
    rs = [(2.6, -3.4), (5.0, -0.6), (7.8, -2.8), (7.2, 2.6)]
    lines = ("M" + "L".join(f"{f(x)} {f(y)}" for x, y in ls) + "M" + "L".join(f"{f(x)} {f(y)}" for x, y in rs))
    stars = "".join(f'<circle cx="{f(x)}" cy="{f(y)}" r=".62"/>' for x, y in ls + rs)
    t = f'transform="translate({f(cx)} {f(cy - 1.3)})"'
    S = [f'<g {t} filter="url(#{p}ug)">'
         f'<path d="{board}" fill="{B_MID}"/>'
         f'<path d="{left}" fill="url(#{p}mf)"/><path d="{right}" fill="url(#{p}mf)"/>'
         f'<path d="{right}" fill="{B_DARK}" fill-opacity=".12"/>' + "".join(bv) +
         f'<path d="{lines}" fill="none" stroke="{B_SPEC}" stroke-opacity=".5" stroke-width=".28" transform="translate(.22 .28)" stroke-linecap="round" stroke-linejoin="round"/>'
         f'<path d="{lines}" fill="none" stroke="#6A4A18" stroke-opacity=".8" stroke-width=".42" stroke-linecap="round" stroke-linejoin="round"/>'
         f'<g fill="#5A3E12">{stars}</g>'
         f'<path d="M.25 -4.7V8.9" stroke="{B_SPEC}" stroke-opacity=".5" stroke-width=".3"/><path d="M0 -4.9V8.7" stroke="{B_DEEP}" stroke-width=".6"/>'
         f'<path d="M-.8 8.5V12.6L0 11.7L.8 12.6V8.5Z" fill="{EMBER_HI}"/><path d="M.8 8.5V12.6L0 11.7Z" fill="{EMBER}"/>'
         '</g>']
    return D, S


def kit_badge(p, kind, job=None, ring_tier="resting"):
    """Ring + seat + content at the shared slot, from the Astrolabe kit."""
    D, S = badge_ring(p + "r", ring_tier)
    hi, lo = ROLE[JOBS[job][1]] if kind == "job" else SEAT[kind]
    dd, ss = badge_seat(p + "s", hi, lo); D += dd; S += ss
    cx, cy = BADGE["cx"], BADGE["cy"]
    if kind == "job":
        name, role, icon_id = JOBS[job]
        ox, oy = job_optical_offset(job)
        sz = BADGE["icon"]
        S.append(f'<image href="{job_png(job)}" x="{f(cx - sz / 2 - ox * sz)}" y="{f(cy - sz / 2 - oy * sz)}" width="{f(sz)}" height="{f(sz)}" '
                 f'preserveAspectRatio="xMidYMid meet" clip-path="url(#{p}sec)"><title>{name} (game icon {icon_id:06d})</title></image>')
    elif kind in ("open", "closed"):
        dd, ss = lock_glyph(p + "g", cx, cy, kind == "open"); D += dd; S += ss
    else:
        dd, ss = book_glyph(p + "g", cx, cy); D += dd; S += ss
    return "".join(D), "".join(S)


def astrolabe_frame(state, hero):
    d, s = frame(f"v7k{STATES.index(state)}{'h' if hero else 'r'}-", STATE_TIER[state], "full", hero)
    return "".join(d), "".join(s)


# =====================================================================================================================
# COMPOSITES
# =====================================================================================================================

def composite(title, face, frm, badge=None):
    fd, under, over = face
    rd, rs = frm
    bd, bs = badge if badge else ("", "")
    return svg(title, [fd, rd, bd], [under, rs, over, bs])


TITLES = {"ready": "Ready", "ready-on-another-job": "Ready on another job", "in-journal": "In journal", "blocked": "Blocked",
          "done-this-cycle": "Done this cycle", "completed": "Completed", "locked-out": "Locked out", "not-checked": "Not checked"}


def state_badge(state, job=DEFAULT_JOB):
    kind = STATE_BADGE.get(state)
    if not kind:
        return None
    return kit_badge(f"v7kb{STATES.index(state)}-", kind, job, "act-now" if state == "ready" else "resting")


# ---------------- Medallion (Brass kit) for the mix check: read-only use of gen5 ----------------

def medallion():
    sys.path.insert(0, str(MED_SRC))
    import gen5
    return gen5


def medallion_face(g5, state):
    """A Medallion face: gen5's medal with its bezel replaced by a split marker and its badge off, cut into under and
    over at the marker (the ribbon and the check are drawn after the bezel)."""
    real = g5.bezel
    g5.bezel = lambda p, hero=True: ([], ["<!--FRAME-->"])
    g5.ROW_TIER = True
    fn = {"ready": g5.ready, "ready-on-another-job": g5.ready_other_job, "in-journal": g5.in_journal, "blocked": g5.blocked,
          "done-this-cycle": g5.done, "completed": g5.completed, "locked-out": g5.locked_out, "not-checked": g5.not_checked}[state]
    text = fn()
    g5.bezel = real
    g5.ROW_TIER = False
    defs = text[text.index("<defs>") + 6:text.index("</defs>")]
    body = text[text.index("</defs>") + 7:text.rindex("</svg>")]
    under, over = body.split("<!--FRAME-->")
    return defs, under, over


def brass_frame(g5, state, hero):
    d, s = g5.bezel(f"bk{STATES.index(state)}{'h' if hero else 'r'}-", hero)
    return "".join(d), "".join(s)


def brass_badge(g5, state, job=DEFAULT_JOB):
    kind = STATE_BADGE.get(state)
    if not kind:
        return None
    g5.ROW_TIER = False
    d, s = g5.badge(f"bkb{STATES.index(state)}-", kind, job if kind == "job" else None)
    return "".join(d), "".join(s)


# =====================================================================================================================

def coverage_lower_half():
    """Mixing rule R4: the share of the well's lower half that Blocked's cloud covers."""
    ys, xs = np.mgrid[0:128:.25, 0:128:.25]
    well = ((xs - 64) ** 2 + (ys - 64) ** 2 <= R_FACE ** 2) & (ys >= 64)
    bumps, (x0, x1, yt, yb) = KUMO
    cl = np.zeros_like(xs, dtype=bool)
    for x, y, r in bumps:
        cl |= (xs - x) ** 2 + (ys - y) ** 2 <= r * r
    rr = (yb - yt) / 2
    cl |= (xs >= x0 + rr) & (xs <= x1 - rr) & (ys >= yt) & (ys <= yb)
    for ex in (x0 + rr, x1 - rr):
        cl |= (xs - ex) ** 2 + (ys - (yt + rr)) ** 2 <= rr * rr
    return float((cl & well).sum() / well.sum())


def main():
    g5 = medallion()
    for hero, hatch, fdir in ((True, "on", "hero"), (True, "off", "mid"), (False, "off", "row")):
        for st in STATES:
            fd, u, o = build_face(st, hero, hatch)
            write(f"{fdir}/{st}.svg", svg(f"{TITLES[st]} (face, {fdir})", [fd], [u]))
            write(f"{fdir}/{st}.over.svg", svg(f"{TITLES[st]} (face overhangs, {fdir})", [fd], [o]))
    # the kit
    for tier in TIERS:
        for finish in ("full", "quiet"):
            d, s = frame(f"v7kt-", tier, finish, True)
            write(f"kit/frame-{tier}-{finish}.svg", svg(f"Astrolabe frame: {tier}, {finish}", d, s))
        d, s = frame("v7kt-", tier, "full", False)
        write(f"kit/row/frame-{tier}-full.svg", svg(f"Astrolabe frame: {tier}, full, row tier", d, s))
    for tier, name in (("resting", "badge-ring"), ("act-now", "badge-ring-act-now")):
        d, s = badge_ring("v7kr-", tier)
        write(f"kit/{name}.svg", svg(f"Astrolabe {name}", d, s))
    for kind, (hi, lo) in SEAT.items():
        d, s = badge_seat("v7ks-", hi, lo)
        write(f"kit/badge-seat-{kind}.svg", svg(f"Astrolabe badge seat: {kind}", d, s))
    d, s = badge_seat("v7ks-", "#FFFFFF", "#FFFFFF")
    write("kit/badge-seat-role-mask.svg", svg("Badge seat mask (tinted by the role colour at runtime)", d, s))
    for kind in ("open", "closed", "journal"):
        d, s = (lock_glyph("v7kg-", 95, 95, kind == "open") if kind != "journal" else book_glyph("v7kg-", 95, 95))
        write(f"kit/badge-{kind}.svg", svg(f"Astrolabe badge glyph: {kind}", d, s))
        d, s = (lock_glyph(f"v7ok{kind[0]}-", 0, 0, kind == "open") if kind != "journal" else book_glyph("v7okj-", 0, 0))
        write(f"_row/badge-{kind}.svg", svg(f"{kind} badge glyph", d, s, vb="-17 -18 34 34", size=34))
    # composites: the theme as designed (Astrolabe kit)
    for st in STATES:
        write(f"{st}.svg", composite(TITLES[st], build_face(st, True), astrolabe_frame(st, True), state_badge(st)))
        write(f"_row/{st}.svg", composite(TITLES[st] + " (row)", build_face(st, False), astrolabe_frame(st, False)))
    for job in JOBS:
        st = "ready-on-another-job"
        write(f"ready-on-another-job-{job}.svg", composite(f"Ready on another job: {JOBS[job][0]}", build_face(st, True),
                                                          astrolabe_frame(st, True), state_badge(st, job)))
    # the mix check, both ways
    for st in STATES:
        write(f"_mix/orrery-in-brass/{st}.svg", composite(TITLES[st], build_face(st, True), brass_frame(g5, st, True), brass_badge(g5, st)))
        write(f"_mix/orrery-in-brass/_row/{st}.svg", composite(TITLES[st], build_face(st, False), brass_frame(g5, st, False)))
        mf = medallion_face(g5, st)
        write(f"_mix/medallion-in-astrolabe/{st}.svg", composite(TITLES[st], mf, astrolabe_frame(st, True), state_badge(st)))
        write(f"_mix/medallion-in-astrolabe/_row/{st}.svg", composite(TITLES[st], mf, astrolabe_frame(st, False)))
    print(f"R4: Blocked's cloud covers {coverage_lower_half():.0%} of the well's lower half")


if __name__ == "__main__":
    main()
