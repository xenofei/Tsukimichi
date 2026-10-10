"""Every checker proves itself before it is trusted: known-bad shapes must fail and known-good ones must pass (the
supervisors' rule since rich pass 2, round 3). `mfl.py build` runs this first and stops if any case is wrong."""
import json
import math
import os

from . import author, challenge, engine, frames, precheck, readability, reviewcmds, stagecheck
from .paths import BUILD, RICH


def _sealed_level():
    """25 orange pegs, each sealed inside a closed ring of brick: no ball can ever reach one."""
    pegs, bricks = [], []
    k = 0
    for row, y in enumerate((230, 300, 370, 440, 510)):
        for col in range(5):
            x = 150 + col * 120 + (row % 2) * 40
            pegs.append({"x": x, "y": y, "r": 6, "canBeOrange": True})
            bricks.append({"kind": "arc", "x": x, "y": y, "r": 20, "start": 0, "sweep": 360, "thickness": 8,
                           "canBeOrange": False})
            k += 1
    for x in (130, 670):
        for y in (200, 260):
            pegs.append({"x": x, "y": y, "canBeOrange": False})
    return {"format": "moonfall-level", "version": 2, "id": "selftest-sealed", "name": "Sealed", "scene": "none",
            "playfield": {"width": 800, "height": 600}, "pegs": pegs, "bricks": bricks}


def _flat_decks_level():
    """Four level decks of seven bricks with a clear chute down the middle: balls rest on the decks (the stuck gate's
    known-bad, the critic's round-1 case) and a straight-down shot touches nothing (the dead-shot gate's)."""
    bricks, pegs = [], []
    for (x0, y) in ((110, 230), (110, 390), (480, 310), (480, 470)):
        for k in range(7):
            bricks.append({"kind": "line", "x1": x0 + 30 * k, "y1": y, "x2": x0 + 30 * k + 27, "y2": y, "thickness": 12,
                           "canBeOrange": False, "canBeGreen": True})
    for (x, y) in ((120, 180), (180, 180), (240, 180), (560, 180), (620, 180), (680, 180), (130, 300), (200, 300), (270, 300),
                   (500, 250), (570, 250), (640, 250), (130, 460), (200, 460), (270, 460), (520, 400), (590, 400), (660, 400),
                   (150, 520), (650, 520), (300, 250), (300, 340), (520, 360), (690, 330), (110, 340), (690, 430)):
        pegs.append({"x": x, "y": y, "r": 9, "canBeOrange": True, "canBeGreen": True})
    return {"format": "moonfall-level", "version": 2, "id": "selftest-decks", "name": "Decks", "scene": "none",
            "playfield": {"width": 800, "height": 600}, "pegs": pegs, "bricks": bricks}


