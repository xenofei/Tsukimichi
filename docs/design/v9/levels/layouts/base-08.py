"""2-3 "The Kraken's Sea" (Vesper Bay, the twins): a set of points, with an orbit.

Subject: the Rhotano Sea the Vesper Bay ferry sails, as the game's own world painting "The Three Great Continents"
draws its wonders: a kraken wrapped round a ship, a galleon under full sail, a flock of birds in a long skein, a
whirlpool, a mermaid with her harp, and a field of wrecks (read as a chart under the moon).

Technique: each wonder is marked as the chart marks it. The kraken is ringed by nine moons under a crown and two arms
of brick; the galleon's hull is a dotted line (it hangs, so it is never brick) with a moon at each masthead and a
brick for each sail; every bird of the skein is a moon, a set of points like a constellation; the whirlpool is an
orbit, a ring of eight moons turning slowly round its eye (an engine orbit mover); the mermaid's side is a run of
brick with a moon on the harp's crown; the wrecks' masts are bricks; the current runs in crests of brick. The open sea
holds moons and the chart's engraved swell marks (short crowns of brick) in staggered rows. The kraken's ring, the
mastheads, the whirlpool and the harp are never green.

Refit to the Peggle-match rules (9 October 2026): about 100 pieces of pegs r 10 and bricks t 20, a third of them brick,
most pieces orange candidates (`b.candidates`), the lower third kept to its share.
"""
LEVEL = dict(id="base-08", name="The Kraken's Sea", stage=2, number=8, scene="rhotano-wonders",
             subject="the sea's wonders on the world chart: kraken, galleon, birds, whirlpool, mermaid, wrecks",
             technique="a set of points, with an orbit (the whirlpool)")


def _brick(b, make):
    """Lays the brick `make()` draws when it keeps every brick rule, else nothing (the reason goes to `skipped`)."""
    br = make()
    b.bricks.pop()
    for k in range(len(b.tags) - 1, -1, -1):
        if b.tags[k][0] == "brick":
            b.tags.pop(k)
            break
    why = b.brick_why_not(br)
    if why:
        b.skipped.append(((round(br.get("x", br.get("x1", 0))), round(br.get("y", br.get("y1", 0)))), "brick", why))
        return None
    return make()


