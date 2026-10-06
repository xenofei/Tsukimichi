# Moonfall screens: response to round 6 (final)

**Round 6 verdicts**
- Game designer: APPROVE (no Major), with m18 and Nits n13–n16.
- UX/UI: APPROVE (no Major), with m30, m31 and Nits.

This is the last fold-in: there is no round 7. Every finding is answered below. `title-arr-*` is rendered again; the other renders are unchanged by this round's work except where noted.

## Game designer, round 6

**[Minor] m18. The companion rule: the new rule is kept, and the doc and a test now say so.**
- The rule: a companion is reached once its stage's first level is open (built and reached, by the road's rule) or a level of its stage is won, and its stage is not veiled. A stage whose levels are not built yet reaches no one, so its card stays dimmed while the map says "Levels on their way".
- `moonfall-modes.md` §2 (the Unlocks block) says exactly that.
- Test: `A_companion_is_reached_only_once_its_stage_has_a_level_built_and_reached`.
  - The Moon Road with stage 1 built and won, stage 2 not built: the frontier is 2-1 (Missing), and the twins are `MetNotReached`.
  - With 2-1 shipped: 2-1 is `Open`, and the twins are `Available`.

**[Nit] n13. Sohm Al that cannot be read falls to Ul'dah before the chart.**
- `FollowShield` also notes whether the shield shows Ul'dah (`titleEarlyShown`).
- `DrawTitle`: when the chosen backdrop is Sohm Al, it failed, and Ul'dah is shown, the title draws Ul'dah. Only a failed painting with nothing story-safe left falls to the chart.
- `moonfall-modes.md`'s backdrop policy says the same.

**[Nit] n14. Ul'dah's own focus.** See UX m30 below: TitleEarly has its own crop, focus and grade.

**[Nit] n15. `Next()` knows the reach mark.**
- When the frontier's level cannot be played (it fell back onto a level not built yet), `Next()` returns the campaign's reach level if it is `Open`, before the veiled scan.
- Test: `Continue_goes_on_at_the_level_the_road_came_to_when_the_frontier_falls_back_onto_an_unbuilt_stage`, your harness E.
  - Far Shore stages 1, 2, 5, 6 and 11 built, walked at A Realm Reborn to 11-1, which is left unplayed.
  - At Heavensward the frontier falls back onto 3-1 (Missing); `Next()` is 11-1, not veiled, and `Continue()` is 11-1.
- `moonfall-modes.md`, "Where Adventure goes next", adds the reach level.

**[Nit] n16. The fresh character: every city's starter sees Ul'dah.**
- New game-data test, through the real shield: `A_fresh_character_of_any_city_sees_uldahs_title_and_not_sohm_al`.
  - It models a fresh character as the evaluator does: one of the cities' openers ("Close to Home": Gridania's three, Limsa Lominsa's two and Ul'dah's three, one per starting class) is ready, and the others are locked out (`Foreclosed`, as `StateResolver` marks another city's start).
  - For all eight openers, with the default options, "Ul'dah - Steps of Nald" is shown and Sohm Al's "The Dravanian Forelands" is hidden. So the title is Ul'dah, never the chart, for a new Gridania, Limsa Lominsa or Ul'dah starter.
- One limit, found while probing: if the other cities' openers are not known to be locked out (every quest still "blocked", for example before the character's quest data has arrived), the real shield hides Ul'dah, and the title shows the chart until the data comes. That is story-safe, and it lasts only until the states arrive.

## UX/UI, round 6

**[Minor] m30, with GD n14 and the lower-band Nit: Ul'dah has its own crop, focus and night, and the 640 scrim fades in.**
- `MoonfallBackdrops.TitleEarly` builds Ul'dah through the same `TitleFrom` as Sohm Al, with its own settings:
  - **crop:** rows 40 to 800 of the 1920 × 1080 painting, down to the white gate. The loading screen's blurred lower band is gone.
  - **focus:** the painting's centre (the great dome) half across the frame (0.50; Sohm Al keeps 0.30).
  - **night:** `Ceiling` 0.42, `Knee` 0.36, `Exposure` 0.72, `SkyDrop` 0.40, `SkyBottom` 0.60 (Sohm Al: 0.66, 0.40, 0.95, 0.25, 0.70).
