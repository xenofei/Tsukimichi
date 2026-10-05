"""Pilot level base-p2 "The Holy See at Night" (terrain emphasis), scene.

Source: the official Coerthas loading-screen painting (ui/loadingimage/-nowloading_base03.tex, 1920 x 1080, (c) SQUARE
ENIX), read from the game install. Mirrored, so its key light (upper right in the original: the brightest cloud tops and
the cathedral's lit faces) comes from the upper left, the Moonfall moon's side. Cropped to 4:3 and night graded
(rich_lib.night_map): Ishgard under the moon above the sea of clouds.
"""
import sys

import numpy as np

from rich_lib import (OUT_SCENES, blur, crop_to, kuwahara, load_official, lum, moon_glow, night_map, save_rgb,
                      smooth, vignette, grain)

SRC = "ui_loadingimage_-nowloading_base03.png"
# the crop in the mirrored source (x, y, w, h): 4:3, inside the painting's letterbox
CROP = (430.0, 60.0, 1293.0, 970.0)


def build(S=1):
    src = load_official(SRC)[:, ::-1]
    W, H = int(800 * S), int(600 * S)
    px = crop_to(src, CROP, (W, H))
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    yy = np.mgrid[0:H, 0:W][0] / H
    sky = blur(smooth(0.02, 0.15, b - r) * smooth(0.75, 0.35, yy), 3 * S)
    out = night_map(px, curve=2.0, chroma=0.28, ceiling=0.44, sky=sky, sky_drop=0.35, local=0.6)
    out = moon_glow(out, -50 * S, -60 * S, 330 * S, 900 * S, 0.12, 0.05)
    out = vignette(out, 0.30)
    return grain(out, 0.008, seed=11)


if __name__ == "__main__":
    for S in (1, 2):
        px = build(S)
        save_rgb(px, OUT_SCENES / f"base-p2-holy-see{'@2x' if S == 2 else ''}.png")
    print("ok")
