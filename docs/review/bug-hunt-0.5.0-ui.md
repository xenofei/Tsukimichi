# Tsukimichi 0.5.0 — UI layer bug hunt (report only)

Scope: `Tsukimichi/Ui/*`, `Tsukimichi.Core/Ui/*`, and the memo behaviour of `Tsukimichi.Core/Query` as used by `Ui/QueryRunner.cs`.
Written incrementally, file by file. "Confirmed" = follows directly from the code as read; "Plausible" = depends on a runtime detail I could not verify from source alone.

Severity scale: crash / wrong display / perf / polish.

---

## Shared infrastructure (UiMetrics, UiRects, UiState, Theme, EmptyState, UiFormat)

No defects found. Notes that matter for the panes:

- `UiMetrics` font-scale contract (class remarks): ImGui multiplies a window's font scale by its *direct* parent's only (`ImGuiWindow::CalcFontSize` reads `ParentWindow->FontWindowScale`, one level). So a grandchild (`ImRaii.Child` inside a pane inside `##center`), a scrolling table's inner window, and a popup opened from a direct child of the main window are all drawn at scale 1 unless they call `UiMetrics.ApplyFontScale()` themselves. Tooltips have no parent window and must always call it. This is checked per pane below.
- `UiState.Reveal` clears narrowing filters and raises `FiltersChanged`, then `MarkQueryDirty()` — good.

## Ui/QueryRunner.cs + Core/Query memo keys

Memo keys covered: catalog instance, `session.Version`, `ui.QueryVersion`, `ui.Scope`, `ui.Sort`, debounced search, `FilterSet.Equals` (cheap: bools + small dictionaries), Stalled hour bucket. Pins changes and viewed-character changes call `MarkQueryDirty()`. `SearchIndex.For` is a `ConditionalWeakTable` keyed by catalog — no rebuild per frame.

### QR-1 (Confirmed, wrong display) — `StalledDays` setting is a query input but not a memo key
`Ui/QueryRunner.cs:368` passes `plugin.Settings.StalledDaysClamped` into `QueryContext`, but nothing in `Update()` (lines 191-199) compares it to the value the last run used. Changing the slider in ConfigWindow while the Stalled preset is active leaves the rows stale until some other key changes (up to an hour via the `stalledHour` bucket). Verified ConfigWindow does not call `MarkQueryDirty` for it (see ConfigWindow section).
Fix: keep `private int ranStalledDays;` next to `ranStalledHour`, include `|| plugin.Settings.StalledDaysClamped != ranStalledDays` in `dirty`, or have ConfigWindow bump `ui.MarkQueryDirty()`.

### QR-2 (Plausible, polish) — search debounce edge when text is restored to the applied value
`Update()` line 167: `!searchDirty && pending != applied && elapsed >= 150ms`. If the user types "a" then deletes back to "" the clear path applies immediately (good). If they type "ab" then backspace to "a" while "a" was the applied text, no re-run is needed — correct. No defect; noting the path was checked.

### QR-3 (Confirmed, perf-neutral) — `filtersSnapshot.Equals(ui.Filters)` runs every frame
Cost is ~15 field compares plus three small dictionary walks; acceptable. No allocation.

## Ui/MainWindow.cs

### MW-1 (Confirmed, wrong display / layout) — window minimum size ignores `UiScale`
`MainWindow.cs:117` `MinimumSize = (800, 500)` is in Dalamud-global-scaled units only. Left column is `Px(240)` and right column `Px(360)` (`UiMetrics.cs:78-79`), both multiplied by `UiScale`. At `UiScale = 1.5` the fixed columns need 900 px while the window can shrink to 800 px: the stretch centre column collapses to zero/negative width and the quest table becomes unusable (ImGui clamps, but the centre shows only a sliver). At `UiScale = 1.6` (the clamp max) it is 960 px.
Fix: recompute `SizeConstraints.MinimumSize = new Vector2(MinWidth, MinHeight) * UiMetrics.FontScale` at the top of `Draw()` (after `UiMetrics.Update`), or size the min from `LeftColumnWidth + RightColumnWidth + Px(200)`.

### MW-2 (Plausible, wrong display) — sync tooltip cached on `session.Version` but the glyph reads `PollerHealthy` live
`MainWindow.cs:396-417`: `syncTooltip` is rebuilt only when `session.Version` changes; `MainWindow.cs:512` colours the glyph from `session.PollerHealthy` every frame. If the poller pauses/resumes without bumping `Version` (Runtime was reviewed elsewhere; I did not confirm whether `PollerHealthy` bumps it), the glyph goes grey while hovering still says "Live". Fix: include `session.PollerHealthy` in the toolbar cache key.

