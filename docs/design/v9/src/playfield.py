"""Moonfall playfield parts (plan v9 G8), in the engine's 800 x 600 units: the night sky, the Medallion brass frame and
rails, the launcher (a brass telescope), the two bucket proposals (A: a crescent cradle on a rail; B: a lantern boat
on a strip of water), the HUD (score, balls, multiplier dial, the power's medallion) and a sample level.

Geometry follows docs/research/plan-v9/peg-measurements.md: walls at x 75 and 725 (ball centre 81.5..718.5), the
launcher pivot at (400, 87) with the ball leaving 73 from it, the bucket's mouth 104 and rim 131 wide with its rim
top at y 573, pegs r 10 (collision radius sum 16.7 with the ball's 6), bricks about 30 x 20.
"""
import math

import numpy as np

from mf_lib import (L, P, Img, blur, brass_shade, draw_ball, draw_brass, draw_brick, draw_moon, enamel, fbm, hexc,
                    normals_from_height, ramp, screen, sd_circle, sd_rrect, sd_segment, sector_brick, smooth, text)

WALL_L, WALL_R, TOP = 75.0, 725.0, 41.0
PIVOT = (400.0, 87.0)
BARREL = 73.0
RIM_Y = 573.0
SKY_AT_PEGS = "#141C3A"


# ------------------------------------------------------------------------------------------------ the night sky
def sky(img, seed=5, fever=0.0):
    """Tsukimichi's night sky behind the board: zenith to horizon in the Night scene's blues, the glow of the moon
    that stands just off the frame at the upper left (the one light), a few small stars that thin out in that glow,
    thin high cloud, and two far ridges at the foot, backlit by nothing, lit on their upper-left faces."""
    sl, xx, yy = img.full()
    t = np.clip(yy / 600.0, 0, 1)
    col = ramp(t, [(0, "#070B1E"), (0.35, "#0F1738"), (0.75, "#18234C"), (1.0, "#22305E")])
    img.px[sl] = col
    # the moon's glow, from just beyond the upper-left corner
    d = np.sqrt((xx + 60) ** 2 + (yy + 70) ** 2)
    img.add(sl, hexc("#B9C8F0"), np.exp(-(d / 380) ** 2) * 0.20 + np.exp(-(d / 900) ** 2) * 0.06)
    # thin high cloud, lit on the edges that face the moon
    cl = fbm(img.h, img.w, 90 * img.S, 5, seed)
    band = np.exp(-((yy - 205) / 70) ** 2) * 0.8 + np.exp(-((yy - 395) / 50) ** 2) * 0.5
    dens = np.clip((cl - 0.52) * 3.0, 0, 1) * band
    stretch = blur(dens, 2 * img.S)
    gy, gx = np.gradient(stretch)
    litc = np.clip(-(gx * -0.7 + gy * -0.7) * 40 / img.S, 0, 1)
    img.over(sl, hexc("#26335E"), stretch * 0.35)
    img.add(sl, hexc("#7F92C6"), litc * stretch * 0.25)
    # stars: small, few, fainter in the moon's glow and toward the horizon; none larger than a ball's highlight
    rng = np.random.default_rng(seed + 1)
    for _ in range(150):
        x, y = rng.uniform(WALL_L, WALL_R), rng.uniform(TOP, 470)
        dm = math.hypot(x + 60, y + 70)
        a = (0.15 + 0.6 * rng.random() ** 2.5) * min(1.0, max(0.0, (dm - 300) / 400)) * (1 - y / 600)
        r = 0.45 + 0.35 * rng.random()
        c = hexc("#DCE5FF" if rng.random() < 0.8 else "#FFE9C4")
        w = img.win(x, y, 3)
        if w is None or a <= 0.01:
            continue
        s2, X, Y = w
        img.add(s2, c, np.exp(-((X - x) ** 2 + (Y - y) ** 2) / (2 * r * r)) * a)
    # two far ridges at the foot: the farther paler (haze), both lit only on slopes that face the upper left
    xs = (np.arange(img.w) + 0.5) / img.S
    for (base, amp, cell, sd, top, bot, lift) in ((518, 46, 140, 11, "#1E2B58", "#1A2650", 0.16), (552, 30, 90, 12, "#121A3A", "#0D1430", 0.10)):
        n = np.interp(xs, np.linspace(0, 800, 40), np.random.default_rng(sd).random(40))
        n = blur(np.repeat(n[None, :], 3, 0), cell * img.S * 0.18)[1]
        ridge = base - amp * n
        m = np.clip((yy - ridge[None, :]) * img.S + 0.5, 0, 1)
        rc = ramp(np.clip((yy - ridge.min()) / 80, 0, 1), [(0, top), (1, bot)])
        slope = np.gradient(ridge) * img.S
        face = np.clip(-slope * 1.4, 0, 1)[None, :] * np.exp(-np.clip(yy - ridge[None, :], 0, None) / 14)
        rc = rc + (hexc("#5A6CA4") - rc) * (face * lift * 2.0)[..., None]
        img.over(sl, rc, m)
    img.add(sl, hexc("#3A4C84"), np.exp(-((yy - 548) / 9) ** 2) * 0.10)       # low mist between the ridges
    if fever:
        img.mul(sl, hexc("#0A0E22"), np.full(img.px.shape[:2], 0.35 * fever, np.float32))


