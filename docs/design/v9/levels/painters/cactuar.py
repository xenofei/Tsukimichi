"""Our own painting for 1-3 "The Cactuar": a cactuar mid-stride on a dune crest in the Thanalan desert by moonlight.

The cactuar is FFXIV's running cactus of the Thanalan sands: one long body, the face's three dark holes (two eyes and
an open mouth), three short spines on its crown, one arm flung up and one down, legs mid-stride. It is painted as a
moonlit silhouette (dark sage, its ribs catching the light, a rim of moonlight on the side toward the moon), standing on
the near dune's crest; two farther dune lines and the starfield lie behind; the full moon (emissive, the light itself)
sits in the upper left where no peg goes.

FEATURES (board units) are what the layout traces: the silhouette, its parts, and the dune crests.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

# the silhouette, clockwise on screen, from the crown: right flank, the lowered arm, right leg, left leg, the raised
# arm, back to the crown
CACTUAR = [(452, 212), (480, 220), (497, 246), (503, 286), (514, 298), (546, 316), (568, 342), (578, 380), (566, 392),
           (552, 372), (536, 350), (508, 340), (502, 378), (494, 418), (502, 440), (522, 466), (540, 484), (530, 497),
           (506, 488), (482, 462), (466, 446), (450, 447), (432, 452), (412, 478), (390, 500), (373, 494), (388, 468),
           (404, 440), (406, 420), (402, 392), (400, 362), (392, 342), (364, 332), (340, 318), (324, 292), (319, 262),
           (331, 255), (343, 280), (362, 298), (394, 308), (402, 296), (406, 266), (414, 240), (430, 220)]
TUFTS = [[(436, 222), (440, 196), (448, 220)], [(450, 214), (458, 186), (464, 216)], [(466, 220), (478, 198), (478, 226)]]
EYES = [(437, 262, 7.5, 11.0), (469, 266, 7.5, 11.0)]
MOUTH = (452, 300, 9.0, 13.0)
NEAR_DUNE = [(0, 462), (60, 470), (150, 486), (250, 500), (380, 500), (470, 496), (560, 500), (650, 486), (740, 470), (800, 462)]
MID_DUNE = [(0, 410), (60, 400), (140, 380), (220, 372), (300, 384), (380, 410), (470, 430), (560, 420), (620, 396),
            (680, 384), (740, 392), (800, 400)]
FAR_DUNE = [(0, 338), (60, 330), (160, 312), (250, 318), (330, 336), (420, 352), (520, 344), (640, 322), (720, 316), (800, 324)]
MOON = (150, 108, 34)

FEATURES = {
    "cactuar": {"points": [list(p) for p in CACTUAR]},
    "crown": [[458, 186]],
    "eyes": [[437, 262], [469, 266]],
    "mouth": [[452, 300]],
    "raised hand": [[322, 258]],
    "lowered hand": [[574, 388]],
    "feet": [[380, 498], [534, 492]],
    "near dune": {"points": [list(p) for p in NEAR_DUNE]},
    "mid dune": {"points": [list(p) for p in MID_DUNE]},
    "far dune": {"points": [list(p) for p in FAR_DUNE]},
}


def poly_mask(pts, S, smooth_n=6, ss=3):
    P = RL.catmull(pts + pts[:3], smooth_n, closed=False) if smooth_n else pts
    im = Image.new("L", (int(800 * S * ss), int(600 * S * ss)), 0)
    ImageDraw.Draw(im).polygon([(x * S * ss, y * S * ss) for (x, y) in P], fill=255)
    return np.asarray(im.resize((int(800 * S), int(600 * S)), Image.BOX), np.float32) / 255


def below(curve, S, X, Y):
    """Coverage of everything below a crest polyline (x increasing), with a soft antialiased edge."""
    xs = np.array([p[0] for p in curve], np.float32)
    ys = np.array([p[1] for p in curve], np.float32)
    top = np.interp(X[0], xs, ys)
    n = fbm(1, X.shape[1], 18 * S, 3, 5)[0] - 0.5
    top = top + n * 4
    return np.clip((Y - top[None, :]) * S + 0.5, 0, 1), top


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    # ---- the sky: deep indigo at the zenith to a violet haze over the far dunes; the moon's glow from the upper left
    sky = ramp(np.clip(Y / 420, 0, 1), [(0, "#07061A"), (0.45, "#120F34"), (0.8, "#231C4C"), (1.0, "#30285A")])
    mx, my, mr = MOON
    d_moon = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    sky = screen(sky, hexc("#C8C4F0") * (np.exp(-(d_moon / 240) ** 2) * 0.16 + np.exp(-(d_moon / 700) ** 2) * 0.05)[..., None])
    sky = strokes(sky, np.ones((H, W), np.float32), S, lambda x, y: -0.15 + 0.2 * math.sin(x / 110), int(5000 * S * S),
                  length=(10, 20), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=4)
    px = RL.stars(sky, S, 900, 21, (0, 0, W, 430 * S), avoid=lambda x, y: float(np.clip((math.hypot(x / S - mx, y / S - my) - 60) / 120, 0, 1)), bright=0.55)
    px, _disc = RL.moon_emissive(px, S, mx, my, mr, seed=7)
    # ---- the dunes, far to near: each a lit face toward the moon (upper left) and a shadowed lee
    for curve, base, lit, k in ((FAR_DUNE, "#1B1940", "#5A5488", 0.6), (MID_DUNE, "#161434", "#4E4A80", 0.7),
                                (NEAR_DUNE, "#100E26", "#47437A", 0.8)):
        m, top = below(curve, S, X, Y)
        depth = np.clip((Y - top[None, :]) / 90, 0, 1)
        slope = np.gradient(top)[None, :] * S
        face = np.clip(0.55 - slope * 1.2, 0, 1)                      # faces rising to the right look toward the moon
        col = ramp(depth, [(0, lit), (0.25, base), (1, "#0A0918")])
        col = col * (0.75 + 0.45 * face)[..., None]
        ripple = 0.5 + 0.5 * np.sin((Y - top[None, :]) * 0.9 + X * 0.05 + 3 * fbm(H, W, 30 * S, 2, 9))
        col = col * (0.92 + 0.10 * ripple)[..., None]
        crest = np.exp(-((Y - top[None, :] - 1.2) / 1.4) ** 2) * k
        col = screen(col, hexc("#9C96D2") * (crest * 0.35)[..., None])
        px = px * (1 - m[..., None]) + col * m[..., None]
    dmask = below(NEAR_DUNE, S, X, Y)[0]
    px = strokes(px, dmask, S, lambda x, y: 0.05 * math.sin(x / 60), int(3500 * S * S), length=(8, 16), width=(1.0, 2.0),
                 jitter=0.05, seed=8)
    # a soft contact shadow where its feet meet the crest
    for (fx, fy) in ((382, 499), (532, 494)):
        sh = np.exp(-(((X - fx) / 26) ** 2 + ((Y - fy - 3) / 5) ** 2))
        px = px * (1 - 0.45 * sh)[..., None]
    # ---- the cactuar: a dark sage silhouette with its ribs, spines and a rim of moonlight on the moon side
    body = poly_mask(list(CACTUAR), S)
    for t in TUFTS:
        body = np.maximum(body, poly_mask(t, S, smooth_n=0))
    gy, gx = np.gradient(blur(body, 2.2 * S))
    facing = np.clip(-(gx * -0.707 + gy * -0.707) * S * 5.0, 0, 1)        # edges whose outward normal faces upper left
    rim = facing * smooth(0.2, 0.8, body) * (1 - smooth(0.85, 1.0, blur(body, 2.0 * S)))
    ribs = 0.5 + 0.5 * np.cos((X - 452 + (Y - 300) * 0.12) * 0.42)
    shade = blur(body, 16 * S)
    # a cylinder lit from the upper left: the moon side of every limb lighter, its far side falling to dark
    gyb, gxb = np.gradient(blur(body, 9 * S))
    turn = np.clip((gxb * 0.8 + gyb * 0.6) * S * 9.0, -1, 1)          # > 0 on the left and upper faces
    col = ramp(np.clip(shade, 0, 1), [(0, "#0A1418"), (0.6, "#153036"), (1.0, "#1F4446")])   # teal, off orange's protan line
    col = col * (0.85 + 0.45 * turn)[..., None]
    col = col * (0.82 + 0.22 * ribs)[..., None]
    spines = ((np.sin(X * 1.9) * np.sin(Y * 1.9 + X * 0.3)) > 0.93).astype(np.float32) * smooth(0.6, 0.95, body)
    col = screen(col, hexc("#9FC8A8") * (spines * 0.25)[..., None])
    col = screen(col, hexc("#C9D6FF") * (np.clip(rim * 2.2, 0, 1) * 0.55)[..., None])
    px = px * (1 - body[..., None]) + col * body[..., None]
    # the face: two eyes and an open mouth, dark holes
    face = np.zeros_like(body)
    for (ex, ey, rx, ry) in EYES + [MOUTH]:
        face = np.maximum(face, np.clip((1 - np.sqrt(((X - ex) / rx) ** 2 + ((Y - ey) / ry) ** 2)) * min(rx, ry) * S + 0.5, 0, 1))
    px = px * (1 - 0.92 * face[..., None]) + hexc("#04060A") * (0.92 * face[..., None])
    # ---- the value ceiling (the board's rule), the moon excepted
    Yl = px @ RL.LUM
    lim = np.where(d_moon < mr + 30, 1.0, 0.42)
    px = px * np.where(Yl > lim, lim / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.26)
    return RL.grain(px, 0.008, seed=17)
