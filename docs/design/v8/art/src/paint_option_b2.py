"""Option B painters for 1.22.0 Welcome home, 1.21.0 What next and 1.19.0 Right answers (registered in painters_b.py
as welcome-b, whatnext-b and answers-b). Each keeps the supervised scene brief of its Option A painting
(paint_release.py) and adds depth, atmosphere, a figure and the painterly finish of 1.20.0's Option B, and returns
(canvas, masks) with the standard mask keys.

Light, in every scene: one natural light, plus at most one warm practical light; a few dim distant windows (at most
five) may sit on the horizon as part of the scene. Shadows follow the natural light; the practical light adds only
its own small pool and bounce.

Original work, painted in code; nothing is traced and no official art is used.
"""
import math

import numpy as np

from artlib import Canvas, blur, fbm, fbm1d, hexc, smooth
from paint_option_b import kuwahara
from paint_release import bezier, crescent, moon_disc, stars, tex_sample, top_edge

W, H = 2240, 880
LUM = np.array([0.2126, 0.7152, 0.0722], np.float32)
KEYS = ("clouds", "under", "moon", "moonlit", "far", "city", "cliff", "field", "ridge", "figs", "lantern")


def zeros():
    return {k: np.zeros((H, W), np.float32) for k in KEYS}


def ridge_layer(c, M, xs, base, amp, cell, seed, top, bot, light_x, lit_col, cap=0.92, haze=0.0, rim=None):
    """A far range: a silhouette darker than the sky behind it, its light-facing slopes lifted, haze at its foot."""
    n = fbm1d(W, cell, 7, seed)
    r = base - H * amp * (0.30 + 2.6 * np.clip(n - 0.28, 0, None) ** 1.2)
    r = r - 5 * (fbm1d(W, 18, 3, seed + 1) - 0.5)
    m = c.below_curve(r, 1.2)
    col = c.vgrad([(0, top), (1, bot)], r.min(), base + 0.08 * H)
    sl = np.gradient(blur(np.repeat(r[None, :], 3, 0), 16)[1])
    facing = np.clip(np.where(xs < light_x, -1, 1) * sl * 2.0 + 0.2, 0, 1)
    lit = facing[None, :] * np.exp(-np.clip(c.yy - r[None, :], 0, None) / 40) * (0.55 + 0.8 * fbm(H, W, 30, 3, seed + 2))
    col = col + (hexc(lit_col) - col) * (lit * 0.40)[..., None]
    sky_at = c.px[np.clip(r.astype(int) - 3, 0, H - 1), np.arange(W)]
    sky_l = blur(np.repeat((sky_at @ LUM)[None, :], 3, 0), 20)[1]
    col = col * np.minimum(1.0, cap * sky_l[None, :] / np.maximum(col @ LUM, 1e-4))[..., None]
    c.over(col, m)
    if rim is not None:
        rimm = np.clip(c.below_curve(r, 1) - c.below_curve(r + 2.5, 1.2), 0, 1) * rim[None, :]
        c.add(hexc("#AFC0EA"), rimm * 0.5)
    if haze:
        c.add(hexc("#6E86C0"), np.exp(-((c.yy - base) / 18) ** 2) * haze * m)
    M["far"] = np.maximum(M["far"], m)
    return r


def cloud_band(c, M, seed, y0, y1, light_xy, warm=False, dens_k=0.5):
    """Thin moonlit (or twilit) cloud: soft streaks, lit on the side that faces the light; returns nothing."""
    # long horizontal streaks (stratus and altostratus), not isotropic puffs
    cl = tex_sample(fbm(512, 512, 70, 6, seed), c.xx / 4.5, c.yy / 1.1)
    cl2 = tex_sample(fbm(512, 512, 20, 4, seed + 1), c.xx / 3.0, c.yy / 1.0)
    band = np.exp(-((c.yy - (y0 + y1) / 2) / ((y1 - y0) / 2)) ** 2)
    dens = blur(np.clip((cl * 0.75 + cl2 * 0.35 - dens_k) * 3.4 * band, 0, 1), 2.0)
    lx, ly = light_xy
    gy, gx = np.gradient(blur(dens, 5))
    vx, vy = lx - c.xx, ly - c.yy
    n = np.sqrt(vx ** 2 + vy ** 2) + 1
    lit = np.clip(-(gx * vx / n + gy * vy / n) * 30, 0, 1) * dens        # the edge that faces the light
    base = hexc("#2A3866") if not warm else hexc("#3A3A6A")
    litc = hexc("#B8C6EE") if not warm else hexc("#E6A88C")
    col = np.stack([np.full((H, W), v, np.float32) for v in base], -1)
    col = col + (litc - col) * lit[..., None]
    c.over(col, dens * 0.85)
    M["clouds"] = np.maximum(M["clouds"], dens)
    M["under"] = np.maximum(M["under"], lit)


