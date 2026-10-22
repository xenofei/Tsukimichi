"""2-2 "Moonpath on the Bay" (Vesper Bay, the twins): light and shadow.

Subject: the moon's road of light on the sea at Vesper Bay, from the horizon to the shore (our own painting,
painters/moonpath.py), with the pier and its lamps, a moored ferry and the town on the shore.

Technique (Peggle-match refit, 9 October 2026): the pegs draw the light. A halo of moons rings the moon and shafts of
brick fall from it toward the horizon (the painting's own shafts); the road itself is a widening V of brick, two edges
opening from the horizon to the shore, with the glints (moons) in rows between them; the horizon, the headland and the
town's roofline are dotted, with brick on the roofs and the headland's slope; the pier's deck, lamps and pilings, the
ferry's hull and masts are traced; wave crests (short arcs of brick) and swell moons fill the dark water either side.
"""
import math

from layout import brick_samples

LEVEL = dict(id="base-07", name="Moonpath on the Bay", stage=2, number=7, scene="vesper-moonpath",
             subject="the moon's road of light on the sea at Vesper Bay",
             technique="light and shadow (pegs on the moon's glints)")

MOON = (292, 128)


def hw(y):
    """Half-width of the V of brick that frames the road (1.5 times the painted glow's half-width)."""
    d = max(0.0, min(1.0, (y - 300.0) / 300.0))
    return 1.9 * (6 + 70 * d ** 1.1)


def try_brick(b, brick, tag):
    """Lay a brick the pre-flight's rules allow (the helper builds it, tests it against what is laid, keeps or drops it)."""
    b.bricks.pop()
    b.tags.pop()
    why = b.brick_why_not(brick)
    if why is None:
        b.bricks.append(brick)
        b.tags.append(("brick", tag))
        return True
    return False


def crest(b, x, y, R=34, start=235, sweep=70, tag="wave crest"):
    return try_brick(b, b.arc(x, y, R, start, sweep, tag=tag), tag)


