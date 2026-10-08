VERDICT: APPROVE (0 blockers, 0 majors, 0 minors)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| M1 | Fixed | 4-4 base-19 | Protan orange on verdigris is fixed. The green has moved to the city ring's outer face, and the stone under the pieces is lapis. | Pipeline p10/min 0.101/0.083 → **0.107/0.105**. `bricks_min_margin` 0.248 → 0.266. My engine measure: 0.108/0.105 at 1280 and 0.108/0.106 at 640. The worst brick is now (575,194) at 0.107. (501,429) is no longer orange-able (its spoke's orange is now (474,403)). (195,193) measures ≥ 0.108. |
| m1 | Fixed | 4-5 base-20 | The low "Thal ring" arc is gone, and the Exchange crown is blue only. | Pipeline min 0.090 → 0.103 (the NW crown arc). Engine: that arc is 0.106/0.108 at 1280/640. See n4 for the new lowest place. |
| m2 | Fixed | 4-1 base-16 | The trail now reads as faint long dashes at 640, not as guide dots. It stops clear of the guide. | 10-on/6-off dashes, alpha 0.45–0.5, width 2. The Gate route stops at (468,185), 68 units from the guide column. In the 640 crop, the dashes are dim strokes; the guide dots are crisp white. |
| m3 | Fixed | 4-3 base-18 (pack) | `--moment cleared` now keeps an orange outside Lunar Burst's reach of every green. It fails with an error instead of showing the tally. | `Program.cs` (uncommitted). The pack's `base-18-cleared-1280.png` is now the real cleared board: 1 orange left, score 118,850, no tally. |
| n1 | Nit (carried) | 4-1 base-16 | The map's two dark arrowheads still read as UI pointers. | (500,200) and (1010,480) on the 1280 render; clearest on the cleared board. This is the art reviewer's or the owner's call, as in round 1. |
| n2 | Nit (partly fixed) | 4-2 base-17 | Oranges no longer use the spire's lit flank. A purple dealt there is still the stage's lowest F6. | Orange F6 worst 0.220 → 0.304 at (271,136) in the pipeline; 0.329 in the engine at both sizes. A purple at (314,347) stays at 0.223 (pipeline floor 0.20; the shipped lowest is 0.219). If an orange were dealt there, the engine would measure 0.302/0.295. No action needed. |
| n4 | Nit (new) | 4-5 base-20 | The new east-rampart orange-able brick, line (671,394–422), has no protan headroom under my measure. | Engine 0.0996 at 1280 and 0.1014 at 640. The pipeline does not rank it worst (its min is 0.103 elsewhere). My method reads about 0.003 lower than the pipeline; stage 3's floor under it is 0.106. In the crop it stands out clearly between pale-blue segments on muted purple. A local tone of about 0.95 under it would add margin. Optional. |
| n5 | Nit (pack) | stage | Two pack texts are out of date. | `index.md` still says "before round 1" and describes `base-18-cleared-1280.png` as the `--scene-only 2` bare scene; it is now a real cleared render. `stage-mr4.txt` is the pre-round-1 run (e.g. base-16 pooled 26.5, base-17 jewels 232/159, base-20 held out 18.3); `index.md` gives the current figures. |
| n6 | Nit (process) | stage | `docs/design/v9/levels/supervisor/response-mr4-round-1.md` does not exist. The stage-level edits are uncommitted in the worktree. | `git status`: orange-able swaps in 4-1 (563,302)↔(465,196), 4-2 (689,222)↔(413,210) and 4-3 (649,297)↔(578,206), the render-tool fix, the charter's 4-1 source row, and regenerated pack files for 4-1 to 4-3. I judged the tree as I found it. The core DLL was built at 15:11:43, after the level JSONs (15:11:31), so my renders match these edits. Each new candidate is clear: protan 0.105/0.134/0.114 and F6 0.388/0.464/0.360 (1280; 640 within 0.007). |

## Method
- **Renders:** 5 levels × {1280, 640} `--moment start`, all built from the current tree. Motion frames at 0/3/7/11/15/19/23/27 s for all five. 4-4 also at 1 s steps to 30 s, then 33–49 s, plus 16.3, 16.5 and 16.6 s, to follow its 50 s glints. Reduce motion at 6 and 7 s for all five. All went to my scratch folder with `--cache`. The worktree was not touched.
- **Measures:** the same as round 1 (`em.py`):
  - **Mapping:** s 1.2667, offset (133.3, 31.7) at 1280; s 0.6017, offset (79.0, 30.9) at 640.
  - **F6:** face p80 against ring p90, in luma.
  - **F9:** Machado protan, core against ground. Peg ground is 12–20 units out; brick ground is a band 3–11 units out.
  - **Calibration:** stage 3 under this method gives protan min 0.106–0.118 and F6 worst 0.270–0.346.
  - **New candidates:** I placed a dealt orange's core colour on each candidate's own ground.
- **Motion:** each peg's ring p90 lift across frames, and moving pixels in the guide corridor (x 600–680, y 240–740 at 1280).

## Per-level verdicts

