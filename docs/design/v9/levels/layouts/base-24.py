"""5-4 "The Arches of the Bridge" (Harbour Lights, Merlwyb): terrain emphasis.

Subject: Limsa Lominsa's lamplit bridge seen from the water, in the east of the game's own La Noscea loading-screen
painting (`-nowloading_base01_hr1`, read from the install, never shipped): three arches over the harbour mist, the lamps
on its deck, the cables swagged between its two towers, the domed east tower and its spires.

Crop (recorded here; the charter and sources-plan are not this level's files): [2560, 920, 1280, 960] rather than the
sources-plan's [2400, 700, 1440, 1080]. At the planned crop the deck lay at y 440 and the arches' crowns at 470-485,
in the bucket's approach, with the Admiral's white tower (5-1's subject) filling the left third. Moved east and down
and narrowed (1280 px, over the README's 1100), the deck sits at y 350 and the arches' crowns at 411-421; the overlap
with 5-1's crop falls from 24% to 12% of this crop, none with 2-1's.

Technique (the pilot base-p2's): the layout rides the bridge. Bricks lie in the painted intrados of the two whole
arches, opened at the keystone (an 18-unit slot between the bricks' rounded ends, as 3-3's east billow, so no flat apex
holds a ball), and one brick in the right arch at the wall; the two bricks by each crown are candidates. The bridge is
lamplit (round 1, game designer M2): of the deck's six painted lamps, three burn as the scene's flickering lights
(`lamp fires`; F5 allows three in a 40-unit band, and keeps every piece 8 units plus the halo from them) and a moon
stands on each of the other three, so lamps and moons alternate along the rail. Three more lights burn in the towers'
lit windows (`tower lights`: the dome's drum, the middle tower's upper window, its lit windows under the deck). The deck
is dotted in three spans over the arches, broken where the towers stand on the piers. The cables are dotted (the swag
between the towers, the west stays to the shore); the middle tower carries its chimney tops and thin spire, its roof
turret, its lit windows and a cable anchor on its east face; the east tower its spire tip and bodies, the drum's east
flank, the balcony and its two legs; the Admiral's castle at the left edge its pale wall and the spiral stair's railed
walkway. The arches, the lamps, the spire tip and the chimneys are the subject's own shapes: never green.

Round 1 (game designer M2, critic m6) took out the upper scatter that traced nothing: eight cloud moons, the dome's
lantern and flanks, a high tower-wall moon, the mist row and the two moons under the arches. The dome's crown as a brick
(the game designer's suggestion) is out of every ball's reach (the reach gate refused it), as are the castle wall's top
moon and the east spire's tip.

Candidates (30): three lamps, three lit windows, the four crown bricks and the right arch's brick, three of the east
tower's seven leg moons, the balcony's inner moon, two pier moons, the inner spire tip, a chimney top and the thin spire,
the roof turret, the stair's lower flight, a castle wall moon, two cable moons, and five of the deck. Low ones (y 430 or lower): one (the right
arch's brick); low oranges are about 4% of those left in lost games.

Tuning (seeds 1-864, and 20001-23456 for close calls), toward the charter's 21.5:
- Round 0 (83 pieces): tuning 21.3, second block 21.6, held out 21.3; the sky scatter carried much of that.
- Round 1, with the scatter gone (64 pieces), read 27.8: the deck, lamps and cables are where balls fall anyway. The
  balcony's corner moon (692, 180) as a candidate is left in 43-47% of lost games, so it stays blue; the east tower's
  legs, a pier moon and the deck's ends carry the hard right instead, and the cable anchor under the launcher (398, 296)
  is worth about 0.7. The castle wall's two top moons quieten the palette at the stair, where the painting's compressed
  blocks flip (the converter gate's 2x maximum, 0.043 without them, 0.038 with).
- Final: tuning 21.7, second block 21.0 (pooled 21.1); held out 21.3 (768 of 1728), the rule's 48 games 17. The worst
  holdout is the balcony's inner moon at 24% of lost games; low oranges 4.6% of those left.
"""

import math

LEVEL = dict(id="base-24", name="The Arches of the Bridge", stage=5, number=24, scene="limsa-bridge-arches",
             subject="the lamplit bridge: bricks in its arches, its lamps alight or crowned with moons",
             technique="terrain emphasis (brick runs on crests, moons on summits)")

