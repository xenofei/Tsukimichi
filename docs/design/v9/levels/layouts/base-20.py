"""4-5 "Ul'dah from the Sands" (The Sunlit Steps, Raubahn): the city's wall outlined in brick and moons; the stage's finale.

Source (sources-plan.md, owner's answers: "4-5 switches from the blurred west strip to an Ul'dah city map"): the Steps
of Nald's own city map, `ui/map/w1t1/01/w1t101_m.tex`, crop [330, 420, 1160, 870], unmirrored (1160 px, sharper than
1-1's map at 2x). The Nald is Ul'dah's western half, the side that faces the sands of Western Thanalan; the other Ul'dah
maps are 4-1's (`w1t2/01`) and 4-4's (`w1t2/02`), and `w1t1/02` is only Ul'dah Landing. The map's red exit arrows are
erased, so the roads out of the Gate of the Sultana, the Gate of Nald and toward the Steps of Thal run on as gilt routes
into the sands. Crop overlap: this crop holds the whole Nald district, so it covers all of 8-4's planned crop
[380, 560, 960, 720] (stage 8, not built); 8-4 must move east of x 1100 or to another map (recorded in the hand-back).

Subject and technique (round 1: game designer M1, critic M2): a city plan has no skyline, so the level is a landmark
partly outlined, the city's great wall as the traveller from the sands meets it. The wall is brick where it stands
proud: a crown of brick on the north-west tower's dome and on the Exchange's dome (each with a moon on the dome), a
rampart down the wall's east sweep toward the Steps of Thal, and one on the west face 18 units out in the sands. Moons
carry the rest: a band 18 units above the wall's crest, the west face past the Gate of the Sultana, the crescent's inner
face, and the roads out of the gates into the sands. The sands read as sands: the field falls from night to desert rose
under a low sand-glow, with moonlit dunes at both lower corners and a sand-haze mist along the foot. To the south-east the
map's own faint ghost of the Steps of Thal is lifted along its streets (scene tone), and the layout traces what shows:
moons down its curved street and its pale inner arc, a run of brick down its east street, a moon on its east block.
Two stars sit low over the open sands (blue), west where the road runs out and east beyond the wall's sweep; the west
one closes the last dead first-shot lane by the left wall. The Exchange's crown is all blue (UX m1: its orange segment
sat under 0.10 for a protanope); the tower's crown carries one instead (its west brick). The orange-able bricks
are the ones that keep 0.10 for a protanope with their neighbours blue (measured with every brick orange, then as dealt).

Difficulty (finale, target 18.5; critic m5: about 0.5-1 per 48 harder than round 1's 18.3 held out / 18.75 pooled, so
the stage's mean falls from MR 3's): the candidates sit on the subject's places across the whole board, none at y 430 or
lower (one on each side below y 400: the west road and the Thal's east street); the board's weight is in the Thal's
streets, the west face and the ramparts. Tuning (seeds 1-864) 16.3, second block (3456 games, seeds 20001-23456) 18.7
(round 1: 18.3 and 18.8; over the 6,048 games about 0.45 harder); held out 18.0 (round 1: 18.3). The line bricks are 12
thick, not 10: at 10 the east rampart's first brick failed `Peg_marks_sit_on_every_brick_inside_its_face` (its mark's
centre off the middle line on the 1-unit clearance grid, as 4-4's did at integration).
"""
import math

LEVEL = dict(id="base-20", name="Ul'dah from the Sands", stage=4, number=20, scene="uldah-from-the-sands",
             subject="the Steps of Nald on Ul'dah's own city map, met from the night sands: its great wall in brick and "
                     "moons, a crown on each dome, the roads out into the desert and the Steps of Thal beyond",
             technique="a landmark partly outlined (the city's wall in brick and moons)")


