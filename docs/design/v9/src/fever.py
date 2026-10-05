"""Moonfall's Fever (plan v9 G3) and the free-ball cue, as playfield renders used by peg_states.py.

  fever_approach(S)  the ball nearing the last orange: the game at 1/10 speed and zoomed to 2x on the ball; the sky
                     dims a little, the last orange's halo widens, the ball leaves a short slow-motion trail
  fever_after(S)     after the hit: every remaining peg lights ("the board goes full"), the banner, and the five
                     Fever cups along the foot (10,000 / 50,000 / 100,000 / 50,000 / 10,000)
"""
import math

import numpy as np

from mf_lib import P, Img, draw_ball, draw_brass, draw_moon, hexc, ramp, sd_rrect, text
from playfield import (cradle_geometry, draw_level, frame, hud, launcher, level_layout, sky, WALL_L, WALL_R)
from portraits import medallion_portrait
from mf_lib import sd_circle


def late_level():
    """The sample level late on: one orange left (the 'last orange'), a handful of blues, a green and the purple."""
    pegs, bricks, kinds = level_layout()
    keep = {0, 2, 5, 7, 14, 16, 20, 24, 26, 29, 31, 36, 40, 44, 47, 52, 58, 61, 63, 66, 68, 70}
    n = len(pegs) + len(bricks)
    gone = set(range(n)) - keep
    oranges = [i for i in keep if kinds[i] == "orange"]
    last = 26 if 26 in keep else oranges[0]
    for i in keep:
        if kinds[i] == "orange" and i != last:
            kinds[i] = "blue"
    kinds[last] = "orange"
    return pegs, bricks, kinds, gone, last


def fever_cups(img):
    """Five brass cups across the foot, the centre one gilded brightest. Each is a shallow bowl seen a little from
    above: the far rim and a shadowed interior, the bowl's outer wall below, its near lip catching the light on the
    upper left. Its value is engraved on a small enamel plate set into the bowl's front."""
    vals = ["10,000", "50,000", "100,000", "50,000", "10,000"]
    w = (WALL_R - WALL_L) / 5
    for i, v in enumerate(vals):
        bx = WALL_L + w * (i + 0.5)
        rx, ry, rim_y, foot = w / 2 - 5, 5.0, 566.0, 590.0
        base = 0.10 if i == 2 else 0.0
        # the outer wall: the lower half of an ellipse from the rim down to the foot
        def wall(X, Y, bx=bx, rx=rx):
            e = ((X - bx) / rx) ** 2 + ((Y - rim_y) / (foot - rim_y)) ** 2 - 1
            return np.maximum(e * 8, rim_y - Y)
        draw_brass(img, wall, (bx, 578, rx + 4), "round", depth=5.0, width=6.0, base=base)
        # the interior seen over the near lip: dark, lit faintly on its far (lower-right-facing) wall
        sl, xx, yy = img.win(bx, rim_y, rx + 3)
        inner = img.cov(((xx - bx) / (rx - 2.0)) ** 2 + ((yy - rim_y) / (ry - 1.2)) ** 2 - 1)
        img.over(sl, ramp(np.clip((yy - rim_y + ry) / (2 * ry), 0, 1), [(0, "#2A2010"), (1, "#0C0A08")]), inner)
        far_wall = inner * np.clip((xx - bx) / rx, 0, 1) * np.clip(1 - (yy - rim_y + ry) / (2 * ry), 0, 1)   # the far right wall faces the light
        img.add(sl, hexc("#9A7E4A"), far_wall * 0.35)
        # the rim: a ring round the opening, its near lip lit on the upper left
        def rim(X, Y, bx=bx, rx=rx):
            e = np.sqrt(((X - bx) / rx) ** 2 + ((Y - rim_y) / ry) ** 2)
            return (np.abs(e - 0.93) - 0.08) * rx * 0.5
        draw_brass(img, rim, (bx, rim_y, rx + 3), "round", depth=1.2, width=1.2, base=base + 0.05)
        # the plate, set into the bowl's front, with a hairline shadow under its top edge
        plate = lambda X, Y, bx=bx: sd_rrect(X, Y, bx - 25, 575, bx + 25, 587, 3)
        sl, xx, yy = img.win(bx, 581, 30)
        img.over(sl, hexc(P["enamel_deep"]), img.cov(plate(xx, yy)) * 0.95)
        img.mul(sl, hexc("#05070F"), img.cov(plate(xx, yy)) * np.exp(-((yy - 575.8) / 0.7) ** 2) * 0.6)
        text(img, bx, 581.5, v, "ui_sb", 9.0 if i != 2 else 9.8, P["gilt_high"] if i == 2 else P["cream"], anchor="mm", halo=0)