### MW-3 (Confirmed, polish) — status bar text is one unbroken line
`MainWindow.cs:779-784`: `TextDisabled(status)` followed by `SameLine(0,0)` + MSQ text; at the 800 px minimum with UiScale 1.3+ the MSQ segment runs past the window edge and is clipped with no ellipsis or tooltip on the main status string. Low priority.

### MW-4 (Confirmed, OK) — programmatic tab switch
`DrawNavigation`/`DrawTab` (lines 681-709) handle the one-frame `SetSelected` latency correctly; a request is re-issued until the drawn tab matches. No drop found.

### MW-5 (Confirmed, OK) — Push/Pop
`Draw()` resets `SetWindowFontScale(1f)` in `finally`. `DrawBody` table has 3 `TableSetupColumn` for 3 columns. `DrawNavigation` disposes `bar` before `left`. All good.

## Ui/TablePane.cs

### TP-1 (Confirmed, UX) — selected row does not survive the filter that hides it; nothing tells the user
Not a table bug per se, but `ScrollToExternalSelection` (line 567) only scrolls when `SelectedRowId` changes; when the query re-runs and the selected row is no longer in `rows`, the detail pane still shows it while the table shows no highlight. Acceptable, but see DP notes.

### TP-2 (Confirmed, OK) — clipper, sort specs, popup scope
- Clipper is fed a constant `rowHeight` and every row is laid out to exactly `RowContent + 2*CellPadding.Y` (Dummy/Selectable sized to `RowContent`, `CenterText` offsets, icon offset). Consistent.
- 7 columns / 7 `TableSetupColumn`. `TableSetupScrollFreeze(0,1)` before headers. OK.
- `ContextPopupItem("##ctx")` is inside `PushId(rowId)` and after the row `Selectable`, so it attaches to that item. The popup's parent window is the table inner window which already called `ApplyFontScale()` (line 128), so the menu inherits the scale. OK.
- `ApplySortSpecs` resets `SpecsDirty` and only writes `ui.Sort` on change. OK.
- `IsItemHovered()` at line 284 after the popup `using` block: while the popup is open the "last item" is the popup's last menu item, but the hover test requires the hovered window to be the table window, so the name tooltip simply does not show while the menu is open. Harmless.

### TP-3 (Confirmed, polish) — `ImRaii.PushIndent(12f)` in `DrawEmpty` (line 504) is not scaled through `UiMetrics.Px`.

### TP-4 (Confirmed, UX) — persisted sort vs imgui.ini
`TablePane.cs:139` hands the config sort to ImGui only as `DefaultSort` on the first frame, and ImGui's own `.ini` table settings win when present. On the first frame `SpecsDirty` is true, so `ApplySortSpecs` then overwrites `ui.Sort` with whatever ImGui restored. Net effect: `Configuration.SortColumn/SortDescending` are written every session but never actually restore anything once imgui.ini has a sort for `##quests`. Harmless today (both stores agree), but a future "reset sort" in Settings would not work without also `ImGui.TableSetColumnSortDirection` / clearing the ini entry.

## Ui/TreePane.cs

### TR-1 (Confirmed, UX) — Reveal selects a genre whose parents may be collapsed; nothing opens them
`TreePane.DrawNode` (line 83) never calls `ImGui.SetNextItemOpen`. `UiState.Reveal` (Moonlit/Flight/Characters links, MSQ status click, chat) sets `ui.Scope = Genre(x)`; if that genre's section/category is collapsed the tree shows no selected row and no scroll. The user sees the table scoped to something without a visible anchor in the tree.
Fix: when `ui.Scope` changes to a scope not equal to the last drawn one, walk `sections` for the ancestor chain of the new scope and call `SetNextItemOpen(true)` on each ancestor for that frame, then `SetScrollHereY` on the selected node.

### TR-2 (Confirmed, OK) — Push/Pop
`TreePop` only when `open && !Leaf`, and leaves use `NoTreePushOnOpen`. `PushClipRect`/`PopClipRect` paired. OK.

### TR-3 (Confirmed, polish) — per-frame `CalcTextSize` per node
Line 133; ~100 nodes at most when everything is expanded, no allocation. Fine.

> **Correction to QR-1** (after reading FilterPanel + ConfigWindow): the only Stalled-days slider is `FilterPanel.cs:181-185` and it calls `changed()` -> `MainWindow.OnFiltersChanged` -> `MarkQueryDirty()`. ConfigWindow has no copy. So QR-1 is **not a live bug**; downgrade to a fragility note: `QueryRunner` reads `plugin.Settings.StalledDaysClamped` (line 368) without keying on it, so any future writer of `StalledDays` that forgets `MarkQueryDirty` produces stale rows. Cheapest hardening: add `ranStalledDays` to the dirty check.

## Ui/FilterPanel.cs

