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

## In flight
**Stages MR 4, MR 5 and MR 6 in one chained workflow** (run `wf_2529e0f6-97c`; watch with `/workflows`; resume with Workflow `resumeFromRunId`).
- Script: `~/.claude/projects/C--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/workflows/scripts/moonfall-stages-wf_2529e0f6-97c.js`.
- Per stage:
  1. authors, one per level;
  2. an integrator, which merges the previous stage's approved branch first;
  3. three fresh reviewers;
  4. round 1 recorded verbatim;
  5. one fixer per level, then a stage fixer;
  6. round-2 verification, recorded.
- A stage that isn't approved stops the chain.
- 2026-10-08: the stage-4 fixer hit the 5-hour usage limit and the run stopped. It was resumed at 15:57 with the same `resumeFromRunId`; finished agents replay from the journal. If a limit stops it again, resume it the same way after the reset.
- It returns one entry per stage: `approved`, `branch`, `worktree` and the verdict lines.

When it finishes:
1. merge the last approved stage's branch with `--no-ff` (it holds the stages before it);
2. run `pwsh tools/gate.ps1`, `mfl.py selftest` and `convert --all --check` (run `mfl.py fetch` first if the cache is missing a painting);
3. remove the `wf_2529e0f6-97c-*` worktrees and branches (paths contain a space: use `git worktree list --porcelain`);
4. measure the cost per stage with a journal scan.

## Next
1. **The remaining stages:** MR 7 to 11, then FS 1 to 12, with the same script.
   - Far Shore stage 1 ships the two Fan Kit wallpapers with a "© SQUARE ENIX" notice in `THIRD-PARTY-NOTICES.txt` (owner's answer).
   - Point `challenges.json` ch-03 and ch-04 at `base-27` and `base-37` when those levels ship.
2. **The full release,** after all 115 levels: version 1.24.0 or later, What's new, changelog. Ask the owner before pushing.
   - The owner decided on 7 October: at the full release, Moonfall gets its own button on the main window, not just the status-bar pill.

## Open
- **Owner items:** none open. The unused user-level agents and skills stay as they are (owner, 7 October).
- **Not verified in game:** GPU drawing, live motion, the keyboard and gamepad reveal, scene build speed, and 1-3's Fever moon.

## Gates
`pwsh tools/gate.ps1`. The raw commands are the fallback, in `docs/team/agent-brief-common.md`. Levels: `py -3 tools/moonfall-levels/mfl.py selftest`, `stage 1`, `stage 2` and `convert --all --check`.

## Do not
- Never push, tag or release without asking the owner.
- Never resume a stalled agent whose context is large; start a fresh one from a handoff.
- Never read whole transcripts; use the monitor and `save_report.py`.
