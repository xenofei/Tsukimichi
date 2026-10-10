"""Moonfall's level-production pipeline. Run from anywhere with `py -3 tools/moonfall-levels/mfl.py <command>`.

  selftest [--force-selftest]   every checker against its known-bad and known-good cases (skipped when unchanged)
  fetch                         dump every game texture the scene recipes and the chrome read into the cache
  trace <scene|level-id>        the tracing sheet: graded scene, grid, features and (for a level) its pieces
  check <level-id>...           the fast inner loop (about 3 s): pre-flight, loader, sweep, reach and the 48 rule games;
        [--json]                prints PASS or the exact problems (tag, x, y, hint); writes nothing
  build <level-id>... | --all   the full pipeline for each level; refuses (exit 1) any level that fails a check;
        [--force]               a level unchanged since its last PASS is skipped unless --force
        [--keep2x <id,...>]     also write these levels' 2x composites to composites/
        [--jobs N]              N levels at once, one process each (default 1: one level, its renders in a pool)
        [--keep-going]          do not stop after the cheap phase (the check's) when it finds a fault
        [--force-selftest]      rerun the self-test (otherwise skipped when nothing it reads changed since its last PASS)
        [--json]                the verdict, the faults ({rule, piece, x, y, hint}) and the figures as JSON on stdout
  level <level-id> [--force]    build, convert, convert --check and the engine render of one level; one verdict line
  stage <key> [--force] [--json] key: mr1-mr11 (The Moon Road; a bare 1-11 means the same) or fs1-fs12 (The Far Shore)
                                (games already played on an unchanged level file are re-used unless --force)
                                the stage's table: the held-out ramp, jewels of neighbours, game paintings against ours
  precheck <level-id>... | --all  the findings the supervisors raised round after round, measured before review: subject
        [--rev <git rev>] [--only a,b] legibility at 640, a bright disc behind the aim guide, near-repeats, empty and crowded
        [--strict]                    patches, stuck and holdout hot spots, thin margins (report only; --strict exits 1 on a FAIL)
  challenge <ch-id> | <level-id> <oranges>   the greedy player at a challenge's orange count on its carrier level(s):
                                at least 5 of 48 wins (ch-03 35 oranges on base-27; ch-04 45 on base-37, the one exception)
  stuck <level-id>              where balls come to rest on the first shots the stuck rule fires on
  dead <level-id>               where the first shots that touch nothing fly (to put a piece in their lane)
  ease <level-id> [tags]        each place of the subject ranked by how often it is the orange left behind
  generate <level-id> [--seed N] [--variants K] [--scene NAME] [--out DIR] [--no-engine] [--no-jewels]
                                K candidate layouts from the scene's features, the technique and charter section 1
                                (build/generate/<id>/v<k>.py, previews and contact.png); each passes the pre-flight
  tune <level-id | layout.py> [--band LO,HI] [--finale-vs MEAN] [--max-plays N] [--out PATH]
                                layout-preserving moves until the held-out figure is in the band (default: the stage's
                                band); writes the layout's TUNING block (in place, or to --out)
        [--hold [--lock X]]     difficulty-neutral: back to the level's locked held-out figure after a fix
  tune --stage <key> [--out DIR] the stage's five levels together: mean band and finale rule, validated on fresh seeds
  sources                       write docs/design/v9/levels/sources.json from the recipes
  diff <level-id> --rev <rev>   one PNG: the composite at that revision beside now, changed pieces ringed, with a summary
  sheet <level-id>... | --stage <key>   a contact sheet of composites (--engine adds the plugin's renders) with each level's figures
  status --stage <key> | --all [--json]  per level: build stamp, verdict, wins against the band, convert, precheck, changes since review
  pack --stage <key> [--changed-since <rev>]   the review pack in docs/design/v9/levels/review/<key>/ (no build; --no-engine skips renders)
  convert <level-id>... | --all the runtime recipe and pictures for each level (Tsukimichi.Core scenes, plugin assets)
        [--check] [--jobs N]    only the converter's gate: the game's bare scene against the pipeline's (MoonfallRender);
        [--force]               a level unchanged since its last PASS is not re-checked; levels checked N at a time (4)

See README.md next to this file.
"""
import json
import os
import sys
import time

sys.path.insert(0, str(__import__("pathlib").Path(__file__).resolve().parent))

