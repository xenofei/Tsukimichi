"""4-2 "The Spire of the Sands" (The Sunlit Steps, Raubahn): a landmark partly outlined.

Subject: the tall gilt spire at Ul'dah's east edge and the skyway that runs into it, in the game's own Thanalan
loading-screen painting (read from the install, never shipped), unmirrored: the east strip beyond 1-4's crop,
[2780, 400, 1060, 795]. 1-4 took [800, 0, 2080, 1560] mirrored, which is x 960-3040 of the source; this crop overlaps
it by 260 px of its 1060 (24.5% of its own area, under the 25% rule), and is wider than 2-3's approved 960. The charter's
"red craft at the upper right" is the painted hull of an airship seen from below, its keel running down toward the
launcher; it is traced, not outlined (its prow sits by the launcher's swing).

Technique: both flanks of the spire are traced 18 units outside its silhouette, a crown of brick caps its head under the
needle, the skyway's deck is a run of brick with two lamps over its rail, and the airship's keel is a dotted line. The
cloud lens to the east is drawn by its lit top and its underside, the haze over the far city by a row, the far blue
dome by a crown with a moon on its needle. A short high streak of three blue moons sets the ramp.

Candidates (30): the flanks (ten), the keel (five), the cloud lens (eight), the skyway lamps and the west sky (four),
the haze (two) and the dome's needle. None is at y 430 or lower (the cheap-difficulty gate); the low ones are the
skyway lamps, the east flank's (320, 419), the needle and the haze. Tuned on seeds 1-864 (25.6) and 20001-23456 (25.0)
toward the charter's 25; the high streak of four moons read 23.4, of three 25.6.
"""
import math

LEVEL = dict(id="base-17", name="The Spire of the Sands", stage=4, number=17, scene="uldah-spire-sands",
             subject="the tall spire and the skyway at Ul'dah's east edge, partly outlined, under a red airship's keel",
             technique="a landmark partly outlined")

# the orange candidates, by place (each the nearest peg within 6 units)
ORANGE = {
    # the spire's east flank, every other moon, and its lamp's two neighbours
    (271, 136), (290, 206), (300, 240), (307, 275), (314, 347), (320, 419),
    # the west flank
    (143, 179), (140, 250), (143, 322), (147, 358),
    # the open sky west of the spire, the skyway's lamps
    (100, 242), (104, 334), (108, 423), (140, 404),
    # the airship's keel
    (378, 225), (447, 193), (482, 177), (516, 160), (550, 144),
    # the cloud bank's lit top and its underside
    (392, 312), (478, 296), (522, 293), (566, 292), (654, 294), (480, 343), (579, 350), (629, 348),
    # the haze over the far city, the far dome's needle
    (518, 426), (614, 420), (356, 398),
}


def build(b):
    # ---- the spire: a crown of brick over its head, both flanks traced just outside the silhouette
    b.arc_bricks(205, 140, 50, 232, 76, n=1, t=10, green=False, tag="spire head")
    b.trace("spire west flank", spacing=36, r=9, offset=18, start=10, end_trim=30, tag="west flank")
    b.trace("spire east flank", spacing=36, r=9, offset=-18, start=10, tag="east flank")
    # ---- the skyway: its deck in brick, lamps along its rail
    deck = [(96, 468), (124, 452), (158, 431)]
    b.bricks_along(deck, length=28, gap=2.5, t=10, green=False, tag="skyway")
    for (x, y) in ((108, 423), (140, 404)):
        b.place(x, y, r=9, tag="skyway lamp")
    # ---- the airship's keel, dotted, and its stern
    b.trace("craft keel", spacing=38, r=9, offset=16, tag="keel")
    for (x, y) in ((660, 112), (702, 118)):
        b.place(x, y, r=9, tag="stern")
    # ---- the cloud bank as a lens (its lit top and its underside), and the haze over the far city
    b.trace("cloud bank", spacing=44, r=9, tag="cloud bank")
    b.trace([(430, 336), (500, 346), (570, 350), (640, 348), (712, 342)], spacing=50, r=9, tag="cloud underside")
    b.trace("haze", spacing=48, r=9, tag="haze")
    # a short high streak of cloud, blue (the ramp's knob: four moons read 23.4 per 48 on the tuning seeds, three 25.6)
    b.trace([(590, 242), (640, 238), (700, 241)], spacing=50, r=9, tag="high streak")
    # ---- the far city: a crown on the blue dome, its needle, the shell arch's apex, far lamps
    cx, cy, R, a0, sw = b.circle("far dome")
    b.arc_bricks(cx, cy, R, a0, sw, n=1, t=10, green=False, tag="far dome")
    for (x, y) in ((356, 398), (357, 434)):
        b.place(x, y, r=9, green=False, tag="far needle")
    b.place(448, 486, r=9, tag="shell arch")
    # ---- the spire's foot and the skyway's under-arch, blue, over the bucket's approach
    for (x, y) in ((206, 498), (252, 504), (296, 494), (112, 500), (150, 482)):
        b.place(x, y, r=9, tag="foot")
    for (x, y) in ((530, 492), (600, 488), (670, 494)):
        b.place(x, y, r=9, tag="far lamp")
    # ---- the open sky west of the spire
    for (x, y) in ((100, 242), (104, 334)):
        b.place(x, y, r=9, tag="west sky")
    # ---- the candidates
    for p in b.pegs:
        p["canBeOrange"] = any(math.hypot(p["x"] - x, p["y"] - y) <= 6 for (x, y) in ORANGE)
    b.greens_in_reach()
