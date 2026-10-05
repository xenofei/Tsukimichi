"""The playfield chrome, rich pass 2: the frame and HUD built from FFXIV's own UI art, graded to Menphina's Medallion,
in the level's palette. Board units (800 x 600); hr textures at 0.5 units per hr px, as the game draws them.

What each part is made of (every texture is recorded in ../sources.json):
  rails       the quest journal's window ground (Journal_Detail), graded to enamel in the level's jewel
  outer frame the quest journal's gilt frame (Journal_Frame): vine corners above, banner-and-reed corners below
  walls       the journal frame's gilt triple rule along the board's opening (the ball turns at it)
  lower rails the journal's vertical scroll ornament (Journal_Frame), standing where the pilasters stood
  launcher    the approved brass telescope (playfield.launcher) under the PvP rank emblem's gilt wings (PVPRankEmblem3)
  name plate  the Gold Saucer's gilt pill (LovmPalette) with the level's name in Jupiter
  score       a recessed plate in the pill, TrumpGothic numerals in gold
  ball tube   the approved glass tube, its cage the journal's vertical rules, its finials the PvP spire
  dial        Lord of Verminion's great lattice ring round an enamel face, the oranges-cleared arc in gold, the
              multiplier in TrumpGothic
  medallion   the carrier's face (the plugin's card crop) in the lattice ring, the power's name in Jupiter, the
              scholar gauge's three gems as the turns left, lit in the carrier's colour
  buckets     the approved lantern cart and boat
"""
import math

import numpy as np

from r2lib import (C, PALETTES, blit, gild, gilt_frame, gtext, gwidth, hexc, part, ramp, ring, disc_image, screen,
                   sd_rrect, smooth, enamel_ground, shadow_under, blur)
import r2kit as K
import r2cast
import frame_rich as fr
import playfield as pf
from rich_lib import draw_ball, draw_moon

WALL_L, WALL_R, TOP, FOOT = 75.0, 725.0, 41.0, 594.0
# the rails' interiors between the outer frame's band and the walls' band (round 2: the instruments are centred here)
LX, RX, RAIL_W = 33.0, 767.0, 42.0
SMALL = {"on": False}         # the 640 x 480 variant: labels that cannot meet the floor there are left out
BUCKET_X_DEFAULT = fr.BUCKET_X_DEFAULT


def rails(img, pal):
    """The enamel rails round the board opening, in the level's jewel, and the outer gilt frame."""
    p = PALETTES[pal]
    stops = [(0.0, "#070A1E"), (0.45, p["sky"]), (1.0, _mix(p["sky"], p["jewel1"], 0.45))]
    from r2lib import Img
    # one continuous ground for all the rails, laid round the opening (no seams between rails)
    tmp = Img(img.w, img.h, img.S, px=np.zeros_like(img.px))
    enamel_ground(tmp, 0, 0, 800, 600, r=0, stops=stops, sheen=0.08, jewel=p["jewel2"])
    sl, xx, yy = img.full()
    board = (xx > WALL_L) & (xx < WALL_R) & (yy > TOP) & (yy < FOOT)
    img.px = np.where(board[..., None], img.px, tmp.px)
    sh = np.maximum(np.clip(1 - (yy - TOP) / 10, 0, 1), np.clip(1 - (xx - WALL_L) / 10, 0, 1)) * board
    img.mul(sl, hexc("#02030A"), sh * 0.55)
    # the walls: the journal's gilt band round the opening, entirely outside it (round 3, critic K1: the band and its
    # shadow end at the wall, never inside), plain mitred corners
    gilt_frame(img, WALL_L - 17, TOP - 17, WALL_R + 17, FOOT + 30, scale=0.46, corners=False, warm=0.1)
    # and a dark reveal either side of every wall: 2.5 units inside, and a dark enamel fillet of 4 units between the
    # wall and the gilt, so a peg touching a wall sits on dark, not on gilt (critic K1; the approved frame's beads
    # had the same dark groove)
    edge = np.minimum.reduce([xx - WALL_L, WALL_R - xx, yy - TOP])
    img.mul(sl, hexc("#02030A"), np.clip(1 - edge / 2.5, 0, 1) * board * 0.7)
    out = np.maximum.reduce([WALL_L - xx, xx - WALL_R, TOP - yy])
    fillet = (out > 0) & (out < 4.2) & (yy < FOOT) & (xx > WALL_L - 5) & (xx < WALL_R + 5)
    img.mul(sl, hexc("#03040C"), fillet.astype(np.float32) * 0.8)
    # the outer frame: the journal's frame with its vine and reed corners, in the rails
    gilt_frame(img, 0, 0, 800, 600, scale=0.40, corners=True, warm=0.1)


