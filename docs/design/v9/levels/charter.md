# Moonfall levels: the production charter

Status: draft for the owner's one look, 6 October 2026. It plans the 105 levels still to make: The Moon Road's stages 3 to 11
(`base-11` to `base-55`) and The Far Shore's stages 1 to 12 (`expansion-01` to `expansion-60`). Stages 1 and 2 (`base-01`
to `base-10`) have shipped, and this charter continues from them.

**What it is for.** Each stage can then be made on its own, in parallel, without breaking its neighbours. The rules in
section 1 bind every stage. The table in section 2 gives each stage its five levels. Section 3 lists what each stage must
check against the stage before it. A stage may change a level's subject or crop inside these rules, as long as it records
the change here. It may never change a band, a jewel pair at a stage boundary, or a challenge carrier without the
coordinator.

**Method and gates.** Nothing here changes `tools/moonfall-levels/README.md`. Every level still passes the pre-flight, the
sweep, reach, play (the greedy player wins at least 5 of seeds 1-48), cheap difficulty, the scene ceiling, the dress guard,
F2-F9, the print check and the converter's gate, and every stage passes `mfl.py stage <n>`.

**Marks.** *[assumed]* marks something not established from the repo. *About [x, y, w, h]* is a proposed crop in source
pixels, judged from the cached painting at reduced scale. The stage's author fixes the real crop on the trace sheet.

## 1. Rules that bind every stage

### 1.1 Difficulty bands

The ramp is the greedy player's wins per 48 on the held-out games at the level's own number. Each step and finale gap is
judged on the pooled 6,912 games, as `mflkit/stagecheck.py` does now: a step of at least 0.5 per 48, and a finale at
least 2.5 below its 4th level, each after one standard error is taken off.

The shipped ramp, from the reports (held out) and the game designer's runtime round 2 (pooled):

| Stage | 1 | 2 | 3 | 4 | 5 (finale) | Mean | Band in `stagecheck.BANDS` |
|---|---|---|---|---|---|---|---|
| 1 | 30.7 | 27.2 | 25.1 | 23.7 | 20.4 | 25.4 | 30 → 21 |
| 2 (pooled) | 28.12 | 26.75 | 25.55 | 24.12 | 20.65 | 25.0 | 27 → 20 |

**The pattern for every later stage.** The opener is at L, then L − 1.5, L − 3 and L − 4.5, and the finale is at its band's end.
- Steps of 1.5 keep a resolved 0.5 even after a drift of 0.6, since one standard error over 6,912 games is about 0.4.
- A finale gap of 3.5 (4.0 to 4.5 on the two campaign finales) clears the 2.5 rule with the same margin.
- Band ends hold to ±1.2 (two standard errors), as `stagecheck` holds stages 1 and 2.

| Stage | Opener → finale | Mean | | Stage | Opener → finale | Mean |
|---|---|---|---|---|---|---|
| MR 3 | 27.0 → 19.0 | 23.6 | | FS 1 | 25.0 → 17.0 | 21.6 |
| MR 4 | 26.5 → 18.5 | 23.1 | | FS 2 | 24.5 → 16.5 | 21.1 |
| MR 5 | 26.0 → 18.0 | 22.6 | | FS 3 | 24.0 → 16.0 | 20.6 |
| MR 6 | 25.0 → 17.0 | 21.6 | | FS 4 | 23.5 → 15.5 | 20.1 |
| MR 7 | 24.5 → 16.5 | 21.1 | | FS 5 | 23.0 → 15.0 | 19.6 |
| MR 8 | 24.0 → 16.0 | 20.6 | | FS 6 | 22.5 → 14.5 | 19.1 |
| MR 9 | 23.0 → 15.0 | 19.6 | | FS 7 | 22.0 → 14.0 | 18.6 |
| MR 10 | 22.5 → 14.5 | 19.1 | | FS 8 | 21.5 → 13.5 | 18.1 |
| MR 11 | 22.0 → 13.0 (gap 4.5) | 18.4 | | FS 9 | 21.0 → 13.0 | 17.6 |
| | | | | FS 10 | 20.5 → 12.5 | 17.1 |
| | | | | FS 11 | 20.0 → 12.0 | 16.6 |
| | | | | FS 12 | 19.5 → 11.0 (gap 4.0) | 16.0 |

- **Each stage is harder overall.** Both ends and the mean fall from stage to stage, and the mean falls by 0.5 to 1.0.
- **Each stage opens easier than the last finale.** The ease-back is 6.35 into MR 3 and 7.0 to 7.5 after that, as the shipped
  game does (20.4, then 28.1).
- **The floor.** The hardest level in the game is FS 12-5 at 11 per 48 (23%). That keeps two margins:
  - The 5-of-48 rule holds with room: at 11 per 48, seeds 1-48 fall below 5 wins under 1% of the time.
  - `MoonfallAces.Suggest` always has 20% of games to choose from (section 1.7).
- **The Far Shore starts over (question 1).** Its stages are numbered from 1, and greens only come at level 3
  (`MoonfallLevelPlace.Number`), so its first two levels have none. FS 1 therefore opens near MR 6's level. Twelve more
  stages could not keep falling from MR 11's 13 without going below the floor.
- **Tooling (done, 7 October 2026).** `stagecheck.BANDS` holds every stage of the tables above, keyed by campaign and stage
  (`mr3`, `fs1`); `mfl.py stage mr3` / `stage fs1` read the level ids' prefix (`base-`, `expansion-`) to pick the campaign, a
  bare number still means the Moon Road, and a stage's first level is compared with the previous stage's last level in the same
  campaign (Far Shore stage 1: nothing).

### 1.2 Jewels

- **F7 inside a level:** the two jewels are at least 60 degrees apart. Plan for **70 or more**, since the measured hue drifts
  from the recipe by up to about 5 degrees (1-2's water planned at 150 measured 151, and 2-1's at 154).
- **F7 between neighbours:** two pairs are the same palette when both hues are within 30 degrees, compared either way round
  (`stagecheck.same_jewels`). This applies across a stage boundary too: a stage's opener against the last finale. Plan
  for **40 or more**. Every pair in section 2 meets 70 within the level and 40 to each neighbour (checked by script),
  starting from 2-5's measured pair, 343 / 209.
- **Hue scale.** These are the reports' `first_jewel_hue` and `second_jewel_hue` (the shipped ten run 247-343 first and
  151-10 second). The pairs here keep the night sky's violet-indigo first jewel (240-345) and move the second.
  - Keep both jewels out of 30-110 degrees, the orange pegs' and the gilt chrome's family. *[assumed: a planning rule drawn
    from F9's orange-on-orange and protan known-bad cases. F9 itself is the gate.]*
- **A stage may swap pairs** between its own levels, if both boundaries stay 40 or more. A changed boundary pair is
  re-checked against the neighbouring stage's table row.

### 1.3 Art sources

**Shares.** The owner's decision 15 is about two thirds game paintings. With the shipped 4 of 10, the plan reaches **76 of 115
(66%)**:

| | Shipped | Moon Road 3-11 | Far Shore | All 115 |
|---|---|---|---|---|
| Game painting, verified path | 4 | 10 | 7 | 21 |
| Game painting, needs a source | 0 | 22 | 33 | 55 |
| Our painting | 6 | 13 | 20 | 39 |
| Game share | 40% | 71% (32/45) | 67% (40/60) | 66% (76/115) |

The Moon Road reaches 36 of 55 (65%). The README asks for 33 of 45 from stages 3-11. This plan has 32, because stage 6 (The
Silent Stars) is a sky stage of our own paintings. The per-stage targets:
- 4 of 5 game paintings in every stage set in a place;
- 3 in MR 10 and FS 12;
- 1 in MR 6 and FS 6;
- 0 in FS 11 (The Courier's Wake, set nowhere by the owner's answer of 6 October).

**Verified game textures.** Each name below appears in a repo file: `docs/design/v9/levels/sources.json`,
`docs/design/v9/rich2/sources.json`, a shipped runtime recipe in `Tsukimichi.Core/Moonfall/Levels/scenes/`, or the pilots'
`level-method.md`.

| Texture | What it is | Used so far | Planned here |
|---|---|---|---|
| `ui/loadingimage/-nowloading_base01_hr1.tex` | La Noscea painting, Limsa from the water | 2-1 (x < 1720) | 5-1, 5-4 (east, kept for stage 5 by the README) |
| `ui/loadingimage/-nowloading_base02_hr1.tex` | Thanalan painting, Ul'dah | 1-4 [800, 0, 2080, 1560] mirrored | 4-2, 4-5 (the strips beyond 1-4's crop) |
| `ui/loadingimage/-nowloading_base03.tex` | Coerthas painting, Ishgard over the cloud sea | pilot base-p2 (approved) and recipe `holy-see` (no shipped level) | 3-3 |
| `ui/loadingimage/-nowloading_base05_hr1.tex` | world painting "The Three Great Continents" | 2-3 [1080, 1300, 960, 720] | 3-1, 3-5, 5-2, FS 4-5, FS 5-2 (distinct regions) |
| `ui/loadingimage/-nowloading_base10.tex` | Kugane painting | recipe `lantern-night` (no shipped level) | FS 8-1 |
| `ui/loadingimage/-nowloading_base21.tex` | Old Sharlayan painting | pilot exp-p1 (approved) | FS 9-1 |
| `ui/loadingimage/-nowloading_base25.tex` | Mare Lamentorum painting | pilot exp-p3 (approved) | FS 12-1, FS 12-4 |
| `ui/map/world/01/world01_m.tex` | the world map | the Adventure map and the level select | 6-3, 11-5, FS 6-3 (question 3) |
| `ui/map/w1f1/00/w1f100_m.tex` | Western Thanalan's area map | 1-1 [284, 340, 1429, 1071] | none (little is left outside 1-1's crop) |
| `ui/loadingimage/-nowloading_base07.tex` | Sohm Al, Dravania (Heavensward) | the title backdrop | none: it is the title's painting, and the story reaches it after FS 3's Sea of Clouds |

