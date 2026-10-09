# Response: MR 5 Harbour Lights, round 1

The fold-in for the round 1 reviews (`game-designer-mr5-round-1.md`, `ux-mr5-round-1.md`, `level-critic-mr5-round-1.md`).
Each level's own findings were fixed on its own commit (5-1 `236c7343`, 5-2 `701657e9`, 5-3 `a27ab1d4`, 5-4 `71a9bff6`,
5-5 `756d6121`); the stage pass then retuned 5-5 for the ramp. Every finding is answered below. No gate's rule or
threshold changed. The pack in `docs/design/v9/levels/review/mr5/` is regenerated: every level's renders, reports and
trace sheets (each on its level's commit, 5-5's again on the stage commit), `stage-mr5.txt` and `index.md`.

## Where the stage stands

| Code | Id | Name | Held out | Pooled | Jewels | F6 worst | Protan p10 / min | Converter 2x max / p99.9, 1x p99 |
|---|---|---|---|---|---|---|---|---|
| 5-1 | base-21 | The White Towers | 25.9 | 25.45 | 275 / 210 | 0.224 | 0.132 / 0.123 | 0.039 / 0.011, 0.010 |
| 5-2 | base-22 | The Strait of Merlthor | 23.5 | 24.02 | 290 / 357 | 0.326 | 0.115 / 0.103 | 0.022 / 0.015, 0.035 |
| 5-3 | base-23 | Lights on the Water | 22.0 | 22.3 | 255 / 171 | 0.206 | 0.108 / 0.107 | 0.008 / 0.005, 0.007 |
| 5-4 | base-24 | The Arches of the Bridge | 21.3 | 21.07 | 284 / 356 | 0.223 | 0.118 / 0.110 | 0.038 / 0.008, 0.007 |
| 5-5 | base-25 | The Lighthouses of Vylbrand | 17.1 | 17.7 | 246 / 168 | 0.267 | 0.108 / 0.104 | 0.020 / 0.014, 0.018 |

- `mfl.py stage mr5`: PASS. Pooled steps 1.43, 1.72 and 1.23 (± 0.41); finale gap 3.37 ± 0.40. The mean is 22.11.
- `mfl.py convert base-21 base-22 base-23 base-24 base-25 --check`: PASS, 10 of 10.
- `mfl.py selftest`: every checker passes its known-bad and known-good cases.
- `pwsh tools/gate.ps1`: the result is in the commit message and the hand-back (it ran after this file was written).
- Aces over 576 games: 5-1 270,000 (305 wins), 5-2 250,000 (288), 5-3 240,000 (260), 5-4 230,000 (254, was 240,000:
  its round-1 layout), 5-5 230,000 (206, was 240,000: the stage retune). The Perf test
  `Shipped_Aces_are_unchanged_by_the_hard_level_rule` passes 25 of 25. 5-5 is now the lowest shipped win count, so the
  README's Aces note reads 206-389.

### The stage retune (5-5)

The fixes to 5-3 and 5-4 made both levels harder (pooled 23.05 → 22.3 and 21.68 → 21.07). `stage mr5` then failed:
the finale gap was 2.82 ± 0.40, not resolvably 2.5. 5-4 could not ease enough to close it without collapsing its step
from 5-3 (21.37 at most for a resolved 0.5), so the finale moved instead. 5-5's contour orange moved from (606, 211) to
(632, 235), the variant its author had measured about 0.5 harder. I compared it with three other single swaps on the
tuning seeds (1-864, then 3,456 games from seed 20001):

| Variant | 1-864 | 20001-23456 |
|---|---|---|
| round-1 layout | 18.8 | 17.7 |
| (606, 211) → (632, 235), kept | 17.2 | 17.3 |
| (668, 297) → (632, 235) | 17.4 | 17.9 |
| (668, 297) → (654, 264) | 15.9 | 17.6 |
| (601, 309) → (632, 235) | 16.7 | 17.5 |

