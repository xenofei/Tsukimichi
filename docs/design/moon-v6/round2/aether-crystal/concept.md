# Round 2 concept: Aether Crystal Moon

## Pitch

The moon is a cut moonstone set in metal. Its lit part is pale, cool crystal: a clean silhouette at row size, one facet split at mid size, and four or five hairline facets at hero size. Each glyph is a job-icon-style medallion with a bevel rim, an enamel well and a crystal moon. Act-now states get gilt metal, and the rest get dark pewter. Only Ready carries an aether glow and its own moon road of crystal glints, so Ready is the plugin icon in miniature. In the icon, the moon road becomes broken crystal shards laid exactly under the lit crescent, in the moon's own colour, with a flattened mirror of the crescent just below the horizon. The detail comes from material (bevel, facets, glow), never from surface texture. There are no craters, no yellow and no sphere gradient.

## FFXIV idioms borrowed (original geometry only)

- **Gilt bevel.** A job-icon metal rim drawn as two arcs: light at the top-left, shadow at the bottom-right, with one specular streak. The icon uses the gilt-bevel rounded square.
- **Aether and crystal.**
  - The lit moon is faceted moonstone.
  - Ready has an aether glow.
  - Ready on another job carries a Tide-blue job crystal.
  - A tiny aetheryte (a spindle crystal with an oblique ring) stands on the icon's far shore, with its own short reflection.
- **Moon lore.**
  - Locked out is Dalamud's red moon under an ofuda sealing band.
  - Not checked is oborozuki, the hazy moon.
  - The eight states echo the calendar's eight-phase moon icon.
- **Armillary ticks.** Four bezel ticks on the medallions, and twelve engraved gilt ticks on Completed's bezel.
- **Quest-marker grammar.** The outline (rim) carries the category: gilt means act now, pewter means wait or done. The interior carries the flavour.

## Tier plan (one SVG serves every size, so hero detail is hairline-thin and low-opacity)

| Tier | What reads |
|---|---|
| Row (16–20 px) | Keyline, rim, two-tone body, and one state mark: road glints, crystal, ribbon, limb, check, band or wisps. Facets are 1.1–1.6 units at 0.1–0.55 opacity, which is sub-pixel and drops out. |
| Mid (24–40 px) | Bevel light and shadow, the main facet split (the culet "V" across the lit part), the terminator band, and the ribbon's fold. |
| Hero (48 px and up) | Bezel ticks and specular, the faceted table plane, unlit facets in Blocked's earthshine, Completed's engraved gilt bezel, the ofuda's printed border, and Ready's aether glow. |

Flair mapping: Full shows hero, Quiet shows mid, and Plain shows row.

## States

| State | Metaphor |
|---|---|
| Ready | Gilt-rimmed crystal crescent in a pale aether glow, with its moon road of three crystal glints laid under the lit centroid. |
| Ready on another job | The same crescent in pewter, with a Tide-blue job crystal standing in the dark half. |
| In journal | Pewter, a waxing gibbous moon, and a gilt bookmark ribbon hung over the top-left rim. |
| Blocked | New moon with earthshine: an ashen crystal disc with faint unlit facets and one thin bright limb. |
| Done this cycle | Waning half moon (lit on the left) with a pale check in the dark half. |
| Completed | Quiet full moon: a dim cabochon-cut moonstone inside a wide engraved pewter bezel, with no halo. |
| Locked out | Blood moon, a copper-dark disc with a red limb, sealed by a notched diagonal paper ofuda. No rim ring. |
| Not checked | Oborozuki: a faint disc veiled by two tapered mist wisps that run past its edge. |

## Plugin icon: "Crystal steps" (512 master)

