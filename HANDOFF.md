# Handoff

Updated 2026-10-10. To resume, read this file and `git log -5`, then `docs/team/agent-brief-common.md`.

## Goal
Moonfall, plan v9 (`docs/feature-plan-v9.md`): a Peggle-like game inside Tsukimichi.
- **Shipped:** 1.23.0 "Moonfall preview", with the menus and screens and the first 10 of 115 levels.
- **Next:** the remaining 105 levels, then the full release (version after 1.23.0).
- **Running alongside:** the workflow changes from `docs/team/workflow-audit-2026-10-06.md`, all approved by the owner.

## Done
- **1.23.0 is released,** with the tag pushed and pluginmaster checked. The owner tested it in game on 7 October and the preview works.
- **Optimization team:** eight `opt-*` specialists in `.claude/agents/`. The roster is in `docs/team/optimization-team.md`.
- **Audit and owner approvals:**
  - the shared brief `docs/team/agent-brief-common.md`;
  - `tools/claude-usage/monitor.py` and `save_report.py`;
  - `.claude/settings.json`: fallback model, `autoCompactWindow` 400k, retry watchdog;
  - the global CLAUDE.md trimmed;
  - the smoke-test pages removed.
- **Sources plan:** `docs/design/v9/levels/sources-plan.md`, with the owner's answers (Fan Kit ships with a © notice).
- **Pipeline prep, merged:** stage keys `mr1`–`mr11` and `fs1`–`fs12`, `mfl.py challenge`, and the Ace rule for hard levels.
- **Flaky tests fixed:** the save writer has its own thread; the allocation tests take the smallest of 5 windows (owner-approved); the gate's rerun list is empty.
- **Stage MR 3 shipped to main** (7be2978f): base-11 to base-15, approved in round 2. The pilot cost 391M input tokens; its single fold-in took 42%, so the fold-in is now split per level. The runtime crop rounds to 8-bit steps between passes, as the pipeline does.
- **Charter for the 105 levels:** `docs/design/v9/levels/charter.md`, with the owner's answers of 7 October.
- **Gate wrapper and caches:** `pwsh tools/gate.ps1` (skips an unchanged tree) and the caches for `mfl.py build`/`stage` and MoonfallRender `--cache`.

## In flight (2026-10-10, early)
**Owner's blanket approval for Moonfall decisions** (memory `moonfall-blanket-approval`): take the recommended option and report it. A public release still gets one confirmation line.

**Refit workflow running:** `moonfall-refit-v2`, run `wf_2f5573bb-347` (script under `~/.claude/projects/C--Users-devon-Desktop-Tsukimichi--Main-Repo---claude-worktrees-moonfall-peggle-docs-design-v9-levels/60059d08-…/workflows/scripts/`). It refits MR 1–6 on `moonfall-peggle`, stage by stage: authors (generate, tune, check, level; MR 2 pilots Sonnet authors), integrator, three reviewers from the pack, parallel per-level fixers, round 2 only after a Blocker or Major. It stops at the first stage that fails. Resume with `Workflow({scriptPath, resumeFromRunId: 'wf_2f5573bb-347', args})`; the args are in the launch call (stage list MR 1–6; MR 1 `authored: true`). `keep-awake.ps1` watches its journal.
- The `mf-*` agent types are not loaded in this session (added after it started), so the script sets the models directly: Sonnet/medium for the integrator and fixers, Haiku/low for the recorder. A new session can use `agentType`.
- MR 6's old round-2 M3 (a 0.5 step) is obsolete under the Peggle rules.

**Speed-up tools, all merged on `moonfall-peggle`** (e7c4a1c7): `generate`, `tune` (`--stage`, `--hold`), jewels search, `check` (about 3 s), self-test stamp, shared caches in `%LOCALAPPDATA%/tsukimichi-moonfall-cache`, `--json`, `trace-many`, faster build with a render pool, incremental `convert --check`, `--jobs`, `level`, `diff`, `sheet`, `status`, `pack`, `precheck`. Build outputs are byte-identical to before (checked on base-01..03). The generator's dress limits follow the runtime loader (8 mask terms, shaft `near` -180..1000).

**Also running:** a debugger on `MoonfallSceneStartTests.The_menus_thumbnail_is_the_levels_own_scene_scaled_down`, which fails "never settled" under machine load and passes alone. Until it lands, the workflow lets that test pass only when it passes alone.

**Branches kept, worktrees removed:** `worktree-agent-a82a44151d5bd65d3`, `worktree-agent-a733a0357a0fa4ec7`, `worktree-agent-af6cf5375db3db43e` (all merged), the stage-2 partial `worktree-wf_f24443a9-c85-7..11` (discarded work), and the unmerged MR 6 authoring branch `worktree-wf_2529e0f6-97c-17` (left alone).

**Main** has 25 approved levels and is green.

## Next
1. **Merge `moonfall-peggle` into main** when the refit's gates pass, and run the full check.
2. **Continue MR 7–11 and FS 1–12** under the new rules.
   - Far Shore stage 1 ships the two Fan Kit wallpapers with a "© SQUARE ENIX" notice in `THIRD-PARTY-NOTICES.txt` (owner's answer).
   - Point `challenges.json` ch-04 at `base-37` when it ships (ch-03 already points at `base-27`, from MR 6).
3. **The full release,** after all 115 levels: version 1.24.0 or later, What's new, changelog. Ask the owner before pushing.
   - At the full release, Moonfall gets its own button on the main window (owner, 7 October).

## Open
- **Owner items:** none open. The unused user-level agents and skills stay as they are (owner, 7 October).
- **Not verified in game:** GPU drawing, live motion, the keyboard and gamepad reveal, scene build speed, and 1-3's Fever moon.

## Gates
`pwsh tools/gate.ps1`. The raw commands are the fallback, in `docs/team/agent-brief-common.md`. Levels: `py -3 tools/moonfall-levels/mfl.py selftest`, `stage 1`, `stage 2` and `convert --all --check`.

## Do not
- Never push, tag or release without asking the owner.
- Never resume a stalled agent whose context is large; start a fresh one from a handoff.
- Never read whole transcripts; use the monitor and `save_report.py`.
