# UX/UI specialist, levels round 6

**Reviewed:** the ten levels base-01 to base-10 at `c0b5ac71`, in worktree `agent-a570460c913ca1c79`. Round 5 was reviewed at `a52c841c`.
- **Read:** `round6-context.md`, `round2-context.md`, `git diff a52c841c c0b5ac71` (dress.py `lint`, readability.py hue clause and wall window, README, the 2-3 and 1-4 scenes, layouts 1-2, 2-1, 2-3 and 2-5), the reports, the composites, and the gitignored `build/dressed` and `build/composites`. Their timestamps run 06:04–06:13 on 2026-10-06, before the 06:18 commit, and `build/composites/*.png` are byte-identical to the committed `composites/`, so they are current.
- **Read on main, via `git show`:** `MoonfallSceneRecipe` (mask kinds) and `MoonfallSceneRecipeLoader` (term validation).

**OVERALL: APPROVE.** G4 is resolved and nothing on the ten boards is a Major.
- **G4 is gone.** 2-3 has no disc term; the lead bird (512, 118) and the right wing (558, 156) are blue. The cleared board shows no coin where the lilac disc was (`ux6/cmp08.png`). F9 holds without it: protan min 0.110 at (133, 185), deutan min 0.128.
- **m7 is PARTLY resolved.** The structural guard and the hue clause both work for the cases they were built for. But:
  - the guard can be sidestepped by nudging a disc 13 units off the piece's edge, by a feature `near` term, or by a glow. That is new Minor **m8**; the README says these routes are covered and they are not.
  - the hue clause catches coins on every peg on 8 boards from ΔE 0.02–0.035. On 1-4 it does not catch them below ΔE 0.05 in any direction, and it never catches a coin confined to one region of pegs.
  - No shipped recipe uses any of these routes, so the ten boards are clean.
- **New Minor m9:** 1-2's green water region has no wall fade. The new wall window reads 6.43 there, inside the range of round 4's wall-strip defects (5.7–6.5). The README dismisses it as "a painted feature", but the painting's water is dark violet; the green is the dress's region.

## Method

**Ran (read-only on the repo; scripts and outputs in `scratchpad/ux6/`):**
- **`mfl.py selftest`:** exit 0, every case ok (`selftest.log`):
  - all 8 lint known-bads are refused, and the ten as shipped pass the lint;
  - syn05 fails on 2-2 (hue 0.0332) and 1-4 (0.0295);
  - the pilots pass, with hue at most 0.0074;
  - the stage-step cases pass.

  `git status` is clean, and I did not run `build`.
