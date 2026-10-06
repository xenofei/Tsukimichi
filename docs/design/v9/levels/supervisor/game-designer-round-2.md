# Game designer, levels round 2

5 October 2026. Worktree `agent-a570460c913ca1c79` at `ad2d9a16`. I edited no repo files and did not run `build`. `git status` is clean after my runs.

**Overall verdict: REVISE.** One Major is left: my round-1 M3, the ghost discs, is not resolved. Most boards still quieten the jewel per peg (`quiet: [22..30, 12]`), against the coordinator's rule ("quiet by low frequency, never per peg"). On 1-2, 1-4, 1-5, 2-2 and 2-5 this prints a coloured coin round each peg. You can see the coins at 1x during play, and they stay in the scene after the pegs clear. The approved pilots show none. The pipeline's ghost check passes these boards because its measure mixes chroma with lightness.

Everything else from round 1 is resolved or down to Minor:
- the repeated paintings;
- 2-4's dead right half;
- 2-1's plainness;
- the finale rule;
- the 25-candidate boards;
- dead first shots;
- 1-1's protan separation.

## Method
- **Read:**
  - `round2-context.md`;
  - my round-1 report, and the UX and level-critic round-1 reports;
  - the pipeline README;
  - the layouts (base-02, 06 and 09 in full);
  - all ten scene recipes' jewel, veil and tone settings;
  - all ten reports;
  - the runtime veil and `near` sections of `docs/design/v9/scene-recipe.md` on main.
- **Looked at:**
  - all ten 1x composites, and a 5×2 contact sheet of the set;
  - 2x crops of 1-1, 1-2, 1-4 and 2-1;
  - native-1x crops of 1-5, 2-5, 2-2 and 2-4;
  - all ten piece-free ("cleared") boards and all ten dressed scenes (`build/`);
  - for comparison, the pilots' dressed scenes (`rich2/scenes`) and composites (base-p2, exp-p2).
- **Ran (read-only):**
  - `mfl.py stage 1` and `stage 2` (all PASS);
  - `mfl.py stuck base-01` and `stuck base-06`;
  - `mfcheck play` at each level's own number for **1296 greedy games**. Seeds 1–432 reproduce every report's ramp exactly; seeds 433–1296 are fresh. The standard error is ±0.66 per 48 over 1296 games.
  - My own parse of which oranges are left in lost games. It gives each low orange's share of the leftovers against its share of the deal.
  - My own disc measure. It is the OKLab distance between the ground 5–12 units from each isolated still peg and the open ground 32–48 units out. I measured it on the dressed scene (no pieces, no veil) and on the dealt composite, for the ten levels and the six pilots, as full ΔE and as chroma (a/b) alone.
- **Scratch files:** `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/gd2/`. The play logs are in `ramp/`, with `discs.py`, `discs_play.py`, `ramp2.py` and `lowshare.py` alongside.
- **Not modelled:** powers (Super Guide, Multiball), the runtime converter for scene recipes, and in-game rendering.

### Key figures

| Level | Pieces | Candidates | Framing % | No-hit angles | Pegs per first shot (median) | Ramp /48 over 432 (report) | Ramp /48 over 1296 (mine) | Stuck-rule games /1296 | Disc chroma, dressed scene (median) |
|---|---|---|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 76 | 28 | 2.18 | 0 | 8 | 27.2 | 26.6 | 61 | 0.041 (n 1) |
| 1-2 Horizon by Night | 62 | 29 | 4.76 | 0 | 8 | 25.7 | 26.8 | 25 | **0.036** (n 8) |
| 1-3 The Cactuar | 68 | 28 | 3.75 | 0 | 9 | 24.3 | 23.8 | 31 | 0.021 (n 4) |
| 1-4 The Gilded Dome | 68 | 30 | 1.04 | 0 | 9 | 23.6 | 24.0 | 16 | **0.030**, max 0.049 (n 8) |
| 1-5 The Crystal's Call | 70 | 29 | 2.38 | 0 | 8 | 19.1 | 19.8 | 20 | **0.032**, max 0.085 (n 5) |
| 2-1 Limsa Across the Water | 75 | 28 | 3.62 | 0 | 8 | 27.8 | 27.7 | 92 | 0.026 (n 1) |
| 2-2 Moonpath on the Bay | 80 | 28 | 1.35 | 0 | 10 | 24.9 | 23.7 | 22 | **0.057** (n 7) |
| 2-3 The Kraken's Sea | 77 | 29 | 1.96 | 0 | 10 | 23.9 | 24.3 | 57 | none isolated |
| 2-4 The Ferry Under Sail | 75 | 28 | 1.24 | 0 | 9 | 23.1 | 23.1 | 9 | 0.024, ΔE max 0.072 (n 4) |
| 2-5 Twin Lanterns | 70 | 31 | 1.22 | 0 | 9 | 19.7 | 19.9 | 22 | **0.042** (n 2) |

