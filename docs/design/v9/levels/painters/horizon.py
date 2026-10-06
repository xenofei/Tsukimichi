"""Our own painting for 1-2 "Horizon by Night": the Western Thanalan town on its mesa, its roofline against the stars.

Horizon stands on the mesa's rim: flat-roofed sandstone houses, the great water tower on its stilts with a domed cap,
the mine's timber derrick on the west, strings of lanterns slung between the roofs. The mesa's cliff falls away below
the town, lit on its moon-facing ledges; the desert floor beyond is dark. The moon's glow comes from beyond the upper
left; the stars thin toward it.

FEATURES (board units): the roofline (the band the layout traces), the tank's crown, the derrick, the lantern strings,
the cliff's edge.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

RIM = 392.0                                     # the mesa's top, where the houses stand
# houses: (x0, x1, roof y), flat roofs with parapets; the water tower; the derrick
HOUSES = [(96, 150, 352), (150, 196, 366), (238, 300, 340), (300, 344, 358), (352, 412, 332), (412, 452, 350),
          (616, 664, 344), (664, 712, 362)]
TOWER = dict(x=540.0, tank_top=262.0, tank_bot=318.0, half=44.0, stilt_foot=RIM)
DERRICK = dict(x=214.0, top=246.0, half_foot=22.0)
STRINGS = [((150, 352), (238, 340)), ((452, 350), (496, 318)), ((584, 318), (616, 344))]
CLIFF = [(0, 410), (90, 404), (170, 418), (260, 412), (360, 424), (460, 416), (560, 426), (660, 414), (800, 420)]


def roofline():
    """The town's silhouette from west to east, as the layout traces it (just above the roofs)."""
    pts = [(80, RIM - 30)]
    for (x0, x1, y) in HOUSES[:2]:
        pts += [(x0 + 4, y), (x1 - 4, y)]
    pts += [(DERRICK["x"] - 16, 300), (DERRICK["x"], DERRICK["top"]), (DERRICK["x"] + 16, 300)]
    for (x0, x1, y) in HOUSES[2:6]:
        pts += [(x0 + 4, y), (x1 - 4, y)]
    t = TOWER
    pts += [(t["x"] - t["half"], t["tank_top"] + 8), (t["x"], t["tank_top"] - 22), (t["x"] + t["half"], t["tank_top"] + 8)]
    for (x0, x1, y) in HOUSES[6:]:
        pts += [(x0 + 4, y), (x1 - 4, y)]
    return pts


FEATURES = {
    "roofline": {"points": [[x, y] for (x, y) in roofline()]},
    "tank": {"circle": [TOWER["x"], TOWER["tank_top"] + 52, 66], "from": 222, "sweep": 96},
    "derrick": [[DERRICK["x"], DERRICK["top"]]],
    "strings": [[list(a), list(b)] for (a, b) in STRINGS],
    "cliff": {"points": [list(p) for p in CLIFF]},
}


