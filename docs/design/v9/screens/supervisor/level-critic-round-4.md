# Moonfall screens: level-design critic supervision, round 4

**Reviewed:** my round-3 review, the response to it, the committed board and HUD renders at HEAD e0db5d19 (`hud-*`, `pause-*`, `tally-*`, `duelhud-*`, `duelhud-reduce-motion-*`, `duelhud-plain-*`, `scene-veiled-*`, `scene-shown-*`), and the code named in the brief:
- MoonfallGameArt.cs: `PickScene`, `SafeRecipe`, `SceneVeiled`, `VeilChanged`, `Thumbs` (`thumbBuiltWith`), `BuildThumb`, `Warm`;
- MoonfallModes.cs: `SceneHide`, `NextLevel`;
- MoonfallWindow.cs: `DrawPlainBar`, `RefreshPlainDuel`, and `HoldBoardForRender` in `DrawPlay`;
- Art.cs:143–148;
- Flow.cs: `AdventureNext`, `HasNext`, `NextLevelToWarm`, `LeaveBoard`;
- Tally.cs: `PrepareTally`, `TallyRows`, and the sub-line and note drawing;
- Board.cs: the plain tally;
- the renderer's `Settle` and `HoldRelease`.

The Release renderer's binaries (4:10:49) are newer than every changed source file (newest 4:10:39). I rendered into my scratchpad only (`…/scratchpad/lc4/`):
- base-04 at `--story 0` (fallback) and `--story 2` (Kugane shown), at 1280 and 640, each twice, with `--text-check`;
- base-04 at 640 with `--reduce-motion`, in both states;
- base-04 veiled, with `--moment tally` at both sizes and with `--decoration off` at 640;
- the plain duel at 640 and 1280, at the staged moment and with `--seconds 15`, with `--text-check`;
- the plain solo board at 640;
- the twelve committed board renders, re-made at both sizes;
- `--alloc` on play, pause, tally, the veiled and shown base-04, the rich and plain duels, and the plain solo board. As a thumbnail check, also on levels, title and Quick Play at `--story 0`.

## Summary
**All four round-3 Minors and Nits I asked about are resolved:**
- **The plain bar's balls on the opponent's turn.** "Balls N" is now second, after the turn. Projected across every opponent and score width at 640, it is never dropped. The worst case is Kan-E-Senna thinking at six digits, where turn and Balls show with 48 px spare.
- **The thumbnail race.** `Thumbs` records each build's recipe name. It compares that against today's pick when the build completes, and never lands a stale one.
- **`--text-check` and the plain bar.** It now hears the bar: 27 strings on the plain duel at 640, against 11 in round 3.
- **Reproducible staged renders.** Two runs of each scene render are pixel-identical (0 differing pixels at both sizes, both states). All twelve committed board renders match my re-renders to within 2–5 pixels at 8% fuzz.

**The fallback scene on base-04 is clean and story-safe.**
- It shows Moonfall's own sky painting: purple sky, low generic hills and a teal lowland haze. No place is depicted.
- The level's own dressing is kept: two lantern strings hung from the walls, the moon glow from the upper right, and mist.
- It is the same moment as the shown board: 5,760 points, 9 balls, 15 oranges, the same 44 pegs.
- Readability rises well above the shown board's: worst orange 0.474 against 0.365 at 1280, and 0.453 against 0.318 at 640. Every colour is at least 0.45 on the fallback.
- The board allocates 0 bytes over 600 frames on play, pause, tally and the veiled scene.

**One new Minor, on the tally.** The response says that after a win with no Next because of the veil, the tally "says 'Stage 12 is set past your story: Map opens on it.'". It never does: the note is computed, but on a won level the tally draws the sub-line instead. The Map button does open on the stage.

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
| duelhud-plain-640 | APPROVE |
| scene-veiled-1280 | APPROVE |
| scene-veiled-640 | APPROVE |
| scene-shown-1280 | APPROVE |
| scene-shown-640 | APPROVE |
| veiled base-04 tally (1280, 640), plain, and Reduce motion (own renders) | APPROVE |

