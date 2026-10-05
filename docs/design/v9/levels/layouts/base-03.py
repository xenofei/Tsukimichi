"""1-3 "Ul'dah Across the Sands" (The Waking Sands, Minfilia): a silhouette band.

Subject: Ul'dah's skyline as the road from the Waking Sands first shows it: the two gilt towers, a white dome with its
spire, the low roofs of the lower town, in the game's own Thanalan painting (its western half), mirrored so its light
comes from the upper left.

Technique: the skyline is traced along its roofline. Dotted moons run up the towers' outer edges (a light stroke, 16
units clear of the stone, so the gilt shows between them), a crown of brick sits on the white dome with its spire as a
short vertical run, a second short crown on the eastern domes, and the lower town's roofs are a dotted line along the
foot. The open sky to the east holds the evening's first stars. Greens arrive here (level 3): the spire and the
towers' crowns stay orange or blue, never green.
"""
LEVEL = dict(id="base-03", name="Ul'dah Across the Sands", stage=1, number=3, scene="uldah-gilt-towers",
             subject="Ul'dah's skyline: the gilt towers and the white dome",
             technique="a silhouette band (the roofline traced)")


def build(b):
    x, y, r, a0, sw = b.circle("white dome")
    b.arc_bricks(x, y, r, a0, sw, n=2, t=12, tag="white dome crown")
    x, y, r, a0, sw = b.circle("east domes")
    b.arc_bricks(x, y, r, a0, sw, n=1, t=10, tag="east domes crown")
    b.trace("spire", spacing=38, r=9, orange=True, green=False, tag="spire")
    b.trace("tower a west", spacing=36, r=9, orange={0, 3}, tag="tower a")
    b.trace("tower a east", spacing=36, r=9, orange={0, 3}, tag="tower a")
    b.trace("tower b east", spacing=36, r=9, orange={0, 3}, tag="tower b")
    for p in b.pegs[:]:
        if p["y"] < 215 and p.get("canBeOrange"):
            p["canBeGreen"] = False                      # the towers' crowns
    b.trace("far spires", spacing=36, r=9, orange={1, 4}, tag="far spires")
    b.trace("east spire", spacing=36, r=9, orange={1, 3}, tag="east spire")
    b.trace("lower roofs", spacing=36, r=9, orange="every:2", tag="lower roofs")
    b.trace("lower roofs 2", spacing=50, r=9, orange="every:2", tag="lower roofs")
    for k, (x, y) in enumerate(b.f("evening stars")):
        b.place(x, y, r=8, orange=k in (0, 1, 2, 3, 4, 5, 6), tag="evening star")
