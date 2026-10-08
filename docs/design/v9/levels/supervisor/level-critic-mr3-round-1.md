VERDICT: REVISE (1 blocker, 2 majors, 6 minors)

Read-only review. Besides the pack, I ran the built `mfcheck` from the worktree into my scratchpad: the sweep and traces on 3-1 and 3-3, and 3,456 fresh games per level on seeds 40001-43456, a block that no check or tuning uses. I ran no build, convert, stage or selftest, and `git status` is clean.

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| B1 | Blocker | 3-1, 3-3, 3-5 | The converter gate fails. The charter (§ Method and gates) requires every level to pass it, and nothing records an exception. | index.md: 3-1 2x max 0.080 (limit 0.04), p99.9 0.029 (0.015), 1x p99 0.040 (0.035); 3-3 max 0.055; 3-5 max 0.071, p99.9 0.020. On 3-3 the grade alone reads p99.9 0.023 |
| M1 | Major | 3-3 → 3-4 | The 3-3 → 3-4 step does not hold on fresh games. The pooled 1.03 came from an integration change made while looking at the same pooled games. | My block: 23.89 against 23.57, a step of 0.32 ± 0.58. Added to the pooled 6,912 games (10,368 in all): 0.79 ± 0.33, which leaves **0.46** after one standard error. The rule needs 0.5 |
| M2 | Major | 3-5 | The finale's subject barely carries the board. Half the pieces trace nothing, they cause most of the losses, and the whale cannot be read at 640. | `layouts/base-15.py`: 23 "cloud sea" moons, 11 billow bricks and 2 extras make 36 of 72 pieces. The trace sheet shows no feature line under them. 6 of the top 8 holdouts are these filler moons. The whale is about 110 × 40 units and dim; in engine-640 the ring is lost among the pegs |
| m1 | Minor | 3-3 | More balls trigger the stuck rule here than on any level in the game, and it has 2 long flights. | Sweep: 14 of 681 aims stuck (2.1%). Play: 120 stuck-rule fires in 1,728 games, and 234 in 3,456 on my block. The shipped maximum is 88 (1-1). 2 flights over 15 s (the most before was 1). The traced shots linger round the ridge's low end and the east billow arc, about (395-460, 250-370) |
| m2 | Minor | 3-3 | The cheap-difficulty gate passes with little room, and the top two holdouts are off-subject. | Low oranges are 0.233 of those left, against 0.167 of the candidates: a ratio of 1.40, limit 1.5 (1.39 on my block). 46-48% of lost games leave a low orange. Top holdouts: (702, 228) at 25% and (714, 292) at 23%, moons in the right wall strip that trace nothing |
| m3 | Minor | 3-1 | Two lane-south candidates sit just above the cheap gate's line on purpose, and the top one would fail it. | `base-11.py`: "(410, 421) and (378, 424) sit just above y 430, so no orange is low". (410, 421) is left in 26% of lost games (26% on my block too); at y 430 it would break the 25% rule. 4 of the top 5 holdouts are in the lower middle (y 383-424) |
| m4 | Minor | 3-4 | The sliding pegs leave the cloud crests they are meant to ride, because the painted clouds never move. | The `cloudlane` motion block has stars and mist only. Movers slide 20, 26 and 32 units along x on wavy crests. Off-crest distance, maximum / median: crest 3 9.5 / 4.3, crest 4 11.3 / 6.8, crest 5 24.3 / 10.8. That is up to 3 peg radii (r 8) |
| m5 | Minor | 3-4, 3-5 | 3-4 is still called "The Airship Under Way" but shows no airship, and the charter's rows for 3-4 and 3-5 were not updated after their subjects changed. | The charter still lists 3-4 as "an airship's hull and rigging in outline". `sources-plan.md` 3-4 records the owner-approved "no hull", but the name is unchanged and the charter's rule is to record the change there. 3-5's technique (CR) also changed to "a creature in outline" |
| m6 | Minor | 3-5 | The held-out figure flatters the finale. | Held out 19.2; 5,184 fresh 17.9; my block 18.49; all games together 18.31, 0.7 under the 19.0 target (inside ±1.2). The finale gap on all games is 4.68, against the 3.5 the charter plans |
| n1 | Nit | all | The "start of play" engine renders are a few shots in, so they cannot be matched piece for piece with the composites. | Balls 8-10, oranges 18-23 left, scores 1,360-8,260. 3 of 3-1's 5 bricks are already gone |
| n2 | Nit | 3-1 | The layout note quotes the wrong band edge. | It says "inside the band's easy end (27 + 1.8)"; the charter holds band ends to ±1.2. All games together read 27.84 |
| n3 | Nit | all | No level reaches the 0.12 protan figure; all five pass only on the 0.101 fallback. | p10 is 0.106-0.117. 3-4's lowest place, the lamp at (301, 392), is also its top holdout |
| n4 | Nit | 3-1 | The second jewel's share is just over its floor after the integration retune. | 0.168 against 0.15, after `regionQuiet` went from 0.8 to 0.7 |

