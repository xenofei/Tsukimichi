"""Moonfall's screens, rich pass 2 (round 2): every screen at 1280 x 800 and at the 640 x 480 minimum, built from
FFXIV's own UI art and type, in the bolder colour system, all from one progress state (r2state).

  py -3 screens2.py [title|map|characters|levels|hud|power|fever|tally|pause|pegmarks|lineup ...]

Writes screens/<name>-1280.png and -640.png (lineup: characters/lineup.png; pegmarks: screens/pegmarks.png). The 640
screens are checked against the text floors (r2lib.DISPLAY); any text below them is printed and fails the run.
"""
import math
import sys

import numpy as np

import r2lib
from r2lib import (C, PALETTES, Img, OUT_SCREENS, blit, blur, gild, gtext, gwidth, gwrap, hexc, official, panel,
                   part, ramp, save_rgb, screen, smooth, gilt_frame, shadow_under, ring, disc_image, load_rgb, OUT_COMP,
                   record, sd_rrect)
import r2kit as K
import r2cast
import r2state as ST
from rich_lib import crop_to, fbm, grain, moon_glow, night_lab, stars, draw_moon, moon_emissive

INK2 = "#C3CBEA"           # secondary text with meaning: at least 4.5:1 on every panel ground (UX m10)
INK3 = "#AEB6D6"           # tertiary (decorative captions), 4.5:1 on the darkest grounds


# ------------------------------------------------------------------------------------------------ backdrops
def jewel_night(W, H, S, pal, seed=5, moon=(0.12, 0.10), nebula=0.55):
    p = PALETTES[pal] if isinstance(pal, str) else pal
    h, w = int(H * S), int(W * S)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = np.clip(yy / h * 0.8 + xx / w * 0.2, 0, 1)
    base = ramp(t, [(0, p["sky"]), (1, p["deep"])])
    n1 = fbm(h, w, 220 * S, 4, seed)
    n2 = fbm(h, w, 160 * S, 4, seed + 7)
    j1 = smooth(0.50, 0.85, n1)[..., None] * hexc(p["jewel1"]) * 0.30 * nebula
    j2 = smooth(0.55, 0.90, n2)[..., None] * hexc(p["jewel2"]) * 0.26 * nebula
    px = screen(screen(base, j1), j2)
    px = moon_glow(px, moon[0] * w, moon[1] * h, 260 * S, 900 * S, 0.16, 0.08, col="#C9D6FF")
    return stars(px, S, int(W * H / 2600), seed, (0, 0, w, h * 0.85), bright=0.6)


TITLE_PAL = dict(name="Amethyst and sapphire (title)", sky="#1C2E8E", deep="#070A22", jewel1="#7A4FC8", jewel2="#6A4FC8",
                 warm="#FFB45E", rim="#C9B8FF")
PALETTES["title"] = TITLE_PAL


def jewel_grade(px, pal, sky=None, k=1.0, value_hues=None):
    """Bolder colour over a night-graded painting, lightness kept exactly."""
    from r2lib import srgb_to_oklab, oklab_to_srgb
    p = PALETTES[pal] if isinstance(pal, str) else pal
    lab = srgb_to_oklab(px)
    L = lab[..., 0]

    def ab(hx):
        v = srgb_to_oklab(hexc(hx)[None, None])[0, 0]
        n = math.hypot(v[1], v[2]) + 1e-6
        return v[1] / n, v[2] / n
    a1, b1 = ab(p["jewel1"])
    a2, b2 = ab(p["jewel2"])
    ad, bd = ab(p["sky"])
    t = smooth(0.15, 0.55, L)
    ta, tb = ad * (1 - t) + a2 * t, bd * (1 - t) + b2 * t
    if sky is not None:
        ta, tb = ta * (1 - sky) + a1 * sky, tb * (1 - sky) + b1 * sky
    n = np.sqrt(ta * ta + tb * tb) + 1e-6
    ta, tb = ta / n, tb / n
    Cc = np.sqrt(lab[..., 1] ** 2 + lab[..., 2] ** 2)
    hi = smooth(0.55, 0.80, L)
    Cn = (0.035 + Cc * 1.6) * (1 - 0.7 * hi) * k
    lab[..., 1] = lab[..., 1] * 0.35 + ta * Cn * 0.65
    lab[..., 2] = lab[..., 2] * 0.35 + tb * Cn * 0.65
    return oklab_to_srgb(lab)


def title_background(W, H, S, focus_x=0.62):
    record("ui/loadingimage/-nowloading_base07.tex", "title backdrop (Dravania, Sohm Al), graded at load")
    src = official("ui_loadingimage_-nowloading_base07.png")
    ch = 1010.0
    cw = min(1920.0, ch * W / H)
    x0 = np.clip(960.0 - cw * focus_x, 0, 1920 - cw)
    px = crop_to(src, (x0, 40.0, cw, ch), (int(W * S), int(H * S)))
    yy = np.mgrid[0:px.shape[0], 0:px.shape[1]][0] / px.shape[0]
    sky = blur(smooth(0.0, 0.10, px[..., 2] - px[..., 0]) * smooth(0.7, 0.3, yy), 3 * S)
    out = night_lab(px, ceiling=0.66, knee=0.40, exposure=0.95, gamma=1.6, sky=sky, sky_drop=0.25, S=S)
    return jewel_grade(out, "title", sky)


def chart_background(W, H, S, crop):
    """The world map as a moonlit chart: sapphire lands, teal seas (round 2, designer M1: the second jewel in the
    sea), lightness kept."""
    record("ui/map/world/01/world01_m.tex", "Adventure map and level select (the world map), graded at load")
    src = official("ui_map_world_01_world01_m.png")[587:587 + 872]
    px = crop_to(src, crop, (int(W * S), int(H * S)))
    out = night_lab(px, gamma=1.35, exposure=0.85, ceiling=0.54, warm_keep=0.25, chroma_mid=0.45, S=S) * 0.86
    from dress2 import jewel as _jewel
    from r2lib import srgb_to_oklab
    L = srgb_to_oklab(out)[..., 0]
    sea = smooth(0.42, 0.55, blur(L, 3 * S))
    # dress2.jewel works on board grids; apply its region logic directly here
    from r2lib import oklab_to_srgb
    lab = srgb_to_oklab(out)
    def d(hx):
        v = srgb_to_oklab(hexc(hx)[None, None])[0, 0]
        n = math.hypot(v[1], v[2])
        return v[1] / n, v[2] / n
    la, lb = d("#2B4FC0")
    sa, sb = d("#1FA0B0")
    cl = 0.07 * smooth(0.04, 0.2, L) * (1 - sea)
    cs = 0.05 * smooth(0.04, 0.2, L) * sea
    lab[..., 1] = lab[..., 1] * 0.3 + la * cl + sa * cs
    lab[..., 2] = lab[..., 2] * 0.3 + lb * cl + sb * cs
    return moon_glow(oklab_to_srgb(lab), -60 * S, -60 * S, 600 * S, 1400 * S, 0.10, 0.05)


