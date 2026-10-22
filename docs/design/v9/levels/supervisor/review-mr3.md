VERDICT: REVISE (0 blockers, 3 majors, 9 minors)

| id | sev | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | base-11 | F9 protan worse than round 1; the worst orange of any level for a protanope | p10 0.102 (<0.105), min 0.053 at brick (420,510); round 1 had 0.105/0.103; precheck FAIL |
| M2 | Major | base-13 | F9 protan worse than round 1 (same case as MR 2's M3) | p10 0.102, min 0.084 at brick (132,164); round 1 had 0.112/0.111 |
| M3 | Major | base-15 | F9 protan worse than round 1, on the finale | p10 0.103, min 0.076 at brick (168,410); round 1 had 0.112/0.109 |
| m1 | Minor | base-15 | Stuck rule fires 92 per 1728 games (shipped max 88); balls rest in an upper-left pocket | `stuck`: rests at (170,185), (165,230), (175,225); densest window 42% at (150,221) |
| m2 | Minor | base-14 | Least lavish board: rows of moons over a plain painting; the beacons barely show (gone at 640) | engine-1280/640; brick share 13%; 119 pieces |
| m3 | Minor | base-14 | Wall-side holdout | (104,182) is the last orange in 20% of lost games |
| m4 | Minor | base-11 | About 12 arc bricks repeat as a pattern instead of tracing the lanes | engine-1280; densest window 37% at (660,251) |
| m5 | Minor | stage | Palettes repeat: 3-1 and 3-5 (one painting, one jewel pair), 3-2 and 3-4 (both our cloud sea with beacons) | jewels 252/179 vs 256/165, 289/212 vs 274/209; crop overlap 24% |
| m6 | Minor | base-15 | The whale is small and dark; the vortex and the chart's title read first; a smudge at 640 | engine-640; crop of engine-1280 |
| m7 | Minor | stage | Pack hygiene: index.md, stage-mr3.txt, traces and cleared renders are from 7 Oct; no current `stage mr3` run; 3-5's render shows a stale Ace | git log 87cd819f; render ACE 200,000 against aces.json 190000 |
| m8 | Minor | stage | convert shows `stale` on 3-2 to 3-5 (commit order only) | recipes and painters unchanged since conversion |
| m9 | Minor | base-13 | ch-06 "clear every peg" now sits on a 112-piece board; not measured | charter 1.6 says "moderate count" |
| n1 | Nit | base-12 | Beacon glow behind the aim guide | axis WARN, 12 u^2 at L>=0.8 |
| n2 | Nit | base-12 | Easiest level (5.8/48); the curve is flat before the finale | margin -0.6 |
| n3 | Nit | base-15 | Wins 2/48 on the rule seeds, exactly the floor | play greedy_won 2 |
| n4 | Nit | base-13 | Pieces crowd at the spire top | densest window 41% at (555,116) |

## Stage (3-1 to 3-5)
- **Difficulty.** Held-out wins per 48 are 4.6, 5.8, 4.6, 4.4 and 3.9. The mean of 4.66 sits in the band (3.04-5.24); Peggle's MR 3 mean is 4.14. The finale's 3.9 is under the mean of the other four (4.85). On the held-out games the gap is 0.96 with a standard error of about 0.36, so the finale rule passes. The pooled check over 5,184 fresh games is not in the pack, because stage-mr3.txt is the 7 Oct run (m7). Re-run `stage mr3` after the Major fixes.
- **Shape of the stage.** The levels sit within about 1 win per 48 of each other, and 3-2 is the easiest. That is allowed under the 9 Oct rules.
- **Playability.** No Blockers: all five levels PASS. No sweep shot misses everything. Reach is complete. Stuck aims are at most 4 of 777. Every holdout is under 25% of lost games.
- **Theme.** The set holds together as "what flies over Eorzea by night". Five techniques (TR, LS, TE, DR, CR) run with no repeat, 3 of the 5 paintings come from the game, and the texture reuse rule holds (24% against 25%). Variety is the weakness (m5): two dark blue-green chart boards and two purple cloud-sea boards with beacons. 3-3 is the only board unlike the others.
- **Majors.** All three are the same regression from the refit. The boards are denser and more pieces are candidates, so orange candidates now sit on dark green land or pink cloud. The fix is cheap: drop or move the few lowest-margin candidate places, starting with each report's `protan_worst_at`, then re-check F9 and play.
  - Be careful on 3-5: it wins exactly 2/48 on the rule seeds, and the finale gap has to hold.
- **Readability.** F6 worst margins are 0.291, 0.247, 0.285, 0.232 and 0.270, all over the 0.225 guard. Every piece kind reads at 1280 and at 640 on all five levels (checked on the renders).
- **Not verifiable from stills:**
  - 3-4's slide movers near the beacon posts;
  - the beacon flicker and beams under the pegs on 3-2.
- **Process note.** `mfl.py stuck base-15` built the mfcheck tool (3 s). Nothing in the worktree changed (git status clean).

## base-11 (3-1) The Airship Road
- **Subject.** The chart reads well. Pegs follow the dashed lanes from the chart's airship down to the city shields. The two shields are ringed by six moons each (the TR ring stops). The dragon behind the launcher keeps the axis clear.
- **Spare pieces (m4).** The right half and the bottom carry about a dozen arc bricks set as cloud crowns. They read as a repeated decoration, not as a trace.
- **Protan (M1).** The worst place is the low arc at (420,510) over the dark land, the last-orange zone (holdouts at (399,483) and (522,470)).
- **Board.** 95 pieces with a 44% brick share. It plays at 4.6/48 and is a good opener.

## base-12 (3-2) Above the Cloud Sea
- **Subject.** It reads clearly and is lavish. The obelisk beacons send dotted beam lines toward the misted moon, and the pegs ride the beams. Four tall brick columns frame the lane, and arcs sit on the cloud crests.
- **Board.** Mirror-symmetric, which suits a vanishing-point scene.
- **Difficulty.** The easiest level (n2).
- **Moon.** It stays misted under the guide; the only bright point is a small beacon patch (n1).

## base-13 (3-3) The Holy See
- **Subject.** The stage's best board. The brick spire, the walls and the arch outline the cathedral. The long brick ridge runs along the mountain crest, and arcs sit on the pink billows.
- **Board.** It was eased to 4.6. The spire top is crowded (n4).
- **Challenge (m9).** It carries ch-06 at 112 pieces, against the charter's "moderate" count. Whether clearing every peg is feasible is unmeasured.
- **Protan (M2).** The worst place is the ridge's west-end brick on pink cloud.

## base-14 (3-4) The Lane by Starlight
- **Subject.** The star river's spine of bricks climbing to the upper right is a strong touch.
- **Brick crowns gone (m2).** Hardening removed the crowns on the far banks, so the far crests are now even rows of moons. With the drifting rows, the lower half reads as four wavy rows of pegs over a plain painting.
- **Beacons.** The lane's beacons, the subject's perspective, are thin posts. They barely read at 1280 and vanish at 640.
- **Board.** 119 pieces (over the 115 target), 13% bricks, and a wall-side holdout (m3). Protan is fine (0.122/0.104).
- **Risk.** If the owner marks one level Redo, this is the likeliest.

## base-15 (3-5) The Sky Whale
- **The whale.** Its ring of 14 moons reads at 1280. But the whale itself is a dark navy figure about a fifth of the board wide, and at 640 it is a smudge (m6). The eye goes first to the green vortex, the isle's banner and the chart's gothic title across the middle, which is behind pegs but legible.
- **Board.** The vortex's brick walls, the eddy ring and the flock's chevron make the finale dense and busy rather than spectacular. That is acceptable.
- **Upper-left pocket (m1).** It catches balls: 92 stuck fires.
- **Protan (M3).** The worst place is the brick at (168,410) by the whale's tail on green.
- **Ace.** aces.json has 190,000, but the pack's render still shows 200,000 (m7).

## Engine against pipeline
- **Layouts.** The engine layouts match the composites piece for piece. I spot-checked 3-5's top right and its whale ring.
- **Moments.** The composites show a mid-game moment (14 oranges); the renders show the deal (25).
- **convert `stale` (m8).** This is commit order only. The pipeline recipes and painters are unchanged since the runtime recipes were converted, and the cloudsea picture was re-shipped in 87cd819f. Run `convert --check base-12 base-13 base-14 base-15` before the test drop anyway.
- **Before the owner plays.** Regenerate the pack: a fresh `stage mr3`, the trace sheets and the index.md replaced.
