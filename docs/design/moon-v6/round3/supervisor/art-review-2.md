# Art review 2: Menphina's Medallion, round 3, after the fixes

**Reviewer:** art supervisor
**Subject:** `round3/medallion-r3/` after the designer applied the 14 required fixes from `art-review-1.md`, the mock reviewer's four notes, and polish items 1, 2, 4, 5 and 7.

**Evidence:**
- The new `_sheet.png`.
- Supervisor renders in the scratchpad, `sup-r3/`:
  - all eight glyphs at 512 px (`r2big1.png`, `r2big2.png`);
  - the icon at 1024 px;
  - the icon at 64 px, magnified ×8 with pixelated scaling;
  - two 6× crops of the lantern (`r2lant.png`, `r2lant2.png`).
- The SVG source, read only to name elements.

## Verdict: CHANGES REQUIRED (one small item)

Thirteen of the 14 required fixes are done and verified, and fix 6 is done in part. One shadow-logic error remains, which fix 6 introduced. It is a one-element change.

Once it is applied, the set is approved with no further full review. A spot check of that one element is enough.

## Required fix

### 1. The lantern's warm pool lights the ground the chūdai shades

**File:** `plugin-icon.svg`, in the lantern group:

```
<ellipse cx="0" cy="436" rx="30" ry="4" fill="#E0B860" fill-opacity=".25" clip-path="url(#r3i-ic)"/>
```

**What is wrong:**
- The pool is centred on the kiso base's bottom edge (local y 436), and the base is drawn over it. The visible part is therefore a hard, even ring around the foot: 3 units on each side of the base, and 4 units below it.
- Seen whole, it reads as a brass saucer rim, which you can see at 512 and 1024 px.
- Physically, the light comes from the window (local y 348–371). The chūdai platform sticks out about 6 units past the firebox, just below the window. It blocks every steep downward ray, so the foot of the base and its sides sit in the chūdai's shadow.
- Window light can only reach the islet's top some distance in front of the base. So there must be a dark gap between the base and the pool, and nothing should be lit at the base's sides.
- The flat fill also gives the pool a hard edge. A pool of light fades out at its edges.

**Change:**
- Replace it with a pool detached from the base and centred in front of it:
  - `<ellipse cx="0" cy="441.5" rx="22" ry="2.6" fill="url(#r3i-pool)" clip-path="url(#r3i-ic)"/>`
  - `r3i-pool` is a radial gradient: `#E0B860` at 0.32 at the centre, 0 at the edge.
- The base's bottom (y 436) then has about 3 units of unlit ground before the pool begins.
- Leave the jamb and sill line as it is.

## Verified from round 1

| # | Fix | Status |
|---|---|---|
| 1 | Crescent mirror slices deleted from the icon and Ready | Done. The road alone carries the reflection. |
| 2 | Road kept below the horizon and foreshortened | Done. Row 1 is a 2.5-unit sliver under the hairline, and rows get taller and further apart toward the viewer. At 64 px the road is a clean broken column, not a solid bar. |
| 3 | Secondary glints | Done. They are thin dashes, and the irregular row counts remove the ladder read. |
| 4 | Lantern reflection | Done. Four warm needles directly under the window (x 72) get brighter and longer toward the mirror point at the bottom edge. They are clearly warm light on water at 64 px. |
| 5 | Lantern tangents | Done. The eave is 14 units clear of the frame, the islet runs under the frame, and there are about 10 units of water between the islet and the road. |
| 6 | The flame lights its surroundings | The jamb and sill lights are fine; at 512 the hairline gap between them and the glass is sub-pixel. The pool is wrong: see the required fix above. |
| 7 | Lemon-slice bands | Done. The crescent shades smoothly from limb to terminator with one terminator, and at 1024 px it reads as a moon, not a fruit. Ready's and Ready on another job's crescents are softened too. |
| 8 | In journal's phantom terminator | Done. A graded gibbous with one terminator, and the seam is gone. |
| 9 | Ready's sky | Done. It brightens toward the horizon, the sea stays darker than the sky, and the crescent gains contrast. |
| 10 | Completed's limb ring and check tip | Done. The limb band fades toward the lower right with the key light, so there is no plate rim. The check's cap clearly crosses the silhouette at the upper right, so the tangent is gone. |
| 11 | Done's inlay | Done. It is flush inside the dark limb and reads as a gilt edge set into the moon, not a second crescent. Widening it inward to 6 is acceptable. |
| 12 | Not checked | Done. The hazy moon is brightest at its core and fades into a corona. The wisp peaks over the moon that lights it and fades both ways. Its left end tapers inside the viewBox, so there is no hard cut. |
| 13 | Ribbon | Done. The top folds behind the medal along an arc, there is a lit band on the crest and a shaded band on the inner slope, and its soft shadow lands on the rim and enamel. |
| 14 | Filigree and horizon line | Done. A single corner hairline sits well clear of the moon, and the headland now hides the horizon line. |