# ------------------------------------------------------------------------------------------------ frame and rails
def frame(img):
    """The Medallion frame: a brass bead round the window, enamel rails (top 0..41, left 0..75, right 725..800) with a
    brass bead on their board edges, and the Medallion corner marks, lit at the top left and shaded at the bottom right."""
    sl, xx, yy = img.full()
    rail = (sd_rrect(xx, yy, 0, 0, 800, 600) < 0) & ~((xx > WALL_L) & (xx < WALL_R) & (yy > TOP))
    col = enamel(sl, xx, yy, img, 0, 0, 800, 600)
    img.over(sl, col, rail.astype(np.float32))
    # contact shade on the board just inside the beads (the rails stand proud of the sky)
    inner = sd_rrect(xx, yy, WALL_L, TOP, WALL_R, 600)
    img.mul(sl, hexc("#05070F"), np.clip(1 + inner / 7, 0, 1) * 0.55 * (inner < 0))
    # beads: the outer frame, then the rails' board edges (the side walls and the top rail's foot)
    outer = lambda X, Y: np.maximum(sd_rrect(X, Y, 0, 0, 800, 600, 4), -sd_rrect(X, Y, 6, 6, 794, 594, 2))
    draw_brass(img, outer, None, "round", depth=3.0, width=3.0)
    edge = lambda X, Y: np.maximum(sd_rrect(X, Y, WALL_L - 3.5, TOP - 3.5, WALL_R + 3.5, 597, 3), -sd_rrect(X, Y, WALL_L, TOP, WALL_R, 640, 1))
    draw_brass(img, edge, None, "round", depth=2.0, width=1.8)
    # the corner marks of the Medallion kit, on the board opening
    for (cx, cy, sx, sy) in ((WALL_L + 2, TOP + 2, 1, 1), (WALL_R - 2, 594, -1, -1)):
        def cm(X, Y, cx=cx, cy=cy, sx=sx, sy=sy):
            a = sd_rrect(X, Y, min(cx, cx + 16 * sx), min(cy, cy + 3 * sy), max(cx, cx + 16 * sx), max(cy, cy + 3 * sy), 1)
            b = sd_rrect(X, Y, min(cx, cx + 3 * sx), min(cy, cy + 16 * sy), max(cx, cx + 3 * sx), max(cy, cy + 16 * sy), 1)
            return np.minimum(a, b)
        draw_brass(img, cm, (cx + 8 * sx, cy + 8 * sy, 14), "round", depth=1.5, width=1.4, base=0.06 if sx > 0 else -0.04)


# ------------------------------------------------------------------------------------------------ the launcher
def cylinder(img, px_, py_, dx, dy, s0, s1, r0, r1, base=0.0):
    """A brass cylinder along (dx, dy) from s0 to s1 units off the pivot, radius r0 to r1, with flat ends: its
    height comes from the distance to its axis only, so it shades as a turned tube, not a string of beads."""
    cx, cy = px_ + dx * (s0 + s1) / 2, py_ + dy * (s0 + s1) / 2
    w = img.win(cx, cy, (s1 - s0) / 2 + max(r0, r1) + 2)
    if w is None:
        return
    sl, X, Y = w
    along = (X - px_) * dx + (Y - py_) * dy
    across = (X - px_) * dy - (Y - py_) * dx
    t = np.clip((along - s0) / max(1e-3, s1 - s0), 0, 1)
    rad = r0 + (r1 - r0) * t
    sd = np.maximum(np.abs(across) - rad, np.maximum(s0 - along, along - s1))
    cov = img.cov(sd)
    q = np.clip(across / rad, -1, 1)
    nzz = np.sqrt(np.clip(1 - q * q, 0, 1))
    nx_, ny_ = q * dy, -q * dx                                                         # the normal turns round the axis
    col = brass_shade(nx_, ny_, nzz, base=base)
    img.over(sl, col, cov)
    # a hairline where each piece meets the next (an end face), darker on the side away from the light
    seam = np.exp(-((along - s1) * img.S / 0.8) ** 2) * (np.abs(across) < rad)
    img.mul(sl, hexc("#1E1608"), seam * 0.6)


