VERDICT: REVISE (0 blockers, 1 major, 5 minors)

| id | sev | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | 1-3 | The stuck rule fires 154 times in 1,728 games. The shipped maximum is 88, and precheck rates this FAIL. Balls come to rest on the two east dune crowns, which were left closed. The layout's own note says a closed crown's apex holds a ball, and it opened only the two west crowns. | `layouts/base-03.py:117-118` (`gap 0`) against `:115-116` (`gap 70`, with a summit moon) and the note at `:13-14`. `mfl.py stuck base-03`: 13 of the 17 resting points are at x 655-695, y 280-370, on the far crown (~y 316) and the mid crown (~y 384). |
| m1 | Minor | 1-2 | The stuck rule fires 80 times in 1,728 games (precheck WARN). Balls rest where the west switchback road's upper leg passes under the derrick's feet. | `mfl.py stuck base-02`: 9 of the 20 resting points are at (195-270, 425-455). Derrick feet are at y 388 (`base-02.py:58-63`); the road is at `:69-73`. |
| m2 | Minor | 1-2 | Bricks wrap the water tank's sides and run down the outer stilts. The tower loses its box-on-stilts shape and reads as an arch. Next to the brick A of the derrick, the pair reads as the letters "A ∩". | `base-02.py:65-68`. In `base-02-engine-1280.png` the tank's outline is hidden under the bricks. The painting has a clear domed tank on four stilts. |
| m3 | Minor | stage | Three of five boards open with the same shape: two large arcs bracketing the launcher. These are 1-1's "hills", 1-2's Milky Way and 1-5's rings. On the contact sheet the stage's top halves look alike. | `sheet.png`; `base-01.py` (hills), `base-02.py:53-55`, `base-05.py:70-73` |
| m4 | Minor | 1-3 | The two "moonbeam" bricks match nothing a player can see. Neither the painting nor the engine render shows a beam, so they look like stray sticks in the sky by the moon. | `base-03.py:112-113`; `sagolii-cactuar@2x.png` has no shafts; `base-03-engine-1280.png` upper left |
| m5 | Minor | 1-3 | 45 of 98 pieces (46%) trace no feature (precheck WARN at 30%). The footprint "track" is two rows of moons spaced 36 units apart, so the lower left reads as a generic peg grid, not footprints. | `base-03.py:137-139`; precheck `patches`; lower left of `base-03-engine-640.png` |
| n1 | Nit | 1-1 | Bricks are 22% of the pieces, under the 30-45% guidance. The layout justifies this from the subject, a trail of places, and I accept the reason. | report `brick_share 0.224` |
| n2 | Nit | 1-5 | The sheet prints "margin -1.7" in red as if it were a fault. The band rule applies to the stage mean, not to one level, and the finale is meant to sit low. | `sheet.png`; charter 1.1 |
| n3 | Nit | 1-5 | The first 48 seeds win 14 games, against 5.8 per 48 held out (about 3.6 SE apart). Seeds 1-48 are inside the tuning block (seeds 1-864), so this is probably sampling, not a defect. No action. | `status`; `base-05.py:17` |
| n4 | Nit | stage | 1-1 shares its jewel hues with 1-3 and with 1-4 (none are neighbours; precheck WARN). Three of five boards sit in the same violet and red colours. This is UX's call. | precheck `repeats` |
| n5 | Nit | 1-3, 1-4 | The lower third is almost never orange: 11% of 1-3's candidates and 6% of 1-4's. The rule allows this, but every end-game shot on these boards goes to the middle and top. | `status` low3 column |

## Stage verdict

**Rules: all met.**
- Every level has 95-105 pieces, inside the 95-115 target. Orange candidates are 61-66% of pieces.
- The lower third holds at most 30% of candidates on every level; 1-2 sits at the limit with 29.5%.
- Bricks are 31-34% of pieces except on 1-1 (n1).
- Held-out wins per 48 are 8.4, 9.0, 8.8, 8.3 and 5.8. The stage mean of 8.06 is inside the band of 7.47-9.67.
- The finale wins 5.8 per 48 against 8.63 for the other four. The gap is 2.8, about 6 standard errors, so it is clearly resolved. Peggle's own stage 1 finale is 4.42 against 8.57.
- The 48 rule games give 6-14 wins on every level, above the floor of 2.
- Aces are re-suggested in `aces.json` (190k, 180k, 220k, 210k, 180k).
- `mfl.py check` passes on all five levels, and `status` shows every build and convert fresh.

**Curve and pacing.** The curve is Peggle's shape: four levels about level and then a clear drop for the finale. Levels 1-1 to 1-4 lie within 0.7 per 48 of each other, about one standard error, so the stage feels flat until 1-5. That is right for the game's first stage. 1-1 is not the gentlest of the four (1-2 is, by 0.6), but the difference is within noise, so I'm not raising it.

