"""Moonfall's runtime sprites (plan v9 G8, stage 2): the approved art, baked into the atlas the plugin draws the board with.

Writes Tsukimichi/assets/moonfall/:
  atlas.png, atlas@2x.png   every sprite at 1x (1 px per engine unit) and 2x, packed on the same layout (2x is 1x doubled)
  atlas.json                the manifest: each sprite's rect in 1x px (= engine units) and its anchor in units
  sky.png                   the night sky behind the board opening, 1x only (it is soft; the 2x tier scales it)

Every sprite is painted by the approved recipes in mf_lib.py and playfield.py (the one light, the moon pegs, the
moonstone, the brass, the ball), on a transparent canvas: an RGBA canvas that composites 'over' exactly, keeps 'add'
(screen) as light on what is drawn and as a glow layer where nothing is, and keeps 'mul' on what is drawn.

What the plugin draws procedurally instead (spec-moonfall.md, "Runtime assets needed"): the launcher's tube (shaded in
screen space, so its highlight stays upper left at every angle), the bricks' shading (the strip below carries only the
stone; the light comes from each vertex's normal), the enamel's gradient and the board's contact shade.

Run: py -3 runtime_atlas.py   (only numpy and Pillow)
"""
import json
import math
import pathlib

import numpy as np
from PIL import Image

from mf_lib import (H_BLINN, L, P, PEG, Img, blur, draw_ball, draw_brass, fbm, hexc, normals_from_height, ramp, sd_circle,
                    sd_rrect, sd_segment, seas_field, smooth)
from playfield import SKY_AT_PEGS, cradle_geometry, sky

ROOT = pathlib.Path(__file__).resolve().parents[4]
OUT = ROOT / "Tsukimichi" / "assets" / "moonfall"
KINDS = ["blue", "orange", "green", "purple"]
TURNS = [-0.4, 0.0, 0.4]          # the baked turns of each sea layout (the light never turns with them)
LAYOUTS = 4
ATLAS_WIDTH = 1024
PAD = 2                           # 1x px of replicated edge round every sprite (4 at 2x)


# ------------------------------------------------------------------------------------------------ the RGBA canvas
class Rgba(Img):
    """A premultiplied RGBA canvas with mf_lib's Img interface, so the approved draw calls paint sprites unchanged."""

    def __init__(self, w, h, S):
        super().__init__(w, h, S)
        self.a = np.zeros((self.h, self.w), np.float32)

    def over(self, sl, col, a):
        a = np.clip(a, 0, 1)
        col = np.asarray(col, np.float32)
        self.px[sl] = self.px[sl] * (1 - a[..., None]) + col * a[..., None]
        self.a[sl] = self.a[sl] * (1 - a) + a

    def add(self, sl, col, amt):
        # screen on what is drawn; where nothing is, a glow layer of the light's colour (the board under it is dark)
        amt = np.clip(amt, 0, 1)
        light = np.clip(np.asarray(col, np.float32) * amt[..., None], 0, 1)
        self.px[sl] = self.px[sl] + light * (1 - self.px[sl])
        self.a[sl] = self.a[sl] + amt * (1 - self.a[sl])

    def mul(self, sl, col, amt):
        a = np.clip(amt, 0, 1)[..., None]
        self.px[sl] = self.px[sl] * (1 - a + a * np.asarray(col, np.float32))

    def shadow(self, sl, amt, col="#05070F"):
        """A darkening of whatever is under the sprite (a contact shadow): the colour 'over' at that alpha."""
        self.over(sl, hexc(col), amt)

    def rgba(self):
        a = self.a[..., None]
        rgb = np.where(a > 1e-5, self.px / np.maximum(a, 1e-5), 0)
        out = np.concatenate([np.clip(rgb, 0, 1), np.clip(a, 0, 1)], -1)
        return bleed(out)


def bleed(rgba, rounds=6):
    """Fills the colour of (nearly) transparent pixels from their neighbours, so bilinear sampling never pulls black in."""
    rgb, a = rgba[..., :3].copy(), rgba[..., 3]
    known = a > 0.02
    for _ in range(rounds):
        if known.all():
            break
        acc = np.zeros_like(rgb)
        cnt = np.zeros(a.shape, np.float32)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1)):
            k = np.roll(np.roll(known, dy, 0), dx, 1)
            c = np.roll(np.roll(rgb, dy, 0), dx, 1)
            acc += c * k[..., None]
            cnt += k
        fill = (~known) & (cnt > 0)
        rgb[fill] = acc[fill] / cnt[fill][:, None]
        known = known | fill
    return np.concatenate([rgb, a[..., None]], -1)


def canvas(w, h, S):
    return Rgba(round(w * S), round(h * S), S)


# ------------------------------------------------------------------------------------------------ pegs
def moon_face(img, x, y, r, kind, state, variant, rot):
    """mf_lib.draw_moon without its halo (the halo is a sprite of its own, tinted by kind): the lit face, the soft
    terminator, the earthshine of the unlit part and the seas, exactly as approved."""
    w = img.win(x, y, r * 1.2)
    sl, xx, yy = w
    S = img.S
    u, v = (xx - x) / r, (yy - y) / r
    d2 = u * u + v * v
    d = np.sqrt(d2)
    cov = np.clip((1 - d) * r * S + 0.5, 0, 1)
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    mu0 = u * L[0] + v * L[1] + nz * L[2]
    ls = np.minimum(np.where(mu0 > 0, mu0 / (mu0 + nz + 1e-4), 0.0) / 0.5, 1.12)
    lit_t = blur(smooth(-0.05, 0.08, mu0), 0.11 * r * S)
    shade = 0.80 * ls + 0.20 * np.clip(mu0, 0, 1)
    k = PEG[kind]
    alb, sea, glow = hexc(k["albedo"]), hexc(k["sea"]), hexc(k["glow"])
    s = seas_field(u, v, variant, rot)
    col = alb[None, None, :] * (1 - s[..., None] * 0.62) + sea[None, None, :] * (s[..., None] * 0.62)
    earth = hexc(SKY_AT_PEGS) * 1.45 + alb * 0.13
    if state == "lit":
        col = col * 0.80 + np.array([1, 1, 1], np.float32) * 0.10 + glow * 0.12
        lit_t = blur(smooth(-0.20, -0.06, mu0), 0.08 * r * S)
        shade = np.maximum(shade * 1.10 + 0.05, 0.55)
        earth = earth * 0.6 + glow * 0.26
    face = col * shade[..., None]
    face = face * lit_t[..., None] + earth * (1 - lit_t[..., None])
    img.over(sl, face, cov)


