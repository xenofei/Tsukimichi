"""A stage's checks across its levels (the coordinator's round-1 decisions, the round-2 supervision): F7 between
neighbours, the stage's mean difficulty within a band, and the finale as the stage's hardest. Pure functions over
(id, ramp per 48, jewel hues) rows, so they prove themselves on synthetic stages before `mfl.py stage` trusts them."""
from .engine import GREEDY_MIN_WINS

# Owner's decision, 9 October 2026: a stage is judged by the MEAN of its levels' ramp (the greedy player's wins per 48,
# pooled) within a band, plus a finale that is the stage's hardest level, following Peggle's measured curve. The old rule
# that each level is harder than the one before is gone: levels within a stage may come in any order.
#
# The bands are measured from Peggle's campaigns on the new engine (2026-10-09): every campaign level of Peggle Deluxe
# (11 stages, The Moon Road) and Peggle Nights (12 stages, The Far Shore) converted to our level format in scratch (no
# Peggle level enters the repo) and played by mfcheck's greedy player over the 1728 held-out games (seeds 865-2592) at
# its campaign number. PEGGLE_STAGE_MEANS is each stage's mean per 48 (one standard error 0.08-0.21).
# Keyed by campaign and stage ("mr" The Moon Road, "fs" The Far Shore: the Far Shore's stages number from 1 again).
PEGGLE_STAGE_MEANS = {"mr1": 8.57, "mr2": 6.23, "mr3": 4.14, "mr4": 3.86, "mr5": 4.78, "mr6": 4.77, "mr7": 5.36,
                      "mr8": 3.72, "mr9": 2.18, "mr10": 2.84, "mr11": 3.08,
                      "fs1": 10.34, "fs2": 7.43, "fs3": 5.24, "fs4": 4.23, "fs5": 4.68, "fs6": 3.56, "fs7": 4.11,
                      "fs8": 3.66, "fs9": 4.16, "fs10": 1.87, "fs11": 1.33, "fs12": 2.57}
# The band's half-width: how far a stage's mean, set by five hand-made levels, sits from the curve by design. Peggle's
# own levels spread 2.5 per 48 about their stage's mean (the pooled within-stage standard deviation), so a mean of five
# carries 2.5 / sqrt(5) = 1.1. The measurement itself (0.1-0.2) and the converter's approximations (levels converted
# cleanly sit 0.4 per 48 from the rest, within their stages) are smaller.
MEAN_HALF_WIDTH = 1.1
# The playability floor (decision 21): every level wins at least 5 of 48, so no stage's mean can be below it. Where
# Peggle's curve is lower, the band is raised to start at the floor and keeps its width (FLOOR to FLOOR + 2 x
# MEAN_HALF_WIDTH): Moon Road stages 3-11 and Far Shore stages 3-12 (see charter section 1.1).
STAGE_FLOOR = float(GREEDY_MIN_WINS)
# (low, high) of a stage's mean per 48: below `low` is harder than the band, above `high` easier
MEAN_BANDS = {k: (round(max(m - MEAN_HALF_WIDTH, STAGE_FLOOR), 2), round(max(m + MEAN_HALF_WIDTH, STAGE_FLOOR + 2 * MEAN_HALF_WIDTH), 2))
              for k, m in PEGGLE_STAGE_MEANS.items()}

MEAN_TOL = 0.3              # the stage mean may sit this far outside its band: about one standard error of a mean of five
                            # levels (0.6 per level over 1728 games, 0.3 over the 6912 pooled)