# ------------------------------------------------------------------------------------------------ shared parts
def hexc_to(hx, k):
    v = hexc(hx) * (1 - k) + hexc("#FFF6E8") * k
    return "#%02X%02X%02X" % tuple(int(c * 255) for c in v)


def moon_on(img, mx, my, mr):
    img.px, _ = moon_emissive(img.px, img.S, mx, my, mr, face_col="#EEF0F8")


def board_thumb(level_id):
    return load_rgb(OUT_COMP / f"{level_id}@2x.png")[82:1188, 150:1450]


def place(img, rgb, x, y, w, h):
    blit(img, np.concatenate([rgb, np.ones(rgb.shape[:2] + (1,), np.float32)], -1), x, y, w, h)


def framed_thumb(img, level_id, x, y, w, scale=0.34):
    h = w * 1106 / 1300
    shadow_under(img, x, y, x + w, y + h, r=2, off=(3, 6), soft=10, k=0.6)
    place(img, board_thumb(level_id), x, y, w, h)
    gilt_frame(img, x, y, x + w, y + h, scale=scale, corners=False)
    return h


def fit_text(s, face, size, maxw, floor=None):
    """The largest size up to `size` at which s fits maxw (never below floor)."""
    while gwidth(s, face, size) > maxw and size > (floor or 1):
        size -= 0.5
    return size


def logotype(img, cx, y, size, sub=None, sub_size=None):
    gtext(img, cx, y, "MOONFALL", "jupiter", size, C["gold_hi"], anchor="mm", edge="#140A02", edge_w=2.2, gilt=True,
          glow="#FFB45E", glow_r=10, glow_k=0.30, shadow=0.7, tracking=size * 0.06)
    w = gwidth("MOONFALL", "jupiter", size, size * 0.06)
    K.crest_rule(img, cx, y + size * 0.42, w * 0.86, crest=True, scale=0.5 * size / 120)
    if sub:
        gtext(img, cx, y + size * 0.66, sub, "jupiter", sub_size or size * 0.30, C["cream"], anchor="mm", edge="#05070F",
              edge_w=1.2, glow="#9FB2FF", glow_k=0.15)


def btn(img, x0, y0, x1, y1, label, size, state="normal", sub=None, small=False, primary=False):
    """Buttons (round 2, UX m16): Jupiter for primary pills and titles, AXIS (the game's button face) for the rest."""
    face = "jupiter" if primary else "axis"
    if face == "axis":
        size = size * 0.72
    K.button(img, x0, y0, x1, y1, label, size, state, sub=sub, face=face)


def banner(img, cx, cy, text, size, w_ribbon, sub=None, sub_size=15, accent="#FFB45E", max_w=600.0, laurel=True,
           plate_alpha=1.0, alpha=1.0, color=None):
    """Triple Triad's laurel split to frame the words, joined by one continuous ribbon behind them (round 2,
    designer M4 and UX M6: no laurel across the lettering, no gap in the ribbon). Returns the text's half width."""
    p = gild(part("tt_laurel"), 0.45, 0.2)
    h, w = p.shape[:2]                              # 160 x 640 hr px: ribbon tails at the ends, branches by the plaque
    left, right = p[:, :262], p[:, 380:]
    tw = gwidth(text, "jupiter", size, 6)
    scale = (size * 1.9) / h
    gap = 8
    # the whole banner (laurels and tails) stays within max_w: the laurels scale down, never the words
    while tw + 2 * gap + (left.shape[1] + right.shape[1]) * scale > max_w and scale > 0.25:
        scale *= 0.95
    lw = left.shape[1] * scale
    # the ribbon: a band of the ribbon's own colour joining the two tails behind the text
    band_h = size * 1.0 + (sub_size * 1.9 if sub else 0)
    by0 = cy - size * 0.55
    sl, X, Y = img.win(cx, by0 + band_h / 2, tw / 2 + 60)
    bm = img.cov(np.maximum(np.abs(X - cx) - (tw / 2 + 26), np.abs(Y - (by0 + band_h / 2)) - band_h / 2))
    col = ramp(np.clip((Y - by0) / band_h, 0, 1), [(0, "#7A3A12"), (0.5, "#5A2A10"), (1, "#3A1A08")])
    img.mul(sl, hexc("#020308"), np.clip(1 - np.abs(Y - (by0 + band_h / 2) - 4) / (band_h * 0.8), 0, 1) *
            (np.abs(X - cx) < tw / 2 + 40) * 0.35 * plate_alpha)
    img.over(sl, col, bm * 0.94 * plate_alpha)
    for yy_ in (by0 + 2, by0 + band_h - 2):
        rule = lambda X_, Y_, yy_=yy_: np.maximum(np.abs(Y_ - yy_) - 1.1, np.abs(X_ - cx) - (tw / 2 + 26))
        from rich_lib import draw_brass
        draw_brass(img, rule, (cx, yy_, tw / 2 + 30), "round", depth=0.8, width=1.0, alpha=max(plate_alpha, 0.6))
    if laurel:
        blit(img, left, cx - tw / 2 - gap - lw, cy - h * scale * 0.52, lw, h * scale, alpha)
        blit(img, right, cx + tw / 2 + gap, cy - h * scale * 0.52, right.shape[1] * scale, h * scale, alpha)
    if color:
        gtext(img, cx, cy, text, "jupiter", size, color, anchor="mm", edge="#140A02", edge_w=1.4, glow=accent, glow_r=6,
              glow_k=0.35, tracking=2, alpha=alpha)
    else:
        gtext(img, cx, cy, text, "jupiter", size, C["gold_hi"], anchor="mm", gilt=True, edge="#140A02", edge_w=1.8,
              glow=accent, glow_r=8, glow_k=0.35, shadow=0.7, tracking=6, alpha=alpha)
    if sub:
        gtext(img, cx, cy + size * 0.42 + sub_size * 0.75, sub, "axis", sub_size, C["cream"], anchor="mm",
              edge="#140A02", edge_w=1.0, alpha=alpha)
    return tw / 2


