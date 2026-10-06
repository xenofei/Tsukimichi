"""2-1 "Limsa Across the Water" (Vesper Bay, the twins): a landmark in outline over a procession.

Subject: Limsa Lominsa, the far end of the Vesper Bay ferry, as the ferry casts off for the bay: the great tree-grown
tower, its round canopy over the city's spires, the lamplit bridge running east from its foot on two arches, and the sea
mist below, in the game's own La Noscea painting (its western part; stage 5 keeps the castle and the far towers to the
east for its own crop) at its high-resolution size.

Technique: the tree's canopy is a dotted outline 18 units outside its crown, broken at the top where the spire rises
through it (game designer M5: one Limsa landmark carries the board); every moon of it is a candidate, never green, and
four lanterns hang inside the crown. The bridge east of the tree is a procession: its deck dotted, its lamps dotted
above, the house at its west end with two lit windows (never green); the two arches beneath wear crowns of brick, open
at the keystone, the slot between them wider than a ball's rest. The city's four dark spires west of the tree are dotted columns, lights glow in the mist at the
tree's foot, and a dotted swell runs across the water at y 480-500 in three runs. The stage opens here, so the oranges sit high and
in the open, and the sky is left to the painting's clouds (blue moons in the open sky made the board harder without
saying anything).
"""
import math

LEVEL = dict(id="base-06", name="Limsa Across the Water", stage=2, number=6, scene="limsa-across-water",
             subject="Limsa Lominsa's tree-grown tower and its lamplit bridge from the water",
             technique="a landmark in outline over a procession")


def build(b):
    # the tree's canopy: the landmark (never green: it is the subject's own shape)
    b.trace("canopy", spacing=36, r=9, offset=-18, orange=True, green=False, tag="canopy")
    for (x, y) in b.f("house windows"):
        b.place(x, y, r=9, orange=True, green=False, tag="house window")
    # two lanterns hung in the tree's crown, so a shot straight down meets the landmark
    for (x, y) in b.f("tree lanterns"):
        b.place(x, y, r=9, orange=True, green=False, tag="tree lantern")
    # the bridge: deck and lamps (a procession); its arches' crowns of brick are open at the keystone (about 19 between
    # the bricks, wider than the 17 a ball can still rest in: critic round 4, N11), so no ball rests on an apex
    # (the deck's east end, 6, is a candidate and the air's (700, 320) blue: the wider keystone slot eased the board by
    # about 1.2 per 48, and this buys it back on the tuning seeds)
    b.trace("bridge deck", spacing=36, r=9, orange={1, 4, 6}, tag="bridge deck")
    b.trace("bridge lamps", spacing=36, r=9, orange={0, 2, 4}, tag="bridge lamp")
    for name in ("west arch", "east arch"):
        cx, cy, R, a0, sw = b.circle(name)
        b.arc_bricks(cx, cy, R, a0, sw, n=2, gap_deg=math.degrees(30 / R), t=10, tag=name)
    # the city west of the tree: its dark spires, dotted
    b.trace("west spire", spacing=34, r=8, orange={1}, tag="west spire")
    b.trace("middle spire", spacing=34, r=8, orange={0, 2}, tag="middle spire")
    b.trace("far spire", spacing=34, r=8, orange={1}, tag="far spire")
    b.trace("near spire", spacing=34, r=8, tag="near spire")
    # the water: lights in the mist at the city's foot, and the swell
    b.trace("mist lights", spacing=40, r=8, tag="mist light")
    # the swell in three runs that follow the water, not one fence across it (critic L13)
    for run in ([(96, 492), (250, 486)], [(320, 492), (440, 498)], [(520, 488), (700, 494)]):
        b.trace(run, spacing=40, r=8, tag="swell")
    # the air between: a few moons, the open-water ones candidates
    LIT = ((330, 400), (296, 420), (420, 410))
    for (x, y) in LIT + ((700, 320), (600, 300), (470, 300)):
        b.place(x, y, r=8, orange=(x, y) in LIT, tag="air")
    for (x, y) in ((356, 342), (446, 346)):          # two more lanterns in the crown, in the dead lanes (critic G4)
        b.place(x, y, r=9, green=False, tag="tree lantern")
    b.greens_in_reach()
