# Review pack: MR 6 The Silent Stars

Stage 6 of The Moon Road, base-26 to base-30, as integrated (9 October 2026). The heavens Urianger reads: the armillary,
the Twelve's wheel, the seer's chart, a meteor shower, the Moon Gate. The stage is set nowhere. Four scenes are our own
paintings. 6-3 reads the game's world map from the install, so its composite, trace sheet and engine renders hold game
art. The pack is for review only, and the plugin never ships it.

## Files

Each level `base-NN` (26 to 30) has these files:

| File | What it is |
|---|---|
| `base-NN-composite.png` | The pipeline's composite at 1x (`mfl.py build`), the board as reviewed |
| `base-NN-engine-1280.png` | The plugin's own render at 1280 x 800 (MoonfallRender `--moment start --cache`): the board as dealt, before the first shot |
| `base-NN-engine-640.png` | The same at 640 x 400 (the small window) |
| `base-NN-cleared-1280.png` | The board near cleared (`--moment cleared`), 1280 x 800: what the scene shows once the pegs go |
| `base-NN-trace.png` | The trace sheet (`mfl.py trace`): the graded scene at 2x, the grid, the features, the pieces and the pre-flight |
| `base-NN-trace.txt` | The trace sheet's report: the points skipped while tracing, the piece and candidate counts |
| `base-NN-report.json` | The pipeline's full report for the level (every check, the ramp and the jewels) |

`stage-mr6.txt` holds the output of `mfl.py stage mr6`: the table, the pooled ramp over 6,912 games per level, and the
verdict (PASS).

## The stage

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Source | Place (shield) |
|---|---|---|---|---|---|---|---|---|---|
| 6-1 | base-26 | The Armillary | 24.7 | 24.7 | 296 / 55 | 0.205 | 0.131 / 0.124 | ours (armillary) | Nowhere |
| 6-2 | base-27 | The Wheel of the Twelve | 23.0 | 23.72 | 261 / 329 | 0.270 | 0.134 / 0.131 | ours (twelve) | Nowhere |
| 6-3 | base-28 | The Seer's Chart | 22.3 | 22.6 | 290 / 168 | 0.285 | 0.130 / 0.128 | World map, `ui/map/world/01/world01_m.tex` | Nowhere (a chart) |
| 6-4 | base-29 | Meteor Shower | 20.8 | 20.57 | 258 / 173 | 0.230 | 0.110 / 0.092 | ours (meteors) | Nowhere |
| 6-5 | base-30 | The Moon Gate | 17.4 | 17.4 | 313 / 214 | 0.254 | 0.103 / 0.102 | ours (moongate) | Nowhere |

- **Ramp:** the pooled steps are 1.18, 0.92 and 2.03, each about ± 0.41, and the finale gap is 3.17 ± 0.40. The mean is
  21.76, against the charter's 21.6 (band 25 → 17). The jewel check against 5-5 (246 / 168) passes.
