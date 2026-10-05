"""Pilot level exp-p2 "The Ferry in the Stars" (technique: a constellation), scene. Painted in code.

The Far Shore's later, violet hour over open sea. High in the sky stands the constellation sailors of the Far Shore
steer by: the Lantern Ferry, a small boat with a lantern on its stern post (the expansion's own bucket, set among the
stars). Its stars are joined by faint engraved gilt lines and, fainter still, the old star atlas's drawing of the boat,
so the figure is there before a single peg lights. The Milky Way crosses the sky with its dust lanes; the moon's glow
comes from beyond the upper left; on the horizon, the far shore's headland with its gate; on the sea, the sky's broken
reflection. A painterly stroke pass (brush.py) works the sky, the Milky Way and the sea.

Everything is in the engine's 800 x 600 units; the layout (level_constellation.py) puts a peg on every star of the
figure and dots along its lines.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

from brush import flow_const, strokes
from layout import smooth_path
from rich_lib import LUM, OUT_SCENES, blur, fbm, grain, hexc, ramp, save_rgb, screen, smooth, vignette

HORIZON = 462.0
# the Lantern Ferry: its stars (name, x, y, magnitude 1 brightest) and the lines between them
STARS = {
    "bow": (178, 286, 2), "fore keel": (262, 344, 3), "mid keel": (372, 362, 2), "aft keel": (480, 350, 3),
    "stern": (566, 300, 2), "gunwale": (372, 304, 3), "post": (590, 226, 3), "post head": (600, 148, 2),
    "arm": (556, 150, 4), "lantern": (548, 190, 1), "ferryman": (430, 250, 2), "oar": (300, 236, 3),
}
LINES = [("bow", "fore keel"), ("fore keel", "mid keel"), ("mid keel", "aft keel"), ("aft keel", "stern"),
         ("bow", "gunwale"), ("gunwale", "stern"), ("stern", "post"), ("post", "post head"), ("post head", "arm"),
         ("arm", "lantern"), ("ferryman", "gunwale"), ("ferryman", "oar")]


def star_xy(name):
    x, y, _ = STARS[name]
    return float(x), float(y)


def lines_mask(W, H, S, width=1.1, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    dr = ImageDraw.Draw(im)
    for a, b in LINES:
        (x0, y0), (x1, y1) = star_xy(a), star_xy(b)
        # the line stops short of each star, as an atlas draws it
        L_ = math.hypot(x1 - x0, y1 - y0)
        ux, uy = (x1 - x0) / L_, (y1 - y0) / L_
        dr.line([((x0 + ux * 6) * S * ss, (y0 + uy * 6) * S * ss), ((x1 - ux * 6) * S * ss, (y1 - uy * 6) * S * ss)],
                fill=255, width=max(1, int(round(width * S * ss))))
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def atlas_drawing(W, H, S, ss=3):
    """The old atlas's faint drawing of the ferry: hull, post, lantern, the ferryman, in fine engraved strokes."""
    im = Image.new("L", (W * ss, H * ss), 0)
    dr = ImageDraw.Draw(im)
    def path(pts, w=1.0):
        P_ = smooth_path(pts, 12)
        dr.line([(x * S * ss, y * S * ss) for (x, y) in P_], fill=255, width=max(1, int(round(w * S * ss))))
    path([(160, 278), (210, 330), (300, 362), (380, 372), (470, 362), (540, 330), (580, 292)], 1.2)     # keel
    path([(166, 282), (260, 300), (372, 306), (480, 302), (574, 290)], 1.0)                             # gunwale
    for k in range(9):                                                                                  # planking
        t = (k + 1) / 10
        x = 180 + 380 * t
        path([(x, 300 + 4 * math.sin(t * math.pi)), (x - 4, 340 + 22 * math.sin(t * math.pi))], 0.6)
    path([(568, 296), (586, 240), (594, 190), (600, 146)], 1.1)                                         # post
    for (cx, cy, rx, ry) in ((548, 190, 9, 13),):                                                       # lantern
        dr.ellipse([(cx - rx) * S * ss, (cy - ry) * S * ss, (cx + rx) * S * ss, (cy + ry) * S * ss], outline=255,
                   width=max(1, int(round(0.9 * S * ss))))
    path([(430, 250), (436, 276), (432, 302)], 1.0)                                                     # ferryman
    dr.ellipse([(424) * S * ss, (232) * S * ss, (440) * S * ss, (248) * S * ss], outline=255, width=max(1, int(round(0.9 * S * ss))))
    path([(300, 236), (360, 266), (420, 300), (470, 340)], 0.9)                                         # oar
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def point_star(px, X, Y, x, y, mag, col="#E8ECFF", S=1):
    """A star: a tight core and a soft glow, sized by magnitude (1 brightest). No rays."""
    d2 = (X - x) ** 2 + (Y - y) ** 2
    core = {1: 1.5, 2: 1.15, 3: 0.9, 4: 0.75}[mag]
    a = {1: 1.0, 2: 0.85, 3: 0.65, 4: 0.5}[mag]
    g = np.exp(-d2 / (2 * core * core)) * a + np.exp(-d2 / (2 * (core * 5) ** 2)) * a * 0.22
    return screen(px, hexc(col) * g[..., None])


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    rng = np.random.default_rng(12)
    # ---- the violet sky: zenith to horizon, the moon's glow beyond the upper left
    sky = ramp(np.clip(Y / HORIZON, 0, 1), [(0, "#07061A"), (0.35, "#110E30"), (0.75, "#221C4A"), (1.0, "#352C5E")])
    d_moon = np.sqrt((X + 70) ** 2 + (Y + 80) ** 2)
    sky = screen(sky, hexc("#C4C0EE") * (np.exp(-(d_moon / 360) ** 2) * 0.16 + np.exp(-(d_moon / 900) ** 2) * 0.05)[..., None])
    # the Milky Way: a band from lower left to upper right, mottled, with dark dust lanes along its spine
    ang = math.radians(-28)
    u = (X - 400) * math.cos(ang) + (Y - 300) * math.sin(ang)
    v = -(X - 400) * math.sin(ang) + (Y - 300) * math.cos(ang) + 30 * np.sin(u / 160)
    band = np.exp(-(v / 70) ** 2)
    mott = fbm(H, W, 26 * S, 5, 3)
    dust = smooth(0.48, 0.62, fbm(H, W, 18 * S, 4, 9)) * np.exp(-((v + 8) / 22) ** 2)
    glow = band * (0.45 + 0.55 * mott) * smooth(HORIZON, HORIZON - 120, Y)
    # the dust lanes darken only the band's own light (never the sky behind it), and fade as the band fades
    glow = glow * (1 - blur(dust, 2.5 * S) * 0.75)
    sky = screen(sky, hexc("#8E86C8") * (glow * 0.26)[..., None])
    sky = screen(sky, hexc("#D6CFF0") * (np.clip(glow - 0.45, 0, 1) * 0.30)[..., None])
    sky_m = (Y < HORIZON).astype(np.float32)
    sky = strokes(sky, sky_m, S, lambda x, y: ang + 0.25 * math.sin(x / 90), int(7000 * S * S), length=(8, 18),
                  width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=4)
    # the field of faint stars, thicker along the Milky Way
    n = int(1400)
    xs = rng.uniform(0, 800, n)
    ys = rng.uniform(0, HORIZON - 6, n)
    keep = rng.random(n) < (0.35 + 0.65 * np.exp(-(((-(xs - 400) * math.sin(ang) + (ys - 300) * math.cos(ang))) / 80) ** 2))
    px = sky
    for x, y in zip(xs[keep], ys[keep]):
        r = 0.35 + 0.4 * rng.random() ** 2
        a = 0.10 + 0.55 * rng.random() ** 3
        x0, x1 = int(max(0, (x - 3) * S)), int(min(W, (x + 3) * S + 1))
        y0, y1 = int(max(0, (y - 3) * S)), int(min(H, (y + 3) * S + 1))
        g = np.exp(-((X[y0:y1, x0:x1] - x) ** 2 + (Y[y0:y1, x0:x1] - y) ** 2) / (2 * r * r)) * a
        c = hexc("#E2E4FF" if rng.random() < 0.8 else "#FFE6C8")
        px[y0:y1, x0:x1] = screen(px[y0:y1, x0:x1], c * g[..., None])
    # ---- the constellation: the atlas drawing (faintest), the lines (faint gilt), its stars
    px = screen(px, hexc("#B8A878") * (atlas_drawing(W, H, S) * 0.13)[..., None])
    px = screen(px, hexc("#D9BE82") * (lines_mask(W, H, S) * 0.42)[..., None])
    for name, (x, y, mag) in STARS.items():
        px = point_star(px, X, Y, x, y, mag, "#FFE2B8" if name == "lantern" else "#E8ECFF", S)
    # ---- the far shore: a long low headland on the horizon, the gate on its point, lit on its moon side
    xs_ = (np.arange(W) + 0.5) / S
    land = HORIZON - 4 - 22 * smooth(380, 560, xs_) * (1 - smooth(700, 820, xs_)) - 6 * fbm(1, W, 20 * S, 3, 5)[0] * smooth(380, 460, xs_)
    land = np.where(xs_ < 380, HORIZON - 1.5, land)
    lm = np.clip((Y - land[None, :]) * S + 0.5, 0, 1) * (Y < HORIZON + 0.5)
    px = px * (1 - lm[..., None]) + hexc("#0D0B20") * lm[..., None]
    # the gate: two posts and a lintel, a torii-like gate on the headland's point
    gx, gb = 470.0, HORIZON - 12
    gate = np.zeros((H, W), np.float32)
    for (x0, y0, x1, y1) in ((gx - 9, gb - 26, gx - 7, gb), (gx + 7, gb - 26, gx + 9, gb), (gx - 13, gb - 28, gx + 13, gb - 25.5),
                             (gx - 10, gb - 22, gx + 10, gb - 20.5)):
        gate = np.maximum(gate, np.clip((np.minimum(np.minimum(X - x0, x1 - X), np.minimum(Y - y0, y1 - Y))) * S + 0.5, 0, 1))
    px = px * (1 - gate[..., None]) + hexc("#0A0918") * gate[..., None]
    rim = np.clip(gate - np.roll(np.roll(gate, int(S), 1), int(S), 0), 0, 1) + np.clip(lm - np.roll(lm, int(S), 0), 0, 1) * 0.5
    px = screen(px, hexc("#A69EDA") * (rim * 0.35)[..., None])
    # ---- the sea: the sky's colour darkened, the Milky Way and the bright stars broken into glints
    sea_m = (Y >= HORIZON).astype(np.float32) * (1 - lm)
    depth = np.clip((Y - HORIZON) / (600 - HORIZON), 0, 1)
    sea = ramp(depth, [(0, "#2A2350"), (0.3, "#16123A"), (1, "#0A0820")])
    rip = fbm(H, W, 3.0 * S, 3, 21)
    sea = sea * (0.88 + 0.22 * rip)[..., None]
    # reflections: the sky above the same x, mirrored and broken by horizontal ripples
    refl_y = np.clip(2 * HORIZON - Y, 0, HORIZON - 1)
    iy = np.clip((refl_y * S).astype(int), 0, H - 1)
    shift = (np.sin(Y * 0.9) * (1 + depth * 6) * S).astype(int)
    ix = np.clip(xx.astype(int) + shift, 0, W - 1)
    mirrored = px[iy, ix]
    lines_ = (np.sin(Y * 2.2 + rip * 6) > 0.2).astype(np.float32)
    sea = screen(sea, mirrored * (0.45 * lines_ * (1 - depth * 0.6))[..., None])
    sea = strokes(sea, sea_m, S, flow_const(0.0), int(4000 * S * S), length=(6, 16), width=(0.8, 1.6), jitter=0.08, seed=8)
    px = px * (1 - sea_m[..., None]) + sea * sea_m[..., None]
    # ---- the value ceiling (the board's rule), vignette, grain
    Yl = px @ LUM
    px = px * np.where(Yl > 0.42, 0.42 / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = vignette(px, 0.28)
    return grain(px, 0.008, seed=31)


if __name__ == "__main__":
    for S in (1, 2):
        save_rgb(paint(S), OUT_SCENES / f"exp-p2-lantern-ferry{'@2x' if S == 2 else ''}.png")
    print("ok")
