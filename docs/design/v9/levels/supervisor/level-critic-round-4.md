# Level-design critic, levels round 4

6 October 2026. Worktree `agent-a570460c913ca1c79` at `b32a74e2`. I edited no repo files and did not run `mfl.py build`. `git status` is clean after my runs. The only writes went to my scratch directory and, through `mfl.py selftest`, to gitignored `build/` fixtures.

**Overall verdict: REVISE, on the pipeline only.** All ten level files are sound and I would ship them as they are. One pipeline Major blocks approval:
- **M1.** The re-worked print gate no longer catches round 2's per-peg coin on a shipped board. On 2-2, with round 2's quiet put back, the coins are plainly visible and the gate passes.
- No shipped level has a coin: every recipe uses the default `quietBlur` 40.
- But this gate is the guard for round 2's Major, and the README names it as the future converter's gate.
- The fix is small: pin `quietBlur` at 40, gate the converter by a direct comparison, and correct the README.

Status of round 3's findings:
- **Resolved:** N2 (in form), N5, N7.
- **Partly resolved:** N1, N3, N4 and N8, with residues in M1, N9 and N10.
- **Still open:** N6 (as acknowledged) and the L11 ceiling (Nit N11).

## Method

**Files and self-test**
- `mfl.py selftest`: all 102 cases pass. New cases:
  - 12.8 slot, chain-only deck, periods 10/10.1;
  - ring and wide-disc prints, the six pilots as known-good;
  - much-easier stage, swapped palette, the previous stage's last level.
- All ten `json/*.json` are byte-equal to `json.dumps(Board.level_json(), indent=2)` rebuilt from the layouts. All pass `mfcheck validate` and a fresh `Board.check()` (`levels2.py`).
- `build/json` is byte-equal to the committed files.
- `mfl.py stage 1` and `stage 2` exit 0.

**Ramp (`ramp4.py`)**
- `mfcheck play` on each file over three seed blocks:
  - tuning, seeds 1–864;
  - the build's held-out block, 865–2592 (`play … 1728 n 864`);
  - a fresh block nobody has used, 2593–4320.
- I confirmed the dll is newer than `Program.cs`, and that the offset changes the deal (seeds 0 and 864 give different games).
- I also played two 1-1 variants (`v11.py`).

**Print and quiet**
- `print4.py`, on every board:
  - the gate on the shipped dress, and with the grain rule off;
  - round 2's per-peg quiet put back (`quietBlur` 0, with the level's own widths and with `[22, 12]`);
  - synthetic hue coins (r + 24, OKLab chroma shift 0.01–0.05) added to the shipped dress.
- `grainwhy.py`: per-peg print against grain on 2-2.
- `gatefix.py`: two alternative statistics over the ten levels and the six pilots.
- `quietmap.py`: heat maps of the quiet's own change.
- `coinview.py`: the cleared, veil-free 2-2, shipped against round 2's quiet, viewed at 1x.

**Runtime parity.** I read `MoonfallSceneBuilder.Palette` (lines 360–413), `MoonfallClearance` and `MoonfallFilters.Blur` on main via `git show main:`.

**Pre-flight bypasses (`bypass5.py`, `e1.py`)**
- C1 again;
- E1: crossing slides of periods 10 and 10.04;
- notch slots of 12.8–20 between converging arc feet, each swept and tallied with `stuckcells.py`;
- the print gate's shapes on a flat sky and on skies with grain std 0.005–0.03.

**Engine**
- Every level swept at 0.25° and 0.05°, plus `reach` (`sweeps.py`).
- `mfl.py stuck base-04`, and round 3's 1-4 against round 4's.

**Visuals.** Composites at 1x for 1-1, 2-2, 1-3, 1-4, 1-5 and 2-5. Cleared quiet maps for 1-1, 1-2 and 2-2.

Scripts and outputs: `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/critic4/`

## Verdicts

