"""The converter from this pipeline's scene recipes to the runtime's own (`moonfall-scene` version 1, extended; see
docs/design/v9/scene-recipe.md and the README's "The runtime recipe").

What the game builds at load and what we ship:
- The painting. A game painting is read from the player's install and cut, erased and night graded by the runtime (no
  pixel of Square Enix's ships). Our own painting ships as a picture, undressed and without its grain (the runtime adds the
  grain): `Tsukimichi/assets/moonfall/scenes/<scene>@2x.png`.
- Under the palette (`paint`): the moon's glow (a game painting's `glow`), our `tone`, and an engraved route as two plates
  (its dark cut multiplied, its gilt screened).
- The jewel palette, with every mask term the pipeline draws (`poly`, `line`, `land` and the keep masks as `spare`) and its
  quiet as `near` terms, as `dress.py` draws them.
- Over the palette (`light`): the glows as runtime glows; a moving shaft for the beams (`beamsOnly`: only its 15% moving
  share is made); then the plates, all our own art:
    X = screen(P, A) * T,  X = X * K + Cp,  X = screen(X, R)
  where A is the shafts' still share (85%) and the chart's neat-line light, T the chart's ticks (a multiplier), K one less
  the framing's coverage (cloth and silhouettes; the runtime's framing cover is one less it), Cp their colour premultiplied,
  and R the silhouettes' rim and snow. `split_dress` computes them from the same primitives the dress draws with.
- The small lights as runtime lights (lanterns flicker in play), and the recipe's `runtime` block: the motion (stars,
  mist, fireflies, dust in the beams), which lights flicker, and the moon Fever swells.

The gate (`check`): the runtime's bare scene (MoonfallRender --scene-only: the scene over the opening with the beams at
rest, before the veil and the pieces, grain off) against this pipeline's dressed scene (grain off), OKLab distance over the
opening, max and 99.9th percentile.
"""
import contextlib
import copy
import json
import subprocess

import numpy as np
from PIL import Image

from . import paths  # noqa: F401
from .paths import CACHE, JSON_OUT, REPO, cache_name
import dress2 as D
import rich_lib as RL
from r2lib import hexc, screen, smooth, blur, srgb_to_oklab
from rich_lib import fbm

RUNTIME_SCENES = REPO / "Tsukimichi.Core" / "Moonfall" / "Levels" / "scenes"
RUNTIME_LEVELS = REPO / "Tsukimichi.Core" / "Moonfall" / "Levels"
PICTURES = REPO / "Tsukimichi" / "assets" / "moonfall" / "scenes"
FALLBACK = "moon-road-night"          # our own night painting of no place: a game painting's stand-in
BEAMS_STILL = 0.85                    # MoonfallSceneBuilder.BeamBreath: the beams add the other 15% at rest
GATE = 0.02                           # the converter's gate: OKLab max or 99.9th percentile (critic rounds 4-5)
OPENING = (75, 41, 725, 594)          # MoonfallSceneBuilder.OpeningRect: the base layer's board rectangle
RENDER = REPO / "tools" / "Tsukimichi.MoonfallRender"
RENDER_DLL = RENDER / "bin" / "Release" / "net10.0-windows" / "Tsukimichi.MoonfallRender.dll"
GRADE_KEYS = {"exposure": "exposure", "gamma": "gamma", "ceiling": "ceiling", "knee": "knee", "detail": "detail",
              "chroma_mid": "chromaMid", "chroma_high": "chromaHigh", "tint": "tint", "tint_k": "tintK",
              "base_hue": "baseHue", "warm_keep": "warmKeep", "form": "form", "form_r": "formRadius", "band_k": "bandK"}


@contextlib.contextmanager
def no_grain():
    """Every grain the pipeline adds (the painters' and the graded scene's) left out, for the gate and the pictures."""
    keep = RL.grain
    RL.grain = lambda px, amt=0.012, seed=3: np.clip(px, 0, 1)
    try:
        yield
    finally:
        RL.grain = keep


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


