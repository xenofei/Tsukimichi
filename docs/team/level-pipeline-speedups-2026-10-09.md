# Speeding up Moonfall's level pipeline (research, 9 October 2026)

Four research streams fed this plan:
- measurements of our own runs (94 agents across MR 3–6 and the MR 1 refit);
- a profiled audit of the level tools;
- outside research on level generation and automated play-testing;
- the Claude Code throughput docs.

The raw reports are in the session scratchpad (`speed-research/`).

**Already being built** (not repeated below): the layout generator, the difficulty tuner, `precheck`, parallel fixers, Sonnet for integrators and fixers, no round 2 when round 1 finds only Minors, a shared texture cache, prebuilt tools per stage, and a gate that skips unaffected parts.

## The finding behind most of the slowness
`mfl.py build` runs the pipeline's full self-test (about 4–5½ minutes) on every call that has anything to build. Across 338 build calls, each call took a median of 198–426 s, against 33–80 s for the level work itself. That is about **45% of an author's time** and roughly 10 agent-hours per stage. About 85% of an author's wall time is waiting for tools, not thinking.

## Ranked plan

| # | Change | Evidence | Saving per stage (est.) | Effort |
|---|---|---|---|---|
| 1 | Stamp the self-test on a hash of `mflkit/*.py`, the C# tools and the fixtures; skip it when unchanged. Run its 7 sections in parallel on a miss | profile: self-test 5m39s; 338 calls at +160–340 s each | 1.5–2 h wall, about 10 agent-hours | S |
| 2 | A fast inner loop: `mfl.py check <id>` (preflight, loader, sweep, reach, 48 rule games, about 3 s), and `build` stops after the cheap phase on failure (`--keep-going` for the old behaviour) | 40% of builds were refused, almost all by deterministic rules found only after the full overhead | about 20–25 min per author | S |
| 3 | The generator solves the deterministic gates up front: a jewel pair for F7/F9/F6, the spread cap while placing candidates, the runtime limits (6 mask terms, shaft `near` ≥ 0) checked at author time | refusals: F7 52%, spread 31, F9 27, F6 about 40, print 16; runtime limits found only at integration | about 40% fewer author iterations | M (extends the generator) |
| 4 | A stage-aware tuner: tune all five levels together against the stage mean and the finale rule, lock each level's difficulty before art fixes, and give level fixers a difficulty-neutral mode | every round 2 with a Major came from neighbouring retunes; the MR 5 integrator retuned 5-3 four times | about 1–2 h and one review round | M (extends the tuner) |
| 5 | Faster builds: share the board mask and frame across the 5 composite renders, play the games in parallel with the Python phases, and render in parallel | build profile: renders 17–24 s, mask 8.5 s, frame 6.8 s; Python single-threaded on 32 cores | a cold build from about 65 s to about 30 s | S–M |
| 6 | An incremental `convert --check` (stamp per level, reuse the 2x reference for 1x, build Render once), and an incremental `stage` (cached game blocks per level hash). The integrator relies on author stamps instead of rebuilding | about 17 s per level with no cache; integrators ran convert 4× at 2–2.6 min | 20–30 min | M |
| 7 | `build --jobs N` and `mfl.py level <id>` (build, convert, check and render in one verdict) | serial loop; agents chain 4 commands by hand | 4–6× on stage-wide builds | M |
| 8 | Shared, content-keyed caches outside the worktree (self-test stamp, play, graded scenes, renders), and a per-level key for Render's cache | each new worktree starts cold; any level change invalidates every cached render | minutes per agent | S–M |
| 9 | Machine-readable `--json` from build, check and stage, with fix hints, and pre-flight messages that name the tag and x,y instead of "brick 7" | agents scrape logs and count pieces | fewer agent turns | S |
| 10 | Reviewer efficiency: reviewers use the prebuilt pack only (one UX reviewer spent 29 min running convert); round 2 gets the diff and changed images only; a before-and-after diff renderer; contact sheets | round 1 reviewers read 16–25 images and use 2–9M tokens each | 20–30 min | S–M |
| 11 | A stall watchdog: flag any tool call over 20 min, relaunch the agent from its last commit, put timeouts on long commands, and keep the machine awake during unattended runs | two stalls of about 4½ h (all five MR 4 authors at once; fix 5-4), most likely usage-limit waits | up to 4½ h per event | S |
| 12 | Claude settings: identical sibling agents with the shared brief first (sibling cache sharing); explicit `effort` per role; `omitClaudeMd` plus a preloaded level-spec skill for integrators and fixers; Haiku with `maxTurns` for purely mechanical steps; run the orchestrating session interactively so 5-hour limits wait instead of failing | Claude Code docs, 2026-10-09 | fewer tokens per stage, so fewer limit pauses | S |
| 13 | `trace-many` in mfcheck for `stuck` and `dead` | one process per angle | up to 10 s per bad level | S |

## Later, and bigger
- **A stronger test player** alongside the greedy one: a rollout or beam search over aims, calibrated once against the owner's own play on about 10 levels. Difficulty targets then mean more for humans. (M; Stephenson & Renz, arXiv 1902.02518; King, Gudmundsson et al. 2018.)
- **Variants per level (MAP-Elites style),** with contact sheets, so reviewers pick a level instead of looping through tune-and-review. (M; Fontaine et al., arXiv 2007.05674.)
- **Bayesian optimisation in the tuner,** for fewer games per tuning step. (M; Google Research, "Finding game levels with the right difficulty in a few trials".)

The outside research was a light pass. Verify each library's licence before adopting it. The plugin itself gains no dependencies: these tools stay in `tools/`.

## Expected effect
With items 1–6 together with the tools already being built:
- an author drops from about 83 to roughly 25–35 minutes;
- a stage from about 12–16 h to roughly 5–7 h of active time;
- tokens per stage fall by roughly half, so there are fewer usage-limit pauses, which remain the largest wall-time cost.
