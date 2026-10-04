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


def crescent(c, mx, my, mr, ux, uy, k):
    """A crescent with a true (elliptical) terminator, so its horns are diametrically opposite. (ux, uy) points toward
    the sun; k is the terminator's half-width as a fraction of the radius (0 = half moon, 1 = new moon)."""
    dx, dy = (c.xx - mx) / mr, (c.yy - my) / mr
    a = dx * ux + dy * uy
    b = -dx * uy + dy * ux
    rr = np.sqrt(dx * dx + dy * dy)
    disc = np.clip((1 - rr) * mr + 0.5, 0, 1)
    term = k * np.sqrt(np.clip(1 - b * b, 0, 1))
    lit = np.clip((a - term) * mr + 0.5, 0, 1) * disc
    return disc, lit


def flakes(c, n, seed, avoid, sky_bottom, near_scale=1.0):
    """Falling snow: few, slow, larger and softer the nearer they are. No flake over the moon, and no small (far,
    sharp) flake against the sky or the ridges, where it would read as a star."""
    rng = np.random.default_rng(seed)
    out = np.zeros((c.h, c.w), np.float32)
    mx, my, mr = avoid
    for _ in range(n):
        x, y = rng.random() * c.w, rng.random() * c.h
        z = rng.random() ** 2
        r = 1.3 + 6.5 * z * near_scale
        a = 0.55 - 0.32 * z
        if math.hypot(x - mx, y - my) < mr + 3 * r + 4:
            continue
        if r < 4.0 and y < sky_bottom:
            continue
        x0, x1 = int(max(0, x - 3 * r)), int(min(c.w, x + 3 * r + 1))
        y0, y1 = int(max(0, y - 3 * r)), int(min(c.h, y + 3 * r + 1))
        yy, xx = np.mgrid[y0:y1, x0:x1]
        k = np.exp(-(((xx - x) ** 2 + (yy - y) ** 2) / (2 * (r * 0.55) ** 2)))
        out[y0:y1, x0:x1] = np.maximum(out[y0:y1, x0:x1], k * a)
    return out


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
    disc, lit = crescent(c, mx, my, mr, ux, uy, 0.62)
    c.over(hexc("#22305E"), disc * 0.40)  # earthshine: the dark limb barely a shade over the sky
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
    near_pass = np.exp(-((xs - gx) / (0.25 * W)) ** 2)[None, :]
    farcol = farcol + (hexc("#9E93BE") - farcol) * (np.clip(ffacing, 0, 1)[None, :] * near_pass * depth * 0.45)[..., None]
    farcol = farcol + (hexc("#434A7E") - farcol) * (np.clip(-ffacing, 0, 1)[None, :] * depth * 0.35)[..., None]
    snow_n = fbm(H, W, 60, 5, 21)
    farcol = farcol + (hexc("#A7A9CF") - farcol) * (np.clip((snow_n - 0.50) * 3, 0, 1) * smooth(hz, far.min(), c.yy) * 0.22 * (0.4 + 0.6 * near_pass))[..., None]
    # a backlit range is never brighter than the sky behind it: cap it at 0.9x the sky's luminance at its ridgeline
    ridge_rows = np.clip(far.astype(int) - 3, 0, H - 1)
    sky_at = c.px[ridge_rows, np.arange(W)]
    sky_l = blur(np.repeat((sky_at @ np.array([0.2126, 0.7152, 0.0722], np.float32))[None, :], 3, 0), 24)[1]
    far_l = farcol @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    k = np.minimum(1.0, 0.9 * sky_l[None, :] / np.maximum(far_l, 1e-4))
    farcol = farcol * k[..., None]
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

    # falling snow: few, slow, larger and softer the nearer they are; never over the moon, never star-like in the sky
    c.over(hexc("#E6EAF8"), flakes(c, 90, 91, (mx, my, mr), base + 12))

    v = c.radial(W * 0.55, H * 0.45, W * 0.75, H * 0.85)
    c.mul(hexc("#05070F"), np.clip(v - 0.55, 0, 1) * 0.45)
    return c


