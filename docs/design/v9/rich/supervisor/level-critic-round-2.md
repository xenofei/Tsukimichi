# Level-design critic, round 2 (verbatim)

The same independent agent as round 1 re-ran its harness on the round-2 files. Its report follows unchanged; the designer's response is at the end.

---

# Moonfall pilot levels: level-design critic report, round 2 (5 Oct 2026)

**Five of the six levels pass. base-p1 does not, because its level file and its script disagree, and each version breaks one rule.**

Every round-1 Major is fixed in the files as they stand:

- the base-p2 ridge cup and wall wedge are gone;
- the exp-p1 dome notch is gone: the stuck rule fires on 1 of 171 first shots, down from 23;
- every orange candidate in all six levels can be touched by a direct first flight, against 3 of 26 last round in the worst levels;
- every pilot's greedy win rate is now inside the shipped range.

**The base-p1 mismatch.** `levels/base-p1.json` (08:21) is older than `src/level_airship_road.py` (08:27). The two have the same 84 positions, but the script makes only three of the four pegs in each northern ring candidates, so it has 29 candidates and the file has 32.

- **The file**, which the composite shows, fails the method's spread rule: 12 candidates in one 200×200 square.
- **The script's version** passes spread, but the greedy player wins only 3 of 48, below every shipped level.

The cause is in the process. Every `level_*.py` calls `L.check()` and ignores what it returns, then writes the level anyway. So the "enforced" pre-flight does not actually stop an export.

I re-ran everything from round 1 on the current files:

- `mfcheck validate`;
- `mfcheck sweep` at 1° (seed 1) and 0.5° (seed 2);
- `mfcheck play` with **48** games, and the same for the shipped levels so the baseline is like for like;
- my own harness: 300 random full games (about 3,600 shots), and the greedy player with leftover oranges logged;
- direct reach at 0.1° steps, the spread scan at 1 px steps, and contrast on the new composites at 1× and 0.8×.

I also looked at every new composite at 1× and 0.8×.

---

## base-p1 "The Airship Road": **CHANGES**

The route now reads. The 14 trail dots run through the gaps in the rings of four along the painted dashes, and at 0.8× you can follow it from Limsa to Ul'dah, north to Gridania and Ishgard, east to Ala Mhigo and over the sea.

1. **Major: the level file and its script disagree, and each version breaks one rule.**
   - **Evidence, the file** (32 candidates, which the composite shows):
     - The square x 297–497, y 162–362 holds **12** candidates; the limit is 10.
     - The pre-flight's own 20 px grid also finds 12, at (235, 160) and (295, 160).
     - Greedy player: 6 of 48 wins.
   - **Evidence, the script's version** (29 candidates; I exported it to my scratch folder only):
     - Spread passes: at most 10 in a square; below y 400 there are 8 candidates on the left and 3 on the right.
     - Greedy player: **3 of 48** (6%), with 3.65 oranges left on average.
     - That is below every shipped level: 5 to 13 of 48 (10–27%), averaging 1.7 to 3.9 left.
     - The oranges most often left at a loss: Ul'dah's ring #5 (289,515) in 16 of 45 lost games and #4 (320,503) in 13; the cloud-edge candidate #46 (680,246) in 16; Limsa's ring #0 (142,461) in 12.
   - **Fix:**
     - Re-export from the script.
     - Make the winnable rule hold too. For example, give the low Ul'dah ring three candidates of four, like the northern rings, and move the cloud-edge candidate off #46 to a more central piece. Aim for at least 5 of 48.
     - Make `check()` fail the export: raise when it returns problems.
2. **Minor: the purple can land on rarely reached pegs.** No candidate is out of reach any more, but #83 (680,160) is lit in 124 of 300 random games. Nothing to do now.

Readability is fine: the lowest contrast ratio is 2.6 at 1× and 2.5 at 0.8×, on the compass pegs over the pale engraving. Purple is 5.7 or more.

## base-p2 "The Holy See": **APPROVE**

The ridge is monotone and starts 18 px from the wall, and there is no cup. The stuck rule fires on 1.1% of random-play shots, down from 2.5% and inside the shipped 0–1.2%. Spread passes: at most 10 in a square, and 7 left and 4 right below y 400. The greedy player wins 6 of 48. It reads clearly as Ishgard over the cloud sea.

1. **Minor: one billow heart cradles the ball.**
   - **Evidence:**
     - Heart #7 (209,314) is **12.0 px** from the right end of billow brick #85, at (182.1, 311.7). That is exactly a ball's width.
     - It is the level's one remaining stuck spot: the ball sits at about (190, 305), and #85 and #7 account for 16 of the 40 stuck-rule clears in random play.
     - The pre-flight's notch check only compares brick with brick, so it cannot see this.
   - **Fix:**
     - Lower #7 to y 326 or more. That gives about 15.5 px to #85 and about 21 px to #86.
     - Extend the notch check to brick-end/peg pairs.
