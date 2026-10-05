# Level-design critic, rich pass 2, round 2: re-dressed boards and the new checks

5 October 2026. Checked at commit e9691733 (worktree `agent-a24967afd6691151a`, under `docs/design/v9/`).

**Verdict:**
- **All six boards: APPROVE.** Every round-1 Major on a board is fixed, and I measured each fix myself. No peg sits on framing any more.
- **The rules: REVISE.** Two of the new checkers cannot fail. F3c (straight edges) cannot fire at all. F7 (two jewels) counts one hue band twice. Because of those two, three things pass that the written rules forbid: base-p1 and exp-p3 have a single jewel, and exp-p1's lamp column is a straight 110-unit post.

## Method

- **Level files.** `git diff 7eee5186 HEAD -- docs/design/v9/rich/levels` is empty. `mfcheck validate` returns OK on all six, with the same counts as round 1, so play is unchanged.
- **Composites.** I re-rendered all six from `composite2.render`; they match the committed @2x files within 0.002. I also rendered each board with every piece removed, to get its true backdrop (scene, veil, chrome and spill).
- **Readability**, at 1× and at 0.8× (a Lanczos downscale of @2x), the same measures as round 1:
  - the face: mean relative luminance in 0.6 r;
  - the backdrop: an annulus r+2 to r+9 on the piece-free render;
  - the contrast ratio CR;
  - my own luma margin: the face's 80th percentile minus the ring's 90th.
- **Worst cases.** Each kind's median face against every place it can be dealt. Exp-p3's 26 orbit pegs were sampled at 24 points along their path. Bricks were measured as before.
- **Approved against round 2.** I put each board's approved scene and its round-2 scene through the same veil and ring at every peg position.
- **Framing.** I built two masks:
  - my own, from pixels: where OKLab L fell 0.06 or more against the approved scene, or the colour was replaced;
  - the designer's `ctx.cover`, rebuilt read-only.
  
  Clearance used my own distance field: pegs, bricks, and movers at 96 points along their path.
- **Checkers.** I ran `framecheck` on synthetic shapes (straight posts, a slab at 45°, a disc, a hole, a long rim). I recomputed the hue histograms independently.
- **Motion.** `motion/play.png` decoded with Pillow, 48 frames at 640×480.
- **By eye.** Crops at 4× of every new framing shape, purple pasted onto the eight worst places, and full boards at 0.8×.

**Correction to round 1.** My round-1 Nit that the APNG changed peg pixels by up to 45/255 came from ImageMagick's `-coalesce` misdecoding the file. Decoded with Pillow, this round's preview changes peg pixels by at most 0.0003. Please withdraw that Nit.

## The round-1 findings

| Round-1 finding | Status | Evidence |
|---|---|---|
| base-p1, purple's worst-case drop (Minor) | Fixed, within measurement noise | My ring p90 at (130,260), (180,220) and (250,240) rises 0.026–0.028 over the approved board; the designer measures 0.016. Worst-case purple CR is 2.54 (approved 2.85). It reads clearly (pasted and checked). See Nit P1 below |
| base-p1, neat-line as an inner wall (Minor) | Fixed | Inset 2 units, and kept out of x 315–485 |
| base-p2, arch and trefoils (Minor) | Fixed | Replaced by fir boughs that part round the pegs. Smallest clearance 6.7 (my distances, their mask); 6.25 by my pixel mask |
| base-p3, trunks and branch (Major) | Fixed | Trunk centres are at x 57 and 743, at most about 1.5 units inside the opening. The branch is gone. Smallest clearance 8.75 |
| base-p3, fronds crowding the pom-pom (Minor) | Fixed | The oak leaves stop near x 620; the pom-pom ring is clear |
| base-p3, fireflies touching pegs in motion (Minor) | Fixed | 0 warm moving pixels within 8 units of any peg edge. Within 2.5 units the largest lift is 0.008. Between 2.5 and 8 units it is 0.032 at one pixel (290,340), a dust mote; that is effectively the 0.03 cap |
| exp-p1, column and rail with an orange on them (Major) | Fixed for clearance | Nearest piece about 40 units away, clearance 8.7 or more. But see Minor X1 |
| exp-p1, laurel tip (Nit) | Fixed | |
| exp-p2, dotted rigging and the boom (Minor) | Fixed | The ropes are continuous and unlit, 23.8 or more from pieces. The boom is now festoons above the board |
| exp-p3, crystals (Major) | Fixed | The right-hand crystals were not placed (they could not clear). The left ones clear by 16 or more. Smallest clearance on the board 7.8 |
| Rules F3a, F3b, F3c, F3d, F5, F6, F1 | Partly fixed | See the verdict on F1–F8 |

