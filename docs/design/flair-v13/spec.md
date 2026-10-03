# Decoration v13: three looks you can tell apart at a glance

**Owner note:** "The differences between full/quiet/plain 'looks' barely look any different from one another."

**Why they look the same today:** the three levels change ornament, but not structure. Full and Quiet share the Moon Road hero, the brass card border, the brass rules, the display headings and the table header. Plain only drops the rules and the fonts, and it still draws a banner for every quest. All three keep the same row heights, spacing, medals and status bar. Most of the difference is in a few 1 px lines.

**The fix:** each level becomes its own look. They differ in five things you can see in a thumbnail: pane tone, art, card material, row density and medal finish. Ornament then follows from each look.

| Level | Identity | Who it is for | Thumbnail signature |
|---|---|---|---|
| **Full** | *The Moon Road.* Night sky over water: gilt brass, banners, display type, glow, motion and room to breathe. | Players who keep the window open as part of the game's look, for example story-first players and screenshotters. | Indigo-to-night gradient, a bright banner top right, gold-framed cards, gold group eyebrows |
| **Quiet** | *Still water.* One flat theme tone, hairlines, tonal cards, standard type and silver-rimmed medals. | Players who keep it open while they play and want it calm next to the game HUD. | Even slate, no art, soft grey blocks on the right, sparse gold only on states |
| **Plain** | *The ledger.* Dense rows, flat glyphs, no cards or art, plain headings and colour only where it means something. | Completionists and planners scanning hundreds of rows, and small or low-resolution windows. | A spreadsheet: zebra rows, a grey header band with column dividers, a key-value list on the right |