# ======================================================================================================
# 1.19.0 Right answers
# ======================================================================================================
def moon_disc(c, mx, my, mr, seed=3, centre=False):
    """A near-full moon: soft basalt seas in the real layout, gentle limb darkening, a cool halo. No holes."""
    d = c.radial(mx, my, mr)
    disc = np.clip((1 - d) * mr / 1.2 + 0.5, 0, 1)
    col = np.stack([np.full((c.h, c.w), v, np.float32) for v in hexc("#F3EFE3")], -1)
    # the seas as connected chains, as the near side shows them: Procellarum and Imbrium on the left, Serenitatis,
    # Tranquillitatis and Fecunditatis running down the right, Nubium below; one union at one opacity, ragged edges
    seas = np.zeros((c.h, c.w), np.float32)
    chains = [[(-0.42, -0.18, 0.20, 0.26), (-0.50, 0.10, 0.18, 0.28), (-0.38, 0.32, 0.16, 0.16), (-0.24, -0.32, 0.22, 0.17), (-0.10, -0.24, 0.12, 0.10)],
              [(0.04, -0.34, 0.17, 0.14), (0.18, -0.16, 0.16, 0.15), (0.30, 0.04, 0.18, 0.14), (0.40, 0.24, 0.12, 0.13), (0.50, -0.30, 0.09, 0.08)],
              [(-0.16, 0.30, 0.15, 0.10), (-0.02, 0.36, 0.10, 0.08)]]
    if centre:   # Insularum and Vaporum join the chains across the middle, so the seas never read as a ring or a "C"
        chains.append([(-0.24, 0.02, 0.15, 0.11), (-0.04, -0.06, 0.12, 0.09), (0.12, 0.06, 0.10, 0.08)])
    for chain in chains:
        for (sx, sy, rx, ry) in chain:
            seas = np.maximum(seas, c.ellipse(mx + sx * mr, my + sy * mr, rx * mr, ry * mr, 0.6))
    seas = blur(seas, mr * 0.08) * (0.78 + 0.22 * fbm(c.h, c.w, mr * 0.18, 3, seed))
    col = col + (hexc("#A3A39C") - col) * (np.clip(seas, 0, 1) * 0.50)[..., None]
    limb = np.clip(d, 0, 1) ** 3
    col = col + (hexc("#C9C3B4") - col) * (limb * 0.35)[..., None]
    # the thin unlit sliver on the upper left (the sun is under the horizon, lower right): about 0.09 r deep
    # a true terminator: an ellipse from horn to horn, 0.09 r wide at the limb's middle, nothing at the horns
    ux, uy = 0.919, 0.395
    dx, dy = (c.xx - mx) / mr, (c.yy - my) / mr
    a = dx * ux + dy * uy
    b = -dx * uy + dy * ux
    term = np.clip((-0.91 * np.sqrt(np.clip(1 - b * b, 0, 1)) - a) * mr + 0.5, 0, 1) * disc
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

    stars(c, 230, 23, hz * 0.97, sky_lum, near_moon=(mx, my, mr), warm=0.08)  # a near-full moon washes out the faint ones
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
    # its shadow: faint and foreshortened, toward the viewer and right, away from the moon (up and left of it)
    c.mul(hexc("#0A1222"), c.poly([(tx - 5, ty), (tx + 5, ty), (tx + 96, ty + 30), (tx + 40, ty + 34)], 4) * 0.45)
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
    # rims on the edges that face the moon: up and right for the left tuft, up and left for the right one
    rim_l = np.clip(tuft - np.roll(np.roll(tuft, 2, axis=0), -1, axis=1), 0, 1)
    rim_r = np.clip(tuft - np.roll(np.roll(tuft, 2, axis=0), 1, axis=1), 0, 1)
    tips = np.where(c.xx < mx, rim_l, rim_r) * smooth(H, H - 0.30 * H, c.yy) * 1.4
    c.add(hexc("#7F96C8"), tips * 0.35)

    v = c.radial(W * 0.55, H * 0.42, W * 0.78, H * 0.85)
    c.mul(hexc("#03050C"), np.clip(v - 0.55, 0, 1) * 0.5)
    return c


