"""Jewels chosen up front, and the runtime's scene limits checked at author time (speed-up items 3, 9 October 2026).

40% of author builds were refused by deterministic rules found only after the full build; F7 (the two jewels' share and
separation), F9 (the orange pegs' protan margin) and F6 (the pegs' faces) led them. `mfl.py generate` therefore picks
the jewel pair as well as the layout:

- `pairs(neighbours)`: candidate (first, second) hues in the charter's plan (1.2): the first in the night sky's
  violet-indigo 240-345, the second outside 30-110 (the orange pegs' and the gilt's family), 70 or more apart, and 40
  or more from each neighbour's pair either way round (stagecheck.same_jewels needs 30), most distinct first;
- `jewel_block(h1, h2, ...)`: the dress's palette and jewel for the pair, the second jewel a region round the subject
  (its silhouette or its lines), faded within 60 units of the walls (F7's wall rule), chroma 0.28;
- `choose(...)`: dresses the variant with each pair in turn and measures F6, F7 and F9 exactly as the build does
  (dress.dress, the composites, readability.measure) until one passes all three; the result is written next to the
  variant as v<k>.dress.json for the author to merge into the recipe;
- `runtime_limits(recipe)`: what the runtime refuses at integration: at most 6 terms in any mask (a region's quiet
  counts, the converter adds it), a shaft's `near` of 0 or more, a region's chroma of 0.3 or less.
"""
import json
import math

from . import paths, stagecheck

FIRST = (260, 275, 245, 290, 305, 320, 335)
# the second jewel's hues in order of preference: rose and cyan-blue keep the orange pegs' protan margin (F9) better
# than greens behind them (1-4's rose 346 over a blue 261 reads 0.112; the first trials' greens 0.085-0.095)
SECOND = (345, 0, 200, 215, 185, 330, 170, 155, 230, 140)
MASK_TERMS_MAX = 6
REGION_CHROMA_MAX = 0.3
WALL_FADE = [["x", 75, 135], ["x", 725, 665]]


def hue_dist(a, b):
    return min(abs(a - b) % 360, 360 - abs(a - b) % 360)


def pairs(neighbours=(), apart=70, neigh=40):
    """Candidate pairs, most distinct from the neighbours first. `neighbours`: [(h1, h2)] of the levels either side."""
    out = []
    for h1 in FIRST:
        for h2 in SECOND:
            if hue_dist(h1, h2) < apart or 30 <= h2 <= 110:
                continue
            d = min([max(min(hue_dist(h1, a), hue_dist(h2, b)), min(hue_dist(h1, b), hue_dist(h2, a)))
                     for (a, b) in neighbours if a is not None and b is not None] or [180])
            # a pair is "the same palette" when both hues lie within 30 either way round: keep 40 in at least one
            same = any(stagecheck.same_jewels((h1, h2), (a, b)) or
                       (max(hue_dist(h1, a), hue_dist(h2, b)) < neigh and max(hue_dist(h1, b), hue_dist(h2, a)) < neigh)
                       for (a, b) in neighbours if a is not None and b is not None)
            if not same:
                out.append((-min(d, 60), SECOND.index(h2) + FIRST.index(h1), h1, h2))
    return [(h1, h2) for (_d, _a, h1, h2) in sorted(out)]


def oklch_hex(L, C, h):
    a, b = C * math.cos(math.radians(h)), C * math.sin(math.radians(h))
    l_ = (L + 0.3963377774 * a + 0.2158037573 * b) ** 3
    m_ = (L - 0.1055613458 * a - 0.0638541728 * b) ** 3
    s_ = (L - 0.0894841775 * a - 1.2914855480 * b) ** 3
    rgb = (4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_,
           -1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_,
           -0.0041960863 * l_ - 0.7034186147 * m_ + 1.7076147010 * s_)

    def enc(c):
        c = min(max(c, 0.0), 1.0)
        return 12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055
    return "#" + "".join(f"{round(enc(c) * 255):02X}" for c in rgb)


def jewel_block(h1, h2, subject_mask, chroma=0.2):
    """The palette and jewel of a dress for the pair; `subject_mask`: the second jewel's region mask terms (the wall
    fade is added); `chroma`: the region's."""
    palette = {"name": f"generated {h1}/{h2}", "sky": oklch_hex(0.24, 0.10, h1), "deep": oklch_hex(0.13, 0.06, h1),
               "jewel1": oklch_hex(0.45, 0.18, h1), "jewel2": oklch_hex(0.68, 0.14, h2), "warm": "#FFB45E",
               "rim": oklch_hex(0.86, 0.08, h2)}
    jewel = {"bands": [[0, oklch_hex(0.56, 0.18, h1)], [300, oklch_hex(0.52, 0.17, h1)], [600, oklch_hex(0.44, 0.15, h1)]],
             "valueHues": [[0.08, oklch_hex(0.32, 0.12, h1)], [0.45, oklch_hex(0.90, 0.06, h1)]],
             "chroma": 1.3, "floor": 0.04,
             "regions": [{"hex": oklch_hex(0.62, 0.17, h2), "chroma": chroma, "mask": list(subject_mask) + WALL_FADE}],
             "quiet": [40, 18]}
    return palette, jewel


