"""Ishgard Glass: generator for the 8 state glyphs (128 grid) and the plugin icon (512)."""
import math, pathlib, random

OUT = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6\round2\ishgard-glass")
OUT.mkdir(parents=True, exist_ok=True)

# ---- palette tokens
SUMI = "#0B0F1C"
IVORY, IVORY_COOL = "#F3F0E6", "#E2E7F1"      # lit glass, act-now (Ready only)
MOONSTONE, MOONSTONE_ALT = "#C3CEE4", "#B3C0DA"  # lit glass, every other state
MOON_DIM, MOON_DIM_ALT = "#7E8EB2", "#7686AB"  # Completed: the quiet full moon
LAPIS, LAPIS2 = "#1B2A57", "#22346C"
ASH = "#2B3658"
GILT_HI, GILT, GILT_MID, GILT_LO, GILT_DK = "#FFF3D1", "#E9D49C", "#C9A766", "#A88B52", "#6B5124"
LEAD = "#8A6E3A"
PEW_HI, PEW, PEW_LO, PEW_DK = "#8C95B0", "#69728F", "#474F6C", "#323950"
AETHER_HI, AETHER, AETHER_LO = "#9BE6FF", "#4FB3EA", "#1F5FA8"
RED, RED_ALT, RED_DK = "#A23A38", "#8F3133", "#6A2024"
PAPER = "#EDE3C8"
MIST = "#A9B6D2"