2. **Minor: the purple sometimes lands on the spire tips.**
   - **Evidence:**
     - #18 (549,58) and #20 (588,86) are blue now, but they are lit in only **13 and 9 of 300** random games.
     - Any blue peg can be dealt purple or green. The level-5, seed-1 composite shows the purple on #18, where it is almost never collectable.
   - **Fix:** remove #18 and #20, or move the tip pegs down to about y 110 or below. The spire's painted tips still read without them.

Readability: the lowest contrast ratio is 2.3, for orange #7 on the bright cloud bank, and the oranges stay clear at 0.8×.

## base-p3 "The Moonlit Post": **APPROVE**

The moogle reads at once. No candidate sits at the launcher's height, and the stuck rule fires on 0.9% of random-play shots. The greedy player wins 5 of 48, which equals base-02, the hardest shipped level. That is at the edge of the range but inside it.

1. **Minor: the top pom-pom candidate is the most common last orange.**
   - **Evidence:** #12 (603,133) is still orange in **15 of 42** lost greedy games, and is lit in 106 of 300 random games.
   - **Fix:** move the pom-pom ring's candidate one peg down the ring, about (585,191).
2. **Minor: rarely reached blue pegs can carry the purple or the greens.**
   - **Evidence:** #10 (572,83) and #11 (602,99) are lit in 60 and 45 of 300 random games, and the sky peg #90 (700,160) in 43.
   - **Fix:** remove #90. Accept the pom-pom pegs as part of the drawing.
3. **Minor: two pegs sit exactly at the wall-gap limit.**
   - **Evidence:** #48 (98,457.5) and #74 (98,530.4) leave **12.5 px** to the wall. In random play the ball got stuck against #74 three times, at (80, 530).
   - **Fix:** move both to x 100 or more.
4. **Nit: the lower board reads as a grid.** Below y 400 there are 4 staggered rows of about 40 pegs. At 0.8× they read as a lattice more than a forest, and they dilute the moogle a little. Cutting one row would help.

Readability: the lowest contrast ratio is 2.3, for blue #86 (554,540) in the lantern's glow; everything else is 3.3 or more.

## exp-p1 "The Domes of Sharlayan": **APPROVE**

The notches are gone: the stuck rule fires on 1 of 171 first shots and 0.2% of random-play shots. The domes now read, with full crowns, a lower course and drums, and the statue's dotted outline is legible. The greedy player wins **11 of 48**, the best of the six. Spread passes: at most 9 in a square, and 4 left and 3 right below y 400.

1. **Minor: the four sky pegs are almost dead.**
   - **Evidence:** blue cloud pegs #37–#40, at y 84–110, are lit in only 29–50 of 300 random games.
   - **Fix:** lower them to about y 130 or below, or remove two.
2. **Nit: one water peg is the most common last orange.** The bottom water peg #11 (101,476) is still orange in 17 of 37 lost greedy games, but it is reachable: the column's pegs are lit in 159–170 of 300 games. It is acceptable as the level's hard corner.

Readability: the lowest contrast ratio is 2.3, for the great dome's blue finial #0 (432,231).

## exp-p2 "The Ferry in the Stars": **APPROVE**

Every candidate is within direct reach. The stuck rule fires on 0.3% of random-play shots. The greedy player wins 9 of 48. Spread passes: at most 10 in a square, and 5 left and 5 right below y 400. Readability is the best of the six, with a lowest contrast ratio of 3.0. The ferry still reads.

1. **Minor: the star field now competes with the figure.**
   - **Evidence:**
     - The sky stars went up to r 10 while the figure's line dots are r 8. The lighter stroke is now the figure's, which reverses the method's line weights.
     - With 106 pieces, the evenly spread field flattens the Milky Way's density.
     - The ferry reads more weakly at 0.8× than in round 1.
   - **Fix:** field stars to r 9, and cut the near-dead top-edge stars (point 2).
2. **Minor: nine top-edge stars are near-dead.**
   - **Evidence:**
     - Blue field stars #33–#41, at y 62–105, are lit in only **13–53 of 300** random games.
     - They are about 8% of the board, so the purple will often land where it cannot be collected.
     - #49 (696,178) is never reached by a first shot.
   - **Fix:** remove them, or drop them below y 130. That also brings the piece count towards 95.
3. **Nit: the two sea-glint rows read as a lattice.** A wave-like stagger would help.

## exp-p3 "The Sea of Sorrows": **APPROVE**

The storms moved to the night limb, so the bright planet is clear of pegs. The lowest contrast ratio is now 2.9, up from 1.9. There are 28 candidates, with the best spread of the six: at most 7 in a square, and 3 left and 6 right below y 400. The stuck rule fires on 0.1% of random-play shots, and the greedy player wins 7 of 48.

1. **Nit: the planet's interior is now empty.** It is a hollow of radius 124, but it does not swallow balls: shots aimed at it (0–29°) still light 2–14 pegs each.
2. **Nit: the low field's candidates are frequent last oranges.** #61 (330,512) is left in 15 of 41 lost games and #64 (660,516) in 13. They are fine as the level's hard corner.

---

## Measurements (round 2)

