# Game designer, levels round 5

6 October 2026. Worktree `agent-a570460c913ca1c79` at `a52c841c`; round 4 was reviewed at `b32a74e2`. I edited no tracked files and did not run `build`. `git status` is clean after my runs. Side effects, all in the gitignored `build/`:
- `mfl.py selftest` rewrote its four fixture boards.
- My variant JSONs are in `build/json/gd5/`.

**Overall verdict: APPROVE.** I found no Major.

Round-4 findings:
- **G19 is resolved.** 1-1 to 1-2 is now 2.1 ± 0.4 over 6912 untuned games per level.
- **G18 and G11 are partly resolved.**
  - The red wall strips on 1-3 and 2-4 are gone, and F7 now gates second-jewel colour that sits only along the walls.
  - 2-5's envelope and its low reflection are unchanged.
  - 1-1's bridge is now a single holdout. A paired variant shows the bridge ring's shield causes it, not any one moon, and the spread rule forces a candidate there.

Two new Minors:
- **G20:** stage 2's first step (2-1 to 2-2) is 0.6 ± 0.4 pooled, and 0.02 on a third seed block. The new `stage` confirmation passes it at 1.05 by the luck of one block.
- **G21:** this round's disc term on 2-3 puts a per-peg coin round the candidate at (512, 118). It has the set's worst print (0.088). The print gate cannot catch it, because it judges only the median and the 90th percentile.

I would ship these ten levels as they are. Before release I would fix G21 (one recipe term) and open G20 (one candidate swap).

## Method

