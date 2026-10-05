"""2-1 "Limsa Across the Water" (Vesper Bay, the twins): a procession over a lattice of arches.

Subject: Limsa Lominsa, the far end of the Vesper Bay ferry, as the ferry casts off for the bay: the great tree-grown
tower and the
lamplit bridge on its arches over the sea mist, with the white castle's flank beyond, in the game's own La Noscea
painting (its western half) at its high-resolution size.

Technique: the bridge deck is a dotted line with its lamps dotted above it (a procession), and the house on the bridge has
its roof lamps; beneath, the two arches are crowns of brick with a lantern hung in each; the cloud tops above are gentle
humps of brick with a moon on each; the tower's canopy and the castle's flank are dotted edges, and the tower's lit
windows and a few lights in the mist fill the lower town. The stage opens here; the arches' lanterns, behind their
bricks, want a bank shot.
"""
LEVEL = dict(id="base-06", name="Limsa Across the Water", stage=2, number=6, scene="limsa-across-water",
             subject="Limsa Lominsa's lamplit bridges and tree-grown tower from the water",
             technique="a procession (bridge lamps) over a lattice of arches")


def build(b):
    b.trace("deck", spacing=50, r=9, tag="deck")
    for name in ("main arch", "west arch", "cloud a", "cloud b"):
        cx, cy, R, a0, sw = b.circle(name)
        b.arc_bricks(cx, cy, R, a0, sw, n=3 if R > 80 else 2, t=12 if R > 60 else 10, tag=name)
    b.trace("deck lamps", spacing=48, r=9, orange={1}, tag="deck lamp")
    b.trace("roof lamps", spacing=38, r=9, orange={0, 3}, green=False, tag="roof lamp")
    for name in ("main arch", "west arch"):
        cx, cy, R, _, _ = b.circle(name)
        b.place(cx, cy - R + 36, r=9, orange=True, green=False, tag="arch lantern")
    for name in ("cloud a", "cloud b"):
        cx, cy, R, _, _ = b.circle(name)
        b.place(cx, cy - R - 28, r=9, orange=True, tag="cloud summit")
    b.trace("castle edge", spacing=36, r=9, orange=True, tag="castle")
    b.trace("canopy edge", spacing=40, r=9, orange={1, 2}, tag="canopy")
    b.trace("pier", spacing=40, r=9, orange={0}, tag="pier")
    for k, (x, y) in enumerate(b.f("castle windows")):
        if b.place(x, y, r=9, orange=True, tag="castle window") and k == 0:
            b.arc(x, y + 30, 60, 240, 60, t=10, tag="window hood")    # a hood of brick over the lit window
    for k, (x, y) in enumerate(b.f("tower lights")):
        b.place(x, y, r=9, orange=True, tag="tower window")
    for k, (x, y) in enumerate(b.f("mist lights")):
        b.place(x, y, r=9, orange=True, tag="mist light")
    for (x, y) in ((150, 240), (230, 290), (650, 420), (700, 300), (640, 200), (300, 430), (420, 430), (520, 300),
                   (690, 200), (330, 200), (250, 200), (610, 330), (200, 330), (360, 470), (130, 250), (620, 480), (190, 480), (540, 440), (300, 380)):
        b.place(x, y, r=8, tag="air")
    b.greens_in_reach()
