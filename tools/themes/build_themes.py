# The multi-theme build (feature plan v7 T4; docs/research/plan-v7/theme-system.md, sections 6.4 and 7.1).
#
# Reads one manifest per glyph set (tools/themes/sets/<set>.json), renders its approved SVG masters with headless Chrome
# (the same pipeline as docs/design/moon-v6/round5/gen_atlas.py, which stays as history), and writes per set:
#   medals.png, medals@2x.png, medals.json   the hero atlas, in Medallion's layout to the pixel (MedalLayout): the same
#                                           eleven sprites at 48/64/96/128 px, 2 px apart, and the same JSON schema
#   row.png, row.json                       the row strip: each state's row-tier master at every whole device pixel
#                                           from 12 to 31 (a real render at that size, not a downscale); 1x only
#   metrics.json                            the per-set gates (section 7.1) and the cross-set similarity table (5.2)
# and, outside the repo, contact sheets, a cross-set heatmap and a plain-text report (--out, default a temp folder).
#
# Each tier takes its source explicitly from the manifest (Ishgard Glass's _mid/ for 48 and 64 px, its full masters
# for 96 and 128), never from media queries. Ready on another job ships once per role seat with the seat left empty,
# as Medallion does: the plugin draws the game's job icon into it.
#
# Metrics are round 5's metrics.py, generalised: each state at 16 and 20 px in a 40 px cell on a palette window,
# blurred (0.6) greyscale luminance; distinctness is the summed difference of a pair, salience the summed difference
# from the window. Vision modes: greyscale, Vienot deuteranopia (round 5's), and Machado 2009 deuteranopia,
# protanopia and tritanopia (Core's ColorVisionSimulation), each then taken to greyscale. Machado's three are gated at
# 16 px on every ground (weakest pair at least 11). On Ishgard Snow, Ready's salience is measured in OKLab with
# lightness down-weighted (or chroma only) and in plain luminance, with Ready over the palette's warm wash, from each
# state drawn on black and white mattes (G2L, at 16 and 20 px).
#
# Usage:
#   python tools/themes/build_themes.py                 build every set, run the gates, write sheets to the temp folder
#   python tools/themes/build_themes.py --set ishgard-glass
#   python tools/themes/build_themes.py --check         rebuild into a temp folder and diff against the repo (exit 1)
#   python tools/themes/build_themes.py --out DIR       contact sheets and the report go to DIR
import argparse
import base64
import hashlib
import importlib.util
import itertools
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SETS_DIR = os.path.join(HERE, "sets")
sys.path.insert(0, os.path.join(REPO, "docs", "design", "moon-road", "banners"))
from rasterize import chrome, screenshot  # noqa: E402

# ---------------------------------------------------------------- the atlas contract (MedalLayout, medals.json)
PAD = 2
WIDTH, HEIGHT = 782, 574
TIERS = [48, 64, 96, 128]
SPRITES = ["ready", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked",
           "other-job-tank", "other-job-healer", "other-job-dps", "other-job-hand"]
ROW_SIZES = list(range(12, 32))

# ---------------------------------------------------------------- the states the gates measure
STATES = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out",
          "not-checked"]
SHORT = dict(zip(STATES, ["Rdy", "RoJ", "Jrn", "Blk", "Done", "Comp", "Lock", "NotC"]))
CELL = 40
SIZES = [16, 20]
NIGHT = "#0F1424"
# Informational grounds (section 8.2's windows, and render_sheet.py's daylight swatch). The gates run on Night.
GROUNDS = {"night": NIGHT, "ishgard-snow": "#EEF1F6", "daylight": "#E9E4D6"}
BARS = {"weakest16": 12.0, "weakest20": 16.0, "readyLead": 1.3, "completedOfReady": 0.8, "salienceMin": 15.0,
        "cvdWeakest16": 11.0, "lightReadyLead": 1.3, "lightLumaFloor": 0.70,
        "mixClose": 12.0, "mixHard": 10.0, "mixReadyLead": 1.25}
GATE_MODES = ["grey", "deut"]        # the round 5 gate: greyscale and Vienot deuteranopia
ALL_MODES = ["grey", "deut", "machado-deut", "machado-prot", "machado-trit"]
# The colour-vision gate (the realism supervisor's ruling for 1.16.0): Machado 2009 protanopia, deuteranopia and
# tritanopia, weakest pair at 16 px, on every ground (theme-system.md section 7.1: Night, the paired palette, daylight).
CVD_MODES = ["machado-prot", "machado-deut", "machado-trit"]
# Light-palette Ready salience: on Ishgard Snow a Ready row's glow becomes a warm wash (spec-1.16.md A4). Every set
# ships the default wash (the realism supervisor's ruling, spec-1.16 A4.1); the old .90 / 4 px fallback is still
# measured and recorded, never chosen.
LIGHT_GROUND = "ishgard-snow"
WASHES = {"default": {"color": "#F2D27A", "alpha": 0.75, "radiusPx": 3},
          "fallback": {"color": "#F2D27A", "alpha": 0.90, "radiusPx": 4}}
LIGHT_WASH = "default"
# G2L's measures, per pixel against the window, summed over the cell. Full OKLab difference is mostly lightness, so on a
# light page every dark-faced state outweighs Ready's light face; the ruling down-weights lightness (Ready must lead by
# 1.3 under "weighted", or failing that under "chroma"), and keeps a floor on plain luminance (round 5's greyscale
# salience) so Ready never reads as washed out.
LIGHT_MEASURES = ["weighted", "chroma"]
# Black and white mattes (not grounds): each state drawn on both recovers its colour and alpha, so the build can lay
# it over the wash exactly as the plugin will.
MATTES = {"matte-black": "#000000", "matte-white": "#FFFFFF"}


# ================================================================ manifests and sources

def load_manifest(key):
    with open(os.path.join(SETS_DIR, f"{key}.json"), encoding="utf-8") as fh:
        m = json.load(fh)
    assert m["key"] == key, f"{key}.json names itself {m['key']}"
    assert list(m["sprites"]) == SPRITES, f"{key}: sprites must be exactly {SPRITES}, in that order"
    assert sorted(int(t) for t in m["tiers"]) == TIERS, f"{key}: tiers must be {TIERS}"
    return m


def all_manifests():
    keys = sorted(n[:-5] for n in os.listdir(SETS_DIR) if n.endswith(".json"))
    return [load_manifest(k) for k in keys]


