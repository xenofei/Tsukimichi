# Tsukimichi — V2 Feature Plan

Date: 2026-09-28
Status: proposal for the owner's review. Nothing here is scheduled until approved.
Inputs: the owner's second feedback round (toolbar access, engagement, tutorial, scale, banners, data verification), player research across the official forums, Steam discussions, wikis and plugin issue trackers (docs/research is summarised below), the competitor feature matrix in docs/feature-panel.md, and the data verification report in docs/data/verification-report.md.

## 1. What is being delivered now (in progress, not part of this plan)

These were requested in the same message and are being built while this plan is written:
- Help, Tutorial and Settings buttons on the main window toolbar.
- UI scale and icon scale sliders (defaults raised so glyphs, icons and text read larger).
- Quest banner images in the detail pane header and in table tooltips, using the journal artwork the game ships for each quest.
- Engagement pass: filling moons in tree rows, state stripes on table rows, moon-shaped requirement marks, empty states with guidance.
- Help window rebuilt from cards, phase rows, numbered steps and tips, with a topic search.
- Interactive tutorial overlay: fourteen steps that dim the window and highlight each region with a step card, offered on first run and restartable from the toolbar, help, or settings.
- Item and reward database verification with tightened exclusivity rules. Done: a `--verify` mode in DataGen checks structure, icon file existence, banner artwork, and 197 cross-checks against xivapi (all pass). It found and fixed a shipped bug (every orchestrion roll carried reward id 0 and 17 rolls were lost), dropped 713 items that are not actually quest-exclusive (Fantasia, cordials, tickets, coffers, vendor-resold items), and left 3,464 entries across 1,173 quests. Full report in docs/data/verification-report.md.

Two follow-ups the verification surfaced, small and worth doing in 0.2:
- Moonlit rows for duty unlocks, jobs, aether currents, traits, titles, achievements, blue mage spells and system unlocks have no icon source; give each kind a fallback icon (content-type icon for duties, job icon for jobs, the aether current icon, the title/achievement icons).
- Mount, minion, ornament and job names come from the sheet in lowercase ("magitek armor"); title-case them at load.

## 2. What players are asking for (ranked by how often it came up)

| # | Grievance | Evidence | What Tsukimichi could do |
|---|---|---|---|
| 1 | "The aether current list says I'm missing one and I can't tell which quest." | Six or more separate forum threads plus Steam | A per-zone Flight view: the five quest currents with done / accepted / blocked-by state and a map flag, clearly separated from field currents |
| 2 | No in-game list of unlock ("blue") quests not yet done | Forum and Steam threads; players fall back to wiki tables and per-patch articles | A Feature quests preset, available-now first, with a "new in patch 7.x" grouping |
| 3 | Journal limits: no unaccepted quests, no reward search, five-quest tracking cap, accepted quests get buried | Wiki scope page; several forum threads | An "accepted but stalled" list; a level-band filter |
| 4 | Job and role quest tracking for alt jobs after MSQ | Forum threads; HaselTweaks ships a partial workaround | A per-job ladder: next job or role quest at level N with NPC and zone, and a level-up nudge |
| 5 | Long chain gating (Hildibrand to Manderville relics, Crystal Tower to Shadowbringers) | Forum thread; press coverage of the 57-quest chain | Chain progress on any quest in a named chain: N of M done, next quest, flag |
| 6 | "Which quest gives X?" and collection ownership context | Migration to FFXIV Collect and Lalachievements | Moonlit already covers this; surface it in item tooltips and the context menu |
| 7 | Alts repeating unlocks | Forum thread; players keep spreadsheets | A "diff versus main" view over existing snapshots |
| 8 | Missing seasonal quests | Forum thread; seasonal achievements are permanently lost | Active-event surfacing with an "ends soon" badge where dates are known |
| 9 | New players losing the MSQ under the five-quest display | Forum thread; web MSQ trackers | An MSQ position line that ignores the journal's hide state |
| 10 | Allied society allowance grind | Forum thread | Show rank and unlock state on society rows; leave timers to DailyDuty |

## 3. Proposed features

Priority tiers: **P1** next release, **P2** the one after, **P3** later. Effort: S (a day), M (a few days), L (a week or more of agent work plus in-game testing).

### P1: Quick wins on existing data

