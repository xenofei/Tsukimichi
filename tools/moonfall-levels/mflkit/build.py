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
import time

import numpy as np
from PIL import Image

from . import author, engine, frames, readability, scene
from .composite import render
from .paths import ASSETS, BUILD, COMPOSITES, JSON_OUT, LAYOUTS, REPORT, SHARED
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


def _scene_cached(recipe, S):
    """The graded scene, cached in the shared cache folder by the recipe's source and overlays."""
    painter_src = ""
    if recipe["source"]["kind"] == "painting":
        from .paths import PAINTERS
        painter_src = (PAINTERS / f"{recipe['source']['painter']}.py").read_text(encoding="utf-8")
    from . import cache
    from .paths import CACHE, cache_name
    tex = cache.file_hash(CACHE / cache_name(recipe["source"]["texture"])) if recipe["source"]["kind"] == "game" else ""
    # the grading code (the kit) and the game texture are in the key: the folder is shared by every worktree
    key = hashlib.sha1(json.dumps([recipe["source"], recipe.get("overlays"), recipe.get("vignette"), recipe.get("seed"),
                                   recipe.get("tone"), recipe.get("masks"), recipe.get("features") if recipe.get("tone") else None,
                                   painter_src, S, tex, cache.kit_hash()], sort_keys=True).encode()).hexdigest()[:16]
    p = SHARED / "scenes" / f"{recipe['name']}-{key}{'@2x' if S == 2 else ''}.png"
    if p.exists():
        return RL.load_rgb(p)
    px = scene.graded(recipe, S)
    tmp = p.with_name(f"{p.stem}.{os.getpid()}.tmp.png")
    RL.save_rgb(px, tmp)
    os.replace(tmp, p)
    return RL.load_rgb(p)


def _named(text, board):
    """The loader's `pegs[7]` / `bricks[22]` (file order) as the layout's own names: `peg 'tag' (x, y)`."""
    import re

    def name(m):
        piece = (board.pegs if m.group(1) == "pegs" else board.bricks)
        i = int(m.group(2))
        return board.label(piece[i]) if i < len(piece) else m.group(0)
    return re.sub(r"\b(pegs|bricks)\[(\d+)\]", name, text)


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




def orange_views(level, recipe, scene2, cols, stage, number):
    """Composites with every orange candidate dealt orange, at moments of the movers' cycle (at t = 0 only when the
    level has no movers), with each candidate's place at that moment: what F9 measures."""
    from board import mover_pos, pieces as all_pieces
    allp = all_pieces(level)
    movers = [d for k, d in allp if k == "peg" and d.get("move")]
    period = max((d["move"]["period"] for d in movers), default=0)
    times = [0.0] if not movers else [period * k / 4 for k in range(4)]
    oc = [("orange" if d.get("canBeOrange", True) else c) for (k, d), c in zip(allp, cols)]
    views = []
    for t in times:
        img = render(level, recipe, scene2, oc, S=2, stage=stage, number=number, t=t)
        places = [(*mover_pos(d, t), d.get("r", 10)) for k, d in allp if k == "peg" and d.get("canBeOrange", True)]
        places += [("brick", d) for k, d in allp if k == "brick" and d.get("canBeOrange", False)]
        views.append((img, places))
    return views


