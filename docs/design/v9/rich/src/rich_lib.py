"""Moonfall rich pass (plan v9, owner brief of 5 October 2026): shared kit for the level scenes, the Medallion night
grade, the frame ornament and the screen mockups.

Builds on the approved v9 kit (docs/design/v9/src/mf_lib.py: the one light, moon pegs, bricks, brass, text) and the v8
painting kit (artlib: blur, fbm). Only numpy and Pillow. Units: game px of the 800 x 600 playfield; S = device px per unit.

Official art: the level scenes may start from Square Enix paintings read out of the player's own game install
(ui/loadingimage/*.tex, ui/map/*). tools/texdump writes them to .cache/ (gitignored); the graded renders are committed.
"""
import json
import math
import pathlib
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

RICH = pathlib.Path(__file__).resolve().parent.parent
V9 = RICH.parent
sys.path.insert(0, str(V9 / "src"))
from mf_lib import (L, LUM, P, PEG, Img, blur, brass_shade, draw_ball, draw_brass, draw_brick, draw_moon,  # noqa: E402,F401
                    enamel, fbm, font, hexc, normals_from_height, ramp, screen, sd_circle, sd_rrect, sd_segment,
                    sector_brick, smooth, text, text_size, wrap, draw_motes, H_BLINN)

CACHE = RICH / ".cache"
OUT_SCENES = RICH / "scenes"
OUT_COMP = RICH / "composites"
OUT_SCREENS = RICH / "screens"
LEVELS = RICH / "levels"

# The board opening of the frame (what the player sees of a scene) and the engine's limits (MoonfallRules)
WALL_L, WALL_R, TOP, FOOT = 75.0, 725.0, 41.0, 594.0
PIVOT = (400.0, 87.0)
LAUNCH_CLEAR = 73.0 + 12.0          # MoonfallLevelLoader.LauncherClearance
LOWEST_EDGE = 573.0 - 12.0 - 1.0    # MoonfallLevelLoader.LowestEdge


def to_u8(px):
    return (np.clip(px, 0, 1) * 255 + 0.5).astype(np.uint8)


def save_rgb(px, path, size=None, quality=None):
    im = Image.fromarray(to_u8(px), "RGB")
    if size:
        im = im.resize(size, Image.LANCZOS)
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.suffix.lower() in (".jpg", ".jpeg"):
        im.save(path, quality=quality or 90, subsampling=0, optimize=True, progressive=True)
    else:
        im.save(path, optimize=True)
    return im


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB"), np.float32) / 255.0


def load_official(name):
    """An official texture the texdump tool wrote to .cache/, as float RGB."""
    p = CACHE / name
    if not p.exists():
        raise SystemExit(f"missing {p}: run tools/texdump (see spec-rich.md, 'Sources')")
    return load_rgb(p)


def crop_to(px, box, size):
    """Crops box = (x, y, w, h) in source px (floats allowed) and resamples to size = (W, H) with Lanczos."""
    x, y, w, h = box
    im = Image.fromarray(to_u8(px), "RGB")
    im = im.resize(size, Image.LANCZOS, box=(x, y, x + w, y + h))
    return np.asarray(im, np.float32) / 255.0


def lum(px):
    return px @ LUM


def kuwahara(px, r):
    """A fast Kuwahara filter (four quadrant means, the least varied wins): flattens texture into painted strokes and
    keeps edges, which unifies official paintings and our own under one brush. r in px."""
    if r < 1:
        return px
    h, w, _ = px.shape
    pad = np.pad(px, ((r, r), (r, r), (0, 0)), mode="edge").astype(np.float64)
    l = pad @ LUM.astype(np.float64)
    def integ(a):
        s = np.cumsum(np.cumsum(a, 0), 1)
        return np.pad(s, ((1, 0), (1, 0)) + ((0, 0),) * (a.ndim - 2))
    I, I_l, I_l2 = integ(pad), integ(l), integ(l * l)
    n = (r + 1) ** 2
    best_v = np.full((h, w), np.inf)
    out = np.zeros((h, w, 3))
    for oy, ox in ((0, 0), (0, r), (r, 0), (r, r)):
        y0, x0 = np.arange(h)[:, None] + oy, np.arange(w)[None, :] + ox
        y1, x1 = y0 + r + 1, x0 + r + 1
        box = lambda S: S[y1, x1] - S[y0, x1] - S[y1, x0] + S[y0, x0]
        m = box(I_l) / n
        v = box(I_l2) / n - m * m
        mc = box(I) / n
        better = v < best_v
        best_v = np.where(better, v, best_v)
        out = np.where(better[..., None], mc, out)
    return out.astype(np.float32)


