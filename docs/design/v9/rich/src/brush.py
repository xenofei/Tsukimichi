"""A painterly stroke pass for the paintings made in code (the rich pass's answer to "it looks plain").

A painting is first built as shaded forms (light, shadow, colour), then repainted with thousands of short opaque
strokes: each stroke takes its colour from the shaded form under it (with a small jitter in value and hue), follows the
form's flow (feathers along a body, grass upward, cloud along its drift) and is clipped to its own part, so edges stay
where the drawing put them. Strokes are drawn at twice the device resolution and reduced, so they are antialiased.
"""
import math

import numpy as np
from PIL import Image, ImageDraw


def strokes(base, mask, S, flow, n, length=(4.0, 9.0), width=(1.2, 2.2), jitter=0.06, seed=0, taper=True,
            hue_jitter=0.02, ss=2, density=None, bias=0.0):
    """Repaints `base` (HxWx3 float, device px) inside `mask` (HxW 0..1) with `n` strokes.

    flow(x, y) -> angle in radians (units in, the stroke's direction); length/width in units; density (HxW, optional)
    weights where strokes land. bias lifts or lowers every stroke's value a little (a glaze)."""
    H, W, _ = base.shape
    rng = np.random.default_rng(seed)
    w8 = Image.fromarray((np.clip(base, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").resize((W * ss, H * ss), Image.BILINEAR)
    lay = w8.copy()
    dr = ImageDraw.Draw(lay)
    m = mask if density is None else mask * density
    flat = m.ravel()
    tot = flat.sum()
    if tot <= 0:
        return base
    idx = rng.choice(flat.size, size=n, p=flat / tot)
    ys, xs = np.divmod(idx, W)
    xs = xs + rng.random(n)
    ys = ys + rng.random(n)
    order = np.argsort(rng.random(n))
    for k in order:
        x, y = xs[k], ys[k]
        ux, uy = x / S, y / S
        a = flow(ux, uy) + rng.normal(0, 0.12)
        L_ = rng.uniform(*length) * S
        w_ = rng.uniform(*width) * S
        dx, dy = math.cos(a) * L_ / 2, math.sin(a) * L_ / 2
        c = base[min(H - 1, int(y)), min(W - 1, int(x))]
        v = 1 + rng.normal(bias, jitter)
        h = rng.normal(0, hue_jitter, 3)
        col = tuple(int(np.clip((c[i] * v + h[i]) * 255, 0, 255)) for i in range(3))
        p0 = ((x - dx) * ss, (y - dy) * ss)
        p1 = ((x + dx) * ss, (y + dy) * ss)
        ww = max(1, int(round(w_ * ss)))
        dr.line([p0, p1], fill=col, width=ww)
        if taper:
            r = ww / 2
            dr.ellipse([p0[0] - r * 0.6, p0[1] - r * 0.6, p0[0] + r * 0.6, p0[1] + r * 0.6], fill=col)
        else:
            r = ww / 2
            for p in (p0, p1):
                dr.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=col)
    out = np.asarray(lay.resize((W, H), Image.BOX), np.float32) / 255
    return base * (1 - mask[..., None]) + out * mask[..., None]


def flow_const(angle):
    return lambda x, y: angle


def flow_radial(cx, cy, offset=0.0):
    return lambda x, y: math.atan2(y - cy, x - cx) + offset


def flow_field(field, S):
    """A flow from an angle array in device px."""
    H, W = field.shape
    return lambda x, y: float(field[min(H - 1, max(0, int(y * S))), min(W - 1, max(0, int(x * S)))])
