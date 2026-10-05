# Level-design critic, rich pass 2, round 3: the checks fixed and the boards re-dressed

5 October 2026. Checked at commit 5130f3df (worktree `agent-a24967afd6691151a`, under `docs/design/v9/`).

**Verdict: APPROVE, all six boards and the F-rules.**
- Every round-2 Major and Minor is fixed, and I checked each fix myself.
- The checks now fail when they should: they catch my own synthetic shapes as well as their own self-test.
- What remains is Minor or Nit: two gaps in the rules' scope, and some small measurement notes.

## Method

- **Level files.** `git diff 7eee5186 HEAD -- docs/design/v9/rich/levels` is empty. `mfcheck validate` returned OK in round 2, and the files are byte-identical since, so play is unchanged.
- **Composites.** I re-rendered all six; they match the committed @2x files within 0.002. I rendered each board again with every piece removed, to get its backdrop: scene, veil, chrome, bucket and spill.
- **Readability**, at 1× and at 0.8× (a Lanczos downscale of @2x), the same measures as before:
  - the face: mean relative luminance in 0.6 r;
  - the backdrop: an annulus r+2 to r+9 on the piece-free render;
  - the contrast ratio CR;
  - my luma margin: the face's 80th percentile minus the ring's 90th.
- **Worst cases.** Each kind against every place it can be dealt, the 26 orbit pegs at 24 points along their path, and bricks. Each board's veiled scene was also compared position by position against the approved board's.
- **Framing.**
  - My pixel mask: OKLab L darkened by 0.06 or more against the approved scene, or the colour replaced.
  - The designer's `ctx.cover`, rebuilt read-only.
  - Clearances from my own distance field: pegs, bricks, and movers at 96 points along their path.
- **The checks.**
  - My synthetic shapes through `framecheck`: straight posts 4–60 wide, the old column's geometry, a disc and a hole of r 9 (alone and joined to a slab), and a 60-unit rim.
  - The designer's self-test (`py -3 framecheck.py`).
  - My own direction-split straight-run test over the real masks.
- **Colour.** Independent OKLab hue histograms, in 30° bins, of each scene's opening.
- **Motion.** `motion/play.png` decoded with Pillow, 48 frames.
- **By eye.** 4× crops of every changed shape, purple pasted onto eight worst spots (including the exp-p3 nebula), and full boards at 0.8×.

## The round-2 findings

| Round 2 | Status | Evidence |
|---|---|---|
| R1 (Major), F3c could never fail | Fixed | My synthetic posts 4, 8, 11, 20, 30, 40 and 60 units wide, and the old column's geometry (11 wide, ±0.6 wobble), are now all caught. So are a disc touching a blob, a hole of r 9 in a slab, and a 60-unit rim. The self-test passes all 15 cases. My own test on the real masks finds no straight run except exp-p2's left rope: 3.2 units wide, exempt as a thin stroke, as I asked |
| R2 (Major), F7 double-counted | Fixed | My exclusive-window shares of the second jewel: base-p1 24% (teal at 180–210°, against 76% for the sapphire window); base-p2 25% (rose at 300–360°); base-p3 17%; exp-p1 23%; exp-p2 24%; exp-p3 19%. These agree with readcheck.json (0.245, 0.252, 0.165, 0.235, 0.239, 0.196) |
| R3, F3d could not see holes | Fixed | A hole of r 9 in a slab is now caught |
| R4 and K1, F6 blind to the chrome, and the frame inside the walls | Fixed | Scanning the composite: on the right, a dark reveal of luma 0.04–0.14 runs from x ≈ 723 to 728 and the gilt starts at 729; on the left the gilt ends at 70 and is dark to the wall. At base-p2 (714,292), blue is now CR 3.37, and purple's worst case on base-p2 is 2.53 at (209,328), against round 2's 2.07 with the frame |
| R5, F3a relied on drawers registering | Fixed | `darkened_near_pieces` is in. My own pixel mask's smallest clearances: base-p1 13.8, base-p2 6.25, base-p3 10.8, exp-p1 8.5, exp-p2 14.6, exp-p3 16.7. With your mask and my distances: 6.7, 8.75, 8.66, 34.3, 7.8 |
| X1, exp-p1's column | Fixed | The column is removed. The balustrade sits on a curved terrace with its lamp, clear of every piece, and no straight run is found |
| L1, fireflies in a row | Fixed | 9 fireflies, at (90,515), (383,500), (405,404), (439,454), (555,424), (671,456), (703,452), (710,408) and (710,516): spread out, no row, and out of the bucket's lane. In motion, no warm moving pixel comes within 8 units of a peg |
| P1, base-p1 purple's drop | Fixed | At purple's worst place (130,260), the ring's p90 now rises less than 0.02 over the approved board. The F6 check, now including the chrome, measures a drop of 0.010 |
| Nits: the rope, the round rocks | Mostly fixed | The right rope is gone. The rocks are irregular, but see N2 |

