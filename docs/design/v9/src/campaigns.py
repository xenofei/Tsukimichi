"""Moonfall campaign key art (plan v9 G6, the owner's answer: one game, a base campaign and an expansion that opens
after it): campaign-base.png and campaign-expansion.png, 960 x 540 each (painted at 1920 x 1080), plus @2x.

One identity for both: the same sky grammar (one near-full moon high on the left, a single falling star on the
right, lighting nothing), the same lockup at the lower left (MOONFALL in spaced capitals over a gilt rule, the
campaign's name beneath), the same Medallion finish (a warm varnish grade, a vignette and the slim gilt slip lit
from the upper left), and each campaign's own bucket as its warm practical light:
  base       The Moon Road   55 levels: a pale road over moonlit hills to a crescent cradle hung as a lantern
                             at a waystation (bucket A), with one traveller on the road
  expansion  The Far Shore   60 levels: open sea at a later hour, a lantern boat (bucket B) bound for a far shore
                             with a gate on its headland
Run: py -3 campaigns.py
"""
import math
import pathlib
import sys

import numpy as np
from PIL import Image

V8SRC = pathlib.Path(__file__).resolve().parents[2] / "v8" / "art" / "src"
sys.path.insert(0, str(V8SRC))
from artlib import Canvas, blur, fbm, fbm1d, hexc, smooth  # noqa: E402
from paint_option_b2 import finish, ridge_layer, zeros, contact_shadow, figure  # noqa: E402
from paint_option_b3 import glitter, moon_full, reflect, rims, vignette  # noqa: E402
from paint_release import band, bezier, stars, tex_sample  # noqa: E402

from mf_lib import P, V9, Img, text  # noqa: E402
import paint_option_b2 as _b2  # noqa: E402
import paint_option_b3 as _b3  # noqa: E402

W, H = 1920, 1080
_b2.W, _b2.H, _b3.W, _b3.H = W, H, W, H      # the v8 helpers read the canvas size from their module


def falling_star(c, sx0, sy0, sx1, sy1):
    L = math.hypot(sx1 - sx0, sy1 - sy0)
    proj = ((c.xx - sx0) * (sx1 - sx0) + (c.yy - sy0) * (sy1 - sy0)) / (L * L)
    t = np.clip(proj, 0, 1)
    dperp = np.abs((c.xx - sx0) * (sy1 - sy0) - (c.yy - sy0) * (sx1 - sx0)) / L
    streak = np.exp(-(dperp / 1.3) ** 2) * t ** 1.8 * (proj > 0) * (proj < 1)
    hd = np.sqrt((c.xx - sx1) ** 2 + (c.yy - sy1) ** 2)
    c.add(hexc("#E8EEFF"), streak * 0.9)
    c.add(hexc("#F4F2EA"), np.exp(-(hd / 2.8) ** 2) + np.exp(-(hd / 11) ** 2) * 0.2)


def sky(c, M, top, mid, low, horizon, hz, mx, my, mr, seed, glow="#8EA6DC"):
    c.px = c.vgrad([(0, top), (0.42, mid), (0.82, low), (1.0, horizon)], 0, hz)
    c.px[int(hz):] = hexc(horizon)
    c.add(hexc(glow), np.exp(-(c.radial(mx, my, 0.42 * W, 0.62 * H)) ** 2 * 2.0) * 0.24)
    sky_only = c.px.copy()
    stars(c, 260, seed, hz * 0.92, lambda y: 0.03 + 0.24 * min(1.0, max(0.0, y / hz)) ** 2, near_moon=(mx, my, mr * 1.5), warm=0.10)
    moon_full(c, M, mx, my, mr)
    return sky_only


