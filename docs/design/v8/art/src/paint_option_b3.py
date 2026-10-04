"""Option B painters for the backfill, 1.14.0 to 1.18.0 (registered in painters_b.py as polish-b, faces-b, themes-b,
mixmatch-b and runs-b). Each follows the production recipe in spec-1.22.md W2 and returns (canvas, masks) with the
standard mask keys, plus any extra masks its <key>.json names (far_layers, lines).

The five scenes vary the series: a harbour, a forest, a summit, a garden at dawn and a river, under five different
moons and palettes, so the nine releases read as one series and not one repeated picture.

Light, in every scene: one natural light, plus at most one warm practical light that lights something. Shadows follow
the natural light; the practical light adds only its own pool, bounce and reflection. Every moon is lit toward the
sun; a near-full moon carries the approved maria chains of paint_release.moon_disc (no hook-shaped sea, no holes)
and a thin unlit sliver.

Original work, painted in code; nothing is traced. Limsa Lominsa, Gridania, Coerthas, Mor Dhona and Kugane appear as
our own simplified silhouettes, as the plan's art rule allows.
"""
import math

import numpy as np

from artlib import Canvas, _box, blur, fbm, fbm1d, hexc, smooth
from paint_option_b2 import KEYS, contact_shadow, figure, finish, zeros
from paint_release import band, bezier, crescent, stars, tex_sample, top_edge

W, H = 2240, 880
LUM = np.array([0.2126, 0.7152, 0.0722], np.float32)


# ------------------------------------------------------------------------------------------------------ shared
def moon(c, M, mx, my, mr, ux, uy, k, tint="#F3EFE3", unlit="#1E2A55", unlit_a=0.45, seas=True, glow="#DCE6FF", glow_a=0.16):
    """A moon lit toward the sun (ux, uy). k > 0 is a crescent, k = 0 a half moon, k < 0 a gibbous (the unlit sliver
    is 1 + k radii wide at its middle). The seas are the approved chains of paint_release.moon_disc (Procellarum and
    Imbrium on the left, Serenitatis to Fecunditatis down the right, Nubium below), one union at one opacity: no
    hook-shaped sea, no holes. The unlit part is a faint earthshine barely above the sky."""
    disc, lit = crescent(c, mx, my, mr, ux, uy, k)
    # the unlit part first, then the glow over it: the earthshine sits at the local sky's value, never below it (no hole)
    c.over(hexc(unlit), disc * unlit_a)
    c.add(hexc(glow), np.exp(-(c.radial(mx, my, mr * 4.5)) ** 2 * 2.2) * glow_a)
    c.add(hexc(glow), np.exp(-(c.radial(mx, my, mr * 12)) ** 2 * 2.0) * glow_a * 0.6)
    c.add(hexc(glow), disc * (1 - lit) * 0.06)
    col = np.stack([np.full((c.h, c.w), v, np.float32) for v in hexc(tint)], -1)
    if seas:
        col = col + (hexc("#A3A39C") - col) * (sea_mask(c, mx, my, mr, 3) * 0.44)[..., None]
    limb = np.clip(c.radial(mx, my, mr), 0, 1) ** 3
    col = col + (hexc("#C9C3B4") - col) * (limb * 0.35)[..., None]
    c.over(col, lit)
    M["moon"], M["moonlit"] = disc, lit


def moon_full(c, M, mx, my, mr, ux=0.919, uy=0.395, tint=(1.0, 1.0, 1.0), seed=3):
    """A near-full moon after the approved recipe of paint_release.moon_disc (Option A's answers-base): the seas
    (sea_mask) at one opacity, limb darkening, a cool halo, and a thin unlit sliver (0.09 r) on the side away from the
    sun (ux, uy). The seas are laid out with wider gaps than moon_disc's chains, because at this size, after the
    paint pass, those chains joined into a hook. Returns the masks: moon is the disc, moonlit the disc without the
    sliver. tint warms a low moon. Every moon is kept out of the Kuwahara pass."""
    d = c.radial(mx, my, mr)
    disc = np.clip((1 - d) * mr / 1.2 + 0.5, 0, 1)
    col = np.stack([np.full((c.h, c.w), v, np.float32) for v in hexc("#F3EFE3")], -1)
    col = col + (hexc("#A3A39C") - col) * (sea_mask(c, mx, my, mr, seed) * 0.44)[..., None]
    limb = np.clip(d, 0, 1) ** 3
    col = col + (hexc("#C9C3B4") - col) * (limb * 0.35)[..., None]
    col = col * np.asarray(tint, np.float32)
    dx, dy = (c.xx - mx) / mr, (c.yy - my) / mr
    a = dx * ux + dy * uy
    b = -dx * uy + dy * ux
    term = np.clip((-0.91 * np.sqrt(np.clip(1 - b * b, 0, 1)) - a) * mr + 0.5, 0, 1) * disc
    c.add(hexc("#DCE6FF"), np.exp(-(c.radial(mx, my, mr * 4.5)) ** 2 * 2.2) * 0.16)
    c.add(hexc("#C8D6FF"), np.exp(-(c.radial(mx, my, mr * 12)) ** 2 * 2.0) * 0.10)
    c.over(col, disc)
    c.over(hexc("#2A3866"), term * 0.82)
    M["moon"], M["moonlit"] = disc, np.clip(disc - term, 0, 1)


def sea_mask(c, mx, my, mr, seed):
    seas = np.zeros((c.h, c.w), np.float32)
    # a broken chain with clear gaps, so no two seas join into a hook ("?" or "C"): Procellarum along the left limb,
    # Imbrium upper left, Serenitatis upper right of centre, Tranquillitatis right of centre, Fecunditatis and
    # Nectaris lower right, Crisium alone by the right limb, Nubium low on the left
    # Imbrium and Serenitatis are joined by a fainter Vaporum, so the two upper seas never read as a pair of eyes
    for (sx, sy, rx, ry, k) in (  # the left limb: Procellarum, broken from Imbrium; Humorum and Nubium below
                                (-0.60, 0.10, 0.16, 0.34, 0.8), (-0.32, -0.30, 0.20, 0.17, 1.0), (-0.46, 0.42, 0.08, 0.08, 0.6), (-0.20, 0.40, 0.14, 0.09, 0.65),
                                # Vaporum, then the right chain: Serenitatis, Tranquillitatis, Fecunditatis, Nectaris
                                (-0.09, -0.27, 0.13, 0.07, 0.55), (0.14, -0.34, 0.15, 0.14, 0.95), (0.26, -0.04, 0.18, 0.15, 1.0), (0.47, 0.24, 0.12, 0.15, 0.8), (0.24, 0.30, 0.08, 0.08, 0.6),
                                # Crisium, alone by the right limb
                                (0.70, -0.24, 0.08, 0.10, 0.85)):
        seas = np.maximum(seas, c.ellipse(mx + sx * mr, my + sy * mr, rx * mr, ry * mr, 0.6) * k)
    seas = blur(seas, mr * 0.07) * (0.80 + 0.20 * fbm(c.h, c.w, mr * 0.18, 3, seed))
    return np.clip(seas, 0, 1)


def streak_x(a, r):
    """A horizontal smear (rippled water stretches reflections sideways)."""
    for _ in range(2):
        a = _box(a, r, 1)
    return a


def reflect(c, wm, src, hz, objs, seed, amp=(1.5, 16.0), fres=(0.80, 0.38), deep="#081024", dark=0.82, smear=3, brk=0.0):
    """Water: the scene mirrored about the horizon (sky and far land) and each near object about its own waterline,
    broken by ripples that grow toward the viewer, mixed with the deep water colour by a Fresnel term that is strong
    at grazing angles and weaker close up. src is the canvas without moon and stars (they never mirror as dots)."""
    yy, xx = c.yy, c.xx
    t = np.clip((yy - hz) / (c.h - hz), 0, 1)
    rip = tex_sample(fbm(512, 512, 22, 3, seed), xx / 7.0, yy / 1.0) - 0.5
    rip2 = tex_sample(fbm(512, 512, 9, 2, seed + 1), xx / 5.0, yy / 1.0) - 0.5
    a = amp[0] + amp[1] * t
    sx = np.clip(xx + rip * a * 2.0, 0, c.w - 1).astype(int)
    out = src[np.clip((2 * hz - yy + rip2 * a * 0.5).astype(int), 0, c.h - 1), sx]
    # near objects' reflections break into horizontal dashes where wave faces tilt away (brk: how much)
    dash = 1 - brk * np.clip((tex_sample(fbm(512, 512, 6, 2, seed + 2), xx / 9.0, yy / 0.8) - 0.42) * 4, 0, 1)
    for m, axis in objs:
        sy = np.clip((2 * axis - yy + rip2 * a * 0.5).astype(int), 0, c.h - 1)
        om = m[sy, sx] * (yy > axis)
        out = out * (1 - om[..., None]) + (src[sy, sx] * dash[..., None] + out * (1 - dash[..., None])) * om[..., None]
    out = streak_x(blur(out, 1.0), smear) * dark
    R = fres[0] + (fres[1] - fres[0]) * t
    col = hexc(deep) * (1 - R[..., None]) + out * R[..., None]
    c.over(col, wm)


def glitter(c, gx, hz, wm, seed, col="#FFF1D2", strength=0.9, w0=10.0, spread=0.32, thr=0.62, near=1.0):
    """The moon's broken path on rippled water: a column of sparkles under it, wider and sparser toward the viewer,
    with a soft unbroken line where it meets the horizon."""
    yy, xx = c.yy, c.xx
    d = np.clip(yy - hz, 0, None)
    hw = w0 + d * spread
    path = np.exp(-((xx - gx) / hw) ** 2) * (yy > hz)
    n = tex_sample(fbm(512, 512, 5, 2, seed), xx / (2.0 + d * 0.012), yy / 0.9)
    sp = np.clip((n - thr) * 9, 0, 1) * (1 - 0.55 * np.clip(d / (c.h - hz), 0, 1)) * near
    c.add(hexc(col), (sp * path * strength + path * np.exp(-d / 14) * 0.45) * wm)


def rims(m, dx, dy, k=2):
    """The edge band of a mask that faces the direction (dx, dy): pixels in m whose neighbour that way is not."""
    sh = np.roll(np.roll(m, -int(round(dy * k)), 0), -int(round(dx * k)), 1)      # the neighbour toward (dx, dy)
    return np.clip(m - sh, 0, 1)


def seal_far(M, layers=None):
    """The treatments take everything outside far and city as sky, and the recipe's far masks run down to the
    frame's foot (as paint_option_b2.ridge_layer's do). So far is closed over the ground and the water below it; with
    far_layers, the nearest range takes the ground, and each layer keeps only what no nearer layer covers."""
    # the treatments take city minus field and ridge, so the ground never runs under a building
    M["field"] = M["field"] * (1 - M["city"])
    M["ridge"] = M["ridge"] * (1 - M["city"])
    ground = np.maximum(M["field"], M["ridge"])
    M["far"] = np.maximum(M["far"], ground)
    if layers:
        M[layers[-1]] = np.maximum(M[layers[-1]], ground)
        nearer = np.zeros_like(M["far"])
        for n in reversed(layers):
            M[n] = np.clip(M[n] - nearer, 0, 1)
            nearer = np.maximum(nearer, M[n])


def vignette(c, cx=0.55, cy=0.45, k=0.5):
    v = c.radial(W * cx, H * cy, W * 0.78, H * 0.85)
    c.mul(hexc("#03050C"), np.clip(v - 0.55, 0, 1) * k)


def cap_to_sky(c, col, m, sky_px, cap):
    """Holds a far silhouette's colour at cap x the luminance of the sky behind each of its pixels (the bare sky the
    silhouette covers), so a backlit form is always darker than the sky it stands against, and a tall tower does not
    darken the hill under it."""
    sl = blur(sky_px @ LUM, 6)
    return col * np.minimum(1.0, cap * sl / np.maximum(col @ LUM, 1e-4))[..., None]


