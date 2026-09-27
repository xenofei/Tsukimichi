# Tsukimichi

Status: pre-alpha. Nothing here is usable in game yet.

Tsukimichi (月道) reads as "the moon's path": *tsuki* (moon) from the character's family name Tsukikage (月影, moon-shadow) and *michi* (path). The plugin is the road you walk by moonlight: every FINAL FANTASY XIV quest is a step on the path, and the moon fills as you complete it. It catalogs every quest by journal type, shows which are completed or available right now with a breakdown of each unfulfilled requirement, gathers quests with unique rewards under Moonlit treasures, and keeps a snapshot per character so the whole account stays in view.

## Layout

| Project | Purpose |
|---|---|
| `Tsukimichi.Core` | Domain model, evaluation, query and storage. net10.0, no Dalamud reference. |
| `Tsukimichi` | The Dalamud plugin (`Dalamud.NET.Sdk/15.0.0`). |
| `Tsukimichi.DataGen` | Console tool that turns local game files into `unique_quests.json`. |
| `Tsukimichi.Tests` | xunit tests for Core. |

## Build

Requires the .NET 10 SDK and a Dalamud dev install (XIVLauncher puts it at `%AppData%\XIVLauncher\addon\Hooks\dev`; the SDK resolves it automatically, or set `DALAMUD_HOME`).

```
dotnet build Tsukimichi.sln -c Debug
dotnet test Tsukimichi.sln -c Debug
```

Build through the solution. It sets the x64 platform, and the plugin lands in `Tsukimichi\bin\x64\Debug\` (`Tsukimichi.dll`, the generated `Tsukimichi.json` manifest, `Tsukimichi.Core.dll`, `icon.png`, `Data\`). Building `Tsukimichi\Tsukimichi.csproj` on its own writes to `Tsukimichi\bin\Debug\` instead.

To load it in game: Dalamud Settings, Experimental, Dev Plugin Locations, add `Tsukimichi\bin\x64\Debug`, then enable Tsukimichi in the plugin installer's Dev Tools tab.

## Data generation

```
dotnet run --project Tsukimichi.DataGen -- --game "<path to sqpack>" --out Tsukimichi/Data/unique_quests.json --curated Tsukimichi/Data/curated
```

The generator is a stub until the data phase lands.
