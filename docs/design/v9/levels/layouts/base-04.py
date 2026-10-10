"""1-4 "The Gilded Dome" (The Waking Sands, Minfilia): a landmark partly outlined.

Subject: Ul'dah's great dome, the city where Minfilia grew up, in the game's own Thanalan painting (the loading image
the game shows for the Waking Sands itself), mirrored so its light comes from the upper left.

Technique: the great dome's crown is a long arc of nine curved bricks, so a ball rolls off it to either side; the
lesser domes and the palace's great arch wear crowns of two bricks with a moon standing over the joint (every crown
never green: it is the subject's own shape). A moon stands on each lantern turret, on the gilt crest and on the lesser
domes' finials; the drum's windows are a dotted line under the crown. The needle spires either side of the dome and the
east tower are columns of brick, the west sails are traced in brick along their edges, and the aqueduct stands on brick
legs under its dotted deck, beside a dotted pier. The palace's columns and windows and the terrace's lamps along the
foot are blue (but one window), the lamps staggered on two steps. Only part of each shape is drawn: the painting
finishes it.

Peggle-match refit (owner's decision, 9 October 2026): 105 pieces (35 bricks, 33%), pegs r 10 and bricks t 20, 64
candidates (61%) marked by `b.candidates`. Left out: the great crown and the west spire (blue: as candidates they were
the first pieces every shot cleared, and the board read about 4 per 48 easier), the low pieces but one each side (the
low oranges were over-represented among those left: cheap difficulty), five sky stars (blue in the open sky, the
difficulty lever), and the lamps, columns, windows and sails. Tuned on seeds 20001-23456 to 8.9 per 48 against stage
MR 1's mean of 8.57 (band 7.47-9.67).
"""
import math

LEVEL = dict(id="base-04", name="The Gilded Dome", stage=1, number=4, scene="uldah-gilded-dome",
             subject="Ul'dah's great dome and the palace beneath it",
             technique="a landmark partly outlined")


def build(b):
    # ---- bricks first (the pegs keep their distance from them)
    # the crowns are the subject's own shapes: they shield the features and never turn green
    cx, cy, R, a0, sw = b.circle("great dome")
    b.arc_bricks(cx, cy, R, a0, sw, n=9, green=False, tag="dome crown")
    for name, (s, w) in (("dome a", (212, 116)), ("dome b", (212, 116)), ("east dome", (222, 96)),
                         ("great arch", (212, 116))):
        x, y, r, _, _ = b.circle(name)
        b.arc_bricks(x, y, r, s, w, n=2, green=False, tag=f"{name} crown")
    # the needle spires either side of the dome and the east tower: columns of brick
    b.bricks_along([(297, 112), (297, 196)], length=40, tag="west spire")
    b.bricks_along([(628, 150), (628, 300)], length=36, tag="east spire")
    b.bricks_along("tower", length=34, tag="tower")
    # the west sails along their edges
    b.bricks_along("west sail", length=30, tag="west sail")
    b.bricks_along([(x - 6, y) for (x, y) in b.f("west sail back")], length=30, tag="west sail")
    # the aqueduct's legs under its deck
    for x in (128, 208):
        b.line(x, 330, x, 366, tag="aqueduct leg")

    # ---- the dome's features: lantern turrets, the crest, the finials of the lesser domes (never green)
    for (x, y) in ((503, 233), (540, 270), (565, 304), (410, 236)):
        b.place(x, y, green=False, tag="turret")
    for name in ("dome a", "dome b", "east dome"):
        x, y, r, _, _ = b.circle(name)
        b.place(x, y - r - 36, green=False, tag=f"{name} finial")
    for name in ("dome c", "dome d"):
        (x, y), = b.f(name)
        b.place(x, y, green=False, tag=f"{name} finial")
    x, y, r, _, _ = b.circle("great arch")
    b.place(x, y - r - 36, green=False, tag="keystone")
    # the dome's gilt band under the crown, and its drum: the ring of windows at its foot
    for (x, y) in ((372, 262), (448, 222)):
        b.place(x, y, tag="gilt band")
    b.trace("drum", spacing=34, tag="drum")
    # the aqueduct: its posts and deck
    for (x, y) in b.f("aqueduct posts"):
        b.place(x, y, tag="aqueduct post")
    b.trace("aqueduct deck", spacing=34, tag="aqueduct deck")
    for y in (340, 376):
        b.place(168, y, tag="aqueduct arch")
    # the west hall's tower and the east rigging, dotted
    b.trace([(255, 318), (255, 440)], spacing=34, tag="west hall")
    b.trace([(100, 190), (100, 300)], spacing=36, tag="west pier")
    b.trace([(580, 330), (596, 390), (622, 450)], spacing=36, tag="east rigging")
    b.trace([(650, 330), (650, 402)], spacing=36, tag="east gallery")
    # the palace's windows and columns, and the terrace's lamps along the foot, staggered on two steps
    for (x, y) in ((292, 452), (328, 452), (560, 420)):
        b.place(x, y, tag="palace window")
    for (x, y) in ((372, 460), (436, 460), (492, 460)):
        b.place(x, y, tag="column")
    for k, x in enumerate(range(100, 701, 46)):
        b.place(x, 494 if k % 2 else 507, tag="terrace lamp")
    # the sky between the spires: a few stars
    for (x, y) in ((190, 135), (240, 150), (250, 222), (330, 170), (505, 110), (664, 172), (700, 196), (200, 190),
                   (292, 322), (556, 345), (520, 440)):
        b.place(x, y, tag="star")
    # the west pier's foot and more stars over the aqueduct and beside the east dome
    for (x, y) in ((99, 392), (160, 212), (592, 190), (250, 100), (640, 105), (150, 105)):
        b.place(x, y, tag="star")

    # ---- candidates: every piece but these (blue)
    b.greens_in_reach()
    # the low pieces stay blue but for one each side, (255, 420) and (560, 420) (the low oranges were over-represented
    # among those left: cheap difficulty); five sky stars are blue (the difficulty lever)
    low = [(292, 452), (328, 452), (520, 440), (614, 432), (684, 451), (310, 418)]
    sky = [(505, 110), (592, 190), (250, 100), (640, 105), (150, 105)]
    return b.candidates(exclude=low + sky,
                        exclude_tags=("terrace lamp", "column", "west sail", "dome crown", "west spire"))
