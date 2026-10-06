# Moonfall screens: UX/UI supervision, round 4

**What I reviewed.** I read my round-3 review, the implementer's round-3 response, the round-3 reviews from the level critic and the game designer, and the Far Shore section of `moonfall-modes.md`. I read the round-4 diff at HEAD e0db5d19:
- `ShieldText.RequestMenu`
- `MenuButton`'s Veiled press
- In `MoonfallWindow.Map.cs`: `PlayOrReveal`, `PanelPlayTip`, the `SmallStagePanel` head, `ShieldMark`'s small path, `StoryVeil`, `StopTooltip`, `Legend`, `MakeMapWords`, `FitLine` and `EndsOnSmallWord`
- The title's veiled Continue at both sizes, and the chart backdrop
- `DrawPlainBar`'s whole parts
- `AdventureNext` and the tally's veil note
- The challenge pill and its line
- The companions' joins line
- `MoonfallShieldTests`

I also read `Keyboard.cs`, `PlaceholderMenu.cs` and `MoonfallScreens.cs` to follow the keyboard path. Then I probed Dalamud's installed `Dalamud.Bindings.ImGui.dll` for its ImGui generation. It has `NavDisableHighlight` and `NoPopupHierarchy`, and lacks `NavCursorVisible` and `ImGuiButtonFlags_EnableNav`, so it is the 1.89–1.90 line.

I re-rendered with the Release renderer (bin built 04:10, commit 04:14) into my scratchpad (`…/scratchpad/r4/`), never the repo. All 26 runs used `--text-check`:
- `title` at both sizes: `--far-built --far-walk --story 3` (the road waits at stage 9), the same at `--story 0` (it waits at stage 3), and `--story 0` without a walk (the chart backdrop).
- `far` at both sizes: walked at `--story 3`, and with `--stage 12`; walked at `--story 0` with `--stage 3`; and `--far-won 30 --story 2` with `--stage 9` (veiled and not reached) and `--stage 7` (veiled and reached).
- `challenges` and `characters` at `--story 0`, walked.
- `duelhud --decoration off` at both sizes, and at 640 with `--seconds 8`.
- `--level base-04` at `--story 0` (both sizes) and `--story 5` (640): the scenes.
- `title` and `levels` at 640 with no story set.

**How I measured.** As in round 3:
- Cap heights come from row profiles.
- Contrast is the WCAG ratio between the ground's median and the text's 97th–99th percentile. Where the ground is a busy backdrop I also give the ratio against the text's own dark edge.
- Crops are point-zoomed `z-*.png` files, and contact sheets are `s-*.png`, in the same folder.

## Summary
Every round-3 finding is resolved: M4, m18–m23 and the Nits. **All 26 `--text-check` runs report 0 leaks**, with stories 0, 2, 3 and 5 and up to 8 stages veiled.

- **M4 is fixed, and the fix is good.**
  - When the road waits at a veiled stage, the 1280 card reads CONTINUE, then the shield's mark with "Endwalker area 1" (the placeholder, with its hover and reveal), then "The road waits at a stage set past your story…". Its button is "Adventure map".
  - The Adventure sub-line reads "The Far Shore · stage 9 of 12".
  - The 640 title says "The road waits past your story: Endwalker area 1" under an "Adventure map" pill.
  - The map opens on stage 9.
- **m18: the keyboard and gamepad reveal works.**
  - Pressing the Veiled pill calls `ShieldText.RequestMenu`. The window's root `DrawMenu` then opens the shield's own popup in the same frame.
  - With nav active, ImGui places the popup at the focused pill and moves its focus to "Reveal this name · this session", so it can be navigated.
  - The reveal still takes a second, deliberate choice.
- **m22, the 640 shield mark,** now reads as an eye struck through.
- **m23, the plain bar,** never cuts a word.

Three new Minors, none of them Major:
- **m24.** Esc or the gamepad's B, used to dismiss the reveal menu, also goes back a screen. ImGui closes the popup during `NewFrame`, before `HandleKeys` checks `IsPopupOpen`. This is now on the main keyboard path; it was already true of the right-click menu in round 3, and I missed it then.
- **m25.** The tally's "Map opens on it" holds for the Map button but not for Esc or B.
- **m26.** At 1280, the stop tooltip's two veiled reason lines are three times longer than its plate. They fall to the 12 px floor and run past the plate's edge onto the chart. This also predates the round: the round-3 line was as long.

