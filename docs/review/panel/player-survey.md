# Tsukimichi V3 plan — player survey panel

Date: 2026-09-28
Method: five simulated FFXIV players, each briefed with docs/feature-plan-v3.md, docs/research/player-gripes-2026.md, CHANGELOG.md (0.1.0–0.5.0), docs/design/ui-revamp-proposal.md, docs/design/mockups/main-window.html and docs/design/glyphs/proposal.md. Each persona reacted from their own play, not from the plan's own framing. The moderator synthesis at the end is the panel's output; the persona sections are the evidence for it.
Status: review input for the final proposal review; nothing here is a decision.

Reading key for task ids: T1–T17 are the 0.6.0–0.8.0 tasks, P1–P14 the player features (0.9.0, 1.0.0, Later).

---

## Persona 1 — "Aldric", 2013 veteran completionist (spreadsheets, FFXIV Collect, five characters, main at 100 % MSQ)

**What he'd use daily**
- The Journal tree, but only if the exact `done/total` stays on the row. The UI proposal §2.3 moves `821/905` into the tooltip and Dense mode and shows "82%"; the glyph proposal §4.3 and T11 keep `done / total` with a mini bar; the mockup shows percentages. He needs the count: a percentage cannot tell him that one quest is missing in a 612-quest section (99.8 % rounds to 100 %). "If I have to hover to see the number, the plugin is slower than my spreadsheet."
- P12 Export, immediately. He would install the plugin for P12 alone: xiv-shinies asked for a plugin to sync cards, the Lodestone lags a day and misses orchestrion and cards. He is annoyed that a size-S, data-only feature sits in 0.9.0 behind three releases of visual work.
- Moonlit, with the T4 Online Store overlay, and P11's per-character seasonal history by year.
- T1: he has retired quests completed (pre-5.3 ARR MSQ, 5.5 Summoner). "A completed one still shows as Completed when revealed" is right; what he actually needs is the reverse check on the 44 retired rows with a same-name listed replacement. If the game flags only the old id on his 2013 character and the listed replacement is what the tree counts, ARR reads 99 % forever with no way to fix it. He wants a T1 test: a snapshot completing the retired twin but not the replacement must not leave the replacement uncounted or Blocked (or the tree must say "done as the retired version").

**What he'd never open**
Todo overlay, Tutorial, "Since you were away", Cards mode, "Clear my blues", the level-up nudge (turned off day one), the star-chart Path.

**What confuses him**
- "5,373 quests" in the status bar, "showing 5,193 of 5,193" in the plan, 5,149 on the Lodestone, and the 180 Unlisted. Three totals in one product; he will ask which one is "100 %".
- "Retired and hidden": retired means removed from the game; "hidden" is also what T7's "Not unique (hide)" does and what the report calls the 23 hidden chain steps (which get refiled, not hidden). One word for three things.
- "Feature Quests" (a journal genre, 71 % in the mockup) directly above "Feature Unlocks" (a derived virtual node, 67 %). He assumes one is a bug.
- T4's "also on the Online Store" understates it. For a past-year festival item the quest is gone; the store is the *only* source now. "Also" implies the quest still works.

**What's missing**
- A "Store only" confidence/filter in Moonlit so the 68 past-seasonal entries stop inflating his unique-reward denominator.
- Export scope: completed quest ids, Moonlit obtained state, and (since xiv-shinies asked for exactly this) Triple Triad cards and orchestrion rolls, with the id scheme documented (sheet row id, not name). A CSV in addition to JSON; his spreadsheet cannot read JSON without a macro.
- A visible data-version stamp ("catalog 7.5b, verified 2026-10-xx") he can quote when he files a discrepancy.
- A "count retired quests I completed" line in the tree tooltip, because his personal 100 % includes them.

**What he'd cut**
Cards mode, T17 motion (chevron rotation, reveal pulse, breathing pip: "I run fifty plugins; nothing should breathe"), the hero banner (he wants two more table rows instead), Compare with's "unlock value" ranking (he wants the raw list; the ranking hides the one quest he cares about behind 25 rows and "and N more").

