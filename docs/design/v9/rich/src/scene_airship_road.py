"""Pilot level base-p1 "The Airship Road" (technique: a trail on a map), scene.

Source: the official world map painting "The Three Great Continents" (ui/loadingimage/-nowloading_base05.tex,
1920 x 1080, (c) SQUARE ENIX), read from the game install, cropped to Aldenard and Vylbrand and night graded as a
chart lying under the moon: parchment gone to moonlit blue-grey, the land darker than the sea, the lettering and the
drawings (dragons, ships, the city banners) kept. Over it, painted natively at each scale so it stays crisp: the
airship's route as an engraved gilt dashed line, which stays on the chart when the pegs on it clear.
"""
import numpy as np
from PIL import Image, ImageDraw

from rich_lib import OUT_SCENES, crop_to, grain, hexc, load_official, moon_glow, night_lab, save_rgb, screen, vignette

SRC = "ui_loadingimage_-nowloading_base05.png"
CROP = (-30.0, 370.0, 840.0, 630.0)          # source px (x, y, w, h), 4:3; x < 0 is edge padding, under the rail
PAD = 64
SCALE = 800.0 / CROP[2]


def to_board(x, y):
    return ((x - CROP[0]) * SCALE, (y - CROP[1]) * SCALE)


# the stops, in source px (each city's banner on the painting), and the route between them: Limsa Lominsa, across the
# strait to Ul'dah, north to Mor Dhona's lake, up to Ishgard, down to Gridania, east to Ala Mhigo
STOPS = {"Limsa Lominsa": (92, 832), "Ul'dah": (283, 888), "Mor Dhona": (306, 720), "Ishgard": (313, 548),
         "Gridania": (399, 617), "Ala Mhigo": (546, 658)}
ROUTE_SRC = [(92, 832), (140, 846), (190, 858), (240, 876), (283, 888), (300, 852), (306, 810), (300, 766),
             (306, 720), (296, 676), (292, 630), (300, 586), (313, 548), (346, 566), (372, 594), (399, 617),
             (436, 628), (474, 634), (512, 646), (546, 658), (590, 690), (630, 716), (672, 732), (706, 740)]
# (the last four: the road leaves Ala Mhigo over the Rhotano Sea, east toward the Far East)


def route_board():
    return [to_board(x, y) for (x, y) in ROUTE_SRC]


def dashed(px, S, pts, col, width=1.5, dash=5.0, gap=4.5, alpha=0.6):
    H, W, _ = px.shape
    lay = Image.new("L", (W * 2, H * 2), 0)
    dr = ImageDraw.Draw(lay)
    P_ = np.asarray(pts, np.float64)
    seg = np.sqrt(((P_[1:] - P_[:-1]) ** 2).sum(1))
    cum = np.concatenate([[0], np.cumsum(seg)])
    s = 0.0
    while s < cum[-1]:
        e = min(s + dash, cum[-1])
        # each dash follows the path (its own vertices in between), so a long dash bends with the route
        ts = np.concatenate([[s], cum[(cum > s) & (cum < e)], [e]])
        pts = [(np.interp(t, cum, P_[:, 0]) * S * 2, np.interp(t, cum, P_[:, 1]) * S * 2) for t in ts]
        dr.line(pts, fill=255, width=max(2, int(round(width * S * 2))), joint="curve")
        s += dash + gap
    m = np.asarray(lay.resize((W, H), Image.BOX), np.float32) / 255
    # an engraved line: a dark cut, its lower-right lip catching the light
    sh = np.roll(np.roll(m, max(1, int(S * 0.6)), 0), max(1, int(S * 0.6)), 1)
    px = px * (1 - 0.35 * np.clip(sh - m, 0, 1))[..., None]
    return screen(px, hexc(col) * (m * alpha)[..., None])


def build(S=1):
    from layout import smooth_path
    W, H = int(800 * S), int(600 * S)
    src = np.pad(load_official(SRC), ((0, 0), (PAD, 0), (0, 0)), mode="reflect")
    px = crop_to(src, (CROP[0] + PAD, CROP[1], CROP[2], CROP[3]), (W, H))
    out = night_lab(px, gamma=1.35, exposure=0.85, warm_keep=0.25, chroma_mid=0.45, S=S)
    out = moon_glow(out, -60 * S, -60 * S, 420 * S, 900 * S, 0.10, 0.05)
    out = dashed(out, S, smooth_path(route_board(), 8), "#D9BE82", width=2.2, alpha=0.60)
    out = vignette(out, 0.28)
    return grain(out, 0.008, seed=5)


if __name__ == "__main__":
    for S in (1, 2):
        save_rgb(build(S), OUT_SCENES / f"base-p1-airship-road{'@2x' if S == 2 else ''}.png")
    print("ok", [tuple(round(v) for v in p) for p in route_board()])
