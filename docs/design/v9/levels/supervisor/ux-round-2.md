# UX/UI specialist, levels round 2

**Reviewed:** the ten levels base-01 to base-10 at `ad2d9a16`, in worktree `agent-a570460c913ca1c79`. I judged them against level-method §5 and §8 (F5, F6, F9), the coordinator's round-2 decisions, and the six approved pilots. I also read main's runtime scene-recipe format (`git show main:docs/design/v9/scene-recipe.md`).

**OVERALL: REVISE.** One round-1 Major is resolved: 1-1's colour-blind (protan) separation. But per-peg ghost halos are still there, which breaks the binding decision "quiet by low frequency, never per peg":
- **Unresolved:** 1-5's ghost discs remain.
- **New:** 2-2 and 2-5 now show them, on their newly painted skies.
- **Still in the game:** these halos come from the jewel's quiet, which the game bakes into the scene at load. The runtime's per-piece veil does not remove them.
- **Why the build passed them:** the pipeline's ghost gate cannot see this kind of disc.

## Method

**Ran (read-only):**
- `mfl.py selftest` (all ok). I did not run `build` and changed nothing in the repo.
- Scripts and images are in `scratchpad/ux2/`: `measure2.py`, `ghost_snr.py`, `halo_r1.py`, `huedisc.py`, plus crops.

**F6 (value margin).**
- Faces were measured on the composite at each scale.
- The ring was taken at its 90th percentile, and at its 99th to catch glints.
- I covered every position each kind can be dealt to, with movers at 24 points along their path, on the piece-free board, at 1x and at 0.8x (Lanczos 640×480).

**Colour-vision safety.**
- Machado 2009 at severity 1, protan and deutan.
- The measure is the OKLab a/b distance from each kind's dealt core to the ground 12–20 px round it.
- I took it at every position the kind can be dealt to (all orange candidates, movers at 24 points).

**Ghost discs.** I rebuilt three piece-free boards at 2x from the committed dressed scenes, using the engine's own deal at seed 1:
- **baked:** veil and dress, the same board as the pipeline's cleared board;
- **no-veil:** the dress only. This is the runtime's cleared board, since the runtime draws the veil per piece and fades it with the piece;
- **no-quiet:** the dress with `quiet` removed and no veil.

On each I ran four measures:
- round 1's exact measure (`ux/halo2.py`), on the 1x composites, for the levels and the six pilots;
- the pipeline's own `readability.ghost`;
- a new **dE/texture** measure: the disc's ΔE divided by the open ground's own OKLab spread. A disc is visible in proportion to how flat the ground round it is;
- direct viewing of the cleared boards and 2x crops.

**Other checks:**
- Bright peg-sized spots: luma > 0.40 and 3–26 px across, in the piece-free board.
- The lamp posts.
- The 2-4 sails at 0.8x.
- The 2-3 band at 2x.

## Verdicts

The pilots' ghost dE is 0.019–0.066. Their dE/texture is 0.24–1.11 at the median and at most 1.93 at the 90th percentile.

