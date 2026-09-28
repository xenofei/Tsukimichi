"""Rasterize the glyph design sheet to docs/design/glyphs/glyphs-v2.png.

Strategy (in order):
  1. cairosvg, if importable, renders glyphs-v2.svg directly.
  2. Otherwise `pip install cairosvg` is attempted once and step 1 retried.
     (On Windows the wheel installs but libcairo is usually missing, so this
     typically falls through.)
  3. Fallback: the same sheet is re-expressed procedurally with Pillow from
     the same numbers as the SVG, the way assets/icons/render_icons.py does
     for the plugin icon. Circles, arcs, polygons, boolean disc masks, and
     (for the interior detail, proposal.md section 3.6) soft ellipses and
     blurred bands.

Also writes glyphs-v2-zoom.png: a 4x nearest-neighbour blow-up of the small
rows (Accepted candidates at 24/16/12 px, state row at 32/24/16 px, the 24/16 px
halos and the 20 px tree) so legibility can be judged pixel by pixel.

Usage: python docs/design/glyphs/render_preview.py
"""

from __future__ import annotations

import math
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SVG = HERE / "glyphs-v2.svg"
PNG = HERE / "glyphs-v2.png"
ZOOM = HERE / "glyphs-v2-zoom.png"

W, H = 1200, 930
OFF = 190            # rows 1-4 sit this far below their v2.0 positions (row 0 was inserted)


# ---------------------------------------------------------------------------
# 1 + 2: cairosvg
# ---------------------------------------------------------------------------
def try_cairosvg() -> bool:
    try:
        import cairosvg  # noqa: F401
    except Exception:  # ImportError or a failing native import
        try:
            subprocess.run([sys.executable, "-m", "pip", "install", "cairosvg"],
                           check=False, capture_output=True, timeout=300)
            import cairosvg  # noqa: F401,F811
        except Exception:
            return False
    try:
        import cairosvg
        cairosvg.svg2png(url=str(SVG), write_to=str(PNG), output_width=W, output_height=H)
        return PNG.exists()
    except Exception as exc:  # pragma: no cover - environment dependent
        print(f"cairosvg present but failed: {exc!r}")
        return False


# ---------------------------------------------------------------------------
# 3: Pillow re-expression of the sheet
# ---------------------------------------------------------------------------
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps  # noqa: E402

try:
    import numpy as np
except Exception:  # pragma: no cover
    np = None

NIGHT = (0x0F, 0x14, 0x24)
MOON = (0xF2, 0xD2, 0x7A)
MOON_HI = (0xFF, 0xF0, 0xBE)
MOON_LO = (0xD6, 0xB2, 0x5A)
SILVER = (0xDD, 0xE3, 0xF0)
SILVER_HI = (0xFF, 0xFF, 0xFF)
SILVER_LO = (0xB9, 0xC2, 0xD8)
DUSK = (0x7C, 0x86, 0xA8)
ECLIPSE = (0xB2, 0x5C, 0x7F)
VEIL = (0x4A, 0x52, 0x70)
VEIL_LINE = (0x5C, 0x65, 0x84)  # halo track (3.19 : 1 on Night)
SHADOW = (0x3A, 0x43, 0x63)     # unlit disc (ui-revamp token), always paired with a rim
UMBRA = (0x2C, 0x33, 0x4A)      # the current UnlitDisc, kept only for the maria tint
IMGUI_BG = (0x14, 0x14, 0x14)
WHITE = (0xFF, 0xFF, 0xFF)

SS = 4

# ---- interior detail (proposal.md section 3.6), unit disc coordinates (y down) ----
DETAIL_MIN_R = 12.0           # px; below this the lit part is a clean gradient (r >= 9) or flat
BAND_W = 0.12                 # terminator glow band width, fraction of r
RIM_DARK = (0.72, 0.16)       # rim vignette: starts at 0.72 r, full strength at the rim; peak alpha (Umbra)

# maria: (cx, cy, rx, ry, rotation deg, alpha on gold, alpha on silver)
MARIA = [
    (-0.30, -0.24, 0.32, 0.24, -25, 0.20, 0.14),
    ( 0.30,  0.06, 0.24, 0.20, -15, 0.18, 0.13),
    (-0.10,  0.40, 0.30, 0.14,  10, 0.18, 0.13),
]
# craters: (cx, cy, radius, has shadow arc)
CRATERS = [
    ( 0.34, -0.46, 0.120, True),
    (-0.50,  0.30, 0.095, False),
    ( 0.10,  0.60, 0.075, False),
]
CRATER_FILL = 0.14            # Umbra
CRATER_SHADOW = (0.84, 170, 290, 0.30)   # inner radius fraction, start, end (deg, cw from 3 o'clock), alpha
CRATER_LIGHT = (0.96, -10, 110, 0.38)    # highlight edge on the lower-right, white
TONES = {
    "gold":   dict(hi=MOON_HI, mid=MOON, lo=MOON_LO, glow=(MOON_HI, 0.30), rim=1.0),
    "silver": dict(hi=SILVER_HI, mid=SILVER, lo=SILVER_LO, glow=(WHITE, 0.25), rim=0.75),
}