def tooltip(img, x, y, lines, w):
    """FFXIV's tooltip: a dark plate with a thin gilt band."""
    h = 12 + 20 * len(lines)
    sl, X, Y = img.win(x + w / 2, y + h / 2, w)
    img.over(sl, hexc("#070A1C"), img.cov(sd_rrect(X, Y, x, y, x + w, y + h, 4)) * 0.94)
    gilt_frame(img, x, y, x + w, y + h, scale=0.22, corners=False)
    for i, ln in enumerate(lines):
        gtext(img, x + 12, y + 16 + 20 * i, ln, "axis", 14, C["cream"] if i == 0 else INK2, anchor="lm")


def check_floors(name):
    v = r2lib.DISPLAY["violations"]
    if v:
        print(f"  TEXT FLOOR ({name}):", *v, sep="\n    ")
    bad = list(v)
    v.clear()
    return bad


# ------------------------------------------------------------------------------------------------ title
def title(W=1280, H=800):
    S = 1.0
    small = W < 700
    img = Img(W, H, S, px=title_background(W, H, S, focus_x=0.30 if not small else 0.5))
    sl, X, Y = img.full()
    cid = ST.CARRIER
    code, name, _st, _b, lid = ST.CURRENT
    if not small:
        img.mul(sl, hexc("#04050E"), smooth(600, 140, X) * 0.55)
        moon_on(img, 200, 112, 40)
        logotype(img, 330, 236, 118, sub="a peg game under Menphina's moon", sub_size=34)
        items = [("Adventure", f"{ST.CAMPAIGN} · stage {ST.STAGE} of {ST.STAGES}"), ("Quick Play", "any level you have reached"),
                 ("Challenges", "12 of 40 won"), ("Duel", "against Minfilia, or a friend")]
        y = 350
        for (lab, sub) in items:
            K.button(img, 172, y, 488, y + 54, lab, 36, "normal", sub=sub)
            y += 66
        btn(img, 172, y + 6, 322, y + 44, "Companions", 26)
        btn(img, 338, y + 6, 488, y + 44, "Options", 26)
        # the Continue card: the default focus (round 2, UX M1 and m8, designer n3): the next level, its companion in
        # their colour, and the primary action
        x0, y0, x1, y1 = 852, 520, 1244, 772
        K.glow_rect(img, x0 - 10, y0 - 10, x1 + 10, y1 + 10, 10, cid["accent"], 0.45, 16)
        panel(img, x0, y0, x1, y1, jewel=cid["accent"], scale=0.36)
        framed_thumb(img, lid, x0 + 24, y0 + 30, 166)
        tx, tw = x0 + 212, x1 - 24 - (x0 + 212)
        gtext(img, tx, y0 + 34, "CONTINUE", "axis", 14, C["gold"], anchor="lm")
        gtext(img, tx, y0 + 62, code, "trump_s", 26, C["gold_hi"], anchor="lm", edge="#05070F", edge_w=0.6)
        gtext(img, tx + 38, y0 + 62, name, "jupiter", fit_text(name, "jupiter", 28, tw - 38), C["cream"], anchor="lm",
              edge="#05070F")
        gtext(img, tx, y0 + 88, "not yet won · ace 240,000", "axis", 14, INK2, anchor="lm")
        K.medallion(img, cid, tx + 18, y0 + 128, 17, glow=0.5)
        gtext(img, tx + 44, y0 + 120, "with Cid", "axis", 14, C["cream"], anchor="lm")
        gtext(img, tx + 44, y0 + 141, cid["power"], "jupiter", 23, hexc_to(cid["accent"], 0.2), anchor="lm", edge="#05070F")
        K.button(img, x0 + 22, y1 - 54, x1 - 24, y1 - 18, f"Continue {code}", 28, "focus")
        gtext(img, 40, 776, "1.23.0", "axis", 12, INK3, anchor="lm", edge="#05070F")
    else:
        img.mul(sl, hexc("#04050E"), np.exp(-((X - 320) / 220) ** 2) * smooth(110, 190, Y) * 0.55)
        moon_on(img, 86, 60, 22)
        logotype(img, 320, 86, 66, sub="a peg game under Menphina's moon", sub_size=20)
        y = 156
        K.glow_rect(img, 196, y - 2, 444, y + 42, 18, cid["accent"], 0.32, 8)
        K.button(img, 200, y, 440, y + 40, f"Continue {code}", 26, "focus")
        gtext(img, 320, y + 54, f"{name} · with Cid", "axis", 12.5, INK2, anchor="mm", edge="#05070F")
        y += 72
        for lab in ("Adventure", "Quick Play", "Challenges", "Duel"):
            K.button(img, 214, y, 426, y + 34, lab, 24, "normal")
            y += 42
        btn(img, 214, y + 2, 316, y + 32, "Companions", 19)
        btn(img, 324, y + 2, 426, y + 32, "Options", 19)
        gtext(img, 16, 466, "1.23.0", "axis", 14, INK3, anchor="lm", edge="#05070F")
    return img


# ------------------------------------------------------------------------------------------------ the Adventure map
BASE_STOPS = [(245, 712), (300, 760), (440, 750), (445, 645), (545, 610), (478, 520), (395, 470), (620, 560), (760, 640),
              (920, 700), (1060, 760)]


def lock_mark(img, x, y, s):
    """A small padlock in dim gilt: the stop is not reached yet (a sign with meaning, UX M2)."""
    from rich_lib import draw_brass
    sl, X, Y = img.win(x, y, s * 2)
    img.over(sl, hexc("#0A0E22"), img.cov(np.sqrt((X - x) ** 2 + (Y - y) ** 2) - s * 1.25))
    draw_brass(img, lambda X_, Y_: sd_rrect(X_, Y_, x - s * 0.62, y - s * 0.15, x + s * 0.62, y + s * 0.75, s * 0.15),
               (x, y + 0.3 * s, s), "round", depth=0.6, width=0.8, base=-0.1)
    draw_brass(img, lambda X_, Y_: np.maximum(np.abs(np.sqrt((X_ - x) ** 2 + (Y_ - y + s * 0.1) ** 2) - s * 0.42) - s * 0.12,
                                             Y_ - (y - s * 0.1)), (x, y - 0.4 * s, s), "round", depth=0.5, width=0.6, base=-0.1)