def peg_sprite(S, kind, state, layout, turn):
    img = canvas(24, 24, S)
    moon_face(img, 12, 12, 10.0, kind, state, layout, TURNS[turn])
    return img, (12, 12)


def radial_sprite(S, units, fn):
    """A white sprite whose alpha is fn(d) of the distance d from its centre, in units; it fades to nothing at its edge."""
    img = canvas(units, units, S)
    sl, xx, yy = img.full()
    c = units / 2
    d = np.sqrt((xx - c) ** 2 + (yy - c) ** 2)
    a = fn(d) * smooth(c, c - 1.5, d)
    img.over(sl, hexc("#FFFFFF"), a)
    return img, (c, c)


def halo_sprite(S):
    # the lit halo of draw_moon, in peg radii (r 10): a bloom to about 2 r; tinted by the kind's glow colour
    return radial_sprite(S, 52, lambda d: np.exp(-np.clip(d / 10 - 0.92, 0, None) ** 2 * 3.2) * 0.50
                         + np.exp(-np.clip(d / 10 - 1, 0, None) * 2.6) * 0.14)


def bloom_sprite(S):
    # the clearing's 0-60 ms bloom round the limb (draw_moon's flash term, at flash 1)
    return radial_sprite(S, 52, lambda d: np.exp(-np.clip(d / 10 - 1, 0, None) ** 2 * 2.2) * 0.55)


def soft_sprite(S):
    # a unit gaussian, exp(-d^2) with d in quarters of the sprite: the lantern's air glow, the notch bloom, the cup glow
    return radial_sprite(S, 24, lambda d: np.exp(-(d / 4.0) ** 2))


def speck_sprite(S):
    # a speck of moondust: draw_motes' exp(-0.9 (d / mr)^2), drawn at mr = 1 unit and scaled to each speck
    return radial_sprite(S, 6, lambda d: np.exp(-0.9 * d * d))


def disc_sprite(S):
    img = canvas(24, 24, S)
    sl, xx, yy = img.full()
    img.over(sl, hexc("#FFFFFF"), img.cov(sd_circle(xx, yy, 12, 12, 10)))
    return img, (12, 12)


def sliver_sprite(S):
    """Plain's flat peg: the unlit part of the gibbous moon (the same soft terminator), white, for a darker tint."""
    img = canvas(24, 24, S)
    sl, xx, yy = img.full()
    u, v = (xx - 12) / 10, (yy - 12) / 10
    d2 = u * u + v * v
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    mu0 = u * L[0] + v * L[1] + nz * L[2]
    lit_t = smooth(-0.05, 0.08, mu0)
    img.over(sl, hexc("#FFFFFF"), img.cov(sd_circle(xx, yy, 12, 12, 10)) * (1 - lit_t))
    return img, (12, 12)


def ring_sprite(S):
    """Plain's lit halo: a flat ring just outside the limb."""
    img = canvas(30, 30, S)
    sl, xx, yy = img.full()
    img.over(sl, hexc("#FFFFFF"), img.cov(np.abs(sd_circle(xx, yy, 15, 15, 12.4)) - 0.9))
    return img, (15, 15)


def dot_sprite(S):
    """The aim guide's silver dot (playfield.guide_dots): a moonstone core of r 1.6 with a faint glow."""
    img = canvas(8, 8, S)
    sl, xx, yy = img.full()
    d2 = (xx - 4) ** 2 + (yy - 4) ** 2
    img.add(sl, hexc("#C3CEE4"), np.exp(-d2 / 6) * 0.35 * smooth(4, 2.5, np.sqrt(d2)))
    img.over(sl, hexc("#E2E8F4"), np.clip(0.5 - (np.sqrt(d2) - 1.6) * img.S, 0, 1))
    return img, (4, 4)


def ball_sprite(S):
    img = canvas(14, 14, S)
    draw_ball(img, 7, 7, 6.0)
    return img, (7, 7)


# ------------------------------------------------------------------------------------------------ bricks
def brick_sprite(S, kind, state):
    """A moonstone slab, 30 x 12 with its rounded ends, lying level (mf_lib.draw_brick without its halo). The plugin
    lays it along each brick's line or arc: its two ends as caps and its middle repeated, mirrored, to any length.
    Interim art: the light is baked for a level slab, so it turns with a steep brick."""
    img = canvas(30, 12, S)
    sl, xx, yy = img.full()
    sd = sd_rrect(xx, yy, 0, 0, 30, 12, 4.5)
    cov = img.cov(sd)
    hgt = np.sqrt(np.clip(-sd / 4.5, 0, 1)) * 4.5
    nx, ny, nz = normals_from_height(hgt, S)
    lam = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    k = PEG[kind]
    alb, sea, glow = hexc(k["albedo"]), hexc(k["sea"]), hexc(k["glow"])
    m = fbm(sd.shape[0], sd.shape[1], 9 * S, 3, 70 + KINDS.index(kind))
    s = np.clip((m - 0.45) * 2.4, 0, 1) * 0.55
    col = alb * (1 - s[..., None]) + sea * s[..., None]
    shade = 0.10 + 1.0 * lam ** 1.4
    if state == "lit":
        col = col * 0.72 + 0.18 + glow * 0.16
        shade = shade * 1.08 + 0.08
    nh = np.clip(nx * H_BLINN[0] + ny * H_BLINN[1] + nz * H_BLINN[2], 0, 1)
    face = col * shade[..., None] + (nh ** 50 * 0.35)[..., None]
    img.over(sl, np.clip(face, 0, 1), cov)
    return img, (0, 6)


def brick_flat_sprite(S):
    img = canvas(30, 12, S)
    sl, xx, yy = img.full()
    img.over(sl, hexc("#FFFFFF"), np.clip(0.5 - sd_rrect(xx, yy, 0, 0, 30, 12, 4.5) * S, 0, 1))
    return img, (0, 6)


# ------------------------------------------------------------------------------------------------ the launcher
PX, PY = 400.0, 87.0


