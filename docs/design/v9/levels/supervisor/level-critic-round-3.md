# Level-design critic, levels round 3

6 October 2026. Worktree `agent-a570460c913ca1c79` at `35cd98b5`. I edited no repo files and did not run `mfl.py build`. `git status` is clean after my runs. The only writes were to my scratch directory and, through `mfl.py selftest`, to the gitignored `build/` selftest boards.

**Overall verdict: APPROVE.** Round 2's Major (G1) is resolved:
- The quiet can no longer print a per-peg coin.
- The new print gate fails round 2's per-peg quiet on the real boards (1-5, 2-3 and 1-1), not only on synthetic cases.
- No coin is visible on the veil-free cleared boards, even with saturation boosted 160%.

G2, G4, G5, G6, G7, L11 and L14 are resolved, with small residues. Four new Minors remain; none blocks shipping:
- **N1:** 1-1's losses are decided by its two lowest oranges, just above the cheap gate's line.
- **N2:** main's runtime scene format cannot express the new quiet.
- **N3:** the print gate has blind shapes.
- **N4:** the mover check stops at 240 s.

I would fix N1 before release; it is a one-peg change.

## Method

**Self-test and files**
- `mfl.py selftest`: all 88 cases pass (preflight, framecheck, readcheck, stage, engine).
- All ten `json/*.json` are byte-equal to `json.dumps(Board.level_json(), indent=2)` rebuilt from their layouts.
- All ten pass `mfcheck validate` and a fresh `Board.check()`.
- `build/json` and `build/composites` are byte-equal to the committed files.
- `mfl.py stage 1` and `stage 2` exit 0.

**Probes re-run against the new checkers**
- Round 2's probes: `bypass.py`, `bypass2.py`, `bypass3.py`, `levels2.py`, `ghost3.py`.
- New probes (`bypass4.py`, `chain.py`):
  - movers whose shared cycle runs past the 240 s cap;
  - decks at 10.5° and 12°;
  - lone level bricks 19.5 units long;
  - a chain of 15-unit level bricks;
  - a full row of pegs just above the bucket's lane;
  - synthetic dresses on the print gate: a 24-unit disc, a 40-unit disc, a ring 13–19 units out, a three-sector crescent, discs on two of five pegs.

**Print and quiet**
- `oldquiet.py`: the print gate on the current boards with round 2's quiet put back (no blur, gain 1).
- `quietmap.py`: each veil-free cleared board dressed with its quiet against the same dress without it. OKLab change per pixel, heat maps, and the cleared boards viewed at 1x.
- Main's `docs/design/v9/scene-recipe.md`, read via `git show main:`.

**Engine**
- Every level swept at 0.25° (681 aims) and at 0.05° (3401 aims), plus `reach`.
- `mfl.py stuck` on 2-1 and 1-1.
- 2-2's narrow dead lane traced at 0.03° steps.
- All ten re-played over 864 games (`cheap3.py`). The ramps reproduce the reports exactly. The share of oranges left was broken down by home y ≥ 400, 415 and 430.

**Geometry and visuals**
- Arch and brick gaps measured on 2-1, 1-3 and 1-2 (`arches.py`).
- Movers checked over real time: the LCM of the periods, sampled at 0.02 s.
- Viewed: every composite at 1x; the cleared, veil-free boards of 1-1, 1-2 and 1-5 (`img/quiet-*.png`).

Scripts and outputs: `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/critic3/`

## Verdicts

