VERDICT: APPROVE (0 blockers, 0 majors, 1 minor)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| m1 | Minor | base-25 5-5 | The finale's lighthouses still don't read as lights when the board is still, and they are now colder than in round 1. The turning beams are imperceptible in motion. The round-1 "tower glyph on the plate" option was not taken up. | My `--moment cleared` renders: lamp peaks are near-neutral grey, (295,346) rgb(172,175,174), (556,543) rgb(172,176,166) and (947,570) rgb(186,184,177). In round 1 (555,543) was rgb(249,237,204). The lamp colour is #FFE6B0 at k 0.38, and the warm glows were eased (0.04 → 0.025, 0.035 → 0.022). On `engine-640` the lamps are specks. Beams in motion: frames at 0 s and 6 s differ by p90 3 and p99 6 of 255 in the west sea (x 240-700, y 330-700), so the sweep is real but invisible. |
| n1 | Nit | base-24 5-4 | It is lamplit now, but only just. The six lights are 3-4 px points. The two warm glows lift the deck only slightly and it stays blue. A rose band remains under the arches. About six pegs still sit on no labelled feature. | Cleared, 100x60 means: (345,450) rgb(69,78,118) → (87,91,123), and (740,440) (61,71,113) → (82,86,119). The foot band: (640,650) is rgb(108,66,78) and (400,650) is (96,55,66). Unlabelled pegs on the `trace.png` sheet are at about (155,170), (357,220), (310,290), (395,295), (600,390) and (600,420) in trace coordinates. |
| n2 | Nit | base-23 5-3 | The reflection columns are warmer, but they still read as khaki, not gold. | The brightest 200 px of the x 360-400 column (y 450-650) average rgb(123,112,95): hue 37°, saturation 0.13. In round 1 they were hue 27°, saturation 0.08. F6 is at 0.206, so brightening is closed, but saturation may not be. |
| n3 | Nit | base-25 5-5 | The top holdout (459,390) is the last orange in 24% of lost games. Losing on the same peg about once in four losses is a feel cost, though it is under the gate. | The response, critic n3. |
| n4 | Nit | base-21 5-1 | The beams (k 0.08) can't be seen on the still board. Parts of the towers are still lavender-slate. As a whole the castle now reads grey-white, so this is accepted. | Cleared (470,470) rgb(78,81,112), round 1 (71,78,113). The foot is (950,700) rgb(2,101,95), sea teal (round 1 (0,76,46)). |
| n5 | Nit | stage | Two green feet and a near-shared jewel pair remain on 5-3 and 5-5. They are two levels apart. Accepted. | Jewels 255/171 and 246/168. Region hues #48B07E and #3FAE74 (5-5 chroma 0.08 now). |

## Round-1 findings

| R1 id | Status | Check |
|---|---|---|
| M1 (5-3 lamps cold) | **Fixed** | The lamp heads are warm on the cleared board. (495,272) was rgb(106,120,145) and is now (186,168,134). (724,268) is now (200,179,138) and the jetty lamp (908,378) is (198,177,136). The six flickering `lights` match the file (the `harbour-lights.json` `dress.lights`). No moon stands on a fire, and every quay lamp shows at 640. The three unflickered fires (Q2, Q4, Q6) are an F5 limit and are accepted. The residual is n2. |
| M2 (5-4 not lamplit, scatter, ruled deck, rose smear) | **Fixed; residual → n1** | Six warm points burn on the painting's lamps and windows: (345,450) is rgb(255,247,194) and the dome drum (880,210) is (255,249,206). The deck is three spans (4/5/1) with lamps and moons alternating. The cloud and under-arch filler is gone, and the trace now labels chimneys, thin spire, drum flank, stair and castle wall. The foot fell from (640,700) rgb(117,61,79) to (88,58,76). Option (a) is met. |
| m1 (5-1 towers, lawn foot, arcs over nothing, beams) | **Fixed; residual → n4** | The foot is sea teal. The right arcs now sit on a teal-blue cloud bank: (985,420) was rgb(24,35,95) and is now (0,59,119). The castle reads grey-white at 1280 and 640. |
| m2 (5-5 lighthouses faint, beams faint, seam) | **Seam fixed; rest open → m1** | (400,542) rgb(59,89,98) and (400,545) (60,90,98) match, so no step remains. The beams were checked in motion: they turn, but you can't see it. |
| m3 (stage pairs up, leans green) | **Improved → n5** | 5-1 is teal and violet. 5-4 is warm points over a violet dusk, now clearly unlike 5-1. |
| m4 (5-2 south rose erased) | **Fixed** | In the cleared crop (280-600, 480-720) the rose now shows as a violet star on a lifted glow under its ring, and it holds at 640. |
| n1 (5-3 plinko) | Accepted | The columns are the subject. The flank moons at the fires break the top band. |
| n2 (5-2 third base05 board) | Accepted | — |

