"""Moonfall rich pass 2 (owner review of 5 October 2026: "the pictures for the characters look ugly, and it still looks
a bit plain overall"): the shared kit.

What is new over the rich pass (../rich/src):
  * FFXIV's own UI art: the window ornament, buttons, rings and laurels are the game's own textures (ui/uld/*_hr1.tex),
    read from the player's install, graded to Menphina's Medallion. Every texture used is recorded (record()) and
    written to ../sources.json, so the spec lists exactly what the plugin reads at runtime.
  * FFXIV's own type: Jupiter, AXIS and TrumpGothic from the install (r2font.py; Dalamud game fonts at runtime).
  * Bolder colour: a colour system of jewel tones and warm gold over the Medallion night (PALETTES).
  * The characters are real FFXIV characters, shown with their Triple Triad card art (r2cast.py).

Only numpy and Pillow. Units: screen px at 1x (or playfield units for the board); S = device px per unit.
Official art is read from ../rich/.cache (written by ../rich/tools/texdump, gitignored); the renders are committed.
"""
import functools
import json
import math
import pathlib
import sys

import numpy as np
from PIL import Image, ImageFilter

RICH2 = pathlib.Path(__file__).resolve().parent.parent
V9 = RICH2.parent
RICH = V9 / "rich"
CACHE = RICH / ".cache"
sys.path.insert(0, str(RICH / "src"))
sys.path.insert(0, str(V9 / "src"))

from rich_lib import (P, Img, blur, hexc, ramp, screen, smooth, srgb_to_oklab, oklab_to_srgb, load_rgb,  # noqa: E402,F401
                      save_rgb, sd_circle, sd_rrect, LUM, draw_moon, draw_ball)

OUT_SCREENS = RICH2 / "screens"
OUT_COMP = RICH2 / "composites"
OUT_SCENES = RICH2 / "scenes"
OUT_CHARS = RICH2 / "characters"
OUT_MOTION = RICH2 / "motion"
SOURCES = RICH2 / "sources.json"

# ------------------------------------------------------------------------------------------------ sources record
_USED = {}


def record(path, purpose):
    _USED.setdefault(path, [])
    if purpose not in _USED[path]:
        _USED[path].append(purpose)


def write_sources():
    old = json.loads(SOURCES.read_text(encoding="utf-8")) if SOURCES.exists() else {}
    for k, v in _USED.items():
        prev = old.get(k, [])
        prev = [prev] if isinstance(prev, str) else prev
        old[k] = sorted(set(prev) | set(v))
    SOURCES.write_text(json.dumps(dict(sorted(old.items())), indent=1, ensure_ascii=False) + "\n", encoding="utf-8")


import atexit  # noqa: E402

atexit.register(write_sources)


# ------------------------------------------------------------------------------------------------ game textures
@functools.lru_cache(None)
def uld(name):
    """A UI texture (ui/uld/<name>_hr1.tex) as float RGBA (straight alpha)."""
    p = CACHE / "uld" / f"ui_uld_{name}_hr1.png"
    if not p.exists():
        raise SystemExit(f"missing {p}: run rich/tools/texdump dumplist (see spec-rich2.md, 'Rebuild')")
    return np.asarray(Image.open(p).convert("RGBA"), np.float32) / 255.0


@functools.lru_cache(None)
def icon(n):
    p = CACHE / "portraits" / f"icon_{n:06d}.png"
    if not p.exists():
        raise SystemExit(f"missing {p}: run rich/tools/texdump icon .cache/portraits {n}")
    return np.asarray(Image.open(p).convert("RGBA"), np.float32) / 255.0


@functools.lru_cache(None)
def official(name):
    p = CACHE / name
    if not p.exists():
        raise SystemExit(f"missing {p}: run rich/tools/texdump dump")
    return load_rgb(p)


