# Level-design critic, levels round 1

5 October 2026. Worktree `agent-a570460c913ca1c79` at 732efa9b. I edited no repo files. I did not run `mfl.py build`, and `git status` is clean after my runs.

**Overall verdict: REVISE.** Nine of the ten levels are approved, with Minors. base-02 has one Major. The pipeline has three Majors: its checkers for cups and mover clearance can be bypassed, and the stuck gate has no self-test.

## Method

- **Self-tests and the shipped loader.** `mfl.py selftest` passes all 44 cases. `mfcheck validate` returns OK for all ten `json/*.json` files. Every level file is byte-equal to `Board.level_json()` rebuilt from its layout script, so script and file have not drifted.
- **Trace sheets and composites.** I ran `mfl.py trace base-01…10`, and viewed every composite at 1x plus 2x crops of base-02 and base-08. `mfl.py stage 1`, `stage 2`, and `mfl.py stuck` for base-01, 02, 04 and 09.
- **My own probes against the levels:**
  - mover clearance along the whole path, sampled 360 times: to still pegs, to bricks, mover against mover;
  - peg-to-brick gaps of 13 to 20 units, flat brick spans, pieces in the bucket's lane;
  - `canBeGreen` on movers and bricks;
  - point-in-polygon of base-02's pegs against the cactuar's silhouette;
  - F9 recomputed with the oranges on movers included.