def _mix(a, b, t):
    v = hexc(a) * (1 - t) + hexc(b) * t
    return "#%02X%02X%02X" % tuple(int(c * 255) for c in np.clip(v, 0, 1))


def rail_rules(img):
    """Round 2 (designer m7, UX m3: no floating ornament): the side rails are divided into their instruments by the
    journal's gilt rule, set across each rail as a shelf (structure: it separates the ball tube from the lower rail,
    and the medallion's plate from the lower rail on the right)."""
    r = gild(part("jf_rule_a"), 0.55, 0.1)
    seg = r[:, 20:60]
    for x0 in (LX - RAIL_W / 2, RX - RAIL_W / 2):
        blit(img, seg, x0, 424, RAIL_W, r.shape[0] * 0.40)


def fever_cups_lit(img, accent):
    """Fever's cups (round 2, designer M4, UX M5): the approved cups lit from within (the centre cup brightest), each
    value on a larger plate in TrumpGothic at the number floor."""
    from rich_lib import draw_brass
    vals = ["10,000", "50,000", "100,000", "50,000", "10,000"]
    w = (WALL_R - WALL_L) / 5
    for i, v in enumerate(vals):
        bx = WALL_L + w * (i + 0.5)
        sl, X, Y = img.win(bx, 560, w * 0.7)
        d = np.sqrt(((X - bx) / (w * 0.45)) ** 2 + ((Y - 566) / 14) ** 2)
        k = 0.55 if i == 2 else 0.22
        img.add(sl, hexc("#FFD27A"), np.exp(-d ** 2 * 1.6) * k)
        img.add(sl, hexc(accent), np.exp(-(np.clip(d - 1.0, 0, None) / 0.6) ** 2) * (d > 0.95) * (0.30 if i == 2 else 0.10))
        sl2, X2, Y2 = img.win(bx, 582, 46)
        pm = img.cov(sd_rrect(X2, Y2, bx - 36, 574, bx + 36, 591, 5))
        img.over(sl2, ramp(np.clip((Y2 - 574) / 17, 0, 1), [(0, "#121A40"), (1, "#070B22")]), pm)
        draw_brass(img, lambda X_, Y_, bx=bx: np.abs(sd_rrect(X_, Y_, bx - 36, 574, bx + 36, 591, 5)) - 0.9,
                   (bx, 582, 42), "round", depth=1.0, width=0.9)
        gtext(img, bx, 582.5, v, "trump", 18, C["gold_hi"] if i == 2 else C["cream"], anchor="mm", edge="#05070F",
              edge_w=0.6, gilt=(i == 2))


def lower_ornaments(img):
    """The journal's vertical scroll ornament on each lower side rail (where the pilasters stood)."""
    o = gild(part("jf_vscroll"), 0.55, 0.1)
    w, h = 26.0, 26.0 * o.shape[0] / o.shape[1]
    for (cx, flip) in ((37.5, False), (762.5, True)):
        blit(img, o, cx - w / 2, 410, w, h, flipx=flip)


