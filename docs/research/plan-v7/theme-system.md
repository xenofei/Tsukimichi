# Plan v7: the theme system (owner point 7)

Research and design only. No product code was changed for this document.

> "Improve the previous moon concepts from before, make sure they're beautiful and improved on. Implement them as
> options within the plugin. Users can choose themes, or also mix-and-match icons as well (and even ui/ux color
> palettes)." (the owner, 2026-10-03)

## 0. Summary

- **Three independent axes, one preset.**
  - **Glyphs:** which set draws each of the eight quest states.
  - **Palette:** the UI colours: surfaces, text, accent and state inks.
  - **Frames:** the metal of the medal rims and badges, and the Decoration ornament of cards, rules and tooltips.
  - A **theme** is a named preset of all three. Picking a theme sets the three axes. Customising any of them turns the theme into "Custom (based on X)".
- **Every glyph set speaks one state grammar** (§2), learned from rounds 2 to 5. Sets differ in material, frame and locale, never in what the marks mean. This is what makes mixing safe: a mixed set never has two meanings for one mark.
- **Faces, frames and badges are separate layers.**
  - A mixed set takes its frames from one kit, so the column keeps one cohesive border colour. That was the owner's own round 2 note on Medallion.
  - The faces carry the per-state choice.
- **Five glyph sets plus Classic:**
  - Menphina's Medallion (default, unchanged);
  - Aether Crystal, Ishgard Glass, Astrologian's Orrery and Sumi to Kinpaku, all revived with their round 1 and 2 failures fixed;
  - Classic (1.11), kept whole-theme only.
- **Five palettes:** Night (default), Dawn, Ishgard Snow (the first light palette), Kugane Lacquer, and Follow Dalamud. Each has a high-contrast form.
- **Legibility in mixes comes from data, not from rules of thumb.** The build renders every face of every set on every palette, through greyscale and three colour-vision simulations. It ships a pairwise distinctness and salience table. The Themes page looks up the 28 pairs of a mix and warns, in plain words, when two choices are too close or when Ready stops being the loudest.
- **Rendering:**
  - Medallion keeps its procedural row meshes.
  - The new sets use pre-rendered atlases at hero sizes and whole-pixel row strips at row sizes, loaded lazily per set in use.
  - `gen_atlas.py` becomes a multi-theme build that also runs the gates.
- **Phasing:**
  - **1.14:** the infrastructure, Ishgard Glass and Aether Crystal, and the Night, Ishgard Snow and high-contrast palettes.
  - **1.15:** mix-and-match, frames and share codes.
  - **1.16:** Orrery, Sumi, Dawn and Kugane Lacquer.

---

## 1. What exists today (1.13.0)

