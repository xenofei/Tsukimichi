# Menphina's Medallion

Round 2, direction A. Original art only: nothing here is traced from or modelled on a Square Enix asset.

## Pitch

Each state is a small minted medallion in the FFXIV job-icon style: a light emblem on an enamel well, set in a bevelled metal rim. The emblem is always a moon phenomenon, and the metal says how urgent it is. Ready is the only state with a gilt bezel and lit lapis enamel. It shows a crescent over its own moon road, so it is the plugin icon in miniature. The other states sit in pewter, in oxidised pewter (Completed), or in red iron (Locked out). The detail comes from the material, not from the moon's surface: a two-arc bevel, engraved astrolabe ticks, small moon-daisy rosettes, and hatching in the shadow side. The lit moon stays pale, cool moonstone, so nothing reads as cheese.

## FFXIV idioms borrowed (as original geometry)

- **Job and role icon build.** A light glyph on coloured enamel inside a metal rim, with gold used as metal: light, mid and deep tones plus one specular streak.
- **Astrologian and armillary.** The bezel carries a running hairline and 24 fine ticks. Ticks are skipped at the four diagonals, where tiny six-petal rosettes sit.
- **Menphina the Lover.** The full moon is her emblem, so Completed is a full-moon medal with an engraved eight-petal moon-daisy.
- **Dalamud, the red moon.** Locked out is a red moon split by a fracture, in an iron rim on oxblood enamel.
- **Aetheryte crystal.** Ready on another job has a faceted job crystal. The icon has an original aether spindle on the far island: the far shore you travel to.
- **Doma and Hingashi.** The icon has a stone lantern (tōrō). Not checked uses kasumi mist bands (oborozuki).

## Tier plan (one SVG, detail fades with size)

| Tier | Size | What shows |
|---|---|---|
| Row (Flair Plain) | 16–20 px | Keyline, rim tone (gilt, pewter, oxidised or iron), enamel value, and the moon silhouette and mark. That is 3 masses. |
| Mid (Flair Quiet) | 24–40 px | Adds the bevel gradient, the inner counter-bevel, the terminator band and limb band, and Ready's bezel glint and horizon line. |
| Hero (Flair Full) | 48 px and up | Adds bezel engraving (hairline, 20 ticks, 4 rosettes), the seat ring with 4 diamond studs, hatching in the shadow side, the wax seal on the ribbon, and the moon-daisy on Completed. All of it is ≤0.6-unit hairlines at 0.3–0.5 opacity. |

At 16 px all hero detail drops below one pixel and disappears. Nothing turns to mush, because the hero layer has no fills.

## States

| State | Metaphor |
|---|---|
| Ready | Gilt bezel, lit lapis sky: a thick crescent over its moon road, with glints under the lit centroid. It is the icon in miniature. |
| Ready on another job | Pewter. A half moon with an aether job crystal in its dark half. |
| In journal | Pewter. A gibbous moon with a gilt journal ribbon hanging over the bezel at top-left; a wax seal at hero size. |
| Blocked | Pewter. A new moon: an ashen, engraved disc with a bright earthlit limb ("not lit yet"). |
| Done this cycle | Pewter. A waning half (it comes back) with a moonstone check in the dark half. |
| Completed | Oxidised pewter. A quiet full-moon medal that fills the well; an engraved moon-daisy at hero size. |
| Locked out | Iron rim on oxblood enamel. Dalamud's red moon split by a fracture. |
| Not checked | Ghosted rim, no enamel. Oborozuki: a faint disc behind two kasumi mist bands that drift past the rim. |

I replaced the recommended ofuda band on Locked out with the fracture. Every medallion has a ring, so a diagonal band inside a ring would read as a no-entry sign. The fracture is a lore-true Dalamud cue, and its gap is 12 units, which meets the feature floor.

## Plugin icon: "Moon road to the lantern"