def launcher(img, aim_deg=-18.0, gauge=0.4, ball=True, flare=None):
    """A brass telescope on an armillary hub: the hub hangs from the top rail on a short yoke; the tube swings
    +/-81 degrees round the pivot; a crescent glass channel above the hub fills with moonlight toward the next free
    ball (notches at 25k, 75k and 125k of the shot's score)."""
    px_, py_ = PIVOT
    # the yoke: two arms from a foot plate on the rail, meeting the hub's sides
    for sx in (-1, 1):
        arm = lambda X, Y, sx=sx: sd_segment(X, Y, px_ + sx * 15, 40, px_ + sx * 13, py_ - 4)[0] - 2.2
        draw_brass(img, arm, (px_ + sx * 14, 62, 28), "round", depth=2.0, width=2.2)
    foot = lambda X, Y: sd_rrect(X, Y, px_ - 21, 37.5, px_ + 21, 43, 2.5)
    draw_brass(img, foot, (px_, 40, 24), "round", depth=1.8, width=2.6)
    # the crescent gauge: an arc channel above the hub, from 200 to 340 degrees (screen angles, y down)
    sl, xx, yy = img.win(px_, py_, 34)
    ang = np.degrees(np.arctan2(yy - py_, xx - px_)) % 360
    rr = np.sqrt((xx - px_) ** 2 + (yy - py_) ** 2)
    a0, a1 = 200.0, 340.0
    inarc = smooth(a0 - 1, a0 + 1, ang) * smooth(a1 + 1, a1 - 1, ang)
    chan = np.clip(0.5 - (np.abs(rr - 25.5) - 3.0) * img.S, 0, 1) * inarc
    img.over(sl, hexc("#0A0F22"), chan)                                          # the glass channel, dark when empty
    fill_to = a0 + (a1 - a0) * gauge
    fillm = chan * smooth(fill_to + 0.8, fill_to - 0.8, ang) * np.clip(0.5 - (np.abs(rr - 25.5) - 2.2) * img.S, 0, 1)
    fc = ramp(np.clip((ang - a0) / (a1 - a0), 0, 1), [(0, "#7FA0E0"), (1, "#E2E8F4")])
    img.over(sl, fc, fillm * 0.95)
    img.add(sl, hexc("#C3CEE4"), fillm * np.exp(-((rr - 25.5) / 1.2) ** 2) * 0.25)
    # the glass: a highlight on its upper-left, toward the light
    gl = chan * np.exp(-((rr - 24.0) / 0.7) ** 2) * smooth(270, 210, ang)
    img.add(sl, hexc("#F4F2EA"), gl * 0.35)
    rimc = lambda X, Y: np.abs(np.sqrt((X - px_) ** 2 + (Y - py_) ** 2) - 25.5) - 3.0
    ring = lambda X, Y: np.maximum(np.abs(rimc(X, Y)) - 0.9, -inarc_fn(X, Y))
    def inarc_fn(X, Y):
        A = np.degrees(np.arctan2(Y - py_, X - px_)) % 360
        return np.minimum(A - (a0 - 2), (a1 + 2) - A)
    draw_brass(img, ring, (px_, py_, 34), "round", depth=1.0, width=0.9)
    for f in (0.2, 0.6, 1.0):                                                    # the three notches
        a = math.radians(a0 + (a1 - a0) * f)
        nx_, ny_ = px_ + math.cos(a) * 25.5, py_ + math.sin(a) * 25.5
        tick = lambda X, Y, a=a, nx_=nx_, ny_=ny_: sd_segment(X, Y, nx_ - math.cos(a) * 3.6, ny_ - math.sin(a) * 3.6, nx_ + math.cos(a) * 3.6, ny_ + math.sin(a) * 3.6)[0] - 0.8
        draw_brass(img, tick, (nx_, ny_, 6), "round", depth=0.8, width=0.8)
        if flare is not None and abs(flare - f) < 1e-6:
            # a free ball earned: the notch's glass blooms with moonlight (a soft bloom, no rays), 0.4 s
            slf, Xf, Yf = img.win(nx_, ny_, 22)
            df = np.sqrt((Xf - nx_) ** 2 + (Yf - ny_) ** 2)
            img.add(slf, hexc("#E2E8F4"), np.exp(-(df / 4.5) ** 2) * 0.75 + np.exp(-(df / 12) ** 2) * 0.22)
    # the tube: a tapered brass telescope with two rings and a hood; the ball rests in its mouth
    a = math.radians(aim_deg)
    dx, dy = math.sin(a), math.cos(a)
    cylinder(img, px_, py_, dx, dy, 8, 62, 7.4, 6.0)                                 # the tube, tapering
    for (s0, s1, r0) in ((22, 25.5, 8.2), (48, 51, 7.4), (61, 70, 7.9)):              # two bands and the hood
        cylinder(img, px_, py_, dx, dy, s0, s1, r0, r0, base=0.05)
    # the mouth: the hood's dark opening, seen nearly end-on as the tube points toward the board
    mx_, my_ = px_ + dx * 70, py_ + dy * 70
    sl2, X2, Y2 = img.win(mx_, my_, 9)
    across = (X2 - mx_) * dy - (Y2 - my_) * dx
    along = (X2 - mx_) * dx + (Y2 - my_) * dy
    mouth = np.clip(0.5 - (np.sqrt(across ** 2 / 6.2 ** 2 + along ** 2 / 1.6 ** 2) - 1) * 4 * img.S, 0, 1)
    img.over(sl2, hexc("#07090F"), mouth)
    # the hub: a bevelled brass boss with a moonstone cabochon
    hub = lambda X, Y: sd_circle(X, Y, px_, py_, 14)
    draw_brass(img, hub, (px_, py_, 16), "bevel", depth=3.0, width=4.0)
    sl3, X3, Y3 = img.win(px_, py_, 7)
    u, v = (X3 - px_) / 5.5, (Y3 - py_) / 5.5
    d2 = u * u + v * v
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    lam = np.clip(u * L[0] + v * L[1] + nz * L[2], 0, 1)
    h = np.clip((u * L[0] + v * L[1] + nz * L[2] + nz) / np.sqrt(2 * (1 + L[2])), 0, 1)
    cab = ramp(lam, [(0, P["moonstone_deep"]), (0.6, P["moonstone_mid"]), (1, P["moonstone_high"])]) + hexc(P["moonstone_spec"]) * (h ** 80 * 0.9)[..., None]
    img.over(sl3, np.clip(cab, 0, 1), np.clip((1 - np.sqrt(d2)) * 5.5 * img.S + 0.5, 0, 1))
    if ball:
        draw_ball(img, px_ + dx * BARREL, py_ + dy * BARREL)
    return (px_ + dx * BARREL, py_ + dy * BARREL), (dx, dy)