def build(b):
    # ---- bricks first (so every moon is placed against them)
    # the north-west tower's dome and the Exchange's dome: a crown of brick each (never green)
    for name, tag in (("nw tower", "tower crown"), ("exchange dome", "dome crown")):
        cx, cy, R, a0, sw = b.circle(name)
        b.arc_bricks(cx, cy, R + 14, 200, 140, n=3, tag=tag, green=False)
    # the great wall's east sweep, down from the roofline toward the Steps of Thal: a rampart of brick
    b.bricks_along("east rampart", length=26, t=12, tag="east rampart")
    # the west face's rampart, 18 units out in the sands
    b.bricks_along("west rampart", length=26, t=12, tag="west rampart")
    # the Steps of Thal's east street, lifted from the map's ghost: a run of brick down it
    b.bricks_along("thal column", length=24, t=12, tag="thal column")
    # ---- moons
    # the north-west tower: a moon at each foot of its crown
    cx, cy, R, a0, sw = b.circle("nw tower")
    for a in (160, 20):
        b.place(cx + (R + 16) * math.cos(math.radians(a)), cy + (R + 16) * math.sin(math.radians(a)), r=8,
                green=False, tag="tower dome")
    # the roofline: one band of moons 18 units above the great wall's crest
    b.trace("roofline band", spacing=29, r=7, tag="roofline")
    # the Hall of Flames' two fountains
    for (x, y) in FOUNTAINS:
        b.place(x, y, r=7, tag="fountain")
    # the west face, from the hall down past the Gate of the Sultana: 18 units out in the sands
    b.trace("west face", spacing=30, r=8, offset=18, tag="west wall")
    b.trace("west face south", spacing=30, r=8, offset=18, tag="west wall")
    # the crescent's inner face, toward the Steps of Thal
    b.trace("inner face", spacing=32, r=8, offset=18, tag="inner wall")
    # the roads out of the gates into the sands
    b.trace("west road", spacing=32, r=7, start=35, tag="west road")
    b.trace("north road", spacing=30, r=7, start=16, tag="north road")
    b.trace("east road", spacing=30, r=7, start=10, tag="east road")
    b.trace("guild roof", spacing=32, r=8, offset=-16, tag="guild roof")
    b.place(312, 482, r=8, tag="chapel")
    # stars low over the open sands, west where the road runs out and east beyond the wall's sweep
    for (x, y) in SAND_STARS:
        b.place(x, y, r=7, tag="sand star")
    # the Steps of Thal beyond: moons down its curved street and its pale inner arc, and one on its east block
    b.trace("thal arc", spacing=30, r=7, tag="thal arc")
    b.trace("thal inner arc", spacing=30, r=7, tag="thal inner arc")
    b.place(692, 470, r=7, tag="thal east block")
    # a moon on each dome itself, inside its crown (blue, placed last so the deal stays)
    for name in ("nw tower", "exchange dome"):
        cx, cy, R, a0, sw = b.circle(name)
        b.place(cx, cy, r=8, tag="dome moon")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=8)
    # orange bricks: one each on the tower's crown, the ramparts and the Thal's street (the Exchange's crown stays blue:
    # UX m1)
    brick_tags = [t[1] for t in b.tags if t[0] == "brick"]
    for tag, ks in ORANGE_BRICKS:
        bricks = [br for br, t in zip(b.bricks, brick_tags) if t == tag]
        for k in ks:
            bricks[k]["canBeOrange"] = True
    b.greens_in_reach()


FOUNTAINS = ((322, 250), (346, 229))
SAND_STARS = ((116, 288), (684, 196))
ORANGE_BRICKS = (("tower crown", (0,)), ("east rampart", (2,)), ("west rampart", (1,)), ("thal column", (2,)))
CANDIDATES = (
    (236, 200),                                                             # the tower's crown, its feet
    (375, 195), (432, 182), (460, 178), (489, 178),                         # the roofline
    (322, 250), (346, 229),                                                 # the fountains
    (266, 256), (216, 368), (213, 400),                                     # the west face
    (299, 352), (329, 363), (386, 389), (418, 388), (441, 366), (502, 365), (625, 308),  # the inner face
    (111, 404), (143, 401), (175, 399),                                     # the west road
    (546, 133), (685, 276),                                                 # the north and east roads
    (601, 339), (614, 366),                                                 # the Thal's curved street
    (540, 373),                                                             # the Thal's inner arc
)
