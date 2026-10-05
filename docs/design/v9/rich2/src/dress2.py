"""Re-dressing the six approved pilot scenes (rich pass 2, round 2): fuller, bolder, with depth. The approved scene is
the middle ground and is kept; on it go

  1. the level's palette: a jewel grade that keeps every pixel's OKLab lightness exactly (F1), with a second jewel
     pushed into the board's large non-peg regions (sky, sea, cloud sea, far ground) at chroma 0.08-0.13;
  2. light: shafts from the upper left (the one light) and one light event per board (a lamp pool, moonbeams, an
     aurora, earthlight);
  3. framing: dark foreground silhouettes in the corners and along the walls (oak and laurel leaves, fir boughs, firs,
     trunks, sailcloth, a prow, outcrops and crystals, a quill and inkwell), each rim-lit in short runs on the side that
     faces the light;
  4. the level's small lights (lamps, fireflies, glints), warm, kept 8+ units from every piece.

Organic elements (each leaf, needle, leaflet, barb) are dropped one by one when they would come within 6 units of a
piece (F3a), so the foliage parts round the pegs as real foliage would; large shapes (trunks, rocks, hulls) are placed by
hand. framecheck.check() runs on every board and the scene is not written if any rule fails.

  py -3 dress2.py [<level-id> ...]     writes scenes/<stem>.png (800 x 600) and @2x (1600 x 1200)

Pegs and layouts are not touched (level files stay in ../rich/levels). Rules: level-method.md, section 8.
"""
import json
import math
import sys

import numpy as np

from r2lib import (PALETTES, OUT_SCENES, RICH, RICH2, hexc, load_rgb, save_rgb, screen, smooth, srgb_to_oklab,
                   oklab_to_srgb, blur, LUM)
from rich_lib import fbm
import framecheck

WALL_L, WALL_R, TOP, FOOT = 75.0, 725.0, 41.0, 594.0


# ------------------------------------------------------------------------------------------------ grids
def grid(S):
    h, w = int(600 * S), int(800 * S)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    return (xx + 0.5) / S, (yy + 0.5) / S


def lwin(S, cx, cy, rad):
    """A local window round (cx, cy) units of radius rad: (slices, X, Y) in units, or None off the board."""
    h, w = int(600 * S), int(800 * S)
    x0, x1 = max(0, int((cx - rad) * S)), min(w, int((cx + rad) * S) + 1)
    y0, y1 = max(0, int((cy - rad) * S)), min(h, int((cy + rad) * S) + 1)
    if x1 <= x0 or y1 <= y0:
        return None
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    return (slice(y0, y1), slice(x0, x1)), (xx + 0.5) / S, (yy + 0.5) / S


def cov(sd, S):
    return np.clip(0.5 - sd * S, 0, 1)


def sd_ellipse(X, Y, cx, cy, rx, ry, rot=0.0):
    c, s = math.cos(rot), math.sin(rot)
    u = ((X - cx) * c + (Y - cy) * s) / rx
    v = (-(X - cx) * s + (Y - cy) * c) / ry
    return (np.sqrt(u * u + v * v) - 1) * min(rx, ry)


# ------------------------------------------------------------------------------------------------ the board context
class Ctx:
    """The board being dressed: S, the distance from every piece's edge (1x field), and the accumulated framing."""

    def __init__(self, level_id, S):
        self.id, self.S = level_id, S
        self.level = framecheck.load_level(RICH / "levels" / f"{level_id}.json")
        self.d1 = framecheck.piece_distance(self.level, 1.0)
        h, w = int(600 * S), int(800 * S)
        self.cover = np.zeros((h, w), np.float32)
        self.rim = np.zeros((h, w), np.float32)
        self.lights = []
        self.dropped = 0

    def clear(self, x, y):
        """Distance (units) from (x, y) to the nearest piece's edge; huge off the board's opening."""
        xi, yi = int(x), int(y)
        if not (0 <= xi < 800 and 0 <= yi < 600):
            return 1e9
        return float(self.d1[yi, xi])

    def ok(self, x, y, reach):
        """An element of radius `reach` at (x, y) keeps 6 units (plus half a unit of anti-aliasing) from every piece."""
        if not (WALL_L - 40 < x < WALL_R + 40 and TOP - 40 < y < 640):
            return True
        if WALL_L - reach > x or x > WALL_R + reach or y < TOP - reach:
            return True
        ok = self.clear(x, y) - reach >= 6.5
        if not ok:
            self.dropped += 1
        return ok


def stamp(ctx, mask, sd_fn, cx, cy, rad):
    w = lwin(ctx.S, cx, cy, rad)
    if w is None:
        return
    sl, X, Y = w
    mask[sl] = np.maximum(mask[sl], cov(sd_fn(X, Y), ctx.S))


# ------------------------------------------------------------------------------------------------ 1. palette
def _dir(hx):
    v = srgb_to_oklab(hexc(hx)[None, None])[0, 0]
    n = math.hypot(v[1], v[2]) + 1e-6
    return v[1] / n, v[2] / n


