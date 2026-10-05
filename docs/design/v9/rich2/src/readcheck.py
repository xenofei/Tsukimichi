"""Readability and colour checks for the fuller boards (level-method.md section 8, rules F6 and F7).

F6 (round 2, after the level critic's round 1; round 3: the ring is measured on the piece-free composite, so the
chrome and the lantern spill count, critic R4): for each peg kind, its median lit face (luma, 80th percentile in the
disc, measured on the composites) is set against the scene round every position it can be dealt to:
  * orange: every canBeOrange piece; blue, green, purple: every peg;
  * movers at 24 points along their whole path;
  * bricks: the face inside the brick against a band 2-9 units outside it, for the colour dealt;
measured at 1x and at 0.8x (the 640 x 480 window). The margin is face minus the 90th percentile of the veiled scene in
a ring 2-9 units outside the piece. Rule: every margin >= 0.20, and no kind's worst-case margin more than 0.02 below
the approved board's (the rich pass's scene through the same measurement).

F7 (round 2, game designer M1; round 3: exclusive windows, critic R2 and designer N4): two jewels. Over the board
opening, pixels with OKLab chroma above 0.03 are binned by hue in 30-degree bins. The first jewel is the window of
three bins centred on the fullest bin; the second is the fullest window of three bins centred at least 60 degrees away,
counting only bins outside the first window (no bin counts for both). Rule: the second jewel holds at least 15% of the coloured pixels,
and the mean chroma of coloured pixels is at least 0.06.

Protan (round 3, UX m4): orange pegs against the ground round them, as a protanope sees them (Machado 2009,
severity 1): per orange peg, the OKLab a/b distance from the peg's core (the inner half) to the mean of a ring 12-20
px from its centre (at 1x); the 10th percentile over the orange pegs must be at least 0.12, or no worse than the
approved board's less 0.02.

  py -3 readcheck.py                 writes supervisor/readcheck.json and prints a table
"""
import json
import math

import numpy as np
from PIL import Image

from r2lib import RICH, RICH2, LUM, load_rgb, srgb_to_oklab
from board import engine_colours, load_level, pieces, veil, mover_pos
from composite2 import PILOTS

KINDS = ("blue", "orange", "green", "purple")


def faces_from(comp, level, colours, S=1):
    out = {}
    for i, (k, d) in enumerate(pieces(level)):
        if k != "peg" or "move" in d:
            continue
        x, y, r = d["x"] * S, d["y"] * S, d.get("r", 10) * S
        yy, xx = np.mgrid[int(y - r):int(y + r) + 1, int(x - r):int(x + r) + 1]
        m = (xx - x) ** 2 + (yy - y) ** 2 < (r * 0.85) ** 2
        v = comp[yy[m].clip(0, comp.shape[0] - 1), xx[m].clip(0, comp.shape[1] - 1)] @ LUM
        out.setdefault(colours[i], []).append(np.percentile(v, 80))
    return {k: float(np.median(v)) for k, v in out.items()}


def ring_p90(Y, x, y, r, s):
    """90th percentile of luma Y (at s px per unit) in the ring r+2..r+9 units round (x, y)."""
    X0, X1 = int((x - r - 10) * s), int((x + r + 10) * s) + 1
    Y0, Y1 = int((y - r - 10) * s), int((y + r + 10) * s) + 1
    yy, xx = np.mgrid[max(0, Y0):min(Y.shape[0], Y1), max(0, X0):min(Y.shape[1], X1)]
    dd = np.sqrt(((xx + 0.5) / s - x) ** 2 + ((yy + 0.5) / s - y) ** 2)
    m = (dd > r + 2) & (dd < r + 9)
    return float(np.percentile(Y[yy[m], xx[m]], 90)) if m.any() else 0.0


def scenes(which, lid):
    stem = PILOTS[lid][0]
    base = RICH if which == "rich" else RICH2
    return load_rgb(base / "scenes" / f"{stem}@2x.png")


