# Moonfall screens: level-design critic supervision, round 3

**Reviewed:** my round-2 review, the response to it, the committed board and HUD renders at HEAD c9547fd3 (`hud-*`, `pause-*`, `tally-*`, `duelhud-*`, `duelhud-reduce-motion-*`, `duelhud-plain-*`, `scene-veiled-1280`, `scene-shown-1280`), and the code named in the brief:
- Modes.cs: `RefreshDuelNames`, `DuelTurnText`, `DuelPlates`, `ChipSpan`, `BallsChip`, `TurnCaption`, `IdleDim`;
- MoonfallWindow.cs: `DrawPlainBar`, `RefreshPlainDuel`, `RefreshBarText`, `DrawPlay`, `FollowShield`;
- Rich.cs: `LevelPlates`, `AceChip`, `TubeLabelInk`;
- Art.cs:143–148;
- MoonfallGameArt.cs: `HidesScene`, `PickScene`, `RecipeHidden`, `VeilChanged`, `Scenes`, `Thumbs`, `BuildThumb`, `Warm`;
- Pause.cs: `RefreshPauseLine`;
- `MoonfallShield` and `MoonfallModes.SceneVeiled`.

The Release renderer's binaries are newer than every changed source file. I rendered into my scratchpad only (`…/scratchpad/lc3/`):
- duelhud, rich and plain, at 1280 and 640, at the staged moment and with `--seconds 15` (the opponent has shot; the player's turn at 9,520 to 3,190);
- duelhud with `--reduce-motion` at 640;
- base-04 with `--story 0` (veiled) and `--story 2` (shown), with otherwise identical arguments: play at 1280 and 640, play at 640 with `--reduce-motion`, pause and tally at 1280, tally at 640, and play with `--decoration off`;
- the default `hud` at both sizes, as a baseline for the margin method;
- `--text-check` on the veiled play, pause and tally screens, and on the plain duel.

The view mapping is unchanged from round 2:
- 1280: x = 132.8 + 1.268·u;
- 640: x = 26.0 + 0.735·u.

My PNG of `duelhud-plain-640` matches the committed JPEG to 0.4% of pixels, which is JPEG noise.

## Summary
**The round-2 Major is resolved.** On the plain board a duel now shows:
- whose shot it is, first and in gold ("LOUISOIX IS THINKING", "YOUR SHOT");
- both scores, at the right and outside the clip ("LOUISOIX 3,190 · YOU 9,520");
- the shooter's balls, from `TubeBalls`.

Whose shot and both scores survive at 640 in every case I projected. The player's own ball count survives on the player's turn at 640 in every case, with at least 4 px to spare. One gap remains, a Minor. At 640, during the opponent's turn, "Balls N" is set after the level's name. With a long opponent name or a 4–6-digit score it is cut to a bare "Balls" or dropped. This happens for Kan-E-Senna and the twins through most of a duel, and for Louisoix on a 16-letter level name.

All five round-2 Minors are resolved:
- **Name stub.** The name shrinks to the label floor, then cuts at a word's end, and is left out below four letters.
- **Caption past the wall.** `ChipSpan` clamps the chip to 86–716. At 640 the thinking chip now ends at u 712.9, 11.6 units inside the wall; in round 2 it ran 2.7 past.
- **Small caps.** The captions are in capitals, 7 px at 640.
- **The twins.** They have their own strings.
- **Stale cache.** The names are keyed on the duel object, and the fit on whether the fonts are ready.

**The veiled board is clean.** For base-04 at A Realm Reborn:
- the Kugane painting, its string lights, the purple frame grade and the rose window margins are all gone;
- the frame takes the Medallion palette over the plain night sky;
- the game state is identical to the shown board (7,240 points, 8 balls).

The pegs read better on the night sky than on Kugane. Every margin rises, for example orange from 0.418 to 0.482 at 1280. `--text-check` finds 0 leaks on the veiled play, pause and tally screens.

One new Minor sits outside the board but is a stale-scene path. `VeilChanged` forgets a thumbnail whose build is still running. When that build finishes, it uploads the scene thumbnail onto a level that is now veiled.

## Verdicts
| Render | Verdict |
|---|---|
| hud-1280 | APPROVE |
| hud-640 | APPROVE |
| pause-1280 | APPROVE |
| pause-640 | APPROVE |
| tally-1280 | APPROVE |
| tally-640 | APPROVE |
| duelhud-1280 | APPROVE |
| duelhud-640 | APPROVE |
| duelhud-reduce-motion-1280 | APPROVE |
| duelhud-reduce-motion-640 | APPROVE |
| duelhud-plain-1280 | APPROVE |
| duelhud-plain-640 | APPROVE (Minor listed: the opponent's balls are cut on its turn at 640) |
| scene-veiled-1280 | APPROVE |
| scene-shown-1280 | APPROVE (Nit: not the same game moment as scene-veiled) |
| veiled pause and tally, own renders (1280; tally also at 640) | APPROVE |

**OVERALL: APPROVE** (no Major remains; Minors and Nits are listed for the implementer)

## Round-2 findings
| Round-2 finding | Status | Evidence |
|---|---|---|
| [Major] The plain board's duel has no opponent score and no turn | **Resolved** (one Minor follows) | See below. |
| [Minor] The name is cut to a stub | **Resolved** | See below. |
| [Minor] The 640 thinking caption runs past the wall | **Resolved** | `ChipSpan` (Modes.cs:999–1016) shifts the chip left so that x1 ≤ 716, and `TurnCaption` and `BallsChip` both use it (1027, 1062). As rendered at 640, "LOUISOIX IS THINKING" with its dots fills u 555.1–712.9, against the wall at 724.5. At 1280 it fills u 594.8–709.1. Projected, the longest captions start further left, at about u 535 for "KAN-E-SENNA IS THINKING" and u 541 for "THE TWINS ARE THINKING". That is open sky, at least 62 units right of the launcher's x-range (327–473). |
| [Minor] The caption's small caps are 6 px | **Resolved** | The captions are upper-cased (Modes.cs:868–870). At 640 the ink spans rows 65–71 in both "LOUISOIX IS THINKING" and "YOUR SHOT" (7 px). At 1280, "YOUR SHOT" spans rows 90–99. |
| [Minor] The twins' grammar | **Resolved (code)** | Modes.cs:869–870 use `MoonfallDuelTwinsThinking` ("The twins are thinking") and `MoonfallDuelTwinsShot` ("The twins' shot"), upper-cased. The renderer cannot stage the twins. |
| [Minor] The stale name cache | **Resolved (code)** | `RefreshDuelNames` checks `ReferenceEquals(d, duelNamesFor)` and the language. On a new duel it resets both `duelHudFor` and `duelBallsFor` (Modes.cs:853–861). The fit's key now includes `fontsReady` (906–909). The plain bar's `RefreshPlainDuel` key includes the duel too (MoonfallWindow.cs:600). |
| [Nit] Top-band chips hide a high ball | Deferred to the board pass, as the response says | Unchanged: no piece comes within 14.5 units. |
| [Nit] Loader top limit | Deferred to the board pass | Unchanged. |

**The Major in detail.**
- `DrawPlainBar` (MoonfallWindow.cs:548–561) draws YOU's score and then the opponent's, right-aligned before Pause and outside the clip. It then puts `DuelTurnText` first at the left, in `Theme.Gold`.
- `ballsText` now comes from `TubeBalls` (482–495), and so does the pause line (Pause.cs:178, 187, 190).
- Rendered at 640 on the player's turn (`dp640-s15.png`, `c-dp640-s15-bar.png`), the bar's text runs are:
  - "YOUR SHOT" 8–85;
  - "3-3" 102–121;
  - "The Airship Road" 137–245;
  - "Balls 4" 262–301;
  - "Orang…" 318–358, clipped at about 360;
  - "LOUISOIX 3,190" 374–473;
  - "YOU 9,520" 490–556.
- At 1280 the whole bar shows, "×1" included.
- A click during the opponent's thought now has its explanation in gold, first in the bar.

**The name in detail.** Modes.cs:914–931 scales `px` by room/width, down to `NamePx(v, 1, Axis)` (the 7 px cap floor). It then runs `FitLine`, which cuts at a word's end first (Map.cs:871–882). A result under `MinNameLetters` (4) is left out.
- At 1280, LOUISOIX is whole at 3,190 (rendered).
- At 1280, projected: whole at five digits (shrunk to about 12.4 px) and "LOUISO…" at six.
- At 640 it is "LOUI…" at 3,190 (rendered, `c-dh640-s15-top.png`).
- At 640, projected from five digits: the room is about 26.6 px against about 27 px for "LOUI…", so the name is left out and the face ring carries the opponent. This is the fix I asked for.

## Findings
- **[Minor] Plain bar at 640: on the opponent's turn the shooter's balls are cut or gone.**
  - `DrawPlainBar` sets "Balls N" after the stage code and the level's name (MoonfallWindow.cs:576). The clip's right edge moves left as the scores grow (569).
  - I calibrated Segoe UI 17 px widths on the render: "YOUR SHOT" projects 77.0 px against 78 measured, "YOU 9,520" 66.6 against 67, and "LOUISOIX IS THINKING" 146.3 against 150.
  - Projected at 640, with "Balls N" at 40 px:

    | Opponent's turn | 0–0 | 4 digits | 5 digits | 6 digits |
    |---|---|---|---|---|
    | Louisoix, "The Airship Road" | whole | digit cut | digit cut | digit cut |
    | Louisoix, "Lantern Ring" | whole | whole | whole | digit cut |
    | Kan-E-Senna, "The Airship Road" | digit cut | gone | gone | gone |
    | Kan-E-Senna, "Lantern Ring" | whole | label only | gone | gone |
    | The twins, either level | whole | cut | cut or gone | gone |
    | Cid, any | whole | whole | whole | whole |

  - On the player's turn, "Balls N" stays whole in every case. The worst case is Kan-E-Senna at 151,600 to 161,600 on "The Airship Road": it ends at 296 against a clip at 300.
  - The Major's essentials hold: whose shot and both scores always survive. But during the opponent's thought the bar can show a bare "Balls" with no count, or no count at all.
  - Fix: put "Balls N" second, straight after the turn caption, ahead of the stage code and the level's name. This was the order in my round-2 fix. With that order, the worst case (Kan-E-Senna thinking, six digits) puts Balls at 199–239 against a clip at 300.
- **[Minor] A thumbnail building when the veil falls lands the hidden scene on a veiled level's tile.** This is outside the board and HUD, but it is the "no stale scene" question.
  - `VeilChanged` marks a now-veiled thumbnail in `thumbVeiled`. It then skips any id whose build is still running (`!build.IsCompleted → continue`, MoonfallGameArt.cs:631–634) and clears the list (650).
  - That build stays in `thumbBuilds`. When it completes, `Thumbs` uploads its pixels without asking the veil again (363–365). The tile then shows, for example, the Kugane thumbnail until the next shield change.
  - The window is narrow: the veil has to fall, not lift, which happens on a character switch, a setting change or a reveal ending, while a thumbnail is mid-build. But the miss is a spoiler.
  - The board itself is safe:
    - `Scenes` re-picks after `VeilChanged`, gets a null recipe, calls `LetSceneGo`, and abandons the uploads (869–885, 1094–1107);
    - a running board build can no longer land, because its key no longer matches (887, 932);
    - `Warm` and `BuildThumb` go through `PickScene`;
    - `palettes` are cleared.
  - Fix: in `Thumbs`, land a finished build only while `!SceneVeiled(thumbLevels[id])`, and otherwise drop it. Or keep a running build's id in `thumbVeiled` until it completes, instead of clearing it.
- **[Nit] The plain bar clips text mid-glyph** ("Orang", `c-dp640-s15-bar.png`), and a cut "Balls" can lose only its digit. Fix: draw a part only when it fits whole before `right`, and stop there.
- **[Nit] At 640, from about 10,000 points the opponent's plate carries no name.** On the player's turn the "BALLS 4" chip under it does not say whose balls they are; the face ring alone does. This is acceptable as built, as I asked in round 2. A cheap gain would be to name the opponent on that chip whenever the plate's name was left out ("LOUISOIX · BALLS 4"), since there is room up to u 716.
- **[Nit] `--text-check` does not hear the plain bar.** The sink is called only from the chrome's `DrawText` (Rich.cs:110). `DrawPlainBar` uses `dl.AddText` directly, so the plain duel's check heard only 11 strings. The source lint covers `PlayLevelName` there, but the response's "hears every string drawn" is not quite true. Fix: route the bar's text through the sink too.
- **[Nit] The committed `scene-veiled-1280` and `scene-shown-1280` are not the same moment.** One shows 7,240 points and 8 balls, the other 4,120 and 9, and the boards differ.
  - Rendered with identical arguments, they match: both 7,240 and 8 balls (`sv1280.png`, `ss1280.png`).
  - `Settle()` advances the board's clock for as many frames as the art takes to load, so the bucket's phase, and a free ball with it, varies from run to run. The same cause explains most of `hud-1280`'s pixel diff against round 2 (the bucket, one tube ball, a score digit); no peg moved.
  - Fix: re-render the pair together. Or freeze the board's clock during `Settle` so that staged renders are reproducible.

## What passed, with measurements
- **Readability margins on the night sky.** Method as in round 1: each peg's face p80 luma within 0.6 r against its ring p90 from r+2 to r+9 px, with the other pegs and the launcher masked out. The worst peg of each colour on base-04:

  | Render | Orange | Blue | Green |
  |---|---|---|---|
  | 1280, veiled (night sky) | 0.482 | 0.482 | 0.531 |
  | 1280, Kugane shown | 0.418 | 0.465 | 0.519 |
  | 640, veiled | 0.498 | 0.566 | 0.647 |
  | 640, Kugane shown | 0.383 | 0.481 | 0.524 |
  | 640, Reduce motion, veiled / shown | 0.498 / 0.383 | 0.566 / 0.481 | 0.647 / 0.524 |

  - The night sky raises every margin by 0.02–0.12, and none falls below 0.48.
  - Method check on the default hud: purple 0.437 and orange 0.347 at 1280 (round 1: 0.425 and 0.355); purple 0.422 and orange 0.353 at 640 (round 1: 0.426 and 0.353).
  - base-04 has no purple peg at this moment.
- **Veil: no picture, no stale scene.**
  - Art.cs:146–147 sets `picture = plain || hidden ? null : …`, so no interim picture stands in for a hidden scene.
  - `RecipeHidden` is picked once per level, and `VeilChanged` resets that pick.
  - `FollowShield` runs at the top of `Draw` (MoonfallWindow.cs:223), before `gameArt.Frame`, so a newly hidden scene is released before the next board is drawn.
  - The veiled pause and tally (1280, and the tally at 640) show the night sky and "The Moon Road · 1-4"; no place name appears.
  - `--text-check`: 0 leaks on the veiled play (87 strings), pause at 640 (88) and tally (269).
- **Chips and captions against the pieces and the launcher.**
  - All chips are at y 43–58. The highest piece in any level or pilot reaches y 72.5, so the clearance is 14.5 units, unchanged.
  - The launcher's pivot is at y 87 and its swing covers x 327–473.
  - Measured chip extents:

    | Chip | 640 | 1280 |
    |---|---|---|
    | "YOUR SHOT" | u 153.7–231.3 | u 165.0–221.0 |
    | Balls chip under YOU (opponent's turn) | — | u 172.1–213.9 |
    | Thinking caption | u 555.1–712.9 | u 594.8–709.1 |

  - At 640 the projected longest caption starts at about u 535. Every chip clears the launcher by at least 62 units across, and lies entirely above the pivot.
- **Floors at 640.**
  - Caption caps: 7 px.
  - The BALLS chip's label: 7 px (rows 65–71), and its count 7–8 px.
  - "LOUI…": at the AXIS floor.
  - The tube's BALLS label takes the shooter's tint: teal for Louisoix, peach for the player's side. It sits at about 0.69 luma on the navy rail (`c-tubes.png`).
- **The plain duel at 1280.** The full bar fits, with "×1" and about 520 px to spare.
- **No regressions on the plain or rich board.**
  - The Ace chip is still hidden in duels (Rich.cs:580).
  - The pause line counts the shooter's balls.
  - `hud-640`, `pause-640` and `tally-640` are byte-identical to round 2.
  - `tally-1280` is pixel-identical (0 differing pixels).
  - `pause-1280` differs only in its footer line.
  - `duelhud-640` differs only in the caption row.

## Unverified
- **Fonts and scale.** The plain bar's widths at 640 are projected from Segoe UI 17 px, calibrated to within 4 px on the render. Dalamud's real font and a global font scale other than 1 are unverified. The window's minimum size scales with `UiScale`, so the ratios should hold.
- **Unstaged opponents and scores.** The renderer stages only Louisoix, at 0–0 thinking and 9,520 to 3,190 on the player's turn. The following are projected, not rendered: Kan-E-Senna, the twins, Cid, 5- and 6-digit scores, the name drop at 640, and the "LOUISOIX'S SHOT" state.
- **The thumbnail race.** From code; not reproduced.
- **A live shield change while the board is open** (veil laid or lifted mid-level): from code (`FollowShield`, then `VeilChanged`, then `Scenes`); not exercised.
- **Not rendered at all:**
  - the duel tally and the plain duel tally;
  - other veiled scenes (lantern-night is the only scene tagged after A Realm Reborn);
  - a Far Shore level, which cannot be played while veiled;
  - in-game rendering through Dalamud.
- **The owner's rule on names.** No place name reaches the HUD, pause or tally on a scene-only veil. But base-04's own level name, "LANTERN RING", still shows there, as `moonfall-modes.md:68` specifies: only a veiled stage hides its levels' names. If the owner meant "its name" to include a level whose scene alone is veiled, `PlayLevelName` (Map.cs:68) would also need `SceneVeiled`. I read the rule as covering only place names, so I did not raise this as a finding.

*Process note: everything was rendered and measured in my scratchpad (`…/scratchpad/lc3/`). `git status` in the worktree is clean at c9547fd3.*
