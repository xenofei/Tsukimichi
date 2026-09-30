# Tsukimichi — V3 Feature Plan (proposal)

Date: 2026-09-28
Status: **v3.3, 2026-09-28. Approved in principle by the owner with revisions; reviewed by a six-member panel (§2a); execution begins with release 0.5.1.**

Owner's revisions (2026-09-28): the Accepted glyph reads too much like the full moon; the moons need interior detail in the manner of artistic moon illustrations; the quest path section needs an artistic redesign inspired by other games and applications; the database verification must cover every quest, not a sample, with references; everything else in the design stands.
Baseline: 0.5.0 released (597 tests, 0 warnings).

## 0. Inputs

Seven reports were produced for this plan; each is in the repo and is the evidence for the tasks below.

| Report | Path | What it answers |
|---|---|---|
| Unlisted bucket analysis | docs/data/unlisted-report.md | What the 180 Unlisted quests are and where each belongs |
| Database verification, second pass | docs/data/verification-report-2.md | Inaccuracies against the Lodestone (official), xivapi, Garland Tools, consolegameswiki, FFXIV Collect |
| Bug hunt, Core/Game/Plugin | docs/review/bug-hunt-0.5.0-core.md | Wrong-state, threading, perf and persistence defects |
| Bug hunt, UI layer | docs/review/bug-hunt-0.5.0-ui.md | ImGui, scaling, guards, missing hover affordances |
| Player research since Dawntrail | docs/research/player-gripes-2026.md | Ranked grievances 2024-07-02 → 2026-09-28 with links; 14 proposals |
| UI/UX revamp proposal + mockup | docs/design/ui-revamp-proposal.md, docs/design/mockups/main-window.html | Structure, tokens, micro-interactions, accessibility, increments |
| Vector glyph redesign | docs/design/glyphs/{proposal.md, glyphs-v2.svg, glyphs-v2.png, glyphs-v2-zoom.png, imgui-notes.md} | Why the moons fail at 12–20 px; the halo gauge; exact geometry |

Interpretation note: the owner's time bound "no later than the release of Dawnbreaker" was read as **Dawntrail (patch 7.0, 2 July 2024)**; the research window runs from that date to today.

## 1. Findings that drive the plan