def pick_mark(img, x, y, r):
    """Stage 11, "your pick" (GD N7, UX m5): its own sign, a gilt four-point star on enamel, not the card back."""
    sl, X, Y = img.win(x, y, r + 2)
    d = np.sqrt((X - x) ** 2 + (Y - y) ** 2)
    img.over(sl, ramp(np.clip((Y - y + r) / (2 * r), 0, 1), [(0, "#24357A"), (1, "#0A1030")]), img.cov(d - r * 1.02))
    u, v = (X - x) / (r * 0.8), (Y - y) / (r * 0.8)
    st = np.abs(u) ** 0.5 + np.abs(v) ** 0.5
    img.over(sl, ramp(np.clip((Y - y + r) / (2 * r), 0, 1), [(0, "#FFF2C8"), (1, "#C8962E")]), np.clip((1.0 - st) * 8, 0, 1))


def stop_medallion(img, n, x, y, r, small=False):
    st = ST.stage_state(n)
    if n == 11:
        pick_mark(img, x, y, r)
        sl, X, Y = img.win(x, y, r)
        img.mul(sl, hexc("#060816"), img.cov(np.sqrt((X - x) ** 2 + (Y - y) ** 2) - r) * 0.45)
        ring(img, x, y, r, "lv_ring", k_gild=0.2, alpha=0.75)
    else:
        c = next(c for c in r2cast.CAST if c["stage"] == n and c["id"] != "Bolt")
        if c["id"] in ST.UNMET:
            disc_image(img, gild(r2cast.card_back(), 0.5)[70:184, 44:158], x, y, r * 1.04)
            ring(img, x, y, r, "lv_ring")
        elif st == "locked":
            # dimmed and drained, the ring in the locked grade (UX M2)
            f = r2cast.face(c)
            g = f[..., :3].mean(-1, keepdims=True)
            f = np.concatenate([(f[..., :3] * 0.3 + g * 0.7) * 0.42, f[..., 3:]], -1)
            disc_image(img, f, x, y, r * 1.04)
            ring(img, x, y, r, "lv_ring", k_gild=0.2, alpha=0.75)
            sl, X, Y = img.win(x, y, r + 8)
            d = np.sqrt((X - x) ** 2 + (Y - y) ** 2)
            img.mul(sl, hexc("#202430"), np.clip((r * 1.48 - d) * 2, 0, 1) * (d > r * 0.95) * 0.35)
        else:
            K.medallion(img, c, x, y, r, "lv_ring")
    if st == "locked" and n != 11:
        lock_mark(img, x + r * 0.95, y + r * 0.9, max(r * 0.36, 5.0))
    if st == "done":
        draw_moon(img, x + r * 1.0, y + r * 0.95, max(6.0, r * 0.32), "orange", "lit", variant=n, sky="#141C3A")