Renders (1600 × 1000): `full.png`, `quiet.png`, `plain.png`, and `side-by-side.png` (the thumbnail test, all three at 32 %). Mock: `mock.html` (`#full`, `#quiet`, `#plain`, `#side`; `#glyphs` shows Plain's medals at true 12 px for measuring).

**Revision 2** applies the supervisor's review (`supervisor-review.md`):
1. A night grade on every Full banner.
2. `MedalTokens.Plain`, a brightness ladder of its own for Plain.
3. Plain's Locked out and Not checked return to the approved medal language.
4. The hero halo only for Ready and Ready on another job.
5. Polish:
   - corner marks overlap the frame;
   - no stars at the header rule;
   - Quiet's pane tones are about 3 % apart;
   - a moon road on the banner's bottom edge.

---

## 1. Every visual dimension, by level

Px values are logical px at UI scale 1.0 (`UiMetrics.Px`). Colours are the Night palette. Under "Follow Dalamud colours" the same roles map to the host style.

| Dimension | Full | Quiet | Plain |
|---|---|---|---|
| **Pane background** | Sky over water, deeper than today. `Top` brightened toward indigo (`#1B2552`) at the top, falling to `Night` by 46 %, then flat. A faint water lift (`Top` at 0.35) over the last 30 %. Rail is `Abyss` at 0.9. Under every column of the main window and Flight. | One solid theme tone in steps about 3 % of lightness apart: tree `#0E1323`, window and table `#131929`, detail `#182033`, cards `#1F273C` on the detail tone. Rail is `#10151F`. No gradient. Panes are told apart by tone, not by lines. | Flat `Night` everywhere (`#11151F`, a touch less blue). Rail and status bar are `Deep`. |
| **Star field** | Yes, at `StarField` magnitudes: 70 % faint (1 × 1, 0.16), 25 % small (r 1, 0.26), 5 % bright (r 1.5 plus a 7 px cross, 0.36). Only in empty sky: the rail, the tree below its last node, and the table's title band (above the column header, clear of the title, and never on the brass header rule). Never under a label (StarField's skip rule). Seeded, so it never shimmers frame to frame. | None | None |
| **Card frame** | Gilt brass: a 1 px border with a lit gradient (light from upper left, `#E2C78C` → `Gilt` → `#6E5732` → `#5A4729`). An 8 px L corner mark at each corner, overlapping the frame by 1 px so mark and frame read as one casting: the top two in `GiltHigh`, the bottom two in a darker brass. Fill `Raised` at 0.82 over the gradient. Inner top highlight `MoonHigh` at 0.07. Drop shadow 0 3 10 at 0.38. Radius 4. | Tonal: fill `#1D2438` on the lighter detail tone. No border, no shadow, radius 6. The card is just a slightly raised plane. | None. Sections are a heading row with a 1 px line under it; content sits on the pane. |
| **Dividers** | Moon-road divider (`Ornament.Divider`): brass rules fading out both ways from a 6 px lozenge of moonlight with a 6 px glow. Used between tree blocks, under the hero, and above the status bar (full-width brass rule, brightest at the centre). | A 1 px hairline in `#262D42`, full width, no fade, no sigil | A 1 px `Line` (`#262B38`) and nothing else. Group headers are a raised band, not a rule. |
| **Headings font** | Game display fonts (`GameHeadingFonts`). Titles (scope, quest name, hero state) in Jupiter, 20 / 21 / 17 px. Eyebrows (tree header, group headers, card headings, column header) in TrumpGothic caps, tracked 0.14–0.20 em, in `GiltHigh`. | Dalamud body font, semibold, sentence case. Title 14–16 px `Silver`; card headings 12 px `Silver`; column header 11 px `Dusk`. No caps, no tracking. | Dalamud body font, semibold, 11.5–13 px, `Silver`. The column header sits on a raised band. |
| **Row height and density** | Table 34 px (Comfortable) / 28 (Dense). Tree 34. Group header 34 (8 px air above). About 20 rows visible in a 900 px window. | Table 30 / 24. Tree 30. Group header 28. About 23 rows. | Table 24 at any Density (the `TableRowMinPx` floor, so the WCAG 2.5.8 24 px target still holds). Tree 24 with no gauge. Group header 20. Zebra always on. About 30 rows: all 28 quests and their 5 groups fit. |
| **Glyph style and size** | Menphina's Medallion as shipped. Row tier 18 px, with a 1.5 px drop shadow under the medal. Ready rows add a 4 px gold glow. The hero is the badge medal at 80 px. The status bar MSQ pill carries a 16 px medal. | The same medal face, with the brass rim swapped for a 1 px Silver hairline (`#C3CBDF` at 0.62) one pixel outside the well. The badge sits on a pane-coloured gap so it reads as laid on top. Row 16 px; hero 52 px. "Medals, lighter rim." | Flat medals, row tier only, 12 px, from a new **`MedalTokens.Plain`**. It is flat like high contrast, but uses the standard palette and has its own brightness ladder: Ready reads first and Completed recedes (see §1.1). Rims are 1 px in the state's ink at 0.6. With Moon style = Classic, Plain keeps the 1.11 glyphs at the same size. The detail pane has no hero medal: a 12 px glyph sits on the State line. |
| **Tree gauges** | Orbit: gold arc, moon knob, and a phase core. 22 px. | A 2 px ring arc only (`#CDB57A` on `#2A3149`), with no knob and no core. 18 px. | None. Counts and a right-aligned percentage column (`98%`) in `Mist`. Ready counts are a bare gold number. |
| **Banners** | Yes. The quest banner from the fallback chain, 156 px tall, with a 1 px brass keyline and a 6 18 shadow. **Every banner is night-graded at draw time** (§1.2), so a daylight zone sits in the moonlit window. A faint moon road (six MoonHigh dashes at 0.07–0.17, widening toward the viewer) lies along the bottom-right edge, clear of the title. The title is in Jupiter on the art. The hero medal rises over the banner's bottom-left edge. It has a gold halo **only when the state is Ready or Ready on another job**. | None. A title block (name, path · Lv) over a hairline, then a medal plate: 52 px medal, the state in its ink, the detail line and the step bar. | None. Name and path, then a key-value list: State · Level · Giver. |
| **Glows** | Active rail station (icon drop-glow, bar glow), selected tree node bar, Ready rows' road line, Ready medals, the hero halo (Ready and Ready on another job only), the divider lozenge, the primary pill (14 px gold bloom), the live pip. Nothing In journal, Blocked, Done, Completed, Locked out or Not checked glows. | None | None |
| **Motion** | Moon Road moments, never under Reduce motion. The hero medal rises 8 px and fades in (1.1 s ease-out) when the selection changes. A glint runs a Ready row's road line every 9 s. The station slides on a tab change. Bead rings sequence. Moonlit tiles fade. Plus everything Quiet has. | Subtle only: 120 ms hover fades, gauge fills, the reveal pulse. Nothing loops. | Essentials only. State changes are instant. Gauges and hover draw with no transition (TreePane.Strip already does this for gauges). |
| **Spacing tokens** | `pane-pad` 10–12, `gap` 12, card pad 11 × 14, pill height 30, toolbar 26 | `pane-pad` 8, `gap` 8, card pad 9 × 11, pill 28 | `pane-pad` 4–6, `gap` 4, no card pad, button 22, toolbar 22 |
| **Column widths** | Rail 64 · tree 292 · detail 404 | Rail 60 · tree 276 · detail 380 | Rail 40 (compact, icons only, no crest or labels) · tree 262 · detail 330. The table gains about 120 px. |
| **Table header** | Moon Road: the scope title in Jupiter with a gilt TrumpGothic count, column header in tracked caps, and a brass rule under it that is brightest at 18 % | Scope title in semibold body with the count in `Dusk`, the column header in body 11 px, and a hairline under it | The 1.3 raised header band (`#1E2330`) with 1 px column dividers. No scope title; the chip row already says Pinned. |
| **Selection** | Warm wash (`Moon` 0.13 → 0.02 left to right) with brass hairlines top and bottom | Neutral wash (`Silver` 0.06) with a 1 px `Veil` outline | `Silver` 0.09 wash, nothing else |
| **Action pills** | Raised gradient pills with brass borders; the primary is a lit gold gradient with a bloom | Flat pills with a neutral border; the primary is flat `Moon` | Rectangular 22 px buttons, text only, radius 3. The primary is set apart by gold text. |
| **Status bar** | 30 px. A brass rule above it, brightest at the centre. A `Deep` gradient. The orbit halo with %. The MSQ pill (gold tint, gold border, a 16 px medal). The live pip with a glow. | 26 px. A hairline above it. The ring halo with %. MSQ as gold text with no pill. The live pip flat. | 20 px. A 1 px line above it. Text only: `65% · 5,373 quests · showing 28 of 28 \| ● live \| MSQ: Dawntrail`. Grey, with the MSQ name in `Silver`. |
| **Tooltips** | Brass frame (same gradient as cards) with corner marks top-left and bottom-right, overlapping the frame by 1 px. Fill `Raised` 0.97. A 40 px medal with its glow. The title in Jupiter 15 px in the state's ink. Shadow 0 10 26. | Flat `#1A2135`, a 1 px neutral border, radius 6. A 28 px silver-rim medal. The title semibold 12.5 px in the state's ink. | A plain box, radius 2, padding 5 × 7. Inline text: **State.** then the meaning. No medal. |
| **Colour budget** | Gold carries meaning and material: states, brass, eyebrows and glows | Gold only for states, the primary action and unique rewards. Everything else is grey-blue. | Gold only for state glyphs, Ready counts and the word "unique". Everything else is greyscale. |

