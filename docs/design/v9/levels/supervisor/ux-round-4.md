# UX/UI specialist, levels round 4

**Reviewed:** the ten levels base-01 to base-10 at `b32a74e2`, in worktree `agent-a570460c913ca1c79`.
- **Read:** `round4-context.md`; my round-3 report and the other two round-3 reports; the README diff; `dress.py` (the quiet); `readability.py` (the reworked `dress_print` and its self-tests); the scene and layout diffs since `35cd98b5`; the reports; the composites and the gitignored build outputs.
- **Read on main, via `git show`:** `MoonfallSceneBuilder.Palette`, `MoonfallClearance`, `MoonfallFilters.Blur`, `MoonfallColor.Smooth` and `MoonfallGrade.Jewel`.

**OVERALL: APPROVE.** I would ship these ten as they are.
- **G3 is resolved.** `dress.py`'s quiet is now main's `near` term exactly. At 1x my independent port of the C# matches the pipeline bit for bit.
- **m3 is resolved.** 1-5's protan figures: p10 0.123, minimum 0.112.
- **No Major remains.** Two new Minors need fixing before stage 3, or before any rebuild of these ten:
  - **m5:** the reworked print gate now passes round 2's coin defect on two real boards, 2-1 and 2-2. Its grain normalisation zeroes the median on all ten boards.
  - **m6:** the blur-first quiet lowered F9 on 1-2, 1-4, 2-3 and 2-5. Each still meets the rule, but four single places are now under 0.10.

## Method

**Ran (read-only):**
- `mfl.py selftest`: all ok. It reports the six pilots as passing with median 0 and p90 between 0 and 0.0663.
- I did not run `build`. `git status` is clean.
- Scripts are in `scratchpad/ux4/`:
  - `measure3.py`: my round-3 measure, unchanged. It writes `ux4_measure.json`.
  - `rt4.py`: a fresh numpy port of main's clearance, `ToPlane`, three-box blur, `Smooth`, the `near` term with invert and scale, and the palette's `where`, written from the C#. It feeds that `near` into `dress()` and diffs the result against the pipeline at 1x and 2x.
  - `neardiff.py`; `print4.py`; `fool4.py`; `gatefix.py`; `brickprint.py`; `snr_nq.py`; `variant_view.py`; `cvdcrop.py`; `blobdiff.py`.

**F6.**
- Faces are measured on the composite. The ring is the 90th percentile of luma 2–9 units out; glints use the 99th percentile.
- I covered every place a kind can be dealt, with movers at 24 points, at 1x and at 0.8x (Lanczos).

**Colour vision.**
- Machado 2009 at severity 1, protan and deutan, plus normal vision.
- Measure: the a/b distance from each kind's core to the ground 12–20 px round it.
- Covered every candidate, movers at 24 points, and orange-able bricks in 7 windows along each.

