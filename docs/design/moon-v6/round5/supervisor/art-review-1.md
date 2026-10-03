# Art review 1: Menphina's Medallion, round 5

**Reviewer:** art supervisor (realism: light, shadow, reflection, colour, material)
**Subject:** `round5/medallion-r5/`, against `../brief.md`, with the design critic's `critic.md` taken into account

**Evidence:**
- `_sheet.png` and `_variants.png`.
- Supervisor renders in the scratchpad, `sup-r5/`:
  - every glyph and all three Blocked candidates at 256 and 512 px (`g256.png`, `a.png`, `b.png`);
  - Blocked b at 1024 px (`z_bb.png`);
  - the Ready, Blocked and In journal badges at 16× (`badge_*.png`);
  - the icon at 512 and 1024 px, two 4× shore crops (`z_shoreL.png`, `z_shoreR.png`), and the icon at 32 and 64 px, magnified with pixelated scaling (`small.png`).
- Pixel sampling of the renders.
- The SVG source, read only to name elements.

## Verdict: CHANGES REQUIRED (eight items)

The round does what the owner asked, and most of it is physically sound:
- The Kugane shore's reflections all hang directly below their sources, at mirror distance.
- The shore's moonlit edges face the moon.
- The road's brightest row sits at the mirror point.
- The badge frame is one system.

The open items are:
- three realism errors: a silver lining where no light is, a pale seam under the far shore, and an asymmetric glitter path;
- the badge glyphs' material;
- the critic's points that I endorse.

## Blocked: the decision

**Ship b**, the new moon behind cloud (`blocked.svg` = `blocked-b.svg`), with fixes 1–3 below. a and c are retired.

- **b is the right physics for "obscured, darker".**
  - A new moon gives almost no light, so the medal *should* be hushed.
  - The only bright thing left is a thin sunlit limb glimpsed between banks.
  - That is the most obscure and the darkest of the three without becoming muddy, and it is the only one that passes the 16 px row tier (12.3).
- **a** reads as round 4 one step darker, and fails the row tier (11.8).
- **c**'s stratus lobes read as a smear on the enamel rather than as a cloud deck. It also fails the row tier (11.2).
- I agree with the critic's ranking, b > a > c, and I make the critic's tweak required. The dimming is a realism point, not taste: with a new moon, the clouds' highlights should not rival the only light source in the sky.

## Required fixes

### 1. Blocked: the silver lining is clipped to the whole disc, so it lines clouds over the unlit part

**File:** `blocked.svg` (`blocked-b.svg`), clip paths `r5bb-amc` and `r5bb-bmc` (both `<circle cx="72" cy="44" r="25"/>`).

**What is wrong:**
- A silver lining is sunlight scattered through a cloud's edge from a bright source behind it.
- Over the ashen, earthlit part of a new moon there is no such source. Yet the `#DCE2EE` .25 dilate ring is drawn wherever a cloud edge crosses the disc.
- At 1024 px it rims the cloud lobes over the dark disc, sampled at (57, 68, 109) against the ashen disc's (59, 70, 111). That is backlighting with no light behind it.

**Change:** make both clips the lit crescent instead of the disc: the same path as `r5bb-lc`, `M72 19A25 25 0 0 1 72 69A15.5 25 0 0 0 72 19Z` with `rotate(-12 72 44)`. The lining then appears only where a cloud edge crosses the sunlit limb.

### 2. Blocked: dim the clouds' lit tops by about 12%

**File:** `blocked.svg`, the clouds' key-lit highlight stops and lit rims (both banks).

**What is wrong:**
- The cloud highlights sample at (135, 146, 179) and are the brightest *large* mass on the medal.
- On a new-moon night that reads as "cloudy", not "dark". The value budget belongs to the thin limb (216, 224, 239).
- This is also the owner's "moon a bit darker, more obscured".

**Change:**
- Scale the highlight and lit-rim colours about 12% toward the cloud body tone. The target is lit tops at about L 0.52 instead of 0.58 (OKLab).
- Leave the bodies, undersides and cast shadows unchanged.
- **Guard:** keep the 16 px weakest pair at 12 or more (shipped and row tier). If Blk-Lock drops under 12, keep fix 3 and reduce the dimming until it holds.

### 3. Blocked: one thin wisp across the limb

**File:** `blocked.svg`, the upper bank.

