VERDICT: APPROVE (0 blockers, 0 majors, 1 minor)

| id | Severity | Level | Issue | Evidence |
|---|---|---|---|---|
| m1 (new) | Minor | 5-4 base-24 | The six new flickering lamps have cores as bright as the ball and brighter than the guide dots. Two of them sit in the main shot cone, and one is beside the guide column. This repeats round 1's 5-5 m3 pattern. | Scene `limsa-bridge-arches`: the lights are k 1.0, core 2.8, halo 14, haloK 0.32. Core L is 0.93 at t 0 and 0.962–0.984 from 3 to 19 s. On the cleared board at 1280 it is 0.967–0.982, and at 640 it is 0.91–0.98. For comparison, the ball is 0.973 and the guide dots are 0.84–0.90. The deck lamp at (441,323) is 41 units right of the guide column: 52 px at 1280 and 25 px at 640. On the cleared ±35° cone the brightest point is the (518,322) lamp at 0.982, and the other four levels peak at the bucket lamp. Each lamp is 28–36 units from the nearest peg edge, so F6 holds (engine worst 0.258/0.247), and the warm halo separates them from the crisp white dots. At 640, though, the two deck lamps that flank the blue at (481,323) read as two stray white dots beside the column. Suggested fix, as on 5-5: lower k from 1.0 to about 0.4–0.5 so the cores land at or under 0.80, then re-check F6's drop. |
| n1 (carried) | Nit | 5-3 base-23 | The buoy lantern beside the guide column is now a flickering light and a little brighter. | It is at (410,331.3), 10 units from the column. Core L is 0.79–0.81 for t ≥ 3 (0.73 in round 1), and 0.81 on the cleared board, against dots of 0.889. On the cleared board it sits beside the guide's lowest dots. It is warm, on a post, and its flicker swings under 0.01, so it still reads as a lamp at both sizes. No action needed. |
| n2 (carried) | Nit | 5-3 base-23 | Pipeline F6 is now 0.013 under the shipped lowest. | Worst 0.206: purple at 0.8x, (286,379), against a shipped lowest of 0.219 and a floor of 0.20. The cream reflections caused it, and the response says they cannot brighten further. In the engine the worst is 0.272 at 1280 and 0.270 at 640. Not a readability problem. |
| n3 (carried) | Nit | 5-2 base-22 | The map lettering beside the guide is still drawn as speckle. | Not fixed: it is the painting's own lettering. The reason is accepted. |
| n5 (new) | Nit | 5-1 base-21 | Lifting the towers to grey-white cost about 0.03 of F6. 5-1 now has the stage's lowest engine F6. | Purple at (528,300): 0.232/0.208 at 1280/640, against 0.261/0.239 in round 1. Orange at (306,326): 0.254/0.218. The pipeline worst is 0.224. Every piece is still above 0.20, and the blues on the pale tower hold (lowest 0.309 at 640). The tower plume right of the guide column peaks at 0.717 on the cleared board (0.645 in round 1), still under the dots. The towers should not get any brighter. |

## Round-1 findings: status

| R1 id | Status | Evidence (this round) |
|---|---|---|
| m1 protan guard | Closed | Pipeline p10/min: 5-2 0.115/0.103 and 5-5 0.108/0.104. Every level is now ≥ 0.108, above the 0.105 guard. Engine at 1280 (t ≥ 3) / 640: 5-2 0.114/0.107 and 0.116/0.108; 5-5 0.106/0.104 and 0.107/0.105. 5-5's weakest oranges are still the west three, (141.9,349), (156.5,430.7) and (174.9,338.3), now up 0.006–0.008. In 5-2 the worst moved off the copper to (163.4,191) at 0.108. |
| m2 5-2 route on the guide | Closed | The glint route ends at (370,288), 30 units from the column. On the cleared board the beige dashes stop about 38 px left of the dots at 1280. |
| m3 5-5 lamp cores | Closed | Cores are 0.70–0.79 from 0 to 19 s at 1280, 0.675–0.77 at 640, and 0.75–0.79 cleared, all under the dots (0.89–0.92 in these frames) and the ball. They are wider, softer discs that read as lamps. |
| n1 | Carried | See n1 above (slightly brighter, still a nit). |
| n2 | Carried | See n2 above (pipeline lower, engine fine). |
| n3 | Carried | Accepted. |
| n4 5-3 cleared lamp heads | Closed (moot) | No moon stands on a lamp now. The cleared board shows only the painted lamps, and the pipeline ghost worst is 0.035 (was 0.055). |

## Method

