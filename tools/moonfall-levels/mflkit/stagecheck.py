"""A stage's checks across its levels (the coordinator's round-1 decisions, the round-2 supervision): F7 between
neighbours, the stage's mean difficulty within a band, and the finale as the stage's hardest. Pure functions over
(id, ramp per 48, jewel hues) rows, so they prove themselves on synthetic stages before `mfl.py stage` trusts them."""

# Owner's decision, 9 October 2026: a stage is judged by the MEAN of its levels' ramp (the greedy player's wins per 48,
# pooled) within a band, plus a finale that is the stage's hardest level by a margin, following Peggle's measured curve.
# The old rule that each level is harder than the one before is gone: levels within a stage may come in any order.
#
# PLACEHOLDER BANDS. These numbers are NOT derived from Peggle's curve yet. They are the old charter table (section 1.1)
# moved to a mean band: the old opener/finale midpoint, plus or minus MEAN_HALF_WIDTH. They will be re-derived from
# Peggle's curve once the engine's rules change (a separate task is changing the engine) and the shipped levels are
# refitted. Until then, a stage that fails only on its mean band is a sign to re-derive, not to re-tune.
# Keyed by campaign and stage ("mr" The Moon Road, "fs" The Far Shore: the Far Shore's stages number from 1 again).
MEAN_HALF_WIDTH = 2.0       # PLACEHOLDER: the band's half-width around the old midpoint
_OLD_OPENER_FINALE = {"mr1": (30.0, 21.0), "mr2": (27.0, 20.0),
                      "mr3": (27.0, 19.0), "mr4": (26.5, 18.5), "mr5": (26.0, 18.0), "mr6": (25.0, 17.0), "mr7": (24.5, 16.5),
                      "mr8": (24.0, 16.0), "mr9": (23.0, 15.0), "mr10": (22.5, 14.5), "mr11": (22.0, 13.0),
                      "fs1": (25.0, 17.0), "fs2": (24.5, 16.5), "fs3": (24.0, 16.0), "fs4": (23.5, 15.5), "fs5": (23.0, 15.0),
                      "fs6": (22.5, 14.5), "fs7": (22.0, 14.0), "fs8": (21.5, 13.5), "fs9": (21.0, 13.0), "fs10": (20.5, 12.5),
                      "fs11": (20.0, 12.0), "fs12": (19.5, 11.0)}
# (low, high) of a stage's mean per 48: below `low` is harder than the band, above `high` easier
MEAN_BANDS = {k: ((a + b) / 2 - MEAN_HALF_WIDTH, (a + b) / 2 + MEAN_HALF_WIDTH) for k, (a, b) in _OLD_OPENER_FINALE.items()}

MEAN_TOL = 0.3              # the stage mean may sit this far outside its band: about one standard error of a mean of five
                            # levels (0.6 per level over 1728 games, 0.3 over the 6912 pooled)
FINALE_GAP = 2.5            # PLACEHOLDER, re-derived with the bands: the finale at least this far below the stage's
                            # next-hardest level (it was 2.5 below the 4th level)
