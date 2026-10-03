"""Sumi to Kinpaku (v7 theme): tsukimi crests in ink, shell-white and cut gold leaf.

Writes, into docs/design/v7/themes/sumi-to-kinpaku:
  faces/<state>-under.svg, faces/<state>-over.svg   unframed faces (the well, clipped to r 52.4) and their overhangs
  _mid/..., _row/...                                the same for the mid tier (48-64 px) and the row tier (under 32 px)
  kit/...                                           the Kirikane kit: frames at four urgency tiers (Full, Quiet; hero and
                                                    row), badge rings, seats and glyphs
  <state>.svg, ready-on-another-job-<job>.svg       composites (face under, kit frame, kit badge, face over)
  _row/badge-<kind>.svg                             the badge glyphs alone, for drawing beside a row-tier medal

Every medal is a small lacquered crest plaque, made the way a maki-e craftsman makes a suzuri-bako lid:
- the ground is ro-iro lacquer (sumi), glossy, so it carries a soft specular sheen of the key light and nothing else;
- the moons are gofun (ground shell-white), built up a hair in relief (moriage): matte, flat in tone, lit on their
  upper-left edges by the key light and casting a short shadow down and to the right;
- the gold is real leaf: cut into strips (kirikane) for the moon road and the frame's crest line, laid flat for the
  check and the "comes back" arc; flat leaf reflects one even tone, so it never takes a ball gradient;
- Blocked's mist is silver leaf that has tarnished to pewter (gin-kasumi), Dalamud is vermilion lacquer (shu-urushi)
  cracked down to the black ground, and the ribbon is indigo-dyed silk (ai-zome).
One light for everything: the UI key light from the upper left (azimuth 135 deg). Inside Ready's scene the moon is the
light and the road falls directly below its lit centroid.

Tiers: row (under 32 px) is flat shapes only, at most three masses and three tones, strokes drawn bold; mid (48-64 px)
adds the relief and its one-pixel cast shadow (the kirie layering); hero (96 px and up) adds the kirikane hairlines,
Kugane Castle on Ready's far shore, the susuki grass of the harvest moon, and the lacquer's hairline crazing.

Run: python _src/gen.py   (then python _src/mix.py and the sheet: moon-v6/round5/render_sheet.py)
"""
import base64, math, pathlib
import numpy as np

OUT = pathlib.Path(__file__).resolve().parent.parent
JOBDIR = OUT.parents[3] / "design" / "moon-v6" / "round5" / "medallion-r5" / "_src" / "jobs"
ROW = False
MID = False
PLAIN = False  # Decoration Plain: the row geometry in flat ink and gold, a state rim instead of the kit frame

# ---------------- palette tokens ----------------
KEY = "#050406"                                         # keyline, cast shadow
SOCKET = "#080406"                                      # the black ground under broken lacquer
# the lacquer ground (sumi): the well is a lifted sumi seat; moon states get a breath of ai (indigo) in the black
WELL_T, WELL_B = "#0E0C10", "#060508"                   # ro-iro lacquer: a warm black, never navy
LACQ_HI, LACQ, LACQ_LO = "#3A363E", "#141216", "#060508"  # ro-iro lacquer of the frame: lit slope, body, shaded slope
SHEEN = "#FFFFFF"                                       # the key light's reflection in glossy lacquer
# gofun (shell-white): matte, warm
GOFUN_HI, GOFUN, GOFUN_MID, GOFUN_LO = "#FFFBF1", "#F2ECDD", "#D6CFBE", "#A29C8D"
MOON = "#F3EAD3"                                        # torinoko: the lit moon, one flat tone
MOON_SHADE = "#B7AE97"                                  # the relief's shaded lip on gofun
EARTH = "#2E2C36"                                       # the unlit disc (earthshine), a lifted sumi plane
NOTC_MOON = "#35333E"                                   # Not checked's faint moon behind the "?"
EARTH_DONE = "#3A3843"                                  # Done's dark half, a step lighter (no contrast-toggle read)
CMOON, CMOON_LIP = "#A4AABA", "#CDD2DE"                 # Completed: gofun gone cool and quiet, two steps under the moon
# gold leaf (kinpaku); the act-now frame and badge ring are the shared medal gilt (every kit)
KIN_HI, KIN, KIN_LO, KIN_DEEP = "#F4DA92", "#DEB862", "#A98843", "#6E5426"
GILT_SPEC, GILT_HI, GILT_LIGHT, GILT, GILT_MID, GILT_DEEP, GILT_DARK = (
    "#FFF4D6", "#E6CF98", "#D9BE82", "#9A7E4A", "#7C6236", "#5C4724", "#33260F")
# silver leaf tarnished to pewter (Blocked's mist, the closed lock)
GIN_HI, GIN_DEEP = "#A3AABC", "#232736"
# vermilion lacquer (Dalamud)
SHU_HI, SHU, SHU_LO, SHU_LIP = "#DE6A50", "#C4433F", "#7E2418", "#F4A892"
# indigo silk (the ribbon)
AI_HI, AI, AI_DEEP = "#9DB5E8", "#5C7DC6", "#2E447F"
# Ready's night: an ai bokashi sky over a darker sea; In journal's is a step deeper
SKY_T, SKY_M, SKY_H, SEA_H, SEA_B = "#1B2652", "#4660A2", "#B2C4EC", "#4660A6", "#1B2852"   # Hiroshige's ai bokashi
# Plain: one flat tone each; Ready's dusk sky is flattened to its pale twilight (the lit part of the bokashi), so Ready
# still leads once every state wears Medallion's Plain rim
PSKY, SEA_M, NSKY_M, NSEA_M = "#7D94CD", "#3A4F8E", "#33467B", "#1A2448"
NSKY_T, NSKY_H, NSEA_H, NSEA_B = "#1D2848", "#4A5F98", "#2A3A6C", "#111938"
ROLE = {"tank": ("#5878C2", "#2C417E"), "healer": ("#5C9A68", "#2B5A38"), "dps": ("#B25A64", "#5E2632")}
JOBS = {"paladin": ("Paladin", "tank", 62019), "bard": ("Bard", "dps", 62023), "white-mage": ("White Mage", "healer", 62024)}
DEFAULT_JOB = "paladin"

C = 64.0
R_KEY, R_OUT, R_MID, R_IN = 63.2, 61.6, 56.8, 50.9      # silhouette, frame outer edge, the crest line, frame inner edge
R_WELL = 52.4             # every face's well (Medallion's): the kit frame's inner edge covers it by 1.5 units
# the Kirikane kit's urgency tiers: act now is the shared medal gilt (every kit); the others are ro-iro lacquer with a
# crest line of cut gold leaf, each a step quieter: (line colour or None, line opacity, gofun edge opacity, sheen)
TIERS = {
    "act-now":  None,
    "resting":  (KIN, 1.0, .34, .5),
    "finished": (KIN_LO, .8, .26, .34),
    "ghost":    (None, 0, .32, .2),
}
STATE_TIER = {"ready": "act-now", "ready-on-another-job": "resting", "in-journal": "resting", "blocked": "resting",
              "done-this-cycle": "resting", "completed": "finished", "locked-out": "resting", "not-checked": "ghost"}
LIGHT_V = (-0.6, -0.8)                                  # unit vector toward the key light


def tier():
    return "row" if ROW else ("mid" if MID else "hero")


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


def bez(p0, p1, p2, p3, n=24):
    return [((1 - t) ** 3 * p0[0] + 3 * (1 - t) ** 2 * t * p1[0] + 3 * (1 - t) * t * t * p2[0] + t ** 3 * p3[0],
             (1 - t) ** 3 * p0[1] + 3 * (1 - t) ** 2 * t * p1[1] + 3 * (1 - t) * t * t * p2[1] + t ** 3 * p3[1])
            for t in (i / n for i in range(n + 1))]


def svg(title, defs, body, vb=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {vb} {vb}" width="{vb}" height="{vb}"><title>{title}</title>'
            f'<defs>{"".join(defs)}</defs>{"".join(body)}</svg>\n')


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


def rad_grad(p, name, cx, cy, r, stops):
    return (f'<radialGradient id="{p}{name}" cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" gradientUnits="userSpaceOnUse">'
            + "".join(f'<stop offset="{f(o)}" stop-color="{c}"' + (f' stop-opacity="{f(a)}"' if a is not None else "") + "/>"
                      for o, c, a in [(s + (None,))[:3] for s in stops]) + "</radialGradient>")


# ---------------- craft: relief, cast shadow, the well ----------------

def relief(p, n, markup, light=GOFUN_HI, dark=MOON_SHADE, lo=.75, do=.35, w=1.0):
    """Moriage (raised gofun) or a cut-paper plane, lit from the upper left: a lit lip on the edges that face the key
    light and a shaded lip on the edges that face away. Mid and hero only (the row tier is flat)."""
    if ROW:
        return [], []
    D = [f'<mask id="{p}{n}L"><g fill="#fff">{markup}</g><g fill="#000" transform="translate({f(w * .7)} {f(w * .9)})">{markup}</g></mask>']
    S = [f'<rect width="128" height="128" fill="{light}" fill-opacity="{f(lo)}" mask="url(#{p}{n}L)"/>']
    if dark:
        D.append(f'<mask id="{p}{n}D"><g fill="#fff">{markup}</g><g fill="#000" transform="translate({f(-w * .7)} {f(-w * .9)})">{markup}</g></mask>')
        S.append(f'<rect width="128" height="128" fill="{dark}" fill-opacity="{f(do)}" mask="url(#{p}{n}D)"/>')
    return D, S


