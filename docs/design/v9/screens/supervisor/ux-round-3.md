# Moonfall screens: UX/UI supervision, round 3

**What I reviewed.** I read my round-2 review, the implementer's round-2 response, and the round-2 reviews from the level critic and the game designer. I looked at the committed renders in `docs/design/v9/screens/renders/`, including the new `far-veiled-*`, `duelhud-plain-*`, `scene-veiled-1280` and `scene-shown-1280`. I read the round-3 diff at HEAD c9547fd3:
- `MoonfallWindow.Map.cs`: `ShieldPlaceholder`, `ShieldMark`, `Legend`, `StopTooltip`, `MakeMapWords`, `FitLine`, `LevelTile`, `StagePanel`, `SmallStagePanel`
- `MoonfallWindow.Menu.cs`: `MenuButton` with the new `Veiled` style
- `MoonfallWindow.cs`: `FollowShield`, the re-arming in `DrawPlay`, `DrawPlainBar`, `RefreshPlainDuel`, the `ShieldText.DrawMenu` call
- `MoonfallWindow.Pause.cs`, `.Modes.cs`, `.Title.cs`, `.Characters.cs`, `.Rich.cs`, `.Flow.cs`
- `ShieldText.cs` and `PlaceholderMenu.cs`
- `MoonfallModes.cs`, `MoonfallShield.cs` and `MoonfallLooks.cs` in Core
- `Plugin.Moonfall.cs`
- `MoonfallScreensLintTests.cs` and `MoonfallShieldTests.cs`