No regressions found. 5-1 and 5-2 keep their layouts, and 5-1 is still the clean opener. My `--moment start --cache` renders of base-23, base-24 and base-25 match the pack's `engine-1280` pixel for pixel (diff max 0).

## Per-level verdicts

**5-1 base-21 The White Towers: PASS (n4).** At 640 it reads at a glance as the towers over the bay. The teal foot now says "water", and the cloud bank gives the right-hand arcs a reason to be there. It is a good-feeling opener at 25.45 pooled.

**5-2 base-22 The Strait of Merlthor: PASS.**
- The lavish chart board is unchanged.
- The south rose is restored under its ring, and the gilt route ends cleanly at (370,288).
- The foot haze adds nothing and costs nothing.

**5-3 base-23 Lights on the Water: PASS (n2).**
- This is now the stage's title board in earnest: warm quay lamps along the curve, lit loggias, flickering jetty lamps and buoy, and the crescent moon.
- The flank moons let each fire show between pegs. The top band no longer reads as a bare row because the lamps stand between them.
- The reflections are the only thing short of gold.
- Plays at 22.3, a clean middle.

**5-4 base-24 The Arches of the Bridge: PASS (n1).**
- The board now has a subject to match its name:
    - lamps on the rail alternate with moons;
    - the drum light crowns the east tower;
    - the cable swag (591,311 → 791,350 at 1280) reads as a cable;
    - three arch bricks sit on the arches.
- It is still the stage's darkest board. The open lower third (only bricks under y 590) gives clean falls to the bucket, which suits a 21-point board.
- Raising the light points' `r` or halo a little, if F5/F6 allow, would make "lamplit" read at 640, not just at 1280. Optional.

**5-5 base-25 The Lighthouses of Vylbrand: PASS (m1).**
- The play is right for a finale:
    - it is the hardest board (17.7 pooled);
    - its movers are the stage's first;
    - the retuned orange at (632,235) stays on the summit contour;
    - stuck fires are 61 per 1,728.
- It still closes the stage quieter than 5-3. Taming the cores for UX (k 0.95 → 0.38) was right, but at that k the pale #FFE6B0 renders grey. The warm glows were eased for protan at the same time.
- **For m1, either route works:**
    - Recolour the five lamp points to the amber the stage's other lamps use (#FFC777 or #FFB45E) at the same k 0.38. That changes hue, not luminance, so the UX ceiling (L 0.75-0.78, under the guide and ball) holds. It needs a protan and F6 re-check at the Aleport ring.
    - Add a small lighthouse glyph to the plate at each lamp, the round-1 option that was not answered.
- The beams can't get brighter under F4, so the lamps have to carry the title on their own.
- I'd take this as a fix within MR 5 if another round runs for anyone else. Otherwise, it should be the first polish item before ship.

## The stage as a set

- **Theme.** "Harbour Lights" is now true on three boards: 5-3 (quay and jetty lamps), 5-4 (rail lamps and drum) and 5-1's posts. The finale is the weak link (m1).
- **Variety.** It is better than round 1:
    - 5-1 has a teal sea foot;
    - 5-2 is the violet and copper chart;
    - 5-3 is a night harbour in blue and cream;
    - 5-4 has warm points over a violet dusk;
    - 5-5 is a blue-green coastal map.
    - The 5-3 and 5-5 green-foot echo is a nit.
- **Ramp.** The pooled values are 25.45 / 24.02 / 22.3 / 21.07 / 17.7:
    - The steps are 1.43, 1.72 and 1.23, and the finale gap is 3.37 ± 0.40. That is 0.13 under my 3.5-4 aim and within error, so it is accepted.
    - The ramp is steady and the finale reads clearly as the summit.
    - The mean is 22.11, 0.49 under the charter's 22.6. MR 6 should fall about 0.5, not more, as the response says.
    - All Aces are above the hard-level line.
- **Finale.** Mechanically it is a proper summit. Visually it needs m1 to end on lights over the sea rather than grey haze.