def build(b):
    axis = 292.0
    # ---- the moon's crown, its halo and shafts
    b.arc_bricks(MOON[0], MOON[1], 64, 185, 90, n=2, tag="moon crown", green=False)
    for deg in (20, 160):
        a = math.radians(deg)
        b.place(MOON[0] + 66 * math.cos(a), MOON[1] + 66 * math.sin(a), r=10, green=False, tag="moon halo")
    for deg, rr in ((60, 104), (90, 104), (120, 104), (60, 174), (120, 174)):
        a = math.radians(deg)
        c, s = math.cos(a), math.sin(a)
        b.line(MOON[0] + (rr - 14) * c, MOON[1] + (rr - 14) * s, MOON[0] + (rr + 14) * c, MOON[1] + (rr + 14) * s,
               tag="moon shaft")
    # ---- the road: the V's two edges in brick, the glints between
    for side in (-1, 1):
        # dashes with gaps wider than the notch limit, so balls pass through the light into the road
        b.bricks_along([(axis + side * hw(336), 336), (axis + side * hw(520), 520)], length=36, gap=2.5, tag="road edge")
    # ---- the headland's slope and the town's roofs in brick
    for (x1, y1, x2, y2) in ((540, 300, 556, 286), (732, 270, 740, 300)):
        try_brick(b, b.line(x1, y1, x2, y2, tag="town roof"), "town roof")
    # ---- the pier's pilings and the ferry's masts: steep runs of brick
    for x in (566, 608, 650, 692):
        try_brick(b, b.line(x, 468, x, 504, tag="piling"), "piling")
    for (x, top, foot) in b.features["masts"]:
        b.bricks_along([(x, top + 24), (x, foot - 30)], length=36, gap=2.5, tag="mast")
    # ---- wave crests in the dark water either side of the road
    for (x, y) in ((120, 365), (210, 350), (160, 440), (95, 505), (200, 480), (420, 380), (500, 365), (462, 452),
                   (380, 480), (520, 500), (110, 420), (230, 420), (450, 330), (380, 430), (180, 320), (370, 345), (140, 380), (330, 400)):
        crest(b, x, y + 30)
    for (y, n) in ((368, 1), (402, 1), (438, 2), (474, 2), (506, 3)):
        for k in range(n):
            b.place(axis + (k - (n - 1) / 2) * 34, y, r=10, green=False, tag="moonpath")
    # ---- horizon, headland, town
    for x in (204, 240, 340, 376, 412, 448, 484, 520):
        b.place(x, 306, r=10, tag="horizon")
    b.trace("headland", spacing=38, r=10, tag="headland")
    b.trace("town", spacing=36, r=10, tag="town")
    # ---- the pier, its lamps and pilings, the ferry
    b.trace("pier deck", spacing=36, r=10, tag="pier deck")
    for (x, y) in b.f("lamps"):
        b.place(x, y - 14, r=10, green=False, tag="pier lamp")
    # ---- streaks of cloud catching the moonlight in the rose sky (short slanted bricks, leaning toward the moon)
    for (x, y, deg) in ((140, 130, 28), (190, 235, 30), (130, 210, -28), (420, 130, -28), (470, 215, -30),
                        (560, 125, -28), (630, 200, -30), (520, 270, -28), (400, 250, -30), (200, 160, 30), (560, 175, 30), (665, 150, -28), (330, 210, 28), (110, 170, -28),
                        (600, 255, 28), (470, 160, 28), (255, 275, 30)):
        a = math.radians(deg)
        c, s_ = 15 * math.cos(a), 15 * math.sin(a)
        if try_brick(b, b.line(x - c, y - s_, x + c, y + s_, tag="cloud streak"), "cloud streak"):
            b.place(x, y + 30, r=10, tag="star")      # a star in the streak's lee
    # ---- swell in the dark water and a field of stars in the sky
    for k, y in enumerate((346, 384, 422, 460, 498)):
        for x in range(100 + (22 if k % 2 else 0), 703, 44):
            b.place(x, y, r=10, tag="swell")
    for k, y in enumerate(range(80, 290, 46)):
        for x in range(100 + (26 if k % 2 else 0), 703, 52):
            if (x, y) not in ((672, 80), (100, 80)) and math.hypot(x - MOON[0], y - MOON[1]) > 62:
                b.place(x, y, r=10, tag="star")
    # the emptiest holes get a last star or swell moon (the largest clear circle first), up to 106 pieces
    spots = [(x, y) for y in range(86, 398, 14) for x in range(100, 704, 14)
             if (y > 130 or 130 < x < 660) and math.hypot(x - MOON[0], y - MOON[1]) > 62]
    while len(b.pegs) + len(b.bricks) < 112:
        pts = [(p["x"], p["y"]) for p in b.pegs] + [q for br in b.bricks for q in brick_samples(br, 6.0)]
        best, bd = None, 0.0
        for (x, y) in spots:
            if not b.fits(x, y, 10):
                continue
            d = min(math.hypot(x - px, y - py) for (px, py) in pts)
            if d > bd:
                best, bd = (x, y), d
        if best is None:
            break
        b.place(best[0], best[1], r=10, tag="star" if best[1] < 300 else "swell")
    b.greens_in_reach()
    # the dull lower water is never orange (low oranges decided lost games: the cheap-difficulty rule)
    ptags = [t for (k, t) in b.tags if k == "peg"]
    low = [(p["x"], p["y"]) for p, t in zip(b.pegs, ptags) if (t == "moonpath" and p["y"] >= 500)]
    return b.candidates(exclude=low, exclude_tags=("pier deck", "piling", "horizon"), within=2.0)


# ---- tuning (written by `mfl.py tune`; held out None/48, tuning seeds 5.6/48, band 4.60-5.80; NOT in band: plays exhausted)
TUNING = {
    "blue": [[292.0, 402.0], [275.0, 438.0], [309.0, 438.0], [275.0, 474.0], [309.0, 474.0], [516.0, 172.0], [422.0, 184.0], [352.0, 170.0], [240.0, 170.0], [235.0, 99.0], [344.0, 218.1], [292.0, 232.0], [240.0, 218.1], [379.5, 279.6], [470.9, 214.5], [560.0, 125.0]],
    "orange": [[240.0, 306.0], [340.0, 306.0], [448.0, 306.0], [484.0, 306.0]]
}