**Read:**
- `round5-context.md` and `round2-context.md`, plus my round-4 report;
- the full diff `b32a74e2..a52c841c`:
  - layouts base-01 and base-06; all ten recipe changes;
  - `dress.py` (`QUIET_BLUR_MIN`, `regionQuiet`), `author.py` (`NOTCH_MAX` 17, exact `common_cycle`), `mfl.py` (`stage`'s second-block confirmation), `readability.dress_print`, `stagecheck.py`, the README;
- all ten `report/*.json`.

**Ran:**
- `mfl.py selftest`: exit 0, 109 ok. The new cases fire: a 15-unit slot, crossing slides of periods 10 and 10.04, round 2's coin on 2-2, 2-1 and 1-4 (all three ran, none "not run"), quietBlur below 40 refused, and F7 with the second jewel only along the walls.
- `mfl.py stage 1` and `stage 2`: both exit 0, all PASS.
  - Stage 2 re-played 2-1 and 2-2 on its second block: 27.7 and 26.4, pooled to 27.55 and 26.5.
  - Its confirmation copies in `build/json` match `json/` byte for byte (checked with `cmp`).
- **Fresh ramp, a third block.** 2592 greedy games per level on seeds 5185–7776. This is outside the tuning seeds (1–864), the held-out seeds (865–2592), `stage`'s confirmation block (2593–4320) and my round-4 block (2593–5184).
  - I re-played 1-1 and 2-1, the two changed layouts, on 2593–5184.
  - For the eight unchanged levels I reused my round-4 logs, since their JSONs are unchanged.
  - Pooled with the report's held-out games, that is **6912 untuned games per level (±0.29 per 48)**.
- **Paired variants** on tuning seeds 1–864 and fresh 5185–7776:
  - `v01a`: 1-1 with (504, 415) blue and (533, 398) a candidate.
  - `v06a`: 2-1 with round 4's candidates on the new keystone geometry: (700, 320) a candidate and (686, 382) blue.
- **Holdouts** parsed by home; shares at y ≥ 400 and y ≥ 430.
- **Images.** Contact sheets of all ten dressed scenes (the cleared board) beside my round-4 sheets. Pairs at 760 px. 2x crops of 2-3 round (512, 118) and of 1-4's new tone disc. OKLab hue and chroma profiles across the walls on 1-1, 2-4 and 2-5. Where each board's second jewel lies (my `j2where.py`).
- **Scratch:** `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/gd5/`:
  - `ramp/` (block 3) and `ramp4/` (1-1 and 2-1 on 2593–5184);
  - `var/tune` and `var/fresh`;
  - `ana.py`, `ramp.py`, `j2where.py`, `prof.py`, `sheet.py`;
  - `img/`.

### Key figures

| Level | Pieces | Held-out (865–2592) | 2593–5184 | 5185–7776 | **Pooled, 6912 games** | Lost games with an orange at y ≥ 400 left (block 3) | Top holdouts (block 3, share of lost games) | Games where the stuck rule fires |
|---|---|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 82 | 30.69 | 30.87 | 30.80 | **30.80** | 0.41 | **(504, 415) 31%**, (608, 344) 17% | 4.9% |
| 1-2 Horizon by Night | 67 | 28.22 | 29.20 | 28.52 | **28.70** | 0.52 | (678, 413) 18%, (606, 416) 18% | 3.2% |
| 1-3 The Cactuar | 70 | 25.06 | 25.26 | 25.59 | **25.33** | 0.41 | (387, 415) 22%, (567, 404) 22% | 2.5% |
| 1-4 The Gilded Dome | 70 | 23.67 | 23.50 | 24.33 | **23.85** | 0.41 | (556, 420) 20%, (517, 386) 20% | 1.4% |
| 1-5 The Crystal's Call | 71 | 20.42 | 20.67 | 20.70 | **20.62** | 0.62 | (361, 336) 23%, (519, 336) 23% | 1.1% |
| 2-1 Limsa Across the Water | 74 | 27.39 | 27.65 | 27.04 | **27.35** | 0.39 | (330, 400) 20%, (506, 387) 17% | 2.8% |
| 2-2 Moonpath on the Bay | 82 | 26.58 | 26.61 | 27.02 | **26.76** | 0.54 | (548, 424) 22%, (682, 382) 19% | 1.7% |
| 2-3 The Kraken's Sea | 77 | 25.33 | 25.52 | 25.72 | **25.55** | 0.49 | **(558, 156) 37%**, then 17% | 3.4% |
| 2-4 The Ferry Under Sail | 77 | 24.17 | 23.83 | 24.35 | **24.11** | 0.31 | (463, 402) 21%, (319, 399) 18% | 0.6% |
| 2-5 Twin Lanterns | 74 | 19.17 | 20.06 | 19.00 | **19.44** | **0.80** (all at y ≥ 430) | (576, 471) 20%, (632, 471) 19%, (622, 262) 17%, (224, 471) 17% | 1.3% |

**Steps, pooled** (the standard error of a step is about 0.41):

| Stage | Steps, pooled | Block 3 alone |
|---|---|---|
| Stage 1 | **2.10**, 3.37, 1.48, 3.24 | 2.28, 2.93, 1.26, 3.63 |
| Stage 2 | **0.60**, 1.21, 1.44, 4.67 | **0.02**, 1.30, 1.37, 5.35 |

Fresh minus held-out over the ten levels averages +0.23 on block 3 and +0.18 on 2593–5184, which is noise. The held-out block reports honestly.

## Coordinator's decisions: met or not

| Decision | Status | Evidence |
|---|---|---|
| Stage 1 from about 30 down to 21 | Met | 30.8 down to 20.6, pooled |
| Stage 2 from about 27 down to 20 | Met | 27.35 down to 19.44 |
| Each finale the hardest in its stage, 2.5 or more below its 4th level | Met | 1-5: 3.24 below 1-4. 2-5: 4.67 below 2-4 |
| Each level at least 0.5 harder than the one before | Met on the point estimates. **2-1 to 2-2 is not resolved** (G20) | 0.60 ± 0.41 pooled; 0.02 on block 3 |
| No cheap difficulty | Met by the gate. Not by intent on 2-5 (G11) | 2-5: 80% of lost games leave a reflected orange at y ≥ 430 |
| Ghost discs: quiet by low frequency, never per peg | **Broken once, deliberately: 2-3's disc at (512, 118)** (G21) | Print worst 0.0879 at that peg |
| Fullness, framing, dead first shots, painting rules | Unchanged from round 4 | Pieces 82/67/70/70/71 and 74/82/77/77/74; `stage` PASS |

## Verdicts

| Level | Verdict | Reads | Open findings |
|---|---|---|---|
| 1-1 Road to Horizon | APPROVE | Yes: the route of stops | G11 residue (now a Nit: the bridge ring shields its low moon); thin first-jewel edge at the walls (Nit, in G18) |
| 1-2 Horizon by Night | APPROVE | Yes | G18 residue: the dark bowl in the green field is wider with `[40, 18]` |
| 1-3 The Cactuar | APPROVE | Yes. The red wall strips are gone; the red stays in the foreground under the lowest dune, which reads as sand | Steps: 3.37 into 1-3 is the stage's largest (Nit) |
| 1-4 The Gilded Dome | APPROVE | Yes. The new tone disc at (310, 418) does not show on the textured painting (2x crop) | — |
| 1-5 The Crystal's Call | APPROVE | Yes | N-a (F7 sits on both floors: 60°, 0.158) |
| 2-1 Limsa Across the Water | APPROVE | As before | G20 (no clear step to 2-2) |
| 2-2 Moonpath on the Bay | APPROVE | Yes | G20. F7 walls figure 1.43, near the 1.5 limit |
| 2-3 The Kraken's Sea | APPROVE | Yes | **G21** (a coin round (512, 118)); G17 (one peg in 37% of lost games) |
| 2-4 The Ferry Under Sail | APPROVE | Yes, still the set's strongest. The envelope is much softer | G18 residue (mauve side columns and a rose foot round a violet window) |
| 2-5 Twin Lanterns | APPROVE | Yes | G11 (the reflection decides losses), G18 (unchanged), G6 |
| **Set** | **APPROVE** | | |

## Status of round-4 findings

### G19 (Minor: 1-1 and 1-2 tied, then a 3.6 drop): RESOLVED for the tie; a Nit remains on the shape
- **The tie is gone.** Pooled, 1-1 is 30.80 and 1-2 is 28.70: a step of 2.10 ± 0.40. It is 2.28 on block 3 alone and 1.67 on 2593–5184.
- **How it was done.** The author eased 1-1 by about 1.9: (533, 398) is blue, and (220, 204) and (354, 331) became candidates in place of (533, 398) and (340, 300). I had suggested hardening 1-2 instead.
- **The opening.** Stage 1 now opens at 30.8, inside the band (30 + 1.8).
- **The tool** shipped as I asked: `stage` re-plays any step under 1.0 on 1728 more seeds. Its weakness is G20.
- **Residue (Nit).** 1-2 to 1-3 is now 3.37, the stage's largest step and larger than the finale gap (3.24). A finale should feel like the stage's biggest jump. It meets the 2.5 rule.
  - **Fix**, if touched again: harden 1-2 by about 0.7 with one blue sky star above the roofline. The steps would become about 2.8, 2.7, 1.5, 3.2.

### G11 (Minor: low oranges decide losses on 1-1 and 2-5): PARTLY RESOLVED; still Minor because of 2-5
**1-1: now a Nit.**
- (533, 398) is blue. (504, 415) is now the single top holdout:

  | Block | Share of lost games |
  |---|---|
  | Tuning | 36% |
  | 2593–5184 | 31% |
  | 5185–7776 | 31% |

- The ratio at y ≥ 400 is 3.56 (round 4: 3.2), still the set's highest. "Either of the top two" is 45%, but the second is now (608, 344), a mid-height shore moon, not a low one.
- **Paired variant `v01a`** (the other bridge moon as the candidate). (533, 398) is then left in 30–34% of lost games, and 1-1 eases to 31.54 against 30.80 on the same seeds.
  - So the bridge ring shields whichever lower moon is orange.
  - The spread rule requires a right-half candidate at y ≥ 400 (`author.py` line 523). On 1-1 only the ring's (504, 415) qualifies, since (533, 398) and (475, 398) are just above 400 and the road at (458, 437) is in the bucket's approach.
- **Accept it.** It is a fair shot at y 415, 15 above the cheap line, and about 11% of all games are lost with it left.
- **Fix, only if the ring is re-drawn:** open the ring's south-east gap so the low moons are not shielded. Do not loosen the spread rule.

**2-5: unchanged.**
- On block 3, 80% of lost games leave a reflected orange at y ≥ 430. That is 52% of the leftovers against 43% of the candidates.
- The top holdouts are the reflected heads at y 471 (16–20% each).
- The gate passes only because the difficulty is spread over five heads (each under 25%, ratio 1.2 under 1.5).
- The author's round-4 experiment still stands: about 6.8 per 48 of the finale's difficulty is this reflection.
- The coordinator has not yet ruled.
- **Fix as in round 4:** heads at 3 of 5 and blue stars in the sky lanes, or an explicit ruling that a finale may take its difficulty from a low subject.

### G18 (Minor: the quiet prints the layout's envelope): PARTLY RESOLVED; now a Nit
**Resolved:**
- **The walls.** I measured second-jewel pixels in the 50-unit wall strips, which are 15% of the area:

  | Board | Round 4 | Round 5 |
  |---|---|---|
  | 2-4 | 34% | **4%** |
  | 1-3 | not measured | **2%** |
  | 2-3 | 31% | **10%** |
  | 1-1 | 27% | **3%** |

- F7 now gates it (`second_at_walls_x` at most 1.5), with a self-test.
- 2-4's `regionQuiet` 0.3 at chroma 0.14 removes the saturated red sides. The dark-blue window is far softer.

**Residue (soft, about 0.03 chroma or 45° of hue over 25–40 units):**
- **2-4.** At y 300–450 the sides (x 110–235 and 610–685) are hue 320–330 at chroma about 0.068. The middle (x 260–585) is hue 280 at 0.095. The foot at y 540 is hue 27, chroma 0.085, from x 135 to 660. So a violet window still has mauve sides and a rose foot.
- **2-5.** Unchanged: rose inside the arch and at the walls, mauve round the lanterns, and the ghost U in the water.
- **1-2.** The dark bowl under the ledge rows is wider with `[40, 18]`.
- **1-1.** 79% of the second jewel lies more than 60 units from any piece (area 32%). It is the sea, so it reads as geography.
- **New and mild.** On 1-1, 2-4 and 2-5 the wall fade brings back the first jewel at full chroma in the last 30–35 units by each wall (1-1 at y 150: 0.105–0.13 at x 710–735 against 0.055 in the field). In the composite it reads as a blue rim light along the rail. It is fine.

**Fix (optional):** for 2-4, paint the rose into the sea with `paint` rather than a band; leave 2-5 to the UX supervisor.

### N-a (Nit: 1-5's F7 on both floors): UNCHANGED
60° apart, second share 0.158.

### N-b (Nit: protan minimums fell): RESOLVED

| Board | Protan figure, round 5 |
|---|---|
| 1-2 | min 0.126 |
| 1-4 | p10 0.118 |
| 2-3 | min 0.105 |

2-3 got there through G21's disc.

### G17, G16, G7, G6, G4: UNCHANGED (Nits)
- **G17.** 2-3's (558, 156) is in 37% of lost games on block 3.
- **G16.** Bottom thirds are still empty.
- **G7.** 1-1 still has the most stuck-rule games, at 4.9–5.4%.
- **G6.** 2-5's framing is 1.49%.
- **G4.** No stage-4 Ul'dah source list yet.

## New findings

### G20: Minor: stage 2's first step (2-1 to 2-2) is not resolved, and the new confirmation passes it by luck
**Evidence**
- Pooled over 6912 untuned games each: 2-1 is 27.35 and 2-2 is 26.76, a step of **0.60 ± 0.41**.
- By block:

  | Block | Step |
  |---|---|
  | Held-out | 0.81 |
  | 2593–5184 | 1.04 |
  | 5185–7776 | **0.02** |

- `mfl.py stage 2` re-played both on 2593–4320 (27.7 and 26.4) and passed the step at 1.05.
- **Why the confirmation does not confirm.** A second block of 1728 games gives about ±0.39 per level, ±0.55 per step. If the true step is 0.6, the pooled figure clears 0.5 about 57% of the time. It is a second roll of the dice. It removes false failures but does not establish the step.
- **This round's 2-1 change was net neutral**, as intended. The pooled figure was 27.57 in round 4 and is 27.35 now.
- **Paired `v06a`**, round 4's candidates on the new keystone geometry: 28.56 against 27.28 on tuning seeds, and 28.30 against 27.04 on block 3. That is +1.26, confirming the author's "about 1.2". The candidate swap fully bought back the wider keystone, which leaves 2-1 where round 4 had it, a hair above 2-2.

**Fix**
- **Levels.** Open the step to about 1.0 or more by design, on tuning seeds.
  - The preferred option is to ease 2-1 by about 0.6 with a half-measure: swap one mid-difficulty candidate (holdout share about 15%) for an easier upper one. The stage then opens at about 28.0, inside the band's 27 + 1.8.
  - `v06a` in full (+1.26) also works: steps of about 1.8, 1.2, 1.4, 4.7. But it opens stage 2 at 28.6, more than "about 27".
  - Hardening 2-2 instead would squeeze 2-2 to 2-3 (1.21) below 1.0.
- **Tool.** For a step under 1.0, play enough fresh games to resolve the rule, for example 5184 per level, ±0.32 per step. Or require pooled step minus one standard error to be at least 0.5. Add a note to the README that 0.5 is unresolvable at 3456 games.

### G21: Minor: 2-3's new `disc` term is a per-peg coin, and the print gate cannot see it
**Evidence**
- **The term.** `scenes/rhotano-wonders.json` adds `["disc", 512, 118, 34, -12]` to the green-teal region's mask. It is centred exactly on the candidate peg at (512, 118), the flock's lead bird. It was added for UX m6 (protan 0.086 there).
- **The report.** `report/base-08.json` gives a print worst of **0.0879 at (512, 118)**:
  - the highest of the ten levels (next: 1-1 0.058, 1-5 0.057, 2-5 0.054);
  - 11 times 2-3's p90 (0.0078);
  - 2.2 times the p90 limit (0.040);
  - more than twice the p90 of round 2's known-bad coin on 2-2 (0.036);
  - round 4's 2-3 worst was 0.061, at another peg.
- **On the cleared board** (2x crop, `gd5/img/c08disc.png`) it is a soft lavender disc about 90 units across on the green-teal chart. It sits just under the launcher, where the player looks on every shot. In play it is a halo round the lead bird; after the board clears, it is a coin with nothing in it.
- **Why the gate misses it.** `dress_print` judges the median and the 90th percentile over 67 samples. Up to 6 pegs can carry a full coin and pass, so a deliberate single coin is invisible to it. That breaks the coordinator's binding "quiet by low frequency, never per peg", in the recipe rather than in the quiet.

**Fix**
- **Level.** Remove the disc. Then do one of these:
  - make (512, 118) blue (F9 measures candidates only) and promote another bird. This also spreads G17: for example, make (558, 156) blue too and promote two outer V tips, then re-measure on tuning seeds;
  - or keep the green-teal off a band shaped to the flock (its x-range at y about 90–170) rather than a disc round one peg.
- **Pipeline.** Add a worst-peg clause to the print. Calibrate it between the six pilots' worst and 0.088; the ten levels' next highest is 0.058. Or add a recipe lint that refuses any `disc` mask term centred within the peg's radius plus about 10 units of a peg. Add a self-test: 2-3 with this disc must fail.

### N-c: Nit: `stage` re-plays the build copy, not the shipped level
- `cmd_stage` plays `paths.BUILD / "json" / f"{lid}.json"`.
- Today those files match `json/` byte for byte. A stale or hand-edited build copy, for example after a variant experiment saved under the level's own name, would be measured silently instead.
- The confirmation also has no self-test.
- **Fix:** play `json/<id>.json`, or refuse when the two differ.

## What works
- **The ramp is honest and sits in its bands.**
  - Three independent untuned blocks agree with the held-out figures to within noise (mean +0.2).
  - Stage 1 is 30.8, 28.7, 25.3, 23.9, 20.6. Stage 2 is 27.35, 26.8, 25.55, 24.1, 19.4.
  - Both finales clear their gap with room (3.2 and 4.7).
- **G19 was fixed with the tool I asked for.** The README now says to accept or reject variants on tuning seeds. The author's 1-1 figure (31.9 on tuning) reproduces exactly (31.94).
- **Pipeline hardening is real and self-tested:**
  - quietBlur pinned and refused below 40;
  - the print measure repaired, and it fails round 2's coin on three real boards;
  - exact mover cycles;
  - `NOTCH_MAX` 17 (2-1's keystones re-opened to about 19.7);
  - F7's wall clause.
- **The cleared boards are calmer.** The red walls on 1-3 and 2-4 are gone, 2-4's window is soft, and 1-4's new tone disc is invisible on the painting.
- **Variety, fullness and reads are unchanged from round 4:** ten distinct subjects. 2-4, 1-3 and 2-2 are the strongest.

## Unverified
- **The runtime.** Powers, the converter (it does not exist), `MoonfallRender` output, and the per-piece veil in play. The `tone`, rim-fill, poly, not-poly and inverted-product masks have no runtime form, so 1-4's tone discs and 1-5's dome house are untested in the game.
- **Human play.** Every difficulty figure is mfcheck's greedy player with ±1.5° aim error. Whether a 2.1 step reads as "harder" to a person is untested.
- **The pilots' worst-peg print values,** needed to set G21's limit. I did not extract them.
- **G20's half-measure swap.** I did not pick or measure the candidate. Only the full `v06a` was measured.