def path_of(m, rel, tier_dir="."):
    """A manifest path: relative to the set's root, or to the repo when it starts with '/'. '{tier}' is the tier's
    folder (for example Ishgard Glass's '_mid' at 48 and 64 px)."""
    rel = rel.replace("{tier}", tier_dir)
    if rel.startswith("/"):
        return os.path.normpath(os.path.join(REPO, rel[1:]))
    return os.path.normpath(os.path.join(REPO, m["root"], rel))


def read_text(path):
    # Text mode on purpose (universal newlines), as gen_atlas.py reads: Medallion's atlas stays byte-identical.
    with open(path, encoding="utf-8") as fh:
        return fh.read()


_MODULES = {}


def python_source(spec):
    """{'python': 'repo/path.py', 'call': 'fn', 'args': [...]}: an SVG string from a generator function (Medallion's
    empty-seat medals come from gen_atlas.py's own empty_seat_medal, so its frame is the approved one to the unit)."""
    path = os.path.join(REPO, spec["python"])
    mod = _MODULES.get(path)
    if mod is None:
        name = "themes_src_" + re.sub(r"\W", "_", os.path.relpath(path, REPO))
        sp = importlib.util.spec_from_file_location(name, path)
        mod = importlib.util.module_from_spec(sp)
        sp.loader.exec_module(mod)
        _MODULES[path] = mod
    return getattr(mod, spec["call"])(*spec.get("args", []))


def layer_text(m, layer, tier_dir):
    if isinstance(layer, str):
        return read_text(path_of(m, layer, tier_dir))
    text = read_text(path_of(m, layer["file"], tier_dir))
    for old, new in layer.get("recolour", {}).items():
        assert old in text, f"{layer['file']}: recolour source {old} not found"
        text = text.replace(old, new)
    return text


def texts_for(m, spec, tier):
    """The SVG text(s) one sprite draws at one tier: a single master, or layers stacked bottom to top."""
    tier_dir = m["tiers"][str(tier)]
    if isinstance(spec, dict) and str(tier) in spec:
        spec = spec[str(tier)]
    if isinstance(spec, str):
        return [read_text(path_of(m, spec, tier_dir))]
    if "python" in spec:
        return [python_source(spec)]
    return [layer_text(m, layer, tier_dir) for layer in spec["layers"]]


def inner(svg_text, prefix):
    """The body of one SVG (between <svg> and </svg>, minus its title) with every id prefixed (gen_atlas.py's)."""
    mt = re.search(r"<svg[^>]*viewBox=\"([^\"]+)\"[^>]*>(.*)</svg>", svg_text, re.S)
    view, body = mt.group(1), mt.group(2)
    body = re.sub(r"<title>.*?</title>", "", body, flags=re.S)
    body = re.sub(r'id="([^"]+)"', lambda k: f'id="{prefix}{k.group(1)}"', body)
    body = re.sub(r"url\(#([^)]+)\)", lambda k: f"url(#{prefix}{k.group(1)})", body)
    body = re.sub(r'href="#([^"]+)"', lambda k: f'href="#{prefix}{k.group(1)}"', body)
    return view, body


def body_of(texts, prefix):
    """(viewBox, body) for one sprite. A single master keeps gen_atlas.py's prefix exactly; layers get their own."""
    if len(texts) == 1:
        return inner(texts[0], prefix)
    views, bodies = zip(*(inner(t, f"{prefix}l{j}_") for j, t in enumerate(texts)))
    assert len(set(views)) == 1, f"layers disagree on the viewBox: {views}"
    return views[0], "".join(bodies)


def standalone(texts):
    """One self-contained SVG for a sprite (for the metric renders, which draw each state as its own <img>)."""
    if len(texts) == 1:
        return texts[0]
    view, body = body_of(texts, "x")
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{view}" width="128" height="128">{body}</svg>'


# ================================================================ the hero atlas (gen_atlas.py's, generalised)

