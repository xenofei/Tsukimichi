# Moonfall screens: response to round 5

**Round 5 verdicts**
- Game designer: APPROVE (no Major), with m14–m17, n11–n12. m12 and m13 were the owner's to decide.
- UX/UI: APPROVE (no Major), with m27–m29 and Nits.
- Level critic: APPROVE, with Nits.

Every Minor and Nit is answered below, and so are the owner's two answers. The renders in `../renders/` are made again from this state.

**New renders**
- `title-arr-*`: the title before Heavensward, now over Ul'dah's painting (m13). These replace round 5's chart version.
- `hud-plain-*`: the plain board's bar in solo play, which shows the new order (LC Nit).
- `far-stepover-*` changes: stage 11 is set nowhere now, so at A Realm Reborn the walk reaches and wins it, and the moogle shows face up (m12).

## The owner's answers

**[Minor] m12. Stage 11 is set nowhere. Owner's answer: recommendation taken (6 October 2026).**
- `MoonfallPlaces`: the Courier's Wake (Storm Post, the moogle) is `MoonfallPlace.Nowhere`, like the Ferry in the Stars (stage 6). It is our own painting of a courier's wake over open sky, so it names no place.
- The stage has no shipped levels yet, so it has no scene recipe. A new test, `The_courier_stages_shipped_levels_show_scenes_of_no_place`, holds every stage-11 level that ships to a scene of no place. The stage's lore ("Moogle post, every inn in Eorzea") names no area.
- The walk-the-road theory no longer exempts A Realm Reborn. At every era from A Realm Reborn to Dawntrail:
  - stage 11 is not veiled;
  - the moogle is `Available`;
  - the moogle is in `QuickPlayCompanions()`;
  - `QuickPlay("expansion-51", Moogle)` is not null.
- The era test allows exactly stages 6 and 11 to name no place.
- `moonfall-modes.md`: the Far Shore list says the Courier's Wake is set nowhere, and §3 says the moogle is reached at every era once the road comes to stage 11.

**[Minor] m13. The title before Heavensward is Ul'dah's painting. Owner's answer: recommendation taken (6 October 2026).**
- There is a new backdrop, `MoonfallBackdrop.TitleEarly`. It uses `ui/loadingimage/-nowloading_base02.tex`, the Ul'dah loading screen, graded like the title.
- It is tagged `(A Realm Reborn, "Ul'dah - Steps of Nald")`. Tests:
  - `MoonfallShieldGameDataTests` adds it to the places it checks against the real shield over the game's data. With `TSUKIMICHI_GAME_PATH` set, the shield places "Ul'dah - Steps of Nald" in A Realm Reborn.
  - `MoonfallShieldTests` asserts that a story at A Realm Reborn hides Sohm Al but not Ul'dah.
- `FollowShield` chooses the title's backdrop:
  - the Title (Sohm Al) when the shield shows it;
  - otherwise TitleEarly, when the shield shows it;
  - otherwise the chart.
- `DrawTitle` falls back to the chart only when the chosen painting cannot be read (`MoonfallGameArt.BackdropFailed`). The chart is the last fallback, never the first choice.
- `moonfall-modes.md`'s backdrop policy says the same.
- Renders: `title-arr-1280` and `title-arr-640` (`--story 0`).
- For the reviewer: the painting is the game's own Ul'dah loading screen as the client ships it. Please confirm by eye that it reads as Ul'dah and is story-safe at A Realm Reborn.

## Game designer, round 5

**[Minor] m14. No reveal on a reached veiled stage whose levels are not built.**
- `panelRevealable = Veiled && Reached && !veiledComing`, where `veiledComing = Veiled && Reached && !StageBuilt`.
- On an unbuilt stage the panel keeps the veil (the mark and the placeholder) and shows:
  - the Waiting pill "Levels on their way";
  - the line "Set past your story, and its levels are on their way: there is nothing to reveal yet." (`MoonfallStageVeiledComingLine`).
- The reveal stays on the name's right-click, as the shield does everywhere.
- The stop tooltip gives three reasons:
  - not reached → "Past your story, and not reached yet";
  - built → "Select it to reveal its place";
  - unbuilt → "Past your story; its levels are on their way".
