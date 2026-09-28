# Tsukimichi

Status: V1 feature-complete, awaiting first in-game smoke test. Builds clean against Dalamud API 15 with 356 passing tests.

Tsukimichi (月道) reads as "the moon's path": *tsuki* (moon) from the character's family name Tsukikage (月影, moon-shadow) and *michi* (path). The plugin is the road you walk by moonlight: every FINAL FANTASY XIV quest is a step on the path, and the moon fills as you complete it. It catalogs every quest by journal type, shows which are completed or available right now with a breakdown of each unfulfilled requirement, gathers quests with unique rewards under Moonlit, and keeps a snapshot per character so the whole account stays in view.

## What it does

- **Journal** tab: the real journal hierarchy (Section, Category, Genre) with completion counts and a filling moon per node; a sortable, searchable quest table; a detail pane with every requirement marked met or not and the exact gap ("needs Sworn, you are Trusted"), rewards, the prerequisite path, and the quest giver with map-flag and journal buttons.
- **Filters**: hide completed, available now (both global with per-category overrides), state, expansion, level, job, reward kind, repeatable, seasonal, pinned, unlisted. Active filters show as chips; an empty result explains which filters caused it.
- **Moonlit** tab: rewards obtainable only through a quest (emotes, mounts, minions, orchestrion, cards, gear, unlocks and more) with obtained state, the quest state, and a confidence badge showing whether the claim comes from game data or a curated list.
- **Characters** tab: every character snapshotted on this account, with an offline account-wide view of any quest's state per character, JSON export, and forget.
- **Quest states** are moon phases: full = completed, first quarter = ready, waxing gibbous = accepted, new = blocked, waning gibbous = repeatable done this cycle, eclipsed = foreclosed, veiled = unknown.

Commands: `/tsukimichi` toggles the window, `/tsukimichi <text>` searches and prints quest links to chat, `/tsukimichi config`, `/tsukimichi glyphs`.

## Install (custom repository)

1. In game, type `/xlsettings`, open the **Experimental** tab, and under **Custom Plugin Repositories** paste:
   ```
   https://raw.githubusercontent.com/xenofei/Tsukimichi/main/pluginmaster.json
   ```
2. Click the **+** button, then **Save and Close**.
3. Type `/xlplugins`, search for **Tsukimichi**, and click **Install**.

Updates arrive through the plugin installer like any other plugin. The repository index is `pluginmaster.json` at the root of this repo; each release's `latest.zip` is attached to the matching GitHub Release.

## Layout

| Project | Purpose |
|---|---|
| `Tsukimichi.Core` | Domain model, state evaluation, query, storage, unique-reward catalog. net10.0, no Dalamud reference. |
| `Tsukimichi.GameData` | Lumina mapping from game sheets to the Core model. Shared by the plugin and the tests. |
| `Tsukimichi` | The Dalamud plugin (`Dalamud.NET.Sdk/15.0.0`): runtime state reader, poller, snapshots, ImGui UI. |
| `Tsukimichi.DataGen` | Console tool that turns local game files into `Tsukimichi/Data/unique_quests.json`. |
| `Tsukimichi.Tests` | xunit tests for Core and GameData; data-driven tests run when `TSUKIMICHI_GAME_PATH` points at the game's `sqpack` folder. |
| `assets/` | Original icon (SVG source, Pillow renderer, PNG). |
| `docs/` | Feasibility report, feature panel, design spec, implementation plan, smoke checklists, data reports. |

## Build

Requires the .NET 10 SDK and a Dalamud dev install (XIVLauncher puts it at `%AppData%\XIVLauncher\addon\Hooks\dev`; the SDK resolves it automatically, or set `DALAMUD_HOME`).

```
dotnet build Tsukimichi.sln -c Debug
dotnet test Tsukimichi.sln -c Debug
```

Build through the solution. The plugin lands in `Tsukimichi\bin\x64\Debug\` (`Tsukimichi.dll`, the generated `Tsukimichi.json` manifest, `Tsukimichi.Core.dll`, `Tsukimichi.GameData.dll`, `icon.png`, `Data\`). A Release build additionally produces `Tsukimichi\bin\Release\Tsukimichi\latest.zip` via DalamudPackager.

## Load it in game

1. Dalamud Settings (`/xlsettings`), Experimental, Dev Plugin Locations: add the full path to `Tsukimichi\bin\x64\Debug`.
2. Plugin Installer (`/xlplugins`), Dev Tools tab: enable Tsukimichi.
3. `/tsukimichi` opens the window. Follow `docs/ui-smoke-checklist.md` and `docs/ui-smoke-checklist-panes.md` for the first pass.

Per-character data lives in `%AppData%\XIVLauncher\pluginConfigs\Tsukimichi\` (`characters\<ContentId>.json`, `user\pins.json`, `user\overrides.json`). Nothing is uploaded anywhere.

## Data generation

Regenerate the unique-rewards file after a game patch or after editing the curated files:

```
dotnet run --project Tsukimichi.DataGen -- --game "<path to sqpack>" --out Tsukimichi/Data/unique_quests.json --curated Tsukimichi/Data/curated
dotnet run --project Tsukimichi.DataGen -- --verify --game "<path to sqpack>"
```

Curated overlays (`Tsukimichi/Data/curated/`) are keyed by Quest row id; see the README there for the verification recipe. The helper scripts in `tools/curated/` query xivapi to check ids.

## Not yet in V1

Community reward-source overlay, festival calendar and countdowns, patch-of-origin badges, IPC provider and Wotsit registration, entitlement (free trial) filter, UI localization resources, official repo submission. Each is listed as a DRAFT-NEEDED phase in the design spec.