def small_lights(recipe, level, S=1):
    """The dress's small lights as the runtime's: every point resolved (auto-placed ones and the lantern posts'), those the
    dress leaves out (within 8 units plus its halo of a piece) dropped, in the dress's order."""
    from .dress import Ctx, draw_framing, auto_lights
    d = recipe.get("dress") or {}
    h, w = int(600 * S), int(800 * S)
    _, posts = draw_framing(Ctx(level, S, recipe.get("features")), np.zeros((h, w, 3), np.float32), d.get("framing", []),
                            np.zeros((h, w), np.float32))
    lc = Ctx(level, S, recipe.get("features"))
    flicker = (recipe.get("runtime") or {}).get("flicker", [])
    out = []
    for n, li in enumerate(d.get("lights", [])):
        pts = [tuple(p) for p in li.get("points", [])]
        if li.get("auto"):
            au = li["auto"]
            pts += auto_lights(lc, au.get("n", 9), tuple(au.get("box", (90, 380, 712, 516))), au.get("seed", 5),
                               li.get("halo", 5.0))
        if li.get("posts"):
            pts += posts
        halo = li.get("halo", 5.0)
        for (x, y, *rest) in pts:
            s = rest[0] if rest else 1.0
            if lc.clear(x, y) < 8 + halo * s * 0.6:
                continue
            out.append({"x": round(float(x), 3), "y": round(float(y), 3), "size": round(float(s), 4),
                        "colour": li.get("col", "#FFC86E"), "core": li.get("r", 1.4), "k": li.get("k", 0.8),
                        "halo": halo, "haloK": li.get("hk", 0.24), "flicker": n in flicker})
    return out


def split_dress(recipe, sc, level, S):
    """(P, parts): the palette's output and the plates (see the module's docstring), at S. A: the shafts' still share and
    the chart's neat-line light (screened); T: the chart's ticks (multiplied); K: one less the framing's coverage
    (multiplied); C: the framing's colour premultiplied (added); R: the rim and snow (screened). Glows and small lights
    are left to the runtime (`runtime_recipe`)."""
    from .dress import Ctx, dress, draw_framing
    P, _ = dress(_post_free(recipe), sc, level, S)
    d = recipe.get("dress") or {}
    ctx = Ctx(level, S, recipe.get("features"))
    ctx.recipe = recipe
    h, w = int(600 * S), int(800 * S)
    A = np.zeros((h, w, 3), np.float32)
    for sh in d.get("shafts", []):
        A = D.shafts(A, S, origin=tuple(sh.get("origin", (-140, -220))), angles=tuple(sh["angles"]),
                     widths=tuple(sh["widths"]), k=min(0.08, sh.get("k", 0.07)) * BEAMS_STILL, col=sh.get("col", "#BFD2FF"),
                     seed=sh.get("seed", 3), reach=sh.get("reach", 900.0), sway=0.0, near=sh.get("near", 150.0))
    T = np.ones((h, w), np.float32)
    for e in d.get("extras", []):
        if e["kind"] == "chart_border":
            t = D.chart_border(np.ones((h, w, 3), np.float32), S)[..., 0]
            A = screen(A, D.chart_border(np.zeros((h, w, 3), np.float32), S) / np.maximum(t, 1e-6)[..., None])
            T = T * t
        else:
            raise ValueError(f"no plate form for extra {e['kind']}")
    m0 = np.zeros((h, w), np.float32)
    f0, _ = draw_framing(Ctx(level, S, recipe.get("features")), np.zeros((h, w, 3), np.float32), d.get("framing", []), m0)
    f1, _ = draw_framing(Ctx(level, S, recipe.get("features")), np.ones((h, w, 3), np.float32), d.get("framing", []),
                         np.zeros((h, w), np.float32))
    a1 = np.clip(1 - (f1 - f0).mean(-1), 0, 1)
    if m0.any():
        sil = d.get("silhouette", {})
        a2, B, R = _silhouette_parts(ctx, m0, body=sil.get("body", "#05060E"), rim=sil.get("rim", "#9EB4FF"),
                                     rim_k=sil.get("rimK", 0.45), inner=sil.get("inner", "#0C1230"),
                                     inner_k=sil.get("innerK", 0.5), seed=sil.get("seed", 5), snow=sil.get("snow"))
    else:
        a2, B, R = np.zeros((h, w), np.float32), np.zeros((h, w, 3), np.float32), np.zeros((h, w, 3), np.float32)
    K = (1 - a1) * (1 - a2)
    C = f0 * (1 - a2)[..., None] + B * a2[..., None]
    return P, {"A": A, "T": T, "K": K, "C": C, "R": R}


