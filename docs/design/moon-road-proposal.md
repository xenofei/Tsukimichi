# Tsukimichi: "Moon Road" redesign (design v4)

Date: 2026-09-30
Authors: Mirei (graphic artist) and Theo (UI/UX), working as a pair
Status: proposal for the owner. No code has been changed.
Builds on: `docs/design/ui-revamp-proposal.md` (tokens, gauges, cards, tab strip, most of it already shipped in 1.1), `docs/design/path-section-proposal.md` (star chart, shipped), `docs/design/glyphs/` (moon glyphs v2.1, shipped), `docs/design/mockups/main-window.html`.
Inputs: the owner's screenshots 3–9 (quest table, journal tree, rail, narrow tree, narrow My blues, narrow detail, Flight), `Tsukimichi/Ui/*`, `Tsukimichi.Core/Ui/*`, and the narrow-width and icon audit (`scratchpad/ui-audit/report.md`, `tree-dump.txt`, `names/names-before-after.tsv`, `icons/index.html`, `montage/rowsize.png`). Icon ids, name rules, pane floors and breakpoints follow the audit. §15 lists where we deliberately differ, and why.
Companions: `mockup.html` (high-fidelity mockup, wide and narrow views, Flight and Moonlit panels, the ornament kit) and `ornaments/` (SVG sources, 1x and 2x).

---

## 0. What the owner asked for, and where it is answered

| Owner's words | Answer | Section |
|---|---|---|
| "It still looks basic, and doesn't have enough flare/character to it." | One concept (the moon road) carried by a crest, one ornament family, game display fonts, official art in the hero, and a deeper surface ladder | 1–5, 7 |
| Unique icons per journal node, official where they exist | Official icons: EventIconType markers for sections, `BeastTribe.IconReputation`, class/job/role, Grand Company and ContentType tiles, `ExVersion.Icon` for chapters. Original monoline glyphs fill the gaps the audit found. The icon sits inside an orbit ring that carries progress. | 6, 7.2 |
| Scenario chain names take too much space | The audit's `JournalNames.Short`: parent prefix and kind suffix stripped, parentheses become " · ARR–EW" (the widest label goes from 514 to 297 px; the total from 62,040 to 32,443). Full name in the tooltip. | 7.2.3 |
| The journal tree overlaps when narrow | Every row element has a priority and a reserved width (`RowFit`), so nothing is ever drawn over another. Five tiers, the last an icon strip. The pane has a floor enforced by a real splitter. | 7.2, 8 |
| Panels hide content when narrow ("Blocked ·") | The state word is never cut and the separator only draws when the reason fits. My blues goes to two-line rows. | 7.3, 7.8, 8 |
| The left tab rail wastes space | Rail goes from 136 to 64 logical px (44 compact) and takes the overall gauge, help and settings; the freed width goes to the tree (default 240 → 300, its Full tier) | 7.1 |
| The detail pane wraps one or two characters per line | Three width tiers with label-over-value stacking below 320; wrapping by words, never letters; a 260 px floor | 7.4, 8 |

---

## 1. Concept: the moon road

**Tsukimichi (月の道, "moon road")** is the path of light the moon lays across night water. In the redesign, that image is the whole identity. The window is the night sea: a deep indigo surface that gets slightly lighter toward the top of each pane, like sky over water. The moon is the only warm light, and it keeps its one job: gold means "you can act on this now" or "you have walked this far". Everything the player has walked is drawn as a road of light: a thin gold path under each journal row, the lit thread of the star chart, and the stations of the tab rail strung on one line. The ornament is a single, restrained family in brushed brass: a hairline that fades in and out, three moon phases where it breaks, and a small four-point star. It frames only the moments that matter, such as the hero banner and section eyebrows, never every box. Character also comes from what the game already owns, read from the player's install: the official journal icons, the quest's journal banner, zone art for Flight, and the game's display fonts (TrumpGothic, Jupiter, MiedingerMid). The plugin then feels like a page of FFXIV's own journal written by moonlight, not a generic dark dashboard.

**Why it suits this plugin.** The name already *is* the concept, and the moon phases already carry quest state, so the metaphor grows from something that works rather than being pasted on. A road is also the right picture for what the plugin does: it shows where you have been, where you are and what comes next. The concept adds almost nothing per frame. It is mostly lines, gradients and textures the game already has in memory.

## 2. Principles

| # | Principle | In practice |
|---|---|---|
| P1 | **Gold still means act or walked** | The 1.1 gold discipline stays. Ornament uses **Gilt** (brass, 2.2:1 darker than Moon and close to Dusk in greyscale), which reads as frame, not as signal. Gold fills stay on Ready, Accepted, progress, the selected tree rule and primary buttons. |
| P2 | **Identity by official art, state by the moon** | The game's icons say *what* a node is, so the player recognises it at once. The moon says *where you stand*. They are never swapped. |
| P3 | **Nothing overlaps, ever** | Every row is laid out right to left by priority with reserved widths (`RowFit`, §8.1). Text never wraps narrower than 10 em; below that the layout stacks or hides. |
| P4 | **Ornament frames moments, not boxes** | Sections are open, with an eyebrow and a fading rule instead of rounded cards. Corner marks appear on at most one thing per pane. |
| P5 | **Flair is a setting** | `Settings › Display › Flair`: **Full** (default), **Quiet** (ornaments and fonts but no textures, star fields or banners behind the table), **Plain** (1.1 look). High contrast forces textures off. |
| P6 | **Cheap by construction** | Icon ids resolve once per catalog. Textures come from `ITextureProvider`'s shared cache, and bundled art is one atlas. Nothing allocates per frame, and motion is five short, triggered moments. |

---

## 3. Palette: Theme tokens

The existing tokens all stay (`Night #0F1424`, `NightSunken #0B0F1C`, `NightRaised #1E2437`, `NightHover #262D45`, `NightLine #2A3149`, `VeilLine #5C6584`, `Shadow #3A4363`, `Silver #DDE3F0`, `Mist #A9B2CC`, `Dusk #7C86A8`, `Veil #4A5270`, `VeilText #8A93B0`, `Moon #F2D27A`, `MoonHigh #FFF0BE`, `MoonDeep #D6B25A`, `MoonDim`, `Eclipse #B25C7F`, `EclipseText #D68AA8`). Six are added.

| New token | Hex | Role | Contrast on Night / Raised / Abyss | Rule |
|---|---|---|---|---|
| **Abyss** | `#080B16` | Rail background, the letterbox behind the hero banner, the sunken strip under the status bar | n/a (surface) | The deepest surface. Only the rail and wells use it. |
| **NightTop** | `#151C33` | Top stop of each pane's vertical gradient (NightTop at the top, Night at 60 % of the height and below) | n/a (surface) | `AddRectFilledMultiColor` once per pane: "sky over water". Quiet and Plain draw flat Night. |
| **Gilt** | `#A88B52` | Ornament hairlines: dividers, section rules, corner marks, the rail thread, the crest ring | 5.66 / 4.76 / 6.06 | Decorative only. Never text, never a fill larger than 4 px, never the only carrier of meaning. Usually drawn at alpha 0.55–0.8. |
| **GiltHigh** | `#D9BE82` | Ornament highlight points: corner diamonds, the sigil star, the divider's side phases | 10.2 / 8.5 / 10.9 | Points under 4 px only. |
| **Tide** | `#6F8FD0` | The cool accent: the "blue" of unlock quests (My blues), wind and sky in Flight, the moon road's cool end, external links | 5.69 / 4.78 / 6.09 | AA for text on Night. It is the only other hue, and never means "act now". |
| **TideDeep** | `#24345C` | Bottom stop of the procedural night-sky placeholder (quests without a banner), Flight's water gradient | 1.50 on Night | Surface only. |

Derived values (no new constants, just `WithAlpha`):

| Alias | Value | Where |
|---|---|---|
| `OrbitTrack` | VeilLine @ 0.55 | The unlit part of every orbit ring (was the halo gauge track) |
| `RoadTrack` | NightLine @ 1.0 | The unwalked part of a tree row's road |
| `RoadWalked` | MoonDeep → Moon, horizontal | The walked part |
| `GiltRule` | Gilt @ 0.7 → 0 | Section rule and divider arms |
| `HeroScrim` | Night @ 0 → 0.94 | Hero banner, from 35 % of its height to the bottom |
| `HeroSideScrim` | Abyss @ 0.55 → 0, left to right over 60 % | Keeps the title legible over bright banners |

**Where they go in code.** Add the hex constants to `GlyphTokens` in `Tsukimichi.Core/Ui/GlyphPalette.cs` (`AbyssHex = 0x080B16`, `NightTopHex = 0x151C33`, `GiltHex = 0xA88B52`, `GiltHighHex = 0xD9BE82`, `TideHex = 0x6F8FD0`, `TideDeepHex = 0x24345C`), so the contrast tests in `Tsukimichi.Tests/Ui` can assert the ratios above. Add `Vector4` and packed `uint` fields in `Tsukimichi/Ui/Theme.cs` beside the existing tokens. `SurfaceColors` gains `Deep` (Abyss, or the host's `WindowBg` darkened 35 % under "Follow Dalamud colours") and `Ornament` (Gilt, or the host's `Border` colour pushed to 3:1). The ornament then follows a light Dalamud theme without a second design.

Priority: **Must** (tokens are cheap and everything else uses them).

---

## 4. Type roles

