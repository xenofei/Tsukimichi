VERDICT: APPROVE (0 blockers, 0 majors, 7 minors)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 (r2) | closed | 1-3 base-03 | Stuck rule: fixed | Held-out fires went from 98 to 46. On fresh seeds 2593-4320 there were 37 fires (121 in round 2), and on seeds 4321-6048 there were 51. That averages 44 per 1728 games, under the WARN line of 70. `mfl.py stuck`: 2 of 777 aims (6 in round 2) |
| m1 (r2) | closed | 1-2 base-02 | Brick share: fixed | `preflight.bricks` 30 of 100, `brick_share` 0.30, `warnings` [] |
| m2 (r2, partly fixed) | Minor | 1-2 base-02 | (542, 487) is now never orange, but the F9 protan minimum is still 0.078. It sits on (504, 478), the next brick up the same leg, which is still an orange candidate. The response quotes the 10th percentile (0.115) as if it were the minimum. This is UX's call. | report `readcheck.report.protan_min` 0.078, `protan_worst_at` [brick, 504, 478], `protan_orange_p10` 0.115. json/base-02.json: brick (504, 478)-(539.8, 486.6) has `canBeOrange: true`. Precheck margins WARN 0.078 |
| m8 (new) | Minor | 1-2 base-02 | Lengthening the road legs added stuck rests, though the counts stay well inside the rule | Sweep: 6 of 777 aims (3 in round 2). Three of the rests are on the new legs: (215, 430) at the west leg's lower end, and (620, 420) and (655, 410) on the east leg at the joins between segments (x 621-623 and 661-663). Fires: 34 held-out (25 in round 2); 36 and 33 on the two fresh blocks (20 in round 2) |
| m3 (r1 m2, open) | Minor | 1-3 base-03 | 46% of the pieces still trace nothing. The holdout zone is smaller, because the tuning now forces the old top-right holdouts blue. | Precheck: 45 of 98 untraced. Lost games that leave an orange at x ≥ 620, y ≤ 300: 57.8% in round 2, now 39.2% and 42.3%. TUNING `blue` [(669.9, 204.8), (669.9, 239.2), (690, 280)]. Top piece #32 (640, 222): 14.8% and 18.6% |
| m4 (r1 m3, open) | Minor | 1-4 base-04 | The right-wall column is unchanged and accepted as difficulty | Fresh block 4321-6048: (684, 392) 16.9%, (684, 352) 14.9%. Under the 20% wall-side rule |
| m5 (r1 m4, open) | Minor | 1-1 base-01 | Brick share is 22%. The docstring gives the reason. | `preflight.brick_share` 0.224, unchanged |
| m6 (r1 m5, open) | Minor | all | The densest 90 x 90 window is still at the FAIL edge on 1-2. This is the coordinator's call. | Precheck: 1-2 45% at (225, 326); 1-3 40% at (225, 221) |
| m7 (r1 m6, open) | Minor | all | F9 protan minima are under 0.10. This is UX's call. | 1-1 0.075, 1-2 0.078 (m2), 1-3 0.098 at (515, 389), 1-4 0.083, 1-5 0.098 (on a brick that is never orange) |

Nits, no action needed:
- **1-2 Ace in the pack render.** `base-02-engine-1280.png` shows the Ace chip at 180,000, but `aces.json` now ships 200,000.
    - The pack was made at 08:23 (index.md), and the Ace edit landed later, at 08:40 in 73ab2ac1.
    - aces.json is an embedded resource, so the render came from a DLL built before the edit.
    - The layout in the render is current. The in-engine Ace display is not verified.
- **1-3 reads easier on fresh seeds than its locked figure.**
    - Held-out: 8.86. Tuning seeds: 9.7 (TUNING comment). My two fresh blocks: 9.06 and 10.31.
    - Pooled over 6048 games it is about 9.45, so 1-3 is the stage's easiest board.
    - No rule binds a single level, and the stage mean stays in band (below). A higher held-out figure would come from the selection luck of `tune --hold`, which tunes against the held-out block.
- **The response's stage figures don't match the reports, again.** It gives pooled 8.05 and a finale of 5.52 against 8.69. The committed reports give 7.93 and 5.33 against 8.58. Both pass.
- **1-2's lower third.** Candidates in the lower third rose from 23% to 28%, close to the 30% cap.
- **1-2's wall gaps.** The 13.9-unit gaps at (99, 414) and (701, 414) are unchanged; the legs' wall ends did not move. A Peggle board has a similar 13.5 gap.
- **1-3's brick share** is 29.6%, the same 29 bricks as before; one peg was added.

## Stage figures

| | 1-1 | 1-2 | 1-3 | 1-4 | 1-5 (finale) | pooled |
|---|---|---|---|---|---|---|
| held-out 865-2592 (report) | 7.89 | 9.22 | 8.86 | 8.33 | 5.33 | 7.93 |
| fresh 2593-4320 | 8.14* | 9.42 | 9.06 | 8.22* | 5.72* | 8.11 |
| fresh 4321-6048 (new) | 8.06 | 8.25 | 10.31 | 8.67 | 5.75 | 8.21 |
| both fresh, 3456 games | 8.10 | 8.84 | 9.68 | 8.45 | 5.74 | **8.16** |
| rule seeds 1-48 | 5 | 14 | 8 | 8 | 7 | |
| stuck fires: held / fresh A / fresh B | 20/17/18 | 34/36/33 | 46/37/51 | 23/31/33 | 10/9/4 | |
| top holdout per piece (fresh B) | 17.9% | 15.2% | 18.6% | 16.9% | 13.6% | |

