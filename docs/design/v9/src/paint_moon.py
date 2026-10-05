"""The near-full moon for Moonfall's paintings (the What's new art and the campaign tiles), after supervision round 1.

One circular limb. Inside it the disc is shaded as a sphere in the sun's light (a Lommel-Seeliger face, flat as the
real moon's), with a soft terminator and an unlit part about 0.2 r wide on the side away from the sun, held a touch
above the local sky by earthshine so the limb stays one circle. Four to six broken, lobed maria of different depth
across the upper middle (no ring, no hook, no single lozenge). A neutral, barely warm lit tone, never yellow.
Writes the masks the Option B treatments read: moon (the disc), moonlit (the lit part) and seas.
"""
import math

import numpy as np

from artlib import blur, hexc, smooth

# (x, y, rx, ry, depth, lobe phase) in disc radii: Imbrium, Serenitatis, Tranquillitatis, Fecunditatis, a faint
# Procellarum on the left limb and a small Nubium below; separate seas, clustered, not along one curve
MARIA = [(-0.30, -0.24, 0.25, 0.19, 1.00, 0.4), (0.10, -0.32, 0.17, 0.14, 0.85, 1.6), (0.32, -0.06, 0.19, 0.16, 0.90, 2.3),
         (0.46, 0.22, 0.10, 0.12, 0.60, 0.9), (-0.56, 0.06, 0.12, 0.26, 0.55, 2.8), (-0.14, 0.30, 0.12, 0.08, 0.45, 1.2)]


def maria(u, v):
    acc = np.zeros_like(u)
    for (sx, sy, rx, ry, k, ph) in MARIA:
        du, dv = (u - sx) / rx, (v - sy) / ry
        rr = np.sqrt(du * du + dv * dv)
        th = np.arctan2(dv, du)
        edge = 1 + 0.16 * np.sin(3 * th + ph) + 0.08 * np.sin(5 * th + 2 * ph)
        acc = np.maximum(acc, smooth(edge + 0.20, edge - 0.22, rr) * k)
    return acc


def moon(c, M, mx, my, mr, sun=(0.919, 0.395), phase_cos=0.80, tone="#ECEBE4", sea_tone="#A8A8A6"):
    """Paints the moon on canvas c (artlib.Canvas) at (mx, my), radius mr px. sun: the screen direction toward the
    sun (here lower right: the sun is far below the horizon), so the unlit part is on the upper left."""
    ux, uy = sun
    n = math.hypot(ux, uy)
    ux, uy = ux / n, uy / n
    sphi = math.sqrt(1 - phase_cos ** 2)
    Ls = np.array([ux * sphi, uy * sphi, phase_cos], np.float32)
    x0, x1 = max(0, int(mx - mr * 14)), min(c.w, int(mx + mr * 14))
    y0, y1 = max(0, int(my - mr * 14)), min(c.h, int(my + mr * 14))
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    u, v = (xx + 0.5 - mx) / mr, (yy + 0.5 - my) / mr
    d2 = u * u + v * v
    d = np.sqrt(d2)
    disc = np.clip((1 - d) * mr + 0.5, 0, 1)
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    mu0 = u * Ls[0] + v * Ls[1] + nz * Ls[2]
    ls = np.minimum(np.where(mu0 > 0, mu0 / (mu0 + nz + 1e-4), 0.0) / 0.5, 1.10)
    lit = blur(smooth(-0.05, 0.08, mu0), 0.10 * mr)                                # a soft terminator inside the disc
    shade = (0.82 * ls + 0.18 * np.clip(mu0, 0, 1))
    s = maria(u, v)
    col = hexc(tone) * (1 - s[..., None] * 0.42) + hexc(sea_tone) * (s[..., None] * 0.42)
    face = col * np.clip(shade, 0, 1.05)[..., None]
    sky = c.px[y0:y1, x0:x1].copy()
    # the halo first (the moon's own light scattered in the air), then the disc over it
    glow = np.exp(-(d / 4.5) ** 2 * 2.2) * 0.16 + np.exp(-(d / 12) ** 2 * 2.0) * 0.08
    halo = 1 - (1 - sky) * (1 - hexc("#DCE6FF") * glow[..., None])
    earth = halo * 1.06                                                             # earthshine: just above the sky round it
    disc_col = face * lit[..., None] + earth * (1 - lit[..., None])
    out = halo * (1 - disc[..., None]) + disc_col * disc[..., None]
    c.px[y0:y1, x0:x1] = out
    for key in ("moon", "moonlit", "seas"):
        if key not in M:
            M[key] = np.zeros((c.h, c.w), np.float32)
    M["moon"][y0:y1, x0:x1] = disc
    M["moonlit"][y0:y1, x0:x1] = disc * lit
    M["seas"][y0:y1, x0:x1] = s * disc
