# Moonfall rich pass 2: UX/UI supervision, round 1

Date: 5 October 2026
Reviewer: game UX/UI specialist supervisor (independent)
Reviewed: `docs/design/v9/rich2/` at commit 1ef1ab72. I looked at `spec-rich2.md`, `characters.md`, `level-method.md` §8, all 16 screens (8 screens at 1280 and 640), `characters/lineup.png`, the 6 composites at 1× and 2×, and both APNG previews (all 60 frames of each, decoded with Pillow). I compared everything with the approved `../rich/screens/`. Measurements were made with numpy: WCAG contrast, ink heights, OKLab colour-blind simulation (Machado 2009), and per-pixel motion range against the base-p3 peg map.

## Verdicts

| Asset | Verdict |
|---|---|
| Title (1280, 640) | **REVISE** |
| Adventure map (1280, 640) | **REVISE** |
| Characters (1280, 640) | **REVISE** |
| Level select (1280, 640) | **REVISE** |
| In game HUD (1280, 640) | **REVISE** |
| Fever (1280, 640) | **REVISE** |
| Tally (1280, 640) | **REVISE** |
| Pause (1280, 640) | **REVISE** |
| Character line-up | **APPROVE** (Minors only) |
| HUD chrome on the 6 composites | **REVISE**. Peg readability on every board passes; the chrome does not (M5, m4, m5). |
| Motion: title | **APPROVE** |
| Motion: play | **REVISE** |

## What works

These are confirmed and need no change:
- **Peg readability holds.** The readcheck minimum margins are all at or above 0.246.
- **The ambient motion is well behaved in the APNG.** In `motion/play.png`, pegs and the 2.5-unit ring round them show zero luma change across all 60 frames. HUD type (name, stage, rail label, gems, multiplier) shows zero change.
- **Both loops are seamless.** The last-to-first frame difference is the same as the normal frame-to-frame difference.
- **The glints are timed as specified.** Each glint plays once per loop: play at t = 3.0–3.7 s, title at t = 1.2–1.8 s.
- **Title moondust is masked out of the buttons**, and it is subtle.
- **The game's own frames, pills and fonts read clearly as FFXIV.** The previous "plain" complaint is answered at 1280.

The problems are concentrated in three places: the 640 size, the level-select hierarchy, and several composition collisions.

## Findings

### Blocker
None.

### Major

**M1. Title 1280: the Continue card's title runs into the frame.**
- *What I see:* "4-3 THE AIRSHIP ROAD" ends at about x 1245, over the window's right gilt band (x ≈ 1228–1245, y ≈ 640–658). The "D" is drawn on the gilt.
- *Why it matters:* this is the first screen, and the clipped text is its most useful information.
- *Fix:* fit the title to the text column (shrink to fit, or drop to two lines), or widen the card. Add a rule to the kit: text must stay at least 8 units inside a frame's inner edge.

**M2. Adventure map, both sizes: open and locked stops cannot be told apart, and the stage-5 state contradicts the rest of the screen.**
- *What I see:*
  - The spec says locked stops are dimmed. Measured ring luma: done or open stops 1 and 3 are 0.42–0.43; locked stops 6–11 are 0.26–0.31. That is a small difference, and every ring is still full gilt.
  - The stop-5 portrait (Merlwyb) has a core luma of 0.67, the same as done stops 1 and 3 (0.68–0.70). It is not dimmed at all.
  - The header says "4 of 11 stages" and stop 4 glows as "here", yet the Characters screen also shows Merlwyb (stage 5) face up with her power.
  - The only cues for state are the small orange pips and solid versus dashed road.
- *Why it matters:* the player cannot see at a glance which stops can be played. The mock also teaches the wrong rule.
- *Fix:*
  - Locked stops: portrait at about 45% luma with desaturation, the ring in the "locked" grade (as `_pill('locked')` already does), and a small lock or seal mark. This is meaning, not noise.
  - Done stops: keep the pip.
  - "Here": keep the glow.
  - Make stage 5 consistent across the map, Characters and the line-up (dimmed, "meet at stage 5").

**M3. Level select, both sizes: the hierarchy is inverted.**
- *What I see:*
  - The sealed tiles 4-4 and 4-5 (1280 x 764–1236, y 176–424) are the brightest, most saturated and most textured things on the screen. They are the Triple Triad card back in tooled gold.
  - The playable tiles are dark boards.
  - The open or selected tile 4-3 has no visible mark. The band above it measures 0.238 luma against 0.186–0.200 for the others, which is invisible in practice. The spec promises "the open tile glows".
  - The approved pass had quiet enamel seals, and the hierarchy was right.
