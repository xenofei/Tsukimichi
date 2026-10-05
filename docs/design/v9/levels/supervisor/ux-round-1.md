# UX/UI specialist, levels round 1

Reviewed: the ten levels base-01 to base-10 in worktree `agent-a570460c913ca1c79`, judged against level-method §5 and §8 (F5, F6, F9) and against the six approved pilots.

## Method

- **What I looked at.**
  - All ten 1x composites, and the six pilot composites for comparison.
  - 2x crops of the places that carry risk: the 2-2 glitter path, the 1-5 aurora greens, the 1-3 teal field, the 1-4 sky and lower town, the 1-2 face, the 2-3 vortex ring, the 2-4 islands, the lamp posts on 2-1 and 2-2, and the 2-5 fireflies.
  - 0.8x crops, made by a Lanczos downscale of the @2x composites to 640×480 and enlarged with point sampling.
  - The undressed composites, and the piece-free ("cleared") boards of 1-3, 1-4 and 2-3, with base-p1 and exp-p3 rendered the same way for comparison.
- **Re-measured independently** (scripts in `scratchpad/ux/`; nothing in the repo was run or changed, and `build` was not run). I rebuilt each piece-free board with `mflkit.composite.render(..., gone=all)` from the committed dressed @2x scene, using the engine's own deal (mfcheck `colours`, at the recorded seed).
  - **F6.** I measured faces at each scale, rather than reusing the 1x faces at 0.8x. I took the ring's 90th percentile and also its 99th, to catch glints. I covered every position each kind can be dealt to, with movers at 24 points along their path.
  - **CVD.** I used Machado 2009 at severity 1, protan and deutan.
    - For each kind, I took the OKLab a/b distance from its core to the ground 12–20 px round it.
    - I measured this at every position the kind can be dealt to (all orange candidates, movers included), not only at the 25 the engine dealt.
    - I ran the same measure on the pilots.
  - **Bright, peg-sized spots.** I looked for patches in the piece-free board brighter than 0.40 luma and 3–26 px across.
  - **Halo or ghost visibility.** For isolated pegs, I compared the OKLab ΔE between the ground 5–12 units from the piece and the open ground 32–48 units out. I ran this on the ten levels and the six pilots.
- **Was the undressed board a sound reference?** I judged this for F6 and for F9 separately (see M1).

## Verdicts

| Level | F6 worst (pipeline / mine) | Protan orange p10 dealt / min, all candidates | Ghost-disc ΔE (pilots 0.019–0.066) | Verdict |
|---|---|---|---|---|
| 1-1 Road to Horizon | 0.306 / 0.333 | **0.091 / 0.089** | n/a (no isolated pegs) | **REVISE** (M1) |
| 1-2 The Cactuar | 0.289 / 0.329 | 0.101 / 0.082 | 0.046 | APPROVE (m2) |
| 1-3 Ul'dah Across the Sands | 0.240 / 0.258 | 0.116 / 0.114 | 0.071 | APPROVE (m1) |
| 1-4 The Gilded Dome | 0.234 / 0.253 | 0.132 / 0.127 | 0.080 | APPROVE (m1) |
| 1-5 The Crystal's Call | 0.353 / 0.382 | 0.146 / 0.131 | 0.090 | APPROVE (m1) |
| 2-1 Limsa Across the Water | 0.302 / 0.374* | 0.131 / 0.119 | 0.059 | APPROVE (n1) |
| 2-2 Moonpath on the Bay | 0.330 / 0.368 | 0.155 / 0.153 | 0.059 | APPROVE (n1) |
| 2-3 The Kraken's Sea | 0.324 / 0.342 | 0.121 / 0.117 | 0.079 | APPROVE (m1) |
| 2-4 The Strait of Merlthor | 0.298 / 0.322 | 0.121 / 0.118 | 0.050 | APPROVE |
| 2-5 Twin Lanterns | 0.380 / 0.408 | 0.165 / 0.134 | 0.048 | APPROVE |

\*On 2-1 the deal put purple on a brick, so no still purple peg gave me a face. My figure there is orange's; the pipeline's 0.302 uses the pilot face.

**OVERALL: REVISE.** One Major remains, on 1-1. Everything else is Minor or Nit.

