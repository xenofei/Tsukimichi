# 1.16 "Themes": palettes and the Settings › Themes page

This spec answers the owner's point 7: "Users can choose themes, or also mix-and-match icons as well (and even ui/ux color pallettets)."

It covers two rows, against `docs/research/plan-v7/theme-system.md` §3, §5.1–5.2, §8 and §9:
- **T8**, the palettes;
- **T9**, the Themes page.

1.16 ships four themes (Menphina's Medallion, Ishgard Glass, Aether Crystal and Classic) and two palettes (Night and Ishgard Snow), each with a high-contrast form, plus Follow Dalamud. 1.17 adds Orrery, Sumi, Dawn, Kugane Lacquer, per-state mixing and share codes; §B6 shows that it fits this page without a redesign.

**Revision 2** applies the supervisor's review (two changes) and records the rulings and the owner's decisions:
1. **Gauges on light palettes** get their own ink, so they read as UI graphics (§A6, §A8).
2. **The banner's location line** fits one line by the label ladder and takes the title's shadow and bloom (§A5).
3. **Rulings:**
   - no stars on light palettes (§A5);
   - Ready on light palettes and its salience gate (§A4.1);
   - the colour-vision gates (§A8.1);
   - no "Try a light theme" hint (§B2);
   - Sumi's card hidden until its art is approved (§B6).

**Files**

| File | What it is |
|---|---|
| `spec-1.16.md` | This spec |
| `1.16/palettes.py` | Every palette role as a hex, the high-contrast transform, the portrait grade per palette and the WCAG table. It writes `palettes.json` and `contrast.md`, both used by this spec and the mock. |
| `mock.html` | `#snow-full`, `#snow-full-drawer`, `#snow-quiet`, `#snow-plain`, `#palettes` and `#themes`. Sources: `mock-src/v716.js` and `v716.css`, plus palette and theme hooks in `v7.js` and `v715.js`. |
| `snow-full.png` | The Journal window in Ishgard Snow at Decoration Full, with the Ishgard Glass theme: dawn sky, rail, tree, table, and the detail pane with Tataru's portrait |
| `snow-full-drawer.png` | The same window with the filter drawer open |
| `snow-quiet.png`, `snow-plain.png` | Ishgard Snow at Quiet (drawer open) and Plain (drawer open with Advanced) |
| `palettes.png` | Night, Night HC, Snow and Snow HC; portraits on dark and light; the contrast table |
| `themes-page.png` | Settings › Themes as 1.16 ships it (Ishgard Glass hovered and previewed), beside the same page in 1.17 |

The previews use the approved theme art: `docs/design/v7/themes/ishgard-glass/` and `aether-crystal/` (row and hero tiers), and Menphina's Medallion from `moon-v6/round5/medallion-r5/`.

---

## A. Palettes (T8)

### A1. What a palette moves, and what it never moves

A palette owns the **chrome**: surfaces, text inks, state words, stripes, ornament ink, sky, shadows, washes and the portrait plate. It **never recolours a glyph**.
- Medals keep their own enamel wells and keylines, so on Snow they read as jewels on paper (`snow-*.png`).
- **Moon gold** keeps its meaning of "act now" in every palette. A palette may only shift its lightness so it reads as text (Snow's Accent is a deep gold, `#755308`); gold fills such as the primary pill, the bead and the badge stay gold.

The record is `UiPalette` (research §8.1), plus two fields introduced here:
- `ShadowInk`: the colour shadows are drawn in;
- `WashInsteadOfGlow`: the light-palette rule in §A4.

### A2. The roles

**Night** is today's palette. The 1.16 audit found two tokens under the bar, and bumps them:
- **TextTertiary** `#7C86A8` → `#8B94B3`. It was 3.8:1 on Hover and 4.3:1 on Raised, and is now 4.5:1 on all four surfaces.
- **StrongLine** `#5C6584` → `#646D8A`. It was 2.7:1 on Raised and is now 3.0:1.

| Role | Night | Ishgard Snow | Notes for Snow |
|---|---|---|---|
| Window | #0F1424 | **#EEF1F6** snow | |
| Sunken | #0B0F1C | #E1E6EE | |
| Raised (card) | #1E2437 | #F9FAFC | Cards lift *lighter* than the window: snow under light |
| Hover | #262D45 | #DCE3ED | |
| Line | #2A3149 | #CAD2DF | |
| StrongLine | #646D8A | #7A859C | 3.3:1 |
| Text | #DDE3F0 | **#1A2136** navy ink | |
| TextSecondary | #A9B2CC | #434D6A | Research had #485270. It is darkened so it holds 4.5 on Hover. |
| TextTertiary | #8B94B3 | #56607C | Research #5F6984 was 4.1 on Hover |
| Deep (rail) | #080B16 | #D9DFE9 | |
| Top / zenith | #151C33 / #1B2552 | #F8FAFD / **#D3DEF0** | The dawn sky (§A5) |
| Ornament (kit metal) | Brass #A88B52 | **Lead (Came)** #7C8498 | It follows the frame kit (§A6) |
| OrnamentHigh | #D9BE82 | #59627A | |
| OrnamentLight (Section headings, sorted column) | #E6CF98 | **#3F4862** with the Came kit, **#6E5320** with Brass | 8.0:1 and 6.4:1 |
| Cool (links, unlocks) | #6F8FD0 | #2C569E Ishgard blue | |
| Accent (Moon as text) | #F2D27A | **#755308** deep gold | |
| ShadowInk | #000000 | **#1A2136** | Cool shadows on snow (§A4) |

**State inks** (the status words) and **stripes**:

| State | Night word | Snow word | Snow stripe |
|---|---|---|---|
| Ready, In journal | #F2D27A | #755308 | #A07B25, solid |
| Completed | #F2D27A | #6B5420 | #C8B07A (faded gold, like Night's .55) |
| Ready on another job | #DDE3F0 | #1A2136 | #59627A |
| Done this cycle | #DDE3F0 | #2A3454 | #59627A at .55 |
| Blocked | #A9B2CC | #434D6A | #8A93AA |
| Locked out | #D68AA8 | **#962A6A** | #962A6A, dashed |
| Not checked | #8A93B0 | #56607C | #8A93AA, dotted |

The dash and dot patterns are unchanged: they are the accessibility carrier, not colour.

**The CVD check** from research §8.2 (Accent against Locked out, worst-case ΔE in OKLab) still applies. On Snow the pair is a deep gold against a plum. That is a large lightness and hue gap; the plugin's `PaletteContrastTests` CVD row must hold ≥ 0.08.

### A3. High-contrast forms

High contrast applies the same transform to both palettes (research §8.3). Decoration caps at Quiet, as today.
- **No sky:** Top and the zenith equal the Window.
- **Ornament** is drawn in StrongLine, opaque.
- **Strong lines:** Night `#7C86A8`, Snow `#4A5470`.
- **Lines:** Snow Line `#9AA4B8`.
- **Text:** Snow Text `#0B1020`, Secondary `#2A3350`, Tertiary `#3A4462`. Night Secondary `#C3CBDF`, Tertiary `#A0A9C4`.
- **Accent, Cool and every state ink** are pushed toward Text until they reach **7:1 on the Window**.
- **Stripes** reach at least 3:1.
- **Moons:** the glyphs are the shared high-contrast ladder (`MedalTokens.HighContrastDark` / `HighContrastLight`, chosen by the window's luminance), whatever the theme.

### A4. Light, shadow and glow on a light palette

The light model is v13's: one moon, upper left. On snow a few things change.
- **Shadows** are cool navy, never black, and lighter:

| Surface | Shadow |
|---|---|
| Cards | 0 2 8 `#1A2136` at .10, plus 0 1 2 at .08 |
| Tooltips | 0 8 22 at .16 |
| Drawer | 0 14 30 at .16, plus the unoffset contact shadow 0 0 14 at .10 on the list side |
| Medals in rows | 0 1 1 at .30 |

- **Top highlights:** every raised surface keeps a 1 px **white** inner top highlight. It is the lit edge, as `MoonHigh` .06 is on Night.
- **Glow becomes a warm wash** (`WashInsteadOfGlow`). Additive light on a light page is invisible or muddy, so:
  - a Ready row's medal halo is a `#F2D27A` wash at .75 within 3 px (the same footprint as Night's glow);
  - the hero halo is a `#E9C46A` wash at .30 → 0;
  - the selected row is a gold wash (`#F2D27A` .30 → .05) with `#AC8324` .45 hairlines;
  - nothing else glows on Snow.
- **The Ready road glint** is off on light palettes. A 1 px lead-gold road `#AC8324` .55 → 0 stays.

#### A4.1 Ready on a light palette (the supervisor's ruling)

**Medals are never recoloured**, on any palette. On light palettes, Ready is carried by four things:
- the **gilt act-now ring** (the kit's act-now frame, gilt in every kit);
- the **warm wash**: Snow's Ready-only wash, `#F2D27A` at .75 within 3 px of the row medal;
- the **gold status word** (`#755308`);
- the **gold stripe** (`#A07B25`).

**Salience** is measured as the OKLab ΔE, chroma included, between the 16 px row medal (with its wash) and the window. The salience test in `MedalTests` gains a light-palette row.

**Gate:** Ready ≥ **1.3×** the next state.

**If the gate fails:** raise the wash to **.90 within 4 px**. The medal is never recoloured to pass.

**The measure, revised (the supervisor's final ruling).** This replaces the gate and the fallback above. Full OKLab ΔE turned out to be mostly lightness: on a light page every dark-faced state outweighs Ready's light face, so no set reached 1.3 under it with either wash. The gate is now G2L in `tools/themes/build_themes.py`, asserted again in `ThemeAtlasTests` with the bars written in C#.

What it measures: the row tier at **16 and 20 px** on Ishgard Snow (`#EEF1F6`), every set. Each state's per-pixel difference from the window is summed over the cell. Each state is drawn on black and white mattes to recover its colour and alpha, and Ready is composited over its wash.

The wash ships at **#F2D27A .75 within 3 px for every set**. The .90 / 4 px wash is no longer a fallback; the build records it for reference only.

The gates on a light palette:
1. **Lead:** Ready ≥ **1.3×** the next state by lightness-down-weighted OKLab ΔE, √((ΔL/3)² + Δa² + Δb²). If a set falls under 1.3 on that measure at either size, it must reach **1.3×** by chroma-only ΔE, √(Δa² + Δb²). `metrics.json` records which measure passed.
2. **Lightness floor:** Ready's plain luminance salience (round 5's greyscale salience), with the wash, ≥ **0.70×** the next state's. **Not checked is left out of "next state" here**, because its dark face outweighs Ready's light one in plain luminance. Gate 3 holds it instead.
3. **Not checked under Ready:** Not checked's chroma-only salience is below Ready's, at 16 and 20 px.
4. **Colour vision and greyscale (§A8.1):** the weakest pair is ≥ **11** under protanopia, deuteranopia and tritanopia, and ≥ **12** in greyscale. Not checked counts in these, as in every distinctness gate.
5. **Redundancy:** on light palettes, Ready always keeps the gilt act-now ring, the gold stripe and the "Ready" word. Gates 1–3 measure only the medal and its wash, so these three are never dropped on a light palette.

### A5. The sky on a light palette: a dawn over snow

**Decision:** on Ishgard Snow, Full's night sky becomes **a still dawn over the Coerthas snowfield**.

| Part | Value |
|---|---|
| Pane gradient | zenith `#D3DEF0` → `#DCE5F3` (14 %) → `#E7ECF4` (30 %) → a faint rose horizon `#EFEAEC` (42 %) → snow `#EEF1F6` (56 %) → `#F1F3F7` → `#F4F6F9` at the foot. Snow is brighter low down, because it reflects the sky. |
| **Star field** | **Off.** Dark stars on a pale sky read as dust, and pale stars vanish (white on the zenith is 1.36:1). |
| **Stars** | **None on light palettes** (the supervisor's ruling). Snow is a still dawn with no stars at all; stars are a dark-palette feature. The morning star of the first draft is removed. |
| Moving sky, twinkle, completion meteor, Milky Way | **Off** on light palettes. The Motion settings show them disabled, with "Night palettes only". |
| Constellations | Off |
| Banners (Full) | The **daylight grade**: no night multiply, 20 % desaturation, brightness 1.04, and a scrim to Snow (`#EEF1F6`: 0 at the top, .22 at 40 %, .92 at the foot). The title is **navy ink** with a white 1 px shadow and a 10 px white bloom. **The location line takes the same shadow and bloom**, and fits **one line by the label ladder**: the full path ("Main Scenario (Heavensward) › Heavensward · Lv 54"), then without its "Main Scenario (…) ›" prefix ("Heavensward · Lv 54"), then the level alone. It never ends in an ellipsis and never wraps, so no word is left alone over bright art. The keyline is 1 px lead. |

### A6. Ornament and frames on a light palette

Frames follow the theme. Ishgard Glass brings the **Came** kit (lead), the kit the research designed to read on light.

- **Card frame (Full):**
  - **Edge:** a 1 px lead ramp lit from the upper left: `#B8C0D0` → `#7C8498` → `#5A6278` → `#7C8498` → `#4A5268`.
  - **Corner marks:** `#59627A` on top, `#4A5268` on the bottom.
  - **Section headings:** OrnamentLight `#3F4862` with a white 1 px shadow.
- **Rules and dividers:** lead `#59627A` fading out; the divider lozenge white → `#9AA2B6` → `#59627A`.
- **Act now stays gilt in every kit:**
  - **Gold:** the primary pill (`#F8DE96` → `#EBC66C` → `#D6AE52`, edge `#A88437`, ink `#2A1E05`, 10:1), the bead, the Ready badge, toggles when on (`#EDD48C` track, a `#755308` crescent) and the badge count.
  - **Lead, not gold:** everything else.
- **Brass on Snow** (a user who keeps Brass frames): the brass ramp is unchanged, because brass reads on light. Headings use the deep gilt `#6E5320` (6.4:1).
- **Quiet medals:** the Quiet hairline rim is `#7A859C` at .8 on light. Night's `#C3CBDF` would vanish.
- **Gauges** (the tree's orbit, the rail's Journal station and foot gauge, Quiet's ring) get **their own ink on light palettes**. Night's pale cream arc measured about 1.5:1 on snow, and a gauge is a UI graphic that needs 3:1.
  - **The arc:** a gilt ramp from **`#8A6A1C`** (highlight, upper left) to **`#755308`** (shade, lower right).
  - **The groove:** **`#CAD2DF`** between two 0.5-unit **`#7A859C`** keylines (3.3:1 on the window), so the gauge has an edge whatever is behind it.
  - **The knob:** `#8A6A1C` with a `#F9FAFC` rim.
  - **The filling moon:** moonstone `#C3CEE4` over a **`#59627A`** dark side.
  - Measured in §A8. `snow-full.png` and `snow-quiet.png` show the gauges in place.
- **Plain on Snow:**
  - band `#E3E8F0`, lines `#D3DAE5`, zebra `#1A2136` at .03;
  - the Plain medal ladder is unchanged (glyph art);
  - set values in the drawer carry a gold `*` in `#AC8324`.
- **Quiet on Snow:** the research's designed tones: rail `#E3E8F0`, tree `#E8ECF2`, table `#EEF1F6`, detail `#F3F5F9`, cards `#FAFBFD` with a `#E1E6EE` ring, rule `#D3DAE5`.

### A7. Portraits on a light palette

This follows the supervisor's ruling (1.15, Q5): **skip the night multiply; keep the desaturation and the black lift.**

| Family | Matrix (rows R, G, B: r g b + offset) |
|---|---|
| Colour (Duty Support, Triple Triad, delivery) | `.7791 .1734 .0175 +.0147` / `.0516 .9009 .0175 +.0196` / `.0516 .1734 .7450 +.0353` |
| Battle dialogue | `.5881 .3469 .0350 +.0147` / `.1031 .8319 .0350 +.0196` / `.1031 .3469 .5200 +.0353` |

These come from d = .25 / .50, no multiply, scale .97, and a lift of Night × .25. The lift keeps a portrait's darkest values at the navy of Snow's ink, never pure black. That ties the face to the page.

**The plate on Snow:**
- well `#DCE3EE` → `#C8D1E0`;
- keyline a lead ramp `#B8C0D0` → `#7C8498` → `#5A6278` → `#4A5268`, with a white 1 px outer line at .9;
- lip shadow `#1A2136` at .22.

**Fallbacks:** silhouettes and initials are drawn in `#56607C`, and society emblems keep their own tile.

The comparison is in `palettes.png`.

### A8. Contrast

All values are WCAG 2.x, computed by `1.16/palettes.py`. The **worst** surface is the one listed.
- Text needs 4.5:1.
- Large text (the Section role at 1.80×) and UI lines, stripes and ornament points need 3:1.
- The high-contrast forms reach 7:1 for every ink on the Window.

| Ink | On | Kind | Night | Night HC | Snow | Snow HC |
|---|---|---|---|---|---|---|
| Text | Window, Raised, Sunken, Hover (worst) | text, 4.5:1 | #DDE3F0 10.6 | #DDE3F0 10.6 | #1A2136 12.4 | #0B1020 14.7 |
| TextSecondary | Window, Raised, Sunken, Hover (worst) | text, 4.5:1 | #A9B2CC 6.4 | #C3CBDF 8.4 | #434D6A 6.5 | #2A3350 9.6 |
| TextTertiary | Window, Raised, Sunken, Hover (worst) | text, 4.5:1 | #8B94B3 4.5 | #A0A9C4 5.8 | #56607C 4.8 | #3A4462 7.4 |
| Accent | Window, Raised, Sunken (worst) | text, 4.5:1 | #F2D27A 10.5 | #F2D27A 10.5 | #755308 5.6 | #694C0B 6.3 |
| Cool | Window, Raised (worst) | text, 4.5:1 | #6F8FD0 4.8 | #86A1D7 5.9 | #2C569E 6.3 | #294F91 7.1 |
| OrnamentLight | Window, Raised (worst) | large text (Section heading), 3:1 | #E6CF98 10.1 | #E6CF98 10.1 | #3F4862 8.0 | #3F4862 8.0 |
| Ready | Window, Raised, Hover (worst) | text, 4.5:1 | #F2D27A 9.2 | #F2D27A 9.2 | #755308 5.4 | #694C0B 6.2 |
| Completed | Window, Raised (worst) | text, 4.5:1 | #F2D27A 10.5 | #F2D27A 10.5 | #6B5420 6.4 | #624E20 7.1 |
| ReadyOnOtherJob | Window, Raised (worst) | text, 4.5:1 | #DDE3F0 12.0 | #DDE3F0 12.0 | #1A2136 14.1 | #1A2136 14.1 |
| DoneThisCycle | Window, Raised (worst) | text, 4.5:1 | #DDE3F0 12.0 | #DDE3F0 12.0 | #2A3454 10.8 | #2A3454 10.8 |
| Blocked | Window, Raised (worst) | text, 4.5:1 | #A9B2CC 7.3 | #A9B2CC 7.3 | #434D6A 7.4 | #434D6A 7.4 |
| LockedOut | Window, Raised, Hover (worst) | text, 4.5:1 | #D68AA8 5.2 | #D68AA8 5.2 | #962A6A 5.7 | #8E2866 6.1 |
| NotChecked | Window, Raised (worst) | text, 4.5:1 | #8A93B0 5.0 | #97A0BA 5.9 | #56607C 5.5 | #47506A 7.1 |
| StrongLine | Window, Raised (worst) | UI line, 3:1 | #646D8A 3.0 | #7C86A8 4.3 | #7A859C 3.3 | #4A5470 6.6 |
| StripeGold | Window, Raised, Hover (worst) | stripe, 3:1 | #F2D27A 9.2 | #F2D27A 9.2 | #A07B25 3.0 | #A07B25 3.0 |
| StripeLocked | Window (worst) | stripe, 3:1 | #B25C7F 4.1 | #B25C7F 4.1 | #962A6A 6.5 | #962A6A 6.5 |
| OrnamentHigh | Window (worst) | ornament point, 3:1 | #D9BE82 10.2 | #D9BE82 10.2 | #59627A 5.4 | #59627A 5.4 |
| GaugeArc (highlight end) | Window, Groove, Zenith (worst) | gauge arc, UI graphic 3:1 | #F2D27A 9.3 | #F2D27A 9.3 | #8A6A1C 3.3 | #8A6A1C 3.3 |
| GaugeArcShade (shade end) | Window, Groove, Zenith (worst) | gauge arc, UI graphic 3:1 | #D6B25A 6.8 | #D6B25A 6.8 | #755308 4.6 | #755308 4.6 |

**One measured shortfall: the gauge arc's highlight end.** It is measured honestly against everything it can sit on:

| `#A07B25` against | Contrast |
|---|---|
| Window `#EEF1F6` | 3.5:1 |
| Zenith `#D3DEF0` (the first tree rows at Full) | 2.9:1 |
| Groove `#CAD2DF` (the unfilled part) | **2.6:1** |

The shade end `#755308` passes everywhere (at least 4.6:1). The arc's boundary against the pane is carried by the `#7A859C` keylines (3.3:1), so the gauge's shape always reads. The fill-against-groove pair on the highlight quarter does not reach 3:1.

**Ruled by the supervisor:** the highlight stop is **`#8A6A1C`** (the ramp's midpoint). That gives 3.3:1 on the groove, 3.7:1 on the zenith and 4.5:1 on the window, keeps the shade at `#755308` and keeps the ramp's direction. The mock and the contrast table use it; `#A07B25` stays only as the gold row stripe, where it meets 3:1.

**Other pairs checked:**

| Pair | Contrast |
|---|---|
| Gold pill ink on the Snow pill | 10.0:1 |
| Quiet's flat gold pill | 11.1:1 |
| Plain's gold primary text `#755308` on Raised | 6.7:1 |
| Brass headings `#6E5320` on Snow | 6.4:1 |
| Night HC's state inks on the Window | 7:1 by construction; at least 5.2 on Hover |

`PaletteContrastTests` gains one row per palette and form (four in 1.16), asserting this table.

#### A8.1 Colour-vision gates (the supervisor's ruling)

The glyph gates run against each palette's window, at 16 px:
- the weakest pair under **protanopia, deuteranopia and tritanopia** must be **≥ 11**;
- the weakest pair in **greyscale** stays **≥ 12**, the round-5 ship bar.

The pair metric is the same one `docs/design/moon-v6/round5/metrics.py` uses: the summed luminance difference after a 0.6 px blur, with CVD simulation through Viénot 1999 for protan and deutan, and Brettel for tritan. It runs on Night and Snow, for every theme's row strip, in `tools/themes/build_themes.py` (the matrix of research §6.4), and is asserted in C# from `metrics.json`.

### A9. Code map

| Piece | Where |
|---|---|
| `UiPalette` and `PaletteId` (`Night`, `IshgardSnow`, `Dalamud`), with `ShadowInk`, `WashInsteadOfGlow` and `SceneTokens` (sky stops, `StarField` on or off, `BannerGrade` daylight or night) | Tsukimichi.Core/Ui/Themes |
| `UiPalette.ForHighContrast()` (the §A3 transform) | Core, tested |
| `Theme.Refresh` reads `AppearanceResolver.Resolve(...)` once a frame; about 230 `Theme.Moon`, `Silver`, … references become palette properties (research §8.4) | Tsukimichi/Ui/Theme.cs, plus the audit |
| `FlairTones` light branch fed the designed Quiet and Plain hexes | Core |
| `BannerGrade.Daylight` (no multiply, desaturate .2, brightness 1.04, scrim to the window) | `BannerGrading` |
| `PortraitGrade.For(palette, family)` (the §A7 matrices) | Core; `PortraitGrading` keys its cache by palette as well |
| Star field, moving sky, meteor, Milky Way and constellations gated by `Scene.StarField` (false on every light palette; nothing is drawn in their place) | `TabStrip`, `TreePane.Art`, `TablePane`, `PathChart` |
| Gauge inks per palette (`GaugeInk`: arc highlight and shade, groove, keyline, moon dark side) | `MedalGauge`, `Orbit` |
| The label ladder for the banner's location line | `DetailPane.Hero` (the existing `LabelLadder`) |
| Lint test: no new `Theme.Moon` as text outside glyph code | `ImGuiLintTests` |

---

## B. Settings › Themes (T9)

### B1. Placement

- **Order:** a new Settings section, **Themes**, sits second, after General.
- **What moves here:** Moon style and Moon colours move from General › Look, keeping their search keywords, so `SettingsSearch` still finds "moon", "glyph", "high contrast", "colour blind", "palette", "light" and "theme".
- **What stays in General › Look:** Decoration, heading fonts and motion.

### B2. Layout (`themes-page.png`, left)

The page is one column, 620–660 px wide, in the Settings window's content area. Every section has a fixed height, so nothing reflows.

| Section | Contents |
|---|---|
| Heading | "Themes" in the Title role; one line: "Moons, frames and colours. Hover a theme to preview it; click to use it." |
| **Theme** | A **card grid**: 206 × 196 px cells, 3 per row, gap 14. It wraps by width only. |
| **Preview** | A fixed **660 × 268** panel: six quest rows at the user's real row height (one of each kind of state), and a 64 px hero medal with its heading. It is drawn under the hovered (or current) appearance with `PushAppearance`. Its header reads "Previewing: Ishgard Glass · In use: Menphina's Medallion" while hovering, else "Preview · {theme}". |
| **Colours** | **Palette:** swatch tiles (76 × 48 mini windows: sky, two bars, a gold dot) for Night, Ishgard Snow and Follow Dalamud; the selected tile has the 2 px gilt edge. **High contrast:** a MoonToggle with "One set of moons made for low vision, on every theme. Your theme's moons return when it is off." **Frames:** a segmented control: From theme · Brass · Silver · Came. |
| **Reset appearance** | A quiet action (§B5) |

**Card anatomy:**
- **Theme pane:** drawn in the theme's own palette (a night sky for Night themes, the dawn gradient for Snow), holding **8 faces at 28 px** (the row tier, 4 × 2, in state order) and a mock row ("Firmament" with its 16 px Ready medal and the word Ready in that palette's Accent).
- **Under the pane:**
  - the name in the Title face;
  - "Came frames · Ishgard Snow" in TextTertiary;
  - a strip of 4 palette chips (window, card, text, accent);
  - on the theme in use, "In use" in Accent.
- **Classic** is labelled **Legacy** (whole-theme only; it can't be mixed in 1.17).
- **The selected card** has a 2 px gilt edge (the act-now gilt). Never a tick glyph.
- **The hovered card** has a 1 px `#7C86A8` edge. Nothing moves or lifts: the layout is static.

**No nudges.** The page never suggests a theme: there is no "Try a light theme" hint, on first run or later (the owner's decision).

### B3. Behaviour

| Action | Result | Motion |
|---|---|---|
| Hover a card | The Preview panel swaps to that theme. The page itself does not change. | A crossfade over `Swap` 0.12 s; instant under Reduce motion |
| Click a card | The theme applies at once, everywhere, followed by the Undo toast "Theme: Ishgard Glass · Undo" (8 s). | The edge moves over `Select` 0.15 s. Windows repaint on the next frame, with no animation of the whole UI. |
| Click a palette tile | It applies at once, with an Undo toast. | The same |
| High contrast | It applies at once, with an Undo toast. While it is on, the cards show the shared HC moons, with the line "High contrast uses one set of moons on every theme." | |
| Frames | It applies at once, with an Undo toast. | |

**Picking a theme changes its palette and frames too.** Ishgard Glass brings Snow and Came. A palette or frame picked by hand afterwards is kept as an override, until the user picks "From theme" or resets.

**Follow Dalamud** is a palette tile, not a separate toggle.

### B4. Spacing and type

| Element | Value |
|---|---|
| Section headings | The Section role (§1 of `spec.md`) with its rule |
| Gaps | 22 px between sections; setting rows at least 52 px, with label and caption on the left and the control on the right |
| Captions | 11.5 px, TextTertiary, one line. Longer hints go in tooltips. |
| Settings nav | Unchanged: it gains "Themes" |

### B5. Reset, with safety

- **1.16:** "Reset appearance" returns to Menphina's Medallion on Night, with Brass frames and high contrast off.
  - Everything it discards can be restored by **Undo**, so it is **one click**, followed by the Undo toast "Appearance reset · Undo" (8 s). This is `GuardedAction.ResetAppearance`, tier None, Undo on.
- **1.17:** while a per-state mix is set, reset discards it, so it becomes **Hold to reset** (`ConfirmGate`, the Hold tier).
  - Undo still follows.
  - The tooltip carries the mix's share code, so the mix can always be recovered.

### B6. How 1.17 fits without a redesign (`themes-page.png`, right)

Nothing above Frames moves. 1.17 only **adds** content to existing containers and **appends** two sections:

| 1.17 addition | Where it goes |
|---|---|
| Astrologian's Orrery | One more card. The grid is already 3 wide, so it fills the second row beside Classic. **Sumi to Kinpaku stays hidden until its art is approved** (the owner's decision): no placeholder card and no rings. It appears in the same grid when it ships. |
| Dawn, Kugane Lacquer | Two more palette tiles in the same row |
| Astrolabe, Kirikane | Two more frame segments |
| **Mix moons by state** | A new section after Frames: 8 rows (state name, its current medal at 32 px, a combo "From theme / {set}" with face thumbnails). The hint column is reserved, so a warning ("close to Blocked in a list", with a quiet amber dot, words not ⚠) never shifts the row. Then one status line, "1 pair of moons is close in a list", with **Fix it** and **Reset mix**. |
| **Share** | A new section: the code (`TM1-…`), Copy, a paste field, and Apply (which previews "This will change…" first) |
| Reset | Becomes "Hold to reset" while a mix is set (§B5) |

The data model (`AppearanceConfig`: Theme, Glyphs, Palette, Frames, HighContrast) ships whole in 1.16 with `Glyphs` empty, so 1.17 needs no migration.

### B7. Code map

- `ConfigWindow.Themes.cs`: the section, reusing:
  - `Chrome.SegmentedControl` (Frames);
  - `Chrome.MoonToggle`;
  - a new `ThemeCard` and `PaletteTile` (an `InvisibleButton` plus the draw list);
  - `UndoToast`.
- `ThemePreview`: draws the panel through `PushAppearance(resolved)`. The 16 px row and 64 px hero use the real glyph renderers.
- `AppearanceResolver` (Core, pure, tested): resolves the theme, then the overrides, then high contrast, into a `ResolvedAppearance`. It is cached and rebuilt only when the config or the host style changes.
- **Migration** (research §5.3):
  - `MoonStyle.Classic` becomes theme `classic`;
  - `GlyphPalette = HighContrast` becomes `HighContrast = true`;
  - `FollowDalamudColours` becomes palette `dalamud`.

---

## Decisions in this spec

1. **Snow's sky is a still dawn with no stars at all** (the supervisor's ruling). There is no star field, morning star, moving sky, meteor or Milky Way on light palettes.
2. **On light palettes, glows become warm washes**, and every shadow is navy at about a third of Night's alpha.
3. **Medals are never recoloured**, on any palette (the supervisor's ruling). On light palettes, Ready is carried by:
   - the gilt act-now ring;
   - the Ready-only warm wash;
   - the gold word;
   - the gold stripe.

   It is gated at ≥ 1.3× the next state by lightness-down-weighted OKLab ΔE (or chroma-only ΔE), with a 0.70× luminance floor (Not checked aside, held under Ready on chroma) and the .75 / 3 px wash for every set (§A4.1, the supervisor's final ruling).
4. **Gauges get their own ink on light palettes** (§A6).
5. **Ishgard Snow pairs with the Came (lead) kit** by default. Brass on Snow is allowed, with deep-gilt headings.
6. **Night gets two token bumps** (TextTertiary and StrongLine).
7. **The colour-vision gates:** the weakest pair is ≥ 11 under protan, deutan and tritan, and ≥ 12 in greyscale (§A8.1).
8. **Reset in 1.16 is one click with Undo.** It becomes Hold only once a mix exists (1.17).
9. **Hover previews, click applies, Undo follows.** There are no Apply or OK buttons.
10. **No "Try a light theme" hint** (the owner's decision).
11. **Sumi's card stays hidden until its art is approved** (the owner's decision).

## Open question

