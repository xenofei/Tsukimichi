"""Restyles one release illustration for each theme (spec-1.22.md W2, decision 1): the same painting, given the theme's
palette grade and its motif layer. The frame comes from the theme's kit and is drawn by the popup, not baked here.

This file is the recipe the plugin runs once when the popup opens (ReleaseArt.Compose): a grade (lift, gamma, gain,
saturation, a split tone from the palette) and one motif sprite layer per kit, composited over the base. The mock
renders exactly these outputs. Quiet uses the grade without the motif layer; Plain shows no art.

Run: py -3 grade_release.py        -> ../themed/<release>-<theme>[-quiet].png  (1120 x 440, the 2x tier)
"""
import math
import pathlib

import numpy as np
from PIL import Image, ImageDraw

from artlib import blur, fbm, hexc

ART = pathlib.Path(__file__).resolve().parent.parent
OUT = ART / "themed"
RELEASES = {
    # moon position and radius in the 1120 x 440 base (the orrery motif centres on it)
    "evercold": {"moon": (0.815, 0.20, 18.0)},
    "answers": {"moon": (0.585, 0.235, 25.0)},
}

# Grade per theme. lift/gain are added/multiplied per channel; sat scales chroma; split tone tints shadows and
# highlights by luminance. Medallion is the reference: the painting as painted.
GRADES = {
    "medallion": dict(lift=(0, 0, 0), gamma=1.0, gain=(1, 1, 1), sat=1.0, shadow=None, high=None),
    "classic": dict(lift=(0.01, 0.01, 0.015), gamma=1.04, gain=(0.98, 0.98, 1.0), sat=0.82, shadow=None, high=None),
    # Ishgard Glass sits on Ishgard Snow: a milkier, cooler, lighter print, as seen through clear glass in daylight
    "ishgard-glass": dict(lift=(0.07, 0.08, 0.10), gamma=0.90, gain=(0.98, 1.0, 1.03), sat=0.80, shadow=("#2C3A64", 0.25), high=("#EEF3FF", 0.20)),
    # Aether Crystal: crisp and cool, highlights leaning toward aether
    "aether-crystal": dict(lift=(0, 0.005, 0.012), gamma=1.0, gain=(0.95, 1.01, 1.04), sat=0.95, shadow=("#0E2A40", 0.20), high=("#BFF0FF", 0.18)),
    # Orrery sits on Dawn: plum shadows, warm dawn-gold highlights
    "astrologian-orrery": dict(lift=(0.015, 0.0, 0.01), gamma=1.0, gain=(1.04, 0.98, 0.97), sat=0.95, shadow=("#2B1F3A", 0.40), high=("#F5C47C", 0.22)),
    # Sumi sits on Kugane Lacquer: most colour drawn out, warm black lacquer shadows, the lights kept warm
    "sumi-to-kinpaku": dict(lift=(0.0, -0.005, -0.01), gamma=1.06, gain=(1.03, 1.0, 0.95), sat=0.40, shadow=("#16100F", 0.45), high=("#F3E9DB", 0.25)),
}


def lum(px):
    return px[..., 0] * 0.2126 + px[..., 1] * 0.7152 + px[..., 2] * 0.0722


def grade(px, g):
    px = px + np.array(g["lift"], np.float32) * (1 - px)
    px = np.clip(px, 0, 1) ** g["gamma"]
    px = px * np.array(g["gain"], np.float32)
    L = lum(px)[..., None]
    px = L + (px - L) * g["sat"]
    if g["shadow"]:
        c, a = g["shadow"]
        w = np.clip(1 - L / 0.45, 0, 1) * a
        px = px + (hexc(c) - px) * w * 0.6
    if g["high"]:
        c, a = g["high"]
        w = np.clip((L - 0.45) / 0.55, 0, 1) * a
        px = 1 - (1 - px) * (1 - hexc(c) * w)
    return np.clip(px, 0, 1)


def over(px, color, alpha):
    a = np.clip(alpha, 0, 1)[..., None]
    return px * (1 - a) + np.asarray(color, np.float32) * a


def screen(px, color, amount):
    return 1 - (1 - px) * (1 - np.clip(np.asarray(color, np.float32) * np.clip(amount, 0, 1)[..., None], 0, 1))


