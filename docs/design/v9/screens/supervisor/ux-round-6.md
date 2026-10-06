# Moonfall screens: UX/UI supervision, round 6

**What I reviewed.** I read three documents first:
- my round-5 review;
- the implementer's round-5 response, with the owner's answers on m12 and m13;
- the round-5 game-designer and level-critic reviews.

Then I read `git diff 8c51d4d8 8addf12c` at HEAD 8addf12c. The code I read:
- `DrawWindow` and the `escAfterPopup` latch, `FollowShield` and `DrawPlainBar` (`MoonfallWindow.cs`);
- `Keyboard.EscapeHeld`;
- in `Map.cs`: `veiledComing`, `panelRevealable`, the stop reasons, the 640 short line, the 640 head (NoNav and the slate fill) and the "here" ring;
- the tally focus (`Tally.cs`), and the wrapped plain note and text sink (`Board.cs`);
- `DrawTitle` with its backdrop choice and chart fallback (`Title.cs`);
- `MoonfallBackdrops`, `MoonfallGameArt.BackdropFailed`;
- the new and changed lints in `MoonfallScreensLintTests.cs`.

To check m27 I reused round 5's decompiled `WindowHost` (`scratchpad/r5/WindowHost.cs:546–559`).

**Renders.** I re-rendered with the Release renderer into `…/scratchpad/ux6/`, never into the repo. The renderer's `Tsukimichi.dll` was built at 05:49 and `Tsukimichi.Core.dll` at 05:22, both after every changed source; the newest is `MoonfallWindow.cs` at 05:25. All 28 runs used `--text-check`, each at 1280×800 and 640×480:
- `title --story 0`, `--story 0 --far-built --far-walk`, `--story 0 --hint`, `--story 0 --decoration off`, and `--story 1` (Sohm Al, for comparison);
- `far --far-built --far-won 30 --story 2`, at `--stage 9` (veiled, not reached) and `--stage 7` (veiled, reached);
- `far --far-built --far-walk`, at `--story 0` (the stepover, with the moogle) and `--story 3`;
- `far --far-walk --story 0 --stage 4` (unbuilt);
- `tally --far-built --far-walk --leave-one --story 3`, rich and `--decoration off`;
- `play --decoration off`;
- `map --story 0`.

The committed renders match mine to within JPEG noise: PSNR 35.7–43.0 dB on `title-arr-*`, `far-veiled-not-reached-640`, `tally-road-waits-*` and `hud-plain-640`.

To judge the painting I also looked at the ungraded source, `-nowloading_base02` (`ux6/raw-base02.png`, from the rich `.cache`).

**Measuring.**
- WCAG ratios: the text's 97th–99th luminance percentile against the ground. This round the ground is a strip just beside the text, not the text box, because the gilt's dark outline would drag the box median down.
- Cap heights come from row profiles.
- Crops: `s-logo.png`, `s-640top.png`, `z-arr640-logo.png`, `s-here.png`, `s-farr.png`, `s-bottom.png`, `s-arr-variants640.png`.

## Summary
**m27, m28 and m29 are resolved as specified, and so are all the Nits.** All 28 `--text-check` runs report **0 leaks**, with 0 to 7 stages veiled.
- **m27.** The latch is in `MoonfallWindow.cs:253–254`. Dalamud reads `KeyState[ESCAPE]` after `Draw` (`WindowHost.cs:550–551`), and `RespectCloseHotkey` stays false until Esc is let go. That holds whether ImGui closed the popup in `NewFrame` or left it open.
- **m28.** At 640, the not-reached stage reads "Past your story, and not reached yet" over "🔒 Not reached" (`farnr-640.png`).
- **m29.** The rich road-waits tally's halo is on **Map** (`tallyrw-1280.png`), and the plain tally marks Map as its default.

**m13, the A Realm Reborn title.** The painting is Ul'dah, so it is story-safe at A Realm Reborn. The menu and the Continue card stay legible at both sizes, and at 1280 the text contrast is better than over Sohm Al. **At 640, though, the logo sits on the painting's pale paper sky.** The gilt's fill reaches only about 1.9:1 there, against 7.3:1 over Sohm Al, and the small title's scrim shows a hard box edge under the rule (m30).