- **Synthetic known-bad cases against each checker:**
  - pre-flight: 17 cases;
  - shipped loader, sweep and stuck gate: 3 boards;
  - readcheck (F6, F7, F9): 8 cases;
  - framecheck (F3a, F5 on a mover's path): 2 cases.
- **A shielded piece.** base-06 re-swept with its main arch's bricks removed, to see whether the unreached lantern opens.
- **Probe scripts:** `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/critic/probe/`.

## Verdicts

| Level | Verdict | Reads as its subject | Notes |
|---|---|---|---|
| base-01 Road to Horizon | APPROVE | Yes: stops and roads on the chart | Minor L1 |
| base-02 The Cactuar | **REVISE** | Mostly, but a row of pegs crosses the body | **Major C1**, Minor C2 |
| base-03 Ul'dah Across the Sands | APPROVE | Weakly: the painting is murky, the towers read as columns | Minor L3 |
| base-04 The Gilded Dome | APPROVE | Yes: the crown of five bricks carries it | Nit L4 |
| base-05 The Crystal's Call | APPROVE | Yes, clearly | Nit L5 |
| base-06 Limsa Across the Water | APPROVE | Yes: arches, deck and lamps | L6 (shielded lantern verified) |
| base-07 Moonpath on the Bay | APPROVE | Yes: the moon's road is the strongest read in stage 2 | Minor L7 |
| base-08 The Kraken's Sea | APPROVE | As a chart of wonders; busy | Minor L8 |
| base-09 The Strait of Merlthor | APPROVE | Yes: the strait reads as two dotted coasts | Minor L9 |
| base-10 Twin Lanterns | APPROVE | Yes: the gate and its moving reflection | Minor L10 |
| Pipeline and checkers | **REVISE** | — | **Majors P1, P2, P3**; Minors P4–P10 |

Figures from the reports: greedy wins per 48 run from 19 to 30. Sweep stuck shares run from 0 to 4.1%, the highest being base-02 at 7 of 171. Pre-flight problems: none on any level.

## Findings

### C1 (Major), base-02: the far dune runs across the cactuar's body
- **Where.** `layouts/base-02.py`, `b.trace("far dune", …)`.
- **Evidence.** Point-in-polygon against the recipe's `cactuar` silhouette finds six pegs inside it:
  - the face's three holes (two eyes and the mouth), which are intended;
  - three far-dune pegs at (398.6, 348.2), (434.2, 350.9) and (474.1, 347.7), which are not.

  On the 2x composite those three read as a belt across the chest. This breaks section 2's rule that the layout sits on the subject's edges, never on its face. The level's technique is "a creature in outline", so its interior should stay open.
- **Fix.** Split the far-dune polyline where it enters and leaves the silhouette, or skip traced points inside it. Better, a pipeline fix: `Board.why_not` refuses points inside any closed feature the layout declares as the subject's silhouette (for example `b.subject("cactuar")`). The pre-flight then also fails a hand-placed peg there, unless it is marked as a key feature, like the eyes.

### C2 (Minor), base-02: level brick decks hold balls
- **Evidence.**
  - Brick 9, from (665, 386) to (700, 386), is dead level (0.0°).
  - Bricks 3 and 4 at the middle crest's apex slope 2.8° and 4.9°.
  - `mfl.py stuck base-02`: 5 of the 7 stuck first shots rest on these decks, at (180, 360), (205, 355), (210, 360), (665, 370) and (665, 375).
  - The stuck share is 4.1%, against a 5% limit. The greedy player sees 6 stuck-rule fires in 48 games.

  This is exactly the README's own lesson: "a long, shallow deck of brick holds a resting ball".
- **Fix.** Draw the right dune's top (its last two bricks) as dotted pegs. Steepen or split the middle crest at its apex: a dotted cap of two pegs over x 180–245.

### L1 (Minor), base-01
- **The compass rose reads as a stop.** It is a ring of eight moons round a heart, in the same visual language as the route's stop rings, in the empty desert.
  - Fix: open the ring (four cardinal points only), or put smaller r 7 pegs on the points so it reads as engraving rather than a place.
- **F9 (orange for a protanope) is 0.091.** This is the lowest of all 16 boards, the 6 pilots included (theirs run 0.101–0.141). It passes only by the relative clause (see P9).

### L3 (Minor), base-03, and also stages 1 and 2: the same painting twice in a row
- base-03 and base-04 both crop `-nowloading_base02_hr1`.
- base-08 and base-09 both crop `-nowloading_base05_hr1`.

  Back-to-back levels from one painting weaken variety. Neighbours' jewels repeat too: only four distinct jewel pairs across the ten levels (285/195 four times, 285/15 three times).
- base-03's skyline also reads weakly: the towers are indistinct dark shapes.
- **Fix.** For one level of each pair, use another loading image. If that is not possible, at least swap the order so the pair is not adjacent.

### L4 (Nit), base-04: finials stand inside the README's wedge band
- Four finials stand exactly 16.0 units above their crowns: `y − r − 30`.
- The terrace row is 13 pegs at y ≈ 503, a full row 8 units above the bucket's lane.
- Sweep stuck is 2 of 171, so nothing is observed. Note it for later levels.

### L5 (Nit), base-05
- 13 of the constellation's line points were skipped while tracing (8.0 from a star), so the lines are thin. The painted lines carry them.

### L6 (verified, no action), base-06
- Arch lantern peg 16, at (575, 441), is the one piece no first shot reaches. With the middle crown brick removed, the sweep reaches it. So it obeys the "reachable once the shield clears" clause.
- The cloud summits stand 14.0 units above their crowns, with 0 stuck shots.
- The level has 60 pieces, at the pre-flight's floor.

### L7 (Minor), base-07
- **Wave crests reach the bucket's lane.** Bricks 3 and 6, at (196, 518) and (500, 520), reach y 523–530. The lane check does not count bricks (see P5).
- **Docstring drift.** The docstring says "the pier is a deck of brick"; the layout traces it dotted.
- **Fix.** Raise both crests by about 12, and correct the docstring.

### L8 (Minor), base-08
- **Busy.** There are six wonders, two diagonal rhumb lines, a current and an open-sea scatter, 79 pieces in all. The rhumb line from (480, 150) to (250, 540) cuts through the galleon and the wrecks without reading as anything.
  - Fix: drop the rhumb lines, or keep one clear of the wonders. The kraken ring and the whirlpool already carry the board.
- **An islet sits close to the whirlpool.** The islet peg near (530, 340) passes 12.5 units from the whirlpool's orbit path. That is legal (12 or more), but under the 14-unit spacing the trace would have kept had it seen the path (P2).
- **F9 is 0.121**, at the floor.

### L9 (Minor), base-09: the right half is sparse
- 12 of 171 first shots hit nothing (7%, the highest of the ten), and the median is 5 pegs per first shot (the lowest).
- Peg 15, at (149, 138), is never reached: a blue peg high in the top-left that a purple can be dealt to.
- **Fix.** Add 6–8 pegs across Thanalan and Aldenard: hills or roads, x 420–700, y 200–420. Drop or lower peg 15.

### L10 (Minor), base-10: movers checked and sound; greens on movers not checked
- **Clearances along the whole path, sampled 360 times:**
  - mover to still peg, 14.2 at least;
  - mover to mover, 17.2 at least;
  - no bricks near any path.
- **The drifting reflection is safe.** The engine moves every mover on the global game clock (`MoonfallGame.Body.Move`: `seconds % Period`). So movers with the same offset and period stay rigid, and the pre-flight is right to check their spacing as still pegs.
- **Not checked: greens on the movers.** 11 of the 21 movers carry `canBeGreen: true`. The pre-flight's greens rule (`greenable`) excludes movers, so their green status and reach are never checked (see P8).
- **F9 leaves out the reflection.** 13 of the 25 candidates are movers, and F9 skips them. With them included I measure 0.135, so it passes, but nothing in the pipeline measures it.

### P1 (Major), pipeline: the "never a cup" check can be bypassed
- **Where.** `author.py` `Board.check`, in the cup section.
- **Evidence.** These synthetic cases raise no problem:
  - a V of two touching bricks in one run (the run test needs three bricks);
  - a three-brick cup laid with `b.line()`: not in `self.runs`, so never examined;
  - a V-valley where two concave-down crowns' feet touch: different runs, gap under 3.5, so no notch either.

  On the shipped engine, a board of seven such V's fires the stuck rule on 11.1% of first shots. That is about 1.6% per V, so one or two slip past the 5% sweep gate. No current level has one: I checked every touching brick pair.
- **Fix.** Test geometry, not runs. Take every brick pair whose gap is under 3.5 (touching). Fault the junction when both bricks slope down into it, meaning the tangents' heights rise away from the joint on both sides. Extend the run test to two bricks. Add all three cases to the self-test.

### P2 (Major), pipeline: mover clearance is enforced only in part
- **The rule.** Section 4's overlap rule: movers "pass at least a ball's width" from any still piece.
- **Evidence:**
  - `Layout.check` checks movers against still pegs only, never against bricks.
  - An orbit passing 4 units from a brick passes the pre-flight. Added to base-08, it also passes the shipped loader (which only rejects overlap): `validate` returns OK.
  - Two slide movers with different drifts that swap places pass `check()`. The loader skips mover against mover by design.
  - `why_not` ignores orbit paths for still placements: an orbit's `_drift` is `None`, which equals a still peg's `group`. `why_not(400, 340)`, on an orbit's path, returns `None`, and only `check()`'s 12-unit backstop catches it. That is how base-08's islet came to sit at 12.5.
- **Fix:**
  - in `check()`, sample each mover's path against bricks (12 or more) and against movers of a different drift group (12 or more at equal phase);
  - in `why_not`, key orbits by `(kind, centre, period, direction)` so a still peg sees their path;
  - add the 4-unit orbit-and-brick case and the swapping slides to the self-test.

### P3 (Major), pipeline: the engine gates break the standing self-test rule
- **No self-test for the stuck gate.** It has neither a known-bad nor a known-good case. Its only engine case tests `unreached`, and the build never acts on that number.
- **Evidence.**
  - My flat-decks board, with 28 level bricks in four decks, passes the pre-flight's cup and cradle checks and loads. The sweep fires the stuck rule on 46 of 171 first shots (26.9%). It is the known-bad case the gate is missing.
  - `build.py` reports `sweep.unreached` but never refuses on it.
- **Fix.**
  - Add the flat-decks board as the stuck gate's known-bad case, and the pilot base-p2 as its known-good case.
  - Gate `unreached` with the rule's own clause: drop every piece the first sweep reaches (padding blue pegs off-board in a side copy, so the loader's 25-candidate minimum still holds), sweep again, and repeat a few times. Refuse any piece still unreached.