| Level | F6 pipeline worst | F6 engine 1280 / 640 | F9 pipeline p10 / min | F9 engine p10 / min (1280; 640) | Motion near pieces | Verdict |
|---|---|---|---|---|---|---|
| 4-1 base-16 | 0.318 purple (559,400) | 0.385 / 0.371 | 0.109 / 0.103 (563,302) | 0.112 / 0.105; 0.111 / 0.106 | Ring lift ≤ 0.013. Nearest change 19 units from a peg edge. Still under Reduce motion. | APPROVE (n1) |
| 4-2 base-17 | 0.223 purple (314,347) | 0.329 / 0.329 | 0.121 / 0.120 | 0.122 / 0.122; 0.123 / 0.122 | Lift ≤ 0.018. Nearest change 21 units. Still under Reduce motion. | APPROVE (n2) |
| 4-3 base-18 | 0.262 purple (330,226) | 0.361 / 0.342 | 0.108 / 0.106 | 0.111 / 0.108; 0.111 / 0.108 | Lift ≤ 0.017. Halo flicker range 0.026–0.041. Still under Reduce motion. | APPROVE |
| 4-4 base-19 | 0.304 (442,337) | 0.362 / 0.367 | **0.107 / 0.105** | 0.108 / 0.105; 0.108 / 0.106 | Lift ≤ 0.015. The glint passes behind the launcher head (see below). Still under Reduce motion. | APPROVE (M1 fixed) |
| 4-5 base-20 | 0.270 purple (322,250) | 0.325 / 0.355 (purple) | 0.113 / 0.103 | 0.116 / 0.0996; 0.118 / 0.101 (brick) | Lift ≤ 0.015. Nearest change 46 units. No runtime stars. Still under Reduce motion. | APPROVE (n4) |

Under Reduce motion, the only moving pixels on every level are the bucket's (y 705–783). Nothing in the scene moves in the guide corridor on any level; the only corridor change is the bucket. A sheen sweeps the title plaque every 12 s on all five levels. It is frame chrome shared by every level, not level art.

### 4-1 The Steps of Thal: APPROVE
- **m2:** fixed (see table).
- **Glints:** they still slip under the pegs without lifting the rings.
- **New Gate of Thal candidate (563,302):** it sits on paved stone. If dealt orange it measures protan 0.105 and F6 0.388/0.385. The pipeline's new protan worst is this place, at 0.103, still above 0.10.
- **Trail by the guide:** at 640 the Court's stair dashes run 80+ px east of the guide column and read as map lines.
- **n1:** carried.

### 4-2 The Spire of the Sands: APPROVE
- **The red airship hull:** it now sits beside the launcher, and the guide starts at its tip. White dots read well on dark red and on teal, at start and on the cleared board.
- **Keel oranges:** they sit just under the hull. In a protan simulation the dark red hull turns dark olive-brown and the oranges stay brighter. Keel oranges measure ≥ 0.122 protan and ≥ 0.349 F6.
- **The sand band (#B88E5C) at the bottom:** only blues and the bottom arc sit on it. Blue on warm sand is the strongest pair on the board for every vision type. No orange-able piece sits at y ≥ 430.
- **Lamps and beams:** the 8 flickering lamps stay inside the spire, ≥ 21 units from any peg edge, and the beams lift no ring.

### 4-3 Lamps on the Steps: APPROVE
- **Bare flames:** the lamp cores peak at L 0.95–0.97, above the guide's 0.86, so I checked them against the guide.
  - At 640 each core shows as a soft lantern glow with a halo (22–32 px over L 0.6), not as a crisp white dot.
  - Each sits inside its flat-topped ring. A guide from above meets the ring's top pegs before it reaches the core.
  - The nearest lamp to the guide column is 85–90 px away at 1280.
  - Halo flicker is small (range 0.03–0.04), and ring lift is ≤ 0.017.
- **The moon** stays at (128,58), off the launcher's axis.
- **The red gateway** still holds no pieces.
- **m3:** fixed (see table). The pack text is n5.

### 4-4 The Coliseum: APPROVE
- **M1:** fixed. Under the upper arcs the stone is now lapis, and the three orange crescents read on it at 640.
- **Lowest protan place:** an outer-ring peg on the green haze, (153,357), at 0.105–0.106. That meets my round-1 target (min ≥ 0.10, p10 ≥ 0.105).
- **The new city-ring glint:** a point of peak L 0.79, about 40 px long. It travels the dashed ring across the top between 14 and 18 s, passes behind the launcher head at about 16.5 s, and is mostly hidden by the head and the aim arc. It never enters the guide corridor below the ball. The stands' glint runs at 26–28 s, low right, away from the guide.
- **Unchanged from round 1:** the guide over lapis, and the grey dashed rings beside it.

### 4-5 Ul'dah from the Sands: APPROVE
- **m1:** fixed.
- **n3:** moot, since runtime stars are off. No bright moving pixel (L > 0.6) appears anywhere on the board in 8 frames.
- **Pegs on the warmed sands:** oranges on the magenta road separate well in protan (magenta turns blue). The lone green on the sand (279,395) is clear.
- **The new brick crowns and ramparts:** they read at 640.
- **The guide over the city's teal stone:** it reads on the cleared board.
- **The east rampart's orange segment:** that is n4, optional.