| ID | Feature | Effort | Why |
|---|---|---|---|
| V2-01 | Feature quests preset: one click shows unlock quests, available-now first, grouped by patch of origin where known | S | Grievance 2 |
| V2-02 | Accepted but stalled: accepted quests untouched for N days, sorted by category, with a flag button | S | Grievance 3 |
| V2-03 | Level-band filter (current level ±5) as a chip preset | S | Grievance 3 |
| V2-04 | Confidence as a filter on Moonlit ("Unknown only", "Static only") | S | Collections users asked for exactly this |
| V2-05 | Chat notice with a clickable link when a pinned or feature quest becomes available | S | Quest Map issue #2, still open; DailyDuty pattern |
| V2-06 | Discovery commands: `/tsuki zone` lists quests startable in the current zone; `/tsuki which` lists quests for the targeted NPC | M | Mirrors the two Questionable commands players praise, with no automation |
| V2-07 | Teleport to quest giver through Lifestream IPC, gated on `Lifestream.IsBusy`, button hidden when Lifestream is absent | S | Most-requested integration |
| V2-08 | Wotsit registration of quests and Moonlit rewards through `FA.RegisterWithSearch` | S | Cheap discoverability |
| V2-09 | Chain progress widget: for quests in a named side-story chain (Hildibrand, relic steps, Crystal Tower, Omega, Eden and so on), show N of M done and the next quest | M | Grievance 5; chains are curated lists of genre ids |

### P2: Bigger, differentiating

| ID | Feature | Effort | Why |
|---|---|---|---|
| V2-10 | Flight view: per zone, the quest currents from the AetherCurrent sheet with state, blocked-by path and flag; field currents pointed to Aether Compass | M | Grievance 1, the single most repeated complaint |
| V2-11 | Per-job quest ladder for every job with a level-up nudge | M | Grievance 4 |
| V2-12 | Alt diff: quests done on character A and not B, sorted by unlock value | M | Grievance 7; snapshots already hold the data |
| V2-13 | Todo overlay panel (DailyDuty style): pins and available feature quests for the current zone, hover tooltip, click to flag | L | Praised plugin pattern |
| V2-14 | Item tooltip and context-menu injection: "obtained through quest X, done/not done" on Moonlit items via IContextMenu | M | Good Memory pattern; grievance 6 |
| V2-15 | MSQ position line in the status bar and dashboard, independent of journal hide state | S | Grievance 9 |

### P3: Platform

| ID | Feature | Effort | Why |
|---|---|---|---|
| V2-16 | Tsukimichi IPC provider: `IsQuestAvailable`, `GetBlockers`, `OpenQuest` | M | Nothing else exposes availability; lets overlays build on it |
| V2-17 | Questionable cross-check: read `IsQuestLockedReason` to validate the requirement evaluator; opt-in "add to Questionable priority" button | M | Correctness signal; hand-off stays behind a button |
| V2-18 | Seasonal calendar overlay shipped as static data with an "ends soon" badge | M | Grievance 8; no web polling from the plugin |
| V2-19 | Localization of UI strings (resource files) and long-string layout tests | M | Non-English clients |
| V2-20 | Official Dalamud repository submission on the testing track | M | Distribution |

## 4. Things to avoid

- Any auto-accept, dialog or cutscene skip, auto-walk or auto-teleport on open. The official repository rejects these outright, and they draw complaints.
- Re-implementing DailyDuty timers, Tourist vistas, BlueMageHelper spell sources or Aether Radar markers. Point at the specialist plugin.
- Polling web APIs from inside the plugin for event dates. Ship static data or leave it opt-in.
- Hand-maintained quest lists. Every QuestTracker bug is a missing patch quest; Tsukimichi's catalog comes from the game sheets and the data version shows in the help window.

## 5. Suggested order

Release 0.2: V2-01, V2-02, V2-03, V2-04, V2-05, V2-07, V2-08, V2-15 (all small, all on existing data).
Release 0.3: V2-06, V2-09, V2-10, V2-11.
Release 0.4: V2-12, V2-13, V2-14.
Then P3 in whatever order the owner prefers.

## 6. Open questions for the owner

1. Which chains to curate first for V2-09 (Hildibrand, all relic steps, alliance raid stories, normal raid stories)?
2. Should the Flight view live as a fourth tab or as a filter preset inside Journal?
3. For the Todo overlay, one panel or several like DailyDuty?
4. Any appetite for the official repository in 0.3, which requires a license file and a public testing period?
