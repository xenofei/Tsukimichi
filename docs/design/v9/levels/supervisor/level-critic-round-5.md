# Level-design critic, levels round 5

6 October 2026. Worktree `agent-a570460c913ca1c79` at `a52c841c`. I edited no repo files and did not run `mfl.py build`. `git status` shows only two untracked files I did not create: `supervisor/game-designer-round-5.md` and `supervisor/ux-round-5.md`. My other writes went to:
- my scratch directory `.../scratchpad/critic5/`;
- the gitignored `build/scenes` cache;
- selftest fixtures under `build/`.

**Overall verdict: REVISE, on one level.**
- **M1 is resolved.** The repaired print gate fails round 2's coin on all ten real boards, at both the build's 2x→1x path and the self-test's 1x path. `quietBlur` is pinned, and the README's calibration text is now true.
- **New Major, M2:** 2-3 now carries a hand-placed per-peg disc. The new inverted disc term `["disc", 512, 118, 34, -12]` is centred on the candidate at (512, 118).
  - It leaves a plain lavender coin on the green chart when that peg clears, visible at 1x on the full board.
  - In play it puts a spotlight round that one peg.
  - It breaks the coordinator's binding rule ("quiet by low frequency, never per peg").
  - The print gate records it as the set's worst single print (0.0879) but cannot fail one peg.
  - The fix is small: remove the term and settle F9 there by layout.
- The pipeline has no Major left. Minors N13 and N14 cover the remaining ways round the pin and the gate. Nits are N15–N19.

## Method

**Self-test and files**
- `mfl.py selftest`: 109 cases, all ok (1 min 24 s). New cases that pass:
  - the 15-unit slot faults;
  - E1 (periods 10 and 10.04) faults;
  - the F7 wall strip fails;
  - round 2's coin on the real 2-2, 2-1 and 1-4 fails (2-1 and 1-4 ran: textures fetched);
  - a `quietBlur` below 40 is refused.
- `levels5.py` (all ten):
  - `json/*.json` are byte-equal to the layouts rebuilt;
  - the loader accepts every file, and a fresh `Board.check` passes;
  - `build/json` is byte-equal to the committed files.

**Stage checks**
- `mfl.py stage 1` exits 0.
- `mfl.py stage 2` exits 0. It re-plays 2-1 and 2-2 on the second block: 27.7 and 26.4, pooled 27.55 and 26.5.

**Ramp (`ramp5.py`)**
- `mfcheck play` on each file over four seed blocks:
  - tuning, seeds 1–864;
  - held-out, 865–2592;
  - the stage's new second block, 2593–4320;
  - a fourth block nobody has used, 4321–6048.
- The eight unchanged levels reproduce round 4's figures exactly on the first three blocks, so the runs are deterministic.