def soft_ceiling(v, ceil, knee=0.6):
    """Compresses values above knee*ceil smoothly so nothing exceeds ceil (keeps order, keeps the darks)."""
    k = knee * ceil
    over = np.clip(v - k, 0, None)
    span = ceil - k
    return np.where(v > k, k + span * (1 - np.exp(-over / span)), v)


def night_grade(src, *, exposure=0.42, desat=0.30, tint="#B9C8F0", sky=None, sky_dark=0.45, ceiling=0.42,
                shadow="#0B1230", shadow_lift=0.05, warm=None, warm_col="#FFB060", warm_k=0.0, gamma_in=2.2):
    """The Medallion night grade (day for night). src: sRGB float. Moonlight replaces daylight: luminance kept in
    order but lowered, colour drained to about a third and turned toward the moon's cool white, the darks lifted a
    little toward the night's blue (air between us and the far land), the sky (mask `sky`) taken down further than the
    land so lit snow and cloud stand above it, and every value held under `ceiling` so a peg (lit face L about 0.6 to
    0.7) always stands well above the scene behind it. `warm` (mask) keeps a few practicals (lit windows) warm."""
    lin = np.power(np.clip(src, 0, 1), gamma_in)
    Y = lin @ LUM
    col = Y[..., None] + (lin - Y[..., None]) * desat
    t = np.power(hexc(tint), gamma_in)
    t = t / (t @ LUM)
    col = col * t * exposure
    if sky is not None:
        col = col * (1 - sky[..., None] * (1 - sky_dark))
    out = np.power(np.clip(col, 0, None), 1 / gamma_in)
    sh = hexc(shadow)
    Yo = out @ LUM
    lift = np.clip(1 - Yo / 0.25, 0, 1) * shadow_lift
    out = out + (sh - out * 0.0) * lift[..., None]
    # value ceiling on luminance, hue and saturation kept
    Yo = np.maximum(out @ LUM, 1e-5)
    Yc = soft_ceiling(Yo, ceiling)
    out = out * (Yc / Yo)[..., None]
    if warm is not None and warm_k:
        out = screen(out, hexc(warm_col) * (warm * warm_k)[..., None])
    return np.clip(out, 0, 1)


NIGHT_RAMP = [(0.00, "#04060D"), (0.10, "#080C1C"), (0.24, "#101936"), (0.42, "#1E2B58"), (0.60, "#3A4C84"),
              (0.78, "#7383B4"), (0.92, "#B4C1E0"), (1.00, "#DCE3F2")]


def night_map(src, *, curve=1.55, chroma=0.22, ceiling=0.42, sky=None, sky_drop=0.30, lift=0.0, ramp_stops=None,
              warm=None, warm_col="#FFB060", warm_k=0.0, local=0.0, warm_keep=0.35):
    """The Medallion night grade as a value map. The source's luminance (its drawing: every edge and plane) is kept in
    order and pushed through a curve that sinks the mid-tones (so fog and haze fall back and only the planes facing the
    moon stay bright), then mapped onto the Medallion night ramp (abyss, enamel blue, moonstone). A little of the
    source's own colour (chroma) rides on top so stone, gilt and foliage stay distinct. The sky (mask) drops further
    than the land. Every value stays under `ceiling` (luminance), so a peg's lit face (about 0.6 to 0.7) always stands
    well above the scene behind it. `local` adds back a little local contrast (detail) after the curve."""
    Y = np.clip(src @ LUM, 0, 1)
    if local:
        Yb = blur(Y, 6)
        Y = np.clip(Y + (Y - Yb) * local, 0, 1)
    v = np.power(Y, curve)
    if sky is not None:
        v = v * (1 - sky * sky_drop)
    v = np.clip(v + lift, 0, 1)
    base = ramp(v, ramp_stops or NIGHT_RAMP)
    # the source's chroma, drained and cooled, rides on the ramp
    c = src - Y[..., None]
    # moonlight carries little warmth: warm chroma (sunlit cloud, sand) is drained harder than cool
    cool = smooth(-0.06, 0.06, src[..., 2] - src[..., 0])
    c = c * (warm_keep + (1 - warm_keep) * cool)[..., None]
    out = base + c * chroma * (0.4 + v[..., None])
    Yo = np.maximum(out @ LUM, 1e-5)
    Yc = soft_ceiling(Yo, ceiling)
    out = out * (Yc / Yo)[..., None]
    if warm is not None and warm_k:
        out = screen(out, hexc(warm_col) * (warm * warm_k)[..., None])
    return np.clip(out, 0, 1)