def f(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def dim(hexcol, k):
    """Same hue, lower light: scale a colour in linear light (a reflection loses light, not hue)."""
    c = [int(hexcol[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    lin = [x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c]
    out = [v * k for v in lin]
    srgb = [12.92 * v if v <= .0031308 else 1.055 * v ** (1 / 2.4) - .055 for v in out]
    return "#" + "".join(f"{round(max(0, min(1, v)) * 255):02X}" for v in srgb)


def lit_path(cx, cy, r, k, kind):
    if kind == "full":
        return f"M{f(cx-r)} {f(cy)}A{f(r)} {f(r)} 0 1 1 {f(cx+r)} {f(cy)}A{f(r)} {f(r)} 0 1 1 {f(cx-r)} {f(cy)}Z"
    top, bot = f"{f(cx)} {f(cy-r)}", f"{f(cx)} {f(cy+r)}"
    p = f"M{top}A{f(r)} {f(r)} 0 0 1 {bot}"
    if kind == "half":
        return p + "Z"
    return p + f"A{f(k*r)} {f(r)} 0 0 {1 if kind == 'gibbous' else 0} {top}Z"


def term_path(cx, cy, r, k, kind):
    top, bot = f"{f(cx)} {f(cy-r)}", f"{f(cx)} {f(cy+r)}"
    if kind == "half":
        return f"M{top}L{bot}"
    return f"M{top}A{f(k*r)} {f(r)} 0 0 {0 if kind == 'gibbous' else 1} {bot}"


def gib_centroid(r, k):
    return 4 * r / (3 * math.pi) * (1 - k)


def rot(cx, cy, ang):
    return f' transform="rotate({f(ang)} {f(cx)} {f(cy)})"' if ang else ""


def side(cx, cy, R, s, c, start):
    """Leaded-light came for one side (s=+1 lit side, -1 dark side), in the local frame.
    Returns (mid_line, hero_lines, tinted_panes): one straight came parallel to the terminator at |x| = c R, and three
    voussoir came (-40, 0, +40 degrees) dividing the limb pane into four lights like the outer ring of a rose window."""
    x = cx + s * c * R
    h = R * math.sqrt(1 - c * c)
    mid = f"M{f(x)} {f(cy-h)}L{f(x)} {f(cy+h)}"
    hero, pts = "", {}
    for a, fy in ((-40, -.3), (0, 0), (40, .3)):
        t = math.radians(a)
        lx, ly = cx + s * R * 1.05 * math.cos(t), cy + R * 1.05 * math.sin(t)
        hero += f"M{f(x)} {f(cy+fy*R)}L{f(lx)} {f(ly)}"
        pts[a] = (x, cy + fy * R, lx, ly)
    # tint alternate lights: (-40..0) and (+40..pole)
    a0, a1, a2 = pts[-40], pts[0], pts[40]
    pane = (f"M{f(a0[0])} {f(a0[1])}L{f(a0[2])} {f(a0[3])}L{f(cx+s*R*1.2)} {f(cy-R*.35)}L{f(a1[2])} {f(a1[3])}L{f(a1[0])} {f(a1[1])}Z"
            f"M{f(a2[0])} {f(a2[1])}L{f(a2[2])} {f(a2[3])}L{f(cx+s*R*.9)} {f(cy+R*1.2)}L{f(x)} {f(cy+R*1.2)}Z")
    return mid, hero, pane


def roundel(p, cx, cy, R, metal, dark, lit=None, ang=0, lit_fill=MOONSTONE, lit_alt=MOONSTONE_ALT, mid=True,
            hero=True, ticks=0, rimw=6.0, lead=LEAD, dark_lead=GILT_MID, dark_alt=LAPIS2, sheen=True, term=True):
    """Stained-glass roundel: sumi keyline, two-tone bevel came, dark glass, lit glass, came by tier.
    Row: silhouette + two tones (+ terminator).  Mid: one came splits the lit glass.  Hero: fan + axis came, tinted pane."""
    D, B = [], []
    w_mid, w_hair = R * .065, R * .032
    D.append(f'<clipPath id="{p}disc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}"/></clipPath>')
    if metal:
        st = ([(0, GILT_HI), (.3, GILT), (.62, GILT_LO), (1, GILT_DK)] if metal == "gilt"
              else [(0, PEW_HI), (.3, PEW), (.68, PEW_LO), (1, PEW_DK)])
        D.append(f'<linearGradient id="{p}rim" x1="{f(cx-R)}" y1="{f(cy-R)}" x2="{f(cx+R)}" y2="{f(cy+R)}" gradientUnits="userSpaceOnUse">'
                 + "".join(f'<stop offset="{o}" stop-color="{c}"/>' for o, c in st) + '</linearGradient>')
        B.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R+rimw+1.8)}" fill="{SUMI}" fill-opacity=".85"/>')
    B.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R+.6)}" fill="{dark}"/>')
    kind, k = lit if lit else ("none", 0)
    t_x = cx - k * R if kind == "gibbous" else (cx + k * R if kind == "crescent" else cx)
    # dark-side came (drawn under the lit glass; only what falls on dark glass stays visible)
    if hero and kind != "full":
        dm, dh, dp = side(cx, cy, R, -1, .58, t_x)
        B.append(f'<g clip-path="url(#{p}disc)"{rot(cx, cy, ang)}><path d="{dp}" fill="{dark_alt}" fill-opacity=".8"/>'
                 f'<path d="{dm}{dh}" fill="none" stroke="{dark_lead}" stroke-opacity=".32" stroke-width="{f(w_hair)}"/></g>')
    if lit:
        d = lit_path(cx, cy, R, k, kind)
        D.append(f'<clipPath id="{p}lit"><path d="{d}"{rot(cx, cy, ang)}/></clipPath>')
        B.append(f'<path d="{d}" fill="{lit_fill}"{rot(cx, cy, ang)}/>')
        cl = f'url(#{p}lit)'
        if kind == "full":
            parts = [side(cx, cy, R, 1, .42, cx - R * .25)]
            if hero:
                B.append(f'<g clip-path="{cl}"{rot(cx, cy, ang)}>' + "".join(f'<path d="{pp}" fill="{lit_alt}"/>' for _, _, pp in parts)
                         + f'<path d="{"".join(m + h for m, h, _ in parts)}" fill="none" stroke="{lead}" stroke-opacity=".35" '
                           f'stroke-width="{f(w_hair)}"/></g>')
        elif kind != "crescent":
            m, h, pp = side(cx, cy, R, 1, .42 if kind == "gibbous" else .5, t_x)
            g = f'<g clip-path="{cl}"{rot(cx, cy, ang)}>'
            if hero:
                g += f'<path d="{pp}" fill="{lit_alt}"/><path d="{h}" fill="none" stroke="{lead}" stroke-opacity=".55" stroke-width="{f(w_hair)}"/>'
            if mid:
                g += f'<path d="{m}" fill="none" stroke="{lead}" stroke-opacity=".8" stroke-width="{f(w_mid)}"/>'
            B.append(g + '</g>')
        if kind != "full" and term:
            B.append(f'<path d="{term_path(cx, cy, R, k, kind)}" fill="none" stroke="{GILT_MID}" stroke-width="{f(w_mid)}" '
                     f'stroke-linecap="round"{rot(cx, cy, ang)}/>')
    if sheen:
        D.append(f'<linearGradient id="{p}sh" x1="{f(cx-R)}" y1="{f(cy-R)}" x2="{f(cx+R)}" y2="{f(cy+R)}" gradientUnits="userSpaceOnUse">'
                 f'<stop offset=".16" stop-color="#fff" stop-opacity="0"/><stop offset=".28" stop-color="#fff" stop-opacity=".12"/>'
                 f'<stop offset=".4" stop-color="#fff" stop-opacity="0"/></linearGradient>')
        B.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="url(#{p}sh)"/>')
    if metal:
        B.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R+rimw/2)}" fill="none" stroke="url(#{p}rim)" stroke-width="{f(rimw)}"/>')
        a0, a1, rr = math.radians(140), math.radians(310), R + rimw * .72
        B.append(f'<path d="M{f(cx+rr*math.cos(a0))} {f(cy+rr*math.sin(a0))}A{f(rr)} {f(rr)} 0 0 1 {f(cx+rr*math.cos(a1))} '
                 f'{f(cy+rr*math.sin(a1))}" fill="none" stroke="{GILT_HI if metal == "gilt" else "#DCE2EE"}" stroke-opacity=".5" '
                 f'stroke-width="{f(R*.022)}" stroke-linecap="round"/>')
        if ticks == -1:
            # engraved gilt inlay running round the middle of the came (Completed, hero-subtle)
            B.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R+rimw*.5)}" fill="none" stroke="{GILT}" stroke-opacity=".75" '
                     f'stroke-width="{f(R*.03)}"/>')
        elif ticks:
            t = "".join(f'M{f(cx+(R+rimw*.2)*math.cos(a))} {f(cy+(R+rimw*.2)*math.sin(a))}L{f(cx+(R+rimw*.85)*math.cos(a))} '
                        f'{f(cy+(R+rimw*.85)*math.sin(a))}' for a in (math.radians(-90 + i * 360 / ticks) for i in range(ticks)))
            B.append(f'<path d="{t}" stroke="{GILT}" stroke-opacity=".9" stroke-width="{f(R*.045)}" stroke-linecap="round"/>')
    return D, B


