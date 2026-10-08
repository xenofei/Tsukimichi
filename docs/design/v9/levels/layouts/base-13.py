"""3-3 "The Holy See" (The Night Skyway, Cid): terrain emphasis.

Subject: Ishgard's walls over the sea of clouds, in the game's own Coerthas loading-screen painting (read from the
install, never shipped), mirrored so its light comes from the upper left: the approved pilot base-p2's crop and its
runtime recipe holy-see, brought to the shipped levels' rules.

Technique (the pilot's): the layout rides the terrain. Moonstone bricks lie along the mountain range's crest, in two
runs that meet at the high summit (so no brick lies level across it), with a moon over each summit 17 units clear of
the crest; three scalloped bricks follow the billows of the cloud sea, a heart in the trough under each; three arch
bricks sit in the bridge's painted arches with its lamps dotted along the deck above; the cathedral is drawn by moons on
its spire tips, its two flanks and a ring round its rose window, and the fog below holds the piers and the lower town.
The crest, the arches, the spire tips and the rose window are the subject's own shapes: never green.

What changed from the pilot (the shipped rules): the crest's bricks are 28 long, not 24 (24 left 16-unit notches
between every other brick); the summit moons, the bridge lamps and one flank moon stand clear of the wedge band; the
fog's lowest row of piers lost a peg and the lower town rose 4 units, so 3 pieces reach into the bucket's lane; a fourth
blue moon in the high cloud (402, 222; removed at stage integration, below), a blue moon on a mid spire (566, 222) and one on the cloud sea under the slope
(276, 256) were added.

Candidates (30, the charter's count): the summits, the billow hearts, the bridge lamps, the gate spire, two spire tips,
the rose window, the flanks, two moons in the cloud beyond the cathedral, and only five low (y 430 or lower). The pilot's
two oranges on the piers and the third on the cathedral's foot moved up to the flanks and the cloud beyond: low
oranges were 41% of the oranges left in lost games against 27% of the candidates (the cheap-difficulty gate's 1.5
times). The east spire tip (624, 134) stays blue: as a candidate it was left in 36% of lost games. Tuned on seeds
1-864 and 20001-23456 (23.9 and 23.8 per 48) toward the charter's 24.

Stage integration (mr3): the bridge's arches went from 8 to 10 thick (the runtime's mark test, below), which made the
board 0.7 per 48 harder and left the step to 3-4 unresolved (0.73 +- 0.41 pooled); the fourth high-cloud blue moon
(402, 222) came out to give it back (tuning 22.7, second block 23.8; pooled 23.73, step to 3-4 1.03).

MR 3 round 1 (game designer m3 and m4, critic m1 and m2):
- The two candidates in the right wall strip, (702, 228) and (714, 292), each decided about a quarter of lost games and
  traced nothing; their candidates moved onto the cathedral (the left flank's (472, 232) and the mid spire (566, 222)),
  and the whole set was rebalanced on the tuning seeds: the right flank's (668, 248), the rose window's lower-right moon
  (577, 364) and the cloud sea's (140, 404) and (232, 394) are candidates; the lower town's (150, 448) and (192, 470) and
  the east bridge lamp are not. Only three candidates are low (y 430 or lower): low oranges are 9% of those left in lost
  games against 10% of the candidates (the gate's limit is 1.5 times; it read 1.40). No holdout passes 18%.
- The stuck rule: the east billow's crown sat under the ridge's low end, and a slow ball came to rest on its flat apex
  (`mfl.py stuck`: four of the fourteen stuck first shots). The crown is opened at its keystone (a slot the ball passes,
  past the notch band), and the ridge's last brick, which hung over it and left a 23-unit cradle against the left flank's
  moon, is gone. The sweep's stuck share fell from 2.1% to 1.9% and the play's stuck fires from 120 to 97 per 1728 games.
- A blue moon in the high cloud over the west billow (140, 240) gives back the difficulty the wall candidates carried:
  tuning 23.8, second block 23.9; held out 23.8.
"""

import math

LEVEL = dict(id="base-13", name="The Holy See", stage=3, number=13, scene="holy-see",
             subject="Ishgard's walls over the cloud sea: bricks on the crests, moons on the spires",
             technique="terrain emphasis (brick runs on crests, moons on summits)")

SUMMIT_ORANGE = {(256, 150), (341, 200)}
SPIRE_ORANGE = {(514, 100), (484, 150), (566, 222)}        # the two highest tips are left to the art
FLANK_ORANGE = {(462, 270), (472, 232), (642, 176), (654, 212), (668, 248), (678, 320)}
BEYOND_ORANGE = set()                     # (MR 3 round 1: the two wall-strip candidates moved onto the cathedral)
TOWN_ORANGE = set()
RIDGE_ORANGE = set()                     # bricks of the east run, from the high summit


