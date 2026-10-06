# Game designer, levels round 6

6 October 2026. Worktree `agent-a570460c913ca1c79` at `c0b5ac71`; round 5 was reviewed at `a52c841c`. I edited no tracked files and did not run `build`; `git status` is clean after my runs. Side effects, all in gitignored places: `mfl.py selftest` rewrote its fixture boards under `build/`, and my variant JSONs are in `docs/design/v9/levels/build/json/gd6/`.

**Overall verdict: APPROVE.** I found no Major.

- **Both round-5 Minors are resolved.**
  - G20: 2-1 to 2-2 is now 1.09 ± 0.29 over 13,824 games per level (round 5: 0.60 ± 0.41).
  - G21: the disc on 2-3 is gone. 2-3's worst print is 0.0217 (was 0.088). G17 went with it: 2-3's top holdout is now 20% (was 37%).
- **The owner's 2-5 answer is met.**
  - Lost games that leave an orange at y ≥ 430: 54% (round 5: about 80%).
  - Gap below 2-4: 3.12 ± 0.29.
  - 2-5 is still stage 2's hardest level.
- **Two new Minors, both in the pipeline. Neither is in the shipped ten.**
  - G22: `stage` confirms only steps under 1.0 on the held-out block. Steps of 1.0 or more, and the finale gap, are judged on 1,728 games (±0.8).
  - G23: `dress.lint` misses round 5's coin when it is moved 20 units off its peg.
- **Three new Nits.**
  - G24: 2-5's difficulty leans on one shielded corner star, and its "outer moons" are mislabelled.
  - G25: 2-5 is now level with 1-5.
  - N-d: a stale comment in `base-08.py`.

I would ship these ten levels and this pipeline as they are.

## Method

**Read:**
- `round6-context.md` and `round2-context.md`, and my round-5 report;
- `git diff a52c841c c0b5ac71`:
  - layouts base-02, 06, 08 and 10;
  - `rhotano-wonders.json` and `uldah-gilded-dome.json`;
  - in `mflkit/`: `dress.py` (`lint`), `stagecheck.py` (`se48`, `step_resolved`, `CONFIRM_GAMES` 5184) and `author.py` (N15);
  - `mfl.py` (`cmd_stage` now plays `paths.JSON_OUT`) and the README;
- all ten `report/*.json`.

**Ran:**
- `mfl.py selftest`: exit 0, 136 ok. The new cases fire:
  - the lint cases: NaN quietBlur, `dist` in a region and in a keep mask, regionQuiet 1.5, and 2-3 with a centred, keep or tone disc;
  - syn05 on the real 2-2 and 1-4;
  - the 16-unit slots at 44.7, 45.2 and 45.7;
  - a step of 0.8 on one block and 0.6 over 6,912 games refused; 0.95 over 6,912 passed.
- `mfl.py stage 1`: exit 0, clean, no confirmation needed.
- `mfl.py stage 2`: exit 0. It confirmed 2-3 and 2-4 from `json/`: 25.55 and 24.12 pooled over 6,912 games.

**Ramp (my own runs on the shipped `json/` files):**
- Three blocks per level:
  - the held-out block, seeds 865–2592 (1,728 games). It reproduces every report figure to within rounding.
  - the confirmation block, seeds 2593–7776 (5,184 games). It reproduces `stage 2`'s figures exactly.
  - a fresh block, seeds 30001–36912 (6,912 games), used by no one else.
- Pooled, that is **13,824 games per level (±0.20 per 48, ±0.29 per step)**. Raw mfcheck output is kept; holdouts are counted by piece home.

**Paired variants:**
- Played on the tuning block (seeds 20001–23456) and on fresh seeds 40001–43456, 6,912 games each.
- 2-2 with one more blue sky star (v1 at (390, 236), v2 at (400, 180)).
- 2-5:
  - v1: (690, 200) blue and (660, 170) a candidate;
  - v2: the blue (140, 150) moved to (140, 170), mirroring the right-hand shield;
  - v3: the reflected arch's candidates at k 0 and 6 (y 456) instead of k 1 and 5 (y 486).

**Lint probe:** round 5's disc `["disc", x, y, 34, -12]` placed 0–26 units off isolated 2-3 pegs, run through `dress.lint`.

**Images:**
- composites 1-2, 1-4, 2-1, 2-2, 2-3 and 2-5;
- dressed 1-4 and 2-5;
- 2x crops of 2-3's top band round (512, 118) (`gd6/img/d08top.png`) and of 1-4's new tone disc (`gd6/img/d04low.png`), compared with round 5's `gd5/img/c08disc.png`.

