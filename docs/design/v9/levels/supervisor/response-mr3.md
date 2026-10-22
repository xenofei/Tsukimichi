# Response: MR 3 The Night Skyway, review of 22 October 2026

Answers to `review-mr3.md` (0 Blockers, 3 Majors, 9 Minors). No gate's rule or threshold changed. All three Majors are the
same regression from the Peggle-match refit: more pieces can be orange, so orange candidates landed on dark land or pink
cloud where a protanope reads them least. The fix in each case is to leave the lowest-margin places blue, never a rule change.

## Where the stage stands (`mfl.py stage mr3`, 6,912 games a level)

| Code | Id | Pooled wins per 48 | Protan p10 / min (review: round 1 was 0.105 / 0.103, 0.112 / 0.111, 0.112 / 0.109) |
|---|---|---|---|
| 3-1 | base-11 | 4.85 | 0.102 / 0.097 (review 0.102 / 0.053) |
| 3-2 | base-12 | 6.02 | unchanged |
| 3-3 | base-13 | 4.60 | 0.104 / 0.096 (review 0.102 / 0.084) |
| 3-4 | base-14 | 4.33 | unchanged |
| 3-5 | base-15 | 4.28 | 0.106 / 0.081 (review 0.103 / 0.076) |

Stage mean 4.82 (band 3.04 to 5.24). The finale is 0.67 below the other levels' mean of 4.95, so the finale rule holds. The
F9 gate itself (p10 at least 0.101, the lowest approved pilot) passes on all five.

## Majors

- **M1 base-11, protan.** Fixed. The refit's worst orange place, the low arc brick at (420,510), and more
  low-margin bricks on the dark land and the brightest cloud ((399,483), (660,215), (569.5,491), and the arc at (599.5,423)
  that the refit had let back in) are left blue. The fixer branch had also turned two
  cloud-hump bricks orange to hold the ramp; the board plays 4.85 pooled. Protan minimum 0.053 to 0.097, p10 0.102. The precheck's `margins` FAIL is gone
  (it is now a WARN, p10 under the 0.105 guard).
- **M2 base-13, protan.** Fixed by the fixer branch: the ridge's west-end brick on pink cloud no longer deals orange
  (the tuning's two orange moves are dropped). Minimum 0.084 to 0.096, p10 0.104. The level still wins 4.6 per 48
  pooled.
- **M3 base-15, protan.** Fixed: the two bricks by the whale's tail on green, (122,255) and (168,248), are left blue
  (added to `b.candidates` exclusions). Minimum 0.076 to 0.081, p10 0.103 to 0.106 (now over the guard). The remaining
  minimum is the brick at (543,483); it is a WARN only (the FAIL needs p10 under 0.105 as well). The finale is held at
  4.28 pooled (3.9 before), so it stays the stage's hardest; it wins 3 of the 48 rule games (was 2, the floor).

## Minors and Nits

- **m1 base-15 stuck fires:** 92 to 74 per 1728 games (under the shipped maximum 88; precheck WARN 70). Not changed further.
- **m2, m3 base-14; m4 base-11; m6 base-15; n1, n2, n4:** not fixed here. Content and taste findings for the owner's
  Minor list (`minor-list`); nothing here touches those levels' art.
- **m5 palettes:** not fixed; the jewel pairs and crops are inside the charter's limits (24% against 25%).
- **m7 pack hygiene:** fixed: `stage mr3` was re-run, the pack is regenerated with `pack --changed-since 8eecd003`, and
  the Aces of base-11, base-13 and base-15 are re-suggested (190,000, 210,000, 210,000; the pack's renders show them).
- **m8 convert stale:** `convert base-11..base-15` re-run and `--check` passes.
- **m9 ch-06 on base-13:** not measured here; left for the owner's Minor list.
- **n3 base-15 rule seeds:** now 3 of 48.