# ======================================================================================================
# 1.14.0 The polish you asked for: a boatwright's lit window over Limsa's harbour, the moon rising over the sea
# ======================================================================================================
def polish():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.585 * H
    c.px = c.vgrad([(0, "#060A20"), (0.45, "#0F1940"), (0.80, "#212E5C"), (1.0, "#3A3F6E")], 0, hz)
    c.px[int(hz):] = hexc("#3A3F6E")
    mx, my, mr = 0.26 * W, 0.36 * H, 40.0
    # a low moon: its glow is warmer near the horizon, through more air
    c.add(hexc("#C9B7A6"), np.exp(-(c.radial(mx, my, 0.30 * W, 0.42 * H)) ** 2 * 2.0) * 0.22)
    c.add(hexc("#D9B79A"), np.exp(-((c.yy - hz) / (0.06 * H)) ** 2) * np.exp(-((c.xx - mx) / (0.25 * W)) ** 2) * 0.18 * (c.yy < hz))
    sky_only = c.px.copy()
    # the night sky of 1.14: stars at three depths, warm and cool, and a faint Milky Way far from the moon
    mw = np.exp(-(((c.xx - (0.98 * W - c.yy * 0.46)) / (0.07 * W)) ** 2)) * (c.yy < hz) * smooth(0.45 * W, 0.75 * W, c.xx)
    mott = fbm(H, W, 40, 4, 141)
    rift = np.exp(-(((c.xx - (0.985 * W - c.yy * 0.46)) / (0.012 * W)) ** 2)) * (0.5 + 0.5 * fbm(H, W, 30, 3, 142))
    c.add(hexc("#AEB8DA"), mw * (0.35 + 0.65 * mott) * 0.12 * (1 - 0.7 * rift))

    def sky_lum(y):
        return 0.03 + 0.25 * min(1.0, max(0.0, y / hz)) ** 2

    stars(c, 300, 143, hz * 0.95, sky_lum, near_moon=(mx, my, mr * 1.6), warm=0.18)
    # a waning gibbous a little after rising: the sun is far below the horizon, so the thin unlit sliver is on the
    # moon's upper side; low in the sky, its light is a little warm
    moon_full(c, M, mx, my, mr, 0.20, 0.98, tint=(1.0, 0.96, 0.88))
    cl = tex_sample(fbm(512, 512, 70, 6, 144), c.xx / 5.0, c.yy / 1.0)
    dens = blur(np.clip((cl - 0.60) * 3.0 * np.exp(-((c.yy - 0.50 * H) / (0.035 * H)) ** 2), 0, 1), 2.0) * (c.xx < 0.62 * W)
    gy_, gx_ = np.gradient(blur(dens, 4))
    vx, vy = mx - c.xx, my - c.yy
    nn = np.sqrt(vx ** 2 + vy ** 2) + 1
    litc = np.clip(-(gx_ * vx / nn + gy_ * vy / nn) * 30, 0, 1) * dens
    ccol = np.stack([np.full((H, W), v, np.float32) for v in hexc("#2C3462")], -1)
    ccol = ccol + (hexc("#C8B9B4") - ccol) * litc[..., None]
    c.over(ccol, dens * 0.8)
    M["clouds"], M["under"] = dens, litc
    xs = np.arange(W, dtype=np.float32)

    # Limsa Lominsa across the harbour: a sea cliff with a natural arch at its point, and on top a terraced city of
    # flat-roofed blocks, crenellated towers, a tall round tower and two arched bridges; lit only on their
    # moon-facing (left) faces, the rest backlit and darker than the sky behind them
    x0h = 0.455 * W
    plat = hz - 0.13 * H - 8 * (fbm1d(W, 90, 4, 145) - 0.5) - 0.02 * H * smooth(0.6 * W, W, xs)
    prof = smooth(x0h, x0h + 0.025 * W, xs)
    cliff_top = hz - (hz - plat) * prof
    head = c.below_curve(cliff_top, 1.2) * (c.yy < hz + 2)
    ax_, aw_ = 0.522 * W, 0.016 * W                                                       # the sea arch: an arch-shaped opening
    arch_h = np.maximum(c.poly([(ax_ - aw_, hz + 2), (ax_ + aw_, hz + 2), (ax_ + aw_, hz - 0.045 * H), (ax_ - aw_, hz - 0.045 * H)], 0.6),
                        c.ellipse(ax_, hz - 0.045 * H, aw_, aw_ * 1.3, 0.6))
    head = head * (1 - arch_h)
    rng = np.random.default_rng(149)
    town = np.zeros((H, W), np.float32)
    x = 0.535 * W
    while x < W:                                                                          # terraces of blocks, stepping up and down
        bw = 30 + 60 * rng.random()
        bh = 14 + 46 * rng.random() * smooth(0.52 * W, 0.62 * W, x)
        y0 = plat[int(min(W - 1, x))] + 6
        town = np.maximum(town, c.poly([(x, y0), (x + bw, y0), (x + bw, y0 - bh), (x, y0 - bh)], 0.6))
        x += bw * (0.6 + 0.3 * rng.random())
    towers = np.zeros((H, W), np.float32)
    for (tx, th, tw_, cap) in ((0.60, 0.15, 40, "flat"), (0.655, 0.21, 46, "flat"), (0.715, 0.13, 36, "spire"), (0.765, 0.19, 50, "flat"),
                               (0.835, 0.16, 40, "flat"), (0.885, 0.24, 34, "round"), (0.94, 0.15, 44, "flat"), (0.985, 0.18, 40, "spire")):
        x = tx * W
        y0 = plat[int(min(W - 1, x))] + 6
        y = y0 - th * H
        towers = np.maximum(towers, c.poly([(x - tw_ / 2, y0), (x + tw_ / 2, y0), (x + tw_ / 2 * (0.86 if cap == "round" else 1), y), (x - tw_ / 2 * (0.86 if cap == "round" else 1), y)], 0.6))
        if cap == "flat":
            for k in range(-2, 3):
                cx_ = x + k * tw_ / 5
                towers = np.maximum(towers, c.poly([(cx_ - tw_ / 14, y + 1), (cx_ + tw_ / 14, y + 1), (cx_ + tw_ / 14, y - 8), (cx_ - tw_ / 14, y - 8)], 0.5))
        elif cap == "spire":
            towers = np.maximum(towers, c.poly([(x - tw_ / 2 - 4, y + 2), (x + tw_ / 2 + 4, y + 2), (x, y - 44)], 0.6))
        else:   # a round tower with a gallery and a domed cap
            towers = np.maximum(towers, c.poly([(x - tw_ / 2 - 6, y + 4), (x + tw_ / 2 + 6, y + 4), (x + tw_ / 2 + 6, y - 4), (x - tw_ / 2 - 6, y - 4)], 0.5))
            towers = np.maximum(towers, c.poly([(x - tw_ / 3, y - 4), (x + tw_ / 3, y - 4), (x + tw_ / 3, y - 22), (x - tw_ / 3, y - 22)], 0.5))
            towers = np.maximum(towers, c.ellipse(x, y - 22, tw_ / 3 + 2, 12, 0.6) * (c.yy < y - 22))
    for (a, b, yb) in ((0.655, 0.765, 0.36), (0.835, 0.94, 0.385)):                       # two-arched bridges between towers
        ys = yb * H + 0.075 * H                                                            # the arches spring here
        span = c.poly([(a * W, yb * H - 8), (b * W, yb * H - 8), (b * W, ys), (a * W, ys)], 0.6)
        q = (b - a) * W
        for f_ in (0.25, 0.75):
            span = span * (1 - c.ellipse(a * W + q * f_, ys, q / 4 - 9, 0.058 * H, 0.6) * (c.yy < ys + 2))
        pier_x = (a + b) / 2 * W
        span = np.maximum(span, c.poly([(pier_x - 9, ys - 4), (pier_x + 9, ys - 4), (pier_x + 12, plat[int(pier_x)] + 6), (pier_x - 12, plat[int(pier_x)] + 6)], 0.6))
        towers = np.maximum(towers, span)
    towers = np.maximum(towers, town)
    far = np.maximum(head, towers)
    fcol = c.vgrad([(0, "#2A315A"), (1, "#20264A")], 0.30 * H, hz)
    fcol = cap_to_sky(c, fcol, far, sky_only, 0.88)
    c.over(fcol, far)
    lit_face = rims(far, -1, 0, 3) * (c.yy < hz)
    c.add(hexc("#9D9BB6"), blur(lit_face, 0.7) * 0.55)
    c.add(hexc("#6C7096"), top_edge(far, 2) * 0.25)
    M["far"] = far
    # the sea under the sky, to the horizon
    field = np.clip((c.yy - hz) / 1.2 + 0.5, 0, 1)
    M["field"] = field

    # the boathouse workshop on its piles, right of centre: gable end lit by the moon, front in shadow
    yw = 0.792 * H            # its waterline
    yd = 0.768 * H            # its deck
    ye = 0.588 * H            # the eaves
    gx0, gx1 = 0.600 * W, 0.652 * W
    fx1 = 0.872 * W
    apex = (0.626 * W, 0.476 * H)
    gable = c.poly([(gx0, yd), (gx1, yd), (gx1, ye), apex, (gx0 - 2, ye)], 0.6)
    front = c.poly([(gx1, yd), (fx1, yd), (fx1, ye), (gx1, ye)], 0.6)
    roof = c.poly([apex, (0.852 * W, 0.476 * H), (fx1 + 14, ye + 2), (gx1, ye + 2)], 0.6)
    piles = np.zeros((H, W), np.float32)
    for px_ in np.arange(gx0 + 8, fx1, 46):
        piles = np.maximum(piles, c.poly([(px_ - 4, yd - 2), (px_ + 4, yd - 2), (px_ + 4, yw + 1), (px_ - 4, yw + 1)], 0.5))
    deck = c.poly([(gx0 - 6, yd - 3), (fx1 + 8, yd - 3), (fx1 + 8, yd + 6), (gx0 - 6, yd + 6)], 0.5)
    house = np.maximum.reduce([gable, front, roof, piles, deck])
    planks = tex_sample(fbm(256, 256, 3, 2, 146), c.xx / 0.25, c.yy / 6.0)
    c.over(c.vgrad([(0, "#4E5A88"), (1, "#3A4470")], ye, yd) * (0.88 + 0.2 * planks)[..., None], gable)
    c.over(c.vgrad([(0, "#1C2238"), (1, "#151A2C")], ye, yd) * (0.9 + 0.16 * planks)[..., None], front)
    shingle = 0.9 + 0.12 * (np.sin(c.yy / 4.0) > 0.6)
    c.over(hexc("#141829") * shingle[..., None], roof)
    c.over(hexc("#0E1220"), np.maximum(piles, deck))
    # closed boat doors on the right, planked
    bdoor = c.poly([(0.800 * W, yd - 2), (0.862 * W, yd - 2), (0.862 * W, 0.640 * H), (0.800 * W, 0.640 * H)], 0.5)
    c.over(hexc("#121726") * (0.9 + 0.2 * planks)[..., None], bdoor)
    c.mul(hexc("#0A0D18"), (np.abs(c.xx - 0.831 * W) < 1.5) * bdoor)
    # the window: warm interior, the lamp low on the bench, tools hanging, mullions
    wx0, wx1, wy0, wy1 = 0.672 * W, 0.772 * W, 0.616 * H, 0.712 * H
    win = c.poly([(wx0, wy1), (wx1, wy1), (wx1, wy0), (wx0, wy0)], 0.5)
    lampx, lampy = 0.700 * W, 0.684 * H
    inter = c.vgrad([(0, "#B8683A"), (0.6, "#E99A55"), (1, "#F7B66E")], wy0, wy1)
    hot = np.exp(-((c.xx - lampx) ** 2 + ((c.yy - lampy) * 1.4) ** 2) / (2 * 34 ** 2))
    inter = 1 - (1 - inter) * (1 - hexc("#FFF0C8") * (hot * 0.9)[..., None])
    c.over(inter, win)
    tools = np.zeros((H, W), np.float32)
    bench = c.poly([(wx0, 0.692 * H), (wx1, 0.692 * H), (wx1, wy1), (wx0, wy1)], 0.5)
    tools = np.maximum(tools, bench)
    tools = np.maximum(tools, c.poly([(0.735 * W, wy0), (0.737 * W, wy0), (0.737 * W, 0.640 * H), (0.735 * W, 0.640 * H)], 0.4))   # a saw on its nail
    tools = np.maximum(tools, c.poly([(0.725 * W, 0.640 * H), (0.752 * W, 0.640 * H), (0.752 * W, 0.652 * H), (0.725 * W, 0.656 * H)], 0.5))
    tools = np.maximum(tools, c.poly([(0.760 * W, wy0), (0.762 * W, wy0), (0.762 * W, 0.662 * H), (0.760 * W, 0.662 * H)], 0.4))   # a mallet
    tools = np.maximum(tools, c.poly([(0.754 * W, 0.656 * H), (0.768 * W, 0.656 * H), (0.768 * W, 0.668 * H), (0.754 * W, 0.668 * H)], 0.5))
    ring = c.ellipse(0.716 * W, 0.640 * H, 13, 13, 0.6) * (1 - c.ellipse(0.716 * W, 0.640 * H, 8, 8, 0.6))                    # a coil of line
    tools = np.maximum(tools, ring)
    lampb = c.poly([(lampx - 6, 0.692 * H), (lampx + 6, 0.692 * H), (lampx + 4, 0.676 * H), (lampx - 4, 0.676 * H)], 0.5)   # the lamp's foot
    c.over(hexc("#3A2216"), tools * win)
    c.over(hexc("#5A3018"), lampb * win)
    mull = ((np.abs(c.xx - (wx0 + (wx1 - wx0) / 4)) < 1.8) | (np.abs(c.xx - (wx0 + (wx1 - wx0) / 2)) < 1.8) | (np.abs(c.xx - (wx0 + 3 * (wx1 - wx0) / 4)) < 1.8) |
            (np.abs(c.yy - (wy0 + (wy1 - wy0) / 2)) < 1.8)) * win
    frame = np.clip(c.poly([(wx0 - 5, wy1 + 5), (wx1 + 5, wy1 + 5), (wx1 + 5, wy0 - 5), (wx0 - 5, wy0 - 5)], 0.5) - win, 0, 1)
    c.over(hexc("#24170E"), mull)
    c.over(hexc("#10131F"), frame)
    M["lantern"] = np.clip(win - mull - tools * win - lampb * win, 0, 1)
    city = np.maximum(house, frame)
    M["city"] = city

    # the boat moored to the left: hull, mast, a furled sail on its boom; rigging as lines
    bwl = 0.836 * H
    bx0, bx1 = 0.360 * W, 0.505 * W
    hull = c.poly([(bx0, bwl - 0.040 * H), (bx0 + 0.02 * W, bwl - 0.022 * H), (bx0 + 0.03 * W, bwl), (bx1 - 0.015 * W, bwl), (bx1, bwl - 0.030 * H),
                   (bx1 - 0.004 * W, bwl - 0.034 * H)], 0.6)
    mast_x = 0.425 * W
    mtop = 0.425 * H
    mast = c.poly([(mast_x - 3.5, bwl - 0.03 * H), (mast_x + 3.5, bwl - 0.03 * H), (mast_x + 2, mtop), (mast_x - 2, mtop)], 0.5)
    boom = c.poly([(mast_x, 0.742 * H), (bx1 - 0.01 * W, 0.752 * H), (bx1 - 0.01 * W, 0.760 * H), (mast_x, 0.752 * H)], 0.5)
    sail = c.poly([(mast_x + 4, 0.736 * H), (bx1 - 0.016 * W, 0.746 * H), (bx1 - 0.016 * W, 0.752 * H), (mast_x + 4, 0.748 * H)], 1.0)
    boat = np.maximum.reduce([hull, mast, boom, sail])
    rig = np.zeros((H, W), np.float32)
    for (p, q) in (((mast_x, mtop + 2), (bx0 + 6, bwl - 0.04 * H)), ((mast_x, mtop + 2), (bx1 - 4, bwl - 0.034 * H))):
        n = int(math.hypot(q[0] - p[0], q[1] - p[1]))
        tt = np.linspace(0, 1, n)
        ys_ = (p[1] + (q[1] - p[1]) * tt).astype(int)
        xs_ = (p[0] + (q[0] - p[0]) * tt).astype(int)
        rig[ys_, xs_] = 1.0
    rig = np.clip(blur(rig, 0.6) * 1.8, 0, 1)
    c.over(hexc("#0C101C"), boat)
    c.over(hexc("#141A2C"), rig * 0.8)
    M["figs"] = boat
    M["rigging"] = rig

    # the harbour: the sky and the cliffs mirrored about the horizon, the boathouse and the boat about their own
    # waterlines; ripples break them into streaks. No moon and no stars in the mirror: the moon gets its own path.
    src = c.px.copy()
    solid = np.maximum.reduce([far, city, boat])
    src = sky_only * (1 - solid[..., None]) + src * solid[..., None]
    reflect(c, field * (1 - np.maximum(city, boat) * (c.yy < yw)), src, hz, [(far, hz), (city, yw), (boat, bwl)], 147,
            amp=(1.2, 22.0), fres=(0.85, 0.42), deep="#0A1230", brk=0.65)
    # the boat stays in front of its reflection
    c.over(hexc("#0C101C"), boat)
    c.over(hexc("#141A2C"), rig * 0.8)
    finish(c, np.maximum.reduce([city, boat, towers, rig, M["moon"]]), 741)
    # after the pass: the moon's path on the water, the moonlit rims, the window's glow and its warmth on the boat
    glitter(c, mx, hz, field * (1 - np.maximum(city, boat)), 148, col="#FFEBC8", strength=0.95, w0=8.0, spread=0.26, thr=0.60)
    c.add(hexc("#B8BDD8"), rims(boat, -1, -1, 2) * 0.45)
    c.add(hexc("#B8BDD8"), rims(house, -1, -1, 2) * (c.xx < 0.70 * W) * 0.45)
    c.add(hexc("#FFB466"), blur(M["lantern"], 9) * 0.55 * (1 - M["lantern"]))
    to_win = np.exp(-((c.xx - wx0) ** 2 + (c.yy - (wy0 + wy1) / 2) ** 2) / (2 * 260 ** 2))
    c.add(hexc("#FFB062"), rims(boat, 1, 0, 3) * to_win * 0.9)
    c.add(hexc("#FFB062"), deck * (c.xx > wx0 - 30) * (c.xx < wx1 + 30) * 0.18)
    seal_far(M)
    vignette(c)
    return c, M


