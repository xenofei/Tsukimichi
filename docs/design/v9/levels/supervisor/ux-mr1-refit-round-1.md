VERDICT: REVISE (0 blockers, 1 major, 4 minors)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| M1 | Major (shared with the game designer) | 1-3 base-03 | The stuck rule fires 154 times in 1,728 games, above the shipped maximum of 88. Players will often see a ball come to rest on the cactuar's shoulder bricks before the rule clears pieces. This is the stage's most frequent "is it broken?" moment. | `precheck`: play FAIL (WARN 70, FAIL 100), report `stuck_fires 154`. `mfl.py stuck base-03`: 17 first shots stick, at (655-695, 280-370) on the right arm and shoulder bricks and at (160-250, 360-480) on the left ledge bricks. |
| m1 | Minor | 1-5 base-05 | Protan orange separation is under the MR 5 guard (p10 0.105), and the thin spot is the crystal's core column. | Pipeline: p10 0.104, minimum 0.090 at brick (440,329). On candidate places in the engine at 640, the minimum is 0.102 at peg (440,244), and the column bricks (440,305)/(440,351) read 0.105/0.106. In the protan and deutan crops the orange still reads as gold on grey. MR 6 round 1 m1 (0.103) set the precedent for Minor. |
| m2 | Minor | 1-2 base-02 | Peg (690,70) hangs directly under the Ace chip, 3 px below it at 640 and about 6 px at 1280. The chip sits over the board's top-right corner, so this peg's hit pop and the rebounds there run against HUD text. Before the refit, none of the ten levels had a peg under the chip. | Zoom of `base-02-engine-640.png` (470-520, 45-85). The 12-20 unit ring around (690,70) and (585,55) picks up the chip and plate text. Old base-01..10: no peg with x>600, y<95. New: 1-2 has (690,70) and (640,90). |
| m3 | Minor | 1-4 base-04, 1-1 base-01 | In the engine at 640, the worst hypothetical F6 (a purple, since any unlit blue can turn purple) sits just under the 0.20 floor: on 1-4's pale aqueduct stone, and on 1-1's pale Waking Sands map block. | Face L minus ring p90 (12-20 units out, other pieces masked), 1280/640. 1-4: peg (168,340) 0.211/**0.189**, brick (208,348) 0.218/**0.192**; blue there 0.274/0.241. 1-1: peg (150,220) 0.223/**0.196**. The pipeline passes both (worst 0.264 and 0.326 at 0.8x), but 640 is a 0.60 scale. In the crops the pieces read, but grey-blue on grey-blue. |
| m4 | Minor | 1-4 base-04 | The dome's crest is 2.5 units outside the launcher's legal clearance. The aim guide is 4-5 units long across the whole ±12° cone, and at 640 the ball looks as if it rests on the arc. 1-4 is the only level in the stage with no visible guide on central aims. | Brick (418,183) is 87.5 from the pivot (400,87); `LauncherClearance` is 85. Free run along the guide at -12/-6/0/+6/+12°: 5.0/4.5/4.5/4.5/4.0 units. The other levels give 29-270. Before the refit the same dome sat at 91.5 (brick t 10 then, 20 now). |
| n1 | Nit | 1-1, 1-2, 1-4 | The pipeline's F9 brick minima under 0.10 do not show up in the engine. | Pipeline: 0.075 at (628,178), 0.078 at (504,478), 0.083 at (684,392). Engine, hypothetical orange at those places: 0.144/0.136, 0.130/0.123, 0.143/0.138 (1280/640). Protan crops show clear yellow against blue. This looks like a brick-segment sampling artefact in readcheck. Worth a look, but no level change. |
| n2 | Nit | 1-1 base-01 | The pegs now trace the road the glint runs along, so `GlintClear` hides the glint on 57% of its 682 units. It shows fully on only 11%, with about 14 fade on/off changes per pass, so the glint stutters. | `thanalan-road-chart.json` glints against the piece clearance (PegKeepOut 2.5 + GlintReach 3.96, dim 10). Not seen in motion. Optional: drop the glint, since the pegs now carry the road. |
| n3 | Nit | stage (engine palette, carried from round 1 n3) | Without the peg marks (off by default), purple matches blue under protan and deutan. Green becomes cream against mustard orange, and on 1-5 a green arc segment touches orange segments. | CVD crops: purple at (622,426) and (284,507) looks blue. Green brick on 1-5's right arc. With bricks now 20 thick (12 px at 640, above `MinBrickMarkPixels` 11), runtime round 2 m1 should improve. Marks were not rendered in the pack, so this is unverified. |
| n4 | Nit | pack | `index.md` shows convert "stale" for 1-2 to 1-5, but `status` now says fresh. The brief mentions a trace sheet that the pack does not contain. | One transform (1280: s 1.27, +132, +20; 640: s 0.6025, +79, +25.5) fits the committed runtime piece files for all five levels (mean L 0.77 at peg centres), so the piece layouts are current. I could not confirm 1-4's veil 0.34 → 0.42 in the engine render from the pack alone. |

## Per-level verdicts

Table columns:
- **F6:** my hypothetical worst kind at any place, 1280/640.
- **Protan dealt:** the seed-1 oranges in the engine, p10/min (1280; 640 within 0.01).
- **Launcher:** the nearest piece edge to the ball and the free guide run on the axis.

| Level | F6 worst (kind, place) | Protan dealt | Pipeline F9 p10/min | Launcher | Verdict |
|---|---|---|---|---|---|
| 1-1 Road to Horizon | purple 0.223/0.196 (150,220) | 0.141/0.136 | 0.119/0.075 | 62 / 270 | APPROVE (m3, n1, n2) |
| 1-2 Horizon by Night | purple brick 0.331/0.304 (Ace-chip pegs excluded) | 0.128/0.125 | 0.116/0.078 | 37 / 32 | APPROVE (m2, n1) |
| 1-3 The Cactuar | purple brick 0.255/0.233 (229,179) | 0.138/0.129 | 0.114/0.099 | 32 / 39 | REVISE (M1) |
| 1-4 The Gilded Dome | purple 0.211/0.189 (168,340) | 0.126/0.117 | 0.111/0.083 | **9 / 4.5** | APPROVE (m3, m4, n1) |
| 1-5 The Crystal's Call | purple brick 0.309/0.282 | 0.116/0.105 | **0.104**/0.090 | 32 / 31 | APPROVE (m1, n3) |

