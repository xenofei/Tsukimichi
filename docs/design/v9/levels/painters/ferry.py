"""Our own painting for 2-4 "The Ferry Under Sail": the Vesper Bay ferry mid-strait by night, bound for Limsa Lominsa.

The ferry runs east under full sail across the Strait of Merlthor: a dark hull with lit stern windows, two masts with
square sails pale in the moonlight, a jib from the foremast to the bowsprit, a lantern at the stern and one at the bow.
The moon stands high in the upper left behind thin cloud; the far coasts close the strait low on either side (Thanalan
to the west, La Noscea to the east), and the sea runs in long swells with a wake behind the stern.

FEATURES (board units): the deck line, the masts, the yards, the sails' leeches, the jib's stay, the lanterns, the
pennants, the wake and the two coasts.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import fbm, hexc, ramp, screen, smooth

HORIZON = 336.0
MOON = (150, 104, 24)
HULL_TOP = [(244, 386), (290, 396), (340, 401), (400, 404), (470, 402), (540, 396), (590, 386)]
HULL_BOTTOM = [(600, 400), (576, 426), (530, 446), (420, 452), (310, 448), (262, 432), (238, 404)]
MASTS = [(340, 186, 401), (470, 168, 402)]          # x, top, foot
YARDS = [(340, 222, 40), (340, 300, 58), (470, 206, 42), (470, 286, 60)]   # x, y, half-width
BOWSPRIT = ((588, 388), (650, 368))
LANTERNS = [(246, 372), (612, 362)]
WAKE = [(232, 440), (176, 452), (112, 462)]
WEST_COAST = [(0, 318), (52, 312), (110, 318), (170, 328), (214, 336)]
EAST_COAST = [(560, 336), (600, 326), (660, 316), (724, 310), (800, 314)]


def _sails():
    """Each square sail as a polygon: its yard above, its foot below, bellied forward (east)."""
    out = []
    for (x, y, h) in YARDS:
        depth = 64 if h > 50 else 54
        out.append([(x - h, y), (x + h, y), (x + h + 8, y + depth * 0.55), (x + h + 2, y + depth), (x - h + 4, y + depth),
                    (x - h + 6, y + depth * 0.5)])
    return out


def _jib():
    (mx, top, _) = MASTS[1]
    return [(mx + 4, top + 16), BOWSPRIT[1], (BOWSPRIT[0][0] - 10, BOWSPRIT[0][1] + 2), (mx + 30, top + 120)]


FEATURES = {
    "deck": {"points": [list(p) for p in HULL_TOP]},
    "masts": [list(m) for m in MASTS],
    "yards": [list(y) for y in YARDS],
    "leeches": [[x + h + 8, y + (64 if h > 50 else 54) * 0.55] for (x, y, h) in YARDS],
    "stay": {"points": [[MASTS[1][0] + 4, MASTS[1][1] + 16], list(BOWSPRIT[1])]},
    "lanterns": [list(p) for p in LANTERNS],
    "pennants": [[x, top - 16] for (x, top, _) in MASTS],
    "wake": {"points": [list(p) for p in WAKE]},
    "west coast": {"points": [list(p) for p in WEST_COAST]},
    "east coast": {"points": [list(p) for p in EAST_COAST]},
    # the whole ship, sails and hull, as one envelope (the dress keeps its colour off it)
    "ship": {"points": [[236, 400], [262, 206], [330, 176], [470, 150], [560, 236], [662, 372], [600, 402], [576, 428],
                        [530, 448], [420, 454], [300, 450], [260, 434]]},
}


def _poly(W, H, S, pts, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    ImageDraw.Draw(im).polygon([(x * S * ss, y * S * ss) for (x, y) in pts], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def _line(X, Y, S, a, b, w):
    (x0, y0), (x1, y1) = a, b
    dx, dy = x1 - x0, y1 - y0
    L2 = dx * dx + dy * dy
    t = np.clip(((X - x0) * dx + (Y - y0) * dy) / L2, 0, 1)
    d = np.sqrt((X - x0 - t * dx) ** 2 + (Y - y0 - t * dy) ** 2)
    return np.clip((w - d) * S + 0.5, 0, 1)


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    mx, my, mr = MOON
    # ---- the sky: deep indigo, paler toward the horizon and round the moon, a few long clouds lit from beneath
    sky = ramp(np.clip(Y / HORIZON, 0, 1), [(0, "#06071A"), (0.55, "#10153A"), (1.0, "#232A5C")])
    d_moon = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    sky = screen(sky, hexc("#C8CCF4") * (np.exp(-(d_moon / 140) ** 2) * 0.18 + np.exp(-(d_moon / 480) ** 2) * 0.06)[..., None])
    cloud = smooth(0.52, 0.72, fbm(H, W, 60 * S, 4, 41) * 0.7 + 0.3 * np.exp(-((Y - 150) / 50) ** 2)) * (Y < HORIZON - 40)
    sky = screen(sky * (1 - 0.35 * cloud[..., None]), hexc("#5A5E9A") * (cloud * np.exp(-(d_moon / 380) ** 2) * 0.35)[..., None])
    sky = strokes(sky, (Y < HORIZON).astype(np.float32), S, lambda x, y: 0.05 * math.sin(x / 90), int(4000 * S * S),
                  length=(12, 24), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=61)
    px = RL.stars(sky, S, 650, 63, (0, 0, W, (HORIZON - 6) * S),
                  avoid=lambda x, y: float(np.clip((math.hypot(x / S - mx, y / S - my) - 60) / 160, 0, 1)), bright=0.45)
    px, _ = RL.moon_emissive(px, S, mx, my, mr, seed=9)
    # ---- the sea: long swells, darker toward the viewer, the moon's light broken on them beneath the moon
    depth = np.clip((Y - HORIZON) / (600 - HORIZON), 0, 1)
    sea = ramp(depth, [(0, "#22285A"), (0.3, "#121738"), (1, "#070A1E")])
    rip = fbm(H, W, 3.0 * S, 3, 67)
    swell = 0.5 + 0.5 * np.sin(Y * (0.55 - 0.3 * depth) + 0.012 * X + rip * 3.0)
    sea = sea * (0.82 + 0.3 * swell * (0.6 + 0.4 * rip))[..., None]
    road = np.exp(-((X - mx - 30) / (10 + 60 * depth)) ** 2) * smooth(0.55, 0.9, swell) * (0.6 - 0.3 * depth)
    sea = screen(sea, hexc("#C8CCF4") * (road * 0.35)[..., None])
    sea = strokes(sea, (Y >= HORIZON).astype(np.float32), S, lambda x, y: 0.0, int(4000 * S * S), length=(8, 18),
                  width=(0.8, 1.6), jitter=0.08, seed=69)
    sea_m = np.clip((Y - HORIZON) * S + 0.5, 0, 1)
    px = px * (1 - sea_m[..., None]) + sea * sea_m[..., None]
    # ---- the two far coasts, low and dark on the horizon
    land = np.maximum(_poly(W, H, S, WEST_COAST + [(220, HORIZON + 3), (0, HORIZON + 3)]),
                      _poly(W, H, S, EAST_COAST + [(800, HORIZON + 3), (556, HORIZON + 3)]))
    px = px * (1 - land[..., None]) + hexc("#0B0D24") * land[..., None]
    for (wx, wy) in ((40, 322), (88, 318), (690, 318), (742, 314)):
        px = screen(px, hexc("#FFB45E") * (np.exp(-(((X - wx) / 1.4) ** 2 + ((Y - wy) / 1.6) ** 2)) * 0.6)[..., None])
    # ---- the wake: churned water trailing west from the stern
    wake = np.zeros((H, W), np.float32)
    for (a, b) in zip(WAKE[:-1], WAKE[1:]):
        wake = np.maximum(wake, _line(X, Y, S, a, b, 10))
    wake = wake * smooth(0.4, 0.8, fbm(H, W, 4 * S, 3, 71))
    px = screen(px, hexc("#9CA2DC") * (RL.blur(wake, 2 * S) * 0.22)[..., None])
    # ---- the ferry: hull, masts, yards, sails pale in the moonlight, the jib, the bowsprit
    hull = _poly(W, H, S, HULL_TOP + HULL_BOTTOM)
    rig = np.zeros_like(hull)
    for (x, top, foot) in MASTS:
        rig = np.maximum(rig, np.clip((2.0 - np.abs(X - x)) * S, 0, 1) * (Y > top) * (Y < foot))
    for (x, y, h) in YARDS:
        rig = np.maximum(rig, _line(X, Y, S, (x - h - 4, y), (x + h + 4, y), 1.6))
    rig = np.maximum(rig, _line(X, Y, S, BOWSPRIT[0], BOWSPRIT[1], 2.2))
    rig = np.maximum(rig, _line(X, Y, S, (MASTS[0][0], MASTS[0][1] + 4), (MASTS[1][0], MASTS[1][1] + 30), 0.8))
    rig = np.maximum(rig, _line(X, Y, S, (MASTS[0][0], MASTS[0][1] + 4), (244, 380), 0.8))
    sails = np.zeros_like(hull)
    lit = np.zeros_like(hull)
    # the sails: bellied by the wind, so each is brightest across its middle and toward the moon (upper left), dark at
    # its head and foot and along its lee edge; the seams of the cloths run faintly down it
    for poly, (x, y, h) in zip(_sails(), YARDS):
        m = _poly(W, H, S, poly)
        depth = 64 if h > 50 else 54
        u = np.clip((X - (x - h)) / (2 * h), 0, 1)
        v = np.clip((Y - y) / depth, 0, 1)
        tone = 0.30 + 0.55 * np.sin(np.pi * (0.15 + 0.7 * v)) * (1 - 0.55 * u) + 0.12 * (1 - u)
        sails, lit = np.maximum(sails, m), np.where(m > 0, tone, lit)
    jib = _poly(W, H, S, _jib())
    sails = np.maximum(sails, jib)
    lit = np.where(jib > 0, 0.25 + 0.35 * np.clip(1 - (X - 480) / 170, 0, 1), lit)
    cloth = ramp(np.clip(lit, 0, 1), [(0, "#2E3060"), (0.6, "#7476AC"), (1, "#A2A4D4")])
    cloth = cloth * (0.95 + 0.05 * np.sin(X * 0.9))[..., None] * (0.92 + 0.08 * fbm(H, W, 8 * S, 3, 73))[..., None]
    px = px * (1 - sails[..., None]) + cloth * sails[..., None]
    dark = np.maximum(hull, rig * (1 - sails * 0.6))
    body = ramp(np.clip((Y - 380) / 70, 0, 1), [(0, "#14142E"), (1, "#08081A")])
    px = px * (1 - dark[..., None]) + body * dark[..., None]
    rimm = np.clip(dark - np.roll(np.roll(dark, int(S), 1), int(S), 0), 0, 1)
    px = screen(px, hexc("#A8A6E0") * (rimm * 0.4)[..., None])
    # lit stern windows and portholes, the lanterns, the hull's reflection of them in the sea
    for k in range(6):
        wx, wy = 296 + k * 46, 424
        px = screen(px, hexc("#FFB45E") * (np.exp(-(((X - wx) / 1.8) ** 2 + ((Y - wy) / 1.8) ** 2)) * 0.7)[..., None])
    for (lx, ly) in LANTERNS:
        px = screen(px, hexc("#FFB45E") * (np.exp(-(((X - lx) / 2.2) ** 2 + ((Y - ly) / 2.8) ** 2)) * 0.85
                                          + np.exp(-(((X - lx) / 10) ** 2 + ((Y - ly) / 10) ** 2)) * 0.12)[..., None])
        refl = np.exp(-((X - lx) / 3.0) ** 2) * smooth(456, 466, Y) * smooth(540, 490, Y) * smooth(0.3, 0.7, swell)
        px = screen(px, hexc("#FFB45E") * (refl * 0.18)[..., None])
    Yl = px @ RL.LUM
    lim = np.where(d_moon < mr + 40, 1.0, 0.42)
    px = px * np.where(Yl > lim, lim / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.26)
    return RL.grain(px, 0.008, seed=79)
