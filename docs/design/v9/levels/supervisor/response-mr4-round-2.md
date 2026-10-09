# Response: MR 4 The Sunlit Steps, round 2

The fold-in for the round 2 reviews (`game-designer-mr4-round-2.md`, `level-critic-mr4-round-2.md`; UX approved in
`ux-mr4-round-2.md`, and its fixes stay in). Every Major and Minor is answered below, then the Nits. There was no
`response-mr4-round-1.md`; the round-1 status table at the end stands in for it. No gate's rule or threshold changed.
The pack in `docs/design/v9/levels/review/mr4/` is regenerated: every render, the reports, the trace sheets,
`stage-mr4.txt` and `index.md`.

## Where the stage stands

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Converter 2x max / p99.9, 1x p99 |
|---|---|---|---|---|---|---|---|---|
| 4-1 | base-16 | The Steps of Thal | 26.0 | 26.68 | 270 / 338 | 0.318 | 0.109 / 0.103 | 0.019 / 0.008, 0.021 |
| 4-2 | base-17 | The Spire of the Sands | 25.6 | 25.30 | 233 / 63 | 0.224 | 0.121 / 0.120 | 0.023 / 0.008, 0.008 |
| 4-3 | base-18 | Lamps on the Steps | 23.7 | 23.55 | 282 / 1 | 0.262 | 0.108 / 0.106 | 0.009 / 0.007, 0.006 |
| 4-4 | base-19 | The Coliseum | 22.1 | 21.95 | 261 / 179 | 0.304 | 0.107 / 0.105 | 0.021 / 0.008, 0.028 |
| 4-5 | base-20 | Ul'dah from the Sands | 18.1 | 17.88 | 321 / 233 | 0.270 | 0.113 / 0.103 | 0.010 / 0.007, 0.012 |

- `mfl.py stage mr4`: PASS. Pooled steps 1.38, 1.75 and 1.60 (± 0.41); finale gap 4.07 ± 0.40. The mean is 23.07,
  a fall of 0.59 from MR 3's 23.66.
- `mfl.py convert base-16 base-17 base-18 base-19 base-20 --check`: PASS, 10 of 10.
- `mfl.py selftest`: every checker passes its known-bad and known-good cases.
- `pwsh tools/gate.ps1`: build, load, test (9,070 passed, 0 failed) and themes all pass.
- Aces, re-suggested over 576 games: 4-2 260,000 (was 250,000; 302 wins). 4-1 240,000, 4-3 260,000, 4-4 240,000 and
  4-5 220,000 are unchanged. The Perf test `Shipped_Aces_are_unchanged_by_the_hard_level_rule` passes 20 of 20.

## Level critic

- **M4 (4-2 → 4-3 step collapsed): fixed.** I dropped the uncommitted 4-2 swap, so (413, 210) is a candidate again
  and (689, 222) is not. I kept the 4-1 and 4-3 swaps, as you recommended. 4-2's round-2 art changes (m3, below) move
  it a little: held out 25.6, pooled 25.3. The pooled 4-2 → 4-3 step is 1.75 ± 0.41, which clears 0.5 after one
  standard error. I did not re-measure it on a new fresh block (100001+); the stage's 5,184 fresh seeds were replayed
  for every changed level.
- **M5 (fold-in unverifiable): fixed.** Everything is committed. `stage-mr4.txt` is this round's run, and `index.md`
  carries its figures. The render tool's `--moment cleared` fix is committed, and the 4-1 charter row too. The Aces are
  re-suggested on the final layouts (above). This response records the pass.
- **m3 (4-2 right half rows): fixed.** See the game designer's m1.
- **m6 (4-5 notch at (373, 371)): fixed.** The inner face's middle moon (356.7, 378.2) now stands 8 units above the
  line between its neighbours, at (360.5, 368.7), 16.3 clear of each. A ball rolls off its crown. Lifting it straight up
  by 4.5, the first try, left a 13.5 gap to its west neighbour: the sweep fell to 9, but play stuck rose from 51 to 77
  per 1,728. With the final placement the sweep sticks on 6 of 681 (was 13), and play sticks 64 per 1,728. That is
  above round 1's 51 but under the shipped maximum of 88. `mfl.py stuck base-20` shows no rest point near (373, 371).
