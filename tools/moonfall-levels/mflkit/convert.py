"""The converter from this pipeline's scene recipes to the runtime's own (`moonfall-scene` version 1, extended; see
docs/design/v9/scene-recipe.md and the README's "The runtime recipe").

What the game paints at load and what we ship:
- A level on a game painting (the player's own install; nothing of Square Enix's ships): the runtime cuts and grades
  it, applies our `tone` and the jewel palette with its masks, as here. Everything the dress lays over the palette is
  geometry and our own light, independent of the painting's pixels, so it ships as plates (our own art, PNG):
    X = screen(P, A) * T,  then  X * (1 - a) + C * a,  then  screen(X, L)
  where P is the palette's output, A the screened light (shafts, glows, the chart's neat-line), T the chart's ticks
  (a multiplier), (C, a) the cloth and the silhouettes laid over (premultiplied into one), and L the light screened over
  them (the silhouettes' rim and snow, the small lights). `split_dress` computes the four from the same primitives the
  dress draws with, and `recompose` puts them back; the self-test holds the two equal.
- A level on our own painting: the dressed scene itself ships as the picture (grade none, no palette, no plates), so
  the runtime draws exactly what was measured here.
"""
import copy
import math

import numpy as np

from . import paths  # noqa: F401
import dress2 as D
from r2lib import hexc, screen, smooth, blur
from rich_lib import fbm


def _post_free(recipe):
    """The recipe with nothing laid over the palette: its dress gives the palette's output P."""
    r = copy.deepcopy(recipe)
    d = r.get("dress") or {}
    for k in ("shafts", "glows", "extras", "framing", "lights"):
        d[k] = []
    return r


def _silhouette_parts(ctx, mask, body="#05060E", rim="#9EB4FF", rim_k=0.55, rim_w=1.6, inner="#0C1230", inner_k=0.5,
                      alpha=1.0, light=(-0.707, -0.707), seed=1, snow=None):
    """dress2.silhouette's arithmetic, returning its parts: (alpha, body colour, the light screened over it)."""
    S = ctx.S
    m = np.clip(mask, 0, 1)
    mb = blur(m, rim_w * S * 0.6)
    gy, gx = np.gradient(mb)
    facing = np.clip(-(gx * light[0] + gy * light[1]) * S * 4.0, 0, 1)
    band = facing * m * smooth(0.0, 0.6, mb) * (1 - smooth(0.75, 1.0, blur(m, rim_w * S)))
    breaker = smooth(0.36, 0.64, fbm(m.shape[0], m.shape[1], 4.5 * S, 2, seed + 101))
    thick = smooth(0.35, 0.6, blur(m, 2.5 * S))
    rim_a = np.clip(band * 3.0, 0, 1) * rim_k * alpha * breaker * thick
    shade = blur(m, 14 * S)
    body_col = hexc(body) * (1 - inner_k * (1 - shade))[..., None] + hexc(inner) * (inner_k * (1 - shade))[..., None]
    lit = np.zeros(m.shape + (3,), np.float32)
    if snow is not None:
        up = np.clip(-gy * S * 6.0, 0, 1) * m
        lit = screen(lit, hexc(snow) * (np.clip(up * 2, 0, 1) * 0.30 * breaker)[..., None])
    lit = screen(lit, hexc(rim) * rim_a[..., None])
    return m * alpha, body_col, lit


def split_dress(recipe, sc, level, S):
    """(P, parts): the palette's output and the four plates (see the module's docstring), at S."""
    from .dress import Ctx, dress, draw_framing, auto_lights
    P, _ = dress(_post_free(recipe), sc, level, S)
    d = recipe.get("dress") or {}
    ctx = Ctx(level, S, recipe.get("features"))
    ctx.recipe = recipe
    h, w = int(600 * S), int(800 * S)
    A = np.zeros((h, w, 3), np.float32)
    for sh in d.get("shafts", []):
        A = D.shafts(A, S, origin=tuple(sh.get("origin", (-140, -220))), angles=tuple(sh["angles"]),
                     widths=tuple(sh["widths"]), k=min(0.08, sh.get("k", 0.07)), col=sh.get("col", "#BFD2FF"),
                     seed=sh.get("seed", 3), reach=sh.get("reach", 900.0), sway=0.0, near=sh.get("near", 150.0))
    for g in d.get("glows", []):
        A = D.glow(A, S, g["x"], g["y"], g["r"], g["col"], g["k"])
    T = np.ones((h, w), np.float32)
    for e in d.get("extras", []):
        if e["kind"] == "chart_border":
            t = D.chart_border(np.ones((h, w, 3), np.float32), S)[..., 0]
            A = D.chart_border(A, S) / t[..., None]
            T = T * t
        else:
            raise ValueError(f"no plate form for extra {e['kind']}")
    # the framing: cloth laid straight on (festoons), and the silhouettes' mask
    m0, m1 = np.zeros((h, w), np.float32), np.zeros((h, w), np.float32)
    f0, lights0 = draw_framing(Ctx(level, S, recipe.get("features")), np.zeros((h, w, 3), np.float32),
                               d.get("framing", []), m0)
    f1, _ = draw_framing(Ctx(level, S, recipe.get("features")), np.ones((h, w, 3), np.float32), d.get("framing", []), m1)
    a1 = np.clip(1 - (f1 - f0).mean(-1), 0, 1)
    F1 = f0                                                   # the cloth's colour times its alpha
    a, C, Lsil = np.zeros((h, w), np.float32), np.zeros((h, w, 3), np.float32), np.zeros((h, w, 3), np.float32)
    if m0.any():
        sil = d.get("silhouette", {})
        a2, B, Lsil = _silhouette_parts(ctx, m0, body=sil.get("body", "#05060E"), rim=sil.get("rim", "#9EB4FF"),
                                        rim_k=sil.get("rimK", 0.45), inner=sil.get("inner", "#0C1230"),
                                        inner_k=sil.get("innerK", 0.5), seed=sil.get("seed", 5), snow=sil.get("snow"))
    else:
        a2, B = np.zeros((h, w), np.float32), np.zeros((h, w, 3), np.float32)
    a = 1 - (1 - a1) * (1 - a2)
    Cp = F1 * (1 - a2)[..., None] + B * a2[..., None]        # premultiplied
    C = np.where(a[..., None] > 1e-6, Cp / np.maximum(a, 1e-6)[..., None], 0)
    # the small lights, screened last
    L = Lsil
    lc = Ctx(level, S, recipe.get("features"))
    for li in d.get("lights", []):
        pts = [tuple(p) for p in li.get("points", [])]
        if li.get("auto"):
            au = li["auto"]
            pts += auto_lights(lc, au.get("n", 9), tuple(au.get("box", (90, 380, 712, 516))), au.get("seed", 5),
                               li.get("halo", 5.0))
        if li.get("posts"):
            pts += lights0
        L = D.points(lc, L, pts, li.get("col", "#FFC86E"), r=li.get("r", 1.4), k=li.get("k", 0.8),
                     halo=li.get("halo", 5.0), hk=li.get("hk", 0.24))
    return P, {"A": A, "T": T, "C": C, "a": a, "L": L}


def recompose(P, parts):
    """The plates laid over the palette's output, as the runtime lays them."""
    X = screen(P, parts["A"]) * parts["T"][..., None]
    X = X * (1 - parts["a"][..., None]) + parts["C"] * parts["a"][..., None]
    return np.clip(screen(X, parts["L"]), 0, 1)