**Overall: APPROVE** (no Major; m24–m26 and Nits to fold in).

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title | APPROVE (M4 fixed) | APPROVE (M4 fixed) |
| title-road-waits (new) | APPROVE (Nit: a one-word last line) | APPROVE (Nit: no mark) |
| title-arr (new, chart backdrop) | APPROVE | APPROVE |
| title-hint | APPROVE | APPROVE |
| map | APPROVE | APPROVE |
| far | APPROVE | APPROVE |
| far-veiled / far-stepover (new) | APPROVE (m20 fixed; m26 in the code) | APPROVE (m19 and m22 fixed) |
| levels | APPROVE | APPROVE (Nit fixed: "Above…") |
| characters | APPROVE (m20 fixed in the code) | APPROVE |
| quickplay | APPROVE | APPROVE |
| challenges | APPROVE (m21 fixed in the code; Nit on wording) | APPROVE (m21 fixed in the code) |
| duel | APPROVE (Nit fixed) | APPROVE |
| options, hud, pause | APPROVE | APPROVE |
| tally | APPROVE (m25 in the code) | APPROVE (m25 in the code) |
| duelhud / duelhud-plain | APPROVE | APPROVE (m23 fixed) |
| scene-veiled / scene-shown | APPROVE | APPROVE (newly rendered) |
| **Input rules** | **APPROVE** (m18 fixed; m24, m25) | |
| **Spoiler shield** | **APPROVE** (M4 and m18–m22 fixed; m26) | |
| **Overall** | **APPROVE** | |

## Round-3 findings, one by one
- **M4, "more of the road is on its way" at a veiled frontier: resolved.**
  - `modes.Next()` reports a veiled frontier (`Title.cs:102`).
  - The title builds the veiled card from it (`Title.cs:136–148`):
    - the placeholder in `Ink2`;
    - `ShieldPlaceholder` over it (`Title.cs:411–430`) and over the 640 line (`Title.cs:327`);
    - the mark;
    - the wrapped reason.
  - `Continue()` opens the map on that stage (`Title.cs:447`, `OpenMapAt` at `Map.cs:222`).
  - Rendered (`title-rw-1280.png`, `title-rw-640.png`, `title-rw0-*.png`, `z-title1280-card.png`):
    - the sub-line is "The Far Shore · stage 9 of 12" (stage 3 of 12 at A Realm Reborn);
    - `far-rw-*` opens on stage 9.
  - Tests: `Continue_points_at_the_next_playable_stage_past_a_veiled_one_…` and `A_veiled_stage_at_the_end_of_the_road_is_what_continue_reports_not_road_goes_on`.
- **m18, the reveal was mouse-only: resolved.**
  - `MenuButton` now returns true for a Veiled press (`Menu.cs:500`), and `PlayOrReveal` calls `ShieldText.RequestMenu(SpoilerKind.Area, zone, panelName)` (`Map.cs:587–596`, `ShieldText.cs:126`). This holds at both sizes.
  - The request goes through `PlaceholderMenu.Request`, so the target and the "this session" wording stay `ShieldText`'s own.
  - Opening:
    - `DrawMenu` runs after the screens (`MoonfallWindow.cs:268`) and calls `OpenPopup` in the same frame (`ShieldText.cs:172`).
    - With nav active, ImGui's `NavCalcPreferredRefPos` anchors the popup at the focused pill's lower-left, not at the mouse.
  - Navigating:
    - The popup takes focus with a nav init, so "Reveal this name" is highlighted.
    - Arrows and Enter (or the D-pad and A) work.
    - Because the bindings expose `NoPopupHierarchy`, `WindowHasKeys()` stays true while the popup has focus. The arrows are claimed from the game, which closes my round-3 unverified item.
  - After a reveal, `FollowShield` rebuilds the panel. The pill keeps its id and becomes "Choose a level", so a second press plays.
  - The pill's tip reads "Set past your story. Press to reveal its place for this session." (`Map.cs:581`).
  - Gap: dismissing the menu with Esc or B (m24).
- **m19, the merged tooltip at 640: resolved.**
  - The head's fill and "See the stage's five levels" show only for Open or Done stages (`Map.cs:615–624`).
  - On a veiled name the only hover is the shield's own.