**The pilots' disc chroma** is 0.006–0.021 on the dressed scenes, median 0.013. On the composites it is 0.004–0.026, median 0.009. The levels' composites measure 0.026–0.053, median about 0.04.

## Coordinator's decisions: met or not

| Decision | Status | Evidence |
|---|---|---|
| Stage 1 from about 30 down to 21 | **Not met** (G2) | 27.2 down to 19.1 over 432 games; 26.6 down to 19.8 over 1296 |
| Stage 2 from about 27 down to 20 | Met | 27.8 down to 19.7; 27.7 down to 19.9 over 1296 |
| Each finale hardest, at least 2.5 below its 4th level | Met | 1-5 is 4.5 below 1-4 (4.2 over 1296). 2-5 is 3.4 below 2-4 (3.2 over 1296) |
| A 432-game ramp in every report | Met | Reproduced exactly from seeds 1–432 |
| No painting twice in a row | Met | Sources in order: map, ours, ours, Ul'dah, ours / Limsa, ours, chart, ours, ours |
| 1-3 and 2-4 re-sourced; Merlthor parked; 2-1 given a Limsa landmark and a distinct crop | Met | 2-1's crop ends at x 1720; stage 5 keeps x > 1700 |
| Game-art share near 2/3 | **Not met** (G4) | 4 of 10 |
| Stage 2 fullness (about 75 pieces or more) and about 2% framing | Partly (G6) | Pieces 75, 80, 77, 75, **70**. Framing 3.62, **1.35**, 1.96, **1.24**, **1.22** |
| 3–7 extra candidates | Met | 28–31 candidates (3–6 extra) |
| No dead first shots | Met | 0 no-hit angles on all ten |
| No cheap difficulty | Met by the gate's letter, not its intent (G3) | No single low orange reaches 30%, but stage-1 losses still turn on low oranges |
| Ghost discs: quiet by low frequency, never per peg; ghost check ≤ about 0.066 | **Not met** (G1) | Eight of ten recipes still use per-peg quiet. The check passes by its number, but the discs are visible |
| A warm first jewel on one stage-2 board | Met | 2-5: 324° first, share 0.81 (Nit G8) |

## Verdicts

| Level | Verdict | Reads at a glance | Blocking / other findings |
|---|---|---|---|
| 1-1 Road to Horizon | APPROVE | Yes: a route of stops on the map; the compass rose now reads as engraving | G2, G3, G7 |
| 1-2 Horizon by Night | **REVISE** | The painting reads (town, water tower, derrick); the layout mostly does not | **G1**, G5, G3 |
| 1-3 The Cactuar | APPROVE | Yes, strongly; the interior is open apart from the face | G3 |
| 1-4 The Gilded Dome | **REVISE** | Yes: the crown of brick over the dome | **G1** (coins in the sky), G9 |
| 1-5 The Crystal's Call | **REVISE** | Yes | **G1** (coins on the aurora), G2 |
| 2-1 Limsa Across the Water | APPROVE | Partly: a city with a great canopy outlined in orange. "Limsa" comes from the title more than the board | G7 |
| 2-2 Moonpath on the Bay | **REVISE** | Yes: the moon's road, the pier, the ship | **G1** (coins in the rose sky), G6 |
| 2-3 The Kraken's Sea | APPROVE | Yes: the kraken ring, the galleon, the flock, the whirlpool; calmer without the rhumb lines | G7 |
| 2-4 The Ferry Under Sail | APPROVE | Yes, the set's strongest new read | G3, G6; discs borderline (G1) |
| 2-5 Twin Lanterns | **REVISE** | Yes: symmetric gate, lanterns and moving reflection | **G1** (coins on the rose), G6, G8 |
| **Set** | **REVISE** | | |

## Round-1 findings