- **Frame.** A rounded square (rx 112) with the three-band gilt bevel: #5A4320 at 10 units, a #E9CF8A → #B08A45 → #7A5A2A gradient at 8 units, and #F6E6B4 at 45% for 2 units. The two top corners carry an original filigree at 30% opacity: a double hairline ending in tiny crescents.
- **Moon.** A thick crescent, centre (172, 158), r 86, terminator k −0.38 (lit width 0.62 r ≈ 53). It is rotated 30°, so the lit limb faces 30° below horizontal. Moonstone #E2E8F4 with a #C3CEE4 terminator band and a #F3F0E6 limb band. No craters and no radial gradient; only a 15% bloom in the sky.
- **Lit centroid.** x 215.6, y 183.2, computed numerically from the same geometry.
- **Road axis.** x 216.3, measured as the luminance-weighted centre of the road pixels. That is 0.7/512 from the lit centroid.
- **Horizon.** y 300. It brightens to 70% where the road meets it. The flattened mirrored crescent (y × −0.3) sits 4–27 units below the horizon, cut into 3 slices at 42%.
- **Brightest point.** The rule gives y 442; the brightest measured row is y 441, at opacity 0.92.
- **Glints.** 13 moon glints in 7 rows, each at least 12 units tall and growing from 12 to 17 toward the viewer. Row gaps grow from 7 to 18 units (denser toward the horizon). Row lengths are 22–78 and no two rows are alike. The outline is a lens: 48 wide at the horizon, about 130 at the brightest row, about 114 in front. The lantern window adds 2 separate gold glints, and the crystal adds 1 aether glint.
- **Lantern.** An original Hingashi-style tōrō on a rock islet at lower left. Its right edges are moonlit at 32%, the window is two-pane gold with a soft 28% glow, and the two glints fall on the water directly below the islet.
- **Far island.** A low shape on the right horizon with an original aether spindle and glow at its peak.
- **Installed-check corner.** It holds only the sea gradient. The road's right edge does enter the check's left 38 units (x 248–286, y 375–480), as composition 1 implies.

## Palette tokens

| Group | Tokens |
|---|---|
| Keyline | #080B16 |
| Gilt (Ready rim, icon frame) | #FFF3D1 / #E9D49C / #C9A766 / #A88B52 / #6B5124 |
| Pewter | #98A2BE / #5F6888 / #272D48 |
| Oxidised pewter (Completed) | #5E6888 / #3C4463 / #1D2238 |
| Iron (Locked out) | #A87570 / #6E4247 / #2A1722 |
| Lit lapis enamel (Ready) | #4266B4 → #2B4488, sea #1A2858 |
| Resting enamel | #1D2C5E → #141E44 |
| Moonstone | #F3F0E6 / #E2E8F4 / #C3CEE4 / #95A5C8 / #5E6E97 |
| Completed face | #606E96 |
| Ash (Blocked) | #4A5577 |
| Aether | #9BE6FF / #4FB3EA / #1F5FA8 |
| Dalamud red | #D2584E / #8E2A2E, oxblood #3A1628 → #2A0F1C |
| Ribbon gilt | #D9B45E / #8A6526 / #F6E2A8 |
| Mist | #A9B2CC |
| Lantern gold | #E0B860 (the existing Moon Road gold family) |

## Metrics (final)

```
medallion 16px grey: weakest pairs RoJ-Blk 12.4, RoJ-Jrn 12.9, Lock-NotC 13.5 | salience Rdy=69 RoJ=50 Jrn=54 Blk=44 Done=52 Comp=52 Lock=33 NotC=33
medallion 16px deut+grey: weakest pairs RoJ-Blk 12.4, RoJ-Jrn 13.0, Lock-NotC 13.9 | salience Rdy=69 RoJ=50 Jrn=54 Blk=44 Done=52 Comp=51 Lock=36 NotC=33
medallion 20px grey: weakest pairs RoJ-Blk 19.4, Lock-NotC 21.3, RoJ-Jrn 22.1 | salience Rdy=106 RoJ=77 Jrn=84 Blk=69 Done=80 Comp=81 Lock=50 NotC=52
medallion 20px deut+grey: weakest pairs RoJ-Blk 19.3, Lock-NotC 21.9, RoJ-Jrn 22.2 | salience Rdy=106 RoJ=77 Jrn=84 Blk=68 Done=80 Comp=81 Lock=55 NotC=51
16px prot+grey: RoJ-Blk 12.4, RoJ-Jrn 12.6, Lock-NotC 14.2      (Machado 2009, severity 1)
16px trit+grey: RoJ-Blk 12.3, RoJ-Jrn 12.8, Lock-NotC 13.3
20px prot+grey: RoJ-Blk 19.4, RoJ-Jrn 21.6, Lock-NotC 22.5
20px trit+grey: RoJ-Blk 19.2, Lock-NotC 21.1, RoJ-Jrn 21.8
icon: moon OKLCH L .918 C .0175 h 264.5 | road L .747 C .0238 h 268.3, max L .887 vs moon .955
      road axis 216.3 vs lit centroid 215.6 | earthshine 1.04:1 (none) | brightest row y 441 (target 442)
      horizon sky/sea L .357/.352 (open water) | rim 8.75:1 vs #101010
saturated gold (C > 0.09) share: Ready 1.1 %, In journal 6.2 %
```

