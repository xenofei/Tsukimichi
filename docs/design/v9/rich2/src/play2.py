"""The in-play screens (rich pass 2, round 2): HUD, a power firing, Fever, the tally, pause, and the peg-marks assist.
Every one is on The Moon Road's stage 3 with Cid (r2state). Run through screens2.py."""
import json
import math
import subprocess

import numpy as np

import r2lib
from r2lib import (C, PALETTES, Img, RICH, blit, blur, gild, gilt_frame, gtext, gwidth, gwrap, hexc, panel, part, ramp,
                   ring, shadow_under, smooth, screen, sd_rrect)
import r2kit as K
import r2cast
import r2state as ST
from rich_lib import draw_ball, draw_moon, fbm
from screens2 import INK2, INK3, banner, hexc_to, place, fit_text


# ------------------------------------------------------------------------------------------------ the window
def window(board_2x, W, H, pal="base-p1"):
    """The game window: the board scaled to fit; the side margins carry the level's own scene, blurred and darkened
    (its light spills past the frame), as the spec says (round 2: no free-floating ornament there)."""
    from PIL import Image as _I
    img = Img(W, H, 1.0)
    k = min(W / 800, H / 600)
    bw, bh = 800 * k, 600 * k
    bx = (W - bw) / 2
    if bx > 1:
        small = np.asarray(_I.fromarray((board_2x * 255).astype(np.uint8)).resize((int(W / 6), int(H / 6)), _I.BILINEAR),
                           np.float32) / 255
        bg = np.asarray(_I.fromarray((small * 255).astype(np.uint8)).resize((W, H), _I.BICUBIC), np.float32) / 255
        bg = blur(bg, 10)
        p = PALETTES.get(pal, PALETTES["base-p1"])
        img.px = bg * 0.62 + hexc(p["deep"]) * 0.20
        sl, X, Y = img.full()
        edge = np.minimum(X, W - X)
        img.mul(sl, hexc("#020308"), smooth(bx, 0, edge) * 0.55)
    place(img, board_2x, bx, 0, bw, bh)
    return img


def board(level_id, small=False, **kw):
    from composite2 import render
    im, colours = render(level_id, S=2, small=small, **kw)
    return im.px, colours


def trace(level_id, a):
    from board import MFCHECK
    out = subprocess.run(["dotnet", str(MFCHECK), "trace", str(RICH / "levels" / f"{level_id}.json"), str(a), "5", "1", "400"],
                         capture_output=True, text=True, check=True).stdout
    return json.loads(out.strip().splitlines()[-1])


_AIM = {}


def aim_trace():
    """An aim whose first contact is about 150 units out and whose bounce runs on 50 or more (the engine's flight)."""
    if "best" in _AIM:
        return _AIM["best"]

    def plen(pts):
        p = np.asarray(pts)
        return float(np.sqrt(((p[1:] - p[:-1]) ** 2).sum(1)).sum()) if len(p) > 1 else 0.0
    best, score = None, 1e9
    for a in [x * 1.0 for x in range(-60, 61, 2) if abs(x) >= 6]:
        t = trace("base-p1", a)
        if len(t["hits"]) < 2:
            continue
        first = plen(t["points"][:t["hits"][0] + 1])
        second = plen(t["points"][t["hits"][0]:t["hits"][1] + 1])
        if second >= 50 and abs(first - 150) < score:
            best, score = (a, t), abs(first - 150)
    _AIM["best"] = best
    return best


def guide(img, tr, super_guide=False, accent="#5DDAE0"):
    from rich_lib import sd_segment
    pts = np.asarray(tr["points"], np.float64)
    h0, h1 = tr["hits"][0], tr["hits"][1]
    seg = pts[:h0 + 1]
    d = np.concatenate([[0], np.cumsum(np.sqrt(((seg[1:] - seg[:-1]) ** 2).sum(1)))])
    s_ = 17.0
    while s_ < d[-1] - 4:
        x, y = np.interp(s_, d, seg[:, 0]), np.interp(s_, d, seg[:, 1])
        sl, X, Y = img.win(x, y, 4)
        d2 = (X - x) ** 2 + (Y - y) ** 2
        img.add(sl, hexc("#C3CEE4"), np.exp(-d2 / 6) * 0.35)
        img.over(sl, hexc("#E2E8F4"), np.clip(0.5 - (np.sqrt(d2) - 1.6) * img.S, 0, 1) * 0.95)
        s_ += 17.0
    if super_guide:
        for (a, b) in zip(pts[h0:h1], pts[h0 + 1:h1 + 1]):
            cx, cy = (a + b) / 2
            sl, X, Y = img.win(cx, cy, np.hypot(*(b - a)) / 2 + 4)
            dseg = sd_segment(X, Y, a[0], a[1], b[0], b[1])[0]
            img.add(sl, hexc(accent), np.exp(-(dseg / 2.2) ** 2) * 0.35)
            img.over(sl, hexc("#D8FFFB"), np.clip(0.5 - (dseg - 0.65) * img.S, 0, 1) * 0.75)