def recompose(P, parts, glows=(), S=2):
    """The plates laid over the palette's output, as the runtime lays them (glows first: screens commute)."""
    X = P
    for g in glows:
        X = D.glow(X, S, g["x"], g["y"], g["r"], g["col"], g["k"])
    X = screen(X, parts["A"]) * parts["T"][..., None]
    X = X * parts["K"][..., None] + parts["C"]
    return np.clip(screen(X, parts["R"]), 0, 1)


# ------------------------------------------------------------------------------------------------ the recipe
def _points(features, name):
    feat = features[name]
    pts = feat["points"] if isinstance(feat, dict) else feat
    return [[round(float(x), 3), round(float(y), 3)] for (x, y) in pts]


def mask_terms(spec, recipe):
    """A pipeline mask (a product of ramps, dress.mask_of) as the runtime's terms."""
    feats = recipe.get("features") or {}
    out = []
    for t in spec:
        k = t[0]
        if k in ("x", "y"):
            out.append({k: [t[1], t[2]]})
        elif k == "luma":
            out.append({"lum": [t[1], t[2]], "blur": t[3] if len(t) > 3 else 3})
        elif k == "disc":
            _, cx, cy, r, f = t
            out.append({"disc": [cx, cy, r, abs(f)], **({"invert": True} if f < 0 else {})})
        elif k in ("poly", "not-poly"):
            out.append({"poly": _points(feats, t[1]), "blur": (t[2] if len(t) > 2 else 8.0) / 2,
                        **({"invert": True} if k == "not-poly" else {})})
        elif k == "near":
            out.append({"line": [t[3], t[2]], "points": _points(feats, t[1])})
        elif k in ("mask", "not-mask"):
            spec_m = recipe["masks"][t[1]]
            if spec_m.get("kind", "rim-fill") != "rim-fill" or spec_m.get("outside"):
                raise ValueError(f"no runtime form for the mask {t[1]}: {spec_m}")
            out.append({"land": [spec_m.get("threshold", 0.75), spec_m.get("grow", 5)],
                        "blur": (t[2] if len(t) > 2 else 6.0) / 2, **({"invert": True} if k == "not-mask" else {})})
        else:
            raise ValueError(f"no runtime form for the mask term {t}")
    return out


def _palette(recipe):
    from .dress import QUIET_BLUR, QUIET_KEEP, QUIET_REGION
    j = (recipe.get("dress") or {}).get("jewel")
    if not j:
        return None
    quiet = None
    if j.get("quiet"):
        a, b = j["quiet"]
        quiet = {"near": [a, b], "blur": min(float(j.get("quietBlur", QUIET_BLUR)), 40.0), "invert": True}
    rq = float(j.get("regionQuiet", QUIET_REGION))
    regions = []
    for r in j.get("regions", []):
        where = mask_terms(r.get("mask", []), recipe) + ([dict(quiet, scale=rq)] if quiet else [])
        regions.append({"hue": r["hex"], "chroma": r.get("chroma", 0.1), "where": where})
    keeps = ([j["keepMask"]] if j.get("keepMask") else []) + list(j.get("keepMasks", []))
    p = {"bands": [list(b) for b in j["bands"]], "chroma": j.get("chroma", 0.9), "floor": j.get("floor", 0.03),
         "keep": j.get("keep", 0.30), "keepHigh": j.get("keepHi", 0.75), "mix": j.get("mix", 0.5)}
    if j.get("valueHues"):
        p["valueHues"] = [list(v) for v in j["valueHues"]]
    if regions:
        p["regions"] = regions
    if quiet:
        p["where"] = [dict(quiet, scale=QUIET_KEEP)]
    if keeps:
        p["spare"] = [mask_terms(k, recipe) for k in keeps]
    return p