| Level | Verdict | Notes |
|---|---|---|
| 1-1 Road to Horizon | APPROVE (N9) | (368, 427) is blue now. The new candidate (533, 398) is the second holdout (24%). Held 28.78, exactly on the band's tolerance edge; tied with 1-2 on fresh seeds |
| 1-2 Horizon by Night | APPROVE | Unchanged. Fresh 29.19 (held 28.22) |
| 1-3 The Cactuar | APPROVE | The star at (200, 180) reads as a star. Stuck 3/681. Held 25.06, fresh 25.00 |
| 1-4 The Gilded Dome | APPROVE | The stars at (320, 170) and (480, 140) are clear of the dome by 28.8. Stuck 12/681 (was 8); 5 rest on the dome's crest, as 4 of 8 did in round 3. New `tone` disc at (440, 478) (N2 residue) |
| 1-5 The Crystal's Call | APPROVE | The crystal's foot (440, 478) is blue; the west-sky star reads. Held 20.42, fresh 20.19; finale gap 3.3 |
| 2-1 Limsa Across the Water | APPROVE | Unchanged. Held 27.75, fresh 27.17 |
| 2-2 Moonpath on the Bay | APPROVE | N5 resolved: (538, 346) fills the lane; dead 0 at 0.05° (3401 aims). The shipped cleared board is clean |
| 2-3 The Kraken's Sea | APPROVE | Unchanged. Mover gaps ≥ 12.7 still, ≥ 14.1 mover to mover. (558, 156) left in 38% of lost games: a skill shot (Nit) |
| 2-4 The Ferry Under Sail | APPROVE | Rose region at chroma 0.18. Mover to mover ≥ 16.0 over the 63 s cycle |
| 2-5 Twin Lanterns | APPROVE | The U's foot and each ring's lowest moon are blue; two high stars added. 79% of lost games leave a low orange; it passes the ratio rule at 1.19×; the coordinator's call. Still pegs ≥ 14.2, mover to mover ≥ 17.2 |
| Pipeline and checkers | **REVISE** | **M1**; Minors N9, N10; Nits N6, N8, N11, N12 |

**Ramp per 48 games** (tuning block 1–864 / held-out 865–2592 / fresh 2593–4320; SE about 0.8 / 0.57 / 0.57):

| | 1-1 | 1-2 | 1-3 | 1-4 | 1-5 | 2-1 | 2-2 | 2-3 | 2-4 | 2-5 |
|---|---|---|---|---|---|---|---|---|---|---|
| tune | 29.94 | 27.78 | 24.61 | 23.83 | 20.33 | 27.39 | 26.61 | 23.67 | 22.28 | 19.94 |
| held | 28.78 | 28.22 | 25.06 | 23.67 | 20.42 | 27.75 | 26.58 | 25.33 | 24.17 | 19.17 |
| fresh | 28.81 | 29.19 | 25.00 | 23.72 | 20.19 | 27.17 | 26.44 | 25.33 | 24.11 | 20.08 |

- **The build's figures are real held-out figures.** My held-block re-run reproduces every report's `ramp_per_48` (rounded), and the reports record `ramp_first_seed` 865 over 1728 games.
- **The fresh block confirms them.** Fresh minus held averages +0.09; the largest gaps are 1-2 (+0.97) and 2-5 (+0.91).
- **The tuning bias is real.** The tuning block reads 1.7–1.9 harder than the others on 2-3 and 2-4.
- On fresh seeds every stage-2 step holds, and both finale gaps hold (3.5 and 4.0). The one reversal is 1-1 against 1-2 (N9).

**Sweeps:** dead 0 on all ten at both 0.25° and 0.05°. Stuck at most 12/681 (1-4); never-reached `[]` on all ten.

## Status of round-3 findings

