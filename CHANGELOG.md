# Changelog

All notable changes to Tsukimichi are recorded here. The format follows Keep a Changelog; versions follow the plugin's AssemblyVersion (major.minor.patch). The section for a tagged version is copied into the Dalamud manifest's changelog by `tools/make_pluginmaster.py`.

## [Unreleased]

### Fixed
- Login: the first capture after logging in is no longer trusted while the game has not yet delivered the character's quest data (an all-zero completion mask and an empty journal). Before, that capture could reset every "accepted since" time, and the next poll then announced every pinned quest as newly available. Loading the plugin while already logged in now waits for the character the same way instead of retrying with a growing back-off.
- `/tsukimichi` help (the plugin installer's command list and Help › Commands) now names the `search <text>`, `settings` and `todo` subcommands, which worked but were not listed.
- Settings that cannot be read at load are no longer silently replaced: the unreadable file is copied to `Tsukimichi.corrupt-<timestamp>.json` next to it before defaults are written, and the log says where.
- Wotsit: a registration call that fails part-way through the list is retried from the same entry on the next tick instead of leaving the remaining quests and rewards unregistered until the next patch; after five failures on one entry the rest is skipped with a warning in the log.
- Settings › Delete all data now also clears what the plugin remembers about the logged-in character, so its files come back together on the next poll instead of the snapshot reappearing first and the accepted-time file only after the next journal change; forgetting the logged-in character no longer leaves an `.accepted.json` next to no snapshot.
- The account's expansion and level cap are now read from the game, so a quest above what the account owns shows that as its blocker ("requires Dawntrail", "level 100 is above your cap of 90") instead of Ready. Snapshots written by earlier versions carry no cap and are evaluated as before.
- Class quests taken on the job: a class-pinned quest (a Lancer quest, say) that your journal shows accepted on the class's job (Dragoon) now reads as available on that job instead of "Ready on Lancer"; the plugin reads which job each journal quest was accepted on and keeps it in the snapshot.
- Counts and totals: a seasonal quest whose event is not running now leaves every done/total count the way a foreclosed quest does (tree nodes, tab badges, dashboard sections, the Feature Unlocks count, Compare's "neither done"), so "Seasonal Events" and the overall total can reach 100 % between events. Completed seasonal quests still count as done; a quest blocked for any other reason still counts.

## [0.5.1] - 2026-09-28

### Added
- Settings › Display: "Reduce motion". With it on, hold-to-confirm buttons count down in text ("Hold… (0.4 s)") instead of filling an arc.
- Settings › Data: "Your Moonlit verdicts (N)" lists every quest you marked unique or hid as not unique, with the note and the date, a Restore button per row and "Restore all" (hold to confirm, or Shift and click).
- Test fixtures: a real schema-v1 character snapshot (anonymised) that must round-trip through the store unchanged, and a gzipped dump of the mapped quest catalog (`Tsukimichi.DataGen --dump-catalog`) so the tree-total, feature-quest and chain tests run without the game files; one game-data test checks the dump against the live sheets and says when to regenerate it.

### Changed
- CI: every push and pull request builds the solution with warnings as errors and runs the tests. The release workflow refuses a tag whose version differs from the plugin's, or that has no changelog section; it publishes tags with a suffix (`-rc1`) as prereleases without touching the plugin repository index, retries the index push, and can be re-run for an existing tag.
- Partial moons in the tree are visible again: a section that is only a few quests along shows a thin gold crescent instead of a dark disc, and one that is nearly done keeps a visible dark sliver until the last quest. Every state moon now sits on a slightly lighter disc with a coloured rim, so it reads at row size on dark backgrounds; Ready keeps a thin gold ring around it at small sizes where the glow used to vanish; a foreclosed quest shows a diagonal bar through its moon, so the state no longer depends on telling red from grey.
- Marking a quest as unique, and hiding one as not unique from a Moonlit row's context menu, both ask first in the same small popup: type a note (it has the keyboard already; Enter confirms), then press and hold the confirm button until the gold arc around it closes, or hold Shift and click. Releasing early cancels; Escape cancels. "Not unique (hide)" no longer applies on a single click.
- After either verdict a "Marked unique · Undo" (or "Hidden as not unique · Undo") line shows for eight seconds where you made the change; Undo forgets the verdict again.
- Quests you hid as not unique are no longer unreachable: the Moonlit "Yours only" confidence filter lists them struck through in grey, and their context menu offers "Restore shipped verdict". Every other filter keeps them hidden.
- Verdicts now remember when they were given; verdicts stored by earlier versions load unchanged and show no date.
- Help (Moonlit › Overrides and Restore) and the tour's Moonlit step describe the hold-or-Shift confirm and the ways back.

### Fixed
- Allied society dailies: accepting one daily no longer turns every other tribe's dailies Blocked with "not offered today". The game never stores the day's offer (the array the plugin read holds the dailies you have already accepted), so that check is gone; a daily you have picked up now shows Accepted instead of Ready, and one you have turned in today still shows done this cycle.
- Seasonal quests: known event end dates now reach the evaluation, so a seasonal quest of an event whose end date is known and past shows Foreclosed instead of staying Blocked forever (no end dates ship yet; they arrive with the verified data); an event with no known end still shows Blocked with "seasonal event not active" until it runs. Counts and totals are unchanged in this release.
- Switching characters without logging out in between no longer carries the previous character's Recent activity over, and no longer re-announces that character's newly available quests in chat for the new one.

## [0.5.0] - 2026-09-28

### Added
- Item hover hint: while the game's tooltip is up for an item that is a quest-exclusive reward, a small Tsukimichi panel beside it lists each quest that hands the item out with its state moon and "Quest reward: name", then "done" in gold for a completed quest or the next step otherwise; for mounts, minions, orchestrion rolls, cards and ornaments a second line says owned, not owned or veiled (stored character). The panel takes no input and stays clear of the game tooltip and the screen edges. On by default; a Settings checkbox turns it off.
- Item context menu: right-clicking such an item in the inventory, armoury, saddlebag or a retainer adds "Tsukimichi: quest reward (quest)", which opens the main window on that quest; an item several quests give shows "quest rewards (N)" with one submenu line per quest. Chat item links carry no item in the menu and get no entry. On by default; a Settings checkbox turns it off.
- Compare with on the Characters dashboard (alt diff): pick another stored character (the most recently captured one at first) and see "Done on A, not on B" and the reverse, ranked by unlock value (1 for any quest, +3 main scenario, +5 feature quest, +2 per unique reward) with a value badge and the reason ("Feature quest · 2 unique rewards"), the lacking character's state moon, and a click that reveals the quest; each list shows 25 rows and "and N more". A lead line ("A is 12 quests ahead of B"), done-on-both and done-on-neither counts, and per-section counts sit above the lists. A quest foreclosed on the other character (a Grand Company choice not taken) is not counted as missing. Copy list puts the whole list on the clipboard as "name (value)" lines. The other character is evaluated offline from its snapshot once per capture; with a single stored character the section shows "Log in on another character to compare."
- Todo overlay (`/tsuki todo`, Settings › Todo overlay): a small always-visible "☾ Tsukimichi" panel with one collapsible section per enabled part: your pins that are still to do (Ready first), the feature quests you can start in the current zone (up to eight), the next main scenario quest with its blocker, and the current job's next job and role quest when they are open. Each row shows the state moon, the quest name and a hint (next step, "Ready on PLD", journal step, or level and giver); hovering shows the state and next step, a click flags the giver on the map, and a right-click offers Reveal in Tsukimichi, Flag on map, Teleport to giver (with Lifestream) and Link in chat. Right-clicking the title locks or unlocks the panel, resets its position or hides it. The panel is hidden while logged out, in a duty or in a cutscene, and is rebuilt only when the session, the zone, the pins file or a section toggle changes.
- Settings › Todo overlay: show the overlay, lock its position (rows stay clickable), background opacity, the four section toggles and a "Reset position" button. Settings › Item hints: the hover hint and the item context-menu entry (the hooks behind them ship separately).

## [0.4.0] - 2026-09-28

### Added
- Nearby quests window (`/tsuki nearby`): the quests you can start in the current zone (Ready, plus Ready on another job unless turned off), each with its state moon, level, job and Flag and Teleport buttons (Teleport hidden without Lifestream); clicking a name shows it in the Journal. "Also accepted here (N)" folds out the accepted quests whose giver stands in the zone. The list is rebuilt only when the session or the territory changes. A cog at the top right holds the window's settings, stored in `user/discovery.json`.
- Server info bar entry "☾ N" with the count of quests you can start here; the tooltip names up to five of them and a click opens Nearby quests. Hidden at zero unless "Keep the entry visible" is on, and off entirely with "Show a count in the server info bar" unticked.
- Flight tab: every flying zone under its expansion with a filling moon of attuned currents (veiled for stored characters) and the quest currents done, the zone you stand in marked ● and selected first. The selected zone lists its quest currents from the AetherCurrentCompFlgSet and AetherCurrent sheets with attunement, quest state, next step, Flag and (with Lifestream) Teleport buttons; clicking a quest shows its requirements and path in the detail pane. Field currents are counted and pointed at the Aether Compass, never located. Since patch 6.0 the game's own sets hold five quest and four field currents per zone from Heavensward to Endwalker, five and ten in Dawntrail, and Mor Dhona's single current (The Ultimate Weapon) for A Realm Reborn; the view follows the sheet.
- Job quests on the Characters dashboard: one row per leveled job (icon, level, filling moon over its quest ladder, done/total) with the next quest, "Lv N" in gold when it can be taken now or "at Lv N" when not; click reveals it in the Journal. A job's ladder is its base class's quests, its unlock quest (Dark Knight's "Our End" and the like) and then its own quests, from the Class & Job Quests section of the sheet; role quests get one row per role the character has a job in (tank, healer, melee, physical ranged, magical ranged), with the Shadowbringers physical DPS line on both the melee and the ranged row.
- Story chains on the Characters dashboard: every curated chain (Hildibrand, the relic lines, the raid stories and the rest of `curated/chains.json`) with a filling moon, "N of M" and a clickable next quest; chains with nothing done yet fold under "Not started (N)".
- Level-up nudge: when a job's level rises and the next quest of its ladder or its role's ladder is open, a chat line "Level N Job: [quest] is available" with the giver's map link, once per quest per login session. Settings › Notices: "Chat notice when a job or role quest becomes available after a level-up" (on by default).

### Changed
- Tutorial and help cover the Flight tab and the Nearby quests window: a fifteenth tour step after Characters, a "Flight and nearby" help topic, an "Unlock flying" quick-start step, and `/tsuki nearby` in the Commands topic.

### Fixed
- The level-up nudge no longer announces a job or role quest that is already in the journal, or one that was already available before the level-up; only a quest the new level itself unlocks is named.

## [0.3.0] - 2026-09-28

### Added
- Chain progress in the detail pane: quests in a named chain (Hildibrand, the relic lines, Crystal Tower, Omega, Eden, Pandæmonium, the Arcadion, Myths of the Realm and the other Chronicles stories from `curated/chains.json`, plus every journal genre whose quests form a single line) show "Chain: name · N of M done · next: quest" with a filling moon and a clickable next quest.
- Seasonal and special quests show the journal's special icon as a badge beside the state moon in the detail header.
- Teleport to the quest giver through Lifestream: a context-menu item on table rows (hidden when Lifestream is absent, disabled while it is busy or the giver's zone has no aetheryte), backed by an aetheryte index built from the Aetheryte, MapMarker and TerritoryType sheets.
- Wotsit search: every catalog quest ("Quest: name") and every Moonlit reward ("Reward: name (kind)") is registered with Wotsit when it is loaded, re-registered when it reloads or the catalog rebuilds, and picking one reveals the quest in the Journal. Registration is spread over frames within a 4 ms budget per tick.
- Settings › Integrations: "Register quests and rewards with Wotsit" (on by default); turning it off unregisters the entries at once.
- `/tsuki zone` prints chat links for quests you can start in the current zone (Ready or Ready on another job, by level, up to ten plus "and N more"); `/tsuki which` prints every quest the targeted NPC hands out with its state.
- Moonlit confidence filter next to Hide obtained: Any, Static only, Curated only, Yours only, or only rows whose obtained state cannot be read.
- Presets at the top of the filter panel: Feature quests (unlock quests, the ones you can pick up now first), Around my level (current job level ±5, unsynced) and Stalled (accepted quests untouched for a number of days, default 7, slider beside the chips); the active preset shows as a toolbar chip and the empty-result guard names it.
- Accepted-since sidecar (`characters/<ContentId>.accepted.json`) recording when each quest entered the journal, kept by the poller from each diff (a step change refreshes it) and removed with the character or all data.
- Main scenario position independent of the journal's hide state: " · MSQ: <quest>" in the status bar with a tooltip naming the expansion, progress, NPC and zone (click selects the quest), and an "MSQ: <expansion> · next: <quest> (<NPC>, <zone>)" line on the Characters dashboard.
- Chat notice with a quest link and the giver's map link when a pinned or feature quest becomes available (Settings › Notices; main scenario quests only when included), one line per quest per login session.

### Changed
- Mount, minion, fashion accessory and job names read "Magitek Armor" and "Paladin" instead of the sheet's lower case.
- Moonlit rows without a reward icon now show one by kind: duty unlocks and instances use the duty's content-type icon, jobs their job icon, aether currents the attunement crystal, traits, achievements and blue mage spells their sheet icon.
- The Feature Unlocks node and the Feature quests preset use a derived set (1,699 quests): every quest the game draws with the blue "+" journal icon (`Quest.EventIconType` 8, which covers job quests and the Chronicles raid stories), plus curated system and duty unlocks, the shipped unique-reward unlock entries, and quests rewarding a duty, class or job, action, general action, trait, aether current, blue magic spell or a named other reward; main scenario and repeatable quests are excluded.
- Revealing a quest from another pane (Moonlit, Wotsit, chain links) also clears the active preset, so the revealed row is never hidden by it.
- `/tsukimichi` help text names the `zone` and `which` subcommands.

### Fixed
- `/tsuki which` printed every quest a prolific NPC hands out; it now stops at ten links and adds "and N more", like `/tsuki zone`.
- Teleport to the giver could pick the wrong aetheryte for city aetherytes drawn on several maps: the marker came from whichever map page the sheet listed first, so the position was converted with another map's scale and offset. The aetheryte's own map page is used now.

## [0.2.0] - 2026-09-28

### Added
- Help, Tutorial and Settings buttons on the main window toolbar.
- Interactive tutorial: fourteen steps that dim the window and highlight each region, offered on first run and restartable from the toolbar, help window, settings or glyph window.
- Help window rebuilt from cards, phase rows, numbered quick-start steps with Try-it buttons, key caps and tips, with topic search.
- UI scale and icon scale sliders (defaults 1.15× and 1.25×) in the filter panel and settings; the window title bar keeps Dalamud's size.
- Quest journal artwork in the detail pane header and in a tooltip on quest names.
- Characters tab dashboard: completion by journal section, Moonlit summary, pinned quests, recent activity, job levels grouped by role with job icons and base classes hidden once the job is unlocked.
- Reward tooltips with a large icon, item level, category and description; Copy coordinates; `/tsuki` alias; `/tsukimichi help`.
- Pinned quests sort first (toggle in the filter panel and persisted).
- Path section grouped by expansion with completed runs folded, plus an Unlocks next list.
- Mark a quest unique or restore a "not unique" mark from the detail pane.
- Poll timing readout in Settings › About.
- Tooltips on every toolbar control, filter, and column header.
- DataGen `--verify` mode that checks the reward database structure, icon files, banner artwork and cross-checks entries against xivapi.

### Changed
- Filter chips moved onto the toolbar row so the layout below never shifts; the State chip names the excluded states.
- Categories with a single genre fold into one tree leaf.
- Open journal is available only for accepted or completed quests, since the game journal has no page for others.
- Moon glyphs gained shading and a highlight arc at 20 px and larger.
- Tree rows show the filling moon before the name; table rows alternate shading and carry gold or silver stripes for ready and accepted quests; requirement marks are small moons; empty states show a veiled moon with guidance.
- Unique reward data tightened: 713 items that are not quest-exclusive (Fantasia, cordials, tickets, coffers, vendor-resold items) removed; 3,464 entries remain across 1,173 quests.

### Fixed
- Show path now scrolls to and highlights the Path section.
- Moonlit kind moons drew a full moon regardless of progress.
- Every orchestrion roll carried reward id 0 and seventeen rolls were lost to key collisions.
- Placeholder ClassJob rows 44 and 45 appeared as "Job 44" and "Job 45" in the character page.
- The Moonlit State column could not be reordered.
- Forward tab switches (Journal to Moonlit or Characters) were dropped.
- The tutorial card could fall behind the main window; Esc is now scoped to the card.
- Display sliders in Settings desynced from the filter panel and let NaN through.
- Settings changed just before closing the window could go unsaved.

## [0.1.0] - 2026-09-27

### Added
- First release: quest catalog by journal type with completion counts, completed and available-now filters with a per-requirement breakdown, Moonlit unique-rewards tab with obtained state and confidence badges, per-character snapshots with an account-wide view, prerequisite path, pins, map flag, journal open and chat links, characters page, settings and help windows.
