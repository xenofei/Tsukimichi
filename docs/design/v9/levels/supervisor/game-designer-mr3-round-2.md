VERDICT: APPROVE (0 blockers, 0 majors, 1 minor)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| m1 | Minor | base-15 3-5 | The finale has no bricks and no movers, so it has the barest piece mix in the stage. 3-4 is also brickless, so the stage's structural play peaks at 3-3 and then drops away. The lower half is now short dashes of 3 or 4 pegs. That beats identical arcs, but it reads as light scatter at 640. | `base-15-report.json` preflight: bricks 0, movers 0. The trace sheet shows the crest dashes "crest west", "south-west", "south" and "middle" with 3-4 pegs each. Sweep bucket count is 116 of 681 angles, the stage's highest (97, 105, 105, 112). The shipped finales have bricks: 1-5 has 1 and 2-5 has 5. |
| n1 | Nit | base-15 3-5 | The subject string still says "two flocks of birds flying its way", but the north flock is now beyond the right wall. | `layouts/base-15.py:32` and `report/base-15.json:7`. The charter row was updated; the layout was not. |
| n2 | Nit | base-15 3-5 | The chart's "The Garlean Empire" lettering and the Garlemald banner are the largest graphic on the board, in the centre band. They pull the eye from the namesake whale. | `engine-1280`: the lettering spans x 470-980 at y 395. The whale spans x 290-545. |
| n3 | Nit | base-14 3-4 | It is still the quietest painting in the stage: four even crest rows of pegs over smooth banks. The movers still slide over static cloud (round-1 m5 residual). This is accepted as a runtime limit. | `cleared-1280`; response m4/m5 (mist bands must clear the pieces). |
| n4 | Nit | base-13 3-3 | Stuck-ball rescues are down but still above the shipped maximum. At 97 per 1,728 games (about 1 in 18), players will rarely notice. Accepted. | `stuck_fires` 97 (was 120; the shipped maximum is 88). `long_flights` is still 2. |

## Round-1 findings

| R1 id | Status | Check |
|---|---|---|
| M1 (whale small, dark, invisible at 640) | **Fixed** | The crop is [940,330,960,720], tighter than 3-1's 1280, so the finale now zooms in. The whale spans about 33% of the opening (trace x 110-325 of 75-725). It is lit mid-blue on darker grey cloud and reads clearly at 640 (`engine-640`, zoomed). The moons now follow its silhouette (flukes, tail stock, fin, head, belly) at 16 units instead of an oval. |
| M2 (3-4 a twin of 3-2, no airship, green hills) | **Fixed** | Renamed "The Lane by Starlight". The moon disc is gone. The sky is indigo, not violet, and the star river reads at 640 as a diagonal band from behind the left tower. The aqua is confined to the lane's wedge, so the banks read as purple cloud ridges, not green hills. I checked 3-2 and 3-4 side by side: they are now clearly different boards. |
| m1 (3 looks across 5 boards) | **Fixed** | Five distinct looks: the blue-green chart, the violet moon sea, Ishgard in pink and navy, the indigo star lane, and the green-and-grey vortex. The 3-1 and 3-5 crops overlap 24.3%, under the 25% rule, and 3-5 no longer reads as the opener's map. |
| m2 (3-5 lower half is 11 identical arcs) | **Fixed, with a side effect** | The arcs are gone. Lit-crest dashes, the flock chevron and the flight line to the whale's head replace them, and all of them trace painted features. The side effect is m1 above: nothing structural replaced the arcs. |
| m3 (3-3 stuck rescues) | **Improved, accepted** | 120 down to 97, and sweep stuck share 2.1% down to 1.9%. The east crown's flat apex was the cause, and it is now open at the keystone. Residual in n4. |
| m4 (3-3 wall oranges) | **Fixed** | (702,228) and (714,292) are now cathedral moons. In `engine-1280` the right-wall column is all blue. The top holdout is 0.18. The low-share ratio is 0.94 (it was 1.40). |
| m5 (movers over still clouds) | **Partly fixed, accepted** | The movers stay within a peg radius plus one of their crest, and crest 5's 24-unit drift is gone. The clouds cannot move without breaking `MistBandClear`, so this is a runtime limit, not a layout fault. |
| n1 (3-2 holdouts on the 25% line) | Not fixed, accepted | The spread rule blocks the move. Both are above y 430. |
| n2 (3-5 held-out vs fresh gap) | **Fixed** | 18.8 held out against 18.5 fresh. |
| n3 (3-1 upper-left scatter) | Not fixed, accepted | These are the chart's own stops, and the ramp is tuned to them. |
| n4 (3-5 vortex only arced under) | **Fixed** | Moons now run the vortex's visible rim from the right wall, round under the isle and up its left side, plus the inner wall. Together they make a whirlpool round the launcher, the board's best spectacle. |

**Regressions.** None found.
- 3-2: the misted moon keeps the composition, and the ball and guide stand out against it.
- 3-1: the toned banners lose a little sparkle, but the lane still reads.
- 3-3: the crown halves and the dropped ridge brick do not weaken the ridge line.