**One idea the plan does not have — a "Sources" line per quest**
T2a produces, per quest, the Garland Tools, Lodestone and wiki URLs and a per-source verdict. Put that in the detail pane's provenance caption: "Verified against Lodestone · Garland Tools (2026-10-04)" with the links copyable (`Copy source links`). It turns a week of agent verification into something a player can see and trust, and gives him a one-click way to report where the plugin and the Lodestone disagree. Effort S once T2a exists.

---

## Persona 2 — "Pip", sprout, finished A Realm Reborn last month, twelve blue quests in the journal and thirty on the map

**What she'd use daily**
- The Todo overlay and Nearby quests (both shipped), P1 blocker line ("after MSQ: The Vault" is exactly the sentence she is missing), P2's NPC context menu (she would never type `/tsuki why`, but she right-clicks NPCs constantly).
- P3 "Clear my blues", which is the single feature written for her, and P13 Duty Finder hint (she is "Nervous about Crystal Tower" personified). Both are in 1.0.0, roughly twelve weeks of agent work away. "By 1.0.0 I'll either have Googled it or quit."
- P5 payoff gates: she will hit the Shadowbringers role quests within two months. In 1.0.0 it arrives after she has already missed them.

**What she'd never open**
Moonlit (she cannot tell from the name that it is about rewards), Characters › Compare with, Export, "Retired and hidden", Settings › Display › Density, the Glyph debug window, Mark as unique (she does not know what "unique" is being marked).

