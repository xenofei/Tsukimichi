VERDICT: REVISE (0 blockers, 1 major, 3 minors)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| M1 | Major | 4-4 base-19 (also the stage trend) | For a protanope, orange is now the hardest to separate from its ground of any level in the game. MR 3's approval said "MR 4 should not go lower", and this level goes lower: three orange-able brick segments sit on the verdigris ring. F9 passes only on the 0.101 fallback, with no margin left. | Pipeline F9 p10/min **0.101 / 0.083**, worst at the brick at (492,421). The shipped lowest minimum is 0.099, and MR 3 ended at p10 ≥ 0.105 and min ≥ 0.102. My engine measure, at both sizes, puts three bricks under 0.10: (501,429) 0.081/0.078, (575,194) 0.082/0.090 and (195,193) 0.088/0.087, at 1280/640. Its pegs bottom out at 0.103. Under the same measure, stage 3 bottoms out at 0.106. The cause is the same as MR 3 m1, orange on green: in a protan simulation, orange and the verdigris both turn olive. The orange brick's lightness margin is also the stage's lowest (`bricks_min_margin` 0.248, against 0.34–0.44 on the other four). |
| m1 | Minor | 4-5 base-20 | One orange-able arc segment (the "Thal ring" arc) is below 0.10 for protan. | Pipeline min 0.090 (brick at the arc centre (527,275)). Engine 0.088 at its core (527,236), at 1280 and 640. The next-worst place is 0.118. p10 is 0.117/0.119, so the level is healthy apart from this segment. In the protan crop, the olive segment still stands out from the pale-blue segments beside it. |
| m2 | Minor | 4-1 base-16 | The beige trail is drawn as dashes that look like aim-guide dots at 640, and it starts where the straight-down guide ends. | Route 0 is `#D9BE82`, alpha 0.7, width 2.8 (4-4 and 4-5 use 0.45–0.55 and 1.6–2.0). Dash L is 0.70–0.71 against the guide's 0.85–0.86, at both sizes. At 640 each dash is a 2–3 px dot, the size of a guide dot. The trail leaves (630,268), under the guide's first contact, and runs east at a shallow angle. On a shallow right-hand aim, the guide lies beside a second dotted line. 3-1's khaki lane passed because it was dimmer. |
| m3 | Minor | 4-3 base-18 (pack) | The pack's `base-18-cleared-1280.png` shows the Level Clear tally, not the cleared board. | `--moment cleared` keeps the lowest orange, but on 4-3 that orange is cleared too, and the level ends (aced, 1,107,240). My re-render gives the same image. I covered it with `--scene-only 1` (see 4-3). The tool fails without warning on this layout, so later packs could miss the same view. |
| n1 | Nit | 4-1 base-16 | The map's two dark arrowheads at the trail ends read as UI pointers. | (500,200) and (1010,480) on the 1280 render; they are clearest on the cleared board. 4-4 and 4-5 erased their maps' arrows. Whether to keep these is the art reviewer's or the owner's call. |
| n2 | Nit | 4-2 base-17 | Pipeline F6 is at the shipped floor. | Worst 0.220 at (314,347) (shipped lowest 0.219, floor 0.20). It is an orange beside the lit right flank of the spire. In the engine render the same place is 0.296/0.303, the stage's lowest but above every stage 3 level's under my measure (0.270–0.346). It is not a readability problem today, but it has no headroom. |
| n3 | Nit | 4-5 base-20 | Answer to the index's question about stars on the frame: there is no line of stars along the frame. | In the 8-frame max composite (0–27 s), the twinkles are scattered single stars, peak L 0.49. About a dozen sit at y 40–50 across x 110–720, and a few at x 710–720. Several are within 20–100 units of the launcher's head. They are well under the guide (0.86) and the ball, and a different shape. |

## Method

- **Renders:** I rendered with the built MoonfallRender `--cache` into my scratchpad. My start renders of 4-1 and 4-5 are pixel-identical to the pack's (AE 0). The worktree stayed clean.
- **Mapping:** level to screen was fitted by least squares on peg centroids. At 1280 it is s 1.2667, offset (133.3, 31.7); at 640 it is s 0.6017, offset (79.0, 30.9). The median residual is under 1.5 px after correcting for the highlight bias.
- **F6 (engine):** the lit face's p80 against the ring's p90 (r+2 to r+9 units), in luma, for the colours as dealt.
- **F9 (engine):** Machado protan at severity 1. For pegs, the core (0.5r) against the ground 12–20 units out, with other pieces masked out. For bricks, the core against the band 3–11 units out. Dealt-orange pegs and brick segments are found by colour.
- **Calibration:** I ran the same method on stage 3 (base-11 to base-15) at both sizes. It gives protan p10 0.108–0.120 and min 0.106–0.118, and F6 worst 0.270–0.346. These numbers run lower than the MR 3 reviewer's method, so read them for ranking.
- **Motion:** 4-1 and 4-5 at 0, 3, 7, 11, 15, 19, 23 and 27 s. Reduce motion for all five at 6 s, plus 7 s for 4-1 and 4-5. I did not run time sequences for 4-2, 4-3 or 4-4; their runtime declares only dust, plus lamp flicker inside the spire on 4-2 and sky stars above y 180 on 4-3.

## Per-level verdicts

