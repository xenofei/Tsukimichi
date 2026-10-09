"""Our own painting for 6-4 "Meteor Shower" (The Silent Stars, Urianger; set nowhere): a meteor shower over the
mountains on a still, clear night.

The shower's radiant sits high in the upper left, a knot of bright stars the meteors all fly from: nine bright
meteors streak away from it across the sky, each a thin white core that brightens toward its head and ends in a small
round glow, its glow aqua near the head and violet in the long tail; the brightest, a fireball across the upper right,
leaves a faint persistent train drifting beside its path. Many faint meteors fill the fan between them, short near the
radiant and long far from it, so the shower reads as one radiating spray. The rest of the sky is still: a dense field
of small stars, a handful of bright ones, and the Milky Way rising up the right side with its dark rift. Along the
horizon the air glows aqua (airglow), lighting the mist in the valleys; a pale distant range and three nearer ones
close the bottom of the board, the far one with snow on its three peaks (its rock cut by gullies and ribs along each
slope's fall line), the near ones black with a fringe of pines.

FEATURES (board units): each pegged meteor's streak from tail to head ("meteor 1".."meteor 9"), the radiant, the far
range's crest and its peaks, the bright still stars (the layout's still moons stand on those a first flight reaches),
the Milky Way's spine and the open sky (for the stars' motion).
"""
import math

import numpy as np

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

RADIANT = (128.0, 66.0)
# the pegged meteors: (direction from the radiant in degrees, 0 along +x turning toward +y; tail and head distance from
# the radiant; brightness). The first is the fireball; the sixth runs along the upper right, clear of the launcher.
METEORS = (
    (24.0, 318.0, 612.0, 1.00),
    (36.0, 238.0, 478.0, 0.78),
    (48.5, 186.0, 410.0, 0.72),
    (61.0, 262.0, 432.0, 0.66),
    (74.0, 160.0, 338.0, 0.70),
    (14.5, 458.0, 622.0, 0.62),
    (86.0, 238.0, 386.0, 0.60),
    (44.0, 446.0, 578.0, 0.58),
    (94.0, 186.0, 330.0, 0.52),
)
PIVOT = (400.0, 87.0)
HORIZON = 470.0
# the far range: its crest's control points (x, y), three peaks with snow
FAR = [(40, 520), (90, 506), (140, 490), (190, 476), (226, 494), (262, 512), (300, 526), (346, 500), (390, 472),
       (420, 458), (452, 474), (488, 498), (516, 520), (556, 500), (610, 470), (648, 486), (690, 504), (760, 516)]
PEAKS = [(190, 476), (420, 458), (610, 470)]
DISTANT = [(40, 500), (130, 492), (250, 496), (300, 486), (350, 498), (470, 488), (520, 494), (560, 482),
           (700, 494), (760, 488)]
MID = [(40, 552), (110, 540), (170, 548), (240, 532), (300, 544), (380, 556), (460, 546), (540, 538), (600, 550),
       (680, 540), (760, 548)]
NEAR = [(40, 580), (120, 572), (200, 584), (300, 590), (400, 586), (500, 592), (600, 578), (690, 584), (760, 574)]
GALAXY = [(452, 520), (520, 420), (580, 330), (636, 240), (684, 156), (728, 70)]
# bright still stars (x, y, size): the radiant's knot, and the still sky's brightest
RADIANT_STARS = [(120, 58, 1.0), (140, 74, 0.8), (108, 80, 0.7), (150, 52, 0.6), (96, 46, 0.6), (132, 92, 0.5)]
BRIGHT = [(330, 150, 0.7), (470, 178, 0.9), (560, 150, 0.7), (640, 160, 0.9), (250, 152, 0.8), (520, 196, 1.0),
          (474, 262, 0.7), (556, 336, 0.8), (600, 398, 0.7), (640, 350, 0.9), (684, 414, 0.7), (700, 346, 0.7),
          (240, 300, 0.7), (250, 410, 0.6), (380, 420, 0.7), (540, 380, 0.7), (612, 120, 0.8), (690, 100, 0.7), (180, 160, 0.5)]


def meteor_pts(k, d0=None, d1=None):
    th, a, b, _ = METEORS[k]
    a = a if d0 is None else d0
    b = b if d1 is None else d1
    c, s = math.cos(math.radians(th)), math.sin(math.radians(th))
    return [(round(RADIANT[0] + a * c, 1), round(RADIANT[1] + a * s, 1)),
            (round(RADIANT[0] + b * c, 1), round(RADIANT[1] + b * s, 1))]


