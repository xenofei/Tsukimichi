VERDICT: REVISE (0 blockers, 1 major, 4 minors)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | base-20 4-5 | Round 1's M1 is only partly fixed. The structure part is done: the board now has 15 bricks, crowns and ramparts. The sands still don't read. At the foot the field is plum, and the dunes are near-black humps that almost vanish at 640. At 640 the east third is still a double U of moons over a ghost you can't see. | Pixel at the foot of `cleared-1280` (640,700) is rgb(110,49,89), a magenta-plum, not sand or umber. The dunes at the corners are rgb(24,15,28), with a hairline rim. In `engine-640` (x2) the Thal ghost is barely lighter than the field, so the "thal arc" and "thal inner arc" read as two nested arcs over nothing. In `cleared-1280` the lifted ghost reads as a pale "Ul"-like glyph, a watermark rather than streets. |
| m1 | Minor | base-17 4-2 | Round 1's M2 is mostly fixed. The residual: east of x 720 the right half is still five near-flat rows. | In `engine-1280` the high streak sits at y≈320, the lens top ≈380, the seam ≈440, the underside ≈495 and the horizon ≈597. The `trace.png` lens edges converge only west of trace x≈470; from x 520 to 720 they run parallel (y 278 and 362). |
| m2 | Minor | base-17 4-2 | The new sand grade reads as a tint box. Its hard top edge cuts the far dome in half (teal top, ochre bottom), and it fades back to teal about 30 px before the right wall. | `cleared-1280` crop at y 560-780: the dome's ochre starts on a straight line at y≈618, and the teal strip runs from x≈1020 to 1050. |
| m3 | Minor | base-16 4-1 | Round 1's m4 is still open. The three blurred cover patches still show on the plan. | `cleared-1280` (270,340), (400,395) and (710,290). The fix commit declines it because the runtime blur can't reproduce the hatching. |
| m4 | Minor | stage | The ramp can't be checked from the pack. `stage-mr4.txt` is stale (08:26, round-1 figures), and the index lists 4-5 pooled as "(stage fixer)". The worktree also holds uncommitted orange swaps on 4-1, 4-2 and 4-3. The current held-out values imply a wider finale step. | Report `play.ramp_per_48` values are 26.0 / 25.2 / 23.7 / 22.1 / 18.0. Those steps are 0.8, 1.5, 1.6 and 4.1. Round 1 pooled was 3.43, and the charter's 22 → 18.5 is 3.5. `git status`: base-16/17/18 json and layouts modified, plus `MoonfallRender/Program.cs`. |
| n1 | Nit | base-16 4-1 | Still one brick; carried from round 1's n1. | `preflight.bricks` 1 |
| n2 | Nit | stage | 4-2 and 4-5 are now both LM, so the stage has four techniques. The charter allows this (at least four, none twice in a row), but the finale now echoes 4-2's move. 4-1 and 4-5 still both have gilt dashed roads running off the board (round 1's m3 residual). The `-ticks` border is gone from 4-5. | charter rows 4-2 and 4-5. `engine-1280` 4-5 has west, north and east roads; 4-1 has two arrow routes. |
| n3 | Nit | base-20 4-5 | The two "sand stars" stand alone in the open field, and the west one is dealt green. At 640 they read as strays. | `engine-1280` (280,395) green and (1000,278). Layout tag "sand star". |
| n4 | Nit | process | `response-mr4-round-1.md` doesn't exist. I checked the fold-in against the five fix commits' messages (5be6ee37 to fac9cc5a) instead. | `ls supervisor/` |

## Round-1 findings