def shard(x, y, w, h, slant=0.0):
    """A glint cut like a pane: a long hexagon with shallow points."""
    e = min(h * 1.4, w * .3)
    return (f'M{f(x)} {f(y+h/2)}L{f(x+e+slant)} {f(y)}L{f(x+w-e+slant)} {f(y)}L{f(x+w)} {f(y+h/2)}'
            f'L{f(x+w-e-slant)} {f(y+h)}L{f(x+e-slant)} {f(y+h)}Z')


def svg(title, D, B, vb=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {vb} {vb}" width="{vb}" height="{vb}"><title>{title}</title>'
            f'<defs>{"".join(D)}</defs>{"".join(B)}</svg>\n')


# ---- shared moon geometry (Ready == the icon's moon)
TILT = 55      # lit limb faces 55 degrees below horizontal: the sun is under the horizon
K = 0.25       # gibbous terminator
R0 = 40


def ready():
    p = "rdy-"
    cx, cy, R = 64, 46, 37
    D, B = roundel(p, cx, cy, R, "gilt", LAPIS, ("gibbous", K), TILT, lit_fill=IVORY, lit_alt=IVORY_COOL, rimw=7)
    ax = cx + gib_centroid(R, K) * math.cos(math.radians(TILT))
    # the moon road, lens-shaped: narrow by the moon, widest in the middle, broken and narrower in front
    rows = [(94, 10, [(-12, 24)], .78), (107, 11, [(-29, 30), (6, 27)], 1), (121, 7, [(-22, 18), (2, 22)], .88)]
    for y, h, segs, op in rows:
        for dx, w in segs:
            B.append(f'<path d="{shard(ax+dx, y, w, h, 1.2)}" fill="{IVORY}" fill-opacity="{op}" stroke="{GILT_LO}" '
                     f'stroke-opacity=".55" stroke-width="1" stroke-linejoin="round"/>')
    return svg("Ready", D, B), ax