**OVERALL: APPROVE** (no Major remains; one Minor and some Nits are listed for the implementer)

## Round-3 findings
| Round-3 finding | Status | Evidence |
|---|---|---|
| [Minor] Plain bar at 640: the opponent's balls are cut on its turn | **Resolved** | See below. |
| [Minor] Thumbnail race: a build finishing after the veil falls lands the hidden scene | **Resolved (code)** | See below. |
| [Nit] The plain bar clips mid-glyph | **Resolved** | A part is drawn only if `x + w <= right` (MoonfallWindow.cs:594); otherwise the loop stops. No clip rect remains. In every render the last part is whole ("3-3" at 640; "The Airship Road" at 1280). |
| [Nit] `--text-check` does not hear the plain bar | **Resolved** | Both scores (MoonfallWindow.cs:563, 567) and every part (≈599) go to the sink. The plain duel at 640 now reports 27 strings, against 11 in round 3. The solo score and the Pause label still bypass it; see the Nits. |
| [Nit] scene-veiled and scene-shown are not the same moment | **Resolved** | See below. |
| [Nit] At 640, the BALLS chip does not name the opponent once the plate drops its name | Open; acceptable as built | Unchanged. The response does not take it up. |

**The plain bar in detail** (MoonfallWindow.cs:581–601). In a duel the order is turn (gold, `i == 0`), Balls, Oranges, multiplier, code, then name.

Measured text runs at 640, as ink columns:

| Render | Runs |
|---|---|
| Opponent's turn, 0–0 (`dp640.png`) | "LOUISOIX IS THINKING" 9–155, "Balls 5" 173–211, "Oranges 19" 229–301, "×1" 319–331, "3-3" 350–369 \| "LOUISOIX 0" 428–500, "YOU 0" 517–556, Pause from 584 |
| Player's turn, 9,520 to 3,190 (`dp640-s15.png`) | "YOUR SHOT" 8–85, "Balls 4" 102–141, "Oranges 17" 158–230, "×1" 248–260, "3-3" 279–298 \| "LOUISOIX 3,190" 374–473, "YOU 9,520" 490–556 |

- In both, the level's name is left out whole; the crop is `c-bars640.png`.
- At 1280 the whole bar shows, with the name, on both turns (`c-bars1280.png`).

**Projected cases.** I calibrated Segoe UI at 14 px, with widths scaled by 1.03, against the runs above. "LOUISOIX IS THINKING" projects to 148 px against 147 measured, "YOUR SHOT" to 78 against 78, and "YOU 9,520" to 70 against 67. The projection gives the same five parts as both renders. I projected Louisoix, Kan-E-Senna, the twins and Cid, at 0, 4, 5 and 6 digits, on the opponent's thinking turn, the opponent's shot and the player's turn. I used "Balls 10", "Oranges 25" and "×10" as the widest values. The result:
- **Balls survives in all 48 cases.**
- The tightest is Kan-E-Senna thinking at 6 digits: turn and Balls show, with 47.6 px spare.
- The twins at 6 digits, on the player's turn, show five parts with 0.5 px spare; the name is the part that drops.

**The thumbnail race in detail.**
- `Thumbs` stores `thumbBuiltWith[id] = PickScene(level)?.Name` when a build starts (MoonfallGameArt.cs:376).
- When a build completes, `Thumbs` compares that name with today's `PickScene` and drops the build if they differ (363–367); the thumbnail is then rebuilt.
- `VeilChanged` drops completed or landed thumbnails the same way (672–691), abandoning an in-flight upload or releasing a landed texture.
- The fallback's safe recipe has its own name ("lantern-night~safe"). So its board key, warm key and thumbnail cache key (405, 549, 893) can never reuse a Kugane build, or the reverse.
- Not reproduced at runtime, and no test covers it (Nit below).

