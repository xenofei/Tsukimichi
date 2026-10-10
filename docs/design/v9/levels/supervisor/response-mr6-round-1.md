# Response: MR 6 The Silent Stars, round 1

The fold-in for the round 1 reviews (`game-designer-mr6-round-1.md`, `ux-mr6-round-1.md`, `level-critic-mr6-round-1.md`).
Each level's own findings were fixed on its own commit (6-1 `8a1e3691`, 6-2 `5b7fa921`, 6-3 `90583eea`, 6-4 `f79a1dd9`,
6-5 `0e71595d`). The stage pass then retuned 6-2 for the stage mean (critic M1). Every finding is answered below. No
gate's rule or threshold changed. The pack in `docs/design/v9/levels/review/mr6/` is regenerated: every level's renders,
reports and trace sheets (each on its level's commit, 6-2's again on the stage commit), `stage-mr6.txt` and `index.md`.

## Where the stage stands

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Converter 2x max / p99.9, 1x p99 |
|---|---|---|---|---|---|---|---|---|
| 6-1 | base-26 | The Armillary | 24.7 | 24.7 | 296 / 55 | 0.205 | 0.131 / 0.124 | 0.008 / 0.006, 0.006 |
| 6-2 | base-27 | The Wheel of the Twelve | 23.1 | 23.25 | 261 / 329 | 0.269 | 0.134 / 0.132 | 0.007 / 0.004, 0.005 |
| 6-3 | base-28 | The Seer's Chart | 21.1 | 21.62 | 289 / 160 | 0.276 | 0.110 / 0.105 | 0.011 / 0.008, 0.029 |
| 6-4 | base-29 | Meteor Shower | 20.8 | 20.57 | 255 / 191 | 0.236 | 0.119 / 0.106 | 0.005 / 0.003, 0.004 |
| 6-5 | base-30 | The Moon Gate | 17.4 | 17.4 | 312 / 228 | 0.257 | 0.114 / 0.113 | 0.013 / 0.008, 0.006 |

