"""4-3 "Lamps on the Steps" (The Sunlit Steps, Raubahn): light and shadow.

Subject: a stair of lamp glows climbing a dark terrace (our own painting, painters/lampsteps.py). The great sandstone
terrace below the Steps of Nald stands in the night, and a ceremonial stair zigzags up its face in three balustraded
flights; brass lanterns on the newel posts, at each landing and along each flight, throw pools of warm light on the
stone, so the stair reads as a chain of light climbing the dark. A doorway glows between the first two flights, a
bracket lamp burns over the upper landing between crimson banners, and the city's domes stand over the parapet.

Technique (LS): the moons stand in the pools of light. Each newel lantern wears a ring of six moons round its flame
(the pool), and the flame itself stays bare, so the lamps show gold and flicker in their pools (the runtime's small
lights); the handrails between the lanterns are dotted, so the zigzag of the stair is drawn whole; the doorway's arch
is crowned by two bricks open at the keystone with its hanging lantern a moon (never green); the bracket lamp has its
own ring; the two parapets' copings are dotted along the top terrace. The oranges stand in the pools and on the
handrails and copings.

Round 1 (MR 4): a moon on every flame hid the lamps the board is named for (game designer m1), and the ball wedged
between it and its ring (critic M1: 155 stuck-rule fires per 1,728 games, the most shipped). The flames are bare now,
and the rings stand flat-topped (a gap at the bottom, start -60 degrees), so a ball that falls into a pool falls out
of it; lantern D's ring keeps a moon at its top, since a flat-topped ring there left one dead first shot (-40.75).
Stuck-rule fires 52 per 1,728 held-out games (199 per 5,184 fresh); the sweep's stuck share 0.7%.

Built (MR 4 round 1): 78 pieces, 28 candidates, held out 23.6 per 48, pooled 24.05 over 6,912 games (charter 23.5);
tuned at 24.2 on seeds 1-864 and 23.9 on the second block (seeds 20001-23456). The left pools' lower members and the
lower handrail are the holdouts (at most 24% of lost games, all above y 430).
"""
LEVEL = dict(id="base-18", name="Lamps on the Steps", stage=4, number=18, scene="lamp-steps",
             subject="a stair of lamp glows climbing a dark terrace, the pegs in the lamps' pools of light",
             technique="light and shadow (moons in the lanterns' pools, the handrails dotted)")

RING_R, RING_N, RING_START = 34.0, 6, -60.0
RING_START_AT = {"D": -90.0}
# the orange candidates (28), by place: the lanterns' pools, the handrails and the parapets' copings, spread so no
# 200 x 200 square holds more than 10. Tuned on seeds 1-864 and the second block (seeds 20001-23456) to sit between
# 4-2 and 4-4 (round 1: swapped by `ease` toward the pools' harder members once the flames went bare, which eased
# the board by about 2 per 48). Round 2: the pool's (578, 206) gave its candidacy to the east pool's (649, 297),
# reviewed in the round-2 tree (critic: 23.91 on 13,824 fresh games, 71 stuck per 1,728; UX: protan 0.114, F6 0.360
# there); held out 23.7, pooled 23.55.
CANDIDATES = [(140, 206), (359, 175), (248, 206), (213, 243), (230, 272), (301, 175), (330, 158), (359, 209),
              (401, 232), (503, 210), (537, 210), (554, 240), (649, 297), (614, 206), (615, 239), (598, 268),
              (700, 290), (483, 274), (500, 304), (365, 300), (559, 306), (177, 341), (194, 370), (293, 363),
              (401, 387), (177, 399), (232, 405), (657, 426)]


def build(b):
    lamps = b.features["lamps"]
    bx, by = b.features["bracket lamp"]
    # the rings' lamps stay bare: their flames show gold and flicker in each pool (no moon on the flame)
    dx, dy = b.features["door lamp"]
    b.place(dx, dy, r=9, green=False, tag="lamp")
    for name, (x, y) in lamps.items():
        b.stop(x, y, R=RING_R, n=RING_N, r=9, oranges=(), green=True, start_deg=RING_START_AT.get(name, RING_START),
               tag="pool")
    b.stop(bx, by, R=RING_R, n=RING_N, r=9, oranges=(), green=True, start_deg=RING_START, tag="pool")
    # the doorway's crown, open at the keystone
    cx, cy, R, _f, _s = b.circle("door")
    b.arc_bricks(cx, cy, R + 6, 200, 140, n=2, gap_deg=42, t=10, tag="door crown")
    for rail in ("rail 1", "rail 2", "rail 3", "landing B", "landing C"):
        b.trace(rail, spacing=34, r=9, tag="handrail")
    for par in ("parapet L", "parapet R"):
        b.trace(par, spacing=36, r=8, tag="parapet")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=14)
    b.greens_in_reach()
