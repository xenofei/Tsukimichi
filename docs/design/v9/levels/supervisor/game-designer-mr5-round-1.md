VERDICT: REVISE (0 blockers, 2 majors, 4 minors)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | base-23 5-3 | The stage's title board has no warm light. The grade turns the painting's gold quay lamps and windows a cool slate, and the reflections come out beige-grey. Only the buoy lantern is a light and flickers. 8 of the 10 lamp heads sit under a peg. This is MR 4's 4-3 m1 again, on the board the whole stage ("Harbour Lights") is named for. | In `trace.png` the lamps and windows are gold. In `cleared-1280` the lamp heads are (580,268) rgb(98,117,147) and (495,272) rgb(106,120,145), and only the buoy (655,452) is warm at rgb(168,138,95). `harbour-lights.json` has 1 `lights` entry, the buoy at (410,331). The charter's motion is "mist, lamp flicker". `composite` shows the same cool lamps, so the pipeline grade causes it, not the engine. |
| M2 | Major | base-24 5-4 | The board doesn't read as "the lamplit bridge … moons on its lamps". There are no lamps in the crop. The "lamps" row is pegs on a dark deck, and nothing lights up. About 12 pegs in the upper half sit on no feature, and the deck is a ruled row of 16. With the rose fog at the foot, this is the stage's murkiest board and its weakest subject. | In the `cleared-1280` crop (230-870, 380-640) the deck and arches show no lamp, glow or light points. The `trace.png` labels are castle wall, cables, chimneys, tower legs, a "lamps" row of 7 at y 330 on nothing, and a "deck band" of 16 at y 370. Loose pegs sit at about (180,80), (250,130), (160,170), (318,230), (440,215), (500,170), (520,70), (590,70), (560,150) and (618,150). The scene's only lights are the two frame lamps, and its motion is mist and dust, against the charter's "lamp flicker, mist". |
| m1 | Minor | base-21 5-1 | "White towers over the bay": the towers come out slate-lavender, and the foot is a saturated green fog that reads as lawn or hedge, not water. The three "pillow" arcs on the right sit over empty haze. The charter's "beams" motion is missing. | `cleared-1280`: tower (470,470) is rgb(71,78,113), and the foot (950,700) is rgb(0,76,46). The `limsa-white-towers` motion is dust plus a teal mist, with no shafts. In `engine-1280` the arcs at about (985,380), (985,510) and (960,630) sit over nothing. |
| m2 | Minor | base-25 5-5 | The finale's title feature is faint when the board is still. Each lighthouse is a 1.0-1.3 px pinprick with no glyph or halo you can read as a lighthouse. The beams are shafts at k 0.08, so the still board reads as soft haze. The 180° beam from Aleport leaves a ruled horizontal edge across the west sea. There are 3 bricks. The board is legible, but it isn't lavish without motion. | `cleared-1280`: lamp (555,543) is rgb(249,237,204) at about 3 px. The step at y 542 to 545 runs across x 230-535: (400,542) rgb(50,93,96) against (400,545) rgb(58,99,103), and the same step is at x 300 and 480. In the scene, `shafts` are k 0.08 with `moving: true`. The turning beams are unverified, because there is no MoonfallRender binary in the worktree and the renders are stills. |
| m3 | Minor | stage | The stage pairs up and leans green and blue. 5-1 and 5-4 are both crops of base01_hr1, both painted Limsa spires in blue-violet. 5-2 and 5-5 are both blue-violet maps with a dashed ferry lane and a compass rose. 5-1, 5-3 and 5-5 all have green feet. 5-3 repeats 4-3's slot and technique (ours, LS, lamp glows). | Look at the five `engine-1280` together. The 5-2 `glints` and 5-5 `route` plates are both dashed lanes. The region hues are #44B07A (5-1), #48B07E (5-3) and #3FAE74 (5-5). |
| m4 | Minor | base-22 5-2 | The "south compass" ring of 7 pegs sits over a rose the foot darkness has nearly erased. At 640 it reads as a ring over nothing (the east compass is fine). | In the `cleared-1280` crop (290-590, 500-700) the rose is only just visible, against a clear rose at (908,545). |
| n1 | Nit | base-23 5-3 | The interior is 9 near-identical vertical combs. That is true to the subject, but the middle of the board plays like plinko. The buoy ring and the two quay bricks are the only breaks. A wider waver or a gap in one or two columns would add texture. | `trace.png` has columns `refl Q1`-`Q6` and `refl J1`-`J2` at a steady spacing of about 80 px. |
| n2 | Nit | base-22 5-2 | This is the third board on the base05 world chart (after 3-1 and 3-5), and the second trail on it in three stages. The violet and copper grade separates it from 3-1, so it is acceptable. | `sources-plan.md`, charter row 3-1. |

## Per-level verdicts

