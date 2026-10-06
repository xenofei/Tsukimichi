"""A stage's checks across its levels (the coordinator's round-1 decisions, the round-2 supervision): F7 between
neighbours, the difficulty band and its order, the finale. Pure functions over (id, ramp per 48, jewel hues) rows, so
they prove themselves on synthetic stages before `mfl.py stage` trusts them."""

# the bands (the held-out ramp, 1728 games at each level's own number): the stage's first level about this easy, its
# last about this hard, within the ramp's noise
BANDS = {1: (30.0, 21.0), 2: (27.0, 20.0)}
NOISE = 0.6                 # one standard error per 48 over 1728 games ("about" a band end: within two of them)
STEP = 0.5                  # each level at least this much harder than the one before (game designer round 2, G2)
FINALE_GAP = 2.5            # the finale at least this far below its 4th level


def hue_distance(a, b):
    return 360 if a is None or b is None else min(abs(a - b), 360 - abs(a - b))


def same_jewels(a, b):
    """Two levels' jewel pairs within 30 degrees in both hues, compared either way round (a violet-and-rose pair is the
    same palette whichever leads: game designer round 3, G15)."""
    return min(max(hue_distance(a[0], b[0]), hue_distance(a[1], b[1])),
               max(hue_distance(a[0], b[1]), hue_distance(a[1], b[0]))) < 30


def faults(stage, rows, before=None):
    """rows: [(id, ramp, (first hue, second hue))] in play order; before: the previous stage's last row, which the
    player meets just before this stage's first. The problems, empty if none."""
    P = []
    prev = before[2] if before else None
    for (lid, _ramp, pair) in rows:
        if prev is not None and same_jewels(pair, prev):
            P.append(f"F7: {lid} has the same two jewels as the level before it ({prev} then {pair})")
        prev = pair
    ramps = [r for (_l, r, _p) in rows]
    if len(rows) < 2 or any(r is None for r in ramps):
        return P
    for (a, ra, _), (b, rb, _) in zip(rows, rows[1:]):
        if rb > ra - STEP:
            P.append(f"ramp: {b} ({rb}) is not at least {STEP} harder than {a} ({ra})")
    if len(rows) >= 4:
        fin, fourth = ramps[-1], ramps[-2]
        if not (fin <= min(ramps[:-1]) and fin <= fourth - FINALE_GAP):
            P.append(f"ramp: the finale ({fin}) must be the stage's hardest and {FINALE_GAP} or more below its 4th "
                     f"level ({fourth})")
    if stage in BANDS:
        top, bottom = BANDS[stage]
        if ramps[0] < top - 2 * NOISE:
            P.append(f"band: the stage opens at {ramps[0]}, harder than its band's {top} (within {2 * NOISE:.1f})")
        if ramps[-1] < bottom - 2 * NOISE:
            P.append(f"band: the stage closes at {ramps[-1]}, harder than its band's {bottom} (within {2 * NOISE:.1f})")
        if ramps[0] > top + 3 * NOISE or ramps[-1] > bottom + 3 * NOISE:
            P.append(f"band: the stage runs {ramps[0]} to {ramps[-1]}, much easier than its band ({top} to {bottom})")
    return P


def selftest(verbose=True):
    good = [("a", 29.5, (280, 350)), ("b", 27.0, (270, 190)), ("c", 25.0, (300, 0)), ("d", 23.5, (250, 330)),
            ("e", 20.5, (265, 180))]
    cases = [("a good stage 1", faults(1, good), None),
             ("neighbours with the same jewels", faults(1, [good[0], ("b", 27.0, (285, 345))] + good[2:]), "F7"),
             ("a level easier than the one before", faults(1, good[:2] + [("c", 27.4, (300, 0))] + good[3:]), "not at least"),
             ("a finale only 1 below its 4th", faults(1, good[:4] + [("e", 22.5, (265, 180))]), "finale"),
             ("a stage opening at 26", faults(1, [("a", 26.0, (280, 350))] + [(i, r - 3.5, p) for (i, r, p) in good[1:]]),
              "opens"),
             ("a finale at 17", faults(1, good[:4] + [("e", 17.0, (265, 180))]), "closes"),
             ("a stage much easier than its band", faults(1, [(i, r + 4, p) for (i, r, p) in good]), "much easier"),
             ("the same palette swapped round", faults(1, [good[0], ("b", 27.0, (350, 280))] + good[2:]), "F7"),
             ("the stage before's last level", faults(2, good, before=("z", 20.0, (283, 352))), "F7")]
    ok = True
    for name, probs, want in cases:
        good_ = (not probs) if want is None else any(want in q for q in probs)
        ok &= good_
        if verbose:
            print(f"  {'ok ' if good_ else 'BAD'} stage: {name}: {probs[:1] if probs else 'passed'}")
    return ok
