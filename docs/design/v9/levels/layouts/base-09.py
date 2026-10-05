"""2-4 "The Strait of Merlthor" (Vesper Bay, the twins): terrain in outline, with the ferry's lane.

Subject: the narrow sea the Vesper Bay ferry crosses to Limsa Lominsa: the island of Vylbrand with its smoking volcano,
Thanalan's west coast across the strait, in the game's own world painting "The Three Great Continents" at its
high-resolution size, read as a chart under the moon.

Technique: Vylbrand is drawn as an island in outline, an even ring of moons just off its coast; Thanalan's shore across
the strait is a dotted line; the ferry's lane leaves Vesper Bay, rounds the island's south and comes in to Limsa. Vesper
Bay, Limsa Lominsa and Ul'dah (the road home) are rings of five with their oranges; ship lights ride the open sea to the
west and south, and a few hills of Aldenard stand inland.
"""
from layout import direct_reach  # noqa: E402

LEVEL = dict(id="base-09", name="The Strait of Merlthor", stage=2, number=9, scene="merlthor-strait",
             subject="the strait between Vesper Bay and Limsa Lominsa, and the island of Vylbrand",
             technique="terrain in outline (an island and a coast) with a ferry lane")


def build(b):
    for name, oranges in (("vesper bay", (0, 1, 4)), ("limsa lominsa", (0, 1, 2, 3, 4)), ("uldah", (0, 1, 2, 3, 4))):
        (x, y), = b.f(name)
        b.stop(x, y, R=28, n=5, r=9, oranges=oranges, tag=name)
    ring = b.outline("vylbrand", offset=16, spacing=36, r=9, tag="vylbrand coast")
    reach = [p for p in ring if direct_reach(p["x"], p["y"], 9.0)]
    for k, p in enumerate(reach):
        p["canBeOrange"] = k % 2 == 1
    b.trace("thanalan coast", spacing=36, r=9, orange={2, 6}, tag="thanalan coast")
    b.trace("ferry lane", spacing=34, r=8, orange={2}, tag="ferry lane")
    b.trace("road home", spacing=34, r=8, tag="road home")
    for k, (x, y) in enumerate(b.f("west ships")):
        b.place(x, y, r=9, orange=k == 1, tag="ship")
    for k, (x, y) in enumerate(b.f("south lights")):
        b.place(x, y, r=9, orange=k == 1, tag="ship light")
    for (x, y) in ((420, 300), (600, 420), (520, 410), (230, 490)):
        b.place(x, y, r=9, tag="sea and land")
    for k, (x, y) in enumerate(b.f("aldenard")):
        b.place(x, y, r=9, orange=k in (1, 4), tag="hills")
    b.greens_in_reach()