def hero_layout():
    """name -> {tier: (x, y, w, h)}: each tier is a band of rows, as many sprites to a row as the width holds."""
    rects = {name: {} for name in SPRITES}
    y = PAD
    for cell in TIERS:
        per_row = min(len(SPRITES), (WIDTH - PAD) // (cell + PAD))
        for i, name in enumerate(SPRITES):
            rects[name][cell] = (PAD + (i % per_row) * (cell + PAD), y + (i // per_row) * (cell + PAD), cell, cell)
        y += -(-len(SPRITES) // per_row) * (cell + PAD)
    assert y <= HEIGHT, f"layout needs {y} px, atlas is {HEIGHT}"
    return rects


def hero_sheet(scale, rects, bodies):
    parts = []
    for i, name in enumerate(SPRITES):
        for t, cell in enumerate(TIERS):
            view, body = bodies[name][cell]
            x, y, w, h = rects[name][cell]
            # Each copy gets its own id prefix: a filter or clip shared across nested <svg>s would resolve to the first.
            body_t = re.sub(r'(id="|url\(#|href="#)m{0}_'.format(i), lambda k: f"{k.group(1)}m{i}t{t}_", body)
            parts.append(f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="{view}" overflow="hidden">{body_t}</svg>')
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH * scale}" height="{HEIGHT * scale}" '
            f'viewBox="0 0 {WIDTH} {HEIGHT}">{"".join(parts)}</svg>')


def hero_json(rects):
    return {
        "size": [WIDTH, HEIGHT],
        "tiers": TIERS,
        "note": "Rectangles are [x, y, w, h] in 1x pixels, per tier; medals@2x.png is the same layout at twice the size.",
        "sprites": {name: {str(cell): list(r) for cell, r in by_tier.items()} for name, by_tier in rects.items()},
    }


# ================================================================ the row strip

def row_layout():
    """state -> {size: (x, y, w, h)}: one shelf per state, sizes 12..31 left to right, 2 px apart."""
    width = PAD + sum(s + PAD for s in ROW_SIZES)
    rects = {}
    for i, state in enumerate(STATES):
        y = PAD + i * (ROW_SIZES[-1] + PAD)
        x = PAD
        rects[state] = {}
        for s in ROW_SIZES:
            rects[state][s] = (x, y, s, s)
            x += s + PAD
    return rects, (width, PAD + len(STATES) * (ROW_SIZES[-1] + PAD))


def row_sheet(size, rects, bodies):
    parts = []
    for i, state in enumerate(STATES):
        view, body = bodies[state]
        for s in ROW_SIZES:
            x, y, w, h = rects[state][s]
            body_s = re.sub(r'(id="|url\(#|href="#)r{0}_'.format(i), lambda k: f"{k.group(1)}r{i}s{s}_", body)
            parts.append(f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="{view}" overflow="hidden">{body_s}</svg>')
    w, h = size
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{"".join(parts)}</svg>'


def row_json(rects, size):
    return {
        "size": list(size),
        "sizes": ROW_SIZES,
        "note": "Row-tier faces, framed, without badges: each state rendered at every whole device pixel size; "
                "rectangles are [x, y, w, h] in device pixels. 1x only (the sizes are device pixels already).",
        "sprites": {state: {str(s): list(r) for s, r in by_size.items()} for state, by_size in rects.items()},
    }


def render_svg(svg_text, tmp, name, width, height):
    src = os.path.join(tmp, f"{name}.svg")
    with open(src, "w", encoding="utf-8") as fh:
        fh.write(svg_text)
    return screenshot(src, os.path.join(tmp, f"{name}.raw.png"), width, height, transparent=True).convert("RGBA")


def write_png(im, path):
    im.save(path, optimize=True)


def compact_leaves(text):
    """Puts every object or array that holds no other container on one line (metrics.json stays a few dozen KB)."""
    leaf = re.compile(r"([\[{])\n\s*([^\[\]{}]*?)\n\s*([\]}])")
    return leaf.sub(lambda k: k.group(1) + re.sub(r",\n\s*", ", ", k.group(2)) + k.group(3), text)


def write_json(data, path, compact=False):
    if os.path.exists(path):
        # Leave an unchanged file alone: git may have checked it out with CRLF, and rewriting would only churn it.
        with open(path, encoding="utf-8") as fh:
            try:
                if json.load(fh) == json.loads(json.dumps(data)):
                    return
            except ValueError:
                pass
    text = json.dumps(data, indent=2)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write((compact_leaves(text) if compact else text) + "\n")


def build_atlases(m, dest_atlas, dest_set, tmp):
    """Renders and writes one set's hero atlas (1x and 2x) and, when the set has one, its row strip. Returns the
    images for the fit check and the contact sheets."""
    rects = hero_layout()
    bodies = {name: {cell: body_of(texts_for(m, m["sprites"][name], cell), f"m{i}_") for cell in TIERS}
              for i, name in enumerate(SPRITES)}
    os.makedirs(dest_atlas, exist_ok=True)
    out = {}
    for scale, suffix in ((1, ""), (2, "@2x")):
        im = render_svg(hero_sheet(scale, rects, bodies), tmp, f"{m['key']}-medals{suffix}", WIDTH * scale, HEIGHT * scale)
        write_png(im, os.path.join(dest_atlas, f"medals{suffix}.png"))
        out[f"medals{suffix}"] = im
    write_json(hero_json(rects), os.path.join(dest_atlas, "medals.json"))

    row = m.get("row")
    if row and row.get("atlas"):
        rrects, rsize = row_layout()
        rbodies = {state: body_of([layer_text(m, row["sprites"][state], row["dir"])], f"r{i}_")
                   for i, state in enumerate(STATES)}
        os.makedirs(dest_set, exist_ok=True)
        im = render_svg(row_sheet(rsize, rrects, rbodies), tmp, f"{m['key']}-row", *rsize)
        write_png(im, os.path.join(dest_set, "row.png"))
        write_json(row_json(rrects, rsize), os.path.join(dest_set, "row.json"))
        out["row"] = im
    return out


# ================================================================ metrics (round 5's metrics.py, generalised)

def lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def unlin(c):
    return np.where(c <= 0.0031308, 12.92 * c, 1.055 * np.clip(c, 0, None) ** (1 / 2.4) - 0.055)


def luma(rgb):
    l = lin(rgb)
    return unlin(0.2126 * l[..., 0] + 0.7152 * l[..., 1] + 0.0722 * l[..., 2])


VIENOT_DEUT = np.array([[0.29275, 0.70725, 0], [0.29275, 0.70725, 0], [-0.02234, 0.02234, 1]])
MACHADO = {
    "machado-deut": np.array([[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]]),
    "machado-prot": np.array([[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]]),
    "machado-trit": np.array([[1.255528, -0.076749, -0.178779], [-0.078411, 0.930809, 0.147602], [0.004733, 0.691367, 0.303900]]),
}


def simulate(rgb, matrix):
    return unlin(np.clip(lin(rgb) @ matrix.T, 0, 1))


MODE_FN = {
    "grey": luma,
    "deut": lambda x: luma(simulate(x, VIENOT_DEUT)),
    **{k: (lambda mat: (lambda x: luma(simulate(x, mat))))(v) for k, v in MACHADO.items()},
}


def blur(a):
    return np.asarray(Image.fromarray((a * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(0.6)), dtype=float) / 255


def metric_strip(svgs, rows, tmp, name):
    """Chrome draws each SVG as an <img> at each (ground, size) row, one 40 px cell per SVG, as metrics.py does.
    Returns {(ground, size): [cell RGB arrays]}."""
    n = len(svgs)
    uris = [base64.b64encode(s.encode("utf-8")).decode() for s in svgs]
    html = []
    for r, (ground, size) in enumerate(rows):
        imgs = "".join(
            f"<div style='position:absolute;left:{i * CELL}px;top:0;width:{CELL}px;height:{CELL}px;display:flex;"
            f"align-items:center;justify-content:center'><img src='data:image/svg+xml;base64,{u}' width={size} height={size}></div>"
            for i, u in enumerate(uris))
        html.append(f"<div style='position:absolute;left:0;top:{r * CELL}px;width:{n * CELL}px;height:{CELL}px;"
                    f"background:{GROUNDS.get(ground) or MATTES[ground]}'>{imgs}</div>")
    page = os.path.join(tmp, f"{name}.html")
    with open(page, "w", encoding="utf-8") as fh:
        fh.write(f"<html><body style='margin:0;background:{NIGHT}'>{''.join(html)}</body></html>")
    png = os.path.join(tmp, f"{name}.png")
    subprocess.run([chrome(), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                    f"--screenshot={png}", f"--window-size={n * CELL},{len(rows) * CELL}", "file:///" + page.replace("\\", "/")],
                   check=True, capture_output=True)
    img = np.asarray(Image.open(png).convert("RGB"), dtype=float) / 255
    return {row: [img[r * CELL:(r + 1) * CELL, i * CELL:(i + 1) * CELL] for i in range(n)] for r, row in enumerate(rows)}


def metric_sources(m, tier):
    """{state: [svg texts]} for one hero tier (with badges; Ready on another job once per job composite), or for the
    row tier (tier None)."""
    if tier is None:
        row = m["row"]
        return {s: [layer_text(m, row["sprites"][s], row["dir"])] for s in STATES}
    out = {}
    for state in STATES:
        if state == "ready-on-another-job":
            out[state] = [read_text(path_of(m, p, m["tiers"][str(tier)])) for p in m["metrics"]["ready-on-another-job"]]
        else:
            out[state] = [standalone(texts_for(m, m["sprites"][state], tier))]
    return out


def tier_groups(m):
    """The hero tiers grouped by identical sources (each group is measured once), then the row tier."""
    groups = []
    for tier in TIERS:
        src = metric_sources(m, tier)
        digest = hashlib.sha256(json.dumps(src, sort_keys=True).encode()).hexdigest()
        for g in groups:
            if g["digest"] == digest:
                g["tiers"].append(tier)
                break
        else:
            groups.append({"digest": digest, "tiers": [tier], "sources": src})
    for g in groups:
        g["name"] = "hero-" + "-".join(str(t) for t in g["tiers"])
    groups.append({"name": "row", "tiers": [], "sources": metric_sources(m, None),
                   "digest": None})
    return groups


def measure(cells_by_state, ground_rgb, mode):
    """Pairwise distinctness (worst over Ready on another job's job variants) and salience (the loudest variant)."""
    f = MODE_FN[mode]
    lum = {s: [f(c) for c in cs] for s, cs in cells_by_state.items()}
    bg = lum[STATES[0]][0][0, 0]
    blurred = {s: [blur(x) for x in xs] for s, xs in lum.items()}
    pairs = {}
    for a, b in itertools.combinations(STATES, 2):
        pairs[(a, b)] = min(np.abs(x - y).sum() for x in blurred[a] for y in blurred[b])
    sal = {s: max(float(np.abs(x - bg).sum()) for x in xs) for s, xs in lum.items()}
    return pairs, sal


def cells_for(strip, sources, ground, size):
    cells, i = {}, 0
    row = strip[(ground, size)]
    for s in STATES:
        cells[s] = row[i:i + len(sources[s])]
        i += len(sources[s])
    return cells


def r1(v):
    return round(float(v), 2)


def measure_set(m, tmp):
    """Renders and measures every tier group of one set on every ground. Returns the groups with their cells."""
    groups = tier_groups(m)
    rows = [(g, s) for g in [*GROUNDS, *MATTES] for s in SIZES]
    for g in groups:
        flat = [svg for s in STATES for svg in g["sources"][s]]
        g["strip"] = metric_strip(flat, rows, tmp, f"metrics-{m['key']}-{g['name']}")
        g["measures"] = []
        for ground in GROUNDS:
            for size in SIZES:
                cells = cells_for(g["strip"], g["sources"], ground, size)
                for mode in ALL_MODES:
                    pairs, sal = measure(cells, None, mode)
                    g["measures"].append({"ground": ground, "px": size, "mode": mode, "pairs": pairs, "salience": sal})
    return groups


# ---------------------------------------------------------------- light-palette Ready salience (OKLab, with the wash)

OKLAB_M1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929], [0.2119034982, 0.6806995451, 0.1073969566],
                     [0.0883024619, 0.2817188376, 0.6299787005]])
OKLAB_M2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468], [1.9779984951, -2.4285922050, 0.4505937099],
                     [0.0259040371, 0.7827717662, -0.8086757660]])


