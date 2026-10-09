VERDICT: APPROVE (0 blockers, 0 majors, 1 minor)

| id | severity | level | issue | evidence |
|---|---|---|---|---|
| m1 | Minor | base-20 4-5 | What is left of round 2's M1 east-third point. At 640 the Thal ghost under the two east arcs is only faint mauve. On the cleared board it reads as a large pale "UL" watermark, not as streets. The arcs no longer sit over nothing, but the streets are still hard to see. | In `engine-640`, crop x 380-600 and y 220-360 at 3x: the two pale bands under the arcs are just visible. `cleared-1280` (800-1000, 440-700) shows a "UL"-like glyph. The response leaves the "drop the inner arc" option undone. |
| n1 | Nit | base-17 4-2 | The pack's 4-2 renders still show Ace 250,000. The committed `aces.json` and a fresh render both show 260,000. | Pack `engine-1280` vs my `MoonfallRender --moment start --cache` render: AE 3 px, the Ace label only. Every peg and every piece of art is the same. |
| n2 | Nit | pack | The "Integration changes" note in `index.md` gives 4-4 as "23.3 held out, 22.18 pooled". The stage table and `stage-mr4.txt` both give 22.1 / 21.95. | `review/mr4/index.md`, the "Integration changes" paragraph. |
| n3 | Nit | base-20 4-5 | Plum turns to umber through a saturated crimson band. It reads more as a heat glow than as sand. That is a taste note and works as Ul'dah at dusk. | `cleared-1280` (640,640) is rgb(95,39,35), hue about 4°. Below it, (640,700) is rgb(100,69,21), hue about 38°. |

## Round-2 findings

| R2 id | Status | Check |
|---|---|---|
| M1 (4-5 sands don't read) | **Fixed; residual → m1** | **The sands:** at the foot the field is now warm umber and ochre. (640,700) is rgb(100,69,21) and (900,700) is rgb(91,62,26), where round 2 measured plum at hue 320°. **The dunes:** warm dark sand, rgb(52,34,23) and (39,24,15), with a ragged lit crest that survives 640. The city's south lobe now visibly sits on the desert. **The east third:** improved but still faint (m1). |
| m1 (4-2 east third rows) | **Fixed** | `trace.png`: the lens is now an open chevron from the spire's flank (380,330). Its top rises to (700,248) and its underside falls to (700,404). The horizon is a low crest. The right half reads as a tongue widening east, under the airship's keel diagonal. |
| m2 (4-2 sand box) | **Fixed** | `cleared-1280` crop (500-1060, 500-760): the ochre feathers up from the horizon and reaches the right wall, and the teal strip is gone. The far dome no longer sits on a straight cut. Its upper half is still the painting's hazy grey, but that reads as a dome in the haze, not a mask edge. |
| m3 (4-1 blur patches) | **Fixed** | The `clone` boxes carry the map's own hatching through all three places. At (270,340), (400,395) and (710,290) the board now shows hatched voids and parchment, with no blur. A clone box clips the "Ul'dah - Steps of Nald" label, as the arrow did before, and it reads as part of the map. |
| m4 (ramp unverifiable) | **Fixed** | `stage-mr4.txt` is current: 26.68 / 25.30 / 23.55 / 21.95 / 17.88 pooled. The steps are 1.38, 1.75 and 1.60, and the finale gap is 4.07 ± 0.40. That is within one standard error of the 3.5-4 I set as the condition, so accepted. The mean is 23.07 against the charter's 23.1. |
| n1 (4-1 one brick) | **Accepted** | The calm opener is by design, and a brick run would move the 4-1 → 4-2 step. Closed. |
| n2 (four techniques; shared roads) | **Accepted** | The charter allows four. 4-5's roads now run out into the umber, which separates them from 4-1's roads, which end on the night field. |
| n3 (4-5 green sand star) | **Fixed** | `engine-1280` (280,395) is blue. The east star (1000,278) is still allowed to be green. |
| n4 (no round-1 response) | **Fixed** | `response-mr4-round-2.md` records both rounds. |

**Regressions:** none found.
- My fresh 1280 renders of 4-1 and 4-5 match the pack: AE 0 for 4-1, and 111 px of ambient motion for 4-5. For 4-2 only the Ace label differs (n1).
- In the pack renders, 4-3 and 4-4 are unchanged in read: the lamps still lead 4-3, and the arena is still clean at 640.
- The `git status` of the worktree is clean, and everything is committed.

## Per-level verdicts

**4-1 base-16 The Steps of Thal: PASS.**
- The plan is now clean. The cloned hatching is the map's own, so nothing reads as an edit at 1280 or 640.
- It is still the calm opener: the Gold Court hub, two dashed arrow routes, and the blue and red grade.
- One brick, accepted.

**4-2 base-17 The Spire of the Sands: PASS.**
- The subject is complete: the spire flanked by moons, the red airship with its dotted keel, a lens that opens east from the spire, a dune-crest horizon, and an ochre floor wall to wall.
- The east third now has a shape: a converging tongue plus a crest, not five ruled rows.
- The pack's Ace label is stale (n1). It is cosmetic.

**4-3 base-18 Lamps on the Steps: PASS.**
- No change in read. It is the stage's prettiest board: warm flames in open rings, a staircase of lights at 640.
- The cleared render is now a real cleared board.

**4-4 base-19 The Coliseum: PASS.**
- It is still the stage's spectacle: 31 bricks, tiers, spokes and a closed east ring.
- It is clean at 640.

**4-5 base-20 Ul'dah from the Sands: PASS (m1, n3).**
- The finale finally reads as its name: a city plan on a night sky that turns to warm desert at its foot, with sand dunes holding the lower corners.
- The brick crowns, ramparts and orange segments from round 1 make it the stage's second-strongest board, and the hardest one.
- For m1, the cheapest fix is to drop the inner arc. That also eases the finale gap from 4.07 toward the charter's 3.5-4, so it helps the ramp too. Re-check stuck and the 4-4 → 4-5 gap if you do it.
- Otherwise the owner can accept the faint ghost as atmosphere. It does not block.

## The stage as a set
- **Theme.** The walk now reads end to end: the Gate of Thal, the spire under the airship, up the lit steps, into the Coliseum, then out past the walls to the sands. 4-5's umber foot answers 4-2's ochre floor, so "Sunlit Steps" begins and ends with the desert.
- **Variety.**
  - The palettes are distinct: blue-red, teal-ochre, indigo-gold, blue-green, and plum to umber.
  - The three city plans each have their own move: a route walk, an arena of rings, a wall in brick.
  - Four techniques, as the charter allows.
- **Ramp.** 26.68 → 17.88 pooled, every step positive and clear of noise. The finale bites at 4.07. That is the steepest drop in the stage, and right for a finale wall board.
- **Spectacle arc.** Bricks run 1, 4, 2, 31, 15. 4-4 is the peak and 4-5 a strong closer, the same shape as MR 3's accepted stage.
- **Overall.** It meets MR 3's bar. Each board looks rich and reads as its subject at both sizes, and the stage ends on a board that finally looks like its title.

Reviewed:
- The committed pack in `C:\Users\devon\Desktop\Tsukimichi (Main Repo)\.claude\worktrees\wf_2529e0f6-97c-6\docs\design\v9\levels\review\mr4\` at cca6c2c5.
- Fresh renders of base-16, 17 and 20, made with the built `Tsukimichi.MoonfallRender.exe --cache`, in `C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\`.

Unverified: the flicker, glints and mist in motion, and how the new `clone` part looks in-game against the live install's map texture.
