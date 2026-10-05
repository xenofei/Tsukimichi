"""Moonfall's screens, rich pass (owner brief, 5 October 2026): every screen at 1280 x 800 and at the 640 x 480 minimum.

  py -3 screens.py [title|map|characters|levels|hud|fever|tally|pause ...]

Writes screens/<name>-1280.png and screens/<name>-640.png. Every screen is in the Medallion identity: lapis enamel over
guilloche, brass with pearl beading, moonstone, cream and gilt type, the one light from the upper left.
"""
import math
import sys

import numpy as np

from rich_lib import (OUT_COMP, OUT_SCENES, OUT_SCREENS, P, Img, blur, crop_to, draw_moon, grain, hexc, load_official,
                      load_rgb, moon_glow, night_lab, ramp, save_rgb, screen, smooth, text, text_size, vignette, wrap)

# Stage 4 of The Moon Road, as the map, level select and HUD mocks show it (the pilots stand in for its levels)
STAGE4 = [("4-1", "The Moonlit Post", "won", "214,300", "base-p3"), ("4-2", "The Holy See", "aced", "251,880", "base-p2"),
          ("4-3", "The Airship Road", "open", "", "base-p1"), ("4-4", "Bentbranch at Dusk", "locked", "", None),
          ("4-5", "The Twelveswood", "locked", "", None)]
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
    out = night_lab(px, ceiling=0.66, knee=0.40, exposure=0.95, gamma=1.6, sky=sky, sky_drop=0.25, S=S)
    return out


def chart_background(W, H, S, crop):
    """The world map (ui/map/world/01/world01_m.tex, 2048 x 872 inside its texture) as a moonlit chart."""
    src = load_official("ui_map_world_01_world01_m.png")[587:587 + 872]
    px = crop_to(src, crop, (int(W * S), int(H * S)))
    out = night_lab(px, gamma=1.35, exposure=0.85, ceiling=0.54, warm_keep=0.25, chroma_mid=0.45, S=S)
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
    ch = 1060.0 * (H - head) / W
    crop = (150.0, min(400.0, 872.0 - ch), 1060.0, ch)
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
        x0, y0 = 930, 76
        panel(img, x0, y0, x0 + 320, y0 + 316, r=10)
        cameo_badge(img, cameo("haldbrand"), x0 + 62, y0 + 72, 40, 46)
        text(img, x0 + 116, y0 + 36, "Stage 4", "ui_sb", 11, P["gilt_high"], anchor="lm", halo=0)
        text(img, x0 + 116, y0 + 60, STAGE_NAMES["4"], "serif", 18, P["cream"], anchor="lm", halo=0)
        text(img, x0 + 116, y0 + 86, "Haldbrand · Lunar Burst", "ui", 12, P["ink_dim"], anchor="lm", halo=0)
        gilt_rule(img, x0 + 20, x0 + 300, y0 + 132, w=1.0)
        lv = [(n, nm, st, sc) for (n, nm, st, sc, _t) in STAGE4]
        for i, (n, nm, st, sc) in enumerate(lv):
            yy = y0 + 156 + i * 26
            col = P["cream"] if st != "locked" else "#6E7698"
            text(img, x0 + 26, yy, n, "ui_sb", 11, P["gilt_high"] if st != "locked" else "#6E7698", anchor="lm", halo=0)
            text(img, x0 + 64, yy, nm, "serif", 13.5, col, anchor="lm", halo=0)
            if st in ("won", "aced"):
                draw_moon(img, x0 + 296, yy, 6, "orange", "lit", variant=i, sky="#141C3A")
                text(img, x0 + 284, yy, sc, "ui", 11, P["ink_dim"], anchor="rm", halo=0)
        button(img, x0 + 70, y0 + 272, x0 + 250, y0 + 304, "Play 4-3", 15, "focus")
    else:
        button(img, 10, 8, 74, 32, "Back", 11)
        tab(img, 196, 8, 344, head, "The Moon Road", active=True)
        tab(img, 350, 8, 498, head, "The Far Shore")
        panel(img, 380, 50, 630, 136, r=8)
        cameo_badge(img, cameo("haldbrand"), 412, 93, 22, 26, beads=False)
        text(img, 444, 74, "Stage 4", "ui_sb", 9, P["gilt_high"], anchor="lm", halo=0)
        text(img, 444, 92, STAGE_NAMES["4"], "serif", 12.5, P["cream"], anchor="lm", halo=0)
        button(img, 444, 104, 620, 128, "Play 4-3", 11, "focus")
    return img