# ======================================================================================================
# 1.15.0 Faces and icons: a Gridanian lodge at the foot of a giant tree, six lit windows, a face in each
# ======================================================================================================
def head(c, x, ys, kind, s=1.0):
    """A head-and-shoulders silhouette standing behind a window sill at ys, by race: miqote, lalafell, elezen,
    roegadyn, aura, viera. Front view, with natural shoulders and neck, so each reads by its outline alone: the
    Roegadyn broad and tall enough to fill the window, the Lalafell small with a large head."""
    #            head r, shoulder half-width, shoulder top above the sill, neck half-width, neck length
    hr, shw, sht, nw, nl = [v * s for v in {"miqote": (10, 23, 15, 5.5, 4), "lalafell": (12, 14, 6, 5.0, 1), "elezen": (9, 21, 16, 5.0, 6),
                                            "roegadyn": (13, 36, 24, 9.0, 3), "aura": (10, 23, 15, 5.5, 4), "viera": (9.5, 21, 15, 5.0, 5)}[kind]]
    y_s = ys - sht
    m = c.ellipse(x, y_s + 12 * s, shw, 13 * s, 0.7)                                       # the rounded line of the shoulders
    m = np.maximum(m, c.poly([(x - shw, y_s + 12 * s), (x + shw, y_s + 12 * s), (x + shw, ys + 12), (x - shw, ys + 12)], 0.6))
    hy = y_s - nl - hr * 0.95
    m = np.maximum(m, c.poly([(x - nw, y_s + 4 * s), (x + nw, y_s + 4 * s), (x + nw * 0.9, hy), (x - nw * 0.9, hy)], 0.6))   # the neck
    m = np.maximum(m, c.poly([(x - nw * 2.2, y_s + 1 * s), (x + nw * 2.2, y_s + 1 * s), (x + nw, y_s - 3 * s), (x - nw, y_s - 3 * s)], 0.6))  # its base
    m = np.maximum(m, c.ellipse(x, hy, hr, hr * (1.28 if kind == "elezen" else 1.12), 0.7))
    if kind == "miqote":
        for sd in (-1, 1):
            m = np.maximum(m, c.poly([(x + sd * 3 * s, hy - hr * 0.8), (x + sd * 11 * s, hy - hr * 0.4), (x + sd * 10 * s, hy - hr * 1.75)], 0.6))
    elif kind == "elezen":
        for sd in (-1, 1):
            m = np.maximum(m, c.poly([(x + sd * hr * 0.8, hy - 2 * s), (x + sd * hr * 0.8, hy + 4 * s), (x + sd * (hr + 15 * s), hy - 9 * s)], 0.6))
    elif kind == "lalafell":
        m = np.maximum(m, c.ellipse(x, hy - hr * 1.05, 4.5 * s, 4.5 * s, 0.6))          # a topknot
    elif kind == "aura":
        for sd in (-1, 1):
            m = np.maximum(m, c.poly([(x + sd * hr * 0.55, hy - hr * 0.55), (x + sd * hr * 0.95, hy - hr * 0.2), (x + sd * (hr + 9 * s), hy - hr * 1.5),
                                      (x + sd * (hr + 4 * s), hy - hr * 1.35)], 0.6))
    elif kind == "viera":
        for sd in (-1, 1):
            m = np.maximum(m, c.poly([(x + sd * 3 * s, hy - hr * 0.7), (x + sd * 8 * s, hy - hr * 0.8), (x + sd * 10 * s, hy - hr * 3.1), (x + sd * 6 * s, hy - hr * 3.2)], 0.7))
    return m


def fronds(w, h, bases, seed):
    """Fern fronds: an arching rachis with leaflets on both sides, shortening toward the tip. Drawn with Pillow in
    one pass (many small strokes), returned as a soft coverage mask."""
    from PIL import Image, ImageDraw
    im = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(im)
    rng = np.random.default_rng(seed)
    for (bx, by, a0, ln) in bases:
        a = math.radians(a0)
        dx, dy = math.cos(a), math.sin(a)
        curl = (0.35 + 0.2 * rng.random()) * ln * (1 if dx > 0 else -1)
        pts = []
        for i in range(25):
            t = i / 24
            pts.append((bx + dx * ln * t + curl * t * t * 0.6, by + dy * ln * t + abs(curl) * t * t * 0.9))
        d.line(pts, fill=255, width=3)
        for i in range(2, 23):
            t = i / 24
            px_, py_ = pts[i]
            tx_, ty_ = pts[i + 1][0] - pts[i - 1][0], pts[i + 1][1] - pts[i - 1][1]
            n_ = math.hypot(tx_, ty_) + 1e-6
            tx_, ty_ = tx_ / n_, ty_ / n_
            ll = ln * 0.20 * (1 - t) ** 0.7 + 4
            for sd in (-1, 1):
                ca, sa = math.cos(math.radians(55)), math.sin(math.radians(55)) * sd   # leaflets angled forward
                lx_, ly_ = tx_ * ca - ty_ * sa, tx_ * sa + ty_ * ca
                d.line([(px_, py_), (px_ + lx_ * ll, py_ + ly_ * ll)], fill=255, width=3)
    return blur(np.asarray(im, np.float32) / 255.0, 0.6)


