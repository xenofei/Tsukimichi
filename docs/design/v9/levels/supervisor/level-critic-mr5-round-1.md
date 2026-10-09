VERDICT: APPROVE (0 blockers, 0 majors, 6 minors)

I worked read-only in the worktree at HEAD `9404548d`, with no edits, builds or pipeline runs. Since `mfcheck.exe` was built at 19:15, the only Core change is the recipe loader (`MaxMaskTerms`, shaft `near`), so physics is unchanged. I passed it the shipped `Tsukimichi.Core/Moonfall/Levels/base-21..25.json`, which are byte-identical to `docs/.../json`. The render DLL's embedded levels, scenes and `aces.json` match the repo files exactly.

What I ran (output in `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr5crit\`):
- **Fresh games:** `mfcheck play` on four seed blocks no one has used (200001+, 210001+, 220001+, 230001+), 3,456 games each, so 13,824 per level.
- **Sweeps:** a 0.25° sweep on every level, and a trace of every stuck angle to find where the ball lingers.
- **Renders:** MoonfallRender with my own `--cache`: all five at 1280 (start), 5-3 cleared and 5-2 at 640. All seven are pixel-identical to the pack (AE 0).

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| m1 | Minor | 5-3 | At 1280 the lamp reflections look like dull brown streaks, not light, and the grade turns the lamps and windows cold white. This is the level's whole subject. | Trace sheet: the windows and reflections are warm amber. Composite, engine-1280 and cleared-1280: the windows are blue-white, and the reflections are taupe on teal water (brightest pixels about (118,108,94)). In the cleared board the lamp heads are cold while their reflections are pinkish tan. At 640 they still read as warm streaks. Fix: raise the reflections' brightness (cream, low chroma), not their hue, so they stay out of the orange family. |
| m2 | Minor | 5-5 | A hard horizontal edge runs from the west wall to the Aleport light at board y 404. | The luma steps 6-9 at screen y 542-544 along x 240-520 in cleared-1280. The same step is in engine-1280 (61→78) and in the composite at y 402-404 (65→74). It sits exactly at the Aleport shaft's origin y (334,404), whose beams are at 120° and 180° with `near` -170, so it looks like the cut where the shaft starts. It is in the same place in the start and cleared frames, so the moving beam does not hide it. |
| m3 | Minor | 5-3 | The last jetty brick makes a ledge against the right wall. | The brick ends at (706,373), thickness 10, which leaves 13.5 px to the wall (724.5) for a 12 px ball, so its centre has a 1.5 px window to pass. Two of the 3 stuck sweep angles (35.75° and 75.5°) sit in that corner for 140-160 ticks before the stuck rule clears a piece. The loader only checks wall pinches for round pegs. Fix: end the run about 20 px from the wall. Play stuck is 38 per 1,728, under the maximum, so this is not urgent. |
| m4 | Minor | stage | Measured on fresh seeds, the mean falls only 0.36 from MR 4, against the charter's 0.5-1.0. | Fresh means: MR 4 22.81 (my MR 4 round 3 figure) and MR 5 22.45. The pipeline's 0.58 compares against MR 4's pooled 23.07, which was 0.26 generous. MR 5 is on its charter mean (22.6 ± 0.2), so the shortfall comes from MR 4 sitting under its own mean. This is the game designer's call. The record should quote the fresh figures. |
| m5 | Minor | 5-2, 5-5, 5-3 | Protan contrast for the oranges passes only through the 0.101 pilot fallback, with no margin. | p10 / minimum: 5-2 0.101 / 0.099 (exactly the fallback), 5-5 0.102 / 0.099, 5-3 0.109 / 0.108. Three of the five levels are under the 0.12 floor. Any regrade will fail F9. This is the same pattern as MR 4's m4. |
| m6 | Minor | 5-4 | 20 of 83 pieces are filler that traces nothing. | From `layouts/base-24.py`: 8 "cloud" moons, 4 "mist" moons, 2 piers, 3 tower-wall moons, a tower foot and 2 under the arches. They make a scatter in the upper-left sky and a loose bottom row. The bridge still reads through the deck band, the arch bricks and the tower legs. |
| n1 | Nit | 5-1 | The bay haze at the castle's foot is saturated sea-green and reads as a lawn. | engine-1280 and cleared-1280, y 640-770. |
| n2 | Nit | 5-2 | Two-peg gaps on the ferry lane and Aldenard coast hold the ball briefly. | The sweep sticks on 11 of 681 angles. The rest points are (214,246), (238,257) ×2, (298,240) ×2 and (407-436, 262-315): all between two pegs with about 16 px surface gaps. Play stuck is 56 per 1,728, under the maximum of 88. |
| n3 | Nit | 5-5 | The finale's top holdout is right at the 25% line. | (459,390) on the bay shore is left in 25.9% of lost games on fresh seeds (pack: 25%). It is above y 430, so the cheap-difficulty gate does not apply, and it is the finale's knob. |
| n4 | Nit | stage | 5-1, 5-3 and 5-5 use nearly the same jewel pair. | 273/189, 255/171 and 250/181. The neighbour rule passes, but the stage alternates only two palettes. |
| n5 | Nit | stage | The ease-back into the stage is larger than planned. | On fresh seeds 4-5 is 17.55 and 5-1 is 25.84, an ease-back of 8.3 against the charter's 7.0-7.5. Both values are inside their bands. |
| n6 | Nit | 5-2 | The "south compass" ring at (240,450) is invented on bare sea. It mirrors the chart's real compass at (610,410). | Trace sheet. |
| n7 | Nit | 5-1, 5-4, 5-2 | Several pipeline margins are thin. | Converter gate: 5-1 and 5-4 2x maximum 0.039, 5-2 p99.9 0.015 and 1x p99 0.035. 5-1 second-jewel share 0.154 against the 0.15 floor. 5-1 scene ceiling 0.441 against 0.465. |

## Checks that pass

- **Dead first shots:** none. Every sweep angle hits a piece, and every piece is reached by some first shot (0 of 69, 77, 79, 83 and 79 missed). No flight is over 15 s.
- **Stuck per 1,728, fresh:** 15, 56, 38, 31 and 62, all under the maximum of 88. Sweep stuck angles: 1, 11, 3, 0 and 5 of 681, the same as the pack.
- **Ramp, fresh, 13,824 games each:**

  | Level | Fresh /48 | Four blocks |
  |---|---|---|
  | 5-1 | 25.84 | 25.4-26.1 |
  | 5-2 | 24.21 | 23.8-24.7 |
  | 5-3 | 22.69 | 22.1-23.2 |
  | 5-4 | 21.36 | 21.2-21.6 |
  | 5-5 | 18.14 | 18.1-18.3 |

  - Each figure is ± 0.20.
  - Steps are 1.64, 1.52 and 1.33, and the finale gap is 3.22 (each ± 0.29). Each step is still at least 1.04 after one standard error is taken off, and the finale gap 2.94.
  - The opener (26 ± 1.2) and the finale (18 ± 1.2) are inside their bands. The pipeline's pooled figures agree within 0.4 on every level.
  - The 5-2 → 5-3 step, which integration retuned, is 1.52 fresh: comfortably resolved. The (461,371) candidacy is in the shipped file, and the coping bricks are not candidates.
- **Random player:** 0 wins of 40 on every level, in every block.
- **Holdouts and cheap difficulty, per piece and fresh:**
  - No orange at y 430 or lower is left in more than 14.6% of lost games (5-3's (689,446)). 5-1 and 5-2 have no low candidates.
  - The share of low oranges among those left is 0, 0, 0.054, 0.040 and 0.032.
  - Top holdouts: 5-1 (118,418) 23.0%, 5-2 (134,242) 22.2%, 5-3 (172,150) 22.4%, 5-4 (652,176) 21.9%, 5-5 (459,390) 25.9% (n3).
- **ch-11 duel (5-1 to 5-3):** both sides carry oranges. Candidates split left/right 15/12, 15/12 and 13/13, and the oranges left in lost games split 0.47, 0.49 and 0.55. I did not run duel mode itself, so that is unverified. 5-4 (not a duel carrier) leaves 65% of its holdouts on the right, in the hard tower top: noted, no action.
- **5-5 movers:** two orbit pairs at radius 61 around the Aleport and east-cape lights. They clear the inner rings by 15 px of surface.
- **Composites against the engine:** engine-1280 matches the composite piece for piece on all five. The 640 renders read on all five. 5-2's new foot haze reads as a faint violet mist at 640 and in the cleared board, unobtrusive.
- **Pipeline:** all five reports PASS with no fails. Pre-flight problems 0, loader wall pinches and cradling gaps 0, framecheck clean. `stage-mr5.txt` matches the reports, and `index.md` matches both.

## Per-level verdicts

**5-1 The White Towers (base-21): APPROVE.** 25.84 fresh, 15 stuck per 1,728, and the sweep sticks on 1 angle, the cleanest level of the stage. The rigging on the west flank, the spire runs and the gilt tower's window columns trace the painting. The east cloud crowns are a declared invention. In the cleared board the white towers read clearly, with the floor-traded palette. n1, n7.

**5-2 The Strait of Merlthor (base-22): APPROVE.** 24.21 fresh. The coasts, the dashed ferry lane and the Ul'dah roads all sit on the chart's own lines. The bottom 15% of the board is empty water above the bucket, which is fine. The highest sweep stuck rate in the stage comes from two-peg gaps (n2). Protan has no margin (m5). n6, n7.

**5-3 Lights on the Water (base-23): APPROVE.** 22.69 fresh. Nine reflection columns hang from the quay lamps, with the lamp heads, the loggias and the jetty as structure: the light-trace technique done literally. Its weakness is the grade (m1): at 1280 the light reads as brown streaks. Fix the jetty ledge (m3) when convenient.

**5-4 The Arches of the Bridge (base-24): APPROVE.**
- 21.36 fresh. Stuck is 31 per 1,728, and the sweep sticks on 0 of 681 angles.
- The arch bricks sit in the painted intrados with keystone slots, and they read in the cleared board.
- The new mist band (y 518-590) stays clear of the moons.
- m6: about a quarter of the pieces are filler.
- The pink foot mist is strong but in keeping with the scene.

**5-5 The Lighthouses of Vylbrand (base-25): APPROVE.**
- 18.14 fresh, finale gap 3.22.
- Five lights are ringed by moons, and two of them carry orbiting moons as their beams. The cliff road and the high contour trace the map.
- The lighthouses read only as small glints in a still. Whether the turning beams carry the subject in motion is unverified.
- Fix the seam (m2) when the beams are next touched.