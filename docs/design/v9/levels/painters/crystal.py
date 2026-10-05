"""Our own painting for 1-4 "The Crystal's Call": a constellation in the shape of a crystal over the Waking Sands.

Minfilia hears the Mother Crystal through the Echo; here its call is written in the night sky: eight stars in the
figure of a tall faceted crystal (two tips, four shoulders, the front ridge's two ends), joined by faint engraved gilt
lines and, fainter still, an old star atlas's drawing of its facets. A Milky Way crosses the sky behind it with its
dust lanes; below, the dark dunes of Western Thanalan and the Waking Sands' low house with its dome, its lit windows and
two date palms. The moon's glow comes from beyond the upper left.

FEATURES (board units): the stars, the figure's lines, the Milky Way's spine, the horizon and the house's roofline.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

STARS = {"top": (440, 186, 1), "left shoulder": (356, 252, 2), "right shoulder": (524, 252, 2), "ridge top": (440, 244, 3),
         "ridge foot": (440, 412, 3), "left hip": (366, 414, 2), "right hip": (514, 414, 2), "foot": (440, 478, 1)}
LINES = [("top", "left shoulder"), ("top", "right shoulder"), ("top", "ridge top"), ("left shoulder", "ridge top"),
         ("right shoulder", "ridge top"), ("left shoulder", "left hip"), ("right shoulder", "right hip"),
         ("ridge top", "ridge foot"), ("left hip", "ridge foot"), ("right hip", "ridge foot"), ("left hip", "foot"),
         ("right hip", "foot"), ("ridge foot", "foot")]
HORIZON = [(0, 506), (90, 500), (180, 494), (260, 498), (330, 506), (420, 510), (520, 504), (600, 496), (690, 500), (800, 506)]
HOUSE = [(120, 497), (120, 470), (150, 470), (150, 458), (196, 458), (196, 470), (232, 470), (232, 497)]
DOME = (173, 458, 16)
MILKY = [(90, 560), (260, 420), (430, 300), (600, 190), (760, 90)]

FEATURES = {
    "stars": [[x, y] for (x, y, _m) in STARS.values()],
    "lines": [[list(STARS[a][:2]), list(STARS[b][:2])] for a, b in LINES],
    "milky way": {"points": [list(p) for p in MILKY]},
    "crystal": [list(STARS[k][:2]) for k in ("top", "right shoulder", "right hip", "foot", "left hip", "left shoulder")],
    "horizon": {"points": [list(p) for p in HORIZON]},
}


def _lines_mask(W, H, S, segs, width, shorten=7.0, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    dr = ImageDraw.Draw(im)
    for (x0, y0), (x1, y1) in segs:
        L_ = math.hypot(x1 - x0, y1 - y0)
        ux, uy = (x1 - x0) / L_, (y1 - y0) / L_
        dr.line([((x0 + ux * shorten) * S * ss, (y0 + uy * shorten) * S * ss),
                 ((x1 - ux * shorten) * S * ss, (y1 - uy * shorten) * S * ss)], fill=255, width=max(1, int(round(width * S * ss))))
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def point_star(px, X, Y, x, y, mag, col):
    d2 = (X - x) ** 2 + (Y - y) ** 2
    core = {1: 1.6, 2: 1.25, 3: 1.0}[mag]
    a = {1: 1.0, 2: 0.85, 3: 0.7}[mag]
    g = np.exp(-d2 / (2 * core * core)) * a + np.exp(-d2 / (2 * (core * 5) ** 2)) * a * 0.22
    return screen(px, hexc(col) * g[..., None])


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    sky = ramp(np.clip(Y / 500, 0, 1), [(0, "#06061A"), (0.4, "#0F0D30"), (0.8, "#1C1846"), (1.0, "#2A2252")])
    d_moon = np.sqrt((X + 80) ** 2 + (Y + 90) ** 2)
    sky = screen(sky, hexc("#C4C0EE") * (np.exp(-(d_moon / 380) ** 2) * 0.15 + np.exp(-(d_moon / 900) ** 2) * 0.05)[..., None])
    # the Milky Way, lower left to upper right, mottled, its dust lanes darkening only its own light
    P = np.asarray(RL.catmull(MILKY, 16), np.float32)
    dist = np.full((H, W), 1e9, np.float32)
    for k in range(len(P) - 1):
        ax, ay = P[k]
        bx, by = P[k + 1]
        t = np.clip(((X - ax) * (bx - ax) + (Y - ay) * (by - ay)) / max((bx - ax) ** 2 + (by - ay) ** 2, 1e-6), 0, 1)
        dist = np.minimum(dist, np.sqrt((X - ax - (bx - ax) * t) ** 2 + (Y - ay - (by - ay) * t) ** 2))
    band = np.exp(-(dist / 64) ** 2)
    mott = fbm(H, W, 24 * S, 5, 3)
    dust = smooth(0.48, 0.62, fbm(H, W, 16 * S, 4, 9)) * np.exp(-(dist / 22) ** 2)
    glow = band * (0.45 + 0.55 * mott) * (1 - blur(dust, 2.5 * S) * 0.75) * smooth(520, 420, Y)
    sky = screen(sky, hexc("#8C88CC") * (glow * 0.24)[..., None])
    sky = strokes(sky, (Y < 510).astype(np.float32), S, lambda x, y: -0.6 + 0.2 * math.sin(x / 90), int(6000 * S * S),
                  length=(8, 18), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=6)
    px = RL.stars(sky, S, 1100, 33, (0, 0, W, 505 * S),
                  avoid=lambda x, y: 0.35 + 0.65 * float(np.exp(-(dist[min(H - 1, int(y)), min(W - 1, int(x))] / 90) ** 2)), bright=0.5)
    # the figure: the atlas's faint facet drawing (fill hatching inside the crystal), the gilt lines, the stars
    pts = [STARS[k][:2] for k in ("top", "right shoulder", "right hip", "foot", "left hip", "left shoulder")]
    im = Image.new("L", (W * 3, H * 3), 0)
    ImageDraw.Draw(im).polygon([(x * S * 3, y * S * 3) for (x, y) in pts], fill=255)
    inside = np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255
    hatch = (np.sin((X * 0.8 - Y * 0.5) * 0.9) > 0.85).astype(np.float32) * inside
    px = screen(px, hexc("#7FD8E0") * (blur(hatch, 0.4 * S) * 0.05 + inside * 0.025)[..., None])
    segs = [(STARS[a][:2], STARS[b][:2]) for a, b in LINES]
    px = screen(px, hexc("#D9BE82") * (_lines_mask(W, H, S, segs, 1.1) * 0.40)[..., None])
    for name, (x, y, mag) in STARS.items():
        px = point_star(px, X, Y, x, y, mag, "#E6F6FF")
    # ---- the land: dark dunes along the horizon, lit on their crests from the upper left
    xs = np.array([p[0] for p in HORIZON], np.float32)
    top = np.interp(X[0], xs, np.array([p[1] for p in HORIZON], np.float32)) + (fbm(1, W, 16 * S, 3, 5)[0] - 0.5) * 4
    land = np.clip((Y - top[None, :]) * S + 0.5, 0, 1)
    depth = np.clip((Y - top[None, :]) / 80, 0, 1)
    ground = ramp(depth, [(0, "#2C2650"), (0.2, "#141230"), (1, "#08071A")])
    ground = screen(ground, hexc("#9C96D2") * (np.exp(-((Y - top[None, :] - 1.0) / 1.3) ** 2) * 0.25)[..., None])
    px = px * (1 - land[..., None]) + ground * land[..., None]
    # the Waking Sands: a low sandstone house, its dome, two lit windows, two date palms; a silhouette on the dunes
    house = np.zeros((H, W), np.float32)
    im = Image.new("L", (W * 3, H * 3), 0)
    dr = ImageDraw.Draw(im)
    dr.polygon([(x * S * 3, y * S * 3) for (x, y) in HOUSE], fill=255)
    cx, cy, r = DOME
    dr.pieslice([(cx - r) * S * 3, (cy - r) * S * 3, (cx + r) * S * 3, (cy + r) * S * 3], 180, 360, fill=255)
    house = np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255
    px = px * (1 - house[..., None]) + hexc("#0A0918") * house[..., None]
    rim = np.clip(house - np.roll(np.roll(house, int(S), 1), int(S), 0), 0, 1)
    px = screen(px, hexc("#A69EDA") * (rim * 0.4)[..., None])
    for (wx, wy) in ((136, 482), (214, 482)):
        win = np.exp(-(((X - wx) / 2.4) ** 2 + ((Y - wy) / 3.4) ** 2))
        px = screen(px, hexc("#FFB45E") * (win * 0.75 + np.exp(-(((X - wx) / 9) ** 2 + ((Y - wy) / 9) ** 2)) * 0.12)[..., None])
    for (bx, by, h) in ((96, 500, 62), (256, 500, 54)):
        trunk = np.clip((1.6 - np.abs(X - bx - (by - Y) * 0.08)) * S, 0, 1) * (Y < by) * (Y > by - h)
        crown = np.zeros((H, W), np.float32)
        for k in range(7):
            a = math.radians(-160 + k * 23)
            for t in np.linspace(0, 1, 18):
                fx = bx + (by - h - by) * 0.08 * -1 + math.cos(a) * 26 * t
                fy = by - h + math.sin(a) * 26 * t + 14 * t * t
                crown = np.maximum(crown, np.exp(-(((X - fx) ** 2 + (Y - fy) ** 2) / (1.6 * (1.4 - t)) ** 2)))
        sil = np.clip(np.maximum(trunk, crown), 0, 1)
        px = px * (1 - sil[..., None]) + hexc("#07061A") * sil[..., None]
    Yl = px @ RL.LUM
    px = px * np.where(Yl > 0.42, 0.42 / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.26)
    return RL.grain(px, 0.008, seed=41)