LIGHT_MASK = [["y", 330, 190], ["luma", 0.2, 0.36, 4]]     # the painting's lit upper parts (1-4's region)


def subject_mask(gen):
    """The second jewel's region: the declared silhouette (poly), else the subject's longest line (near), else a band
    across the middle of the board."""
    if gen.declared:
        return [["poly", gen.declared[0], 40]]
    lines = sorted((s for s in gen.shapes if s["subject"] and s["kind"] in ("polyline", "silhouette")),
                   key=lambda s: -sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(s["pts"], s["pts"][1:])))
    if lines:
        return [["near", lines[0]["name"], 90, 170]]
    return [["y", 180, 240], ["y", 470, 410]]


def runtime_limits(recipe):
    """What the runtime refuses at integration, for this recipe's dress: the problems, empty if none."""
    from .convert import mask_terms
    probs = []
    d = recipe.get("dress") or {}
    j = d.get("jewel") or {}
    for k, r in enumerate(j.get("regions", [])):
        try:
            n = len(mask_terms(r.get("mask", []), recipe)) + (1 if j.get("quiet") else 0)
        except (KeyError, ValueError, SystemExit) as e:
            probs.append(f"region {k}: a mask term with no runtime form ({e})")
            continue
        if n > MASK_TERMS_MAX:
            probs.append(f"region {k}: {n} mask terms with the quiet ({MASK_TERMS_MAX} at most)")
        if r.get("chroma", 0.1) > REGION_CHROMA_MAX:
            probs.append(f"region {k}: chroma {r['chroma']} ({REGION_CHROMA_MAX} at most)")
    keeps = j.get("keepMask")
    for name, spec in ([("keepMask", keeps)] if keeps else []) + [(f"tone {k}", t.get("mask", [])) for k, t in
                                                                     enumerate(recipe.get("tone") or [])]:
        try:
            n = len(mask_terms(spec, recipe))
        except (KeyError, ValueError, SystemExit) as e:
            probs.append(f"{name}: a mask term with no runtime form ({e})")
            continue
        if n > MASK_TERMS_MAX:
            probs.append(f"{name}: {n} mask terms ({MASK_TERMS_MAX} at most)")
    for k, sh in enumerate(d.get("shafts") or []):
        if sh.get("near", 150.0) < 0:
            probs.append(f"shaft {k}: near {sh['near']} (0 or more)")
    return probs


def measure(level, recipe, number, stage, path):
    """F6, F7 and F9 for a level over its recipe, as the build measures them: (report, fails)."""
    import numpy as np  # noqa: F401
    from . import readability
    from .build import _scene_cached, colours_for, orange_views
    from .composite import render
    from .dress import dress
    g2 = _scene_cached(recipe, 2)
    d2, _ctx = dress(recipe, g2, level, 2)
    _seed, cols = colours_for(path, level, number)
    everything = set(range(len(level["pegs"]) + len(level["bricks"])))
    comp2 = render(level, recipe, d2, cols, S=2, stage=stage, number=number)
    base2 = render(level, recipe, g2, cols, S=2, stage=stage, number=number)
    bd_new = render(level, recipe, d2, cols, S=2, gone=everything, stage=stage, number=number)
    bd_old = render(level, recipe, g2, cols, S=2, gone=everything, stage=stage, number=number)
    views = orange_views(level, recipe, d2, cols, stage, number)
    return readability.measure(level, cols, comp2, base2, bd_new, bd_old, d2, views, undressed2=g2)


def neighbour_pairs(meta):
    """The jewel pairs of the levels either side in play order (their reports), if built."""
    out = []
    for nb in (meta["number"] - 1, meta["number"] + 1):
        lid = f"{str(meta['id']).split('-')[0]}-{nb:02d}"
        p = paths.REPORT / f"{lid}.json"
        if p.exists():
            jw = json.loads(p.read_text(encoding="utf-8")).get("checks", {}).get("readcheck", {}).get("report", {}).get("jewels", {})
            out.append((jw.get("first_jewel_hue"), jw.get("second_jewel_hue")))
    return out


def masks(gen):
    """The second jewel's regions, narrowest first: the painting's lit upper parts, the subject, the board's lower
    half, the whole board (each faded at the walls)."""
    return [("lit parts", LIGHT_MASK), ("subject", subject_mask(gen)), ("lower half", [["y", 260, 360]]), ("board", [])]


