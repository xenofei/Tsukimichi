"""Rasterize the glyph design sheet to docs/design/glyphs/glyphs-v2.png.

Strategy (in order):
  1. cairosvg, if importable, renders glyphs-v2.svg directly.
  2. Otherwise `pip install cairosvg` is attempted once and step 1 retried.
     (On Windows the wheel installs but libcairo is usually missing, so this
     typically falls through.)
  3. Fallback: the same sheet is re-expressed procedurally with Pillow from
     the same numbers as the SVG, the way assets/icons/render_icons.py does
     for the plugin icon. Circles, arcs, polygons and boolean disc masks only.

Also writes glyphs-v2-zoom.png: a 4x nearest-neighbour blow-up of the 16 px
and 20 px rows so small-size legibility can be judged pixel by pixel.

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

W, H = 1200, 700


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
from PIL import Image, ImageChops, ImageDraw, ImageFont  # noqa: E402

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
UMBRA = (0x2C, 0x33, 0x4A)      # the current UnlitDisc, kept for hollow cores
BRUISE = (0x64, 0x55, 0x74)     # Veil pulled a quarter toward Eclipse
IMGUI_BG = (0x14, 0x14, 0x14)
WHITE = (0xFF, 0xFF, 0xFF)

SS = 4


def rgba(rgb, a=1.0):
    return (*rgb, max(0, min(255, round(255 * a))))


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


# ---- geometry rules shared with the SVG ------------------------------------
def outline_px(r: float) -> float:
    """Rim stroke for a state moon of radius r (px): clamp(0.10 r, 1.25, 3)."""
    return min(3.0, max(1.25, 0.10 * r))


def halo_metrics(R: float):
    """Halo gauge for a box of half-size R: track radius, stroke, gap, core radius, epsilon."""
    rt = 0.80 * R
    wt = max(1.5, 0.16 * R)
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

    def _commit(self, layer):
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

    def arc(self, cx, cy, r, start, end, width, fill, caps=True):
        """Stroked arc, degrees clockwise from 3 o'clock (Pillow convention), round caps."""
        layer, d = self._layer()
        w = max(1, round(width * SS))
        d.arc(self._bbox(cx, cy, r), start, end, fill=fill, width=w)
        if caps:
            for ang in (start, end):
                px = cx + r * math.cos(math.radians(ang))
                py = cy + r * math.sin(math.radians(ang))
                d.ellipse(self._bbox(px, py, width / 2), fill=fill)
        self._commit(layer)

    def dashed_ring(self, cx, cy, r, width, n, dash_deg, fill):
        for k in range(n):
            start = k * 360 / n - 90
            self.arc(cx, cy, r, start, start + dash_deg, width, fill, caps=False)

    def half_right_mask(self, cx, cy, r):
        m = self._mask()
        ImageDraw.Draw(m).pieslice(self._bbox(cx, cy, r), -90, 90, fill=255)
        return m

    def lens_mask(self, cx, cy, r, ox, oy, orad):
        return ImageChops.multiply(self.disc_mask(cx, cy, r), self.disc_mask(ox, oy, orad))

    def crescent_mask(self, cx, cy, r, ox, oy, orad):
        return ImageChops.subtract(self.disc_mask(cx, cy, r), self.disc_mask(ox, oy, orad))

    def shaded(self, mask, cx, cy, r, hi, mid, lo, flat=None):
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


# ---- state glyphs -----------------------------------------------------------
STATES = ["completed", "accepted", "ready", "readyother", "donecycle", "blocked", "foreclosed", "unknown"]