def engine_cases(verbose=True):
    engine.ensure_built()
    BUILD.mkdir(parents=True, exist_ok=True)
    good = RICH / "levels" / "base-p2.json"
    pilot = json.loads(good.read_text(encoding="utf-8"))
    bad = json.loads(json.dumps(pilot))
    bad["pegs"][0].update(x=400.0, y=120.0)                 # inside the launcher's swing
    bad_path = BUILD / "selftest-launcher.json"
    bad_path.write_text(json.dumps(bad), encoding="utf-8")
    sealed = BUILD / "selftest-sealed.json"
    sealed.write_text(json.dumps(_sealed_level()), encoding="utf-8")
    decks = BUILD / "selftest-decks.json"
    decks.write_text(json.dumps(_flat_decks_level()), encoding="utf-8")
    # every piece low on the board (a ball bounces at most about 160 units up), and one peg against the ceiling in the
    # top corner: nothing can ever carry a ball up there
    corner = {"format": "moonfall-level", "version": 2, "id": "selftest-corner", "name": "Corner", "scene": "none",
              "playfield": {"width": 800, "height": 600}, "bricks": [],
              "pegs": [{"x": 110 + 34 * (k % 18), "y": 440 + 40 * (k // 18), "r": 9, "canBeOrange": k < 30, "canBeGreen": True}
                       for k in range(36)]}
    corner["pegs"].append({"x": 92.0, "y": 14.0, "r": 8, "canBeOrange": False})
    corner_path = BUILD / "selftest-corner.json"
    corner_path.write_text(json.dumps(corner), encoding="utf-8")
    ok_good, _ = engine.validate(good)
    ok_bad, _ = engine.validate(bad_path)
    ok_sealed, _ = engine.validate(sealed)
    ok_decks, msg = engine.validate(decks)
    pl_good = engine.play(good, 5, games=48)
    play_good = pl_good["greedy_won"]
    cheap_good = engine.cheap(pl_good, engine.piece_homes(good))
    # cheap difficulty on synthetic results (the ratio of low oranges among those left to their share of the deal; the
    # lower-third share of the candidates is the pre-flight's, tested in author.py): low candidates a fifth of the
    # deal but half of what is left, and an even spread
    homes = [((100 + 20 * k, 480.0 if k < 6 else 250.0), True) for k in range(30)]
    one = {"holdouts": [{"piece": 0, "at": [100, 480], "share_of_lost_games": 0.40}], "low_left_share": 0.2}
    ratio = {"holdouts": [], "low_left_share": 0.5}
    even = {"holdouts": [], "low_left_share": 0.22}
    share_30 = [((100 + 20 * k, 450.0 if k < 9 else 250.0), True) for k in range(30)]
    share_40 = [((100 + 20 * k, 450.0 if k < 12 else 250.0), True) for k in range(30)]
    edge = [((100.0, 399.9), True), ((120.0, 400.0), True)]

    # the colours gate: a green dealt to a never-green piece
    lv = {"pegs": [{"x": 1, "y": 1, "canBeGreen": False}, {"x": 2, "y": 2}], "bricks": []}
    from .build import green_faults
    play_sealed = engine.play(sealed, 5, games=48)["greedy_won"] if ok_sealed else -1
    sw_good, sw_decks = engine.sweep(good, 5), engine.sweep(decks, 5)
    rc_good, rc_corner = engine.reach(good, 5), engine.reach(corner_path, 5)
    n_corner = len(corner["pegs"]) - 1
    # a challenge's orange count reaches the engine: the standard 25 given outright plays the same games, and 1 orange
    # is no harder than 25 (the carrier check, mflkit/challenge.py)
    play_25 = engine.play(good, 5, games=48, oranges=25)["greedy_won"]
    play_1 = engine.play(good, 5, games=48, oranges=1)["greedy_won"]
    cases = [("play: 25 oranges given outright are the standard games", play_25 == play_good, True),
             (f"play: 1 orange wins {play_1} of 48, no fewer than 25 ({play_good})", play_1 >= play_good, True),
             ("loader: an approved pilot", ok_good, True), ("loader: a peg in the launcher's swing", ok_bad, False),
             ("loader: the sealed and the decks boards load (their faults are play)", ok_sealed and ok_decks, True),
             (f"play: an approved pilot wins {play_good} of 48", play_good >= engine.GREEDY_MIN_WINS, True),
             (f"play: oranges sealed in rings win {play_sealed} of 48", play_sealed >= engine.GREEDY_MIN_WINS, False),
             (f"stuck gate: a pilot fires on {sw_good['stuck']} of {sw_good['angles']}", sw_good["stuck_share"] < engine.STUCK_MAX_SHARE, True),
             (f"stuck gate: four level decks fire on {sw_decks['stuck']} of {sw_decks['angles']}", sw_decks["stuck_share"] < engine.STUCK_MAX_SHARE, False),
             (f"dead shots: a pilot has {sw_good['no_hit']}", sw_good["no_hit"] == 0, True),
             (f"dead shots: a clear chute has {sw_decks['no_hit']}", sw_decks["no_hit"] == 0, False),
             (f"reach: a pilot leaves {len(rc_good['never'])} unreached", not rc_good["never"], True),
             (f"reach: a peg tucked in the top corner is never reached ({rc_corner['never']})", n_corner in rc_corner["never"], True),
             (f"cheap difficulty: an approved pilot ({cheap_good})", not cheap_good, True),
             ("cheap difficulty: low candidates 20% of the deal, 50% of what is left", not engine.cheap(ratio, homes), False),
             ("cheap difficulty: low oranges left in their share", not engine.cheap(even, homes), True),
             ("cheap difficulty: one low orange left in 40% of lost games is no longer a gate (most pieces are candidates)",
              not engine.cheap(one, homes), True),
             ("lower-third share: 30% of the candidates (9 of 30) is the most", engine.low_share(share_30) <= engine.LOW_SHARE_MAX + 1e-9, True),
             ("lower-third share: 40% is over", engine.low_share(share_40) <= engine.LOW_SHARE_MAX + 1e-9, False),
             ("lower-third share: the third starts at y 400", engine.low_share(edge) == 0.5, True),

             ("colours: a green dealt to a never-green piece", not green_faults(lv, ["green", "blue"]), False),
             ("colours: a green where greens may go", not green_faults(lv, ["blue", "green"]), True)]
    ok = True
    for name, got, want in cases:
        ok &= got == want
        if verbose:
            print(f"  {'ok ' if got == want else 'BAD'} engine: {name}: {got}, expected {want}")
    if verbose and not ok_decks:
        print("   ", msg)
    return ok


def pipeline_cases(verbose=True):
    """The fast loop and the caches prove themselves: the self-test stamp reruns when any file it reads changes (and not for
    a new level), `check` passes a good level and writes nothing, its findings name the piece's tag and place, the --json
    document has its shape, and `trace-many` flies the same balls as `trace`."""
    import contextlib
    import importlib
    import io
    import pathlib
    import tempfile
    from . import cache
    from .paths import JSON_OUT, LAYOUTS, REPORT
    cases = []
    saved = (cache.DIR, cache.kit_files, cache.selftest_key, globals()["run"])
    try:
        # the stamp covers every file of the pipeline's code
        names = {f.name for f in saved[1]()}
        code = {f.name for f in pathlib.Path(__file__).parent.glob("*.py")} | {"mfl.py"}
        cases.append(("stamp: every mflkit/*.py and mfl.py is in the key", code <= names, True))
        fx = fixture_files()
        cases.append(("stamp: the fixtures are the pilots and the converter's levels, not every level",
                      not any(LAYOUTS in f.parents for f in fx) and all(f.name in {f"base-{i:02d}.json" for i in range(1, 11)} | {"base-25.json"}
                                                                        for f in fx if JSON_OUT in f.parents), True))
        with tempfile.TemporaryDirectory() as td:
            td = pathlib.Path(td)
            probe = td / "probe.py"
            probe.write_text("x = 1\n", encoding="utf-8")
            cache.kit_files = lambda: saved[1]() + [probe]
            k1, k2 = cache.selftest_key([]), cache.selftest_key([])
            probe.write_text("x = 22\n", encoding="utf-8")                  # change one kit file
            k3 = cache.selftest_key([])
            fixture = td / "fixture.json"
            fixture.write_text("{}", encoding="utf-8")
            f1 = cache.selftest_key([fixture])
            fixture.write_text('{"a": 1}', encoding="utf-8")                # change one fixture
            f2 = cache.selftest_key([fixture])
            cases.append(("stamp: the same inputs give the same key", k1 == k2, True))
            cases.append(("stamp: changing one kit file changes the key", k3 != k2, True))
            cases.append(("stamp: changing one fixture changes the key", f1 != f2, True))
            # the stamp itself, and run_cached against a fake run
            cache.DIR = td / "levels"
            cache.write_stamp("probe", "abc" * 10)
            cases.append(("stamp: written and read back by its key", bool(cache.read_stamp("probe", "abc" * 10)), True))
            cases.append(("stamp: another key finds nothing", cache.read_stamp("probe", "abd" * 10) is None, True))
            runs = []
            cache.kit_files = saved[1]
            keys = iter(["k1", "k1", "k1", "k2", "k2"])
            cache.selftest_key = lambda _fx: next(keys)
            globals()["run"] = lambda verbose=False: (runs.append(1), True)[1]
            with contextlib.redirect_stdout(io.StringIO()):
                run_cached()                       # a miss: runs, stamps
                run_cached()                       # a hit: skipped
                run_cached(force=True)             # --force-selftest: runs
                run_cached()                       # another key: runs
            cases.append((f"stamp: run, skip, forced run, changed key run (ran {len(runs)} times, expected 3)", len(runs) == 3, True))
            globals()["run"] = lambda verbose=False: False
            cache.selftest_key = lambda _fx: "kfail"
            with contextlib.redirect_stdout(io.StringIO()):
                run_cached()
            cases.append(("stamp: a failing run writes no stamp", cache.read_stamp("selftest", "kfail") is None, True))
    finally:
        cache.DIR, cache.kit_files, cache.selftest_key, globals()["run"] = saved
    # check: a good level passes and nothing is written
    from . import build as B
    before = (REPORT / "base-01.json").stat().st_mtime_ns if (REPORT / "base-01.json").exists() else None
    rep = B.build("base-01", cheap_only=True, log=lambda *_: None)
    after = (REPORT / "base-01.json").stat().st_mtime_ns if (REPORT / "base-01.json").exists() else None
    cases.append(("check: base-01 passes the cheap phase", rep["verdict"] == "PASS" and not rep["problems"], True))
    cases.append(("check: it writes no report", before == after, True))
    # findings name the piece's layout tag and place
    b = author.Board()
    b.peg(300, 300, tag="alpha")
    b.peg(310, 300, tag="beta")
    b.line(150, 156, 210, 156, tag="gamma")
    probs = b.check(verbose=False)
    ov = next((p for p in probs if p.rule == "overlap"), None)
    dk = next((p for p in probs if p.rule == "level deck"), None)
    cases.append(("findings: an overlap names both pegs' tags and places",
                  bool(ov) and "'alpha'" in ov and "'beta'" in ov and ov.tag == "alpha" and (ov.x, ov.y) == (300, 300) and bool(ov.hint), True))
    cases.append(("findings: a level deck names the brick's tag and midpoint, not its index",
                  bool(dk) and "'gamma'" in dk and (dk.x, dk.y) == (180, 156) and "brick 0" not in dk, True))
    # --json
    mfl = importlib.import_module("mfl")
    reps = {"x-1": {"verdict": "REFUSED", "checks": {}, "fails": ["preflight:overlap: pegs overlap"],
                    "problems": [dict(ov.as_dict(), rule="preflight:overlap")] if ov else []},
            "x-2": {"verdict": "PASS", "checks": {"sweep": {"stuck": 1, "angles": 700, "no_hit": 0}}, "fails": [], "problems": []}}
    out = io.StringIO()
    mfl._emit_json("check", reps, stream=out)
    doc = json.loads(out.getvalue())
    keys_ok = all(set(f) >= {"level", "rule", "piece", "x", "y", "hint", "message"} for f in doc["fails"])
    cases.append(("json: {command, verdict, fails[{level, rule, piece, x, y, hint, message}], figures{level}}",
                  set(doc) >= {"command", "verdict", "fails", "figures"} and doc["verdict"] == "REFUSED" and keys_ok and len(doc["fails"]) == 1
                  and set(doc["figures"]) == {"x-1", "x-2"} and doc["figures"]["x-2"]["stuck"] == 1, True))
    # trace-many flies the balls `trace` flies
    good = RICH / "levels" / "base-p2.json"
    engine.ensure_built()
    angles = [-35.5, 4.0, 61.25]
    many = engine.trace_many(good, 5, angles, 90)
    same = True
    for a, tr in zip(angles, many):
        _code, text = engine._run("trace", good, a, 5, 1, 90)
        one = json.loads(text)
        same &= tr["points"] == one["points"] and tr["hits"] == one["hits"] and tr["angle"] == a
    cases.append(("trace-many: three aims in one process fly the balls trace flies", same, True))
    ok = True
    for name, got, want in cases:
        ok &= got == want
        if verbose:
            print(f"  {'ok ' if got == want else 'BAD'} pipeline: {name}: {got}, expected {want}")
    return ok


CONVERT_LEVELS = ("base-01", "base-06", "base-08", "base-09", "base-25")   # convert.selftest's default levels


def _sections():
    """The self-test's independent sections: name -> callable(verbose). None shares state with another, so they run in
    separate processes. The converter's levels are separate sections (convert.selftest(levels=..., masks=...) in pieces)."""
    from . import convert, incremental
    secs = {"preflight": author.selftest, "framecheck": frames.selftest, "readcheck": readability.selftest,
            "stage": stagecheck.selftest, "challenge": challenge.selftest, "precheck": precheck.selftest,
            "review": reviewcmds.selftest, "engine": engine_cases}
    for lid in CONVERT_LEVELS:
        secs[f"convert {lid}"] = lambda v, lid=lid: convert.selftest(v, levels=(lid,), masks=False)
    secs["convert masks"] = lambda v: convert.selftest(v, levels=(), masks=True)
    secs["speed-ups"] = incremental.selftest
    secs["pipeline"] = pipeline_cases
    return secs


def _section(name):
    """One section in its own process, its printed cases captured: (name, ok, text)."""
    import contextlib
    import io
    import traceback
    import time
    t0 = time.time()
    buf = io.StringIO()
    try:
        with contextlib.redirect_stdout(buf):
            ok = bool(_sections()[name](True))
    except Exception:                                       # a crashing checker is a failed one, with its trace
        buf.write(traceback.format_exc())
        ok = False
    return name, ok, buf.getvalue(), time.time() - t0


def run(verbose=True, parallel=True):
    """Every section, all of them, every time (no stamp): in a process pool, or in order with parallel=False. Prints the
    cases of a failed section (all of them when verbose). Returns True when every section passes."""
    engine.ensure_built()                                   # once, before any worker needs the built tools
    names = list(_sections())
    if parallel:
        from concurrent.futures import ProcessPoolExecutor
        heavy = sorted(names, key=lambda n: -(n.startswith("convert ") * 3 + (n in ("readcheck", "preflight")) * 2))  # longest first
        with ProcessPoolExecutor(max_workers=min(len(names), os.cpu_count() or 2, 12)) as ex:
            done = dict((r[0], r) for r in ex.map(_section, heavy))
        results = [done[n] for n in names]
    else:
        results = [_section(n) for n in names]
    merged, took = {}, {}
    for name, ok, text, secs in results:
        if verbose or not ok:
            print(text, end="")
        key = name.split()[0]                               # the converter's pieces report as one section
        merged[key] = merged.get(key, True) and ok
        took[key] = max(took.get(key, 0.0), secs)
    if verbose or not all(merged.values()):
        print("self-test:", ", ".join(f"{k} {'ok' if v else 'FAILED'} ({took[k]:.0f} s)" for k, v in merged.items()))
    return all(merged.values())


def fixture_files():
    """The files the self-test reads besides the code: the approved pilots, the shipped levels it replays and the scene
    recipes, painters and game textures those name. Not every level: a new level must not rerun the self-test."""
    from .paths import CACHE, JSON_OUT, PAINTERS, RICH, RICH2, SCENES, cache_name
    files = sorted((RICH / "levels").glob("*.json"))
    files += [d / "scenes" / f"{stem}.png" for d in (RICH, RICH2) for stem in
              ("base-p1-airship-road", "base-p2-holy-see", "base-p3-moogle", "exp-p1-sharlayan", "exp-p2-lantern-ferry",
               "exp-p3-mare-lamentorum")]
    levels = [JSON_OUT / f"base-{i:02d}.json" for i in range(1, 11)] + [JSON_OUT / f"{lid}.json" for lid in CONVERT_LEVELS]
    for lv in sorted(set(levels)):
        files.append(lv)
        if not lv.exists():
            continue
        scene = json.loads(lv.read_text(encoding="utf-8")).get("scene")
        rec = SCENES / f"{scene}.json"
        files.append(rec)
        if rec.exists():
            src = json.loads(rec.read_text(encoding="utf-8")).get("source", {})
            if src.get("kind") == "painting":
                files.append(PAINTERS / f"{src['painter']}.py")
            elif src.get("texture"):
                files.append(CACHE / cache_name(src["texture"]))
    for sub in ("uld", "portraits", "font"):
        files += sorted((CACHE / sub).glob("*.png"))
    return files


def run_cached(verbose=False, force=False):
    """`run`, unless a previous run PASSED on exactly these inputs (cache.selftest_key): every mflkit/*.py and mfl.py, the
    design kit, the built mfcheck and Core, the C# tool sources, and the fixtures. A pass writes the stamp. A hit prints
    one line and costs about a second; `force` (--force-selftest) reruns."""
    from . import cache
    key = cache.selftest_key(fixture_files())
    if not force:
        hit = cache.read_stamp("selftest", key)
        if hit:
            print(f"self-test: unchanged since last PASS ({hit['time']}); skipped (--force-selftest reruns)")
            return True
    ok = run(verbose)
    if ok:
        cache.write_stamp("selftest", key)
    return ok