- *Why it matters:* the eye goes to what cannot be played. With keyboard or gamepad there is no visible focus.
- *Fix:*
  - Sealed tiles: show the card back darkened to about 40% with low chroma (the "locked" grade), or use the enamel seal again with the crest small in the middle.
  - Give the selected or open tile Triple Triad's selection glow (`TripleTriadCardSelect`, already used on the Characters screen) at full strength, plus a brighter frame.

**M4. Level select 640: captions are clipped and the Play button overlaps a tile.**
- *What I see:*
  - The bottom row's frames cover the top row's second caption line. "Best 214,300", "Best 251,880" and "Ace score 240,000" are cut through at y ≈ 266–271.
  - "ACED" is overlapped by the moon pip (x ≈ 370–400, y ≈ 262).
  - The "Play 4-3" pill overlaps the right frame of tile 4-5 (x ≈ 500–516, y ≈ 428–462).
- *Why it matters:* scores are clipped, and the primary action collides with a locked tile.
- *Fix:* reflow the 640 grid. Either use a 3 + 2 grid with a 10-unit gutter and the caption inside the tile, or use a single row of five smaller tiles with a caption strip. Move Play into its own row, or into a selection strip under the grid.

**M5. Every 640 screen, plus the composites: text below the minimum (no label under about 9 px, no number under 10 px).**

| Screen | Text | Measured |
|---|---|---|
| hud-640 (×N dial) | "×1" | Digit about 8 px; the "×" collapses to a 2 px blob, so it reads "-1" or "•1" (x ≈ 600–622, y ≈ 64–86). The approved pass drew a clear 9–10 px "×1". |
| Composites at 800 × 600 | "×2" | Reads "•2" (x ≈ 742–770, y ≈ 85–105). The lattice ring's heavy band eats the dial's opening. |
| hud-640 | ORANGES, BALLS, SCORE | Caps about 4–5 px (for example ORANGES at x ≈ 594–628, y ≈ 133–138). The approved "Oranges" was about 8 px. |
| fever-640 | Cup values 10,000 / 50,000 / 100,000 | About 5 px (y ≈ 461–470). These are the numbers behind "pick your cup". |
| pause-640 | "hold to restart" | Ink about 6 px (x ≈ 292–348, y ≈ 196–203), at 3.3:1 contrast. This is the safety cue for a destructive action. |
| characters-640 | "Super Guide", "Brass Wings", "stage 6" … | About 7 px ink. |
| levels-640 | "opens after 4-3" | About 6 px, on the busy card-back texture. |
| hud-640 | "4-3" in the name-plate ring | About 5 px. |
| map-640 | Stop numbers | About 7 px; "4" and "6" are hidden by the road and the neighbouring ring. |

- *Why it matters:* the owner's stated minimum is broken on gameplay-critical numbers (multiplier, cup values) and on the destructive-action cue.
- *Fix:*
  - Set floors in the kit: AXIS labels at 9 px or more, TrumpGothic numbers at 10 px or more at 640.
  - Dial: use the smaller lattice ring at 640 so the opening is at least 26 px, and set the multiplier in TrumpGothic at 12 px or more with a real "×" (or drop the "×").
  - Where a label cannot fit at the floor, drop it at 640 (as the title already drops its subtitles) rather than shrink it.

**M6. Fever, both sizes: the laurel is drawn over the title.**
- *What I see:* the two laurel sprigs rise through the second "L" of FULL and the first "O" of MOON (1280 x ≈ 540–560 and 735–760, y ≈ 240–330; 640 x ≈ 255–270 and 375–390).
- *Why it matters:* it reads as a compositing error on the game's signature moment.
- *Fix:* draw the laurel behind the lettering and ribbon, or split the sprigs outward to frame the words. Keep the leaves at least 6 units clear of the glyphs.

**M7. Tally: the "LEVEL CLEAR" plaque is blank at 640 and half off its plaque at 1280.**
- *What I see:*
  - At 640 the crowned plaque (x ≈ 300–335, y ≈ 5–50) is an empty dark tablet with no text.
  - At 1280 the plaque body (x ≈ 612–668, y ≈ 85–120) is empty. "LEVEL CLEAR" sits on its lower edge and over the window's top rule, at about 8 px.
  - The crown also stacks on the HUD's launcher crest behind it (x ≈ 600–680, y ≈ 70–100).
- *Why it matters:* the screen's core message is the smallest text on it, and at 640 it is missing.
- *Fix:*
  - Put "LEVEL CLEAR" (Jupiter, gilt, 14 px or more at 1280 and 10 px or more at 640) inside the plaque, sized to it.
  - Or drop the plaque and set "LEVEL CLEAR" on the ribbon.
  - Dim or hide the HUD crest while the tally is up, or move the window down 20 units.

