"""Our own painting for 6-5 "The Moon Gate" (The Silent Stars, Urianger): a gate of light ringed by orbiting moons.

The heavens Urianger reads end at his Moon Gate: a great ring of pale light standing in the night sky over a dark
upland, the stage's finale. Its rim is a carved dial of light (a bright core between two fine rings, twelve sign marks
and the hours' ticks between them), with a fine corona of filaments curling off it and a soft halo round it. Through the
ring the sky is another one: a teal nebula turning in a slow spiral toward the far moon at its heart, crowded with small
stars, so the gate reads as an opening, not a disc. Two orbits of light circle it, one inside the rim and one outside,
each a fine track the gate's moons ride (the level's orbit movers keep to them exactly). Over it the moon's phases stand
in a crown of twelve, waxing up the left from the new moon's first sliver to gibbous and waning down the right,
the calendar the seer reads.
The sky round it is a deep plum night with rose-violet nebula drifts and dust lanes, thick with stars; below, two dark
ridges close the lower corners, their crests and a few standing stones caught by the gate's light, with far hills and a
low mist between them under the gate.

Nothing figurative: light, sky, moons and land (charter 1.3). The full moons of the crown and the far moon are kept
under the ceiling (no exemption), and nothing bright stands in the launcher's column above y 300: the crown's fullest
moons sit 105 units either side of it (UX MR 3 round 1, M1).

FEATURES (board units): the gate (circle), the two orbit tracks (circles), the far moon, the crown of phases (x, y,
illuminated fraction, waxing), the ridges' crests, the standing stones' tops, the far hills, and the open sky (for the
stars' motion).
"""
import math

import numpy as np

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

C = (400.0, 346.0)              # the gate's centre
R_GATE = 120.0                  # the rim's core
R_IN, R_OUT = 68.0, 163.0       # the two orbit tracks
R_MOON = 21.0                   # the far moon at the gate's heart
R_CROWN = 226.0                 # the crown of phases
# the crown: (angle from the gate's centre, illuminated fraction, waxing); waxing up the left, waning down the right
CROWN = ((172, 0.04, True), (186, 0.13, True), (200, 0.27, True), (214, 0.45, True), (228, 0.68, True),
         (242, 0.92, True), (298, 0.92, False), (312, 0.68, False), (326, 0.45, False), (340, 0.27, False),
         (354, 0.13, False), (8, 0.04, False))
R_PHASE = 12.0
R_POINTS = 252.0                # the crown's points: a star standing out between each pair of phases
# the ridges' crests (left and right), far to near
RIDGE_L = [(40, 404), (82, 414), (118, 428), (150, 446), (182, 470), (212, 494), (240, 516), (266, 540), (290, 568),
           (306, 610)]
RIDGE_R = [(760, 398), (720, 410), (684, 426), (652, 446), (620, 470), (590, 492), (562, 514), (536, 540),
           (512, 568), (496, 610)]
# standing stones on the crests: (x on the crest, height, width, lean)
STONES_L = ((118, 34, 13, -0.06), (182, 28, 12, 0.04))
STONES_R = ((684, 32, 13, 0.05), (620, 27, 12, -0.04))
HILLS = [(40, 470), (120, 474), (210, 486), (300, 500), (360, 506), (400, 503), (440, 507), (520, 498), (610, 484),
         (700, 472), (760, 468)]


def _crown():
    out = []
    for (a, k, wax) in CROWN:
        t = math.radians(a)
        out.append([round(C[0] + R_CROWN * math.cos(t), 1), round(C[1] + R_CROWN * math.sin(t), 1), k, wax])
    return out


def _y_on(pts, x):
    xs, ys = zip(*sorted(pts))
    return float(np.interp(x, xs, ys))


def _stone_tops(ridge, stones):
    return [[float(x), round(_y_on(ridge, x) - h + 2, 1)] for (x, h, _w, _l) in stones]


def _points():
    angs = [a for (a, _k, _w) in CROWN]
    out = []
    for side in (angs[:6], angs[6:]):
        for (u, v) in zip(side, side[1:]):
            m = (u + v + (360 if v < u else 0)) / 2.0
            t = math.radians(m)
            out.append([round(C[0] + R_POINTS * math.cos(t), 1), round(C[1] + R_POINTS * math.sin(t), 1)])
    return out


PHASES = _crown()
POINTS = _points()

