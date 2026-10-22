VERDICT: APPROVE (0 blockers, 0 majors, 1 minor)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| m1 | Minor | base-28 6-3 | Left over from round-1 m2. The top-right green is still the most saturated mass on the board at 640. The fold-in fixed the rest of the land/sea read. I accept it as is, because the colour-balance gate blocks taking more green off. | `engine-640`: the green block covers about x 335-505, y 45-120. Response m2: cloud 0.1 gives a second-jewel share of 0.137 against the 0.15 gate. |
| n1 | Nit | base-30 6-5 | Carried from round 1. The gate beams still don't show in any still frame. | Crop of `engine-1280` x 225-1055, y 80-400: no shafts can be seen above the gate at k 0.06, widths 22-30. Unverified in motion, as the response says. |
| n2 | Nit | base-26 6-1 | The new dusk band reads as two separate amber-red glows, one each side of the stand, rather than one band of sky behind the hills. | `engine-1280` crop y 600-750: two soft blobs at about x 260-470 and 790-1010 with red edges, and a dark gap at the stand. They read as a horizon at 640. |
| n3 | Nit | base-27 6-2 | Each lower-diagonal rim brick is a single short stub. They read as loose capsules more than as part of the ticked rim. The two-brick upper arcs read well. | `engine-1280` (495,545) and (787,543). |
| n4 | Nit | base-26 6-1 | F6 worst is 0.205: it passes, but by only 0.005 over the 0.20 floor. In the stills the purple reads clearly on the violet sky. That is UX's call, and I note it only because the response asked. | `index.md`: F6 0.234 → 0.205. In `engine-1280` the purple peg at (755,513) stands out. I can't judge the 0.8x reading from stills. |
| n5 | Nit | base-29 6-4 | The fixed lake is now very dark, and the mirrored range and meteors barely show. It reads as water, but quietly. Optional: let a few meteor heads show in the reflection. | `cleared-1280` crop y 690-760: there is a faint range reflection, one faint streak, and no scanlines. |

## Round-1 findings checked

| R1 id | Status | Evidence |
|---|---|---|
| M1 opener and finale share one picture | **Fixed** | Jewels 296/55 against 312/228. At 640 side by side, 6-1 is a gold-brass cage on blue-violet with an amber horizon, and 6-5 is a teal gate in a magenta nebula. They no longer read as the same board. `cleared-1280` for 6-1 finally reads as an armillary: gold rings, rods, meridian, heart globe and tripod stand. |
| M2 6-2 thinnest board | **Fixed** | Sweep mean 6.4 → 7.6 and median 6 → 7, which meets the 7.5 acceptance. Bucket 138 → 118. Six rim bricks now give the wheel some body, and the 35 candidates and ch-03 are intact (greedy 27/48). Both steps on either side resolve: 1.45 and 1.63 ± 0.41. |
| m1 6-4 striped lake | **Fixed** | No scanlines at 1280 or in the cleared board. The lake is a dark, smeared mirror of the range, and its jewel region is now teal at chroma 0.04. See n5 for the remaining quietness. |
| m2 6-3 green seas / land reads as water | **Fixed in part; residual accepted as m1** | The green now sits on the land mask, the southern sea is lavender parchment, and the continent reads as a dark landmass with green coasts. Grey cloud texture now sits under the arch bricks at 1280 and faintly at 640, so they read as cloud crests and not as stray rainbows. Only the top-right green mass is left. |
| m3 plain moons | **Fixed** | 6-1's moon has maria (crop 250-370, 105-225). 6-5's far moon on the cleared board has seas, craters, a darkened limb and a tight corona. It is small, but it is a moon now. |
| n1 6-5 beams | **Not verifiable from stills; carried as n1** | Brighter and wider on paper, but still not visible in stills. |
| n2 6-3 stuck rescues | **Fixed** | Stuck fires 73 → 54, sweep sticks 8 → 5, and the ship and isle holdouts are at 0.18 each. |
| n3 6-1 spoke pegs read as scatter | **Fixed** | On the cleared board the spoke glows run through to the tip moons. At start the equator row (y 448 on screen) reads as a line. |
| n4 6-4 protan 0.092 | **Fixed** | Min 0.106 and p10 0.119, over the 0.105 guard. |

