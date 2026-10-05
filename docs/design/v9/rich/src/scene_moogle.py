"""Pilot level base-p3 "The Moonlit Post" (technique: a creature in outline), scene. Painted in code.

A moogle courier in flight over the Black Shroud by night, a letter held to its chest: big round head, pointed ears,
the pom-pom on its curling stalk, bat wings spread, a satchel at its hip, its feet trailing. It flies across the sky
with the moon behind it, high on the left, so it is a dark silhouette (the approved v9 card style, at the size of a
board) rimmed with moonlight on every edge that faces the moon: cool white on its fur, a faint warm red on the
pom-pom, which is red. Below, rows of the forest's crowns step down into mist, each crown rim-lit on its moon side.
A painterly stroke pass (brush.py) repaints sky, cloud and foliage.

Everything is in the engine's 800 x 600 units; the outline in level_moogle.py is placed against these numbers.
"""
import math
import sys

import numpy as np
from PIL import Image, ImageDraw

from brush import flow_const, strokes
from layout import smooth_path
from rich_lib import LUM, OUT_SCENES, V9, blur, moon_emissive, fbm, grain, hexc, ramp, save_rgb, screen, smooth, stars, vignette

sys.path.insert(0, str(V9.parent / "v8" / "art" / "src"))

MOON = (156.0, 108.0, 30.0)

# ---- the moogle, flying right (units)
HEAD = (468.0, 236.0, 88.0, 76.0)                 # centre, radii
EAR_L = [(398, 196), (392, 150), (414, 136), (436, 170)]
EAR_R = [(496, 166), (520, 132), (544, 142), (544, 192)]
STALK = [(486, 162), (492, 128), (512, 104), (540, 92)]
POM = (552.0, 88.0, 21.0)
NOSE = (556.0, 254.0, 17.0)
BODY = (396.0, 330.0, 56.0, 50.0, -0.35)          # centre, radii, tilt
ARM = [(430, 318), (462, 322), (486, 334), (478, 350), (452, 346), (428, 338)]
LETTER = [(470, 318), (520, 312), (526, 346), (476, 352)]
SATCHEL = (360.0, 366.0, 24.0, 19.0)
STRAP = [(436, 290), (402, 316), (372, 350)]
FEET = [((362, 360), (322, 398), 15, 11), ((388, 368), (356, 408), 15, 11)]
# the near wing: a bat wing from the shoulder, its leading edge sweeping up and back, three struts, scalloped
WING_NEAR = dict(root=(392, 288), shoulder=(372, 252), tips=[(236, 156), (214, 214), (226, 274), (268, 318)])
WING_FAR = dict(root=(410, 278), shoulder=(400, 246), tips=[(318, 138), (296, 176), (300, 214), (328, 246)])


# the whole figure sits 20 right and 30 down from where it was drawn, clear of the launcher's swing
DX, DY = 20.0, 30.0
HEAD = (HEAD[0] + DX, HEAD[1] + DY, HEAD[2], HEAD[3])
EAR_L = [(x + DX, y + DY) for (x, y) in EAR_L]
EAR_R = [(x + DX, y + DY) for (x, y) in EAR_R]
STALK = [(x + DX, y + DY) for (x, y) in STALK]
POM = (POM[0] + DX, POM[1] + DY, POM[2])
NOSE = (NOSE[0] + DX, NOSE[1] + DY, NOSE[2])
BODY = (BODY[0] + DX, BODY[1] + DY) + BODY[2:]
ARM = [(x + DX, y + DY) for (x, y) in ARM]
LETTER = [(x + DX, y + DY) for (x, y) in LETTER]
SATCHEL = (SATCHEL[0] + DX, SATCHEL[1] + DY) + SATCHEL[2:]
STRAP = [(x + DX, y + DY) for (x, y) in STRAP]
FEET = [((a[0] + DX, a[1] + DY), (b[0] + DX, b[1] + DY), wa, wb) for (a, b, wa, wb) in FEET]
for _w in (WING_NEAR, WING_FAR):
    _w["root"] = (_w["root"][0] + DX, _w["root"][1] + DY)
    _w["shoulder"] = (_w["shoulder"][0] + DX, _w["shoulder"][1] + DY)
    _w["tips"] = [(x + DX, y + DY) for (x, y) in _w["tips"]]