| Level | F6 worst, pipeline / mine | Protan orange p10, pipeline / mine; min (all candidates, movers incl.) | Ghost dE: pipeline cleared / round-1 method / runtime no-veil | dE/texture median (composite / no-veil) | Verdict |
|---|---|---|---|---|---|
| 1-1 Road to Horizon | 0.295 / 0.340 | 0.135 / 0.135; 0.133 | 0.055 (n=1) / 0.058 / 0.041 | 2.79 / 1.96 (one peg; the band is low-frequency) | APPROVE |
| 1-2 Horizon by Night | 0.386 / 0.431 | 0.151 / 0.151; 0.144 | 0.053 / 0.055 / 0.039 | 2.94 / 2.01 | APPROVE (Minor in G1: discs on the mesa) |
| 1-3 The Cactuar | 0.283 / 0.329 | 0.122 / 0.122; **0.099** at the mouth (452, 300) | 0.043 / 0.045 / 0.022 | 1.74 / 1.35 | APPROVE (n3) |
| 1-4 The Gilded Dome | 0.208 / 0.229 | 0.119 / 0.119; 0.117 | 0.063 / 0.059 / 0.052 | 1.21 (p90 3.07) / 0.91 (p90 2.49) | APPROVE (Minor in G1: sky discs; n1) |
| 1-5 The Crystal's Call | 0.370 / 0.393 | 0.141 / 0.141; 0.129 | 0.060 / **0.096** / **0.093** | **3.46 / 2.59** | **REVISE** (G1) |
| 2-1 Limsa Across the Water | 0.320 / 0.373 | 0.140 / 0.140; 0.134 | 0.049 (n=1) / 0.051 / 0.029 | 0.79 / 0.46 | APPROVE |
| 2-2 Moonpath on the Bay | 0.310 / 0.373 | 0.133 / 0.133; 0.130 | 0.058 / 0.057 / **0.056** | **2.42 / 2.04** | **REVISE** (G1) |
| 2-3 The Kraken's Sea | 0.317 / 0.359 | 0.119 / 0.123; 0.116 | none isolated / 0.078 / 0.074 | 3.07 / 2.94 (a band, not discs) | APPROVE (m1) |
| 2-4 The Ferry Under Sail | 0.230 / 0.228 | 0.147 / 0.151; 0.143 | 0.040 / 0.043 / 0.023 | 1.44 (p90 3.84) / 1.01 (p90 3.96) | APPROVE (m2; Minor in G1) |
| 2-5 Twin Lanterns | 0.387 / 0.411 | 0.130 / 0.129; 0.127 | 0.041 / 0.052 / 0.047 | **2.03 / 2.02** | **REVISE** (G1) |

**What holds everywhere:**
- **F6.** Every kind clears 0.20 at 1x and 0.8x at its worst placement, movers included. Mine runs 0.02–0.06 above the pipeline's, as in round 1. The two tightest boards:
  - 1-4: purple 0.229 at (436, 476), on the pale lower dome, not in the sky.
  - 2-4: purple 0.228 at (340, 322), on the lavender sails.
- **Glints (99th percentile).** Values below 0.18 occur only at:
  - the wall gilt (1-3 at (715, 471));
  - the 1-1 route dashes (0.178);
  - 1-4's pale lower dome (0.143, n1).

  2-2's new glints are dim: no spot over 0.40 luma on that board apart from the lamp and the bucket.
- **Colour separation against the ground.** Every level is within the round-1 range, in all three visions.
  - Green, deutan p10: 0.078–0.100 (round-1 levels 0.074–0.105).
  - Green, protan: 0.103–0.109.
  - Purple, normal vision: minimum 0.045–0.095. The lowest is 2-5's purple on the magenta water.
- **Nothing reads as a peg.** These are all of the bright peg-sized spots:
  - the three lamp posts (now 6–7 px);
  - 1-4's town lamps (3–8 px, up to 0.75);
  - 2-5's fireflies (3 px, up to 0.79);
  - 1-1's label and route marks (0.60–0.63);
  - the bucket lantern.

  Dark features do not read as pegs either: 1-2's derrick knob and finial, 2-2's corner rocks below the rim, and the framing (fronds, ropes, outcrops). The 2-5 fronds are thin and do not clutter.

## Round-1 findings

**M1 (1-1 protan separation): RESOLVED.**
- **Before → now:** p10 0.091 → 0.135; minimum over all 28 candidates 0.089 → 0.133, at (548, 199). Deutan 0.147.
- **Cause, fixed:** `quiet [50, 14]` was added, and the warm glow is down to k 0.03 and moved to (110, 50).
- **Rule, fixed:** F9 in `readability.py` now covers every orange candidate, with movers at four moments of their cycle. It passes at 0.12, or at 0.101 (the lowest pilot), as I asked.
- 1-4 (0.119) and 2-3 (0.119 on the pipeline, 0.123 on mine) pass on the 0.101 branch.

