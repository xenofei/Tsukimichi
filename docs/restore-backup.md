# Restoring a character from its backup

Tsukimichi saves each character to `%AppData%\XIVLauncher\pluginConfigs\Tsukimichi\characters\<id>.json` while you play. Since 1.5.0 it also keeps two backups per character beside it:

- `<id>.prev.json`: a copy of the saved file as it was before a save, refreshed at most once a day. It holds the character as it was up to a day earlier.
- `<id>.prev2.json`: the backup `<id>.prev.json` replaced at its last refresh, one generation older.

A refresh never copies a saved file that lost many completed quests compared with the backup it would replace (the same test as the plausibility guard below). So if a bad reading did get saved shortly before a refresh, the backup keeps the good copy instead of taking the bad one a minute later. Until the character's saved progress catches up again, the backup stays as it was, so it can be older than a day.

The character's quest completion dates are in their own file, `<id>.dates.json`, which an older Tsukimichi (1.4 or before) never touches. Restoring a backup does not need it to change: leave it where it is.

Three things keep bad data out of the save in the first place:

- **The plausibility guard.** If the game suddenly reports far fewer completed quests than a moment ago (more than 50 at once, or more than 10 % of them), or a journal of several quests empties with none of them completed, Tsukimichi does not save that reading. It keeps what it had, tries again a little later, and says once in chat: "Tsukimichi skipped saving: the game reported far fewer completed quests than before; your saved progress is unchanged." Seasonal and repeatable quests are left out of the count, since the game resets those on its own schedule, and so are event quests the game takes out of your journal when an event ends. The first reading after you log in is checked the same way against the saved file. The next normal reading is saved as usual.
- **A reading that stays the same is real.** A bad reading comes right within seconds. If the game keeps reporting the very same loss for a few minutes, Tsukimichi accepts it: it first copies the save it is about to overwrite to `<id>.prev.json` (the backup before that moves to `<id>.prev2.json`), then saves the new reading, and says in chat: "Tsukimichi accepted the game's reading after it stayed the same for a few minutes; a backup of the earlier save is kept." A reading with no completed quests and an empty journal is never accepted this way.
- **Atomic writes.** Every file is written to a temporary file first and then moved into place, so a crash mid-save leaves the old file whole.

## When to restore

Restore only if a character's saved progress is clearly wrong (many completed quests show as not done) and logging in with that character does not put it right. For the logged-in character, the game itself is the source: the next login reads everything again. The backup matters for a character you are not logged in with, whose file is all Tsukimichi has.

## How to restore

1. Close the game (or at least log out of every client with that character, and unload Tsukimichi in `/xlplugins`).
2. Open `%AppData%\XIVLauncher\pluginConfigs\Tsukimichi\characters\`.
3. Find the character's files. The `<id>` is the same number in `<id>.json`, `<id>.prev.json` and `<id>.prev2.json`; the character's name is near the top of each file (`"name": "…"`), and the time it was saved is in `"takenUtc"`.
4. Pick the backup to restore: `<id>.prev.json` is the newer one; use `<id>.prev2.json` if that one is wrong too.
5. Keep the current file aside: rename `<id>.json` to `<id>.bad.json`.
6. Rename the backup you picked to `<id>.json`.
7. Start the game and open Tsukimichi. The Characters tab shows the restored progress. Delete `<id>.bad.json` once you are happy.

The backups are removed with the character: **Forget this character** and Settings › Data › **Delete all Tsukimichi data** delete both, along with the snapshot, its completion dates and its other files.
