# Review pack: MR 5 Harbour Lights

Stage 5 of The Moon Road, base-21 to base-25, as integrated (8 October 2026). The pack contains game art read from the
install (the composites, trace sheets and engine renders of 5-1, 5-2, 5-4 and 5-5). It is for review only, and the
plugin never ships it.

## Files

Each level `base-NN` (21 to 25) has these files:

| File | What it is |
|---|---|
| `base-NN-composite.png` | The pipeline's composite at 1x (`mfl.py build`), the board as reviewed |
| `base-NN-engine-1280.png` | The plugin's own render at 1280 x 800 (MoonfallRender `--moment start --cache`): the board as dealt, before the first shot |
| `base-NN-engine-640.png` | The same at 640 x 400 (the small window) |
| `base-NN-cleared-1280.png` | The board near cleared (`--moment cleared`), 1280 x 800: what the scene shows once the pegs go |
| `base-NN-trace.png` | The trace sheet (`mfl.py trace`): the graded scene at 2x, the grid, the features, the pieces and the pre-flight |
| `base-NN-trace.txt` | The trace sheet's report: the points skipped while tracing, the piece and candidate counts |
| `base-NN-report.json` | The pipeline's full report for the level (every check, the ramp and the jewels) |

There is also `stage-mr5.txt`, the output of `mfl.py stage mr5`: the table, the pooled ramp over 6,912 games per level,
and the verdict (PASS).

## The stage

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Source | Place (shield) |
|---|---|---|---|---|---|---|---|---|---|
| 5-1 | base-21 | The White Towers | 25.9 | 25.45 | 275 / 210 | 0.224 | 0.132 / 0.123 | La Noscea painting, base01_hr1 [1748, 60, 1440, 1080] | Limsa Lominsa Lower Decks |
| 5-2 | base-22 | The Strait of Merlthor | 23.5 | 24.02 | 290 / 357 | 0.326 | 0.115 / 0.103 | World painting, base05_hr1 [0, 1335, 1100, 825] | Nowhere (a chart) |
| 5-3 | base-23 | Lights on the Water | 22.9 | 23.05 | 255 / 171 | 0.218 | 0.109 / 0.108 | ours (harbourlights) | Limsa Lominsa Lower Decks |
| 5-4 | base-24 | The Arches of the Bridge | 21.3 | 21.68 | 284 / 357 | 0.240 | 0.122 / 0.112 | La Noscea painting, base01_hr1 [2560, 920, 1280, 960] | Limsa Lominsa Lower Decks |
| 5-5 | base-25 | The Lighthouses of Vylbrand | 17.8 | 18.25 | 250 / 181 | 0.268 | 0.102 / 0.099 | Western La Noscea map, s1f400 [740, 720, 1300, 975] | Western La Noscea |

The pooled steps are 1.43, 0.97 and 1.37, and the finale gap is 3.43 (each about ± 0.41). The mean is 22.49, against
MR 4's 23.07 (a fall of 0.58) and the charter's 22.6 (band 26 → 18). The jewel check against 4-5 (321 / 233) passes. The
stage uses 4 game paintings and 1 of ours. Every scene falls back to `moon-road-night` when the shield hides it.

Aces, from `MoonfallAces.Suggest` over 576 games: 5-1 270,000 (305 wins), 5-2 250,000 (288), 5-3 240,000 (262),
5-4 240,000 (252), 5-5 240,000 (221). All are above the hard-level line (14 of 48).

The converter gate (`convert base-21 ... base-25 --check`) passes 10 of 10. 2x max / p99.9, then 1x p99: 5-1 0.039 / 0.007,
0.008; 5-2 0.022 / 0.015, 0.035; 5-3 0.008 / 0.005, 0.006; 5-4 0.039 / 0.008, 0.007; 5-5 0.020 / 0.014, 0.018. 5-1's and
5-4's maxima and 5-2's p99.9 and 1x p99 sit just under their limits.

## Integration changes

- **5-3 (base-23), the ramp:** the stage failed its step rule: 5-3 pooled 23.60 against 5-2's 24.02 (a step of 0.42,
  under 0.5). The coping brick's orange candidacy moved to the centre column's middle moon at (461, 371); held out
  23.3 → 22.9, pooled 23.05 (steps 0.97 and 1.37 either side). Two other tries were too far apart: (468, 438) broke the
  cheap-difficulty gate, (467, 405) eased to 21.5 and closed the step to 5-4, and (466, 303) read 24.0.
- **5-1 (base-21):** the runtime refused its region chroma 0.38 (at most 0.3): now 0.3. The converter gate then failed
  the 2x maximum (0.053): pixels in the arch's dark blue flip between two of the painting's compressed block colours,
  and the palette's chroma gain multiplies the gap. The gain is traded for a floor (chroma 1.3 → 0.85, floor 0.04 → 0.1),
  which keeps the mean saturation close and passes at 0.039.
- **5-2 (base-22):** its rose-copper region has six mask terms and the converter adds the quiet, seven: the runtime now
  takes up to 8 (`MoonfallSceneRecipeLoader.MaxMaskTerms`, `scene-recipe.md`). The gate failed on the black foot
  (p99.9 0.0165, 1x p99 0.038): the palette chroma eased 1.4 → 1.25 and a second glow (#B4A0C8, radius 130 at (400, 640),
  k 0.2) lifts the foot into a faint violet haze under the bucket's lane.
- **5-4 (base-24):** its mist band (y 500-590) touched the lowest moons (y 505), so the runtime left it out: the band now
  runs y 518-590, with seed 11 (seed 24 gave the narrower band 11.2% cover, under the runtime test's 12%). The palette
  chroma eased 1.8 → 1.6 for the gate's maximum (0.042 → 0.039, the same flipping pixels at the top left).
- **5-5 (base-25):** the converter had no plate form for its compass rose; it now ships in the ticks plate, in colour,
  exactly (the converter self-test now includes 5-5, max 0.0000). Its lighthouse beams use a shaft `near` of -170
  (full at the lamp), which the runtime now accepts (-180 to 1000).
- The tests that list shipped levels and recipes now list stage 5 (`MoonfallModesTests`, `MoonfallSceneRecipeLoaderTests`).

## For the reviewers

- **5-2:** check the new foot haze at 640 and in the cleared board; it is the only visible addition.
- **5-1 and 5-4:** the palettes are a little less saturated than the authored builds (5-1 keeps its mean through the floor).
- **5-4 and 5-5 crops** differ from `sources-plan.md`: 5-4 uses [2560, 920, 1280, 960] (planned [2400, 700, 1440, 1080];
  the author moved it so the arches sit above the bucket's approach, 12% over 5-1) and 5-5 uses [740, 720, 1300, 975]
  (planned [560, 360, 1360, 1020]). `mfl.py sources` passes on both.
- **The 5-2 to 5-3 step** is resolved by 0.06 (0.97 less 0.41 against 0.5).