def fever_after(S=2):
    img = Img(800 * S, 600 * S, S)
    sky(img, fever=0.6)
    sl, xx, yy = img.win(400, 240, 330)
    img.mul(sl, hexc("#03050C"), np.exp(-((yy - 240) / 34) ** 2) * ((xx > WALL_L) & (xx < WALL_R)) * 0.55)   # the banner's band, behind the pegs
    pegs, bricks, kinds, gone, last = late_level()
    lit = set(range(len(pegs) + len(bricks))) - gone
    draw_level(img, pegs, bricks, kinds, lit=lit, gone=gone | {last})
    frame(img)
    launcher(img, aim_deg=0.0, gauge=0.8, ball=False)
    fever_cups(img)
    draw_ball(img, 452.0, 470.0)
    # the banner: the release of the shot, in the frame's own type and gilt, over a soft abyss band
    text(img, 400, 236, "FULL MOON", "serif", 46, P["cream"], anchor="mm", halo=0.7, tracking=7)
    rule = lambda X, Y: sd_rrect(X, Y, 270, 266, 530, 267.4, 0.7)
    draw_brass(img, rule, (400, 266.7, 140), "round", depth=0.6, width=0.7)
    text(img, 400, 282, "Every peg left is worth more. Pick your cup.", "ui", 11.5, P["ink_dim"], anchor="mm", halo=0.6)
    hud(img, score="412,960", balls=3, mult="×10", cleared=25, oranges_left=0, power="Super Guide", turns=0,
        portrait=medallion_portrait("pipiru"))
    return img


def fever_approach(S=2):
    """Rendered at 2S and cropped round the ball, which is how the engine's 2x zoom looks at the same window size."""
    Z = 2
    img = Img(800 * S * Z, 600 * S * Z, S * Z)
    sky(img, fever=0.35)
    pegs, bricks, kinds, gone, last = late_level()
    draw_level(img, pegs, bricks, kinds, gone=gone)
    frame(img)
    lx, ly = pegs[last]
    # the last orange: its halo widens as the ball nears (a slow breath, not a flash)
    sl, xx, yy = img.win(lx, ly, 60)
    d = np.sqrt((xx - lx) ** 2 + (yy - ly) ** 2)
    img.add(sl, hexc("#FFB070"), np.exp(-np.clip(d - 10, 0, None) / 9) * 0.22)
    # the ball and its slow-motion trail: fading copies along the last tenth of a second of its arc
    bx, by = lx - 22, ly - 27
    for k in range(5, 0, -1):
        draw_ball(img, bx - 3.2 * k, by - 4.6 * k + 0.12 * k * k, alpha=0.10 + 0.05 * (5 - k))
    draw_ball(img, bx, by)
    cx, cy = (bx + lx) / 2, (by + ly) / 2
    cx = min(max(cx, 200), 600)
    cy = min(max(cy, 150), 450)
    x0, y0 = int((cx - 200) * S * Z), int((cy - 150) * S * Z)
    out = Img(800 * S, 600 * S, S, img.px[y0:y0 + 600 * S, x0:x0 + 800 * S].copy())
    return out, (cx, cy)
