"""Readability at Moonfall's smallest window (spec-moonfall.md, "Sizes"): readability.png.

The playfield is drawn natively at the window's scale, not resized from 1x: the engine draws every sprite at the
scale it needs. Shown: the whole frame at 640 x 480 (0.8x, the minimum), and 3x nearest-neighbour zooms of one
peg cluster at 0.8x and at 1x, so the pixels the player actually gets can be judged.
Run: py -3 readability.py
"""
import numpy as np
from PIL import Image

from mf_lib import P, V9, Img, hexc, text
from style_frame import frame_a


def main():
    small, _ = frame_a(S=0.8)
    one, _ = frame_a(S=1.0)
    im_s = Image.fromarray((np.clip(small.px, 0, 1) * 255 + 0.5).astype(np.uint8))
    im_1 = Image.fromarray((np.clip(one.px, 0, 1) * 255 + 0.5).astype(np.uint8))
    W, H = 1500, 560
    sheet = Image.new("RGB", (W, H), tuple(int(v * 255) for v in hexc(P["status_foot"])))
    sheet.paste(im_s, (20, 60))
    # the same cluster (the right hanging arc and the ring's right side, with lit pegs) at 0.8x and 1x, zoomed 3x
    box1 = (int(420 * 1.0), int(140 * 1.0), int(420 * 1.0) + 140, int(140 * 1.0) + 120)
    box8 = (int(420 * 0.8), int(140 * 0.8), int(420 * 0.8) + 112, int(140 * 0.8) + 96)
    z8 = im_s.crop(box8).resize((112 * 3, 96 * 3), Image.NEAREST)
    z1 = im_1.crop(box1).resize((140 * 3, 120 * 3), Image.NEAREST)
    sheet.paste(z8, (680, 60))
    sheet.paste(z1, (1040, 60))
    img = Img(W, H, 1.0, np.asarray(sheet, np.float32) / 255)
    text(img, 20, 36, "Readability at the smallest window: 640 × 480 (0.8×), drawn natively", "serif", 18, P["cream"], anchor="ls", halo=0)
    text(img, 680, 448, "0.8×, zoomed 3×: pegs r 8 px", "ui", 12, P["ink_dim"], anchor="ls", halo=0)
    text(img, 1040, 448, "1×, zoomed 3×: pegs r 10 px", "ui", 12, P["ink_dim"], anchor="ls", halo=0)
    text(img, 680, 476, "At 0.8× a peg is 16 px across: its hue, its lit side and its sliver still read; the seas merge into tone.",
         "ui", 12, P["cream"], anchor="ls", halo=0)
    text(img, 680, 496, "The ball is 10 px across and stays the brightest small thing on the board.", "ui", 12, P["cream"], anchor="ls", halo=0)
    text(img, 680, 516, "HUD text: the smallest labels (Balls, Orange, the power) are 10 units, 8 px at 0.8×, the floor in the spec.", "ui", 12, P["cream"], anchor="ls", halo=0)
    img.save(V9 / "readability.png")


if __name__ == "__main__":
    main()
    print("readability")
