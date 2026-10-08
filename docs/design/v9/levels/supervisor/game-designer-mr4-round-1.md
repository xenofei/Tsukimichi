VERDICT: REVISE (0 blockers, 2 majors, 4 minors)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | base-20 4-5 | The finale doesn't read as "from the Sands" and is the plainest board in the stage. The stage peaks at 4-4 and ends on a flat city plan. | `engine-1280` and `cleared-1280` show a top-down teal map on a magenta field. Nothing on the board looks like sand, a horizon or a skyline. The subject string ("its walls' skyline from the sands") only holds in the layout docstring. There are 3 bricks (one dome crown) against 4-4's 31. The sweep's bucket count is 93, the stage's lowest. The east half is an oval of 14 moons over the "ghost ring", which is almost invisible in `cleared-1280` and at 640 reads as a U of pegs over nothing. The charter theme ends "…and out to the sands". |
| M2 | Major | base-17 4-2 | The board is murky and half filler. The spire reads, but the "red airship's keel" is a dark smear, the ground is green, and the right half is flat rows of pegs. | `cleared-1280` shows a soft teal painting with the spire as a dark rock-like mass. The craft at the top right is a navy blotch, not red, and the "craft keel" diagonal runs under nothing you can recognise. The bottom band is bright green, which reads as grass, not sand (the same failure as MR3 R1 M2's "mint hills"). `trace.png` shows the right half x 380-720 as four near-horizontal rows (y 295, 345, 420, 490), about 30 pegs. Motion is `{"dust":18}` only. |
| m1 | Minor | base-18 4-3 | The lamps the board is named for are hidden under the centre moon of each rosette, and the engine shifts their warm gold to dim pink. There is no lamp flicker, although the charter row says "lamp flicker, dust". | `scenes/assets/lamp-steps.jpg` shows 10 gold lamps. In the `engine-1280` zoom each lamp head sits under a moon, with a faint pinkish halo. The `lamp-steps.json` motion is dust and stars, and it has 0 `flicker` lights. |
| m2 | Minor | base-18 4-3 | The stuck-ball rescue fires more here than anywhere else so far. Players will notice it. | `stuck_fires` 155 (4-1 71, 4-2 15, 4-4 84, 4-5 66). MR3 3-3 was flagged at 120. The shipped maximum is 88. Sweep `stuck_share` 0.021. |
| m3 | Minor | stage | Three of the five boards are top-down Ul'dah plans in the same map script. 4-1 and 4-5 share one visual vocabulary: moons hugging the walls, gilt dashed roads running off the board, and a `-ticks` border plate. | Composite montage: 4-1, 4-4 and 4-5. The dashed edge border appears on 4-1 and 4-5 only. Both have dashed exits leaving the frame (4-1 arrows top left and east; 4-5 west, north and east roads). |
| m4 | Minor | base-16 4-1 | Three soft, blurred rectangles (the cover plate) are visible on the map, plainly so once the pegs clear. | `cleared-1280`, about (270,340), (400,395) and (710,290). They look like smudges on the plan, beside "The Gold Court". |
| n1 | Nit | base-16 4-1 | There is one brick, and the bottom third of the plan (the Goldsmiths' and Miners' guilds) is unused. That's acceptable for a gentle opener, but the structural play starts thin. | `report`: bricks 1. `engine-1280`: no pegs below y 580. |
| n2 | Nit | base-19 4-4 | Two moons stand alone outside the east ring (one of them green) and read as strays from the lattice. | `engine-1280` at about (1010,375) and (1010,435). |
| n3 | Nit | base-19 4-4 | Held-out against fresh is 23.3 / 21.8, the stage's widest gap. Watch this board for spikes. | `stage-mr4.txt` |
| n4 | Nit | base-20 4-5 | The index asks whether a line of stars runs along the frame. Still renders can't show twinkle positions. The dashed right edge in `cleared-1280` is the map-ticks border (it's also on 4-1), not stars. UX should check this in motion. | Right-edge strips compared across all five levels. |
| n5 | Nit | pack | 4-3's `cleared-1280` shows the Level Clear tally, so the pack has no bare view of the scene. I judged the painting from `scenes/assets/lamp-steps.jpg` instead. | `base-18-cleared-1280.png` |

## Per-level verdicts