def mask_draw(w, h, fn, ss=2):
    im = Image.new("L", (w * ss, h * ss), 0)
    fn(ImageDraw.Draw(im), ss)
    return np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255.0


# ---------- motif layers (one per kit; shared by every release) ----------
def motif_frost(px, rel):
    """Ishgard Glass: rime feathers growing in from the two upper corners and the lower left; thin, white-blue."""
    h, w, _ = px.shape
    rng = np.random.default_rng(4)

    def draw(d, ss):
        def branch(x, y, ang, length, width, depth):
            if depth > 5 or length < 4:
                return
            x2, y2 = x + math.cos(ang) * length, y + math.sin(ang) * length
            d.line([(x * ss, y * ss), (x2 * ss, y2 * ss)], fill=int(255 * (0.95 - depth * 0.13)), width=max(1, int(width * ss)))
            n = 4 if depth < 2 else 3
            for k in range(1, n + 1):
                t = k / (n + 1)
                bx, by = x + (x2 - x) * t, y + (y2 - y) * t
                for s in (-1, 1):
                    branch(bx, by, ang + s * (0.95 + rng.normal(0, 0.12)), length * 0.42 * (1 - t * 0.4), width * 0.7, depth + 1)
            branch(x2, y2, ang + rng.normal(0, 0.18), length * 0.62, width * 0.8, depth + 1)

        for (cx, cy, base, k) in [(0, 0, 0.35, 1.0), (w, 0, math.pi - 0.35, 1.0), (0, h, -0.45, 0.6)]:
            for i in range(6):
                a = base + (i - 2.5) * 0.22 + rng.normal(0, 0.05)
                branch(cx, cy, a, (20 + rng.random() * 26) * k, 1.3, 0)

    m = mask_draw(w, h, draw)
    soft = blur(m, 2.2)
    px = screen(px, hexc("#DCEBFF"), soft * 0.28)
    px = over(px, hexc("#F4F8FF"), m * 0.42)
    return px


def motif_shards(px, rel):
    """Aether Crystal: four small floating shards in the sky's corners, lit on their upper-left facets."""
    h, w, _ = px.shape
    shards = [(0.055, 0.13, 9, 0.5), (0.115, 0.27, 6, -0.3), (0.945, 0.11, 7, 0.9)]
    lit = np.zeros((h, w), np.float32)
    dark = np.zeros((h, w), np.float32)
    edge = np.zeros((h, w), np.float32)
    for fx, fy, s, rot in shards:
        cx, cy = fx * w, fy * h
        ca, sa = math.cos(rot), math.sin(rot)
        pts = [(0, -s * 1.7), (s * 0.55, 0), (0, s * 1.2), (-s * 0.6, -0.1 * s)]
        P = [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]
        L = [P[0], P[3], P[2]]  # the facet facing the light (left)
        D = [P[0], P[1], P[2]]
        lit = np.maximum(lit, mask_draw(w, h, lambda d, ss: d.polygon([(x * ss, y * ss) for x, y in L], fill=255)))
        dark = np.maximum(dark, mask_draw(w, h, lambda d, ss: d.polygon([(x * ss, y * ss) for x, y in D], fill=255)))
        edge = np.maximum(edge, mask_draw(w, h, lambda d, ss: d.line([(P[0][0] * ss, P[0][1] * ss), (P[3][0] * ss, P[3][1] * ss)], fill=255, width=ss)))
    px = screen(px, hexc("#9BE6FF"), blur(np.maximum(lit, dark), 5) * 0.22)
    px = over(px, hexc("#3E7FA8"), dark * 0.85)
    px = over(px, hexc("#BDEFFF"), lit * 0.88)
    px = over(px, hexc("#FFFFFF"), edge * 0.8)
    return px


