# Moonfall screens: response to round 1

Each finding from the three round-1 reviews, and what changed. The renders in `../renders/` are re-made from this
state. Three extra renders were added: `title-hint-*` (the first-run Peg marks hint), `options-reduce-motion-*` and
`duelhud-reduce-motion-*`.

## Majors

| Finding | Fix |
|---|---|
| Level critic: the duel's opponent name runs into its score | `DuelPlates` measures the name and fits it into the room left of the score. It shrinks to the label floor, then is cut with "...", or is dropped if even that does not fit (`MoonfallWindow.Modes.cs`, `DuelPlates`, `FitLine`). |
| Game designer: the duel's turn and the opponent's thinking are not readable | The side to shoot gets a glow at 0.5 alpha, breathing ±15% over 3 s, plus a rim in its companion's colour. The waiting plate is dimmed to about 60%. A caption on a dark plate sits under the lit plate: "Your shot", "Louisoix is thinking" with three dots breathing over 2.4 s, or "Louisoix's shot". The waiting side's balls are shown on a chip under its own plate. The renderer now catches the thought; it no longer settles past it. See `duelhud-*`. |
| UX M1: the 640 Peg marks hint overflows | At 640 the hint is one row under Companions and Options (x 60–580, y 436–474): the words on the left, two 26-unit pills on the right. At 1280 it is a journal panel with 32-unit pills. See `title-hint-*`. |
| UX M2: the tally leaks the navigation keys | The navigation keys are claimed whenever a menu shows: a screen, the pause, or the tally (`menu = Current != Play \|\| Paused \|\| LevelOver(g)`). |
| UX M3 and game designer: the duel's level code is in Jupiter old-style figures | The code is drawn in TrumpGothic at the number floor, with the name beside it in Jupiter. |

## Minors and nits

**Locked states**
- The locked pill ink is now #A9B0CC and the inactive tab ink #BCC4E4. Both were lightened to reach at least 4.5:1.
- A locked pill or tab carries a padlock just before its label. The label moves over to make room for it.
- Its reason shows on keyboard focus as well as on hover.
- The Far Shore tab and the 640 Challenges pill both show the padlock.

**Map**
- At 640 the stage panel has a state line ("Companion not yet met in your story", "Win the stages before it to reach it", or the carrier) and a Levels pill.
- An aced level shows ACED in the 1280 stage panel.

**The Far Shore before its levels ship**
- The reached stop has no padlock and is not drained.
- The panel's button reads "Levels on their way", in a new Waiting style: slate, no padlock, no "sealed" tooltip.
- This is `MoonfallLooks.Coming` / `Stop(view, coming)`, and it is tested.

**Level tiles**
- The captions sit 6 units in from the frame.
- Names are always one line. At 1280 they shrink to fit. At 640 a leading "The" is dropped, and the name is cut with "..." if it still does not fit (the strip names the level in full).
- The tile's caption plate is 4 units taller at 640, so Best clears the bottom rule.

**Selection glows**
- `CardSelect` is feathered (the alpha is blurred when the sheet is prepared), tinted 45% toward white, and kept within 9–14 units of the card or tile.

**Characters**
- The 640 captions use short power names, so each stays within its card's pitch.
- The power-at-work inset is a crop round the green peg with each power's own effect drawn on it (guide line, wings, second ball, …) at both sizes.

**Challenges**
- The rows are inset from the corner ornaments and paged as in Quick Play; the last page is a full one.
- The picker's ring is drawn after the medallion's glow, at 1.6 r on a dark under-stroke.
- The pick's line, "No companion: no power" included, shows under the picker.
- A won challenge says "Play again".

**Duel setup**
- Your companion starts as the one Adventure has you with now.
- The opponent's role and line fill the right half.
- The selected opponent's ring shows over its glow.

**Quick Play**
- Opens on the page holding the selection.
- The last page is a full page, not a stub.
- Rows are inset from the ornaments.

**HUD**
- The Ace target sits on a chip under the score plate in Adventure and Quick Play (not in a duel or challenge). It is quiet until the score reaches it, then gilt.
- The tally's plate counts up to the total with the Ace bonus, so the two scores agree.

**Crest**
- On hover the moonstone lifts and its ring brightens; more so while pressed.
- It shows the hand cursor and a tooltip.

**Pause**
- A press must begin outside the panel to resume, so a Restart or Leave hold dragged off the panel never resumes.
- The 640 short line "9 balls · 20 oranges" is back.
- Navigation keys stay claimed while held after the board starts.

**Options**
- Decoration's value is dimmed under Reduce motion, with the note "Reduce motion is on: everything holds still, whatever this says."
- The Peg marks samples always show their marks.
- The menus load the marks sheet.

**Plain fallback**
- The bar's button says Resume while paused and is hidden over the tally.
- The plain tally orders its buttons Replay, Map, Next.
- "Oranges {0}".
- Plain asks for no scene picture.

**Copy**
- "Any companion you have reached" replaces "met".
- The level strip opens with the level's place ("Level 3-3 of The Night Skyway."), then the power's text, which no longer uses a pronoun.

## Not changed

- **HUD margins:** they are still the dark gradient rather than the blurred scene (nit, carried over). This is the board's art pass, not the screens'.
- **Far Shore names:** they stay as proposed, and the owner is asked about them. The designer's notes are passed on: "The Floating Market"; avoid repeating "Lantern"; the two Endwalker place names as a spoiler question.
