"""Option B for decision 1 (spec-1.22.md, "Option B"): a richer, painterly 1.20.0 "Before Evercold", from which
option_b_themes.py makes a distinct art treatment per theme (not only a grade).

The scene: dawn coming over Coerthas. Ishgard stands on its bluff, backlit by the sun still under the horizon behind
it; cloud undersides catch the first light; mist lies in the valley. On the near snow ridge an adventurer with a
lantern and a chocobo look toward the city: the one warm practical light in the scene. Original work, painted in
code; nothing is traced or copied (the Ishgard skyline is our own simplified silhouette of the Holy See's spires).

Light: one natural light, the sun below the horizon behind the city (gx), plus one warm practical light, the lantern.
- Clouds are lit on their undersides, warmest near gx; the city and far range are backlit (darker than the sky behind
  them) with a thin warm rim; the near ridge crest takes the dawn, its face toward us stays in blue shade.
- The figures are rim-lit on their sun side (right); their long dawn shadows run toward the viewer and left; the
  lantern lays a small warm pool on the snow at their feet.
- The waning crescent is lit on its sun-facing (lower-left) limb.

Run: py -3 paint_option_b.py -> ../optionb/evercold-b-base.png (1120 x 440); option_b_themes.py imports paint().
"""
import math
import pathlib

import numpy as np

from artlib import Canvas, blur, fbm, fbm1d, hexc, smooth
from paint_release import crescent, flakes, stars, tex_sample, top_edge

OUT = pathlib.Path(__file__).resolve().parent.parent / "optionb"
W, H = 2240, 880
LUM = np.array([0.2126, 0.7152, 0.0722], np.float32)


def kuwahara(px, r):
    """A painterly flattening: each pixel takes the mean of the least varied of its four (r+1)^2 quadrants."""
    h, w, _ = px.shape
    L = px @ LUM
    pad = r + 1

    def integral(a):
        a = np.pad(a, ((pad, pad), (pad, pad)) + ((0, 0),) * (a.ndim - 2), mode="edge")
        s = np.cumsum(np.cumsum(a, 0, dtype=np.float64), 1)
        return np.pad(s, ((1, 0), (1, 0)) + ((0, 0),) * (a.ndim - 2))

    S, S2, SC = integral(L), integral(L * L), integral(px)
    n = (r + 1) ** 2

    def q(I, dy, dx):
        y0 = pad + dy
        x0 = pad + dx
        A = I[y0:y0 + h, x0:x0 + w]
        B = I[y0:y0 + h, x0 + r + 1:x0 + r + 1 + w]
        C = I[y0 + r + 1:y0 + r + 1 + h, x0:x0 + w]
        D = I[y0 + r + 1:y0 + r + 1 + h, x0 + r + 1:x0 + r + 1 + w]
        return (D - B - C + A) / n

    best = None
    out = np.zeros_like(px)
    for dy, dx in ((-r, -r), (-r, 0), (0, -r), (0, 0)):
        m = q(S, dy, dx)
        v = q(S2, dy, dx) - m * m
        col = q(SC, dy, dx).astype(np.float32)
        if best is None:
            best = v
            out = col
        else:
            take = v < best
            best = np.where(take, v, best)
            out = np.where(take[..., None], col, out)
    return out