DECK_ORANGE = {"deck span west": {1, 3}, "deck span middle": {2, 4}, "deck span east": {0}}


def arch(b, name, a0=210.0, a1=330.0, n_half=2, slot=28.5):
    """An arch's intrados as bricks, opened at the keystone (an 18-unit slot between the bricks' rounded ends)."""
    cx, cy, R, _, _ = b.circle(name)
    half = math.degrees(slot / R) / 2
    return (b.arc_bricks(cx, cy, R, a0, 270 - half - a0, n=n_half, t=10, green=False, tag=name)
            + b.arc_bricks(cx, cy, R, 270 + half, a1 - 270 - half, n=n_half, t=10, green=False, tag=name))


def build(b):
    # ---- the arches: bricks in the painted intrados, opened at the keystone; the two by each crown are candidates.
    # They start at 210 degrees, so the left arch's foot keeps 15 units from the wall (no wall pinch)
    for name in ("left arch", "middle arch"):
        for k, br in enumerate(arch(b, name)):
            br["canBeOrange"] = k in (1, 2)
    cx, cy, R, _, _ = b.circle("right arch")
    for br in b.arc_bricks(cx, cy, R, 212, 28, n=1, t=10, green=False, tag="right arch"):
        br["canBeOrange"] = True
    # ---- the lamps on the deck: three burn as the scene's flickering lights (`lamp fires`, kept clear of every piece:
    # F5), and a moon stands on each lamp between them
    fires = set(map(tuple, b.f("lamp fires")))
    for (x, y) in b.f("lamps"):
        if (x, y) not in fires:
            b.place(x, y, orange=True, green=False, tag="lamp")
    # ---- the deck: a dotted span over each arch, broken at the piers where the towers stand
    for name, oranges in DECK_ORANGE.items():
        b.trace(name, spacing=40, r=9, orange=oranges, tag="deck")
    # ---- the cables: the swag between the towers, the west stays toward the shore
    b.trace("cable east", spacing=36, orange={1}, tag="cable east")
    b.trace("cable west", spacing=36, orange={1}, end_trim=60, tag="cable west")
    # ---- the east tower: its spires (tip and body), the dome's lit drum (a light), the balcony, its two legs
    for (x, y) in b.f("spire tips")[:2]:                # the east spire's tip is out of every ball's reach
        b.place(x, y + 4, orange=x < 600, green=False, tag="spire tip")     # the other is beyond a first flight
    for (x, y) in b.f("spire bodies"):
        b.place(x, y, tag="spire")
    for (x, y) in b.f("drum flank"):
        b.place(x, y, tag="dome drum")                  # the drum's east flank, beside its lit window (a light)
    for (x, y) in b.f("balcony"):
        b.place(x, y, orange=x < 670, tag="balcony")       # the corner moon was left in 38-47% of lost games: blue
    for y in (210, 245, 280, 315):
        b.place(584, y, orange=y == 315, tag="tower leg")
    for y in (222, 257, 292):
        b.place(652, y, orange=y > 230, tag="tower leg")
    # ---- the middle tower: its chimney tops and thin spire, its lit windows (others burn as lights), its walls and roof
    for k, (x, y) in enumerate(b.f("chimneys") + b.f("thin spire")):
        b.place(x, y - 8, r=9, orange=k != 1, green=False, tag="chimney")
    for k, (x, y) in enumerate(b.f("lit windows")):
        b.place(x, y, orange=k > 0, tag="lit window")
    b.place(318, 290, tag="tower wall")
    b.place(398, 296, tag="cable anchor")               # its east face, where the swag to the east tower is made fast
    b.place(362, 222, orange=True, tag="turret")        # the small domed turret on its roof
    b.place(345, 432, tag="tower foot")                 # over its lower lit windows (a light)
    # ---- the Admiral's white castle at the left edge: its pale wall dotted down to the bridge
    for y in (95, 130, 165, 200, 235, 270, 305):
        b.place(100, y, orange=y == 270, tag="castle wall")
    b.trace("spiral stair", spacing=34, tag="spiral stair")    # its railed walkway, curving round
    b.place(160, 175, orange=True, tag="spiral stair")         # the stair's lower flight, within a first flight
    # ---- the piers between the arches
    for (x, y) in ((300, 425), (606, 390), (606, 425)):
        b.place(x, y, orange=x > 600, tag="pier")
    b.greens_in_reach()
