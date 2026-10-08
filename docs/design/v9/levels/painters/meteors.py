"""Our own painting for 6-4 "Meteor Shower" (The Silent Stars, Urianger): falling stars across a still sky.

A moonless night over a mountain lake at the height of a shower. The meteors stream out of one point of the sky, the
radiant, high on the left among a small knot of bright stars, and every streak lies on a ray from it: a long fireball
that crosses the sky to the right and leaves a faint twisted train, a fan of shorter streaks below it, and a scatter
of faint ones. Each streak brightens toward its head, its core white-green and its tail cooling to violet. Behind
them the sky is still: a deep indigo night thick with stars, the Milky Way's band climbing from the lower left to the
upper right with its dark rift, and a faint green airglow lying low over the range. A mountain range closes the
board, its snow caps and crests caught by the starlight, and below it a still lake holds the range and the airglow.

Nothing figurative: sky, light, land and water (charter 1.3). No moon is painted (a shower is watched without one), and
nothing bright stands in the launcher's column: the streaks keep clear of the pivot's ring.

FEATURES (board units): the radiant (circle), each streak as [tail, head] ("meteor 1" ...), the crests of the two
massifs, the peaks, the lake's shore, and the open sky (for the stars' motion).
"""
import math

import numpy as np

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

RADIANT = (190.0, 150.0)
# the streaks: (angle from the radiant in degrees, +x through +y; tail distance; head distance; brightness; width)
STREAKS = (
    (13.0, 330.0, 520.0, 1.00, 2.6),    # 1 the fireball, its train left behind it
    (27.0, 200.0, 400.0, 0.80, 2.0),    # 2
    (41.0, 250.0, 440.0, 0.78, 2.0),    # 3
    (55.0, 150.0, 320.0, 0.82, 2.1),    # 4
    (71.0, 110.0, 270.0, 0.74, 1.9),    # 5
    (90.0, 175.0, 268.0, 0.62, 1.7),    # 6
    (104.0, 118.0, 268.0, 0.60, 1.6),   # 7
    (34.0, 110.0, 190.0, 0.50, 1.4),    # 8
    (18.0, 396.0, 506.0, 0.56, 1.5),    # 9
)
# faint streaks with no pieces: the shower's depth
FAINT = ((23.7, 395.0, 448.0, 0.40), (29.5, 440.0, 482.0, 0.36), (126.0, 80.0, 136.0, 0.34), (34.0, 400.0, 446.0, 0.32),
         (8.0, 230.0, 285.0, 0.28), (48.0, 420.0, 495.0, 0.40), (64.0, 330.0, 395.0, 0.30), (118.0, 150.0, 250.0, 0.30),
         (60.0, 60.0, 100.0, 0.26), (96.0, 70.0, 110.0, 0.24), (24.0, 520.0, 565.0, 0.26))
# the Plough, standing still in the upper right: the handle from the left, then the bowl
PLOUGH = ((500, 150), (543, 138), (586, 144), (624, 162), (672, 168), (684, 210), (634, 206))
CREST_L = [(-10, 512), (40, 500), (80, 478), (118, 456), (150, 468), (182, 442), (205, 452), (232, 428), (262, 452), (292, 466),
           (318, 488), (350, 500), (380, 512), (405, 518)]
CREST_R = [(405, 518), (432, 512), (460, 500), (492, 486), (520, 472), (548, 478), (580, 446), (606, 458), (636, 424),
           (664, 446), (694, 436), (726, 462), (760, 480), (810, 492)]
PEAKS = ((118, 456), (182, 442), (232, 428), (580, 446), (636, 424), (694, 436))
SHORE = 524.0
# the Milky Way's spine, lower left to upper right
MW = [(40, 520), (180, 420), (330, 300), (480, 190), (640, 90), (780, 10)]


def _at(a, d):
    t = math.radians(a)
    return [round(RADIANT[0] + d * math.cos(t), 1), round(RADIANT[1] + d * math.sin(t), 1)]


def _crest_y(x, crest):
    xs, ys = zip(*crest)
    return float(np.interp(x, xs, ys))


FEATURES = {
    "radiant": {"circle": [RADIANT[0], RADIANT[1], 30.0], "from": 0, "sweep": 360},
    **{f"meteor {k + 1}": {"points": [_at(a, d0), _at(a, d1)]} for k, (a, d0, d1, _k, _w) in enumerate(STREAKS)},
    "crest left": {"points": [list(p) for p in CREST_L]},
    "crest right": {"points": [list(p) for p in CREST_R]},
    "peaks": {"points": [list(p) for p in PEAKS]},
    "milky way": {"points": [list(p) for p in MW]},
    "plough": {"points": [list(p) for p in PLOUGH]},
    "star sky": [[76, 30], [724, 30], [724, 420], [636, 410], [580, 432], [520, 456], [405, 500], [320, 474],
                 [232, 414], [182, 428], [118, 442], [76, 462]],
    "radiant knot": [[150, 112], [232, 112], [232, 190], [150, 190]],
    "lake": [[76, 530], [724, 530], [724, 600], [76, 600]],
}


