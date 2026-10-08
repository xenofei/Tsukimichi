"""4-2 "The Spire of the Sands" (The Sunlit Steps, Raubahn): a landmark partly outlined.

Subject: the tall gilt spire at Ul'dah's east edge and the skyway that runs into it, in the game's own Thanalan
loading-screen painting (read from the install, never shipped), unmirrored: the east strip beyond 1-4's crop,
[2780, 400, 1060, 795]. 1-4 took [800, 0, 2080, 1560] mirrored, which is x 960-3040 of the source; this crop overlaps
it by 260 px of its 1060 (24.5% of its own area, under the 25% rule), and is wider than 2-3's approved 960. The charter's
"red craft at the upper right" is the painted hull of an airship seen from below, its keel running down toward the
launcher; it is traced, not outlined (its prow sits by the launcher's swing).

Technique: both flanks of the spire are traced 18 units outside its silhouette, a crown of brick caps its head under the
needle, the skyway's deck is a run of brick with two lamps over its rail, and the airship's keel is a dotted line under
its hull, which the scene grades back to its painted red (MR 4 round 1, game designer M2, critic m3). East of the spire
the cloud lens is drawn as a tongue: its lit top and its underside close to a point by the spire's flank, with three
blue moons on its lit seam; the high streak over it rises east under the craft's stern. Below, a short curl of the haze
band, the sands' horizon (blue), the far blue dome's crown and needle. The ground is graded sand, not green.

Candidates (28): the flanks (seven: the east flank's lit foot by the lamp is blue, UX n2), the keel (six), the cloud
lens (nine: five on its top, four on its underside), the high streak (one), the west sky and the skyway's upper lamp
(three), the dome's needle and the haze curl (two). None is at y 430 or lower (the cheap-difficulty gate), and only
(140, 404) and the haze curl's (522, 404) sit in y 400-430 (round 1's top holdouts at y 419-426, critic m2, are blue or
gone). Tuned on seeds 1-864 (25.7) and 20001-23456 (25.4); the lens seam is the ramp's knob (see build).
"""
import math

LEVEL = dict(id="base-17", name="The Spire of the Sands", stage=4, number=17, scene="uldah-spire-sands",
             subject="the tall spire and the skyway at Ul'dah's east edge, partly outlined, under a red airship's keel",
             technique="a landmark partly outlined")

# the orange candidates, by place (each the nearest peg within 6 units); the cloud lens's are by index along its edges
ORANGE = {
    # the spire's east flank above its lamp (not the lit flank below it: UX n2's F6 headroom)
    (271, 136), (290, 206), (300, 240),
    # the west flank
    (143, 179), (140, 250), (143, 322), (147, 358),
    # the open sky west of the spire, the skyway's upper lamp
    (100, 242), (104, 334), (140, 404),
    # the airship's keel
    (378, 225), (413, 210), (447, 193), (482, 177), (516, 160), (550, 144),
    # the far dome's needle, and the haze band's curl over the far city (the right half's one low candidate)
    (356, 398), (522, 404),
}
LENS_TOP, LENS_UNDER, STREAK = {0, 2, 3, 5, 6}, {0, 1, 3, 5}, {1}


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
    # ---- the airship's keel, dotted under its red hull, and its stern
    b.trace("craft keel", spacing=38, r=9, offset=16, tag="keel")
    for (x, y) in ((660, 112), (702, 118)):
        b.place(x, y, r=9, tag="stern")
    # ---- the cloud lens east of the spire: its lit top and its underside, drawn to the tongue's point by the spire
    lens = b.trace("cloud lens top", spacing=48, r=9, orange=LENS_TOP, tag="lens top")
    lens += b.trace("cloud lens underside", spacing=50, r=9, start=50, orange=LENS_UNDER, tag="lens underside")
    # the lit seam along the lens's middle, blue (the ramp's knob, per 48 on seeds 1-864 / 20001-23456: no seam 25.7 /
    # 26.1, these three 25.7 / 25.4, the same three at 40 apart 24.1 / 24.0; nearer the charter's 25 from above, since
    # 4-3 (23.45) is being eased by its own fix)
    for (x, y) in ((520, 322), (562, 324), (604, 324)):
        b.place(x, y, r=9, tag="lens seam")
    # the high streak of cloud over it, rising east under the craft's stern
    lens += b.trace("high streak", spacing=60, r=9, start=30, orange=STREAK, tag="high streak")
    # ---- the far city: a crown on the blue dome, its needle, the shell arch's apex, and the sands' horizon beyond
    cx, cy, R, a0, sw = b.circle("far dome")
    b.arc_bricks(cx, cy, R, a0, sw, n=1, t=10, green=False, tag="far dome")
    for (x, y) in ((356, 398), (357, 434)):
        b.place(x, y, r=9, green=False, tag="far needle")
    b.place(448, 486, r=9, tag="shell arch")
    for (x, y) in ((472, 414), (522, 404)):
        b.place(x, y, r=9, tag="haze curl")
    b.trace("horizon", spacing=62, r=9, start=40, tag="horizon")
    # ---- the spire's foot and the skyway's under-arch, blue, over the bucket's approach
    for (x, y) in ((206, 498), (252, 504), (296, 494), (112, 500), (150, 482)):
        b.place(x, y, r=9, tag="foot")
    # ---- the open sky west of the spire
    for (x, y) in ((100, 242), (104, 334)):
        b.place(x, y, r=9, tag="west sky")
    # ---- the candidates
    for p in b.pegs:
        p["canBeOrange"] = (p in lens and bool(p.get("canBeOrange"))) or             any(math.hypot(p["x"] - x, p["y"] - y) <= 6 for (x, y) in ORANGE)
    b.greens_in_reach()
