# Packs the 1.15 giver-portrait sprites (spec-1.15 A4, A6) into the plugin's portrait atlas (feature plan v7 F2, F5).
#
# Writes Tsukimichi/assets/ui/portraits.png (1024 x 616), portraits@2x.png (2048 x 1232, the same layout doubled) and
# portraits.json (each sprite's rectangle per tier in 1x pixels). The layout is mirrored by
# Tsukimichi.Core/Ui/PortraitAtlasLayout.cs; a test holds the two to each other and to the PNG sizes, so change both
# together.
#
# Every sprite is the plate's whole 72-unit box, so the plugin draws it over the plate's square and nothing is placed
# twice:
#   - the 16 race silhouettes (make_silhouettes.py), set where the mock sets them (64 units at 4, 6) and clipped to the
#     face circle (r 34.5), one ink, MoonstoneHigh #C9D3EA at .86, as drawn;
#   - the neutral moon disc (moon-disc.svg) at 4, 4, clipped the same way;
#   - the plate shade, Full's lip shadow and moonlight wash in one: Abyss #080B16 at .55 as a crescent (the well minus
#     itself offset 2.6, 3.4), blurred 1.4, then the wash (#FFF0BE .07, radial from 20, 16, r 40), both inside the face
#     circle. Drawn over the face; "over" is associative, so baking the two together is the same as drawing each.
#
# Each sprite is drawn at every tier (24, 48, 72 and 128 px at 1x; the 2x atlas doubles them), so the plugin never
# shrinks a sprite by more than 1.5x (image textures have one mip level). Sprites sit 2 px apart (4 px at 2x) so bilinear
# sampling never bleeds a neighbour in. Chrome headless draws the sheet on a transparent background, as the medal atlas
# does (docs/design/moon-v6/round5/gen_atlas.py).
# Usage: python docs/design/v7/ui/1.15/silhouettes/gen_portrait_atlas.py
import json
import os
import re
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", "..", ".."))
DEST = os.path.join(REPO, "Tsukimichi", "assets", "ui")
sys.path.insert(0, os.path.join(REPO, "docs", "design", "moon-road", "banners"))
from rasterize import screenshot  # noqa: E402

PAD = 2
WIDTH = 1024
TIERS = [24, 48, 72, 128]

RACES = ["hyur", "elezen", "lalafell", "miqote", "roegadyn", "aura", "hrothgar", "viera"]

# sprite key -> source. Order is PortraitSprite's: race (ENpcBase.Race 1-8) by gender (0 male, 1 female), then the
# moon disc and the plate shade.
SPRITES = [(f"{race}-{gender}", f"{race}-{gender}.svg") for race in RACES for gender in ("male", "female")]
SPRITES += [("moon-disc", "moon-disc.svg"), ("plate-shade", None)]

# Where a figure sits in the plate's 72-unit box (the mock's plate(): silhouettes at 4, 6; the disc centred at 4, 4).
OFFSET = {"moon-disc": (4, 4)}
DEFAULT_OFFSET = (4, 6)


def layout():
    """name -> {tier: (x, y, w, h)}: each tier is a band of rows, as many sprites to a row as the width holds."""
    rects = {name: {} for name, _ in SPRITES}
    y = PAD
    for cell in TIERS:
        per_row = min(len(SPRITES), (WIDTH - PAD) // (cell + PAD))
        for i, (name, _) in enumerate(SPRITES):
            rects[name][cell] = (PAD + (i % per_row) * (cell + PAD), y + (i // per_row) * (cell + PAD), cell, cell)
        rows = -(-len(SPRITES) // per_row)
        y += rows * (cell + PAD)
    return rects, y


def figure(name, path):
    """A silhouette's group, placed in the plate box (overflow visible: the shoulders run past the 64-unit box)."""
    with open(os.path.join(HERE, path), encoding="utf-8") as fh:
        text = fh.read()
    body = re.search(r"<svg[^>]*>(.*)</svg>", text, re.S).group(1)
    body = re.sub(r"<title>.*?</title>", "", body, flags=re.S)
    x, y = OFFSET.get(name, DEFAULT_OFFSET)
    return f'<svg x="{x}" y="{y}" width="64" height="64" viewBox="0 0 64 64" overflow="visible">{body}</svg>'


def sprite_body(name, path, prefix):
    clip = f'<clipPath id="{prefix}c"><circle cx="36" cy="36" r="34.5"/></clipPath>'
    if path is not None:
        return f"<defs>{clip}</defs><g clip-path=\"url(#{prefix}c)\">{figure(name, path)}</g>"
    defs = (clip
            + f'<mask id="{prefix}m" maskUnits="userSpaceOnUse" x="0" y="0" width="72" height="72"><rect width="72" height="72" fill="#fff"/>'
            + '<circle cx="38.6" cy="39.4" r="34.5" fill="#000"/></mask>'
            + f'<filter id="{prefix}b" x="-10%" y="-10%" width="120%" height="120%" color-interpolation-filters="sRGB">'
            + '<feGaussianBlur stdDeviation="1.4"/></filter>'
            + f'<radialGradient id="{prefix}h" cx="20" cy="16" r="40" gradientUnits="userSpaceOnUse">'
            + '<stop offset="0" stop-color="#FFF0BE" stop-opacity=".07"/><stop offset="1" stop-color="#FFF0BE" stop-opacity="0"/>'
            + "</radialGradient>")
    return (f"<defs>{defs}</defs><g clip-path=\"url(#{prefix}c)\">"
            + f'<circle cx="36" cy="36" r="35.5" fill="#080B16" fill-opacity=".55" mask="url(#{prefix}m)" filter="url(#{prefix}b)"/>'
            + f'<circle cx="36" cy="36" r="35" fill="url(#{prefix}h)"/></g>')


def sheet(scale, rects, height):
    parts = []
    for i, (name, path) in enumerate(SPRITES):
        for t, cell in enumerate(TIERS):
            x, y, w, h = rects[name][cell]
            # Each copy gets its own id prefix: a clip or filter shared across nested <svg>s would resolve to the first.
            parts.append(f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="0 0 72 72" overflow="hidden">'
                         f"{sprite_body(name, path, f'p{i}t{t}_')}</svg>")
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH * scale}" height="{height * scale}" '
            f'viewBox="0 0 {WIDTH} {height}">{"".join(parts)}</svg>')


def main():
    rects, height = layout()
    os.makedirs(DEST, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        for scale, suffix in ((1, ""), (2, "@2x")):
            src = os.path.join(tmp, f"portraits{suffix}.svg")
            with open(src, "w", encoding="utf-8") as fh:
                fh.write(sheet(scale, rects, height))
            im = screenshot(src, os.path.join(tmp, f"raw{suffix}.png"), WIDTH * scale, height * scale, transparent=True)
            out = os.path.join(DEST, f"portraits{suffix}.png")
            im.convert("RGBA").save(out, optimize=True)
            print(f"portraits{suffix}.png {im.size} {os.path.getsize(out) / 1024:.1f} KB")
    data = {
        "size": [WIDTH, height],
        "tiers": TIERS,
        "note": "Rectangles are [x, y, w, h] in 1x pixels, per tier; portraits@2x.png is the same layout at twice the size. "
                "Each sprite is the plate's whole 72-unit box.",
        "sprites": {name: {str(cell): list(r) for cell, r in by_tier.items()} for name, by_tier in rects.items()},
    }
    with open(os.path.join(DEST, "portraits.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(data, fh, indent=2)
        fh.write("\n")


if __name__ == "__main__":
    main()