# ------------------------------------------------------------------------------------------------ bucket A: crescent cradle
def cradle_geometry(bx, tips_y=569.0, bottom=588.0, thick=8.0, half=65.5):
    """Two circles through the horn tips: the outer gives the crescent's belly, the inner (higher, larger) its hollow."""
    s1 = bottom - tips_y
    R1 = (half ** 2 + s1 ** 2) / (2 * s1)
    s2 = s1 - thick
    R2 = (half ** 2 + s2 ** 2) / (2 * s2)
    return (bx, bottom - R1, R1), (bx, bottom - thick - R2, R2), tips_y


def bucket_cradle(img, bx):
    """Proposal A: a brass crescent, horns up, riding a slim brass rail on two small wheels. Its hollow is the
    catch (104 wide at the rim, 131 across the horns). A moonstone inlay runs along its belly."""
    # the rail it rides
    rail = lambda X, Y: sd_rrect(X, Y, WALL_L, 589.5, WALL_R, 592.5, 1.5)
    draw_brass(img, rail, None, "round", depth=1.2, width=1.5, base=-0.06)
    (ox, oy, R1), (ix, iy, R2), ty = cradle_geometry(bx)
    for wx in (bx - 22, bx + 22):                                              # wheels on the rail
        wh = lambda X, Y, wx=wx: sd_circle(X, Y, wx, 587.5, 3.2)
        draw_brass(img, wh, (wx, 587.5, 5), "bevel", depth=1.5, width=1.4, base=-0.08)
    def cres(X, Y):
        a = sd_circle(X, Y, ox, oy, R1)
        b = -sd_circle(X, Y, ix, iy, R2)
        return np.maximum(np.maximum(a, b), ty - Y)
    # a soft contact shadow on the rail under the belly
    sl, xx, yy = img.win(bx, 590, 70)
    img.mul(sl, hexc("#05070F"), np.exp(-((xx - bx) / 52) ** 2) * np.exp(-((yy - 589.5) / 1.6) ** 2) * 0.55)
    draw_brass(img, cres, (bx, 580, 72), "round", depth=4.0, width=4.2)
    # the moonstone inlay: a fine line along the belly's middle, lit where the belly faces the light
    sl, xx, yy = img.win(bx, 580, 70)
    mid = np.abs(np.sqrt((xx - ox) ** 2 + (yy - oy) ** 2) - (R1 - 2.6))
    inside = (cres(xx, yy) < -1.2).astype(np.float32)
    inl = np.clip(0.5 - (mid - 0.55) * img.S, 0, 1) * inside * (np.abs(xx - bx) < 50)
    tone = ramp(np.clip((xx - bx + 50) / 100, 0, 1), [(0, P["moonstone_high"]), (1, P["moonstone_mid"])])
    img.over(sl, tone, inl * 0.85)


