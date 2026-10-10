VERDICT: APPROVE (0 blockers, 0 majors, 1 minor)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| m1 | Minor | 6-1 base-26 | The gold armillary lowered 6-1's F6. It is now the stage's thinnest, and it sits just over the floor at 640 when the purple lands on the heart peg. | Pipeline worst is 0.205 (purple at 0.8x, (344,448)). The engine reads that place at 0.275 (1280) and 0.277 (640). The engine's real worst is the r12 heart peg (400,330), which sits on its tan halo: ground median 0.450 at 1280 and 0.461 at 640, the brightest ground under any peg in the stage. A purple face there gives 0.226 at 1280 and **0.208 at 640**; a blue gives 0.30/0.29. The heart can turn purple, because any unlit blue can (`MoonfallGame.cs:1373`). Round 1's worst was 0.235/0.250. The purple-on-gold hue contrast and the star mark keep it readable in the crops. Optional fix: tone the halo under the heart about x0.92, the same way 6-5's rim band was toned. |
| n1 | Nit | 6-5 base-30 | Round 1 n4 is fixed where it was, but the extra mask term moved a twinkling star next to another peg. | (209.8,180.7): lift 0.147 → 0.009. The new spot is (175.5,231.6) r7: a star at (169.6,216.2), 9.5 units from the peg's edge, L 0.255 → 0.40 (lift 0.15) every 3 s. In round 1 that peg's ring was 0.010. One mask term is left (15 of 16). After any change, check every peg ring again, since the mask re-deals the stars. |
| n2 | Nit | 6-2 base-27 | A new twinkle sits between the top ray moon and the aim guide at 1280. | (389,282) at 11 s and 23 s: L 0.42 → 0.53 (round 1: steady 0.41). It is 6.7 units from the edge of moon (401,290) and 11 units left of the guide column. The guide dots are 0.83 or more, and the star is soft, so it does not read as a dot. At 640 it stays at 0.36. The recipe is unchanged; the star moved because the piece set changed (6 new bricks). |
| n3 | Nit | 6-1 base-26 | A faint star twinkles 8.6 units from peg (604,330). | L 0.25 ↔ 0.36 (lift 0.11). Ring lift p99 is 0.007. No action. |
| n4 | Nit | 6-3 base-28 | Protan has fallen to the guard after the green moved onto the land. | Pipeline 0.130/0.128 → **0.110/0.105** at (302,164), Ishgard's top moon on the green land. Engine: 0.121/0.112 at 1280 and 0.118/0.112 at 640, about 0.104 once calibrated. In the protan crop the orange turns olive over dark land with olive speckle, and it still reads by lightness. It meets the guard (p10 ≥ 0.105, every place ≥ 0.10) and is now the stage's lowest. No action. |
| n5 | Nit | 6-3 base-28 | Round 1 n1 stands: the 11 s glint beside (347.8,220.6) peaks a little higher. | Lift 0.285 at (353.5,205.9), 8.7 units from the edge (round 1: 0.21 at 6 units). It is warm and lasts about 1 s. Ring lift p99 is 0.000. No action. |

## Round 1 findings

| R1 id | Status | Verification |
|---|---|---|
| m1 6-5 protan p10 0.103 | **Fixed** | Pipeline 0.114/0.113. Engine 0.124/0.120 at 1280 and 0.121/0.119 at 640, about 0.113 calibrated. The bluer far-sky hue works better than my chroma suggestion did. |
| m2 6-4 protan min 0.092 | **Fixed** | Pipeline min 0.106 at (514,431). Engine on slider 16 over 0–10 s: 0.120 at the near end, **0.114** at the far end (t = 4 s), and 0.116 at 640. F6 there is 0.342. The teal lake ring, not the meteor head, was the cause; the response's diagnosis is right. |
| m3 6-3 Gridania light | **Fixed** | Core L is 0.66–0.69 at 1280 (static over 0–27 s) and **0.63 at 640** (was 0.96). Guide dots: 0.83–0.88 at 1280, 0.83–0.86 at 640. It no longer reads as a second dot. |
| n1 6-3 dashes and glints | Kept, no action | See n5. |
| n2 6-4 lake scanlines | **Fixed** | Lake row-mean step p90 1.39 → 1.02. The crop shows smooth swells and no striping. |
| n3 6-5 full-moon discs | Kept, no action | Unchanged. |
| n4 6-5 star beside a peg | Fixed in place, moved | See n1. |

## Method

