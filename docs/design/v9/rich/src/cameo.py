"""The eleven characters as finished portraits: moonstone cameos on lapis (owner brief, 5 October 2026).

A cameo is a portrait carved in relief: here the figure is cut in moonstone, standing proud of a lapis ground, in a
brass bezel. It is a finished portrait (the face, the hair, the costume and the prop are all modelled and lit), it is
Menphina's Medallion itself (a medallion with a face on it), and it reads at every size the game needs, from the
character card (about 180 x 220 units) to the HUD's 48-unit medallion.

How it is made: each character is a set of masks (bust, head, hair, ear, features, prop) drawn from profile curves in
local units (the head is about 100 tall), stacked into a height field, then shaded by the one light from the upper
left: moonstone's body colour, its soft cool subsurface in the shadows, a blue adularescent sheen where it faces the
light, a polished highlight. Hair and cloth get carved strands and folds (ridges in the height field).
"""
import math

import numpy as np  # noqa: I001
from PIL import Image, ImageDraw

from rich_lib import L, H_BLINN, blur, fbm, hexc, ramp, screen, smooth
from layout import smooth_path

SS = 3


class Relief:
    """A height field in local units (x right, y down), w x h units at S px per unit."""

    def __init__(self, w=200.0, h=240.0, S=4.0, ox=100.0, oy=120.0):
        self.w, self.h, self.S = w, h, S
        self.W, self.H = int(w * S), int(h * S)
        self.ox, self.oy = ox, oy                      # where local (0, 0) sits in the canvas (units)
        self.hgt = np.zeros((self.H, self.W), np.float32)
        self.tone = np.zeros((self.H, self.W), np.float32)    # 0 moonstone, 1 a darker inlay (eye, groove), for colour
        self.masks = {}

    # ---- drawing helpers (local units)
    def _xy(self, x, y):
        return ((x + self.ox) * self.S * SS, (y + self.oy) * self.S * SS)

    def poly(self, pts, smooth_n=8):
        P_ = smooth_path(list(pts) + [pts[0]], smooth_n) if smooth_n else list(pts)
        im = Image.new("L", (self.W * SS, self.H * SS), 0)
        ImageDraw.Draw(im).polygon([self._xy(x, y) for (x, y) in P_], fill=255)
        return np.asarray(im.resize((self.W, self.H), Image.BOX), np.float32) / 255

    def ellipse(self, cx, cy, rx, ry):
        im = Image.new("L", (self.W * SS, self.H * SS), 0)
        (x0, y0), (x1, y1) = self._xy(cx - rx, cy - ry), self._xy(cx + rx, cy + ry)
        ImageDraw.Draw(im).ellipse([x0, y0, x1, y1], fill=255)
        return np.asarray(im.resize((self.W, self.H), Image.BOX), np.float32) / 255

    def strokes(self, paths, width, taper=True):
        """Polylines (local units) as a mask, `width` units wide (tapering to a point when taper)."""
        im = Image.new("L", (self.W * SS, self.H * SS), 0)
        dr = ImageDraw.Draw(im)
        for pts in paths:
            P_ = smooth_path(pts, 10) if len(pts) > 2 else pts
            n = len(P_)
            for i in range(n - 1):
                t = i / max(1, n - 1)
                w = width * ((1 - t) ** 0.6 if taper else 1.0)
                dr.line([self._xy(*P_[i]), self._xy(*P_[i + 1])], fill=255, width=max(1, int(round(w * self.S * SS))))
        return np.asarray(im.resize((self.W, self.H), Image.BOX), np.float32) / 255

    # ---- building the relief
    def raise_(self, m, height, round_units=4.0, mode="max", base=None):
        """Adds a raised form, rounded all the way across (round 4, realism round 3: no plateaus). Its height is the
        mean of the mask blurred at three scales up to `round_units`, so the form swells toward its middle like a
        carved volume; the outer edge stays a clean cut (the only bevel), 35% of the height. mode 'max' sets it above
        what is there, 'add' stacks."""
        r = round_units * self.S
        swell = (blur(m, r * 0.18) + blur(m, r * 0.45) + blur(m, r * 0.9)) / 3
        dome = (0.35 + 0.65 * np.clip(swell, 0, 1) ** 0.8) * m
        h = dome * height
        if base is not None:
            h = h + base * m
        self.hgt = np.maximum(self.hgt, h) if mode == "max" else self.hgt + h
        return h

    def bump(self, cx, cy, rx, ry, h, rot=0.0, within=None):
        """A soft rounded swelling (h > 0) or hollow (h < 0): a gaussian in local units, optionally kept inside a mask."""
        yy, xx = np.mgrid[0:self.H, 0:self.W].astype(np.float32)
        X, Y = (xx + 0.5) / self.S - self.ox, (yy + 0.5) / self.S - self.oy
        c, s = math.cos(rot), math.sin(rot)
        u, v = ((X - cx) * c + (Y - cy) * s) / rx, (-(X - cx) * s + (Y - cy) * c) / ry
        g = np.exp(-(u * u + v * v) * 1.4) * h
        # round 5 (realism round 4): a swelling never grows stone where there is none, so it stays inside the carving
        inside = np.clip(self.hgt / 1.2, 0, 1) if within is None else within
        self.hgt = self.hgt + g * inside

    def carve(self, m, depth, soft=0.6, tone=0.0):
        mm = blur(m, soft * self.S)
        self.hgt = self.hgt - mm * depth
        if tone:
            self.tone = np.maximum(self.tone, mm * tone)

    def ridges(self, paths, width, height, soft=0.5):
        m = self.strokes(paths, width)
        self.hgt = self.hgt + blur(m, soft * self.S) * height
        return m