**Scratch:** `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/gd6/`:
- `ramp/{held,conf,fresh}/` and `var/{tune,fresh}/`;
- `ana.py`, `hold.py`, `ramp.py`, `mkvar*.py`, `lintx*.py`, `crop.py`;
- `stage1.log`, `stage2.log`, `selftest.log`.

### Key figures

| Level | Pieces | Cands | Held-out | 2593–7776 | 30001–36912 | **Pooled, 13,824 games** | Lost games leaving an orange at y ≥ 430 / y ≥ 400 | Top holdouts (pooled, share of lost games) |
|---|---|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 82 | 28 | 30.69 | 30.83 | 30.90 | **30.85** | 0 / 0.42 | (504, 415) 32%, (608, 344) 17% |
| 1-2 Horizon by Night | 68 | 28 | 27.19 | 27.57 | 27.88 | **27.68** | 0 / 0.55 | (678, 413) 18%, (606, 416) 18% |
| 1-3 The Cactuar | 70 | 28 | 25.06 | 25.43 | 24.97 | **25.15** | 0 / 0.40 | (387, 415) 22%, (567, 404) 22% |
| 1-4 The Gilded Dome | 70 | 28 | 23.67 | 23.92 | 23.97 | **23.91** | 0 / 0.42 | (556, 420) 21%, (517, 386) 19% |
| 1-5 The Crystal's Call | 71 | 28 | 20.42 | 20.69 | 20.80 | **20.71** | 0 / 0.62 | (519, 336) 23%, (361, 336) 22% |
| 2-1 Limsa Across the Water | 74 | 28 | 27.86 | 28.22 | 28.04 | **28.09** | 0 / 0.38 | (330, 400) 20%, (296, 420) 16% |
| 2-2 Moonpath on the Bay | 82 | 28 | 26.58 | 26.81 | 27.24 | **27.00** | 0 / 0.53 | (548, 424) 20%, (682, 382) 18% |
| 2-3 The Kraken's Sea | 78 | 28 | 24.78 | 25.81 | 25.51 | **25.53** | 0.18 / 0.60 | (676, 336) 20%, (564, 446) 18% |
| 2-4 The Ferry Under Sail | 77 | 28 | 24.17 | 24.09 | 23.71 | **23.91** | 0 / 0.31 | (463, 402) 19%, (319, 399) 17% |
| 2-5 Twin Lanterns | 74 | 28 | 20.44 | 20.56 | 21.06 | **20.79** | **0.54** / 0.54 | **(690, 200) 25%**, (622, 262) 16%, (464, 486) 15% |

**Steps, pooled (±0.29):**

| Stage | Steps | Finale gap | Held-out alone | Fresh block alone |
|---|---|---|---|---|
| Stage 1 | 3.17, 2.53, 1.24 | 3.20 | 3.50, 2.14, 1.39, 3.25 | 3.02, 2.92, 0.99, 3.17 |
| Stage 2 | **1.09**, 1.47, 1.62 | 3.12 | 1.28, 1.81, 0.61, 3.72 | **0.81**, 1.73, 1.80, 2.65 |

- Every step resolves at one standard error: the smallest, 1.09 − 0.29, is 0.80, above 0.5.
- Both finales clear 2.5 by more than one standard error.
- The held-out block reported honestly on the levels as a whole: fresh minus held-out averages +0.27. Individual levels are off by up to ±0.7, for example 2-3 at 24.78 held-out against 25.53 pooled.

## Coordinator's decisions: met or not

| Decision | Status | Evidence |
|---|---|---|
| Stage 1 from about 30 down to 21 | Met | 30.85 down to 20.71 |
| Stage 2 from about 27 down to 20 | Met, at the easy edge | 28.09 down to 20.79. The opener is 1.1 above 27, inside `stagecheck`'s 3 × 0.6 "much easier" tolerance |
| Each level at least 0.5 harder than the one before | **Met and resolved** (G20 fixed) | Smallest step 1.09 ± 0.29 (2-1 to 2-2) |
| Each finale the hardest in its stage, 2.5 or more below its 4th level | Met | 3.20 and 3.12 ± 0.29 |
| **Owner, 2-5: fewer oranges on the low reflected heads** | Met | Heads are candidates at k 2 and 3 only (y 438); six sky stars carry the rest. Low candidates are 6 of 28 (21%) |
| **Owner, 2-5: difficulty from the whole board** | Met, with a caveat (G24) | Holdouts are spread: one at 25%, then 13–16% across heads, lanterns, stars and the arch. One corner star carries about 2.2 per 48 |
| **Owner, 2-5: still the stage's hardest, gap 2.5 or more** | Met | 20.79 against 23.91: 3.12 ± 0.29 |
| **Owner, 2-5: low-orange share of lost games well under round 5's ~80%** | Met | 54% on every block (held-out 0.55, confirmation 0.54, fresh 0.54). At y ≥ 400 it is 54%, in the set's normal range: 2-2 53%, 1-2 55%, 2-3 60%, 1-5 62% |
| No cheap difficulty | Met | 2-5's low share of leftovers is 0.279 against a 0.214 deal (1.30 times, limit 1.5); no low piece is left in 25% or more of lost games |
| Ghost discs: quiet by low frequency, never per peg | **Met** (G21 fixed); guard hole in G23 | No mask term is centred on a piece; print worsts are 0.022–0.058 |
| Fullness, framing, dead first shots, painting rules | Unchanged and passing | Pieces 82/68/70/70/71 and 74/82/78/77/74; no_hit 0 on all ten; game paintings 2 of 5 in each stage |