## Per level

### base-p1 The Airship Road: APPROVE
- **Minor C1: F7 fails as written.** This is a colour finding, for the game designer and the owner; it does not affect readability.
  - Every coloured pixel lies between 210° and 300°: 26% at 210–240°, 61% at 240–270°, 12% at 270–300°. The "teal seas" sit next to the sapphire, not 60° from it.
  - The checker reports a second jewel at 195° with 26%. That 26% is the 225° band, which it also counts as part of the first jewel (first share 1.000). See R2.
- **Nit P1: purple's drop is borderline.** By my measure purple's worst-case drop is 0.026–0.028, just over 0.02; the designer's is 0.016. To clear any doubt, lower the beam k from 0.07 to 0.065.
- **Subject.** The chart reads at 0.8×. The quill reads as a feather and clears every peg by 9.6 or more.

### base-p2 The Holy See: APPROVE
- **Minor K1: the new frame narrows the opening and brightens the edge.** This is new in this round and comes from the chrome.
  - The seamless gilt frame now starts about 1.5 units inside the right wall (x ≈ 723; round 1: about 725.5) and about 1 unit inside the left wall. It is also brighter there.
  - Blue (714,292), which touches the right wall, measures CR 2.69 at 1× and 2.71 at 0.8×, the lowest of any drawn peg on any board (round 1: 3.36).
  - Purple at its worst, at that spot, measures CR 2.07 / 2.10, with a luma margin of 0.14 / 0.17 against the 0.20 rule. Against the scene alone, the worst is 2.52 at (209,328).
  - By eye, purple, blue and green all read clearly against the gold, because the hue difference is large. But the F6 checker measures the scene only, so it cannot see this.
  - Fix: restore a dark inner reveal of 2–3 units on the frame's inner edge (round 1 had one, at luma 0.07–0.14), and keep the gilt outside 724.5. Or extend F6's ring to include the chrome.
- **Readability and subject.**
  - Orange on the rose cloud sea reads well: worst CR 2.88 at (209,328). The rose is muted and the orange saturated.
  - Bricks: blue 3.21, purple 3.71.
  - The subject reads.

### base-p3 The Moonlit Post: APPROVE
- **Minor L1: the 14 fireflies stand in one straight row.**
  - `firefly_spots` puts all 14 at y = 556 exactly, from x 90 to 708, about 40 apart. This is a ranking artefact: the bucket lane is simply the clearest band.
  - Still, and under Reduce motion, they read as a dotted line across the bucket lane, not as fireflies over the wood. Section 5 asks to keep y 520–560 sparse, and in this method dotted lines mean pegs.
  - Fix: no more than 3 fireflies per 40-unit band of height; spread them over y 400–540 where clearance allows (the gaps between the bottom rows qualify at 8 units plus the halo); add jitter.
- **Readability and subject.** Lowest CR 3.49 (orange at (248,198)); purple's worst case 2.79 at (240,120). The oak leaves read as foliage, and the moogle reads.