def motif_orrery(px, rel):
    """Orrery: two hairline orbits about the moon at the 28-degree tilt, a 12-notch scale on the inner one, one bead."""
    h, w, _ = px.shape
    mx, my, mr = RELEASES[rel]["moon"]
    mx, my = mx * w, my * h
    tilt = math.radians(-28)

    def ell(d, ss, rx, ry, a0, a1, width):
        pts = []
        for i in range(181):
            t = math.radians(a0 + (a1 - a0) * i / 180)
            x, y = rx * math.cos(t), ry * math.sin(t)
            pts.append(((mx + x * math.cos(tilt) - y * math.sin(tilt)) * ss, (my + x * math.sin(tilt) + y * math.cos(tilt)) * ss))
        d.line(pts, fill=255, width=width)

    def draw(d, ss):
        ell(d, ss, mr * 4.2, mr * 1.6, -150, 150, ss)
        ell(d, ss, mr * 7.5, mr * 2.8, 200, 320, ss)
        for k in range(12):
            t = math.radians(k * 30)
            r1, r2 = mr * 1.55, mr * 1.80
            d.line([((mx + r1 * math.cos(t)) * ss, (my + r1 * math.sin(t)) * ss), ((mx + r2 * math.cos(t)) * ss, (my + r2 * math.sin(t)) * ss)], fill=200, width=ss)

    m = mask_draw(w, h, draw)
    px = over(px, hexc("#EAD3A0"), m * 0.30)
    t = math.radians(-35)
    bx, by = mr * 4.2 * math.cos(t), mr * 1.6 * math.sin(t)
    bxx, byy = mx + bx * math.cos(tilt) - by * math.sin(tilt), my + bx * math.sin(tilt) + by * math.cos(tilt)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    bead = np.exp(-((xx - bxx) ** 2 + (yy - byy) ** 2) / (2 * 1.6 ** 2))
    px = over(px, hexc("#F2DDA8"), np.clip(bead * 1.6, 0, 1) * 0.95)
    return px


def motif_kinpaku(px, rel):
    """Sumi to Kinpaku: sunago, gold-leaf flecks dusted into the upper corners; cut edges, a few catching the light."""
    h, w, _ = px.shape
    rng = np.random.default_rng(12)
    gold = np.zeros((h, w), np.float32)
    bright = np.zeros((h, w), np.float32)
    for corner_x in (0.0, 1.0):
        for _ in range(42):
            r = rng.random() ** 1.8
            ang = rng.random() * math.pi / 2
            fx = corner_x + (r * 0.30 * math.cos(ang)) * (1 if corner_x == 0 else -1)
            fy = r * 0.62 * math.sin(ang) * 0.9
            s = 1.2 + rng.random() ** 2 * 4.5
            cx, cy = fx * w, fy * h
            pts = [(cx + s * math.cos(a) * (0.6 + rng.random() * 0.6), cy + s * math.sin(a) * (0.6 + rng.random() * 0.6)) for a in sorted(rng.random(4) * math.pi * 2)]
            m = mask_draw(w, h, lambda d, ss: d.polygon([(x * ss, y * ss) for x, y in pts], fill=255))
            gold = np.maximum(gold, m * (0.55 + 0.45 * (1 - r)))
            if rng.random() < 0.22:
                bright = np.maximum(bright, m)
    px = over(px, hexc("#C9A24E"), gold * 0.85)
    px = over(px, hexc("#F4DA92"), bright * 0.7)
    # washi: the faintest fibre in the paper, so the lacquer grade reads as print, not as a filter
    fib = fbm(h, w, 2.0, 2, 77)
    px = px * (1 + (fib - 0.5)[..., None] * 0.04)
    return px


MOTIF = {"ishgard-glass": motif_frost, "aether-crystal": motif_shards, "astrologian-orrery": motif_orrery, "sumi-to-kinpaku": motif_kinpaku}


def compose(rel, theme, quiet=False):
    base = np.asarray(Image.open(ART / f"{rel}-base.png").convert("RGB"), np.float32) / 255.0
    px = grade(base, GRADES[theme])
    if not quiet and theme in MOTIF:
        px = MOTIF[theme](px, rel)
    return Image.fromarray((np.clip(px, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


if __name__ == "__main__":
    OUT.mkdir(exist_ok=True)
    for rel in RELEASES:
        for theme in GRADES:
            compose(rel, theme).save(OUT / f"{rel}-{theme}.png", optimize=True)
            # Quiet is the same grade without the motif layer; only Medallion's is kept (the mock's Quiet popup uses it)
            if theme == "medallion":
                compose(rel, theme, quiet=True).save(OUT / f"{rel}-{theme}-quiet.png", optimize=True)
            print(rel, theme)