## Verdicts

| Level | Verdict | Reads | Open findings |
|---|---|---|---|
| 1-1 Road to Horizon | APPROVE | Yes | G11 residue: (504, 415) is left in 32% of lost games (accepted Nit); G7 (5.2% of games see the stuck rule) |
| 1-2 Horizon by Night | APPROVE | Yes. The new blue star at (360, 200) sits in open sky | G18 residue (Nit) |
| 1-3 The Cactuar | APPROVE | Yes | — |
| 1-4 The Gilded Dome | APPROVE | Yes. The new broad tone `["disc", 390, 455, 100, 40]` shows no edge on the painting (2x crop) | — |
| 1-5 The Crystal's Call | APPROVE | Yes | N-a (F7 60°, 0.158) |
| 2-1 Limsa Across the Water | APPROVE | As before. The deck's candidates moved one peg east | Opens stage 2 at the band's easy edge (28.09) |
| 2-2 Moonpath on the Bay | APPROVE | Yes | — |
| 2-3 The Kraken's Sea | APPROVE | Yes. The flock reads by its silhouettes; no coin at (512, 118) | N-d (stale comment) |
| 2-4 The Ferry Under Sail | APPROVE | Yes, still the set's strongest | G18 residue (Nit) |
| 2-5 Twin Lanterns | APPROVE | Yes | G24, G25, G18 and G6 (Nits) |
| **Set** | **APPROVE** | | G22 and G23 are pipeline Minors |

## Status of round-5 findings

### G20 (Minor: 2-1 to 2-2 not resolved): RESOLVED
**Levels.**
- 2-1 eased by 0.74 pooled: 28.09 against round 5's 27.35. (506, 387) is now blue and (578, 385) a candidate.
- The step is 1.09 ± 0.29 over 13,824 games per level.
- By block: held-out 1.28, confirmation 1.41, fresh 0.81. The fresh-alone figure shows why a single block cannot settle this.

**Tool.**
- `stagecheck.step_resolved` (step minus one standard error, at least 0.5) is correct.
- `se48` is the binomial standard error on the win rate: 0.58 at 1,728 games and 0.29 at 6,912 for a mid-range ramp, matching my figures.
- The test is now real: a measured pooled step under about 0.91 fails.
- What is still missing is in G22.

**Paired check.** I also measured hardening 2-2 instead. One blue sky star hardens 2-2 by 1.3–1.75, too much, because it would close 2-2 to 2-3. Easing 2-1 was the right lever.

### G21 (Minor: 2-3's per-peg disc): RESOLVED
- The term is gone from `rhotano-wonders.json`. The lead bird (512, 118) and the right wing's (558, 156) are blue. The 2x crop shows clean green-teal chart round the flock.
- Print on 2-3: median 0, p90 0.0072, worst 0.0217 at (564, 446). Was 0.0879.
- F9 on 2-3: p10 0.117, minimum 0.11 at (133, 185). It passes by the 0.101 clause, like 2-5 (0.115) and 1-4 (0.118).
- The pipeline fix I asked for exists: `dress.lint`, with self-tests for 2-3's centred, keep and tone discs. It has a hole (G23).

### G17 (Nit: 2-3's skill shot (558, 156) in 37% of lost games): RESOLVED
- 2-3's top holdouts are now (676, 336) 20%, (564, 446) 18%, (564, 362) 18%, (700, 420) 18% and (331, 418) 17%. That is the set's flattest spread.
- Lost games leaving an orange at y ≥ 430 are 18%, all from (564, 446), under every limit.

