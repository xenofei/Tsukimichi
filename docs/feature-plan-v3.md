# Tsukimichi — V3 Feature Plan (proposal)

Date: 2026-09-28
Status: **proposal, awaiting the owner's approval. Nothing here is scheduled or started.**
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
- Recommendation (both designers agree on the diagnosis; geometry taken from the vector proposal, which was verified at 16 px in the rendered sheet): **halo gauge for progress** (Veil track ring, gold arc from 12 o'clock with a visual floor so 3 % reads as a pip, a small filling-moon core) and **refined solid-disc state moons** (Veil dark side, crisp rim, shading only at ≥ 9 px). The two families differ in silhouette, so a Ready quest never reads as a 50 % node. Tree glyphs grow to a 23 px halo in 30 px rows at default scales.
- Main window: a Night chrome with a 36 px toolbar (search pill, presets as a segmented control, Filters badge, character chip), custom tab strip with moon icons and counts, gauge rows in the tree, a state stripe on every table row, and the detail pane as a card stack with a hero banner, requirement checklist and a sticky action bar; a "Classic layout" toggle keeps the stock look. Token set with computed contrast; Dusk demoted from body text on cards because it fails AA there.

## 2. Decisions the owner is asked to make

1. **Unlisted**: approve the seven rules (80 quests filed into existing nodes, 100 retired rows hidden behind the existing "Show Unlisted bucket" setting, renamed "Show retired and hidden quests")? Alternative: keep everything in Unlisted and only fix the "Sephiroth Missions" label.
2. **Modifier key** for Mark as unique and Not unique (hide): Shift (recommended, matches the game's own "hold Shift" conventions) or Ctrl?
3. **Tab strip**: vertical strip with moon icons (as mocked) or keep horizontal tabs restyled? The mockup at docs/design/mockups/main-window.html shows the vertical version.
4. **Whole-window Night chrome** by default with a Classic toggle (recommended), or card the panes only and keep Dalamud's window colours?
5. **Cards mode** (table rows with banner thumbnails): include in the table increment or drop it?
6. **Player features order**: §4 proposes 0.9.0 = P1, P2, P10, P11, P12, P4, P8 and 1.0.0 = P3, P6, P5, P7, P13, P9. Reorder or cut as you like.
7. **License** (carried over): the official repository submission still needs a LICENSE file. MIT or AGPL-3.0 are the two common choices for Dalamud plugins.

## 3. Task list

Effort: S ≈ a day of agent work, M ≈ a few days, L ≈ a week plus in-game testing. Every task ends with: build 0 warnings, tests green, a bug-hunter review pass, a CHANGELOG entry. Each release ends with tag → Actions → pluginmaster → in-game update, as before.

### Release 0.6.0 — data accuracy, Unlisted, fixes, guards

**T1 Unlisted refiling** (M)
- Files: Tsukimichi.GameData/CatalogMapper.cs (new `JournalRefiler`: rules 1–7 from docs/data/unlisted-report.md §4), Tsukimichi.Core/Model/QuestRecord.cs (`IsRetired`, `RefiledFrom` provenance), Tsukimichi.Core/Query/{TreeCounts,QuestQuery,FeaturePresets}.cs (`EventIconType == 10` joins Feature Unlocks; retired quests never enter totals, shown only when the setting is on), Tsukimichi/Data/curated/retired_quests.json (the 3 removed 3.05 sidequests + the 5 listed retired rows), Tsukimichi/Data/curated/refile_overrides.json (3 Eureka quests → The Forbidden Land, Eureka; Seeing the Cieldalaes → G107), Tsukimichi/Ui/{TreePane,DetailPane,Strings}.cs (node renamed "Retired and hidden", provenance line "Filed under X because: rule N").
- Tests: rule outcome counts pinned to the probe's numbers (80 refiled / 100 retired / 1 unlisted), every refiled quest lands in exactly one node, retired quests never counted, data test that all QuestLock joins are still "any".
- Acceptance: the screenshot's "Unlisted 118/179" disappears; Class & Job Quests gain the 23 intro quasi-quests; Feature Unlocks totals rise by 58; totals of the affected genres match the Lodestone counts in verification-report-2 §5.

**T2 Displayed level with offset** (S)
- Files: QuestRecord (`DisplayLevel = Level + LevelOffset`), TablePane.cs:295/364, DetailPane.cs:872, DiscoveryWindow.cs:468, QuestQuery.cs:441 (level filter and level sort use DisplayLevel; the evaluator keeps Level), TodoOverlay and Nearby hints.
- Tests: Quarrels with Squirrels displays 3 and is Ready at level 1; sort by level uses the display value.

**T3 Missing requirement kinds** (M)
- Files: CatalogMapper (map `SatisfactionNpc/Level`, `DeliveryQuest`, `FestivalBegin/End`), Core/Evaluation/Requirements.cs (+ `CustomDeliveryRank`, `CarrierLevel`, seasonal check becomes id active **and** Begin ≤ Phase ≤ End), Game/GameStateReader.cs (read `ActiveFestivals[i].Phase`; read satisfaction ranks from `SatisfactionSupplyManager` and the carrier level if ClientStructs exposes it, otherwise the requirement reports "not checked" like Mount/House), CharacterSnapshot schema v2 with a migrator (festival phase tuple, satisfaction ranks), docs/superpowers/specs update.
- Tests: the 16 satisfaction quests are Blocked at rank 3 and Ready at 4; postmoogle quests Blocked below carrier level; Hatching-tide 2014 chapter 2 Blocked at phase 1.

**T4 Curated data corrections** (S)
- Files: curated/duty_unlocks.json (70011 → 70012), new curated/online_store.json (the 68 item ids from the verification scratchpad, with the rule "past-year festival collectibles"), DataGen CuratedOverlay (emits `otherSource = OnlineStore`), regenerate unique_quests.json and feature_quests.json, MoonlitPane and RewardTooltip show "also on the Online Store".
- Tests: DataGen `--verify` still 0 failed; Starlight Bear carries the store source.

**T5 Core/Game bug fixes** (M)
- Tribe daily offer: stop feeding `TodaysDailyOffer` from `QuestManager.DailyQuests`; add non-completed daily entries to Accepted (GameStateReader.cs:143–152, 281–303; StatePoller.cs:219; RequirementEvaluator.cs:131–135).
- Festival context: `SessionState.SetCatalog` builds the eval context with festival ends and achievement gating, so never-seen events resolve per the rules instead of Blocked forever; Totals exclude out-of-season quests the same way they exclude Foreclosed (SessionState.cs:216/232; TreeCounts).
- Character switch: clear `recentEvents` when `LiveContentId` changes (StatePoller.cs:246–258).
- First pass after login: treat an all-zero completion mask with an empty journal as not ready (SnapshotService.cs:74–78; AcceptedSince.Reconcile).
- Perf: `SnapshotDiff.SameSet` compares sorted sequences before allocating.
- Plausible items verified in game by the owner before they are changed: sticky DoneThisCycle after reset; class-pinned quests taken on the parent job; festival read empty during zone load (reorder rule 3 after rule 4).
- Also: help text lists `search` and `settings`; Configuration.Load copies a corrupt file aside before resetting; Wotsit partial-batch retry; DeleteAllData/ForgetCharacter keep poller memory and disk in step.
- Tests: one per fix in Tsukimichi.Tests (evaluation and runtime), with the failing input from the report.

**T6 UI bug fixes** (M)
- MainWindow minimum size scales with UiScale (MainWindow.cs:117); combos in FilterPanel.cs:526, MoonlitPane.cs:245, CharactersPane.cs:828 use the `ImRaii.Combo` + `ApplyFontScale` pattern; TodoOverlay and DiscoveryWindow apply the font scale; dashboard pins keyed on the runner's pins version (CharactersPane.cs:1757–1799); Reveal opens the ancestor chain and scrolls (TreePane.cs:83); Moonlit selection derived from `ui.SelectedRowId` (MoonlitPane.cs:92); path/unlock/chain clicks go through Reveal; banner cropped not squashed (DetailPane.cs:309); "Not started (N)" node id without N; PushIndent through Px; character combo ids include the content id; sort restore fixed (`SpecsDirty` first frame); table column widths re-asserted when IconScale changes.
- Tests: GaugeGeometry/UiMetrics math where testable; the rest verified by the owner in game at UiScale 0.9 / 1.15 / 1.6.

**T7 Mark-as-unique guard and revert** (S)
- Files: DetailPane.cs:538 and MoonlitPane.cs:465 gate on the chosen modifier (`io.KeyShift`) with the button disabled otherwise and a tooltip "Hold Shift and click"; the popup's note field is auto-focused and Enter confirms; the Yours confidence filter also shows quests hidden by "Not unique" as struck-through rows whose context menu offers Restore; Settings › Data gains "Your Moonlit verdicts (N)" with per-row Restore and Restore all; help and tutorial text updated.
- Tests: UniqueRewardCatalog exposes hidden overrides for the Yours filter; OverridesFile round trip; guard logic unit-tested through a small `ModifierGate` helper.

**T8 Hover affordances** (S)
- Moonlit reward icons (MoonlitPane.DrawIcon) show `RewardTooltip` (large icon, item level, category, description, source, "also on the Online Store"); the icon-less stand-in becomes a veiled moon; job icons on the dashboard, path and unlock moons in the detail pane, confidence badges, chip counts and the DTR entry all get tooltips; badge tooltip in DetailPane.cs:359 respects window hover.

### Release 0.7.0 — Journal revamp (glyphs, tree, status)

**T9 Tokens and state glyphs** (S)
- Theme.cs: `UnlitDisc → Veil`, new `Umbra`, `Bruise`, `MoonHigh/Deep`, `SilverHigh/Deep`, surface tokens `NightSunken/NightHover/NightLine/VeilLine/Shadow`, text tokens `Mist/VeilText/EclipseText`; MoonGlyph.Draw follows docs/design/glyphs/proposal.md §3.1 (rim `clamp(0.10r, 1.25, 3)`, dash counts by radius, notch minimum 2 px, highlight arc ≥ 16 px); GlyphDebugWindow shows the matrix.
- Tests: MoonGeometry unchanged; contrast table asserted for the new tokens (relative luminance helper in Core/Ui).

**T10 Halo gauge** (M)
- Core/Ui/GaugeGeometry.cs (arc endpoints, ε floor `max(0.06, (stroke + 1.5)/(2π·0.80R))`, core radius, cap positions) with tests; Ui/Gauge.cs `MoonGlyph.DrawHalo(dl, center, R, fraction)` and `DrawHaloInline(fraction, size)`; replaces `DrawFilling` for progress everywhere (tree, status bar, dashboard sections, Moonlit kind moons, chains, Flight zones); below R = 7 draws track + arc only.
- Tests: fractions 0, 0.03, 0.1, 0.5, 0.97, 1 produce strictly increasing arc angles with the floor; 0 and 1 exact.

**T11 Tree rows** (M)
- UiMetrics `TreeGlyphRadius(L) = clamp(0.5·L·IconScale, 8, 18)`, `TreeRowHeight`; TreePane draws rows with `ImGuiTreeNodeFlags.FramePadding`, the halo at `labelX + R`, section rows with a 1 px gold rule, expansion badge pills, "done / total" in Dusk with a 44×3 px mini bar, hover wash Silver 5 %, selected row Veil 22 % wash + 2 px gold left rule, complete nodes tinted Moon with a glowing glyph, Ready count badge; keyboard nav preserved by keeping `TreeNodeEx` under the overlay.
- Owner check: 17/612 and 129/195 read at a glance at UiScale 1.15 / IconScale 1.25 (the sheet's row 4 is the reference).

**T12 Status bar and density** (S)
- Status bar gets the halo gauge, a live pip, an MSQ pill and right-aligned version; `Configuration.Density` (Dense 24 / Comfortable 32 / Cards 44 px rows, default Comfortable) drives tree and table row heights; Settings › Display gains Density.

### Release 0.8.0 — Main interface revamp

**T13 Chrome helpers** (M): Ui/Chrome.cs (`BeginCard/EndCard`, `Pill`, `Chip`, `Badge`, `Hairline`, `Lift`, `SegmentedControl`, `IconButtonRound`, `Scrim`, `ImageCover` with centre-crop UVs, `FocusRing`), Ui/Motion.cs (`Lerp/Pulse/Breath` on `ImGui.GetTime()`, `ReduceMotion` gate, key pruning), `Theme.PushNightWindow`, `PushTooltip`. Tests for Motion easing and ImageCover UV math.

**T14 Toolbar, tab strip, chips** (M): MainWindow toolbar rebuilt (search pill, presets segmented control, Filters badge, character chip with job icon and live dot, round icon buttons for Help/Tutorial/Settings), Ui/TabStrip.cs (vertical or horizontal per decision 3) recording `UiRects.Tabs` for the tutorial, conditional chip row, whole-window Night chrome; `Configuration.ClassicLayout` restores the stock tab bar and colours; tutorial run to the end as the acceptance check.

**T15 Table** (M): 3 px state stripe on every state, hover lift and selection ring via `TableSetBgColor`, header restyle, level and expansion pills, job icons, Dense/Comfortable layouts; Cards mode with banner thumbnails (`TryGetWrap`, placeholder, visible rows only) if decision 5 is yes.

**T16 Detail card stack and empty states** (M): hero banner with gradient scrim and state pill, chain progress bar, requirement checklist with moon marks and a next-step bar, reward tiles (unique = gold ring + crescent badge) with hover, path/unlocks/giver cards, sticky action bar of round icon buttons, provenance caption; EmptyState with heading, action button and clickable offending-filter chips.

**T17 Motion, typography, secondary panes** (M): chevron rotation, reveal pulse, gauge fill animation, live-pip breath behind `ReduceMotion`; Axis font handles through `IFontAtlas` with the `AddText` fallback; Moonlit/Flight/Characters left lists on gauge rows; Help/Todo/Discovery on Chrome; keyboard shortcuts (Ctrl+F, Ctrl+1..4, F, Enter) and a focus ring.

### Release 0.9.0 — player-driven features, first set (all from docs/research/player-gripes-2026.md §3)

- **P1 Blocker line** (S): ladder, Nearby, Todo and level-up nudge rows show the single decisive blocker ("after MSQ: The Vault", "Rank: Trusted with the Pelupelu") — presentation over the existing evaluator.
- **P2 "Why not offered?"** (S): IContextMenu entry on a targeted NPC "Tsukimichi: quests here (N)" opening the window on that NPC's quests with blockers; `/tsuki why <quest>` prints blockers to chat; curated notes for known quirks (Up In Arms optional with the Zenith; "Bloodsworn" reads "Allied" after 7.0).
- **P10 Abandoned ledger** (S): `characters/<ContentId>.abandoned.json`, dashboard section, filter chip, chat line at the moment of abandonment.
- **P11 Seasonal now** (S): "Event quests running now" in the Todo overlay and dashboard from the evaluator's Active flag; per-character seasonal history by year; login notice; end dates only as optional static data (V2-18 without polling).
- **P12 Export** (S): Settings and `/tsuki export` write completed quest ids and Moonlit obtained state as JSON and plain lists; format documented for xiv-shinies, FFXIV Collect and spreadsheets.
- **P4 Story sidequests preset** (S–M): artwork flag + derived per-zone side-story chains; book badge on rows; chain N of M in the detail pane.
- **P8 Patch of origin** (M): DataGen seeds `AddedIn` from the wiki's per-patch lists (2.0–7.5) then maintains it by diffing successive game data; "Added in 7.5x" chip; "New this patch" group in the Feature quests preset.

### Release 1.0.0 — player-driven features, second set

- **P3 "Clear my blues" plan** (M): undone feature quests by expansion then zone in story order, tagged by unlock kind, Copy as checklist, Todo pin of the current block.
- **P6 Unlock route for alts** (S–M): pick a job, duty, system or Moonlit reward and get the ordered remaining quests with level gates; Copy route; Pin all.
- **P5 "Before you continue" payoff gates** (M): curated milestone → optional-content pairs shown spoiler-free on the MSQ line and dashboard with a one-time notice (Eden before 6.5, ShB role quests before 5.0's climax, Eureka before Dawntrail's Krile arc, Hildibrand before Manderville relics, the hidden quest after Pilgrim's Traverse floor 100, Bozja before Garlemald).
- **P7 "Since you were away" card** (M): after N days (default 14): last capture, mid-way quests then and now, MSQ then and now, quests the catalog gained, events live now, job levels changed.
- **P13 Duty Finder unlock hint** (M): beside a greyed duty, the unlocking quest with its moon and blocker, same hook pattern as the item hover hint.
- **P9 Journal text reader** (M): per-step journal entries from the client's quest text sheets for completed quests; opt-in lazy full-text search.

### Later
- **P14 Branching MSQ readiness** (L) before 8.0 Evercold (January 2027): per-route ordering from the prerequisite graph, "MSQ: route A 3/9 · route B not started" and a synthetic-branch test fixture.
- V2-16 IPC provider, V2-17 Questionable cross-check, V2-19 localization, V2-20 official repository submission (needs decision 7).

## 4. Order and sizing

| Release | Tasks | Effort | Why this order |
|---|---|---|---|
| 0.6.0 | T1–T8 | ~2 weeks of agent work | Correctness first: totals, states and levels are what every later view displays; the guard and hover items are the owner's direct asks |
| 0.7.0 | T9–T12 | ~1 week | Fixes the "partial moons are hard to see" complaint on its own, visible in the first build, no structural risk |
| 0.8.0 | T13–T17 | ~2 weeks | The "beautiful and modern" delta, behind a Classic toggle so the tutorial and light themes survive |
| 0.9.0 | P1, P2, P10, P11, P12, P4, P8 | ~2 weeks | Small features with the strongest evidence, all on data the plugin already has |
| 1.0.0 | P3, P6, P5, P7, P13, P9 | ~3 weeks | Larger features; P5 and P8 need curated seed tables |

Execution method, as before: one implementer agent per task in an isolated worktree, coordinator merges, bug-hunter review, release pipeline; the owner verifies in game after each release. Step-level implementation plans (docs/superpowers/plans/…) are written per release once this proposal is approved.

## 5. Things this plan deliberately leaves out
- Allowance timers, society rank grinds, aether-current field markers, party-shared quest views, any automation or auto-travel (8.1 does it natively; the official repository rejects it).
- Web polling from inside the plugin for event dates.
- A new journal category for Unlisted: the evidence says the quests belong to existing genres or are retired.