def at(S, x0, y0, w, h):
    """An RGBA canvas covering the playfield rect (x0, y0, w, h): drawing calls take playfield coordinates."""
    img = OffsetRgba(round(w * S), round(h * S), S, x0, y0)
    return img


class OffsetRgba(Rgba):
    def __init__(self, w, h, S, x0, y0):
        super().__init__(w, h, S)
        self.x0, self.y0 = x0, y0

    def win(self, x, y, rad):
        r = super().win(x - self.x0, y - self.y0, rad)
        if r is None:
            return None
        sl, xx, yy = r
        return sl, xx + self.x0, yy + self.y0

    def full(self):
        sl, xx, yy = super().full()
        return sl, xx + self.x0, yy + self.y0


def yoke_sprite(S):
    img = at(S, 374, 34, 52, 52)
    for sx in (-1, 1):
        arm = lambda X, Y, sx=sx: sd_segment(X, Y, PX + sx * 15, 40, PX + sx * 13, PY - 4)[0] - 2.2
        draw_brass(img, arm, (PX + sx * 14, 62, 28), "round", depth=2.0, width=2.2)
    foot = lambda X, Y: sd_rrect(X, Y, PX - 21, 37.5, PX + 21, 43, 2.5)
    draw_brass(img, foot, (PX, 40, 24), "round", depth=1.8, width=2.6)
    return img, (PX - 374, PY - 34)


GAUGE_A0, GAUGE_A1, GAUGE_R = 200.0, 340.0, 25.5


def gauge_sprite(S, fill):
    """The free-ball gauge above the hub (playfield.launcher): fill False gives the empty glass channel with its brass
    rim and the three notches; fill True gives only the moonlight that fills it, which the plugin draws up to the
    shot's progress as a fan round the pivot."""
    img = at(S, 366, 54, 68, 32)
    sl, xx, yy = img.win(PX, PY, 34)
    ang = np.degrees(np.arctan2(yy - PY, xx - PX)) % 360
    rr = np.sqrt((xx - PX) ** 2 + (yy - PY) ** 2)
    a0, a1 = GAUGE_A0, GAUGE_A1
    inarc = smooth(a0 - 1, a0 + 1, ang) * smooth(a1 + 1, a1 - 1, ang)
    chan = np.clip(0.5 - (np.abs(rr - GAUGE_R) - 3.0) * img.S, 0, 1) * inarc
    if fill:
        fillm = chan * np.clip(0.5 - (np.abs(rr - GAUGE_R) - 2.2) * img.S, 0, 1)
        fc = ramp(np.clip((ang - a0) / (a1 - a0), 0, 1), [(0, "#7FA0E0"), (1, "#E2E8F4")])
        img.over(sl, fc, fillm * 0.95)
        img.add(sl, hexc("#C3CEE4"), fillm * np.exp(-((rr - GAUGE_R) / 1.2) ** 2) * 0.25)
        return img, (PX - 366, PY - 54)
    img.over(sl, hexc("#0A0F22"), chan)
    gl = chan * np.exp(-((rr - 24.0) / 0.7) ** 2) * smooth(270, 210, ang)
    img.add(sl, hexc("#F4F2EA"), gl * 0.35)
    gx_, gy_ = PX + math.cos(math.radians(222)) * 24.2, PY + math.sin(math.radians(222)) * 24.2
    img.add(sl, hexc("#FFFFFF"), np.exp(-((xx - gx_) ** 2 + (yy - gy_) ** 2) / 5.0) * chan * 0.95)

    def inarc_fn(X, Y):
        A = np.degrees(np.arctan2(Y - PY, X - PX)) % 360
        return np.minimum(A - (a0 - 2), (a1 + 2) - A)
    rimc = lambda X, Y: np.abs(np.sqrt((X - PX) ** 2 + (Y - PY) ** 2) - GAUGE_R) - 3.0
    ring = lambda X, Y: np.maximum(np.abs(rimc(X, Y)) - 0.9, -inarc_fn(X, Y))
    draw_brass(img, ring, (PX, PY, 34), "round", depth=1.0, width=0.9)
    for f in (0.2, 0.6, 1.0):
        a = math.radians(a0 + (a1 - a0) * f)
        nx_, ny_ = PX + math.cos(a) * GAUGE_R, PY + math.sin(a) * GAUGE_R
        tick = lambda X, Y, a=a, nx_=nx_, ny_=ny_: sd_segment(X, Y, nx_ - math.cos(a) * 3.6, ny_ - math.sin(a) * 3.6,
                                                             nx_ + math.cos(a) * 3.6, ny_ + math.sin(a) * 3.6)[0] - 0.8
        draw_brass(img, tick, (nx_, ny_, 6), "round", depth=0.8, width=0.8)
    return img, (PX - 366, PY - 54)


def tube_sprite(S):
    """The telescope pointing straight down from the pivot (playfield.launcher at aim 0): the tapering tube, its two
    bands, the hood and the dark mouth. The plugin turns it about the pivot to the aim. Interim art: its highlight
    turns with it (the spec asks for screen-space shading, which the richer art will bake per angle)."""
    img = at(S, PX - 10, PY, 20, 74)
    from playfield import cylinder
    cylinder(img, PX, PY, 0.0, 1.0, 8, 62, 7.4, 6.0)
    for (s0, s1, r0) in ((22, 25.5, 8.2), (48, 51, 7.4), (61, 70, 7.9)):
        cylinder(img, PX, PY, 0.0, 1.0, s0, s1, r0, r0, base=0.05)
    sl2, X2, Y2 = img.win(PX, PY + 70, 9)
    mouth = np.clip(0.5 - (np.sqrt((X2 - PX) ** 2 / 6.2 ** 2 + (Y2 - PY - 70) ** 2 / 1.6 ** 2) - 1) * 4 * img.S, 0, 1)
    img.over(sl2, hexc("#07090F"), mouth)
    return img, (10, 0)


