# R4 Performance & robustness — summary
Draw paths clean overall. Problems = framework-thread work + error isolation.
F1 Full ResolveAll on framework thread on job change/level-up/duty clear etc (StatePoller.cs:425-430; SnapshotDiff 73-87): 13.6–16.6 ms + query/TreeCounts/Discovery rebuild -> dropped frames on every gearset swap.
F2 Catalog landing burst: ReversePrereqIndex 9.8, StorySidequests 10.5, ChainCatalog 6.9, FeaturePresets 2.8, PathIndex first build 38-48 ms, MsqGraph 25 ms -> 30-110 ms frame.
F3 ViewCharacter/RefreshStoredFestivals/first Compare synchronous: 15-40 ms per click.
F4 Bump() multicast: one throwing listener stops the rest; first-build error swallowed (Plugin.cs:306-307); rebuild shows false "Catalog unavailable" (Plugin.cs:139).
F5 Plugin.Dispose stops at first exception (805-864) -> leaked hooks into collectible assembly.
F6 Unload: three 5 s waits -> up to ~15 s freeze worst case.
F7 7 icon sites throw (planned).
F8 HookGate exact version match -> every hotfix pauses item hints, context menus, DF hint, DTR until a release.
F9 Startup race: Framework.Update/handlers subscribed before ctor finishes (Plugin.cs:287-290).
F10 No plausibility guard: mid-session mass regression read overwrites good snapshot + sidecars; no backup.
F11 Every save bumps session version (recompute every 10 s); TodoOverlay re-reads pins.json on draw thread (:777); PublishLive allocs per frame. Config corruption & multibox locking solid.
F12 Flaky test likely: SaveBudgetTests.cs:30 (<200ms) & :79 (<100ms) wall-clock not tagged Perf; AtomicFileTests.cs:69 / JournalTextIndexTests.cs:271 Task.Delay(50) release on threadpool vs ~1.3 s retry budget.
Proposals: 1 resolve job/level changes on worker (Should,M); 2 pre-build derived indexes on catalog worker (Should,S-M); 3 async ViewCharacter/festival (Could,M); 4 isolate Bump listeners + log first-build error (Must,S); 5 Unwind each Dispose step (Must,S); 6 relax hook gate to patch date (Should,S); 7 icon fallback (Must,S); 8 plausibility guard + .prev.json backup (Should,S-M); 9 fix flaky tests (Should,S); 10 unload shared deadline (Could,S); 11 CharactersVersion, pins from QueryRunner, subscribe last (Could,S).
Qs: hook gate exact version deliberate?; dispose on framework thread?; regression threshold (seasonal bits clear yearly); which test flaked?
