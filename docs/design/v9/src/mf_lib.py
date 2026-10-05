"""Moonfall concept kit (plan v9, G8): one light, moon pegs, moonstone bricks, the silver ball, brass, enamel and text.

Everything is drawn analytically (signed distances, sphere and height-field normals) so the same code renders the 1x
(800 x 600) and 2x (1600 x 1200) tiers. Units are game px (the engine's 800 x 600 playfield); S is device px per unit.

The one light (every asset): a directional moonlight from the upper left, slightly in front of the board,
L = (-0.424, -0.424, 0.80) with x right, y down and z toward the viewer. A sphere lit by it shows a gibbous face
with a soft terminator and an unlit sliver 0.2 r wide on its lower right: that is what makes a peg read as a moon
(and never as a coin or cheese) at every size. Brass, bricks and the ball are shaded with the same vector.

Only numpy and Pillow; reuses docs/design/v8/art/src/artlib.py (blur, fbm, hexc).
"""
import math
import pathlib
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

V8 = pathlib.Path(__file__).resolve().parents[2] / "v8" / "art" / "src"
sys.path.insert(0, str(V8))
from artlib import blur, fbm, hexc, smooth  # noqa: E402

V9 = pathlib.Path(__file__).resolve().parent.parent
LUM = np.array([0.2126, 0.7152, 0.0722], np.float32)
L = np.array([-0.424, -0.424, 0.80], np.float32)
L = L / np.linalg.norm(L)
H_BLINN = (L + np.array([0, 0, 1], np.float32))
H_BLINN = H_BLINN / np.linalg.norm(H_BLINN)

# ------------------------------------------------------------------------------------------------ palette (Medallion)
P = {
    "abyss": "#080B16", "enamel": "#1D2B5A", "enamel_deep": "#131C40", "zenith": "#1B2552",
    "status_top": "#0E1329", "status_foot": "#0A0E1C",
    "gilt_spec": "#FFF4D6", "gilt_high": "#E6CF98", "gilt": "#D9BE82", "gilt_mid": "#9A7E4A",
    "gilt_shade": "#7C6236", "gilt_deep": "#5C4724", "gilt_dark": "#33260F",
    "corner_lit": "#F0D9A0", "corner_shaded": "#B79755",
    "moonstone_spec": "#F4F2EA", "moonstone_high": "#E2E8F4", "moonstone": "#C3CEE4", "moonstone_mid": "#95A5C8",
    "moonstone_deep": "#5E6E97", "cream": "#F3E9D2", "ink_dim": "#9AA6C8",
}
BRASS_RAMP = [(0.00, "#1E1608"), (0.12, P["gilt_dark"]), (0.26, P["gilt_deep"]), (0.40, P["gilt_shade"]),
              (0.55, P["gilt_mid"]), (0.72, P["gilt"]), (0.86, P["gilt_high"]), (1.00, P["gilt_spec"])]

# peg albedo (lit face at full light) and the seas' tone, per kind. Hues are 60-90 degrees apart and the four differ
# in value too (orange and green brightest, purple darkest), so they separate for colour-blind players as well.
PEG = {
    "blue":   {"albedo": "#9DBDF2", "sea": "#6C84B8", "glow": "#A8C6FF"},
    "orange": {"albedo": "#F49A50", "sea": "#B5623A", "glow": "#FFB070"},
    "green":  {"albedo": "#86DA98", "sea": "#4E9A68", "glow": "#9CF0B0"},
    "purple": {"albedo": "#C58CEB", "sea": "#8657B0", "glow": "#D8A4FF"},
}


def ramp(t, stops):
    t = np.clip(t, 0, 1)
    ts = [s[0] for s in stops]
    cs = np.stack([hexc(s[1]) for s in stops])
    return np.stack([np.interp(t, ts, cs[:, k]) for k in range(3)], -1).astype(np.float32)


def screen(a, b):
    return 1 - (1 - a) * (1 - np.clip(b, 0, 1))


