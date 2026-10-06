# Level-design critic, levels runtime round 1

6 October 2026. Worktree `agent-a570460c913ca1c79` at `c4e9c3db`. I edited, built, committed and stashed nothing in the worktree. I did not run `mfl.py convert --check`, because it runs `dotnet build` first. Instead I called the gate's own functions (`convert.reference`, `runtime_scene`, `compare`) against the built renderer, writing into my scratch folder. I also ran the built `mfcheck.dll` and `MoonfallRender.dll`. Everything I wrote is in `.../scratchpad/levels-b/review-critic/`.

I could not save `report.md` there as you asked: the tool refuses report files from subagents. This message is the full report.

**Overall: the engine draws the ten approved levels faithfully at 2x, and they play as tuned. One Major blocks a clean pass: in the jewel's gamut the engine ships, 2-1 fails F7 (same two jewels as the level before it) against 1-5. The stage check never saw this, because it ran on the clipped-form reports.** I also found eight Minors and seven Nits.

## 1. Verdicts

| Level | Verdict | Why |
|---|---|---|
| 1-1 Road to Horizon | APPROVE | 2x gate max 0.019. Engine pin reproduced by mfcheck. With game art missing, the roads draw over the fallback (m8, Minor) |
| 1-2 Horizon by Night | APPROVE | Faithful to the lightness-kept form, but the most changed from the approved composite: the green water turns dark teal (decision 1) |
| 1-3 The Cactuar | APPROVE | About 15 of its 40 twinkling "stars" sit on the cactuar's body and the moon (m3) |
| 1-4 The Gilded Dome | APPROVE | Worst 2x max (0.036), over 3.75 units² only. Its twinkles land on the dome's architecture (m3) |
| 1-5 The Crystal's Call | APPROVE | Faithful. Its gamut-trimmed aurora is half of M1's pair (the fix may land here or on 2-1) |
| 2-1 Limsa Across the Water | **REVISE** | M1: in the shipped gamut, its jewels (271, 176) are within 30° of 1-5's (275, 202), so F7 fails. The squeeze (decision 2) is faithful |
| 2-2 Moonpath on the Bay | APPROVE | Gamut leaves it unchanged (p99 0.006). Fever swell works. Two twinkles on the moon (m3) |
| 2-3 The Kraken's Sea | APPROVE | 2x max 0.034. At 1x, max 0.127 at the neat-line ticks (n1, report-only) |
| 2-4 The Ferry Under Sail | APPROVE | The Fever moon swell is silently dropped (m4). Twinkles on the moon and sails (m3) |
| 2-5 Twin Lanterns | APPROVE | N21 only partly resolved: (690, 200) is left in 19% of lost games, its mirror in 4% (m5, GD's call) |

## 2. Findings

### M1 (Major), 2-1 and 1-5: F7 fails between 1-5 and 2-1 in the gamut the engine draws

**Evidence**
- I ran `stagecheck.faults` with the stage logs' pooled ramps on two sets of reports (`repcmp.txt`, the inline script's output):
  - On `keepl-build/report`: `F7: base-06 has the same two jewels as the level before it ((275, 202) then (271, 176))`. The hue differences are 4° and 26°, both under 30°.
  - On the approved (clipped) reports: no fault, since 1-5 is (275, 215) and 2-1 is (271, 156).
- This round's `stage1.log` and `stage2.log` show 274/150, 275/215 and 271/156. `cmd_stage` reads `docs/design/v9/levels/report`, so the stage check ran on the clipped form, not on what ships.
- The brief's "passes every pipeline check on all ten" holds per level only. The cross-level check was never run in the engine's gamut.
- The engine matches the keep-L dress to 0.036 at 2x, so these hues are what players see. `img/k05-06.png` shows both boards as violet plus green-teal; the approved pair (`img/a05-06.png`) separated them more.

**Fix**
1. If lightness-kept stays, move 2-1's second jewel back toward its approved green: a region hue near 150–160 at a chroma inside the gamut. Or move 1-5's second jewel to 210° or more.
2. Rebuild in runtime gamut, then rerun `mfl.py stage 1` and `stage 2` on those reports.
3. Make `dress.runtime_gamut()` the pipeline default (`RUNTIME_GAMUT = [True]`), so the repo's reports, composites and stage checks describe what ships.

