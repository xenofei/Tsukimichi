# Contributing

Tsukimichi is a Dalamud plugin for FINAL FANTASY XIV; the [README](README.md) says what it does and how players install it. This page is for building it and correcting its data. Bug reports and data corrections go through the [issue templates](https://github.com/xenofei/Tsukimichi/issues/new/choose).

## Build

Requires the .NET 10 SDK and a Dalamud dev install (XIVLauncher puts it at `%AppData%\XIVLauncher\addon\Hooks\dev`; the SDK finds it there, or set `DALAMUD_HOME`).

```
dotnet build Tsukimichi.sln -c Release -warnaserror
dotnet test Tsukimichi.Tests -c Release --no-build
```

Warnings are errors, in CI and locally. The plugin lands in `Tsukimichi\bin\x64\Release\` when built through the solution; building `Tsukimichi/Tsukimichi.csproj` on its own additionally produces `Tsukimichi\bin\Release\Tsukimichi\latest.zip`, the manifest and `images\icon.png` through DalamudPackager. Tests marked `[GameDataFact]` run only when `TSUKIMICHI_GAME_PATH` points at the game's `sqpack` folder; everything else runs from committed fixtures.

To load a build in game: Dalamud Settings (`/xlsettings`) › Experimental › Dev Plugin Locations, add the full path to the build output; then `/xlplugins` › Dev Tools › enable Tsukimichi.

## Layout

| Project | Purpose |
|---|---|
| `Tsukimichi.Core` | Domain model, state evaluation, query, storage, unique-reward catalog. net10.0, no Dalamud reference. |
| `Tsukimichi.GameData` | Lumina mapping from game sheets to the Core model. Shared by the plugin and the tests. |
| `Tsukimichi` | The Dalamud plugin (`Dalamud.NET.Sdk/15.0.0`): runtime state reader, poller, snapshots, ImGui UI. |
| `Tsukimichi.DataGen` | Console tool that turns local game files into `Tsukimichi/Data/unique_quests.json` and verifies it. |
| `Tsukimichi.Tests` | xunit tests for Core and GameData, with frozen fixtures under `Fixtures/`. |
| `assets/` | The icon (SVG source, renderer, PNG). |
| `docs/` | Feasibility report, plans, design spec, review panel, data reports. |
| `tools/` | `make_pluginmaster.py` (the repository index) and curated-data helpers. |

## Curated data

Facts the game sheets do not hold (duty and system unlock quests, story chains, seasonal windows, store re-sells) live in `Tsukimichi/Data/curated/`. Every key is a Quest row id, every entry carries a note, and every id is verified before it is written. The rules, the file shapes and the verification recipe are in [Tsukimichi/Data/curated/README.md](Tsukimichi/Data/curated/README.md); the curated-data tests (`dotnet test Tsukimichi.Tests --filter "Category=Curated"`) check the files on every build.

Regenerate the reward database after a game patch or after editing the curated files:

```
dotnet run --project Tsukimichi.DataGen -- --game "<path to sqpack>" --out Tsukimichi/Data/unique_quests.json --curated Tsukimichi/Data/curated
dotnet run --project Tsukimichi.DataGen -- --verify --game "<path to sqpack>"
```

## Releases

A release is a tag `vX.Y.Z` whose version equals the csproj `<Version>` and has a `## [X.Y.Z]` section in [CHANGELOG.md](CHANGELOG.md). GitHub Actions builds `latest.zip` from the tagged commit, attaches it to the GitHub Release and regenerates `pluginmaster.json` on `main` ([release workflow](.github/workflows/release.yml)). Nothing is built or uploaded from a developer machine.

## Ground rules

- No network code in the plugin, and no automation: it never moves the character, accepts quests or presses anything.
- Nothing that identifies a character (name, content id) goes into logs, diagnostics or issue text.
- Public copy writes the name as "Tsukimichi — FFXIV quest tracker (Dalamud)".
- Commits use conventional prefixes (`feat:`, `fix:`, `docs:`, `build:`, `ci:`); user-visible changes get a line under `## [Unreleased]` in the changelog, written for players.
