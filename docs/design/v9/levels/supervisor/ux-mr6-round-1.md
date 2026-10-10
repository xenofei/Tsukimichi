VERDICT: APPROVE (0 blockers, 0 majors, 3 minors)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| m1 | Minor | 6-5 base-30 | Protan orange p10 is under the 0.105 guard that MR 5 set ("MR 6 should not set a new low"). It is the lowest p10 of any level approved after its fixes. | Pipeline p10/min **0.103/0.102**; the worst place is (434.2,334.9), a static candidate inside the gate. My engine measure reads 0.112/0.109 at 1280 and 640. That measure runs about 0.008 above the pipeline on every level of this stage, so calibrated it is about 0.104/0.101. The weakest oranges are all on the teal gate face: (422,377), (341,381), (459,384). In the protan crop they still read olive-yellow on grey, and the `--marks` crescents are legible. The MR 5 round 1 precedent (p10 0.101) was a Minor. Fix: lower the teal region's chroma (0.14) under the inner ring, or apply a local tone of about 0.95 under the inner-orbit band, until p10 is ≥ 0.105. |
| m2 | Minor | 6-4 base-29 | Protan minimum is 0.092, under the 0.10 per-place target and the lowest single place in the shipped game (0.099). The same place is also the level's F6 worst for every kind. | Pipeline min 0.092 at (514,431). This is the far end of slider 16, (487.4,408.5)→(514.6,432.1), period 8 s, so the orange rests there at each turn. It rests on the glowing head of meteor 3. Engine at the slide end: 0.101 at 1280 and 0.102 at 640, which calibrates to about 0.093. Pipeline F6 for that place is purple 0.230 and orange 0.259. p10 is healthy (0.110). In the protan crop the orange turns olive over an olive-grey glow, still brighter than it. MR 4's 4-5 single arc segment (0.090) was a Minor. Fix: shorten slider 16 so it stops about 15 units short of the head, or dim the head's glow under it. Either should also lift the F6 worst. |
| m3 | Minor | 6-3 base-28 | The Gridania city light sits 14 units right of the guide column, level with the guide's 6th dot, and is brighter than the dots at 640. | Light (414,262): #F4ECFF, core 2.2, k 0.95, static. Core L is 0.84 at 1280 and **0.96 at 640**. Guide dots are 0.83 (median) at 1280 and 0.78 at 640, and the dot over the Gridania crest falls to 0.63. At 640 it is 8 px off the column and reads as a second white dot beside the guide. This is the MR 5 round 2 m1 pattern (lamps brighter than the dots near the column, there 41 units away), but closer and cool white like the dots. The other city lights are 54 units or more from the column (Mor Dhona 0.95/0.99, Ala Mhigo 0.94/0.95). Fix: drop Gridania's k to about 0.55 so its core sits at 0.75 or less at both sizes, or warm it toward the glint colour #FFE6B0. |
| n1 | Nit | 6-3 base-28 | Ley dashes and route glints cross the guide column. | Static dashes cross x 400 at about y 253 (Ishgard–Gridania, p90 L 0.54), y 327 (Mor Dhona–Ala Mhigo, 0.28) and y 401 (0.39). All are well under the dots (0.83) and drawn as dashes, not dots. The glints cross the column at about 5, 13, 29 and 41 s, with peak L 0.42–0.68, warm and lasting about 1 s. At 11 s a glint lifts the ring of peg (347.8,220.6) by 0.21, 6 units from its edge (round 1 precedent: at least 11.5 units). No action needed. |
| n2 | Nit | 6-4 base-29 | The lake reflection under the bucket lane has fine horizontal scanline striping. | Rows y ≈ 700–770 at 1280, below every piece. It does not affect play. It is texture noise beside the bucket, so I pass it to the critic. |
| n3 | Nit | 6-5 base-30 | The two full-moon discs are the most peg-like shapes left on the cleared board. | (293.9,146.5) and (506.1,146.5): L 0.54 grey, about 1.4× peg size. A blue face is 0.78. Inside the ring of twelve phases they read as moons at both sizes. No action needed. |
| n4 | Nit | 6-5 base-30 | One twinkling star sits 7 units from a peg edge. | Peg (209.8,180.7): the star lifts its ring by 0.095 (peak L 0.275) at 7 and 19 s. The star mask keeps the twelve discs clear, but this peg is not on a disc. A 15th mask term (a disc at the peg, r 20) would clear it. Optional. |

## Method

- **Renders:** MoonfallRender (DLL built 09:34). I checked that its embedded level and recipe JSONs match the files on disk byte for byte. Into my scratch folder with `--cache`: `--moment start` at 0/3/7/11/15/19/23/27 s at 1280 for all five, plus 1–10 s for 6-4 and 6-5; start and cleared at 640; Reduce motion at 6 and 12 s; `--marks`; and 6-3 cleared at every second from 0 to 59 s to follow its glints. The worktree was not touched.
- **F6 (engine):** Oklab L, face core (0.5r) against the median of the ring 12–20 units out, with other pieces masked out. Bricks use the band 3–11 units out.
- **F9 (engine):** Machado protan at severity 1, Oklab chroma distance between core and ring. It reads about 0.008 above the pipeline on every level here (6-1 gives 0.127 against 0.119).
- **Motion:** per-pixel L range over the frames inside each static peg's ring (r+2 to r+10), with mover paths masked out.