# The parts of the game's UI art that Moonfall uses: (texture, (x, y, w, h) in hr1 px, purpose)
ATLAS = {
    # The quest journal's gilt frame (Journal_Frame): art-nouveau vine corners, the crest, the rules, the ring
    "jf_tl": ("Journal_Frame", (13, 13, 180, 166), "panel corner, upper (vine)"),
    "jf_bl": ("Journal_Frame", (13, 189, 176, 177), "panel corner, lower (banner and reeds)"),
    "jf_hrule": ("Journal_Frame", (250, 13, 40, 34), "panel edge, horizontal (sampled and stretched)"),
    "jf_hrule_b": ("Journal_Frame", (250, 330, 40, 36), "panel edge, bottom"),
    "jf_vrule": ("Journal_Frame", (13, 220, 34, 40), "panel edge, vertical"),
    "jf_crest": ("Journal_Frame", (125, 102, 166, 49), "crest over a title rule"),
    "jf_rule_a": ("Journal_Frame", (125, 173, 102, 27), "short rule"),
    "jf_rule_long": ("Journal_Frame", (125, 227, 230, 43), "heavy rule (board rail)"),
    "jf_vscroll": ("Journal_Frame", (413, 13, 59, 198), "vertical scroll ornament"),
    "jf_vrule2": ("Journal_Frame", (352, 13, 35, 86), "vertical triple rule"),
    "jf_ring": ("Journal_Frame", (406, 306, 68, 73), "small gilt ring"),
    # Lord of Verminion (the Gold Saucer's game): the lattice rings and the gilt pill
    "lv_ring": ("LovmPalette", (25, 28, 191, 191), "gilt ring bezel (portraits, dial)"),       # centre (95.5, 95.5), hole r 53
    "lv_ring_big": ("LovmPalette", (149, 83, 313, 313), "great lattice ring with crest points (hero portrait, dial)"),  # centre (156.5, 156.5), hole r 89.5
    "lv_pill": ("LovmPalette", (14, 621, 252, 94), "gilt pill frame (buttons)"),
    "lv_frame": ("LovmMiniMapFrame", (11, 12, 138, 141), "filigree square frame"),
    # Triple Triad (the Gold Saucer's card game): the laurel and ribbon, the crown
    "tt_laurel": ("TripleTriadResultCrown", (40, 58, 640, 160), "laurel and ribbon (aced, Full Moon)"),
    "tt_crown": ("TripleTriadResultCrown", (305, 36, 112, 137), "crowned plaque"),
    "tt_cardsel": ("TripleTriadCardSelect", (6, 12, 220, 272), "card selection glow"),
    # Doman mahjong's intro: a filigree frame
    "emj_frame": ("EMJIntroParts03", (0, 0, 128, 128), "filigree frame (level tiles)"),
    # The PvP rank emblem: gold wing with turquoise inlay, the spire, the crest shield
    "pvp_wing": ("PVPRankEmblem3", (6, 0, 338, 228), "gilt wing with turquoise inlay (launcher crest)"),
    "pvp_spire": ("PVPRankEmblem3", (2, 233, 76, 342), "gilt spire (finials)"),
    "pvp_shield": ("PVPRankEmblem3", (100, 238, 227, 375), "gilt crest shield (escutcheon)"),
    # The scholar's job gauge: the triple gem frame
    "sch_gems": ("JobHudSCH0", (7, 7, 290, 139), "triple gem frame (turns left)"),
    # The window kit: selected item frame, buttons, tabs
    "win_sel": ("WindowA_BgSelected_Corner", (3, 3, 58, 170), "selected frame (focus)"),
    "btn_a": ("ButtonA", (3, 2, 194, 48), "button plate"),
    "tab_a": ("TabButtonA", (3, 2, 170, 48), "tab"),
    "jd_panel": ("Journal_Detail", (40, 180, 640, 128), "the journal's textured window ground (graded to enamel)"),
    "gauge": ("Parameter_Gauge", (2, 42, 316, 36), "gauge frame"),
}


# the rings in their crops: centre x, y, hole radius, the gilt ring's outer radius (hr px)
RING_GEOM = {"lv_ring": (95.5, 95.5, 53.0, 76.0), "lv_ring_big": (156.5, 156.5, 89.5, 123.0)}

# parts whose box takes in pieces of neighbours: keep only the connected piece holding this seed (crop px)
SEEDS = {"jf_tl": (12, 12), "jf_bl": (12, 160)}


def _component(rgba, seed, thr=0.16, grow=7):
    """The connected piece of an atlas crop that holds `seed`, with its soft shadow (grown by `grow` px)."""
    a = rgba[..., 3] > thr
    m = np.zeros_like(a)
    m[seed[1], seed[0]] = True
    if not a[seed[1], seed[0]]:
        ys, xs = np.nonzero(a)
        i = np.argmin((ys - seed[1]) ** 2 + (xs - seed[0]) ** 2)
        m[ys[i], xs[i]] = True
    while True:
        im = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))
        n = (np.asarray(im) > 0) & a
        if (n == m).all():
            break
        m = n
    g = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * grow + 1)), np.float32) / 255
    g = np.asarray(Image.fromarray((g * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)), np.float32) / 255
    keep = np.maximum(m.astype(np.float32), g)
    out = rgba.copy()
    out[..., 3] *= keep
    return out


