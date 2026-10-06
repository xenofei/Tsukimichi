# UX/UI specialist, runtime round 1: the ten levels in the engine

**Reviewed:** worktree `agent-a570460c913ca1c79`, HEAD `c4e9c3db`. I worked read-only. `git status` shows nothing of mine; the one untracked file, `docs/design/v9/levels/supervisor/game-designer-runtime-round-1.md`, is the game designer's.

**OVERALL: APPROVE all ten levels.** There is one set-wide Major, **M1: the colour-blind peg marks are never drawn on bricks.** It is an engine fix, not a change to any level file, and it should land before 1.23.0 ships.

What holds in the engine:
- **Same boards as approved.** Readability in the engine matches the approved boards at both window sizes.
- **The jewel (decision 1):** keeping lightness leaves F6 and the print unchanged or better.
  - It costs at most 0.008 of protan orange separation (1-2).
  - It takes 2-3's F7 to its limits (m1).
- **Motion:** nothing moves near a piece.
- **Cleared boards:** they are cleaner than the pipeline's, because the veil goes with each piece.

The coordinator asked me to save this report as `review-ux/report.md`. The harness refused the write ("Subagents should return findings as text"), so this message is the only copy.

## 1. Verdicts

The columns, all measured on the engine's own renders:
- **F6:** worst kind's margin over the ring p90 at 1280 / 640. The F6 test's own worst is in brackets.
- **Bricks:** worst margin with a purple brick's face, measured on the renders. No test covers bricks.
- **Protan (F9):** orange separation, p10 / minimum at 1280. 640 is within 0.001.
- **F7:** mean chroma and the jewels' hue apart, approved build → engine.

| Level | F6 | Bricks | Protan | F7 | Verdict |
|---|---|---|---|---|---|
| base-01 Road to Horizon | 0.353 / 0.338 purple (0.340) | none | 0.135 / 0.130 (548,199) | 0.070→0.069, 70° | APPROVE |
| base-02 Horizon by Night | 0.434 / 0.411 | 0.487 | 0.128 / 0.125 (570,418), was 0.134 | 0.098→0.094; 125°→78° (water now teal, n3) | APPROVE |
| base-03 The Cactuar | 0.364 / 0.361 orange; test purple 0.331 | 0.380 | 0.123 / **0.099** (452,300), unchanged n3 of round 6 | 0.076→0.075 | APPROVE |
| base-04 The Gilded Dome | 0.310 / 0.311 orange; test purple 0.273 at (517,386) | 0.284 | 0.118 / 0.109 (300,128) | 0.073→0.066 | APPROVE |
| base-05 The Crystal's Call | 0.435 / 0.421 | 0.408 | 0.121 / 0.110 (440,244) | 0.082→0.079 | APPROVE |
| base-06 Limsa Across the Water | 0.400 / 0.396 | 0.320 | 0.143 / 0.135 | 0.086→0.082 | APPROVE |
| base-07 Moonpath on the Bay | 0.409 / 0.410; test purple 0.376 | 0.519 | 0.134 / 0.130 | 0.068 | APPROVE |
| base-08 The Kraken's Sea | 0.381 / 0.381; test purple 0.346 | 0.332 | 0.118 / 0.109 (133,185) | **0.066→0.0605 (limit 0.06); 71°→62° (limit 60)** | APPROVE (m1) |
| base-09 The Ferry Under Sail | 0.316 / 0.294 purple | none | 0.138 / 0.131 | 0.079→0.078; 73°→65° | APPROVE (m2) |
| base-10 Twin Lanterns | 0.473 / 0.473; test purple 0.417 | 0.346 | 0.122 / 0.119 static; mover 0.113 per the keep-L report | 0.065→0.063 | APPROVE |

The engine's F6 test (`dotnet test Tsukimichi.Tests -c Release --no-build --filter "FullyQualifiedName~MoonfallRuntimeArtTests"`): **72 / 72 passed.**
- Every margin is ≥ 0.273 (1-4 purple, at (517,386)).
- Scene p99 luma is 0.203–0.390.
- My render-based figures match the test to within 0.003 at the same places.

## 2. Findings

### M1 (Major, set-wide engine; affects 1-2, 1-3, 1-4, 1-5, 2-1, 2-2, 2-3, 2-5): no peg mark on bricks

**Evidence:**
- `Tsukimichi/Ui/MoonfallWindow.Scene.cs:326`: `if (peg.Cleared || peg.Shape != PegShape.Round) continue;`.
- Render `review-ux/r/base-03-marks-1280.png`, crop `v/marks03b.png`: with `--marks` on, round pegs carry crescents and leaves, but the purple brick segment next to a blue one at board (270–299, 378–385) has no star.
- The ten levels ship 41 bricks:
  - every one can take purple (the purple deal is uniform over blue bodies, `MoonfallGame.cs:1385`);
  - 27 can be green;
  - 1 can be orange (2-2).