class Img:
    """A float RGB canvas in device px, with a scale S (device px per game unit)."""

    def __init__(self, w, h, S=1.0, px=None):
        self.w, self.h, self.S = int(w), int(h), float(S)
        self.px = np.zeros((self.h, self.w, 3), np.float32) if px is None else px

    def win(self, x, y, rad):
        """A local window (device px) around game point (x, y) of game radius rad: slices and the game-unit grids."""
        S = self.S
        x0, x1 = max(0, int((x - rad) * S) - 1), min(self.w, int((x + rad) * S) + 2)
        y0, y1 = max(0, int((y - rad) * S) - 1), min(self.h, int((y + rad) * S) + 2)
        if x1 <= x0 or y1 <= y0:
            return None
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        return (slice(y0, y1), slice(x0, x1)), (xx + 0.5) / S, (yy + 0.5) / S

    def full(self):
        yy, xx = np.mgrid[0:self.h, 0:self.w].astype(np.float32)
        return (slice(0, self.h), slice(0, self.w)), (xx + 0.5) / self.S, (yy + 0.5) / self.S

    def over(self, sl, col, a):
        a = np.clip(a, 0, 1)[..., None]
        self.px[sl] = self.px[sl] * (1 - a) + np.asarray(col, np.float32) * a

    def add(self, sl, col, amt):
        self.px[sl] = screen(self.px[sl], np.asarray(col, np.float32) * np.clip(amt, 0, None)[..., None])

    def mul(self, sl, col, amt):
        a = np.clip(amt, 0, 1)[..., None]
        self.px[sl] = self.px[sl] * (1 - a + a * np.asarray(col, np.float32))

    def cov(self, sdf):
        """Anti-aliased coverage of a game-unit signed distance (negative inside), 1 device px wide."""
        return np.clip(0.5 - sdf * self.S, 0, 1)

    def save(self, path, size=None):
        im = Image.fromarray((np.clip(self.px, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
        if size:
            im = im.resize(size, Image.LANCZOS)
        im.save(path, optimize=True)
        return im


# ------------------------------------------------------------------------------------------------ signed distances
def sd_circle(xx, yy, cx, cy, r):
    return np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) - r


def sd_rrect(xx, yy, x0, y0, x1, y1, r=0.0):
    cx, cy, hw, hh = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
    qx, qy = np.abs(xx - cx) - hw + r, np.abs(yy - cy) - hh + r
    return np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - r


def sd_segment(xx, yy, ax, ay, bx, by):
    px, py, dx, dy = xx - ax, yy - ay, bx - ax, by - ay
    t = np.clip((px * dx + py * dy) / max(dx * dx + dy * dy, 1e-6), 0, 1)
    return np.sqrt((px - dx * t) ** 2 + (py - dy * t) ** 2), t


def sd_sector(xx, yy, ax, ay, R, th, half, t):
    """An annular sector (a curved brick): centre (ax, ay), mid radius R, mid angle th, half span (rad), thickness t."""
    dx, dy = xx - ax, yy - ay
    rr = np.sqrt(dx * dx + dy * dy)
    ang = np.arctan2(dy, dx) - th
    ang = (ang + np.pi) % (2 * np.pi) - np.pi
    d_r = np.abs(rr - R) - t / 2
    d_a = (np.abs(ang) - half) * R
    return np.sqrt(np.maximum(d_r, 0) ** 2 + np.maximum(d_a, 0) ** 2) + np.minimum(np.maximum(d_r, d_a), 0)


# ------------------------------------------------------------------------------------------------ shading
def normals_from_height(h, S):
    gy, gx = np.gradient(h)
    nx, ny = -gx * S, -gy * S
    nz = np.ones_like(h)
    n = np.sqrt(nx * nx + ny * ny + nz * nz)
    return nx / n, ny / n, nz / n


def brass_shade(nx, ny, nz, spec_k=1.0, base=0.0):
    """Brass under the one light: diffuse, a Blinn highlight and a dim reflection of the night sky above (upward
    normals reflect the sky, downward ones the dark board), mapped onto the Medallion gilt ramp."""
    lam = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    nh = np.clip(nx * H_BLINN[0] + ny * H_BLINN[1] + nz * H_BLINN[2], 0, 1)
    spec = nh ** 60 * 0.55 + nh ** 12 * 0.12
    env = np.clip(-ny * 0.5 + 0.5, 0, 1) * 0.10
    t = 0.10 + 0.62 * lam + spec * spec_k + env + base
    return ramp(t, BRASS_RAMP)


def enamel(sl, xx, yy, img, x0, y0, x1, y1):
    """A dark lapis enamel panel: lit end at the upper left, deep end at the lower right, faint vitreous mottling."""
    t = np.clip(((xx - x0) / max(1, x1 - x0) * 0.4 + (yy - y0) / max(1, y1 - y0) * 0.6), 0, 1)
    col = ramp(t, [(0, P["enamel"]), (1, P["enamel_deep"])])
    n = fbm(col.shape[0], col.shape[1], 18 * img.S, 3, 41)
    return col * (0.94 + 0.10 * n)[..., None]


# ------------------------------------------------------------------------------------------------ the seas
# Four layouts of lobed maria, each 4-5 separate seas of different depth clustered across the upper middle of the disc
# (never along a curve, so no hook, ring or "C"): (x, y, rx, ry, depth, lobe phase) in disc radii. Each peg also turns
# its layout by its own angle, so no two neighbours match.
SEA_SETS = [
    [(-0.30, -0.22, 0.24, 0.18, 1.00, 0.3), (0.10, -0.31, 0.17, 0.14, 0.80, 1.1), (0.33, -0.05, 0.19, 0.16, 0.90, 2.0),
     (-0.50, 0.10, 0.12, 0.22, 0.55, 2.7), (-0.06, -0.02, 0.10, 0.08, 0.60, 0.9)],
    [(-0.22, -0.28, 0.28, 0.17, 0.95, 1.4), (0.24, -0.20, 0.20, 0.17, 0.85, 0.2), (-0.46, 0.04, 0.14, 0.18, 0.65, 2.2),
     (0.40, 0.12, 0.12, 0.13, 0.60, 1.7)],
    [(-0.36, -0.12, 0.22, 0.22, 0.90, 0.6), (0.02, -0.30, 0.20, 0.13, 0.75, 2.4), (0.30, -0.12, 0.16, 0.19, 1.00, 1.0),
     (-0.10, 0.10, 0.12, 0.09, 0.55, 2.9), (0.44, 0.20, 0.09, 0.10, 0.50, 0.4)],
    [(-0.26, -0.18, 0.30, 0.20, 1.00, 2.6), (0.20, -0.28, 0.16, 0.12, 0.70, 1.3), (0.34, 0.00, 0.18, 0.14, 0.85, 0.5),
     (-0.54, 0.16, 0.10, 0.16, 0.50, 1.9)],
]


def seas_field(u, v, variant=0, rot=None):
    """The seas, 0..1: separate lobed maria of different depth, each with a soft but definite edge (about 0.06 r)."""
    a = (variant * 0.61) % 1.0 * 1.4 - 0.7 if rot is None else rot
    ca, sa = math.cos(a), math.sin(a)
    ur, vr = u * ca + v * sa, -u * sa + v * ca
    acc = np.zeros_like(u)
    for (sx, sy, rx, ry, k, ph) in SEA_SETS[variant % 4]:
        du, dv = (ur - sx) / rx, (vr - sy) / ry
        rr = np.sqrt(du * du + dv * dv)
        th = np.arctan2(dv, du)
        edge = 1 + 0.16 * np.sin(3 * th + ph) + 0.08 * np.sin(5 * th + 2 * ph)       # a lobed outline
        m = smooth(edge + 0.22, edge - 0.22, rr) * k
        acc = np.maximum(acc, m)
    return acc


# ------------------------------------------------------------------------------------------------ moon pegs
def draw_moon(img, x, y, r, kind, state="unlit", variant=0, sky="#141C3A", scale=1.0, alpha=1.0, flash=0.0, rot=None):
    """A peg as a small moon. state: 'unlit' (a gibbous moon in the one light: lit face, a soft terminator, an unlit
    part about 0.2 r wide on the lower right in earthshine, so the whole disc stays round), 'lit' (struck: the face
    brightens in the kind's hue and a halo blooms; the seas and a narrower sliver stay). scale/alpha/flash are for
    the clearing frames. An unlit peg has no halo: it is not a light."""
    r = r * scale
    w = img.win(x, y, r * 3.2)
    if w is None:
        return
    sl, xx, yy = w
    S = img.S
    u, v = (xx - x) / r, (yy - y) / r
    d2 = u * u + v * v
    d = np.sqrt(d2)
    cov = np.clip((1 - d) * r * S + 0.5, 0, 1) * alpha
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    mu0 = u * L[0] + v * L[1] + nz * L[2]
    ls = np.minimum(np.where(mu0 > 0, mu0 / (mu0 + nz + 1e-4), 0.0) / 0.5, 1.12)     # Lommel-Seeliger: a flat lunar face
    lit_t = blur(smooth(-0.05, 0.08, mu0), 0.11 * r * S)                            # a soft terminator, ~0.25 r wide
    shade = 0.80 * ls + 0.20 * np.clip(mu0, 0, 1)
    k = PEG[kind]
    alb, sea, glow = hexc(k["albedo"]), hexc(k["sea"]), hexc(k["glow"])
    s = seas_field(u, v, variant, rot)
    col = alb[None, None, :] * (1 - s[..., None] * 0.62) + sea[None, None, :] * (s[..., None] * 0.62)
    skyc = hexc(sky)
    earth = skyc * 1.45 + alb * 0.13                                                # earthshine: the disc stays round
    if state == "lit":
        col = col * 0.80 + np.array([1, 1, 1], np.float32) * 0.10 + glow * 0.12
        lit_t = blur(smooth(-0.20, -0.06, mu0), 0.08 * r * S)                       # the sliver narrows to ~0.12 r
        shade = np.maximum(shade * 1.10 + 0.05, 0.55)
        earth = earth * 0.6 + glow * 0.26
    face = col * shade[..., None]
    face = face * lit_t[..., None] + earth * (1 - lit_t[..., None])
    if flash:
        face = screen(face, glow * flash)
    if state == "lit":
        img.add(sl, glow, (np.exp(-np.clip(d - 0.92, 0, None) ** 2 * 3.2) * 0.50 + np.exp(-np.clip(d - 1, 0, None) * 2.6) * 0.14) * alpha)
    if flash:
        img.add(sl, glow, np.exp(-np.clip(d - 1, 0, None) ** 2 * 2.2) * flash * 0.55)
    img.over(sl, face, cov)


def draw_motes(img, x, y, r, kind, t, seed=0):
    """Clearing: the moon's light sifts down as fine moondust and fades (t 0..1): many soft specks, most of them
    below the peg and sinking, none larger than a twentieth of the peg. No light of its own beyond its glow."""
    rng = np.random.default_rng(seed)
    glow = hexc(PEG[kind]["glow"])
    fade = (1 - t) ** 1.6
    for i in range(28):
        ang = rng.uniform(0.15, math.pi - 0.15) if rng.random() < 0.75 else rng.random() * 2 * math.pi
        rad = r * (0.25 + 0.9 * rng.random()) * (0.6 + 0.6 * t)
        mx = x + math.cos(ang) * rad * 0.9
        my = y + math.sin(ang) * rad * 0.7 + r * 1.4 * t * (0.6 + 0.8 * rng.random())
        mr = r * (0.035 + 0.035 * rng.random())
        w = img.win(mx, my, mr * 4)
        if w is None:
            continue
        sl, xx, yy = w
        dd = np.sqrt((xx - mx) ** 2 + (yy - my) ** 2) / max(mr, 1e-3)
        img.add(sl, glow * 0.7 + 0.3, np.exp(-dd * dd * 0.9) * fade * (0.45 + 0.4 * rng.random()))


# ------------------------------------------------------------------------------------------------ bricks
def draw_brick(img, sdf_fn, bbox, kind, state="unlit", variant=0, alpha=1.0):
    """A moonstone brick: a pillowed slab cut in the kind's stone, lit by the one light (diffuse, a soft polish
    highlight), with the same lobed mottling as the seas. sdf_fn(xx, yy) gives its signed distance; bbox = (x, y, rad)."""
    w = img.win(*bbox)
    if w is None:
        return
    sl, xx, yy = w
    S = img.S
    sd = sdf_fn(xx, yy)
    cov = img.cov(sd) * alpha
    hgt = np.sqrt(np.clip(-sd / 4.5, 0, 1)) * 4.5                                   # a rounded edge 4.5 units wide
    nx, ny, nz = normals_from_height(hgt, S)
    lam = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    nh = np.clip(nx * H_BLINN[0] + ny * H_BLINN[1] + nz * H_BLINN[2], 0, 1)
    k = PEG[kind]
    alb, sea, glow = hexc(k["albedo"]), hexc(k["sea"]), hexc(k["glow"])
    m = fbm(sd.shape[0], sd.shape[1], 9 * S, 3, 70 + variant)
    s = np.clip((m - 0.45) * 2.4, 0, 1) * 0.55
    col = alb * (1 - s[..., None]) + sea * s[..., None]
    shade = 0.10 + 1.0 * lam ** 1.4
    if state == "lit":
        col = col * 0.72 + 0.18 + glow * 0.16
        shade = shade * 1.08 + 0.08
        img.add(sl, glow, np.exp(-np.clip(sd, 0, None) ** 2 / 30) * 0.45 * alpha)
    face = col * shade[..., None] + hexc("#FFFFFF") * (nh ** 50 * 0.35)[..., None]
    img.over(sl, face, cov)


def sector_brick(ax, ay, R, th, half, t=12.0):
    def f(xx, yy):
        return sd_sector(xx, yy, ax, ay, R, th, half, t)
    cx, cy = ax + R * math.cos(th), ay + R * math.sin(th)
    return f, (cx, cy, R * half + t + 4)


# ------------------------------------------------------------------------------------------------ the ball
def draw_ball(img, x, y, r=6.0, alpha=1.0):
    """The ball: satin silver, brighter than any unlit peg. Diffuse in the one light with a broad soft highlight on
    the upper left and a small sharper core; a soft horizon between the reflected sky (upper half) and the dark board
    (lower half); a thin darker limb that keeps its edge against any background."""
    w = img.win(x, y, r * 2)
    if w is None:
        return
    sl, xx, yy = w
    u, v = (xx - x) / r, (yy - y) / r
    d2 = u * u + v * v
    d = np.sqrt(d2)
    cov = np.clip((1 - d) * r * img.S + 0.5, 0, 1) * alpha
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    rx, ry, rz = 2 * nz * u, 2 * nz * v, 2 * nz * nz - 1
    sky_t = smooth(-0.45, 0.45, -ry)
    env = ramp(sky_t, [(0, "#20263A"), (1, "#5A6A9E")])
    hot = np.clip(rx * L[0] + ry * L[1] + rz * L[2], 0, 1)
    lam = np.clip(u * L[0] + v * L[1] + nz * L[2], 0, 1)
    diff = hexc("#EEF1F6") * (0.34 + 0.86 * lam)[..., None]
    col = diff * 0.78 + env * 0.22
    col = screen(col, hexc("#FFFFFF") * (hot ** 18 * 0.45 + hot ** 120 * 0.55)[..., None])
    col = col * (1 - 0.38 * smooth(0.78, 1.0, d))[..., None]                      # the limb
    img.over(sl, np.clip(col, 0, 1), cov)


# ------------------------------------------------------------------------------------------------ brass parts
def draw_brass(img, sdf_fn, bbox, profile="round", depth=3.0, width=None, spec_k=1.0, alpha=1.0, base=0.0):
    """A brass part from its signed distance: profile 'round' (a tube or bead, height rising over `width` units from
    the edge) or 'bevel' (a flat top with a bevelled rim). Shaded by brass_shade, so every part takes the one light."""
    w = img.win(*bbox) if bbox else img.full()
    if w is None:
        return None
    sl, xx, yy = w
    sd = sdf_fn(xx, yy)
    cov = img.cov(sd) * alpha
    wd = width if width else depth
    e = np.clip(-sd / wd, 0, 1)
    hgt = (np.sqrt(1 - (1 - e) ** 2) if profile == "round" else smooth(0, 1, e)) * depth
    nx, ny, nz = normals_from_height(hgt, img.S)
    col = brass_shade(nx, ny, nz, spec_k, base)
    img.over(sl, col, cov)
    return sl, cov


# ------------------------------------------------------------------------------------------------ text
FONTS = {
    "serif": "C:/Windows/Fonts/georgia.ttf", "serif_b": "C:/Windows/Fonts/georgiab.ttf",
    "serif_i": "C:/Windows/Fonts/georgiai.ttf", "ui": "C:/Windows/Fonts/segoeui.ttf",
    "ui_sb": "C:/Windows/Fonts/seguisb.ttf", "ui_b": "C:/Windows/Fonts/segoeuib.ttf",
}


def font(name, size):
    try:
        return ImageFont.truetype(FONTS[name], int(round(size)))
    except OSError:
        return ImageFont.truetype(FONTS["ui"], int(round(size)))


def text(img, x, y, s, fname, size, col, anchor="la", halo=0.55, tracking=0.0, engraved=False):
    """Text at game point (x, y), size in game units. A soft abyss halo keeps it legible over sky or brass.
    engraved: cut into brass (a dark cut with a lit lower lip, as the one light from the upper left would show)."""
    S = img.S
    f = font(fname, size * S)
    probe = ImageDraw.Draw(Image.new("L", (4, 4)))
    widths = [probe.textlength(ch, font=f) for ch in s] if tracking else None
    total = (sum(widths) + tracking * S * (len(s) - 1)) if tracking else probe.textlength(s, font=f)
    pad = int(size * S * 1.2 + 8 * S)
    ox = int(x * S - total - pad)
    oy = int(y * S - size * S * 1.5 - pad)
    ww, hh = int(total * 2 + pad * 2), int(size * S * 3 + pad * 2)
    lay = Image.new("L", (ww, hh), 0)
    dr = ImageDraw.Draw(lay)
    lx, ly = x * S - ox, y * S - oy
    if tracking:
        hx = {"l": 0, "m": total / 2, "r": total}[anchor[0]]
        cx = lx - hx
        for ch, wd in zip(s, widths):
            dr.text((cx, ly), ch, font=f, fill=255, anchor="l" + anchor[1])
            cx += wd + tracking * S
    else:
        dr.text((lx, ly), s, font=f, fill=255, anchor=anchor)
    m = np.asarray(lay, np.float32) / 255
    # clip the window to the canvas
    x0, y0 = max(0, ox), max(0, oy)
    x1, y1 = min(img.w, ox + ww), min(img.h, oy + hh)
    if x1 <= x0 or y1 <= y0:
        return
    sl = (slice(y0, y1), slice(x0, x1))
    msub = lambda a: a[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
    if engraved:
        k = int(max(1, S * 0.6))
        lip = np.clip(np.roll(np.roll(m, k, 0), k, 1) - m, 0, 1)
        img.over(sl, hexc(P["gilt_high"]), msub(lip) * 0.7)
        img.over(sl, hexc(P["gilt_dark"]), msub(m) * 0.92)
        return
    if halo:
        img.over(sl, hexc(P["abyss"]), msub(np.clip(blur(m, 1.6 * S) * 2.2, 0, 1)) * halo)
    img.over(sl, hexc(col) if isinstance(col, str) else np.asarray(col, np.float32), msub(m))


def text_size(s, fname, size, S=1.0):
    f = font(fname, size * S)
    b = f.getbbox(s)
    return (b[2] - b[0]) / S, (b[3] - b[1]) / S


def wrap(s, fname, size, maxw, S=1.0):
    words, lines, cur = s.split(), [], ""
    for wd in words:
        t = (cur + " " + wd).strip()
        if text_size(t, fname, size, S)[0] <= maxw or not cur:
            cur = t
        else:
            lines.append(cur)
            cur = wd
    if cur:
        lines.append(cur)
    return lines


def crop_save(src, box, dst, zoom=1):
    im = Image.open(src).crop(box)
    if zoom != 1:
        im = im.resize((im.width * zoom, im.height * zoom), Image.NEAREST)
    im.save(dst)