- **m20, two placeholders that did not answer: resolved.**
  - A veiled stop's tip drops the name: "Stage 9 · past your story · Fireball", in `Ink2` (`Map.cs:793–799`, `:751`).
  - The companions' line reads "Joins on The Far Shore, stage 11 (past your story)" (`Characters.cs:179`).
  - Unrendered.
- **m21, the challenge line pointed at a missing name: resolved.**
  - The line is its own: "Runs through stage N, set past your story. Press Play to open it on the map, where its place can be revealed." (`Modes.cs:599`).
  - It is also the pill's tooltip (`Modes.cs:521`), and pressing the pill opens the map on that stage (`Modes.cs:524–528`).
  - Nit below on "Press Play". Unrendered: no veiled challenge appears on page 1, even at `--story 0` walked.
- **m22, the 640 mark did not read as an eye: resolved.**
  - Below 7 px it draws the almond's two arcs and the slash at 1.5 px, with no pupil and no dark cut (`Map.cs:113–122`).
  - On the map it is at least 7 units (`Map.cs:441`).
  - At 640 the almond is about 10×7 px with an 11×10 px slash. It reads as an eye struck through and is plainly unlike the gold padlock beside it (`z-marks640s.png`, stops 9 and 10, which carry both).
  - It measures **10.9:1** against its disc.
- **m23, the plain bar cut mid-word: resolved.**
  - Whole parts are drawn in priority order until one does not fit, with no clip (`MoonfallWindow.cs:579–602`).
  - At 640: "LOUISOIX IS THINKING · Balls 5 · Oranges 19 · ×1 · 3-3", and "YOUR SHOT · Balls 4 · Oranges 17 · ×1 · 3-3". The level name drops first (`z-plain640.png`, `duelhud-plain-640-s8.png`).
  - At 1280 everything fits, the name included (`z-plain1280.png`).
- **Nits from round 3: all resolved.**
  - The six "Past your story" are gone: the 1280 panel shows the five codes on one row and prints the reason (`Map.cs:521–533`).
  - The codes measure **6.9:1** (the veiled rows were 4.3–5.6).
  - `FitLine` never ends on a small word: "Above…" (`levels-640.png`, `Map.cs:966–991`).
  - Duel's second reason is a sentence (`Title.cs:372`).
  - The 640 duel plate's "LOUI…" was noted only, and stays so.
  - The flat selected-row band is carried over.

## The keyboard and gamepad reveal path, end to end
1. Map, veiled stage. Default focus is on the "Past your story" pill (`isDefault`), and its tooltip shows on focus.
2. Enter, Space or A opens the shield's popup at the pill, with "Reveal this name · this session" focused.
3. Enter or A reveals for the session. The panel rebuilds, and the pill becomes the stage's Play.
4. Cancel: Esc or B closes the popup, then also goes back to the title (m24).

From the title, the veiled card's "Adventure map" (the default) opens the map on the waiting stage, so the path from the first screen is two presses to the menu and one more to reveal. On the challenges screen, the veiled pill opens the map on the challenge's stage, and the reveal is then one more press.

## Findings
- **[Minor] m24. Esc or the gamepad's B closes the reveal menu and also goes back a screen.**
  - In Dalamud's ImGui (1.89–1.90 generation), `NavUpdateCancelRequest` runs in `NewFrame`. On Escape, or `NavGamepadCancel` (B), it closes the top popup and claims no key ownership.
  - Moonfall's `Draw` then runs `HandleKeys` (`MoonfallWindow.cs:240`). There:
    - `IsPopupOpen` is already false (`Flow.cs:66`);
    - focus is back on the window;
    - `Keyboard.BackPressed()` still sees Escape or FaceRight pressed this frame (`Keyboard.cs:81–82`).
  - So `Back()` runs, and the map returns to the title.
  - The right-click menu has done the same since round 3; my round-3 note that "Esc closes the popup first" was wrong. The press path makes this the common case for keyboard and gamepad players.
  - Reasoned from the ImGui source of that generation and the call order; not seen in the game.
  - Fix:
    - Remember whether a popup was open at the end of the last frame; for example, set `menuWasOpen` after `ShieldText.DrawMenu`.
    - In `HandleKeys`, return before Back and Start when it was.
    - Add a lint beside the existing input lints.
