"""base-10 "Twin Lanterns": generated layout, variant 1 of `mfl.py generate base-10 --seed 1`.

Subject: a harbour gate and twin lanterns mirrored in still water
Technique: RF (reflection (the mirror image drawn with slide movers))

From the scene recipe's features (vesper-twin-lanterns):
  lanterns: moons (2)
  gate box: traced (15)
  lantern l: dotted (6)
  lantern r: dotted (6)
  arch: crown of brick (2)
  reflection: slide movers (mirror) (12)
  (open board): fill structures (66)

Hand-polished (mr2 review M2): the water below y 350 holds the gate and both lanterns again, mirrored across the
waterline and foreshortened (a squash of 0.72), drawn with slide movers that ripple while the gate stands still:
the arch as a hanging U of pegs (dotted, never brick: a concave-up brick would cradle the ball), each pillar's
reflection, each lantern's post and head ring. Ripple marks of brick lie in the still water between them.
"""
import math

LEVEL = dict(id='base-10', name='Twin Lanterns', stage=2, number=10, scene='vesper-twin-lanterns', subject='a harbour gate and twin lanterns mirrored in still water', technique='reflection (the mirror image drawn with slide movers)')

GENERATED = {'seed': 1, 'variant': 1, 'technique': 'RF', 'target_pieces': 109, 'brick_share': 0.392, 'mirrored': False, 'spacing': 34.0, 'candidate_share': 0.696}

# never orange: the sure greens and the fill left blue (b.candidates leaves out the unreachable and the
# lowest too, to hold the lower third at 30%)
EXCLUDE = [(169.0, 263.5), (700.0, 185.0), (460.0, 305.0), (530.0, 195.0), (337.0, 263.5), (253.0, 190.4), (400.0, 225.0), (470.0, 205.0), (130.0, 255.0), (296.0, 170.0), (330.0, 170.0), (456.0, 170.0), (490.0, 170.0), (211.0, 117.3), (295.0, 117.3), (505.0, 117.3),
           (196.0, 450.0), (604.0, 450.0)]
