"""Pilot base-p2 "The Holy See" (technique: terrain emphasis), layout.

The scene (scene_ishgard.py) is Ishgard by moonlight above the sea of clouds, mirrored so its light comes from the
upper left. The layout rides the terrain: moonstone bricks lie along the mountain range's crest with a moon on each
summit; three scalloped bricks follow the billows of the cloud sea, with a dotted row below them; three arch bricks sit
in the bridge's arches with its lamps dotted along the deck above; the cathedral is drawn by its spire tips, its two
flanks and its rose window, and the fog below gets the piers and the lower town. Orange candidates are the places that
carry the scene: the summits, the billows' hearts, the bridge lamps, the spire tips and the rose window.

Spacing: dotted lines are 34 or more apart centre to centre (14 between pegs), so a ball passes through them and never
rests in the dip between two; continuous lines are bricks.
"""
from layout import Layout
from rich_lib import LEVELS, level_json, write_level

LEVEL_ID = "base-p2"
NAME = "The Holy See"


def deck_y(x):
    return 441.0 - (x - 312.0) * 0.346          # the bridge deck's top edge in the scene


def build():
    L = Layout()
    # ---- the mountain range: one unbroken run of bricks along the crest, and a moon standing over each summit
    # the crest rises from beside the wall (a ball's width and more clear of it, so none wedges there) to the high summit and
    # then falls without a dip to the shoulder above the clouds: two monotone slopes, so the ball runs along them
    ridge = [(100, 178), (113, 172), (138, 161), (162, 149), (186, 163), (212, 172), (238, 181), (262, 190),
             (286, 205), (310, 222), (334, 237), (360, 248), (395, 258), (428, 272)]
    L.bricks_along(ridge, length=24, gap=2, t=12, tag="ridge")
    for (x, y, o) in ((162, 122, False), (256, 156, True), (341, 202, True)):
        L.peg(x, y, orange=o, tag="moon over a summit")
    # the high cloud bank above the lower slopes
    for (x, y) in ((228, 118), (292, 138), (330, 168)):
        L.peg(x, y, tag="high cloud")
    # ---- the sea of clouds: three billow tops as gentle humps of brick, a heart under each, a dotted row lower
    for (cx, top) in ((150, 300), (268, 290), (392, 306)):
        L.arc(cx, top + 50, 50, 230, 80, t=10, tag="billow")
    # the hearts sit in the troughs between the billows (open from above, on the cloud sea's darker folds)
    for (x, y) in ((100, 352), (209, 314), (330, 316)):
        L.peg(x, y, orange=True, tag="billow heart")
    for (x, y) in ((100, 392), (140, 404), (190, 398), (232, 394)):
        L.peg(x, y, orange=(x == 190), tag="cloud sea")
    # ---- the bridge: three arches as bricks in the painted arches, its lamps dotted along the deck
    for x in (374, 428, 482):
        top = deck_y(x) + 17
        L.arc(x, top + 20, 20, 220, 100, t=8, tag="arch")
    for x in (300, 340, 380, 420):
        L.peg(x, deck_y(x) - 15, orange=True, tag="bridge lamp")
    L.peg(250, 362, orange=True, tag="gate spire")
    # ---- the cathedral: spire tips, two flanks, the rose window
    for (x, y) in ((549, 58), (514, 100), (588, 86), (624, 134), (484, 150)):
        L.peg(x, y, orange=(y >= 100 and x < 600), tag="spire tip")
    for (x, y) in ((478, 194), (472, 232), (462, 270), (452, 308), (446, 346)):
        L.peg(x, y, tag="left flank")
    for (x, y) in ((642, 176), (654, 212), (668, 248), (676, 284), (678, 320), (670, 356), (662, 392)):
        L.peg(x, y, tag="right flank")
    for k, p in enumerate(L.ring(546, 333, 44, 8, start_deg=-90, tag="rose window")):
        p["canBeOrange"] = (k % 2 == 0)
    L.peg(546, 333, r=12, orange=True, tag="rose window heart")
    for (x, y) in ((700, 186), (700, 240), (714, 292), (700, 384)):
        L.peg(x, y, tag="cloud beyond")
    # ---- below: a far peak at the right, the cathedral's foot, the piers in the fog and the lower town
    for (x, y) in ((688, 452), (700, 420)):
        L.peg(x, y, tag="far peak")
    for (x, y) in ((480, 478), (516, 506), (552, 478), (588, 506), (624, 478), (660, 506)):
        L.peg(x, y, orange=(x in (480, 552, 624)), tag="cathedral foot")
    for (x, y) in ((322, 496), (362, 486), (404, 476), (444, 470), (300, 534), (346, 530), (392, 524), (438, 520)):
        L.peg(x, y, orange=(x, y) in ((322, 496), (404, 476)), tag="pier in the fog")
    for (x, y) in ((108, 470), (150, 448), (192, 470), (128, 512), (172, 512), (216, 506), (236, 456)):
        L.peg(x, y, orange=(x, y) in ((150, 448), (192, 470), (236, 456)), tag="lower town")
    return L


if __name__ == "__main__":
    L = build()
    print(L.counts())
    L.check()
    pegs, bricks = L.as_level()
    scene = {"source": "game", "texture": "ui/loadingimage/-nowloading_base03.tex", "mirror": True,
             "crop": [430, 60, 1293, 970], "grade": "medallion-night", "veil": 0.36}
    write_level(level_json(LEVEL_ID, NAME, pegs, bricks, scene=scene), LEVELS / f"{LEVEL_ID}.json")
