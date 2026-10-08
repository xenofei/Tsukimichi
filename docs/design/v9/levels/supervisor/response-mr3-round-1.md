# Response: MR 3 The Night Skyway, round 1

The fold-in for the three round 1 reviews (`game-designer-mr3-round-1.md`, `ux-mr3-round-1.md`,
`level-critic-mr3-round-1.md`). Every Blocker, Major and Minor is answered below, then the Nits. No gate's rule or
threshold changed. The pack in `docs/design/v9/levels/review/mr3/` is regenerated (its `index.md` lists what changed).

## Where the stage stands

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Converter 2x max / p99.9, 1x p99 |
|---|---|---|---|---|---|---|---|---|
| 3-1 | base-11 | The Airship Road | 27.1 | 27.77 | 254 / 173 | 0.267 | 0.105 / 0.103 | 0.023 / 0.011, 0.032 |
| 3-2 | base-12 | Above the Cloud Sea | 25.4 | 25.77 | 290 / 211 | 0.237 | 0.117 / 0.112 | 0.009 / 0.007, 0.004 |
| 3-3 | base-13 | The Holy See | 23.8 | 24.32 | 254 / 338 | 0.265 | 0.112 / 0.111 | 0.034 / 0.009, 0.0349 |
| 3-4 | base-14 | The Lane by Starlight | 22.4 | 21.88 | 275 / 208 | 0.232 | 0.107 / 0.102 | 0.008 / 0.006, 0.005 |
| 3-5 | base-15 | The Sky Whale | 18.8 | 18.57 | 258 / 176 | 0.229 | 0.112 / 0.109 | 0.028 / 0.009, 0.030 |

- `mfl.py stage mr3`: PASS. Pooled steps 2.00, 1.45 and 2.44 (± 0.41); finale gap 3.31 ± 0.40; mean 23.66.
- `mfl.py convert --all --check`: PASS, 30 of 30 (the ten shipped levels too).
- `mfl.py selftest`: every checker passes its known-bad and known-good cases.
- `pwsh tools/gate.ps1`: build, load, test and themes all pass.
- Aces, re-suggested over 576 games: 3-1 260,000 (was 270,000), 3-4 270,000 (260,000), 3-5 250,000 (210,000). 3-2 and
  3-3 are unchanged. The Perf test `Shipped_Aces_are_unchanged_by_the_hard_level_rule` passes 15 of 15.

## Level critic

**B1 (Blocker) — the converter gate failed on 3-1, 3-3 and 3-5. Fixed at the cause.**
- **The 8-bit cut.** The pipeline crops a game painting with PIL's `LANCZOS` on an 8-bit picture. Its horizontal pass
  writes an 8-bit image, which the vertical pass then reads, so an overshoot past white at a sharp light is clipped
  between the passes. The runtime resampled in floating point.
- **The fix.** `MoonfallSceneBuilder.Cut` now resamples the same way (`MoonfallFilters.Resample(..., eightBit: true)`;
  only the scene cut, so the title, the chart and the chrome art are untouched).
