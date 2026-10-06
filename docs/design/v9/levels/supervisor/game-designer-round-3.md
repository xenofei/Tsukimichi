# Game designer, levels round 3

6 October 2026. Worktree `agent-a570460c913ca1c79` at `35cd98b5`. I edited no repo files and did not run `build`. `git status` is clean after my runs. One side effect: `mfl.py selftest` rewrote its own four gitignored fixtures in `docs/design/v9/levels/build/selftest-*.json`.

**Overall verdict: APPROVE.** My one round-2 Major (G1, the coin printed round each peg by the jewel's quiet) is resolved:
- The quiet is now low-frequency on every board.
- My own chroma measure on the dressed scenes now sits inside the approved pilots' range.
- A sound print gate guards it, with self-tests.

No Major remains. Two Minors deserve attention before stage 3 is authored:
- **G10.** The ramp figures are biased about 0.8 per 48 toward "harder" because levels are tuned and measured on the same 864 seeds. On fresh seeds, 2-1 and 2-2 are tied.
- **G11.** On 2-5, low oranges are in 83% of lost games. On 1-1, the two lowest are in 58%. The gate's line at y 430 is now being hugged rather than met.

## Method
- **Read:**
  - `round3-context.md` and `round2-context.md`;
  - my round-2 report;
  - the README;
  - the commit's diffs to `dress.py`, `readability.py`, `stagecheck.py`, `engine.py` and the layouts (base-01, 02, 03, 04, 05, 06, 07, 09, 10);
  - all ten reports;
  - the runtime scene-recipe doc on main (its `near` term).
- **Looked at:**
  - all ten 1x composites;
  - both stages' dressed scenes as contact sheets;
  - cleared and undressed boards for 2-4, 2-2 and 2-5;
  - 2x crops of 1-1;
  - round 2 against round 3 for 1-1;
  - maps of the dress's own chroma change (dressed minus the cached graded scene) for 1-4, 2-2, 2-4 and 2-5.
- **Ran (read-only):**
  - `mfl.py stage 1` and `stage 2`: all PASS.
  - `mfl.py selftest`: all ok.
  - `mfl.py stuck base-01`.
  - `mfcheck play` at each level's own number for **3456 greedy games** per level. Seeds 1–864 reproduce every report's ramp exactly. Seeds 865–3456 (2592 games) are fresh. The standard error is ±0.40 per 48 over 3456 games and ±0.47 over the fresh seeds. Block-to-block spread is binomial: the ratio of observed to expected standard deviation is 0.7–1.2, and 1.5 on 2-2.
  - My parse of who decides lost games, with holdouts keyed by home, at y 400, 415 and 430.
  - My round-2 disc measure (chroma, ground 5–12 units from an isolated peg against 32–48 out) on the ten dressed scenes and the six pilots.
  - A measure of the dress's change inside the layout's envelope against outside it.
- **Scratch files:** `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/gd3/lv/`. The play logs are in `ramp4/`, with `ramp4.py`, `discs.py` and `envelope.py` alongside.
- **Not modelled:** powers, the runtime converter, in-game rendering, and human play.

### Key figures

| Level | Pieces | Candidates | Framing % | Ramp /48: report (seeds 1–864) | Ramp /48: 3456 games | Ramp /48: fresh 865–3456 | Lost games with an orange at y ≥ 415 left | Top holdout (share of lost games) | Games where the stuck rule fires | Disc chroma, dressed scene: median (n) | Print gate: median / p90 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 82 | 28 | 2.18 | 29.3 | 29.03 | 28.93 | **0.58** | (368,427) 33%, (504,415) 30% | **5.4%** | 0.006 (1) | 0.0074 / 0.0224 |
| 1-2 Horizon by Night | 67 | 28 | 4.76 | 27.8 | 28.32 | 28.50 | 0.25 | (678,413) 19% | 3.1% | 0.003 (7) | 0.0052 / 0.0207 |
| 1-3 The Cactuar | 69 | 28 | 3.75 | 26.6 | 27.00 | 27.15 | 0.23 | (387,415), (567,404) 23% | 3.1% | 0.001 (5) | 0.0072 / 0.0171 |
| 1-4 The Gilded Dome | 68 | 28 | 1.04 | 24.4 | 24.92 | 25.09 | 0.36 | (556,420) 22% | 1.0% | 0.012 (8), max 0.034 | 0.0056 / 0.0117 |
| 1-5 The Crystal's Call | 70 | 29 | 2.38 | 21.4 | 21.90 | 22.06 | 0.41 | (519,336) 21%; (440,478) 20% | 0.9% | 0.003 (5) | 0.0056 / 0.0119 |
| 2-1 Limsa Across the Water | 74 | 28 | 3.62 | 27.4 | 27.49 | 27.52 | 0.16 | (330,400) 21% | 2.8% | 0.002 (1) | 0.0036 / 0.0126 |
| 2-2 Moonpath on the Bay | 81 | 28 | 2.18 | 26.6 | 27.25 | 27.46 | 0.18 | (682,382) 19% | 2.0% | 0.007 (7) | 0.0050 / 0.0129 |
| 2-3 The Kraken's Sea | 77 | 29 | 1.96 | 23.7 | 24.86 | 25.26 | 0.37 | **(558,156) 38%** | 3.8% | none isolated | 0.0045 / 0.0143 |
| 2-4 The Ferry Under Sail | 77 | 28 | 3.10 | 22.3 | 23.76 | 24.26 | 0.00 (0.29 at y ≥ 400) | (463,402) 19% | 0.6% | 0.005 (4) | 0.0024 / 0.0089 |
| 2-5 Twin Lanterns | 72 | 29 | 1.49 | 18.9 | 19.92 | 20.24 | **0.83** | (576,471) 18%, (224,471) 17%, (632,471) 17%, (400,514) 17% | 1.5% | 0.021 (2) | 0.0075 / 0.0147 |

**The pilots' disc chroma** on their dressed scenes is 0.006–0.021 (median), with a maximum of 0.043. In round 2 the levels measured 0.021–0.057.

## Coordinator's decisions: met or not (on 3456 games)

| Decision | Status | Evidence |
|---|---|---|
| Stage 1 from about 30 down to 21 | Met | 29.0 down to 21.9 |
| Stage 2 from about 27 down to 20 | Met | 27.5 down to 19.9 |
| Each finale hardest, at least 2.5 below its 4th level | Met | 1-5 is 3.0 below 1-4 (fresh seeds 3.0). 2-5 is 3.8 below 2-4 (fresh 4.0) |
| Each level at least 0.5 harder than the one before (`stagecheck`) | **Not met on fresh seeds** (G10) | 2-1 27.49 against 2-2 27.25 (fresh 27.52 against 27.46). Stage 1 holds: steps 0.7, 1.3, 2.1, 3.0 |
| No painting twice in a row | Met | Unchanged |
| Game-art share near 2/3 | Recorded as a budget | README: stages 3–11 need about 73%. Residual Nit under G4 |
| Stage 2 fullness (about 75 pieces) and about 2% framing | Mostly met | 74, 81, 77, 77, **72** pieces. Framing 3.62, 2.18, 1.96, 3.10, **1.49** |
| No dead first shots | Met | 0 no-hit angles at 0.25° on all ten |
| No cheap difficulty | Met by the gate; not by intent on 2-5 and 1-1 (G11) | See G11 |
| Ghost discs: quiet by low frequency, never per peg | **Met** in the pipeline (G1). Runtime fidelity is unverified (G13) | See G1 and G13 |

## Verdicts

| Level | Verdict | Reads at a glance | Open findings |
|---|---|---|---|
| 1-1 Road to Horizon | APPROVE | Yes: a route of stops on the map; the compass rose reads as engraving | G11 (the two lowest oranges), G7 |
| 1-2 Horizon by Night | APPROVE | Yes now: the roofline is one even band, mostly orange, over the town; the tower crown and derrick read | G5 residual (Nit) |
| 1-3 The Cactuar | APPROVE | Yes, the set's clearest creature | — |
| 1-4 The Gilded Dome | APPROVE | Yes: a crown of brick over the dome; the sky's coins are gone | G9 residual (Nit) |
| 1-5 The Crystal's Call | APPROVE | Yes | G12 (crystal foot) |
| 2-1 Limsa Across the Water | APPROVE | The canopy outline reads; "Limsa" still comes mostly from the title | G10 (tied with 2-2) |
| 2-2 Moonpath on the Bay | APPROVE | Yes: moon road, pier, ship; the rose sky has no coins | G10 |
| 2-3 The Kraken's Sea | APPROVE | Yes | G17 (one peg) |
| 2-4 The Ferry Under Sail | APPROVE | Yes, the set's strongest read | G14 (envelope) |
| 2-5 Twin Lanterns | APPROVE | Yes: mirrored gate and lanterns, the reflection moving | G11 (reflection), G6 residual |
| **Set** | **APPROVE** | | |

## Round-2 findings

- **G1 (Major: per-peg coins from the jewel's quiet): RESOLVED.**
  - `dress.py` now blurs the quiet field over `max(a, 40)` units with gain 1.6. There is no per-peg path left in the code.
  - **My chroma measure on the dressed scenes**, round 2 against round 3:

    | Level | Round 2 | Round 3 |
    |---|---|---|
    | 1-2 | 0.036 | 0.003 |
    | 1-5 | 0.032 | 0.003 |
    | 2-2 | 0.057 | 0.007 |
    | 1-4 | 0.030 | 0.012 (max 0.034) |
    | 2-5 | 0.042 | 0.021 (n 2) |

    All are inside the pilots' 0.006–0.021 median range, and 1-4's max is under the pilots' max of 0.043.
  - **Seen:** the 1x composites of 1-4, 1-5, 2-2 and 2-5 show no coins. 1-4's sky is clean, and so are 2-2's rose sky and 1-5's aurora.
  - **The print gate does what I asked, and more.** It measures dressed minus undressed, round every still peg (not only isolated ones), as a per-sector median. It is never vacuous, and its self-tests include the cluster case. It replaces my proposed chroma-only term, and every board passes it with margin. 1-1's p90 of 0.0224 is the closest to the 0.026 cap.
  - The dark smudges on the cleared boards (2-2's sky, 2-4) are the baked veil. The runtime fades the veil with each piece, and the unchanged ghost check covers it.
  - Two things remain: runtime fidelity (G13) and a large-scale side effect (G14).
- **G2 (Minor: ramp, stage 1 band, flat middles): MOSTLY RESOLVED.**
  - Stage 1 now meets its band. Its steps are clean over 3456 games: 29.0, 28.3, 27.0, 24.9, 21.9.
  - At every position, stage 2 is now 1–2 per 48 harder than stage 1, so my cross-stage worry is answered.
  - What remains is G10: the reported figures are biased, and 2-1 and 2-2 are tied.
- **G3 (Minor: low oranges deciding losses): PARTLY RESOLVED.**
  - Every specific orange I named is blue now: 1-1's low stops, 1-2's switchback road, 1-3's (378, 449) and 2-4's spray.
  - The gate is keyed by piece home and has the ratio clause and self-tests.
  - But the holdouts moved to just above the line, and 2-5 passes only because 45% of its candidates are low (G11).
  - 1-5's crystal foot is still orange, contrary to the round-3 context (G12).
- **G4 (Minor: game-art share): RESOLVED as a recorded decision.** The README records 4 of 10 and the 73% need for stages 3–11. Residual Nit: no list of Ul'dah sources for stage 4 exists yet.
- **G5 (Minor: 1-2 layout is scatter): RESOLVED.**
  - The roofline is one band in three runs at 32 spacing, at y 322–346, three of every four moons candidates.
  - The derrick legs, tank stilts, tank crown and finial carry the rest.
  - The cliff is stepped into ledges at y 410–450.
  - Residual Nit: the sky still has 12 stars (8 high, 4 faint; 4 of them candidates) against the asked "about 6". They sit above y 250, so they no longer blur the band.
- **G6 (Minor: framing and fullness): MOSTLY RESOLVED.**
  - 2-2 is at 2.18% and 2-4 at 3.10%.
  - 2-5 rose to 1.49% and 72 pieces, but framecheck still drops 27 framing elements for clearance. Residual Nit.
- **G7 (Minor: the stuck rule fires often): NOT DONE, downgraded to Nit.**
  - Rest positions are still not logged in `play`.
  - 2-1 fell from 7.1% to 2.8% of games and 2-3 is at 3.8%.
  - 1-1 is now the highest, at 186 of 3456 games (5.4%; round 2 was 4.7%).
  - `mfl.py stuck base-01` shows 8 of 681 first shots, at 8 different places. No trap is concentrated, and the rule fires on 193 of about 47,000 shots (0.4%).
- **G8 (Nit: 2-5's warm jewel): RESOLVED.**
  - 2-5 is now 343°/273°, 70° apart (F7 margin 10°), and reads rose at 1x.
  - Residual, folded into G15: 2-4 (291/357) and 2-5 (343/273) are still the same violet and rose pair, swapped in proportion.
- **G9 (Nit: 1-4's sky scatter): RESOLVED.**
  - The corner stars (150, 150), (620, 172) and (650, 280) are blue now, and so is the top finial (548, 100).
  - Of the ten upper-left oranges, seven are the aqueduct's posts and deck, which is structure the painting shows. Only (230, 150), (300, 128) and (262, 200) are loose sky.

## New findings

### G10 — Minor — Ramp: tuning and measuring on the same 864 seeds biases the reported ramp; 2-1 and 2-2 are tied on fresh seeds
**Evidence**
- **The bias.** Seeds 1–864 reproduce the reports exactly. Fresh seeds 865–3456 (2592 games) give, as fresh minus reported:

  | Level | Fresh minus reported |
  |---|---|
  | 1-1 | −0.4 |
  | 1-2 | +0.7 |
  | 1-3 | +0.55 |
  | 1-4 | +0.7 |
  | 1-5 | +0.66 |
  | 2-1 | +0.1 |
  | 2-2 | +0.86 |
  | 2-3 | +1.56 |
  | 2-4 | +1.96 |
  | 2-5 | +1.34 |

  - The mean is +0.80. Each difference has a standard error of about 0.9, so the mean's is about 0.3: the bias is roughly 2.7 standard errors, not noise.
  - Nine of ten levels are easier on fresh seeds. The two largest shifts are on levels tuned against seeds 1–864 (2-4 this round) or seeds 1–432 (2-3 in round 2).
  - Per-block spread is binomial, so this is selection bias. A variant kept because it hit its target on fixed seeds carries those seeds' luck.
- **The tie.** Over 3456 games, 2-1 is 27.49 and 2-2 is 27.25, a step of 0.24 (fresh 27.52 against 27.46). `stagecheck` asks for at least 0.5, and it passes only on the tuning seeds (27.4 against 26.6).
- 1-1 to 1-2 is 0.71 over 3456 games (fresh 0.43), which is borderline.
- The bands and the finale gaps hold on fresh seeds.

**Fix**
- Tune on seeds 1–864, but have `stage` and the report's ramp use a held-out block: seeds 865–2592, or simply 2592 games. 3456 games for all ten levels ran in a few minutes here.
- Then separate 2-1 from 2-2 by about 1. Either:
  - ease 2-1 with one high, open candidate, keeping it at 27–28 within the band; or
  - harden 2-2 with one blue star in an open sky lane (the README's lever: about 0.5–1 each).
- Record in the README that ramp figures from the tuning seeds are optimistic by about 0.8.

### G11 — Minor — 2-5 and 1-1: low oranges still decide most losses; the gate is met by its letter
**Evidence** (3456 games, holdouts keyed by home)
- **2-5:**
  - 83% of lost games end with an orange at y ≥ 471 still on the board: the reflected heads (y 437–471) and the reflected arch's U (y 486–514), just above the bucket.
  - 13 of its 29 candidates (45%) are at y ≥ 430.
  - The ratio clause passes (0.53 of the leftovers against 0.45 of the deal, 1.19×), and no single orange reaches 25%: the top four, all low, are 17–18%.
  - So the clause cannot catch a board that puts nearly half its deal in the approach.
  - Round 2's 2-5 was the same: 85% in my 1296-game logs. I missed it then because the holdouts were keyed by where movers stopped.
- **1-1** (the first board of the campaign):
  - The two lowest candidates, (368, 427) and (504, 415), sit 3 and 15 units above the gate's line.
  - They are left in 33% and 30% of lost games. One or the other is in 58% of all losses.
  - That is 4.8× their 7% share of the deal at y ≥ 415, and 3.7× at y ≥ 400.
  - Round 2 had 49% of losses at y ≥ 440, so the concentration grew while the board moved over the line.
  - (504, 415) is the bridge stop's lowest moon, shielded by its own ring. (368, 427) sits under the road's dotted column.
- **The same pattern elsewhere.** On 1-3, 1-4, 2-1 and 2-2, the top holdouts are also the lowest candidates, at y 400–427. At y ≥ 400 the ratio is 1.7–2.8× on every board but 1-5 (1.4×) and 2-5 (1.2×).

**Fix**
- **2-5:**
  - Make the reflected arch's three lowest moons (365, 507), (400, 514) and (435, 507) blue.
  - Make each reflected head 3 of 5 candidates instead of 4.
  - Put the difficulty back on the lantern heads and the arch's flanks above the waterline.
  - The finale has 4.0 of margin over 2-4 on fresh seeds, so it can afford about 1 per 48.
- **1-1:**
  - Make (504, 415) blue and move its candidate to the bridge ring's upper moons.
  - Swap (368, 427) for a route moon on the main road.
  - Re-measure on held-out seeds (G10).
- **Gate:**
  - Add a cap on the deal itself: low candidates at most about 25% of the candidates.
  - Measure the single-orange cap at y ≥ 400 too, or make it independent of y: flag any orange left in 30% or more of lost games for review.

### G12 — Minor — 1-5: the crystal's foot is still a candidate, contrary to the docstring and the round-3 context
**Evidence**
- `layouts/base-05.py` places every figure star with `orange=True`. In `json/base-05.json`, peg 7 at (440, 478) has `canBeOrange: true`, and it is dealt orange in the composite.
- The docstring says "all candidates but the crystal's foot", and the round-3 context says "1-5's crystal foot … blue".
- It is the third holdout, left in 20% of lost games. It is the only candidate at y ≥ 430, so 20% of 1-5's losses turn on it.
- The gate passes it because the ratio clause has a 15% floor on the leftovers (1-5's low leftovers are 7%).

**Fix**
- Make (440, 478) blue (`orange=(x, y) != foot`), as documented.
- 1-5 is 3.0 below 1-4 on fresh seeds and will ease a little, so add one candidate higher on the figure, or one blue field star, to keep the finale gap at 2.5 or more. Re-measure on held-out seeds.

### G13 — Minor — G1's fix may not survive the runtime converter (unverified)
**Evidence**
- The pipeline's quiet is `clip(1.6 × blur(smoothstep(dist), σ = 40))`: it blurs the mask after the smoothstep, with gain.
- The runtime's `near` term (scene-recipe doc on main) supports `blur` of 0–40 units *before* the smoothstep, and has no gain. A blurred distance field still dips to its minimum at a lone peg, so the runtime may bring back a smaller disc round isolated pegs.
- The context lists the converter as not built.

**Fix**
- When the converter lands, run the print gate on `Tsukimichi.MoonfallRender` output for all ten levels.
- If the runtime cannot express "blur after smoothstep", add that option to `near` (it is a palette-build-time cost only), rather than approximating it.

### G14 — Nit — 2-4 (also 2-2, 1-4 and 1-1): the low-frequency quiet prints the layout's envelope
**Evidence**
- In the dress-change maps, the jewel is absent in one soft bubble shaped like each layout's envelope. On 2-4 it is a blue haze round the ship and the swell rows.
- The second jewel (rose) survives only at the horizon's two ends and the lower water. The rose share is 0.155 against F7's 0.15 floor.
- Measured as the dress's added chroma within 40 units of the pieces against 60 or more units away: 1-4 0.087, 2-5 0.076, 2-3 0.052, 1-1 0.050, 2-2 0.049.
- At 1x it reads as light round the subject, so it is not a defect today. But it follows the layout, not the painting's light (2-4's moon is top-left), and it stays after the board clears.

**Fix:** none needed now. Where a jewel band must survive under a dense layout, paint it into our painting, as the README's lesson says. Keep an eye on F7's second share on dense boards in later stages.

### G15 — Nit — Set palettes: neighbours across the stage boundary are not checked
**Evidence**
- 1-5 (274/205) is followed in play by 2-1 (269/188). The first jewels are 5° apart and the second 17° apart, which fails the README's own neighbour rule (30° in at least one jewel). `stagecheck` compares neighbours only within a stage.
- 2-4 (291/357) and 2-5 (343/273) are the same violet and rose pair, swapped in proportion (G8's residual). They pass by comparing first jewel with first.

**Fix:** have `stagecheck` take the previous stage's last level as the first row's neighbour, and compare jewel pairs unordered. For now, nudge 2-1's second jewel toward green-gold (about 120–150°) or 1-5's toward aquamarine (about 175°).

### G16 — Nit — Set: the cheap-difficulty rule has emptied the bottom third of most boards
**Evidence**
- Eight of ten boards have no candidate at y ≥ 430. The other two are 1-5 (one, G12) and 2-3 (one, at (564, 446)), plus 2-5's reflection.
- Every board's late game is played in y 120–427, and the lowest candidates, at y 400–427, become the holdouts (G11).
- This is fine for stages 1–2, but it removes a whole kind of shot from the vocabulary: bucket-zone play, bank shots off low bricks.

**Fix:** for later stages, bring low oranges back only as shielded subject features (behind a crown or on a slow mover), within the deal cap proposed in G11, and say so in the README's decisions.

### G17 — Nit — 2-3: one peg decides 38% of losses
**Evidence**
- (558, 156), the top of the flock, is left in 38% of lost games, up from 36% over 864 games. The next holdout is at 17%.
- It sits near the edge of a direct flight's reach. The README puts a sideways ball's drop at about 1.6e-3 × dx², so at dx ≈ 158 a ball falls about 40 units below the pivot.
- That is a fair skill shot, but it makes one placement the level's difficulty.

**Fix:** lower that flock candidate by about 15–20 units along the V, or make it blue and promote another flock moon. Re-measure (G10).

## What works
- **The coins are gone.** It is fixed at the root (low-frequency quiet), the gate cannot be satisfied vacuously, and the pilots' range is met on my own measure.
- **Stage 1's ramp** is now a real ramp on fresh seeds (28.9, 28.5, 27.2, 25.1, 22.1). Stage 2 is harder than stage 1 at every position, and both finales clear their 2.5 gap by 3.0 or more.
- **1-2's roofline** now reads as a traced skyline.
- **2-1's stuck rate** fell from 7.1% to 2.8%.
- **2-4** keeps the lowest stuck rate (0.6%) and the strongest read.
- **Variety:** ten distinct subjects and techniques, the movers meaning something (orbit, swell, reflection), and no painting repeated back to back.
