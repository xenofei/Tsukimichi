# Writes the Moon Road category banners: original night-water illustrations, one motif per journal category.
#
# Each banner is a 376 x 120 viewBox (the detail hero's logical size) written at 2x pixel size (752 x 240). Layers,
# back to front: sky gradient, stars, a faint brass constellation, the moon and its glow, the category's motif on
# the horizon, night water, the motif's reflection, the moon road (the glitter path the moon lays on the water),
# ripples, foreground accents and a soft vignette. The motif's mass sits right of centre and above the horizon, so the
# title the hero prints bottom-left over its scrim always has dark, quiet water behind it.
#
# All art is original; nothing is traced from Square Enix material. The runtime prefers the player's own game art
# (journal banner, duty banner, zone loading image) and falls back to these.
#
# Usage: python docs/design/moon-road/banners/gen_banners.py   (then rasterize.py)
import math
import os
import random

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "src")
W, H = 376, 120
SCALE = 2

GILT, GILT_HI, MOON, MOON_HI, MOON_DEEP = "#A88B52", "#D9BE82", "#F2D27A", "#FFF0BE", "#D6B25A"
SILVER, MIST, TIDE, TIDE_DEEP, NIGHT, ABYSS = "#DDE3F0", "#A9B2CC", "#6F8FD0", "#24345C", "#0F1424", "#080B16"


def f(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


class Banner:
    """One banner under construction: collects defs and layers, then serialises."""

    def __init__(self, slug, title, desc, seed, sky, water, horizon=82, moon=(292, 30, 10.5), glow=MOON, phase=None):
        self.slug, self.title, self.desc = slug, title, desc
        self.rng = random.Random(seed)
        self.sky, self.water, self.hz = sky, water, horizon
        self.mx, self.my, self.mr = moon
        self.glow, self.phase = glow, phase
        self.defs, self.back, self.motif = [], [], []
        self.no_reflect = []   # motif pieces drawn on the horizon but not mirrored (clouds over water, etc.)
        self.in_water = []     # drawn on the water after the reflection, under the moon road (a gate's own reflection)
        self.extra_front = []  # foreground accents over everything but the vignette (snow, fireflies, lanterns)
        self.road_width = 1.0
        self.fade_from, self.fade_to = 90.0, 190.0   # the motif fades in across this band, left to right
        self.stars_n = 80

    # -- helpers -------------------------------------------------------------------------------------------------
    def grad(self, gid, stops, x2=0, y2=1, x1=0, y1=0):
        s = ""
        for st in stops:
            o, c = st[0], st[1]
            a = st[2] if len(st) > 2 else None
            s += f'<stop offset="{f(o)}" stop-color="{c}"' + (f' stop-opacity="{f(a)}"' if a is not None else "") + "/>"
        self.defs.append(f'<linearGradient id="{gid}" x1="{f(x1)}" y1="{f(y1)}" x2="{f(x2)}" y2="{f(y2)}">{s}</linearGradient>')
        return f"url(#{gid})"

    def radial(self, gid, cx, cy, r, stops):
        s = "".join(f'<stop offset="{f(o)}" stop-color="{c}" stop-opacity="{f(a)}"/>' for o, c, a in stops)
        self.defs.append(f'<radialGradient id="{gid}" cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" gradientUnits="userSpaceOnUse">{s}</radialGradient>')
        return f"url(#{gid})"

    def glow_ellipse(self, gid, cx, cy, rx, ry, color, opacity):
        """A soft elliptical glow that fades to nothing at its rim (objectBoundingBox radial)."""
        self.defs.append(f'<radialGradient id="{gid}"><stop offset="0" stop-color="{color}" stop-opacity="{f(opacity)}"/>'
                         f'<stop offset=".45" stop-color="{color}" stop-opacity="{f(opacity * .45)}"/><stop offset="1" stop-color="{color}" stop-opacity="0"/></radialGradient>')
        return f'<ellipse cx="{f(cx)}" cy="{f(cy)}" rx="{f(rx)}" ry="{f(ry)}" fill="url(#{gid})"/>'

    def blur(self, fid, sd):
        self.defs.append(f'<filter id="{fid}" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="{f(sd)}"/></filter>')
        return f"url(#{fid})"

    # -- standard layers -----------------------------------------------------------------------------------------
    def build_sky(self):
        t, m, h = self.sky
        fill = self.grad("sky", [(0, t), (0.55, m), (1, h)])
        self.back.append(f'<rect width="{W}" height="{self.hz + 1}" fill="{fill}"/>')
        # A low haze band just above the horizon, the sky's brightest strip.
        haze = self.grad("haze", [(0, h, 0), (1, h, 0.55)])
        self.back.append(f'<rect y="{f(self.hz - 16)}" width="{W}" height="16" fill="{haze}"/>')

    def build_stars(self):
        r = self.rng
        out = []
        for _ in range(self.stars_n):
            x, y = r.uniform(0, W), r.uniform(0, self.hz - 10) ** 1.0
            y = (y / (self.hz - 10)) ** 1.6 * (self.hz - 10)   # denser near the top
            if math.hypot(x - self.mx, y - self.my) < self.mr * 3.2:
                continue
            mag = r.random()
            rad = 0.22 + mag ** 3 * 0.55
            op = 0.35 + mag * 0.55
            out.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(rad)}" fill="{SILVER}" opacity="{f(op)}"/>')
        for _ in range(4):   # a few four-point sparkles
            x, y = r.uniform(20, W - 20), r.uniform(6, self.hz * 0.55)
            if math.hypot(x - self.mx, y - self.my) < self.mr * 4:
                continue
            s = r.uniform(1.6, 2.6)
            out.append(sparkle(x, y, s, MOON_HI, 0.7))
        self.back.append(f'<g>{"".join(out)}</g>')

    def constellation(self, pts, opacity=0.5):
        """The brass touch: a faint Gilt constellation, hairline links and GiltHigh nodes."""
        d = "M" + " L".join(f"{f(x)} {f(y)}" for x, y in pts)
        nodes = "".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="0.75" fill="{GILT_HI}"/>' for x, y in pts)
        self.back.append(f'<g opacity="{f(opacity)}"><path d="{d}" fill="none" stroke="{GILT}" stroke-width=".35"/>{nodes}</g>')

    def build_moon(self):
        mx, my, mr = self.mx, self.my, self.mr
        g = self.radial("glow", mx, my, mr * 7, [(0, self.glow, 0.34), (0.25, self.glow, 0.12), (1, self.glow, 0)])
        self.back.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr * 7)}" fill="{g}"/>')
        disc = self.radial("disc", mx - mr * 0.3, my - mr * 0.35, mr * 1.4, [(0, MOON_HI, 1), (0.55, MOON, 1), (1, MOON_DEEP, 1)])
        mask = ""
        if self.phase is not None:   # a crescent: the disc minus an offset disc
            ox = self.phase
            self.defs.append(f'<mask id="phase"><rect width="{W}" height="{H}" fill="#fff"/><circle cx="{f(mx + ox)}" cy="{f(my - mr * 0.18)}" r="{f(mr * 0.94)}" fill="#000"/></mask>')
            mask = ' mask="url(#phase)"'
        maria = "".join(
            f'<ellipse cx="{f(mx + dx * mr)}" cy="{f(my + dy * mr)}" rx="{f(rx * mr)}" ry="{f(ry * mr)}" fill="{MOON_DEEP}" opacity=".35"/>'
            for dx, dy, rx, ry in [(-0.28, -0.1, 0.26, 0.2), (0.22, 0.3, 0.2, 0.15), (0.05, -0.42, 0.14, 0.1), (0.38, -0.12, 0.1, 0.08)])
        self.back.append(f'<g{mask}><circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr)}" fill="{disc}"/>{maria if self.phase is None else ""}</g>')
        self.back.append(sparkle(mx - mr * 2.1, my + mr * 1.5, 2.4, GILT_HI, 0.8))

    def build_water(self):
        top, bottom = self.water
        fill = self.grad("water", [(0, top), (1, bottom)])
        return f'<rect y="{f(self.hz)}" width="{W}" height="{f(H - self.hz)}" fill="{fill}"/>'

    def build_reflection(self):
        if not self.motif:
            return ""
        self.defs.append('<linearGradient id="reflfade" x1="0" y1="0" x2="0" y2="1">'
                         '<stop offset="0" stop-color="#fff" stop-opacity=".55"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>')
        self.defs.append(f'<mask id="reflmask"><rect y="{f(self.hz)}" width="{W}" height="{f(H - self.hz)}" fill="url(#reflfade)"/></mask>')
        return f'<g mask="url(#reflmask)" opacity=".5"><use href="#motif" transform="matrix(1 0 0 -1 0 {f(2 * self.hz)})"/></g>'

    def build_road(self):
        r = self.rng
        out = []
        y = self.hz + 0.8
        while y < H + 1:
            depth = (y - self.hz) / (H - self.hz)
            half = (1.6 + depth * 18) * self.road_width
            for _ in range(int(2 + depth * 5)):
                # Glints cluster on the column's centre line (a triangular spread) and thin out toward its edges.
                off = (r.random() - r.random()) * half
                centre = 1 - abs(off) / (half + 0.01)
                seg = r.uniform(0.8, 3.2) * (0.5 + depth * 1.2) * (0.5 + centre)
                op = (0.95 - depth * 0.35) * (0.25 + 0.75 * centre) * r.uniform(0.6, 1)
                col = MOON_HI if centre > 0.6 else MOON
                out.append(f'<path d="M{f(self.mx + off - seg / 2)} {f(y + r.uniform(-.3, .3))} h{f(seg)}" stroke="{col}" '
                           f'stroke-width="{f(0.35 + depth * 0.45)}" stroke-linecap="round" opacity="{f(op)}"/>')
            y += 0.9 + depth * 1.9
        # The column's soft underglow.
        glow = self.glow_ellipse("roadglow", self.mx, self.hz + 10, 40 * self.road_width, 30, MOON, 0.16)
        return f'{glow}<g>{"".join(out)}</g>'

    def build_ripples(self):
        r = self.rng
        out = []
        for _ in range(46):
            y = r.uniform(self.hz + 1.5, H)
            depth = (y - self.hz) / (H - self.hz)
            x = r.uniform(-10, W)
            if abs(x - self.mx) < 18 + depth * 20:
                continue
            ln = r.uniform(6, 26) * (0.5 + depth)
            out.append(f'<path d="M{f(x)} {f(y)} h{f(ln)}" stroke="{TIDE}" stroke-width="{f(0.35 + depth * 0.35)}" opacity="{f(0.1 + (1 - depth) * 0.16)}" stroke-linecap="round"/>')
        # The horizon's hairline glint, brass at the edges fading into moonlight.
        hl = self.grad("hline", [(0, GILT, 0), (0.5, GILT_HI, 0.55), (1, GILT, 0)], x2=1, y2=0)
        return f'<g>{"".join(out)}</g><rect x="{f(self.mx - 150)}" y="{f(self.hz - 0.3)}" width="300" height=".6" fill="{hl}"/>'

    def build_vignette(self):
        v = self.radial("vig", W * 0.62, H * 0.38, W * 0.75, [(0, ABYSS, 0), (0.7, ABYSS, 0.12), (1, ABYSS, 0.55)])
        return f'<rect width="{W}" height="{H}" fill="{v}"/>'

    def svg(self):
        self.build_sky()
        self.build_stars()
        self.build_moon()
        water, reflection, road, ripples = self.build_water(), self.build_reflection(), self.build_road(), self.build_ripples()
        vignette = self.build_vignette()
        self.defs.append(f'<linearGradient id="leftfade" gradientUnits="userSpaceOnUse" x1="{f(self.fade_from)}" y1="0" x2="{f(self.fade_to)}" y2="0">'
                         '<stop offset="0" stop-color="#fff" stop-opacity="0"/><stop offset="1" stop-color="#fff" stop-opacity="1"/></linearGradient>'
                         f'<mask id="motifmask" maskUnits="userSpaceOnUse" x="-20" y="-200" width="{W + 40}" height="{H + 400}">'
                         f'<rect x="-20" y="-200" width="{W + 40}" height="{H + 400}" fill="url(#leftfade)"/></mask>')
        self.defs.append(f'<g id="motif" mask="url(#motifmask)">{"".join(self.motif)}</g>')
        body = "\n".join([
            "".join(self.back),
            '<use href="#motif"/>',
            "".join(self.no_reflect),
            water,
            reflection,
            "".join(self.in_water),
            road,
            ripples,
            "".join(self.extra_front),
            vignette,
        ])
        return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{W * SCALE}" height="{H * SCALE}" viewBox="0 0 {W} {H}">\n'
                f'<title>{self.title}</title>\n<desc>{self.desc} Original Tsukimichi art (Moon Road category banner).</desc>\n'
                f'<defs>{"".join(self.defs)}</defs>\n{body}\n</svg>\n')

    def fg(self, s):
        self.extra_front.append(s)


