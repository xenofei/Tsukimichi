# The multi-theme build (feature plan v7 T4; docs/research/plan-v7/theme-system.md, sections 6.4 and 7.1).
#
# Reads one manifest per glyph set (tools/themes/sets/<set>.json), renders its approved SVG masters with headless Chrome
# (the same pipeline as docs/design/moon-v6/round5/gen_atlas.py, which stays as history), and writes per set:
#   medals.png, medals@2x.png, medals.json   the hero atlas, in Medallion's layout to the pixel (MedalLayout): the same
#                                           eleven sprites at 48/64/96/128 px, 2 px apart, and the same JSON schema
#   row.png, row.json                       the row strip: each state's row-tier master at every whole device pixel
#                                           from 12 to 31 (a real render at that size, not a downscale); 1x only
#   faces.png, faces@2x.png, faces.json,    the unframed faces for the frames axis (1.17 T11; ATLAS-CONTRACT section 7):
#   faces-row.png, faces-row.json           each state's under and over layers, at the hero tiers and every row pixel
#   metrics.json                            the per-set gates (section 7.1), the dark palettes' salience (G2D), the
#                                           cross-set similarity table (5.2, neutral kit, worst and per vision mode)
#                                           and the SHA-256 of each PNG the set ships
# and per frame kit (tools/themes/kits/<kit>.json), under Tsukimichi/assets/ui/kits/<kit>/:
#   frames.png, frames@2x.png, frames.json, frames-row.png, frames-row.json   four urgency tiers at Full and Quiet, and
#                                           the seven badges
#   metrics.json                            every set's faces composed in the kit, held to the per-set gates
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
#   python tools/themes/build_themes.py --check         rebuild into a temp folder and diff against the repo, and flag
#                                                       any file in a theme folder no build writes (exit 1)
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
KITS_DIR = os.path.join(HERE, "kits")
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
# The colour-vision gate's grounds (G1c): Night, the paired palette and a daylight sample.
CVD_GROUNDS = list(GROUNDS)
# The 1.17 dark palettes' windows, read from the palettes' design record (docs/design/v7/ui/1.17/palettes17.json), for
# the dark-palette salience gate (G2D, the realism supervisor's 1.17 ruling).
PALETTES17 = os.path.join(REPO, "docs", "design", "v7", "ui", "1.17", "palettes17.json")
DARK_PALETTES = {"dawn": "dawn", "kugane-lacquer": "kugane"}


def _dark_windows():
    with open(PALETTES17, encoding="utf-8") as fh:
        palettes = json.load(fh)["palettes"]
    return {ground: palettes[key]["Window"] for ground, key in DARK_PALETTES.items()}


DARK_GROUNDS = _dark_windows()
GROUNDS.update(DARK_GROUNDS)
# Ready's halo on a dark palette (a Moon-gold glow under the medal, the light wash's 3 px footprint). G2D gates each palette
# with the least it needs for every set (Ready 1.3, Completed 0.8) and every mix (Ready 1.25) to pass: no halo, else the
# shipped .45, else the .60 raise in the same footprint; every variant is recorded, and the palette records which it must
# draw. Medals are never recoloured.
DARK_HALOS = {"default": {"color": "#F2D27A", "alpha": 0.45, "radiusPx": 3},
              "raised": {"color": "#F2D27A", "alpha": 0.60, "radiusPx": 3}}
DARK_HALO_CHOICES = {"dawn": ["none", "default", "raised"], "kugane-lacquer": ["none", "default", "raised"]}
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

# ---------------------------------------------------------------- the frames axis (1.17 T11; theme-system §3.2-3.3)
# Faces: each state's well and emblem ('under', inside the shared well) and its overhangs ('over'), unframed. Kits: four
# urgency tiers of frame at Full and Quiet, and seven badges. The plugin draws face under, the kit's frame for the state's
# urgency tier, face over, then (from 32 px) the badge.
URGENCY = ["act-now", "resting", "finished", "ghost"]
FINISHES = ["full", "quiet"]
STATE_URGENCY = {"ready": "act-now", "ready-on-another-job": "resting", "in-journal": "resting", "blocked": "resting",
                 "done-this-cycle": "resting", "completed": "finished", "locked-out": "resting", "not-checked": "ghost"}
STATE_BADGE = {"ready": "open", "in-journal": "journal", "blocked": "closed"}
BADGES = ["open", "closed", "journal", "seat-tank", "seat-healer", "seat-dps", "seat-hand"]
# A badge sprite is the badge slot's quarter of the 128-unit box (centre 95, 95, keyline 24, and its drop shadow).
BADGE_BOX = (64, 64, 64, 64)
FULL_BOX = (0, 0, 128, 128)
# Part boxes snap to 8 units, so a box is whole pixels at every hero tier (48 px is 3/8 of the 128-unit box).
GRID = 8
# Ready on another job's metric composites, one per role seat, as the sets' own metrics measure it.
METRIC_JOBS = [("paladin", "seat-tank"), ("bard", "seat-dps"), ("white-mage", "seat-healer")]
# The cross-set table frames every face in one kit (§5.2): Brass, whose four urgency tiers are one bezel, so the frame
# pixels cancel in every pair and the faces decide.
NEUTRAL_KIT = "brass"
KITS_DEST = os.path.join("Tsukimichi", "assets", "ui", "kits")


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


def load_kit(key):
    with open(os.path.join(KITS_DIR, f"{key}.json"), encoding="utf-8") as fh:
        k = json.load(fh)
    assert k["key"] == key, f"kits/{key}.json names itself {k['key']}"
    assert sorted(k["badges"]) == sorted(BADGES), f"kit {key}: badges must be exactly {BADGES}"
    return k


def all_kits():
    keys = sorted(n[:-5] for n in os.listdir(KITS_DIR) if n.endswith(".json"))
    return [load_kit(k) for k in keys]


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


def write_text(text, path):
    """Writes a generated text file with LF endings, leaving it alone when only its line endings differ (git checkout)."""
    if os.path.exists(path):
        with open(path, encoding="utf-8") as fh:
            if fh.read().replace("\r\n", "\n") == text:
                return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


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


# ================================================================ faces and kits: the frames axis (1.17 T11)

def fill(text, subs):
    """Substitutes '{key}' for each value in `subs` (a path's '{tier}' is left for path_of, which takes the tier's folder)."""
    for key, value in subs.items():
        text = text.replace("{" + key + "}", str(value))
    return text


def resolve(m, spec, subs, tier_dir="."):
    """The SVG texts (bottom to top) of one part spec: a path, a python source, {"layers": [...]}, or a dict that picks
    by one of the substitutions' values (a face's {"under": ..., "over": ...}, a kit row's {"full": ..., "quiet": ...})."""
    path_subs = {k: v for k, v in subs.items() if k != "tier"}
    if isinstance(spec, str):
        return [read_text(path_of(m, fill(spec, path_subs), tier_dir))]
    if "python" in spec:
        return [python_source({**spec, "args": [fill(a, subs) for a in spec.get("args", [])]})]
    if "layers" in spec:
        out = []
        for layer in spec["layers"]:
            if isinstance(layer, str):
                out.append(read_text(path_of(m, fill(layer, path_subs), tier_dir)))
            else:
                out.append(layer_text(m, {**layer, "file": fill(layer["file"], path_subs)}, tier_dir))
        return out
    for value in subs.values():
        if isinstance(value, str) and value in spec:
            return resolve(m, spec[value], subs, tier_dir)
    raise KeyError(f"{m['key']}: no part for {subs} in {sorted(spec)}")