def moon_masks(c, mx, my, mr):
    """The disc and its lit part for moon_disc's near-full moon: the thin unlit sliver on the upper left is excluded
    (the same terminator moon_disc paints: 0.09 r at its widest, horn to horn)."""
    disc = c.ellipse(mx, my, mr, mr, 0.7)
    ux, uy = 0.919, 0.395
    dx, dy = (c.xx - mx) / mr, (c.yy - my) / mr
    a = dx * ux + dy * uy
    b = -dx * uy + dy * ux
    term = np.clip((-0.91 * np.sqrt(np.clip(1 - b * b, 0, 1)) - a) * mr + 0.5, 0, 1) * disc
    return disc, np.clip(disc - term, 0, 1)


def finish(c, keep, grain_seed):
    """The painterly pass of 1.20.0's Option B: Kuwahara flattening everywhere but the kept silhouettes, then a light
    brush and canvas texture."""
    from paint_option_b import kuwahara as kw
    crisp = c.px.copy()
    k = np.clip(blur(keep, 5) * 3.0, 0, 1)
    c.px = kw(np.clip(c.px, 0, 1), 5) * (1 - k[..., None]) + crisp * k[..., None]
    strokes = tex_sample(fbm(512, 512, 5, 3, grain_seed), c.xx / 4.0, c.yy / 1.0)
    tooth = fbm(H, W, 1.6, 2, grain_seed + 1)
    c.px = np.clip(c.px * (1 + (strokes - 0.5)[..., None] * 0.07 + (tooth - 0.5)[..., None] * 0.03), 0, 1)


def figure(c, x, y, s=1.0, facing=1):
    """A cloaked adventurer, about 120 px tall at s 1: hood, shoulders, a pack, the cloak's hem moving."""
    f = facing
    m = c.poly([(x - 22 * s, y), (x + 20 * s, y), (x + 11 * s, y - 80 * s), (x + 8 * s, y - 102 * s), (x - 8 * s, y - 102 * s), (x - 12 * s, y - 80 * s)], 0.8)
    m = np.maximum(m, c.ellipse(x, y - 111 * s, 11 * s, 14 * s, 0.8))
    m = np.maximum(m, c.ellipse(x, y - 90 * s, 15 * s, 8 * s, 0.8))
    m = np.maximum(m, c.ellipse(x - f * 12 * s, y - 72 * s, 8 * s, 16 * s, 0.8))
    m = np.maximum(m, c.poly([(x - f * 20 * s, y - 8 * s), (x - f * 34 * s, y - 2 * s), (x - f * 18 * s, y - 26 * s)], 0.8))
    for dx in (-6, 6):   # a stride: the legs show under the hem
        m = np.maximum(m, c.poly([(x + dx * s - 3, y), (x + dx * s + 3, y), (x + dx * s * 1.6 + 2, y + 8 * s), (x + dx * s * 1.6 - 2, y + 8 * s)], 0.6))
    return m


def contact_shadow(c, x0, y0, wdt, dx, dy, strength=0.6):
    """A short soft shadow, contact-dark at the feet, fading along (dx, dy)."""
    ln = math.hypot(dx, dy)
    shp = c.poly([(x0 - wdt / 2, y0 - 1), (x0 + wdt / 2, y0 - 1), (x0 + dx + wdt * 0.3, y0 + dy), (x0 + dx - wdt * 0.3, y0 + dy)], 3)
    away = np.clip(((c.xx - x0) * dx + (c.yy - y0) * dy) / (ln * ln), 0, 1)
    c.mul(hexc("#0A1222"), shp * (strength - (strength - 0.08) * away))
    c.mul(hexc("#060A14"), c.ellipse(x0, y0 + 1, wdt * 0.55, 3.5, 1.5) * 0.5)