- In the seed-1 renders, purple landed on a brick on 6 of the 8 brick boards.
- Brick sprites under colour-vision simulation (atlas, unlit, OKLab):

  | Pair | Vision | ΔE | Of which hue (Δab) | Reads as |
  |---|---|---|---|---|
  | blue vs purple | deutan | 0.074 | 0.017 | a darker blue brick |
  | blue vs purple | protan | 0.111 | 0.039 | |
  | orange vs green | protan | 0.137 | 0.028 | lightness only |

  The orange–green case matters on 2-2: its one orange-able brick can also be green.
- This predates the round (main's placeholder levels had 12 bricks), but this set is where bricks become common.

**Fix:**
- In `PegMarks`, draw the kind's mark on each brick segment: one per segment at its middle spot (`MoonfallVeil.BrickSpots`), sized to the brick's thickness and upright.
- Extend `Peg_marks_put_a_shape_on_every_kind...` to cover bricks.

### m1 (Minor, 2-3): F7 has no margin left under the lightness-kept jewel

**Evidence:**
- Engine scene at 2x (`--scene-only 2 --no-grain`, measured with the pipeline's own `readability.jewels`):
  - mean chroma **0.0605** against the 0.06 limit (approved 0.066);
  - jewels **62°** apart against 60° (approved 71°).
- The keep-L report agrees (0.060, 62°).
- The scene is a game painting read from the player's install, so a patch to that texture could tip it over.

**Fix:**
- Raise `rhotano-wonders`' region chroma by about 0.01 (or its jewel's chroma) until mean chroma is ≥ 0.065.
- Then re-measure F9 at (133,185): protan 0.109 today.

### m2 (Minor, 2-4): Fever never swells the moon, contrary to the brief and the commit

**Evidence:**
- `feverMoon [150,104,24]` needs clearance ≥ R + `MoonKeep` = 42. The peg at (150,150) r 8 sits 38.0 from the moon's centre to its edge.
- `MoonfallSceneBuilder.cs:292` silently drops it.
- Fever render under Reduce motion (bright-disc equivalent radius):

  | Level | Before Fever | In Fever |
  |---|---|---|
  | 2-4 | 1.5 units | 1.5 units (no swell) |
  | 2-2 | 1.3 units | 39.5 units (swells) |

- For readability the drop is right: the swollen disc (r 34.8) would come within 3.2 units of that peg, against a near-white moon.

**Fix:**
- Remove `feverMoon` from 2-4's runtime block, or accept the drop and correct the docs.
- Make the converter's gate report a dropped moon.

1-3: the swollen moon keeps 30.4 units from every piece (computed). The render harness's fever moment never reached Fever on 1-3, so this is checked from the code only.

### m3 (Minor, tests): the engine's F6 skips bricks, and its "0.8x" is not the 640 window

**Evidence:**
- The test iterates `Shape == PegShape.Round` only.
- The pipeline's `bricks_min_margin` scores only the dealt colour (blue), so a purple brick (face 0.61 against a blue brick's 0.71) has never been gated anywhere.
- The 640x480 window draws the board at **0.735**, not 0.8 (content height 441 / 600).
- Today it doesn't matter. On the renders:
  - bricks hold ≥ 0.284 (1-4, purple face);
  - pegs at 640 hold ≥ 0.294 (2-4 purple).

  But nothing would catch a regression.

**Fix:**
- Add bricks to the F6 test, every kind's face, with rings 3–11 units outside the brick's edge.
- Change the second tier to 0.735.

### m4 (Minor, coverage of the spoiler shield's fallback)

**Evidence:**
- The harness's `StoryShield` (`Program.cs:513`) hides only eras past `--story N`. A-Realm-Reborn zones are never hidden, so `--story 0` renders 1-1, 1-4 and 2-1 with their game paintings (`v/story0.png`).
- The shield's own fallback path (`MoonfallGameArt.SafeRecipe`) is therefore unrendered and untested. It drops paint plates but keeps light, cover, cloth and rim plates and the palette regions.
- I checked the fallback look via `--no-game-art`, the builder's fallback over moon-road-night. It differs only in keeping 1-1's engraved roads. Readability there is better than the shipped scenes (1280 / 640):

  | Level | F6 worst | Protan minimum |
  |---|---|---|
  | 1-1 | 0.474 / 0.493 | 0.144 |
  | 1-4 | 0.464 / 0.478 | 0.139 |
  | 2-1 | 0.454 / 0.473 | 0.158 |
  | 2-3 | 0.459 / 0.477 | 0.126 |

**Fix:**
- Add a `--hide-zone "<zone>"` flag to the renderer so `SafeRecipe` itself is rendered.
- Add an F6 case over the fallback scenes.

The fallback's look is art's call: 1-4's crimson region becomes a stray red cloud at upper left, and Ul'dah's lamps float as warm dots (`r/base-04-nogame-1280.png`). It is harmless to readability.

### n1 (Nit): the beams' motion is imperceptible