| Level | Verdict | Reads as its subject | Notes |
|---|---|---|---|
| 1-1 Road to Horizon | APPROVE (fix N1 before release) | Yes | **N1**: (368, 427) is left in 36% of lost games, and (504, 415) in 32%. 34% of lost games leave only these two. Ramp 29.3 (band 30) |
| 1-2 Horizon by Night | APPROVE | Partly: the skyline, derrick and tower read; the mesa does not (the town stands on a flat green field; the ledges are rows of moons) | Band in three runs reads well. Stuck 3/681. Below the town, three rows of blue (ledge, road, road) still read as a field |
| 1-3 The Cactuar | APPROVE | Yes, clearly | Dune bricks now 13–18.5°. Stuck 5/681 |
| 1-4 The Gilded Dome | APPROVE | Yes | Unchanged geometry; candidates swapped |
| 1-5 The Crystal's Call | APPROVE | Yes | G1 resolved: print median 0.0056; with round 2's quiet put back the gate gives 0.078 and fails. No coins on the cleared aurora. F9 p10 0.101 (on the pilot clause) |
| 2-1 Limsa Across the Water | APPROVE | Weakly (art call, as before): the bridge reads; the tower is a dark mass ringed with oranges | L11 resolved: slot 18.8, keystones 16.7. The slot's rest cell (600, 440) is gone from the stuck list |
| 2-2 Moonpath on the Bay | APPROVE | Yes, the strongest in stage 2 | Nit N5: a 0.16° dead lane at 33.27–33.43° |
| 2-3 The Kraken's Sea | APPROVE | As a chart of wonders | Print measured over 58 pegs (0.0045). Round 2's quiet would fail it (0.0152) |
| 2-4 The Ferry Under Sail | APPROVE | Yes, clearly | Swell rows broken into runs. With the wake they still read as a two-row band when still (L13, Nit). Mover gaps ≥ 16.0 over 63 s |
| 2-5 Twin Lanterns | APPROVE | Yes | 81% of lost games leave a low orange; the coordinator's call, passing on the ratio rule (see G3) |
| Pipeline and checkers | APPROVE | — | Minors N2, N3, N4; Nits N5–N8 |

**Figures (864 games, per 48; reproduced exactly by my re-run)**
- **Stage 1:** 29.3, 27.8, 26.6, 24.4, 21.4.
- **Stage 2:** 27.4, 26.6, 23.7, 22.3, 18.9.

**Quarter-degree sweep:** dead 0 on all ten; stuck at most 8/681 (1-1, 1-4); never reached `[]` on all ten.

**Movers over real time**
- 2-3: still pegs ≥ 12.7; bricks ≥ 43.5; mover to mover ≥ 14.1.
- 2-4: still pegs ≥ 22.0; mover to mover ≥ 16.0 over 63 s.
- 2-5: still pegs ≥ 14.2; bricks ≥ 26.3; mover to mover ≥ 17.2.

## Status of round-2 findings

| Finding | Status | Evidence |
|---|---|---|
| **G1** ghost check never looked where the discs are (Major) | **Resolved** | See below |
| G2 mover against mover | **Resolved**, residue N4 | See below |
| G3 cheap difficulty | **Partly resolved**; residue in N1 and on 2-5 | See below |
| G4 dead shots at whole degrees | **Resolved**; residue Nit N5 | Sweep step 0.25 (681 aims); 0 dead on all ten. At 0.05° one lane remains on 2-2 |
| G5 deck rule narrower than the lesson | **Resolved** | See below |
| G6 F9 skips orange bricks | **Resolved** | `orange_views` adds orange-able bricks; `_brick_sep` compares the core with 3–11 px outside. 2-2 now measures 28 places (27 pegs and crest 0) |
| G7 bands not gated | **Resolved** | See below |
| L11 2-1 near-notch between arches | **Resolved** at the level | See below |
| L12 reads as its subject | **Open** (art call) | 1-2's mesa still does not read; 2-1's tower is still a dark mass |
| L13 full-width rows read as fences | **Partly resolved** (Nit) | See below |
| L14 lane counts pieces, not width | **Resolved**; Nit residue N6 | The covered width is gated at 120 units. Round 2's A8 and A8c now fail on "cover" (566 and 540 units). Self-tested |
| L15 self-test coverage | **Mostly resolved**; Nit residue N7 | Cheap difficulty, stage and engine colours each have known-bad and known-good cases. Still missing: a known-good `why_not` case next to an orbit (it works: (400, 412) is accepted 13 clear of a 40-unit orbit) |

