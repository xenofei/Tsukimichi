"""Option B: one painting per release, rendered in a distinct art treatment per theme (not only a grade).
Input: ../optionb/evercold-b-base.png (1120 x 440) and its region masks ../optionb/src-masks.npz (paint_option_b.py).
Output: ../optionb/evercold-b-<theme>.png.

  classic             the painting, as painted
  medallion           an oil painting: heavier impasto, a warm varnish, faint craquelure in the thick paint, a slim
                      gilt slip inside the popup's own brass frame, lit from the upper left
  ishgard-glass       a stained-glass window whose lead follows the drawing: large sky and snow pieces cut along the
                      ridge and cloud edges, the figures and spires painted in grisaille on a few pieces, a white
                      glass crescent, an amber lantern piece, two saddle bars clear of the subject
  aether-crystal      cut moonstone over the sky and the far range only: facets shaded consistently from the upper
                      left (as on domed gems), a faint blue adularescent sheen; the ground and the figures stay clear
  astrologian-orrery  an engraved plate: silver ground and lapis enamel sky, dark line engraving that follows each
                      contour, the clouds cut in line, dense hatching in the shadows; brass only as inlay (the crescent,
                      the graduated limb, the rete)
  sumi-to-kinpaku     sumi-e on toned washi: ink washes by depth, bare-paper snow, one tapered dry-brush stroke for the
                      ridge, a gold-leaf crescent, genji-gumo gold cloud bands with gold-dust edges, kirigane only in
                      the bands and the top corners, a vermilion seal carved with a crescent

Every treatment keeps the one natural light (the sun below the horizon behind the city) and the one warm practical
light (the lantern); none adds a light of its own. Run: py -3 option_b_themes.py
"""
import math
import pathlib

import numpy as np
from PIL import Image, ImageDraw

from artlib import blur, fbm, hexc

ART = pathlib.Path(__file__).resolve().parent.parent
OUT = ART / "optionb"
LUM = np.array([0.2126, 0.7152, 0.0722], np.float32)
HZ = 0.60  # horizon, as a fraction of the height (paint_option_b)


def load():
    return np.asarray(Image.open(OUT / "evercold-b-base.png").convert("RGB"), np.float32) / 255.0