def moon_glow(px, mx, my, r_core, r_wide, k_core=0.16, k_wide=0.05, col="#B9C8F0"):
    """The moon's scattered light in the air, from (mx, my) in px (it may sit beyond the frame)."""
    h, w, _ = px.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt((xx - mx) ** 2 + (yy - my) ** 2)
    a = np.exp(-(d / r_core) ** 2) * k_core + np.exp(-(d / r_wide) ** 2) * k_wide
    return screen(px, hexc(col) * a[..., None])


def vignette(px, k=0.35, cx=0.5, cy=0.45):
    h, w, _ = px.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx / w - cx) / 0.75) ** 2 + ((yy / h - cy) / 0.75) ** 2)
    return px * (1 - k * smooth(0.35, 1.0, d))[..., None]


def grain(px, amt=0.012, seed=3):
    rng = np.random.default_rng(seed)
    n = rng.standard_normal(px.shape[:2]).astype(np.float32)
    return np.clip(px + n[..., None] * amt, 0, 1)


def stars(px, S, n, seed, box, avoid=None, bright=0.7):
    """Small stars in device px; `avoid(x, y)` -> 0..1 thins them (moon glow, land)."""
    h, w, _ = px.shape
    rng = np.random.default_rng(seed)
    x0, y0, x1, y1 = box
    for _ in range(n):
        x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
        a = (0.12 + bright * rng.random() ** 3)
        if avoid is not None:
            a *= avoid(x, y)
        if a < 0.02:
            continue
        r = (0.35 + 0.45 * rng.random()) * S
        c = hexc("#DCE5FF" if rng.random() < 0.82 else "#FFE9C4")
        X0, X1 = int(max(0, x - 4 * r - 1)), int(min(w, x + 4 * r + 2))
        Y0, Y1 = int(max(0, y - 4 * r - 1)), int(min(h, y + 4 * r + 2))
        if X1 <= X0 or Y1 <= Y0:
            continue
        yy, xx = np.mgrid[Y0:Y1, X0:X1].astype(np.float32)
        g = np.exp(-((xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2) / (2 * r * r)) * a
        px[Y0:Y1, X0:X1] = screen(px[Y0:Y1, X0:X1], c * g[..., None])
    return px


# ------------------------------------------------------------------------------------------------ level files
def level_json(level_id, name, pegs, bricks, scene=None, notes=None):
    d = {"format": "moonfall-level", "version": 1, "id": level_id, "name": name,
         "playfield": {"width": 800, "height": 600}}
    if scene:
        d["scene"] = scene          # ignored by the v1 loader (unknown properties are ignored); proposed in spec-rich.md
    if notes:
        d["design"] = notes
    d["pegs"] = pegs
    d["bricks"] = bricks
    return d


def write_level(d, path):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(d, indent=2) + "\n", encoding="utf-8")


def in_bounds(x, y, r):
    return (x - r >= 75.5 and x + r <= 724.5 and y + r <= LOWEST_EDGE and y - r >= 0
            and math.hypot(x - PIVOT[0], y - PIVOT[1]) - r >= LAUNCH_CLEAR)


def resample_path(pts, spacing, start_offset=0.0):
    """Points every `spacing` px along a polyline (list of (x, y))."""
    pts = np.asarray(pts, np.float64)
    seg = np.sqrt(((pts[1:] - pts[:-1]) ** 2).sum(1))
    cum = np.concatenate([[0], np.cumsum(seg)])
    total = cum[-1]
    out = []
    s = start_offset
    while s <= total + 1e-6:
        i = min(np.searchsorted(cum, s, side="right") - 1, len(seg) - 1)
        t = (s - cum[i]) / max(seg[i], 1e-9)
        out.append(tuple(pts[i] + (pts[i + 1] - pts[i]) * t))
        s += spacing
    return out


def catmull(pts, n=16, closed=False):
    """A smooth curve through control points (Catmull-Rom)."""
    P_ = list(pts)
    if closed:
        P_ = [P_[-1]] + P_ + [P_[0], P_[1]]
    else:
        P_ = [P_[0]] + P_ + [P_[-1]]
    out = []
    for i in range(1, len(P_) - 2):
        p0, p1, p2, p3 = map(np.asarray, (P_[i - 1], P_[i], P_[i + 1], P_[i + 2]))
        for k in range(n):
            t = k / n
            out.append(tuple(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                    + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)))
    out.append(tuple(P_[-2]))
    return out