from mflkit import engine, paths, selftest as st  # noqa: E402
from mflkit.paths import LAYOUTS, REPORT, SCENES, SOURCES  # noqa: E402


def level_ids():
    return sorted(p.stem for p in LAYOUTS.glob("*.py"))


def cmd_selftest(args):
    """selftest [--force-selftest]: skipped (one line) when a previous run passed on the same code, tools and fixtures."""
    engine.ensure_ready()
    return 0 if st.run_cached(verbose=True, force="--force-selftest" in args or "--force" in args) else 1


def cmd_fetch(_args):
    from mflkit import shared
    shared.fetch_all()
    shared.sync(paths.CACHE, shared.MIRROR)
    print("cache ready:", paths.CACHE)
    return 0


def cmd_trace(args):
    from mflkit import tracesheet
    from mflkit.scene import load_recipe
    name = args[0]
    board = None
    if (LAYOUTS / f"{name}.py").exists():
        from mflkit.build import make_board
        meta, recipe, board = make_board(name)
        for s in board.skipped:
            print("  skipped", s)
        probs = board.check(verbose=True, number=meta["number"])
        out = tracesheet.sheet(recipe, board, out=paths.BUILD / "trace" / f"{name}.png")
    else:
        recipe = load_recipe(name)
        out = tracesheet.sheet(recipe)
    print("wrote", out)
    return 0


def unchanged(lid, want2x, load_layout, load_recipe, cache):
    """The stamp's time when this level's last build passed on exactly these inputs and its outputs are still there."""
    meta = load_layout(lid).LEVEL
    try:
        key = cache.build_key(lid, meta, load_recipe(meta["scene"]))
    except (OSError, SystemExit):
        return None
    st_ = cache.read_stamp(f"build-{lid}", key)
    if not st_:
        return None
    outs = [paths.JSON_OUT / f"{lid}.json", paths.COMPOSITES / f"{lid}.png", REPORT / f"{lid}.json"]
    if want2x:
        outs.append(paths.COMPOSITES / f"{lid}@2x.png")
    if not all(o.exists() for o in outs):
        return None
    try:
        if json.loads(outs[2].read_text(encoding="utf-8")).get("verdict") != "PASS":
            return None
    except ValueError:
        return None
    return st_["time"]


def _problem_rows(level_id, rep):
    """A report's faults as the --json rows: {level, rule, piece, x, y, hint, message}."""
    rows = [dict(r, level=level_id) for r in rep.get("problems", [])]
    if not rows:                                            # a report from before "problems" existed: the text only
        rows = [{"level": level_id, "rule": f.split(":", 1)[0], "piece": "", "x": None, "y": None, "hint": "", "message": f}
                for f in rep.get("fails", [])]
    return rows


def _figures(rep):
    c = rep.get("checks", {})
    pf, sw, pl, rc = c.get("preflight", {}), c.get("sweep", {}), c.get("play", {}), c.get("reach", {})
    fig = {"pieces": pf.get("pieces"), "candidates": pf.get("orange_candidates"), "lower_third_share": pf.get("lower_third_share"),
           "stuck": sw.get("stuck"), "angles": sw.get("angles"), "dead": sw.get("no_hit"), "never_reached": len(rc.get("never", [])),
           "greedy_won_of_48": pl.get("greedy_won"), "ramp_per_48": pl.get("ramp_per_48"), "seconds": rep.get("seconds")}
    if "readcheck" in c:
        r = c["readcheck"]["report"]
        fig.update(worst_margin=r.get("worst_margin"), jewels=[r["jewels"].get("first_jewel_hue"), r["jewels"].get("second_jewel_hue")])
    if "ceiling" in c:
        fig["ceiling_p99_luma"] = c["ceiling"].get("p99_luma")
    return fig


def _emit_json(command, reps, extra=None, stream=None):
    """The --json document: {command, verdict, fails: [...], figures: {level: {...}}}; the verdict is PASS only if every level passed."""
    fails = [row for lid, r in reps.items() for row in _problem_rows(lid, r)]
    out = {"command": command, "verdict": "PASS" if reps and all(r["verdict"] == "PASS" for r in reps.values()) and not (extra or {}).get("fails") else "REFUSED",
           "fails": fails + list((extra or {}).get("fails", [])), "figures": {lid: _figures(r) for lid, r in reps.items()}}
    out.update({k: v for k, v in (extra or {}).items() if k != "fails"})
    (stream or sys.__stdout__).write(json.dumps(out, indent=1) + "\n")


