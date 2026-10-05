"""Pilot exp-p1 "The Domes of Sharlayan" (technique: a landmark partly outlined), layout.

The scene (scene_official.py, exp-p1-sharlayan) is Old Sharlayan's harbour on the Far Shore's violet night: the great
statue pouring water from her urn on the left, the domes of the city, its slender columns, the ships at the quay. The
layout outlines part of each landmark, never all of it, so the painting finishes the shape: curved bricks lie along the
crowns of four domes with a moon on each finial; the water falling from the urn is a column of pegs down the left side;
the statue's hood, back and the long curve of her train are dotted; the columns rise as short vertical runs. Oranges
come from the domes, the finials, the columns' capitals, the statue and the urn, and the ships.
"""
import math

from layout import Layout
from rich_lib import LEVELS, level_json, write_level

LEVEL_ID = "exp-p1"
NAME = "The Domes of Sharlayan"


def dome(L, cx, cy, R, a0, a1, pieces, finial=True, t=8.0, tag="dome", orange=True):
    span = (a1 - a0) / pieces
    for k in range(pieces):
        L.arc(cx, cy, R, a0 + k * span + 1.5, span - 3.0, t=t, orange=orange, tag=tag)
    if finial:
        L.peg(cx, cy - R - t / 2 - 13 - 10, orange=True, tag=tag + " finial")


def build():
    L = Layout()
    # ---- the domes: their crowns as curved bricks, a moon on each finial
    dome(L, 432, 318, 60, 200, 340, 4, tag="great dome")
    dome(L, 327, 305, 30, 210, 330, 1, tag="east dome")
    dome(L, 521, 300, 27, 210, 330, 1, tag="small dome")
    dome(L, 707, 430, 54, 200, 272, 2, finial=False, tag="harbour dome")
    # ---- the water poured from the urn, falling down the left side
    L.peg(108, 250, orange=True, tag="the urn")
    for k, y in enumerate(range(296, 525, 36)):
        L.peg(101, y, orange=(k in (0, 6)), tag="falling water")
    # ---- the statue: her hood, her back, and the long curve of her train into the arcade
    L.peg(163, 222, orange=True, tag="statue's hood")
    for (x, y) in ((196, 262), (198, 300)):
        L.peg(x, y, tag="statue's back")
    for k, (x, y) in enumerate(((214, 340), (246, 364), (282, 380), (318, 388), (354, 388), (390, 382))):
        L.peg(x, y, orange=(k % 2 == 1), tag="the train")
    # ---- the columns rising over the roofs (the tallest stands inside the launcher's swing and is left to the art)
    for (x, ys) in ((316, (186,)), (510, (124, 160)), (580, (150,)), (660, (146, 182))):
        for k, y in enumerate(ys):
            L.peg(x, y, orange=(k == 0), tag="column")
    # ---- the ships at the quay
    for k, (x, y) in enumerate(((396, 470), (432, 482), (468, 492), (504, 484), (540, 474), (576, 484))):
        L.peg(x, y, orange=(k in (1, 4)), tag="ship's hull")
    # ---- the sky's clouds and the harbour's water
    for k, (x, y) in enumerate(((130, 112), (168, 98), (208, 92), (248, 100), (560, 70), (604, 82), (648, 96), (690, 112))):
        L.peg(x, y, orange=(k in (1, 6)), tag="cloud")
    for (x, y) in ((150, 540), (196, 528), (242, 540), (300, 520), (612, 528), (652, 506), (604, 380),
                   (640, 346), (150, 446), (196, 470), (246, 446), (600, 236), (640, 270), (690, 240)):
        L.peg(x, y, tag="harbour")
    return L


if __name__ == "__main__":
    L = build()
    print(L.counts())
    L.check()
    pegs, bricks = L.as_level()
    scene = {"source": "game", "texture": "ui/loadingimage/-nowloading_base21.tex", "crop": [-32, 30, 1307, 980],
             "padLeft": 64, "grade": "medallion-night-violet", "veil": 0.30}
    write_level(level_json(LEVEL_ID, NAME, pegs, bricks, scene=scene), LEVELS / f"{LEVEL_ID}.json")
