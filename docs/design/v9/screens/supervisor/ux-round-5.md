# Moonfall screens: UX/UI supervision, round 5

**What I reviewed.** Before the diff I read my round-4 review, the implementer's round-4 response, and the round-4 reviews from the game designer and the level critic. I then read the round-5 diff at HEAD 8c51d4d8:
- `HandleKeys`, `popupWasOpen`, `EndBoard` and `LeaveBoard` (`MoonfallWindow.Flow.cs`)
- the end of `DrawWindow`, and `DrawPlainBar` (`MoonfallWindow.cs`)
- in `MoonfallWindow.Map.cs`: `panelRevealable`, `PanelPlayStyle`, `PanelPlayTip`, `PlayOrReveal`, `SmallStagePanel`'s head, the veiled "here" ring, `StopTooltip`, and the stop and panel words in `MakeMapWords`
- `MenuButton`'s Veiled hover glow (`MoonfallWindow.Menu.cs`)
- the tally note on a win, rich (`Tally.cs`) and plain (`Board.cs`), and the tally's button focus
- the title's eyebrow and its wider wrap (`Title.cs`)
- the challenge fallback (`Modes.cs`)
- the new strings, and the three new lints in `MoonfallScreensLintTests.cs`

To check the close-key path I decompiled the installed Dalamud's `WindowHost` and `WindowSystem` with `ilspycmd`, into my scratchpad.

**Renders.** I re-rendered with the Release renderer into `…/scratchpad/r5/`, never into the repo. The renderer's `Tsukimichi.dll` was built at 04:56, after every changed source file; the newest is `Board.cs` at 04:50. All 22 runs used `--text-check`:
- `tally`, `--far-built --far-walk --leave-one`, at `--story 3` (rich and `--decoration off`), `--story 0`, and `--story 5` (the "last level built" note)
- `title --far-built --far-walk --story 3`
- `title --far-won 0 --story 0`
- `far --far-built --far-walk --story 3` (the road waits at a veiled stage 9)
- `far --far-built --far-won 30 --story 2`, with `--stage 9` (veiled and not reached) and `--stage 7` (veiled and reached)
- `challenges` and `characters`, `--far-built --far-walk --story 0`

Each ran at 1280×800 and 640×480.

**Measuring.** I measured as in round 4:
- cap heights from row profiles;
- WCAG ratio between the ground's median and the text's 97th–99th percentile;
- point-zoomed crops `z-*.png` and contact sheets `s-*.png`, in the same folder.

## Summary
**m24, m25 and m26 are resolved.** All 22 `--text-check` runs report **0 leaks**, with 0 to 8 stages veiled. The Nits are resolved too, except the inert 640 head on a sealed stage, which is only partly resolved.

- **m24.** `HandleKeys` returns before Back and Start when a popup was open at the end of the last frame (`Flow.cs:68`, `MoonfallWindow.cs:275`). A lint holds the order.
- **m25.** `EndBoard` now selects the waiting stage (`Flow.cs:366–374`), so the tally's Map, Esc and B all open the map on it.
- **m26.** The stop tooltip has short reasons of its own. At 14 units they come to about 216 and 169 px, inside the 276 px text width.

**Three new Minors, none of them Major:**
- **m27 (input).** On the **title**, Esc that dismisses the shield's reveal menu also **closes the Moonfall window**. This is m24's class of bug through a path the fix does not cover: Dalamud's close hotkey, not `HandleKeys`. I confirmed it in the decompiled `WindowHost`.
- **m28 (regression, 640).** On a veiled stage that is **not reached**, the 640 panel says "Press Reveal or right-click its name". There is no Reveal button, the pill reads "Not reached", and a reveal would not open the stage. You can see this in the committed `far-veiled-not-reached-640.jpg`.
- **m29 (flow).** When the road waits, the tally's note says "Map opens on it", but Enter or A presses **Replay**, which is the focused button.