| Piece | Where | What it does |
|---|---|---|
| Moon style switch | `Core/Ui/MoonStyle.cs`, `Configuration.MoonStyle`, Settings › General › Look | Medallion (1.12) or Classic (1.11). `Theme.ClassicMoons` makes `MoonGlyph` dispatch to `LegacyMoonGlyph`. |
| Glyph facade | `Ui/MoonGlyph.cs` | Every call site (71 draws across 36 files) goes through `Draw`, `DrawInline`, `DrawVeiled`, `DrawHalo` and `DrawFilling`. This is the seam the theme system plugs into. |
| Medal renderer | `Ui/MedalGlyph.cs` | Medals under 32 device px (`MedalLayout.RowTierMaxPx`) are procedural meshes (`MedalArt` → `MedalMesh`, written straight into the draw list). From 32 px they use the hero atlas (`MedalAtlas`). Badges sit at (95, 95) in the 128-unit box. A row's badge goes beside the medal at text height (`DrawRowBadge`). |
| Medal geometry and tokens | `Core/Ui/MedalArt.cs` (1,513 lines), `MedalMesh.cs`, `MedalTokens.cs` | A hand transcription of `gen5.py`. The finishes are Standard (gilt), LightRim (Quiet), Plain (a flat ladder) and the two high-contrast variants. |
| Hero atlas | `assets/ui/medals.png` (782 × 574, 332 KB) and `@2x` (1564 × 1148, 996 KB), from `docs/design/moon-v6/round5/gen_atlas.py` | 11 sprites (7 states, plus Ready on another job once per role seat) at 4 tiers (48/64/96/128). `MedalLayout` mirrors the JSON, and `MedalTests` holds them together. In memory: 1.8 MB at 1x and 7.2 MB at 2x. |
| Decoration | `Core/Ui/Flair.cs`, `FlairTones.cs` | Full, Quiet and Plain set the pane tone, rules, medal finish and spacing. High contrast caps it at Quiet. |
| Surfaces | `SurfaceColors` (in `ColorMath.cs`) | 17 roles: Night, or mapped from the Dalamud style (`FromHost`), with a `ForHighContrast()` transform. Already used about 354 times. |
| Fixed tokens | `Theme.Moon`, `Silver`, `Dusk`, `Eclipse` and friends, from `GlyphTokens` | Static, so they never follow a palette. `Theme.Moon` and `MoonU32` are used 151 times, and Silver, Dusk and Eclipse about 75 more. |
| Glyph palette | `GlyphPalette` and `GlyphPaletteKind` (Standard or HighContrast) | "Moon colours". High contrast resolves to a dark or light variant by host luminance. |
| Ornaments | `Ui/Ornament.cs`, `assets/ui/ornaments.png` | The brass ramp (`BrassHigh`…), corner marks, sigil star, divider, crest and the tree's category glyphs. |
| Tests | `MedalTests`, `MedalCoverageTests`, `PlainMedalTests`, `GlyphPaletteTests`, `MoonRoadPaletteTests`, `FlairTonesTests`, `ColorVisionTests` | Atlas layout, contrast ladders, CVD separation (Machado 2009 in `ColorVisionSimulation`). |
| Design metrics | `docs/design/moon-v6/round2/metrics.py` (round 5's is identical) | Chrome renders 8 states at 16 and 20 px. Distinctness is the summed blurred luminance difference per pair. Salience is the summed difference from the background. Greyscale and Vienot deuteranopia. |

The round history that shapes this design:
- **Round 1, Sumi to Kinpaku.** It was chosen by its focus group but failed the round 2 audit. It read as a stock status set (radio button, no-entry sign, check_circle), its sumi well measured 1.05 : 1, and it had no FFXIV identity.
- **Round 2: Aether Crystal 6.70, Orrery 5.76, Ishgard Glass 5.87, Medallion 6.91.**
  - The owner did not like A, B or C, and kept D.
  - The owner also said that D's aetheryte crystal "doesn't make sense" and that "the borders should share one cohesive colour".
- **Rounds 3–5** built the grammar Medallion now ships:
  - the moon road inside the well;
  - the open and closed padlock badges, the book badge and the job-icon badge;
  - the new moon behind cloud;
  - the "comes back" arc on Done;
  - the gilt check on Completed;
  - the cracked Dalamud;
  - the "?" moon.
- **Round 5 gate:** the weakest pair is at least 12 at 16 px, Ready is at least 1.3× the next state, and Completed is at most 0.8× Ready.

---

## 2. The shared state grammar (the contract every glyph set signs)

Mixing is only legible if a mark means the same thing in every set. Every set, old or new, must carry these carriers at **row tier** (with the hero additions in brackets):

| State | Required carrier | Badge (hero, ≥ 32 px; beside the row below that) | Frame tier |
|---|---|---|---|
| Ready | A lit moon with its **moon road** of 2–3 broken glints under the lit centroid, **inside the well** (never a stalk below the disc) | Open padlock | **Act now** (gilt in every kit) |
| Ready on another job | The same moon, **no road** | The game's job icon on its role seat | Resting |
| In journal | A **ribbon or bookmark** hanging over the top-left of the rim, and a moon | Book | Resting |
| Blocked | An ashen **new moon with a thin lit limb, obscured** (cloud, or the material's equivalent of cloud) | Closed padlock | Resting |
| Done this cycle | A **waning half** with a **"comes back" arc** along the dark edge | – | Resting |
| Completed | A quiet **full moon** with a **gilt check** crossing the lower-right edge | – | Finished (dimmer) |
| Locked out | **Dalamud red, cracked** (dark socket gaps ≥ 12 units) | – | Resting |
| Not checked | A **"?"** in the set's material over a faint moon | – | Ghost (lowest) |

**Banned in every set.** Each item comes from round 1–2 failures or owner vetoes:
- a ring plus slash, an ofuda band, or any diagonal bar through a disc (the no-entry reads);
- a dashed ring with a diagonal (collides with the Moon Road's `glyph-removed`);
- a check in a circle without a moon;
- a stalk or glints below the disc (lollipop or magnifier);
- glow as the main carrier at row tier;
- two parallel bars (hamburger);
- 24-tick bezels at row or mid tier (sun rays);
- a crystal over a crescent (collides with the Moonlit ornament);
- a crystal on Ready on another job (the owner's veto; the job badge replaced it);
- an aetheryte (the owner's veto);
- a 4-point star (the "AI sparkle");
- craters or holes in a lit area, or a radial ball gradient on yellow (cheese).

**The badge contract.**
- One badge frame spec for every set: centre (95, 95), keyline 24, seat 19.9, job slot 35.5, in the 128-unit box.
- So a face must keep the lower-right badge disc clear of its state-carrying marks. The build measures this (§6).
- Role seat colours (tank, healer, DPS, Hand) are fixed across all kits, so a job reads the same in any theme.

**The tier contract.** These match `MedalLayout` and the Decoration mapping:
- Row is under 32 device px: at most 3 masses and 3 tones.
- Mid is 32–47 px: adds material.
- Hero is 48 px and up: at least 3 craft details, all as hairlines at partial opacity.

---

## 3. The theme model

### 3.1 Axes and presets

```
Theme preset  =  { Glyphs: GlyphSetId for each of 8 states,  Palette: PaletteId,  Frames: FrameKitId }
Appearance    =  Theme preset  +  user overrides (any subset of the 8 states, palette, frames)  +  High contrast flag
```

| Theme (preset) | Glyph set | Frame kit | Palette | Status |
|---|---|---|---|---|
| **Menphina's Medallion** (default) | Medallion | Brass | Night | Ships today. Unchanged here; owner point 5 (darker craters, a glow) is a separate row. |
| **Aether Crystal** | Aether Crystal | Silver | Night | Revived (§4.1) |
| **Ishgard Glass** | Ishgard Glass | Came (lead) | Ishgard Snow | Revived (§4.2) |
| **Astrologian's Orrery** | Orrery | Astrolabe | Dawn | Revived (§4.3) |
| **Sumi to Kinpaku** | Sumi | Kirikane (cut leaf) | Kugane Lacquer | Revived from round 1 (§4.4) |
| **Classic** | Classic (1.11) | Brass | Night | Legacy. Whole theme only; not offered in the per-state mix (§3.4). |

Palettes and frame kits pair freely: Ishgard Glass glyphs on Night with Brass frames is a valid choice.

### 3.2 What a glyph set is

```csharp
// Tsukimichi.Core/Ui/Themes/GlyphSet.cs (proposed)
public enum GlyphSetId : byte          // stable forever: share codes store these numbers
{ Medallion = 1, Classic = 2, AetherCrystal = 3, IshgardGlass = 4, Orrery = 5, Sumi = 6 }

public sealed record GlyphSetInfo(
    GlyphSetId Id, string Key, string Name,          // "ishgard-glass", "Ishgard Glass"
    GlyphRenderKind Kind,                            // Procedural (Medallion, Classic) | Atlas
    FrameKitId DefaultFrames, PaletteId DefaultPalette,
    bool Mixable,                                    // false for Classic
    bool HasPlainFinish);                            // the flat row strip exists (else Medallion's Plain ladder stands in)
```

The faces of a set:
- 8 faces, unframed: the well and emblem only, in the 128-unit box.
- Ready on another job has an empty seat. The job icon and the seat come from the badge layer.
- Each face comes at hero tiers (48/64/96/128 and their 2x) and as a row strip.
- The row strip is **every whole device pixel from 12 to 31**, in the Full finish, plus the Plain flat finish where the set has one.

### 3.3 Frame kits (frames, badges and Decoration ornament)

A frame kit owns everything that is metal or border:

| Layer | Contents | Notes |
|---|---|---|
| Medal frames | Four urgency tiers: **act now** (Ready), **resting**, **finished** (Completed), **ghost** (Not checked). Each comes at Full (the kit's rim) and Quiet (a 1 px hairline, like today's `MedalTokens.LightRim`). | **Act now is gilt in every kit.** The colour of the urgency signal stays constant; only the resting metal changes (brass, silver, lead, astrolabe brass, kirikane). |
| Badges | The badge rim, plus the three badge contents (open lock, book, closed lock) rendered in the kit's metal. Role seats stay fixed. | The row badges at text height stay procedural (`MedalArt.RowGlyph`), recoloured from kit tokens. At 12–16 px the material barely shows, so one drawing serves every kit. |
| Gauges | The halo groove, the arc metal and the filling moon's colours (`MedalGauge`, parameterised) | Classic keeps `LegacyMoonGlyph`'s gauges. |
| Decoration ornament | The card frame ramp (today's `Ornament.BrassHigh`… four stops), corner marks, rule ink and alpha, divider, sigil sprite, crest, and the tooltip frame | A small per-kit `ornaments.png` (about 15 KB). The tree's category glyphs stay shared, tinted by the kit's ornament ink. |

| Kit | Resting metal | Corner mark / sigil | Character |
|---|---|---|---|
| **Brass** | Today's gilt brass (#E2C78C → #5C4724) | Today's corner L and four-point sigil | The Moon Road as shipped |
| **Silver** | Moonstone silver (#E2E8F4 → #5E6E97) | A faceted corner chip and a cut-gem sigil | Cool and calm; for Aether Crystal |
| **Came** | Lead (#8C95B0 → #323950), with a thin gilt inner line on act-now only | A quatrefoil corner and a rose-window sigil | For Ishgard Glass; reads on light palettes |
| **Astrolabe** | Brass with a hairline scale at hero only | A quarter-arc scale corner and an eight-point compass sigil | For Orrery |
| **Kirikane** | Thin cut gold leaf over sumi (#E0B860) | A square kamon corner and a crescent-in-circle crest sigil | For Sumi |

Why frames are their own axis:
- In a mix, frames come from **one** kit, so the column keeps a single border colour. This answers the owner's round 2 note directly.
- Frames are identical across states within an urgency tier, so they add no distinctness risk. The act-now tier only widens Ready's lead.
- Default rule: frames follow the theme. When the user mixes faces, frames stay with the theme's kit until they change them. They never flip state by state.

### 3.4 Where Classic and Medallion fit

- **Medallion** becomes `GlyphSetId.Medallion`, a procedural set.
  - Its row tier stays `MedalArt` meshes, proven and tested.
  - Its hero atlas is rebuilt by the new tool into faces plus the Brass kit's frames and badges. The pixels are identical, which `MedalTests` holds.
  - `MedalTokens` finishes map to kit frames: Gilt is the Brass Full frame, LightRim the Brass Quiet frame, and Plain stays Plain.
- **Classic** becomes `GlyphSetId.Classic`, procedural, drawn by `LegacyMoonGlyph` as today.
  - It is **whole-theme only.** It predates the grammar: its Locked out is a barred ring and its Not checked a dashed ring, which are exactly the banned reads.
  - Mixing it with grammar sets would give two meanings for one state.
  - Its glyphs also can't be rendered by the Python build without a separate port, so they can't enter the matrix.
  - The old Moon style switch becomes the theme choice. `MoonStyle.Classic` migrates to theme Classic.
- **High contrast** stays a single shared set, the Medallion shapes on the HC ladder (`MedalTokens.HighContrast*`), whatever the theme or mix.
  - It is the low-vision guarantee, and every test in `GlyphPaletteTests` stays valid.
  - The mix returns when high contrast is turned off.
  - The Themes page says so in one line.

### 3.5 Decoration × theme

| Decoration | Faces | Frames | Ornament |
|---|---|---|---|
| Full | Hero atlas or procedural; row strip at row size | The kit's Full rim | The kit's ornament (brass, silver, came…) |
| Quiet | The same faces | The kit's Quiet hairline | Hairline rules in the palette's line colour (unchanged) |
| Plain | The set's flat row finish at every size (`HasPlainFinish`), otherwise Medallion's Plain ladder | None (the flat glyph carries a 1 px state rim, as today) | None (unchanged) |
| High contrast (caps at Quiet) | The shared HC set | HC rim | Strong-line ornament (unchanged) |

---

## 4. The revived concepts: improved, and held to the grammar

Process for each set, as in rounds 3–5:
1. A designer brief, built from the fix list below.
2. Rendered sheets.
3. Review by the realism supervisor and the design critic.
4. Pass the metric gates (§7.1) on Night, on the set's paired palette and on a daylight scene sample.
5. A blind first-association juror pass (G4: at most one glyph per set may read as a stock icon).
6. The owner sees the sheets and a mock interface on the plan site before production.

Art masters live in `docs/design/v7/themes/<set>/` (the empty folder already exists).

### 4.1 Aether Crystal: "moonstone in a silver bezel"

**Keep:**
- the best row legibility of round 2, liked by the veteran raider and the colour-blind juror;
- shape-first reading;
- calm, cool material with no cheese.

**Material:** a faceted moonstone cabochon with one facet split at mid tier and 4–5 hairline facets at hero, set in a silver bezel.

| State | Round 2 failure | Improved face |
|---|---|---|
| Ready | Lollipop or magnifier at 16 px; the teal glow read as a mobile "claim" button | Thick crystal crescent over **three shard-glints inside the well**. Act-now gilt bezel. A pale aether bloom (#9BE6FF at ≤ 15%) at Full hero only. Salience must pass at row **without** the glow. |
| Ready on another job | The crystal-in-dark-half collided with Moonlit | The same crescent, no road, an empty seat for the job badge. **No crystal.** |
| In journal | Fine | Gibbous plus a silver-blue silk ribbon over the top-left; book badge. |
| Blocked | RoJ-Blk was the weakest pair (17.6) | An ashen crystal new moon with a bright limb, behind a **faceted frost bank** (low-poly cloud) in the set's material; closed-lock badge. |
| Done this cycle | Read as check_circle | A waning half plus an arc of small facets along the dark edge (the "comes back" arc). |
| Completed | Read as a coin | A brighter cabochon face with a pale edge band and a gilt check crossing the lower right. |
| Locked out | Ofuda band read as no-entry (6 of 6 jurors) | Dalamud as a **fractured red crystal**: cleavage gaps ≥ 12 units, socket-dark, no band. |
| Not checked | Two mist bars read as a hamburger | A dim disc with a **"?" cut as a frosted facet**. No bars. |

**Paired palette:** Night. **Kit:** Silver. **New tokens:** at most 2 (aether high and aether low), hero only.

### 4.2 Ishgard Glass: "the Holy See's rose window"

**Keep:** the most FFXIV-specific idea of round 2, and the Ishgard skyline, the round's best FFXIV detail.

**Material:** stained-glass roundels of ivory and lapis glass, held in lead came.

| State | Round 2 failure | Improved face |
|---|---|---|
| (all) | Lead lines vanished below 48 px, so the glyphs became plain discs | The row tier gets **one 1.5 px came along the terminator**, and the two glass tones carry the phase. Voussoir came only from mid tier. |
| Ready | Lollipop | A gibbous ivory-glass moon over a sea of glass tesserae **inside the well**. Hero adds the **Ishgard skyline** on the far shore with its own small rose window. Act-now gilt came. |
| Ready on another job | Read as an "AI sparkle" | A crescent in glass with an empty job seat. |
| In journal | Fine | A half moon plus a swallow-tailed glass ribbon over the top-left; book badge. |
| Blocked | (fine at 48 px) | Ashen glass new moon behind **cloud-shaped leaded panes**; closed-lock badge. The thin moonstone limb separates it from Not checked (round 2's weakest pair, 12.9). |
| Done this cycle | Check-in-circle at 16 px | A waning half in glass plus a gilt came arc along the dark edge. |
| Completed | A plain filled dot at 16 px | Dim ivory full-moon glass, with a gilt check crossing the lower-right came. |
| Locked out | No-entry sign | Dalamud in red glass, **shattered**: the panes are cracked and offset, the lead still holds, and the socket gaps are dark. |
| Not checked | Almost invisible on daylight | An unleaded frosted pane with the **"?" drawn in lead**, keylined, bright enough for daylight. |

**Paired palette:** Ishgard Snow (it also passes on Night). **Kit:** Came.

The round 2 icon complaint ("a gilt-framed plate") does not apply: the plugin icon stays Medallion's. Dalamud reads the icon from the manifest, so it can't change per user.

### 4.3 Astrologian's Orrery: "a Sharlayan instrument"

**Keep:**
- Blocked's engraved constellation;
- the small rose Dalamud, which becomes the cracked moon's colour;
- round, instrument-like craft.

**Material:** brass astrolabe with lapis star-chart enamel.

| State | Round 2 failure | Improved face |
|---|---|---|
| (all) | The 24 ticks read as sun rays | **Ticks at hero tier only**, in the Astrolabe kit's frame (12 notches). Row and mid tiers are a clean rim. |
| Ready | A lightbulb or user icon at 16 px, scaled 0.88 | Full-size gibbous moon over its road inside the well. A hero-tier hairline limb scale. |
| Ready on another job | A crystal | A first-quarter moon with an empty job seat. |
| In journal | Fine | A crescent plus an ember ribbon; wax seal at hero; book badge. |
| Blocked | Liked | A new moon whose earthshine carries the **engraved constellation**, behind a cloud band; closed-lock badge. |
| Done this cycle | Fine | A waning half plus a "comes back" arc drawn as a short scale arc with an arrowhead. **No bead on the arc**, which would collide with the orbit-reference ornament. |
| Completed | A gold coin | A full moon plus a gilt check. The engraved bezel only at hero. |
| Locked out | A stray slash | A cracked Dalamud in rose-red (#D89A90 lit, #8E2A2E deep). |
| Not checked | Saturn | A **"?" as a constellation**: stars joined by hairlines at hero, a solid "?" glyph at row. |

**Paired palette:** Dawn. **Kit:** Astrolabe.

### 4.4 Sumi to Kinpaku: "tsukimi crests" (round 1, revived)

**Keep:**
- the only set every round 1 juror chose;
- every pair splits by silhouette;
- gold only as cut leaf;
- crest calm, with no cheese.

This is the flat, quiet choice, and the natural partner for Decoration Plain.

| Round 1 failure | Fix |
|---|---|
| Read as a stock status set (radio, no-entry, check_circle, spinner) | Adopt the full grammar of §2: road, ribbon, clouded new moon, comes-back arc, check on a full moon, cracked Dalamud, "?". |
| The sumi well was invisible (1.05 : 1 on Night) | A lifted sumi seat (#161B2C) plus a gofun (shell-white) hairline ring at 30%, measured ≥ 1.3 : 1 against every palette's window. |
| Flat, with no material at large sizes | Craft by layering, not texture: kirie (cut paper) planes with a 1 px offset shadow at mid tier, and kirikane strips at hero. Still no gradients on the lit moon. |
| No FFXIV identity | Kugane and Hingashi crest idioms (round 5's icon already uses the Kugane shore), plus Dalamud lore on Locked out. |

**Paired palette:** Kugane Lacquer. **Kit:** Kirikane.

### 4.5 Medallion and Classic

- **Medallion** is unchanged by this plan. Owner point 5 (craters, glow) changes it through the normal art loop, and the rebuilt atlas flows through the new tool.
- **Classic** is frozen.

---

## 5. Mix-and-match

### 5.1 What the user can do

- **Per state:** choose any mixable set for each of the 8 states. Default: "From theme".
- **Frames:** choose one kit for the whole set. Default: "From theme".
- **Palette:** any palette, independent of the glyphs.
- Everything is applied live. Every change can be undone.

### 5.2 Legibility constraints

**Hard constraints** (the UI does not offer the choice):
- Classic can't be mixed.
- High contrast overrides the mix (shared HC set).
- Frames are one kit per mix.

**Measured checks.** These warn and never block:

| Check | Source | Threshold (16 px, worst of greyscale, deuteranopia, protanopia and tritanopia, on the palette's window) | Message |
|---|---|---|---|
| Pair distinctness, for each of the 28 pairs of different states | The matrix (§6.4) | 10 to 12: "close". Under 10: "hard to tell apart". The round 5 ship bar is 12. | "In a list, *Blocked* (Ishgard Glass) and *Not checked* (Aether Crystal) look alike." |
| Ready salience | The salience table | Ready must be ≥ 1.25× every other state | "*Ready* is no longer the brightest moon in the column." |
| Completed recedes | The salience table | Completed ≤ 0.8× Ready | "*Completed* now draws the eye more than *Ready*." |
| Plain finish | The matrix's Plain column | The same thresholds, checked at Decoration Plain | Shown only when Decoration is Plain |

**Why pixel data and not rules:**
- Every pure set already passes its own gates, but a mix can pair two faces that were never side by side.
- Round 2 showed the failure modes are visual (RoJ-Blk, Blk-NotC, Lock-NotC), not taxonomic.
- So the build renders every face, and the runtime only looks numbers up. There is no GPU readback and nothing to compute per frame.

**The "Fix it" suggestion:**
- For a flagged mix, try every single-state change (8 states × up to 5 sets = 40 candidates, each 28 lookups).
- Offer the change that clears every warning, preferring the set that already covers the most states.
- If no single change clears them, offer "Use the theme's own *X*".

**Option hints.** In the per-state combo, an option that would create a "hard to tell apart" pair shows a quiet amber pip and a tooltip. The user sees it before choosing.

### 5.3 Data model

```csharp
// Configuration (new); old MoonStyle / GlyphPalette / FollowDalamudColours kept and written for one release (downgrade)
public sealed class AppearanceConfig
{
    public int Version { get; set; } = 1;
    public string Theme { get; set; } = "medallion";               // preset key
    public Dictionary<string, string>? Glyphs { get; set; }       // "Ready" -> "ishgard-glass"; absent = from theme
    public string? Palette { get; set; }                          // null = from theme; "dalamud" = Follow Dalamud
    public string? Frames { get; set; }                           // null = from theme
    public bool HighContrast { get; set; }
}
```

- Keys are strings, so the JSON stays readable and survives enum reordering.
- `GlyphSetId` numbers exist only for share codes, and a test pins the id-to-key table.

**Migration** (in `Configuration`'s existing sanitise pass, beside the `MoonStyle` and `GlyphPalette` checks near line 700):
- `MoonStyle.Classic` becomes theme `classic`.
- `GlyphPalette = HighContrast` becomes `HighContrast = true`.
- `FollowDalamudColours` becomes palette `dalamud`.
- Unknown keys fall back to the theme's own choice, with a one-time notice.

**Resolution** (pure, in Core, tested): `AppearanceResolver.Resolve(config, hostStyle) → ResolvedAppearance`.
- It produces 8 set ids, a `UiPalette` (with the HC transform applied), a `FrameKit` and the medal finish per Decoration level.
- `Theme.Refresh` calls it once per frame and allocates nothing.
- The result is cached and rebuilt only when the config or the host style changes.

### 5.4 Share codes

- **Format:** `TM1-XXXX-XXXX-XXXX`, Crockford base32, case-insensitive. O reads as 0 and I/L as 1, so codes survive chat and handwriting.

| Field | Bits |
|---|---|
| Format version | 4 |
| Theme preset | 4 |
| Palette (0 = from theme) | 4 |
| Frames (0 = from theme) | 4 |
| High contrast | 1 |
| Has overrides | 1 |
| 8 states × set id (0 = from theme), only when overrides are present | 32 |
| CRC-8 | 8 |

- **Length:** a preset-only code is 26 bits, which is 6 characters (`TM1-7Q4K2X`). A full mix is 58 bits, which is 12 characters.
- **Paste flow:**
  1. Validate the checksum.
  2. Show "This will change: Ready → Aether Crystal, Palette → Dawn…".
  3. Apply, with an Undo toast (`UndoTimer`).
- Unknown ids (from a newer build) fall back and are named.
- A code carries ids only, never colours or text, so there is nothing to sanitise beyond range checks.

---

## 6. Rendering pipeline per theme

### 6.1 Renderer seam

```csharp
// Tsukimichi/Ui/Themes/IGlyphSet.cs (proposed)
internal interface IGlyphSet
{
    GlyphSetId Id { get; }
    // Face only: frames and badges are drawn by the kit layer
    bool TryDrawFace(ImDrawListPtr dl, QuestState state, Vector2 min, float sizePx, MedalFinish finish, uint tint);
}
```

- `MoonGlyph` stays the facade, so none of the 71 call sites changes.
- `MedalGlyph.Draw` becomes a compositor:
  1. Look up `ResolvedAppearance.SetFor(state)`.
  2. Draw the kit frame for the state's urgency tier.
  3. Draw the face.
  4. At hero size, draw the badge (and the job icon into the seat, as today).
  5. Below 32 px, draw nothing extra; the caller's `DrawRowBadge` puts the badge content beside the medal as today.
- `DrawVeiled`, `DrawHalo` and `DrawFilling` take their material from the frame kit. Classic short-circuits to `LegacyMoonGlyph`, as `Theme.ClassicMoons` does now.
- `Theme.PushAppearance(resolved)` is an allocation-free struct scope like `PushFlair` and `PushGlyphs`. It powers the live preview and the glyph window's A/B tab.

### 6.2 Row tier: procedural or row atlases?

| | Procedural meshes (`MedalArt` style) | Whole-pixel row strips (new) |
|---|---|---|
| Fidelity | Hand-transcribed SVG with no blurs (`MedalArt` drops shadows and soft glints) | Chrome renders the row SVG master at the exact pixel size, so it matches the master |
| Cost per new set | About 1,500 lines of C# geometry plus coverage tuning, a second source of truth | Draw `_row/*.svg` masters; the build does the rest |
| Fractional sizes | Any size | Whole device px only. `MedalGlyph.Box` already snaps to whole px, so this is no loss. |
| Recolour (HC, Plain) | Token swap | HC is the shared set anyway; Plain is a second strip rendered from a flat master |
| Memory | Vertex buffers per size | About 0.3 MB per finish per set (below) |
| Batching | Batches with text (font texture) | Each glyph is an `AddImage`, so a texture switch from text adds about 2 draw commands per row. A 40-row table costs about 80 commands, which is trivial for D3D11. If profiling ever objects, `ImDrawListSplitter` can put glyphs on a channel. |

**Recommendation:**
- **Medallion keeps procedural rows.** They exist, are tested and are pixel-tuned.
- **Every new set ships row strips.**
- Strips cover 12–31 device px. At 32 px and up the hero tiers apply, never shrunk more than 1.5× (`MedalLayout.Pick` logic, generalised).
- Until a strip loads, the Medallion procedural face of the same state stands in. It is always available and always legible, and it shows for at most a few frames.

### 6.3 Atlases, memory and lazy loading

Per set (estimates from the shipped atlas's measured density, 332 KB for 1.8 MB of pixels):

| Atlas | Content | Pixels | VRAM | PNG |
|---|---|---|---|---|
| `faces.png` | 8 faces × 48/64/96/128 | about 256 k | 1.0 MB | about 180 KB |
| `faces@2x.png` | The same at 2x (only drawn above 128 device px) | about 1.0 M | 4.1 MB | about 550 KB |
| `row.png` | 8 faces × 12…31 px × (Full, Plain) | about 160 k | 0.64 MB | about 70 KB |
| Kit `frames.png` (+@2x) | 4 urgency frames × 2 finishes + 7 badge sprites (3 contents + 4 seats) × 4 tiers | about 480 k | 1.9 MB (7.7 at 2x) | about 200 KB (+600) |
| Kit `ornaments.png` | Corner, sigil, divider, crest | about 33 k | 0.13 MB | about 15 KB |
| `previews.png` (all sets) | Each set's 8 faces framed by its own kit, at 28 and 40 px, for the Themes cards | about 120 k total | 0.5 MB | about 60 KB |

**Loading rules** (`ThemeAtlasCache`, generalising `MedalAtlas`):
- Request only the sets and the one kit in `ResolvedAppearance`.
- Load row strips at plugin start for those sets, and hero tiers on the first hero draw.
- Load the 2x tier only above 128 device px (unchanged).
- The Themes page loads `previews.png` only. Hovering or selecting a card requests that set's atlases for the live preview.
- When a set drops out of the appearance, its `ISharedImmediateTexture` references are released, and Dalamud's shared texture cache frees them after its idle period.

**Budgets** (enforced by a test that sums layout JSONs):
- Typical: one set and one kit at 1x is about 3.5 MB, against 1.8 MB today.
- Worst case: a 5-set mix at 1x with every hero tier touched is about 7 MB.
- Hard cap: ≤ 12 MB at 1x for any reachable appearance.

**Packaging:**
- The 5 sets and 5 kits add about 5 MB of PNG.
- Ship them as content files under `assets/themes/` in the plugin zip, loaded with `ITextureProvider.GetFromFile`, rather than growing the DLL's embedded resources.
- Medallion's two PNGs stay embedded, so the default never touches disk.

### 6.4 The multi-theme build (`tools/themes/build_themes.py`)

It replaces `docs/design/moon-v6/round5/gen_atlas.py`. The old script stays as history, and its logic moves into the tool. It reuses `docs/design/moon-road/banners/rasterize.py`'s headless Chrome `screenshot`.

```
docs/design/v7/themes/<set>/theme.json      # manifest: id, key, name, kit, palette, tier masters, plain master, tokens
docs/design/v7/themes/<set>/hero/*.svg      # 8 faces (unframed), 128 viewBox
docs/design/v7/themes/<set>/row/*.svg       # 8 row-tier faces (and row-plain/*.svg)
docs/design/v7/themes/kits/<kit>/...        # frames (4 tiers × Full/Quiet), badge rim + contents, ornaments
```

Steps (`python tools/themes/build_themes.py [--set ishgard-glass] [--check]`):

1. **Render.**
   - Every hero master at each tier (1x and 2x).
   - Every row master at each whole pixel from 12 to 31. This is a real render at that size, not a downscale.
   - Transparent background. Ids are prefixed per copy, as `gen_atlas.py` does.
2. **Pack.** Deterministic shelf packing with a 2 px pad (4 at 2x). It writes `layout.json` (sprite → rect per tier or size) beside each PNG.
3. **Previews.** Composite face, kit frame and badge for the 28 and 40 px card strip.
4. **Per-set gates** (§7.1).
   - Composite each face with its own kit's frames on each palette window and on a daylight scene sample.
   - Run the generalised `metrics.py` (greyscale, Vienot deuteranopia for history, plus Machado deuteranopia, protanopia and tritanopia, matching `ColorVisionSimulation` in Core).
   - Fail the build on any gate.
5. **Cross-set matrix.**
   - For every mixable face pair of different states, compute distinctness at 16 and 20 px, Full and Plain finishes, per palette window, taking the worst over the 4 vision modes.
   - Faces are composited with a **neutral reference frame** (resting tier for all, act-now for Ready). Identical frame pixels cancel in the pairwise sum, so the faces decide.
   - Also compute salience per face.
   - Write `assets/themes/matrix.json`: 700 pairs × 2 sizes × 2 finishes × 5 palettes, about 14 k numbers, about 60 KB.
6. **Contact sheets** into `docs/design/v7/themes/<set>/_sheet.png` (`render_sheet.py`, generalised), plus a mixed-set heatmap for the reviewers.
7. **Optimise.** Lossless PNG optimisation. A quantised (pngquant) variant is tried only behind a ΔE guard (max OKLab ΔE ≤ 0.02 per pixel against the lossless render).

`--check` re-renders into a temporary folder and diffs. CI-style use runs it before a release.

**Determinism:** pin the Chrome major version in `theme.json`'s build note. The C# tests compare layout JSON, not pixels, so a Chrome update re-renders the art but can't break the build.

---

## 7. Testing

### 7.1 Per-set gates (build-time, also asserted in C# from `metrics.json`)

| Gate | Bar | Origin |
|---|---|---|
| G1 Distinctness | Weakest pair ≥ 12 at 16 px and ≥ 16 at 20 px, in greyscale, deuteranopia, protanopia and tritanopia, on Night, the paired palette and a daylight sample | Round 2 G1, tightened to round 5's 12 |
| G2 Salience | Ready ≥ 1.3× the next state; Completed ≤ 0.8× Ready; every state but Not checked ≥ 15 | Round 2 G2 and round 5 |
| G2b Glow-free Ready | G2 also holds with Full-only glow removed | Aether Crystal's round 2 weakness |
| G3 Badge clearance | No state-carrying pixel (alpha > 0.25) inside the badge disc | Round 5 `No_badge_covers_the_lit_moon`, generalised |
| G5 No cheese | Saturated gold (OKLCH C > 0.09, gold hue) ≤ ⅓ of any face | Round 2 G5 |
| Seat contrast | Every face's keyline or seat ≥ 1.3 : 1 against each palette window, and ≥ 3 : 1 on light palettes | Round 1's invisible sumi well |
| Fit | Each sprite's alpha bounding box sits ≥ 1 px inside its cell | Atlas bleed |

Done by people, not code:
- G4: blind stock-icon associations.
- G6: Moon Road collision, checked against the kit's own ornaments.
- The supervisor's realism verdict.

### 7.2 C# tests (new or generalised)

- **`ThemeRegistryTests`:**
  - every registered set and kit has its manifest, layout JSON and packaged files (csproj content items);
  - the `GlyphSetId` ↔ key table is pinned, because share codes depend on it;
  - every preset resolves.
- **`ThemeAtlasLayoutTests`** (generalising `MedalTests`):
  - JSON size equals PNG size, and 2x is exactly double;
  - sprites never touch (pad ≥ 2);
  - every state, tier and whole pixel from 12 to 31 is present;
  - `Pick` never shrinks more than 1.5×;
  - the per-set file and VRAM budgets hold;
  - the summed worst case is ≤ 12 MB.
- **`DistinctnessMatrixTests`:**
  - the matrix covers every mixable pair, palette, size and finish, and was built from the current manifests (a hash of the masters is stored in the JSON);
  - **every pure preset passes on every palette**, so defaults never warn;
  - a known-bad fixture mix is flagged.
- **`MixCheckTests`:**
  - thresholds and messages;
  - "Fix it" clears every warning when a single change can;
  - High contrast suppresses mix warnings.
- **`ShareCodeTests`:**
  - round-trip over all presets and over random mixes;
  - the checksum rejects every single-character typo;
  - Crockford aliases decode;
  - an unknown version or id falls back and is reported.
- **`AppearanceMigrationTests`:**
  - Classic, HighContrast and FollowDalamud migrate;
  - the old fields are written back for downgrade;
  - unknown keys sanitise.
- **`PaletteContrastTests`** (`MoonRoadPaletteTests` and `FlairTonesTests` turned into `[Theory]` over every palette × {standard, HC}):

  | Check | Bar |
  |---|---|
  | Text on window, raised and card | ≥ 7 (≥ 4.5 for secondary, ≥ 4.5 for tertiary on the window only) |
  | Accent and every state ink on window, raised, card and Quiet detail | ≥ 4.5 |
  | Cool | ≥ 4.5 (HC: ≥ 7) |
  | StrongLine and Ornament | ≥ 3 |
  | Hover against window | ≥ 1.12 |
  | Quiet panes | About 3 L* apart |

  The colour-vision checks: under every `ColorVisionSimulation` mode, Accent ("act now") against EclipseText ("locked out") differs by OKLab ΔE ≥ 0.08. State inks never carry meaning alone; the glyph and the state's name always accompany them, but these two must not merge.
- **Frame kit tests:**
  - ornament ink ≥ 3 : 1 on each palette window;
  - the act-now frame is the same gilt in every kit, so the urgency colour is constant;
  - corner and sigil sprites fit.

---

## 8. UI colour palettes

### 8.1 The palette record

```csharp
// Tsukimichi.Core/Ui/Themes/UiPalette.cs (proposed)
public sealed record UiPalette(
    PaletteId Id, string Key, string Name,
    SurfaceColors Surface,                 // the 17 existing roles (Window … CoolDeep), Light flag included
    Vector4 Accent, Vector4 AccentDim,     // today's Theme.Moon / MoonDim as text and small ink
    StateInks States,                      // the 8 state text colours (today's Theme.StateColor switch)
    SceneTokens Scene,                     // Full's sky zenith, water lift, star tint (or none), banner grade target
    FlairToneHexes? Quiet, FlairToneHexes? Plain);   // designed hexes; null = FlairTones' generic mix
```

What migrates:

| Today | Becomes |
|---|---|
| `SurfaceColors.Night` | `UiPalette.Night.Surface` |
| `SurfaceColors.FromHost` | The Follow Dalamud palette, built per frame |
| `FlairTones.For`'s `night` special case | The palette's designed hexes |
| `Ornament.Zenith`, the star colours, `BannerGrade`'s night target | `SceneTokens` |
| `Theme.StateColor` | `palette.States` |
| `Theme.Moon` and `MoonU32` (151 uses) when used as text or accent | `Theme.Accent` |

**Moon stays gold in every palette.** Gold means "act now", and that is a meaning, not a mood. A palette may only shift its lightness for contrast, as Follow Dalamud already does with `EnsureContrast`.

**Glyph colours stay fixed.** Medals carry their own enamel wells and keylines, so they read on any palette; the daylight column of every sheet proves it. The palette never recolours a glyph. It only feeds the matrix's backgrounds.

### 8.2 The four designed palettes

Hexes are first proposals for the designer and supervisor to refine. Ratios were measured (WCAG) for this document.

| Role | Night (today) | Dawn | Ishgard Snow (light) | Kugane Lacquer |
|---|---|---|---|---|
| Window | #0F1424 | #1A1526 plum night | #EEF1F6 snow | #16100F black lacquer |
| Sunken | #0B0F1C | #120E1B | #E1E6EE | #0E0A09 |
| Raised (card) | #1E2437 | #262036 | #F9FAFC (cards lift lighter) | #231917 |
| Hover | #262D45 | #30283F | #DCE3ED | #2D211E |
| Line | #2A3149 | #352D46 | #CAD2DF | #342620 |
| StrongLine | #5C6584 (3.19) | #6F6486 (3.26) | #7A859C (3.28) | #705C52 (3.00) |
| Text | #DDE3F0 (14.2) | #F2E8E6 warm pearl (14.8) | #1A2136 navy ink (14.1) | #F3E9DB washi (15.7) |
| Text secondary | #A9B2CC (8.7) | #C4B4C0 (9.0) | #485270 (6.8) | #C6B6A2 (9.5) |
| Text tertiary | #7C86A8 (5.1) | #9A8BA2 (5.6) | #5F6984 (4.8) | #998775 (5.5) |
| Deep (rail) | #080B16 | #0F0B17 | #D9DFE9 | #0B0807 |
| Top (sky) | #151C33 | #2B1F3A, with the zenith toward a rose horizon | #F8FAFD, zenith #D6E1F2 | #2A1613 vermilion-tinted dusk |
| Ornament | Kit ink (brass #A88B52, 5.7) | Rose-gold #B98C6E (6.0) | Lead #7C8498 (3.3) | Leaf gold #B8913F (6.4) |
| Ornament high | #D9BE82 (10.2) | #E9C4A4 (10.9) | #59627A (5.4) | #E7C87C (11.6) |
| Cool (links, unlocks) | #6F8FD0 Tide (5.7) | #92A2E4 periwinkle (7.2) | #2C569E Ishgard blue (6.3) | #7FA3DA ai-zome indigo (7.3) |
| Accent (Moon as text) | #F2D27A (12.5) | #F5C47C dawn gold (11.1) | #7E5A0E deep gold (5.5) | #F0CC72 (12.2) |
| Locked out text | #D68AA8 (7.0) | #E68FB4 (7.6) | #9B2C6E (6.2) | #E58AC0 (7.8) |
| Not checked text | #8A93B0 (6.0) | #9C8FA8 (5.9) | #5B6480 (5.2) | #9A8C80 (5.8) |
| Accent vs Locked out, worst CVD ΔE (OKLab) | 0.160 | 0.103 | 0.088 (tightest; tritan) | 0.128 |

Notes:
- **Ishgard Snow is the first light palette.** The existing light paths already handle it:
  - `SurfaceColors.Light`;
  - `FlairTones`' light branch, given designed hexes (Quiet: rail #E3E8F0, tree #E8ECF2, table #EEF1F6, detail #F3F5F9, card #FAFBFD, rule #D3DAE5);
  - `GlyphPalette.Resolve`'s light high-contrast variant.
  - New for Snow: the star field is off on light palettes. The pane sky is snow-light, and the banner grade becomes a "daylight" grade (lighter and cooler, lower lift).
  - The rime-glint alternative to stars needs supervisor approval, because of the owner's no-noise rule.
- **Kugane Lacquer's vermilion** lives only in surfaces (the dusk sky tint, the rail's lacquer edge), never in ink. Red already means "Locked out" and "destructive".
  - Its cool accent is indigo, not teal. A first teal proposal (#6FB3A8) collapsed into the Locked-out pink for deuteranopes (ΔE 0.035), which is exactly what the CVD test exists to catch.
- **Dawn** is dark. It is the hour before sunrise, not a light theme, so it shares every dark-path rule with Night.
- **Follow Dalamud** stays as a palette choice (today's toggle), built per frame from the host style.

### 8.3 High-contrast variants

- **Night HC and Snow HC** are designed: today's `GlyphPalette.HighContrastDark` and `HighContrastLight`, plus `SurfaceColors.ForHighContrast()`.
- **Dawn HC and Kugane HC** are derived by the same transform (opaque strong-line ornament, Cool pushed to 7 : 1, no sky gradient), plus `EnsureContrast` on Accent and the state inks to 7 : 1.
- Each variant has its own row in `PaletteContrastTests`.
- The HC glyph set is the shared ladder. Decoration caps at Quiet (unchanged).

### 8.4 Migration cost of the fixed tokens

- About 230 references to `Theme.Moon`, `Silver`, `Dusk`, `Eclipse` and `Veil` (including the U32 forms) become per-frame properties set in `Refresh`.
- Static initialisers that bake colours (for example `LegacyMoonGlyph.GlowColors`) stay on the fixed `GlyphTokens`, because they are glyph art.
- Rule of thumb for the audit:
  - **Text and chrome go to the palette.**
  - **Glyph and picture pixels stay as fixed tokens.**
  - An analyser-style test (as `ImGuiLintTests` does for other rules) bans new `Theme.Moon` uses outside glyph code.

---

## 9. Settings UX: a Themes page

**Placement:**
- A new Settings section, **Themes**, after General.
- Moon style and Moon colours move there from General › Look, with their search keywords kept, so `SettingsSearch` still finds "moon", "glyph", "high contrast" and "colour blind".
- General › Look keeps Decoration, heading fonts and motion.

```
┌ Themes ───────────────────────────────────────────────────────────────────────────┐
│  Theme                                                                            │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐                               │
│  │ ●●●●●●●●     │ │ ●●●●●●●●     │ │ ●●●●●●●●     │   cards: 8 framed faces at    │
│  │ Menphina's   │ │ Aether       │ │ Ishgard      │   28 px on the theme's own    │
│  │ Medallion    │ │ Crystal      │ │ Glass        │   pane, a mock row, 4 palette │
│  │ ▬▬▬▬ Night   │ │ ▬▬▬▬ Night   │ │ ▬▬▬▬ Snow    │   swatches; selected = gilt   │
│  └──────────────┘ └──────────────┘ └──────────────┘   edge, never a tick glyph    │
│  (Orrery, Sumi, Classic …; Classic labelled "Legacy")                             │
│                                                                                   │
│  Live preview  [ fixed-size panel: 8 sample quest rows at the user's real row     │
│                  size + one hero medal + a card, drawn under PushAppearance ]     │
│                                                                                   │
│  Palette   [Night] [Dawn] [Ishgard Snow] [Kugane Lacquer] [Follow Dalamud]        │
│            ☐ High contrast (uses one set of moons made for low vision)            │
│  Frames    [From theme ▾]                                                         │
│                                                                                   │
│  Mix moons by state                         (reserved one-line status, no reflow) │
│   State                 Glyph             From                                    │
│   Ready                 (medal)  [From theme ▾]                                   │
│   Ready on another job  (medal)  [Aether Crystal ▾]   · close to Blocked          │
│   …                                                                               │
│   [Fix it]  [Reset mix]                                                           │
│                                                                                   │
│  Share     [Copy code]   [ paste a code……… ] [Apply]                               │
│  Reset     [Reset appearance to default]   (hold Ctrl; ConfirmGate)               │
└───────────────────────────────────────────────────────────────────────────────────┘
```

Behaviour, following the owner's taste:
- **Static layout.**
  - Card grid cells and the preview panel have fixed sizes.
  - The warning status line is reserved even when empty, so nothing jumps when a warning appears.
  - The grid wraps by width only.
- **Hover to preview, click to apply.**
  - Hovering a card swaps the preview panel to that theme ("Previewing: Ishgard Glass", with the current theme beside it in a small strip).
  - Clicking applies it at once, with an Undo toast.
  - A crossfade of about 120 ms; none under Reduce motion.
- **Mix table.**
  - One row per state: name, current medal at 32 px, and a combo listing each mixable set with its face thumbnail.
  - Options that would create a "hard to tell apart" pair carry a quiet amber pip and a tooltip.
  - Words, not warning glyphs; no ⚠.
- **Warnings** are one calm line ("2 moons are hard to tell apart in a list") with a disclosure for details and "Fix it".
- **Reset:**
  - "Reset mix" and per-row "From theme" are reversible through Undo, so they need no confirmation.
  - "Reset appearance to default" discards a custom mix, so it goes through `ConfirmGate` (hold the modifier), per the owner's safety rule. The share code is shown in its tooltip so the mix can be recovered.
- **Spacious:** section headings, 1.5× row spacing, no paragraph walls. Hints sit in tooltips.
- **Glyph debug window:** a new Themes tab with A/B of any two appearances, the CVD toggles (existing) and the mixed-set matrix as a heatmap for the reviewers.

---

## 10. Effort and phasing

Effort scale as in feature-plan-v6: S under a day, M one to three days, L about a week or more including the art loop.

### Release 1.14: "Themes, part 1" (the infrastructure plus 2 revived themes)

| Id | Item | Effort |
|---|---|---|
| T1 | The appearance model in Core: `GlyphSetId`, presets, `UiPalette`, `FrameKit`, `AppearanceConfig` with migration from Moon style, Moon colours and Follow Dalamud (old fields written for one release), and the pure `AppearanceResolver`, with tests | M |
| T2 | Palette plumbing: `Theme.Refresh` takes the resolved palette. The fixed-token audit (about 230 references: text and chrome to the palette, glyph art stays). `FlairTones` designed hexes per palette. `Ornament` brass, zenith, stars and `BannerGrade` targets from the palette and kit. A lint test against new `Theme.Moon` uses. | L |
| T3 | The renderer seam: the `IGlyphSet` and kit compositor behind `MoonGlyph` (no call-site changes); Medallion and Classic ported as sets; gauges from the kit; procedural stand-ins while atlases load; `PushAppearance` scope | M |
| T4 | The multi-theme build `tools/themes/build_themes.py`: manifests, Chrome render at tiers and whole-pixel strips, layered packing, previews, per-set gates, the cross-set matrix, contact sheets, `--check`. Medallion's atlas regenerated through it, pixel-identical. | M |
| T5 | The atlas runtime: `ThemeAtlasCache` (layout JSON, lazy per set, row strip by device px, 2x on demand, release on drop), content-file packaging, and the layout and budget tests | M |
| T6 | Art: **Ishgard Glass** revived (§4.2), through designer, supervisor, critic, gates and owner. Includes the Came kit (frames, badges, ornaments). | L |
| T7 | Art: **Aether Crystal** revived (§4.1), with the Silver kit | L |
| T8 | Palettes: Night (ported) and **Ishgard Snow** (the first light palette: designed Quiet and Plain hexes, daylight banner grade, no stars), plus HC for both, and `PaletteContrastTests` over palettes including the CVD check | M |
| T9 | Settings › Themes: the card grid, palette picker, frames choice, live preview, reset with safety, and Moon style and Moon colours moved there with their search keywords | M |

### Release 1.15: "Mix and match"

| Id | Item | Effort |
|---|---|---|
| T10 | The per-state mix table, matrix lookups, distinctness and salience warnings, option pips and "Fix it", with `MixCheckTests` | M |
| T11 | The frames axis as a user choice: Brass, Silver and Came for medals, badges, gauges and Decoration ornament (cards, rules, tooltips, corner marks, sigil) | M |
| T12 | Share codes: copy, paste, preview the change, Undo, with `ShareCodeTests` | S |
| T13 | Glyph window Themes tab: A/B of two appearances and the matrix heatmap | S |

### Release 1.16: "Themes, part 2"

| Id | Item | Effort |
|---|---|---|
| T14 | Art: **Astrologian's Orrery** revived (§4.3), with the Astrolabe kit | L |
| T15 | Art: **Sumi to Kinpaku** revived (§4.4), with the Kirikane kit | L |
| T16 | Palettes **Dawn** and **Kugane Lacquer**, and their HC variants | M |

**Totals:** 5 L, 9 M and 2 S. The four art rows dominate: each is a full design round, and they can run in parallel with the code rows.

**Critical path for 1.14:**
1. T1, then T3 and T5 (the seam and atlases).
2. T4 (the tool) unblocks T6 and T7 production. Their concept work can start on day one.
3. T2 runs in parallel and is the largest code risk because of the token audit.

---

## 11. Risks

| Risk | Mitigation |
|---|---|
| A revived set still loses to Medallion on beauty (round 2 scores 5.8–6.7) | Each set must pass the round 5 bar and the owner's own review on the plan site before production. A set that doesn't is cut, not shipped weak. |
| Light palette regressions (text drawn with Night-only tokens) | The T2 audit plus the lint test, and an in-game check of every pane on Ishgard Snow at all three Decoration levels |
| Mixes that pass the numbers but look incoherent | One frame kit per mix gives one border colour, and the shared grammar gives one meaning per mark. The previews make incoherence visible before applying. |
| Atlas memory creep | The budget test (≤ 12 MB at 1x worst case), lazy loading and release on drop |
| Chrome render drift between builds | Layout is tested, not pixels. `--check` diffs renders before a release. |
| The 1.5× shrink rule at 32–47 px | Tiers start at 48. 32 px shrinks exactly 1.5×, as today. |

## 12. Decisions for the owner

1. **The first two revived themes:** Ishgard Glass and Aether Crystal (the recommendation), or another pair?
2. **Classic:** keep it as a legacy whole theme, or retire it now that themes replace the A/B purpose it was kept for?
3. **High contrast:** it always uses one shared low-vision set and ignores the mix (recommended), or should each theme get its own HC art?
4. **Plain decoration:** each theme's own flat moons (recommended), or one shared flat set for all?
5. **Palette names:** Night, Dawn, Ishgard Snow and Kugane Lacquer. Any to add (for example a Crystarium or Sharlayan palette) or rename?
6. **The plugin icon** stays Menphina's Medallion for everyone. Dalamud reads one icon per plugin.