### FP-1 (Confirmed, wrong display at UiScale != 1) — `ImGui.Combo` popups opened from a direct child are unscaled
`FilterPanel.cs:526` `ImGui.Combo("##kind", ...)` (one per reward kind) is drawn directly in the `##left` child. The simple `ImGui.Combo` opens its own popup window whose parent is `##left`; `##left` has its own `FontWindowScale` of 1 (it inherits the main window's scale only through the one-level parent link), so the dropdown list renders at Dalamud scale while the preview box is at UiScale. The author already handles this for `ImRaii.Combo("##job")` (line 490) and the overrides popup (line 392); the reward-kind combos were missed. Same pattern: `MoonlitPane.cs:245` (`##moonlitConfidence`, from `##center`) and `CharactersPane.cs:828` (`ImRaii.Combo("##compareWith")`, from `##center`, no `ApplyFontScale` inside).
Repro: UiScale 1.6, open Filters > Advanced > any reward-kind dropdown: list items are visibly smaller than the preview.
Fix: replace `ImGui.Combo` with `ImRaii.Combo` + `UiMetrics.ApplyFontScale()` + `Selectable` loop (the pattern at lines 482-499), or a shared `UiMetrics.ScaledCombo(...)` helper; add `ApplyFontScale()` after `if (!combo) return;` in `DrawCompareCombo`.

### FP-2 (Confirmed, perf-low) — closures allocated every frame
`Draw` (lines 92-107) and `DrawChips` (lines 257-324) build 10-20 lambdas capturing `f`/`ui` per frame while the panel or any chip is visible. Small, but the codebase's stated contract is "the body allocates nothing". Fix: static setters taking `FilterSet`, or an enum + switch.

### FP-3 (Confirmed, OK)
- Override popup: `PushId(popupId)` wraps both `OpenPopup` and `ImRaii.Popup`; two toggles use distinct ids; disposal order (popup, then id) is right.
- Chip ids are `PushId(label)`; labels distinct. `DrawLevelRange` maps 100 -> `NoLevelMax`. Preset chips wrap by measurement.

### FP-4 (Confirmed, polish) — `ImRaii.PushIndent(8f)` at line 98 not scaled via `UiMetrics.Px`.

## Ui/DetailPane.cs

### DP-1 (Confirmed, destructive-action guard; owner request) — "Mark as unique..." has no modifier-key guard
`DetailPane.cs:538-563`: a plain `SmallButton` opens a popup with a note field and a confirm button. Two clicks, but no modifier. Concrete change: `var io = ImGui.GetIO(); var held = io.KeyShift || io.KeyCtrl;` then `using (ImRaii.Disabled(!held)) SmallButton(...)` with the tooltip reading "Hold Shift to mark..." when not held. Apply the same to `Strings.MoonlitMarkNotUnique` at `MoonlitPane.cs:465`, which today is a single un-guarded `MenuItem` that hides the row instantly (MP-3).
Popup polish: `InputTextWithHint("##uniqueNote")` gets no `SetKeyboardFocusHere()` on `IsWindowAppearing()`, and Enter does not confirm (no `EnterReturnsTrue`).

### DP-2 (Confirmed, discoverability) — reverting manual marks
- Per-quest revert exists in two places: "Restore" at `DetailPane.cs:517` (only while that quest is selected) and "Restore shipped verdict" at `MoonlitPane.cs:460` (only for a row still in the table).
- A quest marked "Not unique (hide)" is dropped from `UniqueRewardCatalog` (`Tsukimichi.Core/Unique/UniqueRewardCatalog.cs:160-162`, `if (!verdict.Unique)` -> excluded), so its row is gone and the context-menu Restore is unreachable for exactly the override it is meant to undo. The only way back: know the quest name -> Journal search -> detail pane -> Restore. Nothing lists the user's overrides; `ConfidenceFilter.Yours` only shows *additions*.
- No Settings-level list or "revert all"; the only bulk revert is "Delete all data", which also removes snapshots and pins (`Strings.ConfigDeleteStep1Text`).
Recommendation: Settings > Data: "Your Moonlit verdicts (N)" table (quest, verdict, note, Restore) plus "Restore all" behind a Shift guard; and/or let `ConfidenceFilter.Yours` include hidden rows (struck-through) so the row context menu can restore them.

### DP-3 (Plausible, wrong display) — banner squashed instead of cropped
`DetailPane.cs:309-312`: `height = min(BannerMaxHeight, width * H/W)` then `ImGui.Image(handle, (width, height))` with default UVs. When the clamp triggers (right column ~`Px(344)` wide, `BannerMaxHeight = Px(200)`: any banner taller than ~1.72:1, or a narrower right column after the user drags the splitter) the texture is drawn non-uniformly scaled. Fix: crop with `uv0/uv1` when clamping.

### DP-4 (Confirmed, polish) — `DrawSpecialBadge` tooltip fires through overlapping windows
`DetailPane.cs:359` uses `IsMouseHoveringRect` with no `IsWindowHovered()`; the badge tooltip shows even when a popup, the tutorial card or another plugin window covers it. Fix: `&& ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows)`.

### DP-5 (Confirmed, UX) — Path / Unlocks / Chain-next clicks bypass `Reveal`
`DetailPane.cs:294, 651, 704` set `ui.SelectedRowId` directly. `TablePane.ScrollToExternalSelection` scrolls only if the row is in the current `rows`; when the clicked step is in another genre than the current scope (common along a path) the table shows no highlight and no scroll. Consider `ui.Reveal(...)` (as Moonlit/Characters do) or a "Show in Journal" affordance.

### DP-6 (Confirmed, missing hover affordance) — path and unlock moons have no state tooltip
`BeginGlyphLine` (`DetailPane.cs:659-678`) and `DrawUnlocks` (`:698-700`) draw `Dummy` + moon but never `IsItemHovered` -> tooltip, unlike the requirements moons (`:433-436`) and every Moonlit/Flight glyph. Fix: after the `Dummy`, `if (ImGui.IsItemHovered()) UiMetrics.Tooltip(Strings.StateName(state))`. The large header moon on the banner (`:334`) has none either (state text below mitigates).

### DP-7 (Confirmed, OK)
- Push/Pop: `PushNightPanel` + `Child("##detail")` both `using var`; early returns safe. `ChannelsSplit/Merge` paired.
- Popup scope: `OpenPopup(MarkUniquePopup)` and `ImRaii.Popup` in the same scope; `ApplyFontScale()` applied (line 556).
- Ids: path steps/unlocks `PushId(rowId)`; folded runs `PushId(runIndex)` (0..n) cannot collide with row ids (>= 65536).
- Model refresh keyed on (rowId, session.Version, bundle, pinned); overrides read live so Mark/Restore show immediately.
- `ScrollToPath` two-frame `SetScrollHereY` is a correct workaround for ImGui's previous-frame content-size clamp.

## Ui/MoonlitPane.cs

### MP-1 (Confirmed, wrong display at UiScale != 1) — confidence combo popup unscaled
`MoonlitPane.cs:245` `ImGui.Combo("##moonlitConfidence", ...)` in `##center`: see FP-1.

### MP-2 (Confirmed, missing hover affordance; owner-flagged) — `DrawIcon` has no tooltip
`MoonlitPane.cs:436-447`: `ImGui.Image` for the reward icon, and a filling-moon stand-in when `Icon == 0`, neither with `IsItemHovered`. The reward-name cell has no tooltip either, so nothing in the row gives the item description that `RewardTooltip.Draw` gives in the detail pane and the journal table. Fix: after `DrawIcon`, `if (ImGui.IsItemHovered())`: find the `RewardRef` in `row.Quest.Rewards` matching `row.Entry` (ItemId, else Kind+Id — `MoonlitIconResolver.FromQuestRewards` already does this match) and call `RewardTooltip.Draw(reward, links, textures)`, else `UiMetrics.Tooltip(row.Name)`. `MoonlitPane` has no `GameLinks` today; inject one.
Also: the `Icon == 0` stand-in is a *filling* moon at 1.0/0.0 — a second copy of the Obtained column in a different glyph language; the class doc says titles/system unlocks "keep the veiled moon (0)". A veiled moon would be consistent.

### MP-3 (Confirmed, destructive action without guard) — "Not unique (hide)" is one click, immediate, and its own undo disappears with the row
`MoonlitPane.cs:465-468`: `MenuItem` -> `SetOverride(rowId, false, null)` -> file write -> `catalogDirty` -> row removed next `Refresh`. No confirm, no modifier, no toast; the row's "Restore shipped verdict" (line 460) can never be shown for it afterwards. See DP-2. Fix: Shift gate with the tooltip saying so; keep hidden rows reachable under `ConfidenceFilter.Yours`.

### MP-4 (Plausible, stale display) — obtained state keyed on `session.Version` only
`RefreshObtained` (line 484) re-reads `unlocks.IsObtained` only when `session.Version` changes. Obtaining a reward outside a quest completion (buying the emote, unlocking the mount later) does not by itself bump the version (driven by quest-state polling); the Obtained moon stays "No" until some other quest state changes. FlightPane solved the same problem for aether currents (`AttunementsMayHaveChanged`: visibility/territory/5 s); Moonlit has no equivalent.

### MP-5 (Confirmed, UX) — row highlight not synced with the global selection
`selectedRow` (line 92) is local; selecting a quest from the detail pane's path, Flight or chat leaves the Moonlit highlight on the previously clicked row. `BuildRows` resets it to -1, so after any override the highlight vanishes although the detail pane still shows that quest. Fix: `selected = ui.SelectedRowId == row.Entry.QuestRowId` (FlightPane's approach).

