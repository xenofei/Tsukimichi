VERDICT: REVISE (0 blockers, 2 majors, 5 minors)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | base-15 3-5 | The finale's subject, the whale, is small and dark on dark. It disappears at 640, and the pegs put an oval ring around it instead of drawing its outline, so the board does not read as "The Sky Whale". It is also less of a spectacle than 3-3, so the stage does not end on its best scene. | Whale is about 140x50 at 1x (trace sheet x 100-270, y 225-290), about 17% of the board's width. In `engine-640` it is invisible. In `engine-1280` it is a navy shape on slate cloud. The crop `[870,312,1400,1050]` is wider than 3-1's 1280, so the finale zooms out from the whale that 3-1 already shows top right. |
| M2 | Major | base-14 3-4 | Close to a repeat of 3-2: same purple night sky, full moon at the top, cloud sea and beacon posts. There is no airship in "The Airship Under Way". It is the plainest painting in the stage, and its mint-green banks read as rolling hills at 640. | Compare `base-12` and `base-14` composites side by side. Report subject: "no hull". A zoom of `cleared-1280` shows flat airbrushed banks with no internal detail. Scene `cloudlane` region hue `#3EAA54`. |
| m1 | Minor | stage | Only 3 looks across 5 boards. 3-1 and 3-5 crop overlapping parts of the same chart (base05_hr1, same whale, same Gridania banner). 3-2 and 3-4 are near twins. | Crops `[0,555,1280,960]` and `[870,312,1400,1050]` overlap at x 870-1280. `index.md`: "3 game paintings and 2 of ours". |
| m2 | Minor | base-15 3-5 | The lower half is 11 identical arc bricks (r30, sweep 70, same angle), spaced like filler. They trace no feature, unlike 3-3's ridge and arches. | `base-15.json` bricks; trace sheet y 330-520. |
| m3 | Minor | base-13 3-3 | The stuck-ball rescue fires far more often here than on the rest of the stage, and 2 long flights. Players will notice it. | `stuck_fires` 120 against 49, 49, 15, 18; `long_flights` 2 against 0; sweep `stuck_share` 0.021, the stage's highest. |
| m4 | Minor | base-13 3-3 | Two oranges against the right wall, just outside the cathedral's right flank, each decide about a quarter of lost games. Losing to a peg tucked against a wall feels cheap. | Holdouts (702,228) 0.25 and (714,292) 0.23. They sit above the y 400 gate line, so the gate never sees them. |
| m5 | Minor | base-14 3-4 | The movers have three parallax speeds, which is nice. But the painted banks they "ride" are static, so the claimed "cloud banks sliding past" is really pegs sliding over still clouds. On the wavier crests (crest 5), pegs at the end of their slide sit off the crest line. | Slide offsets 20/26/32 px at periods 11/8.5/6.5 s. Scene `motion` has only stars and one mist band. |
| n1 | Nit | base-12 3-2 | The outer-beam holdouts sit exactly on the 25% line at y 403. | Holdouts (667,403) 0.25 and (133,403) 0.21. |
| n2 | Nit | base-15 3-5 | Held-out 19.2 against fresh 17.9 is the widest gap in the stage, the others being 0.5 or less. The finale gap of 4.48 is the largest so far (stage 1 3.23, stage 2 3.47). It is acceptable as escalation, but 3-5 is the board to watch for spikes. | `stage-mr3.txt` |
| n3 | Nit | base-11 3-1 | Small 1-3 peg fragments (dragon, flying dragon, compass, isles) make the upper left read as scatter. The lane from the airship to Ishgard and on to Gridania is the only strong line. | Trace sheet y 150-300. |
| n4 | Nit | base-15 3-5 | The painting's showpiece, the vortex around the floating isle, sits in the launcher's empty zone and behind the launcher arm. The pegs only arc under it. | Trace sheet, the red circle at the top centre. |

## The stage as a set