**What confuses her**
- The state words. "Foreclosed" reads as a punishment ("did I fail it?"); it means she picked the Maelstrom. "Veiled" (mockup: "Veiled · achievements not loaded") means nothing to her. "Done this cycle" — cycle of what? "Blocked" sounds like a bug. "Ready on other job" she gets. She would rather read "Not available: you chose the Maelstrom", "Not checked", "Done today / this week".
- The status bar tells her there are 5,373 quests. That number *is* the overwhelm complaint (gripe #2). The default view of the plugin is a completionist's view of the whole game; nothing in the plan makes a small view the default for a new player.
- Two moon systems side by side: state moons (solid) and halo gauges (ring + core). The glyph doc explains why they must differ; she just sees "why is this moon hollow". The tab strip's Journal moon (16 px, no arc) is a third variant.
- "Filters (2)" badge plus a chip row plus presets on the toolbar plus presets mirrored in the filter panel: three places that change what the table shows.
- The hero banner shows journal artwork for the *selected* quest regardless of state (mockup: Brotherhood of Ash, Blocked, full banner). She selects quests she has not done to read their requirements; the art is a spoiler for the quest she is about to do.
- "Tribal Quests" in the hero meta line; the game has said "Allied Society Quests" since 7.0 and the research doc says to use the game's term.

**What's missing**
- A spoiler guard, anywhere. P5 says "spoiler-free" but the tree shows Endwalker and Dawntrail as sections, the MSQ chip names the next MSQ quest, the table lists every future MSQ quest by name when she clears filters, and the Path card lists names of quests she has not reached. Nothing hides banner art for undone quests.
- A "what does this blue quest unlock" tag on the row itself. P3 has it (unlock kind) but only inside the plan view; the table's Rewards column shows icons, not "unlocks: Crystal Tower".
- The "abandon-safe" note (P3's optional bullet): which accepted blues she can drop without losing progress. That is the "dozens in my journal" complaint and it is buried as an optional line inside a 1.0.0 feature.
- Roulette relevance for P13: "this unlock adds X to Alliance Raid roulette".

**What she'd cut**
T2a's week (she understands why but it is a week she is waiting), P8 patch of origin, P12 export, Cards mode, the star-chart Path (she wants "do this next" before constellation art), Density modes (three ways to be wrong).

**One idea the plan does not have — "Sprout mode" (spoiler shield + small view)**
A single first-run switch: tree sections beyond the character's MSQ expansion collapse and show only counts; MSQ quests beyond the current position render as "Main Scenario · quest 47 of 195" instead of their name; banner art is shown only for completed quests; the status bar shows "showing 412 quests in your reach" rather than 5,373. The evaluator already knows MSQ position (0.3.0) and expansion per quest, so this is a presentation filter over existing data (S–M). It also gives P5 its "spoiler-free" guarantee for free. Tasks touched: T11, T12, T16, P4, P5.

---

## Persona 3 — "Marius", returning after 18 months, back to finish Dawntrail before Evercold

**What he'd use daily**
- MSQ position in the status bar and dashboard (shipped), the Stalled preset (everything he has is stalled), P1 blocker, P11 seasonal now (he missed Nocturne for Heroes and does not intend to miss the next one), P8 patch of origin ("Added in 7.35" is the one label the game refuses to show).
- P7 "Since you were away" is the feature named after him, and it does not work for him: it triggers "after N days since the last capture", and he has never had a capture. He installs the plugin today; there is no "then". P7 as written serves players who already had Tsukimichi installed when they left, a population that on 0.5.0 is roughly the owner.
- P13: his roulettes have collapsed onto Crystal Tower because he never unlocked the later alliance raids.
- P14: he came back for 8.0. The plan puts P14 in "Later", after 1.0.0. Adding the effort column (~2 + 1 + 2 + 2 + 3 weeks = ~10 weeks of agent work, plus in-game verification per release) lands 1.0.0 in mid-to-late December 2026; Evercold is January 2027. P14 is size L. The branching MSQ ships before the plugin can read it.

**What he'd never open**
Moonlit's confidence filter, Export, Compare with, the Glyph debug window, Density settings, "Retired and hidden" (he wondered whether quests he remembered were removed in 7.x; the research doc says none were, but the node name will make him check).

**What confuses him**
- The 7.5 prerequisite change on Splendorous-tool sidequests ("Go West, Craftsman" → "Inscrutable Tastes"): he finished the old prerequisite; will the plugin show him Blocked on the new one? The research doc flags it; no task in T1–T8 names it.
- The plugin's own interface changes twice during his return (0.7.0 glyphs, 0.8.0 chrome). He is re-learning the game and will be re-learning the tracker every few weeks; the tutorial is redone in T14, but the Help topics, screenshots and the fifteen tour steps are not listed as deliverables of T9–T12.
- "Stalled: 7 days" fires on every accepted quest he has. Fine, but the empty-state guidance for "everything is stalled" should say "welcome back" rather than list filters.

**What's missing**
- A first-run orientation that does not need a prior snapshot: "You are at MSQ 7.2 · Dawntrail; 3 job quests behind on VPR; 2 event quests running; 148 quests added since patch 7.1". Everything but the last clause is derivable today; the last clause needs P8.
- P7's dependency on P8 is stated in the research doc (the "quests the catalog gained" line) but the plan puts P8 in 0.9.0 and P7 in 1.0.0 without saying P8 is P7's seed. It should say so, and P7 should be scoped to work with a user-declared "I last played in patch X" when no capture exists.
- Abandoned ledger (P10) is useless retroactively for him; he would rather have "accepted quests whose prerequisite changed since 7.1" (the 7.5 note again).

**What he'd cut or defer**
T17 motion, Cards mode, P9 journal text reader (nice, last), P12 export, the T16 star chart. He would trade all five for P14 before January.

**One idea the plan does not have — "I last played in patch…" picker**
On first run (and in Settings › Characters) a patch picker (2.0 … 7.5). It seeds P7 without a snapshot, drives a "New since 7.1" group in the Feature quests preset (P8's `AddedIn` makes it a one-line filter), and lets the returning-player card list MSQ patches between then and now by name without spoiling their content. Effort S on top of P8. It turns P7 from an owner-only feature into the returning-player feature the evidence (11 threads) asked for.

---

## Persona 4 — "Sable", alt-heavy, levelling a third character (Maelstrom, Twin Adder and Immortal Flames across the three)

**What he'd use daily**
- Compare with (shipped) and P6 unlock route — he is doing the Blue Mage rush on the third character right now, and P6 is in 1.0.0. "The alt will be at cap before the route exists."
- P1 blocker on job ladders, Nearby, Todo overlay, P2's NPC menu, P10 abandoned ledger (he abandons to declutter and forgets what he dropped), the character chip with the live dot (T14; he switches characters a dozen times a session).
- T5's character-switch fix (Recent activity leaking, notices re-announcing on a fast switch) is *his* bug and he is glad it is first. He wants the same fix applied to the level-up nudge and to every notice in P5/P7/P11.

**What he'd never open**
Moonlit confidence filters, P9, P4 story sidequests, Cards mode, Density, the star chart.

**What confuses him**
- "Veiled" means two things: the Unknown state ("Veiled · achievements not loaded") and every stored character's unreadable data ("veiled for stored characters" in the Flight tab, the veiled moon on the character chip in snapshot view). He reads Veiled on an alt row and cannot tell whether it is the character being offline or the requirement being unevaluable.
- "Foreclosed" on an alt does not say which Grand Company that alt picked; the tooltip should ("Foreclosed: chose the Maelstrom").
- Are pins per character or per account? P6's "Pin all" and the Todo overlay's "your pins" do not say. If pins are global, "Pin all" on the alt's route pollutes the main's overlay.
- P7's default (14 days since the last capture) will fire on every alt login; his second character sits idle for a month at a time. The card becomes a nag within a week.
- Notice budget: level-up nudge, pinned/feature notices, P5 one-time notice, P10 chat line, P11 login notice, P7 card. Multiplied by three characters at login. Nothing in the plan caps or centralises them.

**What's missing**
- An account-wide unlock matrix: rows = duties/systems/jobs (Feature Unlocks), columns = characters, cells = state moons. "Which alt has Orbonne unlocked so I can run the roulette on it" is the question Compare with cannot answer because it is pairwise and value-ranked.
- P6 should reuse a route: run it on character 2, save the ordered list, replay it on character 3 with each row re-evaluated. Copy route to clipboard is the poor man's version of this.
- P3 per character with a "done on any character" column, so he skips the blues that only matter once (mounts, minions) and does the ones that matter per character (duties).
- Per-character notice settings ("quiet on this character").

**What he'd cut**
The hero banner, Cards mode, T17 motion, the star chart, P9. He would take a plain list over any of them if it shipped P6 sooner.

**One idea the plan does not have — the unlock matrix**
A Characters-tab view: Feature Unlocks (the derived set, ~1,699 quests, grouped by unlock kind from P3's tagging) as rows, stored characters as columns, one state moon per cell, click a cell → P6 route on that character. It reuses the offline evaluation Compare with already does per snapshot and the halo/moon glyphs from T9–T10. Effort M. Evidence: gripe #5 (12 threads) is about *lists across characters*, which is a matrix, not a pair.

---

## Persona 5 — "Ysolde", lore reader, does every sidequest with artwork, keeps a reading log

**What she'd use daily**
- P4 story sidequests preset (0.9.0, good), the book badge, chain N of M. She wants it as a *sort* and a persistent tree filter, not only a preset she has to re-click.
- P9 journal text reader. Four threads is a small count but 292 points on one of them; for her it is the only feature that makes Tsukimichi a journal rather than a checklist. It is the last item of 1.0.0 with "may slip" energy around it.
- P5 payoff gates: the one curated feature she would contribute to.
- The hero banner (T16) and Cards mode (T15): she is the only panellist who wants both, and Cards mode is exactly her "browsing mode" (the UI proposal calls it that). She notes the owner let it slip to 0.9.0 with no one arguing for it; she is arguing for it.
- T16's star-chart Path, with a question: is the path in *prerequisite* order or *story* order? For lore they differ (the prerequisite graph is a DAG; the story is release order). She reads in release order.

**What she'd never open**
Export, Compare with, the Duty Finder hint, "Clear my blues" (she does not want to clear anything), Moonlit's confidence filter, Density › Dense.

**What confuses her**
- The artwork tell is a heuristic and the plan treats it as a flag: many one-quest vignettes have art and several multi-quest chains have art only on the first quest; most 2.x sidequests have no art but do have chains. P4 needs curated exceptions and a "no art but chained" rule, and the plan should say so.
- "Retired and hidden" hides the 5.3 ARR quests that were her favourite ARR side content; they are lost stories, and the client's text sheets may still carry them. She wants them readable, not hidden, at least in P9.
- The Path card answers "what unlocks this" and the chain bar answers "N of M"; neither answers "where does this sit in the story" — which patch, which expansion arc.
- Same "Feature Quests vs Feature Unlocks" confusion as the others; also "Side Story Quests · Tribal Quests" in the hero meta line (the game's own genre names are "Side Story Quests" and "Allied Society Quests").

**What's missing**
- A reading order. P8's `AddedIn` is the key: "sort by patch added" gives release-order reading for free once P8 lands. The plan never connects P8 to P4/P9; it should.
- A spoiler guard on artwork (same as the sprout): she wants banner art hidden for quests she has not completed, because the art is a summary of the quest.
- Recurring-NPC browsing: "every quest this NPC gives" (P2's "quests here (N)" does that for a targeted NPC; she wants it from the Giver card too).
- P5's starter set is MSQ-payoff only. Side-story payoffs she would add for verification: The Sorrow of Werlyt before Endwalker's Garlemald arc (Gaius), Return to Ivalice before Bozja (a hard prerequisite, not just a payoff), the Coils before Alisaie's later arcs. And a rubric for what counts as a "payoff" so the table does not become "everything is connected".
- New Game+ awareness: since 7.1/7.4/7.5, NG+ replays the 7.0 MSQ, job and role quests, Chronicles, side stories, crafter/gatherer quests and Echoes of Vana'diel. A completed quest that is replayable should say so.

**What she'd cut**
Density › Dense, T17 motion, P13, P12, the "unlock value" ranking in Compare.

**One idea the plan does not have — "Replayable in New Game+" badge and reading order**
On completed quests, a small badge "Replay in New Game+ · Chapter: Dawntrail" mapped from the NG+ chapter sheets, plus "Sort by patch" (P8) and a "Story order" toggle on the Path card. The NG+ mapping is sheet data, the sort is P8, the Path toggle reorders existing nodes by `AddedIn` then prerequisite. Effort S–M after P8. It makes the plugin the index to the game's own replay feature, which no website is.

---

## Moderator synthesis

### (a) The five most common reactions

1. **The order is backwards for players.** Four of five personas wanted at least one 1.0.0 item in 0.9.0, and each named a different one (P3 sprout, P6 alt, P13 sprout and returning, P7 returning, P9 lore). The strongest-evidence gripes (#2 overwhelm 12 threads → P3; #5 alts 12 threads → P6) are in the *second* set; P4 (13 threads) and P8 are in the first. The panel understands "correctness first" for 0.6.0 but not why three releases of data and visual work precede every player feature. Combined with the effort column, 1.0.0 lands within weeks of 8.0 and P14 is after that.
2. **"Feature" means three things.** "Feature Quests" (journal genre), "Feature Unlocks" (derived virtual node, directly below it in the tree at a different percentage), and the "Feature quests" / "Features" preset. Every persona stumbled on it; two assumed a bug.
3. **The state vocabulary is opaque outside the veteran.** Foreclosed, Veiled, Done this cycle, Blocked; and Veiled carries two meanings (the Unknown state; a stored character's unreadable data). The mockup's state pill is the only place the *reason* appears. The plan's P1 "blocker line" is the right cure but it is scoped to ladders and Nearby, not to the state pill and tooltips everywhere.
4. **No spoiler guard, anywhere.** Raised independently by the sprout, the lore reader and the returning player (mid-Dawntrail). The hero banner shows art for undone quests; the tree, MSQ chip, table and Path card name future MSQ quests; P5 promises "spoiler-free" with no mechanism. This is the one gap that makes the plugin unsafe to recommend to a sprout.
5. **Counts vs percentages, and the design docs disagree.** UI proposal §2.3: percentage on the row, count in tooltip and Dense. Glyph proposal §4.3 and T11: `done / total` plus a mini bar. Mockup: percentages. Veteran and alt need the count; sprout and returning prefer the percentage; lore reader indifferent. The plan must state which one T11 builds.

Also raised by two or more: notice fatigue across P5/P7/P10/P11 and the level-up nudge, multiplied by alts (alt, returning); P7 has no path for a player without a prior snapshot (returning, alt); "Tribal Quests" in the mockup against the research doc's own "use allied society everywhere" (sprout, lore); the total-quests number is three different numbers (veteran, sprout).

### (b) Proposed changes to the plan

Priority: **High** = the panel thinks the release is worse without it; **Med** = clear improvement, small cost; **Low** = taste, decide with the owner.

| # | Change | Tasks affected | Priority | Raised by |
|---|---|---|---|---|
| 1 | Reorder the player sets: 0.9.0 = P1, P2, **P3**, **P6**, P10, P12; 0.9.5 (or 0.10.0) = P11, P4, P13, P5, P8; 1.0.0 = P7, P9, **P14**. P11 must still ship before Starlight (December). | §4 order table | High | all five |
| 2 | Pull P14 out of "Later" into 1.0.0 with a January 2027 deadline; if 1.0.0 cannot make it, ship the synthetic-branch fixture and the resolver change alone as 0.9.x. | P14 | High | returning, veteran |
| 3 | Add a **spoiler guard** (setting, on by default for characters below the latest expansion): banner art only for completed quests; MSQ names beyond the current position masked as "Main Scenario · N of M"; tree sections beyond the character's MSQ expansion collapsed with counts only; Path card masks unreached MSQ names. Give P5 its "spoiler-free" mechanism through this. | T11, T12, T16, P4, P5 (new sub-task in 0.7.0 or 0.8.0) | High | sprout, lore, returning |
| 4 | Resolve the count/percentage conflict in writing: row shows `done / total` (glyph doc, T11), percentage in the tooltip and the status bar, Dense unchanged. Update the mockup and UI proposal §2.3 to match. | T11, T12, mockup | High | veteran, alt |
| 5 | Rename to kill the three "Feature"s: keep the journal genre "Feature Quests" (the game's name); rename the virtual node to "Unlocks (all)" or "Everything that unlocks"; rename the preset to "Unlocks I can start"; toolbar segment label the same. | T1, T14, Strings | High | all five |
| 6 | Give P7 a no-snapshot path: an "I last played in patch X" picker seeds the card; P8 becomes P7's declared dependency; P7's trigger becomes per-character and only when there is something to say (mid-way quests, MSQ moved, N new quests > 0), default off for characters captured fewer than twice. | P7, P8, Settings › Characters | High | returning, alt |
| 7 | A single notice budget: Settings › Notices gains "at most N lines per login" and per-character quiet; P5, P10, P11, the level-up nudge and P7 all go through it. | P5, P7, P10, P11, T5 (character switch) | Med | alt, returning |
| 8 | P12 moves to 0.6.0 (it is S, data-only, no UI dependency) and grows CSV output plus Triple Triad cards and orchestrion; ids documented as sheet row ids. | P12, T4 | Med | veteran |
| 9 | T1 adds the retired-twin test: a snapshot completing a retired row but not its same-name listed replacement must not leave the replacement uncounted or Blocked; if the game does not carry completion over, show "done as the retired version". | T1 | Med | veteran |
| 10 | Moonlit: the 68 past-seasonal entries get a "Store only" source (not "also on the Online Store") and a confidence filter value so they can be excluded from the unique-reward denominator. | T4, T8, MoonlitPane | Med | veteran |
| 11 | P1's blocker sentence becomes the state pill and tooltip text everywhere, not only on ladders/Nearby: "Foreclosed: chose the Maelstrom", "Not checked: achievements not loaded". | P1, T8, T16 | Med | sprout, alt |
| 12 | 7.5 prerequisite swap ("Go West, Craftsman" → "Inscrutable Tastes"): add a T3/T5 test that a character who completed the old prerequisite is not Blocked on the Splendorous quests; general rule: prerequisite edges from the current sheet, plus a curated "superseded prerequisite" list. | T3 or T5 | Med | returning |
| 13 | P4 states the artwork heuristic's limits and adds a curated exceptions file plus a "chained but no art" rule; P4 and P9 get "sort by patch" from P8 (reading order). | P4, P8, P9 | Med | lore |
| 14 | P6 gains "save route / replay on another character"; P3 gains a "done on any character" column. Consider the unlock matrix (persona 4's idea) as P6b in 1.0.0. | P3, P6 | Med | alt |
| 15 | Pins: say whether they are per character (Todo overlay tooltip, P6's "Pin all"). If they are global, make them per character before P6. | P6, TodoOverlay | Med | alt |
| 16 | The status bar shows one total (the listed catalog), and the tooltip explains retired/Lodestone deltas. T2a's 5,373 stays in the docs. | T12 | Low | veteran, sprout |
| 17 | T9–T12 list the Help/Tutorial/screenshot updates as deliverables (the tutorial is only named in T14). | T9–T12 | Low | returning |
| 18 | T16's Path card: the "next step" bar stays above the star chart and the target row is visible without scrolling; add a "story order" toggle once P8 exists. | T16, P8 | Low | sprout, lore |
| 19 | Cards mode stays in the plan as the lore reader's browsing mode and is where the P4 book badge and a P9 excerpt live; if it slips, it slips to the release that carries P4. | T15, P4 | Low | lore |
| 20 | P2's "quests here (N)" also reachable from the Giver card ("Other quests from this NPC"). | P2, T16 | Low | lore |
| 21 | Sources line from T2a in the provenance caption ("Verified against Lodestone · Garland Tools, date", copyable links). | T2a, T16 | Low | veteran |
| 22 | New Game+ "replayable" badge from the NG+ chapter sheets on completed quests. | new, after P8 | Low | lore |

### (c) Features the group would move

| Feature | Plan | Panel | Votes | Why |
|---|---|---|---|---|
| P3 Clear my blues | 1.0.0 | 0.9.0 | 4–1 (lore abstains) | The 12-thread gripe with the most concrete answer; the sprout's only feature. |
| P6 Unlock route | 1.0.0 | 0.9.0 | 3–2 | Alt's only feature; the panel notes it is S–M and reuses the path code. |
| P14 Branching MSQ | Later | 1.0.0 (before January 2027) | 4–1 | Timeline arithmetic; the plugin cannot break in 8.0 week. |
| P12 Export | 0.9.0 | 0.6.0 | 3–2 | Size S, no UI dependency, the one feature a tracker-site user installs for. |
| P13 Duty Finder hint | 1.0.0 | first in 1.0.0, or 0.9.5 | 3–2 | Sprout and returning use it; the roulette-collapse threads are the largest by comment count (380, 144). |
| P7 Since you were away | 1.0.0 | 1.0.0 but rescoped (no-snapshot path) | 5–0 | As written it only serves players who had the plugin before they left. |
| P8 Patch of origin | 0.9.0 | 0.9.5 with P7, or keep and declare it P7/P4/P9's seed | 3–2 | Nobody uses it alone; three features depend on it. |
| P4 Story sidequests | 0.9.0 | keep | 3–2 | Lore reader daily; the sprout and alt would not miss it. |
| P9 Journal text reader | 1.0.0 (last) | keep, but not "may slip" | 4–1 | Lore reader would move it up; the others would not open it; consensus is "ship it, don't lose it". |
| P11 Seasonal now | 0.9.0 | keep, deadline before Starlight | 5–0 | Time-sensitive: All Saints' Wake and Starlight fall inside the plan. |
| T2a Full verification | 0.6.0 | keep, run in parallel, must not gate the P set | 5–0 | Everyone wants correct data; nobody wants to wait a week for it. |
| T17 Motion | 0.8.0 | last in 0.8.0 or 0.8.1 | 4–1 | Only the lore reader wants it; "Reduce motion" should follow Dalamud's own setting if one exists. |
| T15 Cards mode | may slip to 0.9.0 | keep, tied to P4 | 1–4 want it; 5–0 say do not drop it | It is the one browsing mode the plugin has. |
| T16 Star chart Path | 0.8.0 | keep, next-step bar first | 3–2 | Sprout and alt need the answer before the picture. |

### (d) Wording and naming the group found unclear

| Term | Where | Problem | Suggestion |
|---|---|---|---|
| **Moonlit** | tab, pane, verdicts | Nobody but the veteran and lore reader guessed it means unique rewards; the tab strip gives it a crescent icon and "41%", not a word. | Keep the brand word in the window title and Help; label the tab "Rewards" (or "Moonlit · rewards" if the strip can take a subtitle) and keep the moon icon. |
| **Foreclosed** | state, stripe, pill | Reads as punishment; does not say why (Grand Company choice). | "Closed off", or keep the word but the pill/tooltip always says "chose the Maelstrom"; Foreclosed only in the legend and Help. |
| **Veiled** | Unknown state text, stored-character data, empty states | Two meanings. | Reserve Veiled for "this character's data cannot be read live" (stored character). The Unknown state reads "Not checked · achievements not loaded". |
| **Retired and hidden** | Unlisted node rename (T1) | "Hidden" collides with T7's "Not unique (hide)" and the report's "hidden chain steps" (which are refiled). | "Removed from the game (N)", off by default; tooltip "still Completed if you did them". Setting: "Show quests the game removed". |
| **Done this cycle** | state | Cycle of what? Daily and weekly resets differ. | "Done today" / "Done this week" by the quest's reset kind; the glyph stays. |
| **Blocked** | state, stripe | Sounds like a bug; it means "requirements not met". | Keep the word (short) but always pair it with the decisive blocker (P1 everywhere, change 11). |
| **Feature Quests / Feature Unlocks / Feature quests preset / "Features" segment** | tree, preset, toolbar | Three or four labels for two things. | Change 5 above. Also the toolbar segment "Features" (mockup) vs the preset "Feature quests" (0.3.0): one label. |
| **Level band** (mockup) vs **Around my level** (shipped) | toolbar segment, filter panel | Two names for one preset. | Pick "Around my level"; "Level band" is jargon. |
| **Tribal Quests** | mockup hero meta line | Renamed "Allied Society Quests" in 7.0; the research doc says so. | Use the game's current genre names in every string; add a data test that genre labels come from the sheet. |
| **Halo**, **Umbra**, **Bruise**, **Eclipse**, **Shadow** | token/glyph names | Internal; fine as code names. The panel only asks that none of them appear in user-facing text. | Check Help/Strings for token names before 0.7.0. |
| **Stalled** | preset | Understood by all; the returning player wants the empty-state copy to say "welcome back" when everything is stalled. | Copy tweak only. |
| **Mark as unique / Not unique (hide)** | Moonlit, detail | The sprout cannot parse what is being marked. T7's Shift guard is fine. | "This reward is quest-exclusive" / "Not quest-exclusive"; move both behind an "Advanced" fold. |
| **"also on the Online Store"** | T4 | Understates that the quest is gone. | "Event over · sold on the Online Store" (change 10). |
| **Density: Dense / Comfortable / Cards** | Settings › Display | Cards is not a density; it is a view. | "Row style: Compact / Standard / Cards". |
| **Compare with … "unlock value"** | Characters | The veteran wants raw; others fine. | Add "sort by name" beside the value sort. |
| **5,373 quests · showing 5,193 of 5,193** | status bar | Which total is the total? | One number (change 16). |
| **Classic layout** | Settings | Clear to all. | — |
| **Since you were away** | P7 | Clear; the mechanism is the problem, not the name. | — |
| **Clear my blues** | P3 | Clear to the sprout and alt; the lore reader dislikes "clear". | Keep; it is the players' own phrase. |

### Praise that says why

- **T5 first**: the tribe-daily-offer bug ("accepting one daily flips ~566 other dailies to Blocked") and the character-switch leak are the two defects that make the alt player distrust the plugin; fixing them before any visual work is the right call and the panel would not move it.
- **The halo gauge**: the glyph doc's argument (a ring arc is linear in the fraction; a crescent is a taper; the two families differ in silhouette so Ready is never mistaken for 50 %) convinced even the sprout once explained. The reservation is only that the explanation lives in a design doc and not in the Help legend.
- **The Classic layout toggle**: it is what lets four of five personas accept the whole-window Night chrome without argument.
- **P1 and P2 as presentation over the existing evaluator**: the panel agrees these are the cheapest high-value items in the plan and should not be displaced by the reorder.
- **Leaving out auto-travel, allowance timers and party views**: every persona had read enough patch notes to agree.
