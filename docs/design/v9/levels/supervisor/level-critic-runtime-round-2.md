# Level-design critic, levels runtime round 2

6 October 2026. Worktree `agent-a570460c913ca1c79` at `fa200dc0`. I did not edit, build, commit or stash anything in the worktree. `git status` shows only the other reviewers' two untracked round-2 reports.

I ran none of `dotnet build` or `mfl.py convert`/`build`/`stage`/`selftest`. Instead I:
- called the converter's functions (`reference`, `runtime_scene`, `compare`, `passes`, `dropped`) and `readability.jewels` against the built renderer;
- ran the built `mfcheck.dll` and `MoonfallRender.dll`;
- ran 33 single tests with `--no-build`.

Before relying on the built files I checked that no source in Core, Tests or the plugin is newer than the Core DLL (11:07). Everything I wrote is in `.../scratchpad/levels-b/review-critic/r2/`.

**Overall: M1 is resolved, measured on the engine's own scene. All ten levels pass and play as tuned. My round-1 Minors m2, m4, m5, m6, m8 and nits n1, n3, n7 are resolved; m3 and m7 are mostly resolved.**

One new Minor (m9): the star fix keeps stars off subjects and moons, but at 2x it clusters them on the framing's edges. On 2-4, 11 of 36 stars sit on the garland rope, and the new test cannot see this because it checks the 1x tier only. Owner sign-off on the shipped look (m1) is still the one open item from round 1.

## 1. Verdicts

| Level | Verdict | Why |
|---|---|---|
| 1-1 Road to Horizon | APPROVE | 2x max 0.019, p99.9 0.009; 1x p99 0.030 (rule 0.035). Fallback drops the roads (m8 resolved). Glint runs along the roads |
| 1-2 Horizon by Night | APPROVE | Second jewel back to 151°. Area over OKLab 0.05 against approved went from 44,476 units² to 0 (m1 resolved here). 3 of 36 stars on the palm-frond edge (m9) |
| 1-3 The Cactuar | APPROVE | 0 of 40 stars on the cactuar or the moon (m3 resolved). Fever moon kept |
| 1-4 The Gilded Dome | APPROVE | No stars now. Only lamps flicker and beams breathe. Worst 2x max 0.036, still under 0.04 |
| 1-5 The Crystal's Call | APPROVE | F7 against 2-1 holds. Aurora still teal against approved (18,550 units², owner sign-off). 4 of 30 stars on the palm (m9) |
| 2-1 Limsa Across the Water | APPROVE | **M1 resolved.** On the engine's scene, jewels are (271, 154) against 1-5's (275, 202): 4° and 48°, F7 passes. The squeeze is declared |
| 2-2 Moonpath on the Bay | APPROVE | No stars on the moon. Unchanged otherwise (p99 0.008 against approved) |
| 2-3 The Kraken's Sea | APPROVE | Richer jewel (UX m1), mean chroma 0.066, 77° apart. This look is newer than the approved one (7,838 units² over 0.05): owner sign-off |
| 2-4 The Ferry Under Sail | APPROVE (fix m9 before release) | Moon and sails are clear of stars, but 11 of 36 stars twinkle along the garland rope at 2x (m9). The Fever moon is no longer named (m4 resolved) |
| 2-5 Twin Lanterns | APPROVE | m5 resolved: both corner stars take 15 of 681 first touches, the blockers 1 each. Finale gap 3.05 ± 0.41 on my fresh games. 5 of 24 stars on the top-left branch (m9) |

## 2. Findings

### Round-1 findings, status

**M1 (Major), F7 between 1-5 and 2-1: RESOLVED**

How I measured it:
- I ran `readability.jewels` on MoonfallRender's own 2x bare scene for all ten levels (`r2/gate2.py`, padded into the 1600×1200 canvas the function expects).
- I fed those hues into `stagecheck.faults` with the stage ramps and `before` = 1-5 (`r2/f7.py`).