@functools.lru_cache(None)
def _part(key):
    name, (x, y, w, h), purpose = ATLAS[key]
    a = uld(name)[y:y + h, x:x + w]
    if key in SEEDS:
        a = _component(a, SEEDS[key])
    if key in ("lv_ring", "lv_ring_big"):
        # round 2 (UX m5 stray arc, M5 heavy dial band): each ring sits on a wide textured backing plate that overlaps
        # its neighbour in the texture; keep the gilt ring itself (and the big ring's crest points), drop the plate
        cx, cy, hole, gilt = RING_GEOM[key]
        yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]].astype(np.float32)
        d = np.sqrt((xx + 0.5 - cx) ** 2 + (yy + 0.5 - cy) ** 2)
        keep = np.clip((gilt + 2.5 - d) / 2.0, 0, 1)
        if key == "lv_ring_big":
            keep = np.maximum(keep, np.clip((22 - np.abs(xx + 0.5 - cx)) / 3, 0, 1) * np.clip((153 - d) / 2, 0, 1))
        a = a.copy()
        a[..., 3] *= keep
    return a


def part(key):
    name, _box, purpose = ATLAS[key]
    record(f"ui/uld/{name}_hr1.tex", purpose)
    return _part(key)


# ------------------------------------------------------------------------------------------------ the colour system
# Menphina's Medallion stays (lapis night, brass and gilt, moonstone, cream); rich pass 2 adds jewel tones and warm gold.
C = {
    "abyss": "#05060F", "lapis": "#16245A", "lapis_hi": "#2A3F8C", "enamel": "#1B2A63",
    "sapphire": "#1D4DB8", "amethyst": "#6B3FA8", "garnet": "#8E2440", "emerald": "#13805E", "teal": "#137C86",
    "aquamarine": "#5FD0D6", "rose": "#C76A8C",
    "gold": "#E8B54A", "gold_hi": "#FFE3A0", "gold_deep": "#8A5A12", "amber": "#FFB45E",
    "cream": "#F4ECD8", "ink": "#C9CFE6", "ink_dim": "#8E97BC", "moon": "#E9EDF6",
}

# A palette per level (and per screen): the night's base hue, two jewel accents and the warm light
PALETTES = {
    "base-p1": dict(name="Sapphire and amber (the chart)", sky="#0E1C4E", deep="#070C24", jewel1="#1D4DB8", jewel2="#137C86",
                    warm="#FFB45E", rim="#7FB2FF"),
    "base-p2": dict(name="Glacier and rose dawn (the Holy See)", sky="#14204F", deep="#080B22", jewel1="#2B6FD0", jewel2="#B0577E",
                    warm="#FFC27A", rim="#A9D2FF"),
    "base-p3": dict(name="Emerald wood and amethyst sky (the Shroud)", sky="#1F1650", deep="#050D10", jewel1="#13805E", jewel2="#6B3FA8",
                    warm="#FFB45E", rim="#9EE6C4"),
    "exp-p1": dict(name="Turquoise harbour and gilt (Sharlayan)", sky="#0F2552", deep="#04101A", jewel1="#137C86", jewel2="#3B5FD0",
                   warm="#FFC86E", rim="#8FF0E8"),
    "exp-p2": dict(name="Violet and aquamarine (the open sea)", sky="#24154F", deep="#070616", jewel1="#6B3FA8", jewel2="#2FA7B4",
                   warm="#FFB45E", rim="#C9B8FF"),
    "exp-p3": dict(name="Amethyst and earthlight (the moon)", sky="#1A1240", deep="#040410", jewel1="#7A4FC8", jewel2="#2F7FD0",
                   warm="#FFD08A", rim="#C9D6FF"),
}


