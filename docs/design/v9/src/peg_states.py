"""Moonfall peg states, Fever and the free-ball cue (plan v9 G8, deliverable 2): peg-states.png (1440 x 1060) and @2x.

Pegs and bricks are shown magnified (the shading is what the engine draws at r 10) with an actual-size strip; the
Fever and free-ball panels are cut from full playfield renders. Run: py -3 peg_states.py
"""
import numpy as np
from PIL import Image

from fever import fever_after, fever_approach
from mf_lib import (P, V9, Img, draw_ball, draw_brick, draw_moon, draw_motes, draw_brass, hexc, sd_rrect, sector_brick,
                    text)
from playfield import SKY_AT_PEGS, frame, hud, launcher, level_layout, draw_level, sky
from portraits import medallion_portrait

KINDS = ["blue", "orange", "green", "purple"]
NAMES = {"blue": "Blue", "orange": "Orange", "green": "Green", "purple": "Purple"}
ROLE = {"blue": "10 points", "orange": "100 points; clear all 25", "green": "10 points; wakes the power", "purple": "500 points; moves each shot"}


def panel(img, x0, y0, x1, y1, title=None):
    sl, xx, yy = img.win((x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) / 2 + 4)
    m = img.cov(sd_rrect(xx, yy, x0, y0, x1, y1, 6))
    img.over(sl, hexc(SKY_AT_PEGS), m)
    edge = lambda X, Y: np.abs(sd_rrect(X, Y, x0, y0, x1, y1, 6)) - 0.7
    draw_brass(img, edge, ((x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) / 2 + 4), "round", depth=0.7, width=0.7)
    if title:
        text(img, x0 + 14, y0 - 12, title, "serif", 15, P["cream"], anchor="ls", halo=0)


def paste(img, src, x, y, w, h):
    """Scales an Img's pixels into the sheet at (x, y) with size (w, h) in sheet units."""
    S = img.S
    im = Image.fromarray((np.clip(src.px, 0, 1) * 255 + 0.5).astype(np.uint8)).resize((int(w * S), int(h * S)), Image.LANCZOS)
    a = np.asarray(im, np.float32) / 255
    X, Y = int(x * S), int(y * S)
    img.px[Y:Y + a.shape[0], X:X + a.shape[1]] = a


def crop(src, x, y, w, h):
    S = src.S
    return Img(int(w * S), int(h * S), S, src.px[int(y * S):int((y + h) * S), int(x * S):int((x + w) * S)].copy())