def hub_sprite(S):
    img = at(S, PX - 16, PY - 16, 32, 32)
    hub = lambda X, Y: sd_circle(X, Y, PX, PY, 14)
    draw_brass(img, hub, (PX, PY, 16), "bevel", depth=3.0, width=4.0)
    sl3, X3, Y3 = img.win(PX, PY, 7)
    u, v = (X3 - PX) / 5.5, (Y3 - PY) / 5.5
    d2 = u * u + v * v
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    lam = np.clip(u * L[0] + v * L[1] + nz * L[2], 0, 1)
    h = np.clip((u * L[0] + v * L[1] + nz * L[2] + nz) / np.sqrt(2 * (1 + L[2])), 0, 1)
    cab = ramp(lam, [(0, P["moonstone_deep"]), (0.6, P["moonstone_mid"]), (1, P["moonstone_high"])]) \
        + hexc(P["moonstone_spec"]) * (h ** 80 * 0.9)[..., None]
    img.over(sl3, np.clip(cab, 0, 1), np.clip((1 - np.sqrt(d2)) * 5.5 * img.S + 0.5, 0, 1))
    return img, (16, 16)


# ------------------------------------------------------------------------------------------------ bucket A
def cradle_sprite(S):
    """Proposal A at x 400 (the plugin moves it): the crescent, its wheels, its moonstone inlay and its contact shadow on
    the rail (the rail itself is a slice of its own, stretched across the board)."""
    bx = 400.0
    img = at(S, bx - 72, 562, 144, 32)
    (ox, oy, R1), (ix, iy, R2), ty = cradle_geometry(bx)
    sl, xx, yy = img.win(bx, 590, 70)
    img.shadow(sl, np.exp(-((xx - bx) / 52) ** 2) * np.exp(-((yy - 589.5) / 1.6) ** 2) * 0.55)
    for wx in (bx - 22, bx + 22):
        wh = lambda X, Y, wx=wx: sd_circle(X, Y, wx, 587.5, 3.2)
        draw_brass(img, wh, (wx, 587.5, 5), "bevel", depth=1.5, width=1.4, base=-0.08)

    def cres(X, Y):
        a = sd_circle(X, Y, ox, oy, R1)
        b = -sd_circle(X, Y, ix, iy, R2)
        return np.maximum(np.maximum(a, b), ty - Y)
    draw_brass(img, cres, (bx, 580, 72), "round", depth=6.0, width=4.0)
    slc, Xc, Yc = img.win(bx, 580, 72)
    img.mul(slc, hexc("#2A2010"), img.cov(cres(Xc, Yc)) * np.clip((Xc - bx + 20) / 80, 0, 1) * 0.35)
    sl, xx, yy = img.win(bx, 580, 70)
    mid = np.abs(np.sqrt((xx - ox) ** 2 + (yy - oy) ** 2) - (R1 - 2.6))
    inside = (cres(xx, yy) < -1.2).astype(np.float32)
    inl = np.clip(0.5 - (mid - 0.55) * img.S, 0, 1) * inside * (np.abs(xx - bx) < 50)
    tone = ramp(np.clip((xx - bx + 50) / 100, 0, 1), [(0, P["moonstone_high"]), (1, P["moonstone_mid"])])
    img.over(sl, tone, inl * 0.85)
    return img, (72, 573 - 562)


def rail_sprite(S):
    """The slim brass rail bucket A rides (y 589.5-592.5), a 4-unit slice the plugin stretches from wall to wall."""
    img = at(S, 200, 588, 4, 6)
    rail = lambda X, Y: sd_rrect(X, Y, 0, 589.5, 800, 592.5, 1.5)
    draw_brass(img, rail, None, "round", depth=1.2, width=1.5, base=-0.06)
    return img, (0, 0)


# ------------------------------------------------------------------------------------------------ bucket B
WATER_TOP = 584.0


def boat_sprite(S):
    """Proposal B at x 400 above its waterline (playfield.bucket_boat): the hull, its rail, the stern post, the brass arm,
    the paper lantern, and the lantern's warmth on the post, the rail and the stern, all fixed to the boat. The air
    glow, the reflection and the warm column on the water are drawn by the plugin each frame."""
    bx = 400.0
    half = 65.5
    img = at(S, bx - 70, 528, 140, 57)
    lx, ly = bx + 50.0, 541.0
    sl2, X2, Y2 = img.win(bx, 562, 80)
    U = (X2 - bx) / half
    top = 573.0 - 4.0 * U ** 4
    hd = np.maximum(np.maximum(top - Y2, Y2 - WATER_TOP),
                    np.abs(X2 - bx) - half * np.where(Y2 < WATER_TOP - 6, 1.0, 1 - (Y2 - (WATER_TOP - 6)) / 30))
    hm = img.cov(hd)
    depth = np.clip((Y2 - top) / (WATER_TOP - top + 1e-3), 0, 1)
    wood = ramp(depth, [(0, "#2A2128"), (1, "#120E14")])
    wood = wood * (1 + 0.06 * smooth(0.6, 0.0, ((Y2 - 573) % 4.0) / 4.0))[..., None]
    img.over(sl2, wood, hm)
    rail = img.cov(np.maximum(np.abs(Y2 - top) - 1.3, np.abs(X2 - bx) - half + 0.5))
    img.over(sl2, hexc("#33262A"), rail)
    topedge = img.cov(np.maximum(np.abs(Y2 - (top - 0.9)) - 0.45, np.abs(X2 - bx) - half + 0.5))
    img.add(sl2, hexc("#9FB0DA"), topedge * (0.45 - 0.25 * np.clip((X2 - bx + half) / (2 * half), 0, 1)))

    def post(X, Y):
        t = np.clip((573 - Y) / 30.0, 0, 1)
        cx = bx + 57 - 6 * t ** 2
        return np.maximum(np.abs(X - cx) - 1.7, np.maximum(Y - 574, 535 - Y))
    sl3, X3, Y3 = img.win(bx + 54, 552, 26)
    img.over(sl3, hexc("#241A1C"), img.cov(post(X3, Y3)))
    arm = lambda X, Y: sd_segment(X, Y, bx + 51, 535.5, lx, 535.5)[0] - 0.8
    draw_brass(img, arm, (lx + 3, 536, 8), "round", depth=0.6, width=0.8)
    sl4, X4, Y4 = img.win(lx, ly, 12)
    body = img.cov(sd_rrect(X4, Y4, lx - 4.6, ly - 6.0, lx + 4.6, ly + 6.0, 3.2))
    across = np.clip(1 - ((X4 - lx) / 4.6) ** 2, 0, 1)
    paper = ramp(across, [(0, "#B4602A"), (0.6, "#F2B060"), (1, "#FFE6B0")])
    ribs = 1 - 0.18 * np.exp(-(((Y4 - ly + 6) % 3.0) - 1.5) ** 2 / 0.12)
    img.over(sl4, paper * ribs[..., None], body)
    for (yc, w_) in ((ly - 6.8, 3.6), (ly + 6.8, 3.2)):
        cap = lambda X, Y, yc=yc, w_=w_: sd_rrect(X, Y, lx - w_, yc - 1.0, lx + w_, yc + 1.0, 0.8)
        draw_brass(img, cap, (lx, yc, 6), "round", depth=0.8, width=0.9)
    # the lantern's warmth on what faces it, within one or two lantern-heights
    sl5, X5, Y5 = img.win(bx, 565, 110)
    d5 = np.sqrt((X5 - lx) ** 2 + (Y5 - ly) ** 2)
    fall = 1 / (1 + (d5 / 14) ** 2) ** 1.5
    pm5 = img.cov(post(X5, Y5))
    facing = pm5 * (1 - img.cov(post(X5 - 1.4, Y5)))
    img.add(sl5, hexc("#FFB060"), (pm5 * 0.12 + facing * 0.9) * fall)
    U5 = (X5 - bx) / half
    top5 = 573.0 - 4.0 * U5 ** 4
    rail5 = img.cov(np.maximum(np.abs(Y5 - top5) - 1.3, np.abs(X5 - bx) - half + 0.5))
    hull5 = img.cov(np.maximum(np.maximum(top5 - Y5, Y5 - WATER_TOP), np.abs(X5 - bx) - half))
    img.add(sl5, hexc("#FFB060"), rail5 * fall * 1.4 + hull5 * fall * 0.45)
    return img, (70, 573 - 528)


