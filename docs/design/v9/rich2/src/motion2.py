"""Ambient motion previews (rich pass 2, round 2): short seamless loops of the motion the spec describes, over the
stills. Nothing in the layout moves: pegs, bricks, type, buttons and frames are masked out of every moving layer.

  py -3 motion2.py [title|play|fever]     writes motion/<name>.png: true-colour APNG, 6 s loops at 8 fps
                                          title 600 x 375 (the whole 1280 x 800 title at half size);
                                          play and fever 640 x 480 (the board at 0.8x: the minimum window)

Round 2 (reviewers' round 1): true colour (no shared palette: peg hues kept exactly), the whole title, fireflies placed
so their whole wander and halo keep 8 units from every piece (F5, checked), the lantern flicker anchored on the
bucket's lantern, the active carrier's medallion glow, the beams and fireflies at the top of their allowed range, and a
Fever preview (the moon swells, the sky lifts, moondust bursts once, the cups light).
"""
import math
import sys

import numpy as np
from PIL import Image

from r2lib import OUT_MOTION, OUT_SCREENS, OUT_COMP, RICH, LUM, blur, hexc, load_rgb, screen, smooth
from rich_lib import fbm

LOOP, FPS = 6.0, 8
N = int(LOOP * FPS)


def gilt_mask(px):
    mx, mn = px.max(-1), px.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-3)
    return smooth(0.25, 0.45, sat) * smooth(0.35, 0.6, mx) * (px[..., 0] > px[..., 2] + 0.12) * (px[..., 0] >= px[..., 1] * 0.95)


def star_points(px, mask, n, seed, thr=0.30):
    Y = px @ LUM
    loc = Y - blur(Y, 3)
    cand = np.argwhere((loc > 0.06) & (Y > thr) & (mask > 0.5))
    rng = np.random.default_rng(seed)
    if len(cand) > n:
        cand = cand[rng.choice(len(cand), n, replace=False)]
    return cand


def dot_layer(h, w, pts, rad, amps, col):
    out = np.zeros((h, w, 3), np.float32)
    for (y, x), a, r in zip(pts, amps, rad):
        if a <= 0.002:
            continue
        y0, y1 = int(max(0, y - r * 4)), int(min(h, y + r * 4 + 1))
        x0, x1 = int(max(0, x - r * 4)), int(min(w, x + r * 4 + 1))
        if y1 <= y0 or x1 <= x0:
            continue
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        out[y0:y1, x0:x1] += (np.exp(-((xx - x) ** 2 + (yy - y) ** 2) / (2 * r * r)) * a)[..., None] * hexc(col)
    return out