def super_guide_board():
    """base-p1 aimed with Super Guide (Minfilia) active: for the characters screen's power thumbnail."""
    aim, tr = aim_trace()
    acc = r2cast.BY_ID["SuperGuide"]["accent"]
    px, _ = board("base-p1", aim=aim, after=lambda img, level, colours, aimed: guide(img, tr, True, acc),
                  hud_kw=dict(stage="QP", score="0", balls=10, cleared=0, mult="×1", oranges=25, carrier="SuperGuide",
                              turns=3, active=True, gauge=0.0))
    return px


# ------------------------------------------------------------------------------------------------ HUD
def hud(W=1280, H=800):
    """3-3 The Airship Road with Cid, first shots: aiming, the guide to the first contact (from the engine's flight)."""
    small = W < 700
    aim, tr = aim_trace()
    px, _ = board("base-p1", small=small, aim=aim, after=lambda img, level, colours, aimed: guide(img, tr),
                  hud_kw=dict(stage="3-3", score="96,420", balls=5, cleared=9, mult="×1", oranges=16, carrier="Wings",
                              turns=0, active=False, gauge=0.2))
    return window(px, W, H, "base-p1")


# ------------------------------------------------------------------------------------------------ a power firing
def free_spot(level, region, need=40.0):
    from framecheck import piece_distance
    d = piece_distance(level).copy()
    yy, xx = np.mgrid[0:600, 0:800]
    d[np.sqrt((xx - 400) ** 2 + (yy - 87) ** 2) < 150] = -1        # never over the launcher's swing
    x0, y0, x1, y1 = region
    sub = d[y0:y1, x0:x1]
    iy, ix = np.unravel_index(np.argmax(sub), sub.shape)
    return x0 + ix, y0 + iy, float(sub[iy, ix])


def wings_on_bucket(img, bx, accent):
    """Brass Wings: the PvP emblem's gilt wings bolted to the cart's rims, spreading the mouth to twice its width, with
    the carrier's glow along them."""
    wg = gild(part("pvp_wing"), 0.55, 0.15)
    w = 56.0
    h = w * wg.shape[0] / wg.shape[1]
    for side in (-1, 1):
        x = bx - 65.5 - w + 4 if side < 0 else bx + 65.5 - 4
        sl, X, Y = img.win(x + w / 2, 566, w)
        d = np.sqrt(((X - x - w / 2) / (w * 0.7)) ** 2 + ((Y - 566) / 22) ** 2)
        img.add(sl, hexc(accent), np.exp(-d ** 2 * 1.5) * 0.45)
        blit(img, wg, x, 572 - h * 0.62, w, h, flipx=(side > 0))


