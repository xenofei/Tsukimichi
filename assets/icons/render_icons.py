"""Render the Tsukimichi icon and the quest-state moon glyphs with Pillow.

The composition is the same one authored in tsukimichi.svg (see that file for
the geometry in SVG form). Nothing here parses the SVG; the shapes are
re-expressed procedurally from the same numbers. Everything is drawn from
circles, polygons and arcs. No fonts, no external images.

Outputs (paths relative to the repository root):
    assets/icon.png                       512 x 512 RGBA manifest icon
    assets/icon-64.png                    64 x 64 RGBA preview at list size
    assets/icons/moon-phases-preview.png  contact sheet of the 8 state glyphs

Usage: python assets/icons/render_icons.py
"""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

# ---------------------------------------------------------------------------
# Color tokens (spec 2.3)
# ---------------------------------------------------------------------------
NIGHT = (0x0F, 0x14, 0x24)
MOON = (0xF2, 0xD2, 0x7A)
SILVER = (0xDD, 0xE3, 0xF0)
DUSK = (0x7C, 0x86, 0xA8)
ECLIPSE = (0xB2, 0x5C, 0x7F)
VEIL = (0x4A, 0x52, 0x70)
# Dark disc used by the unlit glyph states: Night lifted halfway toward Veil so
# it stays visible on a Night panel.
DARK_DISC = tuple((n + v) // 2 for n, v in zip(NIGHT, VEIL))

SS = 4  # supersampling factor

# ---------------------------------------------------------------------------
# Icon geometry (512 x 512 design space, identical to tsukimichi.svg)
# ---------------------------------------------------------------------------
SIZE = 512
CORNER = 112                      # rounded-square radius

MOON_C, MOON_R = (172, 160), 100  # crescent outer disc
BITE_C, BITE_R = (214, 138), 88   # disc subtracted to make the crescent
MOON_GLOW_R, MOON_GLOW_A = 175, 0.16   # soft radial halo behind the crescent

HORIZON_Y = 262
# Path edges as cubic Beziers, from the vanishing point down past the bottom.
PATH_L = [(326, 262), (330, 340), (40, 360), (70, 530)]
PATH_R = [(334, 262), (372, 340), (240, 400), (440, 530)]
PATH_A = 0.26

STONES = [(0.42, 6), (0.66, 10), (0.88, 15)]   # (t along path, radius)
STONE_GLOW = [(2.6, 0.12), (1.7, 0.18)]        # (radius multiplier, alpha) halos

STARS = [(398, 104, 11), (456, 186, 8), (300, 64, 6)]  # (cx, cy, outer r)
STAR_INNER = 0.38


def with_alpha(rgb: tuple[int, int, int], a: float) -> tuple[int, int, int, int]:
    return (*rgb, round(255 * a))


def bezier(p, t: float) -> tuple[float, float]:
    (x0, y0), (x1, y1), (x2, y2), (x3, y3) = p
    u = 1 - t
    a, b, c, d = u * u * u, 3 * u * u * t, 3 * u * t * t, t * t * t
    return (a * x0 + b * x1 + c * x2 + d * x3, a * y0 + b * y1 + c * y2 + d * y3)


def path_center(t: float) -> tuple[float, float]:
    lx, ly = bezier(PATH_L, t)
    rx, ry = bezier(PATH_R, t)
    return ((lx + rx) / 2, (ly + ry) / 2)


def star_points(cx: float, cy: float, r: float, inner: float = STAR_INNER):
    pts = []
    for k in range(8):
        ang = math.radians(-90 + 45 * k)
        rad = r if k % 2 == 0 else r * inner
        pts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang)))
    return pts


