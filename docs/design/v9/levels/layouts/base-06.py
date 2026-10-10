"""base-06 "Limsa Across the Water": generated layout, variant 1 of `mfl.py generate base-06 --seed 1`.

Subject: Limsa Lominsa's tree-grown tower and its lamplit bridge from the water
Technique: LM (a landmark in outline over a procession)

From the scene recipe's features (limsa-across-water):
  tree lanterns: moons (2)
  bridge deck: brick and dotted (6)
  bridge lamps: brick and dotted (6)
  west arch: crown of brick (2)
  east arch: crown of brick (2)
  house windows: moons (1)
  trunk lights: moons (3)
  canopy: brick and dotted (7)
  west spire: brick and dotted (4)
  middle spire: brick and dotted (2)
  swell: brick and dotted (15)
  far spire: brick and dotted (2)
  near spire: brick and dotted (2)
  mist lights: brick and dotted (1)
  (open board): fill structures (49)

104 pieces (31 bricks, 30%; 0 movers), 66 candidates (63%, 3% of them in the lower third).
Hand-polish the subject, then `mfl.py tune`, then `mfl.py build` (README, "Author workflow").
"""
import math

LEVEL = dict(id='base-06', name='Limsa Across the Water', stage=2, number=6, scene='limsa-across-water', subject="Limsa Lominsa's tree-grown tower and its lamplit bridge from the water", technique='a landmark in outline over a procession')

GENERATED = {'seed': 1, 'variant': 1, 'technique': 'LM', 'target_pieces': 104, 'brick_share': 0.405, 'mirrored': True, 'spacing': 34.0, 'candidate_share': 0.681}

# never orange: the sure greens and the fill left blue (b.candidates leaves out the unreachable and the
# lowest too, to hold the lower third at 30%)
EXCLUDE = [(487.2, 492.2), (525.2, 491.4), (562.2, 490.9), (599.2, 490.4), (656.8, 491.5), (693.7, 493.6), (210.0, 265.0), (690.0, 255.0), (440.0, 325.0), (570.0, 315.0), (270.0, 375.0), (586.6, 194.3), (390.0, 395.0), (110.0, 235.0)]


