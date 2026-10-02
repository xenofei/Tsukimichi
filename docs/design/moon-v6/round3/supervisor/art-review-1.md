# Art review 1: Menphina's Medallion, round 3

**Reviewer:** art supervisor
**Subject:** `round3/medallion-r3/` (8 glyphs and `plugin-icon.svg`), compared with `round2/menphina-medallion/`
**Evidence:**
- `_sheet.png`.
- Supervisor renders, made with headless Chrome:
  - each glyph at 256 px;
  - In journal, Completed, Done and Locked out at 512 px;
  - the icon at 512 and 1024 px;
  - the icon at 64 px, magnified ×8 with pixelated scaling.
- The SVG source, read only to name elements and measure geometry.

## Verdict: CHANGES REQUIRED

The owner's three asks are met:
- the crystal is gone;
- every medal and the icon frame use one gilt rim;
- D is clearly enhanced.

The shared bezel is lit correctly, and the icon's core reflection logic is right.

What blocks approval is a set of realism errors:
- a mirror image the physics doesn't allow;
- reflections above the horizon;
- a phantom terminator;
- mist that is brightest away from its light;
- a hazy moon lit like a bubble;
- an inlay hanging off its surface;
- several tangents.

All the fixes are local. None of them changes the concept.

## Required fixes

Coordinates are in each file's own viewBox: 512 for the icon, 128 for the glyphs.

### 1. Delete the squashed crescent mirror at the horizon

**Files:** `plugin-icon.svg` and `ready.svg`.

**Elements:**
- In `plugin-icon.svg`: `<g clip-path="url(#r3i-sl)" opacity=".36">` and the `r3i-sl` clipPath.
- In `ready.svg`: `<g clip-path="url(#r3r-sl)" opacity=".35">` and the `r3r-sl` clipPath.

**What is wrong:**
- On flat water, the moon's mirror image sits at the mirror point, which is as far below the horizon as the moon is above it (y ≈ 431 in the icon). That is where the brightest road row already is.
- No compressed copy appears just under the horizon.
- At 1024 px the slices render as three hard-ended grey slabs at y 304–325 with square ends near x 210. They read as UI debris.

**Change:** delete both groups. The road rows already carry the reflection.

### 2. Icon road: keep reflections below the horizon and foreshorten toward it

**File:** `plugin-icon.svg`, the glint paths.

**What is wrong:**
- Row 1 (y 306) has its top edge at y 299.28, above the horizon line (y 300). A reflection cannot sit above the horizon.
- Rows 1–3 are each 12 tall, with gaps of 0 and 0.7 between them, so the top of the road is one solid bar.
- In perspective, ripples flatten sharply toward the horizon. Today the height ratio from the near row to the far row is only 1.5×.

**Change:**
- Re-space the 11 rows into 12, with these centres and heights:

  | Row | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 (brightest) | 10 | 11 | 12 |
  |---|---|---|---|---|---|---|---|---|---|---|---|---|
  | Centre y | 302.5 | 307 | 314 | 323 | 335 | 350 | 368 | 389 | 412 | 437 | 464 | 492 |
  | Height | 2.5 | 3.5 | 5 | 7 | 9 | 11 | 13 | 14.5 | 15.5 | 16.5 | 17 | 18 |

- The gaps then grow monotonically, and the brightest row sits 6 from the 431 rule.
- Opacity ramps from 0.40 at the horizon to 0.80 at row 437, then falls to 0.70 and 0.55.
- Keep the lens half-widths.
- The 12-unit minimum height (G7d) cannot be met by the horizon rows in the 512 master. At 64 px they merge into the horizon highlight anyway, which is the correct read.

### 3. Icon: the secondary glints read as leaves and fish

**File:** `plugin-icon.svg`, the right-hand glints of rows y 363, 403 and 451.

**What is wrong:**
- They are 34–54 wide and 13.5–16.5 tall, an aspect of 2.1 to 3.6, with fat bellies.
- Ripple highlights are thin dashes. This is the round-2 "leaves" note again.