def _seg(X, Y, ax, ay, bx, by):
    """Along (0 at a, 1 at b, unclipped) and across (distance from the line) a segment."""
    dx, dy = bx - ax, by - ay
    L2 = dx * dx + dy * dy
    t = ((X - ax) * dx + (Y - ay) * dy) / L2
    L = math.sqrt(L2)
    across = np.abs((X - ax) * dy - (Y - ay) * dx) / L
    return t, across, L


def _streak(px, S, X, Y, a, d0, d1, k, w, seed, train=False):
    """One meteor: a line on a ray from the radiant, brightening to its head; white-green core, violet tail, a soft
    glow round it, a bloom at the head; a fireball also sheds small sparks and leaves a twisted train."""
    (ax, ay), (bx, by) = _at(a, d0), _at(a, d1)
    pad = max(30.0, w * 6.0 * 3.6)
    x0, x1 = int(max(0, (min(ax, bx) - pad) * S)), int(min(X.shape[1], (max(ax, bx) + pad) * S))
    y0, y1 = int(max(0, (min(ay, by) - pad) * S)), int(min(X.shape[0], (max(ay, by) + pad) * S))
    if x1 <= x0 or y1 <= y0:
        return px
    Xs, Ys, sub = X[y0:y1, x0:x1], Y[y0:y1, x0:x1], px[y0:y1, x0:x1]
    t, across, L = _seg(Xs, Ys, ax, ay, bx, by)
    tc = np.clip(t, 0, 1)
    inside = smooth(-0.02, 0.06, t) * smooth(1.0 + 3.0 / L, 1.0, t)
    rise = tc ** 1.7
    wid = w * (0.35 + 0.65 * tc)
    core = np.exp(-(across / (wid * 0.75)) ** 2) * (0.25 + 0.75 * rise) * inside
    glow = np.exp(-(across / (wid * 4.0)) ** 2) * rise * inside
    head = np.exp(-((Xs - bx) ** 2 + (Ys - by) ** 2) / (2 * (w * 1.6) ** 2))
    bloom = np.exp(-((Xs - bx) ** 2 + (Ys - by) ** 2) / (2 * (w * 6.0) ** 2))
    # the colour along it: violet at the tail, teal-green toward the head, white at the core's head
    tail_c, mid_c, head_c = hexc("#7E6CE8"), hexc("#46D8B0"), hexc("#E6FFF4")
    colr = (tail_c * (1 - tc)[..., None] + mid_c * tc[..., None])
    sub = screen(sub, colr * (glow * 0.42 * k)[..., None])
    sub = screen(sub, (colr * 0.45 + head_c * 0.55) * (core * 1.0 * k)[..., None])
    sub = screen(sub, head_c * (head * 0.95 * k)[..., None])
    sub = screen(sub, hexc("#8CF0D0") * (bloom * 0.22 * k)[..., None])
    if train:
        # a faint twisted train of smoke left behind the fireball, drifting a little off its line
        wav = np.sin(t * 9.0 + 0.6) * 3.2 + np.sin(t * 23.0) * 1.2
        tr = np.exp(-((across - np.abs(wav) * 0.6 - 3.0) / 2.6) ** 2) * smooth(-0.15, 0.2, t) * smooth(0.95, 0.55, t)
        mott = fbm(Xs.shape[0], Xs.shape[1], 7 * S, 3, seed + 5)
        sub = screen(sub, hexc("#6A7CC8") * (tr * (0.10 + 0.10 * mott))[..., None])
        rng = np.random.default_rng(seed)
        for _ in range(7):
            u = rng.uniform(0.80, 0.98)
            off = rng.uniform(-6, 6)
            sx_ = ax + (bx - ax) * u - (by - ay) / L * off
            sy_ = ay + (by - ay) * u + (bx - ax) / L * off
            g = np.exp(-((Xs - sx_) ** 2 + (Ys - sy_) ** 2) / 0.9)
            sub = screen(sub, hexc("#CFFFEA") * (g * rng.uniform(0.25, 0.5))[..., None])
    px[y0:y1, x0:x1] = sub
    return px


