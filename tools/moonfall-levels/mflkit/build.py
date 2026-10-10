"""One level, end to end: layout source + scene recipe -> checked level file, scene, composites and a report.

The order: the self-tests (once per run), the pre-flight, the shipped loader, the first-shot sweep, 48 seeded greedy
games, the graded scene and its value ceiling, the dress and the framing rules, the engine's colours, the composites
(1x and 2x) and readability (F6 at 1x and 0.8x at the worst placements, F7, F9 protan). Every check runs and is
reported; if any fails, nothing is written to json/ or composites/ (the build refuses the level).
"""
import hashlib
import importlib.util
import json
import os
import threading
import time

import numpy as np
from PIL import Image

from . import author, engine, frames, readability, scene
from . import memo  # noqa: F401
from .composite import render, render_async
from .paths import ASSETS, BUILD, COMPOSITES, JSON_OUT, LAYOUTS, REPORT
import rich_lib as RL

CEILING = 0.465               # method section 5: the scene's 99th-percentile luma about 0.46 or less


def load_layout(level_id):
    spec = importlib.util.spec_from_file_location(f"layout_{level_id.replace('-', '_')}", LAYOUTS / f"{level_id}.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def make_board(level_id):
    mod = load_layout(level_id)
    meta = dict(mod.LEVEL)
    recipe = scene.load_recipe(meta["scene"])
    b = author.Board(recipe)
    b.level_id = level_id
    mod.build(b)
    return meta, recipe, b


def save_atomic(px, path, **kw):
    """RL.save_rgb through a temporary name, so two builds that share a scene (--jobs) never meet on a half-written file."""
    tmp = path.with_name(f"{path.stem}.{os.getpid()}.{threading.get_ident()}.tmp{path.suffix}")
    RL.save_rgb(px, tmp, **kw)
    os.replace(tmp, path)


def _scene_cached(recipe, S):
    """The graded scene, cached in build/ by the recipe's source and overlays."""
    painter_src = ""
    if recipe["source"]["kind"] == "painting":
        from .paths import PAINTERS
        painter_src = (PAINTERS / f"{recipe['source']['painter']}.py").read_text(encoding="utf-8")
    key = hashlib.sha1(json.dumps([recipe["source"], recipe.get("overlays"), recipe.get("vignette"), recipe.get("seed"),
                                   recipe.get("tone"), recipe.get("masks"), recipe.get("features") if recipe.get("tone") else None,
                                   painter_src, S], sort_keys=True).encode()).hexdigest()[:12]
    p = BUILD / "scenes" / f"{recipe['name']}-{key}{'@2x' if S == 2 else ''}.png"
    if p.exists():
        return RL.load_rgb(p)
    px = scene.graded(recipe, S)
    save_atomic(px, p)
    return RL.load_rgb(p)


def green_faults(level, cols):
    """The pieces a deal turned green although the file keeps them from green (file order: pegs, then bricks)."""
    return [i for i, (p, c) in enumerate(zip(level["pegs"] + level["bricks"], cols)) if c == "green" and not p.get("canBeGreen", True)]


def colours_for(path, level, number, seed=1):
    """The engine's own deal for this level number (it honours canBeGreen since format v2 shipped). A deal that puts a
    green on a piece the file keeps from green would be an engine fault, so it is refused here."""
    cols = engine.colours(path, number, seed)
    bad = green_faults(level, cols)
    if bad:
        raise RuntimeError(f"the engine dealt green to never-green pieces {bad}")
    return seed, cols




def orange_views_start(level, recipe, scene2, cols, stage, number, pool=None):
    """Composites with every orange candidate dealt orange, at moments of the movers' cycle (at t = 0 only when the
    level has no movers), with each candidate's place at that moment: what F9 measures. Returns [(handle, places)]; a
    handle's .result() is the composite (the renders run in the pool, when there is one)."""
    from board import mover_pos, pieces as all_pieces
    allp = all_pieces(level)
    movers = [d for k, d in allp if k == "peg" and d.get("move")]
    period = max((d["move"]["period"] for d in movers), default=0)
    times = [0.0] if not movers else [period * k / 4 for k in range(4)]
    oc = [("orange" if d.get("canBeOrange", True) else c) for (k, d), c in zip(allp, cols)]
    views = []
    for t in times:
        h = render_async(pool, level, recipe, scene2, oc, S=2, stage=stage, number=number, t=t)
        places = [(*mover_pos(d, t), d.get("r", 10)) for k, d in allp if k == "peg" and d.get("canBeOrange", True)]
        places += [("brick", d) for k, d in allp if k == "brick" and d.get("canBeOrange", False)]
        views.append((h, places))
    return views


def orange_views(level, recipe, scene2, cols, stage, number):
    return [(h.result(), places) for (h, places) in orange_views_start(level, recipe, scene2, cols, stage, number)]


def start_games(path, number):
    """The greedy-player runs of a build (the rule's 48 games and the 1,728 held-out ones) as two background jobs: they
    are mfcheck processes, so they overlap the Python phases. Returns (executor, rule future, held-out future)."""
    from concurrent.futures import ThreadPoolExecutor
    ex = ThreadPoolExecutor(2)
    return (ex, ex.submit(engine.play, path, number, engine.GREEDY_GAMES),
            ex.submit(engine.play, path, number, engine.HELD_GAMES, engine.RAMP_GAMES))


def build(level_id, keep2x=False, log=print, pool=None):
    """Builds one level. `pool` (composite.start_pool()) renders the composites in worker processes, and the games
    always run in the background; without a pool the composites render here, one after another. The report and every
    picture are the same either way."""
    bg = []                                  # executors to wait for if the build ends early
    try:
        return _build(level_id, keep2x, log, pool, bg)
    finally:
        for ex in bg:
            ex.shutdown(wait=True, cancel_futures=True)


def _build(level_id, keep2x, log, pool, bg):
    t0 = time.time()
    from concurrent.futures import ThreadPoolExecutor
    saver = ThreadPoolExecutor(4)             # PNG encoding releases the GIL: the pictures are written side by side
    bg.append(saver)
    saves = []

    def put(px, path, **kw):
        saves.append(saver.submit(save_atomic, px, path, **kw))
    meta, recipe, b = make_board(level_id)
    number, stage = meta["number"], meta["stage"]
    level = b.level_json(meta["id"], meta["name"], meta["scene"])
    rep = {"id": meta["id"], "name": meta["name"], "stage": stage, "number": number, "scene": meta["scene"],
           "subject": meta.get("subject"), "technique": meta.get("technique"),
           "source": recipe["source"].get("texture") or f"our painting ({recipe['source'].get('painter')})",
           "checks": {}, "fails": []}
    fails = rep["fails"]

    # 1. the pre-flight (layout rules, format v2's greens)
    probs = b.check(verbose=False, number=number)
    n = len(level["pegs"]) + len(level["bricks"])
    rep["checks"]["preflight"] = {"problems": probs, "pieces": n, "pegs": len(level["pegs"]), "bricks": len(level["bricks"]),
                                  "movers": sum(1 for p in level["pegs"] if "move" in p),
                                  "orange_candidates": sum(p["canBeOrange"] for p in level["pegs"] + level["bricks"]),
                                  "never_green": sum(not p.get("canBeGreen", True) for p in level["pegs"] + level["bricks"]),
                                  "skipped_while_tracing": [list(s) for s in b.skipped]}
    pf = rep["checks"]["preflight"]
    pf["brick_share"] = round(pf["bricks"] / max(n, 1), 3)
    pf["candidate_share"] = round(pf["orange_candidates"] / max(n, 1), 3)
    pf["lower_third_share"] = round(engine.low_share(engine.piece_homes_of(level)), 3)
    pf["warnings"] = list(b.warnings)
    fails += [f"preflight: {p}" for p in probs]
    log(f"[{level_id}] preflight: {n} pieces, {len(probs)} problems" + "".join(f"; warning: {w}" for w in b.warnings))

    # 2. the shipped loader; the sweep (stuck balls, dead first shots); the pocket rule; the greedy player (started
    #    here and read back below, once the scene work is done)
    cand = BUILD / "json" / f"{level_id}.json"
    author.write_json(level, cand)
    ok, out = engine.validate(cand)
    rep["checks"]["loader"] = {"ok": ok, "output": out.splitlines()}
    games = play_at = None
    if not ok:
        fails.append("loader: " + " | ".join(out.splitlines()[1:4]))
        log(f"[{level_id}] loader FAIL")
    else:
        games = start_games(cand, number)
        bg.append(games[0])
        sw = engine.sweep(cand, number)
        rep["checks"]["sweep"] = sw
        if sw["stuck_share"] >= engine.STUCK_MAX_SHARE:
            fails.append(f"sweep: the stuck rule fired on {sw['stuck']} of {sw['angles']} first shots")
        if sw["no_hit"] > 0:
            fails.append(f"sweep: {sw['no_hit']} first-shot angles hit nothing (every aim should meet a piece)")
        rc = engine.reach(cand, number)
        rep["checks"]["reach"] = rc
        if rc["never"]:
            fails.append(f"reach: pieces {rc['never']} are never reached, even once everything before them clears")
        rep["checks"]["play"] = None                 # keeps its place in the report; filled in when the games are done
        play_at = len(fails)

    # 3. the scene: graded, its ceiling, the dress and the framing rules
    g1, g2 = _scene_cached(recipe, 1), _scene_cached(recipe, 2)
    if ok:                                           # the renders that need only the graded scene start now
        seed, cols = colours_for(cand, level, number)
        everything = set(range(len(level["pegs"]) + len(level["bricks"])))
        h_base2 = render_async(pool, level, recipe, g2, cols, S=2, stage=stage, number=number)
        h_bd_old = render_async(pool, level, recipe, g2, cols, S=2, gone=everything, stage=stage, number=number)
    ceil = scene.ceiling(g1, recipe.get("ceilingExempt", ()))
    rep["checks"]["ceiling"] = {"p99_luma": round(ceil, 3), "limit": CEILING}
    if ceil > CEILING:
        fails.append(f"ceiling: the graded scene's 99th-percentile luma is {ceil:.3f} (limit {CEILING})")
    from .dress import dress
    d2, ctx = dress(recipe, g2, level, 2)
    if ok:
        h_comp2 = render_async(pool, level, recipe, d2, cols, S=2, stage=stage, number=number)
        h_bd_new = render_async(pool, level, recipe, d2, cols, S=2, gone=everything, stage=stage, number=number)
        h_views = orange_views_start(level, recipe, d2, cols, stage, number, pool)
    frep, ffails = frames.check(level, ctx.cover, ctx.rim, ctx.lights, S=2)
    near, nbad = frames.darkened_near_pieces(level, g2, d2)
    frep["darkened_min_clearance"] = None if near is None else round(near, 2)
    if nbad:
        ffails.append("F3a-pixels")
    frep["elements_dropped_for_clearance"] = ctx.dropped
    rep["checks"]["framecheck"] = {"report": frep, "fails": ffails}
    fails += [f"framecheck: {f}" for f in ffails]
    d1 = np.asarray(Image.fromarray(RL.to_u8(d2)).resize((800, 600), Image.LANCZOS), np.float32) / 255
    put(d2, BUILD / "dressed" / f"{level_id}@2x.png")
    put(d1, BUILD / "dressed" / f"{level_id}.png")
    log(f"[{level_id}] scene: ceiling {ceil:.3f}; framing {frep['cover_pct']}%, fails {ffails or 'none'}")

    # 4. colours (the engine's own deal), composites and readability
    if ok:
        rep["checks"]["colours"] = {"seed": seed, "counts": {c: cols.count(c) for c in sorted(set(cols))}}
        comp2 = h_comp2.result()
        comp1 = np.asarray(Image.fromarray(RL.to_u8(comp2)).resize((800, 600), Image.LANCZOS), np.float32) / 255
        base2 = h_base2.result()
        base1 = np.asarray(Image.fromarray(RL.to_u8(base2)).resize((800, 600), Image.LANCZOS), np.float32) / 255
        bd_new, bd_old = h_bd_new.result(), h_bd_old.result()
        views = [(h.result(), places) for (h, places) in h_views]
        rrep, rfails = readability.measure(level, cols, comp2, base2, bd_new, bd_old, d2, views, undressed2=g2)
        rep["checks"]["readcheck"] = {"report": rrep, "fails": rfails}
        fails += [f"readcheck: {f}" for f in rfails]
        put(comp2, BUILD / "composites" / f"{level_id}@2x.png")
        put(comp1, BUILD / "composites" / f"{level_id}.png")
        put(base1, BUILD / "composites" / f"{level_id}-undressed.png")
        bd1 = np.asarray(Image.fromarray(RL.to_u8(bd_new)).resize((800, 600), Image.LANCZOS), np.float32) / 255
        put(bd1, BUILD / "composites" / f"{level_id}-cleared.png")
        jw = rrep["jewels"]
        log(f"[{level_id}] readcheck: worst margin {rrep['worst_margin']}, largest drop {rrep['largest_drop']}, "
            f"jewels {jw['first_jewel_hue']}/{jw['second_jewel_hue']} ({jw['apart_deg']} apart, {jw['second_share']}), "
            f"protan {rrep['protan_orange_p10']}, ghost {rrep['ghost_median']}; fails {rfails or 'none'}")

    # the games, read back now (they ran while the pictures were made); their failures go where they always stood
    if games:
        rule, pl = games[1].result(), games[2].result()
        pl.update(greedy_won=rule["greedy_won"], games=rule["games"], oranges_left_when_lost=rule["oranges_left_when_lost"],
                  random_won=rule["random_won"], random_oranges_mean=rule["random_oranges_mean"], ramp_first_seed=engine.RAMP_GAMES + 1)
        rep["checks"]["play"] = pl
        pfails = []
        if pl["greedy_won"] < engine.GREEDY_MIN_WINS:
            pfails.append(f"play: the greedy player won {pl['greedy_won']} of 48 (at least {engine.GREEDY_MIN_WINS})")
        pfails += [f"play: cheap difficulty: {c}" for c in engine.cheap(pl, engine.piece_homes(cand))]
        fails[play_at:play_at] = pfails
        log(f"[{level_id}] loader ok; sweep stuck {sw['stuck']}/{sw['angles']}, dead {sw['no_hit']}, never reached "
            f"{len(rc['never'])}; greedy {pl['greedy_won']}/48, ramp {pl['ramp_per_48']}/48 over {pl['ramp_games']} held-out games")

    for f in saves:
        f.result()
    saves.clear()
    rep["seconds"] = round(time.time() - t0, 1)
    rep["verdict"] = "PASS" if not fails else "REFUSED"
    REPORT.mkdir(parents=True, exist_ok=True)
    (REPORT / f"{level_id}.json").write_text(json.dumps(rep, indent=1) + "\n", encoding="utf-8")
    if fails:
        log(f"[{level_id}] REFUSED:\n  " + "\n  ".join(fails))
        return rep
    author.write_json(level, JSON_OUT / f"{level_id}.json")
    put(comp1, COMPOSITES / f"{level_id}.png")
    if keep2x:
        put(comp2, COMPOSITES / f"{level_id}@2x.png")
    if recipe["source"]["kind"] == "painting":
        put(g1, ASSETS / f"{recipe['name']}.jpg", quality=88)
        put(g2, ASSETS / f"{recipe['name']}@2x.jpg", quality=88)
    for f in saves:
        f.result()
    log(f"[{level_id}] PASS in {rep['seconds']} s")
    return rep