def faces():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.63 * H
    c.px = c.vgrad([(0, "#040A18"), (0.45, "#0A1A30"), (0.80, "#15303E"), (1.0, "#22424A")], 0, hz)
    c.px[int(hz):] = hexc("#22424A")
    mx, my, mr = 0.16 * W, 0.17 * H, 36.0
    c.add(hexc("#7FA2C0"), np.exp(-(c.radial(mx, my, 0.40 * W, 0.55 * H)) ** 2 * 2.2) * 0.20)
    sky_only = c.px.copy()

    def sky_lum(y):
        return 0.03 + 0.22 * min(1.0, max(0.0, y / hz)) ** 2

    stars(c, 160, 151, hz * 0.7, sky_lum, near_moon=(mx, my, mr), warm=0.10)
    # the first-quarter moon of early night: the sun has set to the lower right, so its right half is lit
    moon(c, M, mx, my, mr, 0.80, 0.60, 0.0, tint="#EEF0E6", unlit="#14243A", unlit_a=0.35, glow="#C8DCF0", glow_a=0.16)
    xs = np.arange(W, dtype=np.float32)

    def crowns(base, amp, seed, cell):
        n = fbm1d(W, cell, 5, seed)
        bumps = np.zeros(W, np.float32)
        rng = np.random.default_rng(seed)
        for _ in range(int(W / cell * 2.2)):
            cx_ = rng.random() * W
            r_ = cell * (0.35 + 0.5 * rng.random())
            hgt = amp * (0.5 + 0.7 * rng.random())
            bumps = np.maximum(bumps, hgt * np.sqrt(np.clip(1 - ((xs - cx_) / r_) ** 2, 0, 1)))
        return base - bumps - 6 * (n - 0.5)

    # the forest beyond the clearing: two walls of giant trees in mist, the farther paler; trunks show under crowns
    lay = []
    for i, (base, amp, seed, cell, top, bot, trunks) in enumerate([(0.44 * H, 0.14 * H, 152, 190, "#1E3A44", "#24444C", 10),
                                                                     (0.50 * H, 0.15 * H, 153, 240, "#122830", "#163038", 8)]):
        r = crowns(base, amp, seed, cell)
        r = np.where(xs < 0.30 * W, r + (0.30 * W - xs) / (0.30 * W) * 0.10 * H * (1 - i * 0.3), r)    # the gap the moon shines through
        m = c.below_curve(r, 1.2) * (c.yy < hz + 2)
        rng = np.random.default_rng(seed + 9)
        tr_m = np.zeros((H, W), np.float32)
        for _ in range(trunks):                                                      # the giants' trunks under their crowns
            tx = (0.03 + 0.94 * rng.random()) * W
            tw_ = (22 + 26 * rng.random()) * (1 + i * 0.5)
            tr_m = np.maximum(tr_m, c.poly([(tx - tw_ * 0.75, hz + 2), (tx + tw_ * 0.75, hz + 2), (tx + tw_ * 0.45, hz - 0.03 * H), (tx + tw_ * 0.4, r[int(tx)] + 10), (tx - tw_ * 0.4, r[int(tx)] + 10), (tx - tw_ * 0.45, hz - 0.03 * H)], 0.8))
        m = np.maximum(m, tr_m)
        col = c.vgrad([(0, top), (1, bot)], r.min(), hz)
        col = cap_to_sky(c, col, m, sky_only, 0.90 - 0.06 * i)
        c.over(col, m)
        # trunks darker than the misty crowns, fading up into the foliage and into the mist at their feet
        fade = smooth(r[None, :] + 0.01 * H, r[None, :] + 0.09 * H, c.yy) * (1 - 0.6 * smooth(hz - 0.07 * H, hz, c.yy))
        c.mul(hexc("#0A1418"), tr_m * fade * (0.30 + 0.10 * i))
        c.add(hexc("#8FB0C0"), rims(m, -1, -1, 2) * (c.yy < hz) * (0.30 - 0.1 * i))
        c.add(hexc("#5F8A92"), np.exp(-((c.yy - (hz - 0.02 * H)) / (0.05 * H)) ** 2) * m * (0.20 - 0.06 * i))   # mist at their feet
        lay.append(m)
        M["far"] = np.maximum(M["far"], m)
    M["far_a"], M["far_b"] = lay[0] * (1 - lay[1]), lay[1]

    # the clearing floor
    field = c.below_curve(np.full(W, hz, np.float32) - 4 * fbm1d(W, 200, 3, 154), 1.5)
    gcol = c.vgrad([(0, "#1E3A40"), (0.4, "#152A30"), (1, "#0A161C")], hz, H)
    moss = fbm(H, W, 6, 3, 155)
    gcol = gcol * (0.86 + 0.24 * moss)[..., None]
    c.over(gcol, field)
    M["field"] = field
    # moonlight shafts through the mist above the clearing, from the moon's direction, falling down and right
    ang = np.arctan2(c.yy - my, c.xx - mx)
    rays = fbm1d(1440, 30, 3, 156)[np.clip(((ang + math.pi) / (2 * math.pi) * 1439).astype(int), 0, 1439)]
    shaft = np.clip((rays - 0.5) * 3, 0, 1) * smooth(0.30 * H, 0.50 * H, c.yy) * (1 - smooth(0.66 * H, 0.86 * H, c.yy)) * (c.xx < 0.62 * W) * (c.xx > 0.12 * W)
    c.add(hexc("#9EC0CC"), shaft * 0.07)

    # the giant tree on the right: its trunk rises out of frame, roots flaring; a bough reaches over the top
    tx0, tx1 = 0.850 * W, 1.02 * W
    root_y = 0.81 * H
    trunk = c.poly([(tx0 - 0.07 * W, root_y + 8), (tx0 - 0.03 * W, root_y - 0.02 * H), (tx0 - 0.008 * W, 0.68 * H), (tx0, 0.55 * H), (tx0, -10), (tx1, -10), (tx1, root_y + 8)], 1.0)
    for (rx0, rw) in ((tx0 - 0.05 * W, 0.04 * W), (tx0 + 0.04 * W, 0.05 * W)):     # root flare: buttress roots into the ground
        trunk = np.maximum(trunk, c.poly([(rx0 - rw, root_y + 10), (rx0 + rw, root_y + 10), (rx0 + rw * 0.2, root_y - 0.07 * H)], 1.0))
    bough = np.zeros((H, W), np.float32)
    # the giant's own canopy: one dark mass hanging into the top right, its lower edge ragged with leaf clusters
    xs_ = np.arange(W, dtype=np.float32)
    edge = 0.035 * H * smooth(0.58 * W, 0.72 * W, xs_) + 6 * (fbm1d(W, 12, 2, 158) - 0.5)
    rngc = np.random.default_rng(157)
    for _ in range(26):                                                            # leaf clusters hanging from the boughs
        cx_ = (0.60 + 0.42 * rngc.random()) * W
        r_ = 30 + 50 * rngc.random()
        dep = (0.03 + 0.07 * rngc.random()) * H * smooth(0.58 * W, 0.75 * W, cx_)
        edge = np.maximum(edge, 0.03 * H * smooth(0.58 * W, 0.72 * W, xs_) + dep * np.sqrt(np.clip(1 - ((xs_ - cx_) / r_) ** 2, 0, 1)))
    leaves = (1 - c.below_curve(edge, 1.5)) * (c.xx > 0.58 * W)
    tree = np.maximum.reduce([trunk, bough, leaves])
    bark = tex_sample(fbm(256, 256, 6, 3, 158), c.xx / 0.5, c.yy / 6.0)
    ridges = 0.5 + 0.5 * np.sin(c.xx / 7.0 + bark * 6.0)                            # deep vertical furrows in the bark
    c.over(c.vgrad([(0, "#0E1A1E"), (1, "#0C1618")], 0, H) * (0.62 + 0.55 * bark * ridges)[..., None] * (1 + 0.25 * trunk * (c.xx < tx0 + 0.04 * W))[..., None], tree)

    # the lodge at its foot: plank walls, a steep shingled roof with moss and upturned eaves, a stone footing
    lx0, lx1 = 0.455 * W, 0.845 * W
    wall_top, wall_bot = 0.585 * H, 0.785 * H
    walls = c.poly([(lx0, wall_bot), (lx1, wall_bot), (lx1, wall_top), (lx0, wall_top)], 0.6)
    roof = c.poly([(lx0 - 34, wall_top + 12), (lx0 - 10, wall_top + 2), (lx0 + 0.06 * W, 0.455 * H), (lx1 - 0.03 * W, 0.445 * H), (lx1 + 26, wall_top + 2), (lx1 + 30, wall_top + 12)], 0.8)
    foot = c.poly([(lx0 - 8, 0.808 * H), (lx1 + 8, 0.808 * H), (lx1 + 4, wall_bot - 4), (lx0 - 4, wall_bot - 4)], 0.6)
    steps = np.zeros((H, W), np.float32)
    lodge = np.maximum.reduce([walls, roof, foot, steps])
    planks = tex_sample(fbm(256, 256, 3, 2, 159), c.xx / 0.22, c.yy / 6.0)
    c.over(c.vgrad([(0, "#1A2228"), (1, "#141B20")], wall_top, wall_bot) * (0.86 + 0.24 * planks)[..., None], walls)
    sh_ = 0.88 + 0.14 * (np.sin((c.yy - 0.0 * c.xx) / 5.0) > 0.5)
    c.over(c.vgrad([(0, "#18282A"), (1, "#121C1E")], 0.445 * H, wall_top + 12) * sh_[..., None], roof)
    c.add(hexc("#3E5A48"), roof * np.clip((fbm(H, W, 22, 3, 160) - 0.55) * 3, 0, 1) * 0.20)        # moss on the shingles
    c.over(hexc("#20282C") * (0.85 + 0.3 * fbm(H, W, 4, 2, 161))[..., None], np.maximum(foot, steps))
    city = np.maximum(lodge, tree)
    M["city"] = city
    # six arched windows, a face in each: Miqo'te, Lalafell, Elezen, Roegadyn, Au Ra and Viera
    kinds = ["miqote", "lalafell", "elezen", "roegadyn", "aura", "viera"]
    wy0, wy1 = 0.615 * H, 0.735 * H
    ww = 0.036 * W
    win = np.zeros((H, W), np.float32)
    faces_m = np.zeros((H, W), np.float32)
    wcs = [lx0 + (lx1 - lx0) * (0.12 + 0.76 * i / 5) for i in range(6)]
    for wc, kind in zip(wcs, kinds):
        wm_ = np.maximum(c.poly([(wc - ww / 2, wy1), (wc + ww / 2, wy1), (wc + ww / 2, wy0 + ww / 2), (wc - ww / 2, wy0 + ww / 2)], 0.5),
                         c.ellipse(wc, wy0 + ww / 2, ww / 2, ww / 2, 0.5) * (c.yy < wy0 + ww / 2 + 1))
        win = np.maximum(win, wm_)
        faces_m = np.maximum(faces_m, head(c, wc, wy1 - 2, kind, 1.25) * wm_)
    inter = c.vgrad([(0, "#F2B26A"), (0.5, "#FFC47E"), (1, "#E89452")], wy0, wy1)
    c.over(inter, win)
    c.over(hexc("#2A1810"), faces_m)
    sill = np.clip(c.poly([(lx0, wy1 + 7), (lx1, wy1 + 7), (lx1, wy1), (lx0, wy1)], 0.5) * blur(win, 12) * 6, 0, 1)
    c.over(hexc("#0E1316"), sill)
    M["figs"] = faces_m
    M["lantern"] = np.clip(win - faces_m, 0, 1)
    # the near bank: a low rise in the foreground with ferns, rim-lit on their moon (upper-left) edges
    crest = 0.905 * H + 0.02 * H * np.sin(xs / W * 3.5) + 5 * (fbm1d(W, 150, 3, 162) - 0.5)
    rm = c.below_curve(crest, 1.5)
    c.over(c.vgrad([(0, "#132428"), (1, "#081014")], crest.min(), H), rm)
    M["ridge"] = rm
    rng = np.random.default_rng(163)
    bases = []
    for (x0_, x1_, lean) in ((0.0, 0.17, -1), (0.83, 1.0, 1)):                       # fern fronds at both near corners
        for _ in range(11):
            bx = (x0_ + rng.random() * (x1_ - x0_)) * W
            by = crest[int(min(W - 1, bx))] + 14
            a0 = -90 + lean * (15 + 50 * rng.random())
            bases.append((bx, by, a0, 110 + 90 * rng.random()))
    fern = fronds(W, H, bases, 164)
    c.over(hexc("#08120F"), fern)

    # the moon's shadows: the lodge throws its shadow toward the viewer and right; the windows' warm pools lie in it
    lshadow = c.poly([(lx0 + 0.03 * W, 0.808 * H), (lx1, 0.808 * H), (lx1 + 0.10 * W, 0.90 * H), (lx0 + 0.10 * W, 0.90 * H)], 10) * field * (1 - rm)
    c.mul(hexc("#08121A"), lshadow * 0.45)
    finish(c, np.maximum.reduce([city, faces_m, win, fern, M["moon"]]), 751)
    pools = np.zeros((H, W), np.float32)
    for wc in wcs:
        d_ = np.clip(c.yy - 0.81 * H, 0, None)
        pools += np.exp(-((c.xx - wc - d_ * 0.15) / (ww * 0.7 + d_ * 0.45)) ** 2) * smooth(0.808 * H, 0.83 * H, c.yy) * np.exp(-d_ / 70) * (1 - smooth(0.85 * H, 0.89 * H, c.yy))
    c.add(hexc("#FFB062"), np.clip(pools, 0, 1) * field * (1 - rm) * 0.22)
    c.add(hexc("#FFB466"), blur(win, 8) * 0.45 * (1 - win))
    c.add(hexc("#FFB062"), np.maximum(foot, steps) * np.clip(blur(win, 30) * 3, 0, 1) * 0.25)
    c.add(hexc("#AFC8D8"), rims(np.maximum(lodge, tree), -1, -1, 2) * (c.yy < 0.80 * H) * 0.40)
    c.add(hexc("#7F9FAA"), rims(fern, -1, -1, 2) * 0.40)
    seal_far(M, ["far_a", "far_b"])
    vignette(c)
    return c, M