- The 640 line follows the same rule. Its pill and state are `Waiting`.
- Tests:
  - `A_partly_built_far_shore…` now asserts that stage 4 at Heavensward with stages 1–3 built is `Veiled`, `Reached` and not `StageBuilt`.
  - The screens lint holds the map to `veiledComing` and `panelRevealable` exactly, and holds the stop tooltip's three-way choice.

**[Minor] m15. A level the road stepped to stays open.**
- `MoonfallProgress` has a per-campaign high-water mark of the frontier: `BaseReach` and `ExpansionReach`.
  - It is optional and 0 in older files.
  - `Clean` clamps it to the level counts.
  - `Absorb` merges the further of the two, and `Copy` keeps it.
- `MoonfallModes.NoteReach()` records `max(reach, Frontier)` for each open campaign. `FinishLevel` calls it after every level's end.
- `Slot` opens level *i* when any of these is true:
  - `i <= Frontier`;
  - its predecessor is won;
  - `i == Reach`.
  
  Only the level the road came to opens, not everything before it, so a revealed stage still opens from its first level. Veiled still ranks first, so an alt with less story sees nothing new.
- `CompanionReached` goes through `Slot`, so the moogle follows the same rule.
- Your two tests:
  - `A_level_the_road_stepped_to_stays_open_when_the_story_moves_on_unplayed`: Heavensward walked until Continue is 11-1, unplayed, then the story reaches Stormblood:
    - the frontier is 4-1;
    - 11-1 is `Open` and the moogle is `Available`;
    - 11-2 is still `Sealed`;
    - the mark survives `Copy` and `Absorb`.
  - `Revealing_a_stepped_over_stage_never_closes_the_level_continue_had_offered`: Heavensward after 3-5, with The Ruby Sea revealed:
    - the frontier is 4-1;
    - 5-1 is `Open` and stage 5 is `Open`.
- `WalkTheRoad` now calls `NoteReach()` after each win, as `FinishLevel` does.
- `moonfall-modes.md` §2: the Unlocks block adds "or i is the campaign's reach", and the bullet now reads "A level the road came to stays open, for good", describing the mark (n11).

**[Minor] m16. Two fixes on the tally.**
- "The last level for now" is drawn only when `modes.Next()` is null.
- `AdventureNext()` falls back to `modes.Next()` when it is playable and `NextLevel` has none. So winning base-55 with the Far Shore built offers "Next · FS 1-1" (`PlaceCode`).
- Test: `Winning_the_moon_roads_last_level_leads_on_to_the_far_shore_not_to_the_last_level_note`:
  - `NextLevel(Base, 54)` is null, with no stepped-over stage;
  - `Next()` is FS 1-1, not veiled.
- A new lint, `The_tallys_next_and_notes_follow_where_the_road_goes_next`, holds the fallback and both notes' conditions.

**[Minor] m17. The 640 line on a veiled stage that is not reached.** See UX m28 below.

**[Nit] n11.** In `moonfall-modes.md` l.69, the veiled stage's pill reveals only when the road has come to the stage and its levels are built. Otherwise:
- the pill is the padlock's "Not reached", or "Levels on their way";
- a reveal is left to the name's right-click.

**[Nit] n12.** The walk theory asserts the rule itself, at every era: `CompanionReached(who) == (view.State != Veiled && (view.Levels[0].Reached || a level of the stage is won))`.

## UX/UI, round 5

**[Minor] m27. An Esc that dismisses a popup on the title no longer closes the window.**
- `DrawWindow` latches `escAfterPopup = (escAfterPopup || popupWasOpen || ImGui.IsPopupOpen(any)) && Keyboard.EscapeHeld()`.
- It then sets `RespectCloseHotkey = flow.Current == Title && !escAfterPopup`. So the press that closed the popup cannot close the window too, even though Dalamud reads the key as held after the window draws, until Esc is let go.
- `Keyboard.EscapeHeld()` keeps the key code in `Keyboard.cs`, under the existing lint.
- The lint asserts:
  - the latch;
  - its `EscapeHeld` release;
  - that the latch is set before the close key is gated on it.
