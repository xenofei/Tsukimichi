# Response: MR 2 Limsa and Vesper Bay, stage fix

The fold-in for `review-mr2.md` (0 blockers, 3 majors, 9 minors). Each level's own finding was fixed on its own
commit (base-06 `31fce794`, base-08 `95ea6879`, base-10 `ed02c8f7`) and merged with `--no-ff`. No gate's rule or
threshold changed. `mfl.py stage mr2` passes: every level PASS, pooled mean 6.87 (band 5.13 to 7.33), finale 2.46
below the other levels' mean 7.36. The two Aces that moved were re-suggested (below).

## Blockers

None.

## Majors

| Id | Level | Answer |
|---|---|---|
| M1 | base-08 | Partly fixed (`95ea6879`). Held out eased from 11.6 to 10.5 per 48 (pooled 10.8) with layout-preserving moves. It is still the stage's outlier: margin to the band is -3.2, low-third share 5%. The stage mean now sits at 6.87 inside the band (it was 7.42, over the top), so the stage rule passes without the 0.3 tolerance, and I did not run `tune --stage`, which is for a failing mean or finale. Not fixed: the level is still about double the other four in wins; bringing it to 6.5 to 7 needs candidate moves into the lower third that change what the board traces. Left for the owner or a further retune. base-06 was not relaxed off the floor, so it stays at 5.5. |
| M2 | base-10 | Fixed (`ed02c8f7`). The mirrored reflection below the waterline is restored (gate and pillar bricks, reflection movers), and the finale is tuned to 5.2 held out (pooled 4.9). It now sits 2.46 below the other four's mean of 7.36, so the finale rule passes on its own and no longer depends on base-08. Remaining precheck WARNs: 43% of pieces over 34 units from a traced feature (below the 50% FAIL), densest window 37%. |
| M3 | base-06 | Fixed (`31fce794`). The orange candidate on the green ground at (136, 490) is dropped from the candidates. F9 protan minimum is now 0.089 at (254, 490) (precheck WARN, under 0.1; the build gate passes). |

## Minors and nits

| Id | Answer |
|---|---|
| m1 | base-07 sky scatter: accepted, unchanged. Precheck 80% untraced overstates it, as the review says (glints count); precheck FAIL is report only. |
| m2 | base-06 bar cluster: not changed. Precheck still reports 49% at (165, 386) and two 13.5 and 13.7 wall gaps. Left as it plays fine (14 stuck fires). |
| m3 | base-06 fog painting and bridge arcs: not changed; a painting change, out of scope for a stage fix. |
| m4 | base-08 wrecks (0.021 OKLab) and the rainbow arcs: not fixed; folded into the M1 retune only if cheap, and it was not. |
| m5 | base-09 stuck rule: 90 fires per 1728 against the shipped maximum 88 (WARN 70, FAIL 100), unchanged. `mfl.py stuck base-09` to be read before the owner plays. |
| m6 | Three night-bay paintings and the shared moon halo (07, 09, 10): not changed, a theme choice. |
| m7 | Brick share under 30%: not changed. |
| m8 | Convert "stale": `convert base-06 base-08 base-10` rerun and wrote no changes; `convert base-06 base-07 base-08 base-09 base-10 --check` is run below. |
| m9 | TUNING headers: base-06, base-08 and base-10 headers were rewritten by their retunes; base-07 still records "plays exhausted". |
| n1 | base-10 second jewel share 0.15 at the floor: unchanged (precheck WARN). |
| n2 | GLOSSARY.md and the 22 October owner's answers are not in this worktree's `docs/design/v9/levels`; not added here. |

## Aces

`MoonfallAces.Suggest` over 576 games, run with `Shipped_Aces_are_unchanged_by_the_hard_level_rule` on 06 to 10:
base-06 200,000, base-07 190,000, base-09 190,000 unchanged; base-08 220,000 to 210,000; base-10 210,000 to 190,000.
Recorded in `Tsukimichi.Core/Moonfall/Modes/aces.json`.
