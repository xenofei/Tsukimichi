# Tsukimichi — Design Specification

Date: 2026-09-27
Status: approved for planning (owner approved the feature panel; decisions D-1..D-9 adopted per their recommendations, see §13)
Inputs: docs/feasibility-report.md, docs/feature-panel.md

## 1. Identity and premise

**Name.** Tsukimichi (月道) reads as "the moon's path": *tsuki* (moon) from the character's family name Tsukikage (月影, moon-shadow or moonlight) and *michi* (path, road). Michiru (満る) means "to fill", as in a moon filling toward full. The plugin is the road you walk by moonlight: every quest is a step on the path, and the moon fills as you complete it.

**Premise carried into the product.**
- A quest's state is a **moon phase**. The eight-state model from the feature panel maps one-to-one onto phases, so the state glyph carries meaning without text.
- The prerequisite chain is **the path**: a vertical trail of moons from the target quest back to the first step, lit where done and dark where not.
- Progress is **the moon filling**: category headers show a small moon whose lit fraction is the completion ratio.
- The unique-rewards category is **Moonlit** treasures: things you can only find on this road.

**Voice.** Calm, precise, short. Labels are nouns. Status text explains the gap in one clause ("needs Sworn, you are Trusted"). No exclamation marks. No cute copy on error states.

## 2. Visual language

### 2.1 State glyphs (procedural, drawn with ImDrawList)

| State | Phase | Drawing |
|---|---|---|
| Completed | Full moon | Filled disc, moon-gold |
| Accepted | Waxing gibbous | Disc lit ~75%, gold, with a thin ring |
| Ready | First quarter | Right half lit, gold; subtle outer glow |
| ReadyOnOtherJob | First quarter, outlined | Right half lit in silver, gold ring |
| DoneThisCycle | Waning gibbous | Left ~75% lit, silver |
| Blocked | New moon | Dark disc, thin silver ring |
| Foreclosed | Eclipsed | Dark disc, red-violet ring, small notch |
| Unknown | Veiled | Dark disc with a dashed ring |

Glyphs are drawn at row height from two overlapping circles (terminator approximated by a circle offset), so they scale with UI scale and need no textures. One helper: `MoonGlyph.Draw(ImDrawListPtr, Vector2 center, float radius, QuestState state)`.

### 2.2 Logo and manifest icon (uniquely created)

A crescent moon low over a winding path that narrows toward a horizon, three small stones on the path lit gold. Geometry is authored in `assets/icons/tsukimichi.svg` (hand-written) and rendered to `assets/icon.png` (512×512) by `assets/icons/render_icons.py` using Pillow drawing the same shapes. No third-party artwork or icon packs anywhere in the project. In-game category icons reuse the game's own journal icons via the texture provider; that is allowed because they are game assets, not third-party.

### 2.3 Color tokens

Defined once in `Theme.cs` and pushed with `ImRaii.PushColor` only where the default Dalamud style is insufficient. The window otherwise respects the user's Dalamud style so it does not look foreign.

| Token | Hex | Use |
|---|---|---|
| Night | #0F1424 | Panel backgrounds for the detail pane and path view |
| Moon | #F2D27A | Completed, Ready, gold accents, progress fill |
| Silver | #DDE3F0 | Primary text on Night, silver glyphs |
| Dusk | #7C86A8 | Secondary text, rings, separators |
| Eclipse | #B25C7F | Foreclosed, destructive actions |
| Veil | #4A5270 | Disabled, unknown |

Text on the default ImGui background uses the default text color; tokens apply to glyphs, badges and the detail pane.

### 2.4 Layout

Main window, resizable, default 1100×700, three regions:

```
+------------------------------------------------------------------+
| [Search............] [Filters ▾] [Character: Michiru ▾]  Sync ●  |
+----------------+-----------------------------+-------------------+
| Journal tree   | Quest table                 | Detail pane       |
| Section        | ◐ Name  Lv  Job  Next step  | Name, moon, tags  |
|  Category (x/y)|                             | Requirements ✓/✗  |
|   Genre        |                             | Rewards           |
|                |                             | Path (chain)      |
|                |                             | Giver + map/journal|
+----------------+-----------------------------+-------------------+
| Status: 5,373 quests · showing 212 · snapshot 21:14 · v1.0.0     |
+------------------------------------------------------------------+
```

Tabs along the top of the tree pane switch the left navigation between **Journal**, **Moonlit** (unique rewards, grouped by reward kind) and **Characters**. Filters live in a persistent side panel toggled from the toolbar; state is always visible as chips under the search box.

## 3. Architecture