| R1 id | Status | Check |
|---|---|---|
| M1 (4-5 not "from the Sands"; plainest board) | **Partly fixed → M1** | **Fixed:** the structure. There are 15 bricks (3 before), with crowns on the north-west tower and the Exchange, orange segments, and ramparts on the east sweep and the west face. The `-ticks` border is gone. It is no longer the plainest board; 4-1 now is. **Not fixed:** the sands. The prescribed "warm, dark desert" grade came out plum, and the dunes are black silhouettes. The east half was rebuilt on the lifted ghost, but at 640 it still reads as a U over nothing. |
| M2 (4-2 murky, green ground, filler rows, motion) | **Mostly fixed → m1** | **The craft:** the hull is now a deep red, with the dotted keel visibly beneath it, so the subject string holds. **The ground:** ochre (#B88E5C), which reads as desert, though see m2. **Motion:** beams and 8 flickering lamps confirmed (`convert --check`: flickers 8). **The right half:** a tongue converging on the spire with a blue seam, a real shape at its west end. Its east third is still rows. |
| m1 (4-3 lamps hidden, no flicker) | **Fixed** | The centre moons are gone, so each flame sits bare in its ring and glows warm gold. Flickers 9 (was 0). The lamps read as the board's motif at both sizes. |
| m2 (4-3 stuck 155) | **Fixed** | 56 per 1,728 in the current report (52 at the fix commit). Sweep: 5 of 681 stuck. |
| m3 (three plans in one script; 4-1 and 4-5 alike) | **Mostly fixed → n2** | The tick border is removed, and 4-5's crowns and ramparts separate it from 4-1's route walk. The dashed off-board roads are still shared. |
| m4 (4-1 blur patches) | **Open → m3** | Declined as a runtime limit. |
| n1 (4-1 one brick) | Open → n1 | Unchanged. |
| n2 (4-4 stray moons) | **Fixed** | Both moons now sit on the east ring's faces, and `engine-1280` shows nothing at x≈1010. |
| n3 (4-4 held-out/fresh gap) | **Fixed** | Held-out 22.1 against pooled 21.95. |
| n4 (4-5 star line on the rim) | **Fixed** | The neat-line seam is removed and runtime stars are off. Mist and road glints carry the motion. |
| n5 (4-3 pack showed the tally) | **Fixed** | `base-18-cleared-1280` is now the bare scene, and the render tool's `cleared` moment now avoids a burst-reachable orange (uncommitted). |

**Regressions:**
- 4-2's sand box (m2).
- The stage's technique variety drops from five to four (n2).
- The finale step widens to about 4.1 held-out (m4). For feel, a finale that bites harder is acceptable, provided pooled confirms it near the charter's 3.5 to 4.

Elsewhere I found none:
- 4-1's dash restyle (10 on, 6 off) reads as a trail, not as aim dots.
- 4-4's verdigris move keeps the arena legible.

## Per-level verdicts

**4-1 base-16 The Steps of Thal: PASS (m3, n1).**
- It's still a calm, legible opener: the Gold Court hub, two arrowed routes and the blue-and-red grade.
- The longer, fainter dashes help at 640.
- For m3, which needs no runtime blur:
  - bake a cloned hatch tile from the map itself into the scene image at build time (a static patch, as the route layers already are);
  - or set a small label cartouche or brass plate over each patch so it reads as dressing.
- If neither is possible, ask the owner to accept it.

**4-2 base-17 The Spire of the Sands: PASS with minors (m1, m2).**
- The subject now delivers: the spire flanked by moons, a red airship overhead with its keel dotted beneath it, and a sand floor.
- For m1: bend the lens's east ends (lift the top edge and drop the underside as they reach the wall, or stop them short at x≈900). Also break the horizon row into a short dune crest, so the east third is a shape rather than a ruled sheet.
- For m2: feather the sand's top edge onto the painted horizon, carry it to the wall, and either keep the far dome out of the jewel mask or grade the whole dome.

**4-3 base-18 Lamps on the Steps: PASS.**
- This is the stage's prettiest board, and now its lamps are the point. Bare warm flames sit in open rings, flicker, and the pools drain instead of cradling.
- At 640 the glows read as a staircase of lights.

**4-4 base-19 The Coliseum: PASS.**
- It's still the stage's spectacle (31 bricks, tiers, spokes), now with glints round the rings.
- The east ring is closed and the board is clean at both sizes.

**4-5 base-20 Ul'dah from the Sands: REVISE (M1).**
The finale now has a spine. The north-west tower and the Exchange wear brick crowns, the east rampart has its orange segment, and the west rampart stands out in the sands. It plays as a proper wall board. What's still missing is the title. Keep the owner's city map and LM, and fix the reading:
1. **Regrade the lower third and the dunes toward warm umber or ochre**, the same jewel move 4-2 used (its #B88E5C sand). A deep night sand works if its hue is about 30-50°, not 320°. Give the dunes a moonlit crest bright enough to survive 640, so the city visibly sits above the desert.
2. **East third.** Lift the Thal ghost until its streets read at 640, or drop the inner arc and let the outer arc plus the brick street carry it. Either way, a 640 viewer should see why the moons curve there.
3. **Optional.** Take the green off the west sand star, or move it onto the west road's line so it reads as a waypoint.

If the owner wants the plum field kept as "night sands", that is a taste call they can make. Then M1 drops to a Minor and the stage approves.

## The stage as a set
- **Theme.** The walk reads: from the Gate of Thal to the spire under the airship, up the lit steps, into the Coliseum, then the city wall. Only the last beat, "out to the sands", is still missing its colour.
- **Variety.**
  - The palettes are distinct (blue-red, teal-ochre, indigo, blue-green, plum-teal), and the three city plans now each have their own move: a route walk, an arena of rings, and a wall in brick.
  - Technique count is down to four (n2), which is acceptable.
  - Sand-toning 4-5 would also echo 4-2's sands as a bookend. That earns the stage title better than any rename.
- **Ramp.** The direction is right: 26.0, 25.2, 23.7, 22.1, 18.0 held-out. The pack's stage table is stale, so I can't confirm the pooled steps (m4). The stage fixer's `mfl.py stage mr4` must land in the pack before round 3.
- **Spectacle arc.**
  - Bricks run 1, 4, 2, 31, 15.
  - 4-4 remains the peak, and 4-5 is now a solid second rather than a drop to the floor. That matches MR 3's accepted shape (finale below the 3-3 peak, m1 there).
  - The stage no longer ends on its weakest board. It ends on a good board that doesn't yet look like its name.

Reviewed: the pack in `C:\Users\devon\Desktop\Tsukimichi (Main Repo)\.claude\worktrees\wf_2529e0f6-97c-6\docs\design\v9\levels\review\mr4\` as it stands in the working tree, including the uncommitted stage-fixer state. Unverified: in-motion flicker, glints and mist; the pooled ramp.
