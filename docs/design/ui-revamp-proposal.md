# Tsukimichi — Main window visual revamp

Date: 2026-09-28
Status: proposal for owner approval (no code touched)
Inputs: docs/superpowers/specs/2026-09-27-tsukimichi-design.md, Tsukimichi/Ui/*.cs (v0.5.0), owner's screenshot verdict ("more beautiful and modern; the partial moons are hard to see")
Mockup: docs/design/mockups/main-window.html (open in a browser; ~1100×720)

---

## 0. Diagnosis in one paragraph

The current window is a stock Dalamud table with the moon language painted on top at 10–12 px. Three things make it read as "plain":

1. **The moons are too small and too dark.** `TreeMoonRadius` is 5 logical px (≈7 px at the default 1.15 × 1.25 scale); `RowGlyphRadius` is 6. At that size a 35 % moon is a two-pixel sliver. Worse, the unlit disc (`Theme.UnlitDisc = #2C334A`) has a contrast of **1.47 : 1** against Night and **1.23 : 1** against the raised header card, so the dark side of the moon disappears and the eye cannot judge the fraction because it cannot see the whole circle (WCAG 1.4.11 asks 3 : 1 for graphical objects). The existing Veil ring is `max(1, r·0.07)` = 1 px at 2.4 : 1, which does not rescue it.
2. **No surface hierarchy.** Everything sits on the user's Dalamud window colour; only the detail pane gets Night. Nothing is a card, no row has a resting/hover/selected difference beyond ImGui's default header colour, tabs are the stock `TabBar`.
3. **Numbers instead of progress.** `821/905` right-aligned in Dusk is accurate but not glanceable; the number the player wants is "how far along am I", which is a percentage or a ring, not a fraction.

Everything below fixes those three while staying inside ImDrawList.

---

## 1. Design principles

| # | Principle | What it means in practice |
|---|---|---|
| P1 | **Glanceable while playing** | The window is opened mid-session, often over a busy scene. State must be readable in under a second from ≥ 60 cm: state moons ≥ 18 px, progress gauges ≥ 24 px, a coloured 3 px state stripe on every table row, never more than two text weights per row. |
| P2 | **Night palette, gold as the only warm hue** | Surfaces are three levels of blue-black (Night, NightRaised, NightHover). All text is cool (Silver / Mist / Dusk). Gold (Moon) is reserved for progress, "actionable now" and selection. Eclipse (red-violet) is reserved for Foreclosed and destructive. Nothing else is coloured, so gold means "look here". |
| P3 | **Shape before colour** | Every state is distinguishable with the colour removed (phase + ring style + notch/dash). Gold vs silver is a second channel, never the only one. |
| P4 | **Density is a user choice** | Three density modes (Dense 24 px rows / Comfortable 32 px / Cards 44 px) set in the existing Display section beside the UI/Icon scale sliders. All sizes derive from `UiMetrics.Scale` / `IconScale`; nothing hard-codes pixels. |
| P5 | **Immediate-mode honesty** | No layout the medium cannot do. Fake elevation with two alpha lines, fake blur with a gradient scrim, no height animations (immediate-mode layout would fight them); animate only alpha, rotation and stroke length. |
| P6 | **Respect the host, but own the window** | Today only the detail pane is Night. The proposal makes the whole main window Night-themed by default (it is the product's identity) and keeps a **Classic layout** toggle that restores the Dalamud-style chrome and the stock tab bar. Popups, tooltips and the config window keep the user's Dalamud style. |

---

## 2. Main window structure

Reference sizes below are at `UiMetrics.Scale = 1.0` (logical px); multiply by Scale at runtime, and moons/icons additionally by IconScale. Default window stays 1100 × 700.

```
+ Tsukimichi ----------------------------------------------------------------- - [] x +
| [o Search quests, rewards, ids        x] (Features|Level band|Stalled) [Filters (2)] |  toolbar 36
|                                     [(job) Michiru Tsukikage . Balmung *]  ◑ ? ^ ⚙  |
| chips: [Hide completed x] [Level 80-90 x]                                            |  chip row 24 (only when engaged)
+--------------+-----------------------------------------------+---------------------+
| TAB STRIP    | TABLE HEADER (sticky, NightRaised)            | DETAIL CARD STACK   |
| ◐ Journal 62%| | ◑ Name            Lv  Job  Next step  Ex  🎁 | +- hero banner ---+ |
| ◇ Moonlit 41%| | ●  ...                                        | | scrim + name    | |
| ◇ Characters | | ◐  ...                                        | | [Ready] pill    | |
| ◇ Flight     | | ○  ...                                        | +-----------------+ |
|--------------|                                               | chain 3/7 ▬▬▬▬░░░  |
| TREE         |                                               | +- Requirements -+ |
| ◑ All quests |                                               | | ● Level 50     | |
|          62% |                                               | | ○ > Sworn ...  | |
| v ◕ Main Sc. |                                               | +----------------+ |
|          91% |                                               | +- Rewards strip + |
|   ● ARR 100% |                                               | +- Path -+ Giver + |
|   ◐ HW   35% |                                               | [⚑][📖][⤴][✈]      |  action bar 32
+--------------+-----------------------------------------------+---------------------+
| ◑ 62% . 5,373 quests . showing 5,193 of 5,193 . * live . [MSQ: Close to Home >] v0.5 |  status 26
+--------------------------------------------------------------------------------------+
```

### 2.1 Header / toolbar (36 px row, NightRaised strip with a 1 px NightLine bottom border)

| Element | Spec (scale 1.0) | Notes |
|---|---|---|
| Search | Pill 280 × 26, rounding 13, fill NightSunken, 1 px border NightLine, focus border Moon @ 0.9. Magnifier (FontAwesome, 12 px, Dusk) at left; clear × appears on hover or when non-empty. Placeholder text Dusk. | `PushStyle(FrameRounding 13, FramePadding (10,5))` + `PushColor(FrameBg NightSunken, Border NightLine)` around the existing `InputTextWithHint`. The Times icon button becomes a draw-list glyph inside the pill's right edge (`InvisibleButton` 20 × 20). |
| Presets | **Segmented control**, 3 segments, height 26, outer rounding 13, fill NightSunken, 1 px NightLine border; active segment fill Moon @ 0.16 + text Moon; inactive text Mist; disabled (no snapshot) text Veil. Segment padding 10 px, dividers 1 px NightLine. | Moves presets out of the filter panel onto the toolbar (they are the highest-value one-click filters). The filter panel keeps a mirror. |
| Filters | Icon button 26 × 26 (`SlidersH`) + label; when any filter is engaged a **count badge** 14 px circle, Moon fill, Night text, at the top-right. Open state: fill Moon @ 0.16. | Replaces the plain `Button("Filters")`. |
| Character chip | Pill h 26: 18 px game job icon (`ITextureProvider`, `AddImageRounded` r 9) + name Silver + world Mist; live: 6 px Moon dot at the right with a 1.6 s breathing alpha (0.6 → 1). Snapshot view: dot replaced by a 12 px veiled moon and the age in Mist. Dropdown keeps the existing combo behaviour (popup styled Night). | The "●" text in `Strings.LiveMarker` becomes the drawn dot. |
| Right cluster | Sync moon 18 px, then three round icon buttons 26 px (help, tutorial, settings), rounding 13, transparent fill, hover fill NightHover, icon Mist → Silver on hover. | Same `UiRects` keys recorded (tutorial). |
| Chip row | Drawn only when at least one filter is engaged; chips are pills h 22, fill NightRaised, 1 px NightLine, text Mist at 0.9×, an × glyph 10 px in Dusk that turns Silver on hover. The whole chip is the clear button (as now). | Removes the fixed empty strip that today reserves a row even when empty. Layout shift is acceptable because the chip row sits above a resizable body. |

Trade-off: presets on the toolbar cost ~230 px of width. Below 1000 px window width the segmented control collapses to a single "Presets ▾" pill.

### 2.2 Tab strip (custom-drawn, left column, 4 rows × 30 px, vertical)

Vertical tabs in the left column beat the stock horizontal `TabBar` because the column is only 240 px wide and the fourth tab already wraps at larger UI scales.

- Each tab: 16 px icon + label + right-aligned badge.
  - Journal: **filling moon of overall completion** (the same gauge as the tree, 16 px, no arc) + "62%".
  - Moonlit: crescent icon (FontAwesome `Moon`) + obtained/total as "41%".
  - Characters: `Users` icon + snapshot count.
  - Flight: `Plane` icon + zones with currents missing, or a full moon when done.
- Active: fill NightHover, 3 px Moon bar on the left edge (rounded 1.5), label Silver, icon Moon. Inactive: label Mist, icon Dusk. Hover: fill NightHover @ 0.6.
- Implementation: `InvisibleButton` per tab + draw list; `ui.Tab` becomes the single source of truth, which removes the `drawnTab` / `SetSelected` one-frame dance in `MainWindow.DrawTab`. Record `UiRects.Tabs` as the union.
- Classic layout toggle restores `ImRaii.TabBar`.

Trade-off: vertical tabs take 120 px of the tree's height. At the default 700 px window height the tree keeps ~430 px, enough for the six sections expanded one level. An icon-only rail (30 px wide, far left) is the fallback if the owner prefers tree height; the mockup shows full labels.

### 2.3 Sidebar tree

Current: `TreeNodeEx` with an empty label and an overlay (moon r 5, name, `done/total` in Dusk). Proposed:

| Property | Value |
|---|---|
| Row height | 28 px (Comfortable), 24 (Dense), 32 (Cards) |
| Indentation | 16 px per level; a 1 px NightLine guide under the parent chevron for open nodes |
| Chevron | Draw-list triangle 8 px, Dusk; rotates 0 → 90° over 140 ms ease-out on open (§3). Leaves have no chevron but keep the slot, so moons align in one column. |
| Progress gauge | **MoonGauge 24 px** (§2.7): filling moon r 8 inside a 2 px ring track (Veil @ 0.55) with a Moon arc from 12 o'clock clockwise = fraction. The arc and the phase encode the same number twice, which is what makes 10 % vs 35 % legible. |
| Name | Silver (sections: two-pass "bold" as now). Complete sections: name Moon and the gauge shows a full moon without the arc track. |
| Right column | Percentage in Mist at 0.85× ("82%"); the exact `821/905` moves to the tooltip and to Dense mode (where it replaces the percentage). Scopes with 0 total show "—". |
| Hover | Full-row fill NightHover, rounding 4, inset 2 px from the column edges. |
| Selected | Fill Moon @ 0.12, 2 px Moon bar at the left edge, name Silver, gauge track Moon @ 0.35. |
| Virtual nodes | Feature Unlocks and Unlisted sit under a hairline (NightLine) with a 6 px gap; Feature Unlocks gets a small `Unlock` glyph in the chevron slot. |
| Badges | Optional 16 px pill right of the name: number of **Ready** quests in the scope, Moon text on Moon @ 0.16 (e.g. "3"). `TreeCounts` needs one extra field per node. Hidden in Dense mode. |

Keep `TreeNodeEx` under the overlay (it supplies keyboard nav, open-on-arrow, `IsItemToggledOpen`) and push `ImGuiCol.Header*` to transparent so the custom fills draw underneath. That preserves `UiRects.Tree` and the tutorial.

### 2.4 Table / list

Current: `RowBg` zebra + `Selectable(SpanAllColumns)` + moon r 6 + a 2 px stripe only for Ready/Accepted.

| Property | Comfortable (default) | Dense | Cards |
|---|---|---|---|
| Row height | 32 | 24 | 44 |
| State moon | r 9 (18 px) | r 6 (12 px) | r 11 (22 px) |
| Banner thumbnail | — | — | 64 × 32, `AddImageRounded` r 4, UV cropped to the banner's centre band; NightRaised placeholder while loading |
| Reward icons | 18 px, r 3 | 14 px | 22 px, r 4 |
| Zebra | off (1 px NightLine @ 0.5 row separators instead) | on (Veil @ 0.10) | off, 4 px gap between cards |

Shared:
- **State stripe** 3 px on the left edge for every state (`Theme.StateColor`; Unknown uses Veil, Blocked uses Dusk @ 0.6). Ready gets the stripe plus the moon halo. Because the stripe uses position and length, not only hue, it survives colour removal.
- **Hover elevation**: fill NightHover + 1 px line along the top edge in Silver @ 0.08 and 1 px along the bottom in Night @ 0.6 (a two-line "lift"). Fill through `TableSetBgColor(RowBg0)`, lines on the draw list; both work with the list clipper.
- **Selection**: fill Moon @ 0.12 + 1 px Moon @ 0.45 outline (rounding 4, inset 1 px) + the stripe brightens to full Moon.
- Name Silver; Level as a 28 × 16 pill (NightSunken, Mist text); Job: 16 px game job icon + abbreviation in Mist; Next step keeps "first word Silver, rest secondary" but the secondary becomes Mist (today's Dusk fails AA on a hovered row, §5); Expansion as a two-letter pill (ARR / HW / SB / ShB / EW / DT) in Dusk.
- Header: sticky, NightRaised fill, labels Mist at 0.85×, sort arrow in Moon.
- Cards mode: each row is a rounded-6 card on NightRaised with a 4 px gap, name at 1.0× and next step under it at 0.85× (two lines), banner thumbnail at the left. It is the "browsing" mode, not the working mode.

Trade-off: dropping zebra in Comfortable loses cross-column tracking on wide tables; the row separator and the hover fill compensate. Dense keeps zebra.

### 2.5 Detail pane as a card stack (360 px column, Night background, 8 px gutter)

Cards: rounding 6, fill NightRaised, 1 px border NightLine, padding 10 × 8, 8 px gap, each with a 0.85× Mist header line (icon + title) and an optional right-aligned control.

1. **Hero banner** (no card chrome): the journal banner at column width, height ≤ 150, corners rounded 6 via `AddImageRounded`. **Gradient scrim** from Night @ 0 at 45 % of the height to Night @ 0.92 at the bottom (`AddRectFilledMultiColor`). Over it: the **state pill** top-left (h 20, fill state colour @ 0.18, 1 px state colour @ 0.7, 12 px state moon + state name in the state text colour), the special badge top-right, the quest name at 1.2× Silver bottom-left (wrap at width − 24), genre · expansion · level in Mist 0.85× beneath. Quests without a banner get a card with a 40 px moon at the left and the same text.
2. **Chain progress**: a 6 px bar (track Veil @ 0.4, fill gold gradient, rounding 3) with "Chain: A Realm Reborn · 3 of 7" in Mist. Only when the quest has a chain.
3. **Requirements card**: rows 22 px: **moon mark 14 px** (full = met, new with a Silver ring = unmet, gold first quarter = the next step), label Silver, detail Mist ("Trusted, needs Sworn"). The next-step row gets a 2 px Moon bar at the card's left edge. No snapshot → the card body says so in Mist.
4. **Rewards strip**: horizontal 36 px icons, rounded 6, on 44 × 44 NightSunken tiles; Moonlit-unique rewards get a 1.5 px Moon ring and a 6 px crescent badge; obtained rewards get a 10 px full moon at the bottom-right. Hover: the existing `RewardTooltip`. Gil / EXP as two small pills at the end.
5. **Moonlit verdict** (when `Overrides` is present): one line with the confidence badge (existing colours) and the override buttons as small pills.
6. **Path card**: the vertical trail (existing `BeginGlyphLine`), moons r 7, connector 2 px Moon where walked / 1 px Dusk where not, expansion headers as Mist 0.85× dividers, the target row on Moon @ 0.12. "Show path" keeps the 1.5 s highlight but as a Moon @ 0.35 → 0 fade of the card border.
7. **Unlocks card**: as now, in the same row style.
8. **Giver card**: NPC name Silver, zone + coordinates Mist, map-pin icon.
9. **Action bar**: sticky at the bottom of the column (drawn outside the scrolling child, 32 px): a pill group of 28 × 28 icon buttons (Flag on map, Open journal, Link in chat, Copy coordinates, Teleport when Lifestream is present), fill NightRaised, hover NightHover, icons Mist → Silver, disabled Veil with the reason in the tooltip. Replaces the row of `SmallButton`s under Giver.
10. **Provenance**: one line Dusk 0.85× under the action bar ("Completed per client flags at 21:14").

### 2.6 Status bar (26 px, NightSunken, 1 px NightLine top border)

Left to right, separated by "·" in Veil:
- Overall **MoonGauge 18 px** + "62%" in Silver (tooltip: done/total).
- "5,373 quests" Mist · "showing 5,193 of 5,193" Mist.
- Live pip: 6 px Moon dot (breathing) + "live" Mist; snapshot: veiled moon 12 px + "snapshot 21:14".
- **MSQ chip**: pill h 18, fill Moon @ 0.10, "MSQ · Close to Home ›" in Moon, click = existing `SelectMsq`. Ellipsis after 28 chars.
- Right-aligned: "v0.5.0" in Veil.

### 2.7 MoonGauge (the fix for illegible partial moons)

Two changes make fractions legible at a glance, and both are needed:

1. **A visible disc.** The unlit side becomes `Shadow = #3A4363` (1.9 : 1 on Night, still a "dark side") **plus a `max(1.5, r·0.12)` px ring in Dusk** (5.1 : 1 on Night, 4.3 : 1 on raised) so the full circle outline always exists. The lit lens (Moon, 6.6 : 1 against Shadow) is then judged against a visible circle. At r ≥ 9 the existing sphere shading stays on.
2. **A second encoding.** An arc from 12 o'clock, clockwise, on a ring 3 px outside the disc: track Veil @ 0.55, 2 px; arc Moon, 2 px, round caps (`PathArcTo` + `PathStroke`; caps are two 1 px filled circles at the ends). The arc gives the number as a length, which people compare better than a lens area, and the phase gives the moon its meaning. Beside it, the percentage in Mist.

Sizes (logical px): tree 24 (r 8 + gap 2 + arc 2, diameter), tab strip 16 (no arc), status bar 18, Characters dashboard 40, empty states 56.

Alternative considered: a plain arc gauge without the moon (cleaner, cheaper). Rejected: the moon is the identity; keep both, drop the arc below 16 px.

Alternative considered: lit fraction as a vertical fill ("moon rising"). Rejected: it breaks the phase metaphor shared with the eight state glyphs.

### 2.8 Empty states

Centre block: a 56 px moon (veiled for "select a quest"; new moon with a Dusk ring for "nothing matches"; a 56 px gauge at 0 % for "no snapshot"), a 1.2× Silver heading ("Nothing matches"), one Mist line ("Hide completed and Level 80–90 are hiding every quest here"), then a primary pill button ("Reset filters") in Moon @ 0.16 / Moon text. The offending-filters list becomes chips inside the message, each clickable to clear only that one. Keep `EmptyState.Draw` and add a `DrawWithAction` overload.

---

## 3. Micro-interactions

All animation is per-frame lerp on `ImGui.GetTime()`; state lives in a `Motion` helper keyed by a `ulong` id (a `Dictionary<ulong,float>` pruned when untouched for 2 s). Every effect is skipped when `Configuration.ReduceMotion` is on. Easing: `v += (target − v) · (1 − exp(−dt · k))` with k = 18 for hover (≈120 ms), 12 for selection (≈150 ms), 10 for chevrons (≈140 ms).

| Interaction | Behaviour | Values |
|---|---|---|
| Hover glow | Row / tab / chip fill alpha lerps in; on moons, the Ready halo (existing three discs) is reused at 0.4× its alpha for any hovered moon. | fill 0 → 1 of NightHover in 120 ms; out in 180 ms |
| Selection ring | Outline alpha 0 → 0.45 and inset 2 px → 1 px ("settle"). Stripe brightens to full Moon. | 150 ms |
| Expand / collapse | Chevron rotation 0 → 90°; children fade alpha 0 → 1 (`PushStyleVar(Alpha)` for the text, the overlay alpha for moons). No height animation. | 140 ms |
| Reveal pulse | On `UiState.Reveal` / `ShowPath`: a Moon ring at the row's rect expands 0 → 6 px while alpha 0.7 → 0, twice, 450 ms each. Same on the Path card border. | 900 ms total |
| Live pip | Alpha 0.6 ↔ 1.0 sine, period 1.6 s. Solid 0.6 when the poller is paused. | — |
| Gauge fill | When a count changes (quest completed), the arc animates from the old to the new fraction. | 500 ms ease-out |
| Button press | Fill darkens to Moon @ 0.26 while active (HelpWindow's `RowHeaderActive`). | immediate |
| Tooltip | `PushStyle(PopupRounding 6, PopupBorderSize 1, WindowPadding (10,8))` + `PushColor(PopupBg Night @ 0.96, Border NightLine, Text Silver, TextDisabled Mist)`; title line Silver, body Mist; the state moon at 14 px in state tooltips. ImGui's default delay; no fade (tooltips are re-created each frame; an alpha fade would flicker on move). | — |

---

## 4. Spacing, typography, tokens

### 4.1 Spacing scale (× `UiMetrics.Scale`)

`Space.XS 2 · S 4 · M 8 · L 12 · XL 16 · XXL 24`. Rounding: `R.Chip 4 · R.Card 6 · R.Panel 8 · R.Pill = height / 2`. Hairline 1, Stripe 3, Bar 2. Column gutter 8. Card padding (10, 8). Toolbar row 36, chip row 24, tab row 30, status 26, action bar 32.

Density (`Configuration.Density`: Dense / Comfortable / Cards) sets `UiMetrics.RowHeight` 24 / 32 / 44, `RowGlyphRadius` 6 / 9 / 11, `RowIconSize` 14 / 18 / 22, `TreeRowHeight` 24 / 28 / 32, `TreeMoonRadius` 6 / 8 / 8.

### 4.2 Typography (× `UiMetrics.FontScale`)

Dalamud's default font (Noto Sans, 17 px at global scale 1). Four roles:

| Role | Size | How |
|---|---|---|
| Display (empty-state heading, hero name) | 1.2× | `IFontAtlas.NewGameFontHandle(Axis18)`, or `SetWindowFontScale(1.2)` inside a dedicated child |
| Body | 1.0× | default |
| Caption (percentages, headers, provenance, chips) | 0.85× | `NewGameFontHandle(Axis14)`; fallback: `ImDrawList.AddText(ImFontPtr, fontSize·0.85, …)`, already used in `DetailPane.DrawBanner` |
| Section title (card headers) | 0.85× + icon | same as Caption |

Rule: never mix more than two roles in one row. `AddText` with a smaller size renders the same atlas glyphs scaled down (slightly soft under 0.8×; 0.85× is the floor).

### 4.3 Colour tokens

Existing tokens stay. New ones are additions to `Theme.cs`; hex values are what the mockup uses. Contrast ratios are computed in §5.

| Token | Hex | Role / where used |
|---|---|---|
| **Surfaces** | | |
| Night | `#0F1424` | Window body, detail pane background, tooltip fill (@ 0.96) |
| NightSunken | `#0B0F1C` | Search pill, status bar, gauge wells, reward tiles, level pills |
| NightRaised | `#1E2437` | Toolbar strip, table header, cards, chips, tab-strip active fill (already `Lerp(Night, Veil, 0.25)`) |
| NightHover | `#262D45` | Hover fill for rows, tabs, buttons |
| NightLine | `#2A3149` | Hairlines, card borders, row separators (subtle) |
| VeilLine | `#5C6584` | Strong dividers, gauge track, non-focus outlines (3.2 : 1 on Night) |
| Shadow | `#3A4363` | The moon's unlit disc (replaces `UnlitDisc #2C334A`); always paired with a ring |
| **Text** | | |
| Silver | `#DDE3F0` | Primary text, silver glyphs |
| Mist | `#A9B2CC` | Secondary text (next step, captions, percentages, chip labels) — new; Dusk stays for tertiary |
| Dusk | `#7C86A8` | Tertiary text, rings, chevrons, separators, placeholder |
| Veil | `#4A5270` | Disabled text, version string, Unknown disc fill |
| VeilText | `#8A93B0` | Unknown state text and dashed ring (Veil itself fails AA) |
| **Accent (gold)** | | |
| MoonBright | `#FFE9A6` | Gradient top stop, moon highlight arc, active icon on hover |
| Moon | `#F2D27A` | Gradient middle, progress arc, selection, Ready / Completed / Accepted, primary button text |
| MoonDeep | `#D8B45A` | Gradient bottom stop, pressed state, gauge arc end cap |
| MoonDim | `#B8933F` | Gold on gold-tinted fills where less pop is wanted (badges on hover) |
| Gold gradient | `MoonBright → Moon → MoonDeep`, vertical | Chain progress bar, primary pill buttons; the MSQ chip uses flat Moon @ 0.10 |
| **State** | | |
| Eclipse | `#B25C7F` | Foreclosed stripe and ring, destructive button fill |
| EclipseText | `#D68AA8` | Foreclosed text and state-pill text (Eclipse fails 4.5 : 1 for text) |
| **Fills with alpha** | | |
| SelectionFill | Moon @ 0.12 | Selected row / node / tab |
| SelectionRing | Moon @ 0.45 | 1 px outline of the selected row |
| ActiveFill | Moon @ 0.16 | Active preset segment, open Filters button, primary button |
| PressedFill | Moon @ 0.26 | Active / pressed |
| HoverLift | Silver @ 0.08 (top edge) + Night @ 0.6 (bottom edge) | Fake elevation on hovered rows |
| Scrim | Night @ 0 → Night @ 0.92 | Hero banner name strip |
| Halo | Moon @ 0.04 / 0.07 / 0.10 at 1.6 / 1.35 / 1.15 r | Ready glow (existing) and moon hover glow at 0.4× |
| ZebraRow | Veil @ 0.10 | Dense-mode alternate rows (was 0.16) |

`Theme.StateColor` gains a `StateTextColor` companion: Foreclosed → EclipseText, Unknown → VeilText, Blocked → Mist, others unchanged.

---

## 5. Accessibility

### 5.1 Contrast (computed, WCAG 2.x relative luminance)

Targets: text 4.5 : 1 (AA), large text / graphics / UI components 3 : 1.

| Foreground | on Night | on NightRaised | on NightHover | Verdict |
|---|---|---|---|---|
| Silver `#DDE3F0` | 14.2 | 12.0 | 10.6 | AAA everywhere |
| Moon `#F2D27A` | 12.5 | 10.5 | 9.5 | AAA |
| MoonDeep `#D8B45A` | 9.3 | 7.8 | 7.1 | AAA |
| Mist `#A9B2CC` | 8.7 | 7.3 | 6.4 | AAA — use for secondary text |
| Dusk `#7C86A8` | 5.1 | 4.3 | 3.8 | AA on Night only; **fails AA on raised / hover** → tertiary and rings only, never body text on cards (today's Dusk next-step text on a hovered row is 3.8) |
| VeilText `#8A93B0` | 6.0 | 5.1 | 4.6 | AA — Unknown text |
| Veil `#4A5270` | 2.4 | 2.0 | 1.8 | fails — disabled only (exempt) |
| EclipseText `#D68AA8` | 7.0 | 5.9 | 5.4 | AA — Foreclosed text |
| Eclipse `#B25C7F` | 4.1 | 3.5 | 3.2 | graphics only (≥ 3 : 1); not for text |
| VeilLine `#5C6584` | 3.2 | 2.7 | 2.4 | strong dividers on Night; on cards use 2 px |
| Shadow `#3A4363` (unlit disc) | 1.9 | 1.6 | 1.4 | below 3 : 1 by design (it is "dark"); the Dusk ring (5.1 / 4.3 / 3.8) supplies the object boundary |
| Moon on Shadow (lit lens vs unlit disc) | **6.6** | | | the number that matters for fraction legibility (was 8.5 vs `#2C334A`, but that disc itself was invisible at 1.47) |
| Night text on Moon (badge counts, primary button) | 12.5 | | | AAA |
| Silver on SelectionFill (`#3C3C40` effective) | 8.5 | | | AAA |

Why not a lighter Shadow to reach 3 : 1 against Night? Even Veil (`#4A5270`) reaches only 2.4 : 1 and drops the lens contrast to 5.2 : 1; the ring is the correct fix, and it is what the existing `DrawFilling` already attempts (a Veil ring) except too thin and too dark: `max(1, r·0.07)` = 1 px in Veil at 2.4 : 1. Proposed: `max(1.5, r·0.12)` px in Dusk.

### 5.2 Colour-blind safety (shape + colour)

Each of the eight states is unique by geometry alone:

| State | Shape channel | Colour channel |
|---|---|---|
| Completed | full disc, no ring | gold |
| Accepted | 75 % gibbous + thin solid ring | gold |
| Ready | half + halo (no ring) | gold |
| ReadyOnOtherJob | half + solid ring | silver disc, gold ring |
| DoneThisCycle | 75 % *left* lit (mirror of Accepted), no ring | silver |
| Blocked | empty disc + solid ring | silver ring |
| Foreclosed | empty disc + thick ring + notch | eclipse |
| Unknown | empty disc + dashed ring | veil |

Gold vs silver is the weakest pair for deuteranopia; the ring / halo difference carries it. The row stripe is 3 px wide for all states; Foreclosed additionally uses a dashed stripe (2 px on, 2 px off) so it is not colour-only. The state pill in the detail hero always includes the state *name*.

Progress: the arc (length) + the phase (area) + the percentage (text) are three independent encodings.

### 5.3 Keyboard focus

- ImGui keyboard nav is a Dalamud-wide setting; the plugin cannot force it but must not break it. Every custom row keeps a real item (`TreeNodeEx` under the tree overlay, `Selectable` under table rows, `InvisibleButton` for tabs / chips) so `IsItemFocused()` works.
- Draw a **focus ring** (1.5 px Moon @ 0.9, rounding 4, inset 1) on any focused item; push `ImGuiCol.NavHighlight` to Moon.
- Shortcuts inside the window (only when it is focused, `ImGui.IsWindowFocused(RootAndChildWindows)` + `IsKeyPressed`): `Ctrl+F` focus search, `Esc` in search clears it, `Ctrl+1..4` tabs, `F` toggles Filters, arrow keys in the tree / table are ImGui's own, `Enter` on a table row = open journal (same as double-click). Document in Help → Commands.
- Tab order follows the visual order: search → presets → filters → character → right cluster → tabs → tree → table → detail action bar.

---

## 6. Implementation map

### 6.1 New helper types

| File | Responsibility | Public surface (sketch) |
|---|---|---|
| `Ui/Chrome.cs` | Rounded surfaces and pills on the draw list, using the channel-split pattern from `DetailPane.DrawHeaderCard`. | `Chrome.BeginCard(id, title?, icon?)` / `EndCard()`; `Pill(text, fill, textColor, height)`; `Chip(label, onClear)`; `Badge(count, pos)`; `Hairline(y)`; `Lift(min, max)` (the two-line elevation); `SegmentedControl(ref index, labels, enabled[])`; `IconButtonRound(icon, size)`; `Scrim(min, max, from, to)`; `ImageCover(wrap, min, max, rounding)` (centre-crop UVs); `FocusRing()`. |
| `Ui/Gauge.cs` | `MoonGauge.Draw(dl, center, radius, fraction, options)` (ring track, arc, inner moon via `MoonGlyph.DrawFilling`, end caps); `MoonGauge.DrawInline(fraction, size, showPercent)`; `Bar(min, max, fraction)` (gradient bar). | Pure drawing; arc maths in Core (`Tsukimichi.Core/Ui/GaugeGeometry.cs`: arc endpoints, cap positions) so it is unit-testable. |
| `Ui/Motion.cs` | Per-key eased values on `ImGui.GetTime()`; pulse and breathing helpers; `ReduceMotion` gate; prune. | `Motion.Lerp(key, target, k)`, `Motion.Pulse(key, seconds)`, `Motion.Breath(period)`. |
| `Ui/TabStrip.cs` | Vertical custom tabs with icon, label, badge; records `UiRects.Tabs`. | `TabStrip.Draw(ref NavTab tab, ReadOnlySpan<TabItem> items)`. |
| `Ui/Typography.cs` | Font handles (Axis14 / Axis18 via `IFontAtlas`) with `AddText` fallbacks. | `Typography.Caption`, `.Display` as `IDisposable` pushes; `Typography.CaptionText(dl, pos, color, text)`. |
| `Theme.cs` (extended) | New tokens from §4.3, `StateTextColor`, `PushNightWindow()` (window-wide Night chrome), `PushTooltip()`. `UnlitDisc` is retargeted to `Shadow`. | — |
| `UiMetrics.cs` (extended) | `Density`, `RowHeight`, `TreeRowHeight`, `TreeGaugeSize`, `GaugeRing`, `Rounding.*`, `Space.*`, `ToolbarHeight`, `StatusHeight`, `ActionBarHeight`. | — |
| `Config/Configuration.cs` | `Density` (enum, default Comfortable), `ClassicLayout` (bool, default false), `ReduceMotion` (bool, default false). | — |
| `Core/Ui/GaugeGeometry.cs` + tests | Arc angle for a fraction, cap positions, clamp. | tests in `Tsukimichi.Tests/Ui`. |

### 6.2 Files that change

| File | Change |
|---|---|
| `MainWindow.cs` | `PushNightWindow` around `DrawContent` (unless Classic); toolbar rebuilt with `Chrome` (search pill, segmented presets, Filters badge, character chip, round icon buttons); chip row conditional; `DrawNavigation` uses `TabStrip` (Classic → old `TabBar`); status bar uses `MoonGauge`, MSQ pill, right-aligned version. `drawnTab` logic only remains for Classic. |
| `TreePane.cs` | `DrawNodeOverlay` → gauge row (hover / selection fills through transparent `Header*` colours, chevron on the draw list, percentage caption, Ready badge). Node gets a `Ready` count. |
| `TablePane.cs` | Row layout per density; stripe for all states; hover / selection via `TableSetBgColor` + `Lift`; header restyle; level / expansion pills; job icon; Cards mode with a thumbnail column (new `Column.Banner`, hidden unless Cards). Zebra only in Dense. |
| `DetailPane.cs` | Header → hero with scrim + state pill; sections → `Chrome.BeginCard`; requirements / rewards / path restyled; action bar drawn under the scrolling child (`Draw` splits the size into child + bar); provenance caption. The `Model` is untouched. |
| `FilterPanel.cs` | Presets become a mirror of the toolbar segmented control; chips → `Chrome.Chip`; Display section gains Density, Classic layout, Reduce motion. |
| `EmptyState.cs` | Heading + line + optional action button + offending-filter chips. |
| `MoonlitPane.cs`, `FlightPane.cs`, `CharactersPane.cs` | Left lists adopt gauge rows and card headers (increment 5). |
| `HelpWindow.cs`, `TodoOverlay.cs`, `DiscoveryWindow.cs`, `HoverHint.cs` | Replace local card / pill helpers with `Chrome` (no visual change intended; increment 5). |
| `Strings*.cs` | New labels: Density, Classic layout, Reduce motion, Ready-badge tooltip, shortcut list in Help. |
| `docs/ui-smoke-checklist.md` | New checklist items per increment. |

### 6.3 Risk notes

| Risk | Mitigation |
|---|---|
| **Tutorial rectangles** (`UiRects`) depend on item rects of the stock widgets. | Every replacement keeps a real item at the same key; `TabStrip` records the union; add a smoke step that runs the tutorial to the end. |
| **Keyboard nav / open-on-arrow** lost if the tree is rebuilt from `InvisibleButton`s. | Keep `TreeNodeEx` under the overlay (as now) and only make its `Header*` colours transparent. |
| **Table selection + custom fills**: `Selectable(SpanAllColumns)` paints its own header colour over the row. | Push `ImGuiCol.Header*` transparent inside the table and paint fills with `TableSetBgColor(RowBg0)` (respects the clipper and column widths). |
| **Font handles**: Axis14 / Axis18 through `IFontAtlas` cost an atlas rebuild and memory. | Ship the `AddText(font, size·0.85)` fallback first (increment 1); add handles in increment 5 behind a null check. |
| **Draw-call growth**: gauge = disc + lens + ring + track + arc + 2 caps ≈ 7 primitives vs 3 today. | Only visible rows draw (clipper); ~40 tree rows + ~30 table rows ≈ 700 extra vertices, negligible. Cap arc segments at 32. |
| **Whole-window Night chrome** conflicts with the spec's "respect the user's style" and with users on light Dalamud themes. | Default on (product identity), Classic toggle turns it off; popups / config keep the host style. Verify against the three stock Dalamud themes. |
| **Banner thumbnails** in Cards mode can stall the first frames (texture loads). | `TryGetWrap`, draw a NightRaised placeholder, never block; visible rows only. |
| **`AddImageRounded`** takes only `uv0 / uv1`; aspect crops must be computed by the caller. | `Chrome.ImageCover` computes centre-crop UVs. |
| **Animation state** on a 5k-row table could leak keys. | Prune entries untouched for 2 s; hover keyed by RowId; only visible rows call `Motion`. |
| **Layout shift** from the conditional chip row. | The body is a resizable table, so a 24 px shift is harmless; the tutorial reads live rects. |
| **Small-text legibility** at UiScale 0.9 (caption = 13 px). | Floor caption at 12 px absolute; below UiScale 1.0 captions render at 1.0×. |

### 6.4 Order of work (each increment shippable)

| # | Scope | Flag | Why this order |
|---|---|---|---|
| 1 | **Tokens + Gauge + tree rows + status gauge.** `Theme` additions, `Shadow` disc + thicker Dusk ring in `MoonGlyph`, `Gauge.cs`, `Motion.cs` (hover / select lerp only), `TreePane` gauge rows with percentage, status-bar gauge, `Density` setting driving row heights. | none (pure rendering; sizes already follow the sliders) | Fixes the owner's stated complaint first; visible in the first build; no structural risk. |
| 2 | **Toolbar + tab strip + chips + status bar.** `Chrome.cs` (pill, segmented, badge, round icon button, chip), `TabStrip.cs`, `PushNightWindow`, MSQ pill, version right-aligned. | **Classic layout** toggle restores the stock tab bar and Dalamud chrome | Biggest "modern" delta; the toggle protects the tutorial and users with light themes. |
| 3 | **Table.** Stripe for all states, hover lift, selection ring, header restyle, level / expansion pills, job icons, Dense / Comfortable; Cards mode with thumbnails last. | Density setting (Comfortable default) | Depends on Chrome from 2; the clipper path needs the most testing. |
| 4 | **Detail card stack + action bar + empty states.** Hero scrim + state pill, cards, rewards strip, sticky action bar, `EmptyState` with action. | none | Self-contained pane; the `Model` is unchanged so no data risk. |
| 5 | **Motion polish + typography + tooltips + secondary panes.** Chevron rotation, reveal pulse, gauge fill animation, live pip; Axis font handles; `PushTooltip`; Moonlit / Flight / Characters left lists on gauge rows; Help / Todo / Discovery on `Chrome`. | Reduce motion toggle | Everything here is additive; can trail by a release. |

Each increment ends with the smoke checklist at UiScale 0.9 / 1.15 / 1.6 and IconScale 0.8 / 1.25 / 2.0, plus a screenshot pair (Classic vs new) for the changelog.

---

## 7. Mockup notes

`docs/design/mockups/main-window.html` renders the Journal tab with the proposed toolbar, tab strip, tree (gauges at 0.10, 0.35, 0.50, 0.90, 1.00), a table with one row per state (plus hover and selected rows), the detail card stack and the status bar. Moons are inline SVG built from a base disc and a lit lens (the same two-disc construction `MoonGeometry` uses), so what you see is what ImDrawList can draw. A strip under the window repeats the five fractions at 12 / 16 / 24 / 32 px with and without the arc, and shows the current `#2C334A` disc next to the proposed Shadow + ring, so legibility at each size can be judged before anything is coded.

It is not pixel-exact ImGui: the font is Noto Sans from Google Fonts (Dalamud's default is also Noto Sans), and the browser anti-aliases curves a little better than ImGui's polygon fill does at 12 px.

## 8. Open questions for the owner

1. Vertical tab strip (as mocked) or an icon-only rail to keep the tree taller?
2. Whole-window Night chrome by default, or keep Dalamud's colours and only card the panes?
3. Presets on the toolbar (as mocked) or leave them in the filter panel?
4. Cards mode with banner thumbnails: worth increment 3's extra work, or drop it?
