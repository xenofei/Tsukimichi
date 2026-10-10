"""3-3 "The Holy See" (The Night Skyway, Cid): terrain emphasis.

Subject: Ishgard's walls over the sea of clouds, in the game's own Coerthas loading-screen painting (read from the
install, never shipped), mirrored so its light comes from the upper left: the approved pilot base-p2's crop and its
runtime recipe holy-see.

Technique (the pilot's): the layout rides the terrain. Moonstone bricks lie along the mountain range's crest in two
runs that meet at the high summit (a slot past the notch limit between them), with a moon over each summit; a dotted
snow line follows the crest lower down the mountain's face, and a row of moons marks the mountain's foot in the cloud.
The billows of the cloud sea are crowns of brick, a moon in the shadow under each and a heart in the troughs between
them, a dotted row of cloud below. The bridge's deck is a run of brick with its lamps dotted above and a moon in two
of its painted arches. The cathedral is drawn by brick down both its flanks, moons on its spire tips and inner
pinnacles, and its rose window as an arch of brick over three moons round a heart; the fog below holds the piers, the
cathedral's foot and the lower town. The crest, the deck, the flanks, the spire tips, the arches and the rose window
are the subject's own shapes: never green.

Peggle refit (owner's rules of 9 October 2026): densified from the shipped layout (88 pieces, 30 candidates), which
traced the subject better than the three generated variants (they scattered pieces over the cathedral and the
mountain); pegs r 10 and bricks t 20, 111 pieces (28 bricks, 25%: the subject has few more steep or crested lines
that take brick without notches against its neighbours). Candidates by exclusion (`b.candidates`), 68 of 111: the
fog's piers and the lower town stay blue (the greens' pool), and so do the crest's and the deck's bricks, the boards'
easiest targets (blue, they took the held-out figure from 6.9 to 5.6 per 48); blue moons in the open sky by the
launcher and the high spires harden it. The hardest places are the shadowed ones: under the billows, under the
crest, under the deck and in the rose window. Tuned (`mfl.py tune`) on seeds 1-864 to 4.0 per 48; held out 4.9 (band
3.04-5.24, stage mean 4.14).
"""

import math

LEVEL = dict(id="base-13", name="The Holy See", stage=3, number=13, scene="holy-see",
             subject="Ishgard's walls over the cloud sea: bricks on the crests, moons on the spires",
             technique="terrain emphasis (brick runs on crests, moons on summits)")


def deck_y(x):
    return 441.0 - (x - 312.0) * 0.346          # the bridge deck's top edge in the scene


def below(pts, d):
    """The polyline `pts` moved `d` units straight down (a line down the mountain's face, parallel to its crest)."""
    return [(x, y + d) for (x, y) in pts]


# never orange: the fog and the lower town (green-able, the greens' pool), the crest's and the deck's bricks (the
# easiest targets on the board) and the moons in the open sky
EXCLUDE_TAGS = ("pier in the fog", "lower town", "ridge", "deck", "open sky")
SUMMITS = ((162, 110), (256, 144), (341, 194))     # 16 or more above the crest's brick
HEARTS = ((100, 352), (209, 340), (330, 340))
EAST_BILLOW_CUT = 10.0
RIDGE_SLOT = 4.0                 # opens the summit's notch past the limit
PINNACLES = ((548, 140), (592, 172), (530, 192), (612, 228), (520, 250), (600, 268))   # the cathedral's inner spires
LEFT_FLANK_TRIM = 40.0           # the flank ends clear of the east billow