def boat_contact_sprite(S):
    """Where the hull meets the water: a dark line and two faint ripple lines beside it (moves with the boat)."""
    bx = 400.0
    half = 65.5
    img = at(S, bx - 84, WATER_TOP - 1, 168, 8)
    sl7, X7, Y7 = img.full()
    inside = np.abs(X7 - bx) < half * 0.9
    img.shadow(sl7, np.exp(-((Y7 - WATER_TOP - 0.4) / 0.8) ** 2) * inside * 0.7)
    for (dy_, a_) in ((2.4, 0.18), (4.6, 0.10)):
        img.add(sl7, hexc("#8FA4DA"), np.exp(-((Y7 - WATER_TOP - dy_) / 0.35) ** 2)
                * (np.abs(X7 - bx) < half + 8 + dy_ * 3) * (np.abs(X7 - bx) > half - 10) * a_)
    return img, (84, 573 - (WATER_TOP - 1))


def water_sprite(S):
    """The strip of still water along the foot (y 584-594), wall to wall, with its faint ripples and moonlit edge."""
    img = at(S, 75, WATER_TOP - 0.5, 650, 11)
    sl, xx, yy = img.full()
    wm = np.clip((yy - WATER_TOP) * img.S + 0.5, 0, 1)
    rip = fbm(img.h, img.w, 5 * img.S, 2, 91)
    wcol = ramp(np.clip((yy - WATER_TOP) / 12, 0, 1), [(0, "#24345F"), (1, "#0E1530")]) * (0.92 + 0.14 * rip)[..., None]
    img.over(sl, wcol, wm)
    img.add(sl, hexc("#8FA4DA"), np.exp(-((yy - WATER_TOP) / 0.7) ** 2) * 0.22 * wm)
    return img, (0, 0.5)


def column_sprite(S):
    """The lantern's broken warm column on the water, straight below it."""
    img = at(S, 450 - 8, WATER_TOP, 16, 10)
    sl, X6, Y6 = img.full()
    colm = np.exp(-((X6 - 450) / 2.6) ** 2) * (0.35 + 0.65 * (np.sin(Y6 * 2.6) > 0.1))
    img.add(sl, hexc("#FFB466"), colm * 0.75)
    return img, (8, 0)


# ------------------------------------------------------------------------------------------------ the lantern cart
ROAD_TOP = 586.0