| Item | Value |
|---|---|
| Frame | Rounded square, rx 112. Bands: #5A4320 (10 units), then a gilt gradient #E9CF8A → #B08A45 → #7A5A2A (8 units), then a #F6E6B4 specular stroke at 45% (2 units) and a #3A2A12 inner keyline. Original corner brackets at 22% opacity. |
| Moon centre | (150, 150), r 82. Thick crescent: terminator ellipse 0.35 r, lit width 0.65 r. The lit limb faces 35° below horizontal. |
| Earthshine disc | #26356B with a 28% hairline limb. |
| Lit centroid x | 188.5 (centroid at (188.5, 176.9)) |
| Road axis x | 188.5 by design; the luminance-weighted measure is 186.7 |
| Horizon y | 296 |
| Brightest-point y | Target 442 (2H − moon y); measured brightest glint row ≈ 450 |
| Glint count | 13 crystal shards in 6 rows (1, 2, 3, 3, 2, 2). Heights run 12 → 18 toward the viewer and row gaps 5 → 14. Lengths are 14–64. Each shard has a silver top facet and a #B4BDD6 lower facet, at 0.42–0.95 opacity. |
| Mirror sliver | The lit crescent flipped, scaled x 0.45 and y 0.3, centred on the road axis at y 304–353 and cut into 3 slices. |
| Other elements | Horizon brightening where the road meets it, a soft column glow, two far-shore silhouettes, the aetheryte, 7 quiet stars and 5 ripple hairlines. |
| Installed-check corner | Only ripple hairlines at 12–20% opacity. All glints end at x ≤ 247. |

## Palette tokens

- **Existing Moon Road tokens:**
  - SILVER #DDE3F0 (lit moonstone)
  - MIST #A9B2CC
  - DUSK #7C86A8
  - SHADOW #3A4363
  - TIDE #6F8FD0 (job crystal)
  - GILT #A88B52
  - GILT_HI #D9BE82
  - NIGHT #0F1424
- **Shared extras:**
  - Sumi #0B0F1C (keyline)
  - Lapis #1B2A57 (well)
  - Gilt shadow #6B5124
  - Moonstone highlight #F4F6FB
- **New tokens (2):**
  - AETHER #4FB3EA, with its highlight #9BE6FF
  - BLOOD #B8443F, with its dark #4A1A22

Gold appears only as metal: Ready's bezel, In journal's ribbon, Completed's engraved ticks and the icon frame.

## Metrics (final, from `metrics.py`)

```
aether 16px grey: weakest pairs RoJ-Blk 17.6, Done-Comp 20.2, Blk-NotC 20.4 | salience Rdy=66 RoJ=45 Jrn=51 Blk=36 Done=50 Comp=50 Lock=42 NotC=20
aether 16px deut+grey: weakest pairs RoJ-Blk 17.6, Blk-NotC 20.2, Done-Comp 20.2 | salience Rdy=65 RoJ=45 Jrn=51 Blk=36 Done=50 Comp=50 Lock=44 NotC=20
aether 20px grey: weakest pairs RoJ-Blk 28.0, Done-Comp 33.2, Blk-NotC 34.7 | salience Rdy=108 RoJ=71 Jrn=81 Blk=58 Done=80 Comp=78 Lock=65 NotC=33
aether 20px deut+grey: weakest pairs RoJ-Blk 28.0, Done-Comp 33.3, Blk-NotC 34.4 | salience Rdy=108 RoJ=71 Jrn=81 Blk=58 Done=80 Comp=78 Lock=68 NotC=33
```

Extra checks (Machado 2009 simulation + greyscale), weakest pairs:

| Simulation | 16 px | 20 px |
|---|---|---|
| Protanopia | RoJ-Blk 17.3 | 27.6 |
| Tritanopia | RoJ-Blk 17.3 | 27.5 |

## Self-assessment against the gates

**G1 Distinctness: pass.**
- The weakest pair is 17.6 at 16 px (bar 10) and 28.0 at 20 px (bar 16), in greyscale, deuteranopia, protanopia and tritanopia.
- No pair differs only by mirror image, rim colour or hue. Done (half, lit left) and In journal (gibbous, lit right) also differ by check versus ribbon.

**G2 Salience: pass, with a thin margin at 16 px.**

| Size | Ready vs next (In journal) | Completed vs Ready |
|---|---|---|
| 16 px | 65–66 vs 51 = 1.27–1.29× | 0.77× |
| 20 px | 108 vs 81 = 1.33× | 0.72× |

- Every state except Not checked is at least 36. Not checked is 20.
- Most of Ready's lead at row size comes from the pale aether glow, which brushes against the brief's "glow as the main carrier" warning. The road glints and gilt rim carry the meaning, but the glow carries the volume.

**G3 Feature floor: pass.** State-carrying features at 16 px:

| Feature | Units | At 16 px |
|---|---|---|
| Ready glints (tall) | 13–15 | 1.6–1.9 px |
| Job crystal | 20 × 48 | |
| Ribbon (wide) | 22 | |
| Blocked limb | 14.4 | 1.8 px |
| Check stroke | 13 | 1.6 px |
| Ofuda band | 21 | |
| Mist wisps | ≈ 13–14 | |