def save_apng(frames, path, size):
    ims = [Image.fromarray((np.clip(f, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").resize(size, Image.LANCZOS) for f in frames]
    path.parent.mkdir(parents=True, exist_ok=True)
    ims[0].save(path, save_all=True, append_images=ims[1:], duration=int(1000 / FPS), loop=0, format="PNG")
    for k, name in ((0, "frame0"), (N // 4, "frame-1.5s"), (N // 2, "frame-3s")):
        ims[k].save(path.with_name(f"{path.stem}-{name}.png"))
    return path.stat().st_size


def ui_mask(base, boxes):
    """Where motion must not show: type and UI (gilt or bright detail, grown 4 px) and the given boxes."""
    h, w = base.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    Y = base @ LUM
    m = ((gilt_mask(base) > 0.3) | (Y > 0.62)).astype(np.float32)
    m = np.clip(blur(m, 4) * 4, 0, 1)
    for (x0, y0, x1, y1) in boxes:
        m = np.maximum(m, ((xx > x0) & (xx < x1) & (yy > y0) & (yy < y1)).astype(np.float32))
    return m


def title_loop():
    """The whole title (1280 x 800): moondust drifting up and right (5-9 px/s), stars twinkling (2 and 3 s, +/-35%),
    two mist layers crossing the lower ground (4 and 9 px/s: parallax), the moon's halo breathing (6 s, +/-6%), one
    glint across the logotype (0.8 s). Moving layers are masked out of the logotype, the subtitle, every button and
    the Continue card (rule 3)."""
    base = load_rgb(OUT_SCREENS / "title-1280.png")
    h, w = base.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    ui = ui_mask(base, [(100, 180, 560, 330), (160, 340, 500, 670), (840, 508, 1256, 784)])
    free = 1 - ui
    sky = smooth(360, 220, yy) * free
    stars = star_points(base, sky, 220, 4)
    rng = np.random.default_rng(7)
    sph = rng.random(len(stars)) * 2 * math.pi
    sper = rng.choice([2.0, 3.0], len(stars))
    gilt = gilt_mask(base) * (yy > 190) * (yy < 300) * (xx > 100) * (xx < 560)
    nd = 140
    dpos = np.stack([rng.uniform(0, w, nd), rng.uniform(0, h, nd)], 1)
    dvel = np.stack([rng.uniform(5, 9, nd), -rng.uniform(2, 5, nd)], 1)
    dph = rng.random(nd)
    mist1 = fbm(h, w * 2, 160, 4, 11)
    mist2 = fbm(h, w * 2, 80, 4, 12)
    ground = smooth(480, 680, yy)
    d_moon = np.sqrt((xx - 200) ** 2 + (yy - 112) ** 2)
    halo = np.exp(-(np.clip(d_moon - 40, 0, None) / 70) ** 2) * (d_moon > 40)
    frames = []
    for f in range(N):
        t = f / FPS
        px = base.copy()
        o1, o2 = int(4 * t) % w, int(9 * t) % w
        m1 = np.roll(mist1, -o1, 1)[:, :w]
        m2 = np.roll(mist2, -o2, 1)[:, :w]
        mist = (smooth(0.55, 0.85, m1) * 0.07 + smooth(0.6, 0.9, m2) * 0.06) * ground * free
        px = screen(px, hexc("#AFC0F0") * mist[..., None])
        amp = 0.35 * np.sin(2 * math.pi * t / sper + sph)
        px = screen(px, dot_layer(h, w, stars, np.full(len(stars), 1.2), np.clip(amp, 0, None) * 0.6, "#E8EEFF") * sky[..., None])
        life = (dph + t / LOOP) % 1.0
        pos = dpos + dvel * (life[:, None] * LOOP)
        pos[:, 0] %= w
        pos[:, 1] %= h
        dust = dot_layer(h, w, [(p[1], p[0]) for p in pos], np.full(nd, 1.3), np.sin(life * math.pi) ** 2 * 0.45, "#DCE4FF")
        px = screen(px, dust * free[..., None])
        br = 0.06 * math.sin(2 * math.pi * t / LOOP)
        px = screen(px, hexc("#C9D6FF") * (halo * 0.14 * (1 + br) * free)[..., None])
        g0 = (t - 1.2) / 0.8
        if 0 <= g0 <= 1:
            band = np.exp(-(((xx + (yy - 236) * 0.6) - (80 + g0 * 560)) / 18) ** 2)
            px = screen(px, hexc("#FFF4D0") * (band * gilt * 0.55 * math.sin(g0 * math.pi))[..., None])
        frames.append(np.clip(px, 0, 1))
    return frames, (600, 375)


def piece_ring(level, h, w, s, grow):
    """Mask (at s px per unit) of every peg grown by `grow` units."""
    from board import pieces
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    X, Y = (xx + 0.5) / s, (yy + 0.5) / s
    m = np.zeros((h, w), np.float32)
    for k, d in pieces(level):
        if k == "peg":
            m = np.maximum(m, ((X - d["x"]) ** 2 + (Y - d["y"]) ** 2 < (d.get("r", 10) + grow) ** 2).astype(np.float32))
    return m


def play_loop():
    """In play, base-p3 (The Moonlit Post) as composited: the moonbeams breathe (6 s, +/-15%) with dust drifting in
    them; fireflies wander small closed loops (9 units, 6 s) and pulse (2 and 3 s), each placed so its whole path and
    halo keep 8 units from every piece; the cart's lantern flickers (two sines, +/-10%); the active carrier's medallion
    glows (3 s, +/-10%); one glint along the top rail."""
    import dress2
    from framecheck import piece_distance
    from board import load_level
    S = 2
    comp = load_rgb(OUT_COMP / "base-p3@2x.png")
    h, w = comp.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    level = load_level(RICH / "levels" / "base-p3.json")
    board = ((X > 75) & (X < 725) & (Y > 41) & (Y < 594)).astype(np.float32)
    free = board * (1 - piece_ring(level, h, w, S, 2.5))
    far = board * (1 - piece_ring(level, h, w, S, 9.0))          # particles keep 8 units (and their size) away
    shaft_kw = dict(origin=(150, 70), angles=(38, 50, 62, 74, 86), widths=(16, 24, 14, 26, 12), k=0.08, col="#D6E4FF",
                    reach=760, near=40)
    beams = dress2.shafts(np.zeros((h, w, 3), np.float32), S, **shaft_kw)
    beam_m = np.clip(beams.mean(-1) / max(beams.mean(-1).max(), 1e-3) * 2.5, 0, 1)
    dist = piece_distance(level)
    ff = []
    for (x, y, s_) in dress2.fireflies("base-p3"):
        halo = 4.5 * s_ * 2.2          # the halo reaches about 2 sigma before it is invisible
        if all(dist[int(y + math.sin(2 * a) * 4), int(x + math.cos(a) * 9)] >= 8 + halo for a in np.linspace(0, 2 * math.pi, 24)):
            ff.append((x, y, s_))
    rng = np.random.default_rng(5)
    fph = rng.random(len(ff)) * 2 * math.pi
    fper = rng.choice([2.0, 3.0], len(ff))
    nd = 90
    dpos = np.stack([rng.uniform(120, 600, nd), rng.uniform(80, 420, nd)], 1)
    dph = rng.random(nd)
    gilt = gilt_mask(comp) * (1 - board) * smooth(44, 30, Y)
    import composite2
    bx = composite2.pick_bucket_x(level, set())
    lx, ly = bx + 50, 541.0
    lant = np.exp(-(((X - lx) / 5) ** 2 + ((Y - ly) / 7) ** 2))
    pool = np.exp(-(((X - lx) / 26) ** 2 + ((Y - 590) / 4) ** 2))
    mx, my = 766.0, 232.0
    dm = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    mglow = np.exp(-(np.clip(dm - 21, 0, None) / 7.0) ** 2) * (dm > 20) * (dm < 40)
    report = {"fireflies": len(ff), "min_path_clearance": None}
    frames = []
    for f in range(N):
        t = f / FPS
        px = comp.copy()
        # the beams: their break-up drifts on a 5-unit circle (light through moving leaves; round 3, GD N2), and they
        # breathe +/-15%; the difference from the still is added (the still holds the beams at rest)
        th = 2 * math.pi * t / LOOP
        moved = dress2.shafts(np.zeros((h, w, 3), np.float32), S, noise_shift=(5 * math.cos(th) - 5, 5 * math.sin(th)),
                              **shaft_kw)
        k = 0.15 * math.sin(2 * math.pi * t / LOOP)
        px = np.clip(px + ((moved - beams) + moved * k) * free[..., None], 0, 1)
        life = (dph + t / LOOP) % 1.0
        pos = dpos + np.stack([np.full(nd, 6.0), np.full(nd, 9.0)], 1) * (life[:, None] * LOOP)
        dust = dot_layer(h, w, [(p[1] * S, p[0] * S) for p in pos], np.full(nd, 1.6), np.sin(life * math.pi) ** 2 * 0.5, "#E4ECFF")
        px = screen(px, dust * (beam_m * far)[..., None])
        if ff:
            ang = 2 * math.pi * t / LOOP + fph
            fp = np.array([(x + math.cos(a) * 9, y + math.sin(2 * a) * 4) for (x, y, _), a in zip(ff, ang)])
            fa = 0.55 + 0.45 * np.sin(2 * math.pi * t / fper + fph)
            core = dot_layer(h, w, [(p[1] * S, p[0] * S) for p in fp], np.full(len(ff), 1.5 * S), fa * 0.95, "#FFD27A")
            hal = dot_layer(h, w, [(p[1] * S, p[0] * S) for p in fp], np.full(len(ff), 4.5 * S), fa * 0.22, "#FFB45E")
            px = screen(px, (core + hal) * board[..., None])
        fl = 0.06 * math.sin(2 * math.pi * t * 2.0) + 0.04 * math.sin(2 * math.pi * t * 3.5 + 1.0)
        px = screen(px, hexc("#FFB868") * ((lant * 0.5 + pool * 0.25) * max(0.0, 0.10 + fl))[..., None])
        px = screen(px, hexc("#E69461") * (mglow * (0.22 + 0.06 * math.sin(2 * math.pi * t / 3.0)))[..., None])
        g0 = (t - 3.0) / 0.8
        if 0 <= g0 <= 1:
            band = np.exp(-(((X + Y * 0.5) - (g0 * 900 - 50)) / 14) ** 2)
            px = screen(px, hexc("#FFF4D0") * (band * gilt * 0.6 * math.sin(g0 * math.pi))[..., None])
        frames.append(np.clip(px, 0, 1))
    # the check: in the 2.5-8 unit band round every peg, no frame lifts luma by more than 0.03 over the still
    band = piece_ring(level, h, w, S, 8.0) * (1 - piece_ring(level, h, w, S, 2.5)) * board
    base = comp @ LUM
    lift = max(float(((fr_ @ LUM - base) * band).max()) for fr_ in frames)
    report["max_lift_near_pegs"] = round(lift, 4)
    print("play:", report)
    return frames, (640, 480)


def fever_loop():
    """Fever's arrival as a loop: over the first 1.5 s the moon swells and brightens, the sky lifts toward the carrier's
    colour and moondust bursts once from the last orange, the banner lands; then it holds, the cups' light breathing
    (3 s, +/-15%) and a glint crossing the banner's laurel. (In the game the arrival plays once; the preview loops.)"""
    import play2
    frames = []
    keys = {}
    for f in range(N):
        t = f / FPS
        a = min(1.0, t / 1.5)
        key = round(a, 2) if t < 1.5 else 1.0
        if key not in keys:
            # the build-up (round 3, GD Nit): the banner arrives over the first second, after the light has begun
            img = play2.fever(800, 600, t=key, banner_alpha=float(np.clip((t - 0.4) / 0.6, 0, 1)))
            keys[key] = img.px.copy()
        px = keys[key].copy()
        h, w = px.shape[:2]
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        if t >= 1.5:
            cups = np.exp(-(((yy - 566) / 14) ** 2)) * ((xx > 75) & (xx < 725))
            px = screen(px, hexc("#FFD27A") * (cups * 0.10 * (0.5 + 0.5 * math.sin(2 * math.pi * t / 3.0)))[..., None])
            g0 = (t - 3.5) / 0.8
            if 0 <= g0 <= 1:
                gm = gilt_mask(keys[1.0]) * (yy > 180) * (yy < 300)
                band = np.exp(-(((xx + yy * 0.5) - (200 + g0 * 520)) / 16) ** 2)
                px = screen(px, hexc("#FFF4D0") * (band * gm * 0.6 * math.sin(g0 * math.pi))[..., None])
        frames.append(np.clip(px, 0, 1))
    return frames, (640, 480)


if __name__ == "__main__":
    which = sys.argv[1:] or ["title", "play", "fever"]
    for n in which:
        frames, size = {"title": title_loop, "play": play_loop, "fever": fever_loop}[n]()
        mb = save_apng(frames, OUT_MOTION / f"{n}.png", size) / 1e6
        print(n, len(frames), "frames", size, round(mb, 2), "MB")