### P4 (Minor), pipeline: the README's lessons are not checks
- **Level decks.** "A long, shallow deck of brick holds a resting ball" has no check. My 210-unit level deck passes the pre-flight.
- **The wedge band.** "A peg 13–16 units above a sloped brick wedges balls", yet the cradle threshold is 13: a peg 14 above a 18° brick passes.
- **Fix.** Fault any brick span under 6° longer than about 30 units, and peg-above-brick gaps under 16. Real levels with 14–16 gaps (base-02, 04, 06, 07) would need a look, or a whitelist where the sweep shows 0 stuck shots there.

### P5 (Minor), pipeline: the bucket's-lane check ignores bricks
- Six bricks in a row at y 545 pass the pre-flight. base-07 has two crests in the lane today.
- **Fix.** Count bricks whose lowest edge passes below y 520.

### P6 (Minor), pipeline: moving candidates are exempt from the reach check
- An orange slide mover at (100, 20) passes.
- **Fix.** Require some point of the mover's path to be in `direct_reach`.

### P7 (Nit), pipeline: touching level pegs are never a saddle
- A gap of 0.5 or less is never a saddle, yet a ball rests on two touching level pegs: `saddle_deg(0)` is 38.7°.
- A cage of ten pegs 0.3 apart round a candidate passes the pre-flight with no problems.
- The engine impact is low: 2 of 171 stuck shots for seven touching pairs.
- **Fix.** Treat gaps from 0 to 0.5 as saddles too. Pass the pegs' real radii to `saddle_deg`, which assumes r 10 (an r 11 pair at 55° slips by 1°).

