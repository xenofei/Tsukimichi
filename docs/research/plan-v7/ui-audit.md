# Plan v7: UI audit (owner points 1, 2, 3, 6, 8, 9)

Research only, against v1.13.0 (`09fc945`). No product code was changed. Line numbers are for that commit. Screenshot measurements come from `owner-quest-pane.png` (453 × 753) and `owner-filter-menu.png` (510 × 1070), read pixel by pixel. I did not run the game, so anything that only a live client can confirm is marked **verify in game**.

---

## 1. Quest-pane section headings look too small

### How they are drawn

- The detail pane's cards (Requirements, Rewards, Unlocks, Hand-in, Moonlit, Path, Giver, Journal text, Duties) all open through `DetailPane.BeginSection` (`Tsukimichi/Ui/DetailPane.Hero.cs:697-708`). That calls `Chrome.BeginCard(..., eyebrow: true)` (`Chrome.cs:97`), then `Chrome.CardContent` (`Chrome.cs:125-175`).
- At **Full**, the card is `CardFrame.BrassCorners` and the title goes through `Chrome.EyebrowTitle` (`Chrome.cs:183-209`): `Typography.Eyebrow(title)` in `Surface.OrnamentHigh` (gilt), upper-cased by `SectionHeading.Label`.
- At **Quiet and Plain**, the title is `ImGui.TextUnformatted` in the body font (1.0×), primary ink, sentence case (`Chrome.cs:140-155`).
- Open sections and pane headings (Journal header, Moonlit, Characters, Flight) use `SectionHeading.DrawLine` (`SectionHeading.cs:75-162`) with the same role through `TitleRole` (`SectionHeading.cs:224-225`): Eyebrow at Full, Caption at Quiet in non-English, body otherwise.

### Type maths at Full (`Tsukimichi.Core/Ui/TypeScale.cs`, `Tsukimichi/Ui/Typography.cs`)

- Eyebrow = TrumpGothic at `EyebrowFactor` **1.45 ×** the body size (`TypeScale.cs:68`).
- The game font is picked per UI-scale bucket (`TypeScale.cs:111`, `NearestOf` with `MaxUpscale` 1.1, `:131-136`). At the default body size (16–17 px) the target is 23–24.6 px, which picks **TrumpGothic 18.4** (24.5 px). The scope then sets the window font scale so it draws at exactly 1.45 × body (`Typography.cs:362-388`, `Scope` ctor `:478-491`).
- Text size (1.13 U7) and UI scale both carry into this. `Typography.Update` multiplies the base px by `UiMetrics.TextScale` (`Typography.cs:155`), and the bucket comes from `UiMetrics.UiScale` (`:142`). So headings already follow both settings. The problem is the face and the ink, not the scaling path.

### Measured on the owner's screenshot

| Glyph | Cap height | Letter width | Stroke |
|---|---|---|---|
| "REWARDS" (TrumpGothic) | 13–14 px | ≈ 5 px | 1 px |
| "855 EXP" (body) | 10 px | ≈ 6 px | 1–2 px |

### Root cause

- The heading is about 1.35× taller than body caps, but TrumpGothic is a very condensed, thin display face. It is drawn in muted gilt at 1 px stroke on a busy brass card, so it reads smaller and fainter than the body text under it.
- The owner suspects "Game fonts for headings", but that setting is not the cause, and turning it **off makes it worse**. With it off, the Eyebrow falls back to the **Caption** role, 0.85 × body with a 12 px floor (`Typography.cs:382-387`, `TypeScale.cs:47`). The headings then become smaller than the body text.
- So at every combination of Decoration level and setting, a section heading is at most about body size:

| Level | Game fonts on | Game fonts off |
|---|---|---|
| Full | condensed 1.45× | caption 0.85× |
| Quiet / Plain | body 1.0× | body 1.0× |

### Proposed fix (UI-1, S–M)

1. Add a dedicated **Section** role, separate from the table and rail Eyebrow so each can be tuned on its own: `TypeScale.SectionFactor = 1.75f`.
   - At the default body (17 px) the target is 29.75 px. That picks **TrumpGothic 23** (30.7 px) drawn at 0.97: crisp, a downscale, never blurred.
   - At UI scale 0.9 it still picks TG 23 (about 0.87×). At 1.3 and 1.6 it moves to TG 34 (45.3 px). The bucket logic already handles this.
