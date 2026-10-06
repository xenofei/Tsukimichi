# Moonfall screens: UX/UI supervision, round 2

**What I reviewed.** Before reviewing I read my round-1 review, the implementer's response and the other two round-1 reviews. I compared the 32 JPEGs in `docs/design/v9/screens/renders/` (made from HEAD b47c7834) with the round-1 findings. I read the input and screen code at HEAD:
- `MoonfallWindow.Flow.cs`: `HandleKeys`, `Back`, `StartBoard`
- `MoonfallWindow.Pause.cs`: `DrawPauseMenu`, `PegMarksHint`, `DrawOptions`
- `MoonfallWindow.Menu.cs`: `MenuButton` with the Waiting style, `MenuTab`, `MenuStepper`, `LockedInk`, `IdleTabInk`
- `MoonfallWindow.Map.cs`: `SmallStagePanel`, `StagePanel`, `LevelTile`, `FitLine`, `MakeLevelsWords`
- `MoonfallWindow.Modes.cs`: the Quick Play and challenges paging, `DuelPlates`, `BallsChip`, `TurnCaption`, `IdleDim`
- `MoonfallWindow.Rich.cs`: `AceChip`, `NumberPx`, `NamePx`
- `MoonfallWindow.Board.cs` (the board's button) and `MoonfallWindow.Title.cs`
- `GameKeyClaim.cs`, `Keyboard.cs`, `MoonfallPause.cs`
- `Tsukimichi.Tests/Moonfall/MoonfallScreensLintTests.cs`

I re-rendered with the built renderer into my scratchpad (`…/scratchpad/r2/`), never the repo:
- every screen at 640 as PNG;
- title, levels, characters, quick play, challenges, tally, pause and duel HUD at 1280;
- `--hint`, `--reduce-motion` (Options and duel HUD) and `--marks`.

**How I measured.**
- Cap heights come from row profiles: a row counts as text when a pixel passes halfway between the ground and the text luminance.
- Contrast is the WCAG ratio between the ground's median and the text's 97th to 99th percentile luminance.
- Crops are 4–16× point zooms, named `z-*.png` in that folder.
- I also extracted the round-1 `levels-1280.jpg` from 6bba684b to tell regressions from carry-overs.

## Summary
All three Majors are fixed:
- **M1, the hint:** at 640 it is one row under Companions and Options, and it no longer overflows.
- **M2, the tally:** it now claims the navigation keys.
- **M3, the duel's level code:** it is TrumpGothic at 10 px.

The minors are mostly fixed as well:
- Locked labels now measure 4.9–6.3:1, and each locked pill or tab has a padlock and gives its reason on focus.
- The 640 stage panel has a state line and a Levels pill.
- Tile captions are inset and fit on one line.
- The selection glows are feathered.
- The power-at-work clip is back at both sizes.
- The challenge rows clear the corner ornaments.
- Options shows the override and the marks.
- Releasing a hold dragged off the panel no longer resumes.
- Navigation keys stay claimed while held.

No Major remains. Seven Minors and a few Nits remain:
- Five are new or were missed in round 1: a double click on Resume can shoot; the overlapping pages; the waiting plate's contrast; the 1280 Best line on the rule; the tests not covering the input fixes.
- Two are residues of round-1 findings: locked Duel still gives no reason, and sealed level names are lost at 640.

**Overall: APPROVE, with the Minors below listed.**

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title | APPROVE (m11) | APPROVE (m11; M1 fixed) |
| title-hint | APPROVE | APPROVE (M1 fixed) |
| map | APPROVE | APPROVE (m2 fixed) |
| far | APPROVE | APPROVE |
| levels | APPROVE (m12) | APPROVE (m13; m3 and m4 fixed) |
| characters | APPROVE (Nit) | APPROVE (m5 fixed) |
| quickplay | APPROVE | APPROVE (m14) |
| challenges | APPROVE (m14, Nit) | APPROVE (m14; m6 fixed) |
| duel | APPROVE | APPROVE (M3 fixed) |
| options | APPROVE (Nit; m8 fixed) | APPROVE (m8 fixed) |
| hud | APPROVE | APPROVE (Ace chip at the floor) |
| pause | APPROVE | APPROVE (short line back) |
| tally | APPROVE | APPROVE |
| duelhud | APPROVE (m15) | APPROVE (m15; m7 fixed) |
| **Input rules** | **APPROVE** (m16, m17, Nit) | |
| **Overall** | **APPROVE** | |

## Round-1 findings, one by one
- **M1, the 640 hint overflow: resolved.**
  - At 640 it is one row at 60–580 × 436–474 (`Pause.cs:401`). Its words use 7 px caps at 13.1:1, and the 26-unit pills sit on the right.
  - Nothing collides: the version label is about 10 px to the left, and the plate stops 6 px under the Companions/Options row (`z-hint.png`, `z-hint2.png`).
  - At 1280 it is a journal panel at 100–640 × 680–768 (`Pause.cs:412`), clear of the pills and the Continue card.
- **M2, the tally leaking keys: resolved.**
  - `menu = flow.Current != Play || pause.Paused || (game is {} g && LevelOver(g))` (`Flow.cs:52`).
  - Over the tally, the arrows, Tab, Enter and Space are claimed while the window has the keys.
- **M3, the duel's level code: resolved.** "1-1" is TrumpGothic with a 10 px cap at 640 (`z-duel` crop, `duel-640.png` at 163,276).
- **m1, locked states: resolved, except Duel (m11).**
  - Measured at 640:
    - the Challenges pill, 5.9–6.3:1 (was 3.9);
    - the Far Shore tab, 4.9–5.9:1 (was 2.8);
    - the Novice and Master tabs, 6.3 and 6.4:1 (were 4.3 and 4.4);
    - "opens after 3-3", 7.7:1;
    - locked challenge names, 5.3–6.4:1.
  - The padlocks are in place (`Menu.cs:463–471` and `:651–654`).
  - The tooltip shows on `nav || hovered` (`Menu.cs:483`, `:661`).
- **m2, the 640 map: resolved.** There is a state line (`Map.cs:484`) and a Levels pill (`Map.cs:486–490`). A sealed stage widens Play to the full row and keeps its tooltip.
- **m3, tile captions: resolved at 640, and at 1280 in x only.**
  - The code and ACED are 6 units in.
  - Names fit one line, and at 640 Best clears the rule by about 4 px.
  - At 1280 Best still sits on the rule (m12, carried over from round 1; the old render shows the same thing).
  - Sealed names cut at 640 are new (m13).
- **m4, selection glows: resolved.** They are feathered and lighter, kept within 9–14 units, and draw no slab at 640.
- **m5, Characters: resolved.**
  - The short 640 captions stay within their pitch.
  - The clip is back at 640 and shows the guide line at 1280.
  - A Nit: the power text is wrapped to within 2 units of the clip at 1280.
- **m6, the challenge rows: resolved.** The rows clear both lower corner ornaments at 1280 (`z-ch-corner.png`) and are paged. The paging brings a new issue (m14).
- **m7, the duel turn: resolved.**
  - The lit plate has a 0.5 glow and a rim, and the waiting plate is dimmed.
  - "Louisoix is thinking ···" is a caption with 7 px caps at 6.7:1.
  - The waiting side's balls show on a chip.
  - The tube and the rail both show the shooter.
  - The dimming costs contrast (m15).
- **m8, Options: resolved.** The Decoration value is dimmed to 5.2–6.3:1, which still passes; the held note is shown; and the marks show even with Peg marks off.
- **m9, a drag-off hold resuming: resolved.** `outsidePress` is set only when the press begins outside the panel, on the board, with no item hovered (`Pause.cs:147–159`). While paused the board is a `Dummy`, so `IsAnyItemHovered` is false there and a plain outside click still resumes. There is a Nit about a stale flag.
- **m10, claims lapsing under a held key: resolved.** `navigationTaken` keeps the claim while any navigation key is down (`Flow.cs:55–64`). `Keyboard.NavigationKeyHeld` reads ImGui's key state, which clearing the game's key state does not touch, so the claim cannot cancel itself.
- **Nits from round 1:**
  - Hint pills: now 26 units at 640 and 32 at 1280. Good enough.
  - Quick Play opening on a stub: replaced by overlapping pages (m14).
  - Flat band on selected rows: unchanged and still a Nit.

## Findings
- **[Minor] m11. A locked Duel still gives no reason.**
  - `Title.cs:248–249` (1280) and `:304` (640) pass `MenuStyle.Locked` with no tooltip.
  - At 640 there is no sub-line, so a padlock is the only sign.
  - At 1280 the sub-line comes from `TitleOpponent()`. When opponents exist but `QuickLevels()` is empty, the pill is locked yet reads "against Minfilia". That is a first-run case.
  - Fix: pass a tooltip such as "Win a level in Adventure first: a duel is played on a level you have reached". Use a matching sub-line when the reason is no levels.
- **[Minor] m12. Level tile at 1280: the Best line sits on the caption's bottom rule.**
  - The glyphs' baseline touches the rule, and the comma of "214,300" crosses it (`z-best.png`; round 1's render is the same, `z-best-old.png`).
  - Cause: `yy = y + th + 42` with `capH = 56` (`Map.cs:860`, `:912`). The 640 plate grew by 4 units, but the 1280 one did not.
  - Fix: `capH` 60 at 1280, or lift `yy` to `th + 38`.