\* Round-2 runs, still valid because those levels are byte-unchanged (`git diff --stat` touches only base-02 and base-03).

- **Band (7.47-9.67).** The stage mean is inside on every seed set: held-out 7.93, fresh 8.16 over 3456 games. The margin to the lower edge is 0.46-0.69.
- **Finale.** On fresh seeds the other four average 8.77 against 1-5's 5.74, a gap of 3.03. On held-out seeds it is 8.58 against 5.33. It holds clearly.
- **Playability floor.** Every level wins 5-14 of rule seeds 1-48 against a floor of 2.
    - 1-2's jump from 4 to 14 is luck on the tuning block, since its held-out figure is 9.2.
    - My seeds 4321-4368 gave 1-2 2 of 48, so its single-block spread is wide, but the rule reads seeds 1-48.
- **Dead first shots, reach and the fast check.** `mfl.py check --json` passes on all five: 0 dead first shots of 777 aims and no unreached piece.
- **Holdouts.** No piece is above 18.6% of lost games (WARN at 25%).
- **Aces.** These are 200k, 200k, 210k, 210k and 190k.
    - The response's figure for 1-2 is 126 of 576 games, and it ran the C# scorer.
    - Whether each Ace sits at the top 40% of wins is not checked here: mfcheck scores differently, so its numbers are not comparable.

## Per level

**1-1 Road to Horizon (base-01): APPROVE (m5)**
- The pipeline files are unchanged. Only the pack re-rendered it, and the engine PNG differs by bytes only.
- Fresh block B gives 8.06 and 18 fires, consistent with round 2.

**1-2 Horizon by Night (base-02): APPROVE (m2, m8)**
- The brick share is fixed (m1).
    - Both road legs now run from the cliff wall to x 217 and x 583, in three bricks each.
    - The switchbacks read better: in the engine at 1280, each upper leg is a full-length road with its hairpin open.
    - The engine render matches the composite.
- **Water tower.** It is unchanged since round 2: an arc crown, two moons inside, and the two stilt bricks.
- **The m2 fix moved the problem rather than removing it.** The pipeline's protan low point is now the neighbouring candidate (504, 478) at 0.078, which is what UX round 1 n1 measured. Excluding that brick too would close it if UX wants it closed.
- **Difficulty.** Locked at 9.2 held-out, 8.84 on fresh seeds; it is not the stage outlier.
- **Stuck balls rose modestly (m8)**, to about 35 fires per 1728 games. The two east-leg rests sit at the joins between segments.
    - On a 15-degree leg a ball should not rest, so this looks like a slow roll across the join that triggers the stuck rule.
    - A single longer brick, or a 2-brick east leg, would test that. It is not required.

**1-3 The Cactuar (base-03): APPROVE (m3)**
- M1 is fixed, as suggested in round 2.
    - The west crowns' summit gap is now 115 (it was 70).
    - Both east crowns now end at x 640 (base-03.py lines 115-118); the summit peg is at (685.3, 385.3).
    - Fires fell to about 44 per 1728 on fresh seeds and sweep rests to 2. Both rests are on the near-dune floor at (180-195, 470-475), not on the crowns.
- **Tracing.** In the engine at 1280 the dunes are still traced: shorter crown stubs on each painted crest. The east dunes now read as a single short brick each plus a peg, which is thinner but still on the crest line.
- **The figure.** The Cactuar and its arm beams are unchanged.
- **Scene and composite.** The scene's lights were re-laid in `sagolii-cactuar.json`, and the engine render matches the composite.
- **Difficulty.** It was held at 8.86 on held-out seeds, but it plays nearer 9.5 elsewhere (see the nits).
- **m3.** The holdout half of m3 has improved through the tuning. The tracing half (46% untraced) is unchanged, and the response defers it.

**1-4 The Gilded Dome (base-04): APPROVE (m4)**
- It is unchanged. Fresh block B gives 8.67 and 33 fires.

**1-5 The Crystal's Call (base-05): APPROVE**
- It is unchanged. Fresh block B gives 5.75 and 4 fires; the top piece is #54 (370, 455) at 13.6%.
- The 40% cluster at (440, 332) in my position-keyed tally is several pieces that share one reference point. Counted by piece, no single piece is above 13.6%.

## What I ran (read-only)
- `mfl.py status --stage mr1`
- `mfl.py check base-01..05 --json`: PASS ×5
- `mfl.py precheck base-02/03`
- `mfl.py stuck base-02/03`
- The prebuilt mfcheck `play <shipped json> 1728 <n> 2592` (base-02 and base-03), and `play ... 4320` for all five levels
- The diff images and the engine renders at 1280 for base-02 and base-03
- The report and json figures, and `git diff ba67b767..73ab2ac1` on the layouts and aces.json

`git status` in the worktree is clean at 73ab2ac1. My outputs are in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/r3/`: play-f1-N.txt, play-f2-N.txt, check-N.json, precheck-N.txt and stuck-N.txt.

Not verified:
- motion in the game itself;
- where balls stall after the first shot;
- the Aces on the C# scorer, and the in-engine Ace chip for 1-2, because the pack render is older than the Ace edit;
- the protan figures in the engine.