def face_texts(m, state, layer, tier):
    """One layer of a set's face of `state` at a hero tier (48 ... 128) or at the row tier ('row')."""
    spec = m["faces"]["row" if tier == "row" else "hero"]
    tier_dir = "." if tier == "row" else m["tiers"][str(tier)]
    return resolve(m, spec, {"state": state, "layer": layer, "tier": tier}, tier_dir)


def frame_texts(k, urgency, finish, tier):
    spec = k["frames"]["row" if tier == "row" else "hero"]
    return resolve(k, spec, {"urgency": urgency, "finish": finish, "tier": tier})


def badge_texts(k, badge):
    return resolve(k, k["badges"][badge], {})


def job_texts(job):
    return [python_source({"python": "tools/themes/sources.py", "call": "job_icon", "args": [job]})]


def face_parts(m):
    """{sprite: {tier: texts}} for a set's faces atlas (hero tiers) and {sprite: texts} for its row strip: each state's
    under layer as '<state>' and its over layer as '<state>-over'. Blank over layers are dropped by the probe."""
    hero = {}
    row = {}
    for state in STATES:
        for layer in ("under", "over"):
            name = state if layer == "under" else f"{state}-over"
            hero[name] = {tier: face_texts(m, state, layer, tier) for tier in TIERS}
            row[name] = face_texts(m, state, layer, "row")
    return hero, row


def frame_parts(k):
    """{sprite: {tier: texts}} for a kit's frames atlas and {sprite: texts} for its row strip: 'frame-<urgency>-<finish>'
    for the four urgency tiers at Full and Quiet, and 'badge-<kind>' (hero only) for the seven badges."""
    hero, row = {}, {}
    for urgency in URGENCY:
        for finish in FINISHES:
            name = f"frame-{urgency}-{finish}"
            hero[name] = {tier: frame_texts(k, urgency, finish, tier) for tier in TIERS}
            row[name] = frame_texts(k, urgency, finish, "row")
    for badge in BADGES:
        texts = badge_texts(k, badge)
        hero[f"badge-{badge}"] = {tier: texts for tier in TIERS}
    return hero, row


