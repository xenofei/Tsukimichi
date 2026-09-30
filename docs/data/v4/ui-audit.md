# UI audit: condensed by the coordinator from the agent's report

Artifacts in this folder:
- icons\index.html: contact sheet per tree node; 2,432 icon PNGs;
- montage\m1..m6.png, rowsize.png;
- tree-dump.txt/.tsv, tree-names.tsv;
- names\names-before-after.tsv (plus measure.py, shorten.py, post.py);
- iconprobe\.

Pixel basis: global scale 1, UI scale 1.15, icon scale 1.25. So Px(x) = 1.15x and Icon(x) = 1.4375x; the body font is 18.4 px.

## 1. Official icons

The tree is built in TreePane.cs (EnsureNodes :610-680, Fold :700-730) from CatalogMapper.JournalIndex (:575-650).

Icon sources:
- **JournalSection / JournalCategory** have no icon column.
- **JournalGenre.Icon:** distinct for classes, jobs and Grand Companies. Otherwise it is generic: 061411 (sidequest), 061412 (MSQ) or one icon per category.
- **ExVersion.Icon** 061875-80: the same shape in six colours, so colour-only.
- **EventIconType markers:** 071201 MSQ, 071221 sidequest, 071341 feature. Good for sections.
- **BeastTribe.IconReputation** 061901-19, 061990: distinct per society (low contrast).
- **ContentType tiles:**

  | Icon | Content |
  |---|---|
  | 061814 | allied societies |
  | 061812 | Grand Company |
  | 061815 | DoL |
  | 061816 | DoH |
  | 061824 | deep dungeons |
  | 061833 | Eureka |
  | 061838 | Bozja |
  | 061846 | V&C |
  | 061847 | Island |
  | 061851 | Occult |

- **ClassJob 0623xx/0624xx** and **role 06258x**: excellent.
- **Region crests 0626xx:** 128 px and illegible at 24 px.
- **Festival IconSpecial 080xxx:** 136×168 portrait, unusable as a row icon.

Plan:
- official icons for Allied Societies, Class & Job, Grand Company and ContentType-backed relic/endeavour nodes;
- EventIconType markers for sections;
- ExVersion for MSQ and regional nodes (the shortened name disambiguates);
- a custom monoline glyph set for the gaps: Chronicles raid series, Hildibrand, Chronicles of Light, Side Story, Special, Other, Coerthas, Mor Dhona, legible region crests, festivals, Omega/Pandæmonium genres, and the deep dungeons (four share one icon).

Drawing and state:
- GetFromGameIcon is already used at 20-24 px (TablePane RowIconSize, CharactersPane). For crispness, draw hr1 at 32 px, or HiRes=false at 24 px.
- Tree rows are at least 30 px, so a 24 px icon fits.
- Keep the state as a **progress ring around the icon**, reusing the halo Ring mode (GaugeGeometry.ModeFor, MoonGlyph.DrawHalo).
  - Complete: full gold ring.
  - 0 %: icon at 60 % opacity.
  - Ready: a gold dot on the ring.
  - Nodes without an icon keep the moon.

## 2. Name shortening

- A pure Core `JournalNames.Short(name, parent, section, language)`:
  - strip the parent prefix and kind suffixes (Main Scenario Quests / Sidequests / Quests / Events / Facet of / Faculty of);
  - keep "II"; "Main Quests" becomes "(Main)"; role genres become "Tank · ShB";
  - a child equal to its parent becomes "Story" or "General" (new strings), or keeps its full name (Lakeland, Thavnair);
  - plus an override map.
- The full name and path always go in the tooltip.
- Over 307 nodes: total 62,040 → 32,443 px; median 183 → 94 px; widest 514 → 297.
- Examples:
  - "Main Scenario (A Realm Reborn through Endwalker)" → "Main Scenario · ARR–EW";
  - "Seventh Umbral Era Main Scenario Quests" → "Seventh Umbral Era";
  - "Chronicles of a New Era - Eden" → "Eden";
  - "Hildibrand Sidequests" → "Hildibrand";
  - "Allied Society Quests (ARR–EW)" → "Allied Societies · ARR–EW";
  - "Class & Job Quests" → "Class & Job".

  The full table is in names\names-before-after.tsv.