| Finding | Status | Evidence |
|---|---|---|
| N1 1-1 lost on its two lowest oranges (Minor) | **Partly resolved**; residue in N9 | See below |
| N2 runtime format cannot express the quiet (Minor) | **Resolved in form**; residues | See below |
| N3 print gate blind shapes (Minor) | **Ring and wide disc resolved**; crescent open (accepted); new hole in M1 | See below |
| N4 movers checked only to 240 s (Minor) | **Resolved for C1**; residue N10 | C1 now faults "come within a ball", and is self-tested. Periods that round to the same 0.1 s escape (N10) |
| N5 2-2 0.16° dead lane (Nit) | **Resolved** | Peg at (538, 346); 0.05° sweep dead 0 on all ten |
| N6 lane-edge row at y 509 (Nit) | **Open** (acknowledged) | No level is affected |
| N7 self-test and tool gaps (Nit) | **Resolved** | See below |
| N8 ramp order and jewels (Nit) | **Partly resolved** | See below |
| L11 residue, notch ceiling | **Partly resolved**; Nit N11 | See below |
| L12 reads (art call) | Open | Unchanged |
| L13 rows read as fences (Nit) | Unchanged | — |

**N1: partly resolved, residue in N9**
- (368, 427) is blue.
- On the held-out block the holdouts are now (504, 415) at 29% (round 3: 32%) and the newly picked (533, 398) at 24%.
- **At y ≥ 395:** four candidates (14% of the deal) are 39% of the oranges left. 60% of lost games leave one (round 3: 61% at y ≥ 415), and 28% leave only these.
- The single-piece gate still starts at y 430. The reason given holds: the spread rule needs a right-half candidate in y 400–430, and on 1-1 that is only (504, 415).
- (533, 398) is not required by any rule.

