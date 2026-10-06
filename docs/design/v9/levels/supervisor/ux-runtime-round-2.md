# UX/UI specialist, runtime round 2: the ten levels in the engine

**Reviewed:** worktree `agent-a570460c913ca1c79`, HEAD `fa200dc0`. I worked read-only. `git status` shows nothing of mine; the one untracked file, `docs/design/v9/levels/supervisor/game-designer-runtime-round-2.md`, is the game designer's. I ran no build and no `mfl.py` command. All my output is in `scratchpad/levels-b/review-ux/r2/`.

**OVERALL: APPROVE all ten levels.**
- **Round 1:** all eight of my findings (M1, m1–m4, n1–n3) are resolved.
- **Engine F6 test** (`dotnet test Tsukimichi.Tests -c Release --no-build --filter "FullyQualifiedName~MoonfallRuntimeArtTests"`): 104 / 104 passed. It now covers bricks, the 0.735 tier and the story-safe scenes.
- **New findings:** two Minors and five Nits. None blocks a level.
  - **m1:** at 640, brick marks shrink to a dot, so you can see that a brick is marked but not which mark it carries.
  - **m2:** 1-1's road glint is brighter than any peg and is on screen most of the time.

## 1. Verdicts

The columns:
- **F6 render:** worst kind's margin over the ring p90 on my renders, 1280 / 640.
- **F6 test:** the engine test's worst, including bricks, at 1x and 0.735x, then its worst over the story-safe scene.
- **Protan (F9):** orange separation, p10 / minimum at 1280. 640 is within 0.001.
- **F7:** the engine's bare scene at 2x: mean chroma and how far apart the jewels are.

| Level | F6 render | F6 test (worst; safe) | Protan | F7 | Verdict |
|---|---|---|---|---|---|
| base-01 Road to Horizon | purple 0.353 / 0.338 | purple 0.343 (0.735x); safe 0.472 | 0.135 / 0.130 | 0.069, 70° | APPROVE (m2, n1) |
| base-02 Horizon by Night | purple 0.434 / 0.411 | peg 0.436, brick 0.470; safe 0.425 | 0.130 / 0.128 (was 0.128 / 0.125) | 0.095, **124°** (n3 fixed) | APPROVE (m1) |
| base-03 The Cactuar | orange 0.364 / 0.361 | purple 0.331, brick 0.361; safe 0.428 | 0.122 / 0.099 (known, round-6 n3, unchanged) | 0.075, 72° | APPROVE (m1) |
| base-04 The Gilded Dome | orange 0.310 / 0.311 | **purple brick 0.222 at (469,325)**; safe 0.430 | 0.118 / 0.109 | 0.066, 90° | APPROVE (m1, n2) |
| base-05 The Crystal's Call | orange 0.435 / 0.421 | purple 0.393, brick 0.396; safe 0.428 | 0.121 / 0.110 (safe 0.105) | 0.079, 73° | APPROVE (m1) |
| base-06 Limsa Across the Water | orange 0.386 / 0.378 | purple brick 0.278 at (525,431); safe 0.424 | 0.143 / 0.135 | 0.087, 116° | APPROVE (m1) |
| base-07 Moonpath on the Bay | orange 0.410 / 0.412 | purple 0.376, orange brick 0.512; safe 0.421 | 0.134 / 0.130 | 0.068, 75° | APPROVE (m1) |
| base-08 The Kraken's Sea | orange 0.379 / 0.378 | purple brick 0.345; safe 0.417 | 0.119 / 0.108 at (133,185) | **0.066, 77°** (m1 of round 1 fixed) | APPROVE (m1) |
| base-09 The Ferry Under Sail | purple 0.316 / 0.294 | purple 0.301; safe 0.448 | 0.138 / 0.131 | 0.078, 65° | APPROVE (n4) |
| base-10 Twin Lanterns | orange 0.474 / 0.476 | purple brick 0.394 (0.735x); safe 0.425 | 0.122 / 0.119 | 0.062, 135° (n3) | APPROVE (m1, n1) |

**Bricks on my renders:** worst margin against a purple brick's face, 1280 / 640:

| 1-2 | 1-3 | 1-4 | 1-5 | 2-1 | 2-2 | 2-3 | 2-5 |
|---|---|---|---|---|---|---|---|
| 0.487 | 0.380 | 0.284 | 0.408 | 0.312 | 0.519 | 0.331 | 0.377 / 0.346 |

The test's brick figures are lower because it takes the face from the unlit sprite and judges the ring spot by spot.

## 2. Findings

### Round-1 findings: all resolved

- **M1 (marks on bricks): resolved.**
  - `MoonfallWindow.Scene.cs` now drops the `Shape != Round` skip and places each mark with `MoonfallPegMarks.Place`.
  - `Peg_marks_sit_on_every_brick_inside_its_face` passes.
  - Renders with `--marks` show a star on every purple brick and a crescent on 2-2's orange brick (`v/brickmarks.png`).
  - Its legibility at 640 is the new m1 below.