- Ambiguous only without the parent: the 40 Main/Daily genres. Chips and scope labels must keep full names.
- Other languages get per-language rules (de/fr/ja prefixes), but localization is frozen, so English only plus fallback.

## 3. Narrow-width audit

**MainWindow body** (MainWindow.cs:1219-1231) is a 4-column resizable ImGui table: rail fixed 136, tree 240 resizable, centre stretch, detail 360 resizable. The window minimum is 1,076 px (ScaleMetrics.cs:204-209).
- No pane has a floor after a splitter drag: ImGui shrinks to about 4 px, and widening one column squeezes the others.
- CentreFloor is only in the window minimum.
- Widths persist in imgui.ini, and there's no reset.
- At the default 1,100 px the centre is about 206 px, while the table's Name column alone is fixed at 276 px.

**Rail** (TabStrip.cs): 156 px wide, 5 × 34.5 px rows ≈ 184 px tall; 68-80 % is empty; it costs 14 % of the width.

**Tree** (TreePane.cs DrawNodeOverlay :342-458):
- names are hard-clipped with no ellipsis (:401-411);
- a cut name has no tooltip unless the node was folded (:512-515);
- below about 163-205 px the right-aligned count (:362-365) slides over the halo and chevron, and nothing yields (the 6.png overlap);
- a depth-1 row at the default width has about 58 px of name room.

**Quest table** (TablePane.cs):
- Name is fixed at 276 px and NoHide (:40, :331), and Status is the only stretch column;
- FitStatusColumn (:525-605) only hides Rewards and Expansion, so below about 640 px Status gets crushed first;
- names have no ellipsis (:662-664), and the hover "…" covers the name's end.

**Detail pane**: word-internal wraps come from TextWrapped in a narrow wrap position.

| Part | Problem | Code |
|---|---|---|
| Header card | 66 px moon box + SameLine title | DetailPane.cs:414-465 |
| State pill | fixed width, overruns | :363-383 |
| Hero banner | name grows upward; the caption is unwrapped AddText | :309-321 |
| CardCaption | collides with the title ("RequirementsAll met") | :257-273 |
| Requirement rows | label + SameLine TextWrapped(detail), 1-2 characters per line | :580-617 |
| Journal path | not wrapped | :502 |
| Moonlit verdict button | pushed off the edge | :713-760 |
| Chain line | overflows | :767-812 |
| Path chart | names hard-clipped | PathChart.cs:609-640, 999-1090 |
| Primary pill | fixed width | :852-860 |

**My blues (PlanPane.cs DrawRow :309-438)**:
- width shares: name 0.36W, pills 0.22W, actions right-anchored, and status gets the leftover, hard-clipped (:396-401);
- status vanishes below about 388 px, and Flag/Reveal overlap below about 367;
- the header title runs under Pin (:256-267); the left list count overlaps its label (:149-151).

**Other panes**:
- **Moonlit:** the toolbar is one about 900 px SameLine chain (:296-344); the table crushes Confidence and State and hides nothing (:366-392).
- **Characters:** about 12 SizingFixedFit tables with wide fixed name columns, so the numbers and status on the right are lost first (CharactersPane.cs :300, 363, 447, 571, 632, 673, 784, 975, 1032, 1346; Abandoned :63-151; Seasonal :82).
- **Flight:** the Actions column is crushed first (:224-247, 316-332); the status can cut the state word (:307).
- **Tonight:** Selectables ignore the wrap (:194); the hint wraps per character (:214-216).
- **WelcomeBack:** title buttons (:133-157); ask row (:241-266); button clipped (:381-384).
- **WhatsNew:** no Max guard, so the buttons draw over the title (:162).
- **EmptyState:** its floors exceed narrow widths (:35, :62); the heading isn't wrapped (:87).
- **Settings:** unwrapped hints; SameLine radio rows; fixed 220 sliders; the verdicts table crushes Restore (:1280-1290).
- **FilterPanel:** fixed Px(110-180) controls overflow below about 190.

