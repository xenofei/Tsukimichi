"""A stage's checks across its levels (the coordinator's round-1 decisions, the round-2 supervision): F7 between
neighbours, the stage's mean difficulty within a band, and the finale harder than the stage's average. Pure functions over
(id, ramp per 48, jewel hues) rows, so they prove themselves on synthetic stages before `mfl.py stage` trusts them."""
import math
from .engine import GREEDY_MIN_WINS

# Owner's decision, 9 October 2026: a stage is judged by the MEAN of its levels' ramp (the greedy player's wins per 48,
# pooled) within a band, plus a finale harder than the mean of the stage's other levels, following Peggle's measured curve. The old rule
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
# The band's lower edge never goes below 2 of 48, the playability floor (owner's decision, 9 October 2026, "Follow
# Peggle, lower the floor": the floor drops from 5 of 48 to 2). The bands are Peggle's stage means, no longer clamped at
# 5: only Moon Road stages 9-10 and Far Shore stages 10-12 reach down to the 2.0 edge (see charter section 1.1).
STAGE_FLOOR = float(GREEDY_MIN_WINS)
# (low, high) of a stage's mean per 48: below `low` is harder than the band, above `high` easier
MEAN_BANDS = {k: (round(max(m - MEAN_HALF_WIDTH, STAGE_FLOOR), 2), round(m + MEAN_HALF_WIDTH, 2))
              for k, m in PEGGLE_STAGE_MEANS.items()}

MEAN_TOL = 0.3              # the stage mean may sit this far outside its band: about one standard error of a mean of five
                            # levels (0.6 per level over 1728 games, 0.3 over the 6912 pooled)
# The finale (owner's decision, 9 October 2026, "Harder than the stage's average"): it wins fewer games per 48 than the
# mean of the stage's other four levels, resolved by one standard error on the pooled games. It need not be the single
# hardest: Peggle's finale is below the mean of its stage's other four in 17 of 23 stages (15 resolvably) and the hardest
# level in only 7.
# One held-out block cannot resolve a small gap, so the finale gap is resolved: the gap to the others' mean less one
# standard error at least 0, over the held-out games and CONFIRM_GAMES more fresh ones per level (game designer rounds 5 and 6,
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
    stage's mean ramp within its band; the finale below the others' mean ramp, resolved by one standard error.
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
    others = rows[:-1]
    nf = games.get(lf, HELD)
    mean_o = sum(r for (_l, r, _p) in others) / len(others)
    se_o = math.sqrt(sum(se48(r, games.get(l, HELD)) ** 2 for (l, r, _p) in others)) / len(others)   # the pooled mean's error
    se_gap = math.hypot(se48(fin, nf), se_o)
    if fin >= mean_o:
        P.append(f"ramp: the finale {lf} ({fin}) must win fewer games than the mean of the stage's other levels ({mean_o:.2f})")
    elif mean_o - fin - se_gap < 0:
        P.append(f"ramp: the finale is not resolvably harder than the others' mean: a gap of {mean_o - fin:.2f} "
                 f"+- {se_gap:.2f} over {nf} games and the pooled others")
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
    # the others' mean is 9.25 (SE 0.2 over 6912 games each, 0.1 pooled); a finale needs a gap over its own and that error
    cases = [("a good stage 1, confirmed", faults("mr1", good, games=G), None),
             ("a finale 0.3 below the others' mean on the held-out block alone (not resolved)",
              faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 8.95])), "not resolvably"),
             ("a finale 2 below the others' mean over 6912 games (resolved)",
              faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 7.25]), games=G), None),
             ("the finale need not be the single hardest: one earlier level below it passes",
              faults("mr1", with_ramps([10.0, 9.5, 5.5, 8.5, 6.0]), games=G), None),
             ("levels in any order: no level has to be harder than the one before",
              faults("mr1", with_ramps([8.5, 10.0, 9.0, 9.5, 6.0]), games=G), None),
             ("a finale equal to the others' mean", faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 9.25]), games=G),
              "must win fewer games"),
             ("a finale above the others' mean", faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 9.6]), games=G),
              "must win fewer games"),
             ("a finale 0.1 below the others' mean, over 6912 games (inside one standard error)",
              faults("mr1", with_ramps([10.0, 9.5, 9.0, 8.5, 9.15]), games=G), "not resolvably"),
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
    # a stage at Peggle's Moon Road stage 9 (mean 2.18): every level 2 of 48 or more, the finale below the others' mean
    low = [("k", 2.6, (300, 20)), ("l", 2.8, (260, 160)), ("m", 2.4, (330, 175)), ("n", 2.5, (250, 200)),
           ("o", 1.4, (280, 345))]
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
              ("a stage at Peggle's Moon Road stage 9 mean passes its band (lower edge 2.0)", faults("mr9", low, games=GK), None),
              ("the same stage judged as Moon Road stage 3 is harder than that band",
               faults("mr3", low, games=GK), "harder than its band"),
              ("the previous stage stays inside a campaign",
               [] if previous_stage("mr3") == "mr2" and previous_stage("fs1") is None and previous_stage("fs12") == "fs11"
               else ["bad"], None),
              ("the campaign of a level id",
               [] if level_campaign("base-11") == "mr" and level_campaign("expansion-01") == "fs"
               and level_campaign("x-1") is None else ["bad"], None),
              ("every stage of both campaigns has a band, Peggle's mean +- 1.1, the lower edge at least 2",
               [] if len(MEAN_BANDS) == 23 and MEAN_BANDS["mr1"] == (7.47, 9.67) and MEAN_BANDS["fs1"] == (9.24, 11.44)
               and MEAN_BANDS["mr2"] == (5.13, 7.33) else ["bad"], None),
              ("the lower edge is clamped at 2.0 and never below; above it the band is the mean +- 1.1",
               [] if all(lo >= STAGE_FLOOR for (lo, _hi) in MEAN_BANDS.values()) and STAGE_FLOOR == 2.0
               and MEAN_BANDS["mr9"] == (2.0, 3.28) and MEAN_BANDS["fs11"] == (2.0, 2.43)
               and MEAN_BANDS["mr3"] == (3.04, 5.24) else ["bad"], None)]
    ok = True
    for name, probs, want in cases:
        good_ = (not probs) if want is None else any(want in q for q in probs)
        ok &= good_
        if verbose:
            print(f"  {'ok ' if good_ else 'BAD'} stage: {name}: {probs[:1] if probs else 'passed'}")
    return ok