# ------------------------------------------------------------------------------------------------ bucket B: lantern boat
def bucket_boat(img, bx):
    """Proposal B: a small lantern boat on a strip of still water along the foot. Dark lacquered planks, a brass
    gunwale, and a paper lantern on a curved stern post: the scene's one warm practical light. It warms the hull's
    inside, the post, the water under it and its own reflection; pegs more than ~60 units away take nothing."""
    sl, xx, yy = img.full()
    water_top = 585.0
    # the water: the sky's foot reflected, darker (Fresnel at a grazing view), with a faint ripple
    wm = np.clip((yy - water_top) * img.S + 0.5, 0, 1) * ((xx > WALL_L) & (xx < WALL_R))
    rip = fbm(img.h, img.w, 6 * img.S, 2, 91)
    wcol = ramp(np.clip((yy - water_top) / 12, 0, 1), [(0, "#2A3A6E"), (1, "#0E1530")]) * (0.9 + 0.2 * rip)[..., None]
    img.over(sl, wcol, wm)
    img.add(sl, hexc("#8FA4DA"), np.exp(-((yy - water_top) / 0.8) ** 2) * 0.25 * wm)
    # hull: a shallow boat, gunwale at 572-575, keel at 591; bow and stern rise a little
    half = 65.5
    def hull(X, Y):
        u = (X - bx) / half
        top = 573.0 - 4.0 * u ** 4
        bot = 573.0 + 18.5 * np.sqrt(np.clip(1 - u ** 2 * 0.98, 0, 1))
        return np.maximum(np.maximum(top - Y, Y - bot), np.abs(X - bx) - half)
    sl2, X2, Y2 = img.win(bx, 580, 78)
    hd = hull(X2, Y2)
    hm = img.cov(hd)
    u = (X2 - bx) / half
    # planks: horizontal strakes, the lit (upper) edge of each faintly lighter; the hull curves away below
    strake = (Y2 - 573) % 5.0
    curve = np.clip(1 - (Y2 - 573) / 20, 0.25, 1)
    wood = ramp(np.clip(curve, 0, 1), [(0, "#120C0A"), (1, "#3A2A22")]) * (1 + 0.10 * smooth(0.6, 0, strake))[..., None]
    wood = wood * (0.92 + 0.12 * fbm(hd.shape[0], hd.shape[1], 4 * img.S, 2, 93))[..., None]
    img.over(sl2, wood, hm)
    # the hollow seen over the gunwale: the boat's inside, lit warm by the lantern
    gun = lambda X, Y: sd_segment(X, Y, bx - half + 2, 573.5 - 4 * 0.85, bx + half - 2, 573.5 - 4 * 0.85)[0] - 0
    def gunwale(X, Y):
        uu = (X - bx) / half
        return np.maximum(np.abs(Y - (573.0 - 4.0 * uu ** 4)) - 1.6, np.abs(X - bx) - half + 1)
    draw_brass(img, gunwale, (bx, 573, 70), "round", depth=1.4, width=1.6)
    # the stern post and lantern
    lx, ly = bx + 50.0, 541.0
    def post(X, Y):
        t = np.clip((573 - Y) / 30.0, 0, 1)
        cx = bx + 57 - 6 * t ** 2
        return np.maximum(np.abs(X - cx) - 1.6, np.maximum(Y - 573, 535 - Y))
    sl3, X3, Y3 = img.win(bx + 54, 552, 26)
    img.over(sl3, hexc("#2A1E18"), img.cov(post(X3, Y3)))
    arm = lambda X, Y: sd_segment(X, Y, bx + 51, 535.5, lx, 535.5)[0] - 0.8
    draw_brass(img, arm, (lx + 3, 536, 8), "round", depth=0.6, width=0.8)
    # the lantern body: a paper cylinder lit from within (brightest at its middle), brass cap and foot
    sl4, X4, Y4 = img.win(lx, ly, 70)
    body = img.cov(sd_rrect(X4, Y4, lx - 4.6, ly - 6.0, lx + 4.6, ly + 6.0, 3.2))
    across = np.clip(1 - ((X4 - lx) / 4.6) ** 2, 0, 1)
    paper = ramp(across, [(0, "#B4602A"), (0.6, "#F2B060"), (1, "#FFE6B0")])
    ribs = 1 - 0.18 * np.exp(-(((Y4 - ly + 6) % 3.0) - 1.5) ** 2 / 0.12)
    img.over(sl4, paper * ribs[..., None], body)
    d4 = np.sqrt((X4 - lx) ** 2 + (Y4 - ly) ** 2)
    img.add(sl4, hexc("#FFB868"), np.exp(-(d4 / 10) ** 2) * 0.45 + np.exp(-(d4 / 26) ** 2) * 0.12)
    for (yc, w_) in ((ly - 6.8, 3.6), (ly + 6.8, 3.2)):
        cap = lambda X, Y, yc=yc, w_=w_: sd_rrect(X, Y, lx - w_, yc - 1.0, lx + w_, yc + 1.0, 0.8)
        draw_brass(img, cap, (lx, yc, 6), "round", depth=0.8, width=0.9)
    # its light: on the hull's stern quarter and the post facing it, and a pool and reflection on the water
    sl5, X5, Y5 = img.win(lx, 575, 70)
    d5 = np.sqrt((X5 - lx) ** 2 + (Y5 - ly) ** 2)
    fall = 1 / (1 + (d5 / 22) ** 2)
    hm5 = img.cov(hull(X5, Y5))
    img.add(sl5, hexc("#FFB060"), fall * hm5 * 0.30)
    wm5 = np.clip((Y5 - water_top) * img.S + 0.5, 0, 1)
    refl_y = 2 * water_top - Y5
    rd = np.sqrt(((X5 - lx) / 1.0) ** 2 + ((refl_y - ly) / 2.2) ** 2)
    img.add(sl5, hexc("#FFB060"), wm5 * np.exp(-(rd / 6) ** 2) * 0.55 * (1 - hm5))
    img.add(sl5, hexc("#FFB060"), wm5 * np.exp(-((X5 - lx) / 18) ** 2) * np.exp(-(Y5 - water_top) / 5) * 0.20 * (1 - hm5))