def hex_rgb(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], dtype=float) / 255


def oklab(rgb):
    return np.cbrt(lin(np.clip(rgb, 0, 1)) @ OKLAB_M1.T) @ OKLAB_M2.T


def unmatte(black, white):
    """A cell drawn on black and on white -> (premultiplied colour, alpha): on black C = P, on white C = P + (1 - a)."""
    alpha = np.clip(1 - (white - black).mean(axis=-1), 0, 1)
    return black, alpha


def wash_cover(alpha, radius):
    """The wash's coverage: everything within `radius` px of the medal (alpha at least .5), with a 1 px soft edge."""
    ys, xs = np.nonzero(alpha >= 0.5)
    if not len(ys):
        return np.zeros_like(alpha)
    gy, gx = np.mgrid[0:alpha.shape[0], 0:alpha.shape[1]]
    dist = np.sqrt((gy[..., None] - ys) ** 2 + (gx[..., None] - xs) ** 2).min(axis=-1)
    return np.clip(radius + 0.5 - dist, 0, 1)


LIGHT_DELTA = {
    # sqrt((dL/3)^2 + da^2 + db^2): OKLab difference with lightness down-weighted three times
    "weighted": lambda d: np.sqrt((d[..., 0] / 3) ** 2 + d[..., 1] ** 2 + d[..., 2] ** 2),
    # sqrt(da^2 + db^2): chroma only
    "chroma": lambda d: np.sqrt(d[..., 1] ** 2 + d[..., 2] ** 2),
}


def light_salience(g, wash, size):
    """Each state's salience on the light window, Ready drawn over the wash (None: no wash): per measure, the summed
    per-pixel difference of its cell from the window ('weighted' and 'chroma' in OKLab, 'luma' round 5's greyscale
    salience). Blending is in sRGB values, as Chrome and ImGui blend. Ready on another job counts its loudest job, as on
    Night. Returns {measure: {state: salience}}."""
    window = hex_rgb(GROUNDS[LIGHT_GROUND])
    window_lab = oklab(window)
    window_luma = luma(window)
    black = cells_for(g["strip"], g["sources"], "matte-black", size)
    white = cells_for(g["strip"], g["sources"], "matte-white", size)
    plain = cells_for(g["strip"], g["sources"], LIGHT_GROUND, size)
    sal = {name: {} for name in [*LIGHT_MEASURES, "luma"]}
    for s in STATES:
        vals = {name: [] for name in sal}
        for b, w, p in zip(black[s], white[s], plain[s]):
            pre, a = unmatte(b, w)
            ground = np.broadcast_to(window, b.shape)
            # The unmatte must reproduce Chrome's own render on the window, or the wash composite below means nothing.
            # Three 8-bit renders leave a few levels of rounding on the odd edge pixel (4.3 at worst, 0.06 on average).
            err = np.abs(pre + (1 - a)[..., None] * ground - p)
            assert err.max() <= 6 / 255 and err.mean() <= 0.25 / 255, \
                f"{g['name']} {s} {size} px: unmatte is off by {err.max() * 255:.1f}/255 (mean {err.mean() * 255:.2f})"
            if s == "ready" and wash:
                cover = (wash_cover(a, wash["radiusPx"]) * wash["alpha"])[..., None]
                ground = ground * (1 - cover) + hex_rgb(wash["color"]) * cover
            c = pre + (1 - a)[..., None] * ground
            d = oklab(c) - window_lab
            for name in LIGHT_MEASURES:
                vals[name].append(float(LIGHT_DELTA[name](d).sum()))
            vals["luma"].append(float(np.abs(luma(c) - window_luma).sum()))
        for name, v in vals.items():
            sal[name][s] = max(v)
    return sal