**Reproducibility in detail.**
- `Settle` sets `HoldBoardForRender` and releases it through `HoldRelease` (Program.cs:213–225, 469–472).
- `DrawPlay` skips the flippers, `Advance` and `boardClock` while it is held (MoonfallWindow.cs:293).
- Results:
  - `sv1280a`/`b`, `ss1280a`/`b`, `sv640a`/`b` and `ss640a`/`b` each differ by 0 pixels.
  - My PNGs against the committed JPEGs at 8% fuzz: scene-veiled 3.6 and 3.0 px, scene-shown 3.7 and 2.8 px (1280, 640).
  - The veiled and shown boards are the same moment: 5,760 points, 9 balls, 15 oranges, ×2 and the same 44 peg centres. The plain board at 640 shows the same numbers too.

## Findings
- **[Minor] Tally: the veil note is never drawn, so a win with no Next does not say why.**
  - On an Adventure win where the road waits at a veiled stage, `PrepareTally` sets `tallyVeil` and `tallyNote = "Stage {0} is set past your story: Map opens on it."` (Tally.cs:129–133).
  - The rich tally draws `var sub = rows > 0 ? tallySub : tallyNote;` (Tally.cs:211). `TallyRows` is 3 or 4 for every won level outside a duel or challenge (Tally.cs:59–60), so a win always shows `tallySub` ("The Moon Road · 3-3 · balls left: 13"), as `tally-1280` shows.
  - The plain tally has the same branch (Board.cs:611–626).
  - The note is set only when `won` is true, so it can never appear. The same was already true of the existing `MoonfallLastLevel` note (Tally.cs:136).
  - The player sees Replay and Map, no Next and no reason. Map does open on the waiting stage (`LeaveBoard`, Flow.cs:358–366; `Leave` goes to the mode's home), and that stage explains itself on the map. So the cost is one confusing screen, not a dead end. But the response's claim does not hold.
  - Fix: when `tallyVeil` (or the last-level note) is set on a win, draw `tallyNote` as the sub-line in place of `tallySub`; the balls-left count is already in the rows. Or add it as a line above the buttons. Make the same change in the plain tally. Add a renderer flag to stage it (for example, `--far-built --far-won N --story S --screen tally`) so the next round can see it.
- **[Nit] The thumbnail race fix has no test.**
  - `MoonfallSceneStartTests` already drives `art.Thumb` and `art.Menu()` with a fake host (lines 106–117).
  - Fix: add a case with a gated build. Start a thumbnail with the scene shown, flip `HidesScene` to Fallback, call `VeilChanged`, then release the build. Assert that no "Moonfall thumbnail" upload carries the Kugane pixels, and that the rebuilt thumbnail came from "lantern-night~safe".
- **[Nit] `Thumbs` re-picks the scene every frame for a completed build that yielded no pixels.**
  - The completed-build check (MoonfallGameArt.cs:363) runs every frame while `upload is null`. A build whose painting and fallback picture both failed, or which faulted, never gets an upload, so `PickScene` and `MoonfallSceneRecipeLoader.Pick` run each frame. `Pick` sorts with `OrderBy` (MoonfallSceneRecipeLoader.cs:128), which allocates.
  - It is reachable only on that failure path. A veiled stage's levels are never asked for thumbnails, because only reached levels are; levels, title and Quick Play at `--story 0` measured 0, 0 and 88 bytes over 600 frames.
  - Fix: compare only when the veil has changed since the build started (a veil epoch stored next to `thumbBuiltWith`), or drop a pixel-less completed build into a "failed" set.
- **[Nit] `SafeRecipe` keeps a recipe's paint, framing, lights and palette regions, and only the picture is tested to be placeless.**
  - The shield test checks that `recipe.Fallback` has no zone (MoonfallShieldTests.cs:331–336). Today only lantern-night can fall back: holy-see is A Realm Reborn and airship-road has no place. Its kept dressing is generic: two cords with six lanterns, a moon glow, shafts, mist, and a teal band that reads as haze over the hills.
  - A future recipe whose framing draws a landmark silhouette would carry it onto the "story-safe" picture.
  - Fix: say in the recipe format docs that framing and paint survive the fallback. Or let a recipe declare `fallbackKeeps` (or strip framing shapes outside a generic set) in `SafeRecipe`.
- **[Nit] The plain solo bar stops at the first part that does not fit, so a long level name would take the game state with it.**
  - In a level the order is code, name, Balls, Oranges, multiplier, and the loop `break`s (MoonfallWindow.cs:594). With the shipped names ("The Airship Road" is 109 px) and a six-digit score, about 154 px is spare at 640, so this does not happen today.
  - Fix: in a level, `continue` past a name that does not fit, or order it like the duel (state first, name last).
- **[Nit] The solo score and the Pause label still bypass the text sink.** They are a number and a fixed label, so no leak is possible. But "hears every string drawn" is still not literally true. Fix: route them through the sink too.

## What passed, with measurements
- **Readability margins over the fallback picture.** Method as in round 1: each peg's face p80 luma within 0.6 r, against its ring p90 from r+2 to r+9 px, with the other pegs and the launcher masked out. I segmented the pegs as the pixels identical between the veiled and shown renders of the same moment. That gave all 44 pegs: 26 blue, 15 orange (matching the HUD's 15), 2 green and 1 purple, radius 11.5–12 px at 1280 and 6.5–7 px at 640. The worst peg of each colour on base-04 at 5,760:

  | Render | Orange | Blue | Green | Purple |
  |---|---|---|---|---|
  | 1280, fallback | 0.474 | 0.540 | 0.588 | 0.463 |
  | 1280, Kugane shown | 0.365 | 0.370 | 0.497 | 0.462 |
  | 640, fallback | 0.453 | 0.500 | 0.573 | 0.452 |
  | 640, Kugane shown | 0.318 | 0.363 | 0.420 | 0.456 |
  | 640, Reduce motion, fallback / shown | 0.453 / 0.318 | 0.500 / 0.363 | 0.573 / 0.420 | 0.451 / 0.455 |

  - On the fallback every colour is at least 0.45, above the default board's baseline (orange 0.347–0.355) and far above the 0.283 accepted in runtime round 1.
  - The shown Kugane board's 0.318 at 640 is lower than round 3's figure (0.383), because the peg layout differs at this moment. It still clears the runtime round's accepted worst (0.283), and this round did not change it.
