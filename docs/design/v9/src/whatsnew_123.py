"""1.23.0 Moonfall: the What's new painting, Menphina's Medallion only (plan v9 G8, deliverable 5).

Option B pipeline (docs/design/v8/spec-1.22.md W2, "Production recipe"): a painter at 2240 x 880 returning the
canvas and its region masks, saved at 1120 x 440 with the standard grain; the release's <key>.json; the Medallion
treatment from docs/design/v8/art/src/option_b_themes.py run on it unchanged; and the shipped JPEG (quality 88,
4:4:4, progressive), checked against the 600 KB budget.

The brief: a still lake at night. One near-full moon high on the left (Menphina; the one natural light) lays its
broken path on the water. Right of centre, a small lantern boat (the game's bucket B) rides the lake with one
traveller in it, looking up at a single falling star beyond the far hills: Moonfall. The paper lantern on the stern
post is the one warm practical light: it warms the traveller's near side, the stern and the post, and lays a broken
reflection on the water. Far ridges are backlit by nothing and lit only on their moon-facing slopes. Reeds frame the
near corners, rim-lit on their moon side. The one moon rule holds: a lobed sea mass, a 0.2 r unlit sliver, the moon
kept out of every brush pass.

Run: py -3 whatsnew_123.py        Outputs: ../art/moonfall-b-{base,medallion}.png, -masks.npz, .json, ../whatsnew-1.23.0.jpg
"""
import io
import json
import math
import pathlib
import sys

import numpy as np
from PIL import Image

V8SRC = pathlib.Path(__file__).resolve().parents[2] / "v8" / "art" / "src"
sys.path.insert(0, str(V8SRC))
from artlib import Canvas, blur, fbm, fbm1d, hexc, smooth  # noqa: E402
from paint_option_b2 import contact_shadow, finish, ridge_layer, zeros  # noqa: E402
from paint_option_b3 import glitter, moon_full, reflect, rims, seal_far, vignette  # noqa: E402
from paint_release import stars, tex_sample, top_edge  # noqa: E402

KEY = "moonfall-b"
V9 = pathlib.Path(__file__).resolve().parent.parent
ART = V9 / "art"
W, H = 2240, 880