**Why:**
- Unbroken, the limb reads as a clean Ready-style crescent peeking out.
- Broken once by cloud, it reads as "glimpsed": the obscurity the owner asked for.

**Change:**
- Add one thin stratus wisp from the upper bank across the limb, about a third of the way down. It is 2–2.5 units thick and tapers at both ends.
- **Material: thin cloud is translucent, not relief.**
  - Fill `#46507A` at about .55 over the limb, so the limb shows through dimmed.
  - Give it no key-lit top and no drop shadow.
  - Add a faint `#DCE2EE` .25 lining on its upper edge, only where it crosses the lit limb (clip as in fix 1).

### 4. Badge glyphs: model them as metal in the job-glyph idiom

The critic's change 2, endorsed as a material requirement.

**Files and elements:**
- the glyphs inside the Ready, Blocked and In journal badges;
- `_row/badge-open.svg`, `badge-closed.svg` and `badge-journal.svg`;
- the glyph builders in `_src/gen5.py`.

**What is wrong:**
- At 16× the locks and the book are flat fills inside a heavy near-black keyline (about 2 units). A keyline is ink, not metal.
- The shackle is a flat stroke with one highlight line, which reads as a tube cut from paper.
- The keyhole is a flat black decal, not a hole.
- The book's page lines each carry a dark lens beneath, so they read as slots or gills cut through the page.
- The game's job glyphs on the neighbouring badges are embossed metal with a soft bevel and a soft dark under-glow, so the set mixes two idioms.

**Change:**
- **Drop the black keyline.**
  - Replace it with a soft under-glow: `#080B16` at .5, offset (0.6, 0.9), blur 0.8, sRGB.
- **Shackle:**
  - Shade it as a cylinder: a light stripe along the side facing the upper left, which follows the bend, and a darker band on the far side.
  - Where the open shackle's free leg ends, give it a rounded lit cap.
- **Body:**
  - Model it as a slab: a lit upper and left bevel (1 unit), a shaded lower and right bevel, and a face gradient from the upper left to the lower right.
- **Keyhole:**
  - Make it a recess: dark fill, with a 0.4-unit lit hairline on its lower-right inner wall (the recess rule).
- **Book:**
  - Draw each text line as a fine incised line: a 0.5-unit darker stroke with a 0.3-unit light hairline on its lower-right side. Delete the dark lens shapes.
  - Keep the page curvature, and keep the board as a darker metal edge rather than a black outline.
- **Size:** scale each glyph to about 60% of the seat's height, about 24 units on the r 19.9 seat, to match the job glyphs' envelope. This also stops the open lock from being the second-loudest mark on Ready.
- Keep the colours: warm gilt for the open lock and the book, cool pewter for the closed lock.

### 5. Locks: one body position for the pair, and a visible open gap

The critic's change 3, endorsed.

**File:** the lock builders in `_src/gen5.py`.

**What is wrong:**
- Each lock is centred on its own bounding box, so the open lock's body sits about 2.2 units higher than the closed lock's (measured at 16×). Ready and Blocked rows then show the body jumping.
- At 32 px the gap between the free leg and the body is under 1 px, so open versus closed rests on colour alone.

**Change:**
- Centre the closed lock optically, and give the open lock the identical body position. Its raised shackle uses the headroom.
- Lift the open shackle until the free leg clears the body by at least 4 units (at least 1 px at the 32 px medal).

### 6. Icon: a pale seam under the far shore

**File:** `plugin-icon.svg`.

**Elements:**
- `<rect x="0" y="254" width="512" height="2" fill="url(#r5i-hz)"/>`;
- `r5i-near` and `r5i-far`, whose bases sit at y 255.

**What is wrong:**
- The bright horizon hairline is the far water's glint, which lies *behind* the shore.
- The shore's waterline sits at y 255, mid-hairline, so the hairline's lower half (y 255–256, up to about .5 opacity under the inner ends of the quays) shows as a pale seam beneath both shores. At 4× the town floats on a light line.
- A shore and its mirror must meet with nothing bright between them.

**Change:**
- Extend both shore silhouettes' bases to y 256.5 (H + 1.5): a far shore's waterline sits just below the true horizon.
- Mirror the shore reflections about that waterline instead of y 255.
- Alternatively, clip `r5i-hz` to the open gap (x 206–306) only.

### 7. Icon: restore the moon's size, and clear the horn and horizon tangent

