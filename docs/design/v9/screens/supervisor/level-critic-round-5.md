# Moonfall screens: level-design critic supervision, round 5

**What I reviewed** (HEAD 8c51d4d8):
- My round-4 review and the response to it.
- The committed board and HUD renders: `hud-*`, `pause-*`, `tally-*`, the new `tally-road-waits-*` and `tally-road-waits-plain-*`, `duelhud-*`, `duelhud-reduce-motion-*`, `duelhud-plain-*`, `scene-veiled-*` and `scene-shown-*`.
- The code named in the brief:
  - Tally.cs: `tallyNoteOnWin` and the sub-line;
  - Board.cs: `DrawEnd`;
  - MoonfallWindow.cs: `DrawPlainBar` and the sink;
  - MoonfallGameArt.cs: `thumbChecked`, `Thumbs`, `VeilChanged`;
  - scene-recipe.md § Fallbacks;
  - the new test in MoonfallSceneStartTests.cs;
  - the renderer's `--leave-one`.

**Renderer build.** The Release renderer's binaries (Tsukimichi.dll 04:56:11, Tsukimichi.Core.dll 04:47:29) are newer than every changed source file (newest: Board.cs 04:50:49; MoonfallGameArt.cs 04:46:34).

**What I rendered** (into my scratchpad only, `…/scratchpad/lc5/`):
- All twenty committed board, HUD and road-waits renders, at both sizes, each with `--text-check`.
- The last-level note: `--far-built --far-walk --leave-one --story 5`, rich and plain, at 1280 and 640.
- The plain solo bar at 640, and as a large-font proxy at 420, 360, 330 and 300 px wide.
- `--alloc` on 17 screens. The plain tallies were also run at 1,800 frames.

**Test run.** I ran `dotnet test Tsukimichi.Tests -c Release --filter FullyQualifiedName~MoonfallSceneStartTests`: 5 passed, 0 failed. This rebuilt the test project's gitignored bin/obj; `git status` is clean.

## Summary
**The round-4 Minor is resolved.**
- A win with no Next now says why, in both tallies:
  - The rich tally draws the note in place of the sub-line (Tally.cs:219).
  - The plain tally adds a line after the rows and grows by 1.4 lines to fit (Board.cs:600, 625–628).
- Both notes are staged and rendered:
  - "Stage 9 is set past your story: Map opens on it." (`tally-road-waits-*`);
  - "That was the last level for now: more are on the way." (my `last-*` renders).
- Neither note touches the buttons.
- At 640 the rich note is small but legible: 12 px nominal, about 6.6:1 against the panel.

**The round-4 Nits:**
- **Resolved:** the per-frame re-pick, the `SafeRecipe` docs, and the text sink on the bar.
- **Resolved, with a caveat:** the thumbnail race now has a test, and it passes here. Without a game install it cannot fail (Nit below).
- **Partly resolved:** the plain solo bar. The name now gives way only when it alone overflows. When it fits, it still crowds out Oranges and the multiplier (Nit below).

**No regressions on the board or HUD.**
- The sixteen pre-existing board and HUD JPEGs are byte-identical to round 4's; none is in the commit's diff.
- My fresh renders of every board render match the committed JPEGs to within 0.3–4.5 px at 8% fuzz.
- The board allocates 0 bytes per frame in steady state on every solo screen. That includes the new road-waits tally, rich and plain.

## Verdicts
| Render | Verdict |
|---|---|
| hud-1280 | APPROVE |
| hud-640 | APPROVE |
| pause-1280 | APPROVE |
| pause-640 | APPROVE |
| tally-1280 | APPROVE |
| tally-640 | APPROVE |
| tally-road-waits-1280 (new) | APPROVE |
| tally-road-waits-640 (new) | APPROVE |
| tally-road-waits-plain-1280 (new) | APPROVE |
| tally-road-waits-plain-640 (new) | APPROVE |
| duelhud-1280 | APPROVE |
| duelhud-640 | APPROVE |
| duelhud-reduce-motion-1280 | APPROVE |
| duelhud-reduce-motion-640 | APPROVE |
| duelhud-plain-1280 | APPROVE |
| duelhud-plain-640 | APPROVE |
| scene-veiled-1280 | APPROVE |
| scene-veiled-640 | APPROVE |
| scene-shown-1280 | APPROVE |
| scene-shown-640 | APPROVE |
| Last-level tally, rich and plain, 1280 and 640 (own renders) | APPROVE |