def paint():
    c = Canvas(W, H)
    M = zeros()
    hz = 0.585 * H
    c.px = c.vgrad([(0, "#050A1E"), (0.40, "#0C1736"), (0.80, "#1B2A56"), (1.0, "#2A3C70")], 0, hz)
    c.px[int(hz):] = hexc("#2A3C70")
    mx, my, mr = 0.215 * W, 0.235 * H, 44.0
    c.add(hexc("#8EA6DC"), np.exp(-(c.radial(mx, my, 0.40 * W, 0.62 * H)) ** 2 * 2.0) * 0.24)
    sky_only = c.px.copy()

    def sky_lum(y):
        return 0.03 + 0.24 * min(1.0, max(0.0, y / hz)) ** 2

    stars(c, 230, 1231, hz * 0.92, sky_lum, near_moon=(mx, my, mr * 1.5), warm=0.10)
    # Menphina's moon, near full: the thin unlit sliver on its upper left (the sun far below the lower right)
    moon_full(c, M, mx, my, mr)
    # a faint band of altostratus low on the right, lit on the edges that face the moon
    cl = tex_sample(fbm(512, 512, 70, 6, 1232), c.xx / 5.0, c.yy / 1.0)
    dens = blur(np.clip((cl - 0.58) * 3.0 * np.exp(-((c.yy - 0.40 * H) / (0.04 * H)) ** 2), 0, 1), 2.0) * smooth(0.40 * W, 0.62 * W, c.xx)
    gy_, gx_ = np.gradient(blur(dens, 4))
    vx, vy = mx - c.xx, my - c.yy
    nn = np.sqrt(vx ** 2 + vy ** 2) + 1
    litc = np.clip(-(gx_ * vx / nn + gy_ * vy / nn) * 30, 0, 1) * dens
    ccol = np.stack([np.full((H, W), v, np.float32) for v in hexc("#28345F")], -1)
    ccol = ccol + (hexc("#AEBBE2") - ccol) * (litc * 0.8)[..., None]
    c.over(ccol, dens * 0.7)
    M["clouds"], M["under"] = dens, litc
    xs = np.arange(W, dtype=np.float32)
    # the far ridges across the lake: three ranges, paler with distance, lit only on their moon-facing slopes
    layers = []
    for i, (yb, amp, top, bot, seed, haze, f) in enumerate([(hz - 0.02 * H, 0.10, "#2A3A68", "#2E3F70", 1241, 0.45, 1.6),
                                                            (hz - 0.004 * H, 0.075, "#1E2B52", "#24335E", 1242, 0.35, 2.4),
                                                            (hz + 2, 0.045, "#151F3E", "#1A2648", 1243, 0.25, 3.6)]):
        before = M["far"].copy()
        ridge_layer(c, M, xs, yb, amp, W / f, seed, top, bot, mx, "#5F78AE", haze=haze)
        layers.append(np.clip(M["far"] - before, 0, 1))
    # the far shore's dark tree line, a serrated conifer edge on the nearest range
    ftop = hz - 0.012 * H - 0.012 * H * fbm1d(W, 200, 4, 1244)
    serr = 9 * (1 - np.abs(((xs / 13.0 + 3 * fbm1d(W, 50, 2, 1245)) % 1.0) - 0.5) * 2) ** 1.6 * (0.4 + 0.8 * fbm1d(W, 60, 2, 1246))
    trees = c.below_curve(ftop - serr, 1.0) * (c.yy < hz + 1)
    c.over(hexc("#0E1530"), trees)
    c.add(hexc("#8EA0D2"), rims(trees, -1, -1, 2) * 0.16)
    layers[-1] = np.maximum(layers[-1], trees)
    for i, m in enumerate(layers):
        M[f"far_{'abc'[i]}"] = m
    M["far"] = np.maximum(M["far"], trees)
    far_all = M["far"].copy()
    # the lake: everything from the far shore down
    lake = np.clip((c.yy - hz) / 1.2 + 0.5, 0, 1)
    M["field"] = lake

    # ---------------------------------------------------------------- the lantern boat, right of centre
    yw = 0.770 * H                        # its waterline
    bx0, bx1 = 0.585 * W, 0.735 * W       # bow (left) to stern (right)
    u = (c.xx - bx0) / (bx1 - bx0)
    sheer = yw - 26 - 18 * (np.abs(u - 0.5) * 2) ** 3                               # the gunwale rises toward bow and stern
    hull = (c.yy > sheer) * (c.yy < yw + 1.5) * (u > 0) * (u < 1)
    endcut = np.clip(1 - ((np.abs(u - 0.5) * 2 - 0.86) / 0.14), 0, 1)             # the stems slope in toward the waterline
    hull = hull * np.where(np.abs(u - 0.5) * 2 > 0.86, (c.yy < yw - (1 - endcut) * 40) * 1.0 + 0.0, 1.0)
    hull = blur(hull.astype(np.float32), 0.7)
    planks = tex_sample(fbm(256, 256, 3, 2, 1247), c.xx / 6.0, c.yy / 0.4)
    strake = 1 + 0.10 * smooth(0.55, 0.0, ((c.yy - sheer) % 7.0) / 7.0)
    hcol = c.vgrad([(0, "#1C1720"), (1, "#0E0B12")], yw - 50, yw) * (0.9 + 0.15 * planks)[..., None] * strake[..., None]
    c.over(hcol, hull)
    gun = np.clip(1 - np.abs(c.yy - sheer) / 2.2, 0, 1) * (u > 0.01) * (u < 0.99)
    c.over(hexc("#2E2630"), gun * 0.9)                                             # the gunwale's rail
    # the traveller, seated amidships facing the stern, head tilted up toward the falling star
    fx, fy = 0.655 * W, yw - 26
    P = lambda pts, s=1.25: [(fx + a * s, fy + b * s) for a, b in pts]
    fig = c.poly(P([(-20, 2), (22, 2), (20, -16), (12, -40), (-6, -44), (-16, -28)]), 0.7)
    fig = np.maximum(fig, c.ellipse(fx + 6 * 1.25, fy - 52 * 1.25, 10 * 1.25, 11.5 * 1.25, 0.7))
    fig = np.maximum(fig, c.poly(P([(-2, -58), (16, -62), (12, -50), (0, -48)]), 0.7))          # the hood, tipped back
    fig = np.maximum(fig, c.poly(P([(10, -36), (26, -24), (24, -16), (8, -26)]), 0.7))          # an arm on the knee
    fig = fig * (c.yy < yw - 4)
    # the stern post, curving up, with an arm and the paper lantern hanging from it
    post = np.zeros((H, W), np.float32)
    for k in range(60):
        tt = k / 59
        px_ = bx1 - 18 - 14 * tt ** 2
        py_ = yw - 30 - 120 * tt
        post = np.maximum(post, c.ellipse(px_, py_, 3.2, 3.2, 0.6))
    arm = c.poly([(bx1 - 32, yw - 148), (bx1 + 16, yw - 148), (bx1 + 16, yw - 144), (bx1 - 32, yw - 144)], 0.5)
    lx, ly = bx1 + 12, yw - 120
    cord = c.poly([(lx - 0.8, yw - 146), (lx + 0.8, yw - 146), (lx + 0.8, ly - 15), (lx - 0.8, ly - 15)], 0.4)
    lant = c.ellipse(lx, ly, 10, 14, 0.6)
    caps = np.maximum(c.poly([(lx - 7, ly - 16), (lx + 7, ly - 16), (lx + 7, ly - 12), (lx - 7, ly - 12)], 0.5),
                      c.poly([(lx - 6, ly + 12), (lx + 6, ly + 12), (lx + 6, ly + 16), (lx - 6, ly + 16)], 0.5))
    wood = np.maximum.reduce([post, arm, cord])
    c.over(hexc("#120E14"), wood)
    c.over(hexc("#0B0E1A"), fig)
    across = np.clip(1 - ((c.xx - lx) / 10) ** 2, 0, 1)
    paper = (hexc("#C46A30") * (1 - across[..., None]) + hexc("#FFD48E") * across[..., None])
    ribs = 1 - 0.20 * np.exp(-((((c.yy - ly + 14) % 5.0) - 2.5) ** 2) / 0.35)
    c.over(paper * ribs[..., None], lant)
    c.over(hexc("#2A1C12"), caps)
    boat = np.maximum.reduce([hull, wood, fig, caps])
    M["figs"] = np.maximum(boat, lant)
    M["lantern"] = lant
    M["post"] = np.maximum(post, np.maximum(arm, cord))

    # ---------------------------------------------------------------- reflections and the near bank
    src = c.px.copy()
    solid = np.maximum.reduce([far_all, boat, lant])
    src = sky_only * (1 - solid[..., None]) + src * solid[..., None]
    reflect(c, lake * (1 - boat * (c.yy < yw)), src, hz, [(far_all, hz), (boat, yw), (lant, yw)], 1248,
            amp=(1.0, 18.0), fres=(0.86, 0.45), deep="#0A1230", brk=0.55)
    c.over(hcol, hull * (c.yy < yw))                                                # the boat stays in front of its reflection
    c.over(hexc("#2E2630"), gun * 0.9 * (c.yy < yw))
    c.over(hexc("#120E14"), wood)
    c.over(hexc("#0B0E1A"), fig)
    c.over(paper * ribs[..., None], lant)
    c.over(hexc("#2A1C12"), caps)
    # the near bank: low, dark, with reeds in both corners
    bank = 0.935 * H + 0.03 * H * np.cos(xs / W * 4.2 + 0.6) + 6 * (fbm1d(W, 140, 3, 1249) - 0.5)
    bankm = c.below_curve(bank, 1.4)
    rng = np.random.default_rng(1250)
    reeds = np.zeros((H, W), np.float32)
    for side, (x0r, x1r, hmax) in enumerate(((0.0, 0.20 * W, 0.24 * H), (0.84 * W, W, 0.20 * H))):
        for _ in range(80):
            x = rng.uniform(x0r, x1r)
            edge_f = (1 - (x - x0r) / (x1r - x0r)) if side == 0 else ((x - x0r) / (x1r - x0r))
            hgt = hmax * (0.25 + 0.75 * edge_f) * (0.5 + 0.5 * rng.random())
            yb = bank[int(min(W - 1, x))] + 4
            lean = rng.uniform(-0.25, 0.25) * hgt
            reeds = np.maximum(reeds, c.poly([(x - 2.4, yb), (x + 2.4, yb), (x + lean + 0.6, yb - hgt), (x + lean - 0.6, yb - hgt)], 0.5))
            if rng.random() < 0.35:                                                 # a seed head
                reeds = np.maximum(reeds, c.ellipse(x + lean, yb - hgt - 8, 2.6, 10, 0.6))
    near = np.maximum(bankm, reeds)
    c.over(c.vgrad([(0, "#0E1526"), (1, "#070A14")], 0.80 * H, H), near)
    M["ridge"] = near

    # ---------------------------------------------------------------- the painterly pass, then the light
    finish(c, np.maximum.reduce([boat, lant, reeds, M["moon"]]), 1251, plain=M["moon"])
    # the falling star, far beyond the hills on the right: a thin straight streak (a meteor's path is straight),
    # brightest at its head, lighting nothing (a distant pinpoint by the lights rule); clear of the moon and the cloud.
    # Drawn after the pass and kept crisp by the treatments (json "lines": ["star", ...]).
    sx0, sy0, sx1, sy1 = 0.742 * W, 0.070 * H, 0.792 * W, 0.262 * H
    L = math.hypot(sx1 - sx0, sy1 - sy0)
    proj = ((c.xx - sx0) * (sx1 - sx0) + (c.yy - sy0) * (sy1 - sy0)) / (L * L)
    t = np.clip(proj, 0, 1)
    dperp = np.abs((c.xx - sx0) * (sy1 - sy0) - (c.yy - sy0) * (sx1 - sx0)) / L
    streak = np.exp(-(dperp / 1.2) ** 2) * t ** 1.8 * (proj > 0) * (proj < 1)
    hd = np.sqrt((c.xx - sx1) ** 2 + (c.yy - sy1) ** 2)
    c.add(hexc("#E8EEFF"), streak * 0.9)
    c.add(hexc("#F4F2EA"), np.exp(-(hd / 2.6) ** 2) * 1.0 + np.exp(-(hd / 10) ** 2) * 0.20)
    M["star"] = np.clip(streak * 2 + np.exp(-(hd / 4) ** 2), 0, 1)
    # the moon's broken path on the lake, under the moon
    glitter(c, mx, hz, lake * (1 - near) * (1 - boat), 1252, col="#FFF1D2", strength=0.85, w0=9.0, spread=0.28, thr=0.60)
    # moonlight rims on the edges that face the moon (upper left)
    c.add(hexc("#B8C6EE"), rims(boat, -1, -1, 2) * 0.45 * (c.yy < yw))
    c.add(hexc("#B8C6EE"), rims(reeds, -1, -1, 2) * 0.40)
    # the lantern: its glow, its light on the traveller's near (right) side, the post, the stern, and the water
    dl = np.sqrt((c.xx - lx) ** 2 + (c.yy - ly) ** 2)
    c.add(hexc("#FFB466"), np.exp(-(dl / 22) ** 2) * 0.50 + np.exp(-(dl / 70) ** 2) * 0.12)
    fall = 1 / (1 + (dl / 80) ** 2)
    c.add(hexc("#FFB062"), blur(rims(fig, 1, 0, 3), 0.8) * fall * 2.2)
    c.add(hexc("#FFB062"), rims(M["post"], 1, 0, 2) * fall * 1.2)
    c.add(hexc("#FFB062"), hull * (c.yy < yw) * smooth(0.68 * W, bx1, c.xx) * fall * 0.22)
    c.add(hexc("#FFB062"), gun * (c.yy < yw) * fall * 0.9)
    # its reflection: a broken warm column on the water below it, and a faint pool round the stern
    refl_y = 2 * yw - ly
    rip = tex_sample(fbm(512, 512, 5, 2, 1253), c.xx / 3.0, c.yy / 0.9)
    col_ = np.exp(-((c.xx - lx) / np.maximum(5 + (c.yy - yw) * 0.10, 1.0)) ** 2) * np.exp(-np.abs(c.yy - refl_y) / 70) * (c.yy > yw + 2)
    c.add(hexc("#FFB466"), col_ * np.clip((rip - 0.40) * 3, 0, 1) * lake * (1 - near) * 0.8)
    c.add(hexc("#FFB062"), np.exp(-(((c.xx - lx) / 120) ** 2 + ((c.yy - (yw + 10)) / 18) ** 2)) * lake * (c.yy > yw) * (1 - near) * 0.10)
    # the boat's contact on the water: a thin dark line where hull meets lake
    c.mul(hexc("#05070F"), np.exp(-((c.yy - yw - 1) / 2.0) ** 2) * (u > 0.02) * (u < 0.98) * 0.6)
    seal_far(M, ["far_a", "far_b", "far_c"])
    vignette(c, cy=0.42)
    return c, M