FEATURES = {
    "gate": {"circle": [C[0], C[1], R_GATE], "from": 180, "sweep": 360},
    "inner track": {"circle": [C[0], C[1], R_IN], "from": 0, "sweep": 360},
    "outer track": {"circle": [C[0], C[1], R_OUT], "from": 0, "sweep": 360},
    "far moon": {"circle": [C[0], C[1], R_MOON], "from": 0, "sweep": 360},
    "phases": {"moons": PHASES},
    "crown points": {"stars": POINTS},
    "ridge left": {"points": [list(p) for p in RIDGE_L]},
    "ridge right": {"points": [list(p) for p in RIDGE_R]},
    "stones": {"tops": _stone_tops(RIDGE_L, STONES_L) + _stone_tops(RIDGE_R, STONES_R)},
    "hills": {"points": [list(p) for p in HILLS]},
    # the gate and its halo, as a polygon (the stars' motion keeps off it, and the second jewel's region follows it)
    "gate halo": [[round(C[0] + 150 * math.cos(math.radians(a)), 1), round(C[1] + 150 * math.sin(math.radians(a)), 1)]
                  for a in range(0, 360, 15)],
    "star sky": [[76, 42], [724, 42], [724, 396], [650, 420], [560, 420], [540, 230], [470, 196], [400, 186],
                 [330, 196], [260, 230], [240, 420], [150, 420], [76, 400]],
}


def _sample(img, xs, ys):
    """img (H x W) sampled at device coordinates xs, ys (bilinear, clamped)."""
    H, W = img.shape
    xs = np.clip(xs, 0, W - 1.001)
    ys = np.clip(ys, 0, H - 1.001)
    x0, y0 = np.floor(xs).astype(np.int32), np.floor(ys).astype(np.int32)
    fx, fy = xs - x0, ys - y0
    a = img[y0, x0] * (1 - fx) + img[y0, x0 + 1] * fx
    b = img[y0 + 1, x0] * (1 - fx) + img[y0 + 1, x0 + 1] * fx
    return a * (1 - fy) + b * fy


def _phase(px, S, X, Y, mx, my, r, k, wax, seed):
    """A moon at phase k (illuminated fraction): the lit part a soft pale face with faint maria, the dark part in
    earthshine, the terminator soft; a small halo that grows with the phase."""
    u, v = (X - mx) / r, (Y - my) / r
    d = np.sqrt(u * u + v * v)
    disc = np.clip((1 - d) * r * S + 0.5, 0, 1)
    if not wax:
        u = -u
    t = 1 - 2 * k
    edge = t * np.sqrt(np.clip(1 - v * v, 0, 1))
    lit = smooth(edge - 0.10, edge + 0.10, u) * disc
    H, W = X.shape
    mott = fbm(H, W, r * S * 0.35, 3, seed)
    seas = smooth(0.48, 0.70, mott) * 0.28
    face = hexc("#EEEAF2") * (1 - seas)[..., None] + hexc("#A6A2B8") * seas[..., None]
    limb = (0.86 + 0.14 * np.sqrt(np.clip(1 - d * d, 0, 1)))[..., None]
    dark = hexc("#1C1A30")
    col = dark * (1 - lit[..., None]) + face * limb * lit[..., None]
    halo = np.exp(-np.clip(d - 1, 0, None) ** 2 / 0.5) * (d > 1) * (0.05 + 0.13 * k)
    px = screen(px, hexc("#D8CCF0") * halo[..., None])
    return px * (1 - disc[..., None]) + col * disc[..., None]


def _ridge_mask(S, X, Y, ridge):
    xs, ys = zip(*ridge)
    order = np.argsort(xs)
    xs, ys = np.asarray(xs)[order], np.asarray(ys)[order]
    top = np.interp(X[0], xs, ys).astype(np.float32)
    H, W = X.shape
    wob = (fbm(1, W, 9 * S, 4, 211)[0] - 0.5) * 12 + (fbm(1, W, 3 * S, 2, 213)[0] - 0.5) * 4
    top = top + wob
    inside = (X[0] >= xs.min()) & (X[0] <= xs.max())
    top = np.where(inside, top, 1e9)
    mask = np.clip((Y - top[None, :]) * S + 0.5, 0, 1)
    return mask, top