**OVERALL: APPROVE.** No Major or Minor remains; four Nits are listed for the implementer.

## Round-4 findings
| Round-4 finding | Status | Evidence |
|---|---|---|
| [Minor] The tally's veil note is never drawn | **Resolved** | See below. |
| [Nit] The thumbnail race fix has no test | **Resolved, with a caveat** | `A_thumbnail_the_veil_overtakes_mid_build_is_never_landed_and_is_rebuilt_story_safe` (MoonfallSceneStartTests.cs:130–161) passes. It discriminates only when the game is installed; see the Nits. |
| [Nit] `Thumbs` re-picks the scene every frame on the failure path | **Resolved** | The compare is gated by `thumbChecked.Add(id)` (MoonfallGameArt.cs:365), so it runs once per finished build. `VeilChanged` clears the set (676), so a build the veil overtook is still compared once. A pixel-less build now costs nothing per frame. Correctness holds: any veil change clears the set, so a build that straddles one is always compared. |
| [Nit] `SafeRecipe` keeps the dress; only the picture is tested | **Resolved (docs)** | scene-recipe.md:232–236 now says the dress (paint, framing, lights, palette regions, motion) survives the fallback, so a hideable recipe's dress must name no place. This is a convention rather than a test, which is the option I offered. |
| [Nit] Plain solo bar: a long name takes the game state with it | **Partly resolved** | MoonfallWindow.cs:603–608: in a level, a name that does not fit is skipped and the run continues. A name that does fit still takes the room, and Oranges and ×N then drop. See the Nits. |
| [Nit] The solo score and the Pause label bypass the sink | **Resolved** | MoonfallWindow.cs:554 and 582. The plain duel at 640 now reports 28 strings, up from 27, because of the Pause label. The plain tally still bypasses the sink; see the Nits. |

**The note in detail.**
- **Where it is set.** `PrepareTally` sets `tallyNoteOnWin` in two places:
  - the road-waits branch (Tally.cs:132–139), which now takes its stage from `modes.Next()`, as the title does;
  - the last-level branch (141–145).
  - Both branches need `won` and Adventure; Quick Play, duels and challenges never set it.
- **Rich tally.** It draws `rows > 0 && !tallyNoteOnWin ? tallySub : tallyNote` (Tally.cs:219), in the sub-line's slot under the level's name.
  - What the note displaces is still on screen: the balls left are in the third row ("Balls left 11 × 10,000"), and the code shows on the header plate ("11-5 First Light").
- **Plain tally.** It sizes the panel with the same flag (Board.cs:600) and draws the note 0.4 line below the total (625–628).

Measured on my renders, ink extents, luma threshold 120:

| Render | Note's ink | Height | Note → buttons |
|---|---|---|---|
| Rich, 1280 | x 518–763 (246 px wide) | 11 px | The note is in the header, about 300 px above the buttons |
| Rich, 640 | x 216–423 (208 px wide) | 9 px; 12 px nominal (15 units × 0.8) | In the header; nowhere near the buttons |
| Plain, 640 | x 126–426 | 13 px | Ink ends at 324; the button labels begin at 349, a 25 px gap |
| Plain, 1280 | x 447–747 | 13 px | Ink ends at 484; the button labels begin at 509, a 25 px gap |

- **Rich panel position.** The note sits where round 4's sub-line sat: about 7 px above the rule, with the crest's top about 4 px below the descenders. No new collision.
- **Plain panel fit.**
  - The veil note leaves about 88 px spare inside the panel's padding at both sizes.
  - The longer last-level note (338 px) leaves about 50 px spare at 640 (`c-last640.png`).
