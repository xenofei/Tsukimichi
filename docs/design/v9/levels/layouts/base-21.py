"""5-1 "The White Towers" (Harbour Lights, Merlwyb): a landmark partly outlined.

Subject: the Admiral's white towers over the bay, the castle of Limsa Lominsa, in the east of the game's own La Noscea
loading-screen painting at its high-resolution size (read from the install, never shipped): the main spire cluster,
the middle spire with its pale flare, the tall east spire and the gilt east tower, the spiral stair round the west
tower, the rigging moored at the castle's west side and the cloud banks east of it. The stage opens on it.

Crop (recorded change from the charter's "about [1800, 60, 1440, 1080]"): [1748, 60, 1440, 1080], 52 px further west,
so the launcher hangs in the open sky between the main spire cluster and the east spire instead of in front of the
main cluster's tallest spire. It stays east of 2-1's crop (which ends at x 1720: no overlap) and overlaps 5-4's
planned crop [2400, 700, 1440, 1080] (sources-plan.md's fix) by 22.3% of either's area (the rule's limit is 25%).

Technique: the castle is partly outlined. Moons run up each spire as far as the launcher's swing allows (the tips
above y ~100 are out of every direct flight), an even outline follows the east tower's edge against the cloud, and the
rigging at the west side is dotted (mast, yard, stay and shroud), so the board's two flanks are the castle's own
silhouette. Inside, the tower faces carry their lit windows in dotted columns, the roof rail between the spires is
dotted, the spiral stair is a brick run with its lamps dotted above (34 clear of it, past the wedge band), and the
window's arch is a crown of brick open at the keystone over a lamp. The cloud banks east of the towers are billow
crowns of brick, the upper two orange-able, a moon under each. The spires, the east edge, the arch and its lamp are
the subject's own shapes: never green. The gilt tower's windows fill the one dead lane of the first sweep (aims
26.75-29 degrees, between the middle and east spires down the gilt tower's face).

Candidates (29, the charter's count): the spires, the east edge, the rigging, the stair lamps, the tower windows and
the two upper billows; none low (y 430 or lower), so cheap difficulty cannot arise. Tuned on seeds 1-864 (24.7) and
20001-23456 (25.8) toward the charter's 26: the roof rail's two moons are blue and two gilt-tower windows, (530, 338)
and (532, 376), are candidates (from 27.1 on the second block). The billow hearts and the window lamp are blue: as
candidates under their crowns they were left in 29-39% of lost games.

Dress: violet night (first jewel ~273) with sea-green mist (~188) on the clouds and the bay's haze round the castle's
foot, never on the castle (`not-poly` of the castle's silhouette); the castle lifted 1.22 so the white towers read at
640; four moonbeams from the upper left; harbour ropes in the top corners and two lantern posts at the foot. Motion:
mist on the water (a band below the pegs), the beams, the posts' flicker.
"""
import math

LEVEL = dict(id="base-21", name="The White Towers", stage=5, number=21, scene="limsa-white-towers",
             subject="the Admiral's white towers over the bay, partly outlined",
             technique="a landmark partly outlined")


def build(b):
    # ---- the towers' spires: moons up each spire as far as the launcher's swing allows (never green)
    b.trace("main spire", spacing=36, r=9, orange={0, 1, 2, 3}, green=False, tag="main spire")
    b.trace("west tower", spacing=36, r=9, orange={0, 2}, green=False, tag="west tower")
    b.trace("middle spire", spacing=34, r=9, orange={0}, green=False, tag="middle spire")
    b.trace("east spire", spacing=34, r=9, orange={0, 2}, green=False, tag="east spire")
    for (x, y) in b.f("east cone"):
        b.place(x, y, r=9, orange=True, green=False, tag="east cone")
    b.trace("roof rail", spacing=38, r=8, tag="roof rail")
    # ---- the east tower's edge against the cloud, an outline just outside it
    b.trace("east tower edge", spacing=36, r=9, orange={1, 3, 5, 6}, green=False, tag="east tower edge")
    # ---- the rigging moored at the west side: mast, yard, a stay and a shroud
    b.trace("mast", spacing=34, r=8, orange={1, 3}, tag="mast")
    b.trace("yard", spacing=36, r=8, orange={1, 3}, tag="yard")
    b.trace("stay", spacing=34, r=8, orange={2}, tag="stay")
    b.trace("shroud", spacing=36, r=8, orange={0}, tag="shroud")
    # ---- the spiral stair round the west tower: a brick run, its lamps dotted above
    b.bricks_along("spiral stair", length=28, gap=2.5, t=10, tag="spiral stair")
    b.trace("stair lamps", spacing=36, r=8, orange={1, 3}, tag="stair lamp")
    b.trace("white tower", spacing=36, r=8, orange={0}, tag="white tower")
    # ---- the window's arch: a crown of brick open at its keystone, a lamp in its heart
    cx, cy, R, a0, sw = b.circle("window arch")
    b.arc_bricks(cx, cy, R, a0, sw, n=2, gap_deg=math.degrees(28.5 / R), t=10, green=False, tag="window arch")
    for (x, y) in b.f("window heart"):
        b.place(x, y, r=9, green=False, tag="window lamp")
    b.trace("east white tower", spacing=36, r=8, orange={0, 1}, tag="east white tower")
    b.trace("gold tower windows", spacing=38, r=8, orange={1, 2}, tag="gold tower window")
    # ---- the cloud banks east of the towers: billow crowns of brick, a moon under the upper two
    for name in ("billow high", "billow mid", "billow low", "billow west"):
        cx, cy, R, a0, sw = b.circle(name)
        b.arc(cx, cy, R, a0, sw, t=10, orange=name in ("billow high", "billow mid"), tag="billow")
        if name in ("billow high", "billow mid"):
            b.place(cx, cy + 4, r=8, tag="billow heart")
    b.greens_in_reach()