# ======================================================================================================
# 1.16.0 Themes: one moon over four horizons, seen from a summit
# ======================================================================================================
def themes():
    c = Canvas(W, H)
    M = zeros()
    sea_y = 0.44 * H
    c.px = c.vgrad([(0, "#050A1C"), (0.45, "#0E1A3C"), (0.85, "#25386A"), (1.0, "#3A4C80")], 0, sea_y)
    c.px[int(sea_y):] = hexc("#3A4C80")
    mx, my, mr = 0.70 * W, 0.16 * H, 44.0
    c.add(hexc("#8EA6DC"), np.exp(-(c.radial(mx, my, 0.45 * W, 0.65 * H)) ** 2 * 2.0) * 0.24)
    sky_only = c.px.copy()

    def sky_lum(y):
        return 0.03 + 0.24 * min(1.0, max(0.0, y / sea_y)) ** 2

    stars(c, 200, 161, sea_y * 0.85, sky_lum, near_moon=(mx, my, mr * 1.4), warm=0.08)
    # Menphina's moon: near full, the thin unlit sliver on its upper left (the sun is far below the lower right)
    moon_full(c, M, mx, my, mr)
    xs = np.arange(W, dtype=np.float32)
    layers = []
    # 1. the farthest horizon: the sea, a level line, palest, with the moon's sheen on it below the moon
    sea = np.clip((c.yy - sea_y) / 1.0 + 0.5, 0, 1)
    c.over(c.vgrad([(0, "#3E4E80"), (1, "#30406E")], sea_y, 0.56 * H), sea)
    c.add(hexc("#E6ECFF"), sea * np.exp(-((c.xx - mx) / (0.03 * W + (c.yy - sea_y) * 0.5)) ** 2) * np.exp(-(c.yy - sea_y) / 16) * 0.6)
    layers.append(sea)
    # 2. Coerthas: tall jagged snow peaks on the left, their moon-facing (right) slopes lit, the faces toward us in
    # blue shade, falling away to the sea toward the middle
    n = fbm1d(W, 150, 6, 164)
    pk = np.zeros(W, np.float32)
    for (px_, ph, pw) in ((0.05, 0.26, 0.10), (0.14, 0.32, 0.10), (0.24, 0.24, 0.09), (0.32, 0.28, 0.08), (0.41, 0.17, 0.09), (0.50, 0.08, 0.08)):
        pk = np.maximum(pk, ph * H * np.clip(1 - np.abs(xs - px_ * W) / (pw * W), 0, 1) ** 1.15)
    snow_top = sea_y + 0.06 * H - pk - 0.015 * H * (n - 0.5) * 2
    snow_top = np.where(xs > 0.58 * W, sea_y + 0.10 * H, snow_top)
    sm = c.below_curve(snow_top, 1.2)
    sl = np.gradient(blur(np.repeat(snow_top[None, :], 3, 0), 3)[1])
    # the light softens across each apex (no vertical edge under a summit) and breaks into couloirs running down the
    # fall line, as snow lies in gullies between dark rock ribs
    face1 = blur(np.repeat(np.clip(sl * 2.2, 0, 1)[None, :], 3, 0), 22)[1]
    depth = np.clip(c.yy - snow_top[None, :], 0, None)
    facing = face1[None, :] * np.exp(-depth / 80) + 0.25 * np.clip(1 - face1[None, :], 0, 1) * np.exp(-depth / 14)
    gullies = tex_sample(fbm(256, 256, 8, 3, 165), (c.xx + c.yy * 0.9) / 1.4, (c.yy - c.xx * 0.5) / 7.0)
    gullies = np.clip((gullies - 0.35) * 2.2, 0, 1)
    # side-lit, not backlit: the moonlit snow is brighter than the night sky behind it, the shaded faces darker
    scol = c.vgrad([(0, "#1E2850"), (1, "#1A2246")], snow_top.min(), 0.6 * H)
    scol = scol + (hexc("#C4CDEA") - scol) * (np.clip(facing * (0.55 + 0.7 * gullies), 0, 1) * 0.92)[..., None]
    scol = scol * (0.82 + 0.18 * np.exp(-((c.yy - sea_y) / (0.25 * H)) ** 2))[..., None]
    c.over(scol, sm)
    c.add(hexc("#C8D4F4"), rims(sm, 1, -1, 2) * 0.35)
    c.add(hexc("#7E90C8"), np.exp(-((c.yy - (sea_y + 0.07 * H)) / 14) ** 2) * sm * 0.16)
    layers.append(sm)
    # 3. Mor Dhona: a rocky ridge with great crystal spires, lit on the facets that face the moon, none near it
    rbase = 0.56 * H - 0.03 * H * fbm1d(W, 260, 4, 166)
    rmask = c.below_curve(rbase, 1.2)
    crys = np.zeros((H, W), np.float32)
    crys_lit = np.zeros((H, W), np.float32)
    crys_r = np.zeros((H, W), np.float32)
    crys_l = np.zeros((H, W), np.float32)
    for (cx_, ch, cw, tilt) in ((0.17, 0.22, 36, -0.10), (0.21, 0.13, 24, 0.18), (0.28, 0.27, 42, 0.05), (0.32, 0.12, 22, 0.30), (0.36, 0.17, 28, -0.2),
                                (0.90, 0.20, 36, 0.12), (0.94, 0.12, 22, -0.25), (0.98, 0.16, 30, 0.0)):
        x = cx_ * W
        yb_ = rbase[int(min(W - 1, x))] + 6
        tipx, tipy = x + tilt * ch * H, yb_ - ch * H
        body = c.poly([(x - cw, yb_), (x + cw, yb_), (tipx + cw * 0.35, tipy + cw * 0.9), (tipx, tipy), (tipx - cw * 0.35, tipy + cw * 0.9)], 0.6)
        if x < mx:   # the moon is to this crystal's upper right: its right facets face it
            face = c.poly([(x, yb_), (x + cw, yb_), (tipx + cw * 0.35, tipy + cw * 0.9), (tipx, tipy)], 0.6)
            crys_r = np.maximum(crys_r, body)
        else:        # the moon is to its upper left
            face = c.poly([(x - cw, yb_), (x, yb_), (tipx, tipy), (tipx - cw * 0.35, tipy + cw * 0.9)], 0.6)
            crys_l = np.maximum(crys_l, body)
        crys = np.maximum(crys, body)
        crys_lit = np.maximum(crys_lit, face * body)
    third = np.maximum(rmask, crys)
    rcol = c.vgrad([(0, "#1E2850"), (1, "#182246")], 0.30 * H, 0.70 * H)
    rcol = cap_to_sky(c, rcol, third, sky_only, 0.9)                        # the rock ridge is backlit
    # crystal faces, lit or not: a cool, low-saturation blue no brighter than the peaks' moonlit snow
    ccol = hexc("#2C3C66") * (1 - crys_lit[..., None]) + hexc("#7E8FBC") * crys_lit[..., None]
    rcol = rcol * (1 - crys[..., None]) + ccol * crys[..., None]
    c.over(rcol, third)
    c.add(hexc("#D2E0F6"), (rims(crys_r, 1, -1, 2) + rims(crys_l, -1, -1, 2)) * 0.4)
    c.add(hexc("#6A80B8"), np.exp(-((c.yy - (rbase.mean() + 0.01 * H)) / 16) ** 2) * (1 - third) * (c.yy > 0.5 * H) * 0.0)
    layers.append(third)
    # 4. the forest hills below the summit: dark, a serrated conifer edge, mist in the valley in front of the ridge
    c.add(hexc("#5E72AA"), np.exp(-((c.yy - 0.66 * H) / 22) ** 2) * third * 0.16)
    ftop = 0.69 * H - 0.035 * H * fbm1d(W, 300, 4, 167)
    serr = 18 * (1 - np.abs(((xs / 20.0 + 3 * fbm1d(W, 60, 2, 168)) % 1.0) - 0.5) * 2) ** 1.5 * (0.5 + 0.7 * fbm1d(W, 60, 2, 169))   # pointed conifer tops
    fmask = c.below_curve(ftop - serr, 1.0)
    fcol = c.vgrad([(0, "#141C3A"), (1, "#0C1228")], ftop.min(), 0.9 * H)
    c.over(fcol, fmask)
    c.add(hexc("#8EA0D2"), rims(fmask, 0, -1, 2) * 0.2)
    layers.append(fmask)
    for i, m in enumerate(layers):
        nearer = np.zeros((H, W), np.float32)
        for m2 in layers[i + 1:]:
            nearer = np.maximum(nearer, m2)
        M["far_" + "abcd"[i]] = np.clip(m - nearer, 0, 1)
        M["far"] = np.maximum(M["far"], m)
    # the summit rock: a dark outcrop rising to the right, lit on its crest by the moon
    crest = 0.93 * H - 0.12 * H * smooth(0.0, 0.70 * W, xs) ** 1.4 + 0.06 * H * smooth(0.80 * W, W, xs) + 8 * (fbm1d(W, 80, 4, 170) - 0.5)
    c.add(hexc("#6A7EB4"), np.exp(-((c.yy - (crest[None, :] - 0.06 * H)) / (0.07 * H)) ** 2) * (0.6 + 0.6 * fbm(H, W, 90, 3, 172)) * 0.10)   # thin moonlit mist behind the summit
    rm = c.below_curve(crest, 1.3)
    rock = tex_sample(fbm(256, 256, 10, 4, 171), c.xx / 1.2, c.yy / 1.6)
    c.over(c.vgrad([(0, "#1A2240"), (1, "#0A0E1C")], crest.min(), H) * (0.8 + 0.35 * rock)[..., None], rm)
    c.add(hexc("#B8C6EE"), top_edge(rm, 3) * 0.35)
    M["ridge"] = rm
    # the watcher, seated on the crest right of centre, looking up at the moon; a short shadow toward us
    s = 1.5
    fx, fy = 0.73 * W, crest[int(0.73 * W)] + 3
    contact_shadow(c, fx + 4, fy, 54 * s, 10, 22 * s, 0.55)
    P = lambda pts: [(fx + a * s, fy + b * s) for a, b in pts]
    w_ = c.poly(P([(-22, 0), (30, 0), (34, -14), (8, -22), (10, -54), (-6, -58), (-18, -40), (-24, -10)]), 0.7)
    w_ = np.maximum(w_, c.ellipse(fx + 2 * s, fy - 66 * s, 11 * s, 13 * s, 0.7))                   # the hood
    w_ = np.maximum(w_, c.poly(P([(8, -22), (30, -30), (38, -12), (30, -10)]), 0.6))                 # knees drawn up
    w_ = np.maximum(w_, c.poly(P([(-14, -44), (-30, -30), (-24, -6), (-14, -10)]), 0.7))            # cloak over the rock
    c.over(hexc("#0C1020"), w_)
    M["figs"] = w_
    finish(c, np.maximum.reduce([w_, crys, M["moon"]]), 761)
    c.add(hexc("#B8C6EE"), rims(w_, 1, -1, 2) * 0.6)
    M["city"] = crys * (1 - fmask)                                               # the crystals are their own shapes
    M["far_c"] = np.clip(M["far_c"] - M["city"], 0, 1)
    seal_far(M, ["far_a", "far_b", "far_c", "far_d"])
    vignette(c, cy=0.40)
    return c, M