def power(W=1280, H=800):
    """A power firing (round 2, designer M8): the green peg is hit on 3-3; Cid's card slides in at the left rail in the
    journal's frame, his power named in his colour; the effect is drawn in that colour: gilt wings spread the cart. A
    style-shot callout rides a ribbon in open sky."""
    small = W < 700
    cid = ST.CARRIER
    from board import engine_colours, load_level, pieces
    lp = RICH / "levels" / "base-p1.json"
    level = load_level(lp)
    colours = engine_colours(lp, 5, 1)
    greens = [(i, d) for i, ((k, d), c) in enumerate(zip(pieces(level), colours)) if c == "green" and k == "peg"]
    gi, gd = max(greens, key=lambda t: t[1]["y"])
    lit = {gi}
    # the pegs the ball lit on its way down to the green (the nearest few above it)
    others = sorted([(math.hypot(d["x"] - gd["x"], d["y"] - gd["y"]), i) for i, (k, d) in enumerate(pieces(level))
                     if k == "peg" and i != gi and d["y"] < gd["y"]])[:3]
    lit |= {i for _, i in others}
    sx, sy, _ = free_spot(level, (300, 120, 680, 330))
    bx = 420.0

    def after(img, level_, colours_, aimed):
        x, y = gd["x"], gd["y"]
        sl, X, Y = img.win(x, y, 60)
        d = np.sqrt((X - x) ** 2 + (Y - y) ** 2)
        # the effect is light (GD N8): a white-gold flash and ring, only its outer edge in the carrier's colour, never
        # a filled disc of a peg's hue
        img.add(sl, hexc("#FFF4DC"), np.exp(-((d - 30) / 3.5) ** 2) * 0.55 + np.exp(-(d / 14) ** 2) * 0.30)
        img.add(sl, hexc(cid["accent"]), np.exp(-((d - 34) / 2.0) ** 2) * 0.35)
        rng = np.random.default_rng(3)
        for k_ in range(14):
            a = rng.uniform(0, 2 * math.pi)
            r0 = rng.uniform(16, 44)
            px_, py_ = x + math.cos(a) * r0, y + math.sin(a) * r0
            s2, X2, Y2 = img.win(px_, py_, 4)
            img.add(s2, hexc("#FFE6C8"), np.exp(-((X2 - px_) ** 2 + (Y2 - py_) ** 2) / 1.6) * 0.8)
        draw_ball(img, x + 16, y + 20)
        wings_on_bucket(img, bx, cid["accent"])
        # the style-shot callout: the same ribbon as FULL MOON and LEVEL CLEAR, small (GD and UX Nit), in open sky
        from screens2 import banner
        banner(img, sx, sy, "LONG SHOT", 22, 0, sub="+25,000", sub_size=18, accent="#FFB45E", max_w=200, laurel=False)

    px, _ = board("base-p1", small=small, lit=lit, after=after, bucket_x=bx, aim=-20.0,
                  hud_kw=dict(stage="3-3", score="131,420", balls=4, cleared=12, mult="×2", oranges=13, carrier="Wings",
                              turns=5, active=True, gauge=0.55, ball=False, pulse=1.0 if small else 0.0))
    img = window(px, W, H, "base-p1")
    if small:
        # at 640 there is no margin: the moment stays in the chrome (UX M1, GD N3). The rail portrait and gems pulse
        # in Cid's colour, and a ribbon slides along the top rail; nothing covers the opening while the ball is live
        from screens2 import banner
        k = min(W / 800, H / 600)
        # the ribbon stays within the top rail (it ends above the opening at y 32): Cid's own face pulses on the rail
        banner(img, 320, 18, "BRASS WINGS", 28, 0, accent=cid["accent"], max_w=600, laurel=True)
        return img
    # Cid's card slides in, in the window's margin beside the board (1.2 s): nothing on the board is covered
    k = min(W / 800, H / 600)
    ox = (W - 800 * k) / 2
    cw, x0, y0, k = ox - 16, 8.0, 150.0, 0.8
    a = r2cast.art(cid)
    ch = cw * a.shape[0] / a.shape[1]
    sl, X, Y = img.win(x0 + cw / 2, y0 + ch / 2, ch)
    d = np.sqrt(((X - x0 - cw / 2) / cw) ** 2 + ((Y - y0 - ch / 2) / ch) ** 2)
    img.add(sl, hexc(cid["accent"]), np.exp(-(np.clip(d - 0.55, 0, None) / 0.2) ** 2) * (d > 0.5) * 0.5)
    shadow_under(img, x0, y0, x0 + cw, y0 + ch, r=2, off=(5, 8), soft=14, k=0.75)
    blit(img, a, x0, y0, cw, ch)
    gilt_frame(img, x0, y0, x0 + cw, y0 + ch, scale=0.36 * k)
    by = y0 + ch + 24 * k
    gtext(img, x0 + cw / 2, by, cid["power"], "jupiter", fit_text(cid["power"], "jupiter", 34 * k, cw + 4, 14),
          hexc_to(cid["accent"], 0.2), anchor="mm", edge="#140A02", edge_w=1.6, glow=cid["accent"], glow_k=0.55, glow_r=6)
    gtext(img, x0 + cw / 2, by + 26 * k, "Cid Garlond", "axis", max(15 * k, 12), C["cream"], anchor="mm", edge="#05070F",
          edge_w=1.2)
    if not small:
        for i, ln in enumerate(("the bucket", "doubles", "for 5 turns")):
            gtext(img, x0 + cw / 2, by + 50 + 17 * i, ln, "axis", 14, INK2, anchor="mm", edge="#05070F", edge_w=1.2)
    return img


