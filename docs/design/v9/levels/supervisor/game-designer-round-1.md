# Game designer, levels round 1

**Overall verdict: REVISE.** Six of the ten levels carry a Major finding. All ten pass the pipeline's hard checks. Four levels read at a glance and play well: 1-4, 1-5, 2-2 and 2-5. The faults are in variety, in the difficulty ramp, in plain boards on stage 2, and in one artefact the fuller-board dress introduced.

## Method
- **Read:**
  - the approved method, rich and rich2 sections 1–8;
  - "Your answers" in feature-plan-v9 and the stage table in spec-rich2;
  - the pipeline README;
  - all ten layout docstrings and builds, the scene recipes, `painters/cactuar.py` and the ten reports.
- **Looked at:**
  - all ten 1x composites;
  - 2x crops of 1-2, 1-3 and 2-1;
  - dressed against undressed for 1-3, 2-3 and 2-4;
  - the trace sheets for 1-3, 1-4, 2-1, 2-4 and 2-5;
  - the six approved pilot composites at 1x and 2x.
- **Ran (read-only):**
  - `mfl.py stage 2` (no `--ramp`).
  - `mfcheck sweep` on all ten levels and the six pilots.
  - `mfcheck play` on each level for **432 greedy games** at its own level number, and the pilots for 144 games at level 5. The standard error is about ±1.1 /48 at 432 games.
  - My own parse of which oranges are still left in the games that were lost.
  - Nothing in the repo was written, and `build` was not run.
- **Not modelled:** the harness plays with no power (Super Guide or Multiball). Real stage-2 play will therefore be easier than these figures.

### Key figures

| Level | Pieces | Framing % | Pegs per first shot (median) | First-shot angles hitting nothing | Greedy /48 (432 games) | Designer's 144-game ramp |
|---|---|---|---|---|---|---|
| 1-1 | 73 | 2.18 | 7 | 4 | 28.0 | 29.7 |
| 1-2 | 74 | 3.75 | 10 | 0 | 24.0 | 26.7 |
| 1-3 | 61 | 2.47 | 7 | 6 | 24.8 | 27.0 |
| 1-4 | 64 | 1.04 | 8 | 1 | 19.4 | 20.7 |
| 1-5 | 62 | 2.38 | 7 | 1 | 19.1 | 17.7 |
| 2-1 | 60 | 0.64 | 9 | 0 | 26.3 | 26.3 |
| 2-2 | 60 | 0.80 | 8 | 4 | 22.3 | 22.7 |
| 2-3 | 79 | 0.26 | 9 | 0 | 18.7 | 21.3 |
| 2-4 | 62 | 0.40 | 5 | 12 | 18.8 | 20.0 |
| 2-5 | 61 | 0.54 | 7 | 0 | 20.8 | 20.3 |

The pilots for comparison:
- **Pieces:** 67–102, mean 83 (this set: mean 65.6).
- **Pegs per first shot (median):** 9–15.
- **Angles hitting nothing:** 0 on every pilot.
- **Greedy /48:** 17.7–24.3.

## Verdicts

| Level | Verdict | Blocking finding |
|---|---|---|
| 1-1 Road to Horizon | APPROVE | |
| 1-2 The Cactuar | APPROVE | |
| 1-3 Ul'dah Across the Sands | REVISE | M1, M3 |
| 1-4 The Gilded Dome | REVISE | M4 (stage-1 ramp) |
| 1-5 The Crystal's Call | APPROVE | |
| 2-1 Limsa Across the Water | REVISE | M5 |
| 2-2 Moonpath on the Bay | APPROVE | |
| 2-3 The Kraken's Sea | REVISE | M3, M4 |
| 2-4 The Strait of Merlthor | REVISE | M2, M4 |
| 2-5 Twin Lanterns | APPROVE | It becomes the hardest of stage 2 once M4 eases 2-3 and 2-4. If the designer hardens 2-5 instead, it needs re-review. |
| **Set** | **REVISE** | |

## Findings

### M1 — Major — 1-3: the same painting twice in a row, it takes stage 4's subject, and it reads weakly
**Evidence**
- 1-3 and 1-4 both crop `-nowloading_base02_hr1` (mirrored), back to back.
- Ul'dah is stage 4's home ("The Sunlit Steps"). By the designer's own note, this is the only ARR Thanalan painting, so stage 4 will reuse it a third time or more.
- On the composite, the towers are smeared violet columns and the white dome barely shows.
- The layout is four vertical dotted columns, a row at y 480–512 and 12 scattered "evening stars".
- The technique is meant to be a silhouette band traced along the roofline. In fact the tower tops (y 70–200) are never traced, because they sit above direct reach. What is traced is the towers' flanks.
- The straight-down first shot is dead: 6 of 171 angles (−13° to +9°) fall through the open chute at x 300–450 and touch nothing. The pilots have no such angles.