- **`measure6.py`:** my round 3–5 measure (`ux4/measure3.py`), unchanged, with fresh caches. It covers F6 (worst p90 ring, every kind, every placement, movers at 24 phases, 1x and 0.8x Lanczos), Machado protan/deutan/normal (orange core against the ground 12–20 units round every candidate, movers and orange-able bricks), ghost (baked and no-veil) and bright blobs. `summ6.py` compares it with round 5.
- **`f9cand6.py`:** per-candidate protan/deutan, including every new candidate (2-5's six sky stars, 2-3's (331, 418), 2-1's deck).
- **`print6.py`:** the shipped `dress_print` + `print_ok` on all ten real boards:
  - as built, and against: round 2's coin, the quiet at blur 10/20, syn03/syn05/synL, and a soft wide coin (r+30, feather 14, ΔE 0.04);
  - amplitude sweeps (ΔE 0.01–0.06) of hue coins along +a+b, −a−b, +a−b, −a+b, and of lightness coins ±L, to find where the rule first fails per board and direction (`print6.json`).
- **`partial.py`, `huep90.py`:** hue coins round only the top 30% / 45% of pegs, and whether a hue-p90 clause could separate them.
- **`lintaudit.py`:** every localised term in the ten recipes (disc, near, poly, derived masks, glows) with its distance from the nearest piece's edge.
- **`bypass.py`, `bypass2.py`:** routes past `dress.lint` on the real 2-3.
- **`walls6.py`, `fade02.py`:** the new wall window on round 4's jewels against the shipped ones; 1-2, 1-4, 1-5 and 2-2 with the README's wall fade.
- **`snr6.py`:** dE/texture at isolated pegs, round 5 against round 6.

**Viewed (all crops ≤ 1568 px):**
- Round 5 against round 6 cleared no-veil boards: 2-3 (`cmp08.png`), 2-5 (`cmp10.png`), 1-2 (`cmp02.png`), 2-1 (`cmp06.png`).
- 1-4's lower city (`c04_low.png`, `c04_blob.png`); 1-2's water, undressed against cleared (`c02_water.png`); 1-2 with a wall fade (`fade02.png`).
- CVD views of 2-5's sky and 2-3's top (`cvd_base-10_80_120.png`, `cvd_base-08_80_80.png`).
- Gate-passing coins: `coin6_base-04_225_0.04.png`, `coin6_base-01_45_0.03.png`, `partial_base-04_top.png`, `partial_base-01_top.png`.
- Lint bypasses: `bypass2_2-3.png`.
- Composites of 1-2, 2-3 and 2-5.

## Verdicts

Notes on the columns:
- **F6:** the pipeline's figure / mine at 1x and 0.8x.
- **F9:** pipeline p10 / my p10, then my minimum.
- **Print:** pipeline form, median / p90 / hue median.
- **Wall window:** the new reported `second_at_walls_worst_window`.

| Level | F6 worst, pipeline / mine 1x, 0.8x | Protan p10, pipeline / mine; min (mine) | Deutan min | Print, median / p90 / hue | Ghost (baked) | Wall window | Verdict |
|---|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 0.290 / 0.357, 0.359 | 0.134 / 0.133; 0.130 (548, 199) | 0.140 | 0.0006 / 0.0324 / **0.0143** | 0.043 | 0.73 | APPROVE (n10) |
| 1-2 Horizon by Night | 0.318 / 0.407, 0.364 | 0.137 / 0.137; 0.126 (678, 413) | 0.144 | 0 / 0.0087 / 0.0013 | 0.028 | **6.43** | APPROVE (m9) |
| 1-3 The Cactuar | 0.285 / 0.331, 0.330 | 0.122 / 0.122; 0.099 (469, 266), (452, 300) | 0.116 | 0 / 0.0097 / 0.0048 | 0.032 | 0.42 | APPROVE (n3) |
| 1-4 The Gilded Dome | 0.219 / 0.286, 0.268 | 0.118 / 0.118; 0.109 (300, 128) | 0.108 | 0 / 0.0182 / 0 | 0.056 | 4.51 | APPROVE (n11) |
| 1-5 The Crystal's Call | 0.368 / 0.397, 0.396 | 0.123 / 0.123; 0.112 (540, 150) | 0.131 | 0 / 0.015 / 0.0007 | 0.034 | 3.67 | APPROVE |
| 2-1 Limsa Across the Water | 0.317 / 0.377, 0.377 | 0.141 / 0.141; 0.134 (528, 346) | 0.147 | 0 / 0.0013 / 0.0041 | 0.029 | 2.12 | APPROVE |
| 2-2 Moonpath on the Bay | 0.332 / 0.387, 0.369 | 0.131 / 0.132; 0.130 (649, 262) | 0.136 | 0 / 0.0039 / 0 | 0.025 | 3.36 | APPROVE |
| 2-3 The Kraken's Sea | 0.290 / 0.363, 0.339 | 0.117 / 0.122; 0.110 (133, 185) | 0.128 | 0 / 0.0072 / 0.0057 | 0.038 (now measurable) | 1.54 | **APPROVE (G4 resolved)**, n8 |
| 2-4 The Ferry Under Sail | 0.285 / 0.281, 0.281 | 0.134 / 0.137; 0.131 (658, 316) | 0.131 | 0 / 0.0145 / 0.0015 | **0.065** (limit 0.066, unchanged since round 4) | 0.42 | APPROVE |
| 2-5 Twin Lanterns | 0.379 / 0.436, 0.401 | 0.115 / 0.114; 0.113 (mover at 190, 438) | 0.114 (p10 0.116) | 0.0024 / 0.0302 / 0.0054 | 0.048 | 1.44 | APPROVE (n6 rest) |
| **Set** | | | | | | | **APPROVE** |

**F6 at 1x and 0.8x:** every board clears the 0.20 margin.
- The lowest is 1-4: pipeline 0.219 at (517, 386), purple 0.8x; mine 0.268 at 0.8x.
- 1-4's new broad tone moved F6 by +0.001 (mine) and by 0.0 against the undressed board.
- 2-3's purple worst fell from 0.310 to 0.296 (pipeline) because the new deal puts purple on a different peg, which changes the purple face. The drop against the undressed board is 0.017, as in round 5.

**F9 at every candidate:**

| Board | Candidates | Protan | Deutan | Normal vision |
|---|---|---|---|---|
| 2-5 six new sky stars | (110, 200), (690, 200), (250, 290), (550, 290), (220, 150), (580, 150) | 0.119, 0.123, 0.129, 0.131, 0.125, 0.127 | 0.120–0.139 | 0.120–0.149 |
| 2-3 | (331, 418) | 0.143 | 0.162 | |
| 2-3 | (300, 140), blue | F6 green 0.458 | | |
| 2-1 | the whole board | min 0.134, p10 0.141 | | |
| 1-1 | | 0.130 / 0.133 | | |

- 2-5's six sky stars: the worst places on 2-5 are the low reflected movers (0.113–0.118), not the new stars. In protan and deutan the stars read as yellow on slate.
- Nothing in the set is under 0.10, except 1-3's known cactuar face: three places at 0.099–0.100, unchanged (n3).

**Ghost:** all at or under 0.065 against the 0.066 limit. 2-3 now has one isolated peg (the blue star at (300, 140)) and reads 0.038 (no-veil 0.023).

**The cleared boards and anything reading as a peg:**
- Bright-blob lists are identical to round 5 on nine boards.
- On 1-4 the new tone changes the blob list: the 23 px blob at (394, 372) is gone, and (242, 397) grew from 13 to 18 px.
- Those are lit lesser-dome masonry, not peg-like (`c04_blob.png`). Nothing peg-sized is new.

## Round-5 findings

**G4 (Major, 2-3's per-peg disc): RESOLVED.**
- `rhotano-wonders.json` region mask is now `[["luma", 0.16, 0.24, 4], ["x", 75, 135], ["x", 725, 665]]`.
- The cleared board shows no coin at (512, 118) (`cmp08.png`, right).
- F9 holds without the disc: protan min 0.110 at (133, 185), p10 0.122 (pipeline 0.117, against the 0.101 pilot floor); deutan min 0.128.
- `lintaudit.py`: no disc term in any shipped recipe sits within 12 units of a piece's edge at a peg's scale:

  | Recipe | Disc | From a piece's edge | Reach | Lint |
  |---|---|---|---|---|
  | 2-5 lantern keeps | (196, 240), (604, 240) | 19.4 | 16 | passes |
  | 2-2 region | (292, 128, 60, −20) | 110.3 | 80 | passes |
  | 1-4 tone | (390, 455, 100, 40) | 14.4 | 140 | exempt (reach ≥ 100) |

**m7 (Minor, the print gate blind to hue coins and single pegs): PARTLY.**
- **Structural guard:** it does what it says. All eight known-bads are refused, and the lint runs in every non-self-test `dress()` call. But it can be sidestepped; see m8.
- **Hue clause: separates syn05 as calibrated.** I reproduce the README's figures:

  | Case | Hue median |
  |---|---|
  | syn05 on the ten | 0.0295 (1-4) to 0.0462 (2-1); fails all ten |
  | Built, known-good | at most 0.0143 (1-1) |
  | Pilots | at most 0.0074 |
  | Limit | 0.022 |

  Calibration margins are +54% on the good side and −25% on the bad side.
- **But the separation is direction- and board-dependent** (`print6.json`). The smallest hue-coin ΔE (round every peg) that the rule fails:

  | Board | +a+b | −a−b | +a−b | −a+b |
  |---|---|---|---|---|
  | 1-4 | 0.05 | 0.05 | 0.05 | 0.05 |
  | 2-3 | 0.03 | **0.05** | 0.03 | 0.04 |
  | 2-1 | 0.025 | **0.04** | 0.035 | 0.025 |
  | 1-1 | **0.035** | 0.02 | 0.025 | 0.025 |
  | Others | 0.02–0.03 in every direction | | | |

  - On 1-4 a −a−b coin at ΔE 0.04 passes (hue 0.0113, p90 0.0369). It shows as plain teal-blue discs across the red sky (`coin6_base-04_225_0.04.png`).
  - At 0.05 along −a−b, 1-4 fails only by p90 (0.0455 against 0.040); the hue clause alone reads 0.0165.
  - Lightness coins on 1-4 pass up to ΔL 0.05.
- **A coin round only part of the pegs passes** (`partial.py`). ΔE 0.05 round the top 45% of pegs passes on 1-1, 1-4 and 2-3; on 1-4 even 0.07 passes. It is plainly visible on 1-4 and 1-1 (`partial_base-04_top.png`, `partial_base-01_top.png`).
  - A hue-p90 clause cannot fix this: 1-1 as built reads hue p90 0.045, against 0.038–0.044 for those partial coins (`huep90.log`).
  - This is acceptable only because no recipe term can draw a region-confined per-peg print: the only route is `dist` × `y`, and the lint refuses `dist`.
- **README honesty:**
  - Its blind-spot list (one peg, `tone`, "weaker hue coins (syn03…)", lightness coins on 1-4 and 2-3) is true but understated: on 1-4 a ΔE 0.04 coin is not "weak", and a print round a minority of pegs is not mentioned.
  - Its closing claim, "the guard covers the quiet, `dist` and peg-scale `disc` terms, which are every per-peg route the recipes have", is wrong (m8).
  - My 1x run fails syn03 on 2-2 by median (0.0075 against 0.006), where the README says it passes. That is borderline, not a contradiction.

**n3 (1-3's cactuar mouth): UNCHANGED.** Protan 0.099 at (469, 266) and (452, 300), 0.100 at (437, 262); deutan 0.116.

**n5 (2-5's corner fronds): UNCHANGED.** This remains the art supervisor's call.

**n6 (hot wall strips): PARTLY, as in round 5.** 2-5's narrow crimson strips at x 75–100 remain (`cmp10.png`). They are the first jewel, so no F7 figure looks at them.

**n7 (keep masks with no runtime form): UNCHANGED.** 1-5's inverted-product keepMask, `tone`, rim-fill and poly / not-poly still have no runtime form.

**n8 (2-3's dE/texture median over 1.2): UNCHANGED.** 1.56 / 3.37, worst (430, 420) at 3.52.

**n9 (F7's wall check missed 1-3): RESOLVED, as a reported figure.** The window figure on round 4's jewels against the shipped ones (`walls6.py`):

| Level | Round 4 jewel | Shipped |
|---|---|---|
| 1-1 | 5.77 | 0.73 |
| 1-3 | **6.50** (the full-height figure was blind at 1.32) | 0.42 |
| 2-3 | 5.69 | 1.54 |
| 2-4 | 6.50 | 0.42 |

It separates cleanly on all four. Why it is not gated is m9.

**n10 (1-1's lapis wall rail): UNCHANGED** (not done, as the context says).

**n11 (1-4's hollowed crimson sky): UNCHANGED.** dE/texture at (300, 128) is 2.49.

**n12 (1-4's tone discs on pegs): RESOLVED.**
- The two peg-centred discs are now one broad `["disc", 390, 455, 100, 40]` mul 0.82, reach 140.
- The lint covers `tone` now.
- The lower city reads as before, slightly darker under the colonnade, with no peg-scale shape (`c04_low.png`).

## New findings

### m8 (Minor, pipeline): `dress.lint` can be sidestepped by a small nudge, a feature `near` term or a glow, and the README says it cannot

**Evidence** (`bypass2.py` on the real 2-3, crop `bypass2_2-3.png`: shipped | nudged disc | glow | near term).

| Variant | Lint | Print rule (median / p90 / hue) | On screen |
|---|---|---|---|
| Round 5's disc `[512, 118, 34, −12]` moved 20 units to (515.5, 137.7): 13.0 from the bird's edge, so it passes the "centre within 12" test | passes | 0 / 0.0044 / 0.0055, passes | the same lilac coin enclosing (512, 118) as G4 |
| A one-point feature `near` keep mask at (512, 118), `["near", "spot", 22, 46]` | passes | passes | a mauve coin round that peg |
| A glow `{x: 512, y: 118, r: 22, k: 0.22}` | passes | passes | a faint pale halo |

- Glows are a runtime layer (`MoonfallGlow`) and three shipped recipes use them, at r 150–260.
- The runtime loader (`MoonfallSceneRecipeLoader`, main) applies no such guard: it accepts any disc and a `near` term at blur 0.
- The README, step 9: "the guard covers the quiet, `dist` and peg-scale `disc` terms, which are every per-peg route the recipes have." Glows and feature `near` terms are routes the recipes have, and the disc rule tests the disc's centre, not what it covers.
- **Why Minor:** no shipped recipe uses these (`lintaudit.py`), and G4's actual disc is refused. But the first thing an author will try when the lint refuses a disc on a peg is to move it a little, and that passes.

**Fix.**
- **Test coverage, not the centre:** refuse a `disc` (region, keep or tone) with reach r + |f| < 100 when any piece's edge lies within its reach (nearest edge < r + |f|).
  - On the shipped ten this passes: 2-5's lantern keeps sit 19.4 from an edge with reach 16; 2-2's disc sits 110 with reach 80; 1-4's tone is exempt.
  - It refuses G4 at any offset up to 46 units.
- **Glows:** refuse a glow with r < 100 that has a piece within r. The shipped glows (r ≥ 150) pass.
- **`near` and `poly` terms:** refuse those whose feature fits in a 100-unit box and lies within the term's reach of a piece.
- **Self-tests:** add the three variants above.
- **README:** replace "every per-peg route the recipes have" with the true list, and add two lines: a print round a minority of the pegs passes (the rule is a median), and on 1-4 hue coins up to ΔE 0.05 pass in any direction.

### m9 (Minor): 1-2's green water region has no wall fade; the new wall window flags it (6.43), and the README's reason for not gating is wrong

**Evidence.**
- `horizon-by-night.json` region `{"hex": "#30B888", "chroma": 0.22, "mask": [["y", 388, 406]]}` has no `x` fade, against the README's own lesson: "Fade every region within 60 units of the walls."
- The undressed painting's water is dark violet. On the cleared board, the green lies bright at both lower walls round a dark U where the pegs were: the layout's envelope, G18's defect (`c02_water.png`). It also shows in play: green lower corners on the composite.
- Its window figure is 6.43 (y 341), the highest in the set and inside round 4's defect range (5.69–6.50). The other shipped boards read 0.42–4.51.
- The README and the readability.py comment explain the absent limit as "highest where a painted feature such as 1-2's lit water meets a wall". The water's green is not painted; it is the jewel region.
- The same unfaded pattern, milder, is on 1-4 (4.51, sky region), 1-5 (3.67) and 2-2 (3.36).

**Fix** (`fade02.py`).
1. Add `["x", 75, 135], ["x", 725, 665]` to 1-2's region. The window falls to 1.04, but F7's share falls to 0.147 (fails 0.15).
   - At chroma 0.26: share 0.153, window 1.08.
   - At chroma 0.30: share 0.159, window 1.11.
   - Re-measure F9 on the water row's candidates after the change; I did not.
2. The same fade takes 1-4 to 1.91 (share 0.22), 1-5 to 1.75 (0.153) and 2-2 to 0.46 (0.167).
3. Then gate the window at about 5: it fails all four of round 4's strips and passes everything else shipped.
4. Correct the README's reason.

### n13 (Nit): the window figure's reported y is the window's top edge, unlabelled

`(y0 + 82) // 2` in `jewels()` is the top of a 100-unit window in board units, so 1-2's "341" means y 341–441. Say so in the README or report the centre.

### n14 (Nit): 2-2's negative-feather disc needs `invert` in conversion; my round-5 formula note was wrong

- Main's loader refuses a disc whose feather is under 0 (`MoonfallSceneRecipeLoader`: "disc's r must be above 0 and its feather at least 0").
- 2-2's region term `["disc", 292, 128, 60, −20]` (and round 5's 2-3 disc, which I said "converts exactly") must therefore be written `disc [292, 128, 60, 20]` with `invert: true`. That is exact: 1 − Smooth(80, 40, d) = Smooth(40, 80, d).
- Record it in the README's converter notes.

## Formula and runtime checks

- **2-3's region is now runtime-expressible as written:** Lum (blur 4) × X × X, plus the quiet's Near (invert, scale 0.6, blur 40). That is 4 of the format's 6 terms.
- **2-5's lantern keeps** are two non-overlapping Disc terms, each inverted on the palette's `where`. Exact, because the runtime's per-term inversion equals 1 − union when the discs do not overlap.
- **2-2's inverted disc:** exact via `invert` (n14).
- **No runtime form:** 1-4's new tone (`tone`), 1-5's inverted-product keep, rim-fill, `poly` / `not-poly` and feature `near`. Unchanged; the converter does not exist.
- **The lint and the hue clause are pipeline-only.** The runtime loader has no structural guard. The README's planned converter gate (OKLab maximum or p99.9 ≤ about 0.02 against the dressed scene) is the only thing standing between a hand-edited runtime recipe and a coin.
- **`QUIET_BLUR_MIN` 40** is enforced by the lint, NaN included, and is self-tested.

## Unverified

- **No `MoonfallRender` output:** the converter does not exist, so the shipped look is unverified wherever the pipeline uses terms with no runtime form (above).
- **m8's and m9's fixes are scratch measurements, not builds:** the coverage lint is unimplemented, and 1-2's F9 with the wall fade is unmeasured.
- **In-game rendering, per-piece veil sprites and the real 640×480 window** are covered only by my 0.8x Lanczos proxy.
- **Colour-vision simulation:** Machado at severity 1 only. Tritan and anomalous trichromacy were not checked.
- **Peg sprites under CVD:** blue–purple deutan 0.018 and green–orange protan 0.029 are unchanged since round 4. They are main's art, outside this set, and rely on the glyphs.
- **Ramp, holdouts and 2-5's low-orange share** are the game designer's; I did not re-play them.