def build(S=2):
    W, H = 1440, 1100
    img = Img(W * S, H * S, S)
    sl, xx, yy = img.full()
    img.px[sl] = np.asarray(hexc(P["status_foot"]))
    text(img, 40, 40, "Moonfall · peg states, Fever and the free-ball cue", "serif", 24, P["cream"], anchor="ls", halo=0)
    text(img, 40, 62, "Menphina's Medallion. One light for every asset: moonlight from the upper left, slightly in front of the board.",
         "ui", 12.5, P["ink_dim"], anchor="ls", halo=0)
    # ---------------------------------------------------------------- pegs, magnified
    x0, y0 = 40, 100
    panel(img, x0, y0, x0 + 820, y0 + 430, "Pegs as small moons (shown 3.6×; the engine draws r 10)")
    cols = ["Unlit", "Lit (hit)", "Clearing 1", "Clearing 2", "Clearing 3", "Actual size"]
    for c, name in enumerate(cols):
        text(img, x0 + 175 + c * 110, y0 + 28, name, "ui_sb", 11.5, P["gilt"], anchor="mm", halo=0)
    for r, k in enumerate(KINDS):
        cy = y0 + 92 + r * 92
        text(img, x0 + 20, cy - 8, NAMES[k], "serif", 15, P["cream"], anchor="lm", halo=0)
        text(img, x0 + 20, cy + 12, ROLE[k], "ui", 9.5, P["ink_dim"], anchor="lm", halo=0)
        cx = x0 + 175
        draw_moon(img, cx, cy, 36, k, "unlit", variant=r, rot=0.5 * r - 0.6)
        draw_moon(img, cx + 110, cy, 36, k, "lit", variant=r, rot=0.5 * r - 0.6)
        draw_moon(img, cx + 220, cy, 36, k, "lit", variant=r, rot=0.5 * r - 0.6, scale=1.06, flash=0.40)      # 0-60 ms: a bloom
        draw_moon(img, cx + 330, cy + 4, 36, k, "lit", variant=r, rot=0.5 * r - 0.6, scale=0.70, alpha=0.45)  # 60-150 ms: it sets, dimming
        draw_motes(img, cx + 330, cy, 36, k, 0.30, seed=r)
        draw_motes(img, cx + 440, cy, 36, k, 0.70, seed=r)                                     # 150-300 ms: its dust sifts down
        draw_moon(img, cx + 532, cy, 10, k, "unlit", variant=r, rot=0.5 * r - 0.6)
        draw_moon(img, cx + 568, cy, 10, k, "lit", variant=r, rot=0.5 * r - 0.6)
    # ---------------------------------------------------------------- bricks
    bx0 = 900
    panel(img, bx0, y0, bx0 + 500, y0 + 430, "Moonstone bricks (2.6×; 30 × 12 at r 236)")
    for c, name in enumerate(["Unlit", "Lit (hit)", "Clearing", "Actual size"]):
        text(img, bx0 + 90 + c * 118, y0 + 28, name, "ui_sb", 11.5, P["gilt"], anchor="mm", halo=0)
    for r, k in enumerate(KINDS):
        cy = y0 + 92 + r * 92
        for c, (st, a) in enumerate((("unlit", 1.0), ("lit", 1.0), ("lit", 0.45))):
            cx = bx0 + 90 + c * 118
            f, bb = sector_brick(cx, cy + 236 * 2.6, 236 * 2.6, -np.pi / 2, np.radians(3.55), 12 * 2.6)
            draw_brick(img, f, bb, k, st, variant=r, alpha=a)
            if c == 2:
                draw_motes(img, cx, cy, 30, k, 0.40, seed=10 + r)
        cx = bx0 + 90 + 3 * 118
        f, bb = sector_brick(cx, cy + 236, 236, -np.pi / 2, np.radians(3.55), 12)
        draw_brick(img, f, bb, k, "unlit", variant=r)
    # ---------------------------------------------------------------- Fever
    fy = 600
    ap, _ = fever_approach(S)
    af = fever_after(S)
    panel(img, 40, fy, 40 + 452, fy + 340, "Fever, 1: the last orange (1/10 speed, 2× zoom)")
    paste(img, ap, 46, fy + 6, 440, 330)
    panel(img, 512, fy, 512 + 452, fy + 340, "Fever, 2: after the hit")
    paste(img, af, 518, fy + 6, 440, 330)
    # ---------------------------------------------------------------- the free-ball cue
    fx = 984
    panel(img, fx, fy, fx + 416, fy + 340, "Free ball")
    states = []
    for (g, fl, nb) in ((0.48, None, False), (0.60, 0.6, False), (0.60, None, True)):
        t = Img(800 * S, 600 * S, S)
        sky(t)
        pegs, bricks, kinds = level_layout()
        draw_level(t, pegs, bricks, kinds, lit={9, 10, 11, 12, 18, 19, 20, 22})
        frame(t)
        launcher(t, aim_deg=12.0, gauge=g, ball=False, flare=fl)
        hud(t, new_ball=nb, balls=7 if nb else 6, portrait=medallion_portrait("pipiru"))
        states.append(t)
    paste(img, crop(states[0], 350, 34, 100, 72), fx + 10, fy + 10, 195, 140)
    paste(img, crop(states[1], 350, 34, 100, 72), fx + 211, fy + 10, 195, 140)
    paste(img, crop(states[2], 2, 118, 136, 101), fx + 10, fy + 178, 195, 145)
    text(img, fx + 107, fy + 163, "filling toward 75k", "ui", 10, P["ink_dim"], anchor="mm", halo=0.6)
    text(img, fx + 308, fy + 163, "75k reached: the notch blooms", "ui", 10, P["ink_dim"], anchor="mm", halo=0.6)
    text(img, fx + 300, fy + 214, "The ball arrives at the top", "ui", 10.5, P["cream"], anchor="mm", halo=0)
    text(img, fx + 300, fy + 230, "of the channel with a soft", "ui", 10.5, P["cream"], anchor="mm", halo=0)
    text(img, fx + 300, fy + 246, "bloom and a quiet +1.", "ui", 10.5, P["cream"], anchor="mm", halo=0)
    text(img, fx + 300, fy + 274, "A bucket catch: the same", "ui", 10.5, P["ink_dim"], anchor="mm", halo=0)
    text(img, fx + 300, fy + 290, "+1, and a chime.", "ui", 10.5, P["ink_dim"], anchor="mm", halo=0)
    # ---------------------------------------------------------------- the ball
    panel(img, 40, 990, 40 + 1360, 1080)
    draw_ball(img, 90, 1035, r=30)
    draw_ball(img, 150, 1035, r=6)
    text(img, 180, 1026, "The ball: satin silver, 6 units. Its brightest point faces the light; the lower half reflects the dark board.",
         "ui", 12, P["cream"], anchor="lm", halo=0)
    text(img, 180, 1046, "Clearing: a bloom (0-60 ms), the moon dims and sets a little (60-150 ms), its dust sifts down and fades (150-300 ms). Reduce motion: a 120 ms fade.",
         "ui", 12, P["ink_dim"], anchor="lm", halo=0)
    return img, af


if __name__ == "__main__":
    img, af = build()
    af.save(V9 / "fever@2x.png")
    af.save(V9 / "fever.png", (800, 600))
    img.save(V9 / "peg-states@2x.png")
    img.save(V9 / "peg-states.png", (1440, 1100))
    print("peg-states")