**G1 (Major): resolved**
- **The quiet.** `dress.py` blurs the quiet's field over `max(a, 40)` units (gain 1.6), so it cannot form a coin.
- **The gate.** `readability.dress_print` measures the dress's own change, every still peg, sector median. It is never vacuous: with fewer than 3 pegs it falls back to the whole board, and a board with no open ground returns 9.0, which fails.
- **Self-tests.** Isolated discs and clustered discs fail; an even dress and a region edge pass.
- **On real boards, with round 2's quiet put back** (`oldquiet.py`):
  - 1-5: median 0.078, p90 0.121 → fails;
  - 2-3: 0.0152 → fails;
  - 1-1: 0.0125 → fails.
- **As shipped:** 0.0056, 0.0045 and 0.0074, all pass.
- **Visually.** The cleared, veil-free 1-5 and 1-2, viewed at 1x with saturation boosted 160%, show no coins. The quiet now acts as wide bands (on 1-5 the whole aurora where the layout sits), not per peg.

**G2 (Minor): resolved, residue N4**
- Round 2's probes now fail:
  - A7b (periods 4 and 6, collide only at unequal phase): "come within a ball";
  - A6 (co-moving overlap and saddle): "overlap" and "saddle".
- Both are self-tested.
- Residue: the real-time window is capped at 240 s (N4).

**G3 (Minor): partly resolved; residue in N1 and on 2-5**
- Holdouts are now keyed by piece (a mover by its home).
- The gate is one orange at home y ≥ 430 in ≥ 25% of lost games, or low oranges over 1.5× their share of the deal. Both are self-tested, and an approved pilot passes.
- Not done: the share of lost games decided only by low oranges is reported (`lost_with_low_orange`) but not gated.
- The ratio form licenses itself: the more candidates sit low, the more low leftovers it allows. 2-5 passes with 45% of its deal low, 53% of oranges left low, and 81% of lost games leaving one. That is the coordinator's recorded call.
- The line moved from 440 to 430, and 1-1's hardest oranges now sit just above it (N1).

**G5 (Minor): resolved**
- Decks are now line bricks under 10°: a chain over 30 units, or one alone of 20 or more.
- Round 2's A4 (separate 28-unit bricks) and A5 (6.5° decks) now fail.
- The 10° limit holds in play: two 220-unit decks at 10.5° pass the deck rule and stick 3/681. Ten separate level bricks 19.5 long stick 7/681 (1.0%).
- 1-3's dune bricks measure 13.1–18.5°.

**G7 (Minor): resolved**
- `stagecheck.py` gates neighbours' jewels, a step of at least 0.5, the finale, and band ends within 1.6. Each is self-tested.
- Stage 1 opens at 29.3 (band 30) and closes at 21.4 (band 21).
- Stage 2 runs from 27.4 down to 18.9 (band 20, inside the tolerance).

**L11 (Minor): resolved at the level**
- The slot between the arches is 18.8, and both keystones are 16.7.
- `mfl.py stuck base-06`: 7 scattered rest cells, none at the slot.
- The pipeline's notch ceiling is still 12.5: round 2's B2 (12.8-unit slots) still passes the pre-flight, and sticks 5/681 against 1/681 for the control. No level is affected.

**L13 (Nit): partly resolved**
- 1-2: ledges in three runs.
- 2-1: swell in three runs.
- 2-4: rows broken at the ship's middle. With the wake they still read as a two-row band when still, which is acceptable.

## New findings