# ------------------------------------------------------------------------------------------------ Fever
FEVER_LEVEL = "base-p3"


def fever_scene(t_swell=1.0, t_sky=1.0, S=2):
    """The Fever lighting change on the dressed scene (designer M4): the moon swells and brightens (behind the oak
    leaves), the sky lifts toward the carrier's colour; the framing stays dark in front."""
    import dress2
    px, ctx = dress2.dress(FEVER_LEVEL, S)
    open_ = 1 - ctx.cover
    X, Y = dress2.grid(S)
    acc = ST.CARRIER["accent"]
    sky = smooth(360, 220, Y) * open_
    px = screen(px, hexc(acc) * (sky * 0.16 * t_sky)[..., None])
    mx, my, r0 = 155.0, 107.0, 29.0
    r = r0 * (1 + 0.45 * t_swell)
    d = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    halo = (np.exp(-(np.clip(d - r, 0, None) / (r * 1.6)) ** 2) * 0.30 + np.exp(-(np.clip(d - r, 0, None) / (r * 5)) ** 2) * 0.12)
    px = screen(px, hexc("#FFF0D8") * (halo * (d > r) * open_ * t_swell)[..., None])
    disc = np.clip((r - d) * S, 0, 1) * open_
    face = hexc("#FFF6EA") * (0.94 + 0.06 * np.sqrt(np.clip(1 - (d / r) ** 2, 0, 1)))[..., None]
    px = px * (1 - disc[..., None]) + face * disc[..., None]
    return px, ctx


def fever_state():
    from board import engine_colours, load_level, pieces
    lp = RICH / "levels" / f"{FEVER_LEVEL}.json"
    lvl = load_level(lp)
    n = len(pieces(lvl))
    rng = np.random.default_rng(4)
    gone = set(rng.choice(n, size=int(n * 0.62), replace=False).tolist())
    cols = engine_colours(lp, 5, 1)
    oranges = [i for i, c in enumerate(cols) if c == "orange"]
    last = max(oranges, key=lambda i: pieces(lvl)[i][1]["y"] if pieces(lvl)[i][0] == "peg" else 0)
    gone |= set(oranges)
    return lvl, gone, set(range(n)) - gone, pieces(lvl)[last][1]


def dust_burst(img, x0, y0, t, keep_clear):
    """Moondust bursting once from the last orange: motes flying out 20-110 units, fading (t 0..1)."""
    rng = np.random.default_rng(11)
    for _ in range(70):
        a = rng.uniform(0, 2 * math.pi)
        sp = rng.uniform(40, 110)
        r = sp * (1 - (1 - t) ** 2)
        x, y = x0 + math.cos(a) * r, y0 + math.sin(a) * r + 10 * t * t
        if keep_clear(x, y) < 6:
            continue
        al = (1 - t * 0.8) ** 1.2 * rng.uniform(0.6, 1.0)
        sl, X, Y = img.win(x, y, 8)
        dd = (X - x) ** 2 + (Y - y) ** 2
        img.add(sl, hexc("#FFE9C8"), np.exp(-dd / 2.4) * al + np.exp(-dd / 18) * al * 0.25)


