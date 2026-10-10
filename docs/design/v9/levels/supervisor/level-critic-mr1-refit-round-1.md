VERDICT: REVISE (0 blockers, 1 majors, 6 minors)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | 1-3 base-03 | The stuck rule fires 154 times per 1728 held-out games and 185 on fresh seeds. Precheck FAILs at 100; the shipped maximum is 88. Balls stall on the flat crests of the right dunes by the wall and on the left mid-dune's summit shoulders. | Report play.stuck_fires 154. Fresh seeds 2593-4320: 185. Sweep: 17 of 777 aims stuck. `mfl.py stuck`: rests at (655-695, 280-370) on arcs #90-92 (crowns at layout lines 117-118, which are closed) and at (160-250, 360) by arcs #85/#86 |
| m1 | Minor | 1-2 base-02 | The stuck rule fires 80 times held-out and 96 on fresh seeds (WARN 70, FAIL 100). Balls roll slowly down the about-15-degree switchback slant from the left wall. | Bricks #88-90 from (99, 414) to (236, 452). Rests at (195-270, 425-455), 20 of 777 aims. Precheck WARN play |
| m2 | Minor | 1-3 base-03 | 46% of the pieces trace nothing (WARN 30%, FAIL 50%). The top-right peg block (about 25 pegs, x 500-700, y 100-290) is a free grid. | Precheck patches: 45 of 98 pieces, base-03-untraced.png |
| m3 | Minor | 1-4 base-04 | The right-wall column is the board's main holdout zone. It is five vertical bricks at x 684 with only the inner face in play. | Bricks #92-96 at x 684, y 232-430: an orange among them is left in 46% of fresh lost games (53% with peg #59 at (700, 196)). Each piece is at most 16%, so the wall-side rule (20%) passes |
| m4 | Minor | 1-1 base-01 | Brick share is 22%, under the 30-45% guidance. The layout docstring gives a reason (trail of places; t 20 leaves no road stretch steep enough for brick). | preflight.warnings; layouts/base-01.py docstring |
| m5 | Minor | all | The densest 90 x 90 window is 42-45% on every level. 1-2 sits at the FAIL edge (45% at (225, 326), the derrick). These thresholds were calibrated on sparse boards; the coordinator should recalibrate them, not the authors. | Precheck patches WARN x5; README "recalibrate on the first refitted stage" |
| m6 | Minor | all | F9 protan minimum is 0.075-0.099 on all five. 1-5's p10 is 0.104, under UX's 0.105 guard (it passes only on the pilot fallback). This is UX's call. | Precheck margins WARN x5 |

Nits, no action needed:
- 1-1, 1-3 and 1-4 share a jewel family. They are not neighbours, so this is a repeats WARN only.
- 9 of 777 first shots on 1-4 rest on the dome apex at (430-475, 145-160). That is 23 stuck fires per 1728, harmless.
- Candidate shares (61-66%) sit at the low end of 60-90%.
- 1-2's lower third is 29.5% against a 30% limit.

## Stage figures (playability under Peggle's rules)

| | 1-1 | 1-2 | 1-3 | 1-4 | 1-5 (finale) | pooled |
|---|---|---|---|---|---|---|
| held-out 865-2592 (report) | 8.4 | 9.0 | 8.8 | 8.3 | 5.8 | 8.06 |
| **fresh 2593-4320 (my run)** | 8.36 | 9.75 | 8.17 | 8.22 | 6.89 | **8.28** |
| both, 3456 games | 8.36 | 9.38 | 8.49 | 8.28 | 6.32 | 8.16 |
| rule seeds 1-48 | 7 | 10 | 6 | 8 | 14 | |
| stuck fires held / fresh | 26 / 12 | 80 / 96 | **154 / 185** | 23 / 31 | 7 / 10 | |

How the fresh games were played: prebuilt mfcheck `play <json> 1728 <n> 2592` on each level's shipped file. `docs/design/v9/levels/json/` and `Tsukimichi.Core/Moonfall/Levels/` are byte-identical.

- **Band.** The stage mean is 8.28 on fresh seeds and 8.06 held-out, both inside 7.47-9.67. On fresh seeds 1-2 alone reads 9.75, above the band's upper edge, but the band applies to the stage mean.
- **Finale.** The other four levels average 8.63 per 48 on fresh seeds against 1-5's 6.89. The gap is 1.74, the standard error about 0.46, so the gap less one standard error is +1.28 and the rule holds. On held-out seeds the gap is 2.8. Peggle's MR 1 finale is 4.42 against a mean of 8.57, so ours is milder but inside the rule.
- **1-5 on seeds 1-48.** It wins 14 of 48 there against 5.8 held-out (about +3.6 SD). Those seeds sit inside the tuning block (1-864), so this is tuning luck, not a defect. The held-out and fresh figures are what count.
- **Playability floor.** Every level wins 6-14 of 48 against a floor of 2.
- **First shots.** No dead first shots on any level (0 of 777 aims), and no piece goes unreached. A first shot touches 11.5-15.6 pieces on average (WARN under 7.5).
- **Holdouts.** No single piece exceeds 16% of lost games, held-out or fresh.
- **Fast check.** `mfl.py check` passes on all five.