# ======================================================================================================
# 1.17.0 Mix and match: stepping stones of six different stones across a stream in a Kugane garden, at dawn
# ======================================================================================================
def mixmatch():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.56 * H
    gx, gy = 0.24 * W, hz + 0.02 * H
    # the Dawn palette: plum night above, a rose horizon over the sun still under it
    c.px = c.vgrad([(0, "#140C22"), (0.35, "#26183A"), (0.68, "#3E2448"), (0.88, "#64385A"), (1.0, "#86506C")], 0, hz)   # rose only toward the sun
    c.px[int(hz):] = hexc("#86506C")
    d = c.radial(gx, gy, 0.50 * W, 0.40 * H)
    c.add(hexc("#F2A688"), np.exp(-d ** 2 * 3.0) * 0.55)
    c.add(hexc("#FFD6B0"), np.exp(-(c.radial(gx, gy, 0.14 * W, 0.08 * H)) ** 2 * 1.4) * 0.5)
    c.add(hexc("#E89A8A"), np.exp(-((c.yy - (hz - 0.05 * H)) / (0.06 * H)) ** 2) * np.exp(-((c.xx - gx) / (0.45 * W)) ** 2) * 0.35)
    sky_only = c.px.copy()
    mx, my, mr = 0.40 * W, 0.22 * H, 34.0

    def sky_lum(y):
        return 0.05 + 0.40 * min(1.0, max(0.0, y / hz)) ** 2

    stars(c, 70, 171, hz * 0.45, sky_lum, near_moon=(mx, my, mr), warm=0.1)
    # the old crescent, low near the coming sun and lit toward it (lower left)
    ux, uy = gx - mx, gy - my
    nn = math.hypot(ux, uy)
    moon(c, M, mx, my, mr, ux / nn, uy / nn, 0.50, tint="#FBEFE0", unlit="#2E1E40", unlit_a=0.25, seas=False, glow="#F6D8C8", glow_a=0.10)
    # a few thin streaks of cloud, lit on their undersides by the dawn, warmest near it
    cl = tex_sample(fbm(512, 512, 70, 6, 172), c.xx / 5.5, c.yy / 1.0)
    dens = blur(np.clip((cl - 0.55) * 3.2 * np.exp(-((c.yy - 0.34 * H) / (0.06 * H)) ** 2), 0, 1), 2.0)
    under = np.clip(-np.gradient(blur(dens, 4), axis=0) * 40, 0, 1)
    near = np.exp(-((c.xx - gx) / (0.35 * W)) ** 2)
    ccol = np.stack([np.full((H, W), v, np.float32) for v in hexc("#3A2448")], -1)
    lit = hexc("#F0A88C") * near[..., None] + hexc("#A87090") * (1 - near)[..., None]
    ccol = ccol + (lit - ccol) * np.clip(under * (0.4 + 0.6 * near), 0, 1)[..., None]
    c.over(ccol, dens * 0.85 * (1 - c.ellipse(mx, my, mr * 1.6, mr * 1.6, 6)))
    M["clouds"], M["under"] = dens, under * dens
    xs = np.arange(W, dtype=np.float32)
    # the far hills, backlit by the dawn, darker than the sky, with a warm rim near the glow; Kugane's keep on the left
    r1 = hz - 0.05 * H - 0.05 * H * fbm1d(W, 420, 5, 173) - 0.03 * H * np.exp(-((xs - 0.30 * W) / (0.10 * W)) ** 2)
    h1 = c.below_curve(r1, 1.2) * (c.yy < hz + 0.06 * H)
    k_x, k_base = 0.30 * W, r1[int(0.30 * W)] + 4
    keep = np.zeros((H, W), np.float32)
    keep = np.maximum(keep, c.poly([(k_x - 64, k_base + 6), (k_x + 64, k_base + 6), (k_x + 52, k_base - 30), (k_x - 52, k_base - 30)], 0.6))  # the stone base
    yb_ = k_base - 30
    for i, (wdt, hgt) in enumerate(((54, 34), (44, 30), (34, 26), (24, 24))):
        top_ = yb_ - hgt
        keep = np.maximum(keep, c.poly([(k_x - wdt * 0.8, yb_ + 2), (k_x + wdt * 0.8, yb_ + 2), (k_x + wdt * 0.8, top_ + 10), (k_x - wdt * 0.8, top_ + 10)], 0.6))
        # the curved roof of each tier, its eaves sweeping up at the ends
        keep = np.maximum(keep, c.poly([(k_x - wdt * 1.35, top_ + 6), (k_x - wdt * 1.1, top_ + 12), (k_x + wdt * 1.1, top_ + 12), (k_x + wdt * 1.35, top_ + 6),
                                        (k_x + wdt * 0.55, top_ - 4), (k_x - wdt * 0.55, top_ - 4)], 0.6))
        if i < 3:   # a cusped gable on the front
            keep = np.maximum(keep, c.poly([(k_x - 12, top_ + 2), (k_x, top_ - 12), (k_x + 12, top_ + 2)], 0.5))
        yb_ = top_
    keep = np.maximum(keep, c.poly([(k_x - 22, yb_ - 2), (k_x + 22, yb_ - 2), (k_x + 8, yb_ - 14), (k_x - 8, yb_ - 14)], 0.6))
    for sd in (-1, 1):   # the shachihoko on the ridge ends
        keep = np.maximum(keep, c.poly([(k_x + sd * 14, yb_ - 12), (k_x + sd * 20, yb_ - 24), (k_x + sd * 22, yb_ - 12)], 0.5))
    h1all = np.maximum(h1, keep)
    hcol = c.vgrad([(0, "#5A3A62"), (1, "#4A3258")], r1.min() - 0.15 * H, hz + 0.06 * H)
    hcol = cap_to_sky(c, hcol, h1all, sky_only, 0.86)
    c.over(hcol, h1all)
    rim = rims(h1all, 0, -1, 2) * (0.25 + 0.75 * np.exp(-((c.xx - gx) / (0.22 * W)) ** 2))
    c.add(hexc("#FFC8A2"), rim * 0.75)
    c.add(hexc("#FFC8A2"), rims(keep, -1, 0, 2) * np.exp(-((c.xx - gx) / (0.2 * W)) ** 2) * 0.5)    # the keep's sunward edge
    M["city"] = keep
    # the far bank: black pines with flat foliage pads on the right, a low dark bank across
    r2 = hz + 0.035 * H + 6 * (fbm1d(W, 160, 3, 174) - 0.5)
    h2 = c.below_curve(r2, 1.2) * (c.yy < 0.66 * H)
    rng = np.random.default_rng(175)
    pines = np.zeros((H, W), np.float32)
    for (px_, ph) in ((0.62, 0.16), (0.70, 0.20), (0.80, 0.14), (0.93, 0.22), (0.08, 0.10)):
        x = px_ * W
        yb2 = r2[int(x)] + 4
        top_ = yb2 - ph * H
        pines = np.maximum(pines, c.poly([(x - 5, yb2), (x + 5, yb2), (x + 14, top_ + ph * H * 0.5), (x + 4, top_ + 10), (x - 2, top_ + 10), (x + 6, top_ + ph * H * 0.5)], 0.6))
        for k in range(5):
            py = top_ + k * ph * H * 0.17 + rng.normal(0, 3)
            pxo = x + rng.normal(0, 16) + (k % 2 - 0.5) * 30
            pw_ = 50 - 6 * k + 20 * rng.random()
            pines = np.maximum(pines, c.ellipse(pxo, py, pw_, 11 + 4 * rng.random(), 1.0) * (c.yy < py + 6))
    h2all = np.maximum(h2, pines)
    c.over(cap_to_sky(c, c.vgrad([(0, "#2E2038"), (1, "#24182E")], r2.min() - 0.2 * H, 0.66 * H), h2all, sky_only, 0.62), h2all)
    c.add(hexc("#E8A890"), rims(h2all, -1, -1, 2) * 0.35)
    M["far_a"] = np.clip(h1 - h2all, 0, 1)
    M["far_b"] = h2all
    M["far"] = np.maximum(h1, h2all)
    # the garden ground: a far bank of moss, the stream across the lower middle, the near bank under it
    field = np.clip((c.yy - (hz + 0.06 * H)) / 1.2 + 0.5, 0, 1)
    yt = 0.700 * H + 0.012 * H * np.sin(xs / W * 5.0 + 1.0) + 5 * (fbm1d(W, 90, 3, 182) - 0.5)       # the far bank's edge
    yb = 0.890 * H + 0.015 * H * np.cos(xs / W * 4.0) + 5 * (fbm1d(W, 90, 3, 183) - 0.5)              # the near bank's edge
    stream = np.clip((c.yy - yt[None, :]) / 2.0 + 0.5, 0, 1) * np.clip((yb[None, :] - c.yy) / 2.0 + 0.5, 0, 1)
    bank = c.vgrad([(0, "#3A2C40"), (0.4, "#2A2034"), (1, "#140E1C")], 0.62 * H, H)
    moss = fbm(H, W, 6, 3, 176)
    bank = bank * (0.84 + 0.28 * moss)[..., None]
    bank = bank + (hexc("#3A4A3A") - bank) * (np.clip((moss - 0.55) * 3, 0, 1) * 0.3)[..., None]
    c.over(bank, field)
    M["field"] = field
    # the stepping stones: six squat stones of six different stones, on a diagonal from the near bank (left) to the
    # far bank (right). Each has a lit top that takes the dawn sky, a face toward us in shade, darker toward the
    # water, and a dark wet line and contact shadow at the water. Gaps between them keep each one its own shape.
    specs = [("basalt", "#5A5262", "#1C1822"), ("granite", "#B8ACA8", "#4A4248"), ("slate", "#6C7890", "#262C3C"),
             ("moss", "#6E7E5A", "#262C22"), ("sandstone", "#B47C64", "#4C2E26"), ("marble", "#DAD0CC", "#6C6266")]
    stones = np.zeros((H, W), np.float32)
    tops = []
    for i, (kind, topc, facec) in enumerate(specs):
        x, y = (0.44 + 0.0875 * i) * W, (0.872 - 0.031 * i) * H
        hw = [100, 90, 80, 72, 62, 52][i]
        ht = hw * 0.55
        ry = hw * 0.30
        top_y = y - ht
        # an irregular top outline (a basalt column is hexagonal), the sides falling to an uneven waterline
        rng_s = np.random.default_rng(300 + i)
        if kind == "basalt":
            ang = np.linspace(0, 2 * math.pi, 7)[:-1] + 0.3
            jit = np.ones(6)
        else:
            ang = np.linspace(0, 2 * math.pi, 29)[:-1]
            jit = 1 + 0.10 * np.convolve(np.r_[rng_s.normal(0, 1, 28), rng_s.normal(0, 1, 28)[:4]], np.ones(5) / 5, "same")[:28]
        tp = [(x + hw * 0.94 * j * math.cos(a), top_y + ry * j * math.sin(a)) for a, j in zip(ang, jit)]
        lx_, rx_ = min(p[0] for p in tp), max(p[0] for p in tp)
        side = c.poly([(lx_, top_y), (rx_, top_y), (rx_ + hw * 0.04, y - ht * 0.45), (rx_ - hw * 0.08, y + rng_s.normal(0, 2)),
                       (lx_ + hw * 0.10, y + rng_s.normal(0, 2)), (lx_ - hw * 0.03, y - ht * 0.4)], 0.8)
        body = np.maximum(c.poly(tp, 0.8), side)
        body = np.maximum(body, c.ellipse(x, y - 1, (rx_ - lx_) * 0.45, ry * 0.40, 0.8))
        topm = c.poly([(x + (px - x) * 0.94, top_y + (py - top_y) * 0.9) for px, py in tp], 1.5) * body
        tex_ = fbm(H, W, 3 if kind in ("granite", "sandstone") else 8, 3, 177 + i)
        face = np.stack([np.full((H, W), v, np.float32) for v in hexc(facec)], -1) * (1 - 0.35 * np.clip((c.yy - top_y) / max(1.0, y - top_y), 0, 1))[..., None]
        top_c = np.stack([np.full((H, W), v, np.float32) for v in hexc(topc)], -1)
        col = face + (top_c - face) * topm[..., None]
        if kind == "granite":
            col = col * (1 - 0.35 * (tex_ > 0.64))[..., None]
        elif kind == "slate":
            col = col * (0.9 + 0.12 * (np.sin(c.yy / 2.2) > 0))[..., None]
        elif kind == "sandstone":
            col = col * (0.88 + 0.16 * np.sin(c.yy / 3.5 + tex_ * 4))[..., None]
        elif kind == "moss":
            col = col + (hexc("#86A06A") - col) * (topm * np.clip((tex_ - 0.45) * 3, 0, 1) * 0.6)[..., None]
        elif kind == "marble":
            col = col * (1 - 0.12 * np.exp(-((np.sin(c.xx / 11 + tex_ * 9)) / 0.08) ** 2))[..., None]
        else:
            col = col * (0.92 + 0.12 * tex_)[..., None]
        c.over(col, body)
        wet = np.clip(c.ellipse(x, y - 1, hw * 0.92, ry * 0.5, 1.0) - c.ellipse(x, y - 5, hw * 0.92, ry * 0.5, 1.0), 0, 1) * body
        c.mul(hexc("#0E0A14"), wet * 0.6)
        stones = np.maximum(stones, body)
        tops.append((topm, x, y, hw, top_y))
    # the stone lantern (a kasuga-doro) on the near bank, left of the first stone: the one warm practical light
    tlx, tly = 0.37 * W, 0.955 * H
    k_ = 1.45
    T = lambda pts: [(tlx + a * k_, tly + b * k_) for a, b in pts]
    toro = np.zeros((H, W), np.float32)
    toro = np.maximum(toro, c.poly(T([(-34, 0), (34, 0), (28, -16), (-28, -16)]), 0.6))           # base
    toro = np.maximum(toro, c.poly(T([(-9, -14), (9, -14), (7, -92), (-7, -92)]), 0.6))            # post
    toro = np.maximum(toro, c.poly(T([(-30, -90), (30, -90), (24, -104), (-24, -104)]), 0.6))      # platform
    toro = np.maximum(toro, c.poly(T([(-20, -104), (20, -104), (20, -138), (-20, -138)]), 0.6))    # firebox
    toro = np.maximum(toro, c.poly(T([(-48, -132), (-40, -140), (0, -164), (40, -140), (48, -132), (0, -146)]), 0.6))  # roof
    toro = np.maximum(toro, c.ellipse(tlx, tly - 170 * k_, 7 * k_, 8 * k_, 0.6))                  # finial
    fire = c.poly(T([(-10, -108), (10, -108), (10, -132), (-10, -132)]), 0.5)
    c.mul(hexc("#0E0A14"), c.ellipse(tlx + 18, tly + 4, 56, 9, 3) * 0.6)
    c.over(c.vgrad([(0, "#3A3240"), (1, "#22202A")], tly - 200, tly) * (0.85 + 0.25 * fbm(H, W, 4, 2, 178))[..., None], toro)
    c.over(hexc("#FFC680"), fire)
    M["lantern"] = fire
    # no figure: the six stones are the subject, and each reads on its own
    figs = np.maximum(stones, toro)
    M["figs"] = figs
    # the stream: the dawn sky mirrored, the stones and the lantern mirrored about their own waterlines, almost still
    src = c.px.copy()
    solid = np.maximum.reduce([M["far"], keep, figs, field * (c.yy < yt[None, :])])     # the far bank mirrors as its dark moss
    src = sky_only * (1 - solid[..., None]) + src * solid[..., None]
    objs = [(stones * (np.abs(c.xx - x) < hw + 4) * (c.yy < y + 1), y) for (_t, x, y, hw, _ty) in tops]
    sm_ = stream * (1 - figs)
    reflect(c, sm_, src, 0.700 * H, objs, 179, amp=(0.6, 5.0), fres=(0.75, 0.50), deep="#140E20", dark=0.85, smear=2)
    c.add(hexc("#F4C0A8"), np.clip(rims(stream, 0, -1, 1), 0, 1) * 0.10)
    # the near bank: a low rise of moss in the bottom corners
    crest = yb
    rm = c.below_curve(crest, 1.5)
    c.over(c.vgrad([(0, "#22182C"), (1, "#0E0A14")], crest.min(), H), rm * (1 - toro))
    M["ridge"] = rm * (1 - toro)
    mist = np.exp(-((c.yy - (hz + 0.07 * H)) / 22) ** 2) * fbm(H, W, 120, 3, 181)
    c.add(hexc("#C890A0"), mist * 0.22)
    finish(c, np.maximum.reduce([figs, keep, pines, M["moon"]]), 771)
    # after the pass: the dawn on the stones' tops and left rims, the lantern's glow and its warmth on what faces it
    for (topm, x, y, hw, _ty) in tops:
        c.add(hexc("#F4B8A0"), topm * 0.10)
    c.add(hexc("#FFC8A8"), rims(stones, -1, -1, 2) * 0.45)
    c.add(hexc("#FFB060"), blur(fire, 10) * 1.2 * (1 - fire))
    c.over(hexc("#FFD9A0"), fire)
    to_l = np.exp(-((c.xx - tlx) ** 2 + ((c.yy - (tly - 174)) * 1.0) ** 2) / (2 * 240 ** 2))
    c.add(hexc("#FFB062"), rims(stones, -1, 0, 3) * to_l * 0.9)
    pool = np.exp(-(np.sqrt((c.xx - tlx) ** 2 + ((c.yy - tly) * 3.0) ** 2) / 120) ** 2) * (1 - figs)
    c.add(hexc("#FFB466"), pool * 0.25 * (c.yy > tly - 30))
    seal_far(M, ["far_a", "far_b"])
    vignette(c)
    return c, M