### 1.1 Plain's medal ladder (`MedalTokens.Plain`)

The high-contrast flat tokens are a contrast ladder for low vision, not a salience hierarchy. Reused for Plain, they would make Completed (the most common state) a gold coin louder than Ready. Plain therefore gets its own tokens, on the shipped `MedalPalette` inks.

Plain uses the shipped geometry, not new shapes. Every crescent is `MedalArt.PhaseOutline(SceneMoon, SceneMoonRadius, SceneMoonTerminator, SceneMoonTilt)`, the same call the high-contrast flat medal makes, scaled up for 12 px. That puts each crescent lit on the **right**, with its limb facing 28° below horizontal toward the lower right, matching the same state's medal in Full and Quiet. The phase direction carries meaning: a waxing moon means ready, a waning moon means done.

| State | Plain flat medal (16-unit box, disc r 6.9) | Rim (1 px, 0.6) | Rank |
|---|---|---|---|
| Ready | Lapis `#5480C8` disc, the largest crescent in `MoonstoneSpecular` `#F4F2EA` | `Moon` | Brightest |
| Ready on another job | Night enamel `#1C2752` disc, `MoonstoneHigh` `#E2E8F4` crescent | `Silver` | High |
| In journal | Night disc, `Moonstone` `#C3CEE4` crescent, a Tide `#6F8FD0` ribbon notch at the upper left | `Moon` | Middle |
| Completed | Night disc with a `MoonstoneMid` `#95A5C8` full moon (r 3.7), and a 1.5 px gilt `#C9A65C` check over it | `MoonDeep` | Below In journal |
| Done this cycle | Night disc with a waning `MoonstoneMid` half moon lit on the **left** (`PhaseOutline(DoneMoon, r, 0, 0, litLeft: true)`), and a 1 px gilt arc on the dark right side | `Silver` | Low |
| Blocked | `MoonstoneDeep` `#5E6E97` disc with a grey cloud band `#3A4260` over its lower half | `Dusk` | Low |
| Locked out | Flat Dalamud red `#C24A58` disc with two thin `#0B0408` fractures (1 px) meeting off-centre at the upper left: the shipped crack layout, cut to its two longest cracks. Not a circle-slash. | `Eclipse` | By hue, not brightness |
| Not checked | Dark disc `#151A28` with the flat "?" of the high-contrast `Unknown` mesh in `Mist`, and its moon-dot. Not a dotted ring. | `VeilText` | Lowest |