- **M1 (1-3: same painting twice, stage 4's subject, weak read, dead chute): resolved.**
  - 1-2 is now our Horizon painting, the cactuar has moved to 1-3, and 1-4 is stage 1's only Ul'dah board.
  - There are 0 no-hit angles on every board.
  - The new 1-2 brings its own weak layout read (G5).
- **M2 (2-4: chart twice, dead right half, cheap difficulty): resolved.**
  - 2-4 is a new subject that reads instantly, with 0 no-hit angles and a median of 9 pegs per first shot.
  - Low oranges still lead its losses, but the gate allows them (G3). The "spray" orange at (672, 432) is the top holdout at 24%. Next come (232, 440) at 16% and (154, 456) at 14%.
- **M3 (the dress prints a disc round every peg): NOT resolved.** It stays Major as G1.
- **M4 (finales not hardest, plateau): mostly resolved.**
  - Both finales are now the hardest by a wide margin, and stage 2 is in its band.
  - Stage 1 misses its band at both ends, and both stages have flat middles (G2).
- **M5 (2-1 plain; Limsa does not read): resolved.**
  - 75 pieces and 3.62% framing.
  - The canopy is an all-candidate outline, so the subject is drawn in orange every game.
  - The arch crowns are open, there is a swell line, and nothing is unreached.
  - The Limsa identity is still carried more by the title than the board. I accept this.
- **m1 (stage-2 fullness): partly resolved** (G6).
- **m2 (exactly 25 candidates): resolved.** Every board has 28–31.
- **m3 (cactuar outline broken, crown on a tuft): resolved.** No pegs cross the body, and the crown is clear.
- **m4 (2-2's low orange): resolved.** (292, 510) is blue; the lowest holdout is (446, 500) at 6%.
- **m5 (dead first shots on 1-1 and 2-2): resolved.** Both have 0 no-hit angles.
- **m6 (1-1 protan): resolved.** p10 is 0.135.
- **m7 (ramp figures): resolved.** Every report has the 432-game ramp, and I reproduced it bit for bit.
- **n1 (1-4 terrace row): resolved.** The row is staggered on two steps.
- **n2 (palette): resolved as asked.** 2-5 has a rose first jewel. A residual is left as Nit G8.

## New findings

### G1 — Major — 1-2, 1-4, 1-5, 2-2, 2-5 (2-4, 2-1 and 1-3 borderline): the jewel's per-peg quiet prints a coloured coin round every peg
**Evidence**
- **The rule is not followed.** The coordinator's rule is "quiet by low frequency, never per peg". The recipes still quieten per peg:
  - 1-2 `quiet [30, 12]`;
  - 1-5 `[26, 12]`;
  - 1-3, 1-4, 2-1, 2-2, 2-4 and 2-5 `[22, 12]`.

  Only 1-1 `[50, 14]` and 2-3 `[44, 12]` merge into a band.
- **How it prints a coin.** In `dress.py` the quiet does two things within 12–22 units of each piece. It cuts the regions' chroma (`rm * (1 − 0.6·near)`), and it sets the jewel's keep mask (`1 − 0.5·near`). So every peg gets its own coin of the base colour inside the region's colour.
- **Seen at native 1x in play:**
  - **1-5:** a dark-blue coin round every peg on the green aurora;
  - **2-2:** blue coins round every sky peg in the rose dusk;
  - **2-5:** blue coins on the rose band and the reflection;
  - **1-4:** grey-maroon coins round the nine sky oranges;
  - **2-4:** fainter coins in the violet sky.
- **The coins stay after the pegs clear.** The dressed scenes (`build/dressed/*.png`, no pieces, no veil) show the same coins. So they are baked into the scene, not drawn by the per-piece veil.
  - The README maps `quiet` to the runtime's `near` palette term, which is built once at load. So in game the coins stay where cleared pegs were.
  - On 1-4's cleared board the sky shows eight dark discs about 25 units across.
- **Measured against the pilots.** Chroma alone, ground 5–12 units from an isolated peg against the open ground 32–48 units out:

  | | Dressed scene | Composite |
  |---|---|---|
  | Levels | 2-2 0.057, 2-5 0.042, 1-2 0.036, 1-5 0.032 (max 0.085), 1-4 0.030 (max 0.049) | 0.026–0.053 |
  | Pilots | 0.006–0.021 | 0.004–0.026, median 0.009 |

  base-p2 has pegs on the same pink cloud colours as 1-4's sky and shows no coins.
- **Two claims in the round-2 context are contradicted.**
  - 2-2's sky is said to be "painted, not dressed, so the quiet prints no discs". In fact the recipe still dresses a rose region over the sky (`#B0566E`, y < 304, and a disc at the moon) with `quiet [22, 12]`. It has the strongest coins of the ten.
  - 1-4's sky was darkened by `tone`, but the rose region over y < 330 puts the chroma back, and the per-peg quiet takes it out again round each peg.
- **Why the ghost check passes.** It takes a full ΔE on the cleared board against the pilots' 0.066. The pilots' ghosts are mostly lightness (their veil), and these are chroma, so the number passes while the eye does not.

**Fix**
1. Replace every per-peg quiet with a low-frequency one. Either:
   - lower the region's chroma evenly where candidates sit (for example 1-4's sky region 0.075 → 0.05, 2-2's 0.065 → 0.045); or
   - widen the quiet to 44 units or more and blur it, so it becomes one band along the layout (what 1-1 and 2-3 already do).
2. On 2-5, where the rose is the base band and not a region, drop the keep-mask quiet and check F9 instead.
3. Re-check F9 after each change. 1-4 is at 0.119, so if F9 drops, lift orange's ground some other way. Do not reprint coins.
4. Add a chroma-only ghost term to the readcheck, measured on the dressed scene round every still peg (not only isolated ones). Gate it at the pilots' maximum of about 0.026. Add a known-bad self-test case with `quiet [22, 12]` over a saturated region.

### G2 — Minor — the ramp: stage 1 misses its band at both ends, and both stages have flat middles
**Evidence** (per 48 games; 432 games / 1296 games)
- **Stage 1:** 27.2 / 26.6, 25.7 / 26.8, 24.3 / 23.8, 23.6 / 24.0, 19.1 / 19.8.
  - The first board of the campaign sits about 3 below the asked-for "about 30".
  - Over 1296 games, 1-2 is no easier than 1-1 is hard (26.8 against 26.6), and 1-3 and 1-4 are tied (23.8 against 24.0). So stage 1 is two plateaus and then a 4.2 drop.
- **The "known gap" reasoning for 1-5 does not hold.** 1-5 only needs to be 2.5 below 1-4 (23.6–24.0), so 21.0–21.5 satisfies both the floor and the finale rule. 19.1 is about 2 harder than it needs to be.
- **Stage 2:** the middle three are flat. Over 1296 games they are 23.7, 24.3 and 23.1, so 2-3 is easier than 2-2.
- **Across the stages:** at each position, stage 1 is as hard as stage 2 (26.6/27.7, 26.8/23.7, 23.8/24.3, 24.0/23.1, 19.8/19.9). In play, Multiball makes stage 2 easier, so the campaign may get easier from stage 1 to stage 2.

**Fix**
- Ease 1-1 by about 3, using its low route oranges (G3).
- Ease 1-5 by about 1.5, for example by making the crystal's low tip (440, 478) or (530, 460) blue.
- Separate 1-3 from 1-4 by about 1.5 by easing 1-3: (378, 449) is its top holdout at 28% (G3).
- Separate 2-2 from 2-3 by about 1. Either harden 2-3 or ease 2-2 at (252, 436) or (332, 436).
- Decide neighbouring levels on 864 games or more. 432 games cannot separate gaps under about 1.5.

### G3 — Minor — 1-1, 1-2, 1-3, 2-4: low oranges still decide losses; the cheap-difficulty gate is met by its letter only
**Evidence** (1296 games)
- **1-1:**
  - 49% of lost games end with an orange at y 440 or below still on the board.
  - The low candidates are 21% of the deal but 41% of the oranges left over (y ≥ 430), 1.9 times their share.
  - Its top holdout, (550, 439) at 23%, sits one unit above the gate's line. (424, 466) is 21% and (395, 449) is 13%.
  - This is the first board every player sees.
- **1-2:** (419, 466) is left in 23% of lost games and (284, 448) in 22%. They are 7% of the deal but 19% of the leftovers, 2.8 times their share.
- **1-3:** (378, 449) is 28%, just under the 30% gate.
- **2-4:** the bow "spray" orange (672, 432) is 24%, eight units above the line. It is decoration, not the ship.

**Fix**
- Make those oranges blue, or lift them above y 430 onto the subject:
  - 1-1: the Horizon-end stops' lower moons;
  - 1-2: the switchback road's two candidates;
  - 1-3: (378, 449);
  - 2-4: (672, 432).

  This is also how 1-1 and 1-3 meet G2.
- Extend the gate to the over-representation ratio. Fail when the low candidates' share of the leftovers is more than 1.5 times their share of the deal at y 430 or below, or when any single orange at y 430 or below is left in 25% or more of lost games.

### G4 — Minor — set: the game-art share is 4 of 10 against "near 2/3"
**Evidence**
- 1-1, 1-4, 2-1 and 2-3 are game paintings; the other six are ours.
- To reach two thirds across a campaign of about 55 levels, stages 3–11 need at least 33 of their 45 boards from game paintings, about 73%.
- **Stage 4 is left very little.** 1-4 crops 2080 × 1560 of the 3840-wide Thanalan painting (x 800–2880, mirrored). Stage 4, Ul'dah's home, inherits only two strips outside that crop: 960 px in the east and 800 px in the west. That is barely one sharp board, for a stage of five.

**Fix**
- Have the coordinator explicitly accept the share as a campaign budget, and record "stages 3–11 need about 73% game art" in the README's decisions.
- Or move one stage-2 board of ours onto a game painting.
- Either way, list Ul'dah sources (loading images and maps) for stage 4 now, before 1-4's crop is fixed for good.

### G5 — Minor — 1-2: the layout is scatter; the painting carries the subject
**Evidence**
- 62 pieces, the fewest in the set.
- The roofline "band" is irregular: 6–7 moons over the western roofs, a gap over the tank's legs and only two over the eastern block. At 1x it does not read as a traced line.
- 15 stars (11 of them candidates) fill the sky as a loose field at the band's height, so the eye cannot separate band from stars.
- The lower half has a dead-straight cliff row of about 15 pegs and a scatter of campfires over a teal plane that reads as sea or lawn, not a mesa.
- The README's own lesson is "buy fullness with structures, not scatter".

**Fix**
- Trace the roofline as a continuous band at even spacing over every roof block, the derrick and the tank. Put most of the candidates on it, the derrick head and the tank crown.
- Cut the sky stars to about 6, with 2–3 candidates.
- Give the cliff a stepped ledge line instead of a straight row.
- Re-measure the ramp afterwards (G2).

### G6 — Minor — 2-2, 2-4, 2-5: framing below about 2%, and 2-5 below 75 pieces
**Evidence**
- Framing is 1.35% on 2-2, 1.24% on 2-4 and 1.22% on 2-5.
- 2-5's willow fronds sit at all four corners, yet framecheck dropped 17 elements for clearance.
- 2-5 has 70 pieces. The designer measured that each blue sky peg made the board harder, which is fair.

**Fix**
- Raise framing to about 2% on all three:
  - 2-5: denser fronds at the top corners, where nothing plays;
  - 2-2: pilings and rocks at the lower corners;
  - 2-4: rigging lines or a dark gunwale foreground at the bottom corners.
- Add pieces to 2-5 as structure, not scatter: a dotted far shore, or stepping stones under the gate. Re-tune its ramp with one candidate.

### G7 — Minor — 2-1 (also 1-1 and 2-3): the stuck rule fires in many games
**Evidence**
- Over 1296 games, the stuck rule fires in 92 games on 2-1 (7.1%), 61 on 1-1 (4.7%) and 57 on 2-3 (4.4%). The other seven boards are at 2.4% or less, and 2-4 is at 0.7%.
- The first-shot sweep shows only 1 stuck first shot on 2-1, at (560, 380) on the bridge deck, and 4 on 1-1. So the traps open later, as pegs clear.

**Fix**
- Log where the ball rests whenever the rule fires in `mfcheck play`, as `stuck` does for first shots, and open those spots.
- 2-1's deck and lamp rows at 36 spacing, with candidates between them, are the first place to look.

### G8 — Nit — 2-5 and set: the warm jewel is magenta, at the F7 threshold, and the set's first jewels are still one family
**Evidence**
- 2-5's first jewel is 324° (magenta-rose), and its two jewels are exactly 60° apart, the F7 limit.
- The other nine first jewels all lie between 250° and 293°. The second jewels alternate between rose (333–3°) and teal (174–206°).
- 2-4 (287/351) and 2-5 (324/264) are the same two hues in different proportions, back to back.

**Fix:** move 2-5 toward lamplight amber or a true rose (about 0–30°). That gives the twins' board its own palette and clears F7 by a margin.

### G9 — Nit — 1-4: nine sky oranges as a loose 3×3 scatter
**Evidence**
- The nine oranges in 1-4's upper-left sky carry no shape. They are also where G1's coins are worst.
- (150, 150) is left in 18% of lost games.

**Fix:** set the stars on the painting's spires (finials at a legal distance) or on a short arc, and keep 4–5 of them as candidates.

## What works
- **Variety:** ten distinct reads across ten boards — map, skyline, creature, dome, constellation; canopy, moonpath, chart, ship, mirrored gate. No painting repeats back to back, and the stage-2 movers (orbit, swell, reflection) each mean something.
- **2-4:** reads at a glance and has the set's lowest stuck rate.
- **1-3:** the cactuar is the stage's clearest subject.
- **2-5:** the reflection is still the best use of movers.
- **Difficulty from the subject:** it now comes mostly from the subject's own places. 1-4's top holdouts are the dome finial and the crown. 2-5's are the lantern heads, and 2-3's is the flock.
- **Near-miss tension:** most lost games end with 1–2 oranges left.