- **Contrast.** Ink p90 against the panel's median: rich 181/46 luma, about 6.6:1; plain 166/36, about 6.4:1.
- **Legibility at 640.** At 1:1 and at 4×, every glyph of the rich 640 note is distinct (`c-notesub640.png`). It is the smallest text on the panel, at the same size as round 4's approved sub-line.

## Findings
- **[Nit] Plain solo bar: a name that fits still crowds out the game state.**
  - MoonfallWindow.cs:589–616: in a level the order is code, name, Balls, Oranges, ×N. Only a name that overflows on its own is skipped.
  - My narrow renders stand in for a font larger than the UI scale; the window's minimum is 640 logical, MoonfallWindow.cs:44. In them, "The Airship Road" is kept while the state drops:
    - at 420 and 360 px, "Balls 10" shows but Oranges and ×1 are lost (`c-bars.png`);
    - at 330 and 300 px, every state part is lost (`c-bars-narrow.png`).
  - At 640 with the shipped names, about 154 px stays spare (`c-bar640.png`), so this does not happen today.
  - Fix: in a level, measure the state parts first and draw the name only if it and all of them fit. Or order the parts like the duel: code, Balls, Oranges, ×N, then name.
- **[Nit] The race test cannot fail without the game install.**
  - The fake's `ReadGameTexture` calls `MoonfallSceneKit.GameTexture`, which returns null without the game (MoonfallSceneKit.cs:55–60). The Kugane build then falls back to the same `recipe.Fallback` picture.
  - So `Assert.Contains(recipe.Fallback!, host.Pictures)` (MoonfallSceneStartTests.cs:160) and `Assert.Single(...)` would both pass even if the overtaken build landed. On this machine the game is present, so the run here was meaningful; on a machine without it, it proves nothing.
  - Fix: make the fake's game-texture read return a sentinel image (for example solid magenta) that does not depend on the install. Have `Upload` record a hash or sample of each thumbnail's pixels, and assert that the landed thumbnail has none of the sentinel.