def deck_y(x):
    return 441.0 - (x - 312.0) * 0.346          # the bridge deck's top edge in the scene


def build(b):
    # ---- the mountain range: the crest in two monotone runs of brick from the high summit, a moon over each summit
    ridge = b.f("ridge")
    top = ridge.index((162, 149))
    b.bricks_along(ridge[:top + 1], length=28, gap=2.5, t=12, green=False, tag="ridge")
    for k, br in enumerate(b.bricks_along(ridge[top:-1], length=28, gap=2.5, t=12, green=False, tag="ridge")):
        br["canBeOrange"] = k in RIDGE_ORANGE
    for (x, y) in b.f("summits"):
        b.place(x, y, orange=(x, y) in SUMMIT_ORANGE, green=False, tag="moon over a summit")
    for (x, y) in ((228, 118), (292, 132), (330, 166), (140, 240)):
        b.place(x, y, orange=(x, y) == (292, 132), tag="high cloud")
    # ---- the sea of clouds: three billow tops as gentle humps of brick, a heart in the trough under each, a dotted
    # row lower and one moon under the slope
    for name in ("billow west", "billow middle", "billow east"):
        cx, cy, R, a0, sw = b.circle(name)
        if name == "billow east":
            # opened at its keystone, an 18-unit slot between the bricks' rounded ends (MR 3 round 1, m3: a slow ball came to rest on the crown's flat
            # apex under the ridge's low end, the board's most frequent stuck shot)
            b.arc_bricks(cx, cy, R, a0, sw, n=2, gap_deg=math.degrees(28.5 / R), t=10, tag="billow")
        else:
            b.arc(cx, cy, R, a0, sw, t=10, tag="billow")
    for (x, y) in b.f("billow hearts"):
        b.place(x, y, orange=True, tag="billow heart")
    for (x, y) in ((100, 392), (140, 404), (190, 398), (232, 394), (276, 256)):
        b.place(x, y, orange=(x, y) in ((140, 404), (190, 398), (232, 394)), tag="cloud sea")
    # ---- the bridge: three arches as bricks in the painted arches (never green), its lamps dotted along the deck
    # (10 thick, as every shipped brick: at 8 the runtime's one-unit clearance field put the middle arch's mark off its
    # middle line, MoonfallRuntimeArtTests.Peg_marks_sit_on_every_brick_inside_its_face; stage integration, mr3)
    for x in (374, 428, 482):
        b.arc(x, deck_y(x) + 37, 20, 220, 100, t=10, green=False, tag="arch")
    for x in (300, 340, 380, 420):
        b.place(x, deck_y(x) - 18, orange=x != 420, tag="bridge lamp")
    b.place(250, 362, orange=True, green=False, tag="gate spire")
    # ---- the cathedral: moons on the spire tips, its two flanks, a ring round the rose window
    for (x, y) in b.f("spire tips"):
        b.place(x, y, orange=(x, y) in SPIRE_ORANGE, green=False, tag="spire tip")
    for (x, y) in b.f("left flank"):
        b.place(x, y, orange=(x, y) in FLANK_ORANGE, tag="left flank")
    for (x, y) in b.f("right flank"):
        b.place(x, y, orange=(x, y) in FLANK_ORANGE, tag="right flank")
    cx, cy, R, _, _ = b.circle("rose window")
    for k, p in enumerate(b.ring(cx, cy, R, 8, start_deg=-90, tag="rose window")):
        p["canBeOrange"] = (k % 2 == 0) or k == 3
        p["canBeGreen"] = False
    b.place(cx, cy, r=12, orange=True, green=False, tag="rose window heart")
    for (x, y) in ((700, 186), (702, 228), (714, 292), (700, 384)):
        b.place(x, y, orange=(x, y) in BEYOND_ORANGE, tag="cloud beyond")
    # ---- below: a far peak at the right, the cathedral's foot, the piers in the fog and the lower town
    for (x, y) in ((688, 452), (700, 420)):
        b.place(x, y, tag="far peak")
    for (x, y) in b.f("cathedral foot"):
        b.place(x, y, orange=x in (480, 552), tag="cathedral foot")
    for (x, y) in b.f("piers"):
        b.place(x, y, tag="pier in the fog")
    for (x, y) in b.f("lower town"):
        b.place(x, y, orange=(x, y) in TOWN_ORANGE, tag="lower town")
    b.greens_in_reach()