def cart_sprite(S):
    """The Moon Road's bucket (decision 14; rich/src/frame_rich.bucket_cart, rich pass proposal C) at x 400: a deep open
    box of dark planks with brass corner straps (its mouth is the catch: 104 wide inside, 131 across the rails), two
    spoked wheels on the road, and a paper lantern on a curved rear post, its warmth on the post and the box. The
    lantern's air glow and its pool on the road are drawn by the plugin each frame (they flicker)."""
    bx = 400.0
    half = 65.5
    rim_y, floor_y = 573.0, 583.0
    img = at(S, bx - 70, 528, 140, 68)

    def box(X, Y):
        return sd_rrect(X, Y, bx - half, rim_y, bx + half, floor_y + 1, 2.0)
    sl, X, Y = img.win(bx, 578, 80)
    cov = img.cov(box(X, Y))
    depth = np.clip((Y - rim_y) / (floor_y - rim_y), 0, 1)
    wood = ramp(depth, [(0, "#3A2C30"), (1, "#16101A")])
    grainw = fbm(cov.shape[0], cov.shape[1], 6 * S, 3, 23)
    planks = 1 - 0.18 * np.exp(-(((Y - rim_y) % 3.4) - 0.2) ** 2 / 0.05)
    wood = wood * (0.9 + 0.15 * grainw)[..., None] * planks[..., None]
    img.over(sl, wood, cov)
    # the dark mouth's lip (the catch), seen from the front
    lip = img.cov(np.maximum(np.abs(Y - rim_y - 0.8) - 0.8, np.abs(X - bx) - half + 1))
    img.add(sl, hexc("#9FB0DA"), lip * (0.40 - 0.25 * np.clip((X - bx + half) / (2 * half), 0, 1)))
    # brass corner straps and nail heads
    for sx in (-1, 1):
        strap = lambda X_, Y_, sx=sx: sd_rrect(X_, Y_, bx + sx * (half - 3.0) - 3.0, rim_y - 0.5, bx + sx * (half - 3.0) + 3.0, floor_y + 1, 1.2)
        draw_brass(img, strap, (bx + sx * (half - 3), 578, 9), "round", depth=1.0, width=1.2, base=-0.04)
        for ny in (rim_y + 2.5, floor_y - 2.0):
            nail = lambda X_, Y_, sx=sx, ny=ny: sd_circle(X_, Y_, bx + sx * (half - 3.0), ny, 0.9)
            draw_brass(img, nail, (bx + sx * (half - 3), ny, 2), "round", depth=0.6, width=0.8)
    # two spoked wooden wheels with iron tyres, on the road
    for wx in (bx - 40.0, bx + 40.0):
        wy, wr = 587.5, 6.5
        s2, X2, Y2 = img.win(wx, wy, wr + 2)
        rr = np.sqrt((X2 - wx) ** 2 + (Y2 - wy) ** 2)
        tyre = img.cov(np.abs(rr - wr + 0.9) - 0.9)
        img.over(s2, hexc("#151318"), tyre)
        img.add(s2, hexc("#8FA4DA"), tyre * np.clip(-((X2 - wx) * 0.7 + (Y2 - wy) * 0.7) / wr, 0, 1) * 0.45)
        ang = np.arctan2(Y2 - wy, X2 - wx)
        spokes = img.cov(np.abs(np.sin(ang * 3)) * rr - 0.55) * (rr < wr - 1.5)
        img.over(s2, hexc("#2A2024"), spokes)
        hub = lambda X_, Y_, wx=wx, wy=wy: sd_circle(X_, Y_, wx, wy, 1.6)
        draw_brass(img, hub, (wx, wy, 3), "round", depth=0.8, width=1.2)
    # the rear post and the lantern (the boat's lantern)
    lx, ly = bx + 50.0, 541.0

    def post(X_, Y_):
        t = np.clip((573 - Y_) / 30.0, 0, 1)
        cxp = bx + 57 - 6 * t ** 2
        return np.maximum(np.abs(X_ - cxp) - 1.7, np.maximum(Y_ - 574, 535 - Y_))
    s3, X3, Y3 = img.win(bx + 54, 552, 26)
    img.over(s3, hexc("#241A1C"), img.cov(post(X3, Y3)))
    arm = lambda X_, Y_: sd_segment(X_, Y_, bx + 51, 535.5, lx, 535.5)[0] - 0.8
    draw_brass(img, arm, (lx + 3, 536, 8), "round", depth=0.6, width=0.8)
    s4, X4, Y4 = img.win(lx, ly, 12)
    body = img.cov(sd_rrect(X4, Y4, lx - 4.6, ly - 6.0, lx + 4.6, ly + 6.0, 3.2))
    acr = np.clip(1 - ((X4 - lx) / 4.6) ** 2, 0, 1)
    paper = ramp(acr, [(0, "#B4602A"), (0.6, "#F2B060"), (1, "#FFE6B0")])
    ribs = 1 - 0.18 * np.exp(-(((Y4 - ly + 6) % 3.0) - 1.5) ** 2 / 0.12)
    img.over(s4, paper * ribs[..., None], body)
    for (yc, w_) in ((ly - 6.8, 3.6), (ly + 6.8, 3.2)):
        cap = lambda X_, Y_, yc=yc, w_=w_: sd_rrect(X_, Y_, lx - w_, yc - 1.0, lx + w_, yc + 1.0, 0.8)
        draw_brass(img, cap, (lx, yc, 6), "round", depth=0.8, width=0.9)
    # its warmth on what faces it: the post's facing side, the box's rear corner
    s5, X5, Y5 = img.win(bx + 20, 575, 90)
    d5 = np.sqrt((X5 - lx) ** 2 + (Y5 - ly) ** 2)
    fall = 1 / (1 + (d5 / 14) ** 2) ** 1.5
    pm5 = img.cov(post(X5, Y5))
    facing = pm5 * (1 - img.cov(post(X5 - 1.4, Y5)))
    img.add(s5, hexc("#FFB060"), (pm5 * 0.12 + facing * 0.9) * fall)
    boxm = img.cov(box(X5, Y5))
    fall2 = 1 / (1 + (d5 / 22) ** 2) ** 1.5
    img.add(s5, hexc("#FFB060"), boxm * fall2 * 0.55)
    gl = np.exp(-(((X5 - (bx + half - 3)) / 2.2) ** 2 + ((Y5 - (rim_y + 0.4)) / 0.9) ** 2)) * boxm
    img.add(s5, hexc("#FFD9A0"), gl * 0.45)
    # the box's contact with the road: a soft dark line under it (the box clears the road by a hair)
    s6, X6, Y6 = img.win(bx, 586, 72)
    img.over(s6, hexc("#05070F"), np.exp(-((Y6 - 586.6) / 0.9) ** 2) * (np.abs(X6 - bx) < half - 4) * 0.45)
    return img, (70, rim_y - 528)


def road_sprite(S):
    """The moon road along the foot (frame_rich.road_strip), its left half (the plugin draws it again mirrored to the right
    wall): pale packed earth, moonlit, ruts, and a grass
    edge rim-lit on its upper left. Anchor: its left end on the road's top line."""
    img = at(S, 75, ROAD_TOP - 5, 325, 15)
    sl, xx, yy = img.full()
    m = np.clip((yy - ROAD_TOP) * img.S + 0.5, 0, 1)
    n = fbm(img.h, img.w, 3.0 * img.S, 3, 77)
    col = ramp(np.clip((yy - ROAD_TOP) / 8, 0, 1), [(0, "#5A6488"), (1, "#3A4266")]) * (0.88 + 0.22 * n)[..., None]
    ruts = np.exp(-((yy - 590.5) / 0.8) ** 2) * 0.10
    col = col * (1 - ruts)[..., None]
    img.over(sl, col, m)
    g = fbm(1, img.w, 2.0 * img.S, 2, 5)[0]
    gtop = ROAD_TOP - 1.2 - 2.4 * g
    gm = np.clip((yy - gtop[None, :]) * img.S + 0.5, 0, 1) * np.clip((ROAD_TOP + 1.5 - yy) * img.S + 0.5, 0, 1)
    img.over(sl, hexc("#141B30"), gm * 0.9)
    img.add(sl, hexc("#7F92C8"), gm * np.exp(-((yy - gtop[None, :]) / 0.5) ** 2) * 0.35)
    return img, (0, 5)