def _range(S, X, Y, crest, seed):
    xs, ys = zip(*crest)
    top = np.interp(X[0], xs, ys).astype(np.float32)
    W = X.shape[1]
    top = top + (fbm(1, W, 7 * S, 4, seed)[0] - 0.5) * 9 + (fbm(1, W, 2 * S, 2, seed + 2)[0] - 0.5) * 3
    inside = (X[0] >= min(xs)) & (X[0] <= max(xs))
    top = np.where(inside, top, 1e9)
    return np.clip((Y - top[None, :]) * S + 0.5, 0, 1), top


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    rx, ry = RADIANT
    # ---- the still sky: deep indigo overhead, a little bluer toward the range, a faint green airglow low over it
    sky = ramp(np.clip(Y / 520, 0, 1), [(0, "#04061A"), (0.35, "#080C2A"), (0.7, "#0E1640"), (1.0, "#14204E")])
    air = smooth(300, 440, Y) * smooth(540, 470, Y) * (0.75 + 0.5 * fbm(H, W, 90 * S, 3, 7))
    sky = sky + (hexc("#1C5A4A") - sky) * (air * 0.40)[..., None]
    sky = screen(sky, hexc("#3AA080") * (smooth(400, 470, Y) * smooth(520, 480, Y) * 0.06)[..., None])
    # the Milky Way: a soft band from the lower left to the upper right, mottled, with a dark rift down it
    mwx, mwy = zip(*MW)
    t_mw = np.clip((X - 40) / 740, 0, 1)
    spine = np.interp(X, mwx, mwy)
    across = (Y - spine) * math.cos(math.atan2(510, 740))
    band = np.exp(-(across / 70) ** 2)
    m1 = fbm(H, W, 60 * S, 5, 11)
    m2 = fbm(H, W, 18 * S, 4, 13)
    cloud = band * (0.45 + 0.75 * smooth(0.35, 0.75, m1)) * (0.7 + 0.5 * m2)
    sky = screen(sky, ramp(t_mw, [(0, "#4A4C80"), (0.4, "#3C4688"), (1, "#2C3070")]) * (cloud * 0.40)[..., None])
    sky = screen(sky, hexc("#B8B0D8") * (np.exp(-(across / 26) ** 2) * smooth(0.5, 0.8, m1) * 0.10 * smooth(700, 120, X))[..., None])
    rift = np.exp(-((across + 8 + (m1 - 0.5) * 30) / 14) ** 2) * smooth(0.40, 0.75, blur(fbm(H, W, 26 * S, 3, 17), 4 * S))
    sky = sky * (1 - 0.30 * rift * band)[..., None]
    sky = strokes(sky, (Y < 500).astype(np.float32), S, lambda x, y: -math.atan2(510, 740),
                  int(4200 * S * S), length=(14, 30), width=(1.4, 2.6), jitter=0.05, hue_jitter=0.004, seed=19)
    # stars: a general field, then the band's crowd
    px = RL.stars(sky, S, 1300, 23, (0, 0, W, 520 * S), bright=0.55)
    bandd = lambda x, y: float(math.exp(-(((y / S) - float(np.interp(x / S, mwx, mwy))) * 0.82 / 60) ** 2))
    px = RL.stars(px, S, 2600, 29, (0, 0, W, 520 * S), bright=0.30, avoid=bandd)
    # the radiant's knot of bright stars, and a faint glow where the streaks meet
    px = screen(px, hexc("#8C9CE0") * (np.exp(-((X - rx) ** 2 + (Y - ry) ** 2) / (2 * 34.0 ** 2)) * 0.12)[..., None])
    knot = ((rx - 14, ry - 8, 0.85), (rx + 9, ry + 11, 0.7), (rx + 4, ry - 18, 0.55), (rx - 22, ry + 16, 0.5),
            (rx + 22, ry - 4, 0.5), (rx - 6, ry + 26, 0.42))
    bright = list(knot) + [(x, y, 0.62 if i in (0, 3, 4) else 0.52) for i, (x, y) in enumerate(PLOUGH)]
    rng = np.random.default_rng(31)
    while len(bright) < 29:
        x, y = rng.uniform(85, 715), rng.uniform(40, 420)
        if math.hypot(x - 400, y - 87) < 110 or math.hypot(x - rx, y - ry) < 60 or                 any(math.hypot(x - qx, y - qy) < 40 for (qx, qy) in PLOUGH):
            continue
        bright.append((x, y, rng.uniform(0.25, 0.5)))
    for (x, y, a) in bright:
        g = np.exp(-((X - x) ** 2 + (Y - y) ** 2) / 1.3)
        sp = (np.exp(-np.abs(X - x) / 0.6) * np.exp(-np.abs(Y - y) / 5.5) + np.exp(-np.abs(Y - y) / 0.6)
              * np.exp(-np.abs(X - x) / 5.5)) * 0.35
        halo = np.exp(-((X - x) ** 2 + (Y - y) ** 2) / 40.0) * 0.06
        px = screen(px, hexc("#E4E6FF") * ((g + sp) * a + halo * a)[..., None])
    # ---- the shower: the faint streaks first, then the nine
    for i, (a, d0, d1, k) in enumerate(FAINT):
        px = _streak(px, S, X, Y, a, d0, d1, k, 1.2, 300 + i)
    for i, (a, d0, d1, k, w) in enumerate(STREAKS):
        px = _streak(px, S, X, Y, a, d0, d1, k, w, 200 + i, train=(i == 0))
    # ---- the range: two massifs meeting in a low saddle, snow on the high peaks, crests caught by the starlight
    for crest, seed in ((CREST_L, 41), (CREST_R, 47)):
        m, top = _range(S, X, Y, crest, seed)
        below = np.clip(Y - top[None, :], 0, 200)
        rock = fbm(H, W, 10 * S, 4, seed + 3)
        body = ramp(np.clip(below / 80, 0, 1), [(0, "#1A2046"), (0.25, "#10142E"), (1, "#06081A")])
        body = body * (0.82 + 0.36 * rock)[..., None]
        # faces: the slopes turned to the upper left (the radiant's side) catch the starlight, the others fall dark
        tsm = blur(np.where(top > 1e8, 600.0, top)[None, :].repeat(3, 0).astype(np.float32), 3 * S)[1]
        slope = np.gradient(tsm) * S
        lit = np.clip(-slope * 1.4, -1, 1)[None, :] * np.exp(-below / 60)
        body = body * (1 + 0.35 * lit)[..., None]
        # snow: the upper slopes of the high peaks, broken by the rock, brightest on the lit faces
        hi = smooth(472, 428, top)[None, :] * np.exp(-below / 26)
        snow = hi * smooth(0.38, 0.60, rock + 0.25 * fbm(H, W, 3 * S, 2, seed + 7)) * (0.55 + 0.45 * np.clip(lit + 0.4, 0, 1))
        body = body + (hexc("#7484BC") - body) * (snow * 0.75)[..., None]
        # couloirs and spurs running down from each peak along the fall line
        cx0, cx1 = min(c[0] for c in crest), max(c[0] for c in crest)
        for (qx, qy) in PEAKS:
            if not (cx0 <= qx <= cx1):
                continue
            rng = np.random.default_rng(int(qx * 7 + qy))
            for _ in range(5):
                ang = math.radians(rng.choice([-1, 1]) * rng.uniform(28, 62) + 90)
                ox = qx + rng.uniform(-14, 14)
                oy = _crest_y(ox, crest) + rng.uniform(2, 8)
                ln = rng.uniform(26, 60)
                t, acr, _L = _seg(X, Y, ox, oy, ox + ln * math.cos(ang), oy + ln * math.sin(ang))
                wig = (fbm(H, W, 4 * S, 2, int(ox)) - 0.5) * 3.0
                line = np.exp(-((acr + wig) / 1.1) ** 2) * smooth(-0.05, 0.1, t) * smooth(1.0, 0.6, t)
                light = rng.random() < 0.5
                tone = hexc("#8090C8") if light else hexc("#05060F")
                body = body + (tone - body) * (line * (0.45 if light else 0.6))[..., None]
        rim = np.exp(-below / 2.2) * (0.35 + 0.25 * np.clip(lit + 0.5, 0, 1))
        body = screen(body, hexc("#A8C4F0") * rim[..., None])
        px = px * (1 - m[..., None]) + body * m[..., None]
    # ---- the lake: the range and the airglow held in still water, a little darker, broken by slow ripples
    lake = smooth(SHORE - 1, SHORE + 2, Y)
    ripple = np.sin(Y * 1.7 + fbm(H, W, 30 * S, 3, 53) * 8) * (Y - SHORE) * 0.05
    my = np.clip(2 * SHORE - Y, 0, 600) * S
    mx = np.clip(X * S + ripple * S, 0, W - 1.001)
    yi = np.clip(my.astype(np.int32), 0, H - 1)
    xi = mx.astype(np.int32)
    refl = px[yi, xi] * 0.55
    refl = refl + (hexc("#1C5A4A") - refl) * (smooth(SHORE, 600, Y) * 0.10)[..., None]
    streaks = (np.sin(Y * 2.6 + fbm(H, W, 12 * S, 2, 59) * 6) > 0.86) * smooth(SHORE + 2, 600, Y) * 0.08
    refl = screen(refl, hexc("#7CA8D8") * streaks[..., None])
    px = px * (1 - lake[..., None]) + refl * lake[..., None]
    shore = np.exp(-((Y - SHORE - 1) / 1.2) ** 2) * 0.18
    px = screen(px, hexc("#8CB4E0") * shore[..., None])
    # ---- the ceiling (the heads kept under it), the vignette and the grain
    Yl = px @ RL.LUM
    lim = 0.43
    px = px * np.where(Yl > lim, lim / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.22)
    return RL.grain(px, 0.008, seed=67)
