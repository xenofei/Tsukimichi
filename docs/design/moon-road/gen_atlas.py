# Packs the ornament kit (ornaments/*.svg) into the plugin's texture atlas.
#
# Writes Tsukimichi/assets/ui/ornaments.png (256 x 128), ornaments@2x.png (512 x 256, the same layout doubled) and
# ornaments.json (each sprite's rectangle in 1x pixels). The layout is mirrored by Tsukimichi.Core/Ui/OrnamentLayout.cs;
# a test holds the two to each other and to the PNG sizes, so change both together.
#
# Each piece's SVG is inlined into one sheet (ids prefixed per piece so gradients and masks do not collide), Chrome
# headless draws the sheet on a transparent background at 1x and at 2x, and the result is saved as RGBA PNG.
# Usage: python docs/design/moon-road/gen_atlas.py
import json
import os
import re
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ORN = os.path.join(HERE, "ornaments")
DEST = os.path.join(REPO, "Tsukimichi", "assets", "ui")
sys.path.insert(0, os.path.join(HERE, "banners"))
from rasterize import screenshot  # noqa: E402

ATLAS_W, ATLAS_H = 256, 128
PAD = 2
GLYPHS = ["all-quests", "removed", "chronicles", "chronicles-of-light", "hildibrand", "side-story", "relic", "endeavors",
          "other", "special", "festival", "deep-dungeon", "region-coerthas", "region-mordhona", "moonlit", "flight",
          "plan-fallback"]

# sprite name -> (source file, source viewBox, x, y, w, h) in 1x atlas pixels
SPRITES = {
    "crest": ("crest.svg", None, 2, 2, 40, 40),
    "corner-mark": ("corner-mark.svg", None, 46, 2, 12, 12),
    "sigil-star": ("sigil-star.svg", None, 62, 2, 12, 12),
    # The divider's middle: waxing crescent, full moon, waning crescent (the arms are drawn with primitives).
    "divider-phases": ("divider-moonroad.svg", "100 0 40 12", 78, 2, 40, 12),
}
for i, name in enumerate(GLYPHS):
    SPRITES["glyph-" + name] = (f"glyph-{name}.svg", None, PAD + (i % 8) * 28, 46 + (i // 8) * 28, 24, 24)


def inner(svg_text, prefix):
    """The body of one piece's SVG (between <svg> and </svg>, minus title/desc) with every id prefixed."""
    m = re.search(r"<svg[^>]*viewBox=\"([^\"]+)\"[^>]*>(.*)</svg>", svg_text, re.S)
    view, body = m.group(1), m.group(2)
    body = re.sub(r"<title>.*?</title>|<desc>.*?</desc>", "", body, flags=re.S)
    body = re.sub(r'id="([^"]+)"', lambda k: f'id="{prefix}{k.group(1)}"', body)
    body = re.sub(r"url\(#([^)]+)\)", lambda k: f"url(#{prefix}{k.group(1)})", body)
    body = re.sub(r'href="#([^"]+)"', lambda k: f'href="#{prefix}{k.group(1)}"', body)
    return view, body


def sheet(scale):
    parts = []
    for i, (name, (file, view, x, y, w, h)) in enumerate(SPRITES.items()):
        with open(os.path.join(ORN, file), encoding="utf-8") as fh:
            own_view, body = inner(fh.read(), f"s{i}_")
        parts.append(f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="{view or own_view}" overflow="hidden">{body}</svg>')
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{ATLAS_W * scale}" height="{ATLAS_H * scale}" '
            f'viewBox="0 0 {ATLAS_W} {ATLAS_H}">{"".join(parts)}</svg>')


def main():
    for name, (_, _, x, y, w, h) in SPRITES.items():
        assert x + w + PAD <= ATLAS_W and y + h + PAD <= ATLAS_H, name
    os.makedirs(DEST, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        for scale, suffix in ((1, ""), (2, "@2x")):
            src = os.path.join(tmp, f"atlas{suffix}.svg")
            with open(src, "w", encoding="utf-8") as fh:
                fh.write(sheet(scale))
            im = screenshot(src, os.path.join(tmp, f"raw{suffix}.png"), ATLAS_W * scale, ATLAS_H * scale, transparent=True)
            out = os.path.join(DEST, f"ornaments{suffix}.png")
            im.convert("RGBA").save(out, optimize=True)
            print(f"ornaments{suffix}.png {im.size} {os.path.getsize(out) / 1024:.1f} KB")
    layout = {
        "size": [ATLAS_W, ATLAS_H],
        "note": "Rectangles are [x, y, w, h] in 1x pixels; ornaments@2x.png is the same layout at twice the size.",
        "sprites": {name: [x, y, w, h] for name, (_, _, x, y, w, h) in SPRITES.items()},
    }
    with open(os.path.join(DEST, "ornaments.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(layout, fh, indent=2)
        fh.write("\n")


if __name__ == "__main__":
    main()
