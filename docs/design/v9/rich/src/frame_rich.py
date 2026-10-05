"""The rich playfield chrome (owner brief, 5 October 2026): Menphina's Medallion with real ornament and depth.

What changed from the approved v9 frame (docs/design/v9/src/playfield.py), all under the same one light
L = (-0.424, -0.424, 0.80):
  * the rails are guilloche enamel: fine engraved waves under translucent lapis enamel, as on a watch dial or a
    presentation medallion, so the frame has a surface at every size instead of flat paint;
  * the walls are pearl beading: a row of small brass beads along the board's edges (they are the walls the ball
    turns at);
  * the corners carry brass rosettes with moonstone cabochons; the side rails end in fluted brass pilasters;
  * the top rail has a level cartouche (stage roundel, name), an engraved escutcheon behind the launcher's yoke and a
    recessed score window;
  * the HUD instruments are richer: a caged glass ball tube with engraved gradations and acorn finials, a multiplier
    dial with a sunburst face and a knurled bezel, and a portrait medallion for the power's carrier;
  * two buckets of one craft: the lantern boat (expansion) and the lantern cart on the moon road (base).
The launcher (the brass telescope) and the pegs are the approved ones.
"""
import math

import numpy as np

from rich_lib import (L, H_BLINN, P, Img, blur, brass_shade, draw_ball, draw_brass, draw_moon, fbm, font, hexc,
                      normals_from_height, ramp, screen, sd_circle, sd_rrect, sd_segment, smooth, text, text_size)
import playfield as pf

WALL_L, WALL_R, TOP, FOOT = 75.0, 725.0, 41.0, 594.0
BUCKET_X_DEFAULT = 520.0


# ------------------------------------------------------------------------------------------------ materials
def guilloche(xx, yy, kind="wave", cx=0.0, cy=0.0, pitch=2.2):
    """Engraved line pattern, 0..1 (1 at a line's floor). wave: horizontal waved lines; sun: radial sunburst; ring:
    concentric rings with a slow wave (a rose-engine pattern)."""
    if kind == "wave":
        ph = (yy + 1.6 * np.sin(xx / 9.0) + 0.7 * np.sin(xx / 3.1 + yy / 7.0)) / pitch
    elif kind == "sun":
        ang = np.arctan2(yy - cy, xx - cx)
        ph = ang * 96 / (2 * math.pi) + 0.15 * np.sin(np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / 2.0)
    else:
        r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        ang = np.arctan2(yy - cy, xx - cx)
        ph = (r + 1.2 * np.sin(ang * 12)) / pitch
    return 0.5 + 0.5 * np.cos(ph * 2 * math.pi)


def enamel_over_guilloche(img, sl, xx, yy, cov, kind="wave", cx=0.0, cy=0.0, lit=(0.0, 0.0), dims=(800.0, 600.0),
                          tint0="#22336A", tint1="#121A3E", pitch=2.2):
    """Translucent lapis enamel fired over an engraved ground: the engraving shows through as fine light and dark
    lines, brightest where the light from the upper left falls on the far wall of each groove."""
    t = np.clip(((xx - lit[0]) / dims[0]) * 0.45 + ((yy - lit[1]) / dims[1]) * 0.55, 0, 1)
    base = ramp(t, [(0, tint0), (1, tint1)])
    g = guilloche(xx, yy, kind, cx, cy, pitch)
    gy_, gx_ = np.gradient(g)
    glint = np.clip(-(gx_ * L[0] + gy_ * L[1]) * img.S * 1.6, 0, 1)
    col = base * (0.86 + 0.16 * g)[..., None] + hexc("#6F86C8") * (glint * 0.10)[..., None]
    mott = fbm(col.shape[0], col.shape[1], 30 * img.S, 3, 17)
    col = col * (0.95 + 0.08 * mott)[..., None]
    # the glaze: a broad soft sheen toward the light, as on fired enamel
    sheen = np.exp(-(((xx - lit[0]) / (dims[0] * 0.9)) ** 2 + ((yy - lit[1]) / (dims[1] * 0.9)) ** 2))
    col = screen(col, hexc("#9FB2E8") * (sheen * 0.06)[..., None])
    img.over(sl, col, cov)


