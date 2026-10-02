# Astrologian's Orrery (round 2)

## Pitch

Every state is a moonstone moon set in a small Sharlayan instrument: a keyline, a two-arc bevel rim and a hairline astrolabe scale. The detail comes from how the moon is made, not from its surface. The lit part is pale, cool cut moonstone with one facet split and a crisp band along the terminator. The dark part is lapis earthshine, and on two states it carries engraved constellation hairlines. The metal is gilt only where the state calls you to act, and pewter everywhere else. At row size the instrument falls away and only a clean moon-family silhouette is left. At 96 px you see the ticks, the engraving, the bevel streak and the ribbon stitching.

The plugin icon is the Ready glyph scaled up. It is a gilt astrolabe ring with twelve notches, Menphina's moon, Dalamud as a small rose companion, and a moon road that is an actual reflection of that moon.

## FFXIV idioms borrowed (original geometry only)

- **Gilt bevel as metal.** A light–dark two-arc rim with one specular streak, the same build as job and role icon rims.
- **Armillary and astrolabe ticks.** 24 hairline ticks on each glyph rim, and a 5° limb scale plus 12 notches on the icon ring.
- **Astrologian constellation hairlines.** Dots joined by 1-unit lines, engraved into earthshine only, never on a lit area.
- **Aether crystal.** The job crystal in Ready on another job.
- **Moon lore.**
  - Menphina's full moon is Completed.
  - Dalamud's red is Locked out, and the rose lesser moon sits in the icon.
  - The 12 notches stand for the twelve moons of the Eorzean year.
- **Quest-marker grammar.** A dark keyline on every mark, plus an outer glow on the one act-now state, Ready.
- **Hingashi idioms.**
  - An ofuda sealing strip on Locked out.
  - Kasumi mist bands on Not checked (oborozuki).

## Tier plan

| Tier | Size | What shows |
|---|---|---|
| Row | 20 px and under | Keyline, a two-tone moon, and one state mark (road, crystal, ribbon, check, strip or mist). Hairlines fall below 0.3 px and disappear. At most 3 masses. |
| Mid | 24–40 px | Adds the bevel rim (light top-left, shadow bottom-right), the facet split across the lit part, and the terminator band. |
| Hero | 48 px and up | Adds the 24-tick astrolabe scale, the gilt specular streak, constellation engraving in earthshine (Ready and Blocked), the ribbon stitch and its seal crescent, ink marks on the ofuda, and Ready's moonlight glow. |

Flair Full maps to Hero, Quiet to Mid and Plain to Row. All hero detail is at most 1.3 units wide and at 0.4–0.65 opacity, so the single SVG never turns to mush at 16 px.

## State metaphors

| State | Metaphor |
|---|---|
| Ready | The icon in miniature: a gilt astrolabe moon, waxing gibbous with its lit limb tilted 30° down-right, over its broken moon road (one moonstone glint and one gilt glint), with a soft moonlight glow. |
| Ready on another job | A first-quarter moonstone moon with an aether job crystal set in its earthshine half. |
| In journal | A thick waxing crescent with an ember journal ribbon tucked in from the top-left. A wax-seal crescent appears at hero size. |
| Blocked | A new moon: an ashen earthshine disc with a thin bright limb ("not lit yet"), engraved with charted stars. |
| Done this cycle | A waning half (it comes back) with a gilt check in the dark half. |
| Completed | A quiet full moon in muted moonstone with an engraved gilt bezel. No halo. |
| Locked out | Dalamud's blood moon: a copper-dark disc with a red limb. A paper ofuda is pasted off-centre and near-vertical, with notched ends past the edge. There is no ring and no 45° slash. |
| Not checked | Oborozuki: a hazy faint moon with two kasumi mist bands drifting past its edge. |

There is no four-point star anywhere. I could not give it a meaning that the moon road does not already carry, and it would add the "AI sparkle" read.

## Icon composition (512 master)

**The medallion is the silhouette.** There is no rounded-square frame. At 64 px a gilt square frame plus an astrolabe ring makes a double border that eats about 20% of the width, and the moon falls to around 7 px. With the ring as the outline, the moon gets 8.5 px of radius, and the gilt ring itself becomes the FFXIV bevel signal. A circle is also distinct among the rounded-square plugin icons, and it matches the Ready glyph exactly (T4). The transparent corners mean the Installed check sits over quiet sea and ring only.

