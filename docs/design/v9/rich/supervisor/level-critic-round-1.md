# Level-design critic, round 1 (verbatim)

An independent agent judged the six pilot levels with the shipped loader and engine (mfcheck, plus its own harness in the session scratchpad) and the composite renders. Its report follows unchanged; the designer's response is at the end.

---

# Moonfall pilot levels: level-design critic report (5 Oct 2026)

**Two of the six levels can go to the owner: base-p3 and exp-p3. The other four need changes.**

There are two problems that keep coming back, and both can be measured:

- **Oranges near the top of the board.** Seven orange candidates sit near the top, beside or above the launcher. No direct flight can touch them, and a 300-game random player lit some of them in under 4% of games. They are why the greedy player wins **0 of 24** games on base-p2, exp-p1 and exp-p2. The shipped levels win 22 of 96.
- **Brick traps.** Two places hold the ball until the stuck rule fires. In base-p2 it is a dip in the ridge and a gap too narrow for the ball at the ridge's left end, against the wall. In exp-p1 it is a notch between two domes.

## How I measured

- The shipped loader through `mfcheck validate`.
- `mfcheck sweep` at 1° steps (seed 1) and 0.5° steps (seed 2).
- `mfcheck play` with 24 greedy games and 40 random games per level.
- My own harness, built against Tsukimichi.Core, in the scratch folder (`crit/`):
  - 300 random-angle full games per level (about 3,600 shots), logging where the ball sits when the stuck rule fires and how often each piece is lit;
  - the greedy player again, logging which oranges are left when a game is lost;
  - single-shot traces.
- Python scripts in the same folder:
  - `direct.py`: which candidates any first free flight (0.1° steps) can touch;
  - `dist.py`: how the oranges are spread;
  - `contrast.py`: peg-against-backdrop luminance contrast on the composites at 1× and 0.8×.
- I looked at every composite at 1×, at 0.8× (640×480) and in 2× crops.

---

## base-p1 "The Airship Road": **CHANGES**

1. **Major: the trail is not drawn by pegs.**
   - **Evidence:**
     - Only **5** trail pegs (r 9) lie along the route: #30 (217,467), #31 (247,478), #32 (320,425), #33 (307,267) and #34 (486,253).
     - The route's line between the stops is carried almost entirely by the faint painted dashes, which nearly disappear at 0.8×.
     - 31 of the 74 pegs are scenery filler that has nothing to do with the route: sea 15, cloud edge 8, northern sea 5, coast 4. They outnumber the trail six to one.
     - A player sees "a map with orange rings on the cities", not "a route".
   - **Fix:**
     - Resample the route at about 34 apart, and let the dots run into each ring's gap. The current keep-out is ring radius + 14 = 43, which deletes most of them; cut it to about 20.
     - Remove most of the sea and cloud-edge filler, so the dotted line is the main stroke on the board.
