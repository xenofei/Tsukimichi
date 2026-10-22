"""Review tools, the pictures: the before-and-after diff of a level and the contact sheet of a stage.

Both are plain PNGs at most MAX_W (1568) pixels wide, so a reviewer can read them whole. Board units are composite pixels
(the 800 x 600 composite is the board 1:1), and only the playfield (PLAY) is shown: the chrome around it is the same on
every level.
"""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from .precheck import PLAY

MAX_W = 1568
BG, FG, DIM = (14, 16, 28), (228, 232, 244), (150, 158, 182)
# the diff's colours (documented in README, "Diff")
ADDED, REMOVED, MOVED = (60, 224, 110), (244, 66, 66), (255, 214, 56)    # a ring round the piece
GAINED, LOST = (56, 224, 255), (255, 84, 224)                             # a dot on the piece: became / stopped being a candidate
VERDICT_COL = {"PASS": (110, 220, 130), "WARN": (240, 200, 80), "FAIL": (244, 90, 90), "REFUSED": (244, 90, 90), "N/A": DIM}

_FONTS = {}


def font(size, bold=False):
    k = (size, bold)
    if k not in _FONTS:
        f = None
        for name in (("segoeuib.ttf", "arialbd.ttf") if bold else ("segoeui.ttf", "arial.ttf")):
            try:
                f = ImageFont.truetype(name, size)
                break
            except OSError:
                continue
        _FONTS[k] = f or ImageFont.load_default(size)
    return _FONTS[k]


def to_image(arr):
    """float RGB 0..1 (as precheck.Source.image gives) -> PIL image."""
    return Image.fromarray(np.clip(np.asarray(arr) * 255 + 0.5, 0, 255).astype(np.uint8))


def playfield(arr):
    return to_image(arr).crop(PLAY)


def fit_width(img, w):
    if img.width <= w:
        return img
    return img.resize((w, round(img.height * w / img.width)), Image.LANCZOS)


def _shape_mask(size, it, grow, hollow):
    """An L mask of one piece grown by `grow`; `hollow` carves out the piece itself so only a ring is left."""
    m = Image.new("L", size, 0)
    d = ImageDraw.Draw(m)
    ox, oy = PLAY[0], PLAY[1]
    pts = [(x - ox, y - oy) for x, y in it["pts"]]
    r = it["r"]
    for rr, v in ((r + grow, 255), (r + 1.5, 0) if hollow else (None, None)):
        if rr is None:
            continue
        if len(pts) == 1:
            x, y = pts[0]
            d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=v)
        else:
            d.line(pts, fill=v, width=max(1, round(2 * rr)))
            for x, y in (pts[0], pts[-1]):
                d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=v)
    return m


def ring(img, its, colour, grow=5.0):
    """Ring each piece in `its` in `colour` (a band `grow` wide round the piece, the piece itself left visible)."""
    if not its:
        return
    m = Image.new("L", img.size, 0)
    for it in its:
        m = Image.fromarray(np.maximum(np.asarray(m), np.asarray(_shape_mask(img.size, it, grow, True))))
    img.paste(Image.new("RGB", img.size, colour), (0, 0), m)


def dot(img, its, colour, r=3.5):
    d = ImageDraw.Draw(img)
    for it in its:
        x, y = it["x"] - PLAY[0], it["y"] - PLAY[1]
        d.ellipse([x - r - 1, y - r - 1, x + r + 1, y + r + 1], fill=(0, 0, 0))
        d.ellipse([x - r, y - r, x + r, y + r], fill=colour)


def arrows(img, pairs, colour):
    d = ImageDraw.Draw(img)
    for p, q, dist in pairs:
        if dist < 3:
            continue
        a = (p["x"] - PLAY[0], p["y"] - PLAY[1])
        b = (q["x"] - PLAY[0], q["y"] - PLAY[1])
        d.line([a, b], fill=colour, width=2)
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        for s in (-0.5, 0.5):
            d.line([b, (b[0] - 9 * math.cos(ang + s), b[1] - 9 * math.sin(ang + s))], fill=colour, width=2)


def text_h(f):
    return f.getbbox("Ag")[3] + 4