def fever(W=1280, H=800, t=1.0, banner_alpha=None):
    small = W < 700
    lvl, gone, lit, last = fever_state()
    sc, ctx = fever_scene(min(1, t / 0.5), min(1, t / 0.8))
    from framecheck import piece_distance
    alive = {"level": {"pegs": [d for i, (k, d) in enumerate(__import__("board").pieces(lvl)) if k == "peg" and i not in gone]}}
    dist = piece_distance(alive["level"])

    def clear(x, y):
        return float(dist[min(599, max(0, int(y))), min(799, max(0, int(x)))])

    def after(img, level, colours, aimed):
        dust_burst(img, last["x"], last["y"], min(1.0, 0.45 + 0.3 * t), clear)
        draw_ball(img, 452.0, 452.0)
        # the plate is full while Fever lands, then fades to 35% so the last ball stays in view (UX m3)
        ba = banner_alpha if banner_alpha is not None else 1.0
        if ba > 0:
            banner(img, 400, 222, "FULL MOON", 66, 470, sub="Every moon left is worth more. Pick your cup.", sub_size=16,
                   accent=ST.CARRIER["accent"], plate_alpha=(1.0 if t < 1.0 else 0.35) * ba, alpha=ba)

    def extra(img):
        sl, X, Y = img.full()
        img.mul(sl, hexc("#03050C"), np.exp(-((Y - 238) / 52) ** 2) * ((X > 75) & (X < 725)) * 0.5)

    px, _ = board(FEVER_LEVEL, small=small, bucket="fever", lit=lit, gone=gone, extra=extra, after=after, scene=sc,
                  hud_kw=dict(stage="3-1", score="182,300", balls=4, cleared=25, mult="×10", oranges=0, ball=False,
                              carrier="Wings", turns=0, active=False, gauge=0.8, aim=0.0, accent=ST.CARRIER["accent"]))
    return window(px, W, H, FEVER_LEVEL)