# ------------------------------------------------------------------------------------------------ grades
def gild(rgba, k=0.55, warm=0.0, lift=0.0):
    """The Medallion grade for the game's gold: its lightness kept, its hue pulled toward the Medallion's warm gilt,
    chroma raised (bolder), so FFXIV's pale journal brass and the Gold Saucer's gold become one gilt. warm > 0 pushes
    toward amber."""
    rgb = rgba[..., :3]
    lab = srgb_to_oklab(rgb)
    Lc, a, b = lab[..., 0], lab[..., 1], lab[..., 2]
    # target hue: Medallion gilt (OKLab hue about 75 deg), amber when warm
    tgt = math.radians(78 - 18 * warm)
    C_ = np.sqrt(a * a + b * b)
    hue = np.arctan2(b, a)
    # pull hue toward target where the pixel is golden (chroma > 0.02); greys stay grey
    w = np.clip(C_ / 0.05, 0, 1) * k
    dh = np.angle(np.exp(1j * (tgt - hue)))
    hue2 = hue + dh * w
    C2 = C_ * (1.0 + 0.45 * k)
    L2 = np.clip(Lc * (1 + lift), 0, 1)
    out = oklab_to_srgb(np.stack([L2, C2 * np.cos(hue2), C2 * np.sin(hue2)], -1))
    return np.concatenate([out, rgba[..., 3:4]], -1)


def tint_grey(rgba, stops, contrast=1.0):
    """Colours a grey game texture (a window ground) through a ramp, by its luminance: FFXIV's grey windows become
    Medallion enamel."""
    Y = rgba[..., :3] @ LUM
    m = Y.mean()
    t = np.clip((Y - m) * contrast + 0.5, 0, 1)
    col = ramp(t, stops)
    return np.concatenate([col, rgba[..., 3:4]], -1)


# ------------------------------------------------------------------------------------------------ drawing
def resample(rgba, w, h):
    """Resamples straight-alpha RGBA to (w, h) device px with Lanczos on premultiplied colour (no dark fringes)."""
    w, h = max(1, int(round(w))), max(1, int(round(h)))
    pm = rgba.copy()
    pm[..., :3] *= pm[..., 3:4]
    chans = [np.asarray(Image.fromarray(pm[..., c].astype(np.float32), "F").resize((w, h), Image.LANCZOS)) for c in range(4)]
    out = np.stack(chans, -1)
    out = np.clip(out, 0, 1)
    a = np.maximum(out[..., 3:4], 1e-6)
    out[..., :3] = np.where(out[..., 3:4] > 1e-4, out[..., :3] / a, 0)
    return np.clip(out, 0, 1)


def blit(img, rgba, x, y, w, h, alpha=1.0, flipx=False, flipy=False, mode="over"):
    """Draws an RGBA image (any size) into the canvas at units (x, y), size (w, h) units."""
    S = img.S
    X0f, Y0f = x * S, y * S
    src = rgba[:, ::-1] if flipx else rgba
    src = src[::-1] if flipy else src
    arr = resample(src, w * S, h * S)
    X0, Y0 = int(round(X0f)), int(round(Y0f))
    xs, ys = max(0, -X0), max(0, -Y0)
    X1, Y1 = min(img.w, X0 + arr.shape[1]), min(img.h, Y0 + arr.shape[0])
    if X1 <= max(X0, 0) or Y1 <= max(Y0, 0):
        return
    a = arr[ys:ys + Y1 - max(Y0, 0), xs:xs + X1 - max(X0, 0)]
    dst = img.px[max(Y0, 0):Y1, max(X0, 0):X1]
    al = a[..., 3:4] * alpha
    if mode == "add":
        img.px[max(Y0, 0):Y1, max(X0, 0):X1] = np.clip(dst + a[..., :3] * al, 0, 1)
    elif mode == "screen":
        img.px[max(Y0, 0):Y1, max(X0, 0):X1] = screen(dst, a[..., :3] * al)
    else:
        img.px[max(Y0, 0):Y1, max(X0, 0):X1] = dst * (1 - al) + a[..., :3] * al


def shadow_under(img, x0, y0, x1, y1, r=8.0, off=(3.0, 5.0), soft=8.0, k=0.55):
    sl, X, Y = img.win((x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) / 2 + soft * 3 + 10)
    sd = sd_rrect(X - off[0], Y - off[1], x0, y0, x1, y1, r)
    m = np.clip(1 - sd / soft, 0, 1) ** 2 * (sd > -1e9)
    inside = img.cov(sd_rrect(X, Y, x0, y0, x1, y1, r))
    img.mul(sl, hexc("#010206"), np.clip(m, 0, 1) * (1 - inside) * k)