def probe(sources, tmp, name):
    """Renders each distinct source at 256 px (twice the 128-unit box) and returns {digest: (alpha array)}."""
    distinct = {}
    for texts in sources:
        digest = hashlib.sha256("\0".join(texts).encode()).hexdigest()
        distinct.setdefault(digest, texts)
    keys = list(distinct)
    per_row = 8
    cell = 256
    parts = []
    for i, key in enumerate(keys):
        view, body = body_of(distinct[key], f"q{i}_")
        parts.append(f'<svg x="{(i % per_row) * cell}" y="{(i // per_row) * cell}" width="{cell}" height="{cell}" '
                     f'viewBox="{view}" overflow="hidden">{body}</svg>')
    w, h = per_row * cell, -(-len(keys) // per_row) * cell
    im = render_svg(f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{"".join(parts)}</svg>',
                    tmp, f"probe-{name}", w, h)
    a = np.asarray(im, dtype=np.uint8)[..., 3]
    return {key: a[(i // per_row) * cell:(i // per_row + 1) * cell, (i % per_row) * cell:(i % per_row + 1) * cell]
            for i, key in enumerate(keys)}


def digest_of(texts):
    return hashlib.sha256("\0".join(texts).encode()).hexdigest()


def unit_box(alphas):
    """The smallest GRID-aligned box (x, y, w, h in the 128-unit box) holding every drawn pixel of `alphas` (256 px
    renders), with a unit of margin for the smaller tiers' antialiasing; None when nothing is drawn."""
    ys, xs = [], []
    for a in alphas:
        yy, xx = np.nonzero(a)
        ys += [yy.min(), yy.max()] if len(yy) else []
        xs += [xx.min(), xx.max()] if len(xx) else []
    if not ys:
        return None
    x0 = max(0, int(min(xs) / 2 - 1) // GRID * GRID)
    y0 = max(0, int(min(ys) / 2 - 1) // GRID * GRID)
    x1 = min(128, -(-int(max(xs) / 2 + 2) // GRID) * GRID)
    y1 = min(128, -(-int(max(ys) / 2 + 2) // GRID) * GRID)
    return x0, y0, x1 - x0, y1 - y0


def part_boxes(hero, row, tmp, name, fixed):
    """Each hero sprite's box (snapped to GRID; `fixed` boxes are held, and checked to contain what they draw), and the
    sprites dropped because they draw nothing at any size (an over layer with no overhang)."""
    alphas = probe([t for by_tier in hero.values() for t in by_tier.values()] + list(row.values()), tmp, name)
    boxes, blank = {}, []
    for sprite, by_tier in hero.items():
        drawn = [alphas[digest_of(t)] for t in by_tier.values()]
        if sprite in row:
            drawn.append(alphas[digest_of(row[sprite])])
        box = unit_box(drawn)
        if box is None:
            blank.append(sprite)
            continue
        if sprite in fixed:
            fx, fy, fw, fh = fixed[sprite]
            bx, by, bw, bh = box
            assert fx <= bx and fy <= by and bx + bw <= fx + fw and by + bh <= fy + fh, \
                f"{name} {sprite}: draws in {box}, outside its fixed box {fixed[sprite]}"
            box = fixed[sprite]
        boxes[sprite] = box
    return boxes, blank


def pack_parts(boxes, cells):
    """Deterministic shelf packing of every (sprite, cell) at its box's size, PAD apart and PAD inside the edges: tallest
    first, in sprite order. Returns ({sprite: {cell: (x, y, w, h)}}, (width, height))."""
    items = []
    for i, (sprite, box) in enumerate(boxes.items()):
        for cell in cells:
            w, h = box[2] * cell // 128, box[3] * cell // 128
            assert w * 128 == box[2] * cell and h * 128 == box[3] * cell, f"{sprite} {cell}: box {box} is not whole pixels"
            items.append((h, w, i, sprite, cell))
    area = sum((w + PAD) * (h + PAD) for h, w, *_ in items)
    width = max(max(w for _, w, *_ in items) + 2 * PAD, int(np.ceil(np.sqrt(area * 1.1))))
    width += width % 2
    items.sort(key=lambda t: (-t[0], -t[1], t[2], t[4]))
    rects = {sprite: {} for sprite in boxes}
    x, y, shelf = PAD, PAD, 0
    for h, w, _, sprite, cell in items:
        if x + w + PAD > width:
            x, y, shelf = PAD, y + shelf + PAD, 0
        rects[sprite][cell] = (x, y, w, h)
        x += w + PAD
        shelf = max(shelf, h)
    return rects, (width, y + shelf + PAD)


def parts_sheet(scale, size, rects, boxes, sources):
    """One SVG drawing every (sprite, cell) of a parts atlas: the sprite's box of the 128-unit box, at the cell's size."""
    parts = []
    n = 0
    for sprite, by_cell in rects.items():
        bx, by, bw, bh = boxes.get(sprite, FULL_BOX)
        for cell, (x, y, w, h) in by_cell.items():
            texts = sources[sprite][cell] if isinstance(sources[sprite], dict) else sources[sprite]
            view, body = body_of(texts, f"p{n}_")
            n += 1
            vx, vy, vw, vh = (float(v) for v in view.split())
            assert (vx, vy, vw, vh) == (0, 0, 128, 128), f"{sprite}: parts must be drawn in the 128-unit box, not {view}"
            parts.append(f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="{bx} {by} {bw} {bh}" overflow="hidden">{body}</svg>')
    w, h = size
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w * scale}" height="{h * scale}" viewBox="0 0 {w} {h}">'
            f'{"".join(parts)}</svg>')


def parts_row_layout(names):
    """{sprite: {size: (x, y, size, size)}}: one shelf per sprite, sizes 12 ... 31 left to right (the row strip's)."""
    width = PAD + sum(s + PAD for s in ROW_SIZES)
    rects = {}
    for i, name in enumerate(names):
        y = PAD + i * (ROW_SIZES[-1] + PAD)
        x = PAD
        rects[name] = {}
        for s in ROW_SIZES:
            rects[name][s] = (x, y, s, s)
            x += s + PAD
    return rects, (width, PAD + len(names) * (ROW_SIZES[-1] + PAD))


def write_parts(stem, dest, hero, row, boxes, tmp, key, note):
    """Renders and writes one parts atlas pair: <stem>.png, <stem>@2x.png and <stem>.json at the hero tiers, and
    <stem>-row.png and <stem>-row.json at every whole pixel from 12 to 31 (the row strip carries no badges)."""
    os.makedirs(dest, exist_ok=True)
    out = {"boxes": boxes}
    rects, size = pack_parts(boxes, TIERS)
    for scale, suffix in ((1, ""), (2, "@2x")):
        im = render_svg(parts_sheet(scale, size, rects, boxes, hero), tmp, f"{key}-{stem}{suffix}", size[0] * scale, size[1] * scale)
        write_png(im, os.path.join(dest, f"{stem}{suffix}.png"))
        out[f"{stem}{suffix}"] = im
    write_json({
        "size": list(size),
        "tiers": TIERS,
        "note": note + " Rectangles are [x, y, w, h] in 1x pixels per tier; each sprite's 'box' is the part of the "
                       "128-unit medal box it covers, so a cell is box × tier / 128 px. The @2x PNG is the same layout "
                       "at twice the size.",
        "boxes": {sprite: list(box) for sprite, box in boxes.items()},
        "sprites": {sprite: {str(c): list(r) for c, r in by.items()} for sprite, by in rects.items()},
    }, os.path.join(dest, f"{stem}.json"))
    out[f"{stem}-rects"] = rects

    names = [s for s in boxes if s in row]
    rrects, rsize = parts_row_layout(names)
    im = render_svg(parts_sheet(1, rsize, rrects, {}, {s: row[s] for s in names}), tmp, f"{key}-{stem}-row", *rsize)
    write_png(im, os.path.join(dest, f"{stem}-row.png"))
    write_json({
        "size": list(rsize),
        "sizes": ROW_SIZES,
        "note": note + " Row cells: each sprite rendered at every whole device pixel size (the whole 128-unit box); "
                       "rectangles are [x, y, w, h] in device pixels, 1x only.",
        "sprites": {sprite: {str(s): list(r) for s, r in by.items()} for sprite, by in rrects.items()},
    }, os.path.join(dest, f"{stem}-row.json"))
    out[f"{stem}-row"] = im
    out[f"{stem}-row-rects"] = rrects
    return out


def build_faces(m, dest_set, tmp):
    """A set's faces atlases (faces.*, faces-row.*): every state's under layer, and its over layer where it has one."""
    hero, row = face_parts(m)
    boxes, blank = part_boxes(hero, row, tmp, f"{m['key']}-faces", {})
    assert not [b for b in blank if not b.endswith("-over")], f"{m['key']}: a face's under layer draws nothing: {blank}"
    note = (f"{m['name']}'s unframed faces (theme-system §3.2): '<state>' is the well and emblem, '<state>-over' the "
            "overhangs drawn above the kit's frame (only states that have them).")
    return write_parts("faces", dest_set, hero, row, boxes, tmp, m["key"], note)


def build_frames(k, dest, tmp):
    """A kit's frames atlases (frames.*, frames-row.*): the four urgency tiers at Full and Quiet, and the badges."""
    hero, row = frame_parts(k)
    fixed = {f"badge-{b}": BADGE_BOX for b in BADGES}
    fixed.update({f"frame-{u}-{f}": FULL_BOX for u in URGENCY for f in FINISHES})
    boxes, blank = part_boxes(hero, row, tmp, f"{k['key']}-frames", fixed)
    assert not blank, f"kit {k['key']}: these parts draw nothing: {blank}"
    note = (f"The {k['name']} kit (theme-system §3.3): 'frame-<urgency>-<finish>' for act-now, resting, finished and "
            "ghost at Full and Quiet, and 'badge-<kind>' at the badge slot (open, closed, journal, and the four empty role "
            "seats; the plugin draws the job icon).")
    return write_parts("frames", dest, hero, row, boxes, tmp, k["key"], note)


def combo_sources(m, k, tier):
    """{state: [svg]} for set `m`'s faces in kit `k` as the plugin composes them, at a hero tier (badges in; Ready on
    another job once per role seat with its job's icon) or at the row tier ('row', no badges)."""
    out = {}
    for state in STATES:
        layers = (face_texts(m, state, "under", tier) + frame_texts(k, STATE_URGENCY[state], "full", tier)
                  + face_texts(m, state, "over", tier))
        if tier == "row":
            out[state] = [standalone(layers)]
        elif state == "ready-on-another-job":
            out[state] = [standalone(layers + badge_texts(k, seat) + job_texts(job)) for job, seat in METRIC_JOBS]
        else:
            badge = STATE_BADGE.get(state)
            out[state] = [standalone(layers + (badge_texts(k, badge) if badge else []))]
    return out


def combo_groups(m, k):
    """The hero tiers grouped by identical composites (each measured once), then the row tier, as tier_groups does."""
    groups = []
    for tier in TIERS:
        src = combo_sources(m, k, tier)
        digest = hashlib.sha256(json.dumps(src, sort_keys=True).encode()).hexdigest()
        for g in groups:
            if g["digest"] == digest:
                g["tiers"].append(tier)
                break
        else:
            groups.append({"digest": digest, "tiers": [tier], "sources": src})
    for g in groups:
        g["name"] = "hero-" + "-".join(str(t) for t in g["tiers"])
    groups.append({"name": "row", "tiers": [], "sources": combo_sources(m, k, "row"), "digest": None})
    return groups


def measure_groups(groups, tmp, name):
    rows = [(g, s) for g in [*GROUNDS, *MATTES] for s in SIZES]
    for g in groups:
        flat = [svg for s in STATES for svg in g["sources"][s]]
        g["strip"] = metric_strip(flat, rows, tmp, f"metrics-{name}-{g['name']}")
        g["measures"] = []
        for ground in GROUNDS:
            for size in SIZES:
                cells = cells_for(g["strip"], g["sources"], ground, size)
                for mode in ALL_MODES:
                    pairs, sal = measure(cells, None, mode)
                    g["measures"].append({"ground": ground, "px": size, "mode": mode, "pairs": pairs, "salience": sal})
    return groups


def kit_flags(groups):
    """What the Frames row warns about for a set's faces in a kit (spec-1.17 §A3's words, the sets' own bars): each pair
    of states under the bars at 16 px on a gated ground and mode (greyscale and Vienot deuteranopia 12 on Night, Machado's
    three 11 on every gated ground; 'hard' under 10, else 'close'), its lowest value; and, at 16 px greyscale on Night,
    Ready leading the next state by under mixReadyLead, or Completed over completedOfReady of Ready."""
    pairs, ready, completed = {}, None, None
    for g in groups:
        for meas in g["measures"]:
            if not is_gated(meas) or meas["px"] != 16:
                continue
            bar = BARS["weakest16"] if meas["mode"] in GATE_MODES else BARS["cvdWeakest16"]
            for (a, b), v in meas["pairs"].items():
                if at_bar(v) < bar and ((a, b) not in pairs or v < pairs[(a, b)]["d"]):
                    pairs[(a, b)] = {"kind": "pair", "a": a, "b": b, "d": r1(v), "mode": meas["mode"], "ground": meas["ground"],
                                     "tier": g["name"], "level": "hard" if at_bar(v) < BARS["mixHard"] else "close"}
            if meas["ground"] == "night" and meas["mode"] == "grey":
                sal = meas["salience"]
                nxt_state, nxt = max(((s, v) for s, v in sal.items() if s != "ready"), key=lambda kv: kv[1])
                lead = round(sal["ready"] / nxt, 2)
                if lead < BARS["mixReadyLead"] and (ready is None or lead < ready["d"]):
                    ready = {"kind": "ready", "a": "ready", "b": nxt_state, "d": lead, "mode": "grey", "ground": "night",
                             "tier": g["name"], "level": "close"}
                comp = round(sal["completed"] / sal["ready"], 2)
                if comp > BARS["completedOfReady"] and (completed is None or comp > completed["d"]):
                    completed = {"kind": "completed", "a": "completed", "b": "ready", "d": comp, "mode": "grey",
                                 "ground": "night", "tier": g["name"], "level": "close"}
    flags = sorted(pairs.values(), key=lambda x: (x["d"], STATES.index(x["a"]), STATES.index(x["b"])))
    return flags + [x for x in (ready, completed) if x]


CHECKS_CS = os.path.join("Tsukimichi.Core", "Ui", "Themes", "FrameKitChecks.g.cs")
CS_SET = {"medallion": "Medallion", "ishgard-glass": "IshgardGlass", "aether-crystal": "AetherCrystal",
          "astrologian-orrery": "Orrery", "sumi-to-kinpaku": "Sumi"}
CS_KIT = {"brass": "Brass", "silver": "Silver", "came": "Came", "astrolabe": "Astrolabe", "kirikane": "Kirikane"}
CS_STATE = dict(zip(STATES, ["Ready", "ReadyOnOtherJob", "Accepted", "Blocked", "DoneThisCycle", "Completed", "Foreclosed",
                             "Unknown"]))
CS_KIND = {"pair": "Pair", "ready": "ReadyLead", "completed": "CompletedRecedes"}


def checks_cs(kit_results):
    """The Frames row's warnings as a compiled table (metrics.json is not packaged): every flag of every set's faces in
    every kit but its own, from the kits' metrics.json."""
    rows = []
    for kit, data in sorted(kit_results.items()):
        for set_key, f in sorted(data["faces"].items()):
            if f["own"]:
                continue
            for x in f["flags"]:
                rows.append(f"        new(FrameKitId.{CS_KIT[kit]}, GlyphSetId.{CS_SET[set_key]}, KitFlagKind.{CS_KIND[x['kind']]}, "
                            f"QuestState.{CS_STATE[x['a']]}, QuestState.{CS_STATE[x['b']]}, {x['d']}f, "
                            f"Hard: {'true' if x['level'] == 'hard' else 'false'}, ColourVision: {'true' if x['mode'].startswith('machado') else 'false'}),")
    return ("// <auto-generated>\n"
            "// Written by tools/themes/build_themes.py from Tsukimichi/assets/ui/kits/*/metrics.json (each kit's 'faces[set].flags');\n"
            "// rebuild with the tool, never by hand. FrameKitChecksTests holds it to those files.\n"
            "// </auto-generated>\n"
            "using Tsukimichi.Core.Model;\n\n"
            "namespace Tsukimichi.Core.Ui.Themes;\n\n"
            "public static partial class FrameKitChecks\n{\n"
            "    private static readonly KitFlag[] Flags =\n    [\n" + "\n".join(rows) + ("\n" if rows else "") + "    ];\n}\n")


def measure_combo(m, k, tmp):
    """Every gate a set passes, for set `m`'s faces in kit `k`: G1, G1c and G2 per tier group, and G2L on the row tier."""
    groups = measure_groups(combo_groups(m, k), tmp, f"{m['key']}-in-{k['key']}")
    light_gates, light = light_measures(groups)
    return groups, gate_rows(m, groups) + light_gates, light


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


# The lightness floor's "next state" leaves Not checked out (the supervisor's second ruling): its dark face outweighs
# Ready's light one in plain luminance, so it is held to stay under Ready on chroma instead (G2L Not checked).
LUMA_FLOOR_EXCLUDES = ("not-checked",)


def light_lead(sal, exclude=()):
    nxt_state, nxt = max(((s, v) for s, v in sal.items() if s != "ready" and s not in exclude), key=lambda kv: kv[1])
    return sal["ready"] / nxt, nxt_state, nxt


def light_measures(groups):
    """G2L, the realism supervisor's ruling (spec-1.16 A4.1), on the row tier at 16 and 20 px with the default wash:
    Ready leads the next state by 1.3 under the weighted measure at both sizes, or failing that under chroma only (the
    record names the measure that passed); Ready's plain luminance salience is at least .70 of the next state's, Not
    checked left out ('luma' leads are recorded that way); and Not checked stays under Ready on chroma. No wash and the
    old fallback are recorded for reviewers. Returns (gate rows, the record for metrics.json)."""
    row = next(g for g in groups if g["name"] == "row")
    variants = {}
    for name, wash in [("none", None), *WASHES.items()]:
        variants[name] = {}
        for size in SIZES:
            entry = {}
            for measure, sal in light_salience(row, wash, size).items():
                lead, nxt_state, _ = light_lead(sal, LUMA_FLOOR_EXCLUDES if measure == "luma" else ())
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
        sal = used[str(size)]["chroma"]["salience"]
        gates.append({"gate": f"G2L Not checked under Ready {size} px (chroma) on Ishgard Snow", "tier": "row",
                      "value": r1(sal["not-checked"] / sal["ready"]), "bar": 1.0,
                      "detail": f"NotC {sal['not-checked']:.0f} / Rdy {sal['ready']:.0f}",
                      "pass": sal["not-checked"] < sal["ready"]})
    record = {"ground": LIGHT_GROUND, "window": GROUNDS[LIGHT_GROUND], "tier": "row", "gatePx": SIZES,
              "measure": measure, "use": LIGHT_WASH, "washes": WASHES, "variants": variants}
    return gates, record


def dark_salience(g, ground, halo, size):
    """Each state's greyscale salience (round 5's G2 measure) on a dark palette's window, Ready drawn over its halo
    (None: none), from the black and white matte renders as light_salience composes the wash. Ready on another job counts
    its loudest job. Returns {state: salience}."""
    window = hex_rgb(GROUNDS[ground])
    window_luma = luma(window)
    black = cells_for(g["strip"], g["sources"], "matte-black", size)
    white = cells_for(g["strip"], g["sources"], "matte-white", size)
    plain = cells_for(g["strip"], g["sources"], ground, size)
    out = {}
    for s in STATES:
        vals = []
        for b, w, p in zip(black[s], white[s], plain[s]):
            c = p
            if s == "ready" and halo:
                # Chrome's own render on the window, plus the halo showing through wherever the medal lets the ground
                # through (its alpha from the mattes): the composite over the haloed ground, without rebuilding the
                # medal's colour (which a filter's rounding on a dark ground can skew by a few levels at an edge).
                _, a = unmatte(b, w)
                back = np.broadcast_to(window, b.shape)
                cover = (wash_cover(a, halo["radiusPx"]) * halo["alpha"])[..., None]
                haloed = back * (1 - cover) + hex_rgb(halo["color"]) * cover
                c = np.clip(p + (1 - a)[..., None] * (haloed - back), 0, 1)
            vals.append(float(np.abs(luma(c) - window_luma).sum()))
        out[s] = max(vals)
    return out


def dark_record(groups):
    """G2D, the dark palettes' salience (the realism supervisor's 1.17 ruling): for every tier group at 16 and 20 px on
    Dawn's and Kugane Lacquer's windows, Ready leads the next state by 1.3 and Completed stays at 0.8 of Ready or under,
    Ready over its halo. Every halo is recorded (and none); a palette's gate uses its first halo under which every tier
    group passes (none, else the shipped .45, else the raised .60). Returns {ground: {tier group: {halo: {px: entry}}}}."""
    rec = {}
    for ground in DARK_GROUNDS:
        rec[ground] = {}
        for g in groups:
            rec[ground][g["name"]] = {}
            for name, halo in [("none", None), *DARK_HALOS.items()]:
                rec[ground][g["name"]][name] = {}
                for size in SIZES:
                    sal = dark_salience(g, ground, halo, size)
                    nxt_state, nxt = max(((s, v) for s, v in sal.items() if s != "ready"), key=lambda kv: kv[1])
                    rec[ground][g["name"]][name][str(size)] = {
                        "salience": {s: r1(v) for s, v in sal.items()}, "readyLead": r1(sal["ready"] / nxt),
                        "next": nxt_state, "completedOfReady": r1(sal["completed"] / sal["ready"])}
    return rec


def dark_passes(entry):
    return entry["readyLead"] >= BARS["readyLead"] and entry["completedOfReady"] <= BARS["completedOfReady"]


def dark_halo_for(ground, records, neutral):
    """The halo a dark palette must draw: its first choice under which every set's every tier group passes G2D at both
    sizes and every mix keeps Ready's lead (dark_mix); the last choice when none does, so the gate rows show the miss.
    Returns (halo name, the mix rows under it)."""
    choices = DARK_HALO_CHOICES[ground]
    for name in choices:
        mixes = dark_mix(neutral, ground, name)
        if all(dark_passes(by_px[px]) for rec in records for by_halo in rec[ground].values()
               for by_px in [by_halo[name]] for px in by_px) and all(x["pass"] for x in mixes):
            return name, mixes
    return choices[-1], mixes


def dark_gate_rows(rec, halos):
    rows = []
    for ground, by_group in rec.items():
        halo = halos[ground]
        for group, by_halo in by_group.items():
            for px, e in by_halo[halo].items():
                rows.append({"gate": f"G2D Ready lead {px} px on {ground} (halo {halo})", "tier": group, "value": e["readyLead"],
                             "bar": BARS["readyLead"], "detail": f"Rdy {e['salience']['ready']:.0f} / {SHORT[e['next']]} "
                                                                 f"{e['salience'][e['next']]:.0f}",
                             "pass": e["readyLead"] >= BARS["readyLead"]})
                rows.append({"gate": f"G2D Completed recedes {px} px on {ground} (halo {halo})", "tier": group,
                             "value": e["completedOfReady"], "bar": BARS["completedOfReady"],
                             "detail": f"Comp {e['salience']['completed']:.0f} / Rdy {e['salience']['ready']:.0f}",
                             "pass": e["completedOfReady"] <= BARS["completedOfReady"]})
    return rows


def dark_mix(neutral, ground, halo_name):
    """A mix's Ready from set A against the other states from set B on a dark palette's window, Ready over the halo
    `halo_name`, at the row tier and the 48 px hero tier, 16 and 20 px (faces in the neutral kit, as the cross table):
    Ready must lead by mixReadyLead (1.25)."""
    out = []
    halo = DARK_HALOS.get(halo_name)
    for which in ("row", "hero"):
        for size in SIZES:
            sal = {k: (dark_salience(v[which], ground, halo, size), dark_salience(v[which], ground, None, size))
                   for k, v in neutral.items()}
            for a_key, b_key in itertools.permutations(list(neutral), 2):
                ready = sal[a_key][0]["ready"]
                others = sal[b_key][1]
                nxt_state, nxt = max(((s, v) for s, v in others.items() if s != "ready"), key=lambda kv: kv[1])
                lead = r1(ready / nxt)
                out.append({"ground": ground, "halo": halo_name, "tier": which, "px": size, "ready": a_key,
                            "others": b_key, "lead": lead, "next": nxt_state, "pass": round(lead, 2) >= BARS["mixReadyLead"]})
    return out


def dark_mix_gate_rows(set_key, mixes):
    """G2D's mix rows for the mixes whose Ready comes from `set_key`."""
    return [{"gate": f"G2D mix Ready lead {x['px']} px on {x['ground']} (halo {x['halo']})", "tier": x["tier"],
             "value": x["lead"], "bar": BARS["mixReadyLead"], "detail": f"Ready from {set_key}, the rest from {x['others']} "
                                                                       f"(next {SHORT[x['next']]})", "pass": x["pass"]}
            for x in mixes if x["ready"] == set_key]


def at_bar(v):
    """Distinctness and salience are judged at one decimal, the precision round 5 set its bars at (its shipped row
    tier holds Blocked vs Locked out at 12.0, which is 11.96 unrounded). Ratios are judged at two decimals."""
    return round(float(v), 1)


def is_gated(meas):
    return (meas["ground"] == "night" and meas["mode"] in GATE_MODES) or is_cvd_gate(meas)


def is_cvd_gate(meas):
    return meas["px"] == 16 and meas["mode"] in CVD_MODES and meas["ground"] in CVD_GROUNDS


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
    sets = [("medals", 1, hero_layout()), ("medals@2x", 2, hero_layout())]
    if "row" in atlas:
        sets.append(("row", 1, row_layout()[0]))
    return fit_check(atlas, sets)


def fit_parts(parts, stem):
    """The same fit check for a faces or frames atlas pair."""
    return fit_check(parts, [(stem, 1, parts[f"{stem}-rects"]), (f"{stem}@2x", 2, parts[f"{stem}-rects"]),
                             (f"{stem}-row", 1, parts[f"{stem}-row-rects"])])


def fit_check(atlas, sets):
    rows = []
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
    MODES_TABLE.clear()
    for a_key, b_key in itertools.permutations(keys, 2):
        for which in ("row", "hero"):
            ga = measured[a_key][which]
            gb = measured[b_key][which]
            for size in SIZES:
                worst = {}
                per_mode = {}
                for mode in ALL_MODES:
                    f = MODE_FN[mode]
                    ca = cells_for(ga["strip"], ga["sources"], "night", size)
                    cb = cells_for(gb["strip"], gb["sources"], "night", size)
                    la = {s: [blur(f(c)) for c in cs] for s, cs in ca.items()}
                    lb = {s: [blur(f(c)) for c in cs] for s, cs in cb.items()}
                    per_mode[mode] = {}
                    for s in STATES:
                        for t in STATES:
                            if s == t:
                                continue
                            v = min(np.abs(x - y).sum() for x in la[s] for y in lb[t])
                            per_mode[mode][pair_key(s, t)] = r1(v)
                            if (s, t) not in worst or v < worst[(s, t)][0]:
                                worst[(s, t)] = (v, mode)
                out.setdefault(a_key, {}).setdefault(b_key, {}).setdefault(which, {})[str(size)] = [
                    {"a": s, "b": t, "d": r1(v), "mode": mode} for (s, t), (v, mode) in worst.items()]
                MODES_TABLE.setdefault(a_key, {}).setdefault(b_key, {}).setdefault(which, {})[str(size)] = per_mode
    return out


# The cross-set table per vision mode (the realism supervisor's 1.17 ruling): {set: {other: {tier: {px: {mode: {pair: d}}}}}},
# filled by cross_table, so a mix is held to each set's own bars (greyscale and Vienot deuteranopia 12, Machado's 11).
MODES_TABLE = {}


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


def metrics_json(m, groups, gates, light, cross, mix_sal, chrome_ver, pngs, dark, halos, dark_mixes):
    """The committed record: the full pair and salience tables behind the gates (Night, greyscale and Vienot
    deuteranopia, 16 and 20 px; every ground, Machado's three, 16 px), a one-line summary for every other ground,
    mode and size, the light-palette salience and the Ready wash the plugin must draw, this set's half of the
    cross-set table, and the SHA-256 of every PNG the set ships (so ThemeAtlasTests can hold the committed atlases
    to the build these numbers came from)."""
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
                "salience (its lead leaves Not checked out); G2L gates the default wash at 16 and 20 px, and "
                "'measure' names the lead measure that passed. 'readyWash' is the wash the plugin draws for this set.",
        "chrome": chrome_ver,
        "grounds": GROUNDS,
        "bars": BARS,
        "gateModes": GATE_MODES,
        "cvdModes": CVD_MODES,
        "readyWash": light["washes"][light["use"]],
        "pngs": pngs,
        "pass": all(r["pass"] for r in gates),
        "gates": gates,
        "tiers": tiers,
        "light": light,
        "cross": {"note": "Pairs are 'this set's state|the other set's state', each pair of different states, worst "
                          "over every vision mode, on Night, for the row tier and the 48 px hero tier's sources, at "
                          f"16 and 20 px. Every face is framed in the neutral kit ({NEUTRAL_KIT}: one bezel for every "
                          "urgency tier, so the frame cancels and the faces decide), as a mix draws one kit for the "
                          "whole column. Under mixClose reads 'close', under mixHard 'hard to tell apart' (section 5.2).",
                  "kit": NEUTRAL_KIT,
                  "sets": sets,
                  "modesNote": "'modes' is the same table per vision mode, {other set: {tier: {px: {mode: {'this "
                               "state|other state': d}}}}}, so a mix is held to the sets' own bars: greyscale and "
                               "Vienot deuteranopia ('deut') at weakest16, Machado's three at cvdWeakest16.",
                  "modes": MODES_TABLE.get(m["key"], {}),
                  "salience": [x for x in mix_sal if m["key"] in (x["ready"], x["others"])]},
        "dark": {"note": "G2D (the realism supervisor's 1.17 ruling): greyscale salience on the dark palettes' windows "
                         "(docs/design/v7/ui/1.17/palettes17.json) per tier group at 16 and 20 px, Ready over its halo "
                         "(none, the shipped 'default' .45, or 'raised' .60). 'halo' is the least halo each palette "
                         "must draw (the same for every set); the gate uses it. 'mix' is Ready from one set against "
                         "the rest from another (neutral kit), held to mixReadyLead.",
                 "windows": DARK_GROUNDS,
                 "halos": DARK_HALOS,
                 "halo": halos,
                 "tiers": dark,
                 "mix": [x for x in dark_mixes if m["key"] in (x["ready"], x["others"])]},
    }


def kit_metrics_json(k, combos, own, fit, chrome_ver, pngs):
    """A kit's record: every set's faces framed in it, held to the per-set gates (G1, G1c and G2 per tier group, G2L on
    the row tier), with the weakest pair and Ready's lead per group for reviewers; the fit of its own atlases; and the
    SHA-256 of each PNG it ships. A set in its own kit is what the set ships (its composites), so it must pass; any
    other set's faces in this kit are a user's frames choice, and a gate it misses is a warning (theme-system §5.2:
    measured checks warn and never block), listed under 'warnings' for the Themes page."""
    faces = {}
    for set_key, (groups, gates, light) in combos.items():
        faces[set_key] = {
            "own": set_key in own,
            "pass": all(g["pass"] for g in gates),
            "warnings": [f"{g['tier']}: {g['gate']}" for g in gates if not g["pass"]],
            "gates": gates,
            "tiers": [{"name": g["name"], "atlasTiers": g["tiers"],
                       "summary": [summary(x) for x in g["measures"] if is_gated(x)]} for g in groups],
            "lightMeasure": light["measure"],
            "flags": [] if set_key in own else kit_flags(groups),
        }
    return {
        "kit": k["key"],
        "name": k["name"],
        "note": "Written by tools/themes/build_themes.py; see tools/themes/README.md. 'faces' holds every set's faces "
                "composed in this kit as the plugin composes them (face under, the frame for the state's urgency tier, "
                "face over, the badge from 32 px; a set in its own kit, 'own', is its shipped composites), held to the "
                "per-set gates (bars as in a set's metrics.json). An own set must pass; for the others a missed gate "
                "is a warning, listed in 'warnings'. 'fit' checks the kit's own atlases.",
        "chrome": chrome_ver,
        "bars": BARS,
        "pngs": pngs,
        "pass": all(f["pass"] for f in faces.values() if f["own"]) and all(r["pass"] for r in fit),
        "fit": fit,
        "faces": faces,
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


def compose_from_atlases(faces, frames, state, tier, quiet=False):
    """One medal composed from the faces and frames atlases exactly as the plugin composes it (face under, frame for the
    state's urgency tier, face over, then the badge from 32 px), at a hero tier (1x) or at a row size ('row', N)."""
    finish = "quiet" if quiet else "full"
    if isinstance(tier, tuple):
        size = tier[1]
        canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        layers = [(faces, "faces-row", state), (frames, "frames-row", f"frame-{STATE_URGENCY[state]}-{finish}"),
                  (faces, "faces-row", f"{state}-over")]
        for atlas, stem, sprite in layers:
            rect = atlas[f"{stem}-rects"].get(sprite, {}).get(size)
            if rect:
                x, y, w, h = rect
                canvas.alpha_composite(atlas[stem].crop((x, y, x + w, y + h)), (0, 0))
        return canvas
    canvas = Image.new("RGBA", (tier, tier), (0, 0, 0, 0))
    badge = STATE_BADGE.get(state) or ("seat-tank" if state == "ready-on-another-job" else None)
    layers = [(faces, "faces", state), (frames, "frames", f"frame-{STATE_URGENCY[state]}-{finish}"),
              (faces, "faces", f"{state}-over")] + ([(frames, "frames", f"badge-{badge}")] if badge else [])
    for atlas, stem, sprite in layers:
        rect = atlas[f"{stem}-rects"].get(sprite, {}).get(tier)
        if rect:
            x, y, w, h = rect
            bx, by, _, _ = atlas["boxes"][sprite]
            canvas.alpha_composite(atlas[stem].crop((x, y, x + w, y + h)), (bx * tier // 128, by * tier // 128))
    return canvas


def mix_sheet(manifests, kits, faces, frames, path):
    """Every set's faces in every kit, composed from the shipped atlases as the plugin composes them: 96 and 48 px at
    Full, 48 px at Quiet, and a 20 px row, on Night. Rows: set; blocks: kit."""
    fnt = font()
    block_h = 96 + 48 + 22 + 20
    col_w = len(STATES) * 100 + 180
    H = 20 + len(kits) * (len(manifests) * (block_h + 10) + 24)
    sheet = Image.new("RGBA", (col_w, H), NIGHT)
    d = ImageDraw.Draw(sheet)
    y = 10
    for k in kits:
        d.text((10, y), f"{k['name']} kit", fill=(230, 233, 242), font=fnt)
        y += 18
        for m in manifests:
            d.text((10, y + 40), m["name"], fill=(170, 178, 200), font=fnt)
            for i, state in enumerate(STATES):
                x = 170 + i * 100
                f_, fr = faces[m["key"]], frames[k["key"]]
                sheet.alpha_composite(compose_from_atlases(f_, fr, state, 96), (x, y))
                sheet.alpha_composite(compose_from_atlases(f_, fr, state, 48), (x, y + 98))
                sheet.alpha_composite(compose_from_atlases(f_, fr, state, 48, quiet=True), (x + 50, y + 98))
                sheet.alpha_composite(compose_from_atlases(f_, fr, state, ("row", 20)), (x, y + 150))
            y += block_h + 10
        y += 6
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


def report_text(results, kit_results, mix_sal, cross):
    lines = []
    for key, r in kit_results.items():
        lines.append(f"== {key} kit: {'PASS' if r['pass'] else 'FAIL'}")
        for g in r["fit"]:
            lines.append(f"  {'ok  ' if g['pass'] else 'FAIL'} {g['tier']:<14} {g['gate']:<32} {g['value']:>7} (bar {g['bar']}) {g['detail']}")
        for set_key, f in r["faces"].items():
            failing = [g for g in f["gates"] if not g["pass"]]
            worst = min((g for g in f["gates"] if g["gate"].startswith("G1")), key=lambda g: g["value"] - g["bar"])
            lead = min((g for g in f["gates"] if g["gate"] == "G2 Ready lead"), key=lambda g: g["value"])
            tag = "ok  " if f["pass"] else ("FAIL" if f["own"] else "warn")
            lines.append(f"  {tag} {set_key + (' (own)' if f['own'] else ''):<26} tightest G1 {worst['value']} (bar {worst['bar']}, "
                         f"{worst['tier']} {worst['gate']} {worst['detail']}); Ready lead {lead['value']} ({lead['tier']})")
            for g in failing:
                lines.append(f"       {tag} {g['tier']:<14} {g['gate']} {g['value']} (bar {g['bar']}) {g['detail']}")
            for x in f.get("flags", []):
                lines.append(f"       Frames row: {x['level']:<5} {x['kind']:<9} {SHORT[x['a']]}-{SHORT[x['b']]} {x['d']} ({x['mode']} on {x['ground']}, {x['tier']})")
        lines.append("")
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
    if results:
        any_set = next(iter(results.values()))["dark"]
        lines.append("")
        lines.append(f"== dark palettes (G2D), halo used: {any_set['halo']}; per set, tier group, px: lead / Comp/Rdy under none, default, raised")
        for key, r in results.items():
            for ground, by_group in r["dark"]["tiers"].items():
                for group, by_halo in by_group.items():
                    for px in ("16", "20"):
                        cells = "  ".join(f"{h} {by_halo[h][px]['readyLead']}/{by_halo[h][px]['completedOfReady']}" for h in by_halo)
                        lines.append(f"  {key:<20} {ground:<15} {group:<18} {px} px  {cells}")
        lines.append("== dark palettes, mixed Ready lead (bar 1.25)")
        seen = set()
        for r in results.values():
            for x in r["dark"]["mix"]:
                k = (x["ground"], x["tier"], x["px"], x["ready"], x["others"])
                if k in seen:
                    continue
                seen.add(k)
                if not x["pass"]:
                    lines.append(f"  under {x['ground']:<15} {x['tier']:<4} {x['px']} px Ready {x['ready']:<20} others {x['others']:<20} lead {x['lead']} (next {SHORT[x['next']]})")
        worst = min((x for r in results.values() for x in r["dark"]["mix"]), key=lambda x: x["lead"], default=None)
        if worst:
            lines.append(f"  weakest: {worst['ground']} {worst['tier']} {worst['px']} px Ready {worst['ready']} others {worst['others']} lead {worst['lead']}")
    return "\n".join(lines) + "\n"


# ================================================================ main

def destinations(m, root):
    dest_set = os.path.join(root, m["dest"])
    dest_atlas = os.path.join(root, m.get("atlasDest", m["dest"]))
    return dest_atlas, dest_set


def kit_dest(k, root):
    return os.path.join(root, k["dest"])


def build(manifests, kits, selected, root, out_dir, sheets=True):
    """Builds the selected sets and every kit into `root` (the repo, or a temp copy for --check). Every set is measured,
    and every set's faces in every kit, so each metrics.json carries the whole cross-set table and each kit's carries
    every set. Returns {set: metrics}, {kit: metrics}, the mixed-salience rows and the table."""
    chrome_ver = chrome_version()
    results, kit_results, atlases, faces, frames, groups_by = {}, {}, {}, {}, {}, {}
    with tempfile.TemporaryDirectory() as tmp:
        for m in manifests:
            if m["key"] in selected:
                dest_atlas, dest_set = destinations(m, root)
                atlases[m["key"]] = build_atlases(m, dest_atlas, dest_set, tmp)
                faces[m["key"]] = build_faces(m, dest_set, tmp)
            groups = measure_set(m, tmp)
            groups_by[m["key"]] = groups
            print(f"{m['key']}: rendered {', '.join(k for k in [*atlases.get(m['key'], {}), *faces.get(m['key'], {})] if not k.endswith('rects') and k != 'boxes') or 'nothing'}; "
                  f"measured {', '.join(g['name'] for g in groups)}")
        combos = {}
        for k in kits:
            frames[k["key"]] = build_frames(k, kit_dest(k, root), tmp)
            for m in manifests:
                if m["kit"] == k["key"]:
                    # A set in its own kit draws its composites (the plugin's fast path), so those are what is measured.
                    groups = groups_by[m["key"]]
                    light_gates, light = light_measures(groups)
                    combos[(m["key"], k["key"])] = (groups, gate_rows(m, groups) + light_gates, light)
                else:
                    combos[(m["key"], k["key"])] = measure_combo(m, k, tmp)
            print(f"{k['key']} kit: rendered frames, frames@2x, frames-row; measured every set's faces in it")
        neutral = {}
        for m in manifests:
            groups = combos[(m["key"], NEUTRAL_KIT)][0]
            neutral[m["key"]] = {"row": next(g for g in groups if g["name"] == "row"),
                                 "hero": next(g for g in groups if 48 in g["tiers"])}
        cross = cross_table(neutral)
        mix_sal = cross_salience(neutral)
        dark = {m["key"]: dark_record(groups_by[m["key"]]) for m in manifests}
        halos, dark_mixes = {}, []
        for ground in DARK_GROUNDS:
            halos[ground], mixes = dark_halo_for(ground, list(dark.values()), neutral)
            dark_mixes += mixes
        for m in (m for m in manifests if m["key"] in selected):
            groups = groups_by[m["key"]]
            light_gates, light = light_measures(groups)
            gates = (gate_rows(m, groups) + light_gates + dark_gate_rows(dark[m["key"]], halos)
                     + dark_mix_gate_rows(m["key"], dark_mixes)
                     + fit_rows(atlases[m["key"]]) + fit_parts(faces[m["key"]], "faces"))
            data = metrics_json(m, groups, gates, light, cross, mix_sal, chrome_ver, png_hashes(m, root),
                                dark[m["key"]], halos, dark_mixes)
            _, dest_set = destinations(m, root)
            os.makedirs(dest_set, exist_ok=True)
            write_json(data, os.path.join(dest_set, "metrics.json"), compact=True)
            results[m["key"]] = data
        for k in kits:
            data = kit_metrics_json(k, {m["key"]: combos[(m["key"], k["key"])] for m in manifests},
                                    {m["key"] for m in manifests if m["kit"] == k["key"]},
                                    fit_parts(frames[k["key"]], "frames"), chrome_ver, kit_png_hashes(k, root))
            write_json(data, os.path.join(kit_dest(k, root), "metrics.json"), compact=True)
            kit_results[k["key"]] = data
        write_text(checks_cs(kit_results), os.path.join(root, CHECKS_CS))
        if sheets and out_dir:
            os.makedirs(out_dir, exist_ok=True)
            for m in (m for m in manifests if m["key"] in selected):
                contact_sheet(m, atlases[m["key"]], groups_by[m["key"]], os.path.join(out_dir, f"{m['key']}-sheet.png"))
            if len(faces) == len(manifests):
                mix_sheet(manifests, kits, faces, frames, os.path.join(out_dir, "mix-sheet.png"))
            if cross:
                keys = [m["key"] for m in manifests]
                for which in ("row", "hero"):
                    cross_heatmap(cross, keys, os.path.join(out_dir, f"cross-{which}-16.png"), which, "16")
            with open(os.path.join(out_dir, "report.txt"), "w", encoding="utf-8") as fh:
                fh.write(report_text(results, kit_results, mix_sal, cross))
    return results, kit_results, mix_sal, cross


PART_FILES = ("{stem}.png", "{stem}@2x.png", "{stem}.json", "{stem}-row.png", "{stem}-row.json")


def outputs(m):
    dest_atlas, dest_set = destinations(m, "")
    files = [os.path.join(dest_atlas, n) for n in ("medals.png", "medals@2x.png", "medals.json")]
    if m.get("row", {}).get("atlas"):
        files += [os.path.join(dest_set, n) for n in ("row.png", "row.json")]
    files += [os.path.join(dest_set, n.format(stem="faces")) for n in PART_FILES]
    return files + [os.path.join(dest_set, "metrics.json")]


def kit_outputs(k):
    dest = kit_dest(k, "")
    return [os.path.join(dest, n.format(stem="frames")) for n in PART_FILES] + [os.path.join(dest, "metrics.json")]


def hashes(files, root):
    """{repo-relative path: SHA-256} of every PNG among `files`, as written under `root`."""
    out = {}
    for rel in files:
        if rel.endswith(".png"):
            with open(os.path.join(root, rel), "rb") as fh:
                out[rel.replace(os.sep, "/")] = hashlib.sha256(fh.read()).hexdigest()
    return out


def png_hashes(m, root):
    """{repo-relative path: SHA-256} of every PNG the set ships, as written under `root`."""
    return hashes(outputs(m), root)


def kit_png_hashes(k, root):
    return hashes(kit_outputs(k), root)


THEMES_DIR = os.path.join("Tsukimichi", "assets", "ui", "themes")


def extras(manifests, kits):
    """Files and folders under the themes and kits folders that no build writes: a stale plain.* or an old set's or
    kit's folder. Every folder is checked, whichever sets were selected."""
    expected = {os.path.normcase(os.path.normpath(rel)) for m in manifests for rel in outputs(m)}
    expected |= {os.path.normcase(os.path.normpath(rel)) for k in kits for rel in kit_outputs(k)}
    folders = {os.path.normcase(os.path.normpath(destinations(m, "")[1])) for m in manifests}
    folders |= {os.path.normcase(os.path.normpath(kit_dest(k, ""))) for k in kits}
    found = []
    for top in (THEMES_DIR, KITS_DEST):
        for dirpath, _, names in os.walk(os.path.join(REPO, top)):
            rel_dir = os.path.relpath(dirpath, REPO)
            if os.path.normcase(rel_dir) != os.path.normcase(top) and os.path.normcase(rel_dir) not in folders:
                found.append(rel_dir)
                continue
            found += [os.path.join(rel_dir, n) for n in names if os.path.normcase(os.path.join(rel_dir, n)) not in expected]
    return sorted(found)


def main():
    ap = argparse.ArgumentParser(description="The multi-theme build: atlases, faces and kits, per-set gates and the cross-set table.")
    ap.add_argument("--set", action="append", help="write only this set (repeatable); every set is still measured for the cross table, and every kit is written")
    ap.add_argument("--check", action="store_true", help="rebuild into a temp folder and diff against the repo")
    ap.add_argument("--out", default=os.path.join(tempfile.gettempdir(), "tsukimichi-themes"),
                    help="where contact sheets and report.txt go (never the repo)")
    ap.add_argument("--no-sheets", action="store_true")
    args = ap.parse_args()

    manifests = all_manifests()
    kits = all_kits()
    assert NEUTRAL_KIT in {k["key"] for k in kits}, f"the neutral kit {NEUTRAL_KIT} has no manifest"
    assert {m["kit"] for m in manifests} <= {k["key"] for k in kits}, "a set names a kit with no manifest"
    selected = set(args.set or [m["key"] for m in manifests])
    unknown = selected - {m["key"] for m in manifests}
    assert not unknown, f"no manifest for {sorted(unknown)}"

    if args.check:
        with tempfile.TemporaryDirectory() as root:
            results, kit_results, _, _ = build(manifests, kits, selected, root, None, sheets=False)
            stale = []
            files = [rel for m in manifests if m["key"] in selected for rel in outputs(m)]
            files += [rel for k in kits for rel in kit_outputs(k)] + [CHECKS_CS]
            for rel in files:
                new, old = os.path.join(root, rel), os.path.join(REPO, rel)
                same = os.path.exists(old) and _same(new, old)
                print(f"{'same ' if same else 'DIFF '} {rel.replace(os.sep, '/')}")
                if not same:
                    stale.append(rel)
            for rel in extras(manifests, kits):
                print(f"EXTRA {rel.replace(os.sep, '/')}  (no build writes it; delete it or add it to a manifest)")
                stale.append(rel)
        failed = [k for k, r in {**results, **kit_results}.items() if not r["pass"]]
        if failed:
            print(f"gates fail: {failed}")
        sys.exit(1 if stale or failed else 0)

    results, kit_results, mix_sal, cross = build(manifests, kits, selected, REPO, None if args.no_sheets else args.out,
                                                 sheets=not args.no_sheets)
    print(report_text(results, kit_results, mix_sal, cross))
    if not args.no_sheets:
        print(f"contact sheets and report: {args.out}")
    failed = [k for k, r in {**results, **kit_results}.items() if not r["pass"]]
    if failed:
        sys.exit(f"gates fail: {failed}")


def _same(a, b):
    if a.endswith(".json"):
        # JSON compares by content: git may check text files out with CRLF.
        with open(a, encoding="utf-8") as fa, open(b, encoding="utf-8") as fb:
            return json.load(fa) == json.load(fb)
    if a.endswith(".cs"):
        # Generated source compares by text: git may check it out with CRLF.
        with open(a, encoding="utf-8") as fa, open(b, encoding="utf-8") as fb:
            return fa.read().replace("\r\n", "\n") == fb.read().replace("\r\n", "\n")
    with open(a, "rb") as fa, open(b, "rb") as fb:
        return fa.read() == fb.read()


if __name__ == "__main__":
    main()