def rgba(rgb, a=1.0):
    return (*rgb, max(0, min(255, round(255 * a))))


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


# ---- geometry rules shared with the SVG ------------------------------------
def outline_px(r: float) -> float:
    """Rim stroke for a state moon of radius r (px): clamp(0.12 r, 1.5, 3) (ui-revamp rule)."""
    return min(3.0, max(1.5, 0.12 * r))


HALO_CORE_MIN_R = 12.0        # box half-size below which the halo is track + arc only (number beside it)


def halo_metrics(R: float):
    """Halo gauge for a box of half-size R: track radius, stroke, gap, core radius, epsilon."""
    rt = 0.80 * R
    wt = max(2.0, 0.18 * R)
    gap = max(1.0, 0.10 * R)
    rc = rt - wt / 2 - gap
    eps = max(0.06, (wt + 1.5) / (2 * math.pi * rt))
    return rt, wt, gap, rc, eps


def visual_fraction(f: float, eps: float) -> float:
    if f <= 0:
        return 0.0
    if f >= 1:
        return 1.0
    return eps + (1 - 2 * eps) * f


def terminator(r: float, w: float):
    """Circle through the poles whose equator crossing is x = (1 - 2w) r. Returns (xe, h, Rt)."""
    xe = (1 - 2 * w) * r
    if abs(xe) < 1e-6:
        return 0.0, None, None
    h = (xe * xe - r * r) / (2 * xe)
    return xe, h, math.hypot(h, r)


