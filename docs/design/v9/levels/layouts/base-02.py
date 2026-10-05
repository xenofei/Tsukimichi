"""1-2 "The Cactuar" (The Waking Sands, Minfilia): a creature in outline.

Subject: a cactuar, the running cactus of the Thanalan sands, mid-stride on a dune crest under the full moon (our own
painting, painters/cactuar.py).

Technique: an even ring of moons runs 18 units outside its silhouette (36 apart), so the ball reads its shape before
the first shot. The parts a child would draw first carry the oranges: the spines on its crown, the raised and the
lowered hand, both feet, and the face's three dark holes (two eyes and the open mouth, never green). The two humps of
the middle dune wear crests of brick with a moon over each summit; the far dune and the near crest are dotted lines; a
few stars stand in the open sky.
"""
LEVEL = dict(id="base-02", name="The Cactuar", stage=1, number=2, scene="sagolii-cactuar",
             subject="a cactuar mid-stride on a dune under the full moon",
             technique="a creature in outline")


def build(b):
    # the face first (inside the silhouette), so the ring keeps clear of nothing it should not
    for (x, y) in b.f("eyes") + b.f("mouth"):
        b.place(x, y, r=9, orange=True, green=False, tag="face")
    ring = b.outline("cactuar", offset=18, spacing=36, r=9, tag="outline")
    for name in ("crown", "raised hand", "lowered hand", "feet"):
        for (x, y) in b.f(name):
            b.key(x, y, orange=True, green=False, within=40)
    # every third moon of the ring is a candidate too, so the oranges circle the whole creature
    for k, p in enumerate(ring):
        if k % 3 == 1:
            p["canBeOrange"] = True
    # the middle dune's two humps: crests of brick (concave down), a moon standing over each summit
    b.bricks_along([(104, 392), (140, 381), (180, 374), (220, 372), (262, 376), (300, 385), (330, 395)], length=26, gap=2.5,
                   t=12, tag="dune crest")
    b.bricks_along([(628, 393), (650, 388), (680, 384), (700, 386)], length=26, gap=2.5, t=12, tag="dune crest")
    b.place(220, 342, r=9, orange=True, tag="summit")
    b.place(678, 354, r=9, orange=True, tag="summit")
    b.trace("far dune", spacing=40, r=8, start=40, end_trim=40, orange={1, 4, 13}, tag="far dune")
    b.trace([(96, 466), (150, 484), (250, 498), (300, 499)], spacing=38, r=9, orange={1}, tag="near dune")
    b.trace([(600, 497), (650, 484), (740, 466)], spacing=38, r=9, orange={1}, tag="near dune")
    for k, (x, y) in enumerate([(560, 176), (628, 214), (688, 170), (590, 270), (668, 286), (140, 250), (250, 230)]):
        b.place(x, y, r=8, orange=k in (1, 3), tag="star")