The critic's change 1, endorsed.

**File:** `plugin-icon.svg`, the moon, horizon and shore placement in `gen5.py` (`ICON_H`).

**What is wrong:**
- The crescent's lower horn is 8 units above the horizon. At 64 px it touches the shore line, so the moon looks perched on the town.
- The moon (r 56) is smaller than round 4's, which the owner liked. At 32 px the road is a grey haze.

**Change:**
- Raise the horizon by 10 (`ICON_H` 255 → 245) and the moon by 20, so the lit centroid is at about y 182.
  - The mirror row (2H − lit y) stays at about 308, above the Installed corner's y 312.
  - The horn clears the horizon by 18 or more.
- Grow the moon to r 62–64 and thicken the crescent to k −0.22.
- Move the shore, the lantern-line lights and their reflections with the horizon, keeping each reflection at mirror distance about its waterline (fix 6).
- Let the top three or four road rows carry the brightness, so the road reads as a bright line at 32 px.

### 8. Icon: the glitter path must be symmetric about the moon

**File:** `plugin-icon.svg`, the road streaks (the road generator in `gen5.py`).

**What is wrong:**
- Glitter is symmetric about the moon's azimuth (x 256) on wind-ruffled water.
- The measured axis is 250.8 because the chips were biased left on purpose. At 1024 px the left chips stack into a regular vertical column (about x 178–215 at 512 scale) that reads as a second, parallel road.
- No wind or optical cause could produce that.

**Change:**
- Distribute the chips on both sides of x 256 so the measured axis is within ±1.5.
- Stagger chip x positions row to row by ±8, so no column forms.
- Keep the Installed corner quiet through the existing vertical fade (0.94 → 0.74), not through lateral bias.

## The critic's other points

| # | Point | Supervisor |
|---|---|---|
| 4 | Keep the book, not the game's "!" | **Agree.** Fix 4's metal treatment applies. A closed leather tome with gilt corners is an optional variant. |
| 5 | A neutral night-enamel seat for state badges | **Optional.** Not a realism issue. Adopt it only if Ready stays at 1.3× or more. |
| 6 | Warm three or four lantern points toward Kugane red-orange | **Optional, physically fine:** chōchin paper glows red-orange. If adopted, each light's reflection must take the same hue directly below it, at about 0.8× its value. |
| 7 | Job icons need no action | **Agree.** The visible-pixel centring is the right method. White Mage's stem on the axis is the right optical choice; +0.3 x is optional. |

## Realism checks that pass

**The icon's Kugane shore**
- **Silhouettes:** read cleanly against the sky, with paler far hills for atmospheric perspective.
- **Moonlit edges:** the left shore is lit on its right edges and the castle on its left, because the moon is between them and behind.
- **Reflections:**
  - Every lantern point and the castle window has its warm reflection directly below, at mirror distance (castle window at about +84 above the horizon, reflection at about +84 below).
  - The pagoda's and the castle's dark mirrors match their heights and sit directly below them.
- **Horizon:** the sea is darker than the sky at the horizon (.483 against .488).
- **Road:** with the moon low and centred, the road is correctly long and narrow, brightest at the mirror point just under the horizon, and widening toward the viewer.

**The clouds over the new moon (b)**
- The relief clouds are lit from the upper left: billow highlights and flat dark undersides.
- Their shadows fall down and to the right onto the limb and the enamel, in sRGB.
- The only faults are fixes 1–3.

**Badge frame**
- One gilt two-slope ring and one down-right shadow on all four states, and the seats are recessed with an inner shadow at the upper left. Correct.

**Everything else**
- The rest of the set is round 4, approved and unchanged: Done, Completed (gold only), Locked out, Not checked and Ready on another job.

## Optional polish

1. **Icon moon colour.** A moon this close to the horizon is warmed and dimmed by the longer air path. A faint warm cast on the lower limb (`#F4E6CC` at about .15, blurred) would add realism. If fix 7 raises the moon, this matters less.
2. **In journal** is getting busy, with a ribbon at the upper left and a badge at the lower right. If the owner says so, shorten the ribbon's tail, as the critic suggests.

## Re-review after the fixes: CHANGES REQUIRED (one small item)