def plate_names(recipe):
    """The plates a scene ships (blend, cover, layer), in the order the runtime lays them, by what the dress holds."""
    d = recipe.get("dress") or {}
    name = recipe["name"]
    paint, light = [], []
    if any(o["kind"] == "route" for o in recipe.get("overlays", [])):
        paint += [(f"{name}-route-cut", "multiply", False, "route-cut"), (f"{name}-route", "screen", False, "route")]
    if d.get("shafts") or d.get("extras"):
        light.append((f"{name}-light", "screen", False, "A"))
    if d.get("extras"):
        light.append((f"{name}-ticks", "multiply", False, "T"))
    if d.get("framing"):
        light += [(f"{name}-cover", "multiply", True, "K"), (f"{name}-cloth", "add", False, "C"),
                  (f"{name}-rim", "screen", False, "R")]
    return paint, light


def runtime_recipe(recipe, level):
    """The runtime recipe (a dict, `moonfall-scene` version 1) for this pipeline recipe on this level."""
    src = recipe["source"]
    d = recipe.get("dress") or {}
    rt = recipe.get("runtime") or {}
    out = {"format": "moonfall-scene", "version": 1, "name": recipe["name"]}
    paint, light = [], []
    if src["kind"] == "game":
        s = {"game": src["texture"]}
        if src.get("mirror"):
            s["mirror"] = True
        if any(src.get("pad", (0, 0))):
            s["pad"] = list(src["pad"])
            s["padMode"] = src.get("padMode", "edge")
        s["crop"] = list(src["crop"])
        if abs(src["crop"][2] / src["crop"][3] - 4 / 3) > 0.02:
            s["squeeze"] = True              # the pipeline resamples any crop to the board: a squarer one is squeezed
        if src.get("erase"):
            s["erase"] = [list(b) for b in src["erase"]]
        out["source"] = s
        out["fallback"] = FALLBACK
        g = {"kind": "violet" if src.get("hour") == "far" else "night"}
        for k, v in src.get("grade", {}).items():
            g[GRADE_KEYS[k]] = v
        if src.get("skyDrop"):
            g.update(skyDrop=src["skyDrop"], skyTop=0.30, skyBottom=0.75)
        out["grade"] = g
        if src.get("glow"):
            gx, gy = src["glow"]
            paint.append({"kind": "moonGlow", "x": gx, "y": gy, "rCore": 330, "rWide": 900, "kCore": 0.11, "kWide": 0.05,
                          "colour": "#C4C0EE"})
        out["vignette"] = recipe.get("vignette", 0.28)
    else:
        out["source"] = {"picture": recipe["name"]}
    for tn in recipe.get("tone", []):
        paint.append({"kind": "tone", "where": mask_terms(tn["mask"], recipe), "mul": tn["mul"]})
    p_plates, l_plates = plate_names(recipe)
    paint += [{"kind": "plate", "picture": n, "blend": b, **({"cover": True} if c else {})} for (n, b, c, _) in p_plates]
    out["grain"] = 0.008
    if paint:
        out["paint"] = paint
    pal = _palette(recipe)
    if pal:
        out["palette"] = pal
    for g in d.get("glows", []):
        light.append({"kind": "glow", "x": g["x"], "y": g["y"], "r": g["r"], "colour": g["col"], "k": g["k"]})
    for sh in d.get("shafts", []):
        light.append({"kind": "shafts", "origin": list(sh.get("origin", (-140, -220))), "angles": list(sh["angles"]),
                      "widths": list(sh["widths"]), "k": min(0.08, sh.get("k", 0.07)), "colour": sh.get("col", "#BFD2FF"),
                      "seed": sh.get("seed", 3), "reach": sh.get("reach", 900.0), "near": sh.get("near", 150.0),
                      "moving": True, "beamsOnly": True})
    light += [{"kind": "plate", "picture": n, "blend": b, **({"cover": True} if c else {})} for (n, b, c, _) in l_plates]
    if light:
        out["light"] = light
    lights = small_lights(recipe, level)
    if lights:
        out["lights"] = [{k: v for k, v in li.items() if not (k == "flicker" and not v)} for li in lights]
    out["veil"] = recipe.get("veil", 0.40)
    if rt.get("motion"):
        out["motion"] = rt["motion"]
    if rt.get("fireflies"):
        out["fireflies"] = rt["fireflies"]
    if rt.get("feverMoon"):
        out["feverMoon"] = rt["feverMoon"]
    pal_c = d.get("palette") or {}
    if all(k in pal_c for k in ("sky", "deep", "jewel1", "jewel2")):
        out["chrome"] = {k: pal_c[k] for k in ("sky", "deep", "jewel1", "jewel2")}
    return out


