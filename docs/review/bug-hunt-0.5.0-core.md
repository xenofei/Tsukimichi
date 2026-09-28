# Tsukimichi 0.5.0 bug hunt (report only, no edits)

Baseline: `dotnet build Tsukimichi.sln -c Release` passes; 315 Core tests (Evaluation/Runtime/Query/Jobs/Todo) and 67 data-backed tests (with TSUKIMICHI_GAME_PATH) pass. Sheet facts below come from a scratch Lumina probe (scratchpad/probe) over the live game files, not from memory.

Coverage note: this pass fully read Tsukimichi.Core (Evaluation, Runtime, Query, Storage, Todo, Jobs, Chains, Diff, Unique, Discovery), Tsukimichi.GameData, Plugin.cs, Game/StatePoller.cs, SnapshotService.cs, SessionState.cs, GameStateReader.cs, Config/Configuration.cs. Two delegated sweeps (Ui/* for ImGui/per-frame cost/UX; Game/* IPC/commands/ChatNotifier/DTR/ItemHooks/HoverHint) had not returned when this report was forced out, so items 3, 4, 6 and 7 of the brief (CharactersPane/DetailPane/Moonlit tooltips/Mark-as-unique guard/commands/notices/DTR/hooks) are NOT covered here and need a follow-up pass.

## Confirmed (by reading code + sheet data)

### C1. Wrong state: `TribeDailyOfferRequirement` is built from `QuestManager.DailyQuests`, which is the 12-slot *work* array, i.e. the tribal dailies accepted today, not the day's offer  (severity: wrong state, ~566 quests)
- `Tsukimichi/Game/GameStateReader.cs:281-303` (`ReadDailyOffer`) copies every non-zero `DailyQuests[i].QuestId`; `StatePoller.cs:219` feeds it as `TodaysDailyOffer`; `RequirementEvaluator.cs:131-135` then marks any repeatable tribe quest not in that set as unmet "not offered today", and `StateResolver` rule 7 turns that into **Blocked**.
- The array has 12 slots, exactly the daily allowance; a character with several tribes unlocked has far more than 12 quests on offer, so the array cannot hold the offer. It holds accepted dailies (with `Flags` bit for turned-in, which `IsDailyQuestCompleted` reads, and which the reader correctly uses for `DailyDone`).
- Failing input: accept one Vanu Vanu daily. Next poll `offer = {that id}`, `offerChanged` triggers a full resolve, and every other tribe daily (Ixal, Namazu, Pelupelu ...) that was Ready flips to Blocked with next step "not offered today". Before accepting anything the set is empty and the check is skipped, so the bug only shows after the first accept of the day.
- Second consequence: an accepted daily in progress is not in `NormalQuests` (see C2), not in `DailyDone`, and is in the "offer", so it resolves **Ready**, never **Accepted**.
- Smallest fix: drop the offer requirement (set `TodaysDailyOffer` to empty from the poller) until a real offer source exists; keep `DailyDone` from the same array. Classified confirmed on structure (slot count) — please still verify in game by accepting one daily and watching other tribes' dailies.

### C2. Accepted tribal dailies never reach `CharacterSnapshot.Accepted`  (wrong state, minor)
- `GameStateReader.cs:143-152` builds `Accepted` from `NormalQuests` only. Daily quest work lives in `DailyQuests`. Resolver rule 4 therefore cannot return Accepted for a daily in the journal; combined with C1 it reads Ready (or Blocked). Fix: add `DailyQuests` entries without the completed flag to `Accepted` (sequence from `Flags`/work is not available; use 0).

### C3. `SessionState.SetCatalog` builds `baseContext` with only `ClassJobs`  (feature gap that makes rule 6 dead code)
- `Tsukimichi/Game/SessionState.cs:216`: `EvalContext.Default with { ClassJobs = bundle.Jobs }`. `IsAchievementGated` is never supplied, `WithFestivalEnds` is never called (and `Data/curated/festivals.json` has `"entries": {}`), `HasMount`/`HasHouse` stay null. Result: `QuestState.Unknown` can never occur, achievement-gated quests show Ready, and the "curated end date" branch of rule 3 never runs. Not a crash; note so the eight-state UI/tests are not trusted as exercised in production.

### C4. Seasonal quests of events the character never saw are Blocked forever and inflate every total  (wrong count / polish)
- Rule 3 (`StateResolver.cs:157-161`) returns Blocked-seasonal unless `FestivalIsPast`, which (per C3) only fires when the character completed a quest of that festival id. The sheet has 310 festival quests across ~120 festival ids; a new character has all 310 as Blocked. `TreeCounts.Add` excludes only Foreclosed from `Total`, so "Seasonal Events" (and Overall) can never reach 100 % and the Compare-with `NeitherDone` counts them. Fix: populate festivals.json end dates, or treat a festival id whose newest quest is older than the current year's run as past.

### C5. `IsSwitchableGrandCompanyQuest` hides the lock only while the character is in *another* GC  (wrong state after a GC switch, low)
- `RequirementEvaluator.cs:190`, used at `StateResolver.cs:148`. Sheet: 15 GC quests carry locks (My Little Chocobo ×3, A Pup No Longer ×3, ...). A character who did Maelstrom's "My Little Chocobo", switched to Twin Adder, now sees Twin Adder's version **Foreclosed** (lock completed, same GC). That is correct for the chocobo (one per character) but the message "foreclosed by My Little Chocobo (Maelstrom)" is right; no fix needed — recorded as verified-correct so nobody "fixes" it. (Kept here because the test name `GC_specific_quest_in_the_same_GC_with_a_completed_lock_is_Foreclosed` reads as if it were a defect.)

### C6. `GrandCompanies.Name(0)` yields "Grand Company 0" in a real path  (polish)
- `RequirementEvaluator.cs:93-101`: for a quest with `GrandCompanyRank != 0` and `GrandCompany == 0` the code uses `s.GrandCompany`; a character with no GC (0) gets "needs Grand Company 0 rank N, you are rank 0". Sheet check: 0 such quests today (all 12 rank-gated quests also set GrandCompany), so latent only. Fix: `Names[0] = "a Grand Company"` path in `GrandCompanies.Name` when id 0.

### C7. `DeleteAllData` and `ForgetCharacter(live)` leave the poller's in-memory state and disk out of step  (data consistency, low)
- `SessionState.cs:170-185` deletes every snapshot and `.accepted.json`. `StatePoller` only listens to `CharacterForgotten`, not `DataDeleted`, so after Delete-all the live character's sidecar is not marked dirty and `saves.Pending` is usually false; the snapshot file reappears only on the next diff, the sidecar on the next journal change. `OnCharacterForgotten` (`StatePoller.cs:339`) does the opposite for Forget: it re-writes the sidecar for a character whose snapshot was just deleted, producing an orphan `.accepted.json`. Fix: on both events call `saves.MarkDirty()` and mark the sidecar dirty (or clear it) so the next flush rewrites both or neither.

### C8. `SnapshotDiff.OtherInputsChanged` allocates two HashSets every poll  (perf, trivial)
- `Tsukimichi.Core/Runtime/SnapshotDiff.cs:63-75` → `SameSet` (`:186`) builds `new HashSet<T>(a)` for `UnlockedInstances` (hundreds of ids) and `CompletedAchievements` on the *fast path* of every 1 s poll, even when nothing changed. Fix: compare with `SameSequence` first (captures are produced in sorted order by `IdsFor`), fall back to the set compare only on mismatch.

### C9. Snapshot schema has no migration path yet `StorageJson` writes enums as strings while `CharacterSnapshot` fields are value types  (persistence, informational)
- `SnapshotMigrator` registry is empty at v1 (fine). `CharacterSnapshot.MaxExpansion`/`LevelCap` are always written as 0 (`GameStateReader.cs:232-233`, "DRAFT-NEEDED E"), so `ExpansionCapRequirement`/`LevelCapRequirement` never fire; when they are wired, bump `CurrentSchemaVersion` and register a step, otherwise old files with 0 will silently read as "no cap" (which is the correct fallback, so risk is low).

## Verified-correct against sheet data (so they are not re-reported)
- Every `QuestLock` row uses `QuestLockJoin == 2` (Any): 42 multi-lock and 22 single-lock quests. The mapper ignoring `QuestLockJoin` (`CatalogMapper.cs:96`) is harmless today; add a data test asserting it so a future patch cannot silently introduce join 1.
- `PreviousQuestJoin`: 503 All / 99 Any for multi-prereq quests; `ToJoin` (2 = Any) matches.
- `ClassJobRequired` + `ClassJobCategory0` both set on 631 quests, and in all 631 the category admits exactly the required job, so `AdmitsJob` pinning to `ClassJobRequired` agrees with the sheet. Open in-game question (plausible, see P2): whether a job may take its base class's quests.
- Second slot `ClassJobCategory1`/`ClassJobLevel[1]`: 50 quests, all "category 1, level 0"; ignoring it is correct.
- `LevelMax` (867 quests) is the level-sync ceiling of scaling dailies, not an acceptance cap; ignoring it is correct.
- Named quests with level 0: 0; `ClassJobRequired > 255`: 0; max runtime quest id 5520 < 8 × 691 mask bytes.
- `BeastReputationRank` rows 0..8 match `TribeRanks.Names`.

## Plausible (needs in-game confirmation)

### P1. Tribe dailies may keep the completion bit permanently, making rule 5 sticky
- 566 tribe dailies have `RepeatIntervalType == 1`; `StateResolver.cs:172` returns **DoneThisCycle** when `completed && RepeatInterval != 0`. If `QuestManager.IsQuestComplete` stays true after the daily reset (the client keeps a "done at least once" bit for some repeatables), every daily ever done shows DoneThisCycle forever and never returns to Ready. Test: complete a daily, wait for the daily reset, check its state. If sticky, drop the `completed` clause from rule 5 and rely on `DailyDone` only.

### P2. Jobs cannot take their base class's quests in the evaluator
- 60 class quests are pinned to a base class (LNC, ARC, CNJ ...), and the category admits only that class. A Dragoon with an unfinished Lancer quest gets **ReadyOnOtherJob (LNC)** with hint "Ready on LNC" (`TodoList.Hint`, DTR/Nearby counts exclude it when "include other job" is off). If the game lets the job take it, the state should be Ready. Test in game on a job with a skipped class quest. Fix if so: in `AdmitsJob`, also accept a job whose `ClassJobParent` equals the pinned class.

### P3. Rule 3 outranks rule 4: an accepted seasonal quest is shown Blocked while the festival flag is momentarily absent
- `StateResolver.cs:157` runs before the journal check; `ActiveFestivals` is read each poll from `GameMain.ActiveFestivals`. If that array is cleared during a zone load, every accepted seasonal quest flips Blocked→Accepted→Blocked, and `QuestEvents.Derive` may emit spurious NewlyAvailable events on the way back (notices are gated by pinned/feature, so mostly harmless). Test: watch a pinned seasonal quest's state through a teleport during an event. Fix: move rule 4 above rule 3 (an accepted quest is in the journal regardless of festival state), matching the game's own journal.

### P4. `FindReadyJob` candidates without a category lookup come from `JobLevels.Keys`
- `StateResolver.cs:236-249`: offline compare (`CharactersPane` "Compare with") uses `baseContext` which has `ClassJobs`, so fine in the plugin; only tests hit the fallback. No action.

## Not covered in this pass (delegated sweeps did not return)
- ImGui id collisions / Push-Pop balance in CharactersPane.cs (2,096 lines), DetailPane.cs, MoonlitPane.cs, TablePane.cs, MainWindow.cs, FilterPanel.cs, TodoOverlay.cs, DiscoveryWindow.cs, ConfigWindow.cs, HelpWindow.cs, TutorialOverlay.cs.
- Per-frame cost and `QueryRunner` memo invalidation.
- The owner-reported items: Mark-as-unique button guard/revert (DetailPane), Moonlit reward icon tooltips (MoonlitPane), other destructive actions (Forget/Delete all in ConfigWindow/CharactersPane).
- Commands, ChatNotifier/NoticeTracker wiring, DtrEntry, ItemHooks, HoverHint, LifestreamIpc/WotsitIpc thread affinity.
Core-side notes relevant to those: `NoticeTracker.Scan` relies on reference identity of `QuestEvent` records and `SessionState.RecentEvents` is capped at 100 newest-first — more than 100 events in one poll (a first-pass full resolve is excluded, but a level-up full resolve is not: `QuestEvents.Derive` compares every row that changed instance) would make every event "new" again; the ReferenceEquals skip in `Derive` protects incremental resolves only.

## Game / IPC / commands / storage sweep (returned after the sections above were written)

Threading verdict: clean. Every ClientStructs / IClientState / ITargetManager / IGameGui access is on the framework thread (Update, Logout, command handlers, context-menu callbacks, UiBuilder.Draw). Only the Lumina catalog build runs off-thread and its continuation marshals via RunOnFrameworkThread with a `gameStateDisposed` check. Wotsit IPC callbacks set volatile flags or marshal; Lifestream has no callbacks. Plugin.Dispose order is correct and every Dalamud-event subscriber unsubscribes. SnapshotDiff.OtherChanged covers every evaluation-relevant snapshot field. GameStateReader bit math and bounds are correct.

G1. wrong-state, confirmed — curated festival end dates never reach the evaluator (same as C3 above): SessionState.cs:232 never calls `WithFestivalEnds`; `Curated.Festivals` is dead data. Fix: `.WithFestivalEnds(Curated.Festivals.ToDictionary(f => f.Key, f => f.Value.End), () => DateTime.UtcNow)` in SetCatalog, and populate festivals.json.

G2. wrong-state, plausible — first pass after login may capture an empty journal/mask. `IsCharacterReadable` (GameStateReader.cs:432) only checks PlayerState loaded + ContentId != 0. If QuestManager is not yet populated on that tick, `AcceptedSince.Reconcile` (AcceptedSince.cs:106-125) drops every stored accepted time, the next poll re-stamps them `now` (Stalled preset reset), and the first diff derives thousands of Completed/NewlyAvailable events, so ChatNotifier announces every pinned/feature quest. Guard: treat a first-pass capture with all-zero mask and empty journal as not ready (keep `last == null`) unless the stored snapshot was also empty.

G3. wrong-state, confirmed (only when Login arrives without a Logout gap) — character switch leaks the previous character's RecentEvents: StatePoller.cs:246-258 flushes and Resets but only `ClearLive` clears `recentEvents`. NoticeTracker resets `lastSeen` on the content-id change, so every NewlyAvailable still in the list is announced for the new character, and Recent activity shows the old character's events. Fix: in `SessionState.SetLive`, clear `recentEvents` when `LiveContentId != snapshot.ContentId`.

G4. perf, confirmed — hot load while `IsLoggedIn` but ContentId still 0: SnapshotService.cs:74-78 sets CharacterReady immediately, Capture throws, poller backs off 2..30 s, so the first capture can be delayed up to 30 s. Fix: on hot load set `awaitingCharacter = true` and schedule the same settle timeout as OnLogin.

G5. polish, confirmed — forgetting the live character leaves an orphan `<id>.accepted.json` (StatePoller.cs:386-392 only marks the sidecar dirty; Flush rewrites the snapshot only when `saves.Pending`). UI disables Forget for the live character, so code-only. Matches C7.

G6. polish — `snapshots.Delete/DeleteAll` raise CharactersChanged -> Bump before `FollowLive()` updates ViewedSnapshot (SessionState.cs:193-222): listeners rebuild once against the deleted character, then again. Fix: update the view first, bump once.

G7. polish — WotsitIpc.cs:680-685: a non-IpcNotReady exception mid-batch sets `pending = null` but leaves `registered = true`, so partial registration is never retried until the catalog instance changes.

G8. polish — Strings.cs:283 help text omits the `search` and `settings` subcommands handled in TsukimichiCommand.cs:90-113.

G9. polish — Configuration.Load swallows any deserialization exception and the next Save overwrites the file, so a transiently unreadable config is silently reset; copy it aside before overwriting.

Verified correct: NoticeTracker suppresses first-pass notices and dedupes per session; DtrEntry rebuilds only on DiscoveryWindow.Changed; HoverHint memoizes per (item, session version, lookup) with null/visibility checks; ItemHooks filters to MenuTargetInventory and uses BaseItemId; RewardLookup.NormalizeItemId ordering; QuestPayload/AgentQuestJournal id spaces; AtomicFile tmp+Move(overwrite) is atomic on Windows; JsonSnapshotStore.List ignores sidecars; Configuration.Save is framework-thread only; no config fields removed/renamed since 0.1.0.

Still not covered: the Ui/* sweep (ImGui id/Push-Pop, per-frame cost, QueryRunner memo, Mark-as-unique guard, Moonlit icon tooltips, destructive-action guards) had not returned at handback.

## Ui/* sweep (returned after handback; supersedes the "not covered" notes above)

All 34 files under Tsukimichi\Ui read fully. No crash-class bugs; ImGui id/push-pop discipline is clean (every ImRaii table/child/popup/combo checks `if (!x)`, TableSetupColumn counts match BeginTable, PushId/PushColor scoped by `using`, raw Begin/End in try/finally). Threading and null/index checks verified with no defects. Paths are `Tsukimichi\Ui\<file>:line`.

Wrong state
U1. Moonlit "Not unique (hide)" is an unguarded destructive action with no in-place recovery. MoonlitPane.cs:465-468 calls SetOverride(rowId,false,null) on one MenuItem click, writes user/overrides.json synchronously (:144-148, :639-651); UniqueRewardCatalog.Build (Core/Unique/UniqueRewardCatalog.cs:157-165,179-183) drops the quest from the unique view so the row vanishes; the only "Restore shipped verdict" is on that vanished row's context menu, and the confidence combo's "Yours" matches only Confidence.UserOverride. Recovery is only via the Journal detail pane "Restore". Fix: confirm popup or Ctrl-modifier (mirroring DetailPane.DrawUnique's Mark-unique popup), a "Hidden by you" confidence-filter option built from overrides with Unique==false (entries remain in catalog.ForQuest), and a toast.
U2. Detail pane Moonlit section misreports curated-only quests: DetailPane.cs:863 HasUniqueEntries = HasShippedUniqueEntry(...) (:1065-1078) scans only the shipped entries; curated system/duty unlocks are merged at build (UniqueRewardCatalog.cs:143-155). A quest listed via curated/system_unlocks.json shows "Not listed in Moonlit treasures" + "Mark as unique..." (:536-538); confirming stores a unique:true override that Build ignores (merged.HasQuest true, :161). Fix: extend IUniqueOverrides with IsListed(rowId) => pane.Catalog.ForQuest(rowId).Count > 0.
U3. Characters dashboard "Pinned" is stale up to a minute after pinning: CharactersPane.cs:1757-1799 re-reads user/pins.json from disk while QueryRunner owns pins and saves on a 1 s debounce (QueryRunner.cs:104-131,207-210); the dashboard rebuilds only on DashboardKey(session.Version, contentId, IsLive, minute, hasMoonlit) (:1393-1406) and TogglePin bumps ui.QueryVersion, not session.Version. Fix: read runner.Pinned and add ui.QueryVersion to DashboardKey.
U4. Scroll offset leaks between tabs: MainWindow.cs:636 draws every tab's centre in the same child "##center", :664 every left pane in "##left". Fix: PushId((int)ui.Tab) around the two children.
U5. UiState.Reveal (UiState.cs:80-132) unconditionally clears HideCompleted (+per-category), AvailableOnly (+per-category), PinnedOnly, StateMask and Preset, and FiltersChanged persists that (MainWindow.cs:338-342,350-370). Every cross-pane link routes here. Clicking a Ready quest in Moonlit permanently turns off "Hide completed"/"Available only" with no notice. Fix: clear a filter only when it would actually exclude the revealed quest, or toast what was cleared.
U6. Moonlit selection highlight lost after any override change: MoonlitPane.cs:517 resets selectedRow=-1 in BuildRows. Fix: highlight on ui.SelectedRowId == row.Entry.QuestRowId.

Perf
U7. Dashboard rebuilt every wall-clock minute (CharactersPane.cs:1396-1397 puts the minute in DashboardKey): TreeCounts.Compute over the whole catalog (:1689, duplicating QueryRunner.Counts), MsqProgress.Compute (:1622), ChainCatalog.Progress per chain, ladder progress per job and a synchronous PinsFile.Load (:1763) just to refresh "(N min ago)". Fix: drop Minute from the key, format the age separately, read counts from runner.Counts.
U8. Compare-with: CharactersPane.cs:1156-1160 StatesFor loads the snapshot and runs StateResolver.ResolveAll synchronously on the draw thread (memoized per contentId/TakenUtc/bundle, one hitch per character); BuildCompare (:1027-1091) re-runs on every session.Version bump while the tab is visible (every 10 s save bumps it via CharactersChanged). Key on the other character's TakenUtc + viewed States reference; compute StatesFor on a Task if the hitch is visible.
U9. FlightPane.cs:314,330 and DetailPane.cs:779 call links.CanFlagMap/CanTeleport per row per frame (linear AetheryteIndex.Nearest scan). Small N; cache per row.
U10. GlyphDebugWindow.cs:64,139,171 interpolate strings per frame (dev window).
QueryRunner memo (QueryRunner.cs:145-211) verified correct (FilterSet content equality, deep Clone, debounced search, hourly Stalled re-run, counts keyed on catalog/version/IncludeUnlisted, festivals keyed on snapshot instance which the poller replaces only on a non-empty diff). TreePane.RefreshCounts re-materializes labels only when TreeCounts instance/FeatureCount change. No LINQ/closures/StringBuilder in hot draw loops.

Polish / UX
U11. Moonlit reward icons and names have no hover tooltip (owner report confirmed): MoonlitPane.cs:436-447 DrawIcon and the Selectable at :377 have no IsItemHovered handling; only the Quest column (:404-407), state moon and confidence badge do. Fix: resolve a RewardRef from row.Quest?.Rewards by ItemId or (Kind,RewardId) as MoonlitIconResolver.FromQuestRewards (:819-850) already does and call RewardTooltip.Draw(reward, links, textures); fall back to UiMetrics.Tooltip(name/kind/source). MoonlitPane needs a GameLinks reference.
U12. Unscaled combo popups (must call UiMetrics.ApplyFontScale per UiMetrics.cs:17-20): CharactersPane.cs:828-853 compare-with combo, MoonlitPane.cs:245 "##moonlitConfidence", FilterPanel.cs:526 "##kind" per reward kind. FilterPanel.DrawJobCategory (:490) and the override popups (:392) do it correctly.
U13. Detail-pane "Restore" (DetailPane.cs:517-520) clears an override and its note with one click, no undo. Confirm popup or Ctrl-click.
U14. Session-only state that arguably should persist: UiState.MoonlitKind/MoonlitHideObtained (UiState.cs:40-42), Moonlit confidenceFilter and filterText (MoonlitPane.cs:63,91), FilterPanelOpen (UiState.cs:33), Tab. MainWindow persists only Filters and Sort (MainWindow.cs:324-336,350-370).
U15. Filter "Reset" (FilterPanel.cs:110-113 -> ResetAll :329-334) also clears the search text with no confirm.
U16. TablePane.cs:137-148 passes ui.Sort as DefaultSort only on the first frame; imgui.ini sort specs for "##quests" win and ApplySortSpecs (:534-564) overwrites ui.Sort. Add ImGuiTableFlags.NoSavedSettings if config is the source of truth.