### 1.1 Unlisted is not one thing (180 quests)
- **99 retired rows**: quests the game removed (5.3 ARR pruning, 5.5 Summoner rework, 6.3 Crystal Tower, and others). All sit on a placeholder issuer NPC (`IssuerStart == 1034221`) or carry the sheet's unnamed "hidden" bool; 44 have a same-name replacement row that is listed. These should stay out of the tree entirely and never count toward totals; a completed one still shows as Completed when revealed.
- **58 quasi-quests** (`EventIconType == 10`): talk-once unlocks (Leves of…, Sights of…, class intro "So You Want to Be a…", Gold Saucer, Squadron, Eureka entry, Palace 51+, New Game+, Variant dungeons, Diadem). They are real feature unlocks and belong in the class/job or regional genres and in Feature Unlocks.
- **23 hidden chain steps**: YoRHa, Resistance Weapons, Beastmaster, Firmament phases, Anima, Pilgrim's Traverse, Island visitation, Duty Recorder, Triple Triad tournament. They belong to the genre of their nearest listed prerequisite or successor.
- Seven deterministic sheet rules file **80 into existing nodes** and leave **100 hidden as retired** (rule outcomes in the report's appendix A). One quest (The New Frontier) has no signal and stays Unlisted. No new journal category is required; the Unlisted node becomes a small "Removed from the game" bucket that is off by default.
- Bonus: five *listed* quests are retired too (two Crystal Tower, one Mor Dhonan, two Return to Ivalice) and inflate three genre totals; three more 3.05 sidequests were removed without any sheet marker and need a curated list.

### 1.2 Database inaccuracies (all confirmed by two sources or the sheet)
1. Displayed level ignores `QuestLevelOffset` on 215 quests (Lodestone: Quarrels with Squirrels is Lv 3, the catalog shows 1). The raw value stays the acceptance gate.
2. Custom-delivery satisfaction rank is not evaluated (16 quests show Ready too early).
3. Delivery Moogle carrier level is not evaluated (17 postmoogle quests show Ready to every level-50 character).
4. Seasonal quests ignore the festival **phase** (`FestivalBegin/End`); later chapters of a phased event show Ready on day one.
5. Curated duty unlock for Asphodelos: The First Circle points at the wrong quest (70011 → should be 70012).
6. Unlisted quests render the journal path " › Sephiroth Missions" because genre row 0 points at category row 0.
7. 68 unique-reward entries (25 minions, 21 emotes, 11 mounts, 5 bardings, 4 rolls, 2 ornaments) are past seasonal rewards that the Online Store re-sells; the sheets cannot know that, so a curated overlay is needed and Moonlit should say "also on the Online Store".
8. The Lodestone (official) lists 5,149 quests vs the catalog's 5,193; every category matches except by the newest rows the Lodestone lags on, the retired rows above, and promo rows. No other structural drift.

### 1.3 Bugs (confirmed)
Core/Game: tribe "daily offer" is read from the accepted-dailies array, so accepting one daily flips ~566 other dailies to Blocked; festival context is never wired into the evaluator, so seasonal quests of unseen events are Blocked forever and inflate every total; a character switch without a logout gap leaks Recent activity and re-announces notices; plus first-pass-after-login guard, sticky DoneThisCycle, class-pinned quests on the parent job, festival-before-journal ordering (plausible, need in-game checks).
UI: main-window minimum size ignores UiScale (table collapses at 1.5×); three combos render unscaled; Todo overlay and Nearby window scale glyphs but not text; dashboard pins are stale for up to a minute and read from disk on the draw thread; Reveal into a collapsed tree shows nothing; Moonlit selection highlight desyncs; several icons and moons have no tooltip; and the owner's two items: Mark-as-unique has no guard, and "Not unique (hide)" removes the row so its own Restore becomes unreachable.

### 1.4 What players ask for (2024-07 → 2026-09, distinct threads)
Hidden prerequisites with no explanation (16), overwhelmed by blue quests with no order (12), "is this sidequest worth doing" (13), alt unlock lists (12), returning-player lostness (11), optional content that gates story payoffs (7), seasonal quests missed (6), journal text for completed quests (4), abandoned quests (4). Game changes since 7.0 that narrow scope: the Duty List now shows ten quests (7.1), allowances and society maps are native (7.3), 8.0 Evercold (January 2027) brings a three-route MSQ and 8.1 native auto-travel, so navigation features are not worth building and "what and why" remains the differentiator.

### 1.5 Design verdicts
- The moon problem is measured: the tree moon is a 7 px radius disc whose dark side (#2C334A) is 1.47:1 against the window, so the eye never sees the whole circle, and the terminator has no floor, so 17/612 is a 0.4 px sliver.
- Revised after the owner's review (glyphs v2.1): Accepted is a 60 % gibbous with a Silver rim and a Night seal dot, distinct from the full moon at 12 px and in greyscale; large moons carry maria, craters and a terminator glow; Foreclosed carries a diagonal bar; the unlit disc is Shadow. The Path section becomes a star chart (docs/design/path-section-proposal.md).
- Recommendation (both designers agree on the diagnosis; geometry from the vector proposal, verified at 16 px in the rendered sheet): **halo gauge for progress** (Veil track ring, gold arc from 12 o'clock with a visual floor so 3 % reads as a pip, a small filling-moon core) and **refined solid-disc state moons** (Shadow dark side, crisp rim, shading only at ≥ 9 px, interior detail at ≥ 12 px). The two families differ in silhouette, so a Ready quest never reads as a 50 % node. Tree glyphs grow to a 24 px halo in 30 px rows at default scales.
- Main window: a Night chrome with a 36 px toolbar (search pill, presets as a segmented control, Filters badge, character chip), custom tab strip with moon icons and counts, gauge rows in the tree, a state stripe on every table row, and the detail pane as a card stack with a hero banner, requirement checklist and a sticky action bar; a "Follow Dalamud colours" mapping keeps the host palette for those who want it. Token set with computed contrast; Dusk demoted from body text on cards because it fails AA there.

## 2. Decisions

Recorded 2026-09-28 from the owner: 1 approved (seven Unlisted rules); 2 Shift (with a hold-to-confirm alternative, see T7); 3 vertical tab strip as mocked; 4 whole-window Night chrome; 5 Cards mode; 6 order as proposed; 7 license still open.

Changed by the review panel (owner to veto if wanted): decision 4's "Classic layout" toggle becomes a "Follow Dalamud colours" token mapping so there is one layout to maintain, not two; decision 5's Cards mode is cut because journal banners are shared per genre, so cards would show identical thumbnails; decision 6's order is re-sequenced so every release carries something a player can screenshot (§4).

Answered by the owner on 2026-09-28:
- **7 License: MIT** (T21 ships the LICENSE file in 0.6.0).
- **8 Official repository: not pursued.** The goal is the best possible questing experience and working alongside other plugins, with or without the official repository. V2-20 is dropped; the interop items move up: the IPC provider (V2-16) joins 0.9.0 and the Questionable cross-check (V2-17) joins 1.0.0.
- **9 Interior moon detail is size-gated.** Maria and craters draw from a 12 px radius (24 px box) upward, so they appear on the detail-pane header moon, the Help legend, the folded-path tooltip, the Tonight card and the glyph window, not on 11 px row moons, which stay a clean gradient so they remain crisp. If the owner wants detail on row moons too, the tree row height must rise to about 34 px.
- **10 Pins: settled.** Pins are already stored per character (user/pins.json is keyed by content id), so P6's "Pin all" pins for the viewed character; no decision is needed.

Found after 0.5.1: the installer shows no icon because the manifest inside the zip carries `IconUrl: null` (the csproj sets no IconUrl and ships icon.png at the package root rather than images/icon.png); fixed in T21. A 50 ms hitch on the poller's first capture after load is added to T5b.

## 2a. Review panel outcomes

Six reviewers critiqued the proposal (reports in docs/review/panel/): a five-persona player survey group, a Dalamud plugin developer, a game UX designer from another studio, a QA and data engineer, an accessibility specialist, and a product and community lead; a final fresh-eyes review then checked the revised plan for contradictions. What changed:

| Theme | Raised by | Change |
|---|---|---|
| No CI; tag workflow can ship a no-op release; snapshot schema bump strands characters on rollback | QA, Dalamud dev | New T0 engineering floor before any feature; T3 is additive at schema v1 |
| Nothing shareable until 0.9.0; 0.6.0 too large to diagnose | Product, QA, Dalamud dev, players | Hotfix 0.5.1 first; 0.6.0 split into 0.6.0 / 0.6.1 / 0.6.2; P1 pulled into 0.6.0 and P2 into 0.6.2 (it must not answer before T3 models every requirement); P10 and P12 into 0.7.0; P11 and P4 into 0.8.0 |
| Requirement fields are readable | Dalamud dev | T3 loses its "not checked" hedge: `SatisfactionSupplyManager.SatisfactionRanks`, `IPlayerState.DeliveryLevel`, `GameMain.Festival.Phase` |
| Spoilers everywhere: MSQ names ahead of position, artwork for undone quests, P5 wording, P9 search | Players, product, UX | New T19 spoiler shield in 0.7.0; P5 phrased instruction-only; P9 search limited to completed quests |
| Colour-only states under colour blindness; halo track too faint; Shift chord excludes some users; right-click-only actions; hotkeys collide with hotbars | Accessibility | Folded into the glyph revision (bar on Foreclosed, seal on Accepted, Shadow disc, thicker track); hold-to-confirm; action bar and "…" buttons; only Ctrl+F and Esc bound by default; a high-contrast glyph palette is deferred to 1.1 |
| Gold used for everything; Status column truncates the blocker; no "what now" surface; vocabulary drift | UX, players | Gold reserved for actionable items and the selection rule; Status is the stretch column; "Tonight" card when nothing is selected; one display name per state (T23) |
| Returning-player card has no "then" for a fresh install | Players, product | P7 gains an "I last played in patch X" picker and depends on P8; moved to 1.1 |
| Full verification would gate releases for a week and lean on a derived source | Product, QA | T2a keeps the owner's "every quest" scope but runs as a parallel stream that never blocks a tag, in its own tool project, against the Lodestone, the wiki and FFXIV Collect; Garland only for duty-unlock instances; its first complete CSV ships with 0.7.0 |
| Support load: wrong-state reports with no diagnostic; stale README; no license | Product | T18 "Report this quest" diagnostic and data stamp; T21 README, LICENSE, in-app "What's new" |
| Curated data has no cross-file invariants; `otherSource` has no reader | QA | T4 adds a structured `OtherSources` array and invariant tests; golden file for T1 |
| Patch day breakage of hooks | Product, Dalamud dev | T20 kill switch: item hint, context menus, DTR and P13 disable themselves on an untested game version, with an "enable anyway" override |

Declined: dropping T2a (the owner asked for every quest verified); keeping both Classic and Night layouts (one layout, themed); Garland per-quest fetches as evidence (derived from the same sheets the plugin reads); a high-contrast glyph palette now (1.1).

## 3. Task list

Effort: S ≈ a day of agent work, M ≈ a few days, L ≈ a week plus in-game testing. Every task ends with: build with warnings as errors, tests green in CI, a bug-hunter review pass, a CHANGELOG entry. Every release ends with tag → Actions → pluginmaster → in-game update; the owner verifies in game against that release's gate (§4).

### Release 0.5.1 — engineering floor and hotfix

Order: T0 → T5a → T9a → T7.

**T0 Engineering floor** (S)
- `.github/workflows/ci.yml`: on push and pull request, restore the Dalamud dev libs the way release.yml does, `dotnet build Tsukimichi.sln -c Release -warnaserror`, `dotnet test`, and the curated-data invariants tests (T4 adds them; the step exists from here).
- `release.yml`: fail the tag build when the csproj `Version` differs from the tag; fail when CHANGELOG.md has no section for that version; pin actions by SHA; `concurrency`; retry the pluginmaster push; `workflow_dispatch` to republish.
- The new guards are exercised on a throwaway `v0.5.1-rc1` tag on a scratch branch (workflow run must fail on a deliberate version mismatch, then pass) before the real tag.
- Freeze a real schema-v1 snapshot fixture under Tsukimichi.Tests/Fixtures before any model change; DataGen `--dump-catalog` producing a gzipped catalog fixture so pure-record tests run in CI, plus one Lumina-backed test that the fixture matches the live sheets.

**T5a Widest-blast state bugs** (S)
- Tribe daily offer no longer read from the accepted-dailies array (`DailyQuestWork` holds only accepted dailies; the offer is never stored); in-progress dailies resolve Accepted (GameStateReader.cs:143–152, 281–303; StatePoller.cs:219; RequirementEvaluator.cs:131–135).
- Festival context wired into the evaluation context in `SessionState.SetCatalog` so quests of never-seen events resolve per the rules. Totals are not changed here (that is T5b, with its What's-new line).
- `recentEvents` cleared when `LiveContentId` changes (StatePoller.cs:246–258).
- Only what the three tests need moves into Core: `EvalContextBuilder` and `RecentEventsTracker`, each with a test that reproduces the failing input from docs/review/bug-hunt-0.5.0-core.md.

**T9a Glyph contrast hotfix** (S)
- MoonGlyph only, geometry exactly as docs/design/glyphs/proposal.md v2.1 §3.1 for the parts that need no new art: unlit disc Shadow #3A4363 with rim `clamp(0.12r, 1.5, 3)` px, terminator floor so a 3 % node draws a visible crescent, Foreclosed diagonal Eclipse bar, Ready below r 9 gets a 1 px ring instead of the invisible glow; new tokens `Shadow` and `VeilLine`. No layout change.
- Tests: MoonGeometry floor (fraction 0.03 yields a lit width ≥ 1.5 px at r 7); GlyphDebugWindow gains greyscale, deuteranopia, protanopia and tritanopia simulation toggles (matrix in docs/review/panel/accessibility.md) so the owner can check every state without a legend.

**T7 Mark-as-unique guard and revert** (S)
- "Mark as unique…" and "Not unique (hide)" both open the confirm popup; its confirm button is enabled by Shift held or by a 600 ms hold-to-confirm arc drawn with the existing `PathArcTo` in MoonGlyph (a countdown text instead of the arc when `Configuration.ReduceMotion` is on; that setting is introduced here with default false; T12 later changes its default to the OS animation setting); the note field is auto-focused and Enter confirms; an undo line appears for 8 s after either verdict.
- The Yours confidence filter also lists quests hidden by "Not unique" as struck-through rows whose context menu offers Restore; Settings › Data gains "Your Moonlit verdicts (N)" with per-row Restore and Restore all.
- Tests: UniqueRewardCatalog exposes hidden overrides; OverridesFile round trip; a `ConfirmGate` helper in Core unit-tested for chord, hold, release-before-done and cancel.

### Release 0.6.0 — accuracy, guards, first shareable answers

Order: T2 → T23 → P1 → T18 → T4 → T8 → T5b → T6 → T21. T2a starts in parallel and never gates the tag. The tag waits for decision 7.

**T2 Displayed level with offset** (S) — `DisplayLevel = Level + LevelOffset` on QuestRecord; used by TablePane.cs:295/364, DetailPane.cs:872, DiscoveryWindow.cs:468, the level filter and sort in QuestQuery.cs:441, the Todo and Nearby hints; the evaluator keeps Level. Tests: Quarrels with Squirrels displays 3 and is Ready at 1; sort by level orders by the display value.

**T23 Vocabulary** (S) — one display name per state everywhere, with the poetic names as glyph subtitles: Ready, Ready on another job, In journal (Accepted), Blocked (always with the blocker), Done today / this week (by interval), Completed, Locked out (reason), Not checked. "Feature Unlocks" node → "Unlock quests", preset "Features" → "Unlocks", "Next step" column → "Status", "Presets" → "Quick views", "Level band" → "My level", "Tribal" → "Allied Society", Moonlit tab subtitle "rewards only a quest gives". A glossary in docs/glossary.md and a string-lint test that no other spelling of a state appears in Strings*.cs.

**P1 Blocker line** (S) — one `BlockerText(evaluation)` function in Core (T23 names) reused by ladders, Nearby, Todo, the level-up nudge, the Status column, and later P2, P6, P13 and T18: the single decisive blocker ("after MSQ: The Vault", "Rank: Trusted with the Pelupelu", "Lv 80"). The Status column becomes the stretch column now and never ellipsises the state word (Exp and Rewards hide first). Tests: BlockerText for each requirement kind; the state word is the first token of every Status string.

**T18 Report this quest** (S) — a button in the detail pane and `/tsuki report` copy a fenced diagnostic block (plugin version, game version, unique_quests.json game version, curated revision, schema, quest id, display state, BlockerText, per-requirement verdicts and inputs, filing rule; no content id); the data version stamp shows in About and the status bar tooltip; a one-line warning in About when unique_quests.json was generated for a different game version; GitHub issue templates (bug, wrong state, data). Test: the block serialises the same for a fixture evaluation.

**T4 Curated data corrections** (S) — duty_unlocks.json 70011 → 70012 (70011 stays an unlock quest only if the EventIconType rule keeps it; the test states which); new curated/online_store.json (68 item ids, note and evidence URL per entry); `UniqueRewardEntry.OtherSources` as a structured array (loader tolerant when absent) rendered by MoonlitPane, RewardTooltip and the hover hint as "Store only", with a Moonlit filter "Hide store re-sells"; feature_quests.json becomes derived by DataGen, not hand-maintained; `Verifier.KnownAnswers` pins 70012 → 808 and 70011 ↛ 808; a curated-invariants test project (schema, ids exist in the sheet, cross-file consistency, no dangling 70011); `tools/regen.ps1` and docs/data/DATA-VERSION.md; DataGen reports drop timestamps so regenerations diff cleanly.

**T8 Hover affordances** (S) — surfaces: Moonlit reward icons (RewardTooltip with the store line), the icon-less stand-in becomes a veiled moon, dashboard job icons, detail-pane path and unlock moons, confidence badges, chip counts, the DTR entry; every state tooltip carries the plain name plus its shape hint ("Blocked · new moon, silver ring"); a sweep replaces `IsMouseHoveringRect` tooltips with item-based ones. Test: a table in Tsukimichi.Tests of state → tooltip text.

**T5b Remaining Core/Game fixes** (S) — out-of-season quests leave Totals the way Foreclosed does (What's-new line and the Help topic in T21 explain the count change); first-pass-after-login guard (`LoginReadiness` in Core: an all-zero completion mask with an empty journal is not ready); `SnapshotDiff.SameSet` without allocation; the poller's first capture after load no longer hitches the frame (50 ms seen: spread the first evaluation over ticks or run the catalog-wide resolve off the framework thread and publish the result); help text lists `search` and `settings`; `ConfigRecovery` copies a corrupt config aside before resetting; Wotsit partial-batch retry; DeleteAllData/ForgetCharacter keep poller memory and disk in step; the poller re-diffs after a catalog retry; `PlayerState.MaxLevel`/`MaxExpansion` wired (closes DRAFT-NEEDED E); parent-job admission from `QuestWork.AcceptClassJob`. Owner checks in game before any code changes: sticky DoneThisCycle after reset; festival read empty during zone load. Tests: one per fix as listed in docs/review/panel/qa-data-engineer.md §2.2.

**T6 UI fixes** (M) — window minimum size scales with UiScale (`ScaleMetrics.MinWindowSize` with a test); the three unscaled combos; Todo overlay and Nearby apply the font scale; dashboard pins keyed on the runner's pins version; Reveal opens the ancestor chain and scrolls; Moonlit selection from `ui.SelectedRowId`; path/unlock/chain clicks through Reveal; banner cropped not squashed; stable "Not started" node id; PushIndent through Px; character combo ids include the content id; sort restore; table column widths re-asserted when IconScale changes; Todo overlay minimum opacity 0.6 with a 1 px Night text shadow below 0.9; `UiMetrics.MinTarget = max(Px(26), 24)`. Owner gate: every window at UiScale 0.9, 1.15 and 1.6 with IconScale 0.8 and 2.0 shows no collapsed column, unscaled popup or clipped text.

**T21 Public-facing hygiene** (S) — plugin icon: `<IconUrl>` in the csproj pointing at the raw assets/icon.png and the icon packaged as images/icon.png so the installer shows it; README rewritten (current features, all commands, install, the hooks the plugin uses, "nothing leaves the machine", ToS note, license, issue path), LICENSE (MIT, copyright Michiru Tsukikage), in-app "What's new" card driven by `LastSeenVersion`, Help topics "Why my counts differ from the journal" and "Known quirks", data credits (Square Enix, consolegameswiki CC BY-NC-SA, FFXIV Collect, Garland Tools).

**T2a Full-catalog verification with references** (L, parallel stream) — new `tools/Tsukimichi.Verify` console: every one of the 5,373 named quests against the Lodestone Eorzea Database (official: name, level, class, prerequisites, rewards) for every quest it lists, consolegameswiki where the Lodestone lacks the row or disagrees, and Garland Tools only for `reward.instance` on curated duty unlocks; every unique_quests.json entry against the full FFXIV Collect dumps and the wiki item acquisition line. Rate ≤ 1 request per 2 s with an identifying user agent and robots.txt honoured; resumable on-disk cache outside the repo keyed by game version; `--offline` and `--since` diff modes. Output committed: docs/data/quest-verification.csv (long form: rowId, name, fact, catalog value, source, source value, source URL, verdict ∈ {match, catalogWrong, sourceWrong, sourceLagging, notModeled, notListed, ambiguous, unresolved}, reason, fixedIn), a derived one-row-per-quest summary, docs/data/reward-verification.csv, docs/data/verification-full.md, docs/data/verification-allowlist.json (each entry with `until`), and sourced end dates for curated/festivals.json (P11 uses them) plus the `SystemReward[1]` coverage list. Facts only are committed (ids, levels, counts, URLs), never quest or description text. **Acceptance: every quest has a row; no row is `unresolved` or `catalogWrong` outside the allowlist; the first complete CSV ships with 0.7.0** and the fixes it finds go to T2/T4 or data-only patch releases.

### Release 0.6.1 — Unlisted refiling

**T1 Unlisted refiling** (M) — `JournalRefiler` in CatalogMapper implementing rules 1–7 from docs/data/unlisted-report.md §4 (rule 6 tie: stays Unlisted with `RefiledFrom == 7`); `QuestRecord.IsRetired` and `RefiledFrom`; `IsRetired` honoured centrally (TreeCounts, QuestQuery, Wotsit registration, search, DTR, Nearby, MsqProgress, Compare); `EventIconType == 10` joins Unlock quests; curated retired_quests.json (the 3 removed 3.05 sidequests and the 5 listed retired rows) and refile_overrides.json (Eureka trio → The Forbidden Land, Eureka; Seeing the Cieldalaes → G107; The New Frontier 67752 → G117), each entry with note and evidence URL; the node renamed "Removed from the game", off by default; detail pane line "Filed under X (rule N)"; runtime `JournalFiling = Refiled | Legacy` setting as the in-field rollback.
- Tests: golden file docs/data/refile-expected.csv (row id, rule, genre) diffed row by row; every refiled quest in exactly one node; retired never counted; a retired-twin test (44 same-name pairs, a character with only the old id flagged still shows Completed on reveal); the `Unknown12` column pinned by a data test since Lumina renumbers unknown columns.
- Gate: genre totals equal the Lodestone counts modulo the documented lag list; "Removed from the game" holds exactly the retired rows and Unlisted holds none.

### Release 0.6.2 — requirement kinds and "why not offered?"

**T3 Missing requirement kinds** (M) — map `SatisfactionNpc/Level`, `DeliveryQuest`, `FestivalBegin/End`; Requirements gain `CustomDeliveryRank` and `CarrierLevel`; the seasonal check becomes id active and Begin ≤ Phase ≤ End, applied in StateResolver rule 3 as well; GameStateReader reads `SatisfactionSupplyManager.Instance()->SatisfactionRanks` (12 slots, index RowId − 1, bounds-checked), `IPlayerState.DeliveryLevel`, and festival (Id, Phase) pairs from whichever of GameMain.ActiveFestivals, PlayerState.ActiveFestivalIds/Phases and EventFramework.Festivals the one-event log shows to be authoritative (the log runs during the next live event; All Saints' Wake in late October if none is running sooner, and 0.6.2 tags after it); snapshot fields are additive at schema v1 (`festivalPhases`, `satisfactionRanks`, `carrierLevel`, empty defaults) so older builds still read the file; SnapshotDiff diffs (id, phase) and the new fields.
- Tests: the 16 satisfaction quests Blocked at rank 3 and Ready at 4; postmoogle quests Blocked below carrier level; Hatching-tide 2014 chapter 2 Blocked at phase 1; the frozen v1 fixture round-trips unchanged.

**P2 "Why not offered?"** (S) — IContextMenu entry on a targeted EventNpc (`MenuTargetDefault.TargetObject`, filtered like DiscoveryCommands.cs:63–69) "Tsukimichi: quests here (N)" opening the window on that NPC's quests with BlockerText; `/tsuki why <quest>` prints blockers to chat; curated quirk notes (Up In Arms optional with the Zenith; "Bloodsworn" reads "Allied" after 7.0); no target content id is ever stored. Gate: the quests from the six research threads in docs/research/player-gripes-2026.md §3 P1–P2 return the right blocker.

### Release 0.7.0 — Journal revamp

Order: T9b → T10 → T11 → T12 → T19 → P10 → P12.

**T9b Glyph art pass** (M) — the revised state set from docs/design/glyphs/proposal.md v2.1: Accepted as the 60 % "sealed early gibbous" with Silver rim and Night seal dot; interior detail (three maria, three craters, terminator glow band, rim vignette) from r ≥ 12 within a 12-primitive budget, so it appears on the detail-pane header moon (r 16), the Help legend, the folded-path tooltip (r 16), the Tonight card and the glyph window (decision 9); highlight arc ≥ 16; tokens `Umbra`, `MoonHigh/Deep`, `SilverHigh/Deep`; moons are used only for quest state and completion fraction (requirement marks, obtained marks and the live pip get check, cross and pip marks). Gate: two non-owner testers name all eight states from the glyph window without a legend; the greyscale sheet row shows all eight distinct.

**T10 Halo gauge** (M) — Core/Ui/GaugeGeometry.cs (arc endpoints, floor ε = max(0.06, (stroke + 1.5)/(2π·0.80R)), core radius, caps) with the QA tests from docs/review/panel/qa-data-engineer.md §2.2 except the core-radius one, which becomes "R 11 → 0; R 12 → 0.61 R"; `MoonGlyph.DrawHalo`: track VeilLine full alpha (Dusk at 0.8 on cards), stroke max(2, 0.18R), core (Shadow + Dusk rim) from R ≥ 12; for R < 12 track + arc with the number beside; for R < 8 the number only; replaces DrawFilling for progress everywhere.

**T11 Tree rows** (M) — `TreeGlyphRadius(L) = clamp(0.5·L·IconScale, 12, 18)` so the halo box is never under 24 px; tree rows are at least 30 px in every density; rows via `ImGuiTreeNodeFlags.FramePadding` (keeping TreeNodeEx for keyboard nav); section rows with a VeilLine rule; an expansion pill only on a node that spans one expansion; "done / total" in Dusk with the 44×3 px mini bar, percentage in the tooltip; hover wash; selected row Veil wash plus a 2 px Moon left rule (the one gold element of a non-Ready row); complete nodes MoonDim without glow; a Ready count badge only when Ready > 0; tab badge = Ready count. Gate: 17/612 and 129/195 read at a glance at the default scales.

**T12 Status bar and density** (S) — halo gauge, static live pip, MSQ pill, right-aligned version in Dusk; `Density` Dense / Comfortable (default) affects table rows (24 / 32 px) only; `ReduceMotion` default now follows the OS animation setting; owner gate at UiScale 0.9 and 1.6 in greyscale (Windows colour filter).

**T19 Spoiler shield** (M) — a `SpoilerMask` predicate in Core: MSQ names beyond the character's position + N (default 3, on by default for MSQ) render as "Main scenario quest (Lv 83)" everywhere names print (tree, table, detail, status, Todo, chat links, Compare, Wotsit, the diagnostic); journal artwork shown only for accepted or completed quests; per-character override and a Settings toggle; "Sprout mode" quick view: sections beyond the character's MSQ expansion collapse to counts and the table shows "N quests in your reach". Tests: the predicate on a fixture; gate: a mid-Endwalker tester sees no MSQ name beyond position.

**P10 Abandoned ledger** (S) — `characters/<ContentId>.abandoned.json` (quest id, when, step reached) written from the diff's Abandoned events; an "Abandoned (N)" dashboard section and filter chip, each row with giver, Flag, Teleport, Reveal; a chat line "Abandoned: [quest] (step 3 of 5)" at the moment it happens, under the shared notice budget.

**P12 Export** (S) — Settings and `/tsuki export` write completed quest ids and names, and Moonlit obtained state (mounts, minions, emotes, orchestrion, bardings, cards, hairstyles, fashion, ornaments) as JSON and CSV; content id, account id and character name are omitted unless "include character name" is ticked; never an upload; docs/export-format.md.

### Release 0.8.0 — Main interface revamp

Order: T13 → overlay and Nearby on Chrome → T16 → T15 → T14 → T20 → T17 → P11 → P4. Gate for the release: the tutorial runs end to end with "Follow Dalamud colours" off and on; no scaling regression at 0.9 / 1.15 / 1.6; keyboard-only tutorial completes; the Todo overlay reads over Coerthas snow at opacity 0.6.

**T13 Chrome helpers** (M) — Ui/Chrome.cs (BeginCard/EndCard, Pill, Chip, Badge, Hairline, Lift, SegmentedControl with an explicit "All" state, IconButtonRound at ≥ MinTarget, Scrim, ImageCover with centre-crop UVs, FocusRing, OutlinedText, HoldButton reusing T7's ConfirmGate), Ui/Motion.cs (event-driven only: Lerp/Pulse, ReduceMotion gate, key pruning, skipped while scrolling), `Theme.PushNightWindow` applied in Window.PreDraw with popups and tooltips explicitly restyled and BgAlpha untouched, `Theme.FollowDalamudColours` mapping; the Todo overlay and Nearby window move onto Chrome with outlined Silver text, Mist hints, 14 px glyph floor, Compact mode, a "…" button per row, flag on double-click or menu (never single click), and "Locked = click-through". Tests: Motion easing, ImageCover UVs, ConfirmGate reuse.

**T16 Detail pane** (M) — Core first: `PathFinder.Alternatives` (up to 3 per Any-join with `RemainingCount`), `PathRow.Alternative` kind, the fold rule "the completed step immediately before a non-completed step never folds", star-field points seeded by `expansionId * 7919 + rowCount`, each with tests. Then the pane: hero ≤ 96 px with scrim and state pill, Requirements first with a single next-step marker, chain shown once, reward tiles with RewardTooltip, the Path section as the star chart from docs/design/path-section-proposal.md §4 (its Cards density row does not apply): sky bands per expansion, walked gold thread, dashed silver future, folded-run beads, Any-join ghost nodes, target halo, Unlocks-next comb, jump-to-target pill, minimap only when scrolling; giver card; a labelled primary action plus Pin in the action bar; plain provenance line; EmptyState with heading, action and offending-filter chips; the "Tonight" card when nothing is selected (Ready count, next MSQ, live events, from the Todo row model). Gate: Hildibrand's 57-quest line scrolls with the target at 60 %.

**T15 Table** (M) — state stripe pattern-encoded (solid / two segments / 60 % / none / dashed / dotted) so it survives greyscale; hover lift from the previous frame's hovered row; selection ring; header restyle; level and expansion pills; job icons; Dense/Comfortable. Test: stripe pattern per state; the Status column rule from P1 holds.

**T14 Toolbar and tab strip** (M) — 36 px toolbar (search pill, Quick views segmented control with "All", Filters badge that counts engaged filters, character chip with job icon and static pip, round icon buttons), scope shown as the first chip; vertical TabStrip with moon icons and Ready-count badges in its own fixed layout column (the tree column keeps its width) recording `UiRects.Tabs` as the union of the rail buttons; the toolbar reflows to two rows below 1000 px of available width; the default window size fits the viewport at UiScale 1.6; tutorial rewritten in three chapters (Find / Read with a legend of all eight states / Beyond), ≤ 35 words per step, Enter/arrows/Esc, "Later" vs "Don't offer again", card scaled by UiScale, FilterPanelOpen restored after the tour.

**T20 Addon kill switch** (S) — `TestedGameVersion` in the manifest; item hint, item and NPC context menus, DTR and (later) the Duty Finder hint disable themselves with a one-line notice on an untested game version, with an "enable anyway" checkbox in Settings › Integrations, until the owner ships a bump.

**T17 Motion, typography, keyboard** (S) — chevron rotation, reveal pulse, gauge fill on change only; game font handles per UiScale bucket with the AddText fallback; only Ctrl+F and Esc bound by default, other shortcuts configurable and off; Menu key / Shift+F10 opens the row menu; every right-click action reachable from the action bar or a "…" button (checklist per pane in the task).

**P11 Seasonal now** (S) — "Event quests running now" in the Todo overlay and dashboard from the evaluator's Active flag, each with state and giver; "announced to end <date> (Lodestone)" only when T2a's sourced date exists; per-character seasonal history by year; login notice under the shared budget.

**P4 Story sidequests preset** (S–M) — quests with journal artwork plus per-zone side-story chains derived from prerequisite links among non-unlock sidequests; a "Story sidequests" quick view, a book badge on rows, chain N of M in the detail pane. Test: chain derivation on a fixture zone.

### Release 0.9.0 — the ordered answers

**V2-16 IPC provider** (M) — `Tsukimichi.IsQuestAvailable(uint questId)`, `Tsukimichi.GetBlockers(uint questId)` (BlockerText and the requirement list), `Tsukimichi.OpenQuest(uint questId)`, `Tsukimichi.GetState(uint questId)`, registered through `ICallGateProvider` with a documented version tag, so overlays and other quest plugins can build on the evaluator. **P3 "Clear my blues" plan** (M) — every undone unlock quest grouped by expansion then zone in story order, tagged by what it unlocks (dungeon, trial, raid series, alliance raid, job, system, society, field operation, flying), with state and BlockerText; "Copy as checklist"; the Todo overlay can pin the current expansion's block. **P6 Unlock route for alts** (S–M) — pick a job, duty, system or Moonlit reward and get the ordered remaining quests with level gates from the prerequisite path; "Copy route"; "Pin all" (after decision 10). **P13 Duty Finder unlock hint** (M) — `AgentContentsFinder.SelectedDuty` via IAddonLifecycle on "ContentsFinder", `UIState.IsInstanceContentUnlocked`, a panel beside the window with the unlocking quest, its moon and BlockerText, behind T20. **P8 Patch of origin** (M) — `AddedIn` from sheet data, Garland's patch field and DataGen diffs (no wiki scrape); "Added in 7.5x" chip; "New this patch" group in the Unlocks quick view. **T22** curated-data contribution guide and issue templates.

### Release 1.0.0 — 8.0-ready

**P5 "Before you continue" payoff gates** (M: instruction-only wording, the reason behind a closed "why? (spoiler)", shown only when the milestone is Ready or In journal, pairs reviewed by two finishers), **P14 Branching MSQ readiness** (L: per-route ordering from the prerequisite graph, multi-position status line, synthetic-branch fixture; must land before the 8.0 pre-patch, January 2027), **V2-17 Questionable cross-check** (M: read `IsQuestLockedReason` when Questionable is present to compare against the evaluator, logging disagreements into the diagnostic; an opt-in "add to Questionable priority" button). 8.0 week is reserved for the API bump alone.

### Release 1.1 — deferred

**P7 "Since you were away"** (M, with the "I last played in patch X" picker seeded from P8; alt-nag guard), **P9 Journal text reader** (M: `IDataManager.Excel.GetRawSheet` per quest, SeString evaluation, indexing off-thread and persisted per game version; search over completed quests only), a high-contrast glyph palette, V2-19 localization (the Prefix/Suffix string concatenations are catalogued now as its input).

## 4. Order, sizing and gates

| Release | Tasks | Effort | Shareable item | Owner gate |
|---|---|---|---|---|
| 0.5.1 | T0, T5a, T9a, T7 | ~4 days | Moons you can read; dailies stop flipping | accepting one tribe daily leaves the others Ready; partial moons visible at default scale; Shift or hold required to mark |
| 0.6.0 | T2, T23, P1, T18, T4, T8, T5b, T6, T21; T2a in parallel | ~2 weeks | Every row says why | Quarrels with Squirrels shows Lv 3 and is Ready at 1; a diagnostic round-trips into an issue; LICENSE and README in place; no scaling regression |
| 0.6.1 | T1 | ~4 days | Unlisted resolved | genre totals equal the Lodestone counts modulo the lag list; Unlisted empty |
| 0.6.2 | T3, P2 | ~4 days | "Why isn't this NPC giving me the quest?" | the 16 satisfaction and 17 postmoogle quests are not Ready without the rank or level; phased event chapters gate; the six research quests return the right blocker |
| 0.7.0 | T9b, T10, T11, T12, T19, P10, P12 | ~1.5 weeks | The new Journal; spoiler shield; export | two testers name eight states unaided; a mid-Endwalker tester sees no MSQ name beyond position; the first complete verification CSV is committed |
| 0.8.0 | T13, T16, T15, T14, T20, T17, P11, P4 | ~2.5 weeks | The new interface and star-chart path | tutorial completes with colours followed off and on, keyboard only; overlay reads over snow at 0.6 |
| 0.9.0 | V2-16, P3, P6, P13, P8, T22 | ~2.5 weeks | "Every unlock, in order"; padlocked duty → quest | a P3 plan followed for one expansion with no unwarned Blocked; five padlocked duties correct |
| 1.0.0 | P5, P14, V2-17 | ~2 weeks | "Before you continue"; 8.0-ready | P5 pairs reviewed by two finishers with no payoff named; synthetic-branch fixture green |
| 1.1 | P7, P9, high-contrast palette, V2-19 | later | | |

## 5. Things this plan deliberately leaves out
- Allowance timers, society rank grinds, aether-current field markers, party-shared quest views, any automation or auto-travel (8.1 does it natively; the official repository rejects it).
- Web polling from inside the plugin; any network code in the plugin at all (verification lives in a separate tool).
- A new journal category for Unlisted; Cards mode; a second maintained layout; idle animation; a high-contrast glyph palette before 1.1.

## 6. Execution method

One implementer agent per task in an isolated worktree with the task text above as its brief plus the relevant report sections; the coordinator merges in the stated order, runs the CI gates locally, dispatches a bug-hunter review of the merged release, applies fixes, bumps the version, writes the CHANGELOG section, tags, confirms the Actions run and the pluginmaster, and the owner verifies the gate in game. Items marked "owner checks in game" are not changed until the owner reports.