def lantern(c, lx, ly, s=1.0):
    lant = c.ellipse(lx, ly, 10 * s, 14 * s, 0.6)
    across = np.clip(1 - ((c.xx - lx) / (10 * s)) ** 2, 0, 1)
    paper = hexc("#C46A30") * (1 - across[..., None]) + hexc("#FFD48E") * across[..., None]
    ribs = 1 - 0.20 * np.exp(-((((c.yy - ly + 14 * s) % (5.0 * s)) - 2.5 * s) ** 2) / (0.35 * s))
    c.over(paper * ribs[..., None], lant)
    caps = np.maximum(c.poly([(lx - 7 * s, ly - 16 * s), (lx + 7 * s, ly - 16 * s), (lx + 7 * s, ly - 12 * s), (lx - 7 * s, ly - 12 * s)], 0.5),
                      c.poly([(lx - 6 * s, ly + 12 * s), (lx + 6 * s, ly + 12 * s), (lx + 6 * s, ly + 16 * s), (lx - 6 * s, ly + 16 * s)], 0.5))
    c.over(hexc("#2A1C12"), caps)
    return np.maximum(lant, caps)


def base():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.60 * H
    mx, my, mr = 0.24 * W, 0.20 * H, 46.0
    sky(c, M, "#050A1E", "#0C1736", "#1B2A56", "#2E4278", hz, mx, my, mr, 2101)
    falling_star(c, 0.73 * W, 0.08 * H, 0.775 * W, 0.24 * H)
    xs = np.arange(W, dtype=np.float32)
    for (yb, amp, top, bot, seed, haze, f) in ((hz, 0.09, "#2A3A68", "#2E3F70", 2102, 0.45, 1.5), (hz + 0.04 * H, 0.07, "#1E2B52", "#24335E", 2103, 0.35, 2.2),
                                               (hz + 0.09 * H, 0.05, "#151F3E", "#1A2648", 2104, 0.25, 3.2)):
        ridge_layer(c, M, xs, yb, amp, W / f, seed, top, bot, mx, "#5F78AE", haze=haze)
    # the near hillside: a broad slope, darker, rolling
    gtop = 0.74 * H + 0.04 * H * np.cos(xs / W * 3.4 + 0.8) + 6 * (fbm1d(W, 200, 4, 2105) - 0.5)
    field = c.below_curve(gtop, 1.4)
    c.over(c.vgrad([(0, "#1E2C4C"), (1, "#0A1222")], gtop.min(), H), field)
    c.add(hexc("#6A7EB4"), np.exp(-np.clip(c.yy - gtop[None, :], 0, None) / 12) * field * 0.14)
    M["field"] = field
    # the moon road: pale worn stone from the lower right up the hill to a waystation right of centre
    wsx, wsy = 0.70 * W, 0.71 * H
    road = bezier((0.86 * W, H + 40), (0.82 * W, 0.90 * H), (0.74 * W, 0.80 * H), (wsx - 8, wsy + 18))
    rm = band(c, road, 120, 16, 1.2) * field
    toward = np.exp(-((c.xx - wsx) / (0.08 * W + (c.yy - wsy) * 0.6)) ** 2)
    stones = tex_sample(fbm(512, 512, 6, 3, 2106), (c.xx - 0.80 * W) / np.clip((c.yy - wsy + 40) / 160, 0.3, 3) + 300, c.yy / np.clip((c.yy - wsy + 40) / 160, 0.3, 3))
    rcol = c.vgrad([(0, "#56658C"), (0.4, "#3A4664"), (1, "#222B44")], wsy, H) * (0.78 + 0.36 * stones)[..., None]
    rcol = 1 - (1 - rcol) * (1 - hexc("#D6E0FF") * (toward * 0.25)[..., None])
    c.over(rcol, rm)
    # the waystation: a post and crossbeam, and the brass crescent cradle hung from it as a lantern (bucket A)
    post = c.poly([(wsx - 5, wsy + 22), (wsx + 5, wsy + 22), (wsx + 4, wsy - 150), (wsx - 4, wsy - 150)], 0.6)
    beam = c.poly([(wsx - 6, wsy - 146), (wsx + 70, wsy - 146), (wsx + 70, wsy - 139), (wsx - 6, wsy - 139)], 0.6)
    chains = np.maximum(c.poly([(wsx + 40, wsy - 139), (wsx + 41.5, wsy - 139), (wsx + 41.5, wsy - 108), (wsx + 40, wsy - 108)], 0.4),
                        c.poly([(wsx + 66, wsy - 139), (wsx + 67.5, wsy - 139), (wsx + 67.5, wsy - 108), (wsx + 66, wsy - 108)], 0.4))
    wood = np.maximum.reduce([post, beam, chains])
    c.over(hexc("#0E1020"), wood)
    # the cradle: a brass crescent, horns up, with a small lantern resting in its hollow
    cx, cy = wsx + 54, wsy - 96
    outer = c.ellipse(cx, cy - 18, 30, 30, 0.6) * (c.yy > cy - 26)
    inner = c.ellipse(cx, cy - 30, 33, 30, 0.6)
    cres = np.clip(outer - inner, 0, 1)
    shade = np.clip(((c.xx - cx) * -0.6 + (c.yy - cy) * -0.8) / 30 + 0.5, 0, 1)
    brass = hexc("#5C4724") * (1 - shade[..., None]) + hexc("#C9A65C") * shade[..., None]
    lant = lantern(c, cx, cy - 24, 0.9)
    c.over(brass, cres)
    figm = figure(c, 0.80 * W, 0.86 * H, s=0.9, facing=-1)
    c.over(hexc("#0B0E1A"), figm)
    keep = np.maximum.reduce([wood, cres, lant, figm, M["moon"]])
    finish(c, keep, 2107, plain=M["moon"])
    c.add(hexc("#B8C6EE"), rims(np.maximum(wood, figm), -1, -1, 2) * 0.45)
    dl = np.sqrt((c.xx - cx) ** 2 + (c.yy - (cy - 24)) ** 2)
    c.add(hexc("#FFB466"), np.exp(-(dl / 26) ** 2) * 0.45 + np.exp(-(dl / 90) ** 2) * 0.10)
    fall = 1 / (1 + (dl / 90) ** 2)
    c.add(hexc("#FFB062"), rims(cres, 0, -1, 2) * 0.8 + rims(post, 1, 0, 2) * fall * 1.2)
    c.add(hexc("#FFB062"), np.exp(-(((c.xx - wsx) / 90) ** 2 + ((c.yy - (wsy + 20)) / 18) ** 2)) * field * 0.16)   # its pool on the road
    contact_shadow(c, wsx, wsy + 22, 14, -26, 10, 0.5)                                                           # the post's, away from the lantern
    contact_shadow(c, 0.80 * W, 0.86 * H + 6, 30, 14, 26, 0.5)                                                    # the traveller's, toward us
    vignette(c, cy=0.42)
    return c, M


