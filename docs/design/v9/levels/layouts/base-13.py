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
blue moon in the high cloud (402, 222), a blue moon on a mid spire (566, 222) and one on the cloud sea under the slope
(276, 256) were added.

Candidates (30, the charter's count): the summits, the billow hearts, the bridge lamps, the gate spire, two spire tips,
the rose window, the flanks, two moons in the cloud beyond the cathedral, and only five low (y 430 or lower). The pilot's
two oranges on the piers and the third on the cathedral's foot moved up to the flanks and the cloud beyond: low
oranges were 41% of the oranges left in lost games against 27% of the candidates (the cheap-difficulty gate's 1.5
times). The east spire tip (624, 134) stays blue: as a candidate it was left in 36% of lost games. Tuned on seeds
1-864 and 20001-23456 (23.9 and 23.8 per 48) toward the charter's 24.
"""

LEVEL = dict(id="base-13", name="The Holy See", stage=3, number=13, scene="holy-see",
             subject="Ishgard's walls over the cloud sea: bricks on the crests, moons on the spires",
             technique="terrain emphasis (brick runs on crests, moons on summits)")

SUMMIT_ORANGE = {(256, 150), (341, 200)}
SPIRE_ORANGE = {(514, 100), (484, 150)}                    # the two highest tips are left to the art
FLANK_ORANGE = {(462, 270), (642, 176), (654, 212), (678, 320)}
BEYOND_ORANGE = {(702, 228), (714, 292)}
TOWN_ORANGE = {(150, 448), (192, 470), (236, 456)}


def deck_y(x):
    return 441.0 - (x - 312.0) * 0.346          # the bridge deck's top edge in the scene


def build(b):
    # ---- the mountain range: the crest in two monotone runs of brick from the high summit, a moon over each summit
    ridge = b.f("ridge")
    top = ridge.index((162, 149))
    b.bricks_along(ridge[:top + 1], length=28, gap=2.5, t=12, green=False, tag="ridge")
    b.bricks_along(ridge[top:], length=28, gap=2.5, t=12, green=False, tag="ridge")
    for (x, y) in b.f("summits"):
        b.place(x, y, orange=(x, y) in SUMMIT_ORANGE, green=False, tag="moon over a summit")
    for (x, y) in ((228, 118), (292, 132), (330, 166), (402, 222)):
        b.place(x, y, orange=(x, y) == (292, 132), tag="high cloud")
    # ---- the sea of clouds: three billow tops as gentle humps of brick, a heart in the trough under each, a dotted
    # row lower and one moon under the slope
    for name in ("billow west", "billow middle", "billow east"):
        cx, cy, R, a0, sw = b.circle(name)
        b.arc(cx, cy, R, a0, sw, t=10, tag="billow")
    for (x, y) in b.f("billow hearts"):
        b.place(x, y, orange=True, tag="billow heart")
    for (x, y) in ((100, 392), (140, 404), (190, 398), (232, 394), (276, 256)):
        b.place(x, y, orange=(x, y) == (190, 398), tag="cloud sea")
    # ---- the bridge: three arches as bricks in the painted arches (never green), its lamps dotted along the deck
    for x in (374, 428, 482):
        b.arc(x, deck_y(x) + 37, 20, 220, 100, t=8, green=False, tag="arch")
    for x in (300, 340, 380, 420):
        b.place(x, deck_y(x) - 18, orange=True, tag="bridge lamp")
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
        p["canBeOrange"] = (k % 2 == 0)
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