```
Tsukimichi.sln
├─ Tsukimichi.Core/        net10.0, no Dalamud reference
│   Model/                 QuestRecord, Requirement*, RewardRef, QuestState, CharacterSnapshot
│   Evaluation/            RequirementEvaluator, StateResolver, ReversePrereqIndex, PathFinder
│   Query/                 FilterSet, QuestQuery (filter+sort+search), SearchIndex
│   Storage/               ISnapshotStore, JsonSnapshotStore, UserData, ShippedData loaders
│   Unique/                UniqueRewardEntry, Confidence, UniqueRewardCatalog
├─ Tsukimichi/             Dalamud.NET.Sdk/15.0.0 plugin
│   Plugin.cs              service injection, wiring, dispose
│   Data/                  LuminaCatalogLoader (sheets → QuestRecord), IconResolver
│   Game/                  GameStateReader (QuestManager/UIState/PlayerState), StatePoller, SnapshotService
│   Ui/                    MainWindow, panes (TreePane, TablePane, DetailPane, FilterPanel, MoonlitPane, CharactersPane), ConfigWindow, MoonGlyph, Theme
│   Commands/              /tsukimichi
│   Config/                Configuration (IPluginConfiguration)
│   Data files/            unique_quests.json, curated/*.json (CopyToOutputDirectory)
├─ Tsukimichi.DataGen/     console tool: local game files → unique_quests.json (+ report)
├─ Tsukimichi.Tests/       xunit; unit tests for Core; optional data-driven tests gated by TSUKIMICHI_GAME_PATH
└─ assets/                 icons (svg + render script + png)
```

Data flow at runtime:
1. Plugin load → `LuminaCatalogLoader` builds `QuestCatalog` off-thread (cancellable) from `IDataManager` sheets in the client language. Catalog is immutable once built.
2. `ShippedData` loads `unique_quests.json` and curated overlays from the plugin directory; `UserData` loads per-character snapshots, pins and overrides from the config directory.
3. If logged in at load, or on `Login` then first `TerritoryChanged`, `SnapshotService` captures a `CharacterSnapshot` from `GameStateReader` on the framework thread.
4. `StatePoller` runs once per second on `IFramework.Update`: reads the completion bitmask, accepted quests, daily state, active festivals; diffs against the last snapshot; emits `QuestEvent`s; updates the snapshot; marks the query dirty.
5. `StateResolver` evaluates states for the selected character from catalog + snapshot (pure, in Core). `QuestQuery` produces the flat, filtered, sorted row array only when dirty. The UI renders from that array with a list clipper.

## 4. Core model

```csharp
enum QuestState { Ready, ReadyOnOtherJob, Accepted, Blocked, DoneThisCycle, Completed, Foreclosed, Unknown }

sealed record QuestRecord(
    uint RowId, ushort QuestId /* low 16 bits */, string InternalId, string Name,
    JournalRef Journal /* section, category, genre ids + names, sort key */,
    byte Expansion, byte Level, byte LevelMax, byte LevelOffset,
    uint ClassJobCategory, uint ClassJobCategory1, uint ClassJobRequired,
    Prereq PreviousQuests /* ids + join All/Any */, uint[] QuestLocks,
    uint[] InstanceContentRequired, JoinKind InstanceJoin,
    byte GrandCompany, byte GrandCompanyRank, byte BeastTribe, byte BeastRank, ushort BeastValue,
    bool IsRepeatable, byte RepeatInterval, byte DailyPool, ushort Festival,
    bool MountRequired, bool HouseRequired, uint[] AcceptConditions,
    Issuer? Issuer /* npc id, name, territory, map, x, y, z */, uint Icon,
    IReadOnlyList<RewardRef> Rewards, uint ExpFactor, uint Gil);

abstract record Requirement(RequirementKind Kind);            // one per gate, produced by the evaluator
sealed record RequirementResult(Requirement Req, bool Met, string Detail); // "Trusted, needs Sworn"

sealed record CharacterSnapshot(
    ulong ContentId, string Name, uint World, DateTime TakenUtc, int SchemaVersion,
    byte[] CompletedBits, IReadOnlyList<AcceptedQuest> Accepted, IReadOnlyDictionary<ushort,byte> DailyDone,
    IReadOnlyDictionary<byte,short> JobLevels, byte GrandCompany, byte[] GcRanks,
    IReadOnlyDictionary<byte,(byte rank, ushort value)> Tribes, byte TribeAllowance, byte LeveAllowance,
    IReadOnlyList<uint> UnlockedInstances, IReadOnlyList<ushort> ActiveFestivals, byte MaxExpansion, byte LevelCap,
    bool AchievementsLoaded, IReadOnlyList<uint> CompletedAchievements, byte CurrentJob);
```

