"""The Option B painter registry: one painter per release key. Each painter returns (canvas, masks) at 2240 x 880,
with the standard mask keys (clouds, under, moon, moonlit, far, city, cliff, field, ridge, figs, lantern; a region a
scene doesn't have is all zeros). This writes, per release, into ../optionb/:
  <key>-base.png    the painting at 1120 x 440
  <key>-masks.npz   its masks at 1120 x 440
Run: py -3 painters_b.py <key> [<key> ...]      (no key: every registered release)

To add a release: write its painter (a function in its own module, as paint_option_b.paint is for 1.20.0), register
it below, and write its <key>.json beside the painting (see spec-1.22.md W2, "Production recipe").
"""
import pathlib
import sys

import numpy as np
from PIL import Image

OUT = pathlib.Path(__file__).resolve().parent.parent / "optionb"


def _evercold():
    from paint_option_b import paint
    return paint()


def _b2(name):
    def run_():
        import paint_option_b2
        return getattr(paint_option_b2, name)()
    return run_


def _b3(name):
    def run_():
        import paint_option_b3
        return getattr(paint_option_b3, name)()
    return run_


PAINTERS = {
    "evercold-b": _evercold,       # 1.20.0 Before Evercold
    "welcome-b": _b2("welcome"),   # 1.22.0 Welcome home
    "whatnext-b": _b2("whatnext"),  # 1.21.0 What next, for every character
    "answers-b": _b2("answers"),   # 1.19.0 Right answers
    "runs-b": _b3("runs"),         # 1.18.0 Runs you can trust
    "mixmatch-b": _b3("mixmatch"),  # 1.17.0 Mix and match
    "themes-b": _b3("themes"),     # 1.16.0 Themes
    "faces-b": _b3("faces"),       # 1.15.0 Faces and icons
    "polish-b": _b3("polish"),     # 1.14.0 The polish you asked for
}


def run(key):
    c, M = PAINTERS[key]()
    OUT.mkdir(exist_ok=True)
    c.save(OUT / f"{key}-base.png", (1120, 440), grain=0.008, seed=3)
    small = {k: np.asarray(Image.fromarray((np.clip(v, 0, 1) * 255).astype(np.uint8)).resize((1120, 440), Image.LANCZOS), np.float32) / 255
             for k, v in M.items()}
    np.savez_compressed(OUT / f"{key}-masks.npz", **small)
    print(f"{key}-base.png, {key}-masks.npz")


if __name__ == "__main__":
    for k in sys.argv[1:] or list(PAINTERS):
        run(k)