def hills(c, hz, xs, light_x, layers):
    """Layered far hills under a night sky: moonlit on the slopes that face light_x, haze where each meets the next."""
    for i, (yb, amp, top, bot, seed, haze, f) in enumerate(layers):
        n = fbm1d(c.w, c.w / f, 7, 40 + seed)
        ridge = yb - c.h * amp * (0.30 + 2.6 * (n - 0.28).clip(0) ** 1.2)
        m = c.below_curve(ridge, 1.3)
        col = c.vgrad([(0, top), (1, bot)], ridge.min(), yb + 0.08 * c.h)
        sl = np.gradient(blur(np.repeat(ridge[None, :], 3, 0), 16)[1])
        facing = np.clip(np.where(xs < light_x, -1, 1) * sl * 2.0 + 0.2, 0, 1)
        lit = facing[None, :] * np.exp(-np.clip(c.yy - ridge[None, :], 0, None) / (26 + 10 * i)) * (0.55 + 0.8 * fbm(c.h, c.w, 30, 3, 60 + i))
        col = col + (hexc("#5F78AE") - col) * (lit * (0.40 - 0.1 * i))[..., None]
        c.over(col, m)
        c.add(hexc("#6E86C0"), np.exp(-((c.yy - yb) / 16) ** 2) * haze * 0.30 * m)


def bezier(p0, p1, p2, p3, n=60):
    pts = []
    for i in range(n + 1):
        t = i / n
        a, b, cc, dd = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3
        pts.append((a * p0[0] + b * p1[0] + cc * p2[0] + dd * p3[0], a * p0[1] + b * p1[1] + cc * p2[1] + dd * p3[1]))
    return pts


def band(c, path, w0, w1, soft):
    left, right = [], []
    for i, (x, y) in enumerate(path):
        w = w0 + (w1 - w0) * i / (len(path) - 1)
        left.append((x - w / 2, y))
        right.append((x + w / 2, y))
    return c.poly(left + right[::-1], soft)


