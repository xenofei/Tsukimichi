"""Option B: one painting per release, rendered in a distinct art treatment per theme (not only a grade).

Inputs, per release <key> (for example evercold-b), all in ../optionb/:
  <key>-base.png     the painting, 1120 x 440 (the 2x tier of the popup's 560 x 220 art band)
  <key>-masks.npz    its region masks, written by the release's painter (py -3 painters_b.py <key>);
                     keys: clouds, under, moon, moonlit, far, city, cliff, field, ridge, figs, lantern (0..1 floats)
  <key>.json         the release's placement data: horizon, sun glow, moon, the figures' zone, saddle bars, gold bands
Output: ../optionb/<key>-<theme>.png for the six themes (lossless masters; ship_option_b.py encodes the shipped files).

  classic             the painting, as painted
  medallion           an oil painting: a warm varnish grade, brush texture lit from the upper left, faint craquelure
                      in the thick paint, a deeper vignette, a slim gilt slip inside the popup's brass frame
  ishgard-glass       a stained-glass window whose lead follows the drawing: large sky and snow pieces cut along the
                      ridge and cloud edges, grisaille figures and spires, a white glass crescent, an amber lantern
  aether-crystal      cut moonstone over the sky and the far range: facets shaded as on blended domes lit from the
                      upper left, a faint adularescent sheen, faded out round the moon; the ground and figures clear
  astrologian-orrery  an engraved plate: silver ground, lapis enamel sky lighter at the dawn, dark line following each
                      contour, clouds as a few large cut shapes, brass only as inlay (crescent, limb, rete, dawn lines)
  sumi-to-kinpaku     sumi-e on washi: ink washes by depth, bare-paper snow, one continuous tapered dry-brush stroke,
                      a gold-leaf crescent, stepped genji-gumo gold bands, kirigane in the bands and corners, a seal

Every treatment keeps the painting's one natural light and its one warm practical light; none adds a light.
Run: py -3 option_b_themes.py [release key]     (default: evercold-b)
"""
import json
import math
import pathlib
import sys

import numpy as np
from PIL import Image, ImageDraw

from artlib import blur, fbm, hexc

ART = pathlib.Path(__file__).resolve().parent.parent
OUT = ART / "optionb"
LUM = np.array([0.2126, 0.7152, 0.0722], np.float32)
THEMES = ["classic", "medallion", "ishgard-glass", "aether-crystal", "astrologian-orrery", "sumi-to-kinpaku"]
CFG = {}


def load(key):
    CFG.clear()
    CFG.update(json.loads((OUT / f"{key}.json").read_text(encoding="utf-8")))
    CFG["key"] = key
    return np.asarray(Image.open(OUT / f"{key}-base.png").convert("RGB"), np.float32) / 255.0


def lum(px):
    return px @ LUM


def label_mean(px, labels, n):
    flat = labels.ravel()
    cnt = np.bincount(flat, minlength=n).astype(np.float32)
    out = np.stack([np.bincount(flat, weights=px[..., k].ravel(), minlength=n) for k in range(3)], -1) / np.maximum(cnt, 1)[:, None]
    return out.astype(np.float32), cnt


def edges(labels):
    e = np.zeros(labels.shape, bool)
    e[:, 1:] |= labels[:, 1:] != labels[:, :-1]
    e[1:, :] |= labels[1:, :] != labels[:-1, :]
    return e


def outline(m):
    """Where a soft mask crosses one half, without wrapping round the image's edges."""
    b = m > 0.5
    e = np.zeros(b.shape, bool)
    e[:, 1:] |= b[:, 1:] != b[:, :-1]
    e[1:, :] |= b[1:, :] != b[:-1, :]
    return e.astype(np.float32)


def highpass(L, s):
    return L - blur(L, s)


def tex(T, u, v):
    th, tw = T.shape
    u = np.mod(u, tw - 1)
    v = np.mod(v, th - 1)
    u0, v0 = np.floor(u).astype(int), np.floor(v).astype(int)
    fu, fv = u - u0, v - v0
    return (T[v0, u0] * (1 - fu) + T[v0, u0 + 1] * fu) * (1 - fv) + (T[v0 + 1, u0] * (1 - fu) + T[v0 + 1, u0 + 1] * fu) * fv


def masks():
    M = dict(np.load(OUT / f"{CFG['key']}-masks.npz"))
    M["below"] = np.maximum(M["far"], M["city"])
    M["sky"] = 1 - M["below"]
    M["city"] = M["city"] * (1 - M["field"]) * (1 - M["ridge"])   # the city and its bluff only, not the plain under it
    # clouds as a few large shapes: no islands or holes smaller than a lead piece or an engraved shape could carry
    M["cloudshape"] = (blur(M["clouds"], 7) > 0.42).astype(np.float32)
    return M


def top_row(m):
    on = m > 0.5
    return np.where(on.any(0), on.argmax(0), m.shape[0]).astype(np.float32)


# Optional per-release keys added for the 1.14-1.18 backfill (absent: every treatment behaves exactly as before):
#   far_layers  mask names in <key>-masks.npz, farthest first, whose union is `far`: each range is cut, engraved,
#               inked and brushed along its own ridgeline, not only the top one
#   fig_splits  x values (px at 1120) where the glass cuts the figures' mask into pieces (one piece per stone)
#   fig_tones   the figures differ in value (stones of different stone): engraved and inked by value, not solid
#   glass_true_colour  glass region ids (6 = the figures) whose pieces keep their own painted colour, not a palette
#   lines       mask names of thin linear things (a guide rope, rigging) that every treatment keeps as a line
#   field_flat  the field is a flat sea or river: its strips, lines and strokes run from the horizon in every column,
#               and only its dark marks are drawn (glass and ink), so a moon's glitter path stays light
#   orrery_crisp  the engraving's lines are cut crisp, unblurred, so fine hatching on dark night masses still reads
#   glass_far_strips  with far_layers, each range is cut in strips along its own ridgeline, with no vertical joins
#   aether_clear_ground  no facets on the field or the ridge, and they fade out just above the horizon
#   orrery_moon "engraved": the moon is a brass ring with its seas engraved and the unlit part in dark enamel,
#               for a near-full moon (a flat brass disc reads as the sun or a coin)
def far_tops(M):
    """(layer mask, its top row) for each far range: the json's far_layers, or `far` alone."""
    names = CFG.get("far_layers")
    if not names:
        return [(M["far"], top_row(M["far"]))]
    return [(M[n], top_row(M[n])) for n in names]


