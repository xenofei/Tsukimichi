# Moonfall rich pass 2: game designer supervision, round 2

Date: 5 October 2026
Reviewed: commit e9691733, `docs/design/v9/rich2/`.

**What I read:**
- `supervisor/response-round-1.md`;
- `spec-rich2.md`, all sections;
- `characters.md`, `src/r2cast.py` (the accents) and section 8 of `level-method.md`;
- `supervisor/readcheck.json` and `framecheck.json`.

**What I looked at:**
- the 9 screens at 1280 and 640, with crops;
- the line-up, the 6 composites at 1× and 2×, and `pegmarks.png`;
- all frames of the three previews (fever 41, play 48, title 48), with motion-range maps and per-region luma swings.

**What I measured myself rather than trusting the designer's numbers:**
- the OKLab hue bins (30°, chroma above 0.03) on the map, the title and the six board openings;
- the accent hues;
- the beam breathing;
- the firefly positions.

Verdict rule: an asset gets REVISE when it carries a Major or Blocker finding. Minor findings and Nits are listed for fixing, but on their own they do not block an asset.

## Summary

The owner's "still a bit plain" is now answered, and three things carry it:
- **Colour.** The boards have real second jewels: base-p2's rose cloud sea, base-p3's emerald wood, exp-p1's turquoise harbour, exp-p2's aquamarine sea and aurora, and exp-p3's blue world.
- **Fever.** It is now a lighting event: the sky lifts, the moon swells, every peg glows, a burst comes off the last orange, and the cups are lit.
- **The cast.** The new power moment finally lets the cast act in play.

The tally reads as a win, and the sealed level tiles recede. One progress state makes the mocks tell one story.

What remains is polish:
- base-p3's fireflies stand in a ruler-straight row;
- the beams do not visibly breathe;
- the power card covers the board at 640;
- the turns-left gauge cannot count Brass Wings' five turns;
- the F7 check has a loophole.

## Verdicts

| Asset | Verdict |
|---|---|
| title (1280, 640) | APPROVE |
| map (1280, 640) | APPROVE |
| characters (1280, 640) | APPROVE |
| levels (1280, 640) | APPROVE |
| hud (1280, 640) | APPROVE |
| power (1280, 640) | APPROVE |
| fever (1280, 640) | APPROVE |
| tally (1280, 640) | APPROVE |
| pause (1280, 640) | APPROVE |
| characters/lineup.png | APPROVE |
| composites/base-p1 | APPROVE |
| composites/base-p2 | APPROVE |
| composites/base-p3 | APPROVE |
| composites/exp-p1 | APPROVE |
| composites/exp-p2 | APPROVE |
| composites/exp-p3 | APPROVE |
| motion/title.png | APPROVE |
| motion/play.png | APPROVE |
| motion/fever.png | APPROVE |
| screens/pegmarks.png | APPROVE |

## Round-1 findings: status

**M1 (single-hue boards): resolved.** My bins, measured over each board opening with pegs included, show the share of coloured pixels in the second jewel's own bins, at least 60° from the base:

| Board | Second jewel | Share |
|---|---|---|
| base-p2 | rose, 300–360° | 23% |
| base-p3 | emerald, 120–210° | 29% |
| exp-p1 | turquoise, 180–210° | 19% |
| exp-p2 | aquamarine, 180–210° | 18% |

Mean chroma rose from 0.038–0.066 (approved) to 0.056–0.084. The map rose from 0.039 to 0.064 and now reads two-tone: cyan seas against navy land.

On base-p1, exp-p3 and the map, the "second jewel" sits only 30° from the base. They pass F7 only through the checker's neighbour-bin overlap; see new finding N4. Visually they are acceptable, so this is not Major.

**M2 (framing reads as walls): resolved.**
- base-p2: the tracery and trefoils are gone, replaced by organic fir boughs.
- base-p3: the trunks are no longer visible inside the opening, and the leaves are oak.
- exp-p1: the column is at x≈702, right of every peg.
- exp-p2: the boom is gone.
- `framecheck.json`: no fails, with the longest rim-lit run 28.4 units.

Residual low-risk shapes are under Nits.

**M3 (boards not fuller): resolved.**
- Every board has a depth layer and a light event, visible at 1×.
- base-p1 is still the sparsest board (0.76% cover), but the chart itself is busy.
- Its new quill reads poorly; see N5.

