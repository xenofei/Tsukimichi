"""Our own painting for 5-3 "Lights on the Water": Limsa Lominsa's Lower Decks harbour by night, the lamps' long
reflections on the basin.

We look into the harbour basin from the water near its mouth. Its back quay curves round the basin, higher (farther)
in the middle and coming toward us at the sides, and a row of iron lamp posts stands along its coping; on the right a
timber jetty runs out toward us on its pilings with two more lamps. Every lamp lays a long, broken column of light down
the dark water toward us, wavering on the swell and widening as it nears, the near ones longest and brightest. Behind
the quay the city climbs in pale stone terraces and towers on both sides (lit arched windows, arcades on the quay,
parapets catching the moon), low in the middle where the basin opens to the sky; a young crescent moon hangs in the
upper left, clear of the launcher. Mooring ropes hang from the bollards, and a thin mist lies on the near water.

No figure is painted: no ship, no person (charter 1.3). The lights and the water are the subject.

FEATURES (board units): each lamp's head (x, y, scale), each reflection's centreline from the water line toward us (the
waver the painting draws), the quay's coping, the jetty's deck, and the moon.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

MOON = (150.0, 70.0, 19.0)
REFL_END = 548.0              # where the painted columns fade (the pegs stop above the bucket's lane)
LAMP_COL = "#FFC777"
REFL_COL = "#F6CE8E"
JETTY = ((548.0, 300.0), (812.0, 412.0))


def quay_top(x):
    """The coping of the back quay: highest (farthest) at x 410, coming toward us at the sides."""
    return 228.0 + 0.00055 * (np.asarray(x, np.float64) - 410.0) ** 2


def scale_at(x):
    return 0.85 + 0.001 * abs(x - 410.0)


def jetty_y(x):
    (x0, y0), (x1, y1) = JETTY
    return y0 + (x - x0) * (y1 - y0) / (x1 - x0)


# the lamps: name, x, the post's base (quay coping or jetty deck), scale, the reflection's phase
QUAY_LAMPS = [("Q1", 112.0, 0.2), ("Q2", 196.0, 1.9), ("Q3", 286.0, 3.4), ("Q4", 352.0, 0.9), ("Q5", 466.0, 2.6),
              ("Q6", 528.0, 4.4)]
JETTY_LAMPS = [("J1", 612.0, 1.3, 1.18), ("J2", 690.0, 5.1, 1.32)]
# the channel buoy in the harbour mouth: a lantern on a short mast over a float (x, the float's water line, scale, phase)
BUOY = ("B", 410.0, 356.0, 0.95, 3.0)


def lamps():
    """(name, x, base y, head y, scale, water line y, phase) for every lamp."""
    out = []
    for (n, x, ph) in QUAY_LAMPS:
        s = scale_at(x)
        base = float(quay_top(x))
        out.append((n, x, base, base - 46 * s, s, base + 15 * s + 4, ph))
    for (n, x, ph, s) in JETTY_LAMPS:
        base = jetty_y(x)
        out.append((n, x, base, base - 46 * s, s, base + 20 * s + 4, ph))
    (n, x, base, s, ph) = BUOY
    out.append((n, x, base, base - 26 * s, s, base + 3, ph))
    return out


def waver(lamp, y):
    """The reflection's centre at depth y: a slow sway that grows as the water nears us."""
    (_n, x, _b, _h, s, yw, ph) = lamp
    t = np.clip((np.asarray(y, np.float64) - yw), 0, None)
    amp = (1.5 + 0.022 * t) * s
    return x + amp * np.sin(t / 46.0 + ph) + 0.5 * amp * np.sin(t / 17.0 + ph * 1.7)


FEATURES = {
    "lamps": {l[0]: [round(l[1], 1), round(l[3], 1), round(l[4], 2)] for l in lamps()},
    **{f"refl {l[0]}": {"points": [[round(float(waver(l, y)), 1), round(y, 1)]
                                   for y in np.arange(l[5], REFL_END + 0.1, 6.0)]} for l in lamps()},
    "quay": {"points": [[x, round(float(quay_top(x)), 1)] for x in range(0, 801, 20)]},
    "jetty": {"points": [list(JETTY[0]), [800.0, round(jetty_y(800.0), 1)]]},
    "waterline": {"points": [[x, round(float(quay_top(x)) + 15 * scale_at(x) + 4, 1)] for x in range(0, 801, 20)]},
    "moon": list(MOON),
}
# two lit loggias high on the terraces (a row of tall arched windows over a balcony), one either side of the basin
LOGGIAS = {"loggia W": [(108.0, 150.0), (140.0, 150.0), (172.0, 150.0), (204.0, 150.0)],
           "loggia E": [(596.0, 140.0), (628.0, 140.0), (660.0, 140.0), (692.0, 140.0)]}