def crest(img):
    """The PvP rank emblem's gilt wings either side of the launcher's yoke; their inner ends tuck under a moonstone in
    a lattice ring large enough to cover the join (round 2, n1: no hard cuts at the centre)."""
    wg = gild(part("pvp_wing"), 0.5, 0.05)
    w = 66.0
    h = w * wg.shape[0] / wg.shape[1]
    blit(img, wg, 400 - 6 - w, 2, w, h)
    blit(img, wg, 400 + 6, 2, w, h, flipx=True)
    sl, X, Y = img.win(400, 20, 14)
    img.over(sl, hexc("#0A1030"), img.cov(np.sqrt((X - 400) ** 2 + (Y - 20) ** 2) - 9.5))
    fr.moonstone(img, 400, 20, 6.0)
    ring(img, 400, 20, 7.6, "lv_ring")


def name_plate(img, stage, name):
    K.pill(img, 86, 8, 300, 34, "normal")
    sl, X, Y = img.win(103, 21, 14)
    img.over(sl, hexc("#0A1030"), img.cov(np.sqrt((X - 103) ** 2 + (Y - 21) ** 2) - 10.0))
    ring(img, 103, 21, 10.0, "lv_ring")
    gtext(img, 103, 21.5, stage, "trump_s", 16.5, C["gold_hi"], anchor="mm", edge="#05070F", edge_w=0.6)
    gtext(img, 124, 21, name, "jupiter", 27, C["cream"], anchor="lm", edge="#05070F", edge_w=0.8)


def score_plate(img, score):
    K.pill(img, 588, 8, 716, 34, "normal")
    sl, X, Y = img.win(652, 21, 70)
    m = img.cov(sd_rrect(X, Y, 598, 12.5, 706, 29.5, 8.5))
    img.over(sl, ramp(np.clip((Y - 12) / 18, 0, 1), [(0, "#03050E"), (1, "#0A1230")]), m)
    img.mul(sl, hexc("#000000"), m * np.clip(1 - (Y - 12.5) / 3.5, 0, 1) * 0.6)
    if not SMALL["on"]:
        gtext(img, 604, 21, "SCORE", "axis", 11.5, "#D8C49A", anchor="lm")
    gtext(img, 701, 21.5, score, "trump", 26, C["gold_hi"], anchor="rm", gilt=True, edge="#120A02", edge_w=0.8,
          glow="#FFB45E", glow_k=0.18, glow_r=3)


def ball_tube(img, balls=6, accent="#C9D6FF"):
    """The approved glass tube and its balls; its cage is the journal's vertical rule, its finials the PvP spire."""
    x0, x1, y0, y1 = LX - 13, LX + 13, 66.0, 330.0
    cx = LX
    sl, xx, yy = img.win(cx, (y0 + y1) / 2, (y1 - y0) / 2 + 22)
    tube = img.cov(sd_rrect(xx, yy, x0, y0, x1, y1, 12))
    across = (xx - x0) / (x1 - x0)
    wall = np.clip(np.abs(across - 0.5) * 2, 0, 1) ** 3
    img.mul(sl, hexc("#03040C"), tube * (0.45 + 0.35 * wall))
    for i in range(balls):
        draw_ball(img, cx, y1 - 13 - i * 25.5, r=10.5)
    img.add(sl, hexc("#F4F2EA"), tube * np.exp(-((across - 0.21) / 0.028) ** 2) * 0.5)
    img.add(sl, hexc("#8FA4DA"), tube * np.exp(-((across - 0.86) / 0.03) ** 2) * 0.22)
    edge_l = np.exp(-((across - 0.04) / 0.025) ** 2) + np.exp(-((across - 0.96) / 0.025) ** 2)
    img.add(sl, hexc("#C3CEE4"), tube * edge_l * 0.30)
    # the cage: the journal's triple rule either side
    v = gild(part("jf_vrule2"), 0.55, 0.1)
    for (xx_, flip) in ((x0 - 7.5, False), (x1 - 2.0, True)):
        blit(img, v[6:-6], xx_, y0 - 2, 9.5, y1 - y0 + 4, flipx=flip)
    sp = gild(part("pvp_spire"), 0.5, 0.05)
    sw = 15.0
    sh_ = sw * sp.shape[0] / sp.shape[1]
    blit(img, sp[:int(sp.shape[0] * 0.42)], cx - sw / 2, y0 - sh_ * 0.42 + 4, sw, sh_ * 0.42)
    blit(img, sp[:int(sp.shape[0] * 0.42)], cx - sw / 2, y1 - 4, sw, sh_ * 0.42, flipy=True)
    # the count: a small pill, TrumpGothic
    K.pill(img, LX - 20, 370, LX + 20, 396, "normal")
    gtext(img, LX, 383.5, str(balls), "trump", 24, C["cream"], anchor="mm", edge="#05070F", edge_w=0.6)
    if not SMALL["on"]:
        gtext(img, LX, 408, "BALLS", "axis", 11.5, "#D8C49A", anchor="mm", edge="#05070F", edge_w=0.6)


