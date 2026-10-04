# What Tsukimichi reads and sends

A plain statement of what the plugin looks at, what it writes, and what leaves your PC. In short: **it reads your game on this PC, keeps its files on this PC, and has no network code. Nothing is sent anywhere.** Settings › Advanced › Privacy & trust shows this summary in game, with the fingerprint of the build you are running.

## What it reads

All of it is read locally, through Dalamud, from the game running on your PC.

- **The game's own data files** on your disk (quest, item, NPC, place, duty and achievement sheets), in your client's language, and the game version (`ffxivgame.ver`) for the diagnostics line.
- **The character you are logged in as:** its name, world and character id, quest completion flags and journal, levels per job, unlocks (duties, aether currents, mounts, minions, emotes and the like), achievements, Grand Company and allied society standings, and the counts of items a quest asks you to hand in. These are the things the game itself shows you.
- **Where you are and what you point at,** only for the feature that needs it: the zone and position (Nearby quests, Walk to giver, and the "Needs you" stuck alert while a hand-off runs), the NPC you target when you open its menu (its kind and id only, never a player's), the item you hover or right-click (the item tooltip panel and menu entry), and the duty selected in the Duty Finder (the unlock hint).
- **Nothing about other players.** Tsukimichi reads only your own characters. One exception: while a hand-off runs and Settings › Alerts › "When you get a tell" is on, an incoming tell's sender name goes into a chat line only you see ("Needs you: … sent you a tell."). The tell's text is not read, kept or answered.
- **Other plugins, when installed,** and only through what they offer: Questionable's status, Allagan Tools' item counts, AutoDuty's duty list and the like, over Dalamud IPC; and the companion plugins' own settings files in Dalamud's `pluginConfigs` folder, read only and never written, so Settings › Automation › Companion plugins can say which recommended settings are on. A setting is changed only through that plugin's own IPC, after you click "Apply recommended settings" and confirm the list.
- **Its own files from other game clients** on the same PC, when you run more than one (multibox), from the shared folder below. It never reads another game process.

## What it writes

Only under Dalamud's plugin configuration folder, `%AppData%\XIVLauncher\pluginConfigs\` unless you moved Dalamud's:

| Where | What |
|---|---|
| `Tsukimichi.json` | Your settings (Dalamud's own plugin configuration file) |
| `Tsukimichi\characters\<id>.json` | One snapshot per character: quest states, levels and unlocks as last read |
| `Tsukimichi\characters\<id>.dates.json`, `.accepted.json`, `.abandoned.json`, `.return.json` | Quest completion dates, when quests were accepted, abandoned quests, and the "Since you were away" state |
| `Tsukimichi\characters\<id>.prev.json`, `.prev2.json` | Daily backups of the snapshot ([how to restore them](restore-backup.md)) |
| `Tsukimichi\characters\<id>.live.json` | While that character is logged in: who is logged in, for a second game client |
| `Tsukimichi\user\pins.json`, `overrides.json`, `discovery.json`, `characters.json` | Pins, your Moonlit verdicts, Nearby settings, per-character settings |
| `Tsukimichi\cache\` | The journal text search index, when that search is on |
| `Tsukimichi\exports\` (or the folder you choose) | Only when you export; your character's name only if you turn it on ([format](export-format.md)) |

Short-lived `.tmp` and `.lock` files appear beside these while they are written. Settings › Characters & data deletes everything but your settings; Forget this character deletes one character's files; uninstalling leaves the folder, so delete it to remove everything.

Besides files, Tsukimichi writes to the **clipboard** when you click a Copy or Report button (the Report block carries the quest, its state and the data version, and no character identifiers), prints **chat lines** that only you see, and writes to Dalamud's own **log** on your PC.

## What it sends

**Nothing.** Tsukimichi has no network code: it never uploads, downloads or checks anything online. The quest data it needs ships inside the plugin, and Dalamud installs its updates.

A test (`Tsukimichi.Tests/Diagnostics/NoNetworkTests.cs`) fails the build if any shipped source names an HTTP client, a web request, a socket or a DNS lookup, so this stays true. The exceptions are things you start yourself, with a click:

- **Links open in your browser.** "Open on…" (Lodestone, Garland Tools, the Console Games Wiki, Teamcraft, FFXIV Collect) and Settings › Advanced › Privacy & trust › "The full statement" are handed to your browser through Dalamud when you click them. Tsukimichi itself fetches nothing.
- **"Open folder"** after an export opens the folder in Windows Explorer.
- **Companion plugins act when you press their buttons.** Teleport (Lifestream), Walk to giver (vnavmesh), Send to Questionable, Run with AutoDuty, and the hand-in section's Artisan and GatherBuddy buttons: the plugin you hand the work to plays the game as you would, and the game talks to its servers as it always does. Tsukimichi sends nothing of its own.
- **Other plugins can ask Tsukimichi** over Dalamud IPC, on your PC: a quest's state and what blocks it, the next main scenario quest, your pins and abandoned quests, whether you own a quest reward. The full list is in [ipc.md](ipc.md). With the Wotsit integration on, quest and reward names are registered with Wotsit so its search finds them.

**If a portrait pack is ever offered** (feature plan v7 F4, not built yet): the giver portraits ship from your own game install first. Should an optional pack ever be downloadable for more coverage, it will be off until you turn it on, and this page and Settings will say exactly what it fetches, from where, and when.

## Check the build you run

Every GitHub release lists the SHA-256 of `latest.zip` and of the `Tsukimichi.dll` inside it, in the release notes and in `SHA256SUMS.txt` beside `latest.zip`. Both are made by the [release workflow](../.github/workflows/release.yml) from the tagged commit, on GitHub's servers, not on a developer's PC.

To compare:

1. In game, Settings › Advanced › Privacy & trust shows the version and the SHA-256 of the `Tsukimichi.dll` you are running, worked out on your PC. Click **Copy**.
2. Open the release of that version on [GitHub Releases](https://github.com/xenofei/Tsukimichi/releases) and compare it with the `Tsukimichi.dll` line.

Or outside the game, in PowerShell (letter case does not matter):

```powershell
Get-FileHash "$env:APPDATA\XIVLauncher\installedPlugins\Tsukimichi\*\Tsukimichi.dll" -Algorithm SHA256
```

A build you made yourself or a dev build shows a different hash; that is expected.