def build(level_id, keep2x=False, log=print, keep_going=True, cheap_only=False):
    """One level end to end. The cheap phase is the pre-flight, the loader, the sweep, the pocket rule and the rule's 48
    games (about 3 s). With keep_going False the build stops after it when it found a fault (nothing slow runs for a level
    that will be refused); cheap_only runs just that phase and writes nothing (`mfl.py check`). The report's "problems"
    list is the machine-readable form of "fails": {rule, piece (layout tag), x, y, hint, message}."""
    t0 = time.time()
    meta, recipe, b = make_board(level_id)
    number, stage = meta["number"], meta["stage"]
    level = b.level_json(meta["id"], meta["name"], meta["scene"])
    rep = {"id": meta["id"], "name": meta["name"], "stage": stage, "number": number, "scene": meta["scene"],
           "subject": meta.get("subject"), "technique": meta.get("technique"),
           "source": recipe["source"].get("texture") or f"our painting ({recipe['source'].get('painter')})",
           "checks": {}, "fails": [], "problems": []}
    fails = rep["fails"]

    def add(rule, msg, piece=None, hint="", at=None):
        """A fault: the report's text ("rule: message", as always) and its machine-readable twin."""
        tag, x, y = b.where(piece) if piece is not None else ("", None, None)
        if at is not None:
            x, y = at
        if hasattr(msg, "as_dict"):                       # a pre-flight Problem carries its own rule, piece and hint
            d = msg.as_dict()
            rep["problems"].append(dict(d, rule=f"{rule}:{d['rule']}" if d["rule"] else rule))
        else:
            rep["problems"].append({"rule": rule, "piece": tag, "x": x, "y": y, "hint": hint, "message": str(msg)})
        fails.append(f"{rule}: {msg}")

    def finish_early(why):
        rep["seconds"] = round(time.time() - t0, 1)
        rep["verdict"] = "PASS" if not fails else "REFUSED"
        rep["stopped"] = why
        if cheap_only:
            return rep
        REPORT.mkdir(parents=True, exist_ok=True)
        (REPORT / f"{level_id}.json").write_text(json.dumps(rep, indent=1) + "\n", encoding="utf-8")
        log(f"[{level_id}] REFUSED ({why}):\n  " + "\n  ".join(fails))
        return rep

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
    for q in probs:
        add("preflight", q)
    log(f"[{level_id}] preflight: {n} pieces, {len(probs)} problems" + "".join(f"; warning: {w}" for w in b.warnings))

    # 2. the shipped loader; the sweep (stuck balls, dead first shots); the pocket rule; the greedy player
    cand = BUILD / "json" / f"{level_id}.json"
    author.write_json(level, cand)
    ok, out = engine.validate(cand)
    rep["checks"]["loader"] = {"ok": ok, "output": out.splitlines()}
    if not ok:
        add("loader", " | ".join(_named(l.strip(), b) for l in out.splitlines()[1:4]), hint="the shipped loader refused the file: fix what its lines name")
        log(f"[{level_id}] loader FAIL")
        if cheap_only or not keep_going:
            return finish_early("cheap phase" + ("" if cheap_only else "; --keep-going runs the rest"))
    else:
        sw = engine.sweep(cand, number)
        rep["checks"]["sweep"] = sw
        if sw["stuck_share"] >= engine.STUCK_MAX_SHARE:
            add("sweep", f"the stuck rule fired on {sw['stuck']} of {sw['angles']} first shots",
                hint="`mfl.py stuck <id>` shows where balls rest: tilt or remove the pieces that hold them")
        if sw["no_hit"] > 0:
            add("sweep", f"{sw['no_hit']} first-shot angles hit nothing (every aim should meet a piece)",
                hint="`mfl.py dead <id>` shows where those shots fly: put a piece in their lane")
        rc = engine.reach(cand, number)
        rep["checks"]["reach"] = rc
        if rc["never"]:
            pcs = [b.piece_by_index(i) for i in rc["never"]]
            pcs = [q for q in pcs if q is not None]
            for q in pcs:
                add("reach", f"{b.label(q)} is never reached, even once everything before them clears", q,
                    "move it into a first free flight, or into the lee of a piece that clears early")
            if not pcs:
                add("reach", f"pieces {rc['never']} are never reached, even once everything before them clears")
        rule = engine.play(cand, number, games=engine.GREEDY_GAMES)
        if rule["greedy_won"] < engine.GREEDY_MIN_WINS:
            add("play", f"the greedy player won {rule['greedy_won']} of 48 (at least {engine.GREEDY_MIN_WINS})",
                hint="open the board: `mfl.py ease <id>` ranks the places that are left orange")
        rep["checks"]["play"] = {k: rule[k] for k in ("greedy_won", "games", "oranges_left_when_lost", "random_won")}
        log(f"[{level_id}] cheap phase: loader ok; sweep stuck {sw['stuck']}/{sw['angles']}, dead {sw['no_hit']}, never reached "
            f"{len(rc['never'])}; greedy {rule['greedy_won']}/48")
        if cheap_only or (fails and not keep_going):
            return finish_early("cheap phase" + ("" if cheap_only else "; --keep-going runs the rest"))
        pl = engine.play(cand, number, games=engine.HELD_GAMES, first=engine.RAMP_GAMES)
        pl.update(greedy_won=rule["greedy_won"], games=rule["games"], oranges_left_when_lost=rule["oranges_left_when_lost"],
                  random_won=rule["random_won"], random_oranges_mean=rule["random_oranges_mean"], ramp_first_seed=engine.RAMP_GAMES + 1)
        rep["checks"]["play"] = pl
        for c in engine.cheap(pl, engine.piece_homes(cand)):
            add("play", f"cheap difficulty: {c}", hint="move orange candidates out of the lower third (`mfl.py ease <id>`)")
        log(f"[{level_id}] loader ok; sweep stuck {sw['stuck']}/{sw['angles']}, dead {sw['no_hit']}, never reached "
            f"{len(rc['never'])}; greedy {pl['greedy_won']}/48, ramp {pl['ramp_per_48']}/48 over {pl['ramp_games']} held-out games")

    # 3. the scene: graded, its ceiling, the dress and the framing rules
    g1, g2 = _scene_cached(recipe, 1), _scene_cached(recipe, 2)
    ceil = scene.ceiling(g1, recipe.get("ceilingExempt", ()))
    rep["checks"]["ceiling"] = {"p99_luma": round(ceil, 3), "limit": CEILING}
    if ceil > CEILING:
        add("ceiling", f"the graded scene's 99th-percentile luma is {ceil:.3f} (limit {CEILING})",
            hint="darken the scene's grade or its brightest feature in the recipe")
    from .dress import dress
    d2, ctx = dress(recipe, g2, level, 2)
    frep, ffails = frames.check(level, ctx.cover, ctx.rim, ctx.lights, S=2)
    near, nbad = frames.darkened_near_pieces(level, g2, d2)
    frep["darkened_min_clearance"] = None if near is None else round(near, 2)
    if nbad:
        ffails.append("F3a-pixels")
    frep["elements_dropped_for_clearance"] = ctx.dropped
    rep["checks"]["framecheck"] = {"report": frep, "fails": ffails}
    for f in ffails:
        add("framecheck", f, hint="see level-method.md section 3 for this frame rule")
    d1 = np.asarray(Image.fromarray(RL.to_u8(d2)).resize((800, 600), Image.LANCZOS), np.float32) / 255
    RL.save_rgb(d2, BUILD / "dressed" / f"{level_id}@2x.png")
    RL.save_rgb(d1, BUILD / "dressed" / f"{level_id}.png")
    log(f"[{level_id}] scene: ceiling {ceil:.3f}; framing {frep['cover_pct']}%, fails {ffails or 'none'}")

    # 4. colours (the engine's own deal), composites and readability
    if ok:
        seed, cols = colours_for(cand, level, number)
        rep["checks"]["colours"] = {"seed": seed, "counts": {c: cols.count(c) for c in sorted(set(cols))}}
        everything = set(range(len(level["pegs"]) + len(level["bricks"])))
        comp2 = render(level, recipe, d2, cols, S=2, stage=stage, number=number)
        comp1 = np.asarray(Image.fromarray(RL.to_u8(comp2)).resize((800, 600), Image.LANCZOS), np.float32) / 255
        base2 = render(level, recipe, g2, cols, S=2, stage=stage, number=number)
        base1 = np.asarray(Image.fromarray(RL.to_u8(base2)).resize((800, 600), Image.LANCZOS), np.float32) / 255
        bd_new = render(level, recipe, d2, cols, S=2, gone=everything, stage=stage, number=number)
        bd_old = render(level, recipe, g2, cols, S=2, gone=everything, stage=stage, number=number)
        views = orange_views(level, recipe, d2, cols, stage, number)
        rrep, rfails = readability.measure(level, cols, comp2, base2, bd_new, bd_old, d2, views, undressed2=g2)
        rep["checks"]["readcheck"] = {"report": rrep, "fails": rfails}
        for f in rfails:
            add("readcheck", f, hint="see level-method.md section 5 for this readability rule")
        RL.save_rgb(comp2, BUILD / "composites" / f"{level_id}@2x.png")
        RL.save_rgb(comp1, BUILD / "composites" / f"{level_id}.png")
        RL.save_rgb(base1, BUILD / "composites" / f"{level_id}-undressed.png")
        bd1 = np.asarray(Image.fromarray(RL.to_u8(bd_new)).resize((800, 600), Image.LANCZOS), np.float32) / 255
        RL.save_rgb(bd1, BUILD / "composites" / f"{level_id}-cleared.png")
        jw = rrep["jewels"]
        log(f"[{level_id}] readcheck: worst margin {rrep['worst_margin']}, largest drop {rrep['largest_drop']}, "
            f"jewels {jw['first_jewel_hue']}/{jw['second_jewel_hue']} ({jw['apart_deg']} apart, {jw['second_share']}), "
            f"protan {rrep['protan_orange_p10']}, ghost {rrep['ghost_median']}; fails {rfails or 'none'}")

    rep["seconds"] = round(time.time() - t0, 1)
    rep["verdict"] = "PASS" if not fails else "REFUSED"
    REPORT.mkdir(parents=True, exist_ok=True)
    (REPORT / f"{level_id}.json").write_text(json.dumps(rep, indent=1) + "\n", encoding="utf-8")
    if fails:
        log(f"[{level_id}] REFUSED:\n  " + "\n  ".join(fails))
        return rep
    author.write_json(level, JSON_OUT / f"{level_id}.json")
    RL.save_rgb(comp1, COMPOSITES / f"{level_id}.png")
    if keep2x:
        RL.save_rgb(comp2, COMPOSITES / f"{level_id}@2x.png")
    if recipe["source"]["kind"] == "painting":
        RL.save_rgb(g1, ASSETS / f"{recipe['name']}.jpg", quality=88)
        RL.save_rgb(g2, ASSETS / f"{recipe['name']}@2x.jpg", quality=88)
    log(f"[{level_id}] PASS in {rep['seconds']} s")
    return rep
