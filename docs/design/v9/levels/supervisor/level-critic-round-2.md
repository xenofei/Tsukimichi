# Level-design critic, levels round 2

5 October 2026. Worktree `agent-a570460c913ca1c79` at `ad2d9a16`. I edited no repo files and did not run `mfl.py build`. `git status` is clean after my runs; the only writes were to the gitignored `build/` (selftest boards) and to my scratch directory.

**Overall verdict: REVISE.** All three round-1 pipeline Majors (P1–P3) and the level Major C1 are resolved, with evidence below. One new Major remains (G1): the ghost check samples only isolated pegs. On 1-5 those are exactly the pegs that print nothing, so the per-peg discs the coordinator banned pass the gate there, and on 2-3 the check measures nothing at all. Everything else is Minor or Nit. Eight of the ten levels I would ship as they are; 1-5 needs its quiet fixed, and 2-3 should be re-measured once G1's fix is in.

## Method

- **Self-tests and files.**
  - `mfl.py selftest`: every case passes (24 pre-flight, 20 framing, 9 readability, 11 engine).
  - All ten `json/*.json` are byte-equal to `json.dumps(Board.level_json(), indent=2)` rebuilt from their layouts.
  - All ten pass `mfcheck validate` and a fresh `Board.check()`.
  - `stage 1` and `stage 2` are clean. Committed composites are hash-equal to `build/composites`.
- **Round-1 probes re-run** against the new pre-flight (`preflight_probe.py`, `preflight_probe2.py`).
- **New bypass probes, each through pre-flight, loader and engine sweep.**
  - Cups: crown feet, short middle brick, peg on a brick's low end.
  - Decks: 6.5° decks, separate level bricks, wide arc apex.
  - Movers: co-moving overlap and saddle, slides of different periods.
  - Lane: long bricks.
  - Narrow slots between arc feet; level pegs 11.9 apart.
- **Movers along their whole path in real time** (2-4 over the LCM of its periods, 63 s): against still pegs, bricks and every other mover, with the engine's own `Body.Move` (orbit sign, slide easing).
- **Engine runs.**
  - A quarter-degree sweep (681 aims) of every level, for dead and stuck shots. Rest cells of 2-1, 1-1, 1-4 and 2-3 traced.
  - 432 greedy games per level: holdouts by piece index, not end position; the share of lost games that leave an orange below y 440.
  - A 2-5 variant with the reflected heads' oranges moved to the stars.