**Measured** at true 12 px on Plain's pane (`#glyphs`, greyscale; salience is the mean of |L − background| over the 12 × 12 box):

| State | Salience | Peak L |
|---|---|---|
| Ready | 87.6 | 242 |
| Ready on another job | 49.1 | 232 |
| In journal | 47.7 | 205 |
| Done this cycle | 45.7 | 164 |
| Completed | 45.6 | 168 |
| Blocked | 42.7 | 113 |
| Locked out | 41.6 | 102 |
| Not checked | 27.9 | 178 |

Ready is **1.78×** the next state (target 1.3×). Completed is **0.52×** Ready (target 0.8× or less). These are measured after fix 5, which reoriented the crescents and the half moon.

### 1.2 Full's banner night grade

Applied at draw time to every banner in the fallback chain: a tint on `AddImage` plus scrim quads. The art itself is never pre-graded. In order:
1. **Multiply** by `#2A3768`.
   - The review's 0.55 left the Dawntrail daylight banner at mean L 65, so the mock uses **0.70**.
   - In code, choose the strength per banner when its texture loads: from 0.55 up to at most 0.75, until the banner's mean L is 55 or less.
2. **Desaturate** by 35 %.
3. **Scrim** to `Night`: 0 at the top, 0.25 at 45 %, 0.85 at the bottom edge.
4. **Moonlight wash:** `MoonHigh` at 0.06, radial from the upper-left corner.

**Measured** on the Dawntrail banner at 1600 × 1000, art only (title and medal excluded):
- mean L **52.5** (target 55 or less);
- peak L **102** (target 170 or less, below the hero moon at about 230).

### Shared at every level
- The Dalamud title bar, the toolbar layout, the chips and every control's behaviour.
- The state stripe and its dash and dot pattern. It is an accessibility carrier (A3), so Plain keeps it at 2 px. Full widens it to 3 px.
- State ink colours, status text and every word of copy.
- **High contrast** still caps at Quiet (`FlairRules.Effective`). Quiet's tonal cards get a `VeilLine` border under high contrast, because tone alone is not a boundary for low-vision users.
- **Reduce motion** stops every loop and every Moon Road moment at all levels.

### Light and material notes (for the realism pass)
- **One light source: the moon, upper left.** The medal crescents sit upper left, so the brass gradients run bright upper left to dark lower right. Card highlights are on the top edge only, and shadows fall straight down (0 3 10). Bottom corner marks are a darker brass than the top ones.
- **Brass is never flat yellow.** It has four stops (highlight, body, shadow, reflected) at 1 px, so it reads as a metal edge, not a stroke. The gold primary pill is the only filled gold surface, and it has its own top highlight.
- **Glow is light, not paint.** Every glow is the same warm `Moon` hue at 0.3–0.6, and only on lit things: Ready, the selected station, the hero halo. Nothing blocked or completed glows.
- **The art is lit by the same moon.** The night grade (§1.2) cools and darkens every banner toward the Moon Road's indigo. Its scrim takes the art down to `Night`, so the zone image melts into the pane instead of sitting on it like a sticker. The hero medal's moon is the brightest thing in the banner.
- **Quiet and Plain have no light at all.** No shadows except the tooltip's separation shadow. That is what makes them read as flat on purpose, not as Full with parts missing.

---

## 2. Implementation map

### FlairRules (`Tsukimichi.Core/Ui/Flair.cs`): predicates to add or change