- **No place depicted.** The fallback is `moon-road-night.png`, Moonfall's own painting, tagged `Nowhere` (MoonfallPlaces.cs:90). It is drawn whole and ungraded, as the builder draws a missing painting: `Source = Picture`, `Grade = null` (MoonfallGameArt.cs:631–650); `Cut` with no crop (MoonfallSceneBuilder.cs:320). The crops `c-sv1280-board.png` and `c-sv-topbottom.png` show purple sky, distant soft hills, teal haze and mist; no buildings, canal, bridges or silhouettes.
- **The fallback builds cleanly.** There are no seams, banding or tiling at 1:1, at the top band, the hills or the bucket rail. It builds at both tiers (`moon-road-night.png` and `@2x` ship). The renderer printed no warnings.
- **The dressing on a placeless sky is appropriate.** The lanterns are the level's identity ("Lantern Ring"), not Kugane's. The cords start at the walls (u 70 and 730), so they read as strung from the frame and need no building to hang from. The six lights sit at v 61–73, at least 14 units above the highest piece's reach. The rest is generic: the moon glow from the upper right, the shafts, and the teal band that reads as lowland haze. The frame keeps lantern-night's plum chrome, which names no place.
- **The veil's three states.** `SceneHide`: a veiled stage gives Hidden, a hidden scene place gives Fallback, otherwise Shown (MoonfallModes.cs:185–190).
  - `PickScene` returns null for Hidden, so `RecipeHidden` is true and Art.cs:147 draws no picture (the bare night sky).
  - For Fallback it returns the safe recipe, and `hidden` is false.
  - If the safe build fails, `picture = recipe.Fallback`, which is null on the safe recipe, so the bare sky shows. No Kugane path is left.
  - The plain board draws no scene, at the same moment (`hp640-04.png`).