All marks cover well over 4 px².

**G4 Moon identity: likely pass, with honest risks.**
- At 48 px, 7 of 8 are plainly moon phenomena: crescent, gibbous, new moon, half, blood moon, hazy moon.
- **Completed** is the likeliest fail. It is a dim faceted grey disc that can read as a coin or button at 16 px.
- **Locked out** can read as a red "no entry" or candy-stripe disc at 16 px. The filled body, red limb and notched paper ends are meant to counter that.
- **Done** sits near Material's check_circle. The half moon is the mitigation.

**G5 No cheese: pass.**
- There are no craters, holes or maria. The lit part is flat moonstone with facet planes.
- There is no radial gradient on any moon body.
- Saturated gold (OKLCH C > 0.09) covers 0% of every glyph, measured, because the gilt is desaturated metal.

**G6 No Moon Road collision: pass, two items to watch.**
- **Ready's gilt bezel** is a gold ring around a moon, and the orbit-reference ornament is a thin gold ring around a core. It differs as a thick bevelled medallion rim around a full-bleed body, not a thin orbit ring around a small dot. Check them side by side.
- **Ready on another job** has a crystal beside a crescent. The Moonlit ornament is a gold bowl crescent cradling a silver crystal above it. Here the crystal is blue and inside the disc's dark half, and the crescent is upright, lit on the right.

**G7 Icon physics: pass, measured on the 512 render in OKLab.**

| Rule | Result |
|---|---|
| (a) Hue and chroma | Moon lit h 268.8°, C 0.020. Glade h 270.5°, C 0.037. Δh = 1.6°, ΔC = +0.017. |
| (b) Brightness | Glade mean L 0.70 and max 0.87, against moon mean 0.90 and max 0.98. The glade is never brighter. |
| (c) Axis | Earthshine is deliberately below 1.15:1: the disc reads as a darker silhouette against the glow, at 0.87. So the axis follows the lit centroid: 186.7 measured against 188.5, within ±10. |
| (d) Glints | 13 glints, at least 12 units tall, thicker toward the viewer, denser near the horizon. Rows are not mirrored, and lengths and gaps vary by at least 15% row to row. Edges are ragged and the outline is a lens (widest around y 421–449, narrower in front), not a pyramid. |
| (e) Limb and echo | The lit limb faces 35° below horizontal. The only crescent echo is the true flattened mirror sliver. |
| (f) Horizon | Sea at the horizon is #2A3C77, sky at the horizon is #2C3F7C. |
| (g) Overlays and size | The check corner holds hairlines only. Outer rim #5A4320 is 2.05:1 against #101010 (just over the bar) and the gilt band is 5.95:1. Moon and road both survive at 32 px. |

## Critique and iteration log

1. **First draft.**
   - Every rim was bright pewter, and Ready was among the quietest states (41 against 86 for Completed).
   - The rose-cut on Completed read as a hexagram.
   - In journal's terminator band drew a full ellipse.
   - Icon glints looked like stacked pebbles, and the mirror sliver was a wide swoosh off to the left.
2. **Second pass.**
   - Rims split into gilt (Ready) and dark pewter (the rest).
   - Moved to an off-centre kite-table cabochon cut and fixed the half-ellipse terminator.
   - Glints became pointed shards, and Not checked bars became tapered wisps.
3. **Third pass.**
   - Ready got a near-half thick crescent, a stronger glow and bigger glints.
   - Completed got a wider bezel and a dimmer body.
   - Icon glints became longer and flatter with a softer lower facet, set out as a lens. The sliver was compacted onto the axis.
4. **Fourth pass.**
   - The glow became pale aether rather than neon cyan, and Ready's facets were lightened.
   - In journal, Done and Completed were quieted slightly to give Ready its 1.25× margin.
   - The check grew to 13 units for G3.

Remaining weaknesses:
- At 128–256 px the icon glints still lean toward "crystal stepping stones" rather than pure light on water. That is literal to the brief's composition, but a juror may read it as a path of stones.
- Ready's 16 px lead depends on the glow.
- Completed is the least moon-like glyph.

`_gen.py` regenerates every SVG: `python _gen.py <out-folder>`.