**Regression check.** 6-3's sweep mean went 8.5 → 8.3, which is fine. Its top holdout is now (214,306) at 0.23, under the gate. 6-2's low orange (526,440) is at 0.21, under the 25% gate. 6-2 lost no glyph readability, and the twelve signs still read at 640. I see no new visual faults in the five start or cleared renders apart from the nits above.

## Per level

**6-1 The Armillary (base-26), APPROVE (n2, n4).** The regrade is a real gain. The cleared board now shows a brass instrument on a stand against dusk, which is the subject. On the dealt board the cage is a fine tan wireframe under the pegs. That is quieter than lavish, but it is legible, and it is now clearly a different object from the finale's gate. The layout is unchanged, and it is still the generous opener: sweep mean 10.1, greedy 29, pooled 24.7.

**6-2 The Wheel of the Twelve (base-27), APPROVE (n3).** It is still the stage's most charming board. The upper rim arcs give the first shot a real thing to hit, near the launcher's natural line, and the shot feel is now in line with the rest of the game (sweep mean 7.6, median 7). The stage-pass radius trims (rays r 8, faint stars r 7.875) don't show in the picture. The interior is still only the hub and four rays, which fits the subject, since the hub is the wheel's centre.

**6-3 The Seer's Chart (base-28), APPROVE (m1).** It now reads as a night chart: a dark continent with green coasts on amethyst seas, cloud crests under the arches, and the five-city loop and Mor Dhona's lines over it. Shrinking the ring moons improved feel (fewer rattles) without thinning the board (sweep mean 8.3). The top-right green is the one thing that still pulls the eye, and I accept it under the gate's limit.

**6-4 Meteor Shower (base-29), APPROVE (n5).** The spectacle is unchanged and still the best in the stage: radiant streaks, the Plough, the lit range, and the first slide movers. The lake glitch is gone.

**6-5 The Moon Gate (base-30), APPROVE (n1).** The layout and ramp are unchanged: held out 17.4, greedy 12/48, finale gap 3.17. The bluer far sky still sits well inside the magenta nebula, the far moon now earns its place at the centre of the reveal, and the reveal of the moon's phases on the cleared board is still the stage's payoff. Now that the opener no longer shows the same picture, the finale's image is its own.

## The stage as a set

- **Theme.** The order still holds: instrument, zodiac, chart, sky, gate. That is Urianger's reading of the heavens. Every scene is set nowhere, with 1 game painting of 5.
- **Variety.**
  - The palettes are now five distinct nights: violet with gold and amber, navy with magenta, amethyst with green, navy with airglow teal, and magenta with teal.
  - The mechanics still ramp: a static brick lattice, then rim bricks (6-2 now has some), then arches, then slide movers, then orbit movers with a brick crown.
  - Three centred ring compositions remain (6-1, 6-2, 6-5). They are now told apart by colour and material, so I accept them.
- **Ramp.**
  - Pooled: 24.7, 23.25, 21.62, 20.57, 17.4. Steps: 1.45, 1.63, 1.05, then a finale gap of 3.17.
  - The mean is 21.51 against the charter's 21.6, 0.60 under MR 5, inside the charter's 0.5 to 1.0.
  - The ease-back from 5-5 is 7.0, at the floor of the charter's band.
  - The 6-2 → 6-3 step I flagged now resolves comfortably.
- **Feel.** The two thinnest boards in the game are still here (6-4 at 7.0, 6-2 at 7.6). Both are now acceptable: 6-4 has its movers, and 6-2 has its rim bricks.
- **Finale.** Earned, distinct and spectacular. This stage meets the bar set by the approving MR 3 reviews.

**What I didn't check:** motion in every level (6-5's beams, the movers' feel, glints and star masks), real play feel, F6 at 0.8x on 6-1, and the critic's fresh seed blocks. I judged from stills, the report figures and `stage-mr6.txt` only.
