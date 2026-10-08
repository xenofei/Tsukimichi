"""4-5 "Ul'dah from the Sands" (The Sunlit Steps, Raubahn): a silhouette band; the stage's finale.

Source (sources-plan.md, owner's answers: "4-5 switches from the blurred west strip to an Ul'dah city map"): the Steps
of Nald's own city map, `ui/map/w1t1/01/w1t101_m.tex`, crop [330, 420, 1160, 870], unmirrored (1160 px, sharper than
1-1's map at 2x). The Nald is Ul'dah's western half, the side that faces the sands of Western Thanalan; the other Ul'dah
maps are 4-1's (`w1t2/01`) and 4-4's (`w1t2/02`), and `w1t1/02` is only Ul'dah Landing. The map's red exit arrows are
erased, so the roads out of the Gate of the Sultana, the Gate of Nald and toward the Steps of Thal run on as gilt routes
into the sands. Crop overlap: this crop holds the whole Nald district, so it covers all of 8-4's planned crop
[380, 560, 960, 720] (stage 8, not built); 8-4 must move east of x 1100 or to another map (recorded in the hand-back).

Subject and technique: seen from the sands, the city is one crescent of wall. A band of moons runs round its whole
silhouette: 18 units above the great arc's outer wall (the roofline), down its west face past the Gate of the Sultana,
and along the crescent's inner face toward the Steps of Thal; the north-west tower's dome wears a crown of moons and the
Exchange's dome a crown of brick (never green), each with a moon on the dome itself. The roads out of the gates are
dotted into the sands, and the stairs down to the Thal drop from the inner face. Beyond, the map's faint ghost of the
Steps of Thal (lifted a little by a broad tone) carries its ring road as an oval of small lanterns: dots on a ghost, not
4-4's rings and spokes of brick.

Difficulty (finale, target 18.5): the candidates sit on the subject's places across the whole board, none at y 430 or
lower; the top band clears readily (ease: under 1% left), so the board's weight is in the west face, the west road's
far end, the inner face and the Thal's ring. Tuning (seeds 1-864) 18.3, second block (3456 games, seeds 20001-23456)
18.8; held out 18.3.
"""
import math

LEVEL = dict(id="base-20", name="Ul'dah from the Sands", stage=4, number=20, scene="uldah-from-the-sands",
             subject="the Steps of Nald on Ul'dah's own city map: its walls' skyline from the sands, a moon on each dome",
             technique="a silhouette band (the city's walls traced)")


def build(b):
    # the north-west tower's dome: a crown of moons round it
    cx, cy, R, a0, sw = b.circle("nw tower")
    for k in range(6):
        a = math.radians(130 + 220 * k / 5)
        b.place(cx + (R + 16) * math.cos(a), cy + (R + 16) * math.sin(a), r=8, green=False, tag="tower dome")
    # the roofline: one band of moons 18 units above the great arc's outer wall
    b.trace("roofline band", spacing=29, r=7, tag="roofline")
    # the Exchange's dome: a crown of brick
    cx, cy, R, a0, sw = b.circle("exchange dome")
    b.arc_bricks(cx, cy, R + 14, 200, 140, n=3, tag="dome crown", green=False)
    # the Hall of Flames' two fountains
    for (x, y) in FOUNTAINS:
        b.place(x, y, r=7, tag="fountain")
    # the west face, from the hall down past the Gate of the Sultana: 18 units out in the sands
    b.trace("west face", spacing=30, r=8, offset=18, tag="west wall")
    # the crescent's inner face, toward the Steps of Thal
    b.trace("inner face", spacing=32, r=8, offset=18, tag="inner wall")
    # the roads out of the gates into the sands
    b.trace("west road", spacing=32, r=7, start=24, tag="west road")
    b.trace("north road", spacing=30, r=7, start=16, tag="north road")
    b.trace("east road", spacing=30, r=7, start=10, tag="east road")
    b.trace("guild roof", spacing=32, r=8, offset=-16, tag="guild roof")
    b.place(312, 482, r=8, tag="chapel")
    # the stairs down to the Steps of Thal (the map's own passages)
    b.trace("thal stair west", spacing=30, r=7, tag="thal stair")
    b.trace("thal stair east", spacing=30, r=7, tag="thal stair")
    # the Steps of Thal beyond, faint under the haze: lanterns on its ring road
    cx, cy, rx, ry = b.features["ghost ring"]["ellipse"]
    ring = [(cx + rx * math.cos(math.radians(a)), cy + ry * math.sin(math.radians(a))) for a in range(100, 461, 4)]
    b.trace(ring, spacing=31, r=7, tag="thal ring")
    # a moon on each dome itself, inside its crown (blue, placed last so the deal stays)
    for name in ("nw tower", "exchange dome"):
        cx, cy, R, a0, sw = b.circle(name)
        b.place(cx, cy, r=8, tag="dome moon")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=8)
    b.greens_in_reach()


FOUNTAINS = ((322, 250), (346, 229))
CANDIDATES = (
    (233, 190), (242, 158), (270, 142), (301, 150),                         # the tower's dome
    (373, 195), (430, 182), (487, 178), (599, 204),                          # the roofline
    (527, 233),                                                             # the Exchange's crown
    (322, 250), (346, 229),                                                 # the fountains
    (266, 256), (254, 304), (216, 366), (213, 396), (211, 425),             # the west face
    (299, 352), (329, 363), (386, 389), (441, 366), (502, 365), (625, 308),  # the inner face
    (122, 403), (154, 400), (184, 398),                                         # the west road
    (546, 133), (685, 276),                                                 # the north and east roads
    (567, 391), (598, 339), (677, 363), (688, 423),                         # the Thal's ring
    (430, 420),                                                             # the stair
)