**Print, pin and coins**
- `print5.py`, on all ten boards:
  - the shipped dress;
  - round 2's coin through the build path (2x→1x) and the self-test path (1x);
  - a NaN `quietBlur`;
  - a wide quiet `[80, 50]` at the pinned blur;
  - a coin drawn without `unpinned`, through the `dist` mask term;
  - synthetic hue coins of 0.02 and 0.03;
  - the OKLab p99 between each variant and the shipped dress (the converter gate's statistic).
- `distcoin.py` and `wideview.py`: the cleared, veil-free 2-2 and 1-2 at 1x.
- `pilotworst.py`: the six pilots' worst single-peg prints.

**The new pieces**
- `disc23.py`: 2-3 with and without its disc term, measured and rendered.
- `tone14.py`: 1-4's tone discs, measured and rendered.
- `p99disc.py`: the converter-gate statistic on 2-3's disc term.
- `notch5.py`: brick-to-brick gaps at 0.1-unit sampling against the check's own, plus synthetic slots with odd and even sample counts.
- `cleared.py`: cleared boards for 1-1, 1-3, 1-5, 2-3, 2-4 and 2-5.

**Engine.** `sweeps.py` at 0.25° and 0.05° plus `reach`, on the two changed layouts (1-1, 2-1). Also `mfl.py stuck base-06`, `stuck base-01` and `dead base-01`.

**Runtime format.** I read `git show main:docs/design/v9/scene-recipe.md`, which defines:
- `disc` with `invert`;
- `scale` 0–1;
- `blur` 0–40;
- at most 6 terms per mask.

Scripts, outputs and images: `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/critic5/` (images in `img/`).

## Verdicts

| Level | Verdict | Notes |
|---|---|---|
| 1-1 Road to Horizon | APPROVE | N9 resolved: 30.6–30.7 on every non-tuning block, 1.4–2.5 above 1-2. (504, 415) is now left in 37% of lost games (Nit N19). Dead 0 at 0.05°; stuck 8/681 |
| 1-2 Horizon by Night | APPROVE | Quiet `[40, 18]`; F9 p10 0.137. The green water still glows in the lower corners on the cleared board (Nit N18) |
| 1-3 The Cactuar | APPROVE | Wall fades read clean on the cleared board |
| 1-4 The Gilded Dome | APPROVE | Two `tone` discs sit on or near pegs (see N14). On the dome's texture they read as shading, not coins |
| 1-5 The Crystal's Call | APPROVE | Finale gap 3.05, pooled over three blocks |
| 2-1 Limsa Across the Water | APPROVE | Keystones are 19.54 true (were 16.7). Stuck 7/681, none at the arches. Dead 0 at 0.05°. Pooled 27.66 |
| 2-2 Moonpath on the Bay | APPROVE | The weakest known-bad margin: round 2's coin fails it on the median alone (0.0106 against 0.006) |
| **2-3 The Kraken's Sea** | **REVISE (M2)** | A per-peg lavender disc round (512, 118) |
| 2-4 The Ferry Under Sail | APPROVE | `regionQuiet` 0.3; F9 p10 0.134. A hot rose band across the bucket's approach remains on the cleared board (art call, UX n6) |
| 2-5 Twin Lanterns | APPROVE | Unchanged in play. Ghost U acknowledged |
| Pipeline and checkers | APPROVE with Minors | M1 resolved. Minors N13, N14; Nits N15–N18 |

**Ramp per 48 games.** Blocks: tuning 1–864, held 865–2592, second 2593–4320, fourth 4321–6048. SE about 0.8 on tuning and 0.57 on each other block.

| | 1-1 | 1-2 | 1-3 | 1-4 | 1-5 | 2-1 | 2-2 | 2-3 | 2-4 | 2-5 |
|---|---|---|---|---|---|---|---|---|---|---|
| tune | 31.94 | 27.78 | 24.61 | 23.83 | 20.33 | 27.28 | 26.61 | 23.67 | 22.28 | 19.94 |
| held | 30.69 | 28.22 | 25.06 | 23.67 | 20.42 | 27.39 | 26.58 | 25.33 | 24.17 | 19.17 |
| second | 30.61 | 29.19 | 25.00 | 23.72 | 20.19 | 27.67 | 26.44 | 25.33 | 24.11 | 20.08 |
| fourth | 30.61 | 29.03 | 25.56 | 23.78 | 21.39 | 27.92 | 26.81 | 26.33 | 23.31 | 19.89 |
| pooled (3 non-tuning, SE ~0.33) | 30.64 | 28.81 | 25.21 | 23.72 | 20.67 | 27.66 | 26.61 | 25.66 | 23.86 | 19.71 |

**Pooled steps**

| | Steps | Finale gap |
|---|---|---|
| Stage 1 | 1.83, 3.60, 1.49 | 3.05 |
| Stage 2 | 1.05, 0.95, 1.80 | 4.15 |

- Both stages sit inside their bands: 30.6 down to 20.7 against 30–21, and 27.7 down to 19.7 against 27–20.
- On the fourth block alone, 2-2 → 2-3 is 0.48 and 1-4 → 1-5 is 2.39. Both are within noise (the SE of a difference is about 0.8), and the pooled figures hold.

**Sweeps**
- Dead 0 on all ten at 0.25° (reports). I re-checked the changed layouts at 0.05° as well: 1-1 and 2-1 are dead 0 over 3401 aims.
- Never-reached: none on any level.
- Stuck at most 12/681 (1-4).

## Status of round-4 findings

| Finding | Status | Evidence |
|---|---|---|
| M1: print gate passes round 2's coin (Major) | **RESOLVED**; residues in N13 and N17 | See below |
| N9: 1-1 and 1-2 tie (Minor) | **RESOLVED** | 1-1 is 30.69 / 30.61 / 30.61 against 1-2's 28.22 / 29.19 / 29.03: steps 2.47, 1.42, 1.58, pooled 1.83. (533, 398) is blue. The new candidates (220, 204) and (354, 331) pass the spread rule; preflight passes |
| N10: `common_cycle` rounding (Minor) | **RESOLVED** | `Fraction(str(period))`. E1 is in the self-test and faults "come within a ball of each other" |
| N11: notch ceiling (Nit) | **RESOLVED**; residue N15 | `NOTCH_MAX` 17, and the 15-unit slot is a self-test. Shipped true gaps: 2-1 19.54 / 18.81 / 19.54, 2-2 17.99 |
| N12: held-out block used for decisions (Nit) | **RESOLVED** | The README now says accept or reject on seeds 1–864. Round 5's calls (1-1's pick, 2-1's candidate) were made there. My untouched fourth block confirms every step (see above) |
| N6: lane-edge row at y 509 | UNCHANGED (acknowledged) | No level is affected |
| N8: jewels | UNCHANGED | 9 of 10 first jewels lie in 247–298° |

**M1 in detail**

*Round 2's coin on the real boards.* `[22, 12]`, blur 0, median / p90:

| Board | 2x→1x path | 1x path | Fails on |
|---|---|---|---|
| 2-2 | 0.0106 / 0.0373 | 0.0105 / 0.0361 | median |
| 2-1 | 0.0055 / 0.0477 | 0.0027 / 0.0452 | p90 |
| 1-4 | 0 / 0.0788 | 0 / 0.0745 | p90 |
| other seven | median 0.016–0.0915 | median 0.0127–0.0877 | median |

*The window the thresholds sit in*
- **Known-good:**
  - shipped boards reach at most 0.0024 median and 0.0324 p90;
  - the six pilots reach at most 0.0027 median and 0.0324 p90.
- **Known-bad:** the weakest are 2-2's median (0.0105) and 2-1's p90 (0.0452).
- **Thresholds:** 0.006 and 0.040 sit inside that window, with about a 13–19% margin on the p90 side.

*Synthetic 0.03 hue coins fail on 9 of 10;* 1-4 passes at 0 / 0.031. That matches the README.

*The README's calibration figures are true* as written. Its converter gate is now a direct comparison, which is the right shape; its statistic is loose (N17).

*Residues*
- The pin can be got round (N13), and the README's "the dress itself cannot draw that coin any more" is false.
- Hand-placed per-peg terms are not gated (M2, N14).

## New findings

### M2 (Major), 2-3: a hand-placed per-peg coin round the candidate at (512, 118)

**Where.** `docs/design/v9/levels/scenes/rhotano-wonders.json`, the green-teal region's mask: `["disc", 512, 118, 34, -12]`. It was added this round for UX m6 (F9 at that place). In `dress.mask_of` a negative feather gives `smooth(22, 46, d)`, so the region is cut away within 22–46 units of the peg and the lapis jewel shows through.

**Evidence**
- **The peg.** `json/base-08.json` has a peg at exactly (512, 118): r 7, a candidate.
- **The picture.** `img/disc23-zoom.png` (dressed scene, shipped | without the term) and `img/c89.png` (the cleared, veil-free board at 1x) both show a distinct lavender disc on the green chart.
  - On the cleared board, the disc stays where the peg was.
  - In play (`img/disc23-play.png`) it is a spotlight round that one candidate, unlike any other peg on the board.
- **The gate's figures**

  | | Median / p90 | Worst single peg |
  |---|---|---|
  | Shipped | 0 / 0.0078 | 0.0879 at (512, 118), the largest of all ten levels (next: 1-1's 0.058) |
  | Without the term | 0 / 0.0072 | 0.0217 |

- **The gate cannot fail it.** One peg moves neither the median nor the p90, and a numeric single-peg cap cannot be calibrated: the approved pilots have single-peg worsts of 0.0855 (base-p2) and 0.19 (exp-p3).
- **The rule it breaks.** The coordinator's binding decision: "quiet by low frequency, never per peg". This is the round-2 G1 class of defect: a print that stays when the peg clears, placed by hand on one peg.
- **What the term buys.** No gate needs it. The F9 rule is p10 ≥ 0.12 or ≥ 0.101. Without the term, p10 was 0.109 in UX round 4, so it passes. The term only serves UX's per-place target (≥ 0.10 at every place).

**Fix**
- Remove the disc term.
- Settle F9 at (512, 118) through the layout:
  - make (512, 118) blue and give the candidate to a place off the green-teal top band;
  - or move the peg off the band.
- Re-tune on seeds 1–864. The pooled 2-2 → 2-3 step is 0.95 now: keep it at 0.5 or more.
- If a colour change is still wanted there, use a geography-scale mask that follows the chart's band, about 100 units or more across, never centred on a piece.
- In the README, state the rule as "no mask term centred on a piece at the peg's scale" (see N14 for the check).

### N13 (Minor), pipeline: the `quietBlur` pin can be got round, and the README overstates it

**Where.** `mflkit/dress.py`:
- the pin is `sigma = min(float(j.get("quietBlur", QUIET_BLUR)), QUIET_BLUR_MAX)` followed by `if sigma < QUIET_BLUR_MIN`;
- the `["dist", a, b]` mask term (documented in the module docstring) is drawn unblurred;
- README step 9 says "The dress itself cannot draw that coin any more".

**Evidence (`print5.py`, `distcoin.py`)**
- **A NaN `quietBlur` is not refused.**
  - `json.loads` accepts `NaN`.
  - `min(nan, 40)` is NaN, and `nan < 40` is False, so the refusal is skipped.
  - `nan > 0.3` is also False, so the clearance is not blurred at all.
  - With `[22, 12]` it draws round 2's coin exactly: 2-2 at 0.0106 / 0.0373, identical to the unpinned case.
  - The gate still fails it on all ten boards.
- **The `dist` term is a per-peg route the pin never sees.** I added a keepMask `[["dist", 22, 12], ["k", 1]]` and multiplied each region by `["dist", 12, 22]`; nothing was refused.
  - On 2-2 the print gate **passes** it at 0.0052 / 0.0128.
  - Faint blue discs are visible round the sky stars at 1.5x (`img/d1zoom.png`, lower half).
  - On the other nine boards the `dist` coin fails the gate.
- **A wide quiet `[80, 50]` at blur 40 is not a hole.** It quietens the whole board evenly, with no halos (`img/wide-both.png`).

**Fix**
- Refuse NaN by testing `if not (sigma >= QUIET_BLUR_MIN)`.
- Refuse `dist` terms in jewel regions and keepMasks, or route them through the same clearance blurred at σ ≥ 40.
- Add both to the self-test.
- Correct the README's sentence.

### N14 (Minor), pipeline: per-peg terms the print gate cannot see

**Where.** `readability.dress_print` measures the dress against the undressed scene. `tone` is graded into that undressed scene (`build._scene_cached` keys on `tone`), so any `tone` term is invisible to it by construction. No check looks at where hand-placed mask terms sit relative to the pieces.

**Evidence**
- **2-3.** The M2 disc passes the gate.
- **1-4.** Two `tone` discs sit on pegs:
  - (310, 418, r 50, f 30) is centred exactly on the candidate at (310, 418);
  - (440, 478, r 60, f 30) is 4.5 from the peg at (436, 476).
- **What they do** (`tone14.py`, cleared board):
  - at 5–12 units from the peg, ΔOKLab 0.035 and 0.041 (L −0.034 and −0.040);
  - at 45–70 units, 0.005 and 0.012.
- **Why only Minor.** On 1-4's textured dome they read as shading of the dome, not as coins (`img/tone14-zoom.png`).

**Fix.**
- Add a preflight rule refusing any dress mask term (region, keep or `tone`) that is centred within about r + 20 of a piece with a radius under about 60. Allow listed exceptions with a reason, where the term follows a painted feature.
- Optionally measure `tone`'s change against the ungraded painting.

### N15 (Nit), pipeline: the notch check can read a slot up to about 0.9 units wide per brick end

**Where.** `author.py` measures notches with `bs[i][::2]` against `bs[k][::2]` (1-unit samples, every other one kept). A brick's end sample (k = n) is dropped whenever n is odd.

**Evidence** (`notch5.py`): the same true 14.90 slot reads 14.90 with an even sample count and 15.81 with an odd one.
- So the effective ceiling is about 16.1 for one skipped end, or about 15.2 when two facing ends are skipped, against the 17 intended.
- In my round-4 B2 sweep, slots of 15–16 still trapped 3–4 balls.
- Every shipped gap reads its true value: 2-1 19.54 / 18.81 / 19.54, 2-2 17.99.

**Fix.** Sample at 1 unit and always include both ends. Add an odd-count slot of about 16 to the self-test.

### N16 (Nit), pipeline: `stage`'s second block plays `build/json`, and its pooling is untested

**Where.** `mfl.py cmd_stage` plays `paths.BUILD / "json" / f"{lid}.json"`.

**Evidence**
- `build/` is gitignored. On a fresh checkout `stage 2` cannot play the second block until a build has run, and after `ease` or a variant build it plays whatever `build/json` last held.
- Today `build/json` is byte-equal to `json/`.
- The pooling has no self-test case.

**Fix.** Play `JSON_OUT / f"{lid}.json"`, the shipped file the report describes, and add a self-test case for a step under 1.0.

### N17 (Nit), README: the converter gate's p99 is too loose for local terms

**Evidence**
- Dropping 2-3's disc term entirely moves the p99 by only 0.0176, with 1.03% of pixels over 0.015 (`p99disc.py`).
- So a converter that mis-scales or mis-feathers a local term at peg scale would pass "p99 about 0.015".
- UX round 4 measured the formula's residue at a maximum of 0.014, p99.9 0.008.

**Fix.** Gate the converter on the maximum (or p99.9) at no more than about 0.02.

### N18 (Nit), pipeline: two unchecked edges of the new colour pieces

- **`regionQuiet` has no range check.** It maps onto the runtime's `scale`, which is limited to 0–1. A value over 1 drives the region mask negative in `dress.py`.
- **F7's wall check averages the full height** (`second_at_walls_x`). A leak confined to the lower corners passes:
  - 1-2's green water glows in its lower-left and lower-right corners on the cleared board (`img/wide-both.png`, lower left), yet reads 0.93;
  - 2-2 sits at 1.43, against the 1.5 limit.
  - This is an art call for GD and UX; I note it only as a limit of the check.

### N19 (Nit), the ramp: residues of N9 and N12

- **1-1's holdout.** (504, 415) is now left in **37%** of held-out lost games (29% in round 4), the largest single holdout in stage 1. 44% of lost games leave an orange at y ≥ 400. It sits above the 430 line, so the cheap-difficulty gate passes; it is GD's call.
- **1-1's tuning figure.** 1-1 was accepted on a tuning figure of 31.94, which is above the "much easier" line of 31.8. The bands are defined on held-out figures, and on this level the tuning block reads 1.3 easier.
  - The README's "accept on the tuning seeds" should say that band thresholds apply to the held-out figure only, or allow for the block's bias.

### Residues kept as Nits

- N6 (the lane-edge row) and N8 (the jewels) are unchanged.
- 1-4 stuck 12/681.
- 2-3: (558, 156) is left in 38% of lost games (GD G17's skill shot).
- 2-5: 79% of lost games leave a low orange (the coordinator's call, within the ratio rule).

## Unverified

- **The runtime's own render.** No converter exists. None of these has a runtime form:
  - `tone` (including 1-4's peg-centred discs);
  - rim-fill masks;
  - `poly` and `not-poly` terms;
  - feature-`near` terms;
  - 1-5's inverted-product keepMask.

  2-3's negative-feather disc should map onto `disc` with `invert` (the smoothstep is symmetric), but I have not checked the runtime's feather convention.
- **Visibility.** I judged visibility at 1x and 1.5x on this display only. The `dist` coin on 2-2 is faint; M2's disc is plain at 1x.
- **Play.** All play evidence is mfcheck's greedy player at seed-derived deals, plus first-shot sweeps. Stuck positions in play (GD G7) are not logged.
- **Untested routes.** Only the two layouts that changed (1-1, 2-1) were swept at 0.05°; the other eight use round 4's sweeps. I did not test regionQuiet values outside 0–1.

