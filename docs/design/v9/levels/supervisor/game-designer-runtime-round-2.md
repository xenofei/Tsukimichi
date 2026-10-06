# Game designer, levels runtime round 2

6 October 2026. Worktree `agent-a570460c913ca1c79` at `fa200dc0`. Everything I made is in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/levels-b/review-game-designer/r2/`:
- bare scenes and star overlays: `scene-NN.png`, `stars-NN.png`, `kept.log`;
- motion frames and maps: `mot/` (8 frames per level at 0 to 8.3 s, and `amb-NN.png`);
- 1-1's dense glint series: `g1/`;
- Fever renders: `fever-03/07/09.png`;
- crops: `water02.png`, `hulls.png`, `tw09pair.png`, `zfr.png`, `z09corners.png`;
- the Ace check: `aces.fsx`.

**Process note.** I ran no `dotnet build` and no `mfl.py` command. I made one slip: `MoonfallRender.dll --help` treats its first argument as the output path, so it wrote a 1.8 MB PNG named `--help` into the worktree root. I deleted it at once, and `git status` is clean. The Ace check ran the renderer's own built `Tsukimichi.Core.dll` through `dotnet fsi` from scratch, which writes nothing to the worktree.

## 1. Verdicts

| Level | Verdict | Why |
|---|---|---|
| base-01 1-1 Road to Horizon | APPROVE | The road glint works and gives 1-1 steady motion: a soft spark, lift 60–145 levels, moving along the roads at 30 u/s. Minor g1: it winks on and off as it passes pegs. |
| base-02 1-2 Horizon by Night | APPROVE | 36 of 36 stars are in the sky and none is on the radio tower or the water tower. The green sea's hue is back. Minor w1: the lower half of the sea is 25–40% darker than the approved composite. |
| base-03 1-3 The Cactuar | APPROVE | **M1 resolved.** 0 of 40 stars sit on the cactuar or the moon. Mist now moves in 47% of its band. Fever is still unrendered (n4). |
| base-04 1-4 The Gilded Dome | APPROVE | No stars, as asked. Its 7 lamps flicker and it has beams and dust. Reads at both sizes. |
| base-05 1-5 The Crystal's Call | APPROVE | **n2 resolved:** the halo is `#5FD8E8` from the recipe. 30 of 30 stars are off the crystal. |
| base-06 2-1 Limsa Across the Water | APPROVE | The green mist is restored: band by band it is within ±3 luma and about 5° of hue of the approved composite. The mist moves in 83% of its band. |
| base-07 2-2 Moonpath on the Bay | APPROVE | No star is on or near the moon. The swollen-moon Fever is intact. Nit: the star field is lopsided. |
| base-08 2-3 The Kraken's Sea | APPROVE | Its richer green jewel reads at 640. Band colours match the approved composite within 1–4 luma. |
| base-09 2-4 The Ferry Under Sail | APPROVE | **M1 resolved:** no stars on the sails, rigging or moon. **M2 resolved** by naming no Fever moon. Its mist has cover (61% of the band moves, against 12.7% in round 1). The port lights flicker. Nits: three stars sit on the bunting ropes, and the brief's "two lanterns" do not exist at runtime. |
| base-10 2-5 Twin Lanterns | APPROVE | **m5 resolved:** both lantern windows flicker, centred on the windows. The right star field mirrors the left. Ramp 20.65 (pooled), finale gap 3.47 ± 0.41 against stage 1's 3.23. |

## 2. Findings

### Round-1 findings, status

- **M1 (twinkles on the subject): resolved.** I read every star's place from `--scene-only 1 --no-grain` (`kept.log`) and drew them on the scene (`stars-NN.png`).
  - 1-3: 40 stars, all in the sky polygon. None is in the cactuar's box (x 340–560, y 210–330) or within 51 of the moon at (150, 108).
  - 2-4: 36 stars, none on the ship, none within 40 of the moon at (150, 104).
  - 1-2: 36, none on the towers. 2-2: 30, none within 36 of the moon. 2-5: 24, none on the gate or the lanterns. 1-5: 30, none on the crystal.
  - Every level keeps the count it asks for: 36, 40, 30, 30, 36 and 24.
