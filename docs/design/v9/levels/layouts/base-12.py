"""3-2 "Above the Cloud Sea" (The Night Skyway, Cid): light and shadow.

Subject: the airship lane's beacons over a moonlit sea of clouds (our own painting, painters/cloudsea.py). The lane runs
straight at the rising moon between two rows of stone beacons, and each lantern throws its beam up into the night; the
two rows' beams fan outward like a crown of light round the lane, and a guide-string of small lights hangs between the
beacons of each row.

Technique: the moons ride the beams. Each beam is a dotted line of moons from just above its lantern to where the
painted beam fades; the nearest lantern on each side wears a ring of moons and the farther ones a single moon on the
lantern (never green: they are the lights themselves). The guide-strings are dotted between the lanterns, the billows'
silver-lined crests in front carry short humps of brick, concave down, and a few stars stand in the night above the
beams' tips. The oranges ride the beams (and the two far lanterns, and a star either side), never the low rings.
"""
LEVEL = dict(id="base-12", name="Above the Cloud Sea", stage=3, number=12, scene="cloudsea-beacons",
             subject="the airship lane's beacons over a moonlit sea of clouds",
             technique="light and shadow (pegs on the beacons' beams)")

# per beam: the start along it (clear of the lantern's ring) and the spacing
BEAMS = {"L1": (68, 36), "L2": (34, 36), "L3": (34, 34), "L4": (34, 32),
         "R1": (68, 36), "R2": (34, 36), "R3": (34, 34), "R4": (34, 32)}
# the guide-strings between the lanterns of a row: (name, start, end trim)
STRINGS = [("L1-L2", 40, 30), ("L2-L3", 30, 30), ("L3-L4", 30, 30), ("R1-R2", 40, 30), ("R2-R3", 30, 30),
           ("R3-R4", 30, 30)]
# the billows' silver-lined crests: (cx, cy, R, start, sweep)
CRESTS = [(330, 488, 34, 225, 90), (470, 500, 34, 225, 90), (196, 520, 30, 230, 80), (604, 520, 30, 230, 80),
          (400, 452, 30, 230, 80), (352, 404, 26, 230, 80), (448, 404, 26, 230, 80)]
# stars above the beams' tips
STARS = [(130, 136), (190, 110), (250, 96), (300, 150), (500, 150), (550, 96), (610, 110), (670, 136)]
# the orange candidates, the left half (the right mirrors it): the beams' moons, the far lanterns and a star
CANDIDATES = [(118, 224), (124, 295), (133, 403), (195, 214), (212, 284), (228, 355), (251, 203), (278, 266),
              (291, 297), (312, 233), (331, 261), (350, 289), (300, 150), (304, 329), (352, 404)]


def build(b):
    heads = b.features["beacons"]["heads"]
    for name, (x, y, s) in heads.items():
        if s > 0.8:
            b.stop(x, y, R=34, n=6, r=9, oranges=(), tag="lantern")
        else:
            b.place(x, y, r=9, green=False, tag="lantern")
    for name, (start, sp) in BEAMS.items():
        b.trace(f"beam {name}", spacing=sp, r=9, start=start, tag="beam")
    for (name, start, trim) in STRINGS:
        b.trace(f"string {name}", spacing=34, r=8, start=start, end_trim=trim, tag="string")
    for (cx, cy, R, a0, sw) in CRESTS:
        b.arc(cx, cy + R, R, a0, sw, t=10, tag="crest")
    for (x, y) in STARS:
        b.place(x, y, r=8, tag="star")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=6)
        b.key(800 - x, y, orange=True, within=6)
    b.greens_in_reach()
