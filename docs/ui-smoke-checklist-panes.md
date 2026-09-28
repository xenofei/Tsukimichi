# UI smoke checklist — Moonlit pane, Characters pane, Config window

Manual, in game, after the panes are wired into MainWindow (T4.5). Companion to `ui-smoke-checklist.md`.
Tick each line on a character that has done at least one quest with a unique reward (Her Last Vow → Most Gentlemanly is the reference).

## Moonlit pane

- [ ] Left column lists **All** first, then one line per reward kind present in `unique_quests.json`, each with a small filling moon and `obtained/total`; the moon's lit fraction matches the ratio (0/1175 is a new moon, 5/6 nearly full, 136/177 gibbous; only `n/n` is a full moon).
- [ ] Kinds with no readable entry on the viewed character (logged out, a stored snapshot, or emote/mount/minion rows without the live client) show the veiled moon with a tooltip instead of an empty moon.
- [ ] Selecting a kind filters the table; selecting **All** shows every entry. The selected line is highlighted.
- [ ] Table columns: Have (moon), Reward (icon + name), Kind, Quest, State (moon), Confidence. Columns resize, reorder and hide from the header context menu; the header stays frozen while scrolling.
- [ ] Dragging the **State** header left of Quest (grab the middle of the header, not its edge) moves the column; Quest keeps its width. The same works for **Have**.
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
- [ ] Main column header: name, world, `Live · time` or `Snapshot taken <time> (<age>)`, then `completed · in journal · current job` (job names read as titles, e.g. `Black Mage`).
- [ ] **Completion by journal section**: `All quests` in gold first, then one row per journal section in journal order, each with a filling moon, `done/total` and a percent; the numbers match the Journal tree's section nodes for the same character.
- [ ] **Moonlit treasures**: Emotes, Mounts, Minions, Orchestrion rolls, Triple Triad cards and Duty unlocks with `obtained/total` equal to the Moonlit pane's left column; veiled moons and the offline hint while viewing a stored snapshot.
- [ ] **Pinned quests**: the viewed character's pins (from `user/pins.json`) with the state moon, name and next step; clicking a name reveals it in the Journal; pinning or unpinning in the table updates the list within a poll or two; a character without pins reads the hint.
- [ ] **Recent activity**: on the live character the last ten events newest first with time, kind (Completed gold, Accepted silver, Newly available gold, Abandoned eclipse) and quest name; accepting a quest adds a row within one poll. On a stored character it says activity is recorded for the logged-in character only.
- [ ] **Job levels** grouped under Tanks, Healers, Melee DPS, Physical ranged DPS, Magical ranged DPS, Disciples of the Hand, Disciples of the Land, sorted by level within a group, each row with the game's job icon (062101…). No `Job 44` / `Job 45` rows on any character (ClassJob placeholder rows are skipped on capture and on display).
- [ ] A base class is hidden once its job is unlocked (Gladiator disappears when the Paladin unlock quest 66591 is complete; Paladin is absent until then). Jobs without a class (Dark Knight, Sage, Viper…) always show.
- [ ] Grand Company line shows the company and rank; allied societies table shows society, rank name and reputation; allowances line shows society and leve counts.
- [ ] No per-frame allocation while the dashboard is open (Dalamud profiler); the view model rebuilds only when the session version, the viewed character or the minute changes.
- [ ] **Export JSON…** writes `<config>/exports/<Name>-<yyyyMMdd-HHmm>.json` (spaces become `_`), the path appears in gold under the buttons for a few seconds and in the log; the file equals the stored snapshot file.
- [ ] **Forget this character** is disabled with a tooltip while viewing the live character; on a stored character it opens a confirm popup, **Cancel** keeps the file, **Forget** deletes `characters/<id>.json`, removes it from the list and the view falls back to the live character.
- [ ] Account view: with no quest selected it says to select one; after selecting a quest in the Journal it lists every character with a moon glyph, state name and the next step text, evaluated from each snapshot without switching characters. Completing that quest on the live character updates its row within one poll.
- [ ] A corrupt snapshot file for another character shows `snapshot unreadable` for that row and a warning in the log, nothing else breaks.

## Config window

- [ ] `/tsukimichi config` (once wired) opens **Tsukimichi Settings**; it is resizable with a sane minimum size.
- [ ] Poll interval slider moves in 0.1 s steps from 0.5 to 5.0 s; releasing it saves (check `config.json` timestamp) and the poller's cadence follows without a reload.
- [ ] Under the slider: `Each poll costs about Y ms; 1 s is the default and is safe.` (a generic sentence before the first poll); Y is a small number of milliseconds and stable while idle.
- [ ] **Help** section: **Show help** opens the help window in front; **Show help on first run** reflects `ShowHelpOnFirstRun` and saves on click.
- [ ] Chat notice checkbox saves on click; **Include main scenario** is greyed out until the notice is on and saves on click.
- [ ] **Show Unlisted bucket** saves on click and the callback fires (the Journal tree shows or hides the Unlisted node).
- [ ] **Delete all Tsukimichi data** opens the first modal; **Continue** opens the second; **Cancel** on either does nothing. **Delete everything** removes `characters/*.json`, `user/pins.json` and `user/overrides.json`, the Characters list empties, hidden Moonlit quests reappear, and the window shows the done message.
- [ ] About lists the plugin version (matches the csproj Version), the reward-data game version and generation date, the unique entry count, the four curated counts, and `Catalog: <n> quests, <Language>` once the catalog is built (`loading` before, the error text after a failed build).
- [ ] About ends with `Last poll: X.XX ms · average Y.YY ms · N polls` once logged in (`No polls yet` before); N grows about once per interval and the line does not flicker or allocate per frame.

## Help window

- [ ] With a fresh config (`ShowHelpOnFirstRun` true) the help window opens by itself the first time the main window opens, on **Getting started**, and the flag is written false to `config.json`; it does not open again on later opens or reloads.
- [ ] `/tsukimichi help` (once the command is merged), **Show help** in Settings and the config checkbox all open it; it is resizable down to 480×360 and remembers its size.
- [ ] Left list: Getting started, The moon phases, Filters and chips, Quest table and detail pane, Moonlit and confidence, Characters and snapshots, Commands, Tips; the selected topic is highlighted and its title reads in gold on the Night panel.
- [ ] **The moon phases** draws all eight glyphs at 16 px radius with shading and a highlight arc on the lit regions, the state name in its state colour, the glyph name, a one-line meaning and the filters that include or hide it.
- [ ] **Commands** lists `/tsukimichi`, `/tsuki`, `search <text>`, `config`, `help` and `glyphs`; **Tips** lists twelve bullets. Every page wraps at the window width with no horizontal scroll.
- [ ] **Show on first run** at the bottom of the topic list toggles the same setting as the config window and saves on click.

## Glyphs (`/tsukimichi glyphs`)

- [ ] Columns read `12 px · table row`, `20 px · toolbar`, `40 px · detail header`, `64 px · help` plus the slider column; the 12 px moons are flat and crisp, from 20 px up the lit regions show a brighter core and a faint arc on the upper-left of the lit part only (no arc on the dark left of Ready or Accepted).
- [ ] The filling row at 0 / 0.25 / 0.5 / 0.75 / 1 reads new, crescent, half, gibbous, full at every size; rings and the Ready glow are unchanged.