def adventure_map(W=1280, H=800):
    S = 1.0
    small = W < 700
    head = 62 if not small else 46
    ch = 1060.0 * (H - head) / W
    crop = (150.0, min(400.0, 872.0 - ch), 1060.0, ch)
    img = Img(W, H, S)
    place(img, chart_background(W, H - head, S, crop), 0, head, W, H - head)
    k = W / crop[2]
    pts = [((x - crop[0]) * k, head + (y - crop[1]) * k) for (x, y) in BASE_STOPS]
    from scene_airship_road import dashed
    from layout import smooth_path
    path = smooth_path(pts, 10)
    walked = smooth_path(pts[:ST.STAGE], 10)
    img.px = dashed(img.px, S, path, "#B08A4A", width=2.4 if not small else 1.8, dash=8, gap=6, alpha=0.65)
    img.px = dashed(img.px, S, walked, "#FFD98A", width=3.2 if not small else 2.4, dash=1000, gap=0, alpha=0.9)
    r_ = 22 if not small else 13
    cid = ST.CARRIER
    for i, (x, y) in enumerate(pts):
        n = i + 1
        if ST.stage_state(n) == "here":
            sl, X, Y = img.win(x, y, r_ * 4)
            d = np.sqrt((X - x) ** 2 + (Y - y) ** 2)
            img.add(sl, hexc(cid["accent"]), np.exp(-(np.clip(d - r_ * 1.5, 0, None) / (r_ * 0.55)) ** 2) * (d > r_) * 0.65)
        stop_medallion(img, n, x, y, r_, small)
        # the stage number on its own small plate, clear of the road and the ring
        ny = y + r_ * 1.62 + (3 if not small else 2)
        nsz = 18 if not small else 15
        sl, X, Y = img.win(x, ny, 16)
        img.over(sl, hexc("#070A1C"), img.cov(sd_rrect(X, Y, x - 11, ny - 8.5, x + 11, ny + 8.5, 6)) * 0.88)
        gtext(img, x, ny + 0.5, str(n), "trump_s", nsz, C["gold_hi"] if ST.stage_state(n) != "locked" else INK3,
              anchor="mm", edge="#05070F", edge_w=0.8)
    panel(img, -8, -8, W + 8, head - 4, corners=False, scale=0.36, shadow=True)
    if not small:
        btn(img, 22, 16, 122, 48, "Back", 24)
        K.tab(img, 430, 16, 630, 50, ST.CAMPAIGN, active=True, size=24)
        K.tab(img, 640, 16, 840, 50, "The Far Shore", size=24)
        gtext(img, W - 30, 33, f"{ST.STAGE} of {ST.STAGES} stages", "axis", 14, INK2, anchor="rm")
        # the face-down stop explains itself on focus
        tooltip(img, 14, 716, ["Stage 2 · Vesper Bay · won · Multiball",
                               "Companion not yet met in your story"], 300)
        x0, y0 = 900, 84
        panel(img, x0, y0, x0 + 348, y0 + 372, jewel=cid["accent"], scale=0.36)
        K.medallion(img, cid, x0 + 64, y0 + 76, 34, "lv_ring", glow=0.45)
        gtext(img, x0 + 122, y0 + 46, f"STAGE {ST.STAGE}", "axis", 14, C["gold"], anchor="lm")
        sname = ST.stage_name(ST.STAGE)
        gtext(img, x0 + 122, y0 + 74, sname, "jupiter", fit_text(sname, "jupiter", 32, 200), C["cream"], anchor="lm",
              edge="#05070F")
        gtext(img, x0 + 122, y0 + 100, f"Cid · {cid['power']}", "axis", 14.5, hexc_to(cid["accent"], 0.3), anchor="lm")
        K.crest_rule(img, x0 + 174, y0 + 140, 296, crest=False, scale=0.36)
        for i, (n, nm, st, sc, _t) in enumerate(ST.LEVELS):
            yy = y0 + 168 + i * 29
            col = C["cream"] if st != "locked" else INK3
            gtext(img, x0 + 26, yy, n, "trump_s", 19, C["gold_hi"] if st != "locked" else INK3, anchor="lm")
            gtext(img, x0 + 66, yy, nm, "jupiter", 24, col, anchor="lm", edge="#05070F")
            if st in ("won", "aced"):
                draw_moon(img, x0 + 322, yy, 6.5, "orange", "lit", variant=i, sky="#141C3A")
                gtext(img, x0 + 308, yy, sc, "axis", 13.5, INK2, anchor="rm")
            elif st == "open":
                gtext(img, x0 + 322, yy, "next", "axis", 13.5, hexc_to(cid["accent"], 0.3), anchor="rm")
        K.button(img, x0 + 74, y0 + 316, x0 + 274, y0 + 354, f"Play {ST.CURRENT[0]}", 28, "focus")
        # legend
        lx, ly = 900, 470
        sl, X, Y = img.win(lx + 174, ly + 40, 200)
        img.over(sl, hexc("#070A1C"), img.cov(sd_rrect(X, Y, lx, ly, lx + 348, ly + 78, 6)) * 0.85)
        gilt_frame(img, lx, ly, lx + 348, ly + 78, scale=0.22, corners=False)
        draw_moon(img, lx + 26, ly + 24, 7, "orange", "lit", variant=1, sky="#141C3A")
        gtext(img, lx + 44, ly + 24, "won", "axis", 14, INK2, anchor="lm")
        sl, X, Y = img.win(lx + 140, ly + 24, 20)
        d = np.sqrt((X - lx - 140) ** 2 + (Y - ly - 24) ** 2)
        img.add(sl, hexc(cid["accent"]), np.exp(-(np.clip(d - 7, 0, None) / 4) ** 2) * 0.7)
        gtext(img, lx + 158, ly + 24, "you are here", "axis", 14, INK2, anchor="lm")
        lock_mark(img, lx + 26, ly + 56, 7)
        gtext(img, lx + 44, ly + 56, "not reached", "axis", 14, INK2, anchor="lm")
        disc_image(img, gild(r2cast.card_back(), 0.5)[70:184, 44:158], lx + 140, ly + 56, 9)
        gtext(img, lx + 158, ly + 56, "not yet met", "axis", 14, INK2, anchor="lm")
        pick_mark(img, lx + 260, ly + 56, 9)
        gtext(img, lx + 276, ly + 56, "your pick", "axis", 14, INK2, anchor="lm")
    else:
        btn(img, 8, 9, 72, 37, "Back", 19)
        K.tab(img, 184, 9, 334, 40, ST.CAMPAIGN, active=True, size=20)
        K.tab(img, 340, 9, 490, 40, "The Far Shore", size=20)
        x0, y0 = 368, 58
        panel(img, x0, y0, 630, y0 + 104, jewel=cid["accent"], scale=0.3)
        K.medallion(img, cid, x0 + 32, y0 + 38, 18, "lv_ring", glow=0.4)
        gtext(img, x0 + 62, y0 + 22, f"STAGE {ST.STAGE}", "axis", 12.5, C["gold"], anchor="lm")
        gtext(img, x0 + 62, y0 + 44, ST.stage_name(ST.STAGE), "jupiter", 21, C["cream"], anchor="lm", edge="#05070F")
        K.button(img, x0 + 62, y0 + 62, 618, y0 + 92, f"Play {ST.CURRENT[0]}", 21, "focus")
    return img


# ------------------------------------------------------------------------------------------------ characters
def char_label(c, st):
    if st == "back":
        return "Not yet met"
    return c["name"]


def hero(img, c, x, y, w):
    a = r2cast.art(c)
    h = w * a.shape[0] / a.shape[1]
    sl, X, Y = img.win(x + w / 2, y + h / 2, max(w, h) / 2 + 90)
    d = np.sqrt(((X - x - w / 2) / (w * 0.75)) ** 2 + ((Y - y - h / 2) / (h * 0.7)) ** 2)
    img.add(sl, hexc(c["accent"]), np.exp(-(np.clip(d - 0.68, 0, None) / 0.22) ** 2) * (d > 0.66) * 0.30)
    shadow_under(img, x, y, x + w, y + h, r=2, off=(5, 8), soft=16, k=0.7)
    blit(img, a, x, y, w, h)
    gilt_frame(img, x, y, x + w, y + h, scale=0.42)
    return h


_SG = {}


def super_guide_thumb():
    """The power at work, for the detail panel (designer m5): Super Guide's line run on past the first bounce, cropped
    from a real board (base-p1) with the engine's own flight."""
    if "px" not in _SG:
        import play2
        px = play2.super_guide_board()
        _SG["px"] = px[100:560, 640:1200]
    return _SG["px"]


