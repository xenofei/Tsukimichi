"""Level scenes built on official FFXIV paintings (the loading-screen art in the player's own install), night graded
with the Medallion grade. One config per scene; the same recipe is what the plugin would run at load time (see
spec-rich.md, 'Scene recipe').

  py -3 scene_official.py [scene-id ...]

Sources ((c) SQUARE ENIX), read by tools/texdump from the game install:
  exp-p1  ui/loadingimage/-nowloading_base21.tex  Old Sharlayan
  exp-p3  ui/loadingimage/-nowloading_base25.tex  Mare Lamentorum
"""
import sys

import numpy as np

from rich_lib import (L, LUM, OUT_SCENES, VIOLET, blur, crop_to, grain, hexc, load_official, moon_glow, night_lab,
                      ramp, save_rgb, screen, smooth, vignette)

# The Far Shore (expansion) is a later hour: the same grade, pulled toward violet (rich_lib.VIOLET).

SCENES = {
    "exp-p1-sharlayan": dict(src="ui_loadingimage_-nowloading_base21.png", crop=(-32.0, 30.0, 1307.0, 980.0),
                             pad=(0, 64), grade=dict(gamma=1.8, sky_drop=0.35, exposure=0.70), glow=(-50, -60)),
    # mirrored: the planet's sunlit side is on the right in the painting; mirrored, its light comes from the left
    "exp-p3-mare-lamentorum": dict(src="ui_loadingimage_-nowloading_base25.png", crop=(557.0, -108.0, 1166.0, 875.0),
                                   mirror=True, pad=(120, 0), pad_mode="reflect",
                                   grade=dict(gamma=1.25, exposure=0.85, chroma_mid=0.42, chroma_high=0.6),
                                   glow=None, pool=True, sphere=(92.5, 325.0, 30.0)),
}


def reshade_sphere(px, S, cx, cy, r):
    """The tower's small sphere was lit from the right in the mirrored painting: repaint it as a dark stone sphere in
    silhouette, with a cool rim on its upper-left limb (the house light), keeping the painting's texture faintly."""
    H, W, _ = px.shape
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    u, v = (X - cx) / r, (Y - cy) / r
    d = np.sqrt(u * u + v * v)
    disc = np.clip((1 - d) * r * S + 0.5, 0, 1)
    tex = px @ LUM
    nz = np.sqrt(np.clip(1 - d * d, 0, 1))
    lam = np.clip(u * L[0] + v * L[1] + nz * L[2], 0, 1)
    body = hexc("#1A1838") * (0.55 + 0.6 * lam)[..., None] + (tex - tex.mean()) [..., None] * 0.15
    rim = np.clip((-(u * 0.707 + v * 0.707)), 0, 1) ** 2 * np.exp(-((1 - d) / 0.08) ** 2)
    body = screen(body, hexc("#B9B4E2") * (rim * 0.75)[..., None])
    return px * (1 - disc[..., None]) + body * disc[..., None]


def relight_sphere(px, S, cx, cy, r, bracket):
    """Round 4 (realism round 3): the tower's sphere, lit from the right in the mirrored painting, is relit in place
    from the upper left. Its visible pixels are multiplied by new / old Lambert shading, so its own texture and
    single limb are kept and nothing is painted over. The cradle's front bracket (polygon `bracket`, in units) is
    masked out, so it stays in front."""
    from PIL import Image, ImageDraw
    H, W, _ = px.shape
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    u, v = (X - cx) / r, (Y - cy) / r
    d2 = u * u + v * v
    disc = np.clip((1 - np.sqrt(d2)) * r * S * 0.7 + 0.5, 0, 1)
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    lam_new = np.clip(u * L[0] + v * L[1] + nz * L[2], 0, 1)
    # an opaque stone sphere shaded by the one light: lit face up to about 0.40 luma (under the board's ceiling),
    # its shadow side held a little above the sky by the planet's glow; the painting's fine texture rides on top
    shade = 0.09 + 0.24 * lam_new ** 1.2
    body = ramp(np.clip(shade / 0.44, 0, 1), [(0, "#14122E"), (0.5, "#4A4878"), (1, "#B4B4D6")])
    Yp = px @ LUM
    detail = (Yp - blur(Yp, 2.5 * S)) * 0.9
    body = np.clip(body + detail[..., None], 0, 1)
    rim = np.clip(-(u + v) * 0.7071, 0, 1) ** 2 * np.exp(-((1 - np.sqrt(d2)) / 0.07) ** 2)
    body = screen(body, hexc("#B9B4E2") * (rim * 0.35)[..., None])
    im = Image.new("L", (W * 3, H * 3), 0)
    ImageDraw.Draw(im).polygon([(x * S * 3, y * S * 3) for (x, y) in bracket], fill=255)
    br = blur(np.asarray(im.resize((W, H), Image.BOX), np.float32) / 255, 0.6 * S)
    m = disc * (1 - br)
    return px * (1 - m[..., None]) + body * m[..., None]


