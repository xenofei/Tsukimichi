# What Tsukimichi reads and sends

A plain statement of what the plugin looks at, what it writes, and what leaves your PC. In short: **it reads your game on this PC, keeps its files on this PC, and sends nothing anywhere. It goes online for one thing only: the optional portrait pack, when you click Download and confirm.** Settings › Advanced › Privacy & trust shows this summary in game, with the fingerprint of the build you are running.

## What it reads

All of it is read locally, through Dalamud, from the game running on your PC.

- **The game's own data files** on your disk (quest, item, NPC, place, duty and achievement sheets), in your client's language, and the game version (`ffxivgame.ver`) for the diagnostics line.
- **The character you are logged in as:** its name, world and character id, quest completion flags and journal, levels per job, unlocks (duties, aether currents, mounts, minions, emotes and the like), achievements, Grand Company and allied society standings, and the counts of items a quest asks you to hand in. These are the things the game itself shows you.
- **Where you are and what you point at,** only for the feature that needs it: the zone and position (Nearby quests, Walk to giver, and the "Needs you" stuck alert while a hand-off runs), the NPC you target when you open its menu (its kind and id only, never a player's), the item you hover or right-click (the item tooltip panel and menu entry), and the duty selected in the Duty Finder (the unlock hint).
- **Nothing about other players.** Tsukimichi reads only your own characters. While a hand-off runs and Settings › Alerts › "When you get a tell" is on, Tsukimichi notices that a tell came in and says so in a chat line only you see ("Needs you: a tell arrived."). The sender's name and the tell's text are not shown, kept or answered.
- **Other plugins, when installed,** and only through what they offer: Questionable's status, Allagan Tools' item counts, AutoDuty's duty list and the like, over Dalamud IPC; and the companion plugins' own settings files in Dalamud's `pluginConfigs` folder, read only and never written, so Settings › Automation › Companion plugins can say which recommended settings are on. A setting is changed only through that plugin's own IPC, after you click "Apply recommended settings" and confirm the list.
- **Its own files from other game clients** on the same PC, when you run more than one (multibox), from the shared folder below. It never reads another game process.

## What it writes

Only under Dalamud's plugin configuration folder, `%AppData%\XIVLauncher\pluginConfigs\` unless you moved Dalamud's:

| Where | What |
|---|---|
| `Tsukimichi.json` | Your settings (Dalamud's own plugin configuration file) |
| `Tsukimichi\characters\<id>.json` | One snapshot per character: quest states, levels and unlocks as last read |
| `Tsukimichi\characters\<id>.dates.json`, `.accepted.json`, `.abandoned.json`, `.return.json`, `.offers.json` | Quest completion dates, when quests were accepted, abandoned quests, the "Since you were away" state, and the quests the game showed you as available (its map markers and quest offers) |
| `Tsukimichi\characters\<id>.prev.json`, `.prev2.json` | Daily backups of the snapshot ([how to restore them](restore-backup.md)) |
| `Tsukimichi\characters\<id>.live.json` | While that character is logged in: who is logged in, for a second game client |
| `Tsukimichi\user\pins.json`, `overrides.json`, `discovery.json`, `characters.json` | Pins, your Moonlit verdicts, Nearby settings, per-character settings |
| `Tsukimichi\cache\` | The journal text search index, when that search is on |
| `Tsukimichi\portraits\` | The optional portrait pack, only if you downloaded it (below) |
| `Tsukimichi\exports\` (or the folder you choose) | Only when you export; your character's name only if you turn it on ([format](export-format.md)) |

Short-lived `.tmp` and `.lock` files appear beside these while they are written. Settings › Characters & data deletes everything but your settings; Forget this character deletes one character's files; uninstalling leaves the folder, so delete it to remove everything.

Besides files, Tsukimichi writes to the **clipboard** when you click a Copy or Report button (the Report block carries the quest, its state and the data version, and no character identifiers), prints **chat lines** that only you see, and writes to Dalamud's own **log** on your PC.

## What it sends

**Nothing.** Tsukimichi never uploads or checks anything online. The quest data it needs ships inside the plugin, and Dalamud installs its updates. Its one download is the optional portrait pack (below), and only when you ask for it.

A test (`Tsukimichi.Tests/Diagnostics/NoNetworkTests.cs`) fails the build if any shipped source names an HTTP client, a web request, a socket, a DNS lookup or another way to fetch an address (an XML reader, a named pipe, the Windows download libraries), except the portrait pack's one file (`Tsukimichi/Game/PortraitPackHttp.cs`), so this stays true. The exceptions are things you start yourself, with a click:

- **Links open in your browser.** "Open on…" (Lodestone, Garland Tools, the Console Games Wiki, Teamcraft, FFXIV Collect) and Settings › Advanced › Privacy & trust › "The full statement" are handed to your browser through Dalamud when you click them. Tsukimichi itself fetches nothing.
- **"Open folder"** after an export opens the folder in Windows Explorer.
- **Companion plugins act when you press their buttons.** Teleport (Lifestream), Walk to giver (vnavmesh), Send to Questionable, Run with AutoDuty, and the hand-in section's Artisan and GatherBuddy buttons: the plugin you hand the work to plays the game as you would, and the game talks to its servers as it always does. Tsukimichi sends nothing of its own.
- **Other plugins can ask Tsukimichi** over Dalamud IPC, on your PC: a quest's state and what blocks it, the next main scenario quest, your pins and abandoned quests, whether you own a quest reward. The full list is in [ipc.md](ipc.md). With the Wotsit integration on, quest and reward names are registered with Wotsit so its search finds them.

### The portrait pack (optional, since 1.20)

Giver portraits come from your own game install. For more faces, Settings › General › Look › Portrait pack offers an optional pack of NPC photos (renders by Garland Tools, credit Celes). Exactly this happens, and only after you click **Download…** and confirm a dialog that names the size, the release and the address:

- **What is fetched:** one file, `Tsukimichi-portraits.zip`, from Tsukimichi's own GitHub release (`https://github.com/xenofei/Tsukimichi/releases/download/<version>/Tsukimichi-portraits.zip`). GitHub redirects the download to its own file servers (`objects.githubusercontent.com` or `release-assets.githubusercontent.com`); a redirect anywhere else stops it. The request carries a User-Agent naming Tsukimichi's version, and nothing about you, your characters or your settings.
- **What is checked:** the file must be exactly the size and SHA-256 the plugin ships with (`Data/portrait_pack.json`), or it is deleted. Inside, only the pack's own images and manifest are accepted: no folders, no programs, nothing outside the pack's folder. Every image must match its own SHA-256 and decode as a picture before anything is installed.
- **Where it goes:** `pluginConfigs\Tsukimichi\portraits\` (`current.json` names the pack in use; a `download-<id>.part` file exists only while it downloads).
- **When:** only on that click. A plugin update with a newer pack says so in Settings and waits for you to click **Update…**; nothing downloads on its own. **Remove…** deletes the pack again.
- **Credit and source:** the photos are Garland Tools' NPC renders (garlandtools.org, photos by Celes) of Square Enix's game art. FINAL FANTASY XIV © SQUARE ENIX.

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
