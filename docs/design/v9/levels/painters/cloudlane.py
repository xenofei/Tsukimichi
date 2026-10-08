"""Our own painting for 3-4 "The Lane by Starlight" (re-subjected: sources-plan.md, 3-4): the airship lane by night.

No airship art exists at a usable size (sources-plan.md), so the level shows what an airship passes rather than the
hull: the cloud sea sliding by and the lane's beacon lamps marching away to the horizon, under the star river. The moon
has set beyond the upper right corner; its light still rakes the cloud sea from the right, so the banks run in billows
whose crests are lined with silver on their moonward (right, upper) sides, far ones low and hazed, near ones heaped,
with smaller billows on their faces and the troughs between them in deep shadow (the dress lays the level's aqua on the
lane's own wedge of cloud). Over the lane the sky is a deep indigo-blue, and the star river, a milky band of crowded small stars with a
dark rift of dust winding along it, climbs from behind the left cloud tower to the upper right: the Night Skyway the
airships steer by. Two slate-blue cloud towers stand on the far horizon. The lane is two rows of beacons, slim posts
with a pale gold lamp at the head, rising out of the cloud tops and closing toward a vanishing point on the horizon;
each lamp pools a little light on the billows round its foot, and a faint current with drifting motes runs between the
rows. An overhang of dark cloud closes the upper left corner.

MR 3 round 1 (game designer M2, m5): the full moon and the violet sky echoed 3-2, and the banks were flat and read as
hills; the moon set, the star river, the indigo-blue sky and the silver-lined crests are that round's answer.

FEATURES (board units): each bank's crest (the top edge of its billows), the beacons (lamp, perspective scale, side),
the lane's two edges, the star river's spine, and the open sky (for the stars' motion).
"""
import math

import numpy as np
from PIL import Image

import rich_lib as RL
from brush import strokes
from rich_lib import blur, fbm, hexc, normals_from_height, ramp, screen, smooth

HORIZON = 268.0
# MR 3 round 1 (game designer M2: the board echoed 3-2's purple sky and full moon, and its banks read as green hills):
# the moon is set beyond the frame's upper right, so its light rakes the banks from the right and silvers their
# crests, and the sky over the lane holds the star river instead, the Night Skyway the airships steer by.
MOON = (868, -60, 24)
# the star river's spine across the sky, from behind the left tower up to the upper right (board units)
GALAXY = [(232, 262), (300, 236), (372, 206), (446, 174), (522, 142), (600, 112), (676, 84), (740, 62)]
VP = (452.0, 272.0)                               # the lane's vanishing point on the far cloud sea
LANE_NEAR = ((150.0, 576.0), (660.0, 576.0))      # the lane's two edges where they leave the board
SCALES = (0.78, 0.50, 0.32, 0.19, 0.09)           # the beacons' perspective scale, near to far
LIGHT = np.array([0.68, -0.60, 0.42], np.float32)  # from the set moon: right, up (screen y down), a little toward us
LIGHT = LIGHT / np.linalg.norm(LIGHT)
# the clouds' colour by light: the towers and the overhang in the sky's lavender, the cloud sea a near-neutral silver
# (the dress lays the second jewel's sea-green on its moonlit crowns; a lavender paint there would cancel it where the
# quiet keeps the paint)
SILVER = [(0, "#0A0B1C"), (0.18, "#151A2E"), (0.40, "#2A3346"), (0.62, "#525C70"), (0.82, "#949CB0"), (1.0, "#D2D6E2")]
LAVENDER = [(0, "#070B1E"), (0.18, "#101C3C"), (0.40, "#22365E"), (0.62, "#4A6088"), (0.82, "#8298BE"), (1.0, "#C2D0EA")]

# the banks, far to near (closer together toward the horizon): base y (the billows' feet), puff radius range, haze
BANKS = [
    (282, (6, 12), 0.70),
    (304, (9, 17), 0.56),
    (336, (13, 24), 0.40),
    (382, (17, 32), 0.24),
    (446, (22, 42), 0.10),
    (540, (34, 60), 0.0),
]


