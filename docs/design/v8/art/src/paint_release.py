"""Paints the two example release illustrations for the 1.22 What's new popup (spec-1.22.md, W2):

  evercold-base.png   1.20.0 "Before Evercold": a snowfield before dawn. The light is the coming sun, still under the
                      horizon behind a low pass; the waning crescent is lit on its sun-facing (lower-left) limb, the
                      far ridges are backlit with a thin warm rim, and every shadow falls toward the viewer.
  answers-base.png    1.19.0 "Right answers": a clear moonlit road. One light, the near-full moon above the road's
                      far end; the road catches it most where it runs toward the moon, the signpost at the fork is
                      rim-lit on its moon side and throws its shadow toward the viewer and left.

Both are painted at 2240 x 880 and saved at 1120 x 440: the popup's art band is 560 x 220 logical px, so this is its
2x tier. Everything is original; nothing is traced. Run: py -3 paint_release.py
"""
import math
import pathlib

import numpy as np

from artlib import Canvas, blur, fbm, fbm1d, hexc, smooth, value_noise

OUT = pathlib.Path(__file__).resolve().parent.parent
W, H = 2240, 880
OUTSIZE = (1120, 440)


def stars(c, n, seed, ymax, sky_lum, near_moon=None, warm=0.08):
    rng = np.random.default_rng(seed)
    acc = np.zeros((c.h, c.w, 3), np.float32)
    cool, moonw, gold = hexc("#DCE5FF"), hexc("#F4F2EA"), hexc("#FFE2A8")
    for _ in range(n):
        x, y = rng.random() * c.w, rng.random() ** 1.3 * ymax
        b = rng.random() ** 3.2
        r = 0.9 + 1.5 * b
        a = (0.25 + 0.75 * b) * max(0.0, 1.0 - 1.9 * sky_lum(y))
        if near_moon is not None:
            mx, my, mr = near_moon
            d = math.hypot(x - mx, y - my) / mr
            a *= min(1.0, max(0.0, (d - 2.2) / 5.0))
        if a <= 0.02:
            continue
        col = gold if rng.random() < warm else (moonw if rng.random() < 0.3 else cool)
        x0, x1 = int(max(0, x - 6)), int(min(c.w, x + 7))
        y0, y1 = int(max(0, y - 6)), int(min(c.h, y + 7))
        yy, xx = np.mgrid[y0:y1, x0:x1]
        k = np.exp(-(((xx - x) ** 2 + (yy - y) ** 2) / (2 * (r * 0.62) ** 2)))
        acc[y0:y1, x0:x1] += col * (k * a)[..., None]
    c.px = 1 - (1 - c.px) * (1 - np.clip(acc, 0, 1))  # stars are light: screen them over the sky


def tex_sample(T, u, v):
    th, tw = T.shape
    u = np.mod(u, tw - 1)
    v = np.mod(v, th - 1)
    u0 = np.floor(u).astype(int)
    v0 = np.floor(v).astype(int)
    fu, fv = u - u0, v - v0
    a, b = T[v0, u0], T[v0, u0 + 1]
    cc, d = T[v0 + 1, u0], T[v0 + 1, u0 + 1]
    return (a + (b - a) * fu) * (1 - fv) + (cc + (d - cc) * fu) * fv


def top_edge(m, k):
    """The upper rim band (k px thick) of a coverage mask: pixels in the mask whose pixel k above is not."""
    sh = np.zeros_like(m)
    sh[k:] = m[:-k]
    return np.clip(m - sh, 0, 1)