### G11 (Minor: low oranges decide losses on 1-1 and 2-5): RESOLVED for 2-5; the 1-1 Nit is unchanged
- **2-5**: see the decisions table.
- **What carries 2-5's difficulty, measured (paired, 6,912 games each):**

  | Variant | Change | 2-5 ramp |
  |---|---|---|
  | Shipped | — | 20.39 |
  | v1 | Corner star (690, 200) made blue, its shield (660, 170) a candidate | 22.63 (+2.24) |
  | v3 | Reflected arch's candidates at k 0 and 6 (y 456) instead of k 1 and 5 (y 486) | 21.92 (+1.53) |

  So about 2.2 per 48 sits in one corner star and about 1.5 in the two deepest arch moons. See G24.
- **1-1**: (504, 415) is in 32% of lost games (37% held-out). Unchanged and accepted in round 5: a fair shot at y 415, forced by the spread rule.

### G19 residue (Nit: 1-2 to 1-3 was stage 1's largest step): RESOLVED
- The blue star (360, 200) hardened 1-2 by 1.0: 27.68 against round 5's 28.70.
- Stage 1's steps are now 3.17, 2.53, 1.24 and a 3.20 finale. The finale is no longer out-jumped.
- The opener's step (3.17) now equals it. That is a fair shape for a warm-up board.

### N-c (Nit: `stage` re-played the build copy): RESOLVED
`cmd_stage` plays `paths.JSON_OUT` (`docs/design/v9/levels/json`). Its stage-2 figures match my own runs of `json/` exactly.

### G18 (Nit: the quiet prints the layout's envelope): UNCHANGED
- The dressed 2-5 still shows the mauve field inside the layout's envelope and rose at the walls.
- 2-4 and 1-2 are as in round 5.
- This is UX's call.

### N-a, G16, G7, G6, G4: UNCHANGED
- N-a: 1-5's F7 sits at 60° and 0.158.
- G16: bottom thirds are still blue-only on most boards.
- G7: 1-1's stuck rule fires in 5.2% of games; 1.2% of first shots.
- G6: 2-5's framing is 1.49%.
- G4: still no stage-4 Ul'dah source list.

## New findings

### G22: Minor: `stage` confirms only steps under 1.0, so most steps and every finale gap are still judged on one 1,728-game block
**Evidence**
- `cmd_stage` plays the confirmation block only when the held-out step is under `CONFIRM_STEP` (1.0). Steps from 1.0 up, and the finale-gap rule, are judged on the held-out 1,728 games alone, at ±0.81 per step.
  - A true step of exactly 0.5 reads 1.0 or more on that block 27% of the time (z = 0.62), and is then passed unconfirmed. A true 0.3 passes 16% of the time.
- This round shows the spread. The two stage-2 transitions that matter were not confirmed:

  | Transition | Held-out | Fresh block alone | Pooled, 13,824 games |
  |---|---|---|---|
  | 2-1 to 2-2 | 1.28 | 0.81 | 1.09 |
  | 2-4 to 2-5 (finale gap) | 3.72 | 2.65 | 3.12 |

  The shipped ramp is fine (pooled above), but the tool did not establish it. The README's "the step must hold by one standard error over all 6912 games" reads as if it does.
- Cost of the fix: 432 games took 9 s here, so the 5,184-game confirmation is about 2 minutes per level, or about 2 minutes for a stage in parallel.

**Fix**
- Play every level of the stage on the 5,184 confirmation seeds and judge every step and the finale gap on the pooled 6,912 games with `step_resolved`. For the finale, require gap minus one standard error to be at least 2.5.
- Self-test: a held-out step of 1.2 whose confirmation pools to 0.7 must fail.

### G23: Minor: `dress.lint` passes round 5's coin when its centre is moved 20 units off the peg
**Evidence**
- The rule refuses a peg-scale `disc` (reach under 100) only when its centre is within 12 units of a piece's edge.
- My `gd6/lintx2.py` placed round 5's term `["disc", x, y, 34, -12]` on 2-3's region mask at offsets from two isolated pegs, (300, 140) and (331, 418):

  | Offset | Lint |
  |---|---|
  | 0 or 15 units | Refused |
  | 20, 22 or 25 units | Passes |

  At every passing offset the peg still lies wholly inside the disc's 34-unit core, which makes an off-centre coin round it.
- The README says the guard "covers the quiet, `dist` and peg-scale `disc` terms, which are every per-peg route the recipes have". That is not true for an offset disc, and an author placing a disc on a painted feature next to a peg would make exactly this.