class Canvas:
    """RGBA canvas at SS x scale with proper alpha blending per shape."""

    def __init__(self, w: int, h: int, bg=None):
        self.w, self.h = w * SS, h * SS
        self.img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        if bg is not None:
            self.img.paste((*bg, 255), (0, 0, self.w, self.h))

    def _s(self, v):
        return v * SS

    def _layer(self):
        layer = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        return layer, ImageDraw.Draw(layer)

    def _commit(self, layer):
        self.img = Image.alpha_composite(self.img, layer)

    def circle(self, cx, cy, r, fill):
        layer, d = self._layer()
        s = self._s
        d.ellipse([s(cx - r), s(cy - r), s(cx + r), s(cy + r)], fill=fill)
        self._commit(layer)

    def ring(self, cx, cy, r, width, fill):
        layer, d = self._layer()
        s = self._s
        d.ellipse([s(cx - r), s(cy - r), s(cx + r), s(cy + r)],
                  outline=fill, width=max(1, round(s(width))))
        self._commit(layer)

    def arc(self, cx, cy, r, start, end, width, fill):
        layer, d = self._layer()
        s = self._s
        d.arc([s(cx - r), s(cy - r), s(cx + r), s(cy + r)], start, end,
              fill=fill, width=max(1, round(s(width))))
        self._commit(layer)

    def polygon(self, pts, fill):
        layer, d = self._layer()
        d.polygon([(self._s(x), self._s(y)) for x, y in pts], fill=fill)
        self._commit(layer)

    def rounded_square(self, x, y, w, h, radius, fill):
        layer, d = self._layer()
        s = self._s
        d.rounded_rectangle([s(x), s(y), s(x + w), s(y + h)],
                            radius=s(radius), fill=fill)
        self._commit(layer)

    def clip_rounded_square(self, x, y, w, h, radius):
        s = self._s
        mask = Image.new("L", (self.w, self.h), 0)
        ImageDraw.Draw(mask).rounded_rectangle(
            [s(x), s(y), s(x + w), s(y + h)], radius=s(radius), fill=255)
        self.img.putalpha(ImageChops.multiply(self.img.getchannel("A"), mask))

    def two_discs(self, cx, cy, r, bx, by, br, fill, keep: str):
        """Boolean of disc A (cx, cy, r) with disc B (bx, by, br).

        keep="minus": A minus B (a crescent).
        keep="and":   A intersect B (a lit portion whose terminator is B's rim).
        """
        s = self._s
        a = Image.new("L", (self.w, self.h), 0)
        ImageDraw.Draw(a).ellipse([s(cx - r), s(cy - r), s(cx + r), s(cy + r)], fill=255)
        b = Image.new("L", (self.w, self.h), 0)
        ImageDraw.Draw(b).ellipse([s(bx - br), s(by - br), s(bx + br), s(by + br)], fill=255)
        if keep == "minus":
            mask = ImageChops.subtract(a, b)
        elif keep == "and":
            mask = ImageChops.multiply(a, b)
        else:
            raise ValueError(keep)
        layer = Image.new("RGBA", (self.w, self.h), fill)
        layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
        self._commit(layer)

    def crescent(self, cx, cy, r, bx, by, br, fill):
        self.two_discs(cx, cy, r, bx, by, br, fill, "minus")

    def soft_glow(self, cx, cy, r, rgb, alpha, steps: int = 32):
        """Radial halo: alpha falls off quadratically from the centre to r."""
        layer, d = self._layer()
        s = self._s
        for i in range(steps, 0, -1):
            k = i / steps
            rr = r * k
            a = alpha * (1 - k) ** 2
            d.ellipse([s(cx - rr), s(cy - rr), s(cx + rr), s(cy + rr)],
                      fill=with_alpha(rgb, a))
        self._commit(layer)

    def down(self, w: int, h: int) -> Image.Image:
        return self.img.resize((w, h), Image.LANCZOS)


# ---------------------------------------------------------------------------
# Icon
# ---------------------------------------------------------------------------
def render_icon() -> Canvas:
    c = Canvas(SIZE, SIZE)
    c.rounded_square(0, 0, SIZE, SIZE, CORNER, (*NIGHT, 255))

    # Faint ground below the horizon, then the path itself.
    c.polygon([(0, HORIZON_Y), (SIZE, HORIZON_Y), (SIZE, SIZE), (0, SIZE)],
              with_alpha(SILVER, 0.04))
    n = 48
    left = [bezier(PATH_L, i / n) for i in range(n + 1)]
    right = [bezier(PATH_R, i / n) for i in range(n + 1)]
    c.polygon(left + right[::-1], with_alpha(SILVER, PATH_A))

    # Crescent with a soft halo.
    c.soft_glow(*MOON_C, MOON_GLOW_R, MOON, MOON_GLOW_A)
    c.crescent(*MOON_C, MOON_R, *BITE_C, BITE_R, (*MOON, 255))

    # Stars.
    for sx, sy, sr in STARS:
        c.polygon(star_points(sx, sy, sr), (*SILVER, 255))

    # Stones on the path, glow first.
    for mul, a in STONE_GLOW:
        for t, r in STONES:
            x, y = path_center(t)
            c.circle(x, y, r * mul, with_alpha(MOON, a))
    for t, r in STONES:
        x, y = path_center(t)
        c.circle(x, y, r, (*MOON, 255))

    c.clip_rounded_square(0, 0, SIZE, SIZE, CORNER)
    return c