def _faint():
    """The faint meteors in the fan (no pegs): short near the radiant, longer far from it, none behind the launcher."""
    rng = np.random.default_rng(611)
    out = []
    while len(out) < 26:
        th = rng.uniform(-8, 104)
        d0 = rng.uniform(40, 560)
        L = 30 + 0.32 * d0 * rng.uniform(0.6, 1.3)
        c, s = math.cos(math.radians(th)), math.sin(math.radians(th))
        pts = [(RADIANT[0] + d0 * c, RADIANT[1] + d0 * s), (RADIANT[0] + (d0 + L) * c, RADIANT[1] + (d0 + L) * s)]
        # keep off the launcher's cone, the far range and the pegged meteors' own lines
        bad = False
        for t in np.linspace(0, 1, 9):
            x = pts[0][0] + (pts[1][0] - pts[0][0]) * t
            y = pts[0][1] + (pts[1][1] - pts[0][1]) * t
            if math.hypot(x - PIVOT[0], y - PIVOT[1]) < 110 or y > 450 or x > 730 or x < 70:
                bad = True
        if any(abs(th - m[0]) < 3.5 and d0 + L > m[1] - 20 and d0 < m[2] + 20 for m in METEORS):
            bad = True
        if not bad:
            out.append((th, d0, d0 + L, rng.uniform(0.18, 0.42)))
    return out


FAINT = _faint()


def _crest(pts, x):
    xs, ys = zip(*pts)
    return float(np.interp(x, xs, ys))


FEATURES = {
    **{f"meteor {k + 1}": {"points": [list(p) for p in meteor_pts(k)]} for k in range(len(METEORS))},
    "radiant": [list(RADIANT)],
    "far crest": {"points": [list(p) for p in FAR]},
    "peaks": [list(p) for p in PEAKS],
    "bright stars": [[x, y] for (x, y, _s) in BRIGHT],
    "milky way": {"points": [list(p) for p in GALAXY]},
    "star sky": [[76, 42], [724, 42], [724, 470], [640, 452], [610, 440], [560, 470], [500, 476], [420, 436],
                 [340, 470], [300, 492], [200, 452], [140, 470], [76, 480]],
}


def _seg_dist(X, Y, a, b):
    ax, ay = a
    bx, by = b
    L2 = (bx - ax) ** 2 + (by - ay) ** 2
    t = np.clip(((X - ax) * (bx - ax) + (Y - ay) * (by - ay)) / max(L2, 1e-6), 0, 1)
    return np.hypot(X - ax - (bx - ax) * t, Y - ay - (by - ay) * t), t


def _meteor(px, S, X, Y, th, d0, d1, B, seed, train=False):
    """One meteor: a thin white core brightening toward its head, a small flare at the head, an aqua glow near the
    head and a violet one down its tail; the fireball also leaves a faint, bent persistent train beside its path."""
    c, s = math.cos(math.radians(th)), math.sin(math.radians(th))
    a = (RADIANT[0] + d0 * c, RADIANT[1] + d0 * s)
    b = (RADIANT[0] + d1 * c, RADIANT[1] + d1 * s)
    pad = 30
    x0, x1 = int(max(0, (min(a[0], b[0]) - pad) * S)), int(min(X.shape[1], (max(a[0], b[0]) + pad) * S))
    y0, y1 = int(max(0, (min(a[1], b[1]) - pad) * S)), int(min(X.shape[0], (max(a[1], b[1]) + pad) * S))
    if x1 <= x0 or y1 <= y0:
        return px
    Xs, Ys = X[y0:y1, x0:x1], Y[y0:y1, x0:x1]
    d, t = _seg_dist(Xs, Ys, a, b)
    # brightness along the streak: from nothing at the tail, rising toward the head, flickering, and fading out past
    # the head's round glow
    rng = np.random.default_rng(seed)
    flick = 1 + 0.18 * np.sin(t * (14 + 8 * rng.random()) + rng.uniform(0, 6))
    along = (t ** 1.4) * smooth(1.0, 0.86, t) * flick
    amp = along * B
    w = 0.5 + 1.4 * B * (t ** 1.2)
    core = np.exp(-(d / w) ** 2) * amp
    glow_n = np.exp(-(d / (2.5 + 4.0 * B)) ** 2) * amp * 1.3
    glow_w = np.exp(-(d / (6 + 8 * B)) ** 2) * amp
    head = smooth(0.45, 0.85, t)
    sub = px[y0:y1, x0:x1]
    sub = screen(sub, hexc("#9A8CF0") * (glow_w * 0.10 * (1 - head))[..., None])
    sub = screen(sub, hexc("#74E2C8") * (glow_w * 0.12 * head)[..., None])
    sub = screen(sub, hexc("#B6C2FF") * (glow_n * 0.22)[..., None])
    sub = screen(sub, hexc("#F2F4FF") * (np.clip(core, 0, 1) * 0.92)[..., None])
    # the head: a small round glow where the meteor burns brightest, haloed aqua
    hx, hy = a[0] + (b[0] - a[0]) * 0.88, a[1] + (b[1] - a[1]) * 0.88
    hd = np.hypot(Xs - hx, Ys - hy)
    sub = screen(sub, hexc("#8EEAD6") * (np.exp(-(hd / (4 + 5 * B)) ** 2) * 0.22 * B)[..., None])
    sub = screen(sub, hexc("#F6FFFC") * (np.exp(-(hd / (1.1 + 1.3 * B)) ** 2) * 0.80 * B)[..., None])
    if train:
        # the persistent train: a faint, wider ribbon drifted a little off the path and bent by the high wind
        H_, W_ = Xs.shape
        bend = (fbm(H_, W_, 40 * S, 2, seed + 5) - 0.5) * 10
        nx, ny = -s, c
        off = (Xs - a[0]) * nx + (Ys - a[1]) * ny - 7 - bend
        along_t = np.clip(((Xs - a[0]) * c + (Ys - a[1]) * s) / (d1 - d0), 0, 1)
        rib = np.exp(-(off / 5.5) ** 2) * smooth(0.05, 0.4, along_t) * smooth(1.0, 0.75, along_t)
        rib = rib * (0.6 + 0.6 * fbm(H_, W_, 14 * S, 3, seed + 9))
        sub = screen(sub, hexc("#7FC8C0") * (rib * 0.10)[..., None])
    px[y0:y1, x0:x1] = sub
    return px