**5-1 base-21 The White Towers: PASS (m1).**
- A good opener. At 640 the castle reads at a glance: two peaked spire outlines traced in moons, four tower columns, a window arch with its heart peg, and the ship's rigging dotted at the left.
- Plays well: 25.9 held out, stuck share 0.001, 10 bricks, holdouts spread around the board.
- The boundary from 4-5 (a map) to 5-1 (a painting) changes texture, as the charter wants.
- **For m1:**
  - Pull the foot's region from green toward sea teal or blue-grey so it reads as the bay under the towers.
  - Lift the towers' value toward white.
  - Optionally add slow shafts for the charter's "beams".

**5-2 base-22 The Strait of Merlthor: PASS (m4, n2).**
- The subject reads well. The dashed lane leaves the Limsa Lominsa banner, crosses under the "Strait of Merlthor" lettering, and lands on Thanalan's coast with Ul'dah's shield. Both coasts are dotted, the Sagolii arcs are orange, and the galleon and east compass dress the edges.
- It is lavish for a map.
- The integration's foot haze is a faint violet at 640 and in the cleared board. It adds nothing and costs nothing. Accepted.
- Stuck 11 (0.016) and 51 fires are fine.
- **For m4:** either lift the south rose, as the north glow does, or move that ring up onto lettered sea.

**5-3 base-23 Lights on the Water: REVISE (M1, n1).**
- The composition is the stage's best: crescent moon, crenellated quays, a curving quay wall, the jetty with its bricks, and reflections running down to the bucket. It is clean at 640.
- The board sells warmth, and the grade removes it.
- **Fix for M1:**
  - Hold the lamp heads, windows and reflection streaks warm, with a warm `valueHues` high end or a gold region masked to the lamps and reflections.
  - Add a flickering `lights` entry at each of the 10 lamp heads.
  - Take the peg off each lamp head: put it on the head's reflection, or ring the head.
  - Re-check the ramp after any candidate moves. The 0.97 and 1.37 steps either side have about 0.06 of slack.

**5-4 base-24 The Arches of the Bridge: REVISE (M2).**
- What works: the three arch bricks sit right on the painting's great arches. The towers and dome at the right are handsome. It plays cleanly (stuck 0, bucket 99).
- What doesn't: "lamplit" and "moons on its lamps" have no support in the art.
- **Fix for M2:** either option works.
  - (a) Add warm flickering lights along the deck and on the tower balcony, and put the "lamps" pegs on them.
  - (b) Re-subject the board to "the bridge and its cables", trace the cables with intent, and drop "lamplit".
- Either way, fold the upper scatter into features (cables, chimney tops, the dome's crown as a brick), and break the 16-peg deck row into spans between the piers.
- Tone the rose foot down so it reads as dusk mist rather than a red smear.

**5-5 base-25 The Lighthouses of Vylbrand: PASS (m2).**
- A proper finale mechanically:
  - it is the hardest board (17.8);
  - the stage's first movers, two opposed pairs orbiting Aleport and the east cape, escalate the mechanics;
  - the rings are legible at 640;
  - the east sea's contour arcs give the open water a shape.
- What it lacks is spectacle when still.
- **For m2:**
  - Give each light a readable lighthouse mark: a bigger warm core and halo, or a small tower glyph on the plate.
  - Raise the beams' k so they are visible at 640.
  - Feather the 180° beam's edge so it stops reading as a seam.
- Then check the beams in motion (UX) before calling it the stage's peak.

## The stage as a set
- **Theme.** The arc is clear: the Admiral's towers, the chart of the strait, the harbour at night, the bridge, the coast's lighthouses. The trouble is that the theme is "Harbour **Lights**", and today no board in the stage shows warm harbour light. 5-1 and 5-4 have none, 5-3 grades its lamps cold, and 5-5's are pinpricks. Fixing M1 and M2 (and m2) makes the title true.
- **Variety.** It is adequate but clumped (m3): two paintings from the same source with the same grade, two blue maps with dashed lanes, and three green feet. 5-4's fix is the best chance to separate it from 5-1. Warm lamps over the rose dusk would do it.
- **Ramp.** The pooled values are 25.45 / 24.02 / 23.05 / 21.68 / 18.25, with steps of 1.43, 0.97 and 1.37. The finale gap is 3.43 ± 0.40, within one standard error of the 3.5-4 I set in MR 4. The mean is 22.49 against the charter's 22.6. Accepted. Aces are all above the hard-level line. Stuck fires run 19-62, all well under MR 4's 155 flag.
- **Finale.** Its play is right: the hardest board and the first movers. Its look is quieter than 5-3. With m2 fixed, it should close the stage on lights turning over the sea.

Re-check in round 2:
- M1 and M2 in `engine-1280`, `engine-640` and `cleared-1280`;
- a fresh `stage-mr5.txt`, if any candidacy moves (above all, the 5-2 → 5-3 step);
- 5-5's beams in motion.