class _Quiet:
    """With --json the human lines go to stderr, so stdout carries the JSON alone."""

    def __init__(self, on):
        self.on = on

    def __enter__(self):
        if self.on:
            self.old, sys.stdout = sys.stdout, sys.stderr

    def __exit__(self, *exc):
        if self.on:
            sys.stdout = self.old


def cmd_check(args):
    """check <level-id>... [--json]: the fast inner loop. The pre-flight, the shipped loader, the first-shot sweep, the pocket
    rule and the rule's 48 greedy games on the level's layout file (about 3 s): PASS, or every problem with the piece's
    layout tag, x, y and a hint. Writes nothing (no report, no json, no composites). Exit 1 when a level has a problem.
    A PASS here is necessary, not sufficient: `build` also grades the scene, dresses it and checks the framing and
    readability."""
    js = "--json" in args
    ids = [a for a in args if not a.startswith("--")]
    if not ids:
        print("check <level-id>... [--json]")
        return 2
    engine.ensure_built()
    from mflkit.build import build
    reps = {}
    with _Quiet(js):
        for lid in ids:
            rep = build(lid, cheap_only=True, log=lambda *_: None)
            reps[lid] = rep
            if rep["verdict"] == "PASS":
                print(f"[{lid}] PASS ({rep['seconds']} s): pre-flight, loader, sweep, reach, 48 rule games")
            else:
                print(f"[{lid}] FAIL ({rep['seconds']} s):")
                for r in _problem_rows(lid, rep):
                    print(f"  ! {r['rule']}: {r['message']}" + (f"\n      fix: {r['hint']}" if r["hint"] else ""))
    if js:
        _emit_json("check", reps)
    return 0 if all(r["verdict"] == "PASS" for r in reps.values()) else 1


def cmd_build(args):
    js = "--json" in args
    with _Quiet(js):
        code, reps = _build(args)
    if js:
        _emit_json("build", reps)
    return code


def _build(args):
    keep2x = set()
    if "--keep2x" in args:
        i = args.index("--keep2x")
        keep2x = set(args[i + 1].split(","))
        args = args[:i] + args[i + 2:]
    jobs = 1
    if "--jobs" in args:
        i = args.index("--jobs")
        jobs, args = int(args[i + 1]), args[:i] + args[i + 2:]
    force = "--force" in args
    keep_going, force_selftest = "--keep-going" in args, "--force-selftest" in args
    args = [a for a in args if a not in ("--force", "--keep-going", "--force-selftest", "--json")]
    if force:
        os.environ["MFL_NO_CACHE"] = "1"
    ids = level_ids() if args == ["--all"] or not args else args
    engine.ensure_ready()
    from mflkit import cache
    from mflkit.build import load_layout
    from mflkit.scene import load_recipe
    todo = []
    for lid in ids:
        when = None if force else unchanged(lid, lid in keep2x, load_layout, load_recipe, cache)
        if when:
            print(f"[{lid}] unchanged since last PASS ({when}); skipped (--force rebuilds)")
        else:
            todo.append(lid)
    if not todo:
        print(f"built 0 of {len(ids)}: all unchanged since their last PASS")
        return 0, {lid: json.loads((REPORT / f"{lid}.json").read_text(encoding="utf-8")) for lid in ids if (REPORT / f"{lid}.json").exists()}
    ids_all, ids = ids, todo
    if not st.run_cached(verbose=False, force=force_selftest):
        print("a checker failed its own self-test: nothing is built")
        return 2, {}
    from mflkit import incremental
    t0 = time.time()
    keys = {}
    for lid in ids:
        meta = load_layout(lid).LEVEL
        keys[lid] = cache.build_key(lid, meta, load_recipe(meta["scene"]))
    bad, reps = incremental.build_many(ids, keep2x, jobs, keys, keep_going=keep_going)
    print(f"built {len(ids) - len(bad)} of {len(ids)} in {time.time() - t0:.0f} s" + (f"; refused: {', '.join(bad)}" if bad else "")
          + (f"; {len(ids_all) - len(ids)} unchanged, skipped" if len(ids_all) > len(ids) else ""))
    return (1 if bad else 0), reps


