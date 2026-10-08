"""Our own painting for 4-3 "Lamps on the Steps": a stair of lamp glows climbing a dark terrace in Ul'dah by night.

The great sandstone terrace below the Steps of Nald stands in near darkness, and a ceremonial stair climbs its face in
three flights: from the plaza at the lower right up to a landing at the left, back across to a landing at the right,
and up again to the top terrace, a zigzag. Brass lanterns stand on the newel posts of the balustrade, at each landing
and along each flight, and every one throws a pool of warm light on the stone round it: the handrail, the balusters and
the steps behind them, the ashlar courses of the wall. Between the pools the terrace falls back into the night, so the
stair reads as a chain of light climbing the dark. A lit doorway opens in the wall between the first two flights with
a lantern hung in its arch, a great gateway glows at the stair's foot, a bracket lamp burns over the upper landing
between two crimson banners, and over the parapet the city's domes and a minaret stand against a violet sky, a full
moon over the great dome to the left.

Light only, no figure (charter 4, 4-3): every shape is architecture or light.

FEATURES (board units): each flight's handrail as a polyline (the landings included), each lamp's flame (where its
pool centres), the doorway's arch, the gateway, the two parapets and the moon.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

TOP = 214.0                    # the top terrace's floor line (the parapet's foot)
# the flights' handrails, bottom to top: (x0, y0) -> (x1, y1); the landings are level runs at the turning points
F1 = ((752.0, 498.0), (160.0, 392.0))
F2 = ((160.0, 392.0), (632.0, 290.0))
F3 = ((632.0, 290.0), (330.0, 214.0))
LANDING_B = ((92.0, 392.0), (160.0, 392.0))
LANDING_C = ((632.0, 290.0), (708.0, 290.0))
FLAME = 22.0                   # a newel lantern's flame stands this far above the handrail
# the newel lanterns: name, flight, x on the handrail
NEWELS = [("1a", F1, 640.0), ("1b", F1, 418.0), ("B", F1, 160.0), ("2a", F2, 348.0), ("2b", F2, 466.0),
          ("C", F2, 632.0), ("3a", F3, 520.0), ("D", F3, 330.0)]
DOOR = (600.0, 404.0, 30.0)    # the doorway's arch: centre and inner radius (its voussoirs run 30-40)
DOOR_LAMP = (600.0, 398.0)
BRACKET = (196.0, 272.0)       # the bracket lamp over the upper landing
GATE = (212.0, 486.0, 50.0)    # the great gateway at the stair's foot: the arch's centre and inner radius
MOON = (128.0, 58.0, 20.0)
BANNERS = [(124.0, 30.0, 92.0), (268.0, 30.0, 92.0), (700.0, 28.0, 70.0)]   # x, width, length (hung from TOP + 8)


def rail_y(f, x):
    (x0, y0), (x1, y1) = f
    return y0 + (y1 - y0) * (x - x0) / (x1 - x0)


def newel(n):
    _name, f, x = n
    return (x, rail_y(f, x) - FLAME)


FEATURES = {
    "rail 1": {"points": [[700.0, round(rail_y(F1, 700.0), 1)], list(F1[1])]},
    "rail 2": {"points": [list(F2[0]), list(F2[1])]},
    "rail 3": {"points": [list(F3[0]), list(F3[1])]},
    "landing B": {"points": [list(p) for p in LANDING_B]},
    "landing C": {"points": [list(p) for p in LANDING_C]},
    "lamps": {n[0]: [round(c, 1) for c in newel(n)] for n in NEWELS},
    "door lamp": list(DOOR_LAMP),
    "bracket lamp": list(BRACKET),
    "door": {"circle": [DOOR[0], DOOR[1], DOOR[2] + 5.0], "from": 180, "sweep": 180},
    "gate": {"circle": list(GATE), "from": 180, "sweep": 180},
    "parapet L": {"points": [[140.0, TOP - 8], [300.0, TOP - 8]]},
    "parapet R": {"points": [[470.0, TOP - 8], [660.0, TOP - 8]]},
    "moon": list(MOON),
    "gate opening": {"points": [[round(GATE[0] + (GATE[2] + 4) * math.cos(math.radians(t)), 1),
                                 round(GATE[1] + (GATE[2] + 4) * math.sin(math.radians(t)), 1)] for t in range(180, 361, 15)]
                     + [[GATE[0] + GATE[2] + 4, 600.0], [GATE[0] - GATE[2] - 4, 600.0]]},
    **{f"banner {i + 1}": {"points": [[bx - bw / 2, TOP + 6], [bx + bw / 2, TOP + 6], [bx + bw / 2, TOP + 6 + bl],
                                      [bx, TOP + bl - 6], [bx - bw / 2, TOP + 6 + bl]]}
       for i, (bx, bw, bl) in enumerate(BANNERS)},
}


# ------------------------------------------------------------------------------------------------ drawing helpers
class Canvas:
    """Masks drawn with PIL at 3x supersampling in board units and box-filtered to the painting's pixels."""

    def __init__(self, S, ss=3):
        self.S, self.ss = S, ss
        self.W, self.H = int(800 * S), int(600 * S)
        self.k = S * ss

    def new(self):
        im = Image.new("L", (self.W * self.ss, self.H * self.ss), 0)
        return im, ImageDraw.Draw(im)

    def done(self, im):
        return np.asarray(im.resize((self.W, self.H), Image.BOX), np.float32) / 255

    def P(self, pts):
        return [(x * self.k, y * self.k) for (x, y) in pts]

    def w(self, units):
        return max(1, int(round(units * self.k)))