def cast(p, n, markup, op=.5):
    """The short shadow a raised plane casts down and to the right: one device pixel at the mid tier (the kirie
    layering), a little finer at hero. None at row size."""
    if ROW:
        return [], []
    dx, dy, sd = (1.6, 2.1, .5) if MID else (1.1, 1.5, .45)
    return ([blur_filter(p, f"{n}cb", sd)],
            [f'<g filter="url(#{p}{n}cb)"><g transform="translate({f(dx)} {f(dy)})" fill="{KEY}" fill-opacity="{f(op)}">{markup}</g></g>'])


def well(p, top=WELL_T, bot=WELL_B):
    """The lacquer well, clipped to r 52.4. Returns (defs, base, over): 'over' is drawn above the scene: the lacquer's
    glossy sheen toward the key light, and the shadow the raised frame casts on the well's upper-left inner edge."""
    D = [lin_grad(p, "wl", 0, 12, 0, 116, [(0, top), (1, bot)]),
         f'<clipPath id="{p}wc"><circle cx="64" cy="64" r="{f(R_WELL)}"/></clipPath>',
         # glossy lacquer mirrors the key light as one broad soft band across the upper left of the well, about a third
         # of its width: broad tone, so it holds at row size without ever becoming texture
         blur_filter(p, "gl", 5.0)]
    base = [f'<rect width="128" height="128" fill="{top if PLAIN else f"url(#{p}wl)"}"/>']
    if PLAIN:
        return D, base, []
    over = [f'<g filter="url(#{p}gl)"><rect x="-40" y="23" width="208" height="34" fill="{SHEEN}" fill-opacity=".07" '
            f'transform="rotate(-45 40 40)"/></g>']
    if not ROW:
        D += [blur_filter(p, "ib", 1.3),
              f'<mask id="{p}im"><rect width="128" height="128" fill="#fff"/><circle cx="{f(64 + 2.6)}" cy="{f(64 + 3.6)}" r="{f(R_WELL)}" fill="#000"/></mask>']
        over.append(f'<circle cx="64" cy="64" r="{f(R_WELL + 1)}" fill="{KEY}" fill-opacity=".5" mask="url(#{p}im)" filter="url(#{p}ib)"/>')
    return D, base, over


def clip_well(p, parts):
    return f'<g clip-path="url(#{p}wc)">{"".join(parts)}</g>'


# ---------------- the Kirikane kit: frames ----------------

def kit_frame(p, tier_name="resting", quiet=False):
    """The Kirikane kit's medal frame. Resting, finished and ghost are a ring of ro-iro lacquer: a convex band whose
    upper-left slope carries the key light's soft reflection, with a crest line of cut gold leaf (kirikane) laid round
    its crown and a gofun hairline at its outer edge. Act now is the same ring gilded (the shared medal gilt ramp), the
    crest line then a fine dark groove: the crest's 'hinata' (sunlit) form against the others' 'kage' (shadow) form.
    Quiet: the keyline and one hairline in the tier's metal. The inner edge (r 50.9) covers every face's well (52.4)."""
    if quiet:
        col, op = {"act-now": (GILT_LIGHT, 1), "resting": (KIN, 1), "finished": (KIN_LO, .9), "ghost": (GOFUN, .35)}[tier_name]
        return [], [f'<path d="{ring(R_IN, 55.6)}" fill="{KEY}" fill-rule="evenodd"/>',
                    f'<path d="{ring(53.4, 54.8)}" fill="{col}" fill-opacity="{f(op)}" fill-rule="evenodd"/>']
    if tier_name == "act-now":
        D = [lin_grad(p, "fo", 14, 10, 114, 118, [(0, GILT_HI), (.42, GILT), (.78, GILT_MID), (1, GILT_DEEP)]),
             lin_grad(p, "fi", 14, 10, 114, 118, [(0, GILT_DEEP), (.55, GILT_MID), (1, GILT_LIGHT)])]
        S = [f'<path d="{ring(R_IN - .9, R_KEY)}" fill="{KEY}" fill-opacity=".92" fill-rule="evenodd"/>',
             f'<path d="{ring(R_MID, R_OUT)}" fill="url(#{p}fo)" fill-rule="evenodd"/>',
             f'<path d="{ring(R_IN, R_MID)}" fill="url(#{p}fi)" fill-rule="evenodd"/>',
             f'<path d="{ring(R_MID - .3, R_MID + .3)}" fill="{GILT_DARK}" fill-opacity=".55" fill-rule="evenodd"/>',
             f'<path d="{taper_arc(R_MID + 2.3, -168, -102, 2.4)}" fill="{GILT_SPEC}" fill-opacity=".95"/>',
             f'<path d="{taper_arc(R_IN + 1.9, 18, 52, 1.6)}" fill="{GILT_SPEC}" fill-opacity=".45"/>']
        return D, S
    line, line_op, gofun, sheen = TIERS[tier_name]
    D = [lin_grad(p, "fo", 14, 10, 114, 118, [(0, LACQ_HI), (.45, LACQ), (1, LACQ_LO)]),
         lin_grad(p, "fi", 14, 10, 114, 118, [(0, LACQ_LO), (.6, LACQ), (1, LACQ_HI)])]
    S = [f'<path d="{ring(R_IN - .9, R_KEY)}" fill="{KEY}" fill-opacity=".95" fill-rule="evenodd"/>',
         f'<path d="{ring(R_MID, R_OUT)}" fill="url(#{p}fo)" fill-rule="evenodd"/>',
         f'<path d="{ring(R_IN, R_MID)}" fill="url(#{p}fi)" fill-rule="evenodd"/>']
    if sheen:
        # glossy lacquer: the key light's reflection, a soft band on the upper-left crown, and a faint second one where
        # the inner slope faces the light at the lower right
        D.append(blur_filter(p, "fb", .55 if not ROW else .3))
        S.append(f'<g filter="url(#{p}fb)"><path d="{taper_arc(R_MID + 2.2, -166, -104, 2.2)}" fill="{SHEEN}" fill-opacity="{f(sheen)}"/>'
                 f'<path d="{taper_arc(R_IN + 1.8, 22, 58, 1.4)}" fill="{SHEEN}" fill-opacity="{f(sheen * .35)}"/></g>')
    if line:
        w = 2.6 if ROW else 1.1
        S.append(f'<path d="{ring(R_MID - w / 2, R_MID + w / 2)}" fill="{line}" fill-opacity="{f(line_op)}" fill-rule="evenodd"/>')
        if not ROW and not MID:
            # hero: the leaf's cut edge catches the light along its upper-left run
            S.append(f'<path d="{taper_arc(R_MID - .3, -170, -100, .5)}" fill="{KIN_HI}" fill-opacity="{f(.8 * line_op)}"/>')
    gw = 1.8 if ROW else .75
    S.append(f'<path d="{ring(R_OUT - gw, R_OUT)}" fill="{GOFUN}" fill-opacity="{f(gofun)}" fill-rule="evenodd"/>')
    return D, S


# ---------------- the Kirikane kit: badge ----------------
BADGE = dict(cx=95.0, cy=95.0, r_key=24.0, r_out=23.1, r_mid=21.5, r_in=19.9, icon=35.5)
SEAT = {"open": ("#33508F", "#121C3C"),      # Ready: ai lacquer under the gold-leaf lock
        "closed": ("#302D35", "#0C0A0E"),    # Blocked: sumi under the pewter lock
        "journal": ("#3E5A9A", "#1A2A57")}   # In journal: the ribbon's indigo


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


def badge_ring(p, tier_name="resting"):
    """The shared badge slot (Medallion r5 geometry): centre (95, 95), keyline r 24 with the standard down-right
    shadow, ring r 19.9-23.1. Act now: the shared gilt. Otherwise ro-iro lacquer with a kirikane crest line."""
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    x0, y0, x1, y1 = cx - 24, cy - 24, cx + 24, cy + 24
    D = [shadow_filter(p, "bs", 1.4, 1.9, 1.1, .65)]
    S = [f'<g filter="url(#{p}bs)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_key"])}" fill="{KEY}"/></g>']
    if tier_name == "act-now":
        D += [lin_grad(p, "go", x0, y0, x1, y1, [(0, GILT_HI), (.45, GILT), (1, GILT_DEEP)]),
              lin_grad(p, "gi", x0, y0, x1, y1, [(0, GILT_DEEP), (.55, GILT_MID), (1, GILT_LIGHT)])]
        S += [f'<path d="{ring(b["r_mid"], b["r_out"], cx, cy)}" fill="url(#{p}go)" fill-rule="evenodd"/>',
              f'<path d="{ring(b["r_in"], b["r_mid"], cx, cy)}" fill="url(#{p}gi)" fill-rule="evenodd"/>',
              f'<path d="{ring(b["r_mid"] - .2, b["r_mid"] + .2, cx, cy)}" fill="{GILT_DARK}" fill-opacity=".45" fill-rule="evenodd"/>',
              f'<path d="{taper_arc(b["r_mid"] + .75, -170, -100, 1.2, cx, cy)}" fill="{GILT_SPEC}" fill-opacity=".9"/>']
    else:
        D += [lin_grad(p, "go", x0, y0, x1, y1, [(0, LACQ_HI), (.45, LACQ), (1, LACQ_LO)]),
              lin_grad(p, "gi", x0, y0, x1, y1, [(0, LACQ_LO), (.6, LACQ), (1, LACQ_HI)]),
              blur_filter(p, "gb", .4)]
        S += [f'<path d="{ring(b["r_mid"], b["r_out"], cx, cy)}" fill="url(#{p}go)" fill-rule="evenodd"/>',
              f'<path d="{ring(b["r_in"], b["r_mid"], cx, cy)}" fill="url(#{p}gi)" fill-rule="evenodd"/>',
              f'<g filter="url(#{p}gb)"><path d="{taper_arc(b["r_mid"] + .8, -165, -105, 1.0, cx, cy)}" fill="{SHEEN}" fill-opacity=".45"/></g>',
              f'<path d="{ring(b["r_mid"] - .35, b["r_mid"] + .35, cx, cy)}" fill="{KIN}" fill-rule="evenodd"/>',
              f'<path d="{ring(b["r_out"] - .5, b["r_out"], cx, cy)}" fill="{GOFUN}" fill-opacity=".3" fill-rule="evenodd"/>']
    return D, S


