# Game designer, levels round 4

6 October 2026. Worktree `agent-a570460c913ca1c79` at `b32a74e2`; round 3 was reviewed at `35cd98b5`. I edited no repo files and did not run `build`, and `git status` is clean after my runs. Two side effects, both in the gitignored `build/`:
- `mfl.py selftest` rewrote its four fixture boards.
- `mfl.py ease base-01` wrote `build/json/base-01-ease.json`.

**Overall verdict: APPROVE.** No Major. The round-4 changes resolve or improve G10, G12, G13 (in the pipeline) and G15. Three Minors remain:
- **G11 residue:** 1-1's bridge pair and 2-5's reflection still decide most losses.
- **G19, new:** stage 1's ramp is now flat and then drops sharply. 1-1 and 1-2 are tied on fresh seeds, then a 3.6 drop to 1-3 that this round's blue star caused.
- **G18, new (G14 escalated):** with the quiet in the runtime's form, the second jewel survives mainly where the layout is not. It prints the layout's envelope on the cleared board: red wall strips on 1-3 and 2-4, a blue window on 2-4, pale columns and a ghost U on 2-5, and a violet silhouette on a maroon field on 1-1.

I would ship these levels as they are. I would fix G19 and the 1-1 part of G11 before release, because each is a one- or two-peg change.

## Method

**Read:**
- `round4-context.md` and `round2-context.md`;
- my round-3 report, and the UX and critic round-3 reports;
- the full diff `35cd98b5..b32a74e2`: layouts base-01, 03, 04, 05, 07 and 10; scenes for crystal-call, ferry, cactuar, gilded dome, moonpath and twin lanterns; `dress.py`, `engine.py`, `stagecheck.py`, `build.py`, `mfl.py`, mfcheck `Program.cs`; the README;
- all ten reports, against round 3's (extracted with `git show`);
- main's `MoonfallClearance.Far` (96) and the `near` term in `MoonfallSceneBuilder` (`Smooth(a0, a1, blurred clearance)`).