def light_lead(sal):
    nxt_state, nxt = max(((s, v) for s, v in sal.items() if s != "ready"), key=lambda kv: kv[1])
    return sal["ready"] / nxt, nxt_state, nxt


def light_measures(groups):
    """G2L, the realism supervisor's ruling (spec-1.16 A4.1), on the row tier at 16 and 20 px with the default wash:
    Ready leads the next state by 1.3 under the weighted measure at both sizes, or failing that under chroma only (the
    record names the measure that passed), and Ready's plain luminance salience is at least .70 of the next state's.
    No wash and the old fallback are recorded for reviewers. Returns (gate rows, the record for metrics.json)."""
    row = next(g for g in groups if g["name"] == "row")
    variants = {}
    for name, wash in [("none", None), *WASHES.items()]:
        variants[name] = {}
        for size in SIZES:
            entry = {}
            for measure, sal in light_salience(row, wash, size).items():
                lead, nxt_state, _ = light_lead(sal)
                entry[measure] = {"salience": {s: r1(v) for s, v in sal.items()}, "readyLead": r1(lead), "next": nxt_state}
            variants[name][str(size)] = entry

    used = variants[LIGHT_WASH]
    wash = WASHES[LIGHT_WASH]
    # Ratios are judged at two decimals, as on Night.
    measure = next((x for x in LIGHT_MEASURES if all(used[str(s)][x]["readyLead"] >= BARS["lightReadyLead"] for s in SIZES)),
                   LIGHT_MEASURES[-1])
    gates = []
    for size in SIZES:
        for key, gate, bar in [(measure, f"G2L Ready lead {size} px ({measure})", BARS["lightReadyLead"]),
                               ("luma", f"G2L lightness floor {size} px", BARS["lightLumaFloor"])]:
            v = used[str(size)][key]
            nxt = v["next"]
            gates.append({"gate": f"{gate} on Ishgard Snow", "tier": "row", "value": v["readyLead"], "bar": bar,
                          "detail": f"wash {wash['alpha']:.2f} within {wash['radiusPx']} px: Rdy {v['salience']['ready']:.0f} / "
                                    f"{SHORT[nxt]} {v['salience'][nxt]:.0f}",
                          "pass": v["readyLead"] >= bar})
    record = {"ground": LIGHT_GROUND, "window": GROUNDS[LIGHT_GROUND], "tier": "row", "gatePx": SIZES,
              "measure": measure, "use": LIGHT_WASH, "washes": WASHES, "variants": variants}
    return gates, record


def at_bar(v):
    """Distinctness and salience are judged at one decimal, the precision round 5 set its bars at (its shipped row
    tier holds Blocked vs Locked out at 12.0, which is 11.96 unrounded). Ratios are judged at two decimals."""
    return round(float(v), 1)


def is_gated(meas):
    return (meas["ground"] == "night" and meas["mode"] in GATE_MODES) or is_cvd_gate(meas)


def is_cvd_gate(meas):
    return meas["px"] == 16 and meas["mode"] in CVD_MODES


def gate_rows(m, groups):
    """Section 7.1's per-set gates, per tier group: G1 on Night (weakest pair at 16 and 20 px, greyscale and Vienot
    deuteranopia), G1c on every ground (weakest pair at 16 px under Machado protanopia, deuteranopia and tritanopia),
    and G2 on Night (Ready's lead, Completed's recession, every state but Not checked visible)."""
    rows = []
    for g in groups:
        for meas in g["measures"]:
            if is_cvd_gate(meas):
                (a, b), v = min(meas["pairs"].items(), key=lambda kv: kv[1])
                rows.append({"gate": f"G1c weakest pair 16 px {meas['mode']} on {meas['ground']}", "tier": g["name"],
                             "value": r1(v), "bar": BARS["cvdWeakest16"], "detail": f"{SHORT[a]}-{SHORT[b]}",
                             "pass": at_bar(v) >= BARS["cvdWeakest16"]})
                continue
            if meas["ground"] != "night" or meas["mode"] not in GATE_MODES:
                continue
            (a, b), v = min(meas["pairs"].items(), key=lambda kv: kv[1])
            bar = BARS["weakest16"] if meas["px"] == 16 else BARS["weakest20"]
            rows.append({"gate": f"G1 weakest pair {meas['px']} px {meas['mode']}", "tier": g["name"],
                         "value": r1(v), "bar": bar, "detail": f"{SHORT[a]}-{SHORT[b]}", "pass": at_bar(v) >= bar})
            if meas["px"] == 16 and meas["mode"] == "grey":
                sal = meas["salience"]
                nxt_state, nxt = max(((s, v2) for s, v2 in sal.items() if s != "ready"), key=lambda kv: kv[1])
                lead = sal["ready"] / nxt
                rows.append({"gate": "G2 Ready lead", "tier": g["name"], "value": r1(lead), "bar": BARS["readyLead"],
                             "detail": f"Rdy {sal['ready']:.0f} / {SHORT[nxt_state]} {nxt:.0f}", "pass": round(lead, 2) >= BARS["readyLead"]})
                comp = sal["completed"] / sal["ready"]
                rows.append({"gate": "G2 Completed recedes", "tier": g["name"], "value": r1(comp),
                             "bar": BARS["completedOfReady"], "detail": f"Comp {sal['completed']:.0f} / Rdy {sal['ready']:.0f}",
                             "pass": round(comp, 2) <= BARS["completedOfReady"]})
                low_state, low = min(((s, v2) for s, v2 in sal.items() if s != "not-checked"), key=lambda kv: kv[1])
                rows.append({"gate": "G2 every state visible", "tier": g["name"], "value": r1(low), "bar": BARS["salienceMin"],
                             "detail": SHORT[low_state], "pass": at_bar(low) >= BARS["salienceMin"]})
    return rows