def beads_line(img, x0, y0, x1, y1, r=2.5, pitch=6.0, depth_k=1.0):
    """A row of pearl beads (brass spheres) from (x0, y0) to (x1, y1)."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    half = math.hypot(x1 - x0, y1 - y0) / 2
    w = img.win(cx, cy, half + r + 2)
    if w is None:
        return
    sl, X, Y = w
    ux, uy = (x1 - x0) / (2 * half), (y1 - y0) / (2 * half)
    along = (X - x0) * ux + (Y - y0) * uy
    across = -(X - x0) * uy + (Y - y0) * ux
    n = max(1, int(round(2 * half / pitch)))
    p = 2 * half / n
    k = np.clip(np.round(along / p), 0, n)
    da = along - k * p
    d = np.sqrt(da * da + across * across)
    cov = img.cov(d - r)
    u, v = (da * ux - across * uy) / r, (da * uy + across * ux) / r
    nz = np.sqrt(np.clip(1 - u * u - v * v, 0, 1))
    col = brass_shade(u, v, nz * depth_k + (1 - depth_k), base=0.02)
    # the gap between beads is a dark groove
    img.mul(sl, hexc("#0A0806"), img.cov(np.abs(across) - r * 0.95) * (1 - cov) * 0.6)
    img.over(sl, col, cov)


def rosette(img, cx, cy, R=13.0, petals=8, cab=4.2):
    """A brass rosette with a moonstone cabochon: petals as raised lobes, a bead ring, the stone in a bezel."""
    def petal_sdf(X, Y):
        ang = np.arctan2(Y - cy, X - cx)
        rr = np.sqrt((X - cx) ** 2 + (Y - cy) ** 2)
        edge = R * (0.80 + 0.20 * np.abs(np.cos(ang * petals / 2)))
        return rr - edge
    draw_brass(img, petal_sdf, (cx, cy, R + 2), "round", depth=2.6, width=R * 0.45)
    ring = lambda X, Y: np.abs(sd_circle(X, Y, cx, cy, cab + 2.2)) - 0.9
    draw_brass(img, ring, (cx, cy, cab + 4), "round", depth=1.0, width=0.9, base=0.05)
    moonstone(img, cx, cy, cab)


def moonstone(img, cx, cy, r, tone=None):
    """A moonstone cabochon in the one light: a soft body, the inner blue sheen (adularescence) on the side toward
    the light, a crisp highlight."""
    w = img.win(cx, cy, r + 1.5)
    if w is None:
        return
    sl, X, Y = w
    u, v = (X - cx) / r, (Y - cy) / r
    d2 = u * u + v * v
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    lam = np.clip(u * L[0] + v * L[1] + nz * L[2], 0, 1)
    nh = np.clip(u * H_BLINN[0] + v * H_BLINN[1] + nz * H_BLINN[2], 0, 1)
    col = ramp(lam, [(0, P["moonstone_deep"]), (0.55, P["moonstone_mid"]), (1, P["moonstone_high"])])
    sheen = np.exp(-(((u + 0.25) ** 2 + (v + 0.2) ** 2) / 0.22)) * 0.35
    col = screen(col, hexc("#9CC0FF") * sheen[..., None]) + hexc(P["moonstone_spec"]) * (nh ** 70 * 0.9)[..., None]
    img.over(sl, np.clip(col, 0, 1), np.clip((1 - np.sqrt(d2)) * r * img.S + 0.5, 0, 1))


# ------------------------------------------------------------------------------------------------ the frame
def frame(img):
    """Rails of guilloche enamel, the outer moulding, pearl-beaded walls, corner rosettes and the pilasters."""
    S = img.S
    sl, xx, yy = img.full()
    board = (xx > WALL_L) & (xx < WALL_R) & (yy > TOP) & (yy < FOOT)
    rail = (~board).astype(np.float32)
    enamel_over_guilloche(img, sl, xx, yy, rail, "wave", dims=(800.0, 600.0))
    # the board's inner shade: the rails stand proud of the open night, so it darkens just inside them (top, left)
    sh = np.maximum(np.clip(1 - (yy - TOP) / 9, 0, 1), np.clip(1 - (xx - WALL_L) / 9, 0, 1)) * board
    img.mul(sl, hexc("#04060D"), sh * 0.55)
    # the outer moulding: a bevelled brass frame with an engraved hairline inside it
    outer = lambda X, Y: np.maximum(sd_rrect(X, Y, 0, 0, 800, 600, 4), -sd_rrect(X, Y, 6, 6, 794, 594, 2))
    draw_brass(img, outer, None, "round", depth=3.0, width=3.0)
    inner = lambda X, Y: np.abs(sd_rrect(X, Y, 9.5, 9.5, 790.5, 590.5, 2)) - 0.5
    sl2, X2, Y2 = img.full()
    img.over(sl2, hexc(P["gilt_mid"]), img.cov(inner(X2, Y2)) * 0.55 * (1 - board))
    # the walls: pearl beads along the board's left, right and top edges, set on a slim brass fillet
    fillet = lambda X, Y: np.maximum(sd_rrect(X, Y, WALL_L - 4.2, TOP - 4.2, WALL_R + 4.2, FOOT + 2, 2),
                                     -sd_rrect(X, Y, WALL_L - 0.6, TOP - 0.6, WALL_R + 0.6, FOOT + 30, 1))
    draw_brass(img, fillet, None, "round", depth=1.2, width=1.4, base=-0.10)
    beads_line(img, WALL_L - 2.3, TOP + 3, WALL_L - 2.3, FOOT - 3, r=2.2, pitch=5.6)
    beads_line(img, WALL_R + 2.3, TOP + 3, WALL_R + 2.3, FOOT - 3, r=2.2, pitch=5.6)
    beads_line(img, WALL_L + 3, TOP - 2.3, WALL_R - 3, TOP - 2.3, r=2.2, pitch=5.6)
    # the corners of the board opening and of the window: rosettes with moonstones
    for (cx, cy, R) in ((WALL_L - 2.5, TOP - 2.5, 7.5), (WALL_R + 2.5, TOP - 2.5, 7.5)):
        rosette(img, cx, cy, R=R, petals=8, cab=2.6)
    for (cx, cy) in ((20, 20), (780, 20)):
        rosette(img, cx, cy, R=10.5, petals=8, cab=3.4)
    # the side rails' lower halves: fluted pilasters, ending in rosettes at the foot
    pilaster_pair(img)


def pilaster_pair(img):
    for (x0, x1) in ((24.0, 52.0), (748.0, 776.0)):
        y0, y1 = 404.0, 566.0
        shaft = lambda X, Y, x0=x0, x1=x1: sd_rrect(X, Y, x0 + 3, y0 + 9, x1 - 3, y1 - 9, 1.5)
        sl, X, Y = img.win((x0 + x1) / 2, (y0 + y1) / 2, (y1 - y0) / 2 + 8)
        sd = shaft(X, Y)
        cov = img.cov(sd)
        w = (x1 - x0 - 6)
        fx = ((X - (x0 + 3)) / w) * 4
        fr = fx - np.floor(fx) - 0.5
        hgt = (1.0 - np.cos(fr * math.pi * 1.0)) * 0.5 * np.clip(-sd / 1.0, 0, 1) * 1.4
        hgt = 1.4 - hgt
        nx, ny, nz = normals_from_height(hgt, img.S)
        img.over(sl, brass_shade(nx, ny, nz, base=-0.06), cov)
        for (yc, hh) in ((y0 + 4.5, 4.5), (y1 - 4.5, 4.5)):
            cap = lambda X, Y, yc=yc, hh=hh, x0=x0, x1=x1: sd_rrect(X, Y, x0, yc - hh, x1, yc + hh, 2.0)
            draw_brass(img, cap, ((x0 + x1) / 2, yc, (x1 - x0) / 2 + 3), "round", depth=2.0, width=2.2)
        rosette(img, (x0 + x1) / 2, y1 + 13, R=8.5, petals=8, cab=2.8)


# ------------------------------------------------------------------------------------------------ the top rail
def cartouche(img, x0, x1, y0, y1):
    """An enamel plate with a gilt bead frame and scrolled ends: where the level's name sits."""
    cy = (y0 + y1) / 2
    body = lambda X, Y: sd_rrect(X, Y, x0, y0, x1, y1, (y1 - y0) / 2)
    sl, X, Y = img.win((x0 + x1) / 2, cy, (x1 - x0) / 2 + 6)
    cov = img.cov(body(X, Y))
    img.over(sl, ramp(np.clip((Y - y0) / (y1 - y0), 0, 1), [(0, "#0C1330"), (1, "#070B1C")]), cov)
    img.add(sl, hexc("#5A70B8"), cov * np.exp(-((Y - y0 - 2) / 2.4) ** 2) * 0.10)
    rim = lambda X, Y: np.abs(body(X, Y)) - 1.1
    draw_brass(img, rim, ((x0 + x1) / 2, cy, (x1 - x0) / 2 + 4), "round", depth=1.2, width=1.1)