`QuestId` is `(ushort)(RowId & 0xFFFF)`; every runtime lookup uses it. Names are stored as plain strings at build time; no per-frame string conversion.

## 5. Evaluation rules (StateResolver)

Order of checks; the first decisive rule wins.
1. Completed bit set and not repeatable → **Completed**.
2. Any `QuestLock` quest completed → **Foreclosed**. Grand Company-specific quests where the character is in another GC → Blocked (switchable), not Foreclosed.
3. Festival quest whose festival is inactive: if the character existed during a past run (heuristic: any quest of that festival completed, or festival end in curated data is past) → **Foreclosed**, else → **Blocked: seasonal**.
4. Accepted → **Accepted** (with sequence).
5. Repeatable and done this cycle (daily done flag, or completed bit on repeatable with interval) → **DoneThisCycle**.
6. Achievement-gated (curated) and achievements not loaded → **Unknown**.
7. Evaluate requirement list; all met on current job → **Ready**; all met on some other leveled job → **ReadyOnOtherJob**; else **Blocked** with results.

Requirement list, in display and "Next step" priority order: foreclosure, expansion or level cap, class/job, level (unsynced `JobLevels`), previous quests (All/Any with counts), Grand Company and rank, tribe rank then reputation then allowance then daily offer, duty completion, seasonal, extra accept conditions, mount, house.

Incremental re-evaluation: `ReversePrereqIndex` maps quest id → dependents; on a completion diff only dependents, level-indexed quests (on level change) and festival-indexed quests (on festival change) are re-resolved.

## 6. Storage layout

Config directory (`IDalamudPluginInterface.GetPluginConfigDirectory()`):
```
config.json                  Configuration (filters memory, per-category overrides, window prefs, schemaVersion)
characters/<ContentId>.json  CharacterSnapshot
user/pins.json               pinned quest ids per character
user/overrides.json          user unique/not-unique overrides
```
Plugin directory (read-only, shipped):
```
unique_quests.json           generated; { gameVersion, generatedUtc, entries: [ {questId, kind, rewardId, itemId, confidence, source} ] }
curated/system_unlocks.json  questId → { label, kind:"system" }
curated/duty_unlocks.json    questId → contentFinderConditionId[]
curated/festivals.json       festivalId → { name, start, end, mogStation:bool }   (optional; absent = active/inactive only)
curated/feature_quests.json  questId[] shown in the Feature Unlocks virtual category
```
All writes are temp-file + atomic rename. Unknown quest ids in snapshots are preserved.

## 7. UI specification

**Toolbar.** Search box (debounced 150 ms, matches quest name, reward names and numeric id), Filters toggle, Character combo (live character marked ●, others show snapshot age), sync indicator (moon glyph: full when live, veiled when viewing a snapshot).

**Filter panel.** Checkbox groups, always visible when open, chips summarize active filters:
- Completion: Hide completed (global), per-category override list.
- Availability: Available now (global), per-category override list.
- State (advanced): eight checkboxes.
- Expansion (six), Level range slider, Job category combo, Reward kind (three-state per kind), Repeatable, Seasonal-active only, Include Unlisted.
- "Nothing matches" panel with the offending filters named and a Reset button.

**Journal tree.** Section → Category → Genre; each node shows `done/total` and a mini filling moon. Virtual categories: **Feature Unlocks** (curated), **Unlisted** (genre 0). Selecting a node scopes the table.

**Quest table.** Columns: glyph, name, level, job category, next step, expansion, reward icons (up to 4). Sortable by name, level, state, expansion. Row context menu: pin, map flag, open journal, copy name, show path, Quest Map graph (if IPC available).

**Detail pane.** Header (name, glyph, genre, expansion, patch if known), requirement list with ✓/✗ and detail text, rewards with obtained badges, path (vertical chain of glyphs), giver with map and journal buttons, provenance line ("Completed per client flags at 21:14").

**Moonlit pane.** Left: reward kinds with counts (obtained/total). Table: reward name and icon, quest, obtained state, confidence badge (static/community/curated) with source on hover, quest state glyph. Toggle: hide obtained.

**Characters pane.** List of snapshots with name, world, taken time, quests completed; actions: view, export JSON, forget. Account view: for the selected quest or reward, each character's state.

**Config window.** Poll interval (0.5–5 s), chat notice for newly available (off by default, MSQ excluded), show Unlisted, language override (client default), data retention buttons (Forget character, Delete all data), about (versions of plugin, game data stamp, unique data stamp).

**Command.** `/tsukimichi` opens the window; `/tsukimichi <text>` searches and prints matches to chat as quest links with map links; `/tsukimichi config`.