## Per level

**3-1 The Airship Road: REVISE (B1, m3).**
- **Trace:** honest. The pegs follow the painted dashed lane from the airship over Ishgard down to Gridania, with the dragons, isles and cloud shore as stops.
- **Play:** no dead first shots, nothing unreached, stuck 1.5% in the sweep.
- **Ramp:** held out 27.6; 28.19 on my block; 27.84 on all games. Inside the band, and the integration retune was confirmed on both tuning blocks.
- **Art:** engine-1280 and 640 match the composite by eye. The converter diff is bright mainly in the near-black land and islands, which supports the index's cause.
- **B1:** a hard gate still fails. Fix the cause (the grade differs by about one level per channel, and OKLab magnifies that in near-black pixels) or take a gate amendment to the owner, checked against the selftest. No threshold change was made, which is right.

**3-2 Above the Cloud Sea: APPROVE.**
- **Trace:** the pegs ride the beacons' beams and the light strings between the spires, mirrored, and read clearly at 640.
- **Play:** stuck 1.0%, no cheap-difficulty signal. Ramp 25.4 held out and 25.47 on my block, against a target of 25.5.
- **Gates:** the converter gate passes, and F7 is clear of both neighbours.

**3-3 The Holy See: REVISE (B1, M1, m1, m2).**
- **Trace:** strong. Brick runs on the crest, the rose-window ring, the flanks and the bridge. The cleared board reads well.
- **Play:** passes every gate, but it is the stickiest level in the game (m1) and sits closest to the cheap-difficulty limit (m2).
- **Fix:**
  - Check the slow spots with `mfl.py stuck base-13`.
  - Move the two wall-strip candidates onto the cathedral.
  - Then rebalance with M1.

**3-4 The Airship Under Way: REVISE (M1, m4, m5).**
- **M1:** 3-4 plays about as easy as 3-3. All games together read 22.99 against a target of 22.5. Make 3-4 harder by about 0.7, or 3-3 easier, and confirm on a block no one has used.
- **m4:** either make the crest bands drift with the movers, or slide the movers along the crest curve rather than straight in x.
- **Gates:** the converter gate passes.

**3-5 The Sky Whale: REVISE (B1, M2, m6).**
- **What works:**
  - The even ring round the whale and the two flock lanes trace well at 1280.
  - No candidate sits low, and the integration's broad tone change leaves no visible mark on the cleared board. Its worst ghost peg (0.137 at (260, 470)) also shows nothing.
- **M2:** about half the board is cloud-sea filler that carries the finale's difficulty. Either make the whale bigger in the crop or the layout, or give the cloud sea something it actually traces, such as the painting's lanes and billow crests, with the candidates moved onto it.
- **B1 and m6:** as above.

**Stage:**
- The mean is 23.6, on target.
- The ease-back from 2-5 is 7.0.
- F7 is clear at every boundary. The closest pair is 3-4 and 3-5 at 38° / 20°, still not the same palette under the rule.
- No dead first shots and nothing unreached on any level.