def over(px, m, col):
    m = np.clip(m, 0, 1)[..., None]
    return px * (1 - m) + (hexc(col) if isinstance(col, str) else col) * m


def _hash(a, b):
    v = np.sin(a * 127.1 + b * 311.7) * 43758.5453
    return v - np.floor(v)


# ------------------------------------------------------------------------------------------------ the painting
def paint(S=1):
    C = Canvas(S)
    W, H = C.W, C.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    mx, my, mr = MOON
    d_moon = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)

    # ---- the sky: deep indigo overhead, a rose-violet glow from the city's lamps along the parapet
    px = ramp(np.clip(Y / TOP, 0, 1), [(0, "#06051A"), (0.5, "#130E34"), (0.85, "#25184A"), (1.0, "#3A2152")])
    px = screen(px, hexc("#7A3A62") * (np.exp(-((TOP - Y) / 46) ** 2) * 0.30 * (Y < TOP + 4))[..., None])
    px = screen(px, hexc("#C6C8F0") * (np.exp(-(d_moon / 90) ** 2) * 0.16 + np.exp(-(d_moon / 300) ** 2) * 0.07)[..., None])
    px = strokes(px, (Y < TOP).astype(np.float32), S, lambda x, y: 0.06 * math.sin(x / 80), int(3000 * S * S),
                 length=(10, 22), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=41)
    px = RL.stars(px, S, 700, 17, (0, 0, W, (TOP - 30) * S),
                  avoid=lambda x, y: float(np.clip((math.hypot(x / S - mx, y / S - my) - 40) / 120, 0, 1))
                  * float(np.clip((TOP - 40 - y / S) / 60, 0, 1)), bright=0.5)
    # thin cloud lying across the sky, silvered near the moon
    streak = np.zeros((H, W), np.float32)
    for (y0, amp, th, k, seed) in ((96, 6, 3.0, 0.7, 3), (132, 8, 4.2, 0.5, 4), (40, 10, 5.0, 0.35, 5)):
        wob = y0 + amp * np.sin(X / 110 + seed) + 8 * (fbm(1, W, 70 * S, 3, seed + 40)[0][None, :] - 0.5)
        band = np.exp(-((Y - wob) / th) ** 2) * smooth(0.40, 0.72, fbm(H, W, 46 * S, 4, seed + 60))
        streak = np.maximum(streak, band * k)
    streak = blur(streak, 0.8 * S)
    lit = np.exp(-(d_moon / 150) ** 2)
    scol = hexc("#2A2050") * (1 - lit[..., None]) + hexc("#B8B8E6") * lit[..., None]
    px = px * (1 - 0.4 * streak[..., None]) + scol * (0.4 * streak[..., None])
    px, _disc = RL.moon_emissive(px, S, mx, my, mr, seed=11)

    # ---- the city over the parapet: the great dome hall at the left, a minaret at the right, roofs between
    city_far, dr = C.new()
    for (x0, x1, top) in ((300, 380, 196), (372, 440, 188), (430, 520, 194), (512, 560, 184), (556, 650, 198),
                          (690, 760, 190), (40, 100, 192)):
        dr.rectangle([x0 * C.k, top * C.k, x1 * C.k, (TOP + 2) * C.k], fill=255)
    dr.ellipse([(560 - 26) * C.k, (166 - 22) * C.k, (560 + 26) * C.k, (166 + 22) * C.k], fill=255)
    dr.rectangle([(556) * C.k, 132 * C.k, 564 * C.k, 146 * C.k], fill=255)
    dr.ellipse([(462 - 16) * C.k, (186 - 14) * C.k, (462 + 16) * C.k, (186 + 14) * C.k], fill=255)
    far = C.done(city_far)
    px = over(px, far, np.array([0.075, 0.055, 0.13], np.float32))
    city, dr = C.new()
    # the great dome on its drum, a lantern cupola and finial; two small domes at its shoulders
    dr.rectangle([96 * C.k, 160 * C.k, 300 * C.k, (TOP + 2) * C.k], fill=255)
    dr.pieslice([(198 - 96) * C.k, (160 - 62) * C.k, (198 + 96) * C.k, (160 + 62) * C.k], 180, 360, fill=255)
    dr.rectangle([190 * C.k, 88 * C.k, 206 * C.k, 100 * C.k], fill=255)
    dr.pieslice([190 * C.k, 80 * C.k, 206 * C.k, 96 * C.k], 180, 360, fill=255)
    dr.line([(198 * C.k, 82 * C.k), (198 * C.k, 66 * C.k)], fill=255, width=C.w(1.6))
    for cx in (106, 290):
        dr.pieslice([(cx - 22) * C.k, (166 - 20) * C.k, (cx + 22) * C.k, (166 + 20) * C.k], 180, 360, fill=255)
        dr.rectangle([(cx - 22) * C.k, 165 * C.k, (cx + 22) * C.k, (TOP + 2) * C.k], fill=255)
    # the minaret: shaft, balcony, bulb and spire; a lower tower beside it
    dr.rectangle([668 * C.k, 104 * C.k, 692 * C.k, (TOP + 2) * C.k], fill=255)
    dr.rectangle([660 * C.k, 118 * C.k, 700 * C.k, 124 * C.k], fill=255)
    dr.ellipse([(680 - 15) * C.k, (88 - 16) * C.k, (680 + 15) * C.k, (88 + 18) * C.k], fill=255)
    dr.line([(680 * C.k, 74 * C.k), (680 * C.k, 50 * C.k)], fill=255, width=C.w(1.6))
    dr.rectangle([610 * C.k, 150 * C.k, 646 * C.k, (TOP + 2) * C.k], fill=255)
    dr.pieslice([606 * C.k, 132 * C.k, 650 * C.k, 168 * C.k], 180, 360, fill=255)
    near = C.done(city)
    # the dome's ribs and its moonlit flank; the drum's arcades lit from within
    du = (X - 198) / 96
    dv = (160 - Y) / 62
    on_dome = (du ** 2 + dv ** 2 < 1) & (Y < 160)
    ribs = np.abs(np.sin(np.arcsin(np.clip(du / np.sqrt(np.maximum(1 - dv ** 2, 1e-3)), -1, 1)) * 5.5)) < 0.08
    body = ramp(np.clip((mx + 60 - X) / 260 + (160 - Y) / 400, 0, 1), [(0, "#0C0A1C"), (0.6, "#17142E"), (1.0, "#2A2650")])
    body = body * (1 - 0.35 * (ribs & on_dome))[..., None]
    rim = np.clip(near - np.roll(np.roll(near, int(1.4 * S), 1), int(1.4 * S), 0), 0, 1)
    px = over(px, near, body)
    px = screen(px, hexc("#B8BEF0") * (rim * 0.45 * (X < 330))[..., None])
    # small lit windows in the drum and the city: warm, dim
    win, dr = C.new()
    rng = np.random.default_rng(23)
    for x0 in range(112, 296, 16):
        dr.rounded_rectangle([x0 * C.k, 176 * C.k, (x0 + 5) * C.k, 192 * C.k], radius=2.5 * C.k, fill=255)
    for _ in range(46):
        x0, y0 = rng.uniform(300, 760), rng.uniform(172, 208)
        if far[min(H - 1, int(y0 * S)), min(W - 1, int(x0 * S))] + near[min(H - 1, int(y0 * S)), min(W - 1, int(x0 * S))] > 0.5:
            dr.rectangle([x0 * C.k, y0 * C.k, (x0 + 2.2) * C.k, (y0 + 3.2) * C.k], fill=int(rng.uniform(120, 255)))
    for y0 in (130, 150, 170, 190):
        dr.rectangle([678 * C.k, y0 * C.k, 682 * C.k, (y0 + 7) * C.k], fill=200)
    wins = C.done(win)
    px = screen(px, hexc("#E89A50") * (wins * 0.55)[..., None])
    px = screen(px, hexc("#C86A3A") * (blur(wins, 3 * S) * 0.35)[..., None])

    # ---- the terrace: ashlar courses of sandstone, lit only by the lamps (and a breath of the moon from the left)
    wall = (Y >= TOP - 8).astype(np.float32)
    course = np.floor((Y - TOP) / 13.0)
    off = _hash(course, 3.0) * 40
    bw = 30 + 14 * _hash(course, 7.0)
    bx = np.floor((X + off) / bw)
    tone = 0.78 + 0.34 * _hash(course * 13 + bx, 1.7)
    fy = (Y - TOP) / 13.0 - course
    fx = (X + off) / bw - bx
    joint = np.maximum(smooth(0.075, 0.0, fy) * 0.9, smooth(0.04, 0.0, np.minimum(fx, 1 - fx) * bw / 13.0) * 0.8)
    grime = fbm(H, W, 22 * S, 4, 51)
    streaks = smooth(0.55, 0.85, fbm(H, W, 6 * S, 3, 52) * 0.4 + fbm(1, W, 9 * S, 3, 53)[0][None, :] * 0.6)
    chip = fbm(H, W, 3 * S, 3, 54)
    albedo = tone * (0.80 + 0.35 * grime) * (1 - 0.22 * streaks) * (0.92 + 0.16 * chip) * (1 - 0.55 * joint)

    # the lamps' light: every lantern's pool on the stone (a near pool and a broad spill)
    lamps = [newel(n) for n in NEWELS]
    lights = [(x, y, 1.0) for (x, y) in lamps] + [(DOOR_LAMP[0], DOOR_LAMP[1], 0.8), (BRACKET[0], BRACKET[1], 0.85)]
    light = np.zeros((H, W), np.float32)
    for (lx, ly, k) in lights:
        d2 = (X - lx) ** 2 + ((Y - ly) * 0.92) ** 2
        light += k * (1.05 / (1 + d2 / 26.0 ** 2) ** 1.6 + 0.09 * np.exp(-d2 / 60.0 ** 2))
    gx, gy, gr = GATE
    light += 0.40 * np.exp(-((X - gx) ** 2 + ((Y - 560) * 1.6) ** 2) / 80.0 ** 2)
    light = light / (1 + 0.35 * light)
    moonfill = 0.12 * smooth(620, 60, X) * smooth(600, TOP, Y)
    stone_lit = hexc("#DCA0A4")
    stone_dark = hexc("#1C1530")
    stone = stone_dark * (0.42 + moonfill)[..., None] + stone_lit * (light * 0.66)[..., None]
    stone = stone * albedo[..., None]
    px = px * (1 - wall[..., None]) + stone * wall[..., None]

    # the parapet: a coping band with stepped merlons along the top terrace, lit at D
    par, dr = C.new()
    dr.rectangle([0, (TOP - 8) * C.k, 800 * C.k, (TOP + 4) * C.k], fill=255)
    for x0 in np.arange(80, 724, 22):
        dr.polygon(C.P([(x0, TOP - 8), (x0, TOP - 13), (x0 + 3, TOP - 13), (x0 + 3, TOP - 17), (x0 + 9, TOP - 17),
                        (x0 + 9, TOP - 13), (x0 + 12, TOP - 13), (x0 + 12, TOP - 8)]), fill=255)
    parm = C.done(par)
    pcol = (hexc("#2A2040") * 0.7)[None, None, :] + stone_lit * (light * 0.55)[..., None]
    px = over(px, parm, pcol * 0.95)
    top_edge = np.clip(parm - np.roll(parm, int(1.2 * S), 0), 0, 1)
    px = screen(px, hexc("#F0C080") * (top_edge * np.clip(light, 0, 1) * 0.45)[..., None])

    # ---- the banners: crimson cloth hung from the parapet, gold-bordered with a sun disc, lit by the bracket lamp
    for (bxc, bwid, blen) in BANNERS:
        im, dr = C.new()
        y0 = TOP + 6
        dr.polygon(C.P([(bxc - bwid / 2, y0), (bxc + bwid / 2, y0), (bxc + bwid / 2, y0 + blen),
                        (bxc, y0 + blen - 12), (bxc - bwid / 2, y0 + blen)]), fill=255)
        m = C.done(im)
        im2, dr2 = C.new()
        dr2.line(C.P([(bxc - bwid / 2 + 3, y0 + 4), (bxc - bwid / 2 + 3, y0 + blen - 3)]), fill=255, width=C.w(1.2))
        dr2.line(C.P([(bxc + bwid / 2 - 3, y0 + 4), (bxc + bwid / 2 - 3, y0 + blen - 3)]), fill=255, width=C.w(1.2))
        r = bwid * 0.28
        cy = y0 + blen * 0.38
        dr2.ellipse([(bxc - r) * C.k, (cy - r) * C.k, (bxc + r) * C.k, (cy + r) * C.k], outline=255, width=C.w(1.4))
        for a in range(0, 360, 30):
            ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
            dr2.line(C.P([(bxc + ca * r * 1.25, cy + sa * r * 1.25), (bxc + ca * r * 1.6, cy + sa * r * 1.6)]),
                     fill=255, width=C.w(1.0))
        gold = C.done(im2) * m
        fold = 0.75 + 0.25 * np.sin((X - bxc) / bwid * 9.0) ** 2
        cloth = hexc("#7A1028")[None, None, :] * (0.25 + 0.95 * np.clip(light, 0, 1.2))[..., None] * fold[..., None]
        px = over(px, m, cloth)
        px = over(px, gold, hexc("#D8A050") * (0.25 + 0.8 * np.clip(light, 0, 1.2))[..., None])
        px = over(px, blur(m, 2.5 * S) * (1 - m) * smooth(bxc, bxc + bwid, X) * 0.5, np.array([0.02, 0.015, 0.03], np.float32))

    # ---- the great gateway at the stair's foot: a deep arch, warm light inside, voussoirs round it
    gx, gy, gr = GATE
    d_g = np.sqrt((X - gx) ** 2 + (Y - gy) ** 2)
    opening = np.where(Y < gy, (d_g < gr).astype(np.float32), ((np.abs(X - gx) < gr) & (Y < 600)).astype(np.float32))
    opening = blur(opening, 0.5 * S)
    inside = ramp(np.clip((Y - (gy - gr)) / (600 - gy + gr), 0, 1), [(0, "#120808"), (0.45, "#3A1C0E"), (1.0, "#7A4418")])
    inside = inside * (0.55 + 0.6 * np.exp(-((X - gx) / 26) ** 2))[..., None]
    # the passage recedes: a smaller inner arch, its jambs in shadow, a far lamp at its end
    d_in = np.sqrt((X - gx) ** 2 + (Y - gy - 14) ** 2)
    inner = np.where(Y < gy + 14, d_in < gr * 0.55, np.abs(X - gx) < gr * 0.55).astype(np.float32)
    inside = inside * (0.55 + 0.45 * blur(inner, 1.0 * S))[..., None]
    inside = screen(inside, hexc("#FFB868") * (np.exp(-((X - gx) ** 2 + (Y - gy - 6) ** 2) / 9.0 ** 2) * 0.5)[..., None])
    px = over(px, opening, inside)
    vous = ((d_g > gr) & (d_g < gr + 13) & (Y < gy + 1)).astype(np.float32)
    ang = np.degrees(np.arctan2(Y - gy, X - gx))
    vj = smooth(0.06, 0.0, np.abs(((ang + 180) / 12.0) % 1 - 0.5) * 2 - 0.94 + 0.06)
    px = over(px, blur(vous, 0.5 * S), stone * (0.9 + 0.3 * (1 - vj))[..., None] * 1.15)
    # the gateway's inner glow spilling out over the plaza floor
    px = screen(px, hexc("#FFB060") * (np.exp(-((X - gx) ** 2 + ((Y - 584) * 1.5) ** 2) / 70.0 ** 2) * 0.14)[..., None])

    # ---- the doorway between the first two flights, with a lantern hung in its arch
    dx_, dy_, drr = DOOR
    d_d = np.sqrt((X - dx_) ** 2 + (Y - dy_) ** 2)
    dop = np.where(Y < dy_, (d_d < drr).astype(np.float32), ((np.abs(X - dx_) < drr) & (Y < 470)).astype(np.float32))
    dop = blur(dop, 0.5 * S)
    din = ramp(np.clip((Y - (dy_ - drr)) / 80, 0, 1), [(0, "#1E0E0A"), (0.5, "#4A2410"), (1.0, "#6A3A18")])
    din = din * (0.8 + 0.9 * np.exp(-((X - dx_) ** 2 + (Y - DOOR_LAMP[1] - 6) ** 2) / 22.0 ** 2))[..., None]
    # a lattice screen of the city's star pattern across the doorway's lower half
    lat = (smooth(0.12, 0.0, np.abs(((X - Y) / 9.0) % 1 - 0.5) * 2 - 0.86 + 0.12)
           + smooth(0.12, 0.0, np.abs(((X + Y) / 9.0) % 1 - 0.5) * 2 - 0.86 + 0.12)).clip(0, 1) * smooth(418, 424, Y)
    din = din * (1 - 0.75 * lat)[..., None]
    px = over(px, dop, np.clip(din, 0, 1))
    dv_ = ((d_d > drr) & (d_d < drr + 10) & (Y < dy_ + 1)).astype(np.float32)
    dang = np.degrees(np.arctan2(Y - dy_, X - dx_))
    dvj = smooth(0.08, 0.0, np.abs(((dang + 180) / 15.0) % 1 - 0.5) * 2 - 0.92 + 0.08)
    px = over(px, blur(dv_, 0.5 * S), stone * (0.95 + 0.35 * (1 - dvj))[..., None] * 1.2)
    # the hanging chain
    ch, dr = C.new()
    dr.line(C.P([(dx_, dy_ - drr), (DOOR_LAMP[0], DOOR_LAMP[1] - 10)]), fill=255, width=C.w(0.8))
    px = over(px, C.done(ch), hexc("#1A1010"))

    # ---- the flights: steps behind a balustrade (handrail, vase balusters, plinth) on a moulded stringer
    def flight(f, extra=()):
        segs = [f] + list(extra)
        for (a, b) in segs:
            (x0, y0), (x1, y1) = a, b
            xa, xb = min(x0, x1), max(x0, x1)
            ya, yb = (y0, y1) if x0 < x1 else (y1, y0)
            ry = lambda x: ya + (yb - ya) * (x - xa) / max(xb - xa, 1e-6)
            # the stringer and plinth body
            im, dr = C.new()
            dr.polygon(C.P([(xa, ry(xa) + 4), (xb, ry(xb) + 4), (xb, ry(xb) + 46), (xa, ry(xa) + 46)]), fill=255)
            body = C.done(im)
            im, dr = C.new()
            dr.polygon(C.P([(xa, ry(xa) + 4), (xb, ry(xb) + 4), (xb, ry(xb) + 18), (xa, ry(xa) + 18)]), fill=255)
            gap = C.done(im)
            yield body, gap, xa, xb, ry

    rails, dr_r = C.new()
    balus, dr_b = C.new()
    plinth, dr_p = C.new()
    mould, dr_m = C.new()
    treads, dr_t = C.new()
    bodies = np.zeros((H, W), np.float32)
    gaps = np.zeros((H, W), np.float32)
    for f, extra in ((F3, ()), (F2, (LANDING_C,)), (F1, (LANDING_B,))):
        for body, gap, xa, xb, ry in flight(f, extra):
            bodies = np.maximum(bodies, body)
            gaps = np.maximum(gaps, gap)
            dr_r.polygon(C.P([(xa, ry(xa) - 2.5), (xb, ry(xb) - 2.5), (xb, ry(xb) + 4), (xa, ry(xa) + 4)]), fill=255)
            dr_p.polygon(C.P([(xa, ry(xa) + 18), (xb, ry(xb) + 18), (xb, ry(xb) + 27), (xa, ry(xa) + 27)]), fill=255)
            dr_m.line(C.P([(xa, ry(xa) + 31), (xb, ry(xb) + 31)]), fill=255, width=C.w(1.6))
            dr_m.line(C.P([(xa, ry(xa) + 46), (xb, ry(xb) + 46)]), fill=255, width=C.w(2.2))
            for x in np.arange(xa + 3.5, xb - 1, 6.5):
                yt, yb_ = ry(x) + 4, ry(x) + 18
                dr_b.rectangle([(x - 1.0) * C.k, yt * C.k, (x + 1.0) * C.k, yb_ * C.k], fill=255)
                dr_b.ellipse([(x - 2.1) * C.k, (yt + 5.5) * C.k, (x + 2.1) * C.k, (yt + 11.5) * C.k], fill=255)
                dr_b.rectangle([(x - 1.7) * C.k, yt * C.k, (x + 1.7) * C.k, (yt + 1.6) * C.k], fill=255)
            slope = abs((ry(xb) - ry(xa)) / max(xb - xa, 1e-6))
            if slope > 0.05:
                step = 5.0 / slope
                for x in np.arange(xa, xb, step):
                    yl = ry(x) + 13 if ry(xb) < ry(xa) else ry(x + step) + 13
                    dr_t.line(C.P([(x, yl), (x + step, yl)]), fill=255, width=C.w(1.1))
                    dr_t.line(C.P([(x + step if ry(xb) < ry(xa) else x, yl), (x + step if ry(xb) < ry(xa) else x, yl + 5)]),
                              fill=110, width=C.w(0.8))
    rails, balus, plinth, mould, treads = (C.done(m) for m in (rails, balus, plinth, mould, treads))
    lit = np.clip(light, 0, 1.3)
    # the stringer's face (smooth ashlar), and the steps glimpsed between the balusters (darker, their nosings lit)
    smooth_stone = (stone_dark * (0.6 + moonfill)[..., None] + stone_lit * (lit * 0.66)[..., None]) * \
        (0.9 + 0.15 * grime)[..., None]
    px = over(px, bodies, smooth_stone)
    px = over(px, gaps, smooth_stone * 0.42)
    px = screen(px, hexc("#FFD090") * (treads * lit * 0.45)[..., None])
    px = over(px, mould, smooth_stone * 0.55)
    px = over(px, plinth, smooth_stone * 1.12)
    px = over(px, balus, smooth_stone * 1.18)
    bal_edge = np.clip(balus - np.roll(balus, int(1.0 * S), 1), 0, 1)
    px = screen(px, hexc("#FFC880") * (bal_edge * lit * 0.35)[..., None])
    px = over(px, rails, smooth_stone * 1.25)
    rail_top = np.clip(rails - np.roll(rails, int(1.4 * S), 0), 0, 1)
    px = screen(px, hexc("#FFD49A") * (rail_top * lit * 0.55)[..., None])
    # the shadow each flight casts on the wall below it
    shade = np.clip(blur(bodies, 4 * S) - bodies, 0, 1) * (1 - smooth(0.0, 1.0, lit))
    px = px * (1 - 0.45 * np.roll(shade, int(5 * S), 0))[..., None]

    # ---- the plaza floor at the bottom: paving in perspective, the gate's and the first lantern's light on it
    floor = smooth(548, 552, Y) * (1 - opening)
    fl_u = (X - 400) / np.maximum(Y - 470, 10)
    fl_lines = smooth(0.10, 0.0, np.abs((fl_u * 6) % 1 - 0.5) * 2 - 0.88 + 0.1)
    fl_rows = smooth(0.12, 0.0, np.abs((np.log(np.maximum(Y - 520, 1)) * 5) % 1 - 0.5) * 2 - 0.86 + 0.12)
    fcol = (stone_dark * 0.6 + stone_lit * (lit * 0.5)[..., None]) * (1 - 0.35 * np.maximum(fl_lines, fl_rows))[..., None]
    px = over(px, floor, fcol)

    # ---- the lanterns: newel piers with brass lanterns, the door's hanging lantern, the bracket lamp
    fire = np.zeros((H, W), np.float32)
    halo = np.zeros((H, W), np.float32)
    brass, dr_br = C.new()
    piers, dr_pi = C.new()
    for (x, y) in lamps:
        r_ = y + FLAME
        dr_pi.rectangle([(x - 4.5) * C.k, (r_ - 4) * C.k, (x + 4.5) * C.k, (r_ + 27) * C.k], fill=255)
        dr_pi.rectangle([(x - 6) * C.k, (r_ - 7) * C.k, (x + 6) * C.k, (r_ - 3) * C.k], fill=255)
        _lantern(dr_br, C, x, y)
    for (x, y) in (DOOR_LAMP, BRACKET):
        _lantern(dr_br, C, x, y, s=0.85)
    bx_, by_ = BRACKET
    dr_br.line(C.P([(bx_, by_ - 10), (bx_, by_ - 16), (bx_ - 16, by_ - 16)]), fill=255, width=C.w(1.2))
    dr_br.arc([(bx_ - 26) * C.k, (by_ - 26) * C.k, (bx_ - 6) * C.k, (by_ - 6) * C.k], 270, 360, fill=255, width=C.w(1.0))
    piers, brass = C.done(piers), C.done(brass)
    glass_im, dr_g = C.new()
    for (x, y) in lamps + [DOOR_LAMP, BRACKET]:
        s_ = 1.0 if (x, y) in lamps else 0.85
        dr_g.rectangle([(x - 5.6 * s_) * C.k, (y - 8 * s_) * C.k, (x + 5.6 * s_) * C.k, (y + 7 * s_) * C.k], fill=255)
    glass = C.done(glass_im)
    px = over(px, piers, smooth_stone * 1.2)
    pier_edge = np.clip(piers - np.roll(piers, -int(1.0 * S), 1), 0, 1)
    px = screen(px, hexc("#FFC880") * (pier_edge * lit * 0.4)[..., None])
    # the lanterns' glass panes, lit gold from within, then the brass cage over them
    px = screen(px, hexc("#FFC870") * (glass * 0.55)[..., None])
    px = over(px, brass, hexc("#3A2412") * 0.9 + hexc("#E0A050") * 0.12)
    for (lx, ly, k) in lights:
        d2 = (X - lx) ** 2 + (Y - ly) ** 2
        fire += k * (np.exp(-d2 / (2 * 2.6 ** 2)) * 0.95 + np.exp(-d2 / (2 * 7.5 ** 2)) * 0.30)
        halo += k * (np.exp(-d2 / (2 * 11.0 ** 2)) * 0.16 + np.exp(-d2 / (2 * 30.0 ** 2)) * 0.05)
    # the flame is gold; the glow it throws into the night air is a rose-lilac haze (the jewel's own light)
    px = screen(px, hexc("#FFD48A") * np.clip(fire, 0, 1.2)[..., None])
    px = screen(px, hexc("#E6B4DC") * np.clip(halo, 0, 1.0)[..., None])
    # dust in the lamplight
    motes = smooth(0.80, 0.95, fbm(H, W, 1.6 * S, 2, 61)) * np.clip(light - 0.3, 0, 1)
    px = screen(px, hexc("#FFD8A0") * (motes * 0.10)[..., None])

    # soft strokes over the stone for a painted surface
    st = strokes(px, wall * (1 - opening), S, lambda x, y: 0.0 + 0.05 * math.sin(y / 23), int(9000 * S * S),
                 length=(3, 7), width=(1.2, 2.2), jitter=0.035, hue_jitter=0.005, seed=29)
    px = px * 0.65 + st * 0.35

    # the ceiling: luma eased under 0.45 everywhere but the moon's face and the flames (a soft knee, so no edge shows)
    Yl = px @ RL.LUM
    soft = RL.soft_ceiling(Yl, 0.45, 0.84)
    keep = smooth(mr + 3, mr - 1, d_moon)
    target = soft * (1 - keep) + Yl * keep
    px = px * (target / np.maximum(Yl, 1e-4))[..., None]
    px = RL.vignette(px, 0.22)
    return RL.grain(px, 0.008, seed=57)