- **[Minor] m25. The tally says "Stage N is set past your story: Map opens on it", but Esc or B does not.**
  - Only `LeaveBoard` selects the waiting stage (`Flow.cs:361–366`), and only the tally's Map button calls it.
  - Back over the tally goes `MoonfallBack.Leave` → `EndBoard()` (`Flow.cs:102–105`). The map then opens on the stage just played (`mapStage` is kept, so `EnsureMapSelection` does not re-pick).
  - Fix: set the selection from `tallyVeil` in `EndBoard`, or call `LeaveBoard` from the Back case when the level is over. Unrendered.
- **[Minor] m26. At 1280 the stop tooltip's veiled reason overflows its plate.**
  - The plate is 300 units wide with 276 for text (`Map.cs:744`). The second line is a single `MenuText` with `maxWidth` (`Map.cs:752`), which shrinks only to the label floor: 7 / 0.583 = 12 px for Axis.
  - The lines are long:
    - `MoonfallStageVeiledLine` is 101 characters;
    - `MoonfallStageVeiledSealedLine` is 130;
    - every other stop line is 35–36.
  - I measured AXIS at 4.73 px a character at 14.5 units (the panel's wrapped line, `far-rw-1280.png`). At the floor the two veiled lines come to about 395 and 513 px, so they run about 120 and 225 px past the plate onto the chart, in `Ink2` with no edge.
  - The tooltip also shows on focus, so keyboard players see it.
  - Round 3's line was 110 characters, so this predates the round; I missed it because the renderer never hovers.
  - Fix, either of:
    - wrap the stop lines with `Wrap(…, W − 24)` in `MakeMapWords` and grow `h` with the line count;
    - give the stop tooltip its own short reason, such as "Past your story: select it to reveal its place" / "…and not reached yet", and leave the full sentence to the panel.
- **[Nit]**
  - **Wording of the remedy.**
    - The 1280 panel says "press its button", but the button reads "Past your story".
    - The not-reached line and the 640 state line ("Right-click its name to reveal it", `Map.cs:842`) mention only the right-click, though the pill reveals there too.
    - The challenge line says "Press Play", but its pill reads "Past your story".
    - Suggest "…or press Past your story" on the map, and "Press it to open the stage on the map" on challenges.
  - **The Veiled pill looks inert but answers.** It keeps the slate fill and gets no hover glow (`Menu.cs:450–452`). Give a Veiled entry the hover glow, still slate, so the mouse sees that it does something.
  - **The 640 head on a veiled or sealed stage** still takes focus and shows the ring, but does nothing (`Map.cs:607–627`). Either skip it in nav when `!headOpens`, or let it call `PlayOrReveal` on a veiled stage.
  - **The 1280 road-waits card** leaves "session." alone on a third line (`title-rw-1280.png`). Widen the wrap (`Title.cs:418`, `x1 − x0 − 60`), or drop "for this session" there: the menu itself says it.
  - **The 640 road-waits line has no shield mark**, where 1280 does. Optional: a 7-unit mark before the line.
  - **"You are here" in the legend** marks nothing when the road waits at a veiled stop, which is unlit by design. That is acceptable, since the panel opens on it. Optionally, give the veiled "here" stop a faint ring.
  - **Placeholders are now in Axis 22 and 15**, where the names they stand for are Jupiter 32 and 21. That departs from `ShieldText`'s "same size and weight", but it is justified by Jupiter's "I" for "1", and the slot holds.
  - **`challengeVeil?.Number ?? 0`** (`Modes.cs:599`) would print "stage 0" if `TryPlace` failed. Fall back to `MoonfallChallengeVeiled`.
  - **Doc comment.** `FitLine`'s `<summary>` now sits on `EndsOnSmallWord`, which has two summaries, and `FitLine` has none (`Map.cs:964–981`).
  - **Tests.** `MoonfallScreensLintTests` gained nothing this round. Add lints for:
    - a Veiled press reaching `RequestMenu`;
    - the head's `headOpens` gate;
    - the plain bar's whole parts;
    - m24's guard, once added.
  - **Carried over:** selected list rows still use a flat band.

## Input rules
- **Mouse first: holds.**
  - Every entry is still an `InvisibleButton`.
  - Placeholders keep hover and right-click.
  - A left click on the Veiled pill opens the same menu, at the mouse.
- **Visible focus: holds.**
  - The Veiled pill is the default and shows its ring and its "Press to reveal" tip on focus.
  - The 640 head shows a ring on a veiled stage but does nothing there (Nit).
- **Esc goes back, or pauses in play: holds, except while a popup is open (m24).** With the reveal menu open, Esc closes it and also goes back.
- **Keys are claimed from the game: holds.** `NoPopupHierarchy` exists, so `IsWindowFocused(RootAndChildWindows)` counts the popup, and the arrows used in it are claimed.
- **A click that opens or resumes never shoots: holds, unchanged.**
- **Safety on spoiling actions: holds.** A press opens a menu, never the reveal; the reveal is a second choice and lasts only the session.
- **Holds (press-and-hold buttons): unchanged.**

## Text floors (measured at 640)
**Labels (7 px cap floor): pass.**
| Text | Cap |
|---|---|
| Panel placeholder "Endwalker area 1" (Axis 15) | 8 |
| State line "Right-click its name to reveal it" | 7 |
| "PAST YOUR STORY" pill | 9 |
| "STAGE 9" | 7 |
| Title line "The road waits past your story: …" | 7 (at the floor) |
| Plain bar parts | unchanged from round 3 |

**Numbers (8 px floor): pass.** The stop numbers and plain-bar scores are unchanged. The codes "9-1…9-5" in the 1280 panel have a cap of 12.

**Contrast (secondary, locked and placeholder text, 4.5:1 floor)**
| Text | Ratio |
|---|---|
| Placeholder, 640 panel (Axis, `Ink2`) | 5.9–6.7:1 |
| State line, 640 panel | 5.7–6.8:1 |
| Veiled pill label, 640 | 6.2–6.8:1 |
| "STAGE 9", 640 | 5.1–5.5:1 |
| Shield mark on a 640 stop (vs its disc) | 10.9:1 (was 8.9–9.1, and now reads as an eye) |
| Placeholder, 1280 panel | 6.0–6.9:1 |
| Reason lines, 1280 panel | 6.6–7.2:1 |
| Codes row (`Ink3`), 1280 panel | 6.9:1 (the old veiled rows were 4.3–5.6) |
| Veiled pill label, 1280 | 6.0–6.8:1 |
| Title card placeholder, 1280 | 5.9–6.8:1 |
| Title card reason, 1280 | 6.0–7.2:1 |
| "CONTINUE" caption, 1280 | 5.4–5.7:1 |
| Title line, 640, over Sohm Al | 4.4–5.6:1 against the backdrop median; 7.2–9.3:1 against its own edge |
| Title line, 640, over the chart | 5.7–7.2:1; 7.9–10.0:1 against its edge |

## Unverified
- **The reveal menu in the game.** The renderer keeps the mouse off-screen and sends no keys, so I did not see:
  - the popup;
  - its placement at the pill;
  - its nav highlight;
  - the round trip after a reveal.

  These are reasoned from `ShieldText`, `PlaceholderMenu` and ImGui's `NavCalcPreferredRefPos` and popup nav init, for the generation the bindings match.
- **m24** is reasoned from ImGui's `NewFrame` order and `Keyboard.BackPressed`, not observed. It assumes Dalamud sets `NavEnableKeyboard`, which Moonfall's keyboard focus already relies on. If Dalamud did not set it, Esc would not close the popup at all, and `HandleKeys` would ignore Esc while the popup is open.
- **m26** is computed from a measured glyph advance and the label floor, not rendered: the stop tooltip needs a hover or focus.
- **Unrendered states:**
  - the tally's veil note and its Map (m25);
  - a veiled challenge (m21);
  - the companions' veiled joins line (m20);
  - the 1280 stop tooltip;
  - the 640 head's focus on a veiled stage.
- **The renderer binary.** I used the existing Release build (04:10, four minutes before the commit) and did not rebuild, because the worktree is read-only for me.
- **Gamepad input reaching the game** (`GameKeyClaim` clears keyboard keys only), as in rounds 2 and 3.
- **The tests and gates.** I did not build or run them.
- **Fonts and window sizes:** Dalamud's font metrics, and sizes between 640 and 1280.

