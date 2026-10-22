VERDICT: REVISE (0 blockers, 3 majors, 9 minors)

| id | sev | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | base-08 | Plays at 11.6/48, about double the other four. It lifts the stage mean to 7.42, over the band's 7.33 top (passes only on the 0.3 tolerance), and 06/07 were over-tuned to compensate | status: margin -4.3, low3 5%; sheet header; base-06/07 TUNING bands 4.70-5.90 / 4.60-5.80, 07 "NOT in band: plays exhausted" |
| M2 | Major | base-10 | Finale lost its reflection (half the subject), is the plainest board, plays easier than 2-1 and 2-2, and passes the finale rule only because of base-08's outlier | diff --rev 74a669c1; precheck 44% untraced; others' mean 7.63 now, 6.56 if base-08 sat at 7.33, which is under the finale's 6.6 |
| M3 | Major | base-06 | F9 protan FAIL: orange against the green ground, bottom-left | p10 0.101 (<0.105), min 0.086 at [136,490] (<0.09) |
| m1 | Minor | base-07 | Sky half is generic scatter; the precheck's 80% untraced overstates it (moonpath glints count) | precheck patches; composite upper half |
| m2 | Minor | base-06 | Three-bar cluster against the left wall, hairline wall gaps, one holdout | densest 49% at (165,386); gaps 13.5/13.7 at x 99; piece 47 in 13% of lost games |
| m3 | Minor | base-06 | Tower and bridge lost in fog; green ground reads as grass, not water | engine-1280; bridge arcs went from 4 to 2 |
| m4 | Minor | base-08 | 'Wrecks' invisible; figures fade at 640; 13 rainbow arcs read as a pattern | legibility 0.021 OKLab; engine-640 |
| m5 | Minor | base-09 | 90 stuck fires per 1728 (shipped max 88), with movers | precheck play WARN |
| m6 | Minor | stage | Three night-bay paintings (07, 09, 10); a moon with a peg halo at top left twice (07, 09) | sheet; jewel-pair WARN 07/09 |
| m7 | Minor | stage | Brick share under 30% on 06, 08, 09, 10 (29, 23, 26, 22%) | preflight warnings |
| m8 | Minor | stage | convert 'stale' on 06/07 (likely git order only); confirm before the owner plays | runtime level JSON updated in d6801ef8; scene JSON last changed 10 Oct |
| m9 | Minor | stage | TUNING headers record off-band targets or 'plays exhausted' | layouts/base-06.py, base-07.py |
| n1 | Nit | base-10 | Second jewel share 0.15, at the floor | precheck margins |
| n2 | Nit | stage | GLOSSARY.md and the 22 Oct owner's-answers section are missing from the worktree | charter.md headings |

## Stage (2-1 to 2-5)
Held-out wins per 48: 5.7, 6.2, 11.6, 7.0, 6.6. The mean is 7.42 against the band of 5.13-7.33. The stage passes `stage` on paper, but the curve is held together by one outlier. 2-3 will feel like a free level: only 5% of its candidates sit in the lower third, and most losses leave many oranges. 2-1 is the hardest level and 2-2 the next. The finale 2-5 is the third hardest, so the stage does not build. The coordinator answered base-08 by over-tuning base-06 (7.1 to 5.7, now 2/48 on the rule seeds, right at the floor) and base-07 (the tuner exhausted its plays). Fix in this order:
1. Retune base-08 toward about 6.5-7 with layout-preserving moves (one candidate or one blue at a time). Move some candidates into the lower third.
2. Bring base-10 to about 5.5 or below, so it clears the mean of the other four resolvably.
3. Re-run `stage mr2`.

That also lets base-06 relax off the floor if the mean allows.

Theme: a Limsa and Vesper Bay sea set. It hangs together, but 07, 09 and 10 are all night-bay paintings in purple and red, and 07 and 09 share the moon-at-top-left halo. Variety comes from technique (light path, chart points, vessel, reflection) more than from the scenes.

No Blockers. Every level PASSes. No sweep shot misses everything; the most stuck aims are 17 of 777 (base-09). The axis checks pass on all five. Every piece kind reads at 1280 and 640 on all five (F6 worst margin 0.262 or better).

## base-06 (2-1) Limsa Across the Water
- The canopy ring of six bricks around the dark tower is the one clear trace. The bottom peg row works as the procession.
- The bridge is down to two arcs, and the painting is a dark fog wash. The tower and bridge do not read at 640, and the green ground looks like grass, not water (m3).
- M3, protan: the orange candidates on the bottom row at [136,490] sit on green ground. Exclude those places from the candidates, or nudge them up off the green, then re-check F9.
- The left-wall bar cluster (m2) plays fine (14 stuck fires) but crowds the view and makes wall-hugging holdouts.

## base-07 (2-2) Moonpath on the Bay
- The strongest subject in the stage. The two long rails converge on the moon's road and the glint pegs sit on it. The arc brick haloes the moon, and the jetty piles are traced at the right.
- The sky half is uniform scatter of pegs and tilted bricks (m1). Acceptable under Peggle's rules, and lavish enough.
- Holdouts cluster on the right wall (x 688-702), each 11-13% of lost games, below the 25% WARN.

## base-08 (2-3) The Kraken's Sea
- A rich world-chart painting. The kraken (top left) and the whirlpool orbit are traced. The galleon is marked by a mast brick on the guide column.
- 13 arc bricks read as a repeated rainbow motif rather than the chart's figures.
- The wrecks are invisible (legibility 0.021), so the four tilted bricks at the bottom left trace nothing (m4).
- Main problem is M1 (difficulty). Fold m4 into that retune if cheap.
- 8 movers; their motion cannot be judged from stills.

## base-09 (2-4) The Ferry Under Sail
- Best-reading board after 07. The masts are vertical bricks, the hull a peg arc. The moon has its ring of pegs, and the constellation bricks follow star lines. Lavish and clear at both sizes.
- The ship is flat translucent polygons, plainer than the other scenes.
- 90 stuck fires, with slide movers (m5). Run `mfl.py stuck base-09` before the owner plays.

## base-10 (2-5) Twin Lanterns
- Compared with the approved 74a669c1 board, the refit dropped the full gate arch (one arc brick remains) and the mirrored arc of pegs below the waterline. The 'reflection' technique is now invisible in the start render. The lower half is loose blue scatter with four tilted bricks.
- The lanterns are small and unlit. The orange pegs on the lantern heads are a nice touch, but they vanish when hit.
- As a finale it is the least spectacular board and not clearly the hardest (M2).
- Fix: restore a mirrored trace of the gate and lanterns below the waterline (movers allowed), then tune it below the others' mean.

## Engine against pipeline
- The 1280 and 640 engine renders match the composites' layouts on all five. The composites show a mid-game moment with 14 oranges, the engine the start with 25.
- The convert 'stale' flag on 06/07 is a commit-order artifact: the runtime level JSON was updated in d6801ef8, and the scene recipes are unchanged by tuning moves. Still, run `convert --check base-06 base-07` before the owner's play-through (m8).
- Not verifiable from stills: motion of the movers on 08, 09 and 10 near the pieces, and whether runtime effects reproduce the pipeline's.

Read-only. Diffs were written to the scratchpad only: C:/Users/devon/AppData/Local/Temp/claude/C--Users-devon-Desktop-Tsukimichi--Main-Repo-/03472ffd-da4d-4ebe-abe9-5d3a5d020632/scratchpad/base-06-diff.png (also base-07, base-08 and base-10 diffs in the same folder).
