VERDICT: APPROVE (0 blockers, 0 majors, 2 minors)

I worked read-only. `git status` in the worktree is clean, and nothing was built, converted, staged or selftested. Besides the pack, I ran two checks with the existing binaries and wrote all output to my scratchpad (`.../scratchpad/r2/`):

- **Converter gate.** I called the gate's own `convert.gate()` / `check()` against the built MoonfallRender. Its Core.dll was built at 23:34 and includes the `MoonfallFilters` change from 22:20. This is the same method the runtime critic used before, and it does not call `mfl.py convert`.
- **Fresh games.** `mfcheck play` ran 3,456 games per level on each of two seed blocks that no one has tuned on: 40001-43456 (mine from round 1) and 50001-53456 (new). That is 6,912 fresh games per level.

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| m1 (carried) | Minor | 3-3 | Still the stickiest level in the game. Better than round 1, but still over the shipped maximum. | Stuck-rule fires per 1,728 games: 100 and 96 on my two blocks; the pack reports 97. Round 1 was 117 on my block. The shipped maximum is 88 (1-1). Sweep stuck rate 1.9%, 2 long flights (unchanged). |
| m7 (new) | Minor | 3-3 | The converter gate's 1x figure passes by 0.4%, and its worst place is the hard mauve edge at x 500 that UX flagged (n1). | Reproduced 1x p99 **0.03486** against a limit of 0.035, worst at (500.5, 301.5). Any later change to 3-3's dress or grade is likely to fail it. Softening that edge would fix both problems. |
| n3 (carried) | Nit | all | No level reaches the 0.12 protan figure; all five pass only on the 0.101 fallback. | p10 is 0.105-0.117. 3-4's minimum rose from 0.086 to 0.102. |
| n4 (carried) | Nit | 3-1 | The second jewel's share is thinner than before. | 0.159 against the 0.15 floor (was 0.168). |
| n5 | Nit | 3-5 | The `subject` string still says "two flocks of birds", but the north flock is now beyond the right wall. | `layouts/base-15.py` `LEVEL["subject"]`; the report's `subject` field says the same. The docstring says the flight line "carries two moons", but the code places three. |
| n6 | Nit | 3-4 | The layout docstring contradicts the code. | Docstring: movers slide "from a point on the crest to the crest's own height further east". Code: a straight slide at the run's mean height, gated by `OFF_CREST = 9`. The `CANDIDATES` comment still mentions "the moon's two inner moons", but the moon is gone. |
| n7 | Nit | 3-5 | At the new zoom, the chart's own lettering is the largest shape on the board. | In engine-1280, "The Gyhfebu Swaine" spans about 500 px across the middle, larger than the whale (about 250 px). It is painting content and does not hurt readability (F6 0.229). This is the owner's call. |
| n8 | Nit | 3-1 | The candidate moved to Gridania, (514, 400), is now the top holdout. | Left in 23.6% / 24.0% of lost games on my blocks; 23% in the pack. It sits above y 430, so the cheap-difficulty gate does not count it. |

## Round-1 findings checked

- **B1 (converter gate failed on 3-1, 3-3, 3-5): fixed.** My rerun reproduces every figure in `index.md`, and the gate's self-proof passes. 2x max / p99.9: 3-1 0.0233/0.0114, 3-3 0.0338/0.0093, 3-5 0.0276/0.0089. Nothing was dropped on any level, the thresholds are unchanged, and 3-2 and 3-4 still pass.
  - The 1x differences on 3-5 follow edges: the whale's lit outline, the birds and the lettering. This is the expected difference between the pipeline (which downsizes its 2x scene) and the runtime (which cuts at 1x). The gate judges 1x by p99 (0.030), so this is fine.
- **M1 (the 3-3 → 3-4 step did not hold on fresh games): fixed.**
  - On my 6,912 fresh games per level: 3-3 24.00 / 23.90 per 48 and 3-4 22.03 / 22.75. That is a step of **1.56 ± 0.41**, so 1.15 after one standard error, against the rule's 0.5.
  - Pooled with the pack's 6,912 games, the step is 2.00 ± 0.29.