- **[Nit] The road-waits tally focuses Replay, while its note points at Map.**
  - On a win with no Next, Replay takes the focus (Tally.cs:304). Enter or A replays the level just won, though the note says "Map opens on it".
  - Fix (UX or owner's call): when `tallyVeil` is set, focus Map instead (`focus: tallyVeil is not null` on 316, and its negation on 304).
- **[Nit] The plain tally's strings bypass the text sink.**
  - Board.cs:610–653 draws the banner, the rows, the new note and the button labels with `dl.AddText` and `ImGui.Button`, not through `TextSinkForRender`.
  - The note carries only a stage number, so no leak is possible today. But `--text-check` on the plain tally does not hear it.
  - Fix: invoke the sink for `tallyBanner`, `tallyNote` and the labels in `DrawEnd`.

## What passed, with measurements
- **The road-waits staging is honest.**
  - `--leave-one` (Program.cs:171–177) removes the walk's last won level and pulls `ExpansionCleared` back to it, so the tally really wins 11-5 and then finds the road waiting.
  - With `--story 3` the text check reports 3 stages veiled and 0 leaks: 210 strings at 1280, 204 at 640, and 170 and 163 for plain.
  - The note names stage 9, the stage `modes.Next()` and the title name (m9). Whether "stage 9" reads well right after winning stage 11 is the game designer's to judge, not a board finding.
- **The last-level note.** At `--story 5` the walk reaches 12-5. The tally shows "That was the last level for now: more are on the way." in place of the sub-line in the rich tally, and as its own line in the plain one. It is laid out like the veil note (`c-last1280.png`, `c-last640.png`).
- **Byte-identical board renders.** The committed `hud-*`, `pause-*`, `tally-*`, `duelhud-*` and `scene-*` JPEGs are not in the 8c51d4d8 diff, so they are round 4's files.
- **My re-renders against the committed JPEGs, at 8% fuzz** (differing pixels at 1280 / 640):

  | Render | 1280 | 640 |
  |---|---|---|
  | hud | 4.5 | 3.6 |
  | pause | 2.8 | 2.7 |
  | tally | 2.1 | 3.0 |
  | duelhud | 3.6 | 2.6 |
  | duelhud-reduce-motion | 3.7 | 2.6 |
  | duelhud-plain | 2.5 | 2.5 |
  | scene-veiled | 3.6 | 2.8 |
  | scene-shown | 3.7 | 3.0 |
  | tally-road-waits | 1.8 | 1.8 |
  | tally-road-waits-plain | 0.3 | 0.5 |

- **Text check on the board.**
  - 0 leaks on every render.
  - The veiled base-04 board hears 103 strings at 1280 and 91 at 640, with 8 stages veiled.
  - The plain solo bar hears 36 strings at 640.
- **Per-frame allocation** (`--alloc`, 600 frames after 2 s; bytes on the drawing thread):

  | Screen | Bytes |
  |---|---|
  | play, 1280 | 0 |
  | play, 640 | 0 |
  | pause, 1280 | 0 |
  | tally, 1280 | 0 |
  | tally, 640 | 0 |
  | road-waits tally, rich, 1280 | 0 |
  | road-waits tally, rich, 640 | 0 |
  | plain solo, 640 | 0 |
  | veiled base-04, 1280 | 0 |
  | shown base-04, 1280 | 0 |
  | rich duel, 1280 | 2,672 |
  | plain duel, 640 | 4,112 |
  | levels at `--story 0` (thumbnails) | 0 |
  | title at `--story 0` (thumbnails) | 0 |
  | Quick Play at `--story 0` (thumbnails) | 88 |

  - The duel figures are unchanged from round 4: event-driven count-up and caption changes.
  - The plain road-waits tally measured 512 bytes at 600 frames and 512 again at 1,800. It is a one-off settle, not per frame.
  - The plain Moon Road tally measured 5,584 at both 600 and 1,800 frames, also a one-off; the last-level one, 32 bytes at 1,800.
  - The "~KB" lines the allocation listener prints are background scene builds, not the drawing thread.
- **Thumbnail check, from code.** I traced the full lifecycle in `Thumbs` (MoonfallGameArt.cs:361–383, 671–691). These cases are all correct:
  - a build overtaken mid-flight (compared once after the veil clears the set);
  - one finished before `VeilChanged` (dropped there);
  - one finished without pixels (checked once, then idle);
  - a rebuild with no veil change (no compare needed).
- **The plain bar's `continue`.** It identifies the name with `ReferenceEquals(part, levelName)`. Empty parts are skipped first, so the name cannot alias the code. In a duel, a name that does not fit still ends the run, and the name is last there anyway.

## Unverified
- **In-game.** Dalamud's real font and a global font scale other than 1. My narrow-width renders stand in for a larger font; they are not the real thing.
- **Gamepad and keyboard focus on the tally.** The Replay focus is read from code (Tally.cs:304, 316) and the focus ring in `tally-road-waits-1280`, not driven by input.
- **The race test on a machine without the game.** That it cannot fail there is reasoned from MoonfallSceneKit.cs:55–60; I did not run it without an install.
- **A shield change while the tally is up.** `PrepareTally` caches per game and language (Tally.cs:66–76). A reveal while the tally shows would not refresh the note. I found no way to open the reveal menu over the tally, so this is unstaged.
- **Other locales.** The notes are not wrapped. In English the longest leaves about 50 px inside the plain panel at 640; translations are frozen and not checked.
- **Not covered:** GPU texture paths and live rendering through Dalamud.

*Process note: every render, crop (`c-bars.png`, `c-bars-narrow.png`, `c-bar640.png`, `c-notesub640.png`, `c-last1280.png`, `c-last640.png`) and script (`ink.py`) is in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/lc5/`. Nothing in the worktree was edited, staged or committed. The filtered test run rebuilt only gitignored bin/obj output, and `git status` is clean at 8c51d4d8.*