# ------------------------------------------------------------------------------------------------ characters
WHO = {  # key: (personality, what the power does, how long)
    "pipiru": ("Counts every star twice and tells you the path before you ask.",
               "Her star globe draws the guide on, through the first bounce to the next peg.", "3 shots"),
    "kaede": ("Calm on the outside, competitive underneath; never misses a beat.",
              "Her second chakram leaves her hand: a twin ball springs from the green peg.", "this shot"),
    "marcia": ("Left the legions to mend things instead; fussy about rivets.",
               "Brass vanes unfold on the bucket as two wings, widening it.", "5 turns"),
    "haldbrand": ("A gentle giant who raises his voice only to fire.",
                  "A lunar cartridge bursts at the green peg and lights every peg within its ring.", "this shot"),
    "gajavati": ("Thavnair's cheeriest trader; swats bad luck away with festival fans.",
                 "Two fans fold out at the foot's corners; click and they flick the ball back up.", "3 turns"),
    "ysolde": ("Dry, patient, and opens doors nobody else can see.",
               "A ring of moonlight opens under the board; the ball drops back in from the top.", "this shot"),
    "ottilie": ("Kind, unhurried, and impossible to argue with.",
                "Moonflower petals drift from the green peg to the nearest oranges and light them.", "this shot"),
    "gyobo": ("Flustered, earnest, and sure the drum is never rigged.",
              "The shrine's drum turns once and drops a free ball, a triple score or another power.", "next shot"),
    "aldous": ("Studied the red moon's fall for thirty years; very polite, very loud spells.",
               "The ball wears a red-moon ember and burns straight through the pegs it meets.", "next shot"),
    "ione": ("Has already worked out your shot, and is sorry about it.",
             "Four nouliths ride beside the ball and nudge it onto the best path.", "next shot"),
    "kupsa": ("Delivers the bolt, kupo, and signs for nothing.",
              "A bolt jumps from the first peg hit to the bucket, lighting the pegs along it.", "this shot"),
}


def cast():
    from cameo_cast import CAST
    return CAST