# ------------------------------------------------------------------------------------------------ tally
def tally(W=1280, H=800):
    small = W < 700
    lvl = json.loads((RICH / "levels" / "base-p2.json").read_text())
    n = len(lvl["pegs"]) + len(lvl["bricks"])
    gone = set(range(n)) - {3, 9, 17, 22, 30, 41, 47, 55, 61}
    px, _ = board("base-p2", small=small, gone=gone,
                  hud_kw=dict(stage="3-2", score="251,880", balls=3, cleared=25, mult="×10", oranges=0, ball=False,
                              carrier="Wings", turns=0, active=False))
    img = window(px, W, H, "base-p2")
    sl, X, Y = img.full()
    img.mul(sl, hexc("#03040C"), np.full(X.shape, 0.6, np.float32))
    cid = ST.CARRIER
    rows = [("Shots", "71,880"), ("Full Moon, centre cup", "100,000"), ("Balls left  3 × 10,000", "30,000"),
            ("Long Shot", "25,000"), ("Off the Wall", "25,000")]
    if not small:
        x0, y0, x1, y1 = 350, 150, 930, 772
        panel(img, x0, y0, x1, y1, jewel=cid["accent"])
        banner(img, 640, y0 + 4, "LEVEL CLEAR", 44, 420, accent=cid["accent"])
        K.title(img, 640, y0 + 76, "The Holy See", 50, anchor="mm")
        gtext(img, 640, y0 + 108, f"{ST.CAMPAIGN} · 3-2 · won with 3 balls to spare", "axis", 15, INK2, anchor="mm")
        K.crest_rule(img, 640, y0 + 134, 440, crest=True, scale=0.4)
        y = y0 + 168
        for lab, v in rows:
            gtext(img, x0 + 76, y, lab, "jupiter", 27, C["cream"], anchor="lm", edge="#05070F")
            gtext(img, x1 - 76, y, v, "trump", 26, C["cream"], anchor="rm", edge="#05070F", edge_w=0.6)
            y += 38
        K.crest_rule(img, 640, y - 8, 440, crest=False, scale=0.36)
        gtext(img, x0 + 76, y + 30, "Total", "jupiter", 38, C["gold_hi"], anchor="lm", gilt=True, edge="#140A02")
        gtext(img, x1 - 76, y + 30, "251,880", "trump", 44, C["gold_hi"], anchor="rm", gilt=True, edge="#140A02",
              glow="#FFB45E", glow_k=0.3)
        # the ace callout: a lit moon, ACED and NEW BEST in gilt, with a glint (designer M5)
        cy = y + 98
        sl, X, Y = img.win(x0 + 200, cy, 170)
        img.over(sl, hexc("#2A1206"), img.cov(sd_rrect(X, Y, x0 + 60, cy - 30, x0 + 330, cy + 30, 8)) * 0.85)
        gilt_frame(img, x0 + 60, cy - 30, x0 + 330, cy + 30, scale=0.24, corners=False)
        draw_moon(img, x0 + 96, cy, 17, "orange", "lit", variant=1, sky="#2A1206")
        gtext(img, x0 + 126, cy - 8, "ACED", "trump_s", 32, C["gold_hi"], anchor="lm", gilt=True, edge="#140A02",
              glow="#FFD27A", glow_k=0.4)
        gtext(img, x0 + 216, cy - 8, "NEW BEST", "trump_s", 22, hexc_to(cid["accent"], 0.2), anchor="lm", edge="#140A02")
        gtext(img, x0 + 126, cy + 17, "ace score 240,000", "axis", 14, INK2, anchor="lm")
        K.medallion(img, cid, x1 - 100, cy, 26, "lv_ring", glow=0.5)
        gtext(img, x1 - 140, cy - 9, "Cid Garlond", "jupiter", 23, C["cream"], anchor="rm", edge="#05070F")
        gtext(img, x1 - 140, cy + 12, "Brass Wings ×1", "axis", 14, hexc_to(cid["accent"], 0.3), anchor="rm")
        btn_y = y1 - 64
        K.button(img, x0 + 50, btn_y, x0 + 196, btn_y + 42, "Replay", 28)
        K.button(img, x0 + 210, btn_y, x0 + 340, btn_y + 42, "Map", 28)
        K.button(img, x0 + 356, btn_y, x1 - 50, btn_y + 42, "Next: 3-3", 30, "focus")
    else:
        x0, y0, x1, y1 = 100, 46, 540, 470
        panel(img, x0, y0, x1, y1, jewel=cid["accent"], scale=0.32)
        banner(img, 320, y0 + 2, "LEVEL CLEAR", 28, 260, accent=cid["accent"])
        K.title(img, 320, y0 + 46, "The Holy See", 32, anchor="mm")
        gtext(img, 320, y0 + 68, "3-2 · won with 3 balls to spare", "axis", 12, INK2, anchor="mm")
        y = y0 + 94
        for lab, v in rows:
            gtext(img, x0 + 34, y, lab, "jupiter", 19, C["cream"], anchor="lm", edge="#05070F")
            gtext(img, x1 - 34, y, v, "trump", 18, C["cream"], anchor="rm")
            y += 25
        gtext(img, x0 + 34, y + 14, "Total", "jupiter", 26, C["gold_hi"], anchor="lm", gilt=True, edge="#140A02")
        gtext(img, x1 - 34, y + 14, "251,880", "trump", 30, C["gold_hi"], anchor="rm", gilt=True, edge="#140A02")
        draw_moon(img, x0 + 46, y + 54, 10, "orange", "lit", variant=1, sky="#141C3A")
        gtext(img, x0 + 64, y + 50, "ACED", "trump_s", 20, C["gold_hi"], anchor="lm", gilt=True)
        gtext(img, x0 + 112, y + 50, "NEW BEST", "trump_s", 15, hexc_to(cid["accent"], 0.2), anchor="lm")
        gtext(img, x0 + 64, y + 68, "ace score 240,000", "axis", 12, INK2, anchor="lm")
        K.medallion(img, cid, x1 - 54, y + 50, 16, "lv_ring", glow=0.4)
        gtext(img, x1 - 54, y + 78, "Cid \u00b7 Brass Wings \u00d71", "axis", 12.5, hexc_to(cid["accent"], 0.3), anchor="mm")
        K.button(img, x0 + 24, y1 - 46, x0 + 128, y1 - 16, "Replay", 20)
        K.button(img, x0 + 138, y1 - 46, x0 + 232, y1 - 16, "Map", 20)
        K.button(img, x0 + 242, y1 - 46, x1 - 24, y1 - 16, "Next: 3-3", 21, "focus")
    return img


# ------------------------------------------------------------------------------------------------ pause
def toggle(img, x, y, on, small=False, accent="#E69461"):
    """A switch: its track filled in the accent when on, with On/Off beside it (UX m6)."""
    w, h = (44, 22) if not small else (34, 17)
    K.pill(img, x - w, y - h / 2, x, y + h / 2, "focus" if on else "locked")
    if on:
        sl, X, Y = img.win(x - w / 2, y, w)
        img.over(sl, hexc(accent), img.cov(sd_rrect(X, Y, x - w + 3, y - h / 2 + 3, x - 3, y + h / 2 - 3, h / 2 - 3)) * 0.7)
    kx = x - h / 2 - 1 if on else x - w + h / 2 + 1
    sl, X, Y = img.win(kx, y, h)
    d = np.sqrt((X - kx) ** 2 + (Y - y) ** 2)
    img.over(sl, ramp(np.clip((Y - y + h / 2) / h, 0, 1), [(0, "#FFF2C8"), (1, "#C8962E")]), img.cov(d - h * 0.36))
    gtext(img, x - w - 10, y, "On" if on else "Off", "axis", 14 if not small else 12, C["cream"] if on else INK2, anchor="rm")