| Predicate | Today | v13 | Notes |
|---|---|---|---|
| `Rules(flair)` | Full, Quiet | **Remove.** Replace with `Rule(flair) → RuleStyle { MoonRoad, Hairline, Line }` (Full, Quiet, Plain) | The single biggest reason Quiet looks like Full. Every `Theme.ShowRules` caller must choose MoonRoad art or a hairline. |
| `PaneGradient(flair)` | Full | Full (unchanged); new constants in `Ornament.PaneGradient`: a deeper top stop and a bottom water lift | `MainWindow.cs` 1545 and 1636 |
| `PaneTone(flair) → PaneTone { Gradient, Tonal, Flat }` | — | **New.** Quiet paints tree, table and detail in three steps of the window tone. | `MainWindow.cs` column backdrops, `Theme.PushNightPanel` |
| `StarField(flair)` | (folded into `Glow`) | **New, split from `Glow`.** Full only. | Stars in rail, tree and table title band: `MainWindow.cs`, `TreePane.Art.cs`, `TablePane.cs`, `PathChart.cs` |
| `CornerMarks(flair)` | Full | Full (unchanged); also on tooltips | `Chrome.cs`, `DetailPane.Hero.cs`, `CharactersPane.cs`, `Theme.PushTooltip` |
| `Glow(flair)` | Full | Full (unchanged). The hero halo also needs `state is Ready or ReadyOnOtherJob`. | `TabStrip.cs`, `DetailPane.Hero.cs` (halo gate), `MedalGlyph.cs` (row-tier Ready glow), `Chrome.ActionPill.cs` (primary bloom) |
| `BannerGrade(flair)` | — | **New.** Full only: the night grade of §1.2 | `DetailPane.Hero.cs` (tint on `AddImage`, scrim quads, wash, moon-road dashes), `BannerArtwork.cs` (per-texture mean L, measured once on load, picks the multiply strength) |
| `Motion(flair, reduce)` | Full, not reduced | Unchanged (Moon Road moments) | `Motion.cs`, `TabStrip.cs`, `FlightPane.cs`, `MoonlitPane.cs`, `DetailPane.Hero.cs` |
| `UiMotion(flair, reduce)` | (Reduce motion alone) | **New.** Full and Quiet, not reduced. Plain: no hover fades, gauge fills or reveal pulse. | `Motion.cs` (`Motion.Pulse` / ease helpers), `TablePane.cs` hover ease, `TreePane.Strip.cs` (already static at Plain) |
| `GameHeadingFonts(setting, toggle)` | toggle and not Plain | **toggle and Full only** | `Plugin.cs` 568 → `Typography.Update` |
| `Card(flair) → CardFrame` | BrassCorners / Brass / Hairline | **BrassCorners / Tonal / None** (add `Tonal`, `None`; retire `Brass` and `Hairline`, except that high contrast maps `Tonal` to a VeilLine border) | `Chrome.cs` 243–294 (`CardBorder`, `CardChildBorder`), every `Chrome.Card` caller |
| `MoonRoadTable(flair)` | Full, Quiet | **Replace with `TableHeader(flair) → { MoonRoad, Eyebrow, Raised }`** | `TablePane.cs` 334 |
| `ReadyRoad(flair)` | Full | Full (unchanged); adds the 9 s glint under `Motion` | `TablePane.cs` 528 |
| `Hero(flair) → HeroStyle { Banner, Plate, Ledger }` | (ShowRules → Moon Road hero, else banner) | **New.** Full: banner with the medal rising. Quiet: title, then a 52 px medal plate with no banner. Plain: a key-value list with no medal. | `DetailPane.cs` 378–395 (`DrawHero`), `DetailPane.Hero.cs`; Plain's per-quest banner (`DrawBanner`) is retired |
| `Banner(flair)` | Full/Quiet (hero), Plain (1.3 banner) | **Full only** | `DetailPane.cs`, `FlightPane.cs` 220 (zone banner already Full-only) |
| `Medal(flair, moonStyle) → MedalFinish { Gilt, LightRim, Plain }` | atlas when hero and not Plain | **New.** Full `Gilt` (as shipped), Quiet `LightRim`, Plain `Plain` (or the Classic glyph when Moon style = Classic) | `MedalGlyph.cs` 219 (`UseAtlas`). In `MedalTokens`, add a `LightRim` token (rim off, a 1 px Silver keyline at `RimOuter`, the badge on a gap) and a `Plain` token set (§1.1, its own ladder; do not reuse `HighContrastDark`). `MedalArt.cs` needs flat Locked out (two cracks) and Unknown ("?" with moon-dot) meshes on the standard inks. Also `MedalAtlas.cs` and `TreePane.Glyph.cs` 25. |
| `TreeGauge(flair) → { Orbit, Ring, None }` | (Plain static fill) | **New.** Plain draws a percentage column instead. | `TreePane.Strip.cs` 97, `MedalGauge.cs`, `Orbit.cs` |
| `RowTarget(flair, density)` | `ScaleMetrics.TableRowTarget(density)` 32 / 24 | **New in `ScaleMetrics`:** Full 34 / 28, Quiet 30 / 24, Plain 24 / 24. Tree rows are Full 34, Quiet 30, Plain 24 (Plain has no halo, so the A4 halo-box floor does not apply). | `ScaleMetrics.cs` 166–179, `UiMetrics.TableRowContentHeight`, `UiMetrics.TreeRowHeight`, `TablePane.cs` 380–528 (Plain forces zebra on, 526) |
| `Spacing(flair) → FlairSpacing` | — | **New record** of `PanePad`, `Gap`, `CardPad`, `PillHeight`, `RailWidth` (Plain uses `RailCompactLogical` 44, icons only) | `DetailPane.cs`, `Chrome.ActionPill.cs`, `TabStrip.cs`, `MainWindow.Frame.cs` (window padding 266, 640) |
| `StatusBar(flair) → { MoonRoad, Quiet, Text }` | (ShowRules → brass rule) | **New** | `MainWindow.cs` 1684–1829 (`DrawStatusBar`), `ChromeBands.StatusBarHeight` (30 / 26 / 20) |
| `Tooltip(flair) → { Brass, Flat, Plain }` | one style | **New** | `Theme.PushTooltip` (Theme.cs 481–496), `UiMetrics.Tooltip`, `RewardTooltip.cs`, the stripe tooltip in `TablePane.cs` 1169 |