FEATURES.update({k: {"points": [list(p) for p in v]} for k, v in LOGGIAS.items()})
# the basin's open water: below the quay's water line to the jetty, then below the jetty's pilings
FEATURES["water"] = {"points": [[x, round(float(quay_top(x)) + 15 * scale_at(x) + 4, 1)] for x in range(0, 549, 12)]
                     + [[548.0, round(jetty_y(548.0) + 24, 1)], [800.0, round(jetty_y(800.0) + 34, 1)], [800, 600],
                        [0, 600]]}

# the city behind the quay: (x0, x1, top) blocks, back (hazier) and front (darker)
BACK = [(-10, 66, 98), (58, 118, 120), (110, 168, 106), (160, 226, 140), (218, 288, 160), (280, 334, 178),
        (476, 526, 178), (518, 580, 150), (572, 640, 124), (632, 700, 104), (692, 760, 116), (752, 812, 96)]
FRONT = [(-10, 88, 152), (82, 148, 174), (140, 212, 162), (204, 262, 188), (254, 316, 202), (308, 352, 214),
         (456, 500, 214), (494, 548, 198), (540, 608, 180), (600, 666, 190), (658, 728, 164), (720, 812, 172)]
TOWERS = [(34, 62, 62), (148, 166, 118), (606, 626, 128), (700, 724, 70)]