def backdrop(which, lid):
    """The board with every piece removed, as the player sees what lies behind a peg: scene, veil, chrome, bucket and
    its lantern spill (at 2x)."""
    level = load_level(RICH / "levels" / f"{lid}.json")
    gone = set(range(len(pieces(level))))
    if which == "rich":
        import composite as rc
        stem, bk = PILOTS[lid][0], ("cart" if lid.startswith("base") else "boat")
        img, _ = rc.render(lid, stem, bk, 1, S=2, gone=gone)
    else:
        import composite2
        img, _ = composite2.render(lid, S=2, gone=gone)
    return img.px


def worst_cases(bd2, level, faces, colours):
    """Per kind and scale: the worst margin over every eligible position (and where); bd2 is the piece-free board."""
    out = {}
    for scale_name, s in (("1x", 1.0), ("0.8x", 0.8)):
        im = Image.fromarray((np.clip(bd2, 0, 1) * 255).astype(np.uint8)).resize((int(800 * s), int(600 * s)), Image.LANCZOS)
        Y = np.asarray(im, np.float32) / 255 @ LUM
        for kind in KINDS:
            face = faces[kind]
            worst = (9.0, None)
            for i, (k, d) in enumerate(pieces(level)):
                if k != "peg":
                    continue
                if kind == "orange" and not d.get("canBeOrange", False):
                    continue
                pts = [mover_pos(d, d["move"]["period"] * j / 24) for j in range(24)] if d.get("move") else [(d["x"], d["y"])]
                for (x, y) in pts:
                    mg = face - ring_p90(Y, x, y, d.get("r", 10), s)
                    if mg < worst[0]:
                        worst = (mg, (round(x), round(y)))
            out[f"{kind} {scale_name}"] = [round(worst[0], 3), worst[1]]
    return out


def brick_margins(bd2, comp, level, colours):
    Y = np.asarray(Image.fromarray((np.clip(bd2, 0, 1) * 255).astype(np.uint8)).resize((800, 600), Image.LANCZOS),
                   np.float32) / 255 @ LUM
    C = comp @ LUM
    from board import sd_line_capsule, sd_arc_capsule
    yy, xx = np.mgrid[0:600, 0:800].astype(np.float32)
    X, Yg = xx + 0.5, yy + 0.5
    res = []
    for i, (k, d) in enumerate(pieces(level)):
        if k != "brick":
            continue
        ht = d.get("thickness", 20) / 2
        sd = (sd_line_capsule(X, Yg, d["x1"], d["y1"], d["x2"], d["y2"], ht) if d["kind"] == "line"
              else sd_arc_capsule(X, Yg, d["x"], d["y"], d["r"], d["start"], d["sweep"], ht))
        inside, band = sd < -1.5, (sd > 2) & (sd < 9)
        if inside.any() and band.any():
            res.append((round(float(np.percentile(C[inside], 80) - np.percentile(Y[band], 90)), 3), colours[i]))
    return min(res) if res else None


