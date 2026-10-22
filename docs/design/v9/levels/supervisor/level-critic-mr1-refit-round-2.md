VERDICT: REVISE (0 blockers, 1 majors, 7 minors)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 (r1 M1, partly fixed) | Major | 1-3 base-03 | The stuck rule still fires about 110 times per 1728 games, above the FAIL line of 100. The held-out 98 is a lucky draw. The left mid-dune shoulders named in round 1 were not touched. | Report `play.stuck_fires` 98. Fresh seeds 2593-4320: 121, with 118 of 1728 games stalling. Over 3456 games that is 109.5 per 1728. The shipped maximum is 88. `mfl.py stuck`: 6 of 777 aims. Of those, 3 rest at (160-250, 360) by the west mid-dune gap ends and 2 at (655-675, 295-300) on the far-east crown |
| m1 (new) | Minor | 1-2 base-02 | Brick share fell from 34% to 28.6%, under the 30-45% guidance, though the subject allows brick | `preflight.bricks` 34 -> 28. The tank sides went from 3 bricks per side to 1 stilt, and the west road's upper leg was trimmed. `preflight.warnings` reads "brick share 29%" |
| m2 (new) | Minor | 1-2 base-02 | The pipeline's F9 protan minimum fell to 0.061, the lowest on the stage. This is UX's call. | Precheck margins: 0.061 at brick (542, 487), the east lower switchback, which is now a candidate. UX round 1 n1 had 0.078 at (504, 478) |
| m3 (r1 m2, open) | Minor | 1-3 base-03 | 46% of the pieces still trace nothing. The top-right free grid is still the board's holdout zone. | Precheck: 45 of 97 pieces. Fresh lost games: an orange at x ≥ 620, y ≤ 300 is left in 57.8% of them. The top pieces are #34 15.8%, #52 14.0%, #33 13.6% and #31 13.0% |
| m4 (r1 m3, open) | Minor | 1-4 base-04 | The right-wall column is unchanged. It is accepted as legitimate difficulty. | Fresh: #96 16.1% and #95 15.6% of lost games. The wall-side rule (20%) passes |
| m5 (r1 m4, open) | Minor | 1-1 base-01 | Brick share is 22%. The docstring gives a reason. | `preflight.brick_share` 0.224 |
| m6 (r1 m5, open) | Minor | all | The densest 90 x 90 window is 40-45%, and 1-2 is still at the FAIL edge. The coordinator should recalibrate this threshold. | Precheck: 1-2 45% at (225, 326) |
| m7 (r1 m6, open) | Minor | all | F9 protan minima are under 0.10. On 1-5 the minimum now falls on a brick that is never orange. This is UX's call. | 1-1 0.075 at (628, 178), which `TUNING` now forces orange. 1-3 0.099. 1-5 0.098 at (440, 332), in the excluded core column |

Nits, no action needed:
- The response says the 1-3 east crowns open "with a moon in the gap", but no moon was added.
    - The diff reads "0 added". `base-03.json` has no new peg in x > 590, y 270-430.
    - Each east crown's summit lies at or east of x1 = 700, so the 44-unit "gap" only shortens the crown. It does not open it.
- The response's stage figures (pooled 7.96; finale 5.52 against 8.57) do not match the committed reports (7.88; 5.33 against 8.52). Both pass, so no rule outcome changes.
- `status` shows convert "stale" for 1-1, 1-2 and 1-5. This comes from git commit order only.
    - All five runtime level files are byte-identical to the pipeline JSON.
    - The 1-2 cloth, cover and rim pictures were regenerated in 52efc6c3.
    - horizon-by-night has no lights to move.

Resolved since round 1:
- r1 m1 (1-2 stuck): fresh fires went from 96 to 20, held-out 80 to 25, sweep 20 to 3 aims.
- 1-2's fresh 9.75 above the band's upper edge now reads 8.78.

## Stage figures

| | 1-1 | 1-2 | 1-3 | 1-4 | 1-5 (finale) | pooled |
|---|---|---|---|---|---|---|
| held-out 865-2592 (report) | 7.89 | 8.72 | 9.14 | 8.33 | 5.33 | 7.88 |
| **fresh 2593-4320 (my run)** | 8.14 | 8.78 | 9.39 | 8.22 | 5.72 | **8.05** |
| both, 3456 games | 8.01 | 8.75 | 9.26 | 8.28 | 5.53 | 7.97 |
| rule seeds 1-48 | 5 | 4 | 8 | 8 | 7 | |
| stuck fires held / fresh | 20 / 17 | 25 / 20 | **98 / 121** | 23 / 31 | 10 / 9 | |
| top holdout (fresh) | 14.8% | 12.2% | 15.8% | 16.1% | 13.9% | |