- **[Minor] m13. Level select at 640: sealed level names are cut and cannot be read in full anywhere.**
  - "Above the C…" and "Ironworks…" (`z-tile640b.png`).
  - The strip names only the selected level. A sealed tile cannot be selected (`if (hit && reached)`, `Map.cs:947`), and tiles have no tooltip.
  - Fix: show the full name in a tooltip on hover or focus when `FitLine` cut it. Or let a sealed tile be selected, so the strip shows its name with the existing "Not reached" pill.
- **[Minor] m14. The "full last page" makes paging repeat most rows.**
  - `first = Math.Min(page * rows, count - rows)` (`Modes.cs:234`, `:414`). The results:
    - Quick Play at 640 (13 levels, 11 rows): page 2 shows 1-3 to 3-3, so 9 of its 11 rows repeat page 1.
    - Challenges at 1280 (12 rows, 11 a page): 10 of page 2's 11 rows repeat.
    - Challenges at 640: 8 of 10 rows repeat.
  - The pager says "2 of 2", but turning the page looks like a scroll of one or two rows, and nothing shows which rows are new.
  - Fix, either of:
    - keep the full last page, and label the pager with the range shown ("3–13 of 13") in place of "2 of 2";
    - or go back to short last pages: the list already opens on the page holding the selection, so the round-1 stub is gone either way.