### m1 (Minor), 1-2, 1-5, 2-4, 1-4: the approved look is not what ships

Runtime bare scene against the approved (clipped) dress at 2x (`gate/gate-all.json`, `vsApproved`):

| Level | p99 | Area over OKLab 0.05 (units²) | Share of the 359k-unit² opening |
|---|---|---|---|
| 1-2 | 0.075 | 44,476 | 12% |
| 2-4 | 0.056 | 24,509 | |
| 1-5 | 0.075 | 18,550 | |
| 1-4 | 0.061 | 10,697 | |
| 1-1 | 0.060 | 5,212 | |
| 2-1 | | 76 | |
| 2-2 | | 14 | |

- **1-2:** the second jewel's hue moves from 150 to 200, and the two jewels go from 125° to 76° apart. The vivid green lower field becomes a dark teal (`img/ak-02.png`, `img/r-02.png`).
- **1-5:** the green aurora darkens to teal.
- Every per-level check still passes in this form, so this is the owner's look decision rather than a play defect.

**Fix:** show the owner `img/ak-*.png` (approved on the left, shipped form on the right) before release, and record the shipped form as the approved one.

### m2 (Minor), converter gate: the statistic is blind to local errors

**Where:** `convert.gate` passes a level when `min(max, p99.9) <= 0.02`, so the 99.9th percentile alone suffices.

