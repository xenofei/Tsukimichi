# Tsukimichi — Feature Panel

Date: 2026-09-27
Status: pre-design inventory. Nothing here is built. This document feeds the design spec.
Companion: [feasibility-report.md](feasibility-report.md)

## How to read this

Every feature has an ID, a source, a tier, and a status.

Source codes:
- **REQ** — originally requested by the owner.
- **SUG** — suggested in the feasibility round.
- **FLOW** — surfaced by the user-flow gap analysis (edge cases the requested features need to work).
- **USR** — demonstrated user demand in issue trackers of comparable plugins (Quest Map, QuestTracker, QuestJournal, Collections, DailyDuty, Questionable).
- **CMP** — table stakes; every credible competitor has it.
- **WS** — whitespace; no competitor has it.

Tiers:
- **V1** — must ship in the first release.
- **V1.5** — first follow-up; small, builds on V1 data.
- **V2** — larger additions after V1 is stable.
- **Later** — opt-in or speculative.
- **Delegate** — not building; link or IPC to a plugin that already does it well.

Status: **Verified** (data and API confirmed), **Curated** (needs a hand-maintained overlay), **Decision** (needs an owner decision, listed in §12).

## 1. Original requirements, restated with refinements

| ID | Requirement as stated | Refinement from research | Status |
|---|---|---|---|
| F-01 | Track ALL the character's quests, separated by quest type | Catalog built from the game's `Quest` sheet at load (5,373 named quests), grouped by the real journal hierarchy Section → Category → Genre. Never a hand-maintained quest list; never matched by name. | Verified |
| F-02 | Category of quests with unique rewards/opportunities only obtainable through the quest | Reward-first view. About 75% classified automatically from sheets; system unlocks and most dungeon unlocks need a curated overlay with a confidence badge. | Verified + Curated |
| F-03 | Every category can hide/show completed quests | Global toggle with per-category override remembered in config. "Completed" means permanently completed or foreclosed; repeatables are never "completed". | Verified |
| F-04 | Every category can hide/show quests that can be taken or done now | Same toggle model. "Available now" is computed by the plugin against an eight-state model (§2), not a single game flag. | Verified |
| F-05 | Detailed requirements per quest, highlighting what still needs to be done | Detail panel lists every requirement with pass/fail and the exact gap. Table shows the first failing requirement as a "Next step" column. | Verified |
| F-06 | Local database the plugin refers to | Two stores: shipped read-only data (catalog overlays, unique-rewards file) and user data (per-character snapshots, pins, filter memory) in the plugin config directory. JSON first; SQLite only if queries demand it. | Verified, Decision D-3 |

## 2. Cross-cutting decision: the quest state model

Both requested toggles (F-03, F-04) are views over one per-quest, per-character state. Adopting this early drives the evaluator, filters, snapshots and the account-wide view.

| State | Meaning | "Hide completed" hides it | "Available now" keeps it |
|---|---|---|---|
| Ready | All requirements met on the current job | No | Yes |
| ReadyOnOtherJob | Met on a job you have leveled but are not on | No | Yes |
| Accepted | In progress; show current step | No | Yes |
| Blocked | One or more requirements unmet; reasons listed | No | No |
| DoneThisCycle | Repeatable or daily already done; resets at a known time | No | No |
| Completed | Permanently completed | Yes | No |
| Foreclosed | Locked out by a past choice (QuestLock) or a missed seasonal event | Yes | No |
| Unknown | Gated by achievements not yet loaded by the client | No | No |

Power users get the full set as checkboxes in an advanced popover. Source: FLOW.