def mult_dial(img, cleared=11, mult="×2", accent="#FFB45E"):
    cx, cy, R = RX, 96.0, 14.0
    sl, xx, yy = img.win(cx, cy, R + 4)
    face = img.cov(np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) - R)
    rr = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    img.over(sl, ramp(np.clip(rr / R, 0, 1), [(0, "#24357A"), (0.8, "#111A44"), (1, "#070B22")]), face)
    ang = (np.degrees(np.arctan2(yy - cy, xx - cx)) + 90) % 360
    frac = cleared / 25.0
    band = np.clip(0.5 - (np.abs(rr - (R - 2.4)) - 1.6) * img.S, 0, 1)
    img.over(sl, hexc("#04060F"), band * 0.8)
    arc = band * smooth(frac * 360 + 0.8, frac * 360 - 0.8, ang)
    img.over(sl, ramp(np.clip(ang / 360, 0, 1), [(0, "#FFCF6A"), (1, "#FF8A3A")]), arc)
    img.add(sl, hexc("#FFB45E"), arc * 0.25)
    for n_left in (15, 10, 6, 3):
        a = math.radians((25 - n_left) / 25 * 360 - 90)
        tx, ty = cx + math.cos(a) * (R - 2.4), cy + math.sin(a) * (R - 2.4)
        s2, X2, Y2 = img.win(tx, ty, 3)
        img.over(s2, hexc("#FFF0C0"), img.cov(np.sqrt((X2 - tx) ** 2 + (Y2 - ty) ** 2) - 0.8))
    ring(img, cx, cy, R + 0.6, "lv_ring_big")
    # round 2 (UX M5): a real multiplication sign (AXIS has one; TrumpGothic's drew as a dot) and the digits in
    # TrumpGothic at the number floor
    digits = mult.lstrip("×x")
    wd = gwidth(digits, "trump", 18)
    wx = gwidth("×", "axis", 15)
    x0 = cx - (wd + wx) / 2
    gtext(img, x0, cy + 1, "×", "axis", 15, C["cream"], anchor="lm", edge="#05070F", edge_w=0.8)
    gtext(img, x0 + wx, cy + 1, digits, "trump", 18, C["cream"], anchor="lm", edge="#05070F", edge_w=0.8, glow=accent,
          glow_k=0.25, glow_r=3)


def oranges_left(img, n=14):
    draw_moon(img, RX - 12, 148, 7.5, "orange", sky="#16245A")
    gtext(img, RX - 2, 148.5, str(n), "trump", 24, C["cream"], anchor="lm", edge="#05070F", edge_w=0.6)
    if not SMALL["on"]:
        gtext(img, RX, 170, "ORANGES", "axis", 11, "#D8C49A", anchor="mm", edge="#05070F", edge_w=0.6)


