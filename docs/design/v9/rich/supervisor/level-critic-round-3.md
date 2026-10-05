# Level-design critic, round 3 (verbatim): OVERALL APPROVE

The same independent agent as rounds 1 and 2. Its report follows unchanged.

---

# Moonfall pilot levels: level-design critic report, round 3 (5 Oct 2026)

**All six levels pass. Nothing Major or Minor is left; the five remaining notes are Nits and can be left as they are.**

The round-2 blocker is fixed:

- `levels/base-p1.json` now matches its script.
- I rebuilt every level from its script (read-only, compared in my scratch folder). All six files match their scripts, and every script's `check()` returns no problems.

Every round-2 Minor is resolved:

- **base-p2:** heart #7 at (209,328) is now 13.9 px from the nearest brick end, up from 12.0. The ridge and billow stuck spot is gone: random play now records 1 stuck-rule clear on #7, down from 16 on #7 and #85 together.
- **exp-p2:** the field stars are r 9 and none sit above y 130. The least-lit piece is now lit in 22% of random games, up from 4%.
- **exp-p1:** the clouds sit at y 132–140, and the least-lit piece is now lit in 23% of games, up from 10%.
- **base-p3:** no peg is within 14 px of a wall.

I ran the same harness as before:

- `mfcheck validate`;
- `mfcheck sweep` at 1° (seed 1) and 0.5° (seed 2);
- `mfcheck play` with 48 games; the shipped baseline is the 48-game run from round 2;
- my own harness: 300 random full games, and the greedy player with leftover oranges logged;
- direct reach, the spread scan at 1 px steps, brick-end/peg and wall gaps, and contrast on the re-rendered composites at 1× and 0.8×.

I also looked at every composite at 0.8×.

## base-p1 "The Airship Road": **APPROVE**

- **Spread:** 26 candidates, at most 9 in any 200×200 square, and 5 left and 5 right below y 400.
- **Play:** the greedy player wins 7 of 48 (15%), with 2.85 oranges left on average. The shipped levels win 10–27% with 1.7–3.9 left. No orange dominates the losses: the most frequent, #63 (500,420), is left in 12 of 41.
- **Traps:** the stuck rule fires on 0.4% of random-play shots, and every piece is reached by some first shot.
- **Subject:** the route reads: the dots, the rings of four and the ships' lights.
- **Nit:** the compass's orange #41 (524,343) is the lowest contrast in the set, 2.3 at 1× and 2.2 at 0.8×, over the pale engraving. Acceptable.

## base-p2 "The Holy See": **APPROVE**

- **Play:** the greedy player wins 13 of 48 (27%), equal to the easiest shipped level, with 2.00 oranges left on average.
- **Traps:** the stuck rule fires on 0.6% of random-play shots, down from 1.1%.
- **Spread:** at most 10 in a square, and 7 left and 4 right below y 400.
- **Nit:** two blue pegs are rarely lit. #42 (700,186) is never reached by a first shot and is lit in 65 of 300 random games; #19 (624,134) is lit in 68. Both are blue and reachable later, so they are acceptable.

## base-p3 "The Moonlit Post": **APPROVE**

- **Play:** the greedy player wins 9 of 48 (19%), with 2.75 oranges left on average.
- **Traps:** the stuck rule fires on 0.5% of random-play shots. No pinches, and spread passes.
- **Nit:** the stuck rule fires on 17 of 341 first shots in the 0.5° sweep (5.0%), at the method's limit, though only 3.5% in the 1° sweep and 0.5% in random play.
- **Nit:** the blue pom-pom pegs #11 (602,99) and #10 (572,83) are lit in 42 and 58 of 300 random games.
- **Nit:** the lower board still reads as a lattice of rows, as in round 2.

## exp-p1 "The Domes of Sharlayan": **APPROVE**

- **Play:** the greedy player wins 9 of 48 (19%).
- **Traps:** the stuck rule fires on 1 of 171 first shots and 0.2% of random-play shots.
- **Nit:** the urn #5 (108,250) and the bottom water peg #11 (101,476) are the most common last oranges, left in 18 and 17 of 39 losses. Both are reachable (lit in about 160 of 300 games), so they are the level's hard corner.

