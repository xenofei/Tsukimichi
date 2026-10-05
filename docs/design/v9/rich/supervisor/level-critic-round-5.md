# Level-design critic, round 5: readability after the regrade

5 October 2026. Composites checked: `composites/*.png`, round-5 commit 52c237fc. Verdict: **APPROVE**, all six levels.

## What changed since round 4

- The layouts are unchanged. Since round 4 the only change to a level file is in `levels/base-p3.json`: a `canBeOrange` swap between two treetops, which is the round-4 fix for #47.
- I rebuilt every level from its script (read-only): all six files match, and every pre-flight returns no problems.

## How I measured

- The composites at 1× and downscaled to 0.8× (640×480, Lanczos).
- For every peg, including the orbit movers at the positions the composite draws, I measured two luminances:
  - the face: mean relative luminance within 0.6 r;
  - the backdrop: an annulus from r + 2 to r + 9 px, with neighbouring pegs removed.
- The contrast ratio is (lighter + 0.05) / (darker + 0.05).
- **One change from round 4.** Each peg's kind is now read from the pixels the composite draws, not from mfcheck's colour pick for seed 1, because the composites do not use that seed's colours. For example, (432,231) in exp-p1 is drawn orange, and round 4 labelled it blue. The overall minimum does not depend on the labels, so round 4's minimums stand. Only its per-kind labels could be off.
- **Worst case for purple and green.** Either can be dealt to any blue peg. So I also put each kind's median face luminance against the backdrop of every peg that can never be orange, and report the lowest result.

## Results

All values are contrast ratios. The worst cases are the kind placed on the brightest blue-peg backdrop.

| Level | Lowest of any peg, 1× | Lowest of any peg, 0.8× | Purple as drawn (1×) | Purple, worst case (1×, 0.8×) | Green as drawn (1×) | Green, worst case (1×, 0.8×) | Lowest in round 4 (1×, 0.8×) |
|---|---|---|---|---|---|---|---|
| base-p1 | 3.1 (orange, 600,202) | 3.1 | 3.7 | 2.6, 2.6 | 5.9 | 3.9, 3.8 | 2.7, 2.7 |
| base-p2 | 2.7 (orange, 209,328) | 2.6 | 3.1 | 2.3, 2.2 | 4.5 | 3.3, 3.2 | 2.4, 2.4 |
| base-p3 | 3.3 (orange, 274,176) | 3.1 | 4.5 | 2.6, 2.5 | 6.0 | 3.8, 3.7 | 3.3, 3.1 |
| exp-p1 | 2.8 (orange, 432,231) | 2.7 | 3.2 | 2.6, 2.5 | 3.8 | 3.9, 3.8 | 2.6, 2.5 |
| exp-p2 | 3.0 (orange, 372,304) | 3.0 | 3.9 | 2.7, 2.6 | 4.0 | 3.8, 3.7 | 3.0, 3.0 |
| exp-p3 | 3.4 (orange, 598,300) | 3.3 | 5.0 | 3.0, 2.9 | 7.3 | 4.4, 4.2 | 3.3, 3.2 |

- **The lowest contrast is 2.6**, base-p2's billow heart at 0.8×. It was 2.4 in round 4 and 1.9 in round 1. Every level is as good as or better than in round 4.
- **Purple's worst case** is 2.2, base-p2 at 0.8× on the cloud bank at (714,292).
  - I checked it by eye: I pasted the drawn purple sprite onto the brightest blue-peg backdrop in five levels (base-p1, base-p2, base-p3, exp-p1, exp-p2) and enlarged the crops 2×.
  - It reads as purple in every one. Its hue is clearly apart from the blue-grey ground, and it keeps its dark rim.
  - Purple is the darkest kind, so this is the case the method says to watch, and it holds.
- **Green** is 3.7 or more as drawn and 3.2 or more in the worst case.
- **At 0.8×**, every level still reads as its subject. The regrade's restored fine detail does not produce anything that looks like a peg. The bucket lanterns sit clear of the pegs.

## Play check on base-p3's candidate swap

`mfcheck play`, 96 games at level 5: the greedy player wins 16 (17%), up from 13 in round 4. The shipped levels win 10–27%.

## Verdict

APPROVE: base-p1, base-p2, base-p3, exp-p1, exp-p2, exp-p3. No new issues.

Still not verified:
- in-game rendering, as opposed to the composites;
- purples and greens dealt by seeds other than the composites' own, beyond the worst-case bound above;
- whether the scene grades are realistic, which is the realism supervisor's call;
- the engine's bucket-rim chatter at the right wall, which stays with the owner.

OVERALL: APPROVE