def badge_seat(p, kind):
    """The seat: a disc of coloured lacquer, brightest toward the light, shaded on its upper-left edge by the ring
    in front of it. kind: open / closed / journal, or a role (tank / healer / dps; colours fixed in every kit)."""
    b = BADGE
    cx, cy = b["cx"], b["cy"]
    hi, lo = ROLE[kind] if kind in ROLE else SEAT[kind]
    D = [rad_grad(p, "en", cx - 5, cy - 6, b["r_in"] * 1.5, [(0, hi), (1, lo)]),
         f'<clipPath id="{p}ec"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}"/></clipPath>',
         f'<mask id="{p}em"><rect width="128" height="128" fill="#fff"/><circle cx="{f(cx + 1.4)}" cy="{f(cy + 1.9)}" r="{f(b["r_in"])}" fill="#000"/></mask>',
         blur_filter(p, "eb", .8)]
    S = [f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"])}" fill="url(#{p}en)"/>',
         f'<g clip-path="url(#{p}ec)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(b["r_in"] + 1)}" fill="{KEY}" fill-opacity=".5" mask="url(#{p}em)" filter="url(#{p}eb)"/></g>']
    return D, S


LOCK_OY = -0.2   # Medallion's: one body position for both locks


def leaf_bevel(p, n, shape, light, dark, w=.9):
    """A lit edge on the upper-left and a shaded edge on the lower-right of a flat applied plate (leaf over a thin
    ground, so it has a hair of thickness)."""
    D = [f'<mask id="{p}{n}bl"><g fill="#fff">{shape}</g><g fill="#000" transform="translate({f(w * .7)} {f(w * .9)})">{shape}</g></mask>',
         f'<mask id="{p}{n}bd"><g fill="#fff">{shape}</g><g fill="#000" transform="translate({f(-w * .7)} {f(-w * .9)})">{shape}</g></mask>']
    S = [f'<rect x="-20" y="-20" width="40" height="40" fill="{light}" fill-opacity=".85" mask="url(#{p}{n}bl)"/>',
         f'<rect x="-20" y="-20" width="40" height="40" fill="{dark}" fill-opacity=".7" mask="url(#{p}{n}bd)"/>']
    return D, S


def lock_glyph(p, cx, cy, open_):
    """Medallion's padlock outline (so a mix reads one lock), made in this kit's materials: the open lock (Ready) in
    gold leaf laid over a thin ground, the closed lock (Blocked) in tarnished silver leaf. Flat leaf: one tone, a lit
    upper-left edge and a shaded lower-right edge; the keyhole is cut through to the black ground."""
    if open_:
        face, spec, shade = KIN, "#FFF1C8", KIN_DEEP
        shackle = "M-4.6 0V-11A4.6 4.6 0 0 1 4.6 -11V-5.5"
    else:
        face, spec, shade = "#9AA1B4", "#E8ECF3", "#3A4053"
        shackle = "M-4.6 0V-6A4.6 4.6 0 0 1 4.6 -6V0"
    body_d = "M-7.5 1.9A1.9 1.9 0 0 1 -5.6 0H5.6A1.9 1.9 0 0 1 7.5 1.9V9.1A1.9 1.9 0 0 1 5.6 11H-5.6A1.9 1.9 0 0 1 -7.5 9.1Z"
    key_d = "M0 3.1a1.55 1.55 0 0 1 .95 2.8L1.4 8.3H-1.4L-.95 5.9A1.55 1.55 0 0 1 0 3.1Z"
    t = f'transform="translate({f(cx)} {f(cy + LOCK_OY)})"'
    D = [f'<filter id="{p}ug" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">'
         f'<feDropShadow dx=".6" dy=".9" stdDeviation=".7" flood-color="{KEY}" flood-opacity=".55"/></filter>',
         f'<mask id="{p}sm"><path d="{shackle}" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"/></mask>']
    dd, bv = leaf_bevel(p, "b", f'<path d="{body_d}"/>', spec, shade); D += dd
    S = [f'<g {t} filter="url(#{p}ug)">'
         f'<path d="{shackle}" fill="none" stroke="{face}" stroke-width="2.6" stroke-linecap="round"/>'
         f'<g mask="url(#{p}sm)"><path d="{shackle}" fill="none" stroke="{shade}" stroke-opacity=".7" stroke-width="1" transform="translate(.75 .75)" stroke-linecap="round"/>'
         f'<path d="{shackle}" fill="none" stroke="{spec}" stroke-opacity=".85" stroke-width=".7" transform="translate(-.6 -.6)" stroke-linecap="round"/></g>'
         f'<path d="{body_d}" fill="{face}"/>' + "".join(bv) +
         f'<path d="{key_d}" fill="{KEY}" fill-opacity=".9"/>'
         '</g>']
    return D, S


def book_glyph(p, cx, cy):
    """Medallion's open journal, made as a Japanese book: two pages of gofun-white paper over an indigo cloth board,
    lines of sumi script, and a small indigo ribbon at the foot. No gold: the journal is a resting state."""
    left = "M0 -5C-3 -7.4 -6.6 -7.9 -10 -6.7V7C-6.6 6 -3.2 6.5 0 8.8Z"
    right = "M0 -5C3 -7.4 6.6 -7.9 10 -6.7V7C6.6 6 3.2 6.5 0 8.8Z"
    board = "M-10.8 -6.4V8.1C-7.1 7 -3.4 7.6 0 10C3.4 7.6 7.1 7 10.8 8.1V-6.4L10 -6.7V7C6.6 6 3.2 6.5 0 8.8C-3.2 6.5 -6.6 6 -10 7V-6.7Z"
    pages = f'<path d="{left}"/><path d="{right}"/>'
    D = [f'<filter id="{p}ug" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">'
         f'<feDropShadow dx=".6" dy=".9" stdDeviation=".7" flood-color="{KEY}" flood-opacity=".55"/></filter>']
    dd, bv = leaf_bevel(p, "p", pages, GOFUN_HI, GOFUN_LO, .8); D += dd
    lines = ("M-8.2 -3.9C-6 -4.6 -3.8 -4.3 -1.7 -3M-8.2 -1C-6 -1.7 -3.8 -1.4 -1.7 -.1M-8.2 1.9C-6 1.2 -3.8 1.5 -1.7 2.8"
             "M8.2 -3.9C6 -4.6 3.8 -4.3 1.7 -3M8.2 -1C6 -1.7 3.8 -1.4 1.7 -.1M8.2 1.9C6 1.2 3.8 1.5 1.7 2.8")
    t = f'transform="translate({f(cx)} {f(cy - 1.3)})"'
    S = [f'<g {t} filter="url(#{p}ug)">'
         f'<path d="{board}" fill="{AI_DEEP}"/>'
         f'<path d="{left}" fill="{GOFUN}"/><path d="{right}" fill="{GOFUN_MID}"/>' + "".join(bv) +
         f'<path d="{lines}" fill="none" stroke="#2A2A33" stroke-opacity=".7" stroke-width=".55" stroke-linecap="round"/>'
         f'<path d="M0 -4.9V8.7" stroke="{GOFUN_LO}" stroke-width=".6"/>'
         f'<path d="M-.8 8.5V12.6L0 11.7L.8 12.6V8.5Z" fill="{AI_HI}"/><path d="M.8 8.5V12.6L0 11.7Z" fill="{AI}"/>'
         '</g>']
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


def badge(p, kind, job=None, tier_name="resting"):
    """The Kirikane kit's whole badge: ring, seat and content. The row tier draws none (the glyph goes beside the row)."""
    if ROW:
        return [], []
    D, S = badge_ring(p + "r", tier_name)
    dd, ss = badge_seat(p + "s", JOBS[job][1] if kind == "job" else kind); D += dd; S += ss
    dd, ss = badge_content(p + "c", kind, job); D += dd; S += ss
    return D, S


# ---------------- the moon (shared by Ready, Ready on another job and In journal) ----------------
MOON_G = (49.0, 42.0, 29.0, -0.18, 28.0)    # cx, cy, r, k, rot: Medallion's crescent, so Ready and RoJ match across sets
H = 80.0
LIT_C = centroid(*MOON_G)