def escutcheon(img, cx=400.0):
    """The engraved brass plate the launcher's yoke hangs from: a pointed oval with scrolled wings and an engraved
    border, a moonstone at its centre."""
    def plate(X, Y):
        e = np.sqrt(((X - cx) / 36.0) ** 2 + ((Y - 19.0) / 15.0) ** 2) - 1.0
        wing = np.minimum(sd_circle(X, Y, cx - 40, 21, 7.0), sd_circle(X, Y, cx + 40, 21, 7.0))
        return np.minimum(e * 13.0, wing)
    sl, X, Y = img.win(cx, 20, 52)
    sd = plate(X, Y)
    cov = img.cov(sd)
    hgt = np.clip(-sd / 3.0, 0, 1) * 2.2
    nx, ny, nz = normals_from_height(hgt, img.S)
    img.over(sl, brass_shade(nx, ny, nz, base=0.0), cov)
    border = np.abs(sd + 2.6) - 0.35
    img.mul(sl, hexc("#3A2C12"), img.cov(border) * 0.7)
    for sx in (-1, 1):
        img.mul(sl, hexc("#3A2C12"), img.cov(np.abs(sd_circle(X, Y, cx + sx * 40, 21, 3.6)) - 0.4) * 0.6)
    moonstone(img, cx, 17.5, 3.6)