## Engine renders against the composites

All five match the pipeline composites piece for piece and in their art.
- Convert showed "stale" when the pack was made, by git order only. It reads fresh after `98e376f0`, and the runtime JSON is byte-identical to the pipeline's.
- The Aces in the renders match `aces.json` (190k, 180k, 220k, 210k, 180k).
- Whether each Ace is at the top 40% of wins is not checked here. `MoonfallAces.Suggest` scores with the C# playability report, not mfcheck, so I could not reproduce it. An mfcheck estimate on my fresh wins reads 160k, 150k, 170k, 190k and 180k, but those numbers are not comparable.
- At 640, 1-4 is the hardest board to read (blue pegs on blue-grey architecture). Its F6 worst margin is 0.264, which passes.

## Per level

**1-1 Road to Horizon (base-01): APPROVE with m4**
- The map trail survives the refit:
    - places are rings of moons;
    - the road is dotted between them;
    - the coasts and hills are brick;
    - the compass rose and sea lane are the chart's own, blue and kept off the road.
- At r 10 the place rings read more as clusters than rings, but the route from the Waking Sands east and down toward Ul'dah is legible on the 1280 render.
- Its play is the cleanest of the stage: stuck 2 of 777 aims and 12-26 fires.
- The HARD exclusion list (the hardest places left blue) is a fair way to tune difficulty.

**1-2 Horizon by Night (base-02): APPROVE with m1**
- The derrick (an A-frame in brick) and the water tower (a brick arch on stilts) trace the subject well.
- The roofline itself is only lightly traced; pegs sit over the roofs rather than along their edges.
- The two big sky arcs frame the scene but do not show a Milky Way that is visibly painted.
- The lower funnels and the peg triangle between them hold the main holdouts: pieces #31-34 on row y 462, together 40% of lost games, ordinary for a group of that size.
- The stuck fires are the real concern: 96 on fresh seeds is close to FAIL. Steepening the left switchback slant (or breaking it at its middle) would cut them.

**1-3 The Cactuar (base-03): REVISE (M1, m2)**
- The figure is traced well: arm and leg bricks, a face from three pegs, dune crowns in arc brick.
- M1: the two right-hand dune crowns (`crown(..., 625, 700, 0, "mid dune")` and `(..., 624, 696, 0, "far dune")`) are closed. Their near-flat crests sit against the right wall, where balls stall.
- The left crowns are already opened at their summit; open the right ones the same way, or end them short of the crest. Check the left mid-dune's shoulders at (160-250, 360) as well.
- Fixing it should take stuck fires below 100 and the sweep's 17 stuck aims to the single digits, as on the approved shipped board (12 of 681 at most).
- m2: the top-right peg block traces nothing. Shaping it into a second ripple field or a moon halo would also break up that wall-side holdout block (pieces #31, #33, #34 and #35 at x 640-670: 51% of fresh lost games together).
- Re-check the band after the fix: removing stuck balls changes the win rate.

**1-4 The Gilded Dome (base-04): APPROVE with m3**
- The great dome reads through its brick arc.
- The inner arches and the paired west buttresses add structure, and the board is the densest (105 pieces).
- The right-wall column (m3) is legitimate Peggle-style difficulty but is the one wall-hugging hotspot. Making one or two of its segments never-orange would soften it if 1-4 needs to move later.
- Stuck play is fine: 9 rests on the dome apex, 23-31 fires.

**1-5 The Crystal's Call (base-05): APPROVE**
- The constellation crystal is clearly traced:
    - outline pegs, with larger star pegs at its vertices;
    - three brick facets inside;
    - painted constellation lines.
- The flanking arcs and peg rings frame it.
- The finale passes resolvably on both held-out and fresh seeds.
- Play is clean: stuck 1 of 777 aims, 7-10 fires, holdouts spread evenly (top piece 12%).
- The band margin of -1.7 in `status` is the level against the stage band. It is expected for a finale and is not a fault.

## What I ran
- `mfl.py status --stage mr1`
- `mfl.py precheck base-01..05` (its crop PNGs went to the gitignored build/precheck folder)
- `mfl.py check --json` on all five
- `mfl.py stuck base-02/03/04`
- mfcheck `play` on 1728 fresh seeds per level

Nothing in the worktree changed: `git status` is clean at `76c94f40`. Scratch output is in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/` (fresh-*.txt, precheck.txt, stuck-*.txt).

Not verified: in-game motion, the Ace suggestion on its own C# player, and anything at runtime beyond the still renders.