def state_glyph(r: float, state: str) -> Glyph:
    g = Glyph(box=r * 2.6 + 4)          # room for the Ready glow
    o = outline_px(r)
    rr = r - o / 2
    shade = r >= 9                      # class L/M get the radial shading, S stays flat
    gold = dict(hi=MOON_HI, mid=MOON, lo=MOON_LO, flat=None if shade else MOON)
    silver = dict(hi=SILVER_HI, mid=SILVER, lo=SILVER_LO, flat=None if shade else SILVER)
    gib = 0.5 * r

    if state == "completed":
        g.shaded(g.disc_mask(0, 0, r), 0, 0, r, **gold)
        if r >= 16:
            g.highlight_arc(0, 0, r)
    elif state == "accepted":
        g.circle(0, 0, r, rgba(VEIL))
        g.shaded(g.lens_mask(0, 0, r, +gib, 0, r), 0, 0, r, **gold)
        g.ring(0, 0, rr, o, rgba(MOON))
    elif state == "ready":
        g.glow(0, 0, r * 1.75, MOON, 0.55 if r >= 9 else 0.5)
        g.circle(0, 0, r, rgba(VEIL))
        g.ring(0, 0, rr, o, rgba(DUSK))
        g.shaded(g.half_right_mask(0, 0, r), 0, 0, r, **gold)
    elif state == "readyother":
        g.circle(0, 0, r, rgba(VEIL))
        g.shaded(g.half_right_mask(0, 0, r), 0, 0, r, **silver)
        g.ring(0, 0, rr, o, rgba(MOON))
    elif state == "donecycle":
        g.circle(0, 0, r, rgba(VEIL))
        g.ring(0, 0, rr, o, rgba(DUSK))
        g.shaded(g.lens_mask(0, 0, r, -gib, 0, r), 0, 0, r, **silver)
    elif state == "blocked":
        g.circle(0, 0, r, rgba(VEIL))
        g.ring(0, 0, rr, o, rgba(SILVER))
    elif state == "foreclosed":
        g.circle(0, 0, r, rgba(BRUISE))
        ow = o * 1.2
        g.ring(0, 0, r - ow / 2, ow, rgba(ECLIPSE))
        g.circle(0.72 * r, -0.72 * r, max(0.30 * r, 2.0), rgba(NIGHT))
    elif state == "unknown":
        g.circle(0, 0, r, rgba(VEIL, 0.45))
        if r >= 10:
            g.dashed_ring(0, 0, rr, o, 12, 16, rgba(DUSK))
        else:
            g.dashed_ring(0, 0, rr, o, 8, 22, rgba(DUSK))
    else:
        raise ValueError(state)
    return g