### MP-6 (Confirmed, polish)
- Reward `Selectable` (line 377) does not `SpanAllColumns`; clicking Kind/State/Confidence cells does nothing, and the context menu only opens on the name.
- Left-column filling moons (line 346) have no tooltip in the normal case (count text mitigates).
- Filter `InputTextWithHint` has no clear button; session-only (not claimed persisted).

### MP-7 (Confirmed, OK)
- Table: 6 columns / 6 `TableSetupColumn`; `TableSetupScrollFreeze` before headers; `ApplyFontScale()` inside the ScrollY inner window (line 299) so the row context popup inherits it.
- Clipper without explicit height: rows are laid out identically (glyph Dummy of `InlineGlyphSize(line)`, icon `RowIconSize`), so ImGui's first-row measurement holds.
- `VisibleKey` readonly record struct; `filterText` compared by value; no per-frame allocation.
- Overrides reloaded on `DataDeleted`; catalog rebuilt lazily.

## Ui/FlightPane.cs

### FL-1 (Confirmed, OK) — no defects found
- Tables 3/3 and 5/5; no ScrollY (scrolls with `##center`), so no inner window to scale.
- `AttunementsMayHaveChanged` frame guard dedupes the two `Refresh` calls per frame; 5 s live refresh plus territory/visibility triggers.
- `SyncSelection` honours an external `ui.FlightTerritoryId`, falls back to current zone, then first zone.
- Every glyph in both columns has a tooltip. Compass `ImGui.Image` (line 356) has none, the text beside it does.

