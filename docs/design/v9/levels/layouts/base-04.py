"""1-4 "The Gilded Dome" (The Waking Sands, Minfilia): a landmark partly outlined.

Subject: Ul'dah's great dome, the city where Minfilia grew up, in the game's own Thanalan painting (the loading image
the game shows for the Waking Sands itself), mirrored so its light comes from the upper left.

Technique: five curved bricks lie on the great dome's crown, so a ball rolls off it to either side; the two middle
domes wear short crowns of brick and the palace's great arch one more (every crown never green: it is the subject's
own shape). A moon stands on each lantern turret, on the gilt crest and on the lesser domes' finials, 20 units clear of
their crowns; the aqueduct's posts and deck to the west and a few stars between the spires carry oranges too. The
palace's columns and the terrace's lamps along the foot are blue (low oranges there decided lost games), the lamps
staggered on two steps; the tall tower to the east is a dotted column, the west sail is traced along its edges. Only
part of each shape is drawn: the painting finishes it.

The fourth level of the stage: bricks shield the dome's features, so oranges there need a bank shot or a cleared crown.
The features (turrets, crest, finials) may be orange but never green. The sky is darkened in the grade and the veil
eased so no disc stays behind when its moon clears (UX round 1, m1).
"""
import math

LEVEL = dict(id="base-04", name="The Gilded Dome", stage=1, number=4, scene="uldah-gilded-dome",
             subject="Ul'dah's great dome and the palace beneath it",
             technique="a landmark partly outlined")


def build(b):
    # the crowns are the subject's own shapes: they shield the features and never turn green
    cx, cy, R, a0, sw = b.circle("great dome")
    b.arc_bricks(cx, cy, R, a0, sw, n=5, t=12, green=False, tag="dome crown")
    for name in ("dome a", "dome b", "east dome"):
        x, y, r, _, _ = b.circle(name)
        b.arc_bricks(x, y, r, 215, 110, n=1, t=10, green=False, tag=f"{name} crown")
    x, y, r, _, _ = b.circle("great arch")
    b.arc_bricks(x, y, r, 215, 110, n=1, t=10, green=False, tag="great arch")
    # the dome's features: lantern turrets, the crest, the finials of the lesser domes (orange, never green); a finial
    # stands 20 units clear of its crown, out of the wedge band (critic L4)
    for (x, y) in b.f("turret tips") + b.f("crest"):
        b.place(x, y, r=9, orange=True, green=False, tag="turret")
    for name in ("dome a", "dome b", "east dome"):
        x, y, r, _, _ = b.circle(name)
        b.place(x, y - r - 34, r=9, orange=True, green=False, tag=f"{name} finial")
    for name in ("dome c", "dome d"):
        (x, y), = b.f(name)
        b.place(x, y, r=9, orange=True, green=False, tag=f"{name} finial")
    x, y, r, _, _ = b.circle("great arch")
    b.place(x, y - r - 34, r=9, orange=True, green=False, tag="keystone")
    # the dome's drum: the ring of windows at its foot, a dotted line under the crown
    b.trace("drum", spacing=38, r=9, orange="every:2", tag="drum")
    # the palace's columns (blue: the low oranges decided lost games), the tower's dotted column, the aqueduct
    for (x, y) in b.f("columns"):
        b.place(x, y, r=9, tag="column")
    b.trace("tower", spacing=36, r=9, orange={0, 2}, tag="tower")
    for (x, y) in b.f("aqueduct posts"):
        b.place(x, y, r=9, orange=True, tag="aqueduct post")
    b.trace("aqueduct deck", spacing=36, r=9, orange={0, 1, 3}, tag="aqueduct deck")
    # the sails, west and east, along their leading edges; the terrace's lamps along the foot, staggered so they read
    # as lamps on two steps rather than a floor (game designer n1), and blue
    b.trace("west sail", spacing=36, r=9, orange={0}, tag="west sail")
    b.trace("west sail back", spacing=36, r=9, tag="west sail")
    for k, x in enumerate(range(100, 701, 46)):
        b.place(x, 494 if k % 2 else 507, r=9, tag="terrace lamp")
    # the sky between the spires: dark moons at the dome's flanks, so the crown is fed from both sides; a few stars
    # over the aqueduct and the east dome
    for (x, y) in ((262, 200), (238, 330), (600, 330), (640, 210), (556, 420), (281, 305)):
        b.place(x, y, r=9, orange=(x, y) != (281, 305), tag="sky")
    for (x, y) in ((150, 150), (230, 150), (300, 128), (620, 172), (650, 280)):
        b.place(x, y, r=8, orange=True, tag="star")
    # the candidates the greedy player found hardest to clear (the east dome's finial at the top edge, the stars at the
    # board's corners, the tower's top) give way to the tower's middle and the aqueduct's end (`mfl.py ease`; the
    # round-2 ramp)
    for (x, y, o) in ((548, 100, False), (150, 150, False), (620, 172, False), (650, 280, False), (684, 232, False),
                      (684, 340, True), (684, 376, True),
                      (250, 279, True)):
        b.key(x, y, orange=o, within=6)
