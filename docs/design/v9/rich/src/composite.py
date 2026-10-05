"""Composite: a pilot level as the player sees it (scene, veil, pegs and bricks, frame, launcher, HUD, bucket).

  py -3 composite.py <level-id> <scene-stem> [boat|cart] [seed]

Writes composites/<level-id>.png (800 x 600) and @2x (1600 x 1200); the 2x is painted natively and the 1x is reduced
from it. The colours are the engine's own pick for level 5 (greens on) and the seed.
"""
import sys

import numpy as np

from board import draw_pieces, engine_colours, load_level, veil
from rich_lib import LEVELS, OUT_COMP, OUT_SCENES, Img, load_rgb, save_rgb
import frame_rich as fr


def render(level_id, scene_stem, bucket="boat", seed=1, S=2, hud_kw=None, lit=(), gone=(), aim=None, extra=None):
    path = LEVELS / f"{level_id}.json"
    level = load_level(path)
    colours = engine_colours(path, 5, seed)
    scene = load_rgb(OUT_SCENES / f"{scene_stem}{'@2x' if S == 2 else ''}.png")
    k = level.get("scene", {}).get("veil", 0.30)
    scene = veil(scene, level, S, k=k)
    img = Img(800 * S, 600 * S, S, px=scene.copy())
    draw_pieces(img, level, colours, lit=lit, gone=gone)
    if extra:
        extra(img)
    fr.playfield_chrome(img, level, bucket=bucket, bucket_x=fr.BUCKET_X_DEFAULT, aim=aim, hud_kw=hud_kw or {})
    return img, colours


if __name__ == "__main__":
    lid, stem = sys.argv[1], sys.argv[2]
    bucket = sys.argv[3] if len(sys.argv) > 3 else "boat"
    seed = int(sys.argv[4]) if len(sys.argv) > 4 else 1
    img, colours = render(lid, stem, bucket, seed)
    save_rgb(img.px, OUT_COMP / f"{lid}@2x.png")
    save_rgb(img.px, OUT_COMP / f"{lid}.png", size=(800, 600))
    print(lid, {c: colours.count(c) for c in set(colours)})
