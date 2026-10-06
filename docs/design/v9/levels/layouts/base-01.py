"""1-1 "Road to Horizon" (The Waking Sands, Minfilia): a trail on a map.

Subject: the road a new adventurer walks out of the Waking Sands, on Western Thanalan's own area map: east through
the Footfalls' oasis to Horizon, south over the bridge at Horizon's Edge to Scorpion Crossing and on to the gate toward
Ul'dah, with the side roads to Crescent Cove, Copperbell Mines and down through the Hammerlea toward the Silver Bazaar.

Technique: each place is a ring of six moons; the roads between are dotted with small moons (r 8) over the gilt road
engraved on the chart, so a cleared board leaves the road behind, and the land's east shore is dotted too. The oranges
are the places on the upper route and the roads between them that a player clears most readily (ranked by `mfl.py
ease`, kept to the spread rule): the three stops low on the chart (Hammerlea, Scorpion Crossing, the east gate) sit in
the bucket's approach and stay blue (round 2, game designer G3), so the first board of the campaign is won on the
route, not lost in the bucket's lane. The land's west
shore is a light dotted edge, two ships' lights ride the sea to the west, and the chart's compass rose is engraved
in the empty desert to the south-west (eight small moons round its heart, so it never reads as a place). The first
level of the campaign: no bricks, wide gaps, every orange within a direct shot.
"""
import math

LEVEL = dict(id="base-01", name="Road to Horizon", stage=1, number=1, scene="thanalan-road-chart",
             subject="the road from the Waking Sands to Horizon and on toward Ul'dah",
             technique="a trail on a map")

CANDS = {(108, 402), (150, 218), (121, 202), (608, 344), (179, 168), (179, 202), (220, 204), (354, 331), (267, 208),
         (267, 240), (275, 288), (296, 191), (325, 208), (325, 240), (376, 236), (422, 238), (451, 221), (480, 238),
         (480, 270), (504, 415), (519, 216), (519, 248), (548, 199), (548, 265), (577, 216), (632, 371), (664, 387),
         (699, 396)}
STOPS = ["waking sands", "footfalls", "crescent cove", "horizon", "copperbell mines", "bridge", "scorpion crossing",
         "east gate", "hammerlea"]


def build(b):
    ORANGES = {"waking sands": (1, 2, 3), "footfalls": (0, 1, 4, 5), "horizon": (0, 1, 5), "hammerlea": (),
               "scorpion crossing": (), "east gate": (), "bridge": (0, 1, 3, 5), "copperbell mines": (0, 1, 5)}
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
    for name, o in (("road main", "every:2"), ("road south", False), ("road east", False), ("road hammerlea", False)):
        b.trace(name, spacing=32, r=8, start=40, end_trim=40, tag=name, orange=o)
    b.trace("road bazaar", spacing=34, r=8, start=38, tag="road bazaar")
    b.trace("west shore", spacing=34, r=8, orange={2, 4}, tag="west shore")
    b.trace("east shore", spacing=36, r=8, orange={2, 3, 4, 5}, tag="east shore")       # the land's eastern edge
    # every first shot meets the chart (game designer m5): the west shore's southern point and two ships' lights
    for (x, y) in ((306, 362), (152, 318), (108, 402)):
        b.place(x, y, r=8, orange=(x, y) == (108, 402), tag="ship's light" if x < 200 else "west shore")
    # the candidates (see the docstring): set last, by place. (533, 398) at the bridge is blue: it was left in a quarter
    # of lost games and tied 1-1 with 1-2 (critic round 4, N9; the critic's swaps break the spread rule, so the road's
    # (220, 204) and the west shore's (354, 331) take its place and (340, 300)'s, picked with `ease` on the tuning seeds)
    for p in b.pegs:
        p["canBeOrange"] = (round(p["x"]), round(p["y"])) in CANDS