- **Theme.** The stage holds together well as "The Night Skyway": the chart, the cloud sea, Ishgard, the lane, then the open sky. Every board is night-graded, and Cid and Brass Wings fit.
- **Difficulty ramp.** It is textbook. The pooled scores fall 27.67, 25.77, 23.73, 22.70, 18.22. Every step resolves 0.5 (the 3-3 to 3-4 step of 1.03 ± 0.41 only just). The opener sits above stage 2's finale (20.65), so the saw-tooth resets properly.
- **Close losses.** Close losses run high (3-3 `lost_with_low_orange` 0.48), which is good tension.
- **Spectacle peaks in the middle.** 3-3 is the richest and most legible board, and the finale is weaker than it.
- **Pairs.** Visually the set pairs up: 3-1 with 3-5 (the same chart), and 3-2 with 3-4 (purple moon over the cloud sea). The mechanics vary well: rings and arcs on 3-1, mirrored beam lines on 3-2, a long brick run on 3-3, movers on 3-4 (the stage's only motion mechanic, and it has no bricks), outlines and arcs on 3-5. The look varies far less than the mechanics.
- **What would fix the set.** A tighter whale crop on 3-5 and a recognisable airship element on 3-4. M1 and M2 are the whole fix.

## Per-level verdicts

### base-11 3-1 The Airship Road: APPROVE
- **Scene.** The world chart is lavish: the painted airship, dragons, the dashed lane, banners, cloud humps. The lane from the airship south-east past Ishgard to Gridania is the readable spine, and the hex rings around the holds and shields add structure. The two cloud-hump arcs on the right sit on the painted cloud shore.
- **Small window.** The dark grade leaves the lower land murky at 640. It is acceptable for an opener.
- **Difficulty.** The easiest board in the stage (27.6), which is right for an opener.
- **To fix.** n3 only.

### base-12 3-2 Above the Cloud Sea: APPROVE
- **The best composition in the stage.** The beacon beams fan up from spires on the cloud sea toward a big moon. The pegs ride the beams, and light strings hang between the spires. The board is mirrored, legible at 640 and pretty when cleared (the beams and strings remain).
- **Bricks.** The seven arcs sit low as cloud billows and earn their place.
- **To fix.** n1 only.

### base-13 3-3 The Holy See: APPROVE (with m3 and m4)
- **The showpiece.** Ishgard's cathedral, the bridge on its arches, and a pink dusk ridge whose crest is traced by an 11-brick run. The bridge arches carry 3 small arcs, and the rose window gets a peg ring. It reads as Ishgard at both sizes.
- **Density.** The left half (the sky) is looser than the dense cathedral, but that balances the picture.
- **Feel.** Two playtest numbers stand out: 120 stuck-ball rescues and two right-wall holdouts. Look at where the rescues fire: probably the ridge apex near (162,149) or the arch cups, now that the arches are 10 thick. If it is an arch cup, open it slightly. Move or demote the wall oranges at (702,228) and (714,292).

### base-14 3-4 The Airship Under Way: REVISE (M2, m5)
- **What works.** The lane in perspective (beacon diamonds closing toward the horizon), the cloud-tower outlines and the parallax movers are good ideas. The movers give the stage its only motion mechanic.
- **What fails.**
  - The board does not read as its title. Nothing says "airship".
  - The painting is the plainest in the stage: flat, mushy banks, green enough to read as hills at 640.
  - It shares its sky, moon and beacons with 3-2, the board just before the previous one.
- **Fix: identity.** Give it an identity of its own. Options:
  - Paint a foreground piece of the ship: the bow rail, a brass prow, rigging lines or an envelope edge across a corner. Pegs or bricks can trace the rail.
  - Or move the moon low or off-board and change the sky's hue so it stops echoing 3-2.
  - Or rename it to what it shows ("The Lane by Night").
- **Fix: painting and motion.**
  - Richer cloud modelling: rim light on the crests, internal billows.
  - Push the second jewel toward a cooler cloud hue.
  - A slow drift in the bank's own picture, or a per-crest mist band, so the movers ride moving cloud (m5).

### base-15 3-5 The Sky Whale: REVISE (M1, m2)
- **What works.** The finale's difficulty is right (18.2, the hardest yet). The bird flocks are traced nicely. The chart's vortex and labels give the board atmosphere.
- **What fails.**
  - The whale is tiny, dark and only ringed by pegs, so the namesake does not read. At 640 it is absent.
  - The lower half is 11 identical arcs.
  - The crop overlaps 3-1's chart, so the finale shows the opener's map, zoomed out.
- **Fix: the whale.**
  - Recrop base05_hr1 tighter around the whale and flocks, so the whale spans at least about 30% of the board width (the finale should zoom in, not out).
  - Or lift it with a rim or plate light like 3-3's silhouette treatment.
  - Then have the pegs trace its outline (back, head, flukes, fin) instead of an oval, so the subject reads as "a creature in outline", as its report claims.
- **Fix: the lower half.** Replace the uniform billow arcs with a feature: the flocks' flight line, cloud crests, or the whale's wake.
- **The ramp.** Re-run it after recropping. The current gap of 4.48 leaves room, and 3-5 should not drop below about 17 pooled.

## Method
- **What I worked from.** The review pack (index, stage table, composites, engine renders at 1280 and 640, cleared renders, trace sheets and their reports, the report JSONs), the shipped level files `Tsukimichi.Core/Moonfall/Levels/base-11..15.json` and the scene files `scenes/{aldenard-airship-lane,cloudsea-beacons,holy-see,cloudlane,sky-whale}.json`.
- **Engine renders.** They are mid-play shots (score above zero, some pieces already cleared), so I judged the full layouts from the composites.
- **Files.** Nothing in the worktree was edited, built or run. My crops are in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/mr3gd/`.
- **Unverified.** In-game motion and Fever, and whether the movers' drift is visible at play speed.
