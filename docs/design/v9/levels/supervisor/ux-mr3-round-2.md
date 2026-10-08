VERDICT: APPROVE (0 blockers, 0 majors, 1 minor)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| m1 (new) | Minor | 3-1 base-11 | Protan orange separation dropped this round. Its p10 is now the lowest of all 15 levels and clears the 0.101 F9 fallback by only 0.004. | Pipeline p10/min went from 0.110/0.109 to 0.105/0.103. The lowest shipped p10 is 0.115. My engine measure at exact peg centres gives p10 0.122 at 1280 and 640; the shipped range is 0.133 to 0.170. The likely cause is the grey-green black-floor lift (k 0.024), and the response's n4 trade notes the F9 cost. The minimum, 0.121, is inside the shipped range (lowest 0.115), so no single peg fails. |
| n1 (carried) | Nit | 3-3 base-13 | The story-safe scene still has the mauve region's hard edge at x 500. | Not fixed, with the reason given: it would put the 1x converter p99 at risk (0.0349 against 0.035). Shipped 2-5 n1 is the precedent. Accepted. |
| n2 (carried) | Nit | 3-5 base-15 | Painted birds sit under or against the flock's pegs, so wing tips show either side of a peg. At 640 they read as dark specks. | Lower-right flock, for example the oranges at (542,412) and (570,323) and the pegs at (592,441), (542,442) and (542,472); about 30% of the disc area under each is dark painting. The peg kind is still clear at both sizes, and with `--marks` the crescents are legible. The reason for not fixing it (it keeps the trace) is accepted. |
| n5 (new) | Nit | stage | Protan p10 still runs thinner than stages 1 and 2. All five levels still pass F9 only on the 0.101 fallback. | Pipeline p10s are 0.105, 0.117, 0.112, 0.107 and 0.112; four are under the shipped lowest, 0.115. The minimums are now all at least 0.102, against a shipped lowest of 0.099. This is the same point as the critic's n3, which was not fixed. MR 4 should not go lower. |

## Round-1 findings: status

| R1 id | Status | Evidence (this round) |
|---|---|---|
| M1 3-2 moon on the axis | **Fixed** | The disc is now L 0.620 at p50 and 0.655 at p95, at both sizes (it was 0.934). The aim dots over the disc stand 0.27–0.31 above it at 1280 and 640 (it was 0.025). Over the sky they stand 0.38–0.57 above. The ball (L 0.885–0.98) is clearly brighter than the disc. In the ±35° cone down to y 300, leaving out the guide column, the cleared scene's p99.9 is 0.692; its maximum is 0.812, a single point on the beacon lamp at (376,271). That lamp is a few pixels across, sits under the ball's L and is 25 units clear of pegs, as in round 1. Pipeline F6 is 0.237; the worst place has moved off the moon to (236,388). Crops confirm that the guide reads all the way down the column at 640. |
| m1 protan on 3-4 and 3-5 | **Fixed** | Pipeline minimums: 3-4 went from 0.086 to 0.102 and 3-5 from 0.090 to 0.109. Engine, at exact centres with 3-4's movers at their start positions: 3-4 p10/min 0.125/0.120 (lowest at (448,355) on the aqua wedge), 3-5 0.135/0.123. Both minimums are inside the shipped range. Sage crests no longer sit under the movers. |
| m2 3-5 F7 chroma | **Fixed** | The pipeline report gives mean chroma 0.069, with jewels 258/176 and 82° apart. The converter gate passes 30 of 30. I did not re-measure the engine's bare-scene F7 myself; the pack's figure (0.069) matches the pipeline. |
| m3 F6 headroom | **Fixed (improved)** | Pipeline worst: 3-1 0.267, 3-2 0.237, 3-3 0.265, 3-4 0.232, 3-5 0.229. All are above the shipped lowest, 0.219. 3-5's pipeline figure fell from 0.260 to 0.229 on the new board (inner-wall moon at (509,240), beside the banner), but in the engine it improved from 0.230/0.205 to 0.268/0.242 at 1280/640. The stage median, 0.237, is still below the shipped median of about 0.30 (see n5 for the stage trend). |
| n1 3-3 safe-scene edge | Open, accepted | See n1. |
| n2 3-5 birds | Open, accepted | See n2. The new flock chevron puts more pegs on birds than round 1's board did. |
| n3 3-4 lamp posts under movers | **Fixed** | The movers slide 20–26 units on rows 3–5. The posts now stand only under the static lamp moons ((519,349), (395,318) and the beacon rings), which are meant to look fixed. On the start render no mover sits on a post. |
| n4 engine F6 test not in pack | Answered | The response reports `MoonfallRuntimeArtTests` 154/154, 40 of them on base-11 to 15, and the gate 9,018/9,018. I could not run them under the read-only brief, so this is unverified by me. |

## Per-level verdicts

| Level | F6 pipeline (worst) | F6 engine 1280 / 640 | F9 pipeline p10 / min | F9 engine (exact centres) p10 / min | Motion (static-peg ring lift, from 6 s) | Verdict |
|---|---|---|---|---|---|---|
| 3-1 base-11 The Airship Road | 0.267 purple 0.8x (308,229) | 0.262 / about 0.29 (see note) | **0.105** / 0.103 | **0.122** / 0.121 | ≤ 0.005 | APPROVE (m1) |
| 3-2 base-12 Above the Cloud Sea | 0.237 purple 0.8x (236,388) | 0.286 / 0.287 | 0.117 / 0.112 | 0.140 / 0.132 | ≤ 0.0105 | APPROVE (M1 fixed) |
| 3-3 base-13 The Holy See | 0.265 purple 0.8x (300,427) | 0.227 / 0.218 | 0.112 / 0.111 | 0.136 / 0.130 | 0.041 at (216,506): the bucket lamp, as in round 1 | APPROVE (n1) |
| 3-4 base-14 The Lane by Starlight | 0.232 purple (301,392) | 0.250 / 0.229 | 0.107 / 0.102 | 0.125 / 0.120 | ≤ 0.007 | APPROVE |
| 3-5 base-15 The Sky Whale | 0.229 purple 1x (509,240) | 0.268 / 0.242 | 0.112 / 0.109 | 0.135 / 0.123 | ≤ 0.017 (bucket lamp at the bottom) | APPROVE (n2) |