def crest_moon(p, geom=MOON_G, fill=MOON, earth=EARTH, earth_op=1.0, ring_op=.2):
    """The moon as a crest: the earthlit disc is a lifted sumi plane edged with a gofun hairline (the 'kage' outline of
    a moon crest, so the crescent always reads as a moon and never as a sail), and the lit crescent is one flat piece
    of gofun laid on it in relief."""
    cx, cy, r, k, rot = geom
    d, tr = phase(cx, cy, r, k, "right", rot)
    t = f' transform="{tr}"' if tr else ""
    shape = f'<path d="{d}"{t}/>'
    D, S = [], []
    if earth:
        S.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="{earth}" fill-opacity="{f(earth_op)}"/>')
    if ring_op:
        S.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - (.9 if ROW else .4))}" fill="none" stroke="{GOFUN}" '
                 f'stroke-opacity="{f(ring_op)}" stroke-width="{f(1.8 if ROW else .8)}"/>')
    if not ROW:
        D += [blur_filter(p, "mcb", .5),
              f'<mask id="{p}mcm"><rect x="-20" y="-20" width="168" height="168" fill="#fff"/>'
              f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="#000"/></mask>']
        S.append(f'<g mask="url(#{p}mcm)"><g filter="url(#{p}mcb)"><g transform="translate(.7 .95)" fill="{KEY}" '
                 f'fill-opacity=".35">{shape}</g></g></g>')
    S.append(f'<path d="{d}"{t} fill="{fill}"/>')
    dd, ss = relief(p, "m", shape, lo=.7, do=.3); D += dd; S += ss
    return D, S


# ---------------- Ready, Ready on another job, In journal ----------------
# Kugane Castle's tenshu on the far shore (hero only): a stone base, three storeys under flared roofs, and a finial;
# x from 15 to 31, left of the road, clear of the moon's lower horn and of the badge.
CASTLE = [
    "M14 80L33 80L31.6 76.6L15.6 76.6Z",                                            # ishigaki (stone base)
    "M18.4 76.8V73.2H29.2V76.8Z",                                                    # first storey
    "M14.8 73.8Q19 73.3 20.6 71.3H27Q28.6 73.3 32.8 73.8Z",                         # first roof, flared eaves
    "M20.2 71.5V68.6H27.4V71.5Z",                                                    # second storey
    "M17.6 69.1Q20.6 68.7 21.8 67.2H25.8Q27 68.7 30 69.1Z",                          # second roof
    "M21.4 67.4V65.3H26.2V67.4Z",                                                    # third storey
    "M19.6 65.7Q22.4 65.3 23.8 62.9Q25.2 65.3 28 65.7Z",                             # top roof (irimoya gable)
    "M23.55 63.3V61.6H24.05V63.3Z",                                                  # finial
    "M8 80V78.6H14.4V80ZM33 80V79H44V80Z",                                           # sea walls either side
]
# the moonlit edges of the eaves, in kirikane (gold leaf cut to a hair), as a maki-e artist outlines a silhouette
EAVES = ["M14.8 73.8Q19 73.3 20.6 71.3H27Q28.6 73.3 32.8 73.8", "M17.6 69.1Q20.6 68.7 21.8 67.2H25.8Q27 68.7 30 69.1",
         "M19.6 65.7Q22.4 65.3 23.8 62.9Q25.2 65.3 28 65.7"]

# road rows (y, height, [(dx0, dx1)]) about the lit centroid's x: strips of cut gold leaf, each cut on a slant, longer and
# thicker toward the viewer (flat leaf reflects one tone, so the road brightens by coverage, not by value)
ROAD_HERO = [
    (82.6, .9, [(-2.5, 3.5)]),
    (85.8, 1.2, [(-7.0, -1.0), (2.0, 6.5)]),
    (90.2, 1.5, [(-4.5, 8.5)]),
    (95.8, 1.9, [(-12.0, -2.5), (1.0, 6.0), (8.5, 12.5)]),
    (102.6, 2.3, [(-9.0, 13.0)]),
    (110.6, 2.8, [(-16.5, -6.5), (-3.0, 10.0), (13.0, 17.5)]),
]
ROAD_MID = [
    (84.5, 2.0, [(-4.5, 5.0)]),
    (91.5, 2.6, [(-9.0, -.5), (3.0, 9.5)]),
    (99.5, 3.2, [(-7.5, 12.0)]),
    (109.0, 4.0, [(-15.5, -3.0), (0.5, 14.5)]),
]
ROAD_ROW = [                                      # the row tier: three rows, two or three broken glints
    (86.5, 4.6, [(-7.5, 7.5)]),
    (97.5, 6.0, [(-13.0, -.5), (2.5, 13.0)]),
    (109.5, 7.2, [(-16.0, 15.5)]),
]


def strip(x0, x1, y, h):
    """One strip of kirikane: a parallelogram, its ends cut on a slant (the bamboo knife's cut)."""
    s = h * .3
    return poly([(x0 + s, y - h / 2), (x1 + s, y - h / 2), (x1 - s, y + h / 2), (x0 - s, y + h / 2)])


def moon_scene(p, mode):
    """mode 'ready' (Ready), 'rest' (Ready on another job: night, no sea, no road), 'night' (In journal)."""
    D, S = [], []
    cxr, _ = LIT_C
    if mode == "rest":
        dd, base, over = well(p); D += dd
        G = list(base)
    else:
        sky_t, sky_h, sea_h, sea_b = (SKY_T, SKY_H, SEA_H, SEA_B) if mode == "ready" else (NSKY_T, NSKY_H, NSEA_H, NSEA_B)
        dd, base, over = well(p); D += dd
        # the sky is bokashi, the woodblock printer's graded wipe: at dusk (Ready) a band of deep ai at the top that
        # opens quickly into pale twilight toward the horizon, as in Hiroshige; at night (In journal) one slow grade
        stops = [(0, sky_t), (.38, SKY_M), (1, sky_h)] if mode == "ready" else [(0, sky_t), (1, sky_h)]
        D.append(lin_grad(p, "sky", 0, 12, 0, H, stops))
        G = [f'<rect width="128" height="{f(H)}" fill="{(PSKY if mode == "ready" else NSKY_M) if PLAIN else f"url(#{p}sky)"}"/>']
        if mode == "ready" and not ROW:
            # the moon's own bokashi halo in the sky (a printer's wipe, not a glow: the row tier has none)
            D.append(rad_grad(p, "hl", LIT_C[0] - 4, LIT_C[1] - 2, 40, [(0, "#B8C8EC", .2), (1, "#B8C8EC", 0)]))
            G.append(f'<rect width="128" height="{f(H)}" fill="url(#{p}hl)"/>')
        if mode == "ready" and not ROW and not MID:
            castle = "".join(f'<path d="{d}"/>' for d in CASTLE)
            G.append(f'<g fill="#0B0F1F">{castle}</g>')
            G.append(f'<g fill="none" stroke="{KIN}" stroke-width=".45" stroke-linecap="round" opacity=".85">'
                     + "".join(f'<path d="{d}"/>' for d in EAVES) + '</g>')
    # by dusk the unlit disc is the sky's own value (the air in front of it is lit); at night it is earthlit sumi
    dd, ss = crest_moon(p, earth=None if mode == "ready" else EARTH, ring_op=.2 if mode != "ready" else (.42 if ROW else .28)); D += dd; G += ss
    if mode != "rest":
        # the sea: a second cut-paper plane laid over the sky; its top edge is the horizon, lit along its lip where it
        # faces the moon, and a band just under it mirrors the sky (Fresnel)
        D.append(lin_grad(p, "sea", 0, H, 0, 116, [(0, sea_h), (1, sea_b)]))
        D.append(lin_grad(p, "fr", 0, H, 0, H + 10, [(0, sky_h, .5 if mode == "ready" else .3), (1, sky_h, 0)]))
        G.append(f'<rect y="{f(H)}" width="128" height="{f(128 - H)}" fill="{(SEA_M if mode == "ready" else NSEA_M) if PLAIN else f"url(#{p}sea)"}"/>')
        if not PLAIN:
            G.append(f'<rect y="{f(H)}" width="128" height="10" fill="url(#{p}fr)"/>')
        if mode == "ready" and not ROW and not MID:
            # the castle's dark mirror on the calm sea, directly below it, softened by the swell
            D.append(blur_filter(p, "cm", .5))
            castle = "".join(f'<path d="{d}"/>' for d in CASTLE)
            G.append(f'<g filter="url(#{p}cm)"><g fill="#0B0F1F" fill-opacity=".32" transform="matrix(1 0 0 -1 0 {f(2 * H)})">{castle}</g></g>')
        if not ROW:
            D.append(lin_grad(p, "hz", 12, 0, 116, 0, [(0, GOFUN, .05), ((cxr - 12) / 104, GOFUN, .6 if mode == "ready" else .35),
                                                       (1, GOFUN, .05)]))
            G.append(f'<rect x="10" y="{f(H - .4)}" width="108" height=".8" fill="url(#{p}hz)"/>')
        rows = ROAD_ROW if ROW else (ROAD_MID if MID else ROAD_HERO)
        if mode == "ready":
            strips = "".join(f'<path d="{strip(cxr + x0, cxr + x1, y, h)}"/>' for y, h, dashes in rows for x0, x1 in dashes)
            G.append(f'<g fill="{KIN}">{strips}</g>')
        else:
            # In journal: the same road in silver leaf, laid thin and dull (the path is known, not open), mid and hero
            # only; at row size the night sea is bare, so the column never reads as bars
            if not ROW:
                strips = "".join(f'<path d="{strip(cxr + x0, cxr + x1, y, h * .8)}"/>' for y, h, dashes in rows for x0, x1 in dashes)
                G.append(f'<g fill="{GIN_HI}" fill-opacity=".42">{strips}</g>')
        if mode == "ready" and not ROW and not MID:
            # hero: the upper edge of each strip catches the key light (leaf has a cut edge)
            hi = "".join(f'<path d="M{f(cxr + x0 + h * .55)} {f(y - h / 2 + .15)}H{f(cxr + x1 + h * .55)}"/>'
                         for y, h, dashes in rows for x0, x1 in dashes)
            G.append(f'<g stroke="{KIN_HI}" stroke-width=".3" opacity=".9">{hi}</g>')
    S.append(clip_well(p, G + over))
    return D, S


def face(title, key, under, over=([], []), badge_kind=None, job=None):
    """A face: the well ('under', clipped to r 52.4) and anything that overhangs the rim ('over', drawn above the
    kit's frame). The frame tier and the badge come from the kit, by state."""
    return dict(title=title, key=key, under=under, over=over, badge=badge_kind, job=job, tier=STATE_TIER[key])


def ready():
    return face("Ready", "ready", moon_scene("skr-", "ready"), badge_kind="open")


def ready_other_job(job=DEFAULT_JOB):
    return face(f"Ready on another job: {JOBS[job][0]}", "ready-on-another-job", moon_scene(f"sko{job[:3]}-", "rest"),
                badge_kind="job", job=job)


def ribbon(p):
    """The bookmark: an indigo silk ribbon (applied, so front-lit) whose top wraps behind the medal along an arc
    concentric with it (r 66), lies over the frame and drops into the well over the moon's lower horn, ending in a
    swallowtail. Lit edge on the left; a short soft shadow down-right onto the frame and the well."""
    x0, w, y1 = 24.5, 17.5, 76.0
    ya = 64 - math.sqrt(66 ** 2 - (64 - x0) ** 2)
    yb = 64 - math.sqrt(66 ** 2 - (64 - x0 - w) ** 2)
    rib = f"M{f(x0)} {f(ya)}A66 66 0 0 1 {f(x0 + w)} {f(yb)}V{f(y1)}L{f(x0 + w / 2)} {f(y1 - 7)}L{f(x0)} {f(y1)}Z"
    D = [lin_grad(p, "rb", x0, 0, x0 + w, 0, [(0, AI_HI), (.28, AI), (1, AI_DEEP)]),
         f'<clipPath id="{p}rc"><path d="{rib}"/></clipPath>']
    S = []
    if not ROW:
        D.append(blur_filter(p, "rs", 1.0))
        S.append(f'<path d="{rib}" fill="{KEY}" fill-opacity=".55" transform="translate(1.2 1.1)" filter="url(#{p}rs)"/>')
    S.append(f'<path d="{rib}" fill="{AI if PLAIN else f"url(#{p}rb)"}" stroke="{KEY}" stroke-width="{2.2 if not ROW else 3.0}" stroke-linejoin="round" paint-order="stroke"/>')
    # the part that wraps behind the medal's edge is in shade
    S.append(f'<g clip-path="url(#{p}rc)"><path d="{ring(R_KEY, 70)}" fill="{AI_DEEP}" fill-rule="evenodd"/></g>')
    if not ROW and not MID:
        # hero: a gofun stitch near each edge, the silk's woven selvedge
        S.append(f'<g opacity=".45"><path d="M{f(x0 + 2.4)} 14V{f(y1 - 5.5)}M{f(x0 + w - 2.4)} 14V{f(y1 - 4)}" stroke="{GOFUN}" '
                 f'stroke-width=".5" stroke-dasharray="1.6 1.2"/></g>')
    return D, S


def in_journal():
    return face("In journal", "in-journal", moon_scene("skj-", "night"), over=ribbon("skj-o"), badge_kind="journal")


# ---------------- Blocked: a new moon behind silver-leaf mist ----------------
BLOCKED_MOON = (72.0, 44.0, 25.0)
BLOCKED_LIMB = (-0.78, -12.0)       # a thin sunlit limb (about 11% lit), facing a little above the right
# the cloud: one bank of cut silver leaf with a scalloped crown (the kirie cloud of Japanese paper-cutting), low and wide,
# its billows rising toward the moon on the right so they cover the new moon's lower third; its foot runs out of the
# well. Billows (x, y, r) and a rounded base (x0, x1, y_top, y_bottom).
CLOUD_BILLOWS = [(19.0, 86.0, 9.5), (32.0, 77.0, 11.5), (48.0, 71.0, 12.5), (64.0, 68.0, 12.0), (79.0, 63.0, 11.5),
                 (94.0, 63.5, 10.5), (110.0, 72.0, 10.0)]
CLOUD_BASE = (16.0, 124.0, 80.0, 132.0)
CLOUD = "#5C637A"                     # silver leaf gone to pewter: one flat tone, under the limb's value


def cloud_markup(inset=0.0):
    """The cloud's silhouette (a union of billows over a rounded base, with a filler under the crown); 'inset' shrinks
    every part, which traces a contour inside the edge."""
    x0, x1, yt, yb = CLOUD_BASE
    return ("".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r - inset)}"/>' for x, y, r in CLOUD_BILLOWS)
            + f'<rect x="{f(x0 + inset)}" y="{f(yt)}" width="{f(x1 - x0 - 2 * inset)}" height="{f(yb - yt)}"/>'
            + '<rect x="36" y="70" width="68" height="14"/>')