2. Fix the fallback. When game fonts are off, a glyph is missing (CJK), or the level is Quiet or Plain, a section heading should fall back to the **Display** handle at **1.15 × body** (Axis 14/18), not Caption 0.85×. Add `TypeRole.Section` with fallback `Display` scaled to 1.15. A heading must never be smaller than body text.
3. Ink: use `OrnamentHigh` at Full (already), plus a 1 px dark under-stroke, or draw twice at a 0.5 px offset for weight, the same way `Chrome.OutlinedEllipsisAt` outlines. At Quiet and Plain, keep primary ink.
4. Optional tracking: TrumpGothic tracked by +0.5 px per glyph (draw per glyph from the cached string) reads much better in caps. Do this only if (1) is not enough. It is per-frame work, so cache the advances.
5. Scaling stays automatic. Everything is `body px × factor`, and body px already includes Text size × UI scale. `HeadingLayout` grows the line box from the measured line (`HeadingLayout.cs:48`), and `CardCaption` centres the caption on the title rect (`DetailPane.cs:353-375`), so captions stay aligned.

| Level | Proposed heading size at the default text size (cap height in brackets) |
|---|---|
| Full | ≈ 30 px TrumpGothic (caps ≈ 17 px) |
| Quiet | 1.15 × body Axis, sentence case (≈ 19.5 px) |
| Plain | 1.15 × body, primary ink |

6. Tests: `TypeScaleTests` gains a Section-role bucket table, and `HeadingLayoutTests` is unchanged.
7. Re-check the hand-in, Unlocks "+N more" and Path headers at a 300 px pane. A 30 px heading with a caption ("2 of 3 unmet") wraps sooner, but `CardCaption` already wraps.

---

## 2. The filter drawer

Code: `MainWindow.Frame.cs:604-731` (`DrawDrawer`, `DrawDrawerHeader`, `DrawDrawerEdge`) and `FilterPanel.Draw` (`FilterPanel.cs:117-158`).

### 2a. Text from the tree shows at the sides, and "JOURNAL" over "Filters"

**What the pixels show.** The drawer is drawn as an outer child `##filterDrawer` (opaque `Raised` fill, 10 × 8 padding, border) holding an inner child `##filterDrawerBody` (`MainWindow.Frame.cs:638-657`). In the screenshot:

- Everything the **outer** child owns (its 10 px left and right gutters, and the header strip down to y ≈ 35) has tree content painted **over** it:
  - the tree header "✦ JOURNAL ───" over "Filters";
  - the All-quests row wash and its moon at y 28–40;
  - a 1 px tree glyph at x ≈ 3 on every row;
  - the last digit of each row's count at x ≈ 495.
- The gutter colour stays flat (30, 36, 55) down the whole height, so the outer fill is there and the tree's draw list sits on top of it.
- The **inner** body child is fully opaque over the tree. No row labels show through it.

So the effective order is **outer drawer < `##left` tree < inner body**. ImGui normally draws sibling children in begin order, and the drawer begins last, after `DrawStatusBar` (`MainWindow.cs:694-697`). Something reorders them: the drawer outer frame lands under `##left`. The likely cause is how the binding or Dalamud builds `DC.ChildWindows`. **Verify in game** with `/xldev` › ImGui Metrics › Windows › main window › child order. Whatever the mechanism, the drawer relies on sibling-child z-order, which this shows is not dependable.

**Fix (UI-2a, S):**

1. Draw the whole drawer, header included, inside the one occluding child. Make the outer child zero-padding and borderless, or drop it and draw the frame and edge on the inner child's draw list, so no area of the drawer belongs to the window that loses the z-fight.
2. Belt and braces: while the drawer is open it always covers the tree column, because its width is `max(treeWidth, 300 px)` (`MainWindow.Frame.cs:626`). So `TreePane.Draw` can skip drawing rows (keep layout and state, push an empty clip rect) while `ui.FilterPanelOpen && Tab == Journal`. That removes the bleed whatever the z-order. It also stops the gutters passing hover and clicks to tree rows underneath.
3. The same pattern is used by the notice dock (`MainWindow.Frame.cs:262-284`, one child with content in it, so it is safe). Also check the drawer where the tree is narrower than 300 px: it then overlaps `##center` (the table), which has the same hazard.

### 2b. Large empty grey area when Advanced is closed

- **Root cause:** the drawer is always `bodyHeight` tall (`MainWindow.Frame.cs:626`). Collapsed, `FilterPanel.Draw` is about 7 rows (≈ 210 px), so about 80% of a 1070 px drawer is empty `Raised` fill.
- **Fix (UI-2b, S):** size it to the content.
  - Record the inner content height each frame (`ImGui.GetCursorPosY()` after `filterPanel.Draw`, plus the header and padding).
  - Next frame, open at `min(content, bodyHeight)`, scrolling only past that.
  - Ease the height change over `MotionTokens` (0.16 s, instant under Reduce motion), so opening Advanced grows the sheet instead of jumping.
  - Round the bottom corners and keep the edge shadow along the bottom too.