def power_medallion(img, carrier="SuperGuide", turns=2, active=True, pulse=0.0):
    c = r2cast.BY_ID[carrier]
    mx, my, mr = RX, 232.0, 14.5
    sl, X, Y = img.win(mx, my, mr * 3)
    d = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    if active:
        img.add(sl, hexc(c["accent"]), np.exp(-(np.clip(d - mr * 1.5, 0, None) / (7.0 + 6 * pulse)) ** 2) * (d > mr) * (0.30 + 0.4 * pulse))
    K.medallion(img, c, mx, my, mr, "lv_ring")
    # the power's name (round 2, UX m4 and designer m1): one fitting rule (one line if it fits the rail less 6 units a
    # side, else two lines; never below the label floor), in the carrier's colour on a dark plate
    nm = c["power"]
    lines = fit_name(nm, RAIL_W - 6)
    y = 268
    if lines and not SMALL["on"]:
        size, rows = lines
        hh = 12.5 * len(rows) + 5
        sl, X, Y = img.win(mx, y + hh / 2 - 8, 40)
        img.over(sl, hexc("#060816"), img.cov(sd_rrect(X, Y, RX - RAIL_W / 2, y - 8, RX + RAIL_W / 2, y - 8 + hh, 4)) * 0.85)
        for ln in rows:
            gtext(img, mx, y, ln, "jupiter", size, K_tint(c["accent"], 0.15), anchor="mm", edge="#05070F", edge_w=0.6)
            y += 12.5
    else:
        y = 262
    # turns left: one gem for each turn the power lasts (round 3, GD N6 and UX m1: Brass Wings has five)
    K.gems_n(img, mx, y + 8, RAIL_W - 2, total=c.get("turns", 1), lit=turns, accent=c["accent"])


def fit_name(nm, avail, max_size=18.0, min_size=11.0):
    """The power's name on one line if it fits `avail` units, else on two (at a word or a hyphen), at the largest size
    from max_size down that fits; None if nothing fits at min_size."""
    words = nm.replace("-", "- ").split()
    cands = [[nm]] + [[" ".join(words[:k]).replace("- ", "-"), " ".join(words[k:]).replace("- ", "-")] for k in range(1, len(words))]
    size = max_size
    while size >= min_size:
        for rows in cands:
            if all(gwidth(r, "jupiter", size) <= avail for r in rows):
                return size, rows
        size -= 0.5
    return None


def K_tint(hx, k=0.3):
    v = hexc(hx) * (1 - k) + hexc("#FFF6E8") * k
    return "#%02X%02X%02X" % tuple(int(c_ * 255) for c_ in v)


def playfield_chrome(img, level=None, pal="base-p1", bucket="boat", bucket_x=BUCKET_X_DEFAULT, aim=None, hud_kw=None):
    hud_kw = dict(hud_kw or {})
    if bucket == "boat":
        pf.bucket_boat(img, bucket_x)
    elif bucket == "cart":
        fr.bucket_cart(img, bucket_x)
    elif bucket == "fever":
        import fever as v9_fever
        v9_fever.fever_cups(img)
    if bucket == "fever":
        fever_cups_lit(img, hud_kw.pop("accent", "#FFD27A"))
    rails(img, pal)
    rail_rules(img)
    a = aim if aim is not None else hud_kw.pop("aim", 14.0)
    crest(img)
    aimed = pf.launcher(img, aim_deg=a, gauge=hud_kw.pop("gauge", 0.35), ball=hud_kw.pop("ball", True))
    name_plate(img, hud_kw.pop("stage", "4-3"), hud_kw.pop("name", level.get("name", "") if level else ""))
    score_plate(img, hud_kw.pop("score", "128,450"))
    ball_tube(img, hud_kw.pop("balls", 6))
    mult_dial(img, hud_kw.pop("cleared", 11), hud_kw.pop("mult", "×2"))
    oranges_left(img, hud_kw.pop("oranges", 14))
    power_medallion(img, hud_kw.pop("carrier", "SuperGuide"), hud_kw.pop("turns", 2), hud_kw.pop("active", True),
                    hud_kw.pop("pulse", 0.0))
    return aimed