- **[Minor] m15. Duel HUD: the dimmed waiting plate drops its label below 4.5:1.**
  - `IdleDim` lays #03040C at 0.4 alpha over the whole plate, text included (`Modes.cs:969–973`).
  - "YOU" on the waiting plate measures 3.8–4.4:1 at 640 (`duelhud-640.png` at 116,39; `z-you.png`).
  - On the player's turn, the opponent's name in `LabelInk` (#D8C49A) works out to about 4.0–4.5:1 on paper, and less once antialiased.
  - The balls chip passes: label 4.4–4.9:1, digit 9.8:1.
  - Fix: dim only the pill's fill and face ring, and draw the label and score after the dim. Or lower the dim to 0.25.
- **[Minor] m16. A double click on Resume, or on the pause crest, acts twice.**
  - Resuming does not re-arm the board. `boardArmedAt` is set only in `StartBoard` (`Flow.cs:303`), and none of the `TryResume` paths sets it: Resume, Esc, a click outside the panel (`Pause.cs:157`) and the plain bar.
  - Resume sits over the board, so the second press of a double click activates `##moonfallBoard` while aiming, and its release shoots.
  - A double click on the crest first pauses. Then, because the crest is outside the panel, the second press counts as an outside press and resumes at once.
  - Play has a 0.3 s guard for exactly this, so the "a click that opens or resumes never shoots" rule is only half kept.
  - Fix: set `boardArmedAt = ImGui.GetTime() + BoardArmSeconds` on every resume. Ignore an outside press that comes sooner than `BoardArmSeconds` after the pause began.
- **[Minor] m17. No test covers the round-1 input fixes.**
  - `MoonfallScreensLintTests.cs:37–52` checks only that `ClaimNavigation()` appears. Nothing would catch losing:
    - the tally clause (`LevelOver(g)` in `menu`);
    - the held-key carry-over (`navigationTaken`);
    - the outside-press rule (`outsidePress`).
  - These rules regressed silently once already.
  - Fix: assert each in the existing lint test style.
- **[Nit]**
  - **A stale `outsidePress`.** It stays true if Esc resumes while the mouse is held after an outside press, because `DrawPauseMenu` no longer runs. The next pause's first release, such as the crest click that paused, then resumes at once. Clear it when not paused (`Pause.cs:147–159`).
  - **Padlock against Waiting.** Challenges whose "levels are on their way" carry a padlock (`Modes.cs:443`). The Far Shore stage in the same state uses the new Waiting style with no padlock. Pick one meaning for the padlock.
  - **Characters at 1280.** The power text wraps at 300 units (`Characters.cs:165`) and so ends 2 units from the clip at `x1 − 226` (`:437`); the render shows a gap of about 3 px (`z-charclip.png`). Wrap at 280.
  - **The held Decoration note.** "Reduce motion is on: everything holds still, whatever this says" overstates: Off still makes the board plain. Something like "Reduce motion is on: nothing moves at any setting; Off still draws the plain board."
  - **Selected list rows** still use a flat band rather than the kit's glow (carried over).

## Input rules (from the code)
They hold, except m16:
- **Mouse first.** Every entry is an `InvisibleButton`. The crest now shows the hand cursor and a tooltip.
- **Visible focus.**
  - `FocusOutline` is on `MenuButton`, `HoldButton`, `MenuSwitch`, `MenuStepper` (pager and chevrons), `MenuTab`, `MenuHit` rows and tiles, and the 640 stage head.
  - Inert pills (Locked and Waiting) can still take focus and show the ring. A Locked one also shows its tooltip on focus.
  - Default focus comes from `SetItemDefaultFocus` and `SetKeyboardFocusHere` on entry.
- **Esc goes back, or pauses in play.**
  - `Back()` follows `flow.Back(paused, over)`. Start toggles the pause.
  - `RespectCloseHotkey` is set only on the title.
- **Keys are claimed from the game.**
  - Esc on every screen but the title.
  - The navigation keys on menus, the pause and the tally (`Flow.cs:52`), and kept while held (`:55–64`).
- **A click that opens or resumes never shoots.**
  - The board is a `Dummy` while paused, over the tally and for 0.3 s after start (`Board.cs:170`).
  - The crest is taken before the board's button. Inert pills return false.
  - Gap: a double click on resume (m16).
- **Holds.**
  - Restart and Leave need a 0.9 s hold, with the mouse or the activate key (`HoldButton`, `IsItemActive`).
  - A hold dragged off the panel no longer resumes.

## Text floors (measured at 640)
**Labels (7 px cap floor): pass.**
- Hint words 7.
- "YOU" 7, "BALLS" 7, "LOUISOIX IS THINKING" 7.
- Locked challenge names 7, "Full" (dimmed) 7.
- The Waiting pill "Levels on their way" 9, Far Shore tab 9, Challenges pill 11.
- Round-1 labels unchanged: tile names 7, Best 7, ACED 8.

**Numbers (8 px floor): pass.**
- The duel level code is 10, up from 5 (M3).
- The Ace chip digits are 8.
- The duel balls-chip digit is 7–8. Both chips use `NumberPx(v, 13f, Trump)`, which keeps the cap at the 8 px floor, so the 7 is antialiasing at the floor.
- The waiting plate's score is 11.

**Contrast (secondary and locked text, 4.5:1 floor).**
| Text at 640 | Ratio |
|---|---|
| Challenges locked pill | 5.9–6.3:1 |
| Far Shore locked tab | 4.9–5.9:1 |
| Novice tab | 6.3–6.8:1 |
| Master tab | 6.4–7.1:1 |
| "opens after 3-3" | 7.7:1 |
| Locked challenge name | 5.3–6.4:1 |
| Waiting pill | 5.8–6.5:1 |
| Dimmed Decoration value | 5.2–6.3:1 |
| Hint words | 13.1:1 |
| Thinking caption | 6.7:1 |
| BALLS chip label | 4.4–4.9:1 (borderline) |
| **"YOU" on the dimmed plate** | **3.8–4.4:1 (fails, m15)** |

## Unverified
- **Behaviour in the game:**
  - hover, keyboard and gamepad focus;
  - whether ImGui keeps a hold active while Enter is held;
  - where ImGui places a tooltip when focus comes from the keyboard rather than the mouse;
  - the double-click timing in m16. I reasoned it from the code and did not reproduce it.
- **Gamepad input:** whether it reaches the game. `GameKeyClaim` clears keyboard keys only.
- **Motion:** the turn glow's breath, the dots and the hold's fill. I saw only stills, and only the reduce-motion still for the duel HUD.
- **Unrendered states:** the player's own turn in the duel HUD (the renderer catches the opponent's thought), so the opponent-name contrast in m15 is worked out from the colours, not measured. Also page 2 of Quick Play and the challenges, a sealed or not-met stage in the 640 panel, and a locked Duel on the title.
- **Fonts and sizes:** Dalamud's font metrics, and window sizes between 640 and 1280.
- **Esc on the frame it returns to the title:** whether Dalamud also acts on that press.
- **The tests:** I did not build or run them; the worktree was read-only for me.