CFG = {
    "release": "1.23.0", "name": "Moonfall", "painter": "docs/design/v9/src/whatsnew_123.paint",
    "horizon": 0.585, "sun_glow": [0.215, 0.235], "dawn": 0.0, "dawn_lines": False,
    "moon": [0.215, 0.235, 22.0], "figure_zone": [640, 830], "figure_split": 2000, "bluff_split": 1.0,
    "glass_bars": [30, 430], "aether_domes": [[0.12, 0.14], [0.55, 0.12], [0.88, 0.16]],
    "sumi_bands": [[0.40, 0.66, 0.12, 0.032], [-0.06, 0.18, 0.90, 0.03]],
    "far_layers": ["far_a", "far_b", "far_c"], "field_flat": True, "lines": ["post", "star"],
    "note": "Medallion only (the owner's rule of 4 October 2026: one painting per release in the default theme). The other keys are kept so the five other treatments could still run.",
}


def run():
    ART.mkdir(exist_ok=True)
    c, M = paint()
    c.save(ART / f"{KEY}-base.png", (1120, 440), grain=0.008, seed=3)
    small = {k: np.asarray(Image.fromarray((np.clip(v, 0, 1) * 255).astype(np.uint8)).resize((1120, 440), Image.LANCZOS), np.float32) / 255
             for k, v in M.items()}
    np.savez_compressed(ART / f"{KEY}-masks.npz", **small)
    (ART / f"{KEY}.json").write_text(json.dumps(CFG, indent=2), encoding="utf-8")
    import option_b_themes as T
    T.OUT = ART
    base = T.load(KEY)
    out = T.medallion(base)
    im = Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
    im.save(ART / f"{KEY}-medallion.png", optimize=True)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=88, subsampling=0, progressive=True, optimize=True)
    (V9 / "whatsnew-1.23.0.jpg").write_bytes(buf.getvalue())
    kb = len(buf.getvalue()) / 1024
    print(f"whatsnew-1.23.0.jpg {kb:.1f} KB (budget 600 KB) {'OK' if kb <= 600 else 'OVER'}")


if __name__ == "__main__":
    run()
