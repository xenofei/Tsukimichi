"""5-3 "Lights on the Water" (Harbour Lights, Merlwyb): light and shadow.

Subject: the Lower Decks' harbour lamps and their long reflections on the basin by night (our own painting,
painters/harbourlights.py). Six lamps stand along the curved back quay and two on the jetty that runs out toward us on
the right; each lays a broken column of light down the water toward us, wavering on the swell.

Technique: the moons ride the light. Each reflection is a dotted column of moons that follows the painted column's
waver from just under the quay's water line down to above the bucket's lane; the lamps' fires stand clear between
moons at their own height (never green: they hold the light), so each fire shows and six of them flicker in play. The
quay's sloping west coping and the jetty's deck carry brick runs (both steeper than a deck); the channel buoy in the
harbour mouth wears what fits of a ring of moons (three, between the centre columns), and a moon sits on each window
of the two lit loggias high on the terraces. The oranges ride the columns' upper reaches, a flank of every lamp, a
loggia window, two of the buoy's moons, a middle moon of the centre column and two of the jetty's planks.
Round 1 (stage MR 5): the moons came off the lamps' fires (game designer M1); the candidates were re-picked on the
tuning seeds (1-864 and 20001-23456 pooled: 22.54, against 22.71 for the integrated layout), held out 22.9 to 22.0;
the jetty's run ends 24 units from the wall (critic m3: it left a 1.5-unit ledge).
"""
LEVEL = dict(id="base-23", name="Lights on the Water", stage=5, number=23, scene="harbour-lights",
             subject="the harbour lamps' long reflections on the basin by night",
             technique="light and shadow (pegs on the lamps' reflections)")

# the moons flanking the lamps' fires (x, y, candidate): 26 units out, so each fire's light and halo keep their 8 units
# from every piece (F5) and ship as a flickering light; Q3-Q4 and Q5-Q6 share the moon between them, and Q1's west and
# J2's east flanks would pinch the walls
LAMP_FLANKS = [(138, 224, True),                                  # Q1 (112, 224)
               (170, 204, False), (222, 204, True),               # Q2 (196, 204)
               (260, 192, True), (319, 190, True), (378, 188, False),  # Q3 (286, 192), Q3-Q4, Q4 (352, 188)
               (440, 188, True), (497, 190, True), (554, 191, True),  # Q5 (466, 188), Q5-Q6, Q6 (528, 191)
               (586, 273, True), (638, 273, False),               # J1 (612, 273)
               (664, 300, True)]                                  # J2 (690, 300)
LAST_Y = 505.0                # the columns' last moon: clear of the bucket's lane (y 520-560)
# per column: the start along it below the water line, the spacing, and the indices that are orange candidates
COLUMNS = {"Q1": (24, 34, {0}), "Q2": (22, 34, {0, 1, 3}), "Q3": (22, 34, {0, 4}), "Q4": (22, 34, {0, 2}),
           "Q5": (22, 34, {2}), "Q6": (22, 34, {1, 3}), "J1": (22, 34, {0, 1}), "J2": (22, 34, {0, 1}),
           "B": (34, 34, set())}


def build(b):
    # the channel buoy's lantern in the harbour mouth wears a ring of moons (the middle of the board)
    x, y, _s = b.features["lamps"]["B"]
    b.stop(x, y, R=28, n=6, r=9, oranges=(0, 2), tag="buoy lantern")
    # each lamp's fire stands clear (round 1, game designer M1): it is a small light that flickers in play, and the moons
    # flank it at its own height, a moon either side (one shared where two lamps stand close, none against a wall), so
    # the light shows between them; one flank of each lamp is a candidate (never green: they hold the light)
    for (x, y, orange) in LAMP_FLANKS:
        b.place(x, y, r=9, orange=orange, green=False, tag="lamp flank")
    # the two lit loggias high on the terraces: a moon on each window, an inner window of each a candidate
    for name in ("loggia W", "loggia E"):
        pts = b.f(name)
        for k, (x, y) in enumerate(pts):
            b.place(x, y, r=8, orange=(x, y) == (172.0, 150.0), tag="loggia")
    # the coping's west run and the jetty's deck (both slope 10 degrees or more); the jetty's run stops 24 units short of the
    # east wall, a ball's width and more (round 1, critic m3: ending at x 706 it left a 1.5-unit ledge in the corner)
    b.bricks_along([(96, 283), (120, 272), (164, 260), (208, 251)], length=26, gap=2.5, t=10, tag="coping")
    b.bricks_along([(566, 314), (600, 328), (650, 349), (694, 368)], length=26, gap=2.5, t=10, tag="jetty",
                   trim0=0, trim1=0)
    for name, (start, sp, oranges) in COLUMNS.items():
        pts = b.f(f"refl {name}")
        pts = [p for p in pts if p[1] <= LAST_Y + 1]
        b.trace(pts, spacing=sp, r=9, start=start, orange=oranges, tag="reflection")
    # two of the jetty's deck are candidates too (the lamps' timber); the coping's brick gave its candidacy to a middle
    # moon of the centre column (stage MR 5: 5-3 sat only 0.42 per 48 below 5-2, and a step needs 0.5 resolved)
    for (x, y) in ((600, 328), (660, 353), (461, 371)):
        b.key(x, y, orange=True, within=20)
    b.greens_in_reach()