| Element | Value |
|---|---|
| Ring | Outer r 250, well r 222. 3.2-unit #6B5124 keyline, then a gilt gradient band, a top-left specular arc and a dark inner line. 60 hairline limb ticks at 0.42. 12 slot notches, with the 4 cardinals longer. |
| Greater moon | Centre (160, 150), r 68. Waxing gibbous, terminator semi-axis 0.5 r. Rotated so the lit limb faces 30° below horizontal. Earthshine #42548D carries 5 engraved stars at 0.3. |
| Lit centroid x | 172.5 |
| Road axis x | 166.2. The weighted centre of the glints is 166.8, which is 6.8 from the disc centre and 5.7 from the lit centroid, so it satisfies both readings of G7(c). |
| Horizon y | 278. A 2-unit line at 0.2, brightened (moon colour at 0.45) for 104 units where the road meets it. |
| Brightest point y | 406, which is H + (H − moon y). The rows at y 404–418 peak at 0.92 opacity. |
| Mirrored moon | A true reflection about the horizon, y-scaled by 0.30. It sits at y 296–337 (18 units below the horizon) and includes the earthshine part. It is cut into 3 ripple slices offset −2.5, +3 and −1.5. The lit side is on the right. |
| Glints | 14 round-capped glints in 6 rows (1, 2, 3, 3, 2, 3). Heights 12→16 toward the viewer, gaps 6→9, widths 16–60. The lens is 44 wide at the horizon, about 124 at the brightest point and narrower in front. Ragged and asymmetric. |
| Glade colour | #E6EBEE, the area-weighted mean of the lit moon. The moon is pale, so the glade is never gold. |
| Dalamud | Centre (354, 102), r 17. Rose #D89A90, with the same phase and light direction as the greater moon. Its own faint glint (0.24) sits under it, just below the horizon, above the check zone. |
| Quiet corner | Nothing but dark sea and ring falls in x ≥ 248, y ≥ 312. |

## Palette tokens

| Token | Values |
|---|---|
| `sumi` (keyline) | #0B0F1C |
| `gilt` | deep #6B5124, dark #A88B52, mid #C9A766, light #E9D49C, spec #FFF3D1 |
| `moonstone` (Ready and icon only) | hi #F3F0E6, lit #E2E8F4, band #C3CEE4, mid #95A5C8, low #5E6E97 |
| `moonstone-quiet` (non-act states) | lit #C9D2E6, facet #B4C0DA, band #95A5C8 |
| `pewter` | light #C3CEE4, dark #5E6E97 |
| `earthshine` | #24335F in glyphs, #2C3A66 on Blocked, #42548D in the icon |
| `aether` | hi #9BE6FF, mid #4FB3EA, deep #1F5FA8 |
| `ember` (ribbon) | #E7A35C |
| `blood` | lit #D2584E, deep #8E2A2E, body #4A1820 |
| `paper` (ofuda) | #EFE4CC |
| `glade` | #E6EBEE |
| `dalamud-rose` | #D89A90 |

`moonstone-quiet` and `glade` are the two new tokens. Everything else comes from the research palette.

## Self-assessment against the gates

### Metrics (final)

```
orrery 16px grey: weakest pairs RoJ-Jrn 13.9, Jrn-Blk 16.2, Done-Comp 20.5 | salience Rdy=70 RoJ=51 Jrn=45 Blk=31 Done=52 Comp=50 Lock=32 NotC=30
orrery 16px deut+grey: weakest pairs RoJ-Jrn 14.0, Jrn-Blk 16.4, RoJ-Blk 20.5 | salience Rdy=70 RoJ=51 Jrn=45 Blk=31 Done=52 Comp=50 Lock=33 NotC=30
orrery 20px grey: weakest pairs RoJ-Jrn 21.2, Jrn-Blk 26.1, RoJ-Blk 32.1 | salience Rdy=110 RoJ=80 Jrn=71 Blk=49 Done=82 Comp=78 Lock=50 NotC=48
orrery 20px deut+grey: weakest pairs RoJ-Jrn 21.2, Jrn-Blk 26.5, RoJ-Blk 32.0 | salience Rdy=110 RoJ=79 Jrn=71 Blk=49 Done=81 Comp=77 Lock=52 NotC=48
orrery 16px prot+grey: weakest pairs RoJ-Jrn 13.8, Jrn-Blk 15.9, Done-Comp 20.5
orrery 16px trit+grey: weakest pairs RoJ-Jrn 13.9, Jrn-Blk 16.2, Done-Comp 20.6
orrery 20px prot+grey: weakest pairs RoJ-Jrn 20.9, Jrn-Blk 25.5, RoJ-Blk 32.4
orrery 20px trit+grey: weakest pairs RoJ-Jrn 21.1, Jrn-Blk 25.9, RoJ-Blk 32.2
icon: moon OKLCH L .940 C .0056 h 256 | glade L .771 C .0115 h 255 (dC +.006, dh -1)
icon: max L moon .955 > glade .894 | earthshine 1.22:1 | axis 166.2 (disc 160, lit centroid 172.5) | 14 glints
```

