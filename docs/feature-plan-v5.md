# Tsukimichi feature plan v5: right answers, getting there, in the game

Status: **approved by the owner on 2026-10-01** ("Start executing when ready"). The settled decisions are under [Decisions](#decisions-settled-2026-10-01); they override anything else in this plan that disagrees.

Sources: 20 research passes. Ten looked at what to improve: onboarding, quest data, visual polish, performance, collectors, planning, alts and multibox, the game's own windows, player sentiment, and code health. Ten looked at other plugins and tools: Questionable, travel, navigation and maps, inventory, collections, chat and UI tweaks, automation, daily routines, web tools, and a survey of the wider plugin ecosystem. Summaries of every report are in `docs/research/plan-v5/` (R1–R10, C1–C10); the tags in brackets below point to them.

Standing rules carry over from v4:
- Only player value counts.
- Localization is frozen: new text is English only.
- No Square Enix art fetched from the web.
- The plugin never moves the character or sends input on its own; anything like that sits behind an explicit button.
- Every release goes through the usual gates.

## What the research found

### Confirmed bugs (I checked each one in the code)

| # | Bug | What the player sees | Source |
|---|---|---|---|
| B1 | Accept conditions are listed but never checked (`RequirementEvaluator.cs:244`) | **48 quests read Ready before their main scenario gate**: The Killing Art, the Studium Culture line, the Dawntrail allied society quests, the Tataru line and others | R2 |
| B2 | Allied society rank is stored without masking the "ranked up today" bit (`GameStateReader.cs:241`) | On a rank-up day, rank reads 128+, so higher-rank quests read Ready, and the wrong rank is saved to the snapshot | C8 |
| B3 | Today's allied society offer is never filled in (`EvalContext.TodaysDailyOffer` is always null) | Every eligible allied daily reads Ready, which inflates Nearby, the "☾ N" count and the IPC answers | C8 |
| B4 | The runtime catalog still files crystals, Cordials and society currencies as Artifact gear (`CatalogMapper.cs:402`; the 1.4.1 fix only covered Moonlit) | The reward tooltip and the "Artifact gear" filter are wrong on 78 quests | R2 |
| B5 | Questionable's lock reasons are translated into Japanese and Chinese; we only match the English "Low level" | False "Questionable says…" disagreements for anyone running Questionable in ja, zh-cn or zh-tw | C1 |
| B6 | Pin all promises route order, but pins are a set, re-sorted by state, level and name, with no limit on rows (`TodoList.cs:252`) | A 60-step route becomes 60 overlay rows in the wrong order | R6 |
| B7 | Wotsit keeps only the first 26 matches per plugin, in the order they were registered, and sorts afterwards | Late-expansion quests and every Moonlit reward (registered last) may never show in Wotsit | C6 |
| B8 | Seven icon lookups throw when a game icon is missing; the one in the reward tooltip throws on every frame | The window can break while you hover a reward (no icon is missing today, but a patch can change that) | R3, R4, v4 follow-up |
| B9 | One `Session.Changed` listener that throws stops all the rest; `Dispose` stops at the first exception | The todo list, IPC or chat stop silently; a reload can leak hooks | R4 |
| B10 | Tooltips never wrap | 300–500-character hints run off the screen | R3 |
| B11 | `docs/export-format.md` says FFXIV Collect and XIV Shinies can read the export | Neither can. Collect's import **replaces** whole lists, so a quest-rewards-only file would wipe a player's other marks | C5 |

### Reported but not yet verified in game

- About 36 more quests are gated behind a main scenario milestone that neither the game sheet nor the accept conditions record. Six allowlisted quests have no previous quest at all and read Ready as soon as you reach their level (The Hero's Journey, Shadow Walk with Me, …) [R2].
- Allied society rank-up quests ("I Heard You Like Tanks") only check the rank, never "reputation maxed" [R2].
- Twelve quests use the repeat flag (6 weeklies and the Gift of Joy dailies), and they never read Done [C8].
- Moonlit counts path alternatives: 206 rows repeat another row's (kind, reward), and about 47 can never be obtained, so 100% is impossible. Rewards that are Locked out for good also stay in the totals [R2, R5].
- Stored alts show every collectible as "?", because ownership is only read from the live character [R5, R7].
- Patch-day risk:
  - We compile against Lumina.Excel 7.5.0 while Dalamud ships 7.5.1. If `Unknown12` (the hidden flag) is renamed, the plugin fails to load in game while CI stays green [R10].
  - The hook gate pauses five features on every hotfix, because it needs an exact version match [R4].
  - The 25 allowlisted prerequisites expire at 1.5.0, and nothing enforces that [R10].
- Frame cost: every job change re-evaluates all 5,373 quests on the framework thread, 14–17 ms. Loading the catalog costs one frame of 30–110 ms [R4].

### Other plugins and tools: what's worth connecting

| Plugin | Status | What it gives the player |
|---|---|---|
| **Questionable** | Integrated (lock check, add one quest) | **Send a whole route, chain or expansion's blues to its priority list**; "on Questionable's list (#n)" and "has a path" badges; live "Questionable is doing X" status; wider cross-check (unobtainable, active events) |
| **Lifestream** | Integrated (Teleport) | Teleport that knows attunement; aethernet hop to the shard nearest the giver; Firmament, Island, Occult and Cosmic givers |
| **Teleporter** | — | Not used (decision 2: Lifestream only) |
| **vnavmesh** | — | "Walk to giver" after a teleport, with Stop. This moves the character, so it needs your decision |
| **Quest Map** | — | "Open in Quest Map" for the full requirement graph (its only gates take Quest row ids) |
| **Chat 2** | — | Right-click any quest or item link in chat → "Open in Tsukimichi" |
| **Allagan Tools** | — | Moonlit gear owned across bags, retainers, armoire and dresser; counts of items you need to hand in |
| **GatherBuddy** | — | "Gather" on gatherer quest items (`/gather` flags the node and teleports) |
| **QoL Bar** | — | One-click starter bar |
| **Wotsit** | Integrated | Fix B7 and add command entries |
| Web: Lodestone, Garland Tools, Console Games Wiki, Teamcraft, FFXIV Collect | — | "Open on…" links (the browser opens them; the plugin makes no request); a correct FFXIV Collect import file; Teamcraft list link |
| **Our own IPC** | v1 | New read-only gates other plugins asked for: feature detection, bulk states, Ready lists, quests by zone or item, Moonlit status, route, a structured blocker, per-quest change events, a Disposing message |

No integration possible: DailyDuty, Simple Tweaks, HaselTweaks, Mappy and Collections have no IPC. Instead, HaselTweaks and Simple Tweaks overlaps go into Help › Known quirks. HaselTweaks independently confirms our five flight-current fixes.

**Not recommended:**
- Splatoon and Pictomancy: fragile reflection, and Pictomancy is AGPL.
- Native map markers: the shared array wipes other plugins' markers.
- ~~AutoDuty Run and Artisan CraftItem~~: approved after all (decision 1), as explicit hand-off buttons in 1.6.0.
- TextAdvance and YesAlready toggles.
- Lodestone or Collect fetches from inside the plugin.
- A hotfix data channel: it breaks "no network code", and a tagged release is just as fast.
- Lalachievements: no API.

## Proposed releases

Each release ships on its own with the usual gates. Effort: S = hours, M = a day or two, L = more.

### 1.4.2 · Correctness patch (all S, low risk)

- **B1** Check accept conditions that name a quest. Non-quest values stay "not checked". Fix the verifier so a condition the evaluator doesn't actually check stops counting as a "match".
- **B2** Mask the rank-up bit; keep it as a "ranked up today" flag; repair saved snapshots on load.
- **B4** Move the equipment-only reward rule into one classifier in GameData, shared by DataGen and the catalog; add a fixture test.
- **B5** Make the Questionable level match work in any language; subscribe to `Questionable.ReloadData` so path answers stay fresh.
- **B6** Pins keep their insertion order; the overlay's Pinned section gets a "+N more" cap.
- **B7** Wotsit: search text is the name only; register Ready and In journal quests first, then rewards, then the rest.
- **B8–B10** Safe, high-resolution icon helper at the seven sites; wrapping tooltips; PathChart casing; isolated listeners; `Dispose` that unwinds step by step; the first-build error logged.
- **B11** Correct the export doc and README claims.
- **Patch-day safety:** LoadCheck checks every Lumina member GameData uses against Dalamud's Lumina.Excel. Tag the two wall-clock save tests as Perf and release test readers on a dedicated thread (the likely flaky test).

### 1.5.0 · Honest gates, honest collections (data)

- **Gates**
  - `curated/extra_prerequisites.json` (two sources per entry: wiki plus Questionable ids or the game's text) covers the ~36 hidden main scenario gates and resolves the 25 allowlisted prerequisites before they expire.
  - Rank-up quests check "reputation maxed".
  - New `Tsukimichi.Verify questionable` cross-check gate.
  - CI fails when an allowlist entry's `until` has passed [R2, R10].
- **Repeatables**
  - Today's allied society offer (B3), computed by our own MIT code from the daily seed and rank.
  - Repeat-flag quests read Done today and Done this week.
  - Stored alts reset at the 15:00 UTC daily and Tuesday 08:00 UTC weekly reset.
  - The Done tooltip shows "resets in 3 h" [C8].
- **Moonlit**
  - Other-path rows are hidden and a (kind, reward) pair counts once.
  - "Artifact gear" is renamed **Relic & special weapons** and counts one row per quest ("1 of 18").
  - Every row gets an **availability label**: Get now / Event running / Past event, on the Online Store / Collab, may return / Gone for good. Gone-for-good rows leave the totals by default.
  - Bardings and hairstyles are read through Dalamud's `IUnlockState`.
  - **Owned collectibles are saved in the snapshot** (an added field, no schema bump), so alts and other clients show real answers.
  - Expansion and State filters, and Copy missing [R2, R5, R7, C5].
- **Trust**
  - A "Game updated: N quests are newer than Tsukimichi's data" strip, with a New since data filter.
  - A plausibility guard: a capture that suddenly loses hundreds of completed quests is not saved, and one `.prev.json` backup is kept.
  - The hook gate relaxed to the patch date [R10, R4].

### 1.6.0 · Getting there (travel, routes, Questionable)

- **Teleport**
  - Knows attunement and shows the gil cost; never a dead click.
  - Lifestream only (decision 2): without it, the button names Lifestream.
  - "Already here" hint.
  - Aethernet hop to the shard nearest the giver [C2].
- **Routes**
  - An **active route in the Todo overlay** (the next 3 steps), separate from pins.
  - Teleport and Flag on every step; consecutive steps at one aetheryte merged into one stop.
  - **Flag next stop.**
  - Routes for a whole job, all your pins, or an expansion's blues.
  - Route to this from the Duty Finder panel and My blues [R6, C3].
- **Next stops:** Ready quests grouped by aetheryte for Tonight and the overlay [R6].
- **Questionable**
  - **Send to Questionable** for a route, chain, expansion or pins: appends by default, with an explicit Replace option. The result is checked through Export ("Sent 14 of 17, 3 have no path").
  - "On Questionable's list (#n)" and "has a path" badges.
  - Live status while a Tsukimichi window is open [C1].
- **vnavmesh "Walk to giver"** with Stop (decision 1) [C3].
- **AutoDuty and Artisan hand-offs:** "Run with AutoDuty" for duties a quest or route needs (Duty Support or Trust by default), "Craft with Artisan" for items a quest asks for, Start and Stop Questionable, and the Companion plugins list (decision 1) [C7].

### 1.7.0 · In the game (native surfaces, onboarding)

- **"Worth it?" panel** beside the quest-offer window: Moonlit reward, what it unlocks, chain step, patch. Spoiler-safe; it never presses Accept [R8].
- **"Opened by that":** after a completion, one chat line ("Opened: 2 feature quests, 8 side quests") and a panel beside the quest-complete window [R8, R9].
- **Chat:**
  - clickable [Open] [Pin] [Route] actions on Tsukimichi's chat lines;
  - Chat 2 "Open in Tsukimichi" on quest and item links;
  - the reward menu entry on item links in normal chat [R8, C6].
- **Optional nameplate marks:** "☾ Moonlit reward / Ready on WHM / Pinned" on quest givers [R8].
- **Onboarding**
  - The tour selects a real quest, so its Read chapter shows real requirements.
  - A first-run "Set up your road" card (overlay, notices, server info bar).
  - Overlay and Nearby buttons in the main window.
  - A Help topic "While you play".
  - **What's new collects every version you skipped.**
  - `/tsuki tour`, `/tsuki blues` and similar commands, plus "did you mean".
  - A privacy card: what Tsukimichi reads and keeps [R1, R9].
- **Settings:** a section index with search, Night chrome, and Polling moved to Advanced [R1, R3].

### 1.8.0 · Moon Road part 2, speed, alts

- **Look:**
  - the quest list in the Moon Road style (title with count, Eyebrow header, a road line under Ready rows);
  - brass corners on cards;
  - Moon Road headings in Route, Nearby, Todo and My blues;
  - Settings and Help honour UI scale;
  - ellipses instead of names cut mid-letter;
  - a loading moon that respects Reduce motion [R3].
- **Speed:**
  - Re-evaluation after a job or level change moves to a worker.
  - Derived indexes are built on the catalog worker.
  - Opening a character runs in the background, so changing gearsets and clicking a character no longer stutter [R4].
- **Alts and multibox:**
  - stable character order and a remembered Compare target;
  - per-character settings in one merged shared file (no repeated notices across clients);
  - hide, don't track, and "forget characters not seen in N days";
  - a **who-has-it grid** (rewards × characters);
  - fewer recomputes when other clients save;
  - an honest "not updating" status [R7, R5].

### Side track (fitted into the releases above where convenient)

- **IPC v1 additions:** `GetGates`, a `Disposing` message, `GetStates[]`, `GetQuestsInState`, `GetQuestsInZone`, `GetQuestsForItem`, `GetMoonlitStatus`, `GetRoute` and `GetFirstBlocker`, plus a per-quest `QuestStateChanged` message. Ship a drop-in client file and an EzIPC example [C10].
- **Open in Quest Map; "Open on…" Lodestone / Garland / Wiki / Teamcraft** (a shipped link table built by the Verify tool, spoiler-aware); Copy for Discord (split at 2,000 characters); Copy table as TSV; added export fields; a correct FFXIV Collect import file [C9, C5, C10].
- **Hand-in items:** a detail-pane section listing the items a quest needs and how many you have, from the quest script's RITEM data, plus retainers through Allagan Tools. "Needed for a quest" on the item menu. A Teamcraft link for what's missing. "Gather" through GatherBuddy [C4, C7].
- **Planning extras:**
  - EXP and gil per quest, after the formula is checked against the reward window;
  - "level DRG 52→56 to open 7 quests";
  - MSQ catch-up summary per expansion;
  - allied society board with reset timers [R6].
- **Collector extras:**
  - completion dates, starting from install;
  - free-trial view ("Beyond your trial");
  - story recap ("Previously…") from completed journal text;
  - progress on achievements that need several quests;
  - New Game+ replay badge [R9, R5, C5].
- **Patch 8.0 readiness:**
  - weekly scheduled CI, plus a non-blocking build against Dalamud API 16;
  - LoadCheck in the release workflow;
  - expansion lists derived from the game data;
  - an API 16 migration branch before 8.0 (SeString to ReadOnlySeString, event renames, `HoveredItem` uint) [R10].
- **Code health:** move CharactersPane's 1,000 lines of view-model building into Core with tests [R10].

## Decisions (settled 2026-10-01)

1. **Full automation through other plugins is allowed.**
   - **Companion plugins list:** in Settings and the first-run card. Each plugin is shown as installed, outdated or missing, with what it unlocks in Tsukimichi and its repository link.
   - **A button that needs a missing plugin** names that plugin and why, instead of disappearing.
   - **Plugins Tsukimichi works with:** Questionable (start, stop, send routes), AutoDuty (duties a quest or route needs), Artisan (items a quest asks you to craft), GatherBuddy (items a quest asks you to gather), vnavmesh (walk to the giver) and Lifestream (travel).
   - **Before a hand-off,** Tsukimichi checks that the plugins it depends on are present. Questionable itself calls AutoDuty and Artisan for some steps.
   - **AutoDuty default:** Duty Support or Trust when the duty offers them; a setting allows the regular Duty Finder.
   - **The README promise becomes** "Tsukimichi automates only when you press a button that hands the work to one of these plugins".
   - **Where it ships:** the Questionable, AutoDuty and Artisan controls move into 1.6.0.
2. **Teleport goes through Lifestream only.** There is no Teleporter fallback and no use of the game's own teleport. Without Lifestream, the button says Lifestream is needed. The Companion plugins list recommends it.
3. **Optional integrations only where they help with questing:** Questionable, AutoDuty, Artisan, GatherBuddy, vnavmesh, Lifestream, Allagan Tools, Quest Map and Chat 2. QoL Bar and Teleporter are dropped.
4. **Moonlit totals:**
   - Rewards that are gone for good leave the totals by default, and a toggle brings them back.
   - Other-path variants are hidden.
   - Each relic or special weapon quest counts once.
   - Collaboration events read "may return".
5. **Allied dailies not offered today** read Blocked "not offered today" and are left out of the Nearby count and Ready-only lists.
6. **The hook gate** pauses only on a new patch date, not on every hotfix.
7. **First run** shows a "Set up your road" card. The Todo overlay stays off until the player turns it on.
8. **"Open on…" links** open in the browser. The plugin itself stays offline: no Lodestone or FFXIV Collect fetches, and no data channel.
9. **Owned collectibles and quest completion dates** are saved locally, on by default, and included in the export.
10. **No upstream contributions.** Nothing is sent to other developers' projects; every limitation in another plugin is worked around on Tsukimichi's side (Wotsit's result cap: B7; Questionable's missing path and event gates: polling and lock answers).
11. **API 16 preparation** starts now, as a non-blocking CI job.
12. **Release order:** 1.4.2, then 1.5, 1.6, 1.7, 1.8, with the side track folded in.

## In-game checks I'll need from you along the way

- **Rank-up day:** the bit is set.
- **Repeat-flag quests:** the flag sets, and clears at reset.
- **Allied dailies:** the computed offer matches what the quest givers actually list.
- **Free trial:** what a trial character reports as its expansion and level caps.
- **Journal:** whether right-clicking a quest in the game's own Journal opens a context menu.
- **Multibox:** two clients (still open from v4).
- **Questionable:** the live status works on both versions of Questionable.