### `Theme` shortcuts (`Tsukimichi/Ui/Theme.cs` 197–210)
Replace `ShowRules` with `RuleStyle`, and add `PaneTone`, `ShowStars`, `UiMotion`, `MedalFinish`, `Spacing`, `HeroStyle` and `TableHeader` next to the existing `ShowPaneGradient`, `ShowCornerMarks`, `ShowGlow` and `FlairMotion`.

### `Theme.ShowRules` callers that must choose MoonRoad, Hairline or Line
`CharactersPane.cs` (461, 546, 593, 668, 837–843) · `Chrome.cs` 543 · `DetailPane.cs` (268, 385, 638, 745) · `DetailPane.Hero.cs` 431 · `FlightPane.cs` 137 · `GamePanelShell.cs` 150 · `MainWindow.cs` (886, 1077, 1714) · `MainWindow.Frame.cs` (265, 723) · `MoonlitPane.cs` (513, 1341) · `PathChart.cs` (1113, 1503, 1521) · `PlanPane.cs` 150 · `RouteWindow.cs` 233 · `SectionHeading.cs` (49, 77, 173) · `OpenSection.cs` 48 · `TabStrip.cs` 148 · `TodoOverlay.cs` 443 · `TreePane.cs` (246, 507) · `TreePane.Art.cs` 48.

Rule of thumb: brass art (sigils, the road under rows, the fading rule) becomes `MoonRoad` only. Structure lines become `Hairline` at Quiet and `Line` at Plain.

### Tests (`Tsukimichi.Tests/Ui/FlairRulesTests.cs`)
Rewrite the three level facts so they cover every new predicate. Keep the high-contrast cap test, and add these:
- `Card(Effective(Full, hc))` is `Tonal` with a border.
- `RowTarget` never goes below `TableRowMinPx` at any level or density.
- The hero halo is on only for Ready and Ready on another job.
- In a Core test over the `MedalTokens.Plain` inks, Ready's salience is at least 1.3× every other state's, and Completed's is at most 0.8× Ready's (the round-5 metric at 12 px).

### Settings copy (`Strings.Look.cs`); English only, localization frozen
- **Full:** "The whole Moon Road: night sky, brass, banners, game fonts and motion."
- **Quiet:** "Calm and flat: one tone, thin rules, no art or glow."
- **Plain:** "Information only: dense rows, small flat medals, no decoration."

The settings preview (`ConfigWindow.cs` 512–584) should draw a 3-up miniature of these three looks, not today's single swatch, so the choice is visible before you click it.