### Gate by gate

**G1 Distinctness: pass.**
- Worst pair is 13.9 at 16 px (bar 10) and 20.9 at 20 px (bar 16), across grey, deuteranopia, protanopia and tritanopia.
- No pair differs only by mirror image. Ready on another job and Done this cycle are mirrored halves, but they carry different marks: a crystal against a check.

**G2 Salience: pass.**
- Ready is 70 against 52 for the next state (1.35×).
- Completed is 50 (0.71 × Ready).
- Every state except Not checked is at least 31.

**G3 Feature floor: pass on paper.**
- Ready's glints are 15 and 13 units tall, so at least 1.6 px at 16 px.
- The crystal is 22 wide, the ribbon 16, Blocked's limb 13.2, the check stroke 12, the ofuda 19 and the mist bands 14.
- **Risk:** Ready's medallion is scaled to 0.88 to fit its road, so at 16 px it is the smallest moon in the set.

**G4 Moon identity: uncertain. This is the one to test.**
- At 48 px, 7 of 8 should read as moon phenomena.
- **Stock-icon risks I can see:**
  - Completed at 16–28 px may read as a coin or button.
  - Ready at 16 px may read as a head over a bar (a user or pin icon). I offset the broken glints under the lit limb to fight this.
  - Not checked may read as a ringed planet or a fog weather icon.
  - Locked out still carries a faint "slash" echo, although it has no ring and the strip is off-centre and near-vertical.

**G5 No cheese: pass.**
- No craters, maria or stipple.
- No radial gradient on yellow. The lit moon is cool moonstone.
- Saturated gold is limited to rims, one glint and one check, under a third of any glyph.

**G6 No Moon Road collision: mostly pass.**
- There are no dashed circles and no rings separated from a core. Every bezel hugs its disc and carries ticks.
- **Watch:** Completed's gilt bezel around a full disc is the closest thing to "gilt ring around a core". Ready on another job places a crystal inside the dark half of a half moon, which the "crystal over a crescent" rule might be read to cover.

**G7 Icon physics: pass on every measurable item.**

| Item | Result |
|---|---|
| (a) Hue and chroma | Hue −1°, chroma +0.006. |
| (b) Brightness | The glade's maximum L is below the moon's. |
| (c) Axis | Within 10 of both the disc centre and the lit centroid, with earthshine at 1.22:1. |
| (d) Glints | 14, at least 12 tall, thicker in front and denser at the horizon, with no repeated rows and a lens outline rather than a pyramid. |
| (e) Lit limb | 30° below horizontal. The water copies the moon only as a true flattened mirror. |
| (f) Horizon | The sea at the horizon (#293A65) mirrors the sky at the horizon (#2E416C). |
| (g) Overlays and rim | The Installed-check corner holds only sea and ring. The outer keyline #6B5124 is 2.56:1 against #101010. The icon reads at 32 px on dark and light. |

**Honest weaknesses of the icon:**
- At 64 px the road can look slightly like a stacked-brick column.
- The 12-notch ring can be read as a clock or porthole bezel. The hairline limb scale only rescues that from 128 px up.

## Files

- **Glyphs:** the 8 state SVGs (viewBox 128).
- **Icon:** `plugin-icon.svg` (viewBox 512).
- **Render sheet:** `_sheet.png` and `_sheet.html`, from `render_sheet.py`.
- **`_src/`:**
  - `gen.py` and `icon.py` generate every SVG.
  - `physics.py` measures G7 from a Chrome render.
  - `cvd.py` runs the extra protanopia and tritanopia check on the `metrics.py` renders. It expects them in its own folder.