**M4 (Fever not a spectacle): resolved.**
- The arrival measures a +14% luma lift over the board (frame means 54.4 to 62.0).
- The moon's glow swells, the lit pegs carry halos, and a radial moondust burst comes off the last orange.
- The cups are lit, with the centre brightest.
- One ribbon, with the laurel beside the words, not over them; the subtitle sits on the ribbon.
- It now reads as the payoff.

**M5 (tally headline): resolved.** LEVEL CLEAR is on the ribbon at both sizes. ACED and NEW BEST form a callout. "·" renders at 640.

**M6 (sealed tiles loudest): resolved.** The sealed tiles are drained, with a padlock. 3-3 glows. At 640 the tiles sit in one row, with Play in the selection strip.

**M7 (preview colours): resolved.** The play preview is true colour, and green and purple are exact.

**M8 (no power moment): resolved.** Cid's card, Brass Wings in copper, the gilt wings on the cart, an accent ring at the green, and a LONG SHOT callout. The 640 overlap is N3.

**Minors and Nits from round 1:**
- **m1: resolved.** Accent hues measured: Alisaie 13° (secondary), Raubahn 27, Cid 52, Kan-E-Senna 118, Merlwyb 148, Louisoix 176, Minfilia 200, the moogle 228, the twins 257, Urianger 286, Y'shtola 316, Tataru 355. The primaries are at least 25° apart and off 60–100°. Power names on the rail sit on a dark plate.
- **m2: resolved.** Each stage is named for its companion's home. One mock-only oddity is a Nit.
- **m3: resolved.** Stage 3 with Cid everywhere (title, map, levels, HUD, power, fever, tally, pause).
- **m4: resolved.** The selection glow is in Minfilia's aquamarine.
- **m5: resolved.** The Super Guide thumbnail is on a real board.
- **m6: resolved.** The moogle's pom-pom is in the ring.
- **m7: resolved.** Blurred scene margins, the scrolls removed, the rail shelves.
- **m8: resolved.** The neat-line is on the frame's edge.
- **m9: resolved** (rope arcs remain as a Nit).
- **m10: resolved.**
- **m11: resolved.** The whole title is shown, and the motion map shows no motes over the buttons or the card. Logotype motion is the one glint.
- **m12: partly resolved.** The fireflies have halos, but the beams still do not visibly breathe (N2), and the fireflies are now in a row (N1).
- **m13 and m14: resolved.**
- **n1, n2, n3: resolved or answered.** The Continue card is the default focus, with Cid.

**The twins' card era** (my round-1 unverified note): answered by the plugin's CardEra rule (No. 59 is below No. 68, where Heavensward starts). Accepted.

## New findings

### Minor

**N1. base-p3's fireflies stand in one straight, evenly spaced row (base-p3; play.png; also visible on levels and characters thumbnails).**
- **What I see.** About 15 points sit at y≈555 (1×), spaced 37–60 units, across the full width just above the cart rail. Bright rows in play frame 0 are at y 427–438 (640 scale) only.
- **Why it matters.** The row reads as runway lights, or as a dotted "floor" line above the bucket. Either way it is a noise line, not fireflies, and it undoes the "alive" read on the board that carries the play preview.
- **Fix.** Place the fireflies by sampling free space from the clearance distance field in 2-D:
  - pockets between the bottom rows (y 470–560), above the ferns, and among the top-corner foliage;
  - jittered spacing, height, size, brightness and twinkle phase;
  - F5 kept by the same field.

**N2. The beams do not breathe visibly (play.png).**
- **What I measured.** Mean luma in the upper-left beam area (640 px coordinates (90–290, 60–260)) swings 0.1% over the loop. The mid board swings 0.2%.
- **Why it matters.** The spec and the response claim "breathe ±15%". At the F4 cap of 0.08, ±15% is about ±0.012, which is imperceptible by construction. The only board motion is the dust motes and the fireflies.
- **Fix.** Make the beams move rather than pulse: drift the slow noise that breaks the shafts (moving gaps, about 3–6 px/s), which reads as light through moving leaves without raising the cap. Or drop the claim from the spec.