### FL-2 (Confirmed, polish) — header moon (`DrawHeader`, 190-199) has no tooltip.

## Ui/ConfigWindow.cs

### CW-1 (Confirmed) — destructive-action inventory
| Action | Location | Guard today |
|---|---|---|
| Delete all data | `ConfigWindow.cs:462-517` | destructive-styled button -> modal 1 (explains) -> modal 2 ("cannot be undone") -> destructive confirm. Adequate. |
| Forget character | `CharactersPane.cs:697-760` | disabled while live (tooltip says why) + destructive style + modal confirm naming the character. Adequate. |
| Reset todo position | `ConfigWindow.cs:395`, `TodoOverlay` header menu | none; reversible. Fine. |
| Clear pins | not found anywhere in Ui (pins are removed one at a time via Pin/Unpin, or all via Delete all data) | n/a |
| Not unique (hide) | `MoonlitPane.cs:465` | none (MP-3) |
| Mark as unique... | `DetailPane.cs:538` | popup + confirm button, no modifier (DP-1) |
| Restore / Restore shipped verdict | `DetailPane.cs:517`, `MoonlitPane.cs:460` | none; non-destructive apart from losing the note. Fine. |

### CW-2 (Confirmed, by design) — settings window does not apply `UiMetrics.ApplyFontScale()`; UiScale is documented as the main window's scale. `ImGui.SetTooltip` is used here instead of `UiMetrics.Tooltip`; consistent.

### CW-3 (Confirmed, OK) — Push/Pop
`DrawTodoOverlay` early-returns inside `using var indent/disabled`; both dispose. Modal pair uses one scope for `OpenPopup` and `PopupModal`; `openSecondConfirm` bridges the frame between the two modals correctly.

## Ui/MoonGlyph.cs

### MG-1 (Confirmed, OK)
`DrawInline`/`DrawFillingInline` reserve a `Dummy` so `IsItemHovered` works after them. Static scratch lists are only touched from the draw thread. No per-frame allocation.

## Ui/CharactersPane.cs

### CP-1 (Confirmed, wrong display at UiScale != 1) — compare combo popup unscaled
`CharactersPane.cs:828-853`: `ImRaii.Combo("##compareWith")` opened from `##center`, no `UiMetrics.ApplyFontScale()` after `if (!combo) return;`. See FP-1.

### CP-2 (Confirmed, stale display + I/O) — dashboard "Pinned" section reads `user/pins.json` from disk and ignores the live pin set
`BuildPinned` (`CharactersPane.cs:1757-1799`) calls `PinsFile.Load(paths.PinsFile)` every dashboard rebuild (session version change, viewed character change, or the once-a-minute tick, line 1397). `QueryRunner.TogglePin` marks the *query* dirty and saves the file after a 1 s debounce (`QueryRunner.cs:24, 207-210`) but does not touch `session.Version`, so: (a) pinning/unpinning a quest in the Journal then switching to Characters shows the old list for up to a minute, (b) within the 1 s debounce even a rebuild reads the pre-toggle file, (c) a synchronous file read on the draw thread once a minute while the tab is open. Fix: give the pane `runner.Pinned` (`IReadOnlySet<uint>`) plus a pins version counter and add it to `DashboardKey`; drop the file read.

