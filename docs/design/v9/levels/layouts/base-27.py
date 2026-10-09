"""6-2 "The Wheel of the Twelve" (The Silent Stars, Urianger): a constellation (ch-03 "Thirty-Five Moons").

Subject: the Twelve's signs as one great constellation: the old star atlas's zodiac wheel of Eorzea turning round the
night sky, its twelve signs set in the gilt band in the calendar's order, Halone's spear at the top left and clockwise
to Althyk's hourglass, each four stars over the atlas's engraving of its emblem (our own painting, painters/twelve.py).

Technique: a moon sits on every star of every sign (48, four to a sign; the brightest of each larger), so the ring of
twelve small figures is the wheel, and the painting's dotted gilt line joins each sign to the next. The pole star at the
hub is a large moon (r 12: a straight-down first shot no longer slips past it) with a moon (r 9 since the stage
retune, below) at the tip of each of its rosette's four long rays: a cross, not a ring, so the hub does not echo 6-1's ring of moons round its sun (r 8 read
22.0 per 48 on the tuning seeds, r 10 23.8). The dividers of the wheel's lower half and its two flanks, where the band's
dividing lines meet its inner rule, carry a small moon each; the top's three are left to the signs (all twelve as pegs
cost about 4 per 48 on the tuning seeds: they wall the hub's sky off from the upper signs).

Candidates: exactly 35 (ch-03's carrier at the pre-flight's maximum): the signs' stars, the hub's four rays, a divider
on the right flank and one on the lower left, kept to the spread rule (10 per 200 x 200, which the wheel's top and each
flank reach; at least one candidate below y 400 each side). The three lowest signs (Byregot, Rhalgr, Azeyma) give none:
Byregot's top star (454, 449) was left in up to 24% of lost games. Oschon's (526, 440) and (582, 429) are the
candidates in the bucket's approach; lost games leaving an orange at y 430 or lower are 14%.

Ramp (target 23.5): 23.8 on the tuning seeds (1-864), 23.9 on the second tuning block (seeds 20001-23456), 23.5 held
out; ch-03 at 35 oranges wins 28 of 48. Top holdouts: (175, 409) 21% and (582, 429) 20% of lost games.

Stage retune (MR 6 integration): with the hub's rays at r 10 the stage's pooled games read 24.62 per 48 against 6-1's
24.7, a step of 0.08 (the stage needs 0.5 after one standard error). The rays went to r 9 (the halfway point of the r 8
and r 10 tuning figures above): held out 24.2, pooled 23.52 over 6912 games, steps 1.18 to 6-1 and 0.92 to 6-3;
ch-03 at 35 oranges wins 29 of 48.

Round 1 fix (game designer M2: the thinnest board in the game, sweep mean 6.4, the ball falling through the wheel's
empty middle; critic n1): the wheel's inner ticked rim carries six bricks at its four diagonals (RIM_SPANS below), so
balls crossing the wheel meet its rim. Pegs on the rim's lower half (a bead per sign) were tried: sweep 6.8 and 3.4 per
48 harder on the tuning seeds. Bricks running over the rim's top are level decks, and longer lower runs cradle against
the divider moons, so the lower diagonals take one brick each. Sweep mean 7.7, median 7. The bricks eased the board
(24.4 on seeds 1-864, 24.1 on 20001-23456), so the rays' moons went to r 8.5: 22.7 and 23.5 on the tuning blocks, held
out 23.0, pooled 23.72 over 6912 games (23.52 before). The top ray's moon moved to x 401 (n1: a shot at exactly 0
degrees bounced on it and the hub for 17 s). Stuck fires 54 per 1728 held-out games (23 before; 88 is the shipped
maximum); (526, 440) is left in 22% of lost games. ch-03 at 35 oranges wins 20 of 48. Ace unchanged (270 of 576).
"""
import math