## Self-assessment against the gates

- **G1 Distinctness: pass.** At 16 px the weakest pair is 12.3 (gate 10); at 20 px it is 19.2 (gate 16), across grey, deuteranopia, protanopia and tritanopia. No pair differs only by mirror image, ring colour or hue: every pair has a different emblem silhouette.
- **G2 Salience: pass, with thin margins.**
  - Ready is 1.28× the next state at 16 px and 1.26× at 20 px (gate 1.25). In journal is the closest competitor.
  - Completed is 0.75× Ready at 16 px and 0.76× at 20 px (gate 0.8).
  - Every state except Not checked is at least 33 (gate 15).
  - Ready's lead comes from the gilt rim and the lit lapis enamel. All other states use dim resting enamel and darker pewter.
- **G3 Feature floor: pass by construction.** These features are at least 12 units (1.5 px at 16 px):
  - the ribbon (15 wide);
  - the crystal (21 × 42);
  - the check stroke (11.5, plus a 15-unit keyline);
  - Blocked's limb (12.2);
  - the fracture gap (12);
  - the mist bands (12);
  - Ready's road column (about 30 × 32).

  Ready's individual road streaks are thinner (2.8–4.2 units); the column, not each streak, carries the state.
- **G4 Moon identity: the main risk.** At 48 px all 8 read as moons or moon events in my own reading, but no blind panel has run yet. At 16 px:
  - Completed is a plain blue-grey disc in a rim. It could be named "disabled radio" or "coin".
  - Done this cycle keeps a check in a circle, so a check_circle read is possible.
  - Not checked could read as a hamburger or Saturn.

  At most one of these may fail.
- **G5 No cheese: pass.** There are no craters, maria or ball gradients, and the moon is cool moonstone. Saturated gold covers at most 6.2% of any glyph. Completed's moon-daisy is a 0.7-unit engraving at 30%, shown only at hero size. It is a medal emblem, not surface texture, but a juror might see it as surface detail.
- **G6 Moon Road collision: watch.** At 16–20 px, Ready's gilt bezel is still a closed gold ring around a blue core. These features separate it from orbit-reference's thin progress arc and bead:
  - the rim is a 9.5-unit band, not a hairline;
  - it has a bevel gradient and a counter-bevel;
  - its enamel is a filled sky with the moon road, not a grey square.

  There is no star at the centre, no dashed circle, no diagonal and no crystal over a crescent: the crystal sits beside a half moon. An in-context check next to the ornaments is still needed.
- **G7 Icon physics: pass on every measured item.**
  - (a) Hue Δ 3.8° and chroma Δ +0.006.
  - (b) The road peaks at 93% of the moon's lightness.
  - (c) No earthshine, so the road is measured against the lit centroid: Δ 0.7/512.
  - (d) 13 glints at least 12 tall, thicker in front, denser toward the horizon, in a ragged lens.
  - (e) The limb faces 30° below horizontal. The only crescent copy is the true flattened mirror.
  - (f) On open water, sky and sea lightness match at the horizon.
  - (g) The rim is 8.75:1, and the icon still reads as moon, road and lantern at 32 px.

  One soft spot remains: the road's right edge enters the Installed check's left edge.
- **T3 Moon Road coherence: over budget.** This direction adds more than the 2 new tokens the brief allows: lapis and resting enamel, aether, Dalamud red, iron, oxidised pewter and ribbon gilt. Any merge would need to fold these into GlyphPalette, or collapse the iron and oxidised rims into plain pewter.
