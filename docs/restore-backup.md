# Restoring a character from its backup

Tsukimichi saves each character to `%AppData%\XIVLauncher\pluginConfigs\Tsukimichi\characters\<id>.json` while you play. Since 1.5.0 it also keeps one backup per character beside it, `<id>.prev.json`: a copy of the saved file as it was before a save, refreshed at most once a day. It holds the character as it was up to a day earlier.

Two things keep bad data out of the save in the first place:

- **The plausibility guard.** If the game suddenly reports far fewer completed quests than a moment ago (more than 50 at once, or more than 10 % of them), or a journal of several quests empties with none of them completed, Tsukimichi does not save that reading. It keeps what it had, tries again a little later, and says once in chat: "Tsukimichi skipped saving: the game reported far fewer completed quests than before; your saved progress is unchanged." Seasonal and repeatable quests are left out of the count, since the game resets those on its own schedule. The next normal reading is saved as usual, and logging in again starts over.
- **Atomic writes.** Every file is written to a temporary file first and then moved into place, so a crash mid-save leaves the old file whole.

## When to restore

Restore only if a character's saved progress is clearly wrong (many completed quests show as not done) and logging in with that character does not put it right. For the logged-in character, the game itself is the source: the next login reads everything again. The backup matters for a character you are not logged in with, whose file is all Tsukimichi has.

## How to restore

1. Close the game (or at least log out of every client with that character, and unload Tsukimichi in `/xlplugins`).
2. Open `%AppData%\XIVLauncher\pluginConfigs\Tsukimichi\characters\`.
3. Find the character's files. The `<id>` is the same number in `<id>.json` and `<id>.prev.json`; the character's name is near the top of each file (`"Name": "…"`).
4. Keep the current file aside: rename `<id>.json` to `<id>.bad.json`.
5. Rename `<id>.prev.json` to `<id>.json`.
6. Start the game and open Tsukimichi. The Characters tab shows the restored progress. Delete `<id>.bad.json` once you are happy.

The backup is removed with the character: **Forget this character** and Settings › Data › **Delete all Tsukimichi data** delete it along with the snapshot and its other files.