- `mfl.py stage mr6`: PASS. Pooled steps 1.45, 1.63 and 1.05 (± 0.41); finale gap 3.17 ± 0.40.
- **Mean:** 21.51 pooled (the charter's 21.6). MR 5's pooled mean is 22.11, so the stage falls 0.60, inside the charter's
  0.5 to 1.0. Both ends fall: the opener 25.45 → 24.7, the finale 17.7 → 17.4. The ease-back from 5-5 is 7.0, the
  bottom of the charter's 7.0 to 7.5.
- `mfl.py convert base-26 base-27 base-28 base-29 base-30 --check`: PASS, 10 of 10.
- `mfl.py selftest`: every checker passes its known-bad and known-good cases.
- `pwsh tools/gate.ps1`: the result is in the commit message and the hand-back (it ran after this file was written).
- **Aces** over 576 games: 6-1 250,000, 6-2 250,000 (unchanged by the stage pass), 6-3 250,000 (was 230,000: its round
  1 layout), 6-4 220,000, 6-5 230,000. The Perf test `Shipped_Aces_are_unchanged_by_the_hard_level_rule` passes 30 of 30.
- **ch-03** (Thirty-Five Moons, on 6-2): 35 oranges on 35 candidates, and the greedy player wins 27 of 48.

### The stage retune (6-2)

After the per-level fixes the pooled ramp was 24.7, 23.67, 21.62, 20.57 and 17.4. 6-3's fix had already opened the
6-2 → 6-3 step to 2.05, but the mean was 21.59, a fall of only 0.52 from MR 5: no margin for the critic's fresh-seed
reading (0.22 lower than the pack's pooled at integration). 6-2 sat about 0.5 above its pattern slot (6-1 less 1.5,
23.2), and 6-1 could not take the hardening (the ease-back from 5-5 is already 7.0), nor could 6-4 (the finale gap) or
6-5 (approved, and 17.4 is inside its band). So 6-2 takes it. The places, the 35 candidates and the six rim bricks are
unchanged; only radii move. Every variant below was measured with `stage mr6` on the same 6,912 pooled games:

| Variant (rays / bright stars / faint stars) | Pooled per 48 | Step 6-1 → 6-2 | Step 6-2 → 6-3 |
|---|---|---|---|
| round 1 fix: 8.5 / 9 / 8 | 23.67 | 1.03 | 2.05 |
| 8 / 9 / 8 | 23.57 | 1.13 | 1.95 |
| 8 / 8.5 / 7.5 | 22.05 | 2.65 | 0.43 (fails) |
| 8 / 8.5 / 7.75 | 22.58 | 2.12 | 0.96 |
| 8 / 8.5 / 8 | 23.8 | 0.90 (fails) | 2.18 |
| 8 / 9 / 7.75 | 21.95 | 2.75 | 0.33 (fails) |
| **8 / 9 / 7.875, kept** | **23.25** | **1.45** | **1.63** |

A quarter-unit radius change moves a single block by up to 1 per 48 either way, so only the pooled figure was used,
and the kept variant is the one closest to the pattern's 1.5 steps. Held out 23.1, greedy 25 of 48. Sweep mean 7.6,
median 7 (game designer M2 asked for 7.5 or more); sweep sticks 3 of 681; stuck fires 41 per 1,728; the low orange
(526, 440) is left in 21% of lost games (gate 25%). F6 worst 0.269, protan 0.134 / 0.132. The Ace is unchanged.

## Game designer

- **M1 (6-1 and 6-5 share one picture): fixed on 6-1's commit.** 6-1 is no longer teal on violet: the brass frame, the
  rods, the stand and the heart globe are gold, the rings pale silver, and an amber dusk band lies behind the far hills.
  Jewels 284 / 214 → 296 / 55 (6-5 is 312 / 228, so the second jewels are now 173 degrees apart). 6-5's fix also made
  its far sky bluer. Cost: 6-1's F6 worst fell from 0.234 to 0.205 (purple at 0.8x, (344, 448)). It clears the 0.20
  floor, but the margin is thin: the teal region had lifted the margin by about 0.03, and the gold cannot sit near the
  pegs (gold under oranges fails protan). Please look at 6-1's purple at 0.8x in round 2.
- **M2 (6-2 the thinnest board): fixed on 6-2's commit, held by the stage pass.** Six bricks sit on the wheel's painted
  inner ticked rim at its four diagonals. Sweep mean 6.4 → 7.7 on 6-2's commit and 7.6 after the stage pass, median 6
  → 7. Both steps either side of 6-2 resolve (1.45 and 1.63 pooled).
- **m1 (6-4 striped lake): fixed on 6-4's commit.** The painter no longer shifts the mirror row by row; broad swells
  move the reflection, the water smears it down and cools half its colour, and only short broken glints mark it. The
  lake's region is the airglow's teal at chroma 0.04 (was green at 0.11).
- **m2 (6-3 green seas, land reads as water): fixed in part on 6-3's commit.** The green now lies on the land mask, as
  the source paints it; the southern sea's green is gone and the cloud sea's is 0.25 (was 0.3). Not done: the map's top
  right is still a strong green, now the land seen between the clouds plus the cloud tint. Taking more green off the
  cloud sea fails the colour-balance check (cloud 0.1 gave a second-jewel share of 0.137 against the 0.15 gate).
- **m3 (plain moons): fixed on 6-1's and 6-5's commits.** 6-1's moon has the kit's maria, held under the cap. 6-5's far
  moon is a real full moon: seas, Tycho's and Copernicus's rays, small craters lit from the upper left, a darkened limb
  and a tight corona.
- **n1 (6-5 beams not visible): fixed in part on 6-5's commit.** The beams are k 0.06 and 22-30 wide (were k
  0.03 and 18-24). At 1280 they are still faint in a still frame. Unverified: how they read in motion.