def cmd_stage(args):
    js = "--json" in args
    args = [a for a in args if a != "--json"]
    collect = {}
    with _Quiet(js):
        code = _stage(args, collect)
    if js:
        rows = [{"level": None, "rule": "stage", "piece": "", "x": None, "y": None, "hint": "", "message": f} for f in collect.get("fails", [])]
        sys.__stdout__.write(json.dumps({"command": "stage", "verdict": "REFUSED" if rows or code else "PASS", "fails": rows,
                                         "figures": collect.get("figures", {})}, indent=1) + "\n")
    return code


def _stage(args, collect):
    """The stage's table from the reports: pieces, the rule's 48 games, the held-out ramp (1728 games, about +-0.55 per 48), the
    random player, the jewels; then every level pooled over 6912 games; it faults neighbours with the same jewels, a
    finale that is not the stage's hardest by the gap (resolved by one standard error), and a stage whose mean is
    outside its band (Peggle's measured stage means, raised to the 5-of-48 floor: see stagecheck.py)."""
    if "--force" in args:
        os.environ["MFL_NO_CACHE"] = "1"
        args = [a for a in args if a != "--force"]
    from mflkit import stagecheck
    try:
        key = stagecheck.stage_key(args[0])
    except (IndexError, ValueError) as e:
        print(e if args else "stage <mr1-mr11 | fs1-fs12 | 1-11> [--force]")
        return 2
    camp, n = key[:2], int(key[2:])
    allr = [json.loads(p.read_text(encoding="utf-8")) for p in sorted(REPORT.glob("*.json"))]
    # a stage is a campaign and a number: the Far Shore's stages number from 1 again, so the id's prefix says which
    reps = sorted([r for r in allr if stagecheck.level_campaign(r["id"]) == camp and r["stage"] == n], key=lambda r: r["number"])
    fails = []
    print(f"stage {key}: {len(reps)} levels")
    print(f"{'level':8} {'name':28} {'pieces':>6} {'greedy':>7} {'ramp/48':>8} {'random':>7} {'jewels':>10} {'source'}")
    rows = []
    for r in reps:
        c = r["checks"]
        jw = c.get("readcheck", {}).get("report", {}).get("jewels", {})
        pair = (jw.get("first_jewel_hue"), jw.get("second_jewel_hue"))
        ramp48 = c.get("play", {}).get("ramp_per_48", "-")
        print(f"{r['id']:8} {r['name'][:28]:28} {c['preflight']['pieces']:>6} {c.get('play', {}).get('greedy_won', '-'):>7} "
              f"{ramp48:>8} {c.get('play', {}).get('random_won', '-'):>7} "
              f"{str(pair[0]) + '/' + str(pair[1]):>10} {r['source']}")
        rows.append((r["id"], c.get("play", {}).get("ramp_per_48"), pair))
    # every level is played on CONFIRM_GAMES more fresh seeds (after the held-out block), and every step and the finale
    # gap are judged on the pooled games by one standard error (game designer rounds 4-6, G19, G20 and G22; critic
    # N22). The shipped file is played, not the build copy (critic N16).
    from concurrent.futures import ThreadPoolExecutor
    pooled, games = {}, {}
    total = engine.HELD_GAMES + stagecheck.CONFIRM_GAMES

    def confirm(row):
        lid, held, _pair = row
        num = next(r["number"] for r in reps if r["id"] == lid)
        pl = engine.play(paths.JSON_OUT / f"{lid}.json", num, stagecheck.CONFIRM_GAMES,
                         first=engine.RAMP_GAMES + engine.HELD_GAMES)
        return lid, held, pl["ramp_per_48"]
    with ThreadPoolExecutor(5) as ex:
        for lid, held, fresh in ex.map(confirm, [r for r in rows if r[1] is not None]):
            pooled[lid] = round((held * engine.HELD_GAMES + fresh * stagecheck.CONFIRM_GAMES) / total, 2)
            games[lid] = total
            print(f"  {lid}: held-out {held}, {stagecheck.CONFIRM_GAMES} fresh {fresh}, pooled {pooled[lid]} per 48 "
                  f"(+-{stagecheck.se48(pooled[lid], total):.2f}) over {total} games")
    rows = [(lid, pooled.get(lid, ramp), pair) for (lid, ramp, pair) in rows]
    ramps_ = [r for (_l, r, _p) in rows if r is not None]
    if len(ramps_) == len(rows) and len(rows) >= 2:
        lo, hi = stagecheck.MEAN_BANDS[key]
        mo = sum(ramps_[:-1]) / (len(ramps_) - 1)
        print(f"  pooled mean {sum(ramps_) / len(ramps_):.2f} (band {lo:.2f} to {hi:.2f}); finale "
              f"{mo - ramps_[-1]:.2f} below the other levels' mean {mo:.2f}")
    # the level the player meets just before this stage's first: the previous stage's last, in the same campaign (the Far
    # Shore's stage 1 has nothing before it)
    prev = stagecheck.previous_stage(key)
    last = [r for r in allr if prev and stagecheck.level_campaign(r["id"]) == camp and r["stage"] == int(prev[2:])]
    before = None
    if last:
        r = max(last, key=lambda r: r["number"])
        jw = r["checks"].get("readcheck", {}).get("report", {}).get("jewels", {})
        before = (r["id"], r["checks"].get("play", {}).get("ramp_per_48"), (jw.get("first_jewel_hue"), jw.get("second_jewel_hue")))
    fails += stagecheck.faults(key, rows, before, games)
    collect["fails"] = list(fails)
    collect["figures"] = {lid: {"ramp_per_48": ramp, "jewels": list(pair), "pooled_per_48": pooled.get(lid), "games": games.get(lid)}
                          for (lid, ramp, pair) in rows}
    game = sum(1 for r in reps if not r["source"].startswith("our painting"))
    print(f"game paintings {game} of {len(reps)}; verdicts: " + ", ".join(f"{r['id']} {r['verdict']}" for r in reps))
    for f in fails:
        print("  !", f)
    print("precheck (report only, not a gate; `mfl.py precheck <ids>` runs it alone):")
    try:
        from mflkit import precheck
        precheck.precheck([r["id"] for r in reps], print_=lambda t: print("  " + t.replace("\n", "\n  ")))
    except Exception as e:                           # a report must never break the stage table
        print(f"  precheck failed to run: {type(e).__name__}: {e}")
    return 1 if fails else 0


