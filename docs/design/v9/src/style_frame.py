"""Moonfall style frames (plan v9 G8, deliverable 1), Menphina's Medallion only.

  style-frame-a.png / @2x   mid-shot: the ball in flight, six pegs lit, a "100" popup, bucket A (the crescent cradle)
  style-frame-b.png / @2x   aiming: the guide's dots to the first peg, Super Guide's line past it, bucket B (the lantern boat)

Each is painted at 2x (1600 x 1200) and reduced to 1x (800 x 600) with Lanczos. Run: py -3 style_frame.py
"""
import math

import numpy as np

from mf_lib import V9, Img, draw_ball, hexc, text
from playfield import (bucket_boat, bucket_cradle, draw_level, frame, guide_dots, hud, launcher, level_layout, sky)
from portraits import medallion_portrait


def frame_a(S=2):
    img = Img(800 * S, 600 * S, S)
    sky(img)
    pegs, bricks, kinds = level_layout()
    # the shot so far: down the right hanging arc, onto the ring's right side
    lit = {9, 10, 11, 12, 18, 19}
    draw_level(img, pegs, bricks, kinds, lit=lit)
    frame(img)
    launcher(img, aim_deg=28.0, gauge=0.42, ball=False)
    bucket_cradle(img, 296.0)
    # the ball, just off peg 19, with the popup under the last orange it hit
    draw_ball(img, 487.0, 352.0)
    last_orange = max((i for i in lit if kinds[i] == "orange"), default=None)
    if last_orange is not None:
        x, y = pegs[last_orange]
        # just below the struck peg (as measured), nudged to the first spot clear of every other peg and the ball
        for (ox, oy) in ((0, 21), (-22, 14), (22, 14), (0, -21)):
            tx, ty = x + ox, y + oy
            if all(math.hypot(tx - px, ty - py) > 23 for j, (px, py) in enumerate(pegs) if j != last_orange) and math.hypot(tx - 487, ty - 352) > 14:
                break
        text(img, tx, ty, "100", "ui_sb", 9.5, "#FFD7A8", anchor="mm", halo=0.7)
    hud(img, portrait=medallion_portrait("pipiru"))
    return img, kinds


def frame_b(S=2):
    img = Img(800 * S, 600 * S, S)
    sky(img)
    pegs, bricks, kinds = level_layout()
    gone = {9, 10, 11, 12, 18, 19, 3, 4}                                        # cleared on earlier shots
    draw_level(img, pegs, bricks, kinds, gone=gone)
    frame(img)
    start, d = launcher(img, aim_deg=20.0, gauge=0.0, ball=True)
    live = [p for i, p in enumerate(pegs) if i not in gone]
    guide_dots(img, start, d, live, super_guide=True)
    bucket_boat(img, 520.0)
    hud(img, score="141,830", balls=5, mult="×2", cleared=13, oranges_left=12, turns=1,
        portrait=medallion_portrait("pipiru"))
    return img, kinds


if __name__ == "__main__":
    for name, fn in (("a", frame_a), ("b", frame_b)):
        img, kinds = fn()
        img.save(V9 / f"style-frame-{name}@2x.png")
        img.save(V9 / f"style-frame-{name}.png", (800, 600))
        print(name, {k: kinds.count(k) for k in set(kinds)})
