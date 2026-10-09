VERDICT: APPROVE (0 blockers, 0 majors, 0 minors)

I worked read-only in the worktree at HEAD `cca6c2c5`, which had no uncommitted changes. Physics is unchanged since `mfcheck.exe` was built at 07:12: since then only Art files, level JSON and `aces.json` changed. The level files I measured are the shipped `Tsukimichi.Core/Moonfall/Levels/base-16..20.json`, and each is byte-identical to `docs/.../json`.

What I ran:
- **Fresh games.** `mfcheck play` on four seed blocks no one has used (100001+, 110001+, 120001+, 130001+), 3,456 games each, so 13,824 games per level.
- **Sweeps.** A 0.25° sweep on every level, and a trace of every stuck angle.
- **Renders.** Every level in MoonfallRender at 1280 (`--moment start`), plus 4-1 `--moment cleared` and 4-5 at 640, with my own `--cache`. I compared each one pixel by pixel with the pack.

Output is in `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr4r3\`.

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| n8 (new) | Nit | 4-2, pack | All three 4-2 pack renders show the old Ace, 250,000. The shipped value is 260,000. | `base-17-engine-1280.png`, `-engine-640.png` and `-cleared-1280.png` show "ACE 250,000". They were rendered at 18:28 and 18:39; `aces.json` was committed at 18:59. My render from the current build shows 260,000 and is otherwise identical to the pack's. Re-render 4-2 when the pack is next touched. |
| n9 (new) | Nit | 4-2 → 4-3 | The step passes, but the pipeline overstates it. It is the thinnest step in the stage. | Fresh: 4-2 24.60 ± 0.20 and 4-3 23.63 ± 0.20, a step of 0.97 ± 0.29 (0.68 after one standard error, so charter 1.1 passes). The four 4-2 blocks are 24.39, 24.50, 25.00 and 24.50. The pipeline's pooled 4-2 is 25.30, so its step of 1.75 is about 0.7 too generous. No change needed. The record should quote the fresh figure. |
| n10 (new) | Nit | 4-5 | The finale gap sits just over the charter's 3.5-4. | Fresh: 17.55 ± 0.20, a gap of 4.13 ± 0.28. The finale is inside its band (18.5 ± 1.2) but near the bottom. My rule (a gap of at least 2.5) passes. This is the game designer's call, as the response already says. |
| n11 (new) | Nit | pack | `index.md` "Integration changes" still quotes 4-4 at 23.3 held out and 22.18 pooled. | Those figures are from integration. The stage table above them (22.1 / 21.95) is current. Mark the paragraph as historical, or drop the figures. |
| n2 (carried) | Nit | 4-3 | The dome and minaret silhouettes are in our painting. | This is the owner's call. |

## Round-2 findings checked

- **M4 (4-2 → 4-3 step collapsed): fixed.** The swap of (413,210) for (689,222) is gone from the shipped 4-2. On fresh seeds the step is 0.97 ± 0.29, which clears 0.5 after one standard error (0.68); round 2 measured −0.08. The round-2 lens and horizon rework made 4-2 about 0.9 harder than HEAD's 25.5 was on my round-2 seeds. That is why the step is 0.97 and not the 1.65 I predicted (n9).
- **M5 (fold-in unverifiable): fixed.** Everything is committed in `cca6c2c5`: the levels, `Program.cs` `--moment cleared`, the charter, `aces.json`, `stage-mr4.txt` from this round, `index.md` and both responses. `stage-mr4.txt` matches the reports (held out 26.0, 25.6, 23.7, 22.1 and 18.1, all PASS). `index.md` agrees with it. My renders of 4-1, 4-3 and 4-4 are pixel-identical to the pack. On 4-5, 142 pixels differ (0.014%, within x 501-732 and y 390-708), which looks like a glint's phase; I did not confirm the cause. The only real staleness is the 4-2 Ace label (n8).
- **m3 (4-2 right half reads as rows): fixed.** The trace sheet now shows the lens edges opening east: the top rises toward (700,248) and the underside falls toward (700,404). The horizon is a dune crest, so the right half reads as a widening tongue, not parallel rows. The lens is still an invented cloud on haze. It follows the painting's cloud streaks loosely, which is acceptable as the game designer prescribed it.
- **m6 (4-5 notch at (373,371)): fixed.**
  - (360.5,368.7) clears both neighbours by 16.2.
  - The sweep sticks on 6 of 681 angles (was 13), and none rests near (373,371).
  - Of the six rest points, four or five are on the roofline band: (444,171), (411,173), (415,168), (483,157), and (333,187) just below it. One is at (423,370). The roofline gaps are unchanged.
  - Play sticks 61 per 1,728 on fresh games (round 2: 68).
- **n6 (index described 4-3's cleared render as the bare scene): fixed.** `index.md` describes every cleared render as a real cleared board.
- **n7 (4-5 "from the Sands" on little art): fixed.**
  - **The sands.** The foot is warm umber and amber in engine-1280, engine-640 and cleared-1280, with a rose-red transition band.
  - **The dunes.** They are warm and their moonlit crests are dashed.
  - **At 640.** The sands read as sand and dusk. The transition band looks a little like fire, but within taste.

## Checks that pass

- **Dead first shots:** none on any level. No sweep angle hits nothing, and every piece is reached by some first shot (0 of 62, 67, 78, 77 and 69 missed).
- **Stuck per 1,728, fresh:** 72, 14, 64, 72 and 61, all under the shipped maximum of 88. The sweep's stuck angles are 3, 0, 5, 8 and 6 of 681. On 4-3 the rest points are the same as in round 2: (632,224), (492,188), (321,407) and (287,401). No ball rests inside a ring.
- **Long flights:** one, on 4-4, unchanged.
- **Ramp, fresh, 13,824 games each:** 26.58, 24.60, 23.63, 21.68 and 17.55.
  - Steps: 1.98, 0.97, 1.95 and 4.13, each ± 0.29.
  - Mean 22.81, a fall of 0.85 from MR 3's 23.66, inside 0.5-1.0.
  - The opener is inside its band (26.5 ± 1.2), and so is the finale.
- **Random player:** 0 wins of 40 on 4-1, 4-3, 4-4 and 4-5. On 4-2 it wins 1 of 40 in every run, which is the same fixed game each time. The pack's report shows the same win. No gate covers it and it does not matter.
- **Holdouts in lost games, fresh:**
  - 4-1: 44% of lost games hold an orange in y 400-430, the same as round 2. The top holdout is (558.8,400.2) at 25.2%, against 24.5% in round 2: on the edge of the band and at the 25% line, unchanged and noise-level.
  - 4-2: (579,374) 27%.
  - 4-5: (540,373) 29%, (601,339) 27% and (614,366) 24%. These are on the Thal ghost arc and are the finale's intended knob.
  - The pipeline's cheap-difficulty gate (y 430 or lower) reports 0 on every level except 4-4 (0.06).
- **Composites against the engine:** engine-1280 matches the composite piece for piece on 4-2 and 4-5, checked side by side.
- **4-1 clone boxes:** in cleared-1280 the hatching and parchment run unbroken through (108,243), (210,287) and (455,204). I see no blur patches or seams at 2x zoom.
- **Pipeline:** all five reports PASS with no fails. The loader, pre-flight, reach, framecheck, colours and readcheck are clean, and the stage gate passes.

## Per-level verdicts

**4-1 The Steps of Thal (base-16): APPROVE.**
- 26.58 fresh, 72 stuck per 1,728, and the sweep sticks on 3 of 681 angles.
- The clone fix removes the blurred patches cleanly, and the walk with its trail and Gate exit arrow still reads.
- The swap is committed, and the orange on the Gate of Thal moon is on the subject.

**4-2 The Spire of the Sands (base-17): APPROVE (n8, n9).**
- 24.60 fresh, 14 stuck per 1,728, and no stuck angle in the sweep.
- The spire ring, the lamp arch, the red hull with its keel, and now a widening lens and a dune-crest horizon carry the subject.
- The sand runs to the wall, so the teal strip is gone.
- Re-render the pack's Ace label.

**4-3 Lamps on the Steps (base-18): APPROVE (n2).**
- 23.63 fresh, 64 stuck per 1,728, and the sweep sticks on 5 of 681 angles.
- No regression. The committed swap matches what I approved in round 2.

**4-4 The Coliseum (base-19): APPROVE.**
- Unchanged since round 2: 21.68 fresh, 72 stuck per 1,728.
- Still the strongest board.

**4-5 Ul'dah from the Sands (base-20): APPROVE (n10).**
- 17.55 fresh, 61 stuck per 1,728, and the sweep sticks on 6 of 681 angles.
- The notch is closed.
- The sands now carry the name: an umber foot, warm dunes with lit crests.
- The wall traces honestly, and the east third sits on the lifted Thal ghost.
- The gap of 4.13 is the game designer's call.
