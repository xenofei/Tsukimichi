# Round 2 brief: why round 1 failed, success criteria, metaphors, rubric (research 2)

The evidence renders and `metrics.py` (pixel metrics: weakest state pairs and salience at 16/20 px, greyscale and deuteranopia) are in the session scratchpad under `r2research/`.

## 1. Why round 1 failed

**A. It reads as a stock project-management status set.** At 16–28 px:
- Ready is the contrast-toggle icon.
- Ready on another job reads as the letter "D".
- Blocked is a radio button.
- Done is Material's check_circle.
- Locked out is a no-entry sign.
- Not checked is a backlog spinner.
- In journal is a pac-man with a pellet: its diamond is 1–2 px at 16 px.
- Completed reads as a planet or a macaron.

Only three of the eight read as moons. Blocked, Locked out and Not checked have no moon cue at all. They are also the three weakest pairs in every round-1 set:

| Set | Weakest pairs at 16 px (greyscale or deuteranopia) |
|---|---|
| Final | 8.3, 9.8, 10.3 |
| A | 9.6, 11.0, 15.2 |
| B | 6.8, 7.1, 7.4 |

**B. It is flat, with no material.**
- The gold is a flat stroke, not metal.
- The sumi well is about 1.05:1 on Night, so it is invisible.
- At 96 px the glyphs carry no more craft than at 16 px.

**C. There is no FFXIV identity.** Japanese styling is not FFXIV styling. Missing:
- bevelled gilt;
- aether and crystal light;
- Sharlayan and Astrologian instruments;
- the quest-marker grammar;
- Eorzean moon lore.

**D. It collides with Moon Road ornaments.**
- glyph-removed is a dashed circle with a diagonal.
- orbit-reference is a gold ring around a core, meaning progress.
- glyph-special is a gilt ring around a centre mark.

**E. The icon is not a reflection of its moon.**
- **Chroma.** The moon has chroma 0.036 and the road 0.106–0.122, three times the moon's.
- **Position.** The road's axis sits about 35/512 left of the lit centroid, under the dark bite.
- **Shape.** The pill pyramid reads as a ziggurat or signal bars.
- **Brightness.** The road is brighter than the moon.
- **Earthshine.** There is none, so the disc is lost.
- **Horizon.** It is a hard ledge.

**F. The process failed.**
- Jurors scored the SVG source without looking at renders.
- The concepts shared one skeleton.
- There was no absolute bar, no owner-taste juror and no FFXIV art juror.

## 2. Hard gates (failing any one disqualifies a concept)

**G1. Distinctness.**
- The weakest pair must score at least 10.0 at 16 px and at least 16.0 at 20 px, in both greyscale and deuteranopia + greyscale.
- Also check protanopia and tritanopia.
- No pair may differ only by mirror image, ring colour or hue.

**G2. Salience.**
- Ready is the loudest, at least 1.25 times the next state.
- Completed is at most 0.8 times Ready.
- Every state except Not checked has salience of at least 15 at 16 px.

**G3. Feature floor.**
- Every state-carrying feature is at least 1.5 px (preferably 2) at 16 px.
- Every mark covers at least 4 px².

**G4. Moon identity (the stock-icon test).**
- Jurors give blind first associations at 16 px and 48 px. A glyph fails if 2 or more name a stock UI icon. At most one glyph per set may fail.
- At 48 px, at least 6 of the 8 must be named a moon phenomenon.

**G5. No cheese.**
- No craters or holes in a lit area.
- No radial ball gradient on yellow.
- Saturated gold (C > 0.09) covers no more than a third of any glyph.

**G6. No Moon Road collision.** No dashed circle with a diagonal, no gold orbit ring around a core, no gilt ring with a centre star, no crystal over a crescent.

**G7. Icon physics** (on the 512 master):
- (a) The glade's hue is within ±8° of the moon's, and its chroma is at most the moon's + 0.03.
- (b) The glade is never brighter than the moon.
- (c) The glade's axis is within ±10/512 of the disc centre, with the disc shown by earthshine at 1.15:1 or more. Without earthshine, it is within ±10/512 of the lit centroid.
- (d) The glade is broken by ripples:
  - at least 12 glints, with no two rows mirroring each other;
  - lengths and gaps vary by at least 15% from row to row;
  - glints get thicker toward the viewer and denser toward the horizon;
  - the edges are ragged, with no triangle or pyramid shape;
  - every glint is at least 12/512 tall.