# ======================================================================================================
# 1.22.0 Welcome home: a door left open on a lit hall
# ======================================================================================================
def welcome():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.64 * H
    c.px = c.vgrad([(0, "#050A1E"), (0.40, "#0D193A"), (0.80, "#1C2C58"), (1.0, "#2B3F74")], 0, hz)
    c.px[int(hz):] = hexc("#2B3F74")
    mx, my, mr = 0.20 * W, 0.21 * H, 40.0
    c.add(hexc("#5D78B8"), np.exp(-(c.radial(mx, my, 0.45 * W, 0.6 * H)) ** 2 * 2.0) * 0.28)

    def sky_lum(y):
        return 0.03 + 0.22 * min(1.0, max(0.0, y / hz)) ** 2

    stars(c, 200, 131, hz * 0.95, sky_lum, near_moon=(mx, my, mr), warm=0.08)
    cloud_band(c, M, 501, 0.10 * H, 0.42 * H, (mx, my), dens_k=0.58)
    moon_disc(c, mx, my, mr, seed=5, centre=True)
    M["moon"], M["moonlit"] = moon_masks(c, mx, my, mr)
    xs = np.arange(W, dtype=np.float32)
    ridge_layer(c, M, xs, hz + 4, 0.065, W / 2.0, 61, "#25355E", "#2C3F6C", mx, "#5F78AE", haze=0.45)
    ridge_layer(c, M, xs, hz + 0.045 * H, 0.05, W / 3.0, 62, "#1A2844", "#223451", mx, "#4E6898", haze=0.35)
    # the meadow plain under the moon
    gtop = hz + 0.085 * H
    field = c.below_curve(np.full(W, gtop, np.float32) - 5 * fbm1d(W, 240, 4, 19), 1.5)
    gcol = c.vgrad([(0, "#24364F"), (0.35, "#192839"), (1, "#0B1420")], gtop, H)
    T = fbm(512, 512, 90, 4, 55)
    dyy = np.clip(c.yy - gtop + 14, 1, None)
    roll = blur(tex_sample(T, (c.xx - W / 2) / dyy * 8 + 200, 4200 / dyy), 2)
    rsh = np.clip(-np.gradient(roll, axis=0) * np.clip(dyy / 90, 0.3, 3) * 26, -1, 1)
    gcol = gcol + (hexc("#4B6188") - gcol) * (np.clip(rsh, 0, 1) * 0.35)[..., None]
    c.over(gcol, field)
    M["field"] = field
    # the house, right of centre, backlit by the moon: facade in shadow, a cool rim on the roof's moon side
    x0, x1, yb = 0.565 * W, 0.795 * W, 0.80 * H
    wall_top = yb - 0.20 * H
    peak = (0.5 * (x0 + x1), wall_top - 0.15 * H)
    c.mul(hexc("#0A1220"), c.poly([(x0, yb), (x1, yb), (x1 + 0.17 * W, H + 20), (x0 + 0.08 * W, H + 20)], 14) * 0.45)
    facade = c.poly([(x0, yb), (x1, yb), (x1, wall_top), (x0, wall_top)], 0.6)
    roof = c.poly([(x0 - 18, wall_top + 4), peak, (x1 + 18, wall_top + 4)], 0.6)
    chim = c.poly([(x1 - 92, wall_top - 0.10 * H), (x1 - 66, wall_top - 0.10 * H), (x1 - 66, wall_top - 0.02 * H), (x1 - 92, wall_top - 0.02 * H)], 0.6)
    house = np.maximum(np.maximum(facade, roof), chim)
    stonew = fbm(H, W, 5, 2, 37)
    fcol = c.vgrad([(0, "#1B2234"), (1, "#141A29")], wall_top, yb) * (0.92 + 0.14 * stonew)[..., None]
    c.over(fcol, facade)
    c.over(hexc("#121826"), np.maximum(roof, chim))
    rim = np.clip(house - np.roll(np.roll(house, 2, axis=0), 2, axis=1), 0, 1) * (c.xx < peak[0] + 30)
    c.add(hexc("#9FB2E0"), rim * 0.55)
    top_c = wall_top - 0.10 * H
    up = np.clip(top_c - c.yy, 0, None)
    drift = up * 0.9 + 0.004 * up ** 2 + 10 * (fbm(H, W, 40, 3, 72) - 0.5) * np.clip(up / 60, 0, 1)
    width = 6 + up * 0.35
    smoke = np.exp(-((c.xx - (x1 - 79 + drift)) / width) ** 2) * (c.yy < top_c + 2) * np.exp(-up / 160) * (0.6 + 0.6 * fbm(H, W, 18, 3, 71))
    c.add(hexc("#8A96C0"), blur(smoke, 5) * 0.22)
    M["city"] = house
    # the open door and two windows: one practical light, the lit hall
    dx0, dx1, dy0 = 0.655 * W, 0.700 * W, yb - 0.125 * H
    door = c.poly([(dx0, yb), (dx1, yb), (dx1, dy0), (dx0, dy0)], 0.5)
    hall = c.vgrad([(0, "#E59A55"), (0.55, "#FFC27E"), (1, "#FFD9A0")], dy0, yb)
    lamp = np.exp(-((c.xx - (dx0 + dx1) / 2 - 6) ** 2 + (c.yy - (dy0 + 26)) ** 2) / (2 * 14 ** 2))
    hall = 1 - (1 - hall) * (1 - hexc("#FFF0CC") * (lamp * 0.8)[..., None])
    c.over(hall, door)
    c.over(hexc("#7A4A26"), c.poly([(dx0, yb), (dx0 + 9, yb - 4), (dx0 + 9, dy0 + 6), (dx0, dy0)], 0.5) * 0.85)
    c.over(hexc("#3A2416"), c.poly([(dx1, yb), (dx1 - 14, yb - 6), (dx1 - 14, dy0 + 8), (dx1, dy0)], 0.5))
    win = np.zeros((H, W), np.float32)
    for wx in (0.600 * W, 0.755 * W):
        win = np.maximum(win, c.poly([(wx - 22, yb - 0.115 * H), (wx + 22, yb - 0.115 * H), (wx + 22, yb - 0.065 * H), (wx - 22, yb - 0.065 * H)], 0.5))
    mull = ((np.abs(c.xx - 0.600 * W) < 1.6) | (np.abs(c.xx - 0.755 * W) < 1.6) | (np.abs(c.yy - (yb - 0.09 * H)) < 1.6)) * win
    c.over(hexc("#FFC57E"), win)
    c.over(hexc("#2A1B12"), mull)
    M["lantern"] = np.maximum(door, win)
    # the near ground and the stone path up to the door
    crest = 0.86 * H + 0.03 * H * np.sin(xs / W * 3.0) + 6 * (fbm1d(W, 160, 3, 81) - 0.5)
    rm = c.below_curve(crest, 1.5)
    c.over(c.vgrad([(0, "#1E2E44"), (1, "#0B1420")], crest.min(), H), rm)
    M["ridge"] = rm
    dcx = (dx0 + dx1) / 2
    path = bezier((0.47 * W, H + 30), (0.52 * W, 0.93 * H), (0.62 * W, 0.86 * H), (dcx, yb + 2))
    from paint_release import band
    pm = band(c, path, 220, 44, 1.2)
    stones = fbm(H, W, 5, 3, 91)
    pcol = c.vgrad([(0, "#3A4560"), (1, "#222A3C")], yb, H) * (0.85 + 0.25 * stones)[..., None]
    pcol = pcol + (hexc("#0C111C") - pcol) * (np.exp(-((stones - 0.5) / 0.05) ** 2) * 0.5)[..., None]
    c.over(pcol, pm)
    # the traveller, coming home up the path: moon shadow toward the viewer and right, the door warming her front
    fx, fy = 0.585 * W, 0.885 * H
    contact_shadow(c, fx, fy, 34, 120, 40)
    person = figure(c, fx, fy, 0.78, facing=1)
    M["figs"] = person
    c.over(hexc("#121828"), person)
    # the door's light: a pool widening down the path, the windows' faint pools, warm on her side facing the door
    dist = np.clip(c.yy - yb, 0, None)
    wid = (dx1 - dx0) / 2 + dist * 0.9
    spill = np.exp(-((c.xx - dcx) / np.maximum(wid, 1)) ** 4) * (c.yy > yb) / (1 + dist / 60) ** 1.4
    for wx in (0.600 * W, 0.755 * W):
        spill = spill + np.exp(-((c.xx - wx) / (30 + dist * 0.6)) ** 2) * (c.yy > yb) * np.exp(-dist / 40) * 0.25
    finish(c, np.maximum(np.maximum(np.maximum(house, person), M["lantern"]), M["moon"]), 701)
    c.add(hexc("#FFB062"), np.clip(spill, 0, 1) * 0.5 * (1 - person))
    c.add(hexc("#FFB466"), blur(M["lantern"], 10) * 0.45 * (1 - M["lantern"]))
    c.add(hexc("#9FB2E0"), np.clip(person - np.roll(np.roll(person, 2, 0), 2, 1), 0, 1) * 0.55)   # moon rim, upper left
    c.add(hexc("#FFB062"), np.clip(person - np.roll(person, -2, 1), 0, 1) * 0.6)                  # the door side
    v = c.radial(W * 0.55, H * 0.45, W * 0.78, H * 0.85)
    c.mul(hexc("#03050C"), np.clip(v - 0.55, 0, 1) * 0.5)
    return c, M