class Glyph:
    """A small RGBA canvas at SS x around one glyph, composited onto the sheet."""

    def __init__(self, box: float):
        self.box = box
        self.size = int(math.ceil(box * SS))
        self.c = self.size / 2
        self.img = Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))

    # -- primitives (coordinates relative to the glyph centre, y down) --------
    def _bbox(self, cx, cy, r):
        return [self.c + (cx - r) * SS, self.c + (cy - r) * SS, self.c + (cx + r) * SS, self.c + (cy + r) * SS]

    def _layer(self):
        layer = Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))
        return layer, ImageDraw.Draw(layer)

    def _commit(self, layer, mask=None):
        if mask is not None:
            layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
        self.img = Image.alpha_composite(self.img, layer)

    def _mask(self):
        return Image.new("L", (self.size, self.size), 0)

    def disc_mask(self, cx, cy, r):
        m = self._mask()
        ImageDraw.Draw(m).ellipse(self._bbox(cx, cy, r), fill=255)
        return m

    def paint(self, mask, fill):
        layer = Image.new("RGBA", (self.size, self.size), fill)
        layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
        self._commit(layer)

    def circle(self, cx, cy, r, fill):
        self.paint(self.disc_mask(cx, cy, r), fill)

    def ring(self, cx, cy, r, width, fill):
        """Stroke of `width` centred on radius r."""
        outer = self.disc_mask(cx, cy, r + width / 2)
        inner = self.disc_mask(cx, cy, max(0.0, r - width / 2))
        self.paint(ImageChops.subtract(outer, inner), fill)

    def arc(self, cx, cy, r, start, end, width, fill, caps=True, mask=None):
        """Stroked arc, degrees clockwise from 3 o'clock (Pillow convention), round caps."""
        layer, d = self._layer()
        w = max(1, round(width * SS))
        d.arc(self._bbox(cx, cy, r), start, end, fill=fill, width=w)
        if caps:
            for ang in (start, end):
                px = cx + r * math.cos(math.radians(ang))
                py = cy + r * math.sin(math.radians(ang))
                d.ellipse(self._bbox(px, py, width / 2), fill=fill)
        self._commit(layer, mask)

    def dashed_ring(self, cx, cy, r, width, n, dash_deg, fill):
        for k in range(n):
            start = k * 360 / n - 90
            self.arc(cx, cy, r, start, start + dash_deg, width, fill, caps=False)

    def polyline(self, pts, width, fill):
        layer, d = self._layer()
        d.line([(self.c + x * SS, self.c + y * SS) for x, y in pts], fill=fill,
               width=max(1, round(width * SS)), joint="curve")
        for x, y in (pts[0], pts[-1]):
            d.ellipse(self._bbox(x, y, width / 2), fill=fill)
        self._commit(layer)

    def half_right_mask(self, cx, cy, r):
        m = self._mask()
        ImageDraw.Draw(m).pieslice(self._bbox(cx, cy, r), -90, 90, fill=255)
        return m

    def lens_mask(self, cx, cy, r, ox, oy, orad):
        return ImageChops.multiply(self.disc_mask(cx, cy, r), self.disc_mask(ox, oy, orad))

    def crescent_mask(self, cx, cy, r, ox, oy, orad):
        return ImageChops.subtract(self.disc_mask(cx, cy, r), self.disc_mask(ox, oy, orad))

    def lit_mask(self, r, w, side=+1):
        """Lit region of a disc of radius r whose lit WIDTH at the equator is w (0..1).

        The terminator is a circle through the poles (MoonGeometry.FillingLayers).
        side +1 lights the right (waxing), -1 the left (waning)."""
        if w >= 1:
            return self.disc_mask(0, 0, r)
        if w <= 0:
            return self._mask()
        xe, h, Rt = terminator(r, w)
        if h is None:
            m = self.half_right_mask(0, 0, r)
        elif xe > 0:
            m = self.crescent_mask(0, 0, r, h, 0, Rt)
        else:
            m = self.lens_mask(0, 0, r, h, 0, Rt)
        return ImageOps.mirror(m) if side < 0 else m

    def shaded(self, mask, cx, cy, r, hi, mid, lo, flat=None, **_):
        """Radial shading like the SVG gradient: centre (-0.32 r, -0.34 r), radius 1.25 r."""
        if flat is not None or np is None:
            self.paint(mask, rgba(flat or mid))
            return
        ys, xs = np.mgrid[0:self.size, 0:self.size]
        gx = self.c + (cx - 0.32 * r) * SS
        gy = self.c + (cy - 0.34 * r) * SS
        t = np.sqrt((xs - gx) ** 2 + (ys - gy) ** 2) / (1.25 * r * SS)
        t = np.clip(t, 0, 1)
        hi_a, mid_a, lo_a = (np.array(c, dtype=float) for c in (hi, mid, lo))
        first = t[..., None] / 0.42
        second = (t[..., None] - 0.42) / 0.58
        col = np.where(t[..., None] < 0.42, hi_a + (mid_a - hi_a) * first, mid_a + (lo_a - mid_a) * second)
        rgb = Image.fromarray(np.clip(col, 0, 255).astype("uint8"), "RGB")
        rgb.putalpha(mask)
        self._commit(rgb)

    # -- interior detail (section 3.6) ---------------------------------------
    def mare(self, mask, cx, cy, rx, ry, rot, color, alpha):
        """Soft ellipse: full alpha inside 0.7 of its radius, fading to 0 at the edge."""
        if np is None:
            return
        ys, xs = np.mgrid[0:self.size, 0:self.size]
        x = xs - (self.c + cx * SS)
        y = ys - (self.c + cy * SS)
        cs, sn = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        xr = x * cs + y * sn
        yr = -x * sn + y * cs
        d = np.sqrt((xr / (rx * SS)) ** 2 + (yr / (ry * SS)) ** 2)
        a = np.clip((1.0 - d) / 0.30, 0, 1) * alpha * 255
        layer = Image.new("RGBA", (self.size, self.size), rgba(color))
        layer.putalpha(Image.fromarray(a.astype("uint8"), "L"))
        self._commit(layer, mask)

    def blurred(self, mask, fill, sigma_px, clip=None):
        layer = Image.new("RGBA", (self.size, self.size), fill)
        layer.putalpha(mask.filter(ImageFilter.GaussianBlur(sigma_px * SS)))
        self._commit(layer, clip)

    def vignette(self, lit, r, start, alpha, color):
        """Rim darkening: alpha rises quadratically from 0 at start*r to `alpha` at the rim."""
        if np is None:
            ring = ImageChops.subtract(self.disc_mask(0, 0, r), self.disc_mask(0, 0, r * (start + 1) / 2))
            self.blurred(ring, rgba(color, alpha), 0.06 * r, clip=lit)
            return
        ys, xs = np.mgrid[0:self.size, 0:self.size]
        t = np.hypot(xs - self.c, ys - self.c) / (r * SS)
        a = np.clip((t - start) / (1 - start), 0, 1) ** 2 * alpha * 255
        layer = Image.new("RGBA", (self.size, self.size), rgba(color))
        layer.putalpha(Image.fromarray(a.astype("uint8"), "L"))
        self._commit(layer, lit)

    def detail(self, lit, r, tone: str, band=None):
        """Maria, craters, terminator glow and rim darkening inside the lit mask `lit`.

        `band` is the terminator-glow mask (already lit-side only) or None for a full disc.
        Only called for r >= DETAIL_MIN_R. Twelve primitives in ImGui terms (imgui-notes 2b)."""
        T = TONES[tone]
        for cx, cy, rx, ry, rot, a_gold, a_silver in MARIA:
            self.mare(lit, cx * r, cy * r, rx * r, ry * r, rot, UMBRA, a_gold if tone == "gold" else a_silver)
        line = max(1.0, 0.035 * r)
        for cx, cy, rc, shadow in CRATERS:
            self.paint(ImageChops.multiply(lit, self.disc_mask(cx * r, cy * r, rc * r)), rgba(UMBRA, CRATER_FILL))
            if shadow:
                k, s, e, a = CRATER_SHADOW
                self.arc(cx * r, cy * r, rc * r * k, s, e, line, rgba(UMBRA, a), caps=False, mask=lit)
            k, s, e, a = CRATER_LIGHT
            self.arc(cx * r, cy * r, rc * r * k, s, e, line, rgba(WHITE, a * (1 if tone == "gold" else 0.8)), caps=False, mask=lit)
        if band is not None:
            col, a = T["glow"]
            self.blurred(band, rgba(col, a), 0.03 * r, clip=lit)
        start, alpha = RIM_DARK
        self.vignette(lit, r, start, alpha * T["rim"], UMBRA)

    def lit(self, mask, r, tone: str, band=None):
        """Paint a lit region: flat below r = 9, gradient from 9, gradient + detail from 12."""
        T = TONES[tone]
        if r < 9:
            self.paint(mask, rgba(T["mid"]))
            return
        self.shaded(mask, 0, 0, r, T["hi"], T["mid"], T["lo"])
        if r >= DETAIL_MIN_R:
            self.detail(mask, r, tone, band)

    def glow(self, cx, cy, r, color, alpha, steps=24):
        """Radial halo: alpha falls off quadratically from the centre to r (render_icons.py)."""
        layer, d = self._layer()
        for i in range(steps, 0, -1):
            k = i / steps
            d.ellipse(self._bbox(cx, cy, r * k), fill=rgba(color, alpha * (1 - k) ** 2))
        self._commit(layer)

    def highlight_arc(self, cx, cy, r):
        self.arc(cx, cy, 0.76 * r, 200, 252, 0.06 * r, rgba(WHITE, 0.32))

    def down(self) -> Image.Image:
        px = int(round(self.box))
        return self.img.resize((px, px), Image.LANCZOS)

    def paste_on(self, sheet: Image.Image, x: float, y: float):
        small = self.down()
        half = small.size[0] / 2
        sheet.alpha_composite(small, (int(round(x - half)), int(round(y - half))))


