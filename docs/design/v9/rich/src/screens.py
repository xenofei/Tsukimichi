"""Moonfall's screens, rich pass (owner brief, 5 October 2026): every screen at 1280 x 800 and at the 640 x 480 minimum.

  py -3 screens.py [title|map|characters|levels|hud|fever|tally|pause ...]

Writes screens/<name>-1280.png and screens/<name>-640.png. Every screen is in the Medallion identity: lapis enamel over
guilloche, brass with pearl beading, moonstone, cream and gilt type, the one light from the upper left.
"""
import math
import sys

import numpy as np

from rich_lib import (OUT_SCENES, OUT_SCREENS, P, Img, blur, crop_to, draw_moon, grain, hexc, load_official, load_rgb,
                      moon_glow, night_map, ramp, save_rgb, screen, smooth, text, text_size, vignette, wrap)
from ui_kit import (button, cameo_badge, dim, gilt_rule, logotype, panel, place_rgb, place_rgba, tab, vignette_rect)
import ui_kit  # noqa: F401  (registers the display fonts)
import sys as _sys
import pathlib as _pl

_sys.path.insert(0, str(_pl.Path(__file__).resolve().parents[2] / "src"))
_sys.path.insert(0, str(_pl.Path(__file__).resolve().parents[3] / "v8" / "art" / "src"))
from paint_moon import moon as paint_moon  # noqa: E402

_CAMEOS = {}


def cameo(key, S=3.0):
    if (key, S) not in _CAMEOS:
        from cameo import render
        from cameo_cast import CAST
        fn = {c[0]: c[4] for c in CAST}[key]
        _CAMEOS[(key, S)] = render(fn, S=S)
    return _CAMEOS[(key, S)]


def moon_on(img, mx, my, mr):
    class _C:
        pass
    c = _C()
    c.px, c.w, c.h = img.px, img.w, img.h
    c.yy, c.xx = np.mgrid[0:img.h, 0:img.w].astype(np.float32)
    paint_moon(c, {}, mx * img.S, my * img.S, mr * img.S)
    img.px = c.px


# ------------------------------------------------------------------------------------------------ backgrounds
def title_background(W, H, S, focus_x=0.62):
    """Sohm Al's peak under the moon: the official Dravania painting (ui/loadingimage/-nowloading_base07.tex), its
    light from the upper left, night graded; the moon painted where its light comes from."""
    src = load_official("ui_loadingimage_-nowloading_base07.png")
    aspect = W / H
    ch = 1010.0
    cw = min(1920.0, ch * aspect)
    if cw >= 1920:
        cw = 1920.0
        ch = cw / aspect
    peak_x = 960.0
    x0 = np.clip(peak_x - cw * focus_x, 0, 1920 - cw)
    px = crop_to(src, (x0, 40.0, cw, ch), (int(W * S), int(H * S)))
    yy = np.mgrid[0:px.shape[0], 0:px.shape[1]][0] / px.shape[0]
    sky = blur(smooth(0.0, 0.10, px[..., 2] - px[..., 0]) * smooth(0.7, 0.3, yy), 3 * S)
    out = night_map(px, curve=1.7, chroma=0.24, ceiling=0.62, sky=sky, sky_drop=0.25, local=0.5)
    return out


def chart_background(W, H, S, crop):
    """The world map (ui/map/world/01/world01_m.tex, 2048 x 872 inside its texture) as a moonlit chart."""
    src = load_official("ui_map_world_01_world01_m.png")[587:587 + 872]
    px = crop_to(src, crop, (int(W * S), int(H * S)))
    out = night_map(px, curve=1.25, chroma=0.16, ceiling=0.46, local=0.8, warm_keep=0.25)
    return moon_glow(out, -60 * S, -60 * S, 600 * S, 1400 * S, 0.10, 0.05)