- **m1 (2-3's F7): resolved.** The engine's bare scene measures mean chroma 0.066 and 77° apart (limits 0.06 and 60°). F9 at (133,185) is unchanged: protan 0.108.
- **m2 (2-4's Fever moon): resolved.**
  - `feverMoon` is gone from `ferry-under-sail.json`.
  - The `kept` report now prints `moonDeclared`, and `What_the_recipe_asks_for_survives_its_level` asserts that the moon is kept when declared.
- **m3 (F6 test skipped bricks; wrong small tier): resolved.**
  - The test scores every kind a brick may be dealt, with rings 3–11 units outside its edge.
  - The small tier is `SmallWindowScale = 0.735`.
- **m4 (shield fallback untested): resolved.**
  - `--hide-zone` renders `MoonfallSceneBuilder.StorySafe`, the same code the plugin's `SafeRecipe` now calls.
  - `Every_peg_reads_against_its_story_safe_scene` passes for all ten, worst 0.417.
  - My renders of the safe scenes show F6 of at least 0.398 at 640 (1-2 purple) and protan minimum of at least 0.105 (1-5).
  - 2-3 (set nowhere) is untouched under the shield, as intended.
- **n1 (beams imperceptible): resolved.**
  - On the beam levels the broad (10-unit) motion amplitude now has p99.5 of 0.011–0.022, peaking at 0.035 in 1-2's shaft at (89,179). In round 1 it was about 0.009 at a shaft core. It reads as a slow swell (`v/beam02.png`, `v/beam04.png`, gain ×40).
  - The keep-out holds: dark holes stay round every piece. Within 6.5 units of a piece's edge, the largest change on beam levels is ≤ 0.0084.
- **n2 (fast flicker): resolved.**
  - The flicker is now 1.3 and 1.9 Hz, and the rate test passes.
  - The halo swings luma 0.03–0.07 within 4 units of a lamp, ≤ 0.02 at 8 units.
  - The closest flickering light to any piece is 2-4's middle porthole, 11.7 units from the edge of the peg at (390.6,403.5). Its swing 6 units out is 0.022, and the change in the band 0.8–2.5 units round any piece is ≤ 0.003.
- **n3 (1-2's water turned teal): resolved.** The second jewel is back at 151° (approved 150°), 124° from the first (approved 125°).

### m1 (new, Minor; every brick level: 1-2, 1-3, 1-4, 1-5, 2-1, 2-2, 2-3, 2-5): at 640 a brick's mark shrinks to a dot

**Evidence** (`v/brickmarks.png`; crops of `r/base-NN-marks-{1280,640}.png`):
- A brick's mark box is its thickness: 10 units on 27 bricks, 12 on 14. At 0.735 that is 7.4–8.8 px. A round peg's mark is 12–13 px.
- At 1280 the four-point star on purple bricks reads clearly.
- At 640 the star becomes a 2–3 px dark dot or dash:
  - 1-4 at (415,394): a horizontal dash;
  - 2-5 at (531,498): a vertical bar;
  - 1-5 and 2-3: a dot.
- The crescent on 2-2's orange brick at (141,390) becomes a small tick.
- Marked against unmarked still works, so blue against purple (the deutan pair) is fine.
- The weak case is 2-2's one brick at (140,420), the only brick that can be dealt both orange and green (r 30, sweep 70, thickness 10). Under protan, orange and green differ only in lightness (ΔE 0.137, round 1), so the player needs the crescent-or-leaf shape, which at 640 is a 7 px glyph.
- No green brick was dealt in these renders, so the leaf on a brick is unverified.

**Fix** (pick one):
- At the small tier, floor the brick mark's box at about 11 px, i.e. half-size ≥ max(thickness/2, 7.5 units). Accept a 1–2 px overhang and relax the "inside its face" test to the star's core.
- Or draw the brick's mark on an upright medallion: a disc of the brick's own face, r 7.5, at its middle.

Then add a test that a brick's mark box is at least 10 px at 0.735.

### m2 (new, Minor; 1-1): the road glint outshines every peg and is almost always present

**Evidence** (48 frames, 0–23.5 s, `r/base-01-m1280-*.png`):
- Peak luma is 0.87–0.91 over a spot about 6 units across (44–60 px above 0.5 at 1280). For scale:
  - every peg face is 0.62–0.72;
  - only the ball is brighter.
- Two tracks of about 10.7 s each run on a 16 s cycle, offset 8 s, so a bright glint shows in about 60% of frames.
- Its centre may come within 6.46 units of a piece's edge, so its halo reaches 2.5–6 units from pegs. Every other light keeps further off (fireflies 8, beams 6.5 rising to full at 9.5).
- It lifts the F6 ring's p90 by up to **0.022**. That is at (219.5,203.7), and 0.019 at (267.4,207.5), the level's worst F6 peg. The other nine levels lift by at most 0.009.
- Readability holds: the worst margin stays ≥ 0.33 against the 0.20 floor. But it is the brightest moving thing on the board after the ball, running through the densest pegs.

**Fix:**
- Cap the core so its peak stays below the peg faces: the white core from 0.8 to about 0.4, for a peak of about 0.6.
- Give the board rest: one track at a time, e.g. `every` ≥ 24 s with offsets that don't overlap.
- Optionally match the fireflies' 8-unit keep-out.

The owner judges how visible it should be; readability alone does not require the change.

### n1 (Nit; 1-1, and 2-5 and 2-4 in look): the story-safe scenes keep motion and dress tied to the hidden painting

- 1-1 under the shield and without game art (`r/base-01-hide-1280.png`, `v/hglint.png`) keeps both glints. They trace roads that are no longer drawn, so a spark wanders the night sky between pegs.
- 2-5's safe scene keeps its palette regions: a crimson field with a darker red disc at the board's centre (`v/hs0510.png`).
- 2-4's safe scene keeps the ferry's cover plate as a dark sail shape, with its three portholes.
- Readability is fine on all three (above).
- **Fix:** `StorySafe` drops `Motion.Glints`. The regions and light plates are art's call, as 1-4's red cloud was in round 1.

### n2 (Nit, information; 1-4): the set's thinnest F6

The test's purple brick at (469,325) holds 0.222 against the 0.20 floor; my render measures 0.284. The painting is the game's (Ul'dah's loading image), so a patch could lighten it, but the test will catch that. No change asked.

### n3 (Nit, information; 2-5): the set's thinnest F7

Mean chroma is 0.062 (approved 0.065, limit 0.06). The painting is our own and ships as a JPEG, so it cannot drift. No change asked.

### n4 (Nit, docs; 2-4): "the two lanterns" do not flicker

- The brief and the commit say 2-4's port lights include two flickering lanterns. The converted recipe has three flickering lights: the portholes at (296/388/480, 424).
- The other two points, (246,372) and (612,362), sit exactly on pegs. `convert.small_lights` drops them, as the dress does.
- Nothing is missing against the approved scene. Correct the text.

### n5 (Nit, harness): `--no-game-art` renders take about 150 s each

`Settle()` runs to its 3000-frame cap twice, because `ArtSettledForRender` never settles without game art. It is unchanged from round 1 and affects the harness only.

### Motion and Reduce motion, confirmed

- **Rings:**
  - Static pegs: the ring's p90 2–9 units out rises at most 0.0087 (1-5, a firefly), except 1-1's glint (m2).
  - The band 0.8–2.5 units out changes at most 0.0129. That is on 2-3 by its orbiting movers; 0.011 on 1-1.
  - Frames: 20 at 1280 over the 9.5 s beam loop (48 on 1-1), 10 at 640, plus 8 at 0.1 s steps for the flicker.
- **Mist at 0.11:** none measurable reaches the pieces. The amplitude along rows y 500–520 on mist levels equals that on 1-4 and 2-3, which have no mist; the bucket's lamp sets it. Every band starts 8–18.5 units below the lowest piece's edge, as in round 1.
- **Reduce motion:** the still frame at t=0 and at t=5 differ by at most 0.0076 (p99.9 0.0028) on 1-1, 1-3, 1-4, 1-5 and 2-5, and 0.015 on 2-4 (mover veils). No glint, flicker, twinkle or beam drift shows.
- **Missing painting (`--no-game-art`)** on 1-1, 1-4, 2-1, 2-3: F6 ≥ 0.388 (1-1 purple at 640), protan minimum ≥ 0.124. The paint plates are dropped, so 1-1's engraved roads are now gone, as in the shield's version.
- **Protan and the jewels:**
  - The art changes on 1-2, 2-1 and 2-3 hold or improve F9 (above).
  - Under protan simulation the two jewels stay 160–168° apart on every board. They separate along the blue–yellow axis, so the scene's two colours still read.

## 3. Not checked or not verifiable

- **Real game:** the in-game GPU path, ImGui's real sampling, and live 60 fps motion. I judged motion from sampled frames and the code.
- **Marks not dealt:** green-brick and orange-brick marks other than 2-2's crescent (the renderer has no seed option), and the leaf at 640 on a brick.
- **Moments:** Fever, tally and cleared, which I did not re-render this round.
- **Colour vision:** movers' colour-blind separation; tritan vision; Machado simulation at severity 1 only.
- **Other reviewers' ground:** visual fidelity against the approved scenes, the stars' sky masks, the aces, and the converter gate.

Everything is in `scratchpad/levels-b/review-ux/r2/`:
- **Scripts:** `f6motion.py`, `motion2.py`, `beams.py`, `flick.py`, `cvd.py`, `brickf6.py`, `brickmarks.py`, `f7r2.py`.
- **Logs:** `runtimeart.log`, `f6motion.log`, `motion2.log`, `cvd.log`, `cvdhide.log`, `f6hide.log`, `brickf6.log`, `f7r2.log`, `mist.log`, `nogame.log`.
- **Renders:** in `r/`.
- **Crops and maps:** in `v/`.