**M8. Tally 640: garbled text "ACED tt new best".**
- *What I see:* the string at x ≈ 172–232, y ≈ 310–320 renders as "ACED tt new best". The middle dot falls back to the wrong glyphs and the ace-score phrase is lost.
- *Why it matters:* corrupted player-facing text on the payoff screen.
- *Fix:* set this line in AXIS, which has "·", as at 1280: "ACED · new best". Add a glyph-coverage check for every face and string in the mocks.

**M9. Pause 1280: the settings collide with the frame, and the window is cramped.**
- *What I see:* the footnote "Moonfall pauses itself in combat, duties and cutscenes." (y ≈ 662–673) touches the bottom-left and bottom-right corner ornaments. The "Sound" row (y ≈ 643) sits inside the corner-ornament zone.
- *Why it matters:* the owner asked for spacious settings, and here text overlaps chrome.
- *Fix:* make the window about 60 units taller, or move the quick settings out (for example into Options). Keep all text above the band where the bottom corner ornaments sit (inner edge minus about 40 units).

**M10. Motion, play: fireflies drift onto the pegs.**
- *What I see:*
  - In `motion/play.png` (Pillow-decoded), luma rises by up to 0.76 at 2.5–4 units from a peg's edge. 1.1% of that ring rises by more than 0.15.
  - There are 26 hotspots, for example (130, 490), (325, 445), (590, 415) and (570, 465). In frames, warm dots sit against the edges of orange and blue pegs.
  - Cause: `motion2.play_loop` places 16 fireflies at random in x 110–690, y 420–540, wandering 9 px. They are masked only by the 2.5-unit ring.
- *Why it matters:* this contradicts F5 ("no warm point within 8 units of any peg's edge") and motion rule 3 ("no ambient layer may lift a pixel near a peg by more than 0.03"). A warm speck flaring at a peg's rim can read as a hit spark or an orange flicker.
- *Fix:* place fireflies, including their whole wander path and their 3.5 px halo, at least 8 units from every peg edge (as `dress2.points` already does for still lights). Add the 2.5–8 unit band to the motion check.

### Minor

**m1. Gilt frame joins show seams** (title thumbnail, level tiles, map header).
- *What I see:*
  - The Continue thumbnail's corner blocks have dark outlined miter squares at (897, 603), (1050, 603) and (897, 725). Its bottom band also overlaps the outer window's banner corner.
  - The level-tile corners show blue seam lines (for example x ≈ 736 and 768, y ≈ 190).
  - At the right end of the map-640 header (x ≈ 622–632, y ≈ 5–35), an enamel line crosses the gilt and an 8 px black gap is left at the window edge.
- *Fix:* cut the corner pieces with a 1 px overlap and alpha-feathered miters, and draw the header frame to the window edge.

**m2. The pills show faint vertical seam lines** above and below their cap joins (for example pause 1280 at x ≈ 515 and 766, between Restart, Options and Leave). This is the stretched middle slice of the pill's shadow strip.
- *Fix:* blit the shadow as its own 9-slice, or blur the shadow strip across the joins.

**m3. The HUD margins do not match the spec, and they carry floating ornaments.**
- *What I see:*
  - hud-1280's margins are flat navy, not "the level's scene, blurred" as §4 states.
  - They carry floating Journal-scroll fragments (x ≈ 35–70 and 1210–1245, y ≈ 320–475).
  - A hard-cut fragment hangs under BALLS (x ≈ 145–170, y ≈ 545–660), and its mirror is in the right rail.
- *Why it matters:* this conflicts with the owner's rule against noise glyphs (ornament must be structure or meaning).
- *Fix:* blurred scene in the margins as specified, and remove the free-floating pieces. Use the scroll piece only where it terminates a rule or rail.

**m4. The rail power label has no fitting rule.**
- *What I see:* LUNAR BURST, SUPER GUIDE and STORM POST wrap to two lines. MOONBLOOM runs on one line from wall to wall of the rail (exp-p1 composite x ≈ 727–790; tally-1280 x ≈ 1078–1170). FIREBALL fits on one line.
- *Fix:* one rule: one line if it fits within the rail minus 6 units on each side, otherwise wrap, with a size floor.

**m5. The ×N dial has an extraction artifact.** A stray lattice arc sticks out of the dial's upper-left (composites x ≈ 731–737, y ≈ 72–92; hud-640 x ≈ 588–594, y ≈ 60–80). A piece of a neighbouring ring was picked up by the radius cut.
- *Fix:* tighten the cut mask for that ring.

**m6. Pause, both sizes: the toggle state is ambiguous.**
- *What I see:* Reduce motion is a bright gilt knob on the left of a dark track, with no On/Off text; the approved pass said "Off". Decoration "Full" and Sound "70%" look like plain values, with no stepper or slider affordance.
- *Fix:* fill the track in the accent when on, add "On"/"Off" in AXIS, and add ‹ › steppers to Decoration and Sound.