**Ran:**
- `mfl.py stage 1` and `stage 2`: both exit 0, all PASS.
- `mfl.py selftest`: all ok, including the new stage cases (swapped palette, previous stage's last level, much easier than band).
- `mfl.py ease base-01`.
- **Fresh ramp.** `mfcheck play <json> 2592 <n> 2592` for all ten: 2592 greedy games per level on seeds 2593–5184, outside both the tuning block (1–864) and the build's held-out block (865–2592). The offset works: 1-1's first 48 games win 32, against 28 on seeds 1–48. The standard error is ±0.47 per 48. Pooled with the report's 1728 held-out games, that is 4320 untuned games per level (±0.36).
- **Who decides lost games.** My parse of the logs, with holdouts keyed by home, at y ≥ 400, 415 and 430.
- **Scratch variants**, played on the same fresh seeds, paired:
  - 1-3 without this round's star (200, 180);
  - 1-1 with the bridge candidate moved back from (533, 398) to (533, 366).
- **Images:**
  - round 3 against round 4 composites for 1-1, 1-3, 1-5, 2-5 and others;
  - contact sheets of all ten dressed scenes (the runtime's cleared board, no veil), set beside my round-3 sheets;
  - 2x crops of 1-3's dunes and 2-5's sky;
  - chroma profiles across envelope edges;
  - where each board's second-jewel pixels lie (wall strips, and more than 60 units from any piece).
- **Scratch:** `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/gd4/`:
  - play logs in `ramp/` and `var/`;
  - scripts `ana.py`, `hold.py`, `j2where.py` and `prof.py`;
  - images in `img/`.
- **Not modelled:** powers, the runtime converter, `MoonfallRender` output, the per-piece veil in play, and human play.

### Key figures

| Level | Pieces | Ramp /48, report (held-out 865–2592) | Ramp /48, fresh (2593–5184) | Ramp /48, pooled (4320 games) | Lost games with an orange at y ≥ 400 left | Top holdouts (share of lost games) | Games where the stuck rule fires |
|---|---|---|---|---|---|---|---|
| 1-1 Road to Horizon | 82 | 28.8 | 29.00 | **28.92** | 0.43 | **(504, 415) 30%, (533, 398) 26%**; either in 46% | 5.3% |
| 1-2 Horizon by Night | 67 | 28.2 | **29.20** | **28.80** | 0.57 | (606, 416) 19%, (612, 326) 18% | 2.5% |
| 1-3 The Cactuar | 70 | 25.1 | 25.26 | 25.20 | 0.41 | (387, 415) 23%, (567, 404) 22% | 2.3% |
| 1-4 The Gilded Dome | 70 | 23.7 | 23.50 | 23.58 | 0.42 | (556, 420) 22%, (517, 386) 20% | 1.4% |
| 1-5 The Crystal's Call | 71 | 20.4 | 20.67 | 20.56 | 0.62 | (361, 336) 23%, (519, 336) 23% | 1.0% |
| 2-1 Limsa Across the Water | 74 | 27.8 | 27.41 | 27.57 | 0.41 | (330, 400) 24%, (128, 364) 19% | 3.4% |
| 2-2 Moonpath on the Bay | 82 | 26.6 | 26.61 | 26.61 | 0.54 | (548, 424) 20%, (314, 400) 18% | 1.6% |
| 2-3 The Kraken's Sea | 77 | 25.3 | 25.52 | 25.43 | 0.48 | **(558, 156) 41%**, then 17% | 4.1% |
| 2-4 The Ferry Under Sail | 77 | 24.2 | 23.83 | 23.98 | 0.33 | (463, 402) 20%, (319, 399) 18% | 0.5% |
| 2-5 Twin Lanterns | 74 | 19.2 | 20.06 | 19.72 | **0.78** (all of them at y ≥ 437) | (576, 471) 20%, (224, 471) 19%, (632, 471) 18% | 1.4% |

**Steps, pooled:**
- Stage 1: 0.12, **3.60**, 1.62, 3.02.
- Stage 2: 0.96, 1.18, 1.45, 4.26.
- Fresh seeds alone: 1-1 → 1-2 is **−0.20**. 1-2 measures easier than 1-1.

## Coordinator's decisions: met or not

| Decision | Status | Evidence |
|---|---|---|
| Stage 1 from about 30 down to 21 | Met | Pooled 28.9 down to 20.6 (opens 1.1 inside the 1.2 tolerance) |
| Stage 2 from about 27 down to 20 | Met | 27.6 down to 19.7 |
| Each finale the hardest, 2.5 or more below its 4th level | Met | 1-5: 3.0 below 1-4. 2-5: 4.3 below 2-4 |
| Each level at least 0.5 harder than the one before (`stagecheck`) | **Not met on untuned seeds at 1-1 → 1-2** (G19) | Pooled 28.92 against 28.80 (step 0.12 ± 0.5). Fresh 29.00 against 29.20. Passes only on the held-out point estimates (0.6). 2-1 → 2-2 is fixed (0.96) |
| No cheap difficulty | Met by the gate. Not by intent on 2-5, and partly on 1-1 (G11) | See G11 |
| Ghost discs: quiet by low frequency, never per peg | Met. No peg-scale coins on any dressed scene I viewed. Layout-scale prints grew (G18) | See G18 |
| No painting twice in a row; game-art share | Unchanged; recorded as a budget | — |
| Fullness (stage 2 about 75 pieces) and about 2% framing | Mostly met | Pieces 74, 82, 77, 77, 74. 2-5 framing still 1.49% with 27 elements dropped |
| No dead first shots | Met | `stage`: all PASS; 2-2's 0.16° lane filled at (538, 346) |

## Verdicts

| Level | Verdict | Reads | Open findings |
|---|---|---|---|
| 1-1 Road to Horizon | APPROVE | Yes: the route of stops | G11 (bridge pair), G19 (tie with 1-2), G18 (maroon field with violet silhouette) |
| 1-2 Horizon by Night | APPROVE | Yes (unchanged layout) | G19 (no harder than 1-1), G18 (dark bowl printed in the green field) |
| 1-3 The Cactuar | APPROVE | Yes | G19 (the new star makes a 3.6 drop), G18 (red wall strips) |
| 1-4 The Gilded Dome | APPROVE | Yes | — |
| 1-5 The Crystal's Call | APPROVE | Yes. The aurora now reads as a faint teal band rather than green | N-a (F7 at both floors) |
| 2-1 Limsa Across the Water | APPROVE | As before (the canopy outline; "Limsa" mostly from the title) | — |
| 2-2 Moonpath on the Bay | APPROVE | Yes | — |
| 2-3 The Kraken's Sea | APPROVE | Yes | G17 (one peg in 41% of losses) |
| 2-4 The Ferry Under Sail | APPROVE | Yes, the set's strongest | G18 (red side curtains; blue window on the cleared board) |
| 2-5 Twin Lanterns | APPROVE | Yes. The teal high sky over rose reads as twilight | G11 (reflection), G18 (pale columns, ghost U), G6 residue |
| **Set** | **APPROVE** | | |

## Status of round-3 findings

### G10 (Minor: tuning-seed bias; 2-1 and 2-2 tied): RESOLVED in mechanism and for 2-1/2-2. One tie moved to 1-1/1-2 (G19)
- **The mechanism.** `engine.play(first=…)` and mfcheck's seed offset work. The build now reports seeds 865–2592, and the README says to tune only on 1–864.
- **The bias is gone.** Fresh minus held-out over the ten levels:

  | Level | Fresh minus held-out |
  |---|---|
  | 1-1 | +0.2 |
  | 1-2 | +1.0 |
  | 1-3 | +0.16 |
  | 1-4 | −0.2 |
  | 1-5 | +0.27 |
  | 2-1 | −0.39 |
  | 2-2 | 0.0 |
  | 2-3 | +0.22 |
  | 2-4 | −0.37 |
  | 2-5 | +0.86 |

  The mean is +0.18, with a standard error of about 0.23, so it is noise. Round 3's was +0.80.
- **2-1 and 2-2 are separated.** 2-1 is 27.57 and 2-2 is 26.61 pooled, a step of 0.96.
- **The new tie.** The same statistical weakness now shows at 1-1 → 1-2; see G19.

### G11 (Minor: low oranges decide losses on 2-5 and 1-1): PARTLY RESOLVED; still Minor
**1-1.**
- (368, 427) is now blue (the critic's N1).
- (504, 415) is still a candidate. This is forced: the spread rule needs a right-half candidate at y 400–430, and `ease` shows (504, 415) is the only peg there.
- This round also moved the bridge's other candidate down, from (533, 366) to **(533, 398)**. `ease` ranks (533, 398) 11th hardest of 82 places. It is now the second holdout, at 26%.
- **Result:** the two bridge moons decide **46%** of lost games (round 3: 58%). At y ≥ 400 the ratio is 3.2× (round 3: 3.7×), still the set's highest.
- **Paired check** (same seeds), with (533, 366) restored: 1-1 eases from 29.00 to 29.35. (533, 366) is then left in 20% and (504, 415) in 32%.
- **Diagnosis:** the bridge ring shields its low moon. The place, not the line at y 430, is the problem.

**2-5.**
- The U's foot (400, 514) is blue. The author tried my full fix (U out, heads at 3 of 5) and measured about 26 held-out, so it was reverted.
- On fresh seeds, **78%** of lost games leave a low reflected orange. That is 51% of the leftovers against 43% of the deal. The top four holdouts are the reflected heads at y 471 (17–20% each).
- **The author's own experiment is the key evidence.** It shows about 6.8 per 48 of the finale's difficulty is the low reflection. Without it, 2-5 (about 26) would be easier than 2-3 (25.4) and 2-4 (24.0).
- So the finale meets "hardest in its stage" through the very thing decision 1 calls cheap. The round-3 context accepted it as the subject ("low by nature … within the ratio"). I keep it Minor on that basis, and I name it plainly so the coordinator rules on it knowingly.

**Fix.**
- **1-1.** Add one peg to the right half at y 400–425, in open route ground away from the bridge ring. For example, a route moon east of the bridge near (580, 410), clear of the ring's shield; check it with `trace` and `ease`. Make it the spread rule's candidate, make (504, 415) blue, and return the bridge candidate to (533, 366) or an upper ring moon. The expected result is about 29.5–30, which also opens the stage nearer its band (G19).
- **2-5.** Keep the U foot blue, make the heads 3 of 5, and buy back about 6 per 48 above the waterline. Use 2–3 blue stars in the sky lanes the greedy player uses, and move candidates to the lantern crowns' outer moons and the high corners. The README's "one peg moves it 2–4" says this is two or three changes.
- Or the coordinator states that a finale may draw its difficulty from a low subject.

### G12 (Minor: 1-5's crystal foot a candidate): RESOLVED
- `json/base-05.json` peg 7 at (440, 478) has `canBeOrange: false`, as documented.
- 1-5 now has no candidate at y ≥ 430, and the foot is gone from the holdouts.
- The finale gap held: 3.0 pooled. The blue field star (150, 200) was added for it.

### G13 (Minor: the fix may not survive the runtime): RESOLVED in the pipeline; the runtime is still unverified
- `dress.py` now draws `smooth(a, b, blur(min(dist, 96), 40))`. This matches main's `Palette` (the clearance capped at `Far` = 96, a blur of at most 40 before the smoothstep, no gain).
- So F9 and the print describe what the game will draw. The converter is still missing, so `tone`, rim fills and poly masks are untested in the game.
- The side effect is G18.

### G14 (Nit: the quiet prints the layout's envelope): ESCALATED to Minor as G18
The runtime form made it far stronger.

### G15 (Nit: neighbours across stages, swapped pairs): RESOLVED
- `stagecheck.same_jewels` compares pairs either way round.
- `mfl.py stage` passes the previous stage's last level, and both behaviours have self-tests.
- 1-5 (275/215) → 2-1 (271/156): 59° apart in the second jewel.
- 2-4 (291/15) → 2-5 (355/228): different pairs now that 2-5's second jewel is teal.

### G16 (Nit: bottom thirds emptied): UNCHANGED, Nit
- Nine of ten boards now have no candidate at y ≥ 430. 1-5 joined them; 2-3 keeps (564, 446), plus 2-5's reflection.
- Still fine for stages 1–2. Revisit for stage 3+.

### G17 (Nit: 2-3's (558, 156) decides a large share): UNCHANGED; the evidence is stronger
- It is now left in 41% of lost games on fresh seeds (round 3: 38%).
- The author's swap took 2-3 to 28 held-out, so this one placement carries about 2.7 per 48.
- It is a fair edge-of-reach skill shot, not cheap difficulty, so it stays a Nit.
- **Fix:** spread the difficulty. Make it blue, and promote two moderately hard flock moons, such as the V's two outer tips, rather than one very hard one. Re-measure on held-out seeds.

### Residues
- **G6 (2-5 framing 1.49%, 27 elements dropped):** unchanged, Nit.
- **G7 (stuck positions not logged in play):** unchanged, Nit. 1-1 is the highest, at 5.3% of games.
- **G4 (no stage-4 Ul'dah source list):** unchanged, Nit.

## New findings

### G18 — Minor — Runtime-form quiet: the second jewel now survives mainly where the layout is not, printing the layout's envelope (red wall strips, a blue window, pale columns)
**Evidence**
- **The cause.** With the blur before the smoothstep, every cluster gets full quiet out to about `a` plus 40 units. The region colour then survives only beyond the layout. This round also raised region chroma to hold F7: 2-4 rose 0.125 → 0.18; 1-3 0.08 → 0.12 with a moved band; 1-5 and 2-5 re-tuned.
- **Visible on the dressed scenes** (the runtime's cleared board, `build/dressed/*.png`, `img/dressed1.png` and `img/dressed2.png`; compare round 3's `gd3/lv/dressed*.png`):
  - **2-4:** a dark-blue rounded rectangle round the ship and swell rows, on a saturated red sea at both sides and the foot. In play, it shows as red strips down both walls.
  - **1-3:** red slabs at both walls across the dunes, with near-vertical edges, plus a red foreground. In play they read as red light leaking from the walls (`img/c03L.png`).
  - **2-5:** pale mauve columns round each lantern and the arch in the rose band. A pale ghost of the reflected U stays in the water after the board clears (`img/d10.png`).
  - **1-1:** the open map is now maroon, round a violet silhouette of the route. The second-jewel share doubled, 0.18 → 0.36, with no recipe change.
  - **1-2:** a dark bowl printed in the brightened green field under the ledge rows.
- **Measured.** I classified the second jewel's pixels by F7's own bins on the dressed scenes:
  - 2-4 has 34% of its second jewel in the 50-unit wall strips, which are 15% of the area (2.3×). 2-3 has 31% (2×) and 1-1 27%.
  - 1-1 has 75% of its second jewel more than 60 units from any piece, an area of 32%.
  - So on 2-4 and 1-3, F7's 15% floor is now met largely by colour at the walls, where the player neither aims nor looks for the picture.
  - The edges are soft: about 40 units, with a chroma change of 0.03–0.04 (2-4 at y 420). The region inside is large and uniform, so the shape reads.
- **Why the gate misses it.** The print gate is peg-scale: band pairs and a sector median, so a half-plane edge at a cluster's rim does not count. Even so, its p90 rose on 1-1 (0.022 → 0.043), 1-2 (0.021 → 0.059), 2-4 (0.009 → 0.029) and 2-5 (0.015 → 0.028). The threshold moved 0.026 → 0.070 with the new texture-normalised measure, so the values are not strictly comparable; that is the UX supervisor's to judge.
- **Why it matters.** The cleared board is the reward moment. A layout-shaped window or curtains is the layout's ghost at the scale of the whole board. The binding rule says "never per peg" and is met; the spirit, nothing of the layout stays when it clears, is not.

**Fix**
- Let regions carry less of the quiet than the jewel. Use a smaller region scale (for example 0.3 instead of 0.6), or no region quiet where the region is painted.
- Shape regions by the painting, not by the layout's absence:
  - 2-4: put the rose into the painting's sky and sea with `paint`, not as a band the quiet cuts.
  - 1-3: confine the band to the dunes' ridgelines with a luma or poly mask, instead of `y` alone.
- Add a layout-scale check: the share of the second jewel in the 50-unit wall strips at most about 1.5× their area, and the second jewel's correlation with the layout's clearance below a set limit.
- Review 2-4, 1-3, 2-5 and 1-1 by eye on the dressed scene.

### G19 — Minor — Stage 1: 1-1 and 1-2 tied, then a 3.6 drop to 1-3 that this round's blue star made
**Evidence**
- Pooled over 4320 untuned games: 1-1 28.92, 1-2 28.80, 1-3 25.20, 1-4 23.58, 1-5 20.56.
- On fresh seeds alone, 1-2 (29.20) is *easier* than 1-1 (29.00). `stagecheck` passes 1-1 → 1-2 only on the held-out point estimates (28.8 against 28.2). Each of those has a standard error of 0.55, so a step of 0.6 is about 0.8 standard errors of the difference.
- Neither layout caused the tie. 1-2 is unchanged, and 1-1 measures as in round 3 (28.93). Round 3's fresh step was already only 0.43.
- **The drop.** The new 1-3 star (200, 180) costs **1.7 per 48** (paired variant without it: 26.94 against 25.26). Round 3's stage 1 was 29.0, 28.3, 27.0, 24.9, 21.9 with steps 0.7, 1.3, 2.1, 3.0. Now it is 0.1, 3.6, 1.6, 3.0.
- The first board of the campaign then plays the same as the second, and the third is the stage's biggest jump.

**Fix** (one change per level):
- Ease 1-1 with G11's bridge fix. The paired variant shows +0.35 from (533, 366) alone, and the full fix likely gives about 29.5–30.
- Harden 1-2 by about 1.5 with one blue star in an open sky lane above the roofline. That is the README's lever: about 0.5–1 each, or a high candidate moved to an edge.
- Keep 1-3's star. The target is about 29.6, 27.3, 25.2, 23.6, 20.6.
- **Tool:** have `stage` report the step against its noise. Flag a step under about 1.0 for a re-measure on another 1728 seeds before it passes, since 0.5 cannot be resolved at ±0.55 per level.

### N-a — Nit — 1-5's F7 sits on both floors
- The report gives jewels 275/215, **60° apart (floor 60)**, second share **0.158 (floor 0.15)**. Both margins are under 0.01 of their limits.
- The aurora moved to aquamarine and the corner green is gone. It now reads as a faint teal band. That is a good change for colour-blind players: protan p10 is 0.101 → 0.123, and the minimum is 0.062 → 0.112, resolving UX m3. But any grade change will trip F7.
- **Fix:** widen the milky-way region a little, or raise its chroma by about 0.02 where it is clear of candidates. The `keepMask` already exists. Then re-check protan.

### N-b — Nit (cross-reference for UX) — protan minimums fell on boards whose recipes did not change
- **1-2:** min 0.126 → **0.084** (p10 0.137 → 0.126).
- **2-3:** min 0.113 → 0.087 (p10 0.117 → 0.109).
- **1-4:** p10 0.117 → 0.110.
- These come only from the quiet's new form: the region colour is stronger in open ground.
- All pass F9 on the p10 clause, so this is the UX supervisor's call. I record it because it is a regression this round brought on unchanged layouts.

## What works
- **The ramp is now measured honestly.** Held-out seeds removed the 0.8 bias, and fresh seeds agree with the reports to within noise.
- **Stage 2's ramp** is clean (27.6, 26.6, 25.4, 24.0, 19.7), and both finale gaps clear 3.0 or more.
- **The quiet matches the runtime's formula.** What the pipeline measures is what main will draw, with no peg-scale coins on any dressed scene.
- **Small fixes delivered as documented:** 1-5's foot is blue, 2-2's dead lane is filled, and `stagecheck` checks swapped palettes and the stage boundary.
- **Palettes:**
  - 2-5 rose with a teal high sky now differs clearly from 2-4's violet and rose, and reads as twilight.
  - 1-5's aquamarine aurora fixes its colour-blind floor.
- **Variety and reads** are unchanged from round 3. These are ten distinct subjects; 2-4, 1-3 and 2-2 are the strongest.