def jewel(px, S, bands, chroma=1.0, value_hues=None, keep_hi=0.75, keep=0.30, mask=None, mix=0.5, floor=0.024,
          regions=()):
    """Bolder colour with lightness kept (F1): OKLab L is untouched. The chroma direction comes from `bands`
    (y, hex) down the board, blended with `value_hues` [(L, hex)] by value; chroma is raised (a floor plus 1.4 x the
    source's), highlights kept cool. `regions` [(mask, hex, chroma)] then push a second jewel into large regions."""
    X, Y = grid(S)
    lab = srgb_to_oklab(px)
    L = lab[..., 0]
    ys = np.array([b[0] for b in bands], np.float32)
    dirs = np.array([_dir(b[1]) for b in bands], np.float32)
    ta = np.interp(Y, ys, dirs[:, 0])
    tb = np.interp(Y, ys, dirs[:, 1])
    if value_hues:
        ls = np.array([v[0] for v in value_hues], np.float32)
        vd = np.array([_dir(v[1]) for v in value_hues], np.float32)
        va, vb = np.interp(L, ls, vd[:, 0]), np.interp(L, ls, vd[:, 1])
        ta, tb = ta * (1 - mix) + va * mix, tb * (1 - mix) + vb * mix
    n = np.sqrt(ta * ta + tb * tb) + 1e-6
    ta, tb = ta / n, tb / n
    Cc = np.sqrt(lab[..., 1] ** 2 + lab[..., 2] ** 2)
    hi = smooth(0.60, 0.85, L)
    Cn = (floor + Cc * 1.4) * chroma * (1 - keep_hi * hi) * smooth(0.02, 0.12, L)
    a2 = lab[..., 1] * keep + ta * Cn * (1 - keep)
    b2 = lab[..., 2] * keep + tb * Cn * (1 - keep)
    for (rm, hx, cr) in regions:
        da, db = _dir(hx)
        c2 = cr * smooth(0.04, 0.14, L) * (1 - 0.6 * hi)
        a2 = a2 * (1 - rm) + da * c2 * rm
        b2 = b2 * (1 - rm) + db * c2 * rm
    if mask is not None:
        a2 = lab[..., 1] * (1 - mask) + a2 * mask
        b2 = lab[..., 2] * (1 - mask) + b2 * mask
    return oklab_to_srgb(np.stack([L, a2, b2], -1))


# ------------------------------------------------------------------------------------------------ 2. light
def shafts(px, S, origin=(-140.0, -220.0), angles=(48, 56, 63, 71), widths=(26, 18, 34, 22), k=0.07, col="#BFD2FF",
           seed=3, reach=900.0, sway=0.0, mask=None, near=150.0, noise_shift=(0.0, 0.0)):
    """Light shafts from a light beyond the upper left: soft bands radiating from `origin`, each fading with distance
    and broken by slow noise. `sway` (degrees) turns them slightly (the motion previews). F4: k at most 0.08."""
    X, Y = grid(S)
    dx, dy = X - origin[0], Y - origin[1]
    ang = np.degrees(np.arctan2(dy, dx))
    dist = np.sqrt(dx * dx + dy * dy)
    acc = np.zeros_like(X)
    for i, (a, w) in enumerate(zip(angles, widths)):
        a = a + sway * (1 if i % 2 else -0.7)
        hw = math.degrees(math.atan2(w, 520.0))
        acc += np.exp(-((ang - a) / hw) ** 2) * (0.70 + 0.30 * ((i * 0.37) % 1.0))
    n = _shaft_noise(S, seed)
    pad = int(12 * S)
    ox, oy = int(round(noise_shift[0] * S)), int(round(noise_shift[1] * S))
    n = n[pad + oy:pad + oy + int(600 * S), pad + ox:pad + ox + int(800 * S)]
    fall = smooth(reach, 200.0, dist) * smooth(near, near + 180.0, dist)
    amt = acc * fall * (0.55 + 0.6 * n) * k
    if mask is not None:
        amt = amt * mask
    return screen(px, hexc(col) * amt[..., None])


_NOISE = {}


def _shaft_noise(S, seed):
    """The slow noise that breaks the shafts, with a 12-unit margin so it can drift (the motion previews)."""
    if (S, seed) not in _NOISE:
        pad = int(12 * S)
        _NOISE[(S, seed)] = fbm(int(600 * S) + 2 * pad, int(800 * S) + 2 * pad, 90 * S, 3, seed)
    return _NOISE[(S, seed)]


def glow(px, S, x, y, r, col, k, mask=None):
    X, Y = grid(S)
    d = np.sqrt((X - x) ** 2 + (Y - y) ** 2)
    a = np.exp(-(d / r) ** 2) * k
    if mask is not None:
        a = a * mask
    return screen(px, hexc(col) * a[..., None])


def points(ctx, px, pts, col, r=1.4, k=0.8, halo=5.0, hk=0.25):
    """Small lights (lamps, fireflies, glints): a core and a halo; any within 8 units (plus its halo) of a piece's
    edge is left out (F5)."""
    S = ctx.S
    for (x, y, *rest) in pts:
        s = rest[0] if rest else 1.0
        if ctx.clear(x, y) < 8 + halo * s * 0.6:
            continue
        ctx.lights.append((x, y, halo * s))
        w = lwin(S, x, y, halo * s * 3)
        if w is None:
            continue
        sl, X, Y = w
        d = np.sqrt((X - x) ** 2 + (Y - y) ** 2)
        a = np.exp(-(d / (r * s)) ** 2) * k + np.exp(-(d / (halo * s)) ** 2) * hk
        px[sl] = screen(px[sl], hexc(col) * a[..., None])
    return px


# ------------------------------------------------------------------------------------------------ 3. silhouettes
def silhouette(ctx, px, mask, body="#05060E", rim="#9EB4FF", rim_k=0.55, rim_w=1.6, inner="#0C1230", inner_k=0.5,
               alpha=1.0, light=(-0.707, -0.707), seed=1, snow=None):
    """A dark foreground shape: a near-black body with a little of the palette in it, and a rim of light on the edges
    that face the upper left, broken into short runs (F3b) by slow noise, as light catches bark, leaves and stone.
    snow: a colour laid on the upper faces (fir boughs)."""
    S = ctx.S
    m = np.clip(mask, 0, 1)
    ctx.cover = np.maximum(ctx.cover, m)
    mb = blur(m, rim_w * S * 0.6)
    gy, gx = np.gradient(mb)
    facing = np.clip(-(gx * light[0] + gy * light[1]) * S * 4.0, 0, 1)
    band = facing * m * smooth(0.0, 0.6, mb) * (1 - smooth(0.75, 1.0, blur(m, rim_w * S)))
    breaker = smooth(0.36, 0.64, fbm(m.shape[0], m.shape[1], 4.5 * S, 2, seed + 101))
    thick = smooth(0.35, 0.6, blur(m, 2.5 * S))          # thin stems, ropes and posts catch no rim (F3b)
    rim_a = np.clip(band * 3.0, 0, 1) * rim_k * alpha * breaker * thick
    ctx.rim = np.maximum(ctx.rim, rim_a)
    shade = blur(m, 14 * S)
    body_col = hexc(body) * (1 - inner_k * (1 - shade))[..., None] + hexc(inner) * (inner_k * (1 - shade))[..., None]
    out = px * (1 - m[..., None] * alpha) + body_col * (m[..., None] * alpha)
    if snow is not None:
        up = np.clip(-gy * S * 6.0, 0, 1) * m
        out = screen(out, hexc(snow) * (np.clip(up * 2, 0, 1) * 0.30 * breaker)[..., None])
    return screen(out, hexc(rim) * rim_a[..., None])