### 2c. The "Filters" title is barely readable

- **Root causes:**
  - It is body-size text in `TextSecondary` (`MainWindow.Frame.cs:684-688`), with no heading role.
  - It is overdrawn by the tree's "✦ JOURNAL" heading (2a), which sits on exactly the same line, because the drawer's top is `leftMin`, the tree's top.
- **Fix (UI-2c, S):** once 2a is done, draw the header with `SectionHeading.DrawLine(Strings.Filters, activeCountCaption, …, reserve: 2 × button)`. That gives the sigil, the Section role (UI-1) and the brass rule. Add a caption such as "3 on" from `FilterBadge.Count(ui.Filters)`. Pin and × become `Chrome.IconButtonRound`, as on the rail foot.

### 2d. Aesthetics: what reads as odd today

- **Quick views:** this is a dim `TextDisabled` label, but the views themselves live on the toolbar. Under it sits only an unlabelled slider "7 days" and then "Stalled after" *after* the slider (`FilterPanel.cs:237-253`). The order reads backwards, and the heading names something that is not there.
- **Overrides:** `SmallButton`s render as bare text beside each checkbox (`FilterPanel.cs:589-597`), so they look like labels, not buttons. Under the Night theme the checkboxes are near-black squares with low contrast.
- **Reset:** a bare `SmallButton` that clears every filter *and the search* (`FilterPanel.cs:150-155`, `ResetAll`). It has no confirmation or undo, which goes against the owner's "safety on destructive clicks".
- **Advanced:** a stock `CollapsingHeader` band in Dalamud style. It is the only stock-looking element in the window.

**Redesign direction (UI-2d, M):** a "sheet" that drops from under the toolbar over the tree column, content-sized.

- **Header:** "✦ FILTERS ─── 3 on", with round pin and close buttons.
- **Group "Show":** three switch rows (Hide completed, Available now, Pinned first), each 32 px tall with the label on the left and a toggle on the right. The per-category overrides become a small FontAwesome `SlidersH` icon button at the row's end, with a dot badge when any override is set.
- **Group "Stalled view":** "Stalled after [ 7 days ▾ ]" in reading order, with a one-line caption: "used by the Stalled quick view on the toolbar".
- **"More filters":** a disclosure row with a chevron that rotates (eased), and a count of engaged advanced filters on the right. Inside, the existing groups each get a small eyebrow heading (States, Expansions, Added in, Level, Job, Rewards) on hairline rules, at the spacious settings rhythm the owner likes.
- **Footer:** "Reset filters" as a secondary pill button on the left, disabled when nothing is engaged, with an **Undo toast** (`UndoToast` already exists) rather than a confirm modal. "Done" sits on the right (closes, same as ×).
- **Surface:** `Chrome.CardSurface` at Full (brass and corner marks, drop shadow), tonal at Quiet, flat with a 1 px line at Plain. The drawer's right brass edge (`DrawDrawerEdge`) stays.
- **Motion:** keep the existing 0.16 s fade. Add a 6 px downward settle on open, and nothing under Reduce motion.

---

## 3. Journal column headers are small by default

- **Code:** `TablePane.DrawHeaders` (`TablePane.cs:1106-1170`), role from `HeaderRole` (`TablePane.cs:1180-1181`).
  - Full: Eyebrow (TrumpGothic 1.45×) in `TextTertiary`.
  - Quiet: **Caption 0.85×** in `TextTertiary`.
  - Plain: Caption 0.85× in `TextSecondary` (`:1132-1134`).
- **Row height:** the row is begun with `ImGui.TableNextRow(ImGuiTableRowFlags.Headers)` with **no min height** (`TablePane.cs:1111`), so it is just the label line plus 2 × `CellPadding.Y`, about 20–22 px. The data rows are 34 / 30 / 24 px (`ScaleMetrics.TableRowContent`).
- **Root cause:** the headers are the smallest and dimmest text in the table: caption size (or condensed TrumpGothic), tertiary ink, and a row shorter than any data row.
- **Proposed default (UI-3, S):**
  - **Size:** headers in **body size (1.0×)** at Quiet and Plain (no longer Caption). At Full, use the Eyebrow at **1.6×** (TG 23 at ≈ 27 px), or simply the Section role at 1.6 if UI-1 lands.
  - **Ink:** `TextSecondary` at every level; the sorted column keeps primary or gilt.
  - **Height:** `ImGui.TableNextRow(ImGuiTableRowFlags.Headers, UiMetrics.Px(Full ? 32 : Quiet ? 28 : 24))`.
  - **Text size:** all of this follows Text size automatically through body px.
  - **Risk:** fixed columns are sized to at least their header label (`LayoutBudgets.FixedColumnWidth`, `MaxFixedColumnLogical` 120). Bigger labels widen Level, Job and Exp. Re-run `LayoutBudgetTests` for every language, and keep Eyebrow capitals, which are narrow, at Full.