- Unverified in game: the Dalamud close-hotkey path is reasoned from the decompiled `WindowHost`, not pressed in the client.

**[Minor] m28 / GD m17. A 640 veiled stage that is not reached.**
- The short line is `MoonfallStopVeiledSealedLine` ("Past your story, and not reached yet"). An unbuilt stage gets `MoonfallStopVeiledComingLine`.
- `MoonfallStageVeiledShort` ("Press Reveal or right-click its name") is drawn only when `panelRevealable`. The lint asserts that it appears exactly once on the map and behind `panelRevealable`.
- The new render `far-veiled-not-reached-640` shows "Past your story, and not reached yet" over "🔒 Not reached".

**[Minor] m29. The road-waits tally's default press is Map.**
- Rich tally: Replay takes focus only when `tallyVeil is null`, and home (Map) takes it when `tallyVeil is not null`.
- Plain tally: `SetItemDefaultFocus` goes on the home button when `tallyVeil` is set.
- Both are linted.

**Nits**
- **Panel wording:** "Set past your story. Right-click its name or press Reveal its place. It shows for this session only." This is your wording, with the reason kept first, because the panel's name is the placeholder.
- **Stop tooltip:** the second line drops its lead: "Select it to reveal its place".
- **The 640 head:**
  - When it can't act (sealed, not reached or unbuilt), it is pushed `NoNav`, so it is no longer a dead focus stop.
  - On a revealable stage it shows the slate hover fill.
- **The "here" ring:** now `#C9D2F2`, at full alpha and width 2.6, well over 3:1 against the chart ground.
- **The plain tally note:** wrapped at `panel.X − 2·pad`. The panel grows for the wrapped lines.
- **Lint for m25:** `EndBoard` must read `tallyVeil` and select its stage.
- **Carried over, not changed:**
  - the unbuilt title's "stage 1 of 12" sub-line (the game designer has not asked for a change);
  - the optional 640 road-waits mark;
  - the flat selected-row band.

## Level critic, round 5

- **The plain solo bar order.** In a level the bar now draws the code, then Balls, Oranges and ×N, and the name last (`[stageText, ballsText, orangesText, multiplierText, levelName]`). A narrow window drops the name before any game state. The duel order is unchanged. The lint holds both orders. See `hud-plain-*`.
- **The race test can fail without the game.**
  - The fake host reads every game painting as a solid magenta sentinel (1920 × 1080), with no install needed.
  - `Upload` records a SHA-256 of each thumbnail's pixels.
  - The test first proves that it discriminates: the sentinel-built Kugane thumbnail and the story-safe one differ.
  - It then asserts that the single landed thumbnail is exactly the story-safe one.
- **The plain tally's strings go through the text sink.** That covers the banner, the rows, the note and the button labels. `--text-check` on the plain road-waits tally now hears 179 strings at 1280 and 172 at 640 (round 5: 170 and 163), with 0 leaks.
- **Focus on the road-waits tally:** see UX m29.

## Checks

- **Allocations** (`--alloc`, after 2 s of warm-up):
  - 0 B over 600 frames: the title at A Realm Reborn (Ul'dah), plain play at 640, rich play at 1280, the map at A Realm Reborn, and the rich road-waits tally.
  - The plain road-waits tally at 640: one 512 B allocation in the first frames measured, the same over 600 and 2400 frames, so nothing per frame.
- **Text check** (`--text-check`): 0 leaks on the A Realm Reborn title, the stepped-over map, the plain road-waits tally, and Companions and Quick Play at A Realm Reborn, at both sizes.
- **Gates:**
  - `dotnet build Tsukimichi.sln -c Release -warnaserror`: 0.
  - LoadCheck: 0.
  - `dotnet test Tsukimichi.sln -c Release --filter "Category!=Perf"`, with `TSUKIMICHI_GAME_PATH` set: 0; 8868 passed, 0 skipped.
  - `py -3 tools/themes/build_themes.py --check`: 0.