Results:
- Stage 1 and stage 2: no faults, both on the engine's hues and on `docs/design/v9/levels/report/`.
- The engine's hues equal the reports' on all ten (2-5: 208 against 209).
- 1-5 → 2-1: the closer pairing is 48° apart (the rule needs 30°). The tightest neighbour pair in the set is now 1-3 → 1-4 at 37°.
- `dress.RUNTIME_GAMUT = [True]` (`tools/moonfall-levels/mflkit/dress.py:82`), so the repo's reports and stage checks describe what ships.

**m1 (Minor), the approved look is not what ships: PARTLY RESOLVED, owner action open**

Engine 2x scene against the round-6 (clipped) dress, drawn with `clipped_gamut()`:

| Level | Area over OKLab 0.05 (units²) | p99 | Note |
|---|---|---|---|
| 1-2 | 0 | 0.038 | Fixed (was 44,476) |
| 2-1 | 0 | 0.035 | |
| 2-4 | 24,509 | 0.055 | |
| 1-5 | 18,550 | 0.075 | |
| 1-4 | 10,680 | 0.061 | |
| 2-3 | 7,838 | 0.066 | New (UX m1) |
| 1-1 | 5,288 | 0.060 | |

- The `approved-vs-shipped-base-NN.jpg` sheets are correct and readable; I viewed 1-2, 1-5, 2-1, 2-3 and 2-4.
- Nothing in the repo records that the owner has seen them or re-approved 1-5, 2-4, 1-4 and 2-3.
- **Fix:** show the owner the sheets for those four levels and record the answer (for example in `tools/moonfall-levels/README.md`'s gamut paragraph).

**m2 (Minor), the converter gate's statistic: RESOLVED**

- `passes()` now requires max ≤ 0.04 AND p99.9 ≤ 0.015 at 2x; at 1x, p99 ≤ 0.035. A level also fails if the build drops anything.
- My rerun at HEAD reproduces 20 of 20 passes:
  - 2x: max 0.007–0.036, p99.9 0.005–0.013;
  - 1x: p99 0.005–0.030.
- My 2x drop test (`r2/sens2.py`) now fails every case:

| Dropped | Figure | Result |
|---|---|---|
| One lamp, 1-4 | max 0.441 | FAIL |
| One lamp, 2-5 | max 0.677 | FAIL |
| 1-1's glow | p99.9 0.0209 | FAIL |
| Cloth plate, 1-4 | p99.9 0.0198 | FAIL |
| Cloth plate, 2-5 | p99.9 0.0201 | FAIL |
| Rim plate, 2-5 | | FAIL |

- The self-test cases (`v2-self.log`) all behave as intended.
- Nits on this are listed under n-a below.

**m3 (Minor), stars on subjects and moons: RESOLVED for subjects and moons; see the new m9**

Overlays of MoonfallRender's `starsAt` on the 2x scene (`r2/stars/*-stars.png`) and 16-frame motion maps (`r2/mot/*-cleared-motion.png`) show:
- 1-3: 0 stars on the cactuar or its moon;
- 1-4: no stars;
- 2-2: none on the moon;
- 2-4: none on the moon or the sails.

**m4 (Minor), 2-4's Fever moon: RESOLVED** (no Fever moon is named any more). Two things now guard against a silent drop:
- the new test `What_the_recipe_asks_for_survives_its_level` (passed);
- the gate's `dropped()` check.

**m5 (Minor), 2-5's corner stars: RESOLVED**
- First-shot sweep (`r2/direct7-r2.txt`): (690, 200) and (110, 200) take 15 first touches each; the blockers (660, 150) and (140, 150) take 1 each.
- 1728 fresh games (seeds 30001+, number 10, `r2/play-10.txt`):
  - (690, 200) is left in 5.6% of lost games (sole orange 0.8%), its mirror in 4.3%. It was 19.2% against 4.1%.
  - The ramp is 21.25 per 48.
- 6912 fresh games per level (`r2/ramp/`): 2-4 → 2-5 is 3.05 ± 0.41, resolvable at 2.5. Pooled with the stage's games: 3.26 ± 0.29.

**m6 (Minor), aces: RESOLVED** (`r2/aces/`)
- `mfcheck play <file> 576 5 0` reproduces all ten shipped aces exactly.
- On 960 other games (seeds 5001+), the share of games winning at or above the ace is 19.0–23.6%, ± 1.3% each.

**m7 (Minor), spoiler tags: RESOLVED as tags.** Every scene is tagged by the place it depicts and has a placeless fallback. `--hide-zone` renders were checked for 1-1, 1-2, 1-3 and 2-1 (`r2/fb/`). The one remaining inconsistency is n-e below.

**m8 (Minor), the fallback drops paint plates: RESOLVED.** 1-1 rendered with `--no-game-art` shows no roads (`r2/fb/a.png`, left). A residual is n-c.

**Nits from round 1**

| Nit | Status | Evidence |
|---|---|---|
| n1, 1x tier | Resolved | Gated at p99 ≤ 0.035 |
| n2, veil calibration | Open | Declared not done |
| n3, squeeze | Resolved | Declared in `docs/design/v9/levels/scenes/limsa-across-water.json:16`; the converter now raises on an undeclared non-4:3 crop |
| n4, optimistic stage figures | Open, unchanged | My fresh 6912 games: 2-1 → 2-2 is 0.81 ± 0.40, not resolvable on fresh games alone. Pooled 13,824: 1.09 ± 0.29, passes |
| n5, dead recipes | Open | `airship-road`, `holy-see`, `lantern-night` still load |
| n6, loader test | Noted | |
| n7, framing check and rims | Resolved | Plates carry `"rim": true`; the loader requires screen blend; the builder fills `ctx.Rim` (`MoonfallSceneBuilder.Masks.cs:404`) |

### New findings

**m9 (Minor): at 2x, stars sit on the framing's edges (2-4 worst; also 2-5, 1-5, 1-2)**

Where: `MoonfallSceneBuilder.Stars` (`Tsukimichi.Core/Moonfall/Art/MoonfallSceneBuilder.cs:828-892`). It rejects a candidate only when its own pixel has `ctx.Cover > 0.05`. A sky pixel right beside a dark silhouette (a rope or branch) is a strong local maximum against its blurred surroundings, so candidates line the framing's edge. The recipes' `starWhere` sky polygons include those edges.

Evidence, stars within 4 units of the cover plate (`r2/starcover.py`). Most sit 0–1.5 units from full cover:

| Level | 2x | 1x |
|---|---|---|
| 2-4 | 11 of 36 | 3 |
| 2-5 | 5 of 24 | 1 |
| 1-5 | 4 of 30 | 1 |
| 1-2 | 3 of 36 | 2 |

- 2-4's motion map (`r2/mot/m09.png`; crop `r2/stars/crop09.png`) shows the twinkles strung along the top-left garland rope. This is the "glitter on the picture" that m3 and GD M1 asked to remove.
- The new test checks stars only at the 1x tier (`Scene1x`), where only 3 land there. It also uses the same `starWhere` mask the builder does, so it passes (I ran it).

**Fix:**
1. In `Stars()`, reject candidates within about 4 units of cover above 0.05 (dilate `ctx.Cover` once), or add a framing `not-mask` term to every `starWhere`.
2. Run the survival test at both tiers, asserting each star keeps that distance from cover.

**Nits**

- **n-a, gate self-test and margin.**
  - `selftest`'s `bad_case` measures at 1x but judges with the 2x rule (`passes(bad, 2)`).
  - The 1x rule (p99 ≤ 0.035) passes every single-part drop except the framing. Lights and glows give p99 0.000; the cloth plate 0.016–0.017. So the 1x tier alone is blind to a dropped part. Low risk, since one recipe builds both tiers and `dropped` covers parts.
  - The p99.9 margin is thin: 2-3 is at 0.0131 against 0.015.
- **n-b, 2-4's "two lanterns" never draw.**
  - The commit and brief say they flicker. But (246, 372) and (612, 362) sit inside pegs (clearance −8.3 against the 10.4 needed), so `small_lights` drops them in both the pipeline and the runtime. HEAD reports `flickers: 3` (the portholes only).
  - `gate-r2.log` (11:03) shows `flickers: 6` for base-09, so it predates the final 2-4 recipe (11:04 pipeline, 11:07 runtime). My rerun at HEAD passes with identical figures.
  - Fix: drop the two points from `docs/design/v9/levels/scenes/ferry-under-sail.json`, or move them, and correct the note.
- **n-c, two different stand-ins.**
  - With the painting missing, the builder draws the full recipe over the fallback picture: grade, palette, regions and glows. On 1-1 that gives a rose-brown band (`r2/fb/a.png`, left).
  - The shield's story-safe form is ungraded (right of the same image).
  - F6 is tested only over the story-safe form.
  - Fix: use `MoonfallSceneBuilder.StorySafe(recipe)` for the missing-painting path too.
- **n-d, independent star sets.** The 1x and 2x tiers pick different star positions, which are not scaled from each other. That is harmless, but the test covers 1x only (see m9).
- **n-e, unshielded stage names (owner's call).** The Moon Road's stage names "The Waking Sands" and "Vesper Bay" (`Tsukimichi.Core/Moonfall/MoonfallStages.cs:56-57`) are never shielded (`OfStage` → Nowhere). Their scenes are hidden when Western Thanalan is.
- **n-f, stale docs.**
  - `tools/moonfall-levels/README.md:250` still says "the shafts' 15%".
  - `docs/design/v9/levels/layouts/base-10.py:63` says "the right field is now the left's mirror", but (680, 300) does not mirror (130, 300). Its mirror would be 670. Effect negligible.

## 3. Checked, and not checked

**Checked**
- **Level files:** all ten shipped files are byte-equal to `docs/design/v9/levels/json/`, including 2-5's new file. 2-5's mirrored peg (660, 150) is `canBeOrange false, canBeGreen true`. Reach shows none unreached.
- **The deal:** 600 `mfcheck colours` deals (10 levels × numbers 3, 6, 10 × seeds 1–20) put zero greens or oranges on forbidden pieces, always 2 greens and 25 oranges.
- **Tests:** these passed with `--no-build` (33 of 33):
  - `What_the_recipe_asks_for_survives_its_level`
  - `Every_peg_reads_against_its_story_safe_scene`
  - `Every_peg_reads_against_its_scene_and_...`
  - `Peg_marks_sit_on_every_brick`
  - `..._deal_their_greens_only_where_canBeGreen_...`
  - `A_missing_painting_falls_back...`
- **Reports:** all ten `report/*.json` verdicts are PASS.
- **Renders:** glints on 1-1, no stars on 1-4, marks on 2-1's bricks.

**Not checked**
- The full test suite: I rely on `v2-test.log`, 8954 passed.
- Motion and GPU blending in the game: judged from 16 sampled frames only.
- 1-3's Fever swell: the `fever` moment with reduce-motion showed no visible swell; that is the game designer's n1.
- The real shield in a fresh story (which Western Thanalan scenes a new character actually sees).
- Human play.

Scripts and outputs are in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/levels-b/review-critic/r2/`:
- scripts: `gate2.py`, `f7.py`, `sens2.py`, `stars.py`, `starcover.py`, `deals.py`, `motion2.py`;
- outputs: `gate/gate-all.json`, `stars/`, `mot/`, `fb/`, `aces/`, `ramp/`, `play-10.txt`, `direct7-r2.txt`.