## The stage as a set

- **Theme.** It still holds as "The Night Skyway": the chart, the moonlit sea, Ishgard, the beacon lane under stars, then the vortex and the whale.
- **Variety.**
  - Look: five distinct palettes now.
  - Mechanics: rings and arcs (3-1), mirrored beams (3-2), a long brick ridge (3-3), parallax movers (3-4), and outline and vortex rim (3-5).
  - Piece mix: weaker at the end. 3-4 and 3-5 have no bricks (m1).
- **Ramp.**
  - Pooled scores are 27.77, 25.77, 24.32, 21.88 and 18.57.
  - The steps are 2.00, 1.45, 2.44 and 3.31, each about ±0.41. The weak 3-3 to 3-4 step from round 1 is now solid.
  - The finale stayed above the 17 floor I set.
  - The opener sits well above stage 2's finale (20.65), so the saw-tooth resets.
- **Finale.** 3-5 now rivals 3-3 as a spectacle: a storm vortex with lightning, a lit whale, birds in flight. The stage ends on a strong image, not on a zoomed-out map. Its one weakness is that it plays as pegs only (m1).
- **Tension.** The cheap-loss signals are clean across the set: no level's wall or low oranges decide more than about 18-25% of lost games.

## Per-level verdicts

### base-11 3-1 The Airship Road: APPROVE
- **What changed.** The candidate at (410,421) moved to Gridania's ring, and broad tones quiet the banners.
- **Read.** The lane from the airship past Ishgard to Gridania remains the spine, and the hex rings and cloud-shore arcs still sit on painted features.
- **Small window.** The lower land is dark and empty of pieces below y 640 at 1280, as before. That is acceptable for an opener.
- **Open.** n3 from round 1, accepted.

### base-12 3-2 Above the Cloud Sea: APPROVE
- **Read.** Still the best-composed board: mirrored beams fan to the moon.
- **The moon.** It is now misted with two strata. It no longer swallows the ball or the guide, and it still anchors the lane.
- **Bricks.** The low billow arcs still earn their place.
- **Open.** Nothing.

### base-13 3-3 The Holy See: APPROVE
- **What changed.** The wall oranges are on the cathedral now, and the crown is opened.
- **Read.** The ridge run, the bridge arches, the rose-window ring and the spire moons all read as Ishgard at both sizes. It stays the most legible and densest board, with 17 bricks.
- **Feel.** Stuck rescues are acceptable (n4).

### base-14 3-4 The Lane by Starlight: APPROVE
- **Identity.** The name now matches the picture: the beacon posts close in perspective toward the light shaft on the horizon, and the star river carries the sky. It no longer echoes 3-2.
- **Pegs.** Four pegs ride the star river, two of them candidates, and the tower outlines frame the upper corners.
- **Feel.** The movers keep to their crests, and it remains the stage's only motion board.
- **Weakness.** The painting is still the quietest of the five (n3), but it is now a clean, readable night scene. It is not mushy, and it does not read as hills.
- **Difficulty.** 21.88 pooled, a proper step from 3-3.

### base-15 3-5 The Sky Whale: APPROVE (with m1)
**Subject.** It now reads as its title at 1280 and 640. The whale is large, lit and outlined by its own ring. The flock's flight line runs to its head, and the flock's chevron carries the lower right. The vortex rim and inner wall turn the top of the board into a whirlpool round the launcher, which suits a finale.

**m1, piece mix.** The board is 74 pegs with no bricks and no movers. The lit-crest dashes are short, so the lower left reads as loose dots, and the sweep sends the most aims of any board in the stage into the bucket (116 of 681). Two ways to fix it:
- **Crest arcs.** Turn two or three of the longer lit-crest dashes into arc bricks that follow the crest ("crest high middle", "crest middle", "crest east low"). They still trace the painting, and they give the finale bricks.
- **Vortex arcs.** Or set a few short arcs along the vortex rim, so the whirlpool becomes a physical bowl.
- **Afterwards.** Re-run the ramp so 3-5 stays between about 17 and 19 pooled.

**Nits.** n1 (stale subject string) and n2 (the Garlean lettering dominates the centre band). The bird marks touching pegs remain UX's n2, accepted.

## Method
- **Sources.**
  - The review pack: `index.md`, `stage-mr3.txt`, and for each of the five levels the composite, the engine renders at 1280 and 640, the cleared render, the trace sheet and its report, and `report.json`.
  - The level files `Tsukimichi.Core/Moonfall/Levels/base-01..15.json`, for brick and peg counts.
  - `layouts/base-15.py` and `charter.md` row 3-5.
- **Comparisons.** The round-1 pack images came from commit 6ca10296 via `git show`, for before and after views of 3-4 and of 3-2 against 3-4.
- **Scratch files.** My comparison crops are in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/mr3gd2/`.
- **Worktree.** Nothing in the worktree was edited, built or run.
- **Unverified.** In-game motion at 60 fps (the movers on their crests, whether the star river shimmers), Fever on 3-5, and how the vortex's lightning reads in motion.