# One held-out block cannot tell a gap of 2.5 from 2.0, so the finale gap is resolved: the gap less one standard error at
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
    # stage 1's band is the placeholder 23.5 to 27.5; the finale is the hardest by 3.0
    good = [("a", 27.0, (280, 350)), ("b", 26.0, (270, 190)), ("c", 25.5, (300, 0)), ("d", 25.0, (250, 330)),
            ("e", 22.0, (265, 180))]
    G = {i: HELD + CONFIRM_GAMES for i in "abcde"}

    def with_ramps(ramps, rows=good):
        return [(i, r, p) for ((i, _r, p), r) in zip(rows, ramps)]
    cases = [("a good stage 1, confirmed", faults("mr1", good, games=G), None),
             ("the same stage on the held-out block alone (a gap of 3.0 over 1728 games is not resolved)",
              faults("mr1", good), "resolvably"),
             ("levels in any order: no level has to be harder than the one before (the dropped 0.5 step rule)",
              faults("mr1", with_ramps([25.0, 27.0, 25.5, 26.0, 22.0]), games=G), None),
             ("a level easier than the one before it, and equal levels, pass",
              faults("mr1", with_ramps([26.0, 26.0, 26.0, 26.0, 22.0]), games=G), None),
             ("a finale gap of 2.7 over 6912 games (critic N22)",
              faults("mr1", with_ramps([27.0, 26.0, 25.5, 25.0, 22.3]), games=G), "finale is not resolvably"),
             ("a finale only 1 below the next hardest", faults("mr1", with_ramps([27.0, 26.0, 25.5, 25.0, 24.0]), games=G),
              "must be the stage's hardest"),
             ("an earlier level harder than the finale", faults("mr1", with_ramps([27.0, 26.0, 21.0, 25.0, 22.0]), games=G),
              "must be the stage's hardest"),
             ("a stage whose mean is much harder than its band", faults("mr1", with_ramps([r - 3 for r in (27.0, 26.0, 25.5, 25.0, 22.0)]), games=G),
              "harder than its band"),
             ("a stage whose mean is much easier than its band", faults("mr1", with_ramps([r + 4 for r in (27.0, 26.0, 25.5, 25.0, 22.0)]), games=G),
              "easier than its band"),
             ("a mean 0.2 below the band's edge is inside the tolerance",
              faults("mr1", with_ramps([25.2, 24.2, 23.7, 23.2, 20.2]), games=G), None),
             ("a mean 0.4 below the band's edge is outside it",
              faults("mr1", with_ramps([25.0, 24.0, 23.5, 23.0, 20.0]), games=G), "harder than its band"),
             ("neighbours with the same jewels", faults("mr1", [good[0], ("b", 26.0, (285, 345))] + good[2:]), "F7"),
             ("the same palette swapped round", faults("mr1", [good[0], ("b", 26.0, (350, 280))] + good[2:]), "F7"),
             ("the stage before's last level", faults("mr2", good, before=("z", 20.0, (283, 352))), "F7")]
    fs = [("f", 22.0, (300, 20)), ("g", 21.0, (260, 160)), ("h", 20.5, (330, 175)), ("i", 20.0, (250, 200)),
          ("j", 16.5, (280, 345))]
    GF = {i: HELD + CONFIRM_GAMES for i in "fghij"}
    cases += [("a bare number is a Moon Road stage, and the key is case-blind",
               [] if stage_key(2) == "mr2" and stage_key("FS1") == "fs1" else ["bad"], None),
              ("a stage that is not in the charter",
               [] if all(_raises(a) for a in ("fs13", "mr12", "x", "0")) else ["bad"], None),
              ("a good Far Shore stage 1 (nothing before it), confirmed", faults("fs1", fs, games=GF), None),
              ("Far Shore stage 1 as Moon Road stage 3 is much harder than that band",
               faults("mr3", with_ramps([r - 6 for r in (22.0, 21.0, 20.5, 20.0, 16.5)], fs), games=GF), "harder than its band"),
              ("Far Shore stage 2 opening on stage 1's last palette", faults("fs2", [fs[0]], before=("z", 16.5, (300, 20))), "F7"),
              ("a stage of one level has no finale to judge", faults("fs2", [("k", 24.5, (250, 330))], before=("j", 17.0, (320, 175))), None),
              ("the previous stage stays inside a campaign",
               [] if previous_stage("mr3") == "mr2" and previous_stage("fs1") is None and previous_stage("fs12") == "fs11"
               else ["bad"], None),
              ("the campaign of a level id",
               [] if level_campaign("base-11") == "mr" and level_campaign("expansion-01") == "fs"
               and level_campaign("x-1") is None else ["bad"], None),
              ("every stage has a placeholder band of 2 either side of the old midpoint",
               [] if all(abs(hi - lo - 2 * MEAN_HALF_WIDTH) < 1e-9 for (lo, hi) in MEAN_BANDS.values())
               and MEAN_BANDS["mr1"] == (23.5, 27.5) else ["bad"], None)]
    ok = True
    for name, probs, want in cases:
        good_ = (not probs) if want is None else any(want in q for q in probs)
        ok &= good_
        if verbose:
            print(f"  {'ok ' if good_ else 'BAD'} stage: {name}: {probs[:1] if probs else 'passed'}")
    return ok
