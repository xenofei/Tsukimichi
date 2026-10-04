"""Small painting kit for the 1.22 release art: float RGB canvases in numpy, soft masks, value-noise fBm, a box-pass
Gaussian blur and light blending. Only numpy and Pillow, so it runs anywhere the mock pipeline runs (py -3)."""
import math

import numpy as np
from PIL import Image, ImageDraw


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ---------- blur ----------
def _box(a, r, axis):
    if r < 1:
        return a
    n = a.shape[axis]
    pad = [(0, 0)] * a.ndim
    pad[axis] = (r + 1, r)
    p = np.pad(a, pad, mode="edge")
    c = np.cumsum(p, axis=axis, dtype=np.float64)
    hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
    lo = np.take(c, np.arange(0, n), axis=axis)
    return ((hi - lo) / (2 * r + 1)).astype(np.float32)


def blur(a, sigma):
    """Gaussian blur by three box passes per axis (works on HxW and HxWxC)."""
    if sigma <= 0.3:
        return a
    w = math.sqrt(12 * sigma * sigma / 3 + 1)
    r = max(1, int(w // 2))
    out = a.astype(np.float32)
    for _ in range(3):
        out = _box(out, r, 0)
        out = _box(out, r, 1)
    return out


# ---------- noise ----------
def value_noise(h, w, cell, seed):
    rng = np.random.default_rng(seed)
    gh, gw = int(h / cell) + 3, int(w / cell) + 3
    g = rng.random((gh, gw)).astype(np.float32)
    ys = np.arange(h, dtype=np.float32) / cell
    xs = np.arange(w, dtype=np.float32) / cell
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    ty = ys - y0
    tx = xs - x0
    ty = ty * ty * (3 - 2 * ty)
    tx = tx * tx * (3 - 2 * tx)
    a = g[y0][:, x0]
    b = g[y0][:, x0 + 1]
    c = g[y0 + 1][:, x0]
    d = g[y0 + 1][:, x0 + 1]
    top = a + (b - a) * tx[None, :]
    bot = c + (d - c) * tx[None, :]
    return top + (bot - top) * ty[:, None]


def fbm(h, w, cell, octaves, seed, gain=0.5):
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        out += value_noise(h, w, max(1.0, cell / (2 ** o)), seed + 101 * o) * amp
        tot += amp
        amp *= gain
    return out / tot


def fbm1d(n, cell, octaves, seed, gain=0.5):
    out = np.zeros(n, np.float32)
    amp, tot = 1.0, 0.0
    rng = np.random.default_rng(seed)
    for o in range(octaves):
        c = max(1.0, cell / (2 ** o))
        g = rng.random(int(n / c) + 3).astype(np.float32)
        xs = np.arange(n) / c
        x0 = np.floor(xs).astype(int)
        t = xs - x0
        t = t * t * (3 - 2 * t)
        out += (g[x0] + (g[x0 + 1] - g[x0]) * t) * amp
        tot += amp
        amp *= gain
    return out / tot


# ---------- canvas ----------
class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = np.zeros((h, w, 3), np.float32)
        self.yy, self.xx = np.mgrid[0:h, 0:w].astype(np.float32)

    def vgrad(self, stops, y0=0, y1=None):
        """Vertical gradient from (t, hex) stops, t in 0..1 between y0 and y1."""
        y1 = self.h if y1 is None else y1
        t = np.clip((self.yy[:, :1] - y0) / max(1, (y1 - y0)), 0, 1)[:, 0]
        ts = [s[0] for s in stops]
        cs = np.stack([hexc(s[1]) for s in stops])
        col = np.stack([np.interp(t, ts, cs[:, k]) for k in range(3)], -1).astype(np.float32)
        return np.broadcast_to(col[:, None, :], (self.h, self.w, 3)).copy()

    def over(self, color, alpha):
        """Paints a colour (3-vector or HxWx3) with a coverage map (HxW) over the canvas."""
        a = np.clip(alpha, 0, 1)[..., None]
        self.px = self.px * (1 - a) + np.asarray(color, np.float32) * a

    def add(self, color, amount):
        """Adds light: screen blend, so highlights roll off instead of clipping."""
        c = np.asarray(color, np.float32) * np.clip(amount, 0, None)[..., None]
        self.px = 1 - (1 - self.px) * (1 - np.clip(c, 0, 1))

    def mul(self, color, amount):
        a = np.clip(amount, 0, 1)[..., None]
        self.px = self.px * (1 - a + a * np.asarray(color, np.float32))

    def radial(self, cx, cy, rx, ry=None):
        ry = rx if ry is None else ry
        return np.sqrt(((self.xx - cx) / rx) ** 2 + ((self.yy - cy) / ry) ** 2)

    def poly(self, pts, soft=0.0):
        im = Image.new("L", (self.w, self.h), 0)
        ImageDraw.Draw(im).polygon([(float(x), float(y)) for x, y in pts], fill=255)
        m = np.asarray(im, np.float32) / 255.0
        return blur(m, soft) if soft else m

    def ellipse(self, cx, cy, rx, ry, soft=0.0):
        im = Image.new("L", (self.w, self.h), 0)
        ImageDraw.Draw(im).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
        m = np.asarray(im, np.float32) / 255.0
        return blur(m, soft) if soft else m

    def below_curve(self, ys, soft=1.0):
        """Coverage of everything below a per-column height curve (len w), antialiased over `soft` px."""
        return np.clip((self.yy - ys[None, :]) / soft + 0.5, 0, 1)

    def save(self, path, size=None, grain=0.0, seed=7):
        px = self.px
        if grain:
            rng = np.random.default_rng(seed)
            px = px + (rng.random(px.shape[:2], dtype=np.float32)[..., None] - 0.5) * grain
        im = Image.fromarray((np.clip(px, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
        if size:
            im = im.resize(size, Image.LANCZOS)
        im.save(path, optimize=True)
        return im
