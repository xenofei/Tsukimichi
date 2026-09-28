# Tsukimichi V2 — Release Plan

Source: docs/feature-plan-v2.md. Each release: implement in isolated worktrees, merge, build with 0 warnings, tests green, review pass, then bump `Version`, add the CHANGELOG section, tag, push, confirm the Actions run and pluginmaster.json. The owner verifies in game after each release; no smoke-test pages.

Conventions: Dalamud API 15, `Dalamud.NET.Sdk/15.0.0`; never call [Obsolete] members; verify signatures against the dev-folder XML; ClientStructs reads only in Tsukimichi/Game on the framework thread; allocation-free per-frame UI; IPC consumers must tolerate the provider being absent.

## Release 0.3.0 — quick wins on existing data (this execution)

Stream A: Journal presets, stalled quests, MSQ position, chat notices
- A1 Feature quests preset (V2-01): identify "blue" feature quests from the sheet (verify `EventIconType` row for feature quests; confirm counts against the wiki's Feature Quests list) plus the curated feature list; preset chip "Feature quests" that scopes to them, available-now first; group header by expansion.
- A2 Accepted but stalled (V2-02): record `AcceptedSinceUtc` per accepted quest in the snapshot (schema v2, tolerant load) maintained by the poller; a preset "Stalled" listing accepted quests older than N days (config, default 7) sorted by category.
- A3 Level band preset (V2-03): chip "Around my level" = current job level ±5, using the unsynced level.
- A4 MSQ position (V2-15): status bar and dashboard line "MSQ: <expansion> · next: <quest> (<NPC>, <zone>)" computed from the MSQ genres: the first non-completed MSQ quest in journal order.
- A5 Availability chat notices (V2-05): when enabled, a clickable chat line when a pinned or feature quest becomes Ready (from `QuestEvent.NewlyAvailable`), MSQ excluded unless configured; at most one line per quest per session.

Stream B: Integrations and discovery
- B1 Lifestream teleport (V2-07): `Lifestream.Teleport(aetheryteId, subIndex)` via `GetIpcSubscriber`, gated on `Lifestream.IsBusy`; nearest aetheryte to the quest giver from the Aetheryte sheet positions in the same territory; button in the table context menu; hidden when Lifestream is not loaded.
- B2 Wotsit registration (V2-08): `FA.RegisterWithSearch` for every quest ("Tsukimichi: <name>") and Moonlit reward; invoke opens the window on that quest; unregister on dispose; re-register on catalog rebuild.
- B3 Discovery commands (V2-06): `/tsuki zone` lists quests startable in the current territory (Ready or ReadyOnOtherJob, giver territory = `IClientState.TerritoryType`) as chat links; `/tsuki which` lists quests offered by the targeted NPC (`ITargetManager.Target` data id = issuer NPC id).
- B4 Moonlit confidence filter (V2-04): a combo "Any / Static / Curated / Yours / Unknown obtained".
- B5 Moonlit kind fallback icons: duties (content-type icon via ContentFinderCondition), jobs (062100+row), aether currents, traits, titles, achievements, blue mage spells, system unlocks.

Stream C: Data model and detail pane
- C1 IconSpecial badge: add `IconSpecial` to `QuestRecord`, map it, show it as a small badge in the detail header.
- C2 Title-case sheet names for mounts, minions, ornaments and jobs at catalog load.
- C3 Chain progress (V2-09): derive chains from genres whose quests form a single PreviousQuest line; curated `chains.json` merges genres into named chains (Hildibrand across expansions, relic steps per expansion, alliance and normal raid stories); detail pane widget "Chain: <name> · N of M done · next: <quest>"; tests for the linearity check.

Post-merge (coordinator): teleport button in the detail pane giver section; review pass; release 0.3.0.

## Release 0.4.0 — differentiators
- Flight view per zone (V2-10), per-job quest ladder with level-up nudge (V2-11), chain widget on the dashboard, `/tsuki` discovery in a window.

## Release 0.5.0
- Alt diff (V2-12), todo overlay panel (V2-13), item tooltip and context-menu injection (V2-14).

## Later
- IPC provider (V2-16), Questionable cross-check (V2-17), seasonal calendar (V2-18), localization (V2-19), official repository submission (V2-20).