def stepper(img, x, y, val, small=False):
    """A value between two gilt chevron buttons that hug it (UX m9): 20 x 20 at 1280, 16 x 16 at 640."""
    sz = 16 if not small else 14
    b = 20 if not small else 16
    w = max(gwidth(val, "axis", sz), 34 if not small else 28)
    xr = x - b / 2
    xl = x - b - 8 - w - 8 - b / 2
    for (bx, ch) in ((xl, "‹"), (xr, "›")):
        K.pill(img, bx - b / 2, y - b / 2, bx + b / 2, y + b / 2, "normal")
        gtext(img, bx, y - 0.5, ch, "axis", sz + 2, C["gold_hi"], anchor="mm")
    gtext(img, (xl + xr) / 2, y, val, "axis", sz, C["gold_hi"], anchor="mm")


def pause(W=1280, H=800):
    small = W < 700
    px, _ = board("base-p1", small=small, hud_kw=dict(stage="3-3", score="142,700", balls=6, cleared=12, mult="×2",
                                                       oranges=13, carrier="Wings", turns=0, active=False))
    img = window(px, W, H, "base-p1")
    sl, X, Y = img.full()
    img.mul(sl, hexc("#03040C"), np.full(X.shape, 0.6, np.float32))
    settings = (("Reduce motion", False), ("Peg marks", ST.PEG_MARKS), ("Decoration", "Full"), ("Sound", "70%"))
    if not small:
        x0, y0, x1, y1 = 420, 70, 860, 760
        panel(img, x0, y0, x1, y1)
        K.title(img, 640, y0 + 58, "Paused", 60, anchor="mm")
        gtext(img, 640, y0 + 96, "3-3 The Airship Road · 6 balls · 13 oranges left", "axis", 15, INK2, anchor="mm")
        K.crest_rule(img, 640, y0 + 124, 320, crest=True, scale=0.4)
        K.button(img, x0 + 66, y0 + 154, x1 - 66, y0 + 206, "Resume", 36, "focus")
        K.button(img, x0 + 66, y0 + 226, x1 - 66, y0 + 268, "Restart level", 28, "danger")
        # the hold in progress (one frame of it): a lighter fill sweeping the pill from the left
        sl, X, Y = img.win((x0 + x1) / 2, y0 + 247, (x1 - x0) / 2)
        hold = img.cov(sd_rrect(X, Y, x0 + 70, y0 + 230, x0 + 70 + (x1 - x0 - 140) * 0.42, y0 + 264, 17))
        img.add(sl, hexc("#FF9AB8"), hold * 0.22)
        gtext(img, 640, y0 + 286, "hold to restart: this level's score is lost", "axis", 14, "#F0C8D8", anchor="mm")
        K.button(img, x0 + 66, y0 + 310, x1 - 66, y0 + 352, "Leave to the map", 28, "danger")
        gtext(img, 640, y0 + 370, "hold to leave: this level's progress is lost", "axis", 14, "#F0C8D8", anchor="mm")
        K.button(img, x0 + 66, y0 + 394, x1 - 66, y0 + 436, "Options", 28)
        K.crest_rule(img, 640, y0 + 468, 320, crest=False, scale=0.36)
        for i, (lab, val) in enumerate(settings):
            yy = y0 + 502 + i * 36
            gtext(img, x0 + 76, yy, lab, "axis", 16, C["cream"], anchor="lm")
            if isinstance(val, bool):
                toggle(img, x1 - 76, yy, val)
            else:
                stepper(img, x1 - 76, yy, val)
        gtext(img, 640, y1 - 46, "Moonfall pauses itself in combat, duties and cutscenes.", "axis", 14, INK2, anchor="mm")
    else:
        x0, y0, x1, y1 = 160, 44, 480, 476
        panel(img, x0, y0, x1, y1, scale=0.32)
        K.title(img, 320, y0 + 34, "Paused", 40, anchor="mm")
        K.button(img, x0 + 30, y0 + 64, x1 - 30, y0 + 100, "Resume", 26, "focus")
        K.button(img, x0 + 30, y0 + 110, x1 - 30, y0 + 140, "Restart level", 20, "danger")
        gtext(img, 320, y0 + 154, "hold to restart", "axis", 12.5, "#F0C8D8", anchor="mm")
        K.button(img, x0 + 30, y0 + 168, x1 - 30, y0 + 198, "Leave to the map", 20, "danger")
        gtext(img, 320, y0 + 212, "hold to leave", "axis", 12.5, "#F0C8D8", anchor="mm")
        K.button(img, x0 + 30, y0 + 226, x1 - 30, y0 + 256, "Options", 20)
        for i, (lab, val) in enumerate(settings):
            yy = y0 + 286 + i * 30
            gtext(img, x0 + 32, yy, lab, "axis", 13, C["cream"], anchor="lm")
            if isinstance(val, bool):
                toggle(img, x1 - 32, yy, val, small=True)
            else:
                stepper(img, x1 - 32, yy, val, small=True)
    return img