def still_pool(px, S, top=548.0):
    """A still pool of the Sea of Sorrows along the foot (the boat's water continues it): dark, the planet's glow and
    the spires broken into horizontal reflections, a faint lit edge where it meets the shore."""
    H, W, _ = px.shape
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    # (round 3: a true mirror, never brighter than what it reflects; it darkens toward the viewer and its colour runs
    # into the boat's lapis water strip at the foot)
    m = np.clip((Y - top) * S + 0.5, 0, 1)
    depth = np.clip((Y - top) / (600 - top), 0, 1)
    ry = np.clip(2 * top - Y, 0, top - 1)
    iy = np.clip((ry * S).astype(int), 0, H - 1)
    shift = (np.sin(Y * 1.3) * (1 + depth * 4) * S).astype(int)
    ix = np.clip(xx.astype(int) + shift, 0, W - 1)
    refl = blur(px, 1.0 * S)[iy, ix]
    bands = 0.70 + 0.30 * (np.sin(Y * 2.4) > -0.2)
    keep = (0.80 - 0.45 * depth) * bands                  # the mirror's own loss, more toward the viewer
    deep = ramp(depth, [(0, "#0A0A1E"), (0.6, "#0B1028"), (1, "#0E1530")])
    water = refl * keep[..., None] + deep * (1 - keep[..., None]) * 0.6
    water = np.minimum(water, refl * 0.95 + 0.004)
    out = px * (1 - m[..., None]) + water * m[..., None]
    edge = np.exp(-((Y - top - 0.6) / 0.6) ** 2) * m
    return screen(out, hexc("#8C86C6") * (edge * 0.12)[..., None])


def build(cfg, S=1):
    src = load_official(cfg["src"])
    if cfg.get("mirror"):
        src = src[:, ::-1]
    top, left = cfg.get("pad", (0, 0))
    if top or left:
        src = np.pad(src, ((top, 0), (left, 0), (0, 0)), mode=cfg.get("pad_mode", "edge"))
    x, y, w, h = cfg["crop"]
    W, H = int(800 * S), int(600 * S)
    px = crop_to(src, (x + left, y + top, w, h), (W, H))
    g = dict(VIOLET)
    g.update(cfg.get("grade", {}))
    sky = None
    if g.get("sky_drop"):
        yy = np.mgrid[0:H, 0:W][0] / H
        sky = blur(smooth(0.02, 0.15, px[..., 2] - px[..., 0]) * smooth(0.75, 0.30, yy), 3 * S)
    out = night_lab(px, sky=sky, S=S, **g)
    if cfg.get("glow"):
        gx, gy = cfg["glow"]
        out = moon_glow(out, gx * S, gy * S, 330 * S, 900 * S, 0.11, 0.05, col="#C4C0EE")
    if cfg.get("sphere"):
        out = relight_sphere(out, S, *cfg["sphere"], bracket=[(35, 307), (75, 338), (97, 341), (118, 357), (125, 362), (125, 380), (30, 380)])
    if cfg.get("pool"):
        out = still_pool(out, S)
    out = vignette(out, 0.28)
    return grain(out, 0.008, seed=13)


if __name__ == "__main__":
    ids = sys.argv[1:] or list(SCENES)
    for sid in ids:
        for S in (1, 2):
            save_rgb(build(SCENES[sid], S), OUT_SCENES / f"{sid}{'@2x' if S == 2 else ''}.png")
        print("ok", sid)
