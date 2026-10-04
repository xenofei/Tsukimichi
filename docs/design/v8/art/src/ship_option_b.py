"""Encodes one release's six Option B masters (../optionb/<key>-<theme>.png) into the files the plugin ships, and
reports their sizes against the per-release budget (spec-1.22.md W2, "Production recipe").

Format: 1120 x 440 (the art band's 2x tier; the 1x tier is drawn from it at half size), sRGB, no alpha.
  - JPEG, quality 88, 4:4:4 chroma (no subsampling: the lead lines, engraving and gold edges stay clean), progressive;
  - except where a theme's PNG is smaller than its JPEG, which never happens for these paintings but is checked.
Budget: 600 KB per release for all six themes (about 450 KB for 1.20.0). Run: py -3 ship_option_b.py [release key]   (default evercold-b)
"""
import io
import pathlib
import sys

from PIL import Image

OUT = pathlib.Path(__file__).resolve().parent.parent / "optionb"
SHIP = OUT / "ship"
THEMES = ["classic", "medallion", "ishgard-glass", "aether-crystal", "astrologian-orrery", "sumi-to-kinpaku"]
BUDGET = 600 * 1024


def encode(im):
    j = io.BytesIO()
    im.save(j, "JPEG", quality=88, subsampling=0, progressive=True, optimize=True)
    p = io.BytesIO()
    im.save(p, "PNG", optimize=True)
    return (".jpg", j.getvalue()) if len(j.getvalue()) <= len(p.getvalue()) else (".png", p.getvalue())


if __name__ == "__main__":
    key = sys.argv[1] if len(sys.argv) > 1 else "evercold-b"
    SHIP.mkdir(exist_ok=True)
    total = 0
    for t in THEMES:
        im = Image.open(OUT / f"{key}-{t}.png").convert("RGB")
        assert im.size == (1120, 440), im.size
        ext, data = encode(im)
        (SHIP / f"{key}-{t}{ext}").write_bytes(data)
        total += len(data)
        print(f"{t:20s} {ext} {len(data) / 1024:7.1f} KB")
    print(f"{'release total':20s}     {total / 1024:7.1f} KB  (budget {BUDGET / 1024:.0f} KB) {'OK' if total <= BUDGET else 'OVER'}")
    print(f"{'9 releases, est.':20s}     {9 * total / 1024 / 1024:7.2f} MB")