def build(b):
    import math
    cx, cy, R, _, _ = b.circle("kraken")
    # 1. the wonders' fixed moons: the kraken's ring and eye, the mastheads, the whirlpool's orbit, the harp
    for k in range(9):
        a = math.radians(-90 + 40 * k)
        b.place(cx + R * math.cos(a), cy + R * math.sin(a), tag="kraken", green=False)
    b.place(cx, cy + 6, tag="kraken's eye", green=False, inside=True)
    for (x, y) in b.f("galleon masts"):
        b.place(x, y, tag="masthead", green=False)
    wx, wy, wr = b.features["whirlpool"]
    for k in range(8):
        a = math.radians(45 * k)
        b.peg(wx + (wr + 8) * math.cos(a), wy + (wr + 8) * math.sin(a), tag="whirlpool", green=False,
              move={"kind": "orbit", "x": wx, "y": wy, "period": 20, "clockwise": True})
    for (x, y) in b.f("harp"):
        b.place(x, y - 4, tag="harp", green=False)
    # 2. the wonders' bricks: the kraken's crown and arms, the galleon's sails, the current's crests, the mermaid's
    # side, the wrecks' masts
    for a0, sw in ((212, 36), (252, 36), (292, 36), (338, 28), (174, 28)):
        _brick(b, lambda: b.arc(cx, cy, R + 44, a0, sw, tag="kraken crown"))
    for sp in ((352, 292, 362, 262), (418, 300, 408, 270), (338, 262, 346, 238), (428, 262, 420, 236)):
        _brick(b, lambda: b.line(*sp, tag="galleon sail"))
    cur = b.f("current")
    for (p, q) in ((0, 1), (3, 4), (4, 5)):
        _brick(b, lambda: b.line(cur[p][0], cur[p][1], cur[q][0], cur[q][1], tag="current"))
    mer = b.f("mermaid")
    for (p, q) in ((0, 1), (1, 2), (2, 3)):
        _brick(b, lambda: b.line(mer[p][0], mer[p][1], mer[q][0], mer[q][1], tag="mermaid"))
    for (x, y) in b.f("wrecks"):
        _brick(b, lambda: b.line(x, y + 8, x + 12, y - 28, tag="wreck mast"))
    # the chart's tide pools along both shores: a moon under a crown of brick (hard to strike, so they carry the ramp)
    for xs in ((128, 132, 136), (672, 676, 668)):
        last = -99
        for yc in range(170, 460, 8):
            if yc - last < 62:
                continue
            for x in xs:
                if _brick(b, lambda: b.arc(x, yc, 38, 225, 90, tag="tide pool")):
                    b.place(x, yc + 6, tag="tide pool")
                    last = yc
                    break
    # 4. the chart's tide pools wherever one fits: a moon under a crown of brick (a swell mark), hard to strike
    import random
    keep = b.f("flock") + b.f("wrecks") + b.f("islets") + b.f("galleon hull")
    sites = [(x, yc) for yc in range(150, 470, 9) for x in range(110, 700, 9)
             if all(math.hypot(x - kx, yc - ky) > 36 for (kx, ky) in keep)]
    random.Random(3).shuffle(sites)
    pools = 0
    for (x, yc) in sites:
        if pools >= POOLS or not b.fits(x, yc + 2):
            continue
        peg = b.place(x, yc + 2, tag="tide pool")
        if peg is None:
            continue
        if _brick(b, lambda: b.arc(x, yc, 40, 230, 80, tag="swell mark")):
            pools += 1
        else:
            b.pegs.pop()
            for k in range(len(b.tags) - 1, -1, -1):
                if b.tags[k][0] == "peg":
                    b.tags.pop(k)
                    break
    # 3. the dotted wonders
    b.trace("galleon hull", spacing=34, tag="galleon hull")
    for (x, y) in b.f("flock"):
        b.place(x, y, tag="bird")
    for (x, y) in b.f("wrecks"):
        b.place(x, y, tag="wreck")
    for (x, y) in b.f("islets"):
        b.place(x, y, tag="islet")
    # 5. the open sea: moons between the marks, laid in a fixed shuffled order so the board fills evenly
    import random
    lattice = [(92 + 35 * j + (k % 2) * 17.5, 126 + 31 * k) for k in range(13) for j in range(18)]
    fine = [(x, y) for y in range(126, 496, 11) for x in range(92, 712, 11)]
    random.Random(8).shuffle(lattice)
    random.Random(9).shuffle(fine)
    spots = [s for s in lattice + fine if s[1] <= 494]
    slid = 0
    for (x, y) in spots * 3:
        if len(b.pegs) + len(b.bricks) >= TARGET:
            break
        if b.fits(x, y) and any(math.hypot(q["x"] - x, q["y"] - y) < 40 for q in b.pegs):
            if 140 < y < 380 and len(b.pegs) % 3 == 0 and slid < 12 and b.slide(x, y, 26, 0, 7.0, tag="drift"):
                slid += 1
            else:
                b.place(x, y, tag="open sea")
    # no stray moon: a fill moon (or a tide pool) with no other moon within 40 units of its edge is dropped, with its
    # crown (an isolated peg leaves a ghost disc)
    for k in range(len(b.pegs) - 1, -1, -1):
        p = b.pegs[k]
        tags = [t for kind, t in b.tags if kind == "peg"]
        if tags[k] not in ("open sea", "drift", "tide pool") or p["x"] < 133 or p["x"] > 667:
            continue
        if any(math.hypot(q["x"] - p["x"], q["y"] - p["y"]) - q.get("r", 10) - p.get("r", 10) < 40
               for j, q in enumerate(b.pegs) if j != k and "move" not in q):
            continue
        if tags[k] == "tide pool":
            btags = [t for kind, t in b.tags if kind == "brick"]
            for m, br in enumerate(b.bricks):
                if btags[m] == "swell mark" and abs(br["x"] - p["x"]) < 1 and abs(br["y"] - (p["y"] - 2)) < 1:
                    b.bricks.pop(m)
                    n = -1
                    for t_i, (kind, _t) in enumerate(b.tags):
                        if kind == "brick":
                            n += 1
                            if n == m:
                                b.tags.pop(t_i)
                                break
                    break
        b.pegs.pop(k)
        n = -1
        for m, (kind, _t) in enumerate(b.tags):
            if kind == "peg":
                n += 1
                if n == k:
                    b.tags.pop(m)
                    break
    # nothing green against the jewel's wall bands (F6: a green peg over the region tint reads poorly)
    for p in b.pegs + b.bricks:
        if "kind" not in p and (p["x"] < 140 or p["x"] > 660):
            p["canBeGreen"] = False
    b.greens_in_reach()
    pegs_t = list(zip(b.pegs, [t for t in b.tags if t[0] == 'peg']))
    sea = [p for p, (_k, t) in pegs_t if t in ('open sea', 'tide pool')]
    pegs_t = list(zip(b.pegs, [t for t in b.tags if t[0] == 'peg']))
    sea = [p for p, (_k, t) in pegs_t if t in ('open sea', 'tide pool')]
    pegs_t = list(zip(b.pegs, [t for t in b.tags if t[0] == 'peg']))
    sea = [p for p, (_k, t) in pegs_t if t in ('open sea', 'tide pool')]
    pegs_t = list(zip(b.pegs, [t for t in b.tags if t[0] == 'peg']))
    sea = [p for p, (_k, t) in pegs_t if t in ('open sea', 'tide pool')]
    pegs_t = list(zip(b.pegs, [t for t in b.tags if t[0] == 'peg']))
    sea = [p for p, (_k, t) in pegs_t if t in ('open sea', 'tide pool')]
    pegs_t = list(zip(b.pegs, [t for t in b.tags if t[0] == 'peg']))
    sea = [p for p, (_k, t) in pegs_t if t in ('open sea', 'tide pool')]
    birds = [p for p, (_k, t) in pegs_t if t == 'bird']
    lows = [p for p in sea if p["y"] >= 399]
    keep = [min([p for p in lows if p["x"] < 400], key=lambda p: (p["x"] - 300) ** 2 + (p["y"] - 430) ** 2), min([p for p in lows if p["x"] >= 400], key=lambda p: (p["x"] - 520) ** 2 + (p["y"] - 430) ** 2)]
    left = [p for p, _t in pegs_t if p["y"] >= 399 and p not in keep] + sorted([p for p in sea if p["y"] < 399], key=lambda p: (p["x"] - 400) ** 2 + (p["y"] - 230) ** 2)[:2]
    return b.candidates(exclude=[(p["x"], p["y"]) for p in left], exclude_tags=("current", "wreck", "wreck mast", "islet", "mermaid"))


TARGET = 108
POOLS = 22
SWELL = [(120, 260), (210, 330), (290, 310), (420, 280), (460, 350), (350, 380), (380, 430), (200, 480),
         (640, 150), (460, 230)]


# ---- tuning (written by `mfl.py tune`; held out None/48, tuning seeds 10.7/48, band 5.13-7.33; NOT in band: stuck)
TUNING = {
    "blue": [[259.0, 184.9], [512.0, 118.0], [520.0, 236.0], [488.0, 148.0], [301.0, 247.0], [129.0, 276.0], [471.0, 326.0], [282.0, 281.0]],
    "orange": [[378.0, 302.0], [194.0, 374.0]]
}
