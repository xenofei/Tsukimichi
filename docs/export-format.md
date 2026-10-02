# Export format

Tsukimichi can write two files for spreadsheets, scripts and your own tools:

- **Completed quests**: the quests the character has completed, one row per quest.
- **Moonlit collection**: every quest-exclusive reward the Moonlit tab lists, with whether the character has it.

No collection site can import these files as they are.
- **FFXIV Collect's** import page takes its own format, and it **replaces** each list you send. A file that holds only quest rewards would remove every other entry you had marked.
- **XIV Shinies** syncs through its own plugin and has no file import.

Use the ids below to match rows with those sites by hand or in a spreadsheet.

Each comes as **JSON** or **CSV**. Write them from Settings › Data › Export or with a chat command:

```
/tsuki export                  both files, in the format chosen in Settings
/tsuki export quests csv       completed quests as CSV
/tsuki export moonlit json     Moonlit collection as JSON
```

The export is always of the character shown in the window (the logged-in one, or the one picked on the Characters tab).

## Privacy

- The files are written to your computer only. Tsukimichi has no network code and never uploads anything.
- No content id, account id or home world is ever written.
- The character's name is left out unless **Include character name** is ticked in Settings › Data › Export. When it is, the name goes in the JSON header (`character`) and in the file name of both formats; CSV rows never carry it.
- Quest names are written in full. The spoiler shield (Settings › Spoilers) masks names in the window, chat and menus, but not in files: with **List every quest with a completed flag** ticked, the file names every main scenario quest, including the ones you have not reached.

## Where the files go

`exports` in the plugin's config directory by default (`%APPDATA%\XIVLauncher\pluginConfigs\Tsukimichi\exports`). Settings › Data › Export › Output folder changes it; a relative folder (including `\exports` or `D:exports`, which depend on the current drive or directory) is taken under the config directory. After an export the path is shown with an **Open folder** button, and the command prints it to chat.

File names: `tsukimichi-<kind>[-<character>]-<yyyyMMdd>-<HHmmss>.<json|csv>`, with the local time of the export, for example `tsukimichi-quests-20260929-201500.json`.

## Completed quests

One row per quest the character has completed, in journal order. With **List every quest with a completed flag** ticked, every quest the journal knows is listed instead, each with `completed` true or false.

| Field | Meaning |
|---|---|
| `rowId` | Row id in the game's Quest sheet (65536 + n); the id FFXIV Collect, Garland Tools and xivapi use |
| `questId` | The runtime quest id (the low 16 bits of `rowId`) |
| `name` | The quest name in the client's language |
| `section` | Journal section ("Main Scenario (A Realm Reborn through Endwalker)", "Sidequests", …) |
| `category` | Journal category |
| `genre` | Journal genre (the node the quest sits under in the Journal tab) |
| `expansion` | Expansion name ("A Realm Reborn", …, "Dawntrail") |
| `completed` | `true` when the quest's completion flag is set |
| `completedAt` | When Tsukimichi first saw the quest completed, UTC, ISO 8601. Absent in JSON (empty in CSV) when no date is known |
| `completedAfter` | Only for a quest found completed when the character logged in (it was done while Tsukimichi was not running): the character's previous capture, UTC. The quest was completed between `completedAfter` and `completedAt` |

Completion dates start when Tsukimichi 1.5 first captures the character (`completionDatesSinceUtc` in the JSON header). Quests already completed then get no date: nothing is guessed. The dates are kept on your computer with the character's other saved data.

Quests the game removed (the "Removed from the game" node) are included when the character completed them. Quests the journal never names (internal steps) are not.

## Moonlit collection

One row per reward of the Moonlit tab's unique view (rewards you marked not unique are left out), in the tab's order.

| Field | Meaning |
|---|---|
| `kind` | Reward kind: `Emote`, `Mount`, `Minion`, `Orchestrion`, `TripleTriadCard`, `Barding`, `Hairstyle`, `Ornament`, `DutyUnlock`, `SystemUnlock`, `Action`, `Title`, `Item`, … |
| `rewardId` | Row id in the sheet the kind refers to (Mount, Companion, Emote, TripleTriadCard, …) |
| `rewardName` | The reward's name in English, from the shipped reward data, whatever the client's language |
| `questRowId` | Quest sheet row id of the quest that gives it |
| `obtained` | `true`, `false`, or unknown (`null` in JSON, `unknown` in CSV) |