def diff_image(old_arr, new_arr, d, title_old, title_new, summary):
    """Old composite beside the new one with the changed pieces marked; the summary lines under both."""
    po, pn = playfield(old_arr), playfield(new_arr)
    for img, is_old in ((po, True), (pn, False)):
        ring(img, d["removed"] if is_old else d["added"], REMOVED if is_old else ADDED)
        ring(img, [p if is_old else q for p, q, _ in d["moved"]], MOVED)
        arrows(img, d["moved"], MOVED)
        dot(img, [p if is_old else q for p, q in d["gained"]], GAINED)
        dot(img, [p if is_old else q for p, q in d["lost"]], LOST)
    gap, pad = 12, 10
    w = po.width + pn.width + gap + 2 * pad
    f, fb = font(15), font(16, True)
    head = 54
    lines = list(summary)
    foot = pad + len(lines) * text_h(f)
    canvas = Image.new("RGB", (w, head + po.height + foot), BG)
    dr = ImageDraw.Draw(canvas)
    dr.text((pad, 6), title_old, font=fb, fill=FG)
    dr.text((pad + po.width + gap, 6), title_new, font=fb, fill=FG)
    x = pad
    for label, col, kind in (("removed", REMOVED, "ring"), ("added", ADDED, "ring"), ("moved/reshaped (arrow old to new)", MOVED, "ring"),
                             ("now a candidate", GAINED, "dot"), ("no longer a candidate", LOST, "dot")):
        if kind == "ring":
            dr.ellipse([x, 33, x + 14, 47], outline=col, width=3)
        else:
            dr.ellipse([x + 2, 35, x + 12, 45], fill=col)
        dr.text((x + 20, 31), label, font=f, fill=DIM)
        x += 20 + int(dr.textlength(label, font=f)) + 22
    canvas.paste(po, (pad, head))
    canvas.paste(pn, (pad + po.width + gap, head))
    y = head + po.height + pad // 2
    for ln in lines:
        dr.text((pad, y), ln, font=f, fill=FG)
        y += text_h(f)
    return fit_width(canvas, MAX_W)


def sheet_image(title, cells, cols=5, engine=None):
    """A contact sheet. `cells` is a list of dicts: composite (float RGB array or None), caption lines [(text, colour, bold)];
    `engine` an optional list of PIL images (the plugin's renders) in the same order, drawn in a second band per row."""
    pad, gap = 10, 10
    cols = max(1, min(cols, len(cells)))
    cw = (MAX_W - 2 * pad - gap * (cols - 1)) // cols
    f, fb = font(14), font(15, True)
    th = text_h(f)
    rows = [cells[i:i + cols] for i in range(0, len(cells), cols)]
    erows = [engine[i:i + cols] for i in range(0, len(cells), cols)] if engine else None
    ph = round(cw * (PLAY[3] - PLAY[1]) / (PLAY[2] - PLAY[0]))
    eh = round(cw * 800 / 1280) if engine else 0
    cap = 6 * th + 6
    rh = ph + (eh + gap if engine else 0) + cap
    H = 34 + len(rows) * (rh + gap)
    canvas = Image.new("RGB", (MAX_W, H), BG)
    dr = ImageDraw.Draw(canvas)
    dr.text((pad, 6), title, font=font(18, True), fill=FG)
    for ri, row in enumerate(rows):
        y0 = 34 + ri * (rh + gap)
        for ci, c in enumerate(row):
            x0 = pad + ci * (cw + gap)
            if c.get("composite") is not None:
                canvas.paste(playfield(c["composite"]).resize((cw, ph), Image.LANCZOS), (x0, y0))
            else:
                dr.rectangle([x0, y0, x0 + cw, y0 + ph], outline=DIM)
                dr.text((x0 + 8, y0 + 8), "no composite", font=f, fill=DIM)
            y = y0 + ph
            if erows:
                e = erows[ri][ci] if ci < len(erows[ri]) else None
                y += gap // 2
                if e is not None:
                    canvas.paste(e.convert("RGB").resize((cw, eh), Image.LANCZOS), (x0, y))
                else:
                    dr.rectangle([x0, y, x0 + cw, y + eh], outline=DIM)
                    dr.text((x0 + 8, y + 8), "no engine render", font=f, fill=DIM)
                y += eh + gap // 2
            y += 4
            for text, col, bold in c["caption"][:6]:
                ff = fb if bold else f
                while dr.textlength(text, font=ff) > cw and len(text) > 4:
                    text = text[:-2]
                dr.text((x0, y), text, font=ff, fill=col)
                y += th
    return canvas