def _puffs(k):
    """The billows of bank k as spheres (cx, cy, R): a row of heaps along the bank's feet, some of them large, with
    smaller puffs heaped on them in two tiers (deterministic: the painting and the layout read the same shapes)."""
    base, (r0, r1), _ = BANKS[k]
    rng = np.random.default_rng(300 + k)
    out = []
    x = -40.0 - rng.uniform(0, r1)
    while x < 840 + r1:
        R = rng.uniform(r0, r1) * (1.45 if rng.random() < 0.22 else 1.0)
        out.append((x, base - R * 0.10 + rng.normal(0, R * 0.10), R))
        x += R * rng.uniform(0.80, 1.30)
    tiers = []
    for (cx, cy, R) in out:
        if rng.random() < 0.7:
            r = R * rng.uniform(0.50, 0.75)
            tiers.append((cx + rng.uniform(-0.45, 0.45) * R, cy - R * rng.uniform(0.35, 0.60), r))
    return out + tiers


def _towers():
    """Two cloud towers on the far horizon: stacks of puffs narrowing upward, the left one tall."""
    rng = np.random.default_rng(377)
    out = []
    for (x0, top, w) in ((200, 168, 70), (690, 214, 40)):
        y = HORIZON + 4
        while y > top:
            frac = (HORIZON - y) / (HORIZON - top)
            R = w * (0.70 - 0.36 * frac) * rng.uniform(0.9, 1.1)
            for j in range(4):
                out.append((x0 + rng.uniform(-1.0, 1.0) * w * (1 - 0.6 * frac) + 30 * frac, y - rng.uniform(0, R * 0.3),
                            R * rng.uniform(0.7, 1.0)))
            y -= R * 0.45
    return out


PUFFS = [_puffs(k) for k in range(len(BANKS))]
TOWERS = _towers()
OVERHANG = [(x, y, R) for (x, y, R) in ((10, 10, 70), (80, 0, 64), (150, -20, 66), (220, -36, 60), (46, 60, 40),
                                         (112, 48, 36), (176, 26, 32), (250, 4, 30), (300, -14, 28), (8, 100, 34))]


def _top(puffs, x):
    """The top edge of a mass of puffs at x (board units), or None where no puff covers x."""
    best = None
    for (cx, cy, R) in puffs:
        dx = x - cx
        if abs(dx) < R:
            y = cy - math.sqrt(R * R - dx * dx)
            best = y if best is None else min(best, y)
    return best


def _tower_top(x0, x1, step=6):
    return [[float(x), round(_top(TOWERS, x), 1)] for x in range(x0, x1 + 1, step) if _top(TOWERS, x) is not None]


def crest(k, x):
    """The top edge of bank k's billows at x (board units), or None where no puff covers x."""
    best = None
    for (cx, cy, R) in PUFFS[k]:
        dx = x - cx
        if abs(dx) < R:
            y = cy - math.sqrt(R * R - dx * dx)
            best = y if best is None else min(best, y)
    return best


