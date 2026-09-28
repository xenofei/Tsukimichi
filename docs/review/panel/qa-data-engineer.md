# V3 proposal review: verification rigor and release risk

Reviewer role: QA lead and data engineer. Date: 2026-09-28. Scope: docs/feature-plan-v3.md (0.6.0 tasks T1–T8, T2a, T10, release order), docs/data/{unlisted-report, verification-report, verification-report-2}.md, Tsukimichi.DataGen/{Verifier, XivApi, Program, CuratedOverlay}.cs, Tsukimichi.Tests, .github/workflows/release.yml, tools/make_pluginmaster.py, Tsukimichi/Data/curated/*. Read-only; no repo file was changed.

Facts checked in the tree before writing (not taken from the plan):

- Tests: 507 test methods (597 cases with Theory rows); 27 `[GameDataFact]` methods in 6 files, all skipped unless `TSUKIMICHI_GAME_PATH` is set. Tsukimichi.Tests references only Core and GameData; nothing in `Tsukimichi/` (plugin) or `Tsukimichi.DataGen/` is testable today.
- CI: `.github/workflows/` holds `release.yml` only. It triggers on `v*` tags, builds `Tsukimichi/Tsukimichi.csproj` alone, runs no tests, and does not check that the csproj `Version` (0.5.0.0) matches the tag. `Directory.Build.props` has `TreatWarningsAsErrors=false`. The "0 warnings, tests green" gate exists only as an agent habit.
- Snapshot: `CharacterSnapshot.CurrentSchemaVersion = 1`, the migrator registry is empty, `JsonSnapshotStore` quarantines a file whose version is newer than the build, and ignores unknown JSON properties (`Unknown_properties_are_ignored_on_read`).
- `otherSource` is a substring of the free-text `UniqueRewardEntry.Source` string. No code under `Tsukimichi.Core`, `Tsukimichi.GameData` or `Tsukimichi` reads it (grep: zero hits). Only DataGen's Verifier parses it for its report.
- Curated: `feature_quests.json` equals `keys(system_unlocks) ∪ keys(duty_unlocks)` today (101 = 101) but no test enforces it; `unique_quests.json` contains `70011 DutyUnlock 808 [Curated]` (the T4 target); `festivals.json` is empty; CuratedOverlay (DataGen) parses with comments and trailing commas allowed while the README demands strict JSON.
- DataGen `--verify` runs in 6 s, samples 48–64 of 3,464 entries against xivapi (about 1.5 %), and xivapi is derived from the same sheets as the catalog.

## 0. Top findings

1. **No CI exists.** Every quality gate in the plan ("tests green, 0 warnings") is unenforced. Add a PR workflow before T1 lands (section 5). Cost: half a day. Without it, the 0.6.0 gate is a promise, not a check.
2. **The tag workflow can ship a no-op release.** A tag `v0.6.0` on a commit whose csproj still says `0.5.0.0` produces a pluginmaster with `AssemblyVersion 0.5.0.0`; Dalamud sees no update and nobody is told. Add a version-equals-tag guard and a "changelog section exists" guard to `release.yml` (section 5).
3. **T5 promises "one test per fix" for code that cannot be tested.** SessionState, StatePoller, SnapshotService, Configuration, GameStateReader live in the Dalamud-dependent plugin project, which the test project cannot reference. Either extract the logic (an `EvalContextBuilder`, a `RecentEventsTracker`, a `LoginReadiness` rule, a `ConfigRecovery` helper) into Core, or rewrite the T5 test line honestly. Same for T6's `UiMetrics` (plugin) versus `ScaleMetrics` (Core).
4. **T4 is built on a field nothing reads.** `otherSource` exists only inside the `source` string. "MoonlitPane and RewardTooltip show also on the Online Store" needs either a parser of that string or, better, a structured `otherSources` array on `UniqueRewardEntry` (loader tolerant when absent). Decide before T4 starts; add the parser/loader test either way.
5. **T3's snapshot change has a rollback trap.** A v2 file read by a 0.5.0 build is quarantined (moved aside), so rolling back 0.6.0 strands every stored character until each logs in again. Because the store already ignores unknown properties, T3 should be **additive and keep schema version 1** (new `festivalPhases`, `satisfactionRanks`, `carrierLevel` fields with empty defaults) instead of retyping `ActiveFestivals`. If a version bump is still wanted, write a `<id>.v1.bak` once before the first v2 write (section 3).
6. **T1's regression net is a golden file, not pinned counts.** "80 refiled / 100 retired / 1 unlisted" breaks on every patch and says nothing about *which* quest moved. Commit `docs/data/refile-expected.csv` (rowId, rule, targetGenre) from the probe's `rules.psv`, and make the data-driven test print the row-level delta. Also define the rule-6 tie-break now (the vote is unanimous today, the code must still decide).
7. **T2a should run before T1–T4, not alongside them.** The plan says discrepancies found by T2a are "fixed in T2–T4", which is circular when they are the same release. T2a's automated phase (Garland, FFXIV Collect, wiki: about one hour of fetching) is read-only and can start today; the Lodestone crawl (about three hours, slow and fragile) can follow. Put T2a in a separate `Tsukimichi.Verify` project, not in Verifier.cs (section 1).
8. **0.6.0 is too big to diagnose.** It changes what every count means (T1), what Ready means (T3, T5 festival context, T5 daily offer), the reward file (T4), the snapshot (T3), and 15+ UI behaviours (T6). Split: 0.6.0 = T2, T4, T5, T7, T8 (no schema, no refiling, small blast radius); 0.6.1 = T1 behind a "Classic filing" toggle with the golden test; 0.6.2 = T3 after the in-game ClientStructs checks (section 4).
9. **DataGen has zero tests and its known-answer set does not cover the one curated fix planned.** Add `Tsukimichi.DataGen.Tests` (or `InternalsVisibleTo`) for CuratedOverlay and the new online_store overlay; add `70012 -> CFC 808` (and `70011` not) to `Verifier.KnownAnswers` and `Program.SanityChecks`.
10. **Curated data has no cross-file invariants.** `feature_quests.json` is a hand-maintained derived file; the new `retired_quests.json` / `refile_overrides.json` have no schema yet; the runtime never compares `unique_quests.json`'s `gameVersion` with the client's. One `CuratedInvariantsTests` class fixes all of it (section 6).

## 1. T2a: full-catalog verification with references

### 1.1 Sources

| Source | Endpoint | Facts it can confirm | Independent of the sheets? | Rate / terms | Volume for 5,373 quests |
|---|---|---|---|---|---|
| Garland Tools | `https://www.garlandtools.org/db/doc/quest/en/2/<row>.json`; index `db/doc/browse/en/2/quest.json` | name, level, genre, prerequisites with joins, rewards, `patch`, instance partials | **No** for genre/prereqs/rewards (derived from the same sheets); **yes** for patch attribution and the hand-linked instance partials | No published limit or ToS; fan site. Use 2 req/s, 1 connection, a descriptive UA, and never re-fetch a cached row | 5,373 requests, ~5 KB each (~27 MB) |
| Lodestone Eorzea Database | search `…/lodestone/playguide/db/quest/?q=<name>` then the page `…/db/quest/<hash>/` | displayed level, class/job text, "Quest/Duty" prerequisites (duties only), rewards, per-category totals | **Yes** (official, human-facing) | No API. Requires a UA (report). SE's terms restrict automated access; treat as a one-time cited crawl at ≤ 1 req/s, cached forever, never in CI. Confirm the current terms before the full run | 2 requests per listed quest (search + page) ≈ 10,300, ~60 KB pages (~300 MB) |
| consolegameswiki | MediaWiki API `api.php?action=query&prop=revisions&rvprop=content&titles=A\|B\|…` (50 titles per call) | level, requirement text, "removed in patch", acquisition line on item pages, category membership | **Yes** (human-written; community, so lower trust) | Standard MediaWiki etiquette: serial requests, UA, `maxlag=5` | ~110 batched calls for quests + ~10 for the 249 collectible item pages |
| FFXIV Collect | `https://ffxivcollect.com/api/{mounts,minions,emotes,orchestrions,bardings,hairstyles,triads,fashions,ornaments}` (paginated) | `sources[]` per collectible (Quest / Premium: Online Store / Event / …) | **Yes** for store and event availability | Public JSON API, paginated; ≤ 1 req/s | < 100 requests, whole dumps |
| xivapi v2 | already in `XivApi.cs` | that Lumina read the sheet correctly | **No** | Batched 50 rows per call | Optional; keep for the reward sample only |

Independence is the design constraint: Garland and xivapi agreeing with the catalog proves the mapper read the sheet, not that the sheet is what the game shows. A discrepancy is only "catalog wrong" when the sheet itself proves it (as with `QuestLevelOffset`) or when two of {Lodestone, wiki, Collect} agree against the catalog.

### 1.2 Caching

- On-disk cache `verify-cache/<source>/<key>.json` plus a sidecar `<key>.meta.json` (`url`, `fetchedUtc`, `httpStatus`, `etag`, `contentSha256`). Keys: Garland row id; Lodestone the quest hash (and a `search/<normalized name>.json` entry); wiki the page title; Collect the kind and page number.
- Never expire by time. Expire by game version: the cache root is `verify-cache/<gameVersion>/`; a new patch starts a new folder (copy forward with `--reuse-from <version>` for sources that do not lag, then re-fetch only rows whose sheet hash changed).
- `--offline` fails on a cache miss instead of fetching, so reruns are free and deterministic.
- Do not commit the cache (~350 MB). Commit a `verify-cache.manifest.json` (key → sha256, fetchedUtc, status) so a reviewer can tell which fetch produced which verdict. Commit the CSVs.

### 1.3 Per-quest reference encoding (CSV schema)

Two files, both sorted by row id then fact then source, no timestamps in-row (timestamps churn the diff; they live in the manifest).

`docs/data/quest-verification.csv`, one row per (quest, fact, source): the long form is what makes diffs readable.

| Column | Meaning |
|---|---|
| `rowId` | Quest row id |
| `name` | Catalog name (glyphs stripped) |
| `fact` | enum: `name`, `displayLevel`, `rawLevel`, `classJob`, `genre`, `prereqs`, `prereqJoin`, `questLocks`, `duties`, `rewards`, `patch`, `listed`, `retired`, `satisfactionRank`, `carrierLevel`, `festivalPhase` |
| `catalog` | The catalog's value, canonical text (ids sorted, `;`-joined) |
| `source` | enum: `garland`, `lodestone`, `wiki`, `collect`, `sheet` |
| `sourceValue` | The source's value in the same canonical text, or empty |
| `sourceRef` | URL or `sheet:Quest.<column>` |
| `verdict` | enum: `match`, `catalogWrong`, `sourceWrong`, `sourceLagging`, `notModeled` (source cannot express the fact), `notListed` (source lacks the quest), `ambiguous` (name collision), `unresolved` |
| `reason` | Short text; required when the verdict is not `match` |
| `fixedIn` | Task id or release when `catalogWrong` (`T2`, `T3`, `T4`, `0.6.1`) |

`docs/data/quest-verification-summary.csv`, one row per quest, generated from the long file: `rowId, name, garland, lodestone, wiki, worst`, where each source column is the worst verdict for that source. This is the "one row per quest" the plan asks for; it is derived, never edited.

`docs/data/reward-verification.csv`, one row per `unique_quests.json` entry: `questRowId, kind, rewardId, itemId, rewardName, collectId, collectSources, wikiAcquisition, catalogOtherSource, verdict, reason, fixedIn`.

`docs/data/verification-allowlist.json`: `{ "<rowId>:<fact>:<source>": { "verdict": "sourceLagging", "reason": "...", "evidence": "<url>", "until": "7.6" } }`. Acceptance for T2a is then mechanical: **no row with verdict `unresolved` or `catalogWrong` outside the allowlist**, and every allowlist entry still applies (`until` not passed).

Canonicalization before comparison: strip private-use glyphs (Verifier already does), NFC, case-insensitive, straight quotes, collapse whitespace; levels as integers with `displayLevel = Level + LevelOffset`; prerequisites as sorted row-id sets (Lodestone and the wiki give names, so resolve name → row id and mark `ambiguous` when the name has variants: the eight Close to Home rows, the three Grand Company variants, both It's Probably Pirates).

### 1.4 Discrepancy classification (decision table)

| Situation | Verdict |
|---|---|
| Sheet column proves the source (e.g. `Level + Offset` equals the Lodestone) | `catalogWrong` |
| Two independent sources agree against the catalog | `catalogWrong` |
| One independent source disagrees, the other agrees with the catalog, sheet agrees with the catalog | `sourceWrong` |
| Source lacks the row and the row id is above the source's highest known row, or the row's `patch` (Garland) is newer than the source's patch banner (Lodestone shows "Patch 7.56") | `sourceLagging` |
| Source has the row but the fact is outside its model (Lodestone: carrier level, satisfaction rank, festival phase; Garland: retired flag) | `notModeled` |
| Source returns no hit for a name that has several rows | `ambiguous` |
| Anything else | `unresolved` (must be triaged into the allowlist or a fix) |

Retired rows (the 99 + 8) are compared only on `retired` against the wiki's "removed in patch" text; every other fact is `notModeled` for them so they do not pollute the counts.

### 1.5 Repeatable per patch, diffable

- CLI: `Tsukimichi.Verify --game <sqpack> --cache <dir> [--offline] [--sources garland,wiki,collect,lodestone] [--only <rowId,…>] [--since docs/data/quest-verification.csv]`.
- `--since` prints only rows whose verdict changed versus the committed file, grouped by verdict, with the row-level evidence. That is the per-patch triage view.
- Patch flow: new sqpack → `DataGen` regen → `Verify --sources garland,wiki,collect` (fast) → triage the `--since` delta → update allowlist → commit CSVs and manifest → `Verify --sources lodestone` when the Lodestone has caught up (weeks later) → second triage. The lag allowlist gets an `until` so entries retire themselves.
- Determinism: fixed sort order, no timestamps in the CSV, cache-only reruns.

### 1.6 Where it lives

A separate console project `Tsukimichi.Verify` (referencing Core, GameData and the DataGen `GameSheets` via a shared project or a project reference), not new code in `Verifier.cs`:

- `Verifier.cs` is already a single 902-line class with one job: gate a regenerated `unique_quests.json` in 6 seconds. T2a is a multi-hour, multi-source crawl with HTML parsing (AngleSharp or HtmlAgilityPack for the Lodestone), a cache layer and CSV diffing. Different cadence, different dependencies, different failure modes.
- Keep DataGen's `--verify` as the post-regen gate and add to its known answers (section 6). Do not let it grow an HTTP crawler.
- `tools/` is for the Python release helper; a C# project keeps Lumina access and the `CatalogMapper` in one language.

### 1.7 Run time and failure modes

Estimate (single-threaded, polite):

| Phase | Requests | Rate | Wall clock |
|---|---|---|---|
| Garland per-quest JSON | 5,373 | 2/s | ~45 min |
| Wiki batched raw pages | ~120 | 1/s | ~2 min |
| FFXIV Collect dumps | < 100 | 1/s | ~2 min |
| Lodestone search + page | ~10,300 | 1/s | ~2.9 h, plus retries |
| Compute and CSV from cache | 0 | – | < 1 min |

First full run about 4 hours; every rerun from cache under a minute.

Failure modes to design for:

- Lodestone search misses: 3 of 28 names in the report (glyph-prefixed names, seasonal pages with a different layout). Mitigation: search by stripped name, fall back to name plus level, then mark `notListed`.
- Name collisions: city and Grand Company variants share a name. Disambiguate by area and level from the search results; else `ambiguous`.
- Wiki: 404s (7 of 27 in the report), redirects, disambiguation pages, page collisions (Pitch Perfect). Follow redirects once; treat disambiguation as `ambiguous`.
- HTML layout drift on the Lodestone: parse with tolerant selectors and assert a minimum field count per page; a parse yielding fewer than N fields is `unresolved`, not `match`.
- HTTP 429 or a ban mid-run: token bucket, exponential backoff, hard stop after 10 consecutive failures, resume from cache.
- Time-dependent facts: Collect's "Premium" flag changes when the store re-lists an item. Stamp `fetchedUtc` in the manifest; the reward CSV is a snapshot, not a truth.
- The existing sheet-only facts: Garland reports `genre 0` for unlisted rows and no retired marker; do not count Garland toward `retired`.
- Git churn: the long CSV is ~5,373 × ~4 facts × ~3 sources ≈ 60k rows (~8 MB). Acceptable, but keep it sorted and timestamp-free or every rerun rewrites the file. If size matters, commit the summary CSV and gzip the long one.

### 1.8 Reward verification specifics

- Prefer an id join with FFXIV Collect where its API exposes an item id (check `item_id` on mounts, minions, orchestrions); fall back to normalized name and a small alias table (the three names that needed shorter queries: "Endwalker - Footfalls", "Where Daemons Abide", "Clowning Around").
- Add a check that every quest with `SystemReward[1] != 0` (49 rows) is either in `system_unlocks.json` or in an explicit ignore list with a reason; report §3 says only 11 of 49 are covered.

## 2. Test strategy gaps

Baseline conventions to follow: xunit, `Fixture.Quest(...)` with `with { }` overrides, `QueryTestData`, `[GameDataFact]` for sheet-backed facts, `TempDir` for storage tests.

### 2.1 What lacks a regression test today

| Task | Gap |
|---|---|
| T1 | No refiler exists; the plan's tests are pinned totals only (patch-sensitive, not row-level). Rule-6 tie-break undefined. Retired handling in the evaluator (a `Retired` requirement kind is suggested in the report but absent from the plan). |
| T2 | Plan's tests are fine but need the filter, sort and hint paths listed explicitly. |
| T3 | New requirement kinds have no tests yet; `SnapshotDiff` compares festival id lists, so a phase change would not trigger re-resolution; "not checked" semantics for missing data undefined. |
| T4 | DataGen has no tests; `otherSource` has no reader; known answers do not cover 70012. |
| T5 | Most fixes are in the plugin project and cannot be tested from Tsukimichi.Tests. |
| T6 | `UiMetrics` is in the plugin; only `ScaleMetrics` (Core) is testable. |
| T7 | `ModifierGate` is new; fine as planned. |
| T10 | `GaugeGeometry` is new; plan lists one test. |

### 2.2 Proposed tests

**T1, `Tsukimichi.Tests/Data/JournalRefilerTests.cs` (pure, synthetic catalogs):**

| Test | Input | Expected |
|---|---|---|
| `Rule1_placeholder_issuer_marks_retired` | quest genre 0, `IssuerNpcId 1034221` | `IsRetired`, `RefiledFrom == 1`, journal unchanged |
| `Rule1_hidden_flag_marks_retired_without_issuer` | genre 0, hidden bool true, issuer normal | `IsRetired` |
| `Rule1_applies_to_listed_rows_named_in_retired_quests_json` | quest genre 18 listed; curated retired set contains it | `IsRetired`; `TreeCounts` genre 18 total excludes it; completed retired still `Completed` when `includeRetired` |
| `Rule2_class_intro_takes_first_listed_successor_genre` | `InternalId "ClsGla001_00177"`, successor genre 156 | genre 156, `RefiledFrom == 2`, `IsFeature` (EventIconType 10) |
| `Rule2_regex_rejects_ordinary_class_quests` | `"ClsGla002_…"`, `"JobDrk300_…"` | not matched |
| `Rule3_grand_company_maps_to_233_plus_gc` | `GrandCompany 2` | genre 235 |
| `Rule4_walks_unlisted_prereqs_in_slot_order_same_expansion` | A(unlisted) → B(unlisted) → C(listed G27, exp 3); target exp 3 | G27. With C exp 0: rule 4 skipped, falls through |
| `Rule5_uses_successor_then_questlock` | no listed prereq; successor listed G108 | G108; with only a QuestLock to G107: G107 (decide whether the expansion constraint applies to locks; the report recommends dropping it) |
| `Rule6_territory_vote_dominant_genre` | territory 628: three listed quests G128, G128, G124 | G128 |
| `Rule6_tie_stays_unlisted` | territory with G128 (1), G124 (1) | unlisted, `RefiledFrom == 7` |
| `Rule7_no_signal_stays_unlisted` | issuer without territory | unlisted |
| `Overrides_win_over_rules` | 68478 in `refile_overrides.json` → 90 | G90, `RefiledFrom == 0` (override) |
| `Refiled_quest_keeps_assigned_flag_for_detail_pane` | any rule 2–6 hit | `IsUnlisted` false, `IsAssigned` true (or whatever flag the detail pane reads) |
| `SortKey_ranks_refiled_quest_inside_new_genre` | rule-2 quest | `Journal.SortKey` within the genre's range; `ByGenre[156]` contains it |
| `Rules_apply_in_order_retired_wins_over_class_intro` | `ClsGla001` row with issuer 1034221 | retired |

**T1, data-driven (`[GameDataFact]`):**

- `Refiler_outcome_matches_committed_expected_csv`: reads `docs/data/refile-expected.csv` (rowId, rule, genre; 180 rows from the probe's `rules.psv`) and prints every differing row before failing. This is the regression net; it also feeds CI once the catalog fixture exists (section 5).
- `Every_quest_lands_in_exactly_one_node`: for all 5,373, exactly one of {listed genre node, retired bucket, unlisted bucket}.
- `Totals_match_lodestone_counts_within_known_lag`: sections 0–7 against the numbers in verification-report-2 §5 with the explained diffs subtracted. Keep the expected numbers in one `ExpectedCounts` constants file stamped with the game version so a patch bump is one edit.
- `FeaturePresetsTests.Event_icon_type_ten_is_a_feature_quest` (synthetic) plus the data-driven count 58 + 2.
- `All_QuestLock_joins_are_any` as the plan says.

**T2, `QuestQueryFilterTests` / `QuestQueryScopeAndSortTests` / evaluator:**

- `DisplayLevel_is_level_plus_offset` (unit on the record).
- `Level_range_filter_uses_display_level`: quest Level 1, Offset 2; filter min 3 → included; filter max 2 → excluded.
- `Sort_by_level_uses_display_level`: Level 4/Offset 5 sorts after Level 8/Offset 0.
- `Evaluator_level_gate_uses_raw_level`: Level 1/Offset 2, character level 1 → `Level` requirement met, `Ready`.
- `SearchIndex_and_todo_hint_show_display_level` where a pure formatter exists.
- Data-driven: `Quarrels_with_squirrels_displays_3_and_is_ready_at_1`; `Offset_rows_count_is_215` (patch-sensitive, in `ExpectedCounts`).

**T3, `RequirementEvaluatorTests` / `StateResolverTests` / `SnapshotDiffTests`:**

- `CustomDeliveryRank_blocked_below_required`: snapshot `SatisfactionRanks {2: 3}`, quest `SatisfactionNpc 2, SatisfactionLevel 4` → `Blocked`, detail "satisfaction rank 4 with M'naago (have 3)".
- `CustomDeliveryRank_ready_at_required`: `{2: 4}` → met.
- `CustomDeliveryRank_not_checked_when_snapshot_has_no_ranks`: null map (v1 snapshot or reader unavailable) → pass-through with "not checked", same as Mount/House. Present map with no key → rank 0 → `Blocked`.
- `CarrierLevel_blocked_below_and_met_at`; `CarrierLevel_not_checked_when_null`.
- `Seasonal_requires_active_id_and_phase_in_window`: active `{(172, 1)}`, quest Begin 2 End 5 → `Blocked` "chapter not yet open"; phase 3 → met; phase 6 → `Blocked` "chapter closed".
- `Seasonal_with_zero_begin_and_end_accepts_any_phase` (backward compatibility for the 257 rows with End only, and for a v1 snapshot without phases).
- `Active_festival_with_chapter_one_completed_keeps_chapter_two_blocked_not_foreclosed`: `FestivalIsPast` must not fire while the id is active.
- `SnapshotDiff_reports_festival_change_on_phase_change_only`: same ids, phase 1 → 2 → `ChangedFestivals` contains 172; identical tuples → empty.
- `StateResolver_incremental_re_resolves_festival_quests_on_phase_change`.
- Data-driven: `Sixteen_satisfaction_quests_carry_npc_and_level`, `Seventeen_postmoogle_quests_carry_delivery_level_1_to_12`, `Hatching_tide_2014_rows_have_begin_1_to_4_end_5`.

**T4, new `Tsukimichi.DataGen.Tests` (or `InternalsVisibleTo("Tsukimichi.Tests")`):**

- `OnlineStore_overlay_adds_other_source_to_matching_entries`: fake generator with entries for item 21050; `online_store.json` lists 21050 → entry's other sources contain `OnlineStore`; an id not in any entry → warning line, not a crash.
- `OnlineStore_overlay_rejects_non_numeric_keys_with_warning`.
- Runtime: `UniqueRewardsFile_reads_other_sources_array_and_tolerates_absence`; `RewardSourceTests.Parse_semicolon_source_yields_other_sources` if the string is kept.
- `Verifier.KnownAnswers` and `Program.SanityChecks`: `70012 -> DutyUnlock 808` present, `70011 -> 808` absent, `68546 Starlight Stakeout carries OnlineStore`.
- Data-driven: `Feature_quests_json_equals_union_of_unlock_files` (see section 6; this one is pure and runs in CI).

**T5 (after extraction into Core):**

- `TribeDailyOffer_accepting_one_daily_does_not_block_other_dailies`: `Accepted` contains daily X; dailies Y, Z of the same tribe → no `TribeDailyOffer` failure on Y, Z (the failing input from bug-hunt-0.5.0-core).
- `EvalContextBuilder_populates_festival_ends_and_achievement_gate_from_catalog`: never-seen festival with a curated end in the past → `FestivalIsPast` true → `Foreclosed`; without an end → `Blocked`, not counted in totals (assert through `TreeCounts`).
- `RecentEventsTracker_clears_on_content_id_change`.
- `AcceptedSinceTests.Reconcile_ignores_all_zero_mask_with_empty_journal`.
- `SnapshotDiffTests.SameSet_allocates_nothing_for_equal_sorted_inputs` (measure with `GC.GetAllocatedBytesForCurrentThread`, pattern from `QueryPerfTests`).
- `ConfigRecovery_copies_corrupt_file_aside_before_reset` (reuse `AtomicFile.TryQuarantine`; assert the `.corrupt-<stamp>` file exists and the reset config loads).
- `Wotsit_partial_batch_retry` only if the batching logic is extracted; otherwise list it as owner-verified.
- `DeleteAllData_and_ForgetCharacter_keep_poller_memory_and_disk_in_step`: testable only through an `ISnapshotStore` fake if the poller's memory is a Core type; else owner-verified.

**T6:** move `MinWindowSize(uiScale)` into `ScaleMetrics` and test `Min_window_size_scales_linearly_and_never_below_base`; the rest stays an owner checklist (add it to `docs/ui-smoke-checklist.md` with the three scales named).

**T7, `ModifierGateTests`:** `Enabled_only_when_configured_modifier_held` (Shift true/false, Ctrl setting variant), `Tooltip_text_names_the_modifier`; `UniqueRewardCatalogTests.Hidden_overrides_are_exposed_for_the_yours_filter`; `OverridesFile_round_trips_hidden_entries_with_notes`.

**T10, `Tsukimichi.Tests/Ui/GaugeGeometryTests.cs`:**

| Test | Expected |
|---|---|
| `Arc_angles_are_strictly_increasing_with_floor` (Theory over R ∈ {6, 7, 8, 12, 18, 23}) | fractions 0, 0.03, 0.1, 0.5, 0.97, 1 → angles strictly increasing; 0 → exactly 0; 1 → exactly 2π |
| `Floor_epsilon_matches_spec` | R 7, stroke 1.5: ε = max(0.06, (1.5 + 1.5) / (2π · 0.80 · 7)) = 0.0853 (4 d.p.); R 23, stroke 2.5: ε = max(0.06, 4/(2π·18.4)) = 0.06 |
| `Fraction_below_floor_renders_floor_not_zero` | 0.01 → arc length equals ε · 2π; 0 → 0 |
| `Arc_starts_at_twelve_oclock_clockwise` | start angle −π/2; 0.25 ends at 0 (3 o'clock) |
| `Cap_positions_lie_on_track_radius` | \|cap − centre\| = 0.80 R within 1e-4 for both caps |
| `Core_radius_is_zero_below_R7_and_positive_above` | R 6 → 0; R 8 → > 0 and < 0.80 R − stroke |
| `Out_of_range_and_nan_fractions_clamp` | −0.1 → 0; 1.2 → 2π; NaN → 0 |
| `Near_complete_is_distinguishable_from_complete` | at R 8 the gap between 0.97 and 1 is ≥ 1 px of arc length; 1 closes the ring (no cap gap) |
| `Monotone_over_fine_grid` | 1,000 fractions at R 12 never decrease |

**T9 (cheap, worth adding now):** `ContrastTests.Relative_luminance_matches_wcag_known_pairs` (black/white 21:1, `#2C334A` on the window colour equals the 1.47:1 the design report measured) and a table test that every new token pair claimed AA passes 4.5:1.

## 3. Snapshot schema migration (T3)

### 3.1 Recommendation: additive, no version bump

`JsonSnapshotStore` already ignores unknown properties on read and every property has a default. If T3 adds `IReadOnlyDictionary<ushort, ushort> FestivalPhases`, `IReadOnlyDictionary<byte, byte> SatisfactionRanks` and `byte? CarrierLevel` while leaving `ActiveFestivals` as the `ushort` list:

- a 0.5.0 build reads a 0.6.x file (extra properties ignored) — rollback is safe;
- a 0.6.x build reads a 0.5.0 file (new properties default to empty) — upgrade needs no migrator step;
- "phase unknown" is `FestivalPhases` lacking the id → the evaluator falls back to id-only, which is exactly today's behaviour.

Bump `CurrentSchemaVersion` only when a field changes meaning. If the team prefers the tuple redesign and a bump to 2 anyway, then: register the v1 → v2 step (first real use of the registry), and write `<ContentId>.v1.bak` once before the first v2 save so a rollback can restore it. Without that, rollback quarantines every stored character.

### 3.2 Migration test plan

| Test | What it proves |
|---|---|
| `V1_fixture_loads_with_new_fields_defaulted` | A frozen `Tsukimichi.Tests/Fixtures/snapshots/v1-0.5.0.json` (captured **before** T3 changes the model; redacted ContentId 1) loads; `ActiveFestivals` preserved; `FestivalPhases` empty; `SatisfactionRanks` empty; `CarrierLevel` null; no warnings |
| `Save_then_Load_round_trips_every_field` (extend the existing test) | new fields survive |
| `Load_does_not_rewrite_the_file` | a v1 file stays byte-identical after `Load`; only `Save` rewrites |
| `Newer_file_is_quarantined_not_deleted` (exists as `Missing_migration_step_quarantines_file`; keep) | the rollback failure mode is visible in a test, so the .bak decision is deliberate |
| `Migration_step_is_idempotent` (only with a bump) | applying the step twice equals once |
| `Partial_document_loads` | `{}` and `{ "contentId": 1 }` deserialize with defaults (reflection over record properties: every one has a default) |
| `First_capture_after_upgrade_does_not_reannounce` | `SnapshotDiff` between the v1-loaded snapshot and a fresh capture with the same festival ids and phases reports no festival change; `NoticeTracker` emits nothing |
| `Offline_evaluation_of_v1_snapshot_falls_back_to_id_only_seasonal_check` | the Characters compare view (evaluates stored characters offline) does not flip seasonal quests to Blocked for a character captured before 0.6 |
| `Diff_and_migration_across_two_versions` (later) | chain v1 → v2 → v3 when v3 arrives; add the frozen fixture for each shipped version |

Action before T3 starts: capture and commit the v1 fixture from a real 0.5.0 snapshot (names and ContentId scrubbed). Once the model changes, the old shape can no longer be produced by code.

## 4. Release risk

### 4.1 Is 0.6.0 too big?

Yes. It changes five independent axes at once (filing, evaluator semantics, reward data, snapshot, UI), so when the owner sees "Feature Unlocks went from 101 to 159 and my Starlight quest turned Blocked" there is no way to attribute it. The bug-hunt items also contain three "plausible, verify in game first" fixes, which is a second-order unknown inside the same release.

Proposed split (numbers are a suggestion; the point is the grouping):

| Release | Tasks | Why together |
|---|---|---|
| 0.6.0 | T2, T4, T5 (confirmed items only), T7, T8, T6 | Small blast radius: display and data corrections, tested bug fixes, owner's two asks. No schema change, no filing change |
| 0.6.1 | T1 + T2a phase 1 results, behind `Configuration.JournalFiling = Refiled \| Classic` (default Refiled) | One axis: where quests sit. Golden test plus Lodestone totals gate. The toggle is the in-field rollback |
| 0.6.2 | T3 + T5 "plausible" items | One axis: what Ready means. Requires the in-game ClientStructs checks (`SatisfactionSupplyManager`, carrier level, `ActiveFestivals[i].Phase`) to be done first, and the frozen v1 fixture committed |
| 0.7.0 | T9–T12 as planned | |

If the owner wants a single 0.6.0 number, use Dalamud's testing channel: `make_pluginmaster.py --testing` writes `TestingAssemblyVersion` and a distinct `DownloadLinkTesting`, `IsTestingExclusive: true` for the refiling build; the owner opts in in-game, verifies, then the same tag is promoted to the install link.

### 4.2 Gates

- Build 0 warnings enforced by `-warnaserror` in CI (or `TreatWarningsAsErrors=true`), tests green in CI, not on a laptop.
- T1: `refile-expected.csv` golden equal; totals equal Lodestone per section after the known-lag adjustment; every retired-and-completed quest still reads `Completed` with the setting on; the owner's screenshot numbers match the predicted ones (Class & Job +23, Feature Unlocks +58, "Retired and hidden" 100, Unlisted 1).
- T3: in-game checklist (phase read non-zero during a live event; satisfaction rank matches the Custom Deliveries window; carrier level matches the Delivery Moogle NPC text) recorded in `docs/ui-smoke-checklist.md` before tagging.
- T4: `DataGen --verify` 0 failed with the new known answers; `unique_quests.json` diff reviewed (expected: 1 moved DutyUnlock, 68 entries gain `OnlineStore`, nothing else).

### 4.3 Rollback story if 0.6.x misfiles quests

Three layers, cheapest first:

1. **User-side, no release.** `JournalFiling = Classic` restores 0.5.0 filing at runtime (the refiler is a pure pass over `QuestRecord`s; keep both outputs available or rebuild the catalog on toggle). `refile_overrides.json` pins a single quest to a genre or to `"unlisted"`, so a data-only hotfix fixes one misfile.
2. **Data hotfix release.** Bump patch version, ship only curated JSON changes; no code.
3. **Code revert release.** Dalamud only installs a *higher* version, so "re-point pluginmaster at v0.5.0" does not roll users back. The procedure is: branch from the last good tag, `git revert` the offending merges, bump to the next patch version, tag. Document it in `docs/release-process.md` and add a `workflow_dispatch` "republish" job to `release.yml` that regenerates `pluginmaster.json` for a given existing tag (for the case where the pluginmaster push failed or was wrong) without rebuilding.

What is and is not at risk: completion is read from the client each login, so a misfile never loses progress; pins are keyed by row id, unaffected; `NoticeTracker` may re-announce when totals shift (the T5 first-pass guard should also cover "first pass after a plugin version change"); stored-character snapshots are safe only with the additive T3 design (section 3).

## 5. CI

### 5.1 On pull request and push to main (`ci.yml`, new)

Runs on `ubuntu-latest` for the test projects (Core, GameData, Tests, DataGen need only Lumina) and `windows-latest` for the plugin build (needs `DALAMUD_HOME`):

1. `dotnet restore Tsukimichi.sln`.
2. `dotnet build Tsukimichi.sln -c Release -warnaserror` (or flip `TreatWarningsAsErrors` to true in `Directory.Build.props`; the plan already treats warnings as failures).
3. `dotnet test Tsukimichi.Tests -c Release --no-build --logger trx` with the results uploaded as an artifact; the 27 `[GameDataFact]` tests skip here (see 5.3 to stop that).
4. Data checks (fast, pure): shipped curated files parse under strict options; `feature_quests.json` union invariant; `unique_quests.json` parses via `UniqueRewardsFile.Load` with zero warnings; `docs/data/refile-expected.csv` parses. All of these can be xunit tests, so step 3 covers them once written.
5. `python -m py_compile tools/make_pluginmaster.py` and a dry run against a fake manifest.
6. Optional: `dotnet format --verify-no-changes`.

### 5.2 On tag (`release.yml`, amended)

- Run steps 1–3 of 5.1 first; a tag must not build what a PR would reject.
- Guard: `Tsukimichi/Tsukimichi.csproj` `<Version>` first three components equal the tag without `v`; fail loudly otherwise.
- Guard: `CHANGELOG.md` has a `## [<version>]` section with at least one bullet; `make_pluginmaster.py` currently drops the changelog silently when the section is missing.
- Pin third-party actions by commit SHA (`softprops/action-gh-release@v2`, `actions/setup-dotnet@v4`) and add `concurrency: release` so two tags cannot race the push to main.
- The pluginmaster push does `git checkout -B main origin/main` and pushes; wrap it in a fetch-rebase-retry loop (3 attempts) because a docs commit landing during the build makes the push fail and the release then exists without a pluginmaster update.
- Add the `workflow_dispatch` republish job from 4.3.

### 5.3 Data-driven tests without game files

Today 27 tests skip in any environment but the owner's machine, and the T1 golden test would join them. Recommendation: a **committed catalog fixture**.

- DataGen gains `--dump-catalog <path>`: serializes the mapped `CatalogBundle` (5,373 `QuestRecord`s plus the names and job-category tables) as `Tsukimichi.Tests/Fixtures/catalog-<gameVersion>.json.gz` (estimated 1–2 MB gzipped; the names repeat and ids compress well). It is test content, not shipped in the plugin package.
- Tests that need only mapped records (refiler, TreeCounts totals, FeaturePresets, chains, job ladders, flight index, T2 offsets, T3 field presence) read the fixture and run everywhere. A `CatalogFixture` class fixture loads it lazily, mirroring `GameDataFixture`.
- The Lumina-backed tests remain `[GameDataFact]` and gain one more: `Fixture_matches_live_sheets` asserts the committed fixture equals a fresh `CatalogMapper.Map` (record equality), so the fixture cannot go stale without the owner's run failing. Regenerating the fixture is a step in the patch flow next to `unique_quests.json`.
- Exposure: the fixture contains quest names, levels and ids, the same class of game text the repo already ships in `unique_quests.json`, so it introduces no new licensing question; keep it out of the plugin output like everything under `Tsukimichi.Tests/`.
- Alternative rejected: trimming to a few hundred quests. The prerequisite closure needed for PathFinder tests grows fast, and the T1 golden test needs the whole population.

## 6. Data-integrity holes and the regeneration flow

1. **`otherSource` is unstructured and unread.** `UniqueRewardEntry.Source` is a free-text `;`-joined string; `otherSource=` inside it is parsed only by DataGen's report. T4 adds `OnlineStore` into the same string and then wants the UI to show it. Add `IReadOnlyList<string> OtherSources` (or a flags enum) to the entry, emit it from DataGen as a JSON array, and make the loader tolerate its absence (older files). Test the loader both ways. Keep `source` as provenance text only.
2. **`feature_quests.json` is a derived file with no invariant test.** README says "regenerate when either file changes"; nothing checks it. Either derive it at load (`FeatureQuests = file ∪ keys(system) ∪ keys(duty)`) or add `CuratedInvariantsTests.Feature_quests_equals_union_of_unlock_keys`. Note the T4 side effect: moving `70011 → 70012` in `duty_unlocks.json` removes 70011 from the union unless it is added to `system_unlocks.json` or kept explicitly; the plan says 70011 "stays the chain's first quest" but not whether it stays a feature quest.
3. **T4's fix is baked into `unique_quests.json`** (`70011 DutyUnlock 808 [Curated]`). The regen is mandatory, and the known-answer set must pin `70012 → 808` and `70011 ↛ 808` (Verifier.KnownAnswers and Program.SanityChecks), otherwise a stale regen passes.
4. **No runtime version check.** DataGen's `--verify` fails when `data.GameVersion != game version`; the plugin never compares the shipped file's `gameVersion` with `IDataManager`'s game version. Log one warning at startup on mismatch and surface it in Settings › Data; test the comparison helper.
5. **New curated files need a schema before code.** `retired_quests.json` and `refile_overrides.json` (T1) and `online_store.json` (T4) should follow the README convention (`note` required) plus an `evidence` URL, and get invariant tests: every key exists in the catalog (data-driven or fixture), no id in both retired and overrides, override target genre exists, `online_store.json` ids all appear in `unique_quests.json` (else a warning that lists them so a regen cannot silently orphan entries).
6. **Strictness mismatch.** The README requires strict JSON; `CuratedOverlay.LoadObject` allows comments and trailing commas; check `CuratedData` uses the same options. Pick one (strict) and add `Shipped_curated_files_parse_under_strict_options` next to the existing `Shipped_curated_files_load_without_warnings`.
7. **`SystemReward[1]` coverage.** 49 rows carry a system-unlock id; 11 are curated. Add the coverage check to T2a (section 1.8) and an ignore list with reasons so the number is reviewed, not forgotten.
8. **Regeneration flow is undocumented as a sequence and produces noisy diffs.** `verification-report.md` embeds a timestamp and is committed on every run. Add `tools/regen.ps1`: DataGen generate → `--verify` → `dotnet test` (data-driven, with `TSUKIMICHI_GAME_PATH`) → `--dump-catalog` fixture → update `docs/data/DATA-VERSION.md` (game version, xivapi version, date) → print the `git diff --stat`. Move the timestamp out of the report body into DATA-VERSION.md so a no-change regen is a no-change diff.
9. **Curated key duplicates are fine, but say so.** `system_unlocks.json` has six repeated labels (city and Grand Company variants); that is by design per the README. `SystemUnlock` entries all have `rewardId 0`, so the `(quest, kind, rewardId)` uniqueness key allows one system unlock per quest. Document the limit.
10. **`festivals.json` is empty**, so `FestivalIsPast` relies on "any quest of that festival completed". With T3 phases this is still correct while the id is active (the past check only runs when the id is inactive), but write the test in 2.2 so it stays that way. T2a is the natural place to seed `festivals.json` end dates from the wiki, which P11 will need anyway.

## 7. Recommendations by task id

| Task | Recommendation |
|---|---|
| CI (new, before T1) | `ci.yml` per 5.1; amend `release.yml` per 5.2 (version and changelog guards, retry on push, republish job, pinned actions) |
| T1 | Golden `refile-expected.csv` plus row-level diff test; rule-6 tie-break decided; `JournalFiling` Classic/Refiled toggle; schema for `retired_quests.json` and `refile_overrides.json` with invariant tests; decide whether retired rows get a `Retired` requirement kind (report §4 rule 1) and test the Foreclosed/Completed outcome |
| T2 | Add the filter, sort and raw-gate tests listed in 2.2 |
| T2a | Separate `Tsukimichi.Verify` project; sources with an independence matrix; cache with manifest; long-form CSV plus derived summary; verdict enum and allowlist with `until`; run phase 1 before T1–T4 and the Lodestone phase later; seed `festivals.json` and the `SystemReward[1]` coverage list while there |
| T3 | Additive snapshot fields, no version bump (or `.bak` if bumped); freeze the v1 fixture now; `SnapshotDiff` must diff phases; define "not checked" for absent ranks; tests in 2.2 and 3.2; in-game ClientStructs checks before tagging |
| T4 | Structured `otherSources` on the entry; DataGen tests; known answers for 70012; decide 70011's feature status; regen with a reviewed diff |
| T5 | Extract the four plugin-side pieces into Core so the promised tests exist; keep the three "plausible" items out of 0.6.0 until verified in game |
| T6 | Move `MinWindowSize` to `ScaleMetrics` and test it; the rest to the smoke checklist at 0.9 / 1.15 / 1.6 |
| T7 | Tests as planned; add the tooltip-text test |
| T10 | The nine `GaugeGeometryTests` in 2.2, including the ε formula at two radii and clamping |
| Release order | 0.6.0 = T2, T4, T5, T6, T7, T8; 0.6.1 = T1; 0.6.2 = T3; or one 0.6.0 through the Dalamud testing channel |
| Docs | `docs/release-process.md` gains the revert-release procedure; `docs/data/DATA-VERSION.md` and `tools/regen.ps1` for the data flow |