def build(b):
    # ---- tree lanterns
    b.peg(398.0, 296.0, tag='tree lanterns', green=False)
    b.peg(322.0, 352.0, tag='tree lanterns', green=False)
    # ---- bridge deck
    b.peg(470.0, 388.0, tag='bridge deck')
    b.peg(504.0, 387.1, tag='bridge deck')
    b.peg(541.0, 386.1, tag='bridge deck')
    b.peg(578.0, 385.1, tag='bridge deck')
    b.peg(639.9, 383.4, tag='bridge deck')
    b.peg(673.9, 382.4, tag='bridge deck')
    # ---- bridge lamps
    b.peg(486.0, 350.0, tag='bridge lamps')
    b.peg(520.0, 349.4, tag='bridge lamps')
    b.peg(557.0, 348.7, tag='bridge lamps')
    b.peg(594.0, 348.0, tag='bridge lamps')
    b.peg(656.0, 346.8, tag='bridge lamps')
    b.peg(693.0, 346.1, tag='bridge lamps')
    # ---- west arch
    b.arc(548.0, 478.0, 52.0, 215.0, 34.8, t=20.0, tag='west arch')
    b.arc(548.0, 478.0, 52.0, 252.6, 34.8, t=20.0, tag='west arch')
    # ---- east arch
    b.arc(662.0, 478.0, 52.0, 215.0, 34.8, t=20.0, tag='east arch')
    b.arc(662.0, 478.0, 52.0, 252.6, 34.8, t=20.0, tag='east arch')
    # ---- house windows
    b.peg(500.0, 312.0, tag='house windows')
    # ---- trunk lights
    b.peg(262.0, 444.0, tag='trunk lights')
    b.peg(300.0, 470.0, tag='trunk lights')
    b.peg(356.0, 452.0, tag='trunk lights')
    # ---- canopy: the tree's round head. Moons ring its edge (34 outside the crown's line, gaps where the launcher's
    # swing is), and an inner crown of brick, open at the apex and in two slots, holds the dark head itself
    for st in (188, 205.2, 243, 281, 318.8, 336):
        b.arc(411.1, 292.0, 100.0, st, 16.0, t=20.0, tag='canopy')
    # ---- west spire
    b.line(168.0, 272.0, 168.0, 308.6, t=20.0, tag='west spire')
    b.line(168.0, 311.1, 168.0, 347.8, t=20.0, tag='west spire')
    b.line(168.0, 350.2, 168.0, 386.9, t=20.0, tag='west spire')
    b.line(168.0, 389.4, 168.0, 426.0, t=20.0, tag='west spire')
    # ---- middle spire
    b.line(252.0, 216.0, 252.0, 264.8, t=20.0, tag='middle spire')
    b.line(252.0, 267.2, 252.0, 316.0, t=20.0, tag='middle spire')
    # ---- swell
    b.peg(99.0, 491.9, tag='swell')
    b.peg(136.0, 490.4, tag='swell')
    b.peg(185.9, 488.5, tag='swell')
    b.peg(219.9, 489.1, tag='swell')
    b.peg(253.9, 489.7, tag='swell')
    b.peg(345.6, 492.8, tag='swell')
    b.peg(379.6, 495.1, tag='swell')
    b.peg(416.5, 497.8, tag='swell')
    b.peg(453.3, 495.1, tag='swell')
    b.peg(487.2, 492.2, tag='swell')
    b.peg(525.2, 491.4, tag='swell')
    b.peg(562.2, 490.9, tag='swell')
    b.peg(599.2, 490.4, tag='swell')
    b.peg(656.8, 491.5, tag='swell')
    b.peg(693.7, 493.6, tag='swell')
    # ---- far spire
    b.line(128.0, 340.0, 128.0, 388.8, t=20.0, tag='far spire')
    b.line(128.0, 391.2, 128.0, 440.0, t=20.0, tag='far spire')
    # ---- near spire
    b.line(212.0, 310.0, 212.0, 346.8, t=20.0, tag='near spire')
    b.line(212.0, 349.2, 212.0, 386.0, t=20.0, tag='near spire')
    # ---- mist lights
    b.peg(200.0, 452.0, tag='mist lights')
    # ---- bricks in the open sky and the river-side air (sloped, never level)
    b.line(600.0, 150.0, 625.0, 118.0, t=20.0, tag='fill brick run')
    b.line(590.0, 255.0, 612.0, 222.0, t=20.0, tag='fill brick run')
    # ---- the canopy ring (after the spires, whose bricks it must clear)
    for a in range(190, 356, 15):
        b.place(411.1 + 141 * math.cos(math.radians(a)), 292.0 + 141 * math.sin(math.radians(a)), r=10, tag='canopy ring', green=False)
    # ---- fill lattice
    b.line(354.1, 378.7, 319.6, 400.5, t=20.0, tag='fill lattice')
    b.line(423.4, 414.2, 458.0, 436.0, t=20.0, tag='fill lattice')
    b.place(564.1, 229.8, tag='fill lattice')
    b.place(213.4, 194.3, tag='fill lattice')
    b.place(586.6, 194.3, tag='fill lattice')
    b.place(259.3, 115.6, tag='fill lattice')
    b.place(540.7, 115.6, tag='fill lattice')
    b.place(609.0, 158.8, tag='fill lattice')
    b.place(654.9, 237.5, tag='fill lattice')
    b.place(145.1, 237.5, tag='fill lattice')
    b.place(631.4, 123.3, tag='fill lattice', green=False)
    b.place(99.2, 316.2, tag='fill lattice')
    # ---- fill brick run
    b.line(120.6, 198.6, 102.3, 169.9, t=20.0, tag='fill brick run')
    b.line(612.1, 305.1, 630.4, 276.4, t=20.0, tag='fill brick run')
    b.line(188.9, 155.4, 170.7, 126.7, t=20.0, tag='fill brick run')
    b.line(679.4, 198.6, 697.7, 169.9, t=20.0, tag='fill brick run')
    # ---- fill peg
    b.place(410.0, 355.0, tag='fill peg')
    b.place(270.0, 375.0, tag='fill peg')
    b.place(680.0, 295.0, tag='fill peg')
    b.place(400.0, 215.0, tag='fill peg')
    b.place(680.0, 125.0, tag='fill peg', green=False)
    b.place(120.0, 125.0, tag='fill peg', green=False)
    b.place(120.0, 275.0, tag='fill peg')
    b.place(610.0, 235.0, tag='fill peg')
    b.place(370.0, 335.0, tag='fill peg')
    b.place(570.0, 275.0, tag='fill peg')
    b.place(560.0, 155.0, tag='fill peg')
    b.place(240.0, 155.0, tag='fill peg')
    b.place(170.0, 195.0, tag='fill peg')
    b.place(190.0, 235.0, tag='fill peg')
    b.place(440.0, 325.0, tag='fill peg')
    b.place(630.0, 195.0, tag='fill peg')
    b.place(470.0, 245.0, tag='fill peg')
    b.place(690.0, 255.0, tag='fill peg')
    b.place(590.0, 125.0, tag='fill peg')
    b.place(390.0, 395.0, tag='fill peg')
    b.place(290.0, 325.0, tag='fill peg')
    b.place(650.0, 155.0, tag='fill peg')
    b.place(570.0, 315.0, tag='fill peg')
    b.place(220.0, 125.0, tag='fill peg')
    b.place(140.0, 155.0, tag='fill peg')
    b.place(440.0, 225.0, tag='fill peg')
    b.place(210.0, 265.0, tag='fill peg')
    b.place(110.0, 235.0, tag='fill peg')
    b.place(400.0, 455.0, tag='fill peg')

    b.greens_in_reach()
    return b.candidates(exclude=EXCLUDE)


# ---- tuning (written by `mfl.py tune`; held out 7.1/48, tuning seeds 6.8/48, band 5.13-7.33)
TUNING = {
    "blue": [[99.0, 491.9], [379.6, 495.1], [330.2, 176.5], [470.7, 164.2], [259.3, 115.6], [540.7, 115.6], [240.0, 155.0], [379.6, 197.1], [444.7, 197.8], [252.0, 240.4], [252.0, 291.6], [601.6, 237.6], [621.8, 289.9], [179.8, 141.1]],
    "orange": [[586.6, 194.3], [270.0, 375.0], [440.0, 325.0], [690.0, 255.0], [390.0, 395.0], [570.0, 315.0], [210.0, 265.0], [110.0, 235.0]]
}