def far_phase(M, yy):
    """yy minus the ridgeline of the range each pixel belongs to (the nearest range wins)."""
    v = yy - top_row(M["far"])[None, :]
    if CFG.get("far_layers"):
        for m, t in far_tops(M):
            v = np.where(m > 0.5, yy - t[None, :], v)
    return v


def field_top(M):
    """The field's top row; with field_flat, the horizon in every column (a flat sea or river, so a boat or a
    boathouse standing in it never restarts its strips, lines or strokes)."""
    if CFG.get("field_flat"):
        return np.full(M["field"].shape[1], CFG["horizon"] * M["field"].shape[0], np.float32)
    return top_row(M["field"])


def lines_mask(M):
    out = np.zeros_like(M["far"])
    for n in CFG.get("lines", []):
        out = np.maximum(out, M[n])
    return out


def grid(h, w):
    return np.mgrid[0:h, 0:w].astype(np.float32)


# ---------------------------------------------------------------------------------------------------------- medallion
def medallion(px):
    """Visibly oil, not the plain painting: a warm amber varnish, brush strokes that follow the forms (along the ridge
    and the plain, down the cliff faces, round the cloud edges) lit from the upper left, faint craquelure only in the
    thick paint, a deeper vignette inside a slim gilt slip. The silhouettes and the moon are left out of the brush and
    varnish pass, and the crescent is redrawn after it in its own cream. The popup drops its art keyline here."""
    h, w, _ = px.shape
    M = masks()
    yy, xx = grid(h, w)
    from paint_option_b import kuwahara
    L0 = lum(px)
    mz = np.clip(blur(M["moon"], 2) * 2.5, 0, 1)
    sil = np.maximum(blur(np.maximum(M["figs"], M["city"] * (yy < h * CFG["bluff_split"])), 2), mz)
    if CFG.get("lines"):
        sil = np.maximum(sil, blur(lines_mask(M), 1.5) * 2).clip(0, 1)
    p = kuwahara(px, 3) * (1 - sil[..., None]) + px * sil[..., None]
    L = lum(p)[..., None]
    p = p * np.array([1.06, 1.0, 0.84], np.float32)
    p = 1 - (1 - p) * (1 - np.array([0.16, 0.10, 0.02], np.float32) * np.clip(L * 1.2, 0, 1))
    Lv = lum(p)[..., None]
    p = np.clip(Lv + (p - Lv) * 1.10, 0, 1)
    # brush coordinates that follow the forms: v runs across a form's contour, u along it
    rt, ft, at = top_row(M["ridge"]), field_top(M), top_row(M["far"])
    v = yy.copy()
    v = np.where(M["far"] > 0.5, far_phase(M, yy) if CFG.get("far_layers") else yy - at[None, :], v)
    v = np.where(M["field"] > 0.5, yy - ft[None, :], v)
    v = np.where(M["ridge"] > 0.5, yy - rt[None, :], v)
    u = xx.copy()
    cliff = (M["city"] > 0.5) & (yy > h * CFG["bluff_split"])
    u, v = np.where(cliff, yy, u), np.where(cliff, xx, v)                         # down the cliff faces
    cd = blur(M["clouds"], 3) * (M["sky"] > 0.5)
    v = np.where(cd > 0.05, yy + cd * 14, v)                                       # bending round the cloud edges
    stroke = tex(fbm(256, 256, 4, 3, 811), u / 6.0, v / 1.6)
    relief = (np.roll(stroke, 1, 0) - stroke) + (np.roll(stroke, 1, 1) - stroke)
    paint = (1 - sil) * (0.5 + 0.5 * np.clip(L0 / 0.5, 0, 1))
    p = np.clip(p * (1 + (relief * 0.75 + (stroke - 0.5) * 0.05) * paint)[..., None], 0, 1)
    cr = fbm(h, w, 14, 4, 801)
    thick = np.clip((L0 - 0.30) / 0.3, 0, 1) * (1 - np.clip(sil * 3, 0, 1))
    p = p * (1 - (np.exp(-((cr - 0.5) / 0.010) ** 2) * 0.035 * thick * (0.6 + 0.8 * fbm(h, w, 60, 2, 805)))[..., None])
    ex = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))
    rad = np.sqrt(((xx - w / 2) / (w * 0.62)) ** 2 + ((yy - h / 2) / (h * 0.75)) ** 2)
    p = p * (1 - 0.30 * np.clip(rad - 0.55, 0, 1) ** 1.5 - 0.14 * np.clip(1 - ex / 40, 0, 1) ** 2)[..., None]
    # the moon after the pass: the painting's own disc, with at most a light share of the varnish, and the crescent in cream
    moon_col = px * 0.88 + p * 0.12
    p = p * (1 - mz[..., None]) + moon_col * mz[..., None]
    lit = blur(M["moonlit"], 0.5)[..., None]
    p = p * (1 - lit) + (hexc("#F3EAD3") * 0.9 + hexc("#E9C98C") * 0.1) * lit
    inset, wdt = 5, 4
    slip = (ex >= inset) & (ex < inset + wdt)
    litside = np.stack([yy, xx, h - 1 - yy, w - 1 - xx]).argmin(0) < 2
    across = np.clip((ex - inset) / wdt, 0, 1)[..., None]
    hi = hexc("#F0D9A0") * (1 - across * 0.35) + hexc("#C9A65C") * (across * 0.35)
    lo = hexc("#7C6236") * (1 - across * 0.4) + hexc("#5C4724") * (across * 0.4)
    p = np.where(slip[..., None], np.where(litside[..., None], hi, lo), p)
    p = np.where(((ex >= inset + wdt) & (ex < inset + wdt + 1))[..., None], p * 0.55, p)
    p = np.where((ex < inset)[..., None], p * 0.6 + hexc("#1E2236") * 0.4, p)
    return p


