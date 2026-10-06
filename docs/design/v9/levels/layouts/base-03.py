"""1-3 "The Cactuar" (The Waking Sands, Minfilia): a creature in outline.

Subject: a cactuar, the running cactus of the Thanalan sands, mid-stride on a dune crest under the full moon (our own
painting, painters/cactuar.py).

Technique: an even ring of moons runs 18 units outside its silhouette (36 apart), so the ball reads its shape before
the first shot. The parts a child would draw first carry the oranges: the spines on its crown, the raised and the
lowered hand, and the face's three dark holes (two eyes and the open mouth, never green). The two humps of
the middle dune wear crests of brick with a moon over each summit; the far dune and the near crest are dotted lines; a
few stars stand in the open sky.
"""
LEVEL = dict(id="base-03", name="The Cactuar", stage=1, number=3, scene="sagolii-cactuar",
             subject="a cactuar mid-stride on a dune under the full moon",
             technique="a creature in outline")


def build(b):
    # the silhouette's interior stays open (critic C1): only its features stand inside it
    b.subject("cactuar")
    for (x, y) in b.f("eyes") + b.f("mouth"):
        b.place(x, y, r=9, orange=True, green=False, inside=True, tag="face")
    # the crown: one moon over the middle spine, clear of the tips (game designer m3: on the tip it read as horns)
    b.place(458, 168, r=9, orange=True, green=False, tag="crown")
    ring = b.outline("cactuar", offset=18, spacing=36, r=9, tag="outline")
    for name in ("raised hand", "lowered hand"):
        for (x, y) in b.f(name):
            b.key(x, y, orange=True, green=False, within=40)
    # two in every five moons of the ring are candidates too, so the oranges circle the whole creature
    for k, p in enumerate(ring):
        if (k % 5 in (1, 3) and p["y"] < 430) or (k % 5 == 0 and 330 < p["y"] < 430 and p["x"] < 450):
            p["canBeOrange"] = True
    # the feet stand on the near crest, at the bucket's approach: blue, never green (an orange there is cheap)
    for (x, y) in b.f("feet"):
        b.key(x, y, orange=False, green=False, within=40)

    # the middle dune's western hump: its two slopes in brick (13 and 16 degrees: a ball runs off them), its rounded top
    # a moon, and a moon standing over the summit (critic C2: the level apex held balls)
    b.bricks_along([(104, 394), (140, 382), (180, 372)], length=26, gap=2.5, t=12, tag="dune crest")
    b.bricks_along([(270, 378), (300, 385), (330, 395)], length=26, gap=2.5, t=12, tag="dune crest")
    b.place(225, 366, r=9, orange=True, tag="dune crest")
    b.place(222, 328, r=9, orange=True, tag="summit")
    # the eastern hump is low and nearly level: dotted, with its summit moon
    b.trace([(616, 394), (700, 386)], spacing=34, r=9, orange=True, tag="dune crest")
    b.place(664, 348, r=9, orange=True, tag="summit")
    b.trace("far dune", spacing=40, r=8, start=40, end_trim=40, orange={1, 2, 4, 6, 12}, tag="far dune")
    b.trace([(96, 466), (150, 484), (250, 498), (300, 499)], spacing=38, r=9, tag="near dune")
    b.trace([(600, 497), (650, 484), (740, 466)], spacing=38, r=9, tag="near dune")
    for k, (x, y) in enumerate([(560, 176), (628, 214), (688, 170), (590, 270), (668, 286), (140, 250), (250, 230)]):
        b.place(x, y, r=8, orange=k in (0, 3, 4, 5, 6), tag="star")
    for (x, y) in ((320, 170), (200, 180)):       # stars over the crown and the west sky (placed last)
        b.place(x, y, r=8, tag="star")