def save(px, name):
    Image.fromarray((np.clip(px, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").save(OUT / f"evercold-b-{name}.png", optimize=True)


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
    """The painter's own region masks, so each treatment follows the scene's shapes."""
    M = dict(np.load(OUT / "src-masks.npz"))
    M["below"] = np.maximum(M["far"], M["city"])
    M["sky"] = 1 - M["below"]
    M["city"] = M["city"] * (1 - M["field"]) * (1 - M["ridge"])   # the city and its bluff only, not the plain under it
    return M


def top_row(m):
    """For each column, the first row where the mask is set (the top contour of a region)."""
    on = m > 0.5
    any_ = on.any(0)
    return np.where(any_, on.argmax(0), m.shape[0]).astype(np.float32)


MOON = (0.875, 0.15, 15.0)   # the moon's centre (fraction of w, h) and radius at 1120 x 440


# ---------------------------------------------------------------------------------------------------------- medallion
def medallion(px):
    h, w, _ = px.shape
    M = masks()
    from paint_option_b import kuwahara
    L0 = lum(px)
    sil = blur(np.maximum(M["figs"], M["city"] * (np.mgrid[0:h, 0:w][0] < h * 0.52)), 2)
    p = kuwahara(px, 3) * (1 - sil[..., None]) + px * sil[..., None]
    # warm varnish: a gentle yellowing that lifts mids and warms the highlights
    p = p * np.array([1.03, 1.0, 0.92], np.float32)
    L = lum(p)[..., None]
    p = np.clip(L + (p - L) * 1.08, 0, 1)
    p = 1 - (1 - p) * (1 - np.array([0.10, 0.07, 0.02], np.float32) * np.clip(L, 0, 1))
    # craquelure: a third of the old strength, only in the thick (light) paint, never on the dark silhouettes
    cr = fbm(h, w, 14, 4, 801)
    thick = np.clip((L0 - 0.30) / 0.3, 0, 1) * (1 - np.clip(sil * 3, 0, 1))
    cracks = np.exp(-((cr - 0.5) / 0.010) ** 2) * 0.035 * thick * (0.6 + 0.8 * fbm(h, w, 60, 2, 805))
    p = p * (1 - cracks[..., None])
    # the painting darkens a little toward its frame
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    ex = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))
    p = p * (1 - 0.18 * np.clip(1 - ex / 50, 0, 1) ** 2)[..., None]
    # a slim gilt slip inside the popup's brass frame: 4 px, inset 5 px; top and left lit, bottom and right shaded
    inset, wdt = 5, 4
    slip = (ex >= inset) & (ex < inset + wdt)
    sides = np.stack([yy, xx, h - 1 - yy, w - 1 - xx])          # top, left, bottom, right
    lit = sides.argmin(0) < 2                                     # the top and left runs face the light
    across = np.clip((ex - inset) / wdt, 0, 1)                                     # 0 at the outer edge, 1 at the sight edge
    hi = hexc("#F0D9A0") * (1 - across[..., None] * 0.35) + hexc("#C9A65C") * (across[..., None] * 0.35)
    lo = hexc("#7C6236") * (1 - across[..., None] * 0.4) + hexc("#5C4724") * (across[..., None] * 0.4)
    gilt = np.where(lit[..., None], hi, lo)
    p = np.where(slip[..., None], gilt, p)
    p = np.where(((ex >= inset + wdt) & (ex < inset + wdt + 1))[..., None], p * 0.55, p)   # the sight edge's thin shadow
    p = np.where((ex < inset)[..., None], p * 0.6 + hexc("#1E2236") * 0.4, p)              # a dark lip under the brass frame
    return p


# ---------------------------------------------------------------------------------------------------- ishgard glass
def jitter_labels(h, w, cell, seed):
    """Voronoi labels from a jittered grid, searching only the 3 x 3 neighbouring cells."""
    rng = np.random.default_rng(seed)
    gh, gw = int(h / cell) + 2, int(w / cell) + 2
    sx = (np.arange(gw)[None, :] + 0.15 + 0.7 * rng.random((gh, gw))) * cell
    sy = (np.arange(gh)[:, None] + 0.15 + 0.7 * rng.random((gh, gw))) * cell
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
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


def contour_strips(h, w, top, seed, k_band, seg0, seg_k):
    """Pieces cut along a contour: bands at a growing distance below `top` (per column), each cut into long strips.
    So the lead runs parallel to the ridge or the horizon, as a glazier cuts snow and ground, never as paving."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    rng = np.random.default_rng(seed)
    wob = np.interp(np.arange(w), np.linspace(0, w, 12), rng.random(12) * 0.6)[None, :]
    d = np.clip(yy - top[None, :], 0, None)
    band = np.floor(np.sqrt(d) * k_band + wob).astype(np.int64)
    width = seg0 + seg_k * band
    off = (band * 97) % 211
    seg = np.floor((xx + off + 12 * np.sin(yy / 23.0)) / width).astype(np.int64)
    return band * 1000 + seg


SUB = {0: ["#14235E", "#1B2F78", "#2D4AA6", "#5A5AA8", "#A86A86", "#E3A26A"], 1: ["#3E3884", "#5A4F98", "#8A5A9A", "#C07080", "#E6A872"],
       2: ["#3A3478", "#5A4F98", "#7D78BC"], 3: ["#1E1A3C", "#2A2452", "#40356F"], 4: ["#8A9AD0", "#B3C2EA", "#D4DDF4", "#EEF2FC"],
       5: ["#5868A8", "#8496D0", "#B1C0EA", "#DCE4F6"], 6: ["#121426"], 7: ["#1C2A66"], 8: ["#F4EEDC"], 9: ["#E8A040"]}


def ishgard(px):
    h, w, _ = px.shape
    M = masks()
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    reg = np.zeros((h, w), np.int64)
    reg[(M["sky"] > 0.5) & (M["clouds"] > 0.35)] = 1
    reg[M["far"] > 0.5] = 2
    reg[M["city"] > 0.5] = 3
    reg[M["field"] > 0.5] = 4
    reg[M["ridge"] > 0.5] = 5
    reg[M["figs"] > 0.5] = 6
    reg[M["moon"] > 0.5] = 7
    reg[M["moonlit"] > 0.4] = 8
    reg[blur(M["lantern"], 1.5) > 0.25] = 9
    # pieces: the sky sparingly, the clouds a few per cloud, the hills and the city as a few large pieces, the snow and
    # the ridge as contour strips, one piece per figure, the moon two pieces, the lantern one
    sky_l, n_sky = jitter_labels(h, w, 48, 901)
    cl_l, n_cl = jitter_labels(h, w, 36, 902)
    far_l = np.floor((xx + 50 * fbm(h, w, 80, 2, 903)) / 150).astype(np.int64)
    city_l = np.where(yy > h * 0.505, np.floor(xx / 90).astype(np.int64), 100 + np.floor(xx / 70).astype(np.int64))
    field_l = contour_strips(h, w, top_row(M["field"]), 904, 1.0, 150, 30)
    ridge_l = contour_strips(h, w, top_row(M["ridge"]), 905, 0.8, 230, 40)
    figs_l = (xx > 388).astype(np.int64)
    parts = {0: sky_l, 1: cl_l, 2: far_l, 3: city_l, 4: field_l, 5: ridge_l, 6: figs_l, 7: np.zeros_like(reg), 8: np.zeros_like(reg), 9: np.zeros_like(reg)}
    lab = reg * 10_000_000 + np.where(reg >= 0, 0, 0)
    for rid, l in parts.items():
        lab = np.where(reg == rid, rid * 10_000_000 + l, lab)
    uniq, lab = np.unique(lab, return_inverse=True)
    lab = lab.reshape(h, w)
    n = len(uniq)
    col, cnt = label_mean(px, lab, n)
    regc = (uniq // 10_000_000).astype(int)
    cl = col @ LUM
    for rid, hexes in SUB.items():
        pal = np.stack([hexc(x) for x in hexes])
        sel = (regc == rid) & (cnt > 0)
        if not sel.any():
            continue
        lo, hi = np.percentile(cl[sel], 5), np.percentile(cl[sel], 95)
        t = np.clip((cl[sel] - lo) / max(hi - lo, 1e-3), 0, 0.999) * len(hexes)
        col[sel] = col[sel] * 0.2 + pal[t.astype(int)] * 0.8
    glass = col[lab]
    # within a large piece, pot-metal glass varies: a gentle streaked density, and the sky's dawn gradient survives
    streak = tex(fbm(256, 256, 14, 3, 906), xx / 3.0, yy / 0.8)
    glass = glass * (0.92 + 0.14 * streak)[..., None]
    grad = blur(px, 6)
    glass = glass * 0.82 + grad * 0.18 * (reg <= 1)[..., None] + glass * 0.18 * (reg > 1)[..., None]
    # the crescent: one white piece, brighter on its lower left where the sun is
    ml = M["moonlit"] > 0.4
    mx, my = MOON[0] * w, MOON[1] * h
    toward = np.clip(((mx - xx) * 0.7 + (yy - my) * 0.7) / MOON[2] * 0.5 + 0.5, 0, 1)
    glass = np.where(ml[..., None], hexc("#E8E2CC") + (hexc("#FFFBEE") - hexc("#E8E2CC")) * toward[..., None], glass)
    # light through the window: everything a step brighter than paint, the bright pieces blooming past the lead
    glass = 1 - (1 - glass) * 0.9
    bloom = blur(np.clip(lum(glass) - 0.6, 0, 1), 4)
    glass = 1 - (1 - glass) * (1 - np.array([1.0, 0.92, 0.84], np.float32) * (bloom * 0.25)[..., None])
    # grisaille: the painter's fired line work for the figures and the spires, on their few pieces
    L = lum(px)
    gris = np.clip(-highpass(L, 2.0) * 3.5, 0, 0.75) * (np.maximum(M["city"], M["figs"]) > 0.3)
    glass = glass * (1 - gris[..., None] * 0.8)
    win = np.clip(highpass(L, 2.0) * 5, 0, 1) * (M["city"] > 0.5) * (yy < h * 0.5)
    glass = 1 - (1 - glass) * (1 - hexc("#FFC070") * (win * 0.8)[..., None])
    # lead came: 2 px, dark, with a soft highlight on its upper-left side
    came = np.clip(blur(edges(lab).astype(np.float32), 0.6) * 2.2, 0, 1)
    glass = glass * (1 - came[..., None]) + hexc("#202430") * came[..., None]
    hl = np.clip(came - np.roll(np.roll(came, 1, 0), 1, 1), 0, 1)
    glass = glass + (hexc("#8C95B0") - glass) * (hl * 0.30)[..., None]
    # two iron saddle bars, above the spires and the moon, and below the figures and their shadows
    for y in (30, 398):
        glass[y - 2:y + 2] = glass[y - 2:y + 2] * 0.15 + hexc("#1A1C24") * 0.85
        glass[y - 2] = glass[y - 2] * 0.6 + hexc("#6A7088") * 0.4
    return glass


# ---------------------------------------------------------------------------------------------------- aether crystal
def aether(px):
    """Cut moonstone over the sky and the far range only. Facets are shaded consistently, as on a few broad domed
    gems lit from the upper left: a facet on a dome's upper-left slope is bright, one on its lower-right slope dim.
    A faint blue adularescent sheen floats across the upper sky. The ground, the city and the figures stay clear."""
    h, w, _ = px.shape
    M = masks()
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
    # domes: three broad gems across the sky; a facet's normal leans away from its dome's centre
    domes = np.array([[0.17 * w, 0.20 * h], [0.50 * w, 0.14 * h], [0.83 * w, 0.24 * h]], np.float32)
    dist = ((cents[:, None, :] - domes[None, :, :]) ** 2).sum(-1)
    near = domes[dist.argmin(1)]
    nrm = (cents - near) / (w * 0.18)
    shade = np.clip(-(nrm[:, 0] * 0.7071 + nrm[:, 1] * 0.7071), -1, 1)       # upper-left slopes face the light
    area = np.clip(np.maximum(M["sky"], M["far"] * (1 - M["field"])) - np.maximum(M["city"], M["figs"]), 0, 1)
    area = area * (1 - blur(M["moon"], 3) * 3).clip(0, 1)
    area = blur(area, 1.0)
    mix = (0.6 * area)[..., None]
    p = px * (1 - mix) + col[lab] * mix
    p = np.clip(p * (1 + 0.12 * shade[lab] * area)[..., None], 0, 1)
    e = blur(edges(lab).astype(np.float32), 0.5) * area
    up = np.clip(e - np.roll(np.roll(e, 1, 0), 1, 1), 0, 1)
    p = 1 - (1 - p) * (1 - hexc("#CFF3FF") * (e * 0.10 + up * 0.26)[..., None])
    # adularescence: a soft blue sheen floating across the upper sky, as light moving inside the stone
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    sheen = np.exp(-(((xx - 0.36 * w) * 0.5 + (yy - 0.18 * h)) / (0.12 * h)) ** 2) * np.exp(-((xx - 0.36 * w) / (0.35 * w)) ** 2)
    p = 1 - (1 - p) * (1 - hexc("#A8C8FF") * (sheen * area * 0.14)[..., None])
    p = p * np.array([0.96, 1.0, 1.04], np.float32)
    return p


# ---------------------------------------------------------------------------------------------------- orrery plate
def orrery(px):
    """An engraved plate: a silver ground and a lapis enamel sky; the scene cut as dark line that follows each contour
    (parallel to the ridge, the horizon and the far range; down the cliff faces), denser in the shadows; the clouds
    cut in line; brass only as inlay: the crescent, the graduated limb and the rete. Lit from the upper left: each
    groove is dark with a lit lower lip."""
    h, w, _ = px.shape
    M = masks()
    L = lum(px)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    sky = np.clip(M["sky"] - M["moon"], 0, 1)
    sheen = np.clip(1 - (xx / w * 0.6 + yy / h * 0.4), 0, 1)
    silver = hexc("#8E94A4")[None, None, :] + (hexc("#E4E7EE") - hexc("#8E94A4"))[None, None, :] * (0.45 + 0.5 * sheen)[..., None]
    enamel = hexc("#16245A")[None, None, :] + (hexc("#33498A") - hexc("#16245A"))[None, None, :] * np.clip(yy / (h * HZ), 0, 1)[..., None]
    plate = silver * (1 - sky[..., None]) + enamel * sky[..., None]
    dark = np.clip(1.05 - L * 1.9, 0, 1)

    def lines(phase, spacing, weight):
        f = np.abs((phase / spacing) % 1.0 - 0.5) * 2         # 1 at a line's centre, 0 between lines
        return blur(((1 - f) < weight * 0.9).astype(np.float32), 0.45)

    cut = np.zeros((h, w), np.float32)
    ridge, field, far = M["ridge"], M["field"] * (1 - M["ridge"]), M["far"] * (1 - M["field"]) * (1 - M["city"])
    cliff = M["city"] * (yy > h * 0.505)
    bld = M["city"] * (yy <= h * 0.505)
    rt, ft, at = top_row(M["ridge"]), top_row(M["field"]), top_row(M["far"])
    cut += ridge * lines(yy - rt[None, :], 5.0, 0.25 + 0.6 * dark)                          # parallel to the ridge's crest
    cut += field * lines(np.sqrt(np.clip(yy - ft[None, :], 0, None)) * 6.0, 1.6, 0.20 + 0.6 * dark)  # the plain, in perspective
    cut += far * lines(yy - at[None, :], 4.0, 0.25 + 0.6 * dark)                              # parallel to the far ridgeline
    cut += cliff * np.maximum(lines(xx + 3 * np.sin(yy / 9), 4.0, 0.5 + 0.45 * dark), lines(yy, 11.0, 0.15))  # down the faces, strata
    cut += bld * np.maximum(lines(xx + yy, 3.0, 0.85), lines(xx - yy, 3.0, 0.85))           # the backlit city, cross-hatched
    shadow = np.clip((0.42 - L) * 3, 0, 1) * (ridge + field) * (1 - M["figs"])
    cut += shadow * lines(xx - yy, 4.0, 0.35)                                                  # shadows: dense cross-hatching
    cut = np.clip(cut, 0, 1) * (1 - sky)
    cut = np.maximum(cut, M["figs"])                                                          # the figures, solid
    plate = plate * (1 - cut[..., None]) + hexc("#2A2E3B") * cut[..., None]
    lip = np.clip(cut - np.roll(cut, 1, 0), 0, 1) * (1 - M["figs"])
    plate = plate + (hexc("#F4F6FA") - plate) * (lip * 0.25)[..., None]
    # outlines: every region edge cut as one line
    for k in ("far", "ridge", "field", "figs"):
        e = np.clip(np.abs(M[k] - np.roll(M[k], 1, 0)) + np.abs(M[k] - np.roll(M[k], 1, 1)), 0, 1)
        plate = plate * (1 - (e * 0.6)[..., None])
    ce = np.clip(np.abs(M["city"] - np.roll(M["city"], 1, 0)) + np.abs(M["city"] - np.roll(M["city"], 1, 1)), 0, 1)
    plate = plate * (1 - (ce * 0.6)[..., None])
    # the clouds cut in silver line on the enamel: their outline and a few lines along the lit undersides
    cm = (M["clouds"] > 0.35).astype(np.float32)
    co = np.clip(np.abs(cm - np.roll(cm, 1, 0)) + np.abs(cm - np.roll(cm, 1, 1)), 0, 1) * sky
    under = blur(M["under"], 1.0) * sky
    ul = lines(yy, 3.0, np.clip(under * 6, 0, 0.8)) * (under > 0.02)
    plate = plate + (hexc("#C9D0E0") - plate) * (np.clip(blur(co, 0.4) * 1.5 + ul * 0.7, 0, 1) * 0.7)[..., None]
    stars = np.clip((L - blur(L, 3)) * 8 - 0.4, 0, 1) * sky * (yy < h * 0.4)
    plate = plate + (hexc("#E4E7EE") - plate) * (stars * 0.9)[..., None]
    # brass inlay: the crescent (the earthshine side stays enamel), the rete and the graduated limb
    plate = plate * (1 - M["moon"][..., None]) + hexc("#1E2D66") * M["moon"][..., None]
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
    plate = plate + (hexc("#D9B86E") - plate) * (rete * 0.45)[..., None]
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
    # the lantern stays the warm practical light
    plate = 1 - (1 - plate) * (1 - hexc("#FFD48E") * (blur(M["lantern"], 1.5) * 1.3).clip(0, 1)[..., None])
    return plate


# ---------------------------------------------------------------------------------------------------- sumi-e
def genji_gumo(w, h, x0, x1, y, t, seed):
    """A gold cloud band with scalloped, stepped ends (genji-gumo), not a pill: a straight body, each end built of
    three lobes at staggered heights."""
    im = Image.new("L", (w * 2, h * 2), 0)
    d = ImageDraw.Draw(im)
    X0, X1, Y, T = x0 * 2, x1 * 2, y * 2, t * 2
    d.rectangle([X0 + T * 1.2, Y - T, X1 - T * 1.2, Y + T], fill=255)
    for side, xe in ((-1, X0 + T * 1.2), (1, X1 - T * 1.2)):
        for (dx, dy, r) in ((0, -0.55, 0.95), (1.1, 0.40, 0.85), (2.0, -0.25, 0.65)):
            cx = xe + side * dx * T
            cy = Y + dy * T
            d.ellipse([cx - r * T, cy - r * T, cx + r * T, cy + r * T], fill=255)
        d.rectangle([min(xe, xe + side * 0.8 * T), Y - 0.62 * T, max(xe, xe + side * 0.8 * T), Y + 0.62 * T], fill=255)
    return np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255.0


def sumi(px):
    h, w, _ = px.shape
    M = masks()
    L = lum(px)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    paper = hexc("#D6CAAB")[None, None, :] * (0.96 + 0.06 * fbm(h, w, 2.0, 3, 1101))[..., None]
    paper = paper * (1 + (fbm(h, w, 1.2, 2, 1102) - 0.5)[..., None] * 0.05)
    noise = fbm(h, w, 7, 3, 1103)
    ink = np.zeros((h, w), np.float32)
    sky = M["sky"]
    ink += sky * (0.30 * np.clip(1 - yy / (h * HZ), 0, 1) ** 1.4 + 0.05)
    ink = np.maximum(ink, M["clouds"] * sky * (0.22 + 0.10 * noise))
    farw = M["far"] * (1 - M["field"]) * (1 - M["city"])
    ink = np.maximum(ink, farw * (0.30 + 0.08 * noise))
    crest = np.clip(M["far"] - np.roll(M["far"], 3, 0), 0, 1)
    ink = np.maximum(ink, blur(crest, 0.8) * 0.55)
    citywash = M["city"] * (0.50 + 0.30 * np.clip((0.35 - L) * 3, 0, 1) + 0.06 * noise)
    ink = np.maximum(ink, citywash)
    ink = ink * (1 - M["field"] * (1 - M["figs"])) + M["field"] * np.exp(-((yy - h * (HZ + 0.075)) / 6) ** 2) * 0.18
    ink = blur(ink, 1.0)
    pool = np.clip(ink - blur(ink, 3), 0, 1) * 1.2
    ink = np.clip(ink + pool, 0, 1)
    # the near ridge: bare paper below one tapered dry-brush stroke along its crest; the brush lands on the left (wide,
    # dark), thins and dries toward the right, broken by dry gaps along its length
    rid = M["ridge"]
    ink = ink * (1 - rid)
    rt = top_row(rid)
    u = np.clip(xx / w, 0, 1)
    th = 2.0 + 9.0 * (1 - u) ** 0.7
    dd = yy - rt[None, :]
    stroke = np.clip(1 - np.abs(dd - th * 0.45) / (th * 0.55), 0, 1) * (dd > -1)
    streak = tex(fbm(256, 256, 6, 3, 1106), xx / 14.0, yy / 0.9)
    dry = np.clip((streak - (0.25 + 0.35 * u)) * 6, 0, 1)
    press = 0.95 - 0.35 * u
    ink = np.maximum(ink, blur(stroke * dry, 0.5) * press)
    shadow = np.clip((0.45 - L) * 2, 0, 1) * rid * (1 - M["figs"]) * (dd > th)
    ink = np.maximum(ink, blur(shadow, 1.5) * 0.30)
    ink = np.maximum(ink, M["figs"] * 0.95)
    p = paper * (1 - ink[..., None] * 0.93) + hexc("#16130F") * (ink[..., None] * 0.07)
    # gold: two genji-gumo bands (one in the empty sky, one across the far right of the valley), with soft gold-dust
    # edges; kirigane only inside the bands and in the two top corners; the crescent in leaf
    leaf = hexc("#C9A24E")[None, None, :] * (0.9 + 0.2 * fbm(h, w, 4, 2, 1105))[..., None]
    band = np.maximum(genji_gumo(w, h, -60, 0.34 * w, 0.31 * h, 0.040 * h, 1), genji_gumo(w, h, 0.74 * w, w + 60, 0.81 * h, 0.038 * h, 2))
    keep = blur(np.maximum(np.maximum(M["figs"], M["city"]), M["moon"]), 4) * 1.6
    band = np.clip(band - keep, 0, 1)
    rng = np.random.default_rng(1104)
    fringe = np.clip(blur(band, 4) * 1.6 - band, 0, 1)
    speck = (rng.random((h, w)) > 0.86).astype(np.float32)
    dust = fringe * speck * 0.8
    kiri = np.zeros((h, w), np.float32)
    for _ in range(40):
        if rng.random() < 0.5:   # inside a band
            x, y = rng.random() * w, (0.31 if rng.random() < 0.6 else 0.80) * h + rng.normal(0, 4)
        else:                    # a top corner
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
    p = p * (1 - lan[..., None]) + hexc("#D2562E") * lan[..., None]
    # the seal: vermilion, a crescent carved out of it (shiro-moji: the carved mark shows the paper)
    s0x, s0y, sz = w - 36, h - 36, 22
    p[s0y:s0y + sz, s0x:s0x + sz] = p[s0y:s0y + sz, s0x:s0x + sz] * 0.15 + hexc("#B2402F") * 0.85
    sy_, sx_ = np.mgrid[0:sz, 0:sz].astype(np.float32)
    ccx, ccy, cr = sz / 2, sz / 2, sz * 0.32
    carve = ((sx_ - ccx) ** 2 + (sy_ - ccy) ** 2 < cr ** 2) & ~((sx_ - ccx - cr * 0.45) ** 2 + (sy_ - ccy + cr * 0.25) ** 2 < (cr * 0.9) ** 2)
    region = p[s0y:s0y + sz, s0x:s0x + sz]
    p[s0y:s0y + sz, s0x:s0x + sz] = np.where(carve[..., None], hexc("#D6CAAB"), region)
    return p


if __name__ == "__main__":
    base = load()
    save(base, "classic")
    for name, f in (("medallion", medallion), ("ishgard-glass", ishgard), ("aether-crystal", aether), ("astrologian-orrery", orrery), ("sumi-to-kinpaku", sumi)):
        save(f(base), name)
        print(name)
