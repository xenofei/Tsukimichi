VERDICT: APPROVE (0 blockers, 0 majors, 3 minors)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| m1 | Minor | 5-2 base-22, 5-5 base-25 (stage) | For a protanope, the orange margin is lower than on any approved level. 5-2's p10 sits exactly on the F9 fallback of 0.101. MR 3 asked that later stages not go lower. | Pipeline p10/min: 5-2 **0.101/0.099**, 5-5 **0.102/0.099**. Before this stage the lowest approved p10 was 0.105 (MR 3), and the shipped lowest minimum is 0.099. My engine measure gives p10/min 0.103/0.100 at 1280 and 0.099/0.096 at 640 for 5-2, and 0.100/0.099 and 0.097/0.096 for 5-5. 4-1 (approved) gives 0.103/0.102 and 0.098/0.097 under the same measure. 5-2's worst oranges, (426,264), (396.6,281) and (384,404), are on the rose-copper land. 5-5's, (141.9,349), (156.5,430.7) and (174.9,338.3), are under the west lighthouses' warm #FFD9A0 glows. In the protan simulation crops every orange still reads olive against navy or grey, and the `--marks` crescents are legible. |
| m2 | Minor | 5-2 base-22 | Route 1's beige dashes end on the straight-down guide column. This is the 4-1 m2 pattern from MR 4, now fixed only on its opacity. | Route 1 ends at (394,300), 6 units from the column at x 400. MR 4's fix stopped the 4-1 trail 68 units clear. While the column oranges (396.6,281) and (396.6,315) stand, they hide the end. On the cleared board, at 1280 and in my 640 cleared render, the dashes run into the guide's 4th dot (y ≈ 412 at 1280). The dashes are alpha 0.45 and width 1.6, so they stay dim strokes beside crisp white dots, but on a shallow left aim the two lines meet. |
| m3 | Minor | 5-5 base-25 | The five lighthouse lamp cores inside the board are as bright as the ball and brighter than the guide dots. One is in the main shot cone, and two sit inside the orbiting peg rings. | Core L is 0.92 at t = 0 and 0.956–0.981 from t = 3 to 19 s (flicker group 1, swing under 0.03). The ball peaks at 0.973 and the guide dots at 0.84–0.90. At 640 the cores are 0.78–0.90. Lamp (442,467) is 53 px from the guide column at 1280, about 25 units from four pegs. In the cleared ±35° cone it is the brightest point (0.986, 19 px over 0.8). The cone's next-brightest level is 23's lantern at 0.75. The 3-2 beacon in MR 3 was accepted because it sat under the ball's L. Suggested fix: lamp `k` 0.95 → about 0.7, so the cores land near 0.75. |
| n1 | Nit | 5-3 base-23 | The buoy lantern stands 10 units beside the guide column. | The core is 13 px right of the column at 1280 and 6 px at 640. It is L 0.73 against dots of 0.82–0.85, warm against white, on a visible post, and its flicker swings only 0.007. It reads as a lamp at both sizes. No action needed. |
| n2 | Nit | 5-3 base-23 | Pipeline F6 is 0.001 under the shipped lowest. | Worst 0.218 (shipped lowest 0.219, floor 0.20). In the engine the lowest is 0.274 at 1280 (orange (353.7,337.5)) and 0.262 at 640 (orange (532.6,377.6)). Not a readability problem. |
| n3 | Nit | 5-2 base-22 | The map lettering beside the guide is drawn as light speckle. | "Ihsulew" and "on Shore" start 15–20 px right of the column at 1280 and turn into grey grain at 640. Their shape differs from the guide's even dots and they are dimmer. Visible only on right aims. |
| n4 | Nit | 5-3 base-23 | Cleared oranges leave pale lamp heads at their places. | The lamp heads at (112,224), (352,188.1), (612,272.9) and so on are about half a peg wide and sit on posts. The pipeline's ghost worst is 0.055 at (352,188), inside MR 3's range (≤ 0.058). In my 640 cleared crop they read as street lamps. |

## Method