def ready_other_job():
    p = "roj-"
    cx, cy, R = 64, 64, R0
    D, B = roundel(p, cx, cy, R, "pewter", LAPIS, ("gibbous", K), TILT)
    a = math.radians(TILT + 180)
    d = R * .6
    kx, ky = cx + d * math.cos(a), cy + d * math.sin(a)
    hw, hh = 9.5, 15
    B.append(f'<path d="M{f(kx)} {f(ky-hh)}L{f(kx+hw)} {f(ky)}L{f(kx)} {f(ky+hh)}L{f(kx-hw)} {f(ky)}Z" fill="{AETHER}" '
             f'stroke="{SUMI}" stroke-width="1.6" stroke-linejoin="round"/>')
    B.append(f'<path d="M{f(kx)} {f(ky-hh)}L{f(kx+hw)} {f(ky)}L{f(kx)} {f(ky)}Z" fill="{AETHER_HI}"/>')
    B.append(f'<path d="M{f(kx)} {f(ky)}L{f(kx-hw)} {f(ky)}L{f(kx)} {f(ky+hh)}Z" fill="{AETHER_LO}" fill-opacity=".85"/>')
    return svg("Ready on another job", D, B)


def in_journal():
    p = "jrn-"
    cx, cy, R = 66, 66, R0
    D, B = roundel(p, cx, cy, R, "pewter", LAPIS, ("half", 0), 0, mid=False)
    x0, x1, y0, y1 = 36, 54, 7, 82
    D.append(f'<linearGradient id="{p}rib" x1="{x0}" y1="0" x2="{x1}" y2="0" gradientUnits="userSpaceOnUse">'
             f'<stop offset="0" stop-color="{GILT}"/><stop offset=".55" stop-color="{GILT_MID}"/><stop offset="1" stop-color="{GILT_LO}"/></linearGradient>')
    B.append(f'<path d="M{x0} {y0}H{x1}V{y1}L{(x0+x1)/2} {y1-8}L{x0} {y1}Z" fill="url(#{p}rib)" stroke="{SUMI}" '
             f'stroke-width="2" stroke-linejoin="round"/>')
    B.append(f'<path d="M{x0+3} {y0+3}V{y1-5}" stroke="{GILT_HI}" stroke-opacity=".4" stroke-width="1.2"/>')
    return svg("In journal", D, B)


def blocked():
    p = "blk-"
    D, B = roundel(p, 64, 64, R0, "pewter", ASH, ("crescent", .66), 0, dark_alt="#323E62", dark_lead=MIST)
    return svg("Blocked", D, B)


def done():
    p = "don-"
    cx, cy, R = 64, 64, R0
    D, B = roundel(p, cx, cy, R, "pewter", LAPIS, ("half", 0), 180, mid=False)
    pts = f"M{cx+7} {cy+2}L{cx+15} {cy+12}L{cx+29} {cy-13}"
    B.append(f'<path d="{pts}" fill="none" stroke="{SUMI}" stroke-width="17" stroke-linecap="round" stroke-linejoin="round"/>')
    B.append(f'<path d="{pts}" fill="none" stroke="{GILT}" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"/>')
    return svg("Done this cycle", D, B)


def completed():
    p = "cmp-"
    D, B = roundel(p, 64, 64, R0, "pewter", MOON_DIM, ("full", 0), TILT, lit_fill=MOON_DIM, lit_alt=MOON_DIM_ALT,
                   ticks=-1, lead=PEW_DK)
    return svg("Completed", D, B)


def locked():
    p = "lck-"
    cx, cy, R = 64, 64, R0
    D, B = roundel(p, cx, cy, R, "pewter", RED_DK, ("gibbous", K), TILT, lit_fill=RED, lit_alt=RED_ALT, lead="#4A1418",
                   dark_alt="#5E1C20", dark_lead="#C98A7A", term=False)
    L, W = 50, 10.5
    band = f"M{-L} {-W}H{L}L{L-7} 0L{L} {W}H{-L}L{-L+7} 0Z"
    B.append(f'<g transform="translate({cx} {cy}) rotate(-36)"><path d="{band}" fill="{PAPER}" stroke="{SUMI}" stroke-width="2.2" '
             f'stroke-linejoin="round"/><path d="M{-L+12} {-W+3.5}H{L-12}M{-L+12} {W-3.5}H{L-12}" stroke="#B9A57A" stroke-opacity=".5" stroke-width="1"/></g>')
    return svg("Locked out", D, B)


