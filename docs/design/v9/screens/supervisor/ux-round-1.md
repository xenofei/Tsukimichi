# Moonfall screens: UX/UI supervision, round 1

**What I reviewed.** I compared all 26 runtime JPEGs in `docs/design/v9/screens/renders/` with the 14 approved mocks in `rich2/screens/`. I read the code at HEAD 6bba684b:
- `MoonfallScreens.cs`
- `MoonfallWindow.Flow.cs`, `.Menu.cs`, `.Pause.cs`, `.Board.cs`, `.Title.cs`, `.Map.cs`, `.Modes.cs`, `.Tally.cs`
- `GameKeyClaim.cs`, `Keyboard.cs`, `Plugin.Moonfall.cs`
- the two Moonfall screen test files
- `MoonfallGameArt.Menu/Frame`

I re-rendered three screens into my scratchpad: the title with `--hint` at 1280 and 640, and Options at 640 with `--marks --reduce-motion`. Cap heights at 640 come from 6–8× point-zoom crops and row profiles. Contrast is the WCAG ratio, measured from the crops (text pixels against the ground's median).

**One caveat.** While I worked, another agent left uncommitted edits in the working tree (`Modes.cs`, `Rich.cs`, `MoonfallGameArt.cs` and others; one of them adds duel turn captions). I did not review those edits. Every finding below is against HEAD and its renders.

## Summary
The menus follow the mocks closely at both sizes. The kit is consistent, Options is spacious, Resume and Continue are the default focus, and the hold-to-confirm, board-arming and Esc ladder are right. Three Majors block approval:
- the first-run Peg marks hint overflows its plate at 640;
- the tally leaks the navigation keys to the game;
- the duel's level code at 640 falls below the number floor.

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title | APPROVE (Nit) | **REVISE** (M1, m1) |
| map | APPROVE | APPROVE (m1, m2) |
| far | APPROVE | APPROVE (m1) |
| levels | APPROVE (m3) | APPROVE (m3, m4) |
| characters | APPROVE (m4, m5) | APPROVE (m5) |
| quickplay | APPROVE (Nit) | APPROVE (Nit) |
| challenges | APPROVE (m6) | APPROVE (m6) |
| duel | APPROVE (M3 style, legible here) | **REVISE** (M3) |
| options | APPROVE (m8) | APPROVE (m8) |
| hud | APPROVE | APPROVE |
| pause | APPROVE (m9) | APPROVE (m9) |
| tally | APPROVE | APPROVE |
| duelhud | APPROVE (m7) | APPROVE (m7) |
| **Input rules** | **REVISE** (M2; m9, m10) | |
| **Overall** | **REVISE** | |

## Findings
- **[Major] M1. Title at 640: the first-run Peg marks hint overflows.**
  - "New: Peg marks, for colour-blind play" runs past the plate's right edge, through its gilt band and into the Companions pill (my render `title-hint-640.png`).
  - Cause: the plate is 14–204 units wide (`Pause.cs:374`), and the floor stops `maxWidth` shrinking the text.
  - Fix: widen the plate to about 14–420 and put it under the Companions/Options row, or shorten the 640 string.
- **[Major] M2. Tally: the navigation keys reach the game.**
  - `HandleKeys` claims navigation only when `Current != Play || (Paused && !LevelOver)` (`Flow.cs:49`). Over the tally `LevelOver` is true, so nothing is claimed.
  - The tally's Replay, Map and Next are focusable buttons (`Tally.cs:476–481`), so the arrows, Enter and Space both move the focus and act in the game (Enter opens the chat, Space jumps).
  - Fix: claim navigation whenever the tally shows: `menu = Current != Play || Paused || LevelOver(g)`.
- **[Major] M3. Duel at 640: the level code "1-1" is 5 px high, against the number floor of 8.**
  - Jupiter sets it in old-style figures; at 1280 they read "I-I" (`Modes.cs:644/695`).
  - Fix: draw the code in TrumpGothic, as Quick Play and the levels do.
- **[Minor] m1. Locked states give no reason without a mouse, and their labels are dim.**
  - Challenges (title at 640), Duel (no reason given anywhere) and The Far Shore tab (tooltip only, no padlock) explain themselves only on hover.
  - Contrast at 640, against the 4.5:1 floor: Far Shore tab 2.8:1, Challenges 3.9:1, "Not reached" 4.0:1, the unselected Novice and Master tabs 4.3 and 4.4:1.
  - Fix: show the reason on keyboard focus as well (`IsItemFocused`) and as a line at 640; add a padlock to the locked tab; lighten the locked ink to at least 4.5:1.
- **[Minor] m2. Map at 640 has no legend and no stop tooltip.**
  - The face-down stop 2 goes unexplained.
  - Level select is reachable only through the stage header, whose only hint is a hover tooltip.
  - Fix: add a state line (for example "Companion not yet met") and a Levels pill to the 640 stage panel.
- **[Minor] m3. Level tiles: captions sit on the frame.**
  - "3-1" starts on the inner edge and ACED touches the right edge at 1280.
  - At 640 the names wrap onto two lines, and "Best 214,300" sits about 3 px above the bottom rule.
  - Fix: an 8-unit inset; one line at 640 (shrink to fit, or drop "The").
- **[Minor] m4. The selection glow draws as a hard slab.**
  - Characters: a teal plate with "MINFILIA" crowded against its edge.
  - Levels at 640: a copper slab spilling about 20 px below the tile and under the neighbouring tiles.
  - Fix: feather it, keep it within 6 units of the tile, and tint it lighter as the mock does.
- **[Minor] m5. Characters.**
  - At 640, "Sage's Path · 10" and "Storm Post · FS" run together as one line.
  - At 640 the power-at-work clip is dropped.
  - At 1280 the clip shows no guide line.
  - Fix: limit each caption to the card's pitch; restore the clip and show the power in it.
- **[Minor] m6. Challenges at both sizes: the 12th row overruns the lower-left corner ornament and the bottom rule.**
  - Fix: inset the rows 24 units from the corners, and page or scroll the list as Quick Play does.
- **[Minor] m7. Duel HUD: whose turn it is reads weakly.**
  - The turn glow is a 0.32-alpha breath, and at 640 the plate labels are dropped.
  - The ball tube shows the player's balls while the rail shows the shooter's power.
  - The opponent's balls are shown nowhere.
  - Fix: show both sides' balls and name the rail's side. The in-flight captions may cover part of this.
- **[Minor] m8. Options.**
  - With Reduce motion on, Decoration still reads "Full" even though Reduce motion overrides it.
  - With Peg marks on, the sample pegs show no marks: `Menu()` never loads the marks sheet (`MoonfallGameArt.cs:219` against `Frame` at `:636`).
  - Fix: mark Decoration as overridden; load the marks in the menus.
- **[Minor] m9. Pause: dragging off a Restart or Leave hold resumes play.**
  - Releasing the hold over the board resumes the game, because `Pause.cs:135` checks only for a release, after the button has dropped its active state.
  - Fix: resume only for a press that also began outside the panel.
- **[Minor] m10. The key claims lapse 250 ms after a menu starts the board.**
  - If Enter or Space is still held at that point (after Play, or after a keyboard hold on Restart), the game sees a fresh press.
  - Fix: keep claiming a key until it is released.
- **[Nit]**
  - The hint's pills are 24 units tall against 38 elsewhere in the kit.
  - Quick Play at 640 opens on page 2, which holds two rows.
  - Selected list rows use a flat band rather than the kit's glow.

## Input rules
These hold, from the code:
- Mouse first.
- Visible focus: `FocusOutline`, the default entry's glow and `SetItemDefaultFocus`.
- Esc goes back. `RespectCloseHotkey` is on only on the title.
- In play, Esc and Start pause and resume.
- Esc is claimed on every screen but the title.
- The board and the crest are `NoNav`.
- The board takes no press while paused, over the tally, or for 0.3 s after it appears.
- Holds need 0.9 s and work with a mouse or a key.
- A collapsed window holds the sound.

They fail at M2, m9 and m10.

## Text floors (measured at 640)
**Labels (7 px cap floor): pass.**
- Tile name 7, "Best" 7, ACED 8, "opens after" 8.
- STAGE 3 7, Minfilia 7, Quick Play and Challenges names 7, "hold to restart" 7.

**Numbers (8 px floor).**
- Pass: level codes 8, stop number 9, tally numbers 10–11, duel HUD score 11.
- **Fail: the duel's level code, 5 px (M3).**

**Secondary text contrast:** 4.7–12.5:1, except the locked labels in m1.

## Unverified
- Hover, keyboard and gamepad focus in the game; in particular, whether ImGui holds a button active while Enter is held for a hold.
- Whether gamepad input reaches the game: `GameKeyClaim` clears keyboard keys only.
- The hold's fill in motion, and all ambient motion.
- Whether Dalamud acts on the same Esc press that returns to the title (its close key comes back on that frame).
- Dalamud's font metrics.
- Window widths between 640 and 1280.
- The uncommitted working-tree edits.