def characters(W=1280, H=800):
    small = W < 700
    S = 1.0
    img = Img(W, H, S, px=title_background(W, H, S, focus_x=0.5))
    dim(img, 0.62)
    C = cast()
    sel = 0
    unlocked = 5                                                    # stages won so far unlock their characters
    if not small:
        text(img, 40, 40, "Companions", "serif", 26, P["cream"], anchor="lm", halo=0.5)
        text(img, 40, 68, "Each stage of the Moon Road brings one. In Quick Play and the master levels, choose any you have met.",
             "ui", 12.5, P["ink_dim"], anchor="lm", halo=0.5)
        button(img, 1140, 24, 1250, 56, "Back", 14)
        cw, chh, gx, gy, x0, y0 = 190, 214, 16, 16, 40, 100
        for i, (key, name, who, power, _fn) in enumerate(C):
            r, c = divmod(i, 4)
            x = x0 + c * (cw + gx) + (0 if r < 2 else (cw + gx) / 2)
            y = y0 + r * (chh + gy)
            locked = i >= unlocked
            if i == sel:
                sl, X, Y = img.win(x + cw / 2, y + chh / 2, cw)
                from rich_lib import sd_rrect
                sd = sd_rrect(X, Y, x, y, x + cw, y + chh, 10)
                img.add(sl, hexc("#9DB4EA"), np.exp(-np.clip(sd, 0, None) / 7.0) * (sd > 0) * 0.35)
            panel(img, x, y, x + cw, y + chh, r=10, tint0="#24376F" if i == sel else "#1C2A5C",
                  tint1="#121A3C", inner_rule=False)
            cameo_badge(img, cameo(key), x + cw / 2, y + 80, 52, 60, beads=True, dim=locked)
            text(img, x + cw / 2, y + 162, name, "serif", 15, P["cream"] if not locked else "#7E86A4", anchor="mm", halo=0)
            text(img, x + cw / 2, y + 184, power if not locked else f"meet at stage {i + 1}", "ui_sb", 11.5,
                 P["gilt_high"] if not locked else "#6E7698", anchor="mm", halo=0)
        # the detail panel
        key, name, who, power, _fn = C[sel]
        pers, does, lasts = WHO[key]
        px0, py0, px1, py1 = 860, 100, 1250, 764
        panel(img, px0, py0, px1, py1, r=12, bead=True)
        cameo_badge(img, cameo(key), (px0 + px1) / 2, py0 + 150, 110, 128)
        text(img, (px0 + px1) / 2, py0 + 316, name, "serif", 26, P["cream"], anchor="mm", halo=0)
        text(img, (px0 + px1) / 2, py0 + 344, who, "ui", 12.5, P["ink_dim"], anchor="mm", halo=0)
        gilt_rule(img, px0 + 40, px1 - 40, py0 + 368, w=1.0)
        y = py0 + 396
        for line in wrap(pers, "serif_i", 15, px1 - px0 - 60):
            text(img, (px0 + px1) / 2, y, line, "serif_i", 15, P["cream"], anchor="mm", halo=0)
            y += 22
        y += 14
        text(img, px0 + 30, y, power, "ui_sb", 15, P["gilt_high"], anchor="lm", halo=0)
        text(img, px1 - 30, y, lasts, "ui", 12, P["ink_dim"], anchor="rm", halo=0)
        y += 26
        for line in wrap(does, "ui", 13, px1 - px0 - 60):
            text(img, px0 + 30, y, line, "ui", 13, P["cream"], anchor="lm", halo=0)
            y += 20
        button(img, px0 + 70, py1 - 70, px1 - 70, py1 - 28, "Play with " + name.split()[0], 16, "focus")
    else:
        text(img, 14, 22, "Companions", "serif", 17, P["cream"], anchor="lm", halo=0.5)
        button(img, 560, 8, 630, 34, "Back", 11)
        cw, chh, gx, gy, x0, y0 = 108, 128, 6, 6, 10, 44
        for i, (key, name, who, power, _fn) in enumerate(C):
            r, c = divmod(i, 4)
            x = x0 + c * (cw + gx) + (0 if r < 2 else (cw + gx) / 2)
            y = y0 + r * (chh + gy)
            locked = i >= unlocked
            panel(img, x, y, x + cw, y + chh, r=7, tint0="#24376F" if i == sel else "#1C2A5C", tint1="#121A3C",
                  inner_rule=False)
            cameo_badge(img, cameo(key), x + cw / 2, y + 50, 32, 37, beads=False, dim=locked)
            text(img, x + cw / 2, y + 100, name.split()[0] if name != "Sister Ottilie" else "Ottilie", "serif", 12,
                 P["cream"] if not locked else "#7E86A4", anchor="mm", halo=0)
            text(img, x + cw / 2, y + 116, power if not locked else "not yet met", "ui_sb", 9.5,
                 P["gilt_high"] if not locked else "#6E7698", anchor="mm", halo=0)
        key, name, who, power, _fn = C[sel]
        pers, does, lasts = WHO[key]
        px0, py0, px1, py1 = 470, 44, 630, 470
        panel(img, px0, py0, px1, py1, r=9)
        cameo_badge(img, cameo(key), (px0 + px1) / 2, py0 + 74, 52, 60, beads=False)
        text(img, (px0 + px1) / 2, py0 + 152, name, "serif", 15, P["cream"], anchor="mm", halo=0)
        y = py0 + 176
        text(img, px0 + 12, y, power, "ui_sb", 11, P["gilt_high"], anchor="lm", halo=0)
        y += 18
        for line in wrap(does, "ui", 10, px1 - px0 - 24):
            text(img, px0 + 12, y, line, "ui", 10, P["cream"], anchor="lm", halo=0)
            y += 15
        text(img, px0 + 12, y + 6, lasts, "ui", 10, P["ink_dim"], anchor="lm", halo=0)
        button(img, px0 + 12, py1 - 44, px1 - 12, py1 - 14, "Play with " + name.split()[0], 11, "focus")
    return img