---

## 4. The left rail's tab icons

**Code:** `TabStrip.cs` (rail), sizes in `Tsukimichi.Core/Ui/LayoutBudgets.cs:59-104`, placement `LayoutBudgets.PlaceRail` (`:250-279`).

### Current sizes (logical px)

| Element | Size | Code |
|---|---|---|
| Rail width | 64 (compact 44) | `ScaleMetrics.RailLogical` |
| Station height | 54 (min 44; compact 40 / 30) | `LayoutBudgets.cs:59-68` |
| Icon box | **22** | `StationIconLogical`, `:71` |
| Label | **0.7 × body**, about 11–12 px, below the 12 px caption floor (`TypeScale.CaptionFloorPx`) | `RailLabelFraction`, `:77` |
| Crest | 40 | |
| Foot gauge | 36 | |
| Foot buttons | 26 | |

FontAwesome stand-ins (Characters, and the others while art loads) are drawn by `DrawIcon` at the icon font's own size, not fitted to the 22 px box (`TabStrip.cs:640-646`). Characters' "Users" glyph therefore draws at about 16 px next to 22 px art icons.

### Why the rail looks empty

- `PlaceRail` places five fixed 54 px stations under the crest and **anchors the foot to the bottom** (`footTop = max(stationsBottom + gap, height − pad − foot)`, `:265`).
- On a normal-height window, everything between the last station and the foot is empty sky. Only nine seeded stars sit in it (`RailStars = Generate(7, 9)`, `TabStrip.cs:47`, drawn at `:188-194`).

### The coloured lines

- These are the Moon Road **thread**: `DrawThread` (`TabStrip.cs:331-347`) draws a 1 px brass hairline at 0.35 alpha down the rail's centre, broken around each station.
- The active station also gets the **lit bar**: a 22 px MoonHigh → MoonDeep gradient bar with a bead (`DrawLit`, `:451-508`). On a tab change the bead travels along the thread (`StationKey`, `MotionTokens.Travel`).

### Proposed changes

**UI-4a (S): bigger icons that fill the rail.**

- `StationIconLogical` 22 → **30**. Labels at **0.8 × body** (≥ 12 px floor).
- Station height becomes elastic: `station = clamp((height − crest − foot − pads) / 5, 54, 84)`. The five stations then share the free height instead of leaving sky. `FitRail` already has the shrink path; add the grow path above `StationLogical`.
- Optional rail width 64 → 72 for 30 px icons with labels. Check `ScaleMetrics.MinWindowSize` and `PaneSplit` floors if you do.
- Size the FontAwesome stand-ins to the box: push `UiBuilder.IconFont` and `SetWindowFontScale(box / IconFont.FontSize)`, as `Typography.Icon` does (`Typography.cs:436-451`).
- Compact rail: icon 26, station 44–64 elastic.

**UI-4b (S): remove the thread.**

- Drop `DrawThread` (`TabStrip.cs:209-212` call). Replace the lit bar with a selection treatment that does not need a line:
  - a **soft moon-glow plate** behind the active station (rounded rect, `Moon` at 0.10 → 0 radial);
  - a 3 px gilt **notch on the rail's right edge**, which points at the pane it opens.
- Keep the bead only as the travelling notch. On a tab change the notch slides vertically between stations over `MotionTokens.Travel`, eased in and out. That is static layout with tasteful motion, no line.

**UI-4c (S–M): hover and select animation.**

- Today's hover is a 0.6-alpha `Hover` wash (`TabStrip.cs:386-389`), icon alpha 0.72 → 0.9, and label ink lerp. It is flat.
- Proposed, all driven by the existing `Motion.Hover` value with no new timers:
  - **hover:** the icon scales 1.0 → 1.08 around its centre, the label lifts 1 px, and a faint glow plate fades to 0.06;
  - **press:** the icon scales to 0.96 for 80 ms;
  - **select:** the glow plate eases to 0.10, the icon goes to full alpha with a 1-frame-free crossfade from idle tint, the notch slides in, and the orbit on the Journal station refills (already present).
- Under Reduce motion and at Plain: no scale, only ink and plate changes.

---