def stretch_x(rgba, w_px):
    """Stretches a strip horizontally to w_px (a plain rule section repeats perfectly when stretched)."""
    return np.asarray(Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").resize((max(1, int(w_px)), rgba.shape[0]),
                                                                                   Image.BILINEAR), np.float32) / 255


def gilt_frame(img, x0, y0, x1, y1, scale=0.5, corners=True, k_gild=0.55, warm=0.0, alpha=1.0):
    """FFXIV's quest-journal frame around a box: the gilt triple-rule band (24 hr px), and the art-nouveau corners (the
    vine above, the banner and reeds below), mirrored for the right side. scale: units per hr px (0.5 at 1x screens:
    the game draws hr textures at half size on a 1080p-class UI scale)."""
    if not corners:
        return gilt_band(img, x0, y0, x1, y1, scale, k_gild, warm, alpha)
    tl = gild(part("jf_tl"), k_gild, warm)
    bl = gild(part("jf_bl"), k_gild, warm)
    hr = gild(part("jf_hrule"), k_gild, warm)
    hb = gild(part("jf_hrule_b"), k_gild, warm)
    vr = gild(part("jf_vrule"), k_gild, warm)
    o = 6 * scale                                   # the band's outer edge sits 6 hr px inside each part's box
    band = 24 * scale
    W, H = x1 - x0, y1 - y0
    cw, ch = (tl.shape[1] * scale, tl.shape[0] * scale) if corners else (band + o, band + o)
    bw, bh = (bl.shape[1] * scale, bl.shape[0] * scale) if corners else (band + o, band + o)
    if not corners:
        cw = ch = bw = bh = band + o + 2
    # edges first (corners overlap them)
    top_len = W - 2 * cw + 2 * o + 2
    if top_len > 0:
        blit(img, hr, x0 - o + cw - 1, y0 - o, top_len, hr.shape[0] * scale, alpha)
    bot_len = W - 2 * bw + 2 * o + 2
    if bot_len > 0:
        blit(img, hb, x0 - o + bw - 1, y1 + o - hb.shape[0] * scale + 2 * scale, bot_len, hb.shape[0] * scale, alpha)
    side_len = H - ch - bh + 2 * o + 2
    if side_len > 0:
        for flip, xx in ((False, x0 - o), (True, x1 + o - vr.shape[1] * scale)):
            blit(img, vr, xx, y0 - o + ch - 1, vr.shape[1] * scale, side_len, alpha, flipx=flip)
    if corners:
        # round 3 (UX m8): the corner art is cut square where the game's next piece would continue it; the vine
        # stem beyond the band fades out over its last 28 hr px instead (the band's own columns are kept)
        tl, bl = tl.copy(), bl.copy()
        for arr, top in ((tl, False), (bl, True)):
            hh = arr.shape[0]
            ramp_ = np.clip((np.arange(hh) if top else (hh - 1 - np.arange(hh))) / 28.0, 0, 1)
            cols = np.arange(arr.shape[1]) > 34
            arr[..., 3] *= np.where(cols[None, :], ramp_[:, None], 1.0)
        blit(img, tl, x0 - o, y0 - o, cw, ch, alpha)
        blit(img, tl, x1 + o - cw, y0 - o, cw, ch, alpha, flipx=True)
        blit(img, bl, x0 - o, y1 + o - bh, bw, bh, alpha)
        blit(img, bl, x1 + o - bw, y1 + o - bh, bw, bh, alpha, flipx=True)
    return band