# ------------------------------------------------------------------------------------------------ writing
def _level(level_id):
    return json.loads((JSON_OUT / f"{level_id}.json").read_text(encoding="utf-8"))


def graded(recipe, S):
    """The pipeline's graded scene with no grain, 8-bit as the build caches it."""
    from .scene import graded as g
    with no_grain():
        px = g(recipe, S)
    return np.asarray(RL.to_u8(px), np.float32) / 255


def picture(recipe, S=2):
    """Our painting as it ships: the painter's own pixels, no grain (the runtime adds it), no tone (the runtime's)."""
    from .scene import painter
    src = recipe["source"]
    with no_grain():
        return np.clip(painter(src["painter"]).paint(S, **src.get("params", {})), 0, 1)


def _png(arr, path, grey=False):
    u8 = RL.to_u8(np.repeat(arr[..., None], 3, -1) if arr.ndim == 2 else arr)
    im = Image.fromarray(u8[..., 0], "L") if grey else Image.fromarray(u8, "RGB")
    im.save(path, optimize=True)
    return path.stat().st_size


def _route_parts(recipe, S):
    """The routes' plates: (the dark cut to multiply, the gilt to screen), as scene_airship_road.dashed draws them."""
    import scene_airship_road as sar
    h, w = int(600 * S), int(800 * S)
    cut = np.ones((h, w, 3), np.float32)
    lit = np.zeros((h, w, 3), np.float32)
    for ov in recipe.get("overlays", []):
        if ov["kind"] != "route":
            raise ValueError(f"no plate form for the overlay {ov['kind']}")
        pts = RL.catmull([tuple(p) for p in ov["points"]], 8) if ov.get("smooth", True) else ov["points"]
        # dashed(px) = screen(px * c, g): with px = 1 it gives c's effect on white... so draw each part alone
        c1 = sar.dashed(np.ones((h, w, 3), np.float32), S, pts, "#000000", width=ov.get("width", 2.0), dash=ov.get("dash", 5.0),
                        gap=ov.get("gap", 4.5), alpha=ov.get("alpha", 0.6))
        g1 = sar.dashed(np.zeros((h, w, 3), np.float32), S, pts, ov.get("color", "#D9BE82"), width=ov.get("width", 2.0),
                        dash=ov.get("dash", 5.0), gap=ov.get("gap", 4.5), alpha=ov.get("alpha", 0.6))
        # in sequence the routes compose as screen(screen(x*c1, g1)*c2, g2): exact only when they do not cross; the
        # check holds the composed plates against the overlays drawn in order
        cut = cut * c1
        lit = screen(lit * c1, g1)
    return cut, lit


