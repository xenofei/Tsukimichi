"""6-2 "The Wheel of the Twelve" (The Silent Stars, Urianger): a constellation (ch-03 "Thirty-Five Moons").

Subject: the Twelve's signs as one great constellation: the old star atlas's zodiac wheel of Eorzea turning round the
night sky, its twelve signs set in the gilt band in the calendar's order, Halone's spear at the top left and clockwise
to Althyk's hourglass, each four stars over the atlas's engraving of its emblem (our own painting, painters/twelve.py).

Technique: a moon sits on every star of every sign (48, four to a sign; the brightest of each larger), so the ring of
twelve small figures is the wheel. The pole star at the hub is a large moon (r 12: a straight-down first shot no longer
slips past it) in a ring of six. The seven dividers of the wheel's lower half, where the band's dividing lines meet its
inner rule, carry a small moon each; the top's five are left to the signs (all twelve as pegs cost about 4 per 48 on
the tuning seeds: they wall the hub's sky off from the upper signs).

Candidates: exactly 35 (ch-03's carrier at the pre-flight's maximum): the signs' stars, the hub's upper ring and its
foot, and the two lower-rim dividers either side, picked by `ease` among the most readily cleared under the spread rule
(10 per 200 x 200, which the wheel's top and each flank reach). The three lowest signs (Byregot, Rhalgr, Azeyma) give
none: Byregot's top star (454, 449) was left in up to 24% of lost games; the dividers (242, 395) and (558, 395) took
its place and Azeyma's, and lost games leaving an orange at y 430 or lower fell from about 30% to 10%. Oschon's
(526, 440) is the one candidate in the bucket's approach.

Ramp (target 23.5): 23.9 on the second tuning block (seeds 20001-23456), 23.2 held out; ch-03 at 35 oranges wins 18
of 48.
"""
LEVEL = dict(id="base-27", name="The Wheel of the Twelve", stage=6, number=27, scene="wheel-of-the-twelve",
             subject="the Twelve's signs as one great constellation: the star atlas's zodiac wheel of Eorzea",
             technique="a constellation")

# the orange candidates (35), by sign (see the docstring)
CANDIDATES = [(353, 174), (331, 199), (359, 216),                             # Halone
              (462, 169), (438, 190), (449, 220), (481, 222),                 # Menphina
              (584, 204), (552, 200), (531, 226), (560, 247),                 # Thaliak
              (625, 287), (659, 287), (642, 314),                             # Nymeia
              (636, 374), (616, 398),                                         # Llymlaen
              (582, 429), (526, 440),                                         # Oschon
              (242, 395), (558, 395),                                         # two dividers of the lower rim
              (141, 368), (175, 376), (141, 402),                             # Nald'thal
              (134, 263), (182, 263), (158, 284), (158, 316),                 # Nophica
              (229, 198), (263, 198), (229, 247), (263, 247),                 # Althyk
              (400, 300), (367, 319), (433, 319), (400, 376)]                 # the hub


def build(b):
    stars = b.features["sign stars"]
    for name, pts in stars.items():
        for k, (x, y, m) in enumerate(pts):
            b.place(x, y, r=9 if m == 1 else 8, tag="star")
    hx, hy = b.features["hub"]
    b.place(hx, hy, r=12, green=False, tag="pole star")
    b.stop(hx, hy, R=38, n=6, r=8, oranges=(), green=True, tag="hub")
    for (x, y) in b.features["dividers"]:
        if y > 337:                     # the lower half of the rim only: the top is left to the signs
            b.place(x, y, r=7, tag="divider")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=6)
    b.greens_in_reach()
