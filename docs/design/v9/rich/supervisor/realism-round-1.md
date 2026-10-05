# Realism supervisor, round 1 (verbatim)

An independent agent judged the rendered PNGs only (the six pilot scenes and their six composites, 1x and 2x), never the source. Its report follows unchanged; the designer's response is in `realism-round-2.md`'s preamble.

```
ROUND 1 — REALISM SUPERVISOR

Method: I read all 24 files, cropped and zoomed them, and measured them with numpy. Luma means 0.2126R+0.7152G+0.0722B on gamma-encoded 0–1 values. Each 1x/2x pair matches: the 2x downsampled differs from the 1x by a mean of 1.2–3.1 levels, so each finding applies to both sizes. Checks that passed in every composite:
- **Peg readability:** every peg's lit face beats the 90th-percentile luma of the ring around it by at least +0.24.
- **Shadows:** pegs and bricks have no drop or cast shadows. The composite/scene luma ratio around each peg is the same in all eight directions (0.83–0.89, which is the veil).
- **Corners:** there are no corner marks on the playfield.
- **Approved v9 pieces:** the pegs, ball and telescope are lit from the upper left and sit correctly.
- **Not checkable here:** the JPEG q88 4:4:4 recipe, because these files are PNGs.

Shared defect (behind the Majors marked [GRADE] below): the "Medallion night grade" holds the scene under its luminance ceiling by flattening highlights into near-neutral grey. Highlights pile up at luma 0.42–0.46 and keep almost none of their colour:
- Saturation of pixels with L>0.38 is 0.17–0.30. Mid-tones keep 0.55–0.76.
- Pixel values on dome tops and cloud tops read #6E6B7C, #6F747E and #686D7D, with 2–7 levels of variation inside a patch.
- Next to the fully saturated blue mid-tones, these flat patches read as khaki or beige cardboard. Moonlit highlights should read cool silver.
- Shared fix: compress luminance only, in OKLab. Use a soft knee on a blurred base layer, then add the detail layer back so form survives. Keep chroma, and pull hue toward moonstone #C3CEE4. Lower mid-tone chroma about 20–25% so highlights stay the coolest, lightest note.

ASSET 1: scene base-p1-airship-road — CHANGES
1. [Minor] [GRADE], milder here because the map keeps its texture. The sea-of-clouds and other highlights (1x x 430–800, y 0–300) are desaturated, averaging RGB 96,103,124. In the composite (1x ~x 470–720, y 45–200) they read as grey-khaki over navy instead of moonlit white-blue. Fix: the shared fix above.
2. [Nit] Horizontal edge-stretch smear down the full left edge (1x x 0–30). The left rail hides it in the composite. Fix: crop or rebuild the edge, don't stretch it.
3. [Nit] A column of small x-shaped marks at the right edge (1x x 780–795, y 35–145) reads as stray glyphs when the scene is seen alone. The right rail hides it. Fix: crop it out or retouch it.
The orientation is correct (Limsa west, Ul'dah south, Ishgard north, Ala Mhigo east), and the lettering is not mirrored.

ASSET 2: scene base-p2-holy-see — CHANGES
1. [Major] [GRADE]. 8.8% of the frame sits above luma 0.42, with a histogram spike at 0.44. Cloud masses and cathedral highlights turn into flat grey slabs, for example 1x (200,320) #6F747E with 5 levels of variation. They read beige beside #455788 mid-tones, and the cloud forms lose their modelling (2x x 160–1060, y 480–980). In the composite, the clouds between the arc bricks (1x y 270–340) are the worst spot. Fix: the shared fix above.
2. [Nit] Vertical edge-extension streaks under the bridge arches (1x x 300–560, y 475–545). They read as columns dissolving into fog and only show when brightened.
The light agrees with the house rule: the mountain snow is lit on its left faces, the cathedral is brightest on its left with its right spires darker, and the clouds are lit from the top.

ASSET 3: scene base-p3-moogle — CHANGES
1. [Major] The moon (1x centre (163,111), r≈25) is shaded like a lit bead or raised button, not a light source.
   - Its upper-left limb is the darkest part (luma 0.48–0.62), and its lower-right is the brightest (0.93).
   - A darker crescent sits just outside the upper-left limb (luma 0.36–0.38, against 0.40–0.59 on the other sides).
   - The light that should be the house key light therefore looks lit from the lower right. That contradicts the pegs on the same board, which are lit from the upper left.
   - It reads as a plaster ball, and the lobed, heart-shaped maria add to that.
   Fix: render it as emissive. Make the face near-uniformly bright with at most ~10% symmetric limb darkening. Remove the inner bevel and the outer dark crescent. Give it a soft, even, cool halo, and make the maria irregular soft patches.
2. [Minor] The pom-pom's rim light is warm peach (1x ~x 555–580, y 95–130). The only light behind the moogle is the moon, so the rim must be cool, like the fur rim. A warm rim implies a second warm light that does not exist. Fix: change the rim to a dimmed moonstone (#C3CEE4) and keep the pom-pom body as a deep maroon silhouette.
3. [Minor] The letter (1x ~x 488–545, y 342–382) reads as a thick grey tile, not paper.
   - It has a bevelled slab edge 3–4 px thick at 2x, and its uniform mid-grey face is about twice as bright as the fur (luma 0.17 vs 0.09).
   - It is lit from the front although the moogle is backlit, and the seal is a glossy spherical red bead.
   Fix: make the edge thin (at most 1 px) and add the envelope's V-shaped flap crease. Put the face in the same backlit shadow as the body, slightly lifted for paper and cool-toned, with a hairline rim on its upper-left edge only. Make the seal a flat wax disc with a dull sheen.
4. [Nit] Two stray lit fur tips on the snout/chin side that faces away from the moon (2x ≈ (1145,545) and (1150,570)). Fix: remove them.
5. [Nit] The moogle's signature bulbous nose is missing, so the profile reads as a bear or cat with bat wings. Fix: enlarge the snout bump into a nose silhouette.
The fur rim light is otherwise correctly directional: upper-left edges are lit and lower-right edges are dark.

ASSET 4: scene exp-p1-sharlayan — CHANGES
1. [Major] [GRADE]. The domes, roofs and clouds are flattened into featureless grey disks. Examples: the big dome at 2x (840,560) is #6E6B7C with 3 levels of variation, and the dome at 2x (1000,590) is #6D687C. This is the focal area of the level: in the composite the orange arc bricks sit directly on top of these flat grey semicircles (1x (380–490, 285–330), (300–350, 290–320), (495–545, 285–310), (652–725, 385–470)). Fix: the shared fix above, so the domes keep their spherical modelling and their cool violet-silver highlight.
2. [Nit] Horizontal edge smear at the left edge (1x x 0–25, y 115–150 and 230–350) and a darker strip along the top (1x y 0–8). The frame hides both.
The light agrees with the house rule: the statue and towers are lit on their left faces, and the clouds are lit from above.

ASSET 5: scene exp-p2-lantern-ferry — CHANGES
1. [Minor] The dark "dust lane" blobs at low left sit outside the Milky Way band (1x ≈ (245,400), (215,430), (150,450), (110,450)). They are darker than the open sky (luma 0.12–0.13 against 0.20–0.22 around them), where the band has already faded, so they read as soot smudges or holes in the sky. Fix: keep dark lanes inside the bright band, never darker than the background sky, and fade them out as the band fades.
2. [Nit] The constellation line work is inconsistent.
   - Between the two top stars (1x (555,150) and (600,148)) there is both a thin arc and a separate thicker straight segment that touches neither star.
   - The line from the lantern up to (555,150) stops about 8 px short, while the other lines touch their stars.
   Fix: draw one line per edge and use the same end-gap everywhere.
What is right:
- The torii's rim light is on its left posts and its top, matching the house light.
- The sea darkens correctly from the horizon to the foreground (luma 0.16 to 0.05), with a faint darker reflection under the headland.

ASSET 6: scene exp-p3-mare-lamentorum — CHANGES
1. [Major] [GRADE]. The planet's cloud tops are compressed to neutral grey (2x (1000,520) #686D7D, with 11% of the frame piled at luma 0.42–0.44). The oceans stay at full saturation (#0F3D9A, saturation about 0.9). The planet reads as blue-and-khaki camouflage instead of white cloud over blue sea, and it is very visible in the composite (1x x 380–560, y 220–420). Fix: the shared fix above, keeping the cloud tops cool white-silver, and reduce ocean chroma about 25%.
2. [Minor] The small sphere in the crescent cradle (1x ≈ x 68–122, y 296–350; visible in the composite with a peg on it) is lit from the right. Its left half measures luma 0.15 and its right half 0.34. The planet beside it, and the house light, are upper-left. Fix: re-shade or mirror it so its lit side faces upper-left, or turn it into a silhouette with an upper-left rim.
What is right:
- The planet's lit limb, its atmosphere glow on the lit side, and its terminator agree with an upper-left light.
- The floating rocks are lit on their tops.

ASSET 7: composite base-p1 — CHANGES
1. [Minor] The scene's issue 1 above (grey-khaki cloud highlights).
2. [Minor] Frame, all composites: the ball tube reads as an opaque dark-navy capsule, not glass.
   - Nothing behind it shows through. The rail's guilloché is invisible inside the tube.
   - Its faint specular streaks run behind the balls, which hide them (2x x 40–110, y 130–660). A highlight on the glass's front surface must cross over the balls.
   Fix: make the body largely transparent, showing the lapis guilloché darkened about 30% and slightly narrowed toward the edges. Draw a crisp upper-left streak, a faint right-edge reflection and thin bright edge lines over the balls.
3. [Minor] Frame, all composites: at 1x, the escutcheon's guilloché breaks up into scattered bright specks around the moonstone (1x ≈ x 385–415, y 8–25). That reads as glitter noise, not engraving; at 2x the pattern shows moiré. Fix: supersample and low-pass the engraving so the line pitch is at least 2 px at 1x, or use a satin-brushed plate at 1x with one engraved border line.
4. [Nit] The lantern cart: the lantern's warm spill reaches its pole and faintly the plank (luma 0.164 vs 0.139). The brass corner cap directly below it is identical to the far cap (#C1A76F vs #C0A56E). Fix: add a faint warm glint on the right cap's top and the right end of the plank.
The rest of the frame's light agrees with the house rule:
- the cabochons and rosette petals are highlighted upper-left;
- the score window's recess is shaded under its top lip;
- the medallion dome is highlighted upper-left.
The brass, the enamel and the moonstone each read as their material.

ASSET 8: composite base-p2 — CHANGES
1. [Major] The scene's issue 1 above. In the composite, the beige-grey cloud slabs between and under the arc bricks are the dominant field (1x x 80–450, y 270–400).
2. [Minor] The ball tube, as in asset 7, item 2.
3. [Minor] The escutcheon, as in asset 7, item 3.
4. [Nit] The lantern cart's spill, as in asset 7, item 4.
The steel-blue bricks are highlighted along their tops, which is consistent.

ASSET 9: composite base-p3 — CHANGES
1. [Major] The scene's moon (scene item 1). Uncovered at 1x (163,111), it is the brightest object on the board and contradicts the upper-left lighting on the pegs right next to it.
2. [Minor] The scene's pom-pom rim and letter (scene items 2 and 3). In the composite, the pom-pom becomes a dull maroon blot inside the ring of orange pegs (1x (572,117)).
3. [Minor] The ball tube, as in asset 7, item 2.
4. [Minor] The escutcheon, as in asset 7, item 3.
5. [Nit] An orange peg sits exactly on the moogle's eye (1x (540,256)) and reads as a glowing orange eye. Fix: move it one peg-width if that is not intended.
6. [Nit] The lantern cart's spill, as in asset 7, item 4.

ASSET 10: composite exp-p1 — CHANGES
1. [Major] The scene's issue 1 above. The arc bricks sit directly on flat grey dome fills, which look like grey cardboard semicircles under the bricks (coordinates in the scene's entry).
2. [Minor] The ball tube, as in asset 7, item 2.
3. [Minor] The escutcheon, as in asset 7, item 3.
The approved boat sits correctly on the harbour water, with the lantern's reflection directly under it. The veiled waterfall (1x x≈90) keeps the column of blue pegs on it readable.

ASSET 11: composite exp-p2 — CHANGES
1. [Minor] The scene's dust-lane smudges (scene item 1). They are visible in the composite at 1x (245,400) and (215,430).
2. [Minor] The ball tube, as in asset 7, item 2.
3. [Minor] The escutcheon, as in asset 7, item 3.
4. [Nit] The double line between the two top constellation pegs (1x x 555–600, y 148–150) is still visible.
The boat sits correctly on the scene's sea. The constellation's warm lantern star is covered by an orange peg, so there is only one warm light on the board.

ASSET 12: composite exp-p3 — CHANGES
1. [Major] The scene's issue 1 above. The khaki-and-cobalt planet sits behind most of the layout, and orange pegs sit on its beige patches (for example 1x (470,298)).
2. [Minor] The sphere lit from the right (scene item 2), visible at 1x (95,320) with an orange peg on it.
3. [Minor] The approved lantern boat floats on its water strip (1x y 585–595) in front of a starfield and lunar spires. The scene has no water, so the boat sits on nothing that belongs to the scene. Fix: give the scene a dark, still reflecting pool along its bottom, with a faint reflection of the planet's glow, that the boat's water continues. Otherwise use a non-water bucket for this level.
4. [Minor] The ball tube, as in asset 7, item 2.
5. [Minor] The escutcheon, as in asset 7, item 3.

OVERALL: None of the 12 assets is approved.
- **Biggest blocker:** the night grade. Its luminance ceiling greys out and flattens the highlights of all four paintings, which makes domes, clouds and the planet read as khaki cardboard. It is Major on base-p2, exp-p1 and exp-p3 and Minor on base-p1. One fix to the grade (compress luminance, keep and cool the colour, keep local detail) clears most of the Majors.
- **Next most important:** the painted moogle level's moon is shaded as a button lit from the lower right, so it contradicts the house light it is meant to be.
- **Frame:** it is close. Its light direction is consistent and the materials mostly read correctly. Two Minors remain: the ball tube does not read as glass, and the escutcheon's engraving turns into noise at 1x.
- **The ferry scene** needs only the dust-lane fix to pass.
- **Good:** peg readability, the no-shadow ruling and the no-corner-marks ruling all hold.
- **Not verified:** how the art looks in the game, and the shipping JPEG recipe.
```

