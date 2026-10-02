# Tsukimichi — FFXIV quest tracker (Dalamud)

Every FINAL FANTASY XIV quest, what blocks it, and which rewards only a quest can give. A [Dalamud](https://github.com/goatcorp/Dalamud) plugin, installed from a custom repository.

Tsukimichi (月道, "the moon's path") takes *tsuki* from the author's character, Michiru Tsukikage (月影, moon-shadow), and *michi*, path. Every quest is a step on the road and the moon fills as you walk it.

- Source: this repository, [MIT licensed](LICENSE).
- Releases: [GitHub Releases](https://github.com/xenofei/Tsukimichi/releases), built by GitHub Actions from the tagged commit.
- Issues: [GitHub Issues](https://github.com/xenofei/Tsukimichi/issues).

## Install

1. In game, type `/xlsettings`, open the **Experimental** tab, and under **Custom Plugin Repositories** paste:
   ```
   https://raw.githubusercontent.com/xenofei/Tsukimichi/main/pluginmaster.json
   ```
2. Click **+**, then **Save and Close**.
3. Type `/xlplugins`, search for **Tsukimichi**, and click **Install**.
4. `/tsukimichi` (or `/tsuki`) opens the window. A short tour is offered on first run.

Updates arrive through the plugin installer like any other plugin.

## What it does

- **Journal tab**: the game's own journal hierarchy (section, category, genre) with done/total counts and a filling moon per node; a sortable, searchable quest table; a detail pane with every requirement marked met or not and the exact gap ("needs Sworn, you are Trusted"), the rewards, the prerequisite path as a trail of moons, what the quest unlocks next, and the giver with map-flag and journal buttons. The Unlock quests node gathers every quest that opens a duty, system, job, action or trait.
- **Eight quest states as moon glyphs**, one name each everywhere: Ready (first quarter, glowing), Ready on another job, In journal (waxing gibbous), Blocked (new moon, always with what blocks it), Done today / Done this week (repeatables), Completed (full), Locked out (for good, such as a Grand Company choice not taken) and Not checked. The glyphs differ in shape, not only colour; Help › Moon phases is the legend and `docs/glossary.md` the reference.
- **Filters and presets**: hide completed, available now (both with per-category overrides), state, expansion, level, job, reward kind, repeatable, seasonal, pinned, unlisted, added in (one patch series, such as 7.5x); presets for feature quests, quests around your level and stalled quests. Active filters show as chips; an empty table says which filters emptied it.
- **Moonlit tab**: unique quest rewards, the ones that exist nowhere else (emotes, mounts, minions, orchestrion rolls, Triple Triad cards, gear, duties, systems and more), with whether you already own each one, the quest's state and a confidence badge saying whether the claim comes from the game data or a curated list. Your own verdicts (mark unique, hide as not unique) are kept and can be restored.
- **Characters tab**: a snapshot of every character on the account with a dashboard (completion by section, Moonlit progress, pins, abandoned quests, recent activity, job quest ladders, story chains, Grand Company and allied society standings), the main scenario position, an account-wide view of any quest's state per character, **Compare with** (what one character has done that another has not, ranked by unlock value), JSON export and forget.
- **Abandoned quests**: the game keeps no list of what you dropped from the journal; Tsukimichi does. Each abandoned quest is kept with the step it had reached and when ("step 3 of 5 · 2 days ago"), with Flag, Teleport and Reveal to go back for it, an Abandoned filter, and a chat line the moment it happens so a mis-click is noticed.
- **Export**: your completed quests and your Moonlit collection as JSON or CSV for a spreadsheet or your own tools (Settings › Data › Export or `/tsuki export`; format in [docs/export-format.md](docs/export-format.md)). No content id, account or world, and your character's name only if you tick it. Local files only; nothing is uploaded.
- **Flight tab**: every flying zone with its aether current quests, attunement, the quest that blocks each, and Flag or Teleport to the giver.
- **My blues tab**: every blue unlock quest the character has left, by expansion and then zone in story order, each with what it unlocks (dungeon, trial, raid, job, allied society, flying, system and more), its status and Flag and Reveal buttons; filter by kind, Ready only or Sprout mode, copy it as a Markdown checklist, or pin an expansion to the Todo overlay. Ctrl+5 opens it when the tab shortcuts are on.
- **Unlock route**: for a job, a duty, a system, a Moonlit reward or a quest, every quest the character still needs for it, in order, with level gates and main scenario milestones on the way (Route to this under a quest, a Moonlit row's menu, or Route to unlock… on the Characters tab). Copy route copies it as Markdown; Pin all pins every step for the Todo overlay.
- **Patch of origin**: every quest knows the patch it came with ("Added in 7.5" in the detail pane); the Added in filter keeps one patch series, and the Unlocks quick view opens with a "New in 7.5x" group, the unlock quests of the newest patch series.
- **Nearby quests** (`/tsuki nearby`): the quests you can start in the current zone, with a "☾ N" count in the server info bar.
- **Todo overlay** (`/tsuki todo`): a small always-visible panel with your pins, the feature quests you can start here, the next main scenario quest, the current job's next job and role quest, and, once you pin an expansion from My blues, its Ready unlock quests under Clear my blues. Its text is outlined so it reads over bright scenes; Compact mode shows one line per quest, and Locked makes it click-through.
- **Duty Finder unlock hint**: select a padlocked duty in the Duty Finder and a small panel beside it names the quest that unlocks it, its state and what it waits for, with Reveal and Flag giver buttons. It never queues or opens a duty.
- **Item hints**: hovering an item that is a quest-exclusive reward shows which quest gives it and whether it is done; right-clicking such an item in the inventory adds "Tsukimichi: quest reward" to its context menu.
- **Chains and ladders**: Hildibrand, the relic lines, the raid stories and every job and role quest ladder with "N of M" and the next quest.
- **Notices**: an optional chat line when a pinned or feature quest becomes available, when a level-up opens the next job or role quest, and when you abandon a quest.
- **Integrations**: every quest and Moonlit reward is registered with the Wotsit search plugin; Teleport to the giver goes through the Lifestream plugin when it is installed; with Questionable loaded, the detail pane says whether its lock check agrees, and a route, chain, expansion or your pins can be sent to its priority list (and Questionable started) with a button. All are optional. Other plugins can ask Tsukimichi over an IPC API whether a quest can be picked up now, its state, what blocks it and the next main scenario quest, or open a quest in it (see [Works with other plugins](#works-with-other-plugins) and [docs/ipc.md](docs/ipc.md)).
- **Help, tour and settings** from the toolbar: a searchable help window, an interactive tour that points at each part of the window, UI and icon scale, reduce motion.

## Commands

| Command | What it does |
|---|---|
| `/tsukimichi` | open or close the main window |
| `/tsuki` | the same, shorter; every subcommand works with either |
| `/tsuki search <text>` | search and print matching quests to chat as links (`/tsuki <text>` does the same) |
| `/tsuki zone` | chat links for the quests you can start in the current zone, by level |
| `/tsuki which` | every quest the targeted NPC hands out, with its state |
| `/tsuki why [quest name]` | why the selected or named quest is not offered: its state and blocker, one line per requirement, and the curated note where the game is known to skip a step; a Ready quest says whom to talk to, with a map link |
| `/tsuki nearby` | open or close the Nearby quests window |
| `/tsuki todo` | show or hide the Todo overlay |
| `/tsuki export [quests\|moonlit] [json\|csv]` | write your completed quests, your Moonlit collection, or both to the exports folder (see [docs/export-format.md](docs/export-format.md)) |
| `/tsuki config` or `/tsuki settings` | open Settings |
| `/tsuki help` | open the help window |
| `/tsuki glyphs` | the glyph sheet: every moon at every size |

## What it hooks, and what it never does

Tsukimichi reads the game's quest sheets from your installed client and your character's own quest flags, journal, levels and standings through Dalamud. Beyond ordinary Dalamud windows it touches five places in the game UI:

- the **item tooltip**: it reads which item is hovered and draws its own small panel beside the game's tooltip (Settings › Item hints);
- the **item context menu**: it adds a "Tsukimichi: quest reward" entry (Settings › Item hints);
- the **target bar's menu on an NPC**: it adds a "Tsukimichi: quests here (N)" entry on a quest-giving NPC that opens the Journal on that NPC's quests (Settings › Integrations); it reads only the NPC's kind and id, never a player's, and stores nothing;
- the **server info bar**: a "☾ N" entry with the count of quests you can start here (Nearby quests › cog);
- the **Duty Finder**: while it is open it reads which duty is selected and whether you have unlocked it, and beside a padlocked one draws a small panel naming the quest that unlocks it, with its state and what it waits for (Settings › Integrations › "Duty Finder unlock hint"); it never queues or opens a duty.

All five are tested on a specific game version, recorded in each release. After a game patch (a new patch date; hotfixes do not count) they pause themselves until a Tsukimichi update has been tested on the new patch, so a patch that moves the game's interface cannot leave a panel or menu entry misbehaving; a chat line and a notice in Settings › Integrations say so, and the quest journal and everything else keep working. Settings › Integrations › "Enable game hooks on this untested version" runs them anyway on the patch you are on, its hotfixes included; the next patch pauses them again.

Everything else is a Dalamud window. It also talks to three other plugins over Dalamud IPC when they are present: Wotsit (to register searchable entries), Lifestream (to teleport when you click Teleport) and Questionable (to compare its lock check with Tsukimichi's, to send quests to its priority list, and to start and stop it when you click), and it answers other plugins' questions about quests (below).

Tsukimichi automates only when you press a button that hands the work to one of these plugins. On its own it does not move your character, accept or turn in quests, skip dialogue or press anything for you. Teleport to the giver is a button you click, handed to Lifestream; "Add and start Questionable" hands your questing to Questionable, after asking you first, until you stop it. Map flags, journal pages and chat links use the game's own functions.

It has no network code. Nothing leaves your machine. Snapshots and settings live in `%AppData%\XIVLauncher\pluginConfigs\Tsukimichi\` (`characters\<id>.json` with its `<id>.accepted.json`, `<id>.abandoned.json`, `<id>.dates.json` (quest completion dates) and two backups, `<id>.prev.json` refreshed once a day and `<id>.prev2.json` the one before it ([how to restore them](docs/restore-backup.md)), `<id>.live.json` while that character is logged in (see [multibox](#several-game-clients-at-once-multibox)), `user\pins.json`, `user\overrides.json`, `user\discovery.json`). Exports are files you write on purpose, to `exports\` unless you choose another folder, and Settings › Data can delete everything else.

## Several game clients at once (multibox)

If you run two or more game clients at the same time, each with its own character, every client's Tsukimichi sees the others' characters. A character logged in on another client shows as **live in another client** (a ◎ dot and badge) in the character switcher, on the Characters tab, in Compare with and in the account view, with the data that client saved last; it updates every time that client saves (every 10 seconds or so while something changes). Both clients can pin quests and mark Moonlit verdicts at the same time without losing each other's changes.

It works only through the files in the shared config folder, so both clients must use the same Dalamud config folder (the usual setup). It never reads another game process or sends it anything. [docs/multibox.md](docs/multibox.md) explains how it works and what it can't do.

## Works with other plugins

- **Wotsit**: every quest and Moonlit reward is searchable from Wotsit; picking one opens it in Tsukimichi.
- **Lifestream**: Teleport to the giver hands the teleport to Lifestream when it is installed.
- **Questionable**: when it is loaded, the detail pane says whether Questionable's own lock check agrees with Tsukimichi ("Questionable agrees", or "Questionable says: …" with its reason), whether the quest is on Questionable's list ("On Questionable's list (#3)") and whether Questionable has a path for it, and Report this quest records all of it. **Send to Questionable** (on a route, a My blues expansion card, a Characters job or chain row's right-click menu, and the Todo overlay's title menu) adds those quests to Questionable's priority list in order, leaving out what is done, in your journal or locked out, and says in chat how many it took ("Questionable: sent 14 of 17 (3 have no Questionable path)"); "Replace Questionable's list…" empties it first, after asking. "Add and start Questionable" also starts it (Settings › Integrations › "Allow Tsukimichi to start Questionable", on by default; it asks before the first start, and needs vnavmesh, TextAdvance and Lifestream, which Questionable itself requires). While Questionable runs, the status bar and the Todo overlay show what it is doing with a Stop button, and its quest is highlighted in the Journal. Settings › Integrations › "Show Questionable hand-off" (off by default) adds "Add to Questionable priority" for one quest to the detail pane's "…" menu. Without Questionable these buttons stay visible, disabled, and say it is needed. The gates used are listed in [docs/ipc.md](docs/ipc.md#consumed-ipc).
- **Your plugin**: Tsukimichi answers over Dalamud IPC whether a quest can be picked up now, its state, why it is blocked, the next main scenario quest, and "open this quest", with a message when the logged-in character's states change. The gates, their threading and a C# example are in [docs/ipc.md](docs/ipc.md); request a new one with the **IPC request** issue template.

## Languages and translations

Quest, item, NPC, place and duty names always come from the game in your client's language. Tsukimichi's own text (menus, help, the tour, tooltips) follows Dalamud's language: English, and **draft** translations into Japanese (日本語), German (Deutsch) and French (Français). Any other Dalamud language reads in English. Settings › Display › Plugin language switches between following Dalamud and English.

The three translations are drafts, made with machine assistance and not yet read through by players; Settings says so while you use one. Corrections from players of each language are very welcome: a word, a sentence or a whole help topic. [CONTRIBUTING.md › Translations](CONTRIBUTING.md#translations) explains the files and the few rules, and a pull request or an issue quoting the text and your correction both work. Report this quest and the export files stay in English on purpose (they are read by maintainers and tools).

## Verification and releases

- Every push and pull request builds the whole solution with warnings as errors and runs the test suite ([CI workflow](.github/workflows/ci.yml)).
- A release is a tag `vX.Y.Z` on this repository. GitHub Actions builds `latest.zip` from that tagged commit against the current Dalamud, attaches it to the GitHub Release and regenerates `pluginmaster.json` ([release workflow](.github/workflows/release.yml)). The workflow refuses a tag whose version differs from the plugin's or that has no [CHANGELOG](CHANGELOG.md) section.
- The plugin data (`unique_quests.json`) is generated from the game files by a tool in this repository and checked by curated-data tests on every build. The version of the game it was generated for shows in Settings › About.

## Reporting a problem

Open an [issue](https://github.com/xenofei/Tsukimichi/issues/new/choose) with the template that fits: a bug, a quest shown in the wrong state, or a data correction. For a wrong state, paste the diagnostic block the plugin copies for you (Report this quest in the detail pane); it carries the quest id, the state, every requirement's verdict and the data version, and no character identifiers.

Known quirks (quests the game skips a step on, older guides' rank names, seasonal rewards the Online Store re-sells) are listed in the help window under **Known quirks**, next to **Why my counts differ from the journal**.

## Data credits

- Quest, reward and journal data are read from your own installed copy of FINAL FANTASY XIV. FINAL FANTASY XIV © SQUARE ENIX CO., LTD. All rights reserved. FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd.
- Curated lists (duty and system unlocks, story chains, seasonal windows, store re-sells) were checked against the [Final Fantasy XIV Console Games Wiki](https://ffxiv.consolegameswiki.com/) (CC BY-NC-SA 3.0), [FFXIV Collect](https://ffxivcollect.com/) and [Garland Tools](https://www.garlandtools.org/), with ids confirmed through [xivapi](https://v2.xivapi.com/). The plugin itself never contacts any of them.

## Third-party tools and the Terms of Service

Dalamud and every plugin, this one included, are third-party tools that Square Enix does not sanction. Use them at your own discretion, do not mention them in game, and hide them in screenshots and streams.

## License

[MIT](LICENSE). Copyright (c) 2026 Michiru Tsukikage.

Building from source, the project layout and regenerating the data files are described in [CONTRIBUTING.md](CONTRIBUTING.md).