**Prints and halos.**
- **dE/texture** on the no-veil cleared boards (the runtime's cleared board), with and without the quiet, against the pilots.
- **The gate on the real boards.** I re-dressed each level with the shipped recipe but `quietBlur 0` (a literal per-peg coin), and again with round 2's `[22, 12]` and no blur. Each gets the gate's verdict, my dE/texture, and a direct look.
- **Synthetic cases** against the gate: 15 shapes on a flat sky, and coins on grainy ground.

**Viewed:** all ten no-veil cleared boards (`ux4/nv_all.png`); round 3 against round 4 for 2-4, 2-5 and 1-1 (`ux4/r3r4_nv.png`); 2x crops of 1-5's aurora, 1-3's dunes, 2-5's sky, 1-4's sky and lower dome; protan and deutan crops at every new low F9 place.

## Verdicts

Notes on the columns:
- **Print gate:** as shipped (median / p90).
- **dE/texture:** on the no-veil cleared board, isolated pegs, median / p90, with round 3 in brackets. Target: median ≤ 1.2 and p90 ≤ 2.0. The pilots measure 0.24–1.11 median.

| Level | F6 worst, pipeline / mine | Protan orange p10, pipeline / mine; min (mine) | Deutan min | Print gate | dE/texture, no-veil | Verdict |
|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 0.290 / 0.357 | 0.132 / 0.132; 0.125 | 0.133 | 0 / 0.043 | 0.78, n 1 (0.27) | APPROVE |
| 1-2 Horizon by Night | 0.318 / 0.364 | 0.126 / 0.126; **0.085** at (678, 413) | 0.105 | 0 / 0.059 | 0.31 / 0.94 (0.35 / 0.76) | APPROVE (m6) |
| 1-3 The Cactuar | 0.285 / 0.330 | 0.119 / 0.119; 0.099 (mouth) | 0.116 | 0 / 0 | 0.32 / 0.97 (0.33 / 0.52) | APPROVE (n3) |
| 1-4 The Gilded Dome | **0.202** / 0.248 | 0.110 / 0.110; 0.097 at (230, 150) | **0.078** (230, 150) | 0 / 0 | 0.69 / 1.50 (0.66 / 1.49) | APPROVE (m6, n1) |
| 1-5 The Crystal's Call | 0.368 / 0.396 | 0.123 / 0.123; 0.112 | 0.131 | 0 / 0.018 | 1.10 / 1.52 (1.00 / 1.39) | APPROVE |
| 2-1 Limsa Across the Water | 0.318 / 0.359 | 0.140 / 0.140; 0.134 | 0.148 | 0 / 0 | 0.55 / 1.03 (0.44 / 0.95) | APPROVE |
| 2-2 Moonpath on the Bay | 0.332 / 0.369 | 0.131 / 0.132; 0.130 (crests included) | 0.136 | 0 / 0 | 0.42 / 0.84 (0.51 / 0.68) | APPROVE |
| 2-3 The Kraken's Sea | 0.310 / 0.351 | 0.109 / 0.119; **0.086** at (512, 118) | 0.108 | 0 / 0 | 1.34 / 3.37* (1.01 / 3.36) | APPROVE (m6, n8) |
| 2-4 The Ferry Under Sail | 0.285 / 0.281 | 0.141 / 0.142; 0.129 | 0.124 | 0 / 0.029 | 0.38 / 1.26 (0.38 / 1.00) | APPROVE (n6) |
| 2-5 Twin Lanterns | 0.366 / 0.414 | 0.110 / 0.106; 0.104 | **0.098** (p10 0.100) | 0 / 0.028 | 0.88 / 1.27 (0.74 / 1.11) | APPROVE (m6, n5, n6) |
| **Set** | | | | | | **APPROVE** |

\* 2-3's p90 comes from the painting: the no-quiet board gives 3.97 and 3.58 at the same two pegs. Its median of 1.34 is the peg at (114, 500), where the quiet adds about 0.3 (1.02 with no quiet); see n8.

**What holds everywhere:**
- **F6.** Every kind clears 0.20 at 1x and 0.8x at its worst placement, movers included. On my measure the tightest is 0.248.
- **Nothing new reads as a peg.**
  - Bright spots match round 3 to within detection noise.
  - The one apparent new 8 px spot on 1-4 at (180, 330) is the same lamp spark as in round 3 (`ux4/b04_pair.png`).
  - The new blue stars (1-3, 1-4, 1-5, 2-5) and 2-2's (538, 346) read cleanly on their grounds.
- **Cleared boards look finished.**
  - No board shows per-peg coins (`ux4/nv_all.png`).
  - What the quiet leaves is layout-scale: a teal "heart" in 1-5's aurora, a plum envelope in 2-4's sea, and mauve haze round 2-5's lanterns. The exception is the hot gaps at the walls (n6).

## Round-3 findings

**G3 (Major: the quiet could not be drawn by main's scene format): RESOLVED.**

*Formula check.* `dress.py` now does exactly what main does:
- It caps the clearance at 96, as main's `MoonfallClearance.Far` does.
- It blurs the clearance by `min(quietBlur, 40)`, using artlib's three-box blur, edge-held. This is the same blur as main's `MoonfallFilters.Blur`; I checked the running-sum indices.
- It applies the smoothstep `[a, b]` after the blur.
- It scales each region by `1 - 0.6·near` (invert, scale 0.6), and sets the jewel's mask to `1 - 0.5·near` (the palette's `where`).
- `MoonfallGrade.Jewel` blends regions and `Mask` with the same arithmetic as `dress2.jewel`.

*Numerical check (`rt4.py`).* I built main's clearance from the level JSON: circles along the 48-point mover path, plus line and arc capsules, each written from the C#.

| Comparison | At 1x | At 2x |
|---|---|---|
| Clearance field vs `fc.piece_distance` capped at 96 | max difference ≤ 3e-5 (all ten) | max difference ≤ 3e-5 (all ten) |
| `near` plane, pipeline vs my port | max difference **0.0** (all ten) | ≤ 0.009 on eight boards; 0.036 on 2-2; 0.099 on 1-1 |
| Dressed scene, pipeline vs my port | OKLab ΔE **0.0** (all ten) | ΔE ≤ 0.0022 on nine boards; 1-1 max 0.014 (p99.9 0.008) |

- At 2x, `dress()` also reproduces `build/dressed/*@2x.png` to within 0.5/255, so the build outputs are current.
- The 2x residue sits in the bottom 40 units of the canvas (`neardiff.py`; 1-1's worst at (319, 560)). It comes from resampling, not the formula:
  - the pipeline Bicubic-resizes the uncapped field, then caps it;
  - main caps it, then samples bilinearly with `Far` off the board;
  - the edge-held blur carries the bottom row's difference about 40 units in.
- It is below visibility and outside the pegs' area. The converter test I suggested in round 3 (ΔE about 0.01) should allow 0.015 there, or the pipeline could bilinear-sample the clearance the way `ToPlane` does.

**m3 (Minor: 1-5's protan separation fell to the floor): RESOLVED.**
- Protan p10 0.101 → **0.123**; minimum 0.062 → **0.112** at (540, 150). Deutan minimum 0.083 → 0.131.
- This meets the target I set (p10 ≥ 0.12, minimum ≥ 0.10), on the runtime's form.
- F7 still holds: second-jewel share 0.158, just over its 0.15 floor.
- The aquamarine #30C490 at chroma 0.22 still renders as a strong green at the aurora's outer edge, far from pegs (`ux4/c05_aurora.png`). It reads as aurora and is no louder than round 3's.

**m4 (Minor: the print gate's blind shapes and calibration): PARTLY RESOLVED; the calibration created a new problem (m5).**
- **Now caught** (my `fool4.py` and the self-tests):
  - a ring 13–19 units out (median 0.023);
  - a ring 22–34 out;
  - a crisp disc 40 out;
  - lightness coins;
  - coins along a mover's path (median 0.068).
- **Still blind:**
  - half-disc and quarter-disc prints (median 0, as recorded in the context);
  - a ring 36–44 out, which falls between the two band pairs;
  - a band round a long brick: bricks are still skipped, and it passes with median 0.
- The pipeline's dress draws none of these, so they stay Nits.
- The pilots are now known-good self-test cases, as I asked. But see m5.

**n1 (Nit: 1-4's pale lower dome): PARTLY RESOLVED.**
- The new `mul 0.8` disc at (440, 478) took (436, 476) off the worst list (`ux4/c04_low.png`).
- The pipeline's tightest is still exactly 0.202, now purple at 0.8x at (310, 418). The undressed painting there measures 0.199, so the place comes from the painting, not the dress.
- Mine is 0.248. Any grade change will still trip the gate here.

**n3 (Nit: 1-3's cactuar mouth and eyes): UNCHANGED.** Protan 0.099 at (452, 300); deutan 0.116.

**n5 (Nit: 2-5's lower-corner fronds read as stiff ladders): UNCHANGED.** The framing was not touched. This remains the art supervisor's call.

## New findings

### m5 (Minor, pipeline; fix before the next build): the print gate passes round 2's coin defect on real boards, because the grain normalisation zeroes most pegs

**Evidence.**
1. **The median is zero on every board.**
   - Every level reports median 0.0, and six of ten report p90 0.0.
   - Per peg, the "grain" (std of the scene less its 4-unit blur, over the 5–70 ring) has a median of 0.020–0.072, and is 0.024 even on 2-2's flat painted sky. So prints under about 0.03 count as 0.
   - On the shipped boards the grain zeroes 22–51 pegs whose raw print exceeds 0.005 (`print4.py`; 1-4: 49 of 54).
   - The std is inflated by stars and sparkles, which do not mask a smooth tone shift. A robust spread (1.4826 × MAD) gives a lower grain on 2-2 and lifts its median off zero.
2. **Real boards with visible coins pass.** Same recipes, with only the quiet changed:

   | Board and quiet | Gate (median / p90) | My dE/texture, median / p90 | Verdict |
   |---|---|---|---|
   | 2-2, round 2's `[22, 12]`, no blur | 0 / 0.046 | 2.18 / 2.35 (pipeline: 0.42) | **PASSES** |
   | 2-1, `[22, 12]`, no blur | 0 / 0.070, exactly at the limit | 0.97 / 2.01 | **PASSES** |
   | 1-1, literal (`quietBlur 0`) | 0 / 0.050 | 1.85, n 1 | **PASSES** |
   | 2-3, literal | 0 / 0.0731 | 3.05 | fails, by 0.003 |

   - 2-2's coins are plain to see: blue coins on the rose sky (`ux4/var_base-07_22_12_0.png`, right panel). This is round 2's G1 defect, which I measured at 2.04 then.
   - 2-1's coins carve the green water (`ux4/var_base-06_22_12_0.png`).
   - The context's calibration claim (round 2's quiet gives median 0.012–0.095 on flat boards and p90 0.07–0.10 on textured ones) does not hold for 2-1 and 2-2 with the current palettes.
3. **Partial coins pass on a flat sky.** Raising p90 to 0.070 lets crisp ΔE 0.05 coins through on up to 4 of 10 pegs: median 0, p90 0.050 (`fool4.py`). The old 0.026 caught this.

**Why Minor, not Major.** The ten levels as built have no coins. I confirmed this independently (dE/texture within target on all but 2-3's painting-driven median; direct views). The failure is in the guard, as with round 3's m4. But this guard exists because of round 2's Major, and today it would let that defect back in on 2-1 and 2-2.

**Fix.**
1. Add **real-board known-bad cases** to `readability.selftest`: 2-2 and 2-1 dressed with `quiet [22, 12], quietBlur 0` must fail. With the pilots as known-good, any later normalisation then has to separate the defect from the approved art.
2. **Count the excess, not all-or-nothing.** Score each peg as `max(0, print - 1.2 × grain)`, and measure the grain robustly (1.4826 × MAD of the high-pass, so stars do not inflate it). Then reset the p90 between the two sides. My scratch run (`gatefix.py`):

   | Board | Robust + soft (median / p90) |
   |---|---|
   | Pilots | at most 0.0027 / 0.032 |
   | 2-1 with coins | 0.0026 / **0.045** (as built: 0.0012 at p90) |
   | 2-2 with coins | **0.0105** / 0.036 (as built: 0 / 0.004) |
   | 1-1 as built | 0.0017 / 0.036 |

   1-1 as built sits close to the line, so the thresholds must be set against these cases, not by eye.
3. Until then, keep my dE/texture (≤ 1.2 median at isolated pegs, on the no-veil board) as a second guard in readcheck. It flags all three passing cases above.

### m6 (Minor): the blur-first quiet lowered F9 on four boards; four single places are now under 0.10

**Evidence.** The rule (p10 ≥ 0.12, or ≥ 0.101, the lowest pilot) holds on every board. But a narrow quiet (`[22, 12]` to `[30, 12]`), blurred before the smoothstep, barely reaches sparse pegs. So the second jewel now sits at full strength round them. My round-3 proxy predicted this direction.

| Level | Protan min, round 3 → 4 | Other figures | Where, and why |
|---|---|---|---|
| 1-2 | 0.126 → **0.085** at (678, 413) | deutan 0.105 | the green (hue 150) water, unquieted at the right edge |
| 2-3 | 0.113 → **0.086** at (512, 118) | pipeline p10 0.109 | the green-teal chart at the top |
| 1-4 | 0.107 → 0.097 at (230, 150) | deutan **0.078**; normal-vision 0.091; p10 0.116 → 0.110 | the crimson sky, now unquieted round the high oranges |
| 2-5 | 0.111 → 0.104 | protan p10 0.112 → 0.106 (pipeline 0.110); **deutan p10 0.100**, below the 0.101 pilot floor if that floor applied to deutan; normal-vision minimum 0.099 at (447, 507) | oranges on the rose water |

- In the crops, every one of these still reads by value: pegs are clearly lighter than their ground in protan and deutan (`ux4/cvd_a.png`, `ux4/cvd08.png`, `ux4/cvd10s.png`). So this is a margin problem, not a failure.
- Four boards now sit within 0.01 of the pilot floor: 1-3, 1-4, 2-3 and 2-5.

**Fix.** Use the README's own recipe ("widen it until F9 holds"), then re-check F7's share:
- widen the quiet on 1-2 and 1-4 to `[40, 18]`, and on 2-5 likewise (2-3 is already `[44, 12]`, so its fix is the disc below);
- or add a `keepMask` disc at (678, 413), (230, 150) and (512, 118);
- or move those three candidates off the strongest second jewel.

Target: protan p10 ≥ 0.12 or unchanged from round 3; every place ≥ 0.10 protan and deutan.

### n6 (Nit): the quiet's gaps glow hottest, as red strips at the walls on 2-4, 2-5 and 1-3

**Evidence.**
- With blur-first, the region keeps full chroma wherever no peg is near. On 2-4, whose rose region is now at chroma 0.18, the walls show vertical crimson slabs: left at about x 80–95, y 230–330; right at about x 705–720, y 200–300. A hot band also runs across the bucket's approach at y 515–560 (composite `base-09.png`; `ux4/r3r4_nv.png`, top right).
- 2-5 has the same wall strips and a hot crimson door under the arch. 1-3 has crimson strips beside both walls at y 300–340.
- None reads as a peg, and F6 and F9 hold beside them. But they pull the eye to the field's edges, and on the cleared board they read as the layout's negative.

**Fix.** Fade each region within about 40 units of the walls (`["x", 80, 125]` and `["x", 720, 675]` in the region mask), or lower 2-4's region to about 0.15 near the walls.

**Palette look, otherwise:**
- 2-5's teal over crimson is the boldest board in the set. Its teal-to-rose seam near y 170 reads as a twilight band, not an edge. Pegs read on both grounds (`ux4/c10_sky.png`).
- 1-3's rose desert, 1-4's crimson sky (no louder than round 3; `ux4/cmp04top.png`), 1-5's aurora, 2-2 (now calmer, region at chroma 0.045) and 2-4's sea: nothing I would call garish or hard to read beyond n6.

### n7 (Nit, converter): two keep masks have no runtime form

- 1-5's `keepMask` is a product of four ramps, inverted as a whole (a box). The runtime `where` is a product of terms each inverted on its own, so it cannot express "1 − box".
- 1-3's `keepMask` is a `poly`, which also has no counterpart.
- 2-5's two `keepMasks` discs convert exactly, as two inverted disc terms.
- The README lists `poly` but not the inverted-product case. When converted, 1-5's dome house (x 112–240, y 464–499) will be recoloured unless this is handled. Fix: approximate the box with a `disc` term, or add a box term to the format.

### n8 (Nit): 2-3's dE/texture median is 1.34, over my 1.2 target

- The median peg is (114, 500): 1.02 with no quiet, 1.34 with it.
- The other four isolated pegs are set by the painting: the two worst come from it, and the other two measure 0.44 and 0.55.
- The cleared board reads as calm water. This is not visible as a coin, but it is the one board where the quiet adds measurably round an isolated peg.

## Unverified

- **No `MoonfallRender` output.** My port is numpy from the C# source, not the runtime. It matches exactly at 1x and to within ΔE 0.014 at 2x, but the converter does not exist. `tone`, rim-fill masks, `poly` and `not-poly` terms, and 1-5's inverted-product keep mask (n7) still have no runtime form, so the shipped look on 1-3, 1-4 (`tone`) and 1-5 is unverified.
- **The gate fix in m5 is a scratch experiment.** Thresholds for the soft, robust form need setting against the self-test cases I named.
- **Per-piece veil sprites, in-game rendering, and the real 640×480 window** beyond my 0.8x Lanczos proxy.
