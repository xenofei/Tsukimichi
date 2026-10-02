# Round 2 brief: FFXIV visual language (research 1)

Everything below describes Square Enix's art in words, as inspiration for original geometry only. Nothing here should be traced, sampled or downloaded.

## Why round 1 fell flat

- **The glyphs look like a stock icon set.** Every one is a flat two-tone icon (sumi disc, one pale shape, a hairline), closer to Material or SF Symbols than to FFXIV. There is no metal, crystal, glow or bevel.
- **The icon looks like a generic weather or meditation app.** The moon road's pill-shaped dashes stack into a pyramid that reads as a bar chart, not as light on water.
- **"Detail" and "not cheese" pull in opposite directions.** The cheese look came from surface texture (craters, maria, a sphere gradient) on a yellow fill. So the detail has to come from **material and structure**: a metal bezel, crystal facets, engraved constellation lines and aether glow. The lit moon stays pale and cool, and gold appears only as metal.
- **Round 1's shape grammar passed every legibility test**, so it stays: half, gibbous, full, ring, bar and dashes, plus halo, diamond and check. Round 2 is a material and ornament pass on top of it.

## Lore and motifs to echo, as original designs

| Motif | What it is | What we can take from it |
|---|---|---|
| Menphina the Lover | Moon goddess, symbol the full moon, element ice | Full moon as a holy emblem; ice-crystal tones; a 6–8 petal moon-daisy rosette as ornament |
| Dalamud, the lesser red moon | The broken moon that fell; its red means ruin | Fits Locked out; also a small companion "hound" moon for the icon |
| Mare Lamentorum | The moon's surface: grey basalt, and the moon is secretly a starship | A grey-silver palette; engineered geometry is lore-legal; no rabbits |
| Tsukuyomi | The Doman moon primal: exact half light, half shadow | Doman lacquer and kamon styling |
| Moonfire Faire | Summer festival of lanterns and fireworks | Warm gold sparks on night water |
| Ultima Thule | A violet starscape | Violet accent for night skies |
| The Eorzean calendar | 12 moons alternating Astral and Umbral; exactly **8 phases**, shown in a tiny moon-phase icon on the HUD | A native precedent for a small moon-phase glyph |
| Astrologian | Star globes, armillary rings with ticks, four-point star sparkles, constellation dots joined by hairlines, tarot-card gold borders | The most FFXIV-literate source of fine detail |
| Aetheryte | A tall faceted spindle crystal inside oblique rings, with a blue glow | Spindle shapes, orbit rings, aether glow |
| Ishgard | Gothic rose windows: lapis and ivory glass, lead tracery | Stained-glass panes divided by lead lines |
| Kugane and Doma | Vermilion lacquer, gold-leaf clouds, kamon crests (mikazuki, tsuki ni hoshi, oborozuki, kumo ni tsuki) | Crest-style framing |

## How FFXIV's own icons are built

- **Recognisability first.** The lead UI artist warns that icons must not look alike: healer icons are all "sparkly". So stars should not appear on every state.
- **Quest markers** are flat 2–3 tone signals with a dark outline and an outer glow, so they read over any terrain.
- **Status icons:** the outline carries the category and the interior carries the flavour.
- **Job and role icons:** a light glyph on a coloured enamel ground inside a metal rim. This is what "detail" means in FFXIV.
- **Gold is always metal:** a light–dark–light bevel with one specular streak.
- **At 24–40 px:** about 3 value bands, one rim, one focal glint. At 16–20 px the game relies on silhouette and rim only.

## Detail that still reads at 16–20 px

Build each glyph from at most 4 layers:
1. **A 1 px keyline**, darker than any background, so the glyph holds on bright daylight scenes.
2. **A bevel rim of 1–1.5 px, drawn as two arcs.** The light tone runs over the top-left 180°, the shadow tone over the bottom-right.
3. **A two-tone body.** A crisp 1 px band runs along the terminator or the limb. No radial gradient.
4. **One focal glint**, only on states that call you to act.

Larger sizes add more:
- **24–32 px:** one facet split across the lit part, and 4 tick marks on the bezel.
- **40 px and up:** constellation engraving in the dark part, a filigree seat ring, and an aether glow.

## Palette (proposed)

| Ramp | Values |
|---|---|
| Gilt metal | #6B5124 / #A88B52 / #C9A766 / #E9D49C / #FFF3D1 |
| Moonstone ice (the lit moon) | #F3F0E6 / #E2E8F4 / #C3CEE4 / #95A5C8 / #5E6E97 |
| Lapis enamel | #1B2A57 / #253C7A |
| Aether glow | #9BE6FF / #4FB3EA / #1F5FA8 |
| Dalamud red | #D2584E / #8E2A2E |
| Astral ember / umbral ice | #E7A35C / #86BDE8 |
| Thule violet | #4A3A86 |
| Sumi | #0B0F1C |

## Do and don't

| Do | Don't |
|---|---|
| Build detail from rim, facet, engraving and glow | Craters, maria or stipple on the lit part |
| Draw rims as two-arc bevels | Radial sphere gradients |
| Keep the lit moon pale and cool | Large yellow fills |
| Use gold as metal or signal only | Brown or olive shadows |
| Use cool, hue-shifted shadows | A star on every state |
| Allow one glint, on act-now states only | Rabbits, faces or glitter |
| Use red for the eclipsed or broken moon | Copying SE icons, job glyphs or the Twelve's sigils |
| | Pill-dash moon roads |
| | More than 4 layers at row size, or more than 2 gradient steps |

## Four directions

**A · Menphina's Medallion**
- **Glyphs:** a medallion with a gilt or pewter bezel, an enamel well and a moonstone moon. Gilt marks act-now states; pewter marks the rest.
- **Icon:** a medallion moon with a rosette bezel and gilt corner brackets.

**B · Aether Crystal Moon**
- **Glyphs:** the lit part is cut crystal facets. Ready gets an aether glow. In journal's seal becomes a small aetheryte spindle.
- **Icon:** a crystal crescent inside oblique gilt orbit rings over water, with a moon road made of crystal glints.

**C · Astrologian's Orrery**
- **Glyphs:** an armillary ring with ticks. Ready alone gets a single four-point star. In journal shows a bead in transit. Completed carries a constellation engraving.
- **Icon:** a celestial globe with a crescent at its centre, a violet sky and a tarot-card border.

**D · Ishgard Rose Window and Tsukuyomi's duality**
- **Glyphs:** the moon as stained glass, with ivory panes, a lapis dark half and gilt lead lines. Locked out is red glass.
- **Icon:** a rose-window roundel over a road of glass tesserae.

**Research recommendation:** take B and C first, and use A's bevel rim in all four.
