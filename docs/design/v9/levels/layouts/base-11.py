"""3-1 "The Airship Road" (The Night Skyway, Cid): a trail on a map.

Subject: the airship's lane on the game's own world painting "The Three Great Continents" (its north-west, Aldenard
under the moon): from Gridania north-west over the Coerthas highlands to Ishgard, then north across the coast past the
chart's perched dragon to the chart's own airship sailing the Northern Sea and on west along its wake, with a branch
west to the hold on Coerthas's west coast and the lane in from the south toward Gridania. The cloud sea's western
shore, its floating isles and humps, and the dragons on the wing frame it.

Technique: each city is a ring of six moons (Gridania, Ishgard, the western hold) and the airship's berth is three
moons under its keel with an arc of brick over its sails; the lanes between are dotted with moons over the gilt route
engraved on the chart (the scene's overlays, where the runtime's glints run), and the wake runs on west. The chart round
the lane is drawn in brick: the cloud sea's western shore and the Aldenard west coast as solid runs, one arc on
Coerthas's snowy peak, and the chart's terrain (the cloud humps, Coerthas's ridge, Gyr Abania, the Black Shroud's
canopy, the western highlands) as crowns of three arc bricks, each with a moon over its apex where one fits; moons on
the dragons, the floating isles, the north coast, and the compass rose's heart with two of its rhumb lines.

Refit to the Peggle-match rules (owner's decision, 9 October 2026): pegs r 10 and bricks t 20, 95 pieces (44% brick),
every piece an orange candidate (`b.candidates`) except the compass rose and its rhumb lines, the wake, the flying
dragon and the north coast (the board's sure greens), then tuned (`mfl.py tune`, TUNING below) to stage 3's mean
(charter 1.1: band 3.04-5.24 per 48, mean 4.14). The tuning seeds read this board harder than the fresh ones (3.9
against 5.1 per 48 after `tune`), so the last step was by hand on the held-out and stage blocks pooled (6912 games):
four of the readiest cleared places (Ishgard's south-west moon, the berth's west moon, two of the perched dragon's)
left blue, 58 candidates (61%), held out 4.6, stage block 4.0, pooled 4.15 per 48.
"""
import math

LEVEL = dict(id="base-11", name="The Airship Road", stage=3, number=11, scene="aldenard-airship-lane",
             subject="the airship's lane on the old world chart, from Gridania north over Coerthas past the chart's "
                     "own airship and dragons",
             technique="a trail on a map")

# Left blue (never orange): the bricks whose orange a protanope reads least against the ground round them (F9, the
# build's per-place figures): the west ends of two cloud humps on the brightest cloud, the Black Shroud's canopy over
# the bucket's approach, and Gyr Abania's west end.
HARD = [(613.5, 156.4), (657.5, 276.4), (420.0, 476.0), (399.1, 483.0), (660.0, 215.0), (569.5, 490.8), (599.5, 422.8)]
# (lane, trim at its start, trim at its end)
LANES = (("lane gridania", 46, 46), ("lane north", 46, 10), ("lane west", 46, 46), ("lane south", 10, 0))


def crown(b, cx, cy, R, start, sweep, n, **kw):
    """A crown of n arc bricks whose joints are closed (1.5 units on the centre line, never a notch)."""
    return b.arc_bricks(cx, cy, R, start, sweep, n=n, gap_deg=math.degrees(1.5 / R), **kw)


def build(b):
    # the cities: a ring of moons each round its banner, never green (Ishgard's six, held clear of the peak)
    for name, n, R in (("gridania", 6, 35), ("ishgard", 6, 35), ("western hold", 6, 35)):
        (x, y), = b.f(name)
        b.stop(x, y, R=R, n=n, oranges=(), tag=name)
    # the airship's berth: three moons under its keel, where the lane meets it
    cx, cy, R, a0, sw = b.circle("airship")
    for k in range(3):
        a = math.radians(a0 + sw * k / 2)
        b.place(cx + R * math.cos(a), cy + R * math.sin(a), green=False, tag="airship")
    # the lanes: moons dotted along the gilt route
    for name, s0, s1 in LANES:
        b.trace(name, start=s0, end_trim=s1, tag=name)
    b.trace("wake", tag="wake")
    # the chart round the lane
    for (x, y) in b.f("dragon"):
        b.place(x, y, green=False, tag="dragon")
    for (x, y) in b.f("flying dragon"):
        b.place(x, y, tag="flying dragon")
    for (x, y) in b.f("isles"):
        b.place(x, y, tag="floating isle")
    b.trace("north coast", tag="north coast")
    for (x, y) in b.f("gridania castle"):
        b.place(x, y, tag="gridania castle")
    # the compass rose's heart and two of its rhumb lines
    (cx, cy), = b.f("compass")
    b.place(cx, cy, green=False, tag="compass")
    b.trace("rhumb nw", start=34, tag="rhumb")
    b.trace("rhumb sw", start=34, tag="rhumb")
    # brick: the cloud sea's shore and the west coast as solid runs
    b.bricks_along("cloud shore", length=30, tag="cloud shore")
    b.bricks_along("west coast", length=30, trim0=24, tag="west coast")
    # the airship's envelope: an arc of brick over its sails
    cx, cy, R, a0, sw = b.circle("airship")
    b.arc(cx, cy + 4, 40, 220, 100, tag="airship", green=False)
    # Coerthas: one arc of brick on the snowy peak
    (px, py), = b.f("peak")
    b.arc(px, py + 26, 28, 215, 110, tag="peak", green=False)
    # the chart's terrain as crowns of brick, each with a moon over its apex: the cloud sea's humps east of its shore,
    # Coerthas's ridge toward Gridania, Gyr Abania's ridges, the Black Shroud's canopy and the western highlands
    for name, R, moon in (("cloud humps", 30, False), ("ridges", 34, True), ("gyr ridges", 34, False),
                          ("shroud", 34, True), ("highlands", 34, True)):
        tag = name.rstrip("s")
        for (x, y) in b.f(name):
            crown(b, x, y + 30, R, 215, 110, 3, tag=tag)
            if moon:            # a lone moon over a crown in the open sky prints a ghost disc (the cloud humps, Gyr Abania)
                b.place(x, y + 30 - R - 36, tag=tag)
    b.greens_in_reach()
    b.candidates(exclude=HARD, exclude_tags=["compass", "wake", "rhumb", "flying dragon", "north coast"])


# ---- tuning (written by `mfl.py tune`; held out 4.3/48, tuning seeds 4.4/48, band 4.00-5.20)
TUNING = {
    "blue": [[365.0, 287.0], [395.3, 304.5], [395.3, 339.5], [334.7, 304.5], [288.4, 147.8], [255.0, 160.0], [221.6, 147.8], [324.7, 253.2], [304.1, 223.7], [284.2, 193.7], [242.0, 198.0], [472.0, 168.0], [458.0, 200.0], [516.0, 220.0], [422.0, 228.0], [563.5, 176.7], [559.2, 212.6], [255.0, 72.0], [277.0, 307.0], [522.5, 105.6], [620.0, 416.0], [640.7, 423.0], [440.7, 483.0], [590.0, 484.0], [610.7, 491.0], [107.3, 428.0], [154.3, 481.0], [175.0, 474.0]],
    "orange": [[110.0, 272.0], [469.7, 280.7], [505.5, 284.1], [180.0, 276.0], [154.5, 250.5], [129.1, 225.1], [105.0, 201.0], [107.9, 348.1], [614.5, 155.6], [661.0, 215.0]]
}