def frond(ctx, mask, x, y, length, ang, droop, leaf, n, style="laurel", seed=1, width=1.6, twigs=0):
    """A drooping frond: a curved stem with leaves, each leaf dropped if it would come near a piece, and the stem
    ending where it would. styles: laurel (paired ovals), willow (long thin strands), fir (needles), oak (lobed
    masses), fern (paired narrow leaflets)."""
    rng = np.random.default_rng(seed)
    a = math.radians(ang)
    dx, dy = math.cos(a), math.sin(a)

    def P(t):
        return (x + dx * length * t, y + dy * length * t + droop * length * t * t)

    def T(t):
        tx, ty = dx * length, dy * length + 2 * droop * length * t
        nn = math.hypot(tx, ty)
        return tx / nn, ty / nn
    steps = max(40, int(length / max(width * 0.6, 0.6)))
    end = 1.0
    for k in range(steps + 1):
        t = k / steps
        sx, sy = P(t)
        if not ctx.ok(sx, sy, width + 1):
            end = max(0.0, t - 0.03)
            break
    for k in range(int(steps * end) + 1):
        t = k / steps
        sx, sy = P(t)
        wdt = width * (1 - 0.6 * t)
        stamp(ctx, mask, lambda X, Y, sx=sx, sy=sy, wdt=wdt: np.sqrt((X - sx) ** 2 + (Y - sy) ** 2) - wdt, sx, sy, wdt + 2)
    for j in range(twigs):
        t = 0.15 + 0.7 * (j + 0.5) / twigs
        if t > end:
            break
        sx, sy = P(t)
        tx, ty = T(t)
        side = 1 if j % 2 else -1
        ta = math.degrees(math.atan2(ty, tx)) + side * 38
        frond(ctx, mask, sx, sy, length * 0.36 * (1.1 - t * 0.5), ta, droop * 1.2, leaf * 0.85, max(4, n // 3), style,
              seed * 13 + j, width * 0.6)
    for i in range(n):
        t = 0.06 + 0.92 * i / max(1, n - 1)
        if t > end + 0.02:
            break
        sx, sy = P(t)
        tx, ty = T(t)
        size = leaf * (1.05 - 0.55 * t) * (0.8 + 0.4 * rng.random())
        sides = (-1, 1) if style != "oak" else (rng.choice([-1, 1]),)
        for side in sides:
            if style == "willow":
                spread, drop, ry = 14, 1.2, 0.10
            elif style == "fir":
                spread, drop, ry = 62, 0.15, 0.10
            elif style == "fern":
                spread, drop, ry = 70, 0.25, 0.16
            elif style == "oak":
                spread, drop, ry = 50, 0.4, 0.62
            else:
                spread, drop, ry = 34, 0.55, 0.30
            sp = math.radians(spread + rng.normal(0, 6)) * side
            lx, ly = tx * math.cos(sp) - ty * math.sin(sp), tx * math.sin(sp) + ty * math.cos(sp)
            ly += drop
            nl = math.hypot(lx, ly)
            lx, ly = lx / nl, ly / nl
            cx_, cy_ = sx + lx * size * 0.55, sy + ly * size * 0.55
            rx_, ry_ = size * 0.55, size * 0.55 * ry
            if not ctx.ok(cx_, cy_, rx_):
                continue
            rot = math.atan2(ly, lx)
            if style == "oak":
                # a lobed leaf: three overlapping rounds
                for (o, rr) in ((0.0, 1.0), (0.45, 0.72), (-0.45, 0.72)):
                    ox, oy = cx_ + math.cos(rot) * rx_ * o, cy_ + math.sin(rot) * rx_ * o
                    stamp(ctx, mask, lambda X, Y, ox=ox, oy=oy, rr=rr: sd_ellipse(X, Y, ox, oy, rx_ * 0.55 * rr,
                          rx_ * 0.48 * rr, rot + 0.6 * o), ox, oy, rx_ + 2)
            else:
                stamp(ctx, mask, lambda X, Y, cx_=cx_, cy_=cy_, rot=rot: sd_ellipse(X, Y, cx_, cy_, rx_, max(ry_, 0.7),
                      rot), cx_, cy_, rx_ + 2)


def trunk(ctx, mask, x, y0, y1, w0, w1, lean=0.0, seed=2):
    """A trunk with bark: the edge wobbles at two scales (no straight runs), knots break it."""
    S = ctx.S
    w = lwin(S, x, (y0 + y1) / 2, (y1 - y0) / 2 + w1 + abs(lean) + 10)
    sl, X, Y = w
    t = np.clip((Y - y0) / (y1 - y0), 0, 1)
    cx = x + lean * (1 - t) ** 2 + 2.2 * np.sin(Y / 23.0 + seed) + 1.2 * np.sin(Y / 7.3 + seed * 2)
    hw = w0 + (w1 - w0) * t
    n = fbm(X.shape[0], X.shape[1], 6 * S, 3, seed) - 0.5
    sd = np.abs(X - cx) - hw - n * 5.0
    sd = np.maximum(sd, np.maximum(y0 - Y, Y - y1))
    mask[sl] = np.maximum(mask[sl], cov(sd, S))


def pines(ctx, mask, trees, seed=4):
    """Fir silhouettes: (x, base_y, height, width); a tree is shortened until it keeps clear of every piece."""
    S = ctx.S
    rng = np.random.default_rng(seed)
    for (x, by, h, wdt) in trees:
        while h > 30:
            # the cone's edge points, checked against the pieces
            pts = [(x + s * wdt * (0.12 + 0.88 * t), by - h + h * t) for t in np.linspace(0, 1, 12) for s in (-1, 1)]
            if all(ctx.ok(px_, py_, 4) for (px_, py_) in pts):
                break
            h *= 0.88
            wdt *= 0.9
        w = lwin(S, x, by - h / 2, max(h, wdt) / 2 + wdt + 10)
        sl, X, Y = w
        top = by - h
        t = np.clip((Y - top) / h, 0, 1)
        saw = (t * 6) % 1.0
        half = wdt * (0.12 + 0.88 * t) * (0.62 + 0.38 * saw)
        jag = (fbm(X.shape[0], X.shape[1], 4 * S, 2, int(rng.integers(1000))) - 0.5) * 4
        sd = np.abs(X - x) - half - jag
        sd = np.maximum(sd, np.maximum(top - Y, Y - by))
        mask[sl] = np.maximum(mask[sl], cov(sd, S))
        mask[sl] = np.maximum(mask[sl], cov(np.maximum(np.abs(X - x) - wdt * 0.08, np.maximum(by - Y, Y - by - 30)), S))


def outcrop(ctx, mask, pts, seed=7, rough=10.0):
    """An outcrop: the region under a jagged ridge (polyline of (x, y)), with crags at two scales."""
    S = ctx.S
    xs = np.array([p[0] for p in pts], np.float32)
    ys = np.array([p[1] for p in pts], np.float32)
    X, Y = grid(S)
    ridge = np.interp(X[0], xs, ys, left=1e4, right=1e4)
    n = fbm(1, int(800 * S), 14 * S, 4, seed)[0] - 0.5
    n2 = fbm(1, int(800 * S), 4 * S, 2, seed + 1)[0] - 0.5
    ridge = ridge + n * rough + n2 * rough * 0.4
    inx = ((X >= xs.min()) & (X <= xs.max()))
    # settle: the outcrop sinks until it keeps 6.5 units from every piece (F3a)
    d1 = np.asarray(__import__("PIL.Image", fromlist=["Image"]).fromarray(ctx.d1).resize((int(800 * S), int(600 * S))))
    for drop in range(0, 200, 2):
        mm = cov(ridge[None, :] + drop - Y, S) * inx
        on = (mm > 0.4) & (X > WALL_L) & (X < WALL_R) & (Y > TOP)
        if not on.any() or d1[on].min() >= 6.5:
            break
    mask[:] = np.maximum(mask, mm)


def crystal(ctx, px, mask_all, x, y, h, w, tilt=0.0, body="#2A1E6A", lit="#D6C8FF"):
    """A faceted crystal spire: a hexagonal prism's two visible faces (the lit one toward the upper left), a pointed
    tip, a crack line. Drawn directly (it is lit stone, not a silhouette); its coverage is added to the framing."""
    S = ctx.S
    w_ = lwin(S, x, y - h / 2, h / 2 + w + 6)
    sl, X, Y = w_
    c, s = math.cos(tilt), math.sin(tilt)
    u = (X - x) * c + (Y - y) * s
    v = -(X - x) * s + (Y - y) * c
    t = np.clip(-v / h, 0, 1)
    half = w * np.where(t < 0.72, 1.0 - 0.15 * t, (1 - t) / 0.28 * 0.89)
    sd = np.maximum(np.abs(u) - half, np.maximum(v, -v - h))
    m = cov(sd, S)
    face_l = (u < -0.15 * half)
    col = np.where(face_l[..., None], hexc(lit) * 0.55 + hexc(body) * 0.45, hexc(body) * 0.8)
    col = col * (0.75 + 0.35 * t)[..., None]
    crack = np.exp(-((u - 0.3 * w * np.sin(v / 9.0)) / 0.6) ** 2) * (t > 0.2) * (t < 0.65)
    col = col * (1 - 0.35 * crack)[..., None]
    px[sl] = px[sl] * (1 - m[..., None]) + col * m[..., None]
    mask_all[sl] = np.maximum(mask_all[sl], m)


# ------------------------------------------------------------------------------------------------ board extras
def chart_border(px, S):
    """base-p1: the chart's neat-line on the frame's own edge (inset 1.5 units) with a degree scale of alternating
    ticks between it and the wall, kept out of the launcher's span (x 315-485) (critic: no inner wall)."""
    X, Y = grid(S)
    inset = 2.0
    d = np.minimum.reduce([np.abs(X - (WALL_L + inset)), np.abs(X - (WALL_R - inset)), np.abs(Y - (TOP + inset)),
                           np.abs(Y - (FOOT - inset))])
    inside = (X > WALL_L) & (X < WALL_R) & (Y > TOP) & (Y < FOOT)
    away = ~((Y < TOP + 6) & (X > 315) & (X < 485))
    out = screen(px, hexc("#E6C27A") * (cov(d - 0.5, S) * inside * away * 0.45)[..., None])
    edge = (np.minimum.reduce([X - WALL_L, WALL_R - X, Y - TOP, FOOT - Y]) < inset) & inside & away
    along = np.where((Y - TOP < inset) | (FOOT - Y < inset), X, Y)
    out = out * (1 - 0.45 * (edge & ((along // 16) % 2 == 0))[..., None])
    return out


def aurora(px, S, t=0.0, k=0.16):
    """exp-p2's light event: a curtain of aurora over the sea, aquamarine below, green-teal in the middle, violet at the
    top, its rays drifting slowly."""
    X, Y = grid(S)
    ph = t * 2 * math.pi / 6.0
    curve = 300 + 34 * np.sin(X / 120 + 0.8 + ph) + 16 * np.sin(X / 47 + 2.0)
    d = (Y - curve)
    curtain = np.exp(-(np.clip(d, None, 0) / 120) ** 2) * np.exp(-(np.clip(d, 0, None) / 16) ** 2)
    rays = 0.55 + 0.45 * np.sin(X / 7.0 + 1.3 * np.sin(X / 37 + ph))
    a = curtain * rays * smooth(WALL_L, 230, X) * smooth(WALL_R, 570, X)
    tt = smooth(-140, 0, d)
    col = hexc("#B07CFF")[None, None] * (1 - tt[..., None]) + hexc("#33F0C0")[None, None] * tt[..., None]
    return screen(px, col * (a * k)[..., None])


def nebula(px, S, keep_out=None):
    """exp-p3: a nebula of magenta and teal, only in the empty black."""
    X, Y = grid(S)
    n1 = fbm(int(600 * S), int(800 * S), 120 * S, 5, 31)
    n2 = fbm(int(600 * S), int(800 * S), 60 * S, 4, 32)
    dark = smooth(0.12, 0.03, px @ LUM)
    a = smooth(0.30, 0.75, n1) * (0.6 + 0.6 * n2) * dark
    if keep_out is not None:
        a = a * (1 - keep_out)
    col = hexc("#E04FA0") * (1 - 0.5 * n2)[..., None] + hexc("#20B0B8") * (0.5 * n2)[..., None]
    return screen(px, col * (a * 0.42)[..., None])


def compass_rose(ctx, px, cx, cy, R):
    """A chart's compass rose engraved in gilt light: eight points (the cardinal ones long), each split into a lit and
    a shaded half as engraving shows them, two thin rings with degree ticks; kept 6 units from every piece."""
    S = ctx.S
    w = lwin(S, cx, cy, R * 1.25)
    sl, X, Y = w
    u, v = X - cx, Y - cy
    r = np.sqrt(u * u + v * v)
    ang = np.arctan2(v, u)
    lit = np.zeros_like(r)
    dark = np.zeros_like(r)
    for k in range(8):
        a0 = k * math.pi / 4 - math.pi / 2
        L_ = R if k % 2 == 0 else R * 0.58
        half = R * (0.13 if k % 2 == 0 else 0.10)
        ca, sa = math.cos(a0), math.sin(a0)
        al = u * ca + v * sa                       # along the point
        ac = -u * sa + v * ca                      # across it
        inside = (al > 0) & (al < L_) & (np.abs(ac) < half * (1 - al / L_))
        side = ac > 0
        lit = np.maximum(lit, (inside & side).astype(np.float32))
        dark = np.maximum(dark, (inside & ~side).astype(np.float32))
    ring = np.exp(-((r - R * 1.05) / 0.6) ** 2) + np.exp(-((r - R * 0.36) / 0.5) ** 2)
    ticks = np.exp(-((r - R * 1.12) / 2.2) ** 2) * (np.abs(np.sin(ang * 36)) < 0.12)
    from PIL import Image as _I
    d = np.asarray(_I.fromarray(ctx.d1).resize((int(800 * S), int(600 * S))))[sl]
    keep = smooth(6.0, 9.0, d)
    k_ = keep * 0.55
    px[sl] = px[sl] * (1 - 0.45 * blur(dark, 0.4 * S) * keep)[..., None]
    px[sl] = screen(px[sl], hexc("#E6C27A") * ((blur(lit, 0.4 * S) * 0.9 + np.clip(ring, 0, 1) * 0.8 + ticks * 0.6) * k_)[..., None])
    return px


def disc_mask(S, cx, cy, r, feather=8.0):
    X, Y = grid(S)
    return smooth(r + feather, r - feather, np.sqrt((X - cx) ** 2 + (Y - cy) ** 2))


# ------------------------------------------------------------------------------------------------ recipes
def recipe(ctx, sc, t=0.0):
    """The re-dress for one level. sc: the approved scene (float RGB, at S). t: seconds (the motion previews only)."""
    S, lid = ctx.S, ctx.id
    X, Y = grid(S)
    Lsc = srgb_to_oklab(sc)[..., 0]
    sway = 0.6 * math.sin(t * 2 * math.pi / 6.0)
    m = np.zeros_like(X)
    if lid == "base-p1":
        # The Airship Road: a navigator's chart under a desk lamp. Teal seas against sapphire lands (the cooler chart,
        # owner question 4 and UX m13); the lamp's warm pool from the upper left; a quill and an inkwell in the lower
        # right corner, on the chart (depth); the neat-line on the frame's edge.
        sea = smooth(0.30, 0.42, blur(Lsc, 3 * S))
        px = jewel(sc, S, [(0, "#2B5FD0"), (600, "#1D4DB8")], chroma=0.9, floor=0.03,
                   regions=[(sea * 0.85, "#169A9A", 0.085)])
        px = glow(px, S, 150, 120, 300, "#FFC27A", 0.05)
        px = shafts(px, S, origin=(-60, -160), angles=(50, 58, 67), widths=(46, 32, 52), k=0.065, col="#FFD9A0",
                    sway=sway)
        px = chart_border(px, S)
        # round 3 (GD N5): the chart's own compass rose, engraved in gilt light in the open top-left corner (a sign
        # every sea chart carries, drawn in light, not a silhouette); kept 6 units from every piece
        px = compass_rose(ctx, px, 138, 112, 58)
        return px
    if lid == "base-p2":
        # The Holy See: a rose-pink cloud sea against glacier-blue stone and sky; snow-laden fir boughs from the top
        # corners and firs in the lower corners (we stand among the trees of Coerthas); moonbeams from the upper left.
        cloud = smooth(0.30, 0.46, blur(Lsc, 4 * S)) * smooth(140, 210, Y) * smooth(470, 380, Y)
        px = jewel(sc, S, [(0, "#3A6FD8"), (600, "#2B4FB0")],
                   value_hues=[(0.08, "#1E3A9A"), (0.45, "#C9D6FF")], chroma=0.95, floor=0.03,
                   regions=[(cloud, "#D27AA8", 0.085)])
        px = shafts(px, S, origin=(-120, -200), angles=(50, 57, 64, 72), widths=(34, 22, 40, 26), k=0.08,
                    col="#CFE0FF", sway=sway)
        for (x, y, L_, a, dr, sd_) in ((70, 34, 175, 14, 0.50, 1), (86, 36, 130, 46, 0.35, 2), (70, 70, 110, 30, 0.6, 3),
                                       (730, 34, 175, 166, 0.50, 4), (714, 36, 130, 134, 0.35, 5), (730, 70, 110, 150, 0.6, 6)):
            frond(ctx, m, x, y, L_, a, dr, 7, 70, style="fir", seed=sd_, width=2.4, twigs=6)
        pines(ctx, m, [(92, 600, 150, 30), (122, 606, 108, 22), (150, 610, 70, 15), (708, 604, 160, 32),
                       (680, 608, 112, 23), (655, 612, 64, 14)], seed=9)
        px = silhouette(ctx, px, m, body="#060818", inner="#16204e", rim="#DCE8FF", rim_k=0.32, snow="#E8F0FF", seed=5)
        return px
    if lid == "base-p3":
        # The Moonlit Post: an amethyst sky over the emerald Twelveswood; two great oaks at the walls (mostly beyond
        # them), oak leaves hanging from both top corners, ferns in the lower corners; moonbeams through the canopy;
        # fireflies. The moogle's red pom-pom keeps its colour.
        keep_pom = 1 - np.exp(-(((X - 572) ** 2 + (Y - 116) ** 2) / 26.0 ** 2) ** 2)
        wood = smooth(380, 440, Y)
        sky = smooth(330, 260, Y)
        # the wood's colour is quietened round the pegs (UX m4: under protanopia an orange on a saturated wood loses
        # its separation), so the jewel shows in the gaps and the pegs sit on a calm ground
        from PIL import Image as _I
        near = smooth(24.0, 12.0, np.asarray(_I.fromarray(ctx.d1).resize((int(800 * S), int(600 * S)))))
        px = jewel(sc, S, [(0, "#6B3FA8"), (330, "#4A3AA8"), (600, "#1F6E78")], chroma=0.9, floor=0.03,
                   regions=[(wood * 0.95 * (1 - 0.6 * near), "#1FB0A8", 0.12), (sky * 0.6, "#8A4FD0", 0.09)],
                   mask=keep_pom * (1 - 0.5 * near * wood))
        px = shafts(px, S, origin=(150, 70), angles=(38, 50, 62, 74, 86), widths=(16, 24, 14, 26, 12), k=0.08,
                    col="#D6E4FF", reach=760, sway=sway, near=40)
        trunk(ctx, m, 57, 30, 610, 14, 20, lean=-3, seed=3)
        trunk(ctx, m, 743, 30, 610, 14, 20, lean=4, seed=5)
        for (x, y, L_, a, dr, lf, n, sd_) in ((66, 30, 150, 18, 0.55, 20, 13, 11), (88, 40, 110, 55, 0.4, 18, 10, 12),
                                               (60, 90, 120, 30, 0.6, 18, 10, 13),
                                               (736, 30, 140, 168, 0.40, 20, 12, 14), (720, 60, 110, 128, 0.35, 18, 10, 15),
                                               (742, 100, 110, 150, 0.6, 18, 10, 16)):
            frond(ctx, m, x, y, L_, a, dr, lf * 1.15, int(n * 1.8), style="oak", seed=sd_, width=2.2, twigs=3)
        for (x, y, L_, a, sd_) in ((72, 600, 120, -60, 21), (95, 600, 95, -35, 22), (728, 600, 120, -120, 23),
                                   (705, 600, 95, -145, 24)):
            frond(ctx, m, x, y, L_, a, 0.22, 20, 11, style="fern", seed=sd_, width=1.4)
        px = silhouette(ctx, px, m, body="#04080A", inner="#0E3328", rim="#9EE6C4", rim_k=0.5, seed=7)
        px = points(ctx, px, fireflies("base-p3"), "#FFD27A", r=1.5, k=0.8, halo=5, hk=0.24)
        return px
    if lid == "exp-p1":
        # The Domes of Sharlayan: a turquoise harbour under a sapphire sky, gilt on the domes' crowns; laurel boughs
        # from both top corners; a scholars' balustrade with its lamp in the lower right, beyond the pegs; three shafts
        # across the domes; the quay's lamps.
        water = smooth(400, 470, Y)
        crowns = smooth(0.40, 0.50, Lsc) * smooth(150, 200, Y) * smooth(380, 330, Y)
        px = jewel(sc, S, [(0, "#3B5FD0"), (600, "#2C6FB8")], chroma=0.9, floor=0.03,
                   regions=[(water * 0.9, "#20B4B0", 0.10), (crowns * 0.7, "#E0B060", 0.06)])
        px = shafts(px, S, origin=(-140, -200), angles=(50, 58, 66), widths=(40, 28, 36), k=0.08, col="#D2F0FF",
                    sway=sway)
        for (x, y, L_, a, dr, sd_) in ((66, 34, 160, 20, 0.45, 41), (80, 40, 120, 52, 0.35, 42), (734, 34, 160, 160, 0.45, 43),
                                       (720, 40, 120, 128, 0.35, 44), (740, 80, 120, 150, 0.5, 45)):
            frond(ctx, m, x, y, L_, a, dr, 17, 14, style="laurel", seed=sd_, width=1.8)
        # the balustrade, right of x 650, its rail broken by laurel; the lamp column at x 702
        bal = np.zeros_like(m)
        w = lwin(S, 690, 570, 60)
        sl, Xw, Yw = w
        inx = (Xw > 652) & (Xw < WALL_R + 10)
        rail_y = 552 + (Xw - 690) ** 2 / 250.0 - 3.0           # a curved terrace: no straight run (F3c)
        rail = cov(np.abs(Yw - rail_y - 1.2 * np.sin(Xw / 9.0)) - 3.2, S) * inx
        u = ((Xw - 652) % 14) - 7
        tt = np.clip((Yw - 552) / 48, 0, 1)
        prof = 1.8 + 2.6 * np.exp(-((tt - 0.6) / 0.17) ** 2) + 1.2 * np.exp(-((tt - 0.15) / 0.07) ** 2) + 0.8 * np.sin(tt * 19)
        bal_ = cov(np.abs(u) - prof, S) * ((Yw > rail_y) & (Yw < 600) & inx)
        bal[sl] = np.maximum(rail, bal_)
        col_ = np.zeros_like(m)
        stamp(ctx, col_, lambda X_, Y_: sd_ellipse(X_, Y_, 716, 540, 7, 11), 716, 540, 14)
        mm = np.maximum(np.maximum(m, bal), col_)
        px = silhouette(ctx, px, mm, body="#050A12", inner="#0E2E3A", rim="#8FF0E8", rim_k=0.5, seed=9)
        px = points(ctx, px, [(716, 528, 1.5)], "#FFC86E", r=2.2, k=0.95, halo=11, hk=0.32)
        px = points(ctx, px, [(250, 520, 0.7), (330, 530, 0.7), (420, 528, 0.7), (500, 522, 0.7)], "#FFC86E",
                    r=1.0, k=0.6, halo=4, hk=0.18)
        return px
    if lid == "exp-p2":
        # The Ferry in the Stars: aboard the ferry. Violet sky, an aurora of aquamarine and green over a teal sea; the
        # furled sail's cloth in the upper left, rigging as continuous ropes through empty sky, the prow and its
        # lantern in the lower left.
        sea = smooth(430, 470, Y)
        px = jewel(sc, S, [(0, "#4A2F9A"), (380, "#6B3FA8"), (600, "#4A2F9A")], chroma=0.95, floor=0.03,
                   regions=[(sea * 0.9, "#1FB0A8", 0.10)])
        px = aurora(px, S, t)
        # the furled sail: festoons of canvas hanging from the yard above the board, lashed every 40 units
        sail = np.zeros_like(m)
        w = lwin(S, 150, 70, 90)
        sl, Xw, Yw = w
        for (xa, xb, sag) in ((WALL_L - 10, 112, 34), (112, 152, 26), (152, 188, 18), (188, 214, 10)):
            fr_ = np.clip((Xw - xa) / (xb - xa), 0, 1)
            inside = (Xw > xa) & (Xw < xb)
            bottom = TOP - 4 + sag * np.sin(np.pi * fr_) + 3 * np.sin(Xw / 5.0) * np.sin(np.pi * fr_)
            sail[sl] = np.maximum(sail[sl], cov(Yw - bottom, S) * inside)
        on = (sail > 0.5)
        X1, Y1 = grid(S)
        dd = np.asarray(__import__("PIL.Image", fromlist=["Image"]).fromarray(ctx.d1).resize((int(800 * S), int(600 * S))))
        if (dd[on & (X1 > WALL_L) & (Y1 > TOP)] >= 6.5).all():
            # canvas is lit cloth, not a silhouette: folds shaded across it
            folds = 0.5 + 0.5 * np.sin((X1 * 0.9 + Y1 * 1.6) / 4.0)
            cloth = (hexc("#2A1E58") * (0.7 + 0.5 * folds)[..., None]) * (0.8 + 0.4 * smooth(80, 40, Y1))[..., None]
            px = px * (1 - sail[..., None]) + cloth * sail[..., None]
            m = np.maximum(m, sail * 0.0)
            ctx.cover = np.maximum(ctx.cover, sail)
        ropes = np.zeros_like(m)
        for (a, b, sag, wdt) in (((WALL_L - 4, 150), (150, TOP - 6), 34, 1.6),):
            k_ok = True
            pts = [(a[0] + (b[0] - a[0]) * tt + 0, a[1] + (b[1] - a[1]) * tt + sag * 4 * tt * (1 - tt)) for tt in np.linspace(0, 1, 120)]
            pts = [p for p in pts if ctx.ok(p[0], p[1], wdt + 0.5)]
            for (rx_, ry_) in pts:
                stamp(ctx, ropes, lambda X_, Y_, rx_=rx_, ry_=ry_: np.sqrt((X_ - rx_) ** 2 + (Y_ - ry_) ** 2) - wdt, rx_, ry_, wdt + 2)
        px = silhouette(ctx, px, ropes, body="#0A0820", inner="#1A1238", rim="#C9B8FF", rim_k=0.0, seed=12)
        # the ferry's stern lantern on its post, rising from the lower left corner
        for k_ in range(30):
            yy_ = 600 - k_ * 2.0
            if not ctx.ok(84, yy_, 3.5):
                break
            stamp(ctx, m, lambda X_, Y_, yy_=yy_: np.abs(X_ - 84 - 0.5 * np.sin(Y_ / 6)) - 2.6 + 0 * Y_, 84, yy_, 6)
        top_y = 600 - k_ * 2.0
        stamp(ctx, m, lambda X_, Y_: sd_ellipse(X_, Y_, 84, top_y - 6, 5, 7), 84, top_y - 6, 10)
        px = silhouette(ctx, px, m, body="#06051A", inner="#24164E", rim="#C9B8FF", rim_k=0.0, seed=11)
        px = points(ctx, px, [(84, top_y - 6, 1.4)], "#FFB45E", r=2.4, k=0.95, halo=12, hk=0.35)
        return px
    if lid == "exp-p3":
        # The Sea of Sorrows: the world keeps its own blue, white and green (it is the subject); amethyst space with a
        # nebula of magenta and teal in the empty black; earthlight on the world's limb; lunar outcrops and crystal
        # spires in the lower corners.
        world = disc_mask(S, 510, 320, 150, 14)
        px = jewel(sc, S, [(0, "#7A4FC8"), (600, "#4A2F9A")],
                   value_hues=[(0.05, "#3A1F7A"), (0.45, "#C9D6FF")], chroma=0.95, floor=0.03, mask=1 - world)
        # the world's own ocean blue, a little richer (L kept): the board's second jewel (F7)
        ocean = world * smooth(0.10, 0.25, Lsc) * smooth(0.75, 0.55, Lsc)
        px = jewel(px, S, [(0, "#2F6FD8"), (600, "#2F6FD8")], chroma=1.0, floor=0.0, keep=0.5,
                   regions=[(ocean * 0.8, "#2F6FD8", 0.09)], mask=world)
        px = nebula(px, S, keep_out=world)
        d = np.sqrt((X - 510) ** 2 + (Y - 320) ** 2)
        limb = np.exp(-((d - 150) / 9) ** 2) * np.clip(-((X - 510) * 0.7 + (Y - 320) * 0.7) / 150 + 0.3, 0, 1)
        px = screen(px, hexc("#8FC8FF") * (limb * 0.10)[..., None])
        outcrop(ctx, m, [(WALL_L - 10, 470), (110, 505), (150, 548), (200, 576), (240, 610)], seed=17, rough=14)
        outcrop(ctx, m, [(560, 610), (610, 576), (660, 540), (700, 505), (WALL_R + 10, 470)], seed=19, rough=14)
        # drifting rocks in the empty top corners, lit by the earth
        rng = np.random.default_rng(23)
        for (cx_, cy_, r_) in ((98, 72, 17), (94, 150, 15), (104, 226, 15), (704, 66, 16), (710, 140, 15)):
            if ctx.ok(cx_, cy_, r_ * 1.6):
                k_ = rng.integers(5, 7)
                angs = np.sort(rng.uniform(0, 2 * np.pi, k_))
                rads = r_ * rng.uniform(0.55, 1.45, k_) * (1 + 0.45 * np.cos(angs - rng.uniform(0, np.pi)))
                def rock(X_, Y_, cx_=cx_, cy_=cy_, angs=angs, rads=rads):
                    a_ = np.arctan2(Y_ - cy_, X_ - cx_) % (2 * np.pi)
                    rr = np.interp(a_, np.concatenate([angs - 2 * np.pi, angs, angs + 2 * np.pi]), np.tile(rads, 3))
                    return np.sqrt((X_ - cx_) ** 2 + (Y_ - cy_) ** 2) - rr
                stamp(ctx, m, rock, cx_, cy_, r_ * 1.4)
        px = silhouette(ctx, px, m, body="#05040C", inner="#1A1238", rim="#9FC4FF", rim_k=0.45, seed=13)
        for (x, y, h, w_, tl) in ((92, 520, 44, 6.5, -0.18), (108, 536, 30, 4.5, 0.15), (712, 520, 46, 6.5, 0.16),
                                  (694, 540, 30, 4.5, -0.12)):
            if all(ctx.ok(x + math.sin(tl) * h * f, y - math.cos(tl) * h * f, w_ + 1) for f in (0, 0.35, 0.7, 1.0)):
                crystal(ctx, px, ctx.cover, x, y, h, w_, tl)
        return px
    raise KeyError(lid)


def firefly_spots(level_id, n=14, seed=5):
    """Fireflies (base-p3) where their whole wander (9 units across, 4 up and down) and their halo keep 8 units from
    every piece (F5 in motion): a greedy pick of the clearest spots over the wood and the open sky's lower half, at
    least 40 units apart."""
    lvl = framecheck.load_level(RICH / "levels" / f"{level_id}.json")
    d = framecheck.piece_distance(lvl)
    rng = np.random.default_rng(seed)
    cands = []
    for y in range(400, 516, 5):
        for x in range(90, 712, 5):
            s_ = 0.7 + 0.3 * rng.random()
            need = 8 + 4.5 * s_ * 2.2
            if all(d[int(y + math.sin(2 * a) * 4), int(x + math.cos(a) * 9)] >= need for a in np.linspace(0, 2 * math.pi, 12)):
                cands.append((float(d[y, x]) + rng.random() * 6, x, y, s_))
    rng.shuffle(cands)                                 # no ranking: a ranked pick lines up on the clearest row
    out = []
    for _, x, y, s_ in cands:
        band = sum(1 for (a, b, _s) in out if abs(b - y) < 20)
        if band < 3 and all((x - a) ** 2 + (y - b) ** 2 >= 30 ** 2 for (a, b, _s) in out):
            jx, jy = rng.uniform(-2, 2), rng.uniform(-2, 2)
            out.append((x + jx, y + jy, s_))
        if len(out) >= n:
            break
    return out


FIREFLIES = {}


def fireflies(level_id):
    if level_id not in FIREFLIES:
        FIREFLIES[level_id] = firefly_spots(level_id)
    return FIREFLIES[level_id]


def dress(level_id, S, t=0.0):
    from composite2 import PILOTS
    stem = PILOTS[level_id][0]
    sc = load_rgb(RICH / "scenes" / f"{stem}{'@2x' if S == 2 else ''}.png")
    ctx = Ctx(level_id, S)
    px = np.clip(recipe(ctx, sc, t), 0, 1)
    return px, ctx


if __name__ == "__main__":
    from composite2 import PILOTS
    ids = sys.argv[1:] or list(PILOTS)
    reports = {}
    p = RICH2 / "supervisor" / "framecheck.json"
    if p.exists():
        reports = json.loads(p.read_text())
    bad = []
    if not framecheck.selftest():
        raise SystemExit("framecheck's own self-test failed")
    for lid in ids:
        stem = PILOTS[lid][0]
        px, ctx = dress(lid, 2)
        rep, fails = framecheck.check(lid, ctx.cover, ctx.rim, ctx.lights, S=2)
        sc0 = load_rgb(RICH / "scenes" / f"{stem}@2x.png")
        near, nbad = framecheck.darkened_near_pieces(ctx.level, sc0, px)
        rep["darkened_min_clearance"] = None if near is None else round(near, 2)
        if nbad:
            fails.append("F3a-pixels")
        rep["elements_dropped_for_clearance"] = ctx.dropped
        rep["fails"] = fails
        reports[lid] = rep
        print(lid, "FAIL " + ",".join(fails) if fails else "ok", rep)
        if fails:
            bad.append(lid)
            continue
        save_rgb(px, OUT_SCENES / f"{stem}@2x.png")
        save_rgb(px, OUT_SCENES / f"{stem}.png", size=(800, 600))
    p.write_text(json.dumps(reports, indent=1) + "\n")
    if bad:
        raise SystemExit("framecheck failed: " + ", ".join(bad))