def choose(gen, path, tries=6, log=print):
    """A pair whose dress passes F6, F7 and F9 on this variant, searched by what each measurement fails: a second
    jewel under its 15% share gets a broader region and more chroma; hues measured under 60 apart (a painting pulls
    the second jewel toward its own colours) move to a pair further apart; a protan margin under 0.12 lowers the
    region's chroma. Returns the passing (or best) {pair, palette, jewel, fails, measured}."""
    meta = gen.meta
    level = gen.level()
    best = None
    ps = pairs(neighbour_pairs(meta))
    ms = masks(gen)
    pi, mi, chroma = 0, 0, 0.15
    for _ in range(tries):
        (h1, h2), (mname, mask) = ps[pi], ms[mi]
        palette, jewel = jewel_block(h1, h2, mask, chroma)
        dress_ = dict(gen.recipe.get("dress") or {}, palette=palette, jewel=jewel)
        recipe = dict(gen.recipe, dress=dress_)
        limits = runtime_limits(recipe)
        rrep, rfails = measure(level, recipe, meta["number"], meta["stage"], path)
        keyed = [f for f in rfails if f.startswith(("F6", "F7", "F9"))] + limits
        jw = rrep["jewels"]
        log(f"    jewels {h1}/{h2} (region {mname}, chroma {chroma:.2f}): measured {jw['first_jewel_hue']}/{jw['second_jewel_hue']}, second share "
            f"{jw['second_share']}, protan {rrep['protan_orange_p10']}, F6 worst {rrep['worst_margin']}"
            + (f"; {'; '.join(keyed)}" if keyed else "; F6, F7, F9 and the runtime limits pass"))
        cand = (len(keyed), {"pair": [h1, h2], "palette": palette, "jewel": jewel, "fails": keyed,
                              "measured": [jw["first_jewel_hue"], jw["second_jewel_hue"]]})
        if best is None or cand[0] < best[0]:
            best = cand
        if not keyed:
            break
        if jw["apart_deg"] is None or jw["apart_deg"] < 64:
            # the next pair at least 20 further apart than the hues measured (or the next one)
            want = (jw["apart_deg"] or 0) + 20
            pi = next((k for k in range(pi + 1, len(ps)) if hue_dist(*ps[k]) >= max(want, hue_dist(*ps[pi]) + 10)),
                      min(pi + 1, len(ps) - 1))
        if jw["second_share"] < 0.17:
            mi, chroma = min(mi + 1, len(ms) - 1), min(chroma + 0.05, REGION_CHROMA_MAX)
        if any(f.startswith("F9") for f in keyed):
            chroma = max(chroma - 0.06, 0.08)
    return best[1]


def selftest(verbose=True):
    """The pair rule and the runtime limits on known cases (no rendering)."""
    cases = []
    ps = pairs([(270, 180)])
    cases.append(("pairs: every pair 70 apart, the second outside 30-110, none the neighbour's palette",
                  bool(ps) and all(hue_dist(a, b) >= 70 and not 30 <= b <= 110 and not stagecheck.same_jewels((a, b), (270, 180))
                                   for a, b in ps)))
    cases.append(("pairs: the first is far from the neighbour's", max(hue_dist(ps[0][0], 270), hue_dist(ps[0][1], 180)) >= 40))
    hx = oklch_hex(0.6, 0.0, 0)
    cases.append(("oklch: a grey of L 0.6 is grey", hx[1:3] == hx[3:5] == hx[5:7]))
    r = {"features": {"f": [[100, 100], [200, 200]]}, "dress": {"jewel": {"quiet": [50, 22], "regions": [
        {"hex": "#00FF00", "chroma": 0.35, "mask": [["x", 1, 2], ["x", 3, 4], ["y", 1, 2], ["y", 3, 4], ["near", "f", 10, 20],
                                                     ["x", 5, 6]]}]},
        "shafts": [{"near": -20}]}}
    probs = runtime_limits(r)
    cases.append(("runtime limits: 7 mask terms with the quiet, chroma 0.35, a shaft near -20 are all refused",
                  len(probs) == 3))
    good = {"features": {}, "dress": {"jewel": {"quiet": [50, 22], "regions": [{"hex": "#00FF00", "chroma": 0.28,
                                                                                  "mask": [["x", 75, 135], ["x", 725, 665]]}]},
                                      "shafts": [{"near": 0}]}}
    cases.append(("runtime limits: a two-term region at chroma 0.28 and a shaft near 0 pass", not runtime_limits(good)))
    ok = True
    for name, g in cases:
        ok &= bool(g)
        if verbose:
            print(f"  {'ok ' if g else 'BAD'} jewels: {name}")
    return ok