Emotes, minions, mounts, orchestrion rolls, ornaments (fashion accessories), Triple Triad cards, bardings, hairstyles, aether currents and duties are read from the game's unlock flags. Each capture saves them with the character, so a stored character (or one logged in on another game client) exports what it owned at its last capture. A character not captured since Tsukimichi 1.5 reads unknown for these until it logs in once. Rewards that simply follow the quest (actions, traits, jobs, blue magic, titles, achievements, system unlocks) are answered from the quest's completion flag for any character. Items and gear read unknown for now. This is the same obtained state the Moonlit tab shows.

## JSON

A small header, the counts, then the rows. `formatVersion` is bumped only when a field changes meaning or goes away; new fields may be added at any time, so readers should ignore fields they do not know.

| Header field | Meaning |
|---|---|
| `format` | Always `tsukimichi-export` |
| `formatVersion` | `1` |
| `kind` | `quests` or `moonlit` |
| `pluginVersion` | Tsukimichi's version |
| `gameVersion` | The game client's version (empty when it could not be read) |
| `exportedUtc` | When the file was written, UTC, ISO 8601 |
| `character` | The character's name; present only when Include character name is ticked |
| `count` | Number of rows |
| `completedCount` | Quests: rows with `completed` true |
| `obtainedCount`, `unknownCount` | Moonlit: rows obtained, rows unknown |
| `completionDatesSinceUtc` | Quests: when completion dates started being recorded for the character; absent when it has none yet |

Sample (quests, trimmed):

```json
{
  "format": "tsukimichi-export",
  "formatVersion": 1,
  "kind": "quests",
  "pluginVersion": "0.7.0.0",
  "gameVersion": "2026.09.15.0000.0000",
  "exportedUtc": "2026-09-29T18:30:00Z",
  "completionDatesSinceUtc": "2026-09-12T19:02:11Z",
  "count": 2,
  "completedCount": 2,
  "quests": [
    {
      "rowId": 65621,
      "questId": 85,
      "name": "Close to Home",
      "section": "Main Scenario (A Realm Reborn through Endwalker)",
      "category": "Seventh Umbral Era Main Scenario Quests",
      "genre": "Seventh Umbral Era",
      "expansion": "A Realm Reborn",
      "completed": true
    },
    {
      "rowId": 65575,
      "questId": 39,
      "name": "Coming to Gridania",
      "section": "Sidequests",
      "category": "Gridanian Sidequests",
      "genre": "Gridanian Sidequests",
      "expansion": "A Realm Reborn",
      "completed": true,
      "completedAt": "2026-09-20T21:14:05Z"
    }
  ]
}
```

Sample (Moonlit, trimmed):

```json
{
  "format": "tsukimichi-export",
  "formatVersion": 1,
  "kind": "moonlit",
  "pluginVersion": "0.7.0.0",
  "gameVersion": "2026.09.15.0000.0000",
  "exportedUtc": "2026-09-29T18:30:00Z",
  "count": 2,
  "obtainedCount": 1,
  "unknownCount": 0,
  "rewards": [
    {
      "kind": "Emote",
      "rewardId": 114,
      "rewardName": "Most Gentlemanly",
      "questRowId": 66038,
      "obtained": true
    },
    {
      "kind": "Mount",
      "rewardId": 15,
      "rewardName": "unicorn",
      "questRowId": 65730,
      "obtained": false
    }
  ]
}
```

## CSV

UTF-8 with a byte order mark (so Excel reads accented names correctly), comma separated, CRLF line ends, one header row, fields quoted per RFC 4180 when they contain a comma, a quote or a line break. CSV files carry the rows only; the header fields above are JSON-only.

```csv
rowId,questId,name,section,category,genre,expansion,completed,completedAt,completedAfter
65621,85,Close to Home,Main Scenario (A Realm Reborn through Endwalker),Seventh Umbral Era Main Scenario Quests,Seventh Umbral Era,A Realm Reborn,true,,
65575,39,Coming to Gridania,Sidequests,Gridanian Sidequests,Gridanian Sidequests,A Realm Reborn,true,2026-09-20T21:14:05Z,
```

```csv
kind,rewardId,rewardName,questRowId,obtained
Emote,114,Most Gentlemanly,66038,true
Mount,15,unicorn,65730,false
Minion,21,wind-up gentleman,66038,unknown
```

The rows in these samples come from the game data of 2026.09.15. Quest, section, category and genre names are in the client's language; Moonlit reward names are always the shipped English ones.
