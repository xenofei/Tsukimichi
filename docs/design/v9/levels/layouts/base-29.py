"""6-4 "Meteor Shower" (The Silent Stars, Urianger): drift.

Subject: a meteor shower over a mountain lake on a moonless night. The streaks pour out of the radiant, a knot of bright
stars high on the left, every one on a ray from it; behind them the sky is still, the Plough standing in the upper
right, the Milky Way climbing from the lower left with its dark rift and a green airglow low over the range
(painters/meteors.py).

Technique (DR): each painted streak is dotted from its tail, and the falling stars at its head are slide movers that
run out along the streak's own ray toward its head and back, so a moving star never leaves its painted line (MR 3
round 1, critic m4: 3-4's movers left the still crests they rode). The streaks fall at different speeds, each its own
drift group. The radiant is ringed by moons; the faint streaks carry a moon at their heads where they stand in open
sky; the Plough's seven stars are still moons; the range's high peaks wear moons and the slopes into its saddle are
brick runs. Streak 7 runs on to (126, 400), where a moon stops the seven first shots that bounced off the left wall
and fell past the range's left peaks touching nothing.

Candidates (31, charter 31): started from `ease --pick` (the places a player clears most readily, under the spread
rule), then tuned toward the ramp target 20.5 by single swaps on seeds 1-864 and the second tuning block (seeds
20001-23456): the chosen set read 18.9 and 21.0 there (20.6 over both); held out 20.3. No candidate below y 430; the
top holdouts are spread (the left peak (232, 415), the faint head (560, 399), streak 9's tail (635, 295), each about a
fifth of lost games). The Plough's bowl corners (672, 168) and (684, 210) were tried and dropped: each alone carried
a third or more of the lost games. A faint streak head at (560, 190) as a blue peg cost 3 per 48 and was dropped.
"""
import math

LEVEL = dict(id="base-29", name="Meteor Shower", stage=6, number=29, scene="meteor-shower",
             subject="a meteor shower over a mountain lake: the streaks pouring out of the radiant across a still sky, "
                     "the Plough and the range under them",
             technique="drift (the falling stars carried by slide movers along their own painted streaks) with the "
                       "streaks dotted from their tails")

R0 = (190.0, 150.0)             # the radiant (painters/meteors.py)
SLIDE = 36.0                    # how far a falling star runs along its streak
# per streak (as numbered in the painter): (the falling stars' home distances from the radiant, their period, the
# tail's dotted distances)
STREAKS = {
    1: ((404, 444, 484), 7.0, (336, 368)),
    2: ((316, 356), 6.0, (206, 238, 270)),
    3: ((354, 394), 8.0, (256, 288, 320)),
    4: ((240, 278), 6.5, (154, 182, 210)),
    5: ((192, 228), 7.5, (116, 146)),
    6: ((212,), 9.0, (178,)),
    7: ((), 0, (124, 156, 188, 220, 258)),
    8: ((), 0, (116, 148, 182)),
    9: ((), 0, (404, 436, 468, 500)),
}

# the orange candidates (31), by place (a mover by its home)
CANDIDATES = ((232, 415), (487, 408), (472, 190), (500, 150), (694, 423), (208, 174), (218, 141), (286, 215),
              (182, 429), (431, 273), (517, 226), (313, 233), (402, 258), (472, 294), (584, 241), (623, 250),
              (228, 260), (238, 288), (341, 252), (507, 312), (549, 233), (160, 270), (278, 276), (328, 347),
              (152, 301), (635, 295), (110, 260), (144, 332), (610, 387), (560, 399), (126, 400))


def _at(a, d):
    t = math.radians(a)
    return R0[0] + d * math.cos(t), R0[1] + d * math.sin(t)


def _painter():
    import importlib.util
    from pathlib import Path
    spec = importlib.util.spec_from_file_location("painter_meteors", Path(__file__).parents[1] / "painters" / "meteors.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


P = _painter()


def build(b):
    # the radiant, ringed
    b.stop(R0[0], R0[1], R=30, n=5, r=9, oranges=(), green=False, tag="radiant")
    # the streaks: the falling stars at their heads first (slide movers along the ray), then the tails dotted
    for k, (homes, period, tails) in STREAKS.items():
        a = P.STREAKS[k - 1][0]
        ux, uy = math.cos(math.radians(a)), math.sin(math.radians(a))
        dx, dy = round(SLIDE * ux, 1), round(SLIDE * uy, 1)
        for d in homes:
            # homes and runs at a tenth of a unit, as the level file keeps them, so a streak's movers are one group
            x, y = (round(v, 1) for v in _at(a, d))
            b.slide(x, y, dx, dy, period, r=9, tag=f"meteor {k}")
        for d in tails:
            x, y = _at(a, d)
            b.place(x, y, r=7, tag=f"meteor {k} tail")
    # the still sky: the Plough's seven stars
    for (x, y) in P.PLOUGH:
        b.place(x, y, r=8, green=False, tag="plough")
    # the range: a moon on each high peak
    for (x, y) in P.PEAKS:
        b.place(x, y - 13, r=9, green=False, tag="peak")
    # the faint streaks: a moon at each head that stands in the open sky
    for (a, _d0, d1, _k) in P.FAINT:
        x, y = _at(a, d1)
        if y < 440:
            b.place(x, y, r=7, tag="faint meteor")
    # the slopes into the range's saddle as brick runs
    left = [(x, y - 8) for (x, y) in P.CREST_L if 255 <= x <= 360]
    right = [(x, y - 8) for (x, y) in P.CREST_R if 450 <= x <= 522]
    b.bricks_along(left, length=26, gap=2.5, trim0=6, trim1=4, tag="slope left")
    b.bricks_along(right, length=26, gap=2.5, trim0=4, trim1=6, tag="slope right")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True)
    b.greens_in_reach()