# ---- halo gauge -------------------------------------------------------------
def halo_glyph(box: float, f: float) -> Glyph:
    R = box / 2
    rt, wt, gap, rc, eps = halo_metrics(R)
    g = Glyph(box=box + 8)
    v = visual_fraction(f, eps)

    if f >= 1:
        g.ring(0, 0, rt, wt * 2.2, rgba(MOON, 0.10))
        g.ring(0, 0, rt, wt * 1.5, rgba(MOON, 0.16))
        g.ring(0, 0, rt, wt, rgba(MOON))
        g.shaded(g.disc_mask(0, 0, rc), 0, 0, rc, MOON_HI, MOON, MOON_LO, flat=None if rc >= 6 else MOON)
        return g

    g.ring(0, 0, rt, wt, rgba(VEIL))
    if v > 0:
        g.arc(0, 0, rt, -90, -90 + 360 * v, wt, rgba(MOON))

    if f <= 0:
        g.circle(0, 0, rc, rgba(UMBRA))
    else:
        g.circle(0, 0, rc, rgba(UMBRA))
        ec = max(0.10, 1.5 / (2 * rc))
        w = ec + (1 - 2 * ec) * f
        xe = (1 - 2 * w) * rc                       # equator x of the terminator
        if abs(xe) < 1e-3:
            mask = g.half_right_mask(0, 0, rc)
        else:
            h = (xe * xe - rc * rc) / (2 * xe)      # terminator centre on the x axis
            Rt = math.hypot(h, rc)
            if xe > 0:                              # crescent: disc minus terminator disc
                mask = g.crescent_mask(0, 0, rc, h, 0, Rt)
            else:                                   # gibbous: disc intersect terminator disc
                mask = g.lens_mask(0, 0, rc, h, 0, Rt)
        g.shaded(mask, 0, 0, rc, MOON_HI, MOON, MOON_LO, flat=None if rc >= 6 else MOON)
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

    text(d, 40, 42, "Tsukimichi glyphs v2", SILVER, 21, weight="semibold")
    text(d, 270, 42, "state moons with crisp rims · halo gauge for tree progress · proposal sheet", DUSK, 12)

    # Row 1
    text(d, 40, 78, "1 · STATE GLYPHS · 64 PX", DUSK, 11)
    names = ["Completed", "Accepted", "Ready", "Ready on other job", "Done this cycle", "Blocked", "Foreclosed", "Unknown"]
    subs = ["full · gold", "waxing gibbous · gold ring", "first quarter · glow · dusk rim", "first quarter silver · gold ring",
            "waning gibbous silver · dusk rim", "new · silver ring", "eclipse ring · notch · bruised disc", "veiled · dashed dusk ring"]
    for i, st in enumerate(STATES):
        cx = 115 + 135 * i
        state_glyph(28, st).paste_on(sheet, cx, 130)
        text(d, cx, 182, names[i], SILVER, 12, anchor="ms")
        text(d, cx, 197, subs[i], DUSK, 10, anchor="ms")

    # Row 2
    text(d, 40, 228, "2 · 24 PX AND 16 PX", DUSK, 11)
    d.rounded_rectangle([60, 270, 1100, 304], radius=4, fill=rgba(IMGUI_BG))
    for i, st in enumerate(STATES):
        for y in (252, 287):
            state_glyph(10.5, st).paste_on(sheet, 95 + 135 * i, y)
            state_glyph(7, st).paste_on(sheet, 133 + 135 * i, y)
    text(d, 1108, 256, "on Night", DUSK, 10)
    text(d, 1108, 291, "on ImGui bg", DUSK, 10)

    # Row 3
    text(d, 40, 330, "3 · PROGRESS GLYPH (HALO GAUGE) · 64 / 24 / 16 PX", DUSK, 11)
    fracs = [0, 0.03, 0.10, 0.25, 0.50, 0.75, 0.90, 0.97, 1.0]
    samples = ["0 / 240", "17 / 612", "20 / 195", "153 / 612", "98 / 195", "459 / 612", "176 / 195", "594 / 612", "612 / 612"]
    for i, f in enumerate(fracs):
        cx = 110 + 120 * i
        halo_glyph(64, f).paste_on(sheet, cx, 382)
        halo_glyph(24, f).paste_on(sheet, cx - 18, 438)
        halo_glyph(16, f).paste_on(sheet, cx + 16, 438)
        text(d, cx, 470, f"{f:.2f}" if 0 < f < 1 else str(int(f)), SILVER, 11, anchor="ms")
        text(d, cx, 485, samples[i], DUSK, 10, anchor="ms")

    # Row 4: journal tree
    text(d, 40, 522, "4 · JOURNAL TREE IN SITU · 20 PX GLYPHS · 26 PX ROWS", DUSK, 11)
    d.rounded_rectangle([40, 532, 600, 692], radius=4, fill=rgba(IMGUI_BG))
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
        cy = 549 + 26 * k
        top = cy - 13
        if st == "selected":
            d.rectangle([40, top, 600, top + 26], fill=rgba(VEIL, 0.22))
            d.rectangle([40, top, 42, top + 26], fill=rgba(MOON))
        elif st == "hover":
            d.rectangle([40, top, 600, top + 26], fill=rgba(SILVER, 0.05))
        f = done / total if total else 0.0
        if section:
            d.polygon([(52, cy - 4), (60, cy - 4), (56, cy + 3)], fill=rgba(DUSK))
            gx, nx = 78, 98
        else:
            d.polygon([(74, cy - 4.5), (81, cy), (74, cy + 4.5)], fill=rgba(DUSK if st == "selected" else VEIL))
            gx, nx = 100, 120
        halo_glyph(20, f).paste_on(sheet, gx, cy)
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
            d.line([(52, cy + 13.5), (588, cy + 13.5)], fill=rgba(MOON, 0.28), width=1)

    notes = [
        ("Journal tab upgrades shown at left", SILVER, 12, "semibold"),
        ("1 · Section rows: 600 weight, thin gold rule (Moon 28 %) beneath", DUSK, 11, "regular"),
        ("2 · Halo gauge at 20 px: 3 % reads as a gold pip, 66 % as two thirds", DUSK, 11, "regular"),
        ("3 · Complete nodes: full gold ring + core + glow, name tinted Moon", DUSK, 11, "regular"),
        ("4 · Selected row: Veil 22 % wash + 2 px gold left rule; count in Silver", DUSK, 11, "regular"),
        ("5 · Hover row: Silver 5 % wash", DUSK, 11, "regular"),
        ("6 · Expansion badge pill (Veil stroke, Dusk 9 px caps) after the name", DUSK, 11, "regular"),
        ("7 · Count as \"done / total\" in Dusk, 44 × 3 px mini bar 12 px before it", DUSK, 11, "regular"),
    ]
    for j, (s, col, size, weight) in enumerate(notes):
        text(d, 640, 548 + (18 * j if j else 0) + (2 if j else 0), s, col, size, weight=weight)
    return sheet


def write_zoom(sheet: Image.Image):
    """4x nearest-neighbour crops: row 2 (24/16 px states) and row 4 (20 px tree)."""
    crops = [sheet.crop((60, 236, 640, 306)), sheet.crop((80, 420, 660, 456)), sheet.crop((40, 532, 600, 692))]
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
