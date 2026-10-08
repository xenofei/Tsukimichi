VERDICT: REVISE (0 blockers, 2 majors, 2 minors)

I worked read-only. I did not edit, build, convert, stage or selftest anything in the worktree. I ran only the built `mfcheck.exe`, from 07:12; no engine source has changed since. All output is in `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr4r2\`.

**What I reviewed.** I reviewed the worktree as it stands. That is HEAD `fac9cc5a` (the five round-1 fix commits) plus uncommitted edits:
- candidate swaps on base-16, base-17 and base-18;
- `MoonfallRender/Program.cs`;
- the charter;
- regenerated pack files for those three levels.

I measured the uncommitted level files, which match `docs/.../json` piece for piece.

**What I ran:**
- **Fresh games.** `mfcheck play` over 4 new seed blocks (60001+, 70001+, 80001+ and 90001+), 3,456 games each: **13,824 per level**. No one has tuned on these seeds.
- **Committed versions.** The HEAD versions of 4-1, 4-2 and 4-3 on the 60001 and 70001 blocks, to isolate the uncommitted swaps.
- **Sweeps and traces.** A 0.25° sweep on every level, and a trace of each stuck angle on 4-3 and 4-5.

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M4 (new, regression) | Major | 4-2 → 4-3 | The 4-2 → 4-3 step has collapsed and fails charter 1.1 (a step of at least 0.5 after one standard error). | On 13,824 fresh games: 4-2 24.11, 4-3 23.91, so the step is **0.20 ± 0.29 (−0.08 after one standard error)**. On the 60001 and 70001 blocks alone it is 0.39 ± 0.41. The cause is the uncommitted 4-2 swap: (413,210) is no longer orange-able and (689,222) now is. On the same seeds, HEAD's 4-2 plays 25.51 and the working tree's 24.24, which makes 4-2 1.27 harder. HEAD's step was 1.61. The pack cannot show this, because its `stage-mr4.txt` predates round 1 (M5). |
| M5 (new) | Major | stage | The fold-in cannot be verified as a unit: the pack's stage gate is stale and nothing records the stage pass. | `response-mr4-round-1.md` does not exist, in this worktree or the main repo. Three things are uncommitted: the swaps on base-16, base-17 and base-18, the `Program.cs` change to `--moment cleared`, and the 4-1 charter row. `stage-mr4.txt` is from 08:26, before round 1: it still shows 4-3 at 155 stuck and 4-5 at 18.3, and its PASS covers layouts that no longer exist. The `index.md` table disagrees with the reports: 4-1 shows 27.0 against 26.0 and 4-2 26.1 against 25.2. The 4-1 and 4-2 swaps change difficulty after their Aces were suggested (576 games, on the earlier layouts), and I could not run `Shipped_Aces`. |
| m3 (carried, partly fixed) | Minor | 4-2 | The red airship now reads, but the right half is still horizontal rows over haze where no lens is painted. | In engine-1280 the craft is visibly red, and the keel runs about 25 px under its edge. The "cloud lens" sits on no painted edge in the trace sheet. In the engine it reads as rows at y≈278, 322, 368 and 445 (board units), with a point at their west end. |
| m6 (new) | Minor | 4-5 | One notch holds about half of the level's stuck first shots. | Sweep: 13 of 681 stuck (1.9%), the highest in the stage; round 1 had 12. Six of the 13 linger at about (373,371), between the pegs at (356.7,378.2) and (385.6,388.9), both r 8. Their gap is 14.8 px, outside the loader's 0.5-11.8 cradle window. Four more linger on the roofline band at y 157-187 (r 7 pegs, gaps about 15 px). Play stuck is 68 per 1,728, under the shipped maximum of 88, so this is not a blocker. Moving one peg of the notch about 4 px, or onto the line, closes it. |
| n2 (carried) | Nit | 4-3 | The dome and minaret silhouettes are in our painting. | This is the owner's call. |
| n6 (new) | Nit | pack | `index.md` says `base-18-cleared-1280.png` is the bare scene. | The file is now a real `--moment cleared` render (HUD, 1 orange, 11 balls), made with the uncommitted `Program.cs` fix. |
| n7 (new) | Nit | 4-5 | "from the Sands" still rests on little art. | The field is magenta, warming a little toward the foot. The sands read only as the two dark dune silhouettes in the lower corners. The wall subject (LM) now traces honestly; this is for the game designer to weigh. |

## Round-1 findings checked

- **M1 (4-3 stuck): fixed.** Stuck fires 72, 74, 72 and 66 per 1,728 games on my four blocks (71 on average; round 1 had 166 and 154). The sweep sticks on 5 of 681 angles (was 14). No stuck angle lingers inside a ring now. The balls linger at (632,224) twice, on a ring's flat top, and at (492,188), (321,407) and (287,401). The open-bottomed rings read well in engine-1280.
- **M2 (4-5 subject and east third): fixed.**
  - The level is re-subjected as LM, the wall outlined: crowns on the north-west tower and the Exchange, the east and west ramparts, and the band above the crest.
  - The Thal ghost is lifted. Its curved streets are visible in engine-1280 and clearly in the cleared render, and the thal arc, inner arc and east-street bricks sit on them.
  - The top holdouts are still in the east, but now on art you can see: (540,373) in 29.7% of lost games, (601,339) in 26.4%, (614,366) in 24%.
  - The name stays (n7).
- **M3 (4-5 stars on the rim): fixed.** The neat-line seam is gone. I see no edge line along the top or right edge in any 4-5 render, and runtime stars are off. The motion is mist plus glints along the roads.
- **m1 (4-1 stuck): fixed.** Stuck fires 64, 72, 65 and 76 per 1,728 (70 on average; was 93). The sweep sticks on 3 of 681 angles (was 10).
- **m2 (holdouts at the cheap-difficulty line): fixed.**
  - **4-1:** the y 400-430 band now holds 2 candidates. They are 19% of the oranges left in lost games (was 32%), and 43% of lost games hold one (was 62%). (423,425) is no longer a candidate. (389,423) is left in 21.6% of lost games, under the 25% line.
  - **4-2:** 32% of lost games hold a low orange (was 64%). The top holdouts are (579,370), (522,404) and (679,368).
- **m4 (4-4 protan): fixed.** p10 and minimum are 0.107 and 0.105, from the report.
- **m5 (the stage barely harder than MR 3): fixed, partly by the edits that cause M4.** The stage mean on fresh games is **22.77**, against MR 3's 23.66: a fall of 0.89, inside 0.5-1.0. At HEAD it would be about 23.12, a fall of about 0.54.
  - Opener 26.36 ± 0.20, inside the band (26.5 ± 1.2).
  - Finale 17.69 ± 0.20, inside the band (18.5 ± 1.2). Its gap of 4.09 clears the 2.5 rule.
  - The other steps are 2.25 (4-1 → 4-2) and 2.12 (4-3 → 4-4).
- **n1: fixed.** The opener reads 26.36 fresh against 26.0 held out.
- **n3: fixed.** The charter rows now give 4-2's airship and keel, 4-4 on the Thal map, and 4-5 as the wall on the Nald map (LM).
- **n4: fixed.** The second jewel's share is 0.21.
- **n5: fixed.** 4-4 has glints round the city ring and the stands, and the converter check keeps both.

## Checks that pass

- **Dead first shots:** none on any level (no sweep angle hits nothing), and every piece is reachable.
- **Random player:** 0 wins in every run.
- **Long flights:** one, on 4-4, unchanged.
- **Pack reports:** all five say PASS, with clean loader and pre-flight.
- **Composites against the engine:** engine-1280 matches the composite piece for piece on 4-2 and 4-5, checked side by side. On the other three the renders match the fix descriptions and nothing looks out of place. The 640 renders read.
- **Stuck per 1,728 on fresh games:** 70, 16, 71, 79 and 68. All are under the shipped maximum of 88.

## Fix for M4 and M5

Undo the uncommitted 4-2 swap, or use a milder one, and keep 4-1's. On my seeds that gives about 26.35, 25.5, 23.85, 21.76 and 17.62:
- steps of about 0.85, 1.65, 2.09 and 4.15;
- a mean of about 23.0, still a fall of about 0.64 from MR 3.

The alternative is to harden 4-3 by about 0.8, which the 2.12 step to 4-4 has room for. After either fix:
- regenerate `stage-mr4.txt` and `index.md`;
- re-suggest the 4-1 and 4-2 Aces;
- commit;
- write `response-mr4-round-1.md`.

Then re-measure the 4-2 → 4-3 step on fresh seeds, for example 100001+.

## Per-level verdicts

**4-1 The Steps of Thal (base-16): APPROVE.**
- On fresh games: 26.36, with 70 stuck fires per 1,728.
- The walk still reads: the trail is now long faint dashes, and the Gate exit arrow is clear.
- The uncommitted swap moves an orange from (464.6,196.5) to the Gate of Thal moon (563.4,301.5), which is on the subject.

**4-2 The Spire of the Sands (base-17): REVISE (M4; m3 carried).**
- The board is fine. The difficulty is the problem: at 24.11 it sits on top of 4-3.
- The spire ring, the lamp arch and the skyway carry the level, and the red hull now reads.

**4-3 Lamps on the Steps (base-18): APPROVE.**
- M1 is fixed: 71 stuck fires per 1,728, and the sweep sticks on 0.7% of angles.
- The flat-topped, open-bottomed rings read as lamp pools.
- 23.91 on fresh games. Its step from 4-2 belongs to M4.

**4-4 The Coliseum (base-19): APPROVE.**
- Still the strongest board: 21.78, with 79 stuck fires per 1,728.
- 12% of the oranges left in lost games sit in y 400-430, against 10% of the candidates (ratio 1.29).
- Protan has margin now.

**4-5 Ul'dah from the Sands (base-20): APPROVE (m6, n7).**
- The wall subject traces honestly, the east third sits on the lifted Thal ghost, and the rim seam is gone.
- The finale is 17.69, a gap of 4.09 from 4-4.
- Fix the (373,371) notch while you are in the file.