# ------------------------------------------------------------------------------------------------ the frame
def bead_outer_sprite(S):
    """The Medallion frame's outer bead as a 9-slice: a 40-unit frame of the bead round the window (playfield.frame)."""
    img = canvas(40, 40, S)
    outer = lambda X, Y: np.maximum(sd_rrect(X, Y, 0, 0, 40, 40, 4), -sd_rrect(X, Y, 6, 6, 34, 34, 2))
    draw_brass(img, outer, None, "round", depth=3.0, width=3.0)
    return img, (0, 0)


def bead_inner_sprite(S):
    """The rails' bead along the board's edge (the walls and the top rail's foot), as a 9-slice."""
    img = canvas(40, 40, S)
    edge = lambda X, Y: np.maximum(sd_rrect(X, Y, 0, 0, 40, 40, 3), -sd_rrect(X, Y, 3.5, 3.5, 36.5, 36.5, 1))
    draw_brass(img, edge, None, "round", depth=2.0, width=1.8)
    return img, (0, 0)


def mottle_sprite(S):
    """The enamel's faint vitreous mottling (mf_lib.enamel's 0.94 + 0.10 n), as a tileable overlay: black where it
    darkens the enamel, white where it lifts it. Full only."""
    T, B = 96, 24
    img = canvas(T, T, S)
    n = fbm(round((T + B) * S), round((T + B) * S), 18 * S, 3, 41)
    t, b = round(T * S), round(B * S)
    for axis in (1, 0):
        w = (np.arange(b) / b).astype(np.float32)
        if axis == 1:
            n[:, :b] = n[:, :b] * w[None, :] + n[:, t:t + b] * (1 - w[None, :])
        else:
            n[:b, :] = n[:b, :] * w[:, None] + n[t:t + b, :] * (1 - w[:, None])
    n = n[:t, :t]
    f = 0.94 + 0.10 * n
    rgb = np.where((f > 1)[..., None], 1.0, 0.0).astype(np.float32)
    a = np.where(f > 1, (f - 1) * 0.25, 1 - f).astype(np.float32)
    img.px[...] = rgb * a[..., None]
    img.a[...] = a
    return img, (0, 0)


# ------------------------------------------------------------------------------------------------ Fever
def cup_sprite(S, centre):
    """One of Fever's five brass cups (fever.fever_cups) with its enamel plate; the plugin writes the value on it."""
    w = (725.0 - 75.0) / 5
    bx = 75 + w * 0.5
    img = at(S, 75, 559, 130, 33)
    rx, ry, rim_y, foot = w / 2 - 5, 5.0, 566.0, 590.0
    base = 0.10 if centre else 0.0

    def wall(X, Y):
        e = ((X - bx) / rx) ** 2 + ((Y - rim_y) / (foot - rim_y)) ** 2 - 1
        return np.maximum(e * 8, rim_y - Y)
    draw_brass(img, wall, (bx, 578, rx + 4), "round", depth=5.0, width=6.0, base=base)
    sl, xx, yy = img.win(bx, rim_y, rx + 3)
    inner = img.cov(((xx - bx) / (rx - 2.0)) ** 2 + ((yy - rim_y) / (ry - 1.2)) ** 2 - 1)
    img.over(sl, ramp(np.clip((yy - rim_y + ry) / (2 * ry), 0, 1), [(0, "#2A2010"), (1, "#0C0A08")]), inner)
    far_wall = inner * np.clip((xx - bx) / rx, 0, 1) * np.clip(1 - (yy - rim_y + ry) / (2 * ry), 0, 1)
    img.add(sl, hexc("#9A7E4A"), far_wall * 0.35)

    def rim(X, Y):
        e = np.sqrt(((X - bx) / rx) ** 2 + ((Y - rim_y) / ry) ** 2)
        return (np.abs(e - 0.93) - 0.08) * rx * 0.5
    draw_brass(img, rim, (bx, rim_y, rx + 3), "round", depth=1.2, width=1.2, base=base + 0.05)
    plate = lambda X, Y: sd_rrect(X, Y, bx - 25, 575, bx + 25, 587, 3)
    sl, xx, yy = img.win(bx, 581, 30)
    img.over(sl, hexc(P["enamel_deep"]), img.cov(plate(xx, yy)) * 0.95)
    img.mul(sl, hexc("#05070F"), img.cov(plate(xx, yy)) * np.exp(-((yy - 575.8) / 0.7) ** 2) * 0.6)
    return img, (65, 581 - 559)


def rule_sprite(S):
    """The banner's gilt rule under FULL MOON (fever.fever_after), 260 units long."""
    img = at(S, 268, 264.7, 264, 4)
    rule = lambda X, Y: sd_rrect(X, Y, 270, 266, 530, 267.4, 0.7)
    draw_brass(img, rule, None, "round", depth=0.6, width=0.7)
    return img, (132, 2)


def band_sprite(S):
    """The banner's soft band behind the pegs: a vertical gaussian, white (tinted abyss), stretched across the board."""
    img = canvas(4, 64, S)
    sl, xx, yy = img.full()
    img.over(sl, hexc("#FFFFFF"), np.exp(-((yy - 32) / 12.3) ** 2) * smooth(32, 29, np.abs(yy - 32)))
    return img, (2, 32)


# ------------------------------------------------------------------------------------------------ the sky
SKY_X0, SKY_Y0, SKY_X1, SKY_Y1 = 70, 36, 730, 600