## Per level

### base-p1 The Airship Road: APPROVE
- **The compass rose works.** It replaces the quill: gilt light (not a silhouette) centred at (138,112), R 58, fading out within 6–9 units of every piece. It reads as the chart's own engraving, not as anything that plays. Its outer ring is a thin light circle in the corner (see R7).
- **Readability.** Lowest drawn CR 3.23 (orange at (600,202)); purple's worst case 2.51 at (130,260), which reads clearly. Teal seas against sapphire land: the chart reads at 0.8×.

### base-p2 The Holy See: APPROVE
- **The wall edge is fixed** (K1 above).
- **Readability.** Lowest drawn CR 2.90 (orange at (209,328) on the rose cloud), the lowest of the six boards, and better than the approved board's 2.7. Bricks: blue 3.22, purple 3.73.

### base-p3 The Moonlit Post: APPROVE
- **Fireflies and wood.** The fireflies are spread (L1 above). The wood's teal-emerald is quietened near the pegs.
- **Readability.** Lowest CR 3.48 (orange); purple's worst case 2.79 at (240,120).
- **Nit N1: the second jewel is near the floor.** At 16.5–17% it is close to F7's 15%. Keep it above the floor when the board is re-tuned.

### exp-p1 The Domes of Sharlayan: APPROVE
- **Framing.** The column is gone; the terrace balustrade and its lamp are clear.
- **Readability.** Lowest CR 3.11 (orange at (327,248)); purple's worst case 2.63; bricks: orange 3.55, blue 3.62.

### exp-p2 The Ferry in the Stars: APPROVE
- **Framing.** One rope is left, exempt as a thin stroke, 34 units or more from any piece.
- **Readability.** Lowest CR 3.18 (orange at (430,250)); purple's worst case 2.79 at 1× and 2.75 at 0.8×.

### exp-p3 The Sea of Sorrows: APPROVE
- **Nit N3: the nebula now has the purple peg's hue.** The space has moved to hue 300–330° (46% of the coloured pixels), and the nebula raises rings by up to 0.17, at (286,296), (250,330) and (338,311).
  - In those rings the hue is 305–314°, against purple's 310°. Purple separates by value and chroma only: CR 3.67–4.04, margin 0.43 or more, ring chroma 0.05–0.06 against purple's 0.126. Pasted and checked at 0.8×, it reads.
  - Don't push the space further toward 310°, or brighter.
- **Nit N2: one rock still reads as a disc.** The rock at about (101,72) is still a dark near-round shape about 30 units across. It is outside F3d's 10–26 range, so the check passes it, but it reads as a disc rather than a rock. Stretch it further.
- **Readability.** Lowest CR 3.69 (orange); purple's worst case 3.18 / 3.21, at the earth (566,436), including the orbit paths.

## Verdict on F1–F8 (and F9): APPROVE

- **Working and enforced:**
  - **F3a:** both masks; the pixel backstop.
  - **F3b.**
  - **F3c:** posts of every width, slabs at 45°, the thin-stroke exemption.
  - **F3d:** discs and holes.
  - **F5:** stills and motion.
  - **F6:** worst placement, movers, bricks, both scales, chrome included.
  - **F7:** exclusive windows.
  
  My numbers agree with readcheck.json and framecheck.json within method noise.