**Evidence (`sens.py` at 2x on the pipeline's own recomposed parts)**

| Part dropped | max | p99.9 | Gate |
|---|---|---|---|
| One lamp, 1-4 | 0.44 | 0.000 | PASS |
| One lamp, 2-5 | 0.68 | 0.002 | PASS |
| 1-1's glow | 0.035 | 0.021 | FAIL |
| Any plate | | | FAIL |

- The gate's self-proof only tries a whole-board change (the clipped jewel).
- My rerun reproduces the brief's figures. At 2x:
  - max runs 0.0068 (1-5) to 0.036 (1-4);
  - p99.9 runs 0.0053 to 0.0121;
  - the area over 0.03 is at most 3.75 units² (1-4).
- So today's art has no local error. The gate just would not catch a future one.

**Fix**
- Require both clauses: max ≤ 0.04 and p99.9 ≤ 0.02. All ten pass that today.
- Add a parts-drop self-test (one small light, one glow, the ticks plate) that must fail.

### m3 (Minor), 1-3, 1-4, 2-2, 2-4 (and a few on 1-2 and 2-5): twinkling "stars" land on subjects and moons

**Where:** `MoonfallSceneBuilder.Stars` picks any local luma maximum in `StarRegion` (default y 41–330). It excludes only framing cover and points within 10 units of a piece.

**Evidence:** max−min over 16 frames at `--seconds` 0.05–5.8 (`mot/*-cleared-motion.png`, `img/mot-*.png`, `img/m04-crop.png`).

| Level | Where the twinkles land |
|---|---|
| 1-3 | About 15 on the cactuar's body, 2 on the moon disc |
| 1-4 | Most on spires and the dome facade |
| 2-4 | About 6 on the moon disc, plus the sail edges and the bough |
| 2-2 | 2 on the moon |

The result reads as glitter on the creature and on the moon. It is ambient "noise" among the pegs (owner's taste: no noise).

**Fix**
- Give each recipe a sky mask or `starRegion` poly.
- Exclude moon discs (the fever moon's r × 1.5) and pixels brighter than the sky.
- Or have our painters export the stars they painted.

### m4 (Minor), 2-4: the Fever moon swell never happens

**Evidence**
- The recipe's `feverMoon` is [150, 104, 24]. The builder keeps it only when `ctx.Clear(150, 104) >= R + 18 = 42`.
- Peg (150, 150) r 8 sits 38.0 from the moon's centre, so `Layers.Moon` is null.
- Renders at Fever with `--reduce-motion` (`img/fever-rm.png`): 2-2 swells, 2-4 does not.
- 1-3's clearance (79.7 against 52) passes. The scripted Fever did not trigger there, so it is not rendered.

**Fix**
- Use r ≤ 20 for 2-4 (or move the peg).
- Add a build test that every recipe's `feverMoon`, every mist band and the requested star count survive their level. Today's tests check only that survivors keep clear, so a silent drop passes.

### m5 (Minor), 2-5: N21 is only partly resolved by the post-approval fold-in (`1acee699`)

**Evidence**

| | (690, 200) | Mirror (110, 200) |
|---|---|---|
| Left in lost games (1728 fresh games, seeds from 30001, `play-10.txt`) | 19.2% (top holdout), sole orange 3.4% | 4.1% |
| First touches of 681 (0.25° sweep, `direct7.txt`) | 3 | 15 |

- Was 25.1% and 0 first touches.
- The blue (640, 150) is still first touch 13 times, and (660, 120) has no mirror.
- The layout docstring's "both corner stars are fair shots" is not borne out.
- Ramp is fine: 2-5 is 20.16 per 48 on my 6912 fresh games (seeds 30001–36912), 19.9 pooled with the stage's games.

**Fix (GD's call):** mirror the right star field (or drop (660, 120)), or move the candidate. Re-tune and keep the gap.

### m6 (Minor), aces (decision 5): 48 games are too few for a 75th percentile

**Evidence (`aces/`)**
- `mfcheck play <level> 48 5` reproduces all ten shipped aces exactly.
- On 480 other games (seeds 1001–1480), the share of greedy wins that reach the shipped ace runs from 0.19 (1-3, 2-5) to 0.53 (2-2), and is 0.42 on 1-2. The intent is 0.25.
- 2-2 has the set's lowest ace (260k), but its 480-game 75th percentile is 320k.

**Fix:** run `MoonfallAces.Suggest` over 480 or more games (or the 1728 held-out games), then round.

### m7 (Minor), spoiler tags (decision 4): inconsistent by place

- 1-1's Western Thanalan chart is tagged with its zone.
- 1-2 (Horizon) and 2-2, 2-4 and 2-5 (Vesper Bay, its ferry and gate) depict Western Thanalan places but are "Nowhere".
- The Far Shore's stage 2 (Vesper Bay) is tagged Western Thanalan.
- 1-3 depicts Southern Thanalan.
- Separately, `--story 0` does not exercise the fallbacks: a story staged at ARR "has met everyone", so 1-1, 1-4 and 2-1 render their game paintings (`fb/s0-*.png`).

**Fix (owner's call):** either tag by the place depicted, or treat the Moon Road's stages 1–2 as unshielded throughout. Their stage names already print "The Waking Sands" and "Vesper Bay".

### m8 (Minor), 1-1 with the game painting missing: the engraved roads draw over the fallback

**Where:** `SafeRecipe` (shield) strips paint plates. The builder's missing-painting path (`fallback = true`) does not.

**Evidence:** with `--no-game-art` (`fb/fb-01.png`, right), dashed roads cross the moon-road-night sky and run through the pegs.

**Fix:** drop paint plates whenever the fallback picture is drawn.

### Nits

- **n1, 1x tier (report-only, used for windows below about 808 × 606 board pixels and every thumbnail).** 1-1: p99 0.030, max 0.09, mostly the coast glow's speckle and the map text. 2-3: max 0.127 at the top neat-line ticks. Visually equivalent (`img/g1x-01-crop.png`, `img/g1x-08-top.png`). Consider gating 1x p99 ≤ 0.035.
- **n2, veil (approximate; board aligned to the 1280 renders by template fit, 1.27 px a unit at (228, 83)).** Ring luminance r+3 to r+9 over the bare scene, medians across six levels: pipeline 0.56–0.71, runtime 0.60–0.77. The runtime veil darkens about 4–7 points less than the pipeline's baked veil, so the pipeline's readability margins are slightly optimistic. Worth one calibration.
- **n3, squeeze is automatic.** The converter sets `squeeze` whenever a crop is more than 2% off 4:3, and the loader allows 25%. Make the pipeline recipe declare it.
- **n4, stage figures run optimistic.** On my fresh 6912 games: 2-1 → 2-2 is 0.80 ± 0.40, and 2-4 → 2-5 is 3.55 ± 0.40. Pooled with the stage's games: 1.08 ± 0.28 and 4.04 ± 0.28. Both pass; the stage's 1.37 and 4.52 read high.
- **n5, dead recipes.** `airship-road`, `holy-see` and `lantern-night` still load and are tagged, but no shipped level takes them (only the renderer's staged pilots).
- **n6, loader test changed.** "Every shipped scene has its pictures at both tiers" became "either tier" for pictures the builder cuts. This is justified by the design (2x-only plates are resampled at 1x), and noted for the record.
- **n7, framing check blind to rims.** With plates, the builder's framing check (`check: true`) sees the cover but no rim (`ctx.Rim` stays empty), so its rim rules are vacuous at runtime. The pipeline's framecheck covered them on the dress.

## 3. The six provisional decisions

1. **The jewel's gamut: agree with lightness-kept, on two conditions.**
   - It is the right runtime rule: values hold and pegs read. The keep-L build passes every per-level check, and the engine matches it to 0.036.
   - Condition one: the cross-level F7 fault (M1) is fixed.
   - Condition two: the owner re-approves the look on 1-2 and 1-5 (m1).
   - Then make it the pipeline default so the repo's composites, reports and stage checks are the shipped form.
2. **2-1's squeeze: agree.** It is exactly the approved look, and the gate passes (2x max 0.033). Only n3.
3. **Region chroma 0.3: agree.** The gamut trims it anyway; that trim is why 1-2 moved most.
4. **Shield tags: partly disagree.** Tag consistently by place, or not at all on stages 1–2 (m7). Also make the missing-painting fallback drop paint plates as the shield's does (m8).
5. **Aces: agree with the method, not the sample.** 48 games give 19–53% instead of 25% (m6).
6. **Challenges: agree for now.** They are sealed until all 55 levels exist. But:
   - ch-03 and ch-04 are bound to base-04 and base-03, and with the pipeline's fixed 28 candidates they can never open. They need re-pointing to a level authored with 35 or more and 45 or more candidates, or a different rule.
   - ch-01 (150k with 7 balls on 1-3), ch-05 (clear all on 1-1, the 82-piece board) and ch-07 (600k over 2-1 to 2-3) were set on placeholders. Recalibrate them on these levels before challenges open.

## 4. What I checked, and what I could not

**Checked**
- **Level files:** byte-equal to the approved `json/` on all ten.
- **The deal honours `canBeGreen` and `canBeOrange`:** 600 `mfcheck colours` deals (10 levels × numbers 3, 6, 10 × seeds 1–20). Zero on forbidden pieces, always 2 greens and 25 oranges.
- **Engine pin:** mfcheck play base-01 48 5 reproduces it:
  - game 1: lost, 13 shots, 1 left, 89,760;
  - game 2: lost, 17 shots, 1 left, 172,800;
  - game 10: won, 11 shots, 328,750;
  - 30 wins.
- **Stage mapping:** renders of level select and Quick Play show stage 1 "The Waking Sands" 1-1 to 1-5 and stage 2 "Vesper Bay" 2-1 to 2-5, with names and aces.
- **Moved tests:** I read the changes to the challenge, modes, perf, runtime-art, scene-start, loader and playability tests. All are faithful moves.
- **Mist bands:** clear of every piece on all five levels that have mist (1-3, 2-1, 2-2, 2-4, 2-5), with margins of 9–18.5 units.

**Not checked**
- I ran no tests or builds (worktree read-only), so the C# test suite, including the new canBeGreen test and the gate via `mfl.py`, was not run by me.
- Motion is judged from 16 sampled frames and the code. GPU blending of beams, mist and stars is unverified in game.
- 1-3's Fever swell: the scripted Fever did not trigger, so I checked it by clearance only.
- The real shield fallback: the renderer stages the story by expansion only, so `--story 0` shows game paintings.
- Low-core build time. 1-1 builds in 0.45–0.66 s on 32 threads; one cold run under load took 37 s.
- Human play.
- ch-01's feasibility: mfcheck cannot play 7 balls.

Scripts and outputs are in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/levels-b/review-critic/`: `gate_mine.py`, `gate/`, `sens.py`, `motion.py`, `mot/`, `fb/`, `fev/`, `aces/`, `play-*.txt`, `direct7.txt`, `veil.py`, `repcmp.txt`, `img/`.
