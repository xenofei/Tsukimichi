# Review pack: MR 3 The Night Skyway (Cid, Brass Wings)

Stage 3 of The Moon Road, base-11 to base-15, after the round 1 fold-in (7 October 2026; the reviews and the answers to
them are in `docs/design/v9/levels/supervisor/*-mr3-round-1.md` and `response-mr3-round-1.md`). The pack contains game
art read from the install (the composites, trace sheets and engine renders of 3-1, 3-3 and 3-5). It is for review only,
and the plugin never ships it.

## Files

Each level `base-NN` (11 to 15) has these files:

| File | What it is |
|---|---|
| `base-NN-composite.png` | The pipeline's composite at 1x (`mfl.py build`), the board as reviewed |
| `base-NN-engine-1280.png` | The plugin's own render at 1280 x 800 (MoonfallRender `--moment start --cache`): the board as dealt, before the first shot, so it matches the level file piece for piece (round 1's renders were two shots in: critic n1) |
| `base-NN-engine-640.png` | The same at 640 x 400 (the small window) |
| `base-NN-cleared-1280.png` | The board near cleared (`--moment cleared`), 1280 x 800: what the scene shows once the pegs go |
| `base-NN-trace.png` | The trace sheet (`mfl.py trace`): the graded scene at 2x, the grid, the features, the pieces and the pre-flight |
| `base-NN-trace.txt` | The trace sheet's report: the points skipped while tracing, the piece and candidate counts |
| `base-NN-report.json` | The pipeline's full report for the level (every check, the ramp and the jewels) |

There is also `stage-mr3.txt`, the output of `mfl.py stage mr3`: the table, the pooled ramp over 6,912 games, and the
verdict (PASS).

## The stage

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Source |
|---|---|---|---|---|---|---|---|---|
| 3-1 | base-11 | The Airship Road | 27.1 | 27.77 | 254 / 173 | 0.267 | 0.105 / 0.103 | world painting, base05_hr1 |
| 3-2 | base-12 | Above the Cloud Sea | 25.4 | 25.77 | 290 / 211 | 0.237 | 0.117 / 0.112 | ours (cloudsea) |
| 3-3 | base-13 | The Holy See | 23.8 | 24.32 | 254 / 338 | 0.265 | 0.112 / 0.111 | Coerthas painting, base03 |
| 3-4 | base-14 | The Lane by Starlight | 22.4 | 21.88 | 275 / 208 | 0.232 | 0.107 / 0.102 | ours (cloudlane) |
| 3-5 | base-15 | The Sky Whale | 18.8 | 18.57 | 258 / 176 | 0.229 | 0.112 / 0.109 | world painting, base05_hr1 |

The pooled steps are 2.00, 1.45 and 2.44, each with a standard error of 0.41; the finale gap is 3.31 ± 0.40 (the
charter plans 3.5). The mean is 23.66 (the charter's 23.6). The stage uses 3 game paintings and 2 of ours, because the
sources plan moved 3-4 to our painting. The engine's bare scenes give the same jewels as the pipeline (F7 measured on
MoonfallRender's `--scene-only 2`): 3-4 275 / 207 (68 apart), 3-5 257 / 176 (82 apart, mean chroma 0.069).

## What round 1 changed

- **The converter gate passes on all five** (critic B1). The runtime's cut now resamples in 8-bit passes, as PIL's
  `LANCZOS` does on an 8-bit picture (`MoonfallFilters.Resample(..., eightBit: true)`), so the graded paintings agree
  (p99 0.0000 at 2x); 3-1 and 3-5 lift their black floor a few levels with a faint board-wide glow, since OKLab puts
  sRGB 0 and 1 0.067 apart. 3-5's green fades out over the bucket, where the runtime's quiet differs. Figures (2x max /
  p99.9, 1x p99): 3-1 0.023 / 0.011, 0.032; 3-2 0.009 / 0.007, 0.004; 3-3 0.034 / 0.009, 0.0349; 3-4 0.008 / 0.006,
  0.005; 3-5 0.028 / 0.009, 0.030. The shipped ten still pass.
- **3-1:** the south lane's (410, 421), 9 units above the cheap gate's line, gave its candidate to Gridania's (514, 400);
  four broad tones quieten the banners and the pale cloud shore (F6 0.222 to 0.267).
- **3-2:** the moon on the launcher's axis rises misted (its face about L 0.65, 0.69 at most, under the ball's 0.885 and the aim
  guide's 0.96), with two strata of mist across it; a horizon tone lifts F6 from 0.216 to 0.237.
- **3-3:** the two right-wall candidates moved onto the cathedral; the east billow's crown is opened at its keystone and
  the ridge's last brick is gone (stuck fires 120 to 97, sweep 2.1% to 1.9%); the candidates were rebalanced (low share
  ratio 1.40 to 0.94) and a high-cloud blue moon sets the ramp.
- **3-4:** renamed "The Lane by Starlight"; the moon is set beyond the frame, the star river climbs the sky, the banks
  are modelled with silver crest linings and small billows, and the aqua jewel lies on the lane's wedge of cloud only,
  so the banks read as cloud, not hills. The movers ride each run's mean crest height (a run straying more than a peg's
  radius is left out) and keep off the beacons' posts; the low-protan lamp candidate is blue.
- **3-5:** recropped on the whale, [940, 330, 960, 720], 24.3% over 3-1's crop: the whale spans about 200 of the
  opening's 650 units, lit against cloud darkened round it, and ringed along its own silhouette. The billow arcs are
  gone: the vortex's rim and inner wall, an eddy, the flock's chevron and flight line, and dotted lit cloud crests carry
  the board. No candidate sits at y 430 or lower.
- **Aces** re-suggested over 576 games: 3-1 260,000, 3-4 270,000, 3-5 250,000 (3-2 and 3-3 unchanged).