### N1 (Minor), 1-1: the campaign's first board is lost on its two lowest oranges, 3 and 15 units above the cheap gate's line
- **Where.** `layouts/base-01.py` `CANDS`; `engine.LOW_Y = 430`; `ease --pick`, which only excludes places at y ≥ 430.
- **Evidence** (864 games, re-run; holdouts as in the report):

  | Piece | Place | Left in lost games |
  |---|---|---|
  | 72, "west shore" | (368, **427**) | **36%** |
  | 33, "bridge" ring | (504, **415**) | **32%** |

  - These are the two largest single-piece holdouts on any level but 2-3, whose top holdout is at y 156.
  - **At y ≥ 415:** these two are 7% of the candidates, but 34% of the oranges left in lost games (4.9×). 61% of lost games leave one, and 30% leave only these.
  - **At y ≥ 400:** 3.5×, the highest of the ten. The others run 1.2–2.6×.
  - **Gate blind.** No 1-1 candidate is at y ≥ 430, so the ratio rule is vacuous there (`low_left_share` 0.0) and the single-piece rule passes 0.36 at y 427.
  - **Against the docstring.** It says the board "is won on the route, not lost in the bucket's lane". Round 2's G3 found the same pattern (0.30 at y 439, passing by one unit). The line moved, and the share rose.
  - **Not needed for spread.** The spread rule's west-half low candidate is already (108, 402), so (368, 427) is not needed.
- **Fix.**
  - **1-1.** Make (368, 427) blue. Give its candidacy to an upper-route place (`ease --pick --skip west shore`), and re-run the ramp: 29.3 has room up to the band's 30 + 2.4.
  - **The gate.** Apply the 25% single-piece rule from y 400, the spread rule's floor. Only 1-1 exceeds 25% there; the next highest are 1-3's (567, 404) and 1-4's (556, 420), both at 0.23. Gate `lost_with_low_orange` too, or say in the README that a y line plus a ratio is the accepted reading.

### N2 (Minor), pipeline: main's runtime scene format cannot express the new quiet
- **Evidence.** Main's `scene-recipe.md`, "Masks", defines the `near` term like this:
  - 1 within b units of a piece, 0 beyond a;
  - its one option `blur` is "0–40 units, **before** the smoothstep";
  - `scale` is 0–1, so there is no gain above 1.
- **What the pipeline does instead** (`dress.py`): `clip(1.6 · blur(smooth(a, b, dist), max(a, 40)))`. The blur comes after the smoothstep, the gain is 1.6, and 1-1's `[50, 14]` needs a blur of 50.
- **Why it matters.**
  - Blurring a distance field before a smoothstep still yields a disc of radius a round each peg: round 2's coin.
  - A converter mapping `quiet` to `near` as the format stands therefore ships round 2's print. The print gate fails that exact configuration on 1-5, 2-3 and 1-1 (`oldquiet.py` above).
  - The round-3 context records only that the print should be re-measured on runtime output. It does not say that the format cannot carry the fix.
- **Fix.** Before the converter, extend the runtime `near` term:
  - a `spread` (blur after the smoothstep, up to 50 units) and a `gain` (up to 2);
  - or bake the quiet's spread field at load in exactly the pipeline's form.

  Then run `dress_print` on `tools/Tsukimichi.MoonfallRender` output as a converter gate.