## Designer's response (round 2 changes)

- **The night grade (all four paintings)** is rebuilt as the supervisor asked (`rich_lib.night_lab`):
  - It works in OKLab.
  - Lightness is lowered with a curve. A soft knee then compresses only the blurred base layer, and the detail layer is added back, so clouds and domes keep their modelling.
  - Chroma is kept, with mid-tones scaled more than highlights.
  - Hue is pulled toward lapis in the shadows and toward moonstone `#C3CEE4` in the highlights (violet-moonstone on the Far Shore).
  - Warm hues are drained harder than cool ones, and the sky's own colour is halved.
  - On Ishgard the 99th-percentile luma is 0.42.
- **base-p3:**
  - The moon is emissive (`rich_lib.moon_emissive`): a near-uniform face with about 10% symmetric limb darkening, soft irregular maria and a soft, even, cool halo. The bevel and the dark crescent are gone.
  - The pom-pom's rim and fringe are a dim moonstone; its body stays deep maroon.
  - The letter is paper in the backlit shadow, lifted a little and cool, with a hairline rim on its upper-left edge only and a V-shaped flap crease. The seal is a flat wax disc with a dull sheen on its moon-facing edge.
  - The fur fringe stays tight to the moon-facing edges, so no tips are lit on the far side.
  - The nose is larger.
- **exp-p2:** the dust lanes darken only the band's own light and fade with it. Each edge has one line, with the same 6-unit end gap everywhere; the atlas drawing's duplicate arm is gone.
- **exp-p3:**
  - The small sphere is repainted as a dark stone sphere with a cool rim on its upper-left limb.
  - A still pool runs along the scene's foot from y 548, with the scene broken into horizontal reflections, so the boat's water continues it.
- **Frame:**
  - The ball tube is clear glass: the rail's guilloche shows through, darkened, and more so at the walls. A crisp upper-left streak, a faint right reflection and the edge lines are drawn over the balls.
  - The escutcheon is a satin plate with one engraved border, so there is no fine guilloche to break up at 1×.
  - The cart's lantern warms the near strap and the plank's end, with a glint on the strap's top.
- **Not changed:** the Nits behind the rails (the edge smear and the marks), which the frame always covers.