## The mock reviewer's notes and the polish

**Locked out in rose-crimson** (`#DA808A` / `#BA5462` / `#742C3C`)
- This is a defensible harmony move toward Eclipse rose.
- Its saturation is disciplined, and its value still separates it from Blocked in greyscale.
- The wall hairlines (`#E8A0AA` lit on the right half, dark on the left half) still obey the key light.
- Not a realism problem. Polish note: at hero size the upper-left highlight reads milky pink rather than "red moon". If the owner wants more Dalamud, `#CF6E7A` as the top stop keeps the rose family and adds body.

**The ribbon's soft shadow:** correct. The direction is down and to the right, and it is tighter than an emblem's shadow, which suits silk lying flat.

**Completed at 0.71× Ready, with a gilt check:** fine. The check's bevel and keyline are unchanged and correct.

**Polish 1 (darker rim ramp):**
- The emblems now lead on Blocked and Not checked.
- There is still one gilt colour across all nine assets.
- The warm champagne against the cool lapis is still a clean near-complementary pair.

**Polish 2 (Ready's end faces):** correct. The right arm's end face, which faces the light, is lit, and the left arm's is dark.

**Polish 4 (uniform hatching):** correct. The sheen read is gone.

**Polish 7 (islet reflection):** correct. It is now as tall as the islet and sits directly below it.

**Polish 8 (thinner Blocked limb), reverted:** accepted. Distinctness wins over a semantic nuance; this was never a realism issue.

## Remaining optional polish (non-blocking)

1. **Ready on another job's soul crystal.** Add light entry and exit: a sharp specular on the table's upper-left vertex, and a soft transmitted glow (`#A88CEB` at about 0.4) at the bottom of the dark right facet. It would read as crystal rather than painted stone at hero size.
2. **Not checked.** Lift the hazy moon's core slightly (`#C3CEE4` at 0.55 → about 0.65). The veiled moon would then stay the brightest point where the wisp does not cover it.
3. **Rosettes and the moon-daisy.** Draw them incised (dark upper-left wall, light lower-right wall) instead of as plain lines. These are polish items 3 and 5 from review 1.
4. **Ready's left bezel arm.** It could cast a 1.5-unit soft shadow onto the sea in the opening.

## What is right

**Light and metal**
- **One key light everywhere.** Every bezel, the frame, the check, the crystal setting, the inlay and the ribbon are lit from the upper left. Recesses are reversed correctly: the well and the fracture.
- **One cohesive gilt rim,** correctly built as a two-slope turned metal with consistent speculars.

**The icon's water**
- The moon road sits under the lit centroid, in the moon's hue, at about 86% of the moon.
- It is broken into ripples that foreshorten toward the horizon.
- Its brightest row sits at the mirror point.
- The sea is darker than the sky at the horizon and darkens toward the viewer.
- The headland's and the islet's mirrors lie directly below them.
- The lantern has its own warm column.
- The earthshine is plausible.

**Moons and materials**
- **Every moon is now shaded like a moon:** soft terminators, a single terminator each, and key-lit limb bands.
- **Materials read true:**
  - the gilt reads as metal;
  - the enamel as glassy and flush;
  - the silk ribbon drapes;
  - the mist is lit by its moon;
  - the water is broken specular, not leaves.

## Final verdict, after the spot check: APPROVED

I spot-checked the one remaining fix:
- **Source.** `plugin-icon.svg` now has `<ellipse cx="0" cy="441.5" rx="22" ry="2.6" fill="url(#r3i-pool)" clip-path="url(#r3i-ic)"/>`. `r3i-pool` runs from `#E0B860` at 0.32 to 0.
- **Renders.** I checked the coordinator's `pool.png` and my own 6× crop, `r3lant2.png`.

**Result:**
- The pool now sits in front of the kiso base.
- A strip of unlit ground separates it from the base's bottom edge, which is the chūdai's shadow.
- Nothing is lit at the base's sides.
- It fades softly at its edges, with no rim.
- The saucer read is gone, and the flame's light now obeys its occluders.

With the 14 round-1 fixes verified above, every glyph and the icon pass for light, shadow, reflection, colour theory and material. The optional polish listed above stays optional.