# ------------------------------------------------------------------------------------------------ peg marks
def mark(img, x, y, r, kind):
    """The colour-blind assist (UX m13): an engraved mark on each kind's face; blue carries none."""
    sl, X, Y = img.win(x, y, r + 1)
    u, v = (X - x) / r, (Y - y) / r
    if kind == "orange":                      # a crescent
        m = (np.sqrt(u * u + v * v) < 0.52) & (np.sqrt((u - 0.22) ** 2 + (v + 0.12) ** 2) > 0.42)
    elif kind == "green":                     # a leaf: pointed at both ends, with its midrib
        a_, b_ = (u + v) / 1.414, (v - u) / 1.414
        m = (np.sqrt((np.abs(b_) + 0.42) ** 2 + a_ ** 2) < 0.78) & (np.abs(a_) < 0.62)
        m &= ~((np.abs(b_) < 0.05) & (np.abs(a_) < 0.5))
    elif kind == "purple":                    # a four-point star, 55% of the peg, with a light rim
        st = (np.abs(u) ** 0.5 + np.abs(v) ** 0.5)
        rim = (st < 1.02) & (st >= 0.86)
        img.over(sl, hexc("#F4ECFF"), blur(rim.astype(np.float32), 0.5 * img.S) * 0.8)
        m = st < 0.86
    else:
        return
    img.mul(sl, hexc("#1A0C04"), blur(m.astype(np.float32), 0.6 * img.S) * 0.78)


def pegmarks():
    """base-p1 with peg marks on, beside the same board as a deuteranope sees it (Machado 2009, severity 1)."""
    from board import pieces

    def after(img, level, colours, aimed):
        for i, (k, d) in enumerate(pieces(level)):
            if k == "peg" and colours[i] != "blue":
                mark(img, d["x"], d["y"], d.get("r", 10), colours[i])
    px, _ = board("base-p1", after=after, hud_kw=dict(stage="3-3", carrier="Wings", turns=0, active=False))
    from PIL import Image as _I
    one = np.asarray(_I.fromarray((np.clip(px, 0, 1) * 255).astype(np.uint8)).resize((800, 600), _I.LANCZOS), np.float32) / 255
    lin = np.where(one <= 0.04045, one / 12.92, ((one + 0.055) / 1.055) ** 2.4)
    M = np.array([[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]])
    sim = np.clip(lin @ M.T, 0, 1)
    sim = np.where(sim <= 0.0031308, sim * 12.92, 1.055 * np.power(sim, 1 / 2.4) - 0.055)
    out = Img(1640, 660, 1.0, px=np.full((660, 1640, 3), 0.03, np.float32))
    out.px[50:650, 10:810] = one
    out.px[50:650, 830:1630] = sim
    gtext(out, 410, 26, "Peg marks on", "axis", 16, C["cream"], anchor="mm")
    gtext(out, 1230, 26, "The same board, as a deuteranope sees it", "axis", 16, C["cream"], anchor="mm")
    return out
