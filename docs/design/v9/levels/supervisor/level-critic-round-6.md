# Level-design critic, levels round 6

6 October 2026. Worktree `agent-a570460c913ca1c79` at `c0b5ac71`. I edited no repo files, made no commits and did not run `mfl.py build`. `git status` is clean after my runs. My writes went to:
- my scratch directory `.../scratchpad/critic6/` (scripts, outputs, `img/`, and `r5rep/`, which holds round 5's reports extracted with `git archive`);
- the gitignored `build/scenes` cache, written by `build._scene_cached`.

**Overall verdict: APPROVE.** I would ship these ten levels and this pipeline as they are. There is no Major.
- **M2 is resolved.** The disc is gone from `rhotano-wonders.json`, and the lead bird (512, 118) is blue. 2-3's worst single-peg print fell from 0.0879 to 0.0217, and the composite shows no coin.
- **The owner's 2-5 answer is met on my fresh seeds:**
  - 2-5 is the stage's hardest, at 20.93 pooled;
  - its gap below 2-4 is 2.87 ± 0.36;
  - 54% of lost games leave a low orange (round 5: 79%);
  - the low-orange ratio is 1.33, under the 1.5 limit.
- **Four new Minors:**
  - **N20:** `dress.lint` is a denylist of two term kinds. I redrew M2's coin through it four other ways (plus a disc just outside its threshold), and the print check passes them all. The README's "every per-peg route" sentence is false.
  - **N21:** 2-5's top holdout (690, 200) is shadowed by the blue star (660, 170). It is never the first touch of a fresh shot; its mirror at (110, 200) is.
  - **N22:** `stage` resolves only steps under 1.0, and never resolves the finale gap. The 2-1 → 2-2 step and 2-5's gap are both marginal on fresh seeds.
- **Two Nits:**
  - **N23:** the hue clause is median-only, so a coin round the candidates alone passes on three boards.
  - **N24:** a wrong layout comment on 2-3; `dead` and `stuck` still read `build/json`.

## Method

**Self-test and stage checks**
- `mfl.py selftest`: 135 cases, all ok, none skipped (game textures present). These include:
  - the new lint cases;
  - syn05 on 2-2 and 1-4;
  - the N15 slots;
  - the three step-resolution cases.
- `mfl.py stage 1`: exit 0.
- `mfl.py stage 2`: exit 0. It re-played 2-3 and 2-4 on 5184 fresh seeds: pooled 25.55 and 24.12 over 6912 games each. It did not re-play 2-1 and 2-2, whose held-out step is 1.3.

**Files (`levels6.py`, all ten)**
- `json/` is byte-equal to the layouts rebuilt.
- The loader accepts every file, and a fresh pre-flight passes.
- `build/json` is byte-equal to `json/`.
- Pieces and candidates:

  | Level | 1-1 | 1-2 | 1-3 | 1-4 | 1-5 | 2-1 | 2-2 | 2-3 | 2-4 | 2-5 |
  |---|---|---|---|---|---|---|---|---|---|---|
  | Pieces | 82 | 68 | 70 | 70 | 71 | 74 | 82 | 78 | 77 | 74 |
  | Candidates | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 |

**Ramp (`ramp6.py`, `ramp6a.py`)**
- `mfcheck play` on every shipped file over five blocks:
  - tuning, seeds 1–864;
  - held-out, 865–2592;
  - the second tuning block, 20001–23456;
  - two blocks nobody has used: freshA 30001–33456 and freshB 33457–36912.
- Pooled figures use held-out plus both fresh blocks: 8640 games per level.
- Holdouts per candidate are in `hold6.py`.

**Sweeps (`sweeps.py`)**
- The four changed layouts (1-2, 2-1, 2-3, 2-5), at 0.25° and 0.05°, plus `reach`.
- `direct6.py`: 681 traces (0.25°) on 2-5, counting which first shots touch the mirror pairs, and which touch them first.

**Dress attacks**
- `bypass6.py`: eight per-peg variants on the real 2-3 at (512, 118) and the real 2-2 at (450, 200). Each is drawn by `dress()` as the build calls it (no `unpinned`), at 2x and down to 1x, then measured by the print check. Images: `img/bypass-base-08-2x.png` and `img/bypass-base-07-2x.png`.
- `masklint6.py`: a render-based calibration. Every hand-written mask and glow on the ten shipped recipes is drawn at 1x and scored for how much it singles out each peg (the 8-sector median of the mask 5–12 units past the peg's edge against 20–36 units).
- `hue6.py`: hue coins on all ten shipped boards (all pegs, candidates only, 45% of pegs, weaker amplitudes).

**Reports and images**
- `repcmp.py`: round 5 against round 6 reports.
- Composites viewed at 1x: 2-3 and 2-5 whole; 1-4 cropped round its new tone.

## Verdicts

| Level | Verdict | Notes |
|---|---|---|
| 1-1 Road to Horizon | APPROVE | Unchanged. 30.86 pooled. (504, 415) is left in 33% of lost games and is the sole orange left in 15% (GD's Nit, shielded by the bridge ring) |
| 1-2 Horizon by Night | APPROVE | The blue star (360, 200) hardens 1-2 by about 1.1 per 48 (round-5 pooled 28.81, now 27.74). Steps 3.12 / 2.76; dead 0 at 0.05° |
| 1-3 The Cactuar | APPROVE | Unchanged |
| 1-4 The Gilded Dome | APPROVE | The broad tone `["disc", 390, 455, 100, 40]` reads as shadow over the lower city, not a coin (`img/c04-tone.png`). F6 0.219 |
| 1-5 The Crystal's Call | APPROVE | Finale gap 3.19 ± 0.36 pooled |
| 2-1 Limsa Across the Water | APPROVE | Deck swap is in place. 28.01 pooled. Step to 2-2 is marginal (N22) |
| 2-2 Moonpath on the Bay | APPROVE | Unchanged |
| 2-3 The Kraken's Sea | APPROVE | M2 resolved. F9 p10 0.117 (passes by the 0.101 clause). The blue star (300, 140) is the board's one isolated peg, ghost 0.038 |
| 2-4 The Ferry Under Sail | APPROVE | Unchanged |
| 2-5 Twin Lanterns | APPROVE | Meets the owner's answer. The shadowed corner star (N21) is GD's call |
| Pipeline and checkers | APPROVE with Minors | N13 and N15–N19 resolved; N14 partly. New: N20, N22 (Minor); N23, N24 (Nit) |

**Ramp per 48 games** (SE about 0.8 on tuning, 0.57 held-out, 0.4 per 3456-game block, 0.26 pooled)

| | 1-1 | 1-2 | 1-3 | 1-4 | 1-5 | 2-1 | 2-2 | 2-3 | 2-4 | 2-5 |
|---|---|---|---|---|---|---|---|---|---|---|
| report (held) | 30.7 | 27.2 | 25.1 | 23.7 | 20.4 | 27.9 | 26.6 | 24.8 | 24.2 | 20.4 |
| tune 1–864 | 31.94 | 27.33 | 24.61 | 23.83 | 20.33 | 27.94 | 26.61 | 24.89 | 22.28 | 21.44 |
| held 865–2592 | 30.69 | 27.19 | 25.06 | 23.67 | 20.42 | 27.86 | 26.58 | 24.78 | 24.17 | 20.44 |
| tune2 20001–23456 | 30.68 | 28.07 | 25.81 | 23.99 | 20.21 | 27.33 | 27.00 | 25.31 | 23.89 | 20.19 |
| freshA 30001–33456 | 30.71 | 28.01 | 24.64 | 23.64 | 20.53 | 28.28 | 27.60 | 26.10 | 23.81 | 20.99 |
| freshB 33457–36912 | 31.10 | 27.75 | 25.29 | 24.31 | 21.07 | 27.81 | 26.88 | 24.92 | 23.61 | 21.12 |
| **pooled (8640)** | **30.86** | **27.74** | **24.98** | **23.91** | **20.72** | **28.01** | **27.11** | **25.36** | **23.80** | **20.93** |

**Pooled steps**

| | Steps | Finale gap |
|---|---|---|
| Stage 1 | 3.12, 2.76, 1.07 (± 0.36 each) | 3.19 |
| Stage 2 | 0.90, 1.74, 1.56 (± 0.36 each) | 2.87 |

- Both stages sit inside their bands: 30.9 down to 20.7, and 28.0 down to 20.9. 2-1 is within 1.2 of the band's 27.
- Single-block minima:
  - 2-1 → 2-2: 0.68 on freshA;
  - 2-4 → 2-5: 2.49 on freshB.
- The reported figures reproduce exactly on the held-out block, so the runs are deterministic. 2-3 (25.3) and 2-5 (20.2) reproduce on tune2.

**Sweeps.**
- Changed layouts at 0.05° (3401 aims): dead 0 on 1-2, 2-1, 2-3 and 2-5.
- Stuck at 0.05°: 0.7%, 0.8%, 0.9% and 0.5%.
- Reach: never-reached none.
- Unchanged levels, from the reports: dead 0, stuck at most 12/681 (1-4), unreached none.

## Status of round-5 findings

| Finding | Status | Evidence |
|---|---|---|
| M2: 2-3's per-peg disc (Major) | **RESOLVED** | Term removed. (512, 118) and (558, 156) are blue in `json/base-08.json`. Print worst 0.0217 at (564, 446), was 0.0879. The 1x composite shows no lavender coin. F9 p10 0.117, min 0.11 at (133, 185). The control in `bypass6.py` (M2's disc put back) is refused by lint on 2-3 and 2-2 |
| N13: pin bypasses (Minor) | **RESOLVED** for NaN and `dist`; the README overclaim recurs as N20 | `lint` refuses: NaN (`not (qb >= 40)`), `dist` in a region or keep, regionQuiet 1.5. All are self-tested on real recipes |
| N14: per-peg terms the gate cannot see (Minor) | **PARTLY** | Peg-scale discs (region, keep and tone) are now refused. 1-4's two peg-centred tone discs became one broad tone. Glows, `poly`/`not-poly`, `near`, x/y boxes and disc products are still unguarded (N20) |
| N15: notch end samples (Nit) | **RESOLVED** | `author.py` now uses every 1-unit sample. `brick_samples` includes both ends (k = 0..n). Self-test slots between bricks 44.7/45.2/45.7 long all fault |
| N16: `stage` played build/json (Nit) | **RESOLVED**; residue in N24 | It plays `JSON_OUT`. Three resolution cases are in the self-test |
| N17: converter gate statistic (Nit) | **RESOLVED** | README: maximum or p99.9 ≤ about 0.02 |
| N18: regionQuiet range, F7 averaging (Nit) | **RESOLVED** | Range refused. `second_at_walls_worst_window` is reported, not gated: 6.43 on 1-2 at y 341 (the lit water). Art call stays with GD/UX |
| N19: ramp residues (Nit) | **RESOLVED** for the README | Bands and steps now apply to held-out figures only. 1-1's (504, 415) is unchanged at 33% of lost games (GD's Nit) |
| N6, N8 | UNCHANGED (acknowledged) | — |

## New findings

### N20 (Minor), pipeline: `dress.lint` refuses two term kinds; M2's coin can be redrawn through five others

**Where**
- `mflkit/dress.py` `lint`: only checks `dist`, and `disc` centred within 12 units of a piece's edge with reach under 100.
- `README.md:121`: "the guard covers the quiet, `dist` and peg-scale `disc` terms, which are every per-peg route the recipes have".

**Evidence (`bypass6.txt`, `masklint6.txt`)**

Every variant below passes `lint`, is drawn by `dress()` without `unpinned`, and passes the print check. In each case the board's median and p90 do not move (2-3: 0/0.0072–0.0077, hue 0.0057; 2-2: 0/0.0039–0.0044, hue 0).

| Route | Variant | 2-3 at (512, 118) | 2-2 at (450, 200) |
|---|---|---|---|
| `not-poly` | octagon r 34, feather 24 in the green region | Lilac disc, as M2. Own print 0.048; mask singling 0.51 | Plain blue disc. Singling 0.51 |
| Keep box | `["x", x-30, x-18], ["x", x+30, x+18], ["y", …]` | Lilac square. Own print 0.038; singling 0.92 | Faint |
| Keep `near` | a one-point feature, band 16–34 | Lilac disc. Own print 0.040; singling 0.94 | Faint |
| Disc just outside the threshold | `["disc", 470.1, 200, 34, -12]` (centre 12.1 from the peg's edge) | (refused here only because a nearby bird was within 12) | Lint passes; plain blue disc. Own print 0.016; singling 0.44 |
| Glow | r 30, k 0.08 on the peg (glows have no clearance rule at all) | Faint | Grey haze. Singling 0.49–0.50 |
| Tone box | mul 0.8 | Dark square (singling 0.92); `tone` is invisible to the print check by construction | Dark square, own 0.031 |
| Disc lens | four discs of reach 102, centred 95 off the peg | A magenta lens under the bird. Each term's reach ≥ 100 exempts it; a product of large discs can be made arbitrarily small | Same |

Images: `img/bypass-base-08-2x.png` and `img/bypass-base-07-2x.png`. In both, the order is shipped, then the variants in the table's order.

**Why a render-based cap is not the fix.** Shipped painting-driven masks already single pegs out heavily:
- 1-1's rim-fill land: 0.45;
- 1-3's cactuar keep poly: 0.53;
- 2-3's luma region: 0.35.

So a cap on the drawn mask cannot separate a feature edge from a coin at the 0.44–0.64 level.

**Why only Minor.** No shipped recipe uses any of these routes at a piece:
- 1-5's dome-house keep box (x 112–240, y 464–499) holds the peg (160, 470) and the dome brick, but it follows the painted house and spans 128 units;
- 2-2's moon disc is 110 units from the nearest peg;
- 2-5's lantern-window discs stay 19.4 units from the ring pegs.

**Fix (shape-based, like the current lint).** Judge the positional part of each mask (`disc`, `x`, `y`, `poly`, `not-poly`, `near`, and their products; ignore `luma` and `mask`) by its support:
- draw only the positional terms at 1x;
- refuse when the support (mask > 0.5, or its complement for an inverted term) has a bounding extent under 100 units and comes within the piece's radius of a piece;
- replace "centre within 12 of the edge" with "footprint r + |f| reaches the piece";
- give glows the same rule (r < 100 near a piece);
- apply all of this to tone masks too.

All ten shipped recipes pass this (1-3's cactuar poly and 1-5's 128-unit box exceed 100; 2-5's discs do not reach a piece). Add the five variants above to the self-test, and correct README line 121.

### N21 (Minor), 2-5: the finale's top holdout is a corner star shadowed by a blue one

**Where.** `layouts/base-10.py`:
- the candidate star (690, 200);
- the blue star (660, 170) up-left of it, from the first star loop.

The right-hand star field is (660, 120), (660, 170), (680, 300). The left-hand one is (140, 150), (130, 300). They are not mirrors, although `SKY` is symmetric.

**Evidence**
- **Holdouts** (`hold6.txt`, 8640 games, 4872 lost):

  | Star | Left in lost games | Sole orange left |
  |---|---|---|
  | (690, 200) | 25.1% (top) | 5.8% |
  | Mirror (110, 200) | 3.3% (last of all 28 candidates) | 0.3% |

- **Fresh shots** (`direct6.txt`, 0.25° sweep):

  | Peg | Shots that touch it | Shots that touch it first |
  |---|---|---|
  | (690, 200) | 41 of 681 | 0 |
  | (110, 200) | 66 | 15 |
  | Blue (660, 170) | 76 | 16 |

  (690, 200) is reachable only after a bounce: the blue star sits on the launcher's line to it.
- **Composite** (`img/c10.png`): the orange sits behind the blue star against the right wall.
- **Scale.** 25% is within the set's range (1-1's shielded bridge moon is 33%), so the gate's limits are not broken.
- **Why it matters.** The owner asked that the finale's difficulty come "from the whole board". Its single most decisive orange is now a hidden corner star, created by an asymmetry the design did not intend.

**Fix** (GD's call)
- Mirror the star field, for example move (660, 170) to (640, 150), or put a blue at (160, 170) on the left;
- or give the candidate to (660, 170) and make (690, 200) blue.
- Then re-tune on the tuning blocks and keep the gap of 2.5 or more.

### N22 (Minor), pipeline: `stage` resolves steps under 1.0 only, and never the finale gap; both stage-2 margins are thin

**Where**
- `mfl.py cmd_stage` re-plays only `ra - rb < CONFIRM_STEP` (1.0).
- `stagecheck.faults` checks `FINALE_GAP` on point values.

**Evidence**
- **The cut-off.** A step read at 1.0 on the held-out block alone (SE of the step 0.8) is accepted. A step of 0.99 needs 6912 games each and step − SE ≥ 0.5. Read the same way, a held-out-only step needs 1.3.
- **2-1 → 2-2.** Held-out 1.28, so it is not re-played. On my 6912 fresh games (freshA + freshB) it is 0.80 ± 0.40, which **fails** `step_resolved`. Pooled with the held-out block (8640 games) it is 0.90 ± 0.36, which passes by 0.04.
- **2-4 → 2-5 gap.** Held-out 3.72 (the context reports 3.8). On 6912 fresh games: 2.65 ± 0.40. Pooled over 8640: 2.87 ± 0.36. FreshB alone: 2.49.
- **Against the decisions.** Both point estimates meet the coordinator's decisions (step ≥ 0.5, gap ≥ 2.5), so this is not a Major. But the stage's opening step, which the coordinator asked to be opened "by design", is held by about 0.4 above its floor, and the reported gap overstates the true one by about 1.

**Fix**
- Apply `step_resolved` to every step, or set CONFIRM_STEP to about 1.3.
- Apply the same rule to the finale gap (gap − SE ≥ 2.5, re-played on 5184 more seeds when needed).
- Report the pooled figures.
- Opening 2-1 → 2-2 by about another 0.3 would clear both rules.

### N23 (Nit), print check: the hue clause is median-only, so a coin round the candidates passes

**Evidence (`hue6.txt`).** A syn05-strength hue coin (a/b +0.0354 each, OKLab 0.05) round only the candidate pegs (28 of 66–82) passes every clause on three boards:

| Board | Median / p90 / hue median |
|---|---|
| 1-1 | 0.0023 / 0.0344 / 0.0187 |
| 2-2 | 0.002 / 0.0371 / 0.007 |
| 2-3 | 0.0034 / 0.0271 / 0.0205 |

It fails on the other seven. A coin that marks candidates is the worst kind, since it tells the player where oranges may fall. A weaker all-peg coin (a/b 0.018 each) passes on 1-4, 2-2 and 2-3, consistent with the README's syn03 note. No dress primitive draws such a coin today, so the check is only a backstop here.

**Fix.** Add a hue p90 clause, or a median over the candidates alone, calibrated on the pilots and the ten. Record the candidate-coin case in the README's "cannot see" list.

### N24 (Nit), housekeeping

- **2-3's layout comment** (`layouts/base-08.py`, the flock block) says "(331, 418) and (160, 406) take their places". (160, 406) is blue in `json/base-08.json`; only (331, 418) became a candidate.
- **`dead` and `stuck`** (`mfl.py`) still play `paths.BUILD / "json"`. Today it is byte-equal to `json/`, but after `ease` or a variant build these commands play that file, not the shipped one. Use `JSON_OUT`, as `stage` now does.
- **`second_at_walls_worst_window`** reports the window's top `(y0 + 82) // 2`, not its centre. Name it as such.

## Unverified

- **Runtime render.** No converter exists, so none of these has a runtime form:
  - `tone` (including 1-4's broad tone);
  - rim-fill masks;
  - `poly` and `not-poly`;
  - `near`;
  - 1-5's inverted-product keep;
  - 2-5's keep discs.
- **Visibility.** I judged it at 1x and 2x crops on this display. The bypass variants are drawn on scratch copies only.
- **Play.** All play evidence is mfcheck's greedy player on seed-derived deals, plus first-shot traces. Human play and stuck positions in play (GD G7) are not logged.
- **Sweeps.** Only the four changed layouts were swept at 0.05°; the other six rely on their reports (0.25°).
- **Hue calibration.** I did not calibrate the hue p90 clause I propose in N23.

Scripts, outputs and images: `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/critic6/` (`selftest.txt`, `stage1.txt`, `stage2.txt`, `ramp6a.txt`, `hold6*.txt`, `sweeps.txt`, `direct6.txt`, `bypass6.txt`, `masklint6.txt`, `hue6.txt`, `repcmp.txt`, `img/`).