## 3. Catalog and browsing

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-10 | Journal-faithful tree: Section → Category → Genre, collapsible, with completed/total and % on every node (pattern: Questionable's journal window) | REQ, CMP | V1 | Verified |
| F-11 | Sortable table with ImGui list clipper; default sort is journal order (`SortKey`), secondary by level, name, state | CMP, FLOW | V1 | Verified |
| F-12 | Text search on quest name and on reward names (items, emotes, mounts, instances, tribes, jobs), debounced, ID-keyed | CMP, USR | V1 | Verified |
| F-13 | Duplicate-name disambiguation: rows and search results show expansion, genre and ID when names collide | USR, FLOW | V1 | Verified |
| F-14 | "Unlisted" bucket for the 180 named quests with no journal genre; excluded from progress denominators by default | FLOW | V1 | Verified |
| F-15 | Feature-unlock quests as a top-level virtual category, separate from sidequests (users of QuestTracker and QuestJournal asked for exactly this) | USR | V1.5 | Curated |
| F-16 | Group or filter by expansion and by patch of origin; patch badge on rows; "what's new since patch X" | USR | V2 | Curated (patch data from community sources) |
| F-17 | Start-city, Grand Company and class variants of the same quest collapsed so counts are not inflated | CMP | V1 | Verified (QuestLock data) |
| F-18 | Quest icons and reward icons via the texture provider, fetched per frame | CMP | V1 | Verified |

## 4. Filters

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-20 | Hide/show completed, global with per-category override | REQ | V1 | Verified |
| F-21 | Hide/show available now, global with per-category override | REQ | V1 | Verified |
| F-22 | Always-visible filter panel showing current state at a glance; radio/checkbox groups, not stacked "only" toggles (Quest Map issue #15) | USR | V1 | Verified |
| F-23 | Empty-result guard: when filters hide everything, say why and offer a one-click reset | USR | V1 | Verified |
| F-24 | Additional filters: expansion, level range, class/job category, repeatable, seasonal-active, reward kind, state (advanced) | SUG | V1 | Verified |
| F-25 | "Unknown / unclassified" is a visible bucket, never silently dropped (Collections issue #77) | USR | V1 | Verified |
| F-26 | Free-trial / entitlement filter: mark quests above the client's expansion or level cap as blocked | FLOW | V1.5 | Decision D-6 |

## 5. Requirements and availability

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-30 | Requirement evaluator covering level, level cap, class/job category, specific class, previous quests with ALL/ANY joins, mutually exclusive quests, duty completion, Grand Company and rank, tribe rank and reputation value, expansion, seasonal window, extra accept conditions, mount/house requirements | REQ, WS | V1 | Verified |
| F-31 | Detail panel: every requirement with ✓/✗ and the gap ("Trusted, needs Sworn", "2 of 3 prerequisites done") | REQ, WS | V1 | Verified |
| F-32 | Table column "Next step": first failing requirement in a fixed priority order | FLOW | V1 | Verified |
| F-33 | Tribe dailies require rank, reputation, allowance remaining and presence in today's offered set; each shown as its own line | FLOW | V1 | Verified |
| F-34 | Level checks always use true (unsynced) job levels; level-change events ignored while bound by duty | FLOW | V1 | Verified |
| F-35 | Unlock path: recursive prerequisite chain to a target quest, shortest branch through ANY joins, count remaining, done ones ticked | SUG | V1.5 | Verified |
| F-36 | Hidden prerequisites beyond the sheet (zone access, aetheryte) from a curated overlay, seeded from Questionable's findings | USR | V2 | Curated |
| F-37 | Foreclosed detection: QuestLock choices already made, and seasonal quests whose window has passed | FLOW | V1 | Verified |
| F-38 | Achievement-gated requirements show Unknown with a hint to open the Achievements window once | FLOW | V1.5 | Verified |

## 6. Rewards and the unique-rewards category

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-40 | Reward listing per quest: exp, gil, items, optional items, emotes, actions, general actions, instances, job unlocks, tribe reputation, tomestones, artifact gear | REQ, CMP | V1 | Verified |
| F-41 | Unique-rewards view grouped by reward kind: emote, mount, minion, orchestrion, card, hairstyle, ornament, barding, action/trait, job unlock, aether current, blue mage spell, artifact gear, achievement/title, duty unlock, system unlock | REQ, WS | V1 | Verified + Curated |
| F-42 | Obtained state on rewards (emotes, mounts, minions, orchestrion, ornaments, actions readable live; titles need achievements; gear shows n/a) | SUG, CMP | V1 | Verified |
| F-43 | Confidence badge per unique reward: static / community / curated, with the source shown on hover | SUG | V1 | Verified |
| F-44 | Offline generator producing `unique_quests.json` with game version stamp; rows without data show "reward data pending" after a patch | FLOW | V1 | Verified |
| F-45 | Curated overlay file in the repo for system unlocks (retainers, flying, Gold Saucer, dresser, Bozja, Island Sanctuary, etc.) and script-driven dungeon unlocks | SUG | V1 | Curated |
| F-46 | Reward-kind filter with three states (hidden / show / only), copied from Quest Map | CMP | V1 | Verified |
| F-47 | Mog Station awareness: flag seasonal rewards later sold on the store so "truly unique" is honest | SUG | V2 | Curated |
| F-48 | User overrides: mark a quest as unique or not unique locally; stored separately from shipped data | FLOW | V2 | Verified |
| F-49 | Exclusivity signal from sheets: untradable, no market category, absent from shop/recipe/gathering sheets; community drop data subtracted | SUG | V1 | Verified + Curated |

## 7. Characters and data

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-50 | Per-character snapshot keyed by ContentId: completion bits, accepted quests and steps, all job levels, GC and rank, tribe ranks and reputation, unlock flags, timestamp, schema version | SUG, FLOW, WS | V1 | Verified |
| F-51 | Snapshot written at login (after first territory change), on state diff, and at logout; atomic file writes | FLOW | V1 | Verified |
| F-52 | Already-logged-in bootstrap: treat plugin load while in the world as a login | FLOW | V1 | Verified |
| F-53 | Browse mode when logged out or with no snapshot: catalog visible, runtime columns greyed, stale banner with character name and time | FLOW | V1 | Verified |
| F-54 | Character picker to view any snapshotted character; names and worlds refreshed each login | FLOW | V1 | Verified |
| F-55 | Account-wide view: which characters still have a quest available or a reward unobtained; evaluated offline from snapshot inputs | SUG, WS | V1.5 | Verified |
| F-56 | Character comparison side by side | SUG | V2 | Verified |
| F-57 | Forget this character; delete all plugin data; retention documented | FLOW | V1 | Verified |
| F-58 | Snapshot export and import as JSON | SUG | V1.5 | Verified |
| F-59 | Shipped data and user data in separate files with schema-version migration | FLOW | V1 | Verified |
| F-60 | Detection provenance: show why a quest is marked complete (source and time) so users trust it | USR | V1.5 | Verified |
| F-61 | Strict per-character isolation; no shared state across characters or devices | USR | V1 | Verified |

## 8. Live updates, progress and notifications

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-70 | One-per-second diff of completion bitmask, accepted list and daily state on the framework thread; incremental re-evaluation via a reverse-prerequisite index | FLOW | V1 | Verified |
| F-71 | "Newly available" feed: quests that opened after a turn-in or level-up; session-scoped, capped, dismissable, suppressed on the baseline pass | SUG, USR | V1.5 | Verified |
| F-72 | Optional chat notice for newly unlocked quests, MSQ excluded by default, clickable names (Quest Map issue #2) | USR | V1.5 | Verified |
| F-73 | Progress dashboard: completion % per category, expansion and character; MSQ position; tribe rank bars; role quest status | SUG, CMP | V1.5 | Verified |
| F-74 | Pinned quests list, separate from the game's own tracker | SUG | V1.5 | Verified |
| F-75 | Repeatable and weekly quest tracking with reset time (QuestJournal users asked; DailyDuty refuses) | USR | V2 | Verified |
| F-76 | Server-info-bar entry with available-quest count, click to open | SUG | V2 | Verified |
| F-77 | Stats export as image or CSV | USR | Later | Verified |

## 9. Links and integrations

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-80 | Map flag on the quest giver and open in the in-game journal | CMP | V1 | Verified |
| F-81 | Chat command `/tsukimichi <name or id>` printing a quest link and map link; all matches printed when ambiguous | SUG, FLOW | V1.5 | Verified |
| F-82 | External links per quest: Garland Tools, Console Games Wiki, Teamcraft, built from IDs and validated per category | USR | V1.5 | Verified |
| F-83 | IPC provider: ShowQuest(id), GetState(id), so other plugins can deep-link | USR | V2 | Verified |
| F-84 | Register quests with Wotsit's search so Ctrl+T finds them | WS | V2 | Verified |
| F-85 | Call Quest Map's IPC for the prerequisite graph instead of drawing one | CMP | V1.5 | Verified |
| F-86 | Teleport to the nearest aetheryte only when unambiguous, via Lifestream or Teleporter IPC | USR | Later | Decision D-7 |
| F-87 | Hand a quest to Questionable via IPC, opt-in | SUG | Later | Unverified IPC |

## 10. Time-gated content

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-90 | Seasonal panel: active festival quests read from the client's active-festival list | SUG | V1.5 | Verified |
| F-91 | Countdown to event end from a curated `festivals.json`; degrades to active/inactive without it | FLOW | V2 | Curated |
| F-92 | Missed seasonal quests shown as Foreclosed, not Blocked | FLOW | V1 | Verified |
| F-93 | Daily and weekly allowances view (tribe, leve, custom deliveries), kept light | SUG | V2 | Verified; overlaps DailyDuty |

## 11. Platform quality

| ID | Feature | Source | Tier | Status |
|---|---|---|---|---|
| F-100 | Core logic in a Dalamud-free class library with xUnit tests; data-driven tests against the local game files | SUG | V1 | Verified |
| F-101 | Catalog built off-thread with cancellation; loading and error states in the window | FLOW | V1 | Verified |
| F-102 | Rendering budget: pre-filtered flat array recomputed on dirty flags only, list clipper, no per-frame string conversion | FLOW, USR | V1 | Verified |
| F-103 | Localization: quest and reward names from game data in EN/JA/DE/FR keyed by ID; UI strings in a resource file; layouts tested with long FR/DE strings | USR | V1 | Verified |
| F-104 | Catalog rebuild on client language change | FLOW | V1.5 | Verified |
| F-105 | Clean dispose: unsubscribe every event, cancel background work, flush writes; survives dev hot-reload | FLOW | V1 | Verified |
| F-106 | Version stamps: game version in shipped data, schema version in user data, unknown quest IDs preserved across patches | FLOW | V1 | Verified |
| F-107 | Few, clear settings; no styling options (DailyDuty issue #219 lesson) | USR | V1 | Verified |
| F-108 | Official Dalamud repo readiness: open source, lock file committed, deterministic version, no account IDs, no automation | CMP | V1 | Verified |

## 12. Not building (delegate)

| Instead of | Use | Why |
|---|---|---|
| Prerequisite graph visualisation | Quest Map IPC | Already excellent; we show a linear chain |
| Daily/weekly reminders and timers | DailyDuty | Mature, localized, explicitly its niche |
| Full collectible catalogs (glamour, mounts, minions) | Collections, Collector's Anxiety | We only flag quest-exclusive rewards and deep-link |
| Tooltip "already owned" indicator | Good Memory | Different surface |
| Zone completion overlays | Tourist, Explorer's Codex | Different scope |
| Quest automation and pathing | Questionable | Automation-adjacent features are out of scope |
| Web sync of quest completion | Nothing | Lodestone quest history is owner-private; local snapshots only |

## 13. Open decisions for the spec

| ID | Decision | Recommendation |
|---|---|---|
| D-1 | Adopt the eight-state model in §2 as the basis for both filters | Yes |
| D-2 | V1 scope: everything tagged V1 above, roughly 45 items | Yes, then V1.5 as one follow-up release |
| D-3 | Storage: JSON per character vs SQLite | JSON per character for V1; SQLite only if account-wide queries get slow |
| D-4 | Unique-rewards curated overlay: who maintains it and in what format | JSON in the repo, keyed by quest ID, with a contribution guide |
| D-5 | Feature-unlock category (F-15): curated list vs heuristic from reward fields | Start heuristic (system rewards, other rewards, instance unlocks), refine with curated list |
| D-6 | Entitlement filter (F-26): read expansion/level cap from the client | Include if the client field is readable; otherwise skip |
| D-7 | Teleport integration (F-86) | Later; only when a single aetheryte is unambiguous |
| D-8 | Localization pipeline: resource files only vs Crowdin | Resource files for V1 |
| D-9 | Plugin distribution: official repo from the start vs personal custom repo first | Custom repo first for iteration speed, submit to testing track after V1.5 |

## 14. Sources

- Feasibility report and local game-data probe (this repo, docs/feasibility-report.md).
- User-flow gap analysis (spec-flow-analyzer agent, 2026-09-27).
- Issue-tracker mining: GemPlugins/QuestMap, isaiahcat/QuestTracker, QuackieMackie/QuestJournal, Seventhxiv/Collections, foophoof/CollectorsAnxiety, MidoriKami/DailyDuty, PunishXIV/Questionable (issue-intelligence-analyst agent, 2026-09-27).
- Competitor feature inventory covering the plugins above plus Wotsit, Good Memory, Tourist, Explorer's Codex, Garland Tools, FFXIV Collect, Console Games Wiki, Lodestone, Teamcraft, XIV ToDo and the in-game journal (best-practices-researcher agent, 2026-09-27).