# ---- lit-region helpers -----------------------------------------------------
def phase(g: Glyph, r: float, w: float, side=+1):
    """Lit mask + terminator glow band for a pole-to-pole terminator at lit width w."""
    lit = g.lit_mask(r, w, side)
    band = ImageChops.subtract(lit, g.lit_mask(r, w - BAND_W / 2, side))
    return lit, band


def offset_lens(g: Glyph, r: float, ox: float):
    """v2.0 gibbous: disc intersect disc offset by ox (cusps at 0.25 r, +-0.968 r for |ox| = 0.5 r)."""
    lit = g.lens_mask(0, 0, r, ox, 0, r)
    band = ImageChops.subtract(lit, g.lens_mask(0, 0, r, ox, 0, r * (1 - BAND_W)))
    return lit, band


# ---- state glyphs -----------------------------------------------------------
STATES = ["completed", "accepted", "ready", "readyother", "donecycle", "blocked", "foreclosed", "unknown"]
ACCEPTED_W = 0.60          # chosen: early waxing gibbous, lit width 60 % (terminator equator at -0.20 r)
SEAL = (0.40, 0.16, 1.5)   # Accepted's Night "seal" dot: x as a fraction of r, radius fraction, min radius px


def seal(g: Glyph, r: float):
    x, k, mn = SEAL
    g.circle(x * r, 0, max(k * r, mn), rgba(NIGHT))


def ready_glow(g: Glyph, r: float):
    """r >= 9: soft glow (3 discs in ImGui). Below: one 1 px Moon ring at 1.25 r, 35 %."""
    if r >= 9:
        g.glow(0, 0, r * 1.75, MOON, 0.55)
    else:
        g.ring(0, 0, 1.25 * r, 1.0, rgba(MOON, 0.35))


