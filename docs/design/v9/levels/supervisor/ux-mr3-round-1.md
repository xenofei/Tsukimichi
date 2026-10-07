VERDICT: REVISE (0 blockers, 1 major, 3 minors)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| M1 | Major | 3-2 base-12 | The moon sits on the launcher's axis, so the ball and the aim guide disappear over it | `feverMoon [400,220,30]` in `cloudsea-beacons.json`. On the engine renders the moon disc is L 0.934. The aim dots over it reach 0.961 (contrast 0.025 at 1280, 0.025 at 640); the same dots over sky have 0.16–0.27. The ball in the launcher is L 0.885, darker than the moon. The ball leaves at about (402,162), so every shot within ±31° of straight down crosses the disc for its first 60 units. No shipped moon is on the axis (1-3 at x 150, 2-2 at 292, 2-4 off to the side). |
| m1 | Minor | 3-4 base-14, 3-5 base-15 (stage trend) | Protan orange separation is the lowest in the game. The cause is orange on green: 3-4's crests, 3-5's vortex rim | Pipeline F9 p10/min: 3-4 0.106/0.086, 3-5 0.113/0.090; shipped lowest 0.115/0.099. My engine-render measure, 640: 3-4 p10 0.087, min 0.065 at (580,353); 3-5 min 0.071 at (622,194). The same method on shipped levels at 640 gives p10 ≥ 0.092, min ≥ 0.083. All five MR3 levels pass F9 only through the 0.101 pilot fallback (none reaches 0.12), against 3 of the 10 shipped. 3-4 has 0.005 to spare. |
| m2 | Minor | 3-5 base-15 | Jewel mean chroma is exactly at the F7 limit on a game painting whose converter gate fails | readcheck `mean_chroma 0.06` (limit 0.06; shipped thinnest 0.062). `convert --check` fails on 3-5 (2x max 0.071, p99.9 0.020), so the runtime grade is not the measured one. |
| m3 | Minor | 3-2, 3-4, 3-1 | F6 headroom is the thinnest of any stage | Pipeline worst margins: 3-4 0.215 (purple 0.8x, mover at (519,349)), 3-2 0.216 (purple 0.8x at (331,261), the moon's halo), 3-1 0.222. Shipped: lowest 0.219 (1-4), median about 0.30. All five MR3 levels are at or below 0.26. |
| n1 | Nit | 3-3 base-13 | The story-safe scene keeps the mauve region, which ends in a hard vertical edge at x 500 | Render with `--hide-zone "Coerthas Central Highlands"`. It reads well: F6 0.443/0.440, protan p10 0.131/0.115. Same class as shipped 2-5 n1. |
| n2 | Nit | 3-5 base-15 | The painting's bird marks touch pegs; at 640 they read as dark specks stuck to the pegs | South flock around (430–520, 300–420), birds 0–3 units from the pegs' edges. The aim-guide column x 400 runs between birds. |
| n3 | Nit | 3-4 base-14 | Lamp posts under sliding pegs make them look stemmed or fixed, worst at 640 | For example the mover at (556,392) parks on a post cap, as do the movers at (519,349) and (395,318). |
| n4 | Nit | pack | The engine F6 test result for the five new levels is not in the pack | `MoonfallRuntimeArtTests` lists the new levels automatically through `ShippedScenes()`, but neither index.md nor the commit reports a run. I could not run it under the read-only brief. |

## Method

- **Renders:** I re-rendered every level with the built MoonfallRender `--cache` into my scratchpad: the 15 levels at start and cleared, 1280 and 640. My renders of base-11 and base-14 are pixel-identical to the pack's.
- **F6:** the lit face's p80 against the p90 of the ring 2–9 units out on the same window's cleared board, in OKLab L, for the colours as dealt.
- **F9:** orange core against the ground 12–20 units out, simulated as a protanope sees it (Machado 2009, severity 1).
- **Calibration:** I ran the same method on base-01 to base-10 to set the shipped ranges, so the comparisons are like for like. It differs from the pipeline's (undressed reference, every placement), so read it for ranking, not against the 0.20 floor.
- **Motion:** 20 frames from 0 to 28.5 s per level at 1280 (base-01 for reference), plus Reduce motion at 0, 5 and 13 s.

## Per-level verdicts

| Level | F6 pipeline (worst) | F6 engine 1280 / 640 | F9 pipeline p10 / min | F9 engine 640 p10 / min | F7 (chroma, apart) | Verdict |
|---|---|---|---|---|---|---|
| 3-1 base-11 The Airship Road | 0.222 purple 0.8x | 0.268 / 0.259 | 0.110 / 0.109 | 0.100 / 0.089 | 0.078, 82° | APPROVE (m3) |
| 3-2 base-12 Above the Cloud Sea | 0.216 purple 0.8x | 0.250 / 0.248 | 0.117 / 0.112 | 0.108 / 0.106 | 0.074, 78° | **REVISE (M1)**, m3 |
| 3-3 base-13 The Holy See | 0.257 | 0.232 / 0.215 | 0.112 / 0.111 | 0.103 / 0.102 | 0.063, 84° | APPROVE (n1) |
| 3-4 base-14 The Airship Under Way | 0.215 purple 0.8x (mover) | 0.261 / 0.254 | **0.106 / 0.086** | **0.087 / 0.065** | 0.083, 109° | APPROVE with m1 fixed (m1, m3, n3) |
| 3-5 base-15 The Sky Whale | 0.260 | 0.230 / 0.205 (7-unit peg, 8 px; a wider sample gives 0.33) | 0.113 / 0.090 | 0.090 / 0.071 | **0.060**, 91° | APPROVE with m1 and m2 addressed (n2) |

Shipped, same engine method:
- **F6:** 1280 0.241–0.447, 640 0.236–0.420.
- **F9 at 640:** p10 0.092–0.124, minimum 0.083–0.114.

### 3-2 Above the Cloud Sea: REVISE

- **M1:** the moon is part of our own painting and is also the recipe's `feverMoon`, at (400,220) r30.
  - The pipeline's ceiling check passes (p99 0.426), because the disc is a small share of the area.
  - It is the one scene element brighter than the ball, and it sits exactly where every central shot starts.
  - The three shafts at 78/90/102° also fall down the aim column; they are within keep-out and the ring lift is 0.011.
  - The board's first orange pair (331,261) and (469,261) flank the moon. The pipeline's worst place for every kind is there too (purple 0.216, in the moon's halo).
- **Fix (pick one):**
  - Move the moon and its `feverMoon` off the column, to |x−400| ≥ about 110 (for example (520,200)), and re-aim the shafts. The board is symmetric, so a moon low on the horizon at one side also works.
  - Or keep it centred, but drop the disc to L ≤ 0.70 (a misted moon, under the peg faces) and give the guide dots a dark outline over light ground.
  - Then re-run F6 at (331,261).
  - Suggestion for the method: add a check that nothing in the scene within the launcher's ±35° cone, down to y 300, is brighter than the ball.
- **Otherwise good:**
  - The beacon lamps are 25 units clear of the hex pegs.
  - The mist band is clear.
  - The fairy-light strings are small and green-white, not confusable with the guide at either size.
  - Under Reduce motion the scene changes ≤ 0.004 (outside the band the bucket's lamp lights).

### 3-1 The Airship Road: APPROVE

- **Peg readability:** the map's lettering and emblems are busy at 640, but every peg separates; the blue pegs on the pale clouds at upper right are lightness-safe.
- **Glints:** the two glints match shipped 1-1 after its m2 fix: core 0.4, every 30 s, offset 15, tracks of 17.8 s and 6.0 s, overlapping 15–17.8 s as 1-1's do.
  - Peak L about 0.76 at (189,120), against about 0.72 on 1-1. A glint shows in 16 of 20 frames (1-1: 17).
  - Static-peg ring p90 lift ≤ 0.008; the band 0.8–2.5 units out ≤ 0.022.
- **Lane dashes and Reduce motion:**
  - The lane's khaki dashes are elongated and do not read as the round aim dots.
  - Reduce motion stops the glints; the only change left is the bucket lamp's glow (≤ 0.065 at (140,485), p99.9 0.0075).
- **Purple brick:** on the clouds at upper right it holds (pipeline brick minimum 0.403).

### 3-3 The Holy See: APPROVE

- **Pegs on the mountain:** orange on the mauve mountain (second jewel 339°) holds in protan, because the separation runs along b.
- **Purple arch:** on the mauve bridge opening it is lilac on mauve, but carried by lightness. The ridge brick chain reads at 640.
- **Motion:** dust only, ring lift ≤ 0.005. The 0.036 near (216,506) and (392,524) is the bucket lamp passing under the lowest row, as on shipped 1-1.
- **Safe scene:** see n1.

### 3-4 The Airship Under Way: APPROVE once m1 is addressed

- **m1:** under protan the sage crests turn khaki and the oranges mustard, and several oranges on the crests separate mainly by lightness (my crop shows (210,283), (352,257), (567,265) at 640). The 23 sliding movers carry oranges along exactly these crests.
- **Fix:** lower the crest tops' chroma, or darken them along rows 3–5 where orange-able movers slide (a broad tone, as 3-5 did for its brick), until the protan minimum is ≥ 0.10 at 640.
  - Prefer that to a hue shift: the 180° jewel is the charter's boundary pair.
- **Moon:** the moon at (596,118) is off-axis, consistent with 2-2.
- **Motion:** static-peg ring lift ≤ 0.0075. Movers moving under Reduce motion is gameplay, as intended.

### 3-5 The Sky Whale: APPROVE once m1 and m2 are addressed

- **m1:** the protan worst places are orange-able pegs on the green vortex rim, (569,143) in the pipeline and (604–622, 194–218) on the 640 render.
  - **Fix:** a broad tone or chroma cut on the rim behind the north flock, the same way 3-5 already darkens the area behind its billow brick at (233,425).
- **m2:** measure F7 on the engine's bare scene, as runtime round 2 did, before approving. Mean chroma 0.060 leaves no room against the open converter-gate difference on this painting.
- **Glints:** shorter than 3-1's (5.0 s and 3.4 s every 24 s, shown in 11 of 20 frames, peak about 0.70).
- **Motion and Reduce motion:** ring lift ≤ 0.004; under Reduce motion the scene changes ≤ 0.005.
- **Ghost:** the worst is 0.137 at (260,470), the highest of any level (shipped maximum 0.108), but the median 0.048 passes, and the cleared render shows no disc.
- **Small pegs:** 8 orange-able pegs are 7 units (8.4 px at 640), the most of any level (shipped maximum 5, on 2-3). Their marks are small but present.

## Consistency with the shipped stages

- **HUD and chrome:** the HUD, chrome, bucket and companion panel match at both sizes.
- **Brick marks:** the shipped m1 fix (brick marks floored at 10 px at 640) is in `MoonfallPegMarks`.
- **Margins:** the stage reads as a set but runs thinner than stages 1–2:
  - every F9 p10 is under 0.12;
  - every pipeline F6 is ≤ 0.26.
  - It is not a blocker, but the next stages should not drift lower; m1 and m3 should be fixed rather than accepted.

## Not verified

- The in-game GPU path and live 60 fps motion. I judged motion from sampled frames.
- `MoonfallRuntimeArtTests` for base-11 to base-15 (n4).
- The `--no-game-art` fallback for 3-1, 3-3 and 3-5.
- The colours the moon-mark glyphs (`--marks`) take on the new boards.

Scratch output, including the scripts, renders and crops: `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr3ux\`. The worktree is untouched.