def conifer(c, cx, base, hgt, wid, seed, tiers=10):
    """A snow-laden spruce: jagged drooping tiers, returned as (body mask, list of tier masks)."""
    rng = np.random.default_rng(seed)
    body = np.zeros((c.h, c.w), np.float32)
    tiermasks = []
    for i in range(tiers):
        t0 = i / tiers
        top = base - hgt + hgt * t0 * 0.92
        bot = base - hgt + hgt * min(1.0, (i + 1.6) / tiers)
        half = wid * (0.12 + 0.88 * ((i + 1) / tiers) ** 0.9) / 2
        tipdrop = (bot - top) * 0.28
        pts = [(cx + rng.normal(0, 1.2), top)]
        steps = 7
        for s in range(1, steps + 1):  # right side, jagged
            f = s / steps
            pts.append((cx + half * f + rng.normal(0, half * 0.05), top + (bot - top) * f * 0.85 + rng.normal(0, 1.5)))
        pts.append((cx + half * 1.04, bot + tipdrop * 0.3))
        pts.append((cx + half * 0.55, bot - (bot - top) * 0.12))
        pts.append((cx, bot))
        pts.append((cx - half * 0.55, bot - (bot - top) * 0.12))
        pts.append((cx - half * 1.04, bot + tipdrop * 0.3))
        for s in range(steps, 0, -1):
            f = s / steps
            pts.append((cx - half * f + rng.normal(0, half * 0.05), top + (bot - top) * f * 0.85 + rng.normal(0, 1.5)))
        m = c.poly(pts, soft=0.6)
        tiermasks.append(m)
        body = np.maximum(body, m)
    trunk = c.poly([(cx - wid * 0.035, base - hgt * 0.12), (cx + wid * 0.035, base - hgt * 0.12), (cx + wid * 0.045, base + 4), (cx - wid * 0.045, base + 4)], 0.6)
    body = np.maximum(body, trunk)
    return body, tiermasks