- **The text check on the veiled screens.**
  - 0 leaks on veiled play (102 strings at 1280, 90 at 640) and the veiled tally (272 and 266).
  - Kugane is in the hidden list at `--story 0`, because stage 8 is in Kugane (MoonfallPlaces.cs:62).
  - The veiled tally reads "Lantern Ring · The Moon Road · 1-4 · balls left: 12". Its sum is 27,000 + 10,000 + 120,000 + 25,000 = 182,000, as shown.
- **Per-frame allocation (600 frames after 2 s):**

  | Screen | Bytes |
  |---|---|
  | play, 1280 | 0 |
  | play, 640 | 0 |
  | pause, 1280 | 0 |
  | tally, 1280 | 0 |
  | tally, 640 | 0 |
  | veiled base-04, 1280 | 0 |
  | shown base-04, 1280 | 0 |
  | plain solo, 640 | 0 |
  | rich duel, 1280 | 2,672 |
  | plain duel, 640 | 4,112 |

  - The duels' bytes are the score count-up and caption changes as the opponent shoots: under 7 B a frame on average, and event-driven.
  - The plain bar's `ReadOnlySpan<string>` collection literal does not allocate (plain solo: 0).
- **`AdventureNext` and its cached warm level.**
  - The key is `(campaign, levelIndex, progressEpoch)`, and a shield change bumps `progressEpoch` (MoonfallWindow.cs:157), so the cache cannot go stale.
  - `NextLevelToWarm` is gated on `HasNext`, which needs a win, so no warm-ahead is lost during play.
  - The warmed level goes through `PickScene`, so a fallback level warms its safe recipe.
  - `NextLevel` steps over Veiled slots and returns the first reached one.
- **No regressions on the board or HUD.** The round-3 JPEGs against the new ones:
  - `hud-*`, `pause-*` and `tally-*` differ only because the held clock gives a new staged moment. The bucket's phase changed, 8 or 9 balls became 10, and 3,400 became 2,800 on the HUD. The tally now reads balls left 13 and a total of 171,600 (6,600 + 10,000 + 130,000 + 25,000); no peg moved (`c-hud-bottoms.png`, `c-tally-3.png`).
  - The HUD, pause line and tally now agree on the ball count.
  - `duelhud-*` differs only in the moment; the caption, BALLS chip and plates are unchanged (`c-hud-duel640.png`).
  - The Ace chip is still hidden in duels.

## Unverified
- **The tally's veil note in a render.** It cannot be staged: the renderer's tally is The Moon Road's mock, and the Far Shore cannot be won into a veiled stage there. The Minor above is from code; the sub-line branch is confirmed by `tally-1280`.
- **A veiled stage's bare night sky on the board.** It cannot be rendered, because a veiled stage is not playable. It is verified from code (`SceneHide` returns Hidden, so `PickScene` returns null and `picture` is null).
- **The thumbnail race and a live shield change mid-level.** From code only; not reproduced.
- **Fonts and scale.** The plain bar's widths at 640 are projected from Segoe UI. My calibration matches the renders to within 3 px, and the projection reproduces both rendered bars. Dalamud's real font, a global font scale other than 1, and the other opponents (Kan-E-Senna, the twins, Cid) and 5–6-digit scores are projected, not rendered.
- **Margin method.** Peg segmentation here is by pixels identical between the veiled and shown renders, not by fitting the level's coordinates as in round 1. Both find the same 44 pegs. The absolute values may differ from round 3's by a few hundredths, and round 3 was a different moment, so compare within a row rather than across rounds.
- **Not covered:** in-game rendering through Dalamud, GPU texture paths, and other locales.

*Process note: everything was rendered and measured in my scratchpad (`C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/lc4/`): the renders, the crops `c-bars640.png`, `c-bars1280.png`, `c-pair640.png`, `c-sv1280-board.png`, `c-sv-topbottom.png`, `c-hud-bottoms.png`, `c-tally-3.png`, `c-pause-3.png`, `c-hud-duel640.png` and `c-vt-vrm640.png`, and the scripts `margins.py` and `bar.py`. Nothing in the worktree was edited, staged or committed.*