### P8 (Minor), format v2: `canBeGreen` on bricks and movers is never checked
- **Bricks.** All bricks are written `canBeGreen: true`, including the subject itself:
  - base-04's great dome crown;
  - base-03's white dome;
  - base-10's arch crown.

  The shipped engine deals greens (and purple, as seen on base-06) to bricks.
- **Movers.** The pre-flight's greens rules (8 sure greens, all in reach) count only still pegs.
- **Fix.** Decide whether bricks may be green. If they may, mark the subject's crowns never-green and include bricks and mover paths in the reach check. If they may not, have `level_json` write `false` for them.
- **What is right.** The choices on pegs are sensible: eyes, stars, the moonpath, lantern heads, the kraken ring and the harp are never green, and every still greenable peg is in direct reach (my re-check: none out).

### P9 (Minor), readcheck soundness
- **F7: two hues 32° apart (269° and 301°) pass as two jewels** (second share 0.295), because the bins quantise to 30°. The README itself says hues within about 60° count as one.
  - Fix: measure the second jewel's distance as a hue difference, at least 60° between the window means, not as a bin count.
  - The stage's F7 neighbour test compares bin pairs, so a one-bin shift counts as "different".
- **F9 measures only part of the oranges.** It skips oranges on movers (a level whose oranges are all movers returns `None` and cannot fail). It also skips candidates this seed dealt blue: a candidate on an orange patch slips through.
- **F9's relative clause has no approved reference.** It compares a new board with its own undressed render, which no one approved. So the 0.12 floor is never enforced: base-01 0.091, base-02 0.101 and base-03 0.116 pass that way.
  - Fix: take the reference as the lowest approved pilot (0.101), not the level's own undressed board.
- **What holds.** F6 caught a bright patch at a mover's far end. F3a and F5 caught framing and a lamp 3–5 units from a slide's far end.

### P10 (Minor), process: ramp data is stale
- base-02, 04 and 10 have no `ramp` in their reports: they were rebuilt after the 144-game run.
- **The stage 2 curve, per 48 games:**
  - the 48-game figures are not monotonic (30, 26, 20, 25, 21);
  - the ramp, where run, is 26.3, 22.7, 21.3, 20.0;
  - stage 2 as a whole is easier than 1-5 (17.7). The finale base-10 (21 of 48) is no harder than 2-3.

  There is no written difficulty target, so this is for the designer to judge.
- **Fix.** Re-run `stage 2 --ramp 144` after the fixes, and decide whether 2-5 should be the stage's hardest.

## Unverified

- In-game behaviour once the engine reads `canBeGreen`. The play gate today runs with greens dealt anywhere, and the composites use a seed searched to avoid never-green pegs. With the shipped engine as it is, seed 1 puts a green on a never-green piece in base-07, 08 and 09.
- Stuck rates beyond first shots. The greedy games' stuck counts are the only evidence for later shots.