# ======================================================================================================
# 1.20.0 Before Evercold
# ======================================================================================================
def evercold():
    c = Canvas(W, H)
    hz = 0.60 * H
    c.px = c.vgrad([(0, "#070E26"), (0.30, "#111D47"), (0.62, "#26346A"), (0.86, "#4C5288"), (1.0, "#6A6698")], 0, hz)
    c.px[int(hz):] = hexc("#6A6698")
    gx, gy = 0.655 * W, hz + 0.05 * H  # the sun, under the horizon
    d = c.radial(gx, gy, 0.50 * W, 0.40 * H)
    c.add(hexc("#F7B98C"), np.exp(-d ** 2 * 3.2) * 0.62)
    c.add(hexc("#C98FA8"), np.exp(-d ** 2 * 0.9) * 0.20)
    d2 = c.radial(gx, gy, 0.16 * W, 0.11 * H)
    c.add(hexc("#FFE3B8"), np.exp(-d2 ** 2 * 1.4) * 0.55)

    def sky_lum(y):
        t = min(1.0, max(0.0, y / hz))
        return 0.05 + 0.40 * t ** 2.2

    # the crescent: waning, low in the east, lit on the limb that faces the sun
    mx, my, mr = 0.815 * W, 0.20 * H, 36.0
    stars(c, 320, 11, hz * 0.95, sky_lum, near_moon=(mx, my, mr), warm=0.06)
    ux, uy = gx - mx, (gy + 0.25 * H) - my
    n = math.hypot(ux, uy)
    ux, uy = ux / n, uy / n
    disc = c.ellipse(mx, my, mr, mr, 0.7)
    cover = c.ellipse(mx - ux * mr * 0.58, my - uy * mr * 0.58, mr * 1.02, mr * 1.02, 0.9)
    lit = np.clip(disc - cover, 0, 1)
    c.over(hexc("#2B3767"), disc * 0.55)  # earthshine: the dark limb a shade over the sky
    c.add(hexc("#F6EAD2"), np.exp(-(c.radial(mx, my, mr * 3.2)) ** 2 * 1.6) * 0.07)
    c.over(hexc("#F6EDD8"), lit)
    c.over(hexc("#D9CDB6"), lit * np.clip(c.radial(mx + ux * mr * 0.7, my + uy * mr * 0.7, mr * 1.4), 0, 1) * 0.35)

    xs = np.arange(W, dtype=np.float32)
    pass_dip = np.exp(-((xs - gx) / (0.12 * W)) ** 2)
    # far range: hazy, backlit, a thin warm rim along the ridge strongest near the sun
    n1 = fbm1d(W, W / 4.5, 7, 3)
    far = hz - H * (0.05 + 0.20 * n1 ** 1.6) * (1 - 0.72 * pass_dip)
    far = far - 9 * (fbm1d(W, 22, 4, 5) - 0.5)
    m_far = c.below_curve(far, 1.2)
    farcol = c.vgrad([(0, "#565C90"), (1, "#7A77A5")], far.min(), hz + 10)
    # slopes that face the sun (left of the pass, rising to the right) take a lilac lift; the others stay in blue shade
    fslope = np.gradient(blur(np.repeat(far[None, :], 3, 0), 14)[1])
    ffacing = np.clip(np.where(xs < gx, 1, -1) * fslope * 1.4, -1, 1)
    depth = np.exp(-np.clip(c.yy - far[None, :], 0, None) / 90) * (0.6 + 0.8 * fbm(H, W, 26, 3, 23))
    farcol = farcol + (hexc("#9E93BE") - farcol) * (np.clip(ffacing, 0, 1)[None, :] * depth * 0.45)[..., None]
    farcol = farcol + (hexc("#434A7E") - farcol) * (np.clip(-ffacing, 0, 1)[None, :] * depth * 0.35)[..., None]
    snow_n = fbm(H, W, 60, 5, 21)
    farcol = farcol + (hexc("#A7A9CF") - farcol) * (np.clip((snow_n - 0.50) * 3, 0, 1) * smooth(hz, far.min(), c.yy) * 0.30)[..., None]
    c.over(farcol, m_far)
    rim_strength = np.exp(-((xs - gx) / (0.20 * W)) ** 2) * 0.80 + 0.03
    rim = np.clip(c.below_curve(far, 1.0) - c.below_curve(far + 2.5, 1.2), 0, 1) * rim_strength[None, :]
    c.add(hexc("#FFD3AA"), rim * 0.70)
    c.add(hexc("#FFC9A0"), blur(rim, 5) * 0.30)

    # mid range: nearer, darker, snow in its gullies, its sun-facing slopes faintly lifted
    n2 = fbm1d(W, W / 3.0, 7, 9)
    mid = hz + 0.03 * H - H * (0.02 + 0.14 * n2 ** 1.5) * (1 - 0.6 * pass_dip)
    m_mid = c.below_curve(mid, 1.2)
    slope = np.gradient(blur(np.repeat(mid[None, :], 3, 0), 10)[1])
    facing = np.where(xs < gx, 1, -1) * slope
    midcol = c.vgrad([(0, "#2C335F"), (1, "#3B4373")], mid.min(), hz + 0.08 * H)
    gul = fbm(H, W, 40, 5, 33)
    snowm = np.clip((gul - 0.5) * 3.2, 0, 1) * smooth(hz + 0.07 * H, mid.min(), c.yy)
    midcol = midcol + (hexc("#7C82B4") - midcol) * (snowm * 0.6)[..., None]
    lift = np.clip(facing * 0.9, 0, 1)[None, :] * np.exp(-np.clip(c.yy - mid[None, :], 0, None) / 70) * (0.5 + fbm(H, W, 20, 3, 29))
    midcol = midcol + (hexc("#B49AB8") - midcol) * (lift * 0.35)[..., None]
    c.over(midcol, m_mid)
    rim2 = np.clip(c.below_curve(mid, 1) - c.below_curve(mid + 2.5, 1), 0, 1) * (np.exp(-((xs - gx) / (0.18 * W)) ** 2) * 0.7)[None, :]
    c.add(hexc("#FFC9A0"), rim2 * 0.6)

    # the snowfield: perspective drifts, lit from the horizon, a forward-scatter sheen toward the sun
    base = hz + 0.055 * H
    T = fbm(512, 512, 64, 5, 47)
    dy = np.clip(c.yy - base + 18, 1, None)
    u = (c.xx - W / 2) / dy * 9 + 300
    v = 5200 / dy
    hgt = blur(tex_sample(T, u, v), 1.5)
    shade = np.gradient(hgt, axis=0) * np.clip(dy / 120, 0.3, 3)
    edge_line = base - 7 * fbm1d(W, 260, 4, 88) + 3
    field_mask = c.below_curve(edge_line, 2.5)
    # wind ripples (sastrugi): fine, long and low, only where the field is near enough to show them
    rip = tex_sample(fbm(256, 256, 6, 2, 49), (c.xx - W / 2) / dy * 40, 26000 / dy)
    ripple = (rip - 0.5) * smooth(base + 0.06 * H, H, c.yy)
    fieldcol = c.vgrad([(0, "#A794B4"), (0.10, "#8C88B5"), (0.45, "#5F689B"), (1, "#3E4980")], base, H)
    sh = np.clip(-shade * 30, -1, 1)
    fieldcol = fieldcol + (hexc("#36407A") - fieldcol) * (np.clip(sh, 0, 1) * 0.45)[..., None]
    fieldcol = fieldcol + (hexc("#C2AFC8") - fieldcol) * (np.clip(-sh, 0, 1) * 0.22)[..., None]
    fieldcol = fieldcol * (1 + ripple * 0.10)[..., None]
    wpath = 0.05 * W + (c.yy - base) * 0.75
    sheen = np.exp(-((c.xx - gx) / wpath) ** 2) * np.clip(1 - (c.yy - base) / (H - base), 0, 1) ** 1.6
    fieldcol = 1 - (1 - fieldcol) * (1 - hexc("#F6C9A4") * (sheen * 0.42)[..., None])
    c.over(fieldcol, field_mask)
    # the haze where the field meets the hills
    c.add(hexc("#B7A2C4"), np.exp(-((c.yy - base) / 18) ** 2) * 0.30)

    # a hamlet at the foot of the hills: dark roofs under snow, five warm windows, a thread of smoke
    hx, hy = 0.255 * W, base + 6
    rng = np.random.default_rng(5)
    houses = [(-70, 26, 34), (-28, 34, 44), (14, 30, 38), (52, 24, 30), (86, 20, 26)]
    hm = np.zeros((H, W), np.float32)
    roofs = np.zeros((H, W), np.float32)
    for dx, hw, hh in houses:
        x0 = hx + dx
        wall = c.poly([(x0 - hw / 2, hy), (x0 + hw / 2, hy), (x0 + hw / 2, hy - hh * 0.55), (x0 - hw / 2, hy - hh * 0.55)], 0.5)
        roof = c.poly([(x0 - hw / 2 - 4, hy - hh * 0.52), (x0, hy - hh), (x0 + hw / 2 + 4, hy - hh * 0.52)], 0.5)
        hm = np.maximum(hm, np.maximum(wall, roof))
        roofs = np.maximum(roofs, roof)
    c.over(hexc("#1A1E3E"), hm)
    c.over(hexc("#8E92C2"), top_edge(roofs, 4) * 0.85)
    win = np.zeros((H, W), np.float32)
    for dx, hw, hh in [houses[0], houses[1], houses[1], houses[2], houses[4]]:
        wx = hx + dx + rng.uniform(-hw * 0.25, hw * 0.25)
        win = np.maximum(win, c.ellipse(wx, hy - hh * 0.27, 2.6, 3.2, 0.5))
    c.add(hexc("#FFC77A"), win)
    c.add(hexc("#FFB060"), blur(win, 7) * 2.2)
    c.add(hexc("#FFB46A"), blur(win, 24) * 3.0 * (c.yy > hy - 4))
    sx = hx - 28
    smoke = np.exp(-((c.xx - (sx + (hy - 34 - c.yy) * 0.55)) / (4 + (hy - 34 - c.yy).clip(0) * 0.12)) ** 2)
    smoke *= np.clip((hy - 34 - c.yy) / 20, 0, 1) * np.clip(1 - (hy - 34 - c.yy) / 170, 0, 1)
    smoke *= fbm(H, W, 22, 3, 61)
    c.add(hexc("#A6A3C8"), blur(smoke, 3) * 0.22)

    # foreground spruces, left; shadows run toward the viewer, away from the low light
    trees = [(0.075 * W, H + 30, 0.78 * H, 230, 1), (0.165 * W, H - 34, 0.52 * H, 150, 2), (0.012 * W, H - 10, 0.92 * H, 270, 3), (0.215 * W, H - 92, 0.30 * H, 86, 4)]
    shadow = np.zeros((H, W), np.float32)
    for cx, b, hh, ww, s in trees:
        shadow = np.maximum(shadow, c.poly([(cx - ww * 0.2, b - 6), (cx + ww * 0.2, b - 6), (cx + ww * 0.05 - 160, H + 40), (cx - ww * 0.6 - 160, H + 40)], 0))
    c.mul(hexc("#4A5590"), blur(shadow, 16) * 0.55)
    for cx, b, hh, ww, s in trees:
        body, tiers = conifer(c, cx, b, hh, ww, 100 + s)
        c.over(hexc("#0D1626"), body)
        sn = np.zeros((H, W), np.float32)
        for tm in tiers:
            sn = np.maximum(sn, top_edge(tm, 5 if hh > 300 else 3))
        sn *= fbm(H, W, 6, 2, 70 + s) > 0.38
        side = np.clip((c.xx - cx) / (ww * 0.5), -1, 1)
        snowcol = np.stack([np.full((H, W), v, np.float32) for v in hexc("#AEB3D6")], -1)
        snowcol = snowcol + (hexc("#E9C8C2") - snowcol) * (np.clip(side, 0, 1) * 0.55)[..., None]
        c.over(snowcol, blur(sn.astype(np.float32), 0.7) * 0.92)

    # a drift in the right foreground, its crest catching the dawn
    xs2 = xs
    rise = smooth(0.40 * W, 0.97 * W, xs2)
    crest = H + 30 - 0.24 * H * rise - 0.02 * H * np.sin(xs2 / W * 9.0) * rise - 10 * fbm1d(W, 180, 4, 77) * rise
    dm = c.below_curve(crest, 2.0)
    # the drift's face looks at the viewer, away from the light: a blue shade under a thin warm lip
    under = np.exp(-np.clip(c.yy - crest[None, :], 0, None) / 70)
    c.mul(hexc("#3A4682"), dm * (0.25 + 0.35 * under) * rise[None, :])
    lip = np.clip(c.below_curve(crest, 1) - c.below_curve(crest + 4, 2), 0, 1) * (rise ** 2 * (0.25 + 0.75 * np.exp(-((xs2 - gx) / (0.25 * W)) ** 2)))[None, :]
    c.add(hexc("#F4CDB4"), lip * 0.45)
    c.add(hexc("#E8BFB0"), blur(lip, 3) * 0.25)

    # falling snow: few, slow, larger and softer the nearer they are
    rng = np.random.default_rng(91)
    flakes = np.zeros((H, W), np.float32)
    for _ in range(70):
        x, y = rng.random() * W, rng.random() * H
        z = rng.random() ** 2
        r = 1.3 + 6.5 * z
        a = 0.55 - 0.32 * z
        x0, x1 = int(max(0, x - 3 * r)), int(min(W, x + 3 * r + 1))
        y0, y1 = int(max(0, y - 3 * r)), int(min(H, y + 3 * r + 1))
        yy, xx = np.mgrid[y0:y1, x0:x1]
        k = np.exp(-(((xx - x) ** 2 + (yy - y) ** 2) / (2 * (r * 0.55) ** 2)))
        flakes[y0:y1, x0:x1] = np.maximum(flakes[y0:y1, x0:x1], k * a)
    c.over(hexc("#E6EAF8"), flakes)

    v = c.radial(W * 0.55, H * 0.45, W * 0.75, H * 0.85)
    c.mul(hexc("#05070F"), np.clip(v - 0.55, 0, 1) * 0.45)
    return c


