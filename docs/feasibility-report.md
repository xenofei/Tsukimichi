# Tsukimichi — Feasibility Report

Date: 2026-09-27
Target: Dalamud plugin for FINAL FANTASY XIV, character Michiru Tsukikage
Status: research only, no plugin code written

## 1. Verdict

**Feasible.** Every requested capability is achievable with the current Dalamud stack and local game data. One capability (the "unique rewards" category) is roughly 75% automatic from game data and needs a curated overlay for the rest. One nuance (multi-character tracking) requires snapshotting each character while logged in, because the game client only holds quest state for the current character.

| Requirement | Feasible | How | Confidence |
|---|---|---|---|
| Catalog of ALL quests | Yes | `Quest` Excel sheet via Lumina: 5,533 rows, 5,373 named. Opens in ~1.5 s, full iteration under 100 ms. | Verified locally |
| Group by quest type | Yes | `JournalSection` (10) → `JournalCategory` (132) → `JournalGenre`. Matches the in-game journal exactly. | Verified locally |
| Hide/show completed | Yes | `QuestManager.IsQuestComplete(id)` (FFXIVClientStructs) or Dalamud `IUnlockState.IsQuestCompleted`. Accepted quests and current step via `GetQuestSequence`. | Verified in reference plugins |
| Hide/show "available now" | Yes | Computed by the plugin from sheet fields vs. runtime state (§3). The game exposes no single "available" flag. | Fields verified; logic is ours |
| Requirement breakdown showing what is still missing | Yes | Same fields, rendered per requirement with pass/fail. | Fields verified |
| Unique-reward quest category | Mostly | Static links cover emotes, mounts, minions, orchestrion, TT cards, hairstyles, job unlocks, actions, aether currents, blue mage spells, artifact gear, achievements/titles. System unlocks and most dungeon unlocks need a curated list. | Verified; gaps in §4 |
| Local database | Yes | Plugin config directory. JSON per character, or SQLite (Dalamud bundles SQLitePCLRaw and native e_sqlite3; PlayerTrack and PalacePal ship SQLite). | Verified |
| Multi-character | Yes, with caveat | Only the logged-in character is readable. Snapshot per `IPlayerState.ContentId` at login/logout, as DailyDuty does. | Verified |

## 2. Environment verified on this machine

- Game patch 2026.09.15.0000.0000; sqpack folders ffxiv, ex1–ex4.
- Dalamud 15.0.3.5, **API level 15**, runtime **net10.0**, bundled Lumina 7.6.0 / Lumina.Excel 7.5.1, FFXIVClientStructs 7.56.x, ImGui via `Dalamud.Bindings.ImGui`.
- .NET SDK 10.0.401 installed. Toolchain: `<Project Sdk="Dalamud.NET.Sdk/15.0.0">`; manifest fields live in the csproj (no separate manifest JSON since API 14).
- Dev plugin loading via Dalamud Settings → Experimental → Dev Plugin Locations.
- Reference plugins installed locally: Quest Map, Questionable, DailyDuty (all API 15).
- Next API (16, patch 8.0) removes mutable `SeString` and moves sheets to `Dalamud.Excel.Sheets`. Keep string and sheet access behind thin adapters.

## 3. Requirement model (what "can I take this quest" needs)

All present in the `Quest` sheet (Lumina property names):

| Requirement | Field(s) | Notes from the data |
|---|---|---|
| Level | `ClassJobLevel[0]` (+ `ClassJobLevel[1]`, `LevelMax`) | LevelMax nonzero for a handful |
| Class/job | `ClassJobCategory0`, `ClassJobCategory1`, `ClassJobRequired`, `ClassJobUnlock` | Category 142 (any DoW/DoM) covers 2,966 quests; 631 require a specific class |
| Previous quests | `PreviousQuest[3]`, `PreviousQuestJoin` | Join 1 = ALL (5,271 quests), Join 2 = ANY (102 quests, e.g. the three Envoy quests) |
| Mutually exclusive | `QuestLock[2]`, `QuestLockJoin` | 64 quests, e.g. Grand Company choice quests |
| Duty completion | `InstanceContent[3]`, `InstanceContentJoin` | 28 quests |
| Grand Company + rank | `GrandCompany`, `GrandCompanyRank` | 21 quests |
| Tribe + reputation | `BeastTribe`, `BeastReputationRank`, `BeastReputationValue` | 689 quests; names/thresholds in `BeastReputationRank` |
| Expansion | `Expansion` (ExVersion) | 6 expansions |
| Repeatable / daily | `IsRepeatable`, `RepeatIntervalType`, `DailyQuestPool` | 659 repeatable |
| Seasonal | `Festival`, `FestivalBegin/End` | 310 quests |
| Extra conditions | `QuestAcceptAdditionCondition` sheet, `Header`, `MountRequired`, `IsHouseRequired` | Small counts |
| Starting NPC + location | `IssuerStart` → ENpcResident, `IssuerLocation` → Level → Territory/Map + coords | Present for 5,368 quests; enables map links |