# The finale: the stage's hardest, by FINALE_GAP or more after one standard error. Peggle's finale is the greedy player's
# hardest level in only 7 of its 23 stages (4 resolvably: gaps of 0.03 to 3.2), and below the mean of its stage's other
# four in 17 (15 resolvably). No positive margin over the next hardest is met in most of Peggle's stages, so the gap is
# 0: the finale must be resolvably the hardest, the strictest rule that keeps the owner's "hardest level". Whether to
# follow Peggle instead (the finale below the others' mean) is the owner's question (charter section 1.1).
FINALE_GAP = 0.0
# One held-out block cannot resolve a small gap, so the finale gap is resolved: the gap less one standard error at
# least FINALE_GAP, over the held-out games and CONFIRM_GAMES more fresh ones per level (game designer rounds 5 and 6,
# G20 and G22; critic N22)
CONFIRM_GAMES, HELD = 5184, 1728


def stage_key(arg):
    """A stage argument as its key: "mr3", "fs1", or a bare number (a Moon Road stage, as before). ValueError if it is
    none of the charter's stages."""
    a = str(arg).strip().lower()
    key = ("mr" + a) if a.isdigit() else a
    if key not in MEAN_BANDS:
        raise ValueError(f"unknown stage {arg!r}: use mr1-mr11, fs1-fs12 (a bare number is a Moon Road stage)")
    return key


def level_campaign(level_id):
    """The campaign key of a level id: base-NN is The Moon Road ("mr"), expansion-NN The Far Shore ("fs")."""
    return {"base": "mr", "expansion": "fs"}.get(str(level_id).split("-")[0])


def previous_stage(key):
    """The stage the player meets just before this one within its campaign, or None (the Far Shore's first)."""
    camp, n = key[:2], int(key[2:])
    return f"{camp}{n - 1}" if n > 1 else None


def se48(ramp, games):
    """The standard error of a ramp per 48 (wins per 48 games) over `games` games."""
    import math
    p = min(max(ramp / 48.0, 1e-6), 1 - 1e-6)
    return 48.0 * math.sqrt(p * (1 - p) / games)


def step_se(ra, rb, na, nb):
    """The standard error of a difference between two ramps."""
    import math
    return math.hypot(se48(ra, na), se48(rb, nb))


def step_resolved(ra, rb, na, nb, floor):
    """Whether b is resolvably at least `floor` harder than a: the difference less one standard error."""
    return (ra - rb) - step_se(ra, rb, na, nb) >= floor


def hue_distance(a, b):
    return 360 if a is None or b is None else min(abs(a - b), 360 - abs(a - b))


def same_jewels(a, b):
    """Two levels' jewel pairs within 30 degrees in both hues, compared either way round (a violet-and-rose pair is the
    same palette whichever leads: game designer round 3, G15)."""
    return min(max(hue_distance(a[0], b[0]), hue_distance(a[1], b[1])),
               max(hue_distance(a[0], b[1]), hue_distance(a[1], b[0]))) < 30


def faults(stage, rows, before=None, games=None):
    """rows: [(id, ramp, (first hue, second hue))] in play order, the finale last; before: the previous stage's last row,
    which the player meets just before this stage's first (None for the Far Shore's stage 1: nothing); games: {id:
    games its ramp is over} (HELD when absent). The problems, empty if none. The rules: F7 between neighbours; the
    stage's mean ramp within its band; the finale the stage's hardest by FINALE_GAP, resolved by one standard error.
    There is no order rule between the other levels."""
    stage = stage_key(stage)
    games = games or {}
    P = []
    prev = before[2] if before else None
    for (lid, _ramp, pair) in rows:
        if prev is not None and same_jewels(pair, prev):
            P.append(f"F7: {lid} has the same two jewels as the level before it ({prev} then {pair})")
        prev = pair
    ramps = [r for (_l, r, _p) in rows]
    if len(rows) < 2 or any(r is None for r in ramps):
        return P
    lf, fin, _ = rows[-1]
    l2, second, _ = min(rows[:-1], key=lambda r: r[1])       # the next hardest: the lowest ramp among the others
    nf, n2 = games.get(lf, HELD), games.get(l2, HELD)
    if fin > second - FINALE_GAP:
        P.append(f"ramp: the finale {lf} ({fin}) must be the stage's hardest, {FINALE_GAP} or more below the next hardest, "
                 f"{l2} ({second})")
    elif not step_resolved(second, fin, n2, nf, FINALE_GAP):
        P.append(f"ramp: the finale is not resolvably {FINALE_GAP} below {l2}: a gap of {second - fin:.2f} "
                 f"+- {step_se(second, fin, n2, nf):.2f} over {n2} and {nf} games")
    lo, hi = MEAN_BANDS[stage]
    mean = sum(ramps) / len(ramps)
    if mean < lo - MEAN_TOL:
        P.append(f"band: the stage's mean is {mean:.2f}, harder than its band's {lo:.2f} to {hi:.2f} (within {MEAN_TOL})")
    elif mean > hi + MEAN_TOL:
        P.append(f"band: the stage's mean is {mean:.2f}, easier than its band's {lo:.2f} to {hi:.2f} (within {MEAN_TOL})")
    return P


