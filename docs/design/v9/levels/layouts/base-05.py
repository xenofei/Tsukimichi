"""1-5 "The Crystal's Call" (The Waking Sands, Minfilia): a constellation.

Subject: the Mother Crystal's call, which Minfilia hears through the Echo, written in the night sky over the Waking Sands
as a constellation in the figure of a tall faceted crystal (our own painting, painters/crystal.py).

Technique: a moon sits on every star of the figure (larger, r 11: orange, never green); smaller moons (r 7) are dotted
along the atlas's lines between them, the two long flanks carrying a candidate each. A loose field of sky stars
surrounds it, thicker along the Milky Way, its brightest ones candidates so the oranges reach both lower corners. On the
horizon, the Waking Sands' dome wears a crown of brick, and a moon hangs in each date palm's crown.
"""
import math

LEVEL = dict(id="base-05", name="The Crystal's Call", stage=1, number=5, scene="crystal-call",
             subject="a constellation in the figure of a crystal over the Waking Sands",
             technique="a constellation")

# the field: (x, y, candidate). Thicker along the Milky Way (lower left to upper right), thinner elsewhere
FIELD = [(130, 300, False), (170, 360, True), (110, 420, False), (210, 410, True), (250, 330, True), (300, 270, False),
         (190, 230, False), (140, 250, False), (240, 280, False), (330, 340, True), (120, 360, True), (296, 390, True),
         (600, 330, True), (640, 260, True), (690, 200, False), (620, 400, True), (680, 450, False), (580, 470, False),
         (560, 380, True), (660, 340, False), (700, 290, False), (600, 290, True), (530, 460, True), (560, 220, False),
         (300, 458, False), (340, 470, False), (200, 300, True), (680, 384, False), (252, 384, False),
         (104, 206, False), (262, 196, True), (618, 176, False), (690, 330, True), (160, 470, False), (330, 200, True),
         (540, 150, True), (696, 420, False)]


def build(b):
    for (x, y) in b.f("stars"):
        b.place(x, y, r=11, orange=True, green=False, tag="star")
    b.arc(173, 458, 28, 205, 130, t=10, tag="dome")
    b.place(102, 424, r=9, orange=True, tag="palm crown")
    b.place(258, 428, r=9, orange=True, tag="palm crown")
    for (a, c) in b.f("lines"):
        L = math.hypot(c[0] - a[0], c[1] - a[1])
        if L < 60:
            continue
        flank = L > 140 and a[0] != c[0]
        b.trace([a, c], spacing=28, r=7, start=26, end_trim=26, orange={2} if flank else set(), tag="line")
    for (x, y, o) in FIELD:
        b.place(x, y, r=8, orange=o, tag="field star")
    b.trace([(300, 500), (420, 508), (520, 502), (600, 494), (700, 498)], spacing=50, r=9, tag="horizon")
