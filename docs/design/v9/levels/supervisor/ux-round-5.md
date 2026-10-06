# UX/UI specialist, levels round 5

**Reviewed:** the ten levels base-01 to base-10 at `a52c841c`, in worktree `agent-a570460c913ca1c79`. Round 4 was reviewed at `b32a74e2`.
- **Read:** `round5-context.md`, `round2-context.md`, `git diff b32a74e2 a52c841c` (dress.py, readability.py, README, scenes, layouts), the reports, the composites, and the gitignored `build/dressed` and `build/composites` (timestamps 03:45–04:12 on 2026-10-06, so current).
- **Read on main, via `git show`:** `MoonfallSceneRecipe` (mask kinds), `MoonfallSceneBuilder.Palette` (mask evaluation) and `MoonfallColor.Smooth`.

**OVERALL: REVISE, on one Major (G4): 2-3 now prints a violet disc round one peg.**
- **G4 is a single line in one recipe.** To lift F9 at (512, 118), 2-3's green-teal region was given an inverted disc centred exactly on that candidate peg: `["disc", 512, 118, 34, -12]`. On the cleared board, and as a halo in play, it shows as a lilac coin about 90 units across. This is the per-peg print that the binding round-2 decision rules out ("quiet by low frequency, never per peg"). No shipped gate can see it. I share the blame: my round-4 m6 listed "a keepMask disc at … (512, 118)" as one fix.
- **Everything else I would ship.**
  - m6 is resolved: every F9 place is ≥ 0.10, except 1-3's known cactuar mouth, which is unchanged since round 3.
  - The wall fades and `regionQuiet` all have exact runtime forms. They calm 2-4, 1-3 and 2-3.
  - The repaired print gate now fails round 2's coin on all ten real boards.
- **m7 (Minor, pipeline):** the gate is still blind to plainly visible hue coins on the lightness-textured game boards (1-4, also 1-1 and 2-3). It cannot see a single-peg print at all, and G4 is one.

## Method

**Ran (read-only on the repo; scripts and outputs in `scratchpad/ux5/`):**
- **`mfl.py selftest`:** all ok (exit 0). All three real-board coin cases ran, because the game textures are fetched:

  | Case | Median | p90 | Verdict |
  |---|---|---|---|
  | 2-2 | 0.0105 | 0.0361 | fails, as it should |
  | 2-1 | 0.0027 | 0.0452 | fails, as it should |
  | 1-4 | 0.0 | 0.0745 | fails, as it should |
  | Pilots | at most 0.0027 | at most 0.0324 | pass |

  The refusal of `quietBlur` below 40 is self-tested. `git status` is clean, and I did not run `build`.
- **`measure5.py`:** my round-3/4 measure (`ux4/measure3.py`) rerun unchanged on the current builds.
  - F6: worst p90 ring, every kind, every placement, movers at 24 points, at 1x and 0.8x Lanczos.
  - Machado protan, deutan and normal: orange core against the ground 12–20 px round every candidate, movers and orange-able bricks.
  - Ghost on the baked, no-veil and no-quiet boards, and bright peg-sized blobs.
  - Output: `ux4_measure.json` in `ux5/`; `summ5.py` compares it with round 4.
- **`print5.py`:** the shipped `dress_print` on all ten real boards, as built and against known-bad variants:
  - round 2's coin (`[22, 12]`, no blur);
  - the shipped quiet with blur 0, 10 and 20;
  - synthetic coins, radius r+18 with a 6-unit feather, on the shipped dress: hue ΔE 0.03 and 0.05 (a/b along +a+b), and lightness −0.03.
  - Each gate verdict sits beside my dE/texture at isolated pegs (`print5.json`).
- **`coinview.py`:** renders the variants the gate passes, so I could see whether they show.
- **`gate5.py`:** a candidate repair that masks lightness prints by lightness grain and hue prints by hue grain.
- **`pilotworst.py`:** the worst single-peg print on the six pilots.
- **`walls4.py`:** F7's new wall-strip figure on round 4's jewels against round 5's.
- **`f9var.py`:** 2-3 without the disc, and alternatives. Each gets protan and deutan over all candidates, F7, the print, and a crop.

**Viewed (all crops ≤ 1568 px):**
- Round 4 against round 5 no-veil cleared boards for all ten (`cmp01`–`cmp10.png`).
- 2-3's disc in four states, undressed → dressed → cleared → in play (`c08_disc.png`), plus the full board (`c08_full.png`) and the alternatives (`c08_var.png`).
- 1-1's left wall (`c01_wall.png`), 1-4's lower dome and sky (`c04_low.png`, `c04_sky.png`), 2-5's seam (`c10_sky.png`).
- The gate-passing coins: `coin_base-04_syn05.png`, `coin_base-01_syn03.png`, `coin_base-08_synL.png`, `coin_base-08_blur10.png`.