# ------------------------------------------------------------------------------------------------ level select
def board_thumb(level_id):
    """The board opening of a level's composite (the scene with its pegs), as the level's thumbnail."""
    px = load_rgb(OUT_COMP / f"{level_id}@2x.png")
    return px[82:1188, 150:1450]


def level_tile(img, x, y, w, entry, small=False):
    n, nm, st, sc, lid = entry
    th = w * 1106 / 1300
    focus = st == "open"
    panel(img, x - 8, y - 8, x + w + 8, y + th + (52 if not small else 40), r=9,
          tint0="#2A3E7C" if focus else "#1C2A5C", tint1="#121A3C", inner_rule=False)
    if lid:
        place_rgb(img, board_thumb(lid), x, y, w, th)
    else:
        # not yet reached: the scene is a sealed plate, engraved with the stage's crest of a crescent over the road
        sl, X, Y = img.win(x + w / 2, y + th / 2, max(w, th) / 2 + 2)
        from frame_rich import enamel_over_guilloche
        from rich_lib import sd_rrect
        cov = img.cov(sd_rrect(X, Y, x, y, x + w, y + th, 3))
        enamel_over_guilloche(img, sl, X, Y, cov, "ring", x + w / 2, y + th / 2, lit=(x, y), dims=(w, th),
                              tint0="#1A2650", tint1="#0E1430")
        text(img, x + w / 2, y + th / 2, "opens after " + STAGE4[int(n[-1]) - 2][0], "ui", 12 if not small else 9.5,
             P["ink_dim"], anchor="mm", halo=0)
    # the thumbnail's brass slip
    from rich_lib import draw_brass, sd_rrect
    draw_brass(img, lambda X, Y: np.abs(sd_rrect(X, Y, x, y, x + w, y + th, 3)) - 1.0, (x + w / 2, y + th / 2, max(w, th) / 2 + 3),
               "round", depth=1.0, width=1.0)
    fs = 15 if not small else 11.5
    text(img, x, y + th + (18 if not small else 13), n, "ui_sb", 11 if not small else 9, P["gilt_high"], anchor="lm", halo=0)
    text(img, x + (32 if not small else 24), y + th + (18 if not small else 13), nm, "serif", fs,
         P["cream"] if lid else "#7E86A4", anchor="lm", halo=0)
    if st in ("won", "aced"):
        text(img, x, y + th + (40 if not small else 30), "Best " + sc, "ui", 11.5 if not small else 9, P["ink_dim"], anchor="lm", halo=0)
        draw_moon(img, x + w - 10, y + th + (40 if not small else 30), 6 if not small else 5, "orange", "lit", variant=2, sky="#141C3A")
        if st == "aced":
            text(img, x + w - 22, y + th + (40 if not small else 30), "aced", "serif_i", 12 if not small else 9.5,
                 P["gilt_high"], anchor="rm", halo=0)
    elif st == "open":
        text(img, x, y + th + (40 if not small else 30), "Ace score 240,000", "ui", 11.5 if not small else 9,
             P["ink_dim"], anchor="lm", halo=0)