def build(b):
    # ---- the mountain range: the crest in two monotone runs of brick from the high summit (a slot past the notch
    # limit at the summit itself), a moon over each summit
    ridge = b.f("ridge")
    top = ridge.index((162, 149))
    b.bricks_along(ridge[:top + 1], length=30, gap=2.5, green=False, tag="ridge")
    b.bricks_along(ridge[top:-1], length=32, gap=2.5, trim0=RIDGE_SLOT, green=False, tag="ridge")
    for (x, y) in SUMMITS:
        b.place(x, y, green=False, tag="moon over a summit")
    # the snow line down the mountain's face, parallel to the crest (it stops short of the middle billow)
    b.trace(below(ridge[1:9], 48), spacing=36, tag="snow line")
    for (x, y) in ((228, 104), (300, 124), (110, 120), (170, 66), (250, 60)):
        b.place(x, y, tag="high cloud")
    for (x, y) in ((400, 214), (436, 214), (330, 160), (344, 278), (120, 70), (520, 60), (580, 66), (660, 128)):
        b.place(x, y, tag="open sky")
    for (x, y) in ((100, 262), (150, 262), (200, 270)):
        b.place(x, y, tag="mountain's foot")
    # ---- the sea of clouds: three billow crowns of brick, a heart in the trough under each, a dotted row lower
    for name in ("billow west", "billow middle", "billow east"):
        cx, cy, R, a0, sw = b.circle(name)
        if name == "billow east":
            sw -= EAST_BILLOW_CUT        # its east end stops short of the cathedral's left flank (a notch)
        b.arc(cx, cy, R, a0, sw, tag="billow")
    for (x, y) in HEARTS:
        b.place(x, y, tag="billow heart")
    for (x, y) in ((150, 338), (268, 328), (392, 344)):
        b.place(x, y, tag="under a billow")
    b.trace([(104, 408), (224, 402)], spacing=36, tag="cloud sea")
    # ---- the bridge: its deck in brick, the lamps above, a moon in each painted arch (never green)
    b.bricks_along([(318, deck_y(318) + 8), (470, deck_y(470) + 8)], length=34, gap=2.5, green=False, tag="deck")
    for x in (304, 344, 384, 424):
        b.place(x, deck_y(x) - 32, tag="bridge lamp")
    for x in (374, 428):
        b.place(x, deck_y(x) + 46, green=False, tag="arch")
    b.place(250, 362, green=False, tag="gate spire")
    b.place(280, 440, tag="gate tower")
    # ---- the cathedral: brick down both flanks, moons on the spire tips, the rose window's arch in brick over a
    # ring of moons
    b.bricks_along(b.f("left flank"), length=34, gap=2.5, trim1=LEFT_FLANK_TRIM, green=False, tag="left flank")
    b.bricks_along(b.f("right flank"), length=34, gap=2.5, green=False, tag="right flank")
    for (x, y) in b.f("spire tips"):
        b.place(x, y, green=False, tag="spire tip")
    cx, cy, R, _, _ = b.circle("rose window")
    b.arc_bricks(cx, cy, R, 195, 150, n=3, green=False, tag="rose window arch")
    for a in (30, 90, 150):
        t = math.radians(a)
        b.place(cx + R * math.cos(t), cy + R * math.sin(t), green=False, tag="rose window")
    b.place(cx, cy, r=12, green=False, tag="rose window heart")
    for (x, y) in PINNACLES:
        b.place(x, y, tag="pinnacle")
    for (x, y) in ((628, 318), (620, 400), (500, 420), (560, 430), (600, 440)):
        b.place(x, y, tag="cathedral body")
    for (x, y) in ((690, 112), (700, 420), (650, 80), (690, 62)):
        b.place(x, y, tag="cloud beyond")
    # ---- below: the cathedral's foot, the piers in the fog and the lower town
    for (x, y) in b.f("cathedral foot"):
        b.place(x, y, tag="cathedral foot")
    for (x, y) in b.f("piers"):
        b.place(x, y, tag="pier in the fog")
    for (x, y) in b.f("lower town"):
        b.place(x, y, tag="lower town")
    b.greens_in_reach()
    b.candidates(exclude_tags=EXCLUDE_TAGS)


# ---- tuning (written by `mfl.py tune`; held out 5.1/48, tuning seeds 4.0/48, band 3.60-4.70; NOT in band: stuck)
TUNING = {
    "blue": [[256.0, 144.0], [228.0, 104.0], [514.0, 100.0], [484.0, 150.0], [588.0, 506.0], [268.0, 290.0], [387.6, 306.2], [475.1, 212.2], [467.1, 250.7], [459.2, 289.2]],
    "orange": [[400.0, 214.0], [436.0, 214.0], [344.0, 278.0], [115.0, 171.2], [147.2, 156.2], [215.5, 173.2]]
}
