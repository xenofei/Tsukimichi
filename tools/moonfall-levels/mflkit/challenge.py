"""Challenge carriers (production charter 1.6): a level that carries a challenge with more oranges than the standard
25 must still be winnable by the greedy player (decision 21: at least 5 of seeds 1-48) at that orange count.
`mfl.py challenge <ch-id>` plays every carrier of a recorded challenge; `mfl.py challenge <level-id> <oranges>` plays any
level at any count (a check, not a record). Pure rules first, so they prove themselves in the self-test."""

MIN_WINS = 5                # of 48, the rule's games (seeds 1-48), as for every level (decision 21)
GAMES = 48
STANDARD_CANDIDATES = (28, 35)   # the pre-flight's range for a level's orange candidates
MAX_ORANGES = 35                 # the most oranges a challenge may ask of a level, unless EXCEPTIONS allows more

# The recorded challenges that put a level under a changed rule. `levels` are the charter's carriers (section 1.6): the
# stage tables' ids. The shipped challenges.json may still name earlier carriers; the coordinator moves them when the
# carriers ship. `oranges` is the count the challenge asks (25 is the standard deal: the rule's own check).
CARRIERS = {
    "ch-03": {"levels": ["base-27"], "oranges": 35, "note": "Thirty-Five Moons: 6-2 The Wheel of the Twelve, built with exactly 35 candidates"},
    "ch-04": {"levels": ["base-37"], "oranges": 45, "note": "Forty-Five Moons: 8-2 A Thousand Lanterns, built with 45 candidates (the recorded exception)"},
    "ch-06": {"levels": ["base-13"], "oranges": 25, "note": "clear every peg: 3-3 The Holy See, a moderate piece count"},
    "ch-08": {"levels": ["base-10", "base-15", "base-20", "base-25"], "oranges": 25, "note": "the stage finales 2 to 5"},
    "ch-09": {"levels": ["base-43"], "oranges": 25, "note": "250,000 with 7 balls: 9-3 Crystal Light (the score is the coordinator's recalibration)"},
    "ch-11": {"levels": ["base-21", "base-22", "base-23"], "oranges": 25, "note": "duel with Merlwyb: 5-1 to 5-3"},
    "ch-12": {"levels": ["base-46", "base-48", "base-50"], "oranges": 25, "note": "duel with Louisoix: 10-1, 10-3, 10-5"},
}

# The only levels allowed an orange count above MAX_ORANGES, and the only levels allowed the candidates it needs (the
# pre-flight otherwise holds 28-35). Charter 1.6 and the owner's answer 5 (7 October 2026): ch-04's carrier, nothing else.
# Every other rule still holds on such a level (10 per 200 x 200, 60-160 pieces, the reach of every candidate).
EXCEPTIONS = {
    "base-37": {"oranges": 45, "reason": "ch-04 Forty-Five Moons: 8-2 A Thousand Lanterns carries 45 candidates "
                                         "(charter 1.6; the owner's answer to question 5, 7 October 2026)"},
}


def candidate_range(level_id):
    """The orange-candidate count the pre-flight accepts for a level: 28-35, or exactly the exception's count."""
    ex = EXCEPTIONS.get(level_id)
    return (ex["oranges"], ex["oranges"]) if ex else STANDARD_CANDIDATES


def faults(level_id, oranges, candidates, wins, games=GAMES):
    """The problems of a carrier: oranges above the maximum without an exception, more oranges than candidates, or
    fewer than MIN_WINS wins of `games`. Empty if none."""
    P = []
    ex = EXCEPTIONS.get(level_id)
    if oranges > MAX_ORANGES and not (ex and oranges <= ex["oranges"]):
        P.append(f"{level_id}: {oranges} oranges is above {MAX_ORANGES} and the level is not an allowed exception")
    if candidates is not None and oranges > candidates:
        P.append(f"{level_id}: {oranges} oranges but only {candidates} pieces may be orange")
    if wins is not None and wins < MIN_WINS:
        P.append(f"{level_id}: the greedy player wins {wins} of {games} with {oranges} oranges (needs {MIN_WINS})")
    return P


def candidates_of(path):
    import json
    d = json.loads(path.read_text(encoding="utf-8"))
    return sum(1 for p in d["pegs"] + d["bricks"] if p.get("canBeOrange"))


def play(level_id, oranges, force=False):
    """Plays the shipped level file's rule games (seeds 1-48) at `oranges`. (candidates, wins, problems), or None when
    the level is not built yet."""
    from . import engine, paths
    from .build import load_layout
    path = paths.JSON_OUT / f"{level_id}.json"
    if not path.exists():
        return None
    number = load_layout(level_id).LEVEL["number"]
    pl = engine.play(path, number, GAMES, first=0, oranges=oranges)
    cands = candidates_of(path)
    return cands, pl["greedy_won"], faults(level_id, oranges, cands, pl["greedy_won"])


def selftest(verbose=True):
    cases = [("35 oranges, 35 candidates, 9 wins", faults("base-27", 35, 35, 9), None),
             ("35 oranges on 28 candidates", faults("base-27", 35, 28, 9), "may be orange"),
             ("a challenge level the greedy player cannot win (4 of 48)", faults("base-27", 35, 35, 4), "needs 5"),
             ("exactly 5 of 48 passes", faults("base-27", 35, 35, 5), None),
             ("45 oranges on the recorded exception", faults("base-37", 45, 45, 7), None),
             ("45 oranges on any other level", faults("base-27", 45, 45, 7), "not an allowed exception"),
             ("46 oranges even on the exception", faults("base-37", 46, 46, 7), "not an allowed exception"),
             ("45 oranges on the exception, 5 wins short", faults("base-37", 45, 45, 3), "needs 5"),
             ("the standard candidates are 28-35", [] if candidate_range("base-27") == (28, 35) else ["bad"], None),
             ("the exception is 45 candidates, nobody else", [] if candidate_range("base-37") == (45, 45)
              and candidate_range("base-36") == (28, 35) and list(EXCEPTIONS) == ["base-37"] else ["bad"], None),
             ("ch-03 and ch-04 are recorded on the charter's carriers",
              [] if CARRIERS["ch-03"]["levels"] == ["base-27"] and CARRIERS["ch-03"]["oranges"] == 35
              and CARRIERS["ch-04"]["levels"] == ["base-37"] and CARRIERS["ch-04"]["oranges"] == 45 else ["bad"], None)]
    ok = True
    for name, probs, want in cases:
        good = (not probs) if want is None else any(want in q for q in probs)
        ok &= good
        if verbose:
            print(f"  {'ok ' if good else 'BAD'} challenge: {name}: {probs[:1] if probs else 'passed'}")
    return ok