def _poly(W, H, S, pts, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    ImageDraw.Draw(im).polygon([(x * S * ss, y * S * ss) for (x, y) in pts], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def _boxes(W, H, S, boxes, ss=3):
    im = Image.new("L", (W * ss, H * ss), 0)
    dr = ImageDraw.Draw(im)
    for (x0, y0, x1, y1) in boxes:
        dr.rectangle([x0 * S * ss, y0 * S * ss, x1 * S * ss, y1 * S * ss], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def _blocks(W, H, S, blocks, X, rng, crenel=True):
    """A row of stone blocks down to the quay: each a flat roof, some with crenellated parapets."""
    ss = 3
    im = Image.new("L", (W * ss, H * ss), 0)
    dr = ImageDraw.Draw(im)
    k = S * ss
    for (x0, x1, top) in blocks:
        foot = float(quay_top((x0 + x1) / 2)) + 4
        dr.rectangle([x0 * k, top * k, x1 * k, foot * k], fill=255)
        roll = rng.random()
        if crenel and roll < 0.45:
            for cx in np.arange(x0 + 3, x1 - 3, 8.0):
                dr.rectangle([cx * k, (top - 4) * k, (cx + 4) * k, top * k], fill=255)
        elif roll < 0.8:
            # a low pitched roof of tile, with a chimney
            dr.polygon([((x0 - 2) * k, top * k), (((x0 + x1) / 2) * k, (top - (x1 - x0) * 0.22) * k), ((x1 + 2) * k, top * k)],
                       fill=255)
            cx = x0 + (x1 - x0) * (0.25 + 0.5 * rng.random())
            dr.rectangle([cx * k, (top - (x1 - x0) * 0.2) * k, (cx + 4) * k, top * k], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255


def _windows(W, H, S, blocks, rng, lit_p, cut):
    """Small arched windows in rows inside each block; a share of them lit. Returns (dark openings, lit glass)."""
    dark = Image.new("L", (W * 2, H * 2), 0)
    lit = Image.new("L", (W * 2, H * 2), 0)
    dd, dl = ImageDraw.Draw(dark), ImageDraw.Draw(lit)
    k = S * 2
    for (x0, x1, top) in blocks:
        foot = float(quay_top((x0 + x1) / 2)) - 10
        for y in np.arange(top + 10, foot - 6, 15.0):
            if y < cut:
                continue
            for x in np.arange(x0 + 6, x1 - 6, 10.0 + rng.integers(0, 3)):
                if rng.random() < 0.18:
                    continue
                box = [x * k, y * k, (x + 3.2) * k, (y + 6.0) * k]
                (dl if rng.random() < lit_p else dd).rounded_rectangle(box, radius=1.6 * k, fill=255)
    f = lambda im: np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255  # noqa: E731
    return f(dark), f(lit)


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    rng = np.random.default_rng(23)
    mx, my, mr = MOON
    d_moon = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    qt = quay_top(X).astype(np.float32)
    wl = (qt + 15 * (0.85 + 0.001 * np.abs(X - 410)) + 4).astype(np.float32)
    LS = lamps()

    # ---- the sky: deep indigo overhead, a violet haze over the city's lights low down
    sky = ramp(np.clip(Y / 240.0, 0, 1), [(0, "#06061A"), (0.45, "#100F30"), (0.8, "#1E1A44"), (1.0, "#2C2350")])
    sky = screen(sky, hexc("#C6CCF2") * (np.exp(-(d_moon / 90) ** 2) * 0.10 + np.exp(-(d_moon / 300) ** 2) * 0.05)[..., None])
    sky = screen(sky, hexc("#6A4A72") * (smooth(120, 230, Y) * 0.10)[..., None])
    sky = strokes(sky, (Y < 236).astype(np.float32), S, lambda x, y: 0.06 * math.sin(x / 90), int(3800 * S * S),
                  length=(10, 22), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=31)
    px = RL.stars(sky, S, 800, 37, (0, 0, W, 210 * S),
                  avoid=lambda x, y: float(np.clip((math.hypot(x / S - mx, y / S - my) - 40) / 120, 0, 1)), bright=0.5)
    # the young crescent: the moon drawn whole, then the earthlit dark side laid over all but a thin lit limb
    px, disc = RL.moon_emissive(px, S, mx, my, mr, seed=11)
    shadow = np.clip((mr * 0.86 - np.sqrt((X - mx - mr * 0.42) ** 2 + (Y - my + mr * 0.20) ** 2)) * S + 0.5, 0, 1) * disc
    px = px * (1 - shadow[..., None] * 0.88) + hexc("#1A1C38") * (shadow[..., None] * 0.80)

    # ---- the city: back terraces in haze, front terraces darker, towers; moonlit rims on their west faces and roofs
    for (blocks, body, haze, lit_p, seed) in ((BACK, "#1A1A3A", 0.30, 0.20, 3), (FRONT, "#0F1028", 0.0, 0.30, 5)):
        r2 = np.random.default_rng(seed)
        m = _blocks(W, H, S, blocks, X, r2)
        if blocks is BACK:
            m = np.maximum(m, _boxes(W, H, S, [(x0, t, x1, 230) for (x0, x1, t) in TOWERS]))
            for (x0, x1, t) in TOWERS:
                m = np.maximum(m, _poly(W, H, S, [(x0 - 2, t), ((x0 + x1) / 2, t - 22), (x1 + 2, t)]))
        stone = hexc(body) * (0.88 + 0.24 * fbm(H, W, 5 * S, 3, seed + 40))[..., None]
        stone = stone * (1 + 0.10 * smooth(0.5, 0.0, np.abs(((X / 9.0) % 1.0) - 0.5) * 2))[..., None]
        # the pale stone takes the moon higher up and the harbour's lamplight at its foot
        stone = screen(stone, hexc("#5A5C8C") * (smooth(240, 110, Y) * 0.16)[..., None])
        stone = screen(stone, hexc("#A0683C") * (smooth(170, 240, Y) * 0.10)[..., None])
        # string courses: a thin lit ledge every 15 units up the terraces
        ledge = smooth(0.12, 0.0, np.abs(((Y + seed * 3) / 15.0) % 1.0 - 0.06))
        stone = screen(stone, hexc("#8C90C8") * (ledge * 0.10)[..., None])
        px = px * (1 - m[..., None]) + stone * m[..., None]
        rim = np.clip(m - np.roll(m, max(1, int(S * 1.2)), 1), 0, 1) * 0.7 + np.clip(m - np.roll(m, max(1, int(S)), 0), 0, 1)
        px = screen(px, hexc("#9EA6DA") * (rim * (0.28 if haze else 0.36))[..., None])
        # terrace copings: thin lit lines where each block's roof meets the next
        dk, lt = _windows(W, H, S, blocks, r2, lit_p, 0)
        px = px * (1 - dk[..., None] * 0.6) + hexc("#05050F") * (dk[..., None] * 0.6)
        glow = blur(lt, 2.2 * S)
        px = screen(px, hexc("#FFB869") * (lt * (0.62 if haze else 0.85) + glow * 0.5)[..., None])
    # the loggias: tall arched windows lit from within, a balcony's rail under them, light spilling on the stone
    for pts in LOGGIAS.values():
        ys = pts[0][1]
        xs0, xs1 = pts[0][0] - 14, pts[-1][0] + 14
        bay = np.clip((Y - ys + 11) * S, 0, 1) * np.clip((ys + 9 - Y) * S, 0, 1) * (X > xs0) * (X < xs1)
        px = px * (1 - bay[..., None] * 0.35) + hexc("#16122A") * (bay[..., None] * 0.35)
        for (lx, ly) in pts:
            hw = 3.4
            d = np.sqrt(((X - lx) / hw) ** 2 + (np.clip(ly - 3 - Y, 0, None) / hw) ** 2)
            win = np.clip((1 - d) * hw * S, 0, 1) * (Y < ly + 6) * (Y > ly - 7)
            px = px * (1 - win[..., None]) + hexc("#E8A866") * (win * (0.75 + 0.25 * smooth(ly - 7, ly + 6, Y)))[..., None]
            spill = np.exp(-(((X - lx) / 9.0) ** 2 + ((Y - ly) / 11.0) ** 2))
            px = screen(px, hexc("#FFAE62") * (spill * 0.16)[..., None])
        rail = np.exp(-((Y - ys - 8) / 0.8) ** 2) * (X > xs0) * (X < xs1)
        px = screen(px, hexc("#C89868") * (rail * 0.35)[..., None])
        for rx in np.arange(xs0 + 2, xs1, 4.0):
            bal = np.clip((0.7 - np.abs(X - rx)) * S, 0, 1) * (Y > ys + 8) * (Y < ys + 12)
            px = px * (1 - bal[..., None] * 0.6) + hexc("#0A0816") * (bal[..., None] * 0.6)
    # the quay's arcade: a run of arches in the front terraces' foot, warm light inside
    arch_m = np.zeros((H, W), np.float32)
    arc_lit = np.zeros((H, W), np.float32)
    for x in list(np.arange(14, 330, 19.0)) + list(np.arange(492, 790, 19.0)):
        foot = float(quay_top(x)) - 0.5
        top = foot - 15
        hw = 4.6
        d = np.sqrt(((X - x) / hw) ** 2 + (np.clip(top + hw - Y, 0, None) / hw) ** 2)
        arch = np.clip((1 - d) * hw * S, 0, 1) * (Y < foot) * (Y > top)
        arch_m = np.maximum(arch_m, arch)
        lit = rng.random()
        if lit < 0.55:
            inner = np.exp(-((X - x) / 3.0) ** 2) * smooth(top + 2, foot, Y)
            arc_lit = np.maximum(arc_lit, arch * inner * (0.45 + 0.55 * lit / 0.55))
    px = px * (1 - arch_m[..., None] * 0.75) + hexc("#04040C") * (arch_m[..., None] * 0.75)
    px = screen(px, hexc("#FFAE62") * (arc_lit * 0.55)[..., None])
    # piers between the arches catch the moon on their west edges
    px = screen(px, hexc("#9EA6DA") * (np.clip(arch_m - np.roll(arch_m, -max(1, int(S)), 1), 0, 1) * 0.12)[..., None])
    # the harbour mouth's far side in the middle: a low dark headland with a few far lights
    head = _poly(W, H, S, [(330, 240), (360, 214), (392, 208), (430, 212), (470, 222), (480, 240)])
    px = px * (1 - head[..., None]) + hexc("#0C0D24") * head[..., None]
    for (fx, fy) in ((372, 216), (404, 212), (436, 217), (452, 222)):
        px = screen(px, hexc("#FFC07A") * (np.exp(-(((X - fx) / 1.2) ** 2 + ((Y - fy) / 1.2) ** 2)) * 0.7)[..., None])

    # ---- the quay: the coping, the wall's coursed stone to the water, the lamps' pools on it
    wall = np.clip((Y - qt) * S + 0.5, 0, 1) * np.clip((wl - Y) * S + 0.5, 0, 1)
    course = 0.88 + 0.12 * smooth(0.25, 0.0, np.abs(((Y - qt) / 5.0) % 1.0 - 0.5))
    joint = 1 - 0.18 * smooth(0.12, 0.0, np.abs(((X + 6 * np.floor((Y - qt) / 5.0)) / 13.0) % 1.0 - 0.5))
    wstone = hexc("#1C1C38") * (course * joint * (0.85 + 0.3 * fbm(H, W, 4 * S, 3, 61)))[..., None]
    px = px * (1 - wall[..., None]) + wstone * wall[..., None]
    coping = np.exp(-((Y - qt - 1.2) / 1.3) ** 2)
    px = screen(px, hexc("#A8ACE0") * (coping * 0.30)[..., None])
    # bollards on the coping and their ropes hanging into the water
    for bx in (150, 240, 328, 408, 488, 568):
        b0 = float(quay_top(bx))
        bol = np.clip((3.0 - np.abs(X - bx)) * S, 0, 1) * (Y > b0 - 6) * (Y < b0 + 0.5)
        px = px * (1 - bol[..., None]) + hexc("#0A0A18") * bol[..., None]
        px = screen(px, hexc("#A8ACE0") * (bol * smooth(b0 - 4, b0 - 6, Y) * 0.4)[..., None])

    # ---- the water: dark, teal-black, its swell catching the sky and the city's glow
    depth = np.clip((Y - 240) / 360, 0, 1)
    sea = ramp(depth, [(0, "#1C2244"), (0.2, "#121834"), (0.6, "#0A0F24"), (1, "#060A1A")])
    rip = fbm(H, W, 3.0 * S, 3, 27)
    sea = sea * (0.84 + 0.26 * rip)[..., None]
    freq = 0.55 + 1.9 * (1 - depth)                                   # ripples crowd toward the quay (perspective)
    swell = np.sin(Y * freq + rip * 5.5 + np.sin(X / 37.0) * 1.2)
    sea = screen(sea, hexc("#3A3E72") * (smooth(0.55, 0.95, swell) * 0.18 * (1 - 0.5 * depth))[..., None])
    # the city's lights smeared on the water under the quay
    cityrefl = blur(np.clip((Y > wl) * (Y < wl + 40) * 1.0, 0, 1), 6 * S) * smooth(0.2, 0.9, fbm(H, W, 2.5 * S, 2, 71))
    sea = screen(sea, hexc("#C88454") * (cityrefl * 0.10)[..., None])
    # the lamps' reflections: a long column of glints under each lamp, wavering, widening and breaking toward us
    cols = np.zeros((H, W), np.float32)
    dash = np.zeros((H, W), np.float32)
    halo = np.zeros((H, W), np.float32)
    lines = smooth(0.05, 0.75, np.sin(Y * freq * 1.15 + rip * 6.0 + 0.7)) * smooth(0.30, 0.65, fbm(H, W, 5 * S, 3, 33))
    # the glints: short horizontal flashes on the swell's faces, each row jittered sideways (the column breaks up)
    crest = smooth(0.62, 0.92, np.sin(Y * freq * 1.6 + rip * 7.0 + 1.9))
    jit = (fbm(H, W, 7 * S, 2, 45) - 0.5)
    for L in LS:
        (_n, x, _b, _h, s, yw, ph) = L
        t = np.clip(Y - yw, 0, None)
        cx = waver(L, Y)
        w = (2.2 + 0.044 * t) * s
        on = smooth(yw - 1, yw + 5, Y) * smooth(REFL_END + 26, REFL_END - 30, Y)
        fade = (1.0 - 0.40 * np.clip(t / (REFL_END - yw), 0, 1)) * min(1.0, 0.60 + 0.35 * s)
        core = np.exp(-((X - cx) / w) ** 2)
        cols = np.maximum(cols, core * on * fade * (0.35 + 0.65 * lines))
        dx = (X - cx - jit * w * 2.4) / (w * 1.9)
        dash = np.maximum(dash, np.exp(-dx ** 4) * crest * on * fade)
        halo += np.exp(-((X - cx) / (w * 4.0)) ** 2) * on * fade * 0.5
    sea = screen(sea, hexc("#B07A48") * (np.clip(halo, 0, 1) * 0.20)[..., None])
    sea = screen(sea, hexc(REFL_COL) * (cols * 0.66)[..., None])
    sea = screen(sea, hexc("#FFE2AE") * (dash * 0.62)[..., None])
    sea = strokes(sea, (Y >= 236).astype(np.float32), S, lambda x, y: 0.0, int(5000 * S * S), length=(5, 14),
                  width=(0.8, 1.6), jitter=0.06, hue_jitter=0.004, seed=9)
    # a thin mist lying on the near water
    mist = smooth(470, 560, Y) * (0.5 + 0.5 * fbm(H, W, 40 * S, 3, 81))
    sea = screen(sea, hexc("#5A6890") * (mist * 0.10)[..., None])
    sea_m = np.clip((Y - wl) * S + 0.5, 0, 1)
    px = px * (1 - sea_m[..., None]) + sea * sea_m[..., None]

    # ---- the jetty: a timber deck on pilings running toward us on the right, its rail, its shadow on the water
    (jx0, jy0), (jx1, jy1) = JETTY
    jy = jy0 + (X - jx0) * (jy1 - jy0) / (jx1 - jx0)
    js = 1.0 + 0.0016 * np.clip(X - jx0, 0, None)
    deck = (X > jx0) * np.clip((Y - jy + 0.5 * S) * S, 0, 1) * np.clip((jy + 7 * js - Y) * S, 0, 1)
    under = (X > jx0) * (Y > jy) * (Y < jy + 22 * js)
    pil = np.zeros((H, W), np.float32)
    for k in range(9):
        pxk = jx0 + 10 + k * 30
        pil = np.maximum(pil, np.clip((2.6 * (1 + 0.0016 * (pxk - jx0)) - np.abs(X - pxk)) * S, 0, 1)
                         * (Y > jetty_y(pxk)) * (Y < jetty_y(pxk) + 26 * (1 + 0.0016 * (pxk - jx0))))
    shade = blur(under.astype(np.float32), 3 * S) * 0.55
    px = px * (1 - shade[..., None])
    rail = np.zeros((H, W), np.float32)
    for k in range(9):
        rx = jx0 + 18 + k * 30
        rail = np.maximum(rail, np.clip((1.1 - np.abs(X - rx)) * S, 0, 1) * (Y < jetty_y(rx)) * (Y > jetty_y(rx) - 13))
    rail = np.maximum(rail, (X > jx0) * np.exp(-((Y - jy + 12 * js) / 0.9) ** 2))
    jet = np.maximum(np.maximum(deck, pil), rail)
    grainw = 0.85 + 0.3 * fbm(H, W, 3 * S, 3, 91)
    planks = 1 - 0.25 * smooth(0.10, 0.0, np.abs(((X - jx0) / 7.0) % 1.0 - 0.5) - 0.40)
    wood = hexc("#22182A") * (grainw * planks)[..., None]
    px = px * (1 - jet[..., None]) + wood * jet[..., None]
    # the deck's edge and the pilings' west sides catch the moon; the lamps warm the planks round their feet
    px = screen(px, hexc("#A8ACE0") * ((X > jx0) * np.exp(-((Y - jy - 0.4) / 1.0) ** 2) * 0.30)[..., None])
    px = screen(px, hexc("#9EA6DA") * (np.clip(pil - np.roll(pil, max(1, int(S)), 1), 0, 1) * 0.30)[..., None])
    for (n, x, ph, s) in JETTY_LAMPS:
        warm = np.exp(-(((X - x) / (34 * s)) ** 2 + ((Y - jetty_y(x) - 4) / (10 * s)) ** 2)) * deck
        px = screen(px, hexc("#E09A58") * (warm * 0.30)[..., None])
    # mooring buoys riding the swell (each with its own small glint), and slack ropes from the bollards into the water
    for (bx, by, br) in ((160, 352, 3.2), (430, 320, 2.6), (252, 410, 3.6), (560, 446, 4.0), (88, 470, 4.4)):
        buoy = np.clip((br - np.sqrt((X - bx) ** 2 + ((Y - by) * 1.5) ** 2)) * S, 0, 1)
        px = px * (1 - buoy[..., None]) + hexc("#2A1418") * buoy[..., None]
        px = screen(px, hexc("#FFD8A0") * (np.exp(-(((X - bx + br * 0.4) / 1.0) ** 2 + ((Y - by + br * 0.3) / 0.8) ** 2)) * 0.5)[..., None])
        ring = np.exp(-((np.sqrt((X - bx) ** 2 + ((Y - by - 1.5) * 3.2) ** 2) - br * 2.0) / 0.8) ** 2) * (Y > by)
        px = screen(px, hexc("#7884B8") * (ring * 0.18)[..., None])
    for bx in (240, 488):
        b0 = float(quay_top(bx))
        yw_ = b0 + 15 * scale_at(bx) + 4
        rope_pts = [(bx + 18 * t, b0 - 3 + (yw_ + 10 - b0) * t + 6 * t * (1 - t)) for t in np.linspace(0, 1, 16)]
        im = Image.new("L", (W * 3, H * 3), 0)
        ImageDraw.Draw(im).line([(x_ * S * 3, y_ * S * 3) for (x_, y_) in rope_pts], fill=255, width=max(1, int(round(1.2 * S * 3))))
        rope = np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255
        px = px * (1 - rope[..., None] * 0.8) + hexc("#0A0810") * (rope[..., None] * 0.8)

    # ---- the channel buoy: a dark float riding low, banded, with its own ring on the water
    (_bn, bx, bwl, bs, _bp) = BUOY
    flo = np.clip((7.5 * bs - np.abs(X - bx) - np.clip(bwl - Y, 0, None) * 0.55) * S, 0, 1) * (Y > bwl - 9 * bs) * (Y < bwl + 1.5)
    px = px * (1 - flo[..., None]) + hexc("#1A0E16") * flo[..., None]
    band = flo * np.exp(-((Y - bwl + 5 * bs) / (1.0 * bs)) ** 2)
    px = screen(px, hexc("#C88858") * (band * 0.35)[..., None])
    px = screen(px, hexc("#9EA6DA") * (np.clip(flo - np.roll(flo, max(1, int(S)), 1), 0, 1) * 0.3)[..., None])
    ring = np.exp(-((np.sqrt((X - bx) ** 2 + ((Y - bwl - 1.5) * 3.4) ** 2) - 13 * bs) / 1.0) ** 2) * (Y > bwl)
    px = screen(px, hexc("#E8B67A") * (ring * 0.22)[..., None])
    # ---- the lamps: an iron post, a lantern cage, the warm fire; a pool of light on the stone round each foot
    for L in LS:
        (_n, x, base, hy, s, yw, _ph) = L
        post = np.clip((1.3 * s - np.abs(X - x)) * S, 0, 1) * (Y > hy + 6 * s) * (Y < base)
        arm = np.clip((0.9 * s - np.abs(Y - hy - 6 * s)) * S, 0, 1) * (np.abs(X - x) < 5 * s)
        cage = np.clip((1 - np.abs(X - x) / (4.2 * s)) * 6, 0, 1) * (Y > hy - 6 * s) * (Y < hy + 5 * s)
        roof = np.clip((5.0 * s - np.abs(X - x) - (hy - 6 * s - Y) * 1.2) * S, 0, 1) * (Y > hy - 11 * s) * (Y < hy - 5.5 * s)
        iron = np.maximum.reduce([post, arm, roof])
        px = px * (1 - iron[..., None]) + hexc("#07070F") * iron[..., None]
        px = px * (1 - cage[..., None] * 0.6) + hexc("#2A1A10") * (cage[..., None] * 0.6)
        d2 = (X - x) ** 2 + (Y - hy) ** 2
        fire = (np.exp(-d2 / (2 * (2.2 * s) ** 2)) * 0.95 + np.exp(-d2 / (2 * (9 * s) ** 2)) * 0.20
                + np.exp(-d2 / (2 * (30 * s) ** 2)) * 0.07)
        px = screen(px, hexc(LAMP_COL) * fire[..., None])
        pool = np.exp(-(((X - x) / (26 * s)) ** 2 + ((Y - base - 6 * s) / (12 * s)) ** 2))
        px = screen(px, hexc("#E09A58") * (pool * 0.16)[..., None])

    # the ceiling: luma eased under 0.45 everywhere but the moon's thin limb
    Yl = px @ RL.LUM
    soft = RL.soft_ceiling(Yl, 0.45, 0.84)
    face = smooth(mr + 3, mr - 1, d_moon)
    target = soft * (1 - face) + Yl * face
    px = px * (target / np.maximum(Yl, 1e-4))[..., None]
    px = RL.vignette(px, 0.24)
    return RL.grain(px, 0.008, seed=59)