# ------------------------------------------------------------------------------------------------ the HUD
def hud(img, new_ball=False, score="128,450", balls=6, mult="×2", cleared=11, oranges_left=14, level="Lantern Steps", stage="1-3",
        power="Super Guide", turns=2, portrait=None):
    """Score and level on the top rail; the ball channel on the left rail; on the right rail the multiplier dial
    (a gilt arc for oranges cleared, ticks at x2, x3, x5, x10) and the active power's medallion."""
    text(img, 86, 21.5, stage, "ui_sb", 11, P["gilt_high"], anchor="lm", halo=0)
    text(img, 108, 21.5, level, "serif", 13.5, P["cream"], anchor="lm", halo=0)
    text(img, 636, 21.5, "Score", "ui_sb", 10.5, P["gilt"], anchor="rm", halo=0)
    text(img, 714, 21.5, score, "ui_sb", 16, P["cream"], anchor="rm", halo=0)
    # the ball channel: a glass tube with brass caps; balls stack from the bottom
    x0, x1, y0, y1 = 25.0, 51.0, 66.0, 330.0
    sl, xx, yy = img.win(38, (y0 + y1) / 2, (y1 - y0) / 2 + 14)
    tube = img.cov(sd_rrect(xx, yy, x0, y0, x1, y1, 12))
    img.over(sl, hexc("#070A16"), tube * 0.85)
    for i in range(balls):
        draw_ball(img, 38, y1 - 13 - i * 25.5, r=10.5)
    across = (xx - x0) / (x1 - x0)
    sl, xx, yy = img.win(38, (y0 + y1) / 2, (y1 - y0) / 2 + 14)
    tube = img.cov(sd_rrect(xx, yy, x0, y0, x1, y1, 12))
    img.add(sl, hexc("#F4F2EA"), tube * np.exp(-((across - 0.18) / 0.06) ** 2) * 0.28)   # the glass's highlight, upper-left side
    img.add(sl, hexc("#8FA4DA"), tube * np.exp(-((across - 0.90) / 0.05) ** 2) * 0.10)
    outline = lambda X, Y: np.abs(sd_rrect(X, Y, x0, y0, x1, y1, 12)) - 1.0
    draw_brass(img, outline, (38, (y0 + y1) / 2, (y1 - y0) / 2 + 6), "round", depth=1.0, width=1.0)
    for yc in (y0 - 2, y1 + 2):
        cap = lambda X, Y, yc=yc: sd_rrect(X, Y, x0 - 3, yc - 3.5, x1 + 3, yc + 3.5, 3)
        draw_brass(img, cap, (38, yc, 20), "round", depth=2.0, width=2.5)
    if new_ball:
        # the free ball arrives at the top of the stack: a soft moonlit bloom round it and a quiet '+1' beside it
        yb = y1 - 13 - (balls - 1) * 25.5
        slb, Xb, Yb = img.win(38, yb, 26)
        db = np.sqrt((Xb - 38) ** 2 + (Yb - yb) ** 2)
        img.add(slb, hexc("#C3CEE4"), np.exp(-(db / 13) ** 2) * 0.35)
        text(img, 62, yb - 12, "+1", "ui_sb", 11, P["cream"], anchor="lm", halo=0.6)
    text(img, 38, 352, str(balls), "ui_sb", 18, P["cream"], anchor="mm", halo=0)
    text(img, 38, 371, "Balls", "ui_sb", 10, P["gilt"], anchor="mm", halo=0)
    # the multiplier dial
    cx, cy, R = 762.0, 100.0, 25.0
    sl, xx, yy = img.win(cx, cy, R + 6)
    rr = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    ang = (np.degrees(np.arctan2(yy - cy, xx - cx)) + 90) % 360                  # 0 at the top, clockwise
    groove = np.clip(0.5 - (np.abs(rr - R) - 2.6) * img.S, 0, 1)
    img.over(sl, hexc("#0B1230"), groove)
    frac = cleared / 25.0
    arc = groove * smooth(frac * 360 + 0.8, frac * 360 - 0.8, ang) * np.clip(0.5 - (np.abs(rr - R) - 1.9) * img.S, 0, 1)
    nx_, ny_ = (xx - cx) / np.maximum(rr, 1e-3), (yy - cy) / np.maximum(rr, 1e-3)
    across = np.clip((rr - (R - 1.9)) / 3.8, 0, 1)
    nzz = np.sqrt(np.clip(1 - (across * 2 - 1) ** 2, 0, 1))
    gcol = brass_shade(nx_ * (across * 2 - 1), ny_ * (across * 2 - 1), nzz, base=0.05)
    img.over(sl, gcol, arc)
    for (n_left, lab) in ((15, "×2"), (10, "×3"), (6, "×5"), (3, "×10")):
        a = math.radians((25 - n_left) / 25 * 360 - 90)
        tk = lambda X, Y, a=a: sd_segment(X, Y, cx + math.cos(a) * (R - 4.6), cy + math.sin(a) * (R - 4.6), cx + math.cos(a) * (R + 4.6), cy + math.sin(a) * (R + 4.6))[0] - 0.7
        draw_brass(img, tk, (cx + math.cos(a) * R, cy + math.sin(a) * R, 7), "round", depth=0.7, width=0.7)
    edge = lambda X, Y: np.abs(np.sqrt((X - cx) ** 2 + (Y - cy) ** 2) - R) - 3.6
    ring = lambda X, Y: np.abs(edge(X, Y)) - 0.7
    draw_brass(img, ring, (cx, cy, R + 6), "round", depth=0.8, width=0.7)
    text(img, cx, cy + 1, mult, "ui_sb", 15, P["cream"], anchor="mm", halo=0)
    # oranges left, with an amber moon beside the count
    draw_moon(img, 748, 148, 6.0, "orange", sky=P["enamel"])
    text(img, 760, 148.5, str(oranges_left), "ui_sb", 13, P["cream"], anchor="lm", halo=0)
    text(img, 762, 167, "Orange", "ui_sb", 10, P["gilt"], anchor="mm", halo=0)
    # the power's medallion: its character in silhouette on enamel, the turns left as pips
    mx, my, mr = 762.0, 236.0, 24.0
    sl, xx, yy = img.win(mx, my, mr + 4)
    disc = img.cov(sd_circle(xx, yy, mx, my, mr))
    rr = np.sqrt((xx - mx) ** 2 + (yy - my) ** 2) / mr
    img.over(sl, ramp(np.clip((yy - my + mr) / (2 * mr), 0, 1), [(0, "#2A3A72"), (1, "#121A3E")]), disc)
    img.add(sl, hexc("#9DB4EA"), disc * np.exp(-(((xx - mx + 8) ** 2 + (yy - my + 10) ** 2) / 160)) * 0.25)
    if portrait is not None:
        portrait(img, mx, my, mr)
    bez = lambda X, Y: np.abs(sd_circle(X, Y, mx, my, mr + 1.6)) - 1.8
    draw_brass(img, bez, (mx, my, mr + 5), "round", depth=1.6, width=1.8)
    text(img, mx, my + mr + 13, power, "ui_sb", 10, P["cream"], anchor="mm", halo=0)
    for i in range(3):
        pxp = mx - 9 + i * 9
        pip = lambda X, Y, pxp=pxp: sd_circle(X, Y, pxp, my + mr + 25, 2.4)
        if i < turns:
            draw_brass(img, pip, (pxp, my + mr + 25, 4), "round", depth=1.0, width=1.6, base=0.08)
        else:
            sl2, X2, Y2 = img.win(pxp, my + mr + 25, 4)
            img.over(sl2, hexc("#0A0F22"), img.cov(np.abs(sd_circle(X2, Y2, pxp, my + mr + 25, 2.2)) - 0.5))


