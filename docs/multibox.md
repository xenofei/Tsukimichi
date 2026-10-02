# Several game clients at once (multibox)

Every game client runs its own Dalamud and its own copy of Tsukimichi. The copies never talk to each other directly: Dalamud's IPC does not cross processes, and Tsukimichi never reads another game process's memory or sends it input. What they share is the plugin's config folder, `%AppData%\XIVLauncher\pluginConfigs\Tsukimichi\`, and multibox support works entirely through the files in it.

## What you see

- A character logged in on another client shows as **live in another client**, with a ◎ dot, in the character switcher, on the Characters tab (the list, the dashboard header, Compare with and the account view) and on the status bar's pip.
- Viewing it shows the data that client saved last. That client saves its logged-in character at most every 10 seconds while something changes, and this client picks the save up within a few seconds, so what you see is at most about 15 seconds behind.
- **Forget character** is disabled for a character live in another client ("log out there first"): that client owns its files and would write them again at once.
- Pins and Moonlit verdicts (unique / not unique) made in either client are kept: each save merges with what the other client saved.
- What a character owns (mounts, minions, emotes, cards, bardings, hairstyles, duties and the other Moonlit collectibles) and when it completed each quest are saved with its snapshot, so the Moonlit tab, the Characters tab and the export show them for a character live in the other client too, "as of" that client's last save.

## How it works

**Heartbeat.** While a character is logged in, its client keeps `characters\<id>.live.json` with its process id, the time it logged in, the character's name and world, and a timestamp it refreshes every 10 seconds. The file is deleted at logout and when the plugin unloads. A heartbeat older than 30 seconds is stale: that client is gone (it crashed, or the game closed without unloading the plugin), and the character reads as a stored one again.

**Watching.** Each client watches `characters\` and `user\` with a file watcher, and also looks every 3 seconds because watchers can miss events. When another client saves a snapshot, it is read on a background thread and swapped in on the game's framework thread, so drawing never waits on a file. A stored character you are viewing is evaluated again on a background thread too.

**Who writes what.**

| File | Written by | When two clients save at once |
|---|---|---|
| `characters\<id>.json` (snapshot) | only the client where that character is logged in | Never both: another client's character is never written. |
| `characters\<id>.accepted.json`, `.abandoned.json`, `.return.json` | the same client as the snapshot | Same as the snapshot. |
| `characters\<id>.live.json` (heartbeat) | the client holding the character | A client deletes only its own heartbeats and stale ones. |
| `user\pins.json` | every client | Locked, re-read and merged: a save applies only the pins and unpins made in that client, one quest at a time, so two clients editing the same character's pins keep both edits. |
| `user\overrides.json` | every client | Locked, re-read and merged: a save writes only the verdicts changed in that client. |
| `user\discovery.json` (Nearby quests settings) | every client | Last save wins (settings). |
| Dalamud's `pluginConfigs\Tsukimichi.json` (Settings) | every client, through Dalamud | Last save wins (settings). |
| `cache\journal-index.<version>.<language>.bin` | every client that enables journal search | Each client keeps its own language's index; only other game versions not rebuilt for 30 days are removed (two game installs at different patch levels may share the folder). |
| `exports\` | you, on purpose | Each export is a new file. |

Every file is written to a temporary file of its own and then renamed into place, so a save is either complete or not there at all, and two clients saving the same file can never leave half of one save in it. If another client is reading the file at that moment, the rename waits a few milliseconds for it. Snapshots, their sidecars, pins and verdicts are saved on one background queue, so neither that wait nor the lock on the shared files ever holds up the game; a file that stays unwritable (read-only, or refused by its permissions) fails at once and is tried again at the next save.

**Files a client cannot read.** Looking at the other clients' characters never moves a file aside. A snapshot written by a newer Tsukimichi (a higher schema version) is skipped with a warning in the log until this client is updated, and no client ever moves it aside; a damaged one is skipped too and left for the client that owns it, which replaces it on its next save. A pins or verdicts file that cannot be read when the other client's change arrives leaves this client's copy as it is, and a save onto one that cannot be parsed keeps every pin and verdict this client holds.

**Settings are per client, last save wins.** Dalamud stores the settings and each client writes them whole. If you change a setting in one client and a different setting in the other, the client that saves last decides both. This is rarely noticed (settings change seldom) and is accepted; per-character spoiler overrides and "Before you continue" notices live in the settings too and follow the same rule.

**The same character in two clients.** The game does not allow it, but if the files ever say so (a copied config folder, a very late heartbeat), the newer login keeps writing and the other client stops saving that character and writes a warning to the Dalamud log. It resumes by itself once the other heartbeat is gone.

**Delete all data** (Settings › Data) deletes every snapshot, sidecar, pin, verdict and index, except a character live in another client: its snapshot, sidecars and heartbeat stay, since that client owns them and would write them again within seconds. It also deletes its own heartbeat (written again at once while you are logged in) and heartbeats left by clients that are gone. Pins and verdicts are shared, so the other client's copies disappear too, except the pins of the character live there; the files are rewritten under the same lock as any save rather than deleted, so a pin the other client saves at that moment cannot bring the deleted ones back, and the other client merges the change in within a few seconds. **Forget character** deletes that character's snapshot, sidecars and heartbeat, and is not offered for a character live in another client.

## What it can't do

- **Different config folders don't see each other.** Both clients must use the same Dalamud config folder. XIVLauncher profiles with different roaming paths (`--roamingPath`), or two Windows user accounts, each have their own folder and see only their own characters. There is no "shared data folder" setting: moving the whole folder safely would need more than a setting, so this is a limitation for now.
- **Nothing live from the other game.** Only what the other client saved: its character's quests, levels and standings as of its last save. Chat notices, the item and Duty Finder hints and the IPC gates speak for the character logged in on the client you are looking at, as before.
- **No faster than the saves.** The other client saves while its character changes, at most every 10 seconds, so a quest turned in there shows here up to about 15 seconds later.
- **Different game versions or plugin versions.** Both clients read each other's files the way they read their own; a client on an older Tsukimichi still writes the old way (its saves are complete files, but it may save pins or verdicts over the other client's).