The body stays on Dalamud's default font. It covers every client language, and quest, NPC and item names come from the game in that language. Character comes from game fonts in four small roles. Each is built through the existing `Typography` bucket mechanism (one handle per UI-scale bucket, the nearest native size, no bilinear blur) and merges Axis/Noto glyphs for anything the display face lacks.

| Role | Game font (`GameFontFamilyAndSize`) | Where | Fallback | Web stand-in in the mockup |
|---|---|---|---|---|
| **Body** | Dalamud default (Noto Sans) | Names, requirements, everything wrapped | n/a | "M PLUS 1p" (stands in for **AXIS**, whose look the game UI has) |
| **Caption** | AXIS 12/14/18 (as in 1.1) | Chips, card captions, provenance | default font × 0.85 | "M PLUS 1p" at 0.85× |
| **Eyebrow** (new) | **TrumpGothic** 18.4/23/34 | Section eyebrows ("REQUIREMENTS"), table headers, rail labels, pane titles in compact tiers | Caption role | "Saira Extra Condensed", 600, uppercase (stands in for **TrumpGothic**) |
| **Title** (new) | **Jupiter** 20/23/45 | Hero quest name, pane titles ("Seventh Umbral Era"), Flight zone name, Characters' name | Display role (AXIS 18 / 1.2×) for glyphs Jupiter lacks (any CJK title uses AXIS entirely: rule "title has a non-Latin glyph → use Axis") | "Cinzel", 600 (stands in for **Jupiter**'s engraved capitals) |
| **Numeral** (new) | **MiedingerMid** 12/14/18 | Tree counts, percentages, level pills, the rail's overall %, Flight counts | Caption role | "Rajdhani", 600 (stands in for **MiedingerMid**, the game's gauge numerals) |

Rules. There are at most two roles per row, never three. Eyebrows are uppercase Latin only; in other client languages the eyebrow uses the Caption role in sentence case. The Title role is used on at most one line per pane.

Implementation: in `Tsukimichi/Ui/Typography.cs` add `Eyebrow()`, `Title()` and `Numeral()` scopes and handles, built exactly like `Caption`/`Display` (`NewDelegateFontHandle` → `AddGameGlyphs(new GameFontStyle(family))` → merge `NotoSansCjkMedium` for punctuation and `AttachExtraGlyphsForDalamudLanguage`). Add `TypeScale.EyebrowGameFont`, `TitleGameFont` and `NumeralGameFont` in `Tsukimichi.Core/Ui/TypeScale.cs` with tests like the existing caption ones. Cost: three more font handles, which means about 1.5 MB of atlas at the default bucket and one atlas rebuild when the bucket changes. Nothing is added per frame. Priority: Eyebrow and Numeral **Should**, Title **Should** (behind Flair ≠ Plain).

---

## 5. Ornament kit

All pieces are original. SVG sources are in `ornaments/`, and each exists at 1x and 2x (`name.svg`, `name@2x.svg`). Lines and simple shapes are drawn with draw-list primitives, so they stay crisp at every UI scale. Only the pieces with curves too fine for the polygon filler at 12–24 px ship as texture: the crest, the custom glyphs and the corner mark. They ship as one atlas, `Tsukimichi/assets/ui/ornaments.png` plus `ornaments@2x.png` (the 2x one is used when `UiMetrics.IconFactor ≥ 1.5`), with UV rectangles in `Tsukimichi/Ui/OrnamentAtlas.cs`.

| Piece | File | What it is | ImGui technique | Cost | Priority |
|---|---|---|---|---|---|
| **Moon-road divider** | `divider-moonroad` | Hairline fading in from both ends, broken in the middle by waxing crescent · full moon · waning crescent | Two `AddRectFilledMultiColor` 1 px arms (Gilt 0 → 0.8 → 0), a gold `AddCircleFilled` (full), and two crescents made from `MoonGlyph`'s two-disc lens at r 2.6 | ~40 vertices, used 1–3 times per pane | **Should** |
| **Section rule** | `rule-fade` | Hairline from the end of an eyebrow to the pane edge, fading out | One `AddRectFilledMultiColor` | 4 vertices | **Must** (it replaces card borders) |
| **Sigil star** | `sigil-star` | Four-point star, 8–10 px, GiltHigh with a MoonHigh pip | `AddConvexPolyFilled` (8 points) + one circle; already drawn this way in `PathChart` | ~12 vertices | **Must** |
| **Corner marks** | `corner-mark` | Brass L with an inner echo and a diamond tip; four per framed object (UV-flipped) | Atlas `AddImage` with flipped UVs, or 3 `AddLine` + 1 `AddQuadFilled` when the atlas is missing | 4 quads | **Should** |
| **Section header treatment** | (composite) | `✦ REQUIREMENTS ─────── all met`: sigil, Eyebrow text in Mist, Gilt rule to the right, Caption on the far right in Dusk | Replaces `Chrome.BeginCard(title, icon)` with `Chrome.Section(id, eyebrow, caption)`, which draws those four things and a 6 px gap. There is no box and no padding, so 20 px of width comes back per section. | ~20 vertices | **Must** |
| **Rail crest** | `crest` | Moon over night water with its road, in a double brass ring | Atlas image, 40 px (1x) / 80 px (2x) | 1 quad | **Should** |
| **Gap glyphs** | `glyph-*` (17) | all-quests, removed, chronicles, chronicles-of-light, hildibrand, side-story, relic, endeavors, other, special, festival, deep-dungeon, region-coerthas, region-mordhona, moonlit, flight, plan-fallback. The style is a 1.2 px Silver monoline with one gold accent, legible at 24 px. | Atlas image | 1 quad each | **Must** (the tree needs them) |
| **Rail thread** | (primitives) | One vertical Gilt hairline through the tab icons; the active station lit with a short gold gradient and a moon bead | `AddLine` + `AddRectFilledMultiColor` + `AddCircleFilled` | ~16 vertices | **Should** |
| **Pane gradient** | (primitives) | NightTop → Night, top 60 % of each column | One `AddRectFilledMultiColor` per column, behind everything | 4 vertices × 4 columns | **Should** |
| **Star field** | existing `StarField` | Seeded dots, three magnitudes (from the path chart) | Reused behind the rail (below the tabs), behind empty states and in the procedural hero placeholder | ~60 circles for the rail, generated once per size | **Could** |
| **Road under tree rows** | (primitives) | 2 px path under each tree row: track NightLine, walked part MoonDeep → Moon | Two `AddRectFilled` (track) + `AddRectFilledMultiColor` (walked) on the row's bottom edge | 12 vertices per visible row | **Must** (it is the length encoding of progress; see 7.2) |

Textures: the only "texture" beyond game art is the star field, which is primitives. **No paper, no noise, no patterns.** At 1080p these read as dirt, and the game's own UI is clean.

---

## 6. Icon language

### 6.1 Three kinds of symbol, three jobs