def expansion():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.60 * H
    mx, my, mr = 0.24 * W, 0.20 * H, 46.0
    sky_only = sky(c, M, "#07071E", "#141A3E", "#2A2A5C", "#3E3A70", hz, mx, my, mr, 2201, glow="#9A9AD8")
    falling_star(c, 0.73 * W, 0.08 * H, 0.775 * W, 0.24 * H)
    xs = np.arange(W, dtype=np.float32)
    # the far shore on the right: a low headland with pines and a gate on its point, backlit, darker than the sky
    head = hz - (0.05 * H + 0.03 * H * fbm1d(W, 160, 4, 2202)) * smooth(0.55 * W, 0.70 * W, xs)
    serr = 10 * (1 - np.abs(((xs / 14.0 + 3 * fbm1d(W, 50, 2, 2203)) % 1.0) - 0.5) * 2) ** 1.6 * smooth(0.60 * W, 0.70 * W, xs)
    shore = c.below_curve(head - serr, 1.0) * (c.yy < hz + 1)
    gx, gy = 0.615 * W, head[int(0.615 * W)] + 4
    gate = np.maximum.reduce([c.poly([(gx - 30, gy), (gx - 24, gy), (gx - 24, gy - 74), (gx - 30, gy - 74)], 0.5),
                              c.poly([(gx + 24, gy), (gx + 30, gy), (gx + 30, gy - 74), (gx + 24, gy - 74)], 0.5),
                              c.poly([(gx - 46, gy - 82), (gx + 46, gy - 82), (gx + 40, gy - 74), (gx - 40, gy - 74)], 0.5),
                              c.poly([(gx - 36, gy - 62), (gx + 36, gy - 62), (gx + 36, gy - 57), (gx - 36, gy - 57)], 0.5)])
    far = np.maximum(shore, gate)
    c.over(hexc("#14152E"), far)
    c.add(hexc("#9C9AD0"), rims(far, -1, -1, 2) * 0.22)
    M["far"] = far
    sea = np.clip((c.yy - hz) / 1.2 + 0.5, 0, 1)
    # the boat, right of centre, its lantern on the stern post
    yw = 0.80 * H
    bx0, bx1 = 0.52 * W, 0.70 * W
    u = (c.xx - bx0) / (bx1 - bx0)
    sheer = yw - 30 - 22 * (np.abs(u - 0.5) * 2) ** 3
    hull = blur(((c.yy > sheer) * (c.yy < yw + 1.5) * (u > 0) * (u < 1)).astype(np.float32), 0.7)
    hcol = c.vgrad([(0, "#1C1720"), (1, "#0E0B12")], yw - 60, yw)
    c.over(hcol, hull)
    post = np.zeros((H, W), np.float32)
    for k in range(60):
        tt = k / 59
        post = np.maximum(post, c.ellipse(bx1 - 20 - 16 * tt ** 2, yw - 34 - 140 * tt, 3.4, 3.4, 0.6))
    arm = c.poly([(bx1 - 38, yw - 174), (bx1 + 18, yw - 174), (bx1 + 18, yw - 169), (bx1 - 38, yw - 169)], 0.5)
    lx, ly = bx1 + 14, yw - 142
    cord = c.poly([(lx - 0.9, yw - 170), (lx + 0.9, yw - 170), (lx + 0.9, ly - 15), (lx - 0.9, ly - 15)], 0.4)
    fx, fy = 0.60 * W, yw - 30
    Pp = lambda pts, s=1.4: [(fx + a * s, fy + b * s) for a, b in pts]
    fig = np.maximum.reduce([c.poly(Pp([(-20, 2), (22, 2), (20, -16), (12, -40), (-6, -44), (-16, -28)]), 0.7),
                             c.ellipse(fx + 6 * 1.4, fy - 52 * 1.4, 14, 16, 0.7),
                             c.poly(Pp([(-2, -58), (16, -62), (12, -50), (0, -48)]), 0.7)]) * (c.yy < yw - 4)
    wood = np.maximum.reduce([post, arm, cord])
    c.over(hexc("#120E14"), wood)
    c.over(hexc("#0B0E1A"), fig)
    lant = lantern(c, lx, ly)
    boat = np.maximum.reduce([hull, wood, fig])
    src = c.px.copy()
    solid = np.maximum.reduce([far, boat, lant])
    src = sky_only * (1 - solid[..., None]) + src * solid[..., None]
    reflect(c, sea * (1 - boat * (c.yy < yw)), src, hz, [(far, hz), (boat, yw), (lant, yw)], 2204,
            amp=(1.4, 26.0), fres=(0.86, 0.45), deep="#0B0C2A", brk=0.6)
    c.over(hcol, hull * (c.yy < yw))
    c.over(hexc("#120E14"), wood)
    c.over(hexc("#0B0E1A"), fig)
    lant = lantern(c, lx, ly)
    finish(c, np.maximum.reduce([boat, lant, far, M["moon"]]), 2205, plain=M["moon"])
    glitter(c, mx, hz, sea * (1 - boat), 2206, col="#FFF1D2", strength=0.85, w0=10.0, spread=0.30, thr=0.60)
    c.add(hexc("#B8C6EE"), rims(boat, -1, -1, 2) * 0.45 * (c.yy < yw))
    dl = np.sqrt((c.xx - lx) ** 2 + (c.yy - ly) ** 2)
    c.add(hexc("#FFB466"), np.exp(-(dl / 24) ** 2) * 0.5 + np.exp(-(dl / 80) ** 2) * 0.12)
    fall = 1 / (1 + (dl / 90) ** 2)
    c.add(hexc("#FFB062"), blur(rims(fig, 1, 0, 3), 0.8) * fall * 2.2 + rims(post, 1, 0, 2) * fall * 1.2)
    rip = tex_sample(fbm(512, 512, 5, 2, 2207), c.xx / 3.0, c.yy / 0.9)
    col_ = np.exp(-((c.xx - lx) / np.maximum(6 + (c.yy - yw) * 0.10, 1.0)) ** 2) * np.exp(-np.abs(c.yy - (2 * yw - ly)) / 80) * (c.yy > yw + 2)
    c.add(hexc("#FFB466"), col_ * np.clip((rip - 0.40) * 3, 0, 1) * sea * 0.8)
    c.mul(hexc("#05070F"), np.exp(-((c.yy - yw - 1) / 2.0) ** 2) * (u > 0.02) * (u < 0.98) * 0.6)
    vignette(c, cy=0.42)
    return c, M


