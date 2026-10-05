"""The two campaigns' buckets of one craft (owner brief: redesign the base bucket to match the lantern boat):
screens/buckets.png (1600 x 440) shows each at 3x, cut from the 2x composites, side by side with its caption.

  The Moon Road (base):     the lantern cart on the moon road (frame_rich.bucket_cart)
  The Far Shore (expansion): the lantern boat on its strip of water (the approved v9 bucket B)
"""
import numpy as np
from PIL import Image

from rich_lib import OUT_COMP, OUT_SCREENS, P, Img, load_rgb, save_rgb, text
from ui_kit import panel


def crop(level, bx=520.0):
    """Round 4 (realism round 3): the bucket on its own board, every peg cleared, so no peg touches the lantern."""
    import json
    from composite import render
    scene = {"base-p3": "base-p3-moogle", "exp-p2": "exp-p2-lantern-ferry"}[level]
    lv = json.loads((OUT_COMP.parent / "levels" / f"{level}.json").read_text())
    n = len(lv["pegs"]) + len(lv["bricks"])
    img, _ = render(level, scene, "cart" if level.startswith("base") else "boat", 1, S=2, gone=set(range(n)),
                    bucket_x=bx)
    px = img.px
    x0, x1, y0, y1 = int((bx - 90) * 2), int((bx + 90) * 2), int(520 * 2), int(598 * 2)
    im = Image.fromarray((px[y0:y1, x0:x1] * 255).astype(np.uint8)).resize(((x1 - x0) * 3 // 2, (y1 - y0) * 3 // 2), Image.LANCZOS)
    return np.asarray(im, np.float32) / 255


def build():
    img = Img(1600, 440, 1.0)
    panel(img, 0, 0, 1600, 440, r=0, border=False, inner_rule=False)
    for i, (lvl, title, sub) in enumerate((("base-p3", "The Moon Road: the lantern cart",
                                            "A deep open box of dark planks with brass corner straps, two spoked wheels on the moon road, a paper lantern on its curved rear post."),
                                           ("exp-p2", "The Far Shore: the lantern boat",
                                            "Dark planks on a strip of still water, the same paper lantern on its stern post, its reflection broken by ripples."))):
        c = crop(lvl)
        x0 = 40 + i * 780
        panel(img, x0, 30, x0 + 740, 410, r=12, bead=True)
        h, w = c.shape[:2]
        ox, oy = x0 + (740 - w) // 2, 60
        img.px[oy:oy + h, ox:ox + w] = c
        text(img, x0 + 370, 60 + h + 40, title, "serif", 22, P["cream"], anchor="mm", halo=0)
        from rich_lib import wrap
        y = 60 + h + 76
        for line in wrap(sub, "ui", 14, 640):
            text(img, x0 + 370, y, line, "ui", 14, P["ink_dim"], anchor="mm", halo=0)
            y += 22
    save_rgb(img.px, OUT_SCREENS / "buckets.png")


if __name__ == "__main__":
    build()
    print("ok")
