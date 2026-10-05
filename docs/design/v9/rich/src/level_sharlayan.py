"""Pilot exp-p1 "The Domes of Sharlayan" (technique: a landmark partly outlined), layout.

The scene (scene_official.py, exp-p1-sharlayan) is Old Sharlayan's harbour on the Far Shore's violet night: the great
statue pouring water from her urn on the left, the domes of the city, its slender columns, the ships at the quay. The
layout outlines part of each landmark, never all of it, so the painting finishes the shape:
  * the domes: curved bricks lie along each crown (the great dome in two courses), a moon on each finial, and the
    drums' sides dotted down to the roofline;
  * the statue: her silhouette traced by an even dotted outline (hood, back and the long sweep of her train), the urn
    a moon, the water falling from it a column of pegs down the left side;
  * the columns rising over the roofs: short vertical runs; the ships at the quay: a row along their hulls.
Oranges come from the domes, the finials, the statue and its urn, the ships and the column capitals within reach.

Round 2 (level critic round 1): the great dome's crown now ends at 315 degrees, so the notch between it and the small
dome is wider than a ball (no trap); no candidate sits above the reach of a first flight (the clouds and the tall
column are blue); the domes and the statue are drawn, not dotted at random; the cloud filler is gone.
"""
import math

from layout import Layout, resample, smooth_path
from rich_lib import LEVELS, level_json, write_level

LEVEL_ID = "exp-p1"
NAME = "The Domes of Sharlayan"


def dome(L, cx, cy, R, a0, a1, pieces, finial=True, t=8.0, tag="dome", orange=True, drum=0):
    span = (a1 - a0) / pieces
    for k in range(pieces):
        L.arc(cx, cy, R, a0 + k * span + 1.5, span - 3.0, t=t, orange=orange, tag=tag)
    if finial:
        L.peg(cx, cy - R - t / 2 - 13 - 10, orange=True, tag=tag + " finial")
    for k in range(drum):                                   # the drum's sides, dotted down to the roofline
        for sx in (-1, 1):
            L.peg(cx + sx * (R + 2), cy + 22 + k * 34, tag=tag + " drum")


def build():
    L = Layout()
    # ---- the domes
    dome(L, 432, 318, 60, 208, 315, 4, tag="great dome", drum=1)
    L.arc(432, 318, 38, 215, 110, t=8, orange=False, tag="great dome, lower course")
    dome(L, 327, 305, 30, 200, 330, 2, tag="east dome")
    dome(L, 527, 300, 27, 200, 340, 2, tag="small dome")
    dome(L, 707, 430, 54, 196, 268, 2, finial=False, tag="harbour dome")
    # ---- the urn and the water poured from it, falling down the left side
    L.peg(108, 250, orange=True, tag="the urn")
    for k, y in enumerate(range(296, 525, 36)):
        L.peg(101, y, orange=(k in (1, 5)), tag="falling water")
    # ---- the statue's outline: hood, back and the long sweep of her train into the arcade, evenly dotted
    outline = smooth_path([(132, 214), (166, 200), (198, 226), (210, 266), (216, 304), (236, 340), (270, 366),
                           (308, 382), (348, 390), (390, 386)], 10)
    for k, (x, y) in enumerate(resample(outline, 34.0, 0.0)):
        L.peg(x, y, orange=(k % 3 == 1), tag="the statue")
    # ---- the columns rising over the roofs (the tallest stands inside the launcher's swing and is left to the art)
    for (x, ys) in ((304, (184,)), (580, (150, 186)), (640, (156, 192))):
        for k, y in enumerate(ys):
            L.peg(x, y, orange=(k == len(ys) - 1), tag="column")
    # ---- the ships at the quay
    for k, (x, y) in enumerate(((396, 470), (432, 482), (468, 492), (504, 484), (540, 474), (576, 484))):
        L.peg(x, y, orange=(k in (0, 2, 4)), tag="ship's hull")
    for (x, y) in ((450, 440), (522, 444)):
        L.peg(x, y, tag="ship's mast")
    # ---- a few clouds, and the harbour's water
    for (x, y) in ((150, 140), (210, 132), (690, 140)):
        L.peg(x, y, tag="cloud")
    for (x, y) in ((150, 540), (196, 528), (242, 540), (300, 520), (612, 528), (652, 506), (604, 380),
                   (640, 346), (150, 446), (196, 470), (246, 446), (690, 240), (700, 300), (350, 540), (440, 540),
                   (520, 536)):
        L.peg(x, y, orange=(x, y) in ((196, 470), (300, 520), (612, 528)), tag="harbour lantern" if (x, y) in ((196, 470), (300, 520), (612, 528)) else "harbour")
    return L


if __name__ == "__main__":
    L = build()
    print(L.counts())
    if L.check():
        raise SystemExit("pre-flight failed: the level was not written")
    pegs, bricks = L.as_level()
    scene = {"source": "game", "texture": "ui/loadingimage/-nowloading_base21.tex", "crop": [-32, 30, 1307, 980],
             "padLeft": 64, "grade": "medallion-night-violet", "veil": 0.44}
    write_level(level_json(LEVEL_ID, NAME, pegs, bricks, scene=scene), LEVELS / f"{LEVEL_ID}.json")
