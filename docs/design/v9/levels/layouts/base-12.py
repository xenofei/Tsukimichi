"""3-2 "Above the Cloud Sea" (The Night Skyway, Cid): light and shadow.

Subject: the airship lane's beacons over a moonlit sea of clouds (our own painting, painters/cloudsea.py). The lane runs
straight at the rising moon between two rows of stone beacons, and each lantern throws its beam up into the night; the
two rows' beams fan outward like a crown of light round the lane, and a guide-string of small lights hangs between the
beacons of each row.

Technique: the pieces ride the beams. The nearest two beams of each row, bright and steep, run in brick from clear of
their lantern to a moon where the painted beam fades; the far beams are dotted moons. The nearest lantern of each row
wears a ring of moons and the farther ones a single moon on the lantern (never green: they are the lights
themselves). The guide-strings between the near lanterns hang in brick, the billows' silver-lined crests across the
cloud sea at the lane's foot carry humps of brick (concave down, a few with a moon on the lining), the moon at the
lane's end wears a ring of moons, and stars stand in the night above the beams' tips. Refit to the Peggle-match rules
(9 October 2026): pegs r 10, bricks t 20, every piece a candidate but the crests and what no first flight reaches.
"""
import math

LEVEL = dict(id="base-12", name="Above the Cloud Sea", stage=3, number=12, scene="cloudsea-beacons",
             subject="the airship lane's beacons over a moonlit sea of clouds",
             technique="light and shadow (pegs on the beacons' beams)")

# the lanterns: the nearest of each row wears a ring of moons (radius, count), the far ones a single moon
RINGS = {"L1": (35, 6), "R1": (35, 6)}
# the nearest two beams of each row, bright and steep, run in brick from clear of the lantern to a moon where the beam
# fades: (name, trim at the lantern, trim at the tip, brick length)
BRICK_BEAMS = [("L1", 76, 36, 30), ("R1", 76, 36, 30), ("L2", 40, 36, 30), ("R2", 40, 36, 30)]
# the other beams, dotted moons from clear of the lantern to where the painted beam fades: (name, start, spacing)
BEAMS = [("L3", 36, 35), ("L4", 36, 35), ("R3", 36, 35), ("R4", 36, 35)]
# the guide-strings slung between the lanterns of a row run in brick (the cable): (name, trim at the near lantern,
# trim at the far one, brick length)
STRINGS = [("L1-L2", 92, 22, 26), ("L2-L3", 40, 30, 26), ("R1-R2", 92, 22, 26), ("R2-R3", 40, 30, 26)]
# the beacons' dark towers under their lanterns, the left row (the right mirrors it): (x, y top, y bottom)
TOWERS = []
# the billows' silver-lined crests, the left half and the axis (the right mirrors x < 400): (cx, cy, R, start, sweep)
CRESTS = [(350, 372, 22, 230, 80), (400, 420, 26, 230, 80), (286, 434, 26, 230, 80), (354, 462, 28, 230, 80),
          (400, 504, 26, 230, 80), (290, 500, 28, 230, 80)]
# the crests that carry a moon on their silver lining, 36 above the apex
GLINTS = [(400, 420), (354, 462)]
# moonlit tops of the billows under the near strings, the left half: pegs
TOPS = [(205, 500), (245, 478)]
# the moon at the lane's end wears a ring of moons: (radius, count)
MOON_RING = (50, 8)
# stars above the beams' tips, the left half
STARS = [(130, 136), (190, 110), (250, 96), (300, 150), (160, 70), (230, 150), (100, 90), (290, 60), (215, 60)]


def mirror(x):
    return 800 - x


def build(b):
    heads = b.features["beacons"]["heads"]
    for name, (x, y, s) in heads.items():
        if name in RINGS:
            (R, n) = RINGS[name]
            b.stop(x, y, R=R, n=n, oranges=(), tag="lantern")
        else:
            b.place(x, y, green=False, tag="lantern")
    for (name, t0, t1, L) in BRICK_BEAMS:
        b.bricks_along(f"beam {name}", length=L, gap=2.5, trim0=t0, trim1=t1, tag="beam")
        (x, y) = b.features[f"beam {name}"]["points"][-1]
        b.place(x, y, tag="beam tip")
    for (name, start, sp) in BEAMS:
        b.trace(f"beam {name}", spacing=sp, start=start, tag="beam")
    for (name, t0, t1, L) in STRINGS:
        b.bricks_along(f"string {name}", length=L, gap=2.5, trim0=t0, trim1=t1, tag="string")
    for (x, y0, y1) in TOWERS:
        for xx in {x, mirror(x)}:
            b.line(xx, y0, xx, y1, tag="tower", green=False)
    for (cx, cy, R, a0, sw) in CRESTS:
        for xx in {cx, mirror(cx)}:
            b.arc(xx, cy + R, R, a0, sw, tag="crest")
    for (cx, cy) in GLINTS:
        for xx in {cx, mirror(cx)}:
            b.place(xx, cy - 36, tag="glint")
    for (x, y) in TOPS:
        b.place(x, y, tag="billow")
        b.place(mirror(x), y, tag="billow")
    (mx, my, _mr) = b.features["moon"]
    b.stop(mx, my, R=MOON_RING[0], n=MOON_RING[1], oranges=(), green=True, tag="moon")
    for (x, y) in STARS:
        b.place(x, y, tag="star")
        b.place(mirror(x), y, tag="star")
    b.greens_in_reach()
    # never orange: the billow tops low by the near lanterns and the moon's ring (the sure greens on the lane)
    b.candidates(exclude_tags=["billow", "moon"])


# ---- tuning (written by `mfl.py tune`; held out 4.6/48, tuning seeds 4.4/48, band 3.60-4.70)
TUNING = {
    "blue": [[262.7, 231.0], [310.3, 230.3], [537.3, 231.0], [551.0, 198.8], [489.7, 230.3], [250.0, 96.0], [300.0, 150.0], [500.0, 150.0], [230.0, 150.0], [600.9, 230.3], [518.2, 382.4], [451.0, 372.0], [351.0, 372.0], [400.0, 420.0], [514.0, 434.0], [286.0, 434.0], [355.0, 462.0]],
    "orange": [[450.0, 220.0], [435.4, 255.4], [400.0, 270.0], [364.6, 255.4]],
    "nudge": [[670.0, 136.0, 10, 0], [190.0, 110.0, 10, 0], [215.0, 60.0, -10, 0]]
}