Held out 17.8 → 17.1, pooled 18.25 → 17.7, inside the band (18, and 16.8 allowed). The rule's 48 games win 18, stuck
fires are 61 per 1,728, sweep stuck 5 of 681, and low oranges are 2.5% of those left. The swap stays on the summit's
contour, so the subject is unchanged.

## Game designer

- **M1 (5-3 lamps cold, one light): fixed on 5-3's commit.** No moon stands on a lamp's fire. Six fires ship as
  flickering small lights (Q1, Q3, Q5, both jetty lamps, the buoy), and a cream region keeps the fires, loggias and
  windows warm (Q4's head rgb(98,117,147) → (126,119,105)). Not fully as asked: Q2, Q4 and Q6 stay painted fires
  without flicker, because F5's "never a row" allows at most three small lights in a 40-unit band and the six quay
  lamps sit within 36 units of each other. The rule stands.
- **M2 (5-4 not lamplit, scatter, ruled deck, rose smear): fixed on 5-4's commit, option (a).** Six warm flickering
  lights on the painting's own lamps and windows (three deck lamps, the dome drum, two tower windows), with a moon on
  each of the other three deck lamps; two broad warm glows over the deck. The upper scatter is folded into features
  (chimney tops, spire, roof turret, cable anchor, stair, castle wall); the deck row is three spans between the piers.
  The rose foot fades by y 590 under a darkening tone and a paler lilac mist. Not done: the dome's crown as a brick,
  which the reach gate refuses (no ball reaches it); the drum's light and flank carry the dome.
- **m1 (5-1 lavender towers, lawn foot, arcs over nothing, no beams): fixed on 5-1's commit.** The foot is sea teal
  #2AA69C, the castle's lit faces are kept from the jewel and lifted (tone 1.22 → 1.26), a cloud-bank tone puts the
  billow crowns on cloud, and the beams run at F4's k 0.08. In part: the towers are neutral grey-white, not brighter,
  since tone 1.30 broke the scene ceiling (p99 luma 0.470 against 0.465).
- **m2 (5-5 lighthouses faint, beams faint, seam): fixed in part on 5-5's commit.** The seam was the shaft angle
  wrapping at 180°, not a feather; Aleport's beam now points at 162° and no beam is within 18° of the wrap. The lamps
  are wider, softer warm discs (core 1.8 → 2.6 units). Not done: brighter beams (k 0.08 is F4's maximum, enforced by
  `dress.py` and the runtime loader) and a larger halo (13 units broke F6's drop at the Aleport ring, 0.034 against
  0.02). The beams in motion still need a look in round 2.
- **m3 (the stage pairs up and leans green): improved, not closed.** 5-1's foot is sea teal now, so its pair is its own
  (275 / 210), and 5-4's warm lamps over a violet dusk floor set it apart from 5-1, as you suggested. Two green feet are
  left, 5-3 and 5-5, and they are not neighbours. The source pairing (two base01 crops, two blue maps with lanes) is the
  sources plan's and stays; 5-3's slot and technique are the charter's.
- **m4 (5-2 south ring over an erased rose): fixed on 5-2's commit.** A soft tone lifts the south compass rose out of
  the foot's darkness under its ring.
- **n1 (5-3 nine combs, plinko middle): partly addressed on 5-3's commit.** The flank moons at the fires' height and
  the re-picked candidates break the columns' regularity a little. The columns themselves stay: they are the subject.
- **n2 (5-2 third board on base05): accepted, as you did.** The grade separates it from 3-1.
- **Ramp: accepted in round 1, changed since.** The steps are now 1.43, 1.72 and 1.23 and the finale gap 3.37 (see the
  retune above). The gap is a little under your 3.5-4 aim from MR 4, but resolved by 0.47.

## UX

- **m1 (stage protan margin, guard p10 ≥ 0.105): fixed on 5-2's and 5-5's commits.** 5-2 0.101 → 0.115 (the
  rose-copper region gives way round Ul'dah's ring), 5-5 0.102 → 0.108 (the west sea region and two warm glows eased).
  After the stage retune 5-5 is still 0.108 / 0.104. Every MR 5 level is now at or above 0.108, above your 0.105 guard.
- **m2 (5-2 route 1 ends on the guide column): fixed on 5-2's commit.** The gilt route stops at (370, 288).
- **m3 (5-5 lamp cores as bright as the ball): fixed on 5-5's commit.** Core k 0.95 → 0.38; cleared-1280 cores peak at
  L 0.75-0.78, under the guide dots and the ball.
- **n1 (5-3 buoy beside the guide): no action, as you said.**
- **n2 (5-3 F6 0.001 under the shipped lowest): noted.** F6 worst is now 0.206 after the cream reflections, above the
  0.20 floor; the reflections cannot brighten further for this reason.
- **n3 (5-2 lettering beside the guide as speckle): not fixed.** It is the painting's own lettering, and a positional
  fix there would cover pegs.
- **n4 (5-3 pale lamp heads on the cleared board): no action.** They read as street lamps, as you found.

## Level critic

- **m1 (5-3 reflections brown, lamps cold): fixed on 5-3's commit.** The reflections are laid over the water in cream
  gold (hue about 45° → 70°), and the cream region keeps the lamps warm. They are not brighter: F6 is at 0.206 against
  its 0.20 floor with the moons on the columns.
- **m2 (5-5 seam at y 404): fixed on 5-5's commit.** See the game designer's m2: the 180° wrap.
- **m3 (5-3 jetty ledge): fixed on 5-3's commit.** The brick run ends 24 units from the wall; sweep stuck 3 → 2.
- **m4 (the mean falls only 0.36 on fresh seeds): fixed by the round's retunes.** The mean is now 22.11 pooled: a fall
  of 0.96 from MR 4's pooled 23.07 and of 0.70 from your fresh 22.81, inside the charter's 0.5-1.0. Fresh blocks were
  not re-run here; the pooled figures each include 5,184 fresh games. The mean is 0.49 under the charter's 22.6, so MR 6
  (charter 21.6) should not fall by more than about 0.5.
- **m5 (protan passes only through the fallback): fixed in part.** See UX m1: p10 is at least 0.108 on every level, but
  5-2, 5-3, 5-4 and 5-5 still sit under the 0.12 floor and pass through the pilot fallback.
- **m6 (5-4 filler): fixed on 5-4's commit.** The cloud, mist and under-arch filler is gone (83 → 71 pieces); the
  upper pieces are on chimneys, spire, turret, stair and the castle wall.
- **n1 (5-1 lawn foot): fixed on 5-1's commit** (sea teal).
- **n2 (5-2 two-peg gaps hold the ball): not fixed.** It needs a layout change that would move the ramp and the 5-2 → 5-3
  step; play stuck is 56 per 1,728 against 88.
- **n3 (5-5 top holdout at the 25% line): eased by the retune.** (459, 390) is left in 24% of lost games now (25%
  before); it is above y 430, so the cheap-difficulty gate does not apply.
- **n4 (5-1, 5-3 and 5-5 share a jewel pair): improved.** 5-1 moved to 275 / 210 (violet and sea teal); 5-3 (255 / 171)
  and 5-5 (246 / 168) still share theirs, two levels apart.
- **n5 (ease-back 8.3 against 7.0-7.5): accepted.** 4-5 and 5-1 are both in their bands and 5-1's layout is unchanged;
  narrowing it means making the opener harder, which the band allows only at its edge. Left for the game designer.
- **n6 (5-2 invented south compass): kept, with the rose lifted under it** (game designer m4).
- **n7 (thin pipeline margins): noted.** Now: 5-1 2x max 0.039, 5-4 0.038 (limit 0.04); 5-2 p99.9 0.015 and 1x p99
  0.035 (limits 0.015 and 0.035); 5-1 second-jewel share 0.169 (floor 0.15); 5-1 scene ceiling held under 0.465. All
  pass; none were loosened.
