# Changelog

All notable changes to Tsukimichi are recorded here. The format follows Keep a Changelog; versions follow the plugin's AssemblyVersion (major.minor.patch). The section for a tagged version is copied into the Dalamud manifest's changelog by `tools/make_pluginmaster.py`.

## [Unreleased]

### Added
- Presets at the top of the filter panel: Feature quests (unlock quests, the ones you can pick up now first), Around my level (current job level ±5, unsynced) and Stalled (accepted quests untouched for a number of days, default 7, slider beside the chips); the active preset shows as a toolbar chip and the empty-result guard names it.
- Accepted-since sidecar (`characters/<ContentId>.accepted.json`) recording when each quest entered the journal, kept by the poller from each diff (a step change refreshes it) and removed with the character or all data.
- Main scenario position independent of the journal's hide state: " · MSQ: <quest>" in the status bar with a tooltip naming the expansion, progress, NPC and zone (click selects the quest), and an "MSQ: <expansion> · next: <quest> (<NPC>, <zone>)" line on the Characters dashboard.
- Chat notice with a quest link and the giver's map link when a pinned or feature quest becomes available (Settings › Notices; main scenario quests only when included), one line per quest per login session.

### Changed
- The Feature Unlocks node and the Feature quests preset use a derived set (483 quests): curated system and duty unlocks, the shipped unique-reward unlock entries, and quests rewarding a duty, class or job, action, general action, trait, aether current, blue magic spell or a named other reward; main scenario and repeatable quests are excluded.

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