- The 640 scrim now fades in above the modes, from clear at design y 40 to its strength at 110, behind the logo. It is drawn as eight slices per half, each keeping the across ramp, so it has no top edge. This applies to every 640 title, Sohm Al's too.
- **Measured** (`scratchpad/r7/logoband.py`: the ground behind "MOONFALL", x 190–450 and y 86–134 of the 640 render, with the gilt fill and its outline left out; my baseline on round 6's render gave 0.274, against your 0.28):

| | 640 logo band, ground median | Gilt fill against it | 1280 open area, median |
|---|---|---|---|
| Round 6 (Sohm Al's grade) | 0.274 | 2.3:1 | 0.150 |
| New grade only | 0.090 | 5.2:1 | 0.085 |
| New grade and the faded scrim | **0.064** | **6.4:1** | 0.085 |
| Sohm Al, for comparison (with the faded scrim) | 0.058 | 6.8:1 | 0.064 |

- **The scrim edge:** at 3× zoom on the new `title-arr-640` and `title-640`, no edge shows under the rule.
- **The dome:** at 1280 it sits right of the modes, between the logo and the Continue card, with the white gate below it. At 640 the layout is centred, so the modes and the logo cover the frame's middle whatever the focus. There, the dome sits behind the logo and the Continue button, with the spires and the airship's arm on either side.
- Renders: `docs/design/v9/screens/renders/title-arr-1280.jpg` and `title-arr-640.jpg`. The other 640 titles (`title-640`, `title-hint-640`, `title-road-waits-640`) change with the scrim.

**[Minor] m31. A new tally's default press takes the keyboard focus.**
- As a new menu screen does (`EnteredScreen` and `DefaultFocusBefore`): when a new tally is prepared (`PrepareTally`, for a new game only, not on a language change), `tallyFocusAsked = ImGui.GetIO().NavVisible`.
- `TallyFocusBefore(isDefault)` calls `ImGui.SetKeyboardFocusHere()` once, before the default item, then clears the ask.
- Rich tally: `PillButton` calls it before its `InvisibleButton`, with the pill's `focus`, so it follows the existing default rule (Next; else Replay on a win; Map when the road waits, or no win).
- Plain tally: it now has the same default rule (`nextFocus`, `againFocus`, `homeFocus`). Each button asks before its item, and the default one is also marked with `SetItemDefaultFocus`.
- Lint: `A_new_tally_gives_its_default_press_the_keyboard_focus_as_a_new_menu_screen_does` checks the ask, the helper, its place before the rich pill's item and before each plain button, and the menus' own `DefaultFocusBefore` it mirrors.
- Unverified in game: the renderer sends no keys. To check: win the road-waits level with the keys, press Enter, and the map should open on stage 9.

**Nits**
- **The plain tally panel grows by the measured note.** `PlainNoteHeight` measures `CalcTextSize(tallyNote, false, wrap)` only when the note, the wrap width or the font size changes, and the panel adds that height (plus 0.4 of a line) in place of the fixed 1.4 lines.
- **The lower blurred band:** cropped away (m30).
- **The grade removes Ul'dah's gold:** kept, as the title's palette; the owner's call, as both reviewers say.
- **Carried over, accepted:** the 640 road-waits mark, the flat selected-row band, and the unbuilt title's "stage 1 of 12".

## Checks

- **Allocations** (`--alloc`, after 2 s of warm-up):
  - 0 B: the title at A Realm Reborn at 1280, and the rich and plain tallies at 640 and 1280 when the road does not wait.
  - One-off amounts, the same over 600 and 2400 frames, so nothing per frame: 88 B on the 640 title (Ul'dah or Sohm Al), and 512 B on the plain road-waits tally at 640 (as in round 5).
- **Text check:** `title --story 0` at both sizes: 0 leaks, 7 stages veiled.
- **Gates**, after the last edit:
  - `dotnet build Tsukimichi.sln -c Release -warnaserror`: 0.
  - LoadCheck: 0.
  - `dotnet test Tsukimichi.sln -c Release --filter "Category!=Perf"`, with `TSUKIMICHI_GAME_PATH` set: 0; 8872 passed, 0 skipped.
  - `py -3 tools/themes/build_themes.py --check`: 0.
- **Unverified:** in game, m31's focus after a level ends (the renderer sends no keys) and m27's Esc latch; the chart fallback when a painting fails to read (not exercised); the fresh character's title before the character's quest data arrives (see n16).
