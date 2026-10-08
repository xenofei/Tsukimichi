"""Our own painting for 3-2 "Above the Cloud Sea": the airship lane's beacons over a moonlit sea of clouds.

The full moon is rising, misted, out of the cloud sea at the end of the lane, and it backlights the billows: every crest has a
silver lining toward it, every face toward us is in cool shadow. The lane runs straight at the moon between two rows of
beacons, needles of stone standing out of the clouds, each with a brass lantern cage at its head; a farther pair, and
a farther still, are only sparks on the way to the moon. Each near lantern throws a beam up into the night, the near
ones tall, the far ones short, and the two rows' beams fan outward like a crown of light round the lane; each beacon
lights a pool of the cloud tops round its foot in the aether's pale cyan. Thin streaks of high cloud lie across the
sky; stars thin toward the moon.

The cloud sea is a field of cumulus domes (with smaller domes on their shoulders) seen in true perspective from above
it, so the billows shrink and crowd toward the horizon; the beacons stand in the same space and sink into the billows
in front of them.

FEATURES (board units): the beacons' heads (x, y, scale), each beam's centreline from the head up to where its pegs
stop, each guide-string between two beacons of a row, the lane's axis, the horizon and the moon.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, ramp, screen, smooth

HORIZON = 246.0
FOCAL = 400.0                 # px per world unit at depth 1
CAM = 150.0                   # the eye's height above the cloud floor
MOON = (400.0, 220.0, 30.0)
# The moon rises through the cloud sea's haze, misted: its face (OKLab L about 0.68) stays darker than the ball (0.885)
# and the aim guide's dots (0.96), since it sits on the launcher's axis where every central shot starts (UX MR 3 round
# 1, M1: the bright face, L 0.934, swallowed both), and its glow in the sky is halved.
MOON_MIST = 0.56              # the face's sRGB under the haze (maria kept)
MOON_GLOW = 0.10
LANE = 58.0                   # the lane's half-width (world)
HEAD = 100.0                  # the lanterns' height (world)
# the beacons: name, side (-1 left, +1 right), depth (world), the beam's lean from the vertical (degrees, outward), the
# beam's length on the board to its last peg (the painted beam runs on beyond it and fades); a beam of 0 is a spark only
BEACONS = [("L1", -1, 89.0, 5.0, 290.0), ("L2", -1, 141.0, 13.0, 214.0), ("L3", -1, 242.0, 23.0, 146.0),
           ("L4", -1, 464.0, 34.0, 84.0), ("L5", -1, 900.0, 0.0, 0.0), ("L6", -1, 1700.0, 0.0, 0.0)]
BEACONS += [("R" + n[1], 1, z, lean, L) for (n, _s, z, lean, L) in BEACONS]
BEAM_COL = "#A6E2FF"


def head_at(b):
    (_n, side, z, _lean, _L) = b
    return (400.0 + FOCAL * side * LANE / z, HORIZON + FOCAL * (CAM - HEAD) / z, 89.0 / z)


def beam_end(b, frac=1.0):
    (_n, side, _z, lean, length) = b
    x, y, _s = head_at(b)
    a = math.radians(lean * side)
    return (x + math.sin(a) * length * frac, y - math.cos(a) * length * frac)


def string(a, c, n=24):
    """The lane's guide-string slung between two beacons of a row, a catenary in world space (it sags a tenth of its
    span below the cages, which hang it 8 units under the lanterns), as board points."""
    (_n, side, z0, _l, _L), (_m, _s, z1, _k, _K) = a, c
    sag = (z1 - z0) * 0.10
    pts = []
    for i in range(n + 1):
        t = i / n
        z = z0 + (z1 - z0) * t
        h = HEAD - 8 - sag * 4 * t * (1 - t)
        pts.append((400.0 + FOCAL * side * LANE / z, HORIZON + FOCAL * (CAM - h) / z))
    return pts


def strings():
    """Every string of the two rows, near to far: (name, points)."""
    out = []
    for side in ("L", "R"):
        row = [b for b in BEACONS if b[0][0] == side]
        for a, c in zip(row, row[1:]):
            out.append((f"string {a[0]}-{c[0]}", string(a, c)))
    return out


FEATURES = {
    "beacons": {"heads": {b[0]: [round(c, 1) for c in head_at(b)] for b in BEACONS if b[4] > 0}},
    **{f"beam {b[0]}": {"points": [[round(c, 1) for c in head_at(b)[:2]], [round(c, 1) for c in beam_end(b)]]}
       for b in BEACONS if b[4] > 0},
    **{name: {"points": [[round(x, 1), round(y, 1)] for (x, y) in pts]} for (name, pts) in strings()},
    "lane": {"points": [[400, 594], [400, HORIZON]]},
    "horizon": HORIZON,
    "moon": list(MOON),
}

# ------------------------------------------------------------------------------------------------ the cloud field
TEX = 1024                    # texels a side; 0.5 world units each, so the field tiles every 512 units
UNIT = 2.0                    # texels per world unit


def _field():
    """The cloud sea's height (world units) on a tiling grid: big cumulus domes, stretched a little across the view,
    with smaller domes heaped on their shoulders, and a fine boil on top."""
    rng = np.random.default_rng(7)
    h = np.zeros((TEX, TEX), np.float32)

    def stamp(cx, cy, r, base, squash, stretch):
        rx, ry_ = r * stretch * UNIT, r * UNIT
        x0, x1 = int(cx - rx) - 1, int(cx + rx) + 2
        y0, y1 = int(cy - ry_) - 1, int(cy + ry_) + 2
        xs = np.arange(x0, x1) % TEX
        ys = np.arange(y0, y1) % TEX
        u = (np.arange(x0, x1, dtype=np.float32) - cx) / rx
        v = (np.arange(y0, y1, dtype=np.float32) - cy) / ry_
        d = np.clip(1 - v[:, None] ** 2 - u[None, :] ** 2, 0, 1)
        dome = base + np.sqrt(d) * r * squash
        dome = np.where(d > 0, dome, 0)
        sub = h[np.ix_(ys, xs)]
        h[np.ix_(ys, xs)] = np.maximum(sub, dome)

    for _ in range(150):
        r = rng.uniform(26, 62)
        stamp(rng.uniform(0, TEX), rng.uniform(0, TEX), r, rng.uniform(0, 8), 0.8, rng.uniform(1.3, 1.8))
    for (n, lo, hi, sink) in ((420, 10, 22, 0.5), (700, 5, 10, 0.45)):
        for _ in range(n):
            cx, cy = rng.uniform(0, TEX), rng.uniform(0, TEX)
            r = rng.uniform(lo, hi)
            base = h[int(cy) % TEX, int(cx) % TEX] - r * sink
            if base > 3:
                stamp(cx, cy, r, base, 0.8, rng.uniform(1.0, 1.4))
    boil = fbm(TEX, TEX, 6.0, 3, 19) - 0.5
    return h + boil * 1.6


def _sample(tex, u, v):
    """Bilinear sample of a tiling texture at texel coordinates (u across, v down)."""
    u0 = np.floor(u).astype(np.int64)
    v0 = np.floor(v).astype(np.int64)
    fu, fv = (u - u0).astype(np.float32), (v - v0).astype(np.float32)
    u0, v0 = u0 % TEX, v0 % TEX
    u1, v1 = (u0 + 1) % TEX, (v0 + 1) % TEX
    a, b, c, d = tex[v0, u0], tex[v0, u1], tex[v1, u0], tex[v1, u1]
    return (a * (1 - fu) + b * fu) * (1 - fv) + (c * (1 - fu) + d * fu) * fv


def _cloud_sea(S, W, H):
    """Renders the field in perspective, near to far: per pixel the depth seen (inf for sky), the height, the slope
    across and away, and how high the seen point stands above its neighbourhood (for the valleys' shade)."""
    field = _field()
    mips = [field] + [blur(field, s) for s in (1.0, 2.0, 4.0, 8.0, 16.0)]
    grads = []
    for m in mips:
        gv, gu = np.gradient(m)
        grads.append((gu * UNIT, gv * UNIT))
    lift = field - blur(field, 24.0)
    step = 0.5 / S
    near = np.arange(HORIZON + FOCAL * CAM / 50.0, 700.0, -4 * step, dtype=np.float32)
    y_flat = np.concatenate([near, np.arange(700.0, HORIZON + FOCAL * CAM / 30000.0, -step, dtype=np.float32)])
    z = FOCAL * CAM / (y_flat - HORIZON)                         # slice depths, near to far
    cols = (np.arange(W, dtype=np.float32) + 0.5) / S
    xw = (cols[None, :] - 400.0) * z[:, None] / FOCAL
    u = xw * UNIT + 300.0
    v = -z[:, None] * UNIT + np.zeros_like(xw) + 211.0
    texel_per_px = z * UNIT / (FOCAL * S)                        # texels under one device pixel (the mip to read)
    level = np.clip(np.floor(np.log2(np.maximum(texel_per_px, 1.0)) + 0.5), 0, len(mips) - 1).astype(int)
    hs = np.zeros_like(xw)
    gx = np.zeros_like(xw)
    gz = np.zeros_like(xw)
    for lv in range(len(mips)):
        sel = level == lv
        if not sel.any():
            continue
        hs[sel] = _sample(mips[lv], u[sel], v[sel])
        gx[sel] = _sample(grads[lv][0], u[sel], v[sel])
        gz[sel] = -_sample(grads[lv][1], u[sel], v[sel])           # v runs against depth
    lf = _sample(blur(lift, 2.0), u, v)
    ytop = HORIZON + FOCAL * (CAM - hs) / z[:, None]
    M = np.minimum.accumulate(ytop, axis=0)                       # the visible top so far, near to far
    rows = (np.arange(H, dtype=np.float32) + 0.5) / S
    idx = np.empty((H, W), np.int64)
    negM = -M
    for c in range(W):
        idx[:, c] = np.searchsorted(negM[:, c], -rows, side="left")
    sky = idx >= len(z)
    idc = np.clip(idx, 0, len(z) - 1)
    cc = np.broadcast_to(np.arange(W)[None, :], (H, W))
    Z = np.where(sky, np.inf, z[idc])
    return dict(sky=sky, Z=Z, h=hs[idc, cc], gx=gx[idc, cc], gz=gz[idc, cc], lift=lf[idc, cc],
                ytop=ytop[idc, cc], xw=xw[idc, cc])


def _towers(S, W, H, ss=3):
    """Each beacon's needle and cage as (coverage, depth): a tapering stone needle from far below up to a sill, the
    cage's four ribs and its pointed roof with a finial."""
    out = []
    for b in BEACONS:
        x, y, s = head_at(b)
        im = Image.new("L", (W * ss, H * ss), 0)
        dr = ImageDraw.Draw(im)
        k = S * ss
        foot = y + FOCAL * HEAD / b[2]
        rng = np.random.default_rng(int(b[2]) + (0 if b[1] < 0 else 1000))
        # a crag of stone: wide at the foot, worn in steps and ledges, a dressed shaft under the cage
        left, right = [], []
        n = 14
        for i in range(n + 1):
            t = i / n
            yy_ = y + 13 * s + (foot - y - 13 * s) * t
            half = (7.0 + 30 * t ** 1.4) * s
            j = rng.uniform(-1, 1) * (0.6 + 5 * t) * s
            left.append(((x - half + j) * k, yy_ * k))
            right.append(((x + half + rng.uniform(-1, 1) * (0.6 + 5 * t) * s) * k, yy_ * k))
        dr.polygon(left + right[::-1], fill=255)
        dr.rectangle([(x - 7.5 * s) * k, (y + 9 * s) * k, (x + 7.5 * s) * k, (y + 14 * s) * k], fill=255)
        for dx in (-6.8, 6.8):
            dr.line([((x + dx * s) * k, (y + 10 * s) * k), ((x + dx * 0.55 * s) * k, (y - 9 * s) * k)], fill=255,
                    width=max(1, int(round(1.7 * s * k))))
        dr.polygon([((x - 8 * s) * k, (y - 8 * s) * k), ((x + 8 * s) * k, (y - 8 * s) * k), (x * k, (y - 20 * s) * k)], fill=255)
        dr.line([(x * k, (y - 20 * s) * k), (x * k, (y - 28 * s) * k)], fill=255, width=max(1, int(round(1.3 * s * k))))
        m = np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255
        out.append((m, b[2], x, y, s))
    return out


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    mx, my, mr = MOON
    d_moon = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    # ---- the sky: deep violet overhead, paling toward the horizon and the rising moon
    sky = ramp(np.clip(Y / HORIZON, 0, 1), [(0, "#080618"), (0.45, "#151034"), (0.85, "#2A2052"), (1.0, "#3C2E66")])
    sky = screen(sky, hexc("#C6C4F0") * (np.exp(-(d_moon / 120) ** 2) * MOON_GLOW + np.exp(-(d_moon / 420) ** 2) * 0.10)[..., None])
    sky = strokes(sky, (Y < HORIZON + 8).astype(np.float32), S, lambda x, y: 0.05 * math.sin(x / 70), int(4200 * S * S),
                  length=(10, 22), width=(1.6, 3.0), jitter=0.02, hue_jitter=0.004, seed=21)
    px = RL.stars(sky, S, 950, 71, (0, 0, W, (HORIZON - 6) * S),
                  avoid=lambda x, y: float(np.clip((math.hypot(x / S - mx, y / S - my) - 60) / 170, 0, 1)), bright=0.55)
    px, _disc = RL.moon_emissive(px, S, mx, my, mr, seed=8)
    # the haze it rises through: the face dimmed and cooled toward the haze's lilac, and two thin bars of mist lying across
    # its lower half (in front of it, so they read as distance, not as marks on the face)
    haze_face = px * MOON_MIST + hexc("#B4B0E6") * 0.10
    px = px * (1 - _disc[..., None]) + haze_face * _disc[..., None]
    for (yb, th, k) in ((my + 8, 2.6, 0.22), (my + 18, 3.4, 0.30)):
        bar = np.exp(-((Y - yb - 2.0 * np.sin(X / 41)) / th) ** 2) * np.exp(-((X - mx) / (mr * 6.0)) ** 2)
        px = px * (1 - k * bar[..., None]) + hexc("#4E4486") * (k * bar[..., None])
    # high streaks of cloud across the sky, lit from behind where they pass the moon
    streak = np.zeros((H, W), np.float32)
    for (y0, amp, th, k, seed) in ((200, 5, 3.2, 0.85, 3), (172, 7, 4.5, 0.6, 4), (128, 10, 6.0, 0.45, 5), (84, 12, 7.0, 0.3, 6)):
        wob = y0 + amp * np.sin(X / 95 + seed) + 8 * (fbm(1, W, 70 * S, 3, seed + 40)[0][None, :] - 0.5)
        band = np.exp(-((Y - wob) / th) ** 2) * smooth(0.38, 0.72, fbm(H, W, 46 * S, 4, seed + 60))
        streak = np.maximum(streak, band * k)
    streak = blur(streak, 0.8 * S)
    lit_streak = np.exp(-(d_moon / 170) ** 2)
    streak_col = hexc("#2C2652") * (1 - lit_streak[..., None]) + hexc("#B4B6EA") * lit_streak[..., None]
    px = px * (1 - 0.45 * streak[..., None]) + streak_col * (0.45 * streak[..., None])
    # ---- the cloud sea
    cs = _cloud_sea(S, W, H)
    Z = cs["Z"]
    sky_seen = cs["sky"] & (Y < HORIZON + 0.5)          # under the horizon the sky's gaps are the farthest haze
    cloud = (~sky_seen).astype(np.float32)
    zc = np.where(cs["sky"], 30000.0, Z)
    n_len = np.sqrt(1 + cs["gx"] ** 2 + cs["gz"] ** 2)
    ny = 1 / n_len                                     # facing the sky
    toward = np.clip(cs["gz"] / n_len, -1, 1)          # > 0: rising away from us, so facing us
    ao = smooth(-6.0, 6.0, cs["lift"])                 # crowns high, valleys low
    haze = 1 - np.exp(-zc / 2200.0)
    # the crests' silver lining: where the seen depth jumps to something much farther (or the sky) just above
    jump = np.zeros((H, W), np.float32)
    zup = np.vstack([np.full((1, W), 1e9, np.float32), zc[:-1]])
    edge = np.clip((np.log(zup / zc) - 0.05) / 0.25, 0, 1) * cloud
    for k_ in range(int(7 * S)):
        jump = np.maximum(jump, np.vstack([np.zeros((k_, W), np.float32), edge[:H - k_]]) * math.exp(-k_ / (2.2 * S)))
    jump = blur(jump, 0.7 * S)
    bear = np.exp(-((X - mx) / (150 + 260 * (1 - haze))) ** 2) * 0.6 + 0.4
    lining = jump * bear * (1 - 0.5 * haze)
    # the body: shadowed faces toward us, the sky's cool light on the tops, deeper in the valleys
    sh = np.clip(0.10 + 0.70 * ny ** 3 * (0.5 + 0.5 * ao) - 0.35 * np.clip(toward, 0, 1) + 0.15 * ao, 0, 1)
    face = ramp(sh, [(0, "#0A0920"), (0.35, "#1A1842"), (0.7, "#34336E"), (1.0, "#4A4C8A")])
    tex = fbm(H, W, 10 * S, 4, 31)
    face = face * (0.9 + 0.2 * tex)[..., None]
    # beacon pools: the lanterns light the tops round their feet
    pool = np.zeros((H, W), np.float32)
    for b in BEACONS:
        bxw, bz = b[1] * LANE, b[2]
        d2 = (cs["xw"] - bxw) ** 2 + ((Z - bz) * 0.8) ** 2
        pool += np.exp(-d2 / (2 * (22.0 + bz * 0.03) ** 2)) * (1.0 if b[4] > 0 else 0.5)
    pool = np.clip(pool, 0, 1) * (0.3 + 0.7 * ny) * cloud
    face = screen(face, hexc("#4EB4DC") * (pool * 0.26)[..., None])
    face = screen(face, hexc("#9EE6FF") * (pool * jump * 0.5)[..., None])
    # distance: the far sea takes the sky's haze and the moon's glow
    hz = screen(px * 0.92, hexc("#C6C4F0") * (np.exp(-(d_moon / 160) ** 2) * 0.10)[..., None])
    face = face * (1 - haze[..., None] * 0.9) + hz * (haze[..., None] * 0.9)
    face = screen(face, hexc("#D6D8FF") * (lining * 0.85)[..., None])
    px = px * (1 - cloud[..., None]) + face * cloud[..., None]
    # soft strokes along the billows' drift
    st = strokes(px, cloud * smooth(HORIZON + 2, HORIZON + 30, Y), S, lambda x, y: 0.04 * math.sin(x / 50 + y / 30),
                 int(12000 * S * S), length=(3, 7), width=(1.2, 2.4), jitter=0.04, hue_jitter=0.004, seed=17)
    px = px * 0.5 + st * 0.5
    # ---- the beacons: stone needles rim-lit by the moon behind, sunk into the billows in front of them
    for (m, bz, bx, by, s) in sorted(_towers(S, W, H), key=lambda t: -t[1]):
        vis = m * (zc > bz)
        px = px * (1 - vis[..., None]) + hexc("#090918") * vis[..., None]
        rim = np.clip(vis - np.roll(vis, max(1, int(S)), 1), 0, 1) * 0.5 + np.clip(vis - np.roll(vis, -max(1, int(S)), 1), 0, 1) * 0.25
        px = screen(px, hexc("#B8BCF0") * (rim * 0.5)[..., None])
    # ---- the beams: cones from each lantern, narrow at the cage, fading up into the night, motes of dust in them
    beams = np.zeros((H, W), np.float32)
    motes = fbm(H, W, 3.0 * S, 3, 52)
    for b in BEACONS:
        if b[4] <= 0:
            continue
        bx, by, s = head_at(b)
        length = b[4]
        a = math.radians(b[3] * b[1])
        ux, uy = math.sin(a), -math.cos(a)
        dx, dy = X - bx, Y - by
        t = dx * ux + dy * uy
        p = dx * uy - dy * ux
        w = 2.4 * s + 1.2 + np.clip(t, 0, None) * 0.07
        fall = smooth(4 * s, 22 * s, t) * np.exp(-np.clip(t, 0, None) / (length * 0.95)) * smooth(length * 1.5, length * 0.9, t)
        beams += np.exp(-(p / w) ** 2) * fall * (0.7 + 0.5 * min(1.0, s * 1.5)) * (0.8 + 0.4 * motes)
    beams = np.clip(beams, 0, 1.3)
    px = screen(px, hexc(BEAM_COL) * (beams * 0.38)[..., None])
    # ---- the lane's guide-strings between the beacons of each row: a dark cable hung with small aether lights
    for (name, pts) in strings():
        z0 = next(b[2] for b in BEACONS if b[0] == name.split()[1].split("-")[0])
        s0 = 89.0 / z0
        im = Image.new("L", (W * 3, H * 3), 0)
        ImageDraw.Draw(im).line([(x * S * 3, y * S * 3) for (x, y) in pts], fill=255, width=max(1, int(round(1.1 * s0 * S * 3))))
        cab = np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255
        cab = cab * (zc > z0 * 0.98)
        px = px * (1 - 0.7 * cab[..., None]) + hexc("#0C0C20") * (0.7 * cab[..., None])
        P = np.asarray(pts)
        seg = np.hypot(*np.diff(P, axis=0).T)
        cum = np.concatenate([[0], np.cumsum(seg)])
        step = max(7.0, 15.0 * s0)
        for d in np.arange(step * 0.5, cum[-1], step):
            k = int(np.searchsorted(cum, d) - 1)
            t = (d - cum[k]) / max(seg[k], 1e-6)
            lx, ly = P[k] + (P[k + 1] - P[k]) * t
            r0 = 0.9 * max(s0, 0.35) + 0.4
            x0_, x1_ = max(0, int((lx - 12) * S)), min(W, int((lx + 12) * S) + 1)
            y0_, y1_ = max(0, int((ly - 12) * S)), min(H, int((ly + 12) * S) + 1)
            d2 = (X[y0_:y1_, x0_:x1_] - lx) ** 2 + (Y[y0_:y1_, x0_:x1_] - ly) ** 2
            g = np.exp(-d2 / (2 * r0 ** 2)) * 0.8 + np.exp(-d2 / (2 * (r0 * 4) ** 2)) * 0.12
            px[y0_:y1_, x0_:x1_] = screen(px[y0_:y1_, x0_:x1_], hexc("#BFF0FF") * g[..., None])
    # ---- the lanterns: a white-cyan fire in a brass cage
    for b in BEACONS:
        bx, by, s = head_at(b)
        if zc[min(H - 1, int(by * S)), min(W - 1, int(bx * S))] < b[2]:
            continue
        d2 = (X - bx) ** 2 + (Y - by) ** 2
        f0 = 2.6 * s + 0.7
        fire = np.exp(-d2 / (2 * f0 ** 2)) * 0.95 + np.exp(-d2 / (2 * (9 * s + 2.5) ** 2)) * 0.22 \
            + np.exp(-d2 / (2 * (26 * s + 7) ** 2)) * 0.07
        px = screen(px, hexc("#CFF4FF") * fire[..., None])
        brass = np.exp(-(((np.sqrt(d2) - 7.4 * s) / (0.9 * s + 0.3)) ** 2)) * (Y < by + 10 * s) * (Y > by - 9 * s) * 0.22
        px = screen(px, hexc("#E0B470") * brass[..., None])
    # the ceiling: luma eased under 0.45 everywhere but the moon's own face (a soft knee, so no edge shows)
    Yl = px @ RL.LUM
    soft = RL.soft_ceiling(Yl, 0.45, 0.84)
    moon_face = smooth(mr + 3, mr - 1, d_moon)
    target = soft * (1 - moon_face) + Yl * moon_face
    px = px * (target / np.maximum(Yl, 1e-4))[..., None]
    px = RL.vignette(px, 0.24)
    return RL.grain(px, 0.008, seed=53)