## 5. Missing icons (owner point 9)

The full list with sources is below, ordered by how visible each gap is. "verify" means the icon row id should be checked against the sheet.

### A. "A Pup No Longer" (66640 / 66641 / 66642)

The quest has two unlock sources and neither gets an icon.

1. **Curated SystemUnlock "PvP"** (`Tsukimichi/Data/curated/system_unlocks.json:108-122`). `QuestUnlocks.AddFeature` stores icon **0** for every feature row (`Tsukimichi.Core/Unlocks/QuestUnlocks.cs:498-515`). The row has no target id or reward, so the `UnlockIcon` resolver never runs (`DetailPane.Unlocks.cs:202-208`). The tile falls to `MoonGlyph.DrawVeiled` (`DetailPane.Unlocks.cs:287-290`). Moonlit shows the same entry with the PvP menu icon through `MoonlitKindIcons.SystemUnlockCommand` (`Tsukimichi.Core/Ui/PaneArt.cs:266-345`, MainCommand "PvP"), but the detail pane never uses that path.
2. **`Quest.InstanceContentUnlock`** → InstanceContent 40001 → ContentFinderCondition 121, which has **no name**. `CatalogMapper.MapRewards` emits a nameless `RewardKind.Instance` (`Tsukimichi.GameData/CatalogMapper.cs:456-468`), `DutyIndex` skips unnamed CFCs (`DutyIndex.cs:69-73`), and `QuestUnlocks` drops nameless rewards (`QuestUnlocks.cs:403-413`). The solo instance never gets a row.

### B. Findings