# ------------------------------------------------------------------------------------------------ shading
STONE = [(0.0, "#3E4A70"), (0.35, "#7F8CB4"), (0.65, "#BCC6DE"), (0.85, "#DCE2F0"), (1.0, "#F2F2EC")]


def shade(R, ground="lapis", sheen=0.30):
    """The relief in the one light: moonstone above the ground, lapis enamel on the ground, an occlusion where the
    relief meets the ground (the undercut), a soft blue adularescent sheen and a polished highlight."""
    S = R.S
    h = blur(R.hgt, 0.5 * S)
    gy, gx = np.gradient(h)
    nx, ny = -gx * S * 1.6, -gy * S * 1.6                     # slopes per unit, exaggerated as a carver's relief is
    nz = np.ones_like(h)
    n = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / n, ny / n, nz / n
    lam = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    nh = np.clip(nx * H_BLINN[0] + ny * H_BLINN[1] + nz * H_BLINN[2], 0, 1)
    ao = np.clip((blur(h, 2.5 * S) - h) * 0.55, 0, 0.6)      # creases (eye sockets, folds, strands) hold shade
    relief = np.clip(R.hgt / 1.2, 0, 1)                       # where the stone stands above the ground
    # the ground: lapis enamel, darkened in the undercut round the relief
    yy, xx = np.mgrid[0:R.H, 0:R.W].astype(np.float32)
    t = np.clip((xx / R.W) * 0.4 + (yy / R.H) * 0.6, 0, 1)
    ground_col = ramp(t, [(0, "#22356E"), (1, "#101A3C")])
    ground_col = ground_col * (0.94 + 0.08 * fbm(R.H, R.W, 14 * S, 3, 7))[..., None]
    occl = np.clip(blur(relief, 3.0 * S) - relief, 0, 1)
    # the shadow the relief casts on its ground, toward the lower right (a carved figure stands proud of its ground),
    # lifted by cool light scattered through the stone (round 4: the shadow of moonstone is never dead)
    sh = np.roll(np.roll(blur(relief, 1.6 * S), int(2.2 * S), 0), int(2.2 * S), 1)
    dark = np.clip(occl * 1.2 + sh * 0.40, 0, 0.62)
    ground_col = ground_col * (1 - dark)[..., None] + hexc("#3A5294") * (dark * 0.18)[..., None]
    # the stone: lit by the light; a flat face is mid-tone, faces turned to the light glow, creases hold shade
    v = np.clip(0.50 + (lam - L[2]) * 2.6 - ao, 0.06, 1.0)
    stone = ramp(v, STONE)
    stone = stone * (1 - R.tone[..., None] * 0.45) + hexc("#1C2A58") * (R.tone[..., None] * 0.45)
    # moonstone, not plaster (round 4): its body scatters light, so the shadow side stays a cool blue ...
    sss = np.clip(1 - lam, 0, 1) * 0.16
    stone = screen(stone, hexc("#5E7ACC") * sss[..., None])
    # ... thin edges let light through (translucency where the stone is thin, strongest on edges facing the light)
    hmax = max(float(np.percentile(R.hgt[relief > 0.5], 95)) if (relief > 0.5).any() else 1.0, 1e-3)
    thin = np.clip(1 - R.hgt / (hmax * 0.55), 0, 1) * relief
    gy2, gx2 = np.gradient(blur(relief, 1.0 * S))
    gn2 = np.sqrt(gx2 ** 2 + gy2 ** 2) + 1e-6
    toward = np.clip((gx2 + gy2) * 0.7071 / gn2, 0, 1)        # the edge's outward normal faces the upper left
    # (round 5: only the edges that face the light pass it; an edge turned away, under a chin, stays in shadow)
    stone = screen(stone, hexc("#C9DAFA") * (thin * 0.30 * toward ** 1.5 * (a_in := np.clip(relief * 1.5, 0, 1)))[..., None])
    # ... and a milky blue adularescent sheen floats across the high points, drifting toward the upper left
    yy2, xx2 = np.mgrid[0:R.H, 0:R.W].astype(np.float32)
    w_ = relief.sum() + 1e-6
    cxm, cym = (xx2 * relief).sum() / w_, (yy2 * relief).sum() / w_
    drift = np.exp(-(((xx2 - (cxm - 0.16 * R.W)) / (0.38 * R.W)) ** 2 + ((yy2 - (cym - 0.16 * R.H)) / (0.36 * R.H)) ** 2))
    high = np.clip(R.hgt / hmax, 0, 1) ** 1.5
    stone = screen(stone, hexc("#A8C6FF") * (high * drift * sheen * 0.9)[..., None])
    stone = stone + hexc("#FFFFFF") * (nh ** 40 * 0.22 + nh ** 160 * 0.30)[..., None]
    a = np.clip(relief * 1.5, 0, 1)[..., None]
    return np.clip(ground_col * (1 - a) + stone * a, 0, 1)