def fit_rows(atlas):
    """Fit (section 7.1): nothing bleeds outside the cells (alpha 0 in every gap, so bilinear sampling never pulls a
    neighbour in), and no sprite is cut by its cell (no fully opaque pixel on a cell's outermost ring). Section 7.1's
    "1 px inside" cannot hold for the shared silhouette (r 63.2 in the 128 box, 0.3 px from the edge at 48 px, and
    In journal's ribbon overhangs it), Medallion's shipped atlas included; this is the check that does hold."""
    rows = []
    sets = [("medals", 1, hero_layout()), ("medals@2x", 2, hero_layout())]
    if "row" in atlas:
        sets.append(("row", 1, row_layout()[0]))
    for key, scale, rects in sets:
        a = np.asarray(atlas[key], dtype=np.uint8)[..., 3]
        inside = np.zeros_like(a, dtype=bool)
        worst = (-1, None, None)
        for name, by in rects.items():
            for cell, (x, y, w, h) in by.items():
                c = a[y * scale:(y + h) * scale, x * scale:(x + w) * scale]
                inside[y * scale:(y + h) * scale, x * scale:(x + w) * scale] = True
                ring = int(max(c[0].max(), c[-1].max(), c[:, 0].max(), c[:, -1].max()))
                if ring > worst[0]:
                    worst = (ring, name, cell)
        bleed = int(a[~inside].max()) if (~inside).any() else 0
        rows.append({"gate": f"Fit {key}: no bleed", "tier": "row" if key == "row" else "hero", "value": bleed, "bar": 0,
                     "detail": "max alpha outside every cell", "pass": bleed == 0})
        rows.append({"gate": f"Fit {key}: not cut", "tier": "row" if key == "row" else "hero", "value": worst[0], "bar": 254,
                     "detail": f"max alpha on a cell edge: {worst[1]} at {worst[2]}", "pass": worst[0] <= 254})
    return rows


# ================================================================ the cross-set similarity table (section 5.2)

def cross_table(measured):
    """For every ordered pair of sets (A, B), A != B, and every pair of different states (s from A, t from B): the
    distinctness of the two side by side, worst over every vision mode, on Night at 16 and 20 px, for the row tier and
    the smallest hero tier group. Faces are compared as framed in their own kit (the composites the sets ship)."""
    keys = list(measured)
    out = {}
    for a_key, b_key in itertools.permutations(keys, 2):
        for which in ("row", "hero"):
            ga = measured[a_key][which]
            gb = measured[b_key][which]
            for size in SIZES:
                worst = {}
                for mode in ALL_MODES:
                    f = MODE_FN[mode]
                    ca = cells_for(ga["strip"], ga["sources"], "night", size)
                    cb = cells_for(gb["strip"], gb["sources"], "night", size)
                    la = {s: [blur(f(c)) for c in cs] for s, cs in ca.items()}
                    lb = {s: [blur(f(c)) for c in cs] for s, cs in cb.items()}
                    for s in STATES:
                        for t in STATES:
                            if s == t:
                                continue
                            v = min(np.abs(x - y).sum() for x in la[s] for y in lb[t])
                            if (s, t) not in worst or v < worst[(s, t)][0]:
                                worst[(s, t)] = (v, mode)
                out.setdefault(a_key, {}).setdefault(b_key, {}).setdefault(which, {})[str(size)] = [
                    {"a": s, "b": t, "d": r1(v), "mode": mode} for (s, t), (v, mode) in worst.items()]
    return out


def cross_salience(measured):
    """A mix's Ready from set A against the other states from set B (16 px, greyscale, Night)."""
    out = []
    for a_key, b_key in itertools.permutations(list(measured), 2):
        for which in ("row", "hero"):
            sa = next(x for x in measured[a_key][which]["measures"] if x["ground"] == "night" and x["px"] == 16 and x["mode"] == "grey")["salience"]
            sb = next(x for x in measured[b_key][which]["measures"] if x["ground"] == "night" and x["px"] == 16 and x["mode"] == "grey")["salience"]
            nxt_state, nxt = max(((s, v) for s, v in sb.items() if s != "ready"), key=lambda kv: kv[1])
            out.append({"ready": a_key, "others": b_key, "tier": which, "lead": r1(sa["ready"] / nxt),
                        "next": nxt_state, "completedOfReady": r1(sb["completed"] / sa["ready"])})
    return out


# ================================================================ metrics.json

def chrome_version():
    try:
        if os.name == "nt":
            ps = f"(Get-Item '{chrome()}').VersionInfo.ProductVersion"
            return subprocess.run(["powershell", "-NoProfile", "-Command", ps], capture_output=True, text=True).stdout.strip()
        return subprocess.run([chrome(), "--version"], capture_output=True, text=True).stdout.strip()
    except OSError:
        return "unknown"


def pair_key(a, b):
    return f"{a}|{b}"


def summary(x):
    (a, b), v = min(x["pairs"].items(), key=lambda kv: kv[1])
    sal = x["salience"]
    nxt = max(v2 for s2, v2 in sal.items() if s2 != "ready")
    return {"ground": x["ground"], "px": x["px"], "mode": x["mode"], "weakest": pair_key(a, b), "d": r1(v),
            "readyLead": r1(sal["ready"] / nxt), "completedOfReady": r1(sal["completed"] / sal["ready"])}


def metrics_json(m, groups, gates, light, cross, mix_sal, chrome_ver):
    """The committed record: the full pair and salience tables behind the gates (Night, greyscale and Vienot
    deuteranopia, 16 and 20 px; every ground, Machado's three, 16 px), a one-line summary for every other ground,
    mode and size, the light-palette salience and the Ready wash the plugin must draw, and this set's half of the
    cross-set table."""
    tiers = []
    for g in groups:
        gate_measures = [x for x in g["measures"] if is_gated(x)]
        survey = [summary(x) for x in g["measures"] if not is_gated(x)]
        tiers.append({
            "name": g["name"],
            "atlasTiers": g["tiers"],
            "measures": [{
                "ground": x["ground"], "px": x["px"], "mode": x["mode"],
                "pairs": {pair_key(a, b): r1(v) for (a, b), v in x["pairs"].items()},
                "salience": {s2: r1(v) for s2, v in x["salience"].items()},
            } for x in gate_measures],
            "survey": survey,
        })
    sets = {other: {which: {size: {pair_key(x["a"], x["b"]): x["d"] for x in rows} for size, rows in by_size.items()}
                    for which, by_size in by.items()}
            for other, by in cross.get(m["key"], {}).items()}
    return {
        "set": m["key"],
        "name": m["name"],
        "note": "Written by tools/themes/build_themes.py; see tools/themes/README.md. Distinctness and salience are "
                "round 5's metrics.py units (summed blurred luminance difference) in a 40 px cell. Gates run on the "
                "Night window in greyscale and deuteranopia (16 and 20 px), and on every ground under Machado "
                "protanopia, deuteranopia and tritanopia (16 px); 'survey' records the rest. 'light' is the row "
                "tier's salience on Ishgard Snow with Ready over its warm wash, per measure: 'weighted' sums "
                "sqrt((dL/3)^2 + da^2 + db^2) and 'chroma' sqrt(da^2 + db^2) in OKLab, 'luma' is round 5's greyscale "
                "salience; G2L gates the default wash at 16 and 20 px, and 'measure' names the lead measure that "
                "passed. 'readyWash' is the wash the plugin draws for this set.",
        "chrome": chrome_ver,
        "grounds": GROUNDS,
        "bars": BARS,
        "gateModes": GATE_MODES,
        "cvdModes": CVD_MODES,
        "readyWash": light["washes"][light["use"]],
        "pass": all(r["pass"] for r in gates),
        "gates": gates,
        "tiers": tiers,
        "light": light,
        "cross": {"note": "Pairs are 'this set's state|the other set's state', each pair of different states, worst "
                          "over every vision mode, on Night, for the row tier and the 48 px hero tier's sources, at "
                          "16 and 20 px. Each set is framed in its own kit (the sprites it ships). Under mixClose "
                          "reads 'close', under mixHard 'hard to tell apart' (section 5.2).",
                  "sets": sets,
                  "salience": [x for x in mix_sal if m["key"] in (x["ready"], x["others"])]},
    }