def levels(W=1280, H=800):
    small = W < 700
    S = 1.0
    ch = 1060.0 * H / W
    img = Img(W, H, S, px=chart_background(W, H, S, (150.0, min(400.0, 872.0 - ch), 1060.0, ch)))
    dim(img, 0.55)
    if not small:
        button(img, 20, 20, 120, 52, "Map", 14)
        cameo_badge(img, cameo("haldbrand"), 210, 72, 44, 51)
        text(img, 272, 50, "The Moon Road · stage 4", "ui_sb", 12, P["gilt_high"], anchor="lm", halo=0.4)
        text(img, 272, 80, STAGE_NAMES["4"], "serif", 28, P["cream"], anchor="lm", halo=0.4)
        text(img, 272, 108, "Haldbrand Tidewatch carries Lunar Burst through these five.", "ui", 13, P["ink_dim"], anchor="lm", halo=0.4)
        w, gx, x0, y0 = 220, 21, 48, 190
        for i, e in enumerate(STAGE4):
            level_tile(img, x0 + i * (w + gx), y0, w, e)
        panel(img, 48, 560, 1232, 760, r=12, bead=True)
        place_rgb(img, board_thumb("base-p1"), 76, 584, 180, 153)
        text(img, 284, 606, "4-3  The Airship Road", "serif", 22, P["cream"], anchor="lm", halo=0)
        text(img, 284, 636, "A trail on a map: the airship's road from Limsa Lominsa to Ala Mhigo, a ring of moons on every stop.",
             "ui", 13, P["ink_dim"], anchor="lm", halo=0)
        text(img, 284, 664, "Clear the stops' oranges to finish the journey. Ace score 240,000.", "ui", 13, P["ink_dim"], anchor="lm", halo=0)
        cameo_badge(img, cameo("haldbrand"), 312, 712, 22, 26, beads=False)
        text(img, 344, 712, "with Haldbrand · Lunar Burst", "ui", 13, P["cream"], anchor="lm", halo=0)
        button(img, 1000, 690, 1200, 736, "Play 4-3", 18, "focus")
    else:
        button(img, 10, 10, 70, 36, "Map", 11)
        cameo_badge(img, cameo("haldbrand"), 104, 34, 22, 26, beads=False)
        text(img, 134, 24, "Stage 4", "ui_sb", 9.5, P["gilt_high"], anchor="lm", halo=0.4)
        text(img, 134, 44, STAGE_NAMES["4"], "serif", 16, P["cream"], anchor="lm", halo=0.4)
        w, gx, x0 = 182, 18, 29
        for i, e in enumerate(STAGE4):
            r, c = divmod(i, 3)
            level_tile(img, x0 + c * (w + gx) + (0 if r == 0 else (w + gx) / 2), 78 + r * 200, w, e, small=True)
        button(img, 470, 440, 630, 470, "Play 4-3", 13, "focus")
    return img


# ------------------------------------------------------------------------------------------------ in game
def portrait(key):
    """A cameo for the HUD's power medallion, clipped to its disc."""
    def draw(img, mx, my, mr):
        rgba = cameo(key, S=3.0).copy()
        h, w = rgba.shape[:2]
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        d = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h * 0.46) / (w / 2)) ** 2)
        rgba[..., 3] = rgba[..., 3] * np.clip((1 - d) * 60, 0, 1)
        place_rgba(img, rgba[int(h * 0.46 - w / 2):int(h * 0.46 + w / 2)], mx - mr, my - mr, 2 * mr, 2 * mr)
    return draw


def window(board_2x, W, H):
    """The game window: the 800 x 600 board scaled to fit, the margins in the rails' enamel with a brass bead."""
    img = Img(W, H, 1.0)
    k = min(W / 800, H / 600)
    bw, bh = 800 * k, 600 * k
    bx = (W - bw) / 2
    if bx > 1:
        from frame_rich import enamel_over_guilloche, rosette
        sl, X, Y = img.full()
        enamel_over_guilloche(img, sl, X, Y, np.ones(X.shape, np.float32), "wave", dims=(W, H), tint0="#1A2856",
                              tint1="#0E1432")
        for cx in (bx / 2, W - bx / 2):
            rosette(img, cx, H / 2, R=18, petals=12, cab=5.0)
            gilt_rule(img, cx - 0.01, cx + 0.01, 0, ends=False) if False else None
    place_rgb(img, board_2x, bx, 0, bw, bh)
    return img