**N2: resolved in form, residues**
- `dress.py` now computes `smooth(a, b, blur(min(dist, 96), quietBlur ≤ 40))`. Regions keep `1 − 0.6·near`, the jewel `1 − 0.5·near`.
- Main's `Palette` does the same: `Blur(ClearanceAtS, t.Blur·S)` then `Smooth(a0, a1, ·)`, then `v *= Scale`, `m *= Invert ? 1 − v : v`.
- `MoonfallFilters.Blur` is the same three-box Gaussian as the pipeline's blur.
- **Residues:**
  - the converter still does not exist;
  - `tone` grew (1-4's new disc), with no runtime counterpart;
  - `quietBlur` 0 reproduces round 2's coin exactly, and both the format and the dress accept it (see M1).

**N3: ring and wide disc resolved, crescent open, new hole**
- **On a flat sky** (`bypass5.py`), each of these now fails:
  - the 13–19 ring (0.023);
  - the disc 40 out (0.05);
  - a soft Gaussian wash, σ 30 (0.033).
- **The three-sector crescent** still passes (0.0). It is accepted as out of the dress's reach, and I agree: the quiet is isotropic.
- **The new grain rule opens a larger hole** (M1). With grain std 0.03 the 0.05 disc passes; with 0.02 the soft wash passes.

**N7: resolved**
- Chain-only deck case added (it filters on the chain fault).
- "Much easier" stage case added.
- Sweep rows print `{a,7:0.00}`.
- The ghost gate is documented as an in-play check. It is still vacuous on 2-3 (0 isolated pegs), which is acceptable now that the print gate carries the "what stays" duty.

**N8: partly resolved**
- The ramp is on 1728 held-out games (SE 0.57).
- 1-1 → 1-2 is +0.56 held and −0.38 fresh (N9).
- Jewels: 9 of 10 first jewels still lie in 247–298°.

**L11 residue: partly resolved, Nit N11**
- The pre-flight now faults 12.8- and 13.5-unit slots.
- Slots of 14.2, 15 and 16 pass and still trap balls at the slot (N11).

## New findings

### M1 (Major), pipeline: the print gate passes round 2's per-peg coin on 2-2, and its median no longer constrains anything

**Where.** `mflkit/readability.py` `dress_print`, `PRINT_TEXTURE` 1.2, `PRINT_P90` raised from 0.026 to 0.070; `README.md` step 9 ("Calibrated: … round 2's per-peg quiet measured median 0.012-0.095 on the flat boards and p90 0.07-0.10 on the textured ones").

**Evidence**

*The median is 0.0 on every board measured:* all ten shipped boards and all six pilots. Pegs the grain rule masks count as 0, and on every board they are more than half. Only the p90 clause can fail a board.

*Round 2's quiet put back on today's recipes* (`quietBlur` 0, `[22, 12]`), median / p90:

| Board | Result |
|---|---|
| 1-1 | 0.057 / 0.079: fails |
| 1-2 | 0.039 / 0.103: fails |
| 1-3 | 0.022 / 0.088: fails |
| 1-4 | 0 / 0.096: fails |
| 1-5 | 0.0995 / 0.138: fails |
| 2-1 | 0 / **0.0702**: fails by 0.0002 |
| **2-2** | **0 / 0.0467: passes** |
| 2-3 | 0.080 / 0.093: fails |
| 2-4 | 0.048 / 0.123: fails |
| 2-5 | 0.051 / 0.062: fails |

*What 2-2 looks like.* `img/coin07-full.png` sets the cleared, veil-free 2-2 at 1x, shipped beside round 2's quiet. Round 2's version shows a distinct blue disc round every star in the rose sky. The shipped board is clean.

*Why it passes* (`grainwhy.py`):
- 32 of 55 pegs, the textured water, are zeroed.
- The 23 sky pegs print 0.044–0.050, well above their grain × 1.2 (0.008–0.029), so the rule rightly keeps them.
- But they are under half the pegs, so the median is 0, and their p90 0.047 is under the loosened 0.070.

*Synthetic hue coins on the shipped dress:*
- at chroma shift 0.03, they pass on 1-3 and 2-2;
- at 0.02, they pass on 1-2, 1-3, 2-1, 2-2, 2-3, 2-4 and 2-5.

*The README's calibration sentence is false for 2-2,* and 2-1 fails by 0.0002.

*No simple statistic separates the cases* (`gatefix.py`):
- **Leaving masked pegs out** fails the shipped boards too. The blur-first wash prints 0.04–0.06 on outer pegs: 1-1's unmasked median is 0.044, 1-2's 0.060.
- **The share of pegs over 0.03** does not separate either. Pilot exp-p3 is at 23%, while round 2's quiet on 1-4 is at 22% and on 2-1 at 29%.

**Why Major.** No shipped level is affected. But this gate is the guard for round 2's Major (G1). The README makes it the converter's gate ("run the print check and F9 on … MoonfallRender output as its gate"). The one knob that makes a coin, `quietBlur`, is open in both the recipe and the runtime format. And round 3's gate (median 0.010, p90 0.026, no grain rule) caught this exact case on 2-2: median 0.025 without the grain rule.

**Fix.** The first three items resolve M1; the fourth can follow as a Minor.
1. **Pin `quietBlur`.** `dress.py`, or the recipe check, refuses `quietBlur` < 40. All ten recipes already use 40. The quiet is the dress's only per-peg term, so this makes the dress unable to print a coin.
2. **Gate the converter directly.** Compare `MoonfallRender` output with the pipeline's dress, pixel for pixel (ΔE p99 within a small tolerance), not through `dress_print`. The README claims the two formulas are identical.
3. **Correct the README's calibration text.**
4. **Repair `dress_print`, then add the known-bad case.**
   - Measure the masking grain at the print's own scale: the undressed painting's ring-against-ring contrast in the same bands and sectors. A 4-unit high-pass std is inflated by point stars and water ripples, and it does not mask a 60-unit hue disc.
   - Then add 2-2 with `quietBlur` 0 and `[22, 12]` as a real-board known-bad self-test. Under today's gate that case passes, so it can only be added once the gate catches it.

### N9 (Minor), 1-1 and 1-2 are not separated, and 1-1 sits exactly on the band's tolerance edge

**Evidence**
- **Held-out:** 28.78 against 28.22, a step of 0.56 against the 0.5 rule.
- **Fresh block:** 28.81 against **29.19**, reversed.
- **Pooled over the 3456 non-tuning games:** 28.80 against 28.71, a tie (SE of the difference about 0.57).
- **The band's edge.** `stagecheck`'s opening test is `28.8 < 30.0 − 2 × 0.6`. It passes on float equality (both sides are 28.8). Unrounded, the held-out figure is 28.78, just under the line.
- **The cause** is largely the newly picked (533, 398), left in 24% of lost games.

**Fix** (my variants, `v11.py`):

| Variant | Tune | Held | Fresh | (504, 415) left in lost games |
|---|---|---|---|---|
| v1: (533, 398) blue, (422, 270) orange | 31.50 | 30.14 | 30.67 | 35–38% |
| v2: (533, 398) blue, (152, 318) orange | 30.83 | 31.39 | 29.64 | 32–38% |

- v1's tuning figure is under the "much easier" line of 31.8.
- Either variant puts 1-1 back in its band and 1.5–2 above 1-2.
- **The trade-off.** (504, 415) is then left in more lost games. It sits above y 430, so the gate passes, and it is the spread rule's required right-half candidate. Lost games leaving an orange at y ≥ 395 fall from 60% to 51–58%.
- Tune the final pick on seeds 1–864, as the README says.

### N10 (Minor), pre-flight: movers whose periods round to the same 0.1 s are checked for one cycle only

**Where.** `author.Board.common_cycle` rounds each period to 0.1 s before taking the LCM.

**Evidence** (probe E1):
- Two crossing slides: (480, 150) → (640, 150) with period 10, and (560, 66) → (560, 156) with period 10.04.
- `common_cycle` returns 10 s, so the check samples 10 s, where the minimum gap is 15.3.
- The pre-flight and the loader pass it.
- Over real time the pair comes within a ball's width at **47 s** and overlaps at **177 s**.
- Any |Δperiod| < 0.05 escapes both the real-time path and the new phase torus.
- **No shipped level is affected.** Periods are 20 (2-3), 7 and 9 (2-4), and 5 (2-5).

**Fix**
- Take the LCM on exact periods (`Fraction(str(period))`), or use the torus whenever `period * 10` is not an integer.
- Add E1 to the self-test.

### N11 (Nit), notch ceiling 14 sits below the width that still traps

**Evidence.** Round 3's B2 geometry (converging arc feet), swept at 0.25°, with stuck shots tallied by rest cell:

| Slot | Pre-flight | Stuck first shots | Resting at the slot |
|---|---|---|---|
| 12.8 | faults | 2 | — |
| 13.5 | faults | 3 | — |
| 14.2 | passes | 7 | 6 |
| 15 | passes | 5 | 4 |
| 16 | passes | 5 | 3 |
| 17 | passes | 1 | — |
| 20 | passes | 0 | — |

- All of these stay well under the 5% gate.
- The nearest shipped slot is 2-1's at 18.8.

**Fix.** Raise `NOTCH_MAX` to 17, and add a 15-unit slot as a known-bad case.

### N12 (Nit), the held-out block was used for accept/reject decisions

**Evidence.**
- The round-4 context records two such decisions: 2-5's game-designer variant ("~26 held-out") and 2-3's swap ("28 held-out"). The README says the held-out block is never used for tuning.
- The fresh block shows no measurable bias from this: mean fresh minus held is +0.09.

**Fix.** Make such calls on seeds 1–864, or require a third block to confirm before a held-out figure decides anything.

### Residues kept as Nits

- **N6** (lane-edge row at y 509) is unchanged.
- **N8 jewels:** 9 of 10 first jewels still lie in 247–298°.
- **1-4 stuck:** 12/681, 5 of them on the dome's crest (pre-existing).
- **2-3:** (558, 156) is left in 38% of lost games, GD G17's skill shot.

## Unverified

- **The runtime's own render.** No converter exists. I checked the quiet's formula line by line against main, not a runtime render. `tone` (now including 1-4's disc), rim-fill masks and `poly` masks still have no runtime counterpart.
- **Visibility of the shipped wash.** On 1-1 and 1-2, the cleared boards show the quiet as large soft bands following the layout. That is the intended form and an art/UX call. 17–19% of their pegs measure 0.03–0.08 at the second band pair. I judged the cleared boards only at 1x on this display.
- **Play.** All play evidence is mfcheck's greedy player at seed-derived deals, plus first-shot sweeps. I did not log where the stuck rule fires in play (GD G7), nor real game durations (N10's 47 s assumes a game that long).
- **Art reads** (L12) remain the art supervisor's call.
