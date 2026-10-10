# Handoff

Updated 2026-10-08. To resume, read this file and `git log -5`, then `docs/team/agent-brief-common.md`.

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

## In flight (2026-10-09, 22:00)
**Owner's blanket approval for Moonfall decisions** (memory `moonfall-blanket-approval`): take the recommended option and report it. A public release still gets one confirmation line.

**The refit is paused** after stage 1's refit authors. Stage 1's five refitted levels are merged on `moonfall-peggle`; still to do are `stage mr1`, convert, Aces, gates, pack and reviews. Stage 2's partial refit worktrees `wf_f24443a9-c85-7..11` are to be discarded.

**Speed-up plan** (`docs/team/level-pipeline-speedups-2026-10-09.md`, items 1–13, all approved). Being built, each in its own worktree (merge each into `moonfall-peggle` when done, never junctions):
- `generate` and `tune`, plus items 3 and 4 (gates solved up front; a stage-aware `tune --stage` and `--hold`);
- items 1, 2, 8, 9 and 13: self-test stamp, `check`, shared caches outside the worktree, `--json`, `trace-many`;
- items 5, 6 and 7: faster build, incremental convert and stage, `--jobs`, `level`;
- item 10: `diff`, `sheet`, `status` and `pack`.

**Already done:**
- `precheck`, merged on `moonfall-peggle` (65f9e328);
- agent types `mf-integrator`, `mf-fixer` (Sonnet) and `mf-recorder` (Haiku, maxTurns 15), all with omitClaudeMd;
- `tools/claude-usage/keep-awake.ps1`;
- 23 merged worktrees removed.

**Next workflow script:**
- parallel per-level fixers;
- no round 2 on a Minor-only round 1;
- reviewers work from the pack only (`mfl.py pack`);
- round 2 gets diffs only;
- watchdog timeouts on long commands;
- run keep-awake on the journal;
- identical sibling prompts with the shared brief first;
- `agentType: mf-integrator`, `mf-fixer` and `mf-recorder`.

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