| Symbol | Job | Never used for |
|---|---|---|
| **Official game icon** (journal genre, tribe, class/job, expansion, city crest, content type, item) | *Identity*: what a node or reward is. It is instantly recognisable because the player sees it in the game's own Journal. | State |
| **Moon** (state glyphs v2.1, filling moon) | *State and progress*: the eight quest states, and completion when shown as a moon | Identity |
| **Original glyph** (seventeen in `ornaments/`, monoline Silver strokes with one gold accent, in the ornament atlas) | *Gaps*: virtual nodes and the sections and genres the game gives no legible icon (the audit's gap list), and plugin concepts (Moonlit, Flight fallback) | Anything the game has a legible icon for |

### 6.2 Official icon for each journal node (resolution order)

`JournalSection` and `JournalCategory` have **no** icon column. `JournalGenre.Icon` is distinct only for classes, jobs and Grand Companies; elsewhere it is the generic quest-kind icon (`061411` sidequest, `061412` MSQ). `ExVersion.Icon` (`061875`–`061880`) is one shape in six colours. The region crests (`0626xx`) are 128 px paintings that turn to mush at 24 px, and the festival icons (`080xxx`) are 136 × 168 portraits (audit §1, `montage/rowsize.png`). The resolver walks the list below and takes the first icon that is **legible at 24 px and distinct from its siblings'**. Siblings that would share an icon get the next candidate.

| Node kind (examples) | 1st choice | 2nd | Gap glyph (original) | Ids (audit) |
|---|---|---|---|---|
| MSQ sections | **EventIconType marker**, MSQ | n/a | n/a | `071201` |
| MSQ chapters (Seventh Umbral Era … Post-Dawntrail II) | `ExVersion.Icon` (the short name disambiguates siblings in one expansion) | n/a | n/a | `061875` ARR … `061880` DT |
| Sidequests section | EventIconType marker, sidequest | n/a | n/a | `071221` |
| Regional sidequests (Lominsan, Gridanian, Ul'dahn, Ishgardian, …) | `ExVersion.Icon` | n/a | `region-coerthas`, `region-mordhona` (they span two expansions) | `061875`… |
| Allied Society sections | ContentType tile, allied societies | n/a | n/a | `061814` |
| Allied societies (Amalj'aa … Pelupelu) | `BeastTribe.IconReputation` | `BeastTribe.Icon` | n/a | `061901`–`061919`, `061990` |
| Class & Job, DoH / DoL categories | ClassJob / ContentType DoH, DoL | n/a | n/a | `0623xx`, `0624xx`, `061815`, `061816` |
| Role quests | role icons | n/a | n/a | `06258x` |
| Grand Company | ContentType GC tile; companies by `JournalGenre.Icon` | n/a | n/a | `061812`, `061401`… |
| Weapon Enhancement, Records of Unusual Endeavors genres | ContentType tiles where one backs the genre (Eureka, Bozja, Island, Occult, deep dungeons…) | n/a | `relic`, `endeavors` for the categories | `061833`, `061838`, `061847`, `061851`, `061824` |
| Chronicles of a New Era | n/a | n/a | `chronicles` | gap |
| Chronicles series (Primals, Alexander, Omega, Eden…) | `ExVersion.Icon` | n/a | per-series glyphs (Could, §15) | `061875`–`061880` |
| Hildibrand, Chronicles of Light, Side Story | n/a | n/a | `hildibrand`, `chronicles-of-light`, `side-story` | gap |
| Seasonal Events | n/a | n/a | `festival` (row icon; the portrait festival art goes in the hero instead) | gap |
| Other Quests, Special Quests | n/a | n/a | `other`, `special` | gap |
| All quests | n/a | n/a | `all-quests` (the crest) | n/a |
| Feature Unlocks (virtual) | EventIconType marker, feature (blue plus) | n/a | `plan-fallback` | `071341` |
| Removed from the game (virtual) | n/a | n/a | `removed` | n/a |

The implementation keeps no id table for what the sheets provide. It reads the columns, so a patch that adds an allied society gets its emblem for free. The hand lists are small and English-independent, keyed by sheet ids, in `Tsukimichi.Core/Ui/NodeIcons.cs`: the three EventIconType markers, the ContentType tile per genre, and the category → gap-glyph map.

Draw size: the audit found hi-res icons get soft when scaled down to 20–24 px. Rule: at ≤ 24 physical px draw `GameIconLookup` with `HiRes = false` (the native 32 px, crisp); above that use hi-res.

**Where the ids are resolved.** Add `uint IconId` and `OrnamentGlyph Glyph` fields to `TreePane.Node`, resolved in `EnsureNodes` once per catalog through a new `INodeIconSource` (Lumina reads in `Tsukimichi/GameData/NodeIconResolver.cs`, with the pure "distinct from siblings" choice in `Tsukimichi.Core/Ui/NodeIcons.cs` so it is testable). The table's genre icon and the detail breadcrumb read the same resolved id.

### 6.3 The orbit ring: the moon keeps progress, the icon keeps identity

The tree's 1.1 "halo gauge" was a moon with a ring arc. In the redesign **the official icon sits in the middle and the ring stays**, so the gauge becomes an *orbit*:

- The icon is 18 logical px (hi-res texture, `AddImageRounded`, rounding 3). Circular icons like the expansion rings are drawn unrounded.
- The ring is r 13, 2 px: OrbitTrack for the unlit part and a Moon arc from 12 o'clock clockwise for the fraction. Round caps, as in the 1.1 gauge.
- A **moon bead** sits at the arc's head: a 2.2 px MoonHigh disc on a 2.8 px Night disc. It is "the moon travelling its orbit" and it makes small fractions visible (a 3 % arc is a bead, not a hairline).
- At 100 % the ring closes, the bead becomes a full-moon pip at 12 o'clock, and the node's name turns MoonDim (as in 1.1).
- At 0 %, only the track shows, with no bead, and the icon draws at 60 % alpha (as the audit recommends), so untouched chapters recede.
- The Ready badge becomes a 6 px gold pip with a sigil spark at the ring's 2 o'clock in the icon tier (§8), and a pill with the count in wider tiers.

The **moon stays the state signal**: every quest row, the detail hero and the path chart keep the v2.1 glyphs. The **filling moon** stays wherever a scope is summarised without an icon: the rail's Journal tab, the rail's overall gauge and Characters' per-section summary.

ImGui technique: `AddImageRounded`, then `PathArcTo` + `PathStroke` for the track and the arc (this is `GaugeGeometry`, capped at 32 segments), then two `AddCircleFilled` for the bead. Cost: ≈ 90 vertices per visible node, so 40 visible rows come to ≈ 3,600 vertices. That is well inside budget, and it replaces the 1.1 gauge (≈ 70). Priority **Must**.

### 6.4 Texture loading pattern (one pattern for everything)

```
// once per catalog (EnsureNodes / model build): resolve ids, never per frame
node.IconId = resolver.IconFor(node);                  // uint, 0 = use node.Glyph

// per frame, visible rows only
var lookup = new GameIconLookup(node.IconId);           // HiRes (default); a struct, no allocation
if (node.IconId != 0 && textures.GetFromGameIcon(lookup).TryGetWrap(out var wrap, out _))
    dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, tint, rounding);
else
    OrnamentAtlas.Draw(dl, node.Glyph, min, max, tint); // bundled glyph, or a NightRaised square while loading
```

- **Game icons**: `ITextureProvider.GetFromGameIcon(GameIconLookup)`. This is the shared immediate texture that the plugin already uses for rewards, jobs and banners. Dalamud loads it asynchronously and releases it after it has gone unused for a while, so off-screen rows cost nothing. The call never blocks: until `TryGetWrap` succeeds, a placeholder draws.
- **Game paths** (zone loading images, maps): `ITextureProvider.GetFromGame(path)`, with the same `TryGetWrap` pattern. The path string is built once per zone, not per frame.
- **Bundled art** (the ornament atlas): `ITextureProvider.GetFromManifestResource(typeof(Plugin).Assembly, "Tsukimichi.assets.ui.ornaments@2x.png")` (embedded resource, no file path to go wrong). The alternative is the file-based getter on `AssemblyLocation.Directory`; check which name API 15 uses. It loads once. `OrnamentAtlas` keeps the `ISharedImmediateTexture` and computes UVs from a const table.
- The game art is never shipped. It is read from the player's install at runtime; the ornament atlas is original art.

---

## 7. Pane by pane

Sizes are logical px at UiScale 1 (× `UiMetrics.Scale` at runtime; icons also × IconScale).

### 7.1 The rail (Journal / Moonlit / Characters / Flight / My blues)

Today it is 136 px wide, has five rows of 30 px, and the rest is empty (screenshot 5).

| Change | Technique | Cost | Priority |
|---|---|---|---|
| **Width 64** (compact: 44). Each tab becomes a 54 px "station": 22 px icon centred, label under it in **Eyebrow** (TrumpGothic 18.4, Mist; Silver when active). Labels are short by nature: JOURNAL, MOONLIT, CHARACTERS, FLIGHT, MY BLUES. At TrumpGothic's width "CHARACTERS" is ~50 px. | `InvisibleButton` per station (as now), draw list for the rest. `LayoutBudgets.RailLogical` → 64; `TabRowLogical` → 54; `TabStrip.RailWidth` measures the widest Eyebrow label + 2 × 6 padding and clamps to 64..84. | Same as today | **Must** |
| **Crest** on top (40 px), with a 1 px Gilt divider under it. Click = Journal › All quests. | Atlas image | 1 quad | **Should** |
| **Thread**: one Gilt @ 0.35 hairline down through the icon centres, from the crest to the last station. The active station: a 2 px gold segment on the thread (MoonDeep → Moon, 22 px), the icon in Moon, the label in Silver, a soft glow disc behind the icon (Moon @ 0.06, r 18), and a moon bead on the thread at the station. This replaces the 3 px left bar. | `AddLine`, `AddRectFilledMultiColor`, `AddCircleFilled` | ~30 vertices | **Should** |
| **Icons**: Journal = filling moon of overall completion (as 1.1). Moonlit = glyph `moonlit`. Characters = **the viewed character's current job icon** (official, framed), which falls back to the FontAwesome `Users`. Flight = the game's aether-current icon (the audit confirms the id), falling back to glyph `flight`. My blues = **the official blue-plus unlock-quest icon**. | `GetFromGameIcon` / atlas | 3 small textures | **Must** |
| **Badges**: Journal's Ready count moves to the icon's top-right as a 14 px pill ("99+"). My blues shows the number of unlock quests Ready now. | `Chrome.Badge` | none | **Must** |
| **Foot of the rail**: the overall **orbit gauge** (36 px, filling moon inside a ring) with "34%" in Numeral under it. The tooltip gives done/total per section. Below it are round **Help** and **Settings** buttons (moved from the toolbar's right cluster). The toolbar keeps search, quick views, Filters and the character chip, and gains ~90 px. The status bar drops its gauge. | Existing `MoonGauge`, `Chrome.IconButtonRound` | none net | **Must** |
| **Texture**: the star field (Quiet: none) fills the space between the last station and the foot, fading out toward both ends | `StarField` + two alpha gradients | ~60 circles | **Could** |
| **Compact rail** (44 px, icons only, labels in tooltips). It turns on automatically when the window is narrower than 1,040 logical px, or always via `Settings › Display › Compact rail`. | Same code, no label pass | none | **Should** |

Net: the rail frees **72 px** (92 compact). As the audit recommends, the freed width goes to the tree. Because our rail is 64 rather than 44, we can make it `LeftColumnLogical` 240 → **300** (the audit said 280), which puts the tree in its Full tier (≥ 300) at the default window size.

The audit recommends a 44 px icon rail that expands to today's labelled 136 px rail. We keep 44 as the compact mode, but make **64 px labelled stations the default** (§15).

### 7.2 The journal tree

#### 7.2.1 Row anatomy (Full tier)

```
 ▾ (◯icon) Seventh Umbral Era   2.0 ARR  ②         160/213
   ════════════════════════════════════─────────────────  ← road: walked (gold) / to walk (line)
```

| Element | Spec | Technique | Priority |
|---|---|---|---|
| Row | 32 px (was 30), still `TreeNodeEx` under the overlay (keyboard nav, open-on-arrow and reveal are preserved) | as 1.1 | **Must** |
| Chevron | 8 px triangle, Dusk; rotates on open (existing motion) | as 1.1 | n/a |
| Orbit icon | 28 px box (§6.3) | §6.3 | **Must** |
| Name | **Short name** (§7.2.3), Silver; sections use Body semibold (two-pass as now) | `AddText` clipped by `RowFit` | **Must** |
| Patch caption | Only on MSQ chapters in the Full tier: "2.0", "5.1–5.3" in Dusk Caption (players think in patch numbers). **Room-only**: `RowFit` shows it only when the name fits whole beside it, so it never costs the name a letter. | derived once from the node's quests' `Patch` min–max | **Should** |
| Expansion pill | The " · ARR–EW" / " · DT" suffix that `JournalNames.Short` produces is drawn as a pill after the name ("Main Scenario" + ARR–EW), and is **room-only** like the patch caption: it drops before the name is cut, and the tooltip always has it. Also shown when a node's parent spans several expansions and the icon is not already the expansion ring. | `Chrome.PillAt` | **Should** |
| Ready badge | Pill: Moon @ 0.16 fill, Moon text, Numeral role | as 1.1, restyled | **Must** |
| Count | "160/213" in **Numeral**, Mist; complete nodes in MoonDim | `AddText` | **Must** |
| Road | 2 px on the row's bottom edge, from the name's x to the row's right edge − 8. The walked part is `fraction × length` (MoonDeep → Moon); the rest is NightLine. It replaces 1.1's separate 44 × 3 mini bar and gives the length encoding the moon lacks. | §5 | **Must** |
| Selected | Silver @ 0.07 wash, a 2 px Moon rule at the left edge (as 1.1), and the road brightened to full Moon | as 1.1 | n/a |
| Section spacing | A 6 px gap and a **moon-road divider** between the story block (MSQ, Chronicles), the side block (Sidequests, Allied, Class & Job, Other) and the virtual block (Feature Unlocks, Removed) | §5 | **Should** |

#### 7.2.2 Header

The tree gets a one-line header: "✦ JOURNAL" in Eyebrow with a Gilt rule and, right-aligned, the scope's count in Numeral. In the Icon tier this line shows the **selected node's short name** instead, because the rows no longer do.

#### 7.2.3 Short names ("scenario chain names take too much space")

We adopt the audit's rule as specified: a pure `Tsukimichi.Core/Ui/JournalNames.Short(name, parent, section, language)`, applied once in `EnsureNodes`, with the full before/after table in `ui-audit/names/names-before-after.tsv`:

| Rule | Example (before → after) |
|---|---|
| Strip the parent's prefix | "Chronicles of a New Era - Eden" → **"Eden"**; "Chronicles of a New Era - The Warring Triad" → **"The Warring Triad"** |
| Strip the kind suffix (Main Scenario Quests / Sidequests / Quests / Events / Facet of / Faculty of) | "Seventh Umbral Era Main Scenario Quests" → **"Seventh Umbral Era"**; "Post-Shadowbringers Main Scenario Quests II" → **"Post-Shadowbringers II"**; "Hildibrand Sidequests" → **"Hildibrand"**; "Amalj'aa Quests" → **"Amalj'aa"** |
| Parentheses become " · abbreviation" | "Main Scenario (A Realm Reborn through Endwalker)" → **"Main Scenario · ARR–EW"**; "Allied Society Quests (Dawntrail)" → **"Allied Societies · DT"** |
| A child equal to its parent becomes "Story" or "General" | "Omega Quests" under "Omega" → **"Story"** |
| Role genres | → **"Tank · ShB"** |

Result over 307 nodes: the total label width drops from 62,040 to 32,443 px, the median from 183 to 94 px, and the widest from 514 to 297 px. The full name and journal path are always in the tooltip, and scope labels and chips elsewhere keep full names (the 40 "Main / Daily" genres are ambiguous without their parent).

Our addition: the **patch caption** (7.2.1) at the Full tier ("5.1–5.3" beside "Post-Shadowbringers"). The game's own chapter name (`JournalGenre.Name`, e.g. "The Voyage Home", "Dark Reprise") becomes the tooltip's second line and the hero's breadcrumb chip. It adds flavour where there is room, and leaves the tree's label as the name players search for (see the critique log, Short names R3).

#### 7.2.4 Width tiers

See §8.2 for the numbers (the audit's breakpoints). In short: **Full → Trim** (patch caption and expansion pill go) **→ Compact** (count becomes a percentage) **→ Slim** (the ring alone carries progress; Ready becomes a dot) **→ Icon strip** (the pane snaps to 56 px of orbits, and the header names the selection). The row is laid out by `RowFit` in every tier, so the overlap in screenshot 6 cannot happen: the count is reserved *before* the name, the name keeps at least 48 px and is ellipsized, and a cut name always has a tooltip. The road is not in the width budget at all: it lies under the row.

### 7.3 The quest table

Screenshot 3's issues: identical "Close to Home" ×5, the status cut to "Blocked · after: Coming to Limsa Lo…", and an un-styled header.

| Change | Technique | Cost | Priority |
|---|---|---|---|
| **Pane title** over the table: the scope's short name in **Title** (Jupiter), the breadcrumb "Main Scenario ›" in Caption Dusk before it, and a caption on the right: "160 of 213 · 2 ready" (Numeral). A section rule under it. | Draw list over a `Dummy` | ~30 vertices | **Should** |
| **Header row** in Eyebrow, uppercase, Mist; the sort arrow is a Silver 6 px triangle; a Gilt @ 0.5 hairline under the header replaces the table's strong border | `TableHeadersRow` with `ImGuiCol.TableHeaderBg` = transparent, the labels drawn by `TableHeader("##")` + draw-list text (keeps sorting and reordering) | none | **Should** |
| **Duplicate names** get a disambiguator in Mist: "Close to Home · Gridania / · Limsa Lominsa / · Ul'dah" (the issuer's place, already in the catalog). Computed once per query result: when two visible rows share a name, the place is appended. | pre-materialised string in `QueryRunner` | none per frame | **Must** |
| **Genre icon** (16 px, official) before the name, only when the scope spans more than one genre (so "All quests" or a search, not inside one chapter) | `GetFromGameIcon` | 16 px textures, visible rows | **Should** |
| **Status cell**: the state word in its text tone (Ready Moon, Blocked Mist, Foreclosed EclipseText) is **never cut**. The " · reason" part draws only when at least 6 em of it fits, and is otherwise replaced by a 12 px ⓘ mark whose tooltip is the reason. This fixes "Blocked ·" (screenshots 3 and 7). | `RowFit` on the cell | none | **Must** |
| **Level** pill text in Numeral; **Exp** pill stays text (ARR/HW/SB/ShB/EW/DT) but in Eyebrow | as 1.1 | none | **Should** |
| Rows: 1.1's state stripe, lift and selection wash stay. The **Ready row** also gets the road treatment: a 1 px Moon @ 0.35 line along the row's bottom from the name to the right edge ("this one is on your road now") | one `AddRectFilledMultiColor` | 4 vertices per Ready row | **Could** |
| **Column tiers** (§8.2): Rewards → Exp → Job → reason → two-line rows | `TableSetColumnEnabled` as 1.1 already does for Rewards/Exp; the two-line mode is a second row template | none | **Must** |

### 7.4 The detail pane

The 1.1 hero exists (banner ≤ 96 px, scrim, state pill, name), but at narrow widths every part wraps to one or two characters (screenshot 8), and when the banner is missing or hidden the header is a plain card.

#### 7.4.1 Hero header (Must)

```
┌╴corner                                              corner╶┐
│      Quest.Icon journal banner (376×120, hi-res 1128×360)   │  ← full aspect: width × 0.32, max 128
│      scrim from 35 % down, side scrim on the left           │
└╴  ◐ ╶───────────────────────────────────────────────────── ┘
   (moon r 22 sits ON the bottom edge, "rising", 4 px Abyss ring)
      Lady of the Vortex                      ← Title (Jupiter), wraps at column − moon
      [msq] Seventh Umbral Era · Lv 44 · ARR · All jobs · 2.0   ← chips, wrap as a flow
      Ready now · from Cid, Gridania            ← state line (state tone + Mist)
```

| Element | Spec | Technique | Cost |
|---|---|---|---|
| Banner | `Quest.Icon` at column width, **full aspect** (0.32; max 128 px, up from 96), cover-cropped. Missing or hidden (spoiler shield): the **fallback chain**, then the procedural sky | `Chrome.ImageCover` (exists) | 1 texture: 1128 × 360 hi-res ≈ 1.6 MB, one at a time |
| Fallback chain | 1. `Quest.Icon`; 2. the banner of the duty this quest runs or unlocks (`ContentFinderCondition.Image`, same 376 × 120 format); 3. **procedural night sky**: TideDeep → NightTop gradient, the star field seeded by quest id, the quest's genre icon at 48 px and 25 % alpha on the right, and a Gilt horizon line. When the spoiler shield hides the art, the sky shows with "Artwork appears once the quest is in your journal" in Mist. | `ImageCover` / primitives | ≤ 1 texture |
| Frame | **Corner marks** at the four corners, inset 4 px; a 1 px Abyss inner border (it makes bright banners sit in the dark) | atlas × 4 | 4 quads |
| Scrims | `HeroScrim` bottom and `HeroSideScrim` left | 2 × `AddRectFilledMultiColor` | 8 vertices |
| Rising moon | The state glyph at r 22 (IconScale), centred on the banner's bottom edge at x = 16 + r, with a 4 px Abyss ring behind it (a cut-out). The Ready halo draws as usual. Hover = state tooltip (as 1.1's pill). | `MoonGlyph.Draw` | as 1.1 |
| Title | Title role, Silver, wrapped at (column − moon box − 8). With a CJK title it uses the Display role. | `AddText` with wrap width | none |
| Meta chips | Flow layout: breadcrumb chip (genre icon 14 px + short genre name; click = select that tree node), Lv, expansion, class requirement **shortened** ("All jobs", "DoW/DoM", "Gladiator 30"; the full text is in the chip's tooltip), patch. Chips wrap to the next line as whole chips and are never split. | new `Chrome.FlowChips(ReadOnlySpan<Chip>)`: measure, place, wrap; strings built once per model | none per frame |
| State line | The state word in its tone + the reason in Mist, wrapped (≥ 10 em guaranteed by the tier) | `TextWrapped` | none |
| Special badge | `Quest.IconSpecial` top-right on the banner (as 1.1) | as 1.1 | n/a |

The 1.1 state *pill* on the banner is removed. The rising moon carries the state and the state line spells it out, which saves one element that wrapped badly.

#### 7.4.2 Sections (Must)

Every card becomes an **open section** with the header treatment (§5): `✦ REQUIREMENTS ───── all met`, `✦ REWARDS ───── 3`, `✦ PATH ───── step 41 of 53`, `✦ GIVER`. There are no boxes. The moon-road divider separates the hero from the sections. The only framed object in the pane is the hero.

| Section | Wide layout | Narrow layout (tier D3) |
|---|---|---|
| Requirements | Two-column grid: mark (✓ Marks, 14 px) · label (Mist, 38 % of width) · value (Silver). The next-step row gets a 2 px Moon rule at its left. | Stacked: mark + label (Eyebrow-size Caption, Mist) on one line, value under it at full width |
| Rewards | 40 px icon tiles (hi-res) on NightSunken squares, flow-wrapped; Moonlit-unique ones get a Moon ring + crescent badge (as 1.1). Gil and EXP are Numeral pills. Duty unlocks show the **duty banner** as a 3:1 thumbnail with its name. | 32 px tiles, 2–4 per row |
| Path | The star chart (as shipped) | Gutter 24 (was 36), names wrap at ≥ 10 em, beads show only their count |
| Giver | NPC Silver, zone + coords Mist, map-pin | unchanged |

#### 7.4.3 Action bar (Must)

The primary action is a gold pill with an icon and a label ("Teleport · Gridania"). The secondary actions are 28 px round icon buttons (Flag, Open journal, Link in chat, Copy coordinates, Show path). When the row cannot hold them all, the extras go into a **⋯ overflow** popup (Theme.PushPopup) instead of wrapping into a second and third row (screenshot 8). At D3 (below 320 px) the primary pill takes the full width, and the icons plus ⋯ sit on the line under it.

### 7.5 Moonlit

| Change | Technique | Cost | Priority |
|---|---|---|---|
| **Left list** of reward kinds with **official icons**: the game's own menu icons (`MainCommand.Icon` for Mount Guide, Minions, Emotes, Orchestrion List, Fashion Accessories, Glamour; the audit confirms rows). Each row has an orbit ring of obtained/total and a Numeral count. | same row component as the tree (`OrbitRow`) | ~8 icons | **Should** |
| **Gallery view** (toggle beside the table view; remembered): tiles of 64 px **hi-res item icons** on NightSunken, name under them (2 lines max), the quest in Mist. Obtained: a Moon ring + full-moon pip. Not obtained: the quest's state moon at 14 px in the corner (so "Ready" treasures glow). Columns = ⌊width / 96⌋. | `ImGuiListClipper` over rows of tiles; `GetFromGameIcon` hi-res | 64 px textures, visible tiles only | **Should** |
| **Header**: the kind in Title ("Minions"), "11 of 64 found only in quests" caption, section rule | as 7.3 | none | **Should** |
| The table view keeps 1.1's columns and gets the header and status fixes of 7.3 | as 7.3 | none | **Must** (status fix) |

### 7.6 Characters

| Change | Technique | Cost | Priority |
|---|---|---|---|
| **Left list**: each stored character is a row with their **current job icon** (framed, official) and the name. The live one gets a small breathing-free Moon pip (static; see §9). | `GetFromGameIcon` | 1 icon per row | **Should** |
| **Dashboard header**: job icon at 40 px in a corner-marked frame (the one framed object of this pane), the name in Title, world and "last seen" in Mist, the overall orbit gauge at 48 px on the right | atlas + primitives | small | **Should** |
| **Completion by section**: a grid of orbit icons (the tree's component at 36 px) with short names: MSQ, each expansion ring, Sidequests, Allied, Class & Job, Other. It replaces the list of filling moons. | `OrbitRow` in a grid | ~12 orbits | **Should** |
| **Allied societies** row: tribe emblems (`BeastTribe.Icon`) with rank as a Numeral caption; **Grand Company** emblem | `GetFromGameIcon` | ~20 icons | **Could** |
| Job ladders: the framed class/job icons (`062100+` family) replace FontAwesome where any are left | as 1.1 | n/a | **Could** |

### 7.7 Flight (screenshot 9)

| Change | Technique | Cost | Priority |
|---|---|---|---|
| **Zone art banner** at the top of the main pane: the zone's **loading-screen image** (`TerritoryType.LoadingImage` → `LoadingImage.FileName` → `ui/loadingimage/{FileName}_hr1.tex`; confirm the path with the audit probe), cover-cropped to `min(160, width × 0.35)`, scrim, zone name in Title and the expansion ring icon. Only the **selected** zone's image is loaded. | `GetFromGame(path)` + `ImageCover` | 1 texture (~2 MB at 1080p crop; the full image is up to ~8 MB decoded), one at a time | **Should** |
| **Left list**: each zone row gets a **bead ring**. The ring is split into N segments, one per quest current (usually 5 in HW, 2–4 elsewhere): lit = attuned, and a hollow bead = not yet. The fraction becomes countable at a glance ("4 of 5" is four gold beads). Expansion headers are Eyebrow with the expansion ring icon. It replaces the plain filling moons. | `PathArcTo` per segment with 8° gaps | ~120 vertices per row | **Should** |
| **Map with pins** (under the currents list): the zone map texture (`Map.Id` → `ui/map/{id}/{id without '/'}_m.tex`), with each quest current's giver plotted as a pin (gold = attuned, hollow Tide = not). Clicking a pin flags it. | `GetFromGame` + `AddImage` + map coordinate conversion (the same maths the Flag button uses) | 1 texture, ~2.7 MB compressed / up to 16 MB decoded; unloaded when leaving the tab | **Could** |
| Field currents line: "Field currents: 5 of 5 attuned. The Aether Compass finds the rest." Tide text, wind glyph | as 1.1 | none | n/a |

### 7.8 My blues (screenshot 7)

| Change | Technique | Cost | Priority |
|---|---|---|---|
| **Two-line rows** below 560 px (and one line above): line 1 is the state moon, the name (Silver), and **kind icons** (official: the job's framed icon for job unlocks, `ContentType.Icon` for dungeon/trial/raid unlocks, the blue-plus unlock icon for "Other", ≤ 3 then "+n"). Line 2 is the status in its tone + the reason in Mist, wrapped. **No status column**, so "Blocked ·" can no longer be cut. | row template in `PlanPane` | 1–3 small textures per row | **Must** |
| **Flag / Reveal** become 26 px round icon buttons (map pin, eye) with tooltips, right-aligned and vertically centred on the two lines. Below 420 px they fold into one ⋯ menu. | `Chrome.IconButtonRound` | none | **Must** |
| **Expansion cards** keep their fold, but the card header becomes the section treatment with the expansion ring icon and a bead ring of Ready/total | as §5 | small | **Should** |
| Zone groups: zone name in Eyebrow with a Gilt rule | as §5 | none | **Should** |

### 7.9 Settings

| Change | Technique | Cost | Priority |
|---|---|---|---|
| New **Look** group at the top of Display: **Flair** (Full / Quiet / Plain), **Game fonts for headings** (on; off = Caption/Display roles), **Compact rail** (Auto / On / Off), **Reduce motion** (exists), **Glyph palette** (exists), each with a one-line live preview drawn beside it (the rail station, a tree row, the hero corner) | `Chrome.SegmentedControl`, draw-list previews | preview only while the window is open | **Should** |
| The settings window takes the Night chrome (`PushNightWindow`) and section eyebrows; categories on the left as a slim list with the same station look as the rail (icon + Eyebrow label) | as §5 | none | **Could** |

### 7.10 Toolbar and status bar (small, for coherence)

The toolbar loses Help, Tutorial and Settings to the rail foot (Tutorial moves into Help). The character chip shows the official job icon (as 1.1). The status bar loses its gauge (it is on the rail now) and keeps counts, the live pip, the MSQ pill and the version. The MSQ pill gets the MSQ quest-kind icon at 14 px. **Should.**

---

## 8. Responsive rules

### 8.1 Mechanism (shared by every pane; follows audit §4)

1. **PaneSplit replaces the body table.** The four-column resizable `ImGui` table lets any pane shrink to about 4 px and stores widths in imgui.ini with no reset. It is replaced by the audit's `PaneSplit`: `InvisibleButton` handles (6 px hit area, a 1 px NightLine line, Gilt @ 0.6 on hover), widths in `Configuration` in logical units, a double-click to reset, and the pure `PaneLayout.Solve` in Core. Floors: **tree 180, centre 320, detail 260**. The window minimum becomes about 950 logical px with the 64 px rail.
2. **Tiers, not pixels.** A small `Tsukimichi.Core/Ui/PaneTier.cs` (`PaneTier.For(widthLogical, breakpoints)`, ±8 px hysteresis) with the breakpoints as consts in `LayoutBudgets`, which the layout tests already own. It is computed once per frame per pane (a comparison, no allocation).
3. **RowFit** (`Tsukimichi.Core/Ui/RowFit.cs`, as the audit specifies): row elements fitted right to left by priority, with the name as the flexible element (minimum 48 px, ellipsis, and a tooltip whenever it is cut). It runs over a caller-owned `Span`, so nothing allocates. It is used by the tree, the table status cell, My blues, Flight and Moonlit.
4. **ColumnPlan** for tables (`TableGeometry`): Name becomes a **stretch** column (weight 3, min 140), Status stretch (weight 2, min = state word + 24), and the others hide by priority with 16 px hysteresis.
5. **Words, never letters.** The audit's `TextFlow.Wrapped` + `WordWrap` (word breaks, CJK fallback, cached per string and width), `LabelValue` (stacks label over value when the value would get under 10 em), `EllipsisText`, `StatusText` and `SameLineOrWrap` go into `Chrome`.

### 8.2 Breakpoints by pane (logical px of the pane's own width)

| Pane | Tier (width) | What shows | What goes first → last |
|---|---|---|---|
| **Rail** | Stations (window ≥ 1,040) 64 px | icon + label, crest, gauge + %, help, settings | → labels (44 px compact) → crest shrinks to 28 → gauge % text |
| **Tree** (audit breakpoints) | **Full** ≥ 300 | chevron · orbit · short name · patch caption · exp pill · ready pill · count · road | |
| | **Trim** 240–299 | patch caption and expansion pill hidden | caption, pill |
| | **Compact** 200–239 | count → "82%"; complete nodes show no count (the closed ring says it) | fraction |
| | **Slim** 180–199 | no count; the ring alone carries progress; Ready pill → 6 px gold dot on the ring | count |
| | **Icon strip** (dragged below 150 → snaps to 56) | chevron column dropped (open/close with arrows or a click on the orbit) · orbit only; indent 6; the pane header shows the selected node's short name; hover gives name + count | names |
| | floor 180 (labelled) / 56 (strip) | PaneSplit | n/a |
| **Table** (ColumnPlan) | ≥ 640 | all columns; Name and Status stretch 3 : 2 | |
| | 560–639 | Rewards hidden | Rewards |
| | 480–559 | Exp hidden | Exp |
| | 400–479 | Job → icon only, then hidden | Job |
| | 360–399 | Level hidden (Lv moves into the tooltip); status shows the state word + ⓘ when the reason does not fit | Level, reason |
| | 320–359 (our addition) | **two-line rows** (44 px): moon · name · Lv pill / state word + reason (Mist) | columns become lines |
| | floor 320 | PaneSplit | n/a |
| **Detail** | **D1** ≥ 340 | full hero (rising moon r 22, title beside), requirements as a grid, 40 px rewards, full action row | |
| | **D2** 320–339 | moon r 16; chips wrap by segment; action extras → ⋯ | action extras |
| | **D3** 260–319 | banner crops to 2.6:1; the moon (r 14) sits on the banner edge *above* the title (audit: "moon above title"); the title spans the full width; requirements **label over value**; card captions on their own line; chain and journal path wrap on separators; rewards 32 px; path gutter 24; the primary action is full width | side-by-side layouts |
| | floor 260 | PaneSplit | n/a |
| **My blues** | ≥ 560 | one line: moon · name · kind icons · status · Flag · Reveal | |
| | 420–559 | two lines (status under the name); pills sized to content | status column |
| | 320–419 | kind icons ≤ 2 + "+n"; Flag/Reveal → one ⋯ menu (the audit's 420) | buttons |
| **Moonlit** | gallery | columns = ⌊w / 96⌋, minimum 2; names clamp to 2 lines | tile count per row |
| | table | the toolbar goes to two rows below 560; the table hides Confidence, then Kind, never State | Confidence, Kind |
| **Flight** | left list as the tree (bead ring replaces the orbit); main: banner height = min(160, 0.35 w); the map is hidden below 320 | | map, banner |
| **Characters** | section grid columns = ⌊w / 120⌋; the header stacks the gauge under the name below 360 | | gauge position |

The mockup's "Narrow" band shows the tree row at each tier, the icon strip beside the detail pane at D3 (260 px), the table's two-line rows and My blues at 380 px.

---

## 9. Motion

The design has only a few moments, each triggered and each short. There are no ambient loops (the ones in 1.1 are already static). Every one goes through `Motion` and is **instant under Reduce motion** (setting, or Windows' "Show animations" off). Nothing animates layout height.

| Moment | What | Values | Priority |
|---|---|---|---|
| **Moonrise** | When the selected quest changes, the hero moon rises 6 px onto the banner edge while fading in; the title fades 80 ms later | 220 ms ease-out (`MotionMath` k 12); alpha 0 → 1 | **Should** |
| **Orbit fill** | When a node's count changes, the arc and the bead travel from the old fraction to the new one (exists as `Motion.Gauge`, now on the orbit) | 500 ms ease-out | **Must** (exists) |
| **Road glint** | When a quest visible in the table becomes Completed live: one soft highlight (MoonHigh @ 0.5, 24 px wide, gradient) runs once along its tree node's road, and the table row's stripe flashes MoonHigh | 600 ms, once | **Could** |
| **Station change** | On a tab switch, the lit segment of the rail thread slides from the old station to the new one | 160 ms | **Could** |
| **Reveal pulse** | Unchanged (1.1) | 900 ms | exists |

Rejected: star twinkle, breathing Ready halos, parallax banners, animated gradients (distracting mid-fight and a cost every frame).

---

## 10. Accessibility

### 10.1 Contrast

Computed with the WCAG 2.x formula (script in the session scratchpad; the numbers match the 1.1 table for the old tokens).

| Pair | Ratio | Verdict |
|---|---|---|
| Silver on Night / NightTop / Abyss | 14.2 / 13.1 / 15.3 | AAA |
| Mist on Night / NightTop / Raised | 8.7 / 8.0 / 7.3 | AAA; eyebrows and counts use it |
| Dusk on Night / NightTop | 5.1 / 4.7 | AA; patch captions only on Night/NightTop, never on hover |
| Moon on Abyss (rail active icon, badge) | 13.3 | AAA |
| Gilt on Night / Raised | 5.7 / 4.8 | ornament only (≥ 3:1 for graphics even at alpha 0.8 over Night: ≈ 4.3) |
| Gilt vs Moon | 2.2 | ornament is clearly *not* the gold signal; in greyscale Gilt ≈ Dusk (L 0.27 vs 0.24) |
| Tide on Night / Raised | 5.7 / 4.8 | AA for text (My blues "unlock" labels, Flight's field line) |
| Moon bead (MoonHigh) on its Night disc | 16.8 | visible at any fraction |
| OrbitTrack (VeilLine @ 0.55) on Night | ≈ 2.0 | the track is context; the arc (Moon, 12.5) carries the value, and the count/percentage repeats it in text |
| Title/text over banners | ≥ 7:1 guaranteed by `HeroScrim` 0.94 + `HeroSideScrim` 0.55 in the text region (worst case: a white banner → effective background ≤ `#2A2F3F`) | AA |

### 10.2 High contrast (existing `GlyphPaletteKind.HighContrast`)

With the high-contrast palette on:
- Flair drops to **Quiet**: no star fields, no pane gradient, no hero side scrim (the bottom scrim goes to 0.97).
- Ornament lines use VeilLine at full alpha (3.2:1) instead of Gilt at partial alpha, so structure is kept and shimmer is removed.
- Official icons get a 1 px Silver keyline (`AddRect` / `AddCircle`), so dark tribe tiles do not melt into Night.
- Orbit arcs go to 3 px with a 1 px Night gap to the track.
- The road's walked part becomes solid Moon, and the unwalked part becomes VeilLine.

These are all pure draw-parameter switches read from `Theme.Glyphs.IsHighContrast`.

### 10.3 Colour-blind safety

- Identity (icons) never relies on hue alone: official icons differ in shape (tribe emblems, class silhouettes, city crests), and the six expansion rings differ in hue *and* in their position in the list, which is always story order.
- Progress is carried three ways at once: arc length, the bead's position and the count or percentage text. The road adds a fourth (length).
- The eight quest states keep the v2.1 shape channel (silhouette + mark). The table stripe keeps its pattern. The rising moon is always paired with the spelled-out state line.
- Gold vs Gilt is a luminance difference (2.2:1), not a hue difference, so it survives deuteranopia and protanopia.
- Tide (blue) vs Moon (gold) is the safest pair for red-green deficiencies, and it is the only hue pair the design adds.

### 10.4 Focus and input

- Stations, tree rows, table rows, chips, the ⋯ overflow and the gallery tiles are all real items (`InvisibleButton` / `Selectable` / `TreeNodeEx`), so keyboard and gamepad navigation reach them, and `Chrome.FocusRing` draws on focus.
- Every tier keeps the minimum target (`UiMetrics.MinTarget`); the icon-tier tree rows are 32 px.
- Tooltips carry what a tier hides (full name, count, reason), so nothing becomes reachable only at wide widths.

---

## 11. The pair critique (how the decisions were made)

Each area went through at least two rounds. Mirei proposes and Theo critiques for usability and for whether ImGui can build it.

**Concept.**
- R1. Mirei: seigaiha waves and cherry blossom sprigs as the "Japanese moon" texture. Theo: it is décor anyone could paste on; it says nothing about quests, and a pattern behind a 5,000-row table is noise.
- R2. Mirei: then the literal meaning of the name, the moon's road on water: the walked path is lit, the rest is dark water. Theo: that is also a data encoding (length = progress), so it earns its pixels. **Adopted.**

**Palette.**
- R1. Mirei: gold filigree on every divider and corner. Theo: 1.1's gold discipline says gold = act now; gold ornament everywhere teaches the eye to ignore gold.
- R2. Mirei: a darker brass (Gilt, 2.2:1 below Moon, greyscale ≈ Dusk) for anything decorative, and Moon kept for signals. Theo accepts, with the condition that Gilt is never text and never a fill over 4 px. A second round on hue: Mirei wanted a violet for "night". Theo: a third hue competes with state colours (Eclipse is red-violet). **Tide blue was chosen instead**; it already has a meaning in the game (blue unlock quests).

**Type.**
- R1. Mirei: Jupiter for everything, including body. Theo: the game fonts are pre-baked bitmaps at fixed sizes; body text at arbitrary UI scales would blur, and quest names in JP/KO/ZH clients need full coverage.
- R2. Mirei: game fonts only for four small roles, with the body on Dalamud's font. Theo: at most two roles per row, eyebrows Latin-only with a Caption fallback in other languages, and titles with CJK glyphs fall back to Axis. **Adopted.**

**Tree icons and progress.**
- R1. Mirei: official icon on the left *and* the 1.1 moon gauge on the right. Theo: two circles per row, and screenshot 4 shows why the moon gauges do not identify anything: fifteen identical full moons.
- R2. Mirei: put the icon *inside* the ring, making it an orbit with a moon bead at the arc's head. Theo: one element carrying two meanings, with small fractions now visible as a bead. He also asked for a length encoding for cross-row comparison, which **led to the road under each row** (replacing the 44 px mini bar). **Adopted.**
- R3 (siblings). Theo: most MSQ and sidequest genres share one generic icon (061411/061412), so the same icon repeated eight times identifies nothing. Mirei: use the expansion ring for chapters (its colour is the grouping), city crests for regional sidequests, and original glyphs only where the game has nothing. Rule: "first candidate distinct from siblings".

**Short names.**
- R1. Theo: abbreviate ("Post-ShB II"). Mirei: abbreviations read like a spreadsheet.
- R2. Mirei found that the game already names each chapter (`JournalGenre`: "The Voyage Home", "Dark Reprise") and proposed those as the labels. Theo: players think in patches, so add the patch range as a caption at wide widths.
- R3, after the audit's measurements came in. Theo: the audit's suffix rule gives "Post-Shadowbringers II", which is almost as short (the widest label anywhere is 297 px) and is the name players search the wiki for. "Dark Reprise" makes a player stop and translate. Mirei agrees for the tree. The chapter names move to the tooltip's second line and the hero's breadcrumb chip, where there is room for flavour. **Adopted: the audit's `JournalNames.Short` + our patch caption.**

**Rail.**
- R1. Mirei: vertical text labels down a narrow rail, like a scroll. Theo: ImGui cannot rotate text (vertex hacks break on atlas rebuilds). Also considered: horizontal tabs over the tree (the game's own journal); rejected because five labels do not fit in 240 px.
- R2. Mirei: 64 px stations with TrumpGothic labels under the icons, strung on a thread, the crest on top. Theo: then use the space the rail wasted: the overall gauge, help and settings at the foot, and a compact 44 px mode. **Adopted.**
- R3, against the audit's option A (44 px icons, a chevron to expand to 136). Theo: A's expanded state brings back the 136 px the owner called waste, and the collapsed state hides five labels that new players need; labels *under* icons cost 20 px, not 92. The audit's objection to filling the rail (option C) is that it duplicates things shown elsewhere; the foot block *moves* the gauge, help and settings, it does not copy them. Mirei keeps the stations. **Default 64 px stations; 44 px compact as the audit's A, automatic below 1,040 px; the freed width goes to the tree as the audit asks (300 here, 280 in the audit).**

**Detail hero.**
- R1. Mirei: a full-bleed banner with the title in Jupiter over it and a big state pill. Theo: at 220 px the pill wraps and the title over a bright banner is unreadable.
- R2. Mirei: the moon *rises* on the banner's bottom edge and replaces the pill; the title moves below the banner; the banner is framed with corner marks. Theo: good at every width. Add a side scrim for bright art, a fallback chain before the procedural sky, and chips that wrap whole. **Adopted.**

**Narrow widths.**
- R1. Theo: clamp the existing layout table's columns with an internal `TableSetColumnWidth` call; tree floor 88 with an icon tier. Mirei: the icon tier is the tree's best narrow form, because the orbits stay readable at 56 px.
- R2, with the audit. The audit shows the table cannot hold a floor at all (panes shrink to ~4 px, widths persist in imgui.ini with no reset) and that the table's fixed 276 px Name column is the real cause of "Blocked ·". Theo adopts `PaneSplit`, the floors (tree 180, centre 320, detail 260) and `ColumnPlan` (Name stretch) as specified. Mirei keeps the icon strip as a *snap* target: dragging the tree below 150 snaps it to 56 px of orbits instead of shutting it (audit: "snaps shut below 120"). A shut tree hides which scope the table shows; the strip keeps navigation one click away. **Adopted, with the snap-to-strip difference noted in §15.**

**Cards.**
- R1. Mirei: remove the rounded cards (they are the "generic dashboard" look). Theo: the cards separated sections; what replaces the grouping?
- R2. Eyebrow + sigil + fading rule as the header, with the moon-road divider between the hero and the sections. Theo: it also gives back 20 px of padding at narrow widths. **Adopted.**

**Flight.**
- R1. Mirei: zone loading images as thumbnails in every left-list row. Theo: forty full-screen images decoded is hundreds of MB.
- R2. The image goes only in the selected zone's banner; the list gets the bead ring (countable currents). The zone map with pins is a Could, because the coordinate maths exists but the texture is large. **Adopted.**

**Motion.**
- R1. Mirei: twinkling stars and breathing halos. Theo: the window stays open mid-fight; ambient motion pulls the eye away from the game, and it costs every frame.
- R2. Five triggered moments, each under a second, all off under Reduce motion. **Adopted.**

**Game UI parts (uld).**
- R1. Mirei: use the game's own window frames and ornamental corners from the uld textures, as some plugins do. Theo: uld parts are sprite sheets whose coordinates change with the patch and with the player's chosen UI theme (Dark, Light, Classic FF, Clear Blue), and they clash with the Night palette.
- R2. **Original corner marks and dividers** in the same spirit; uld is rejected (see §12).

---

## 12. Deliberately rejected

| Idea | Why not |
|---|---|
| Game window frames and corners from uld textures | Coordinates drift with patches and the player's UI theme; it clashes with the Night palette; maintenance every patch |
| Rotated vertical labels on the rail | ImGui has no text rotation; vertex rotation hacks break on font atlas rebuilds |
| Horizontal tabs above the tree | Five labels do not fit in 240 px; icon-only inactive tabs hide meaning; it costs the tree's height |
| Seigaiha, cherry blossoms, paper or noise textures | Generic "Japanese" décor with no data meaning; noise reads as dirt at 1080p |
| Gold filigree | Breaks the gold = act-now discipline |
| Rounded cards on every section | The generic dashboard look; costs 20 px of padding at narrow widths |
| Colour-coding tree rows by expansion | Seven hues fight gold; the official expansion rings already carry the colour |
| Custom art for all 249 genres | Loses recognition from the game's own Journal; maintenance every patch |
| Jupiter or AXIS as the body font | Bitmap sizes blur at arbitrary scales; CJK coverage risk |
| Zone images as Flight list thumbnails | Memory: one decoded loading image is several MB |
| Star twinkle, breathing halos, parallax | Ambient motion distracts mid-play and costs per frame |
| Abbreviated chapter names ("Post-ShB II") | The game's own chapter names are shorter *and* evocative; abbreviations read like a spreadsheet |
| Detail pane as a slide-over drawer on narrow windows | The column floors and tiers already keep it readable; a drawer adds modality and focus traps |

---

## 13. Implementation map

### 13.1 New assets

| Asset | Where | Source |
|---|---|---|
| `ornaments.png`, `ornaments@2x.png` (crest 40/80, corner mark 12/24, 10 glyphs 24/48, sigil 12/24; ~256 × 128 / 512 × 256) | `Tsukimichi/assets/ui/`, `EmbeddedResource` in the csproj | `ornaments/*.svg` rasterised by a script beside `docs/design/glyphs/render_preview.py` (Chrome headless or resvg; cairo is not installed on this machine) |
| No fonts | n/a | the game's TrumpGothic, Jupiter and MiedingerMid through `IFontAtlas` |
| No game art | n/a | read at runtime (icons, banners, loading images, maps) |

### 13.2 New code

| File | Responsibility |
|---|---|
| `Tsukimichi.Core/Ui/PaneTier.cs` (+ tests) | Tier from width with hysteresis |
| `Tsukimichi.Core/Ui/RowFit.cs` (+ tests) | Priority layout of a row's elements, span-based |
| `Tsukimichi.Core/Ui/JournalNames.cs` (+ tests) | The audit's `Short(name, parent, section, language)` and the patch-range caption |
| `Tsukimichi.Core/Ui/PaneLayout.cs` (+ tests), `Tsukimichi/Ui/PaneSplit.cs` | The audit's splitter model: floors, snap to the icon strip, persisted logical widths, reset |
| `Tsukimichi.Core/Ui/WordWrap.cs`, `Tsukimichi/Ui/TextFlow.cs` | The audit's word wrap (CJK fallback, cached) |
| `Tsukimichi.Core/Ui/NodeIcons.cs` (+ tests) | Candidate order and the "distinct from siblings" choice; the city-crest map |
| `Tsukimichi/GameData/NodeIconResolver.cs` | Lumina reads: `JournalGenre.Icon`, `BeastTribe.IconReputation`/`Icon`, `ExVersion.Icon`, ClassJob icons, quest-kind icons |
| `Tsukimichi/Ui/OrnamentAtlas.cs` | Loads the atlas once; `Draw(dl, Glyph, min, max, tint)`, `Corner(dl, min, max, corner)` |
| `Tsukimichi/Ui/Ornament.cs` | Primitive ornaments: `Divider`, `Rule`, `Sigil`, `Thread`, `Road`, `PaneGradient` |
| `Tsukimichi/Ui/Orbit.cs` | The orbit ring (icon + track + arc + bead), the bead ring (segments) |

### 13.3 Files that change

| File | Change |
|---|---|
| `Tsukimichi.Core/Ui/GlyphPalette.cs` | `GlyphTokens` gains the six hex constants |
| `Tsukimichi/Ui/Theme.cs` | Six tokens + U32s; `SurfaceColors.Deep`/`Ornament`; `Flair` read-through |
| `Tsukimichi/Ui/Typography.cs`, `Tsukimichi.Core/Ui/TypeScale.cs` | Eyebrow, Title and Numeral roles |
| `Tsukimichi/Ui/Chrome.cs` | `Section(...)` (the header treatment), `FlowChips(...)`, `Overflow(...)` (the ⋯ popup) |
| `Tsukimichi/Ui/TabStrip.cs`, `Tsukimichi.Core/Ui/LayoutBudgets.cs`, `ScaleMetrics.cs` | Stations, crest, thread, foot block; `RailLogical` 64, `TabRowLogical` 54; compact mode; `MinWindowSize` shrinks |
| `Tsukimichi/Ui/MainWindow.cs` | The body table becomes `PaneSplit`; pane gradient; the toolbar's right cluster moves to the rail foot; the status bar loses its gauge |
| `Tsukimichi/Ui/TreePane.cs` | Node icon, short name, orbit, road, `RowFit`, tiers, header line, dividers between blocks |
| `Tsukimichi/Ui/TablePane.cs`, `QueryRunner.cs` | Pane title, header styling, disambiguators, genre icons, status cell rule, two-line tier |
| `Tsukimichi/Ui/DetailPane.cs` (+ `.Journal.cs`) | Hero (rising moon, frame, fallback chain, chips, state line), open sections, tiers, action overflow |
| `Tsukimichi/Ui/PathChart.cs` | Narrow gutter at D3 |
| `Tsukimichi/Ui/PlanPane.cs` | Two-line rows, kind icons, icon buttons, tiers |
| `Tsukimichi/Ui/MoonlitPane.cs` | Kind icons, gallery view |
| `Tsukimichi/Ui/FlightPane.cs` | Zone banner, bead rings, map with pins (Could) |
| `Tsukimichi/Ui/CharactersPane.cs` | Header frame, section orbit grid |
| `Tsukimichi/Ui/ConfigWindow.cs`, `Tsukimichi/Config/Configuration.cs` | `Flair`, `GameHeadingFonts`, `CompactRail`; Look group with previews |

### 13.4 Order of work (each step shippable)

1. **Fix what is broken (Must).** `PaneSplit` with floors, `PaneTier`, `RowFit`, `ColumnPlan` (Name stretch), `TextFlow`, the table status rule, My blues two-line rows, detail D1–D3 stacking, action overflow, and `JournalNames.Short`. This is the audit's fix list. There is no new art yet, but every narrow-width complaint is gone.
2. **Identity (Must).** Node icon resolver, orbit ring, road, the new rail (stations, foot block, compact mode), and tokens.
3. **Character (Should).** Type roles, section header treatment (cards → open sections), hero redesign (rising moon, frame, fallback chain), ornament atlas (crest, corners, glyphs), dividers, pane gradient.
4. **Panes (Should).** Moonlit gallery, Flight zone banner and bead rings, Characters header and section grid, the Settings Look group.
5. **Polish (Could).** Moonrise, road glint, station slide, the rail star field, the Flight map with pins, Characters' tribe/GC row, the Ready-row road line.

Test at UiScale 0.9 / 1.15 / 1.6 and IconScale 0.8 / 1.25 / 2.0, with the Standard and High-contrast palettes, Flair Full / Quiet / Plain, and the column widths of screenshots 6–8 (tree ≈ 110 px, detail ≈ 220 px, My blues ≈ 520 px).

---

## 14. About the mockup

`mockup.html` shows:
- **The main window at 1400 × 870** (scrolls sideways on small screens): the 64 px rail with the crest, stations on the thread, a badge and the foot gauge; the tree with orbit icons, short names, patch captions, roads, a divider between blocks, and the virtual nodes; the table scoped to *Seventh Umbral Era* with the pane title, the header treatment and all four common states; and the detail pane for *Lady of the Vortex* (Lv 44, Cid, Gridania; after *Better Late than Never*; leads to *Reclamation*) with the framed banner placeholder, the rising moon, chips, open sections, the duty banner thumbnail, the star-chart path and the action bar with overflow.
- **Narrow widths**: the tree row at each tier (320 / 260 / 220 / 190 / icon strip), the 56 px icon strip beside the detail pane at D3 (260 px), the table's two-line rows with the "Close to Home" disambiguation, and My blues at 380 px.
- **Flight** (*The Sea of Clouds*, 4 of 5, with *Search and Rescue* still blocked) and **Moonlit** (mounts and minions found only in quests) panels.
- **The ornament kit**, drawn from the same shapes as `ornaments/`.

Quest names, levels, issuers, places, gil and chains come from the plugin's own quest data (`scratchpad/quests.json`, `Tsukimichi/Data/unique_quests.json`). The per-character counts are the owner's own from the screenshots. Official game icons are drawn as simple stand-ins (same shape family and colour, clearly not the game art). Banners and zone art are labelled placeholders, and nothing is hotlinked.

---

## 15. Agreement with the UI audit (`scratchpad/ui-audit/report.md`)

**Adopted as specified:**
- Icon sources and ids. EventIconType markers for sections (`071201`, `071221`, `071341`); `BeastTribe.IconReputation`; class/job/role; the Grand Company and ContentType tiles; `ExVersion.Icon` for MSQ and regional nodes.
- The gap list for original glyphs. Region crests and festival portraits are not used as row icons.
- Progress as a ring around the icon; 0 % draws the icon at 60 % alpha; Ready is a gold dot on the ring.
- `JournalNames.Short` and its examples. Full names stay in tooltips and scope labels.
- `PaneSplit` + `PaneLayout.Solve` replace the body table. Floors: tree 180, centre 320, detail 260. Widths are stored in logical units, with a double-click reset.
- `RowFit` with the tree breakpoints 300 / 240 / 200 / 180.
- `ColumnPlan`: Name becomes a stretch column (weight 3, min 140), and columns hide in the order Rewards → Exp → Job → Level.
- Detail: label over value below 320, the moon above the title, word wrapping (never letters), captions on their own line, and chain and path wrapping on separators.
- My blues: Flag and Reveal go to "…" below 420, and the status never cuts the state word.
- Moonlit: the toolbar goes to two rows, and Confidence then Kind hide, never State.
- The helpers `EllipsisText`, `StatusText`, `LabelValue`, `TextFlow`/`WordWrap` and `SameLineOrWrap`, with breakpoints in `LayoutBudgets`.

**Where we differ, and why:**

| Point | Audit | This proposal | Why |
|---|---|---|---|
| Rail default | A: 44 px icon rail, a chevron expands to the labelled 136 px rail | **64 px labelled stations** by default; 44 px compact (the audit's A without the 136 px state) automatically below 1,040 px or by setting | Labels under icons cost 20 px, not 92. The expanded state brings back the waste the owner named. The rail foot *moves* the gauge, help and settings rather than duplicating them (critique log, Rail R3). |
| Tree default width | 280 | **300** | Our 64 px rail frees 72 px; 300 is the tree's Full tier, so the default window shows whole names with their counts. The centre still gains 12 px over today. |
| Tree below its floor | Snaps shut below 120 | **Snaps to a 56 px icon strip** of orbits below 150; the header names the selection; a double-click on the handle restores it | A shut tree hides which scope the table shows. The orbits stay readable at 56 px, so navigation stays one click away. |
| Table below 360 | Columns hide down to Name + Status | Same, plus **two-line rows** from 360 down to the 320 floor | At 320 the Status stretch gets ~120 px, too little for a reason; a second line gives it the full width. |
| My blues two-line | Below ~300 | **Below 560** | The reason ("after: Coming to Ul'dah") is the most useful part of this tab. In one line it competes with name and pills, so it gets its own line early. |
| Chronicles series icons | Original glyphs per series | `ExVersion.Icon` now, per-series glyphs as **Could** | After shortening, the series names are distinctive ("Eden", "Omega"). Sixteen bespoke glyphs cost more than they identify, and the expansion ring still groups them. |