- **Band (7.47-9.67).** The stage mean is inside on both seed sets: 8.05 fresh and 7.88 held-out. The margin to the lower edge is 0.4-0.6, thinner than round 1 because 1-1 and 1-5 were tuned harder.
- **Finale.** On fresh seeds the other four average 8.63 against 1-5's 5.72. The gap is 2.91, the standard error about 0.42, so the gap less one standard error is +2.49. On held-out seeds it is 8.52 against 5.33. The rule holds more firmly than in round 1 (+1.28).
- **1-5 on seeds 1-48.** It now wins 7 of 48, against 14 in round 1. This matches held-out play, so the tuning-block luck noted in round 1 is gone.
- **Playability floor.** Every level wins 4-8 of 48 against a floor of 2. 1-2's 4 is the lowest, consistent with its 8.7 held-out.
- **First shots and reach.** No level has a dead first shot (0 of 777 aims) or an unreached piece.
- **Fast check.** `mfl.py check --json` passes on all five.
- **Holdouts.** No piece exceeds 16.1% of lost games.
- **Aces.** These are now 200k, 180k, 210k, 210k and 190k. Whether each is at the top 40% of wins is not verified here, because `MoonfallAces.Suggest` scores with the C# player.
    - The 60th percentile of my fresh mfcheck wins reads 159k, 149k, 178k, 191k and 184k.
    - The scorers differ, so these are not comparable with the Aces.

## Per level

**1-1 Road to Horizon (base-01): APPROVE with m5**
- Only the difficulty changed: oranges moved to hold the stage mean, 8.4 -> 7.9 held-out and 8.14 fresh.
- There is no layout change, so the trail and its tracing are as approved.
- The three forced oranges are the top holdouts (#62, #6 and #10, 13-15% each), which is acceptable.
- Forcing orange onto (628, 178), the stage's weakest protan brick (0.075), deserves a look from UX (m7).

**1-2 Horizon by Night (base-02): APPROVE with m1, m2**
- The round-1 stuck finding is fixed and play is clean: 20-25 fires, 3 stuck first-shot aims.
- The water tower still reads in the engine at 1280:
    - the painted tank is capped by its brick crown;
    - two moons sit inside it;
    - two stilt bricks stand outside the painted legs.
- The crown now floats over the painting rather than sitting on brick sides, and the brick share fell under 30% (m1). The rooftops are a natural place for brick if the share should come back.
- The Ace-chip peg moved to (696, 104), which is clear.
- The new candidate brick at (542, 487) is the pipeline's protan low point (m2). Excluding it from candidates is a one-line fix if UX wants it.

**1-3 The Cactuar (base-03): REVISE (M1; m3 open)**
- The east change is an improvement: 154 -> 98 held-out, 185 -> 121 fresh, and the sweep down from 17 to 6 aims. The sweep target is met.
- The fire rate is still above FAIL over 3456 games, from two causes:
    - **West mid-dune.** The crown's summit gap of 70 units leaves the brick ends at x of about 180 and 250 on a crest of about 6-7 degrees. Three of the six sweep rests sit there at (160-250, 360). Round 1 asked for these shoulders to be checked; they were not.
    - **Far-east crown.** Its summit is at x of about 715, past x1 = 700, so gap 44 only trims it. It still ends on a crest of about 3-7 degrees by the wall, and balls rest at (655-675, 295-300).
- Suggested fix: widen the west gap to about 110-120 units, and end both east crowns where the crest is 12 degrees or steeper (x of about 640), or replace their last segment with a peg.
- Then re-check the band. 1-3 is already at the top of its tune band at 9.1 held-out and 9.39 fresh, the stage's easiest board, so freeing stuck balls may need a little difficulty put back.
- The figure tracing is unchanged and good. The top-right grid (m3) still traces nothing and holds an orange in 58% of lost games. Reshaping it, for example into a moon halo, would serve both tracing and holdout spread.
- The engine render matches the composite, and the scene lights were re-laid for the new layout (sagolii-cactuar.json).

**1-4 The Gilded Dome (base-04): APPROVE with m4**
- It is unchanged. My fresh run reproduces round 1 exactly (296 wins, 31 fires), which confirms the setup is deterministic.

**1-5 The Crystal's Call (base-05): APPROVE**
- The four core-column pieces were dropped from candidates (63 -> 60, inside 60-90%), with no layout change.
- Difficulty: 5.3 held-out, 5.72 fresh, rule seeds 7 of 48.
- Play: stuck 1 of 777 aims and 9-10 fires. The top holdout is 13.9% (#7 at the crystal's foot).
- The crystal still reads as the subject, now with an always-blue spine.
- The precheck protan minimum (0.098) now falls on a brick that is never orange, so it no longer bites at the core (m7 is UX's to close).

## What I ran (read-only)
- `mfl.py status --stage mr1`
- `mfl.py check base-01..05 --json`: PASS ×5
- `mfl.py precheck base-01/02/03/05`
- `mfl.py stuck base-02/03`
- `git diff 98e376f0..b37496f0` on the layouts, converter, scene and aces
- The diff images, plus engine and composite crops of 1-2's tower and 1-3's dunes
- Prebuilt mfcheck `play <json> 1728 <n> 2592` on all five shipped files

`git status` stays clean at b37496f0. Scratch output is in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/r2/` (fresh-N.txt, check-N.json, precheck-N.txt, stuck-N.txt).

Not verified: in-game motion, where stalls happen after the first shot (`stuck` covers first shots only), the Aces on the C# player, and the protan figures in the engine.