LEVEL = dict(id="base-27", name="The Wheel of the Twelve", stage=6, number=27, scene="wheel-of-the-twelve",
             subject="the Twelve's signs as one great constellation: the star atlas's zodiac wheel of Eorzea",
             technique="a constellation")

HUB_RAY = 48
RAY_R = 8.5              # the rays' moons (round 1 fix: r 9 to 8.5 to hold the ramp once the rim's bricks went in)
TOP_RAY_DX = 1           # the top ray's moon a unit right of the hub's line (critic n1: a shot at exactly 0 degrees)
DIVIDER_Y = 270
# The wheel's inner ticked rim (round 1 fix, game designer M2): brick runs on the ticks inside the inner rule, on the
# ellipse of half-axes A - 58 - TICK and B - 52 - TICK about the hub (A 258, B 146: painters/twelve.py), at the wheel's
# four diagonals: (from, to, trim at from, trim at to[, brick length]) in the painter's rim angles (degrees, 0 along +x,
# turning toward +y). Each run stops 28 units short of a divider moon (no cradle, no wedge); the upper two run on toward
# the top, where no divider moon stands, until the rim flattens to a deck (under 10 degrees).
WHEEL_A, WHEEL_B, IN_A, IN_B = 258.0, 146.0, 58.0, 52.0
TICK = 10.0
RIM_SPANS = [(-72.0, -37.6, 0.0, 28.0, 28.0),          # upper right (two bricks)
             (37.6, 65.6, 28.0, 28.0),                  # lower right (one)
             (114.4, 142.4, 28.0, 28.0),                # lower left (one)
             (217.6, 252.0, 28.0, 0.0, 28.0)]           # upper left (two)
RIM_BRICK = 24.0

# the orange candidates (35), by sign (see the docstring)
CANDIDATES = [(353, 174), (331, 199), (359, 216),                             # Halone
              (462, 169), (438, 190), (449, 220),                             # Menphina
              (584, 204), (552, 200), (531, 226), (560, 247),                 # Thaliak
              (625, 287), (659, 287), (642, 314),                             # Nymeia
              (636, 374), (616, 398), (670, 368),                             # Llymlaen
              (582, 429), (526, 440),                                         # Oschon
              (242, 395), (558, 281),                                         # two dividers of the rim
              (141, 368), (175, 376), (175, 409),                             # Nald'thal
              (134, 263), (182, 263), (158, 284), (158, 316),                 # Nophica
              (229, 198), (263, 198), (229, 247), (263, 247),                 # Althyk
              (401, 290), (352, 338), (448, 338), (400, 386)]                 # the hub's rays


def build(b):
    stars = b.features["sign stars"]
    for name, pts in stars.items():
        for k, (x, y, m) in enumerate(pts):
            b.place(x, y, r=9 if m == 1 else 8, tag="star")
    hx, hy = b.features["hub"]
    b.place(hx, hy, r=12, green=False, tag="pole star")
    for (dx, dy) in ((TOP_RAY_DX, -HUB_RAY), (HUB_RAY, 0), (0, HUB_RAY), (-HUB_RAY, 0)):  # the rosette's four long rays
        b.place(hx + dx, hy + dy, r=RAY_R, tag="hub ray")
    for (x, y) in b.features["dividers"]:
        if y > DIVIDER_Y:                # the rim's lower half and its two flanks: the top is left to the signs
            b.place(x, y, r=7, tag="divider")
    ra, rb = WHEEL_A - IN_A - TICK, WHEEL_B - IN_B - TICK
    for (a0, a1, trim0, trim1, *ln) in RIM_SPANS:          # the inner rim's ticked arcs at the wheel's four diagonals
        pts = [(hx + ra * math.cos(math.radians(a)), hy + rb * math.sin(math.radians(a)))
               for a in [a0 + (a1 - a0) * k / 24 for k in range(25)]]
        b.bricks_along(pts, length=ln[0] if ln else RIM_BRICK, gap=2.5, trim0=trim0, trim1=trim1, tag="rim")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=6)
    b.greens_in_reach()