def paint():
    c = Canvas(W, H)
    hz = 0.60 * H
    gx, gy = 0.645 * W, hz - 0.08 * H  # the sun is under the horizon; its glow is brightest just above the far range
    c.px = c.vgrad([(0, "#060C24"), (0.28, "#0F1B45"), (0.58, "#243267"), (0.82, "#4A4D86"), (1.0, "#6E6399")], 0, hz)
    c.px[int(hz):] = hexc("#6E6399")
    d = c.radial(gx, gy, 0.55 * W, 0.42 * H)
    c.add(hexc("#F6B28A"), np.exp(-d ** 2 * 3.0) * 0.66)
    c.add(hexc("#C98FA8"), np.exp(-d ** 2 * 0.8) * 0.22)
    c.add(hexc("#FFE6BE"), np.exp(-(c.radial(gx, gy, 0.15 * W, 0.10 * H)) ** 2 * 1.3) * 0.6)
    c.add(hexc("#F4A88A"), np.exp(-((c.yy - (hz - 0.10 * H)) / (0.10 * H)) ** 2) * np.exp(-((c.xx - gx) / (0.40 * W)) ** 2) * 0.70)
    c.add(hexc("#FFD2A6"), np.exp(-((c.yy - (hz - 0.12 * H)) / (0.045 * H)) ** 2) * np.exp(-((c.xx - gx) / (0.26 * W)) ** 2) * 0.55)

    def sky_lum(y):
        t = min(1.0, max(0.0, y / hz))
        return 0.05 + 0.42 * t ** 2.0

    mx, my, mr = 0.875 * W, 0.15 * H, 30.0
    # clouds: a broken altocumulus band; undersides lit by the sun below the horizon, warmest near it
    cl = fbm(H, W, 260, 6, 404)
    cl2 = fbm(H, W, 60, 4, 405)
    band = np.exp(-((c.yy - 0.30 * H) / (0.10 * H)) ** 2) + 0.55 * np.exp(-((c.yy - 0.46 * H) / (0.05 * H)) ** 2)
    dens = np.clip((cl * 0.75 + cl2 * 0.35 - 0.47) * 3.6 * band, 0, 1)
    dens = blur(dens, 2.0)
    under = np.clip(-np.gradient(blur(dens, 5), axis=0) * 40, 0, 1)  # density falling downward: the lit underside
    near = np.exp(-((c.xx - gx) / (0.32 * W)) ** 2)
    ccol = np.stack([np.full((H, W), v, np.float32) for v in hexc("#2E3462")], -1)
    ccol = ccol + (hexc("#4A4C80") - ccol) * (np.clip(c.yy / hz, 0, 1) * 0.6)[..., None]
    lit = hexc("#E9A089") * near[..., None] + hexc("#8F7FAE") * (1 - near)[..., None]
    ccol = ccol + (lit - ccol) * np.clip(under * (0.35 + 0.65 * near), 0, 1)[..., None]
    # stars only in the clear sky, never through cloud
    stars(c, 260, 77, hz * 0.8, sky_lum, near_moon=(mx, my, mr), warm=0.05)
    c.over(ccol, dens * 0.92)
    M = {"clouds": dens.copy(), "under": under * dens}
    # the crescent, waning, lit toward the sun (lower left)
    ux, uy = gx - mx, (gy + 0.25 * H) - my
    nn = math.hypot(ux, uy)
    disc, litm = crescent(c, mx, my, mr, ux / nn, uy / nn, 0.6)
    M["moon"], M["moonlit"] = disc, litm
    c.over(hexc("#24305E"), disc * 0.4)
    c.add(hexc("#F6EAD2"), np.exp(-(c.radial(mx, my, mr * 3.2)) ** 2 * 1.6) * 0.07)
    c.over(hexc("#F6EDD8"), litm)
    # faint crepuscular rays from the sun's place: shadows of the far peaks cast up into the haze
    ang = np.arctan2(c.yy - gy, c.xx - gx)
    rays = fbm1d(720, 22, 3, 9)[(((ang + math.pi) / (2 * math.pi)) * 719).astype(int)]
    c.add(hexc("#F2B994"), np.clip(rays - 0.45, 0, 1) * 0.10 * np.exp(-d ** 2 * 1.8) * (c.yy < hz))

    xs = np.arange(W, dtype=np.float32)
    pass_dip = np.exp(-((xs - gx) / (0.22 * W)) ** 2)

    def ridge_layer(base, amp, cell, seed, top, bot, rimk, cap=0.9, haze=0.0):
        r = base - H * amp * (0.25 + fbm1d(W, cell, 7, seed) ** 1.4) * (1 - 0.7 * pass_dip)
        r = r - 6 * (fbm1d(W, 18, 3, seed + 1) - 0.5)
        m = c.below_curve(r, 1.2)
        col = c.vgrad([(0, top), (1, bot)], r.min(), hz + 0.08 * H)
        sky_at = c.px[np.clip(r.astype(int) - 3, 0, H - 1), np.arange(W)]
        sky_l = blur(np.repeat((sky_at @ LUM)[None, :], 3, 0), 20)[1]
        k = np.minimum(1.0, cap * sky_l[None, :] / np.maximum(col @ LUM, 1e-4))
        col = col * k[..., None]
        c.over(col, m)
        rim = np.clip(c.below_curve(r, 1) - c.below_curve(r + 2.5, 1.2), 0, 1) * (np.exp(-((xs - gx) / (0.22 * W)) ** 2) * rimk + 0.03)[None, :]
        c.add(hexc("#FFD2A8"), rim * 0.7)
        if haze:
            c.add(hexc("#C9A7C4"), np.exp(-((c.yy - base) / 22) ** 2) * haze * m)
        M["far"] = np.maximum(M.get("far", 0), m)
        return r

    ridge_layer(hz - 0.01 * H, 0.17, W / 4.0, 501, "#5A5C92", "#77729F", 0.9, 0.9, 0.18)

    # Ishgard on its bluff, backlit: a hazy violet silhouette, a warm rim on its sun-facing edges, a few windows
    bx0, bx1 = 0.45 * W, 0.82 * W
    top_y = 0.50 * H
    # a rocky outcrop: an irregular top, steep broken flanks, never a straight edge
    prof = smooth(bx0 - 0.02 * W, bx0 + 0.035 * W, xs) * (1 - smooth(bx1 - 0.035 * W, bx1 + 0.02 * W, xs))
    jag = fbm1d(W, 60, 5, 8)
    bt = hz + 0.06 * H - (hz + 0.06 * H - top_y) * np.clip(prof * (0.85 + 0.35 * jag), 0, 1) + 8 * (fbm1d(W, 14, 3, 9) - 0.5)
    city = c.below_curve(bt, 1.2)
    rng = np.random.default_rng(31)
    spires = [(0.591, 0.180, 8), (0.611, 0.180, 8), (0.578, 0.265, 8), (0.622, 0.265, 8), (0.560, 0.330, 6), (0.640, 0.330, 6), (0.545, 0.365, 7),
              (0.665, 0.350, 7), (0.700, 0.385, 6), (0.520, 0.395, 6), (0.735, 0.410, 5), (0.585, 0.370, 4), (0.615, 0.370, 4),
              (0.760, 0.430, 5), (0.490, 0.425, 5), (0.495, 0.450, 4), (0.775, 0.450, 4)]
    for sx, sy, sw in spires:
        x = sx * W
        y = sy * H
        body_top = y + (top_y - y) * 0.35
        city = np.maximum(city, c.poly([(x - sw * 1.5, top_y + 6), (x + sw * 1.5, top_y + 6), (x + sw * 1.0, body_top), (x + sw * 0.55, body_top - (body_top - y) * 0.15), (x, y), (x - sw * 0.55, body_top - (body_top - y) * 0.15), (x - sw * 1.0, body_top)], 0.8))
        for side in (-1, 1):  # pinnacles
            px_ = x + side * sw * 1.4
            city = np.maximum(city, c.poly([(px_ - 2.5, body_top + 20), (px_ + 2.5, body_top + 20), (px_, body_top - 18)], 0.6))
    # the Vault's nave between its paired spires: a steep gable under them
    city = np.maximum(city, c.poly([(0.582 * W, top_y + 4), (0.620 * W, top_y + 4), (0.620 * W, 0.300 * H), (0.601 * W, 0.255 * H), (0.582 * W, 0.300 * H)], 0.8))
    # two of the Pillars, flat-topped towers, joined high up by an arched bridge
    for px0 in (0.712, 0.748):
        city = np.maximum(city, c.poly([(px0 * W - 9, top_y + 6), (px0 * W + 9, top_y + 6), (px0 * W + 8, 0.335 * H), (px0 * W - 8, 0.335 * H)], 0.8))
        city = np.maximum(city, c.poly([(px0 * W - 11, 0.335 * H), (px0 * W + 11, 0.335 * H), (px0 * W + 11, 0.325 * H), (px0 * W - 11, 0.325 * H)], 0.6))
    deck_top, deck_bot = 0.360 * H, 0.372 * H
    bx_a, bx_b = 0.712 * W + 8, 0.748 * W - 8
    bridge = c.poly([(bx_a, deck_top), (bx_b, deck_top), (bx_b, deck_bot + 14), (bx_a, deck_bot + 14)], 0.6)
    arch = c.ellipse((bx_a + bx_b) / 2, deck_bot + 26, (bx_b - bx_a) / 2 - 2, 22, 0.6)
    city = np.maximum(city, np.clip(bridge - arch, 0, 1))
    # walls, roofs and the Pillars' terraces along the top of the bluff
    for i in range(90):
        x = (0.48 + 0.31 * rng.random()) * W
        wdt = 9 + rng.random() * 20
        hgt = 8 + rng.random() * 30
        roof = 12 + rng.random() * 18
        city = np.maximum(city, c.poly([(x - wdt / 2, top_y + 10), (x + wdt / 2, top_y + 10), (x + wdt / 2, top_y - hgt), (x, top_y - hgt - roof), (x - wdt / 2, top_y - hgt)], 0.7))
    sky_l = blur(np.repeat((c.px[int(top_y) - 60, :] @ LUM)[None, :], 3, 0), 30)[1]
    citycol = c.vgrad([(0, "#3E4074"), (1, "#353865")], 0.2 * H, hz + 0.05 * H)
    citycol = citycol * np.minimum(1.0, 0.82 * sky_l / np.maximum(citycol @ LUM, 1e-4))[..., None]
    c.over(citycol, city)
    M["city"] = city.copy()
    crim = np.clip(city - np.roll(city, -2, axis=1), 0, 1) * (c.yy < top_y + 6) * (0.35 + 0.65 * np.exp(-((c.xx - gx) / (0.20 * W)) ** 2))
    c.add(hexc("#FFD0A0"), crim * 0.9)
    win = np.zeros((H, W), np.float32)
    for i in range(34):
        wx_ = (0.50 + 0.28 * rng.random()) * W
        wy_ = top_y - rng.random() * 0.06 * H
        if city[int(wy_), int(wx_)] > 0.99 and city[int(wy_) - 3, int(wx_)] > 0.99 and city[int(wy_) + 3, int(wx_)] > 0.99:
            win = np.maximum(win, c.ellipse(wx_, wy_, 1.3, 1.8, 0.4))
    c.add(hexc("#FFC37A"), win * 0.9)
    c.add(hexc("#FFB060"), blur(win, 5) * 1.1)
    # the bluff's cliff below the city: rock face in shadow, snow on its ledges
    cliff = c.below_curve(bt, 1.2) * (c.yy > top_y + 4)
    M["cliff"] = cliff
    rock = tex_sample(fbm(256, 256, 20, 4, 61), c.xx / 1.0, c.yy / 6.0)
    ccol2 = c.vgrad([(0, "#2E3160"), (1, "#3C3F70")], top_y, hz + 0.06 * H) * (0.88 + 0.22 * rock)[..., None]
    snowp = np.clip((tex_sample(fbm(256, 256, 10, 3, 62), c.xx / 1.4, c.yy / 0.7) - 0.6) * 5, 0, 1)
    ccol2 = ccol2 + (hexc("#8A86B6") - ccol2) * (blur(snowp, 0.8) * 0.45)[..., None]
    spur = np.clip((tex_sample(fbm(256, 256, 8, 2, 63), c.xx / 1.0, c.yy / 8.0) - 0.5) * 3, -1, 1)
    ccol2 = ccol2 * (1 - 0.12 * np.clip(spur, 0, 1))[..., None]
    c.over(ccol2, cliff)
    # valley mist, lit warm toward the sun
    mist = np.exp(-((c.yy - (hz + 0.05 * H)) / (0.035 * H)) ** 2) * (0.6 + 0.6 * fbm(H, W, 140, 3, 63))
    c.add(hexc("#E3B0B8") * 0 + hexc("#D7AEC4"), mist * (0.25 + 0.30 * np.exp(-((c.xx - gx) / (0.3 * W)) ** 2)))

    # the valley floor: snow under the dawn sky, a forward-scatter sheen toward the sun
    base = hz + 0.07 * H
    field = c.below_curve(np.full(W, base, np.float32) - 5 * fbm1d(W, 200, 3, 64), 2)
    dy = np.clip(c.yy - base + 18, 1, None)
    T = fbm(512, 512, 64, 5, 65)
    hgt = blur(tex_sample(T, (c.xx - W / 2) / dy * 9 + 300, 5200 / dy), 1.5)
    sh = np.clip(-np.gradient(hgt, axis=0) * np.clip(dy / 120, 0.3, 3) * 30, -1, 1)
    fcol = c.vgrad([(0, "#B59DBA"), (0.15, "#8E89B6"), (0.55, "#5D679B"), (1, "#3B467E")], base, H)
    fcol = fcol + (hexc("#36407A") - fcol) * (np.clip(sh, 0, 1) * 0.4)[..., None]
    fcol = fcol + (hexc("#C9B2C9") - fcol) * (np.clip(-sh, 0, 1) * 0.2)[..., None]
    wpath = 0.05 * W + (c.yy - base) * 0.7
    sheen = np.exp(-((c.xx - gx) / wpath) ** 2) * np.clip(1 - (c.yy - base) / (H - base), 0, 1) ** 1.6
    fcol = 1 - (1 - fcol) * (1 - hexc("#F6C9A4") * (sheen * 0.45)[..., None])
    c.over(fcol, field)
    M["field"] = field

    # the near ridge, from the left: its crest takes the dawn, its face toward us is in blue shade
    crest = 0.665 * H + 0.16 * H * smooth(0.0, 0.62 * W, xs) ** 1.3 + 0.05 * H * smooth(0.62 * W, W, xs) + 10 * (fbm1d(W, 140, 4, 66) - 0.5)
    rm = c.below_curve(crest, 1.5)
    M["ridge"] = rm
    rcol = c.vgrad([(0, "#6E73A8"), (0.35, "#4A5390"), (1, "#2C3468")], crest.min(), H)
    rip = tex_sample(fbm(256, 256, 7, 2, 67), c.xx / 3.0, (c.yy - crest[None, :]) * 2.4 + 400)
    rcol = rcol * (1 + (rip - 0.5)[..., None] * 0.12)
    c.over(rcol, rm)
    lip = np.clip(c.below_curve(crest, 1) - c.below_curve(crest + 5, 2.5), 0, 1) * (0.35 + 0.65 * np.exp(-((xs - gx) / (0.35 * W)) ** 2))[None, :]
    c.add(hexc("#F7CFB6"), lip * 0.6)
    c.add(hexc("#E9BFB2"), blur(lip, 4) * 0.25)
    # sparkle: a few ice glints on the crest, facing the light
    rng2 = np.random.default_rng(68)
    glint = np.zeros((H, W), np.float32)
    for _ in range(120):
        x = rng2.random() * W
        y = crest[int(x)] + 2 + rng2.random() ** 2 * 60
        if y < H - 2:
            glint[int(y), int(x)] = 0.5 + 0.5 * rng2.random()
    c.add(hexc("#FFF2E2"), blur(glint, 0.7) * 1.4)

    # footprints from the lower left to the figures
    fx, fy = 0.33 * W, None
    foot = np.zeros((H, W), np.float32)
    footlit = np.zeros((H, W), np.float32)
    for k in range(16):
        t = k / 15
        x = 0.06 * W + (fx - 0.05 * W - 0.06 * W) * t + (8 if k % 2 else -8)
        yb = H - 20 - (H - 20 - (crest[int(fx)] + 8)) * t ** 0.9
        s = 1.0 - 0.6 * t
        foot = np.maximum(foot, c.ellipse(x, yb, 9 * s, 4 * s, 1.0))
        footlit = np.maximum(footlit, c.ellipse(x + 1.5 * s, yb - 2.5 * s, 8 * s, 2.0 * s, 1.0))
    c.mul(hexc("#3A4482"), np.clip(foot - footlit, 0, 1) * 0.6)

    # the figures: an adventurer with a lantern, and a chocobo, looking toward the city
    ax, ay = 0.315 * W, crest[int(0.315 * W)] + 4
    cx2, cy2 = 0.378 * W, crest[int(0.378 * W)] + 4
    # short, soft dawn shadows (the sun is still below the horizon): contact-dark at the feet, then fading toward the
    # viewer and left, away from the glow behind the city
    for (x0, y0, wdt, ln) in ((ax, ay, 40, 120), (cx2, cy2, 56, 130)):
        shp = c.poly([(x0 - wdt * 0.5, y0 - 2), (x0 + wdt * 0.5, y0 - 2), (x0 - ln, y0 + 30), (x0 - ln - 22, y0 + 20)], 3)
        away = np.clip(((x0 - c.xx) + (c.yy - y0)) / (ln + 30), 0, 1)
        c.mul(hexc("#2A3266"), shp * (0.62 - 0.52 * away))
        c.mul(hexc("#1A2050"), c.ellipse(x0, y0 + 1, wdt * 0.55, 4, 1.5) * 0.55)
    person = np.zeros((H, W), np.float32)
    # cloak: a long A-line, hood, the far arm holding the lantern out to the right
    person = np.maximum(person, c.poly([(ax - 26, ay), (ax + 24, ay), (ax + 13, ay - 92), (ax + 10, ay - 118), (ax - 9, ay - 118), (ax - 14, ay - 92)], 0.8))
    person = np.maximum(person, c.ellipse(ax, ay - 128, 13, 16, 0.8))   # hood
    person = np.maximum(person, c.poly([(ax - 13, ay - 132), (ax - 22, ay - 122), (ax - 10, ay - 120)], 0.6))  # hood's peak, back
    person = np.maximum(person, c.poly([(ax + 8, ay - 108), (ax + 40, ay - 86), (ax + 37, ay - 80), (ax + 6, ay - 98)], 0.6))  # arm
    person = np.maximum(person, c.poly([(ax - 24, ay - 10), (ax - 40, ay - 4), (ax - 22, ay - 30)], 0.8))  # cloak hem in the wind
    person = np.maximum(person, c.ellipse(ax, ay - 104, 17, 9, 0.8))  # shoulders
    person = np.maximum(person, c.ellipse(ax - 14, ay - 84, 9, 18, 0.8))  # the pack on the back
    # the chocobo, facing left toward the adventurer and her lantern; f mirrors every x offset
    f = -1
    choco = np.zeros((H, W), np.float32)
    choco = np.maximum(choco, c.ellipse(cx2 + f * -2, cy2 - 66, 44, 33, 0.8))                       # body
    choco = np.maximum(choco, c.ellipse(cx2 + f * 14, cy2 - 82, 26, 24, 0.8))                       # breast
    choco = np.maximum(choco, c.poly([(cx2 + f * 12, cy2 - 92), (cx2 + f * 36, cy2 - 96), (cx2 + f * 40, cy2 - 132), (cx2 + f * 22, cy2 - 136)], 0.8))  # neck
    choco = np.maximum(choco, c.ellipse(cx2 + f * 34, cy2 - 140, 17, 15, 0.8))                      # head
    choco = np.maximum(choco, c.poly([(cx2 + f * 46, cy2 - 146), (cx2 + f * 66, cy2 - 138), (cx2 + f * 46, cy2 - 132)], 0.6))  # beak
    for (dx_, dy_, ln) in ((-4, -150, 22), (2, -154, 20), (8, -155, 16)):                           # crest feathers, swept back
        choco = np.maximum(choco, c.poly([(cx2 + f * (24 + dx_), cy2 + dy_ + 6), (cx2 + f * (24 + dx_ - ln), cy2 + dy_ - 4), (cx2 + f * (28 + dx_), cy2 + dy_)], 0.6))
    for (dy_, ln) in ((-80, 46), (-70, 52), (-60, 42)):                                               # tail plume
        choco = np.maximum(choco, c.poly([(cx2 + f * -38, cy2 + dy_ - 6), (cx2 + f * (-38 - ln), cy2 + dy_ - 22), (cx2 + f * (-38 - ln * 0.8), cy2 + dy_ + 4), (cx2 + f * -38, cy2 + dy_ + 6)], 0.8))
    choco = np.maximum(choco, c.poly([(cx2 + f * -30, cy2 - 64), (cx2 + f * 6, cy2 - 76), (cx2 + f * 2, cy2 - 52)], 0.6))  # folded wing
    for lx_ in (cx2 + f * -12, cx2 + f * 10):                                                         # legs, knee forward
        choco = np.maximum(choco, c.poly([(lx_ - 5, cy2 - 42), (lx_ + 5, cy2 - 42), (lx_ + f * 6, cy2 - 22), (lx_ + 3, cy2), (lx_ - 3, cy2), (lx_ + f * -1, cy2 - 22)], 0.6))
        choco = np.maximum(choco, c.poly([(lx_ - 3, cy2 - 2), (lx_ + f * 12, cy2 - 1), (lx_ + 3, cy2 + 2)], 0.5))
    figs = np.maximum(person, choco)
    M["figs"] = figs
    c.over(hexc("#141A33"), figs)
    # the lantern: a warm practical light, its small pool on the snow at their feet
    lpx, lpy = ax + 40, ay - 76
    lan = c.ellipse(lpx, lpy + 8, 6, 8, 0.6)
    M["lantern"] = lan

    # a few falling flakes, never over the moon or star-like in the sky
    c.over(hexc("#E6EAF8"), flakes(c, 70, 69, (mx, my, mr), crest.min() + 20))
    v = c.radial(W * 0.55, H * 0.45, W * 0.78, H * 0.85)
    c.mul(hexc("#04060E"), np.clip(v - 0.55, 0, 1) * 0.5)
    # the painterly pass: a Kuwahara flattening of everything but the silhouettes, which keep their anti-aliased edges
    crisp = c.px.copy()
    keep = np.clip(blur(np.maximum(np.maximum(city * (c.yy < top_y + 8), figs), lan), 5) * 3.0, 0, 1)
    c.px = kuwahara(np.clip(c.px, 0, 1), 5) * (1 - keep[..., None]) + crisp * keep[..., None]
    # rims, drawn crisp: the sun's 1 px warm rim on the figures' right-hand edges; the cool sky on their tops
    rimf = np.clip(figs - np.roll(figs, -2, axis=1), 0, 1)
    c.add(hexc("#FFC9A0"), rimf * 0.85)
    c.add(hexc("#9AA6D8"), top_edge(figs, 2) * 0.22)
    # the lantern: its glow, a warm bounce on the chocobo's chest and on the adventurer's arm, a foreshortened pool
    c.over(hexc("#FFD48E"), lan)
    c.add(hexc("#FFB466"), blur(lan, 10) * 1.6)
    c.add(hexc("#FFB466"), np.exp(-((c.xx - lpx) ** 2 + (c.yy - lpy) ** 2) / (2 * 70 ** 2)) * 0.14)
    near_l = np.exp(-((c.xx - lpx) ** 2 + (c.yy - lpy) ** 2) / (2 * 60 ** 2))
    chest_zone = np.exp(-((c.xx - (cx2 - 22)) ** 2 + (c.yy - (cy2 - 84)) ** 2) / (2 * 22 ** 2))
    chest = np.clip(choco - np.roll(choco, 3, axis=1), 0, 1) * near_l + choco * chest_zone * 0.22
    c.add(hexc("#FFB062"), np.clip(chest, 0, 1) * 0.9)
    c.add(hexc("#FFB062"), np.clip(person - np.roll(person, -3, axis=1), 0, 1) * near_l * 0.8)
    dyy = c.yy - ay
    dyy = np.where(dyy < 0, dyy * 2.5, dyy)
    pool = np.exp(-(np.sqrt((c.xx - lpx) ** 2 + (dyy * 3.5) ** 2) / 95) ** 2) * rm * (1 - figs)
    c.add(hexc("#FFB062"), pool * 0.6)
    strokes = tex_sample(fbm(512, 512, 5, 3, 71), c.xx / 4.0, c.yy / 1.0)
    tooth = fbm(H, W, 1.6, 2, 70)
    c.px = np.clip(c.px * (1 + (strokes - 0.5)[..., None] * 0.07 + (tooth - 0.5)[..., None] * 0.03), 0, 1)
    return c, M


if __name__ == "__main__":
    OUT.mkdir(exist_ok=True)
    c, M = paint()
    c.save(OUT / "evercold-b-base.png", (1120, 440), grain=0.008, seed=3)
    from PIL import Image
    small = {k: np.asarray(Image.fromarray((np.clip(v, 0, 1) * 255).astype(np.uint8)).resize((1120, 440), Image.LANCZOS), np.float32) / 255 for k, v in M.items()}
    np.savez_compressed(OUT / "src-masks.npz", **small)
    print("evercold-b-base.png")
