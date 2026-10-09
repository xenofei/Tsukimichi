"""5-3 "Lights on the Water" (Harbour Lights, Merlwyb): light and shadow.

Subject: the Lower Decks' harbour lamps and their long reflections on the basin by night (our own painting,
painters/harbourlights.py). Six lamps stand along the curved back quay and two on the jetty that runs out toward us on
the right; each lays a broken column of light down the water toward us, wavering on the swell.

Technique: the moons ride the light. Each reflection is a dotted column of moons that follows the painted column's
waver from just under the quay's water line down to above the bucket's lane; each lamp wears a moon on its fire (never
green: they are the lights themselves). The quay's sloping west coping and the jetty's deck carry brick runs (both
steeper than a deck); the channel buoy in the harbour mouth wears what fits of a ring of moons (three, between the
centre columns), and a moon sits on each window of the two lit loggias high on the terraces. The oranges ride the
columns' upper reaches, the lamps, two loggia windows, the buoy, a coping brick and two of the jetty's planks.
"""
LEVEL = dict(id="base-23", name="Lights on the Water", stage=5, number=23, scene="harbour-lights",
             subject="the harbour lamps' long reflections on the basin by night",
             technique="light and shadow (pegs on the lamps' reflections)")

LAST_Y = 505.0                # the columns' last moon: clear of the bucket's lane (y 520-560)
# per column: the start along it below the water line, the spacing, and the indices that are orange candidates
COLUMNS = {"Q1": (24, 34, {0, 3}), "Q2": (22, 34, {0, 1, 3}), "Q3": (22, 34, {0, 4}), "Q4": (22, 34, {2}),
           "Q5": (22, 34, {0, 2}), "Q6": (22, 34, {1, 3}), "J1": (22, 34, {0, 1}), "J2": (22, 34, {0, 1}),
           "B": (34, 34, set())}


def build(b):
    for name, (x, y, s) in b.features["lamps"].items():
        if name == "B":
            # the channel buoy's lantern in the harbour mouth wears a ring of moons (the middle of the board)
            b.stop(x, y, R=28, n=6, r=9, oranges=(0, 3), tag="buoy lantern")
        else:
            b.place(x, y, r=9, orange=True, green=False, tag="lamp")
    # the two lit loggias high on the terraces: a moon on each window, an inner window of each a candidate
    for name in ("loggia W", "loggia E"):
        pts = b.f(name)
        for k, (x, y) in enumerate(pts):
            b.place(x, y, r=8, orange=(x, y) == (172.0, 150.0), tag="loggia")
    # the coping's west run and the jetty's deck (both slope 10 degrees or more)
    b.bricks_along([(96, 283), (120, 272), (164, 260), (208, 251)], length=26, gap=2.5, t=10, tag="coping")
    b.bricks_along([(566, 314), (600, 328), (650, 349), (706, 373)], length=26, gap=2.5, t=10, tag="jetty",
                   trim0=0, trim1=0)
    for name, (start, sp, oranges) in COLUMNS.items():
        pts = b.f(f"refl {name}")
        pts = [p for p in pts if p[1] <= LAST_Y + 1]
        b.trace(pts, spacing=sp, r=9, start=start, orange=oranges, tag="reflection")
    # a brick of the coping and two of the jetty's deck are candidates too (the lamps' stone and timber)
    for (x, y) in ((190, 255), (600, 328), (660, 353)):
        b.key(x, y, orange=True, within=20)
    b.greens_in_reach()
