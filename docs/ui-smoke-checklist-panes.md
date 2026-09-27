# UI smoke checklist — Moonlit pane, Characters pane, Config window

Manual, in game, after the panes are wired into MainWindow (T4.5). Companion to `ui-smoke-checklist.md`.
Tick each line on a character that has done at least one quest with a unique reward (Her Last Vow → Most Gentlemanly is the reference).

## Moonlit pane

- [ ] Left column lists **All** first, then one line per reward kind present in `unique_quests.json`, each with a small filling moon and `obtained/total`; the moon's lit fraction matches the ratio.
- [ ] Selecting a kind filters the table; selecting **All** shows every entry. The selected line is highlighted.
- [ ] Table columns: Have (moon), Reward (icon + name), Kind, Quest, State (moon), Confidence. Columns resize, reorder and hide from the header context menu; the header stays frozen while scrolling.
- [ ] Her Last Vow → Most Gentlemanly shows a full moon in Have on a character that has the emote and a new moon on one that does not.
- [ ] While logged out, or viewing a stored snapshot, Have shows the veiled moon for emote/mount/minion/orchestrion/ornament/card/aether current/duty rows and the toolbar says obtained states need the live character; action/trait/job/title/achievement/system rows still answer from the snapshot's completion bit.
- [ ] Reward icons: an item-based reward (mount, minion, orchestrion) shows the item icon; an emote from `Quest.EmoteReward` shows the emote icon; an entry with no icon shows a small moon instead (filled when obtained).
- [ ] **Hide obtained** removes full-moon rows; the `n / total` counter next to the filter updates.
- [ ] Typing in the filter narrows by reward name, quest name or kind (case-insensitive); clearing it restores every row. No stutter while typing with All selected (4k rows).
- [ ] Clicking a quest name switches to the Journal tab, scopes the tree to that genre (Unlisted node for genre-less quests) and selects the quest in the detail pane.
- [ ] Clicking a reward name selects the row and sets the detail pane's quest without leaving the Moonlit tab.
- [ ] State column glyph matches the Journal for the same quest; hovering shows the state name.
- [ ] Confidence badge: shipped entries read `static` in Silver, curated `curated` in Moon, user marks `yours` in Eclipse; hovering shows the source field (e.g. `Quest.EmoteReward`, `curated/system_unlocks.json`).
- [ ] Right-click a reward → **Show in Journal** works; **Not unique (hide)** removes every row of that quest, writes `user/overrides.json` (check the file) and the kind counts drop.
- [ ] After hiding, a quest marked unique again from the detail pane (or by editing the file and reloading the plugin) comes back; **Restore shipped verdict** appears on rows of quests that have an override.
- [ ] With `unique_quests.json` removed from the plugin folder the pane says reward data is not shipped in this build; everything else still works.
- [ ] Scrolling the table with All selected stays smooth (list clipper); no per-frame GC spikes in the Dalamud profiler.

## Characters pane

- [ ] Left column lists every stored snapshot, newest capture first, name on the first line, `World · age · n quests completed` on the second; the logged-in character carries ● and reads `Live` instead of an age.
- [ ] World names resolve (not ids) when the pane was given `IDataManager`.
- [ ] Selecting a stored character views it: the Journal re-evaluates for that snapshot, the sync glyph goes veiled, the choice survives a plugin reload (Configuration.ViewedContentId).
- [ ] Selecting the live character again follows the live state.
- [ ] Main column header: name, world, `Live · time` or `Snapshot taken <time> (<age>)`, then `completed · in journal · current job`.
- [ ] Job levels table sorted by level descending with job names from the catalog bundle; Grand Company line shows the company and rank; allied societies table shows society, rank name and reputation; allowances line shows society and leve counts.
- [ ] **Export JSON…** writes `<config>/exports/<Name>-<yyyyMMdd-HHmm>.json` (spaces become `_`), the path appears in gold under the buttons for a few seconds and in the log; the file equals the stored snapshot file.
- [ ] **Forget this character** is disabled with a tooltip while viewing the live character; on a stored character it opens a confirm popup, **Cancel** keeps the file, **Forget** deletes `characters/<id>.json`, removes it from the list and the view falls back to the live character.
- [ ] Account view: with no quest selected it says to select one; after selecting a quest in the Journal it lists every character with a moon glyph, state name and the next step text, evaluated from each snapshot without switching characters. Completing that quest on the live character updates its row within one poll.
- [ ] A corrupt snapshot file for another character shows `snapshot unreadable` for that row and a warning in the log, nothing else breaks.

## Config window

- [ ] `/tsukimichi config` (once wired) opens **Tsukimichi Settings**; it is resizable with a sane minimum size.
- [ ] Poll interval slider moves in 0.1 s steps from 0.5 to 5.0 s; releasing it saves (check `config.json` timestamp) and the poller's cadence follows without a reload.
- [ ] Chat notice checkbox saves on click; **Include main scenario** is greyed out until the notice is on and saves on click.
- [ ] **Show Unlisted bucket** saves on click and the callback fires (the Journal tree shows or hides the Unlisted node).
- [ ] **Delete all Tsukimichi data** opens the first modal; **Continue** opens the second; **Cancel** on either does nothing. **Delete everything** removes `characters/*.json`, `user/pins.json` and `user/overrides.json`, the Characters list empties, hidden Moonlit quests reappear, and the window shows the done message.
- [ ] About lists the plugin version (matches the csproj Version), the reward-data game version and generation date, the unique entry count, the four curated counts, and `Catalog: <n> quests, <Language>` once the catalog is built (`loading` before, the error text after a failed build).