def _raises(arg):
    try:
        stage_key(arg)
    except ValueError:
        return True
    return False


def selftest(verbose=True):
    # Moon Road stage 1's band is Peggle's 8.57 +- 1.1 (7.47 to 9.67); the finale is the hardest by 2.5
    good = [("a", 10.0, (280, 350)), ("b", 9.5, (270, 190)), ("c", 9.0, (300, 0)), ("d", 8.5, (250, 330)),
            ("e", 6.0, (265, 180))]
    G = {i: HELD + CONFIRM_GAMES for i in "abcde"}

    def with_ramps(ramps, rows=good):
        return [(i, r, p) for ((i, _r, p), r) in zip(rows, ramps)]
    lo1 = MEAN_BANDS["mr1"][0]
    mean_good = sum(r for (_i, r, _p) in good) / len(good)
    cases = [("a good stage 1, confirmed", faults("mr1", good, games=G), None),
             ("a finale 0.5 below the next hardest on the held-out block alone (+-0.58 over 1728 games: not resolved)",
              faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 8.0])), "resolvably"),
             ("the same finale over 6912 games (+-0.29: resolved)",
              faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 8.0]), games=G), None),
             ("levels in any order: no level has to be harder than the one before (the dropped 0.5 step rule)",
              faults("mr1", with_ramps([8.5, 10.0, 9.0, 9.5, 6.0]), games=G), None),
             ("a level easier than the one before it, and equal levels, pass",
              faults("mr1", with_ramps([9.0, 9.0, 9.0, 9.0, 6.0]), games=G), None),
             ("a finale tied with the next hardest, over 6912 games",
              faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 8.5]), games=G), "finale is not resolvably"),
             ("a finale 0.1 below the next hardest, over 6912 games",
              faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 8.4]), games=G), "finale is not resolvably"),
             ("an earlier level harder than the finale", faults("mr1", with_ramps([10.0, 9.5, 5.0, 8.5, 6.0]), games=G),
              "must be the stage's hardest"),
             ("a stage whose mean is much harder than its band", faults("mr1", with_ramps([r - 3 for r in (10.0, 9.5, 9.0, 8.5, 6.0)]), games=G),
              "harder than its band"),
             ("a stage whose mean is much easier than its band", faults("mr1", with_ramps([r + 4 for r in (10.0, 9.5, 9.0, 8.5, 6.0)]), games=G),
              "easier than its band"),
             ("a mean 0.2 below the band's edge is inside the tolerance",
              faults("mr1", with_ramps([r + lo1 - 0.2 - mean_good for r in (10.0, 9.5, 9.0, 8.5, 6.0)]), games=G), None),
             ("a mean 0.4 below the band's edge is outside it",
              faults("mr1", with_ramps([r + lo1 - 0.4 - mean_good for r in (10.0, 9.5, 9.0, 8.5, 6.0)]), games=G), "harder than its band"),
             ("neighbours with the same jewels", faults("mr1", [good[0], ("b", 9.5, (285, 345))] + good[2:]), "F7"),
             ("the same palette swapped round", faults("mr1", [good[0], ("b", 9.5, (350, 280))] + good[2:]), "F7"),
             ("the stage before's last level", faults("mr2", good, before=("z", 6.0, (283, 352))), "F7")]
    # Far Shore stage 1's band is Peggle's 10.34 +- 1.1 (9.24 to 11.44)
    fs = [("f", 12.0, (300, 20)), ("g", 11.5, (260, 160)), ("h", 11.0, (330, 175)), ("i", 10.5, (250, 200)),
          ("j", 8.0, (280, 345))]
    GF = {i: HELD + CONFIRM_GAMES for i in "fghij"}
    # a stage at the floor: every level 5 of 48 or more, the finale resolvably the hardest
    floor = [("k", 6.4, (300, 20)), ("l", 6.2, (260, 160)), ("m", 6.0, (330, 175)), ("n", 5.9, (250, 200)),
             ("o", 5.3, (280, 345))]
    GK = {i: HELD + CONFIRM_GAMES for i in "klmno"}
    cases += [("a bare number is a Moon Road stage, and the key is case-blind",
               [] if stage_key(2) == "mr2" and stage_key("FS1") == "fs1" else ["bad"], None),
              ("a stage that is not in the charter",
               [] if all(_raises(a) for a in ("fs13", "mr12", "x", "0")) else ["bad"], None),
              ("a good Far Shore stage 1 (nothing before it), confirmed", faults("fs1", fs, games=GF), None),
              ("Far Shore stage 1's levels as Moon Road stage 3 are much easier than that band",
               faults("mr3", fs, games=GF), "easier than its band"),
              ("Far Shore stage 2 opening on stage 1's last palette", faults("fs2", [fs[0]], before=("z", 8.0, (300, 20))), "F7"),
              ("a stage of one level has no finale to judge", faults("fs2", [("k", 7.5, (250, 330))], before=("j", 8.0, (320, 175))), None),
              ("a stage at the floor passes a band raised to it (Moon Road stage 9)", faults("mr9", floor, games=GK), None),
              ("a stage at Peggle's own Moon Road stage 9 mean (2.2) is harder than the raised band",
               faults("mr9", with_ramps([3.0, 2.6, 2.2, 2.4, 0.8], floor), games=GK), "harder than its band"),
              ("the previous stage stays inside a campaign",
               [] if previous_stage("mr3") == "mr2" and previous_stage("fs1") is None and previous_stage("fs12") == "fs11"
               else ["bad"], None),
              ("the campaign of a level id",
               [] if level_campaign("base-11") == "mr" and level_campaign("expansion-01") == "fs"
               and level_campaign("x-1") is None else ["bad"], None),
              ("every stage of both campaigns has a band, Peggle's mean +- 1.1",
               [] if len(MEAN_BANDS) == 23 and MEAN_BANDS["mr1"] == (7.47, 9.67) and MEAN_BANDS["fs1"] == (9.24, 11.44)
               and MEAN_BANDS["mr2"] == (5.13, 7.33) else ["bad"], None),
              ("no band asks for a mean below the 5-of-48 floor; a raised band keeps its width",
               [] if all(lo >= STAGE_FLOOR and hi - lo >= 2 * MEAN_HALF_WIDTH - 1e-9 for (lo, hi) in MEAN_BANDS.values())
               and MEAN_BANDS["mr9"] == (5.0, 7.2) and MEAN_BANDS["fs11"] == (5.0, 7.2) else ["bad"], None)]
    ok = True
    for name, probs, want in cases:
        good_ = (not probs) if want is None else any(want in q for q in probs)
        ok &= good_
        if verbose:
            print(f"  {'ok ' if good_ else 'BAD'} stage: {name}: {probs[:1] if probs else 'passed'}")
    return ok