**Theme and variety.** The stage keeps its Waking Sands story: the road, the town, the creature, the city and the Crystal. All five techniques differ (trail, skyline band, creature, landmark, constellation). Stage 1 has no movers, as the charter allows. The one repeat is visual: the arc pair over the launcher (m3).

**Spectacle.** The refit made every board look richer. The old boards were sparse dots over a painting; these are full Peggle boards with real structure in brick. 1-4 and 1-5 look lavish, and 1-3 reads best of the five at 640 px. The weakest subject read is 1-2, whose town is still clear but whose landmarks have turned into letter-like shapes (m2).

## Per level

**1-1 Road to Horizon (base-01): APPROVE**, nits only.
- The map, the engraved dashed road and the land masses read at 1280 and at 640.
- The refit fills the empty west with a sea lane and a compass rose, which gives that side some purpose.
- With 65 candidates, dealt oranges no longer light the road, as the old 30-candidate board did. The road is now drawn in blue moons, which suits the owner's "most pieces can be orange" decision.
- At r 10 the six-moon rings of the place stops read less clearly as rings, but they still sit on the places.
- The lost games stay tense: 25 of 41 end within 2 oranges, and no holdout exceeds 13%.
- The two hill crowns sit on contour lines the player can barely see (part of m3).

**1-2 Horizon by Night (base-02): APPROVE once m1 and m2 are fixed**, both small.
- The roofline band of moons sits on the painted roofs, and the town reads.
- The Milky Way bricks ride a real painted arch. The arch is faint, and the bricks cover its crest, so a player mostly sees two big brackets (m3).
- The lower funnel (two switchback ramps, a three-row pyramid and the campfires) gives a lively end-game bottom.
- The shared west-road and derrick-feet pocket is mildly sticky (m1).
- Suggested fix for m2: brick only the domed crown and the stilts below the tank (from about y 320), and leave the tank's sides as moons. The tank then keeps its silhouette.

**1-3 The Cactuar (base-03): REVISE** for M1.
- It has the stage's best figure. The cactuar is unmistakable at 640, and the eyes and mouth are pegs on the painted face, which is charming. Brick flanks and legs give the ball real silhouette edges.
- M1 matters for feel, not only for the gate: 9% of games stop for a stuck ball.
- The rests sit directly under the east twinkling star (centre at 640, 222), which is also the level's top holdout area: (670, 239) 16%, (640, 222) 14%, (670, 205) 14%, (640, 256) 13%. The shots a player most needs, at that star, are the ones that stall on the crowns below it.
- Suggested fix: open the two east crowns at their summits with a moon in the gap, as the west crowns already are. Then re-tune, because the board will play easier.
- m4 and m5 are polish: drop or re-aim the beam bricks, and stagger the track as real footprints, not two rows.

**1-4 The Gilded Dome (base-04): APPROVE.**
- It is the stage's richest board: a nine-brick dome crown over the painted dome, the lesser crowns in orange and brick spires.
- The difficulty lever is the east alley beside the tower column; holdouts at x 684, y 331-411 are 12-18% each and 44% together. That reads as a deliberate target, not a trap.
- There are 9 stuck rests in 777 aims, which is fine.
- The bottom terrace row is almost entirely blue (n5).
- The two west "sail" bricks look more like ramps than sails. That's acceptable, because the painting finishes the shape.

**1-5 The Crystal's Call (base-05): APPROVE.**
- It is a proper finale. A crystal of brick and stars sits under the launcher, framed by four radiating rings. It is the stage's most theatrical board and clearly harder.
- The ball rarely stalls: the stuck rule fires 7 times, the fewest of the stage.
- Lost games end further from a win (4.0 oranges left on average; 13 of 34 within 2), which fits a hard finale.
- The crystal itself (vertical brick bars and an upper "/\") reads more as a shrine or lantern than a faceted crystal. The painted constellation lines carry the facets, so I'm not raising it.
- On n2: the red −1.7 margin on the sheet is not a rule failure.

## Method

- Read: the agent brief, charter section 1 and the owner's answers, the five layouts, the pack's reports, composites, engine renders at 1280 and 640, and `sheet.png`.
- Compared before and after against each level's composite at `a3295281^`. Each finding cites a painting where it depends on one.
- Ran `mfl.py status --stage mr1` (plus `--json`), `mfl.py check base-01` to `base-05` (all PASS), `mfl.py precheck` (1 FAIL, 19 WARN), and `mfl.py stuck` for base-02 and base-03.
- I edited nothing, and `git status` is clean at `76c94f40`. `status` and `precheck` write crop images only to the gitignored `docs/design/v9/levels/build/precheck/`.
- There are no `*-mr1-round-*` supervisor files. The earlier stage 1 approvals are in `supervisor/game-designer-round-1..6.md`.
- Not verified: hand play in the game, and how the runtime motion and dress look on these boards.