EYE = (520.0 + DX, 226.0 + DY)
SEAL = (498.0 + DX, 333.0 + DY)


def ellipse_pts(cx, cy, rx, ry, rot=0.0, n=96):
    c, s = math.cos(rot), math.sin(rot)
    return [(cx + rx * math.cos(t) * c - ry * math.sin(t) * s, cy + rx * math.cos(t) * s + ry * math.sin(t) * c)
            for t in np.linspace(0, 2 * math.pi, n, endpoint=False)]


def bat_wing(w, scallop=0.30):
    """A bat wing: leading edge from the shoulder out to the first tip, then scalloped membrane between the strut tips
    back to the root."""
    sx, sy = w["shoulder"]
    rx, ry = w["root"]
    tips = w["tips"]
    lead = smooth_path([(sx, sy), ((sx + tips[0][0]) / 2 + 6, (sy + tips[0][1]) / 2 - 22), tips[0]], 12)
    pts = list(lead)
    for a, b in zip(tips[:-1], tips[1:]):
        mx_, my_ = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        # the membrane sags in toward the root between two struts
        vx, vy = rx - mx_, ry - my_
        n = math.hypot(vx, vy)
        cx, cy = mx_ + vx / n * n * scallop, my_ + vy / n * n * scallop
        for k in range(1, 13):
            t = k / 12
            pts.append(((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * cx + t * t * b[0],
                        (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * cy + t * t * b[1]))
    last = tips[-1]
    for k in range(1, 9):
        t = k / 8
        pts.append((last[0] + (rx - last[0]) * t, last[1] + (ry - last[1]) * t + math.sin(t * math.pi) * 8))
    return pts


def struts(w):
    return [(w["root"], t) for t in w["tips"][1:]] + [(w["shoulder"], w["tips"][0])]


def fluffy(pts, depth=1.6, pitch=6.0):
    """Fur: small scallops along a closed contour."""
    P_ = np.asarray(pts + [pts[0]], np.float64)
    seg = np.sqrt(((P_[1:] - P_[:-1]) ** 2).sum(1))
    cum = np.concatenate([[0], np.cumsum(seg)])
    out = []
    s = 0.0
    while s < cum[-1]:
        i = min(np.searchsorted(cum, s, side="right") - 1, len(seg) - 1)
        t = (s - cum[i]) / max(seg[i], 1e-9)
        p = P_[i] + (P_[i + 1] - P_[i]) * t
        d = P_[i + 1] - P_[i]
        n = np.array([d[1], -d[0]]) / max(np.linalg.norm(d), 1e-9)
        ph = (s / pitch) % 1.0
        out.append(tuple(p + n * depth * (abs(math.sin(ph * math.pi)) - 0.5)))
        s += 1.2
    return out


def raster(W, H, S, polys, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    dr = ImageDraw.Draw(im)
    for pts in polys:
        dr.polygon([(x * S * ss, y * S * ss) for (x, y) in pts], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def tubes(W, H, S, segs, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    dr = ImageDraw.Draw(im)
    for (pts, w0, w1) in segs:
        P_ = smooth_path(pts, 10) if len(pts) > 2 else [(pts[0][0] + (pts[1][0] - pts[0][0]) * t,
                                                          pts[0][1] + (pts[1][1] - pts[0][1]) * t)
                                                         for t in np.linspace(0, 1, max(8, int(math.hypot(pts[1][0] - pts[0][0], pts[1][1] - pts[0][1]) * 2)))]
        for k, (x, y) in enumerate(P_):
            r = (w0 + (w1 - w0) * k / max(1, len(P_) - 1)) / 2 * S * ss
            dr.ellipse([x * S * ss - r, y * S * ss - r, x * S * ss + r, y * S * ss + r], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def fur(m, S, n, length=(1.5, 4.2), width=(0.9, 1.6), seed=0, ss=3):
    """Fur along a mask's edge: short tapered tufts drawn outward along the edge's normal, so the silhouette's edge is
    hair, not a cut line."""
    H, W = m.shape
    b = blur(m, 0.9 * S)
    gy, gx = np.gradient(b)
    g = np.sqrt(gx * gx + gy * gy)
    edge = (g > g.max() * 0.25) & (m > 0.2) & (m < 0.95)
    ys, xs = np.nonzero(edge)
    if len(xs) == 0:
        return m
    rng = np.random.default_rng(seed)
    pick = rng.choice(len(xs), size=min(n, len(xs)), replace=False)
    im = Image.fromarray((m * 255).astype(np.uint8), "L").resize((W * ss, H * ss), Image.BILINEAR)
    dr = ImageDraw.Draw(im)
    for k in pick:
        x, y = xs[k] + 0.5, ys[k] + 0.5
        nx, ny = -gx[ys[k], xs[k]], -gy[ys[k], xs[k]]
        nn = math.hypot(nx, ny) + 1e-9
        a = math.atan2(ny / nn, nx / nn) + rng.normal(0, 0.35)
        Ln = rng.uniform(*length) * S
        w0 = rng.uniform(*width) * S
        x0, y0 = x - math.cos(a) * 1.2 * S, y - math.sin(a) * 1.2 * S
        x1, y1 = x + math.cos(a) * Ln, y + math.sin(a) * Ln
        px_, py_ = -math.sin(a) * w0 / 2, math.cos(a) * w0 / 2
        dr.polygon([((x0 + px_) * ss, (y0 + py_) * ss), ((x0 - px_) * ss, (y0 - py_) * ss), (x1 * ss, y1 * ss)], fill=255)
    return np.maximum(m, np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255)


def moogle_masks(W, H, S):
    hx, hy, hrx, hry = HEAD
    head = raster(W, H, S, [ellipse_pts(hx, hy, hrx, hry), smooth_path(EAR_L + [EAR_L[0]], 8),
                            smooth_path(EAR_R + [EAR_R[0]], 8), ellipse_pts(*NOSE[:2], NOSE[2], NOSE[2])])
    head_core = head
    head = fur(head, S, int(1600 * S), seed=1)
    bx, by, brx, bry, rot = BODY
    body_core = raster(W, H, S, [ellipse_pts(bx, by, brx, bry, rot), smooth_path(ARM + [ARM[0]], 6)])
    body = fur(body_core, S, int(1000 * S), seed=2)
    feet_core = raster(W, H, S, [ellipse_pts(b[0], b[1], 10, 7, math.atan2(b[1] - a[1], b[0] - a[0])) for (a, b, _, _) in FEET])
    feet_core = np.maximum(feet_core, tubes(W, H, S, [([a, b], wa, wb) for (a, b, wa, wb) in FEET]))
    feet = fur(feet_core, S, int(300 * S), length=(1.0, 2.6), seed=3)
    sat = raster(W, H, S, [ellipse_pts(*SATCHEL)])
    strap = tubes(W, H, S, [(STRAP, 4.0, 4.0)])
    stalk = tubes(W, H, S, [(STALK, 4.2, 3.0)])
    pom_core = raster(W, H, S, [ellipse_pts(POM[0], POM[1], POM[2] - 2, POM[2] - 2)])
    pom = fur(pom_core, S, int(600 * S), length=(2.0, 5.0), width=(1.0, 1.8), seed=4)
    wing_n = raster(W, H, S, [bat_wing(WING_NEAR)])
    wing_f = raster(W, H, S, [bat_wing(WING_FAR, 0.26)])
    letter = raster(W, H, S, [LETTER])
    return dict(head=head, body=body, feet=feet, satchel=sat, strap=strap, stalk=stalk, pom=pom, wing_near=wing_n,
                wing_far=wing_f, letter=letter, head_core=head_core, body_core=body_core, feet_core=feet_core,
                pom_core=pom_core)


def put(px, col, m):
    return px * (1 - m[..., None]) + np.asarray(col, np.float32) * m[..., None]


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    mx, my, mr = MOON
    dm = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    # ---- sky, the moon's glow, clouds lit on their moon-facing edges, stars, the moon
    px = ramp(np.clip(Y / 520, 0, 1), [(0, "#080C20"), (0.38, "#101A3E"), (0.78, "#1B2752"), (1.0, "#273562")])
    px = screen(px, hexc("#B9C8F0") * (np.exp(-(dm / 150) ** 2) * 0.22 + np.exp(-(dm / 420) ** 2) * 0.08)[..., None])
    cl = fbm(H, W, 70 * S, 5, 37)
    bank = smooth(0.50, 0.72, cl) * (np.exp(-((Y - 168) / 30) ** 2) + 0.8 * np.exp(-((Y - 262) / 22) ** 2) + 0.5 * np.exp(-((Y - 66) / 20) ** 2))
    gy_, gx_ = np.gradient(blur(bank, 2.5 * S))
    tm = dm + 1e-6
    lit_edge = np.clip(-(gx_ * (mx - X) + gy_ * (my - Y)) / tm * 30 * S, 0, 1)
    px = put(px, ramp(np.clip(Y / 520, 0, 1), [(0, "#1A2448"), (1, "#2A3664")]), bank * 0.75)
    px = screen(px, hexc("#A9BCEC") * (lit_edge * bank * (0.18 + 0.30 * np.exp(-(dm / 380) ** 2)))[..., None])
    px = strokes(px, smooth(500, 420, Y), S, flow_const(-0.06), int(11000 * S * S), length=(10, 26), width=(2.0, 4.2),
                 jitter=0.025, hue_jitter=0.005, seed=2)
    px = stars(px, S, 190, 9, (75 * S, 41 * S, 725 * S, 420 * S),
               avoid=lambda x, y: min(1.0, max(0.0, (math.hypot(x / S - mx, y / S - my) - 120) / 260)) * (1 - y / (H * 0.85)))

    px, moon_disc = moon_emissive(px, S, mx, my, mr)
    # ---- the forest: rows of crowns stepping down into mist, each crown rim-lit on its moon side
    rng = np.random.default_rng(5)
    for row, (base, size, col0, col1, mist) in enumerate(((430, 18, "#1E2A54", "#1A244C", 0.22),
                                                           (466, 24, "#141D40", "#111938", 0.16),
                                                           (512, 32, "#0C1332", "#090E26", 0.10))):
        crowns = []
        x = 20 + rng.uniform(0, size)
        while x < 800:
            r = size * rng.uniform(0.6, 1.35)
            kind = "fir" if rng.random() < 0.35 else "broad"
            crowns.append((x, base - r * rng.uniform(0.1, 0.9), r, kind))
            x += r * rng.uniform(0.6, 0.95)
        polys = []
        for (cx, cy, r, kind) in crowns:
            if kind == "fir":
                tiers = [((cx - r * 0.75 * (1 - t * 0.6), cy + r * 0.6 - t * r * 1.5), (cx, cy - r * 0.8 - t * r * 1.5 + r * 0.2),
                          (cx + r * 0.75 * (1 - t * 0.6), cy + r * 0.6 - t * r * 1.5)) for t in (0.0, 0.33, 0.66)]
                polys += [list(t) for t in tiers]
            else:
                pts = ellipse_pts(cx, cy, r, r * 0.95, 0, 40)
                wob = [(px_ + (cx - px_) * 0.12 * math.sin(k * 2.1 + cx), py_ + (cy - py_) * 0.12 * math.cos(k * 1.7 + cy))
                       for k, (px_, py_) in enumerate(pts)]
                polys.append(wob)
        polys.append([(0, base), (800, base), (800, 600), (0, 600)])
        m = raster(W, H, S, polys)
        m = fur(m, S, int(2500 * S), length=(1.0, 3.0), width=(0.8, 1.4), seed=70 + row)
        fill = ramp(np.clip((Y - base + size) / (size * 3), 0, 1), [(0, col0), (1, col1)])
        px = put(px, fill, m)
        b = blur(m, 1.0 * S)
        gy, gx = np.gradient(b)
        gn = np.sqrt(gx * gx + gy * gy) + 1e-6
        facing = np.clip(-(gx * (mx - X) + gy * (my - Y)) / (gn * tm), 0, 1)
        edge = np.clip((m - blur(m, 2.2 * S)) * 3, 0, 1)
        px = screen(px, hexc("#8FA4DA") * (edge * facing * (0.32 - row * 0.06))[..., None])
        px = strokes(px, m * smooth(base + 60, base - size, Y), S, lambda x_, y_: -math.pi / 2 + 0.6 * math.sin(x_ * 0.13),
                     int(2600 * S * S), length=(3, 7), width=(1.2, 2.4), jitter=0.10, seed=60 + row)
        px = screen(px, hexc("#3A4C84") * (np.exp(-((Y - base - 8) / 10) ** 2) * mist)[..., None])
    # ---- the moogle: a dark silhouette rimmed by the moon behind it
    Mk = moogle_masks(W, H, S)
    ux, uy = (mx - X) / tm, (my - Y) / tm

    def edge_face(m, width):
        b = blur(m, 0.8 * S)
        gy, gx = np.gradient(b)
        gn = np.sqrt(gx * gx + gy * gy) + 1e-6
        facing = np.clip(-(gx * ux + gy * uy) / gn, 0, 1) ** 1.1
        return np.clip((m - blur(m, width * S)) * 3.0, 0, 1) * facing
    far_col, near_col = hexc("#0B0E1E"), ramp(np.clip((Y - 80) / 420, 0, 1), [(0, "#161B32"), (1, "#0B0E1C")])
    px = put(px, far_col, Mk["wing_far"])
    px = screen(px, hexc("#B8C6EE") * (edge_face(Mk["wing_far"], 1.4) * 0.35)[..., None])
    order = ("wing_near", "feet", "body", "satchel", "strap", "head", "stalk", "pom", "letter")
    sil = np.zeros((H, W), np.float32)
    for part in order:
        m = Mk[part]
        # a hair of shadow where a nearer part overlaps a farther one, so the parts separate
        sep = np.clip(blur(m, 1.1 * S) - m, 0, 1) * sil
        px = px * (1 - sep * 0.45)[..., None]
        px = put(px, near_col, m)
        sil = np.maximum(sil, m)
    # the wing: a thin membrane between its struts, lit through from the moon behind it (it glows a little, most
    # where it is thinnest, toward its trailing edge); the struts stay dark
    strut = tubes(W, H, S, [([a, b], 3.0, 1.4) for (a, b) in struts(WING_NEAR)]) * Mk["wing_near"]
    wn = Mk["wing_near"]
    thin = np.clip(1 - blur(wn, 6 * S) * 1.1, 0, 1) * 0.6 + 0.4
    trans = wn * (1 - strut) * thin * np.clip(1 - (X - 230) / 260, 0.3, 1)
    px = screen(px, hexc("#3E3570") * (trans * 0.55)[..., None])
    px = put(px, hexc("#151A30"), strut)
    wf = Mk["wing_far"] * (1 - sil)
    px = screen(px, hexc("#2C2652") * (wf * 0.30)[..., None])
    # the letter's wax seal
    lx_, ly_ = SEAL
    # paper in the moogle's backlit shadow, a little lifted and cool; the envelope's flap as a V crease
    lt = Mk["letter"]
    px = screen(px, hexc("#1C2238") * (lt * 0.55)[..., None])
    (ax_, ay_), (bx_, by_), (cx_, cy_), (dx_, dy_) = LETTER
    for (p0, p1) in (((ax_, ay_), (lx_, ly_)), ((bx_, by_), (lx_, ly_))):
        crease = tubes(W, H, S, [([p0, p1], 0.7, 0.7)]) * lt
        px = px * (1 - crease * 0.35)[..., None]
    # the seal: a flat wax disc, its dull sheen only on the edge toward the moon
    seal = np.clip((4.6 - np.sqrt((X - lx_) ** 2 + (Y - ly_) ** 2)) * S, 0, 1)
    px = put(px, hexc("#34121A"), seal)
    sr = np.sqrt((X - lx_) ** 2 + (Y - ly_) ** 2)
    px = screen(px, hexc("#7A4A58") * (seal * np.exp(-((sr - 4.0) / 0.6) ** 2) * np.clip(-((X - lx_) + (Y - ly_)) / 6, 0, 1) * 0.5)[..., None])
    rims = {"head": ("#D8E2F6", 0.85, 1.8), "body": ("#D8E2F6", 0.75, 1.6), "feet": ("#C8D4F2", 0.55, 1.2),
            "wing_near": ("#B8C6EE", 0.70, 1.4), "satchel": ("#C9B48C", 0.55, 1.2), "strap": ("#C9B48C", 0.4, 0.8),
            "stalk": ("#C8D4F2", 0.6, 0.9), "pom": ("#A9B4D4", 0.70, 1.4), "letter": ("#C3CEE4", 0.45, 0.6)}
    covered = np.zeros((H, W), np.float32)
    for part in reversed(order):
        col, k, w = rims[part]
        r = edge_face(Mk[part], w) * (1 - covered)
        px = screen(px, hexc(col) * (r * k)[..., None])
        px = screen(px, hexc(col) * (edge_face(strut, 0.8) * 0.35 if part == "wing_near" else 0)[..., None]) if part == "wing_near" else px
        covered = np.maximum(covered, Mk[part])
    # backlit fur: the tufts along every moon-facing edge carry the light through them, a fine glowing fringe
    for part, col, k in (("head", "#DCE6F8", 0.95), ("body", "#DCE6F8", 0.80), ("feet", "#C8D4F2", 0.6),
                         ("pom", "#A9B4D4", 0.75)):
        core = Mk[part + "_core"]
        tufts = np.clip(Mk[part] - core, 0, 1)
        b = blur(core, 2.0 * S)
        gy, gx = np.gradient(b)
        gn = np.sqrt(gx * gx + gy * gy) + 1e-6
        facing = np.clip(-(gx * ux + gy * uy) / gn, 0, 1) ** 1.6
        facing = blur(facing * (b > 0.02) * (b < 0.98), 0.8 * S) * 1.6
        halo = np.clip(tufts * 1.4, 0, 1) * np.clip(facing, 0, 1)
        px = screen(px, hexc(col) * (halo * k)[..., None])
        px = screen(px, hexc(col) * (blur(halo, 1.6 * S) * k * 0.25)[..., None])
    # the pom-pom's own colour shows faintly in its body; the letter's paper too
    px = screen(px, hexc("#4A1820") * (Mk["pom"] * 0.55)[..., None])
    # the eye: in the moogle's shadow side, a small glint of the sky
    ex, ey = EYE
    px = put(px, hexc("#05060C"), np.clip((1 - np.sqrt(((X - ex) / 5.0) ** 2 + ((Y - ey) / 7.0) ** 2)) * 3 * S, 0, 1))
    px = screen(px, hexc("#DCE6FF") * (np.exp(-(((X - ex + 1.6) ** 2 + (Y - ey + 2.2) ** 2) / 1.6)) * 0.6)[..., None])
    # ---- the value ceiling (the moon excepted: no peg covers it), the vignette, grain
    Yl = px @ LUM
    moon_m = blur(moon_disc, 1.0 * S)
    capk = np.where(Yl > 0.42, 0.42 / np.maximum(Yl, 1e-4), 1.0)
    px = px * (capk * (1 - moon_m) + moon_m)[..., None]
    px = vignette(px, 0.28)
    return grain(px, 0.008, seed=23)


if __name__ == "__main__":
    for S in (1, 2):
        save_rgb(paint(S), OUT_SCENES / f"base-p3-moogle{'@2x' if S == 2 else ''}.png")
    print("ok")
