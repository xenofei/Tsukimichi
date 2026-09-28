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
- [ ] **Display** section: **Window scale** slides 0.90–1.60 (default 1.15) and **Icon scale** 0.80–2.00 (default 1.25); each writes `UiScale` / `IconScale` while dragging and saves on release (check `config.json`); closing the window with a slider still held saves too. Reopening the window shows the stored values.
- [ ] **Help** section: **Start tutorial** opens the main window and starts the tour at step 1; **Show help** opens the help window in front; **Offer the tutorial on first run** reads as the inverse of `TutorialCompleted` and saves on click.
- [ ] Chat notice checkbox saves on click; **Include main scenario** is greyed out until the notice is on and saves on click.
- [ ] **Show Unlisted bucket** saves on click and the callback fires (the Journal tree shows or hides the Unlisted node).
- [ ] **Delete all Tsukimichi data** opens the first modal; **Continue** opens the second; **Cancel** on either does nothing. **Delete everything** removes `characters/*.json`, `user/pins.json` and `user/overrides.json`, the Characters list empties, hidden Moonlit quests reappear, and the window shows the done message.
- [ ] About lists the plugin version (matches the csproj Version), the reward-data game version and generation date, the unique entry count, the four curated counts, and `Catalog: <n> quests, <Language>` once the catalog is built (`loading` before, the error text after a failed build).
- [ ] About ends with `Last poll: X.XX ms · average Y.YY ms · N polls` once logged in (`No polls yet` before); N grows about once per interval and the line does not flicker or allocate per frame.

## Help window

- [ ] The help window no longer opens by itself: a fresh config gets the tutorial offer instead (see Tutorial). `/tsukimichi help`, the toolbar's help button and **Show help** in Settings open it in front; it is resizable down to 560×380 and remembers its size.
- [ ] Rail (left): a **Search help** box, then eight topics each with a FontAwesome icon: Quick start (rocket), The moon phases (moon), Filters and chips (filter), Reading a quest (open book), Moonlit treasures (gem), Characters and snapshots (users), Commands (terminal), Tips (lightbulb). The active topic has a Moon bar on its left edge, a faint gold row background and a gold icon; the others' icons are Dusk.
- [ ] Typing in the search box filters the rail by title and body text (case-insensitive): `override` keeps Filters, Moonlit and Quick start; `xyz` shows `No topic matches.`; clearing it restores all eight. The content pane keeps showing the selected topic while filtering.
- [ ] Content (right) is a Night panel with rounded corners: the topic title in a larger gold font, a one-sentence lede in Dusk, then the blocks. Every paragraph is short (under ~60 words) and wraps at the pane width with no horizontal scroll at 560 px wide.
- [ ] **Quick start**: five numbered steps with a gold circle number. Step 1 has no button; **Try it** on step 2 opens the main window with the filter panel open, step 3 switches to Moonlit, step 4 to Characters, step 5 starts the tutorial (the overlay appears on the main window). Two tips follow; the second has an **Open settings** button that toggles Settings.
- [ ] **The moon phases**: eight rows, each with the glyph at 18 px radius (shading and highlight arc visible), the state name in its state colour, the glyph name in Dusk, a one-line meaning, and `Shown by` followed by pill chips (`State: …` plus `Hide completed off`, `Available now`, `Available now off` or `Left out of totals` as appropriate); a hairline separates rows. Below them a card with three filling moons (new, half, full) explains the progress moon.
- [ ] **Filters and chips**: six rounded, bordered cards (Hide completed, Available now, State, More filters, Chips, Nothing matches), each with a gold icon and a title line, then a tip callout with a lightbulb and a gold bar on the left.
- [ ] **Reading a quest**: cards for Requirements, Path (mentions folded steps), Unlocks next, Giver actions and Provenance, then the right-click tip.
- [ ] **Moonlit treasures**: cards What unique means and Confidence badges, then four coloured chips with meanings (`static` silver, `community` dusk, `curated` gold, `yours` eclipse), then Have, Overrides and Restore cards.
- [ ] **Characters and snapshots**: five cards (Snapshot, View another character, Dashboard, Account view, Export and forget).
- [ ] **Commands**: six key caps (monospace on a raised box with a darker bottom edge) for `/tsukimichi`, `/tsuki`, `/tsukimichi search <text>`, `/tsukimichi config`, `/tsukimichi help`, `/tsukimichi glyphs`, each with its meaning beside it.
- [ ] **Tips**: twelve lightbulb callouts, no bullet characters.
- [ ] No per-frame allocation while the window is open (Dalamud profiler): topic search text is built once; scrolling any page is smooth.