def _streaks(H, W, S, cell, seed, stretch=4):
    """fbm drawn short and stretched along y: streaks running down (sampled later along each slope's fall line)."""
    h = max(8, int(H / stretch))
    n = fbm(h, W, cell * S, 4, seed)
    yi = np.linspace(0, h - 1, H)
    i0 = np.floor(yi).astype(int)
    i1 = np.minimum(i0 + 1, h - 1)
    f = (yi - i0)[:, None].astype(np.float32)
    return n[i0] * (1 - f) + n[i1] * f


def _sample_x(img, X, Y, xs, S):
    """img (device px) sampled at board x = xs (per pixel), on each pixel's own row; linear in x."""
    H, W = img.shape
    fx = np.clip(xs * S - 0.5, 0, W - 1.001)
    i0 = np.floor(fx).astype(int)
    f = fx - i0
    rows = np.arange(H)[:, None]
    return img[rows, i0] * (1 - f) + img[rows, i0 + 1] * f


def _range(px, S, X, Y, pts, body, rim, rim_k, seed, snow=None, jag=6.0, tree=0.0, snowline=40.0, rock_k=1.0):
    """A mountain range: the silhouette below its crest (its run broken at three scales), its faces shaded as rock
    cut by gullies and ribs that run down each slope's fall line, a thin rim of sky light along the crest; `snow` lays
    snow on the upper slopes, on the ribs and out of the gullies; `tree` adds a fringe of pines along the crest."""
    H, W = X.shape
    xs, ys = zip(*pts)
    crest = np.interp(X[0], xs, ys).astype(np.float32)
    rough = ((fbm(1, W, 46 * S, 3, seed)[0] - 0.5) * jag * 1.3 + (fbm(1, W, 14 * S, 3, seed + 1)[0] - 0.5) * jag * 0.8
             + (fbm(1, W, 4 * S, 2, seed + 2)[0] - 0.5) * jag * 0.35)
    crest = crest + rough
    if tree:
        rng = np.random.default_rng(seed + 7)
        tops = np.zeros(W, np.float32)
        x = 0.0
        while x < 800:
            h = rng.uniform(5, 16) * tree * (0.7 + 0.6 * float(fbm(1, 64, 8, 2, seed + 9)[0, int(x / 800 * 63)]))
            wdt = h * rng.uniform(0.26, 0.38)
            cx = x
            lo, hi = int(max(0, (cx - wdt) * S)), int(min(W, (cx + wdt) * S + 1))
            u = np.abs(X[0, lo:hi] - cx) / wdt
            # a pine's tiers: a narrow spire with its boughs in steps
            tops[lo:hi] = np.maximum(tops[lo:hi], h * np.clip(1 - u, 0, 1) * (0.88 + 0.12 * np.cos(u * 26)))
            x += rng.uniform(3, 12) if rng.random() > 0.08 else rng.uniform(14, 30)
        crest = crest - tops
    inside = smooth(-0.6, 0.6, (Y - crest[None, :]) * S)
    below = Y - crest[None, :]
    depth = np.clip(below / 70, 0, 1)
    # each slope's fall line: the crest's broad run says which way the face falls (down-left on a left flank)
    k = max(3, int(30 * S)) | 1
    broad = np.convolve(np.pad(crest, k // 2, mode="edge"), np.ones(k) / k, mode="valid")
    slope = np.gradient(broad) * S                     # dy/dx of the crest (board units)
    fall = np.clip(slope, -1.2, 1.2)[None, :] * np.ones_like(Y)
    xs_fall = X - below * fall * 0.55                  # constant along a fall line, so the streaks follow it
    # rock: gullies (dark) and ribs (lit) along the fall lines, at two scales, ridged so the ribs are sharp
    g1 = _streaks(H, W, S, 9, seed + 3)
    g2 = _streaks(H, W, S, 3.5, seed + 4, stretch=3)
    rib = 1 - np.abs(2 * _sample_x(g1, X, Y, xs_fall, S) - 1)
    fine = _sample_x(g2, X, Y, xs_fall, S)
    rock = 0.5 + (np.clip(0.55 * rib ** 2 + 0.45 * fine, 0, 1) - 0.5) * rock_k
    face = np.clip(0.55 - fall * 0.7, 0, 1)            # faces falling to the left take the airglow's light from the left
    shade = (0.45 + 0.55 * face) * (1 - 0.6 * depth) * (0.65 + 0.7 * rock)
    col = hexc(body) * (0.55 + 0.7 * shade)[..., None]
    if snow is not None:
        # snow on the upper slopes (deeper on the high peaks), lying on the ribs and missing in the gullies
        top = float(min(ys))
        line = snowline * smooth(top + 60, top, crest)[None, :] + 6
        sn = smooth(line + 4, line - 10, below) * smooth(0.48, 0.72, rock + 0.15 * face) * inside
        sn = sn * (0.75 + 0.25 * fbm(H, W, 6 * S, 2, seed + 5))
        col = col * (1 - sn[..., None] * 0.9) + hexc(snow) * (sn * 0.9 * (0.55 + 0.6 * face) * (0.8 + 0.3 * rib))[..., None]
    rimm = np.exp(-np.clip(below, 0, None) / 1.4) * inside * rim_k
    col = screen(col, hexc(rim) * rimm[..., None])
    return px * (1 - inside[..., None]) + col * inside[..., None]


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    # ---- the sky: deep indigo overhead, a lighter blue lower down, and the airglow's band along the horizon
    sky = ramp(np.clip(Y / HORIZON, 0, 1), [(0, "#03051A"), (0.35, "#070C2C"), (0.7, "#101C48"), (1.0, "#1A3060")])
    glow_band = np.exp(-((Y - 450) / 70) ** 2) * (0.75 + 0.5 * fbm(H, W, 90 * S, 3, 21))
    sky = screen(sky, hexc("#3E9C8C") * (glow_band * 0.20)[..., None])
    # the airglow's faint ripples (gravity waves) in long bands
    wav = 0.5 + 0.5 * np.sin(Y / 9.0 + (fbm(H, W, 60 * S, 2, 23) - 0.5) * 6 + X / 160)
    sky = screen(sky, hexc("#4FB0A0") * (glow_band * wav ** 3 * 0.06)[..., None])
    # the sky's texture: long soft strokes, so the night is painted, not a flat gradient
    sky = strokes(sky, (Y < HORIZON + 30).astype(np.float32), S, lambda x, y: 0.04 * math.sin(x / 140 + y / 90),
                  int(3800 * S * S), length=(12, 26), width=(1.6, 3.0), jitter=0.022, hue_jitter=0.003, seed=37)
    # ---- the Milky Way: a soft band up the right side, its dark rift, and its crowded stars
    G = np.asarray(GALAXY, np.float32)
    dmw = np.full(X.shape, 1e9, np.float32)
    for a_, b_ in zip(G, G[1:]):
        dd, _ = _seg_dist(X, Y, a_, b_)
        dmw = np.minimum(dmw, dd)
    wob = (fbm(H, W, 50 * S, 3, 31) - 0.5) * 30
    band = np.exp(-((dmw + wob * 0.3) / 46) ** 2) * (0.7 + 0.6 * fbm(H, W, 22 * S, 4, 33)) * smooth(HORIZON + 10, HORIZON - 40, Y)
    rift = np.exp(-((dmw - 6) / 7) ** 2) * (0.5 + 0.5 * smooth(0.3, 0.7, fbm(H, W, 90 * S, 2, 35)))
    sky = screen(sky, hexc("#B4C0EC") * (band * 0.20 * (1 - 0.45 * rift))[..., None])
    sky = sky * (1 - 0.06 * (rift * band)[..., None])
    px = RL.stars(sky, S, 900, 41, (0, 0, W, HORIZON * S), bright=0.45)
    rng = np.random.default_rng(43)
    for _ in range(3600):
        x, y = rng.uniform(380, 800), rng.uniform(0, HORIZON)
        k = float(band[min(H - 1, int(y * S)), min(W - 1, int(x * S))]) * (1 - 0.45 * float(rift[min(H - 1, int(y * S)), min(W - 1, int(x * S))]))
        if rng.random() > k:
            continue
        r = (0.35 + 0.4 * rng.random() ** 3) * S
        a = 0.10 + 0.40 * rng.random() ** 2.5
        X0, X1, Y0, Y1 = int(max(0, x * S - 3 * r)), int(min(W, x * S + 3 * r + 1)), int(max(0, y * S - 3 * r)), int(min(H, y * S + 3 * r + 1))
        sy, sx = np.mgrid[Y0:Y1, X0:X1].astype(np.float32)
        g = np.exp(-((sx + 0.5 - x * S) ** 2 + (sy + 0.5 - y * S) ** 2) / (2 * r * r)) * a
        px[Y0:Y1, X0:X1] = screen(px[Y0:Y1, X0:X1], hexc("#E4ECFF") * g[..., None])
    # ---- the bright still stars, the radiant's knot among them: a core and a soft halo each
    for (x, y, sz) in BRIGHT + RADIANT_STARS:
        d2 = (X - x) ** 2 + (Y - y) ** 2
        col = "#FFE6C4" if (int(x) + int(y)) % 3 == 0 else "#DCE6FF"
        px = screen(px, hexc(col) * (np.exp(-d2 / (0.9 + 0.8 * sz) ** 2) * 0.75 * sz)[..., None])
        px = screen(px, hexc("#AFC0FF") * (np.exp(-d2 / (4 + 5 * sz) ** 2) * 0.10 * sz)[..., None])
    # the radiant itself: a faint glow where the meteors' lines meet (nothing bright: the eye finds it by the spray)
    dr = np.hypot(X - RADIANT[0], Y - RADIANT[1])
    px = screen(px, hexc("#9C94E8") * (np.exp(-(dr / 46) ** 2) * 0.10)[..., None])
    # ---- the shower: the faint meteors first, then the nine bright ones, the fireball last
    for i, (th, d0, d1, B) in enumerate(FAINT):
        px = _meteor(px, S, X, Y, th, d0, d1, B, 700 + i)
    order = sorted(range(len(METEORS)), key=lambda k: METEORS[k][3])
    for k in order:
        th, d0, d1, B = METEORS[k]
        px = _meteor(px, S, X, Y, th, d0 - 40 * B, d1, B, 800 + k, train=(k == 0))
    # ---- mist in the valleys, lit by the airglow
    mist = np.exp(-((Y - 520) / 26) ** 2) * (0.6 + 0.6 * fbm(H, W, 40 * S, 3, 51))
    px = screen(px, hexc("#5AA89A") * (mist * 0.16)[..., None])
    # ---- the three ranges, far to near
    px = _range(px, S, X, Y, DISTANT, "#2C4466", "#A8E0D8", 0.22, 57, jag=5.0, rock_k=0.25)
    mist0 = np.exp(-((Y - 508) / 14) ** 2) * (0.5 + 0.7 * fbm(H, W, 34 * S, 3, 59))
    px = screen(px, hexc("#5AA89A") * (mist0 * 0.12)[..., None])
    px = _range(px, S, X, Y, FAR, "#1E2A4E", "#9ED8D0", 0.30, 61, snow="#8C9CC8", jag=8.0, snowline=26.0)
    mist2 = np.exp(-((Y - 548) / 16) ** 2) * (0.5 + 0.7 * fbm(H, W, 30 * S, 3, 53))
    px = screen(px, hexc("#4C8E86") * (mist2 * 0.14)[..., None])
    px = _range(px, S, X, Y, MID, "#0E1430", "#6E9EB0", 0.18, 71, jag=6.0, tree=0.7)
    px = _range(px, S, X, Y, NEAR, "#05060F", "#3C5C70", 0.10, 81, jag=5.0, tree=1.3)
    # ---- the ceiling, the vignette and the grain
    Yl = px @ RL.LUM
    lim = 0.42
    px = px * np.where(Yl > lim, lim / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.22)
    return RL.grain(px, 0.008, seed=67)