## Verdicts

Notes on the columns:
- **Print gate:** pipeline form, median / p90, and the worst single peg.
- **dE/texture:** isolated pegs on the no-veil cleared board, median / p90, with round 4 in brackets. Target: median ≤ 1.2, p90 ≤ 2.0.

| Level | F6 worst, pipeline / mine | Protan orange p10, pipeline / mine; min (mine) | Deutan min | Print gate (worst peg) | dE/texture, no-veil | Verdict |
|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 0.290 / 0.357 | 0.134 / 0.133; 0.130 (548, 199) | 0.140 | 0.0006 / 0.0324 (0.058) | 0.29, n 1 (0.78) | APPROVE (n10) |
| 1-2 Horizon by Night | 0.318 / 0.364 | 0.137 / 0.137; **0.126** (678, 413), was 0.085 | 0.144 | 0 / 0.0095 | 0.49 / 0.75 (0.31 / 0.94) | APPROVE |
| 1-3 The Cactuar | 0.285 / 0.330 | 0.122 / 0.122; 0.099 (452, 300), the mouth | 0.116 | 0 / 0.0097 | 0.29 / 0.87 (0.32 / 0.97) | APPROVE (n3) |
| 1-4 The Gilded Dome | **0.217** / 0.267 (was 0.202 / 0.248) | 0.118 / 0.118; **0.109** (300, 128) | **0.108**, was 0.078 | 0 / 0.0182 | 1.00 / 2.19 (0.69 / 1.50) | APPROVE (n11, n12) |
| 1-5 The Crystal's Call | 0.368 / 0.396 | 0.123 / 0.123; 0.112 | 0.131 | 0 / 0.015 | 1.10 / 1.52 (same) | APPROVE |
| 2-1 Limsa Across the Water | 0.317 / 0.377 | 0.140 / 0.140; 0.134 | 0.148 | 0 / 0.0013 | 0.55 / 1.03 (same) | APPROVE |
| 2-2 Moonpath on the Bay | 0.332 / 0.369 | 0.131 / 0.132; 0.130 | 0.136 | 0 / 0.0039 | 0.42 / 0.84 (same) | APPROVE |
| 2-3 The Kraken's Sea | 0.310 / 0.351 | 0.113 / 0.122; 0.105 (237, 147) | 0.124 | 0 / 0.0078 (**0.088 at (512, 118)**) | 1.56 / 3.37 (1.34 / 3.37) | **REVISE (G4)**, n8 |
| 2-4 The Ferry Under Sail | 0.285 / 0.281 | 0.134 / 0.137; 0.131 | 0.131 | 0 / 0.0145 | 0.43 / 1.03 (0.38 / 1.26) | APPROVE |
| 2-5 Twin Lanterns | 0.366 / 0.414 | 0.114 / 0.113; **0.111** (180, 471) | **0.112** (p10 0.115), was 0.098 | 0.0024 / 0.0302 | 0.81 / 1.95 (0.88 / 1.27) | APPROVE (n6 rest) |
| **Set** | | | | | | **REVISE (G4 only)** |

**Ghost:** pipeline, baked veil, median against the 0.066 limit.

| Level | Round 4 | Round 5 |
|---|---|---|
| 2-4 | 0.065 | 0.065 (unchanged) |
| 1-4 | 0.046 | 0.056 |
| 2-5 | 0.034 | 0.048 |
| All others | | ≤ 0.044 |

1-4 and 2-5 rose with the wider quiet but stay under the limit. On my no-veil runtime proxy every board is ≤ 0.041.

**Nothing new reads as a peg:**
- Bright-blob lists match round 4, except:
  - 1-3 gains one 4 px star-sized spot at (710, 318);
  - 1-4's lamp sparks moved: the 8 px spark at (180, 330) is gone, and the new ones are 3–7 px at luma 0.41–0.43.
- Nothing peg-sized (12 px or more) is new.

## Round-4 findings