def _lantern(dr, C, x, y, s=1.0):
    """A brass Ul'dah lantern round a flame at (x, y): a six-sided cage, an onion roof and finial, a stem below."""
    k = C.k
    w = 6.0 * s
    dr.line([((x - w) * k, (y - 8 * s) * k), ((x - w) * k, (y + 8 * s) * k)], fill=255, width=C.w(1.1 * s))
    dr.line([((x + w) * k, (y - 8 * s) * k), ((x + w) * k, (y + 8 * s) * k)], fill=255, width=C.w(1.1 * s))
    dr.line([(x * k, (y - 8 * s) * k), (x * k, (y - 5 * s) * k)], fill=255, width=C.w(0.6 * s))
    dr.rectangle([(x - w - 1) * k, (y + 7 * s) * k, (x + w + 1) * k, (y + 10 * s) * k], fill=255)
    dr.rectangle([(x - 1.2 * s) * k, (y + 10 * s) * k, (x + 1.2 * s) * k, (y + 18 * s) * k], fill=255)
    dr.polygon([((x - w - 1.5) * k, (y - 8 * s) * k), ((x + w + 1.5) * k, (y - 8 * s) * k),
                ((x + 3 * s) * k, (y - 14 * s) * k), (x * k, (y - 19 * s) * k), ((x - 3 * s) * k, (y - 14 * s) * k)],
               fill=255)
    dr.line([(x * k, (y - 19 * s) * k), (x * k, (y - 24 * s) * k)], fill=255, width=C.w(0.9 * s))
