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

109 pieces (18 bricks, 17%; 12 movers), 69 candidates (63%, 3% of them in the lower third).
Hand-polish the subject, then `mfl.py tune`, then `mfl.py build` (README, "Author workflow").
"""

LEVEL = dict(id='base-10', name='Twin Lanterns', stage=2, number=10, scene='vesper-twin-lanterns', subject='a harbour gate and twin lanterns mirrored in still water', technique='reflection (the mirror image drawn with slide movers)')

GENERATED = {'seed': 1, 'variant': 1, 'technique': 'RF', 'target_pieces': 109, 'brick_share': 0.392, 'mirrored': False, 'spacing': 34.0, 'candidate_share': 0.696}

# never orange: the sure greens and the fill left blue (b.candidates leaves out the unreachable and the
# lowest too, to hold the lower third at 30%)
EXCLUDE = [(169.0, 263.5), (700.0, 185.0), (460.0, 305.0), (680.0, 415.0), (100.0, 405.0), (274.0, 373.1), (530.0, 195.0), (337.0, 263.5), (560.0, 385.0), (253.0, 190.4), (463.0, 482.7), (295.0, 482.7), (211.0, 482.7), (460.0, 445.0)]
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
    # ---- reflection
    b.peg(452.0, 400.0, move={'kind': 'slide', 'x': 468.0, 'y': 400.0, 'period': 5.0}, tag='reflection')
    b.peg(392.0, 400.0, move={'kind': 'slide', 'x': 408.0, 'y': 400.0, 'period': 5.0}, tag='reflection')
    b.peg(344.0, 400.0, move={'kind': 'slide', 'x': 360.0, 'y': 400.0, 'period': 5.0}, tag='reflection')
    b.peg(310.0, 400.0, move={'kind': 'slide', 'x': 326.0, 'y': 400.0, 'period': 5.0}, tag='reflection')
    b.peg(221.9, 404.0, move={'kind': 'slide', 'x': 237.9, 'y': 404.0, 'period': 5.0}, tag='reflection')
    b.peg(184.9, 404.0, move={'kind': 'slide', 'x': 200.9, 'y': 404.0, 'period': 5.0}, tag='reflection')
    b.peg(629.9, 404.0, move={'kind': 'slide', 'x': 645.9, 'y': 404.0, 'period': 5.0}, tag='reflection')
    b.peg(592.9, 404.0, move={'kind': 'slide', 'x': 608.9, 'y': 404.0, 'period': 5.0}, tag='reflection')
    b.peg(504.0, 416.0, move={'kind': 'slide', 'x': 520.0, 'y': 416.0, 'period': 5.0}, tag='reflection')
    b.peg(224.0, 440.5, move={'kind': 'slide', 'x': 240.0, 'y': 440.5, 'period': 5.0}, tag='reflection')
    b.peg(632.0, 440.5, move={'kind': 'slide', 'x': 648.0, 'y': 440.5, 'period': 5.0}, tag='reflection')
    b.peg(504.0, 450.0, move={'kind': 'slide', 'x': 520.0, 'y': 450.0, 'period': 5.0}, tag='reflection')
    # ---- fill grid
    b.line(384.0, 309.0, 416.0, 291.0, t=20.0, tag='fill grid')
    b.line(384.0, 455.2, 416.0, 437.2, t=20.0, tag='fill grid')
    b.line(552.0, 309.0, 584.0, 291.0, t=20.0, tag='fill grid')
    b.line(300.0, 437.2, 332.0, 455.2, t=20.0, tag='fill grid')
    b.line(552.0, 455.2, 584.0, 437.2, t=20.0, tag='fill grid')
    b.line(552.0, 162.8, 584.0, 144.8, t=20.0, tag='fill grid')
    b.line(216.0, 162.8, 248.0, 144.8, t=20.0, tag='fill grid')
    b.line(132.0, 291.0, 164.0, 309.0, t=20.0, tag='fill grid')
    b.line(636.0, 144.8, 668.0, 162.8, t=20.0, tag='fill grid')
    b.line(154.0, 437.2, 186.0, 455.2, t=20.0, tag='fill grid')
    b.peg(379.0, 263.5, tag='fill grid')
    b.peg(337.0, 263.5, tag='fill grid')
    b.peg(526.0, 373.1, tag='fill grid')
    b.peg(274.0, 373.1, tag='fill grid')
    b.peg(547.0, 263.5, tag='fill grid')
    b.peg(463.0, 482.7, tag='fill grid')
    b.peg(253.0, 190.4, tag='fill grid')
    b.peg(295.0, 482.7, tag='fill grid')
    b.peg(505.0, 117.3, tag='fill grid')
    b.peg(295.0, 117.3, tag='fill grid')
    b.peg(547.0, 117.3, tag='fill grid')
    b.peg(169.0, 263.5, tag='fill grid')
    b.peg(211.0, 482.7, tag='fill grid')
    b.peg(211.0, 117.3, tag='fill grid')
    b.peg(694.0, 373.1, tag='fill grid')
    b.peg(169.0, 117.3, tag='fill grid', green=False)
    b.peg(106.0, 226.9, tag='fill grid')
    # ---- fill brick run
    b.line(125.0, 186.8, 108.0, 157.4, t=20.0, tag='fill brick run')
    b.line(125.0, 340.1, 108.0, 369.5, t=20.0, tag='fill brick run')
    b.line(461.0, 259.9, 444.0, 230.5, t=20.0, tag='fill brick run')
    b.line(675.0, 333.0, 692.0, 303.5, t=20.0, tag='fill brick run')
    b.line(675.0, 259.9, 692.0, 230.5, t=20.0, tag='fill brick run')
    # ---- fill peg
    b.peg(250.0, 235.0, tag='fill peg')
    b.peg(650.0, 215.0, tag='fill peg')
    b.peg(460.0, 305.0, tag='fill peg')
    b.peg(340.0, 305.0, tag='fill peg')
    b.peg(140.0, 395.0, tag='fill peg')
    b.peg(260.0, 335.0, tag='fill peg')
    b.peg(400.0, 225.0, tag='fill peg')
    b.peg(530.0, 195.0, tag='fill peg')
    b.peg(160.0, 155.0, tag='fill peg')
    b.peg(260.0, 285.0, tag='fill peg')
    b.peg(470.0, 205.0, tag='fill peg')
    b.peg(550.0, 225.0, tag='fill peg')
    b.peg(130.0, 255.0, tag='fill peg')
    b.peg(550.0, 345.0, tag='fill peg')
    b.peg(560.0, 385.0, tag='fill peg')
    b.peg(430.0, 325.0, tag='fill peg')
    b.peg(660.0, 365.0, tag='fill peg')
    b.peg(700.0, 145.0, tag='fill peg', green=False)
    b.peg(700.0, 185.0, tag='fill peg')
    b.peg(100.0, 305.0, tag='fill peg')
    b.peg(150.0, 225.0, tag='fill peg')
    b.peg(360.0, 485.0, tag='fill peg')
    b.peg(460.0, 445.0, tag='fill peg')
    b.peg(680.0, 415.0, tag='fill peg')
    b.peg(100.0, 405.0, tag='fill peg')

    b.greens_in_reach()
    return b.candidates(exclude=EXCLUDE)


# ---- tuning (written by `mfl.py tune`; held out None/48, tuning seeds 5.8/48, band 4.60-5.40; NOT in band: stuck)
TUNING = {
    "blue": [[296.0, 170.0], [330.0, 170.0], [456.0, 170.0], [490.0, 170.0], [632.0, 440.5], [505.0, 117.3], [295.0, 117.3], [211.0, 117.3], [400.0, 225.0], [470.0, 205.0], [360.0, 485.0], [296.0, 256.0], [504.0, 290.0], [224.0, 286.0], [632.0, 286.0], [358.2, 196.6], [400.1, 186.0], [400.0, 300.0], [568.0, 300.0], [568.0, 153.8], [232.0, 153.8], [148.0, 300.0], [652.0, 153.8], [452.5, 245.2]],
    "orange": [[274.0, 373.1], [253.0, 190.4], [169.0, 263.5], [460.0, 305.0], [560.0, 385.0], [700.0, 185.0]],
    "nudge": [[360.0, 485.0, 0, 10], [100.0, 405.0, 0, 10]],
    "period": [[452.0, 400.0, 0.8]]
}