def cmd_precheck(args):
    """precheck <level-id>... | --all [--rev <git rev>] [--only legibility,axis,repeats,patches,play,margins] [--strict]:
    mflkit/precheck.py. Reads the committed level file, scene recipe, composite and build report (no build, no game install),
    from the working tree or from a past commit with --rev. Report only: the exit code is 0 unless --strict and a check FAILs."""
    from mflkit import precheck
    rev, only, strict = None, None, "--strict" in args
    args = [a for a in args if a != "--strict"]
    if "--rev" in args:
        i = args.index("--rev")
        rev, args = args[i + 1], args[:i] + args[i + 2:]
    if "--only" in args:
        i = args.index("--only")
        only, args = args[i + 1].split(","), args[:i] + args[i + 2:]
        bad = [o for o in only if o not in precheck.CHECKS]
        if bad:
            print(f"unknown check {bad}: use {', '.join(precheck.CHECKS)}")
            return 2
    ids = precheck.Source(rev).ids() if args == ["--all"] else args
    if not ids:
        print("precheck <level-id>... | --all [--rev <git rev>] [--only a,b] [--strict]")
        return 2
    _res, worst = precheck.precheck(ids, rev=rev, only=only)
    return 1 if strict and worst == precheck.FAIL else 0


def cmd_challenge(args):
    """The challenge carrier check (mflkit/challenge.py): `challenge ch-03` plays every carrier of a recorded challenge at
    its orange count; `challenge <level-id> <oranges>` plays one level at any count. At least 5 of the rule's 48 games
    must be won; more than 35 oranges only on a recorded exception. A carrier not built yet is reported, not failed,
    when a recorded challenge names it."""
    from mflkit import challenge
    if "--force" in args:
        os.environ["MFL_NO_CACHE"] = "1"
        args = [a for a in args if a != "--force"]
    if args and args[0] in challenge.CARRIERS and len(args) == 1:
        c = challenge.CARRIERS[args[0]]
        jobs, recorded = [(lid, c["oranges"]) for lid in c["levels"]], True
        print(f"{args[0]}: {c['note']}")
    elif len(args) == 2 and args[1].isdigit():
        jobs, recorded = [(args[0], int(args[1]))], False
    else:
        print("challenge <ch-id> | <level-id> <oranges> [--force]; recorded: " + ", ".join(challenge.CARRIERS))
        return 2
    bad = 0
    for lid, oranges in jobs:
        ex = challenge.EXCEPTIONS.get(lid)
        res = challenge.play(lid, oranges)
        if res is None:
            print(f"  {lid}: {oranges} oranges: not built yet (no {paths.JSON_OUT / (lid + '.json')})" + ("" if recorded else " "))
            bad += 0 if recorded else 1
            continue
        cands, wins, probs = res
        print(f"  {lid}: {oranges} oranges over {cands} candidates: the greedy player wins {wins} of {challenge.GAMES}"
              + (f" [exception: {ex['reason']}]" if ex and oranges > challenge.MAX_ORANGES else ""))
        for q in probs:
            print("  !", q)
        bad += 1 if probs else 0
    return 1 if bad else 0