def board(level_id, scene, bucket, seed, **kw):
    from composite import render
    im, colours = render(level_id, scene, bucket, seed, S=2, **kw)
    return im.px, colours


def hud(W=1280, H=800):
    import json
    import playfield as pf
    lvl = json.loads((OUT_COMP.parent / "levels" / "base-p1.json").read_text())
    pegs = [(p["x"], p["y"]) for p in lvl["pegs"]]
    lit = {i for i, (x, y) in enumerate(pegs) if 300 <= x <= 360 and 300 <= y <= 470}
    gone = {i for i, (x, y) in enumerate(pegs) if 100 <= x <= 230 and 420 <= y <= 480}

    def after(img, level, colours, aimed):
        start, d = aimed
        live = [p for i, p in enumerate(pegs) if i not in gone]
        pf.guide_dots(img, start, d, live, super_guide=True)

    px, _ = board("base-p1", "base-p1-airship-road", "cart", 2, lit=lit, gone=gone, aim=-14.0, after=after,
                  bucket_x=300.0,
                  hud_kw=dict(stage="4-3", score="96,420", balls=5, cleared=9, mult="×1", oranges=16,
                              power="Lunar Burst", turns=1, portrait=portrait("haldbrand"), gauge=0.2))
    return window(px, W, H)


def fever(W=1280, H=800):
    import json
    from rich_lib import draw_ball
    lvl = json.loads((OUT_COMP.parent / "levels" / "exp-p2.json").read_text())
    n = len(lvl["pegs"]) + len(lvl["bricks"])
    rng = np.random.default_rng(4)
    gone = set(rng.choice(n, size=int(n * 0.62), replace=False).tolist())
    # every orange has been hit by now (the last one started Fever), so none is left on the board
    from board import engine_colours
    cols = engine_colours(OUT_COMP.parent / "levels" / "exp-p2.json", 5, 7)
    gone |= {i for i, c in enumerate(cols) if c == "orange"}
    lit = set(range(n)) - gone

    def extra(img):
        sl, X, Y = img.full()
        img.mul(sl, hexc("#03050C"), np.exp(-((Y - 240) / 36) ** 2) * ((X > 75) & (X < 725)) * 0.55)

    def after(img, level, colours, aimed):
        from rich_lib import draw_brass, sd_rrect
        draw_ball(img, 452.0, 452.0)
        text(img, 400, 236, "FULL MOON", "title_b", 46, P["cream"], anchor="mm", halo=0.7, tracking=7)
        draw_brass(img, lambda X, Y: sd_rrect(X, Y, 250, 266, 550, 267.4, 0.7), (400, 266.7, 160), "round", depth=0.6, width=0.7)
        text(img, 400, 284, "Every moon left is worth more. Pick your cup.", "ui", 12, P["ink_dim"], anchor="mm", halo=0.6)

    px, _ = board("exp-p2", "exp-p2-lantern-ferry", "fever", 7, lit=lit, gone=gone, extra=extra, after=after,
                  hud_kw=dict(stage="9-2", score="412,960", balls=3, cleared=25, mult="×10", oranges=0, ball=False,
                              power="Fireball", turns=0, portrait=portrait("aldous"), gauge=0.8, aim=0.0))
    return window(px, W, H)


