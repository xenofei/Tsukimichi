"""1-1 "Road to Horizon" (The Waking Sands, Minfilia): a trail on a map.

Subject: the road a new adventurer walks out of the Waking Sands, on Western Thanalan's own area map: east through
the Footfalls' oasis to Horizon, south over the bridge at Horizon's Edge to Scorpion Crossing and on to the gate toward
Ul'dah, with the side roads to Crescent Cove, Copperbell Mines and down through the Hammerlea toward the Silver Bazaar.

Technique: each place is a ring of six moons, the members a ball meets first its oranges; the roads between are dotted with
small moons (r 8) over the gilt road engraved on the chart, so a cleared board leaves the road behind. The land's west
shore is a light dotted edge, two ships' lights ride the sea to the west, and the chart's compass rose is engraved
in the empty desert to the south-west (eight small moons round its heart, so it never reads as a place). The first
level of the campaign: no bricks, wide gaps, every orange within a direct shot.
"""
import math

LEVEL = dict(id="base-01", name="Road to Horizon", stage=1, number=1, scene="thanalan-road-chart",
             subject="the road from the Waking Sands to Horizon and on toward Ul'dah",
             technique="a trail on a map")

STOPS = ["waking sands", "footfalls", "crescent cove", "horizon", "copperbell mines", "bridge", "scorpion crossing",
         "east gate", "hammerlea"]


def build(b):
    # each ring's oranges are the members a falling ball meets first; the Waking Sands' top moon sits at the edge of
    # every direct flight, so its oranges are its right side and foot
    ORANGES = {"waking sands": (1, 2, 3), "footfalls": (0, 1, 4, 5), "horizon": (0, 1, 2, 5), "hammerlea": (0, 1),
               "scorpion crossing": (0, 1), "east gate": (0, 5), "bridge": (0, 4, 5), "copperbell mines": (0, 1, 2, 5)}
    for name in STOPS:
        (x, y), = b.f(name)
        b.stop(x, y, R=33, n=6, r=9, oranges=ORANGES.get(name, (0, 1, 5)), tag=name)
    # the chart's compass rose, engraved rather than a place (critic L1): eight small r 7 moons round its heart, blue
    # scenery: it is the chart's, not the road's
    (cx, cy), = b.f("compass")
    for k in range(8):
        a = math.radians(-90 + 45 * k)
        b.place(cx + 44 * math.cos(a), cy + 44 * math.sin(a), r=7, orange=False, tag="compass point")
    b.place(cx, cy, r=9, orange=False, green=False, tag="compass heart")
    for name in ("road copperbell", "road cove"):
        b.trace(name, spacing=32, r=8, start=40, end_trim=40, tag=name)
    # the two main roads carry a few oranges of their own: the walk between the places
    for name in ("road main", "road south", "road east", "road hammerlea"):
        b.trace(name, spacing=32, r=8, start=40, end_trim=40, tag=name, orange="every:2")
    b.trace("road bazaar", spacing=34, r=8, start=38, tag="road bazaar")
    b.trace("west shore", spacing=34, r=8, tag="west shore")
    # every first shot meets the chart (game designer m5): the west shore's southern point and two ships' lights
    for (x, y) in ((306, 362), (152, 318), (108, 402)):
        b.place(x, y, r=8, tag="ship's light" if x < 200 else "west shore")