def sparkle(x, y, s, color, op):
    k = s * 0.22
    return (f'<path d="M{f(x)} {f(y - s)} L{f(x + k)} {f(y - k)} L{f(x + s)} {f(y)} L{f(x + k)} {f(y + k)} L{f(x)} {f(y + s)} '
            f'L{f(x - k)} {f(y + k)} L{f(x - s)} {f(y)} L{f(x - k)} {f(y - k)} Z" fill="{color}" opacity="{f(op)}"/>')


def ridge(rng, x0, x1, base, lo, hi, step=6.0, jag=0.5):
    """A mountain range polygon from x0 to x1 standing on `base`, peaks between lo and hi (y values, smaller = taller)."""
    pts = [(x0 - 30, base)]   # a long, low foot so the range never starts with a wall
    x = x0
    y = rng.uniform(lo, hi)
    while x < x1:
        pts.append((x, y))
        x += step * rng.uniform(0.6, 1.4)
        y = min(base - 1, max(lo, y + rng.uniform(-1, 1) * (hi - lo) * jag))
    pts.append((x1, y))
    pts.append((x1, base))
    return "M" + " L".join(f"{f(a)} {f(b)}" for a, b in pts) + " Z"


def peak_range(peaks, base, x0, x1):
    """Mountains from explicit (x, y) summits, joined by valleys halfway down."""
    pts = [(x0 - 30, base)]
    prev = None
    for x, y in peaks:
        if prev is not None:
            vx = (prev[0] + x) / 2
            vy = max(prev[1], y) + (base - max(prev[1], y)) * 0.45
            pts.append((vx, vy))
        pts.append((x, y))
        prev = (x, y)
    pts.append((x1, base))
    return "M" + " L".join(f"{f(a)} {f(b)}" for a, b in pts) + " Z"


def pine(x, base, h, w):
    tiers = 4
    d = []
    for i in range(tiers):
        top = base - h + i * h * 0.22
        bot = base - h * 0.18 + (i - tiers + 1) * h * 0.14
        ww = w * (0.45 + i * 0.22)
        d.append(f"M{f(x)} {f(top)} L{f(x + ww)} {f(bot)} L{f(x - ww)} {f(bot)} Z")
    d.append(f"M{f(x - w * 0.08)} {f(base)} h{f(w * 0.16)} v{f(-h * 0.25)} h{f(-w * 0.16)} Z")
    return " ".join(d)


def palm(x, base, h, lean, rng):
    tx, ty = x + lean, base - h
    trunk = f'<path d="M{f(x)} {f(base)} Q{f(x + lean * 0.2)} {f(base - h * 0.5)} {f(tx)} {f(ty)}" stroke="currentColor" stroke-width="{f(h * 0.05)}" fill="none" stroke-linecap="round"/>'
    fronds = []
    for a in (-160, -130, -100, -70, -40, -10, 15):
        ang = math.radians(a + rng.uniform(-8, 8))
        ln = h * rng.uniform(0.38, 0.5)
        ex, ey = tx + math.cos(ang) * ln, ty + math.sin(ang) * ln * 0.6 + ln * 0.35
        cx, cy = tx + math.cos(ang) * ln * 0.5, ty + math.sin(ang) * ln * 0.5 - ln * 0.12
        fronds.append(f'<path d="M{f(tx)} {f(ty)} Q{f(cx)} {f(cy)} {f(ex)} {f(ey)}" stroke="currentColor" stroke-width="{f(h * 0.045)}" fill="none" stroke-linecap="round"/>')
    return trunk + "".join(fronds)


def poly(pts):
    return "M" + " L".join(f"{f(x)} {f(y)}" for x, y in pts) + " Z"


# ------------------------------------------------------------------------------------------------------------------
# The banners
# ------------------------------------------------------------------------------------------------------------------
banners = []


def banner(fn):
    banners.append(fn)
    return fn


@banner
def msq_arr():
    b = Banner("msq-arr", "Main Scenario: A Realm Reborn", "A coast of old stone under a blue night; the lesser moon broken into falling embers, a crystal spire on the far shore.",
               11, sky=("#070D26", "#16296A", "#4C71B4"), water=("#0E1A44", "#060A18"), moon=(300, 26, 10))
    r = b.rng
    # The broken lesser moon, far off: a dim ember disc and its trail of fragments.
    b.back.append('<g opacity=".85"><circle cx="118" cy="22" r="6.5" fill="#6A2A2E"/><circle cx="118" cy="22" r="6.5" fill="none" stroke="#C0563F" stroke-width=".5" opacity=".7"/>'
                  '<path d="M114 18 L119 23 L116 27 M121 17 L119 23 L124 25" stroke="#E08A55" stroke-width=".45" fill="none" opacity=".8"/></g>')
    for i in range(9):
        x, y = 126 + i * 5.5 + r.uniform(-2, 2), 27 + i * 3.4 + r.uniform(-1.5, 1.5)
        b.back.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(1.1 - i * 0.09)}" fill="#E08A55" opacity="{f(0.75 - i * 0.07)}"/>')
    b.constellation([(40, 14), (58, 22), (72, 16), (90, 30)], 0.45)
    far = ridge(r, 120, 376, b.hz, 62, 72, step=7, jag=0.4)
    b.motif.append(f'<path d="{far}" fill="#1C2F63" opacity=".9"/>')
    # The crystal spire on the far shore.
    b.motif.append(f'<path d="{poly([(236, b.hz - 10), (241, b.hz - 44), (246, b.hz - 10)])}" fill="#7FA6E6" opacity=".55"/>'
                   f'<path d="M241 {f(b.hz - 44)} L241 {f(b.hz - 10)}" stroke="#CFE0FF" stroke-width=".5" opacity=".6"/>')
    near = peak_range([(160, 70), (190, 64), (212, 72), (270, 68), (330, 60), (360, 66)], b.hz, 140, 376)
    b.motif.append(f'<path d="{near}" fill="#0E1838"/>')
    # A lighthouse on the headland, its lamp a small warm point.
    b.motif.append('<path d="M343 60 L346 42 L350 42 L353 60 Z" fill="#0E1838"/><path d="M344.5 42 h7 l-1 -3 h-5 Z" fill="#0E1838"/>'
                   '<circle cx="348" cy="40" r="1.5" fill="#FFE2A0"/>')
    b.back.append(f'<circle cx="348" cy="40" r="9" fill="{b.radial("lamp", 348, 40, 9, [(0, "#FFE2A0", .45), (1, "#FFE2A0", 0)])}"/>')
    return b