**m5 (Minor: the print gate passed round 2's coin on real boards): RESOLVED as asked; a residual blind spot is now m7.**
- **Done:**
  - the real-board known-bad cases are in the self-test (2-2, 2-1, 1-4);
  - the grain is robust (1.4826 × MAD) and the scoring soft (the excess over 1.2 × grain);
  - the thresholds are reset to 0.006 / 0.040;
  - the README is corrected.
- **Independently, round 2's coin now fails on all ten boards** (`print5.json`):

  | Board | Median | p90 | Caught by |
  |---|---|---|---|
  | 1-1 | 0.0346 | 0.0527 | median |
  | 1-2 | 0.0282 | 0.0857 | both |
  | 1-3 | 0.0126 | 0.0647 | both |
  | 1-4 | 0 | 0.0742 | p90 |
  | 1-5 | 0.0877 | 0.1266 | both |
  | 2-1 | 0.0026 | **0.0452** | p90 |
  | 2-2 | **0.0105** | 0.036 | median |
  | 2-3 | 0.0469 | 0.0656 | both |
  | 2-4 | 0.0234 | 0.0614 | both |
  | 2-5 | 0.0371 | 0.0516 | both |

- **Known-good, all passing:**
  - the pilots: p90 at most 0.0324;
  - the ten levels as built: p90 at most 0.0324 on 1-1, pipeline form (0.0286 at 1x).
- **The corridor is narrow:** on p90, 0.0324 (good) against 0.0452 (2-1's coin), about 13% either side of 0.040. It holds today.

**m6 (Minor: four places under 0.10 after the blur-first quiet): RESOLVED.**

| Place | Protan, round 4 → 5 | Other figures (round 5) | How |
|---|---|---|---|
| 1-2 (678, 413) | 0.085 → 0.126 | deutan 0.144 | wider quiet `[40, 18]` |
| 1-4 (230, 150) | 0.097 | set minimum 0.109 at (300, 128); deutan 0.078 → 0.108 | wider quiet; F6 lifted by a tone disc (n12) |
| 2-5 | 0.104 → 0.111 | deutan minimum 0.098 → 0.112; deutan p10 0.100 → 0.115 | wider quiet |
| 2-3 (512, 118) | 0.086 → 0.155 | | the disc, which is G4 |

- p10 is ≥ 0.12 everywhere, or no lower than round 3: 1-4 is 0.118 (round 3: 0.116), and 2-5 is 0.113 (round 3: 0.112).
- F7 shares all pass. The lowest are 1-5 at 0.158 and 1-2 at 0.166 (1-2 was 0.22; the wider quiet took some).

**n1 (1-4's pale lower dome): RESOLVED.**
- Pipeline F6 0.202 → 0.217; mine 0.248 → 0.267. The worst place moved to (517, 386).
- How it was done is n12.

**n3 (1-3's cactuar mouth): UNCHANGED.** Protan 0.099 at (452, 300); deutan 0.116.

**n5 (2-5's corner fronds): UNCHANGED.** This remains the art supervisor's call.

**n6 (hot wall strips): RESOLVED on 2-4 and 1-3; PARTLY on 2-5.**
- **2-4:** the crimson slabs are gone (`cmp09.png`). The sea is a muted plum, and only a faint band remains in the bucket's approach.
- **1-3:** the strips at y 260–340 are gone (`cmp03.png`).
- **2-5:** narrow crimson strips at x 75–100 and the hot door under the arch remain. They are the first jewel at full keep where no peg is near, not a region, so neither the wall fade nor F7's new check reaches them (n9).

**n7 (keep masks with no runtime form): PARTLY.** The README now lists 1-5's inverted-product keepMask. It is still unconverted.

**n8 (2-3's dE/texture median over 1.2): UNCHANGED, slightly worse.**
- 1.34 → 1.56, at the peg (114, 500).
- The new wall fade (`["x", 75, 135]`) runs its gradient straight past that peg. It does not show on the board.

## New findings

### G4 (Major): 2-3's inverted disc centred on the candidate (512, 118) prints a coin round one peg

**Evidence.**
- `rhotano-wonders.json` region mask: `[["luma", 0.16, 0.24, 4], ["x", 75, 135], ["x", 725, 665], ["disc", 512, 118, 34, -12]]`. That is the green-teal region taken off 22–46 units round the peg at (512, 118), which sits 0.0 units from the disc's centre.
- **On the cleared board** it is a lilac/lapis coin on the green chart, visible at full-board scale (`cmp08.png`, right; `c08_full.png`, right).
- **In play** it is a halo round that one candidate near the launcher (`c08_full.png`, left; `c08_disc.png`, panel 4). It singles out one peg as if it were special.
- **The print check measures it** at 0.088 at (512, 118). That is the highest worst-peg value in the set, and about 2.4 times the p90 of round 2's coin on 2-2 (0.036).
  - The median/p90 rule cannot see one peg.
  - The ghost check sees nothing on 2-3, which has no isolated pegs.
  - A worst-peg ceiling is not workable either: the pilots' own worst pegs measure 0.0855 (base-p2) and 0.19 (exp-p3) (`pilotworst.py`).
- **It is over-strong:** it lifts protan at the place from 0.086 to 0.155, where 0.10 was the target.

**Fix.** Measured in `f9var.py`; crops in `c08_var.png`.
- **A (preferred):** drop the disc and make (512, 118) blue.
  - Without the disc, it is the only place under 0.10: protan 0.086, deutan 0.109. Every other place is ≥ 0.10, with p10 0.119.
  - 2-3 has 29 candidates, so it keeps 28.
  - The peg is left in only 3% of lost games, so the ramp should barely move. Re-measure it.
- **B:** replace the disc with a broad one off the peg, `["disc", 560, 100, 90, -40]`.

  | Figure | Value |
  |---|---|
  | Protan at (512, 118) | 0.153 |
  | Protan, set minimum | 0.105 at (237, 147) |
  | Protan p10 | 0.122 |
  | Deutan minimum | 0.124 |
  | F7 share | 0.206 (passes) |
  | Walls | 0.83 |
  | Print worst | 0.0217 |

  It reads as a patch of deeper water in the chart's top-right, not a coin. It is still a shape, though.
- **Rejected:** a top fade `["y", 90, 170]` or `["y", 110, 190]` holds F9, but drops F7's share to 0.11 or 0.091 (fail).
- **Pipeline:** add a lint to `dress.py`, like the blur pin. Refuse a region or keepMask `disc` term centred within about 12 units of a peg with r + |f| < 80.
  - 2-3's disc trips it.
  - 2-5's lantern keepMasks, at (196, 240) and (604, 240), are 28 or more units from any peg and pass.
- **README:** the lesson "widen it until F9 holds" should add "never with a mask centred on a peg". My round-4 m6 suggestion of keep discs was wrong.

### m7 (Minor, pipeline): the print gate is blind to hue coins on lightness-textured boards, and to single-peg prints

**Evidence** (`print5.json`; the gate as shipped).

| Variant | Gate verdict | My view |
|---|---|---|
| Hue coin ΔE 0.05 on 1-4 | **passes** (median 0, p90 0.0199) | plainly visible: orange-red discs in the crimson sky and over the dome (`coin_base-04_syn05.png`) |
| Hue coin ΔE 0.03 on 1-1 | **passes** (0.0039 / 0.0234) | visible as rust discs in the flat sea west of the land (`coin_base-01_syn03.png`) |
| Hue coin ΔE 0.03 on 1-4 and 2-3 | passes | |
| Lightness coin −0.03 on 1-4 and 2-3 | passes | on 2-3 hard to see (`coin_base-08_synL.png`): a true negative |
| Shipped quiet at blur 10 | passes on 1-1, 1-3, 1-5, 2-1, 2-2, 2-3, 2-4 | on 2-3 visible green blotches (`coin_base-08_blur10.png`) |
| Shipped quiet at blur 20 | passes on all ten | |

- The context says synthetic 0.03 coins fail on 9 of 10. With my coin (radius r+18, +a+b), they pass on 3 of 10: 1-1, 1-4 and 2-3.
- My dE/texture is just as blind on 1-4 (syn05: 0.83 / 1.35). Both measures let the painting's lightness grain mask a hue shift, which it does not mask perceptually.
- Two per-peg paths stay open outside the quiet's pin:
  - `disc` terms on pegs (G4);
  - the `dist` mask term: an unblurred clearance, so a literal coin, unused today.
- A per-peg `tone` disc sits in the graded scene and so is invisible to the print check by construction (n12).

**What I tried (`gate5.py`).** Splitting the print into lightness and hue parts, each less 1.2 times its matching grain:
- **Catches:** every synthetic coin, and round 2's coin with wide margins (1-4: p90 0.093; 2-1: median 0.0206 / p90 0.0636).
- **But also flags known-good boards:**
  - 1-1 as built (0.0143 / 0.045), whose broad quiet round its clusters is a measurable hue gradient;
  - pilots base-p2 (median 0.0075) and exp-p3 (0.0069);
  - 2-5 as built (median 0.0068).
- The known-good and known-bad medians overlap: 1-1 built at 0.0143 against 1-4's syn03 at 0.0117. So it is not a drop-in replacement.

**Why Minor.** The levels as built have no coins except G4, which I confirmed by eye on all ten. The quiet is pinned, and the gate does the job it was asked to do (round 2's coin, all ten boards).

**Fix.**
1. Make the guard structural: refuse peg-centred `disc` terms (G4's lint) and `dist` terms in jewel masks.
2. Add `syn05`-on-1-4 as a known-bad self-test case, and record in the README that the gate cannot see hue prints on 1-4, 2-3 or 1-1, nor a single peg.
3. If the gate is to cover hue, split the masking and recalibrate against the six pilots, the ten as built, round 2's coin ×10 and the hue coins ×10.

### n9 (Nit): F7's new wall-strip check misses one of the three strips it was made for, and all first-jewel strips

Round 4's jewels dressed on the current boards (`walls4.py`):

| Level | Round 4 → round 5 | Caught? |
|---|---|---|
| 1-1 | 1.79 → 0.21 | yes |
| 2-3 | 2.06 → 0.69 | yes |
| 2-4 | 2.19 → 0.27 | yes |
| 1-3 | **1.32** | **no**: its crimson strips ran only y 260–340, and the area share over the full-height strip dilutes them |

- 2-5's remaining wall crimson is the first jewel, which the check does not look at.
- 2-2 sits at 1.43, against the 1.5 limit.
- **Fix:** take the figure in 100-unit-tall windows, the worst window, and weight it by chroma.

### n10 (Nit): the wall fade on 1-1 swapped a maroon wall strip for a lapis rail

- At y 250–450 the strip at x 78–98 measures chroma 0.103 (hue 277), and x 702–722 measures 0.109. The field measures 0.063–0.073; round 4's maroon was 0.048.
- With the region gone, the first jewel shows at full keep, because no peg is near. The result is a vivid blue column, full height, along both walls (`c01_wall.png`).
- It reads as a frame shadow and is in the field's hue, so it is not garish.
- 1-3 and 2-3 show the same, milder (0.091–0.097 against 0.070).
- **Fix, if wanted:** add the same x fade as an inverted term on the palette's `where` (scale about 0.4).

### n11 (Nit): 1-4's wider quiet hollows the crimson sky along the peg arc

- The red now holds only at the very top and the corners. A grey-mauve band follows the pegs above the dome (`c04_sky.png`).
- dE/texture at the sky's isolated pegs rose: (300, 128) to 2.49, (548, 100) to 2.27, (230, 150) to 1.67. The board's p90 went from 1.50 to 2.19.
- It reads as haze over the dome, not as coins, and it is the price of m6's fix. Leave it unless the art supervisor objects.

### n12 (Nit): 1-4's tone discs sit on pegs and are outside the print check

- Both `mul` tone discs are centred on pegs:
  - the new one, (310, 418, 50, 30, mul 0.85), sits 0.0 units from a peg;
  - the existing one, (440, 478), sits 4.5 units from the peg at (436, 476).
- Measured ΔL: −0.026 at 20–35 units out, −0.009 at 50–65, and 0 by 80. That is broad and lightness-only on a textured painting. I cannot see it (`c04_low.png`).
- The print check compares against the graded scene, which already includes the tone, so it can never see this. Fold it into m7's structural lint (peg-centred discs), or exempt `tone` with a stated reason.

## Formula and runtime checks

- **`regionQuiet`** maps onto main's `MoonfallMaskTerm(Near, [a, b], Blur, Invert: true, Scale: regionQuiet)`. Main evaluates `1 − scale·v`, which is `dress.py`'s `rm·(1 − rq·near)`, per region. The palette's 0.5 is unchanged.
- **The wall fades**, `["x", 75, 135]` and `["x", 725, 665]`, are main's `X` kind exactly. `MoonfallColor.Smooth` accepts either edge as the larger.
- **2-3's negative-feather disc** converts exactly: main's `Smooth(r + f + 1e-3, r − f, d)` with f = −12 is 0 inside 22 and 1 outside 46, as in `dress.py`. It is runtime-expressible; that is not the problem with it.
- **`QUIET_BLUR_MIN` 40** is enforced: a recipe asking for less raises, and this is self-tested.

## Unverified

- **No `MoonfallRender` output.** The converter does not exist, so the shipped look is unverified where the pipeline uses terms with no runtime form:
  - `tone` (1-3, 1-4);
  - rim-fill;
  - `poly` and `not-poly` (1-3, 2-4);
  - 1-5's inverted-product keepMask.
- **The fixes are scratch measurements, not builds.** My G4 alternatives and the `gate5` split are on 2-3 and the ten boards, but nothing was rebuilt. G4 option A's effect on 2-3's ramp is unmeasured.
- **Per-piece veil sprites, in-game rendering, and the real 640×480 window** are covered only by my 0.8x Lanczos proxy.
- **Colour-vision simulation:** Machado at severity 1 only. Tritan and anomalous trichromacy were not checked.