## exp-p2 "The Ferry in the Stars": **APPROVE**

The ferry reads more clearly than in round 2.

- **Play:** the greedy player wins 9 of 48 (19%).
- **Traps:** the stuck rule fires on 0.3% of random-play shots, and every piece is reached by some first shot.
- **Readability:** the best of the six, with a lowest contrast ratio of 3.0.

## exp-p3 "The Sea of Sorrows": **APPROVE**

The level is unchanged since round 2. The greedy player wins 7 of 48 (15%), and the stuck rule fires on 0.1% of random-play shots. On the new grade the lowest contrast ratio is 2.8 at 1× and 2.7 at 0.8×.

## Measurements (round 3)

The greedy column is `mfcheck play` with 48 games at level 5. Random play is 300 games, about 3,600 shots. The contrast column is the lowest ratio of any peg against its backdrop, at 1× and at 0.8×.

| Level | Pieces | Candidates | Most candidates in a 200×200 square | Candidates below y 400 (left, right) | Median pegs per first shot | Stuck rule, first shots (1°, 0.5°) | Stuck rule, random play | Pieces no first shot reaches | Candidates no direct flight can touch | Least-lit piece (share of random games) | Greedy wins / 48 | Mean oranges left | Lowest contrast (1×, 0.8×) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| base-p1 | 84 | 26 | 9 | 5, 5 | 10 | 0.6%, 0.3% | 0.4% | 0 | 0 | 0.41 | 7 | 2.85 | 2.3, 2.2 |
| base-p2 | 89 | 26 | 10 | 7, 4 | 13 | 1.8%, 2.1% | 0.6% | 1 (#42, blue) | 0 | 0.22 | 13 | 2.00 | 2.2, 2.2 |
| base-p3 | 101 | 26 | 8 | 7, 4 | 14 | 3.5%, 5.0% | 0.5% | 0 | 0 | 0.14 | 9 | 2.75 | 2.4, 2.4 |
| exp-p1 | 67 | 29 | 9 | 4, 3 | 9 | 0.6%, 0.3% | 0.2% | 0 | 0 | 0.23 | 9 | 2.73 | 2.4, 2.3 |
| exp-p2 | 102 | 35 | 9 | 6, 6 | 15 | 1.2%, 0.6% | 0.3% | 0 | 0 | 0.22 | 9 | 2.54 | 3.0, 3.0 |
| exp-p3 | 72 | 28 | 7 | 3, 6 | 9 | 1.8%, 1.2% | 0.1% | 0 | 0 | 0.34 | 7 | 3.44 | 2.8, 2.7 |
| base-01 (shipped) | 55 | 55 | 16 | 11, 11 | 8 | 0% | 0% | 0 | 0 | 0.59 | 9 | 2.56 | not measured |
| base-02 (shipped) | 57 | 57 | 16 | 11, 12 | 6 | 2.9% | 0.7% | 0 | 0 | 0.35 | 5 | 3.85 | not measured |
| base-03 (shipped) | 65 | 65 | 16 | 15, 14 | 12 | 8.2% | 1.1% | 0 | 0 | 0.51 | 13 | 1.73 | not measured |
| base-04 (shipped) | 72 | 65 | 18 | 13, 15 | 12 | 2.9% | 1.2% | 0 | 0 | 0.62 | 9 | 2.48 | not measured |

All six levels pass the loader, with no cradling gaps and no wall pinches. No peg is within 12 px of a brick end; the closest is 13.0 px, at exp-p1's finials. The launcher zone and the bucket zone are clear. Each level reads as its subject at 0.8×, and the six techniques stay distinct.

**Still open, outside the level verdicts:**
- the engine's bucket-rim chatter at the right wall, which is with the owner;
- the purples and greens dealt by seeds other than seed 1, which I have not checked;
- whether the scene grades are realistic, which is the realism supervisor's call.

OVERALL: APPROVE