@banner
def msq_hw():
    b = Banner("msq-hw", "Main Scenario: Heavensward", "Gothic spires of a holy city above snow, a great winged shape crossing the moon, falling snow.",
               12, sky=("#0A1226", "#223A62", "#8BAFD4"), water=("#1A2B4A", "#070C1A"), moon=(262, 28, 12), glow="#E6F0FF")
    r = b.rng
    b.stars_n = 60
    b.constellation([(36, 18), (52, 10), (70, 20), (84, 12)], 0.45)
    # A far winged silhouette crossing near the moon.
    b.back.append('<path d="M292 24 q6 -6 14 -4 q-5 1 -7 4 q5 -1 9 2 q-6 0 -9 2 l-3 1 q-2 3 -5 3 q2 -2 2 -4 q-4 1 -8 0 q5 -2 7 -4 Z" fill="#101A30" opacity=".85"/>')
    far = peak_range([(120, 58), (150, 48), (180, 56), (220, 44), (250, 54), (300, 46), (340, 52), (376, 50)], b.hz, 100, 376)
    b.motif.append(f'<path d="{far}" fill="#3A5480" opacity=".8"/>')
    # Snow caps on the far peaks.
    b.motif.append('<g fill="#DCE8F8" opacity=".55"><path d="M150 48 l5 5 l-3 -1 l-3 3 l-3 -2 l-2 1 Z"/><path d="M220 44 l6 6 l-3 -1 l-3 3 l-3 -3 l-3 1 Z"/><path d="M300 46 l5 5 l-3 0 l-2 2 l-3 -3 l-2 1 Z"/></g>')
    # The cathedral: nave, rose window, three spires.
    city = []
    base = b.hz
    for x, h, w in [(206, 44, 5), (222, 58, 6.5), (238, 46, 5), (256, 34, 4), (190, 30, 4), (272, 26, 4)]:
        city.append(poly([(x - w, base), (x - w, base - h * 0.62), (x, base - h), (x + w, base - h * 0.62), (x + w, base)]))
    city.append(f"M182 {base} L182 {base - 16} L290 {base - 16} L290 {base} Z")
    b.motif.append(f'<path d="{" ".join(city)}" fill="#0C1426"/>')
    b.motif.append('<circle cx="222" cy="44" r="2.2" fill="none" stroke="#FFE7AE" stroke-width=".6" opacity=".8"/>'
                   '<g fill="#FFE7AE" opacity=".7"><rect x="205" y="60" width="1" height="3"/><rect x="237" y="62" width="1" height="3"/><rect x="221" y="56" width="1.2" height="4"/></g>')
    # Falling snow over everything.
    snow = "".join(f'<circle cx="{f(r.uniform(0, 376))}" cy="{f(r.uniform(0, 118))}" r="{f(r.uniform(.25, .7))}" fill="#F2F6FF" opacity="{f(r.uniform(.25, .7))}"/>' for _ in range(70))
    b.fg(f"<g>{snow}</g>")
    return b