def medallion_finish(px, moon):
    """The Medallion look for a tile: an amber varnish grade, a deeper vignette and the slim gilt slip lit from the
    upper left (the same numbers as option_b_themes.medallion's grade and slip; the moon keeps 90 % of its paint)."""
    h, w, _ = px.shape
    L = px @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    p = px * np.array([1.06, 1.0, 0.84], np.float32)
    p = 1 - (1 - p) * (1 - np.array([0.16, 0.10, 0.02], np.float32) * np.clip(L * 1.2, 0, 1)[..., None])
    Lv = (p @ np.array([0.2126, 0.7152, 0.0722], np.float32))[..., None]
    p = np.clip(Lv + (p - Lv) * 1.10, 0, 1)
    mz = np.clip(blur(moon, 2) * 2.5, 0, 1)[..., None]                       # the moon keeps its own paint (10 % varnish)
    p = p * (1 - mz) + (px * 0.90 + p * 0.10) * mz
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    ex = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))
    rad = np.sqrt(((xx - w / 2) / (w * 0.62)) ** 2 + ((yy - h / 2) / (h * 0.75)) ** 2)
    p = p * (1 - 0.30 * np.clip(rad - 0.55, 0, 1) ** 1.5 - 0.14 * np.clip(1 - ex / 40, 0, 1) ** 2)[..., None]
    inset, wdt = 6, 6
    slip = (ex >= inset) & (ex < inset + wdt)
    litside = np.stack([yy, xx, h - 1 - yy, w - 1 - xx]).argmin(0) < 2
    across = np.clip((ex - inset) / wdt, 0, 1)[..., None]
    hi = hexc("#F0D9A0") * (1 - across * 0.35) + hexc("#C9A65C") * (across * 0.35)
    lo = hexc("#7C6236") * (1 - across * 0.4) + hexc("#5C4724") * (across * 0.4)
    p = np.where(slip[..., None], np.where(litside[..., None], hi, lo), p)
    p = np.where(((ex >= inset + wdt) & (ex < inset + wdt + 2))[..., None], p * 0.55, p)
    p = np.where((ex < inset)[..., None], p * 0.6 + hexc("#1E2236") * 0.4, p)
    return p


