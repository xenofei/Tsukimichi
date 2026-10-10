VERDICT: REVISE (0 blockers, 1 major, 1 minor)

I worked read-only at HEAD `db1e1893`, and `git status` is clean. Since round 1 (`1ad32e25`), only the level files `base-27.json` and `base-28.json`, four scene recipes and `aces.json` have changed under Tsukimichi.Core and tools, so mfcheck plays the same physics. The shipped `base-26..30.json` files are byte-identical to `docs/.../json`. My output is in `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr6r2\`.

What I ran:
- **Fresh play.** `mfcheck play`, 3,456 games per block, on seed blocks nobody has tuned on:
  - 6-2 and 6-3: my round 1 blocks (300001+, 310001+, 320001+, 330001+), a new block 400001+, and for 6-2 also 340001+.
  - 6-1, 6-4 and 6-5: the new block 400001+, pooled with my round 1 blocks, since their layouts have not changed.
  - That gives 17,280 to 20,736 games per level.
  - mfcheck is deterministic: a second run of 6-2 on 300001+ gave the same output.
- **Sweeps.** 0.25° sweeps of 6-2 and 6-3.
- **Renders.** MoonfallRender, start and cleared at 1280, of 6-2 and 6-3 with my own `--cache`. All four frames match the pack pixel for pixel (AE 0). The render DLL (17:32) embeds the current `base-27.json`: the 37 r 7.875 stars are present.

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M3 | Major | 6-3 → 6-4 | On fresh seeds the 6-3 → 6-4 step does not hold 0.5 after one standard error. This is the same failure as round 1's M2, now one step later. The 6-3 fix overshot its slot, and 6-4 sits above its own. | Fresh: 21.38 → 20.73, a step of 0.65 ± 0.25, which is 0.40 after one standard error. Pack pooled: 1.05 ± 0.41, which is 0.64. Against 6-1's fresh 24.80, the pattern slots are 21.80 for 6-3 (it is 0.42 under) and 20.30 for 6-4 (it is 0.43 over). |
| m3 | Minor | 6-2 | 6-2's measured difficulty is fragile. Its win rate swings between seed blocks far more than the binomial error, so its ± figures are understated. | Six fresh blocks: 24.78, 22.81, 22.35, 23.93, 23.19 and 23.53. The SD is 0.86, against 0.41 expected (χ² ≈ 22 on 5 df). The other levels' blocks sit at or under the binomial SD (6-3 0.30, 6-4 0.19). The stage pass's own scan shows the same cliff: faint stars at r 7.75 give 21.95, at 7.875 23.25 and at 8 23.57. The kept variant was picked on the same 6,912 games it is reported on. The pooled mean still lands on its slot (23.43 against 23.30), so the ramp itself holds. |
| n6 | Nit | 6-2 | The hardening raised the low orange's holdout share. It is now the level's top holdout. | (526,440), below the y 430 line: left in 13.6% of lost games in round 1 and 22.1% fresh now, under the 25% gate. Stuck-rule fires per 1,728 games rose from 22.5 to 47.9, under the 88 maximum. |
| n7 | Nit | 6-3 | The top holdout has crept toward 25%. | (214,307) is left in 24.2% of lost games fresh (22.5% in round 1). It sits above y 430, so no gate applies. |
| n2 | Nit (carried) | 6-1 | The top holdout is just over the 25% line. | (250,415): 25.7% over five blocks. Accepted, with the reason given in the response. |
| n3 | Nit (carried) | 6-5 | The beams still cannot be seen in any still frame. | k 0.06, 22-30 wide. Nothing shows over the gate in cleared-1280. Motion is unverified. |
| n4 | Nit (carried) | 6-4 | The random player wins 1 of 40. | Every block gives 1. 6-2's random player now wins 0. Accepted. |
| m2→n | Nit (carried) | pipeline | `stagecheck` still does not check the stage-to-stage mean fall. | `index.md` now reports the fall (0.60). The response defers the gate rule to the pipeline's owner, with known-bad and known-good cases. That is acceptable. |

## Round 1 findings checked

- **M1 (the stage mean falls only 0.35): resolved.**
  - Fresh means: 24.80, 23.43, 21.38, 20.73 and 17.21. The stage mean is 21.51.
  - The fall from MR 5 (22.08 fresh) is 0.57. The pack's pooled fall is 0.60.
  - Both ends fall: the opener from 25.44 to 24.80, and the finale from 17.41 to 17.21 (within noise, but the point estimate falls).
  - The ease-back from 5-5 is 7.39, inside 7.0-7.5.
- **M2 (the 6-2 → 6-3 step): resolved.** Fresh: 23.43 → 21.38, a step of 2.05 ± 0.25, which is 1.80 after one standard error. 6-3's fix overshot, and the overshoot causes M3.
- **m1 (6-3 the stickiest level): resolved.**
  - Stuck-rule fires per 1,728 fresh games: 83.8 → 64.4.
  - The sweep sticks on 5 of 681 angles (was 8). There are no long flights.
  - The ring moons at r 7.5 (Mor Dhona's r 7) still read as rings on the cities in engine-1280.
- **m2 (`stagecheck` does not check the mean fall): partly fixed.** See the carried nit in the table.
- **n1 (6-2's 0° shot bounces for 17 s): resolved.** The top ray's moon is at x 401. My 0.25° sweep shows 0 flights over 15 s and 3 stuck angles of 681, at −38.75, 13.5 and 28°.
- **n5 (6-4 protan 0.092): resolved.** The report gives a minimum of 0.106 at the same place.

## Checks that pass

- **Dead first shots:** none. Every sweep angle hits a peg. 6-2 reaches 0 of 68 pieces unreached and 6-3 0 of 73. 6-2's sweep mean is 7.6 and its median 7, so the thin board is fixed.
- **Long flights:** none on any level.
- **Stuck per 1,728 fresh games:** 42.9, 47.9, 64.4, 7.3 and 25.8, all under 88.
- **Fresh ramp:**

  | Level | Fresh /48 (games) | Blocks | Pack pooled |
  |---|---|---|---|
  | 6-1 | 24.80 (17,280) | 24.44-25.15 | 24.7 |
  | 6-2 | 23.43 (20,736) | 22.35-24.78 (m3) | 23.25 |
  | 6-3 | 21.38 (17,280) | 21.08-21.79 | 21.62 |
  | 6-4 | 20.73 (17,280) | 20.46-20.97 | 20.57 |
  | 6-5 | 17.21 (17,280) | 16.94-17.65 | 17.4 |

  - Each fresh figure is ± 0.17-0.18. The steps are 1.37, 2.05 and 0.65 (M3), and the finale gap is 3.52.
  - After one standard error is taken off: 1.12, 1.80, 0.40 and 3.27.
  - The opener and the finale are inside their bands (25 ± 1.2 and 17 ± 1.2).
  - The pack's pooled figures are within 0.25 of mine on every level.
- **Low oranges (y 430 or below), fresh:**
  - 6-1: (699,454) 11.1% and (101,454) 10.0%
  - 6-2: (526,440) 22.1% (n6)
  - 6-3: none
  - 6-4: under 1% (movers)
  - 6-5: (182,435) 11.0%
- **Engine against composite:**
  - engine-1280 matches the composite piece for piece on all five levels. My 6-2 and 6-3 re-renders match the pack (AE 0).
  - 6-2's six bricks sit on the ticked band just inside the inner rule, as the docstring describes. The two lower bricks lie on the rule.
  - At 640, 6-3's ley dashes, stars and ring moons still read, and the cloud sea reads as cloud over green land.
- **Pipeline:**
  - Recorded in the pack, not re-run by me: all five reports PASS with an empty `fails` list. `stage mr6` PASS (pooled 1.45, 1.63, 1.05 and 3.17). The converter's 10 of 10 is inside its limits; 6-3's 1x p99 of 0.029 against 0.035 is the closest. ch-03 wins 27 of 48.
  - Checked by me: the stage pass changed only radii in 6-2 (places, the 35 candidates and the bricks are unchanged).

## Per-level verdicts

**6-1 The Armillary (base-26): APPROVE.**
- The layout is unchanged: 24.80 fresh.
- The gold frame and the amber dusk separate it from 6-5, and the spokes now reach their moons, so they read as a frame rather than scatter.
- In stills the dusk reads as two soft amber glows either side of the stand. They sit under the lowest pegs, not behind the low oranges (101,454) and (699,454), so they are not a readability problem.
- The game designer still needs to look at the F6 margin of 0.205 (purple at 0.8x).

**6-2 The Wheel of the Twelve (base-27): APPROVE, with m3 and n6 noted.**
- 23.43 fresh is on its slot.
- The rim bricks trace the wheel's ticked inner ring, and the twelve sign figures still read on the cleared board.
- The tuning sits on a knife edge (m3). Any later change to the engine or the radii can move it by about 1 per 48. Re-measure 6-2 first after any physics change.
- The hardening came partly from the low orange (526,440), which is now the top holdout (n6). If 6-2 ever needs easing, start there.

**6-3 The Seer's Chart (base-28): APPROVE.**
- Its round 1 findings are resolved: it is no longer the stickiest level, the rings let the ball fall through, and the right-hand ornaments were not used to harden it (17.6% and 16.8%).
- At 21.38 it now sits 0.4 under its slot. That is acceptable for 6-3 alone, but it feeds M3.

**6-4 Meteor Shower (base-29): REVISE (M3).**
- 20.73 fresh, 0.43 above its slot.
- The fix belongs here, because it costs nothing elsewhere:
  - Harden 6-4 by about 0.4, to about 20.3. The step from 6-3 becomes about 1.05 (0.8 after one standard error).
  - The finale gap stays at about 3.1 (2.85 after one standard error), and the stage mean falls a further 0.08.
  - Easing 6-3 instead would bring the mean fall back to the 0.5 edge.
- As in round 1, keep the peak-moon holdouts (694,423), (232,415) and (182,429) above y 430. Harden through the streak or slope moons, not by burying those three.
- The lake now reads as a dark mirror, with no scanlines in engine-1280.

**6-5 The Moon Gate (base-30): APPROVE.**
- The layout is unchanged: 17.21 fresh, finale gap 3.52.
- The far moon reads as a small full moon inside the gate, and the bluer far sky does not move any piece.
- The beams still need someone to watch them in motion (n3).

**Stage: REVISE (M3 only).** The fold-in fixed both round 1 majors on fresh seeds, and the stage is now 0.57 harder than MR 5. The last step to fix is 6-3 → 6-4: 6-3 came in under its slot and 6-4 sits over its own. Take about 0.4 out of 6-4, then re-measure on seed blocks that nobody has tuned on. Do not re-measure on the 6,912 pooled games, which have now been used for selection on 6-2 and 6-3.