def build(b):
    # ---- lanterns
    b.peg(196.0, 236.0, tag='lanterns', green=False)
    b.peg(604.0, 236.0, tag='lanterns', green=False)
    # ---- gate box
    b.peg(296.0, 170.0, tag='gate box')
    b.peg(330.0, 170.0, tag='gate box')
    b.peg(456.0, 170.0, tag='gate box')
    b.peg(490.0, 170.0, tag='gate box')
    b.peg(504.0, 225.5, tag='gate box')
    b.peg(504.0, 336.0, tag='gate box')
    b.peg(452.0, 352.0, tag='gate box')
    b.peg(392.0, 352.0, tag='gate box')
    b.peg(344.0, 352.0, tag='gate box')
    b.peg(310.0, 352.0, tag='gate box')
    # gate pillars in brick (steep straight stretches)
    b.line(296.0, 212.0, 296.0, 300.0, t=20.0, tag='gate pillar')
    b.line(504.0, 262.0, 504.0, 316.0, t=20.0, tag='gate pillar')
    # lantern posts in brick
    b.line(224.0, 272.0, 224.0, 300.0, t=20.0, tag='lantern post')
    b.line(632.0, 272.0, 632.0, 300.0, t=20.0, tag='lantern post')
    # ---- lantern l
    b.peg(168.0, 196.0, tag='lantern l')
    b.peg(202.0, 196.0, tag='lantern l')
    b.peg(221.9, 348.0, tag='lantern l')
    b.peg(184.9, 348.0, tag='lantern l')
    # ---- lantern r
    b.peg(576.0, 196.0, tag='lantern r')
    b.peg(610.0, 196.0, tag='lantern r')
    b.peg(629.9, 348.0, tag='lantern r')
    b.peg(592.9, 348.0, tag='lantern r')
    # ---- arch
    b.arc(400.0, 274.0, 88.0, 228.3, 26.7, t=20.0, tag='arch')
    b.arc(400.0, 274.0, 88.0, 256.7, 26.7, t=20.0, tag='arch')
    # ---- reflection: the gate and the lanterns again below the waterline (y 350), squashed to 0.72, ripple drifting
    def mir(y):
        return 350.0 + 0.72 * (350.0 - y)
    for k in range(5):
        a = math.radians(205 + 130 * k / 4)
        b.peg(400.0 + 88.0 * math.cos(a), mir(274.0 + 88.0 * math.sin(a)), tag='arch reflection',
              move={'kind': 'slide', 'x': 400.0 + 88.0 * math.cos(a) + 12.0, 'y': mir(274.0 + 88.0 * math.sin(a)), 'period': 5.0})
    for (x, side) in ((296.0, -1), (504.0, 1)):
        for y in (384.0, 410.0):
            b.peg(x, y, tag='pillar reflection', move={'kind': 'slide', 'x': x + 12.0, 'y': y, 'period': 5.0})
    for lx in (196.0, 604.0):
        b.peg(lx, 380.0, tag='post reflection', move={'kind': 'slide', 'x': lx + 12.0, 'y': 380.0, 'period': 5.0})
        for k in range(5):
            a = math.radians(90 + 72 * k)
            x, y = lx + 34.0 * math.cos(a), 432.0 + 18.0 * math.sin(a)
            b.peg(x, y, tag='lantern reflection', green=False, move={'kind': 'slide', 'x': x + 12.0, 'y': y, 'period': 5.0})
    # still water: ripple marks of brick in pairs, and the far shore's lights between
    for (x, y) in ((260.0, 480.0), (540.0, 480.0)):
        b.arc(x, y + 30.0, 30.0, 235.0, 70.0, t=20.0, tag='ripple')
    b.line(110.0, 480.0, 142.0, 498.0, t=20.0, tag='ripple')
    b.line(658.0, 498.0, 690.0, 480.0, t=20.0, tag='ripple')
    for (x, y) in ((400.0, 430.0), (110.0, 395.0), (690.0, 395.0)):
        b.peg(x, y, tag='still water')
    # ---- fill grid
    b.line(384.0, 309.0, 416.0, 291.0, t=20.0, tag='fill grid')
    b.line(552.0, 309.0, 584.0, 291.0, t=20.0, tag='fill grid')
    b.line(552.0, 162.8, 584.0, 144.8, t=20.0, tag='fill grid')
    b.line(216.0, 162.8, 248.0, 144.8, t=20.0, tag='fill grid')
    b.line(132.0, 291.0, 164.0, 309.0, t=20.0, tag='fill grid')
    b.line(636.0, 144.8, 668.0, 162.8, t=20.0, tag='fill grid')
    b.peg(379.0, 263.5, tag='fill grid')
    b.peg(337.0, 263.5, tag='fill grid')
    b.peg(547.0, 263.5, tag='fill grid')
    b.peg(253.0, 190.4, tag='fill grid')
    b.peg(505.0, 117.3, tag='fill grid')
    b.peg(295.0, 117.3, tag='fill grid')
    b.peg(547.0, 117.3, tag='fill grid')
    b.peg(169.0, 263.5, tag='fill grid')
    b.peg(211.0, 117.3, tag='fill grid')
    b.peg(169.0, 117.3, tag='fill grid', green=False)
    b.peg(106.0, 226.9, tag='fill grid')
    # ---- fill brick run
    b.line(125.0, 186.8, 108.0, 157.4, t=20.0, tag='fill brick run')
    b.line(461.0, 259.9, 444.0, 230.5, t=20.0, tag='fill brick run')
    b.line(675.0, 333.0, 692.0, 303.5, t=20.0, tag='fill brick run')
    b.line(675.0, 259.9, 692.0, 230.5, t=20.0, tag='fill brick run')
    # ---- fill peg
    b.peg(250.0, 235.0, tag='fill peg')
    b.peg(650.0, 215.0, tag='fill peg')
    b.peg(460.0, 305.0, tag='fill peg')
    b.peg(340.0, 305.0, tag='fill peg')
    b.peg(260.0, 335.0, tag='fill peg')
    b.peg(400.0, 225.0, tag='fill peg')
    b.peg(530.0, 195.0, tag='fill peg')
    b.peg(160.0, 155.0, tag='fill peg')
    b.peg(260.0, 285.0, tag='fill peg')
    b.peg(470.0, 205.0, tag='fill peg')
    b.peg(550.0, 225.0, tag='fill peg')
    b.peg(130.0, 255.0, tag='fill peg')
    b.peg(550.0, 345.0, tag='fill peg')
    b.peg(430.0, 325.0, tag='fill peg')
    b.peg(700.0, 145.0, tag='fill peg', green=False)
    b.peg(700.0, 185.0, tag='fill peg')
    b.peg(100.0, 305.0, tag='fill peg')
    b.peg(150.0, 225.0, tag='fill peg')

    b.greens_in_reach()
    return b.candidates(exclude=EXCLUDE)


# ---- tuning (written by `mfl.py tune`; held out 5.2/48, tuning seeds 4.6/48, band 4.80-6.00)
TUNING = {
    "blue": [[571.7, 437.6], [636.3, 437.6], [296.0, 256.0], [504.0, 290.0], [358.2, 196.6], [400.1, 186.0], [400.0, 300.0], [568.0, 153.8], [232.0, 153.8], [452.5, 245.2]],
    "orange": [[169.0, 263.5], [130.0, 255.0], [700.0, 185.0]]
}