def _stone(px, S, X, Y, ridge, x, h, w, lean, lit_side):
    base = _y_on(ridge, x) + 6
    top = base - h - 6
    t = np.clip((base - Y) / (base - top), 0, 1)
    cx = x + lean * (base - Y)
    half = w / 2 * (1 - 0.30 * t)
    shape = np.clip((half - np.abs(X - cx)) * S + 0.5, 0, 1) * (Y > top) * (Y < base)
    shape = shape * np.clip((Y - top - 1.5 * (np.abs(X - cx) / max(half.max(), 1)) ** 2 * 4) * S + 0.5, 0, 1)
    body = hexc("#0C0A16")
    rim = np.clip(1 - np.abs((X - cx) * lit_side - half * 0.75) / 1.6, 0, 1) * shape * smooth(base, top, Y)
    px = px * (1 - shape[..., None]) + body * shape[..., None]
    return screen(px, hexc("#A8E0DA") * (rim * 0.45)[..., None])


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    cx, cy = C
    DX, DY = X - cx, Y - cy
    d = np.sqrt(DX * DX + DY * DY)
    th = np.arctan2(DY, DX)
    # ---- the sky: a deep plum night, darker overhead
    sky = ramp(np.clip(Y / 520, 0, 1), [(0, "#07041A"), (0.4, "#120826"), (0.8, "#1E0D33"), (1.0, "#26123C")])
    # rose-violet nebula drifts and darker dust lanes, swept round the gate a little
    swirl = th + 0.0016 * d
    n1 = fbm(H, W, 110 * S, 5, 31)
    n2 = fbm(H, W, 46 * S, 4, 37)
    sx = (cx + d * np.cos(swirl)) * S
    sy = (cy + d * np.sin(swirl)) * S
    neb = _sample(n1, sx, sy) * 0.65 + _sample(n2, sx, sy) * 0.35
    drift = smooth(0.50, 0.78, neb) * (0.55 + 0.45 * smooth(520, 160, Y))
    sky = sky + (hexc("#5A2468") - sky) * (drift * 0.42)[..., None]
    sky = screen(sky, hexc("#B05AA8") * (smooth(0.66, 0.88, neb) * 0.10)[..., None])
    dust = smooth(0.56, 0.74, _sample(fbm(H, W, 34 * S, 3, 41), sx, sy)) * smooth(0.40, 0.6, neb)
    sky = sky * (1 - 0.40 * dust[..., None])
    sky = strokes(sky, (Y < 470).astype(np.float32), S, lambda x, y: math.atan2(y - cy, x - cx) + math.pi / 2,
                  int(5200 * S * S), length=(12, 26), width=(1.4, 2.6), jitter=0.03, hue_jitter=0.005, seed=53)
    # the gate's wide halo in the air, cool, and the stars (thinned near the gate)
    halo = np.exp(-np.clip(d - R_GATE, 0, None) / 60) * (d > R_GATE - 4)
    sky = screen(sky, hexc("#6A8CB8") * (halo * 0.16)[..., None])
    px = RL.stars(sky, S, 1500, 57, (0, 0, W, 520 * S), bright=0.55,
                  avoid=lambda x, y: float(np.clip((math.hypot(x / S - cx, y / S - cy) - R_GATE - 10) / 40, 0, 1)))
    # a few brighter stars with soft spikes, away from the gate and the crown
    rng = np.random.default_rng(61)
    for _ in range(14):
        x, y = rng.uniform(80, 720), rng.uniform(46, 440)
        if math.hypot(x - cx, y - cy) < R_OUT + 40 or any(math.hypot(x - p[0], y - p[1]) < 30 for p in PHASES):
            continue
        g = np.exp(-((X - x) ** 2 + (Y - y) ** 2) / 1.2)
        sp = (np.exp(-np.abs(X - x) / 0.5) * np.exp(-np.abs(Y - y) / 5) + np.exp(-np.abs(Y - y) / 0.5)
              * np.exp(-np.abs(X - x) / 5)) * 0.35
        px = screen(px, hexc("#E6DCFF") * ((g + sp) * rng.uniform(0.25, 0.45))[..., None])
    # ---- through the gate: another sky, a teal nebula turning toward the far moon, crowded with small stars
    inner = smooth(R_GATE - 4, R_GATE - 12, d)
    tw = th + 2.4 * np.log1p((R_GATE - np.clip(d, 0, R_GATE)) / 30)
    ix = (cx + d * np.cos(tw)) * S
    iy = (cy + d * np.sin(tw)) * S
    m1 = _sample(fbm(H, W, 40 * S, 4, 71), ix, iy)
    arms = (0.5 + 0.5 * np.cos(2 * tw + 0.6)) ** 2.0
    depth = np.clip(d / R_GATE, 0, 1)
    far = ramp(depth, [(0, "#0E3A44"), (0.35, "#0A2A36"), (0.8, "#08202E"), (1.0, "#0C2838")])
    far = far + (hexc("#2C8C88") - far) * (smooth(0.42, 0.80, m1) * (0.25 + 0.55 * arms) * 0.55)[..., None]
    far = screen(far, hexc("#7ED8CC") * (arms * smooth(0.55, 0.85, m1) * 0.10 * (1 - depth * 0.5))[..., None])
    far = far * (1 - 0.35 * smooth(0.60, 0.80, _sample(fbm(H, W, 18 * S, 3, 77), ix, iy))[..., None] * (1 - arms[..., None]))
    far = RL.stars(far, S, 900, 83, ((cx - R_GATE) * S, (cy - R_GATE) * S, (cx + R_GATE) * S, (cy + R_GATE) * S),
                   bright=0.45)
    # the far moon at the heart, soft and cool, its light spilling into the nebula
    far = screen(far, hexc("#A0E4DA") * (np.exp(-(d / 46) ** 2) * 0.22)[..., None])
    mu, mv = DX / R_MOON, DY / R_MOON
    md = np.sqrt(mu * mu + mv * mv)
    mdisc = np.clip((1 - md) * R_MOON * S + 0.5, 0, 1)
    mmott = fbm(H, W, R_MOON * S * 0.4, 3, 89)
    mface = hexc("#D6F0EC") * (1 - 0.22 * smooth(0.5, 0.7, mmott))[..., None] * (0.80 + 0.20 * np.sqrt(np.clip(1 - md * md, 0, 1)))[..., None]
    far = far * (1 - mdisc[..., None]) + mface * mdisc[..., None]
    px = px * (1 - inner[..., None]) + far * inner[..., None]
    # ---- the orbit tracks: fine rings of light the moons ride, with small marks along the outer one
    for (R, k, w) in ((R_IN, 0.30, 1.0), (R_OUT, 0.34, 1.1)):
        line = np.exp(-((d - R) / w) ** 2) * k + np.exp(-((d - R) / 6) ** 2) * 0.05
        px = screen(px, hexc("#BCE6F0") * line[..., None])
    marks = (np.cos(th * 36) > 0.93) * np.exp(-((d - R_OUT - 5) / 1.6) ** 2) * 0.22
    marks = marks + (np.cos(th * 24) > 0.95) * np.exp(-((d - R_IN + 5) / 1.4) ** 2) * 0.16
    px = screen(px, hexc("#BCE6F0") * blur(marks.astype(np.float32), 0.6 * S)[..., None])
    # ---- the rim: a carved dial of light, the core between two fine rings, twelve sign marks and the hours' ticks
    ang = (th + math.pi) % (2 * math.pi)
    core = np.exp(-((d - R_GATE) / 2.6) ** 2)
    band = smooth(R_GATE - 11, R_GATE - 8, d) * smooth(R_GATE + 11, R_GATE + 8, d)
    rings = np.exp(-((d - R_GATE - 9.5) / 0.9) ** 2) + np.exp(-((d - R_GATE + 9.5) / 0.9) ** 2)
    sign = np.abs(((ang / (2 * math.pi / 12)) % 1) - 0.5)          # 0.5 at each of the twelve marks
    signs = smooth(0.43, 0.49, sign) * smooth(R_GATE + 8.5, R_GATE + 3.5, d) * smooth(R_GATE - 8.5, R_GATE - 3.5, d)
    hour = np.abs(((ang / (2 * math.pi / 48)) % 1) - 0.5)
    ticks = smooth(0.40, 0.48, hour) * smooth(R_GATE + 8, R_GATE + 5, d) * smooth(R_GATE + 3, R_GATE + 5, d)
    shimmer = 0.80 + 0.40 * _sample(fbm(H, W, 20 * S, 3, 97), (cx + R_GATE * np.cos(th)) * S, (cy + R_GATE * np.sin(th)) * S)
    stone = hexc("#2A2E4C")
    px = px * (1 - band[..., None] * 0.75) + stone * (band * 0.75)[..., None]
    px = screen(px, hexc("#9CC8E0") * (band * 0.10)[..., None])
    px = screen(px, hexc("#E8F4FF") * (core * 0.62 * shimmer)[..., None])
    px = screen(px, hexc("#C8DCF4") * (rings * 0.30)[..., None])
    px = screen(px, hexc("#F0E8FF") * (signs * 0.42 + ticks * 0.20)[..., None])
    # the corona: fine filaments curling off the rim, and a soft glow either side of it
    curl = th - 0.006 * (d - R_GATE)
    fil = np.zeros_like(d)
    rng = np.random.default_rng(101)
    for k in (41, 59, 73, 97, 131, 157):
        fil += np.cos(k * curl + rng.uniform(0, 6.3))
    fil = smooth(1.6, 3.4, fil) * np.exp(-np.clip(d - R_GATE - 8, 0, None) / 16) * (d > R_GATE + 6)
    px = screen(px, hexc("#B8D4F0") * (fil * 0.22)[..., None])
    glow = np.exp(-((d - R_GATE) / 18) ** 2) * 0.16
    px = screen(px, hexc("#9EC4E8") * glow[..., None])
    # ---- the crown of phases, and the stars that stand out between them as its points (a faint thread of light joins
    # each point to the moons either side, so the crown reads as one figure)
    for i, (mx, my, k, wax) in enumerate(PHASES):
        px = _phase(px, S, X, Y, mx, my, R_PHASE, k, wax, 120 + i)
    for j, (sx_, sy_) in enumerate(POINTS):
        i = j + (1 if j >= 5 else 0)
        for (mx, my, _k, _w) in (PHASES[i], PHASES[i + 1]):
            L = math.hypot(mx - sx_, my - sy_)
            t = np.clip(((X - sx_) * (mx - sx_) + (Y - sy_) * (my - sy_)) / (L * L), 0, 1)
            dd = np.hypot(X - (sx_ + t * (mx - sx_)), Y - (sy_ + t * (my - sy_)))
            thread = np.exp(-(dd / 0.8) ** 2) * smooth(0.0, 0.25, t) * smooth(1.0, 0.65, t) * 0.10
            px = screen(px, hexc("#D8CCF0") * thread[..., None])
        g = np.exp(-((X - sx_) ** 2 + (Y - sy_) ** 2) / 2.2)
        sp = (np.exp(-np.abs(X - sx_) / 0.7) * np.exp(-np.abs(Y - sy_) / 6) + np.exp(-np.abs(Y - sy_) / 0.7)
              * np.exp(-np.abs(X - sx_) / 6)) * 0.45
        halo = np.exp(-((X - sx_) ** 2 + (Y - sy_) ** 2) / 60.0) * 0.08
        px = screen(px, hexc("#EEE4FF") * ((g + sp) * 0.55 + halo)[..., None])
    # ---- the land: far hills under a low mist, then the two ridges and their standing stones
    hx, hy = zip(*HILLS)
    htop = np.interp(X[0], hx, hy).astype(np.float32) + (fbm(1, W, 14 * S, 3, 223)[0] - 0.5) * 8
    hill = np.clip((Y - htop[None, :]) * S + 0.5, 0, 1)
    hcol = ramp(np.clip((Y - 470) / 130, 0, 1), [(0, "#120E24"), (1, "#08061A")])
    hcol = hcol * (0.85 + 0.30 * fbm(H, W, 16 * S, 3, 229))[..., None]
    hlit = np.exp(-np.clip(Y - htop[None, :], 0, None) / 2.4) * np.exp(-((X - cx) / 200) ** 2) * 0.40
    hcol = screen(hcol, hexc("#7EB8C8") * hlit[..., None])
    px = px * (1 - hill[..., None]) + hcol * hill[..., None]
    mist = smooth(490, 515, Y) * smooth(560, 530, Y) * smooth(0.35, 0.75, fbm(H, W, 60 * S, 3, 227)) * 0.14
    px = screen(px, hexc("#5C6C94") * mist[..., None])
    under = np.exp(-(((X - cx) / 130) ** 2 + ((Y - 512) / 30) ** 2)) * 0.16
    px = screen(px, hexc("#8CCCC8") * under[..., None])
    # nearer hills either side of a valley, darker, their tops caught by the gate's light
    ntop = (562 - 0.0007 * (X[0] - cx) ** 2 + (fbm(1, W, 10 * S, 3, 241)[0] - 0.5) * 10).astype(np.float32)
    near = np.clip((Y - ntop[None, :]) * S + 0.5, 0, 1)
    ncol = ramp(np.clip((Y - 520) / 80, 0, 1), [(0, "#0E0B1E"), (1, "#06050F")])
    ncol = ncol * (0.85 + 0.30 * fbm(H, W, 11 * S, 3, 243))[..., None]
    ncol = screen(ncol, hexc("#86C4C4") * (np.exp(-np.clip(Y - ntop[None, :], 0, None) / 2.0) * 0.30
                                          * np.exp(-((X - cx) / 260) ** 2))[..., None])
    px = px * (1 - near[..., None]) + ncol * near[..., None]
    # the processional way: pale flagstones winding up the valley from the foot of the board toward the gate, lit by it
    # (closer slabs larger), with a small marker light at every few slabs
    wy = np.clip(Y, 500, 600)
    wcx = cx + 16 * np.sin((600 - wy) / 22.0) * (wy - 498) / 100
    ww = 3 + (wy - 500) * 0.30
    u = (X - wcx) / ww
    worn = fbm(H, W, 5 * S, 3, 253)
    way = smooth(1.05, 0.80, np.abs(u) + (worn - 0.5) * 0.5) * smooth(503, 510, Y) * (0.55 + 0.45 * smooth(0.25, 0.45, worn))
    persp = np.log(np.clip(Y - 490, 1, None))
    slab = np.abs(((persp * 9.0) % 1) - 0.5)
    joints = smooth(0.42, 0.49, slab) + smooth(0.38, 0.47, np.abs(((u * 1.2 + np.floor(persp * 9.0) * 0.5) % 1) - 0.5))
    stone_col = hexc("#343A58") * (0.70 + 0.6 * fbm(H, W, 3 * S, 2, 251))[..., None]
    lit = (0.45 + 0.55 * smooth(600, 505, Y))[..., None]
    stone_col = screen(stone_col * lit, hexc("#9ED8D0") * (smooth(600, 505, Y) * 0.12)[..., None])
    stone_col = stone_col * (1 - 0.40 * np.clip(joints, 0, 1))[..., None]
    px = px * (1 - way[..., None]) + stone_col * way[..., None]
    for yk in (512, 524, 540, 562, 590):
        wx = cx + 16 * math.sin((600 - yk) / 22.0) * (yk - 498) / 100
        side_w = 3 + (yk - 500) * 0.30 + 3
        for sx_ in (wx - side_w, wx + side_w):
            g = np.exp(-((X - sx_) ** 2 + (Y - yk) ** 2) / (0.6 + (yk - 500) * 0.02) ** 2)
            px = screen(px, hexc("#BDEFE6") * (g * 0.45)[..., None])
            px = screen(px, hexc("#7ED8CC") * (np.exp(-((X - sx_) ** 2 + (Y - yk) ** 2) / 40.0) * 0.06)[..., None])
    for ridge, stones, side in ((RIDGE_L, STONES_L, 1), (RIDGE_R, STONES_R, -1)):
        m, top = _ridge_mask(S, X, Y, ridge)
        below = np.clip(Y - top[None, :], 0, 200)
        rock = fbm(H, W, 12 * S, 4, 233 + side)
        body = ramp(np.clip(below / 90, 0, 1), [(0, "#1C1A30"), (0.3, "#110F20"), (1, "#07060E")])
        body = body * (0.85 + 0.30 * rock)[..., None]
        # the crest caught by the gate's light: brighter on the gate's side, a band of rim then strata below it
        face = np.exp(-((X - cx) ** 2 + (Y - cy) ** 2) / (2 * 360.0 ** 2))
        rim = np.exp(-below / 2.6) * (0.55 + 0.45 * face)
        strata = (np.sin(below * 0.55 + rock * 6) > 0.85) * np.exp(-below / 30) * 0.10
        body = screen(body, hexc("#9ED8D0") * (rim * 0.55 + strata)[..., None])
        px = px * (1 - m[..., None]) + body * m[..., None]
        for (x, h, w, lean) in stones:
            px = _stone(px, S, X, Y, ridge, x, h, w, lean, side)
    # ---- the ceiling (the gate's core and the crown's full moons kept under it), the vignette and the grain
    Yl = px @ RL.LUM
    lim = 0.43
    px = px * np.where(Yl > lim, lim / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.22)
    return RL.grain(px, 0.008, seed=67)