## Per level

**6-1 base-26, The Armillary: APPROVE.**
- **F6:** the pipeline worst is 0.234 (purple 0.8x, (344,448)). The engine worst is orange (420,365): 0.235 at 1280 and 0.250 at 640. The lowest blue is 0.259.
- **Protan:** pipeline 0.119/0.116; engine 0.127/0.123 at 1280 and 0.128/0.126 at 640. The worst place is the orange at (457,214) on the teal hoop.
- **Guide and moon:** the guide runs down the teal hub. Dots are at least 0.82 (1280) and 0.80 (640) against ground p95 0.54, so they stay crisp at both sizes. The moon is at the top left, about 330 px from the launcher and well clear of the shot cone.
- **Motion and region chroma:** ring lift is at most 0.065 (p99 0.007). Reduce motion is still (at most 0.014 change between 6 and 12 s). Region chroma 0.32 → 0.3 does not show.

**6-2 base-27, The Wheel of the Twelve: APPROVE.**
- **F6 and protan:** the stage's best readability. Pipeline F6 worst 0.272. Engine: worst orange 0.331/0.329, lowest blue 0.337. Protan 0.135/0.131 (pipeline) and 0.145/0.142 (engine).
- **Signs and stars:** the sign glows are soft and sit under the pegs. The 13-term star mask works: ring lift is at most 0.069 (p99 0.004).
- **Guide:** it crosses the sun hub at the centre. Dots are at least 0.81 against p95 0.50, and the hub disc stays darker than the dots.
- **The r 10 → r 9 hub moons:** they read the same at 640.
- **Reduce motion:** still.

**6-3 base-28, The Seer's Chart: APPROVE (m3, n1).**
- **F6 and protan:** pipeline worst 0.285; engine lowest orange 0.315/0.334. Protan 0.130/0.128 (pipeline) and 0.137/0.132 at 1280, 0.139/0.131 at 640.
- **Cloud sea:** at chroma 0.3 it still reads as green at 640, as the index asked me to check. The four arch bricks over it hold a lightness margin of 0.27–0.37 at 1280 and 0.27–0.52 at 640 (face 0.73–0.76 against ground p90 0.20–0.48), and each has a dark edge.
- **Lettering:** the map lettering is speckle again (MR 5 n3, accepted). It stays under the pegs.
- **Guide:** the guide is the stage's weakest at the Gridania crest (m3, n1). Elsewhere the dots hold 0.83 against p95 0.42.
- **Motion:** with no dust, motion is just the four glints. Reduce motion is still.

**6-4 base-29, Meteor Shower: APPROVE (m2, n2).**
- **F6:** pipeline worst 0.230, every kind at (513,431) (m2). Engine lowest orange 0.346 at 1280 and 0.340 at 640. Purple 0.508.
- **Protan:** p10 0.110 (pipeline) and 0.114/0.116 (engine).
- **Meteor heads:** soft and dim. Next to the green pegs they never read as peg-like discs: the green at (971,357) is crisp and mint beside the diffuse teal head at (1015,368).
- **Guide:** the streak behind the guide peaks at 0.44 against dots of 0.76 or more (1280) and 0.74 or more (640).
- **Movers:** the twelve sliders move along their own streaks. Under Reduce motion only the movers and the bucket change; I checked this with a diff overlay.

**6-5 base-30, The Moon Gate: APPROVE (m1, n3, n4).**
- **New beams:** k 0.03, about 0.026 at p99 in the peg rings. They are faint and never meet a piece face.
- **Darkened rim band:** it holds. My crude face-minus-ground margin for a purple at the crown's ends is 0.26–0.27 at 1280 and 0.25–0.26 at 640. Under the same crude measure the untoned crown top, about y 226, reads 0.236/0.224 because the rim lines are brighter there. Unverified with the runtime F6: confirm the crown top's runtime figure at 640.
- **F6 and guide:** engine lowest orange 0.358/0.374. The guide crosses the pale gate disc (L 0.51) with dots at 0.83/0.81, which is enough.
- **Orbit movers:** both orbit rings keep moving under Reduce motion, as play needs, and the scene stays still.
- **Protan:** see m1.

## Stage note

| Level | Protan p10 / min (pipeline) | Engine F6 worst 1280 / 640 |
|---|---|---|
| 6-1 | 0.119 / 0.116 | 0.235 / 0.250 |
| 6-2 | 0.135 / 0.131 | 0.331 / 0.329 |
| 6-3 | 0.130 / 0.128 | 0.315 / 0.334 |
| 6-4 | 0.110 / **0.092** | 0.346 / 0.340 |
| 6-5 | **0.103** / 0.102 | 0.358 / 0.374 |

The stage's two lowest protan figures sit in the two hardest levels, and both are under the guard. Fixing m1 and m2 in round 2 would bring the stage back to the MR 5 floor: p10 ≥ 0.105 and every place ≥ 0.10.

My renders and crops are in `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr6\`.