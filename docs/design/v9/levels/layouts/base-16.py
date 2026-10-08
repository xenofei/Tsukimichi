"""4-1 "The Steps of Thal" (The Sunlit Steps, Raubahn): a trail on a map.

Subject: a night walk through Ul'dah's Steps of Thal on the game's own city map (w1t2/01): in at the Gate of Thal on
the east (the map's own exit arrow, kept), north up the market arcade of the Sapphire Avenue Exchange, west over the
bridge to the head of Pearl Lane's pillared hall, down the spoke into the Gold Court's ring, and up the stair to the
Steps of Nald (the map's own stair arrow, kept, ringed where it leaves the plan). The map's other exit arrows are erased.

Technique: each stop is a ring of six moons (the Gate of Thal, the Exchange, the head of Pearl Lane, the Steps of Nald;
the Gold Court has eight, with its fountain at the heart); the walk between them is dotted with small moons (r 8) over
the gilt route engraved on the map (the scene's overlays, where the runtime's glints run). The city round the walk: the
arcade north to the Nald exit, the spokes west to the Coliseum's ring and south from the Court, the Gladiators' lane,
the round plaza where Pearl Lane's hall ends (the hall itself one steep brick), the Weavers' street and Onyx Lane.
The head of Pearl Lane sits just outside the launcher's swing, so its ring keeps only the four moons that fit.

Candidates (29, the charter's count), on the walk's stops and streets only, chosen with `mfl.py ease` on the tuning
seeds and kept to the spread rule (10 per 200 x 200, one at y 400 or lower on each half): the Gold Court's upper ring
and fountain, the Exchange, the walk in past the Gate's arrow, the steps and the Nald stop's lower moon, the arcade,
the Coliseum's upper ring, the round plaza's north and east moons and the Weavers' street, and one on Onyx Lane
(y 423). The Nald stop's upper moons (left in 0.32-0.36 of `ease`'s lost games) stay blue.
The Hustings' curved street (three blue moons between the hall and the arcade) was dropped: tuning 26.3 -> 26.9.

MR 4 round 1 (critic m1, m2, n1): the stuck rescue fired 86-93 times per 1,728 games (the shipped maximum is 88). Two
walk moons sat 12.6 and 14 from a neighbour, a gap the ball (12) wedges in: the bridge's first moon moves 8 west (start
48, now 20 from the Exchange's west moon) and the walk into the Court is placed at (384, 222), 17.6 from Pearl Lane's
ring and 20 from the Court's. Sweep stuck 10 -> 3 of 681; play 86-88 -> 62-66 per 1,728 on both tuning blocks. The
Onyx Lane orange nearest the cheap gate's line, (423, 425), is blue now (it was left in 26.8-31% of lost games); the
round plaza's (494, 358) takes its place. Low oranges (y 400-430) fall from 31% to 20-23% of the oranges left in lost
games, and lost games holding one from 61% to 45-49% (both tuning blocks); the ratio to their share of the deal stays
about 3, since one per half must sit that low and those places are the hardest on the board. The Gate's upper moon
(592, 285) is blue now and the round plaza's north moon (465, 309) an orange, which eases the opener toward its 26.5
(the round's other changes left it at 26.9 on both tuning blocks, against 26.9 and 26.4 before; critic n1).
Ramp (target 26.5, the MR 4 opener): tuning seeds 1-864 27.8, second block 20001-23456 27.5, held out 27.0;
the stage's 5,184 fresh seeds 27.3 (pooled 27.23). Stuck rescue: 62-71 per 1,728 on the held-out and fresh blocks.
"""

LEVEL = dict(id="base-16", name="The Steps of Thal", stage=4, number=16, scene="thal-night-walk",
             subject="a night walk from the Gate of Thal through the markets to the Steps",
             technique="a trail on a map")

CANDS = {(269, 247), (282, 214), (315, 201), (348, 214), (315, 247),     # the Gold Court and its fountain
         (531, 248), (560, 199), (589, 248),                             # the Exchange
         (563, 302),                                                     # the Gate of Thal
         (465, 164),                                                     # the head of Pearl Lane
         (690, 352), (661, 339), (504, 190), (384, 222),                 # the walk
         (293, 171), (285, 142), (243, 120),                             # the steps and the Steps of Nald
         (514, 108), (527, 137), (539, 167),                             # the arcade
         (128, 228), (157, 212), (186, 228),                             # the Coliseum
         (465, 309), (494, 326), (494, 358), (529, 384), (559, 400),     # the round plaza and the Weavers' street
         (389, 423)}                                                     # Onyx Lane


def build(b):
    # the walk's stops: rings of moons, never green
    (x, y), = b.f("gate of thal")
    b.stop(x, y, R=33, n=6, r=9, oranges=(), tag="gate of thal")
    (x, y), = b.f("exchange")
    b.stop(x, y, R=33, n=6, r=9, oranges=(), tag="exchange")
    (x, y), = b.f("pearl plaza")
    b.stop(x, y, R=33, n=6, r=9, oranges=(), tag="pearl plaza", start_deg=-30)
    cx, cy, R, _, _ = b.circle("gold court")
    b.stop(cx, cy, R=R, n=8, r=9, oranges=(), tag="gold court", start_deg=-90)
    b.place(cx, cy, r=11, green=False, tag="fountain")
    # the walk between them
    b.trace("walk market", spacing=32, r=8, start=38, end_trim=36, tag="walk")
    b.trace("walk bridge", spacing=32, r=8, start=48, end_trim=32, tag="walk")
    b.place(384, 222, r=8, tag="walk")                     # clear of Pearl Lane's ring and the Court's (no wedge)
    b.trace("the steps", spacing=30, r=8, end_trim=4, tag="the steps")
    (x, y), = b.f("steps of nald")
    b.stop(x, y, R=33, n=6, r=9, oranges=(), tag="steps of nald")
    b.trace("walk in", spacing=32, r=8, end_trim=20, tag="walk")
    # the city round the walk
    (x, y), = b.f("coliseum")
    b.stop(x, y, R=33, n=6, r=9, oranges=(), tag="coliseum", green=True)
    (x, y), = b.f("round plaza")
    b.stop(x, y, R=33, n=6, r=9, oranges=(), tag="round plaza", green=True)
    b.trace("arcade north", spacing=32, r=8, start=34, tag="arcade")
    b.trace("west spoke", spacing=34, r=8, start=10, tag="west spoke")
    b.trace("south spoke", spacing=34, r=8, tag="south spoke")
    b.trace("weavers", spacing=34, r=8, start=10, tag="weavers")
    b.trace("onyx lane", spacing=34, r=8, start=12, tag="onyx lane")
    b.trace("gladiators", spacing=34, r=8, tag="gladiators")
    b.bricks_along("pearl hall", length=34, gap=2.5, tag="pearl hall")
    # the candidates: set last, by place
    for p in b.pegs:
        p["canBeOrange"] = (round(p["x"]), round(p["y"])) in CANDS
    b.greens_in_reach()