# ------------------------------------------------------------------------------------------------ a sample level
def level_layout(seed=11):
    """About 80 pegs and 9 curved bricks, symmetric, with room to breathe: two hanging arcs, a ring, two wings, a
    brick arch, two low fans and the wall columns. 25 oranges, 2 greens and 1 purple, chosen as the engine would."""
    pegs = []
    for cx in (228.0, 572.0):
        for i in range(9):
            a = math.radians(28 + i * 15.5)
            pegs.append((cx + 108 * math.cos(a), 116 + 108 * math.sin(a)))
    for i in range(12):
        a = math.radians(i * 30 + 15)
        pegs.append((400 + 60 * math.cos(a), 318 + 60 * math.sin(a)))
    for s in (-1, 1):
        for i in range(6):
            pegs.append((400 + s * (295 - i * 24), 300 + i * 17))
        for i in range(5):
            pegs.append((400 + s * (232 - i * 26), 462 - abs(i - 2) * 16))
        for i in range(4):
            pegs.append((400 + s * 300, 150 + i * 32))
    for (x, y) in ((168, 508), (262, 516), (538, 516), (632, 508), (372, 498), (428, 498)):   # the low field
        pegs.append((x, y))
    bricks = []
    for i in range(9):
        th = math.radians(-90 + (i - 4) * 8.0)
        bricks.append((400.0, 650.0, 236.0, th, math.radians(3.55)))
    rng = np.random.default_rng(seed)
    n = len(pegs) + len(bricks)
    idx = rng.permutation(n)
    kinds = ["blue"] * n
    for k in idx[:25]:
        kinds[k] = "orange"
    kinds[idx[25]] = "green"
    kinds[idx[26]] = "green"
    kinds[idx[27]] = "purple"
    return pegs, bricks, kinds