def lockup(img, name, sub):
    """MOONFALL in spaced capitals over a gilt rule, the campaign beneath: the same lockup on both tiles."""
    from mf_lib import draw_brass, sd_rrect
    sl, xx, yy = img.full()
    img.mul(sl, hexc("#03050C"), np.exp(-(((xx - 220) / 330) ** 2 + ((yy - 448) / 70) ** 2)) * 0.55)
    text(img, 64, 438, "MOONFALL", "serif", 54, P["cream"], anchor="ls", halo=0.5, tracking=9)
    rule = lambda X, Y: sd_rrect(X, Y, 66, 452, 400, 454, 1)
    draw_brass(img, rule, (233, 453, 170), "round", depth=0.8, width=1.0)
    text(img, 66, 480, name, "serif_i", 22, P["gilt_high"], anchor="ls", halo=0.5)
    text(img, 66, 502, sub, "ui", 13, P["ink_dim"], anchor="ls", halo=0.5)


def build(kind):
    c, M = base() if kind == "base" else expansion()
    px = medallion_finish(np.clip(c.px, 0, 1), M["moon"])
    img = Img(W, H, 2.0, px.astype(np.float32))
    if kind == "base":
        lockup(img, "The Moon Road", "The base campaign · 55 levels")
    else:
        lockup(img, "The Far Shore", "The expansion · 60 levels · opens after The Moon Road")
    img.save(V9 / f"campaign-{kind}@2x.png")
    img.save(V9 / f"campaign-{kind}.png", (960, 540))


if __name__ == "__main__":
    for k in ("base", "expansion"):
        build(k)
        print(k)