- **Effect on the grade.** The graded paintings now agree to p99 0.0000 at 2x. Before, they differed by up to 15 levels
  in a channel (3-3's 0.055 speckle came from this).
- **What remained.** OKLab is steep at black: sRGB 0 and 1 are 0.067 apart. 3-1's and 3-5's dressed land sat on sRGB
  0-1, so the 2x maximum failed on a single rounding step.
- **The black floor.** Both scenes lift their floor a few levels with a faint board-wide glow (radius 2000; 3-1 grey-green
  k 0.024, 3-5 grey k 0.014).
- **3-5's bottom.** 3-5's green region also fades out over the bucket, where the runtime's quiet differs from the
  pipeline's (a smooth blob at about (550, 580), there before this round too).
- **Result.** All five pass, and so do the ten shipped levels (30 of 30).
- **Unchanged.** The thresholds. The README records it ("Grade parity at the cut").
- **Still thin.** 3-3's 1x p99 reads 0.0349 against 0.035, as it did before this round. The 1x tier grades base03 at 1x,
  where the pipeline downsizes its 2x scene. Switching 3-3 to `base03_hr1` was tried: 2x passed, but 1x rose to 0.042.

**M1 (Major) — the 3-3 → 3-4 step did not hold on fresh games. Fixed.** 3-4 was retuned harder and 3-3 rebalanced
(below). The pooled step over 6,912 games is now 2.44 ± 0.41 (it was 1.03).
- **3-3.** Tuning 23.8, second block 23.9, held out 23.8, pooled 24.32.
- **3-4.** Tuning 23.7, second block 22.4, held out 22.4, pooled 21.88.
- I did not play your seeds 40001-43456 block, so the figure above is not on a block no one used. The second tuning
  blocks (seeds 20001-23456) agree with the pooled order and step.

**M2 (Major) — 3-5's subject barely carried the board. Fixed.** The board was rebuilt round the whale.
- **The crop.** Now [940, 330, 960, 720]: the whale spans about 200 of the opening's 650 units. Before, it was 110 × 40
  units in a 1,400 px crop.
- **The whale.** It is lit (a tone of × 1.5 on its own silhouette) against cloud darkened round it (× 0.82 in a 60-unit
  band). It reads at 640 (`base-15-engine-640.png`).
- **The ring.** An even ring of moons runs 16 units outside the whale's silhouette. The silhouette is taken from the
  painting's dark mask: flukes, tail stock, dorsal fin, head and belly.
- **The filler is gone.** The 23 cloud-sea moons and 11 billow arcs are replaced by things the painting draws:
  - the vortex's rim from the right wall round under the isle and up its left side, and its inner wall;
  - a ring round the west eddy;
  - the south flock's chevron and its flight line to the whale's head;
  - dotted lines along the lit crests of the cloud lobes, found on the graded painting.
- **Candidates.** None sits at y 430 or lower. The top holdouts are the flock's lead pair, the high east crest and the
  flight line (18-21%).

**m1 (Minor) — 3-3 had the most stuck-rule fires in the game. Improved, not to the shipped maximum.**
- **Where the slow balls rested.** `mfl.py stuck base-13` put four of the 14 stuck first shots on the east billow's flat
  apex, under the ridge's low end. The ridge's last brick also left a 23-unit cradle against the left flank's moon.
- **The fix.** The crown is opened at its keystone, with a slot past the notch band, and that ridge brick is gone.
- **Result.** The sweep's stuck share is 1.9% (it was 2.1%), and the play's stuck fires 97 per 1,728 games (120). The
  shipped maximum is 88 (1-1).
- **Left over.** The remaining stuck shots are spread over a dozen places, the rose window's ring the most often (3).
  Long flights stay at 2.

**m2 (Minor) — the cheap-difficulty gate passed with little room, and the top holdouts were off-subject. Fixed.**
- **The wall strip.** Its two candidates, (702, 228) and (714, 292), moved onto the cathedral: the left flank's (472, 232)
  and the mid spire (566, 222).
- **Rebalance.** The set was rebalanced on the tuning seeds (the layout's docstring lists it). Only three candidates are
  low.
- **The gate's ratio.** Low oranges are 9.4% of those left in lost games, against 10% of the candidates: a ratio of 0.94
  (it was 1.40; the limit is 1.5).
- **Holdouts.** No holdout passes 18%.

**m3 (Minor) — a 3-1 lane-south candidate sat just above the cheap gate's line. Fixed.**
- **The move.** (410, 421) gave its candidate to Gridania's upper-right ring moon (514, 400), a city stop. Held out 27.1,
  pooled 27.77.
- **What stays.** The lane keeps (378, 424), left in 21% of lost games: it is the left half's only candidate below y 400,
  which the spread rule needs.

**m4 (Minor) — 3-4's sliding pegs left the crests they ride. Fixed in the layout; the clouds themselves cannot move.**
- **The movers.** Each row still slides as one drift group. Each mover rides its run's mean crest height, and a run whose
  crest strays more than 9 units (a peg's radius and one) either side is left out. Crest 5's movers had sat up to 24.3
  units off.
- **Mist.** Mist bands along the slid crests were built and then dropped. The runtime keeps every mist band
  `PegKeepOut` clear of the pieces across the board's width (`MoonfallMotion.MistBandClear`), so the converter gate
  failed with "dropped 2 mist bands". No other runtime layer moves the painting.

**m5 (Minor) — 3-4's name promised an airship, and the charter's rows were not updated. Fixed.**
- **The name.** 3-4 is now "The Lane by Starlight".
- **The charter.** Its rows for 3-4 and 3-5 (subject, crop, built jewels, candidates) and MR 3's stage row (the theme,
  3 game paintings and 2 of ours) are updated.
- **The sources plan.** Its 3-4 row notes the rename.

**m6 (Minor) — the held-out figure flattered the finale. Fixed.**
- **Held out against fresh.** 3-5 reads 18.8 held out and 18.5 on 5,184 fresh games.
- **The gap.** The pooled finale gap is 3.31 ± 0.40, against the charter's planned 3.5 (it was 4.48).

**n1 — the start-of-play renders were two shots in. Fixed.** MoonfallRender has a `--moment start` (the board as dealt).
The pack's engine renders use it, so they match the level files piece for piece.

**n2 — 3-1's note quoted the wrong band edge. Fixed.** The docstring now explains the two sides of the band. The easy end
of 27 + 1.8 is `stagecheck`'s three standard errors (`3 * NOISE`), and the charter's ± 1.2 is the hard side.

**n3 — no level reaches the 0.12 protan figure. Not fixed.**
- **Where they stand.** All five still pass on the 0.101 fallback (p10 0.105-0.117).
- **3-4.** Its lowest place, the lamp at (301, 392), is no longer a candidate, and its minimum rose from 0.086 to 0.102.
- **What blocks 0.12.** Every route to it on 3-1 (less region quiet, a cooler lift) breaks F9 or F7's share.

**n4 — 3-1's second jewel's share sits just over its floor. Not fixed, and thinner.**
- **Share.** It is 0.159 against 0.15 (it was 0.168). The black-floor lift moved it.
- **What was tried.** `regionQuiet` 0.6 raised it to 0.204, but F9 then failed (p10 0.100).

## Game designer

**M1 (Major) — the whale was small and dark on dark. Fixed.** See the critic's M2.
- **The zoom.** The crop zooms in (960 px, against 3-1's 1,280), and the whale spans about 31% of the opening.
- **The light.** The whale is lit against darkened cloud.
- **The pegs.** They follow its outline: flukes, tail stock, fin, head and belly.
- **The overlap.** The crop overlaps 3-1's by 24.3% of its own area (the rule's 25%). 3-1 shows only the whale's head at
  its top-right wall.
- **What is lost.** The north flock now lies beyond the right wall.

**M2 (Major) — 3-4 repeated 3-2, had no airship, and its banks read as hills. Fixed by renaming and a different scene.**
- **Name.** "The Lane by Starlight": an airship hull cannot come from game art at a usable size (`sources-plan.md`), and
  figures are never code-painted.
- **The moon.** It has set beyond the upper right. Its light rakes the banks from the right, and no disc is left on the
  board.
- **The sky.** It is a deep indigo-blue holding the star river: a milky band of crowded small stars with a dark rift,
  climbing from behind the left tower. Four pegs trace its spine, and two of them are candidates.
- **The clouds.** Each bank's crest is lined silver, with smaller billows on the faces, and the far towers are
  slate-blue.
- **The second jewel.** It no longer tints every crest (that is what read as green hills). It lies on the lane's own wedge
  of cloud between the beacons: the current the airships ride.
- **What it shares with 3-2.** The jewels are 275 / 208 against 3-2's 290 / 211, so the sky still leans violet. F7 needs
  60° within a level, and 3-5's 258 / 176 and 3-3's 254 / 338 are its neighbours; together these leave no
  hue-distinct pair that is cooler (the trials are recorded in the recipe's history).

**m1 (Minor) — only 3 looks across 5 boards. Fixed by M1 and M2.**
- **3-5.** It now shows the vortex and the whale up close, not the opener's chart zoomed out.
- **3-4.** It has no moon, needles or beams: the star river and the lane's aqua wedge set it apart.

**m2 (Minor) — 3-5's lower half was 11 identical arcs that traced nothing. Fixed.** The arcs are gone. Dotted lines now
follow the lit crests of the painted cloud lobes, and the flock's chevron reaches into the lower right.

**m3 (Minor) — 3-3's stuck-ball rescue fired often.** See the critic's m1. The fires are down 120 → 97, the cause was
the east crown's flat apex, and the arches were not involved.

**m4 (Minor) — two 3-3 oranges tucked against the right wall decided losses. Fixed.** See the critic's m2.

**m5 (Minor) — the movers slid over still clouds. Partly fixed.** See the critic's m4.
- **The movers.** They keep to their crests.
- **The clouds.** A drifting cloud picture is not possible: mist bands must clear the pieces, and the painting itself
  does not move.

**n1 — 3-2's outer-beam holdouts sat on the 25% line. Not fixed.** Moving (133, 403) up its beam to (130, 367) broke the
spread rule (11 candidates in one 200 × 200 square), so the pair stays. They are above y 430, so the gate does not count
them, and they read 25% and 21% as before.

**n2 — 3-5's held-out figure was well off its fresh one. Fixed.** It is now 18.8 against 18.5.

**n3 — 3-1's upper left reads as scatter. Not fixed.** The fragments are the chart's own stops (the dragons, the compass,
the isles). Each sky peg moves this board by 0.5-1 per 48, and its ramp was tuned to the band.

**n4 — 3-5's vortex was only arced under. Fixed.** Moons now run along the vortex's whole visible rim and its inner wall.
The inner wall stops short of the isle's banner, where a purple peg failed F6 (0.183).

## UX

**M1 (Major) — 3-2's moon swallowed the ball and the aim guide. Fixed: the moon stays centred and is misted.**
- **Why not moved.** The composition's lane runs to it.
- **The face.** Its OKLab L is about 0.65 and 0.69 at most on the engine's bare scene (it was 0.934). Two strata of
  mist lie across its lower half, and its glow in the sky is halved.
- **Against the ball.** The ball (0.885) and the guide dots (0.96) now stand out against it
  (`base-12-engine-1280.png`).
- **Your suggested check.** Nothing in the launcher's ± 35° cone down to y 300 is brighter than 0.844.
- **F6.** At (331, 261) it is no longer the worst place. A broad horizon tone (× 0.8) brings the board's worst to
  0.237, from 0.216.
- **The guide.** It was not given an outline: the dots already read over the misted face.

**m1 (Minor) — protan orange separation was the lowest in the game on 3-4 and 3-5. Improved.**
- **3-4.** p10 / min is 0.107 / 0.102 in the pipeline (it was 0.106 / 0.086). The crests' green is gone; the aqua lies on
  the lane's wedge only. The lamp candidate (301, 392) is blue.
- **3-5.** 0.112 / 0.109 (it was 0.113 / 0.090). The cloud sea's green fades out over the vortex's upper rim (a y ramp
  in the region's mask), not a peg-scale term.
- **Not measured.** I did not reproduce your 640 engine method, so the 640 minimum is unverified.

**m2 (Minor) — 3-5's mean chroma sat exactly at the F7 limit. Fixed.**
- **Pipeline.** Mean chroma is 0.069.
- **Engine.** On the bare scene (MoonfallRender `--scene-only 2`), F7 reads 257 / 176, 82° apart, second share 0.201,
  mean chroma 0.069.
- **The gate.** The converter gate passes on 3-5.

**m3 (Minor) — F6 headroom was the thinnest of any stage. Improved on the three you named; 3-5 is thinner.**
- **3-1.** 0.267 (was 0.222). Four broad tones (× 0.85, radius 110-130) quieten the banners round Ishgard, Gridania and
  the western hold, and the pale cloud shore.
- **3-2.** 0.237 (was 0.216).
- **3-4.** 0.232 (was 0.215). The lamp cores under their moons are dimmer.
- **3-5.** 0.229 (was 0.260), thinner than before: the new board's pieces sit over the vortex's bright swirls. A broad
  tone (× 0.85, radius 110) quietens the west eddy's cloud and the moonbeams are softer (k 0.03); the worst is now an
  inner-wall moon by the isle's banner, and the largest drop is 0.008. Quietening the banner too reached 0.239, but it
  moved the jewels to 265 / 199, the same palette as 3-4's under the neighbour rule, so it was not kept.

**n1 — 3-3's story-safe scene shows a hard edge at x 500. Not fixed.**
- **Why.** Softening the mauve region's edge changes 3-3's dress. 3-3's 1x converter p99 sits at 0.0349 against 0.035.
- **Precedent.** It is the same class as the shipped 2-5 n1, which was accepted.

**n2 — 3-5's bird marks touch pegs. Not fixed.** The new flock's moons still trace the chevron through the painted birds.
Moving them off the line would lose the trace, and the ramp is tuned to them.

**n3 — lamp posts under sliding pegs.** Two cases:
- **Movers.** A mover whose run would cross a beacon's post is now left out.
- **(556, 392), (519, 349) and (395, 318).** These are still moons on the lamps themselves (the subject's own lights,
  never green), so they are meant to look fixed.

**n4 — the engine F6 test for the new levels was not in the pack. Answered.** `MoonfallRuntimeArtTests` runs over `ShippedScenes()`, which includes base-11 to base-15: run alone it passes 154 of 154, 40 of them on the five new levels (every peg against its scene and its story-safe scene, the ceiling, the glints, the stars off the framing). The gate's full run passed 9,018 of 9,018.

## Not verified

- In-game GPU rendering and live motion (the star river, the low mist and the movers on their crests at 60 fps).
- The protan minimum at 640 by UX's engine method.
- A fresh block of seeds that no one used (the critic's 40001-43456).
