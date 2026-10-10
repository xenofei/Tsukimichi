VERDICT: REVISE (0 blockers, 2 majors, 2 minors)

I worked read-only at HEAD `1ad32e25`, and `git status` is clean. Since MR 5, the only Core change is the recipe loader (`MaxStarMaskTerms`), so mfcheck physics is the same. The shipped `base-26..30.json` files are byte-identical to `docs/.../json`. The pack reports are byte-identical to `docs/.../report/`. The render DLL (Core built 09:34) embeds the same five level files and five scene recipes as the repo. My output is in `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\mr6r1\`.

What I ran:
- **Fresh play:** `mfcheck play` on four seed blocks no one has used (300001+, 310001+, 320001+, 330001+), 3,456 games each, so 13,824 games per level.
- **Sweeps:** a 0.25° sweep on each level, and a trace of every angle where the stuck rule fired.
- **Renders:** MoonfallRender with my own `--cache`, start and cleared at 1280, for 6-3 and 6-5. All four frames match the pack pixel for pixel (AE 0).
- **ch-03:** `mfcheck play base-27 48 27 0 35` gives 29 wins of 48, the same as the pack.

## Findings

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| M1 | Major | stage | The stage mean falls only 0.35 from MR 5, and the charter asks for 0.5 to 1.0 (charter 1.1, "Each stage is harder overall"). MR 5 round 2 m1 warned about this before MR 6 was tuned. | Pooled: MR 5 22.11 → MR 6 21.76, a fall of 0.35. Fresh: MR 5 22.08 (my MR 5 round 2 figure) → MR 6 21.86, a fall of 0.22. `index.md` compares the mean only with the charter's 21.6 and does not report the fall. To fall 0.5, MR 6 needs a mean of about 21.6 or less. |
| M2 | Major | 6-2 → 6-3 | On fresh seeds the 6-2 → 6-3 step does not clear the 0.5 rule. The pooled figure clears it by 0.01. | Fresh: 23.56 → 22.85, a step of 0.70 ± 0.29, which is 0.41 after one standard error. Pooled: 0.92 ± 0.41, which is 0.51. 6-3 is 0.85 above its pattern slot (L − 3 = 22.0). The same surplus causes M1. |
| m1 | Minor | 6-3 | 6-3 is the stickiest level in the stage, and its fresh figure is close to the shipped maximum. | 83.8 stuck-rule fires per 1,728 games on fresh seeds (pack: 73), against the shipped maximum of 88 (1-1). The sweep sticks on 8 of 681 angles. The traced stuck flights linger inside the city rings: compass (120,150), Gridania (456,270), Mor Dhona (360,355), Ala Mhigo (550,275), Limsa (120,415) and Ishgard. Ring gaps are 14.3-17.0 px and the ball is 12 px across, so the ball enters a ring and rattles inside it. |
| m2 | Minor | stage (pipeline) | `stagecheck` and `index.md` do not check the stage-to-stage mean fall. That is how M1 reached review. | `stage-mr6.txt` prints the steps and the verdict PASS but nothing against MR 5. `index.md`: "The mean is 21.76, against the charter's 21.6". |
| n1 | Nit | 6-2 | A shot at exactly 0° bounces straight up and down on the ray moon under the muzzle for 17 s, and the stuck rule fires 4 times. | Ray moon (400,290) and hub (400,338) sit exactly on x 400. The trace stays at x = 400.00 for 1,300 ticks. ±0.005° already escapes, and the mouse sets the aim (`AimAt`), so only one exact pixel column can fire it. This is the pack's unexplained `long_flights: 1`. A nudge of 1 unit in x on (400,290) removes it. |
| n2 | Nit | 6-1 | The top holdout is just over the 25% line on fresh seeds. | (250,415) is left in 25.4% of lost games (pack: 23%). It sits above y 430, so the cheap-difficulty gate does not apply. |
| n3 | Nit | 6-5 | The new beams cannot be seen in the composite, the start frame or the cleared frame. | k 0.03 beams. I can see no shafts above the gate in my re-render of cleared-1280. A person must watch them in motion. |
| n4 | Nit | 6-2, 6-4 | The random player wins 1 of 40 games on each. | Every block agrees (the random player uses fixed seeds). The pipeline does not gate this, and stages 3-5 shipped with the same. |
| n5 | Nit (carried, UX) | 6-4 | Protan minimum is 0.092, under UX's 0.105 guard. | The report's `protan_min` at (514,431), a meteor mover near the lake. Nothing in integration changed it. |

## Checks that pass

- **Dead first shots:** none. Every sweep angle hits a piece, and every piece is reached by some first shot (0 missed of 68, 62, 73, 65 and 72).
- **Long flights:** none over 15 s, except n1.
- **Stuck per 1,728 games, fresh:** 42, 23, 84 (m1), 7 and 27, all under 88. The sweep sticks on 4, 1, 8, 0 and 2 angles of 681.
- **Fresh ramp:**

  | Level | Fresh /48 | Four blocks | Pack pooled |
  |---|---|---|---|
  | 6-1 | 24.89 | 24.68-25.15 | 24.7 |
  | 6-2 | 23.56 | 23.25-24.12 | 23.52 |
  | 6-3 | 22.85 | 22.49-23.31 | 22.6 |
  | 6-4 | 20.73 | 20.46-20.97 | 20.57 |
  | 6-5 | 17.26 | 16.94-17.65 | 17.4 |

  - Each fresh figure is ± 0.20.
  - The steps are 1.33, 0.70 (M2) and 2.12, and the finale gap is 3.47. After one standard error is taken off, they are 1.04, 0.41, 1.84 and 3.18.
  - The opener (24.89) and the finale (17.26) are inside their bands (25 ± 1.2 and 17 ± 1.2).
  - The ease-back from 5-5 (17.41 fresh) is 7.48, inside the charter's 7.0-7.5 but at its top.
  - Both ends fall from MR 5: the opener by 0.55 and the finale by only 0.15.
  - The pack's pooled figures are within 0.25 of mine on every level.
- **Holdouts and cheap difficulty, fresh:**
  - The low-orange (y 430 or below) still pieces are:
    - 6-1: (101,454) and (699,454), left in 9.9% and 10.9% of lost games
    - 6-2: (526,440), 13.6%
    - 6-3: none
    - 6-4: none still (its movers dip under y 430 and are left in under 1%)
    - 6-5: (182,435), 11.0%
  - No low orange is left in more than 13.6% of lost games.
  - Top holdouts: 22.5% or less everywhere except 6-1 (n2).
- **Engine against composite:** engine-1280 matches the composite piece for piece on all five levels (the 6-4 and 6-5 movers are at their start phase). At 640, 6-3's ley dashes, city stars and ring moons still read, so the cut to chroma 0.3 is acceptable for layout.
- **Pipeline:** all five reports PASS with an empty `fails` list. The converter figures in `index.md` are inside their limits. ch-03 passes.

## Per-level verdicts

**6-1 The Armillary (base-26): APPROVE.**
- 24.89 fresh, 42 stuck per 1,728, and the sweep sticks on 4 angles (they linger in the lower ring bowl and at the hub).
- It traces its subject well:
  - The rings, colures, rods and the ecliptic band each carry their moons.
  - The four slanted bricks sit on the painted outer rays, and the corner moons lie on those rays' extensions (checked on the cleared frame).
  - The horizon-ring moons carry the ring's line out to the walls.
- Ten trace points were skipped on the crowded ecliptic, which is acceptable.
- If the stage needs ease taken out (M1), 6-1 has about 0.4 of room before the ease-back reaches 7.0.

**6-2 The Wheel of the Twelve (base-27): APPROVE, n1 aside.**
- 23.56 fresh, 22.5 stuck per 1,728.
- Each of the twelve signs is its own small figure on its painted stars. The wheel dividers and the hub's four ray moons read as the atlas wheel.
- First shots are light (median 6 pegs, and 98 of 681 angles hit 2 or fewer) because the board is a constellation. This is not dead: every angle scores.
- The step into 6-3 belongs to M2, but the fix belongs on 6-3.

**6-3 The Seer's Chart (base-28): REVISE (M2, m1, and the main lever for M1).**
- 22.85 fresh, 0.85 easier than its slot.
- The subject reads cleanly once the board is cleared: six city stars joined by dashed ley lines on the game's world map.
  - The rings sit on the cities.
  - The cloud-hump bricks sit on the cloud sea.
  - The ship, kraken and isles moons are honest map ornaments.
- What needs to change:
  - Harden it by about 0.8, back to about 22.0.
  - Lower the city-ring stickiness (m1) as part of the same change, for example by opening each ring's lower side. A gap of 11.8 px or less would be a cradle, which the loader refuses.
  - The lone right-hand ornament oranges, ships (700,300) and isles (536,400), are already top holdouts (19.7% and 19.6%). Hardening should not come from making them harder to reach.

**6-4 Meteor Shower (base-29): APPROVE.**
- 20.73 fresh, 7 stuck per 1,728, and no sweep sticks.
- The streaks point back to the radiant knot, so the shower converges as a shower should. The movers ride their own streaks, and the slope bricks sit on the range.
- The trim0 3.5 change holds the ramp.
- The top holdouts are the peak moons at (694,423), (232,415) and (182,429), just above the y 430 line. This is acceptable, but if anything moves here, keep them there and do not make them lower.

**6-5 The Moon Gate (base-30): APPROVE.**
- 17.26 fresh, 27 stuck per 1,728, and the finale gap is 3.47.
- It traces its subject: the phase crown sits on its painted discs, the two counter-rotating tracks sit on the gate's rings, and the rim bricks sit on the gate's top arc.
- The F6 tone band is invisible in the frames, as intended.
- The beams need someone to watch them in motion (n3).

**Stage: REVISE.** Measured honestly, the ramp has an unresolved 6-2 → 6-3 step, and the stage is not 0.5 harder than MR 5. Both come mainly from 6-3 sitting high.

A retune that fixes both:
- 6-3 to about 22.0.
- 6-1 or 6-2 down about 0.4 (6-1 to about 24.5 keeps the ease-back at 7.1 or more).
- This gives a mean of about 21.5 and steps of 1.0 to 1.5.
- Re-measure on fresh blocks, not on the tuned held-out games.

Also add the mean-fall rule to `stagecheck` or to the pack's index (m2).