I re-rendered with the Release renderer into my scratchpad (`…/scratchpad/r3/`), never the repo:
- `far` at 640 and 1280 with `--story 0`, `3` and `5`, and `--stage 3`, `9`, `10` and `12`, all with `--text-check`;
- `characters`, `challenges`, `quickplay`, `title` and `duel` at `--story 0`, with `--text-check`;
- `duelhud` at both sizes, also with `--seconds 8` (the player's turn) and `--decoration off`;
- `levels`, `quickplay`, `challenges`, `title`, `characters`, `pause` and `map`, and `options --reduce-motion`.

**How I measured.** As in round 2:
- Cap heights come from row profiles: a row counts as text when a pixel passes halfway between the ground and the text.
- Contrast is the WCAG ratio between the ground's median and the text's 97th to 99th percentile luminance.
- Crops are point-zoomed `z-*.png` files in the same folder.

## Summary
Every round-2 Minor (m11–m17) is resolved, and so are the four Nits I listed. The new plain-bar duel scores, the dim under the words and the re-arming all work as described. Every `--text-check` run reported **0 leaks**: 13 runs, with stories 0, 3 and 5, and up to 8 stages veiled.

The shield is mostly well judged:
- The placeholder sits in the name's slot, at its size, in the secondary ink.
- It answers with `ShieldText`'s own three-line hover and its "Reveal this name" menu.
- At 1280 the eye-slash reads clearly and is unlike the padlock.
- The legend row reads well.
- The Veiled Play pill gives its reason on hover and on focus.

**One new Major.** It is a regression the shield causes. When Adventure's next level lies in a veiled stage, `MoonfallModes.Continue()` returns null. The title then says "The road goes on. Every level built so far is won. More of the road is on its way." That is false: the levels exist, but they are past the player's story. The Adventure sub-line also falls back to "The Moon Road · stage 11 of 11".

There are six new Minors:
1. The reveal can only be reached with the mouse, though the focus text tells keyboard and gamepad players to right-click.
2. At 640, the stage head's "See the stage's five levels" tooltip merges into the shield's hover on a veiled stage.
3. Two placeholders do not answer like the others, and the 1280 stop tooltip prints one in primary cream.
4. A veiled challenge says "(right-click its name)", but that screen shows no name to right-click.
5. At 640 the shield mark is distinct from the padlock but does not read as an eye.
6. The plain duel bar at 640 cuts "Oranges 19" to "Ora".

**Overall: REVISE** (one Major, M4).

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title | REVISE (M4, from the code; m11 fixed) | REVISE (M4; m11 fixed) |
| title-hint | APPROVE | APPROVE |
| map | APPROVE | APPROVE |
| far | APPROVE | APPROVE |
| far-veiled (new) | APPROVE (m20, Nits) | APPROVE (m19, m22) |
| levels | APPROVE (m12 fixed) | APPROVE (m13 fixed, Nit) |
| characters | APPROVE (Nit fixed; m20 in the code) | APPROVE |
| quickplay | APPROVE | APPROVE (m14 fixed) |
| challenges | APPROVE (m14 fixed; m21 in the code) | APPROVE (m14 fixed; m21) |
| duel | APPROVE | APPROVE |
| options | APPROVE (note reworded) | APPROVE |
| hud | APPROVE | APPROVE |
| pause | APPROVE | APPROVE |
| tally | APPROVE | APPROVE |
| duelhud | APPROVE (m15 fixed) | APPROVE (m15 fixed) |
| duelhud-plain (new) | APPROVE | APPROVE (m23) |
| scene-veiled / scene-shown (new) | APPROVE | not rendered |
| **Input rules** | **APPROVE** (m16 and m17 fixed) | |
| **Spoiler shield** | **REVISE** (M4; m18–m22) | |
| **Overall** | **REVISE** | |

## Round-2 findings, one by one
- **m11, a locked Duel gave no reason: resolved.**
  - Both sizes pass `tooltip: DuelLockedWhy`, shown on hover and on focus (`Title.cs:250`, `:305`, `:326`).
  - With no levels, the 1280 sub-line reads "win a level first" (`Title.cs:119`).
  - Nit below on the second reason's wording.
- **m12, the 1280 Best line on the rule: resolved.** `capH` is 60 at 1280 (`Map.cs:958`). "Best 214,300" now clears the caption's bottom rule by about 6 px (`z-best1280.png`).
- **m13, sealed names cut at 640: resolved.**
  - The cut falls at a word's end ("Above the…", "Ironworks…").
  - The full name shows in a tooltip on hover or focus (`Map.cs:992–995`).
  - Nit below on cutting after "the".
- **m14, the overlapping pages: resolved.**
  - `first = page * rows` (`Modes.cs:233`, `:413`).
  - Quick Play page 2 at 640 now shows only 3-2 and 3-3, and the challenges read "1 of 2".
- **m15, the dimmed plate under 4.5:1: resolved.** The dim is now drawn under the words (`Modes.cs:946–973`). Measured at 640:
  - "YOU" on the waiting plate: **9.1–11.3:1** (was 3.8–4.4);
  - the opponent's name on the waiting plate, on the player's turn: **11.3–12.6:1**;
  - the lit, tinted "YOU": 5.3–6.5:1.

  Crop: `z-rails.png`.
- **m16, a double click on Resume or the crest: resolved.**
  - `DrawPlay` re-arms the board and clears `outsidePress` on every pause change (`MoonfallWindow.cs:277–282`).
  - An outside press now needs `BoardArmed` (`Pause.cs:153`).
  - I checked every resume path:
    - Resume, Esc and Start: the board is a `Dummy` from the next frame;
    - the outside release: the same;
    - the plain bar: its button sits above the board, and the board is a `Dummy` from the next frame;
    - the tally's auto-resume: the board is already a `Dummy` there;
    - the crest: the second press of the double click comes inside the 0.3 s, so it is not an outside press.
- **m17, no tests for the input fixes: resolved.**
  - `MoonfallScreensLintTests` now asserts the tally clause, the held-key carry-over, the outside-press rule with `BoardArmed`, and the re-arming.
  - It also checks that names go only through the shield helpers, and that the plugin passes the shield.
- **Nits from round 2: all resolved.**
  - A stale `outsidePress`: cleared on every pause change.
  - Padlock against Waiting: a challenge "on its way" now uses Waiting with no padlock (`chal0-1280.png`).
  - The power text: it wraps at 280 and ends well clear of the clip (`chars-1280.png`).
  - The held Decoration note: it reads "Reduce motion is on: nothing moves at any setting; Off still draws the plain board."
  - Not a round-2 Nit, but new and good: dimmed cards now carry the padlock.

## The spoiler shield
- **The placeholder: consistent with `ShieldText`.**
  - It takes the name's slot and size (Jupiter 32 at 1280, 21 at 640), with no glyph, in Moonfall's secondary ink `Ink2` (#C3CBEA), as `ShieldText.Tone` asks.
  - `ShieldPlaceholder` calls `ShieldText.Interact(…, SpoilerKind.Area, zone, shown)` (`Map.cs:75–87`), which gives:
    - the three-line hover ("Hidden by the spoiler shield", "It's from the story past yours.", "Right-click to reveal it for this session.");
    - the right-click menu with "Reveal this name · this session".
  - The window draws the menu at its root, after the screens (`MoonfallWindow.cs:258–262`). That matches `MainWindow`, `RouteWindow` and `DiscoveryWindow`.
  - `PlaceholderMenu` hands the request to the first host that draws, which is Moonfall in the same frame.
  - A reveal changes the shield's fingerprint. `FollowShield` then bumps `progressEpoch` and calls `VeilChanged`, so the panel, the stops and the thumbnails rebuild.
  - Gaps: m20 (two placeholders that do not answer) and m19 (the stacked tooltip at 640).
- **The shield mark.**
  - At 1280 it reads as an eye-slash on a dark disc: silver, about 12 px, at 9–11:1 against its disc (`z-mark1280.png`). It is plainly unlike the gold padlock.
  - At 640 on the map it is about 8×7 px with 1 px strokes. It reads as a silver cross-hatch at 8.9–9.1:1, distinct from the gold padlock by hue and outline, but not recognisable as an eye (`z-mark-vs-lock640.png`, `z-mark12.png`; m22).
  - On the 640 Veiled pill it is the same size and reads slightly better at 10:1 (`z-panel640.png`).
- **The legend: clear.**
  - "past your story" sits on a third row under "not reached", which carries the padlock, so the two marks can be compared. It measures 7.7–9.8:1 (`z-legend1280.png`).
  - The legend's own mark is the smallest (7 units), but it reads at 1280.
- **The Veiled Play pill: it gives its reason on focus.**
  - It is slate with the shield's mark and inert. On hover or focus it shows "Set past your story: it opens when your story gets there, or when you reveal the place (right-click its name)." (`Map.cs:515`, `:568`; `Menu.cs:476–479`, `:492–495`).
  - At 640 the panel also has a state line, "Opens when your story gets there".
  - The reason's remedy is mouse-only (m18).
- **Reveal with the keyboard or gamepad: not possible.**
  - The reveal opens only on a right-button release over the placeholder's rectangle (`ShieldText.cs:96–108`).
  - Nothing focusable opens it: the inert pill and the 640 head do nothing on a veiled stage.
  - Every other Tsukimichi placeholder is also mouse-only, so this is consistent with Tsukimichi. But Moonfall promises full keyboard and gamepad play (m18).
- **Leaks: none found.**
  - `--text-check` reported 0 leaks over every run.
  - The lint routes every stage or level name through `StageNameShown`, `LevelNameShown` or `PlayLevelName`.
  - Quick Play and Duel lists exclude veiled levels (`QuickPlayLevels` keeps only `Reached` slots).
  - The levels screen of a veiled stage cannot be opened:
    - stop and row hits need Open or Done (`Map.cs:442`, `:480`);
    - the 640 Levels pill is hidden (`Map.cs:560`).
- **The scenes.** `scene-veiled-1280` falls back to a plain night sky. The pegs stay legible and nothing suggests a missing picture. No name is involved, so it needs no placeholder.

## Findings
- **[Major] M4. When Adventure's next level is in a veiled stage, the title says "more of the road is on its way" and points at the Moon Road.**
  - `MoonfallModes.Continue()` returns a place only when its slot is `Open` (`MoonfallModes.cs:220`). `Slot` returns `Veiled` before anything else, so a veiled next level gives null.
  - The title then falls into its "nothing built" branch (`Title.cs:145–153`):
    - "The road goes on";
    - "Every level built so far is won. More of the road is on its way.";
    - and an "Adventure map" button.
  - The Adventure sub-line falls back to the Moon Road (`Title.cs:98`), giving "The Moon Road · stage 11 of 11".
  - The map's "here" also comes from `Continue()` (`MoonfallModes.cs:200`). So no stop is "here", and the map opens on the Moon Road tab.
  - This is the default-focus card of the first screen. It meets every player whose Far Shore road reaches their story's edge, which the owner's rule makes common: a player at A Realm Reborn is stopped at stage 3, one at Shadowbringers at stage 9.
  - It tells them to wait for an update, when the story or a reveal would open it, and it never names the shield.
  - Seen in the code; the renderer cannot stage that progress.
  - Fix:
    - Let `Continue()` (or a sibling) return the place with its state.
    - On the title, show a veiled next level as its code with "Past your story" in `Ink2`, and the line "Set past your story: it opens when your story gets there, or reveal its place on the map."
    - Label the button "Adventure map", and open the Far Shore with that stage selected (`mapCampaign = Expansion`, `mapStage = stage − 1`).
    - Base the Adventure sub-line on the same place.
    - Let `Here` mark the veiled stop. It can stay unlit, but the panel opens on it.
    - Add a `MoonfallShieldTests` case asserting that `Continue()` does not report "nothing left" when the next level is veiled.
- **[Minor] m18. The reveal is mouse-only, yet the focus text tells keyboard and gamepad players to right-click.**
  - Only `ShieldText.Interact`'s right-button release opens the menu (`ShieldText.cs:96–108`). The Veiled pill is inert (`Menu.cs:449`, `:497`), and the 640 head does nothing on a veiled stage (`Map.cs:530`).
  - A gamepad player can read "(right-click its name)" on focus but cannot act on it.
  - Fix:
    - Let activating the Veiled Play pill open the same menu, anchored to the pill. The 640 stage head could do the same.
    - This needs a small public helper in `ShieldText`, such as `RequestMenu(SpoilerKind, name, shown)` wrapping `Menu.Request`, so the target and the "this session" wording stay `ShieldText`'s own.
    - The popup's `MenuItem` is already keyboard-navigable.
    - When `NavVisible`, word the focus tooltip "…or press to reveal the place".
    - The same helper would let Tsukimichi's other placeholders become keyboard-reachable later.
- **[Minor] m19. At 640, a veiled stage's name shows the stage head's "See the stage's five levels" in the same tooltip as the shield's hover.**
  - The head's `MenuHit` covers the name (`Map.cs:530`). Its hover tooltip shows whatever the stage's state (`Map.cs:546`).
  - `ShieldPlaceholder` then calls `ShieldText.Hover` over the same pixels (`Map.cs:555`). Both go through `Theme.Tooltip`, so the two land in one tooltip: an offer the head cannot keep, above "Hidden by the spoiler shield".
  - Sealed stages already had the misleading tooltip and the hover fill, but the shield makes it contradict itself.
  - Fix: show the head's hover fill and tooltip only for Open or Done stages, and leave the veiled name's hover to `ShieldText`.
- **[Minor] m20. Two placeholders do not answer as Tsukimichi's do.**
  - **The 1280 stop tooltip.** Its first line is "Stage 9 · Endwalker area 1 · past your story · Fireball" (`Map.cs:699`), drawn in `Cream` (`Map.cs:657`).
    - `ShieldText` makes a string that holds a placeholder secondary as a whole (`ShieldText.cs:38–43`).
    - Fix: draw that line in `Ink2` when the stop is veiled.
  - **The Characters detail.** For the Moogle courier, whose stage 11 is veiled before Heavensward, it reads "Joins on The Far Shore, stage 11 · Heavensward area N" (`Characters.cs:176–179`, drawn at `:442`).
    - It has the secondary ink but no hover and no reveal.
    - Fix: call `ShieldPlaceholder` over that line when the stage is veiled. Or drop the name and say "Joins on The Far Shore, stage 11 (past your story)".
  - Neither is rendered; the renderer never hovers, and it cannot select the Moogle.
- **[Minor] m21. A veiled challenge points at a name that is not on its screen.**
  - The detail line is `MoonfallStageVeiledLine`, "(right-click its name)" (`Modes.cs:580`). The challenges screen shows level codes, not the stage's placeholder.
  - Its Veiled Play pill has no tooltip (`Modes.cs:518–519`).
  - Fix:
    - Give it its own line, such as "It runs through a stage past your story: it opens when your story gets there, or when you reveal that stage's place on the map."
    - Pass the same line as the pill's tooltip, so focus explains it.
    - Unrendered: no shipped challenge is veiled in the mock state, because "on its way" comes first.
- **[Minor] m22. At 640 the map's shield mark does not read as an eye.**
  - It is `Math.Max(r * 0.36, 5.0)` units (`Map.cs:406`), with strokes at the 1 px floor (`Map.cs:99`).
  - That makes an 8×7 px lattice: the two arcs, the pupil, the slash and the slash's dark cut merge.
  - It still differs from the padlock in hue and outline, and the panel's state line explains the selected stop. But the 640 map has no legend, so the mark has to carry itself.
  - Fix, at 640:
    - draw it at least 7 units;
    - drop the pupil and the dark cut;
    - draw the almond and the slash at about 1.5 px.

    Then compare a 16× crop with the padlock.
- **[Minor] m23. Plain board, duel, 640: the bar cuts a part mid-word.**
  - "LOUISOIX IS THINKING" now leads and the two scores take the right, so the clip at `MoonfallWindow.cs:569` cuts "Oranges 19" to "Ora" and drops ×1 (`z-plainbars.png`).
  - Five-digit scores will push the cut into "Balls 5".
  - Fix:
    - In the parts loop (`MoonfallWindow.cs:576`), draw a part only if it fits whole, and stop at the first that does not.
    - At 640, leave out the level name first: the code already sits in `stageText`.
- **[Nit]**
  - **Six "Past your story" in the 1280 veiled panel**: five rows and the pill (`far-veiled-1280.jpg`). Keep the codes and put an em dash in the name slot. Or set one centred line across the five rows, and let the pill carry the words.
  - **The veiled rows' ink.** "Past your story" in `Ink3` measures 4.3–5.6:1, with the first row lowest because the panel is lighter near its top. Sealed rows measure 5.0–6.1:1.
    - It passes as large text (24 px Jupiter, 3:1 floor), but it sits at the 4.5 line.
    - Use `Ink2` for veiled rows, or darken the top of the panel.
  - **Cut after an article**: "Above the…". `FitLine` could back off past a trailing "the", "of" or "a", giving "Above…".
  - **Duel's second locked reason** is the lowercase sub-line fragment "meet a companion first" (`Title.cs:326`), used as a tooltip. Give it a sentence: "Meet a companion in Adventure first: a duel is played against one."
  - **The 640 duel plate**: once the opponent's score has four digits, the name shows "LOUI…". This follows the level critic's four-letter rule, and the face carries it, so it is noted only.
  - **Selected list rows** still use a flat band rather than the kit's glow (carried over).

## Input rules
They hold:
- **Mouse first.**
  - Every entry is an `InvisibleButton`.
  - The placeholder is a hover-and-right-click area over drawn text, as everywhere in Tsukimichi.
- **Visible focus.**
  - The Veiled pill takes focus, shows the ring and its reason, and does nothing (`Menu.cs:449`, `:487–495`).
  - Tiles show their full names on focus.
- **Esc goes back, or pauses in play.**
  - The flow is unchanged.
  - While the shield's menu is open, `HandleKeys` returns before Back and Start (`Flow.cs:67`), so Esc closes the popup first.
- **Keys are claimed from the game.**
  - Esc is claimed off the title, as before.
  - The navigation keys are claimed over menus, the pause and the tally, and kept while held (`Flow.cs:52–65`), now with lints.
  - Every shield placeholder sits on a menu screen, so the arrows used inside its popup are claimed too.
- **A click that opens or resumes never shoots: now kept on every resume (m16).**
  - Every change of the pause re-arms the board for 0.3 s.
  - An outside press inside that time is ignored.
  - Regaining focus after an `Unfocused` pause re-arms too.
- **Holds.** Unchanged.

## Text floors (measured at 640)
**Labels (7 px cap floor): pass.**
- The placeholder "ENDWALKER AREA 3": E cap 10, small caps 8.
- The state line "Opens when your story gets there": O cap 7.
- The Veiled pill "PAST YOUR STORY": P cap 9.
- The duel plate labels "YOU" and "LOUI…": 7.
- Round-2 labels unchanged.

**Numbers (8 px floor): pass.** The stop numbers and duel scores are unchanged from round 2.

**Contrast (secondary, locked and placeholder text, 4.5:1 floor)**
| Text | Ratio |
|---|---|
| Placeholder name, 640 panel | 5.8–7.0:1 |
| Placeholder name, 1280 panel | 5.1–6.9:1 |
| "Opens when your story gets there", 640 | 5.7–6.9:1 |
| Veiled pill label, 640 | 6.0–6.8:1 |
| Veiled pill label, 1280 | 5.5–6.5:1 |
| Shield mark on the 640 pill | 10.0–10.2:1 |
| Shield mark on a 640 stop | 8.9–9.1:1 (legible as a mark, not as an eye: m22) |
| Legend "past your story", 1280 | 7.7–9.8:1 |
| Legend mark, 1280 | 9.3–11.4:1 |
| Veiled level rows (`Ink3`), 1280 | 4.3–5.6:1 (passes as large text; Nit) |
| Sealed level rows (`Ink3`), 1280, for comparison | 5.0–6.1:1 |
| "YOU" on the dimmed plate, 640 | 9.1–11.3:1 (was 3.8–4.4; m15 fixed) |
| Opponent name on the dimmed plate, 640 | 11.3–12.6:1 |
| Lit "YOU" (accent tint), 640 | 5.3–6.5:1 |

## Unverified
- **Hover, tooltips and the reveal menu in the game.**
  - The renderer keeps the mouse off-screen, so I saw neither the stop tooltip nor the shield's hover and popup.
  - I did not see the merged tooltip in m19. It comes from the code and from `Theme.Tooltip` being called twice in a frame.
  - I did not check that the popup opens where the right-click happened.
- **M4 on screen.** The mock progress cannot reach a veiled next level, so the wrong title copy is reasoned from `Continue()` and `MakeTitleWords`, not rendered.
- **Unrendered states:**
  - a veiled challenge (m21);
  - the Moogle's joins line (m20);
  - a locked Duel;
  - `scene-shown` and `scene-veiled` at 640;
  - the 640 tile tooltip on focus.
- **Whether `Keyboard.WindowHasKeys()` stays true while the shield's popup has focus.** ImGui counts popups as children for `RootAndChildWindows`, but I did not check Dalamud's build. If it were false, arrows used inside the popup could reach the game.
- **Gamepad input** reaching the game (`GameKeyClaim` clears keyboard keys only), as in round 2.
- **`FollowShield`'s cost.** It reads `Session.Spoilers.Fingerprint` every frame; I did not profile it.
- **Double-click speeds over 0.3 s.** The re-arm covers typical double clicks, but Windows lets users set up to 0.9 s; I did not test it.
- **The tests and gates.** I did not build or run them; the worktree was read-only for me.
- **Fonts and window sizes:** Dalamud's font metrics, and sizes between 640 and 1280.