def characters(W=1280, H=800):
    small = W < 700
    S = 1.0
    img = Img(W, H, S, px=jewel_night(W, H, S, "title", seed=11))
    sel = r2cast.BY_ID["SuperGuide"]
    if not small:
        btn(img, 40, 30, 150, 66, "Back", 26)
        K.title(img, 176, 50, "Companions", 58)
        gtext(img, 178, 86, "Eleven of Eorzea's own, one for each stage, each carrying a power. In Quick Play, choose any "
              "you have met.", "axis", 15, INK2, anchor="lm", edge="#05070F", edge_w=1.0)
        cw, gx, gy, x0, y0 = 118, 26, 20, 46, 122
        chh = cw * 256 / 208
        for i, c in enumerate(r2cast.CAST):
            r, col = divmod(i, 4)
            x = x0 + col * (cw + gx) + (0 if r < 2 else (cw + gx) / 2)
            y = y0 + r * (chh + 58 + gy)
            st = ST.char_state(c)
            K.card(img, c, x, y, cw, st, selected=(c is sel))
            cx = x + cw / 2
            nm = char_label(c, st)
            nm_size = fit_text(nm, "jupiter", 25, cw + 22)
            gtext(img, cx, y + chh + 16, nm, "jupiter", nm_size, C["cream"] if st == "met" else INK2, anchor="mm",
                  edge="#05070F")
            sub = c["power"] if st != "locked" else (f"{c['power']} · stage {c['stage']}" if c["id"] != "Bolt"
                                                     else f"{c['power']} · The Far Shore")
            gtext(img, cx, y + chh + 36, sub, "axis", fit_text(sub, "axis", 13.5, cw + 24), hexc_to(c["accent"], 0.35)
                  if st != "locked" else INK2, anchor="mm", edge="#05070F")
        gtext(img, 46, 774, "Face down: someone your story has not introduced yet (the spoiler shield). Dimmed: met, "
              "not yet reached in Moonfall.", "axis", 13.5, INK2, anchor="lm", edge="#05070F")
        detail(img, sel, 676, 104, 1244, 764)
    else:
        btn(img, 10, 10, 78, 38, "Back", 19)
        K.title(img, 92, 25, "Companions", 34)
        cw, gx, gy, x0, y0 = 66, 12, 8, 12, 52
        chh = cw * 256 / 208
        for i, c in enumerate(r2cast.CAST):
            r, col = divmod(i, 4)
            x = x0 + col * (cw + gx) + (0 if r < 2 else (cw + gx) / 2)
            y = y0 + r * (chh + 32 + gy)
            st = ST.char_state(c)
            K.card(img, c, x, y, cw, st, selected=(c is sel))
            cx = x + cw / 2
            nm = "Not met" if st == "back" else (c.get("short") or c["name"].split()[0]) if len(c["name"]) > 9 else c["name"]
            gtext(img, cx, y + chh + 10, nm, "jupiter", 15, C["cream"] if st == "met" else INK2, anchor="mm", edge="#05070F")
            short_p = {"Moon-Viewing Draw": "Draw", "Lunar Burst": "Burst", "Brass Wings": "Wings"}.get(c["power"], c["power"])
            sub = c["power"] if st != "locked" else (f"{short_p} \u00b7 {c['stage']}" if c["id"] != "Bolt" else f"{short_p} \u00b7 FS")
            gtext(img, cx, y + chh + 24, sub, "axis", fit_text(sub, "axis", 12, cw + 10, 12),
                  hexc_to(c["accent"], 0.35) if st == "met" else INK2, anchor="mm", edge="#05070F")
        detail_small(img, sel, 330, 52, 628, 470)
    return img


def detail(img, c, x0, y0, x1, y1):
    panel(img, x0, y0, x1, y1, jewel=c["accent"])
    hw = 190
    hx, hy = x0 + 34, y0 + 36
    hh = hero(img, c, hx, hy, hw)
    tx = hx + hw + 30
    tw = x1 - 34 - tx
    K.title(img, tx, hy + 22, c["name"], fit_text(c["name"], "jupiter", 46, tw))
    y = hy + 56
    for line in gwrap(c["role"], "axis", 15, tw):
        gtext(img, tx, y, line, "axis", 15, INK2, anchor="lm")
        y += 20
    y += 12
    K.crest_rule(img, tx + tw / 2, y + 4, tw, crest=False, scale=0.42)
    y += 28
    for line in gwrap("“" + c["line"] + "”", "axis", 17.5, tw):
        gtext(img, tx, y, line, "axis", 17.5, C["cream"], anchor="lm")
        y += 24
    y += 8
    gtext(img, tx, y, "Joins at stage " + str(c["stage"]) + " · " + c["home"], "axis", 14, INK2, anchor="lm")
    gtext(img, tx, y + 20, "Spoilers: none past A Realm Reborn", "axis", 14, INK2, anchor="lm")
    py = hy + hh + 34
    K.crest_rule(img, (x0 + x1) / 2, py, x1 - x0 - 80, crest=True, scale=0.5)
    py += 48
    K.medallion(img, c, x0 + 72, py + 8, 25, "lv_ring", glow=0.45)
    gtext(img, x0 + 124, py - 2, c["power"], "jupiter", 44, hexc_to(c["accent"], 0.15), anchor="lm", edge="#05070F",
          edge_w=1.4, glow=c["accent"], glow_k=0.25)
    gtext(img, x0 + 126, py + 24, c["lasts"], "axis", 14.5, INK2, anchor="lm")
    py += 58
    # the power at work: a cut from a real board, framed, beside its description
    tw2 = 190
    th2 = tw2 * 460 / 560
    tx2 = x1 - 36 - tw2
    place(img, super_guide_thumb(), tx2, py - 8, tw2, th2)
    gilt_frame(img, tx2, py - 8, tx2 + tw2, py - 8 + th2, scale=0.26, corners=False)
    gtext(img, tx2 + tw2 / 2, py + th2 + 6, "in play: the guide runs on past the bounce", "axis", 12.5, INK2, anchor="mm")
    yy = py
    for line in gwrap(c["does"], "axis", 17, tx2 - 24 - (x0 + 40)):
        gtext(img, x0 + 40, yy, line, "axis", 17, C["cream"], anchor="lm")
        yy += 23
    yy += 14
    gtext(img, x0 + 40, yy, "Won together", "axis", 14.5, INK2, anchor="lm")
    for i in range(5):
        draw_moon(img, x0 + 52 + i * 22, yy + 24, 7.5, "orange", "lit", variant=i, sky="#16245A")
    K.button(img, x0 + 120, y1 - 64, x1 - 120, y1 - 22, "Play with " + r2cast.first_name(c), 30, "focus")