def draw_level(img, pegs, bricks, kinds, lit=(), gone=(), sky_col=SKY_AT_PEGS):
    for i, (bx_, by_, R, th, half) in enumerate(bricks):
        j = len(pegs) + i
        if j in gone:
            continue
        f, bb = sector_brick(bx_, by_, R, th, half, 12.0)
        draw_brick(img, f, bb, kinds[j], "lit" if j in lit else "unlit", variant=i)
    for i, (x, y) in enumerate(pegs):
        if i in gone:
            continue
        draw_moon(img, x, y, 10.0, kinds[i], "lit" if i in lit else "unlit", variant=i % 3, sky=sky_col)


def guide_dots(img, start, d, pegs, speed=395.0, g=500.0, spacing=17.0, super_guide=False):
    """The aim guide: silver dots 17 units apart along the ball's predicted arc from the barrel, stopping where the
    ball would first touch a peg (centres 16.7 apart). With Super Guide a thin line continues past that bounce."""
    x, y = start
    vx, vy = d[0] * speed, d[1] * speed
    dt = 0.002
    run, pts = 0.0, []
    hit = None
    for _ in range(5000):
        nx_, ny_ = x + vx * dt, y + vy * dt
        vy += g * dt
        run += math.hypot(nx_ - x, ny_ - y)
        x, y = nx_, ny_
        if run >= spacing:
            pts.append((x, y))
            run = 0
        for (px_, py_) in pegs:
            if (x - px_) ** 2 + (y - py_) ** 2 < 16.7 ** 2:
                hit = (px_, py_)
                break
        if hit or y > 600:
            break
    for i, (x, y) in enumerate(pts):
        sl, xx, yy = img.win(x, y, 4)
        d2 = ((xx - x) ** 2 + (yy - y) ** 2)
        img.add(sl, hexc("#C3CEE4"), np.exp(-d2 / 6) * 0.35)
        img.over(sl, hexc("#E2E8F4"), np.clip(0.5 - (np.sqrt(d2) - 1.6) * img.S, 0, 1) * (0.95 - 0.02 * i))
    if super_guide and hit:
        nxp, nyp = (x - hit[0]), (y - hit[1])
        nn = math.hypot(nxp, nyp)
        nxp, nyp = nxp / nn, nyp / nn
        vn = vx * nxp + vy * nyp
        vx, vy = (vx - (1 + 0.80) * vn * nxp), (vy - (1 + 0.80) * vn * nyp)
        line = []
        for _ in range(4000):
            x, y = x + vx * dt, y + vy * dt
            vy += g * dt
            line.append((x, y))
            if any((x - px_) ** 2 + (y - py_) ** 2 < 16.7 ** 2 for (px_, py_) in pegs if (px_, py_) != hit) or y > 600 or x < 81 or x > 719:
                break
        for (x, y) in line[::2]:
            sl, xx, yy = img.win(x, y, 2)
            img.over(sl, hexc("#C3CEE4"), np.clip(0.5 - (np.sqrt((xx - x) ** 2 + (yy - y) ** 2) - 0.6) * img.S, 0, 1) * 0.55)
    return pts, hit
