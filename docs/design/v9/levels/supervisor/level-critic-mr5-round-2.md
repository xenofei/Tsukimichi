VERDICT: APPROVE (0 blockers, 0 majors, 1 minor)

I reviewed read-only at worktree HEAD `d7daa53a`, and `git status` is clean. The only code-side changes since round 1 (`9404548d`) are level, scene and aces data. The level JSONs are byte-identical to `docs/.../json`. The render DLL's embedded `base-21..25.json`, the five scene recipes and `aces.json` match the repo, including the stage retune. `mfcheck` physics is unchanged. My output is in `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr5r2\`.

What I ran:
- **Fresh play:** `mfcheck play` on four seed blocks no one has used (240001+, 250001+, 260001+, 270001+), 3,456 games each, so 13,824 per level.
- **Sweeps and traces:** a 0.25° sweep on each level, and a trace of every stuck angle on 5-3 and 5-4.
- **Renders:** MoonfallRender with my own `--cache`, for all 15 pack frames. All 15 match the pack pixel for pixel (AE 0). I also rendered four timed frames of 5-5 cleared (2, 5, 8 and 11 s).

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| m1 | Minor | stage | The response's advice for MR 6 points the wrong way. | The response says MR 6 "should not fall by more than about 0.5". The charter rule is a fall of 0.5 to 1.0. From 22.11 pooled (22.08 fresh), MR 6's mean must be at most about 21.6, which is exactly its charter mean, and at least about 21.1. MR 6 therefore has no room above its charter mean, and some room below it. The record should say that before MR 6 is tuned. |
| n1 | Nit | 5-3 | In the engine the reflections are warm tan, not cream gold, and no brighter than before. | Cleared-1280, warm pixels in the columns: median hue 25° → 32°, saturation 0.14 → 0.18, p90 value 0.48 → 0.48. The response's "hue about 45° → 70°" does not describe the engine frame. The lamps are now warm and no moon covers one, so half of round-1 m1 is fully done. F6 (0.206 against its 0.20 floor) blocks a brighter reflection, so I accept it. |
| n2 | Nit | 5-4 | The new layout's sweep sticks on 5 of 681 angles, up from 0. | Four angles (-6.5°, 36.5°, 40.75°, 46.25°) linger 340-800 ticks in the cable-east swag cup, between moons (453,275) and (488,271). The fifth, -82.75°, makes 30 wall contacts in the pocket between the wall, the stair's first moon (118,52) and the castle-wall moon (100,95). Play stuck on fresh seeds is 30 per 1,728 (round 1: 31), well under 88. |
| n3 | Nit | 5-4 | The random player now wins 1 of 40 games, up from 0. | All four fresh blocks and the pack agree, because the random games use fixed seeds. The pipeline does not gate this, and base-14, base-15 and base-17 shipped the same way. |
| n4 | Nit | 5-4 | The top holdouts all sit in one column: the east tower legs. | On fresh seeds, (652,176) is left in 24.8% of lost games, (652,257) in 24.2% and (652,292) in 20.0%. 61% of the oranges left are on the right. All three sit above y 430 and 5-4 does not carry the duel, so no action. |
| n5 | Nit | 5-3 | The top holdout reads 26.1% on fresh seeds. | (613,411): the pack says 21%. It sits above y 430, so the cheap-difficulty gate does not apply. |
| n6 | Nit | 5-4 | The rose under the arches is shorter but has not gone. | Cleared-1280 row means: screen y 600 has red 32% (it was 28%), y 700 has 27% (it was 42%), and y 740 is now violet. The smear has moved up under the arches. This is the game designer's call. |
| n7 | Nit | 5-2..5-5 | Protan still passes only through the pilot fallback on four levels, but now with margin. | p10 is 0.108-0.118 against the fallback of 0.101, with 0.12 as the floor. Round-1 m5, partly closed. |
| n8 | Nit | 5-5 | The beams in motion are very subtle. | Against the rest frame, the frames at 2, 5, 8 and 11 s differ by about 2% RMSE. Beams breathe and drift but do not turn (`MoonfallMotion.Beams`), so no frame can bring the 180° wrap back. Whether the beams carry the subject in play still needs a person to watch it. |

## Round-1 findings, checked

- **m1 (5-3 brown reflections, cold lamps): partly fixed, now n1.** The lamps are warm, the loggias and windows keep their warmth, and no moon covers a lamp. The reflections have moved from pink-taupe to tan-gold.
- **m2 (5-5 seam at y 404): fixed.**
    - Cleared-1280, the largest row-to-row luma step over x 240-520 was 6.6 at screen y 542; it is gone. The largest step now is 5.2 at y 480, which was already there in round 1.
    - Aleport's beams now point at 120° and 162°.
    - The motion frames at 2, 5, 8 and 11 s show no edge either.
- **m3 (5-3 jetty ledge): fixed.** The last brick ends at x 694, which leaves about 25 units to the wall. The sweep now sticks on 2 angles, at (193,231) and (372,333), and neither is in that corner.
- **m4 (the mean fell too little on fresh seeds): fixed.** The fresh mean is 22.08: a fall of 0.73 from MR 4's fresh 22.81, inside the charter's 0.5-1.0.
- **m5 (protan only through the fallback): partly fixed, now n7.**
- **m6 (5-4 filler): fixed.**
    - 83 pieces → 71. The cloud, mist and under-arch moons are gone.
    - The deck now reads as three spans, and the upper pieces sit on the chimneys, the thin spire, the stair and the dome drum.
    - The castle-wall column (x 100, nine moons) follows a faint building edge in the painting. In the cleared board it reads mostly as a column along the wall. I accept it.