The greedy column is `mfcheck play` with 48 games at level 5, for the shipped levels too. Random play is 300 games, about 3,600 shots. The contrast column is the lowest ratio of any peg against its backdrop, at 1× and at 0.8×.

| Level | Pieces | Candidates | Most candidates in a 200×200 square | Candidates below y 400 (left, right) | Median pegs per first shot | Stuck rule, first shots (1°) | Stuck rule, random play | Pieces no first shot reaches | Candidates no direct flight can touch | Least-lit piece (share of random games) | Greedy wins / 48 | Mean oranges left | Lowest contrast (1×, 0.8×) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| base-p1 (file) | 84 | 32 | **12** | 8, 3 | 10 | 0.6% | 0.4% | 0 | 0 | 0.41 | 6 | 3.44 | 2.6, 2.5 |
| base-p1 (script) | 84 | 29 | 10 | 8, 3 | n/a | n/a | n/a | n/a | 0 | n/a | **3** | 3.65 | n/a |
| base-p2 | 91 | 26 | 10 | 7, 4 | 13 | 2.9% | 1.1% | 1 (#44) | 0 | 0.03 | 6 | 2.90 | 2.3, 2.3 |
| base-p3 | 102 | 27 | 8 | 7, 4 | 14 | 2.9% | 0.9% | 0 | 0 | 0.14 | 5 | 3.15 | 2.3, 2.2 |
| exp-p1 | 68 | 29 | 9 | 4, 3 | 9 | 0.6% | 0.2% | 0 | 0 | 0.10 | 11 | 2.48 | 2.3, 2.3 |
| exp-p2 | 106 | 35 | 10 | 5, 5 | 15 | 0.6% | 0.3% | 1 (#49) | 0 | 0.04 | 9 | 2.81 | 3.0, 3.0 |
| exp-p3 | 72 | 28 | 7 | 3, 6 | 9 | 1.8% | 0.1% | 0 | 0 | 0.34 | 7 | 3.44 | 2.9, 2.8 |
| base-01 (shipped) | 55 | 55 | 16 | 11, 11 | 8 | 0% | 0% | 0 | 0 | 0.59 | 9 | 2.56 | not measured |
| base-02 (shipped) | 57 | 57 | 16 | 11, 12 | 6 | 2.9% | 0.7% | 0 | 0 | 0.35 | 5 | 3.85 | not measured |
| base-03 (shipped) | 65 | 65 | 16 | 15, 14 | 12 | 8.2% | 1.1% | 0 | 0 | 0.51 | 13 | 1.73 | not measured |
| base-04 (shipped) | 72 | 65 | 18 | 13, 15 | 12 | 2.9% | 1.2% | 0 | 0 | 0.62 | 9 | 2.48 | not measured |

All six levels pass the loader. Each has at least 25 candidates. The launcher zone and the bucket zone are clear, and mfcheck finds no cradling gaps and no wall pinches. The six techniques remain distinct.

**Process notes:**
- The pre-flight's spread scan steps in 20 px. A 5 px step costs little and matches what I measure.
- The engine's bucket-rim chatter at the right wall stays with the owner, as you noted.
- Not verified: purples and greens dealt by seeds other than seed 1, and whether the scene grades are realistic, which is the realism supervisor's call.

OVERALL: CHANGES. base-p1 needs changes; base-p2, base-p3, exp-p1, exp-p2 and exp-p3 are approved.

---

## Designer's response (round 3 changes)

**Process.**
- Every `level_*.py` now raises when `check()` reports a problem, so a failing level is never written. The first base-p1 revision was refused this way.
- The spread scan steps in 5 px.
- A peg closer than 13 px to any brick is flagged (the brick-end-to-peg cradle).

**base-p1 (the Major).**
- The file is exported from the script again.
- Five rings carry three candidates of four, and in each the blue one is the lowest moon, the one a falling ball most often misses. Ul'dah, the lowest ring, keeps only its upper two. The compass keeps its top point and its heart.
- The cloud-edge candidate moved to (600, 202).
- Two ships' lights at sea, (500, 420) and (440, 470), are candidates.
- Result: 26 candidates; at most 9 in any 200 × 200 square (5 px scan); 5 left and 5 right below y 400. The greedy player wins **7 of 48**, with a mean of 3.3 oranges left.

**The minors on the approved levels.** All were taken.

| Level | Change | Greedy wins / 48 |
|---|---|---|
| base-p2 | Heart #7 to (209, 328). The two highest spire tips are removed (the art carries them). | 13 |
| base-p3 | Pom-pom candidates only at y 150 and below. The sky peg (700, 160) is removed. The forest rows start at x 100. | 9 |
| exp-p1 | Clouds at (150, 140), (210, 132) and (690, 140); one fewer cloud. | 9 |
| exp-p2 | Field stars r 9. No field star above y 138. Field candidates at y 190 and below. The sea glints ride a long swell (amplitude 9). | 9 |
| exp-p3 | Unchanged (Nits only). | 7 |

The base-p3 lattice and the exp-p3 hollow were left as they are (Nits).