# ======================================================================================================
# 1.22.0 Welcome home: a door left open on a lit hall
# ======================================================================================================
def welcome():
    """Two lights: the near-full moon high on the left, in front of the viewer, so it backlights the house (the front
    is in shadow, the roof and the left wall carry a cool rim, and the house's moon shadow falls toward the viewer and
    right); and the warm hall seen through the open door, which spills a widening pool down the path toward you."""
    c = Canvas(W, H)
    hz = 0.64 * H
    c.px = c.vgrad([(0, "#060B20"), (0.40, "#0E1A3C"), (0.80, "#1E2E5C"), (1.0, "#2E4278")], 0, hz)
    c.px[int(hz):] = hexc("#2E4278")
    mx, my, mr = 0.20 * W, 0.21 * H, 40.0
    c.add(hexc("#5D78B8"), np.exp(-(c.radial(mx, my, 0.45 * W, 0.6 * H)) ** 2 * 2.0) * 0.28)

    def sky_lum(y):
        t = min(1.0, max(0.0, y / hz))
        return 0.03 + 0.22 * t ** 2

    stars(c, 220, 31, hz * 0.97, sky_lum, near_moon=(mx, my, mr), warm=0.08)
    moon_disc(c, mx, my, mr, seed=5)
    xs = np.arange(W, dtype=np.float32)
    hills(c, hz, xs, mx, [(hz + 4, 0.060, "#283863", "#2E4070", 7, 0.45, 2.0), (hz + 0.045 * H, 0.050, "#1C2A47", "#243552", 8, 0.35, 3.0)])
    gtop = hz + 0.085 * H
    ground = c.below_curve(np.full(W, gtop, np.float32) - 5 * fbm1d(W, 240, 4, 19), 1.5)
    gcol = c.vgrad([(0, "#24364F"), (0.35, "#192839"), (1, "#0B1420")], gtop, H)
    blades = fbm(H, W, 2.5, 2, 22)
    gcol = gcol + (hexc("#5C7394") - gcol) * (np.clip((blades - 0.62) * 3, 0, 1) * 0.12 * smooth(gtop + 0.1 * H, H, c.yy))[..., None]
    c.over(gcol, ground)

    # the house, right of centre: facade in the moon's shadow, a cool rim on the roof's left slope and the left wall
    x0, yb = 0.565 * W, 0.80 * H
    x1 = 0.795 * W
    wall_top = yb - 0.20 * H
    peak = (0.5 * (x0 + x1), wall_top - 0.15 * H)
    # its moon shadow first: toward the viewer and right, soft, over the ground
    c.mul(hexc("#0A1220"), c.poly([(x0, yb), (x1, yb), (x1 + 0.17 * W, H + 20), (x0 + 0.08 * W, H + 20)], 14) * 0.45)
    facade = c.poly([(x0, yb), (x1, yb), (x1, wall_top), (x0, wall_top)], 0.6)
    roof = c.poly([(x0 - 18, wall_top + 4), peak, (x1 + 18, wall_top + 4)], 0.6)
    chim = c.poly([(x1 - 92, wall_top - 0.10 * H), (x1 - 66, wall_top - 0.10 * H), (x1 - 66, wall_top - 0.02 * H), (x1 - 92, wall_top - 0.02 * H)], 0.6)
    body = np.maximum(np.maximum(facade, roof), chim)
    fcol = c.vgrad([(0, "#1B2234"), (1, "#141A29")], wall_top, yb)
    stonew = fbm(H, W, 5, 2, 37)
    fcol = fcol * (0.92 + 0.14 * stonew)[..., None]
    c.over(fcol, facade)
    c.over(hexc("#121826"), np.maximum(roof, chim))
    # cool rim: the roof's left slope and the chimney top face the moon
    rim = np.clip(body - np.roll(np.roll(body, 2, axis=0), 2, axis=1), 0, 1) * (c.xx < peak[0] + 30)
    rim = np.maximum(rim, np.clip(chim - np.roll(chim, 2, axis=0), 0, 1))
    c.add(hexc("#9FB2E0"), rim * 0.55)
    # smoke from the chimney, lit faintly by the moon, drifting right
    cx_ = x1 - 79
    sy0 = wall_top - 0.10 * H
    up = np.clip(sy0 - c.yy, 0, None)
    smoke = np.exp(-((c.xx - (cx_ + up * 0.6)) / (5 + up * 0.10)) ** 2) * np.clip(up / 18, 0, 1) * np.clip(1 - up / 210, 0, 1) * fbm(H, W, 20, 3, 71)
    c.add(hexc("#8A96C0"), blur(smoke, 3) * 0.20)

    # the open door: a warm hall inside, its jamb in depth on the left, the door leaf swung in on the right
    dx0, dx1, dy0 = 0.655 * W, 0.700 * W, yb - 0.125 * H
    door = c.poly([(dx0, yb), (dx1, yb), (dx1, dy0), (dx0, dy0)], 0.5)
    hall = c.vgrad([(0, "#E59A55"), (0.55, "#FFC27E"), (1, "#FFD9A0")], dy0, yb)
    lamp = np.exp(-((c.xx - (dx0 + dx1) / 2 - 6) ** 2 + (c.yy - (dy0 + 26)) ** 2) / (2 * 14 ** 2))
    hall = 1 - (1 - hall) * (1 - hexc("#FFF0CC") * (lamp * 0.8)[..., None])
    c.over(hall, door)
    jamb = c.poly([(dx0, yb), (dx0 + 9, yb - 4), (dx0 + 9, dy0 + 6), (dx0, dy0)], 0.5)
    c.over(hexc("#7A4A26"), jamb * 0.85)
    leaf = c.poly([(dx1, yb), (dx1 - 14, yb - 6), (dx1 - 14, dy0 + 8), (dx1, dy0)], 0.5)
    c.over(hexc("#3A2416"), leaf)
    c.add(hexc("#FFB466"), blur(door, 10) * 0.40 * (1 - door))
    # two windows, warm, with mullions
    win = np.zeros((H, W), np.float32)
    for wx in (0.600 * W, 0.755 * W):
        win = np.maximum(win, c.poly([(wx - 22, yb - 0.115 * H), (wx + 22, yb - 0.115 * H), (wx + 22, yb - 0.065 * H), (wx - 22, yb - 0.065 * H)], 0.5))
    mull = ((np.abs(c.xx - 0.600 * W) < 1.6) | (np.abs(c.xx - 0.755 * W) < 1.6) | (np.abs(c.yy - (yb - 0.09 * H)) < 1.6)) * win
    c.over(hexc("#FFC57E"), win)
    c.over(hexc("#2A1B12"), mull)
    c.add(hexc("#FFB466"), blur(win, 9) * 0.30 * (1 - win))

    # the spill: a pool widening down the path from the door sill, fading with distance; the window pools are faint
    dcx = (dx0 + dx1) / 2
    dist = np.clip(c.yy - yb, 0, None)
    wid = (dx1 - dx0) / 2 + dist * 0.9
    spill = np.exp(-((c.xx - dcx) / np.maximum(wid, 1)) ** 4) * (c.yy > yb) / (1 + dist / 60) ** 1.4
    for wx in (0.600 * W, 0.755 * W):
        spill = spill + np.exp(-((c.xx - wx) / (30 + dist * 0.6)) ** 2) * (c.yy > yb) * np.exp(-dist / 40) * 0.25
    c.add(hexc("#FFB062"), np.clip(spill, 0, 1) * 0.55)

    # the stone path from the viewer to the door: stones lit warm near the door, cool and dim further out
    path = bezier((0.47 * W, H + 30), (0.52 * W, 0.93 * H), (0.62 * W, 0.86 * H), (dcx, yb + 2))
    pm = band(c, path, 220, 44, 1.2)
    stones = fbm(H, W, 5, 3, 91)
    joints = np.exp(-((stones - 0.5) / 0.05) ** 2)
    pcol = c.vgrad([(0, "#3A4560"), (1, "#222A3C")], yb, H)
    pcol = pcol * (0.85 + 0.25 * stones)[..., None]
    pcol = pcol + (hexc("#0C111C") - pcol) * (joints * 0.5)[..., None]
    pcol = 1 - (1 - pcol) * (1 - hexc("#FFB062") * (np.clip(spill, 0, 1) * 0.65)[..., None])
    c.over(pcol, pm)
    # two shrubs by the door, warm on the side that faces it, dark otherwise
    for sx, sr in ((dx0 - 46, 30), (dx1 + 50, 26)):
        top = yb - sr * 1.25 + 7 * fbm1d(W, 9, 3, int(sx)) + (((xs - sx) / (sr * 1.1)) ** 2) * sr * 1.2
        sh = c.below_curve(top, 1.0) * (np.abs(c.xx - sx) < sr * 1.15) * (c.yy < yb + 2)
        sh = blur(sh.astype(np.float32), 0.8)
        c.over(hexc("#0C1420"), sh)
        side = 1 if sx < dcx else -1
        c.add(hexc("#FFB062"), np.clip(sh - np.roll(sh, -3 * side, axis=1), 0, 1) * 0.5)

    v = c.radial(W * 0.55, H * 0.45, W * 0.78, H * 0.85)
    c.mul(hexc("#03050C"), np.clip(v - 0.55, 0, 1) * 0.5)
    return c