- **n2 (4-3 dome and minarets in our painting): the owner's call.** Unchanged.
- **n6 (index says 4-3's cleared render is the bare scene): fixed.** `index.md` now describes it as a real cleared
  board, as it is.
- **n7 (4-5 "from the Sands" rests on little art): addressed** under the game designer's M1.

## Game designer

- **M1 (4-5's sands do not read): fixed, as you prescribed.**
  - **The sands.** Below y 430 the jewel turns from the night's plum to warm umber. Its hue at the foot is now about
    35 degrees (composite pixels at (400, 540) and (470, 560): hue 38). Your pixel was 320.
  - **The transition.** The turn runs over y 430-525, which makes a narrow rose band where plum meets sand. A sharper
    turn failed the print check (median 0.0061 against 0.006); this one measures 0.0048.
  - **The dunes.** They are warm dark sand (#24150D, inner #5A3A22), not near-black. Their moonlit crest is twice as
    strong (rim 0.7, was 0.35).
  - **The east third.** The Thal ghost's curved street and inner arc are lifted 1.5, up from 1.35, and its east street
    stays at 1.35. At 1.8, F6 fell to 0.169 on the moons that trace the arc, and (with the first, higher umber grade) the brick on
    the east street fell to protan 0.069. 1.5 passes with F6 at 0.27. Tinting the streets the city's sea-blue failed the print check
    (median 0.0082), since those regions sit under the pegs.
  - **What the renders show.** In `base-20-engine-640.png` both arcs show as pale curved streets under their moons. In
    `cleared-1280` the ghost is still the map's own Steps of Thal. If it still reads as a watermark at 640, the other
    road is to drop the inner arc. That arc holds the top holdout (540, 373), which is in 30% of lost games, so dropping
    it would ease the finale. I have not done it.
- **m1 (4-2 east third still rows): fixed, by your first prescription.** The lens's edges now open as they reach the
  wall: the lit top rises to (700, 248) and the underside falls to (700, 404). The lens reads as a tongue from the
  spire's flank that widens east, not as two parallel rows. The horizon is a low dune crest, rising to y 434 at x 626
  and falling to y 460 at both ends, not a ruled row at 446-450. Its pegs follow it.
- **m2 (4-2 sand box): fixed.**
  - The sand region rises softly from the painted horizon (y 415 → 472). Before, it was a 55-unit band from y 430 that
    started on a line through the far dome.
  - The whole dome (y 472-572) is now sand.
  - The region runs to the wall: it fades over x 700-740 (was 665-725), so the teal strip at the right wall is gone.
  - F7 holds: the second jewel's worst wall window is 1.04, against a limit of 5. The print median is 0.0047.
- **m3 (4-1 blurred patches): fixed, with the cloned hatch tile you suggested, done at runtime.**
  - **The new part.** The runtime gains a source part, `clone`: `[x, y, w, h, dx, dy]` boxes filled with the painting's
    own pixels (dx, dy) away, with the erase's 3-pixel feather. It is read from the install at load, and nothing of the
    game is shipped. It is in `MoonfallSceneBuilder.Clone`, the loader, `scene-recipe.md`, the pipeline's
    `scene._clone` and the converter.
  - **The three arrows.** The three arrows inside the opening now use it. Their offsets were found by matching each
    box's surrounding ring against the map, and checked by eye.
  - **Checks.** The hatching and parchment now run through all three places in `base-16-cleared-1280.png`. The
    converter gate holds 4-1 at 0.019 / 0.008 at 2x, the same as before. There are two new unit tests: a clone keeps a
    period-4 stripe in phase, and a box whose source falls outside is skipped. The shipped recipe reads its three boxes.
- **m4 (ramp unverifiable, stale pack): fixed.** See the stage table above. The finale step is 4.07 ± 0.40 pooled.
  That is a little over the charter's 3.5-4 but within one standard error of 4, and the critic accepts it (rule:
  gap ≥ 2.5). You asked for "near 3.5 to 4". If 4.07 counts as too wide, 4-5 can give one candidate to a blue.
- **n1 (4-1 one brick): not changed.** The opener is the stage's calm board by design: bricks run 1, 4, 2, 31, 15.
  It sits at 26.68 pooled against the charter's 26.5 and steps 1.38 to 4-2. A brick run on Pearl Lane's hall would
  move both. I would rather leave the opener as you passed it.
- **n2 (four techniques; 4-1 and 4-5 roads): not changed.** The charter allows four. 4-5's roads run out of the gates
  into the sands, so they now end in the umber field rather than on the plum.
- **n3 (4-5 sand stars): fixed.** The west star is never green, so it is never dealt green alone in the open. The east
  star stays green-able.
- **n4 (no round-1 response): fixed.** This file records both rounds.

## UX (approved; kept)

- **m1-m3** stay fixed. The 4-5 crown protan is 0.103 at minimum, and the 4-1 trail dashes are unchanged.
  `--moment cleared` is committed, and the pack's `cleared` renders use it.
- **n4 (4-5 east-rampart brick headroom): not changed.** A local tone under one brick is a peg-scale positional tone
  over a piece, which `dress.lint` refuses. The pipeline's protan for 4-5 is p10 0.113, min 0.103, unchanged by the
  sands, which start below the rampart.
- **n1** (the map's two kept arrowheads) is carried as the owner's call; **n2** needed no action. **n5 and n6** are
  fixed (pack text, commits).

## Round 1, status

| Reviewer | Finding | Status |
|---|---|---|
| GD | M1 4-5 not "from the Sands" | Fixed: structure in round 1, sands in round 2 (M1 above) |
| GD | M2 4-2 murky, rows | Fixed: round 1, plus round 2's m1 and m2 |
| GD | m1-m2 4-3 lamps, stuck | Fixed in round 1 |
| GD | m3 4-1 and 4-5 alike | Fixed in round 1; residual noted in n2 |
| GD | m4 4-1 blurred patches | Fixed in round 2 (clone boxes) |
| GD | n1 4-1 one brick | Kept by design (n1 above) |
| GD | n2-n5 | Fixed in round 1 |
| Critic | M1 4-3 stuck; M2 4-5 subject; M3 4-5 rim stars | Fixed in round 1 (confirmed in round 2) |
| Critic | m1, m2, m4, m5 | Fixed in round 1 (confirmed in round 2) |
| Critic | n1, n3-n5 | Fixed in round 1 |
| UX | M1 4-4 protan; m1-m3 | Fixed (approved in round 2) |
