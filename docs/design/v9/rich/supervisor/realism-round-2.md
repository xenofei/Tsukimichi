# Realism supervisor, round 2 (verbatim)

The same independent agent as round 1 judged the rendered PNGs only. Its report follows unchanged; the designer's response is at the end.

```
ROUND 2 — REALISM SUPERVISOR

Method: same as round 1. I read all 24 files, cropped and zoomed them, and measured them with numpy. Luma means 0.2126R+0.7152G+0.0722B on gamma-encoded 0–1 values; OKLab is used for hue checks.

What I confirmed on the board as a whole:
- **1x and 2x match:** the 2x downsampled differs from the 1x by a mean of 1.2–3.2 levels.
- **Peg readability, new layouts:** the smallest margin between a peg's lit face and the 90th-percentile luma of the ring around it is +0.21 (base-p1, 1x (296,194)). Every other board is at +0.24 or more.
- **Shadows:** pegs still cast no drop shadows. Around each peg the composite is never darker on the lower right; the ring ratio is 0.73–0.82 on the top and left and 0.8–1.18 on the bottom and right, so it is the veil, not a shadow.
- **Corners:** there are still no corner marks.
- **Not checkable here:** the JPEG q88 recipe, because these files are PNGs.

Round-1 items that are now fixed:
- **The grey and khaki highlights:** fixed on base-p1, base-p2 and exp-p3. Highlights now sit at a cool hue (OKLab 257–279°), and their fine texture survives: the median 5x5 standard deviation on highlights is 1.5–2.3 times what it was.
- **The base-p3 moon:** fixed. The face measures 0.82–0.89 luma, there is no bevel or crescent, the halo is even and the maria are soft.
- **The pom-pom rim, the letter, the fur tips:** fixed.
- **The exp-p2 dust lanes:** fixed. The area that measured 0.12–0.13 luma now measures 0.20–0.21, the same as the open sky. The constellation lines are fixed too: one line per edge, with the same end gap.
- **The ball tube:** now reads as glass, with its streak drawn over the balls.
- **The escutcheon:** a clean satin plate at 1x.
- **The cart lantern:** the right cap's top is now brighter (0.785 vs 0.718) and the plank's end warmer (0.195 vs 0.142).

ASSET 1: scene base-p1-airship-road — APPROVED
1. [Nit] Carried over: the edge-stretch smear at the left edge (1x x 0–30) and the column of x-shaped marks at the right edge (1x x 780–795, y 35–145). The rails hide both.
2. [Nit] The grade now leaves the chart a fairly grey slate: mid-tone saturation is 0.31 (it was 0.55). Beside the saturated lapis rails it reads slightly steel-grey. Optional fix: lift mid-tone chroma about 15% toward lapis #1D2B5A.
3. [Nit] The gilt route is now a muted tan (RGB about 139,127,109). It is drawn flat over the label banners and shields, for example through the "Gridania" banner at 1x (~400–450, 230–245), so it does not read as engraved gilt. Optional fix: lift its chroma toward gilt #D9BE82. To make it read as engraved, add a 1 px dark lower-right edge, and break the route where it crosses a banner.

ASSET 2: scene base-p2-holy-see — CHANGES
1. [Minor] The cathedral's crown has a yellow-green cast. The spire tips and their aura (1x x 480–600, y 80–160) measure OKLab hue 134–141° with chroma 0.019–0.026, for example RGB (99,107,92) and (106,113,100). Against the blue night this reads as a sulphur-gold light on the focal subject, as if a sunset caught the top. Under moonlight it should be the coolest, whitest note. Fix: in the grade, add a hue-selective chroma cut. In OKLab, cut chroma in the 60–180° band to about 0.3 times, and turn what remains toward moonstone (about 255°). The crown should end at C ≤ 0.01 or with a cool hue.
2. [Nit] Carried over: the vertical edge-extension streaks under the bridge arches (1x x 300–560, y 475–545).
The clouds now pass: they are slate-blue with form (1x (400,640) #626872 at 2x, cool) and no longer read khaki. The light still agrees with the house rule.

ASSET 3: scene base-p3-moogle — APPROVED
1. [Nit] The moon's face is about 5% brighter toward the lower right than the upper left (0.87–0.89 vs 0.81–0.85), and its halo is slightly stronger there too (0.41 vs 0.38). It cannot be seen without measuring.
2. [Nit] The snout is larger but still reads more bear than moogle. This is optional and only affects how readable the subject is.
The rim light is directional on the upper-left edges. The pom-pom is maroon with a cool rim. The letter is shadowed paper (luma 0.135 against 0.086 for the body), with a hairline upper-left rim, a V-shaped crease and a flat seal.

ASSET 4: scene exp-p1-sharlayan — CHANGES
1. [Major] The domes and roofs are still flat slabs. Their fine texture came back, but the large-scale shading did not.
   - A horizontal pass across the big dome (1x y 290, x 392–464) stays between 0.40 and 0.44 luma, with no light-to-shade gradient from left to right.
   - A vertical pass (1x x 432, y 278–309) stays between 0.39 and 0.44.
   - Saturation is about 0.1, for example #706C7C with 3 levels of variation at 1x (420,280).
   - In the composite these are exactly the shapes the arc bricks trace. They read as flat mauve-grey cardboard semicircles under the bricks (1x (380–490, 280–330), (300–350, 290–320), (495–545, 285–310), (650–725, 385–470)), and the roof planes read as flat grey slabs (1x ~(500–640, 180–230)).
   - Cause: the base layer is blurred at a radius smaller than the domes, so their shading sits in the compressed base and gets flattened by the knee.
   Fix: blur the base layer at a radius larger than the biggest dome (at least about 45 px at 1x), or give the soft knee a slope of at least 0.35 above the ceiling. Target: at least 0.06 luma of shading across each big dome, lightest toward the upper left. A faint cool tint (C about 0.02 toward moonstone) would also keep them from reading as grey card.
2. [Minor] The foliage between the buildings is still yellow-green to ochre (1x x 230–300, y 230–330, and around (600–650, 150–200); OKLab hue 134–136°, for example RGB (97,104,94) and (87,94,87)). It is plainly green in the composite. Under moonlight, foliage should go dark blue-grey. Fix: the same hue-selective chroma cut as in base-p2, item 1.
3. [Nit] Carried over: the edge smear at the left edge and the dark strip along the top. The frame hides both.

ASSET 5: scene exp-p2-lantern-ferry — APPROVED
No findings. The dust lanes stay inside the band and are no darker than the sky. The lines are consistent. The torii's rim light is upper-left. The sea's gradient and the faint darker reflection under the headland are right.

ASSET 6: scene exp-p3-mare-lamentorum — CHANGES
1. [Minor] The new still pool is brighter than the sky it reflects.
   - Just below the waterline it measures luma 0.085, and 0.074 further right. Just above the horizon the sky measures 0.025, and 0.011 further right. A mirror cannot be brighter than what it mirrors.
   - The planet's lower limb sits about 30 px above the waterline (luma about 0.12 at 1x (450–510, 495)), yet the pool shows no reflection of it under 1x x 420–600. It reflects only the spires.
   - The pool is violet (#151236) where it meets the boat's lapis water strip, so a colour seam is visible in the composite (2x y ≈ 1165).
   Fix: darken the pool to at or below the sky's value near the horizon, fading darker toward the viewer. Add a faint, broken, mirrored reflection of the planet's lit lower limb under x 420–600. Blend the pool's colour into the bucket strip where they meet.
2. [Minor] The repainted sphere left part of the original lit limb uncovered. The new dark stone shades correctly (0.16 at upper left down to 0.10 at lower right, with a cool upper-left rim). But a pale crescent remains outside the new disc on its lower-right limb (2x x ≈ 228–243, y ≈ 640–700, peak luma 0.43; 1x ≈ (114–122, 320–350)). It reads as light from the lower right. Fix: enlarge or offset the repainted disc by about 3–4 px at 2x so it covers the old limb, or paint that crescent out in the dark stone.
The planet now passes: cool lavender-grey clouds (#61667A) with form over a slate sea, lit from the upper left.

ASSET 7: composite base-p1 — APPROVED
1. [Nit] The scene's Nits 2 and 3: the slate-grey chart and the route drawn over the banners.
The frame findings from round 1 (ball tube, escutcheon, cart) are all resolved.

ASSET 8: composite base-p2 — CHANGES
1. [Minor] The scene's issue 1: the yellow-green crown. It is very visible in the composite, around the purple peg at 1x (548,57) and the spires down to y ≈ 160.
2. [Nit] Carried over: the arch streaks.

ASSET 9: composite base-p3 — APPROVED
No new findings. The moon sits correctly beside the pegs, and the eye peg is no longer orange.

ASSET 10: composite exp-p1 — CHANGES
1. [Major] The scene's issue 1: the flat dome and roof slabs directly under the arc bricks.
2. [Minor] The scene's issue 2: the green foliage, visible at 1x ~(150–300, 220–330) around the column of blue pegs.

ASSET 11: composite exp-p2 — APPROVED
No findings.

ASSET 12: composite exp-p3 — CHANGES
1. [Minor] The scene's issue 1: the pool is brighter than the sky, has no planet reflection, and meets the boat's lapis strip at a violet colour seam (2x y ≈ 1165).
2. [Minor] The scene's issue 2: the leftover lit crescent on the sphere, at 1x ≈ (114–122, 320–350) next to the orange peg at (108,328).

OVERALL: Six assets are approved: base-p1, base-p3 and exp-p2, each as scene and composite. Six are not: base-p2, exp-p1 and exp-p3, each as scene and composite.
- **One Major remains:** the Sharlayan domes and roofs are still flat, because the grade's base-layer blur is smaller than the domes.
- **Three things fix almost everything left:**
  1. A larger base blur radius, or a sloped knee, so large forms keep their shading. This fixes the Sharlayan domes.
  2. A hue-selective chroma cut on yellow and green hues (60–180°). This fixes the base-p2 crown and the Sharlayan foliage.
  3. Darkening the Mare Lamentorum pool, adding the planet's reflection, matching the pool to the boat's water strip, and covering the sphere's leftover crescent.
- **The frame** now passes in all six composites.
- **Not verified:** how the art looks in the game, and the JPEG export.
```