### CP-3 (Confirmed, missing hover affordance) — job icons have no tooltip
`DrawJobIcon` (`CharactersPane.cs:648-658`) draws `ImGui.Image` with no `IsItemHovered`. In `DrawJobs` the *name* cell has the abbreviation tooltip (line 638-641) but the icon does not; in `DrawJobQuests` (line 333) neither icon nor name has one. Fix: `if (ImGui.IsItemHovered()) UiMetrics.Tooltip(name)` after the image, or wrap icon+name in a `Group`.

### CP-4 (Confirmed, polish) — section / job-ladder / chain filling moons have no tooltip
`DrawSections:276`, `DrawJobQuests:350`, `DrawChainTable:409`, `DrawMoonlitSummary:485` (normal case). Count text sits beside each, so low priority; the Moonlit summary's AllUnknown case does have one.

### CP-5 (Confirmed, polish) — "Not started (N)" tree node id changes with N
`DrawChains:377` `ImRaii.TreeNode(d.ChainsNotStartedLabel)`; the label carries the count, so ImGui's open/closed state resets whenever a chain starts. Use `"Not started (N)###chainsNotStarted"`.

### CP-6 (Confirmed, OK)
- All 11 tables have matching column counts; row ids via `PushId(i)`; `DrawDiffList` wraps the shared `##diff` table id in `PushId("onlyViewed"/"onlyOther")`.
- Forget flow: disabled while live, destructive style, modal confirm; modal applies `ApplyFontScale()`; caches cleared and `MarkQueryDirty()` after forgetting.
- Dashboard/compare/account memo keys are sound (version, content id, capture time, bundle, rewards catalog reference).
- `RefreshItems` and `RefreshDashboard` rebuild once a minute (ages tick) — bounded allocation.

> **Correction to MW-2**: `SessionState.SetPollerHealthy` (`Game/SessionState.cs:310-318`) calls `Bump()`, so the toolbar strings are rebuilt when the poller state flips. Retracted. Likewise `ConfigWindow`'s ShowUnlisted callback is wired to `ui.MarkQueryDirty()` (`Plugin.cs:333`) and the tree reads `Settings.ShowUnlisted` live, so that pair is consistent.

## Ui/TodoOverlay.cs

### TO-1 (Confirmed, wrong display at UiScale/IconScale != 1) — moons scale with the main window's Icon/UI scale, text does not
`TodoOverlay.cs:208` `glyphSize = UiMetrics.InlineGlyphSize(lineHeight)` = `max(line, RowGlyphRadius / 0.42)` where `RowGlyphRadius = Icon(6) = global × UiScale × IconScale × 6`. The overlay's text is at Dalamud scale only (no `ApplyFontScale`). At the defaults (1.15 × 1.25) the moon box is ~20.5 px against a ~17 px line; at UiScale 1.6 / IconScale 2.0 it is ~46 px per row, i.e. every todo row is nearly three text lines tall with a tiny label beside a large moon. `DiscoveryWindow.cs:242` has the identical mismatch (`UiMetrics.InlineGlyphSize` with `ImGui.SetTooltip`, no font scale). `HoverHint` is consistent because it calls `ApplyFontScale()` (line 227).
Fix: either apply `UiMetrics.ApplyFontScale()` in these windows (then the moon/text ratio is the intended one), or size the glyph from the local line height only (`lineHeight * 1.2f`) since these windows are documented as Dalamud-scale.

### TO-2 (Confirmed, polish) — tooltip scale differs from the window
`UiMetrics.Tooltip` (used at lines 250, 305) applies UiScale to the tooltip while the overlay body is unscaled; at UiScale 1.6 the tooltip text is 60 % larger than the row it explains. Same root cause as TO-1.

### TO-3 (Confirmed, OK)
- Pins are re-read from disk on a 2 s write-time poll; `QueryRunner` saves after 1 s, so a pin shows in the overlay within ~3 s. Documented; acceptable.
- `SetWindowFontScale(0.75f)` for the lock glyph is restored on the same line. Header/row context popups have distinct ids; `PushId(index)` per row.
- `CollapsingHeader` ids use `###todo{Section}` so the open state survives count changes.
- `RespectCloseHotkey = false`; locked mode only takes NoMove/NoResize.

## Ui/HelpWindow.cs