**What holds everywhere:**
- **Value.** Every kind clears the 0.20 margin at 1x and 0.8x at its worst placement. My re-measure is 0.03–0.04 higher than the pipeline's, because the pipeline samples the face disc half a pixel off-centre. Faces measured at 0.8x differ from the 1x faces by at most ±0.02, so reusing the 1x face at 0.8x hides nothing.
- **Glints.** Taking the ring's 99th percentile, no kind falls below 0.18 anywhere except beside the walls' gilt and the 1-1 route dashes. The overlay line is exempt by the rule.
- **Green and purple against the ground.** In normal, protan and deutan vision, both sit inside the approved pilots' range.
  - Green, normal vision: minimum 0.084–0.154 (pilots 0.077–0.132).
  - Purple, normal vision: minimum 0.053–0.090 (pilots 0.031–0.091).
  - At 0.8x, the greens on the teal fields of 1-3, 1-5, 2-3 and 2-4 still read as mint, clearly apart from blue.
- **2-2 Moonpath.** I checked the glitter path closely; the 14 pegs on it are fine.
  - The brightest ring 90th percentile round any of them is 0.223 at 1x and 0.224 at 0.8x.
  - The brightest glint pixel next to a path peg is 0.39, still 0.2 below the purple face.
  - In the crops, the veil turns the glitter into muted horizontal streaks that never look like pegs.
- **Movers.** The 2-3 orbit ring over the vortex and the 2-5 slides in the reflection hold their margins along the whole path.
- **Nothing reads as a peg.** The bright, peg-sized spots in the piece-free boards are all one of these:
  - the small lights that pass F5;
  - the lamp posts (n1);
  - the 1-1 route dashes;
  - the lacy 2-4 islands, which are bright (up to 0.64) but irregular, never round.

## Findings

### Major

**M1. 1-1: orange separation for protanopes is the lowest of any board, and it passes only on a reference that cannot fail it.**
- **Evidence.**
  - F9 p10 over the dealt oranges is 0.091. Over all 25 candidates my minimum is 0.089, at (267, 208) on the land.
  - The absolute floor is 0.12, and every approved pilot is between 0.101 and 0.141.
  - The undressed board scores 0.110, so the rule's other branch (undressed − 0.02 = 0.090) passes this by 0.001.
  - The dress costs 0.019 of separation. The same dress costs 0.016–0.019 of F6 margin at (267, 208) and (121, 168), close to the 0.02 cap.
  - Deutan is 0.113, the second lowest of the ten.