def _poly(W, H, S, pts, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    ImageDraw.Draw(im).polygon([(x * S * ss, y * S * ss) for (x, y) in pts], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    sky = ramp(np.clip(Y / RIM, 0, 1), [(0, "#07061C"), (0.5, "#14103A"), (0.9, "#2C2256"), (1.0, "#382A60")])
    d_moon = np.sqrt((X + 70) ** 2 + (Y + 80) ** 2)
    sky = screen(sky, hexc("#C4C0EE") * (np.exp(-(d_moon / 420) ** 2) * 0.15 + np.exp(-(d_moon / 900) ** 2) * 0.05)[..., None])
    sky = strokes(sky, np.ones((H, W), np.float32), S, lambda x, y: -0.1 + 0.15 * math.sin(x / 100), int(5000 * S * S),
                  length=(10, 20), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=18)
    px = RL.stars(sky, S, 900, 71, (0, 0, W, (RIM - 40) * S),
                  avoid=lambda x, y: float(np.clip((math.hypot(x / S + 70, y / S + 80) - 200) / 300, 0.3, 1)), bright=0.5)
    # ---- the mesa: the cliff face below the rim, ledges lit toward the moon; the dark desert floor beyond
    xs = np.array([p[0] for p in CLIFF], np.float32)
    edge = np.interp(X[0], xs, np.array([p[1] for p in CLIFF], np.float32)) + (fbm(1, W, 14 * S, 3, 5)[0] - 0.5) * 6
    mesa = np.clip((Y - (RIM - 2)) * S + 0.5, 0, 1)
    depth = np.clip((Y - RIM) / 150, 0, 1)
    rock = ramp(depth, [(0, "#2A2248"), (0.3, "#1A1636"), (1, "#0C0A1E")])
    strata = 0.5 + 0.5 * np.sin(Y * 0.55 + 3 * fbm(H, W, 40 * S, 2, 9))
    rock = rock * (0.85 + 0.25 * strata)[..., None]
    ledge = np.exp(-((Y - edge[None, :]) / 1.6) ** 2) * (Y > RIM)
    rock = screen(rock, hexc("#9C96D2") * (ledge * 0.25)[..., None])
    rock = strokes(rock, mesa, S, lambda x, y: 0.05 * math.sin(x / 50), int(3000 * S * S), length=(8, 16),
                   width=(1.2, 2.2), jitter=0.06, seed=31)
    px = px * (1 - mesa[..., None]) + rock * mesa[..., None]
    # ---- the town in silhouette: houses with parapets, the water tower on stilts, the derrick's timber frame
    town = np.zeros((H, W), np.float32)
    for (x0, x1, y) in HOUSES:
        town = np.maximum(town, _poly(W, H, S, [(x0, RIM), (x0, y), (x0 + 3, y), (x0 + 3, y - 4), (x1 - 3, y - 4),
                                                (x1 - 3, y), (x1, y), (x1, RIM)]))
    t = TOWER
    tank = _poly(W, H, S, [(t["x"] - t["half"], t["tank_bot"]), (t["x"] - t["half"], t["tank_top"] + 8),
                           (t["x"] + t["half"], t["tank_top"] + 8), (t["x"] + t["half"], t["tank_bot"])])
    cap = np.clip((t["half"] + 4 - np.sqrt(((X - t["x"]) / 1.0) ** 2 + ((Y - t["tank_top"] - 10) / 0.62) ** 2)) * S, 0, 1) * (Y < t["tank_top"] + 10)
    tank = np.maximum(tank, cap)
    stilts = np.zeros_like(town)
    for dx in (-36, -12, 12, 36):
        x0 = t["x"] + dx
        stilts = np.maximum(stilts, np.clip((2.6 - np.abs(X - x0 - (Y - t["tank_bot"]) * 0.06 * np.sign(dx))) * S, 0, 1)
                            * (Y > t["tank_bot"]) * (Y < RIM))
    for yb in (340, 366):
        stilts = np.maximum(stilts, np.clip((1.6 - np.abs(Y - yb)) * S, 0, 1) * (np.abs(X - t["x"]) < 40))
    d = DERRICK
    frame = np.zeros_like(town)
    for side in (-1, 1):
        x_at = lambda yv: d["x"] + side * d["half_foot"] * np.clip((yv - d["top"]) / (RIM - d["top"]), 0, 1)
        frame = np.maximum(frame, np.clip((2.2 - np.abs(X - x_at(Y))) * S, 0, 1) * (Y > d["top"]) * (Y < RIM))
    for k in range(5):
        yb = d["top"] + (RIM - d["top"]) * (k + 1) / 6
        frame = np.maximum(frame, np.clip((1.4 - np.abs(Y - yb)) * S, 0, 1) * (np.abs(X - d["x"]) < d["half_foot"] * (yb - d["top"]) / (RIM - d["top"])))
    frame = np.maximum(frame, np.clip((6 - np.sqrt((X - d["x"]) ** 2 + (Y - d["top"]) ** 2)) * S, 0, 1))  # the wheel
    sil = np.clip(np.maximum.reduce([town, tank, stilts, frame]), 0, 1)
    body = ramp(np.clip((Y - 240) / 160, 0, 1), [(0, "#16122E"), (1, "#0C0A1E")])
    px = px * (1 - sil[..., None]) + body * sil[..., None]
    gy, gx = np.gradient(blur(sil, 1.4 * S))
    rim = np.clip((gx + gy) * 0.7 * S * 3.0, 0, 1) * sil
    px = screen(px, hexc("#B9B0E8") * (rim * 0.5)[..., None])
    tank_shade = np.clip(-(X - t["x"]) / t["half"], -1, 1)                  # the tank, a cylinder lit from the west
    px = screen(px, hexc("#9C96D2") * (tank * np.clip(tank_shade * 0.6 + 0.2, 0, 1) * 0.10)[..., None])
    # lit windows in the houses, warm
    rng = np.random.default_rng(7)
    for (x0, x1, y) in HOUSES:
        for _ in range(2):
            wx, wy = rng.uniform(x0 + 8, x1 - 8), rng.uniform(y + 10, RIM - 10)
            px = screen(px, hexc("#FFB45E") * (np.exp(-(((X - wx) / 2.0) ** 2 + ((Y - wy) / 2.6) ** 2)) * 0.8)[..., None])
    # the lantern strings: a sagging line with small warm lanterns
    for (a, b) in STRINGS:
        for k in range(1, 6):
            tt = k / 6
            lx = a[0] + (b[0] - a[0]) * tt
            ly = a[1] + (b[1] - a[1]) * tt + 14 * 4 * tt * (1 - tt)
            px = screen(px, hexc("#FFC27A") * (np.exp(-(((X - lx) / 1.4) ** 2 + ((Y - ly) / 1.6) ** 2)) * 0.7)[..., None])
    Yl = px @ RL.LUM
    px = px * np.where(Yl > 0.42, 0.42 / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.26)
    return RL.grain(px, 0.008, seed=53)