- **n2 (6-3 stuck rescues the stage's highest): fixed on 6-3's commit.** The city ring moons went r 8 → 7.5 (Mor
  Dhona's r 7), so a ball falls through a ring rather than rattling. Stuck fires 73 → 54 per 1,728 held out. The ship
  and isle oranges are left in 18% of lost games each.
- **n3 (6-1 spoke pegs read as scatter): fixed on 6-1's commit.** The spoke glows now run on to the tip and level-spoke
  moons.
- **n4 (6-4 protan 0.092): fixed on 6-4's commit.** Protan min 0.092 → 0.106 at the same place, p10 0.119.
- **Stage: ramp.** The 6-2 → 6-3 step you flagged (resolved by 0.01) now resolves by 0.72 (1.63 ± 0.41).
- **Stage: feel.** 6-2's sweep mean is 7.6; 6-4 stays at 7.0, with its movers, as you asked.
- **Stage: composition (three centred rings).** Accepted. 6-1's gold and dusk now separate the two ring boards that
  matter, the opener and the finale; 6-2's ring is a zodiac of small figures, not an orb.

## UX

- **m1 (6-5 protan p10 0.103): fixed on 6-5's commit.** The gate's far sky region is bluer at the same chroma (hue 202,
  was 182), which pulls a protanope's ground away from the orange's olive. p10 0.103 → 0.114, min 0.102 → 0.113. Lowering
  the chroma, as suggested, moved the ground toward grey and lowered the separation, so it was not used.
- **m2 (6-4 protan min 0.092 at slider 16's end): fixed on 6-4's commit, by a different route.** The shorter slide (26
  or 30 units) left protan at 0.092-0.094 and dimming streak 3 left it at 0.092: the ring round that place is the green
  lake band, not the meteor head. Turning the lake and airglow regions teal lifted it to 0.106; F6 there 0.230 → 0.236.
- **m3 (Gridania's light beside the guide): fixed on 6-3's commit.** Its k is 0.95 → 0.55, so its core at 640 is about
  0.71 (was 0.91), under the guide dots.
- **n1 (6-3 ley dashes and glints cross the guide): no action, as you advised.**
- **n2 (6-4 lake scanlines): fixed with game designer m1.**
- **n3 (6-5 full-moon discs peg-like): no action, as you advised.**
- **n4 (6-5 star beside a peg): fixed on 6-5's commit.** The star mask has a 15th term, a disc of 20 at (209.8, 180.7)
  (16 is the runtime limit).
- **Stage note.** Both protan figures under the guard are back over it: 6-4 min 0.106, 6-5 p10 0.114. The stage's
  lowest is now 6-3's min 0.105 (p10 0.110), after its green moved onto the land; it is at the guard, not under it.

## Level critic

- **M1 (the mean falls only 0.35 from MR 5): fixed on the stage commit.** See the retune above. The pooled mean is
  21.51, a fall of 0.60 from MR 5's 22.11. On the stage's 5,184 fresh games alone the mean is 21.54 (24.7, 23.3,
  21.8, 20.5, 17.4). I did not re-run your four seed blocks (300001+), so your fresh-block figure is unverified; 6-2's
  hardening is 0.42 on the pooled games and should carry over.
- **M2 (the 6-2 → 6-3 step, 0.41 after one error on fresh seeds): fixed on 6-3's commit, as you suggested.** 6-3's
  city ring moons shrank (r 7.5, Mor Dhona's r 7), held out 21.1 and pooled 22.6 → 21.62, near its slot (22.0 from your
  figures, 21.7 from 6-1's 24.7). After the stage pass the step is 1.63 ± 0.41 pooled (1.5 on the fresh games alone).
- **m1 (6-3 the stickiest level): fixed on 6-3's commit.** The ring gaps open to 17.9 and 17.2 (from 16.9 and 15.2).
  Stuck fires 73 → 54 per 1,728 held out and 56 on the fresh games; the sweep sticks on 5 of 681 (was 8). The hardening
  did not come from the right-hand ornaments: ships and isles are left in 18% of lost games each.
- **m2 (`stagecheck` and the index do not check the mean fall): fixed in the pack, not in `stagecheck`.** `index.md`
  now reports the mean against MR 5's, the fall and both ends. I did not add the rule to `stagecheck`: this pass must
  not change a gate's rules, and a new refusal needs its own known-bad and known-good self-test cases. It is a good
  follow-up for the pipeline's owner.
- **n1 (6-2 0° shot bounces 17 s): fixed on 6-2's commit.** The top ray's moon is at x 401; long flights 1 → 0.
- **n2 (6-1 top holdout 25.4% on fresh seeds): not changed.** 6-1's layout is unchanged (the pack reads 23%), (250, 415)
  is above y 430 so the cheap-difficulty gate does not apply, and 6-1 has no room to harden.
- **n3 (6-5 beams invisible): see game designer n1.** Brighter and wider, still unverified in motion.
- **n4 (random player wins 1 of 40 on 6-2 and 6-4): not changed.** 6-2's random player now wins 0 (`stage-mr6.txt`).
  6-4's still wins 1: no check enforces it, stages 3-5 shipped the same, and changing it means moving 6-4 and its ramp.
- **n5 (6-4 protan 0.092): fixed with UX m2.**

## Not verified

- Motion: 6-5's beams, the movers' feel, the glints and the star masks. Real play feel.
- The critic's own fresh seed blocks were not replayed.