Only TablePane and the status bar ever ellipsise.

## 4. Responsive system

Principles:
- every pane has a splitter-enforced floor;
- rows drop parts rather than collide;
- ellipsis + tooltip, and word wrap, never letters;
- stack label over value;
- the arithmetic lives in Core and is unit-tested.

**Floors (logical):**

| Pane | Floor | Default |
|---|---|---|
| Rail | 44 compact / 136 expanded | — |
| Tree | 180 (snaps shut below 120) | 280 |
| Centre | 320 | — |
| Detail | 260 | 360 |

Recommended: replace the body table with **PaneSplit**:
- InvisibleButton handles; double-click resets;
- widths stored in Configuration in logical units;
- pure `PaneLayout.Solve` in Core.

The window minimum becomes about 925 px.

**Tree rows (RowFit, fitted right to left; the name keeps at least 48):**

| Width | Row shows |
|---|---|
| ≥ 300 | everything |
| 240-300 | drop the expansion pill, then the mini bar |
| 200-240 | the count becomes "82 %"; complete nodes show no count |
| 180-200 | the ring alone carries progress; the Ready pill becomes a dot |
| < 180 | icon-only rows |

**Quest table ColumnPlan** (priority order):

| Priority | Column | Behaviour |
|---|---|---|
| 0 | Glyph | fixed |
| 0 | Name | stretch, weight 3, min 140, ellipsis |
| 1 | Status | stretch, weight 2, min = state word + 24 |
| 2 | Level | hides |
| 3 | Job | icon only, then hides |
| 4 | Expansion | hides |
| 5 | Rewards | hides first |

It is deterministic with 16 px hysteresis. Name becoming stretch is the key change.

**My blues / Flight / Abandoned / Moonlit:**
- the same model; the status never cuts the state word;
- pills sized to content;
- below about 420, Flag and Reveal become one "…" menu; below about 300, rows go two-line;
- the Moonlit toolbar goes to two rows; its table hides Confidence, then Kind, never State.

**Detail pane at 260-320:**
- the moon (28 px) goes above the title;
- the meta line wraps by segment;
- requirements stack label over value;
- captions drop to their own line;
- the chain and journal path wrap on separators;
- the state pill gets an ellipsis.

Below 260 the primary action becomes an icon button.

**Helpers:**

| Helper | Where |
|---|---|
| EllipsisText, EllipsisTextAt | Chrome; promotes MainWindow.EllipsisText :1545 and TablePane.DrawStatus :991 |
| StatusText | Chrome |
| LabelValue | Chrome |
| TextFlow.Wrapped + WordWrap | Ui/TextFlow.cs + Core/Ui/WordWrap.cs; word breaks, CJK fallback, cached |
| SameLineOrWrap | promoted from ConfigWindow :449 |
| RowFit | Core/Ui/RowFit.cs |
| ColumnPlan | TableGeometry |
| PaneLayout + PaneSplit | Core + Ui |

Breakpoints live in LayoutBudgets, with tests.

## 5. Rail

| Option | What it is | Width freed |
|---|---|---|
| (A), **recommended** | compact icon rail, 44 logical, tooltips; a chevron expands to today's labelled rail (remembered); the Ready badge sits on the icon | about 105 px, which goes to the tree (default 240 → 280) |
| (B) | horizontal segmented tabs over tree + centre | — |
| (C) | fill the rail's empty space | 0 |

- (A) is the fallback the revamp proposal already anticipated (ui-revamp-proposal.md:89, :382). Start new installs expanded once.
- (C) fills the space with things already shown elsewhere.