def detail_small(img, c, x0, y0, x1, y1):
    panel(img, x0, y0, x1, y1, jewel=c["accent"], scale=0.36)
    hw = 104
    hh = hero(img, c, x0 + 20, y0 + 20, hw)
    tx = x0 + 20 + hw + 16
    K.title(img, tx, y0 + 36, c["name"], fit_text(c["name"], "jupiter", 30, x1 - 16 - tx))
    y = y0 + 60
    for line in gwrap(c["role"], "axis", 12, x1 - 16 - tx):
        gtext(img, tx, y, line, "axis", 12, INK2, anchor="lm")
        y += 15
    y += 6
    for line in gwrap("“" + c["line"] + "”", "axis", 12, x1 - 16 - tx):
        gtext(img, tx, y, line, "axis", 12, C["cream"], anchor="lm")
        y += 15
    py = y0 + 20 + hh + 36
    K.medallion(img, c, x0 + 46, py, 16, "lv_ring", glow=0.35)
    gtext(img, x0 + 72, py, c["power"], "jupiter", 26, hexc_to(c["accent"], 0.15), anchor="lm", edge="#05070F")
    gtext(img, x1 - 18, py, c["lasts"], "axis", 12, INK2, anchor="rm")
    py += 32
    for line in gwrap(c["does"], "axis", 12.5, x1 - x0 - 40):
        gtext(img, x0 + 20, py, line, "axis", 12.5, C["cream"], anchor="lm")
        py += 17
    tw2 = 150
    th2 = tw2 * 460 / 560
    tx2 = (x0 + x1) / 2 - tw2 / 2
    place(img, super_guide_thumb(), tx2, py + 6, tw2, th2)
    gilt_frame(img, tx2, py + 6, tx2 + tw2, py + 6 + th2, scale=0.2, corners=False)
    K.button(img, x0 + 30, y1 - 44, x1 - 30, y1 - 14, "Play with " + r2cast.first_name(c), 21, "focus")


def lineup(W=1280, H=800):
    img = Img(W, H, 1.0, px=jewel_night(W, H, 1.0, "title", seed=11))
    K.title(img, 640, 46, "The Eleven Companions", 58, anchor="mm")
    gtext(img, 640, 84, "Real FFXIV characters, each on their own Triple Triad card from your install, each carrying one "
          "of Moonfall's eleven powers.", "axis", 15, INK2, anchor="mm", edge="#05070F")
    cw = 156
    chh = cw * 256 / 208
    for r, row in enumerate([r2cast.CAST[:6], r2cast.CAST[6:]]):
        gx = 40
        x0 = (W - (len(row) * cw + (len(row) - 1) * gx)) / 2
        y = 116 + r * (chh + 100)
        for i, c in enumerate(row):
            x = x0 + i * (cw + gx)
            sl, X, Y = img.win(x + cw / 2, y + chh / 2, chh)
            d = np.sqrt(((X - x - cw / 2) / cw) ** 2 + ((Y - y - chh / 2) / chh) ** 2)
            img.add(sl, hexc(c["accent"]), np.exp(-(np.clip(d - 0.5, 0, None) / 0.18) ** 2) * (d > 0.45) * 0.22)
            K.card(img, c, x, y, cw, "met")
            gtext(img, x + cw / 2, y + chh + 18, c["name"], "jupiter", fit_text(c["name"], "jupiter", 25, cw + 40),
                  C["cream"], anchor="mm", edge="#05070F")
            gtext(img, x + cw / 2, y + chh + 40, c["power"], "jupiter", 23, hexc_to(c["accent"], 0.42), anchor="mm",
                  edge="#05070F", glow=c["accent"], glow_k=0.2)
            where = f"stage {c['stage']}" if c["id"] != "Bolt" else "The Far Shore, stage 11"
            gtext(img, x + cw / 2, y + chh + 60, where, "axis", 13.5, INK2, anchor="mm")
    return img


# ------------------------------------------------------------------------------------------------ level select
def level_tile(img, x, y, w, entry, small=False):
    n, nm, st, sc, lid = entry
    th = w * 1106 / 1300
    cid = ST.CARRIER
    cap_h = 56 if not small else 44
    if st == "open":
        sel = gild(part("tt_cardsel"), 0.3).copy()
        sel[..., :3] = ramp(sel[..., :3].mean(-1), [(0, cid["accent"]), (1, hexc_to(cid["accent"], 0.45))])
        from PIL import Image as _I, ImageFilter as _F
        sel[..., 3] = np.asarray(_I.fromarray((sel[..., 3] * 255).astype(np.uint8)).filter(_F.GaussianBlur(4)), np.float32) / 255
        blit(img, sel, x - 22, y - 22, w + 44, th + cap_h + 44, mode="add", alpha=0.85)
    panel(img, x - 8, y - 8, x + w + 8, y + th + cap_h, corners=False, scale=0.3,
          jewel=cid["accent"] if st == "open" else None)
    if lid:
        framed_thumb(img, lid, x, y, w, scale=0.3)
    else:
        # sealed: the card back's tooled gilt, darkened and drained so it recedes (UX M3, designer M6)
        back = gild(r2cast.card_back(), 0.5)[30:-30, 20:-20].copy()
        g = back[..., :3].mean(-1, keepdims=True)
        back[..., :3] = (back[..., :3] * 0.25 + g * 0.75) * 0.38 + hexc("#101830") * 0.25
        blit(img, back, x, y, w, th)
        gilt_frame(img, x, y, x + w, y + th, scale=0.3, corners=False, alpha=0.6)
        lock_mark(img, x + w / 2, y + th / 2 - (8 if not small else 6), 9 if not small else 7)
        gtext(img, x + w / 2, y + th / 2 + (18 if not small else 12), "opens after " + ST.LEVELS[int(n[-1]) - 2][0],
              "axis", 14 if not small else 12, INK2, anchor="mm", edge="#05070F", edge_w=1.2)
    fs = 25 if not small else 15
    ty = y + th + (19 if not small else 14)
    gtext(img, x, ty, n, "trump_s", 19 if not small else 14, C["gold_hi"] if lid else INK3, anchor="lm")
    cx0 = x + (36 if not small else 24)
    avail = w - (36 if not small else 24) - 8
    size = fit_text(nm, "jupiter", fs, avail, 14)
    if gwidth(nm, "jupiter", size) > avail:
        words = nm.split()
        cut = len(words) // 2
        for i, ln in enumerate((" ".join(words[:cut]), " ".join(words[cut:]))):
            gtext(img, cx0, ty - 6 + 12 * i, ln, "jupiter", 14, C["cream"] if lid else INK2, anchor="lm", edge="#05070F")
    else:
        gtext(img, cx0, ty, nm, "jupiter", size, C["cream"] if lid else INK2, anchor="lm", edge="#05070F")
    yy = y + th + (42 if not small else 31)
    if st in ("won", "aced"):
        gtext(img, x, yy, "Best " + sc, "axis", 13.5 if not small else 14, INK2, anchor="lm")
        if st == "aced" and not small:
            gtext(img, x + w, yy, "ACED", "trump_s", 17, C["gold_hi"], anchor="rm", gilt=True)
        elif st == "aced":
            sl, X, Y = img.win(x + w - 20, y + 12, 30)
            img.over(sl, hexc("#2A1206"), img.cov(sd_rrect(X, Y, x + w - 44, y + 3, x + w - 3, y + 21, 4)) * 0.92)
            gtext(img, x + w - 23.5, y + 12.5, "ACED", "trump_s", 15, C["gold_hi"], anchor="mm", gilt=True)
        else:
            draw_moon(img, x + w - 8, yy, 6.5 if not small else 5.5, "orange", "lit", variant=2, sky="#141C3A")
    elif st == "open":
        gtext(img, x, yy, "Ace 240,000", "axis", 13.5 if not small else 14, hexc_to(cid["accent"], 0.4), anchor="lm")


