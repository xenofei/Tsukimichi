# Review pack: MR 3 The Night Skyway (Cid, Brass Wings)

Stage 3 of The Moon Road, base-11 to base-15, as integrated on 7 October 2026. The pack contains game art read from the
install (the composites, trace sheets and engine renders of 3-1, 3-3 and 3-5). It is for review only, and the plugin never
ships it.

## Files

Each level `base-NN` (11 to 15) has these files:

| File | What it is |
|---|---|
| `base-NN-composite.png` | The pipeline's composite at 1x (`mfl.py build`), the board as reviewed |
| `base-NN-engine-1280.png` | The plugin's own render at 1280 x 800 (MoonfallRender `--cache`), at the start of play |
| `base-NN-engine-640.png` | The same at 640 x 400 (the small window) |
| `base-NN-cleared-1280.png` | The board near cleared (`--moment cleared`), 1280 x 800: what the scene shows once the pegs go |
| `base-NN-trace.png` | The trace sheet (`mfl.py trace`): the graded scene at 2x, the grid, the features, the pieces and the pre-flight |
| `base-NN-trace.txt` | The trace sheet's report: the points skipped while tracing, the piece and candidate counts |
| `base-NN-report.json` | The pipeline's full report for the level (every check, the ramp and the jewels) |

There is also `stage-mr3.txt`, the output of `mfl.py stage mr3`: the table, the pooled ramp over 6,912 games, and the
verdict (PASS).

## The stage

| Code | Id | Name | Held out | Pooled | Jewels | Source |
|---|---|---|---|---|---|---|
| 3-1 | base-11 | The Airship Road | 27.6 | 27.67 | 253 / 171 | world painting, base05_hr1 |
| 3-2 | base-12 | Above the Cloud Sea | 25.4 | 25.77 | 290 / 211 | ours (cloudsea) |
| 3-3 | base-13 | The Holy See | 23.8 | 23.73 | 254 / 339 | Coerthas painting, base03 |
| 3-4 | base-14 | The Airship Under Way | 22.4 | 22.70 | 289 / 180 | ours (cloudlane) |
| 3-5 | base-15 | The Sky Whale | 19.2 | 18.22 | 251 / 160 | world painting, base05_hr1 |

The pooled steps are 1.90, 2.04 and 1.03, each with a standard error of 0.41. The finale gap is 4.48 ± 0.40. The stage
uses 3 game paintings and 2 of ours, because the sources plan moved 3-4 to our painting.

## What the integration changed

The authors' branches came in unchanged. These changes were made afterwards:

- **3-1 (base-11), ramp.** The pooled step to 3-2 was 0.88 ± 0.41, which does not resolve 0.5. The western hold's lower-left
  candidate (163, 367) moved to the ring's upper-left moon (163, 333).
- **3-1 (base-11), recipe.** The runtime caps a region's chroma at 0.3, so the region went from 0.4 to 0.3, and
  `regionQuiet` went from 0.8 to 0.7 to keep the second jewel's share at 0.168. Neither recipe change touches the ramp.
- **3-3 (base-13), recipe.** The runtime allows at most 6 mask terms. The region's `x [725, 665]` wall fade was dropped.
  It changed nothing, because the region's polygon ends at x 500.
- **3-3 (base-13), arches.** The bridge arches went from 8 to 10 thick, because the runtime test that keeps a brick's mark
  on its middle line failed for the middle arch. That made the board harder, so the high-cloud blue moon (402, 222) was
  removed. The step to 3-4 is now 1.03.
- **3-3 (base-13), snow.** The silhouette's snow ships as a plate of its own, `holy-see-snow`, separate from the rim. The
  runtime's F3b had been counting the snow as rim light (a run of 33.2). The converter changed in
  `mflkit/convert.py`; the rebuilt dress is still exact, because screen is associative.
- **3-2 (base-12).** The mist seed went from 41 to 11, because seed 41's noise left the band almost empty (0.001 cover).
- **3-5 (base-15).** A broad tone (a disc of radius 150 at (240, 430), × 0.76) darkens the cloud sea behind the billow brick
  at (233, 425). Without it, the purple brick's face margin was 0.117, under the required 0.20.

## Open: the converter gate fails for the three game paintings

`mfl.py convert base-11 ... base-15 --check` passes 3-2 and 3-4 and fails the rest:

| Level | Figure | Measured | Limit |
|---|---|---|---|
| 3-1 | 2x maximum | 0.080 | 0.04 |
| 3-1 | 2x p99.9 | 0.029 | 0.015 |
| 3-1 | 1x p99 | 0.040 | 0.035 |
| 3-3 | 2x maximum | 0.055 | 0.04 |
| 3-5 | 2x maximum | 0.071 | 0.04 |
| 3-5 | 2x p99.9 | 0.020 | 0.015 |

The cause is not a dropped part:

- The game's grade and the pipeline's differ by about one level per channel (grade alone p99.9 0.014 on 3-1 and 0.023 on
  3-3).
- On 3-1 and 3-5, most of the excess is in near-black pixels (5-16% of the opening at 12/255 or less), where OKLab turns one
  level into 0.07.
- 3-3's maximum is a few single-pixel speckles in the grade.

The shipped game paintings pass with less margin (2-3: p99.9 0.013). No threshold was changed.