def blocked():
    """The moon is there but you can't reach it: a new moon (an earthlit sumi disc with only a thin sunlit gofun limb)
    behind a bank of cloud cut from silver leaf tarnished to pewter. The leaf is one flat tone held under the limb's
    value, so the limb stays the only light; its scalloped crown is lit along its upper-left edges and casts a short
    shadow on the moon. No gold on a resting state."""
    p = "skb-"
    D, S = [], []
    dd, base, over = well(p); D += dd
    G = list(base)
    mx, my, mr = BLOCKED_MOON
    k, rot = BLOCKED_LIMB
    dd, ss = crest_moon(p, (mx, my, mr, k, rot), earth="#2A2832", ring_op=.16); D += dd; G += ss
    m = cloud_markup()
    dd, ss = cast(p, "c", m, .6); D += dd; G += ss
    G.append(f'<g fill="{CLOUD}">{m}</g>')
    dd, ss = relief(p, "c", m, light=GIN_HI, dark=GIN_DEEP, lo=.85, do=.55, w=1.2); D += dd; G += ss
    if not ROW and not MID:
        # hero: a contour of silver kirikane laid a little inside the cloud's edge, as a maki-e cloud is outlined
        core = poly([(x, y) for x, y, _ in CLOUD_BILLOWS] + [(124.0, 132.0), (16.0, 132.0)])   # under the billows' centres
        D.append(f'<mask id="{p}ct"><g fill="#fff">{cloud_markup(2.4)}</g><g fill="#000">{cloud_markup(2.95)}'
                 f'<path d="{core}"/></g></mask>')
        G.append(f'<rect width="128" height="128" fill="{GIN_HI}" fill-opacity=".5" mask="url(#{p}ct)"/>')
    S.append(clip_well(p, G + over))
    return face("Blocked", "blocked", (D, S), badge_kind="closed")


# ---------------- Done this cycle: a waning half and its "comes back" arc ----------------
DONE_MOON = (60.0, 63.0, 23.0)


def leaf_arc(cx, cy, r0, r1, a0, a1, w, head_len, head_w, n=90):
    """A strip of gold leaf cut along a widening spiral, clockwise from a0 (radius r0) to a1 (radius r1): its tail cut
    to a fine point, then constant width (a cut strip, not a brush stroke), ending in a cut arrowhead pointing on
    round."""
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        a = a0 + (a1 - a0) * t
        rm = r0 + (r1 - r0) * t
        hw = w / 2 * min(.15 + .85 * t / .3, 1)
        outer.append(pt(rm + hw, a, cx, cy))
        inner.append(pt(rm - hw, a, cx, cy))
    da = math.degrees(head_len / r1)
    head = [pt(r1 + head_w / 2, a1, cx, cy), pt(r1, a1 + da, cx, cy), pt(r1 - head_w / 2, a1, cx, cy)]
    return poly(outer + head + inner[::-1])


def done():
    """This cycle's light is spent and will return: a waning half moon (lit on the left, one flat piece of gofun on its
    earthlit disc) and, along its dark edge, a strip of cut gold leaf that curls clockwise from the top to the
    bottom, pointing on round: the moon comes back. Short of a full turn and off-centre, so it is never a refresh
    icon or an orbit ring."""
    p = "skd-"
    D, S = [], []
    dd, base, over = well(p); D += dd
    G = list(base)
    mx, my, mr = DONE_MOON
    dd, ss = crest_moon(p, (mx, my, mr, 0.0, 180.0), earth=EARTH_DONE, ring_op=.2); D += dd; G += ss
    w = 6.4 if ROW else 4.6
    arc = leaf_arc(mx, my, mr + 7.0, mr + 12.5, -112.0, 82.0, w, 10.5 if not ROW else 11.5, 13.0 if not ROW else 15.0)
    shape = f'<path d="{arc}"/>'
    dd, ss = cast(p, "a", shape, .55); D += dd; G += ss
    G.append(f'<path d="{arc}" fill="{KIN}"/>')
    dd, ss = relief(p, "a", shape, light=KIN_HI, dark=KIN_DEEP, lo=.9, do=.6, w=.8); D += dd; G += ss
    S.append(clip_well(p, G + over))
    return face("Done this cycle", "done-this-cycle", (D, S))