**Needs a source (55 levels).**
- A source not yet in the repo is written as *needs:* with the zone and the kind of texture.
- `level-method.md` counts 37 regional loading-screen paintings under `ui/loadingimage/-nowloading_base*.tex`. The README's
  route to them is `texdump loading` (every zone's loading image; the `_hr1` versions are 3840 × 2160) and `texdump maps`
  (the area maps). A name is fixed only once `mfl.py fetch` has dumped it and `mfl.py sources` lists it.
- The map paths follow `ui/map/<code>/<nn>/<code><nn>_m.tex`, as `w1f1/00/w1f100_m.tex` does. Codes for zones other than
  Western Thanalan are not in the repo and are not guessed here.

**Reuse rules.**
- Never the same texture in two levels in a row. That includes a stage boundary.
- At most twice in a stage, and never adjacent.
- A new crop overlaps every earlier crop of the same texture by at most 25% of its own area.
- The README's reservations stand: stage 4 keeps base02 beyond 1-4's crop, and stage 5 keeps base01's east (x > 1700) and
  the Merlthor region of the world painting.
- Crops narrower than 1100 px go soft at 2× (README). The 960 px crop is allowed on 2-3's precedent. The 800 px crop is risk 2.

**Our paintings are for non-figurative subjects only:** skies, water, constellations, light and abstract scenes.
- Creatures, people, ships, airships and similar figures always come from the game's art. The cactuar (1-3) and the
  ferry (2-4) are shipped exceptions and are not repeated.
- A figure may still be drawn as a **constellation**: a moogle or a ship in stars, as pilot exp-p2 does.
- If a figurative *needs* source cannot be found, the level is re-subjected to one our paintings can carry, and this table
  records it (question 2).
- Painter names marked *(new)* are proposals for `docs/design/v9/levels/painters/`.

### 1.4 The spoiler shield

- **Tags.** Every scene is tagged in `MoonfallPlaces.SceneTable` with an era and an area, or set nowhere. The shield hides
  by area. A hidden scene draws its recipe's `fallback` whole and ungraded, with the dress kept but its `paint` plates
  dropped (`scene-recipe.md`, "Fallbacks"). **Every tagged scene here declares `"fallback": "moon-road-night"`** (our night sky,
  as all 13 shipped recipes do). Its dress must stay free of the place: no landmark silhouette and no framing that names
  somewhere, a rule a test enforces.
- **The Moon Road stays in A Realm Reborn.** Its stages are never veiled, so a veiled scene there is played over the plain
  sky and its pegs trace nothing. Every Moon Road scene is ARR or set nowhere. Then only a player still early in ARR meets a
  fallback, as stages 1-2 already allow.
- **A Far Shore level stays in its stage's expansion and area.** The whole stage is veiled until the story reaches its place,
  with a masked name, a veiled preview, and "step over it" in Adventure. Inside an open stage, a scene set somewhere the
  story reaches later would be veiled. So each scene is set in the stage's own area or one reached before it. That is why
  Sohm Al (Dravanian Forelands) is not used in FS 3 (The Sea of Clouds), and why FS 2's levels stay in Western Thanalan.
- **Stages set nowhere have every scene set nowhere:** MR 6, FS 6 and FS 11. They then open at every era, as the owner asked
  for the moogle's stage.
- **Area names.** Names not already in `MoonfallPlaces` are *[assumed]* and must match the shield's own area names. They
  are "Ul'dah - Steps of Thal", "New Gridania", "Central Shroud", "East Shroud", "North Shroud", "The Gold Saucer", "Eastern
  La Noscea", "Lower La Noscea", "Southern Thanalan", "Coerthas Western Highlands" and "Foundation".

### 1.5 Techniques

| Code | Technique | Shipped or pilot example |
|---|---|---|
| TR | a trail on a map (dotted route, ring stops hold the oranges) | 1-1, pilot base-p1 |
| TE | terrain emphasis (brick runs on crests, moons on summits) | pilot base-p2 |
| CR | a creature or vessel in outline (even ring 18 outside the silhouette) | 1-3, pilot base-p3 |
| LM | a landmark partly outlined | 1-4, 2-1, pilot exp-p1 |
| CO | a constellation | 1-5, pilot exp-p2 |
| OR | orbit movers | pilot exp-p3, 2-3's whirlpool |
| SB | a silhouette band (a skyline traced) | 1-2 |
| RF | reflection (the mirror image in slide movers) | 2-5 |
| LS | light and shadow (pegs on beams, glints and lamps) | 2-2 |
| PR | a procession (lanterns dotted along a street or cord) | method §2 |
| LA | a lattice (rings and spokes of brick) | method §2 |
| SP | a set of points (each a ring stop), sometimes with an orbit | 2-3 |
| DR | drift: a moving subject carried by slide movers (rigging, tide, wake, falling stars) | 2-4 |

- **No technique twice in a row** within a stage. Section 2 holds this. Across a stage boundary it is allowed, but avoid it.
- **Every stage uses at least four techniques.**
- **Movers.** Most stages have one or two levels with movers (OR, RF, DR, or SP with an orbit). Stage 1 had none, and MR 4,
  MR 7, MR 9, FS 9 and FS 10 have none by subject. A stage may add a mover where the subject moves.

### 1.6 Candidates and challenges

- **Candidates.** The pre-flight allows 28-35 orange candidates, and all ten shipped levels have 28. The default here rises
  through a stage: 28-29, 30, 30, 31, 32 (33 on a campaign finale). The deal of 25 then varies more as the game goes on.
  Difficulty still comes from where the candidates sit, never from low oranges (the cheap-difficulty gate).
- **ch-03 "Thirty-Five Moons"** (win with 35 oranges) moves to **6-2 `base-27` The Wheel of the Twelve**, built with exactly
  35 candidates (the pre-flight's maximum). A constellation's 35 stars are the subject itself.
- **ch-04 "Forty-Five Moons"** moves to **8-2 `base-37` A Thousand Lanterns**, built with 45 candidates as the explicit
  exception `moonfall-modes.md` asks for (question 5).
  - It needs a recorded pre-flight exception for this one level. Every other rule still holds: 10 per 200 × 200, 60-160
    pieces, the reach of every candidate.
  - Both carriers are the second level of a mid-campaign stage, so the challenge starts from an easier board.
  - **Acceptance:** the greedy player wins at least 5 of 48 at 35 or 45 oranges. Done: `mfcheck play` takes the orange count and
    `mfl.py challenge ch-03` / `ch-04` (or `challenge <level-id> <oranges>`) runs it on the carriers recorded in
    `mflkit/challenge.py`, which also holds the one allow-list entry for 45 candidates (`base-37`, with this section as its reason;
    the pre-flight's 28-35 candidate rule reads it, so no other level can take 45).
  - `challenges.json` still names the shipped carriers (`base-04`, `base-03`) until 6-2 and 8-2 ship; the coordinator moves them then.
- **Other challenges already point at levels made here.** Their stages keep these in mind, and the coordinator recalibrates
  ch-01, ch-05, ch-07 and ch-09 on the built levels, as `moonfall-modes.md` already plans for ch-01, ch-05 and ch-07.

| Challenge | Levels | What it means for the level |
|---|---|---|
| ch-06 clear every peg | `base-13` 3-3 | Keep the piece count moderate (the pilot board) |
| ch-08 the stage finales | `base-15`, `base-20`, `base-25` | Finales as banded |
| ch-09 250,000 with 7 balls, Y'shtola | `base-43` 9-3 Crystal Light | A high-scoring board: many pieces in structures |
| ch-11 duel, Merlwyb | `base-21` to `base-23` (5-1 to 5-3) | Two-sided play: no board that only one side's opening can clear |
| ch-12 duel, Louisoix | `base-46`, `base-48`, `base-50` | As above |

### 1.7 Aces

- **How each Ace is set.** Every level gets its Ace from `MoonfallAces.Suggest` over 576 greedy games at its own number. It
  is the score that 20% of all games reach, rounded to 10,000, and it is recorded in `Moonfall/Modes/aces.json` when the
  level ships. The shipped ten are 240-300k, reached by 18.8-21.5% of games.
- **No hand-set Aces.** A level whose Ace drifts beyond 15-25% of games on 576 fresh seeds is re-suggested, never set by hand.
- **Hard levels (question 4).** Under 20% wins (9.6 per 48), `Suggest` falls back to the lowest win, so the floor of 11 in
  section 1.1 keeps every Ace defined. Below about 14 per 48 the Ace sat near the lowest winning score, and almost every win
  would ace it.
- **Done (the owner's answer 4, 7 October 2026).** `MoonfallAces.Suggest` keeps the rule at 14 of 48 or more. Below it, the Ace is
  the score 40% of the winning games reach (`HardWins` 14, `HardOf` 48, `HardWinShare` 0.40; a rate, so exactly 14 of 48 keeps the
  rule). The shipped ten (252-389 wins of 576) are all above it, and their Aces re-suggest unchanged.

### 1.8 Motion

Every level keeps ambient motion (the converter's `runtime` block; none under Reduce motion):
- dust and the moving beams (±30%), always;
- one **signature** that belongs to the scene:
  - stars kept off the subject (`starWhere`, as runtime round 2 required: no twinkle on a figure, a moon or a lamp);
  - mist in a band below the pegs;
  - glints along a route;
  - lamp or lantern flicker on the painting's real lights;
  - fireflies in a forest.
- Levels with OR, RF or DR techniques also move on the board. The table's "Motion" column names the signature and any
  movers. Fever's moon (`feverMoon`) is named only where a moon is painted (2-4's lesson).

## 2. The 105 levels

- **Codes.** MR stages are written *s-l*, and the Far Shore *FS s-l*.
- **Jewels:** first and second hue, in degrees.
- **Cand:** the number of orange candidates.
- **Ramp:** the held-out target per 48 (±1.2 at the band ends).
- **Place (era):** the scene's tag for the shield, followed by its fallback. "Nowhere" is never veiled.
- **Art source:**
  - a verified path, with the proposed crop;
  - *needs:* a source to find;
  - *ours:* our painter (non-figurative).

### The Moon Road, stages 3-11

| Code | Id | Name | Subject | Tech | Art source | Place (era) | Ramp /48 | Jewels | Cand | Motion |
|---|---|---|---|---|---|---|---|---|---|---|
| 3-1 | base-11 | The Airship Road | the airship's lane on the old world chart, from Gridania north over Coerthas past the chart's own airship and dragons, each city a ring stop | TR | `ui/loadingimage/-nowloading_base05_hr1.tex` (about [0, 640, 1150, 860]: Gridania, Ishgard, the chart's airship (seen near (420, 750)) and dragons; clear of 2-3's crop and of 5-2's strait) | nowhere (never veiled) | 27 | 250 / 175 | 29 | glints along the lanes |
| 3-2 | base-12 | Above the Cloud Sea | a moonlit cloud sea with the lane's beacon lights; pegs ride the beacons' beams | LS | ours: cloudsea (new) | nowhere (never veiled) | 25.5 | 290 / 200 | 30 | mist (cloud drift), stars, beacon flicker |
| 3-3 | base-13 | The Holy See | Ishgard's walls over the cloud sea: bricks on the crests, moons on the spires | TE | `ui/loadingimage/-nowloading_base03.tex` (approved pilot base-p2 and the runtime recipe holy-see) | Coerthas Central Highlands (ARR) → `moon-road-night` | 24 | 255 / 350 | 30 | snow (dust), beams |
| 3-4 | base-14 | The Lane by Starlight | the airship lane by starlight: the cloud banks sliding past (slide movers riding their crests), the beacons closing toward the horizon, the star river overhead; no hull (MR 3 round 1: renamed from "The Airship Under Way", re-subjected by `sources-plan.md`: no airship art at a usable size) | DR | ours: cloudlane (re-subject, `sources-plan.md`) | nowhere (never veiled) | 22.5 | 285 / 175 (built 275 / 208) | 31 | slide movers (clouds), stars, low mist |
| 3-5 | base-15 | The Sky Whale | the chart's whale swimming the cloud sea, its own silhouette ringed by moons; the vortex's rim and inner wall, an eddy, the south flock's chevron and its flight line, and the cloud sea's lit crests dotted (MR 3 round 1: zoomed on the whale; the north flock lies beyond the wall) | CR | `ui/loadingimage/-nowloading_base05_hr1.tex` ([940, 330, 960, 720], MR 3 round 1: the whale about 200 of the opening's 650 units; 24.3% over 3-1's crop, none over 2-3's) | nowhere (never veiled) | 19 | 245 / 160 (built 258 / 176) | 29 | glints, dust |
| 4-1 | base-16 | The Steps of Thal | a night walk from the Gate of Thal through the markets to the Steps, stops as rings of moons | TR | Ul'dah's Steps of Thal city map, `ui/map/w1t2/01/w1t201_m.tex` (crop [660, 740, 920, 690]; sources-plan.md) | Ul'dah - Steps of Thal (ARR) → `moon-road-night` | 26.5 | 270 / 350 | 29 | glints along the walk, lamp flicker |
| 4-2 | base-17 | The Spire of the Sands | the tall spire and the skyway at the city's east edge, partly outlined | LM | `ui/loadingimage/-nowloading_base02_hr1.tex` (east strip beyond 1-4's crop, about [2880, 120, 960, 720], 960 px as 2-3's approved crop; a red craft at the upper right: an airship's hull seen from below, its keel traced under it) | Ul'dah - Steps of Nald (ARR) → `moon-road-night` | 25 | 230 / 160 | 30 | beams, lamp flicker |
| 4-3 | base-18 | Lamps on the Steps | a stair of lamp glows climbing a dark terrace; pegs on the pools of light | LS | ours: lampsteps (new) | Ul'dah - Steps of Nald (ARR) → `moon-road-night` | 23.5 | 285 / 5 | 30 | lamp flicker, dust |
| 4-4 | base-19 | The Coliseum | the arena's tiers as rings and spokes of brick, Raubahn's old ground | LA | Ul'dah's Steps of Thal city map, `ui/map/w1t2/02/w1t202_m.tex` (crop [560, 640, 960, 720]: the Coliseum's lower floor; sources-plan.md) | Ul'dah - Steps of Thal (ARR) → `moon-road-night` | 22 | 261 / 179 | 31 | glints round the city ring and the stands, dust |
| 4-5 | base-20 | Ul'dah from the Sands | the city's great wall met from the night sands: brick crowns on its domes, ramparts, the roads out into the desert, and the Steps of Thal's streets beyond (MR 4 round 1: a plan has no skyline) | LM | Ul'dah's Steps of Nald city map, `ui/map/w1t1/01/w1t101_m.tex` (crop [330, 420, 1160, 870]; the owner's crop fix in sources-plan.md) | Ul'dah - Steps of Nald (ARR) → `moon-road-night` | 18.5 | 320 / 235 | 29 | mist (sand haze), glints along the roads, dust |
| 5-1 | base-21 | The White Towers | the Admiral's white towers over the bay, partly outlined | LM | `ui/loadingimage/-nowloading_base01_hr1.tex` (east, about [1800, 60, 1440, 1080], beyond 2-1's crop (x > 1700, kept for stage 5)) | Limsa Lominsa Lower Decks (ARR) → `moon-road-night` | 26 | 265 / 175 | 29 | mist on the water, beams |
| 5-2 | base-22 | The Strait of Merlthor | the ferry's crossing between two dotted coasts (round 1's parked board) | TR | `ui/loadingimage/-nowloading_base05_hr1.tex` (about [0, 1335, 1100, 825]: Limsa, Vylbrand and the strait (parked for stage 5 in round 1); about 20% over 3-1's crop) | nowhere (never veiled) | 24.5 | 295 / 10 | 30 | glints along the crossing |
| 5-3 | base-23 | Lights on the Water | the harbour lamps' long reflections; pegs on the light | LS | ours: harbourlights (new) | Limsa Lominsa Lower Decks (ARR) → `moon-road-night` | 23 | 250 / 175 | 30 | mist, lamp flicker |
| 5-4 | base-24 | The Arches of the Bridge | the lamplit bridge: bricks in its arches, moons on its lamps | TE | `ui/loadingimage/-nowloading_base01_hr1.tex` (east, about [2300, 640, 1440, 1080] (at most 25% over 5-1's crop)) | Limsa Lominsa Lower Decks (ARR) → `moon-road-night` | 21.5 | 275 / 350 | 31 | lamp flicker, mist |
| 5-5 | base-25 | The Lighthouses of Vylbrand | the coast's lighthouses as ring stops, their beams turning (orbit) | SP | needs: Western or Lower La Noscea area map (`texdump maps`) | Western La Noscea (ARR) → `moon-road-night` | 18 | 240 / 160 | 32 | orbit movers (beams), glints |
| 6-1 | base-26 | The Armillary | a celestial globe of light as rings and spokes of brick | LA | ours: armillary (new) | nowhere (never veiled) | 25 | 285 / 200 | 29 | stars, slow beams |
| 6-2 | base-27 | The Wheel of the Twelve | the Twelve's signs as one great constellation (ch-03: 35 candidates) | CO | ours: twelve (new) | nowhere (never veiled) | 23.5 | 250 / 330 | 35 | stars |
| 6-3 | base-28 | The Seer's Chart | Eorzea's cities as stars joined by the ley lines Urianger reads | SP | `ui/map/world/01/world01_m.tex` (Eorzea's land; the Adventure map's own painting (question 3)) | nowhere (never veiled) | 22 | 300 / 160 | 30 | glints along the lines |
| 6-4 | base-29 | Meteor Shower | falling stars drawn by slide movers across a still sky | DR | ours: meteors (new) | nowhere (never veiled) | 20.5 | 245 / 175 | 31 | slide movers, stars |
| 6-5 | base-30 | The Moon Gate | a gate of light ringed by orbiting moons (Urianger's Moon Gate) | OR | ours: moongate (new) | nowhere (never veiled) | 17 | 320 / 180 | 32 | orbit movers, stars |
| 7-1 | base-31 | Gridania's Canals | the lantern walk along Gridania's canals, bridges as stops | TR | needs: New Gridania city map (`texdump maps`) | New Gridania (ARR) → `moon-road-night` | 24.5 | 270 / 150 | 29 | glints on the water, lamp flicker |
| 7-2 | base-32 | Bentbranch at Dusk | the meadow's treeline as a skyline band at dusk (decision 18's name) | SB | needs: Central Shroud loading painting (`texdump loading`) | Central Shroud (ARR) → `moon-road-night` | 23 | 255 / 345 | 30 | fireflies, mist |
| 7-3 | base-33 | Fireflies in the Glade | a moonlit glade; pegs on the fireflies' lantern paths | LS | ours: glade (new) | Central Shroud (ARR) → `moon-road-night` | 21.5 | 285 / 165 | 30 | fireflies, beams through leaves |
| 7-4 | base-34 | The Sylph Lanterns | a lantern procession winding through the East Shroud's trees | PR | needs: East Shroud loading painting or area map (`texdump loading`, `maps`) | East Shroud (ARR) → `moon-road-night` | 20 | 245 / 330 | 31 | lantern flicker, fireflies |
| 7-5 | base-35 | The Twelveswood | the great trees: bricks on roots and boughs, moons on the crowns (decision 18's name) | TE | needs: the Black Shroud loading painting (`texdump loading`) | North Shroud (ARR) → `moon-road-night` | 16.5 | 300 / 150 | 32 | fireflies, beams, leaf dust |
| 8-1 | base-36 | The Gold Saucer | the Saucer's great ring and lights partly outlined | LM | needs: the Gold Saucer loading painting (`texdump loading`) | The Gold Saucer (ARR) → `moon-road-night` | 24 | 270 / 190 | 29 | lamp flicker, glints |
| 8-2 | base-37 | A Thousand Lanterns | lantern strings over a night market (ch-04: 45 candidates, the explicit exception) | PR | ours: lanternstrings (new) | nowhere (never veiled) | 22.5 | 320 / 160 | 45 | lantern flicker, dust |
| 8-3 | base-38 | Costa del Sol | the bay's lanterns and their mirror image in the water | RF | needs: Eastern La Noscea loading painting or area map (`texdump loading`, `maps`) | Eastern La Noscea (ARR) → `moon-road-night` | 21 | 245 / 175 | 30 | slide movers (ripples), lamp flicker |
| 8-4 | base-39 | The Sapphire Exchange | the market aisles as a route, each stall a ring stop | TR | needs: Ul'dah's Steps of Thal city map, a crop distinct from 4-1's | Ul'dah - Steps of Thal (ARR) → `moon-road-night` | 19.5 | 280 / 355 | 31 | glints, lamp flicker |
| 8-5 | base-40 | Tataru's Draw | a drum of lantern-moons turning over the Saucer (Moon-Viewing Draw) | OR | needs: the Gold Saucer painting, a second crop distinct from 8-1's, or its area map | The Gold Saucer (ARR) → `moon-road-night` | 16 | 240 / 150 | 33 | orbit movers, lamp flicker |
| 9-1 | base-41 | Revenant's Toll | paths through the Toll between the crystal outcrops, stops as rings | TR | needs: Mor Dhona area map (`texdump maps`) | Mor Dhona (ARR) → `moon-road-night` | 23 | 285 / 195 | 29 | glints, crystal glints |
| 9-2 | base-42 | The Glass Spires | Mor Dhona's crystal spires as a skyline band | SB | needs: Mor Dhona loading painting (`texdump loading`) | Mor Dhona (ARR) → `moon-road-night` | 21.5 | 250 / 320 | 30 | stars, mist |
| 9-3 | base-43 | Crystal Light | beams split through a cluster of crystal; pegs on the beams (ch-09: a high-scoring board) | LS | ours: crystal (shipped painter, a new composition) | Mor Dhona (ARR) → `moon-road-night` | 20 | 300 / 185 | 30 | beams, glints |
| 9-4 | base-44 | The Crystal Tower | the tower partly outlined: moons on its tiers, bricks on its crowns | LM | needs: the Crystal Tower loading painting (`texdump loading`) | Mor Dhona (ARR) → `moon-road-night` | 18.5 | 265 / 345 | 31 | beams, dust |
| 9-5 | base-45 | The Crystal Ridges | bricks along the crystal ridges, a moon over each peak | TE | needs: Mor Dhona loading painting, a second crop distinct from 9-2's | Mor Dhona (ARR) → `moon-road-night` | 15 | 240 / 170 | 32 | glints, mist |
| 10-1 | base-46 | Silvertear Falls | the falls as columns of pegs, the cliffs' crowns as bricks | LM | needs: Mor Dhona loading painting or area map, the Silvertear region | Mor Dhona (ARR) → `moon-road-night` | 22.5 | 285 / 200 | 29 | mist (spray), stars |
| 10-2 | base-47 | Dalamud's Red Moon | the red moon breaking, its fragments orbiting | OR | ours: dalamud (new; a painted moon, ceilingExempt) | Mor Dhona (ARR) → `moon-road-night` | 21 | 330 / 230 | 30 | orbit movers, stars |
| 10-3 | base-48 | Carteneau's Wastes | the scarred flats as terrain: bricks on the ridges of the impact | TE | needs: Mor Dhona area map (the Carteneau flats), distinct from 9-1's and 10-1's crops | Mor Dhona (ARR) → `moon-road-night` | 19.5 | 255 / 160 | 30 | dust, mist |
| 10-4 | base-49 | The Twelve's Light | Louisoix's summoning as a constellation of the Twelve across the sky | CO | ours: twelvelight (new) | nowhere (never veiled) | 18 | 285 / 0 | 31 | stars |
| 10-5 | base-50 | Silvertear by Night | the lake mirrors the falls and the moon (finale) | RF | needs: Mor Dhona loading painting, the lake (a crop distinct from 10-1's) | Mor Dhona (ARR) → `moon-road-night` | 14.5 | 245 / 175 | 33 | slide movers (ripples), mist, stars |
| 11-1 | base-51 | The Sagolii by Night | the southern dunes' crests as runs of brick | TE | needs: Southern Thanalan area map or loading painting (`texdump maps`, `loading`) | Southern Thanalan (ARR) → `moon-road-night` | 22 | 285 / 175 | 29 | mist (sand haze), stars |
| 11-2 | base-52 | Five Moons | the five phases of the moon, each a ring orbiting its own centre | OR | ours: fivemoons (new) | nowhere (never veiled) | 20.5 | 250 / 340 | 30 | orbit movers, stars |
| 11-3 | base-53 | Gridania from the Lake | the city's roofline and trees as a band over the water | SB | needs: the Black Shroud or New Gridania loading painting, distinct from stage 7's crops | New Gridania (ARR) → `moon-road-night` | 19 | 300 / 200 | 30 | fireflies, mist |
| 11-4 | base-54 | Camp Dragonhead | the camp's walls and towers partly outlined in the snow | LM | needs: Coerthas Central Highlands area map (`texdump maps`) | Coerthas Central Highlands (ARR) → `moon-road-night` | 17.5 | 260 / 160 | 31 | snow (dust), beams |
| 11-5 | base-55 | The Moon Road | the whole road traced across the world map, every stage's home a stop | TR | `ui/map/world/01/world01_m.tex` (the whole of Eorzea: the Adventure map's own painting, the road you travelled) | nowhere (never veiled) | 13 | 310 / 210 | 33 | glints along the road |

### The Far Shore, stages 1-12

| Code | Id | Name | Subject | Tech | Art source | Place (era) | Ramp /48 | Jewels | Cand | Motion |
|---|---|---|---|---|---|---|---|---|---|---|
| FS 1-1 | expansion-01 | The Lantern Quay | the quay's lantern walk where the voyage sets out | TR | needs: Limsa Lominsa Lower Decks city map (`texdump maps`) | Limsa Lominsa Lower Decks (ARR) → `moon-road-night` | 25 | 265 / 175 | 28 | glints, lamp flicker |
| FS 1-2 | expansion-02 | Ships at Anchor | moored ships' rigging rocking on the swell (slide movers) | DR | needs: a La Noscea loading painting with moored ships (`texdump loading`) | Lower La Noscea (ARR) → `moon-road-night` | 23.5 | 250 / 330 | 29 | slide movers, mist |
| FS 1-3 | expansion-03 | The Lighthouse Beam | a beam sweeping the night sea; pegs on the beam and its sparkle | LS | ours: lighthouse (new) | Limsa Lominsa Lower Decks (ARR) → `moon-road-night` | 22 | 295 / 165 | 30 | beams, glints |
| FS 1-4 | expansion-04 | Ports of Call | the voyage's ports of call along Vylbrand's coast, each a ring stop | SP | needs: Lower La Noscea area map (`texdump maps`) | Lower La Noscea (ARR) → `moon-road-night` | 20.5 | 240 / 165 | 31 | glints |
| FS 1-5 | expansion-05 | Setting Sail | a galleon under full sail in an even ring of moons | CR | needs: a La Noscea loading painting with a ship under sail (`texdump loading`) | Lower La Noscea (ARR) → `moon-road-night` | 17 | 320 / 175 | 32 | mist, beams |
| FS 2-1 | expansion-06 | The Twin Lighthouses | Vesper Bay's lights over the Thanalan coast, partly outlined | LM | needs: Western Thanalan loading painting (`texdump loading`) | Western Thanalan (ARR) → `moon-road-night` | 24.5 | 280 / 355 | 28 | beams, lamp flicker |
| FS 2-2 | expansion-07 | The Twins' Stars | a pair of twin constellations over the sea (Multiball's pairs) | CO | ours: twinstars (new) | Western Thanalan (ARR) → `moon-road-night` | 23 | 250 / 180 | 29 | stars |
| FS 2-3 | expansion-08 | The Coast from the Sea | the Thanalan coastline as terrain: bricks on the cliffs | TE | needs: Western Thanalan area map, outside 1-1's crop [284, 340, 1429, 1071] of ui/map/w1f1/00/w1f100_m.tex, or another coast map | Western Thanalan (ARR) → `moon-road-night` | 21.5 | 300 / 160 | 30 | mist, glints |
| FS 2-4 | expansion-09 | The Silver Bazaar | the coastal bazaar's ruined arches partly outlined against the sea | LM | needs: Western Thanalan loading painting or area map, a crop distinct from 2-1's | Western Thanalan (ARR) → `moon-road-night` | 20 | 255 / 345 | 31 | dust, stars |
| FS 2-5 | expansion-10 | The Twin Lights | the two lights and their mirror halves across the board (finale) | RF | needs: Western Thanalan loading painting, a crop distinct from 2-1's | Western Thanalan (ARR) → `moon-road-night` | 16.5 | 305 / 200 | 32 | slide movers, lamp flicker |
| FS 3-1 | expansion-11 | Above the Sea of Clouds | cloud tops as humps of brick, moons on the floating isles | TE | needs: The Sea of Clouds loading painting (`texdump loading`) | The Sea of Clouds (HW) → `moon-road-night` | 24 | 265 / 175 | 28 | mist (cloud drift), stars |
| FS 3-2 | expansion-12 | Falcon's Nest | the outpost's tower and walls partly outlined in the snow | LM | needs: Coerthas Western Highlands loading painting or area map (`texdump loading`, `maps`) | Coerthas Western Highlands (HW) → `moon-road-night` | 22.5 | 285 / 355 | 29 | snow (dust), lamp flicker |
| FS 3-3 | expansion-13 | Wind over the Clouds | wind streams drawn by slide movers across an open sky | DR | ours: windstreams (new) | The Sea of Clouds (HW) → `moon-road-night` | 21 | 245 / 175 | 30 | slide movers, stars |
| FS 3-4 | expansion-14 | The Floating Isles | the airship's route between the isles, each a ring stop | TR | needs: The Sea of Clouds area map (`texdump maps`) | The Sea of Clouds (HW) → `moon-road-night` | 19.5 | 285 / 160 | 31 | glints |
| FS 3-5 | expansion-15 | Ishgard Receding | Ishgard's skyline as a band seen from the deck (finale) | SB | needs: an Ishgard (Heavensward) loading painting (`texdump loading`) | Foundation (HW) → `moon-road-night` | 16 | 250 / 330 | 32 | stars, snow (dust) |
| FS 4-1 | expansion-16 | Across the Ruby Sea | the route between the isles, each a ring stop | TR | needs: The Ruby Sea area map (`texdump maps`) | The Ruby Sea (SB) → `moon-road-night` | 23.5 | 280 / 175 | 28 | glints |
| FS 4-2 | expansion-17 | Onokoro by Moonlight | the island's shrine gate partly outlined | LM | needs: The Ruby Sea loading painting (`texdump loading`) | The Ruby Sea (SB) → `moon-road-night` | 22 | 330 / 215 | 29 | lamp flicker, mist |
| FS 4-3 | expansion-18 | The Red Tide | the red sea's tide drawn by slide movers under the moon | DR | ours: redtide (new) | The Ruby Sea (SB) → `moon-road-night` | 20.5 | 260 / 0 | 30 | slide movers, mist |
| FS 4-4 | expansion-19 | The Kojin's Reef | reef ridges as runs of brick, a moon on each head | TE | needs: The Ruby Sea loading painting, a crop distinct from 4-2's | The Ruby Sea (SB) → `moon-road-night` | 19 | 295 / 160 | 31 | glints, mist |
| FS 4-5 | expansion-20 | The Phoenix of the East | the chart's firebird in an even ring of moons (finale) | CR | `ui/loadingimage/-nowloading_base05_hr1.tex` (about [2740, 1100, 1100, 825]: the firebird (seen near (3450-3800, 1350-1700))) | nowhere (never veiled) | 15.5 | 245 / 345 | 32 | glints, dust |
| FS 5-1 | expansion-21 | The Isles of Umbra | the isles' coasts as terrain: bricks on the cliffs | TE | needs: Western La Noscea area map (`texdump maps`) | Western La Noscea (ARR) → `moon-road-night` | 23 | 270 / 190 | 28 | mist, glints |
| FS 5-2 | expansion-22 | The Admiral's Fleet | the chart's ships in the eastern sea, each a ring stop | SP | `ui/loadingimage/-nowloading_base05_hr1.tex` (about [2040, 1350, 960, 720], east of 2-3's crop, about 22% over FS 4-5's [ships to confirm on the trace sheet]) | nowhere (never veiled) | 21.5 | 310 / 165 | 29 | glints |
| FS 5-3 | expansion-23 | Phosphor Wake | a glowing wake on black water; pegs on the light | LS | ours: phosphor (new) | nowhere (never veiled) | 20 | 250 / 180 | 30 | glints, mist |
| FS 5-4 | expansion-24 | The Wreck at Sirensong | the wreck partly outlined, masts as short columns | LM | needs: a Western La Noscea loading painting with the wreck (`texdump loading`) | Western La Noscea (ARR) → `moon-road-night` | 18.5 | 280 / 355 | 31 | mist, beams |
| FS 5-5 | expansion-25 | The Admiral's Flagship | a flagship's rigging over a moving sea (finale) | DR | needs: a La Noscea loading painting with a flagship (`texdump loading`) | Western La Noscea (ARR) → `moon-road-night` | 15 | 240 / 160 | 32 | slide movers, mist, lamp flicker |
| FS 6-1 | expansion-26 | The Ferry in the Stars | the ferry as a constellation (approved pilot exp-p2) | CO | ours: constellation (pilot exp-p2's painter) | nowhere (never veiled) | 22.5 | 280 / 200 | 28 | stars |
| FS 6-2 | expansion-27 | The Night Ferry's Wake | the wake behind an unseen ferry, drawn by slide movers | DR | ours: wake (new) | nowhere (never veiled) | 21 | 250 / 335 | 29 | slide movers, stars |
| FS 6-3 | expansion-28 | The Navigator's Chart | the voyage's course round Eorzea's coasts on the world map, each port a ring stop | TR | `ui/map/world/01/world01_m.tex` (the coasts, a crop distinct from MR 6-3's; MR 11-5 uses the whole map) | nowhere (never veiled) | 19.5 | 300 / 165 | 30 | glints |
| FS 6-4 | expansion-29 | The Turning Sky | star trails orbiting the pole star | OR | ours: startrails (new) | nowhere (never veiled) | 18 | 255 / 185 | 31 | orbit movers, stars |
| FS 6-5 | expansion-30 | Starfall on the Water | the stars and their mirror image in a calm sea (finale) | RF | ours: starfall (new) | nowhere (never veiled) | 14.5 | 320 / 180 | 32 | slide movers, stars |
| FS 7-1 | expansion-31 | The Faerie Lands | Il Mheg's faerie tower partly outlined | LM | needs: Il Mheg loading painting (`texdump loading`) | Il Mheg (ShB) → `moon-road-night` | 22 | 260 / 155 | 28 | fireflies, beams |
| FS 7-2 | expansion-32 | Pixie Lights | drifting faerie lights; pegs on their paths | LS | ours: pixielights (new) | Il Mheg (ShB) → `moon-road-night` | 20.5 | 255 / 325 | 29 | fireflies, stars |
| FS 7-3 | expansion-33 | The Lake Castle | the castle in the lake and its reflection | RF | needs: Il Mheg loading painting, a crop distinct from 7-1's | Il Mheg (ShB) → `moon-road-night` | 19 | 285 / 195 | 30 | slide movers, mist |
| FS 7-4 | expansion-34 | Il Mheg's Paths | the flower paths between the faerie villages, stops as rings | TR | needs: Il Mheg area map (`texdump maps`) | Il Mheg (ShB) → `moon-road-night` | 17.5 | 240 / 155 | 31 | glints, fireflies |
| FS 7-5 | expansion-35 | The Floating Grove | floating isles' rims as bricks drifting slowly, moons on the treetops (finale) | TE | needs: an Il Mheg loading painting with floating isles (`texdump loading`) | Il Mheg (ShB) → `moon-road-night` | 14 | 300 / 150 | 32 | slide movers (drift), fireflies |
| FS 8-1 | expansion-36 | Lantern Night | Kugane's lantern strings as a procession over the canal | PR | `ui/loadingimage/-nowloading_base10.tex` (the runtime recipe lantern-night, crop [240, 0, 1440, 1080]) | Kugane (SB) → `moon-road-night` | 21.5 | 345 / 200 | 28 | lantern flicker, mist |
| FS 8-2 | expansion-37 | Kugane's Harbour | the harbour streets as a route, each pier a ring stop | TR | needs: Kugane city map (`texdump maps`) | Kugane (SB) → `moon-road-night` | 20 | 255 / 345 | 29 | glints |
| FS 8-3 | expansion-38 | Paper Lanterns on the Canal | floating lanterns drifting downstream (slide movers) | DR | ours: floatinglanterns (new) | Kugane (SB) → `moon-road-night` | 18.5 | 285 / 160 | 30 | slide movers, flicker |
| FS 8-4 | expansion-39 | The Ruby Bazaar | the bazaar's rooflines as a skyline band | SB | needs: a Kugane loading painting other than base10 (`texdump loading`) | Kugane (SB) → `moon-road-night` | 17 | 240 / 170 | 31 | lantern flicker, stars |
| FS 8-5 | expansion-40 | Tataru's Lucky Drum | a drum of lantern-moons turning over the bazaar (finale) | OR | needs: the same Kugane source as 8-4, a distinct crop, or Kugane's city map | Kugane (SB) → `moon-road-night` | 13.5 | 325 / 175 | 32 | orbit movers, flicker |
| FS 9-1 | expansion-41 | The Domes of Sharlayan | the domes' crowns as curved bricks (approved pilot exp-p1) | LM | `ui/loadingimage/-nowloading_base21.tex` (approved pilot exp-p1's crop) | Old Sharlayan (EW) → `moon-road-night` | 21 | 270 / 185 | 28 | beams, mist (falls) |
| FS 9-2 | expansion-42 | The Studium's Stars | the Studium's star globe as a constellation | CO | ours: studium (new) | Old Sharlayan (EW) → `moon-road-night` | 19.5 | 320 / 210 | 29 | stars |
| FS 9-3 | expansion-43 | The Harbour of Sharlayan | the harbour walk as a route, piers as stops | TR | needs: Old Sharlayan city map (`texdump maps`) | Old Sharlayan (EW) → `moon-road-night` | 18 | 250 / 160 | 30 | glints |
| FS 9-4 | expansion-44 | Lamps of the Agora | the agora's colonnades as a skyline band | SB | needs: Old Sharlayan painting at hr1, a crop at most 25% over exp-p1's, else another Sharlayan loading painting | Old Sharlayan (EW) → `moon-road-night` | 16.5 | 290 / 200 | 31 | lamp flicker, stars |
| FS 9-5 | expansion-45 | The Sea Wall | the sea wall and breakwater as runs of brick (finale) | TE | needs: Old Sharlayan area map or loading painting (`texdump maps`, `loading`) | Old Sharlayan (EW) → `moon-road-night` | 13 | 240 / 330 | 32 | mist, beams |
| FS 10-1 | expansion-46 | Labyrinthos | the artificial sky's dome as rings and spokes of brick | LA | needs: Labyrinthos loading painting (`texdump loading`) | Labyrinthos (EW) → `moon-road-night` | 20.5 | 275 / 175 | 28 | beams, glints |
| FS 10-2 | expansion-47 | The False Sky | the false sky's light falling in shafts; pegs on the light | LS | ours: falsesky (new) | Labyrinthos (EW) → `moon-road-night` | 19 | 245 / 340 | 29 | beams, dust |
| FS 10-3 | expansion-48 | The Archons' Gardens | the garden paths as a route, each grove a ring stop | TR | needs: Labyrinthos area map (`texdump maps`) | Labyrinthos (EW) → `moon-road-night` | 17.5 | 300 / 160 | 30 | glints |
| FS 10-4 | expansion-49 | The Aitiascope | the great structure partly outlined | LM | needs: Labyrinthos loading painting, a crop distinct from 10-1's | Labyrinthos (EW) → `moon-road-night` | 16 | 260 / 190 | 31 | beams, glints |
| FS 10-5 | expansion-50 | The Archon's Crossing | the crossing's spans as bricks, moons on the pylons (finale) | TE | needs: a Labyrinthos loading painting of the crossing, or its area map | Labyrinthos (EW) → `moon-road-night` | 12.5 | 320 / 165 | 32 | mist, beams |
| FS 11-1 | expansion-51 | The Courier's Wake | a streak of light across the open sky, drawn by slide movers | DR | ours: courierwake (new) | nowhere (never veiled) | 20 | 280 / 190 | 28 | slide movers, stars |
| FS 11-2 | expansion-52 | Storm Post | a bolt's path from the top to the bucket; pegs along it | LS | ours: storm (new) | nowhere (never veiled) | 18.5 | 245 / 330 | 29 | beams (lightning flicker), stars |
| FS 11-3 | expansion-53 | The Post Bag's Stars | the courier's rounds as a trail of stars across the night, each delivery a ring stop | TR | ours: poststars (new) | nowhere (never veiled) | 17 | 300 / 160 | 30 | glints |
| FS 11-4 | expansion-54 | The Lantern Relay | a relay of lanterns passed across the sky | PR | ours: relay (new) | nowhere (never veiled) | 15.5 | 255 / 175 | 31 | lantern flicker, stars |
| FS 11-5 | expansion-55 | The Courier's Constellation | the moogle drawn only in stars (finale) | CO | ours: mooglestars (new) | nowhere (never veiled) | 12 | 320 / 175 | 32 | stars |
| FS 12-1 | expansion-56 | The Sea of Sorrows | an orbit of moons round the world in the sky (approved pilot exp-p3) | OR | `ui/loadingimage/-nowloading_base25.tex` (approved pilot exp-p3's crop) | Mare Lamentorum (EW) → `moon-road-night` | 19.5 | 265 / 195 | 28 | orbit movers, stars |
| FS 12-2 | expansion-57 | Starrise | the star rising over the grey sea; pegs on its light | LS | ours: starrise (new) | Mare Lamentorum (EW) → `moon-road-night` | 18 | 310 / 205 | 29 | stars, beams |
| FS 12-3 | expansion-58 | Craters of the Moon | crater rims: brick crowns above, dotted lower rims (never a cup) | TE | needs: Mare Lamentorum area map (`texdump maps`) | Mare Lamentorum (EW) → `moon-road-night` | 16.5 | 245 / 165 | 30 | dust, stars |
| FS 12-4 | expansion-59 | The Drifting Stones | the drifting rocks along the belt, carried by slide movers | DR | `ui/loadingimage/-nowloading_base25.tex` (a crop at most 25% over exp-p3's; its `_hr1` version if `texdump` lists one) | Mare Lamentorum (EW) → `moon-road-night` | 15 | 285 / 210 | 31 | slide movers, stars |
| FS 12-5 | expansion-60 | Moonfall | the full moon low over the grey sea, its limb partly outlined in moons above it (the game's last level) | LM | ours: fullmoon (new; a painted moon, ceilingExempt) | Mare Lamentorum (EW) → `moon-road-night` | 11 | 330 / 190 | 33 | stars, beams |

## 3. Stages

Each stage records its companion, its theme and the checks against the stage before it. "Boundary F7" is the planning
distance (section 1.2) between the previous finale's pair and this stage's opener. Every boundary also changes texture:
no stage opens on the texture of the finale before it.

| Stage | Companion (power) | Theme: what ties the five | Game / ours | Previous finale → opener (ramp) | Boundary F7 |
|---|---|---|---|---|---|
| MR 3 The Night Skyway | Cid (Brass Wings) | What flies over Eorzea by night: the chart's airship lane, the cloud sea's beacons, Ishgard over its clouds, the lane by starlight, and the chart's whale. | 3 / 2 (3-4 moved to ours: `sources-plan.md`) | 2-5 20.65 (343/209) → 27.0 (250/175) | 93 |
| MR 4 The Sunlit Steps | Raubahn (Lunar Burst) | Ul'dah by night, from the Gate of Thal up the Steps to the Coliseum and out to the sands. | 4 / 1 | 3-5 19.0 → 26.5 | 110 |
| MR 5 Harbour Lights | Merlwyb (Flippers) | Limsa and its waters: the Admiral's towers, the strait, the harbour lamps, the bridge, Vylbrand's lighthouses. | 4 / 1 | 4-5 18.5 → 26.0 | 55 |
| MR 6 The Silent Stars | Urianger (Moon Gate) | The heavens Urianger reads: the armillary, the Twelve's wheel, the seer's chart, a meteor shower, the Moon Gate. Set nowhere. | 1 / 4 | 5-5 18.0 → 25.0 | 45 |
| MR 7 The Shroud by Night | Kan-E-Senna (Moonbloom) | Gridania and the Black Shroud after dark: the canals, Bentbranch at Dusk, a firefly glade, the sylphs' lanterns, the Twelveswood (decision 18's names). | 4 / 1 | 6-5 17.0 → 24.5 | 50 |
| MR 8 The Market Lanterns | Tataru (Moon-Viewing Draw) | Eorzea's lantern-lit markets and fairs: the Gold Saucer, a night market (ch-04), Costa del Sol, the Sapphire Exchange, the lucky draw. ARR only, so Kugane goes to FS 8. | 4 / 1 | 7-5 16.5 → 24.0 | 40 |
| MR 9 Mor Dhona's Glass | Y'shtola (Fireball) | Mor Dhona's crystal country: Revenant's Toll, the glass spires, crystal light (ch-09), the Crystal Tower, the ridges. | 4 / 1 | 8-5 16.0 → 23.0 | 45 |
| MR 10 Silvertear by Night | Louisoix (Sage's Path) | The Calamity remembered: Silvertear Falls, Dalamud's red moon, Carteneau, the Twelve's light, and the lake (ch-12 duel on 10-1, 10-3, 10-5). | 3 / 2 | 9-5 15.0 → 22.5 | 45 |
| MR 11 Your Pick | the player picks | The road revisited at its hardest: the Sagolii, the five moons, Gridania from the lake, Camp Dragonhead, and the whole Moon Road on the world map. | 4 / 1 | 10-5 14.5 → 22.0 | 40 |
| FS 1 The Lantern Quay | Minfilia (Super Guide) | The voyage sets out from Limsa's lantern-lit quay: ships at anchor, the lighthouse, the ports of call, setting sail. No greens on 1-1 and 1-2. | 4 / 1 | 11-5 13.0 → 25.0 (the reset, question 1) | 45 |
| FS 2 The Twin Lights | Alphinaud & Alisaie (Multiball) | Vesper Bay's lights seen from the sea, in pairs, with subjects distinct from stage 2's moonpath, ferry and lanterns: lighthouses, twin stars, the coast, the Silver Bazaar. | 4 / 1 | FS 1-5 17.0 → 24.5 | 105 |
| FS 3 The Skyward Deck | Cid (Brass Wings) | From an airship's deck over the Sea of Clouds: cloud tops, Falcon's Nest, the wind, the isles' route, Ishgard receding. | 4 / 1 | FS 2-5 16.5 → 24.0 | 40 |
| FS 4 The Sunlit Isles | Raubahn (Lunar Burst) | The Ruby Sea's islands by night: the isles' route, Onokoro, the red tide, the Kojin's reef, the chart's firebird. | 4 / 1 | FS 3-5 16.0 → 23.5 | 75 |
| FS 5 The Admiral's Sea | Merlwyb (Flippers) | La Noscea's open water: the Isles of Umbra, the chart's fleet, a phosphor wake, the Sirensong wreck, the flagship. | 4 / 1 | FS 4-5 15.5 → 23.0 | 75 |
| FS 6 The Ferry in the Stars | Urianger (Moon Gate) | The crossing under the stars (pilot exp-p2 opens it): the wake, the navigator's chart, the turning sky, starfall on the water. Set nowhere. | 1 / 4 | FS 5-5 15.0 → 22.5 | 40 |
| FS 7 The Floating Grove | Kan-E-Senna (Moonbloom) | Il Mheg's faerie lands: the tower, pixie lights, the lake castle, the flower paths, the floating grove. | 4 / 1 | FS 6-5 14.5 → 22.0 | 60 |
| FS 8 The Floating Market | Tataru (Moon-Viewing Draw) | Kugane's harbour market at night: lantern night (the recipe already built), the harbour, floating lanterns, the Ruby Bazaar, the lucky drum. | 4 / 1 | FS 7-5 14.0 → 21.5 | 50 |
| FS 9 The Domes of Sharlayan | Y'shtola (Fireball) | Old Sharlayan (pilot exp-p1 opens it): the Studium's stars, the harbour, the agora's lamps, the sea wall. | 4 / 1 | FS 8-5 13.5 → 21.0 | 55 |
| FS 10 The Archon's Crossing | Louisoix (Sage's Path) | Labyrinthos beneath Sharlayan: the false sky's dome and light, the archons' gardens, the Aitiascope, the crossing. | 4 / 1 | FS 9-5 13.0 → 20.5 | 65 |
| FS 11 The Courier's Wake | the moogle courier (Storm Post) | Our own sky, set nowhere so it opens at every era: the courier's wake, the storm's bolt, the post's trail of stars, the lantern relay, the moogle in stars. | 0 / 5 | FS 10-5 12.5 → 20.0 | 40 |
| FS 12 The Sea of Sorrows | the player picks | The moon itself (pilot exp-p3 opens it): starrise over the grey sea, the craters, the drifting stones, and Moonfall, the full moon, as the game's last level. No loporrits (decision 12). | 3 / 2 | FS 11-5 12.0 → 19.5 | 55 |

How a stage proves its boundary: `mfl.py stage <n>` already plays every level on 5,184 more seeds and compares the opener's
jewels with the previous stage's last level. It reads the previous stage's shipped report, so stages can be built in
parallel only against the planned row here. Once the previous stage ships, re-run its check.

## 4. Stage 4 (Ul'dah): its sources

Stage 1's 1-4 took the middle of the only Thanalan painting in the repo: base02 [800, 0, 2080, 1560], mirrored. The game
designer flagged in rounds 2-6 (G4) that stage 4 had no source list. These are its sources:

| Use | Source | Status |
|---|---|---|
| 4-2 The Spire of the Sands | `ui/loadingimage/-nowloading_base02_hr1.tex`, the east strip, about [2880, 120, 960, 720]: the tall spire, the skyway, and a red craft at the upper right *[confirm on the trace sheet]* | verified path; 960 px, as 2-3 |
| 4-5 Ul'dah from the Sands | Ul'dah's Steps of Nald city map, `ui/map/w1t1/01/w1t101_m.tex`, crop [330, 420, 1160, 870] (the owner's answer: the blurred west strip of base02 was replaced by a city map; sources-plan.md) | verified path; built (MR 4) |
| 4-1 The Steps of Thal | Ul'dah's Steps of Thal city map, `ui/map/w1t2/01/w1t201_m.tex`, crop [660, 740, 920, 690] (sources-plan.md's [560, 620, 960, 720] left the plan's north-west quarter empty) | verified path; built (MR 4) |
| 4-4 The Coliseum | Ul'dah's Steps of Thal city map, `ui/map/w1t2/02/w1t202_m.tex`, crop [560, 640, 960, 720] (the Coliseum is on the Thal map; sources-plan.md) | verified path; built (MR 4) |
| 4-3 Lamps on the Steps | our painting `lampsteps`: light only, no figure | ours |
| Fallback for 4-5, and spare | Central Thanalan's area map, or one of the other Thanalan loading paintings among the 37 (`texdump loading`). 1-1 proved an area map works as a night chart | needs a source |
| Not available | the world painting's Ul'dah lies inside 5-2's Merlthor crop; `world01_m` is already used three times (question 3); `w1f100_m` is mostly inside 1-1's crop | |

## 5. Risks and questions for the owner

1. **The Far Shore starts over.** It opens at about MR 6's difficulty and falls to 11 per 48 on the moon, instead of
   continuing from The Moon Road's 13. *Recommend yes:* twelve more falling stages would go below the 5-of-48 floor, and its
   first two levels have no greens anyway.
2. **Sources.** 55 of 105 levels name a source still to find (`texdump loading` / `maps`), and 5 of them are figurative:
   an airship, ships, a galleon, a wreck and a flagship. Where the game has nothing, may we use an official Fan Kit wallpaper
   without logo or text? That is the method's option (c), shipped as a picture of about 1-2 MB *[assumed size]*. Or do we
   re-subject the level to one of our sky, water or light paintings? *Recommend:* the Fan Kit first. 4-5's 800 px crop is
   the one soft source: accept it, or move 4-5 to Central Thanalan's map.
3. **Repeated charts.** The world painting carries 6 of 115 levels in distinct regions (2-3, 3-1, 3-5, 5-2, FS 4-5, FS 5-2).
   The world map, which is also the Adventure map's own painting, carries 3 (6-3, 11-5 "The Moon Road", FS 6-3). Accept the
   repetition, or cap each at 4 and turn the rest into our paintings? That would bring the share down to about 64%.
4. **Aces on hard levels.** Below about 14 per 48, an Ace set at 20% of all games is close to "any win". Keep the rule, or
   for those levels set the Ace at the top 40% of wins *[a change to `MoonfallAces`]*?
5. **ch-04 Forty-Five Moons** needs one level built outside the 28-35 candidate rule (8-2, 45 candidates, recorded as an
   exception). *Recommend yes;* the alternative is to give ch-04 a different rule.
6. **Repetition within Mor Dhona.** Stages 9 and 10 need seven Mor Dhona crops from perhaps one painting and one map
   *[assumed: the count of Mor Dhona paintings among the 37 is unknown]*. If the sources are thin, MR 10 moves 10-1 and 10-3
   to our water and sky paintings, which drops its game share to 1 of 5. Is that acceptable?

### Owner's answers (7 October 2026)

| Question | Answer |
|---|---|
| 1. The Far Shore's difficulty | **It restarts near Moon Road stage 6** and falls to the moon's finale. |
| 2. Figures with no game picture | **Use official Fan Kit art.** It is an official FFXIV Fan Kit wallpaper without logo or text, shipped as a picture. |
| 3. and 6. Repetition | **Allowed in distinct regions.** No two neighbouring levels share a picture. If Mor Dhona's sources are thin, two of its levels move to our water or sky paintings. |
| 4. Aces on the hardest levels | **The top 40% of wins.** Below about 14 per 48, the Ace is set at the score 40% of winning games reach. This needs a change to `MoonfallAces`. |
| 5. ch-04 | **The recommendation is taken.** 8-2 carries 45 candidates as a recorded exception. |

**Unverified here.**
- The crops and what sits in each region: the chart's airship, dragons, whale, firebird and ships, and base02's red craft.
  They were read from the cached paintings at reduced scale.
- Every *needs* source.
- The shield's area names marked *[assumed]*.
- The ramp, jewel and candidate figures are targets, not measurements. Every level proves its own in the pipeline.