**Fix**
- Keep 1-4 as stage 1's single Ul'dah board.
- Give 1-3 a Western Thanalan subject from another source. Options:
  - our own painting of Horizon's water tower, or the Footfalls oasis under the moon;
  - the Silver Bazaar or Scorpion Crossing roofline as a true silhouette band, low enough to trace.
- Put pieces in the centre chute so a straight-down shot meets a peg.

### M2 — Major — 2-4: a second chart board in a row, a dead right half, and cheap difficulty
**Evidence**
- 2-3 and 2-4 both crop the world painting `-nowloading_base05_hr1` (960×720 each). Stage-3 pilot 3-3 uses the same painting.
- Stages 1–3 would then hold four chart boards, two of them consecutive and in the same green-teal palette with runic lettering.
- 2-4's stops (rings of five) and dotted lanes repeat 1-1's trail on a map.
- The right half (x 400–720, y 50–400) holds 8 pegs:
  - 12 of 171 first-shot angles hit nothing, 11 of them aimed right (43°–85°);
  - the median first shot lights 5 pegs, the set's lowest.
- The losses come from oranges in the bucket's approach:
  - (290, 488), the south light, is left in 45% of lost games;
  - (454, 493) and (487, 493), the Ul'dah stop's lower moons, in 39% each;
  - 61% of all oranges left in lost games sit below y 440.
- The README already names low oranges as the cheap kind of difficulty.

**Fix**
- Re-source 2-4 as a non-chart board. Options:
  - our own painting of the ferry under sail mid-strait: the hull dotted and the masts as runs, with slide-mover swell;
  - Vesper Bay's lighthouse beam, using the light-and-shadow technique.
