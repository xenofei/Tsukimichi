"""Every checker proves itself before it is trusted: known-bad shapes must fail and known-good ones must pass (the
supervisors' rule since rich pass 2, round 3). `mfl.py build` runs this first and stops if any case is wrong."""
import json
import math

from . import author, engine, frames, readability
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


def engine_cases(verbose=True):
    engine.ensure_built()
    BUILD.mkdir(parents=True, exist_ok=True)
    good = RICH / "levels" / "base-p2.json"
    bad = json.loads(good.read_text(encoding="utf-8"))
    bad["pegs"][0].update(x=400.0, y=120.0)                 # inside the launcher's swing
    bad_path = BUILD / "selftest-launcher.json"
    bad_path.write_text(json.dumps(bad), encoding="utf-8")
    sealed = BUILD / "selftest-sealed.json"
    sealed.write_text(json.dumps(_sealed_level()), encoding="utf-8")
    ok_good, _ = engine.validate(good)
    ok_bad, _ = engine.validate(bad_path)
    ok_sealed, msg = engine.validate(sealed)
    play_good = engine.play(good, 5)["greedy_won"]
    play_sealed = engine.play(sealed, 5)["greedy_won"] if ok_sealed else -1
    sw = engine.sweep(sealed, 5)
    cases = [("loader: an approved pilot", ok_good, True), ("loader: a peg in the launcher's swing", ok_bad, False),
             ("loader: the sealed board loads (its fault is play, not format)", ok_sealed, True),
             (f"play: an approved pilot wins {play_good} of 48", play_good >= engine.GREEDY_MIN_WINS, True),
             (f"play: oranges sealed in rings win {play_sealed} of 48", play_sealed >= engine.GREEDY_MIN_WINS, False),
             (f"sweep: sealed oranges are never reached ({sum(1 for i in sw['unreached'] if i < 25)} of 25)",
              all(i in sw["unreached"] for i in range(25)), True)]
    ok = True
    for name, got, want in cases:
        ok &= got == want
        if verbose:
            print(f"  {'ok ' if got == want else 'BAD'} engine: {name}: {got}, expected {want}")
    return ok


def run(verbose=True):
    results = {"preflight": author.selftest(verbose), "framecheck": frames.selftest(verbose),
               "readcheck": readability.selftest(verbose), "engine": engine_cases(verbose)}
    if verbose:
        print("self-test:", ", ".join(f"{k} {'ok' if v else 'FAILED'}" for k, v in results.items()))
    return all(results.values())