# ======================================================================================================
# 1.21.0 What next, for every character: a lantern at a crossroads, paths to several lights
# ======================================================================================================
def whatnext():
    """Late twilight. The sun has set off to the right: a faint warm afterglow low on that side, and a young crescent
    lit on its lower-right limb, toward it. On the ground, one local light: a lantern on a post by a waystone at a
    crossroads. Its pool is foreshortened; the waystone is warm on the lantern side and throws its shadow away from it.
    Four paths leave the crossroads; three run toward distant lights (a hamlet, a tower, a farm): every character's
    next step."""
    c = Canvas(W, H)
    hz = 0.60 * H
    c.px = c.vgrad([(0, "#070D25"), (0.40, "#10204A"), (0.78, "#24406E"), (1.0, "#3C5A88")], 0, hz)
    c.px[int(hz):] = hexc("#3C5A88")
    c.add(hexc("#D9A07A"), np.exp(-(c.radial(1.05 * W, hz + 0.02 * H, 0.45 * W, 0.22 * H)) ** 2 * 1.6) * 0.30)
    # the afterglow: a low peach band on the right, about 8 % of the sky's height, fading to the left
    band_y = np.exp(-((c.yy - (hz - 0.035 * hz)) / (0.045 * hz)) ** 2)
    c.add(hexc("#F2B08C"), band_y * smooth(0.35 * W, 1.0 * W, c.xx) * 0.32)
    mx, my, mr = 0.80 * W, 0.22 * H, 24.0

    def sky_lum(y):
        t = min(1.0, max(0.0, y / hz))
        return 0.04 + 0.32 * t ** 2

    stars(c, 260, 41, hz * 0.95, sky_lum, near_moon=(mx, my, mr), warm=0.06)
    ux, uy = (1.02 * W - mx), (hz + 0.15 * H - my)
    n = math.hypot(ux, uy)
    disc, lit = crescent(c, mx, my, mr, ux / n, uy / n, 0.55)
    c.over(hexc("#1E3260"), disc * 0.35)
    c.add(hexc("#F6EAD2"), np.exp(-(c.radial(mx, my, mr * 3)) ** 2 * 1.6) * 0.06)
    c.over(hexc("#F6EDD8"), lit)
    xs = np.arange(W, dtype=np.float32)
    hills(c, hz, xs, 1.0 * W, [(hz + 4, 0.065, "#263962", "#2C416E", 11, 0.45, 2.2), (hz + 0.045 * H, 0.050, "#1B2A46", "#223451", 12, 0.35, 3.2)])
    gtop = hz + 0.085 * H
    ground = c.below_curve(np.full(W, gtop, np.float32) - 5 * fbm1d(W, 240, 4, 29), 1.5)
    gcol = c.vgrad([(0, "#26394F"), (0.35, "#1B2A3A"), (1, "#0C1520")], gtop, H)
    c.over(gcol, ground)

    # three distant lights where the paths lead
    lights = [(0.17 * W, hz + 0.030 * H, 4), (0.43 * W, hz - 0.035 * H, 0), (0.71 * W, hz + 0.050 * H, 3)]
    lm = np.zeros((H, W), np.float32)
    for lx, ly, k in lights:
        for j in range(k):
            lm = np.maximum(lm, c.ellipse(lx + j * 9 - k * 4, ly + (j % 2) * 3, 2.2, 2.6, 0.5))
    # the tower on the middle hill, a dark silhouette; its two lit windows sit inside it
    tx = 0.43 * W
    c.over(hexc("#141E33"), c.poly([(tx - 9, hz + 0.01 * H), (tx + 9, hz + 0.01 * H), (tx + 6, hz - 0.03 * H), (tx, hz - 0.05 * H), (tx - 6, hz - 0.03 * H)], 0.6))
    for wy_ in (hz - 0.012 * H, hz - 0.028 * H):
        lm = np.maximum(lm, c.ellipse(tx, wy_, 1.4, 2.2, 0.4))
    c.add(hexc("#FFC77A"), lm)
    c.add(hexc("#FFB060"), blur(lm, 6) * 1.6)

    # the paths: one toward the viewer, three out to the lights; pale dust under the sky's light
    X = (0.52 * W, 0.80 * H)
    paths = [bezier((0.44 * W, H + 30), (0.47 * W, 0.92 * H), (0.50 * W, 0.84 * H), X),
             bezier(X, (0.40 * W, 0.76 * H), (0.25 * W, 0.70 * H), (lights[0][0], gtop + 2)),
             bezier(X, (0.49 * W, 0.74 * H), (0.45 * W, 0.70 * H), (lights[1][0], gtop + 2)),
             bezier(X, (0.60 * W, 0.75 * H), (0.68 * W, 0.71 * H), (lights[2][0], gtop + 2))]
    pm = band(c, paths[0], 230, 40, 1.2)
    for p in paths[1:]:
        pm = np.maximum(pm, band(c, p, 38, 6, 1.0))
    pm *= np.clip((c.yy - gtop) / 22, 0, 1) ** 0.8
    dust = fbm(H, W, 6, 3, 13)
    pcol = c.vgrad([(0, "#5F7098"), (0.4, "#45537A"), (1, "#28324C")], gtop, H) * (0.88 + 0.2 * dust)[..., None]
    c.over(pcol, pm)
    c.mul(hexc("#0B1220"), np.clip(blur(pm, 5) - pm, 0, 1) * 0.7)

    # the lantern post and the waystone at the crossroads
    lx, ly = X[0] + 70, X[1] - 4
    lamp_y = ly - 0.13 * H
    # the pool: a smooth radial falloff on the ground under the lamp (the post foot, nudged toward the lantern side),
    # 4:1 wide because the ground is seen at a low angle; the far half is compressed further, never cut
    pcx, pcy = lx + 16, ly
    dyy = c.yy - pcy
    dyy = np.where(dyy < 0, dyy * 1.8, dyy)
    dist = np.sqrt((c.xx - pcx) ** 2 + (dyy * 4.0) ** 2)
    pool = np.exp(-(dist / 170) ** 2)
    c.add(hexc("#FFB466"), pool * 0.42)
    # the waystone, left of the lantern: its shadow is contact-dark at its base and runs away from the lamp's ground
    # point, left and a little toward the viewer, across the path into the grass, fading with distance
    wx, wy, wr = X[0] - 10, X[1] + 4, 20
    sh = c.poly([(wx - wr, wy - 1), (wx + wr * 0.6, wy + 1), (wx - 160, wy + 30), (wx - 176, wy + 14)], 3)
    away = np.clip((wx + wr * 0.6 - c.xx) / 176, 0, 1)
    c.mul(hexc("#0A1220"), sh * (0.75 - 0.55 * away))
    c.mul(hexc("#05080F"), c.ellipse(wx - 2, wy, wr * 1.05, 3.5, 1.5) * 0.6)  # contact
    # the post's own thin shadow, from its foot, running left
    c.mul(hexc("#0A1220"), c.poly([(lx - 3, ly), (lx + 3, ly + 1), (lx - 96, ly + 12), (lx - 100, ly + 8)], 1.5) * (0.6 - 0.45 * np.clip((lx - c.xx) / 100, 0, 1)))
    stone = c.poly([(wx - wr, wy), (wx + wr, wy), (wx + wr * 0.7, wy - wr * 2.3), (wx - wr * 0.2, wy - wr * 2.8), (wx - wr * 0.9, wy - wr * 2.0)], 0.6)
    c.over(hexc("#2C3448"), stone)
    c.add(hexc("#FFB466"), np.clip(stone - np.roll(stone, -3, axis=1), 0, 1) * 0.7)
    c.add(hexc("#8EA4D6"), top_edge(stone, 2) * 0.3)
    # the post and the lantern
    c.over(hexc("#181A24"), c.poly([(lx - 3, ly), (lx + 3, ly), (lx + 2.5, lamp_y), (lx - 2.5, lamp_y)], 0.5))
    c.over(hexc("#181A24"), c.poly([(lx - 2, lamp_y + 2), (lx + 22, lamp_y + 2), (lx + 22, lamp_y + 6), (lx - 2, lamp_y + 6)], 0.5))
    lan = c.poly([(lx + 14, lamp_y + 8), (lx + 30, lamp_y + 8), (lx + 28, lamp_y + 32), (lx + 16, lamp_y + 32)], 0.5)
    c.over(hexc("#FFD08A"), lan)
    c.add(hexc("#FFB060"), blur(lan, 8) * 1.4)
    c.add(hexc("#FFB466"), np.exp(-((c.xx - lx - 22) ** 2 + (c.yy - lamp_y - 20) ** 2) / (2 * 60 ** 2)) * 0.18)

    v = c.radial(W * 0.55, H * 0.45, W * 0.78, H * 0.85)
    c.mul(hexc("#03050C"), np.clip(v - 0.55, 0, 1) * 0.5)
    return c


if __name__ == "__main__":
    import sys
    jobs = {"evercold": evercold, "answers": answers, "welcome": welcome, "whatnext": whatnext}
    for name in sys.argv[1:] or jobs:
        jobs[name]().save(OUT / f"{name}-base.png", OUTSIZE, grain=0.012, seed=len(name))
        print(f"{name}-base.png")