### HW-1 (Confirmed, OK) — no defects found
Rail child + Night content child both `using`; `SetWindowFontScale(TitleScale)` restored; `ChannelsSplit/Merge` paired in `Card`, `FillingCard`, `Tip`; `PushId` per card/step/tip so repeated block ids do not collide; chips wrap by measurement; search text is prebuilt once and filtered without allocation. The window is Dalamud-scale by design.

### HW-2 (Confirmed, polish) — a searched-away topic stays shown
`DrawRail` hides non-matching topics but `topic` is unchanged, so the content pane can show a topic the rail no longer lists. Harmless.

## Ui/DiscoveryWindow.cs

### DW-1 — see TO-1 (glyph/text scale mismatch, line 242).

### DW-2 (Confirmed, OK)
5/5 columns per table; one clipper reused sequentially for two tables in one window (legal); `###nearbyAccepted` stable header id; cog popup opened and begun in the same scope; `Same()` dedupes rebuilds without allocation.

## Ui/TutorialOverlay.cs

### TU-1 (Confirmed, polish) — one-frame highlight flicker on tab-changing steps
`GoTo` sets `ui.Tab` for steps 11-13; MainWindow applies `SetSelected` a frame late, so on the frame the step changes the old tab's rects are recorded and the new step's keys (`moonlit.kinds`, `characters.dashboard`, `flight.table`) are missing. `TryTarget` then falls back (or finds nothing) for one frame: the dim has no hole and the card is placed by `CenterIn`, then jumps beside the target. Fix: skip drawing the highlight/card for the first `CardSettleFrames` after a step change (the card already waits two frames for its size), or record the target from the previous frame's rects.

### TU-2 (Confirmed, OK)
`ImGui.Begin` is paired with `End` in `finally`; colour/style pushes are `using var` disposed after `End`. Esc is handled only while the card is focused (documented); MainWindow suspends its close hotkey during the tour. `stackalloc` spans; no per-frame allocation. `Complete()` persists `TutorialCompleted`.

## Ui/HoverHint.cs

### HH-1 (Confirmed, OK)
`NoInputs | NoNav` are correct for a passive hint. `Begin`/`End` paired; conditional `Alpha` push for the settle frames; model memoized on (item, version, lookup). `Indent/Unindent` paired.

### HH-2 (Confirmed, dead code) — `Listed(uint)` (line 172) is unused.

## Ui/GameLinks.cs

### GL-1 (Confirmed, OK)
`CanOpenJournal` pattern (`state is A or B or C`) parses as intended. Every game call is wrapped; sheet lookups cached per id. `NearestAetheryte` is evaluated per visible row per frame in FlightPane/DiscoveryWindow/context menus — a small linear scan per zone; acceptable.

## Ui/GlyphDebugWindow.cs

### GD-1 (Confirmed, OK) — developer window; tables 7/7 and 6/6 columns; string interpolation per frame is fine here.

## Tsukimichi.Core/Ui (ScaleMetrics, OverlayGeometry, MoonGeometry)

### CU-1 (Confirmed, OK) — pure arithmetic; `Cutout` degrades gracefully on capacity overflow (leaves the hole undimmed rather than throwing); `PlaceCard` clamps into bounds.

## Cross-cutting

### XC-1 (Plausible, wrong display after changing IconScale) — ImGui restores saved pixel widths for the icon-derived columns
`TablePane` (`##quests`: glyph and rewards columns) and `MoonlitPane` (`##moonlitTable`: two glyph columns) are `Resizable` tables without `NoSavedSettings`, so once the user has resized *any* column ImGui writes every column's width to imgui.ini and restores them on later launches, overriding the `TableSetupColumn` widths — including the `NoResize` ones that are computed from `IconScale`. ImGui rescales saved widths by font-size ratio (`RefScale`), which tracks UiScale but not IconScale. Symptom: raise IconScale from 1.25 to 2.0, the Rewards column stays ~85 px and shows 2-3 clipped icons; the glyph column clips the moon. Fix: after `TableHeadersRow`, if `ImGui.TableGetColumnWidth(i)` differs from the computed width for the two `NoResize` columns, call `ImGui.TableSetColumnWidth(i, width)`; or give those columns `ImGuiTableColumnFlags.NoSavedSettings`-equivalent by recomputing each frame.

### XC-2 (Confirmed, UX) — two "unlisted" switches
`Configuration.ShowUnlisted` (Settings) controls the tree node; `FilterSet.IncludeUnlisted` (Filters panel) controls whether unlisted rows join "All quests". With Settings off and the filter on, unlisted quests appear in the table with no tree node to explain them (`TreePane.cs:70` shows the node only when scoped there). Consider one switch, or have the tree show the node whenever `Filters.IncludeUnlisted` is on.

