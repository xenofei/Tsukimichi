"""1.15 delivery portraits: remove the client emblem's script ring without touching the figure (spec-1.15 §A2.4).

This is the reference for the DataGen step (`DataGen --portrait-masks`). It runs offline, once per patch, on the 8
custom-delivery portraits (SatisfactionNpc.Icon). For each it writes:
  <icon>-keep.png   a 1-bit keep mask at the hr texture's size: the derived data the plugin ships
                    (curated/portrait_masks/), never the art itself
  <icon>-keyed.png  the art with the mask applied, for the mockups and the contact sheet only (not shipped)
At runtime the plugin multiplies the texture's alpha by the keep mask in the same CPU pass that makes the graded copy
(PortraitGrading), so no keying logic runs in the game.

Steps (all at the hr size):
 1. opaque  = alpha > 40
 2. script colour key = alpha > 8 & saturation > .35 & value > .12 & hue 40-170 deg & G > B + .06 & not skin-red
    (R > G + .05 and R > .6): the green letters, their dark-green shading and the gold seal
 3. FIGURE = the 4-connected region of (opaque & not within 2 px of script colour) flood-filled from the curated face
    seed (the 2 px halo stops the flood at the letters' dark outlines), then grown back 2 px into opaque non-script
    pixels so the hair's anti-aliased edge where a letter touches it is kept. Nothing in the figure is ever keyed.
 4. Every other opaque island (not connected to the figure) is removed when it is script (half or more of its pixels
    match the key or its halo) or tiny (under 12 px). Other islands (a detached hair tip, a hand) are kept.
 5. Script-colour pixels outside the figure are removed too (keyed from alpha > 8, so the letters' soft edges count),
    dilated 1 more px but never into the figure, plus any low-alpha pixel within the script's halo.
 6. Fill: removed pixels take the backdrop's local colour and alpha: a normalised blur (r 6 px) of the surrounding
    pixels that are neither figure nor removed. These portraits' backdrop is transparent, so the fill is alpha 0 and the
    plate's well shows, exactly as it does around the figure everywhere else; a painted backdrop would be filled with
    its own blurred colour instead. Nothing inside the figure is ever made transparent.
Run: python key_delivery.py
"""
import pathlib
from collections import deque
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = pathlib.Path(__file__).resolve().parent
SEEDS = {"061661": (190, 225), "061662": (178, 232)}  # the face's centre (curated with the box)

def keep_mask(rgba):
    a = rgba[..., 3].astype(float) / 255
    rgb = rgba[..., :3].astype(float) / 255
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / (mx + 1e-6)
    opaque = a > 40 / 255
    # hue in degrees: the script is green (90-170) and gold (40-90); skin, hair and leather are below 40 or desaturated
    d = np.where(mx - mn > 1e-6, mx - mn, 1)
    hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    script = (a > 8 / 255) & (sat > .35) & (mx > .12) & (hue >= 40) & (hue <= 170) & (g > b + .06) & ~((r > g + .05) & (r > .6))
    return opaque, script

def flood(cand, seed):
    h, w = cand.shape
    seen = np.zeros_like(cand)
    q = deque([seed[::-1]])
    seen[seed[1], seed[0]] = True
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            yy, xx = y + dy, x + dx
            if 0 <= yy < h and 0 <= xx < w and cand[yy, xx] and not seen[yy, xx]:
                seen[yy, xx] = True
                q.append((yy, xx))
    return seen

def islands(mask):
    h, w = mask.shape
    lab = np.zeros(mask.shape, int)
    n = 0
    for y0, x0 in zip(*np.nonzero(mask)):
        if lab[y0, x0]:
            continue
        n += 1
        q = deque([(y0, x0)])
        lab[y0, x0] = n
        while q:
            y, x = q.popleft()
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not lab[yy, xx]:
                        lab[yy, xx] = n
                        q.append((yy, xx))
    return lab, n

def dilate(m, px):
    im = Image.fromarray((m * 255).astype("uint8")).filter(ImageFilter.MaxFilter(2 * px + 1))
    return np.asarray(im) > 127

for icon, seed in SEEDS.items():
    src = Image.open(HERE / f"{icon}.png").convert("RGBA")
    rgba = np.asarray(src).copy()
    opaque, script = keep_mask(rgba)
    # The script's letters are drawn with a dark outline, which the colour key does not match and which touches hair and
    # shoulders; so the flood runs around a 2 px halo of the key, then the figure takes back what lies within 2 px of it
    # that is not script colour (anti-aliased hair edges), so no figure pixel is lost where a letter touches.
    halo = dilate(script, 2)
    figure = flood(opaque & ~halo, seed)
    figure = (dilate(figure, 2) & opaque & ~script) | figure
    other = opaque & ~figure
    lab, n = islands(other)
    remove = np.zeros_like(other)
    kept = []
    for i in range(1, n + 1):
        isl = lab == i
        area = isl.sum()
        frac = (script[isl] | halo[isl]).mean()
        if area < 12 or frac >= .5:
            remove |= isl
        else:
            kept.append((int(area), round(float(frac), 2)))
    remove |= dilate(script & ~figure, 1) & ~figure
    # The letters' soft, low-alpha edges (alpha 8-40, not "opaque") near the script go too.
    remove |= dilate(halo, 1) & ~figure & ~opaque & (rgba[..., 3] > 0)
    if kept:
        print("  kept islands (area, script share):", kept[:8])
    # Fill from the local backdrop (normalised blur of pixels that are neither figure nor removed).
    bg = (~figure & ~remove).astype(float)
    a = rgba[..., 3].astype(float) / 255
    blur = lambda arr: np.asarray(Image.fromarray((np.clip(arr, 0, 1) * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(6)), float) / 255
    wsum = blur(bg) + 1e-4
    fill_a = blur(a * bg) / wsum
    out = rgba.astype(float)
    for c in range(3):
        fill_c = blur(rgba[..., c] / 255 * a * bg) / (blur(a * bg) + 1e-4)
        out[..., c] = np.where(remove, fill_c * 255, out[..., c])
    out[..., 3] = np.where(remove, fill_a * 255, out[..., 3])
    keep = ~remove
    Image.fromarray(out.clip(0, 255).astype("uint8")).save(HERE / f"{icon}-keyed.png")
    Image.fromarray((keep * 255).astype("uint8")).convert("1").save(HERE / f"{icon}-keep.png")
    print(icon, "figure", int(figure.sum()), "removed", int(remove.sum()), "islands", n,
          "removed inside figure", int((remove & figure).sum()))
