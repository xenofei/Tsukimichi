# Tsukimichi — V3 Feature Plan (proposal)

Date: 2026-09-28
Status: **v3.1, 2026-09-28. Approved in principle by the owner with revisions; reviewed by a six-member panel (§2a); execution begins with release 0.5.1.**

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
- Seven deterministic sheet rules file **80 into existing nodes** and leave **100 hidden as retired** (rule outcomes in the report's appendix A). One quest (The New Frontier) has no signal and stays Unlisted. No new journal category is required; the Unlisted node becomes a small "Retired and hidden" bucket that is off by default.
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
- Recommendation (both designers agree on the diagnosis; geometry from the vector proposal, verified at 16 px in the rendered sheet): **halo gauge for progress** (Veil track ring, gold arc from 12 o'clock with a visual floor so 3 % reads as a pip, a small filling-moon core) and **refined solid-disc state moons** (Veil dark side, crisp rim, shading only at ≥ 9 px). The two families differ in silhouette, so a Ready quest never reads as a 50 % node. Tree glyphs grow to a 23 px halo in 30 px rows at default scales.
- Main window: a Night chrome with a 36 px toolbar (search pill, presets as a segmented control, Filters badge, character chip), custom tab strip with moon icons and counts, gauge rows in the tree, a state stripe on every table row, and the detail pane as a card stack with a hero banner, requirement checklist and a sticky action bar; a "Classic layout" toggle keeps the stock look. Token set with computed contrast; Dusk demoted from body text on cards because it fails AA there.

## 2. Decisions

Recorded 2026-09-28 from the owner: 1 approved (seven Unlisted rules); 2 Shift (with a hold-to-confirm alternative, see T7); 3 vertical tab strip as mocked; 4 whole-window Night chrome; 5 Cards mode; 6 order as proposed; 7 license still open.

Changed by the review panel (owner to veto if wanted): decision 4's "Classic layout" toggle becomes a "Follow Dalamud colours" token mapping so there is one layout to maintain, not two; decision 5's Cards mode is cut because journal banners are shared per genre, so cards would show identical thumbnails; decision 6's order is re-sequenced so every release carries something a player can screenshot (§4).

Still open for the owner, not blocking execution:
- **7 License.** MIT is the norm for Dalamud plugins; needed before the first public post, not only for the official repository.
- **8 Authorship disclosure.** The official Dalamud repository's AI usage policy rejects entirely AI-generated submissions and requires disclosure of AI use. V2-20 needs the owner's decision on how the plugin is presented before a submission is prepared.

## 2a. Review panel outcomes

Six reviewers critiqued the proposal (reports in docs/review/panel/): a five-persona player survey group, a Dalamud plugin developer, a game UX designer from another studio, a QA and data engineer, an accessibility specialist, and a product and community lead. What changed:

| Theme | Raised by | Change |
|---|---|---|
| No CI; tag workflow can ship a no-op release; snapshot schema bump strands characters on rollback | QA, Dalamud dev | New T0 engineering floor before any feature; T3 is additive at schema v1 |
| Nothing shareable until 0.9.0; 0.6.0 too large to diagnose | Product, QA, Dalamud dev, players | Hotfix 0.5.1 first; 0.6.0 split into 0.6.0 / 0.6.1 / 0.6.2; P1 and P2 pulled into 0.6.0; P10 and P12 into 0.7.0; P11 and P4 into 0.8.0 |
| Requirement fields are readable | Dalamud dev | T3 loses its "not checked" hedge: `SatisfactionSupplyManager.SatisfactionRanks`, `IPlayerState.DeliveryLevel`, `GameMain.Festival.Phase` |
| Spoilers everywhere: MSQ names ahead of position, artwork for undone quests, P5 wording, P9 search | Players, product, UX | New T19 spoiler shield in 0.7.0; P5 phrased instruction-only; P9 search limited to completed quests |
| Colour-only states under colour blindness; halo track too faint; Shift chord excludes some users; right-click-only actions; hotkeys collide with hotbars | Accessibility | Folded into the glyph revision (bar on Foreclosed, seal on Accepted, Shadow disc, thicker track); hold-to-confirm; action bar and "…" buttons; only Ctrl+F and Esc bound by default |
| Gold used for everything; Status column truncates the blocker; no "what now" surface; vocabulary drift | UX, players | Gold reserved for actionable items; Status is the stretch column; "Tonight" card when nothing is selected; one display name per state (T23) |
| Returning-player card has no "then" for a fresh install | Players, product | P7 gains an "I last played in patch X" picker and depends on P8; moved to 1.1 |
| Full verification would gate releases for a week and lean on a derived source | Product, QA | T2a keeps the owner's "every quest" scope but runs as a parallel stream that never blocks a tag, in its own tool project, against the Lodestone, the wiki and FFXIV Collect; Garland only for duty-unlock instances |
| Support load: wrong-state reports with no diagnostic; stale README; no license | Product | T18 "Report this quest" diagnostic and data stamp; T21 README, LICENSE, in-app "What's new" |
| Curated data has no cross-file invariants; `otherSource` has no reader | QA | T4 adds a structured `otherSources` array and invariant tests; golden file for T1 |
| Patch day breakage of hooks | Product, Dalamud dev | T20 kill switch: item hint, context menus, DTR and P13 disable themselves on an untested game version |

Declined: dropping T2a (the owner asked for every quest verified); keeping both Classic and Night layouts (one layout, themed); Garland per-quest fetches as evidence (derived from the same sheets the plugin reads).

## 3. Task list

Effort: S ≈ a day of agent work, M ≈ a few days, L ≈ a week plus in-game testing. Every task ends with: build with warnings as errors, tests green in CI, a bug-hunter review pass, a CHANGELOG entry. Every release ends with tag → Actions → pluginmaster → in-game update; the owner verifies in game.

### Release 0.5.1 — engineering floor and hotfix

**T0 Engineering floor** (S)
- `.github/workflows/ci.yml`: on push and pull request, restore the Dalamud dev libs the same way release.yml does, `dotnet build Tsukimichi.sln -c Release -warnaserror`, `dotnet test` (data-driven tests skip without game files), a curated-data invariants test project step.
- `release.yml`: fail the tag build when the csproj `Version` does not equal the tag; fail when CHANGELOG.md has no section for that version; pin actions by SHA; add `concurrency`; retry the pluginmaster push; add `workflow_dispatch` to republish.
- Freeze a real schema-v1 snapshot fixture under Tsukimichi.Tests/Fixtures before any model change; DataGen `--dump-catalog` producing a gzipped catalog fixture so pure-record tests run in CI, plus one Lumina-backed test that the fixture matches the live sheets.
- Tests: the workflow files are validated by running them on this release.

**T5a Widest-blast state bugs** (S)
- Tribe daily offer no longer read from the accepted-dailies array; in-progress dailies resolve Accepted (GameStateReader.cs:143–152, 281–303; StatePoller.cs:219; RequirementEvaluator.cs:131–135).
- Festival context wired into the evaluation context in `SessionState.SetCatalog` so quests of never-seen events resolve per the rules; out-of-season quests leave Totals the way Foreclosed does.
- `recentEvents` cleared when `LiveContentId` changes (StatePoller.cs:246–258).
- The evaluation-context builder, recent-events tracker and login-readiness check move into Core (`EvalContextBuilder`, `RecentEventsTracker`, `LoginReadiness`) so each fix has a test.

**T9a Glyph contrast hotfix** (S)
- MoonGlyph only: unlit disc becomes Shadow #3A4363 with a rim `clamp(0.12r, 1.5, 3)` px, terminator floor so a 3 % node draws a visible crescent, Foreclosed gets the diagonal Eclipse bar, Ready below r 9 gets a 1 px ring instead of the invisible glow. No layout change; the art pass (T9b) waits for 0.7.0.
- Tests: MoonGeometry floor; GlyphDebugWindow shows the matrix.

**T7 Mark-as-unique guard and revert** (S)
- "Mark as unique…" and "Not unique (hide)" both open the confirm popup; the confirm button is enabled by Shift held or by a 600 ms hold-to-confirm arc (text fallback under reduced motion); the note field is auto-focused and Enter confirms; an undo line appears for 8 s after either verdict.
- The Yours confidence filter also lists quests hidden by "Not unique" as struck-through rows whose context menu offers Restore; Settings › Data gains "Your Moonlit verdicts (N)" with per-row Restore and Restore all.
- Tests: UniqueRewardCatalog exposes hidden overrides; OverridesFile round trip; a `ConfirmGate` helper in Core unit-tested for chord, hold and cancel.

### Release 0.6.0 — accuracy, guards, first shareable answers

**T2 Displayed level with offset** (S) — `DisplayLevel = Level + LevelOffset` on QuestRecord; used by TablePane.cs:295/364, DetailPane.cs:872, DiscoveryWindow.cs:468, the level filter and sort in QuestQuery.cs:441, the Todo and Nearby hints; the evaluator keeps Level. Tests: Quarrels with Squirrels displays 3 and is Ready at 1.

**T4 Curated data corrections** (S) — duty_unlocks.json 70011 → 70012; new curated/online_store.json (68 item ids, note and evidence URL per entry); `UniqueRewardEntry.OtherSources` as a structured array (loader tolerant when absent) that MoonlitPane, RewardTooltip and the hover hint render as "Store only" with a Moonlit filter "Hide store re-sells"; feature_quests.json becomes derived, not hand-maintained; a curated-invariants test project (schema, ids exist in the sheet, cross-file consistency, no dangling 70011); `tools/regen.ps1` and docs/data/DATA-VERSION.md; DataGen reports drop timestamps so regenerations diff cleanly.

**T5b Remaining Core/Game fixes** (S) — first-pass-after-login guard; `SnapshotDiff.SameSet` without allocation; help text lists `search` and `settings`; Configuration.Load copies a corrupt file aside; Wotsit partial-batch retry; DeleteAllData/ForgetCharacter keep poller memory and disk in step; poller re-diffs after a catalog retry; `PlayerState.MaxLevel`/`MaxExpansion` wired (closes DRAFT-NEEDED E). Plausible items the owner checks in game before code changes: sticky DoneThisCycle after reset; festival read empty during zone load. The parent-job question is settled by `QuestWork.AcceptClassJob` without a play test.

**T6 UI fixes** (M) — window minimum size scales with UiScale; the three unscaled combos; Todo overlay and Nearby apply the font scale; dashboard pins keyed on the runner's pins version; Reveal opens the ancestor chain and scrolls; Moonlit selection from `ui.SelectedRowId`; path/unlock/chain clicks through Reveal; banner cropped not squashed; stable "Not started" node id; PushIndent through Px; character combo ids include the content id; sort restore; table column widths re-asserted when IconScale changes; Todo overlay minimum opacity 0.6 with a 1 px Night text shadow below 0.9; `UiMetrics.MinTarget = max(Px(26), 24)`.

**T8 Hover affordances** (S) — Moonlit reward icons show RewardTooltip (with the store line); the icon-less stand-in becomes a veiled moon; job icons, path and unlock moons, confidence badges, chip counts and the DTR entry get tooltips; badge tooltips respect window hover.

**P1 Blocker line** (S) — ladder, Nearby, Todo, level-up nudge and the new Status column show the single decisive blocker ("after MSQ: The Vault", "Rank: Trusted with the Pelupelu", "Lv 80"); one `BlockerText` function in Core reused by P2, P6, P13 and the diagnostic.

**P2 "Why not offered?"** (S) — IContextMenu entry on a targeted EventNpc (`MenuTargetDefault.TargetObject`, filtered like DiscoveryCommands.cs:63–69) "Tsukimichi: quests here (N)" opening the window on that NPC's quests with blockers; `/tsuki why <quest>` prints blockers to chat; curated quirk notes (Up In Arms optional with the Zenith; "Bloodsworn" reads "Allied" after 7.0); no target content id is ever stored.

**T18 Report this quest** (S) — a button in the detail pane and `/tsuki report` copy a fenced diagnostic block (plugin version, game version, data version, curated revision, schema, quest id, state, decisive blocker, per-requirement verdicts and inputs, filing rule; no content id); the data version stamp shows in About and the status bar tooltip; GitHub issue templates.

**T21 Public-facing hygiene** (S) — README rewritten (current features, all commands, install, what hooks the plugin uses, "nothing leaves the machine", ToS note, license, issue path), LICENSE per decision 7, in-app "What's new" card driven by `LastSeenVersion`, help topics "Why my counts differ from the journal" and "Known quirks", data credits (Square Enix, consolegameswiki CC BY-NC-SA, FFXIV Collect, Garland Tools).

**T23 Vocabulary** (S) — one display name per state everywhere, with the poetic names as glyph subtitles: Ready, Ready on another job, In journal (Accepted), Blocked (always with the blocker), Done today / this week (by interval), Completed, Locked out (reason), Not checked. "Feature Unlocks" node → "Unlock quests", preset "Features" → "Unlocks", "Next step" column → "Status", "Presets" → "Quick views", "Level band" → "My level", "Tribal" → "Allied Society", Moonlit tab subtitle "rewards only a quest gives". A glossary in docs and a string-lint test that no other spelling of a state appears in Strings*.cs.

**T2a Full-catalog verification with references** (L, parallel stream, never gates a tag) — new `tools/Tsukimichi.Verify` console: every one of the 5,373 named quests against the Lodestone Eorzea Database (official: name, level, class, prerequisites, rewards) for every quest it lists, consolegameswiki where the Lodestone lacks the row or disagrees, and Garland Tools only for `reward.instance` on curated duty unlocks; every unique_quests.json entry against the full FFXIV Collect dumps and the wiki item acquisition line. Rate ≤ 1 request per 2 s with an identifying user agent and robots.txt honoured; resumable on-disk cache outside the repo keyed by game version; `--offline` and `--since` diff modes. Output committed: docs/data/quest-verification.csv (long form: rowId, name, fact, catalog value, source, source value, source URL, verdict ∈ {match, catalogWrong, sourceWrong, sourceLagging, notModeled, notListed, ambiguous, unresolved}, reason, fixedIn), a derived one-row-per-quest summary, docs/data/reward-verification.csv, docs/data/verification-full.md. Facts only are committed (ids, levels, counts, URLs), never quest or description text. Discrepancies feed T2/T4 and data-only patch releases.

### Release 0.6.1 — Unlisted refiling

**T1 Unlisted refiling** (M) — `JournalRefiler` in CatalogMapper implementing rules 1–7 from docs/data/unlisted-report.md §4; `QuestRecord.IsRetired` and `RefiledFrom` provenance; `IsRetired` honoured centrally (TreeCounts, QuestQuery, Wotsit registration, search, DTR, Nearby, MsqProgress, Compare); `EventIconType == 10` joins Unlock quests; curated retired_quests.json (the 3 removed 3.05 sidequests and the 5 listed retired rows) and refile_overrides.json (Eureka trio → The Forbidden Land, Eureka; Seeing the Cieldalaes → G107), each entry with note and evidence URL; the node renamed "Removed from the game" and off by default; detail pane line "Filed under X (rule N)"; runtime `JournalFiling = Refiled | Classic` setting as the in-field rollback.
- Tests: golden file docs/data/refile-expected.csv (row id, rule, genre) diffed row by row; every refiled quest in exactly one node; retired never counted; a retired-twin test (44 same-name pairs); rule 6 tie-break defined and tested; the `Unknown12` column pinned by a data test since Lumina renumbers unknown columns; genre totals equal the Lodestone counts modulo documented lag.

### Release 0.6.2 — requirement kinds

**T3 Missing requirement kinds** (M) — map `SatisfactionNpc/Level`, `DeliveryQuest`, `FestivalBegin/End`; Requirements gain `CustomDeliveryRank` and `CarrierLevel`; the seasonal check becomes id active and Begin ≤ Phase ≤ End, applied in StateResolver rule 3 as well; GameStateReader reads `SatisfactionSupplyManager.Instance()->SatisfactionRanks` (12 slots, index RowId − 1, bounds-checked), `IPlayerState.DeliveryLevel`, and festival (Id, Phase) pairs from the array the owner's one-event log shows to be authoritative (GameMain.ActiveFestivals, PlayerState.ActiveFestivalIds/Phases and EventFramework.Festivals are all logged for one event first); snapshot fields are additive at schema v1 (`festivalPhases`, `satisfactionRanks`, `carrierLevel`, empty defaults) so older builds still read the file; SnapshotDiff diffs (id, phase) and the new fields.
- Tests: the 16 satisfaction quests Blocked at rank 3 and Ready at 4; postmoogle quests Blocked below carrier level; Hatching-tide 2014 chapter 2 Blocked at phase 1; a v1 fixture round-trips unchanged; the 7.5 prerequisite swap ("Go West, Craftsman" → "Inscrutable Tastes") resolves.

### Release 0.7.0 — Journal revamp

**T9b Glyph art pass** (M) — the revised state set from docs/design/glyphs/proposal.md v2.1: Accepted as the 60 % "sealed early gibbous" with Silver rim and Night seal dot; interior detail (three maria, three craters, terminator glow band, rim vignette) from r ≥ 12 within a 12-primitive budget; highlight arc ≥ 16; tokens `Umbra`, `MoonHigh/Deep`, `SilverHigh/Deep`, `VeilLine`, `Shadow`; §3.7 documents what each state relies on when colour is removed; moons are used only for quest state and completion fraction (requirement marks, obtained marks and the live pip get their own marks).

**T10 Halo gauge** (M) — Core/Ui/GaugeGeometry.cs (arc endpoints, floor ε = max(0.06, (stroke + 1.5)/(2π·0.80R)), core radius, caps) with the nine tests from docs/review/panel/qa-data-engineer.md §2.2; `MoonGlyph.DrawHalo`: track VeilLine full alpha, stroke max(2, 0.18R), core (Shadow + Dusk rim) from R ≥ 12, track + arc + number below; replaces DrawFilling for progress everywhere.

**T11 Tree rows** (M) — `TreeGlyphRadius(L) = clamp(0.5·L·IconScale, 8, 18)` with a 24 px box floor; rows via `ImGuiTreeNodeFlags.FramePadding` (keeping TreeNodeEx for keyboard nav); section rows with a gold rule; expansion pills; "done / total" in Dusk with the 44×3 px mini bar in every density, percentage in the tooltip; hover wash, selected wash with a gold left rule; complete nodes tinted MoonDim without glow (gold reserved for actionable rows); Ready count badge; tab badge = Ready count.

**T12 Status bar and density** (S) — halo gauge, static live pip, MSQ pill, right-aligned version in Dusk; `Density` Dense 24 / Comfortable 32 (default), no Cards; `ReduceMotion` defaults from the OS animation setting.

**T19 Spoiler shield** (M) — MSQ names beyond the character's position + N (default 3, on by default for MSQ) are masked everywhere names print (tree, table, detail, status, Todo, chat links, Compare, Wotsit); journal artwork shown only for accepted or completed quests; a per-character override and a Settings toggle; "Sprout mode" preset that also narrows the default view.

**P10 Abandoned ledger** (S) and **P12 Export** (S) — as in the research doc; export strips content id, account id and character name by default, adds CSV, cards and orchestrion, and is never an upload.

### Release 0.8.0 — Main interface revamp

Order inside the release: T13 → overlay and Nearby on Chrome → T16 → T15 → T14 → T20 → T17 → P11, P4.

**T13 Chrome helpers** (M) — Ui/Chrome.cs (BeginCard/EndCard, Pill, Chip, Badge, Hairline, Lift, SegmentedControl with an explicit "All" state, IconButtonRound at ≥ MinTarget, Scrim, ImageCover with centre-crop UVs, FocusRing, OutlinedText), Ui/Motion.cs (event-driven only: Lerp/Pulse, ReduceMotion gate, key pruning, skipped while scrolling), `Theme.PushNightWindow` applied in Window.PreDraw with popups and tooltips explicitly restyled, `Theme.FollowDalamudColours` mapping; the Todo overlay and Nearby window move onto Chrome with outlined Silver text, Mist hints, 14 px glyph floor, Compact mode, a "…" button per row, and "Locked = click-through".

**T16 Detail pane** (M) — hero ≤ 96 px with scrim and state pill, Requirements first with a single next-step marker, chain shown once, reward tiles with hover, the Path section as the star chart from docs/design/path-section-proposal.md (sky bands per expansion, walked gold thread, dashed silver future, folded-run beads, Any-join ghost nodes via `PathFinder.Alternatives`, target halo, Unlocks-next comb, jump-to-target pill, minimap only when scrolling), giver card, a labelled primary action plus Pin in the action bar, plain provenance line; EmptyState with heading, action and offending-filter chips; the "Tonight" card when nothing is selected (Ready count, next MSQ, live events, from the Todo row model).

**T15 Table** (M) — Status is the stretch column and never ellipsises the noun; Exp and Rewards hide first; state stripe pattern-encoded (solid / two segments / 60 % / none / dashed / dotted) so it survives greyscale; hover lift from the previous frame's hovered row; selection ring; header restyle; level and expansion pills; job icons; Dense/Comfortable.

**T14 Toolbar and tab strip** (M) — 36 px toolbar (search pill, Quick views segmented control with "All", Filters badge that counts engaged filters, character chip with job icon and static pip, round icon buttons), scope shown as the first chip; vertical TabStrip with moon icons and Ready-count badges recording `UiRects.Tabs`, the left column widened so the rail does not eat the tree; toolbar reflows to two rows at narrow widths; window default size fits the viewport at UiScale 1.6; tutorial rewritten in three chapters (Find / Read with a legend of all eight states / Beyond), ≤ 35 words per step, Enter/arrows/Esc, "Later" vs "Don't offer again", card scaled by UiScale.

**T20 Addon kill switch** (S) — `TestedGameVersion` in the manifest; item hint, item and NPC context menus, DTR and (later) the Duty Finder hint disable themselves with a one-line notice on an untested game version until the owner ships a bump.

**T17 Motion, typography, keyboard** (S) — chevron rotation, reveal pulse, gauge fill on change only; game font handles per UiScale bucket with the AddText fallback; only Ctrl+F and Esc bound by default, other shortcuts configurable and off; Menu key / Shift+F10 opens the row menu; every right-click action reachable from the action bar or a "…" button.

**P11 Seasonal now** (S) and **P4 Story sidequests preset** (S–M) — as in the research doc; a single notice budget shared by P5, P7, P10, P11 and the level-up nudge.

### Release 0.9.0 — the ordered answers

**P3 "Clear my blues" plan** (M), **P6 Unlock route for alts** (S–M), **P13 Duty Finder unlock hint** (M: `AgentContentsFinder.SelectedDuty` via IAddonLifecycle on "ContentsFinder", `UIState.IsInstanceContentUnlocked`, behind T20), **P8 Patch of origin** (M: from sheet data, Garland's patch field and DataGen diffs, not a wiki scrape; "Added in 7.5x" chip, "New this patch" group), **T22 Curated-data contribution guide** and issue templates.

### Release 1.0.0 — 8.0-ready

**P5 "Before you continue" payoff gates** (M: instruction-only wording, the reason behind a closed "why? (spoiler)", shown only when the milestone is Ready or In journal, pairs reviewed by two finishers), **P14 Branching MSQ readiness** (L: per-route ordering from the prerequisite graph, multi-position status line, synthetic-branch fixture; must land before the 8.0 pre-patch, January 2027), **V2-20 official repository preparation** (pending decisions 7 and 8). 8.0 week is reserved for the API bump alone.

### Release 1.1 — deferred

**P7 "Since you were away"** (M, with the "I last played in patch X" picker seeded from P8; alt-nag guard), **P9 Journal text reader** (M: `IDataManager.Excel.GetRawSheet` per quest, SeString evaluation, indexing off-thread and persisted per game version; search over completed quests only), V2-16 IPC provider, V2-17 Questionable cross-check, V2-19 localization (the Prefix/Suffix string concatenations are catalogued now as its input).

## 4. Order and sizing

| Release | Tasks | Effort | Shareable item |
|---|---|---|---|
| 0.5.1 | T0, T5a, T9a, T7 | ~4 days | Moons you can read; dailies stop flipping |
| 0.6.0 | T2, T4, T5b, T6, T8, P1, P2, T18, T21, T23; T2a starts in parallel | ~2 weeks | "Why isn't this NPC giving me the quest?" |
| 0.6.1 | T1 | ~4 days | Unlisted resolved |
| 0.6.2 | T3 | ~4 days | Custom delivery, postmoogle and event chapters gate correctly |
| 0.7.0 | T9b, T10, T11, T12, T19, P10, P12 | ~1.5 weeks | The new Journal; spoiler shield; export |
| 0.8.0 | T13, T16, T15, T14, T20, T17, P11, P4 | ~2.5 weeks | The new interface and star-chart path |
| 0.9.0 | P3, P6, P13, P8, T22 | ~2 weeks | "Every unlock, in order"; padlocked duty → quest |
| 1.0.0 | P5, P14, V2-20 prep | ~2 weeks | "Before you continue"; 8.0-ready |
| 1.1 | P7, P9, V2-16, V2-17, V2-19 | later | |

Success criteria without telemetry, per release, are in docs/review/panel/product-community.md §6 and are adopted as the release gates (for 0.6.0: genre totals equal the Lodestone counts modulo documented lag; the quests from the six research threads return the right blocker through P2; a diagnostic round-trips into an issue).

## 5. Things this plan deliberately leaves out
- Allowance timers, society rank grinds, aether-current field markers, party-shared quest views, any automation or auto-travel (8.1 does it natively; the official repository rejects it).
- Web polling from inside the plugin; any network code in the plugin at all (verification lives in a separate tool).
- A new journal category for Unlisted; Cards mode; a second maintained layout; idle animation.

## 6. Execution method

One implementer agent per task in an isolated worktree with the task text above as its brief plus the relevant report sections; the coordinator merges, runs the CI gates locally, dispatches a bug-hunter review of the merged release, applies fixes, bumps the version, writes the CHANGELOG section, tags, confirms the Actions run and the pluginmaster, and the owner verifies in game. Plausible-bug items marked "owner checks in game" are not changed until the owner reports.
