VERDICT: APPROVE (0 blockers, 0 majors, 3 minors)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| R1-M1 → m1 | Minor (downgraded from Major; shared with the game designer) | 1-3 base-03 | Mostly resolved. The cluster on the right shoulder and arm, which caused the round-1 Major, is gone. The stuck rule still fires 98 times in 1,728 games. That is 10 over the shipped maximum of 88, and 4 to 10 times the rest of the stage (10-25). | Report `stuck_fires` 154 → 98: WARN 70, under the FAIL of 100. `mfl.py stuck base-03` gives 6 stuck first shots (was 17). Two are left on the east side, at (655,295) and (675,300). Four are on the west mid-dune ledge: (160,360), (170,360), (250,360) and (230,480). |
| R1-m1 | Resolved | 1-5 base-05 | The orange-to-ground separation under protan vision is back at the MR 5 guard, and the core column no longer takes orange. | Pipeline p10 0.104 → **0.105**; minimum 0.090 → 0.098. Engine, dealt oranges p10/min: 0.116/0.105 → **0.123/0.110** at 1280 and 0.119/0.107 at 640. Worst candidate place at 640: 0.102 at (440,244) → **0.108** at (440,412). Peg (440,244) and bricks 6 and 7 are now `canBeOrange: false`. |
| R1-m2 | Resolved | 1-2 base-02 | No peg sits under the Ace chip any more. | The corner pair is now at (104,104) and (696,104). The top of peg (696,104) is about 33 units below the chip: about 20 px at 640 and 42 px at 1280 (zoom `r2/b02_tr.png`). No piece now has x > 600 and y < 95. Hypothetical F6 at (696,104) is 0.43 or more at 640 for every kind. |
| m3 | Minor (carried, not fixed) | 1-4, 1-1 | The worst hypothetical F6 at 640 is still at the 0.20 floor. | Re-measured, F6 for a hypothetical purple at 1280/640. 1-4 peg (168,340): 0.217/**0.196**. 1-4 brick (208,348): 0.224/**0.198**. 1-1 peg (150,220): 0.229/**0.202**. 1-4's pieces are unchanged. The tune made 1-1 (150,220) an orange candidate, and orange there reads 0.257/0.219, with protan separation not among the level's three worst. The rationale in the response (the pipeline passes at 0.264 and 0.326) is acceptable for shipping. |
| m4 | Minor (carried, not fixed) | 1-4 base-04 | The dome crown is still 87.5 units from the pivot, against a clearance of 85. The aim guide runs 4-5 units on central aims. | base-04 is unchanged in the diff. The response says lowering the crown 8 or 12 units failed another gate. I agree this needs its own pass. |
| n1 | Nit (carried, new instance) | 1-2 | The pipeline's F9 brick minimum on 1-2 is now 0.061, the stage's lowest. The engine does not show it. readcheck also reports an arc's centre, not the brick, as its "worst at" place. | Pipeline `protan_min` 0.078 → 0.061 at `['brick',542,487]` (the x1,y1 of the lower east road). Engine hypothetical orange at (522,482) and (560,491): 0.118 and 0.120 at 640. The dealt minimum is 0.116 at 1280. 1-5's worst is reported at `['brick',440,332]`, which is the rings' arc centre, not a piece (readability.py:343 uses `x`/`y` for arcs). |
| n2 | Nit (carried) | 1-1 | The glint is hidden on 57% of the road. | Not changed. |
| n3 | Nit (carried) | stage palette | With the colour-blind marks off, green and orange are hard to tell apart. 1-5's seed-1 deal now puts a green segment of the outer right ring between two orange segments. | `base-05-engine-*.png`, right outer ring. The marks are still not rendered in the pack. |
| n4 | Nit (tooling) | 1-1, 1-2, 1-5 | `status` and `index.md` say convert is "stale" for 1-1, 1-2 and 1-5. The runtime level files are byte-identical to the pipeline json. | `cmp` on all 5 files: same. Round 1's transform fits all five engine renders (peg-centre L 0.74-0.78). `convert_state` (review.py:164) compares the commit times of the scene recipe. Convert leaves an unchanged recipe as it is, so these levels can never show as fresh. "changed since review" says "never reviewed" for every level. |
| n5 | Nit (record accuracy) | 1-3 | The response says the east crowns "open at the summit with a moon in the gap". No peg was added. The crowns were only shortened. | Peg count is 68 before and 68 after. The mid crown went from 625-700 to 625-663 and lost one arc. The far crown went from 624-696 to 624-686. The diff reads "0 added, 1 removed, 2 reshaped". The bare ends read as shorter crests, not broken bricks, so there is no UX cost. |
| n6 | Nit (pre-existing) | 1-2 | Peg (585,55) still sits about 10 units below the SCORE plate. The plate's gold frame drags its ring-based F6 down to 0.07 at 640. | In the zoom the peg reads clearly as pale blue on navy. It was there before round 1 and is unchanged. I list it only so the next layout touch leaves it alone or drops it about 15 units. |

## Per-level verdicts

| Level | Changed | Dealt F6 min at 640 | Protan dealt p10/min (1280) | New pieces | Verdict |
|---|---|---|---|---|---|
| 1-1 Road to Horizon | Candidate set only (6 in, 6 out) | 0.282, blue brick (250,398) | 0.142/0.134 | none | APPROVE (m3, n2) |
| 1-2 Horizon by Night | 4 added, 6 removed, 8 moved or reshaped | 0.361 | 0.135/0.116 | F6 0.36 or more for any kind, gaps 15.9 or more | APPROVE (n1, n6) |
| 1-3 The Cactuar | 2 east crown arcs shortened, 1 arc removed | 0.341 | 0.142/0.138 | none | APPROVE (m1, n5) |
| 1-4 The Gilded Dome | Not changed (not re-reviewed) | – | – | – | APPROVE (m3, m4 carried) |
| 1-5 The Crystal's Call | 3 core pieces lost orange candidacy | 0.363 | 0.123/0.110 | none | APPROVE (n3) |

**1-1, Road to Horizon.** No piece moved. The tune swapped six orange candidates and wrote a TUNING block, and the lower-third share fell from 18% to 15%. One new candidate is peg (150,220) on the pale Waking Sands block, my m3 spot. Orange reads there (F6 0.219 at 640), and protan separation at the level's candidate places is 0.131 or more at 640. The engine render changed only by the new deal. No regression.

**1-2, Horizon by Night.** m2 is resolved, and none of the new pieces costs readability.
- **Corner pegs:** the pair at (104,104) and (696,104) sits on open night sky. The right one now clears the Ace chip by about 20 px at 640 and 42 px at 1280.
- **New stars:** (205,120) and (595,110) beside the arches read at F6 0.41 or more at 640, with edge gaps of 16.2 and 24.7 units.
- **Tank pegs:** (540,300) and (578,304) sit inside the water tower under its crown. On the dark window grid, orange reads at protan 0.156-0.168, and under protan it becomes mustard on navy (`r2/crops640s.png`). Cutting the side bricks to start at y 340 leaves the tank framed by its crown and stilts, which reads cleanly.
- **Launcher:** the pieces near the launcher are unchanged, and `in_swing_px` is 0.
- **Motion:** the scene's slow light shafts (k 0.05, from the top left) already crossed this corner before. The new corner pegs add no new moving element near a piece.
- **Art:** the horizon-by-night cloth, cover and rim art was regenerated for the new pieces, and the renders show it.

**1-3, The Cactuar.** The fix landed where my round-1 evidence pointed: the right arm and shoulder bricks went from most of 17 resting first shots to 2. Four of the remaining six rest on the west mid-dune crown ledge (160-250, 360), which this round did not touch.
- **Stuck rule:** 98 fires is under the FAIL but still the stage's outlier. A player meets the stuck rule in about 1 game in 18, against about 1 in 70 to 170 on the other four levels. I downgrade it to Minor. Whether to open the west mid crown the same way is the game designer's call.
- **Readability:** the shortened east crown bricks read at F6 0.30 or more for any kind at 640, and the dealt oranges on them separate at protan 0.145-0.166.
- **Scene lights:** the converter re-laid the two amber lights to (409,316) and (312,354), 21.6 and 23.7 units from the nearest piece edge. They have no `flicker`, so they are static dress. Their core of about 1 unit cannot be mistaken for an orange peg.
- **Launcher:** clearance is unchanged (`min_clearance` 7.9, `in_swing_px` 0).

**1-5, The Crystal's Call.** m1 is resolved.
- **Core column:** peg (440,244) and bricks 6 and 7 always stay blue now. In the protan crop the teal core holds only blue pieces, and the dealt oranges around it read as olive or gold on grey-teal.
- **Protan figures:** the pipeline p10 reaches the guard exactly (0.105). The engine has margin: dealt minimum 0.107 at 640, and the worst candidate place 0.108 at (440,412).
- **Remaining issue:** the green segment between orange segments on the outer ring is a matter of the deal and the palette (n3), not of this change.

**1-4, The Gilded Dome.** Not changed. m3 and m4 remain open as Minors, and the response explains why. They do not block the stage.

## Regressions looked for
- **Piece spacing:** the closest peg-to-peg gap is 14.0 units on 1-2 and 14.1 on 1-3. Nothing merges at 640.
- **Kind readability:** every dealt piece of every kind reads at F6 0.28 or more at 640 on all five levels (dealt-face measure). The lowest is 1-1's unchanged blue brick, at 0.282.
- **Launcher:** nothing sits in the swing on any level.
- **Ace text:** the chip's number changed only in value (base-01 200k, base-03 210k, base-05 190k). The digit count is the same, so the chip's width is unchanged.
- **Code:** the changes to `MoonfallWindow.cs` and `MoonfallGameArt.cs` add render and test hooks only (`ArtRunningForRender`, `ArtPendingForRender`). Nothing a player sees changes.
- **Render reliability:** the renderer now waits until the game art has settled, so the pack's engine renders are more trustworthy than in round 1.

## Method and what is unverified
I worked read-only. I ran:
- `mfl.py status --stage mr1`;
- `mfl.py check base-01..05`: all PASS;
- `mfl.py stuck base-03`.

The worktree is clean afterwards. I re-ran the round-1 measurement scripts on the refreshed pack (Oklab F6 face against ring p90, Machado protan a/b core against ring), with the round-1 transforms, which still fit all five renders. Outputs are in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/ux/r2/`: measure2, f62, hyp, dealt, newp, the stuck output, `b02_tr.png` and `crops640s.png`.

**Unverified:**
- motion over time;
- the colour-blind marks;
- Reduce motion;
- tritan vision;
- where the 98 stuck fires on 1-3 happen in later shots (only first shots were mapped).