def jewels(sc):
    lab = srgb_to_oklab(sc[82:1188, 150:1450])
    a, b = lab[..., 1], lab[..., 2]
    Cc = np.sqrt(a * a + b * b)
    hue = (np.degrees(np.arctan2(b, a)) + 360) % 360
    col = Cc > 0.03
    bins = np.bincount((hue[col] // 30).astype(int), minlength=12).astype(float)
    tot = max(bins.sum(), 1)
    win = lambda i: {(i - 1) % 12, i % 12, (i + 1) % 12}
    first = int(np.argmax(bins))                       # the first jewel's window is centred on the fullest bin
    taken = win(first)
    far = [i for i in range(12) if min(abs(i - first), 12 - abs(i - first)) >= 2]
    second = max(far, key=lambda i: sum(bins[j] for j in win(i) - taken))
    return {"coloured_px_pct": round(float(col.mean()) * 100, 1), "mean_chroma": round(float(Cc[col].mean()), 3),
            "first_jewel_hue": first * 30 + 15, "first_share": round(float(sum(bins[j] for j in taken) / tot), 3),
            "second_jewel_hue": second * 30 + 15,
            "second_share": round(float(sum(bins[j] for j in win(second) - taken) / tot), 3),
            "bins_pct": [round(float(b / tot) * 100, 1) for b in bins]}


PROTAN = np.array([[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]])


def protan_orange(comp, level, colours):
    lin = np.where(comp <= 0.04045, comp / 12.92, ((comp + 0.055) / 1.055) ** 2.4)
    sim = np.clip(lin @ PROTAN.T, 0, 1)
    sim = np.where(sim <= 0.0031308, sim * 12.92, 1.055 * np.power(sim, 1 / 2.4) - 0.055).astype(np.float32)
    lab = srgb_to_oklab(sim)
    out = []
    for i, (k, d) in enumerate(pieces(level)):
        if k != "peg" or colours[i] != "orange" or "move" in d:
            continue
        x, y, r = d["x"], d["y"], d.get("r", 10)
        yy, xx = np.mgrid[int(y - r - 22):int(y + r + 22), int(x - r - 22):int(x + r + 22)]
        ok = (xx >= 0) & (yy >= 0) & (xx < 800) & (yy < 600)
        yy, xx = yy[ok], xx[ok]
        dd = np.sqrt((xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2)
        core = lab[yy[dd < r * 0.5], xx[dd < r * 0.5]][:, 1:].mean(0)
        rm = (dd >= 12) & (dd <= 20)
        ring = lab[yy[rm], xx[rm]][:, 1:].mean(0)
        out.append(float(np.sqrt(((ring - core) ** 2).sum())))
    return round(float(np.percentile(out, 10)), 3) if out else None


def main():
    report = {}
    all_faces = {}
    data = {}
    for lid in PILOTS:
        path = RICH / "levels" / f"{lid}.json"
        level = load_level(path)
        colours = engine_colours(path, 5, 1)
        comp = load_rgb(RICH2 / "composites" / f"{lid}.png")
        f = faces_from(comp, level, colours)
        data[lid] = (level, colours, comp, f)
        for k, v in f.items():
            all_faces.setdefault(k, []).append(v)
    gfaces = {k: float(np.median(v)) for k, v in all_faces.items()}
    fails = []
    for lid, (level, colours, comp, f) in data.items():
        faces = {k: f.get(k, gfaces[k]) for k in KINDS}
        bd_new, bd_old = backdrop("rich2", lid), backdrop("rich", lid)
        new = worst_cases(bd_new, level, faces, colours)
        old = worst_cases(bd_old, level, faces, colours)
        rows = {}
        for key in new:
            drop = round(old[key][0] - new[key][0], 3)
            rows[key] = {"worst": new[key][0], "at": new[key][1], "approved": old[key][0], "drop": drop}
            if new[key][0] < 0.20 or drop > 0.02:
                fails.append(f"{lid} {key} worst {new[key][0]} (approved {old[key][0]})")
        bm = brick_margins(bd_new, comp, level, colours)
        pr = protan_orange(comp, level, colours)
        pr_old = protan_orange(load_rgb(RICH / "composites" / f"{lid}.png"), level, colours)
        if pr is not None and pr < 0.12 and pr < (pr_old or 0) - 0.02:
            fails.append(f"{lid} protan orange separation {pr} (approved {pr_old})")
        jw = jewels(scenes("rich2", lid))
        jw_old = jewels(scenes("rich", lid))
        if jw["second_share"] < 0.15 or jw["mean_chroma"] < 0.06:
            fails.append(f"{lid} jewels {jw}")
        report[lid] = {"faces": {k: round(v, 3) for k, v in faces.items()}, "worst_case": rows,
                       "bricks_min_margin": bm, "jewels": jw, "jewels_approved": jw_old,
                       "protan_orange_p10": pr, "protan_orange_p10_approved": pr_old}
        w1 = min(r["worst"] for r in rows.values())
        print(f"{lid}: worst margin {w1:.3f}; drops " +
              ", ".join(f"{k} {r['drop']:+.3f}" for k, r in rows.items() if r["drop"] > 0.005) +
              f"; bricks {bm}; jewels 2nd {jw['second_hue'] if 'second_hue' in jw else jw['second_jewel_hue']} "
              f"{jw['second_share']:.2f} chroma {jw['mean_chroma']:.3f} (approved {jw_old['second_share']:.2f}, "
              f"{jw_old['mean_chroma']:.3f}); protan {pr} (approved {pr_old})")
    report["fails"] = fails
    (RICH2 / "supervisor" / "readcheck.json").write_text(json.dumps(report, indent=1) + "\n")
    print("FAILS:" if fails else "all pass", *fails, sep="\n  ")


if __name__ == "__main__":
    main()