def not_checked():
    p = "nck-"
    cx, cy, R = 64, 64, R0
    lp = lit_path(cx, cy, R, .2, "gibbous")
    B = [f'<circle cx="{cx}" cy="{cy}" r="{R+5}" fill="none" stroke="{SUMI}" stroke-opacity=".3" stroke-width="5"/>',
         f'<circle cx="{cx}" cy="{cy}" r="{R+5}" fill="{MIST}" fill-opacity=".1"/>',
         f'<circle cx="{cx}" cy="{cy}" r="{R+5}" fill="none" stroke="{MIST}" stroke-opacity=".3" stroke-width="2"/>',
         f'<path d="{lp}" fill="{MIST}" fill-opacity=".22"/>']
    for y, x0, x1, h, op in ((53, 8, 100, 14, .42), (79, 34, 122, 13, .34)):
        B.append(f'<path d="M{x0} {y}Q{(x0+x1)/2} {y-h} {x1} {y-1}Q{(x0+x1)/2} {y+h*.15} {x0} {y}Z" fill="{MIST}" fill-opacity="{op}"/>')
    return svg("Not checked", [], B)


def make_glints(sd, H, ty, ax, spec):
    """Glints: a lens envelope around the axis; long, broken; thicker and sparser toward the viewer."""
    rnd = random.Random(sd)
    rows, y, r = [], ty + 34, 0
    while y + 12 + 11 * ((y - H) / (512 - H)) < 490:
        t = (y - H) / (spec - H)
        hw = 26 + 52 * min(t, 1) ** .8 - (20 * (t - 1) if t > 1 else 0)
        h = 12 + 11 * ((y - H) / (512 - H))
        n = rnd.choice([1, 1, 2] if t < .35 else ([1, 2, 3] if t < .8 else [2, 3, 3]))
        left = ax - hw * rnd.uniform(.8, 1.15) + rnd.uniform(-8, 8)
        right = ax + hw * rnd.uniform(.8, 1.15) + rnd.uniform(-8, 8)
        while True:
            gaps = [rnd.uniform(9, 24) for _ in range(n - 1)]
            free = right - left - sum(gaps)
            ws = [rnd.uniform(.35, 1.65) for _ in range(n)]
            ws = [w / sum(ws) * free for w in ws]
            if min(ws) >= 32 or n == 1:
                break
            n -= 1 if rnd.random() < .5 else 0
        x = left
        bright = max(.5, min(1.0, 1 - abs(t - 1) * .7)) * .88
        for j in range(n):
            edge = abs(x + ws[j] / 2 - ax) > .6 * hw
            rows.append((x, y, ws[j], h, bright * (.8 if edge else 1), r, gaps[j] if j < n - 1 else None))
            x += ws[j] + (gaps[j] if j < n - 1 else 0)
        y += h + 4 + 9 * ((y - H) / (512 - H)) ** 1.2
        r += 1
    return rows


def check_glints(rows, H):
    """G7(d): >=12 glints, each >=12 tall, thicker toward the viewer, denser toward the horizon,
    row-to-row mean length and mean gap differ by >=15%, no row a mirror of another, every glint >=20 long."""
    msgs = []
    byrow = {}
    for g in rows:
        byrow.setdefault(g[5], []).append(g)
    R = [byrow[k] for k in sorted(byrow)]
    if len(rows) < 12: msgs.append(f"only {len(rows)} glints")
    if min(g[3] for g in rows) < 12: msgs.append("glint under 12 tall")
    if min(g[2] for g in rows) < 30: msgs.append("glint under 30 long")
    hs = [r[0][3] for r in R]
    if any(b <= a for a, b in zip(hs, hs[1:])): msgs.append("not thicker toward viewer")
    ys = [r[0][1] for r in R]
    sp = [b - a for a, b in zip(ys, ys[1:])]
    if any(b < a for a, b in zip(sp, sp[1:])): msgs.append("not denser toward horizon")
    ml = [sum(g[2] for g in r) / len(r) for r in R]
    for a, b in zip(ml, ml[1:]):
        if abs(a - b) / max(a, b) < .15: msgs.append(f"row lengths too similar {a:.0f}/{b:.0f}")
    mg = [sum(g[6] for g in r if g[6]) / max(1, len(r) - 1) for r in R]
    for a, b in zip(mg, mg[1:]):
        if a and b and abs(a - b) / max(a, b) < .15: msgs.append(f"row gaps too similar {a:.0f}/{b:.0f}")
    sig = [tuple(round(g[2] / 4) for g in r) for r in R if len(r) > 1]
    if len(set(sig)) < len(sig) or any(s[::-1] in sig and s[::-1] != s for s in sig): msgs.append("rows mirror")
    return (not msgs), msgs, ml, mg