**N3. The power card covers the board at 640 (power-640, card at about (65–145, 95–235)).**
- **What I see.** At 1280 the card sits in the margin, which is right. At 640 it lies over the top-left of the opening for 1.2 s while the ball is live. It hides two or three pegs and any ball passing there.
- **Fix.** At 640, show the card in the left rail's space (over the ball tube, which is static), or shrink it to the rail medallion with a ribbon carrying the power name. Never draw over the opening during flight.

**N4. The F7 check double-counts neighbouring hue bins (`readcheck.py` `jewels()`).**
- **The flaw.** The second jewel's share includes its ±1 neighbour bins, so a hue 30° from the first jewel counts toward both jewels.
- **Where it shows.**
  - base-p1: the "195°" second jewel has 0% in its own bin (180–210); all 25% is in 210–240, beside the base at 240–270.
  - exp-p3: the "225°" jewel is 20% at 240–270 and 0% at 210–240. The magenta and teal nebula measures about 1%.
- **Fix.** Make the two jewels' bin sets disjoint, so the second jewel's bins exclude the first's ±1. Then:
  - base-p1: push the seas to a true teal (180–210°);
  - exp-p3: make the nebula register.

**N5. base-p1's quill reads as a fishbone or zipper (composite (88,65)–(180,170); hud-1280 (235,85)–(370,265)).**
- **What I see.** A straight dark shaft with regular comb barbs on a 45° diagonal. The inkwell "beyond the corner" is an unreadable dark blob at about (75,45).
- **Why it matters.** It is ornament without readable meaning, against the owner's taste, sitting in the top-left shot lane.
- **Fix.** Give it a true feather silhouette (a tapering vane, soft irregular barbs, a lit nib), or use a navigator's object that reads at a glance: brass dividers, or a compass rose on the chart.

**N6. The turns-left gauge cannot count Brass Wings (power, hud).** It has three scholar gems, but Wings lasts five turns (`moonfall-powers.md`). Fix: one gem per turn for each power (five for Wings), or a TrumpGothic count beside the gems.

**N7. The card back means two things on the map (map stop 11, legend "not yet met · 11: your pick").** The spoiler-shield symbol is reused for the "Your Pick" stage. Fix: give Your Pick its own icon, for example three fanned cards or a gilt star, with its own legend line.

**N8. Some accents sit on peg hues and are drawn on the board.**
- **The overlaps.** Cid's copper (52°) and Raubahn's flame (27°) sit beside the orange peg. Merlwyb (148°) sits beside the green peg, and Y'shtola (316°) beside the purple one.
- **Where it shows.** On power-1280, the copper ring round the green peg at (293,682) reads close to an orange halo.
- **Fix.** On the board, draw power effects as light (a white-gold core with an accent edge, never a filled disc), with chroma below the pegs'.

### Nit

- **The style-shot callout.** It is a maroon wooden plaque, not the "small gilt ribbon" of the spec, and does not match the Fever and tally ribbon family. Use the ribbon at a small size.
- **"MOON GATE" on the line-up.** Urianger's star-violet still reads greyish on the violet ground.
- **The twins' secondary accent.** #E0506A (13°) is 14° from Raubahn and 18° from Tataru, if it is shown anywhere.
- **The pegmarks leaf.** At 1× it reads as a blob. In the deuteranope view, orange and green differ mainly by crescent against blob. Give the leaf pointed tips and a midrib. The star and crescent are good.
- **Low-risk framing to watch in play.** All of these pass framecheck:
  - exp-p1's lamp column (x≈702, y 480–590) and the top rail of the balustrade (y≈549, x 652–720);
  - exp-p2's thin rope arcs in the top corners;
  - exp-p3's dark, rim-lit discs in the top corners, about 30 units across, at about (85,55) and (700,65).
- **The Fever preview.** It opens with the banner already landed, so the build-up is not shown.
- **The title backdrop.** It is still single-hue (93% in one 30° bin). Add the sapphire to the shadows that the palette table promises.
- **Stage 3's mock levels.** "The Night Skyway" (seen from the air) holds ground-level pilots (the Moonlit Post in the Twelveswood, the Holy See). Make the real stage 3 levels skyward.

## Unverified

How the motion, the Fever arrival and the power card's slide feel in the game at real frame pacing. Everything above is from stills and rendered previews.

OVERALL: APPROVE
