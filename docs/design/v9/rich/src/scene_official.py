"""Level scenes built on official FFXIV paintings (the loading-screen art in the player's own install), night graded
with the Medallion grade. One config per scene; the same recipe is what the plugin would run at load time (see
spec-rich.md, 'Scene recipe').

  py -3 scene_official.py [scene-id ...]

Sources ((c) SQUARE ENIX), read by tools/texdump from the game install:
  exp-p1  ui/loadingimage/-nowloading_base21.tex  Old Sharlayan
  exp-p3  ui/loadingimage/-nowloading_base25.tex  Mare Lamentorum
"""
import sys

import numpy as np

from rich_lib import (OUT_SCENES, blur, crop_to, grain, hexc, load_official, moon_glow, night_map, save_rgb, screen,
                      smooth, vignette)

# The Far Shore (expansion) is a later hour: the same grade on a deeper violet ramp.
VIOLET_RAMP = [(0.00, "#05040E"), (0.10, "#0A0820"), (0.24, "#151234"), (0.42, "#282356"), (0.60, "#463F80"),
               (0.78, "#8077B0"), (0.92, "#BFB8DE"), (1.00, "#E0DCF0")]

SCENES = {
    "exp-p1-sharlayan": dict(src="ui_loadingimage_-nowloading_base21.png", crop=(-32.0, 30.0, 1307.0, 980.0),
                             pad=(0, 64), curve=1.9, chroma=0.22, ceiling=0.42, sky_drop=0.40, local=0.5,
                             ramp=VIOLET_RAMP, glow=(-50, -60)),
    # mirrored: the planet's sunlit side is on the right in the painting; mirrored, its light comes from the left
    "exp-p3-mare-lamentorum": dict(src="ui_loadingimage_-nowloading_base25.png", crop=(557.0, -108.0, 1166.0, 875.0),
                                   mirror=True, pad=(120, 0), pad_mode="reflect", curve=1.15, chroma=0.42, ceiling=0.44,
                                   sky_drop=0.0, local=0.4, ramp=VIOLET_RAMP, glow=None),
}


def build(cfg, S=1):
    src = load_official(cfg["src"])
    if cfg.get("mirror"):
        src = src[:, ::-1]
    top, left = cfg.get("pad", (0, 0))
    if top or left:
        src = np.pad(src, ((top, 0), (left, 0), (0, 0)), mode=cfg.get("pad_mode", "edge"))
    x, y, w, h = cfg["crop"]
    W, H = int(800 * S), int(600 * S)
    px = crop_to(src, (x + left, y + top, w, h), (W, H))
    sky = None
    if cfg.get("sky_drop"):
        yy = np.mgrid[0:H, 0:W][0] / H
        sky = blur(smooth(0.02, 0.15, px[..., 2] - px[..., 0]) * smooth(0.75, 0.30, yy), 3 * S)
    out = night_map(px, curve=cfg["curve"], chroma=cfg["chroma"], ceiling=cfg["ceiling"], sky=sky,
                    sky_drop=cfg.get("sky_drop", 0.0), local=cfg.get("local", 0.0), ramp_stops=cfg.get("ramp"),
                    warm_keep=cfg.get("warm_keep", 0.35))
    if cfg.get("glow"):
        gx, gy = cfg["glow"]
        out = moon_glow(out, gx * S, gy * S, 330 * S, 900 * S, 0.11, 0.05, col="#C4C0EE")
    out = vignette(out, 0.28)
    return grain(out, 0.008, seed=13)


if __name__ == "__main__":
    ids = sys.argv[1:] or list(SCENES)
    for sid in ids:
        for S in (1, 2):
            save_rgb(build(SCENES[sid], S), OUT_SCENES / f"{sid}{'@2x' if S == 2 else ''}.png")
        print("ok", sid)
