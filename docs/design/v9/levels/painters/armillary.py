"""Our own painting for 6-1 "The Armillary": a celestial globe of light, as rings and spokes.

The heavens Urianger reads. On a dark terrace under a violet night stands a great armillary sphere: its brass frame
(the fixed meridian, a full ring seen face on, and the broad horizon ring, ruled and ticked like a calendar, crossing
it at the sphere's waist) holds a cage of rings of pale silver light turning about the tilted polar axis: the equator,
the two tropics, the two polar circles, the two colures through the poles, and the ecliptic, a wider band ticked for
the days. At its heart a small golden globe burns and throws ten soft spokes of light out through the rings to the
moons at their tips (two of them level, along the horizon ring's plane). The axis
runs out past the poles as a brass rod, its north end tipped with a star finial. Below, the sphere stands on a fluted
column and a three-footed base on the terrace's parapet; behind, the Milky Way crosses the sky from the lower left, a
violet nebula drifts, the last amber of the dusk glows low behind banks of cloud over the far hills either side, their
tops lit, the full moon (with its seas) hangs at the upper left (well clear of the launcher) and two bright planets at
the right.

MR 6 round 1 (game designer M1, m3, n3): the cage was teal on violet, 6-5's Moon Gate picture; it is now the brass
instrument the painting always drew, its rings silver (not gold: gold under the oranges is on their side of a
protanope's axis) and its heart gold, with the dusk's amber as the second jewel's other home. The moon had been
clipped flat by the cap; its face now sits under it with the kit's maria. The spokes reach their moons.

Geometry: the rings are true circles in 3D, seen from a little above (VIEW_EL) and projected orthographically, so the
painting and the layout (which reads FEATURES) share one set of curves. The parts of a ring behind the sphere's centre
are drawn dimmer than the parts in front.

FEATURES (board units): the meridian (centre and radius), the horizon ring, the equator, the ecliptic, the two tropics,
the colures (as polylines; the equator's and ecliptic's depths alongside), the poles, the axis rods, the heart, the spokes, the stand, the
star sky and the horizon.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from paint_moon import maria
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

C = (400.0, 330.0)            # the sphere's centre on the board
R = 150.0                     # the celestial rings' radius
RM = 172.0                    # the fixed meridian's radius (seen face on: a circle)
RH = 204.0                    # the horizon ring's radius (seen from a little above: a flat ellipse)
VIEW_EL = math.radians(14.0)  # how far above the sphere's waist we stand
TILT = math.radians(22.0)     # the polar axis leans right by this in the picture
NOD = math.radians(18.0)      # and toward us by this
OBLIQ = math.radians(23.44)   # the ecliptic's tilt to the equator
PIVOT = (400.0, 87.0)
MOON = (138.0, 104.0, 30.0)   # the full moon, upper left (x, y, r): never behind the launcher
PLANETS = [(664.0, 128.0, 1.0), (618.0, 96.0, 0.7)]
PARAPET = 538.0               # the terrace's parapet top (y)


# ------------------------------------------------------------------------------------------------ the geometry
def _unit(v):
    v = np.asarray(v, np.float64)
    return v / np.linalg.norm(v)


AXIS = _unit((math.sin(TILT), math.cos(TILT) * math.cos(NOD), math.cos(TILT) * math.sin(NOD)))
_E1 = _unit(np.cross((0.0, 0.0, 1.0), AXIS))           # the equinoxes' line (in the equator)
_E2 = _unit(np.cross(AXIS, _E1))                        # the solstices' line


def _rot(v, k, a):
    """v rotated by angle a about the unit vector k (Rodrigues)."""
    v, k = np.asarray(v, np.float64), np.asarray(k, np.float64)
    return v * math.cos(a) + np.cross(k, v) * math.sin(a) + k * np.dot(k, v) * (1 - math.cos(a))


ECL_N = _unit(_rot(AXIS, _E1, OBLIQ))                    # the ecliptic's pole


def project(p):
    """World (x right, y up, z toward us; sphere centre at 0) to board (x, y) and depth (toward us)."""
    x, y, z = p
    up = y * math.cos(VIEW_EL) - z * math.sin(VIEW_EL)
    depth = y * math.sin(VIEW_EL) + z * math.cos(VIEW_EL)
    return (C[0] + x, C[1] - up, depth)


def circle3d(centre, normal, r, n=360):
    n_ = _unit(normal)
    a = _unit(np.cross(n_, (0.0, 0.0, 1.0)) if abs(n_[2]) < 0.9 else np.cross(n_, (1.0, 0.0, 0.0)))
    b = np.cross(n_, a)
    c = np.asarray(centre, np.float64)
    return [project(c + r * (math.cos(t) * a + math.sin(t) * b)) for t in np.linspace(0, 2 * math.pi, n + 1)]


def _ellipse_from_view(r):
    """The horizon ring: horizontal, so seen from VIEW_EL above it is a flat ellipse."""
    return [project((r * math.cos(t), 0.0, r * math.sin(t))) for t in np.linspace(0, 2 * math.pi, 361)]


EQUATOR = circle3d((0, 0, 0), AXIS, R)
ECLIPTIC = circle3d((0, 0, 0), ECL_N, R)
TROPICS = [circle3d(AXIS * R * math.sin(OBLIQ) * s, AXIS, R * math.cos(OBLIQ)) for s in (1, -1)]
POLARS = [circle3d(AXIS * R * math.cos(OBLIQ) * s, AXIS, R * math.sin(OBLIQ)) for s in (1, -1)]
COLURES = [circle3d((0, 0, 0), _E1, R), circle3d((0, 0, 0), _E2, R)]
HORIZON_RING = _ellipse_from_view(RH)
HORIZON_INNER = _ellipse_from_view(RH - 9)
MERIDIAN = [(C[0] + RM * math.cos(t), C[1] + RM * math.sin(t), 0.0) for t in np.linspace(0, 2 * math.pi, 361)]
NORTH, SOUTH = project(AXIS * R), project(-AXIS * R)
_d = np.array([NORTH[0] - C[0], NORTH[1] - C[1]])
AXIS_DIR = _d / np.linalg.norm(_d)                       # the axis's direction in the picture (toward north)
ROD_N = [(NORTH[0], NORTH[1]), (C[0] + AXIS_DIR[0] * (RM + 26), C[1] + AXIS_DIR[1] * (RM + 26))]
ROD_S = [(SOUTH[0], SOUTH[1]), (C[0] - AXIS_DIR[0] * RM, C[1] - AXIS_DIR[1] * RM)]
HEART = (C[0], C[1], 20.0)
# ten spokes of light from the heart, through the rings and out past the meridian to the moons at their tips (the
# layout's spoke tips and level-spoke moons sit on them: game designer MR 6 round 1, n3) (degrees, 0 along +x, +y down)
SPOKE_ANGLES = [180.0, -157.5, -112.5, -67.5, -22.5, 0.0, 22.5, 67.5, 112.5, 157.5]
SPOKES = [[(C[0] + 30 * math.cos(math.radians(a)), C[1] + 30 * math.sin(math.radians(a))),
           (C[0] + (RM + 160) * math.cos(math.radians(a)), C[1] + (RM + 160) * math.sin(math.radians(a)))]
          for a in SPOKE_ANGLES]
STAND_TOP = C[1] + RM
STAND = {"column": [(C[0], STAND_TOP), (C[0], PARAPET - 14)],
         "feet": [(C[0] - 62, PARAPET + 4), (C[0], PARAPET + 8), (C[0] + 62, PARAPET + 4)]}
HORIZON = [(0, 506), (90, 500), (180, 504), (270, 510), (530, 510), (620, 502), (710, 498), (800, 504)]
MILKY = [(30, 590), (170, 470), (300, 380), (470, 270), (620, 170), (800, 60)]


def _pts(curve, nd=1):
    return [[round(p[0], nd), round(p[1], nd)] for p in curve]


def _depth(curve, nd=1):
    return [round(p[2], nd) for p in curve]


FEATURES = {
    "meridian": {"circle": [C[0], C[1], RM], "from": 180, "sweep": 180},
    "horizon ring": {"points": _pts(HORIZON_RING[::4])},
    "horizon ring depth": _depth(HORIZON_RING[::4]),
    "equator": {"points": _pts(EQUATOR[::4])},
    "equator depth": _depth(EQUATOR[::4]),
    "ecliptic": {"points": _pts(ECLIPTIC[::4])},
    "ecliptic depth": _depth(ECLIPTIC[::4]),
    "tropic north": {"points": _pts(TROPICS[0][::6])},
    "tropic south": {"points": _pts(TROPICS[1][::6])},
    "colure 1": {"points": _pts(COLURES[0][::6])},
    "colure 2": {"points": _pts(COLURES[1][::6])},
    "north pole": [round(NORTH[0], 1), round(NORTH[1], 1)],
    "south pole": [round(SOUTH[0], 1), round(SOUTH[1], 1)],
    "rod north": {"points": [[round(x, 1), round(y, 1)] for (x, y) in ROD_N]},
    "rod south": {"points": [[round(x, 1), round(y, 1)] for (x, y) in ROD_S]},
    "heart": [C[0], C[1]],
    "spokes": [[[round(x, 1), round(y, 1)] for (x, y) in s] for s in SPOKES],
    "stand": {"points": [[C[0], STAND_TOP], [C[0], PARAPET - 14]]},
    "feet": [[round(x, 1), round(y, 1)] for (x, y) in STAND["feet"]],
    "moon": list(MOON),
    "moon disc": [[round(MOON[0] + (MOON[2] + 10) * math.cos(t), 1), round(MOON[1] + (MOON[2] + 10) * math.sin(t), 1)]
                  for t in np.linspace(0, 2 * math.pi, 16, endpoint=False)],
    "star sky": [[70, 30], [730, 30], [730, 500], [70, 500]],
    "sphere": [[round(C[0] + (RM + 14) * math.cos(t), 1), round(C[1] + (RM + 14) * math.sin(t), 1)]
               for t in np.linspace(0, 2 * math.pi, 24, endpoint=False)],
    "horizon": {"points": [list(p) for p in HORIZON]},
}


# ------------------------------------------------------------------------------------------------ drawing helpers
def _canvas(W, H, ss):
    im = Image.new("L", (W * ss, H * ss), 0)
    return im, ImageDraw.Draw(im)


def _done(im, W, H):
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def _line(dr, pts, S, ss, width):
    q = [(x * S * ss, y * S * ss) for (x, y) in pts]
    if len(q) > 1:
        dr.line(q, fill=255, width=max(1, int(round(width * S * ss))), joint="curve")


def ring_masks(W, H, S, curve, width, ref=0.0, ss=3):
    """A ring drawn as a line: (front, back) masks, split where the ring passes behind `ref` depth."""
    fr, dfr = _canvas(W, H, ss)
    bk, dbk = _canvas(W, H, ss)
    run, front = [], None
    for p in curve:
        f = p[2] >= ref
        if front is None or f == front:
            run.append((p[0], p[1]))
        else:
            run.append((p[0], p[1]))
            _line(dfr if front else dbk, run, S, ss, width)
            run = [(p[0], p[1])]
        front = f
    _line(dfr if front else dbk, run, S, ss, width)
    return _done(fr, W, H), _done(bk, W, H)


def ticks_mask(W, H, S, curve, every, inner, outer, centre, width=0.6, major=5, ss=3):
    """Short ticks across a ring, radial from `centre` in the picture (a calendar's days)."""
    im, dr = _canvas(W, H, ss)
    for k in range(0, len(curve) - 1, every):
        x, y = curve[k][0], curve[k][1]
        dx, dy = x - centre[0], y - centre[1]
        L_ = math.hypot(dx, dy) or 1.0
        ux, uy = dx / L_, dy / L_
        o = outer * (1.6 if (k // every) % major == 0 else 1.0)
        _line(dr, [(x - ux * inner, y - uy * inner), (x + ux * o, y + uy * o)], S, ss, width)
    return _done(im, W, H)


def _dist_to(X, Y, pts):
    P = np.asarray(pts, np.float32)
    dist = np.full(X.shape, 1e9, np.float32)
    for k in range(len(P) - 1):
        ax, ay = P[k]
        bx, by = P[k + 1]
        t = np.clip(((X - ax) * (bx - ax) + (Y - ay) * (by - ay)) / max((bx - ax) ** 2 + (by - ay) ** 2, 1e-6), 0, 1)
        dist = np.minimum(dist, np.sqrt((X - ax - (bx - ax) * t) ** 2 + (Y - ay - (by - ay) * t) ** 2))
    return dist


def _glow_line(px, mask, col, k_core, k_halo, S, halo=3.0):
    return screen(px, hexc(col) * (mask * k_core + blur(mask, halo * S) * k_halo)[..., None])


def _moon(px, S, X, Y):
    """The full moon with a surface (game designer MR 6 round 1, m3): its halo from the kit's emissive moon, then a
    face held under the painting's cap (so the cap does not flatten it to one grey) with the kit's lobed maria
    (paint_moon.maria, the What's new moon's seas), a soft limb and a faint fine mottle; no craters, no terminator."""
    mx, my, mr = MOON
    px, disc = RL.moon_emissive(px, S, mx, my, mr, seed=26, face_col="#D4D0E2")
    u, v = (X - mx) / mr, (Y - my) / mr
    d = np.sqrt(u * u + v * v)
    nz = np.sqrt(np.clip(1 - d * d, 0, 1))
    H, W = X.shape
    seas = np.clip(blur(maria(u, v), 1.2 * S) * (0.80 + 0.40 * fbm(H, W, mr * S * 0.25, 3, 27)), 0, 1)
    fine = fbm(H, W, mr * S * 0.06, 3, 28) - 0.5
    face = hexc("#D6D2E4") * (1 - 0.36 * seas)[..., None] + hexc("#8A88A2") * (0.36 * seas)[..., None]
    face = face * ((0.80 + 0.20 * nz ** 0.6) * (1 + 0.06 * fine))[..., None]
    return px * (1 - disc[..., None]) + face * disc[..., None]


# ------------------------------------------------------------------------------------------------ the painting
def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    # ---- the sky: deep violet-indigo, a little lighter toward the horizon, the moon's light from the upper left
    sky = ramp(np.clip(Y / 520, 0, 1), [(0, "#07041A"), (0.35, "#120A34"), (0.75, "#1C1046"), (1.0, "#271656")])
    d_moon = np.sqrt((X - MOON[0]) ** 2 + (Y - MOON[1]) ** 2)
    sky = screen(sky, hexc("#C8B8F4") * (np.exp(-(d_moon / 300) ** 2) * 0.10 + np.exp(-(d_moon / 800) ** 2) * 0.04)[..., None])
    # the Milky Way from the lower left up behind the sphere to the upper right, mottled, its dust lanes dark
    P = RL.catmull(MILKY, 16)
    dist = _dist_to(X, Y, P)
    band = np.exp(-(dist / 75) ** 2)
    mott = fbm(H, W, 26 * S, 5, 31)
    dust = smooth(0.47, 0.62, fbm(H, W, 15 * S, 4, 33)) * np.exp(-(dist / 28) ** 2)
    glow = band * (0.4 + 0.6 * mott) * (1 - blur(dust, 2.5 * S) * 0.8) * smooth(540, 480, Y)
    sky = screen(sky, hexc("#A69CE0") * (glow * 0.20)[..., None])
    # the last of the dusk: an amber afterglow low along the far hills, under the night's violet
    dusk = np.exp(-((Y - 512) / 52) ** 2) * (0.75 + 0.25 * fbm(H, W, 70 * S, 3, 29))
    sky = screen(sky, hexc("#E8A04C") * (dusk * 0.30)[..., None])
    # a violet nebula drifting over the sphere's upper left and a wisp at the lower right
    neb_n = fbm(H, W, 46 * S, 5, 35)
    neb_a = np.exp(-(((X - 250) / 190) ** 2 + ((Y - 220) / 120) ** 2))
    neb_b = np.exp(-(((X - 610) / 150) ** 2 + ((Y - 420) / 90) ** 2))
    neb = np.clip((neb_a * 0.9 + neb_b * 0.7) * smooth(0.36, 0.78, neb_n), 0, 1)
    sky = screen(sky, hexc("#9A4CD8") * (neb * 0.20)[..., None])
    sky = screen(sky, hexc("#E0A8FF") * (neb ** 2 * smooth(0.6, 0.85, fbm(H, W, 10 * S, 3, 37)) * 0.08)[..., None])
    sky = strokes(sky, (Y < 520).astype(np.float32), S, lambda x, y: -0.6 + 0.25 * math.sin(x / 90), int(6500 * S * S),
                  length=(8, 18), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=26)
    # the field of stars, thicker along the Milky Way, thinner inside the sphere's cage
    r_s = np.sqrt((X - C[0]) ** 2 + (Y - C[1]) ** 2)

    def avoid(x, y):
        xi, yi = min(W - 1, int(x)), min(H - 1, int(y))
        inside = 0.35 if r_s[yi, xi] < RM + 6 else 1.0
        return (0.35 + 0.65 * float(np.exp(-(dist[yi, xi] / 90) ** 2))) * inside
    px = RL.stars(sky, S, 1400, 26, (0, 0, W, 500 * S), avoid=avoid, bright=0.5)
    # the moon (upper left) and two planets (upper right)
    px = RL.moon_glow(px, MOON[0] * S, MOON[1] * S, MOON[2] * 1.6 * S, MOON[2] * 5 * S, k_core=0.12, k_wide=0.05,
                      col="#C8C0F0")
    px = _moon(px, S, X, Y)
    for (x, y, m) in PLANETS:
        d2 = (X - x) ** 2 + (Y - y) ** 2
        g = np.exp(-d2 / (2 * 1.8 ** 2)) * m + np.exp(-d2 / (2 * 9 ** 2)) * 0.18 * m
        px = screen(px, hexc("#FFE6C8") * g[..., None])
    near_launch = smooth(96, 132, np.sqrt((X - PIVOT[0]) ** 2 + (Y - PIVOT[1]) ** 2))
    # ---- the heart's light: a soft bloom filling the cage, and ten soft spokes of light from the heart
    bloom = np.exp(-(r_s / 120) ** 2) * 0.11 + np.exp(-(r_s / 34) ** 2) * 0.07
    px = screen(px, hexc("#A498C8") * bloom[..., None])
    im, dr = _canvas(W, H, 2)
    for s in SPOKES:
        _line(dr, s, S, 2, 9.0)
    spokes = blur(_done(im, W, H), 6 * S)
    # full past the ring of six round the heart, softer inside it (the heart's pegs keep their F6 margin), and a
    # little stronger out past the meridian, where the spokes run on to their moons in the open sky
    fade = np.exp(-(np.maximum(r_s - 40, 0) / 300) ** 2) * (0.5 + 0.5 * smooth(40, 90, r_s))
    px = screen(px, hexc("#E4DCD0") * (spokes * fade * (0.38 + 0.18 * smooth(150, 210, r_s)) * near_launch)[..., None])
    # ---- the rings of light (the celestial circles): front bright, back dim; each a double rule
    light = np.zeros((H, W), np.float32)
    back = np.zeros((H, W), np.float32)
    for curve, w in ((EQUATOR, 2.8), (TROPICS[0], 1.7), (TROPICS[1], 1.7), (POLARS[0], 1.4), (POLARS[1], 1.4),
                     (COLURES[0], 2.0), (COLURES[1], 2.0)):
        f, b_ = ring_masks(W, H, S, curve, w)
        light, back = np.maximum(light, f), np.maximum(back, b_)
    px = _glow_line(px, back, "#9C98B8", 0.40, 0.14, S, 3.0)
    px = _glow_line(px, light, "#DCD8E8", 0.85, 0.30, S, 3.5)
    # the ecliptic: a wider band, two rules and the days' ticks, warmer (a faint gold in the light)
    ecl_in = circle3d((0, 0, 0), ECL_N, R - 6)
    f1, b1 = ring_masks(W, H, S, ECLIPTIC, 2.0)
    f2, b2 = ring_masks(W, H, S, ecl_in, 1.4)
    tk = ticks_mask(W, H, S, ECLIPTIC, 6, 6, 0.5, (C[0], C[1]), width=0.6, major=5)
    px = _glow_line(px, np.maximum(b1, b2) + tk * 0.4, "#A8A8C0", 0.30, 0.08, S)
    px = _glow_line(px, np.maximum(f1, f2) + tk * 0.6, "#ECEEF8", 0.60, 0.16, S)
    # ---- the brass frame: the fixed meridian (face on) with its graduations, and the broad horizon ring
    brass, brass_hi = "#B8925A", "#F0D8A0"
    im, dr = _canvas(W, H, 3)
    for rr, w in ((RM, 2.6), (RM - 7, 1.0), (RM + 6, 0.8)):
        _line(dr, [(C[0] + rr * math.cos(t), C[1] + rr * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 400)], S, 3, w)
    mer = _done(im, W, H)
    mtk = ticks_mask(W, H, S, MERIDIAN, 3, 6.5, 0.0, (C[0], C[1]), width=0.5, major=10)
    px = screen(px * (1 - mer[..., None] * 0.5), hexc(brass) * (mer * 0.55)[..., None])
    px = screen(px, hexc(brass_hi) * ((mer * 0.25 + mtk * 0.30) * near_launch)[..., None])
    px = screen(px, hexc("#E8C88A") * (blur(mer, 4 * S) * 0.10)[..., None])
    hf, hb = ring_masks(W, H, S, HORIZON_RING, 2.2)
    hf2, hb2 = ring_masks(W, H, S, HORIZON_INNER, 1.2)
    htk = ticks_mask(W, H, S, HORIZON_RING, 3, 0.0, 6.5, (C[0], C[1] + 2), width=0.5, major=6)
    hfront, hback = np.maximum(hf, hf2), np.maximum(hb, hb2)
    # the band between the horizon ring's two rules, a faint brass fill (front) to read as a broad ring
    yy0 = np.interp(X[0], [p[0] for p in HORIZON_RING[180:361]], [p[1] for p in HORIZON_RING[180:361]])
    px = screen(px, hexc(brass) * (hback * 0.30)[..., None])
    px = screen(px * (1 - hfront[..., None] * 0.4), hexc(brass) * (hfront * 0.60)[..., None])
    px = screen(px, hexc(brass_hi) * (hfront * 0.30 + htk * 0.22)[..., None])
    px = screen(px, hexc("#E8C88A") * (blur(hfront, 4 * S) * 0.10)[..., None])
    del yy0
    # ---- the axis: brass rods past the poles, a star finial on the north, the heart globe
    im, dr = _canvas(W, H, 3)
    _line(dr, ROD_N, S, 3, 3.0)
    _line(dr, ROD_S, S, 3, 3.0)
    _line(dr, [(NORTH[0], NORTH[1]), (SOUTH[0], SOUTH[1])], S, 3, 1.4)
    rods = _done(im, W, H)
    px = screen(px * (1 - rods[..., None] * 0.4), hexc(brass) * (rods * 0.55)[..., None])
    px = screen(px, hexc(brass_hi) * (rods * 0.22)[..., None])
    fx, fy = ROD_N[1]
    fin = (np.exp(-((X - fx) / 0.9) ** 2 - ((Y - fy) / 9) ** 2) + np.exp(-((Y - fy) / 0.9) ** 2 - ((X - fx) / 9) ** 2))
    px = screen(px, hexc("#FFF0C8") * (fin * 0.45 + np.exp(-((X - fx) ** 2 + (Y - fy) ** 2) / 30) * 0.4)[..., None])
    for (x, y, _) in (NORTH, SOUTH):
        px = screen(px, hexc(brass_hi) * (np.exp(-((X - x) ** 2 + (Y - y) ** 2) / 12) * 0.5)[..., None])
    hx, hy, hr = HEART
    d_h = np.sqrt((X - hx) ** 2 + (Y - hy) ** 2)
    globe = smooth(hr + 1, hr - 1, d_h)
    shade = np.clip(1 - np.sqrt(((X - hx + 6) ** 2 + (Y - hy + 7) ** 2)) / (hr * 1.6), 0, 1)
    gcol = ramp(shade, [(0, "#9A5E22"), (0.5, "#F0C070"), (1.0, "#FFF8E6")])
    gn = fbm(H, W, 6 * S, 3, 39)
    gcol = gcol * (0.85 + 0.3 * gn[..., None])
    px = px * (1 - globe[..., None]) + gcol * globe[..., None]
    px = screen(px, hexc("#FFE6B0") * (np.exp(-(d_h / 32) ** 2) * 0.18)[..., None])
    # ---- the terrace: hills far off, the parapet, the stand (fluted column, three-footed base)
    xs = np.array([p[0] for p in HORIZON], np.float32)
    top = np.interp(X[0], xs, np.array([p[1] for p in HORIZON], np.float32)) + (fbm(1, W, 20 * S, 3, 41)[0] - 0.5) * 8
    land = np.clip((Y - top[None, :]) * S + 0.5, 0, 1)
    depth = np.clip((Y - top[None, :]) / 40, 0, 1)
    ground = ramp(depth, [(0, "#2A1E52"), (0.3, "#150F30"), (1, "#0A071C")])
    ground = screen(ground, hexc("#A08ED8") * (np.exp(-((Y - top[None, :] - 1.0) / 1.3) ** 2) * 0.18)[..., None])
    px = px * (1 - land[..., None]) + ground * land[..., None]
    # banks of cloud low over the far hills either side, their tops lit by the moon
    cl_n = fbm(H, W, 34 * S, 5, 45)
    cl_top = 452 + 26 * fbm(1, W, 60 * S, 3, 47)[0][None, :] - 18 * np.exp(-((X - 120) / 90) ** 2)         - 10 * np.exp(-((X - 690) / 80) ** 2)
    side = smooth(260, 170, X) + smooth(540, 630, X)
    cloud = smooth(0.0, 10.0, Y - cl_top - 8 * (cl_n - 0.5)) * side * smooth(512, 500, Y)
    lit = np.exp(-((Y - cl_top - 4) / 6) ** 2) * side * (0.5 + 0.5 * cl_n)
    px = px * (1 - cloud[..., None] * 0.55) + ramp(np.clip((Y - 440) / 70, 0, 1), [(0, "#3A2C72"), (1, "#1A1240")])         * (cloud * 0.55)[..., None]
    px = screen(px, hexc("#F0C488") * (lit * 0.36)[..., None])
    # the mist along the far hills
    mist = np.exp(-((Y - 500) / 18) ** 2) * (0.6 + 0.4 * fbm(H, W, 40 * S, 3, 43))
    px = screen(px, hexc("#6A5AA8") * (mist * 0.10)[..., None])
    # the parapet: a stone coping and balusters, dark against the mist
    im, dr = _canvas(W, H, 3)
    s3 = S * 3
    dr.rectangle([0, (PARAPET - 3) * s3, W * 3, (PARAPET + 5) * s3], fill=255)
    dr.rectangle([0, (PARAPET + 22) * s3, W * 3, H * 3], fill=255)
    for bx in np.arange(10, 800, 22):
        dr.ellipse([(bx - 5) * s3, (PARAPET + 6) * s3, (bx + 5) * s3, (PARAPET + 16) * s3], fill=255)
        dr.rectangle([(bx - 3) * s3, (PARAPET + 5) * s3, (bx + 3) * s3, (PARAPET + 23) * s3], fill=255)
    par = _done(im, W, H)
    px = px * (1 - par[..., None]) + hexc("#0C0920") * par[..., None]
    par_rim = np.clip(par - np.roll(par, int(max(1, S)), 0), 0, 1)
    px = screen(px, hexc("#8E80C8") * (par_rim * 0.30)[..., None])
    # the stand: a fluted column under the meridian, a knop, and three curved feet on the coping
    im, dr = _canvas(W, H, 3)
    cx = C[0]
    dr.polygon([((cx - 5) * s3, STAND_TOP * s3), ((cx + 5) * s3, STAND_TOP * s3), ((cx + 8) * s3, (PARAPET - 14) * s3),
                ((cx - 8) * s3, (PARAPET - 14) * s3)], fill=255)
    dr.ellipse([(cx - 11) * s3, (STAND_TOP + 6) * s3, (cx + 11) * s3, (STAND_TOP + 16) * s3], fill=255)
    dr.ellipse([(cx - 16) * s3, (PARAPET - 20) * s3, (cx + 16) * s3, (PARAPET - 8) * s3], fill=255)
    for (fx_, fy_) in STAND["feet"]:
        pts = [(cx + (fx_ - cx) * t + 10 * math.sin(math.pi * t) * (1 if fx_ > cx else -1 if fx_ < cx else 0),
                PARAPET - 14 + (fy_ - PARAPET + 14) * t ** 1.4) for t in np.linspace(0, 1, 20)]
        _line(dr, pts, S, 3, 4.0)
        dr.ellipse([(fx_ - 6) * s3, (fy_ - 3) * s3, (fx_ + 6) * s3, (fy_ + 3) * s3], fill=255)
    st = _done(im, W, H)
    st_shade = 0.55 + 0.45 * smooth(cx + 6, cx - 6, X)        # lit from the left (the moon)
    px = px * (1 - st[..., None]) + (hexc("#6A5232") * st_shade[..., None] * 1.3) * st[..., None]
    st_rim = np.clip(st - np.roll(st, int(max(1, S)), 1), 0, 1)
    px = screen(px, hexc(brass_hi) * (st_rim * 0.45)[..., None])
    for k in range(-2, 3):                                 # the flutes
        fl = np.exp(-((X - cx - k * 2.6) / 0.5) ** 2) * (Y > STAND_TOP + 18) * (Y < PARAPET - 20)
        px = screen(px, hexc(brass) * (fl * 0.18)[..., None])
    Yl = px @ RL.LUM
    cap = 0.42 + 0.40 * smooth(MOON[2] + 4, MOON[2] - 2, d_moon)      # the moon's face keeps its light (ceilingExempt)
    px = px * np.where(Yl > cap, cap / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.22)
    return RL.grain(px, 0.008, seed=26)