### N3 (Minor), print gate: blind to prints outside its two rings, and to prints on three sectors or fewer
- **Evidence** (synthetic dresses on the self-test's flat rose sky, five pegs, r 9):

  | Dress | Median | Verdict |
  |---|---|---|
  | Hue disc 24 units out (the self-test's case) | 0.051 | fails |
  | Disc 40 units out | 0.0 | **passes** |
  | Ring 13–19 units from the peg's edge (between the 5–12 and 20–36 rings) | 0.0 | **passes** |
  | Three-sector crescent under each peg (a baked drop shadow) | 0.0 | **passes** |

- **Risk.** The current dress cannot produce any of these, because the blur prevents it. They matter for N2's runtime re-measure and for future recipes.
- **Fix.**
  - Add a third ring, 40–56 units out, and take the largest of near-vs-mid and near-vs-far.
  - Measure the annulus 12–20 as well.
  - Take the upper quartile over sectors, not the median, and keep the region-edge self-test passing by requiring two opposite sectors.
  - Add the three passing shapes as known-bad self-tests.

### N4 (Minor), pre-flight: movers of different periods are checked for at most 240 s
- **Evidence.** Probe C1: two slides on one line, periods 10 and 10.1 (LCM 1010 s), the second 103 units ahead.
  - Minimum gap over 240 s: 18.6.
  - Within a ball's width (12) first at **264 s**; overlapping first at **334 s**.
  - The pre-flight and the loader pass it.
- **Why it matters.** Game time runs from level start, including aiming time (`MoonfallGame.GameTick`, "the bucket's and the movers' clock"), so a slow game reaches 300 s.
- **No shipped level is affected.** 2-4's LCM is 63 s; 2-3 and 2-5 each use one period.
- **Fix.** When the LCM exceeds the cap, check the whole phase torus (every pair of phases), which is conservative. Add C1 to the self-test.

### N5 (Nit), 2-2: a 0.16° dead first shot between sweep steps
- `mfcheck trace` at 33.28–33.40° touches nothing (0 hits). 33.25 hits 1 and 33.45 hits 1.
- The 0.25° sweep steps over it. Every other level is clean at 0.05°.
- **Fix.** Put a peg in the lane (`mfl.py dead` after a 0.05° sweep). In the gate, refine round any aim whose hit count is 1, so that narrower lanes cannot hide.

### N6 (Nit), the bucket lane's edge is a hard line
- Probe L1: a full-width row of 18 pegs at y 509 (bottom 519) passes the pre-flight. It sticks 30/681 (4.4%, just under the 5% gate).
- No level has a full row there: 2-4's y 500 row is in three runs, and 2-1's swell is at 486–498.
- **Fix.** Count covered width from about y 500, or fault any row covering over 70% of the board's width below y 480.

### N7 (Nit), self-test and tool gaps
- **Chain rule untested.** The deck self-tests now all trip the lone-brick rule ("brick 0 … 20 at most alone"). The chain rule (bricks under 20 units chained over 30) has no case only it catches; it does work (my four 15-unit bricks fault "bricks [0, 1, 2, 3] … 60 units").
- **Stage check.** No case for the "much easier than its band" branch.
- **Old ghost gate.** It still passes vacuously on 2-3 ("None over 0"). The print gate now carries the "what stays" duty, so either make it non-vacuous or document it as an in-play veil check only.
- **Sweep row format.** `mfcheck` prints sweep rows to one decimal (`{a,6:0.0}`). At the 0.25° step, `mfl.py stuck` and `dead` re-trace a rounded angle (72.25 → "72.3"), 0.05° off, wider than N5's lane. Print two decimals.

### N8 (Nit), ramp order and jewels
- **Ramp order.** Each step is gated at 0.5 per 48 against an SE of about 1.1 on a difference. 2-1 → 2-2 (0.8) and 1-2 → 1-3 (1.2) are not separated statistically. The order holds on point estimates, which is the rule as written.
- **Jewels.** 9 of 10 first jewels lie in 247–296°. Neighbours 1-3/1-4 (296/357 against 262/349) pass F7 by 4°.

## Unverified

- **The runtime's print.** No converter exists. All print and F9 figures are on the pipeline's own render. N2 says why they will not carry over as the format stands.
- **In-game play.** All play evidence is mfcheck's greedy player at seed-derived deals, plus first-shot sweeps. I did not log where the stuck rule fires during play (GD G7). Stuck-rule fires per 864 games: 1-1 44, 2-3 41, 1-2 32.
- **Real game length.** The 334 s collision in N4 assumes a slow player; I did not measure real game durations.
- **Art reads.** L12 is an art-supervisor call. I judged the composites at 1x on this display only.