# ------------------------------------------------------------------------------------------------ title
def title(W=1280, H=800):
    S = 1.0
    img = Img(W * S, H * S, S, px=title_background(W, H, S, focus_x=0.30 if W > 700 else 0.5))
    small = W < 700
    if not small:
        moon_on(img, 210, 120, 38)
        vignette_rect(img, 0.35)
        # the left column: a dark glass of night behind the menu, so the type sits on calm ground
        sl, X, Y = img.full()
        img.mul(sl, hexc("#05070F"), smooth(560, 120, X) * 0.55)
        logotype(img, 330, 250, 64, sub="a peg game under Menphina's moon", sub_size=19)
        items = [("Adventure", "The Moon Road · stage 4 of 11", "focus"), ("Quick Play", "any level you have reached", "normal"),
                 ("Challenges", "12 of 40 won", "normal"), ("Duel", "against Ione, or a friend", "normal")]
        y = 350
        for (lab, sub, st) in items:
            button(img, 170, y, 490, y + 52, lab, 19, st, sub=sub)
            y += 64
        button(img, 170, y + 8, 322, y + 46, "Characters", 15)
        button(img, 338, y + 8, 490, y + 46, "Options", 15)
        # continue card: the next level, its scene and its stage's character
        panel(img, 900, 600, 1240, 760, r=10)
        thumb = load_rgb(OUT_SCENES / "base-p3-moogle.png")
        place_rgb(img, thumb[41:594, 75:725], 916, 616, 150, 128)
        text(img, 1082, 630, "Continue", "ui_sb", 11, P["gilt_high"], anchor="lm", halo=0)
        text(img, 1082, 656, "1-3  The Moonlit Post", "serif", 15, P["cream"], anchor="lm", halo=0)
        text(img, 1082, 680, "Best 214,300 · not yet aced", "ui", 11.5, P["ink_dim"], anchor="lm", halo=0)
        cameo_badge(img, cameo("pipiru"), 1110 + 82, 718, 22, 26, beads=False)
        text(img, 1082, 718, "with Pipiru", "ui", 11.5, P["ink_dim"], anchor="lm", halo=0)
        text(img, 40, 772, "1.23.0", "ui", 11, P["ink_dim"], anchor="lm", halo=0.3)
    else:
        moon_on(img, 92, 70, 22)
        vignette_rect(img, 0.35)
        sl, X, Y = img.full()
        img.mul(sl, hexc("#05070F"), np.exp(-((X - 320) / 210) ** 2) * smooth(130, 200, Y) * 0.50)
        logotype(img, 320, 112, 40, sub="a peg game under Menphina's moon", sub_size=13)
        y = 196
        for (lab, st) in (("Adventure", "focus"), ("Quick Play", "normal"), ("Challenges", "normal"), ("Duel", "normal")):
            button(img, 200, y, 440, y + 38, lab, 15, st)
            y += 46
        button(img, 200, y + 4, 314, y + 34, "Characters", 12)
        button(img, 326, y + 4, 440, y + 34, "Options", 12)
        text(img, 20, 464, "1.23.0", "ui", 10, P["ink_dim"], anchor="lm", halo=0.3)
    return img


# ------------------------------------------------------------------------------------------------ the Adventure map
# The Moon Road's eleven stages as stops on the world map (source px in the world01 crop below), each with its
# character; the road runs Limsa Lominsa → Ul'dah → Mor Dhona → Gridania → Ishgard → Ala Mhigo, then on across the
# Ruby Sea and the Far East. The Far Shore's twelve stages continue past the edge of the known map (Sharlayan, the sea,
# the moon), on the expansion tab.
BASE_STOPS = [  # (source x, y in the 2048 x 872 map, stage label, character key, state)
    (245, 712, "1", "pipiru", "done"), (300, 760, "2", "kaede", "done"), (440, 750, "3", "marcia", "done"),
    (445, 645, "4", "haldbrand", "here"), (520, 600, "5", "gajavati", "open"), (500, 530, "6", "ysolde", "locked"),
    (395, 485, "7", "ottilie", "locked"), (620, 575, "8", "gyobo", "locked"), (760, 640, "9", "aldous", "locked"),
    (920, 700, "10", "ione", "locked"), (1060, 760, "11", "kupsa", "locked")]
STAGE_NAMES = {"1": "Harbour Lights", "2": "The Sunlit Steps", "3": "Mor Dhona's Glass", "4": "The Shroud by Night",
               "5": "Ala Mhigo's Gate", "6": "Ishgard's Bells", "7": "The Northern Wastes", "8": "The Ruby Sea",
               "9": "Doma's Lanterns", "10": "The Azim Steppe", "11": "The Moon Road's End"}