# ---- plugin icon
def icon(seed=2156):
    random.seed(seed)
    H = 300
    MX, MY, MR = 180, 148, 90
    cdx = gib_centroid(MR, K)
    litx = MX + cdx * math.cos(math.radians(TILT))
    lity = MY + cdx * math.sin(math.radians(TILT))
    ax = (MX + litx) / 2
    spec = H + (H - MY)
    p = "tsk-"
    D = [f'<clipPath id="{p}tile"><rect width="512" height="512" rx="112"/></clipPath>',
         f'<linearGradient id="{p}sky" x1="0" y1="0" x2="0" y2="{H}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#0E1530"/>'
         f'<stop offset=".7" stop-color="#1A2650"/><stop offset="1" stop-color="#2F3F6E"/></linearGradient>',
         f'<linearGradient id="{p}sea" x1="0" y1="{H}" x2="0" y2="512" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#2B3A68"/>'
         f'<stop offset=".2" stop-color="#16214A"/><stop offset="1" stop-color="#070B19"/></linearGradient>',
         f'<radialGradient id="{p}col" cx="{f(ax)}" cy="{f(spec)}" r="1" gradientUnits="userSpaceOnUse" '
         f'gradientTransform="translate({f(ax)} {f(spec)}) scale(80 175) translate({f(-ax)} {f(-spec)})">'
         f'<stop offset="0" stop-color="{IVORY}" stop-opacity=".14"/><stop offset="1" stop-color="{IVORY}" stop-opacity="0"/></radialGradient>',
         f'<linearGradient id="{p}hz" x1="20" y1="0" x2="492" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOONSTONE}" stop-opacity="0"/>'
         f'<stop offset="{f((ax-20)/472)}" stop-color="{IVORY}" stop-opacity=".5"/><stop offset="1" stop-color="{MOONSTONE}" stop-opacity="0"/></linearGradient>',
         f'<linearGradient id="{p}fr" x1="0" y1="0" x2="512" y2="512" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#E9CF8A"/>'
         f'<stop offset=".5" stop-color="#B08A45"/><stop offset="1" stop-color="#7A5A2A"/></linearGradient>']
    B = [f'<g clip-path="url(#{p}tile)">',
         f'<rect width="512" height="{H}" fill="url(#{p}sky)"/><rect y="{H}" width="512" height="{512-H}" fill="url(#{p}sea)"/>']
    # far shore: an original gothic skyline on the right horizon (cool, low contrast), mirrored faintly
    sk = (f'M296 {H}V288H310V266L316 252L322 266V282H338V272L350 259L362 272V288H372V232L381 192L390 232V284H400V266L412 250L424 266V290'
          f'H438V277L446 268L454 277V293H476V297H512V{H}Z')
    # distant snow ridge (Coerthas) behind the skyline: one more depth layer, barely above the sky tone
    ridge = f'M0 {H}V284Q40 270 84 278Q130 262 176 276Q236 258 292 272Q350 256 410 270Q462 260 512 274V{H}Z'
    B.append(f'<path d="{ridge}" fill="#263563"/>')
    B.append(f'<path d="{sk}" fill="#121A38"/>')
    # moonlight catching the moon-facing (left) edges of the spires: hero-only rim light
    rim = (f'M310 282V266L316 252M338 282V272L350 259M372 284V232L381 192M400 284V266L412 250M438 290V277L446 268')
    B.append(f'<path d="{rim}" fill="none" stroke="{MOONSTONE}" stroke-opacity=".3" stroke-width="2.5" stroke-linejoin="round"/>')
    # the cathedral's own rose window, echoing the moon (tiny, warm, far dimmer than the moon)
    B.append(f'<circle cx="381" cy="250" r="6" fill="{GILT_MID}" fill-opacity=".5"/>')
    B.append(f'<path d="{sk}" fill="#121A38" fill-opacity=".4" transform="translate(0 {2*H}) scale(1 -1)"/>')
    B.append(f'<path d="M378 352h6" stroke="{GILT_MID}" stroke-opacity=".18" stroke-width="3" stroke-linecap="round"/>')
    gD, gB = roundel(p, MX, MY, MR, "gilt", LAPIS, ("gibbous", K), TILT, lit_fill=IVORY, lit_alt=IVORY_COOL, rimw=8)
    D += gD
    B += gB
    B.append(f'<rect x="20" y="{H-1}" width="472" height="2.5" fill="url(#{p}hz)"/>')
    B.append(f'<rect x="{f(ax-80)}" y="{H}" width="160" height="{512-H}" fill="url(#{p}col)"/>')
    # flattened mirror of the lit glass under the horizon: flipped, y x0.3, cut into 3 thin rippled slices
    lp = lit_path(MX, MY, MR, K, "gibbous")
    sy, ty = .3, H + 10
    for i, (a, b, sx) in enumerate(((0, 6, -5), (11, 6, 4), (22, 7, -2))):
        D.append(f'<clipPath id="{p}s{i}"><rect x="0" y="{f(ty+a)}" width="512" height="{f(b)}"/></clipPath>')
        B.append(f'<g clip-path="url(#{p}s{i})"><path d="{lp}" fill="{dim(IVORY, .4)}" transform="translate({sx} {f(ty)}) '
                 f'scale(1 {-sy}) translate(0 {f(-(MY+MR))}) rotate({TILT} {MX} {MY})"/></g>')
    # glints: lens envelope around the axis; long, thin, broken; thicker and sparser toward the viewer
    rows = None
    for sd in range(seed, seed + 400):
        cand = make_glints(sd, H, ty, ax, spec)
        if check_glints(cand, H)[0] and len(cand) >= 14:
            rows, seed_used = cand, sd
            break
    for x, yy, w, h, op, _r, _g in rows:
        B.append(f'<path d="{shard(x, yy, w, h, 2)}" fill="{dim(IVORY, op)}"/>')
    # gothic spandrel tracery in the two upper corners (hero-only, <=30%): a cusped arc echoing the came
    for sx in (1, -1):
        tr = f'translate({0 if sx == 1 else 512} 0) scale({sx} 1)'
        B.append(f'<g transform="{tr}" fill="none" stroke="{GILT_MID}" stroke-opacity=".28" stroke-width="2.5">'
                 f'<path d="M22 118A96 96 0 0 1 118 22"/><path d="M40 92A30 30 0 0 1 64 66A30 30 0 0 1 92 40"/>'
                 f'<circle cx="52" cy="52" r="9"/></g>')
    B.append('</g>')
    B += [f'<rect x="5" y="5" width="502" height="502" rx="107" fill="none" stroke="#5A4320" stroke-width="10"/>',
          f'<rect x="9" y="9" width="494" height="494" rx="103" fill="none" stroke="url(#{p}fr)" stroke-width="8"/>',
          f'<rect x="14" y="14" width="484" height="484" rx="98" fill="none" stroke="#F6E6B4" stroke-opacity=".45" stroke-width="2"/>']
    info = dict(moon=(MX, MY, MR), lit_centroid=(round(litx, 1), round(lity, 1)), axis=round(ax, 1), H=H, spec=spec, glints=len(rows), rows=len(set(g[5] for g in rows)), seed=seed_used)
    return svg("Tsukimichi", D, B, 512), info, rows


if __name__ == "__main__":
    r, ax = ready()
    files = {"ready": r, "ready-on-another-job": ready_other_job(), "in-journal": in_journal(), "blocked": blocked(),
             "done-this-cycle": done(), "completed": completed(), "locked-out": locked(), "not-checked": not_checked()}
    for k_, v in files.items():
        (OUT / f"{k_}.svg").write_text(v, encoding="utf-8")
    s, info, rows = icon()
    (OUT / "plugin-icon.svg").write_text(s, encoding="utf-8")
    print("ready road axis", round(ax, 1), "| icon", info)
    ok, msgs, ml, mg = check_glints(rows, info["H"])
    print("G7d", ok, msgs, "row mean lengths", [round(v) for v in ml], "row mean gaps", [round(v) for v in mg])
    for x, y, w, h, op, rr, gg in rows:
        print(f"  row{rr} glint x{x:.0f}-{x+w:.0f} y{y:.0f} w{w:.0f} h{h:.1f} op{op:.2f}")