def cmd_sources(_args):
    from mflkit.scene import load_recipe
    out = {}
    for lid in level_ids():
        from mflkit.build import load_layout
        meta = load_layout(lid).LEVEL
        r = load_recipe(meta["scene"])
        if r["source"]["kind"] == "game":
            out.setdefault(r["source"]["texture"], []).append(
                f"{lid} \"{meta['name']}\": scene {meta['scene']}, crop {r['source']['crop']}"
                f"{', mirrored' if r['source'].get('mirror') else ''} (read from the install at load, graded, never shipped)")
        else:
            out.setdefault("(our own painting) " + r["source"]["painter"], []).append(f"{lid} \"{meta['name']}\": scene {meta['scene']}")
    SOURCES.write_text(json.dumps(dict(sorted(out.items())), indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print("wrote", SOURCES)
    return 0


def cmd_stuck(args):
    """Where balls come to rest: for every first shot on which the stuck rule fires, the 5-unit cell the ball spends
    the longest in, tallied (a flat deck, a notch, a cradle)."""
    import re
    import subprocess
    from collections import Counter
    engine.ensure_built()
    lid = args[0]
    path = paths.JSON_OUT / f"{lid}.json"            # the shipped file (critic N24)
    meta = __import__("mflkit.build", fromlist=["load_layout"]).load_layout(lid).LEVEL
    n = str(meta["number"])
    out = subprocess.run(["dotnet", str(paths.MFCHECK), "sweep", str(path), n, "1", str(engine.SWEEP_STEP)], capture_output=True, text=True).stdout
    angles = [float(m.group(1)) for m in re.finditer(r"^\s*(-?[\d.]+)\s+pegs.*STUCK", out, re.M)]
    cells = Counter()
    for a, tr in zip(angles, engine.trace_many(path, n, angles, 3000)):
        pts = tr["points"]
        c = Counter((round(x / 5) * 5, round(y / 5) * 5) for x, y in pts)
        cells[c.most_common(1)[0][0]] += 1
    print(f"{len(angles)} stuck first shots; where they rest (x, y): count")
    for cell, k in cells.most_common(12):
        print(f"  {cell}: {k}")
    return 0


def cmd_dead(args):
    """The first shots that touch nothing: for each, where its flight passes (every 40 units between y 150 and 520),
    so a piece that means something can be put in its lane."""
    import re
    import subprocess
    engine.ensure_built()
    lid = args[0]
    path = paths.JSON_OUT / f"{lid}.json"            # the shipped file (critic N24)
    n = str(__import__("mflkit.build", fromlist=["load_layout"]).load_layout(lid).LEVEL["number"])
    out = subprocess.run(["dotnet", str(paths.MFCHECK), "sweep", str(path), n, "1", str(engine.SWEEP_STEP)],
                         capture_output=True, text=True).stdout
    angles = [float(m.group(1)) for m in re.finditer(r"^\s*(-?[\d.]+)\s+pegs\s+0 ", out, re.M)]
    for a, tr in zip(angles, engine.trace_many(path, n, angles, 600)):
        pts = tr["points"]
        keep, last = [], None
        for (x, y) in pts:
            if 150 <= y <= 520 and (last is None or (x - last[0]) ** 2 + (y - last[1]) ** 2 >= 1600):
                keep.append((round(x), round(y)))
                last = (x, y)
        print(f"{a:6.1f}: {keep}")
    return 0


def cmd_ease(args):
    """ease <level-id> [tag,tag,...] [--pick N] [--skip tag,tag]: how hard each place is to clear. A scratch copy of the
    level makes every peg with one of the tags (default: every peg) an orange candidate, the greedy player plays 864
    games, and each place is ranked by the share of lost games it is left orange in. With --pick, it also proposes the
    N places most readily cleared that keep the spread rule (a third of the candidates per 200 x 200, a candidate in
    y 400-430 on each half) and stay out of the bucket's approach (home above y 430), skipping the tags given: a
    starting set for the places that carry the subject. (Since the owner's decision of 9 October 2026 a layout marks
    most pieces as candidates with `b.candidates(exclude=...)`; this is for finding which places to exclude.)"""
    from mflkit.build import make_board
    from mflkit import author
    pick, skip = None, set()
    if "--pick" in args:
        i = args.index("--pick")
        pick, args = int(args[i + 1]), args[:i] + args[i + 2:]
    if "--skip" in args:
        i = args.index("--skip")
        skip, args = set(args[i + 1].split(",")), args[:i] + args[i + 2:]
    lid = args[0]
    tags = set(args[1].split(",")) if len(args) > 1 else None
    meta, recipe, b = make_board(lid)
    level = b.level_json(meta["id"], meta["name"], meta["scene"])
    peg_tags = [t for (k, t) in b.tags if k == "peg"]
    for p, t in zip(level["pegs"], peg_tags):
        if tags is None or t in tags:
            p["canBeOrange"] = True
    path = paths.BUILD / "json" / f"{lid}-ease.json"
    __import__("mflkit.author", fromlist=["write_json"]).write_json(level, path)
    pl = engine.play(path, meta["number"])
    share = {tuple(h["at"]): h["share_of_lost_games"] for h in pl["holdouts"]}
    rows = sorted(((share.get((round(p["x"]), round(p["y"])), 0.0), round(p["x"]), round(p["y"]), t)
                   for p, t in zip(level["pegs"], peg_tags) if p["canBeOrange"]), reverse=True)
    print(f"{lid}: {sum(1 for p in level['pegs'] if p['canBeOrange'])} candidates in the copy; ramp {pl['ramp_per_48']}/48")
    for s_, x, y, t in rows:
        print(f"  {s_:5.2f}  ({x}, {y})  {t}")
    if pick:
        pool = sorted(r for r in rows if r[2] < 430 and r[3] not in skip)

        def spread_ok(sel):
            limit = author.spread_limit(pick)
            return all(sum(1 for (_s, x, y, _t) in sel if sx <= x < sx + 200 and sy <= y < sy + 200) <= limit
                       for sx in range(75, 530, 5) for sy in range(40, 400, 5))
        sel = []
        for half in (lambda x: x < 400, lambda x: x >= 400):
            sel += [r for r in pool if 400 <= r[2] and half(r[1])][:1]
        for r in pool:
            if len(sel) >= pick:
                break
            if r not in sel and spread_ok(sel + [r]):
                sel.append(r)
        print(f"proposed candidates ({len(sel)}):", sorted((x, y) for (_s, x, y, _t) in sel))
    return 0


def cmd_level(args):
    """level <level-id> [--force]: build -> convert -> convert --check -> the engine render, and one verdict line."""
    from mflkit import incremental
    ids = [a for a in args if not a.startswith("--")]
    if len(ids) != 1:
        print("level <level-id> [--force]")
        return 2
    return incremental.level(ids[0], cmd_build, force="--force" in args)


def cmd_convert(args):
    import subprocess
    from mflkit import convert
    jobs = 4
    if "--jobs" in args:
        i = args.index("--jobs")
        jobs, args = int(args[i + 1]), args[:i] + args[i + 2:]
    ids = level_ids() if "--all" in args else [a for a in args if not a.startswith("--")]
    if not ids:
        print("convert <level-id>... | --all [--check]")
        return 2
    if "--check" not in args:
        total = 0
        for lid in ids:
            name, sizes = convert.write(lid)
            pngs = sum(v for k, v in sizes.items() if k.endswith(".png"))
            total += pngs
            print(f"{lid}: {name}, pictures {pngs / 1024:.0f} KB ({', '.join(k for k in sizes if k.endswith('.png')) or 'none'})")
        print(f"pictures written: {total / 1024:.0f} KB")
        return 0
    from mflkit import incremental
    scratch = paths.BUILD / "convert"
    scratch.mkdir(parents=True, exist_ok=True)
    ok, rows = incremental.gate(ids, scratch, jobs=jobs, force="--force" in args)
    print(f"converter gate: {'PASS' if ok else 'FAIL'} ({sum(1 for r in rows if r[4])} of {len(rows)}: 2x max <= "
          f"{convert.GATE_MAX} and p99.9 <= {convert.GATE}, 1x p99 <= {convert.GATE_1X}, nothing dropped); diffs in {scratch}")
    return 0 if ok else 1


def _opt(args, name, default=None, cast=str):
    """Pops `--name value` from args: (value, the args left)."""
    if name in args:
        i = args.index(name)
        return cast(args[i + 1]), args[:i] + args[i + 2:]
    return default, args


def cmd_generate(args):
    """generate <level-id> [--seed N] [--variants K] [--scene NAME] [--out DIR] [--no-engine] [--no-jewels]: K candidate layouts
    (mflkit/generate.py) in build/generate/<id>/, each passing the pre-flight, with a preview and a contact sheet."""
    import pathlib
    from mflkit import generate
    seed, args = _opt(args, "--seed", 1, int)
    k, args = _opt(args, "--variants", 4, int)
    scene_name, args = _opt(args, "--scene")
    out, args = _opt(args, "--out", None, pathlib.Path)
    use_engine = "--no-engine" not in args
    tries = 0 if "--no-jewels" in args else 6
    args = [a for a in args if a not in ("--no-engine", "--no-jewels")]
    if len(args) != 1:
        print(cmd_generate.__doc__)
        return 2
    t0 = time.time()
    if use_engine:
        engine.ensure_built()
    res = generate.generate(args[0], seed=seed, variants=k, scene_name=scene_name, out_dir=out, use_engine=use_engine,
                            jewel_tries=tries)
    where = res[0][0].parent if res else out
    print(f"{len(res)} variants in {time.time() - t0:.0f} s, in {where}" + ("; contact sheet contact.png" if use_engine else ""))
    print(f"pick one: copy {where / 'v<k>.py'} to {LAYOUTS / (args[0] + '.py')}, polish the subject, then `mfl.py tune {args[0]}`")
    return 1 if any(p for (_path, p, _r) in res) else 0


def cmd_tune(args):
    """tune <level-id | layout.py> [--band LO,HI] [--finale-vs MEAN] [--max-plays N] [--out PATH]: layout-preserving
    moves (mflkit/tune.py) until the held-out figure is in the band; writes the layout's TUNING block.
    tune <level-id> --hold [--lock X]: back to the level's locked held-out figure (its report's) after a fix.
    tune --stage <key> [--out DIR]: the stage's five levels together, against its mean band and the finale rule."""
    import pathlib
    from mflkit import tune
    band, args = _opt(args, "--band", None, lambda s: tuple(float(v) for v in s.split(",")))
    fin, args = _opt(args, "--finale-vs", None, float)
    mp, args = _opt(args, "--max-plays", 40, int)
    out, args = _opt(args, "--out", None, pathlib.Path)
    stage, args = _opt(args, "--stage")
    lock, args = _opt(args, "--lock", None, float)
    hold = "--hold" in args
    args = [a for a in args if a != "--hold"]
    engine.ensure_built()
    if stage:
        return 0 if tune.tune_stage(stage, max_plays=mp, out_dir=out)["ok"] else 1
    if len(args) != 1:
        print(cmd_tune.__doc__)
        return 2
    res = tune.tune_layout(args[0], band=band, finale_vs=fin, max_plays=mp, out=out, hold=hold, lock=lock)
    return 0 if res["ok"] else 1


COMMANDS = {"check": cmd_check, "level": cmd_level, "generate": cmd_generate, "tune": cmd_tune, "precheck": cmd_precheck, "convert": cmd_convert, "selftest": cmd_selftest, "stuck": cmd_stuck, "dead": cmd_dead, "ease": cmd_ease, "fetch": cmd_fetch, "trace": cmd_trace, "build": cmd_build, "stage": cmd_stage,
            "challenge": cmd_challenge, "sources": cmd_sources}

from mflkit import reviewcmds  # noqa: E402  (diff, sheet, status, pack: mflkit/reviewcmds.py)
COMMANDS.update(reviewcmds.COMMANDS)

if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        print(__doc__)
        sys.exit(2)
    sys.exit(COMMANDS[sys.argv[1]](sys.argv[2:]))
