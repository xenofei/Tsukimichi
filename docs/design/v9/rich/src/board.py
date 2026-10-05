"""Draws a level file's pegs and bricks over its scene, as the engine places them (docs/design/v9/rich).

Pieces come from the level JSON in file order (pegs, then bricks), which is the engine's order; their colours come from
the engine itself (tools/mfcheck `colours`), so a composite shows a board the game would really deal.
"""
import json
import math
import pathlib
import subprocess

import numpy as np

from rich_lib import RICH, blur, draw_brick, draw_moon, hexc, smooth

MFCHECK = RICH / "tools" / "mfcheck" / "bin" / "Release" / "net10.0" / "mfcheck.dll"


def engine_colours(level_path, number=5, seed=1):
    out = subprocess.run(["dotnet", str(MFCHECK), "colours", str(level_path), str(number), str(seed)],
                         capture_output=True, text=True, check=True).stdout
    return json.loads(out.strip().splitlines()[-1])


def sd_arc_capsule(xx, yy, cx, cy, R, start_deg, sweep_deg, half_t):
    """An arc brick as the engine shapes it: the band of the arc of radius R about (cx, cy) from start through sweep
    (degrees, 0 along +x, turning toward +y), `half_t` either side, with rounded ends."""
    a0 = math.radians(start_deg)
    sw = math.radians(sweep_deg)
    dx, dy = xx - cx, yy - cy
    ang = np.arctan2(dy, dx)
    rel = (ang - a0) % (2 * math.pi)
    on = rel <= sw
    rr = np.sqrt(dx * dx + dy * dy)
    d_band = np.abs(rr - R)
    e0 = (cx + R * math.cos(a0), cy + R * math.sin(a0))
    e1 = (cx + R * math.cos(a0 + sw), cy + R * math.sin(a0 + sw))
    d0 = np.sqrt((xx - e0[0]) ** 2 + (yy - e0[1]) ** 2)
    d1 = np.sqrt((xx - e1[0]) ** 2 + (yy - e1[1]) ** 2)
    return np.where(on, d_band, np.minimum(d0, d1)) - half_t


def sd_line_capsule(xx, yy, x1, y1, x2, y2, half_t):
    px, py, dx, dy = xx - x1, yy - y1, x2 - x1, y2 - y1
    t = np.clip((px * dx + py * dy) / max(dx * dx + dy * dy, 1e-6), 0, 1)
    return np.sqrt((px - dx * t) ** 2 + (py - dy * t) ** 2) - half_t


def pieces(level):
    """(kind, data) per piece in engine order."""
    out = [("peg", p) for p in level.get("pegs", [])]
    out += [("brick", b) for b in level.get("bricks", [])]
    return out


def mover_pos(p, t):
    """A mover's position t seconds in (the engine starts every mover at its file position at t = 0)."""
    m = p.get("move")
    if not m:
        return p["x"], p["y"]
    ph = 2 * math.pi * t / m["period"]
    if m["kind"] == "orbit":
        r = math.hypot(p["x"] - m["x"], p["y"] - m["y"])
        a = math.atan2(p["y"] - m["y"], p["x"] - m["x"]) + (ph if m.get("clockwise", True) else -ph)
        return m["x"] + r * math.cos(a), m["y"] + r * math.sin(a)
    s = (1 - math.cos(ph)) * 0.5
    return p["x"] + (m["x"] - p["x"]) * s, p["y"] + (m["y"] - p["y"]) * s


def draw_pieces(img, level, colours, lit=(), gone=(), sky="#141C3A", t=0.0, sky_at=None):
    """Draws every piece; `sky_at(x, y)` (optional) gives the scene's colour behind a peg for its earthshine."""
    for i, (kind, d) in enumerate(pieces(level)):
        if i in gone:
            continue
        col = colours[i]
        state = "lit" if i in lit else "unlit"
        if kind == "brick":
            ht = d.get("thickness", 20) / 2
            if d["kind"] == "line":
                f = lambda X, Y, d=d, ht=ht: sd_line_capsule(X, Y, d["x1"], d["y1"], d["x2"], d["y2"], ht)
                cx, cy = (d["x1"] + d["x2"]) / 2, (d["y1"] + d["y2"]) / 2
                rad = math.hypot(d["x2"] - d["x1"], d["y2"] - d["y1"]) / 2 + ht + 4
            else:
                f = lambda X, Y, d=d, ht=ht: sd_arc_capsule(X, Y, d["x"], d["y"], d["r"], d["start"], d["sweep"], ht)
                mid = math.radians(d["start"] + d["sweep"] / 2)
                cx, cy = d["x"] + d["r"] * math.cos(mid), d["y"] + d["r"] * math.sin(mid)
                rad = d["r"] * math.sin(min(math.radians(d["sweep"] / 2), math.pi / 2)) + ht + 6
                if d["sweep"] > 180:
                    cx, cy, rad = d["x"], d["y"], d["r"] + ht + 4
            draw_brick(img, f, (cx, cy, rad), col, state, variant=i)
        else:
            x, y = mover_pos(d, t)
            r = d.get("r", 10)
            sk = sky_at(x, y) if sky_at else sky
            draw_moon(img, x, y, r, col, state, variant=i % 4, sky=sk, rot=((i * 0.618034) % 1.0) * 1.2 - 0.6)


def layout_mask(level, W, H, S, t=0.0, grow=16.0):
    """Coverage of the layout (every piece grown by `grow` units), feathered: where the readability veil goes."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = (xx + 0.5) / S, (yy + 0.5) / S
    d = np.full((H, W), 1e9, np.float32)
    for kind, p in pieces(level):
        if kind == "brick":
            ht = p.get("thickness", 20) / 2
            if p["kind"] == "line":
                d = np.minimum(d, sd_line_capsule(X, Y, p["x1"], p["y1"], p["x2"], p["y2"], ht))
            else:
                d = np.minimum(d, sd_arc_capsule(X, Y, p["x"], p["y"], p["r"], p["start"], p["sweep"], ht))
        else:
            if p.get("move"):
                # a mover's whole path
                for k in range(24):
                    mx, my = mover_pos(p, p["move"]["period"] * k / 24)
                    d = np.minimum(d, np.sqrt((X - mx) ** 2 + (Y - my) ** 2) - p.get("r", 10))
            else:
                d = np.minimum(d, np.sqrt((X - p["x"]) ** 2 + (Y - p["y"]) ** 2) - p.get("r", 10))
    return smooth(grow, 0.0, d)


def veil(px, level, S, k=0.30, grow=18.0):
    """The readability veil: the scene dims by up to k behind and just round the layout (feathered over `grow`
    units), so every piece sits on a darker, calmer ground than the open scene. No outline, no shadow shape: the
    painting simply recedes where the play is."""
    H, W, _ = px.shape
    m = blur(layout_mask(level, W, H, S, grow=grow), 6 * S)
    return px * (1 - k * m)[..., None]


def load_level(path):
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8"))