Notes that apply to every level:
- **Size and density:** pegs at r10 are 25 px at 1280 and 12 px at 640, and bricks are 25 and 12 px thick. Every kind reads at both window sizes. The closest gap between a peg and any other piece is 6 units or more on every board, so nothing merges at 640. The densest 90-unit windows (42-45%) are the A of 1-2, the cactuar's right shoulder, the aqueduct of 1-4 and the crystal's top. They read as clusters, not as smears.
- **Kinds:** dealt-face F6 in the engine is 0.29 or more for every dealt kind at both sizes. The lowest is a blue brick on 1-1 at (250,398) at 640, at 0.288.
- **Launcher and guide:** nothing sits in the swing (`in_swing_px` 0 everywhere). Axis precheck passes on all five, so no scene light falls on the guide column. The 1-3 green brick (408,221) borders the guide column, but it is a piece, not dress, and it reads as a target.
- **Motion:** the engine keeps glints, stars and dust 2.5-8 units from pieces, so a denser board means less motion near pegs rather than more. 1-4's flickering lights are 103 units or more from any piece, and 1-3's are 59.6. The only motion change worth a note is 1-1's chopped glint (n2).

**1-1 base-01, Road to Horizon: APPROVE.**
- The navy map's saturated blue coast halos sit under pale blue pegs. Lightness separates them (blue peg F6 0.247 at 640 at the worst place), but the town-label lettering on the pale Waking Sands block (top left) is the stage's busiest ground under pegs (m3).
- Orange under protan becomes yellow on grey-blue, and every candidate is 0.13 or more in the engine.
- 1-1 has no greens, which matches the charter (no greens on 1-1 and 1-2).

**1-2 base-02, Horizon by Night: APPROVE.**
- This is the cleanest ground in the stage: night sky, a city silhouette and green water. Every kind's F6 is 0.30 or more at 640 away from the HUD corner.
- The only issue is the two new top-right pegs under the Ace chip (m2). Moving (690,70) down about 15 units, or left of x 640, clears it.

**1-3 base-03, The Cactuar: REVISE.**
- Readability is good: the face pegs sit in dark sockets, and the green brick on the head and the green peg on the hill are clear.
- Protan on the face pegs is 0.099 in the pipeline and 0.105 in the engine at 640. This has been known since round 6 and is unchanged.
- The blocker to approval is M1. Every resting spot is on the arm and shoulder bricks, so the fix lies in the layout, not the dress.

**1-4 base-04, The Gilded Dome: APPROVE with minors.**
- This is the busiest painting: a grey-blue architectural line drawing whose highlights are close to the blue peg colour. The worst place is the aqueduct leg the integration commit veiled at 0.42, and it is still the stage's thinnest at 640 (m3). The fix would be about ×0.92 more veil local to (150-220, 320-360), then a recheck of F9.
- m4 is the main UX cost. Lowering the dome 10-15 units would give a 2-3 dot guide without changing the subject.
- The purple at (284,507) sits on dark ground at 0.505 and reads well.

**1-5 base-05, The Crystal's Call: APPROVE with m1.**
- The crystal outline lines meet the peg edges without reading as extra pieces.
- The protan thin spot is orange on the teal crystal core. If the column stays orange-able, a bluer or lower-chroma core of the kind used on 6-5 would lift p10 over 0.105.
- The green arc segment next to orange segments is an engine-palette matter (n3).

## Method and what is unverified
- I worked read-only and edited nothing in the worktree. I ran `mfl.py status --stage mr1`, `precheck` (report only; its crops go to the gitignored `build/`) and `stuck base-03`. All other figures come from the pack's PNGs and the committed runtime JSONs, measured by scripts in `C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/ux/`:
    - F6: Oklab L, face median against ring p90.
    - Protan and deutan: Machado severity 1, a/b distance of the core against the ring.
    - Hypothetical faces: the engine's median dealt face per kind.
- **Unverified:**
    - motion over time: I made no frame sequence, so n2 is computed, not seen;
    - the colour-blind marks;
    - Reduce motion;
    - whether 1-4's veil change is in its engine render;
    - tritan vision.
- My engine F6 at 640 runs about 0.03 under the pipeline's 0.8x figures because of the downscale, so treat m3 as an estimate near the floor.