**Change:**
- Each secondary glint: height ≤ 8 (about half its row's main streak), aspect ≥ 5, and opacity 0.75× the main streak.
- Alternatively, split each into two thin dashes.
- Break the strict one-two-one-two rhythm, which reads as a ladder at 64 px.

### 4. Icon: the lantern reflection is too dim and runs the wrong way

**File:** `plugin-icon.svg`, the two `#E0B860` paths in the lantern group, at local y 476 and 493 (global y ≈ 450 and 465).

**What is wrong:**
- Opacities of 0.46 and 0.30 put them at about 45% of the window's lightness. The moon road is at 85% of the moon, so the water's reflectance is inconsistent.
- They fade with distance.
- The window is about 81 units above the islet's waterline (y ≈ 432), so its mirror point is at y ≈ 513, at the bottom edge. The warm column should get brighter and longer toward the bottom frame, not die out.
- At 64 px they read as brown pebbles.

**Change:** replace them with four needle streaks centred on the window's x:

| Global y | Width | Height | Opacity |
|---|---|---|---|
| 446 | 12 | 2.5 | 0.40 |
| 457 | 16 | 3 | 0.52 |
| 469 | 22 | 4 | 0.62 |
| 482 | 28 | 5 | 0.70 |

- Use the road's needle shape and fill `#E0B860`.
- Let the last one run under the frame.

### 5. Icon: two tangents at the lantern

**File:** `plugin-icon.svg`, the lantern group (`translate(62 300) scale(.86)`).

**What is wrong:**
- **(a) Frame.** The kasa's left eave tip lands at x ≈ 22.4, about 4 units from the frame's inner edge (x ≈ 18). It kisses the frame, and at 64 px it fuses into it.
- **(b) Road.** The islet's right tip at (112.7, 432.4), and its moonlit rim stroke `M22 431C40 433 54 438 58 442`, end on the left tip of the brightest glint at (112, 427).

**Change:**
- Shift the lantern group and `r3i-lg` +10 in x (pivot 62 → 72).
- Extend the islet path's left end to local x −70 so it runs under the frame.
- Shorten the islet's right end from local x 59 to 40, and end the rim stroke at local x 38.
- Keep at least 10 units of open water between the islet and every glint.

### 6. Icon: the lantern's flame lights nothing

**File:** `plugin-icon.svg`, the lantern group.

**What is wrong:**
- The window (`#E0B860`) glows inside a pure-black silhouette.
- Real flame light falls on the nearest surfaces the viewer can see. The lantern sits below eye level (the horizon is y 300), so the viewer sees the islet's top surface in front of it.

**Change:**
- Add a warm pool on the islet top: an ellipse at local (0, 436), rx 30, ry 4, `#E0B860` at 0.25 opacity, clipped to the islet path.
- Add 1-unit jamb highlights on the inner edges of the window opening: `#E0B860` at 0.5 opacity on both sides and on the sill.

### 7. Moons: hard concentric bands read as a lemon or onion slice

**File:** `plugin-icon.svg`, inside `clip-path="url(#r3i-lc)"`.

**What is wrong:**
- The limb band (`circle r77 stroke #F4F2EA 6`), the terminator band (`ellipse rx28.8 stroke #C3CEE4 10`) and the extra hairline (`ellipse rx41.8 stroke #95A5C8`) are all crisp-edged.
- A moon has one terminator. Its brightness falls off smoothly from the limb to the terminator, with no rind and no second terminator line.
- At 1024 px this is the round-2 "lemon slice" complaint.

**Change:**
- Delete the rx 41.8 hairline.
- Run the limb band and the terminator band through `feGaussianBlur` with stdDeviation 3, inside the clip.
- Apply the same softening (stdDeviation 0.8) to the terminator and limb strokes in `ready.svg` (`#C3CEE4` 3.2) and `ready-on-another-job.svg` (`#C3CEE4` 2.6).

### 8. In journal: a phantom terminator inside the lit gibbous

**File:** `in-journal.svg`, the element `<ellipse cx="70" cy="66" rx="13.44" ry="32" stroke="#95A5C8" stroke-width="2">`.

**What is wrong:**
- The whole ellipse is stroked, so its back half draws a dark arc about 13 units inside the lit body (peaking at x ≈ 83).
- That is a second terminator that cannot exist. At hero size the moon reads as a contact lens or a mussel shell.

**Change:**
- Stroke only the front half: `<path d="M70 34A13.44 32 0 0 0 70 98" .../>`.
- Grade the body from `#E2E8F4` at the right limb to `#C3CEE4` at the terminator, instead of a flat `#C3CEE4` with a ring.

### 9. Ready: the sky gradient is upside down

**File:** `ready.svg`, `r3r-wl`.

**What is wrong:**
- `#628EDC` at the top falls to `#5079C9` at the bottom, so the sky is lightest at the zenith.
- The icon's sky (`r3i-sky`) brightens toward the horizon, as real skies do. Ready is the icon in miniature, so it must follow the same physics.

**Change:**
- Use a userSpace gradient from y 12 to y 80: `#4F78C8` at the top to `#6A95E0` at the horizon.
- Keep the sea's `#4067AF` at the horizon so the sea stays darker than the sky.
- This also lifts the crescent's contrast.

### 10. Completed: the rim ignores the key light, and the check makes a tangent

**File:** `completed.svg`.

**(a) Limb ring**
- **Element:** `<circle r="36.4" stroke="#C3CEE4" stroke-opacity=".85" stroke-width="3">`.
- **What is wrong:** the ring is uniform all the way round, while the disc beneath it follows the upper-left key light. A uniform pale rim is a plate edge or a radio button.
- **Change:** stroke it with a linear gradient along (0.2, 0.15) → (0.8, 0.9): `#E2E8F4` at 0.95, then `#C3CEE4` at 0.5 at the midpoint, then 0 at the lower right.

**(b) Check tip**
- **Element:** the check paths `M64 86L78 100L116 54`.
- **What is wrong:** the keyline cap ends at r ≈ 60.4, which is 2.8 units *inside* the r 63.2 silhouette. It is tangent to the medal edge, and the circle is not actually broken (concept.md says it is).
- **Change:**
  - Move the long-arm tip to (117.5, 40), and the highlight path's tip to (115.1, 38.3). The cap then clears the keyline by about 3 units and stays inside the viewBox.
  - Alternatively, pull the tip back to (103.5, 69.2) so it ends clearly inside the well.

### 11. Done this cycle: the inlay hangs off the moon

**File:** `done-this-cycle.svg`, the gilt path `M84.5 37.19…` (`fill="url(#r3d-cb)"`).

**What is wrong:**
- Measured from the disc centre (62, 64), the band spans r 31–39, but the limb is at r 34. It overhangs the disc by up to 5 units into the enamel.
- An inlay cannot sit off the edge of the surface it is set into.
- The overhang makes an eccentric second crescent, which gives the ")" bracket and second-moon read.

**Change:**
- Redraw it as a band set inside the limb:
  - the outer edge is flush at r 34;
  - the inner edge is at r 34 − w(θ);
  - w peaks at 5 near θ 15° and tapers to 0.6 at −50° and 80°.
- Keep the keyline and the gradient.
- If the 16 px metrics drop, widen it inward to 6, never outward.

### 12. Not checked: the light logic of the hazy moon and the mist

**File:** `not-checked.svg`.

**(a) Hazy moon**
- **Element:** `circle r30 stroke #C3CEE4 .3` (blurred).
- **What is wrong:** it makes the limb brighter than the core, so the moon reads as a bubble or a donut. A moon in haze is brightest at its core, with a corona fading outward.
- **Change:**
  - Delete the ring.
  - Fill r 31 with a radial gradient: `#C3CEE4` at 0.55 at the centre, `#95A5C8` at 0.35 at r 28, and 0 at r 40.

**(b) Wisp brightness**
- **Element:** `r3n-ws`.
- **What is wrong:** the gradient peaks at x 40.8, and the wisp's thick end lies on the rim at x −4 to 20. The mist is lit only by the moon, but it is brightest away from it.
- **Change:**
  - Move the peak stop to offset 0.61 (x 64, over the moon).
  - Drop the end stops to 0.35 and 0.30.

**(c) Wisp clipping**
- **Element:** the wisp paths, which all start at `M-4`.
- **What is wrong:** the wisp starts outside the 0–128 viewBox, so its spill end is cut by a hard vertical edge at x 0. The cut is visible at 96 px and on the sheet.
- **Change:** taper the left end to a point at x ≥ 3.

### 13. In journal: the ribbon's physics

**File:** `in-journal.svg`, the ribbon path `M24 3H39V70…` and the fold band `M24 10H39V13H24Z`.

**What is wrong:**
- The top is a flat cut at y 3. It floats 3–12 units above the silhouette with nothing to hang from.
- The fold band is horizontal, but the rim crest it crosses slopes about 30° there.
- Silk draped over a raised rim follows the rim.

**Change:**
- Make the top edge an arc concentric with the medal at r 66, as a rounded fold wrapping behind the medal. Shade the strip outside r 63.2 `#3F5A98`.
- Replace the horizontal band with two curved bands that follow the rim:
  - a crest highlight at r 58.5–60.5, `#A9BEEA` at 0.5 (it faces the light);
  - an inner-slope shade at r 53–56, `#080B16` at 0.3 (where the ribbon drops into the well).
- The silhouette break stays.

### 14. Icon: a near-tangent filigree, and the horizon line crosses land

**File:** `plugin-icon.svg`.

**(a) Filigree**
- **Element:** the inner arc `M40 118C40 78 78 40 118 40` and the outer arc `M30 132…`.
- **What is wrong:** the inner arc comes within 16 units of the earthshine disc and runs nearly concentric with it. The outer arc comes within 23 units. Together they form an echo and tangent around the moon.
- **Change:**
  - Delete both inner arcs.
  - Redraw each outer arc as `M30 110C30 64 64 30 110 30`, with its crescent finials at (30, 116) and (116, 30). That leaves at least 27 units of clearance.

**(b) Horizon hairline**
- **Element:** `r3i-hz`.
- **What is wrong:** it is painted after the headland, so a 41% → 6% bright line runs across the foot of the land from x 300 to 512. The far-water glint lies behind the headland and must be hidden by it.
- **Change:** draw the hairline before the two headland paths.

## Optional polish

1. **Rim value.** On Blocked and Not checked, the gilt rim is the brightest large area, which gives "a gold ring with something inside".
   - Darken the shared ramp's base and mid stops by about 8% on all eight (for example `#A88B52` → `#9A7E4A` and `#8A6F3E` → `#7C6236`). One colour is kept, and the emblems lead.
2. **Ready's bezel end faces.** Light the right arm's end face (it faces the light) with a `#D9BE82` hairline, and keep the left arm's end face dark. Add a 1.5-unit soft shadow from the left arm's end onto the sea.
3. **Rosettes.** At 96 px they read as screw heads. Draw them as incised (dark upper-left wall, light lower-right wall), or keep only two.
4. **Hatching** (Blocked, Ready on another job, Done, In journal). The masks make it stronger toward the lit side, so it reads as sheen or moiré. Make it uniform, or hero-only at 0.15.
5. **Completed check.** It is the second-loudest mark in the row. Set its body from `#D9BE82` to `#A88B52` so Completed recedes further.
   - Draw the moon-daisy as incised two-tone instead of light lines, which read as embossed.
6. **Locked out.**
   - The highlight stop `#E4766A` drifts toward salmon at hero size; `#D8645A` keeps Dalamud's crimson.
   - Add a light hairline on the lower arc of the black socket (`circle r36`): the lit lower wall of a recess.
7. **Islet reflection** (`M-46 455H56…`). It is 11 tall, but the islet is 23. Make it 23, and add a faint (0.2) broken dark column below it for the post.
8. **Blocked's crescent.** Its lit width is 13.3 of 70, about a 4-day moon. For "thinnest limb", set the terminator ellipse to rx ≈ 28.
9. **Ribbon shadow.** A uniform drop shadow (1.1, 1.6) implies the ribbon floats. Tighten it to dy 1.0 where the ribbon lies flat on the enamel.

## What is right

**The owner's asks**
- **The aetheryte crystal is gone.** The scene's only emitters are now the moon and the lantern, and the unlit headland is the right "next destination".
- **One border.** All eight bezels and the icon frame share one gilt ramp and one two-slope build.

**Light on metal**
- **The bezel is correct, and identical on all eight.**
  - The outer slope is bright at the upper left.
  - The inner slope is dark at the upper left and bright at the lower right, as a convex turned rim should be.
  - A strong specular sits on the upper-left outer slope, with a weak one on the lower-right inner slope.
  - The well's shadow sits inside the upper-left edge.
  - Emblem shadows fall down and to the right (1.1, 1.6).
- **The icon frame** follows the same ramp and casts its shadow onto the scene along the top and left edges only. Correct.

**Glyph emblems**
- **The soul crystal.** Its facet values follow the key light: the table is lightest, the right facet darkest, and the setting goes from gilt light at the upper left to dark at the lower right. Its violet (about 290°) is analogous to the lapis, so it is harmonious, not garish.
- **Locked out.**
  - The fracture walls are lit correctly: the right half's wall is light and the left half's wall is dark.
  - The crack survives greyscale.
  - The red is not garish.
- **The check's bevel.** The highlight sits on the long arm's upper-left face, with a keyline and a down-right drop shadow. Correct.

**The icon's reflection**
- The road axis is at 170.5 against the lit centroid's 172.
- The road is in the moon's hue, at a peak of 85%.
- The brightest row sits near the mirror point, and the lens widens toward it.
- The sea is darker than the sky at the horizon and darkens toward the viewer, which is correct Fresnel behaviour.
- The headland's mirror lies directly below it and is darker than the water.
- The earthshine at 1.12:1 is plausible.
- The lantern is backlit, with its moonlit rims on the moon side.

**Colour**
- The warm champagne gilt against the cool lapis is a near-complementary pair held below C 0.09: rich, not brassy.
- Ready leads in Night and in greyscale.

**Against round 2**
- The check has moved to Completed.
- The wisp replaces the hamburger bars.
- The crack is thicker.
- The road clears the Installed corner.