- **Renders:** I used the built MoonfallRender with `--cache` into my scratchpad. My `base-21` start render is pixel-identical to the pack's. I made 1280 time sequences at 0, 3, 7, 11, 15 and 19 s for all five levels, plus 1.5/10/16/17 s for 5-2 and 1.5/2/4.5/5/9/13/17 s for 5-5. I rendered 640 at 7 s for all five, plus 2/5/9/13/17 s for 5-5. I also rendered reduce motion at 6 s (and at 12 s for 5-1, 5-3 and 5-5), `--marks` and `hud`, and 640 cleared boards for 5-2, 5-3 and 5-5. The worktree stayed clean.
- **Mapping:** the same as MR 4. At 1280: s 1.2667, offset (133.3, 31.7). At 640: s 0.6017, offset (79.0, 30.9). Checked on 5-3's (410,303.3) orange, which lands at (652,414).
- **F9 (engine):** Machado protan at severity 1. Oklab chroma distance between the core (0.5r) and the median of the ring 12–20 units out, with other pieces masked. Bricks use the band 3–11 units out. It tracks the pipeline within 0.003: for 5-1 it gives 0.137/0.132 against the pipeline's 0.140/0.132. I calibrated it on MR 4 (16 to 20 at 1280): 0.103/0.102, 0.120/0.119, 0.109/0.106, 0.106/0.104, 0.113/0.112.
- **F6 (engine):** the face's p80 against the ring's p90 (r+2 to r+9), in luma.
- **Shot cone:** ±35° from the launcher, y < 690 (above the bucket lamp), leaving out the guide column, measured on the cleared board.

## Per-level verdicts

**5-1 base-21, The White Towers: APPROVE.** Engine F6 worst is 0.261/0.239 at 1280/640, a purple at (528,300); the lowest orange is 0.288/0.250. Protan p10/min is 0.137/0.132 and 0.136/0.130, the healthiest in the stage. The brick at (672,404) holds in the pipeline (0.132). The lower palette chroma does not show in readability. The guide runs over dark navy at both sizes. The pale tower plume at x ≈ 685 sits 45 px off the column and peaks at 0.645, the cone's maximum, well under the dots. Motion is limited to the corner fan of shafts, the teal foot mist and dust. Reduce motion is still: under 0.005 change between 6 and 12 s.

**5-2 base-22, The Strait of Merlthor: APPROVE (m1, m2, n3).** The new foot haze (#B4A0C8, r 130 at (400,640), k 0.2) is a faint violet under the bucket lane. It is dark (the cone's p99 is 0.29), does not compete with the bucket lamp, and reads the same at 640 and on the cleared board. Engine F6 is the stage's best, 0.356/0.330. The three route glints (every 30 s, at 0, 8 and 15 s) never brightened any pixel by more than 0.23 in my frames, so they stay faint near pieces. The guide is crisp over the dark land at both sizes. For m1, a local lift of the land's chroma under the three central oranges, or moving one candidacy off the copper, would restore margin. For m2, cut route 1's last leg so it stops at (370,288).

**5-3 base-23, Lights on the Water: APPROVE (n1, n2, n4).** The moon is at the top left, about 320 px from the launcher and about 100 px above the nearest pegs. It never meets the guide. The reflection streaks behind each column are static, and pegs on them hold (engine F6 at least 0.262, protan 0.111/0.110 and 0.109/0.106). The lit windows are tiny cool squares, never peg-sized at 640. Motion comes from the moon's three shafts (slow), the stars above y 186, the mist band at y 530–594 and the lantern flicker. All of it stays out of the pieces' faces. Reduce motion is still.

**5-4 base-24, The Arches of the Bridge: APPROVE.** The lower palette chroma does not show in readability. Engine F6 worst is 0.272/0.257 at the rose-sky orange (250,130). Protan is 0.116/0.111 and 0.114/0.108. The mist band now starts at y 518 and stays clear of the lowest row (y 505). The rigging lines cross under the bottom arcs and pegs: they are dark and thin and leave the pieces clear (framecheck's darkened clearance of 6.69 has precedent in 3-3's 6.81). The highest ghost in the game (0.077 at (600,505)) shows nothing on the cleared board. The purple beside the guide at (690,302) is a different hue and size from the dots.

**5-5 base-25, The Lighthouses of Vylbrand: APPROVE (m1, m3).** The movers orbit the two lamps with an orbit radius of 61 units. The lamps' 9-unit halos never touch them, and sampled along the orbits the movers' F6 is at least 0.26 at both sizes. Reduce motion keeps the movers moving, as gameplay should, and freezes the scene apart from the movers. The compass rose is 150 px from the nearest peg. The guide crosses the pale inlet at y 255–300 and stays visible at 640, where its dots are dimmer. The route's glint (every 24 s) did not show near pieces. For m1, ease the k of the west glows (#FFD9A0, 0.035–0.04) under the oranges at x 140–175.

## Stage note

These are the stage's protan p10s, against MR 4's 0.107–0.121:

| Level | Protan p10 |
|---|---|
| 5-1 | 0.140 |
| 5-2 | 0.101 |
| 5-3 | 0.109 |
| 5-4 | 0.122 |
| 5-5 | 0.102 |

MR 6 should not set a new low. A guard at p10 ≥ 0.105 would hold the floor where MR 3 left it.