def sky_image():
    img = Img(800, 600, 1.0)
    sky(img)
    px = img.px[SKY_Y0:SKY_Y1, SKY_X0:SKY_X1]
    return Image.fromarray((np.clip(px, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


# ------------------------------------------------------------------------------------------------ the registry
def sprites():
    """Every sprite: name -> (bake(S) -> (Rgba, anchor in units))."""
    out = {}
    for kind in KINDS:
        for layout in range(LAYOUTS):
            for turn in range(len(TURNS)):
                for state in ("unlit", "lit"):
                    v = layout * len(TURNS) + turn
                    out[f"peg.{kind}.{v}.{state}"] = (lambda S, k=kind, l=layout, t=turn, s=state: peg_sprite(S, k, s, l, t))
        for state in ("unlit", "lit"):
            out[f"brick.{kind}.{state}"] = (lambda S, k=kind, s=state: brick_sprite(S, k, s))
    out.update({
        "brick.flat": brick_flat_sprite,
        "halo": halo_sprite, "bloom": bloom_sprite, "soft": soft_sprite, "speck": speck_sprite,
        "disc": disc_sprite, "sliver": sliver_sprite, "ring": ring_sprite, "dot": dot_sprite, "ball": ball_sprite,
        "launcher.yoke": yoke_sprite, "launcher.tube": tube_sprite, "launcher.hub": hub_sprite,
        "launcher.gauge": lambda S: gauge_sprite(S, False), "launcher.gauge.fill": lambda S: gauge_sprite(S, True),
        "bucket.cradle": cradle_sprite, "bucket.rail": rail_sprite,
        "bucket.boat": boat_sprite, "bucket.boat.contact": boat_contact_sprite, "bucket.water": water_sprite,
        "bucket.column": column_sprite,
        "bucket.cart": cart_sprite, "bucket.road": road_sprite,
        "frame.bead.outer": bead_outer_sprite, "frame.bead.inner": bead_inner_sprite, "frame.mottle": mottle_sprite,
        "fever.cup": lambda S: cup_sprite(S, False), "fever.cup.centre": lambda S: cup_sprite(S, True),
        "fever.rule": rule_sprite, "fever.band": band_sprite,
    })
    return out


def pack(sizes):
    """Skyline packing at 1x, tallest first: each sprite goes where its top comes out lowest (then leftmost), so the
    short ones fill the space under the shelves the tall ones open. Returns name -> (x, y) of each sprite's content (its
    padding round it) and the atlas height."""
    order = sorted(sizes, key=lambda n: (-sizes[n][1], -sizes[n][0], n))
    sky = np.zeros(ATLAS_WIDTH, np.int64)
    pos = {}
    for n in order:
        w, h = int(sizes[n][0]) + 2 * PAD, int(sizes[n][1]) + 2 * PAD
        if w > ATLAS_WIDTH:
            raise ValueError(f"{n} is wider than the atlas")
        tops = np.lib.stride_tricks.sliding_window_view(sky, w).max(axis=1)
        x = int(np.argmin(tops))
        y = int(tops[x])
        sky[x:x + w] = y + h
        pos[n] = (x + PAD, y + PAD)
    return pos, int(sky.max())


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    table = sprites()
    baked = {1: {}, 2: {}}
    anchors, sizes = {}, {}
    for name, fn in table.items():
        for S in (1, 2):
            img, anchor = fn(S)
            baked[S][name] = img.rgba()
            if S == 1:
                anchors[name] = anchor
                sizes[name] = (img.w, img.h)
            elif (img.w, img.h) != (sizes[name][0] * 2, sizes[name][1] * 2):
                raise ValueError(f"{name}: the 2x sprite is not twice the 1x one")
    pos, height = pack(sizes)
    height = (height + 3) // 4 * 4
    manifest = {"format": "moonfall-atlas", "version": 1, "width": ATLAS_WIDTH, "height": height,
                "files": {"1x": "atlas.png", "2x": "atlas@2x.png", "sky": "sky.png"},
                "sky": {"x": SKY_X0, "y": SKY_Y0, "w": SKY_X1 - SKY_X0, "h": SKY_Y1 - SKY_Y0},
                "pegVariants": LAYOUTS * len(TURNS),
                "buckets": {"base": "cart", "expansion": "boat"},
                "gauge": {"from": GAUGE_A0, "to": GAUGE_A1},
                "inks": {**{f"glow.{k}": PEG[k]["glow"] for k in KINDS}, **{f"flat.{k}": PEG[k]["albedo"] for k in KINDS},
                         **{f"flat.{k}.shade": PEG[k]["sea"] for k in KINDS},
                         "ground": SKY_AT_PEGS, "enamel": P["enamel"], "enamel.deep": P["enamel_deep"], "keyline": P["gilt"],
                         "lantern": "#FFB868", "cup.lit": P["gilt_high"], "moonlight": P["moonstone_high"],
                         "dim": "#0A0E22", "band": "#03050C", "contact": "#05070F"},
                "points": {"boat.lantern": [50, -32]},
                "slices": {"frame.bead.outer": 12, "frame.bead.inner": 8},
                "sprites": {}}
    for S in (1, 2):
        sheet = np.zeros((height * S, ATLAS_WIDTH * S, 4), np.float32)
        for name, px in baked[S].items():
            x, y = pos[name]
            p = PAD * S
            padded = np.pad(px, ((p, p), (p, p), (0, 0)), mode="edge")
            sheet[(y - PAD) * S:(y - PAD) * S + padded.shape[0], (x - PAD) * S:(x - PAD) * S + padded.shape[1]] = padded
        im = Image.fromarray((np.clip(sheet, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")
        im.save(OUT / ("atlas.png" if S == 1 else "atlas@2x.png"), optimize=True)
    for name in sorted(table):
        x, y = pos[name]
        w, h = sizes[name]
        ax, ay = anchors[name]
        manifest["sprites"][name] = {"x": x, "y": y, "w": w, "h": h, "ax": round(float(ax), 3), "ay": round(float(ay), 3)}
    sky_image().save(OUT / "sky.png", optimize=True)
    scenes = OUT / "scenes"
    scenes.mkdir(exist_ok=True)
    for S, suffix in ((1, ""), (2, "@2x")):
        img = Img(800 * S, 600 * S, S)
        sky(img)
        img.save(scenes / f"moon-road-night{suffix}.png")
    (OUT / "atlas.json").write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8")
    total = sum((OUT / f).stat().st_size for f in ("atlas.png", "atlas@2x.png", "sky.png", "atlas.json"))
    for f in sorted(scenes.iterdir()):
        print(f"scene {f.name}: {f.stat().st_size:,} bytes")
    tex = ATLAS_WIDTH * height * 4
    sky_px = (SKY_X1 - SKY_X0) * (SKY_Y1 - SKY_Y0) * 4
    print(f"{len(table)} sprites, atlas {ATLAS_WIDTH} x {height} (2x {ATLAS_WIDTH * 2} x {height * 2}), files {total:,} bytes")
    print(f"texture memory: 1x {tex:,} + sky {sky_px:,} = {tex + sky_px:,} bytes; with 2x {tex * 5 + sky_px:,} bytes")


if __name__ == "__main__":
    main()
