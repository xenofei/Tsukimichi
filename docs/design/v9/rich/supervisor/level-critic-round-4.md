# Level-design critic, round 4 (verbatim): OVERALL APPROVE

The same independent agent as rounds 1–3. Its report follows unchanged; the designer's response is at the end.

---

Level critic, round 4: OVERALL APPROVE. All six levels pass; one Minor and one Nit on base-p3 need no re-review. The same report is going to the coordinator.

# Moonfall pilot levels: level-design critic report, round 4 (5 Oct 2026)

**Scope I checked.** Between commits 83c4de95 and 7932b45b, the only change to base-p1, base-p2, exp-p1 and exp-p3 is the `veil` value; exp-p2 is unchanged. Only base-p3's layout changed. I rebuilt every level from its script (read-only, in my scratch folder): all six files match, and every pre-flight returns no problems.

## base-p3 "The Moonlit Post": APPROVE
- **Hard rules:**
  - the loader passes: 85 pieces, 26 candidates;
  - no cradling gaps and no wall pinches;
  - the lowest piece is at y 504, so the lantern has its lane;
  - every candidate can be touched by a direct first flight;
  - every piece is reached by some first shot.
- **Spread:** at most 10 in any 200×200 square (my 1 px scan); 8 left and 3 right below y 400.
- **Traps:**
  - the stuck rule fires on 5 of 171 first shots (2.9%) at 1° and 15 of 341 (4.4%) at 0.5°;
  - in random play it fires on 0.4% of shots, down from 0.5%;
  - no spot repeats more than 3 times in 300 games.
- **Play:**
  - median 12 pegs per first shot, against 14 last round and 6–12 for the shipped levels;
  - 20 of 171 first shots are caught by the bucket;
  - `mfcheck play` with 96 games: 13 wins (13.5%) and 3.01 oranges left on average, matching your 13 of 96;
  - the shipped levels win 10–27% at 48 games each, leaving 1.7–3.9 oranges on average;
  - random play clears 18.1 oranges on average.
- **Minor:** treetop candidate #47 (674,418) is still orange in 30 of the 83 lost greedy games (36%). It is reachable, so it is not a trap, but it is the clear last orange. Next most common: #14 (586,190) and #49 (138,450), 20 each.
  - Fix: make #47 blue and move the candidate one treetop to the left. No re-review is needed.
- **Nit:** the blue pom-pom pegs #11 (602,99) and #10 (572,83) are still lit in only 43 and 59 of 300 random games. This is unchanged and acceptable as part of the drawing.
- **Readability:**
  - the lowest contrast ratio is 3.3 at 1× and 3.1 at 0.8×, the second-highest in the set;
  - the moogle reads clearly at 0.8×, and the forest is now two rows plus the treeline, less of a lattice.

## Readability on the new composites (all six)
The lowest peg-against-backdrop contrast ratio, at 1× and at 0.8×, now and in round 3:

| Level | Round 4 | Round 3 |
|---|---|---|
| base-p1 | 2.7, 2.7 | 2.3, 2.2 |
| base-p2 | 2.4, 2.4 | 2.2, 2.2 |
| base-p3 | 3.3, 3.1 | 2.4, 2.4 |
| exp-p1 | 2.6, 2.5 | 2.4, 2.3 |
| exp-p2 | 3.0, 3.0 | 3.0, 3.0 |
| exp-p3 | 3.3, 3.2 | 2.8, 2.7 |

- **Purple** is 4.5 or more everywhere, and **green** 3.7 or more.
- The stronger veils raise contrast everywhere, even with the brighter p99 grades.
- The restored modelling in base-p2's cloud sea and exp-p3's planet does not compete with the pegs. Each level still reads as its subject at 0.8×.
- **Lantern spill:** I checked it at 2× on exp-p2, exp-p1, base-p1 and exp-p3. It is subtle; blue pegs stay blue and orange stays orange, and the lantern sits clear of the pegs in every composite.

## The other five levels: APPROVE
The layouts are unchanged, so the round-3 play measurements stand: greedy wins of 7, 13, 9, 9 and 7 of 48 for base-p1, base-p2, exp-p1, exp-p2 and exp-p3. Every candidate can be touched by a direct first flight, and the stuck rule fires on 0.1–0.6% of random-play shots.

**Still open, outside the level verdicts:**
- the engine's bucket-rim chatter at the right wall, which is with the owner;
- purples and greens dealt by seeds other than seed 1, which I have not checked;
- whether the scene grades are realistic, which is the realism supervisor's call.

OVERALL: APPROVE

---

## Designer's response

I took the Minor as the critic suggested (no re-review needed). Treetop #47 (674,418) is blue, and the candidate moved one treetop to the left (636,…).
- The level still has 26 candidates and passes the pre-flight and the loader.
- `mfcheck play` with 96 games: 16 wins (it was 13), with 3.0 oranges left on average.
- The Nit (the pom-pom pegs) is left as part of the drawing.