Runtime state to compare against: `IPlayerState` (all job levels, GC + rank, ContentId, name, world), `QuestManager` (complete / accepted / sequence, daily completion, tribe reputation, leve allowances), `UIState` (instance content unlocked/completed, unlock links, emotes, minions), `PlayerState` (mounts, orchestrion, ornaments, aether currents). Achievements are readable only after the client fetches them (opening the Achievements window), so title/achievement gating must display "unknown" until then.

## 4. Unique-reward detection: automatic vs. curated

Automatic from static sheets (verified counts):
- Emotes: `Quest.EmoteReward` (41 quests) plus items with ItemAction 2633 carrying the emote unlock link (168 of 191 non-quest-link emotes). Uncovered: seasonal, Gold Saucer, hunt emotes.
- Mounts: quest item rewards with ItemAction 1322 (36 quests). Minions: ItemAction 853 (62 quests). Orchestrion: ItemAction 25183 (the Orchestrion row id is `Item.AdditionalData`, not `ItemAction.Data`). TT cards: 3357. Ornaments: 20086. Bardings: 1013.
- Actions: `Action.UnlockLink` → Quest (249 actions). Traits: `Trait.Quest` (54). Job unlocks: `ClassJob.UnlockQuest`. Blue mage: `AozActionTransient.RequiredForQuest`. Aether currents: `AetherCurrent.Quest` (150 quests flagged "Aether Current").
- Job artifact gear: `ItemRewardType` 6 → `QuestClassJobReward` (115 quests).
- Achievements/titles: `Achievement.Key/Data` → Quest (753 + 131 links), `Achievement.Title` (891 titles).
- Exclusivity signal: `Item.IsUntradable`, `ItemSearchCategory == 0`, absence from GilShop/SpecialShop/Recipe/Gathering sheets.

Not in static data (needs a curated overlay stored as JSON in the repo, seeded from wiki, Garland Tools, Teamcraft):
- **Most dungeon/trial unlocks.** Only 35 quests link via `InstanceContentUnlock` and 105 duties via `ContentFinderCondition.UnlockCriteria`; 368 of 404 Duty Finder entries (including Sastasha, Toto-Rak, the Praetorium) have no static link because the unlock happens in quest script. Runtime unlock state is still readable; only the quest attribution is missing.
- System/feature unlocks (retainers, flying, Gold Saucer, Glamour Dresser, Bozja, Island Sanctuary, etc.). `QuestRewardOther` has only 17 named rows; `SystemReward` is 95 untyped byte pairs.
- Duty drops, FATE rewards, Mog Station re-sales (needed to prove a reward is truly quest-only).

Recommended pipeline: offline generator (Lumina against local game files, or xivapi v2 with a pinned version) → static classification → community overlay (Teamcraft JSON under MIT, Garland Tools, FFXIV Collect) → `curated.json` → ship `unique_quests.json` with a `confidence` field (static / community / curated). Re-run each patch.

## 5. Risks

1. FFXIVClientStructs offsets and the Dalamud API level change every major patch; the plugin needs a rebuild per API bump (routine for every Dalamud plugin).
2. `IUnlockState` is marked experimental. Fallback is direct ClientStructs calls, which Quest Map and Questionable already use.
3. Native SQLite stays loaded after a dev hot-reload and the DB file can stay locked until game restart. Mitigation: clear the connection pool on dispose (PalacePal pattern) or use per-character JSON, which is sufficient for ~5k quests × a few characters.
4. Quest Map already covers "search quests by reward" and requirement graphs. Tsukimichi's differentiators: per-character completion snapshots across the account, the "available now" evaluator with a missing-requirements breakdown, and the exclusivity-aware unique-rewards category.
5. 180 named quests have `JournalGenre` 0 and fall into a placeholder category ("Sephiroth Missions", section 255): removed, legacy, or hidden quests. Bucket them as "Unlisted".
6. Achievement state is unavailable until the player opens the Achievements window once per session.
7. A session usage limit interrupted one research agent mid-run; its probe output was recovered from disk, but the fourth pass (unknown-field correlation) never ran. Nothing in the verdict depends on it.

