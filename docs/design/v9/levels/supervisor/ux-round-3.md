# UX/UI specialist, levels round 3

**Reviewed:** the ten levels base-01 to base-10 at `35cd98b5`, in worktree `agent-a570460c913ca1c79`. I read `round3-context.md`, `round2-context.md`, the three round-2 reports, the README, `readability.py` (the `dress_print` gate and its self-tests) and `dress.py` (the low-frequency quiet). I also read main's runtime palette code (`MoonfallSceneBuilder.Palette` and `MoonfallFilters.Blur`, via `git show`) to check how the quiet will convert. I looked at the composites, the dressed scenes, and my own cleared boards with no veil (the runtime's cleared board).

**OVERALL: REVISE.** One Major is left, and it is in the pipeline, not the layouts:
- **G1 (per-peg halos) is resolved in the pipeline.** All ten runtime cleared boards are free of coins, and every level is within my round-2 target.
- **G2 (the ghost gate) is mostly resolved.** The new print gate catches per-peg coins and is never vacuous.
- **New Major, G3.** The fix depends on two things main's scene format cannot express: a blur applied after the smoothstep, and a gain.
  - A literal conversion (`quiet` mapped onto the runtime's `near` term) brings the coins back on every board.
  - The closest form the runtime can draw puts F9 below the floor on 1-3, 1-5, 2-2 and 2-4.
  - So what passes here is not what the game will draw.

On the pipeline's own boards I would approve all ten with Minors. Two Minors stand out:
- 1-5's F9 is now exactly at the floor (min 0.062), a regression this round.
- The print gate has blind spots, and its thresholds are not calibrated on the pilots (4 of the 6 approved pilots would fail them).

## Method

**Ran (read-only):**
- `mfl.py selftest` (all ok), `stage 1` and `stage 2` (all PASS).
- I did not run `build`, and `git status` is clean.
- The committed composites are hash-equal to `build/composites`.
- Scripts are in `scratchpad/ux3/`:
  - `measure3.py`: my round-2 measure, now with orange-able bricks.
  - `ghost_snr3.py`, `print_calib.py`, `pilot_print_framing.py`, `fool_print.py`.
  - `rtproxy.py` and `rt_f9.py`: the runtime proxy.
  - `f9list.py`, `cvdcrop.py`, `sheet.py`.

**F6.**
- Faces were measured on the composite at each scale. The ring is the 90th percentile 2–9 units out, plus the 99th percentile for glints.
- Every place a kind can be dealt was covered, with movers at 24 points, at 1x and 0.8x (Lanczos).

**Colour vision.**
- Machado 2009 at severity 1, protan and deutan.
- Measure: the a/b distance from each kind's core to the ground 12–20 px round it.
- Covered every candidate, movers at 24 points, and every orange-able brick (ground 3–11 px outside its edge, in 7 windows along it).
- Also the pipeline's own `protan_seps` on its orange views, to list the worst places.

**Prints and ghosts.** I rebuilt piece-free 2x boards from the committed dressed scenes in three versions:
- **baked:** veil and dress;
- **no-veil:** the runtime's cleared board;
- **no-quiet:** quiet removed, no veil.

On these I ran:
- my **dE/texture** measure (round 2), on the 1x composites, the no-veil boards and the dressed scenes, for the levels and the six pilots;
- `readability.dress_print` on the six pilots (dressed `rich2/scenes` against undressed `rich/scenes`), to calibrate its thresholds;
- 11 synthetic cases against the print gate.

**Runtime proxy (`rtproxy.py`).** I re-dressed each level with only the quiet changed, to what main's builder can draw:
- the clearance blurred by at most 40 units **before** the smoothstep;
- `invert` and `scale 0.6` on each region;
- the keep reduction through the palette's `where` mask, which main does have (`MoonfallJewelGrade.Mask`).

Two variants: blur 0 (literal) and blur 40 (the most the runtime allows). On each I measured F9, the print gate and dE/texture.

**Also:** bright peg-sized spots (luma > 0.40, 3–26 px), and lamp posts. Viewed directly:
- all ten no-veil cleared boards (contact sheet `ux3/nv_all.png`);
- 2x crops of 1-2's mesa, 1-4's sky, 1-5's aurora, 2-2's dusk, 2-3's band, 2-4's sea and sails, and 2-5's fronds and water;
- protan and deutan crops of 1-2, 1-4, 1-5 and 2-5.

## Verdicts

dE/texture is on the no-veil cleared board, given as median / p90. The round-2 median follows in brackets. Target: at most 1.2 median and 2.0 p90. The pilots' dressed scenes measure 0.19–0.60 median and 0.55–1.65 p90.

| Level | F6 worst, pipeline / mine | Protan orange p10, pipeline / mine; min (all candidates, movers and bricks) | Deutan orange min | Print gate, median / p90 | dE/texture, no-veil (round 2) | Verdict |
|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 0.295 / 0.340 | 0.132 / 0.133; 0.131 | 0.141 | 0.0074 / 0.0224 | 0.27 / 0.27, n 1 (1.96) | APPROVE |
| 1-2 Horizon by Night | 0.318 / 0.364 | 0.137 / 0.137; 0.126 | 0.144 | 0.0052 / 0.0207 | 0.35 / 0.76 (2.01) | APPROVE |
| 1-3 The Cactuar | 0.282 / 0.317 | 0.118 / 0.118; 0.099 (mouth and both eyes) | 0.116 | 0.0072 / 0.0171 | 0.33 / 0.52 (1.35) | APPROVE (n3) |
| 1-4 The Gilded Dome | **0.202** / 0.241 | 0.117 / 0.116; 0.107 | 0.106 | 0.0056 / 0.0117 | 0.66 / 1.49 (0.91) | APPROVE (n1) |
| 1-5 The Crystal's Call | 0.365 / 0.387 | **0.101 / 0.101; 0.062** at (540, 150) | **0.083** | 0.0056 / 0.0119 | 1.00 / 1.39 (2.59) | APPROVE on the pipeline board, with m3; hit hardest by G3 |
| 2-1 Limsa Across the Water | 0.316 / 0.357 | 0.140 / 0.140; 0.134 | 0.148 | 0.0036 / 0.0126 | 0.44 / 0.95 (0.46) | APPROVE |
| 2-2 Moonpath on the Bay | 0.338 / 0.370 | 0.123 / 0.125; 0.122 (crests included) | 0.124 | 0.0050 / 0.0129 | 0.51 / 0.68 (2.04) | APPROVE |
| 2-3 The Kraken's Sea | 0.308 / 0.347 | 0.117 / 0.122; 0.113 | 0.132 | 0.0045 / 0.0143 | 1.01 / 3.36* (2.94) | APPROVE |
| 2-4 The Ferry Under Sail | 0.284 / 0.283 | 0.139 / 0.140; 0.130 | 0.129 | 0.0024 / 0.0089 | 0.38 / 1.00 (1.01; p90 was 3.96) | APPROVE |
| 2-5 Twin Lanterns | 0.387 / 0.409 | 0.113 / 0.112; 0.111 | 0.112 | 0.0075 / 0.0147 | 0.74 / 1.11 (2.02) | APPROVE (n5) |
| **Set** | | | | | | **REVISE (G3)** |

\* 2-3's p90 comes from the painting, not the dress. The undressed graded scene already measures 2.95–2.99 at the same two pegs, (430, 420) and (369, 339).

**What holds everywhere:**
- **F6.** Every kind clears 0.20 at 1x and 0.8x at its worst placement, movers included.
  - The tightest place is still 1-4's pale lower dome (436, 476): 0.202 on the pipeline, 0.241 on mine.
  - Glint values (99th percentile) below 0.18 occur only at known places: 1-3's wall gilt (0.100), 1-4's dome (0.155) and 1-1's route dashes (0.178).
  - 1-2 shows 0.123 at (84, 334), but that is my ring catching the frame's gilt for a peg 1 unit off the wall. In the crop, the peg reads cleanly.
- **Nothing new reads as a peg.**
  - Bright spots are as in round 2: lamp posts at 6–7 px and 0.80 at rim height; 1-4's town lamps; 2-5's fireflies (3–4 px); 1-1's route marks; the bucket lantern.
  - The new spots are a 3 px spark on 1-3's cactuar belly (469, 334) and nothing else.
  - 2-4's darker sails cut its 0.40-luma blobs from 55 to 5.
  - The new framing does not read as pegs: 2-2's festoons sit under the top chrome, 2-4's outcrops are in the lower corners, and 2-5's longer fronds are thin, dim, and keep their clearance.
- **Cleared boards look finished.** All ten runtime cleared boards are free of discs and coins (`ux3/nv_all.png`).
  - What the quiet leaves is a soft, layout-wide easing of the second jewel. It reads as natural tonal variation: a cooler band on 1-2's mesa, 2-2's horizon and 2-4's sea; a mauve haze beside 2-5's arch; a teal heart in 1-5's aurora.
  - 2-3's centre is a desaturated slate sea. It reads as deep water, not as a stain.

## Round-2 findings

**G1 (Major: the jewel's quiet prints a hue halo round every peg): RESOLVED in the pipeline.**

| Level | No-veil dE/texture median, round 2 → round 3 |
|---|---|
| 1-5 | 2.59 → 1.00 |
| 2-2 | 2.04 → 0.51 |
| 2-5 | 2.02 → 0.74 |
| 1-2 | 2.01 → 0.35 |
| 1-4 | 0.91 → 0.66 (p90 2.49 → 1.49) |
| 2-4 | p90 3.96 → 1.00 |

- **Visually:**
  - 1-5's polka-dot field is gone (`ux3/c05_nvbk.png`: no-veil on the left, baked on the right; the baked dots are the veil, which fades per piece at runtime).
  - 2-2's sky shows no coins (`ux3/c07_sky_s.png`).
  - 2-4's sea has no rectangle edge (`ux3/c09_sea_s.png`).
- **The cause is fixed:** the quiet is now a Gaussian of sigma 40 applied after the smoothstep, with gain 1.6.
- **But it does not survive conversion** to the runtime as specified; see G3.

**G2 (Major, pipeline: the ghost gate cannot catch the defect): MOSTLY RESOLVED; the residue is Minor m4.**
- **What is fixed:**
  - The gate measures the dress's own change on the right board (no pieces, no veil, so the painting cancels).
  - It takes every still peg, so it is never vacuous: 46–76 pegs per board.
  - It fails my round-2 hue-disc case and the critic's clustered case (both self-tested).
- **What is left:** blind shapes, and thresholds stricter than the approved pilots (m4).

**m1 (2-3's merged band reads as grey soot in play): RESOLVED.**
- At veil 0.34, the quietened band reads as deep slate-blue sea round the clusters, not as soot (`ux3/c08_band_s.png`, top).
- Cleared, it reads as calm water (bottom of the same crop).

**m2 (2-4: a rectangle printed on the sea, and the sails' F6): RESOLVED.**
- The sails are darker: purple margin 0.228 → 0.283 on mine, 0.230 → 0.284 on the pipeline.
- At 0.8x, the pegs on the sails read cleanly.
- The mover rows leave a soft plum band with no edge (`ux3/c09_sea_s.png`).

**n1 (1-4's pale lower dome): UNCHANGED, Nit.**
- The pipeline's F6 is 0.202 at (436, 476), 0.002 above the floor; mine is 0.241.
- Any later change to the grade will trip the gate here.

**n2 (F9 skipped orange-able bricks): RESOLVED.** Bricks are sampled along their length. 2-2's two crests measure ≥ 0.122 on mine.

**n3 (1-3, the cactuar's mouth): UNCHANGED, Nit.**
- The mouth (452, 300) and both eyes, (437, 262) and (469, 266), are at 0.099 protan.
- These are the only candidates under 0.10 apart from 1-5's.

**n4 (README lesson): RESOLVED.**

**Round-1 n3 (kind against kind for colour-blind players): UNCHANGED** (engine palette): protan green–orange 0.029, deutan blue–purple 0.018.

## New findings

### G3 (Major, pipeline ↔ runtime): the quiet that resolves G1 cannot be drawn by main's scene format, and its closest expressible form breaks F9 on four levels

**Evidence.**
- **The runtime's form.** Main's `near` term blurs the clearance distance (`blur` at most 40 units) **before** the smoothstep, and has no gain:
  - `MoonfallSceneBuilder.Palette`: `source = Blur(ctx.ClearanceAtS, t.Blur * S)`, then `Smooth(a0, a1, source)`.
  - The pipeline instead blurs the mask **after** the smoothstep by `max(a, 40)` and multiplies by 1.6 (`dress.py`).
  - Blurring the distance first gives a lone or sparse peg no quiet at all, because the blurred distance round it is about 40, beyond `a`.
- **A literal conversion, as the README plans (`quiet` onto a `near` term), reprints the coins** (`ux3/rt_base-05.png` and `ux3/rt_base-07.png`, middle panels):
  - the print gate fails on all ten: median 0.012–0.077 against 0.010;
  - 1-5's dE/texture is 3.30 at the median and 8.69 at p90;
  - 2-2's coins are back in the rose sky.
- **The closest expressible form** (blur 40 on `near`, invert and scale 0.6 on each region, and the keep reduction through `palette.where`) avoids the coins, but:

  | Level | F9 protan p10 (min), pipeline → runtime proxy | Print gate, runtime proxy (gate 0.010 / 0.026) |
  |---|---|---|
  | 1-5 | 0.101 → **0.072** (0.033 at (540, 150)) | passes |
  | 2-4 | 0.139 → **0.091** | passes |
  | 2-2 | 0.123 → **0.098** | passes |
  | 1-3 | 0.118 → **0.099** | **median 0.0115** |
  | 1-1 | passes | **median 0.0114** |
  | 1-2 | passes | **p90 0.032** |
  | 2-5 | 0.113 → 0.107 | **median 0.0113** |

  1-4 (0.108), 2-1 (0.140) and 2-3 (0.109) hold F9.
- **Why it matters.** The binding rule ("quiet by low frequency, never per peg") and F9 are about the game. Every F9 and print figure in the ten reports describes a dress the game cannot draw.
- **Why now.** This round raised every second-jewel region's chroma by 40–60%, and leaned harder on the post-blur and gain to hold F9:

  | Level | Region chroma, round 2 → round 3 |
  |---|---|
  | 1-5 | 0.18 → 0.26 |
  | 1-4 | 0.075 → 0.13 |
  | 1-2 | 0.15 → 0.22 |
  | 2-4 | 0.08 → 0.125 |
  | 2-5 | 0.08 → 0.12 |

**Fix (pick one, then measure on the form that ships):**
1. **Make the runtime draw what the pipeline measures (preferred).**
   - Add two options to main's `near` mask term, `spread` and `gain`. `spread` is a Gaussian applied to the mask after the smoothstep, up to about 60 units. `gain` multiplies the result, clamped to 1.
   - The converter then writes each region as `near [a, b]` with `spread: max(a, 40)`, `gain: 1.6`, `invert` and `scale 0.6`, and writes `palette.where` the same way with `scale 0.5`.
   - Add a test: the converted 1-5 and 2-2, built by `MoonfallRender`, match `build/dressed` within an OKLab ΔE of about 0.01.
2. **Or move the pipeline to the runtime's form.**
   - Replace the post-blur and gain in `dress.QUIET_*` with a blur of the clearance (at most 40), then the smoothstep.
   - Re-tune the palettes so F9 holds. The proxy shows 1-3, 1-5, 2-2 and 2-4 need work.

Either way, until the converter exists, readcheck should measure F9 and the print on a dress built with the runtime's mask semantics. That makes a mismatch fail at build time.

### m3 (Minor): 1-5's protan separation fell to the floor this round

**Evidence.**
- Protan orange: p10 0.141 → **0.101**, the exact pilot floor (the pipeline also reports 0.101). The minimum fell 0.129 → **0.062** at (540, 150), the lowest place in the set.
- 11 of 29 candidates are under 0.12. 2 are under 0.10: (540, 150) and (440, 244), the latter at 0.099.
- Deutan minimum: 0.150 → 0.083.
- **The cause:** the aurora's region moved from aquamarine #30C0A0 at chroma 0.18 to green #40C878 at 0.26. Under protanopia and deuteranopia the aurora turns the same olive as the oranges, so the pegs separate by value alone (`ux3/cvd05_s.png`: normal, protan, deutan).
- The runtime proxy takes it to 0.072 (G3).

**Fix.** Any one of these, then re-check F7's share (the reason the chroma was raised):
- Move the aurora's hue back toward aquamarine or teal (about 190–200°), where the candidates sit.
- Or add a `keepMask` that lowers the region's chroma to about 0.16 over the upper-right candidates (x > 480, y < 280).
- Or move the (540, 150) candidate off the aurora's core.

Target: p10 ≥ 0.12 and min ≥ 0.10, on the pipeline board and on the runtime form.

### m4 (Minor, pipeline): the print gate has blind shapes, and its thresholds are stricter than the approved pilots

**Evidence, blind shapes** (`ux3/fool_print.py`, on a flat rose sky).

Every print below has the same ΔE, 0.050, as the coin case, which fails at 0.050. All of these pass with median ≤ 0.0015:
- a ring 13–19 units out, between the near and mid bands;
- a crisp disc 40 units out, so both bands fall inside it;
- a half-disc or a quarter-disc under each peg (3 or fewer of 8 sectors);
- a crisp blue stain round a cluster, edge 40 → 30 units out (0.0015) or 60 → 45 units out (0.0000);
- coins along a mover's path (movers are skipped);
- a band round a long brick (bricks are skipped).

The pipeline's present dress draws none of these, and I checked all ten cleared boards by eye. But the runtime's blur-before-smoothstep is exactly the kind that makes crisp-edged cluster stains, so the gate would not catch a G3 conversion that goes wrong in that way.

**Evidence, calibration.** The thresholds (median 0.010, p90 0.026) sit between the levels' old and new values. They were not set from the pilots. On the six approved pilots, `dress_print` gives:

| Pilot | Median | p90 | Notes |
|---|---|---|---|
| base-p1 | 0.0062 | 0.0243 | |
| base-p2 | **0.0102** | **0.0375** | worst 0.100 at its fir boughs; with framing excluded, 0.0090 / 0.0354 |
| base-p3 | **0.0120** | **0.0342** | its wood carried the very per-peg quiet `[24, 12]` this gate targets |
| exp-p1 | 0.0095 | 0.0185 | |
| exp-p2 | 0.0094 | **0.0285** | |
| exp-p3 | **0.0216** | **0.0469** | |

- **4 of the 6 approved pilots fail** the p90 threshold, and 3 of 6 fail the median.
- On textured game art the texture hides these prints: the pilots' composites measure dE/texture of 1.11 or less.
- So the gate is right for flat paintings, but it will refuse stage 3+ boards built like the pilots.
- 1-1 (p90 0.0224) and 1-2 (0.0207) already sit near its edge.

**Fix.**
1. Add the two blind shapes to the self-test, a ring at 13–19 units and a crisp disc at 40 units. Catch them with a second, wider pair of bands: 5–20 units against 45–70 units.
2. Sample movers at their home position and along their path, and orange-able bricks along their middle line.
3. Record the pilots' values in the README.
4. For game-art boards, pass a peg whose print divided by the local OKLab texture is about 1.2 or less, so the gate measures visibility rather than absolute ΔE.

### n5 (Nit): 2-5's lower-corner fronds read as stiff ladders

**Evidence.** The two fronds rising from the lower corners have straight, evenly spaced leaflets (`ux3/c10_lowfronds_s.png`). They read as ladders or fishbones rather than willow. They are dark, keep their clearance and do not clutter: no readability effect.

**Fix.** Optionally, give them more droop and uneven leaflet lengths. This is the art supervisor's call.

## Unverified

- **The runtime proxy is my model, not `MoonfallRender`:**
  - I changed only the quiet and kept every other layer of the pipeline's dress (`tone`, rim-fill masks, `poly` and `not-poly` masks), which also need converting.
  - I assumed main's `ClearanceAtS` is the same uncapped distance field as `piece_distance`.
  - I assumed `palette.where` blends exactly as the pipeline's `mask` does.
  - The direction of G3 is solid (I read the code), but the exact numbers should be re-measured on `tools/Tsukimichi.MoonfallRender` once the converter exists.
- **Per-piece veil sprites.** The in-play look with the runtime's per-piece veil sprites instead of the baked, merged veil.
- **In-game.** Anything in the game itself, and the 640×480 window beyond my 0.8x Lanczos proxy.