- **Cause.**
  - The recipe `thanalan-road-chart.json` is the only one of the ten with no `jewel.quiet`.
  - It paints the land with a strong teal region (#169A92, chroma 0.12).
  - It adds a warm glow (#FFC27A, k 0.05, r 300) centred at (150, 110), over the first stop's oranges.
  - Under protanopia the teal land turns grey-olive, close to the oranges' yellow-ochre. In the simulation the oranges still stand out by value but not by hue.
- **Is the undressed reference sound?**
  - **For F6, yes.** The 0.20 floor is absolute, and the jewel step keeps L by construction, so "the dress may cost at most 0.02" is exactly the intended check.
  - **For F9, no.** The approved boards were reviewed, but an undressed new board is not. Its "−0.02" branch therefore removes the floor altogether: a board that is already poor passes because it is poor.
- **Fix.**
  1. Add `"quiet": [22, 12]` to the 1-1 jewel, as on the other nine. It raised separation on 1-4 (0.117 → 0.132), 2-1 (0.117 → 0.131) and 2-5 (0.144 → 0.165).
  2. Either lower the warm glow to k 0.03 or move it off the first stop.
  3. Re-measure. The target is a p10 of at least 0.12 over all candidates.
  4. In `readability.py`, make F9 for new levels pass only if p10 ≥ 0.12, or ≥ max(0.10, undressed − 0.02), where 0.10 is the lowest approved pilot.
  5. Measure F9 over every orange candidate, movers included, as F6 does, rather than only the dealt seed's still pegs. On my all-candidate figures no other level falls below 0.10 (lowest p10: 1-2 at 0.101).

### Minor

**m1. 1-3, 1-4, 1-5, 2-3: isolated pegs sit in visible discs that stay behind after the pegs clear.**
- **Evidence.**
  - Measured over isolated pegs, the disc's contrast with the open ground (median ΔE) is:
    - 1-5: 0.090
    - 1-4: 0.080
    - 2-3: 0.079
    - 1-3: 0.071
  - The highest pilot is 0.066 (exp-p3), and the pilots' median is about 0.04.
  - On 1-4, the 90th-percentile luma step is 0.216, in the mauve sky round (127, 235), (207, 235) and (261, 199).
- **What the player sees.**
  - On 1-3's teal field, each peg wears a grey, sooty ring, which looks like the shadow the method forbids ("the veil has no shape").
  - On 1-4 the sky pegs sit in dark smudges; on 1-5 dark blue discs sit on the green aurora; on 2-3 grey smoke follows the layout across the green map.
  - The veil and dress are drawn once at load, so the discs stay where pegs used to be. The rendered cleared boards show about 20 grey dots on 1-3's teal field, smudges in 1-4's sky and smoke trails on 2-3. The pilots' cleared boards (base-p1, exp-p3) show none.
  - Players will not shoot at them, since they are about three pegs wide and dark. But they clutter the board during play, and they break the promise that "a cleared board looks finished".
- **Cause.** On 1-3, 2-3 and 1-5, the dress paints a saturated jewel over a large flat field, and `quiet: [22, 12]` then takes its colour out in separate small discs. On 1-4 the cause is luminance: the veil at 0.44 on the brightest flat field.
- **Fix.**
  - **1-3, 1-5, 2-3:** widen the quiet ramp to about `[44, 12]`, so neighbouring discs merge into one calm band along the layout. This is chroma only, so F6 is unaffected and F9 should hold.
  - **Do not lower the veil on 1-3 overall.** At k 0.30 the left side's worst purple, at (289, 268), would drop to about 0.15.
  - **1-4:** darken the sky in the grade with a `tone` entry over the sky (y < 250, mul about 0.85). The veil's step scales with the ground's luma, so it shrinks, and the margins grow.
  - **Pipeline:** add a ghost check to the readcheck. On the piece-free board, the ΔE for isolated pegs should be no more than the pilots' maximum (about 0.066).

**m2. 1-2: the face features carry the worst protan separation.**
- **Evidence.**
  - p10 is 0.101, equal to the lowest approved board (exp-p1).
  - The minimum, 0.082 at the mouth (452, 300), and the next worst sit on the cactuar's dark green body. That green lies on orange's protan confusion line.
  - Value still carries them (the body is dark), and these are the meaning pegs (eyes and mouth).
- **Fix.** Shift the body's hue toward blue-green (teal), off the protan line. Alternatively, quieten the body's chroma within 12–22 units of the face pegs with a `keepMask`.

### Nit

**n1. 2-1 and 2-2: the lamp posts at bucket height look like a second bucket lantern.**
- **Evidence.** Each lamp is 9–10 px across with a peak luma of 0.88–0.89: 2-1's at (86, 526) and 2-2's at (711, 525). That makes them the brightest thing on the board after the bucket's own lantern (0.97). They sit in the bucket's lane at the walls. When the cart reaches that wall, the two warm lights stand side by side, which matters most at 0.8x. They pass F5.
- **Fix.** Halve the lamp's halo strength (`hk`), or lower the post so its light sits at or below the rim's height (y ≥ 545).

**n2. Pipeline: 0.8x reuses the 1x faces.**
- **Evidence.** Measured faces at 0.8x move by −0.031 to +0.014: 2-2's green drops from 0.787 to 0.756 and its purple from 0.637 to 0.615. The worst-case verdicts do not change.
- **Fix.** Take the faces from the downscaled composite at each scale, for rigour.

**n3. Carried over from earlier rounds (engine palette, not these levels): kind-against-kind separation for colour-blind players.**
- **Evidence.** For protanopes, green and orange are 0.029 apart in a/b; they differ only by lightness (about 0.75 against 0.65). For deuteranopes, blue and purple are 0.018 apart.
- **Fix.** None for the levels. This is what Peg marks are for. It strengthens the case for the first-run hint (spec open question 6), because 2-3 and 2-4 are the first boards where greens sit among many oranges.