@banner
def msq_sb():
    b = Banner("msq-sb", "Main Scenario: Stormblood", "A crimson night over a lakeside fortress and a far eastern tower across the water; a war banner lifts in the wind.",
               13, sky=("#12081A", "#3C1428", "#A2403C"), water=("#2A1020", "#0A0610"), moon=(300, 30, 11), glow="#FFD2B0")
    r = b.rng
    b.constellation([(30, 16), (46, 26), (64, 18), (80, 28), (96, 20)], 0.45)
    far = ridge(r, 110, 376, b.hz, 56, 68, step=9, jag=0.5)
    b.motif.append(f'<path d="{far}" fill="#4A1C2C" opacity=".85"/>')
    # The fortress on its cliff: walls, towers, a gate tower.
    f_ = []
    for x0, w, h in [(176, 10, 30), (188, 26, 22), (216, 8, 36), (226, 22, 20), (250, 9, 28)]:
        f_.append(f"M{x0} {b.hz} V{b.hz - h} h{w} V{b.hz} Z")
        for k in range(int(w // 3)):   # crenellations
            f_.append(f"M{x0 + k * 3} {b.hz - h} v-2 h1.6 v2 Z")
    b.motif.append(f'<path d="{" ".join(f_)}" fill="#1A0A14"/>')
    b.motif.append('<g fill="#FFB27A" opacity=".75"><rect x="220" y="52" width="1.2" height="3"/><rect x="195" y="64" width="1" height="2.6"/><rect x="254" y="60" width="1" height="2.6"/></g>')
    # The pagoda far across the water.
    pg = []
    for i, (w, y) in enumerate([(14, 70), (11, 64), (8, 58), (5, 53)]):
        pg.append(f"M{330 - w} {y} Q330 {y - 3.5} {330 + w} {y} L{330 + w * 0.7} {y + 1.4} L{330 - w * 0.7} {y + 1.4} Z")
        pg.append(f"M{330 - w * 0.55} {y + 1.4} h{w * 1.1} v{4.5 if i < 3 else 3} h{-w * 1.1} Z")
    pg.append("M329.4 53 l.6 -7 l.6 7 Z")
    b.motif.append(f'<path d="{" ".join(pg)}" fill="#2A0E1C"/>')
    b.motif.append(f'<rect x="309" y="{b.hz - 8}" width="44" height="8" fill="#2A0E1C"/>')
    # A war banner lifting from the gate tower.
    b.motif.append('<path d="M220 44 V28" stroke="#1A0A14" stroke-width=".8"/><path d="M220.4 28 q7 1 12 -1 q-3 4 1 8 q-6 1 -13 -1 Z" fill="#8E2A2A" opacity=".95"/>')
    return b


@banner
def msq_shb():
    b = Banner("msq-shb", "Main Scenario: Shadowbringers", "Night returning to a sky drowned in light: violet dark breaking through a white glare, a crystal tower rising over a lake.",
               14, sky=("#0C0822", "#2E1F5C", "#8E78C8"), water=("#1E1640", "#07051A"), moon=(248, 30, 11), glow="#E6DCFF")
    r = b.rng
    # The retreating flood of light: a pale glare in the upper right, torn into shards as night returns.
    light = b.radial("flood", 376, -10, 150, [(0, "#FFFFFF", 0.55), (0.4, "#E8E0FF", 0.2), (1, "#E8E0FF", 0)])
    b.back.append(f'<rect width="376" height="{b.hz}" fill="{light}"/>')
    for _ in range(8):
        x, y, s = r.uniform(300, 370), r.uniform(4, 40), r.uniform(3, 7)
        b.back.append(f'<path d="{poly([(x, y - s), (x + s * 0.35, y), (x, y + s * 0.6), (x - s * 0.3, y)])}" fill="#F4F0FF" opacity="{f(r.uniform(.2, .45))}"/>')
    b.constellation([(40, 20), (56, 12), (70, 22), (86, 16), (98, 26)], 0.45)
    far = ridge(r, 100, 376, b.hz, 64, 72, step=8, jag=0.35)
    b.motif.append(f'<path d="{far}" fill="#3A2C70" opacity=".85"/>')
    # The crystal tower: a tall faceted spire with side shards.
    tower = poly([(196, b.hz), (200, 30), (204, 14), (208, 30), (212, b.hz)])
    b.motif.append(f'<path d="{tower}" fill="#6D5DB8"/>'
                   '<path d="M204 14 L204 82" stroke="#E6DCFF" stroke-width=".5" opacity=".7"/>'
                   f'<path d="{poly([(190, b.hz), (193, 50), (196, b.hz)])} {poly([(212, b.hz), (216, 46), (220, b.hz)])} {poly([(184, b.hz), (186, 64), (189, b.hz)])}" fill="#51449A"/>')
    b.back.append(b.glow_ellipse("tglow", 204, 34, 18, 36, "#C9B8FF", .4))
    near = peak_range([(150, 76), (176, 72), (240, 74), (300, 70), (350, 74)], b.hz, 130, 376)
    b.motif.append(f'<path d="{near}" fill="#150F30"/>')
    return b


@banner
def msq_ew():
    b = Banner("msq-ew", "Main Scenario: Endwalker", "A blue world's limb rising beyond the moon, a colonnade of learning on the shore, a pale gold dawn at the edge of night.",
               15, sky=("#070C22", "#1C2E56", "#C8B27E"), water=("#16223C", "#060A16"), moon=(318, 22, 8.5))
    r = b.rng
    # The world seen from the moon: a great limb of blue with a thin bright rim.
    planet = b.radial("planet", 196, 150, 118, [(0, "#1F4F8C", 1), (0.8, "#2E6BB0", 1), (0.97, "#7FB6E8", 1), (1, "#CFE6FF", 1)])
    b.back.append(f'<circle cx="196" cy="150" r="118" fill="{planet}" opacity=".78"/>')
    b.back.append(f'<circle cx="196" cy="150" r="118" fill="none" stroke="#DDF0FF" stroke-width=".6" opacity=".7"/>')
    b.back.append('<g fill="#DDEBFA" opacity=".18"><ellipse cx="170" cy="46" rx="30" ry="4"/><ellipse cx="236" cy="56" rx="22" ry="3"/><ellipse cx="150" cy="62" rx="18" ry="2.4"/></g>')
    b.constellation([(30, 14), (44, 24), (60, 16), (74, 28)], 0.45)
    # The colonnade: a pediment on columns, and a dome beside it.
    cols = [f"M{x} {b.hz} V{b.hz - 16} h2.2 V{b.hz} Z" for x in range(262, 312, 7)]
    cols.append(f"M258 {b.hz - 16} h56 v-3 h-56 Z M256 {b.hz - 19} L286 {b.hz - 29} L316 {b.hz - 19} Z")
    cols.append(f"M322 {b.hz} V{b.hz - 12} A12 12 0 0 1 346 {b.hz - 12} V{b.hz} Z M333.4 {b.hz - 24} v-5 h1.2 v5 Z")
    b.motif.append(f'<path d="{" ".join(cols)}" fill="#0D1530"/>')
    b.motif.append(f'<rect x="240" y="{b.hz - 3}" width="136" height="3" fill="#0D1530"/>')
    far = ridge(r, 120, 250, b.hz, 72, 78, step=10, jag=0.3)
    b.motif.append(f'<path d="{far}" fill="#22345C" opacity=".8"/>')
    return b


@banner
def msq_dt():
    b = Banner("msq-dt", "Main Scenario: Dawntrail", "A stepped golden pyramid above a green lowland, its stair a road of light, the sky warm at the horizon.",
               16, sky=("#06121A", "#0F3438", "#D2A150"), water=("#0E2A2C", "#040C10"), moon=(214, 24, 10))
    r = b.rng
    b.constellation([(34, 18), (50, 12), (64, 24), (82, 16), (96, 22)], 0.45)
    far = peak_range([(120, 60), (160, 52), (200, 62), (330, 54), (376, 60)], b.hz, 100, 376)
    b.motif.append(f'<path d="{far}" fill="#1D4A44" opacity=".85"/>')
    # The pyramid: five steps, a crowning shrine, the stair lit gold.
    steps = []
    cx, base = 282, b.hz
    for i in range(5):
        w = 40 - i * 7
        steps.append(f"M{cx - w} {base - i * 7} h{2 * w} v-7 h{-2 * w} Z")
    steps.append(f"M{cx - 5} {base - 35} h10 v-6 h-10 Z M{cx - 7} {base - 41} h14 l-2 -2 h-10 Z")
    b.motif.append(f'<path d="{" ".join(steps)}" fill="#0E2622"/>')
    b.motif.append(f'<path d="M{cx - 3} {base} L{cx - 1.5} {base - 35} h3 L{cx + 3} {base} Z" fill="{MOON}" opacity=".55"/>'
                   f'<rect x="{cx - 1.5}" y="{base - 40}" width="3" height="4" fill="#FFE2A0" opacity=".85"/>')
    b.back.append(b.glow_ellipse("shrine", cx, base - 38, 22, 14, "#FFD27A", .45))
    # Palms either side.
    b.motif.append(f'<g color="#07160F">{palm(234, b.hz, 22, -4, r)}{palm(246, b.hz, 16, 3, r)}{palm(330, b.hz, 20, 4, r)}{palm(342, b.hz, 26, -3, r)}</g>')
    return b


# -- sidequest regions ---------------------------------------------------------------------------------------------
@banner
def side_la_noscea():
    b = Banner("side-la-noscea", "Sidequests: La Noscea", "Sea cliffs and a sailing ship at anchor under a teal night; the harbour lamps low on the water.",
               21, sky=("#06121F", "#123A52", "#3F8098"), water=("#0D2C3C", "#040C14"), moon=(250, 26, 10.5))
    r = b.rng
    b.constellation([(36, 22), (50, 14), (64, 24), (80, 18)], 0.45)
    cliff = poly([(300, b.hz), (304, 56), (316, 50), (330, 52), (344, 44), (360, 48), (376, 46), (376, b.hz)])
    b.motif.append(f'<path d="{cliff}" fill="#0A1E2A"/>')
    b.motif.append(f'<path d="{ridge(r, 120, 300, b.hz, 70, 76, step=10, jag=.3)}" fill="#1B4A5E" opacity=".8"/>')
    # A two-masted ship with furled sails.
    ship = ("M190 78 L234 78 L228 84 L196 84 Z M204 78 V50 M220 78 V56 M196 58 L212 60 M198 66 L210 67 M214 62 L226 63 M215 70 L225 70 "
            "M204 50 L234 72")
    b.motif.append(f'<path d="M190 78 L234 78 L228 84 L196 84 Z" fill="#081620"/>'
                   f'<path d="{ship[ship.index("M204"):]}" stroke="#081620" stroke-width="1.1" fill="none"/>'
                   '<path d="M205 54 q6 6 0 12 Z M221 60 q5 5 0 10 Z" fill="#12303E"/>')
    b.motif.append('<circle cx="232" cy="74" r="1" fill="#FFD9A0"/>')
    for x in (310, 318, 334, 350, 362):
        b.motif.append(f'<rect x="{x}" y="{f(r.uniform(58, 70))}" width="1.2" height="2" fill="#FFD9A0" opacity=".8"/>')
    # Gulls.
    b.back.append('<path d="M150 30 q2 -2 4 0 q2 -2 4 0 M166 22 q1.6 -1.6 3.2 0 q1.6 -1.6 3.2 0" stroke="#0A1E2A" stroke-width=".6" fill="none"/>')
    return b


@banner
def side_black_shroud():
    b = Banner("side-black-shroud", "Sidequests: The Black Shroud", "A deep forest of giant trees at the water's edge, fireflies drifting over a still pool.",
               22, sky=("#06110D", "#113224", "#3F6E4E"), water=("#0C2418", "#030A07"), moon=(236, 24, 10), glow="#F4FFD8")
    r = b.rng
    b.constellation([(30, 14), (44, 22), (58, 12), (74, 20)], 0.45)
    back_trees = "".join(pine(x, b.hz, r.uniform(20, 30), r.uniform(5, 7)) for x in range(120, 380, 9))
    b.motif.append(f'<path d="{back_trees}" fill="#16392A" opacity=".85"/>')
    # Two ancient trees: broad trunks, heavy canopies.
    for cx, top, w in [(282, 18, 34), (342, 26, 30)]:
        b.motif.append(f'<path d="M{cx - 5} {b.hz} Q{cx - 3} {b.hz - 30} {cx - 8} {top + 22} L{cx + 8} {top + 22} Q{cx + 3} {b.hz - 30} {cx + 6} {b.hz} Z" fill="#081811"/>')
        blobs = "".join(f'<ellipse cx="{f(cx + r.uniform(-w, w) * .8)}" cy="{f(top + r.uniform(0, 22))}" rx="{f(r.uniform(9, 15))}" ry="{f(r.uniform(6, 10))}"/>' for _ in range(7))
        b.motif.append(f'<g fill="#0A1E15">{blobs}</g>')
    near = "".join(pine(x, b.hz, r.uniform(12, 18), r.uniform(4, 5)) for x in range(170, 250, 11))
    b.motif.append(f'<path d="{near}" fill="#0B1F16"/>')
    flies = "".join(f'<circle cx="{f(r.uniform(150, 370))}" cy="{f(r.uniform(56, 100))}" r="{f(r.uniform(.4, .8))}" fill="#E8F59A" opacity="{f(r.uniform(.45, .9))}"/>' for _ in range(22))
    b.fg(f'<g>{flies}</g>')
    return b


@banner
def side_thanalan():
    b = Banner("side-thanalan", "Sidequests: Thanalan", "Warm desert night: mesas and dunes, a domed city of merchants glowing gold on the far shore.",
               23, sky=("#120C16", "#3A2432", "#B8784C"), water=("#2A1A1A", "#0A0608"), moon=(318, 26, 10), glow="#FFE0B8")
    r = b.rng
    b.constellation([(36, 16), (54, 24), (68, 14), (86, 22), (100, 12)], 0.45)
    mesa = poly([(118, b.hz), (126, 62), (160, 60), (166, b.hz)]) + " " + poly([(170, b.hz), (176, 66), (196, 65), (204, b.hz)])
    b.motif.append(f'<path d="{mesa}" fill="#4A2A2A" opacity=".85"/>')
    # The domed city: a great dome, minarets, walls.
    city = [f"M216 {b.hz} V{b.hz - 12} H300 V{b.hz} Z",
            f"M236 {b.hz - 12} A20 20 0 0 1 276 {b.hz - 12} Z",
            f"M254.6 {b.hz - 32} v-6 h1.2 v6 Z",
            f"M224 {b.hz - 12} V{b.hz - 30} l2 -3 l2 3 V{b.hz - 12} Z",
            f"M286 {b.hz - 12} V{b.hz - 26} l2 -3 l2 3 V{b.hz - 12} Z",
            f"M300 {b.hz} V{b.hz - 8} A7 7 0 0 1 314 {b.hz - 8} V{b.hz} Z"]
    b.motif.append(f'<path d="{" ".join(city)}" fill="#1C1016"/>')
    b.motif.append('<g fill="#FFC870" opacity=".8">' + "".join(f'<rect x="{x}" y="{f(b.hz - 8)}" width="1.2" height="2.2"/>' for x in range(222, 298, 7)) + "</g>")
    b.back.append(b.glow_ellipse("cityglow", 256, b.hz - 10, 56, 22, "#FFB060", .32))
    dunes = f"M300 {b.hz} Q330 {b.hz - 10} 350 {b.hz - 4} Q364 {b.hz - 12} 376 {b.hz - 8} V{b.hz} Z"
    b.motif.append(f'<path d="{dunes}" fill="#2C1A1C"/>')
    return b


@banner
def side_coerthas():
    b = Banner("side-coerthas", "Sidequests: Coerthas and Ishgard", "Snowbound highlands under a pale moon: a lone watchtower on a white ridge, snow on the wind.",
               24, sky=("#0B1224", "#2A3656", "#A8B7D2"), water=("#26324E", "#0A0E1C"), moon=(300, 24, 11), glow="#EEF4FF")
    r = b.rng
    b.stars_n = 50
    b.constellation([(36, 18), (50, 10), (66, 20), (80, 12)], 0.45)
    far = peak_range([(110, 52), (150, 36), (190, 50), (240, 40), (290, 54), (340, 42), (376, 50)], b.hz, 90, 376)
    b.motif.append(f'<path d="{far}" fill="#5A6C92" opacity=".75"/>')
    b.motif.append('<g fill="#EEF4FF" opacity=".7"><path d="M150 36 l8 9 l-4 -1 l-4 4 l-4 -4 l-4 2 Z"/><path d="M240 40 l7 8 l-3 -1 l-4 3 l-3 -3 l-4 1 Z"/><path d="M340 42 l7 8 l-3 -1 l-3 3 l-4 -3 l-3 1 Z"/></g>')
    near = f"M160 {b.hz} Q200 {b.hz - 14} 240 {b.hz - 10} Q280 {b.hz - 20} 330 {b.hz - 12} Q356 {b.hz - 8} 376 {b.hz - 10} V{b.hz} Z"
    b.motif.append(f'<path d="{near}" fill="#9AA9C8" opacity=".75"/>')
    b.motif.append(f'<path d="M286 {b.hz - 17} V{b.hz - 38} l-2 0 l5 -6 l5 6 l-2 0 V{b.hz - 17} Z" fill="#1A2240"/>'
                   f'<rect x="288.4" y="{b.hz - 32}" width="1.2" height="2" fill="#FFD9A0"/>')
    snow = "".join(f'<path d="M{f(x)} {f(y)} l{f(-1.6)} {f(0.9)}" stroke="#F4F8FF" stroke-width=".5" opacity="{f(r.uniform(.3, .7))}" stroke-linecap="round"/>'
                   for x, y in ((r.uniform(0, 380), r.uniform(0, 118)) for _ in range(80)))
    b.fg(f"<g>{snow}</g>")
    return b


@banner
def side_dravania():
    b = Banner("side-dravania", "Sidequests: Dravania and Abalathia", "Islands adrift in a sea of cloud, rookery spires of rock, a long-winged shape far off.",
               25, sky=("#081228", "#22406C", "#94B4DA"), water=("#1C3052", "#070E1E"), moon=(232, 26, 10.5), glow="#F0F6FF")
    r = b.rng
    b.constellation([(34, 22), (48, 14), (62, 24), (78, 16)], 0.45)
    # Floating islands: flat tops, roots hanging beneath.
    for cx, cy, w in [(290, 44, 22), (338, 58, 14), (180, 56, 12)]:
        b.no_reflect.append(f'<path d="M{cx - w} {cy} Q{cx} {cy - 4} {cx + w} {cy} L{cx + w * 0.4} {cy + w * 0.7} L{cx} {cy + w * 1.1} L{cx - w * 0.5} {cy + w * 0.6} Z" fill="#1A2C4E"/>'
                            f'<path d="M{cx - w * 0.7} {cy - 1} q{w * 0.3} -6 {w * 0.6} -2 q{w * 0.3} -7 {w * 0.7} 0" fill="#12203C"/>')
    spires = " ".join(poly([(x - w, b.hz), (x - w * 0.3, b.hz - h), (x + w * 0.4, b.hz - h * 0.9), (x + w, b.hz)]) for x, h, w in [(236, 34, 5), (252, 22, 4), (318, 28, 4.5)])
    b.motif.append(f'<path d="{spires}" fill="#101C36"/>')
    # The sea of cloud over the horizon (not mirrored).
    clouds = "".join(f'<ellipse cx="{f(x)}" cy="{f(b.hz - r.uniform(0, 3))}" rx="{f(r.uniform(16, 34))}" ry="{f(r.uniform(4, 7))}"/>' for x in range(100, 380, 18))
    b.no_reflect.append(f'<g fill="#C9D8EE" opacity=".35" filter="{b.blur("cblur", 1.4)}">{clouds}</g>')
    b.back.append('<path d="M120 20 q5 -5 11 -3 q-4 1 -6 3 q4 -1 7 1 q-5 0 -7 2 q-2 2 -4 2 q1 -2 1 -3 q-3 1 -6 0 q4 -1 4 -2 Z" fill="#101C36" opacity=".75"/>')
    return b


@banner
def side_gyr_othard():
    b = Banner("side-gyr-abania-othard", "Sidequests: Gyr Abania and Othard", "Tall karst peaks over a rose-dusk sea, a gate standing in the shallows, lanterns on a far pier.",
               26, sky=("#100E1E", "#2E2442", "#B26A5E"), water=("#2A1C2E", "#0A0610"), moon=(260, 24, 10), glow="#FFE6D8")
    r = b.rng
    b.constellation([(34, 14), (50, 24), (66, 16), (82, 26)], 0.45)
    peaks = []
    for x, h, w in [(128, 26, 8), (146, 40, 9), (170, 30, 7), (318, 44, 10), (340, 30, 8), (362, 38, 9)]:
        peaks.append(f"M{x - w} {b.hz} Q{x - w * 0.9} {b.hz - h} {x} {b.hz - h - 3} Q{x + w * 0.9} {b.hz - h} {x + w} {b.hz} Z")
    b.motif.append(f'<path d="{" ".join(peaks)}" fill="#3A2640" opacity=".9"/>')
    # The gate in the water.
    gx, gw, gy = 234, 18, b.hz + 6
    gate = (f"M{gx - gw * 0.72} {gy} V{gy - 24} h2.2 V{gy} Z M{gx + gw * 0.72 - 2.2} {gy} V{gy - 24} h2.2 V{gy} Z "
            f"M{gx - gw} {gy - 26} Q{gx} {gy - 29} {gx + gw} {gy - 26} v2 Q{gx} {gy - 27} {gx - gw} {gy - 24} Z M{gx - gw * 0.8} {gy - 20} h{gw * 1.6} v1.6 h{-gw * 1.6} Z")
    b.no_reflect.append(f'<path d="{gate}" fill="#8A2E2E"/>')
    b.in_water.append(f'<g opacity=".35" transform="matrix(1 0 0 -1 0 {2 * gy})"><path d="{gate}" fill="#8A2E2E"/></g>')
    b.motif.append(f'<rect x="270" y="{b.hz - 3}" width="40" height="3" fill="#1C1226"/>')
    b.motif.append('<g fill="#FFB86A" opacity=".85">' + "".join(f'<circle cx="{x}" cy="{b.hz - 5}" r="1.1"/>' for x in (274, 282, 290, 298, 306)) + "</g>")
    return b


@banner
def side_norvrandt():
    b = Banner("side-norvrandt", "Sidequests: Norvrandt", "Lakeland shallows under a violet night: trees of crystal, fae lights and petals on the water.",
               27, sky=("#0A0C24", "#262A66", "#8A7ACC"), water=("#1C1E4A", "#06061A"), moon=(292, 26, 10.5), glow="#F2E8FF")
    r = b.rng
    b.constellation([(36, 18), (52, 10), (66, 22), (84, 14)], 0.45)
    far = ridge(r, 110, 376, b.hz, 66, 74, step=9, jag=0.35)
    b.motif.append(f'<path d="{far}" fill="#35306E" opacity=".85"/>')
    # Crystal trees: faceted trunks branching into shards.
    for x, h in [(214, 34), (240, 24), (338, 30), (358, 20)]:
        top = b.hz - h
        shards = []
        for dx, dy, s, lean in [(0, 0, 1.0, 0), (-5, 5, .65, -.35), (5, 4, .7, .35), (-2.5, -4, .5, -.15), (3, -3, .45, .2)]:
            sx, sy = x + dx, top + dy
            shards.append(poly([(sx + lean * 10 * s, sy - 13 * s), (sx + 3.4 * s, sy - 2 * s), (sx, sy + 3 * s), (sx - 3.4 * s, sy - 2 * s)]))
        b.motif.append(f'<path d="{poly([(x - 2, b.hz), (x - 1, top), (x + 1, top), (x + 2, b.hz)])}" fill="#1E1A48"/>'
                       f'<path d="M{x} {b.hz - h * .5} l-5 -6 M{x} {b.hz - h * .6} l5 -5" stroke="#1E1A48" stroke-width="1.2"/>'
                       f'<path d="{" ".join(shards)}" fill="#8C7FD8" opacity=".78"/>'
                       f'<path d="M{x} {top - 13} V{top + 3}" stroke="#E8E0FF" stroke-width=".4" opacity=".7"/>')
    lights = "".join(f'<circle cx="{f(r.uniform(160, 376))}" cy="{f(r.uniform(40, 100))}" r="{f(r.uniform(.5, 1.1))}" fill="{r.choice(["#F4C4EC", "#C8E8FF", "#FFF0BE"])}" opacity="{f(r.uniform(.4, .9))}"/>' for _ in range(24))
    b.fg(f"<g>{lights}</g>")
    return b


@banner
def side_ilsabard():
    b = Banner("side-ilsabard", "Sidequests: Ilsabard, Sharlayan and Thavnair", "A port city of onion domes and a scholar's tower across a still harbour, a cold northern ridge behind.",
               28, sky=("#0A1020", "#1E2E4A", "#86A2BC"), water=("#152236", "#050A14"), moon=(222, 26, 10))
    r = b.rng
    b.constellation([(36, 20), (50, 12), (64, 22), (80, 14), (94, 22)], 0.45)
    far = peak_range([(240, 58), (270, 50), (300, 56), (340, 46), (376, 52)], b.hz, 220, 376)
    b.motif.append(f'<path d="{far}" fill="#4A5E7C" opacity=".75"/>')
    domes = []
    for x, rr, h in [(258, 8, 18), (282, 11, 22), (306, 7, 14), (330, 9, 20)]:
        domes.append(f"M{x - rr} {b.hz - h} Q{x - rr} {b.hz - h - rr * 1.3} {x} {b.hz - h - rr * 1.9} Q{x + rr} {b.hz - h - rr * 1.3} {x + rr} {b.hz - h} Z")
        domes.append(f"M{x - rr * 0.8} {b.hz} V{b.hz - h} h{rr * 1.6} V{b.hz} Z M{x - 0.5} {b.hz - h - rr * 1.9} v-4 h1 v4 Z")
    domes.append(f"M346 {b.hz} V{b.hz - 40} l3 -4 l3 4 V{b.hz} Z")
    b.motif.append(f'<path d="{" ".join(domes)}" fill="#101A2C"/>')
    b.motif.append('<g fill="#FFD48A" opacity=".8">' + "".join(f'<rect x="{x}" y="{f(b.hz - r.uniform(6, 16))}" width="1.1" height="2"/>' for x in (254, 262, 278, 286, 303, 327, 334, 348)) + "</g>")
    return b


@banner
def side_tural():
    b = Banner("side-tural", "Sidequests: Tural", "Jungle highlands and a falling cascade under a warm teal night, palms leaning over the river mouth.",
               29, sky=("#06121A", "#0E2E36", "#4E8E78"), water=("#0C2A2A", "#030C0C"), moon=(250, 22, 10.5), glow="#F4FFE8")
    r = b.rng
    b.constellation([(34, 16), (48, 26), (64, 14), (80, 22)], 0.45)
    far = peak_range([(120, 56), (160, 44), (206, 54), (300, 38), (346, 50), (376, 46)], b.hz, 100, 376)
    b.motif.append(f'<path d="{far}" fill="#1C4A44" opacity=".85"/>')
    # The cascade down the cliff face.
    b.motif.append(f'<path d="M282 {b.hz} Q286 60 292 50 Q298 44 306 45 Q316 44 322 52 Q328 64 332 {b.hz} Z" fill="#0A221F"/>')
    b.motif.append('<path d="M302.5 46 q.6 18 -.4 36 M305.5 45.5 q.4 18 .2 36.5 M308.5 46 q-.4 18 .6 36" stroke="#CFEFEF" stroke-width=".55" fill="none" opacity=".5"/>'
                   '<path d="M301 46 q5 -2 9 0" stroke="#E8FFFF" stroke-width=".6" fill="none" opacity=".45"/>')
    b.motif.append(f'<ellipse cx="306" cy="{b.hz - 1}" rx="9" ry="2" fill="#E8FFFF" opacity=".35"/>')
    b.motif.append(f'<g color="#051410">{palm(196, b.hz, 24, -5, r)}{palm(210, b.hz, 18, 4, r)}{palm(338, b.hz, 22, 5, r)}{palm(356, b.hz, 28, -4, r)}{palm(370, b.hz, 18, 3, r)}</g>')
    b.motif.append(f'<path d="M180 {b.hz} Q200 {b.hz - 6} 230 {b.hz - 3} V{b.hz} Z M320 {b.hz} Q350 {b.hz - 7} 376 {b.hz - 5} V{b.hz} Z" fill="#061612"/>')
    return b


# -- other categories ----------------------------------------------------------------------------------------------
@banner
def allied_societies():
    b = Banner("allied-societies", "Allied Societies", "A night gathering by the water: tents and hide pennants round a fire, its sparks rising toward the moon.",
               31, sky=("#0B1022", "#1D2340", "#6A5A70"), water=("#161A30", "#05060E"), moon=(300, 26, 10))
    r = b.rng
    b.constellation([(34, 22), (50, 14), (66, 24), (82, 16)], 0.45)
    far = ridge(r, 110, 376, b.hz, 66, 74, step=10, jag=.3)
    b.motif.append(f'<path d="{far}" fill="#2A2E4E" opacity=".85"/>')
    tents = []
    for x, w, h in [(214, 12, 16), (250, 15, 20), (338, 12, 15)]:
        tents.append(poly([(x - w, b.hz), (x, b.hz - h), (x + w, b.hz)]))
        tents.append(f"M{x} {b.hz - h} l-2 -4 M{x} {b.hz - h} l2 -4")
    b.motif.append(f'<path d="{" ".join(tents)}" fill="#12142A" stroke="#12142A" stroke-width=".6"/>')
    # Pennants of several peoples, each a different muted hue.
    for x, col, h in [(230, "#8C6A3A", 30), (270, "#3E6E5A", 34), (322, "#6E4A7A", 28), (356, "#7A3E3E", 32)]:
        b.motif.append(f'<path d="M{x} {b.hz} V{b.hz - h}" stroke="#12142A" stroke-width=".8"/>'
                       f'<path d="M{x + .4} {b.hz - h} l9 2 l-9 3 Z" fill="{col}"/>')
    fx, fy = 294, b.hz - 1
    b.back.append(b.glow_ellipse("fire", fx, fy - 6, 34, 22, "#FF9A4A", .5))
    b.motif.append(f'<path d="M{fx - 4} {fy} Q{fx - 3} {fy - 7} {fx} {fy - 10} Q{fx + 3} {fy - 6} {fx + 4} {fy} Z" fill="#F08A3A"/>'
                   f'<path d="M{fx - 2} {fy} Q{fx - 1} {fy - 4} {fx} {fy - 6} Q{fx + 1.5} {fy - 3} {fx + 2} {fy} Z" fill="#FFD27A"/>')
    sparks = "".join(f'<circle cx="{f(fx + r.uniform(-6, 6) + i * 0.4)}" cy="{f(fy - 12 - i * 3.2)}" r="{f(r.uniform(.3, .6))}" fill="#FFC070" opacity="{f(.8 - i * .05)}"/>' for i in range(14))
    b.motif.append(sparks)
    return b


@banner
def class_job():
    b = Banner("class-job", "Class and Job Quests", "A training ground on a knoll by the water: a sword and a staff crossed and planted, a hammer and a fishing rod beside them.",
               32, sky=("#0D1022", "#262C4A", "#6E7AA6"), water=("#151A30", "#05070E"), moon=(336, 22, 10))
    r = b.rng
    b.constellation([(36, 16), (52, 24), (66, 14), (84, 22)], 0.45)
    far = ridge(r, 110, 376, b.hz, 66, 74, step=10, jag=.3)
    b.motif.append(f'<path d="{far}" fill="#2E3656" opacity=".85"/>')
    knoll = f"M200 {b.hz} Q260 {b.hz - 16} 330 {b.hz - 6} Q356 {b.hz - 2} 376 {b.hz - 3} V{b.hz} Z"
    b.motif.append(f'<path d="{knoll}" fill="#10142A"/>')
    # Sword and staff crossed.
    sx, sy = 272, b.hz - 12
    b.motif.append(f'<g transform="rotate(-18 {sx} {sy})"><path d="M{sx - 1.2} {sy} V{sy - 38} l1.2 -4 l1.2 4 V{sy} Z" fill="#AEB8D0"/>'
                   f'<path d="M{sx - 6} {sy - 4} h12 v1.6 h-12 Z M{sx - 0.9} {sy} v7 h1.8 v-7 Z" fill="{GILT}"/></g>')
    b.motif.append(f'<g transform="rotate(20 {sx} {sy})"><path d="M{sx - .7} {sy + 6} V{sy - 40}" stroke="#5E4A34" stroke-width="1.5"/>'
                   f'<circle cx="{sx}" cy="{sy - 42}" r="2.6" fill="#9FD0FF"/><circle cx="{sx}" cy="{sy - 42}" r="4.5" fill="none" stroke="{GILT}" stroke-width=".6"/></g>')
    # A hammer and a fishing rod beside them.
    b.motif.append(f'<path d="M306 {b.hz - 5} V{b.hz - 20}" stroke="#5E4A34" stroke-width="1.1"/><path d="M301 {b.hz - 22} h10 v4 h-10 Z" fill="#8A93B0"/>')
    b.motif.append(f'<path d="M322 {b.hz - 4} Q326 {b.hz - 26} 344 {b.hz - 38}" stroke="#5E4A34" stroke-width=".9" fill="none"/>'
                   f'<path d="M344 {b.hz - 38} Q348 {b.hz - 20} 350 {b.hz + 4}" stroke="{SILVER}" stroke-width=".3" fill="none" opacity=".6"/>')
    return b


@banner
def grand_company():
    b = Banner("grand-company", "Grand Company", "A watchtower and rampart above a harbour, three company standards in storm red, serpent gold and flame black.",
               33, sky=("#100E1C", "#2A2238", "#7A5A5A"), water=("#1C1626", "#07050C"), moon=(222, 26, 10.5))
    r = b.rng
    b.constellation([(36, 14), (50, 24), (68, 16), (84, 26)], 0.45)
    far = ridge(r, 110, 376, b.hz, 64, 72, step=9, jag=.35)
    b.motif.append(f'<path d="{far}" fill="#3A2E44" opacity=".85"/>')
    wall = [f"M244 {b.hz} V{b.hz - 14} H376 V{b.hz} Z", f"M300 {b.hz - 14} V{b.hz - 40} h18 V{b.hz - 14} Z"]
    for x in range(244, 376, 5):
        wall.append(f"M{x} {b.hz - 14} v-2.4 h2.4 v2.4 Z")
    for x in range(300, 318, 4.5 if False else 4):
        wall.append(f"M{x} {b.hz - 40} v-3 h2.4 v3 Z")
    b.motif.append(f'<path d="{" ".join(wall)}" fill="#130E1A"/>')
    b.motif.append(f'<rect x="308" y="{b.hz - 32}" width="1.4" height="3" fill="#FFCF8A" opacity=".85"/>')
    for x, col, trim in [(262, "#9E3B34", "#D9BE82"), (309, "#C4A23E", "#3A3024"), (354, "#26222A", "#C9A650")]:
        top = b.hz - (52 if x == 309 else 30)
        b.motif.append(f'<path d="M{x} {b.hz - (40 if x == 309 else 14)} V{top}" stroke="#130E1A" stroke-width=".9"/>'
                       f'<path d="M{x + .5} {top + 1} h9 v10 l-4.5 -3 l-4.5 3 Z" fill="{col}"/>'
                       f'<path d="M{x + .5} {top + 1} h9" stroke="{trim}" stroke-width=".7"/>')
    return b


@banner
def seasonal():
    b = Banner("seasonal", "Seasonal Events", "A festival night: paper lanterns drifting on the water, fireworks opening over the far shore under a crescent.",
               34, sky=("#0C0C24", "#22205A", "#5A4E9A"), water=("#16163A", "#05051A"), moon=(318, 24, 10), phase=4.5)
    r = b.rng
    b.constellation([(36, 20), (52, 12), (66, 22), (80, 14)], 0.45)
    far = ridge(r, 110, 376, b.hz, 68, 76, step=9, jag=.3)
    b.motif.append(f'<path d="{far}" fill="#2A2860" opacity=".85"/>')
    for cx, cy, col, n, rad in [(232, 30, "#F2D27A", 18, 13), (270, 20, "#E7A6C8", 14, 9), (196, 44, "#8FB4F0", 12, 8)]:
        rays = []
        for i in range(n):
            a = 2 * math.pi * i / n + r.uniform(-.08, .08)
            rays.append(f'<path d="M{f(cx + math.cos(a) * rad * .35)} {f(cy + math.sin(a) * rad * .35)} L{f(cx + math.cos(a) * rad)} {f(cy + math.sin(a) * rad)}" '
                        f'stroke="{col}" stroke-width=".55" stroke-linecap="round" opacity=".85"/>')
            rays.append(f'<circle cx="{f(cx + math.cos(a) * rad * 1.1)}" cy="{f(cy + math.sin(a) * rad * 1.1)}" r=".5" fill="{col}"/>')
        b.back.append(f'<g>{"".join(rays)}</g>')
        b.back.append(f'<circle cx="{cx}" cy="{cy}" r="{rad * 1.6}" fill="{b.radial(f"fw{cx}", cx, cy, rad * 1.6, [(0, col, .22), (1, col, 0)])}"/>')
    lan = []
    for _ in range(16):
        x, y = r.uniform(150, 370), r.uniform(b.hz + 6, 114)
        depth = (y - b.hz) / (H - b.hz)
        s = 1.2 + depth * 2.4
        lan.append(f'<ellipse cx="{f(x)}" cy="{f(y + s * 1.6)}" rx="{f(s * 1.4)}" ry="{f(s * .5)}" fill="#FFB65A" opacity=".25"/>'
                   f'<rect x="{f(x - s * .6)}" y="{f(y - s)}" width="{f(s * 1.2)}" height="{f(s * 1.6)}" rx="{f(s * .3)}" fill="#FFB65A"/>'
                   f'<rect x="{f(x - s * .6)}" y="{f(y - s)}" width="{f(s * 1.2)}" height="{f(s * .35)}" fill="#8A3A2A"/>')
    b.fg(f'<g opacity=".9">{"".join(lan)}</g>')
    return b


@banner
def chronicles():
    b = Banner("chronicles", "Chronicles of a New Era", "A colossal ancient guardian half sunk in the sea, a ring of ruined arches beside it, the old tales written in the stars.",
               35, sky=("#0A0E20", "#1C2446", "#56648E"), water=("#141C36", "#04060E"), moon=(248, 22, 10))
    r = b.rng
    b.constellation([(30, 26), (44, 14), (62, 20), (76, 10), (94, 18), (104, 30)], 0.55)
    far = ridge(r, 110, 376, b.hz, 70, 76, step=10, jag=.3)
    b.motif.append(f'<path d="{far}" fill="#2A3458" opacity=".85"/>')
    # The guardian: a crowned head and shoulders rising from the water, one arm raised with an open hand.
    g = (f"M270 {b.hz} V{b.hz - 16} Q272 {b.hz - 26} 284 {b.hz - 28} L286 {b.hz - 36} Q286 {b.hz - 48} 296 {b.hz - 50} "
         f"Q306 {b.hz - 48} 306 {b.hz - 36} L308 {b.hz - 28} Q320 {b.hz - 26} 322 {b.hz - 16} V{b.hz} Z")
    crown = f"M288 {b.hz - 49} L290 {b.hz - 56} L293 {b.hz - 51} L296 {b.hz - 58} L299 {b.hz - 51} L302 {b.hz - 56} L304 {b.hz - 49} Z"
    arm = f"M318 {b.hz - 22} L330 {b.hz - 40} L334 {b.hz - 52} l3 -1 l0 6 l3 -5 l2 1 l-1 7 L336 {b.hz - 38} L324 {b.hz - 16} Z"
    b.motif.append(f'<path d="{g} {crown} {arm}" fill="#0D1228"/>')
    b.motif.append(f'<path d="M292 {b.hz - 40} h3 M298 {b.hz - 40} h3" stroke="#9FC4FF" stroke-width=".8" opacity=".8"/>')
    arches = []
    for x in range(200, 256, 12):
        h = r.uniform(12, 20)
        arches.append(f"M{x} {b.hz} V{b.hz - h} A5 5 0 0 1 {x + 10} {b.hz - h} V{b.hz} h-2 V{b.hz - h + 1} A3 3 0 0 0 {x + 2} {b.hz - h + 1} V{b.hz} Z")
    b.motif.append(f'<path d="{" ".join(arches)}" fill="#161C38"/>')
    return b


@banner
def hildibrand():
    b = Banner("hildibrand", "Hildibrand Adventures", "A gentleman inspector's evening: a gas lamp on the quay, a top hat left on a bollard, a magnifier catching the crescent.",
               36, sky=("#0E0F1C", "#2A2A3E", "#7C7494"), water=("#1A1A2C", "#06060C"), moon=(292, 26, 10), phase=-4.2)
    r = b.rng
    b.constellation([(36, 18), (50, 26), (64, 16), (80, 24)], 0.45)
    far = ridge(r, 110, 376, b.hz, 66, 72, step=9, jag=.3)
    b.motif.append(f'<path d="{far}" fill="#34334C" opacity=".85"/>')
    quay = f"M200 {b.hz} V{b.hz - 6} H376 V{b.hz} Z"
    lamp = f"M236 {b.hz - 6} V{b.hz - 44} h1.4 V{b.hz - 6} Z M232 {b.hz - 50} h9 l-1.5 6 h-6 Z M233 {b.hz - 50} l3.7 -3 l3.7 3 Z"
    bollard = f"M300 {b.hz - 6} V{b.hz - 12} q0 -2 5 -2 q5 0 5 2 V{b.hz - 6} Z"
    hat = f"M297 {b.hz - 14} h16 v-1.6 h-2.6 V{b.hz - 26} h-10.8 V{b.hz - 15.6} h-2.6 Z"
    b.motif.append(f'<path d="{quay} {lamp} {bollard} {hat}" fill="#0E0E1A"/>')
    b.motif.append(f'<rect x="302.2" y="{b.hz - 19}" width="10.8" height="2" fill="{MOON}" opacity=".9"/>')
    b.motif.append(f'<rect x="233.4" y="{b.hz - 49}" width="6.2" height="4.6" fill="#FFE2A0"/>')
    b.back.append(f'<circle cx="236.7" cy="{b.hz - 47}" r="18" fill="{b.radial("gas", 236.7, b.hz - 47, 18, [(0, "#FFE2A0", .45), (1, "#FFE2A0", 0)])}"/>')
    # The magnifier held up to the crescent: its lens rim in brass.
    b.back.append(f'<circle cx="{b.mx - 1}" cy="{b.my + 1}" r="16" fill="none" stroke="{GILT}" stroke-width="1.1" opacity=".75"/>'
                  f'<path d="M{b.mx - 12} {b.my + 12} L{b.mx - 26} {b.my + 26}" stroke="{GILT}" stroke-width="2.2" stroke-linecap="round" opacity=".75"/>')
    fog = "".join(f'<ellipse cx="{f(x)}" cy="{f(b.hz + r.uniform(-2, 2))}" rx="{f(r.uniform(20, 40))}" ry="{f(r.uniform(3, 5))}"/>' for x in range(120, 380, 26))
    b.no_reflect.append(f'<g fill="#B8B4CC" opacity=".22" filter="{b.blur("fog", 2)}">{fog}</g>')
    return b


@banner
def relic():
    b = Banner("relic", "Relic Weapons and Unusual Endeavours", "An anvil on a stone shelf by the water, a blade standing in it and glowing, sparks climbing toward the moon.",
               37, sky=("#120C14", "#2E1C24", "#8A5236"), water=("#22141A", "#080406"), moon=(236, 24, 10.5), glow="#FFE6C8")
    r = b.rng
    b.constellation([(34, 22), (48, 12), (64, 22), (78, 12), (92, 22)], 0.5)
    far = ridge(r, 110, 376, b.hz, 64, 72, step=9, jag=.35)
    b.motif.append(f'<path d="{far}" fill="#3E2428" opacity=".85"/>')
    shelf = f"M250 {b.hz} L258 {b.hz - 10} H350 L358 {b.hz} Z"
    ax, ay = 304, b.hz - 10
    anvil = f"M{ax - 14} {ay - 9} h26 q-2 3 -8 4 v3 l4 2 v2 h-20 v-2 l4 -2 v-3 q-8 0 -12 -4 Z"
    b.motif.append(f'<path d="{shelf} {anvil}" fill="#140A0E"/>')
    b.back.append(b.glow_ellipse("forge", ax, ay - 26, 30, 34, "#FFB060", .55))
    b.motif.append(f'<path d="M{ax - 1.4} {ay - 9} V{ay - 46} l1.4 -5 l1.4 5 V{ay - 9} Z" fill="#FFE9B8"/>'
                   f'<path d="M{ax - 7} {ay - 13} h14 v1.8 h-14 Z" fill="{GILT_HI}"/><circle cx="{ax}" cy="{ay - 12}" r="1.2" fill="#E8574A"/>')
    sparks = "".join(f'<circle cx="{f(ax + r.uniform(-16, 16))}" cy="{f(ay - r.uniform(10, 60))}" r="{f(r.uniform(.3, .7))}" fill="#FFC070" opacity="{f(r.uniform(.4, .9))}"/>' for _ in range(26))
    b.motif.append(sparks)
    return b


@banner
def other():
    b = Banner("other", "Other Quests", "The moon road itself: calm water, a low far shore and a wide sky of stars.",
               38, sky=("#0B1024", "#1A2548", "#3A4E82"), water=("#101A36", "#04060E"), moon=(262, 28, 12))
    r = b.rng
    b.stars_n = 120
    b.road_width = 1.25
    b.constellation([(30, 18), (46, 10), (60, 22), (76, 14), (92, 24), (108, 16)], 0.5)
    far = ridge(r, 100, 376, b.hz, 74, 78, step=12, jag=.25)
    b.motif.append(f'<path d="{far}" fill="#1E2C52" opacity=".9"/>')
    return b


def main():
    os.makedirs(OUT, exist_ok=True)
    rows = []
    for fn in banners:
        b = fn()
        path = os.path.join(OUT, f"{b.slug}.svg")
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(b.svg())
        rows.append((b.slug, b.title, b.desc))
    with open(os.path.join(HERE, "banners.tsv"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("slug\ttitle\tdescription\n")
        for row in rows:
            fh.write("\t".join(row) + "\n")
    write_preview(rows)
    print(len(rows), "banners written to", OUT)


GLYPH_ORDER = ["all-quests", "removed", "chronicles", "chronicles-of-light", "hildibrand", "side-story", "relic", "endeavors",
               "other", "special", "festival", "deep-dungeon", "region-coerthas", "region-mordhona", "moonlit", "flight",
               "plan-fallback"]


def write_preview(rows):
    """preview.html: every banner as the hero shows it (scrim, corner marks, a title bottom-left) and the ornament atlas."""
    art = "../../../../Tsukimichi/assets/ui/banners/"
    ui = "../../../../Tsukimichi/assets/ui/"
    cards = []
    for slug, title, desc in rows:
        cards.append(f'''<figure class="card">
  <div class="hero"><img src="{art}{slug}@2x.png" alt="{title}" width="752" height="240">
    <div class="scrim"></div><i class="c tl"></i><i class="c tr"></i><i class="c br"></i><i class="c bl"></i>
    <div class="title"><b>{title.split(": ")[-1]}</b><span>Sample quest · Lv 50</span></div></div>
  <figcaption><code>{slug}@2x.png</code><span>{desc}</span></figcaption>
</figure>''')
    glyphs = "".join(f'<li><span class="g" style="background-position:-{2 + 28 * (i % 8)}px -{46 + 28 * (i // 8)}px"></span><code>{name}</code></li>'
                     for i, name in enumerate(GLYPH_ORDER))
    html = f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Moon Road Banners</title>
<link rel="preconnect" href="https://fonts.googleapis.com"><link href="https://fonts.googleapis.com/css2?family=Cinzel:wght@600&family=M+PLUS+1p:wght@400;500&display=swap" rel="stylesheet">
<style>
:root{{--bg:#0F1424;--abyss:#080B16;--raised:#1E2437;--line:#2A3149;--text:#DDE3F0;--mist:#A9B2CC;--dusk:#7C86A8;--gilt:#A88B52;--moon:#F2D27A}}
@media (prefers-color-scheme: light){{:root:not([data-theme="dark"]){{--bg:#F4F2EC;--abyss:#E6E2D8;--raised:#FFFFFF;--line:#D8D2C4;--text:#1C2338;--mist:#4A5270;--dusk:#5C6584;--gilt:#8A6E36;--moon:#8A6A10}}}}
:root[data-theme="light"]{{--bg:#F4F2EC;--abyss:#E6E2D8;--raised:#FFFFFF;--line:#D8D2C4;--text:#1C2338;--mist:#4A5270;--dusk:#5C6584;--gilt:#8A6E36;--moon:#8A6A10}}
:root[data-theme="dark"]{{--bg:#0F1424;--abyss:#080B16;--raised:#1E2437;--line:#2A3149;--text:#DDE3F0;--mist:#A9B2CC;--dusk:#7C86A8;--gilt:#A88B52;--moon:#F2D27A}}
*{{box-sizing:border-box}}
body{{margin:0;background:var(--bg);color:var(--text);font:15px/1.55 "M PLUS 1p",system-ui,sans-serif}}
main{{max-width:1180px;margin:0 auto;padding:28px 16px 64px}}
h1{{font:600 28px/1.2 Cinzel,serif;letter-spacing:.03em;margin:0 0 6px}}
h2{{font:600 13px/1 system-ui,sans-serif;letter-spacing:.14em;text-transform:uppercase;color:var(--mist);margin:40px 0 14px;display:flex;align-items:center;gap:10px}}
h2::after{{content:"";flex:1;height:1px;background:linear-gradient(90deg,var(--gilt),transparent)}}
p.lede{{color:var(--mist);max-width:760px;margin:0}}
.controls{{margin:18px 0 0;display:flex;gap:16px;flex-wrap:wrap;color:var(--mist);font-size:14px}}
.grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(100%,376px),1fr));gap:22px}}
.card{{margin:0}}
.hero{{position:relative;aspect-ratio:376/120;border-radius:6px;overflow:hidden;background:#080B16;box-shadow:inset 0 0 0 1px #080B16}}
.hero img{{display:block;width:100%;height:100%;object-fit:cover}}
.scrim{{position:absolute;inset:0;background:linear-gradient(transparent 35%,rgba(15,20,36,.94)),linear-gradient(90deg,rgba(8,11,22,.55),transparent 60%)}}
body.noscrim .scrim,body.noscrim .title{{display:none}}
body.nocorners .c{{display:none}}
.c{{position:absolute;width:12px;height:12px;background:url("../ornaments/corner-mark@2x.svg") center/contain no-repeat}}
.c.tl{{top:4px;left:4px}}.c.tr{{top:4px;right:4px;transform:scaleX(-1)}}.c.br{{bottom:4px;right:4px;transform:scale(-1,-1)}}.c.bl{{bottom:4px;left:4px;transform:scaleY(-1)}}
.title{{position:absolute;left:16px;bottom:12px;right:16px;display:flex;flex-direction:column;gap:2px}}
.title b{{font:600 19px/1.15 Cinzel,serif;color:#DDE3F0;letter-spacing:.02em}}
.title span{{font-size:12px;color:#A9B2CC}}
figcaption{{display:flex;flex-direction:column;gap:2px;padding:8px 2px 0;font-size:13px;color:var(--mist)}}
figcaption code{{color:var(--text);font-size:12.5px}}
.atlas{{display:flex;flex-wrap:wrap;gap:24px;align-items:flex-start}}
.sheet{{background:#0F1424;border:1px solid var(--line);border-radius:6px;padding:12px}}
.sheet img{{display:block;image-rendering:auto;max-width:100%;height:auto}}
.sheet small{{display:block;color:#A9B2CC;margin-top:6px;font-size:12px}}
ul.glyphs{{list-style:none;padding:0;margin:0;display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px;flex:1;min-width:260px}}
ul.glyphs li{{display:flex;align-items:center;gap:10px;background:#0F1424;border:1px solid var(--line);border-radius:6px;padding:6px 8px;color:#A9B2CC}}
.g{{width:24px;height:24px;flex:none;background:url("{ui}ornaments@2x.png") no-repeat;background-size:256px 128px}}
</style></head>
<body><main>
<h1>Moon Road banners</h1>
<p class="lede">Original category art for the detail hero (feature plan V4, step 5 of the banner chain): night water, the moon and its road of light, one motif per category. Each is 752 × 240 (2x of the hero's 376 × 120), quantised PNG, embedded in the plugin. Shown here as the hero draws it: the bottom and side scrims, corner marks, and a title over the bottom-left.</p>
<div class="controls"><label><input type="checkbox" id="scrim" checked> Hero scrim and title</label><label><input type="checkbox" id="corners" checked> Corner marks</label></div>
<h2>Category banners · {len(rows)}</h2>
<div class="grid">
{chr(10).join(cards)}
</div>
<h2>Ornament atlas</h2>
<div class="atlas">
  <div class="sheet"><img src="{ui}ornaments@2x.png" width="512" height="256" alt="ornaments@2x.png"><small>ornaments@2x.png · 512 × 256</small></div>
  <div class="sheet"><img src="{ui}ornaments.png" width="256" height="128" alt="ornaments.png"><small>ornaments.png · 256 × 128</small></div>
  <ul class="glyphs">{glyphs}</ul>
</div>
</main>
<script>
for (const [id, cls] of [["scrim", "noscrim"], ["corners", "nocorners"]]) {{
  const box = document.getElementById(id);
  box.addEventListener("change", () => document.body.classList.toggle(cls, !box.checked));
}}
</script>
</body></html>
'''
    with open(os.path.join(HERE, "preview.html"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(html)


if __name__ == "__main__":
    main()