## 6. Recommended architecture (input to the spec phase)

- `Tsukimichi.Core` (net10.0 class library, no Dalamud references): catalog model, requirement evaluator, filters/sorting, snapshot model, storage layer. Unit-testable with xUnit; data-driven tests can open the local game files with standalone Lumina.
- `Tsukimichi` (Dalamud.NET.Sdk plugin): adapters over `IDataManager`, `IPlayerState`, `QuestManager`/`UIState`; ImGui windows via `WindowSystem` + `ImRaii`; `/tsukimichi` command; optional DTR bar entry.
- `Tsukimichi.Data` (build-time tool): generates `unique_quests.json` and merges curated overlays.
- Storage: plugin config directory. Start with JSON per ContentId; move to SQLite only if query needs demand it.

## 7. Feature suggestions

Core (as requested):
- Tabs by journal section, tree by category/genre, sortable and filterable table with text search.
- Filters: completed / not completed / accepted; available now / blocked; expansion; level range; class/job; repeatable; seasonal-active.
- Quest detail panel: requirements with ✓/✗ per item and the exact gap ("needs Sworn with the Ixal, you are Trusted"), rewards with icons, starting NPC with a clickable map link, open in journal.
- Unique-rewards tab grouped by reward kind with obtained / not-obtained state and a confidence badge.
- Per-character snapshots with an account-wide view ("which of my characters still has this quest available").

Suggested additions:
- Chain view: walk `PreviousQuest` recursively to show the shortest path to unlock a target quest, with the count of quests remaining.
- Progress dashboards: completion % per category/expansion, MSQ position, tribe rank progress, role quest progress.
- "Newly available" list: quests that just unlocked after a level-up or turn-in (hook `LevelChanged` and quest completion events).
- Seasonal alerts: active festival quests with a countdown; flag ones whose rewards later appear on the Mog Station.
- Daily/weekly view: tribe allowances, leve allowances, custom deliveries (overlaps DailyDuty; keep light).
- Export/import: JSON export of a character snapshot; optional CSV.
- Chat integration: `/tsukimichi <quest name>` prints a quest link plus a map link.
- Localization from game data (EN/JA/DE/FR names come from the sheets) with a small resource file for UI strings.
- Integration hooks: IPC so Questionable can be asked to run a selected quest (opt-in, later).

## 8. Existing plugins to learn from

| Plugin | Repo | What to borrow |
|---|---|---|
| Quest Map | github.com/GemPlugins/QuestMap | Reward filters, requirement graph, journal open |
| Questionable | github.com/PunishXIV/Questionable | `QuestFunctions.cs` for completion/sequence reads and prerequisite logic |
| DailyDuty | github.com/MidoriKami/DailyDuty | Per-ContentId JSON storage pattern |
| Collector's Anxiety | github.com/foophoof/CollectorsAnxiety | `UnlockItemCache` ItemAction mapping |
| PalacePal / PlayerTrack | github.com/PunishXIV/PalacePal, github.com/Infiziert90/PlayerTrack | SQLite inside a Dalamud plugin |

## 9. Agent assignment for the build phase

| Phase | Agent type | Task |
|---|---|---|
| Spec | main session (brainstorming → writing-plans) | Design spec in `docs/superpowers/specs/`, then the implementation plan |
| Data pipeline | general-purpose | Lumina-based generator for the quest catalog and unique-rewards JSON; curated overlay format |
| Core library | general-purpose (TDD) | Requirement evaluator, filters, snapshot model, unit tests |
| Plugin shell + UI | general-purpose | SDK project, services, WindowSystem, tables, detail panel, map/chat links |
| Runtime adapters | general-purpose | QuestManager/UIState/PlayerState reads on the framework thread; login/logout snapshots |
| Review | code-reviewer, bug-hunter, test-coverage-reviewer | After each phase |
| Performance | performance-oracle | Table rendering with 5k rows, per-frame allocations |
| Docs | update-docs / best-practices-researcher | README, official-repo submission checklist |

## 10. Throwaway artifacts

Probe code and raw output live in the session scratchpad (`QuestProbe/`, passes 2–3). They are not part of the project. The full JournalCategory list and sample quest dumps are in `pass2.txt`.
