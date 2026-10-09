"""Our own painting for 6-2 "The Wheel of the Twelve": the Twelve's signs as one great constellation.

The heavens Urianger reads. Round the night sky turns the old star atlas's zodiac wheel of Eorzea: a gilt band, ruled
and ticked like an astrolabe's rim, tipped back a little as a wheel lying in the sky, and set in it the twelve signs of
the Twelve, one for each moon of the year, in the calendar's order (the first Astral Moon at the top left, turning
clockwise): Halone's spear and shield, Menphina's crescent, Thaliak's ewer, Nymeia's spindle, Llymlaen's wave, Oschon's
arrow, Byregot's spire, Rhalgr's falling bolt, Azeyma's balance, Nald'thal's twin coins, Nophica's bole and Althyk's
hourglass. Each sign is four stars joined by faint gilt lines, as the atlas draws a constellation, and behind them the
atlas's own engraving of the emblem, faint and pale in its element's tint. At the wheel's hub burns the pole star in a
twelve-rayed rosette. Behind it all the Milky Way crosses the sky from the lower left, and a rose nebula glows in the
wheel's heart; below, a dark line of hills and a seer's small domed observatory with one lit window.

FEATURES (board units): the wheel (its rim, inner and outer rules), the hub, each sign's centre, stars and lines, a
box round each sign (for the runtime's twinkle mask), the star sky, the Milky Way and the horizon.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

C = (400.0, 338.0)            # the hub
A, B = 258.0, 146.0           # the rim's half-axes (the wheel tipped back a little)
OUT = (42.0, 38.0)            # the band's outer rule: A + 42, B + 38
IN = (58.0, 52.0)             # its inner rule, where the dividers between the moons end: A - 58, B - 52
PIVOT = (400.0, 87.0)
K = 0.94                      # the signs' scale: their figures are drawn at +-36 units and set at K of it

ICE, WATER, WIND, BOLT, FIRE, EARTH = "#BFE4FF", "#86C8FF", "#A6F0D0", "#D6BCFF", "#FFC09A", "#EAD8A0"

# The twelve signs in the calendar's order, clockwise from the top left: name, deity, angle on the rim (degrees, 0 along
# +x, turning toward +y), element tint, nudge of the centre (board units), stars (dx, dy, magnitude 1 brightest), lines
# (index pairs), and the atlas's engraving: ("p", pts) a polyline, ("pc", pts) closed, ("c", x, y, r) a circle,
# ("a", x, y, r, from, to) an arc (degrees), ("e", x, y, rx, ry) an ellipse.
SIGNS = [
    ("spear", "Halone", -105, ICE, (-10, 0),
     [(18, -22, 1), (-6, 4, 2), (-28, 28, 3), (24, 22, 2)], [(0, 1), (1, 2), (1, 3)],
     [("p", [(-34, 34), (24, -24)]), ("pc", [(6, -6), (12, -20), (28, -30), (20, -12)]),
      ("c", 24, 22, 15), ("c", 24, 22, 4), ("p", [(-30, 24), (-24, 30)])]),
    ("crescent", "Menphina", -75, ICE, (10, 0),
     [(-2, -28, 2), (-28, -6, 1), (-16, 26, 2), (18, 28, 3)], [(0, 1), (1, 2), (2, 3)],
     [("a", 0, 0, 33, 75, 285), ("a", 10, -2, 25, 95, 265), ("p", [(12, -8), (16, -14)]), ("p", [(16, 2), (22, 2)])]),
    ("ewer", "Thaliak", -45, WATER, (0, 0),
     [(28, -20, 2), (-6, -24, 2), (-28, 4, 3), (2, 26, 1)], [(1, 0), (1, 2), (2, 3), (3, 0)],
     [("pc", [(-8, -22), (-10, -12), (-20, 0), (-18, 16), (-8, 26), (10, 26), (20, 14), (18, -2), (6, -12), (4, -22)]),
      ("p", [(16, -4), (24, -12), (32, -22)]), ("p", [(-14, -14), (-30, -8), (-30, 8), (-19, 10)]),
      ("p", [(-12, -22), (8, -22)]), ("p", [(-16, 8), (16, 8)])]),
    ("spindle", "Nymeia", -15, WATER, (0, 0),
     [(0, -28, 2), (-18, 0, 3), (18, 0, 3), (0, 28, 1)], [(0, 1), (0, 2), (1, 3), (2, 3)],
     [("p", [(0, -36), (0, 36)]), ("e", 0, 0, 22, 8), ("p", [(0, -32), (12, -36), (24, -32), (32, -22), (36, -6), (34, 10)]),
      ("p", [(-6, -20), (6, -16), (-6, -12), (6, -8)])]),
    ("wave", "Llymlaen", 15, WIND, (0, 0),
     [(-28, 10, 2), (-6, -16, 1), (16, 10, 3), (30, -22, 2)], [(0, 1), (1, 2), (2, 3)],
     [("p", [(-36, 16), (-22, 2), (-10, -16), (2, -22), (12, -16), (8, -6), (0, -10)]),
      ("p", [(4, 16), (16, 6), (26, -10), (36, -26)]), ("p", [(-36, 28), (-12, 24), (12, 28), (36, 22)])]),
    ("arrow", "Oschon", 45, WIND, (0, -4),
     [(-26, 24, 3), (0, 0, 2), (26, -22, 1), (-34, -10, 3)], [(0, 1), (1, 2), (0, 3)],
     [("p", [(-30, 28), (28, -26)]), ("pc", [(30, -28), (14, -24), (26, -12)]),
      ("p", [(-26, 24), (-34, 14)]), ("p", [(-20, 18), (-28, 8)]), ("p", [(-26, 24), (-16, 32)]),
      ("p", [(-20, 18), (-10, 26)])]),
    ("spire", "Byregot", 75, BOLT, (0, -4),
     [(0, -30, 1), (0, 4, 3), (-24, 28, 2), (24, 28, 2)], [(0, 1), (1, 2), (1, 3), (2, 3)],
     [("pc", [(0, -38), (-8, -14), (-10, 10), (-28, 32), (28, 32), (10, 10), (8, -14)]),
      ("p", [(-4, -6), (4, -6)]), ("p", [(-6, 14), (6, 14)]), ("p", [(0, -38), (0, -46)])]),
    ("bolt", "Rhalgr", 105, BOLT, (0, -4),
     [(-16, -28, 2), (14, -10, 1), (-14, 12, 3), (16, 30, 2)], [(0, 1), (1, 2), (2, 3)],
     [("pc", [(-22, -36), (20, -12), (4, -6), (22, 36), (-20, 12), (-4, 4)]),
      ("p", [(-30, -30), (-22, -36), (-28, -42)])]),
    ("balance", "Azeyma", 135, FIRE, (0, -2),
     [(0, -28, 2), (-28, 4, 1), (28, 4, 1), (0, 28, 3)], [(0, 1), (0, 2), (0, 3)],
     [("p", [(-30, -18), (30, -18)]), ("p", [(0, -24), (0, 30)]), ("p", [(-12, 32), (12, 32)]),
      ("p", [(-30, -18), (-38, 4)]), ("p", [(-30, -18), (-20, 4)]), ("p", [(30, -18), (20, 4)]),
      ("p", [(30, -18), (38, 4)]), ("a", -29, 4, 10, 0, 180), ("a", 29, 4, 10, 0, 180), ("c", 0, -30, 5)]),
    ("coins", "Nald'thal", 165, FIRE, (0, 0),
     [(-18, -22, 2), (-18, 14, 3), (18, -14, 3), (18, 22, 2)], [(0, 1), (2, 3), (1, 2), (0, 2)],
     [("c", -12, -4, 19), ("c", 12, 4, 19), ("c", -12, -4, 13), ("c", 12, 4, 13)]),
    ("bole", "Nophica", 195, EARTH, (0, 0),
     [(0, 30, 2), (0, -4, 3), (-26, -26, 1), (26, -26, 2)], [(0, 1), (1, 2), (1, 3)],
     [("p", [(0, 34), (0, -4)]), ("p", [(0, -4), (-26, -26)]), ("p", [(0, -4), (26, -26)]), ("p", [(0, 6), (-14, -8)]),
      ("p", [(0, 2), (14, -12)]), ("a", 0, -12, 32, 190, 350), ("p", [(0, 34), (-12, 38)]), ("p", [(0, 34), (12, 38)])]),
    ("hourglass", "Althyk", 225, EARTH, (4, 0),
     [(-18, -26, 2), (18, -26, 3), (-18, 26, 3), (18, 26, 2)], [(0, 1), (2, 3), (0, 3), (1, 2)],
     [("p", [(-24, -32), (24, -32)]), ("p", [(-24, 32), (24, 32)]),
      ("p", [(-16, -30), (-14, -16), (-3, -2), (-14, 14), (-16, 30)]), ("p", [(16, -30), (14, -16), (3, -2), (14, 14), (16, 30)]),
      ("p", [(-8, 26), (0, 18), (8, 26)])]),
]


def rim(a_deg, da=0.0, db=0.0):
    a = math.radians(a_deg)
    return (C[0] + (A + da) * math.cos(a), C[1] + (B + db) * math.sin(a))


def _arc_angles(n=12, offset=0.5, fine=4096):
    """The angles (degrees) of n points evenly spaced by arc length round the rim, clockwise from the top, the first
    `offset` of a step before it."""
    a = np.linspace(-90.0, 270.0, fine + 1)
    pts = np.array([rim(t) for t in a])
    s = np.concatenate([[0.0], np.cumsum(np.hypot(*np.diff(pts, axis=0).T))])
    step = s[-1] / n
    return [float(np.interp(((k - offset) * step) % s[-1], s, a)) for k in range(n)]


SIGN_ANGLES = _arc_angles(12, 0.5)            # the signs, the first (Halone) just before the top
DIVIDER_ANGLES = _arc_angles(12, 0.0)         # the dividers between the moons, the first at the top


def sign_centre(s):
    x, y = rim(SIGN_ANGLES[SIGNS.index(s)])
    return (x + s[4][0], y + s[4][1])


def sign_stars(s):
    cx, cy = sign_centre(s)
    return [(cx + dx * K, cy + dy * K, m) for (dx, dy, m) in s[5]]


def ellipse_pts(da=0.0, db=0.0, n=96):
    return [rim(360.0 * k / n, da, db) for k in range(n)]


HORIZON = [(0, 534), (70, 528), (150, 532), (240, 538), (330, 534), (420, 538), (520, 532), (610, 528), (690, 524),
           (760, 530), (800, 532)]
OBSERVATORY = (676, 524)      # the seer's observatory on the right-hand hill: its foot's centre
MILKY = [(40, 600), (190, 470), (330, 372), (470, 290), (620, 190), (800, 70)]

FEATURES = {
    "wheel": {"points": [[round(x, 1), round(y, 1)] for (x, y) in ellipse_pts()]},
    "wheel outer": {"points": [[round(x, 1), round(y, 1)] for (x, y) in ellipse_pts(OUT[0], OUT[1], 48)]},
    "wheel inner": {"points": [[round(x, 1), round(y, 1)] for (x, y) in ellipse_pts(-IN[0], -IN[1], 48)]},
    "hub": list(C),
    "dividers": [[round(c, 1) for c in rim(a, -IN[0], -IN[1])] for a in DIVIDER_ANGLES],
    "signs": {s[1]: [round(c, 1) for c in sign_centre(s)] for s in SIGNS},
    "sign stars": {s[1]: [[round(x, 1), round(y, 1), m] for (x, y, m) in sign_stars(s)] for s in SIGNS},
    "sign lines": {s[1]: [list(p) for p in s[6]] for s in SIGNS},
    **{f"sign {s[1]}": [[round(sign_centre(s)[0] + dx, 1), round(sign_centre(s)[1] + dy, 1)]
                         for (dx, dy) in ((-40, -40), (40, -40), (40, 40), (-40, 40))] for s in SIGNS},
    "star sky": [[70, 30], [730, 30], [730, 520], [70, 520]],
    "milky way": {"points": [list(p) for p in MILKY]},
    "horizon": {"points": [list(p) for p in HORIZON]},
}


# ------------------------------------------------------------------------------------------------ drawing helpers
def _canvas(W, H, ss):
    im = Image.new("L", (W * ss, H * ss), 0)
    return im, ImageDraw.Draw(im)


def _done(im, W, H):
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def _poly_line(dr, pts, S, ss, width, closed=False):
    q = [(x * S * ss, y * S * ss) for (x, y) in pts]
    if closed:
        q = q + [q[0]]
    dr.line(q, fill=255, width=max(1, int(round(width * S * ss))), joint="curve")


def _arc_pts(x, y, r, a0, a1, n=40, rx=None, ry=None):
    rx, ry = rx or r, ry or r
    return [(x + rx * math.cos(math.radians(a0 + (a1 - a0) * k / n)), y + ry * math.sin(math.radians(a0 + (a1 - a0) * k / n)))
            for k in range(n + 1)]


def engraving(W, H, S, shapes, origin, width=0.9, ss=3):
    """The atlas's engraving of one emblem: its shapes about `origin` (at the signs' scale K), in fine lines."""
    im, dr = _canvas(W, H, ss)
    ox, oy = origin

    def sc(sh):
        k = sh[0]
        if k in ("p", "pc"):
            return (k, [(x * K, y * K) for (x, y) in sh[1]])
        if k in ("c", "a"):
            return (k, sh[1] * K, sh[2] * K, sh[3] * K, *sh[4:])
        return (k, sh[1] * K, sh[2] * K, sh[3] * K, sh[4] * K)
    for sh in map(sc, shapes):
        k = sh[0]
        if k in ("p", "pc"):
            _poly_line(dr, [(ox + x, oy + y) for (x, y) in sh[1]], S, ss, width, closed=k == "pc")
        elif k == "c":
            _poly_line(dr, _arc_pts(ox + sh[1], oy + sh[2], sh[3], 0, 360, 48), S, ss, width)
        elif k == "a":
            _poly_line(dr, _arc_pts(ox + sh[1], oy + sh[2], sh[3], sh[4], sh[5], 32), S, ss, width)
        elif k == "e":
            _poly_line(dr, _arc_pts(ox + sh[1], oy + sh[2], 0, 0, 360, 48, rx=sh[3], ry=sh[4]), S, ss, width)
    return _done(im, W, H)


def segments(W, H, S, segs, width, shorten=8.0, ss=3):
    """Straight gilt lines that stop short of each star, as an atlas draws them."""
    im, dr = _canvas(W, H, ss)
    for (x0, y0), (x1, y1) in segs:
        L_ = math.hypot(x1 - x0, y1 - y0)
        ux, uy = (x1 - x0) / L_, (y1 - y0) / L_
        dr.line([((x0 + ux * shorten) * S * ss, (y0 + uy * shorten) * S * ss),
                 ((x1 - ux * shorten) * S * ss, (y1 - uy * shorten) * S * ss)], fill=255,
                width=max(1, int(round(width * S * ss))))
    return _done(im, W, H)


def point_star(px, X, Y, x, y, mag, col, k=1.0):
    d2 = (X - x) ** 2 + (Y - y) ** 2
    core = {1: 1.7, 2: 1.3, 3: 1.05}[mag]
    a = {1: 1.0, 2: 0.85, 3: 0.7}[mag] * k
    g = np.exp(-d2 / (2 * core * core)) * a + np.exp(-d2 / (2 * (core * 5) ** 2)) * a * 0.22
    # a faint four-point glint on the brightest
    if mag == 1:
        g = g + (np.exp(-((X - x) / 0.7) ** 2 - ((Y - y) / 7) ** 2) + np.exp(-((Y - y) / 0.7) ** 2 - ((X - x) / 7) ** 2)) * 0.25 * k
    return screen(px, hexc(col) * g[..., None])


def _dist_to(X, Y, pts):
    P = np.asarray(pts, np.float32)
    dist = np.full(X.shape, 1e9, np.float32)
    for k in range(len(P) - 1):
        ax, ay = P[k]
        bx, by = P[k + 1]
        t = np.clip(((X - ax) * (bx - ax) + (Y - ay) * (by - ay)) / max((bx - ax) ** 2 + (by - ay) ** 2, 1e-6), 0, 1)
        dist = np.minimum(dist, np.sqrt((X - ax - (bx - ax) * t) ** 2 + (Y - ay - (by - ay) * t) ** 2))
    return dist


# ------------------------------------------------------------------------------------------------ the painting
def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    # the sky: deep indigo, lighter toward the hills; the moon's light from beyond the upper left
    sky = ramp(np.clip(Y / 540, 0, 1), [(0, "#040A1C"), (0.35, "#081838"), (0.75, "#0C1E44"), (1.0, "#122850")])
    d_moon = np.sqrt((X + 90) ** 2 + (Y + 110) ** 2)
    sky = screen(sky, hexc("#BEC4F0") * (np.exp(-(d_moon / 360) ** 2) * 0.13 + np.exp(-(d_moon / 900) ** 2) * 0.05)[..., None])
    # the Milky Way, from the lower left up behind the hub to the upper right: mottled, its dust lanes darkening its light
    P = RL.catmull(MILKY, 16)
    dist = _dist_to(X, Y, P)
    band = np.exp(-(dist / 70) ** 2)
    mott = fbm(H, W, 26 * S, 5, 3)
    dust = smooth(0.47, 0.62, fbm(H, W, 15 * S, 4, 9)) * np.exp(-(dist / 26) ** 2)
    glow = band * (0.4 + 0.6 * mott) * (1 - blur(dust, 2.5 * S) * 0.8) * smooth(560, 500, Y)
    sky = screen(sky, hexc("#8AA8DC") * (glow * 0.22)[..., None])
    # the rose nebula in the wheel's heart, and a wisp of it along the Milky Way
    neb_n = fbm(H, W, 40 * S, 5, 21)
    heart = np.exp(-(((X - C[0]) / 150) ** 2 + ((Y - C[1]) / 92) ** 2))
    wisp = np.exp(-(dist / 46) ** 2) * (np.exp(-(((X - 190) / 120) ** 2 + ((Y - 470) / 60) ** 2))
                                         + np.exp(-(((X - 610) / 120) ** 2 + ((Y - 196) / 60) ** 2)))
    neb = np.clip((heart * 0.9 + wisp * 0.7) * smooth(0.35, 0.75, neb_n), 0, 1)
    sky = screen(sky, hexc("#C8508A") * (neb * 0.20)[..., None])
    sky = screen(sky, hexc("#F0A0D8") * (neb ** 2 * smooth(0.6, 0.85, fbm(H, W, 10 * S, 3, 23)) * 0.10)[..., None])
    sky = strokes(sky, (Y < 540).astype(np.float32), S, lambda x, y: -0.62 + 0.2 * math.sin(x / 80), int(6500 * S * S),
                  length=(8, 18), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=27)
    # the field of stars, thicker along the Milky Way, thinner in the band where the signs stand
    rimd = np.abs(np.sqrt(((X - C[0]) / A) ** 2 + ((Y - C[1]) / B) ** 2) - 1.0)

    def avoid(x, y):
        xi, yi = min(W - 1, int(x)), min(H - 1, int(y))
        return (0.35 + 0.65 * float(np.exp(-(dist[yi, xi] / 90) ** 2))) * (0.45 + 0.55 * float(smooth(0.08, 0.22, rimd[yi, xi])))
    px = RL.stars(sky, S, 1300, 27, (0, 0, W, 530 * S), avoid=avoid, bright=0.5)
    # ---- the wheel: the band's faint lift, its rules, the ticks of the days and the twelve dividers
    near_launch = smooth(100, 140, np.sqrt((X - PIVOT[0]) ** 2 + (Y - PIVOT[1]) ** 2))
    r_out = np.sqrt(((X - C[0]) / (A + OUT[0])) ** 2 + ((Y - C[1]) / (B + OUT[1])) ** 2)
    r_in = np.sqrt(((X - C[0]) / (A - OUT[0])) ** 2 + ((Y - C[1]) / (B - OUT[1])) ** 2)
    inband = smooth(1.02, 0.98, r_out) * smooth(0.98, 1.02, r_in)
    px = screen(px, hexc("#4A4AA0") * (inband * 0.055 * near_launch)[..., None])
    im, dr = _canvas(W, H, 3)
    for (da, db, w) in ((OUT[0], OUT[1], 1.0), (OUT[0] - 5, OUT[1] - 5, 0.6), (-IN[0], -IN[1], 1.0),
                        (-IN[0] + 5, -IN[1] + 5, 0.6)):
        _poly_line(dr, ellipse_pts(da, db, 180), S, 3, w, closed=True)
    for k in range(120):                     # a tick for every three days, along the inner rule's inside
        a = 3.0 * k
        _poly_line(dr, [rim(a, -IN[0] + 5, -IN[1] + 5), rim(a, -IN[0] + (11 if k % 5 == 0 else 8),
                                                              -IN[1] + (11 if k % 5 == 0 else 8))], S, 3, 0.5)
    for a in DIVIDER_ANGLES:                 # the dividers between the moons, across the band
        _poly_line(dr, [rim(a, -IN[0] + 5, -IN[1] + 5), rim(a, OUT[0] - 5, OUT[1] - 5)], S, 3, 0.7)
    rules = _done(im, W, H)
    px = screen(px, hexc("#D9BE82") * (rules * 0.34 * near_launch)[..., None])
    # a small four-point ornament where each divider meets the inner rule
    for a in DIVIDER_ANGLES:
        x, y = rim(a, -IN[0], -IN[1])
        orn = np.exp(-((X - x) / 0.8) ** 2 - ((Y - y) / 5) ** 2) + np.exp(-((Y - y) / 0.8) ** 2 - ((X - x) / 5) ** 2)
        px = screen(px, hexc("#E8D6A0") * (orn * 0.22 * near_launch)[..., None])
    # ---- the signs: the atlas's engraving in its element's tint, the gilt lines, the stars
    for s in SIGNS:
        cx, cy = sign_centre(s)
        eng = engraving(W, H, S, s[7], (cx, cy), width=1.0)
        halo = blur(eng, 3.0 * S)
        px = screen(px, hexc(s[3]) * (eng * 0.30 + halo * 0.07)[..., None])
        st = sign_stars(s)
        segs = [((st[i][0], st[i][1]), (st[j][0], st[j][1])) for (i, j) in s[6]]
        px = screen(px, hexc("#D9BE82") * (segments(W, H, S, segs, 1.1) * 0.42)[..., None])
        # the sign's own faint glow, so each reads as a cluster at the small window
        g = np.exp(-(((X - cx) / 40) ** 2 + ((Y - cy) / 40) ** 2))
        px = screen(px, hexc(s[3]) * (g * 0.035)[..., None])
        for (x, y, m) in st:
            px = point_star(px, X, Y, x, y, m, "#EEF4FF")
    # ---- the hub: the pole star in a twelve-rayed rosette
    im, dr = _canvas(W, H, 3)
    for k in range(24):
        a = math.radians(-90 + 15 * k)
        r0, r1 = 16, (50 if k % 2 == 0 else 30)
        _poly_line(dr, [(C[0] + r0 * math.cos(a), C[1] + r0 * math.sin(a)), (C[0] + r1 * math.cos(a), C[1] + r1 * math.sin(a))],
                   S, 3, 0.7 if k % 2 == 0 else 0.5)
    _poly_line(dr, _arc_pts(C[0], C[1], 12, 0, 360, 48), S, 3, 0.7)
    _poly_line(dr, _arc_pts(C[0], C[1], 58, 0, 360, 96), S, 3, 0.5)
    rose = _done(im, W, H)
    px = screen(px, hexc("#E8D6A0") * (rose * 0.26)[..., None])
    px = screen(px, hexc("#F4D0F0") * (np.exp(-(((X - C[0]) ** 2 + (Y - C[1]) ** 2) / 26 ** 2)) * 0.10)[..., None])
    px = point_star(px, X, Y, C[0], C[1], 1, "#FFF6F0", k=1.0)
    # ---- the land: a dark line of hills, lit along their crests, and the seer's observatory
    xs = np.array([p[0] for p in HORIZON], np.float32)
    top = np.interp(X[0], xs, np.array([p[1] for p in HORIZON], np.float32)) + (fbm(1, W, 18 * S, 3, 5)[0] - 0.5) * 6
    land = np.clip((Y - top[None, :]) * S + 0.5, 0, 1)
    depth = np.clip((Y - top[None, :]) / 60, 0, 1)
    ground = ramp(depth, [(0, "#28224C"), (0.25, "#12102C"), (1, "#070618")])
    ground = screen(ground, hexc("#9C96D2") * (np.exp(-((Y - top[None, :] - 1.0) / 1.3) ** 2) * 0.22)[..., None])
    px = px * (1 - land[..., None]) + ground * land[..., None]
    ox, oy = OBSERVATORY
    im, dr = _canvas(W, H, 3)
    s3 = S * 3
    dr.rectangle([(ox - 16) * s3, (oy - 22) * s3, (ox + 16) * s3, (oy + 6) * s3], fill=255)
    dr.pieslice([(ox - 14) * s3, (oy - 36) * s3, (ox + 14) * s3, (oy - 8) * s3], 180, 360, fill=255)
    dr.rectangle([(ox + 16) * s3, (oy - 12) * s3, (ox + 30) * s3, (oy + 6) * s3], fill=255)
    dr.polygon([(ox + 2 * 1) * s3, (oy - 30) * s3, (ox + 18) * s3, (oy - 44) * s3, (ox + 20) * s3, (oy - 42) * s3,
                (ox + 5) * s3, (oy - 27) * s3], fill=255)          # the telescope through the dome's slit
    house = _done(im, W, H)
    px = px * (1 - house[..., None]) + hexc("#08071A") * house[..., None]
    rimh = np.clip(house - np.roll(np.roll(house, int(S), 1), int(S), 0), 0, 1)
    px = screen(px, hexc("#A69EDA") * (rimh * 0.35)[..., None])
    for (wx, wy) in ((ox - 6, oy - 10), (ox + 23, oy - 4)):
        win = np.exp(-(((X - wx) / 2.2) ** 2 + ((Y - wy) / 3.0) ** 2))
        px = screen(px, hexc("#FFB45E") * (win * 0.7 + np.exp(-(((X - wx) / 9) ** 2 + ((Y - wy) / 9) ** 2)) * 0.10)[..., None])
    Yl = px @ RL.LUM
    px = px * np.where(Yl > 0.42, 0.42 / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.24)
    return RL.grain(px, 0.008, seed=27)