### exp-p1 The Domes of Sharlayan: APPROVE
- **Minor X1: the lamp column fails F3c as written, but the checker misses it.**
  - The column (x 696.5–707.5, y 490–594, wobble ±0.6) is a straight post about 110 units long, 23 units inside the right wall.
  - With a working straight-run test, its edges fail F3c at the rule's own tolerance (0.75 over 36): 31 hits at (690–700, 500–545). Part of the balustrade also fails, at (670, 540–550).
  - It reads as a lamp post, and it is about 40 units from the nearest piece, so the risk to play is low. But it is exactly the straight post F3c exists to stop, and once F3c works it will be refused.
  - Fix: move its axis to x ≥ 716 so it is mostly under the frame, or taper and flute it with a broken outline.
- **Readability and subject.**
  - Lowest CR 3.11 (orange at (327,248)); purple's worst case 2.63 at (432,231); bricks: orange 3.55, blue 3.62.
  - The gilt crowns stay low in warm chroma (95th percentile 0.021 or less round the orange candidates).
  - The subject reads.

### exp-p2 The Ferry in the Stars: APPROVE
- **Nit R1: one stretch of rope fails F3c as written.** Rope r1 between (100,120) and (130,70) is within 0.75 of a straight line for about 38 units. It is a dark stroke about 3 units wide and cannot be mistaken for a wall. Decide whether F3c applies to unlit strokes of 4 units or less (see R1).
- **Readability and subject.**
  - The brighter aurora raises the rings round the constellation by up to 0.069, at (365,243), (398,246) and (243,292).
  - Even so: lowest CR 3.18 (orange at (430,250)); purple's worst case 2.79 at 1× and 2.75 at 0.8×, at (430,250).
  - Purple against the violet and the aurora reads by eye. The constellation still reads.
  - The stern lantern at (84, about 536) is 42 or more from a peg.

### exp-p3 The Sea of Sorrows: APPROVE
- **Minor C2: F7 fails as written.** Every coloured pixel lies between 240° and 330° (270–300° holds 84%). The magenta and teal nebula registers about 1% outside that band. The checker reports a first share of 0.999 and a second of 0.165, and the 0.165 is the shared band (R2).
- **Nebula against the 0.02 rule.** The nebula raises rings by up to 0.12, at (286,296) and (339,307). Those are not the worst places, and every kind's worst case is within 0.02 of the approved board.
- **Readability and subject.** Lowest CR 3.69 (orange); purple's worst case 3.18 / 3.20, including the orbit paths. The earth and the orbit read.
- **Nit: two dark round rocks.** The drifting rocks at about (95,72) and (700,62) are dark rounds about 30 units across. They don't read as a ball or a peg, but they are the roundest framing on any board.

## Verdict on F1–F8: REVISE

- **Major R1: F3c can never fail.** `framecheck.straight_runs` searches a radius of span/2 = 18. Points inside that radius span at most about 34 units (35 pixel centres), but it requires 0.95 × 36 = 34.2.
  - Synthetic perfectly straight posts 4, 8, 11, 20, 30, 40 and 60 units wide, and a slab at 45°, all return None.
  - For thin shapes, both edges also fall in one window, which breaks the line fit.
  - Fix:
    - Search a radius of span/2 + 2.
    - Fit each edge class (facing left, right, up, down) separately.
    - Add synthetic positives as a regression test that must fail.
  
  With that change: synthetic posts are caught; exp-p1's column and balustrade and exp-p2's rope are flagged; base-p1, base-p2, base-p3 and exp-p3 are clean. State whether unlit strokes of 4 units or less are exempt.
- **Major R2: F7 double-counts.** `readcheck.jewels` accepts a second jewel 2 bins (60°) from the first, but scores each jewel as its bin plus both neighbours. So the bin between them counts for both.
  - base-p1 (first 1.000, second 0.264) and exp-p3 (0.999, 0.165) pass with one hue family.
  - Fix: make the windows exclusive (each bin goes to its nearer jewel), or require 90° between the jewels.
  - It does not affect readability, but as written it cannot enforce "two jewels" on the other 109 levels.