**Overall: APPROVE** (no Major remains; m27–m29 and the Nits are to fold in).

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title | APPROVE | APPROVE |
| title-road-waits | APPROVE (Nit fixed: no orphan word; the eyebrow is "CONTINUE · STAGE 9") | APPROVE (Nit: no mark, optional, carried over) |
| title-arr / title-hint | APPROVE | APPROVE |
| title, unbuilt Far Shore (own render) | APPROVE (Nit: the sub-line reads "stage 1 of 12"; the game designer's call) | APPROVE |
| map | APPROVE | APPROVE |
| far | APPROVE | APPROVE |
| far-veiled / far-stepover (reached, "Reveal its place") | APPROVE (m26 fixed; Nit on the wording) | APPROVE (Nit fixed: the head reveals) |
| far-veiled-not-reached (new) | APPROVE | **APPROVE with m28** (the line points to a missing Reveal) |
| levels | APPROVE | APPROVE |
| characters | APPROVE | APPROVE |
| quickplay | APPROVE | APPROVE |
| challenges | APPROVE (Nit fixed: the copy names the pill; the fallback has no "stage 0") | APPROVE |
| duel, options, hud, pause | APPROVE | APPROVE |
| tally | APPROVE (m25 fixed) | APPROVE (m25 fixed) |
| tally-road-waits (new) | APPROVE with m29 | APPROVE with m29 |
| tally-road-waits-plain (new) | APPROVE (Nit: the note is not wrapped) | APPROVE |
| duelhud / duelhud-plain | APPROVE | APPROVE |
| scene-veiled / scene-shown | APPROVE | APPROVE |
| **Input rules** | **APPROVE** (m24 fixed; m27 and m29 new) | |
| **Spoiler shield** | **APPROVE** (m26 fixed; m28) | |
| **Overall** | **APPROVE** | |

## Round-4 findings, one by one
- **m24, Esc or B closing a popup also went Back: resolved, on every screen but the title (see m27).**
  - `popupWasOpen` is set after `ShieldText.DrawMenu`, at the end of `DrawWindow` (`MoonfallWindow.cs:271–275`).
  - `HandleKeys` checks it, or a popup open now, before Start and Back (`Flow.cs:66–71`).
  - `ClaimBack` still runs first (`Flow.cs:46–49`), so the game never sees the Esc either.
  - `Keyboard.BackPressed` uses `IsKeyPressed(…, false)` with no repeat (`Keyboard.cs:81–82`), so an Esc held after the popup closes does not fire Back a frame later.
  - Side effects:
    - A popup closed by clicking outside it, or by choosing an item, holds the keys for at most one frame.
    - A popup closed by choosing an item closes within its own frame, so it holds them for none.
  - Lint: `The_key_that_closes_a_popup_is_not_also_back` (`MoonfallScreensLintTests.cs:211`). It also asserts that the flag is set after `DrawMenu`.
- **m25, Back over the tally ignored the waiting stage: resolved.**
  - The selection moved into `EndBoard` (`Flow.cs:366–374`), behind `Adventure && LevelOver && tallyVeil`.
  - `MoonfallBack.Leave` → `EndBoard` (`Flow.cs:104–105`) now opens the map on the waiting stage, as the Map button does. `flow.Leave()` has already set the Map screen, and `EnteredScreen` runs after `HandleKeys`, so the new `mapStage` holds.
  - Stale state: `tallyVeil` cannot come from an earlier game. `PrepareTally` resets it per game (`Tally.cs:77`), and the tally is drawn in the frame the level ends (`MoonfallWindow.cs:306–319` advances, then `DrawBoard` draws the end), before any later `HandleKeys`.
  - The pause menu's Leave goes through `LevelOver == false` and is unaffected.
  - Not linted (Nit).
- **m26, the stop tooltip overflowed: resolved.**
  - `stopTipLines` uses `MoonfallStopVeiledLine`, "Past your story: select it to reveal its place" (46 characters), and `MoonfallStopVeiledSealedLine`, "Past your story, and not reached yet" (36) (`Map.cs:816`).
  - I measured Axis at 4.82–4.92 px a character at 14.5 units (the panel's lines in `farrw-1280.png`: 251 px for 51 characters, 265 px for 55). At the tooltip's 14 units that is about 216 and 169 px, against the 276 px text width (`Map.cs:760–768`).
  - Computed, not rendered: the renderer never hovers.
- **Nits: all resolved, except the inert 640 head on a sealed stage, which is partial.**
  - **Remedy wording.**
    - The map pill reads "Reveal its place", and the 1280 line names it.
    - The challenge line names "Past your story", the label its pill carries.
    - New wording issue: the not-reached case (m28).
  - **The Veiled pill's hover glow.** Done: `EntryGlow(…, hovered && (!inert || style == MenuStyle.Veiled))` (`Menu.cs:452`).
  - **The 640 head.** It reveals on a reached veiled stage (`Map.cs:616–627`). It is still a focus stop with an outline, and does nothing, on a sealed or not-reached stage (`Map.cs:641–644`). Partial; Nit below.
  - **The road-waits card.** It wraps at `x1 − x0 − 36` (`Title.cs:420`): two lines, no orphan (`titlerw-1280.png`).
  - **`challengeVeil`.** It falls back to "Runs past your story." (`Modes.cs:599`). The pill's press is still guarded by `challengeVeil is { }`, and `PlaySelectedChallenge` ignores a Veiled entry (`Modes.cs:521–528`, `:538`).
  - **The `FitLine` doc comment** is fixed (`Map.cs:998`).
  - **Lints.** Three were added (`MoonfallScreensLintTests.cs:211`, `:223`, `:239`).
  - **The faint "here" ring.** Added (`Map.cs:441–445`). It is visible at both sizes (`z-here1280.png`, `z-here640.png`), but it is low-contrast (Nit).
  - **Not changed, accepted:** the 640 road-waits mark, the Axis placeholder size, and the flat selected-row band.

## Findings
- **[Minor] m27. On the title, Esc that dismisses the shield's menu also closes the Moonfall window.**
  - The title's veiled Continue card is a placeholder with the shield's right-click menu, at both sizes (`Title.cs:329`, `:427`). On the title, `RespectCloseHotkey` is true (`MoonfallWindow.cs:245`).
  - Dalamud's `WindowHost` (decompiled, `scratchpad/r5/WindowHost.cs:546–559`) checks this after the window's `Draw` (`:372`):
    - `KeyState[ESCAPE]` is down;
    - the window is focused (`RootAndChildWindows`);
    - its static `wasEscPressedLastFrame` is not set;
    - and `RespectCloseHotkey` is true.

    When all four hold, it sets `IsOpen = false`.
  - So whether ImGui has already closed the popup in `NewFrame` or not, the Esc that dismisses the menu closes the window. With popup hierarchy, the popup counts toward the window's focus anyway.
  - `popupWasOpen` gates only `HandleKeys`, so it does not help here.
  - This also predates round 5: the card's placeholder arrived in round 4.
  - Only keyboard Esc is affected. Dalamud's B-to-close needs `NavId == 0`.
  - **Fix:**
    - Latch it: `escAfterPopup = (escAfterPopup || popupWasOpen || ImGui.IsPopupOpen(…Any…)) && ImGui.IsKeyDown(ImGuiKey.Escape);`, then `RespectCloseHotkey = flow.Current == MoonfallScreen.Title && !escAfterPopup;`.
    - The latch has to hold until Esc is released. Dalamud reads the key as held, and sets `wasEscPressedLastFrame` only when it closes something, so it would close the window on the next frame of the same press.
    - Extend `The_key_that_closes_a_popup_is_not_also_back` to cover `RespectCloseHotkey`.
- **[Minor] m28. At 640, a veiled stage that is not reached says "Press Reveal or right-click its name" over a "Not reached" pill.**
  - At 640, `panelState` uses `MoonfallStageVeiledShort` for every veiled stage (`Map.cs:860`). Meanwhile `panelRevealable` (`:854`) makes the pill "Not reached" with a padlock, and makes the head inert.
  - So the line names a button that is not on screen. It also offers the very reveal that the game designer's m10 withdrew, because a reveal will not open the stage yet. The 1280 panel gets this right ("…Revealing its place will not open it until then.").
  - Seen in the committed `far-veiled-not-reached-640.jpg` and in my `farnr-640.png` (crops `z-nr640-committed.png`, `z-nr640.png`).
  - **Fix:** at 640, when `!sel.Reached`, use the existing `MoonfallStopVeiledSealedLine`, "Past your story, and not reached yet". Its 36 characters fit where the current 36 do. Optionally, lint that `MoonfallStageVeiledShort` is used only when `panelRevealable`.
- **[Minor] m29. The road-waits tally points to Map, but Enter or A presses Replay.**
  - On a win with no Next, `PillButton` focuses Replay (`Tally.cs:304`, `focus: !nextFocus && won`). Map is focused only when there is no Again (`:316`).
  - When `tallyVeil` is set, the note says "Stage 9 is set past your story: Map opens on it." The default press, though, replays a level already won. A keyboard or gamepad player then has to pause and hold Leave to get out.
  - Seen in `tally-road-waits-1280.jpg` and `z-tallybtn1280.png`: Replay carries the focus halo.
  - **Fix:** focus the home button when `tallyVeil is not null`, for example `focus: !nextFocus && (!won || tallyAgain is null || tallyVeil is not null)`, and the inverse on Replay. Keep Replay focused for the "last level built" note.
- **[Nit]**
  - **The 1280 panel line repeats itself:** "Right-click its name, or press Reveal its place, to reveal the place for this session." Suggest "Right-click its name or press Reveal its place. It shows for this session only."
  - **The stop tooltip says "past your story" twice:** "Stage 9 · past your story · Fireball", then "Past your story: select it to reveal its place". The second line could drop its lead: "Select it to reveal its place".
  - **The 640 head on a sealed or not-reached stage** is still a nav stop that draws `FocusOutline` and does nothing (`Map.cs:641–644`). Skip it in nav when `!headOpens && !panelRevealable`.
    - On a revealable stage, the head reveals on a click but shows no hover fill.
    - Optionally, give it the slate fill when `panelRevealable`, so the mouse sees that it answers.
  - **The veiled "here" ring is faint.** About 2.4:1 against the chart: ring `#888CA1` against ground `#3C4F7B`, sampled at 1280 (`farrw-1280.png`, row 645). That is under the 3:1 non-text guideline. The legend's "you are here" swatch is the orange glow, which a veiled stop never shows.
    - The panel opening on the stage carries the meaning, so this is acceptable.
    - Raise the ring's alpha to 1.0 and its width, or add a legend note.
  - **The plain tally's note** is drawn with an unwrapped `AddText` (`Board.cs:625–628`). In English it fits at both sizes (`tally-road-waits-plain-*`). A longer locale would run past the 420-unit panel, so wrap it at `panel.X − 2·pad`.
  - **Lint for m25.** Assert that `EndBoard` reads `tallyVeil`, so the Back path cannot drift from the Map button again.
  - **The unbuilt title** (`titleunbuilt-1280.png`). "The road goes on" sits beside the Adventure sub-line "The Far Shore · stage 1 of 12", which reads as playable. This is for the game designer to weigh.
  - **Carried over:** the 640 road-waits mark (optional), and the flat selected-row band.

## Input rules
- **Mouse first: holds.**
  - Veiled pills now glow on hover.
  - A left click on the 640 head of a reached veiled stage opens the reveal menu at the mouse.
  - Placeholders keep their rect-based hover and right-click (`ShieldText.Interact`), so no overlapping item blocks them.
- **Visible focus: holds, with the Nit.** The 640 head on a sealed stage is an outlined focus stop with no action.
- **Esc goes back, or pauses in play: holds off the title (m24 fixed). On the title, Esc dismissing a menu also closes the window (m27).**
- **Default action: m29.** The road-waits tally's default is Replay, where its note points to Map.
- **Keys are claimed from the game: holds, unchanged.** `ClaimBack` runs before the popup gate.
- **A click that opens or resumes never shoots: holds, unchanged.**
- **Safety on spoiling actions: holds.**
  - A press opens the shield's menu, never the reveal itself.
  - On a stage that is not reached, the pill is "Not reached" and offers no reveal. Only the 640 line still suggests one (m28).
- **Holds (press-and-hold buttons): unchanged.**

## Text floors (measured at 640)
**Labels (7 px cap floor): pass.**
| Text | Cap (px) |
|---|---|
| Tally note "Stage 9 is set past your story…" (rich, Axis) | 7 (at the floor; rows 156–162, `tallyrw-640.png`) |
| Panel state line "Press Reveal or right-click its name" | 7 (rows 144–150) |
| Pill "REVEAL ITS PLACE" | 9 (rows 169–177) |
| Pill "NOT REACHED" | 9 (rows 169–177) |
| Plain tally note (ImGui font) | ≥9 |
| Title line, placeholder, "STAGE 9" | unchanged from round 4 (7, 8 and 7) |

**Numbers (8 px floor): pass, unchanged.** No new numerals at 640 except the note's "9", which shares the 7 px line. As in round 4, the note is a sentence, not a figure.

**Stop tooltip (1280, computed).** The reason lines are about 216 px and 169 px within 276 px, at 14 units, with no floor shrink.

**Contrast (secondary, locked and placeholder text, 4.5:1 floor; `s-contrast.png`)**
| Text | Ratio |
|---|---|
| Tally note, rich, 640 | 6.0–6.7:1 |
| Tally note, rich, 1280 | 5.7–6.5:1 |
| 640 state line ("Press Reveal…", reached and not reached) | 5.7–6.8:1 |
| "REVEAL ITS PLACE" pill, 640 | 6.4–6.8:1 |
| "NOT REACHED" pill, 640 | 6.4–6.8:1 |
| "REVEAL ITS PLACE" pill, 1280 | 5.8–6.6:1 |
| "NOT REACHED" pill, 1280 | 6.0–6.5:1 |
| 1280 panel reason lines (Ink2) | 6.2–7.2:1 |
| Title eyebrow "CONTINUE · STAGE 9" (GoldInk), 1280 | 5.4–5.9:1 |
| Veiled "here" ring against the chart (non-text) | ≈2.4:1 (Nit) |

## Unverified
- **m27 in the game.** It is reasoned from the decompiled `WindowHost` of the installed dev Dalamud (`Hooks/dev`), and from Moonfall's `RespectCloseHotkey` on the title. It assumes Dalamud's "focus management" setting (`IsFocusManagementEnabled`) is on. I believe that is the default, but I did not read the config.
- **m24's fix in the game.** The renderer sends no keys and keeps the mouse off-screen. The popup's close in `NewFrame` and its focus returning to the pill are reasoned from ImGui 1.89–1.90, as in round 4.
- **Hover-only states:**
  - the Veiled pill's hover glow;
  - the 1280 stop tooltip (m26 is computed);
  - the 640 head's click-to-reveal;
  - the tally's Map and Esc landing on stage 9 (m25 is code-read).
- **Unrendered:**
  - a veiled challenge: none appears on page 1, even at `--story 0` walked;
  - the companions' veiled joins line.
- **Tests and gates.** I did not build or run them; the worktree is read-only for me, and building writes `bin/` and `obj/`. The response reports 8863 passed.
- **Gamepad input reaching the game.** `GameKeyClaim` clears keyboard keys only; unchanged.
- **Fonts and window sizes:** Dalamud's own font metrics, sizes between 640 and 1280, and longer locales (relevant to the plain tally note).