- **M2 (3-5's subject barely carried the board): fixed.**
  - The trace sheet shows a true outline ring of 15 moons about 16 units outside the whale: flukes, tail stock, fin, head and belly. The flight line runs to the head along the painted route. The flock's chevron is traced through the painted birds. The vortex rim, inner wall and west eddy are each ringed.
  - The 23 filler moons and 11 billow arcs are gone. The low cloud crests that replaced them are short runs of 2-3 blue moons. On the trace sheet each sits on a lit lobe edge, and none is a candidate.
  - The holdouts are now on the subject. On my blocks: the flock's lead (543, 412) at 20-22%, the high east crest (629, 314) at 20-22%, the flock (568, 429) at 17-18%, then the flight line.
  - The whale is about 30% of the board's width and reads at 640 as a whale with its ring. Engine-1280 and engine-640 match the composite piece for piece.
- **m1 (3-3 stuck rule): improved, not fixed.** See the table.
- **m2 (3-3 cheap-difficulty margin, holdouts off-subject): fixed.**
  - On my blocks, low oranges are 8.9-9.0% of those left in lost games, against 10% of the candidates: a ratio of about 0.9 (limit 1.5).
  - The top holdouts are now on the subject: (232, 394) in the lower town at 17-19% and the right flank (642, 176) at 15%. The right wall's column of moons is all blue now.
- **m3 (3-1 lane-south candidate on the gate's line): fixed.** (378, 424) stays, left in 21-22% of lost games, above y 430. See n8 for the new top holdout.
- **m4 (3-4 movers left their crests): fixed in the layout.** `build()` drops any run whose crest strays more than 9 units from the run's mean, so the worst case is about a peg's radius (it was 24.3). The painted clouds still do not move; the response explains why mist cannot be added.
- **m5 (3-4 name, charter rows): fixed.** The name, the charter's 3-4, 3-5 and MR 3 rows, and the sources plan are all updated.
- **m6 (held-out figure flattered the finale): fixed.** 3-5 reads 18.82 / 18.36 on my blocks, against held out 18.8 and pooled 18.57.
- **n1 (engine renders a few shots in): fixed.** Every engine render now shows score 0, 10 balls and 25 oranges, and matches the composite piece for piece.
- **n2 (3-1 band edge): fixed** in the docstring.

## Ramp, measured on fresh games

| Level | My 6,912 fresh games | Pack pooled (6,912) | All 13,824 together |
|---|---|---|---|
| 3-1 | 27.26 | 27.77 | 27.52 |
| 3-2 | 25.88 | 25.77 | 25.83 |
| 3-3 | 23.95 | 24.32 | 24.14 |
| 3-4 | 22.39 | 21.88 | 22.14 |
| 3-5 | 18.59 | 18.57 | 18.58 |

- **Steps on my games:** 1.38, 1.93, 1.56, then a finale gap of 3.80, each ± 0.41.
- **Steps on all games:** 1.69, 1.69, 2.00 and 3.56, each ± 0.29.
- **Mean:** 23.61 (target 23.6).
- **Bands:** every level is inside its ±1.2 band. The finale gap of 3.6-3.8 is near the charter's planned 3.5.
- **Playability:** no dead first shots (`no_hit` 0 everywhere) and no unreached pieces on any level. Sweep stuck rates: 3-1 1.5%, 3-2 1.0%, 3-3 1.9%, 3-4 0.1%, 3-5 1.0%.
- **Stuck-rule fires per 1,728 games on my blocks:** 3-1 46, 3-2 40, 3-4 14-18, 3-5 38-45, 3-3 about 98 (m1).

## Per level

**3-1 The Airship Road: APPROVE.**
- **Trace:** unchanged and honest. The pegs follow the dashed lane from the airship to Gridania.
- **Art:** the four broad tones raise F6 to 0.267 and leave no visible mark at 1280.
- **Converter:** passes at both tiers.
- **Open:** n4 and n8 only.

**3-2 Above the Cloud Sea: APPROVE.**
- **Art:** the misted moon reads under the ball and the aim guide in the engine render.
- **Gates and play:** the converter's 2x max is 0.009, and the ramp is 25.9 on my games.
- **Holdout:** (667, 403) is left in 22-24% of lost games, which is GD n1 (accepted).

**3-3 The Holy See: APPROVE, with m1 and m7 open.**
- **Fixes:** the crown is opened at its keystone and the last ridge brick is gone. The wall-strip candidates moved onto the cathedral. Both fixes show in engine-1280 and on the cleared board.
- **Gates:** every gate passes.
- **Open:** the stuck-rule fire rate stays the highest in the game, and the 1x converter headroom is a single rounding step. Softening the mauve edge at x 500 is the shared fix if the owner wants more headroom.

**3-4 The Lane by Starlight: APPROVE.**
- **Trace:**
  - The lane is dotted in perspective: two converging lines and the beacons, with rings of moons round the near lamps.
  - The far towers wear moons on their crowns.
  - The star river is plainly visible on the cleared board, and its four spine moons sit on it.
  - Crest 2 carries no pegs, which is a fair choice.
- **Play:** the stickiest-free level of the stage (sweep 0.1%).
- **Gates:** F7 is 68° apart, and the engine matches the composite.
- **Not my focus:** the green-lit lower banks may still read as hills to the game designer.

**3-5 The Sky Whale: APPROVE.**
- The round-1 Major is resolved: the whale, its ring, the flight line and the flock now carry both the board and its losses.
- No candidate sits at y 430 or lower, and the cleared board is clean.
- **Open:** n5 and n7.

## Not verified

- Live motion at 60 fps: 3-4's movers against the still clouds, and 3-5's glints.
- GPU rendering in game.
- Human play: every difficulty figure comes from mfcheck's greedy player with ±1.5° aim error.
