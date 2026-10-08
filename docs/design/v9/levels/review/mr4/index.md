# Review pack: MR 4 The Sunlit Steps (Raubahn, Lunar Burst)

Stage 4 of The Moon Road, base-16 to base-20, as integrated (8 October 2026), before round 1. The pack contains game art
read from the install (the composites, trace sheets and engine renders of 4-1, 4-2, 4-4 and 4-5). It is for review only,
and the plugin never ships it.

## Files

Each level `base-NN` (16 to 20) has these files:

| File | What it is |
|---|---|
| `base-NN-composite.png` | The pipeline's composite at 1x (`mfl.py build`), the board as reviewed |
| `base-NN-engine-1280.png` | The plugin's own render at 1280 x 800 (MoonfallRender `--moment start --cache`): the board as dealt, before the first shot |
| `base-NN-engine-640.png` | The same at 640 x 400 (the small window) |
| `base-NN-cleared-1280.png` | The board near cleared (`--moment cleared`), 1280 x 800: what the scene shows once the pegs go. On 4-3 that moment ends the level (its last orange clears too and the tally shows: UX m3, GD n5, round 1), so `base-18-cleared-1280.png` is the bare scene (`--scene-only 2`, scaled to 1280 wide) |
| `base-NN-trace.png` | The trace sheet (`mfl.py trace`): the graded scene at 2x, the grid, the features, the pieces and the pre-flight |
| `base-NN-trace.txt` | The trace sheet's report: the points skipped while tracing, the piece and candidate counts |
| `base-NN-report.json` | The pipeline's full report for the level (every check, the ramp and the jewels) |

There is also `stage-mr4.txt`, the output of `mfl.py stage mr4`: the table, the pooled ramp over 6,912 games per level,
and the verdict (PASS).

## The stage

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Source | Place (shield) |
|---|---|---|---|---|---|---|---|---|---|
| 4-1 | base-16 | The Steps of Thal | 27.0 | 27.23 | 270 / 339 | 0.318 | 0.109 / 0.104 | Steps of Thal city map, w1t201 | Ul'dah - Steps of Thal |
| 4-2 | base-17 | The Spire of the Sands | 26.1 | 25.85 | 233 / 62 | 0.223 | 0.121 / 0.120 | Thanalan painting, base02_hr1 (east strip) | Ul'dah - Steps of Nald |
| 4-3 | base-18 | Lamps on the Steps | 23.6 | 24.05 | 282 / 1 | 0.256 | 0.108 / 0.106 | ours (lampsteps) | Ul'dah - Steps of Nald |
| 4-4 | base-19 | The Coliseum | 22.1 | 21.95 | 261 / 179 | 0.304 | 0.107 / 0.105 | Steps of Thal city map, w1t202 | Ul'dah - Steps of Thal |
| 4-5 | base-20 | Ul'dah from the Sands | 18.3 | 18.75 | 319 / 237 | 0.302 | 0.117 / 0.090 | Steps of Nald city map, w1t101 | Ul'dah - Steps of Nald |

The pooled steps are 1.10, 1.95, 1.27 and the finale gap 3.43 (each about ± 0.41). The mean is 23.26 (the charter's
23.1, band 26.5 → 18.5). The jewel check against 3-5 (258 / 176) passes. The stage uses 4 game paintings and 1 of ours.
Every scene falls back to `moon-road-night` when the shield hides it. 4-4 is tagged Steps of Thal, not the charter's
Nald: the Coliseum is on the Thal map (`sources-plan.md`, which wins). 4-5 is on the Steps of Nald city map, not the
charter's west strip of base02 (the owner's crop fix in `sources-plan.md`).

Aces, from `MoonfallAces.Suggest` over 576 games: 4-1 240,000 (322 wins), 4-2 250,000 (320), 4-3 260,000 (294),
4-4 240,000 (258), 4-5 260,000 (223). All are above the hard-level line (14 of 48).

The converter gate (`convert base-16 ... base-20 --check`) passes 10 of 10. 2x max / p99.9, then 1x p99: 4-1 0.019 / 0.008,
0.022; 4-2 0.023 / 0.008, 0.008; 4-3 0.009 / 0.007, 0.006; 4-4 0.021 / 0.008, 0.028; 4-5 0.015 / 0.007, 0.019.

## Integration changes

- **4-4 (base-19):** the runtime test `Peg_marks_sit_on_every_brick_inside_its_face` failed on two bricks whose mark
  centre fell off the brick's middle line on the 1-unit clearance grid. The outer tier's north-west crown now starts at
  224.5 degrees, not 225 (its east end, at the keystone slot, is unchanged), and the Promenade's run is trimmed 1 unit
  at its north end (`trim0=1.0`). A first try (start 225.5) eased the level to 23.8 held out and left the 4-3 to 4-4 step
  unresolved (0.85 ± 0.41); the final one holds at 23.3 held out, 22.18 pooled. Its Ace moved from 250,000 to 240,000.
- No other level changed.

## For the reviewers

- Three of the five scenes are Ul'dah city maps (4-1, 4-4, 4-5), with 4-1 and 4-5 both drawn as a trail over the city
  plan: check that they do not read as near-repeats, and that 4-5 still reads as "from the Sands" (the charter's skyline
  subject moved with the art).
- 4-3's painting is ours and draws Ul'dah's dome and minarets as silhouettes: check it against the owner's rule that
  figures come from game art.
- 4-5's runtime stars: the converter report places many of its 30 stars on the opening's top row (y 43) and right edge
  (x 723); check the engine renders for a line of stars along the frame.
