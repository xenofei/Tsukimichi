# Round 2 brief: plugin icon composition and glyph rendering (research 3)

The evidence is in the session scratchpad: 67 top plugin icons at 64 px, Dalamud's installer overlays, and a correct-reflection prototype at `proto/proto.svg`.

## A. Plugin icon

### Why the owner is right about round 1

1. **Colour.** The gold road sits under an ivory moon. A reflection takes the moon's colour.
2. **Position.** The road sits around x 228–245, but the crescent's lit band is centred near x 285–300.
3. **Shape.** It is a widening pyramid. A real glitter path is lens-shaped: narrow at the horizon, widest at the moon's mirror point, then narrower and more broken in front.
4. **Brightness.** There is no brightest point.
5. **Echo.** Nothing in the water echoes the crescent.

### Rules for the moon road (月の道), in 512-unit master coordinates (H = horizon y)

1. **Column centre.** Centre the column on the lit area's centre, about disc centre x + 0.6 r for a crescent lit on the right.
2. **Brightest point.** Place it at y = H + (H − moon y), or at the bottom edge if that falls outside the tile.
3. **Width.** About the lit width at the horizon, 2–2.5× that at the brightest point, and 10–20% narrower in front. The outline is an asymmetric lens with ±15% jitter, never a pyramid.
4. **Dashes.**
   - 8–14 round-capped dashes.
   - Height 12→24 and gaps 6→14 toward the viewer.
   - One dash per row at the horizon, and 2–3 per row lower down.
   - Lengths 20–90.
5. **Colour.**
   - Moon colour at 100% at the brightest point, and 45–60% at the horizon and edges.
   - Gold only as a warm tint on at most 30% of the small outer dashes.
   - An optional glow under the dashes at 10–20%.
6. **"Matches the moon" cue.** Draw a flattened mirror of the crescent 10–20 units below the horizon: flipped, y scaled 0.3–0.4, lit side on the right, broken into 2–3 slices. Brighten the horizon where the road meets it.
7. **Phase.** Use a thick crescent with lit width of at least 0.45 r, so the road is at least 6 px wide at 64 px. Avoid a full disc, which is the Completed glyph and risks the cheese read.

### Installer facts (from Dalamud source)

**How the icon is drawn**
- At 64×64 scaled.
- Square, at most 512×512. Ship at 512.

**Overlays**
- The Installed check covers x 31–61, y 39–61 at 64 px.
- Update, trouble and disabled overlays are large and centred.
- Disabled dims the icon to 40% and adds a centred grey crescent.

**Layout consequences**
- Moon in the upper-left or centre.
- Road at 25–50% of the width.
- The bottom-right holds only quiet sea.
- No big crescent dead centre.

### What the top plugin icons do (67 viewed)

- **FFXIV-native look:** a gold-bevelled rounded square around a dark well with one emblem (JobBars, VFXEditor, NoTankYou). This is the strongest "FFXIV" signal.
- **Medallions:** PriceInsight, Umbra, GatherBuddy.
- **Transparent silhouettes** (Collections, Craftimizer) read best.
- **Busy illustrations turn to mush.** DistantSeas works with about 4 flat bands.

### Detail budget at 64 px

**Composition**
- One focal silhouette.
- 2–3 depth layers.
- One focal highlight.
- At most one secondary element of at least 80 units.

**Sizes**
- Minimum feature 12 units.
- Minimum gap 8 units.
- At most about 6 main colours.

**Brightness**
- The moon is always the brightest pixel.
- The road peaks at about 90% of it.

**Detail above 64 px**
- Detail added at 128 and 256 px stays at 30% opacity or less.

### Frame

- A rounded square at rx 112, with a three-band gilt bevel:
  - #5A4320, 10 units;
  - a gradient #E9CF8A → #B08A45 → #7A5A2A, 8 units;
  - a highlight #F6E6B4 at 45%, 2 units.
- Filigree only at 128 px and above, original design.

### Four compositions

1. **Moon road to the lantern** (primary)
   - A thick crescent, r 88 at (180, 165), lit on the right.
   - A road column at about x 232, running y 304–500, widest (about 130) at y 430.
   - A Hingashi-style stone lantern silhouette at the bottom-left, with a gold window casting two glints.
   - A low island on the right horizon.
   - Variant: an original crystal on the far shore where the road meets the horizon.
2. **Crystal steps.** The glints become 5–6 rhombic crystal steps under the moon, each with a moon-coloured top facet and a #6F86B8 side facet.
3. **Journal with a moon clasp.**
   - A navy journal with gilt corners.
   - The clasp is the Ready glyph.
   - A cream ribbon cut into three dashes as the road.
4. **Menphina sigil.**
   - A gilt ring with 12 notches.
   - A gibbous moon.
   - A small rose companion moon (Dalamud, Menphina's hound) of at least 24 units.
   - A short water arc with a four-dash road.

## B. Rendering the state glyphs

### What exists today

- **MoonGlyph.cs** is about 900 lines of ImDrawList primitives.
- **OrnamentAtlas.cs** already ships an embedded atlas, loaded via `GetFromManifestResource`:
  - it picks the @2x version above 1.25× and shows a fallback while loading;
  - an `OrnamentLayout` table with a size test.

### Dalamud constraints

**Texture sampling**
- Image textures have MipLevels = 1, and the sampler is linear.
- So never upscale, shrink by at most 2×, and integer-snap rectangles.

**Tinting**
- `AddImage` tint multiplies, so it works for white alpha masks.

**Batching**
- Texture switches break batching.
- Procedural glyphs batch with text.

### Recommendation: a hybrid

**Rows (physical r < 16)**
- Procedural, for Flair Plain and for high contrast.

**Hero (r ≥ 16, Flair Full or Quiet): a layered white-mask atlas**
- The atlas holds:
  - 2–3 masks per glyph: the well plus keyline, the lit body with greyscale surface detail, and the metal or mark;
  - each mask is tinted with GlyphPalette tokens at draw time;
  - one atlas for every theme.
- Sizes 48, 64, 96 and 128. Always use the smallest size at or above the target.
- About 1024×1024 RGBA, 4 MiB on the GPU.
- Effort is about 1–2 days, modelled on OrnamentAtlas.

**Shared geometry**
- One geometry table drives both procedural and atlas silhouettes.
- A test checks their alpha overlap at 48 px is at least 0.97.

### Pipeline

- **Existing pipeline.** `docs/design/moon-road/gen_atlas.py` and `banners/rasterize.py` already rasterize SVGs with headless Chrome using a transparent background. Clone them for the glyph atlas.
- **Masks.** Force RGB to 255 on mask layers.
- **Authoring grid.** Author hero masters on a 128-unit grid, on whole and half units.
