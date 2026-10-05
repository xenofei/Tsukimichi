"""Our own painting for 2-3 "Twin Lanterns": Vesper Bay's harbour gate between two stone lanterns, mirrored in still water.

A sandstone gate (two square pillars and a round arch, in the Thanalan style) stands at the water's edge between two
tall stone lanterns, each lit. The bay is glass-still, so everything stands twice: the gate and the twin lanterns above
the waterline, and their mirror images below it, darker and broken by slow ripples. A violet night sky with the moon's
glow from the upper left; the far shore a low dark line on the horizon.

FEATURES (board units): the waterline, the arch (a circle), the pillars, the lanterns' heads and posts.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

WATER = 350.0
ARCH = (400.0, 274.0, 88.0)           # centre and radius of the arch's inner curve's centreline
PILLARS = [(318.0, 30.0), (482.0, 30.0)]   # x and width
LANTERNS = [(196.0, 236.0), (604.0, 236.0)]
POST_TOP = 254.0

FEATURES = {
    "waterline": WATER,
    "arch": {"circle": list(ARCH), "from": 200, "sweep": 140},
    "pillars": [[x, 262, WATER - 8] for (x, _w) in PILLARS],
    "lanterns": [list(p) for p in LANTERNS],
    "posts": [[x, POST_TOP + 22, WATER - 8] for (x, _y) in LANTERNS],
}


def _shape(S, W, H):
    """The gate and the lanterns above the water, as a coverage mask (the reflection mirrors it)."""
    ss = 3
    im = Image.new("L", (W * ss, H * ss), 0)
    dr = ImageDraw.Draw(im)
    cx, cy, R = ARCH
    k = S * ss
    # pillars
    for (x, w) in PILLARS:
        dr.rectangle([(x - w / 2) * k, (cy - 18) * k, (x + w / 2) * k, WATER * k], fill=255)
        dr.rectangle([(x - w / 2 - 5) * k, (cy - 24) * k, (x + w / 2 + 5) * k, (cy - 16) * k], fill=255)   # capitals
    # the arch: a thick band between R-14 and R+14 over the upper half, and the lintel above it
    dr.pieslice([(cx - R - 16) * k, (cy - R - 16) * k, (cx + R + 16) * k, (cy + R + 16) * k], 180, 360, fill=255)
    dr.pieslice([(cx - R + 14) * k, (cy - R + 14) * k, (cx + R - 14) * k, (cy + R - 14) * k], 180, 360, fill=0)
    dr.rectangle([(cx - R + 14) * k, cy * k, (cx + R - 14) * k, WATER * k], fill=0)
    # the lanterns: a post, a housing with a roof and a finial
    for (lx, ly) in LANTERNS:
        dr.rectangle([(lx - 5) * k, POST_TOP * k, (lx + 5) * k, WATER * k], fill=255)
        dr.rectangle([(lx - 9) * k, (WATER - 14) * k, (lx + 9) * k, WATER * k], fill=255)
        dr.rectangle([(lx - 11) * k, (ly - 8) * k, (lx + 11) * k, (ly + 16) * k], fill=255)
        dr.polygon([((lx - 17) * k, (ly - 8) * k), ((lx + 17) * k, (ly - 8) * k), (lx * k, (ly - 22) * k)], fill=255)
        dr.ellipse([(lx - 3) * k, (ly - 28) * k, (lx + 3) * k, (ly - 22) * k], fill=255)
    m = np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255
    # the lanterns' windows are holes (lit)
    win = np.zeros_like(m)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    for (lx, ly) in LANTERNS:
        win = np.maximum(win, ((np.abs(X - lx) < 7) & (Y > ly - 4) & (Y < ly + 12)).astype(np.float32))
    return m, win


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    sky = ramp(np.clip(Y / WATER, 0, 1), [(0, "#08061C"), (0.5, "#151036"), (1.0, "#2C2456")])
    d_moon = np.sqrt((X + 60) ** 2 + (Y + 80) ** 2)
    sky = screen(sky, hexc("#C4C0EE") * (np.exp(-(d_moon / 380) ** 2) * 0.16 + np.exp(-(d_moon / 900) ** 2) * 0.05)[..., None])
    sky = strokes(sky, np.ones((H, W), np.float32), S, lambda x, y: 0.15 * math.sin(x / 90), int(4000 * S * S),
                  length=(10, 20), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=14)
    px = RL.stars(sky, S, 800, 61, (0, 0, W, (WATER - 30) * S), bright=0.5)
    # the far shore: a low dark band on the horizon, a few lights
    shore = WATER - 6 - 6 * fbm(1, W, 30 * S, 3, 7)[0] - 4 * smooth(0, 300, X[0]) * smooth(800, 500, X[0])
    sm = np.clip((Y - shore[None, :]) * S + 0.5, 0, 1) * (Y < WATER + 0.5)
    px = px * (1 - sm[..., None]) + hexc("#0E0C24") * sm[..., None]
    # the gate and the lanterns: sandstone in moonlight (lit on the left faces), the windows warm
    m, win = _shape(S, W, H)
    gy, gx = np.gradient(blur(m, 2.0 * S))
    lit = np.clip((gx * 0.7 + gy * 0.7) * S * 6.0, -1, 1)
    stone = ramp(np.clip(blur(m, 10 * S), 0, 1), [(0, "#1A1730"), (1, "#2E2A48")])
    stone = stone * (0.9 + 0.35 * lit)[..., None]
    courses = (np.sin(Y * 1.5) > 0.92).astype(np.float32) * 0.12
    stone = stone * (1 - courses)[..., None]
    px = px * (1 - m[..., None]) + stone * m[..., None]
    rim = np.clip(m - np.roll(np.roll(m, int(S), 1), int(S), 0), 0, 1)
    px = screen(px, hexc("#B8B0E8") * (rim * 0.45)[..., None])
    glow = np.zeros((H, W), np.float32)
    for (lx, ly) in LANTERNS:
        glow = np.maximum(glow, np.exp(-(((X - lx) / 16) ** 2 + ((Y - ly - 4) / 16) ** 2)))
    px = screen(px, hexc("#FFB45E") * (win * 0.75 + glow * 0.12)[..., None])
    # ---- the water: a mirror of all above, darker toward the viewer, broken by slow horizontal ripples
    wm = np.clip((Y - WATER) * S + 0.5, 0, 1)
    depth = np.clip((Y - WATER) / (600 - WATER), 0, 1)
    ry = np.clip(2 * WATER - Y, 0, WATER - 1)
    iy = np.clip((ry * S).astype(int), 0, H - 1)
    rip = fbm(H, W, 4.0 * S, 3, 33)
    shift = (np.sin(Y * 0.8 + rip * 4) * (1.5 + depth * 5) * S).astype(int)
    ix = np.clip(xx.astype(int) + shift, 0, W - 1)
    refl = blur(px, 0.8 * S)[iy, ix]
    bands = 0.78 + 0.22 * (np.sin(Y * 2.0 + rip * 5) > -0.3)
    keep = (0.72 - 0.38 * depth) * bands
    deep = ramp(depth, [(0, "#0C0A26"), (1, "#070618")])
    water = refl * keep[..., None] + deep * (1 - keep[..., None])
    water = np.minimum(water, refl * 0.92 + 0.004)
    water = strokes(water, wm, S, lambda x, y: 0.0, int(3000 * S * S), length=(6, 14), width=(0.8, 1.4), jitter=0.06, seed=9)
    px = px * (1 - wm[..., None]) + water * wm[..., None]
    edge = np.exp(-((Y - WATER - 0.5) / 0.7) ** 2)
    px = screen(px, hexc("#8C86C6") * (edge * 0.15)[..., None])
    Yl = px @ RL.LUM
    px = px * np.where(Yl > 0.42, 0.42 / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.26)
    return RL.grain(px, 0.008, seed=47)
