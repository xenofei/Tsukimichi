# Handoff

Updated 2026-10-07. To resume, read this file and `git log -5`, then `docs/team/agent-brief-common.md`.

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
- **Charter for the 105 levels:** `docs/design/v9/levels/charter.md`, with the owner's answers of 7 October.
- **Gate wrapper and caches:** `pwsh tools/gate.ps1` (skips an unchanged tree) and the caches for `mfl.py build`/`stage` and MoonfallRender `--cache`.

## In flight
**The stage 3 pilot workflow** (run `wf_e874cb78-8d2`; resume with Workflow `resumeFromRunId`; watch with `/workflows`).
- Script: `~/.claude/projects/C--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/workflows/scripts/moonfall-stage-pilot-wf_e874cb78-8d2.js`.
- Its steps:
  1. five authors, one per level (base-11 to base-15), each in its own worktree;
  2. an integrator: stage `mr3`, convert, wiring, gates and a review pack in `docs/design/v9/levels/review/mr3/`;
  3. three fresh reviewers;
  4. a fold-in that records round 1 verbatim in `docs/design/v9/levels/supervisor/*-mr3-round-1.md`;
  5. three verifiers, with round 2 recorded verbatim.
- It returns only verdict lines and branch, worktree and commit.

When it finishes:
1. if every round-2 verdict is APPROVE, merge the integrator's branch with `--no-ff`;
2. run `pwsh tools/gate.ps1` and the level self-test;
3. clean up the worktrees;
4. run `tools/claude-usage/monitor.py` and compare it with the baseline.

## Next
1. **Stage 3:** if the pilot works, reuse the script for the other stages, about three in flight.
   - Far Shore stage 1 ships the two Fan Kit wallpapers with a "© SQUARE ENIX" notice in `THIRD-PARTY-NOTICES.txt` (owner's answer).
   - Point `challenges.json` ch-03 and ch-04 at `base-27` and `base-37` when those levels ship.
2. **The full release,** after all 115 levels: version 1.24.0 or later, What's new, changelog. Ask the owner before pushing.
   - The owner decided on 7 October: at the full release, Moonfall gets its own button on the main window, not just the status-bar pill.

## Open
- **Owner items:**
  - unused user-level agents and skills (they can only be removed globally);

- **Not verified in game:** GPU drawing, live motion, the keyboard and gamepad reveal, scene build speed, and 1-3's Fever moon.

## Gates
`pwsh tools/gate.ps1`. The raw commands are the fallback, in `docs/team/agent-brief-common.md`. Levels: `py -3 tools/moonfall-levels/mfl.py selftest`, `stage 1`, `stage 2` and `convert --all --check`.

## Do not
- Never push, tag or release without asking the owner.
- Never resume a stalled agent whose context is large; start a fresh one from a handoff.
- Never read whole transcripts; use the monitor and `save_report.py`.