# ======================================================================================================
# 1.19.0 Right answers
# ======================================================================================================
def moon_disc(c, mx, my, mr, seed=3):
    """A near-full moon: soft basalt seas in the real layout, gentle limb darkening, a cool halo. No holes."""
    d = c.radial(mx, my, mr)
    disc = np.clip((1 - d) * mr / 1.2 + 0.5, 0, 1)
    col = np.stack([np.full((c.h, c.w), v, np.float32) for v in hexc("#F3EFE3")], -1)
    seas = np.zeros((c.h, c.w), np.float32)
    for (sx, sy, rx, ry) in [(-0.30, -0.28, 0.30, 0.24), (0.02, -0.30, 0.22, 0.18), (0.22, -0.04, 0.24, 0.20),
                             (-0.45, 0.05, 0.22, 0.34), (-0.12, 0.10, 0.18, 0.14), (0.30, 0.30, 0.14, 0.12),
                             (0.44, -0.30, 0.10, 0.09), (-0.20, 0.42, 0.16, 0.10)]:
        seas = np.maximum(seas, c.ellipse(mx + sx * mr, my + sy * mr, rx * mr, ry * mr, mr * 0.10))
    seas *= 0.75 + 0.25 * fbm(c.h, c.w, mr * 0.22, 3, seed)
    col = col + (hexc("#9DA6BF") - col) * (seas * 0.55)[..., None]
    limb = np.clip(d, 0, 1) ** 3
    col = col + (hexc("#C9C3B4") - col) * (limb * 0.35)[..., None]
    # the thin unlit sliver on the upper left (the sun is under the horizon, lower right)
    cover = c.ellipse(mx - mr * 1.86, my - mr * 0.80, mr * 1.05, mr * 1.05, 1.0)
    term = np.clip(cover * disc, 0, 1)
    c.add(hexc("#DCE6FF"), np.exp(-(c.radial(mx, my, mr * 4.5)) ** 2 * 2.2) * 0.16)
    c.add(hexc("#C8D6FF"), np.exp(-(c.radial(mx, my, mr * 12)) ** 2 * 2.0) * 0.10)
    c.over(col, disc)
    c.over(hexc("#2A3866"), term * 0.82)