**m7. Pause: "Leave to the map" throws away the level in progress with no hold or undo.**
- This carries over from the approved pass, but it falls under the owner's safety rule for destructive clicks.
- *Fix:* give it the same hold-to-confirm as Restart, or an undo toast on the map.

**m8. Title 1280: no default focus, and the Continue card is not visibly a button.**
- *Fix:* add a "Continue 4-3" primary pill in the card, or give the card the focus glow. Show the focused state on one control.

**m9. Mock type spacing (AXIS) is broken at 640.**
- *What I see:* "runso n", "go es", "vo ice" (characters-640 description); "Decoratio n", "So und" (pause-640); "op ens after" (level select).
- In-game ImGui raster will differ, so this is a mock defect, but the owner judges from these images.
- *Fix:* apply the FDT kerning and advance correctly in `r2font.render_mask`.

**m10. Secondary text with meaning is below 4.5:1 contrast.**

| Text | Contrast |
|---|---|
| "Spoilers: A Realm Reborn" (characters-1280, x 981, y 366) | about 3.3:1 |
| "meet at stage N" | about 3.8:1 |
| Line-up "stage N" | about 3.0:1 |
| "hold to restart" | about 3.3:1 |

- *Fix:* lift these to at least 4.5:1 (for example, cream at 75% instead of slate).

**m11. Characters: two inconsistencies.**
- The face-down card shows its power (Multiball), while the dimmed cards hide theirs. Show the power on all of them.
- Back is top-right here but top-left on the map and level select. Put it top-left everywhere.

**m12. Characters 640: the Super Guide medallion overlaps the detail window's left frame** (x ≈ 333–345, y ≈ 205–255), and "SUPER GUIDE" butts against the ring.
- *Fix:* inset it by 12 units and add a 6-unit gap before the text.

**m13. Colour-blind distinguishability.**
- **New in this pass (base-p1):** the parchment lands lower how much the orange pegs stand out in colour (OKLab a, b distance, peg core to a 12–20 px ring):

  | Vision | Measure | Approved pass | Rich pass 2 |
  |---|---|---|---|
  | Normal | 10th percentile | 0.154 | 0.114 |
  | Protan | 10th percentile | 0.129 | 0.086 |
  | Worst peg | — | 0.153 | 0.063 |

  The luma margins still pass. In the protan and deutan simulations, orange pegs and the lands both become olive-tan.
- **Recommendation:** on owner question 4, go back to the cooler chart, or cut the lands' chroma by about half.
- **Carried over from earlier passes:** the peg kinds differ by hue only. Under deutan, purple and blue are ΔE_ok 0.078 apart; under tritan, green and blue are 0.049 apart.
- **Fix:** add a colour-blind assist in Options that puts a distinct mark on each kind, such as a crescent, leaf or star (marks that carry meaning).

**m14. Motion, play preview: the lantern flicker is on the wrong spot, and the carrier glow is missing.**
- The lantern flicker is applied at the frame's bottom-left corner (24, 586; rise 0.12). The lantern itself (about 570, 540) has zero change. The cause is the warm-argmax search in `motion2.py`. A flicker on the frame also contradicts motion rule 1.
- The spec'd carrier-medallion glow (±10%, 3 s) is absent: zero change over the loop.
- *Fix:* anchor the flicker to the bucket's lantern, and render the medallion glow, so the preview shows what the spec describes.

**m15. Map: stops 5 and 6 crowd each other** (rings nearly touching at about (440, 440)). At 640, stop numbers 4 and 6 are covered. The face-down stop 2 has no "Not yet met" cue on the map.
- *Fix:* nudge the stop positions, keep the numbers clear of the road and rings, and add a hover or focus label.

**m16. Buttons are set in Jupiter small caps everywhere.**
- FFXIV sets button labels in AXIS and keeps Jupiter for titles and zone names. At 640, the small caps fall to about 5–6 px x-height ("COMPANIONS" and "OPTIONS" on title-640).
- *Fix:* use AXIS for small and secondary pills (at least at 640), and keep Jupiter for primary pills and titles.

### Nit
- **n1.** The moogle is "The Moogle" on the Characters grid but "A moogle courier" on the line-up. Use one name.
- **n2.** The map panel's "THE SHROUD BY NIGHT" ends about 6 px from the frame's right corner (1280 x ≈ 1228). Allow more margin.
- **n3.** On the line-up, the Multiball sapphire on the amethyst ground is 3.5:1. Lift its lightness slightly, keeping the hue.
- **n4.** The Fever sub-line "Every moon left is worth more. Pick your cup." crosses pegs and constellation lines (1280 y ≈ 377–395). Contrast is fine (11.7:1); a soft dark backing would calm it.

OVERALL: REVISE