def score_window(img, x0, x1, y0, y1):
    """A recessed window of dark glass in a brass bezel: the score's counter."""
    body = lambda X, Y: sd_rrect(X, Y, x0, y0, x1, y1, 4.0)
    sl, X, Y = img.win((x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2 + 6)
    cov = img.cov(body(X, Y))
    img.over(sl, ramp(np.clip((Y - y0) / (y1 - y0), 0, 1), [(0, "#04060E"), (1, "#0B1128")]), cov)
    # the inner shadow along its top and left (the bezel stands proud of the glass)
    inner = np.clip(1 - (Y - y0) / 4.0, 0, 1) + np.clip(1 - (X - x0) / 4.0, 0, 1)
    img.mul(sl, hexc("#000000"), np.clip(inner, 0, 1) * cov * 0.5)
    # the glass: a faint reflection band toward the light
    img.add(sl, hexc("#8FA4DA"), cov * np.exp(-((Y - y0 - 3.5) / 1.6) ** 2) * np.clip(1 - (X - x0) / (x1 - x0), 0, 1) * 0.12)
    rim = lambda X, Y: np.abs(body(X, Y)) - 1.3
    draw_brass(img, rim, ((x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2 + 4), "round", depth=1.4, width=1.3)


# ------------------------------------------------------------------------------------------------ the instruments
def ball_tube(img, balls=6, new_ball=False):
    """The ball channel: a glass tube in a three-strut brass cage, engraved gradations at each ball's place, acorn
    finials at the ends, the count on an engraved plate below."""
    x0, x1, y0, y1 = 25.0, 51.0, 66.0, 330.0
    cx = 38.0
    sl, xx, yy = img.win(cx, (y0 + y1) / 2, (y1 - y0) / 2 + 22)
    tube = img.cov(sd_rrect(xx, yy, x0, y0, x1, y1, 12))
    # clear glass: the rail's guilloche enamel shows through, darkened (the tube's shade inside) and a little more at
    # the walls, where the glass is seen edge-on
    across = (xx - x0) / (x1 - x0)
    wall = np.clip(np.abs(across - 0.5) * 2, 0, 1) ** 3
    img.mul(sl, hexc("#05070F"), tube * (0.30 + 0.35 * wall))
    for i in range(balls):
        yb = y1 - 13 - i * 25.5
        draw_ball(img, cx, yb, r=10.5)
        if i > 0:
            slc, Xc, Yc = img.win(cx, yb + 12.75, 8)
            img.mul(slc, hexc("#05070F"), np.exp(-(((Xc - cx) / 4.5) ** 2 + ((Yc - (yb + 12.75)) / 2.2) ** 2)) * 0.55)
    sl, xx, yy = img.win(cx, (y0 + y1) / 2, (y1 - y0) / 2 + 22)
    tube = img.cov(sd_rrect(xx, yy, x0, y0, x1, y1, 12))
    across = (xx - x0) / (x1 - x0)
    # the front surface's reflections, over the balls: a crisp streak on the upper-left side, a faint one on the
    # right, and thin bright lines where the glass is seen edge-on
    img.add(sl, hexc("#F4F2EA"), tube * np.exp(-((across - 0.21) / 0.028) ** 2) * 0.55)
    img.add(sl, hexc("#F4F2EA"), tube * np.exp(-((across - 0.27) / 0.06) ** 2) * 0.10)
    img.add(sl, hexc("#8FA4DA"), tube * np.exp(-((across - 0.86) / 0.03) ** 2) * 0.22)
    edge_l = np.exp(-((across - 0.04) / 0.025) ** 2) + np.exp(-((across - 0.96) / 0.025) ** 2)
    img.add(sl, hexc("#C3CEE4"), tube * edge_l * 0.30)
    # the cage: three struts (two at the sides, one at the back is hidden), with engraved gradations between balls
    for sx in (x0 - 1.2, x1 + 1.2):
        strut = lambda X, Y, sx=sx: sd_rrect(X, Y, sx - 1.5, y0 + 2, sx + 1.5, y1 - 2, 1.4)
        draw_brass(img, strut, (sx, (y0 + y1) / 2, (y1 - y0) / 2 + 3), "round", depth=1.4, width=1.5)
    for i in range(1, 10):
        yg = y1 - i * 25.5 + 0.0
        if yg < y0 + 8:
            break
        for sx in (x0 - 1.2, x1 + 1.2):
            tk = lambda X, Y, sx=sx, yg=yg: sd_rrect(X, Y, sx - 3.2, yg - 0.7, sx + 3.2, yg + 0.7, 0.6)
            draw_brass(img, tk, (sx, yg, 5), "round", depth=0.7, width=0.7, base=0.05)
    # acorn finials
    for (yc, s) in ((y0 - 6, -1), (y1 + 6, 1)):
        cap = lambda X, Y, yc=yc: sd_rrect(X, Y, x0 - 4.5, yc - 4.5, x1 + 4.5, yc + 4.5, 4.2)
        draw_brass(img, cap, (cx, yc, 20), "round", depth=2.4, width=3.0)
        knob = lambda X, Y, yc=yc, s=s: sd_circle(X, Y, cx, yc + s * 9.5, 4.2)
        draw_brass(img, knob, (cx, yc + s * 9.5, 6), "round", depth=2.2, width=3.6)
        moonstone(img, cx, yc, 2.6)
    if new_ball:
        yb = y1 - 13 - (balls - 1) * 25.5
        slb, Xb, Yb = img.win(cx, yb, 26)
        db = np.sqrt((Xb - cx) ** 2 + (Yb - yb) ** 2)
        img.add(slb, hexc("#C3CEE4"), np.exp(-(db / 13) ** 2) * 0.35)
        text(img, 58, yb - 14, "+1", "ui_sb", 11, P["cream"], anchor="lm", halo=0.6)
    plate(img, 38, 362, 46, 34)
    text(img, 38, 356, str(balls), "ui_sb", 17, P["cream"], anchor="mm", halo=0)
    text(img, 38, 373.5, "Balls", "ui_sb", 10, P["gilt_high"], anchor="mm", halo=0)


def plate(img, cx, cy, w, h):
    """A small engraved brass-framed enamel plate (for a count and its label)."""
    body = lambda X, Y: sd_rrect(X, Y, cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2, 5.0)
    sl, X, Y = img.win(cx, cy, max(w, h) / 2 + 5)
    cov = img.cov(body(X, Y))
    img.over(sl, ramp(np.clip((Y - cy + h / 2) / h, 0, 1), [(0, "#0D1434"), (1, "#080C20")]), cov)
    rim = lambda X, Y: np.abs(body(X, Y)) - 1.0
    draw_brass(img, rim, (cx, cy, max(w, h) / 2 + 3), "round", depth=1.1, width=1.0)


def mult_dial(img, cleared=11, mult="×2"):
    """The multiplier dial: a sunburst-engraved enamel face, a gilt arc for the oranges cleared in a groove, engraved
    ticks at x2, x3, x5 and x10, a knurled bezel."""
    cx, cy, R = 762.0, 100.0, 25.0
    sl, xx, yy = img.win(cx, cy, R + 8)
    face = img.cov(sd_circle(xx, yy, cx, cy, R + 3.5))
    enamel_over_guilloche(img, sl, xx, yy, face, "sun", cx, cy, lit=(cx - R, cy - R), dims=(2 * R, 2 * R),
                          tint0="#25386F", tint1="#0F1636")
    rr = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    ang = (np.degrees(np.arctan2(yy - cy, xx - cx)) + 90) % 360
    groove = np.clip(0.5 - (np.abs(rr - R) - 2.6) * img.S, 0, 1)
    img.over(sl, hexc("#070B1E"), groove)
    frac = cleared / 25.0
    arc = groove * smooth(frac * 360 + 0.8, frac * 360 - 0.8, ang) * np.clip(0.5 - (np.abs(rr - R) - 1.9) * img.S, 0, 1)
    nx_, ny_ = (xx - cx) / np.maximum(rr, 1e-3), (yy - cy) / np.maximum(rr, 1e-3)
    across = np.clip((rr - (R - 1.9)) / 3.8, 0, 1)
    nzz = np.sqrt(np.clip(1 - (across * 2 - 1) ** 2, 0, 1))
    img.over(sl, brass_shade(nx_ * (across * 2 - 1), ny_ * (across * 2 - 1), nzz, base=0.05), arc)
    for n_left in (15, 10, 6, 3):
        a = math.radians((25 - n_left) / 25 * 360 - 90)
        tk = lambda X, Y, a=a: sd_segment(X, Y, cx + math.cos(a) * (R - 4.6), cy + math.sin(a) * (R - 4.6),
                                          cx + math.cos(a) * (R + 4.6), cy + math.sin(a) * (R + 4.6))[0] - 0.7
        draw_brass(img, tk, (cx + math.cos(a) * R, cy + math.sin(a) * R, 7), "round", depth=0.7, width=0.7)
    # the knurled bezel
    def bezel(X, Y):
        a_ = np.arctan2(Y - cy, X - cx)
        rr_ = np.sqrt((X - cx) ** 2 + (Y - cy) ** 2)
        return np.abs(rr_ - (R + 5.6)) - (2.0 + 0.35 * np.cos(a_ * 60))
    draw_brass(img, bezel, (cx, cy, R + 9), "round", depth=1.6, width=1.8)
    text(img, cx, cy + 1, mult, "ui_sb", 15, P["cream"], anchor="mm", halo=0.35)


def oranges_left(img, n=14):
    draw_moon(img, 747, 152, 6.5, "orange", sky=P["enamel"])
    text(img, 759, 152.5, str(n), "ui_sb", 13, P["cream"], anchor="lm", halo=0)
    text(img, 762, 171, "Oranges", "ui_sb", 10, P["gilt_high"], anchor="mm", halo=0)


def power_medallion(img, portrait=None, power="Super Guide", turns=2, name=None):
    """The carrier's portrait in a medallion: a bevelled brass bezel with a bead ring, the portrait under glass, the
    power's name on a small ribbon plate, and the turns left as three moonstones (dim when spent)."""
    mx, my, mr = 762.0, 240.0, 25.0
    sl, xx, yy = img.win(mx, my, mr + 8)
    disc = img.cov(sd_circle(xx, yy, mx, my, mr))
    img.over(sl, ramp(np.clip((yy - my + mr) / (2 * mr), 0, 1), [(0, "#2A3A72"), (1, "#121A3E")]), disc)
    if portrait is not None:
        portrait(img, mx, my, mr)
    # glass: a soft reflection toward the light, a darker lower-right limb
    rr = np.sqrt((xx - mx) ** 2 + (yy - my) ** 2) / mr
    img.add(sl, hexc("#C9D6F4"), disc * np.exp(-(((xx - mx + 9) ** 2 + (yy - my + 11) ** 2) / 70)) * 0.16)
    img.mul(sl, hexc("#05070F"), disc * smooth(0.75, 1.0, rr) * 0.35)
    bez = lambda X, Y: np.abs(sd_circle(X, Y, mx, my, mr + 2.4)) - 2.4
    draw_brass(img, bez, (mx, my, mr + 6), "bevel", depth=2.2, width=2.4)
    ring_beads(img, mx, my, mr + 6.6, 30, 1.25)
    plate(img, mx, my + mr + 16, 58, 15)
    text(img, mx, my + mr + 16.5, power, "ui_sb", 10, P["cream"], anchor="mm", halo=0)
    for i in range(3):
        pxp = mx - 11 + i * 11
        if i < turns:
            moonstone(img, pxp, my + mr + 33, 3.0)
        else:
            s2, X2, Y2 = img.win(pxp, my + mr + 33, 4.5)
            img.over(s2, hexc("#0A0F22"), img.cov(sd_circle(X2, Y2, pxp, my + mr + 33, 3.0)))
        rim = lambda X, Y, pxp=pxp: np.abs(sd_circle(X, Y, pxp, my + mr + 33, 3.7)) - 0.7
        draw_brass(img, rim, (pxp, my + mr + 33, 5.5), "round", depth=0.8, width=0.7)


def ring_beads(img, cx, cy, R, n, r):
    for k in range(n):
        a = 2 * math.pi * k / n
        bx, by = cx + R * math.cos(a), cy + R * math.sin(a)
        s, X, Y = img.win(bx, by, r + 1)
        u, v = (X - bx) / r, (Y - by) / r
        d2 = u * u + v * v
        nz = np.sqrt(np.clip(1 - d2, 0, 1))
        img.over(s, brass_shade(u, v, nz, base=0.03), np.clip((1 - np.sqrt(d2)) * r * img.S + 0.5, 0, 1))


def top_rail(img, stage="1-3", name="Lantern Steps", score="128,450"):
    cartouche(img, 84, 268, 9, 33)
    roundel = lambda X, Y: sd_circle(X, Y, 100, 21, 10.5)
    sl, X, Y = img.win(100, 21, 13)
    img.over(sl, hexc("#0A1028"), img.cov(roundel(X, Y)))
    draw_brass(img, lambda X, Y: np.abs(roundel(X, Y)) - 1.4, (100, 21, 13), "round", depth=1.4, width=1.4)
    text(img, 100, 21.5, stage, "ui_sb", 9.5, P["gilt_high"], anchor="mm", halo=0)
    text(img, 118, 21.5, name, "serif", 13.5, P["cream"], anchor="lm", halo=0)
    escutcheon(img)
    score_window(img, 600, 716, 9, 33)
    text(img, 611, 21.5, "Score", "ui_sb", 10, P["gilt_high"], anchor="lm", halo=0)
    text(img, 708, 21.5, score, "ui_sb", 16, P["cream"], anchor="rm", halo=0)


# ------------------------------------------------------------------------------------------------ the buckets
def road_strip(img):
    """The moon road along the foot: a pale packed-earth road, moonlit, its far edge softened by grass."""
    S = img.S
    sl, xx, yy = img.full()
    top = 586.0
    m = np.clip((yy - top) * S + 0.5, 0, 1) * ((xx > WALL_L) & (xx < WALL_R))
    n = fbm(img.h, img.w, 3.0 * S, 3, 77)
    col = ramp(np.clip((yy - top) / 8, 0, 1), [(0, "#5A6488"), (1, "#3A4266")]) * (0.88 + 0.22 * n)[..., None]
    ruts = np.exp(-((yy - 590.5) / 0.8) ** 2) * 0.10
    col = col * (1 - ruts)[..., None]
    img.over(sl, col, m)
    # the grass edge: tufts rim-lit by the moon on their upper-left
    g = fbm(1, img.w, 2.0 * S, 2, 5)[0]
    gtop = top - 1.2 - 2.4 * g
    gm = np.clip((yy - gtop[None, :]) * S + 0.5, 0, 1) * np.clip((top + 1.5 - yy) * S + 0.5, 0, 1) * ((xx > WALL_L) & (xx < WALL_R))
    img.over(sl, hexc("#141B30"), gm * 0.9)
    img.add(sl, hexc("#7F92C8"), gm * np.exp(-((yy - gtop[None, :]) / 0.5) ** 2) * 0.35)


def bucket_cart(img, bx):
    """The base campaign's bucket (proposal C): a small lantern cart on the moon road. A deep open box of dark planks
    with brass corner straps (its mouth is the catch: 104 wide inside, 131 across the rails), two spoked wheels on the
    road, and a paper lantern on a curved rear post: the playfield's one warm light, as on the boat."""
    S = img.S
    half, inner = 65.5, 52.0
    rim_y, floor_y = 573.0, 583.0
    road_strip(img)
    # the box: two side boards (the rims), the front board between them, seen square on
    def box(X, Y):
        return sd_rrect(X, Y, bx - half, rim_y, bx + half, floor_y + 1, 2.0)
    sl, X, Y = img.win(bx, 578, 80)
    cov = img.cov(box(X, Y))
    depth = np.clip((Y - rim_y) / (floor_y - rim_y), 0, 1)
    wood = ramp(depth, [(0, "#3A2C30"), (1, "#16101A")])
    grainw = fbm(cov.shape[0], cov.shape[1], 6 * S, 3, 23)
    planks = 1 - 0.18 * np.exp(-(((Y - rim_y) % 3.4) - 0.2) ** 2 / 0.05)
    wood = wood * (0.9 + 0.15 * grainw)[..., None] * planks[..., None]
    img.over(sl, wood, cov)
    # the dark mouth's inner shadow (the catch): the top board's lip, seen from the front
    lip = img.cov(np.maximum(np.abs(Y - rim_y - 0.8) - 0.8, np.abs(X - bx) - half + 1))
    img.add(sl, hexc("#9FB0DA"), lip * (0.40 - 0.25 * np.clip((X - bx + half) / (2 * half), 0, 1)))
    # brass corner straps and nail heads
    for sx in (-1, 1):
        strap = lambda X_, Y_, sx=sx: sd_rrect(X_, Y_, bx + sx * (half - 3.0) - 3.0, rim_y - 0.5, bx + sx * (half - 3.0) + 3.0, floor_y + 1, 1.2)
        draw_brass(img, strap, (bx + sx * (half - 3), 578, 9), "round", depth=1.0, width=1.2, base=-0.04)
        for ny in (rim_y + 2.5, floor_y - 2.0):
            nail = lambda X_, Y_, sx=sx, ny=ny: sd_circle(X_, Y_, bx + sx * (half - 3.0), ny, 0.9)
            draw_brass(img, nail, (bx + sx * (half - 3), ny, 2), "round", depth=0.6, width=0.8)
    # wheels: two spoked wooden wheels with iron tyres, resting on the road
    for wx in (bx - 40.0, bx + 40.0):
        wy, wr = 587.5, 6.5
        s2, X2, Y2 = img.win(wx, wy, wr + 2)
        rr = np.sqrt((X2 - wx) ** 2 + (Y2 - wy) ** 2)
        tyre = img.cov(np.abs(rr - wr + 0.9) - 0.9)
        img.over(s2, hexc("#151318"), tyre)
        img.add(s2, hexc("#8FA4DA"), tyre * np.clip(-((X2 - wx) * 0.7 + (Y2 - wy) * 0.7) / wr, 0, 1) * 0.45)
        ang = np.arctan2(Y2 - wy, X2 - wx)
        spokes = img.cov(np.abs(np.sin(ang * 3)) * rr - 0.55) * (rr < wr - 1.5)
        img.over(s2, hexc("#2A2024"), spokes)
        hub = lambda X_, Y_, wx=wx, wy=wy: sd_circle(X_, Y_, wx, wy, 1.6)
        draw_brass(img, hub, (wx, wy, 3), "round", depth=0.8, width=1.2)
    # the rear post and the lantern (same lantern and light as the boat's)
    lx, ly = bx + 50.0, 541.0
    def post(X_, Y_):
        t = np.clip((573 - Y_) / 30.0, 0, 1)
        cxp = bx + 57 - 6 * t ** 2
        return np.maximum(np.abs(X_ - cxp) - 1.7, np.maximum(Y_ - 574, 535 - Y_))
    s3, X3, Y3 = img.win(bx + 54, 552, 26)
    img.over(s3, hexc("#241A1C"), img.cov(post(X3, Y3)))
    arm = lambda X_, Y_: sd_segment(X_, Y_, bx + 51, 535.5, lx, 535.5)[0] - 0.8
    draw_brass(img, arm, (lx + 3, 536, 8), "round", depth=0.6, width=0.8)
    s4, X4, Y4 = img.win(lx, ly, 70)
    body = img.cov(sd_rrect(X4, Y4, lx - 4.6, ly - 6.0, lx + 4.6, ly + 6.0, 3.2))
    acr = np.clip(1 - ((X4 - lx) / 4.6) ** 2, 0, 1)
    paper = ramp(acr, [(0, "#B4602A"), (0.6, "#F2B060"), (1, "#FFE6B0")])
    ribs = 1 - 0.18 * np.exp(-(((Y4 - ly + 6) % 3.0) - 1.5) ** 2 / 0.12)
    img.over(s4, paper * ribs[..., None], body)
    for (yc, w_) in ((ly - 6.8, 3.6), (ly + 6.8, 3.2)):
        cap = lambda X_, Y_, yc=yc, w_=w_: sd_rrect(X_, Y_, lx - w_, yc - 1.0, lx + w_, yc + 1.0, 0.8)
        draw_brass(img, cap, (lx, yc, 6), "round", depth=0.8, width=0.9)
    d4 = np.sqrt((X4 - lx) ** 2 + (Y4 - ly) ** 2)
    img.add(s4, hexc("#FFB868"), np.exp(-(d4 / 9) ** 2) * 0.40 + np.exp(-(d4 / 24) ** 2) * 0.10)
    # its warmth: the post's facing side, the box's rear corner, and a pool on the road under it
    s5, X5, Y5 = img.win(bx + 20, 575, 90)
    d5 = np.sqrt((X5 - lx) ** 2 + (Y5 - ly) ** 2)
    fall = 1 / (1 + (d5 / 14) ** 2) ** 1.5
    pm5 = img.cov(post(X5, Y5))
    facing = pm5 * (1 - img.cov(post(X5 - 1.4, Y5)))
    img.add(s5, hexc("#FFB060"), (pm5 * 0.12 + facing * 0.9) * fall)
    boxm = img.cov(box(X5, Y5))
    fall2 = 1 / (1 + (d5 / 22) ** 2) ** 1.5
    img.add(s5, hexc("#FFB060"), boxm * fall2 * 0.55)
    gl = np.exp(-(((X5 - (bx + half - 3)) / 2.2) ** 2 + ((Y5 - (rim_y + 0.4)) / 0.9) ** 2))
    img.add(s5, hexc("#FFD9A0"), gl * 0.45)
    pool = np.exp(-(((X5 - lx) / 26) ** 2 + ((Y5 - 590) / 3.2) ** 2)) * (Y5 > 586) * (1 - boxm)
    img.add(s5, hexc("#FFB060"), pool * 0.22)
    # the box's contact with the road: a soft dark line under it (the cart stands on its wheels; the box clears the
    # road by a hair, so only an occlusion line)
    s6, X6, Y6 = img.win(bx, 586, 72)
    img.mul(s6, hexc("#05070F"), np.exp(-((Y6 - 586.6) / 0.9) ** 2) * (np.abs(X6 - bx) < half - 4) * 0.45)


def playfield_chrome(img, level=None, bucket="boat", bucket_x=BUCKET_X_DEFAULT, aim=None, hud_kw=None):
    hud_kw = dict(hud_kw or {})
    if bucket == "boat":
        pf.bucket_boat(img, bucket_x)
    elif bucket == "cart":
        bucket_cart(img, bucket_x)
    elif bucket == "fever":
        import fever as v9_fever
        v9_fever.fever_cups(img)
        # round 4 (realism Nit): each cup's rim catches the moon on its upper-left arc
        w = (WALL_R - WALL_L) / 5
        for i in range(5):
            cx, rx, ry, rim_y = WALL_L + w * (i + 0.5), w / 2 - 5, 5.0, 566.0
            sl, X, Y = img.win(cx, rim_y, rx + 4)
            e = np.sqrt(((X - cx) / rx) ** 2 + ((Y - rim_y) / ry) ** 2)
            band = np.exp(-((e - 0.95) / 0.045) ** 2)
            ang = np.arctan2((Y - rim_y) / ry, (X - cx) / rx)
            arc = np.clip(-np.cos(ang + math.pi / 4), 0, 1) ** 2       # strongest at the upper left of the rim
            img.add(sl, hexc(P["gilt_spec"]), band * arc * 0.55)
    frame(img)
    a = aim if aim is not None else hud_kw.pop("aim", 14.0)
    aimed = pf.launcher(img, aim_deg=a, gauge=hud_kw.pop("gauge", 0.35), ball=hud_kw.pop("ball", True))
    top_rail(img, hud_kw.pop("stage", "1-3"), hud_kw.pop("name", level.get("name", "") if level else ""),
             hud_kw.pop("score", "128,450"))
    ball_tube(img, hud_kw.pop("balls", 6), hud_kw.pop("new_ball", False))
    mult_dial(img, hud_kw.pop("cleared", 11), hud_kw.pop("mult", "×2"))
    oranges_left(img, hud_kw.pop("oranges", 14))
    power_medallion(img, hud_kw.pop("portrait", None), hud_kw.pop("power", "Super Guide"), hud_kw.pop("turns", 2))
    return aimed
