# Tsukimichi V1 — Implementation Plan

Spec: docs/superpowers/specs/2026-09-27-tsukimichi-design.md
Execution: subagent-driven; one implementer per task, reviewer passes after each phase. Every task ends with `dotnet build` (and `dotnet test` where tests exist) passing on this machine against Dalamud 15 libs at `%AppData%\XIVLauncher\addon\Hooks\dev`.

Conventions for all tasks:
- SDK string for the plugin project: `<Project Sdk="Dalamud.NET.Sdk/15.0.0">`. Core, DataGen and Tests are plain `Microsoft.NET.Sdk` net10.0.
- Verify API signatures against `Dalamud.xml` and `FFXIVClientStructs.xml` in the Dalamud dev folder before use; never guess.
- No third-party artwork. No NuGet packages beyond: xunit, Lumina + Lumina.Excel (DataGen and Tests only), Microsoft.NET.Test.Sdk, coverlet (optional).
- Commit after each task with a conventional message.

## Phase 0 — Repository and identity

**T0.1 Repo scaffold.** git init; `.gitignore` (bin, obj, .vs, *.user, packages.lock.json kept); `Tsukimichi.sln`; four projects per spec §3 with references (Plugin → Core; Tests → Core; DataGen standalone); `Directory.Build.props` (Nullable, LangVersion latest, TreatWarningsAsErrors off for now); README stub; `dotnet build` passes; plugin csproj has Name/Author/Punchline/Description/RepoUrl, `Version` 0.1.0.0, and copies `Data/**` and `assets/icon.png` to output.

**T0.2 Icons.** Hand-write `assets/icons/tsukimichi.svg` (crescent over winding path, three lit stones). Write `assets/icons/render_icons.py` (Pillow) drawing the same geometry to `assets/icon.png` 512×512 with transparent background, plus `assets/icon-64.png`. Run it. Commit the PNGs.

## Phase 1 — Core domain

**T1.1 Model.** Records from spec §4, `QuestState`, `JoinKind`, `RequirementKind`, `RewardRef` (kind, id, itemId, count, name, icon), `JournalRef`, `Issuer`, `UniqueRewardEntry`, `Confidence`. `QuestCatalog` (immutable: by row id, by quest id, by genre/category/section, name search keys lowercase).

**T1.2 StateResolver + RequirementEvaluator.** Rules from spec §5 with `ReversePrereqIndex`. Tests: one test per rule and per ordering conflict; ANY/ALL joins; QuestLock foreclosure; GC-specific blocked not foreclosed; repeatable DoneThisCycle; other-job readiness; unsynced levels; tribe four-part requirement; Unknown for achievement gating.

**T1.3 PathFinder.** Chain to target: shortest through ANY branches, cycle-safe, marks done steps. Tests.

**T1.4 QuestQuery.** `FilterSet` (all filters in spec §7), `Apply(catalog, states, filterSet, sort, search)` → `QuestRow[]` and an `EmptyReason` describing which filters removed everything. Search matches name, reward names, id. Tests incl. per-category overrides.

**T1.5 Storage.** `JsonSnapshotStore` (atomic writes, corrupt file quarantine, schema version, unknown ids preserved), `UserData` (pins, overrides), `ShippedData` loaders for `unique_quests.json` and curated files with tolerant parsing. Tests with temp dirs.

## Phase 2 — Data generation

**T2.1 DataGen.** Console tool per spec §8 using Lumina against the local game path; emits `Tsukimichi/Data/unique_quests.json` and `docs/data/unique-report.md`. Also emits `docs/data/catalog-stats.md` (counts per section/category) for sanity. Run it and commit outputs.

**T2.2 Curated seeds.** Create `Tsukimichi/Data/curated/` with `system_unlocks.json`, `duty_unlocks.json`, `feature_quests.json` seeded from well-known quests (retainers, chocobo, Gold Saucer, glamour dresser, flying per expansion, MSQ dungeon unlock quests for ARR at minimum), each entry with a `note` field citing why. `festivals.json` left as an empty object with a schema comment (DRAFT-NEEDED B).

## Phase 3 — Plugin runtime

**T3.1 Catalog loader.** `LuminaCatalogLoader` maps `IDataManager` sheets to `QuestRecord` per spec §4, off-thread with cancellation, language from client. Includes JournalSection/Category/Genre names, issuer location via Level → Map/TerritoryType, rewards with names and icons.

**T3.2 Game state reader + poller + snapshot service.** `GameStateReader` (QuestManager, UIState, PlayerState, IPlayerState, active festivals, achievements loaded flag), `SnapshotService` (login bootstrap incl. already-logged-in, first TerritoryChanged, logout flush), `StatePoller` (1 Hz diff → events → store update → dirty flag), exponential backoff on exceptions. All reads on the framework thread.

**T3.3 Plugin wiring.** `Plugin.cs` with services, `WindowSystem`, command, config load/save, dispose hygiene (unsubscribe everything, cancel builds, flush). `Configuration` with schema version.

## Phase 4 — UI

**T4.1 Theme + MoonGlyph.** Tokens and the procedural phase glyph; a debug window that renders all eight glyphs at three sizes.

**T4.2 MainWindow shell.** Toolbar, three-pane layout with splitters, status bar, filter panel with chips and empty-result guard, character combo, sync glyph.

**T4.3 Journal tree + table.** Tree with counts and filling moons, virtual categories (Feature Unlocks, Unlisted), table with list clipper, sorting, context menu, reward icons via texture provider.

**T4.4 Detail pane.** Requirements ✓/✗ with detail, rewards with obtained badges, path chain, giver with map flag (`IGameGui.OpenMapWithMapLink`) and open journal, provenance line.

**T4.5 Moonlit + Characters panes, Config window, command.** Per spec §7.

## Phase 5 — Verification

**T5.1 Review passes.** code-reviewer on Core, bug-hunter on Game/ and Ui/, test-coverage-reviewer on Tests, performance-oracle on TablePane and StatePoller. Fix findings.

**T5.2 Owner smoke test (manual, in game).** Checklist: add repo folder as dev plugin location; `/tsukimichi` opens; tree counts match the in-game journal Completed tab for one category; a known Ready quest shows first-quarter glyph; complete a quest and see the row flip within 2 s; log out, window shows stale banner; log in on an alt, character combo shows both; Moonlit pane lists Her Last Vow → Most Gentlemanly with obtained state; map flag opens the map at the giver.

## Later phases (DRAFT-NEEDED, not scheduled)

A community overlay, B festival calendar, C feature-unlock heuristic review, D IPC + Wotsit, E entitlement, F localization resources, G official repo submission.
