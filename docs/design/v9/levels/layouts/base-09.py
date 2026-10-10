"""2-4 "The Ferry Under Sail" (Vesper Bay, the twins): a vessel's rigging over a moving sea.

Subject: the Vesper Bay ferry mid-strait by night under full sail, bound for Limsa Lominsa, the far coasts low on either
side (our own painting, painters/ferry.py).

Technique (refitted to the Peggle-match rules, 9 October 2026): the ship is drawn the way a child draws one, by its
lines. Each mast is a run of brick (the pennants fly beside them, never green); each sail's lee edge is a brick bellied
with the wind and its windward yard end a moon; the jib's stay and the bowsprit are bricks; the gunwale is a dotted
line from the stern lantern to the bow lantern with the lit windows staggered beneath it; the stern and the bow are
bricks. Over the horizon the two far coasts are crowns of brick, the moon wears a ring of moons, Vesper the evening
star wears another, and the Twin Lamps (a constellation, painted) are moons joined by bricks. Below the hull the swell
moves: rows of moons slide east and back on the engine's clock (slide movers), each broken into runs so they read as
waves.

Difficulty (stage mr2, band 5.13-7.33 per 48): what makes the board hard is where the oranges may fall. Moons stand
down both side walls (the "rim"), two sit in the sails' channels between mast and lee edge (the "pockets"), and the low
swell rows are candidates, so a deal often leaves one in a place a bounce reaches only by luck; the open-sky stars and
the first stars of the sea stay blue. Held out 7.0/48 on 1,728 fresh games.
"""
import math

LEVEL = dict(id="base-09", name="The Ferry Under Sail", stage=2, number=9, scene="ferry-under-sail",
             subject="the Vesper Bay ferry under full sail in the strait at night",
             technique="a vessel's rigging over a moving sea (slide movers)")

DECK = [(244, 386), (290, 396), (340, 401), (400, 404), (470, 402), (540, 396), (590, 386)]


def deck_y(x):
    for (x0, y0), (x1, y1) in zip(DECK[:-1], DECK[1:]):
        if x0 <= x <= x1:
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return DECK[0][1] if x < DECK[0][0] else DECK[-1][1]


def mast(b, x, top, foot, n, tag):
    """A mast: n bricks of equal length down x from top to foot."""
    gap = 2.5
    each = (foot - top - gap * (n - 1)) / n
    for k in range(n):
        y0 = top + k * (each + gap)
        b.line(x, y0, x, y0 + each, tag=tag)


def link(b, p, q, trim, tag):
    """A brick between two stars, trimmed `trim` units off each end."""
    (x0, y0), (x1, y1) = p, q
    d = math.hypot(x1 - x0, y1 - y0)
    ux, uy = (x1 - x0) / d, (y1 - y0) / d
    b.line(x0 + ux * trim, y0 + uy * trim, x1 - ux * trim, y1 - uy * trim, tag=tag)