Shipped ten, same engine method and moment: protan p10 0.133–0.170 and minimum 0.115–0.165. Engine F6 worst per level is 0.222–0.404 at 1280.

Note on 3-1's 640 F6: my script reports 0.216 at (422,228), but that comes from mis-centring a 9-unit peg at 640. Measured at the level coordinates, the peg reads face 0.79 against ring 0.46, a margin of about 0.33. The next-worst place on the board is 0.285.

### 3-1 The Airship Road: APPROVE (m1)
- The four broad tones quieten the banners, and F6 rises from 0.222 to 0.267. Every peg separates at 640, including the oranges on the dark teal at (221,367) and (335,305).
- **m1:** the black-floor lift adds a grey-green cast to the darkest land, which is likely what cost the protan p10 here.
  - Suggested fix if 3-1 is touched again: make the lift neutral grey, or restrict it to the region outside the orange candidates' 12–20 unit rings.
  - The lift's purpose, the converter gate's 2x maximum, only needs the floor off sRGB 0. The tint is not part of that.
- Glints are unchanged from round 1. Static-peg ring lift is ≤ 0.005 from 6 s on.
- The khaki lane dashes still do not read as aim dots.

### 3-2 Above the Cloud Sea: APPROVE (M1 fixed)
- **The moon:** it reads as a misted moon behind two strata of mist, not as a hole or a giant peg. The guide dots and the ball read over it at both sizes.
- **Motion:** star twinkles peak at L ≤ 0.53, 23 or more units from a peg's edge. Beacon pulses lift the neighbouring rings by ≤ 0.0105. Under Reduce motion, the only change is the bucket lamp's (≤ 0.024, at y 518).

### 3-3 The Holy See: APPROVE (n1)
- The keystone opening and the ridge brick removal are clean.
- The ridge chain now ends at about (400,260), right under the guide column. The dots run onto the brick's end and stay readable. Whether straight-down shots should be able to land there is the designer's call; it is not a readability issue.
- Engine F6 640 is 0.218, blue at (457,306), the same as round 1's accepted 0.215.
- The left-sky "breathing" motion is ≤ 0.016 at p99, the same as round 1 and as shipped 1-1.

### 3-4 The Lane by Starlight: APPROVE
- **Scene:** the new scene reads well at 640.
  - The star river's small stars do not compete with the guide dots, which are larger, regular and brighter.
  - The aqua wedge carries the oranges with enough protan separation.
  - The silver crest linings stay under the faces. Along every mover's track, the margin is ≥ 0.248 except the cleared board's leftover orange, which is a measurement artefact.
- **Reduce motion:** only the movers move (gameplay), plus the bucket; the scene is otherwise still.
- **Title:** "The Lane by Starlight" fits the plaque at 640.

### 3-5 The Sky Whale: APPROVE (n2)
- **Pegs:** they read against the vortex, the "Gyrfalcon Engine" lettering and the cloud lobes at both sizes.
  - The aim guide stays legible over the banner's green emblem box on the column.
  - The khaki dashed flight line matches 3-1's lane dashes and does not read as the guide.
- **Small pegs:** 35 of the 74 pieces are 7 units (8 orange-able), the most in the game (shipped maximum 15, on 1-5). With `--marks` at 640 the crescents on the 7-unit oranges stay legible, matching 1-5.
- **Glints:** they are mild (L ≤ 0.40, 18 or more units from peg edges). Under Reduce motion the change is ≤ 0.040, at the bucket.
- **Ghosts:** the cleared board shows no ghost discs. The ghost check no longer applies, because the peg-scale brick tone is gone.

## Consistency with the shipped stages
- **Chrome:** the HUD, chrome, bucket, companion panel and brick marks are unchanged and match at both sizes.
- **Pack renders:** they are pixel-identical to my own re-renders with the built MoonfallRender, `--moment start --cache`, for all 15 pack images.
- **Margins:** F6 is now inside the shipped range on every level. Protan minimums are inside it too. Protan p10 remains the stage's thin spot (m1, n5).

## Method
- **Measurement change:** the pack now uses `--moment start`, so round 1's engine figures (taken from `hud`, two shots in) are not like-for-like. I re-rendered base-01 to base-15 at start and cleared, 1280 and 640, and re-calibrated against the shipped ten.
- **Protan:** measured at the level file's exact peg centres, which removes the centring artefacts round 1's script had on small pegs. The ground is a ring r+2 to r+10 out on the cleared board, with the guide column masked. The simulation is Machado protan at severity 1.
- **Motion:** 20 frames from 0 to 28.5 s per level at 1280. Lifts are judged from 6 s, because every level shows a deal-in transient before that. Reduce motion was checked at 0, 5 and 13 s on 3-2, 3-4 and 3-5.

## Not verified
- In-game GPU rendering and live 60 fps motion; I judged motion from sampled frames.
- `MoonfallRuntimeArtTests` and the gate run (n4).
- Engine bare-scene F7 on 3-4 and 3-5 (I relied on the pipeline report and the pack's figures).
- The `--no-game-art` fallback for 3-1, 3-3 and 3-5.

Scratch (scripts, renders, crops): `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr3ux\` (round 2 in `r2\`, `h2\`). I did not touch the worktree.