- **Renders:** MoonfallRender `--cache` into `scratchpad\mr6r2\`, 97 frames. I checked that the Release Core.dll (17:32) embeds the on-disk JSONs byte for byte: base-26 to base-30 and all five scene recipes, including base-27 from the stage pass. Frames:
  - start at 0, 3, 7, 11, 15, 19, 23 and 27 s at 1280 for all five levels;
  - 0–10 s for 6-4 and 6-5, and 1–14 s at 640 for 6-5;
  - 640 start and cleared, 1280 cleared, Reduce motion at 6 and 12 s, and `--marks`.
- **Measures:** the same as round 1. F6 is Oklab L of the core against the ring 12–20 units out, with other pieces masked. Protan is the Machado severity 1 simulation with a chroma distance. Motion is the L range in the r+2 to r+10 rings, with mover paths masked.
- **New this round:** a candidate-aware hypothetical F6 that puts each kind on every place it can occupy, using engine face L (orange 0.686, purple 0.676, blue 0.75). I added it for the gold regression the response asked me to look at.

## Per level

**6-1 base-26, The Armillary: APPROVE (m1, n3).**
- **The gold:** gold and dusk replace the teal. The gold sits only on the lum-0.22–0.34 frame lines inside the disc, so it never fills a peg ring.
- **Protan:** it improved: pipeline 0.119 → 0.131/0.124, engine 0.148/0.141 at 1280 and 0.147/0.142 at 640. The worst place is still the orange on the hoop at (457,214).
- **F6:** actual deal worst is blue 0.269 at 1280 (the heart) and orange 0.260 at 640.
- **Hypothetical F6:** orange on the candidate (362,330) gives 0.263/0.250; purple on the heart gives 0.226/0.208 (m1). I checked the pipeline's (344,448) visually: gold rods cross pale violet, and the peg is crisp.
- **Bottom of the board:** the amber dusk lies below the lowest ring, and the bottom pegs keep at least 0.30.
- **Moon:** it now has maria (p95 0.78/0.81). It is at the top left, far from the launcher.
- **Guide:** the 640 profile matches round 1 exactly (dots 0.69–0.78 over the brass arc's 0.53), so no regression.
- **Reduce motion:** still (max 0.018).

**6-2 base-27, The Wheel of the Twelve: APPROVE (n2).**
- **F6 and protan:** engine worst orange 0.341/0.328; protan 0.146/0.143 at 1280 and 0.146/0.145 at 640. Pipeline: 0.269, 0.134/0.132.
- **New rim bricks:** six bricks sit clear on the ticked rim, with blue brick margin 0.371.
- **Small pegs:** the r 7.875 faint stars and r 8 ray moons read the same at 640.
- **Guide:** it runs down the hub. Dots median 0.83 against ground p95 0.37.
- **Motion:** ring lift p99 0.005. Reduce motion is still (0.013).

**6-3 base-28, The Seer's Chart: APPROVE (n4, n5).**
- **F6:** engine orange worst 0.325/0.334 and hypothetical worst 0.263/0.261 at (214,306). The 0.099 "blue" in my blob pass is the arch brick's own shadow; the crop shows a crisp brick.
- **The green:** green on the land puts a strong green field at the top right, behind the arch bricks. The bricks stay pale with dark edges. Green pegs keep at least 0.37 on L.
- **Guide:** the column profile is unchanged; m3 is fixed.
- **Reduce motion:** still (0.012).

**6-4 base-29, Meteor Shower: APPROVE.**
- **F6 and protan:** engine worst orange 0.357/0.341; protan 0.126/0.120 at 1280 and 0.127/0.116 at 640. Pipeline: F6 0.236, protan 0.119/0.106.
- **Lake:** the teal lake and airglow read as night water.
- **Guide:** the streak peaks well under the dots (0.80 or more against ground p95 0.32).
- **Reduce motion:** outside the mover paths, the change matches round 1 exactly (max 0.066, the same pixels), which is mover shading and was accepted then.

**6-5 base-30, The Moon Gate: APPROVE (n1).**
- **F6 and protan:** engine worst orange 0.375/0.383 and hypothetical 0.349/0.348; protan per m1 above.
- **Far moon:** a muted teal-grey disc with seas, median L 0.39/0.40, darker than every peg. At 640 it reads as a moon, never as a peg. The crown brick shields it from the guide.
- **Beams:** k 0.06. Ring lift p95 is at most 0.039 near the gate floor, and they never cross the guide column. In motion they are still faint, but I found nothing to fix for readability.
- **Reduce motion:** outside the orbits it is still (max 0.028).

## Stage

| Level | Protan p10/min (pipeline) | Engine protan p10/min 1280 · 640 | F6 pipeline | Engine F6 worst (deal) 1280/640 | Engine hypothetical worst 1280/640 |
|---|---|---|---|---|---|
| 6-1 | 0.131/0.124 | 0.148/0.141 · 0.147/0.142 | 0.205 | 0.269/0.260 | 0.226/**0.208** (purple, heart) |
| 6-2 | 0.134/0.132 | 0.146/0.143 · 0.146/0.145 | 0.269 | 0.341/0.328 | 0.284/0.273 |
| 6-3 | 0.110/0.105 | 0.121/0.112 · 0.118/0.112 | 0.276 | 0.325/0.334 | 0.263/0.261 |
| 6-4 | 0.119/0.106 | 0.126/0.120 · 0.127/0.116 | 0.236 | 0.357/0.341 | 0.313/0.308 |
| 6-5 | 0.114/0.113 | 0.124/0.120 · 0.121/0.119 | 0.257 | 0.375/0.383 | 0.349/0.348 |

- **Protan:** every level is at or over the MR 5 guard (p10 ≥ 0.105, every place ≥ 0.10). The floor is now 6-3 (n4), which meets the guard rather than falling under it.
- **F6:** the floor moved to 6-1's gold heart (m1), which passes but is the one place worth a tone.
- **Motion:** all three twinkle nits come from re-dealt star layouts. The stage would benefit from a pipeline check of star distance to every peg ring, rather than one mask term per review.
- **Unverified:** the beams and glints in real-time motion, and purple on the heart in a live deal (computed from the ground plus the measured purple face).

Renders and crops: `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr6r2\`
