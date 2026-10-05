"""1-1 "Road to Horizon" (The Waking Sands, Minfilia): a trail on a map.

Subject: the road a new adventurer walks out of the Waking Sands, on Western Thanalan's own area map: east through
the Footfalls' oasis to Horizon, south over the bridge at Horizon's Edge to Scorpion Crossing and on to the gate toward
Ul'dah, with the side roads to Crescent Cove, Copperbell Mines and down through the Hammerlea toward the Silver Bazaar.

Technique: each place is a ring of five moons, its three upper moons the oranges; the roads between are dotted with
small moons (r 8) over the gilt road engraved on the chart, so a cleared board leaves the road behind. The land's west
shore is a light dotted edge, and the chart's compass rose stands in the empty desert to the south-west. The first
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
    ORANGES = {"waking sands": (1, 2, 3), "hammerlea": (0, 1), "scorpion crossing": (0, 1), "east gate": (4, 5), "bridge": (0, 4, 5), "compass": ()}
    for name in STOPS:
        (x, y), = b.f(name)
        b.stop(x, y, R=33, n=6, r=9, oranges=ORANGES.get(name, (0, 1, 5)), tag=name)
    # the chart's compass rose: eight points round a heart; blue scenery: it is the chart's, not the road's
    (cx, cy), = b.f("compass")
    for k in range(8):
        a = math.radians(-90 + 45 * k)
        b.place(cx + 46 * math.cos(a), cy + 46 * math.sin(a), r=9, orange=False, tag="compass point")
    b.place(cx, cy, r=11, orange=False, green=False, tag="compass heart")
    for name in ("road copperbell", "road cove"):
        b.trace(name, spacing=32, r=8, start=40, end_trim=40, tag=name)
    # the two main roads carry a few oranges of their own: the walk between the places
    for name in ("road main", "road south", "road east", "road hammerlea"):
        b.trace(name, spacing=32, r=8, start=40, end_trim=40, tag=name, orange="every:2")
    b.trace("road bazaar", spacing=34, r=8, start=38, tag="road bazaar")
    b.trace("west shore", spacing=34, r=8, tag="west shore")