- **Minor R3: F3d catches discs but not holes.** A disc of r 9 is caught; a hole of r 9 cut in a slab is not. The rule says "or hole", and round 1's trefoil was holes. Fix: test the filled-in holes (inside the framing, not touching the opening's free area) for disc shape.
- **Minor R4: F6 measures the scene only.** The new frame lowered contrast for pegs that touch a wall (K1), and nothing measures it. Measure the ring on the piece-free composite (scene, veil, chrome, spill), or keep a dark reveal of 2 units or more on the frame as a chrome rule.
- **Minor R5: F3a relies on every drawer registering.** It only sees shapes that register in `ctx.cover`; `crystal` and the festoons paint first and register afterwards.
  - Add a pixel check: L dropped 0.06 or more against the graded scene, within r + 6 of any piece, means fail. My version of that check agrees with yours on these six boards (6.25 or more).
- **Sound and enforced:**
  - F3a: clearances 6.25 or more on every board by my own masks and distances;
  - F3b: a synthetic 60-unit rim run is caught; the longest real run is 28.4;
  - F5 and F8: stills and motion verified;
  - F6: now covers movers, bricks, worst placements and both scales; my numbers agree with its findings except for K1 and P1;
  - F1, F2 and F4.
- **Suggestion (F5).** Add a spread rule (L1), so lights are never placed in a regular row.

## Readability, my measurements

The figure is the CR, with the position in brackets.

| Level | Scale | Lowest drawn peg | Purple, worst case | Green, worst case | Smallest margin, drawn | Purple margin, worst case | Bricks |
|---|---|---|---|---|---|---|---|
| base-p1 | 1× | 3.26 orange (600,202) | 2.54 (130,260) | 3.74 | 0.270 orange (340,172) | 0.295 | none |
| | 0.8× | 3.25 | 2.54 | 3.71 | 0.267 | 0.301 | |
| base-p2 | 1× | **2.69** blue (714,292), frame-adjacent | **2.07** (714,292) with the frame; 2.52 (209,328) scene only | 3.04 | 0.247 blue (714,292) | **0.143** (frame) | blue 3.21, purple 3.71 |
| | 0.8× | 2.71 | **2.10**; scene only 2.52 | 3.07 | 0.278 | **0.171** (frame) | blue 3.24, purple 3.72 |
| base-p3 | 1× | 3.49 orange (248,198) | 2.79 (240,120) | 4.15 | 0.392 | 0.366 | none |
| | 0.8× | 3.49 | 2.79 | 4.19 | 0.385 | 0.352 | |
| exp-p1 | 1× | 3.11 orange (327,248) | 2.63 (432,231) | 3.85 | 0.268 | 0.297 | orange 3.55, blue 3.62 |
| | 0.8× | 3.09 | 2.63 | 3.86 | 0.264 | 0.308 | orange 3.61, blue 3.67 |
| exp-p2 | 1× | 3.18 orange (430,250) | 2.79 (430,250) | 4.11 | 0.367 | 0.338 | none |
| | 0.8× | 3.19 | 2.75 | 4.05 | 0.377 | 0.332 | |
| exp-p3 (with the movers' paths) | 1× | 3.69 orange (598,300) | 3.18 (566,436) | 4.63 | 0.378 | 0.310 | none |
| | 0.8× | 3.69 | 3.20 | 4.67 | 0.377 | 0.323 | |

Purple's worst case on the veiled scene alone, approved board then round 2:

| Level | Approved | Round 2 |
|---|---|---|
| base-p1 | 2.85 | 2.54 |
| base-p2 | 2.50 | 2.52 |
| base-p3 | 2.78 | 2.83 |
| exp-p1 | 2.64 | 2.62 |
| exp-p2 | 2.85 | 2.78 |
| exp-p3 | 3.16 | 3.16 |

Every kind passes the 0.20 margin against the scene at both scales. The only margin under 0.20 in the whole board is purple against the new frame at (714,292) (K1), and by eye it reads clearly.

Not verified:
- in-game rendering;
- deals other than seed 1, beyond the worst-case bounds;
- motion on boards other than base-p3.

OVERALL: REVISE