2. **Major: the oranges are not spread (the method's own checks fail).**
   - **Evidence:**
     - There are **0** candidates below y 400 on the right half. Lower-left holds 13 candidates and lower-right 4.
     - One 200×200 square, x 240–440 and y 145–345, holds **14** candidates; the limit is 10.
   - **Fix:** give the lower right a stop or meaningful candidates. Either add a seventh stop there, or mark the sea-lane lights (592,526), (640,506) and (686,486) `canBeOrange`. Then take two candidates off the clustered northern rings.
3. **Minor: two rings are frequent last oranges.**
   - **Evidence:** in lost greedy games, the Limsa ring (#2, #3) and the Ul'dah ring (#5–#9) are still orange in 5 of 18 losses each. The level is still winnable: 6 of 24, the same as the shipped levels.
   - **Fix:** none required. Watch it after fix 1 changes the routing.
4. **Minor: three "northern sea" pegs are almost dead.**
   - **Evidence:** #54 (110,150), #55 (146,136) and #56 (184,132) are lit in only 59, 71 and 113 of 300 random games.
   - **Fix:** remove them, or move them below y 160.
5. **Nit: the painted route line is too thin.** At 0.8× it is at the edge of visibility. Thicken it to about 2 px at 1×.

Readability is fine: the lowest contrast ratio is 2.9, and purple is 5.9 or more.

## base-p2 "The Holy See": **CHANGES**

1. **Major: the ridge traps the ball.**
   - **Evidence:** in random play the stuck rule fires on **2.5%** of all shots. The shipped levels fire on 0–1.2%. 46 of the 90 events are at the ridge's left end, and the ridge's two dips (point 2 below) account for most of the rest.
     - Brick #79 starts at x 88, half-thickness 6. That leaves a **6.5 px** gap to the wall, narrower than the ball. A trace at −85° shows the ball wedged at (81.5, 155.4), and the stuck rule clears #79 there. Across 300 games the rule cleared #79 35 times.
     - The polyline (88,166) → (113,172) → (138,165) dips at (113,172), so the continuous bricks form a **cup** there. The stuck rule cleared #80 18 times. The method itself says "Never a cup."
     - A second dip at (236,186) catches the ball at about (240,170). The rule cleared #85 there 6 times.
   - **Fix:**
     - Start the ridge at x 81.5 or less, so it touches the wall, or at x 100 or more.
     - Remove the two local minima so each slope is monotone. For example, drop (113,172) and (236,186), or lift them above their neighbours.
2. **Major: two spire tips are effectively unreachable, and the level is never won.**
   - **Evidence:**
     - No direct flight can touch candidates #26 (549,58) and #28 (588,86). They are lit in only **7 and 9 of 300** random games.
     - They are still orange in **20 and 19 of the 24** lost greedy games.
     - The greedy player wins **0 of 24**. Shipped levels win 2–8 of 24.
     - #0 (162,119), a moon over a summit, is also out of direct reach (left in 4 games).
   - **Fix:** take `canBeOrange` off #26 and #28 and keep the spire candidates at y 100 or below on the screen. The rest of the outline then still reads as the spires.
3. **Major: the oranges are not spread (the method's own checks fail).**
   - **Evidence:**
     - There are **0** candidates below y 400 on the right half.
     - A 200×200 square at x 380–580, y 205–405 holds **11**, almost all of them the rose window's 9.
   - **Fix:** make two or three of the cathedral-foot pegs (480–660, 478–506) candidates, and make only every other rose-window peg a candidate.
4. **Minor: the pegs under the ridge are shielded.**
   - **Evidence:**
     - The 1° sweep's first shots never reach 10 pieces. The snowline pegs #3, #4, #5 and #7 lie under the ridge bricks.
     - The high-cloud pegs #8–#12, at y 72–110, are lit in only 27–71 of 300 random games.
   - **Fix:** drop the snowline pegs or move them out from under the crest. Lower the high-cloud row to about y 130.
5. **Minor: the billow hearts have low contrast.**
   - **Evidence:** the orange billow hearts #14 (150,330) and #16 (392,336) sit on bright cloud. Their contrast ratio is **2.0**, the lowest in the level, though their hue still separates them at 0.8×.
   - **Fix:** a stronger veil (about 0.4) locally, or move the hearts onto the darker cloud troughs.

The level does read as its subject: the ridge line, the arches in the bridge and the cathedral's outline are all clear. With 100 pieces it is the densest pilot, at a median of 13 pegs per shot.

## base-p3 "The Moonlit Post": **APPROVE**

It reads instantly as a winged moogle: the even outline ring, the oranges on the pom-pom, ears and wings, and the eye. It plays within the shipped levels' range: the greedy player wins 4 of 24, and the stuck rule fires on 0.4% of random-play shots. The minor fixes below can go in without another review.

1. **Minor: one peg pinches the left wall.**
   - **Evidence:** #37 (89,404) is **3.5 px** from the wall. mfcheck counts 1 wall pinch, and in random play the stuck rule fired twice with the ball at (80,390).
   - **Fix:** move it to x 98 or more, or to x 85.5 so it touches the wall.
2. **Minor: two pom-pom oranges are at the launcher's height.**
   - **Evidence:**
     - No direct flight can touch #10 (572,83) or #11 (602,99).
     - #11 is the most common last orange: left in **11 of 20** lost greedy games. The next most common is left in 4.
   - **Fix:** take `canBeOrange` off #10 and #11, or move the pom-pom down about 25 units.
3. **Minor: two sky pegs are dead weight.**
   - **Evidence:** #44 (640,70) and #45 (684,104) are lit in 25 and 21 of 300 random games.
   - **Fix:** remove them.
4. **Nit: the lower part is a plain lattice.** Below y 404 the board is a generic lattice: the treetops row plus three rows of filler, with only 4 candidates in the lowest third. The painted forest crowns could carry a wavy treeline instead of straight rows.

Nothing in the painting reads as a peg. The painted moon is clear of pegs, and the pom-pom is a dark fuzzy disc. The lowest contrast ratio is 2.8.

## exp-p1 "The Domes of Sharlayan": **CHANGES**

1. **Major: a notch between two domes traps the ball.**
   - **Evidence:**
     - The stuck rule fires on **23 of 171 first shots (13.5%)** at 1° steps and on 39 of 341 (11.4%) at 0.5°. The method's limit is 5%; the shipped levels range from 0 to 8.2%.
     - In random play, **50 of the 55** stuck events are at (490, 275).
     - The cause: the great dome's last crown brick ends at (488.4, 297.5), with centre (432,318), R 60, ending at 340°. The small dome's brick starts at (497.6, 286.5), with centre (521,300), R 27, starting at 210°. The gap between them is 14.3 − 8 = **6.3 px**, a V-notch.
     - A trace at 21° shows the ball resting at (491, 276) until the stuck rule clears #59.
   - **Fix:** do one of these:
     - move the small dome's centre to x 535 or more;
     - end the great dome's crown at about 325°;
     - join the two crowns into one continuous line.
     Any of them gives 14 px or more of clearance or no gap at all.
2. **Major: the oranges in the top row are almost unreachable, and the level is never won.**
   - **Evidence:**
     - No direct flight can touch candidates #33 (168,98) and #38 (648,96), both clouds, or #24 (660,146), a column.
     - They are lit in 39, 34 and 87 of 300 random games.
     - They are still orange in **20, 15 and 6 of the 24** lost greedy games.
     - The greedy player wins **0 of 24**, even though the average loss leaves only 2.6 oranges.
   - **Fix:** take `canBeOrange` off the cloud pegs and off #24. Use dome or ship pieces instead, or move the clouds below y 130.
3. **Major: the subject barely reads.**
   - **Evidence:**
     - Only 8 bricks (4 small arcs) draw the domes.
     - The statue's pegs (hood, back, train) and the columns (single pegs or pairs) form no shape a player would name. At 0.8× the board reads as scattered pegs with three orange arcs.
     - 22 of the 62 pieces are cloud or harbour filler.
     - With 62 pieces it is the sparsest pilot. The method targets 80–120.
     - The source is the kind of busy panorama that the method's subject test 2 warns against.
   - **Fix:**
     - Draw more of each dome: the full crown on the two small domes, plus dotted drum sides down to the roofline.
     - Give the great dome a second, lower course of bricks.
     - Draw the statue's silhouette as an 18-unit outline, the moogle technique, rather than three loose dots.
     - Cut the cloud filler.
4. **Minor: few oranges are low.**
   - **Evidence:** only 3 candidates lie below y 400 (1 left, 2 right).
   - **Fix:** fold this into fix 3.
5. **Minor: one finial has low contrast.**
   - **Evidence:** the blue finial #0 (432,231) sits on a pale roof, with a contrast ratio of **2.2**.
   - **Fix:** a slightly stronger veil there.

## exp-p2 "The Ferry in the Stars": **CHANGES**

It reads as its subject best of the six: a clear ferry constellation, with the atlas lines and the small r 7 dots still readable at 0.8×. It is also the cleanest physically: the stuck rule fires on 0.6% of first shots and 0.1% of random-play shots. One problem blocks it.

1. **Major: three field stars at the top edge are oranges no one can reach, and the level is never won.**
   - **Evidence:**
     - Farthest-point picking pushed field-star candidates into the corners: #33 (162,62), #34 (510,67) and #35 (631,78).
     - No direct flight can touch any of them. They are lit in **11, 56 and 26 of 300** random games.
     - They are still orange in **22, 12 and 17 of the 24** lost greedy games.
     - The greedy player wins **0 of 24**, with 4.7 oranges left on average; the shipped levels average 2.6.
     - The 1° sweep's first shots also never reach #33 or #35.
   - **Fix:** limit field-star candidates to y 130 or more (the direct-flight envelope), or drop these three and pick the farthest points again within that band.
2. **Minor: the sea is sparse.**
   - **Evidence:** the sea band holds 11 small pegs, and only 5 candidates sit in the lowest third.
   - **Fix:** make two or three sea reflections candidates; reflected bright stars would mean something.

## exp-p3 "The Sea of Sorrows": **APPROVE**

The orbiting ring reads at once and is a distinct technique. The mover is fair:

- 26 pegs at 42 px/s, about 21 px apart, so a ball always passes;
- the stuck rule fires on 0.1% of random-play shots;
- the spread is the best of the six: at most 6 candidates in any 200×200 square, and 2 left and 5 right below y 400.

The minor fixes below can go in without another review.

1. **Minor: pegs sit on the bright planet.**
   - **Evidence:**
     - The six "storm" pegs sit on the lit planet, which breaks the method's rule that a bright planet is where pegs need not go.
     - Orange #26 (470,300) has a contrast ratio of **1.9**, the lowest in all six composites. Its hue keeps it visible at 2× and 0.8×.
     - Unverified: I did not see a purple or green land on a storm peg.
   - **Fix:** keep the storms on the planet's darkest limb, or raise the veil to about 0.4 inside the planet's disc.
2. **Minor: one rock is the most common last orange.**
   - **Evidence:**
     - The greedy player wins only 2 of 24, the same as the hardest shipped level (base-02).
     - The top-right rock #48 (680,150) cannot be touched by a direct flight and is left in **11 of 22** losses.
   - **Fix:** take `canBeOrange` off #48, and make a lower belt rock a candidate.
3. **Minor: the oranges are always the same.**
   - **Evidence:** there are exactly **25** candidates, so every play has the same 25 oranges, against the method's "never solved by memory alone".
   - **Fix:** mark 3 to 5 more pieces as candidates, for example the low-field pegs and the rib's other pegs.

---

## Measurements

The greedy figures come from `mfcheck play` with 24 games at level 5; first-shot figures from the 1° sweep at seed 1; random-play figures from 300 games, about 3,600 shots.

| Level | Pieces (bricks) | Candidates | Most candidates in a 200×200 square | Candidates below y 400 (left, right) | Stuck rule, first shots | Stuck rule, random play | Pieces no first shot reaches | Candidates no direct flight can touch | Least-lit piece (share of random games) | Greedy wins / 24 | Mean oranges left (greedy) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| base-p1 | 74 (0) | 34 | **14** | 10, **0** | 1.8% | 0.6% | 0 | 0 | 0.20 | 6 | 3.0 |
| base-p2 | 100 (21) | 27 | **11** | 5, **0** | 4.1% | **2.5%** | **10** | 3 | **0.02** | **0** | 4.5 |
| base-p3 | 74 (0) | 29 | 10 | 3, 1 | 3.5% | 0.4% | 2 | 2 | 0.07 | 4 | 1.6 |
| exp-p1 | 62 (8) | 26 | 10 | 1, 2 | **13.5%** | 1.5% | 0 | 3 | 0.08 | **0** | 2.6 |
| exp-p2 | 67 (0) | 26 | 9 | 2, 3 | 0.6% | 0.1% | 2 | 3 | **0.04** | **0** | 4.7 |
| exp-p3 | 74 (0, 26 movers) | 25 | 6 | 2, 5 | 1.8% | 0.1% | 0 | 1 | 0.32 | 2 | 3.0 |
| base-01 (shipped) | 55 | 55 | 16 | 11, 11 | 0% | 0% | 0 | 0 | 0.59 | 6 | 2.5 |
| base-02 (shipped) | 57 | 57 | 16 | 11, 12 | 2.9% | 0.7% | 0 | 0 | 0.35 | 2 | 4.3 |
| base-03 (shipped) | 65 | 65 | 16 | 15, 14 | 8.2% | 1.1% | 0 | 0 | 0.51 | 8 | 1.4 |
| base-04 (shipped) | 72 | 65 | 18 | 13, 15 | 2.9% | 1.2% | 0 | 0 | 0.62 | 6 | 2.0 |

All six levels pass the loader. Each has at least 25 candidates. The launcher zone and the bucket zone are clear (the lowest piece is at y 548). Median pegs per first shot range from 8 to 13; the shipped levels range from 6 to 12. Each pilot's technique is distinct from the other five, though base-p3 and exp-p3 are both "a ring round a central figure" at a glance (Nit).

**Outside my verdict (engine):** at the bottom of the right wall, when the bucket is at its right extreme, the ball chatters between the wall and the bucket's rim: up to 64 wall bounces near y 599 before it is lost. base-p1 shows it at 0°, exp-p1 at 3° and 7°, and the shipped base-04 three times. The bucket's outer rim reaches past the wall (401.5 + 260 + 65.5 ≈ 727 > 724.5). It is worth a separate engine check.

OVERALL: CHANGES. base-p1, base-p2, exp-p1 and exp-p2 need changes; base-p3 and exp-p3 are approved.

---

## Designer's response (round 2 changes)

Every Major and Minor was taken. The pre-flight (`src/layout.py` `check()`) now enforces the critic's measurements, so they cannot regress:
- every orange candidate must be touched by some first free flight;
- no two bricks may leave a notch narrower than a ball (3.5 to 12.5 px);
- spread: at most 10 candidates in any 200 × 200 square, and candidates below y 400 on both halves;
- no wall pinches (0.5 to 12.5 px).

- **base-p1.** The stops are rings of four, turned so the route runs through two opposite gaps. The trail is 14 dots every 30 units along the whole route (r 8), and the route now continues east over the Rhotano Sea. Filler is cut: the northern sea is gone and the cloud edge is down to 4. The land between the stops carries scenery pegs. Three ships' lanterns on the southern sea lane are candidates, so the lower right always holds oranges. The three crowded northern rings carry three candidates of four. The painted route line is 2.2 wide.
- **base-p2.** The ridge starts at x 100, 18 px from the wall, and is monotone on both sides of the summit, so there is no cup. The snowline pegs are gone, and the high cloud is three pegs placed lower. The spire candidates above y 100 and the right spire are blue. Every other rose-window peg is a candidate, and the cathedral foot's upper row carries three. The billow hearts moved from under the humps into the troughs between them. Veil 0.36.
- **base-p3.** #37 moved off the wall. Nothing at the launcher's height is a candidate. The two dead sky pegs are gone. The lower board is now a wavy treeline over three staggered rows of forest crowns, with cottage lamps as candidates (27 candidates in all). The eye is a blue moon (the realism Nit).
- **exp-p1.** The great dome's crown runs 208–315° and the east dome's 200–330°, so both notches are wider than a ball. The stuck rule on first shots went from 23 of 171 to 1 of 171. The clouds and the tall column are blue. The domes have full crowns, a lower course and drum sides. The statue is an even dotted outline of 11 pegs from hood to train. Three harbour lanterns are candidates. Veil 0.36.
- **exp-p2.** Field-star candidates sit only within reach (y ≥ 140, 110 < x < 670), 20 of them. Two staggered rows of sea glints hold three candidates. Sky stars are r 10 and line dots r 8.
- **exp-p3.** The storm pegs moved to the planet's night-side limb. #48 is blue. There are 28 candidates (the low field and the rib).
- **Engine note** (the bucket rim chatters at the right wall): it goes to the owner as an open question, because plugin code is outside this task.

Measured after the changes (mfcheck; greedy player, 48 games at level 5):

| Level | Pieces | Candidates | Stuck, first shots | Greedy wins / 48 |
|---|---|---|---|---|
| base-p1 | 84 | 27 | 1 / 171 | 2 |
| base-p2 | 91 | 26 | 5 / 171 | 6 |
| base-p3 | 102 | 27 | 5 / 171 | 5 |
| exp-p1 | 68 | 29 | 1 / 171 | 11 |
| exp-p2 | 106 | 35 | 1 / 171 | 9 |
| exp-p3 | 72 | 28 | 3 / 171 | 7 |