- **Renders:** I used the built MoonfallRender with `--cache` into my scratchpad. All five of my start renders at 1280 are pixel-identical to the pack's (AE 0).
    - Time sequences at 1280: 3, 7, 11, 15 and 19 s for 5-3, 5-4 and 5-5; 7 and 15 s for 5-1; 7 s for 5-2.
    - 640 at 7 s for all five levels.
    - Cleared at 7 s (1280) and at 0 s (640) for 5-3, 5-4 and 5-5.
    - Reduce motion at 6 and 12 s for 5-3, 5-4 and 5-5.
    - The worktree was not touched.
- **Mapping and measures:** the same as round 1. At 1280: s 1.2667, offset (133.3,31.7). At 640: s 0.6017, offset (79.0,30.9). Movers are left out of the static measures.
    - F9: Machado protan, Oklab chroma distance between the core and the ring 12–20 units out.
    - F6: the face's p80 against the ring's p90 (r+2 to r+9), in luma.
    - Lamp L: the Oklab L maximum within 3 units of the light's centre.
- **Calibration:** 5-1's engine protan 0.126/0.122 tracks the pipeline's 0.132/0.123, the same offset as in round 1.
- **Motion:** per-pixel luma range over 3–19 s, and the p95 of that range in each peg's ring.

## Per-level verdicts

**5-1 base-21, The White Towers: APPROVE (n5).**
- Readability: protan 0.125/0.121 at 1280 and 0.127/0.120 at 640, still the stage's healthiest. The sea-teal foot is dark and leaves the bottom arcs and bucket lamp clear.
- Motion: the corner beams at k 0.08 move a peg ring's luma by at most 0.020.
- The guide runs over dark navy at both sizes.

**5-2 base-22, The Strait of Merlthor: APPROVE (n3).**
- Readability: engine F6 is 0.345 and above, still the stage's best. The lifted south rose under the south ring does not weaken that ring's pegs.
- The foot haze reads as before at 640 and cleared.
- Route 1 now stops well short of the guide.

**5-3 base-23, Lights on the Water: APPROVE (n1, n2).**
- Lamps: the six flickering lights (k 0.6, core 1.6) peak at 0.80–0.83, under the dots.
    - Each sits 17 units from the nearest peg edge, and its 8-unit halo stays outside the F6 ring.
    - At 640 they read as lamp heads on posts, not peg-sized discs.
- Reflections and colour-blind margins: the cream-gold reflections leave the pegs on them clear (engine F6 ≥ 0.272 at 1280 and 0.270 at 640), and protan is 0.111/0.109 and 0.112/0.108.
- Motion: flicker, mist and stars move a peg ring's luma by at most 0.024.
- Reduce motion freezes everything except the bucket.

**5-4 base-24, The Arches of the Bridge: APPROVE (m1).**
- The bridge reads as lamplit, and the deck row is broken into spans.
- The new upper pieces on the chimneys, spire and castle wall all read: worst F6 is 0.258/0.247 (orange at (256,158) and (352,302)), and protan is 0.119/0.112 and 0.121/0.112.
- Foot: the rose fades out, and the lilac mist at alpha 0.08 starts below the lowest moons. The bottom arcs hold (pipeline brick margin 0.282).
- Motion near pieces: at most 0.024 of ring luma, and reduce motion is still.
- The only issue is m1, the lamp cores' brightness near the guide and in the cone. I recommend the one-value fix before merge, but it does not block.

**5-5 base-25, The Lighthouses of Vylbrand: APPROVE.**
- Fixes: the y 404 seam is gone (the largest row-to-row step in the west sea is under 0.003 in every frame), and the lamp cores now sit under the guide.
- Beams in motion: at k 0.08 they are faint, slow sweeps. The largest ring swing is 0.034, at (616,440) beside the Swiftperch lamp, and it never crosses a peg's face.
- Movers: they orbit clear of the 8-unit halos. Reduce motion keeps only the movers and the bucket moving.
- Margins: engine F6 is ≥ 0.300 at 1280 and ≥ 0.29 at 640. The retuned orange at (632,235) stands on the dark summit contour and does not change readability.
- The guide still crosses the pale inlet visibly at 640.

## Stage note

| Level | Protan p10 (pipeline) | Engine 1280 / 640 | Engine F6 worst 1280 / 640 |
|---|---|---|---|
| 5-1 | 0.132 | 0.125 / 0.127 | 0.232 / 0.208 |
| 5-2 | 0.115 | 0.114 / 0.116 | 0.345 / 0.330 |
| 5-3 | 0.108 | 0.111 / 0.112 | 0.272 / 0.270 |
| 5-4 | 0.118 | 0.119 / 0.121 | 0.258 / 0.247 |
| 5-5 | 0.108 | 0.106 / 0.107 | 0.300 / 0.29 |

- The 0.105 guard holds on every level. Four of five still pass F9 through the pilot fallback, and MR 6 should not go lower.
- For lamps: keep every in-board light core at or under about 0.80 L. The ball is 0.97 and the guide dots 0.84–0.90. 5-3 and 5-5 meet this; 5-4 does not (m1).