## Designer's response (round 3 changes)

The grade (`rich_lib.night_lab`) is changed for every painting scene:

- **Large forms.** The base layer is blurred at 48 units, wider than the largest dome, and the knee keeps a slope of 0.35. After the ceiling's soft roll-off, a **form light** gives the painting's pale shapes relief: each shape's silhouette is blurred into a height, and the one light from the upper left shades it. It darkens the faces turned away four times more than it lightens those turned toward the light, so the ceiling holds.
  - My measurement on the big dome (1x, y 290, x 392–464): 0.377–0.443, lightest on the left (first ten px 0.436, last ten 0.389).
  - The vertical pass: 0.365–0.435.
- **Yellow and green (OKLab hue 45–195°).** Chroma is cut to 0.3, and what remains is pulled toward moonstone.
  - Holy See crown (1x x 480–600, y 80–160, bright pixels): hue 263°, C 0.026.
  - Sharlayan foliage (1x x 230–300, y 230–330): hue 289°, C 0.049.
- **The ceiling.** A soft roll-off over its last 0.06. The 99th-percentile luma is 0.41–0.43 on all four paintings.

**Mare Lamentorum.**

- **The pool is a true mirror.** It reflects the slightly blurred scene above, loses more toward the viewer, and is clamped to never exceed what it mirrors.
  - Just below the waterline it measures 0.037, against 0.049 just above it.
  - Under the planet it measures 0.020, against 0.021 above.
  - Its colour runs to `#0E1530`, the boat strip's lapis, at the foot.
- **The sphere** is repainted larger and lower right (centre (97, 325), r 31), so it covers the old lit limb. The crescent area now measures at most 0.146.

Nits not changed: base-p1's slate chart and the route over the banners; base-p3's moon asymmetry (about 5%) and the snout.
