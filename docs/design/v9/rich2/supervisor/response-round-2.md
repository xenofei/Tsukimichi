# Designer's response to round 2 (rich pass 2)

5 October 2026. Answers every Major and Minor in `game-designer-round-2.md` (GD, APPROVE), `ux-round-2.md` (UX) and `level-critic-round-2.md` (LC), and the Nits that were cheap. Round 3 is reviewed at the commit that adds this file.

## The checkers (LC R1–R5, GD N4)

| # | Answer |
|---|---|
| LC R1, F3c could never fail | `framecheck.straight_runs` is rewritten:<br>• the outline is split by the side it faces, so a thin post's two edges are never fitted together;<br>• each point's neighbourhood radius is span/2 + 2;<br>• the run is the longest gap-free stretch within 0.75 of the fitted line.<br>A self-test runs before every dress. Synthetic straight posts 4–60 units wide and a slab at 45° must fail; a wavy stem and an ellipse must pass. All do.<br>**Your question:** strokes 4 units wide or narrower (unlit ropes, rigging) are exempt, found by a 5 × 5 px opening at 1×; the self-test covers a rope (exempt) and a 6-unit post (caught).<br>The working check then flagged three things on the boards:<br>• exp-p1's column and balustrade: the column is removed and the balustrade sits on a curved terrace;<br>• exp-p2's right rope: removed;<br>• a false hit at exp-p3's foot from the mask being cut at the opening: the band within 15 units of the foot is excluded, as the rule says |
| LC R2 and GD N4, F7 double-counts | The windows are now exclusive. The first jewel is the window round the fullest bin; the second counts only bins outside it.<br>The fixes on the boards:<br>• base-p1's seas are a true teal (OKLab 195°);<br>• exp-p3's world keeps and slightly enriches its own ocean blue (lightness kept);<br>• base-p3's wood is teal-emerald.<br>Second jewels now hold 17–25% (`spec-rich2.md`, section 5) |
| LC R3, F3d holes | `round_shapes` also tests the framing's holes (background components that do not reach the open board). The self-test includes a hole of r 9 in a slab |
| LC R4 and K1, F6 blind to the chrome | F6 now measures the ring on the piece-free composite (scene, veil, chrome, bucket, spill), for both the approved and the new boards.<br>The frame is fixed:<br>• the gilt band lies wholly outside the walls;<br>• a dark 4-unit fillet sits between the wall and the gilt;<br>• a 2.5-unit dark reveal sits inside.<br>base-p2 at (714,292) now shows no drop against the approved board |
| LC R5, F3a relies on drawers registering | Pixel backstop `framecheck.darkened_near_pieces`: any pixel 0.06 or more darker than the graded scene within 6 units of a piece fails the board, whatever drew it. Its smallest clearance on each board is in `framecheck.json` |
| LC suggestion, a spread rule for lights (F5) | Fireflies are picked by a shuffled draw from the clear space: at most three per 40-unit band of height, 30 or more apart, y 400–515 (out of the bucket's lane). The rule is in `level-method.md`, F5 |
| LC Nit P1 (base-p1 purple's drop) | Beam k lowered to 0.065. The F6 drop (now with the chrome) is 0.010 |
| LC Nit (exp-p3 round rocks) | The rocks are irregular (radius jitter ±45%, elongated) |
| LC correction (the APNG Nit) | Withdrawn, with thanks |

## Game designer

| # | Answer |
|---|---|
| N1 fireflies in a row | See the spread rule above: 9 fireflies over the wood, none in a row, none in the bucket's lane |
| N2 beams do not visibly move | The shafts' break-up noise now drifts on a 5-unit circle every 6 s (light through moving leaves), on top of the ±15% breathing. It stays subtle by design: about 0.012 per pixel at the 95th percentile over the beam area. The spec states the measured figure rather than claiming more |
| N3 power card at 640 | At 640 the moment stays in the chrome:<br>• a laurelled BRASS WINGS ribbon on the top rail, ending above the opening;<br>• the rail portrait and gems pulse in Cid's colour.<br>At 1280 the card stays in the margin |
| N4 F7 loophole | See LC R2 |
| N5 quill reads as a fishbone | Replaced by the chart's own compass rose, engraved in gilt light in the open top-left corner |
| N6 gauge cannot count five turns | One gem per turn: five for Brass Wings (a slim gilt bar), three for Super Guide and Flippers (the scholar gauge's own setting), one for the rest (`r2cast` `turns`) |
| N7 card back means two things | Stage 11 has its own sign, a gilt four-point star, with its own legend line ("your pick") |
| N8 accents near peg hues on the board | Power effects are drawn as light: a white-gold flash and ring, with only the outer edge in the carrier's colour |
| Nit: style-shot plaque | It is now the FULL MOON ribbon, small and without laurel |
| Nit: MOON GATE greyish | Line-up power names are lighter (42% toward cream) |
| Nit: the twins' secondary accent | Removed (it was not shown) |
| Nit: the peg-marks leaf | Pointed at both ends, with a midrib |
| Nit: framing to watch | The exp-p1 column is gone. The rope arcs: one rope, exempt as a thin stroke. The exp-p3 rocks are irregular |
| Nit: the Fever preview starts landed | The banner now fades in over the first second, after the light has begun |
| Nit: the title is single-hue | The title's shadows lean sapphire and its mid-tones amethyst |
| Nit: stage 3's mock levels are ground-level | Noted for the real stage 3 levels. The pilots are reused for the mocks, as the spec says |

## UX/UI specialist

| # | Answer |
|---|---|
| M1 power card covers the board at 640 | See GD N3. Nothing is drawn over the opening at 640 |
| m1 gauge against the text | See GD N6 |
| m2 Peg marks On but no marks | The shared state has Peg marks Off (`r2state.PEG_MARKS`); the pause screen shows Off |
| m3 the FULL MOON plate covers pegs | Once Fever has landed, the plate fades to 35%; the lettering stays |
| m4 base-p3 protan separation | The wood's jewel is teal-emerald, quietened within 12–24 units of every peg. Protan separation: 0.141 (approved 0.144). The check is in `readcheck.py` (rule F9) |
| m5 card back's double meaning | See GD N7 |
| m6 no padlocks at 640 | Padlocks show at both sizes |
| m7 THE IRONWORKS DOCK clipped | Tile captions fit their column with an 8-unit inset, and wrap to two lines at the floor size when they cannot fit |
| m8 square cut at the top-right corner | The corner art's vine stem fades out over its last 28 hr px, where the game's next piece would have continued it |
| m9 steppers too small | Gilt chevron buttons, 20 × 20 at 1280 and 16 × 16 at 640, hugging the value |
| m10 fireflies | See the spread rule above |
| m11 the purple star | 55% of the peg, with a light rim |
| n1 accent pairs | Not changed this round. The accents are 25° or more apart in hue, and the name is always beside the colour |
| n2 characters 640 | "Moogle" is used on the small grid; the dimmed cards show their power and stage ("Burst · 4") |
| n3 tally 640, Cid's line | "Cid · Brass Wings ×1" is under his portrait |
| n4 hold progress | The Restart pill is mocked part-way through its hold (a lighter fill sweeping it) |
| n5 callout style | See GD's style-shot Nit |
| n6 pause 640 over the name plate | The window starts below the top rail |
| Motion Nit (report per band) | The spec now gives the largest lift within 8 units of a peg: 0.017 |