**m1 (ghost discs): PARTLY RESOLVED; one level is now Major.**
- **Old 1-3 (Ul'dah across the sands):** the board is gone, so this is moot.
- **1-4: partly resolved.**
  - The veil's luminance step in the sky is fixed: round-1 measure 0.080 → 0.059, and dL is down.
  - But the four sky pegs at (148, 148), (229, 148), (260, 198) and (299, 127) still print grey-blue discs in the rose sky.
  - Those discs survive with the veil off: no-veil dE 0.052, dE/texture 3.63 at (150, 150).
  - The cause is the quiet removing the rose region (#B04E78) per peg (crop: `ux2/b04_sky3.png`, baked / no-veil / no-quiet). Minor; see G1.
- **1-5: NOT resolved.**
  - Round-1 measure 0.090 → 0.096.
  - No-veil 0.093, against no-quiet 0.017. The discs are entirely the quiet removing the 0.18-chroma aquamarine regions.
  - The runtime cleared board (`ux2/base-05-nv.png`) is a polka-dot field of blue discs over the green aurora.
  - The pipeline's median (0.060) passes it only because of how it picks pegs; its own worst peg is 0.099. Now Major; see G1.
- **2-3: discs resolved, band Minor.**
  - With `quiet [44, 12]` there are no per-peg discs.
  - Cleared, the merged band reads as calm blue staining on the green map.
  - In play it reads as grey soot round the clusters; see m1.
- **The ghost gate was added, but it is unsound;** see G2.

**m2 (cactuar face, protan): IMPROVED, now a Nit (n3).**
- p10 0.101 → 0.122.
- The minimum is 0.082 → 0.099, still at the mouth (452, 300). The body is darker, so value carries the mouth.

**n1 (lamp posts at bucket height): RESOLVED.**

| Board | Round 1 | Round 2 |
|---|---|---|
| 2-1 | (86, 526), 9–10 px, peak 0.89 | (700, 546), 6 px, 0.80 |
| 2-2 | (711, 525), 9–10 px, peak 0.89 | (711, 545), 7 px, 0.80 |
| 2-3 | — | (86, 545), 7 px, 0.80 |

All three now sit at rim height, smaller and dimmer than the cart lantern (21 px, 0.97). They no longer read as a second bucket.

**n2 (0.8x reuses the 1x faces): RESOLVED.** `faces_at` now measures each scale.

**n3 (kind-against-kind separation for colour-blind players): UNCHANGED.** This is the engine palette, not these levels:
- protan green–orange 0.029;
- deutan blue–purple 0.018.

Peg marks and the first-run hint still carry it.

## Does the runtime scene-recipe format change the weight of the ghost findings?

**Only for the veil.**
- **The veil fades with its piece.** Main's builder never bakes the veil. Each piece carries its own veil sprite, which fades as the piece clears. So the luminance part of a ghost disappears from a cleared board in game, and the pipeline's ghost gate over-weights it by measuring the baked veil.
- **In play, nothing changes.** Live pegs still carry their veil.
- **The jewel's quiet does not fade.** It maps onto the palette's `near` mask term, which the builder computes from all pieces at load and bakes into the scene. So the hue discs persist on the game's cleared board.
- **The hue discs are what fail here.** On 1-5, 2-2, 2-5, 1-2 and 1-4's sky, the no-veil dE is close to the composite dE (1-5: 0.093 vs 0.096; 2-2: 0.056 vs 0.057). These boards will not look finished when cleared in the game.

**Converter caveat:**
- The runtime palette has no per-pixel equivalent of the dress's `keep` reduction (`keepm = 1 − 0.5·near`).
- The region quiet needs `near` with `invert` and `scale 0.6`.
- So F9 will move after conversion, and main's tests measure only F6 and the ceiling, not F9 or ghosts.
- F9 and the ghost check should be re-measured on `tools/Tsukimichi.MoonfallRender` output once the recipes are converted.

## New findings

### G1 (Major): the jewel's quiet prints a hue halo round every peg on smooth skies (1-5, 2-2, 2-5), and to a lesser degree on 1-2, 1-4 and 2-4

**Evidence.**
- **2-2.** Every sky peg (about ten) sits in a crisp blue-grey disc about 50 units across on the rose dusk (crop `ux2/b07_sky2x.png`). The discs are just as clear on the no-veil cleared board (`ux2/base-07-nv.png`).
  - The round-2 claim that the sky is "painted, not dressed, so the quiet prints no discs" is false.
  - `vesper-moonpath.json` still dresses a rose region (#B0566E, chroma 0.065, mask `y 304→280`) over the sky, on blue bands. The quiet swings each peg's surround back to the blue band.
- **2-5.** Every peg in the upper sky wears a magenta halo on the blue (crop `ux2/b10_top.png`).
  - The cause is the same mechanism inverted: a blue region (#2E5AC0, `y 260→140`) on rose bands.
  - dE/texture is 2.03 in play and 2.02 cleared.
- **1-5.** A polka-dot field across the aurora, in play and cleared (`ux2/base-05-nv.png`).
  - dE/texture: median 3.46, 90th percentile 6.87, worst 10.9 at (170, 360). That is the worst in the set.
- **Minor-grade instances:**
  - **1-2:** dark-blue discs on the green mesa, from the teal region (#2AA8A0, chroma 0.15); dE/texture 2.94.
  - **1-4:** the four sky discs described under m1.
  - **2-4:** blue discs round the pegs on the rose sea; worst dE/texture 3.95 at (100, 390).
- **Why only these boards.** The ground is smooth. On our own paintings the local OKLab spread is 0.01–0.02, so a hue swing of 0.04–0.06 stands 2–3.5 times above the texture. The pilots' discs were luminance discs on textured game art (dE/texture ≤ 1.11 at the median).

**Fix.** Remove the hue the quiet swings, rather than spreading the swing out:
1. **2-2:** make the jewel band rose where the sky is, and drop the sky region. For example, `bands [[0,"#8E4A78"],[285,"#8E4A78"],[305,"#2E48C8"],[600,"#2038A0"]]`. Region and band then agree, so the quiet changes nothing visible there.
2. **2-5:** the mirror image. Use a blue band at the top, for example `[[0,"#3A4AA8"],[150,"#3A4AA8"],[260,"#9A4072"],…]`, and drop the blue region.
3. **1-5:** paint the aurora into `painters/crystal.py` and protect it with a `keepMask`, instead of dressing it as two 0.18-chroma regions. A wider quiet already failed F7 on this board.
4. **1-4:** carry the sky's rose in a band (or a `tone` hue) rather than a region.
5. **1-2 and 2-4:** the same treatment, for the mesa's teal and the sea's rose.

Then re-measure F9 on each, because the quiet exists for F9. If protan separation falls below 0.12, lower the region's chroma rather than putting back a per-peg quiet. Re-check F7's second-jewel share (≥ 15%) as well. Target: dE/texture at most about 1.2 at the median and 2.0 at the 90th percentile, on the no-veil cleared board.

### G2 (Major, pipeline): the ghost gate cannot catch the defect it was added for

**Evidence.** I found five ways it fails:
- **Hue discs pass.** On a synthetic flat rose sky with a blue-shifted disc round each of three pegs (dE 0.049), `readability.ghost` returns 0.050 and passes (`ux2/huedisc.py`). Its self-test only has a dark luminance disc.
- **It can be vacuous.**
  - On 2-3 no peg is isolated, so the gate passes with no measurement.
  - On 1-1 and 2-1 the "median" is a single peg.
- **On wide-quiet levels it measures the quiet against itself.** Its open ring (32–48 units) lies inside the quiet ramp of 2-3 (`[44,12]`), 1-1 (`[50,14]`) and 1-2 (`[30,12]`).
- **It measures what the runtime will not draw.** It works on the baked-veil board, which the runtime no longer produces.
- **Picking pegs differently flips the result.**
  - 1-5: 0.060 on the pipeline's selection, against 0.096 on round 1's.
  - 2-3: no measurement, against 0.078.

**Fix.**
1. Measure on the cleared board with the veil off (what the game leaves) and with it on (in play).
2. Use dE divided by the open ring's OKLab spread, and gate the median at 1.2 and the 90th percentile at 2.0 (the pilots' highest are 1.11 and 1.93).
3. When fewer than three pegs qualify, fall back to near-against-far ground per 100×100 cell (5–14 units from a piece against 60 or more units away), so the gate is never vacuous.
4. Add the self-test case "a hue disc on a flat rose sky must fail".

### m1 (Minor): 2-3's merged band reads as grey soot in play

**Evidence.**
- **Cleared:** the `[44, 12]` quiet gives a calm, low-frequency blue staining with irregular edges (`ux2/b08_cmp.png`, lower half). This works.
- **In play:** the veil at 0.40 darkens that desaturated band to slate grey round the clusters, most of all the kraken's ring (same crop, upper half). On the green map it reads as smoke, not sea.

**Fix.**
1. Ease the veil 0.40 → 0.34. F6 has room: worst 0.317 on the pipeline, 0.359 on mine.
2. Or make the band a deep sea-teal (for example #2A5EA8) instead of #3048C8, so the quieted area reads as deeper water rather than grey.
3. Then re-check F6 and F9.

### m2 (Minor): 2-4, the mover rows print a rectangle on the sea, and the sails hold the set's tightest F6

**Evidence.**
- **The rectangle.** The quiet along the two rows of slide movers turns the rose sea into a blue-grey band with rounded ends. It spans about x 95–690 and y 440–525, and it stays on the cleared board (`ux2/base-09-nv.png`).
- **The sails.**
  - The painted sails are light lavender, ring 90th percentile about 0.37.
  - They give purple 0.228 at (340, 322) and (340, 254), the lowest margin of the ten (pipeline 0.230).
  - At 0.8x (`ux2/b09_08x.png`), blue pegs on the sails read, but with the weakest contrast on the board.

**Fix.**
1. The sea: apply G1's band-agreement fix.
2. The sails: darken them by about 0.05 L in `painters/ferry.py`, to put margins back at 0.25 or more.

### n1 (Nit): 1-4, the pale lower dome at (436, 476)

**Evidence.** It is the worst F6 place on 1-4: purple at the 90th percentile is 0.229, and at the 99th 0.143 (glints).

**Fix.** Optionally, add a `tone` of about 0.9 over that dome.

### n2 (Nit, pipeline): F9 skips orange-candidate bricks

**Evidence.** F9 never measures 2-2's two orange-able arcs, at (140, 420) and (446, 500). Both sit on the dark sea, so they pass by inspection.

**Fix.** Sample along each orange-candidate brick's middle line in `orange_views`.

### n3 (Nit): 1-3, the cactuar's mouth

**Evidence.** Protan separation at the mouth (452, 300) is 0.099. It is the only meaning peg under 0.10.

**Fix.** Optionally, shift the body's teal another 10–15° toward blue near the face.

### n4 (Nit, docs): the README lesson is wrong

**Evidence.** The README says that painting 2-2's rose dusk avoids the quiet's discs. It did not: the rose is dressed as well.

**Fix.** Correct the lesson after G1, to: "paint the colour, and keep the jewel band the same hue there".

## Unverified

- The runtime's per-piece veil sprites against the baked, merged veil: whether rows read as beads instead of bands.
- F9 and the ghost measures after conversion to `moonfall-scene` v1.
- Anything in the game itself.