- Merlthor itself fits stage 5 (Merlwyb, Limsa's waters); park it there.
- If it is kept here:
  - fill the right half with pegs that carry meaning (Thanalan's coast and hills);
  - lift the Ul'dah stop and the south light above y 450.

### M3 — Major — 2-3, also visible on 1-3 and 2-4: the dress prints a grey disc round every peg
**Evidence**
- The dressed 2-3 shows a desaturated grey-blue disc, about 25 units in radius, round every peg on the green chart.
- The discs merge into a grey lattice that hides the chart's art (birds, rhumb lines) exactly where the subject is drawn.
- The undressed composite has none, so the jewel step's quietening round the pegs causes it.
- 1-3's teal sky shows the same dark coin round each of its 12 star pegs.
- Pilot 3-3 has the same veil (0.4) on a teal map and shows no discs.
- This is the "plain board, now stained" outcome the owner would notice first.

**Fix**
- Quieten the jewel by low frequency: lower the field's chroma evenly, or feather the quietening over the union of the layout (30+ units), never per peg.
- Re-check the dressed board against the undressed one: no peg-centred chroma step should be visible at 1x.
- F9 must still pass.

### M4 — Major — 1-4/1-5 and 2-3/2-4/2-5: neither stage finale is the hardest, and difficulty plateaus from level 4
**Evidence** (432 games each; noise ±1.1 /48)
- **Stage 1:** 28.0, 24.0, 24.8, 19.4, 19.1. The finale equals 1-4.
- **Stage 2:** 26.3, 22.3, 18.7, 18.8, 20.8. The finale is easier than 2-3 and 2-4.
- **The 144-game ramp overstated the gaps.** It showed 20.7 → 17.7 for 1-4 → 1-5, and 21.3 / 20.0 / 20.3 for 2-3 / 2-4 / 2-5.
- **Levels 4–10 all sit at 18.7–20.8.** That is already the difficulty of the stage-3 and expansion pilots (17.7–24.3), so the campaign has no room left to ramp. In play, Multiball will make stage 2 easier still.

**Fix**
- Hold each finale clearly hardest in its stage (at least 2.5 /48 below its 4th level, measured over 432 games or more), and keep stages 1–2 in a gentler band, for example:
  - stage 1 from about 30 to 21;
  - stage 2 from about 27 to 20.
- **Stage 1:** ease 1-4 to about 23. Its oranges left in lost games are low and shielded: (372, 470) 25%, (460, 461) 19% and (560, 505) 20%. Make the terrace-lamp and low column oranges non-candidates, and add blue pegs on the palace.
- **Stage 2:** ease 2-3 and 2-4 to about 23–24. For 2-4, lift its low oranges (M2). 2-3's losses are spread evenly, so add blue pegs along the rhumb lines and the flock to raise chains.
- **2-5:** keep it at about 20.8 as the finale.
- Adding blue pegs both eases these boards and answers M5 and m1.

### M5 — Major — 2-1: plain, and Limsa does not read
**Evidence**
- 60 pieces (the pre-flight floor) and 0.64% framing.
- The board is a foggy blue painting with two horizontal dotted rows (deck and lamps) and six brick humps.
- Nothing recognisable as Limsa (the tree-tower, the lighthouse, the Bulwark) is drawn.
- "Castle edge" is a ruler-straight column of six candidates at x ≈ 565, y 140–330, over no visible edge. With 27 candidates it is orange almost every game.
- The lower band (y 470–560) is empty water.
- For a stage opener, two caged lanterns decide most losses:
  - (380, 488) is left in 48% of lost games;
  - (575, 441) in 36%, and no first shot reaches it.

**Fix**
- Re-crop or re-trace so one Limsa landmark carries the board: the tree-grown tower's canopy as a real outline, or the lighthouse as a dotted column.
- Replace the castle column with the castle's actual flank.
- Add a dotted swell line at y 470–505.
- Open one arch crown (two bricks with a gap of at least 14 at the keystone), or raise the lanterns so a side shot reaches them.
- Target 75 pieces or more and about 2% framing.
- Note: this uses stage 5's only regional La Noscea painting. Reserve a different crop for stage 5.

### m1 — Minor — 2-2, 2-5 and stage 2 generally: fullness below the pilots' bar
**Evidence**
- Stage-2 framing covers 0.26–0.8% of the opening, against stage 1's 1.0–3.75% and the pilots' up to 5.3%.
- 2-2 and 2-5 have 60 and 61 pieces, against the method's 80–120 target.
- 1-4 (64) and 1-5 (62) are also sparse. 1-5's airy constellation is deliberate, but the constellation pilot exp-p2 managed 102 pieces.
- Both 2-2 and 2-5 still read well.

**Fix**
- **2-2:** add swell crests and stars, and harbour framing (rope, pilings) to about 2%.
- **2-5:** add more stars and a dotted far shore in the upper half, and frond framing as the recipe intends.

### m2 — Minor — 1-1, 1-2, 1-4, 1-5, 2-2 and 2-5: exactly 25 orange candidates
**Evidence:** with 25 candidates, the oranges are identical in every game, so the board can be solved from memory. The method asks for 25–35; the pilots use 26–35. This matters for replay in Quick Play, Challenges and Duel.

**Fix:** add 3–7 candidates that carry the subject (the pilots' practice) on each.

### m3 — Minor — 1-2: the outline is broken at the subject
**Evidence**
- The far-dune trace puts three pegs across the cactuar's belly, at (398, 348), (434, 351) and (474, 348).
- The crown orange at (458, 186) sits exactly on the middle tuft's tip. Only two tufts show, so the crown reads as horns or cat ears.

**Fix**
- End the far-dune trace 18 units outside the silhouette.
- Move the crown peg to about (458, 168), clear of the launcher (99.6 from the pivot, against the 94 required).
- The three face oranges (two eyes and the mouth) are a good touch; keep them.

### m4 — Minor — 2-2: one low orange decides losses
**Evidence:** the moonpath's bottom orange at (292, 510) is left in 44% of lost games.

**Fix:** make it a blue peg, or lift the last row to y ≤ 490.

### m5 — Minor — 1-1 and 2-2: dead first shots
**Evidence**
- 1-1 has 4 angles that hit nothing (−59° to −57°, and −22°).
- 2-2 has 4 (−41°, and 22°–24°, which fall straight into the bucket).
- The pilots have none.

**Fix:** one blue peg in each lane.

### m6 — Minor — 1-1: weakest colour-blind (protan) separation in the set
**Evidence:** the orange pegs' protan separation is 0.091 (rule: 0.12, or no more than 0.02 below the undressed board). It passes only by that allowance (undressed 0.11), on the first board every player sees.

**Fix:** pass this to the readability check, so 1-1 meets 0.12 outright.

### m7 — Minor — process: ramp figures
**Evidence**
- `report/base-02`, `base-04` and `base-10` have no `ramp` field, yet the summary quotes 26.7, 20.7 and 20.3 for them.
- 144 games was not enough to separate neighbouring levels (M4).

**Fix:** record a 432-game ramp in every report and decide the ramp from those figures.

### n1 — Nit — 1-4
1-4's terrace lamps are a ruler-straight row of 13 pegs across the full width at y 505. It reads as a floor. Stagger it, or drop every third peg.

### n2 — Nit — set: palette
The first jewel is 285° on 7 of 10 boards, and the second alternates between 195° and 15°. F7 passes, but the set looks like two palettes alternating. Give one stage-2 board a warmer first jewel (lamplight amber or rose).

## What works
- **1-4** reads as Ul'dah's dome at a glance, and its brick crown is a fine bank-shot feature.
- **1-5** reads as a crystal constellation immediately.
- **2-2** reads as the moon's path, the pier and the ferry.
- **2-5** is symmetric, clear and the twins' own board, and its slide-mover reflection is the set's best use of movers.
- **Close losses:** across the set, 39–73% of lost games end with two oranges or fewer left, which is a good near-miss tension.
- **The techniques vary well on paper:** ten techniques across ten boards. Movers appear only in stage 2, which is right for the opening stages.

Scratch scripts and outputs are in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/gd/`, with the 432-game runs in `ramp/*.txt`.