# ======================================================================================================
# 1.18.0 Runs you can trust: a ferry on a guide rope, crossing a misty river under a veiled moon
# ======================================================================================================
def runs():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.535 * H
    c.px = c.vgrad([(0, "#070B1C"), (0.45, "#111A36"), (0.85, "#24304E"), (1.0, "#334060")], 0, hz)
    c.px[int(hz):] = hexc("#334060")
    mx, my, mr = 0.78 * W, 0.17 * H, 38.0
    sky_only0 = c.px.copy()

    def sky_lum(y):
        return 0.03 + 0.22 * min(1.0, max(0.0, y / hz)) ** 2

    stars(c, 140, 181, hz * 0.8, sky_lum, near_moon=(mx, my, mr * 3), warm=0.06)
    # a waning gibbous: the sun is far below the horizon to the lower left
    moon_full(c, M, mx, my, mr, -0.55, 0.83)
    # a thin veil of altostratus: a soft aureole round the moon, the veil lit faintly where it is thin near it
    veil = tex_sample(fbm(1024, 1024, 120, 5, 182), c.xx / 2.4, c.yy / 1.2)          # large enough never to wrap
    vd = np.clip((veil - 0.40) * 1.6, 0, 1) * smooth(0.0, 0.15 * H, c.yy) * (1 - smooth(0.42 * H, hz, c.yy))
    c.add(hexc("#C9D2EC"), np.exp(-(c.radial(mx, my, mr * 3.2)) ** 2 * 1.4) * 0.22)
    vlit = np.exp(-(c.radial(mx, my, 0.35 * W, 0.45 * H)) ** 2 * 1.5)
    vcol = hexc("#2A3456") * (1 - vlit[..., None]) + hexc("#8A96BA") * vlit[..., None]
    c.over(vcol, vd * 0.45)
    M["clouds"] = vd * 0.45                     # a thin veil, as painted: no cloud shapes to cut or engrave
    M["under"] = vd * vlit * 0.45
    sky_only = sky_only0 * (1 - vd[..., None] * 0.45) + vcol * (vd[..., None] * 0.45)
    xs = np.arange(W, dtype=np.float32)
    # the far hills, misty, and the far bank's trees, darker; both under the sky's luminance
    r1 = hz - 0.03 * H - 0.07 * H * fbm1d(W, 500, 5, 183) ** 1.3
    h1 = c.below_curve(r1, 1.2) * (c.yy < hz + 0.04 * H)
    c.over(cap_to_sky(c, c.vgrad([(0, "#2C3858"), (1, "#334064")], r1.min(), hz), h1, sky_only, 0.92), h1)
    rng = np.random.default_rng(184)
    r2 = hz + 0.045 * H - 6 * fbm1d(W, 50, 3, 185)
    for _ in range(46):   # rounded willows and alders along the far bank
        x = rng.random() * W
        rr = 22 + 40 * rng.random()
        r2 = np.minimum(r2, hz + 0.045 * H - (0.03 * H + 30 * rng.random()) * np.sqrt(np.clip(1 - ((xs - x) / rr) ** 2, 0, 1)))
    h2 = c.below_curve(r2, 1.2) * (c.yy < hz + 0.05 * H)
    c.over(cap_to_sky(c, c.vgrad([(0, "#1A2238"), (1, "#141A2C")], r2.min(), hz + 0.05 * H), h2, sky_only, 0.70), h2)
    c.add(hexc("#9AA8CC"), rims(h2, 1, -1, 2) * 0.22)
    M["far_a"] = np.clip(h1 - h2, 0, 1)
    M["far_b"] = h2
    M["far"] = np.maximum(h1, h2)
    # the river
    wl = hz + 0.05 * H
    field = np.clip((c.yy - wl) / 1.2 + 0.5, 0, 1)
    M["field"] = field
    # the guide rope: from a post on the far bank (left) to a post on the near bank (right), sagging a little
    fpx, fpy_b, fpy_t = 0.115 * W, wl + 2, wl - 0.13 * H
    npx, npy_b, npy_t = 0.905 * W, 0.93 * H, 0.43 * H
    t = np.linspace(0, 1, 1600)
    rx_ = fpx + (npx - fpx) * t
    ry_ = fpy_t + (npy_t - fpy_t) * t + 0.035 * H * 4 * t * (1 - t)
    rope = np.zeros((H, W), np.float32)
    rope[np.clip(ry_.astype(int), 0, H - 1), np.clip(rx_.astype(int), 0, W - 1)] = 1
    rope[np.clip(ry_.astype(int) + 1, 0, H - 1), np.clip(rx_.astype(int), 0, W - 1)] = 1
    rope = np.clip(blur(rope, 0.7) * 1.6, 0, 1)
    posts = np.maximum(c.poly([(fpx - 3, fpy_b), (fpx + 3, fpy_b), (fpx + 2.5, fpy_t - 4), (fpx - 2.5, fpy_t - 4)], 0.5),
                       c.poly([(npx - 11, npy_b), (npx + 11, npy_b), (npx + 9, npy_t - 14), (npx - 9, npy_t - 14)], 0.6))

    def rope_y(x):
        tt = (x - fpx) / (npx - fpx)
        return fpy_t + (npy_t - fpy_t) * tt + 0.035 * H * 4 * tt * (1 - tt)

    # the ferry: a flat barge hung from a traveller block on the rope by a bridle; the ferryman at the stern with a
    # steering pole, a chocobo amidships, the lantern on a short pole at the bow
    fwl = 0.735 * H
    fx0, fx1 = 0.448 * W, 0.690 * W
    hull = c.poly([(fx0 - 16, fwl - 0.036 * H), (fx1 + 16, fwl - 0.036 * H), (fx1, fwl), (fx0, fwl)], 0.6)
    rails = np.zeros((H, W), np.float32)
    for x in np.linspace(fx0 + 6, fx1 - 6, 9):
        rails = np.maximum(rails, c.poly([(x - 2, fwl - 0.036 * H), (x + 2, fwl - 0.036 * H), (x + 2, fwl - 0.068 * H), (x - 2, fwl - 0.068 * H)], 0.4))
    rails = np.maximum(rails, c.poly([(fx0, fwl - 0.068 * H), (fx1, fwl - 0.068 * H), (fx1, fwl - 0.062 * H), (fx0, fwl - 0.062 * H)], 0.4))
    deck_y = fwl - 0.036 * H
    # the ferryman, at the stern, both hands on a long pole into the water behind
    fmx = fx0 + 0.022 * W
    man = figure(c, fmx, deck_y, 0.98, facing=1)
    pole = band(c, [(fmx + 6, deck_y - 70), (fmx - 0.07 * W, fwl + 0.04 * H)], 5, 4, 0.5)
    # the chocobo amidships, facing the near bank
    cx2, cy2 = 0.572 * W, deck_y
    s = 0.95
    cyb = cy2 - 16 * s                                                           # long legs: the body stands higher
    choco = np.zeros((H, W), np.float32)
    choco = np.maximum(choco, c.ellipse(cx2, cyb - 66 * s, 44 * s, 31 * s, 0.8))
    choco = np.maximum(choco, c.ellipse(cx2 + 18 * s, cyb - 80 * s, 25 * s, 23 * s, 0.8))
    choco = np.maximum(choco, c.poly([(cx2 + 14 * s, cyb - 92 * s), (cx2 + 36 * s, cyb - 96 * s), (cx2 + 40 * s, cyb - 132 * s), (cx2 + 22 * s, cyb - 136 * s)], 0.8))
    choco = np.maximum(choco, c.ellipse(cx2 + 34 * s, cyb - 140 * s, 16 * s, 14 * s, 0.8))
    choco = np.maximum(choco, c.poly([(cx2 + 46 * s, cyb - 146 * s), (cx2 + 64 * s, cyb - 138 * s), (cx2 + 46 * s, cyb - 132 * s)], 0.6))
    for (dx_, dy_, ln) in ((-4, -150, 22), (2, -154, 20), (8, -155, 16)):
        choco = np.maximum(choco, c.poly([(cx2 + (24 + dx_) * s, cyb + (dy_ + 6) * s), (cx2 + (24 + dx_ - ln) * s, cyb + (dy_ - 4) * s), (cx2 + (28 + dx_) * s, cyb + dy_ * s)], 0.6))
    for (dy_, ln) in ((-80, 44), (-70, 50), (-60, 40)):
        choco = np.maximum(choco, c.poly([(cx2 - 38 * s, cyb + (dy_ - 6) * s), (cx2 - (38 + ln) * s, cyb + (dy_ - 22) * s), (cx2 - (38 + ln * 0.8) * s, cyb + (dy_ + 4) * s), (cx2 - 38 * s, cyb + (dy_ + 6) * s)], 0.8))
    for lx_ in (cx2 - 12 * s, cx2 + 10 * s):
        choco = np.maximum(choco, c.poly([(lx_ - 5 * s, cyb - 42 * s), (lx_ + 5 * s, cyb - 42 * s), (lx_ + 6 * s, cy2 - 30 * s), (lx_ + 3 * s, cy2), (lx_ - 3 * s, cy2), (lx_ - 1 * s, cy2 - 30 * s)], 0.6))
    # the lantern on its pole at the bow
    lpx = fx1 - 0.012 * W
    lpole = c.poly([(lpx - 3, deck_y), (lpx + 3, deck_y), (lpx + 2.5, deck_y - 0.13 * H), (lpx - 2.5, deck_y - 0.13 * H)], 0.4)
    arm = c.poly([(lpx - 2, deck_y - 0.13 * H), (lpx + 22, deck_y - 0.13 * H), (lpx + 22, deck_y - 0.124 * H), (lpx - 2, deck_y - 0.124 * H)], 0.4)
    lan_y = deck_y - 0.13 * H + 20
    lan = c.poly([(lpx + 13, lan_y - 9), (lpx + 31, lan_y - 9), (lpx + 28, lan_y + 16), (lpx + 16, lan_y + 16)], 0.5)
    # the traveller block on the rope above the ferry's middle, and its bridle down to bow and stern
    bx_ = 0.565 * W
    by_ = rope_y(bx_)
    block = c.ellipse(bx_, by_ + 6, 8, 9, 0.5)
    bridle = np.maximum(band(c, [(bx_, by_ + 12), (fx0 + 30, deck_y - 2)], 2.4, 2.4, 0.4), band(c, [(bx_, by_ + 12), (fx1 - 30, deck_y - 2)], 2.4, 2.4, 0.4))
    ferry = np.maximum.reduce([hull, rails, man, choco, lpole, arm, block])
    M["figs"] = ferry
    lines = np.maximum.reduce([rope, posts, bridle, pole])
    M["rope"] = lines
    # the near bank: reeds on the left, the landing and the near post on the right
    crest = 0.905 * H - 0.012 * H * np.sin(xs / W * 4.0) + 5 * (fbm1d(W, 120, 3, 186) - 0.5)
    rm = c.below_curve(crest, 1.5)
    src = c.px.copy()
    # paint the boat and the far post first so the water can mirror them
    c.over(hexc("#0E1220"), np.maximum(ferry, posts))
    c.over(hexc("#141A2C"), rope * 0.9)
    c.over(hexc("#141A2C"), bridle)
    c.over(hexc("#10141F"), pole)
    c.over(hexc("#FFD08A"), lan)
    solid = np.maximum.reduce([M["far"], ferry, lan, posts, pole])
    src = sky_only * (1 - solid[..., None]) + c.px * solid[..., None]
    reflect(c, field * (1 - np.maximum(ferry, lan)), src, wl, [(np.maximum(ferry, lan) * (c.yy < fwl + 1), fwl), (posts * (c.xx < 0.3 * W), fpy_b)], 187,
            amp=(1.0, 12.0), fres=(0.78, 0.40), deep="#0A1020")
    c.over(hexc("#0E1220"), ferry * (c.yy < fwl + 1))
    c.over(hexc("#10141F"), pole)
    c.over(hexc("#FFD08A"), lan)
    # river mist, moonlit, lying low on the water and thinning toward us
    mist = np.exp(-((c.yy - (wl + 0.02 * H)) / (0.035 * H)) ** 2) * (0.5 + 0.7 * fbm(H, W, 160, 3, 188))
    c.add(hexc("#8E9CC4"), mist * 0.30 * (1 - ferry))
    c.over(c.vgrad([(0, "#1A2032"), (1, "#0A0E18")], crest.min(), H) * (0.85 + 0.25 * fbm(H, W, 5, 3, 189))[..., None], rm)
    M["ridge"] = rm
    landing = c.poly([(0.80 * W, crest[int(0.80 * W)] + 6), (W + 10, crest[-1] - 4), (W + 10, crest[-1] + 26), (0.80 * W, crest[int(0.80 * W)] + 28)], 0.6)
    c.over(hexc("#161A26") * (0.85 + 0.25 * tex_sample(fbm(256, 256, 3, 2, 190), c.xx / 6.0, c.yy / 0.5))[..., None], landing)
    c.over(hexc("#0E1220"), posts * (c.xx > 0.5 * W))
    reeds = np.zeros((H, W), np.float32)
    rng = np.random.default_rng(191)
    for _ in range(120):
        bx = rng.random() ** 1.4 * 0.30 * W
        hgt = 60 + 130 * rng.random() * (1 - bx / (0.32 * W))
        lean = rng.normal(8, 14)
        y0 = crest[int(bx)] + 8
        reeds = np.maximum(reeds, c.poly([(bx - 2.2, y0), (bx + 2.2, y0), (bx + lean + 0.6, y0 - hgt), (bx + lean - 0.6, y0 - hgt)], 0.6))
    c.over(hexc("#080C16"), reeds)
    M["rope"] = np.maximum(M["rope"], reeds)                                    # reeds kept as lines in every treatment
    # soft moon shadows (the veil diffuses them): the near post's shadow toward us and right
    contact_shadow(c, npx, npy_b, 26, -60, 30, 0.45)
    finish(c, np.maximum.reduce([ferry, lines, lan, reeds, M["moon"]]), 781)
    # after the pass: the moon's broken path on the river, rims, the lantern's glow, its column on the water and
    # its warmth on the chocobo and the deck
    glitter(c, mx, wl, field * (1 - ferry), 192, col="#E6ECFF", strength=0.55, w0=6.0, spread=0.20, thr=0.66, near=0.8)
    c.add(hexc("#B8C4E6"), rims(np.maximum(ferry, posts), 1, -1, 2) * 0.45)
    c.add(hexc("#9AA8CC"), np.clip(rope - np.roll(rope, 2, 0), 0, 1) * 0.55)                     # the rope's moonlit top
    c.add(hexc("#9AA8CC"), np.clip(bridle - np.roll(bridle, 2, 0), 0, 1) * 0.55)                 # and the bridle's
    c.add(hexc("#9AA8CC"), rims(reeds, 1, -1, 2) * 0.35)
    c.add(hexc("#FFB060"), blur(lan, 9) * 1.3 * (1 - lan))
    lcx, lcy = lpx + 22, lan_y + 4
    near_l = np.exp(-((c.xx - lcx) ** 2 + (c.yy - lcy) ** 2) / (2 * 120 ** 2))
    c.add(hexc("#FFB062"), rims(np.maximum(choco, rails), 1, 0, 3) * near_l * 1.0)
    c.add(hexc("#FFB062"), hull * near_l * 0.25)
    col_ = np.exp(-((c.xx - lcx) / (6 + np.clip(c.yy - fwl, 0, None) * 0.10)) ** 2) * (c.yy > fwl + 2) * np.exp(-np.clip(c.yy - fwl, 0, None) / 90)
    brk = np.clip((tex_sample(fbm(512, 512, 5, 2, 193), c.xx / 3.0, c.yy / 0.9) - 0.42) * 4, 0, 1)
    c.add(hexc("#FFB466"), col_ * brk * field * (1 - rm) * 0.9)
    M["lantern"] = lan
    seal_far(M, ["far_a", "far_b"])
    vignette(c, cx=0.5)
    return c, M