## Tutorial (interactive tour)

- [ ] First run: with `TutorialCompleted` false (fresh config) the welcome card appears over the main window the first time it opens this session, with **Take the tour** and **Not now**. **Not now** closes it and writes `TutorialCompleted: true`; it is not offered again on later opens or reloads. Setting **Offer the tutorial on first run** in Settings brings the offer back on the next plugin load.
- [ ] **Take the tour** goes straight to step 2 (Search) on the Journal tab. The toolbar's tutorial button, **Start tutorial** in Settings, step 5 of Quick start and **Tutorial preview** in the glyph window all start at step 1 (the welcome card with Next / Skip) and switch to Journal.
- [ ] While the tour runs the main window area (the union of every recorded region) is dimmed with Night at about 70 % except a rounded cutout around the target and around the card; the target has a 2 px gold border with three fading rings outside it. The card is not dimmed and sits beside the target with a 14 px gap: to the right, or to the left when the target is near the right screen edge, else below, else above; it never leaves the screen. Cards without a target (welcome, finish) are centred over the window.
- [ ] Card: `n of 14` in Dusk, the title in a larger gold font, the body (under 50 words) wrapped at 330 px, then **Back** (from step 2 on), **Next** and **Skip** at the right. Clicking anywhere outside the card does nothing to the tour (the window underneath stays usable); **Esc** with the card focused skips.
- [ ] Steps and highlights, in order: 1 Welcome (none) · 2 Search (`search`) · 3 Filters (`filterPanel` + `filtersButton`; the panel opens on entering the step) · 4 Chips (`chips`) · 5 Three tabs (`tabs`, switches to Journal) · 6 Journal tree (`tree`) · 7 Quest table (`table`) · 8 Requirements (`detail.requirements`, falls back to `detail`) · 9 Path and unlocks next (`detail.path`) · 10 Giver actions (`detail.giver`) · 11 Moonlit treasures (`moonlit.kinds` + `moonlit.table`, switches to Moonlit) · 12 Characters (`characters.dashboard`, falls back to `characters.list`, switches to Characters) · 13 Help, tour and settings (`helpButton` + `tutorialButton` + `settingsButton`) · 14 That is the road (none).
- [ ] With no quest selected, steps 8–10 highlight the whole detail pane; with a region missing entirely (e.g. no chips recorded) the card still shows, without a highlight, and Next still works.
- [ ] **Back** returns to the previous step and re-applies its tab; going from 12 back to 10 lands on the Journal tab with the detail pane highlighted.
- [ ] Finish card: **Open help** opens the help window and ends the tour; **Done** ends it. Either writes `TutorialCompleted: true` to `config.json`. **Skip** or Esc ends the tour without writing the flag.
- [ ] Closing the main window mid-tour hides the overlay; reopening it resumes the same step.
- [ ] No per-frame allocation while the tour runs (Dalamud profiler): step texts and `n of 14` strings are built once; the dimming uses stack buffers.

## Glyphs (`/tsukimichi glyphs`)

- [ ] Columns read `12 px · table row`, `20 px · toolbar`, `40 px · detail header`, `64 px · help` plus the slider column; the 12 px moons are flat and crisp, from 20 px up the lit regions show a brighter core and a faint arc on the upper-left of the lit part only (no arc on the dark left of Ready or Accepted).
- [ ] The filling row at 0 / 0.25 / 0.5 / 0.75 / 1 reads new, crescent, half, gibbous, full at every size; rings and the Ready glow are unchanged.
- [ ] **Tutorial preview** (top row, after the scale readout) starts the tour on the main window exactly as the toolbar button does, so it can be checked without touching the config.