**4-1 base-16 The Steps of Thal: PASS (m4, n1).**
- The opener is calm and legible: the Gold Court's ring is the hub, and the walk is a gilt dashed route with arrowheads at both ends. Moons hold the route's stops, and glints run along the walk.
- The blue-and-red grade is the boldest map look in the stage. Its 1.10 step to 4-2 is fine.
- Fix the three blur patches; they cheapen the reveal.
- The one brick (the Steps' vertical bar) is a little bare. A pair of bricks on the Steps of Thal's terraces would give the opener some structure without making it harder.

**4-2 base-17 The Spire of the Sands: REVISE (M2).**
The left half works:
- moons flank the spire on both sides at a set offset;
- an arc brick caps it, and the skyway is a diagonal brick;
- the far dome's arch sits near the bucket.

The rest falls short:
- **The craft.** The painting is soft at this crop and the grade buries what the subject promises. Grade the craft back towards its red, or put a warm rim on it, so the keel line visibly sits under something. If it still can't read, drop "under a red airship's keel" from the subject.
- **The green ground.** Pull it to ochre or sand-dark so the base reads as desert, not lawn.
- **The right half.** Replace the parallel rows with a shape: cloud tongues that curve, a staggered skyway rail of short bricks, or a second partial outline. Rows over cloud bands are filler at this bar.
- **Motion.** Give it a signature. The charter row says "beams, lamp flicker". The spire's lamps are already `flicker: true` in the scene's light list, but the motion block is dust only; confirm the runtime actually animates them.

**4-3 base-18 Lamps on the Steps: PASS with minors (m1, m2).**
- This is the stage's best-reading painting at both sizes. The dome, minarets, moon, zig-zag stairs, banners and the warm doors make a clear Ul'dah night, and the lamp rosettes are a charming, readable motif.
- Owner's rule: the dome and minarets are flat architectural silhouettes, the charter row is "light only, no figure", and nothing figurative is code-painted. I judge it compliant.
- **m1 fix:** take the centre moon off each lamp (ring the lamp with six) or offset the rosette below the lamp head. Keep the lamps' warm gold through the grade and add flicker to them.
- **m2 fix:** find where the 155 rescues happen. The rosettes' inner pockets and the handrail runs are the likely cradles.

**4-4 base-19 The Coliseum: PASS (n2, n3).**
- This is the stage's spectacle: concentric brick tiers with orange segments, spokes, the arena floor plan at the centre and dashed ring guides. Raubahn's own ground suits his stage.
- It reads instantly at 640, and the 31 bricks give the stage its structural peak.
- The Thal tag over the charter's Nald follows `sources-plan.md` and is fine.
- The integration nudge (224.5 degrees, trim 1) is invisible in play.

**4-5 base-20 Ul'dah from the Sands: REVISE (M1).**
The owner chose a city map for 4-5, so the fix should work within that choice:
- **Make the sands visible.** Grade the off-map field as warm, dark desert with a sand-haze band (the lilac mist at y 520-594 could become a dune haze), or add a low skyline or horizon motif along the bottom. "From the Sands" then reads without a rename.
- **Give the finale structure.** The city wall is the subject, and wall wants brick. Turn runs of the roofline band and the west face into brick (with orange segments, as 4-4 does), and give the north-west tower a brick crown to match the Exchange.
- **Fix the east half.** Either lift the Thal ghost ring until it reads, or rebuild that half around something visible (the east road into the sands, a caravan route).
- **Option for the owner:** if a rename is preferred ("The Walls of Nald"), the charter theme's "out to the sands" is lost and the finale still needs the spectacle above. The charter's fallback, Central Thanalan's area map, would show the city in the desert, but that reverses the owner's call, so it goes to them, not to the build.

## The stage as a set
- **Theme.** The walk holds: Gate of Thal, the spire, the lit steps, the Coliseum, then out of the city. Every board is night-graded, and Raubahn and Lunar Burst fit. The last beat ("out to the sands") isn't delivered yet (M1).
- **Variety.** There are five techniques (TR, LM, LS, LA, SB) with no repeats, which is good. The palettes vary: blue-red, teal, indigo, blue-green, magenta. But three plans in the same script, with 4-1 and 4-5 sharing route-and-wall moves, make the stage feel more like one map than five places (m3). Fixing M1 and M2 with sand-toned grading and visible craft and skyline motifs would separate them.
- **Ramp.** Pooled scores run 26.5, 25.4, 23.45, 22.18, 18.75, and the steps 1.10, 1.95, 1.27 and 3.43 all resolve against ±0.41. The mean of 23.26 matches the charter's 23.1. The finale gap of 3.43 sits within stages 1-3's range. Aces are 240-280k, all above the hard-level line. The ramp needs no change.
- **Spectacle arc.** The stage builds well from 4-1 through 4-3 to the 4-4 peak, then drops (M1). As it stands the stage ends on its weakest board, the same failure MR3 round 1 flagged as M1. Bricking and dressing 4-5 should bring the finale at least level with 4-4.
