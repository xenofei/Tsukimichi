"""Our own painting for 2-2 "Moonpath on the Bay": Vesper Bay by night, the moon's road of light on the sea.

The moon stands low in the upper left of the sky over the bay (emissive: it is the light itself) and lays its road of
glints on the water, a broken column that widens toward the shore. To the east, Vesper Bay's pier runs out on its posts
with three lamps, a ferry is moored alongside, and the town's flat roofs and domes stand on the shore with lit windows;
to the west, a dark headland closes the bay. Stars thin out toward the moon.

FEATURES (board units): the glint rows of the moonpath, the pier's deck and lamps, the ferry's hull and masts, the
town's roofline and the headland.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

HORIZON = 300.0
MOON = (292, 128, 30)
PATH_X = 292.0
DECK_Y = 430.0
PIER = (548, 740)
LAMPS = [(574, 396), (628, 396), (682, 396)]
HULL = [(590, 404), (604, 420), (640, 428), (686, 426), (716, 412)]
MASTS = [(626, 404, 262), (676, 404, 286)]
TOWN = [(540, 300), (556, 286), (580, 286), (584, 272), (606, 272), (612, 282), (640, 282), (648, 262), (664, 262),
        (668, 278), (700, 278), (706, 268), (730, 268), (740, 300)]
DOMES = [(618, 282, 10), (696, 278, 9)]
HEADLAND = [(0, 268), (60, 266), (120, 276), (170, 292), (200, 300)]

FEATURES = {
    "moonpath": {"axis": PATH_X, "rows": [[334, 1, 0], [366, 2, 34], [400, 2, 44], [436, 3, 40], [474, 3, 46], [510, 3, 52]]},
    "pier deck": {"points": [[PIER[0], DECK_Y], [PIER[1] - 20, DECK_Y]]},
    "lamps": [list(p) for p in LAMPS],
    "hull": {"points": [list(p) for p in HULL]},
    "masts": [[x, top, foot] for (x, foot, top) in MASTS],
    "town": {"points": [list(p) for p in TOWN]},
    "headland": {"points": [list(p) for p in HEADLAND]},
}


def _poly(W, H, S, pts, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    ImageDraw.Draw(im).polygon([(x * S * ss, y * S * ss) for (x, y) in pts], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    mx, my, mr = MOON
    # ---- the sky: a rose dusk, paling toward the horizon and round the moon (the jewel keeps it rose)
    sky = ramp(np.clip(Y / HORIZON, 0, 1), [(0, "#10081A"), (0.5, "#24122E"), (1.0, "#46243E")])
    d_moon = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    sky = screen(sky, hexc("#C8C8F2") * (np.exp(-(d_moon / 150) ** 2) * 0.20 + np.exp(-(d_moon / 520) ** 2) * 0.07)[..., None])
    sky = strokes(sky, (Y < HORIZON).astype(np.float32), S, lambda x, y: 0.1 * math.sin(x / 80), int(4000 * S * S),
                  length=(10, 22), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=12)
    px = RL.stars(sky, S, 700, 51, (0, 0, W, (HORIZON - 4) * S),
                  avoid=lambda x, y: float(np.clip((math.hypot(x / S - mx, y / S - my) - 70) / 160, 0, 1)), bright=0.5)
    px, _ = RL.moon_emissive(px, S, mx, my, mr, seed=5)
    # ---- the sea: dark, its ripples catching the sky; the moon's road of glints, widening toward the shore
    depth = np.clip((Y - HORIZON) / (600 - HORIZON), 0, 1)
    sea = ramp(depth, [(0, "#25234E"), (0.25, "#141434"), (1, "#080820")])
    rip = fbm(H, W, 3.0 * S, 3, 23)
    sea = sea * (0.86 + 0.24 * rip)[..., None]
    half = 6 + 70 * depth ** 1.1                                     # the road's half-width at each depth
    road = np.exp(-((X - PATH_X) / np.maximum(half, 1)) ** 2)
    lines = smooth(0.15, 0.6, np.sin(Y * (2.6 - 1.4 * depth) + rip * 5.0)) * smooth(0.35, 0.7, fbm(H, W, 6 * S, 3, 29))
    glint = road * lines * (0.85 - 0.35 * depth)
    sea = screen(sea, hexc("#D8DCFF") * (glint * 0.55)[..., None])
    sea = screen(sea, hexc("#8E92D8") * (road * 0.10)[..., None])
    sea = strokes(sea, (Y >= HORIZON).astype(np.float32), S, lambda x, y: 0.0, int(4000 * S * S), length=(6, 16),
                  width=(0.8, 1.6), jitter=0.08, seed=8)
    sea_m = np.clip((Y - HORIZON) * S + 0.5, 0, 1)
    px = px * (1 - sea_m[..., None]) + sea * sea_m[..., None]
    # ---- the headland to the west and the town to the east, dark on the horizon, the town's windows lit
    land = np.maximum(_poly(W, H, S, HEADLAND + [(200, 304), (0, 304)]),
                      _poly(W, H, S, TOWN + [(800, 300), (800, 304), (540, 304)]))
    for (cx, cy, r) in DOMES:
        land = np.maximum(land, np.clip((r - np.sqrt((X - cx) ** 2 + (Y - cy) ** 2)) * S + 0.5, 0, 1) * (Y < cy + 0.5))
    px = px * (1 - land[..., None]) + hexc("#0A0A1E") * land[..., None]
    rimm = np.clip(land - np.roll(np.roll(land, int(S), 1), int(S), 0), 0, 1)
    px = screen(px, hexc("#A8A6E0") * (rimm * 0.35)[..., None])
    for (wx, wy) in ((566, 294), (594, 290), (626, 290), (656, 276), (684, 288), (718, 284)):
        px = screen(px, hexc("#FFB45E") * (np.exp(-(((X - wx) / 1.6) ** 2 + ((Y - wy) / 2.2) ** 2)) * 0.8
                                          + np.exp(-(((X - wx) / 6) ** 2 + ((Y - wy) / 6) ** 2)) * 0.10)[..., None])
    # the lamps' reflections on the water under the pier (the board's warm note)
    for (lx, ly) in LAMPS:
        refl = np.exp(-((X - lx) / 3.0) ** 2) * smooth(DECK_Y + 4, DECK_Y + 14, Y) * smooth(DECK_Y + 90, DECK_Y + 30, Y)
        refl = refl * smooth(0.3, 0.7, np.sin(Y * 2.2 + rip * 4) * 0.5 + 0.5)
        px = screen(px, hexc("#FFB45E") * (refl * 0.25)[..., None])
    # ---- the pier: deck and posts; the moored ferry: hull and two masts with their yards; all dark, rim-lit
    pier = np.zeros((H, W), np.float32)
    pier = np.maximum(pier, np.clip((3.5 - np.abs(Y - DECK_Y - 1)) * S, 0, 1) * (X > PIER[0]) * (X < 800))
    for k in range(int((800 - PIER[0]) / 36) + 1):
        px_ = PIER[0] + 8 + k * 36
        pier = np.maximum(pier, np.clip((2.2 - np.abs(X - px_)) * S, 0, 1) * (Y > DECK_Y) * (Y < DECK_Y + 40))
    for (lx, ly) in LAMPS:
        pier = np.maximum(pier, np.clip((1.3 - np.abs(X - lx)) * S, 0, 1) * (Y > ly + 6) * (Y < DECK_Y))
    hull = _poly(W, H, S, HULL + [(716, 398), (590, 396)])
    for (mx_, foot, top) in MASTS:
        hull = np.maximum(hull, np.clip((1.6 - np.abs(X - mx_)) * S, 0, 1) * (Y > top) * (Y < foot))
        hull = np.maximum(hull, np.clip((1.2 - np.abs(Y - top - 22)) * S, 0, 1) * (np.abs(X - mx_) < 26))
    dark = np.maximum(pier, hull)
    px = px * (1 - dark[..., None]) + hexc("#08081A") * dark[..., None]
    rimm = np.clip(dark - np.roll(np.roll(dark, int(S), 1), int(S), 0), 0, 1)
    px = screen(px, hexc("#A8A6E0") * (rimm * 0.4)[..., None])
    for (lx, ly) in LAMPS:
        px = screen(px, hexc("#FFB45E") * (np.exp(-(((X - lx) / 2.2) ** 2 + ((Y - ly - 4) / 2.8) ** 2)) * 0.85
                                          + np.exp(-(((X - lx) / 10) ** 2 + ((Y - ly - 4) / 10) ** 2)) * 0.12)[..., None])
    Yl = px @ RL.LUM
    lim = np.where(d_moon < mr + 40, 1.0, 0.42)
    px = px * np.where(Yl > lim, lim / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.26)
    return RL.grain(px, 0.008, seed=43)
