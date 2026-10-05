"""Moonfall's level-production pipeline. Run from anywhere with `py -3 tools/moonfall-levels/mfl.py <command>`.

  selftest                      every checker against its known-bad and known-good cases
  fetch                         dump every game texture the scene recipes and the chrome read into the cache
  trace <scene|level-id>        the tracing sheet: graded scene, grid, features and (for a level) its pieces
  build <level-id>... | --all   the full pipeline for each level; refuses (exit 1) any level that fails a check
        [--keep2x <id,...>]     also write these levels' 2x composites to composites/
  stage <n> [--ramp 144]        the stage's table: ramp, jewels of neighbours, game paintings against ours
  stuck <level-id>              where balls come to rest on the first shots the stuck rule fires on
  sources                       write docs/design/v9/levels/sources.json from the recipes

See README.md next to this file.
"""
import json
import sys
import time

sys.path.insert(0, str(__import__("pathlib").Path(__file__).resolve().parent))

from mflkit import engine, paths, selftest as st  # noqa: E402
from mflkit.paths import LAYOUTS, REPORT, SCENES, SOURCES  # noqa: E402


def level_ids():
    return sorted(p.stem for p in LAYOUTS.glob("*.py"))


def cmd_selftest(_args):
    return 0 if st.run() else 1


def cmd_fetch(_args):
    engine.ensure_built()
    from mflkit.scene import load_recipe
    want = set()
    for p in SCENES.glob("*.json"):
        r = load_recipe(p.stem)
        if r["source"]["kind"] == "game":
            want.add(r["source"]["texture"])
    paths.CACHE.mkdir(parents=True, exist_ok=True)
    todo = [t for t in sorted(want) if not (paths.CACHE / paths.cache_name(t)).exists()]
    if todo:
        code, out = engine.texdump("dump", paths.CACHE, *todo)
        print(out)
    # the chrome's UI art, the cards and the fonts (spec-rich2.md, "Rebuild")
    uld = ["JobHudSCH0", "Journal_Detail", "Journal_Frame", "LovmPalette", "PVPRankEmblem3", "TabButtonA",
           "TripleTriadBattle", "TripleTriadCardSelect", "TripleTriadResultCrown", "LovmMiniMapFrame", "EMJIntroParts03",
           "WindowA_BgSelected_Corner", "ButtonA", "Parameter_Gauge"]
    lst = paths.CACHE / "uld.txt"
    lst.write_text("\n".join(f"ui/uld/{u}_hr1.tex" for u in uld) + "\n")
    if not all((paths.CACHE / "uld" / f"ui_uld_{u}_hr1.png").exists() for u in uld):
        print(engine.texdump("dumplist", paths.CACHE / "uld", lst)[1])
    cards = [87019, 87020, 87049, 87050, 87056, 87058, 87059, 87060, 87065, 87066, 87067]
    if not all((paths.CACHE / "portraits" / f"icon_{c:06d}.png").exists() for c in cards):
        print(engine.texdump("icon", paths.CACHE / "portraits", *cards)[1])
    font = paths.CACHE / "font"
    if not (font / "common_font_font7.png").exists():
        print(engine.texdump("raw", font, *[f"common/font/{f}.fdt" for f in ("AXIS_36", "Jupiter_46", "Jupiter_23",
                                                                           "MiedingerMid_36", "TrumpGothic_184", "TrumpGothic_68")])[1])
        print(engine.texdump("dump", font, *[f"common/font/font{i}.tex" for i in range(1, 8)])[1])
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


def cmd_build(args):
    keep2x = set()
    if "--keep2x" in args:
        i = args.index("--keep2x")
        keep2x = set(args[i + 1].split(","))
        args = args[:i] + args[i + 2:]
    ids = level_ids() if args == ["--all"] or not args else args
    engine.ensure_built()
    if not st.run(verbose=False):
        st.run(verbose=True)
        print("a checker failed its own self-test: nothing is built")
        return 2
    print("self-test: every checker passes its known-bad and known-good cases")
    from mflkit.build import build
    bad = []
    t0 = time.time()
    for lid in ids:
        rep = build(lid, keep2x=lid in keep2x)
        if rep["verdict"] != "PASS":
            bad.append(lid)
    print(f"built {len(ids) - len(bad)} of {len(ids)} in {time.time() - t0:.0f} s" + (f"; refused: {', '.join(bad)}" if bad else ""))
    return 1 if bad else 0


