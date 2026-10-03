# Packs Menphina's Medallion (round 5, medallion-r5/*.svg) into the plugin's hero-size medal atlas (feature plan v6 G1).
#
# Writes Tsukimichi/assets/ui/medals.png (782 x 574), medals@2x.png (1564 x 1148, the same layout doubled) and
# medals.json (each sprite's rectangle per tier in 1x pixels). The layout is mirrored by Tsukimichi.Core/Ui/MedalLayout.cs;
# a test holds the two to each other and to the PNG sizes, so change both together.
#
# Sprites: the seven state medals as shipped (badges included: the open lock on Ready, the journal on In journal, the
# closed lock on Blocked), and Ready on another job once per role seat with the seat left empty. The plugin draws the
# game's own job icon into that seat at runtime (concept.md, "Job-badge frame spec"), so no game art is packed here.
# The empty-seat medals come from gen5.py itself: its badge() is reused with the lock left out and the role's enamel
# swapped in, so the frame is the approved one to the unit.
#
# Each sprite is drawn at every tier (48, 64, 96 and 128 px at 1x), so the plugin never shrinks a sprite by more than
# 1.5x (image textures have one mip level). Sprites sit 2 px apart (4 px at 2x) so bilinear sampling never bleeds a
# neighbour in. Chrome headless draws the sheet on a transparent background, as gen_atlas.py does for the ornaments.
# Usage: python docs/design/moon-v6/round5/gen_atlas.py
import json
import os
import re
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
MEDALS = os.path.join(HERE, "medallion-r5")
DEST = os.path.join(REPO, "Tsukimichi", "assets", "ui")
sys.path.insert(0, os.path.join(REPO, "docs", "design", "moon-road", "banners"))
sys.path.insert(0, os.path.join(MEDALS, "_src"))
from rasterize import screenshot  # noqa: E402
import gen5  # noqa: E402

PAD = 2
WIDTH, HEIGHT = 782, 574
TIERS = [48, 64, 96, 128]

# The DoH/DoL seat (no combat role): a slate enamel, so the game's gold job glyph keeps its contrast. Not in round 5's
# palette; GlyphTokens.Medallion.HandHex holds the same value.
HAND = ("#66708E", "#2C324C")

# sprite key -> source: a file in medallion-r5/ or a role seat for Ready on another job. Order is MedalSprite's.
SPRITES = [
    ("ready", "ready.svg"),
    ("in-journal", "in-journal.svg"),
    ("blocked", "blocked.svg"),
    ("done-this-cycle", "done-this-cycle.svg"),
    ("completed", "completed.svg"),
    ("locked-out", "locked-out.svg"),
    ("not-checked", "not-checked.svg"),
    ("other-job-tank", ("tank",)),
    ("other-job-healer", ("healer",)),
    ("other-job-dps", ("dps",)),
    ("other-job-hand", ("hand",)),
]


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
    assert y <= HEIGHT, f"layout needs {y} px, atlas is {HEIGHT}"
    return rects


def empty_seat_medal(role):
    """Ready on another job with the role's enamel seat and nothing in it: gen5's own medal and badge frame, with the
    badge's content (the lock it draws for "open") left out and the seat colour swapped for the role's."""
    seat = gen5.ROLE[role] if role in gen5.ROLE else HAND
    real_badge, real_lock, real_seat = gen5.badge, gen5.lock_glyph, dict(gen5.SEAT)

    def seat_only(p, kind, job=None):
        return real_badge(p, "open")

    try:
        gen5.SEAT["open"] = seat
        gen5.lock_glyph = lambda p, cx, cy, open_: ([], [])
        gen5.badge = seat_only
        return gen5.ready_other_job(gen5.DEFAULT_JOB)
    finally:
        gen5.badge, gen5.lock_glyph = real_badge, real_lock
        gen5.SEAT.clear()
        gen5.SEAT.update(real_seat)


def source(spec):
    if isinstance(spec, tuple):
        return empty_seat_medal(spec[0])
    with open(os.path.join(MEDALS, spec), encoding="utf-8") as fh:
        return fh.read()


def inner(svg_text, prefix):
    """The body of one medal's SVG (between <svg> and </svg>, minus its title) with every id prefixed."""
    m = re.search(r"<svg[^>]*viewBox=\"([^\"]+)\"[^>]*>(.*)</svg>", svg_text, re.S)
    view, body = m.group(1), m.group(2)
    body = re.sub(r"<title>.*?</title>", "", body, flags=re.S)
    body = re.sub(r'id="([^"]+)"', lambda k: f'id="{prefix}{k.group(1)}"', body)
    body = re.sub(r"url\(#([^)]+)\)", lambda k: f"url(#{prefix}{k.group(1)})", body)
    body = re.sub(r'href="#([^"]+)"', lambda k: f'href="#{prefix}{k.group(1)}"', body)
    return view, body


def sheet(scale, rects, bodies):
    parts = []
    for i, (name, _) in enumerate(SPRITES):
        view, body = bodies[name]
        for t, cell in enumerate(TIERS):
            x, y, w, h = rects[name][cell]
            # Each copy gets its own id prefix: a filter or clip shared across nested <svg>s would resolve to the first.
            body_t = re.sub(r'(id="|url\(#|href="#)m{0}_'.format(i), lambda k: f"{k.group(1)}m{i}t{t}_", body)
            parts.append(f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="{view}" overflow="hidden">{body_t}</svg>')
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH * scale}" height="{HEIGHT * scale}" '
            f'viewBox="0 0 {WIDTH} {HEIGHT}">{"".join(parts)}</svg>')


def main():
    rects = layout()
    bodies = {name: inner(source(spec), f"m{i}_") for i, (name, spec) in enumerate(SPRITES)}
    os.makedirs(DEST, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        for scale, suffix in ((1, ""), (2, "@2x")):
            src = os.path.join(tmp, f"medals{suffix}.svg")
            with open(src, "w", encoding="utf-8") as fh:
                fh.write(sheet(scale, rects, bodies))
            im = screenshot(src, os.path.join(tmp, f"raw{suffix}.png"), WIDTH * scale, HEIGHT * scale, transparent=True)
            out = os.path.join(DEST, f"medals{suffix}.png")
            im.convert("RGBA").save(out, optimize=True)
            print(f"medals{suffix}.png {im.size} {os.path.getsize(out) / 1024:.1f} KB")
    data = {
        "size": [WIDTH, HEIGHT],
        "tiers": TIERS,
        "note": "Rectangles are [x, y, w, h] in 1x pixels, per tier; medals@2x.png is the same layout at twice the size.",
        "sprites": {name: {str(cell): list(r) for cell, r in by_tier.items()} for name, by_tier in rects.items()},
    }
    with open(os.path.join(DEST, "medals.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(data, fh, indent=2)
        fh.write("\n")


if __name__ == "__main__":
    main()
