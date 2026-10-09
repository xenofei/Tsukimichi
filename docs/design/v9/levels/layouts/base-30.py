"""6-5 "The Moon Gate" (The Silent Stars, Urianger): orbit movers, the stage's finale.

Subject: Urianger's Moon Gate, a great ring of pale light in the night sky, ringed by orbiting moons; through it another
sky turns toward the far moon at its heart, and the moon's phases stand over it in a crown (painters/moongate.py).

Technique (OR): the gate's moons ride its two painted orbits as engine orbit movers, ten on the outer track turning
clockwise and six on the inner one turning the other way, so the rings counter-rotate round the gate as one figure. The
rim itself is still: a crown of arc bricks over its top (the carved dial's upper arc) and moons round its foot just
inside the bright core. The far moon is ringed by five moons; each of the crown's twelve phases carries a moon; the
standing stones on the two ridges wear a moon on their tops, the upper crests are dotted and the lower crests are short
brick runs. Between each pair of phases a star stands out as the crown's point, joined to its moons by a thread of light.

The finale's difficulty comes from the whole board, not its bottom: the crown of brick closes over the gate from 186 to
354 degrees and the rim's moons stand at 10, 30, ... 170 degrees, so the gate's inside (the far moon's ring, the inner
orbit, the rim's upper moons) is reached through narrow side openings past the turning outer ring; the crown's star
points carry the sky's hard places. 32 candidates (the charter's): only the lower left stone sits below y 430 (the
spread rule's candidate below y 400 on the left half), and low oranges are 3% of those left. Tuned on seeds 1-864 (16.0)
and the second tuning block (seeds 20001-23456: 16.5); held out 17.4 against the target 17. The left gibbous phase was
the easiest orange and the right crown point (646, 289) the hardest (27-32% of lost games); both are blue, and no
holdout now passes 22%. Two wall-tucked ridge candidates (84, 404) and (716, 401) were tried and dropped: they carried
a third of the lost games each.
"""
import math

LEVEL = dict(id="base-30", name="The Moon Gate", stage=6, number=30, scene="moongate",
             subject="Urianger's Moon Gate: a ring of light ringed by orbiting moons, the far moon at its heart and "
                     "the moon's phases in a crown over it",
             technique="orbit movers (the gate's moons on its two painted tracks, counter-rotating) with the rim "
                       "traced in brick and moons")

OUTER = dict(n=10, period=30.0, clockwise=True, oranges=(0, 1, 2, 7, 8, 9))
INNER = dict(n=6, period=20.0, clockwise=False, oranges=(1, 3, 5))
R_MARKS = 98.0                  # the rim's moons stand just inside its bright core (F6: a moon on the core reads poorly)
MARKS = dict(first=10, last=170, step=20, oranges=(0, 1, 7, 8))   # every 20 degrees round the foot, east to west
CROWN = dict(start=186, sweep=168, n=7, oranges=())   # the gate crown's arc bricks (an odd count: a brick at the apex)
FAR_ORANGE = (1, 2, 4)          # the far moon's ring, clockwise from the top
# the crown of phases, by index from the left: the left gibbous (294, 146) stays blue, since it was the board's easiest
# orange (left in no lost game: the launcher's first shots take it) and its place goes to the crown point beside it
PHASE_ORANGE = tuple(k for k in range(12) if k != 5)
STONE_ORANGE = (0, 1, 2)        # the standing stones' moons (the lower left one: a candidate below y 400 on that half)
# the crown's star points: (255, 140) beside the blue gibbous; the right point (646, 289) is blue, since as an orange it
# was left in 27-32% of lost games, the board's lone skill shot (32 candidates, the charter's)
POINT_ORANGE = (1, 4)
RIDGE_BRICKS = True
RIDGE_ORANGE = {"ridge left": (), "ridge right": ()}


def build(b):
    cx, cy, R, _, _ = b.circle("gate")
    _, _, r_out, _, _ = b.circle("outer track")
    _, _, r_in, _, _ = b.circle("inner track")
    # the gate's moons on their tracks: the outer ring clockwise, the inner the other way
    for k in range(OUTER["n"]):
        a = math.radians(-90 + 360.0 * k / OUTER["n"] + 18)
        b.peg(cx + r_out * math.cos(a), cy + r_out * math.sin(a), r=9, orange=k in OUTER["oranges"], tag="outer orbit",
              move={"kind": "orbit", "x": cx, "y": cy, "period": OUTER["period"], "clockwise": OUTER["clockwise"]})
    for k in range(INNER["n"]):
        a = math.radians(-90 + 360.0 * k / INNER["n"])
        b.peg(cx + r_in * math.cos(a), cy + r_in * math.sin(a), r=9, orange=k in INNER["oranges"], tag="inner orbit",
              move={"kind": "orbit", "x": cx, "y": cy, "period": INNER["period"], "clockwise": INNER["clockwise"]})
    # the rim: a crown of arc bricks over its top, and moons round its foot just inside the core
    crown = b.arc_bricks(cx, cy, R, CROWN["start"], CROWN["sweep"], n=CROWN["n"], tag="gate crown")
    for k in CROWN["oranges"]:
        crown[k]["canBeOrange"] = True
    for k, deg in enumerate(range(MARKS["first"], MARKS["last"] + 1, MARKS["step"])):
        a = math.radians(deg)
        b.place(cx + R_MARKS * math.cos(a), cy + R_MARKS * math.sin(a), r=8, orange=k in MARKS["oranges"],
                tag="gate rim")
    # the far moon at the heart, ringed
    b.stop(cx, cy, R=36, n=5, r=9, oranges=FAR_ORANGE, green=False, tag="far moon")
    # the crown of phases: a moon on each
    for k, (x, y, _k, _wax) in enumerate(b.features["phases"]["moons"]):
        b.place(x, y, r=10, orange=k in PHASE_ORANGE, green=False, tag="phase")
    for k, (x, y) in enumerate(b.features["crown points"]["stars"]):
        b.place(x, y, r=7, orange=k in POINT_ORANGE, tag="crown point")
    # the ridges: a moon on each standing stone, the crests dotted above them, and the lower crests as brick runs
    for k, (x, y) in enumerate(b.features["stones"]["tops"]):
        b.place(x, y - 9, r=9, orange=k in STONE_ORANGE, green=False, tag="stone")
    for name, (x0, x1), (bx0, bx1) in (("ridge left", (70, 175), (192, 250)), ("ridge right", (625, 730), (548, 596))):
        crest = [(x, y - 11) for (x, y) in b.f(name)]
        b.trace([p for p in crest if x0 <= p[0] <= x1], spacing=34, r=8, orange=set(RIDGE_ORANGE[name]), tag=name)
        lower = [p for p in crest if bx0 - 20 <= p[0] <= bx1 + 20]
        if RIDGE_BRICKS:
            b.bricks_along([(x, y + 2) for (x, y) in lower], length=26, gap=2.5, trim0=8, trim1=8, tag=name + " crest")
        else:
            b.trace(lower, spacing=34, r=8, start=10, tag=name + " crest")
    b.greens_in_reach()