def foreclosed(g: Glyph, r: float, o: float, rr: float):
    g.circle(0, 0, r, rgba(SHADOW))
    g.ring(0, 0, rr, o, rgba(ECLIPSE))
    bw = max(2.0, 0.22 * r)
    g.polyline([(-0.636 * r, -0.636 * r), (0.636 * r, 0.636 * r)], bw, rgba(ECLIPSE))
    if r >= 12:
        g.circle(0.72 * r, -0.72 * r, 0.30 * r, rgba(NIGHT))


def state_glyph(r: float, state: str) -> Glyph:
    g = Glyph(box=r * 2.6 + 4)          # room for the Ready glow
    o = outline_px(r)
    rr = r - o / 2

    if state == "completed":
        g.lit(g.disc_mask(0, 0, r), r, "gold")
        if r >= 16:
            g.highlight_arc(0, 0, r)
    elif state == "accepted":
        g.circle(0, 0, r, rgba(SHADOW))
        lit, band = phase(g, r, ACCEPTED_W)
        g.lit(lit, r, "gold", band)
        seal(g, r)
        g.ring(0, 0, rr, o, rgba(SILVER))
    elif state == "ready":
        ready_glow(g, r)
        g.circle(0, 0, r, rgba(SHADOW))
        g.ring(0, 0, rr, o, rgba(DUSK))
        lit, band = phase(g, r, 0.5)
        g.lit(lit, r, "gold", band)
    elif state == "readyother":
        g.circle(0, 0, r, rgba(SHADOW))
        lit, band = phase(g, r, 0.5)
        g.lit(lit, r, "silver", band)
        g.ring(0, 0, rr, o, rgba(MOON))
    elif state == "donecycle":
        g.circle(0, 0, r, rgba(SHADOW))
        g.ring(0, 0, rr, o, rgba(DUSK))
        lit, band = offset_lens(g, r, -0.5 * r)
        g.lit(lit, r, "silver", band)
    elif state == "blocked":
        g.circle(0, 0, r, rgba(SHADOW))
        g.ring(0, 0, rr, o, rgba(SILVER))
    elif state == "foreclosed":
        foreclosed(g, r, o, rr)
    elif state == "unknown":
        g.circle(0, 0, r, rgba(SHADOW, 0.60))
        if r >= 10:
            g.dashed_ring(0, 0, rr, o, 12, 16, rgba(DUSK))
        else:
            g.dashed_ring(0, 0, rr, o, 8, 22, rgba(DUSK))
    else:
        raise ValueError(state)
    return g


# ---- Accepted candidates (row 0) -------------------------------------------
CANDIDATES = ["v2.0", "a", "b", "c", "d", "e", "f"]
CHOSEN = "f"


def accepted_candidate(r: float, variant: str) -> Glyph:
    g = Glyph(box=r * 2.6 + 4)
    o = outline_px(r)
    rr = r - o / 2
    g.circle(0, 0, r, rgba(SHADOW))
    if variant == "v2.0":                      # the v2.0 sheet: 75 % offset lens, gold ring
        lit, band = offset_lens(g, r, 0.5 * r)
        g.lit(lit, r, "gold", band)
        g.ring(0, 0, rr, o, rgba(MOON))
    elif variant == "a":                       # 60 % gibbous, gold ring
        lit, band = phase(g, r, 0.60)
        g.lit(lit, r, "gold", band)
        g.ring(0, 0, rr, o, rgba(MOON))
    elif variant == "b":                       # 66 % gibbous, gold rim only over the lit side, dark side open
        lit, band = phase(g, r, 0.66)
        g.lit(lit, r, "gold", band)
        g.arc(0, 0, rr, -96, 96, o, rgba(MOON), caps=True)
    elif variant == "c":                       # 75 % lens + gold ring + journal badge lower right
        lit, band = offset_lens(g, r, 0.5 * r)
        g.lit(lit, r, "gold", band)
        g.ring(0, 0, rr, o, rgba(MOON))
        br = max(0.36 * r, 2.5)
        bx = by = 0.66 * r
        g.circle(bx, by, br, rgba(NIGHT))
        if r >= 16:                            # tick
            g.polyline([(bx - 0.45 * br, by + 0.02 * br), (bx - 0.12 * br, by + 0.38 * br), (bx + 0.48 * br, by - 0.40 * br)],
                       max(1.0, 0.22 * br), rgba(MOON))
        else:                                  # dot
            g.circle(bx, by, max(1.0, 0.36 * br), rgba(MOON))
    elif variant == "d":                       # 75 % lens, silver ring
        lit, band = offset_lens(g, r, 0.5 * r)
        g.lit(lit, r, "gold", band)
        g.ring(0, 0, rr, o, rgba(SILVER))
    elif variant == "e":                       # 60 % gibbous, silver ring (a + d): best by colour, fails greyscale rule
        lit, band = phase(g, r, ACCEPTED_W)
        g.lit(lit, r, "gold", band)
        g.ring(0, 0, rr, o, rgba(SILVER))
    elif variant == "f":                       # (e) + Night seal dot on the lit side: chosen (== state_glyph "accepted")
        return state_glyph(r, "accepted")
    else:
        raise ValueError(variant)
    return g