# ======================================================================================================
# 1.21.0 What next: a lantern at a crossroads, paths to a few distant lights
# ======================================================================================================
def whatnext():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.60 * H
    c.px = c.vgrad([(0, "#070D25"), (0.40, "#10204A"), (0.78, "#24406E"), (1.0, "#3C5A88")], 0, hz)
    c.px[int(hz):] = hexc("#3C5A88")
    c.add(hexc("#D9A07A"), np.exp(-(c.radial(1.05 * W, hz + 0.02 * H, 0.45 * W, 0.22 * H)) ** 2 * 1.6) * 0.30)
    c.add(hexc("#F2B08C"), np.exp(-((c.yy - (hz - 0.035 * hz)) / (0.045 * hz)) ** 2) * smooth(0.35 * W, 1.0 * W, c.xx) * 0.32)
    mx, my, mr = 0.80 * W, 0.22 * H, 24.0

    def sky_lum(y):
        return 0.04 + 0.32 * min(1.0, max(0.0, y / hz)) ** 2

    stars(c, 240, 41, hz * 0.95, sky_lum, near_moon=(mx, my, mr), warm=0.06)
    cloud_band(c, M, 511, 0.30 * H, 0.52 * H, (1.05 * W, hz), warm=True, dens_k=0.55)
    ux, uy = (1.02 * W - mx), (hz + 0.15 * H - my)
    n = math.hypot(ux, uy)
    disc, lit = crescent(c, mx, my, mr, ux / n, uy / n, 0.55)
    c.over(hexc("#1E3260"), disc * 0.35)
    c.over(hexc("#F6EDD8"), lit)
    M["moon"], M["moonlit"] = disc, lit
    xs = np.arange(W, dtype=np.float32)
    ridge_layer(c, M, xs, hz + 4, 0.065, W / 2.2, 111, "#263962", "#2C416E", 1.0 * W, "#6E78A8", haze=0.45)
    ridge_layer(c, M, xs, hz + 0.045 * H, 0.050, W / 3.2, 112, "#1B2A46", "#223451", 1.0 * W, "#5A6A98", haze=0.35)
    gtop = hz + 0.085 * H
    field = c.below_curve(np.full(W, gtop, np.float32) - 5 * fbm1d(W, 240, 4, 29), 1.5)
    gcol = c.vgrad([(0, "#26394F"), (0.35, "#1B2A3A"), (1, "#0C1520")], gtop, H)
    blades = fbm(H, W, 2.5, 2, 32)
    gcol = gcol + (hexc("#56708E") - gcol) * (np.clip((blades - 0.62) * 3, 0, 1) * 0.14 * smooth(gtop + 0.1 * H, H, c.yy))[..., None]
    c.over(gcol, field)
    M["field"] = field
    # a few dim distant windows where the paths lead (five in all), and the tower on the middle hill
    tx = 0.43 * W
    tower = c.poly([(tx - 9, hz + 0.01 * H), (tx + 9, hz + 0.01 * H), (tx + 6, hz - 0.03 * H), (tx, hz - 0.05 * H), (tx - 6, hz - 0.03 * H)], 0.6)
    c.over(hexc("#141E33"), tower)
    M["city"] = tower
    lm = np.zeros((H, W), np.float32)
    for (lx, ly) in ((0.17 * W - 5, hz + 0.030 * H), (0.17 * W + 6, hz + 0.032 * H), (tx, hz - 0.018 * H), (0.71 * W - 4, hz + 0.050 * H), (0.71 * W + 6, hz + 0.052 * H)):
        lm = np.maximum(lm, c.ellipse(lx, ly, 1.8, 2.2, 0.5))
    c.add(hexc("#FFC77A"), lm * 0.7)
    c.add(hexc("#FFB060"), blur(lm, 5) * 0.8)
    # the paths
    from paint_release import band
    X = (0.52 * W, 0.80 * H)
    paths = [bezier((0.44 * W, H + 30), (0.47 * W, 0.92 * H), (0.50 * W, 0.84 * H), X),
             bezier(X, (0.40 * W, 0.76 * H), (0.25 * W, 0.70 * H), (0.17 * W, gtop + 2)),
             bezier(X, (0.49 * W, 0.74 * H), (0.45 * W, 0.70 * H), (tx, gtop + 2)),
             bezier(X, (0.60 * W, 0.75 * H), (0.68 * W, 0.71 * H), (0.71 * W, gtop + 2))]
    pm = band(c, paths[0], 230, 40, 1.2)
    for p_ in paths[1:]:
        pm = np.maximum(pm, band(c, p_, 38, 6, 1.0))
    pm *= np.clip((c.yy - gtop) / 22, 0, 1) ** 0.8
    dust = fbm(H, W, 6, 3, 13)
    c.over(c.vgrad([(0, "#5F7098"), (0.4, "#45537A"), (1, "#28324C")], gtop, H) * (0.88 + 0.2 * dust)[..., None], pm)
    c.mul(hexc("#0B1220"), np.clip(blur(pm, 5) - pm, 0, 1) * 0.7)
    # the near ground: a gentle rise in the foreground
    crest = 0.90 * H + 0.02 * H * np.sin(xs / W * 4.0) + 5 * (fbm1d(W, 140, 3, 83) - 0.5)
    M["ridge"] = c.below_curve(crest, 1.5)
    c.mul(hexc("#0A1220"), M["ridge"] * 0.25)
    # the lantern post and the waystone at the crossroads, and a traveller reading the stone
    lx, ly = X[0] + 70, X[1] - 4
    lamp_y = ly - 0.13 * H
    wx, wy, wr = X[0] - 10, X[1] + 4, 20
    contact_shadow(c, wx, wy, wr * 2, -150, 24, 0.7)
    contact_shadow(c, lx, ly, 6, -96, 10, 0.55)
    tx2, ty2 = X[0] - 52, X[1] + 10
    contact_shadow(c, tx2, ty2, 30, -120, 26, 0.6)
    stone = c.poly([(wx - wr, wy), (wx + wr, wy), (wx + wr * 0.7, wy - wr * 2.3), (wx - wr * 0.2, wy - wr * 2.8), (wx - wr * 0.9, wy - wr * 2.0)], 0.6)
    post = np.maximum(c.poly([(lx - 3, ly), (lx + 3, ly), (lx + 2.5, lamp_y), (lx - 2.5, lamp_y)], 0.5),
                      c.poly([(lx - 2, lamp_y + 2), (lx + 22, lamp_y + 2), (lx + 22, lamp_y + 6), (lx - 2, lamp_y + 6)], 0.5))
    trav = figure(c, tx2, ty2, 0.62, facing=1)
    c.over(hexc("#2C3448"), stone)
    c.over(hexc("#181A24"), post)
    c.over(hexc("#121828"), trav)
    M["figs"] = np.maximum(np.maximum(stone, post), trav)
    lan = c.poly([(lx + 14, lamp_y + 8), (lx + 30, lamp_y + 8), (lx + 28, lamp_y + 32), (lx + 16, lamp_y + 32)], 0.5)
    M["lantern"] = lan
    finish(c, np.maximum(M["figs"], np.maximum(lan, tower)), 711)
    # the lantern after the pass: its glow, the pool on the ground, warm on the sides that face it
    c.over(hexc("#181A24"), c.poly([(lx + 21.4, lamp_y + 6), (lx + 22.6, lamp_y + 6), (lx + 22.6, lamp_y + 9), (lx + 21.4, lamp_y + 9)], 0.4))
    c.over(hexc("#FFD08A"), lan)
    c.add(hexc("#FFB060"), blur(lan, 8) * 1.4)
    pcx, pcy = lx + 16, ly
    dy_ = c.yy - pcy
    dy_ = np.where(dy_ < 0, dy_ * 1.8, dy_)
    pool = np.exp(-(np.sqrt((c.xx - pcx) ** 2 + (dy_ * 4.0) ** 2) / 170) ** 2) * (1 - M["figs"])
    c.add(hexc("#FFB466"), pool * 0.42)
    near = np.exp(-((c.xx - (lx + 22)) ** 2 + (c.yy - (lamp_y + 20)) ** 2) / (2 * 140 ** 2))
    c.add(hexc("#FFB466"), np.clip(M["figs"] - np.roll(M["figs"], -2, 1), 0, 1) * near * 0.9)
    c.add(hexc("#9AA6D8"), top_edge(M["figs"], 2) * 0.2)
    v = c.radial(W * 0.55, H * 0.45, W * 0.78, H * 0.85)
    c.mul(hexc("#03050C"), np.clip(v - 0.55, 0, 1) * 0.5)
    return c, M