| Level | F6 pipeline worst | F6 engine 1280 / 640 | F9 pipeline p10 / min | F9 engine p10 / min (1280; 640) | Motion near pieces | Verdict |
|---|---|---|---|---|---|---|
| 4-1 base-16 Steps of Thal | 0.304 (559,400) | 0.386 / 0.385 | 0.109 / 0.104 | 0.111 / 0.105; 0.111 / 0.106 | Route glints pass ≥ 11.5 units from a peg edge (peak L 0.76 on the dash). Static-peg ring lift ≤ 0.013. Still under Reduce motion (≤ 0.03). | APPROVE after m2 (n1) |
| 4-2 base-17 Spire of the Sands | **0.220** (314,347) | 0.296 / 0.303 | 0.120 / 0.119 | 0.121 / 0.121; 0.122 / 0.121 | The spire's lamps are 3 px points, ≥ 20 units from pegs | APPROVE (n2) |
| 4-3 base-18 Lamps on the Steps | 0.232 (520,240) | 0.392 / 0.384 | 0.107 / 0.101 | 0.110 / 0.106; 0.110 / 0.106 | Stars only above y 180; no flicker at runtime (`flickers 0`) | APPROVE (m3, pack only) |
| 4-4 base-19 The Coliseum | 0.304 (442,337) | 0.362 / 0.367 | **0.101 / 0.083** | 0.094 / 0.081; 0.096 / 0.078 (bricks) | Dust only | **REVISE (M1)** |
| 4-5 base-20 Ul'dah from the Sands | 0.302 (322,250) | 0.359 / 0.344 | 0.117 / 0.090 | 0.119 / 0.088; 0.121 / 0.088 (brick) | Route glints ≥ 46 units from pieces. Ring lift ≤ 0.014. Still under Reduce motion. | APPROVE after m1 (n3) |

### 4-1 The Steps of Thal: APPROVE after m2
- **Pegs:** every kind reads on the rose stone and the blue hatching at both sizes. The engine F6 worst is 0.386 at 1280 and 0.385 at 640, the stage's best. The purple at (494,358) sits on the paved stone and is clear.
- **The bright parchment edge:** the red at lower left is hot but free of pieces and well off the guide's column.
- **m2 fix:** take route 0 to alpha about 0.45 and width about 2.0, as 4-4 and 4-5 do, or lengthen its dashes so they still read as dashes at 640. Either way, start the trail clear of the guide's first 60 units under the ball.
- **Glints:** a glint passes in front of the dashes and slips under pegs without lifting their rings. That is acceptable.
- **Near-repeat question:** at play size the rose-and-blue plan with a trail does not read like 4-5's sea-blue city on desert-rose. They are distinct.

### 4-2 The Spire of the Sands: APPROVE (n2)
- **Readability:** the soft, out-of-focus painting helps here. Blues and oranges separate cleanly from the teal at 640.
- **Pieces on green:** the green bottom haze carries four blues and the purple. Each has F6 ≥ 0.34 in the engine.
- **The guide:** it runs straight down over the dark airship hull, white on near-black, and reads all the way down on the cleared board.
- **Pipeline protan:** 0.120, the stage's best.

### 4-3 Lamps on the Steps: APPROVE
- **The moon:** it sits at (128,58), far off the launcher's axis. Nothing bright crosses the guide's column.
- **Windows by the guide:** two lit parapet windows sit 2–3 px from the guide column, at L 0.31. The guide dots are 0.82–0.86, so they do not compete. The paired dots at the end of the guide also appear on 4-4; they come from the guide itself, not the scene.
- **The lamps:** the lamp glows sit under each ring's centre peg. The centre pegs still measure F6 ≥ 0.39, and the pools are warm without being hot.
- **The red gateway:** the gateway at lower left is the brightest area. It holds no pieces and sits only along the bucket's travel.
- **Cleared view:** I reviewed the cleared scene from `--scene-only` because of m3.
- **The game-art figure rule** is for the art reviewer.

### 4-4 The Coliseum: REVISE (M1)
- **Everything else reads well:**
  - The guide threads the keystone gap clearly at 640.
  - The arena's grey dashed rings stay dim and dashed beside the white guide dots.
  - The green outer ring runs behind the launcher's head, not along the guide.
  - With `--marks`, the brick crescents are legible at 640, so marks-on players are covered.
- **M1 fix:** the 0.101 fallback must not be the only thing passing this level.
  - Apply a broad tone that darkens or desaturates the verdigris inside the band 3–11 units round the three orange-able segments, as 3-5 did for its brick. Alternatively, shift the ring's hue toward blue-teal under the upper arcs. As a last resort, make (575,194) and (195,193) not orange-able and promote two pegs instead.
  - Target: every place ≥ 0.10 and p10 ≥ 0.105.
  - Re-check `bricks_min_margin` afterwards; it should rise from 0.248.

### 4-5 Ul'dah from the Sands: APPROVE after m1
- **Pegs:** they read on both the sea-blue city and the desert-rose sands.
- **The purple peg:** the purple at (500,478) sits on purple-rose ground but separates by lightness: the face is pinker and clearly brighter. A protanope sees it as blue, which is the game-wide limit the marks cover.
- **The guide:** it reads over the city's hatched stone on the cleared board.
- **m1 fix:** apply a local tone under the one orange-able arc segment, or make it blue-only.
- **Stars:** see n3.