**m12.** At A Realm Reborn the walk reaches stage 11, and the moogle shows face up and won (`farso-1280.png`).

**Two new Minors, none of them Major:**
- **m30 (640 title on Ul'dah):** the logo's fill against the ground falls to about 1.9:1, and the scrim's top edge shows.
- **m31 (input, found this round, predates it):** the tally sets its default only with `SetItemDefaultFocus`, which ImGui honours only on a window's appearing frame. m29 moved the visual default, but Enter or A may not press Map. This corrects my own round-5 claim that Enter pressed Replay.

**Overall: APPROVE** (no Major remains; m30, m31 and the Nits are to fold in).

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title (Sohm Al) | APPROVE (unchanged) | APPROVE |
| **title-arr (Ul'dah, new painting)** | **APPROVE** (all title text is above Sohm Al's ratios; Nits on the blurred lower band and the grade) | **APPROVE with m30** (the logo holds only by its outline; the scrim edge shows) |
| title-arr, hint and decoration off | APPROVE | APPROVE (the hint bar is opaque) |
| title-road-waits, title-hint | APPROVE (unchanged) | APPROVE |
| map | APPROVE (the ring is fixed) | APPROVE |
| far | APPROVE | APPROVE |
| far-veiled (reached) | APPROVE (Nit fixed: no repeated wording) | APPROVE (the head reveals; slate fill on hover, code-read) |
| far-veiled-not-reached | APPROVE | **APPROVE** (m28 fixed) |
| far-stepover (the moogle at stage 11) | APPROVE | APPROVE |
| far, veiled and unbuilt ("Levels on their way") | not renderable (Unverified); code-read APPROVE | code-read APPROVE (the line fits; see Text floors) |
| tally-road-waits | APPROVE (m29: the halo is on Map; m31) | APPROVE (m31) |
| tally-road-waits-plain | APPROVE (the note is wrapped; Nit on the panel height) | APPROVE |
| hud-plain (new) | APPROVE (code, balls, oranges, ×N, then the name) | APPROVE (all five parts fit) |
| levels, characters, quickplay, challenges, duel, options, pause, duelhud | APPROVE (re-rendered only by the implementer; diff-neutral) | APPROVE |
| **Input rules** | **APPROVE** (m27 fixed; m31 new, predates this round) | |
| **Spoiler shield** | **APPROVE** (m28 fixed; Ul'dah is story-safe at A Realm Reborn) | |
| **Overall** | **APPROVE** | |

## Round-5 findings, one by one
- **m27, Esc that closed a popup on the title also closed the window: resolved (code-read).**
  - The latch is `escAfterPopup = (escAfterPopup || popupWasOpen || ImGui.IsPopupOpen(…Any…)) && Keyboard.EscapeHeld()`, then `RespectCloseHotkey = flow.Current == Title && !escAfterPopup` (`MoonfallWindow.cs:253–254`).
  - It runs inside `Draw`, before Dalamud's check (`WindowHost.cs:372` draws, then `:550–551` checks), so it gates the very frame that Esc closes the popup.
  - Both ImGui cases are covered:
    - With keyboard nav enabled, ImGui closes the popup in `NewFrame`, and `popupWasOpen` from the last frame trips the latch.
    - Without nav, the popup stays open, and `IsPopupOpen` trips it.
  - It holds while Esc is held: Dalamud sets `wasEscPressedLastFrame` only when it closes something, so without the latch it would close the window on the next frame. It releases on key-up, and the next press closes the window as usual.
  - `popupWasOpen` is still set after `DrawMenu` (`:284`).
  - Lint: `Dalamuds_close_key_closes_the_window_only_from_the_title` (`MoonfallScreensLintTests.cs:84`) asserts the latch, its `EscapeHeld` release, and that the latch comes before the gate.
- **m28, the 640 line on a not-reached veiled stage: resolved.**
  - `panelState` uses `MoonfallStageVeiledShort` only when `panelRevealable`. Otherwise it uses the sealed line, or the coming line (`Map.cs:883`).
  - Rendered: "Past your story, and not reached yet" over "🔒 Not reached" (`farnr-640.png`).
  - The lint asserts that `MoonfallStageVeiledShort` appears exactly once, behind `panelRevealable` (`MoonfallScreensLintTests.cs:241–245`).
- **m29, the road-waits tally's default press: resolved as specified (see m31).**
  - Replay takes focus only when `tallyVeil is null`, and home takes it when `tallyVeil` is set (`Tally.cs:309`, `:321`). The plain tally puts `SetItemDefaultFocus` on Map (`Board.cs:662–666`).
  - Rendered: the Map pill carries the warm focus glow and Replay does not (`tallyrw-1280.png`).
  - Linted in `The_tallys_next_and_notes_follow_where_the_road_goes_next` (`:253`).
- **Nits: all resolved.**
  - **The 1280 panel wording** now reads "Set past your story. Right-click its name or press Reveal its place. It shows for this session only." (`s-farr.png`).
  - **The stop tooltip's second line** is now "Select it to reveal its place" (`Strings.resx`).
  - **The 640 head** is pushed `NoNav` when it cannot act (`Map.cs:616–627`). On a revealable stage it gets the slate fill on hover or nav (`:653–657`).
    - The tabs and stops are ordinary nav items and no bumper switches campaign, so focus cannot be left on a head that turned inert.
  - **The "here" ring** is now `#C9D2F2` at full alpha, width 2.6 (`Map.cs:444`). It is a clear pale band at both sizes (`s-here.png`), well over 3:1 against the navy ground.
  - **The plain tally note** is wrapped at `panel.X − 2·pad` (`Board.cs:632`). See the Nit on the panel's height.
  - **The m25 lint** asserts that `EndBoard` reads `tallyVeil` and selects its stage.
  - **Carried over, accepted:**
    - the unbuilt title's "stage 1 of 12";
    - the 640 road-waits mark;
    - the flat selected-row band.

## The A Realm Reborn title (m13)
- **The choice.**
  - `FollowShield` picks Title, then TitleEarly, then the chart, by the shield (`MoonfallWindow.cs:161–163`). `DrawTitle` falls back to the chart only when `BackdropFailed` says the painting could not be read (`Title.cs:195–199`).
  - While the art builds, the night stands in and `menuArtPending` counts it, except for a failed painting, so the renderer's settle wait cannot hang on a missing file.
- **The painting.** The ungraded `-nowloading_base02` (`raw-base02.png`) is plainly Ul'dah: gold domes, minarets, the palace's great dome and an airship. That is story-safe for any A Realm Reborn character.
  - The jewel grade, shared with Sohm Al (`MoonfallBackdrops.cs:61`, `:69–86`), turns the gold to amethyst. The result reads as a domed, spired city under a moon, in the title's palette.
  - It is a sketch on pale paper, much brighter than Sohm Al's night:
    - the open area at 1280 has median luminance **0.171** against **0.075**;
    - the 640 logo band has **0.280** against **0.071**.
- **The menu and the Continue card: legible at both sizes.**
  - The pills and the card are opaque, so the painting never shows through their labels.
  - At 1280, the left scrim (`Title.cs:227–231`) puts the whole menu column on a dark ground.
  - At 640, the Continue sub-line "The Airship Road · with Cid" is at 5.9:1 against its ground median (Sohm Al: 5.2:1).
- **Title text against the painting** (fill against the ground strip beside it, median, then 90th percentile):

| Text | Ul'dah | Sohm Al |
|---|---|---|
| 1280 logo "MOONFALL" | 5.8 / 2.9 | 4.2 / 3.2 |
| 1280 subtitle | 8.9 / 4.4 | 4.5 / 3.0 |
| 1280 subtitle, "Menphina's moon" end | 6.8 / 3.9 | 3.5 / 2.7 |
| **640 logo** | **1.9 / 1.6** | 7.3 / 3.7 |
| 640 subtitle | 6.3 / 2.9 | 5.8 / 3.4 |
| 640 Continue sub-line | 5.9 / 4.0 | 5.2 / 3.9 |
| Version "1.22.1" (both sizes) | 5.0–8.6 | 4.7–8.5 |

  - At 1280, Ul'dah is better than Sohm Al throughout, because the scrim covers the logo's side.
  - At 640, the logo sits above the small scrim, which starts at design y 110 (`Title.cs:236–239`), straight on the pale sky over the great dome. It still reads, but only through its dark edge (`z-arr640-logo.png`): m30.

## Findings
- **[Minor] m30. At 640, the A Realm Reborn title's logo sits on pale sky, and the scrim's top edge shows as a box.**
  - In the small layout, the scrim's two horizontal gradients start at design y 110, just under the rule (`Title.cs:236–239`). Above that the logo has no scrim.
  - Over Ul'dah the band behind the logo has median luminance 0.33. The gilt's fill against it is about **1.9:1**, under 3:1 even for display type; Sohm Al gives 7.3:1. The glyphs hold only because of their 1-unit dark outline.
  - Against that brighter ground, the scrim's hard top edge reads as a darker rectangle starting at the rule (`s-640top.png`, `s-arr-variants640.png`).
  - 1280 is not affected: there the logo is inside the left scrim.
  - **Fix (either; the first is preferred because it also calms 1280's pale upper right):**
    - Give TitleEarly its own night grade in `MoonfallBackdrops.Title`, for example `Ceiling ≈ 0.50` and `Exposure ≈ 0.80`, against Sohm Al's 0.66 and 0.95 (`:80`). That brings the paper sky toward Sohm Al's level. Aim for the 640 logo band's median luminance ≤ 0.12, which gives the fill ≥ 4:1.
    - Or, at 640, start the scrim higher behind the logo (about design y 40), with a vertical fade from clear at its top, so it has no hard edge. Then re-render `title-arr-640` and check the edge has gone.
- **[Minor] m31. The tally's default press is set only with `SetItemDefaultFocus`, which ImGui ignores outside a window's appearing frame.**
  - The rich tally's `PillButton` marks its default with `ImGui.SetItemDefaultFocus()` alone (`Tally.cs:493–505`), and so does the plain tally (`Board.cs:662–666`). Both are drawn inside the Moonfall window, which has long been open when a level ends.
  - In ImGui's source, `SetItemDefaultFocus` begins `if (!window->Appearing) return;`, and it otherwise acts only on a pending nav init. The installed `cimgui.dll` carries the string "1.88". This is from my reading of the source; no copy is on disk, so it is not verified here.
  - So m29's change moves the **visual** default correctly: the rich pill is `lit` when `focus && !NavVisible`, so Map glows. But for a keyboard or gamepad player:
    - Enter or A acts on whatever nav item play left focused;
    - a first arrow starts nav at the window's first nav item, not at Map.
  - My round-5 m29 said "Enter or A presses Replay", and that overstated what the code does.
  - Predates round 5. Esc and B already land on the map at the waiting stage (m25), so a keyboard path exists; hence Minor.
  - **Fix:** do what the menus do. `EnteredScreen` sets `focusAsked = NavVisible` (`Menu.cs:88`), and `DefaultFocusBefore` calls `SetKeyboardFocusHere()` before the default entry (`Menu.cs:419–426`). For the tally:
    - set a `tallyFocusAsked = ImGui.GetIO().NavVisible` once when a new tally starts (the `tallyShown` change in `Tally.cs:409–411`);
    - call `ImGui.SetKeyboardFocusHere()` once before the focused pill, and before the plain tally's default button, then clear it;
    - lint it beside `DefaultFocusBefore`.

    To verify in game: win the road-waits level with the keys, press Enter, and the map should open on stage 9.
- **[Nit]**
  - **The plain tally's panel does not grow for a wrapped note,** although the response says it does. Its height adds a fixed 1.4 lines for the note (`Board.cs:600`).
    - By my arithmetic a two-line note still fits in the slack above the buttons. A third line would overlap them.
    - Measure `ImGui.CalcTextSize(tallyNote, false, panel.X − 2·pad).Y` once in `PrepareTally`, and add it in place of the fixed 1.4 lines.
  - **The Ul'dah title's lower third is the loading screen's own blurred band:** vertical smear streaks behind Duel, Companions and Options, and under the card (`s-bottom.png`).
    - The shared crop takes rows 40–1050 of the 1080 painting (`MoonfallBackdrops.cs:72–76`).
    - Crop TitleEarly shorter, for example rows 40–800 resampled to 800, or accept it as a reflection. It sits on the owner's rich-art preference.
  - **The grade removes Ul'dah's gold,** the one cue that says "Ul'dah" at a glance. This is consistent with the title's palette, and a call for the owner or the art realism supervisor, not a UX fault.
  - **Carried over:**
    - the 640 road-waits mark (optional);
    - the flat selected-row band;
    - the unbuilt title's "stage 1 of 12".

## Input rules
- **Mouse first: holds.**
  - The 640 head of a revealable stage now shows the slate hover fill. It still opens the shield's menu on a click (code-read).
  - Placeholders keep their own hover and right-click.
- **Visible focus: holds.**
  - The inert 640 head is no longer a dead nav stop (`NoNav`, `Map.cs:616–627`).
  - The tally's halo is on the button its note names.
- **Esc goes back, or pauses in play: holds.**
  - On the title, Esc that dismisses the shield's menu no longer closes the window (m27, latched until release).
  - Off the title, `HandleKeys`' popup gate is unchanged.
- **Default action: the cue holds; the press is m31.** The road-waits tally points to Map and haloes Map. Whether Enter or A presses it depends on m31.
- **Keys are claimed from the game: holds, unchanged.**
- **A click that opens or resumes never shoots: holds, unchanged.**
- **Safety on spoiling actions: holds.**
  - A reveal is offered only where it opens something: reached and built (`Map.cs:875–876`).
  - The 640 line no longer points to a missing Reveal button.
  - The A Realm Reborn title shows only an A Realm Reborn place, and the chart remains the fallback.
- **Holds (press-and-hold buttons): unchanged.**

## Text floors (measured at 640)
**Labels (7 px cap floor): pass.**
| Text | Cap (px) |
|---|---|
| Panel line "Past your story, and not reached yet" (Axis 12) | 7 (rows 144–150, `farnr-640.png`), at the floor |
| Panel line "Past your story; its levels are on their way" (unrendered) | 7, computed: about 197 px at 4.47 px a character, within 230 units (`Map.cs:675`, `x1 − x0 − 32`), so no shrink |
| Plain tally note (ImGui font) | ≥9 |
| Title logo, subtitle, Continue sub-line, version | unchanged sizes |

**Numbers (8 px floor): pass, unchanged.** No new numerals at 640.

**Contrast (secondary text, 4.5:1 floor):**
| Text | Ratio |
|---|---|
| 640 "Past your story, and not reached yet" | 7.1:1 |
| 640 subtitle over Ul'dah | 6.3:1 against the median ground |
| 640 Continue sub-line over Ul'dah | 5.9:1 |
| Version over Ul'dah | 5.0–8.6:1 |
| 640 logo fill over Ul'dah (display type) | **≈1.9:1, held by its outline (m30)** |
| Veiled "here" ring against the chart (non-text) | well over 3:1 (round 5: ≈2.4:1) |

## Unverified
- **m27 in the game.** It is reasoned from the decompiled `WindowHost` and the latch. Three assumptions:
  - Dalamud's focus management is on, as in round 5.
  - The game's `KeyState[ESCAPE]` and ImGui's `IsKeyDown(Escape)` release on the same frame. A one-frame skew where ImGui sees the release first would let Dalamud close the window on that frame.
  - Keyboard nav may or may not be enabled; both cases are covered.
- **m31's premise.** ImGui's `SetItemDefaultFocus` appearing guard is from my reading of the source, not from a copy on disk. The "1.88" version comes from a string in `cimgui.dll`. In-game behaviour after a level ends is untested: the renderer sends no keys.
- **Hover-only states:**
  - the 640 head's slate fill;
  - the stop tooltips' three reasons;
  - the tally's Map press.
- **Unrendered:**
  - a veiled, reached and unbuilt stage ("Levels on their way"). The renderer's `--far-built` builds every stage, and without it the stage is not reached (`farcoming-640.png`).
  - the m16 "Next · FS 1-1" pill after winning base-55.
- **The painting's identity** rests on the ungraded texture as the rich cache holds it (`ui_loadingimage_-nowloading_base02.png`). I did not cross-check it against the game's sheets.
- **Tests and gates.** I did not build or run them; the worktree is read-only for me. The response reports 8868 passed.
- **Fonts, sizes and locales:** Dalamud's own font metrics, sizes between 640 and 1280, and longer locales (the plain tally's panel-height Nit).
