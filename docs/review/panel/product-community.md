# Tsukimichi V3 — product and community review

Date: 2026-09-28
Reviewers: product lead (live-service companion tools) and community manager (large FFXIV Discord)
Scope: docs/feature-plan-v3.md against feature-plan-v2.md, research/player-gripes-2026.md, feature-panel.md, CHANGELOG.md and README.md. Read-only; no repo file other than this one was touched.
Questions answered: (1) release sequence, (2) which of P1–P14 will be talked about, cuts and a 1.0 headline, (3) community and Square Enix risk, (4) naming and messaging, (5) support load, (6) metrics-free success criteria. §7 collects every recommendation by task id.

---

## 0. Top findings

1. **The order is defensible but the cadence is wrong for word of mouth.** 0.6.0 (two weeks) and 0.8.0 (two weeks) are both invisible or risky releases with nothing a player can screenshot and post. Every release should carry one shareable feature. P1 and P2 are S-sized, sit on the existing evaluator, and answer the single most repeated question in the research (16 threads). Pull them into 0.6.0. (§1)
2. **T2a is the wrong shape.** A full-catalog re-verification of 5,373 quests against three web sources is a week of scraping whose product is a CSV. Take it off the release-critical path, drop Garland Tools as a per-quest source (it is derived from the same sheets the plugin reads, so it verifies nothing), keep the Lodestone as the official reference and the wiki as the human one, and commit facts only (ids, levels, counts, URLs), never quest text. (§1, §3)
3. **Ship a 0.5.1 hotfix for contrast, not art.** The moon contrast fix (Veil disc, terminator floor, rim) is one day and cannot regress data. The owner's revisions (interior maria, redrawn Accepted lens) are art iteration with a review loop and belong in 0.7.0. Bundle 0.5.1 with the three wrong-state bugs everyone would hit (tribe dailies flipping to Blocked, seasonal quests Blocked forever inflating totals, character-switch leak) and the owner's guard (T7). (§1)
4. **P7 is dead weight for a year.** "Since you were away" needs a snapshot taken before the player left. Today there are no users; anyone who installs after returning has no "then". Defer to 1.1 and revisit once installs exist. P12 stays cheap but is not a headline until a receiving site (xiv-shinies asked) commits to an import. (§2)
5. **8.0 Evercold lands in January 2027, three months from now, and P14 is in "Later".** The plan's ~10 weeks of agent work ends on top of the 8.0 API bump, when every plugin breaks for days. Either 1.0.0 ships before the 8.0 pre-patch with the P14 fixture in it, or 1.0.0 *is* the 8.0-ready release. Do not schedule anything else for 8.0 week. (§1, §2)
6. **"Tsukimichi" + "Moonlit" is the English title of an anime** (*Tsukimichi: Moonlit Fantasy*, 2021, second season 2024). Search for "Tsukimichi" returns the anime, not the plugin, and the pairing reads as fan branding. Keep the name (it carries the owner's meaning and the moon language is the identity); never use the two words together in public copy; always write "Tsukimichi (FFXIV quest tracker, Dalamud)"; give the Moonlit tab a plain-language subtitle. (§4)
7. **No LICENSE, stale README, no bug-report path.** A public GitHub repo with no license is all-rights-reserved; the first thing a Discord mod checks before allowing a custom-repo link is licence, source, build provenance and what the plugin hooks. The README still says "awaiting first in-game smoke test, 356 tests" and lists Wotsit as "not yet". Decision 7 (licence) is not "needed only for V2-20"; it is needed before the first public post. (§4)
8. **The biggest support surface is the curated overlay set, and it grows in this plan** (retired_quests, refile_overrides, online_store, chains, duty_unlocks, system_unlocks, festivals, P5 gates, P8 AddedIn, P2 quirk notes). Every one lags every patch. Add a data-version stamp that includes the curated overlay revision, an in-app "Report this quest" that copies a diagnostic block, and an auto-disable for addon-adjacent features on an untested game version. (§5)
9. **Spoilers are the community risk this plan under-weights.** P5 names story beats by construction ("Eureka before the Krile arc"), P9's full-text search would surface journal text of quests the player has not done, and the tree already shows every MSQ quest name. A "spoiler shield" (mask MSQ names beyond current position + N; P9 search over completed quests only; P5 phrased as "do X before Y", never why) is cheap and is itself a talkable feature. (§3)

---

## 1. Release sequence

### 1.1 What the proposed order gets right

- Correctness before cosmetics is the right instinct for a tracker: a wrong state or a wrong total is the one thing that kills trust, and a screenshot of a wrong "Ready" travels further than any feature. T1, T3, T5 are non-negotiable before a public post.
- Glyphs (0.7.0) before the interface (0.8.0) is right: the moons are the identity and appear in every screenshot; the chrome is a frame around them.
- A Classic toggle in 0.8.0 respects the lesson the panel itself recorded (F-107, DailyDuty issue #219: users hate being styled).

### 1.2 What it gets wrong

**No release before 0.9.0 has anything to talk about.** Word of mouth for a Dalamud plugin comes from four places: the official repository list, r/ffxiv "plugin recommendation" threads, YouTube "best QoL plugins" lists, and screenshots posted in FC and community Discords. All four need a screenshot that explains itself. The plan's first four releases produce (a) totals that match the Lodestone, (b) moons that read at 16 px, (c) a Night chrome, and only then (d) features. The first two are necessary but not shareable; the third is shareable only as "look, pretty", which is the weakest possible post for a *what and why* tool.

**0.8.0 is the highest-regression release, placed immediately before the feature releases.** Whole-window chrome, a custom tab strip, font handles, motion, three density modes and a Cards mode all touch scaling, which is already the dominant class in bug-hunt-0.5.0-ui. Every 0.8.x fix round delays 0.9.0. Interleaving small features into 0.7.0 and 0.8.0 decouples "when do we have something to post" from "is the chrome stable".

**T2a inflates 0.6.0 by a week for documentation.** The second-pass sample found 0 failures on 194 checks and eight *systematic* inaccuracies, all of which come from unmapped sheet fields, not from bad rows. A full pass will find more of the same class (sheet fields not consumed) and a handful of curated errors; it will not find row-level drift, because the catalog is read from the sheets. The value is real (a committed CSV to diff after patches) but it is not release-gating. Run it as a parallel stream and land its corrections as 0.6.x curated fixes (T4 pattern).

**The whole plan ends on the 8.0 launch.** ~10 weeks of agent work from 2026-09-28 lands mid-December; the pre-patch and API bump then break the plugin regardless of readiness, and the three-route MSQ breaks MSQ position, chains and the Todo overlay's "next MSQ" line. P14 is L and sits in "Later".

### 1.3 Moving a visible feature earlier: for

- P1 (blocker line) and P2 ("why not offered?") are S, sit on the evaluator that 0.6.0 is fixing, and answer grievance #1 (16 threads) and #10. A screenshot of an NPC context-menu entry that says "Saint Sayer: after MSQ quest The Vault" is the plugin's whole pitch in one image.
- The blocker string is exactly what a bug report needs. Building P1 first means the "Report this quest" diagnostic (§5) reuses it instead of inventing a second serialisation of requirement verdicts.
- P10 (abandoned ledger) is S, emotionally resonant (the Orbonne re-run thread), and is a one-line chat notice people will quote.
- None of these depend on 0.7.0 or 0.8.0 visuals.

### 1.4 Moving a visible feature earlier: against

- The owner verifies every release in game; more releases cost more owner evenings. Mitigation: keep the release count the same and change what each contains.
- A feature shipped before the moons are fixed gets screenshotted with bad moons. Mitigation: 0.5.1 contrast hotfix first (§1.5), and no public promotion before 0.7.0.
- P2 relies on evaluator correctness that T3 and T5 are still fixing; a "why not offered" that is itself wrong is worse than nothing. Mitigation: P2 ships in the same release as T3/T5, not before, and its curated quirk notes start with the six cases the research already lists.

Verdict: move P1, P2 and the diagnostic into 0.6.0; move P10 and P12 into 0.7.0; move P11 and P4 into 0.8.0. 0.9.0 and 1.0.0 shrink to the M-sized features (§2.4 has the table).

### 1.5 The glyph hotfix: for and against

For a hotfix (0.5.1, now):
- T9's diagnosis is measured (1.47:1 disc, 0.4 px sliver at 17/612) and the fix for *legibility* is a token change plus a floor: Veil `#4A5270` disc, rim `clamp(0.10r, 1.25, 3)`, terminator floor, notch minimum. That is a day, cannot regress data, and is the owner's own complaint about a build the owner uses every session.
- Living with the corrected moons for a month before the art pass gives the owner real-use evidence for the art decisions (which sizes matter, which states are confused) instead of a review of a rendered sheet.

Against a hotfix:
- The owner's revisions (Accepted lens redrawn, interior maria and crater detail on every lit disc, fade-out below the shading radius) are art with a review loop. A hotfix that is redrawn twice in 0.7.0 is churn, and interior detail at 12–16 px risks the very "can't see the phase" problem the fix targets.
- T10 (halo gauge) replaces `DrawFilling` everywhere (tree, status bar, dashboard, Moonlit, chains, Flight). That is structural and belongs in a release with owner checks at three scales, not a hotfix.

Verdict: split T9 into **T9a** (contrast, floor, rim, notch; hotfix) and **T9b** (Accepted geometry, interior detail; 0.7.0). Ship 0.5.1 = T9a + T7 + the three T5 items with the widest blast radius (tribe daily offer, festival context, character-switch leak). Everything else in T5/T6 stays in 0.6.0.

### 1.6 The data work (T2a) versus the glyph work

They are independent streams with different bottlenecks (one is polite web fetching on a dev box, the other is owner review of rendered sheets). Run T2a in the background from day one, with the rule that it never gates a tag; its findings land as T4-pattern curated corrections in whichever release is next. Bound it: Lodestone pages for the ~5,149 listed quests, wiki pages only where the Lodestone disagrees with the sheet or lacks the row, FFXIV Collect dumps (paged API, a handful of calls) for the 3,464 unique entries. Drop Garland per-quest documents except for the `reward.instance` field used to check curated duty unlocks. Rationale in §3.1.

---

## 2. Which of P1–P14 get talked about

The test we applied: would a mod pin it, would a player screenshot it, would it come up unprompted when someone asks "which quest tracker" in a Discord. "Quietly useful" means people are glad it is there and never mention it. "Dead weight" means the effort will not change whether anyone installs.

| ID | Feature | Verdict | Why |
|---|---|---|---|
| P1 | Blocker line | Quietly useful, but load-bearing | Nobody posts "my ladder row says after MSQ: The Vault". Everybody stops asking. It is also the sentence P2, P13, the Todo overlay and the diagnostic all reuse. Build first. |
| P2 | "Why not offered?" from the NPC | **Talked about** | Answers the #1 recurring question at the moment of confusion. Mods will link it in place of the wiki. The context-menu entry is a screenshot. Risk: a wrong answer is worse than the game's silence; ship with T3/T5. |
| P3 | "Clear my blues" plan | **Talked about** | This is the Google Doc and the Content Unlock wiki page, per character, in game. "Copy as checklist" output will be pasted into Discords, which is free distribution. |
| P4 | Story sidequests preset | Medium | The r/ffxivdiscussion "favourite yellow quests" crowd (88 comments) will like it; the general population will not notice. Cheap because the artwork flag exists. Keep, do not headline. |
| P5 | "Before you continue" payoff gates | **Talked about, and argued about** | The 395-point thread is this feature. It will also be the first controversy: the moment the plugin says "do Eden before 6.5", someone posts that the plugin spoils or that the pairing is wrong. Curation quality and spoiler phrasing are the whole feature (§3.3). Keep, gate behind a spoiler shield, start with the six pairs the research names and a "suggest a pair" issue template. |
| P6 | Unlock route for alts | **Talked about** in the alt threads | "Fastest route to BLU on a fresh alt" was 346 points. "Copy route" output gets pasted. Depends on P1's blocker string and the existing path code. |
| P7 | "Since you were away" | **Dead weight until there are installs** | Requires a snapshot from before the absence. Zero users today means the "then" does not exist for anyone who installs after returning, which is the population that has the problem. Defer to 1.1; the "what changed in the catalog" half is P8. |
| P8 | Patch of origin | Quietly useful, infrastructure | The chip is nice; the value is that P3, P7 and the Feature preset's "New this patch" group need `AddedIn`. Treat as data work, seed once, credit the wiki. |
| P9 | Journal text reader | Talked about by a different audience | The lore community (DRK journal thread, 292 points; PC Gamer) will love it and it is a "Garlond Tools in game" pitch. It is not a quest-tracker feature and it carries the spoiler risk in the search half. Move to 1.1 with search over completed quests only. |
| P10 | Abandoned ledger | Quietly useful, cheap, quotable | The chat line "Abandoned: [quest] (step 3 of 5)" is the kind of thing people screenshot when it saves them a duty re-run. Move earlier. |
| P11 | Seasonal now | Quietly useful | The game announces events at login; this repeats it in a place you can find later. Fine. The "history by year" list is for collectors and will be asked for by the xiv-shinies crowd. Never show an end date unless it is curated with a source URL (§3.4). |
| P12 | Export for trackers | Cheap, not a headline | A JSON file nobody imports is a JSON file. Value appears only if xiv-shinies (who asked for a plugin) or a spreadsheet template consumes it. Do it (S), reach out first, keep it clipboard-friendly. Strip identifiers (§3.2). |
| P13 | Duty Finder unlock hint | **Talked about** | The 380-comment roulette thread and "stuck on Labyrinth" threads are this. A screenshot of a padlocked duty with "Unlock: The Light of Hope, blocked by: Lv 50 MSQ" is the best single image in the plan. Fragile (addon-adjacent, breaks on patches); needs the auto-disable in §5. |
| P14 | Branching MSQ readiness | Not a feature, a deadline | Nobody will talk about it and everybody will notice if MSQ position is wrong in 8.0 week. Move the fixture and resolver work before 1.0.0. |

### 2.1 Cuts

- **Cut from 1.0.0:** P7 (to 1.1, conditional on installs), P9 (to 1.1, search restricted).
- **Cut from 0.8.0:** T15 Cards mode (decision 5 already allows the slip; make it a cut) and the animation half of T17 (chevron rotation, reveal pulse, gauge fill, live-pip breath). Motion in an ImGui overlay drawn over a game is the first thing users with reduced-motion needs and 30 fps clients complain about, and it is invisible in a screenshot. Keep the typography and keyboard shortcuts.
- **Trim T2a** as in §1.6.
- **Keep everything else**, re-sequenced.

### 2.2 The 1.0 headline (five lines, in order)

1. **"Why isn't this NPC giving me the quest?"** Right-click any NPC or any quest and get the one thing blocking it. (P1 + P2)
2. **Every unlock, in order.** A per-character plan to clear the blue quests by expansion and zone, and the missing quest named beside every locked duty in Duty Finder. (P3 + P13)
3. **Before you continue.** Spoiler-free reminders of the optional stories that pay off in the next main-scenario chapter. (P5)
4. **Moonlit: rewards you can only get from a quest**, with owned state, on any of your characters. (existing, T4/T8 polish)
5. **Alts without a spreadsheet.** Compare characters and get the ordered route to any job, duty or reward. (existing Compare + P6)

Everything else (Flight, Nearby, Todo overlay, ladders, chains, seasonal, abandoned, export, patch chips) is the second paragraph of the README, not the headline.

### 2.3 What 1.0 should mean

In Dalamud terms, 1.0 signals "ready for the official repository". If 1.0.0 ships on a custom repo with no licence, the number is wasted. Tie 1.0.0 to: LICENSE committed, README rewritten (§4.4), V2-20 submission PR opened on the testing track (or an explicit decision not to), and the P14 fixture green. Official-repo listing is the single largest organic-discovery lever available; it dwarfs anything in P1–P14.

### 2.4 Proposed re-sequence

| Release | Contents | Shareable item | Effort delta vs plan |
|---|---|---|---|
| 0.5.1 (hotfix, this week) | T9a contrast/floor/rim; T7 guard; T5 tribe daily, festival context, character-switch leak | none (owner's own use) | +0 (all pulled forward) |
| 0.6.0 | T1, T2, T3, T4, remaining T5, T6, T8; **P1, P2**; **T18 diagnostic + data stamp** (§5); **T21 README/LICENSE** | "Why not offered?" | +3 S; T2a moved out |
| 0.7.0 | T9b, T10, T11, T12; **P10, P12**; first public post | halo gauge screenshots, abandoned ledger | +2 S |
| 0.8.0 | T13, T14, T16, T17 (typography, shortcuts, no motion); **P11, P4**; **T19 spoiler shield**; **T20 addon kill switch** | Night chrome, story sidequests | −1 M (Cards), −½ M (motion), +2 S–M |
| 0.9.0 | P3, P6, P13, P8; **T22 data contribution guide** | "Clear my blues", DF unlock hint | −P1, P2, P10, P11, P12, P4 |
| 1.0.0 (before the 8.0 pre-patch) | P5, P14 fixture and per-route resolver, V2-20 submission, README final | "Before you continue", official repo | −P7, −P9, +P14 |
| 1.1 | P7, P9, V2-16 IPC, V2-19 localisation | | |
| T2a | parallel stream from day one, never gates a tag; corrections land in the next release | | |

---

## 3. Community and Square Enix risk

Ground rules the community already lives by, which every recommendation below follows: the plugin makes no network calls; nothing leaves the machine; nothing automates; nothing is shown that the game itself would not show the player at that point.

### 3.1 T2a: Lodestone at scale

- **Terms.** The Lodestone's terms of use prohibit automated access in general language. Enforcement in practice is rate-based (429s, temporary blocks). Fan sites that read character pages daily (FFXIV Collect, Lalachievements, xiv-shinies) have done so for years without sanction. A single polite pass over ~5,149 Eorzea Database pages from a developer's machine is squarely in that practice.
- **Rate.** One request every 2–3 s with a persistent on-disk cache is ~4 hours for the Lodestone and is a one-evening job. Identify the client (User-Agent naming the project and a contact URL), respect `robots.txt`, retry with back-off on 429, never parallelise. Never run it in CI or on a schedule; the plan already says reruns are free from cache, so make "the cache lives outside the repo" explicit.
- **What to commit.** Facts only: row id, name, displayed level, class text, prerequisite ids, reward ids, per-source verdict, URL. **Do not** commit quest description or objective text scraped from the Lodestone or the wiki; that text is Square Enix's and the wiki's, and a 5,000-row CSV of it in a public repo is the one thing that could draw a takedown. The plugin never needs it (P9 reads the client's own sheets at runtime).
- **Source choice.** Garland Tools is a one-person fan project whose quest documents are generated from the same game sheets the plugin reads. Fetching 5,373 per-quest JSONs from it verifies the plugin against itself and costs a volunteer bandwidth. Use it only for the curated duty-unlock check (`reward.instance`) and credit it. The Lodestone is the official reference; consolegameswiki is the human-curated one and the only place retired quests and game-skipped steps are recorded.
- **Reputational.** The reputational line is not "did you read the Lodestone" but "does the plugin phone home". It does not. Say so in the README in one sentence and keep it true: the P12 export is a file, P11 end dates are static, P8 is a seed table.
- **Credits.** Add a "Data credits" section in Help › About and in the README naming Square Enix (Lodestone), consolegameswiki (CC BY-NC-SA), Garland Tools and FFXIV Collect. Fan-site maintainers and their communities notice when a tool built on their work does not credit them; it is also the fastest way to earn a mention from those sites.

### 3.2 P12: exporting player data

- Quest completion is not sensitive, but identifiers are. The export must contain **no ContentId, no AccountId, no home-world/character name by default** (opt-in name+world for the spreadsheet use case). The Dalamud official repository rejects account identifiers in output; the panel already lists this under F-108.
- Format: `{ "tsukimichi": "<plugin version>", "data": "<data stamp>", "completed": [ids], "moonlit": { "obtained": [entry ids] } }` plus a plain newline list of ids and a "name (id)" list for spreadsheets. Document it in the repo, but the honest expectation is that nobody imports it until xiv-shinies does; open a thread with them before 0.7.0 and let their answer shape the schema.
- Do not add "upload to" anything. A plugin that pushes data to a website, however innocuous, is the thing the "plugins are cheating" contingent points at.

### 3.3 Spoilers (P5, P9, and the tree itself)

FFXIV's community treats story spoilers more seriously than any other MMO's; r/ffxiv enforces spoiler tags on quest names, and streamers blur their journals. Three exposures in this plan:

- **P5 by construction names story beats.** "Finishing Eden changes a scene in 6.5" tells a player who has not done 6.5 that Eden characters appear; "Eureka before Dawntrail's Krile arc" is a character-arc spoiler. Phrase pairs as *instruction, never reason*: "Before you continue Endwalker 6.5: finish Eden (optional)". Never name the payoff. Put the reason behind a "why? (spoiler)" click that defaults closed. Show a pair only when the milestone quest is Ready or Accepted, never on a dashboard for the whole game.
- **P9 search over all journal text** surfaces entries from quests the player has not done. Default the index to completed quests only; a "include quests I have not completed" checkbox in Settings with a spoiler warning. Journal entries for completed quests are text the game has already shown, so the reader itself is safe.
- **The tree shows every quest name** (as Quest Map does, so the norm tolerates it), and the MSQ line names the *next* quest, which is fine. But the detail pane's hero banner (0.8.0, T16) and the existing artwork tooltip show journal artwork for uncompleted quests; artwork can reveal characters and locations. Add a **spoiler shield** setting (T19, proposed): mask MSQ quest names beyond current position + N (default N = 3, "▒▒▒ (Lv 87)"), show artwork only for completed or accepted quests, and P5 reasons closed. Default the shield **on** for main scenario. This is cheap, it is the kind of thing streamers ask for, and it pre-empts the first "this plugin spoiled X" post.

### 3.4 Event-date data (P11, V2-18)

A wrong end date is the one data error that costs a player something irrecoverable (the seasonal reward). Rule: never show a date the plugin cannot attribute. `festivals.json` entries carry a source URL (the Lodestone event announcement) and the date is rendered as "announced to end 2026-11-03 (Lodestone)". With no entry, show "running now" only. Never compute "ends soon" from a guess, and do not poll (the plan already forbids it).

### 3.5 Anything that looks like automation

- Nothing in P1–P14 acts on the game. The context-menu entry (P2), the item hint and the Duty Finder hint (P13) are read-only overlays. Lifestream teleport is a button the user clicks, behind an IPC to a plugin the user chose to install. That is the accepted line; keep it, and say so in the README ("Tsukimichi never accepts, moves, teleports or skips anything on its own").
- **P13 draws beside a game addon.** Do it the way the item hint does it (a separate ImGui window positioned next to the addon, no node injection), and gate it behind the addon kill switch (§5, T20) because addon layouts change on patches.
- **Avoid "hand to Questionable" (V2-17, F-87) in any public-facing release.** The mere presence of a Questionable integration in the settings will get the plugin classed with automation in some Discords, whatever the button does. If it ships, it ships opt-in, hidden until Questionable is detected, and it is not mentioned in the README headline.
- The "official repository rejects automation" line in §5 of the plan is correct; add "and so do most community Discords" to the owner's mental model.

### 3.6 The ToS elephant

All Dalamud plugins are third-party tools under the FFXIV ToS. Square Enix's stated position is that they are prohibited and that action is taken when they see them (in screenshots, streams, reports). The community's operating rules: do not mention plugins in game, hide the UI in screenshots you post publicly, do not use them in ways that affect other players. Tsukimichi is a private, read-only overlay, which is the lowest-risk class, but the README and the first post must carry the standard disclaimer and the "hide it in screenshots" advice, or the first reply in any thread will supply it for you.

---

## 4. Naming and messaging

### 4.1 "Tsukimichi"

- The name has a real origin (Michiru Tsukikage) and the moon language is the product's whole visual identity. Keep it.
- **Collision:** *Tsukimichi: Moonlit Fantasy* is the English title of a currently running anime (from *Tsuki ga Michibiku Isekai Douchuu*). "Tsukimichi" alone is unfindable; "Tsukimichi Moonlit" is the anime's exact title. This is not a legal problem for a free plugin, but it is a discoverability and first-impression problem: a mod or a player who searches will assume a fan project.
- Mitigations: always write **"Tsukimichi — FFXIV quest tracker (Dalamud)"** in titles, the GitHub description, the manifest `Punchline`, and every post; never put "Tsukimichi" and "Moonlit" in the same sentence in public copy; add "ffxiv", "quest-tracker", "dalamud-plugin" to the GitHub topics.
- Pronunciation and meaning belong in the README's first paragraph (they already are) and in Help › About; people share names they can say.

### 4.2 "Moonlit"

- As a tab label it says nothing about its contents. The first screenshot a stranger sees has tabs "Journal · Moonlit · Flight · Characters", and only two of those are guessable. Keep the word (it is the identity) and add a plain subtitle wherever the tab is introduced: "Moonlit — rewards you can only get from a quest". The manifest description and README should always say "unique quest rewards (the Moonlit tab)", not "Moonlit".
- The 0.8.0 tab strip with moon icons and counts is the right place for a tooltip carrying that subtitle.

### 4.3 The state names

Internal names (`Ready`, `ReadyOnOtherJob`, `Accepted`, `Blocked`, `DoneThisCycle`, `Completed`, `Foreclosed`, `Unknown`) are fine. What players read must be the plain word plus the reason:

| Internal | Show | Note |
|---|---|---|
| Ready | Ready | with P1's blocker line empty |
| ReadyOnOtherJob | Ready on PLD | already done |
| Accepted | In journal · step 3 of 5 | "Accepted" reads as "the game accepted something" |
| Blocked | Not yet · after MSQ: The Vault | "Blocked" sounds like a bug when the reason is missing; P1 fixes it |
| DoneThisCycle | Done today / Done this week | never show the internal name |
| Completed | Completed | |
| Foreclosed | Locked out · chose Maelstrom / event over | "Foreclosed" is a mortgage word and reads as permanent and scary; "locked out (why)" is what players say |
| Unknown | Not checked · open Achievements once | T3 already uses "not checked" for Mount/House; make it the one word |

Moon-phase names (full, first quarter, eclipsed, veiled) are a legend, not labels: keep them in the glyph window and Help. The theme tokens (Veil, Umbra, Bruise, Night, Dusk, Mist, Eclipse) are internal; make sure "Bruise" never reaches a tooltip.

### 4.4 What a Discord mod asks before allowing a custom-repo link

In the order they ask it, with what the README must answer:

1. **Is the source public and what licence?** → LICENSE (MIT is the Dalamud norm and removes friction; AGPL signals "no closed forks" but confuses non-developers and is rarely used for plugins) and a "Source" line at the top.
2. **Is the zip built from the tag by CI, and can I see the workflow?** → "Releases are built by GitHub Actions from the tagged commit (link to workflow); `pluginmaster.json` points at the release asset." The plan's pipeline already does this; say it.
3. **Does it automate anything, move the character, skip dialogue, accept quests?** → One sentence: never.
4. **Does it send anything anywhere?** → "No network code. Nothing leaves your machine. Per-character files live in `pluginConfigs\Tsukimichi`. Export is a file you make on purpose, without identifiers."
5. **What does it hook?** → List them plainly: item tooltip (hover hint), item context menu (IContextMenu), NPC context menu (P2), Duty Finder window position (P13), server info bar. Everything else is Dalamud services.
6. **Is it on the official repo, and if not, why?** → "Custom repo while under active development; official submission planned for 1.0" (and then do it).
7. **Who maintains it and where do I report a bug?** → Author, Issues link, the diagnostic block instructions (§5), and a support channel (a GitHub Discussions page is enough; a Discord server is a commitment).
8. **Does it spoil?** → The spoiler shield (§3.3) and its default.
9. **ToS?** → The standard disclaimer.

### 4.5 The README today

It is out of date in ways that would fail the checklist: "V1 feature-complete, awaiting first in-game smoke test", "356 passing tests" (0.5.0 has 597), "Not yet in V1: … Wotsit registration" (shipped in 0.3.0), commands list omits `/tsuki zone|which|nearby|todo`, no mention of Flight, Nearby, Todo overlay, ladders, chains, Compare, item hints. Rewrite for 0.6.0 (T21, proposed): one-paragraph pitch with the five headline lines, two screenshots (after 0.7.0), install, the mod checklist answers, data credits, "what it never does", how to report, licence. Move build and layout to CONTRIBUTING.md or a docs page; a mod does not read `dotnet build`.

### 4.6 The first public post

- Not before 0.7.0 (moons), and only after the README rewrite and LICENSE.
- Where: a plugin-recommendation thread on r/ffxiv (allowed; do not make a standalone "my plugin" post first, the sub's mods treat those as self-promotion), the owner's own FC and community Discords, and the custom-repo channels of the servers that allow them. Not the goatcorp support server, which does not support custom repos.
- The post's image is one screenshot with three things: a locked NPC quest with its blocker, the Moonlit tab, and a tree with halo gauges. Its text is the five headline lines and the "never automates, never phones home" sentence.

---

## 5. Support load

### 5.1 What will generate bug reports, in order of volume

1. **"It says Ready but the NPC won't give it."** The sheet's extra accept conditions are listed, not evaluated (research §1 row 1); game-skipped steps (Up In Arms) are not modelled; T3's new requirement kinds report "not checked" when ClientStructs lacks a field. Every one of these is a "wrong state" report against the plugin's core promise.
2. **"Your totals don't match the in-game journal / the Lodestone."** T1 changes every genre total and hides 100 rows. Players compare with the journal's own completed/total. The provenance line ("Filed under X because: rule N") helps; a Help topic titled "Why my counts differ from the journal" helps more.
3. **UI scale.** T6 fixes the known set; 0.8.0 introduces a new set (custom tab strip, font handles, density modes, Classic toggle). Expect reports at 0.9×, 1.5× and on 4K.
4. **Curated overlay lag after every patch.** Eight overlay files now, eleven after this plan. Each patch: new feature quests missing from derivations, new duty unlocks, new Moonlit entries "reward data pending", chains without the new step, P5 pairs for the new expansion, P8 `AddedIn` for the new rows.
5. **Moonlit false positives.** "This minion is on the market board / the Online Store" (T4's 68 are the known set; there will be more). Confidence badges and "also on the Online Store" help; a one-click "Report this reward" that copies the entry id helps more.
6. **Addon-adjacent breakage at patches.** Item hint, context menu, DTR entry, P13. These break silently or draw in the wrong place the day a patch lands, before the owner has played.
7. **"Where did the Unlisted quests go?"** and "the plugin says I'm locked out of X forever" (Foreclosed wording; §4.3).
8. **Character and snapshot confusion** (T5 character-switch leak, stale banner, "Compare with" on a stale capture).

### 5.2 Pre-empting it (proposed tasks)

**T18 Report this quest + data stamp (S, 0.6.0).** A button in the detail pane (and on Moonlit rows) that copies a fenced diagnostic block to the clipboard:

```
Tsukimichi 0.6.0 · data 2026.09.20 · curated r14 · schema 2
Quest 67818 "Saint Sayer" (Sidequests › Gyr Abanian › The Peaks)
State: Blocked · decisive: after MSQ: The Vault (66201, not done)
Requirements: level 60/60 ok · class any ok · prev 66201 missing · festival n/a
Inputs: job WAR 62 · GC Maelstrom rank 8 · tribe – · festivals active [] · snapshot 2026-09-28T19:02Z live
Refiled: rule 3 (EventIconType 10)
```

No ContentId, no character name. The same string feeds a GitHub issue template ("paste the block"). The data stamp is `gameVersion` (already in `unique_quests.json`) plus a curated-overlay revision written by DataGen and shown in Settings › About, the status-bar tooltip and the diagnostic. Support triage becomes "which stamp" instead of "which build did you say you had".

**T19 Spoiler shield (S, 0.8.0).** As in §3.3.

**T20 Addon kill switch (S, 0.8.0, before P13).** Every addon-adjacent feature (item hint, item and NPC context menus, DTR, P13) carries a `TestedGameVersion`; on a newer game version they disable themselves and Settings shows "disabled until verified on this patch; enable anyway". One line in the changelog re-enables them per release. This turns patch-day breakage from N bug reports into zero.

**T21 README, LICENSE and Help "What's new" (S, 0.6.0, updated per release).** In-app: a "What's new in 0.6.0" card at the top of Help, populated from the manifest changelog, shown once after an upgrade (`Configuration.LastSeenVersion`). Dalamud's installer shows changelogs too, but nobody reads them there; the card is where "why did my counts change" gets answered before it is asked. Add Help topics "Why my counts differ from the journal" (T1) and "Known quirks" (P2's curated notes, surfaced as lines in the requirement checklist so users do not report them).

**T22 Curated overlay contribution path (S, 0.9.0).** A `CONTRIBUTING-data.md` with the per-file format, the `--verify` recipe, and issue templates "Missing unlock quest", "Not actually unique", "Suggest a Before-you-continue pair". Community-maintained overlays are how every long-lived plugin survives its author's patch-day availability.

**Wording changes (0.6.0, in T1/T5/T6):** "Foreclosed" → "Locked out (reason)"; "Blocked" always with P1's line; "Unknown" → "Not checked"; the Unlisted node's rename to "Retired and hidden" gets a tooltip "quests the game removed or never lists; off by default".

---

## 6. Metrics-free success criteria

No telemetry, so every criterion is something the owner, a small tester group, or the public record can observe. Recruit **three to five testers** from the owner's FC or Discord before 0.7.0 (different UI scales, at least one non-English client, at least one player with an alt and one mid-MSQ). They are the only signal until the official repo.

| Release | Criteria (all must hold before tagging) |
|---|---|
| 0.5.1 | Owner: accepting one allied-society daily changes no other daily's state; a never-seen festival's quests are not counted in any total; switching characters shows no Recent activity from the previous one. Owner reads 17/612 and 3% at IconScale 1.0 without a zoom. |
| 0.6.0 | Every genre total equals the Lodestone count in verification-report-2 §5 modulo the documented lag rows. "Unlisted" contains exactly the quests the rules leave (1) and the node is off by default. Quarrels with Squirrels shows Lv 3 and is Ready at 1. The 16 satisfaction and 17 postmoogle quests are not Ready on a character that does not qualify. Every T5 fix has a test that failed before the fix. P2: the six research threads' quests (Shallow Moor/Aloalo, Saint Sayer, 7.15 Hildibrand, Up In Arms, Warrior role quest, The Honest Truth) each return the right blocker on a tester character in that situation, or a curated note. The diagnostic block round-trips into a GitHub issue template. LICENSE present. README passes the §4.4 checklist. |
| 0.7.0 | Two testers who are not the owner name the state of eight moons in a screenshot without a legend, at their own UiScale. 17/612 and 129/195 read at a glance at 1.15/1.25 (plan's own check) and at 0.9/1.6. The first public post goes up and the first reply is not "what does it hook / does it phone home / licence?" (because the README answered it). At least one external bug report arrives with an intact diagnostic block. |
| 0.8.0 | The tutorial runs end to end in Night and in Classic. Toggling Classic and back changes no persisted setting and no column width. No tester reports a scaling regression in the first week; none reports the tab strip as "where did the tabs go". Spoiler shield: a tester who has not finished Endwalker confirms no MSQ name beyond their position is visible anywhere (tree, search, Todo, MSQ line, P5). |
| 0.9.0 | P3: a tester with 20+ undone feature quests follows the plan for one expansion and never hits a Blocked entry that the plan did not warn about. P13: on a fresh alt, five padlocked duties in Duty Finder each show the correct unlocking quest; the hint disables itself on a fake newer `TestedGameVersion`. P6's "Copy route" for Blue Mage matches the wiki's route within the level gates. At least one "Copy as checklist" output is pasted by someone else in a Discord (observed, not measured). |
| 1.0.0 | Official-repo submission PR opened (or the decision not to, written down). P14 fixture: a synthetic three-route branch resolves per-route positions and no existing chain or MSQ test regresses. P5's six pairs reviewed by two people who have finished the relevant content, phrased with no payoff named. README screenshot, headline and mod checklist final. Zero open "wrong state" issues older than one release. |
| Every release | Build 0 warnings, tests green, bug-hunter pass, changelog entry, in-app "What's new" card renders. Every curated overlay file's revision is bumped and shown in the data stamp. |

---

## 7. Recommendations by task id

| Task | Recommendation |
|---|---|
| T1 | Keep. Add Help topic "Why my counts differ from the journal"; tooltip on "Retired and hidden". Provenance line stays. |
| T2 | Keep in 0.6.0. |
| T2a | Off the critical path; parallel stream from day one, never gates a tag. Lodestone (official) + wiki on disagreement + FFXIV Collect API; drop Garland per-quest fetches except `reward.instance`. Rate ≤ 1 req / 2 s, identifying UA, robots.txt, on-disk cache kept out of the repo, never in CI. Commit facts only (no description text). Credit all four sources in Help › About and README. |
| T3 | Keep. Rename the "Unknown" state word to "Not checked" everywhere to match this task's wording. |
| T4 | Keep. Add "Report this reward" (entry id to clipboard) alongside "also on the Online Store". |
| T5 | Split: tribe daily offer, festival context, character-switch leak → 0.5.1; remainder → 0.6.0. |
| T6 | Keep in 0.6.0. Rename "Foreclosed" display to "Locked out (reason)". |
| T7 | Move to 0.5.1 (owner's ask, S, no data risk). |
| T8 | Keep. Add the Moonlit tab subtitle tooltip "rewards you can only get from a quest". |
| T9 | Split: **T9a** contrast, floor, rim, notch → 0.5.1; **T9b** Accepted geometry and interior detail → 0.7.0 with owner review at 12, 16, 23 px. |
| T10 | Keep in 0.7.0. |
| T11 | Keep. Run the 17/612 check with two non-owner testers. |
| T12 | Keep. |
| T13 | Keep. |
| T14 | Keep. On first launch after upgrade, a one-time notice "New look; Classic layout is in Settings › Display". Classic is a supported path, tested in the tutorial. |
| T15 | Cut Cards mode. Keep stripes, pills, Dense/Comfortable. |
| T16 | Keep. Artwork banner respects the spoiler shield (completed/accepted only by default). |
| T17 | Keep typography and shortcuts; cut the animations (or ship them default-off). |
| T18 (new) | Report this quest + data stamp (curated overlay revision) + issue template. 0.6.0. |
| T19 (new) | Spoiler shield: MSQ names beyond position + N masked, artwork for completed/accepted only, P5 reasons closed, P9 search over completed only. 0.8.0, default on for MSQ. |
| T20 (new) | Addon kill switch with `TestedGameVersion` for item hint, context menus, DTR, P13. 0.8.0, before P13. |
| T21 (new) | README rewrite to the §4.4 checklist; LICENSE; Help "What's new" card; data credits; ToS disclaimer; "what it never does". 0.6.0, refreshed each release. |
| T22 (new) | Curated-data contribution guide and issue templates. 0.9.0. |
| P1 | Move to 0.6.0; build before P2; its string is reused by T18, P6, P13. |
| P2 | Move to 0.6.0 (same release as T3/T5, not before). Start curated notes with the six research cases; surface notes in the requirement checklist. |
| P3 | Keep in 0.9.0; headline. "Copy as checklist" output is the distribution vehicle. |
| P4 | Move to 0.8.0 as the release's small feature. |
| P5 | Keep in 1.0.0; headline. Instruction-only phrasing, reason behind a closed "why? (spoiler)", shown only when the milestone is Ready/Accepted, six starter pairs reviewed by two finishers, "suggest a pair" template. |
| P6 | Keep in 0.9.0; headline with Compare. |
| P7 | Defer to 1.1; revisit when installs exist (needs a "then"). |
| P8 | Keep in 0.9.0 as data work; credit the wiki; not a headline. |
| P9 | Defer to 1.1; reader for completed quests, search over completed only by default. |
| P10 | Move to 0.7.0. |
| P11 | Move to 0.8.0. End dates only with a source URL, rendered "announced to end … (Lodestone)". |
| P12 | Move to 0.7.0. No identifiers by default; JSON + plain id list + "name (id)" list; talk to xiv-shinies before fixing the schema; never an upload. |
| P13 | Keep in 0.9.0; headline. Separate window beside the addon, no node injection; behind T20. |
| P14 | Move the fixture and per-route resolver into 1.0.0; 1.0.0 must ship before the 8.0 pre-patch or be the 8.0-ready release. Reserve 8.0 week for the API bump only. |
| V2-17 | If ever shipped: opt-in, hidden unless Questionable is present, never in the README headline. |
| V2-20 | Tie to 1.0.0. |
| Decision 7 | Decide now, not at V2-20. MIT unless the owner has a reason for AGPL. |
| Decision 5 | Resolve as "cut". |
| Decision 4 | Keep Night default with Classic, plus the one-time notice above. |