def build(b):
    # ---- the masts and their pennants
    mast(b, 340, 204, 362, 4, "mast")
    mast(b, 470, 200, 362, 4, "mast")
    for (x, y) in ((336, 164), (478, 164)):
        b.place(x, y, orange=True, green=False, tag="pennant")
    # ---- the sails: the lee edge of each in brick, the windward yard end a moon
    for (x0, y0, x1, y1) in ((390, 224, 390, 274), (412, 312, 412, 358), (522, 214, 522, 258), (540, 292, 540, 346)):
        b.line(x0, y0, x1, y1, tag="leech")
    for (x, y) in ((306, 224), (290, 304), (436, 210), (290, 350)):
        b.place(x, y, tag="yard")
    # ---- the jib's stay and the bowsprit's tip
    b.line(580, 262, 606, 292, tag="stay")
    b.place(646, 378, tag="bowsprit")
    # ---- the hull: stern and bow in brick, the gunwale dotted with the lit windows staggered beneath it
    b.line(244, 408, 266, 436, tag="stern")
    b.line(598, 404, 578, 430, tag="bow")
    for i in range(9):
        x = 280 + 38 * i
        b.place(x, deck_y(x) - 2, orange=i in (2, 5), tag="gunwale")
    for i in range(8):
        x = 299 + 38 * i
        b.place(x, deck_y(x) + 28, tag="window")
    for (x, y) in ((246, 372), (612, 362)):
        b.place(x, y, orange=True, green=False, tag="lantern")
    # ---- the wake and the bow's spray
    b.trace("wake", spacing=42, tag="wake")
    for (x, y) in ((646, 404), (672, 432)):
        b.place(x, y, tag="spray")
    # ---- the moon and its ring
    b.stop(150, 104, 46, 8, oranges={1, 3, 5, 7}, green=False, start_deg=-90.0, tag="moon ring")
    # ---- Vesper, the evening star, and its ring
    vx, vy = b.f("vesper")[0]
    b.place(vx, vy, orange=True, green=False, tag="vesper")
    b.stop(vx, vy, 38, 6, oranges={0, 3}, green=False, start_deg=-90.0, tag="vesper ring")
    # ---- the Twin Lamps: five stars, four bricks between them
    stars = [tuple(p) for p in b.f("twins")]
    for s in stars:
        b.place(s[0], s[1], orange=True, green=False, tag="twin lamp")
    for (i, j) in ((0, 1), (1, 2), (1, 3), (1, 4)):
        link(b, stars[i], stars[j], 38, "twin line")
    # ---- the thin cloud: streaks of brick across the sky
    for (x0, y0, x1, y1) in ((226, 150, 262, 136), (100, 190, 134, 206)):
        b.line(x0, y0, x1, y1, tag="cloud")
    # ---- the far coasts: crowns of brick
    b.arc_bricks(145, 420, 100, 245, 50, n=1, tag="west coast")
    b.arc_bricks(680, 406, 74, 253, 34, n=1, tag="east coast")
    # ---- the swell's long crests, in brick, in the corners of the sea
    b.arc_bricks(125, 566, 68, 252, 36, n=1, tag="crest")
    b.arc_bricks(672, 566, 68, 252, 36, n=1, tag="crest")
    # ---- the jib's belly
    for (x, y) in ((576, 336), (600, 352)):
        b.place(x, y, tag="jib")
    # ---- stars in the open sky and over the water
    for (x, y) in ((96, 262), (96, 150), (300, 140), (250, 70), (210, 100), (700, 210), (660, 300), (700, 380), (240, 330),
                   (190, 378), (170, 412), (620, 150), (700, 300), (560, 250), (640, 400)):
        b.place(x, y, tag="star")
    for (x, y) in ((700, 130), (700, 170), (700, 250), (700, 296), (100, 236), (100, 272), (250, 52), (320, 52)):
        b.place(x, y, tag="rim")
    for (x, y) in ((376, 330), (506, 322)):
        b.place(x, y, tag="pocket")
    for (x, y) in ((100, 400), (108, 446), (676, 432)):
        b.place(x, y, tag="buoy")
    b.line(590, 226, 626, 238, tag="cloud")
    b.line(244, 184, 274, 196, tag="cloud")
    # ---- the swell: rows sliding east and back, each broken into runs (slide movers)
    for (y, runs, dx, period) in ((470, ((258, 420), (528, 690)), 30, 7.0), (504, ((210, 330), (420, 520), (590, 640)), -30, 9.0)):
        for (x0, x1) in runs:
            for x in range(x0, x1 + 1, 46):
                b.slide(x, y, dx, 0, period, tag="swell")
    b.greens_in_reach()
    return b.candidates(exclude_tags=("star", "spray", "wake"))


# ---- tuning (written by `mfl.py tune`; held out None/48, tuning seeds 6.9/48, band 3.40-4.80; NOT in band: stuck)
TUNING = {
    "blue": [[336.0, 164.0], [436.0, 210.0], [337.0, 428.7], [304.0, 470.0], [350.0, 470.0], [340.0, 222.8], [470.0, 220.3], [390.0, 250.0], [522.0, 236.0], [244.9, 142.6], [117.0, 198.0], [259.0, 190.0]],
    "orange": [[700.0, 210.0], [700.0, 380.0], [240.0, 330.0], [190.0, 378.0]],
    "nudge": [[170.0, 412.0, 10, 0]]
}
