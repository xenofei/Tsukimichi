"""4-3 "Lamps on the Steps" (The Sunlit Steps, Raubahn): light and shadow.

Subject: a stair of lamp glows climbing a dark terrace (our own painting, painters/lampsteps.py). The great sandstone
terrace below the Steps of Nald stands in the night, and a ceremonial stair zigzags up its face in three balustraded
flights; brass lanterns on the newel posts, at each landing and along each flight, throw pools of warm light on the
stone, so the stair reads as a chain of light climbing the dark. A doorway glows between the first two flights, a
bracket lamp burns over the upper landing between crimson banners, and the city's domes stand over the parapet.

Technique (LS): the moons stand in the pools of light. Each newel lantern wears a ring of six moons round its flame
(the pool) and a moon on the flame itself (never green: the lamps are the subject's own lights); the handrails between
the lanterns are dotted, so the zigzag of the stair is drawn whole; the doorway's arch is crowned by two bricks open at
the keystone with its hanging lantern a moon; the bracket lamp has its own ring; the two parapets' copings are dotted
along the top terrace. The oranges stand in the pools and on the upper handrails.

Built (MR 4): 86 pieces, 29 candidates, held out 23.6 per 48 (charter 23.5); seeds 1-864 put the chosen set at 23.6
and the second tuning block (seeds 20001-23456) at 23.5. The lantern 2a stands at x 348 rather than 318 so its ring's
right-hand moons meet the near-vertical shots just left of the launcher's axis (14 dead first shots otherwise). The
lower handrail's moons and the lowest rings' are the holdouts (at most 25% of lost games, all above y 430).
"""
LEVEL = dict(id="base-18", name="Lamps on the Steps", stage=4, number=18, scene="lamp-steps",
             subject="a stair of lamp glows climbing a dark terrace, the pegs in the lamps' pools of light",
             technique="light and shadow (moons in the lanterns' pools, the handrails dotted)")

RING_R, RING_N = 34.0, 6
# the orange candidates (29), by place: the lanterns' pools (their rings' upper members), the handrails and the
# parapets' copings, spread so no 200 x 200 square holds more than 10. Tuned on seeds 1-864 and the second block
# (seeds 20001-23456: 23.5 per 48) toward the charter's 23.5; 29 rather than 30, since every square the spread rule
# allows an easy 30th in was full and a hard 30th put the board a per 48 harder.
CANDIDATES = [(140, 206), (176, 206), (212, 206), (248, 206), (196, 238), (301, 175), (330, 158), (359, 209),
              (401, 232), (491, 223), (520, 206), (549, 223), (578, 206), (614, 206), (603, 251), (603, 285),
              (700, 290), (466, 270), (495, 287), (377, 312), (528, 313), (160, 336), (189, 353), (291, 364),
              (389, 399), (189, 387), (232, 405), (418, 416), (640, 422)]


def build(b):
    lamps = b.features["lamps"]
    for name, (x, y) in lamps.items():
        b.place(x, y, r=9, green=False, tag="lamp")
    bx, by = b.features["bracket lamp"]
    b.place(bx, by, r=9, green=False, tag="lamp")
    dx, dy = b.features["door lamp"]
    b.place(dx, dy, r=9, green=False, tag="lamp")
    for name, (x, y) in lamps.items():
        b.stop(x, y, R=RING_R, n=RING_N, r=9, oranges=(), green=True, tag="pool")
    b.stop(bx, by, R=RING_R, n=RING_N, r=9, oranges=(), green=True, tag="pool")
    # the doorway's crown, open at the keystone
    cx, cy, R, _f, _s = b.circle("door")
    b.arc_bricks(cx, cy, R + 6, 200, 140, n=2, gap_deg=42, t=10, tag="door crown")
    for rail in ("rail 1", "rail 2", "rail 3", "landing B", "landing C"):
        b.trace(rail, spacing=34, r=9, tag="handrail")
    for par in ("parapet L", "parapet R"):
        b.trace(par, spacing=36, r=8, tag="parapet")
    for (x, y) in CANDIDATES:
        b.key(x, y, orange=True, within=14)
    b.greens_in_reach()
