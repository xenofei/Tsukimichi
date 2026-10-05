"""FFXIV's own fonts, read from the player's install (common/font/*.fdt and font*.tex), for the mocks.

At runtime the plugin needs none of this: Dalamud exposes the same faces as game fonts (GameFontFamily.Jupiter, Axis,
TrumpGothic, MiedingerMid), so Moonfall's type is the game's type and nothing is bundled (open question 7 of the rich
pass is answered by the game itself). Here the .fdt glyph tables and the packed atlases (four glyph planes per RGBA
texture) are read directly so the mocks show exactly those faces.

Faces used:
  * Jupiter 46: the game's display serif (zone names, duty titles): screen titles, MOONFALL, banners;
  * TrumpGothic 184 / 68: the game's condensed display capitals (the job gauges' and countdown's numbers): score,
    big counts;
  * AXIS 36: the game's UI sans (every window label): labels, body text;
  * MiedingerMid 36: the game's numeric face (damage numbers, timers): small counts.
"""
import functools
import pathlib
import struct

import numpy as np
from PIL import Image

from r2lib import CACHE, record

FONT = CACHE / "font"
FACES = {"jupiter": "Jupiter_46", "trump": "TrumpGothic_184", "trump_s": "TrumpGothic_68", "axis": "AXIS_36",
         "mied": "MiedingerMid_36", "jupiter_s": "Jupiter_23"}


@functools.lru_cache(None)
def _atlas(index):
    p = FONT / f"common_font_font{index}.png"
    record(f"common/font/font{index}.tex", "game font atlas (type in the mocks; Dalamud game fonts at runtime)")
    return np.asarray(Image.open(p).convert("RGBA"), np.float32) / 255.0


@functools.lru_cache(None)
def face(name):
    stem = FACES[name]
    b = (FONT / f"common_font_{stem}.fdt").read_bytes()
    record(f"common/font/{stem}.fdt", "game font glyph table")
    o, ko = struct.unpack_from("<ii", b, 8)
    count = struct.unpack_from("<I", b, o + 4)[0]
    tw, th, size, line_h, ascent = struct.unpack_from("<HHfII", b, o + 0x10)
    glyphs = {}
    for k in range(count):
        cu, _sj, ti, ox, oy, w, h, nx, cy = struct.unpack_from("<IHHHHBBbb", b, o + 0x20 + 16 * k)
        try:
            ch = cu.to_bytes(4, "big").lstrip(b"\0").decode("utf-8")
        except UnicodeDecodeError:
            continue
        glyphs.setdefault(ch, (ti, ox, oy, w, h, nx, cy))
    kern = {}
    if ko and b[ko:ko + 4] == b"knhd":
        kc = struct.unpack_from("<I", b, ko + 4)[0]
        for k in range(kc):
            lu, ru, _a, _b, off = struct.unpack_from("<IIHHi", b, ko + 0x10 + 16 * k)
            try:
                kern[(lu.to_bytes(4, "big").lstrip(b"\0").decode(), ru.to_bytes(4, "big").lstrip(b"\0").decode())] = off
            except UnicodeDecodeError:
                pass
    # the cell height (every glyph's bounding height is the line's), used as the em
    em = max((g[4] for g in glyphs.values()), default=line_h)
    return dict(glyphs=glyphs, kern=kern, line_h=line_h, ascent=ascent, em=em, size=size)


# the plane order within an RGBA atlas: texture index % 4 -> channel of the RGBA png (measured: see __main__)
PLANE = (0, 1, 2, 3)


class MissingGlyph(ValueError):
    pass


def glyph_mask(name, ch):
    f = face(name)
    g = f["glyphs"].get(ch)
    if g is None:
        # round 2 (UX M8: "ACED tt new best"): a glyph the face lacks is an error, never a silent fallback
        raise MissingGlyph(f"{FACES[name]} has no glyph for {ch!r} (U+{ord(ch):04X})")
    ti, ox, oy, w, h, nx, cy = g
    atlas = _atlas(ti // 4 + 1)
    return atlas[oy:oy + h, ox:ox + w, PLANE[ti % 4]], g


def render_mask(s, name, px_size, tracking=0.0):
    """The text as a coverage mask (float, 0..1) at px_size device px per em, and its advance width in device px.
    tracking in device px. Round 2 (UX m9, broken spacing at 640): the line is composed at the atlas's own resolution,
    with exact advances and kerning, and resampled once as a whole, so small sizes keep their spacing."""
    f = face(name)
    k = px_size / f["em"]
    pen = 0.0
    parts = []
    prev = None
    for ch in s:
        m, g = glyph_mask(name, ch)
        ti, ox, oy, w, h, nx, cy = g
        if prev is not None:
            pen += f["kern"].get((prev, ch), 0)
        parts.append((pen, cy, m))
        pen += w + nx + tracking / k
        prev = ch
    pad = 8
    W0 = int(np.ceil(max(pen, 1) + max((p[2].shape[1] for p in parts), default=0))) + 2 * pad
    H0 = int(f["em"]) + 2 * pad
    big = np.zeros((H0, W0), np.float32)
    for (x, cy, m) in parts:
        if m.size == 0:
            continue
        hh, ww = m.shape
        xi = int(np.floor(x)) + pad
        fx = x - np.floor(x)
        sub = m if fx < 0.01 else np.asarray(Image.fromarray(m.astype(np.float32), "F").transform(
            (ww + 1, hh), Image.AFFINE, (1, 0, -fx, 0, 1, 0), resample=Image.BILINEAR))
        Y0 = int(cy) + pad
        X1, Y1 = min(W0, xi + sub.shape[1]), min(H0, Y0 + sub.shape[0])
        big[Y0:Y1, xi:X1] = np.maximum(big[Y0:Y1, xi:X1], sub[:Y1 - Y0, :X1 - xi])
    Wt, Ht = max(1, int(round(W0 * k))), max(1, int(round(H0 * k)))
    out = np.asarray(Image.fromarray(big, "F").resize((Wt, Ht), Image.LANCZOS if k > 0.5 else Image.BOX), np.float32)
    out = np.clip(out, 0, 1)
    # trim the padding so the mask's origin matches the old layout (2 px of margin at the target size)
    off = int(round(pad * k)) - 2
    if off > 0:
        out = out[off:, off:]
    return out, pen * k


def measure(s, name, size, tracking=0.0):
    m, w = render_mask(s, name, size, tracking)
    return w


@functools.lru_cache(None)
def cap_box(name):
    """Rows of the cap height of the face, as a fraction of the em: (top, baseline)."""
    m, _ = glyph_mask(name, "H")
    rows = np.nonzero(m.max(1) > 0.4)[0]
    f = face(name)
    g = f["glyphs"]["H"]
    top = (rows[0] + g[6]) / f["em"] if len(rows) else 0.2
    base = (rows[-1] + 1 + g[6]) / f["em"] if len(rows) else 0.8
    return top, base


if __name__ == "__main__":
    # plane check: render "Moonfall 0123" with every plane mapping and save a sheet
    import sys
    out = []
    for name in ("jupiter", "trump", "axis", "mied"):
        m, w = render_mask("MOONFALL Moonfall 123,450 ×2", name, 60)
        out.append(m)
    W = max(o.shape[1] for o in out)
    sheet = np.zeros((sum(o.shape[0] for o in out), W), np.float32)
    y = 0
    for o in out:
        sheet[y:y + o.shape[0], :o.shape[1]] = o
        y += o.shape[0]
    Image.fromarray((sheet * 255).astype(np.uint8)).save(sys.argv[1] if len(sys.argv) > 1 else "fonts.png")
