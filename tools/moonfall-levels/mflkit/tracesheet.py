"""The tracing sheet: the graded scene at 2x with a 50-unit grid, the board's opening, the launcher's swing, the
bucket's lane, the scene's feature polylines (labelled) and, when a layout exists, its pieces: orange candidates
ringed in orange, pegs that may never be green crossed, points a trace had to skip in red. Read coordinates off it,
correct the features in the recipe, and run it again."""
import math

from PIL import Image, ImageDraw, ImageFont

from . import paths  # noqa: F401
from .paths import BUILD
import rich_lib as RL


def sheet(recipe, board=None, S=2, out=None):
    from .build import _scene_cached
    px = _scene_cached(recipe, S)
    im = Image.fromarray(RL.to_u8(px)).convert("RGBA")
    lay = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(lay)
    try:
        font = ImageFont.truetype("arial.ttf", 11 * S // 2 + 6)
    except OSError:
        font = ImageFont.load_default()
    for g in range(0, 801, 50):
        dr.line([(g * S, 0), (g * S, 600 * S)], fill=(255, 255, 255, 60 if g % 100 else 110), width=1)
        dr.text((g * S + 2, 2), str(g), fill=(255, 255, 255, 200), font=font)
    for g in range(0, 601, 50):
        dr.line([(0, g * S), (800 * S, g * S)], fill=(255, 255, 255, 60 if g % 100 else 110), width=1)
        dr.text((2, g * S + 2), str(g), fill=(255, 255, 255, 200), font=font)
    dr.rectangle([75 * S, 41 * S, 725 * S, 594 * S], outline=(255, 220, 120, 200), width=2)
    r = (85 + 10) * S
    dr.ellipse([400 * S - r, 87 * S - r, 400 * S + r, 87 * S + r], outline=(255, 90, 90, 200), width=2)
    dr.rectangle([75 * S, 520 * S, 725 * S, 560 * S], outline=(255, 90, 90, 120), width=1)
    for name, feat in (recipe.get("features") or {}).items():
        if isinstance(feat, dict) and "circle" in feat:
            cx, cy, R = feat["circle"]
            a0, sw = feat.get("from", 180), feat.get("sweep", 180)
            feat = {"points": [[cx + R * math.cos(math.radians(a0 + sw * k / 24)), cy + R * math.sin(math.radians(a0 + sw * k / 24))]
                               for k in range(25)]}
        if (isinstance(feat, dict) and "points" not in feat) or not isinstance(feat, (list, dict)) \
                or (isinstance(feat, list) and feat and not isinstance(feat[0], (list, tuple))):
            continue                                               # a parametric feature (the layout reads it)
        pts = feat["points"] if isinstance(feat, dict) else feat
        pts = [p[:2] if not isinstance(p[0], (list, tuple)) else p for p in pts]
        if pts and isinstance(pts[0][0], (list, tuple)):          # a list of segments (a figure's lines)
            for (a, c) in pts:
                dr.line([(a[0] * S, a[1] * S), (c[0] * S, c[1] * S)], fill=(120, 255, 220, 160), width=1)
            continue
        sm = feat.get("smooth", 0) if isinstance(feat, dict) else 0
        P = RL.catmull([tuple(p) for p in pts], sm) if sm else pts
        dr.line([(x * S, y * S) for (x, y) in P], fill=(120, 255, 220, 230), width=2)
        for (x, y) in pts:
            dr.ellipse([x * S - 3, y * S - 3, x * S + 3, y * S + 3], fill=(120, 255, 220, 255))
        dr.text((pts[0][0] * S + 6, pts[0][1] * S - 16), name, fill=(120, 255, 220, 255), font=font)
    if board is not None:
        for p in board.pegs:
            rr = p.get("r", 10) * S
            col = (255, 160, 60, 255) if p["canBeOrange"] else (200, 220, 255, 220)
            dr.ellipse([p["x"] * S - rr, p["y"] * S - rr, p["x"] * S + rr, p["y"] * S + rr], outline=col, width=3)
            if not p.get("canBeGreen", True):
                dr.line([(p["x"] * S - rr * 0.6, p["y"] * S), (p["x"] * S + rr * 0.6, p["y"] * S)], fill=col, width=2)
        from layout import brick_samples
        for bk in board.bricks:
            pts = [(x * S, y * S) for (x, y) in brick_samples(bk, 2.0)]
            col = (255, 160, 60, 255) if bk["canBeOrange"] else (200, 220, 255, 230)
            dr.line(pts, fill=col, width=int(bk["thickness"] * S * 0.8))
        for ((x, y), tag, why) in board.skipped:
            dr.line([(x * S - 8, y * S - 8), (x * S + 8, y * S + 8)], fill=(255, 40, 40, 255), width=3)
            dr.line([(x * S - 8, y * S + 8), (x * S + 8, y * S - 8)], fill=(255, 40, 40, 255), width=3)
    im = Image.alpha_composite(im, lay).convert("RGB")
    out = out or BUILD / "trace" / f"{recipe['name']}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out)
    return out