- **n1 (5-1 lawn foot): fixed.** The foot is sea teal now, and the towers are grey-white instead of lavender.
- **n2 (5-2 two-peg gaps): unchanged, as declared.** The sweep sticks on 11 angles; play stuck is 55 per 1,728.
- **n3 (5-5 top holdout at 25%): eased.** (459,390) is left in 24.4% of lost games on fresh seeds.
- **n4 (shared jewel pairs): partly fixed.** 5-1 has its own pair now (275/210). 5-3 (255/171) and 5-5 (246/168) still share one, two levels apart.
- **n5 (ease-back): improved.** 4-5 at 17.55 → 5-1 at 25.44 gives an ease-back of 7.89 (it was 8.3), against the charter's 7.0-7.5.
- **n6 (5-2 invented compass): kept,** and the rose under its ring is now lifted.
- **n7 (thin pipeline margins): unchanged.** The response's figures match the reports, and nothing was loosened.

## Checks that pass

- **Dead first shots:** none. Every sweep angle hits a piece, and every piece is reached by some first shot (0 missed of 69, 77, 82, 71 and 79). No flight is over 15 s.
- **Stuck per 1,728, fresh:** 15, 55, 20, 30 and 62, all under 88. The sweep sticks on 1, 11, 2, 5 and 5 of 681 angles, the same counts the response reports.
- **Ramp, fresh, 13,824 games per level:**

    | Level | Fresh /48 | Four blocks | Pack pooled |
    |---|---|---|---|
    | 5-1 | 25.44 | 25.2-25.6 | 25.45 |
    | 5-2 | 24.07 | 23.8-24.5 | 24.02 |
    | 5-3 | 22.26 | 21.6-23.1 | 22.3 |
    | 5-4 | 21.22 | 20.6-21.7 | 21.07 |
    | 5-5 | 17.41 | 17.2-17.6 | 17.7 |

    - Each fresh figure is ± 0.20.
    - The steps are 1.36, 1.81 and 1.04, and the finale gap is 3.81 (each ± 0.29). After one standard error is taken off, they are still 1.08, 1.52, 0.75 and 3.53.
    - The opener and the finale are inside their bands (26 ± 1.2 and 18 ± 1.2).
    - The pipeline's pooled figures agree with mine within 0.3 on every level. The finale retune holds on fresh seeds: 17.41 against 18.14 for the round-1 layout.
    - The 5-3 → 5-4 step (1.04) is the tightest, but it is resolved.
- **Random player:** 0 wins of 40 on every level except 5-4, which wins 1 (n3).
- **Holdouts and cheap difficulty, fresh:**
    - The low-orange (y 430 or below) candidates are 0, 0, 1, 1 and 1 per level. The low oranges' share of those left is 0, 0, 0.045, 0.037 and 0.034, each at most 1.3 times their share of the candidates; the gate refuses above 1.5 times.
    - No low orange is left in more than 11.8% of lost games (5-3's (689,446)).
    - Top holdouts: 5-1 (147,262) 23.4%, 5-2 (134,242) 23.0%, 5-3 (613,411) 26.1% (n5), 5-4 (652,176) 24.8%, 5-5 (459,390) 24.4%.
- **ch-11 duel balance (5-1 to 5-3):** the share of oranges left on the left side of the board is 0.47, 0.49 and 0.51. I did not run duel mode itself.
- **Engine against composite:** engine-1280 matches the composite piece for piece on all five levels. At 640, 5-3's lamps and columns, 5-4's lamps and 5-5's lights all still read.
- **Pipeline:** all five reports PASS with an empty `fails` list. The pack reports are byte-identical to `docs/.../report/`. `stage-mr5.txt` and `index.md` match the reports.

## Per-level verdicts

**5-1 The White Towers (base-21): APPROVE.**
- 25.44 fresh, 15 stuck per 1,728, and the sweep sticks on 1 angle.
- The layout is unchanged.
- The castle reads grey-white and the bay sea teal. The rigging and spire runs still trace the painting.

**5-2 The Strait of Merlthor (base-22): APPROVE.**
- 24.07 fresh.
- The route now ends off the guide column, and the south rose is lifted under its ring.
- The two-peg gaps (11 sweep angles) stay as declared.

**5-3 Lights on the Water (base-23): APPROVE.**
- 22.26 fresh, 20 stuck per 1,728. The jetty ledge is gone.
- The lamps are warm small lights, and the moons flank them instead of covering them.
- The reflections are tan-gold (n1); F6 caps them.
- The subject now reads as lamps over their light on the water.

**5-4 The Arches of the Bridge (base-24): APPROVE.**
- 21.22 fresh. This layout is the biggest change of the round, and it is better.
    - It is lamplit: six flickering lights plus the moons on the deck lamps.
    - The deck reads as three spans, and the upper pieces now sit on real features.
- What it costs, all nits:
    - The cable swag cup and a top-left wall pocket (n2).
    - One random win (n3).
    - Holdouts concentrated on the east tower (n4).
    - A rose band that is shorter but still there (n6).

**5-5 The Lighthouses of Vylbrand (base-25): APPROVE.**
- 17.41 fresh, a finale gap of 3.81, 62 stuck per 1,728, and the top holdout at 24.4%.
- The seam is gone, and the lamp cores sit under the guide dots.
- The retune moves one candidacy along the same contour, so the trace is unchanged.
- The beams in motion still need a person to watch them (n8).

**Stage: APPROVE.** On fresh seeds the ramp is resolved at every step, and the mean falls 0.73 from MR 4. Fix the MR 6 guidance in the record (m1) before MR 6 is tuned.