| # | What shows now | Where | Proper source | Effort |
|---|---|---|---|---|
| I1 | Veiled moon on every curated **feature** unlock (≈133 SystemUnlocks: PvP, Retainers, Hunts, Gold Saucer, …) | `QuestUnlocks.cs:510`; tile `DetailPane.Unlocks.cs:287` | `MoonlitKindIcons.SystemUnlockCommand(label)` → `MainCommand.Icon`, resolved in `AddFeature` (read MainCommand icons next to `AreaIcon`, `UnlockLinkReader.cs:129`) | S |
| I2 | Opens column hides every icon-0 unlock, so it undercounts | `TablePane.cs:2179-2180` (`ShowsOpens` requires `Icon != 0`) | Fixed by I1 / I4; or draw a generic kind glyph instead of hiding | S |
| I3 | Field-operation features (Eureka, Bozja, Delubrum, Occult Crescent) show the veiled moon | `UnlockTags.cs:60-61` → `AddFeature` | `ContentType.Icon` 26 / 29 / 38 (already in `NodeIcons.GenreContentType`, `NodeIcons.cs:124-130`) | S |
| I4 | Duty rows use `ContentType.Icon` only. When it is 0: veiled moon, or the generic Duty Finder icon | `UnlockLinkReader.cs:310-325`, `RewardArtIndex.cs:264-291`, `Plugin.cs:1058-1062` | `CFC.Icon` → `ContentType.Icon` → `ContentType.IconDutyFinder` → MainCommand Duty Finder | S |
| I5 | Nameless instance rewards are dropped (Pup case) | `CatalogMapper.cs:456-468`, `QuestUnlocks.cs:403-413` | Name from `CFC.TerritoryType.PlaceName`; icon from `CFC.ContentType` (PvP = ContentType 6, verify) | M |
| I6 | Title tiles in Rewards show the generic Achievements menu icon | `DetailPane.cs:1310-1314` passes `Source = string.Empty`, so `AchievementOf` returns 0 | Pass the real `UniqueRewardEntry` from `uniqueByQuest`; `Achievement.Icon` | S |
| I7 | Unresolved `Quest.Reward` slots render a blank tile (veiled moon, empty name, kind "Other") | `CatalogMapper.cs:414-417` → `DetailPane.cs:592-596` | Resolve the other row types, or skip nameless slots in `BuildRewards` | S |
| I8 | "855 EXP · 414 gil" is text only (visible in the owner's screenshot) | `DetailPane.Exp.cs:50`, `:61-75` | Gil: `Item#1.Icon` (65002, verify); EXP: 65001 (verify) | S |
| I9 | ≈50 Moonlit system unlocks with no menu match fall back to the PlanFallback glyph | `PaneArt.cs:266-302`, `:357-362`; `MoonlitPane.cs:1571-1576` | MainCommand rows (Grand Company, Challenge Log, Saddlebag); `ContentType.Icon` 9 / 31 / 36 / 6 for Treasure hunt / Ocean fishing / Island / Crystalline Conflict; BLU 62136; `SatisfactionNpc` icon for Custom deliveries (all verify) | S per label |
| I10 | Moonlit Hairstyle / Other use atlas glyphs | `PaneArt.cs:305-322` | Hairstyle: an Aesthetician item icon or a `CharaMakeCustomize.Icon` sample | S |
| I11 | Characters role-ladder rows show a blank square (`ImGui.Dummy`) | `CharactersPane.cs:2626`, `:1345-1350` | Role icons already in `NodeIcons.RoleGenreIcon` (62581 / 62582 / 62584 / 62586 / 62587) | S |
| I12 | Table Job column: empty icon slot for DoL, DoH, DoW/DoM, Multi, Any | `TablePane.cs:1980-1990`, `QueryRunner.cs:744-779` | `ContentType.Icon` 16 / 17 (`NodeIcons.LandContent` / `HandContent`); 62147 emblem for war/magic or multi | S |
| I13 | Characters › Grand Company and Allied societies tables are text only | `CharactersPane.cs:1394-1433` | `BeastTribe.Icon` (already read, `NodeIconResolver.cs:59-61`); GC seal item icons (verify) | S–M |
| I14 | Plan pane unlock-kind pills and filter chips are text only | `PlanPane.cs:714-727`, `:122-145` | `ContentType.Icon` 2 / 4 / 5 / 37 / 26; Job 62147; Flying 60033; System menu icon | M |
| I15 | In-game panels (offer, result, journal companion) list Moonlit rewards and unlocks as text | `GamePanelShell.cs:326-357`, `QuestResultPanel.cs:75-84` | `UnlockEntry.Icon`, `MoonlitIconResolver` (`QuestBrief` needs icon ids) | M |
| I16 | Detail › Duties (AutoDuty) section: names only | `DetailPane.Companions.cs:126-131` | CFC / ContentType icon | S |
| I17 | Requirements lines: text plus check or cross only | `DetailPane.Unmet.cs:252-276` | ClassJob 62100+id; `BeastTribe.Icon`; `Mount.Icon`; `Achievement.Icon`; ContentType | M |
| I18 | Characters › Collection rows and kind filter: names only | `CharactersPane.Collection.cs:418-421`, `:109-125` | `MoonlitIconResolver.Resolve`, `KindIcon` | S–M |
| I19 | Route window header is always FontAwesome MapSigns | `RouteWindow.cs:242-245` | Target kind's duty, reward or menu icon | M |
| I20 | Text-only buttons for actions that have icons in the detail toolbar (Flag, Teleport, Walk, Stop, Reveal, Copy, Route, Pin) | `TravelControls.cs:103`, `:124` (shared by Plan, Flight and Abandoned); `PlanPane.cs:233`, `:256`, `:441`, `:452`, `:739`, `:751`; `RouteWindow.cs:268`, `:377`, `:400`, `:525`, `:545`, `:575`; `TodoOverlay.cs:469`, `:483`; `FlightPane.cs:461`; `TonightCard.Stops.cs:93`; `CharactersPane.Abandoned.cs:47`, `:223`, `:238`; `CharactersPane.Planning.cs:181`; `CharactersPane.Route.cs:31` | FontAwesome as in `DetailPane.cs:830-843`; reuse `Chrome.ActionPill` (icon plus label, collapses to icon) | M total (S per site) |
| I21 | Unlocks "+N more" popup: names without the icons the rows show | `DetailPane.Unlocks.cs:481-486` | `row.Icon` | S |
| I22 | Plan expansion list and cards: text only (Flight uses expansion rings) | `PlanPane.cs:161-200` | `ExVersion` ring icons (`NodeIcons.FirstExpansionRingIcon`) | S |
| I23 | Rail Characters station uses FontAwesome Users, beside game and atlas art | `TabStrip.cs:61-68`, `:522-546` | MainCommand "Character" icon (verify), drawn at the box size | S |
| I24 | Area and world-map unlocks all share the Map menu icon | `UnlockLinkReader.cs:129` | No per-zone icon in the sheets; acceptable | — |

**UnlockKind coverage** (`Tsukimichi.Core/Plan/UnlockKind.cs`):

| Kind | Status |
|---|---|
| Dungeon, Trial, NormalRaid, AllianceRaid | OK through `ContentType.Icon` (veiled moon or menu fallback when it is 0) |
| FieldOperation | Curated labels have none (I3) |
| Job | OK |
| Society | No row |
| Flying | OK |
| System | 0 (I1) |
| Other | Nameless instances dropped (I5) |

The Plan pane draws no icon for any kind (I14).

**Intentional placeholders, keep:**

- the spoiler artwork placeholder (`ArtworkPlaceholder.cs`);
- the banner loading sky (`DetailPane.Hero.cs:545`);
- the icon-loading stand-in (`GameIcon.cs:107-120`).

---

## 6. The stars today (input for the designer)

### Generator: `Tsukimichi.Core/Ui/StarField.cs`

- **Algorithm:** a 31-bit LCG, `state = state × 1103515245 + 12345` masked to 31 bits (`:132-136`). For each star it draws three values: U, V, magnitude.
- **Placement:** U and V stay 3% inside the unit square (`0.03 + 0.94 × r`, `:76-77`).
- **Magnitude:** < 0.70 Faint, < 0.95 Small, else Bright, so **70 / 25 / 5%** (`:79`).
- **Seeding:**
  - Path chart bands: `expansion × 7919 + rowCount` (`:51`).
  - Fixed fields:
    - rail: `Generate(7, 9)`, 9 stars (`TabStrip.cs:47`);
    - tree sky: `Generate(31, 14)`, 14 stars (`TreePane.cs:312`);
    - table title band: `Generate(97, 7)`, 7 stars (`TablePane.cs:134`);
    - Settings preview: `Generate(53, 6)` (`ConfigWindow.General.cs:376`).
- **Density:** only the Path chart scales with area, at one star per **1400 logical px²**, clamped to **6–48** per band (`:42-63`, nominal width 324). The fixed fields have a fixed count, mapped by unit coordinates onto whatever rectangle they get. A tall tree sky therefore gets the same 14 stars as a short one, spread thinner.

### Renderer for the Full-only fields: `Ornament.Stars` (`Tsukimichi/Ui/Ornament.cs:191-244`)

| Tier | Shape | Alpha |
|---|---|---|
| Faint | 1 × 1 px rect | 0.16 |
| Small | r 1 px disc (8 segments) | 0.26 |
| Bright | 7 px plus-cross (arms 3.5 px, 0.7 px stroke) at 0.36, and an r 1.5 core | 0.50 (0.36 × 1.4) |

- **Colour:** cool white `#DDE6FF`. About one in six stars is warm `MoonHigh` (`(i × 7919) % 6 == 0`, `:225`).
- **Scaling:** sizes scale with `UiMetrics.Px(1)`.
- **Avoid rule:** stars are skipped inside an optional avoid rect plus 4 px.
- **Gate:** drawn only when `Theme.ShowStars` (`FlairRules.StarField` = Full only, and never under high contrast, which caps the level at Quiet, `Flair.cs:43-47`, `:73`).
- **Where:**
  - the rail sky between the last station and the foot (`TabStrip.cs:188-194`);
  - under the last tree node, at least 40 px tall, inset 8 px (`TreePane.cs:298-308`);
  - the table title band between the title and the count, 16 px clear of each (`TablePane.cs:1261-1267`);
  - the Settings Decoration preview.

### Path chart stars (`PathChart.cs:553-583`, drawn `:888-920`)

These are a separate renderer and they draw at **every** Decoration level. They are not gated by `ShowStars`, and they draw in `Surface.Text` ink, not the cool or warm tones.

| Tier | Shape | Alpha |
|---|---|---|
| Faint | 1 × 1 | 0.14 |
| Small | r 1 | 0.22 |
| Bright | r 1.5 at 0.32, plus a 2.5 px cross at 0.12 (cross dropped in dense layout) | 0.32 / 0.12 |

- **Placement rules** (`Excluded`, `:595-623`): no star within 6 px of the thread's x, within 12 px of a node, or inside a step name's text box. Captions may have stars behind them.
- **Alternation:** bands alternate a 0.03 text-tone wash with a hairline at the top (`DrawSky`, `:868-893`).

### Motion

- **There is no twinkle anywhere.** The spec says "Seeded, so it never shimmers frame to frame" (`docs/design/flair-v13/spec.md:37`), and the code agrees: positions and alphas are constant.
- **Inconsistencies to hand to the designer:**
  - two renderers with different alphas and inks;
  - path stars at Quiet and Plain, everything else Full only;
  - fixed counts on fields of variable size.

### Ideas the designer can weigh (owner point 3)

These are suggestions, not decided.

- One renderer.
- Area-based density for every field.
- A rare, slow, seeded **twinkle**: about 1 in 12 Bright and Small stars breathing ±25% alpha over 4–7 s periods, phase from the seed. Off under Reduce motion and at Quiet and Plain.
- A **fourth tier**: a soft "glow star" (r 3 radial at 0.06) for depth.
- One faint **shooting star** at most every few minutes in the tree sky, under Full with motion on.
- Warm stars clustered near the moon art (crest, orbit) rather than every sixth.

---

## 7. Quick wins spotted along the way

| # | Item | Where | Effort |
|---|---|---|---|
| Q1 | Rail labels draw at 0.7 × body (≈ 11 px), below the 12 px caption floor used everywhere else (accessibility B6). The default font is scaled down bilinearly, so it is slightly soft. Use the Caption role. | `TabStrip.cs:425-433`, `LayoutBudgets.cs:77` | S |
| Q2 | Rail FontAwesome icons are drawn at the icon font's own size, not the 22 px box, so they mismatch the art icons | `TabStrip.cs:640-646` | S |
| Q3 | The drawer header's "Filters" sits on the same baseline as the tree's "✦ JOURNAL" (both at `leftMin`) | `MainWindow.Frame.cs:638`, `TreePane.Art.cs:44-66` | S (with UI-2a) |
| Q4 | The Filters panel heading "Quick views" labels a slider for the Stalled threshold, and "Stalled after" comes *after* the "7 days" value | `FilterPanel.cs:237-253` | S |
| Q5 | "Overrides" and "Reset" `SmallButton`s render as bare text, so they are not recognisable as buttons | `FilterPanel.cs:150`, `:593` | S |
| Q6 | Reset clears every filter **and the search** with one click and no undo | `FilterPanel.cs:150-155`, `ResetAll` | S (route through `UndoToast`) |
| Q7 | Table header row has no min height, so it is shorter than every data row | `TablePane.cs:1111` | S |
| Q8 | Switching "Game fonts for headings" off makes headings *smaller* (Caption 0.85×), which is the opposite of what a user expects | `Typography.cs:382-387` | S (UI-1 step 2) |
| Q9 | Path chart stars ignore the Decoration level (they draw at Quiet and Plain too) and use text ink | `PathChart.cs:754`, `:888-920` | S |
| Q10 | The Moonlit card's line "Listed in Moonlit treasures." is in tertiary ink on a brass card and is hard to read (owner screenshot); use secondary | `DetailPane.cs:304-306` (Moonlit section body) | S |
| Q11 | The Opens column silently hides icon-0 unlocks, so it disagrees with the detail pane's Unlocks list | `TablePane.cs:2179-2180` | S (with I1) |

---

## Proposed plan rows

| Id | Item | Effort |
|---|---|---|
| UI-1 | Section heading role: TrumpGothic at 1.75 × body (TG 23 at default), fallback Display 1.15× (never below body), heavier gilt ink; follows Text size and UI scale | S–M |
| UI-2a | Filter drawer: one occluding child (no outer gutters or header on the losing window) and the tree skips drawing while covered. Fixes the side bleed and JOURNAL over Filters | S |
| UI-2b | Filter drawer sized to its content (eased height), max body height, rounded foot | S |
| UI-2c | Drawer header as a SectionHeading "FILTERS" with an "N on" caption and round pin/× | S |
| UI-2d | Filter sheet redesign: grouped switch rows, override icon buttons, "Stalled after" in reading order, "More filters" disclosure with count, Reset with Undo toast, brass card surface | M |
| UI-3 | Journal headers: body size (Full TG at 1.6×), secondary ink, min height 32 / 28 / 24; re-run layout budgets | S |
| UI-4a | Rail: 30 px icons, elastic station height that fills the rail, labels at 0.8× (≥ 12 px), FontAwesome sized to the box | S |
| UI-4b | Rail: remove the brass thread and lit bar; glow plate and sliding edge notch for the active tab | S |
| UI-4c | Rail hover, press and select motion (scale 1.08 / 0.96, plate fade, notch travel), Reduce-motion safe | S–M |
| UI-5a | Feature-unlock icons from MainCommand / ContentType (I1, I2, I3, I9); Pup PvP fixed | S |
| UI-5b | Duty icon chain CFC → ContentType → IconDutyFinder (I4, I16); nameless instances named from TerritoryType (I5) | M |
| UI-5c | Reward fixes: Title achievement icon (I6), blank reward slots (I7), EXP and gil icons (I8), "+N more" icons (I21) | S |
| UI-5d | Icons in Characters (roles I11, GC and tribes I13, collection I18), table Job groups (I12), Plan (I14, I22) | M |
| UI-5e | Icon + label action pills on every travel and route button (I20), Route header icon (I19), in-game panels (I15), requirement icons (I17) | M–L |
| UI-6 | Stars: one renderer, area-based density, Decoration-gated path stars, optional seeded slow twinkle and glow tier (designer brief in §6) | S–M |
| UI-Q | Quick wins Q1–Q11 not covered above | S |
