# Level-design critic, rich pass 2, round 1: readability and fuller boards

5 October 2026. Checked at commit 1ef1ab72 (worktree `agent-a24967afd6691151a`). Verdict: **REVISE**. Three boards (base-p3, exp-p1, exp-p3) have a framing silhouette that looks solid, with pegs sitting on it. The fuller-board rules also need measurable checks before they are used on the other 109 levels.

## What I checked and how

- **Level files.** `git diff 7eee5186 HEAD -- docs/design/v9/rich/levels` is empty. The last commit to touch that path is 448abcd0, which is an ancestor of 7eee5186. `mfcheck validate` returns OK on all six levels, with the same counts as before: 84, 89, 85, 67, 102 and 72 pieces, 0 cradles and 0 wall pinches. The files are byte-identical, so play has not changed and I did not re-run `play`.
- **Composites re-rendered.** I re-rendered every composite from `composite2.render` into my scratch folder. The committed @2x files match within 0.002 (rounding), so I measured exactly what the scripts produce.
- **Backdrops.** I rendered each board a second time with every piece removed (`gone` = all pieces, the bucket held in place). That gives the true backdrop: the scene, the veil, the chrome and the lantern spill.
- **Per peg at 1× (800×600) and 0.8× (640×480, a Lanczos downscale of @2x):**
  - the face: the mean relative luminance in 0.6 r on the composite;
  - the backdrop: the mean in an annulus r+2 to r+9 on the piece-free render;
  - the contrast ratio, CR = (lighter + 0.05) / (darker + 0.05);
  - the F6 margin, measured independently: the face's 80th-percentile luma minus the ring's 90th-percentile luma.
  - Each peg's kind was read from its pixels; it matched the engine's deal (seed 1) on every board.
- **Worst cases.** Each kind's median face was set against every position it can be dealt to:
  - orange: every candidate;
  - blue, purple and green: every peg, since every board has more than 25 candidates;
  - the 26 orbit pegs of exp-p3: 24 points along their path.
- **Bricks.** Face inside the brick (sd < −1.5) against a band 2 to 9 units outside it.
- **By eye.** Purple was pasted onto the eight worst places (sprite from exp-p3) and checked at 1× and 0.8×. Framing was checked with crops at 4×.
- **Framing masks.** I rebuilt each board's framing mask (`dress2.COVER`) to measure how far each peg is from the framing, and to test F2's regions.
- **Motion.** I unpacked `motion/play.png` (60 frames) and re-ran `motion2.play_loop()` in float to tell the design apart from the APNG's encoding.

## Per level

### base-p1 The Airship Road: APPROVE

- **Minor: purple's worst case dropped more than F6 allows.** At (130,260) the backdrop is the parchment highlight, raised by the lamp pool and the beams. The ring's p90 rose 0.029 (0.037 at (250,240)). Purple's worst-case CR fell from 2.85 to 2.55.
  - It still reads clearly as purple (pasted and checked), and its margin is 0.29.
  - But this exceeds F6's "no more than 0.02 worse", measured at the worst placement rather than at seed 1's dealt purple.
  - Fix: lower the lamp glow (`glow` k 0.07 to about 0.05), or keep the parchment value-hue below the lamp's pool west of x 280.
- **Minor: the neat-line reads as an inner wall at 0.8×.** The inner gilt line runs 13 units inside every wall. A ball (r 6) bouncing off the real wall visibly crosses it first. The degree bars (y 47–54) also pass through the launcher's swing (F2 keeps framing out of it).
  - Fix: move the neat-line and the bars to an inset of 4 units or less, so they read as the frame's own edge, or halve the inner line's strength. Keep the bars out of x 315–485.
- **Orange on the parchment holds.**
  - Worst orange is CR 3.24 at (600,202).
  - The parchment's 95th-percentile warm chroma round any candidate is 0.049 or less, against the orange face's 0.118, so F5's "orange is the most saturated warm thing" holds.
- **Subject.** The board still reads as a chart. The neat-line and the lamp support it.

### base-p2 The Holy See: APPROVE

- **Minor: the window arch makes the top corners look closed.** The pointed-arch spandrels (x 75–210 and 590–725, y 41–200) have a thin rim-lit moulding ring, a clean arc about 200 units long. No piece touches it, and the corners outside it hold no pieces, so the cost to play is low. It is still the kind of clean arc F3 forbids.
  - Fix: break the moulding into cusped tracery (foils every 30–40 units), or drop its rim.
