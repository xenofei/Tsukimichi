"""4-4 "The Coliseum" (The Sunlit Steps, Raubahn): a lattice.

Subject: the Coliseum's lower floor as the game's own Steps of Thal map draws it (`ui/map/w1t2/02/w1t202_m.tex`,
sources-plan.md's crop [560, 640, 960, 720], read from the install and never shipped): the arena's sunken sands, its
inner and outer tiers and its wings (the west corridor and hall, the south-west corridor, the Royal Promenade, the east
wing), over the faint ring and spokes of the city above. The map's three red exit arrows are erased.

Technique (LA, rings and spokes of brick): the board is a wheel centred on the sands.
- Rings: the inner tier (R 56) and the outer tier (R 90, the map's pink tier, its east side dotted) are crowns of brick
  over the sands (never green), with a third, the stands (R 125), under the launcher. Each is opened at its keystone,
  one slot over the next, so a ball falls through to the sands (a moon on the pit) instead of resting on a flat apex.
  The city ring (R 210 round its own centre) has crowns of brick on its upper flanks, clear of the launcher, and is
  dotted round its lower half; moons on its inner face under the crowns and on its outer face down the flanks give the
  ring the map's thick band.
- Spokes: the wings and the Promenade are runs of brick along the map's own corridors, and three spokes of the wheel
  (north-west, north-east, south-east) run from the stands toward the city ring. The recipe engraves the rings and six
  spokes in gilt under the pieces, so the lattice reads at 640 even as the pieces clear.

Candidates (31): the sands, the inner walk, the outer tier's east moons and its west crown, the city ring's flanks and
faces, the stands' south-west moon, a brick of the north-west spoke, the city's crowns, the inner tier's west brick,
the south-east spoke and the Promenade, and the west city spoke. Three candidates sit at y 430 or lower (the city
ring's (253, 445), the stands' (319, 439) and the south-east spoke's brick); low oranges are about 6% of those left in
lost games, and no holdout passes 25%. No candidate sits against a wall: the
outer-face moon at (624, 231), as a candidate, decided 36-42% of lost games and is blue; two blue moons on the outer
face above the flanks' columns (202 and 338 degrees, and 328) shield them instead.

Tuning (seeds 1-864 and 20001-23456): 21.7 and 21.5 per 48 toward the charter's 22; held out 22.3. The stands' slot
is 46 units (its ends slope about 10 degrees): at 30 a slow ball rested on its near-flat ends, and the inner tier's
unopened apex under the two slots held balls too (play's stuck fires fell from 138 to 58 per 1728 on the tuning seeds).
"""
import math

LEVEL = dict(id="base-19", name="The Coliseum", stage=4, number=19, scene="uldah-coliseum",
             subject="the arena's tiers as rings and spokes of brick, Raubahn's old ground",
             technique="a lattice (rings and spokes of brick)")

ARENA = (389, 318)


def on(cx, cy, R, deg):
    a = math.radians(deg)
    return cx + R * math.cos(a), cy + R * math.sin(a)


def ring_pegs(b, cx, cy, R, degs, oranges=(), tag="", green=True, r=10.0):
    out = []
    for d in degs:
        x, y = on(cx, cy, R, d)
        out.append(b.place(x, y, r=r, orange=d in oranges, green=green, tag=tag))
    return out


def build(b):
    ax, ay = ARENA
    # ---- bricks first (so every moon is placed against them)
    # the arena's tiers: the inner tier's crown over the pit, the outer tier's from the north-west round to the east,
    # and the stands' crown under the launcher
    cx, cy, R, a0, sw = b.circle("inner tier")
    b.arc_bricks(cx, cy, R, a0, sw, n=2, gap_deg=math.degrees(30.0 / R), t=10, green=False, tag="inner tier")
    cx, cy, R, a0, sw = b.circle("outer tier")
    slot = math.degrees(30.0 / R)                   # opened at its keystone, under the stands' own slot
    b.arc_bricks(cx, cy, R, 225, 45 - slot / 2, n=1, t=10, green=False, tag="outer tier")
    b.arc_bricks(cx, cy, R, 270 + slot / 2, 75 - slot / 2, n=2, t=10, green=False, tag="outer tier")
    b.arc_bricks(ax, ay, 125, 249, 42, n=2, gap_deg=math.degrees(46.0 / 125), t=10, tag="stands")
    # the city ring: crowns of brick on its upper flanks, clear of the launcher
    cx, cy, R, _, _ = b.circle("city ring")
    b.arc_bricks(cx, cy, R, 186, 38, n=3, t=10, tag="city ring")
    b.arc_bricks(cx, cy, R, 316, 38, n=3, t=10, tag="city ring")
    # the spokes: the arena's wings and the Royal Promenade, and three spokes of the wheel
    b.bricks_along("promenade", length=26, t=10, tag="promenade")
    b.bricks_along("west corridor", length=24, t=10, tag="west corridor")
    b.bricks_along("west hall", length=32, t=10, tag="west hall")
    b.bricks_along("south-west corridor", length=22, t=10, tag="south-west corridor")
    b.bricks_along("east wing", length=24, t=10, tag="east wing")
    for s in ("spoke north-west", "spoke north-east", "spoke south-east"):
        b.bricks_along(s, length=22, t=10, tag=s)
    # ---- moons
    px, py, _ = b.features["pit"]["circle"]
    b.place(px, py, r=12, orange=True, green=False, tag="the sands")
    cx, cy, R, _, _ = b.circle("inner tier")
    ring_pegs(b, cx, cy, R, (20, 56), oranges=(20, 56), tag="inner tier walk")
    cx, cy, R, _, _ = b.circle("outer tier")
    ring_pegs(b, cx, cy, R, (10, 30, 125), oranges=(10, 125), tag="outer tier east")
    cx, cy, R, _, _ = b.circle("city ring")
    ring_pegs(b, cx, cy, R, (8, 19, 30, 41, 52, 63, 74, 85, 96, 107, 118, 129, 140, 151, 162, 173),
              oranges=(8, 19, 41, 129, 151, 173), tag="city ring")
    # the city ring's two faces: its inner face under the crowns, its outer face down the flanks
    ring_pegs(b, cx, cy, 178, (194, 206, 218, 230, 310, 322, 334, 346), oranges=(206, 310, 322, 334, 346),
              tag="city ring inner face")
    ring_pegs(b, cx, cy, 244, (202, 192, 182, 172, 162, 152, 142, 8, 18, 28, 38, 348, 338, 328),
              oranges=(192, 182, 172, 162, 152, 18, 28),
              tag="city ring outer face")
    ring_pegs(b, ax, ay, 140, (12, 72, 120, 132, 228), oranges=(120,), tag="stands")
    for (x, y) in ((104, 292), (690, 320), (100, 246), (692, 272)):
        b.place(x, y, orange=(x, y) == (104, 292), tag="city spoke")
    # ---- orange bricks: the stands' crown, the outer tier's crown, a brick of each spoke and of the city's crowns
    brick_tags = [t[1] for t in b.tags if t[0] == "brick"]
    for tag, ks in (("outer tier", (0,)), ("spoke north-west", (2,)),
                    ("inner tier", (0,)),
                    ("spoke south-east", (1,)), ("city ring", (1, 4)), ("promenade", (1,))):
        bricks = [br for br, t in zip(b.bricks, brick_tags) if t == tag]
        for k in ks:
            bricks[k]["canBeOrange"] = True
    b.greens_in_reach()
