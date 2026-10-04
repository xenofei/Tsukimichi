"""Option B: one painting per release, rendered in a distinct art treatment per theme (not only a grade).
Input: ../optionb/evercold-b-base.png (1120 x 440). Output: ../optionb/evercold-b-<theme>.png.

  classic             the painting, as painted
  medallion           a gilt-framed oil vignette: heavier impasto, a warm varnish, faint craquelure, an oval opening
                      in a gilt moulding lit from the upper left, lapis velvet in the spandrels
  ishgard-glass       a stained-glass window: leaded cells of flat glass, grisaille paint for the detail, light
                      passing through (the bright cells bloom), two iron saddle bars
  aether-crystal      a crystalline facet treatment: a jittered triangle mesh, each facet one flat colour with its own
                      tilt lit from the upper left, bright facet edges, the figures kept in grisaille
  astrologian-orrery  an engraved astrolabe plate: lapis enamel sky, brass ground, the scene cut as hatching whose
                      weight follows the shadows, almucantar and azimuth hairlines, a graduated limb
  sumi-to-kinpaku     sumi-e on toned washi: ink washes by depth, gold-leaf cloud bands (suyari-gasumi) and kirigane
                      squares in the dawn, the moon left as bare paper

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


# ---------------------------------------------------------------------------------------------------------- medallion
def medallion(px):
    h, w, _ = px.shape
    from paint_option_b import kuwahara
    p = kuwahara(px, 3)
    # warm varnish: a gentle yellowing that lifts mids and warms the highlights
    p = p * np.array([1.03, 1.0, 0.92], np.float32)
    L = lum(p)[..., None]
    p = np.clip(L + (p - L) * 1.08, 0, 1)
    p = 1 - (1 - p) * (1 - np.array([0.10, 0.07, 0.02], np.float32) * np.clip(L, 0, 1))
    # craquelure: fine dark cracks at very low alpha
    cr = fbm(h, w, 14, 4, 801)
    cracks = np.exp(-((cr - 0.5) / 0.012) ** 2) * 0.10
    p = p * (1 - cracks[..., None])
    # the oval opening in a gilt moulding; lapis velvet in the spandrels
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    rx, ry = w * 0.47, h * 0.47
    d = np.sqrt(((xx - w / 2) / rx) ** 2 + ((yy - h / 2) / ry) ** 2)
    inside = np.clip((1 - d) * 160, 0, 1)
    p = p * (1 - 0.35 * np.clip((d - 0.80) / 0.2, 0, 1) ** 2)[..., None]       # the painting darkens toward its frame
    velvet = np.stack([np.full((h, w), v, np.float32) for v in hexc("#141A33")], -1) * (0.85 + 0.3 * fbm(h, w, 3, 2, 802))[..., None]
    p = p * inside[..., None] + velvet * (1 - inside[..., None])
    ring = np.exp(-((d - 1.0) / 0.018) ** 2)
    ang = np.arctan2(yy - h / 2, xx - w / 2)
    light = 0.5 + 0.5 * np.cos(ang - math.radians(-135))                          # upper left is lit
    gilt = hexc("#5C4724")[None, None, :] + (hexc("#F0D9A0") - hexc("#5C4724"))[None, None, :] * (0.15 + 0.85 * light)[..., None]
    inner = np.exp(-((d - 0.975) / 0.006) ** 2)
    p = p * (1 - ring[..., None]) + gilt * ring[..., None]
    p = p * (1 - inner[..., None] * 0.6)                                          # a thin dark sight edge
    p = p * (1 - np.exp(-((d - 1.035) / 0.012) ** 2)[..., None] * 0.5)            # the moulding's shadow on the velvet, down-right
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


# ---------------------------------------------------------------------------------------------------- region masks
def masks():
    """The painter's own region masks (paint_option_b exports them), so each treatment follows the scene's shapes."""
    M = dict(np.load(OUT / "src-masks.npz"))
    M["below"] = np.maximum(M["far"], M["city"])
    M["city"] = M["city"] * (1 - M["field"]) * (1 - M["ridge"])   # the city and its bluff only, not the plain under it
    M["sky"] = 1 - M["below"]
    return M


def regions(M):
    """One id per region, painted back to front: 0 sky, 1 cloud, 2 far range, 3 city and bluff, 4 valley snow,
    5 near ridge, 6 figures, 7 moon."""
    r = np.zeros(M["sky"].shape, np.int64)
    r[(M["sky"] > 0.5) & (M["clouds"] > 0.35)] = 1
    r[M["far"] > 0.5] = 2
    r[M["city"] > 0.5] = 3
    r[M["field"] > 0.5] = 4
    r[M["ridge"] > 0.5] = 5
    r[M["figs"] > 0.5] = 6
    r[M["moon"] > 0.5] = 7
    r[M["moonlit"] > 0.4] = 8
    return r


# ---------------------------------------------------------------------------------------------------- ishgard glass
GLASS = [hexc(x) for x in ("#1B2F78", "#2D4AA6", "#4A3F92", "#6E5AA8", "#B8607A", "#E09A62", "#F2D49A",
                           "#DCE4F4", "#9FB3E2", "#7D78BC", "#2A2452", "#40356F", "#14162A", "#F4EEDC")]


def ishgard(px):
    h, w, _ = px.shape
    M = masks()
    reg = regions(M)
    scale = {0: 15, 1: 12, 2: 14, 3: 8, 4: 18, 5: 20, 6: 5, 7: 6, 8: 3}
    lab = np.zeros((h, w), np.int64)
    off = 0
    for rid, cell in scale.items():
        l, n = jitter_labels(h, w, cell, 900 + rid)
        lab = np.where(reg == rid, l + off, lab)
        off += n
    col, cnt = label_mean(px, lab, off)
    # each piece is a real pot-metal glass, chosen per region by the piece's own lightness, so the window reads in
    # the glazier's colours: lapis and cobalt sky, rose and amber where the dawn glows, violet hills, dark amethyst
    # city, white and pale-blue glass for the snow, near-black for the figures, white for the moon
    SUB = {0: ["#14235E", "#1B2F78", "#2D4AA6", "#5A5AA8", "#A86A86", "#E3A26A"], 1: ["#3E3884", "#5A4F98", "#8A5A9A", "#C07080", "#E6A872"],
           2: ["#3A3478", "#5A4F98", "#7D78BC"], 3: ["#1E1A3C", "#2A2452", "#40356F", "#5A4A8A"],
           4: ["#5A6AA8", "#9FB3E2", "#C9D4F0", "#E8EEFA"], 5: ["#3C4A8C", "#6E80C0", "#A9B9E6", "#DCE4F4"],
           6: ["#101222", "#14162A"], 7: ["#1E2C6A", "#26357A"], 8: ["#F4EEDC"]}
    regc = np.bincount(lab.ravel(), weights=reg.ravel(), minlength=off) / np.maximum(cnt, 1)
    regc = np.rint(regc).astype(int)
    cl = col @ LUM
    for rid, hexes in SUB.items():
        pal = np.stack([hexc(x) for x in hexes])
        sel = (regc == rid) & (cnt > 0)   # labels a region never uses are empty: leave them out
        if not sel.any():
            continue
        lo, hi = np.percentile(cl[sel], 5), np.percentile(cl[sel], 95)
        t = np.clip((cl[sel] - lo) / max(hi - lo, 1e-3), 0, 0.999) * len(hexes)
        col[sel] = col[sel] * 0.25 + pal[t.astype(int)] * 0.75
    glass = col[lab] * (0.93 + 0.14 * fbm(h, w, 4, 2, 903))[..., None]
    # light through the window: everything a step brighter than paint, the bright pieces blooming past the lead
    glass = 1 - (1 - glass) * 0.88
    bloom = blur(np.clip(lum(glass) - 0.55, 0, 1), 4)
    glass = 1 - (1 - glass) * (1 - np.array([1.0, 0.92, 0.84], np.float32) * (bloom * 0.30)[..., None])
    # grisaille: the painter's fired line work for the figures, spires and windows
    L = lum(px)
    gris = np.clip(-highpass(L, 2.0) * 3.5, 0, 0.75) * (np.maximum(M["city"], M["figs"]) > 0.3)
    glass = glass * (1 - gris[..., None] * 0.8)
    # the lantern, a piece of amber flashed glass
    glass = 1 - (1 - glass) * (1 - hexc("#FFC07A") * (blur(M["lantern"], 1.5) * 1.2)[..., None].clip(0, 1))
    # lead came: 2 px, dark, with a soft highlight on its upper-left side
    came = np.clip(blur(edges(lab).astype(np.float32), 0.6) * 2.2, 0, 1)
    glass = glass * (1 - came[..., None]) + hexc("#202430") * came[..., None]
    hl = np.clip(came - np.roll(np.roll(came, 1, 0), 1, 1), 0, 1)
    glass = glass + (hexc("#8C95B0") - glass) * (hl * 0.30)[..., None]
    # two iron saddle bars, placed clear of the figures and the city
    for y in (int(h * 0.27), int(h * 0.88)):
        glass[y - 2:y + 2] = glass[y - 2:y + 2] * 0.15 + hexc("#1A1C24") * 0.85
        glass[y - 2] = glass[y - 2] * 0.6 + hexc("#6A7088") * 0.4
    return glass


# ---------------------------------------------------------------------------------------------------- aether crystal
def aether(px):
    """The scene seen through cut moonstone: a jittered triangle mesh over the painting. Each facet takes part of
    its mean colour and its own tilt, lit from the upper left; the facet edges catch the light. The painting stays
    legible under it; the figures and the city keep their own shapes."""
    h, w, _ = px.shape
    M = masks()
    rng = np.random.default_rng(1001)
    cell = 26
    gh, gw = int(h / cell) + 2, int(w / cell) + 2
    vx = (np.arange(gw)[None, :] + 0.6 * (rng.random((gh, gw)) - 0.5)) * cell
    vy = (np.arange(gh)[:, None] + 0.6 * (rng.random((gh, gw)) - 0.5)) * cell
    im = Image.new("I", (w, h), 0)
    d = ImageDraw.Draw(im)
    n = 0
    for j in range(gh - 1):
        for i in range(gw - 1):
            a, b, c_, dd = (vx[j, i], vy[j, i]), (vx[j, i + 1], vy[j, i + 1]), (vx[j + 1, i], vy[j + 1, i]), (vx[j + 1, i + 1], vy[j + 1, i + 1])
            for t in (((a, b, dd), (a, dd, c_)) if (i + j) % 2 else ((a, b, c_), (b, dd, c_))):
                n += 1
                d.polygon(t, fill=n)
    lab = np.asarray(im, np.int64)
    col, _ = label_mean(px, lab, n + 1)
    subject = blur(np.maximum(np.maximum(M["city"], M["figs"]), M["moon"]), 1.5)
    mix = (0.55 * (1 - subject * 0.85))[..., None]
    p = px * (1 - mix) + col[lab] * mix
    tilt = rng.normal(0, 1, (n + 1, 2)).astype(np.float32)
    lit = (tilt[:, 0] * -0.7071 + tilt[:, 1] * -0.7071)[lab]
    p = np.clip(p * (1 + 0.09 * lit * (1 - subject))[..., None], 0, 1)
    p = p * np.array([0.95, 1.0, 1.05], np.float32)
    e = blur(edges(lab).astype(np.float32), 0.5) * (1 - subject)
    up = np.clip(e - np.roll(np.roll(e, 1, 0), 1, 1), 0, 1)
    p = 1 - (1 - p) * (1 - hexc("#CFF3FF") * (e * 0.08 + up * 0.20)[..., None])
    # a faint dispersion fringe on the brightest facet edges: aether blue on one side, rose on the other
    p[..., 2] = np.clip(p[..., 2] + up * 0.05, 0, 1)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    glint = np.zeros((h, w), np.float32)
    for _ in range(8):
        j, i = rng.integers(1, int(gh * 0.40)), rng.integers(1, gw - 1)
        gx, gy = vx[j, i], vy[j, i]
        if math.hypot(gx - 0.875 * w, gy - 0.15 * h) < 70:
            continue
        glint = np.maximum(glint, np.exp(-((xx - gx) ** 2 + (yy - gy) ** 2) / (2 * 1.5 ** 2)))
    return 1 - (1 - p) * (1 - hexc("#BFF0FF") * (glint * 0.75)[..., None])


# ---------------------------------------------------------------------------------------------------- orrery plate
def orrery(px):
    """An engraved astrolabe plate: a lapis enamel sky inlaid in brass, the land cut as hatching whose weight follows
    the shadows, the city and the figures cross-hatched dark, the moon a brass inlay, and the rete's hairlines over all.
    The plate is lit from the upper left: grooves are dark with a lit lower lip."""
    h, w, _ = px.shape
    M = masks()
    L = lum(px)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    sky = np.clip(M["sky"] - M["moon"], 0, 1)
    sheen = np.clip(1 - (xx / w * 0.6 + yy / h * 0.4), 0, 1)
    brass = hexc("#7C6034")[None, None, :] + (hexc("#E2C88E") - hexc("#7C6034"))[None, None, :] * (0.35 + 0.55 * sheen)[..., None]
    enamel = hexc("#16245A")[None, None, :] + (hexc("#33498A") - hexc("#16245A"))[None, None, :] * np.clip(yy / (h * HZ), 0, 1)[..., None]
    plate = brass * (1 - sky[..., None]) + enamel * sky[..., None]
    land = 1 - sky
    # hatching on the land: horizontal grooves, heavier in shadow
    dark = np.clip(1.0 - L * 1.7, 0, 1)
    period = 6.0
    groove = blur((np.abs((yy % period) - period / 2) < dark * period * 0.42).astype(np.float32), 0.5)
    # cross-hatching for the city and the figures, the darkest things
    xg = blur((np.abs(((xx + yy) % 5.0) - 2.5) < 1.0).astype(np.float32), 0.5) * np.maximum(M["city"], M["figs"])
    cut = np.clip(groove + xg, 0, 1) * land
    plate = plate * (1 - (cut * 0.62)[..., None])
    lip = np.clip(cut - np.roll(cut, 1, 0), 0, 1)
    plate = plate + (hexc("#F2DDA8") - plate) * (lip * 0.22)[..., None]
    # in the enamel, the dawn glow and the clouds' lit undersides as fine brass lines
    glow = np.clip((L - 0.32) * 2.6, 0, 1) * sky
    fine = blur((np.abs(((yy - 1) % 3.0) - 1.5) < glow * 1.4).astype(np.float32), 0.4)
    plate = plate + (hexc("#E6CF98") - plate) * (fine * 0.6)[..., None]
    stars = np.clip((L - blur(L, 3)) * 8 - 0.4, 0, 1) * sky * (yy < h * 0.4)
    plate = plate + (hexc("#F4E4BC") - plate) * (stars * 0.9)[..., None]
    # outlines of the ridges, the city and the figures as single cut lines
    for k in ("far", "city", "ridge", "figs", "field"):
        e = np.clip(np.abs(M[k] - np.roll(M[k], 1, 0)) + np.abs(M[k] - np.roll(M[k], 1, 1)), 0, 1)
        plate = plate * (1 - (e * 0.55)[..., None])
    # the moon, a brass inlay; the earthshine side a duller brass
    plate = plate * (1 - M["moon"][..., None]) + hexc("#26356E") * M["moon"][..., None]
    plate = plate * (1 - M["moonlit"][..., None]) + hexc("#F2DDA8") * M["moonlit"][..., None]
    # the rete: almucantar circles centred below the plate, azimuth hairlines, the horizon, a graduated limb
    im = Image.new("L", (w * 2, h * 2), 0)
    dr = ImageDraw.Draw(im)
    cx, cy = w * 1.0, h * 2.8
    for k in range(6):
        r = (h * 1.45 + k * h * 0.17) * 2
        dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=255, width=2)
    for a in range(-48, 49, 16):
        t = math.radians(a - 90)
        dr.line([(cx, cy), (cx + math.cos(t) * h * 6, cy + math.sin(t) * h * 6)], fill=140, width=2)
    for i in range(0, w * 2, 24):
        tl = 18 if (i // 24) % 5 == 0 else 9
        dr.line([(i, h * 2 - 4), (i, h * 2 - 4 - tl)], fill=255, width=2)
        dr.line([(i, 4), (i, 4 + tl)], fill=255, width=2)
    dr.line([(0, h * 2 - 26), (w * 2, h * 2 - 26)], fill=255, width=2)
    dr.line([(0, 26), (w * 2, 26)], fill=255, width=2)
    rete = np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255.0
    rete = rete * (1 - np.maximum(M["figs"], M["moon"]))
    plate = plate + (hexc("#F0DCA6") - plate) * (rete * 0.38)[..., None]
    # the lantern stays the warm practical light
    plate = 1 - (1 - plate) * (1 - hexc("#FFD48E") * (blur(M["lantern"], 1.5) * 1.3).clip(0, 1)[..., None])
    return plate


# ---------------------------------------------------------------------------------------------------- sumi-e
def sumi(px):
    """Sumi-e on toned washi with gold leaf. The sky is a pale wash, darker at the top; clouds are wet blooms; the far
    range a pale wash with a darker crest; the city a mid wash with dark spires; the snow is bare paper; the near
    ridge is one dry-brush stroke; the figures solid ink; the moon gold leaf; the dawn kirigane and two gold cloud
    bands (suyari-gasumi), kept clear of the subject; one vermilion touch for the lantern; a seal."""
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
    # wet edges: ink pools at the boundary of each wash
    ink = blur(ink, 1.0)
    pool = np.clip(ink - blur(ink, 3), 0, 1) * 1.2
    ink = np.clip(ink + pool, 0, 1)
    # the near ridge: bare paper below one dry-brush stroke along its crest
    rid = M["ridge"]
    ink = ink * (1 - rid)
    stroke = np.clip(rid - np.roll(rid, 4, 0), 0, 1)
    dry = (fbm(h, w, 1.5, 2, 1106) > 0.42) * (0.65 + 0.35 * fbm(h, w, 40, 2, 1107))
    ink = np.maximum(ink, blur(stroke, 0.6) * dry * 0.85)
    shadow = np.clip((0.45 - L) * 2, 0, 1) * rid * (1 - M["figs"])
    ink = np.maximum(ink, blur(shadow, 1.5) * 0.30)
    ink = np.maximum(ink, M["figs"] * 0.95)
    p = paper * (1 - ink[..., None] * 0.93) + hexc("#16130F") * (ink[..., None] * 0.07)
    # gold: the moon in leaf, kirigane in the dawn, two gold bands in the empty sky and across the far valley
    leaf = hexc("#C9A24E")[None, None, :] * (0.9 + 0.2 * fbm(h, w, 4, 2, 1105))[..., None]
    im = Image.new("L", (w * 2, h * 2), 0)
    dr = ImageDraw.Draw(im)
    for (x0, x1, y, t) in ((-40, 0.36, 0.31, 0.030), (0.78, 1.05, 0.79, 0.030)):
        X0 = (x0 if x0 < 0 else x0 * w) * 2
        dr.rounded_rectangle([X0, (y - t) * h * 2, x1 * w * 2, (y + t) * h * 2], radius=t * h * 2, fill=255)
    band = np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255.0
    keep = blur(np.maximum(np.maximum(M["figs"], M["city"]), M["moon"]), 4) * 1.6
    band = np.clip(band - keep, 0, 1)
    band = np.maximum(band, 0)
    rng = np.random.default_rng(1104)
    kiri = np.zeros((h, w), np.float32)
    gx_, gy_ = 0.645 * w, h * (HZ - 0.10)
    for _ in range(60):
        r = rng.random() ** 1.5
        a = rng.random() * math.pi
        x = gx_ + math.cos(a) * r * 0.32 * w
        y = gy_ - math.sin(a) * r * 0.20 * h
        s = 1.0 + rng.random() * 2.2
        kiri[int(max(0, y - s)):int(y + s), int(max(0, x - s)):int(x + s)] = 0.6 + 0.4 * rng.random()
    kiri = np.clip(kiri - keep, 0, 1) * sky
    p = p * (1 - band[..., None] * 0.9) + leaf * (band[..., None] * 0.9)
    p = p * (1 - kiri[..., None]) + hexc("#E2BE68") * kiri[..., None]
    p = p * (1 - M["moonlit"][..., None]) + hexc("#E6C878") * M["moonlit"][..., None]
    lan = blur(M["lantern"], 1.0)
    p = p * (1 - lan[..., None]) + hexc("#D2562E") * lan[..., None]
    p[h - 34:h - 14, w - 34:w - 14] = p[h - 34:h - 14, w - 34:w - 14] * 0.2 + hexc("#B2402F") * 0.8
    return p


if __name__ == "__main__":
    base = load()
    save(base, "classic")
    for name, f in (("medallion", medallion), ("ishgard-glass", ishgard), ("aether-crystal", aether), ("astrologian-orrery", orrery), ("sumi-to-kinpaku", sumi)):
        save(f(base), name)
        print(name)
