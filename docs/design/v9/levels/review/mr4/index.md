# Review pack: MR 4 The Sunlit Steps (Raubahn, Lunar Burst)

Stage 4 of The Moon Road, base-16 to base-20, as integrated (8 October 2026), after the round-2 fixes (`../../supervisor/response-mr4-round-2.md`). The pack contains game art
read from the install (the composites, trace sheets and engine renders of 4-1, 4-2, 4-4 and 4-5). It is for review only,
and the plugin never ships it.

## Files

Each level `base-NN` (16 to 20) has these files:

| File | What it is |
|---|---|
| `base-NN-composite.png` | The pipeline's composite at 1x (`mfl.py build`), the board as reviewed |
| `base-NN-engine-1280.png` | The plugin's own render at 1280 x 800 (MoonfallRender `--moment start --cache`): the board as dealt, before the first shot |
| `base-NN-engine-640.png` | The same at 640 x 400 (the small window) |
| `base-NN-cleared-1280.png` | The board near cleared (`--moment cleared`), 1280 x 800: what the scene shows once the pegs go. The kept orange is the lowest one out of Lunar Burst's reach of every green, and the render fails rather than show the tally (UX m3, GD n5, round 1), so every level's file, 4-3's too, is a real cleared board |
| `base-NN-trace.png` | The trace sheet (`mfl.py trace`): the graded scene at 2x, the grid, the features, the pieces and the pre-flight |
| `base-NN-trace.txt` | The trace sheet's report: the points skipped while tracing, the piece and candidate counts |
| `base-NN-report.json` | The pipeline's full report for the level (every check, the ramp and the jewels) |

There is also `stage-mr4.txt`, the output of `mfl.py stage mr4`: the table, the pooled ramp over 6,912 games per level,
and the verdict (PASS).

## The stage

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Source | Place (shield) |
|---|---|---|---|---|---|---|---|---|---|
| 4-1 | base-16 | The Steps of Thal | 26.0 | 26.68 | 270 / 338 | 0.318 | 0.109 / 0.103 | Steps of Thal city map, w1t201 | Ul'dah - Steps of Thal |
| 4-2 | base-17 | The Spire of the Sands | 25.6 | 25.30 | 233 / 63 | 0.224 | 0.121 / 0.120 | Thanalan painting, base02_hr1 (east strip) | Ul'dah - Steps of Nald |
| 4-3 | base-18 | Lamps on the Steps | 23.7 | 23.55 | 282 / 1 | 0.262 | 0.108 / 0.106 | ours (lampsteps) | Ul'dah - Steps of Nald |
| 4-4 | base-19 | The Coliseum | 22.1 | 21.95 | 261 / 179 | 0.304 | 0.107 / 0.105 | Steps of Thal city map, w1t202 | Ul'dah - Steps of Thal |
| 4-5 | base-20 | Ul'dah from the Sands | 18.1 | 17.88 | 321 / 233 | 0.270 | 0.113 / 0.103 | Steps of Nald city map, w1t101 | Ul'dah - Steps of Nald |

The pooled steps are 1.38, 1.75, 1.60 and the finale gap 4.07 (each about ± 0.41). The mean is 23.07 (MR 3's
23.66: a fall of 0.59; the charter's 23.1, band 26.5 → 18.5). The jewel check against 3-5 (258 / 176) passes. The stage uses 4 game paintings and 1 of ours.
Every scene falls back to `moon-road-night` when the shield hides it. 4-4 is tagged Steps of Thal, not the charter's
Nald: the Coliseum is on the Thal map (`sources-plan.md`, which wins). 4-5 is on the Steps of Nald city map, not the
charter's west strip of base02 (the owner's crop fix in `sources-plan.md`).

Aces, from `MoonfallAces.Suggest` over 576 games: 4-1 240,000 (317 wins), 4-2 260,000 (302; round 2, was 250,000),
4-3 260,000 (289), 4-4 240,000 (258), 4-5 220,000 (199). All are above the hard-level line (14 of 48).

The converter gate (`convert base-16 ... base-20 --check`) passes 10 of 10. 2x max / p99.9, then 1x p99: 4-1 0.019 / 0.008,
0.021; 4-2 0.023 / 0.008, 0.008; 4-3 0.009 / 0.007, 0.006; 4-4 0.021 / 0.008, 0.028; 4-5 0.010 / 0.007, 0.012.

## Integration changes

- **4-4 (base-19):** the runtime test `Peg_marks_sit_on_every_brick_inside_its_face` failed on two bricks whose mark
  centre fell off the brick's middle line on the 1-unit clearance grid. The outer tier's north-west crown now starts at
  224.5 degrees, not 225 (its east end, at the keystone slot, is unchanged), and the Promenade's run is trimmed 1 unit
  at its north end (`trim0=1.0`). A first try (start 225.5) eased the level to 23.8 held out and left the 4-3 to 4-4 step
  unresolved (0.85 ± 0.41); the final one holds at 23.3 held out, 22.18 pooled. Its Ace moved from 250,000 to 240,000.
- No other level changed.

## For the reviewers (round 3)

- **4-5:** the sands are umber below y 430 (hue about 35 at the foot), the dunes carry a moonlit crest, and the Thal
  ghost's arcs are lifted 1.5 (1.8 failed F6 on the moons that trace them). Check the foot and the east third at 640.
- **4-2:** the lens opens toward the wall (top to y 248, underside to y 404), the horizon is a dune crest, and the sand
  grade is feathered from the painted horizon to the wall, so the far dome is sand whole.
- **4-1:** the three blurred patches are gone: those arrows are now `clone` boxes (a new runtime source part, the
  map's own hatching copied from nearby, parity checked by the converter gate). Check the plan at (108, 243),
  (210, 287) and (455, 204) in board units.
- **Stage:** 4-2's round-2 swap was dropped (critic M4); the 4-1 and 4-3 swaps stay. The finale gap is 4.07 pooled, a
  little over the charter's 3.5-4 (the game designer's condition, m4).