# ---- halo gauge -------------------------------------------------------------
def halo_glyph(box: float, f: float) -> Glyph:
    R = box / 2
    rt, wt, gap, rc, eps = halo_metrics(R)
    g = Glyph(box=box + 8)
    v = visual_fraction(f, eps)

    if f >= 1:
        if R >= 8:
            g.ring(0, 0, rt, wt * 2.2, rgba(MOON, 0.10))
            g.ring(0, 0, rt, wt * 1.5, rgba(MOON, 0.16))
        g.ring(0, 0, rt, wt, rgba(MOON))
        if R >= HALO_CORE_MIN_R:
            g.lit(g.disc_mask(0, 0, rc), rc, "gold")
        return g

    g.ring(0, 0, rt, wt, rgba(VEIL_LINE))
    if v > 0:
        g.arc(0, 0, rt, -90, -90 + 360 * v, wt, rgba(MOON))

    if R < HALO_CORE_MIN_R:                     # 16-18 px strip / status bar: track + arc, number beside it
        return g
    oc = outline_px(rc)
    g.circle(0, 0, rc, rgba(SHADOW))            # core disc: Shadow with a Dusk rim, like the state moons
    g.ring(0, 0, rc - oc / 2, oc, rgba(DUSK))
    if f > 0:
        ec = max(0.10, 1.5 / (2 * rc))
        w = ec + (1 - 2 * ec) * f
        lit, band = phase(g, rc, w)
        g.lit(lit, rc, "gold", band)
    return g


# ---- sheet ------------------------------------------------------------------
def font(size: int, weight: str = "regular"):
    names = {"regular": ["segoeui.ttf"], "semibold": ["seguisb.ttf", "segoeuib.ttf", "segoeui.ttf"]}[weight]
    for n in names:
        try:
            return ImageFont.truetype(n, size)
        except Exception:
            continue
    return ImageFont.load_default(size)


def text(d: ImageDraw.ImageDraw, x, y, s, fill, size=11, anchor="ls", weight="regular"):
    d.text((x, y), s, fill=fill, font=font(size, weight), anchor=anchor)