def tally(W=1280, H=800):
    small = W < 700
    import json
    lvl = json.loads((OUT_COMP.parent / "levels" / "exp-p1.json").read_text())
    n = len(lvl["pegs"]) + len(lvl["bricks"])
    gone = set(range(n)) - {3, 9, 17, 22, 30, 41, 47, 55, 61}
    px, _ = board("exp-p1", "exp-p1-sharlayan", "boat", 4, gone=gone,
                  hud_kw=dict(stage="7-4", score="278,640", balls=3, cleared=25, mult="×10", oranges=0, ball=False,
                              power="Moonbloom", turns=0, portrait=portrait("ottilie")))
    img = window(px, W, H)
    dim(img, 0.50)
    rows = [("Shots", "278,640"), ("Full Moon, centre cup", "100,000"), ("Balls left  3 × 10,000", "30,000"),
            ("Long Shot ×2", "50,000"), ("Off the Wall", "25,000")]
    if not small:
        x0, y0, x1, y1 = 360, 130, 920, 690
        panel(img, x0, y0, x1, y1, r=14, bead=True)
        text(img, 640, y0 + 52, "The Domes of Sharlayan", "serif", 28, P["cream"], anchor="mm", halo=0)
        text(img, 640, y0 + 84, "The Far Shore · 7-4 · won with 3 balls to spare", "ui", 13, P["ink_dim"], anchor="mm", halo=0)
        gilt_rule(img, x0 + 60, x1 - 60, y0 + 110)
        y = y0 + 150
        for lab, v in rows:
            text(img, x0 + 70, y, lab, "serif", 17, P["cream"], anchor="lm", halo=0)
            text(img, x1 - 70, y, v, "ui_sb", 17, P["cream"], anchor="rm", halo=0)
            y += 38
        gilt_rule(img, x0 + 60, x1 - 60, y - 6, ends=False)
        text(img, x0 + 70, y + 30, "Total", "serif", 22, P["gilt_high"], anchor="lm", halo=0)
        text(img, x1 - 70, y + 30, "483,640", "ui_sb", 26, P["gilt_high"], anchor="rm", halo=0)
        # the ace plate: earned, the moon is lit
        draw_moon(img, x0 + 92, y + 92, 14, "orange", "lit", variant=1, sky="#141C3A")
        text(img, x0 + 120, y + 86, "Aced", "serif", 20, P["gilt_high"], anchor="lm", halo=0)
        text(img, x0 + 120, y + 108, "ace score 420,000 · new best", "ui", 12, P["ink_dim"], anchor="lm", halo=0)
        cameo_badge(img, cameo("ottilie"), x1 - 110, y + 96, 34, 40)
        button(img, x0 + 50, y1 - 70, x0 + 200, y1 - 30, "Replay", 15)
        button(img, x0 + 215, y1 - 70, x0 + 345, y1 - 30, "Map", 15)
        button(img, x0 + 360, y1 - 70, x1 - 50, y1 - 30, "Next: 7-5", 16, "focus")
    else:
        x0, y0, x1, y1 = 120, 40, 520, 440
        panel(img, x0, y0, x1, y1, r=10)
        text(img, 320, y0 + 30, "The Domes of Sharlayan", "serif", 18, P["cream"], anchor="mm", halo=0)
        text(img, 320, y0 + 52, "7-4 · won with 3 balls to spare", "ui", 10, P["ink_dim"], anchor="mm", halo=0)
        gilt_rule(img, x0 + 30, x1 - 30, y0 + 68)
        y = y0 + 92
        for lab, v in rows:
            text(img, x0 + 34, y, lab, "serif", 12.5, P["cream"], anchor="lm", halo=0)
            text(img, x1 - 34, y, v, "ui_sb", 12.5, P["cream"], anchor="rm", halo=0)
            y += 26
        text(img, x0 + 34, y + 14, "Total", "serif", 15, P["gilt_high"], anchor="lm", halo=0)
        text(img, x1 - 34, y + 14, "483,640", "ui_sb", 18, P["gilt_high"], anchor="rm", halo=0)
        draw_moon(img, x0 + 46, y + 56, 9, "orange", "lit", variant=1, sky="#141C3A")
        text(img, x0 + 64, y + 56, "Aced · new best", "serif", 13, P["gilt_high"], anchor="lm", halo=0)
        button(img, x0 + 24, y1 - 46, x0 + 124, y1 - 16, "Replay", 11)
        button(img, x0 + 134, y1 - 46, x0 + 224, y1 - 16, "Map", 11)
        button(img, x0 + 234, y1 - 46, x1 - 24, y1 - 16, "Next: 7-5", 12, "focus")
    return img