def adventure_map(W=1280, H=800):
    S = 1.0
    small = W < 700
    head = 56 if not small else 40
    # the chart fills the screen below the header; the world map crop is chosen to hold the road
    crop = (120.0, 380.0, 1060.0, 1060.0 * (H - head) / W)
    img = Img(W * S, H * S, S)
    chart = chart_background(W, H - head, S, crop)
    place_rgb(img, chart, 0, head, W, H - head)
    vignette_rect(img, 0.30)
    k = W / crop[2]

    def to_screen(sx, sy):
        return ((sx - crop[0]) * k, head + (sy - crop[1]) * k)

    pts = [to_screen(x, y) for (x, y, *_r) in BASE_STOPS]
    # the road: a gilt dashed line (walked part solid and brighter)
    from scene_airship_road import dashed
    from layout import smooth_path
    path = smooth_path(pts, 10)
    here = [i for i, s in enumerate(BASE_STOPS) if s[4] == "here"][0]
    walked = smooth_path(pts[:here + 1], 10)
    img.px = dashed(img.px, S, path, "#9A7E4A", width=2.2 if not small else 1.6, dash=7, gap=6, alpha=0.6)
    img.px = dashed(img.px, S, walked, "#E6CF98", width=2.8 if not small else 2.0, dash=1000, gap=0, alpha=0.75)
    # the stops
    r_ = 24 if not small else 13
    for (x, y), (_, _, lab, key, st) in zip(pts, BASE_STOPS):
        cameo_badge(img, cameo(key), x, y, r_, r_ * 1.16, beads=not small, dim=(st == "locked"))
        if st == "done" and not small:
            draw_moon(img, x + r_ * 0.78, y + r_ * 0.95, 6.5, "orange", "lit", variant=1, sky="#141C3A")
        if st == "here":
            sl, X, Y = img.win(x, y, r_ * 3)
            d = np.sqrt(((X - x) / r_) ** 2 + ((Y - y) / (r_ * 1.16)) ** 2)
            img.add(sl, hexc("#C3CEE4"), np.exp(-np.clip(d - 1.15, 0, None) * 3.0) * (d > 1.12) * 0.45)
        num = f"{lab}"
        text(img, x, y + r_ * 1.16 + (11 if not small else 7), num, "ui_sb", 12 if not small else 9,
             P["cream"] if st != "locked" else P["ink_dim"], anchor="mm", halo=0.8)
    # the header: campaign tabs, the stage panel, back
    panel(img, -10, -10, W + 10, head, r=0, border=True, inner_rule=False)
    if not small:
        button(img, 20, 12, 120, 44, "Back", 14)
        tab(img, 420, 14, 640, head, "The Moon Road", active=True)
        tab(img, 650, 14, 870, head, "The Far Shore")
        text(img, 1250, 30, "4 of 11 stages", "ui", 12, P["ink_dim"], anchor="rm", halo=0)
        # the selected stage: a panel on the right
        x0, y0 = 930, 470
        panel(img, x0, y0, x0 + 320, y0 + 300, r=10)
        cameo_badge(img, cameo("haldbrand"), x0 + 62, y0 + 72, 40, 46)
        text(img, x0 + 116, y0 + 36, "Stage 4", "ui_sb", 11, P["gilt_high"], anchor="lm", halo=0)
        text(img, x0 + 116, y0 + 60, STAGE_NAMES["4"], "serif", 18, P["cream"], anchor="lm", halo=0)
        text(img, x0 + 116, y0 + 86, "Haldbrand · Lunar Burst", "ui", 12, P["ink_dim"], anchor="lm", halo=0)
        gilt_rule(img, x0 + 20, x0 + 300, y0 + 132, w=1.0)
        lv = [("4-1", "The Moonlit Post", "won", "214,300"), ("4-2", "Spriggan Hollow", "won", "188,050"),
              ("4-3", "The Lamplighters", "open", ""), ("4-4", "Bentbranch at Dusk", "locked", ""),
              ("4-5", "The Twelveswood", "locked", "")]
        for i, (n, nm, st, sc) in enumerate(lv):
            yy = y0 + 156 + i * 26
            col = P["cream"] if st != "locked" else "#6E7698"
            text(img, x0 + 26, yy, n, "ui_sb", 11, P["gilt_high"] if st != "locked" else "#6E7698", anchor="lm", halo=0)
            text(img, x0 + 64, yy, nm, "serif", 13.5, col, anchor="lm", halo=0)
            if st == "won":
                draw_moon(img, x0 + 296, yy, 6, "orange", "lit", variant=i, sky="#141C3A")
                text(img, x0 + 284, yy, sc, "ui", 11, P["ink_dim"], anchor="rm", halo=0)
        button(img, x0 + 70, y0 + 250, x0 + 250, y0 + 286, "Play 4-3", 15, "focus")
    else:
        button(img, 10, 8, 74, 32, "Back", 11)
        tab(img, 196, 8, 344, head, "The Moon Road", active=True)
        tab(img, 350, 8, 498, head, "The Far Shore")
        panel(img, 380, 384, 630, 470, r=8)
        cameo_badge(img, cameo("haldbrand"), 412, 427, 22, 26, beads=False)
        text(img, 444, 408, "Stage 4", "ui_sb", 9, P["gilt_high"], anchor="lm", halo=0)
        text(img, 444, 426, STAGE_NAMES["4"], "serif", 12.5, P["cream"], anchor="lm", halo=0)
        button(img, 444, 438, 620, 462, "Play 4-3", 11, "focus")
    return img


SCREENS = {"title": title, "map": adventure_map}


if __name__ == "__main__":
    names = sys.argv[1:] or list(SCREENS)
    for n in names:
        for (W, H) in ((1280, 800), (640, 480)):
            img = SCREENS[n](W, H)
            save_rgb(grain(img.px, 0.006, seed=3), OUT_SCREENS / f"{n}-{W}.png")
        print("ok", n)