def gilt_band(img, x0, y0, x1, y1, scale=0.5, k_gild=0.55, warm=0.0, alpha=1.0):
    """The journal's gilt triple-rule band round a box, built as one image: the horizontal rule stretched along the
    top and bottom, its rotation down the sides, joined by 45-degree mitres, so there are no corner blocks or seams."""
    hr = gild(part("jf_hrule"), k_gild, warm)              # 40 x 34 hr px: the band is rows 6..30 (24 px), shadow below
    o = 6 * scale
    S = img.S
    W = int(round((x1 - x0 + 2 * o) * S))
    H = int(round((y1 - y0 + 2 * o) * S))
    bh = hr.shape[0] * scale * S                           # strip height in device px (with its shadow)
    strip_h = max(1, int(round(bh)))
    col = resample(hr[:, 18:22], 4, strip_h)[:, 1:2]       # one column of the rule, at device size
    top = np.repeat(col, W, 1)
    side = np.repeat(np.transpose(col, (1, 0, 2)), H, 0)  # the rule turned: its outer edge on the left
    canvas = np.zeros((H, W, 4), np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    regions = []
    # top strip (outer edge at the top), bottom (flipped: outer edge at the bottom), left, right (flipped)
    t = np.zeros_like(canvas); t[:strip_h] = top[:min(strip_h, H)]
    b = np.zeros_like(canvas); b[H - strip_h:] = top[::-1][:min(strip_h, H)]
    l_ = np.zeros_like(canvas); l_[:, :strip_h] = side[:, :min(strip_h, W)]
    r_ = np.zeros_like(canvas); r_[:, W - strip_h:] = side[:, ::-1][:, :min(strip_h, W)]
    # mitres: each pixel belongs to the nearest edge
    dt, db, dl, dr = yy, H - 1 - yy, xx, W - 1 - xx
    near = np.argmin(np.stack([dt, db, dl, dr]), 0)
    for i, layer in enumerate((t, b, l_, r_)):
        m = (near == i)[..., None]
        canvas = np.where(m, layer, canvas)
    X0, Y0 = int(round((x0 - o) * S)), int(round((y0 - o) * S))
    from r2lib_blit import blit_arr_dev
    blit_arr_dev(img, canvas, X0, Y0, alpha)
    return 24 * scale


@functools.lru_cache(None)
def _ground(seed=0):
    """The journal ground's grain only (high-passed so its own gradient and bevel go), mirror-tiled so no seam shows."""
    g = part("jd_panel")
    Y = g[..., :3] @ LUM
    hp = Y - blur(Y.astype(np.float32), 12)
    t = np.concatenate([hp, hp[:, ::-1]], 1)
    t = np.concatenate([t, t[::-1]], 0)
    rgba = np.stack([t + 0.5] * 3 + [np.ones_like(t)], -1).astype(np.float32)
    return rgba


def enamel_ground(img, x0, y0, x1, y1, r=6.0, stops=None, alpha=1.0, sheen=0.10, jewel=None):
    """A panel's ground: the journal window's own textured grey, graded to Medallion enamel (lapis, or a jewel ramp),
    with a soft sheen toward the upper-left light."""
    stops = stops or [(0.0, "#0A1030"), (0.5, "#16245A"), (1.0, "#22357A")]
    g = _ground()
    S = img.S
    w, h = int((x1 - x0) * S), int((y1 - y0) * S)
    # tile the ground (it is 742 x 175 hr px) at half scale
    tile = resample(g, g.shape[1] * 0.5 * S, g.shape[0] * 0.5 * S)
    reps_x, reps_y = w // tile.shape[1] + 2, h // tile.shape[0] + 2
    big = np.tile(tile, (reps_y, reps_x, 1))[:h, :w]
    tt = np.clip(0.5 + (big[..., 0] - 0.5) * 7.0, 0, 1)
    col = ramp(tt * 0.55 + 0.30, stops)
    sl, X, Y = img.win((x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) / 2 + 2)
    # place col into the window's coordinates
    Ys, Xs = sl
    ox, oy = int(round(x0 * S)) - Xs.start, int(round(y0 * S)) - Ys.start
    canvas = np.zeros(X.shape + (3,), np.float32)
    hh, ww = canvas.shape[:2]
    cy0, cx0 = max(0, oy), max(0, ox)
    cy1, cx1 = min(hh, oy + h), min(ww, ox + w)
    canvas[cy0:cy1, cx0:cx1] = col[cy0 - oy:cy1 - oy, cx0 - ox:cx1 - ox]
    # vertical light: brighter at the top (the light is above)
    t = np.clip((Y - y0) / max(1, (y1 - y0)), 0, 1)
    canvas = canvas * (1.12 - 0.30 * t)[..., None]
    if jewel:
        jw = hexc(jewel)
        canvas = screen(canvas, jw * (0.10 * np.exp(-((X - x1) / (0.6 * (x1 - x0))) ** 2 - ((Y - y1) / (0.6 * (y1 - y0))) ** 2))[..., None])
    sh = np.exp(-(((X - x0) / (0.9 * (x1 - x0))) ** 2 + ((Y - y0) / (0.9 * (y1 - y0))) ** 2))
    canvas = screen(canvas, hexc("#9FB2E8") * (sh * sheen)[..., None])
    cov = img.cov(sd_rrect(X, Y, x0, y0, x1, y1, r)) * alpha
    img.over(sl, canvas, cov)


def panel(img, x0, y0, x1, y1, corners=True, stops=None, jewel=None, alpha=0.96, shadow=True, scale=0.5, warm=0.0):
    """A window: journal-frame gilt over an enamel ground, with a soft shadow below-right."""
    if shadow:
        shadow_under(img, x0, y0, x1, y1, r=4, off=(4, 7), soft=14, k=0.6)
    enamel_ground(img, x0, y0, x1, y1, r=4, stops=stops, alpha=alpha, jewel=jewel)
    gilt_frame(img, x0, y0, x1, y1, scale=scale, corners=corners, warm=warm)


# ------------------------------------------------------------------------------------------------ type (game fonts)
def _gradient_fill(h, stops):
    t = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    return ramp(t, stops)


# Round 2 (UX M5): the text floors at the 640 x 480 minimum. A label's cap height must be at least 7 px and a number's
# at least 8 px on the display (AXIS caps are 0.72 of the font's px size, so 7 px caps is a 9.7 px font; TrumpGothic's
# digits are its caps). DISPLAY is the display px per unit of the image being drawn (set by the caller: 1.0 for a
# 640 screen, 0.8 for a board shown in the 640 window); None skips the check (1280 screens).
DISPLAY = {"scale": None, "violations": []}
FLOOR_LABEL, FLOOR_NUMBER = 7.0, 8.0


def _check_floor(s, face, size):
    sc = DISPLAY["scale"]
    if sc is None:
        return
    import r2font
    top, base = r2font.cap_box(face)
    cap = (base - top) * size * sc
    digits = sum(ch.isdigit() for ch in s) >= max(1, len(s.strip()) // 2)
    need = FLOOR_NUMBER if digits else FLOOR_LABEL
    if cap + 1e-6 < need:
        DISPLAY["violations"].append((s, face, round(cap, 2), need))


def gtext(img, x, y, s, face="axis", size=14.0, col="#F4ECD8", anchor="lm", edge=None, edge_w=1.4, glow=None,
          glow_r=6.0, glow_k=0.6, gilt=False, shadow=0.0, tracking=0.0, alpha=1.0, grad=None):
    """Text in a game face. size: cap-to-cap em in units (the face's cell). anchor: l/m/r by x, t/m/b/s by y (m centres
    the caps). edge: an outline colour (FFXIV draws most UI text with a dark edge); glow: a soft halo; gilt: a vertical
    gold gradient fill (titles); grad: explicit gradient stops."""
    import r2font
    _check_floor(s, face, size)
    S = img.S
    m, wpx = r2font.render_mask(s, face, size * S, tracking * S)
    f = r2font.face(face)
    k = size * S / f["em"]
    top, base = r2font.cap_box(face)
    H, W = m.shape
    ax = {"l": 0, "m": 0.5, "r": 1.0}[anchor[0]]
    if anchor[1] == "m":
        oy = (top + base) / 2 * size * S + 2
    elif anchor[1] == "t":
        oy = top * size * S + 2
    elif anchor[1] in "bs":
        oy = base * size * S + 2
    else:
        oy = 2
    X0 = int(round(x * S - wpx * ax)) - 2
    Y0 = int(round(y * S - oy))
    pad = int(glow_r * S * 3 + edge_w * S + 4)
    mm = np.pad(m, pad)
    X0 -= pad
    Y0 -= pad
    H, W = mm.shape
    xs, ys = max(0, -X0), max(0, -Y0)
    X1, Y1 = min(img.w, X0 + W), min(img.h, Y0 + H)
    if X1 <= max(X0, 0) or Y1 <= max(Y0, 0):
        return wpx / S
    region = (slice(max(Y0, 0), Y1), slice(max(X0, 0), X1))
    msk = mm[ys:ys + Y1 - max(Y0, 0), xs:xs + X1 - max(X0, 0)]
    if glow:
        g = blur(msk, glow_r * S * 0.6)
        img.px[region] = screen(img.px[region], hexc(glow) * (np.clip(g * 2.2, 0, 1) * glow_k * alpha)[..., None])
    if shadow:
        sh = np.roll(np.roll(blur(msk, 1.2 * S), int(1.5 * S), 0), int(1.0 * S), 1)
        img.px[region] = img.px[region] * (1 - np.clip(sh * shadow * alpha, 0, 1))[..., None]
    if edge:
        r_ = max(1, int(round(edge_w * S)))
        e = np.asarray(Image.fromarray((msk * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * r_ + 1)), np.float32) / 255
        e = blur(e, 0.5 * S)
        a = np.clip(e * 1.4, 0, 1) * alpha
        img.px[region] = img.px[region] * (1 - a[..., None]) + hexc(edge) * a[..., None]
    if gilt or grad:
        stops = grad or [(0.0, "#FFF4C8"), (0.42, "#F2CB6E"), (0.58, "#C88A2C"), (1.0, "#FFE7A0")]
        # the gradient spans the caps
        hh = msk.shape[0]
        t = np.clip((np.arange(hh)[:, None] - (pad + oy - (base - top) * size * S + (2 if anchor[1] != "m" else 0)) + 0.0) /
                    max(1.0, (base - top) * size * S), 0, 1)
        t = np.clip((np.arange(hh)[:, None] - (pad + 2 + top * size * S)) / max(1.0, (base - top) * size * S), 0, 1)
        fill = ramp(np.broadcast_to(t, msk.shape), stops)
    else:
        fill = hexc(col)[None, None, :]
    a = np.clip(msk, 0, 1)[..., None] * alpha
    img.px[region] = img.px[region] * (1 - a) + fill * a
    return wpx / S


def gwidth(s, face="axis", size=14.0, tracking=0.0):
    import r2font
    return r2font.render_mask(s, face, size * 4, tracking * 4)[1] / 4


def gwrap(s, face, size, maxw):
    words, lines, cur = s.split(), [], ""
    for w_ in words:
        t = (cur + " " + w_).strip()
        if gwidth(t, face, size) <= maxw or not cur:
            cur = t
        else:
            lines.append(cur)
            cur = w_
    if cur:
        lines.append(cur)
    return lines


RINGS = {"lv_ring": (95.5, 95.5, 53.0, 78.5), "lv_ring_big": (156.5, 156.5, 89.5, 125.5)}   # centre x, y, hole r, outer r


def ring(img, cx, cy, hole_r, key="lv_ring", k_gild=0.5, warm=0.0, alpha=1.0):
    """One of Lord of Verminion's gilt rings, scaled so its hole is hole_r units, centred on (cx, cy)."""
    rx, ry, hr, outer = RINGS[key]
    a = gild(part(key), k_gild, warm)
    s = hole_r / hr
    blit(img, a, cx - rx * s, cy - ry * s, a.shape[1] * s, a.shape[0] * s, alpha)
    return outer * s


def disc_image(img, rgb_or_rgba, cx, cy, r, alpha=1.0, feather=0.8):
    """An image clipped to a disc (a portrait under a ring)."""
    src = rgb_or_rgba
    if src.shape[-1] == 3:
        src = np.concatenate([src, np.ones(src.shape[:2] + (1,), np.float32)], -1)
    S = img.S
    arr = resample(src, 2 * r * S, 2 * r * S)
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt((xx + 0.5 - w / 2) ** 2 + (yy + 0.5 - h / 2) ** 2)
    arr[..., 3] *= np.clip((w / 2 - d) / max(feather * S, 0.5), 0, 1)
    blit_arr(img, arr, cx - r, cy - r, alpha)


def blit_arr(img, arr, x, y, alpha=1.0):
    """Draws an already device-sized RGBA array at units (x, y)."""
    S = img.S
    X0, Y0 = int(round(x * S)), int(round(y * S))
    xs, ys = max(0, -X0), max(0, -Y0)
    X1, Y1 = min(img.w, X0 + arr.shape[1]), min(img.h, Y0 + arr.shape[0])
    if X1 <= max(X0, 0) or Y1 <= max(Y0, 0):
        return
    a = arr[ys:ys + Y1 - max(Y0, 0), xs:xs + X1 - max(X0, 0)]
    al = a[..., 3:4] * alpha
    dst = img.px[max(Y0, 0):Y1, max(X0, 0):X1]
    img.px[max(Y0, 0):Y1, max(X0, 0):X1] = dst * (1 - al) + a[..., :3] * al


def size_for_cap(face, cap_px, scale=1.0):
    """The gtext size whose cap height is cap_px display px at `scale` display px per unit."""
    import r2font
    top, base = r2font.cap_box(face)
    return cap_px / ((base - top) * scale)