- **Minor R6: drawn light is outside F3.** F3 only sees darkened framing (silhouettes and the pixel backstop). Decorations drawn in light, such as the compass rose and the chart's neat-line, have no clearance, straightness or disc check in `framecheck`. The rose keeps clear only through its own `keep` mask.
  - Fix: route every light overlay through one helper that applies the same 6-unit `keep` and registers the overlay in a `ctx.light_cover` that `framecheck` checks for F3a, F3c and F3d.
- **Minor R7: clean arcs aren't tested.** F3c tests straight runs only. A clean unlit arc, like round 1's window arch (r about 132, a sagitta of 1.2 over 36 units), would now pass F3b and F3c. The rule text in round 1 said "line or arc".
  - Fix: add a constant-curvature test (fit a circle per edge class over 60 units, residual under 0.75, radius over 40 units). Or state that large clean arcs are judged by the critic.
- **Nit N4 (F8): motion measurements differ.** The spec gives the largest lift within 8 units of a peg as 0.017. In the preview I measure 0.034 at two pixels, (230,395) and (290,340), 2.5–8 units from a peg edge; they are dust motes. Within 2.5 units the largest lift is 0.008, and peg pixels change by 0.0008 at most. Effectively within the 0.03 cap, but say how 0.017 was measured (at 1× or on the 0.8× preview).
- **F9 (protan).** I did not judge it; it is the UX specialist's.

## Readability, my measurements

The figure is the CR, with the position in brackets.

| Level | Scale | Lowest drawn peg | Purple, worst case | Green, worst case | Smallest margin, drawn | Purple margin, worst case | Bricks |
|---|---|---|---|---|---|---|---|
| base-p1 | 1× | 3.23 orange (600,202) | 2.51 (130,260) | 3.70 | 0.275 orange (340,172) | 0.304 | none |
| | 0.8× | 3.22 | 2.51 | 3.67 | 0.272 | 0.309 | |
| base-p2 | 1× | 2.90 orange (209,328) | 2.53 (209,328) | 3.72 | 0.296 orange (300,430) | 0.315 | blue 3.22, purple 3.73 |
| | 0.8× | 2.88 | 2.53 | 3.70 | 0.307 | 0.319 | blue 3.24, purple 3.74 |
| base-p3 | 1× | 3.48 orange (248,198) | 2.79 (240,120) | 4.15 | 0.391 | 0.366 | none |
| | 0.8× | 3.48 | 2.79 | 4.18 | 0.385 | 0.352 | |
| exp-p1 | 1× | 3.11 orange (327,248) | 2.63 (432,231) | 3.85 | 0.268 | 0.297 | orange 3.55, blue 3.62 |
| | 0.8× | 3.09 | 2.63 | 3.86 | 0.264 | 0.308 | orange 3.61, blue 3.67 |
| exp-p2 | 1× | 3.18 orange (430,250) | 2.79 (430,250) | 4.11 | 0.367 | 0.338 | none |
| | 0.8× | 3.19 | 2.75 | 4.05 | 0.377 | 0.332 | |
| exp-p3 (with the movers' paths) | 1× | 3.69 orange (598,300) | 3.18 (566,436) | 4.64 | 0.383 | 0.316 | none |
| | 0.8× | 3.69 | 3.21 | 4.67 | 0.384 | 0.329 | |

Purple's worst case on the veiled scene alone, approved board then round 3:

| Level | Approved | Round 3 |
|---|---|---|
| base-p1 | 2.85 | 2.51 |
| base-p2 | 2.50 | 2.53 |
| base-p3 | 2.78 | 2.82 |
| exp-p1 | 2.64 | 2.62 |
| exp-p2 | 2.85 | 2.78 |
| exp-p3 | 3.16 | 3.16 |

Every kind clears the 0.20 margin at its worst placement on every board at both scales, chrome included. The lowest margin anywhere is 0.264 (exp-p1, orange at 0.8×).

Not verified:
- in-game rendering;
- deals other than seed 1, beyond the worst-case bounds;
- motion on boards other than base-p3;
- F9 (protan), which is the UX specialist's.

OVERALL: APPROVE