- **Minor: the trefoils look like grey discs.** At (115,92) and (685,92), each trefoil is three discs of r 8.1. That is peg-sized, and at 0.8× they read as a "club" glyph.
  - Fix: make the cut a pointed quatrefoil or a single foiled oculus no smaller than 30 units across, so nothing round is peg-sized.
- **Nit: the firs reach above y 470 and sit behind two pegs.** The firs top out at y 450 and 444, and they lie behind pegs (108,470) and (128,512). They read as trees and the pegs read clearly. The firs are within F2's 60-unit side band.
- **Nit: the spec's "no purple dealt" is wrong.** Seed 1 deals a purple brick at (158,155), and `readcheck` ignores bricks. It measures CR 3.74, which is fine.
- **Readability.** The lowest contrast on any board is here: orange at (209,328), CR 2.82 at 1× and 2.80 at 0.8× (round 5 measured 2.7 and 2.6 by its own method). Purple's worst case is 2.46, against 2.50 approved.

### base-p3 The Moonlit Post: REVISE

- **Major: both trunks read as inner walls, and pegs sit on them.**
  - The right trunk (x 690–724, y 50–600) has a continuous rim-lit left edge: a straight bright vertical line from y 170 to 590, 25 to 35 units inside the wall. It runs through the centres of pegs (700,236), (690,330) and (689,489); their framing clearances are −9.6, −4.5 and −5.7.
  - The left trunk's right edge, unlit but clean, runs through (110,360), (104,415) and (100,457).
  - The branch (92,250)→(132,205) is a straight rim-lit stick behind peg (120,230).
  - A player reads the right edge as where the ball will bounce on the right side, 30 units early. The pegs look embedded in the bark.
  - Fix:
    - Move the trunks out so that 12 units or less of each lies inside the opening (centre at x 66 and 734).
    - Give the inner edge bark breaks, with the rim lit in short runs of no more than 30 units.
    - Give the branch a fork and leaves, or end it at x 105 or less.
    - No framing within r + 6 of any peg.
- **Minor: the top-right fronds crowd the pom-pom.** The fronds sweep through the pom-pom's ring, the subject's key feature with its oranges. Pegs (602,98) and (602,132) sit on frond stems.
  - Fix: keep the right-hand fronds right of x 625, or above y 75 where x < 625.
- **Minor: in motion, fireflies touch pegs (F5 vs F8).**
  - In `motion2.play_loop`, the 16 fireflies wander through x 110–690, y 420–540, among the bottom rows.
  - Their cores pass behind or onto pegs: (480,446), (674,418), (346,450), (157,488) and (594,460). They are only cut at the 2.5-unit ring, so a warm point pops out at a peg's rim, which reads like a hit spark.
  - The still image keeps 8 units; the motion does not.
  - Fix: place each wander path so that path plus halo stays 8 units or more from every peg edge, or seed the fireflies only in clear spaces.
- **Nit: the APNG preview changes peg pixels.** Peg pixels change between frames by up to 45/255 (palette re-optimisation in the encoding). The float frames change nothing inside a peg (0.0), so this is the preview, not the design. Write it with `optimize=False`, or at least note it.
- **Readability and subject.** Readability is good: lowest CR 3.54 (orange), and purple's worst case 2.78 at (240,120), the lilac-on-lavender sky. Purple reads there by value; checked by eye at 0.8×. The moogle still reads.

### exp-p1 The Domes of Sharlayan: REVISE