# ---------------- Completed: the harvest moon, with the check ----------------
FULL = (60.0, 60.0, 31.0)
# susuki (pampas grass) for the moon-viewing, hero only: blades as tapered curves, and two plumes bowing right
# "Susuki ni tsuki" (the moon and pampas grass of the hanafuda August card and of the moon-viewing): at hero, a low
# hill in sumi silhouette rises across the moon's lower left, and a few susuki blades and two ears stand from its ridge.
HILL = "M-2 130V92C10 86 20 84 31 86C43 88 53 95 61 104C66 110 70 120 71 130Z"
SUSUKI_BLADES = [((20, 87), (23, 79), (29, 72), (37, 67), 1.7), ((28, 87), (34, 80), (42, 75), (52, 73), 1.5),
                 ((13, 90), (12, 82), (14, 75), (19, 69), 1.5), ((38, 90), (46, 85), (55, 82), (64, 82), 1.3)]
SUSUKI_STEMS = [((24, 87), (25, 79), (27, 72), (30, 66), .75), ((33, 88), (35, 82), (39, 76), (44, 72), .65)]
SUSUKI_EARS = [((30, 66), (31.4, 63.6), (33.8, 61.8), (37.4, 61.0), 2.6), ((44, 72), (46.2, 69.6), (49.4, 68.2), (53.6, 68.0), 2.3)]


def taper_curve(p0, p1, p2, p3, w, n=28, mode="blade"):
    pts = bez(p0, p1, p2, p3, n)
    if mode == "blade":            # full width at the root, drawn to a point at the tip
        ws = [w * (1 - i / n) ** .9 + .05 for i in range(n + 1)]
    elif mode == "stem":           # an even stalk
        ws = [w for _ in range(n + 1)]
    else:                          # an ear: a slender plume, widest a third of the way up, drawn to a point
        ws = [w * math.sin(math.pi * min(i / n, .999) ** .7) + .1 for i in range(n + 1)]
    return tapered_path(pts, ws)


def check_over(p):
    """The check, in gold leaf laid flat over a thin raised ground: Medallion's mark and width (so a mix reads one
    check), struck across the lower right and out past the rim, keylined, with a lit upper-left edge and a shaded
    lower-right one, and a short shadow."""
    chk = "M64 86L78 100L117.5 40"

    def stroke(w, col, tr=None):
        t = f' transform="translate({tr[0]} {tr[1]})"' if tr else ""
        return (f'<path d="{chk}" fill="none" stroke="{col}" stroke-width="{f(w)}" stroke-linecap="round" '
                f'stroke-linejoin="round"{t}/>')
    D = [blur_filter(p, "cs", .9)]
    S = [f'<g filter="url(#{p}cs)" opacity=".6">{stroke(15, KEY, (1.1, 1.6))}</g>', stroke(15, KEY), stroke(10, KIN)]
    if not ROW:
        # the leaf's edges: a light band where the stroke's edge faces the key light, a dark one where it faces away
        D.append(f'<mask id="{p}cm">{stroke(10, "#fff")}</mask>')
        S.append(f'<g mask="url(#{p}cm)"><g opacity=".8">{stroke(3.4, "#FFF1C8", (-3.0, -3.3))}</g>'
                 f'<g opacity=".55">{stroke(3.4, KIN_DEEP, (3.0, 3.3))}</g></g>')
    return D, S


def completed():
    """The harvest moon of tsukimi: a full moon in gofun gone cool and quiet, one flat tone two steps under Ready's
    moon, in relief. At hero, the hanafuda August card's image: a low sumi hill at the lower left (clear of the moon, so
    it never reads as a phase) and susuki blades and ears standing from it across the moon, in silhouette (in front of
    the moon, never a mark on it). The gold-leaf check strikes across the lower right and out past the rim."""
    p = "skc-"
    D, S = [], []
    dd, base, over = well(p); D += dd
    G = list(base)
    cx, cy, r = FULL
    disc = f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}"/>'
    dd, ss = cast(p, "m", disc, .45); D += dd; G += ss
    G.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="{CMOON}"/>')
    dd, ss = relief(p, "m", disc, light=CMOON_LIP, dark="#8F887A", lo=.8, do=.4); D += dd; G += ss
    if not ROW and not MID:
        grass = ([taper_curve(*q[:4], q[4]) for q in SUSUKI_BLADES] + [taper_curve(*q[:4], q[4], mode="stem") for q in SUSUKI_STEMS]
                 + [taper_curve(*q[:4], q[4], mode="ear") for q in SUSUKI_EARS])
        G.append(f'<g fill="#050407"><path d="{HILL}"/>' + "".join(f'<path d="{d}"/>' for d in grass) + '</g>')
    S.append(clip_well(p, G + over))
    return face("Completed", "completed", (D, S), over=check_over("skc-o"))


# ---------------- Locked out: Dalamud, its shu lacquer broken into plates ----------------
DALAMUD = (64.0, 64.0, 38.0)
IMPACT = (52.0, 50.0)                        # up and left of centre, off both axes
# Through cracks, which part the lacquer into plates: each a run of (screen angle, length) segments from the impact
# (the last runs out past the rim). Every segment is at least 25 deg off the vertical and the horizontal, each crack
# bends at least once, and no straight run is longer than about 60% of the diameter. 'row': whether the row tier shows
# it (row size shows only the three widest).
THROUGH = [
    # small: at the row and mid tiers this arm swings about 15 deg toward the vertical, so it no longer lines up with t3
    # across the impact: the junction is an unequal "Y" (arms about 97, 109 and 148 deg apart), never a letter "T"
    dict(key="t2", segs=[(-48.0, 12.0), (-66.0, 8.0), (-38.0, None)], small=[(-64.0, 10.0), (-44.0, 5.0), (-64.0, None)], row=True),
    dict(key="t4", segs=[(36.0, 16.0), (64.0, 11.0), (34.0, None)], row=True),
    dict(key="t3", segs=[(150.0, 10.0), (126.0, 7.0), (158.0, None)], row=True),
    dict(key="t1", segs=[(-136.0, 9.0), (-112.0, 5.0), (-148.0, None)], row=False),
]
# Partial cracks that die inside a plate: (the through crack, the waypoint they branch from, angle, length)
PARTIAL = [("t4", 1, 118.0, 15.0), ("t2", 1, -150.0, 10.0)]
# each plate is pushed out along its bisector, so the black ground (the socket) shows between the plates: about one
# device pixel of gap at every tier (row 5.5 units at 16 px, mid 3.0 at 48 px, hero 2.2)
PLATE_DISP = {"row": 6.0, "mid": 3.0, "hero": 2.2}
PLATE_TURN = 1.8                             # degrees, alternating, so no two edges stay parallel
CHIP = (18.0, 11.0, 11.0)                    # the plate that fell out: span along the rim (deg), depth along t3, inset
# each plate lies at a slightly different tilt in its socket, so each takes the key light a little differently
PLATE_TONE = {"t2": "#C94842", "t4": "#BB3D3A", "t3": "#D0524A", "t1": "#C4433F"}
HIBI = [((72, 32), (76, 39), (75, 45)), ((86, 70), (91, 75), (95, 74)), ((46, 82), (50, 88), (56, 90))]


def ray_hit(ix, iy, ang, cx, cy, R):
    ux, uy = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    dx, dy = ix - cx, iy - cy
    b = dx * ux + dy * uy
    c = dx * dx + dy * dy - R * R
    t = -b + math.sqrt(b * b - c)
    return ix + ux * t, iy + uy * t


def crack_points(segs, start, cx, cy, R):
    pts = [start]
    for ang, ln in segs:
        x, y = pts[-1]
        if ln is None:
            pts.append(ray_hit(x, y, ang, cx, cy, R + 3))
        else:
            pts.append((x + ln * math.cos(math.radians(ang)), y + ln * math.sin(math.radians(ang))))
    return pts


def inside(pt_, poly_pts):
    x, y = pt_
    n, c = len(poly_pts), False
    for i in range(n):
        (x0, y0), (x1, y1) = poly_pts[i], poly_pts[(i + 1) % n]
        if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
            c = not c
    return c