- **Ghost.**
  - The gate's measure with its isolation filter relaxed.
  - A veil-free cleared board, dressed against undressed (what the jewel's quiet alone leaves).
  - Main's `docs/design/v9/scene-recipe.md`, read via `git show`, for how the runtime builds the palette.
- **Viewed:** every composite at 1x, 2-1 at 2x crops, and the cleared boards of 1-2, 1-4, 1-5, 2-3 and 2-5.
- **Scripts:** `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/critic2/`
  - `bypass*.py`, `levels2.py`, `cheap.py`, `cheap_exp.py`, `misc.py`, `stuck4.py`, `ghost2.py`, `ghost3.py`

## Verdicts

| Level | Verdict | Reads as its subject | Notes |
|---|---|---|---|
| 1-1 Road to Horizon | APPROVE | Yes | Minor G3 (holdout at the cheap gate's edge), G4 (dead aims at 72.3–72.5°), G7 (27.2, band ~30) |
| 1-2 Horizon by Night | APPROVE | Partly: a generic night skyline; the tank and derrick carry it, no mesa reads | G4 (four dead aims), L13 (a 16-peg cliff row) |
| 1-3 The Cactuar | APPROVE | Yes, clearly | C1 and C2 resolved |
| 1-4 The Gilded Dome | APPROVE | Yes | L4 resolved |
| 1-5 The Crystal's Call | **REVISE** | Yes | **Major G1**: the quiet prints a disc per peg across the aurora, and it stays after clearing. G7 (19.1, band ~21) |
| 2-1 Limsa Across the Water | APPROVE | Weakly: a dark mass ringed with oranges; the bridge reads | Minor L11 (a 12.8-unit slot between the arches), L12, L13 |
| 2-2 Moonpath on the Bay | APPROVE | Yes, the strongest in stage 2 | L7 resolved; G4 (one dead aim); G6 (its two orange crests are outside F9) |
| 2-3 The Kraken's Sea | APPROVE, conditional on G1 | As a chart of wonders | The ghost gate measured nothing here (G1); L8 resolved |
| 2-4 The Ferry Under Sail | APPROVE | Yes, clearly: masts, yards, gunwale, sails | Movers clear over 63 s; L13 (two 19-mover swell rows read as a fence when still) |
| 2-5 Twin Lanterns | APPROVE | Yes: the gate and its moving reflection | G3: 81% of lost games leave a low orange (designer's call, data below) |
| Pipeline and checkers | **REVISE** | — | **Major G1**; Minors G2–G7; Nits |

Figures (432 games, per 48):
- **Stage 1:** 27.2, 25.7, 24.3, 23.6, 19.1.
- **Stage 2:** 27.8, 24.9, 23.9, 23.1, 19.7.
- **First-shot sweep (1°):** stuck at most 4 of 171 (1-1); dead 0; never reached 0 on all ten.
- **Quarter-degree sweep:** stuck at most 18 of 681 (2-1, 2.6%).

## Status of round-1 findings

| Finding | Status | Evidence |
|---|---|---|
| **P1** cup bypass (Major) | **Resolved** | Cups are now judged by geometry: any touching pair whose bricks both rise away from the joint. Two of my round-1 cases are caught (V of two bricks; three-brick cup laid with `line()`); they are in the self-test too. My round-1 case 8 was mis-built (its arcs' ends were 81 apart, so they never touched). Rebuilt with the feet touching (`A1`), it is caught too. A V with a short middle brick (under 10 units, which the rise test cannot see) is still caught, by the cup or notch test between the outer bricks. |
| **P2** mover clearance (Major) | **Resolved for the stated cases**; residue in G2 | Orbit 4 from a brick: caught. Swapping slides: caught. A still placement on an orbit path: refused. Shipped levels over real time: 2-3 orbit ≥ 12.7 to still pegs and ≥ 43.5 to bricks; 2-4 (periods 7 and 9) mover-mover ≥ 16.0 over 63 s, still ≥ 20.5, no bricks; 2-5 still ≥ 14.2, brick ≥ 26.3, mover-mover ≥ 17.2. |
| **P3** engine gates untested (Major) | **Resolved** | Stuck gate: the flat-decks board fails (26/171) and pilot base-p2 passes (3/171). `reach` gates `unreached` by iterated sweeps on a board without the pieces already reached; the corner peg is its known-bad case. All ten levels: never reached `[]`. |
| P4 decks, wedge band | **Partly**: G5 | My 210-unit level deck and a peg 14 above an 18° brick are both caught, and both are self-tested. On 1-4, no peg stands 13–18 above any brick (finials sit 19 clear). Residue below. |
| P5 lane ignores bricks | **Resolved**; Nit L14 | Six bricks at y 548 are caught. The lane is counted by pieces, though (L14). |
| P6 moving candidate out of reach | **Resolved** | Self-tested; my orange slide at (100, 20) is caught. |
| P7 touching pegs, real radii | **Resolved** | A gap of 0.3, level, is caught. r 11 pegs at 55° are caught. |
| P8 `canBeGreen` on bricks and movers | **Resolved** | Bricks and movers count in the 8 sure greens and in reach. Crowns are never green on 1-2 (tank), 1-4 (all nine crowns and the arch) and 2-5 (arch). The engine's seed-1 deal is checked, and the engine honours `CanBeGreen` (`MoonfallGame.cs:1343`). |
| P9 F7 and F9 soundness | **Resolved**; residue in G6 | F7 uses mean-hue distance; the 32° case fails in the self-test. F9 covers every peg candidate, with movers at four moments (2-3: 116 places; 2-5: 124). The clause compares against the 0.101 pilot, never the level's own board. |
| P10 stale ramp | **Resolved**; residue in G7 | Every report carries 432 games. `stage` enforces the finale (19.1 ≤ 21.1; 19.7 ≤ 20.6). |
| **C1** far dune across the cactuar (Major) | **Resolved** | `b.subject("cactuar")`, together with `why_not` and `check()`, keeps the interior open. Only the three face pegs are inside the polygon. Self-tested. |
| C2 level decks on 1-3 | **Resolved** | Crests at 13° and 16° with a summit moon. Stuck 0/171 (1° sweep) and 2/681 (quarter-degree). |
| L1 compass rose | **Resolved** | Eight r 7 blue points and a heart; F9 0.135. Nit: it is still the most prominent ring on the board. |
| L3 the same painting twice | **Resolved** | No adjacent repeats. Nit: 9 of 10 first jewels lie within 250–293°. |
| L4 finials in the wedge band | **Resolved** | Finials sit 19 clear; the terrace is staggered at y 494 and 507. |
| L5 thin constellation lines | **Open** (Nit) | 15 points still skipped while tracing on 1-5. |
| L6 2-1 shielded lantern | **Superseded** | 2-1 is re-cropped; new geometry in L11. |
| L7 crests in the lane, docstring | **Resolved** | Crests all above y 490; lane pieces: 1 peg. The docstring now says the pier is dotted. |
| L8 rhumb lines, islet | **Resolved** | Both are gone. Nit: still placements may stand 12 from an orbit's path (`_mover_near` uses 12, dotted lines use 14); 2-3 has one at 12.7. |
| L9 2-4 sparse | **Superseded** | Merlthor is parked; the ferry has no dead aims and a median of 9 pegs per first shot. |
| L10 movers' greens, F9 | **Resolved** | Mover paths count in the green reach; 2-5's F9 is 0.130 with the reflection included. |

## New findings

### G1 (Major), pipeline and 1-5: the ghost check never looks where the discs are
- **Where:** `readability.ghost`. It measures only pegs with no other piece within 40 units, and it passes (`g_med is None`) when there are none.
- **Evidence.**
  - **1-5.** The pipeline's own cleared board (`build/composites/base-05-cleared.png`) shows a polka-dot field of dark discs across the aurora. The gate reported 0.060, measured over 5 isolated pegs, which sit in plain sky.
    - The gate's own formula on every still peg with open ground: median **0.091**, and 38 of 56 pegs over 0.066.
    - With the veil off (the runtime draws the veil live per piece) and the dressed board compared with the undressed one, the quiet alone `[26, 12]` prints **median 0.080, max 0.114** at the peg sites. The other levels print 0.025–0.040.
  - **2-3.** The gate measured **no** pegs (`ghost None over 0`), because the open-sea moons were paired. The quiet-only print is 0.061, and it reads as grey blotches over the green chart.
  - **The runtime keeps the print.** Main's `scene-recipe.md` says the scene is built at load. The quiet maps to the palette's `near` mask term, so the print is baked and stays after its pegs clear. Only the veil is exempt ("never baked", lines 47–50).
  - This breaks the coordinator's binding rule: "quiet by low frequency, never per peg".
- **Fix.**
  - **The gate.** Measure on the veil-free cleared board, over every still peg. Take the colour shift between dressed and undressed at the peg's site against open ground, so the painting's own detail cancels. Refuse when the median is over the pilots' value on the same measure. A board with too few samples must fail, never pass. Add 1-5 as the known-bad case.
  - **1-5.** Paint the aurora's colour into `painters/crystal.py`, as the README lesson already says (2-2's rose dusk), or widen the quiet to `[44, 12]` and re-check F7's share.
  - **2-3.** Re-measure with the new gate.

### G2 (Minor), pre-flight: mover against mover still has holes
- **Different periods.** `check()` compares movers at equal phase, but the engine moves each mover on its own period.
  - Probe `A7b`: two slides of periods 4 and 6. Their true minimum gap is −17.4 (they collide); the equal-phase minimum is 13.3. The pre-flight passes them, and so does the loader.
- **Same drift group.** Movers in one group are never compared with each other, and the still-peg overlap and saddle loop covers still pegs only.
  - Probe `A6`: two co-moving slides overlapping by 8, and two in a level saddle of gap 3. Both pass the pre-flight and the loader.
- **No shipped level is affected** (see P2 above).
- **Fix.**
  - Sample real time over the LCM of the pair's periods (capped), not equal phase.
  - Run the overlap and saddle rules on same-group movers at t = 0.
  - Add both probes to the self-test.

### G3 (Minor), the cheap-difficulty gate is blind to movers, counts one orange at a time, and has no self-test
- **Where:** `engine.play` keys holdouts by the piece's integer **end** position, so a mover's losses scatter across many keys. The gate then needs a single orange left in at least 30% of lost games.
- **2-5.** By piece index, the top four holdouts are the reflection's movers, at y 471–492: 0.27, 0.23, 0.22 and 0.20. None appears in the report's holdouts.
  - **81%** of lost games leave an orange below y 440, and 19% leave only low ones.
  - Counterpoint: moving the ten reflected-head oranges to the stars drops 2-5 to 9.2/48. The low reflection is not what makes the finale hard, so whether this counts as cheap is the designer's call.
- **1-1.** The top holdout is piece 36 at (550, **439.0**), share **0.30**. It passes by one unit, because the test is `y > 440`. 48% of 1-1's lost games leave a low orange.
- **Fix.**
  - Key holdouts by piece index.
  - Report and gate the share of lost games decided only by oranges below y 440.
  - Add a known-bad case (a board with low oranges only) and a known-good one (a pilot).

### G4 (Minor), the dead-first-shot gate sweeps whole degrees only
- A quarter-degree sweep finds dead aims the gate misses:
  - 1-1: 72.3° and 72.5°;
  - 1-2: −67.5°, −67.3°, 25.3° and 25.5°;
  - 2-1: 7.5°;
  - 2-2: −40.5°.
- They are narrow, but the rule says "no dead first shots", and a player can aim at any angle.
- **Fix:** sweep at 0.25° (681 aims). Use `mfl.py dead` at that step to place a piece in each lane.

### G5 (Minor), the deck rule is narrower than the lesson (P4 residue)
- **Decks at 6.5°.** Two 220-unit decks fire the stuck rule on 7 of 171 first shots (4.1%), and the pre-flight passes them.
- **Separate level bricks.** Eight 28-unit level bricks, 26 apart (no notch, no chain), fire it on 5 of 171, and the pre-flight passes them.
- **Arcs** are exempt from the deck rule. My wide-crown probe showed 0 stuck shots, so that exemption is harmless as far as I measured.
- **Fix:** raise the slope limit to about 10°, and count any level line brick of 20 units or more, chained or not.

### G6 (Minor), F9 skips orange bricks
- `orange_views` collects peg candidates only. 2-2's two orange wave crests (bricks 0 and 4) are never measured: 26 places, not 28.
- **Fix:** sample brick candidates along their middle line.

### G7 (Minor), the difficulty bands are not gated, and both stage 1 ends miss them
- **The opening.** 1-1 is 27.2 against "about 30": the campaign's first level is the stage's hardest opening relative to its band. 2-1 (27.8) is as easy as 1-1.
- **The finale.** 1-5 is 19.1 against "about 21". This is a known gap; the 2.5-below-1-4 rule allows up to 21.1.
- `stage` checks the finale but not the bands.
- **Fix:** add the band ends to `stage`, with a ±1.1 tolerance, and ease 1-1 by about 2.

### L11 (Minor), 2-1: a near-notch between the arches
- **The slot.** The west arch's east foot and the east arch's west foot are **12.8** apart. The notch rule's ceiling is 12.5 and the ball is 12. Both feet slope steeply into the slot.
- **The keystones.** Both are 15.7 apart, against the README's "16 or more".
- **Evidence.**
  - 3 of 18 quarter-degree stuck shots rest at cell (600, 440), the slot.
  - 2-1 has the most stuck shots in the quarter-degree sweep (18/681) and the most stuck-rule fires in play (32 in 432 games).
- **Fix.**
  - Move the east arch about 8 units east (slot 20 or more), and keystones to 16.
  - In the pipeline, raise the notch ceiling toward 14, the dotted spacing.

### L12 (Minor), reads as its subject
- **2-1.** The tree-grown tower is a dark blob under a ring of oranges. Neither the tower nor its spire reads at 1x. The four spires are a 3 × 5 block of pegs that reads as a grid.
- **1-2.** Reads as a generic night skyline of boxes. Horizon's mesa does not read: the town stands on a flat green band.
- These are art calls, so I leave the fix to the art supervisor.

### L13 (Nit), full-width rows read as fences
- 1-2's cliff ledge: 16 pegs from x 110 to 702 at y 407–416.
- 2-1's swell line: 16 pegs at y 492–494.
- 2-4's two swell rows: 19 movers from x 114 to 636 at y 462–500. Read when still, they look like a grid.
- **Fix:** break each into two or three runs that follow the painting's contour.

### L14 (Nit), the lane check counts pieces, not width
- Two 270-unit bricks covering the bucket's lane pass it (probe `A8`). The sweep caught them only because they also held balls: 13.5% stuck.
- **Fix:** measure the covered width of y 520–560.

### L15 (Nit), self-test coverage
- No known-bad or known-good case for:
  - the cheap-difficulty gate;
  - the `stage` ramp and neighbour checks;
  - the engine-colours gate.
- No known-good case next to an orbit for `why_not`.

## Unverified

- **The runtime converter.** I took the "baked at load" reading from main's `scene-recipe.md`, which is not merged here. The converter from these recipes to the runtime format does not exist yet, so it is unproven that the quiet's print ships exactly as measured.
- **In-game play.** All play evidence is mfcheck's greedy player at seed-derived deals. I measured stuck balls only on first shots, plus the greedy games' stuck counts.
- **Pilot calibration.** I did not compute the pilots' values on my veil-free ghost measure, so the G1 threshold needs calibrating on them.