I re-rendered the regenerated files in the scratchpad, `sup-r5/` (the `v2_*.png` files):
- the icon at 1024 px, plus 4× shore crops, and at 32 and 64 px magnified with pixelated scaling;
- Blocked at 1024 px, plus a nearest-neighbour crop of the wisp (`v2_wisp.png`);
- the Ready, Blocked and In journal badges at 16×.

### Verified

| # | Fix | Result |
|---|---|---|
| 1 | Silver lining | The lining is clipped to the sunlit limb. No lining remains over the earthlit disc. Correct. |
| 4 | Badge glyphs as metal | No ink keylines remain. The locks' shackles are shaded as cylinders that follow the bend, the bodies are bevelled slabs, and the keyholes are recesses with a lit lower-right wall. The book's text lines are incised, its pages are bevelled, and its board is darker metal. All sit on a soft under-glow and match the job-glyph idiom. Correct. |
| 5 | Locks as a pair | The two bodies share one position, and the open gap is 4.2 units. Correct. |
| 6 | Shore seam | No light seam remains under either shore. The hairline lives only in the gap, and the mirrors and lantern reflections are taken about the waterline. Correct. |
| 7 | Moon size and tangent | r 63, with the horn 25 above the horizon, so the tangent is gone. At 32 and 64 px the moon is a clear shape and the road is a bright line. Correct. |
| 8 | Road symmetry | Chips sit on both sides, staggered, with no column. The axis is 254.6, within tolerance. It reads as glitter. Correct. |
| Opt. | Chōchin lanterns | Each red-orange light has its reflection in the same hue, directly below it at mirror distance. Correct. |

### The two questions

**Is the 6% cloud dimming acceptable? Yes.**
- The cloud tops are now at L .72 against the limb's about .93, so the limb is clearly the only light.
- The wisp (once fixed below) carries the extra obscurity.
- The guard was honoured: Blocked vs Locked out is 12.0 at the row tier.

**Is the road's brightness in the Installed corner acceptable? Yes, for realism.**
- The road is a continuous glitter path. Its brightness is set by the moon's altitude and the mirror point, which is at y 303, above the corner.
- Suppressing it inside the corner would break the reflection physics, which is the thing the owner asked to protect.
- 0.97× at y 312, fading to 0.72× by y 440, is the physically correct falloff.
- Whether the Installed check stays legible over it is a functional question. Check it in the real installer. If it fails, fix it with the check's own backing, not by darkening a physically correct road.

### Required fix

**9. `blocked.svg`, the stratus wisp** (the `#46507A` .55 path starting `M64 38…`).

**What is wrong:**
- **Its edges are knife-sharp.** At 1024 px it reads as a blade or slash across the crescent, a hard cut that also echoes Locked out's fracture language. Thin cloud has feathered edges.
- **It is painted across the small cloud's lit lobe.** That draws a stripe over the cloud's face instead of a wisp trailing out of the bank.
- **It continues past the limb onto the night enamel as a pale pointed tip.** Off the moon, a thin wisp has no light behind it, so it should vanish against the sky, not show lighter than it.

**Change:**
- Blur the wisp with stdDeviation 0.5 (sRGB) so both edges feather.
- Draw it beneath the small cloud in z-order, so it emerges from the bank's lower right.
- Fade its opacity to 0 within 3 units outside the lit limb, using a gradient mask along its length.
- Keep its thickness, position and lining.
- Re-check that Blocked vs Locked out stays at 12.0 or more.

When fix 9 is in, the round is approved with a spot check of that element only. No further full review is needed.

## Final verdict, after the spot check of fix 9: APPROVED

I checked `blocked.svg` at 1024 px, with a nearest-neighbour crop of the wisp (`sup-r5/v3_z_bb.png` and `v3_wisp.png`):
- **Soft edges.** The wisp is now a feathered band of thin cloud across the limb. It no longer reads as a slash or a crack.
- **Emerges from the bank.** It comes out from under the small cloud, with no stripe across the lit lobe.
- **Vanishes off the moon.** Beyond the limb, its pixels sample as plain night enamel, (26, 39, 82–83), with no pale tip.
- **The limb reads as glimpsed through cloud,** which is the obscurity the owner asked for.
- **Distinctness holds.** Blocked vs Locked out is 12.0 at row size and 13.0 as shipped.

Round 5 passes for light, shadow, reflection, colour theory and material:
- all eight required fixes and fix 9;
- the chōchin lights;
- the unchanged round-4 states.

**APPROVED.**