def render_sheet() -> Image.Image:
    sheet = Image.new("RGBA", (W, H), rgba(NIGHT))
    d = ImageDraw.Draw(sheet)

    text(d, 40, 42, "Tsukimichi glyphs v2.1", SILVER, 21, weight="semibold")
    text(d, 290, 42, "state moons with crisp rims and lunar detail · halo gauge for tree progress · proposal sheet", DUSK, 12)

    # Row 0: Accepted candidates next to Completed, Ready and DoneThisCycle at 24 / 16 / 12 px, plus a greyscale line
    text(d, 40, 78, "0 · ACCEPTED CANDIDATES · 24 / 16 / 12 PX · NEXT TO COMPLETED, READY, DONE THIS CYCLE · LAST LINE = 16 PX IN GREYSCALE", DUSK, 11)
    cols = [("completed", "Completed", "reference"), ("ready", "Ready", "reference"), ("donecycle", "Done this cycle", "reference"),
            ("v2.0", "v2.0 Accepted", "75 % lens · gold ring"), ("a", "(a) 60 %", "gold ring"),
            ("b", "(b) 66 %", "gold rim, lit side"), ("c", "(c) 75 % + badge", "tick · gold ring"),
            ("d", "(d) 75 %", "silver ring"), ("e", "(e) 60 %", "silver ring · a + d"), ("f", "(f) 60 % + seal", "silver ring · Night dot")]
    pitch = 112
    grey = Image.new("RGBA", (W, 40), rgba(NIGHT))
    for i, (key, name, sub) in enumerate(cols):
        cx = 78 + pitch * i
        if key == CHOSEN:
            d.rounded_rectangle([cx - 52, 92, cx + 52, 224], radius=6, outline=rgba(MOON), width=1)
            text(d, cx, 89, "CHOSEN", MOON, 9, anchor="ms")
        for r, y in ((10.5, 112), (7, 140), (6, 162)):
            g = state_glyph(r, key) if key in STATES else accepted_candidate(r, key)
            g.paste_on(sheet, cx, y)
        (state_glyph(7, key) if key in STATES else accepted_candidate(7, key)).paste_on(grey, cx, 20)
        text(d, cx, 206, name, SILVER if key == CHOSEN else DUSK, 10, anchor="ms")
        text(d, cx, 218, sub, DUSK, 8, anchor="ms")
    grey = grey.convert("L").convert("RGBA")
    sheet.alpha_composite(grey.crop((0, 8, 1150, 32)), (0, 174))
    d = ImageDraw.Draw(sheet)
    text(d, 1170, 116, "24", DUSK, 10, anchor="ms")
    text(d, 1170, 144, "16", DUSK, 10, anchor="ms")
    text(d, 1170, 166, "12", DUSK, 10, anchor="ms")
    text(d, 1170, 190, "grey", DUSK, 10, anchor="ms")

    # Row 1
    text(d, 40, 78 + OFF, "1 · STATE GLYPHS · 64 PX · WITH INTERIOR DETAIL (r ≥ 12)", DUSK, 11)
    names = ["Completed", "Accepted", "Ready", "Ready on other job", "Done this cycle", "Blocked", "Foreclosed", "Unknown"]
    subs = ["full · gold", "gibbous 60 % · seal · silver ring", "first quarter · glow · dusk rim", "first quarter silver · gold ring",
            "waning gibbous silver · dusk rim", "new · silver ring", "eclipse ring · bar · notch", "veiled · dashed dusk ring"]
    for i, st in enumerate(STATES):
        cx = 115 + 135 * i
        state_glyph(28, st).paste_on(sheet, cx, 130 + OFF)
        text(d, cx, 182 + OFF, names[i], SILVER, 12, anchor="ms")
        text(d, cx, 197 + OFF, subs[i], DUSK, 10, anchor="ms")

    # Row 2
    text(d, 40, 228 + OFF, "2 · 32 / 24 / 16 PX", DUSK, 11)
    d.rounded_rectangle([50, 270 + OFF, 1100, 304 + OFF], radius=4, fill=rgba(IMGUI_BG))
    for i, st in enumerate(STATES):
        for y in (252 + OFF, 287 + OFF):
            state_glyph(16, st).paste_on(sheet, 78 + 135 * i, y)
            state_glyph(10.5, st).paste_on(sheet, 116 + 135 * i, y)
            state_glyph(7, st).paste_on(sheet, 148 + 135 * i, y)
    text(d, 1110, 256 + OFF, "on Night", DUSK, 10)
    text(d, 1110, 291 + OFF, "on ImGui bg", DUSK, 10)

    # Row 3
    text(d, 40, 330 + OFF, "3 · PROGRESS GLYPH (HALO GAUGE) · 64 / 24 / 16 PX (16 = TRACK + ARC ONLY, NUMBER BESIDE IT)", DUSK, 11)
    fracs = [0, 0.03, 0.10, 0.25, 0.50, 0.75, 0.90, 0.97, 1.0]
    samples = ["0 / 240", "17 / 612", "20 / 195", "153 / 612", "98 / 195", "459 / 612", "176 / 195", "594 / 612", "612 / 612"]
    for i, f in enumerate(fracs):
        cx = 110 + 120 * i
        halo_glyph(64, f).paste_on(sheet, cx, 382 + OFF)
        halo_glyph(24, f).paste_on(sheet, cx - 18, 438 + OFF)
        halo_glyph(16, f).paste_on(sheet, cx + 16, 438 + OFF)
        text(d, cx, 470 + OFF, f"{f:.2f}" if 0 < f < 1 else str(int(f)), SILVER, 11, anchor="ms")
        text(d, cx, 485 + OFF, samples[i], DUSK, 10, anchor="ms")

    # Row 4: journal tree
    text(d, 40, 522 + OFF, "4 · JOURNAL TREE IN SITU · 24 PX GLYPHS · 30 PX ROWS", DUSK, 11)
    d.rounded_rectangle([40, 532 + OFF, 600, 716 + OFF], radius=4, fill=rgba(IMGUI_BG))
    rows = [
        # (section?, name, badge, done, total, state)
        (True, "Main Scenario", None, 758, 1002, None),
        (False, "Seventh Umbral Era", "ARR", 612, 612, None),
        (False, "Seventh Astral Era", "ARR", 129, 195, "selected"),
        (False, "Heavensward", "HW", 17, 612, "hover"),
        (True, "Side Quests", None, 388, 1624, None),
        (False, "Chronicles of a New Era", None, 0, 240, None),
    ]
    for k, (section, name, badge, done, total, st) in enumerate(rows):
        cy = 551 + OFF + 30 * k
        top = cy - 15
        if st == "selected":
            d.rectangle([40, top, 600, top + 30], fill=rgba(VEIL, 0.22))
            d.rectangle([40, top, 42, top + 30], fill=rgba(MOON))
        elif st == "hover":
            d.rectangle([40, top, 600, top + 30], fill=rgba(SILVER, 0.05))
        f = done / total if total else 0.0
        if section:
            d.polygon([(52, cy - 4), (60, cy - 4), (56, cy + 3)], fill=rgba(DUSK))
            gx, nx = 80, 102
        else:
            d.polygon([(74, cy - 4.5), (81, cy), (74, cy + 4.5)], fill=rgba(DUSK if st == "selected" else VEIL))
            gx, nx = 102, 124
        halo_glyph(24, f).paste_on(sheet, gx, cy)
        complete = done >= total
        text(d, nx, cy + 4.5, name, MOON if complete else SILVER, 13, weight="semibold" if section else "regular")
        if badge:
            bw = 26 if len(badge) == 3 else 24
            bx = nx + d.textlength(name, font=font(13)) + 8
            d.rounded_rectangle([bx, cy - 7, bx + bw, cy + 7], radius=7, outline=rgba(VEIL), width=1)
            text(d, bx + bw / 2, cy + 3.5, badge, DUSK, 9, anchor="ms")
        # mini bar + count
        d.rounded_rectangle([462, cy - 1.5, 506, cy + 1.5], radius=1.5, fill=rgba(VEIL))
        if f > 0:
            d.rounded_rectangle([462, cy - 1.5, 462 + max(2, 44 * f), cy + 1.5], radius=1.5, fill=rgba(MOON))
        count = f"{done:,} / {total:,}".replace(",", " ")
        text(d, 588, cy + 4, count, SILVER if st == "selected" else DUSK, 11, anchor="rs")
        if section:
            d.line([(52, cy + 15.5), (588, cy + 15.5)], fill=rgba(MOON, 0.28), width=1)

    notes = [
        ("Journal tab upgrades shown at left", SILVER, 12, "semibold"),
        ("1 · Section rows: 600 weight, thin gold rule (Moon 28 %) beneath", DUSK, 11, "regular"),
        ("2 · Halo gauge at 24 px (floor): 3 % reads as a gold pip, 66 % as two thirds", DUSK, 11, "regular"),
        ("3 · Complete nodes: full gold ring + core + glow, name tinted Moon", DUSK, 11, "regular"),
        ("4 · Selected row: Veil 22 % wash + 2 px gold left rule; count in Silver", DUSK, 11, "regular"),
        ("5 · Hover row: Silver 5 % wash", DUSK, 11, "regular"),
        ("6 · Expansion badge pill (Veil stroke, Dusk 9 px caps) after the name", DUSK, 11, "regular"),
        ("7 · Count as \"done / total\" in Dusk, 44 × 3 px mini bar 12 px before it", DUSK, 11, "regular"),
    ]
    for j, (s, col, size, weight) in enumerate(notes):
        text(d, 640, 548 + OFF + (18 * j if j else 0) + (2 if j else 0), s, col, size, weight=weight)
    return sheet