# ======================================================================================================
# 1.19.0 Right answers: a clear moonlit road
# ======================================================================================================
def answers():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.585 * H
    c.px = c.vgrad([(0, "#060B20"), (0.35, "#0D1838"), (0.72, "#1D2D5B"), (1.0, "#33487E")], 0, hz)
    c.px[int(hz):] = hexc("#33487E")
    mx, my, mr = 0.585 * W, 0.235 * H, 50.0
    c.add(hexc("#5D78B8"), np.exp(-(c.radial(mx, my, 0.55 * W, 0.70 * H)) ** 2 * 2.0) * 0.30)

    def sky_lum(y):
        return 0.03 + 0.22 * min(1.0, max(0.0, y / hz)) ** 2

    stars(c, 220, 23, hz * 0.97, sky_lum, near_moon=(mx, my, mr), warm=0.08)
    moon_disc(c, mx, my, mr, centre=True)
    M["moon"], M["moonlit"] = moon_masks(c, mx, my, mr)
    xs = np.arange(W, dtype=np.float32)
    for i, (yb, amp, top, bot, seed, haze, f) in enumerate([(hz + 4, 0.075, "#2A3A66", "#304373", 4, 0.50, 1.8), (hz + 0.040 * H, 0.070, "#1F2D4C", "#27385A", 5, 0.40, 2.6),
                                                            (hz + 0.085 * H, 0.045, "#172438", "#1E2E46", 6, 0.30, 3.4)]):
        ridge_layer(c, M, xs, yb, amp, W / f, 40 + seed, top, bot, mx, "#5F78AE", haze=haze)
    gtop = hz + 0.105 * H
    field = c.below_curve(np.full(W, gtop, np.float32) - 6 * fbm1d(W, 240, 4, 9), 1.5)
    gcol = c.vgrad([(0, "#2A3D58"), (0.35, "#1C2C42"), (1, "#0D1724")], gtop, H)
    T = fbm(512, 512, 90, 4, 55)
    dyy = np.clip(c.yy - gtop + 14, 1, None)
    roll = blur(tex_sample(T, (c.xx - W / 2) / dyy * 8 + 200, 4200 / dyy), 2)
    rsh = np.clip(-np.gradient(roll, axis=0) * np.clip(dyy / 90, 0.3, 3) * 26, -1, 1)
    gcol = gcol + (hexc("#4B6188") - gcol) * (np.clip(rsh, 0, 1) * 0.40)[..., None]
    gcol = gcol + (hexc("#0B1420") - gcol) * (np.clip(-rsh, 0, 1) * 0.35)[..., None]
    c.over(gcol, field)
    M["field"] = field
    from paint_release import band
    fork = (0.505 * W, 0.745 * H)
    near = bezier((0.30 * W, H + 30), (0.36 * W, 0.93 * H), (0.47 * W, 0.80 * H), fork)
    main = bezier(fork, (0.535 * W, 0.71 * H), (0.575 * W, 0.68 * H), (mx + 4, gtop - 1))
    side = bezier(fork, (0.47 * W, 0.725 * H), (0.38 * W, 0.70 * H), (0.30 * W, gtop + 4))
    rm = np.maximum(band(c, near, 300, 46, 1.2), np.maximum(band(c, main, 46, 7, 1.0), band(c, side, 40, 10, 1.0))) * field
    rm *= np.clip((c.yy - gtop + 4) / 10, 0, 1) ** 0.8
    far_f = np.clip(1 - (c.yy - gtop) / (0.30 * H), 0, 1) ** 1.5
    toward = np.exp(-((c.xx - mx) / (0.06 * W + (c.yy - gtop) * 0.7)) ** 2) * far_f
    side_m = band(c, side, 40, 10, 1.0) * (1 - band(c, main, 46, 7, 1.0))
    rcol = c.vgrad([(0, "#7484AC"), (0.30, "#4E5B80"), (1, "#2A344F")], gtop, H)
    stone = fbm(H, W, 6, 3, 81)
    rcol = rcol * (0.84 + 0.26 * stone)[..., None]
    rcol = 1 - (1 - rcol) * (1 - hexc("#D6E0FF") * (toward * 0.55)[..., None])
    rcol = rcol + (hexc("#26324E") - rcol) * (side_m * 0.45 * smooth(gtop, 0.80 * H, c.yy) + side_m * 0.25)[..., None]
    c.over(rcol, rm)
    c.mul(hexc("#0B1220"), np.clip(blur(rm, 6) - rm, 0, 1) * 0.8)
    # the near ground: the grass foreground
    crest = 0.88 * H + 0.03 * H * np.cos(xs / W * 5.0) + 5 * (fbm1d(W, 120, 3, 85) - 0.5)
    M["ridge"] = c.below_curve(crest, 1.5)
    c.mul(hexc("#0A1220"), M["ridge"] * 0.2)
    # the lone tree, the signpost, two waystones, and a traveller on the lit road walking toward the moon
    tx, ty = 0.80 * W, gtop + 0.006 * H
    rng = np.random.default_rng(17)
    canopy = np.zeros((H, W), np.float32)
    for _ in range(70):
        ang = rng.random() * math.pi * 2
        rad = rng.random() ** 0.6
        canopy = np.maximum(canopy, c.ellipse(tx + math.cos(ang) * rad * 46, ty - 96 + math.sin(ang) * rad * 30, 9 + rng.random() * 9, 7 + rng.random() * 6, 0.8))
    canopy = blur(np.clip(canopy, 0, 1), 0.7)
    tree = np.maximum(canopy, c.poly([(tx - 4, ty), (tx + 4, ty), (tx + 2.5, ty - 70), (tx - 2.5, ty - 70)], 0.6))
    c.mul(hexc("#0A1222"), c.poly([(tx - 5, ty), (tx + 5, ty), (tx + 96, ty + 30), (tx + 40, ty + 34)], 4) * 0.45)
    px0, py0 = fork[0] + 64, fork[1] + 16
    contact_shadow(c, px0, py0, 8, -78, 64, 0.55)
    sign = np.maximum(c.poly([(px0 - 3.2, py0), (px0 + 3.2, py0), (px0 + 2.6, py0 - 104), (px0 - 2.6, py0 - 104)], 0.5),
                      np.maximum(c.poly([(px0 - 2, py0 - 94), (px0 + 52, py0 - 102), (px0 + 61, py0 - 96), (px0 + 52, py0 - 90), (px0 - 2, py0 - 84)], 0.5),
                                 c.poly([(px0 + 2, py0 - 74), (px0 - 44, py0 - 70), (px0 - 52, py0 - 65), (px0 - 44, py0 - 60), (px0 + 2, py0 - 64)], 0.5)))
    stones_m = np.zeros((H, W), np.float32)
    stone_tops = np.zeros((H, W), np.float32)
    for sx, sy, sr in [(0.555 * W, 0.705 * H, 7), (0.405 * W, 0.86 * H, 14)]:
        c.mul(hexc("#0A1222"), c.ellipse(sx - sr * 0.6, sy + 1, sr * 1.6, sr * 0.35, 1.5) * 0.6)    # contact shadow
        st = c.poly([(sx - sr * 1.2, sy + 1), (sx + sr * 1.1, sy + 1), (sx + sr * 0.9, sy - sr * 0.9), (sx + sr * 0.3, sy - sr * 1.25),
                     (sx - sr * 0.5, sy - sr * 1.2), (sx - sr * 1.05, sy - sr * 0.8)], 0.6)
        stones_m = np.maximum(stones_m, st)
        stone_tops = np.maximum(stone_tops, top_edge(st, 3))
    trx, try_ = 0.5705 * W, 0.693 * H
    contact_shadow(c, trx, try_, 14, -16, 26, 0.55)
    trav = figure(c, trx, try_, 0.36, facing=1)
    c.over(hexc("#0B1322"), tree)
    c.over(hexc("#1A1A24"), sign)
    c.over(hexc("#1E2538"), stones_m)                                         # the face toward the viewer, in shade
    c.over(hexc("#101624"), trav)
    figs = np.maximum(np.maximum(tree, sign), np.maximum(stones_m, trav))
    M["figs"] = figs
    # tall grass at the near corners
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
    finish(c, np.maximum(np.maximum(figs, tuft), M["moon"]), 721)
    # rims after the pass: every silhouette's edges facing the moon (above and toward x = mx)
    rimmed = np.clip(figs - stones_m, 0, 1)                                     # the stones take only a lit top
    rim_l = np.clip(rimmed - np.roll(np.roll(rimmed, 2, 0), -1, 1), 0, 1)
    rim_r = np.clip(rimmed - np.roll(np.roll(rimmed, 2, 0), 1, 1), 0, 1)
    c.add(hexc("#AFC0EA"), np.where(c.xx < mx, rim_l, rim_r) * 0.6)
    c.add(hexc("#8EA0CC"), stone_tops * 0.5)
    rim_l = np.clip(tuft - np.roll(np.roll(tuft, 2, 0), -1, 1), 0, 1)
    rim_r = np.clip(tuft - np.roll(np.roll(tuft, 2, 0), 1, 1), 0, 1)
    c.add(hexc("#7F96C8"), np.where(c.xx < mx, rim_l, rim_r) * smooth(H, H - 0.30 * H, c.yy) * 0.45)
    mist = np.exp(-((c.yy - (hz + 0.06 * H)) / 18) ** 2) * fbm(H, W, 120, 3, 5)
    c.add(hexc("#8197CF"), mist * 0.16)
    v = c.radial(W * 0.55, H * 0.42, W * 0.78, H * 0.85)
    c.mul(hexc("#03050C"), np.clip(v - 0.55, 0, 1) * 0.5)
    return c, M