# ---------------------------------------------------------------------------------------------------- ishgard glass
def jitter_labels(h, w, cell, seed):
    rng = np.random.default_rng(seed)
    gh, gw = int(h / cell) + 2, int(w / cell) + 2
    sx = (np.arange(gw)[None, :] + 0.15 + 0.7 * rng.random((gh, gw))) * cell
    sy = (np.arange(gh)[:, None] + 0.15 + 0.7 * rng.random((gh, gw))) * cell
    yy, xx = grid(h, w)
    cy, cx = (yy // cell).astype(int), (xx // cell).astype(int)
    best = np.full((h, w), 1e9, np.float32)
    lab = np.zeros((h, w), np.int64)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            ny, nx = np.clip(cy + dy, 0, gh - 1), np.clip(cx + dx, 0, gw - 1)
            dd = (xx - sx[ny, nx]) ** 2 + (yy - sy[ny, nx]) ** 2
            take = dd < best
            best = np.where(take, dd, best)
            lab = np.where(take, ny * gw + nx, lab)
    return lab, gh * gw


def figure_zone(M, margin=24):
    """The x range no vertical glass join may cross: the figures' mask plus a margin, joined with the json's zone."""
    cols = np.where((M["figs"] > 0.3).any(0))[0]
    z0, z1 = CFG.get("figure_zone", [10 ** 6, -1])
    if len(cols):
        z0, z1 = min(z0, cols.min() - margin), max(z1, cols.max() + margin)
    CFG["_zone"] = (z0, z1)


def no_cut_x(xx):
    """Vertical joins never fall inside the figures' zone: x is held at the zone's left edge across it, and runs on
    from there past it, so no join is forced at the zone's right edge either."""
    z0, z1 = CFG["_zone"]
    return np.where(xx <= z0, xx, np.where(xx < z1, z0, xx - (z1 - z0)))


def opening(m, r):
    """A morphological opening (erode, then dilate) by about r px: drops any shape narrower than 2r, keeps the rest."""
    er = (blur(m, r / 2) > 0.92).astype(np.float32)
    return ((blur(er, r / 2) > 0.35) & (m > 0.5)).astype(np.float32)


def merge_small(lab, min_px=300, frozen=None):
    """Merges every piece smaller than min_px into a neighbouring piece, so no lead island or loop survives."""
    for _ in range(40):
        cnt = np.bincount(lab.ravel())
        small = (cnt[lab] < min_px) & (True if frozen is None else ~frozen)
        if not small.any():
            break
        for dy, dx in ((0, -1), (-1, 0), (0, 1), (1, 0)):
            sh = np.roll(np.roll(lab, dy, 0), dx, 1)
            take = small & (cnt[sh] >= min_px) & (True if frozen is None else ~np.roll(np.roll(frozen, dy, 0), dx, 1))
            lab = np.where(take, sh, lab)
            small = small & ~take
    return lab


def contour_strips(h, w, top, seed, k_band, seg0, seg_k):
    """Pieces cut along a contour: bands below `top`, each cut into long strips, so the lead runs parallel to the
    ridge or the horizon, never as paving; no join falls inside the figures' zone."""
    yy, xx = grid(h, w)
    rng = np.random.default_rng(seed)
    wob = np.interp(np.arange(w), np.linspace(0, w, 12), rng.random(12) * 0.6)[None, :]
    band = np.floor(np.sqrt(np.clip(yy - top[None, :], 0, None)) * k_band + wob).astype(np.int64)
    seg = np.floor((no_cut_x(xx) + (band * 97) % 211 + 12 * np.sin(yy / 23.0)) / (seg0 + seg_k * band)).astype(np.int64)
    return band * 1000 + seg


SUB = {0: ["#14235E", "#1B2F78", "#2D4AA6", "#5A5AA8", "#A86A86", "#E3A26A"], 1: ["#3E3884", "#5A4F98", "#8A5A9A", "#C07080", "#E6A872"],
       2: ["#3A3478", "#5A4F98", "#7D78BC"], 3: ["#1E1A3C", "#2A2452", "#40356F"], 4: ["#8A9AD0", "#B3C2EA", "#D4DDF4", "#EEF2FC"],
       5: ["#5868A8", "#8496D0", "#B1C0EA", "#DCE4F6"], 6: ["#121426"], 7: ["#1C2A66"], 8: ["#F4EEDC"], 9: ["#E8A040"]}


def ishgard(px):
    h, w, _ = px.shape
    M = masks()
    yy, xx = grid(h, w)
    figure_zone(M)
    reg = np.zeros((h, w), np.int64)
    reg[(M["sky"] > 0.5) & (opening(M["cloudshape"], 12) > 0.5)] = 1
    reg[M["far"] > 0.5] = 2
    reg[M["city"] > 0.5] = 3
    reg[M["field"] > 0.5] = 4
    reg[M["ridge"] > 0.5] = 5
    reg[M["figs"] > 0.5] = 6
    reg[M["moon"] > 0.5] = 7
    reg[M["moonlit"] > 0.4] = 8
    reg[blur(M["lantern"], 1.5) > 0.25] = 9
    sky_l, _ = jitter_labels(h, w, 48, 901)
    cl_l, _ = jitter_labels(h, w, 44, 902)
    far_l = np.floor((no_cut_x(xx) + 18 * np.sin(yy / 41.0)) / 150).astype(np.int64)   # gently curved joins, never loops
    city_l = np.where(yy > h * CFG["bluff_split"], np.floor(xx / 90).astype(np.int64), 100 + np.floor(xx / 70).astype(np.int64))
    if CFG.get("far_layers"):                                       # each range its own pieces, the lead on its ridgeline
        for i, (m, _t) in enumerate(far_tops(M)):
            if CFG.get("glass_far_strips"):                       # strips along each ridgeline, no vertical joins
                far_l = np.where(m > 0.5, (i + 1) * 1_000_000 + contour_strips(h, w, _t, 907 + i, 0.9, 240, 40), far_l)
            else:
                far_l = np.where(m > 0.5, (i + 1) * 1000 + far_l, far_l)
    fig_l = (xx > CFG["figure_split"]).astype(np.int64)
    if CFG.get("fig_splits"):                                       # one piece per stone
        fig_l = np.digitize(xx, CFG["fig_splits"]).astype(np.int64)
    parts = {0: sky_l, 1: cl_l, 2: far_l, 3: city_l,
             4: contour_strips(h, w, field_top(M), 904, 1.0, 150, 30), 5: contour_strips(h, w, top_row(M["ridge"]), 905, 0.8, 230, 40),
             6: fig_l}
    lab = reg * 10_000_000
    for rid, l in parts.items():
        lab = np.where(reg == rid, rid * 10_000_000 + l, lab)
    uniq, lab = np.unique(lab, return_inverse=True)
    lab = lab.reshape(h, w)
    keep_small = np.isin(reg, [6, 7, 8, 9])                     # the figures, the moon and the lantern stay as drawn
    merged = merge_small(lab, frozen=keep_small)
    lab = np.where(keep_small, lab, merged)
    uniq2, lab = np.unique(lab, return_inverse=True)
    lab = lab.reshape(h, w)
    regc = (uniq[uniq2] // 10_000_000).astype(int)
    col, cnt = label_mean(px, lab, len(uniq2))
    cl = col @ LUM
    sub = dict(SUB)
    sub.update({int(k): v for k, v in CFG.get("glass_palette", {}).items()})   # per-release ground glass
    for rid, hexes in sub.items():
        if rid in CFG.get("glass_true_colour", []):                 # each piece keeps its own stone's colour
            continue
        pal = np.stack([hexc(x) for x in hexes])
        sel = (regc == rid) & (cnt > 0)
        if sel.any():
            lo, hi = np.percentile(cl[sel], 5), np.percentile(cl[sel], 95)
            t = np.clip((cl[sel] - lo) / max(hi - lo, 1e-3), 0, 0.999) * len(hexes)
            col[sel] = col[sel] * 0.2 + pal[t.astype(int)] * 0.8
    glass = col[lab]
    streak = tex(fbm(256, 256, 14, 3, 906), xx / 3.0, yy / 0.8)
    glass = glass * (0.92 + 0.14 * streak)[..., None]
    glass = glass * 0.82 + blur(px, 6) * 0.18 * (reg <= 1)[..., None] + glass * 0.18 * (reg > 1)[..., None]
    mx, my, mr = CFG["moon"][0] * w, CFG["moon"][1] * h, CFG["moon"][2]
    toward = np.clip(((mx - xx) * 0.7 + (yy - my) * 0.7) / mr * 0.5 + 0.5, 0, 1)
    glass = np.where((reg == 8)[..., None], hexc("#E8E2CC") + (hexc("#FFFBEE") - hexc("#E8E2CC")) * toward[..., None], glass)
    glass = 1 - (1 - glass) * 0.9
    glass = 1 - (1 - glass) * (1 - np.array([1.0, 0.92, 0.84], np.float32) * (blur(np.clip(lum(glass) - 0.6, 0, 1), 4) * 0.25)[..., None])
    L = lum(px)
    gris = np.clip(-highpass(L, 2.0) * 3.5, 0, 0.75) * (np.maximum(M["city"], M["figs"]) > 0.3)
    ground = np.maximum(M["field"], M["ridge"]) * (1 - M["figs"])                 # paths and roads, painted lightly on the ground glass
    gl = -highpass(L, 3.0) if CFG.get("field_flat") else np.abs(highpass(L, 3.0))   # on water, only dark marks: glitter stays light
    gris = np.maximum(gris, np.clip(gl * 5 - 0.08, 0, 0.55) * ground)
    glass = glass * (1 - gris[..., None] * 0.8)
    came = np.clip(blur(edges(lab).astype(np.float32), 0.6) * 2.2, 0, 1)
    glass = glass * (1 - came[..., None]) + hexc("#202430") * came[..., None]
    if CFG.get("lines"):                                            # a rope or rigging as a came of its own
        came = np.maximum(came, np.clip(blur(lines_mask(M), 0.5) * 2.5, 0, 1))
        glass = glass * (1 - came[..., None]) + hexc("#202430") * came[..., None]
    hl = np.clip(came - np.roll(np.roll(came, 1, 0), 1, 1), 0, 1)
    glass = glass + (hexc("#8C95B0") - glass) * (hl * 0.30)[..., None]
    for y in CFG["glass_bars"]:
        glass[y - 2:y + 2] = glass[y - 2:y + 2] * 0.15 + hexc("#1A1C24") * 0.85
        glass[y - 2] = glass[y - 2] * 0.6 + hexc("#6A7088") * 0.4
    return glass


# ---------------------------------------------------------------------------------------------------- aether crystal
def aether(px):
    h, w, _ = px.shape
    M = masks()
    yy, xx = grid(h, w)
    rng = np.random.default_rng(1001)
    cell = 30
    gh, gw = int(h / cell) + 2, int(w / cell) + 2
    vx = (np.arange(gw)[None, :] + 0.6 * (rng.random((gh, gw)) - 0.5)) * cell
    vy = (np.arange(gh)[:, None] + 0.6 * (rng.random((gh, gw)) - 0.5)) * cell
    im = Image.new("I", (w, h), 0)
    d = ImageDraw.Draw(im)
    n = 0
    cents = [(0.0, 0.0)]
    for j in range(gh - 1):
        for i in range(gw - 1):
            a, b, c_, dd = (vx[j, i], vy[j, i]), (vx[j, i + 1], vy[j, i + 1]), (vx[j + 1, i], vy[j + 1, i]), (vx[j + 1, i + 1], vy[j + 1, i + 1])
            for t in (((a, b, dd), (a, dd, c_)) if (i + j) % 2 else ((a, b, c_), (b, dd, c_))):
                n += 1
                d.polygon(t, fill=n)
                cents.append((sum(q[0] for q in t) / 3, sum(q[1] for q in t) / 3))
    lab = np.asarray(im, np.int64)
    cents = np.array(cents, np.float32)
    col, _ = label_mean(px, lab, n + 1)
    # blended domes: each facet's normal is a softly weighted sum over every dome, so no straight seam crosses the sky
    domes = np.array([[x * w, y * h] for x, y in CFG["aether_domes"]], np.float32)
    dist2 = ((cents[:, None, :] - domes[None, :, :]) ** 2).sum(-1)
    wts = np.exp(-(dist2 - dist2.min(1, keepdims=True)) / (2 * (0.16 * w) ** 2))
    wts = wts / wts.sum(1, keepdims=True)
    nrm = ((cents[:, None, :] - domes[None, :, :]) * wts[..., None]).sum(1) / (w * 0.18)
    shade = np.clip(-(nrm[:, 0] * 0.7071 + nrm[:, 1] * 0.7071), -1, 1)
    area = np.clip(np.maximum(M["sky"], M["far"] * (1 - M["field"])) - np.maximum(M["city"], M["figs"]), 0, 1)
    if CFG.get("aether_clear_ground"):                                           # no facets on the ground, nor along the horizon
        area = area * (1 - np.clip(np.maximum(M["field"], M["ridge"]) * 2, 0, 1))
        area = area * np.clip((CFG["horizon"] * h - yy) / (0.07 * h), 0, 1)
    mx, my, mr = CFG["moon"][0] * w, CFG["moon"][1] * h, CFG["moon"][2]
    dm = np.sqrt((xx - mx) ** 2 + (yy - my) ** 2)
    fade = np.clip((dm - mr * 1.2) / (mr * 3.0), 0, 1)
    near_figs = np.clip(blur(M["figs"], 12) * 5, 0, 1)                          # and within about 30 px of the figures
    if CFG.get("lines"):
        near_figs = np.maximum(near_figs, np.clip(blur(lines_mask(M), 4) * 5, 0, 1))
    area = blur(area, 1.0) * (fade * fade * (3 - 2 * fade)) * (1 - near_figs)
    mix = (0.6 * area)[..., None]
    p = px * (1 - mix) + col[lab] * mix
    p = np.clip(p * (1 + 0.12 * shade[lab] * area)[..., None], 0, 1)
    e = blur(edges(lab).astype(np.float32), 0.5) * area
    up = np.clip(e - np.roll(np.roll(e, 1, 0), 1, 1), 0, 1)
    p = 1 - (1 - p) * (1 - hexc("#CFF3FF") * (e * 0.10 + up * 0.26)[..., None])
    sheen = np.exp(-(((xx - 0.36 * w) * 0.5 + (yy - 0.18 * h)) / (0.12 * h)) ** 2) * np.exp(-((xx - 0.36 * w) / (0.35 * w)) ** 2)
    p = 1 - (1 - p) * (1 - hexc("#A8C8FF") * (sheen * area * 0.14)[..., None])
    return p * np.array([0.96, 1.0, 1.04], np.float32)


# ---------------------------------------------------------------------------------------------------- orrery plate
def orrery(px):
    h, w, _ = px.shape
    M = masks()
    L = lum(px)
    yy, xx = grid(h, w)
    hz = CFG["horizon"]
    sky = np.clip(M["sky"] - M["moon"], 0, 1)
    sheen = np.clip(1 - (xx / w * 0.6 + yy / h * 0.4), 0, 1)
    silver = hexc("#8E94A4")[None, None, :] + (hexc("#E4E7EE") - hexc("#8E94A4"))[None, None, :] * (0.45 + 0.5 * sheen)[..., None]
    enamel = hexc("#16245A")[None, None, :] + (hexc("#33498A") - hexc("#16245A"))[None, None, :] * np.clip(yy / (h * hz), 0, 1)[..., None]
    # the dawn: the enamel lightens low behind the city, toward the sun's glow
    gx, gy = CFG["sun_glow"][0] * w, CFG["sun_glow"][1] * h
    dawn = np.exp(-((yy - gy) / (0.11 * h)) ** 2) * np.exp(-((xx - gx) / (0.30 * w)) ** 2)
    enamel = enamel + (hexc("#7A86C0") - enamel) * (dawn * CFG.get("dawn", 0.75))[..., None]
    plate = silver * (1 - sky[..., None]) + enamel * sky[..., None]
    dark = np.clip(1.05 - L * 1.9, 0, 1)

    def lines(phase, spacing, weight):
        f = np.abs((phase / spacing) % 1.0 - 0.5) * 2
        cutl = ((1 - f) < np.minimum(weight, 0.55) * 0.9).astype(np.float32)          # never so wide they merge
        return cutl if CFG.get("orrery_crisp") else blur(cutl, 0.45)

    cut = np.zeros((h, w), np.float32)
    ridge, field, far = M["ridge"], M["field"] * (1 - M["ridge"]), M["far"] * (1 - M["field"]) * (1 - M["city"])
    cliff = M["city"] * (yy > h * CFG["bluff_split"])
    bld = M["city"] * (yy <= h * CFG["bluff_split"])
    rt, ft, at = top_row(M["ridge"]), field_top(M), top_row(M["far"])
    cut += ridge * lines(yy - rt[None, :], 5.0, 0.25 + 0.6 * dark)
    cut += field * lines(np.sqrt(np.clip(yy - ft[None, :], 0, None)) * 6.0, 1.6, 0.20 + 0.6 * dark)
    cut += far * lines(far_phase(M, yy) if CFG.get("far_layers") else yy - at[None, :], 4.0, 0.25 + 0.5 * dark)
    cut += cliff * np.maximum(lines(xx + 3 * np.sin(yy / 9), 4.0, 0.5 + 0.45 * dark), lines(yy, 11.0, 0.15))
    dense = lambda ph: blur((np.abs((ph / 3.0) % 1.0 - 0.5) * 2 > 0.25).astype(np.float32), 0.45)
    cut += bld * np.maximum(dense(xx + yy), dense(xx - yy))                       # the backlit city: dense cross-hatching
    shadow = np.clip((0.42 - L) * 3, 0, 1) * (ridge + field + far) * (1 - M["figs"])
    cut += shadow * lines(xx - yy, 4.0, 0.35)                                     # every shadow as cross-hatching
    cut = np.clip(cut, 0, 1) * (1 - sky)
    if CFG.get("fig_tones"):                                                       # stones of different stone: hatched by value
        tone = np.clip((0.66 - L) * 2.2, 0, 1)
        crisp = lambda ph, wdt: (np.abs((ph / 5.0) % 1.0 - 0.5) * 2 < wdt).astype(np.float32)   # unblurred, so a 5 px hatch stays a hatch
        fh = np.maximum(crisp(xx - yy, 0.12 + 0.30 * tone), crisp(xx + yy, 0.30 * np.clip(tone * 1.6 - 0.6, 0, 1)) * (tone > 0.4)) * 0.85
        cut = cut * (1 - M["figs"]) + M["figs"] * fh
    else:
        cut = np.maximum(cut, M["figs"])
    if CFG.get("lines"):
        cut = np.maximum(cut, np.clip(lines_mask(M) * 1.6, 0, 1))
    plate = plate * (1 - cut[..., None]) + hexc("#2A2E3B") * cut[..., None]
    lip = np.clip(cut - np.roll(cut, 1, 0), 0, 1) * (1 - M["figs"])
    lip[0] = 0
    plate = plate + (hexc("#F4F6FA") - plate) * (lip * 0.25)[..., None]
    for k in ("far", "ridge", "field", "figs", "city"):
        plate = plate * (1 - (outline(M[k]) * 0.6)[..., None])
    if CFG.get("far_layers"):                                                      # each range's own ridgeline
        for m, _t in far_tops(M):
            plate = plate * (1 - (outline(m) * (1 - M["figs"]) * (1 - M["city"]) * 0.6)[..., None])
    # the clouds: a few large shapes, each one clean silver outline, a few parallel lines along its lit underside
    cs = opening((blur(M["clouds"], 10) > 0.33).astype(np.float32) * sky, 20)   # no cloud under about 40 px across
    # a flat, lit base: each column's lowest cloud row, levelled over a wide span, cuts the shape off below it
    on = cs > 0.5
    bottom = np.where(on.any(0), h - 1 - np.argmax(on[::-1], 0), -1).astype(np.float32)
    span = 70
    lv = np.array([bottom[max(0, i - span):i + span][bottom[max(0, i - span):i + span] >= 0].min() if (bottom[max(0, i - span):i + span] >= 0).any() else -1 for i in range(w)], np.float32)
    lv = np.where(bottom >= 0, np.minimum(bottom, lv + 4), -1)
    cs = (on & (yy <= lv[None, :])).astype(np.float32)
    plate = plate + (hexc("#4A5A98") - plate) * (cs * 0.45)[..., None]            # a lighter enamel inside each shape
    co = outline(cs)
    ul = np.zeros((h, w), np.float32)
    for k in (3, 6, 9):                                                           # 2-3 cut lines along the lit base
        ul = np.maximum(ul, (np.abs(yy - (lv[None, :] - k)) < 0.7) * cs * (k < 9 or 1))
    plate = plate + (hexc("#C9D0E0") - plate) * (np.clip(blur(co, 0.5) * 1.6 + ul * 0.8, 0, 1) * 0.75)[..., None]
    stars = np.clip((L - blur(L, 3)) * 8 - 0.4, 0, 1) * sky * (yy < h * 0.4) * (1 - cs)
    plate = plate + (hexc("#E4E7EE") - plate) * (stars * 0.9)[..., None]
    # brass inlay: the dawn as three short brass lines low behind the city, the crescent, the rete and the limb
    dl_ = lines(yy - gy, 5.0, 0.3) * (np.abs(yy - gy - 0.02 * h) < 0.05 * h) * np.exp(-((xx - gx) / (0.22 * w)) ** 2) * sky
    dl_ = dl_ * (1.0 if CFG.get("dawn_lines", True) else 0.0)                      # no dawn lines in a moonlit night
    plate = plate + (hexc("#D9B86E") - plate) * (dl_ * 0.55)[..., None]
    plate = plate * (1 - M["moon"][..., None]) + hexc("#1E2D66") * M["moon"][..., None]
    if CFG.get("orrery_moon") == "engraved":
        # a near-full moon: a silver face with its seas engraved as hatching, the unlit sliver left in dark enamel, and
        # a brass ring round the whole disc (a flat brass disc would read as the sun or a coin)
        lit = M["moonlit"]
        Lm = np.where(lit > 0.5, L, np.nan)
        hi_ = np.nanpercentile(Lm, 92) if np.isfinite(Lm).any() else 1.0
        sea = np.clip((hi_ - L) / 0.22, 0, 1) * (lit > 0.5)
        face = hexc("#DCDFE6") * (1 - sea[..., None] * 0.15)
        plate = plate * (1 - lit[..., None]) + face * lit[..., None]
        hatch = (np.abs(((xx - yy) / 3.0) % 1.0 - 0.5) * 2 < 0.25 + 0.35 * sea).astype(np.float32) * np.clip(sea * 3 - 0.3, 0, 1) * lit
        plate = plate * (1 - (hatch * 0.75)[..., None]) + hexc("#4A5066") * (hatch * 0.75)[..., None]
        ring = np.clip(blur(outline(M["moon"]), 0.6) * 1.8, 0, 1)
        plate = plate * (1 - ring[..., None]) + hexc("#D9B86E") * ring[..., None]
    else:
        plate = plate * (1 - M["moonlit"][..., None]) + hexc("#E6CC90") * M["moonlit"][..., None]
    im = Image.new("L", (w * 2, h * 2), 0)
    dr = ImageDraw.Draw(im)
    cx, cy = w * 1.0, h * 2.8
    for k in range(5):
        r = (h * 1.45 + k * h * 0.2) * 2
        dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=255, width=2)
    for a in range(-48, 49, 24):
        t = math.radians(a - 90)
        dr.line([(cx, cy), (cx + math.cos(t) * h * 6, cy + math.sin(t) * h * 6)], fill=150, width=2)
    rete = np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255.0
    rete = rete * (1 - np.maximum(M["figs"], blur(M["moon"], 2) * 2).clip(0, 1))
    plate = plate + (hexc("#D9B86E") - plate) * (rete * 0.20)[..., None]
    limb = Image.new("L", (w * 2, h * 2), 0)
    dl = ImageDraw.Draw(limb)
    for i in range(0, w * 2, 24):
        tl = 18 if (i // 24) % 5 == 0 else 9
        dl.line([(i, h * 2 - 4), (i, h * 2 - 4 - tl)], fill=255, width=2)
        dl.line([(i, 4), (i, 4 + tl)], fill=255, width=2)
    dl.line([(0, h * 2 - 26), (w * 2, h * 2 - 26)], fill=255, width=2)
    dl.line([(0, 26), (w * 2, 26)], fill=255, width=2)
    limb = np.asarray(limb.resize((w, h), Image.LANCZOS), np.float32) / 255.0
    plate = plate * (1 - limb[..., None] * 0.8) + hexc("#D9B86E") * (limb[..., None] * 0.8)
    lan = blur(M["lantern"], 0.8)[..., None]                                      # the practical light, a warm brass inlay
    plate = plate * (1 - lan) + hexc("#E8C47A") * lan
    return plate


# ---------------------------------------------------------------------------------------------------- sumi-e
def genji_gumo(w, h, x0, x1, y, t):
    """A gold cloud band whose ends step down in three stacked tiers, each rounded, the top tier inset most."""
    im = Image.new("L", (w * 2, h * 2), 0)
    d = ImageDraw.Draw(im)
    X0, X1, Y, T = x0 * 2, x1 * 2, y * 2, t * 2
    tiers = [(-T, -T / 3, 0.0), (-T / 3, T / 3, 0.9 * T), (T / 3, T, 1.6 * T)]     # top, middle, bottom: the top inset most
    for (ya, yb, ext) in tiers:
        r = (yb - ya) / 2
        a, b = X0 + 2.2 * T - ext, X1 - 2.2 * T + ext
        d.rounded_rectangle([a, Y + ya, b, Y + yb], radius=r, fill=255)
    return np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255.0


def sumi(px):
    h, w, _ = px.shape
    M = masks()
    L = lum(px)
    yy, xx = grid(h, w)
    hz = CFG["horizon"]
    paper = hexc("#D6CAAB")[None, None, :] * (0.96 + 0.06 * fbm(h, w, 2.0, 3, 1101))[..., None]
    paper = paper * (1 + (fbm(h, w, 1.2, 2, 1102) - 0.5)[..., None] * 0.05)
    noise = fbm(h, w, 7, 3, 1103)
    ink = np.zeros((h, w), np.float32)
    sky = M["sky"]
    ink += sky * (0.30 * np.clip(1 - yy / (h * hz), 0, 1) ** 1.4 + 0.05)
    ink = np.maximum(ink, M["clouds"] * sky * (0.22 + 0.10 * noise))
    farw = M["far"] * (1 - M["field"]) * (1 - M["city"])
    if CFG.get("far_layers"):                                                     # washes by depth: paler with distance
        tops = far_tops(M)
        for i, (m, t) in enumerate(tops):
            k = 0.12 + 0.30 * i / max(1, len(tops) - 1)
            ink = np.where(m * (1 - M["field"]) * (1 - M["city"]) > 0.5, k + 0.06 * noise, ink)
            ink = np.maximum(ink, blur(((np.abs(yy - t[None, :]) < 1.0) & (t[None, :] < h)).astype(np.float32), 0.6) * (k + 0.25))
    else:
        ink = np.maximum(ink, farw * (0.30 + 0.08 * noise))
        ink = np.maximum(ink, blur(outline(M["far"]) * (yy < h * hz), 0.8) * 0.55)
    ink = np.maximum(ink, M["city"] * (0.50 + 0.30 * np.clip((0.35 - L) * 3, 0, 1) + 0.06 * noise))
    ink = ink * (1 - M["field"] * (1 - M["figs"])) + M["field"] * np.exp(-((yy - h * (hz + 0.075)) / 6) ** 2) * 0.18
    ink = blur(ink, 1.0)
    ink = np.clip(ink + np.clip(ink - blur(ink, 3), 0, 1) * 1.2, 0, 1)
    # the near ridge: bare paper under one continuous tapered stroke along its crest: the brush lands wide and dark
    # on the left and thins toward the right; only thin dry streaks run along it
    rid = M["ridge"]
    ink = ink * (1 - rid)
    rt = top_row(rid)
    u = np.clip(xx / w, 0, 1)
    th = 2.5 + 8.0 * (1 - u) ** 0.7
    dd = yy - rt[None, :]
    stroke = np.clip(1 - np.abs(dd - th * 0.45) / (th * 0.55), 0, 1) * (dd > -1)
    streak = tex(fbm(256, 256, 3, 2, 1106), xx / 40.0, yy / 0.5)
    dry = 1 - np.clip((streak - (0.70 - 0.12 * u)) * 10, 0, 1) * 0.85            # thin streaks, a little more toward the end
    ink = np.maximum(ink, blur(stroke, 0.4) * dry * (0.95 - 0.30 * u))
    # the figures' shadows: a light, separate wash below the stroke
    shadow = np.clip((0.45 - L) * 2, 0, 1) * rid * (1 - M["figs"]) * (dd > th + 4)
    ink = np.maximum(ink, blur(shadow, 1.5) * 0.18)
    if CFG.get("fig_tones"):                                                      # each stone in its own ink tone
        ink = ink * (1 - M["figs"]) + M["figs"] * np.clip(0.30 + 0.75 * np.clip(1.0 - L * 1.6, 0, 1), 0, 0.95)
        ink = np.maximum(ink, blur(outline(M["figs"]), 0.5) * 0.85)
    else:
        ink = np.maximum(ink, M["figs"] * 0.95)
    if CFG.get("lines"):
        ink = np.maximum(ink, np.clip(lines_mask(M) * 1.5, 0, 0.85))
    # paths and roads: light brush lines where the painting has a clear edge on the ground
    ground = np.maximum(M["field"], M["ridge"]) * (1 - M["figs"])
    gl = -highpass(L, 3.0) if CFG.get("field_flat") else np.abs(highpass(L, 3.0))   # on water, a light path stays bare paper
    ink = np.maximum(ink, blur(np.clip(gl * 7 - 0.12, 0, 1), 0.6) * ground * 0.5)
    p = paper * (1 - ink[..., None] * 0.93) + hexc("#16130F") * (ink[..., None] * 0.07)
    leaf = hexc("#C9A24E")[None, None, :] * (0.9 + 0.2 * fbm(h, w, 4, 2, 1105))[..., None]
    band = np.zeros((h, w), np.float32)
    for (x0, x1, y, t) in CFG["sumi_bands"]:
        band = np.maximum(band, genji_gumo(w, h, x0 * w, x1 * w, y * h, t * h))
    keep = blur(np.maximum(np.maximum(M["figs"], M["city"]), M["moon"]), 4) * 1.6
    band = np.clip(band - keep, 0, 1)
    rng = np.random.default_rng(1104)
    fringe = np.clip(blur(band, 4) * 1.6 - band, 0, 1)
    dust = fringe * (rng.random((h, w)) > 0.86) * 0.8
    kiri = np.zeros((h, w), np.float32)
    for _ in range(40):
        if rng.random() < 0.5:
            bx0, bx1, by, _t = CFG["sumi_bands"][0 if rng.random() < 0.6 else 1]
            x, y = (bx0 + rng.random() * (bx1 - bx0)) * w, by * h + rng.normal(0, 4)
        else:
            cx = 0 if rng.random() < 0.5 else w
            x = cx + (1 if cx == 0 else -1) * rng.random() ** 1.6 * 0.14 * w
            y = rng.random() ** 1.6 * 0.16 * h
        s = 1.0 + rng.random() * 2.0
        kiri[int(max(0, y - s)):int(max(0, y + s)), int(max(0, x - s)):int(max(0, x + s))] = 0.6 + 0.4 * rng.random()
    kiri = np.clip(kiri - keep, 0, 1) * (np.maximum(band, (yy < 0.17 * h) * ((xx < 0.15 * w) | (xx > 0.85 * w))) > 0.3)
    p = p * (1 - band[..., None] * 0.9) + leaf * (band[..., None] * 0.9)
    p = p * (1 - dust[..., None]) + hexc("#D4AE58") * dust[..., None]
    p = p * (1 - kiri[..., None]) + hexc("#E2BE68") * kiri[..., None]
    p = p * (1 - M["moonlit"][..., None]) + hexc("#E6C878") * M["moonlit"][..., None]
    lan = blur(M["lantern"], 1.0)
    p = p * (1 - lan[..., None]) + hexc(CFG.get("sumi_light", "#D2562E")) * lan[..., None]   # one touch for the practical light
    ly_, lx_ = np.where(M["lantern"] > 0.5)
    if len(lx_):                                                                  # a 1 px ink bail up to her hand
        cx_, top_ = int(round(lx_.mean())), int(ly_.min())
        p[max(0, top_ - 5):top_, cx_] = p[max(0, top_ - 5):top_, cx_] * 0.25 + hexc("#16130F") * 0.75
    s0x, s0y, sz = w - 36, h - 36, 22
    p[s0y:s0y + sz, s0x:s0x + sz] = p[s0y:s0y + sz, s0x:s0x + sz] * 0.15 + hexc("#B2402F") * 0.85
    sy_, sx_ = np.mgrid[0:sz, 0:sz].astype(np.float32)
    ccx, ccy, cr = sz / 2, sz / 2, sz * 0.32
    carve = ((sx_ - ccx) ** 2 + (sy_ - ccy) ** 2 < cr ** 2) & ~((sx_ - ccx - cr * 0.45) ** 2 + (sy_ - ccy + cr * 0.25) ** 2 < (cr * 0.9) ** 2)
    p[s0y:s0y + sz, s0x:s0x + sz] = np.where(carve[..., None], hexc("#D6CAAB"), p[s0y:s0y + sz, s0x:s0x + sz])
    return p


TREAT = {"classic": lambda px: px, "medallion": medallion, "ishgard-glass": ishgard, "aether-crystal": aether,
         "astrologian-orrery": orrery, "sumi-to-kinpaku": sumi}

if __name__ == "__main__":
    key = sys.argv[1] if len(sys.argv) > 1 else "evercold-b"
    base = load(key)
    for name in THEMES:
        out = TREAT[name](base)
        Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").save(OUT / f"{key}-{name}.png", optimize=True)
        print(key, name)