### XC-3 (Plausible, low) — character combo duplicate labels
`MainWindow.cs:577-583` uses `Selectable(label)` with labels built from name + world; two stored snapshots with the same name and world (a deleted-and-recreated character with a new ContentId) collide on ImGui id and the second entry cannot be selected. Fix: `PushId(id)` per entry.

### XC-4 (Confirmed, polish) — keyboard
No shortcut focuses the search box; `/tsukimichi <text>` fills it without focusing; the Mark-unique popup does not focus its text field or accept Enter. Tutorial has no keyboard Next/Back.

---

## Ranked summary

**Confirmed, fix before release**
1. **MW-1** `MainWindow.cs:117` — window minimum width ignores UiScale; at UiScale >= 1.5 the fixed side columns (`Px(240)` + `Px(360)`) exceed the 800 px minimum and the quest table collapses. Fix: scale `MinimumSize` by `UiMetrics.FontScale` each frame.
2. **FP-1 / MP-1 / CP-1** `FilterPanel.cs:526`, `MoonlitPane.cs:245`, `CharactersPane.cs:828` — combo popups opened from a direct child of the main window render at Dalamud scale, not UiScale. Fix: `ImRaii.Combo` + `UiMetrics.ApplyFontScale()`.
3. **TO-1 / DW-1** `TodoOverlay.cs:208`, `DiscoveryWindow.cs:242` — glyph size follows UiScale × IconScale while the window's text does not; at max scales todo rows are ~46 px tall. Fix: apply the font scale in those windows or size glyphs from the local line height.
4. **MP-3 + DP-1 + DP-2** — "Not unique (hide)" is one un-guarded click that removes its own undo path; "Mark as unique..." has no modifier guard; no list of the user's overrides and no "revert all" short of "Delete all data". Fix: Shift/Ctrl gate on both, keep hidden rows reachable under the "Yours" confidence filter, add a Settings > Data verdicts table with Restore / Restore all.
5. **CP-2** `CharactersPane.cs:1757` — dashboard Pinned section reads `user/pins.json` from disk and is not invalidated by `TogglePin`; stale for up to a minute and a sync file read on the draw thread. Fix: pass `runner.Pinned` + a pins version into the pane.
6. **TR-1** `TreePane.cs:83` — `Reveal` selects a genre whose ancestors may be collapsed; nothing opens or scrolls to it. Fix: `SetNextItemOpen` on the ancestor chain when the scope changes externally, then `SetScrollHereY`.
7. **MP-2 / CP-3 / DP-6** — missing hover affordances: Moonlit reward icon (`MoonlitPane.cs:436`), job icons (`CharactersPane.cs:648`), path/unlock moons (`DetailPane.cs:659, 698`). Fix: `IsItemHovered` -> `RewardTooltip.Draw` / `UiMetrics.Tooltip`.
8. **MP-5** `MoonlitPane.cs:92` — row highlight is local state, desyncs from `ui.SelectedRowId` and is wiped on every override.

**Plausible (needs an in-game check)**
9. **XC-1** — imgui.ini-restored column widths override IconScale-derived `NoResize` column widths in `##quests` and `##moonlitTable` after the user has resized any column.
10. **MP-4** `MoonlitPane.cs:484` — Obtained state only refreshes on `session.Version`; rewards obtained outside a quest completion stay "No" until another quest state changes (FlightPane already has the refresh pattern to copy).
11. **DP-3** `DetailPane.cs:309` — banner is squashed, not cropped, when the height clamp triggers.

**Polish / low**
12. DP-4 badge tooltip through overlapping windows; DP-5 path/unlock/chain clicks bypass `Reveal`; TU-1 one-frame tutorial flicker on tab steps; CP-5 tree-node id includes the count; FP-2 per-frame closures; FP-4/TP-3 unscaled `PushIndent`; TO-2 tooltip scale; HW-2; HH-2 dead `Listed`; XC-2 two unlisted switches; XC-3 combo label collision; XC-4 keyboard; MW-3 status-bar overflow; TP-4 config sort never actually restores once imgui.ini has one.

**Retracted after verification**: QR-1 (Stalled-days slider does mark the query dirty), MW-2 (`SetPollerHealthy` bumps the version).

**Verified clean**: Push/Pop and Begin/End pairing in every pane (ImRaii `using` throughout; the two manual `ImGui.Begin` sites use try/finally); all 20+ tables have matching `TableSetupColumn` counts; both clippers are fed constant-height rows; popups are opened and begun in the same id scope everywhere; sort-spec handling resets `SpecsDirty` and only writes on change; QueryRunner/TreeCounts memo keys cover catalog, session version, query version, scope, sort, filters (by value), debounced search, pins and viewed character; FlightPane, HelpWindow, TutorialOverlay, HoverHint, GameLinks, GlyphDebugWindow and Core/Ui have no defects.