def cmd_stage(args):
    """The stage's table. --ramp N replays the greedy player over N seeded games per level (the rule uses 48, whose
    win count swings by about 3.5; 144 games narrow that to about 2) and writes it to each report as `ramp`."""
    ramp = 0
    if "--ramp" in args:
        i = args.index("--ramp")
        ramp = int(args[i + 1])
        args = args[:i] + args[i + 2:]
    n = int(args[0])
    reps = [json.loads(p.read_text(encoding="utf-8")) for p in sorted(REPORT.glob("*.json"))]
    reps = sorted([r for r in reps if r["stage"] == n], key=lambda r: r["number"])
    if ramp:
        from concurrent.futures import ThreadPoolExecutor
        from mflkit.paths import JSON_OUT

        def one(r):
            return engine.play(JSON_OUT / f"{r['id']}.json", r["number"], games=ramp)["greedy_won"]
        with ThreadPoolExecutor(2) as ex:
            for r, won in zip(reps, ex.map(one, reps)):
                r["ramp"] = {"games": ramp, "greedy_won": won, "per_48": round(won * 48 / ramp, 1)}
                (REPORT / f"{r['id']}.json").write_text(json.dumps(r, indent=1) + "\n", encoding="utf-8")
    fails = []
    print(f"stage {n}: {len(reps)} levels")
    print(f"{'level':8} {'name':28} {'pieces':>6} {'greedy':>7} {'ramp/48':>8} {'random':>7} {'jewels':>10} {'source'}")
    prev = None
    for r in reps:
        c = r["checks"]
        jw = c.get("readcheck", {}).get("report", {}).get("jewels", {})
        pair = (jw.get("first_jewel_hue"), jw.get("second_jewel_hue"))
        print(f"{r['id']:8} {r['name'][:28]:28} {c['preflight']['pieces']:>6} {c.get('play', {}).get('greedy_won', '-'):>7} "
              f"{r.get('ramp', {}).get('per_48', '-'):>8} {c.get('play', {}).get('random_won', '-'):>7} "
              f"{str(pair[0]) + '/' + str(pair[1]):>10} {r['source']}")
        if prev is not None and pair == prev:
            fails.append(f"F7: {r['id']} has the same two jewels as the level before it")
        prev = pair
    game = sum(1 for r in reps if not r["source"].startswith("our painting"))
    print(f"game paintings {game} of {len(reps)}; verdicts: " + ", ".join(f"{r['id']} {r['verdict']}" for r in reps))
    for f in fails:
        print("  !", f)
    return 1 if fails else 0


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
    lid = args[0]
    path = paths.BUILD / "json" / f"{lid}.json"
    meta = __import__("mflkit.build", fromlist=["load_layout"]).load_layout(lid).LEVEL
    n = str(meta["number"])
    out = subprocess.run(["dotnet", str(paths.MFCHECK), "sweep", str(path), n], capture_output=True, text=True).stdout
    angles = [float(m.group(1)) for m in re.finditer(r"^\s*(-?[\d.]+)\s+pegs.*STUCK", out, re.M)]
    cells = Counter()
    for a in angles:
        tr = subprocess.run(["dotnet", str(paths.MFCHECK), "trace", str(path), str(a), n, "1", "3000"],
                            capture_output=True, text=True).stdout
        pts = json.loads(tr)["points"]
        c = Counter((round(x / 5) * 5, round(y / 5) * 5) for x, y in pts)
        cells[c.most_common(1)[0][0]] += 1
    print(f"{len(angles)} stuck first shots; where they rest (x, y): count")
    for cell, k in cells.most_common(12):
        print(f"  {cell}: {k}")
    return 0


COMMANDS = {"selftest": cmd_selftest, "stuck": cmd_stuck, "fetch": cmd_fetch, "trace": cmd_trace, "build": cmd_build, "stage": cmd_stage,
            "sources": cmd_sources}

if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        print(__doc__)
        sys.exit(2)
    sys.exit(COMMANDS[sys.argv[1]](sys.argv[2:]))
