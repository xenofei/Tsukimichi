VERDICT: REVISE (0 blockers, 3 majors, 5 minors)

I worked read-only. I did not edit, build, convert or stage anything in the worktree. I ran only the built binaries, with output going to `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr4\` (files prefixed `crit*`, `play-*`, `ref-*`). The integrator's logs are in the same folder. I also copied the five worktree `json/base-16..20.json` there, over any files with the same names. The pegs and bricks in those files match the shipped `Tsukimichi.Core/Moonfall/Levels` files exactly.

What I ran:
- **Fresh games.** `mfcheck play` ran 3,456 games per level on each of two seed blocks no one has tuned on, 40001+ and 50001+: 6,912 per level.
- **Baseline.** The same play run on MR 3 (1,728 games each, block 40001+), for comparison.
- **Sweep and traces on 4-3.** A first-shot sweep at 0.25°, then a trace of every stuck angle to find where the ball lingers.
- **Renders of 4-5.** Seven MoonfallRender frames of the cleared moment, using `--cache` into my folder.

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | 4-3 | The stickiest level in the game by far. The lamp rings trap the ball. | Stuck rule fires per 1,728 games: **166 and 154** on my two blocks; the pack says 155. Approved MR 3 ranges 17-98 (3-3 at 98 was carried as a Minor), and the shipped maximum is 88. The sweep fires on 14 of 681 angles (2.1%). Lamp rings are 6 pegs at radius 34 around a centre peg, all r 9, which leaves 16 px gaps. The ball is 12 px (`BallRadius` 6), so it drops into the cell. The loader's cradling window (0.5-11.8 px) cannot see a 16 px gap. Of the 14 stuck angles, 6 linger inside a ring (within 22 px of the centres at (466,304), (632,268) and (640,456)). The rest linger on the ring rims and on the rail and parapet rows. |
| M2 | Major | 4-5 | The finale does not read as "Ul'dah from the Sands", and its east third traces art you can barely see. | The board is a top-down plan of the Steps of Nald. The "skyline" (SB) is the plan outline of the north wall, and a plan has no skyline. Engine-1280, engine-640 and the cleared render all read as a third city plan in a five-level stage, a sibling of 4-1 (also a city plan with a dashed route leaving the frame). East of x 560, about 20 pieces sit on bare magenta over a ghost of the map: the "thal ring" U and the two 3-peg "thal stair" stubs. That region holds the top two holdouts on my seeds: (567,391) in 27.5% of lost games and (598,339) in 22.9%. The hardest part of the finale is where the subject is weakest. |
| M3 | Major | 4-5 | The signature stars form a line along the frame's rim, not "stars over the roofline". | The converter's 1x `starsAt` (`convcheck.log`): 14 of 30 at y 42-44, 9 at x 722-724, 4 at x 78-90. That is 27 of 30 on the opening's edges. At 1280 there is a static 1 px pink seam along the top and right edges of the opening, unchanged across my 7 frames. 4-1 does not have it. Stars are local brightness peaks, so they settle on that seam. Fix: remove the seam, or keep `starWhere` off the rim (y ≥ 60, x ≤ 700). |
| m1 | Minor | 4-1 | Stuck rate is above the shipped maximum. | 102 and 84 per 1,728 games on my blocks (93 average); the pack says 71. The sweep fires on 10 of 681 angles. |
| m2 | Minor | 4-1, 4-2 | The top holdouts sit just above the cheap-difficulty line (y 430). | **4-1:** oranges at y 400-430 are 10% of candidates but 32% of the oranges left in lost games (ratio 3.07). 62% of lost games still hold one. (423,425) is left in 26.8% of lost games: 5 px lower, it would fail `CHEAP_ONE` (25%). **4-2:** ratio 2.00, and 64% of lost games still hold one; the top three holdouts are all at y 419-426. Approved MR 3 runs 1.31-2.63. |
| m3 | Minor | 4-2 | The right half is plain rows, and the "red airship's keel" does not read. | About 15 pieces east of x 400 (rows at y 112, 240, 345 and 490) sit on no traced feature. In the engine, the craft is graded to a dark teal smear, and the `subject` string still promises "a red airship's keel". The spire's ring carries the level well. |
| m4 | Minor | 4-4 | The protan figure passes with no margin. | p10 is **0.101**, exactly the 0.101 fallback; the minimum is 0.083, the lowest in two stages. Any regrade of the scene would fail F9. |
| m5 | Minor | stage | The ramp holds, but thinly. MR 4 is barely harder than MR 3. | On my fresh games: 27.12, 26.14, 23.75, 22.19, 18.70, each ±0.29. Steps are 0.99, 2.39, 1.56 and 3.49 (each ±0.41). 4-1 → 4-2 is 0.58 after one standard error, against the rule's 0.5. Pooled with the pack (13,824 games per level), the stage mean is 23.42 against MR 3's 23.66. That is a fall of 0.24, where charter 1.1 asks for 0.5-1.0. The finale (18.73) is no harder than 3-5's (18.57). Hardening 4-5 by about 0.7 would fix both. |
| n1 | Nit | 4-1 | The held-out ramp under-reads the opener. | Held out 25.9 against 27.12 on my seeds and 26.7 on the pack's 5,184. That is still inside the band (26.5 ± 1.2). |
| n2 | Nit | 4-3 | Our painting draws Ul'dah's great dome and minarets, though the charter row says "light only, no figure". | Charter 1.4's figure rule names creatures, people and vessels, not buildings, so this is the owner's call. 4-3 now carries the skyline that 4-5 lost, which supports renaming 4-5 (see M2). |
| n3 | Nit | 4-2, 4-4, 4-5 | The charter is stale. | The section 2 rows and section 4 still give 4-4 as the Nald map and 4-5 as base02's west strip with a skyline subject. The 4-2 row does not mention the keel. |
| n4 | Nit | 4-2 | The second jewel's share is thin. | 0.161 against the 0.15 floor. |
| n5 | Nit | 4-4 | No signature motion. | The converter keeps 0 stars, flickers, glints and mist. The charter row plans only "beams, dust", but section 1.8 asks for one signature per scene. |

## Checks that pass

- **Dead first shots:** none. No level has a sweep angle that hits nothing, and every piece is reachable.
- **Random player:** 0 wins on every level.
- **Long flights:** one, on 4-4.
- **Pre-flight and loader:** clean. Wall pinches 0, cradling gaps 0.
- **Composites against the engine:** engine-1280 matches the composite piece for piece on all five. The 640 renders read.
- **Converter gate:** `convcheck.log` passes 10 of 10 and matches `index.md`.
- **Repo gates:** `gate3.log` passes, with 9,068 tests.
- **4-4's integration change** holds on fresh games: 22.19 on mine against 22.18 pooled in the pack.

## Per-level verdicts

**4-1 The Steps of Thal (base-16): APPROVE (m1, m2, n1).**
- The walk reads as a route: in at the east arrow, through the Gold Court's ring, out at the Steps of Nald arrow. The ring stops sit on real plazas.
- The crimson grade is strong but readable (F6 worst 0.304).
- The holdouts are on the subject: Weavers (559,400), and Onyx Lane (423,425) and (389,423). They crowd the gate's line, though (m2), and the stuck rate is the second highest in the stage.

**4-2 The Spire of the Sands (base-17): APPROVE (m2, m3, n4).**
- The spire's outline ring, the lamp arch and the skyway brick trace the landmark well. It is the least sticky level in the stage (15 per 1,728).
- The east half is filler rows over cloud haze, and the keel is a smear. Tracing the haze bands' actual edges, or thinning the y 345 and y 490 rows, would help.

**4-3 Lamps on the Steps (base-18): REVISE (M1, n2).**
- It reads well: lamps in their pools, dotted handrails and parapets, the door arch. The ramp sits at 23.75 on my seeds.
- The lamp rings make it the stickiest board shipped. The ball enters the 16 px gaps and wedges against the centre peg.
- Possible fixes:
  - drop the centre pegs and keep the 6-peg rings;
  - pull the rings to about 30 units so the gaps fall under 12 px;
  - open each ring's upper pair.
- After the fix, re-measure on fresh seeds and target 88 or fewer per 1,728. Removing the centre pegs will ease the level, so check the 4-3 → 4-4 step (1.56 now).

**4-4 The Coliseum (base-19): APPROVE (m4, n5).**
- The strongest board in the stage. The brick tiers and spokes sit on the painted arena, with the pit's orange in the middle. The outer stands ring holds the holdouts: (600,397) at 24.8% and (104,292) at 18.8%.
- The low-orange share is 6% of those left, against 6% of the deal: no cheap difficulty.
- Protan has zero margin (m4).

**4-5 Ul'dah from the Sands (base-20): REVISE (M2, M3).**
- Its ramp is right for a finale: 18.70 on my seeds, a gap of 3.49 from 4-4.
- Fix the subject honestly, either:
  - re-subject and rename it around what it traces: the city wall and the three roads out to the sands (west, east and north). That would be TR or LM, and keeps the stage at 4 or more techniques;
  - or re-crop so the east third has art under it.
- Then retrace or remove the thal-ring U, which sits on near-invisible ground.
- Fix the rim seam or keep the stars off the frame (M3).
- While re-tuning, harden the level by about 0.5-1 per 48 (m5).
