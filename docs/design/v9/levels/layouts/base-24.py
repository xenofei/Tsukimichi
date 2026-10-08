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
holds a ball), and one brick in the right arch at the wall; the two bricks by each crown are candidates. Moons stand on
the deck's five painted lamps and the middle tower's three lit windows, a dotted row runs along the deck's dark band,
the cables are dotted (the swag between the towers, the west stays to the shore), and the towers are drawn by moons on
the spire tips, the dome's lantern and flanks, the balcony, and the two legs of the east tower. The arches, the lamps,
the spire tips and the dome's lantern are the subject's own shapes: never green.

Candidates (31, the charter's count): the five lamps, three lit windows, the four crown bricks and the right arch's
brick, the east tower's legs and balcony, the dome's flanks, a spire tip, a chimney, the cables, four of the deck row
and two moons in the high cloud. Low ones (y 430 or lower): one (the right arch's brick); low oranges are 4% of those
left in lost games.

Tuning (seeds 1-864, and 20001-23456 for close calls), toward the charter's 21.5:
- The first board (68 pieces) read 26.4: the deck, lamps and cables are where balls fall anyway. The balcony's corner
  moon (692, 180) was left in 38% of lost games; it is blue.
- The white castle's pale wall at the left edge (a dotted column) and the middle tower's walls under the launcher made
  the board 6 per 48 harder (20.8); the candidates were then spread so no 200 x 200 square holds more than 10 and no
  holdout passes 21% (the balcony's inner moon (652, 176) and the dome's east flank (618, 150) share the hard top
  right). A moon under the middle arch as a candidate was left in 47% of lost games (the cheap-difficulty gate); it
  stays blue. Two piers between the arches and the moon over the spire's foot moved nothing (+-0.5).
- Final: tuning 21.3, second block 21.6; held out 21.3 (767 of 1728), the rule's 48 games 15. The worst holdout is
  the balcony's inner moon at 20% of lost games; low oranges 4% of those left.
"""

import math

LEVEL = dict(id="base-24", name="The Arches of the Bridge", stage=5, number=24, scene="limsa-bridge-arches",
             subject="the lamplit bridge: bricks in its arches, moons on its lamps",
             technique="terrain emphasis (brick runs on crests, moons on summits)")

DECK_ORANGE = {1, 7, 13, 14}                        # (152, 372), (392, 370), (632, 368), (672, 368)
CLOUD_ORANGE = {(250, 130), (160, 170)}
TOWER_WALL_ORANGE = {(318, 230)}


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
    # ---- the lamps on the deck: moons on the painting's own lamps
    for (x, y) in b.f("lamps"):
        b.place(x, y, orange=True, green=False, tag="lamp")
    # ---- the deck: a dotted row along its dark band
    b.trace("deck band", spacing=40, r=9, orange=DECK_ORANGE, tag="deck")
    # ---- the cables: the swag between the towers, the west stays toward the shore
    b.trace("cable east", spacing=36, orange={1}, tag="cable east")
    b.trace("cable west", spacing=36, orange={0, 1}, end_trim=60, tag="cable west")
    # ---- the east tower: the dome's lantern and flanks, its spires, the balcony, its two legs
    cx, cy, R, _, _ = b.circle("dome")
    b.place(cx, cy - R - 16, green=False, tag="dome lantern")
    for (x, y) in b.f("spire tips"):
        b.place(x, y + 4, orange=x < 600, green=False, tag="spire tip")     # the other two are beyond a first flight
    for (x, y) in ((560, 150), (618, 150)):
        b.place(x, y, orange=True, tag="dome flank")
    for (x, y) in b.f("balcony"):
        b.place(x, y, orange=x < 670, tag="balcony")       # the corner moon was left in 38% of lost games: blue
    for y in (210, 245, 280, 315):
        b.place(584, y, orange=y in (245, 315), tag="tower leg")
    for y in (222, 257, 292):
        b.place(652, y, orange=y == 257, tag="tower leg")
    # ---- the middle tower: a chimney, its lit windows, its walls and its foot
    for (x, y) in b.f("chimneys")[:1]:
        b.place(x, y - 8, r=9, orange=True, green=False, tag="chimney")
    for (x, y) in b.f("lit windows"):
        b.place(x, y, orange=True, tag="lit window")
    for (x, y) in ((400, 300), (318, 230), (318, 290)):
        b.place(x, y, orange=(x, y) in TOWER_WALL_ORANGE, tag="tower wall")
    b.place(345, 478, tag="tower foot")
    # ---- under the arches: a moon in each arch's light (blue: as a candidate the middle one decided 47% of losses)
    b.place(170, 452, tag="under the arch")
    b.place(472, 462, tag="under the arch")
    # ---- the sky: moons in the clouds
    for (x, y) in ((180, 80), (250, 130), (160, 170), (520, 70), (500, 170), (700, 330), (105, 120), (440, 215)):
        b.place(x, y, orange=(x, y) in CLOUD_ORANGE, tag="cloud")
    # ---- the Admiral's white castle at the left edge: its pale wall dotted down to the bridge
    for y in (60, 165, 200, 235, 270, 305):
        b.place(100, y, tag="castle wall")
    # ---- the piers between the arches, and the mist below
    for (x, y) in ((300, 425), (606, 425)):
        b.place(x, y, tag="pier")
    for (x, y) in ((110, 505), (290, 505), (600, 505), (700, 505)):
        b.place(x, y, tag="mist")
    b.greens_in_reach()
