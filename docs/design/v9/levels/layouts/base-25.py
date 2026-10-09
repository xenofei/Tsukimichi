"""5-5 "The Lighthouses of Vylbrand" (Harbour Lights, Merlwyb): a set of points, with orbits (the stage finale).

Subject: the Western La Noscea coast on the game's own area map (`ui/map/s1f4/00/s1f400_m.tex`), read as a
harbourmaster's chart at night: from Swiftperch's jetty past Skull Valley and Aleport's piers to the Crawler's rock and
the east cape, with the Isles of Umbra's corner at the lower left. Each headland's lighthouse is a ring stop of moons
round its lamp (the dress lights it and throws its beams out to sea between the ring's moons); the two great lights,
Aleport's and the east cape's, turn their beams: a pair of moons on an orbit round the lamp (periods 16 and 20 s,
turning opposite ways). The chart's light sectors are dotted arcs over the sea Swiftperch's and Aleport's lights cover,
and a short arc under the east cape's. The coast between the lights is traced: Skull Valley's rim, the shore into
Aleport's harbour wall, the bay's east shore, the east cliff as a brick run, and above it the hills' cliff path and a
contour round the summit at about (605, 225). The ferry lane to the Isles is engraved (glints run on it), with a moon on
each of its islets.

Crop: [740, 720, 1300, 975] on the 2048 map (sources-plan proposed [560, 360, 1360, 1020]; moved down and right so the
headlands sit in the opening rather than under the bucket, and the empty hills take less of the board). It overlaps
FS 5-1's planned crop [40, 1100, 960, 720] by 22% of that crop's area (the rule's 25%).

Difficulty comes from the whole board: the lights' rings and the turning beam ends carry most of it; one candidate sits
low, the Isles light's ring at (156, 431), and low oranges are 2.5% of those left in lost games. 32 candidates (the charter's count), spread to the rule's 10 per 200 x 200. Tuning: seeds 1-864
read 18.8, the second tuning block (3456 games from seed 20001) 17.8, against 17.3 for the variant with the contour's
orange at (632, 235); held out 17.8 (target 18). Stage round 1: 5-3 and 5-4 came out harder after their fixes, which left
the finale gap at 2.82 +- 0.40 (unresolved), so the contour's orange moved from (606, 211) to (632, 235): seeds 1-864
read 17.2 and the second tuning block 17.3 (the baseline 18.8 and 17.7 on the same blocks).
"""
import math

LEVEL = dict(id="base-25", name="The Lighthouses of Vylbrand", stage=5, number=25, scene="vylbrand-lighthouses",
             subject="the coast's lighthouses as ring stops, their beams turning",
             technique="a set of points, with orbits (the beams)")

RING_R, RING_r = 30.0, 8.0          # the lighthouse's ring of moons
BEAM_R, BEAM_r = 61.0, 8.0          # the beam's two ends, turning round the lamp


def lighthouse(b, name, beam=None, start=-90.0, n=6):
    (x, y), = b.f(name)
    if beam:
        period, cw, a0 = beam
        for k in range(2):
            a = math.radians(a0 + 180 * k)
            b.peg(x + BEAM_R * math.cos(a), y + BEAM_R * math.sin(a), r=BEAM_r, green=False,
                  tag=name + " beam", move={"kind": "orbit", "x": x, "y": y, "period": period, "clockwise": cw})
    b.stop(x, y, R=RING_R, n=n, r=RING_r, oranges=(), green=False, start_deg=start, tag=name)


def sector(b, name, n, r=7.0):
    """The light's sector on the chart: a dotted arc of n moons round its lighthouse, over the sea it lights."""
    cx, cy, R, a0, sweep = b.circle(name)
    for k in range(n):
        a = math.radians(a0 + sweep * k / (n - 1))
        b.place(cx + R * math.cos(a), cy + R * math.sin(a), r=r, tag=name)


def build(b):
    lighthouse(b, "aleport light", beam=(16, False, -60))
    lighthouse(b, "east cape light", beam=(20, True, -50))
    lighthouse(b, "swiftperch light")
    lighthouse(b, "crawler's rock light", n=5)
    lighthouse(b, "isles light", n=5)
    sector(b, "swiftperch sector", 5)
    sector(b, "aleport sector", 6)
    sector(b, "east cape sector", 3)
    b.trace("valley rim", spacing=34, r=8, tag="valley rim")
    b.trace("west shore", spacing=34, r=8, tag="west shore")
    b.trace("bay shore", spacing=32, r=8, tag="bay shore")
    b.bricks_along("east cliff", length=30, gap=2.5, tag="east cliff")
    b.trace("cliff road", spacing=34, r=8, tag="cliff road")
    b.trace("high contour", spacing=36, r=8, tag="high contour")
    for (x, y) in b.f("ferry lane"):
        b.place(x, y, r=8, tag="ferry lane")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=6)
    for (x, y) in CLIFF_BRICKS:                                 # the east cliff
        b.key(x, y, orange=True, within=20)
    b.greens_in_reach()


CLIFF_BRICKS = ((498, 315),)
CANDIDATES = (
    (128, 220), (154, 235), (102, 235),                         # Swiftperch's light
    (334, 374), (360, 389), (308, 419), (364, 351),             # Aleport's light and its beam (a mover's home)
    (642, 395), (668, 410), (616, 410), (681, 378),             # the east cape's light and its beam (a mover's home)
    (128, 410), (156, 431),                                     # the Isles' light
    (202, 317), (142, 349), (175, 338), (236, 398),             # the Swiftperch and Aleport sectors
    (235, 212), (302, 224),                                     # Skull Valley's rim
    (274, 301), (339, 308), (423, 339), (459, 390),             # the shore into the harbour, the bay
    (470, 200), (496, 222), (549, 265), (601, 309),             # the cliff path
    (548, 168), (578, 189), (632, 235), (668, 297),             # the contour round the summit
)