def levels(W=1280, H=800):
    small = W < 700
    S = 1.0
    ch = 1060.0 * H / W
    img = Img(W, H, S, px=chart_background(W, H, S, (150.0, min(400.0, 872.0 - ch), 1060.0, ch)))
    sl, X, Y = img.full()
    img.mul(sl, hexc("#04050E"), np.full(X.shape, 0.5, np.float32))
    cid = ST.CARRIER
    code, name, _st, _b, lid = ST.CURRENT
    sname = ST.stage_name(ST.STAGE)
    if not small:
        btn(img, 22, 20, 122, 52, "Map", 24)
        K.medallion(img, cid, 200, 78, 40, "lv_ring", glow=0.5)
        gtext(img, 270, 52, f"{ST.CAMPAIGN.upper()} · STAGE {ST.STAGE}", "axis", 14, C["gold"], anchor="lm", edge="#05070F")
        K.title(img, 268, 88, sname, 52)
        gtext(img, 270, 122, "Cid's airship carries the road over Eorzea by night. Cid carries Brass Wings through these five.",
              "axis", 15, INK2, anchor="lm", edge="#05070F")
        w, gx, x0, y0 = 214, 28, 46, 186
        for i, e in enumerate(ST.LEVELS):
            level_tile(img, x0 + i * (w + gx), y0, w, e)
        panel(img, 46, 520, 1234, 770, jewel=cid["accent"], scale=0.4)
        framed_thumb(img, lid, 76, 552, 230)
        K.title(img, 336, 574, name, 46)
        gtext(img, 336, 612, "A trail on a map: the airship's road from Limsa Lominsa to Ala Mhigo, a ring of moons on every stop.",
              "axis", 15, INK2, anchor="lm")
        gtext(img, 336, 636, "Clear the stops' oranges to finish the journey.", "axis", 15, INK2, anchor="lm")
        gtext(img, 336, 672, "ACE SCORE", "axis", 12.5, C["gold"], anchor="lm")
        gtext(img, 336, 698, "240,000", "trump", 30, C["gold_hi"], anchor="lm", gilt=True, edge="#120A02")
        K.medallion(img, cid, 530, 690, 18, "lv_ring", glow=0.35)
        gtext(img, 562, 682, "with Cid", "axis", 15, C["cream"], anchor="lm")
        gtext(img, 562, 704, cid["power"], "jupiter", 23, hexc_to(cid["accent"], 0.25), anchor="lm", edge="#05070F")
        K.button(img, 980, 676, 1200, 728, f"Play {code}", 36, "focus")
    else:
        btn(img, 8, 10, 72, 38, "Map", 19)
        K.medallion(img, cid, 104, 30, 18, "lv_ring", glow=0.4)
        gtext(img, 132, 18, f"STAGE {ST.STAGE}", "axis", 12, C["gold"], anchor="lm", edge="#05070F")
        K.title(img, 130, 40, sname, 30)
        # one row of five, then the selection strip with Play (UX M4)
        w, gx, x0 = 108, 14, 12
        for i, e in enumerate(ST.LEVELS):
            level_tile(img, x0 + i * (w + gx), 82, w, e, small=True)
        sy = 300
        panel(img, 10, sy, 630, 470, jewel=cid["accent"], scale=0.3)
        framed_thumb(img, lid, 26, sy + 18, 150)
        gtext(img, 196, sy + 22, code, "trump_s", 17, C["gold_hi"], anchor="lm")
        gtext(img, 228, sy + 22, name, "jupiter", 24, C["cream"], anchor="lm", edge="#05070F")
        for i, line in enumerate(gwrap("A trail on a map: a ring of moons on every stop.", "axis", 12.5, 410)):
            gtext(img, 196, sy + 48 + 17 * i, line, "axis", 12.5, INK2, anchor="lm")
        gtext(img, 196, sy + 88, "ACE 240,000", "trump_s", 15, C["gold_hi"], anchor="lm", gilt=True)
        K.medallion(img, cid, 210, sy + 128, 13, "lv_ring", glow=0.35)
        gtext(img, 230, sy + 128, f"with Cid · {cid['power']}", "axis", 12.5, C["cream"], anchor="lm")
        K.button(img, 470, sy + 110, 616, sy + 148, f"Play {code}", 26, "focus")
    return img


def screens():
    import play2
    return {"title": title, "map": adventure_map, "characters": characters, "levels": levels, "hud": play2.hud,
            "power": play2.power, "fever": play2.fever, "tally": play2.tally, "pause": play2.pause}


if __name__ == "__main__":
    SCREENS = screens()
    names = sys.argv[1:] or list(SCREENS) + ["lineup", "pegmarks"]
    floor_fail = []
    for n in names:
        if n == "lineup":
            save_rgb(grain(lineup().px, 0.005, seed=3), r2lib.OUT_CHARS / "lineup.png")
            print("ok lineup")
            continue
        if n == "pegmarks":
            import play2
            save_rgb(play2.pegmarks().px, OUT_SCREENS / "pegmarks.png")
            print("ok pegmarks")
            continue
        for (W, H) in ((1280, 800), (640, 480)):
            r2lib.DISPLAY["scale"] = 1.0 if W < 700 else None
            img = SCREENS[n](W, H)
            r2lib.DISPLAY["scale"] = None
            floor_fail += check_floors(f"{n}-{W}")
            save_rgb(grain(img.px, 0.005, seed=3), OUT_SCREENS / f"{n}-{W}.png")
        print("ok", n)
    r2lib.write_sources()
    if floor_fail:
        raise SystemExit(f"{len(floor_fail)} texts below the floor")
