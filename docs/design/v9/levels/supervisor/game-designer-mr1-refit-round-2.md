VERDICT: APPROVE (0 blockers, 0 majors, 6 minors)

| id | sev | level | issue | evidence |
|---|---|---|---|---|
| R1-M1 | resolved (now m1) | 1-3 | The stuck rule now fires 98 times, down from 154. First-shot rests on the east crowns fell from 13 to 2. | report `play.stuck_fires 98`; `mfl.py stuck base-03`: 6 first-shot rests, only (655,295) and (675,300) in the east |
| R1-m1 | resolved | 1-2 | The stuck rule now fires 25 times, down from 80. First-shot rests fell from 20 to 3. | report `stuck_fires 25`, `sweep.stuck 3`; `base-02.py:76` `trim1=45` |
| R1-m2 | resolved | 1-2 | The water tower reads as a tower again: a dome over the painted tank, on two stilts. The "A ∩" letter pair is gone. | `base-02-composite.png` and `base-02-engine-1280.png` around (540, 300); `base-02.py:71` |
| m1 | Minor | 1-3 | The stuck rule fires 98 times. That is only 2 under the FAIL of 100, above the shipped maximum of 88, and 4 to 10 times the rest of the stage (10-25). The response says a summit moon sits in each gap. In fact no summit moon was placed. The 44-unit gap leaves the summit only 22 units from the brick end, so the place step skipped both moons without a warning. The east crowns are just shortened: the mid crown now ends at x 663 and the far crown at x 686. | `json/base-03.json`: the east arcs end at 264.7° and 266.8°; no peg within 40 units of (685,385) or (708,317); `base-03.py:117-118,134-135`; precheck `play` WARN |
| m2 | Minor | 1-1 | 1-1, the game's very first level, was re-tuned harder although no finding asked for it. Held-out wins went from 8.4 to 7.9 per 48 (5 of 48 rule games). It is now the hardest of the four non-finale levels (1-2 8.7, 1-3 9.1, 1-4 8.3). | commit 8a2c213e "re-tuned to hold stage-1 difficulty"; `base-01.py` TUNING; `status` |
| m3 | Minor | stage | The stage has become light on bricks. 1-2 went from 34% to 28.6% when the tank-side and road bricks came out, and 1-3 went from 30.6% to 29.9%. Three of five boards are now under the 30-45% guidance (1-1's 22% was already accepted). | report `preflight.brick_share`: 0.224 / 0.286 / 0.299 / – / 0.316; at 98e376f0, 1-2 was 0.34 |
| m4 | Minor (carried, deferred) | stage | Three boards still open with two arcs bracketing the launcher. This is deferred to the owner's taste review, and I accept the deferral. | `sheet.png` top row, 1-1, 1-2, 1-5 |
| m5 | Minor (carried, deferred) | 1-3 | The moonbeam bricks still match no painted beam. Accepted as polish. | `base-03.py:112-113` |
| m6 | Minor (carried, deferred) | 1-3 | 45 of 97 pieces (46%) still trace no feature. Accepted to hold the tune. | precheck `patches` WARN |
| n1 | Nit | pack | The engine renders show the Ace values from before the fix: 1-1 190k (aces.json says 200k), 1-3 220k (210k), 1-5 180k (190k). The pack therefore does not show what ships. | crops of the Ace chip in `base-01/03/05-engine-1280.png`; `Modes/aces.json:8-12` |
| n2 | Nit | pack | `status` marks convert "stale" on 1-1, 1-2 and 1-5. The runtime level files are byte-identical to the pipeline json (I checked with `cmp`). The flag is probably the git-order heuristic tripping on a scene convert that wrote nothing new, but I did not check the scene recipes. | `status`; `mflkit/review.py:163-177` |
| n3 | Nit | 1-2 | The two new tank moons sit right of centre, at x 539 and 577 in a tank that spans about 497-585, so the left third of the tank is empty. | `base-02.py:45` `STARS_EXTRA` (540,300), (578,304) |
| n4 | Nit | 1-5 | The crystal's core column is now always blue, so the board's centrepiece is never a target. That is acceptable, because the side bars, crown and points still deal orange. The F9 protan minimum still sits on that column (0.098 at the brick at 440,332); that is UX's call. | `base-05.py:45`; precheck `margins` |

## Stage verdict

**Rules: all still met.**
- `mfl.py check` passes on base-01, 02, 03 and 05, and builds are fresh.
- Pieces are 95-105 per level; orange candidates are 60-66 (61-68% of pieces).
- The lower third holds at most 27% of candidates on every level.
- Held-out wins per 48 are 7.9, 8.7, 9.1, 8.3 and 5.3. The stage mean of 7.86 is inside the band of 7.47-9.67, but its margin to the lower edge shrank from 0.59 to 0.39.
- The finale's 5.3 sits 3.2 below the other four's mean of 8.5, more clearly resolved than in round 1 (2.8).
- Rule games are 4-8 of 48 per level, above the floor of 2.
- Aces were re-suggested.
- Precheck has 0 FAIL and 19 WARN, the same count as before; the 1-3 stuck FAIL is now a WARN.

**Curve and pacing.** The overall shape is still Peggle's: four levels near each other, then a clear drop for the finale.
- **What changed:** the order inside the plateau inverted. In round 1, 1-1 was mid-pack. Now it is the hardest of the four, and 1-3 is the easiest at the top of its tune band.
- **How sure:** the gap from 1-1 to 1-3 is 1.2 per 48, about 1.5-2 standard errors, so it is not certain. It is the wrong direction for the first board a new player sees, and nothing asked for it (m2).
- **Suggested fix:** restore 1-1's round-1 TUNING (8.4 held-out, which was approved). The stage mean then rises to about 7.96, which moves it away from the band's lower edge.

**Theme, variety, spectacle.** These are unchanged in substance. The Waking Sands story, the five different techniques and the lavish look all hold. The one stage-level drift is fewer bricks (m3). The 1-2 tank and road trims took bricks out and put moons back, so the stage now leans more on moons than at round 1. That costs a little of the "real structure in brick" that made the refit feel like Peggle.

## Per level

**1-1 Road to Horizon (base-01): APPROVE, with m2.** No pieces moved; only the dealt colours changed.
- Six candidates moved from the south-east road stops to the north-west and the hill-crown ends. The lower third fell from 18% to 15%.
- The map and the road read the same as before.
- 47% of lost games end within 2 oranges, down from 61%, so the end game is slightly less of a nail-biter. That is still fine.
- The only concern is where the level now sits in the curve (m2).

**1-2 Horizon by Night (base-02): APPROVE.** Both round-1 findings are fixed.
- The tower now shows the painted domed tank under the brick crown. The two orange stilts read as legs, and two moons sit in the tank (slightly off-centre, n3). Next to the derrick it now reads as a tower, not a letter.
- The trimmed upper road legs remove the pocket under the derrick's feet: 25 fires, the second-lowest in the stage.
- The added corner and arch-shoulder stars keep the sky full. The peg moved out from under the Ace chip, as UX asked.
- Difficulty barely moved (9.0 to 8.7).
- Side effect: the brick share fell below 30% (m3). Two short bricks would bring it back without touching the tank, for example along the cliff ledge under the roofline at y 462, or one more on each lower switchback leg.

**1-3 The Cactuar (base-03): APPROVE, with m1.** The major is resolved: what the player feels as a stall at the east star cluster is mostly gone. First-shot rests in the east fell from 13 to 2, and the sweep shows 6 stuck aims in 777.
- **What remains:**
    - 98 fires sit 2 under the FAIL line and above every shipped level, so a little seed drift would put it back over.
    - The fix that shipped is not the one the response describes. Each east crown is now a single shoulder brick with nothing at its summit.
    - On the board the east dunes read as short bars on the ridges, while the west dunes have open crests with a summit moon. The asymmetry is visible at 640 but small.
- **Suggested fix:** give the east crowns the west crowns' 70-unit gap so the summit moons fit (as `base-03.py:134-135` already intends). This matches the west read and gives the stuck east crests a clearable piece. Then run `check` and re-tune, because it will play easier still.
- The cactuar figure, which carries the level, is untouched and still the stage's best read.
- The holdouts are still the east star cluster (16%, 14%, 13%, 13%), so the difficulty lever is intact.

**1-4 The Gilded Dome (base-04):** unchanged, not re-reviewed.

**1-5 The Crystal's Call (base-05): APPROVE.**
- Only candidacy changed: the four core-column pieces left the orange pool (n4).
- The finale got harder (5.8 to 5.3) and now separates more clearly from the plateau. 73% of lost games end within 2 oranges, so it ends tense rather than hopeless, which is the right feel for a finale.
- The sheet's red "-2.2 margin" is the same display matter as round-1 n2: the band applies to the stage mean, not to one level.

## Method
- Read the round-1 findings and the response.
- Read `git diff 98e376f0..b37496f0` for the layouts, `json/base-05.json` and `convert.py`.
- Viewed the four diff images, the base-02 and base-03 engine renders at 1280 and 640, a crop of the base-02 composite, Ace chip crops from the 1-1, 1-3 and 1-5 renders, and `sheet.png`.
- Ran:
    - `mfl.py status --stage mr1`
    - `mfl.py check` on base-01, 02, 03 and 05 (all PASS)
    - `mfl.py stuck base-03`
    - `mfl.py precheck` on all five (0 FAIL, 19 WARN)
    - `cmp` of the pipeline json against the runtime level files (all five identical)
    - a json query of the east crown arcs and summit pegs, at the current commit and at 98e376f0
- Edited nothing. `git status` is clean apart from precheck's crops in the gitignored build folder.
- Not verified:
    - hand play in the game
    - where mid-game stuck rests fall on 1-3 (`stuck` covers first shots only)
    - whether the runtime scene recipes for 1-1, 1-2 and 1-5 are current (n2)
    - the Ace values as built into the shipped dll (n1)