def locked_out():
    """Dalamud as a lacquered moon: one disc of shu-urushi broken into plates along cracks that each bend, from an
    impact up and left of centre (52, 50). The plates are pushed a little apart, so the cracks are gaps with the black
    ground (the socket) showing, never drawn seams; two more cracks die inside their plates; one small plate at the
    rim has fallen out. Each plate is flat lacquer with one soft gloss toward the light; edges that face the key light
    catch a pale lip, edges that face away are dark. Row size shows only the three widest cracks. Hero adds hairline
    crazing (hibi) that never opens."""
    p = "skl-"
    D, S = [], []
    dd, base, over = well(p); D += dd
    G = list(base)
    cx, cy, R = DALAMUD
    I = IMPACT
    cracks = [c for c in THROUGH if c["row"] or not ROW]
    segs = {c["key"]: c.get("small", c["segs"]) if (ROW or MID) else c["segs"] for c in THROUGH}
    pts = {k: crack_points(v, I, cx, cy, R) for k, v in segs.items()}
    first = {k: v[0][0] % 360 for k, v in segs.items()}
    order = sorted((c["key"] for c in cracks), key=lambda k: (first[k] - first["t2"]) % 360)
    D.append(f'<clipPath id="{p}dc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}"/></clipPath>')
    D.append(rad_grad(p, "sn", cx - 15, cy - 17, 28, [(0, SHU_HI, .75), (.55, SHU_HI, .2), (1, SHU_HI, 0)]))
    # the socket: the black ground the lacquer was laid on, a shallow dish whose lower-right wall catches the light
    G.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R + .6)}" fill="{SOCKET}"/>')
    if not ROW:
        G.append(f'<path d="{taper_arc(R - 1.2, 10, 120, 2.0, cx, cy)}" fill="{SHU_LO}" fill-opacity=".55"/>')
    # the chip that fell out: a small plate at the rim where t3 runs out, on the side of the plate that follows t3
    e3, s3 = pts["t3"][-1], pts["t3"][-2]
    a3 = math.degrees(math.atan2(e3[1] - cy, e3[0] - cx))
    span, depth, inset = CHIP
    ux, uy = e3[0] - s3[0], e3[1] - s3[1]
    ul = math.hypot(ux, uy)
    q_rim = ray_hit(s3[0], s3[1], math.degrees(math.atan2(uy, ux)), cx, cy, R)
    q_in = (q_rim[0] - ux / ul * depth, q_rim[1] - uy / ul * depth)
    chip = ([q_rim, q_in, pt(R - inset, a3 + span * .7, cx, cy)]
            + [pt(R + 3, a3 + span * (k / 6), cx, cy) for k in range(6, -1, -1)])
    plates = []
    disp = PLATE_DISP[tier()]
    for i, k0 in enumerate(order):
        k1 = order[(i + 1) % len(order)]
        c0, c1 = pts[k0], pts[k1]
        b0 = math.degrees(math.atan2(c0[-1][1] - cy, c0[-1][0] - cx))
        b1 = math.degrees(math.atan2(c1[-1][1] - cy, c1[-1][0] - cx))
        while b1 <= b0:
            b1 += 360
        arc = [pt(R + 3, b0 + (b1 - b0) * j / 24, cx, cy) for j in range(25)]
        poly_pts = c0 + arc + c1[::-1][:-1]
        a0, a1 = first[k0], first[k1]
        while a1 <= a0:
            a1 += 360
        mid = math.radians((a0 + a1) / 2)
        tx, ty = disp * math.cos(mid), disp * math.sin(mid)
        gx = sum(x for x, _ in poly_pts) / len(poly_pts); gy = sum(y for _, y in poly_pts) / len(poly_pts)
        sid = f"{p}s{i}"
        D.append(f'<clipPath id="{sid}"><path d="{poly(poly_pts)}"/></clipPath>')
        g = [f'<g clip-path="url(#{sid})"><g clip-path="url(#{p}dc)">'
             f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="{SHU if PLAIN else PLATE_TONE[k0]}"/>']
        if not ROW:
            g.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="url(#{p}sn)"/>')
            # fracture faces: lit if their outward normal turns toward the key light, shaded if away
            area = sum(poly_pts[k][0] * poly_pts[(k + 1) % len(poly_pts)][1] - poly_pts[(k + 1) % len(poly_pts)][0] * poly_pts[k][1]
                       for k in range(len(poly_pts)))
            sgn = 1 if area > 0 else -1
            for k in range(len(poly_pts)):
                (x0, y0), (x1, y1) = poly_pts[k], poly_pts[(k + 1) % len(poly_pts)]
                if math.hypot(x0 - cx, y0 - cy) > R + 1.5 and math.hypot(x1 - cx, y1 - cy) > R + 1.5:
                    continue
                dx, dy = x1 - x0, y1 - y0
                ln = math.hypot(dx, dy) or 1
                nx, ny = sgn * dy / ln, -sgn * dx / ln
                facing = nx * LIGHT_V[0] + ny * LIGHT_V[1]
                if facing > .15:
                    g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{SHU_LIP}" stroke-width="{f(.8 + .8 * facing)}" '
                             f'stroke-opacity="{f(.35 + .4 * facing)}" stroke-linecap="round"/>')
                elif facing < -.15:
                    g.append(f'<path d="M{f(x0)} {f(y0)}L{f(x1)} {f(y1)}" stroke="{SHU_LO}" stroke-width="{f(.8 - .8 * facing)}" '
                             f'stroke-opacity=".85" stroke-linecap="round"/>')
            # the limb: the lacquer's rounded edge, lit on the upper left, in shade on the lower right
            D.append(lin_grad(p, f"lr{i}", cx - R, cy - R, cx + R, cy + R,
                              [(0, SHU_LIP, .6), (.4, SHU_LIP, .05), (.62, SHU_LO, 0), (1, SHU_LO, .7)]))
            g.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R - 1.0)}" fill="none" stroke="url(#{p}lr{i})" stroke-width="2"/>')
            for pk, wi, ang, ln in PARTIAL:
                root = pts[pk][wi]
                tip = (root[0] + ln * math.cos(math.radians(ang)), root[1] + ln * math.sin(math.radians(ang)))
                mid_pt = ((root[0] + tip[0]) / 2, (root[1] + tip[1]) / 2)
                if inside(mid_pt, poly_pts):
                    w0 = 2.0 if MID else 1.5
                    path = [root, (root[0] * .55 + tip[0] * .45 + .8, root[1] * .55 + tip[1] * .45 - .6), tip]
                    g.append(f'<path d="{tapered_path(path, [w0, w0 * .55, .1])}" fill="{SOCKET}"/>')
            if not MID:
                for h in HIBI:
                    if inside(h[1], poly_pts):
                        g.append(f'<path d="{poly(h, close=False)}" fill="none" stroke="{SHU_LO}" stroke-width=".5" stroke-linecap="round"/>'
                                 f'<path d="{poly(h, close=False)}" fill="none" stroke="{SHU_LIP}" stroke-width=".3" stroke-linecap="round" '
                                 f'opacity=".35" transform="translate(.4 .5)"/>')
        g.append('</g></g>')
        body = "".join(g)
        if k0 == "t3":     # the plate that held the chip: the chip is gone, the socket shows
            D.append(f'<mask id="{p}cm"><rect x="-20" y="-20" width="168" height="168" fill="#fff"/><path d="{poly(chip)}" fill="#000"/></mask>')
            body = f'<g mask="url(#{p}cm)">{body}</g>'
        turn = PLATE_TURN * (1 if i % 2 else -1) * (disp / 3.0)
        plates.append(f'<g transform="translate({f(tx)} {f(ty)}) rotate({f(turn)} {f(gx)} {f(gy)})">{body}</g>')
    G.append("".join(plates))
    S.append(clip_well(p, G + over))
    return face("Locked out", "locked-out", (D, S))


# ---------------- Not checked: a faint moon, and a "?" brushed in gofun ----------------
VEIL = (64.0, 53.0, 21.0)


def question_mark():
    """The "?" as one stroke of a soft brush. It enters thin at the left horn of a faint moon, swells over the top (the
    bowl is that moon's limb) to its full width at the upper right, thins as it turns in to the stem, and lifts off at
    the foot. At row size the foot is a rounded tome; at mid and hero it lifts off dry (kasure): the last few units part
    into three tines. The dot is a tiny full moon. Width is drawn, never textured."""
    vx, vy, vr = VEIL
    wmax = 15.0 if ROW else 13.0
    pts, ws = [], []
    a0, a1 = 198.0, 398.0
    n = 48
    for i in range(n + 1):
        t = i / n
        a = a0 + (a1 - a0) * t
        pts.append(pt(vr, a, vx, vy))
        if ROW:
            ws.append(wmax * (.3 + .7 * math.sin(math.pi / 2 * min(t / .5, 1)) ** .9) if t < .5 else wmax - wmax * .28 * (t - .5) / .5)
        else:     # thick to thin: a thin, sharp entry, full at a third of the way, then thinning steadily
            ws.append(wmax * (.12 + .88 * math.sin(math.pi / 2 * min(t / .36, 1)) ** 1.3) if t < .36
                      else wmax * (1 - .42 * (t - .36) / .64))
    ex, ey = pts[-1]
    tx, ty = -math.sin(math.radians(a1)), math.cos(math.radians(a1))
    stem = bez((ex, ey), (ex + tx * 7, ey + ty * 7), (vx, vy + vr + 3), (vx, vy + vr + 9), 16)[1:]
    w_end = wmax * (.72 if ROW else .58)
    for j, (x, y) in enumerate(stem):
        pts.append((x, y)); ws.append(w_end if ROW else ws[n] + (w_end - ws[n]) * (j + 1) / len(stem))
    qm = tapered_path(pts, ws)
    end = pts[-1]
    dot = (vx, end[1] + (14.5 if not ROW else 15.5), 6.3 if not ROW else 7.6)
    tome = (end[0], end[1], wmax * .36) if ROW else None
    # kasure: two slits that open from the foot upward, parting the end of the stroke into three tines
    slits = []
    if not ROW:
        x, y = end
        for off, ln in ((-w_end * .17, 7.5), (w_end * .2, 5.0)):
            slits.append(poly([(x + off - .45, y + .6), (x + off + .35, y + .6), (x + off + .05, y - ln)]))
    return qm, tome, dot, slits


def not_checked():
    p = "skn-"
    D, S = [], []
    dd, base, over = well(p); D += dd
    G = list(base)
    vx, vy, vr = VEIL
    # the faint moon: a lifted sumi disc with a gofun hairline, like an unfinished crest drawn but not yet laid
    G.append(f'<circle cx="{f(vx)}" cy="{f(vy)}" r="{f(vr + 6)}" fill="{NOTC_MOON}"/>')
    G.append(f'<circle cx="{f(vx)}" cy="{f(vy)}" r="{f(vr + 6 - (.9 if ROW else .4))}" fill="none" stroke="{GOFUN}" '
             f'stroke-opacity="{.3 if ROW else .22}" stroke-width="{1.8 if ROW else .8}"/>')
    qm, tome, (dx, dy, dr), slits = question_mark()
    shape = (f'<path d="{qm}"/>' + (f'<circle cx="{f(tome[0])}" cy="{f(tome[1])}" r="{f(tome[2])}"/>' if tome else "")
             + f'<circle cx="{f(dx)}" cy="{f(dy)}" r="{f(dr)}"/>')
    q = [f'<g fill="{GOFUN_MID}">{shape}</g>']
    dd, ss = relief(p, "q", shape, light=GOFUN_HI, dark=GOFUN_LO, lo=.55, do=.35); D += dd; q += ss
    dd, ss = cast(p, "q", shape, .5); D += dd; G += ss
    if slits:
        D.append(f'<mask id="{p}qk"><rect x="-20" y="-20" width="168" height="168" fill="#fff"/>'
                 f'<g fill="#000">{"".join(f"<path d={chr(34)}{d}{chr(34)}/>" for d in slits)}</g></mask>')
        q = [f'<g mask="url(#{p}qk)">'] + q + ['</g>']
    G += q
    S.append(clip_well(p, G + over))
    return face("Not checked", "not-checked", (D, S))


# ---------------- write: faces, the Kirikane kit, composites ----------------
STATES = (("ready", ready), ("ready-on-another-job", ready_other_job), ("in-journal", in_journal),
          ("blocked", blocked), ("done-this-cycle", done), ("completed", completed),
          ("locked-out", locked_out), ("not-checked", not_checked))
BADGE_TIER = {"open": "act-now"}


def part_svg(title, part):
    D, S = part
    return svg(title, D, S)


def compose(fc, quiet=False):
    """Face (under) + the Kirikane kit's frame for the state's tier + the kit's badge + the face's overhangs."""
    D, S = list(fc["under"][0]), list(fc["under"][1])
    dd, ss = kit_frame("kf-", fc["tier"], quiet); D += dd; S += ss
    if fc["badge"]:
        dd, ss = badge("kb-", fc["badge"], fc["job"], BADGE_TIER.get(fc["badge"], "resting")); D += dd; S += ss
    D += fc["over"][0]; S += fc["over"][1]
    return svg(fc["title"], D, S)


# Plain's rim is Medallion's (MedalTokens.PlainRim, at PlainRimAlpha .6): each state's own ink, so the two Plain
# finishes line up in a mix
PLAIN_RIM = {"ready": "#F2D27A", "in-journal": "#F2D27A", "completed": "#D6B25A", "ready-on-another-job": "#DDE3F0",
             "done-this-cycle": "#DDE3F0", "blocked": "#7C86A8", "locked-out": "#B25C7F", "not-checked": "#8A93B0"}
PLAIN_RIM_ALPHA = .6


def plain_rim(state):
    """Decoration Plain has no frame (theme-system 3.5): the flat face sits on a flat lacquer seat to r 60 and carries
    a rim in its state's own ink at .6 (Medallion's Plain rim), about one device pixel at 16-20 px (r 54-60, centred
    near Medallion's 56.8)."""
    return [], [f'<path d="{ring(R_WELL - .4, 60.0)}" fill="{WELL_T}" fill-rule="evenodd"/>',
                f'<path d="{ring(54.0, 60.0)}" fill="{PLAIN_RIM[state]}" fill-opacity="{PLAIN_RIM_ALPHA}" fill-rule="evenodd"/>']


def compose_plain(fc):
    D, S = list(fc["under"][0]), list(fc["under"][1])
    dd, ss = plain_rim(fc["key"]); D += dd; S += ss
    D += fc["over"][0]; S += fc["over"][1]
    return svg(fc["title"] + " (Plain)", D, S)


def write_plain():
    """_plain/: the flat finish, one master per state for every size (no badge: the row badge glyphs serve)."""
    global ROW, PLAIN
    ROW, PLAIN = True, True
    try:
        base = OUT / "_plain"
        base.mkdir(exist_ok=True)
        for key, fn in STATES:
            (base / f"{key}.svg").write_text(compose_plain(fn()), encoding="utf-8")
    finally:
        ROW, PLAIN = False, False


def write_ornaments():
    """kit/ornaments/: the Kirikane kit's Decoration ornaments, in flat gold leaf (they sit on the pane).
    - sigil.svg: the crescent-in-circle crest (maru ni tsuki) at 13 px and up, with the leaf's lit edge.
    - sigil-small.svg: the same crest drawn for 10-12 px: a bolder ring (just over 1 px at 10 px), a thick crescent,
      and a full pixel of pane between them, no hairlines. Never below 10 px: there it turns to a ring with a blob.
    - lozenge.svg: below 10 px the sigil gives way to a single 2 px gold-leaf lozenge, as Medallion's divider does
      (draw it at exactly 2 px, or 2 px times the UI scale).
    - corner.svg: the square kamon corner mark, an L of kirikane with a cut square at its corner."""
    d = OUT / "kit" / "ornaments"
    d.mkdir(parents=True, exist_ok=True)
    md, mt = phase(16, 16, 9.0, -.3, "right", 28.0)
    body = (f'<path d="{ring(10.8, 13.0, 16, 16)}" fill="{KIN}" fill-rule="evenodd"/>'
            f'<path d="{md}" transform="{mt}" fill="{KIN}"/>'
            f'<path d="{taper_arc(11.25, -165, -105, .5, 16, 16)}" fill="{KIN_HI}"/>')
    (d / "sigil.svg").write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">'
                                 f'<title>Kirikane sigil: crescent in a circle (13 px and up)</title>{body}</svg>\n', encoding="utf-8")
    sd, st = phase(16, 16, 7.6, -.08, "right", 28.0)
    body = (f'<path d="{ring(11.6, 15.2, 16, 16)}" fill="{KIN}" fill-rule="evenodd"/>'
            f'<path d="{sd}" transform="{st}" fill="{KIN}"/>')
    (d / "sigil-small.svg").write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">'
                                       f'<title>Kirikane sigil, small: 10-12 px only (below 10 px draw lozenge.svg)</title>{body}</svg>\n',
                                       encoding="utf-8")
    (d / "lozenge.svg").write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 2 2" width="2" height="2">'
                                   f'<title>Kirikane lozenge: the sigil under 10 px, drawn at 2 px</title>'
                                   f'<path d="M1 0L2 1L1 2L0 1Z" fill="{KIN}"/></svg>\n', encoding="utf-8")
    corner = (f'<path d="M3 29V3H29V5.2H5.2V29Z" fill="{KIN}"/>'
              f'<path d="M8.4 29V8.4H29" fill="none" stroke="{KIN}" stroke-width=".7" stroke-opacity=".75"/>'
              f'<path d="M1.6 1.6H6.6V6.6H1.6Z" fill="{KIN}"/><path d="M1.6 1.6H6.6V2.2H2.2V6.6H1.6Z" fill="{KIN_HI}"/>')
    (d / "corner.svg").write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">'
                                  f'<title>Kirikane corner mark (top left; mirror for the others)</title>{corner}</svg>\n', encoding="utf-8")


