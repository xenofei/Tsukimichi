# Moonfall screens: level-design critic supervision, round 1

**Reviewed:** spec-rich2 §1, 2, 4 and 5; level-method; my round-1 review and the round-2 verdicts; the eight committed renders and the earlier `runtime/screens/base-p1-*`; DrawBoard, RichHud/LevelPlates, DuelPlates/FaceRing, RichEnd, DrawPauseMenu/PauseCrest. I rebuilt the renderer (Release, clean) and rendered into my scratchpad (`…/scratchpad/lvlcritic/`): play at 640 (default, `--reduce-motion`, `--seconds 3`, legacy), play at 1280, duelhud at 1280 and at 640 with `--reduce-motion`, and `--decoration off` for play at 640 and 1280, pause at 640 and tally at 640. For the F6-style check I fitted the view to the peg centres of `base-p1.json`, then took each peg's face p80 luma within 0.6 r against the ring p90 from r+2 to r+9, with the other pegs masked out.

## Summary
Removing the toolbar helps the board. It is now 1.268 px a unit at 1280 (was 1.2155) and 0.735 at 640 (was 0.631), so an r 9 peg is about 13.2 px across at 640 (was 11.4). The board is centred with an 8 px gap above and below. Readability holds:

| Size | Purple | Orange | Blue |
|---|---|---|---|
| 1280, new (old) | 0.425 (0.432) | 0.355 (0.348) | 0.415 (0.417) |
| 640, new PNG | 0.426 | 0.353 | – |
| 640, committed JPEG (old JPEG) | 0.405 (0.450) | – | – |

The 640 JPEG drop is within the encoding and ambience noise; my PNG with Reduce motion gives the same margins. The pause menu and the tally keep to the chrome's rules, and the HUD meets its floors at 640. One Major blocks: the duel's opponent plate puts the name and the score in the same space.

## Verdicts
| Render | Verdict |
|---|---|
| hud-1280 | APPROVE |
| hud-640 | APPROVE |
| pause-1280 | APPROVE |
| pause-640 | APPROVE |
| tally-1280 | APPROVE |
| tally-640 | APPROVE |
| duelhud-1280 | REVISE |
| duelhud-640 | REVISE |
| plain fallback (`--decoration off`, 1280 and 640) | APPROVE |

**OVERALL: REVISE** (one Major, in the duel plates)

## Findings
- **[Major] duelhud-1280 and -640: the opponent's name runs into its score.**
  - The name is set left from x 618 and the score right-aligned at 701, which leaves 83 units for both.
  - Measured at 1280: LOUISOIX spans px 917–971. The tally's "136,600" in the same plate spans px 963–1022.
  - So LOUISOIX overlaps any 6-digit score, URIANGER and THE TWINS overlap 5 digits, and KAN-E-SENNA (about 70 px) overlaps from 4 digits.
  - Fix: measure the name and fit it in the room left of the score. Shrink it no lower than the label floor, then cut it with an ellipsis. Or move the name above the score.
- **[Minor] duelhud-640: neither plate says whose side it is.**
  - YOU and the opponent's name use `LabelPx`, so both are dropped at 640. The plates then show only two 15 px faces; Cid and Louisoix are both white-haired.
  - The YOU plate has about 113 px free.
  - Fix: set YOU at `NamePx`, as the level name is. Set the opponent's name at `NamePx` within the fit above, and drop it only if it cannot fit.
- **[Minor] duelhud: the turn glow is weak and too fast.**
  - Measured just outside the plate edge, the foe's glow lifts luma by about 0.07 (96.5 against 78.8 on the idle plate). It is 6 px wide at 1280 and about 4 px at 640.
  - It breathes ±25% over 2 s; the motion rules allow ±6–15% over 3–6 s.
  - Fix: raise the alpha to about 0.5, add a rim tint in the accent colour, and breathe ±15% over 3 s.
- **[Minor] The crest's pause button has no visible cue.** `crestHovered` is set but never read: the moonstone does not change on hover, and only a tooltip shows. Fix: brighten the moonstone and its ring on hover and press (for example the ring at 1.3× and the stone lifted by 0.1), and show the hand cursor.
- **[Minor] tally-1280 and -640: two different scores are on screen.** The plate reads 136,600 while TOTAL reads 161,600; `aceBonus` is added only to the tally. Fix: have the plate count up to the same total.
- **[Nit] duelhud: the thinking dots' place and pace.** They sit at y 38.5, 2.5 units above the opening, on the rim where the ball flies, and pulse at about 1.3 s. They are more than 100 units from any piece and still under Reduce motion. Fix: put them inside the opponent's plate, beside the score, and slow them to 2 s or more.
- **[Nit] Plain fallback bar:**
  - "Orange 20" should read "Oranges".
  - The bar's Pause button still says "Pause" while paused and on the tally.
  - The plain tally orders its buttons Next, Replay, Map; the rich one orders them Replay, Map, Next.
- **[Nit] Decoration Off logs a warning.** On those runs the renderer logged "airship-road(@2x).png is missing; the level shows the night sky": Plain asks for a picture that does not ship. Fix: skip the request under Plain.
- **[Nit] pause-640 leaves out the line with the level, balls and oranges.** That line is the only reminder of the board's state behind a menu that covers it. Restore a short version (for example "9 balls · 20 oranges") at the floor.

**What passed, with measurements:**
- **HUD floors at 640:** 3-3 about 9 px, level name caps about 10 px, score 12.8 px, balls 11 px, ×1 digit 9 px, oranges 11 px. All meet the floors.
- **Tally at 640:** "Ace bonus" caps 8.7 px, its value 11 px, button caps about 10 px. The LEVEL CLEAR ribbon now clears the name plate.
- **Pause at 640:** the panel sits below the top rail, from about y 70 to 467.
- **Plain fallback:** the bar is one row; the 640 board is 0.685 px a unit. Every kind reads on the flat ground.

## Unverified
- The thinking dots: the renderer settles past the opponent's thought, so none of my renders caught them. I judged them from the code.
- The collision with real duel scores: I projected it from glyph widths measured at 0 and 136,600. I did not render a duel at those scores.
- The cold-start swap from the 1x to the 2x tier mid-board: I could not force it.
- Hover states, in-game rendering, other levels and other deals.