def _crest_line(k, x0=60, x1=740, step=6, smooth_n=5):
    pts = [(x, crest(k, x)) for x in range(x0, x1 + 1, step)]
    ys = np.array([p[1] for p in pts], np.float64)
    # a gentle running mean, so the line follows the billows' run and not every dimple between puffs
    ker = np.ones(smooth_n) / smooth_n
    ys = np.convolve(np.pad(ys, smooth_n // 2, mode="edge"), ker, mode="valid")
    return [[float(x), round(float(y), 1)] for (x, _), y in zip(pts, ys)]


def _edge_point(side, s):
    (nx, ny) = LANE_NEAR[side]
    return VP[0] + (nx - VP[0]) * s, VP[1] + (ny - VP[1]) * s


def _beacons():
    out = []
    for side in (0, 1):
        for s in SCALES:
            bx, by = _edge_point(side, s)
            out.append({"side": side, "s": s, "base": [round(bx, 1), round(by, 1)],
                        "lamp": [round(bx, 1), round(by - 64 * s, 1)]})
    return out


BEACONS = _beacons()

FEATURES = {
    "crest 1": {"points": _crest_line(1)},
    "crest 2": {"points": _crest_line(2)},
    "crest 3": {"points": _crest_line(3)},
    "crest 4": {"points": _crest_line(4)},
    "crest 5": {"points": _crest_line(5)},
    "beacons": {"lamps": [b["lamp"] + [b["s"], b["side"]] for b in BEACONS]},
    "lane left": {"points": [[b["lamp"][0], b["lamp"][1]] for b in BEACONS if b["side"] == 0]},
    "lane right": {"points": [[b["lamp"][0], b["lamp"][1]] for b in BEACONS if b["side"] == 1]},
    "tower left": {"points": _tower_top(120, 300)},
    "tower right": {"points": _tower_top(630, 724)},
    "star river": {"points": [list(p) for p in GALAXY]},
    "lane": [[VP[0], VP[1] - 4], [LANE_NEAR[0][0] - 40, 600.0], [LANE_NEAR[1][0] + 40, 600.0]],
    "star sky": [[76, 132], [200, 112], [300, 110], [420, 60], [700, 44], [724, 44], [724, 196],
                 [650, 206], [626, 258], [306, 258], [290, 190], [240, 158], [150, 158], [110, 196], [76, 232]],
}


def _stretched_noise(H, W, cell, octaves, seed, stretch):
    """fbm drawn narrow and stretched along x: long streaks (cirrus)."""
    w = max(8, int(W / stretch))
    n = fbm(H, w, cell, octaves, seed)
    return np.asarray(Image.fromarray(n, "F").resize((W, H), Image.BILINEAR), np.float32)


def _cloud(px, S, X, Y, puffs, seed, depth, haze, sky_far, body_base=None, soft=1.0, dark=0.0, flow=None, stops=None,
           mist_col="#231E52"):
    """A mass of billows over px: a union of spheres roughened by noise, lit from the moon, each column darkening
    with depth below its own top; `body_base` continues the mass downward as a face that fades into the trough."""
    H, W, _ = px.shape
    hbuf = np.zeros((H, W), np.float32)
    rmin = min(p[2] for p in puffs)
    wob = fbm(H, W, max(3.0, rmin * 0.7) * S, 3, seed)
    for (cx, cy, R) in puffs:
        x0, x1 = int(max(0, (cx - R * 1.1 - 3) * S)), int(min(W, (cx + R * 1.1 + 3) * S + 1))
        y0, y1 = int(max(0, (cy - R * 1.1 - 3) * S)), int(min(H, (cy + R * 1.1 + 3) * S + 1))
        if x1 <= x0 or y1 <= y0:
            continue
        sx, sy = X[y0:y1, x0:x1] - cx, Y[y0:y1, x0:x1] - cy
        Re = R * (0.88 + 0.24 * wob[y0:y1, x0:x1])
        h = np.sqrt(np.clip(Re * Re - sx * sx - sy * sy, 0, None))
        hbuf[y0:y1, x0:x1] = np.maximum(hbuf[y0:y1, x0:x1], h)
    cov = np.clip(hbuf / (3.0 + 0.12 * rmin) / soft, 0, 1)
    if body_base is not None:
        body = smooth(body_base - 8, body_base + 6, Y)
        cov = np.maximum(cov, body)
        hbuf = np.maximum(hbuf, body * 2.0)
    cov = blur(cov, 0.7 * S * soft)
    fuzz = fbm(H, W, max(2.0, rmin * 0.3) * S, 2, seed + 3)
    cov = np.clip(cov * (0.75 + 0.5 * fuzz) * smooth(0.0, 0.35, cov) + cov * (1 - smooth(0.0, 0.35, cov)) * 0.4, 0, 1)         * smooth(0.02, 0.2, cov) + np.clip(cov - 0.999, 0, 1)
    cov = np.where(cov > 0.98, 1.0, cov)
    # the surface: the spheres' heights roughened with small billows, so each puff reads as cauliflower, not a ball
    detail = ((fbm(H, W, max(5.0, rmin * 1.2) * S, 3, seed + 7) - 0.5) * (rmin * 0.30)
              + (fbm(H, W, max(2.5, rmin * 0.45) * S, 2, seed + 9) - 0.5) * (rmin * 0.12))
    hs = blur(hbuf + detail * np.clip(hbuf / 4, 0, 1), (2.6 + 0.16 * rmin) * S)
    nx, ny, nz = normals_from_height(hs, S)
    diffuse = np.clip((nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2] + 0.25) / 1.25, 0, 1)
    # each column's own top, and how far below it a pixel lies (crowns lit, the lower billows in their own shadow)
    filled = cov > 0.5
    top_row = np.where(filled.any(0), filled.argmax(0), H).astype(np.float32) / S
    k = max(3, int(3 * rmin * S)) | 1                 # smoothed along x, so the shadow below has no vertical seams
    for _ in range(2):
        top_row = np.convolve(np.pad(top_row, k // 2, mode="edge"), np.ones(k) / k, mode="valid")
    below = np.clip((Y - top_row[None, :]) / depth, 0, 1)
    rim = smooth(0.0, 0.5, cov) * smooth(0.9, 0.2, cov * 0 + np.clip(hbuf / (rmin * 0.8), 0, 1)) * np.clip(nx * 0.7 - ny * 0.6, 0, 1)
    moonward = 0.72 + 0.40 * np.exp(-((X - MOON[0]) / 330) ** 2)
    deep = smooth(0, 1, below)
    # the crest's silver lining: the first few units under each column's top, caught by the raking moon
    lining = np.exp(-np.clip(Y - top_row[None, :], 0, None) / 2.4) * smooth(0.3, 0.9, cov) * np.clip(0.35 + nx * 0.9, 0, 1)
    # every billow's own upper outline, where it stands against the one behind it: a thinner line of the same light
    sh = max(1, int(round(2.0 * S)))
    outline = np.clip(cov - np.vstack([np.zeros((sh, cov.shape[1]), np.float32), cov[:-sh]]), 0, 1)
    outline = np.maximum(outline, np.clip(smooth(0.5, 0.0, -ny) * 0 + np.clip(-ny - 0.55, 0, 1) * 1.6, 0, 1) * smooth(0.5, 0.9, cov))
    lit = (0.03 + (0.80 * diffuse ** 2.2 + 0.10) * (1 - 0.90 * deep) * moonward + 0.40 * rim * moonward
           + 0.55 * lining * moonward * (1 - haze) + 0.30 * outline * moonward * (1 - haze))
    lit = lit * (0.94 + 0.12 * fbm(H, W, 9 * S, 2, seed + 11)) * (1 - dark)
    col = ramp(np.clip(lit, 0, 1), stops or LAVENDER)
    mist = smooth(0.45, 1.0, below)[..., None] * 0.30          # the troughs fill with a faint mist, not black
    col = col * (1 - mist) + hexc(mist_col) * mist
    col = col * (1 - haze) + sky_far * haze
    px = px * (1 - cov[..., None]) + col * cov[..., None]
    if flow is not None:
        f = flow
        f[0][:] = np.where(cov > 0.5, nx, f[0])
        f[1][:] = np.where(cov > 0.5, ny, f[1])
    return px, cov


def _beacon(px, S, X, Y, b):
    """One of the lane's beacons: a slim post rising out of the cloud tops (its foot lost in the billows), a dark cage
    and cap at its head round a warm lamp, a halo, and the lamp's light pooled on the billows round its foot."""
    s = b["s"]
    (bx, by), (lx, ly) = b["base"], b["lamp"]
    w = 0.8 + 2.0 * s
    t = np.clip((Y - ly) / max(by - ly, 1), 0, 1)
    post = np.clip((w - np.abs(X - bx)) * S + 0.5, 0, 1) * (Y > ly) * smooth(1.0, 0.55, t)
    px = px * (1 - post[..., None]) + hexc("#08081A") * post[..., None]
    cage = np.clip(((3 + 7 * s) - (np.abs(X - lx) * 1.5 + np.abs(Y - ly) * 0.9)) * S + 0.5, 0, 1)
    px = px * (1 - cage[..., None]) + hexc("#100C20") * cage[..., None]
    cap = np.clip((1.0 + 1.6 * s - np.abs(Y - (ly - 5 - 8 * s))) * S + 0.5, 0, 1) * (np.abs(X - lx) < 3 + 8 * s)
    px = px * (1 - cap[..., None]) + hexc("#08081A") * cap[..., None]
    rim = np.clip((0.5 + 0.6 * s - np.abs(X - (bx + w * 0.7))) * S + 0.5, 0, 1) * (Y > ly + 4) * smooth(0.9, 0.4, t)
    px = screen(px, hexc("#8E96D0") * (rim * 0.35)[..., None])
    d2 = (X - lx) ** 2 + (Y - ly) ** 2
    core = 1.3 + 2.6 * s
    px = screen(px, hexc("#FFEAC4") * (np.exp(-d2 / (core * core)) * 0.60)[..., None])     # (under its moon: F6)
    px = screen(px, hexc("#F0D8B4") * (np.exp(-d2 / (6 + 14 * s) ** 2) * (0.08 + 0.06 * s))[..., None])
    pool = np.exp(-(((X - bx) / (16 + 40 * s)) ** 2 + ((Y - by + 6 * s) / (5 + 12 * s)) ** 2))
    px = screen(px, hexc("#F4D8B0") * (pool * (0.06 + 0.08 * s))[..., None])
    return px


def _lane(S, X, Y):
    """The lane between its two edges (0..1, soft at the edges, fading toward the viewer and into the distance) and its
    streaks: lines running toward the vanishing point, broken by slow noise."""
    s = (Y - VP[1]) / (LANE_NEAR[0][1] - VP[1])
    xl = VP[0] + (LANE_NEAR[0][0] - VP[0]) * s
    xr = VP[0] + (LANE_NEAR[1][0] - VP[0]) * s
    u = (X - xl) / np.maximum(xr - xl, 1e-3)
    edge = smooth(-0.02, 0.10, u) * smooth(1.02, 0.90, u)
    lane = edge * smooth(0.0, 0.10, s) * smooth(0.95, 0.70, s)
    H, W = X.shape
    n = fbm(H, W, 30 * S, 3, 83)
    streak = 0.5 + 0.5 * np.sin(u * 2 * np.pi * 7 + (n - 0.5) * 5)
    streak = streak ** 3 * smooth(0.3, 0.7, fbm(H, W, 60 * S, 2, 89))
    return lane.astype(np.float32), streak.astype(np.float32)


def _motes(px, S, lane, n=260, seed=91):
    """Small pale motes drifting in the current."""
    rng = np.random.default_rng(seed)
    H, W, _ = px.shape
    for _ in range(n):
        x, y = rng.uniform(80, 720), rng.uniform(HORIZON + 6, 560)
        a = float(lane[min(H - 1, int(y * S)), min(W - 1, int(x * S))])
        if a < 0.2:
            continue
        s = (y - VP[1]) / (LANE_NEAR[0][1] - VP[1])
        r = (0.4 + 0.9 * s * rng.random()) * S
        X0, X1, Y0, Y1 = int(max(0, x * S - 4 * r)), int(min(W, x * S + 4 * r + 1)), int(max(0, y * S - 4 * r)), int(min(H, y * S + 4 * r + 1))
        yy, xx = np.mgrid[Y0:Y1, X0:X1].astype(np.float32)
        g = np.exp(-((xx + 0.5 - x * S) ** 2 + (yy + 0.5 - y * S) ** 2) / (2 * r * r)) * a * (0.45 + 0.45 * rng.random())
        px[Y0:Y1, X0:X1] = screen(px[Y0:Y1, X0:X1], hexc("#D8ECF4") * g[..., None])
    return px


def _star_river(S, X, Y):
    """The star river's band (0..1: full on its spine, fading over about 40 units either side, soft along its length)
    and the dark rift of dust that winds along it (0..1)."""
    G = np.asarray(GALAXY, np.float32)
    d = np.full(X.shape, 1e9, np.float32)
    t_at = np.zeros(X.shape, np.float32)
    total = 0.0
    for (a, b) in zip(G, G[1:]):
        ab = b - a
        L2 = float(ab @ ab)
        t = np.clip(((X - a[0]) * ab[0] + (Y - a[1]) * ab[1]) / L2, 0, 1)
        dd = np.hypot(X - (a[0] + t * ab[0]), Y - (a[1] + t * ab[1]))
        closer = dd < d
        d = np.where(closer, dd, d)
        t_at = np.where(closer, total + t * np.sqrt(L2), t_at)
        total += float(np.sqrt(L2))
    H, W = X.shape
    wob = (fbm(H, W, 40 * S, 3, 97) - 0.5) * 22
    band = np.exp(-((d + wob * 0.3) / 34) ** 2) * (0.75 + 0.5 * fbm(H, W, 26 * S, 3, 99))
    band = band * smooth(HORIZON + 2, HORIZON - 26, Y) * smooth(0, 140, t_at)
    rift = np.exp(-((d - 5 + wob * 0.4) / 9) ** 2) * smooth(0.30, 0.75, fbm(H, W, 70 * S, 2, 101))
    return band.astype(np.float32), rift.astype(np.float32)


def _river_stars(px, S, band, rift, ymax, n=4200, seed=103):
    """The star river's own stars: small, crowded where the band is full, thinned in the rift."""
    rng = np.random.default_rng(seed)
    H, W, _ = px.shape
    for _ in range(n):
        x, y = rng.uniform(0, 800), rng.uniform(0, ymax)
        k = float(band[min(H - 1, int(y * S)), min(W - 1, int(x * S))]) * (1 - 0.8 * float(rift[min(H - 1, int(y * S)), min(W - 1, int(x * S))]))
        if rng.random() > k:
            continue
        r = (0.35 + 0.45 * rng.random() ** 3) * S
        a = 0.10 + 0.45 * rng.random() ** 2.5
        X0, X1, Y0, Y1 = int(max(0, x * S - 3 * r)), int(min(W, x * S + 3 * r + 1)), int(max(0, y * S - 3 * r)), int(min(H, y * S + 3 * r + 1))
        yy, xx = np.mgrid[Y0:Y1, X0:X1].astype(np.float32)
        g = np.exp(-((xx + 0.5 - x * S) ** 2 + (yy + 0.5 - y * S) ** 2) / (2 * r * r)) * a
        px[Y0:Y1, X0:X1] = screen(px[Y0:Y1, X0:X1], hexc("#E4ECFF") * g[..., None])
    return px


def paint(S=1):
    W, H = int(800 * S), int(600 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    mx, my, mr = MOON
    d_moon = np.sqrt((X - mx) ** 2 + (Y - my) ** 2)
    # ---- the sky: deep indigo-blue overhead, a paler band of air along the far cloud sea, and the set moon's glow in
    # the upper right corner
    sky = ramp(np.clip(Y / HORIZON, 0, 1), [(0, "#03061A"), (0.45, "#0A1436"), (0.8, "#16285A"), (1.0, "#22396E")])
    sky = screen(sky, hexc("#C8D4F4") * (np.exp(-(d_moon / 260) ** 2) * 0.16 + np.exp(-(d_moon / 520) ** 2) * 0.07)[..., None])
    river = _star_river(S, X, Y)
    # thin cirrus in long streaks, lit where they cross the moon's glow
    cir = _stretched_noise(H, W, 22 * S, 4, 51, 6.0)
    cir = smooth(0.54, 0.72, cir) * smooth(50, 100, Y) * smooth(HORIZON - 30, 170, Y)
    sky = sky * (1 - 0.25 * cir[..., None]) + hexc("#6A70AE") * (cir * (0.10 + 0.50 * np.exp(-(d_moon / 240) ** 2)))[..., None]
    sky = strokes(sky, (Y < HORIZON + 10).astype(np.float32), S, lambda x, y: 0.03 * math.sin(x / 120), int(4200 * S * S),
                  length=(14, 30), width=(1.4, 2.8), jitter=0.025, hue_jitter=0.004, seed=53)
    # the star river: a soft milky band, a dark rift of dust along it, and its stars crowded thick
    band, rift = river
    sky = screen(sky, hexc("#B8C8EE") * (band * 0.26 * (1 - 0.55 * rift))[..., None])
    sky = sky * (1 - 0.16 * (rift * band)[..., None])
    px = RL.stars(sky, S, 650, 57, (0, 0, W, (HORIZON - 4) * S), bright=0.5)
    px = _river_stars(px, S, band, rift, (HORIZON - 4))
    # ---- the far cloud towers, then the cloud sea, far bank to near
    sky_far = hexc("#22396E")
    sea_far = hexc("#30344E")
    px, _ = _cloud(px, S, X, Y, TOWERS, 371, 110, 0.58, sky_far, soft=1.6)
    flow = (np.zeros((H, W), np.float32), np.zeros((H, W), np.float32))
    covs = []
    for k, (base, (r0, r1), haze) in enumerate(BANKS):
        px, cov = _cloud(px, S, X, Y, PUFFS[k], 410 + 13 * k, r1 * 2.2, haze, sea_far, body_base=base + r1 * 0.4,
                         soft=1.0 + 0.6 * haze, flow=flow, stops=SILVER, mist_col="#161A30")
        covs.append(cov)
    # the moon's light laid on the cloud tops beneath it: a broken road of sheen on the billows' crowns
    road = np.exp(-((X - mx + 10) / (26 + 0.30 * np.clip(Y - HORIZON, 0, None))) ** 2) * smooth(HORIZON - 4, HORIZON + 30, Y)
    crowns = smooth(-0.15, -0.6, flow[1])
    px = screen(px, hexc("#C8D4F6") * (road * crowns * 0.22)[..., None])
    # ---- the lane itself: a faint current skimming the cloud tops between the beacons, in long streaks that close
    # toward the vanishing point, with motes drifting in it
    lane, streak = _lane(S, X, Y)
    crown_lit = np.clip((px @ RL.LUM) / 0.3, 0, 1)
    px = screen(px, hexc("#A8D8E0") * (lane * (0.03 + 0.12 * streak) * (0.30 + 0.70 * crown_lit))[..., None])
    px = _motes(px, S, lane)
    # ---- the lane's beacons, near ones last
    for b in sorted(BEACONS, key=lambda b: b["s"]):
        px = _beacon(px, S, X, Y, b)
    # ---- the overhang: dark cloud closing the upper left corner, its lower billows caught by the moon
    px, _ = _cloud(px, S, X, Y, OVERHANG, 389, 70, 0.0, sky_far, dark=0.45)
    # ---- the ceiling: keep the board's ground dark (the moon is exempt), then the vignette and the grain
    Yl = px @ RL.LUM
    lim = 0.42
    px = px * np.where(Yl > lim, lim / np.maximum(Yl, 1e-4), 1.0)[..., None]
    px = RL.vignette(px, 0.24)
    return RL.grain(px, 0.008, seed=67)