- **Major: the lamp column and the balustrade's rail look like a post and a ledge, and an orange sits on their corner.**
  - The column (x 600–612, y 470–600, with a 20-wide cap at y 470–484) is a straight rim-lit post the same width as a brick.
  - The balustrade's top rail (y 528.5–535.5, x 600–725) is a clean horizontal bar about 7 units thick.
  - Orange candidate (612,528) sits exactly where post and rail meet (clearance −9.6). The rim raises its ring's p90 by 0.069.
  - This is the lower corner, where balls fall to the boat. The rail reads as something a ball would roll along.
  - Fix:
    - Lower the balustrade so its rail sits at y 560 or lower (under the bucket's lane), or show it only right of x 650.
    - Move the column to x 650 or more, with nothing within r + 6 of (612,528) or (651,505).
    - Break the rail's rim into short runs, or let the laurel or ivy cross it so it reads as scenery.
- **Nit: a laurel tip nearly touches a peg.** The laurel tip ends 3.4 units from peg (640,156).
- **Readability and subject.** Readability is good: lowest CR 3.11 (orange at (327,248)), purple's worst case 2.62, and the orange brick 3.53. The domes still read, and the terrace fits the place.

### exp-p2 The Ferry in the Stars: APPROVE

- **Minor: the rigging is drawn dotted, so it reads like extra star lines.** The ropes are 60 dots along a curve. In this method, dotted lines mean pegs or star lines, so the rigging reads as extra constellation lines in the top corners, competing with the figure. Rope r1 runs behind peg (162,130) (clearance −8.8), and r3 runs behind peg (698,168) (clearance −1.3).
  - Fix: draw the ropes as continuous strokes (enough samples to join up), and keep them r + 6 or more from pegs.
- **Minor: the boom cuts off the corner.** The boom (75,113)→(165,45) is a straight dark spar with a lit top edge, and it reads as a deflector cutting off the corner.
  - Fix: make the furled sail the dominant shape (fuller cloth with folds), and break the spar's rim.
- **Nit: the rigging leaves F2's regions.** About 650 framing pixels lie outside F2's regions (x 215–584, y 41–102, the rigging near the top).
- **Purple on the violet board holds.**
  - As dealt: CR 3.74 at (322,477).
  - Worst case: 2.81 at 1× and 2.77 at 0.8×, at (372,304), the brightest Milky Way. Checked by eye: it separates from both the ground and the blue pegs beside it.
  - The spec's "purple at least 0.39 luma above its ground" holds only for the dealt peg. The worst-case margin is 0.357 at 1× and 0.348 at 0.8×.

### exp-p3 The Sea of Sorrows: REVISE

- **Major: the right-hand crystals look solid, and three pegs sit on or against them.**
  - The tall crystal (700,540; h 80) is a flat violet slab with a straight lavender-lit edge (rim_k 0.7) about 80 units long.
  - Peg (700,482) sits on its upper face and peg (700,548) on its foot (both clearance −9.6). Orange candidate (660,516) touches the small crystal (clearance −1.7).
  - The rims raise the rings round these pegs by 0.064 to 0.074.
  - This is the lower right corner, where balls fall, so it reads as a standing slab the ball would hit.
  - Fix: move the crystals to x 712 and 690 (or shorten them to h 50 or less), so nothing is within r + 6 of a peg, and break the long lit edge with facets.
- **Readability and subject.** Readability is good:
  - lowest CR 3.75 (orange at (598,300));
  - purple's worst case 3.24, including the 26 orbit pegs along their whole path, which `readcheck.py` skips.
  
  The earth and its orbit still read, and the nebula stays in the empty black.

## Verdict on F1–F8

**REVISE.** The intent is right, and F2 is met on every board: the open middle holds 0.0–0.08% framing, and apart from exp-p2's rigging nothing lies outside the regions. But the rules let through every Major above, because nothing measurable stands between framing and pieces.

- **Major (missing rule): framing must keep clear of pieces.** No rule keeps framing away from pieces. 15 pegs on five boards sit on or within 2 units of a silhouette.
  - Add an F3a: no framing pixel (mask > 0.5) within r + 6 of any peg, brick or mover path. `dress2.py` should print the smallest clearance and refuse below 6. The same distance check I ran is about 15 lines.
- **Major (F3 cannot be enforced "by eye").** Three of six pilots break it while the self-check passes.
  - Make it measurable: no rim-lit run longer than 30 units, and no straight or regular edge (deviating less than 1.5 units over 40) inside the opening more than 15 units from a wall.
  - Keep the ban on peg-sized discs (r 6–12), which also catches the trefoil.
- **Major (the F6 checker misses what the critic must judge).** `readcheck.py` misses four things:
  - It skips movers (`"move" in d`), so all 26 of exp-p3's orbit pegs.
  - It skips bricks.
  - It measures purple and green only where seed 1 deals them, never at their worst placement.
  - It compares the board minimum, not the worst case, against the approved board.
  
  For these six boards my numbers pass (worst-case margin 0.29 or more everywhere). For 109 levels it must check movers along their path, bricks, and each kind at its worst eligible place, at 1× and 0.8×.
- **Minor (F5 covers stills only).** F5 and F8 disagree: F5's 8 units is enforced only for still points (`dress2.points`). The motion layer uses a 2.5-unit mask.
  - State F5 for every frame (path plus halo), and make F8's ring a backstop, not the rule.
- **Nit (F1 can't be checked on the output).** F1 holds by construction, but the light layers are added after the jewel grade, so L changes on the output. Check F1 on the jewel step alone, and count F4's light against F6.
- **No change needed.** F4, F7 and F8 are sound as written.

## Readability, my measurements

The figure is the CR, with the position in brackets. "Worst case" means the kind's median face at its worst eligible position.

| Level | Scale | Blue, worst drawn | Orange, worst drawn | Green, as drawn | Purple, as drawn | Purple, worst case | Green, worst case | Smallest F6 margin, any drawn peg | Bricks |
|---|---|---|---|---|---|---|---|---|---|
| base-p1 | 1× | 3.34 (130,260) | 3.24 (600,202) | 6.79 | 4.82 | 2.55 (130,260) | 3.75 | 0.264 orange (340,172) | none |
| | 0.8× | 3.34 | 3.23 | 6.75 | 4.83 | 2.55 | 3.73 | 0.262 | |
| base-p2 | 1× | 3.36 (162,122) | **2.82 (209,328)** | 4.75 | none dealt | 2.46 (209,328) | 3.61 | 0.291 orange (300,430) | blue 3.14 (392,311), purple 3.74 (158,155) |
| | 0.8× | 3.31 | **2.80** | 4.72 | none dealt | 2.46 | 3.59 | 0.302 | blue 3.16, purple 3.75 |
| base-p3 | 1× | 3.69 (240,120) | 3.54 (274,176) | 5.36 | 4.64 | 2.78 (240,120) | 4.14 | 0.396 orange | none |
| | 0.8× | 3.69 | 3.55 | 5.40 | 4.65 | 2.78 | 4.17 | 0.387 | |
| exp-p1 | 1× | 3.41 (432,231) | 3.11 (327,248) | 7.14 | 3.99 | 2.62 (432,231) | 3.84 | 0.270 orange (327,248) | orange 3.53 (542,280), blue 3.61 |
| | 0.8× | 3.43 | 3.09 | 7.18 | 3.99 | 2.62 | 3.85 | 0.266 | orange 3.59, blue 3.66 |
| exp-p2 | 1× | 3.73 (404,303) | 3.19 (372,304) | 5.30 | 3.74 (322,477) | 2.81 (372,304) | 4.13 | 0.400 orange | none |
| | 0.8× | 3.84 | 3.25 | 5.18 | 3.68 | 2.77 | 4.08 | 0.389 | |
| exp-p3 (with the movers' paths) | 1× | 4.24 (566,436) | 3.75 (598,300) | 7.11 | 5.42 | 3.24 (566,436) | 4.72 | 0.385 orange | none |
| | 0.8× | 4.25 | 3.75 | 7.11 | 5.43 | 3.26 | 4.75 | 0.388 | |

How the re-dress moved purple's worst case, measured on the veiled scene only, approved scene then rich pass 2, at the same place:

| Level | Approved | Rich pass 2 | Change |
|---|---|---|---|
| base-p1 | 2.85 | 2.55 | the only real drop |
| base-p2 | 2.50 | 2.46 | |
| base-p3 | 2.78 | 2.82 | |
| exp-p1 | 2.64 | 2.61 | |
| exp-p2 | 2.85 | 2.80 | |
| exp-p3 | 3.16 | 3.21 | |

- Every peg kind passes the 0.20 margin at both scales, including purple and green at their worst placements. Purple was checked by eye on all eight worst spots and reads as purple each time.
- The designer's `readcheck-rich2.json` agrees with my numbers within about 0.02 where it measures. Its gaps are those under F6 above.

Not verified:
- in-game rendering;
- deals other than seed 1, beyond the worst-case bounds;
- how motion layers other than base-p3's look (only base-p3 has a preview).

OVERALL: REVISE