- **M2 (2-4's Fever moon dropped): resolved.** `kept` shows `moon:false, moonDeclared:false`. In `fever-09.png`, Fever zooms in and shows its banner, and the painted moon stays as painted. There is less spectacle than 2-2's swell, but nothing is silently lost.
- **m3 (motion barely visible): resolved for mist, glint and lights; the beams are calm and only just visible.**
  - **Mist (alpha 0.11).** The share of each band's pixels that changes by 2 levels or more over 8.3 s:

    | Level | Share ≥ 2 | 90th percentile |
    |---|---|---|
    | 1-3 | 47% | 7–10 levels across the five |
    | 2-1 | 83% | |
    | 2-2 | 64% | |
    | 2-4 | 61% | |
    | 2-5 | 55% | |

    Round 1 had 2-4 at 12.7% and 1–3 levels. The mist now reads as moving.
  - **Beams (±30% over 9.5 s).** Over a full breath, excluding pegs, the bucket and stars, beam pixels change by a median of 2.0–2.7 levels and a 90th percentile of 2.3–3.7. The 99th percentile is 3.0–4.7, or 8 on 1-4 and 2-5, where lamps fall in the area. The peak and trough frames (t 2.4 and 7.1, `mot/beampairs.png`) differ visibly side by side, but only just. The shafts themselves are faint (about 4–6 levels), so ±30% of them stays near the threshold.
    - This is what I asked for, and it is calm. I do not ask for more (Nit; optional ±40% on 1-3 and 2-2, where the shaft comes from the moon).
  - **Lamps.** The flicker is now 1.3 and 1.9 Hz. The halo means change by 3.4–8 levels, with cores held near 180–195. It is calm and visible.
- **m5 (2-5's lanterns): resolved.** Lights at (196, 240) and (604, 240) sit exactly on the two lantern windows (`stars-10.png`) and flicker (halo range 3.4–4.5 levels). The reflections at y 460 were dropped for clearance, which is acceptable.
- **m6 (Aces): resolved.** I re-ran `MoonfallPlayability.Check(level, 576)` against the built Core:

  | Level | Ace (k) | Games reaching it |
  |---|---|---|
  | 1-1 | 300 | 19.6% |
  | 1-2 | 280 | 20.0% |
  | 1-3 | 260 | 18.8% |
  | 1-4 | 250 | 21.5% |
  | 1-5 | 250 | 19.6% |
  | 2-1 | 270 | 20.7% |
  | 2-2 | 280 | 21.4% |
  | 2-3 | 260 | 18.9% |
  | 2-4 | 240 | 20.5% |
  | 2-5 | 260 | 21.2% |

  `Suggest` returns the shipped value on all ten. The share is a fifth, against my suggested 15%, which is fine.
- **m7 (dead challenges): open, as recorded.** The open item in `docs/design/v9/moonfall-modes.md` is correct. Nothing ships broken, because challenges are sealed in 1.23.0.
- **n1 (Fever's flat disc): not done**, as the brief says. It is still a Nit.
- **n2 (spark halo): resolved.** The halo takes `HaloColour` from the recipe's `"halo": "#5FD8E8"`.
- **n3 (ramp): resolved.**
  - Stage 2's pooled figures are 28.12, 26.75, 25.55, 24.12 and 20.65. Its steps are 1.37, 1.20, 1.43 and 3.47.
  - Stage 1's steps are 3.28, 2.17, 1.48 and 3.23.
  - So the two finales now match (3.47 against 3.23), down from round 1's 4.52.
- **n4 (no Fever render on 1-3): still open.** `fever-03.png` stops at 1 orange left, with no zoom. 1-3's Fever moon is still unverified by render, although `kept` reports `moon:true`.

### New findings

**w1, Minor (1-2): the green sea is back in hue, but its lower half is darker than approved.**
- **Evidence.** Median colour across x 120–680 in 20-unit bands, approved composite against shipped `composites/base-02.png`:

  | Band (y) | Approved luma | Shipped luma | Approved G | Shipped G |
  |---|---|---|---|---|
  | 420–480 | 26 | 30 | | |
  | 500 | 24 | 18 | 33 | 24 |
  | 520 | 22 | 16 | | |
  | 540 | 22 | 14 | | |
  | 560 | 22 | 13 | 30 | 18 |

  - From y 420 to 480 the two match or the shipped is slightly lighter.
  - The engine's bare scene and 1280 render match the shipped composite (`#021501` against approved `#001F01` at y 490–570).
  - Side by side (`water02.png`), the approved lower sea is vivid green and the engine's is dark olive.
  - The recipe's tone (`horizon-by-night.json`: mask `y 388→406`, `mul 1.2`) lifts the whole sea evenly, so it does not undo the fall towards the bottom.
- **Fix.** Add a second tone ramped over the lower sea, from about 1.0 at y 490 to about 1.5–1.6 at y 570, aiming at approved luma 22 (G about 30). Re-check F7 and the print check.

**g1, Minor (1-1): the road glint winks in and out as it passes pegs.**
- **Evidence.**
  - Both tracks run under pegs: the closest clearance is −7.9 and −7.2, and the median is 6.0 and 7.7.
  - The glint shows only beyond 6.46 units and is full beyond 9.46. Fully visible: 36% and 38% of each track's length.
  - The visible runs are short: 16, 6, 13, 31, 17, 12, 15 units on track 1, and 15, 2, 12, 35, 21, 37, 7, 16 on track 2. At 30 u/s each run lasts 0.1–1.2 s.
  - The fade-in is 3 units, about 0.1 s.
  - In 0.5 s samples (`g1/`), the first pass shows at 10, 11.5, 13 and 14.5 s and is hidden in between.
  - When it shows it looks good: a soft warm spark on the dashed road (`g1/gl.png`).
- **Fix.** Pick one:
  - widen `GlintClear`'s ramp to about 10 units, so it dims over about 0.3 s;
  - or let a pass skip, rather than blink through, segments shorter than about 20 units;
  - or slow it to about 20 u/s.

  The keep-out stays as it is.

**s1, Nit (2-4, 1-2, 1-5): a few stars sit on dark framing lines.**
- 2-4: three stars sit on the top-left bunting rope or its end, at (168, 46), (166, 54) and (82, 120). In play they read as small lights strung on the rope (`tw09pair.png`, `z09corners.png`).
- 1-2: (684, 136) and (700, 126) sit inside the top-right palm frond, in the gaps between leaflets (`zfr.png`).
- 1-5: (146, 136) sits at a frond's edge.
- **Fix (optional).** Add `not-poly` terms for the rope and frond silhouettes, or reject star candidates where the framing's cover is above about 0.1 within 3 units.

**s2, Nit (2-2): the star field is lopsided.** 23 of 30 stars are right of x 450, and only 2 are left of the moon (`stars-07.png`). The moon's glow suppresses local maxima on the left. It still reads as a night sky. Optional fix: weight the pick by thirds of the sky.

**p1, Nit (2-4): the port lights.**
- The pipeline names 5 points. The two "lanterns", (246, 372) and (612, 362), sit exactly on the pegs at those coordinates, so the dress's clearance rule drops them. Only 3 portholes flicker (`kept flickers:3`). The brief's "and the two lanterns" is wrong for the runtime.
- In the engine, the 3 flickering portholes are brighter (peak 183) than the 3 baked ones (114), so the six read as alternately lit. In the approved composite all six were equal (`hulls.png`).
- It looks deliberate and fine. Correct the record, or dim the runtime trio to match.

**a1, Nit (2-5): the mirror is one pair short of exact.** The left field has (130, 300) and the right (680, 300); the mirror of 130 is 670. This pair is older than this round and is not a corner star, so the corner stars are unaffected. Fix only if the layout is touched again.

**Observation (1-5, no action):** the aurora is unchanged, as the brief says. At x 560–720, y 40–160, it is 15–30% darker and teal rather than green against approved (luma 21/34/40 against 15/24/32). This follows the owner's lightness-kept decision.

## 3. Reading at 1280 × 800 and 640 × 480

All ten read as their subjects at both sizes (`g640.jpg`, `avs-NN.jpg`):
- 1-1: the chart.
- 1-2: the skyline over a green sea.
- 1-3: the cactuar; its eyes and mouth show as pegs clear.
- 1-4: the dome.
- 1-5: the crystal.
- 2-1: Limsa over green mist.
- 2-2: the moonpath.
- 2-3: the kraken chart.
- 2-4: the ferry.
- 2-5: the gate and the twin lanterns. The gate is the faintest at 640 but still reads.

The engine matches the shipped-form composites throughout.

## 4. Not checked, or not verified

- **Motion was judged from rendered frames, not seen live.** That covers the beams, mist, glint, flicker and twinkles: 8 samples over 8.3 s per level, plus 21 for 1-1's glint. Whether the beams' 2–4 level swell is noticeable in the game is unverified.
- **1-3's Fever moon** (n4): the renderer still misses the last orange.
- **The converter's gate** I did not re-run. Its figures come from `gate-r2.log`.
- **Story-safe variants (`--hide-zone`), peg marks on bricks and the F6 tests** belong to the other reviewers and I did not review them.
- **Twinkle lifts** at the 2-4 rope stars come from sparse samples (one lift of 27 levels measured). The placement is exact; the brightness is approximate.
- **In the game itself:** the GPU path, frame timing, other window sizes and human feel (Ace reach for people, 2-5's finale) are untested.