The moving share swings luma by about 0.009 at a shaft's core (k 0.06 × 15%). The motion amplitude map (`v/dmap*.png`) shows only dust, stars, fireflies, lamps and movers. This is good for readability. Whether that meets the owner's "ambient motion on every level" is art's and the owner's call. Any boost must keep the beam mask (zero within 6.5 units of a piece's edge, full from 9.5).

### n2 (Nit): the lamp flicker reads as fast jitter

The flicker is two sines at 2 and 3.5 Hz, driving the halo's alpha 0–0.16. Centre luma swings 0.03–0.06 (0.11 on 2-1's and 2-2's corner lanterns). Every lamp keeps ≥ 23 units from any piece, and the area is far below WCAG 2.3.1's flash threshold. Optionally slow it to ≤ 2 Hz with irregular modulation.

### n3 (Nit, 1-2): the water's second jewel moved from green to teal

The hue went from 150° (green) to about 198° (teal): bins 4 → 5 and 7. The jewels are now 78° apart instead of 125°. It still passes. The approved green was partly an artefact of the clipping; the engine now draws the recipe's own teal (#30B888). Flag it to the art supervisor.

### Motion checks, all confirmed

- **Rings.** Across 25 frames over the 6 s loop at 1280 and 13 at 640, the ring's 90th percentile (2–9 units out from each static peg, bucket area excluded) rises at most **0.009** (1-5 near a firefly, 1-4 by a lamp).
- **Inner band.** The largest luma change within r+0.8 to r+2.5 of a piece is **0.011**.
- **Tests.** `Nothing_moves_within_2_5_units` and `Fireflies_and_dust_keep_8_units` pass on all ten.
- **Beams behind the framing.** Checked in the code only (`Behind()`, `MoonfallSceneBuilder.cs:549-553`); the amplitude is too small to show in the renders.
- **Mist.** Every band sits below the lowest piece's edge, with alphas 0.05–0.07:

  | Level | Mist band from | Lowest piece's edge | Gap |
  |---|---|---|---|
  | 1-3 | 538 | 519.5 | 18.5 |
  | 2-1 | 516 | 506 | 10 |
  | 2-2 | 540 | 528 | 12 |
  | 2-4 | 516 | 508 | 8 |
  | 2-5 | 532 | 523 | 9 |

- **Reduce motion.** The still frame differs from the moving t=0 frame only by twinkles and fireflies: luma p99 ≤ 0.004 over the opening, 0.03 on 1-5. Fever under Reduce motion goes straight to the lit state with no zoom.
- **Cleared boards.**
  - No veil print anywhere: the engine's veil leaves with each piece, while the pipeline's cleared composites keep baked dark dots (`v/cm4_*.png`).
  - The palette's broad quiet still leaves a soft lighter wash where the layout was, as approved. On 1-1 it is the grey sea at left; it is identical in the pipeline.
- **Marks render** at 640: crescents and leaves stay legible (`v/marks03b.png`).

## 3. The six provisional decisions

1. **Lightness-kept jewel: agree.** Lightness is the axis readability depends on. In the engine, F6, the print (equal or lower: 1-2 p90 0.0115→0.0071) and F9 hold. The worst protan loss is 1-2 at −0.006 to −0.008 (p10 0.128, minimum 0.125), still far above the 0.101 floor. The price is chroma: 2-3 is at F7's edge (m1), and 1-2's water turns teal (n3).
2. **2-1's squeeze: agree.** Readability is unaffected (F6 0.400, protan 0.135). The look is art's call.
3. **Region chroma limit 0.3: agree.** The converter's gate, not the loader, is the real guard against a hand-edited coin. The loader now accepts more.
4. **Scene tags and fallbacks: agree.** The fallback scenes read better than the shipped ones. But the shield's path is unrendered and untested (m4).
5. **Aces:** outside my focus. No view.
6. **28 orange candidates per level:** no readability bearing. Agree.

## 4. Not checked or not verifiable

- **Real game.** The in-game GPU path, ImGui's real sampling and the live 60 fps motion are unverified. Motion was judged from 25 sampled frames and the code. Flicker frequency comes from the code (the 0.25 s sampling cannot resolve 3.5 Hz).
- **Movers' colour-blind separation** in the engine: static candidates only. The keep-L report's mover minimum on 2-5 is 0.113.
- **Purple and green pegs** that weren't dealt in the seed-1 renders: their faces come from the dealt kinds and the F6 test.
- **Shield fallback:** the real `SafeRecipe` render (m4); only the builder fallback was rendered.
- **1-3 Fever** in render (the harness missed the last orange).
- **Colour vision:** Machado at severity 1 only. Under protan or deutan the two jewels collapse into one on every board; that is inherent to dichromacy, and the jewels are scene identity, not gameplay.
- **Tritan** vision.

Everything is in `scratchpad/levels-b/review-ux/`: scripts `f6motion.py`, `cvd.py`, `f7.py`, `brickf6.py`, `f6fb.py`, `dmap.py`; renders in `r/`; crops in `v/`; logs `runtimeart.log`, `f6motion.log`, `cvd.log`, `f7.log`.