# ---------------------------------------------------------------------------
# Quest-state moon glyphs (spec 2.1): base disc + offset terminator disc
# ---------------------------------------------------------------------------
def glyph(c: Canvas, cx: float, cy: float, r: float, state: str) -> None:
    """Draw one quest-state moon. Every lit region is the base disc intersected
    with a second, offset disc whose rim acts as the terminator."""
    ring_w = r * 0.07
    big = r * 6          # a very large offset disc gives a near-straight terminator

    def lit(color, dx, rad):
        c.two_discs(cx, cy, r, cx + dx, cy, rad, (*color, 255), "and")

    if state == "full":
        c.circle(cx, cy, r, (*MOON, 255))

    elif state == "waxing_gibbous":              # right ~75% gold, thin ring
        c.circle(cx, cy, r, (*DARK_DISC, 255))
        lit(MOON, +0.5 * r, r)
        c.ring(cx, cy, r, ring_w, (*MOON, 255))

    elif state == "first_quarter":               # right half gold, outer glow
        c.soft_glow(cx, cy, r * 1.7, MOON, 0.35, steps=16)
        c.circle(cx, cy, r, (*DARK_DISC, 255))
        lit(MOON, +big, big)

    elif state == "first_quarter_silver":        # right half silver, gold ring
        c.circle(cx, cy, r, (*DARK_DISC, 255))
        lit(SILVER, +big, big)
        c.ring(cx, cy, r, ring_w, (*MOON, 255))

    elif state == "waning_gibbous":              # left ~75% silver
        c.circle(cx, cy, r, (*DARK_DISC, 255))
        lit(SILVER, -0.5 * r, r)

    elif state == "new":                         # dark, thin silver ring
        c.circle(cx, cy, r, (*DARK_DISC, 255))
        c.ring(cx, cy, r, ring_w, (*SILVER, 255))

    elif state == "eclipsed":                    # dark, eclipse ring, notch
        c.circle(cx, cy, r, (*DARK_DISC, 255))
        c.ring(cx, cy, r, ring_w * 1.3, (*ECLIPSE, 255))
        nx, ny = cx + r * 0.72, cy - r * 0.72
        c.circle(nx, ny, r * 0.30, (*NIGHT, 255))   # notch bitten from the rim

    elif state == "veiled":                      # dark, dashed ring
        c.circle(cx, cy, r, (*DARK_DISC, 255))
        for k in range(12):
            start = k * 30 - 90
            c.arc(cx, cy, r, start, start + 16, ring_w, (*DUSK, 255))

    else:
        raise ValueError(state)


GLYPH_ORDER = [
    "full",                  # Completed
    "waxing_gibbous",        # Accepted
    "first_quarter",         # Ready
    "first_quarter_silver",  # ReadyOnOtherJob
    "waning_gibbous",        # DoneThisCycle
    "new",                   # Blocked
    "eclipsed",              # Foreclosed
    "veiled",                # Unknown
]


def render_phase_sheet(cell: int = 96) -> Canvas:
    c = Canvas(cell * len(GLYPH_ORDER), cell, bg=NIGHT)
    for i, state in enumerate(GLYPH_ORDER):
        glyph(c, cell * (i + 0.5), cell * 0.5, cell * 0.30, state)
    return c


# ---------------------------------------------------------------------------
def main() -> None:
    here = Path(__file__).resolve().parent
    assets = here.parent

    icon = render_icon()
    icon.down(512, 512).save(assets / "icon.png")
    icon.down(64, 64).save(assets / "icon-64.png")

    sheet = render_phase_sheet()
    sheet.down(sheet.w // SS, sheet.h // SS).save(here / "moon-phases-preview.png")

    print("wrote", assets / "icon.png")
    print("wrote", assets / "icon-64.png")
    print("wrote", here / "moon-phases-preview.png")


if __name__ == "__main__":
    main()