def write(level_id, S=2):
    """Writes the level's runtime recipe and its pictures; returns (recipe name, {file: bytes})."""
    level = _level(level_id)
    from .scene import load_recipe
    recipe = load_recipe(level["scene"])
    rt = runtime_recipe(recipe, level)
    sizes = {}
    RUNTIME_SCENES.mkdir(parents=True, exist_ok=True)
    text = json.dumps(rt, indent=1, ensure_ascii=False)
    from .scene import dump_recipe
    path = RUNTIME_SCENES / f"{recipe['name']}.json"
    dump_recipe(rt, path)
    sizes[path.name] = path.stat().st_size
    del text
    if recipe["source"]["kind"] == "painting":
        sizes[f"{recipe['name']}@2x.png"] = _png(picture(recipe, S), PICTURES / f"{recipe['name']}@2x.png")
    p_plates, l_plates = plate_names(recipe)
    if p_plates:
        cut, lit = _route_parts(recipe, S)
        parts = {"route-cut": cut[..., 0], "route": lit}
        for (n, _b, _c, part) in p_plates:
            sizes[f"{n}@2x.png"] = _png(parts[part], PICTURES / f"{n}@2x.png", grey=parts[part].ndim == 2)
    if l_plates:
        sc = graded(recipe, S)
        _, parts = split_dress(recipe, sc, level, S)
        for (n, _b, _c, part) in l_plates:
            arr = parts[part]
            sizes[f"{n}@2x.png"] = _png(arr, PICTURES / f"{n}@2x.png", grey=arr.ndim == 2)
    return recipe["name"], sizes


# ------------------------------------------------------------------------------------------------ the gate
def reference(level_id, S=2):
    """The pipeline's dressed scene for the level, grain off (float RGB at S)."""
    from .dress import dress, runtime_gamut
    from .scene import load_recipe
    level = _level(level_id)
    recipe = load_recipe(level["scene"])
    with runtime_gamut():
        d, _ = dress(recipe, graded(recipe, S), level, S)
    return d


def runtime_scene(level_id, S, out):
    """MoonfallRender's bare scene for the level at tier S (the opening, beams at rest, no veil, grain off)."""
    cmd = ["dotnet", str(RENDER_DLL), str(out), "--level", level_id, "--scene-only", str(S), "--no-grain"]
    r = subprocess.run(cmd, capture_output=True, text=True, cwd=REPO)
    if r.returncode != 0:
        raise RuntimeError(f"MoonfallRender failed for {level_id}: {r.stdout}\n{r.stderr}")
    return np.asarray(Image.open(out).convert("RGB"), np.float32) / 255


def compare(a, b):
    """OKLab distance between two pictures of the opening: (max, 99.9th percentile, 99th percentile, mean)."""
    d = np.sqrt(((srgb_to_oklab(a) - srgb_to_oklab(b)) ** 2).sum(-1))
    return float(d.max()), float(np.percentile(d, 99.9)), float(np.percentile(d, 99)), float(d.mean())


def opening(px, S):
    x0, y0, x1, y1 = OPENING
    return px[int(y0 * S):int(np.ceil(y1 * S)), int(x0 * S):int(np.ceil(x1 * S))]


def check(level_id, S, scratch):
    """The gate for one level at tier S: (max, p99.9, p99, mean) and the worst place (board units). At 1x the pipeline's
    own 1x is its 2x scene downsized (Lanczos), as build.py makes it."""
    if S == 1:
        ref = np.asarray(Image.fromarray(RL.to_u8(reference(level_id, 2))).resize((800, 600), Image.LANCZOS), np.float32) / 255
    else:
        ref = np.asarray(RL.to_u8(reference(level_id, S)), np.float32) / 255
    ref = opening(ref, S)
    got = runtime_scene(level_id, S, scratch / f"{level_id}-bare@{S}x.png")
    if got.shape != ref.shape:
        raise RuntimeError(f"{level_id}: the runtime's opening is {got.shape}, the pipeline's {ref.shape}")
    figs = compare(ref, got)
    d = np.sqrt(((srgb_to_oklab(ref) - srgb_to_oklab(got)) ** 2).sum(-1))
    yy, xx = np.unravel_index(int(d.argmax()), d.shape)
    worst = (round(OPENING[0] + (xx + 0.5) / S, 1), round(OPENING[1] + (yy + 0.5) / S, 1))
    diff = np.clip(d / 0.05, 0, 1)
    Image.fromarray((diff * 255).astype(np.uint8), "L").save(scratch / f"{level_id}-diff@{S}x.png")
    return figs, worst