- **Sources and shield:** the stage uses 1 game painting of 5 (the charter's 1 for MR 6). Every scene is set nowhere
  (charter 1.4) and falls back to `moon-road-night`.
- **Aces:** from `MoonfallAces.Suggest` over 576 games: 6-1 250,000 (306 wins), 6-2 250,000 (270), 6-3 230,000 (255),
  6-4 220,000 (229), 6-5 230,000 (197). All are above the hard-level line (14 of 48), and 6-5 is now the lowest of
  stages 1-6.
- **ch-03 (Thirty-Five Moons):** now points at base-27. `mfl.py challenge ch-03` passes: 35 oranges on 35 candidates,
  and the greedy player wins 20 of 48 (29 before 6-2's round 1 fix).
- **Converter gate:** `convert base-26 ... base-30 --check` passes 10 of 10. The figures are 2x max / p99.9, then 1x
  p99: 6-1 0.008 / 0.006, 0.006; 6-2 0.007 / 0.004, 0.005; 6-3 0.012 / 0.007, 0.028; 6-4 0.005 / 0.003, 0.004;
  6-5 0.009 / 0.006, 0.006. 6-3's 1x p99 is the closest to its 0.035 limit.

## Integration changes

- **6-2 (base-27), the ramp:** the stage failed its step rule. 6-2 pooled 24.62 against 6-1's 24.7, a step of 0.08.
  The hub's four ray moons went from r 10 to r 9, half way between the author's r 8 (22.0) and r 10 (23.8) tuning
  figures. Held out 23.5 → 24.2 and pooled 24.62 → 23.52, which gives steps of 1.18 and 0.92. The candidates are
  unchanged at 35.
- **6-4 (base-29), a brick:** the runtime test `Peg_marks_sit_on_every_brick_inside_its_face` failed on the right
  slope's first brick. Its middle fell on a whole unit, where the clearance field reads 0.66 off the middle line. The
  run's `trim0` changed from 4 to 3.5, which moves both bricks 0.5 along the crest. A trim of 4.5 also cleared the
  test, but it read 0.5 per 48 harder (pooled 20.07) and closed the finale gap to 2.67. At 3.5, 6-4 is held out 20.8
  and pooled 20.57.
- **6-5 (base-30), its beams:** the recipe's shaft angles were 208-332°. A shaft's angle wraps at 180° (5-5's finding),
  so neither the pipeline nor the engine drew them, and the runtime refused the recipe. They are now -152, -124, -56
  and -28°, the same directions. Faint beams (k 0.03) now rise from the gate in the composite and in the game.
- **6-5 (base-30), F6 at runtime:** the purple brick at the crown's two ends, over the gate's bright rim, read 0.194
  (the floor is 0.20). A tone darkens the band y 300-370 inside 175 units of the gate by x0.93, and the margin is now
  0.223. Raising the veil from 0.22 to 0.24 gained only 0.002, so the veil stays at 0.22.
- **6-1 (base-26) and 6-3 (base-28), region chroma:** the runtime takes at most 0.3. 6-1 went 0.32 → 0.3 and 6-3's
  cloud-sea green went 0.42 → 0.3. Both still pass readcheck. The jewels moved by a degree.
- **6-3 (base-28), motion:** the recipe asked for 6 glints (the runtime takes 4) and 18 dust motes, but dust drifts
  only in moving shafts and 6-3 has none. The dust is gone. The glints keep the five-city loop and Mor Dhona's lines
  to the three far cities. The two short lines to Gridania and Ul'dah stay engraved without a glint; the rings all but
  touch there.
- **Runtime recipe format:** a star mask (`motion.starWhere`) now takes up to 16 terms
  (`MoonfallSceneRecipeLoader.MaxStarMaskTerms`; `scene-recipe.md`). 6-2 keeps its stars off each of its twelve signs
  (13 terms), and 6-5 keeps them off the twelve discs round its gate (14 terms). Other masks keep the limit of 8.
- **Tests:** `MoonfallModesTests` and `MoonfallSceneRecipeLoaderTests`, which list the shipped levels and recipes, now
  list stage 6. `MoonfallChallengeTests` needed an id that is still unshipped and used base-30; it now uses base-55.

## For the reviewers

- **6-2 (round 1 fix, game designer M2, critic n1):** the wheel's inner ticked rim now carries six bricks at its four
  diagonals: two-brick arcs on the upper rim either side of the top (they stop where the rim flattens to a deck) and one
  brick on each lower diagonal, each 28 units clear of the divider moons. The sweep is mean 7.7, median 7 (was 6.4 and
  6); pieces 62 → 68; candidates still 35. The hub's ray moons went from r 9 to r 8.5 to hold the ramp: tuning seeds
  22.7 (1-864) and 23.5 (20001-23456), held out 23.0, pooled 23.72 over the stage's 6,912 games (was 23.52). The top
  ray's moon is at x 401, so the 0° shot no longer bounces on the hub's line (long flights 1 → 0). Stuck fires 54 per
  1,728 (was 23), under the 88 maximum; the sweep sticks on 2 of 681. The low orange (526,440) is left in 22% of lost
  games (was 14%), under the 25% gate. ch-03 wins 20 of 48 at 35 oranges. Ace unchanged (250,000, 270 of 576 win).
- **6-1 (round 1 fix, game designer M1, m3, n3):** the cage is no longer teal. The brass frame and the heart are gold, the rings
  pale silver, and the dusk glows amber behind the far hills. Jewels 296 / 55 (6-5 is 313 / 214). Protan p10 0.131 (was
  0.119). F6 worst 0.205 (was 0.234): the teal region had lifted the margin by 0.03, and the gold cannot sit near the
  pegs. The moon has its seas, and the spokes run on to the moons at their tips. Layout unchanged (24.7 held out).
- **6-5:** check the new beams over the gate and the slightly darker rim band at the crown's ends. Neither was in the
  authored build.
- **6-3:** the cloud sea is less saturated than the authored build (chroma 0.42 → 0.3). At 1280 the chart reads as a
  muted violet and green, so check it at 640 too.
- **6-4:** protan min is 0.092, under UX's 0.105 guard. The author's build was the same; nothing here changed it.
- **6-2 to 6-3:** this step resolves by 0.01 (0.92 less 0.41 against 0.5).
