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

import numpy as np
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
        """Adds a raised form: its edge rounds over `round_units`; mode 'max' sets it above what is there, 'add' stacks."""
        dome = np.sqrt(np.clip(blur(m, round_units * self.S * 0.5), 0, 1)) * m
        h = dome * height
        if base is not None:
            h = h + base * m
        self.hgt = np.maximum(self.hgt, h) if mode == "max" else self.hgt + h
        return h

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
    # the shadow the relief casts on its ground, toward the lower right (a carved figure stands proud of its ground)
    sh = np.roll(np.roll(blur(relief, 1.6 * S), int(2.2 * S), 0), int(2.2 * S), 1)
    ground_col = ground_col * (1 - np.clip(occl * 1.4 + sh * 0.45, 0, 0.75))[..., None]
    # the stone: lit by the light, with a translucent body (shadows stay blue, never black)
    v = np.clip(0.58 + (lam - L[2]) * 2.2 - ao, 0.06, 1.0)   # a flat face is mid-tone; faces turned to the light glow
    stone = ramp(v, STONE)
    stone = stone * (1 - R.tone[..., None] * 0.45) + hexc("#1C2A58") * (R.tone[..., None] * 0.45)
    sss = np.clip(1 - lam, 0, 1) * 0.10
    stone = screen(stone, hexc("#5E7ACC") * sss[..., None])
    stone = screen(stone, hexc("#9CC0FF") * (np.clip(lam - 0.55, 0, 1) * sheen)[..., None])
    stone = stone + hexc("#FFFFFF") * (nh ** 40 * 0.30 + nh ** 160 * 0.35)[..., None]
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
    # the socket (a soft hollow under the brow), the eye in profile (a small almond, open or closed), its upper lid
    R.carve(R.ellipse(ex - 1 * s, ey - 1 * s, 8 * s, 6 * s), 0.9, 3.0)
    R.carve(R.poly([(ex - 4 * s, ey), (ex, ey - (1.0 if closed else 2.4) * s), (ex + 4.5 * s, ey + 0.2 * s),
                    (ex, ey + (0.6 if closed else 1.6) * s)], smooth_n=4), 0.7, 0.4, tone=0.6)
    R.ridges([[(ex - 5 * s, ey - 1.5 * s * lid), (ex - 0.5 * s, ey - 3.2 * s * lid), (ex + 4.5 * s, ey - 1.2 * s)]], 1.4 * s, 0.6)
    R.ridges([[(ex - 9 * s, ey - 8 * s), (ex + 0 * s, ey - 10.5 * s), (ex + 8 * s, ey - 7.5 * s)]], 3.0 * s, 0.7)
    # ear: a C-shaped rim (helix) round a shallow bowl, the lobe below
    ax, ay = ear[0] * s + dx, ear[1] * s + dy
    erx, ery = ear_r[0] * s, ear_r[1] * s
    R.carve(R.ellipse(ax + 1.0 * s, ay, erx * 0.7, ery * 0.7), 0.45, 1.6)
    R.ridges([[(ax + erx * 0.2, ay - ery), (ax - erx * 0.8, ay - ery * 0.6), (ax - erx, ay + ery * 0.1),
               (ax - erx * 0.6, ay + ery * 0.8), (ax + erx * 0.1, ay + ery)]], 2.0 * s, 0.55, soft=0.8)
    # nostril wing, the lips' parting, the corner of the mouth, the cheekbone's soft plane
    R.ridges([[(33 * s + dx, 14 * s + dy), (36 * s + dx, 19 * s + dy), (40 * s + dx, 20 * s + dy)]], 1.4 * s, 0.5)
    R.carve(R.strokes([[(29 * s + dx, 29.5 * s + dy), (36.5 * s + dx, 29.2 * s + dy)]], 0.9 * s), 0.6, 0.3)
    R.carve(R.ellipse(29 * s + dx, 30 * s + dy, 1.6 * s, 1.6 * s), 0.4, 0.4)
    # the cheekbone's soft plane and the jaw's line, running back to the ear (broad and low, no edge)
    R.hgt = R.hgt + blur(R.ellipse(14 * s + dx, 6 * s + dy, 10 * s, 7 * s), 6 * R.S) * 0.9
    R.ridges([[(28 * s + dx, 46 * s + dy), (14 * s + dx, 47 * s + dy), (-4 * s + dx, 32 * s + dy)]], 4.0 * s, 0.30, soft=1.5)


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
