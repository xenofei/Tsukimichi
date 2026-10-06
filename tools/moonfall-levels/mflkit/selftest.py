"""Every checker proves itself before it is trusted: known-bad shapes must fail and known-good ones must pass (the
supervisors' rule since rich pass 2, round 3). `mfl.py build` runs this first and stops if any case is wrong."""
import json
import math

from . import author, engine, frames, readability, stagecheck
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
    # cheap difficulty on synthetic results: one low orange left in 40% of lost games; low candidates a fifth of the
    # deal but half of what is left; and an even spread
    homes = [((100 + 20 * k, 480.0 if k < 6 else 250.0), True) for k in range(30)]
    one = {"holdouts": [{"piece": 0, "at": [100, 480], "share_of_lost_games": 0.40}], "low_left_share": 0.2}
    ratio = {"holdouts": [{"piece": 0, "at": [100, 480], "share_of_lost_games": 0.10}], "low_left_share": 0.5}
    even = {"holdouts": [{"piece": 0, "at": [100, 480], "share_of_lost_games": 0.10}], "low_left_share": 0.22}

    # the colours gate: a green dealt to a never-green piece
    lv = {"pegs": [{"x": 1, "y": 1, "canBeGreen": False}, {"x": 2, "y": 2}], "bricks": []}
    from .build import green_faults
    play_sealed = engine.play(sealed, 5, games=48)["greedy_won"] if ok_sealed else -1
    sw_good, sw_decks = engine.sweep(good, 5), engine.sweep(decks, 5)
    rc_good, rc_corner = engine.reach(good, 5), engine.reach(corner_path, 5)
    n_corner = len(corner["pegs"]) - 1
    cases = [("loader: an approved pilot", ok_good, True), ("loader: a peg in the launcher's swing", ok_bad, False),
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
             ("cheap difficulty: one low orange left in 40% of lost games", not engine.cheap(one, homes), False),
             ("cheap difficulty: low candidates 20% of the deal, 50% of what is left", not engine.cheap(ratio, homes), False),
             ("cheap difficulty: low oranges left in their share", not engine.cheap(even, homes), True),

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


def run(verbose=True):
    results = {"preflight": author.selftest(verbose), "framecheck": frames.selftest(verbose),
               "readcheck": readability.selftest(verbose), "stage": stagecheck.selftest(verbose),
               "engine": engine_cases(verbose)}
    if verbose:
        print("self-test:", ", ".join(f"{k} {'ok' if v else 'FAILED'}" for k, v in results.items()))
    return all(results.values())