- (e) The lit limb faces 15–60° below horizontal. The glade does not copy the crescent's shape, except as a true mirror image.
- (f) The sea mirrors the sky's brightness at the horizon.
- (g) The Installed-check corner stays quiet, the rim is at least 2:1 against #101010, and the icon survives at 32 px.

## 3. Scored targets

- **T1. FFXIV-flavoured but original.**
  - Declare at least 2 idioms used: gilt bevel, aether or crystal, armillary ticks, the quest-marker grammar, moon lore, weather.
  - Shown with no context, at least 3 of 5 jurors say it "belongs in FFXIV".
  - Nothing traceable to an SE asset.
- **T2. Detail by tier.**
  - Row (20 px and under): at most 3 elements and 3 tones.
  - Mid (24–40 px): adds material.
  - Hero (48 px and up): at least 3 allowed craft details: hatching, a bevel with ticks, earthshine, a crisp halo, glints, filigree, facets, a mist band.
  - The icon has at least 6 elements at 128 px and collapses to 3 masses at 32 px.
  - The tiers map onto Flair: Full shows hero detail, Quiet shows mid, Plain shows row.
- **T3. Coherence with Moon Road.** Use the existing tokens plus at most 2 new ones, and match the ornament brass and the 1.2 px monoline.
- **T4. Icon and glyph unity.** Ready is a miniature of the icon.
- **T5. Owner taste.** Static, no meaningless marks, flair, never cheese.

## 4. State metaphors (recommended)

| State | Recommended | Notes |
|---|---|---|
| Ready | **Moon with its moon road beneath** (2–3 broken gold glints) | The plugin's name, and it makes the icon "Ready, scaled up". Halo only with astrolabe ticks; no sparkle star |
| Ready on another job | **Moon with a job crystal** (Tide facet in the dark half) | Only if In journal drops its diamond and the Moonlit ornament is differentiated; check tritanopia |
| In journal | **Journal ribbon or bookmark tab**, with a wax seal at hero size | Hang it top-left if Ready uses the space below |
| Blocked | **New moon with earthshine**: an ashen disc with a thin bright limb | "Not lit yet"; the filled body kills the radio-button read |
| Done this cycle | **Waning half with a check**; tide line or cyclic tick at hero size | Waning already says "comes back" |
| Completed | **Quiet full moon**, no halo at row size; engraved gilt bezel at hero size | Mist wisps move to Not checked |
| Locked out | **Blood moon with an ofuda sealing band**: copper-dark disc, notched diagonal band, no ring | Lore: Dalamud, the red prison moon |
| Not checked | **Oborozuki**: faint disc with 2 mist bands running past the edge | Test head to head against the open dashed ring |

Rejected:
- a rising moon, which reads as a sunrise;
- glow or rays as the main carrier;
- a quill, which reads as an edit pencil;
- gates or bars, which read as pause or prison;
- an hourglass;
- a chain, which reads as a link;
- a moon rabbit, which reads as noise.

Family-level options, hero tier only:
- an astrolabe bezel with ticks;
- 3–5 crystal facets.

## 5. Focus-group rubric (mandatory)

Jurors view rendered PNGs only. The set includes:
- the render sheet;
- the metrics printout;
- a 16-row mock over game screenshots;
- a hero sheet;
- the icon under Dalamud's overlays.

The procedure:
1. A blind first-association pass.
2. The gate checks.
3. Scores of 1–10, with one sentence of evidence each.

The panel: a veteran raider, a new ARR player, a deuteranope (plus a tritan check), a glamour player, an FFXIV UI art juror and an owner proxy.

**Glyph weights (round-1 anchor in brackets):**
- Moon identity, 15% [3]
- FFXIV flavour, 15% [2]
- Craft by tier, 15% [3]
- Row legibility, 15% [7]
- Distinctness, 10% [6]
- Meaning without a legend, 10% [7]
- Moon Road coherence, 10% [5]
- Owner taste, 10% [4]

**Icon criteria, 25% each:**
- Physical consistency [3]
- Readability at 64 and 32 px and under overlays [7]
- FFXIV detail [2]
- Unity with the glyphs [3]

**Ship bar:**
- Glyphs and icon each score at least 8.0, with no criterion under 6.
- The owner proxy gives at least 8.
- The concept beats the anchor by at least 3 on identity, flavour, craft and icon physics.
- It loses no more than 1 on legibility and distinctness.

**Divergence rule:**
- Each concept declares its FFXIV references and its tier plan.
- Ring + slash, a plain dashed circle and a plain half-in-ring are banned unless explicitly justified and passing G4.