def answers():
    c = Canvas(W, H)
    hz = 0.585 * H
    c.px = c.vgrad([(0, "#060B20"), (0.35, "#0D1838"), (0.72, "#1D2D5B"), (1.0, "#33487E")], 0, hz)
    c.px[int(hz):] = hexc("#33487E")
    mx, my, mr = 0.585 * W, 0.235 * H, 50.0
    d = c.radial(mx, my, 0.55 * W, 0.70 * H)
    c.add(hexc("#5D78B8"), np.exp(-d ** 2 * 2.0) * 0.30)

    def sky_lum(y):
        t = min(1.0, max(0.0, y / hz))
        return 0.03 + 0.22 * t ** 2

    stars(c, 420, 23, hz * 0.97, sky_lum, near_moon=(mx, my, mr), warm=0.08)
    moon_disc(c, mx, my, mr)

    xs = np.arange(W, dtype=np.float32)
    # far hills, layered, with moonlit crowns and haze between them
    layers = [(hz + 4, 0.075, "#2A3A66", "#304373", 4, 0.50, 1.8), (hz + 0.040 * H, 0.070, "#1F2D4C", "#27385A", 5, 0.40, 2.6),
              (hz + 0.085 * H, 0.045, "#172438", "#1E2E46", 6, 0.30, 3.4)]
    for i, (yb, amp, top, bot, seed, haze, f) in enumerate(layers):
        n = fbm1d(W, W / f, 7, 40 + seed)
        ridge = yb - H * amp * (0.30 + 2.6 * (n - 0.28).clip(0) ** 1.2)
        m = c.below_curve(ridge, 1.3)
        col = c.vgrad([(0, top), (1, bot)], ridge.min(), yb + 0.08 * H)
        # moonlight on the slopes that face up toward it, fading down each hill
        sl = np.gradient(blur(np.repeat(ridge[None, :], 3, 0), 16)[1])
        facing = np.clip(np.where(xs < mx, -1, 1) * sl * 2.0 + 0.25, 0, 1)
        lit = facing[None, :] * np.exp(-np.clip(c.yy - ridge[None, :], 0, None) / (26 + 10 * i)) * (0.55 + 0.8 * fbm(H, W, 30, 3, 60 + i))
        col = col + (hexc("#5F78AE") - col) * (lit * (0.45 - 0.1 * i))[..., None]
        c.over(col, m)
        c.add(hexc("#6E86C0"), np.exp(-((c.yy - yb) / 16) ** 2) * haze * 0.30 * m)

    # the meadow: broad rolling ground under moonlight, fine grass only near the viewer
    gtop = hz + 0.105 * H
    ground = c.below_curve(np.full(W, gtop, np.float32) - 6 * fbm1d(W, 240, 4, 9), 1.5)
    gcol = c.vgrad([(0, "#2A3D58"), (0.35, "#1C2C42"), (1, "#0D1724")], gtop, H)
    T = fbm(512, 512, 90, 4, 55)
    dy = np.clip(c.yy - gtop + 14, 1, None)
    roll = blur(tex_sample(T, (c.xx - W / 2) / dy * 8 + 200, 4200 / dy), 2)
    rsh = np.clip(-np.gradient(roll, axis=0) * np.clip(dy / 90, 0.3, 3) * 26, -1, 1)
    gcol = gcol + (hexc("#4B6188") - gcol) * (np.clip(rsh, 0, 1) * 0.40)[..., None]
    gcol = gcol + (hexc("#0B1420") - gcol) * (np.clip(-rsh, 0, 1) * 0.35)[..., None]
    blades = fbm(H, W, 2.5, 2, 12)
    near_f = smooth(gtop + 0.12 * H, H, c.yy)
    gcol = gcol + (hexc("#6A80A6") - gcol) * (np.clip((blades - 0.62) * 3, 0, 1) * 0.16 * near_f)[..., None]
    c.over(gcol, ground)

    # the road: from the near left, a fork on the mid ground, the lit branch running on toward the moon
    def road_band(path, w0, w1, soft):
        left, right = [], []
        for i, (x, y) in enumerate(path):
            t = i / (len(path) - 1)
            w = w0 + (w1 - w0) * t
            left.append((x - w / 2, y))
            right.append((x + w / 2, y))
        return c.poly(left + right[::-1], soft)

    def curve(p0, p1, p2, p3, n=60):
        pts = []
        for i in range(n + 1):
            t = i / n
            a = (1 - t) ** 3
            b = 3 * (1 - t) ** 2 * t
            cc = 3 * (1 - t) * t * t
            dd = t ** 3
            pts.append((a * p0[0] + b * p1[0] + cc * p2[0] + dd * p3[0], a * p0[1] + b * p1[1] + cc * p2[1] + dd * p3[1]))
        return pts

    fork = (0.505 * W, 0.745 * H)
    near = curve((0.30 * W, H + 30), (0.36 * W, 0.93 * H), (0.47 * W, 0.80 * H), fork)
    main = curve(fork, (0.535 * W, 0.71 * H), (0.575 * W, 0.68 * H), (mx + 4, gtop - 1))
    side = curve(fork, (0.47 * W, 0.725 * H), (0.38 * W, 0.70 * H), (0.30 * W, gtop + 4))
    rm = np.maximum(road_band(near, 300, 46, 1.2), np.maximum(road_band(main, 46, 7, 1.0), road_band(side, 40, 10, 1.0)))
    rm *= ground
    # the road's far end thins into the grass rather than stopping on a line
    rm *= np.clip((c.yy - gtop) / 26, 0, 1) ** 0.8
    # stone and dust: lighter where it runs toward the moon (forward scatter on worn stone), darker on the side branch
    far_f = np.clip(1 - (c.yy - gtop) / (0.30 * H), 0, 1) ** 1.5
    toward = np.exp(-((c.xx - mx) / (0.06 * W + (c.yy - gtop) * 0.7)) ** 2) * far_f
    side_m = road_band(side, 40, 10, 1.0) * (1 - road_band(main, 46, 7, 1.0))
    rcol = c.vgrad([(0, "#7484AC"), (0.30, "#4E5B80"), (1, "#2A344F")], gtop, H)
    stone = fbm(H, W, 6, 3, 81)
    ruts = np.exp(-((stone - 0.5) / 0.08) ** 2)
    rcol = rcol * (0.84 + 0.26 * stone)[..., None]
    rcol = rcol + (hexc("#1E2840") - rcol) * (ruts * 0.18)[..., None]
    rcol = 1 - (1 - rcol) * (1 - hexc("#D6E0FF") * (toward * 0.55)[..., None])
    rcol = rcol + (hexc("#26324E") - rcol) * (side_m * 0.45 * smooth(gtop, 0.80 * H, c.yy) + side_m * 0.25)[..., None]
    c.over(rcol, rm)
    # verge: the grass edge throws a soft occlusion onto the road's sides
    edge = np.clip(blur(rm, 6) - rm, 0, 1)
    c.mul(hexc("#0B1220"), edge * 0.8)

    # a lone tree on the right rise, rim-lit on its moon side
    tx, ty = 0.80 * W, gtop + 0.006 * H
    canopy = np.zeros((H, W), np.float32)
    rng = np.random.default_rng(17)
    for _ in range(70):
        ang = rng.random() * math.pi * 2
        rad = rng.random() ** 0.6
        ox, oy = math.cos(ang) * rad * 46, -96 + math.sin(ang) * rad * 30
        canopy = np.maximum(canopy, c.ellipse(tx + ox, ty + oy, 9 + rng.random() * 9, 7 + rng.random() * 6, 0.8))
    ragged = fbm(H, W, 4, 2, 18)
    canopy = canopy * (blur(canopy, 6) > 0.5) + canopy * (ragged > 0.5) * (blur(canopy, 6) <= 0.5)
    canopy = blur(np.clip(canopy, 0, 1).astype(np.float32), 0.7)
    trunk = c.poly([(tx - 4, ty), (tx + 4, ty), (tx + 2.5, ty - 70), (tx - 2.5, ty - 70)], 0.6)
    treem = np.maximum(canopy, trunk)
    c.over(hexc("#0B1322"), treem)
    rimt = np.clip(treem - np.roll(np.roll(treem, 2, axis=1), 2, axis=0), 0, 1)  # edges facing up-left, toward the moon
    c.add(hexc("#8EA4D6"), rimt * 0.45)

    # the signpost at the fork, with its shadow falling toward the viewer and left, away from the moon
    px0, py0 = fork[0] + 64, fork[1] + 16
    shadowp = c.poly([(px0 - 3, py0), (px0 + 3, py0), (px0 - 74, py0 + 66), (px0 - 82, py0 + 64)], 1.5)
    c.mul(hexc("#0A1222"), shadowp * 0.55)
    post = c.poly([(px0 - 3.2, py0), (px0 + 3.2, py0), (px0 + 2.6, py0 - 104), (px0 - 2.6, py0 - 104)], 0.5)
    arm1 = c.poly([(px0 - 2, py0 - 94), (px0 + 52, py0 - 102), (px0 + 61, py0 - 96), (px0 + 52, py0 - 90), (px0 - 2, py0 - 84)], 0.5)  # toward the lit road
    arm2 = c.poly([(px0 + 2, py0 - 74), (px0 - 44, py0 - 70), (px0 - 52, py0 - 65), (px0 - 44, py0 - 60), (px0 + 2, py0 - 64)], 0.5)  # toward the side road
    sign = np.maximum(post, np.maximum(arm1, arm2))
    c.over(hexc("#1A1A24"), sign)
    rims = np.clip(sign - np.roll(np.roll(sign, 2, axis=0), -1, axis=1), 0, 1)  # top and right edges, moon side
    c.add(hexc("#AFC0EA"), rims * 0.65)
    # two waystones by the main road, their tops lit
    for sx, sy, sr in [(0.555 * W, 0.705 * H, 6), (0.405 * W, 0.86 * H, 13)]:
        st = c.ellipse(sx, sy, sr, sr * 1.25, 0.6) * (c.yy < sy + sr * 0.6)
        c.mul(hexc("#0A1222"), c.ellipse(sx - sr * 1.2, sy + sr * 0.9, sr * 1.8, sr * 0.45, 2) * 0.5)
        c.over(hexc("#323C55"), st)
        c.add(hexc("#9FB2E0"), top_edge(st, 2) * 0.55)

    # low mist lying in the far valley
    mist = np.exp(-((c.yy - (hz + 0.06 * H)) / 18) ** 2) * fbm(H, W, 120, 3, 5)
    c.add(hexc("#8197CF"), mist * 0.16)

    # tall grass at the near corners frames the view; tips catch the moon
    tuft = np.zeros((H, W), np.float32)
    rng = np.random.default_rng(29)
    for side_x, span in [(0.0, 0.20), (0.82, 0.18)]:
        for _ in range(150):
            bx = (side_x + rng.random() * span) * W
            hgt = 50 + rng.random() ** 1.5 * 150 * (1.0 - abs((bx / W) - (side_x + span / 2)) / span)
            lean = rng.normal(0, 22)
            w0 = 2.0 + rng.random() * 2.2
            tuft = np.maximum(tuft, c.poly([(bx - w0, H + 2), (bx + w0, H + 2), (bx + lean + 0.6, H - hgt), (bx + lean - 0.6, H - hgt)], 0.6))
    c.over(hexc("#08101B"), tuft)
    tips = np.clip(tuft - np.roll(np.roll(tuft, 2, axis=0), -1, axis=1), 0, 1) * smooth(H, H - 0.30 * H, c.yy)
    c.add(hexc("#7F96C8"), tips * 0.35)

    v = c.radial(W * 0.55, H * 0.42, W * 0.78, H * 0.85)
    c.mul(hexc("#03050C"), np.clip(v - 0.55, 0, 1) * 0.5)
    return c


if __name__ == "__main__":
    evercold().save(OUT / "evercold-base.png", OUTSIZE, grain=0.012, seed=1)
    print("evercold-base.png")
    answers().save(OUT / "answers-base.png", OUTSIZE, grain=0.012, seed=2)
    print("answers-base.png")