# ================================================================ contact sheets and the report (outside the repo)

def font():
    try:
        return ImageFont.truetype("segoeui.ttf", 12)
    except OSError:
        return ImageFont.load_default()


def contact_sheet(m, atlas, groups, path):
    """The hero tiers straight from the atlas (1x), the row strip, and the 16/20 px metric renders, on Night and on
    Ishgard Snow."""
    fnt = font()
    rects = hero_layout()
    ground_list = [("Night", NIGHT), ("Ishgard Snow", GROUNDS["ishgard-snow"])]
    col_w = len(SPRITES) * (128 + 8) + 16
    blocks_h = sum(t + 8 for t in TIERS) + 24
    row_h = (len(STATES) * 34 + 24) if "row" in atlas else 0
    metr_h = len(groups) * (len(SIZES) * CELL + 20)
    H = 30 + blocks_h + row_h + metr_h + 20
    sheet = Image.new("RGBA", (col_w * len(ground_list), H), (20, 24, 36, 255))
    d = ImageDraw.Draw(sheet)
    for gi, (label, hexc) in enumerate(ground_list):
        x0 = gi * col_w
        sheet.paste(Image.new("RGBA", (col_w - 8, H - 8), hexc), (x0 + 4, 4))
        ink = (230, 233, 242) if hexc == NIGHT else (26, 33, 54)
        d.text((x0 + 12, 10), f"{m['name']} on {label}", fill=ink, font=fnt)
        y = 30
        for tier in reversed(TIERS):
            for i, name in enumerate(SPRITES):
                x, yy, w, h = rects[name][tier]
                sprite = atlas["medals"].crop((x, yy, x + w, yy + h))
                sheet.alpha_composite(sprite, (x0 + 12 + i * (128 + 8), y))
            y += tier + 8
        y += 24
        if "row" in atlas:
            rrects, _ = row_layout()
            for i, state in enumerate(STATES):
                xx = x0 + 12
                for s in ROW_SIZES:
                    x, yy, w, h = rrects[state][s]
                    sheet.alpha_composite(atlas["row"].crop((x, yy, x + w, yy + h)), (xx, y + (31 - s) // 2))
                    xx += s + 6
                y += 34
            y += 24
        gkey = "night" if hexc == NIGHT else "ishgard-snow"
        for g in groups:
            d.text((x0 + 12, y), g["name"], fill=ink, font=fnt)
            y += 16
            for size in SIZES:
                cells = cells_for(g["strip"], g["sources"], gkey, size)
                xx = x0 + 12
                for s in STATES:
                    c = cells[s][0]
                    sheet.paste(Image.fromarray((c * 255).astype("uint8")).convert("RGBA"), (xx, y))
                    xx += CELL
                y += CELL
            y += 4
    sheet.convert("RGB").save(path)


def heat(v):
    if v < BARS["mixHard"]:
        return (196, 64, 72)
    if v < BARS["mixClose"]:
        return (214, 160, 60)
    return (58, 120, 86)


def cross_heatmap(cross, keys, path, which="row", size="16"):
    fnt = font()
    cell, gap = 34, 24
    grid = len(STATES) * cell
    n = len(keys)
    W = 120 + n * (grid + gap)
    H = 40 + n * (grid + gap + 20)
    im = Image.new("RGB", (W, H), (15, 20, 36))
    d = ImageDraw.Draw(im)
    d.text((10, 10), f"Cross-set distinctness, {which} tier, {size} px, worst vision mode (rows: set A's state; columns: set B's)", fill=(230, 233, 242), font=fnt)
    for ai, a in enumerate(keys):
        for bi, b in enumerate(keys):
            ox, oy = 120 + bi * (grid + gap), 40 + ai * (grid + gap + 20)
            d.text((ox, oy), f"{a} x {b}", fill=(200, 205, 220), font=fnt)
            if a == b:
                continue
            vals = {(x["a"], x["b"]): x["d"] for x in cross[a][b][which][size]}
            for i, s in enumerate(STATES):
                for j, t in enumerate(STATES):
                    x0, y0 = ox + j * cell, oy + 16 + i * cell
                    if s == t:
                        d.rectangle((x0, y0, x0 + cell - 2, y0 + cell - 2), fill=(40, 46, 64))
                        continue
                    v = vals[(s, t)]
                    d.rectangle((x0, y0, x0 + cell - 2, y0 + cell - 2), fill=heat(v))
                    d.text((x0 + 4, y0 + 10), f"{v:.0f}", fill=(255, 255, 255), font=fnt)
        d.text((10, 40 + ai * (grid + gap + 20) + 16 + grid // 2), a, fill=(230, 233, 242), font=fnt)
    im.save(path)


def report_text(results, mix_sal, cross):
    lines = []
    for key, r in results.items():
        lines.append(f"== {key}: {'PASS' if r['pass'] else 'FAIL'}")
        for g in r["gates"]:
            lines.append(f"  {'ok  ' if g['pass'] else 'FAIL'} {g['tier']:<14} {g['gate']:<32} {g['value']:>7} (bar {g['bar']}) {g['detail']}")
        light = r["light"]
        lines.append(f"  light salience, row tier on {light['ground']}, wash used: {light['use']}; G2L lead measure: "
                     f"{light['measure']}")
        for name, by_size in light["variants"].items():
            for size, by_measure in by_size.items():
                for measure, v in by_measure.items():
                    sal = "  ".join(f"{SHORT[s]} {v['salience'][s]:.0f}" for s in STATES)
                    lines.append(f"    {name:<8} {size} px {measure:<8} lead {v['readyLead']:<5} (next {SHORT[v['next']]})  {sal}")
        lines.append("")
    lines.append("== cross-set pairs under the mix bars (16 px, worst vision mode)")
    seen = False
    for key in results:
        for other, by in cross.get(key, {}).items():
            for which, by_size in by.items():
                for x in by_size["16"]:
                    if x["d"] < BARS["mixClose"]:
                        seen = True
                        tag = "hard" if x["d"] < BARS["mixHard"] else "close"
                        lines.append(f"  {tag:<5} {which:<4} {key}:{x['a']} beside {other}:{x['b']} = {x['d']} ({x['mode']})")
    if not seen:
        lines.append("  none")
    lines.append("")
    lines.append("== mixed Ready lead (Ready from A, the rest from B; 16 px grey, Night)")
    for x in mix_sal:
        flag = "" if x["lead"] >= BARS["mixReadyLead"] and x["completedOfReady"] <= BARS["completedOfReady"] else "  <- warns"
        lines.append(f"  {x['tier']:<4} Ready {x['ready']:<15} others {x['others']:<15} lead {x['lead']:<5} (next {SHORT[x['next']]}) Comp/Rdy {x['completedOfReady']}{flag}")
    return "\n".join(lines) + "\n"


# ================================================================ main

def destinations(m, root):
    dest_set = os.path.join(root, m["dest"])
    dest_atlas = os.path.join(root, m.get("atlasDest", m["dest"]))
    return dest_atlas, dest_set


def build(manifests, selected, root, out_dir, sheets=True):
    """Builds the selected sets into `root` (the repo, or a temp copy for --check). Every set is measured, so each
    metrics.json carries the whole cross-set table. Returns {key: metrics}, the mixed-salience rows and the table."""
    chrome_ver = chrome_version()
    results, measured, atlases, groups_by = {}, {}, {}, {}
    with tempfile.TemporaryDirectory() as tmp:
        for m in manifests:
            if m["key"] in selected:
                dest_atlas, dest_set = destinations(m, root)
                atlases[m["key"]] = build_atlases(m, dest_atlas, dest_set, tmp)
            groups = measure_set(m, tmp)
            groups_by[m["key"]] = groups
            measured[m["key"]] = {"row": next(g for g in groups if g["name"] == "row"),
                                  "hero": next(g for g in groups if 48 in g["tiers"])}
            print(f"{m['key']}: rendered {', '.join(atlases.get(m['key'], ['nothing']))}; "
                  f"measured {', '.join(g['name'] for g in groups)}")
        cross = cross_table(measured)
        mix_sal = cross_salience(measured)
        for m in (m for m in manifests if m["key"] in selected):
            groups = groups_by[m["key"]]
            light_gates, light = light_measures(groups)
            gates = gate_rows(m, groups) + light_gates + fit_rows(atlases[m["key"]])
            data = metrics_json(m, groups, gates, light, cross, mix_sal, chrome_ver)
            _, dest_set = destinations(m, root)
            os.makedirs(dest_set, exist_ok=True)
            write_json(data, os.path.join(dest_set, "metrics.json"), compact=True)
            results[m["key"]] = data
        if sheets and out_dir:
            os.makedirs(out_dir, exist_ok=True)
            for m in (m for m in manifests if m["key"] in selected):
                contact_sheet(m, atlases[m["key"]], groups_by[m["key"]], os.path.join(out_dir, f"{m['key']}-sheet.png"))
            if cross:
                keys = [m["key"] for m in manifests]
                for which in ("row", "hero"):
                    cross_heatmap(cross, keys, os.path.join(out_dir, f"cross-{which}-16.png"), which, "16")
            with open(os.path.join(out_dir, "report.txt"), "w", encoding="utf-8") as fh:
                fh.write(report_text(results, mix_sal, cross))
    return results, mix_sal, cross


def outputs(m):
    dest_atlas, dest_set = destinations(m, "")
    files = [os.path.join(dest_atlas, n) for n in ("medals.png", "medals@2x.png", "medals.json")]
    if m.get("row", {}).get("atlas"):
        files += [os.path.join(dest_set, n) for n in ("row.png", "row.json")]
    return files + [os.path.join(dest_set, "metrics.json")]


def main():
    ap = argparse.ArgumentParser(description="The multi-theme build: atlases, per-set gates and the cross-set table.")
    ap.add_argument("--set", action="append", help="write only this set (repeatable); every set is still measured for the cross table")
    ap.add_argument("--check", action="store_true", help="rebuild into a temp folder and diff against the repo")
    ap.add_argument("--out", default=os.path.join(tempfile.gettempdir(), "tsukimichi-themes"),
                    help="where contact sheets and report.txt go (never the repo)")
    ap.add_argument("--no-sheets", action="store_true")
    args = ap.parse_args()

    manifests = all_manifests()
    selected = set(args.set or [m["key"] for m in manifests])
    unknown = selected - {m["key"] for m in manifests}
    assert not unknown, f"no manifest for {sorted(unknown)}"

    if args.check:
        with tempfile.TemporaryDirectory() as root:
            results, _, _ = build(manifests, selected, root, None, sheets=False)
            stale = []
            for m in (m for m in manifests if m["key"] in selected):
                for rel in outputs(m):
                    new, old = os.path.join(root, rel), os.path.join(REPO, rel)
                    same = os.path.exists(old) and _same(new, old)
                    print(f"{'same ' if same else 'DIFF '} {rel.replace(os.sep, '/')}")
                    if not same:
                        stale.append(rel)
        failed = [k for k, r in results.items() if not r["pass"]]
        if failed:
            print(f"gates fail: {failed}")
        sys.exit(1 if stale or failed else 0)

    results, mix_sal, cross = build(manifests, selected, REPO, None if args.no_sheets else args.out, sheets=not args.no_sheets)
    print(report_text(results, mix_sal, cross))
    if not args.no_sheets:
        print(f"contact sheets and report: {args.out}")
    failed = [k for k, r in results.items() if not r["pass"]]
    if failed:
        sys.exit(f"gates fail: {failed}")


def _same(a, b):
    if a.endswith(".json"):
        # JSON compares by content: git may check text files out with CRLF.
        with open(a, encoding="utf-8") as fa, open(b, encoding="utf-8") as fb:
            return json.load(fa) == json.load(fb)
    with open(a, "rb") as fa, open(b, "rb") as fb:
        return fa.read() == fb.read()


if __name__ == "__main__":
    main()