**Fix**
- Refuse a `disc` of reach r + |f| under 100 whenever any piece sample lies within that reach: nearest piece edge less than r + |f|. Movers and bricks are already sampled by `_piece_points`.
- All ten as shipped pass this stricter rule (I measured it):
  - 1-4's tone has reach 140, so it is exempt;
  - 2-2's moon disc reaches 80, and its nearest piece edge is 110.3;
  - 2-5's lantern keep discs reach 16, and their nearest piece edge is 19.4.
- Self-test: round 5's disc moved 22 units off (300, 140) must be refused.

### G24: Nit: 2-5's top holdout is one shielded corner star carrying about 2.2 per 48, and its twin is easy; the "outer moons" are not the outer moons
**Evidence**
- **The corner star.**
  - (690, 200) is left in 24.7% of lost games on the fresh block (25–26% held-out).
  - Its mirror (110, 200) is left in 3.4%. The blue star at (660, 170) sits on the right-hand approach; the left has (140, 150), off the line.
  - Paired v1 (that star blue, its shield (660, 170) a candidate): 2-5 eases by **2.24** (20.39 to 22.63). That would drop the finale gap under 2.5.
  - Paired v2 (the left-hand shield mirrored, (140, 150) moved to (140, 170)): the difficulty moves to (110, 200) at 24–25%, while (690, 200) falls to 10%. The ramp is unchanged, 20.70 against 20.39. So mirroring the shield does not split the load between the twins.
- **The label.**
  - The layout comment and the README say the reflected arch is a candidate "at its two outer moons" (k 1 and 5). Those sit at **y 486**, the lowest candidates in the whole set; the next lowest is 2-3's (564, 446).
  - The outermost pair, k 0 and 6, is at y 456. Paired v3 with those as the candidates eases 2-5 by 1.53.

**Fix**
- No layout change is needed: the star is high on the board, a fair bank off the wall, and under every limit.
- Record in `base-10.py` that the finale gap rests on (690, 200) and its shield, so no later "fix" removes it unknowingly.
- Correct the wording to "the U's second pair (y 486)", or move to k 0 and 6 and re-tune.

### G25: Nit: stage 2's finale is no harder than stage 1's
**Evidence**
- 2-5 is 20.79 ± 0.20 and 1-5 is 20.71 ± 0.20.
- The bands, 21 for stage 1 and 20 for stage 2, intend stage 2 to end about 1 harder. The owner's G11 change eased 2-5 by about 1.35, from 19.44 in round 5.
- It is inside "about 20", so the decision is met.

**Fix (optional):** if 2-5 is touched again, harden it by about 0.5–0.8 away from the low rows and the corner star, then re-check on the tuning block. For scale, one blue star hardened 1-2 by 1.0 and 2-2 by 1.3–1.75.

### N-d: Nit: stale comment in `layouts/base-08.py`
- The comment says "the open sea's (331, 418) and (160, 406) take their places".
- The code makes only (331, 418) a candidate: `orange=(u, v) == (331, 418)`. (160, 406) is blue, and the candidate count of 28 confirms it.
- Fix the comment.

## What works
- **The ramp is right and now resolved.**
  - Stage 1: 30.85, 27.68, 25.15, 23.91, 20.71.
  - Stage 2: 28.09, 27.00, 25.53, 23.91, 20.79.
  - Every step is at least 1.09 ± 0.29, and both finales clear 3.1.
  - Three seed blocks agree, and the held-out block reproduces the reports exactly.
- **The owner's 2-5 answer was carried out with care.** The low share fell from about 80% to 54%. The finale stays clearly hardest, and holdouts are spread across the board's subjects.
- **2-3's fix was done the right way, by layout, not mask.** It also cleared G17. Choosing the final pick on a second 3,456-game tuning block (20001–23456) is sound practice, and the README records it.
- **Pipeline hardening is real and self-tested:**
  - `dress.lint` refuses the quiet, `dist` and centred-disc routes;
  - the hue clause on the print check;
  - N15's full-sample notches;
  - `stage` plays the shipped files and uses a correct one-standard-error test.
- **The cleared boards are calm.** 2-3's top band and 1-4's terrace show no coin.

## Unverified
- **The runtime.** The converter does not exist. `tone`, rim-fill, poly and not-poly masks, and 1-5's inverted keep mask have no runtime form; `MoonfallRender` output is untested.
- **Human play.** Every figure is mfcheck's greedy player with ±1.5° aim error. Whether 2-5's corner star feels fair or merely fiddly to a person is untested, and so is whether a 1.1 step reads as "harder".
- **My variants were not run through pre-flight.** I added or moved pegs straight in the JSON. The engine's loader accepted every variant I report; it rejected one placement, (340, 130), as below the launcher, and I dropped it. The variants are for measurement only, not for shipping.