def pause(W=1280, H=800):
    small = W < 700
    px, _ = board("base-p2", "base-p2-holy-see", "cart", 3,
                  hud_kw=dict(stage="4-2", score="142,700", balls=6, cleared=12, mult="×2", oranges=13,
                              power="Lunar Burst", turns=0, portrait=portrait("haldbrand")))
    img = window(px, W, H)
    dim(img, 0.58)
    if not small:
        x0, y0, x1, y1 = 440, 150, 840, 660
        panel(img, x0, y0, x1, y1, r=14, bead=True)
        text(img, 640, y0 + 50, "Paused", "title_b", 30, P["cream"], anchor="mm", halo=0, tracking=5)
        text(img, 640, y0 + 80, "4-2 The Holy See · 6 balls · 13 oranges left", "ui", 12.5, P["ink_dim"], anchor="mm", halo=0)
        gilt_rule(img, x0 + 50, x1 - 50, y0 + 102)
        button(img, x0 + 60, y0 + 126, x1 - 60, y0 + 172, "Resume", 18, "focus")
        button(img, x0 + 60, y0 + 186, x1 - 60, y0 + 226, "Restart level", 15, sub=None)
        text(img, 640, y0 + 240, "hold to restart: the level's score is lost", "ui", 11, P["ink_dim"], anchor="mm", halo=0)
        button(img, x0 + 60, y0 + 262, x1 - 60, y0 + 302, "Options", 15)
        button(img, x0 + 60, y0 + 316, x1 - 60, y0 + 356, "Leave to the map", 15)
        gilt_rule(img, x0 + 50, x1 - 50, y0 + 382, ends=False)
        for i, (lab, val) in enumerate((("Reduce motion", "Off"), ("Decoration", "Full"), ("Sound", "70%"))):
            yy = y0 + 412 + i * 30
            text(img, x0 + 70, yy, lab, "ui", 13, P["cream"], anchor="lm", halo=0)
            text(img, x1 - 70, yy, val, "ui_sb", 13, P["gilt_high"], anchor="rm", halo=0)
        text(img, 640, y1 - 18, "Moonfall pauses itself in combat, duties and cutscenes.", "ui", 11, P["ink_dim"], anchor="mm", halo=0)
    else:
        x0, y0, x1, y1 = 190, 60, 450, 420
        panel(img, x0, y0, x1, y1, r=10)
        text(img, 320, y0 + 32, "Paused", "title_b", 20, P["cream"], anchor="mm", halo=0, tracking=3)
        gilt_rule(img, x0 + 30, x1 - 30, y0 + 56)
        button(img, x0 + 30, y0 + 74, x1 - 30, y0 + 108, "Resume", 13, "focus")
        button(img, x0 + 30, y0 + 120, x1 - 30, y0 + 150, "Restart level", 11)
        text(img, 320, y0 + 162, "hold to restart", "ui", 9, P["ink_dim"], anchor="mm", halo=0)
        button(img, x0 + 30, y0 + 176, x1 - 30, y0 + 206, "Options", 11)
        button(img, x0 + 30, y0 + 218, x1 - 30, y0 + 248, "Leave to the map", 11)
        for i, (lab, val) in enumerate((("Reduce motion", "Off"), ("Decoration", "Full"), ("Sound", "70%"))):
            yy = y0 + 280 + i * 22
            text(img, x0 + 34, yy, lab, "ui", 10, P["cream"], anchor="lm", halo=0)
            text(img, x1 - 34, yy, val, "ui_sb", 10, P["gilt_high"], anchor="rm", halo=0)
    return img


SCREENS = {"title": title, "map": adventure_map, "characters": characters, "levels": levels, "hud": hud,
           "fever": fever, "tally": tally, "pause": pause}


if __name__ == "__main__":
    names = sys.argv[1:] or list(SCREENS)
    for n in names:
        for (W, H) in ((1280, 800), (640, 480)):
            img = SCREENS[n](W, H)
            save_rgb(grain(img.px, 0.006, seed=3), OUT_SCREENS / f"{n}-{W}.png")
        print("ok", n)