def write_tier(sub):
    """Faces (under and over) and composites for the current tier into OUT/<sub>."""
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
    """The Kirikane kit: four urgency tiers at Full and Quiet (hero and row), the badge rings, seats and glyphs."""
    global ROW
    kit = OUT / "kit"
    for row in (False, True):
        ROW = row
        d = kit / "_row" if row else kit
        d.mkdir(parents=True, exist_ok=True)
        for t in TIERS:
            for quiet in (False, True):
                D, S = kit_frame("kf-", t, quiet)
                (d / f"frame-{t}-{'quiet' if quiet else 'full'}.svg").write_text(
                    svg(f"Kirikane kit frame: {t}, {'Quiet' if quiet else 'Full'}", D, S), encoding="utf-8")
    ROW = False
    for t in ("act-now", "resting"):
        D, S = badge_ring("kr-", t)
        (kit / f"badge-ring-{t}.svg").write_text(svg(f"Kirikane kit badge ring: {t}", D, S), encoding="utf-8")
    for kind in ("open", "closed", "journal", "tank", "healer", "dps"):
        D, S = badge_seat("ks-", kind)
        (kit / f"badge-seat-{kind}.svg").write_text(svg(f"Kirikane kit badge seat: {kind}", D, S), encoding="utf-8")
    for kind in ("open", "closed", "journal"):
        D, S = badge_content("kc-", kind)
        (kit / f"glyph-{kind}.svg").write_text(svg(f"Kirikane kit badge glyph: {kind}", D, S), encoding="utf-8")


def main():
    global ROW, MID
    write_tier("")
    MID = True
    write_tier("_mid")
    MID = False
    ROW = True
    write_tier("_row")
    ROW = False
    write_kit()
    write_plain()
    write_ornaments()
    # the badge glyphs alone at text height, for drawing beside a row-tier medal
    for kind in ("open", "closed", "journal"):
        pp = f"skk{kind[0]}-"
        dd, ss = lock_glyph(pp, 0, 0, kind == "open") if kind != "journal" else book_glyph(pp, 0, 0)
        (OUT / "_row" / f"badge-{kind}.svg").write_text(
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="-17 -18 34 34" width="34" height="34"><title>{kind} badge glyph</title>'
            f'<defs>{"".join(dd)}</defs>{"".join(ss)}</svg>\n', encoding="utf-8")
    print("lit centroid", LIT_C)


if __name__ == "__main__":
    main()