## 8. DataGen tool

`Tsukimichi.DataGen --game "<sqpack path>" --out ../Tsukimichi/Data/unique_quests.json --curated ../Tsukimichi/Data/curated`
1. Scan `Quest` reward slots, `EmoteReward`, `ActionReward`, `GeneralActionReward`, `InstanceContentUnlock`, `ClassJobUnlock`, `OtherReward`, `ItemRewardType` 6 artifact gear.
2. Resolve items through `ItemAction` to mount (1322), minion (853), orchestrion (25183), TT card (3357), ornament (20086), barding (1013), emote/hairstyle unlink (2633).
3. Reverse links: `Action.UnlockLink`, `Trait.Quest`, `ClassJob.UnlockQuest`, `AetherCurrent.Quest`, `AozActionTransient.RequiredForQuest`, `Achievement` key/data → quest with `Title`, `ContentFinderCondition.UnlockCriteria`.
4. Exclusivity: `IsUntradable`, `ItemSearchCategory == 0`, absence from GilShop/SpecialShop/Recipe/GatheringItem/Achievement item sheets → confidence `static`; entries touched by curated files → `curated`. Community overlay is a later phase (see §12).
5. Emit JSON plus a markdown report of counts per kind for review.

## 9. Error handling

- Catalog build failure → window shows "Catalog unavailable" with the exception summary and a Retry button; plugin stays loaded.
- Snapshot file corrupt → renamed to `.corrupt-<timestamp>`, fresh snapshot taken, warning logged once.
- Shipped data missing → Moonlit pane shows "reward data not shipped in this build"; everything else works.
- ClientStructs read throws → poller backs off exponentially to 30 s, logs once, sync indicator shows veiled.
- All game reads happen on the framework thread; UI never touches ClientStructs directly.

## 10. Performance budget

- Catalog build under 3 s off-thread; memory under 30 MB for the catalog.
- Poll tick under 1 ms typical (bitmask compare + accepted list).
- Frame cost under 0.3 ms with 5k rows via list clipper and pre-materialized strings.
- Query rebuild under 20 ms; only on dirty flags.

## 11. Testing

- Core: xunit unit tests for StateResolver (every rule and order), PathFinder (All/Any, cycles, missing ids), QuestQuery (filter combinations, empty-result reasons, sort stability), JsonSnapshotStore (round trip, corrupt file, schema migration, unknown ids preserved).
- Data-driven: with `TSUKIMICHI_GAME_PATH` set, LuminaCatalogLoader's pure mapping is exercised against the real sheets via standalone Lumina and asserts known facts (5,373 named quests, Close to Home requires Coming to Gridania, The Ultimate Weapon rewards item 6008).
- Plugin project: build against Dalamud 15 libs is the compile-time check; in-game smoke test is manual by the owner (checklist in the plan).

## 12. Phases needing additional drafting

Marked **DRAFT-NEEDED**; each gets its own short design note before implementation.
- **DRAFT-NEEDED A: Community overlay ingestion.** Which of Teamcraft, Garland and FFXIV Collect to pull, how to cache, license notes, mapping of free-text sources to quest ids. Not in V1; V1 ships static + curated only.
- **DRAFT-NEEDED B: Festival calendar.** Source of start/end dates and Mog Station flags for `festivals.json`; maintenance cadence.
- **DRAFT-NEEDED C: Feature-unlock category heuristic.** Which reward fields qualify a quest automatically and the review process for the curated list.
- **DRAFT-NEEDED D: IPC surface and Wotsit registration.** Method names, argument shapes, versioning.
- **DRAFT-NEEDED E: Entitlement (free trial / expansion cap).** Verify which client field exposes max expansion and level cap before implementing F-26.
- **DRAFT-NEEDED F: Localization of UI strings.** Resource file layout and how to test long-string layouts.
- **DRAFT-NEEDED G: Official repo submission.** Manifest metadata, images, changelog, testing-track process.

## 13. Decisions adopted

D-1 eight-state model: yes. D-2 V1 scope: all V1 items in the feature panel. D-3 storage: JSON per character. D-4 curated overlays: JSON in repo keyed by quest id. D-5 feature-unlock category: heuristic first (draft C). D-6 entitlement: deferred to draft E. D-7 teleport: later. D-8 localization: resource files (draft F). D-9 distribution: custom repo first.

## 14. Out of scope for V1

Community overlay, festival countdown, patch-of-origin badges, account-wide reward matrix beyond per-quest view, IPC provider, Wotsit, chat notices, DTR entry, export/import, repeatable tracking UI beyond DoneThisCycle, Questionable hand-off.