# ------------------------------------------------------------------------------------------------ the profile
def head_profile(nose=1.0, chin=1.0, brow=1.0, skull=1.0, jaw=1.0, scale=1.0, dx=0.0, dy=0.0, lips=1.0, neck=1.0):
    """A human head in profile facing right, about 100 tall, crown at y -48, chin at y 46 (scaled)."""
    pts = [(-22 * neck, 96), (-26 * neck, 70), (-30 * skull, 48), (-44 * skull, 22), (-48 * skull, -5),
           (-42 * skull, -32), (-22 * skull, -50), (0, -55 * skull), (20 * brow, -44), (30 * brow, -26), (33 * brow, -11),
           (31, -4), (34 + 2 * nose, 4), (36 + 7 * nose, 12 * nose + 2), (37, 18), (36, 22), (37 + 1.5 * lips, 25),
           (35, 28), (36.5 + 1.0 * lips, 30.5), (33, 34), (35 * chin, 41), (30 * chin, 48), (14 * jaw, 51),
           (10, 58), (12 * neck, 70), (15 * neck, 96)]
    return [(x * scale + dx, y * scale + dy) for (x, y) in pts]


def face_features(R, scale=1.0, dx=0.0, dy=0.0, eye=(26, -6), closed=False, ear=(-6, 6), ear_r=(9, 13), lid=1.0):
    """The eye (an almond groove with a lid ridge), the brow ridge, the ear, the nostril, the lips' line, the cheek."""
    s = scale
    ex, ey = eye[0] * s + dx, eye[1] * s + dy
    P = lambda x, y: (x * s + dx, y * s + dy)
    # round 4 (realism round 3): the face is modelled as soft volumes, not lines. Forehead, temple, brow, the socket
    # under it, the eyeball and its lid, the cheekbone, the nose's bridge and wing, both lips, the chin, the jaw
    R.bump(*P(12, -32), 16 * s, 12 * s, 1.6)                  # the forehead's dome
    R.bump(*P(-2, -14), 9 * s, 9 * s, -0.7)                   # the temple's hollow
    R.bump(ex - 2 * s, ey - 9 * s, 9 * s, 3.2 * s, 1.1, rot=-0.15)   # the brow
    R.bump(ex - 1 * s, ey, 7 * s, 4.5 * s, -1.6)              # the socket
    R.bump(ex, ey, 3.6 * s, 2.6 * s, 0.9)                     # the eyeball under its lid
    R.ridges([[(ex - 4.5 * s, ey - 1.0 * s * lid), (ex - 0.5 * s, ey - 2.6 * s * lid), (ex + 4.0 * s, ey - 0.8 * s)]],
             1.3 * s, 0.55, soft=0.7)                         # the upper lid's edge
    R.carve(R.poly([(ex - 3.5 * s, ey + 0.2 * s), (ex, ey - (0.6 if closed else 1.6) * s), (ex + 4.0 * s, ey + 0.4 * s),
                    (ex, ey + (0.5 if closed else 1.3) * s)], smooth_n=4), 0.5, 0.5, tone=0.35)
    R.bump(*P(16, 6), 9 * s, 7 * s, 1.5, rot=0.3)             # the cheekbone
    R.bump(*P(20, 20), 8 * s, 8 * s, -0.6)                    # under the cheekbone
    R.bump(*P(35, 7), 3.0 * s, 8 * s, 1.0, rot=-0.45)         # the nose's bridge
    R.bump(*P(34, 16), 3.6 * s, 3.0 * s, 0.9)                 # the nostril's wing
    R.bump(*P(31, 18), 2.2 * s, 1.6 * s, -0.6)                # its crease
    R.bump(*P(34, 25), 4.0 * s, 2.4 * s, 0.8)                 # the upper lip
    R.bump(*P(33.5, 31), 3.6 * s, 2.4 * s, 0.9)               # the lower lip
    R.bump(*P(30, 34.5), 4.0 * s, 1.6 * s, -0.5)              # the hollow under it
    R.bump(*P(29, 42), 6.0 * s, 5.0 * s, 1.2)                 # the chin
    R.bump(*P(8, 38), 14 * s, 7 * s, 0.6, rot=-0.5)           # the jaw's plane
    # round 5 (realism round 4): the side of the head turns away from the light, the neck is a cylinder with the
    # jaw's shadow on it and the long muscle running down to the collar
    R.bump(*P(-16, -6), 24 * s, 28 * s, 1.8)                  # the cranium's side, a broad swell
    R.bump(*P(-34, 10), 10 * s, 26 * s, -0.8)                 # turning away at the back of the head
    R.bump(*P(-4, 72), 12 * s, 20 * s, 2.2)                   # the neck's round, lit toward the front-left
    R.bump(*P(14, 74), 6 * s, 20 * s, -1.0)                   # its far side turning away
    R.bump(*P(14, 56), 14 * s, 4 * s, -1.4)                   # the jaw's shadow falling on it
    R.bump(*P(4, 76), 3.0 * s, 16 * s, 0.9, rot=-0.35)        # the neck muscle
    # ear: a C-shaped rim (helix) round a shallow bowl, the lobe below
    ax, ay = ear[0] * s + dx, ear[1] * s + dy
    erx, ery = ear_r[0] * s, ear_r[1] * s
    R.carve(R.ellipse(ax + 1.0 * s, ay, erx * 0.7, ery * 0.7), 0.45, 1.6)
    R.ridges([[(ax + erx * 0.2, ay - ery), (ax - erx * 0.8, ay - ery * 0.6), (ax - erx, ay + ery * 0.1),
               (ax - erx * 0.6, ay + ery * 0.8), (ax + erx * 0.1, ay + ery)]], 2.0 * s, 0.55, soft=0.8)
    # the lips' parting and the mouth's corner, finely cut
    R.carve(R.strokes([[(29 * s + dx, 28.4 * s + dy), (36.5 * s + dx, 28.2 * s + dy)]], 0.8 * s), 0.45, 0.3)
    R.bump(*P(28.5, 28.6), 1.6 * s, 1.6 * s, -0.5)


# ------------------------------------------------------------------------------------------------ framing
def oval_mask(W, H, S, cx, cy, rx, ry):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.sqrt(((xx + 0.5) / S - cx) ** 2 / rx ** 2 + ((yy + 0.5) / S - cy) ** 2 / ry ** 2)
    return np.clip((1 - d) * min(rx, ry) * S + 0.5, 0, 1), d


def render(build_fn, S=4.0, size=(200.0, 240.0)):
    """Builds and shades a cameo; returns RGBA float (the oval) at size*S px."""
    w, h = size
    R = Relief(w, h, S, ox=w / 2, oy=h / 2 + 6)
    build_fn(R)
    rgb = shade(R)
    m, _ = oval_mask(R.W, R.H, S, w / 2, h / 2, w / 2 - 2, h / 2 - 2)
    return np.dstack([rgb, m])