def write_zoom(sheet: Image.Image):
    """4x nearest-neighbour crops: row 0 (candidates, two halves), row 2, row 3 small halos, row 4 tree."""
    crops = [
        sheet.crop((20, 96, 600, 200)),                          # row 0: references, v2.0, (a), (b)
        sheet.crop((580, 96, 1160, 200)),                        # row 0: (c), (d), (e), (f)
        sheet.crop((50, 236 + OFF, 630, 306 + OFF)),             # row 2 left half
        sheet.crop((80, 420 + OFF, 660, 456 + OFF)),             # row 3 small halos
        sheet.crop((40, 532 + OFF, 600, 716 + OFF)),             # row 4 tree
    ]
    zoomed = [c.resize((c.width * 4, c.height * 4), Image.NEAREST) for c in crops]
    total_h = sum(z.height for z in zoomed) + 8 * (len(zoomed) - 1)
    out = Image.new("RGBA", (max(z.width for z in zoomed), total_h), rgba(NIGHT))
    y = 0
    for z in zoomed:
        out.alpha_composite(z, (0, y))
        y += z.height + 8
    out.convert("RGB").save(ZOOM)


def main() -> None:
    if try_cairosvg():
        print("rendered with cairosvg:", PNG)
        sheet = Image.open(PNG).convert("RGBA")
    else:
        print("cairosvg unavailable; rendering the same geometry with Pillow")
        sheet = render_sheet()
        sheet.convert("RGB").save(PNG)
        print("wrote", PNG)
    write_zoom(sheet)
    print("wrote", ZOOM)


if __name__ == "__main__":
    main()
