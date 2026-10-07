# Handoff

Updated 2026-10-06. To resume, read this file and `git log -5`, then `docs/team/agent-brief-common.md`.

## Goal
Moonfall, plan v9 (`docs/feature-plan-v9.md`): a Peggle-like game inside Tsukimichi.
- **Shipped:** 1.23.0 "Moonfall preview", with the menus and screens and the first 10 of 115 levels.
- **Next:** the remaining 105 levels, then the full release (version after 1.23.0).
- **Running alongside:** the workflow changes from `docs/team/workflow-audit-2026-10-06.md`, all approved by the owner.

## Done
- **1.23.0 is released,** with the tag pushed and pluginmaster checked. The owner reported not seeing the Moonfall button. It is a small "● Moonfall" pill at the bottom right of the main window's status bar, and `/tsuki moonfall` also opens it.
  - Still waiting on the owner: a screenshot, or confirmation that `/tsuki moonfall` works.
  - Offered and not yet answered: a toolbar button.
- **Optimization team:** eight `opt-*` specialists in `.claude/agents/`. The roster is in `docs/team/optimization-team.md`.
- **Audit and owner approvals:**
  - the shared brief `docs/team/agent-brief-common.md`;
  - `tools/claude-usage/monitor.py` and `save_report.py`;
  - `.claude/settings.json`: fallback model, `autoCompactWindow` 400k, retry watchdog;
  - the global CLAUDE.md trimmed;
  - the smoke-test pages removed.
- **Charter for the 105 levels:** `docs/design/v9/levels/charter.md`, merged at 1c744af0.

## In flight
- **Gate wrapper and result caches.** A background agent is building them in worktree `.claude/worktrees/agent-a283f4ad98f1c793e`. When it hands back:
  1. review its summary;
  2. merge with `--no-ff`;
  3. run `pwsh tools/gate.ps1`, or the raw gates if it fails.

## Next
1. **The owner answers the charter's questions** (section 5 of the charter). The defaults are in its summary.
2. **Prepare for level production:**
   - teach `mfl.py stage` stages 3–11 and a campaign key, so Far Shore stage numbers don't collide with Moon Road ones;
   - add the challenge-carrier check (the greedy player must win at 35 and 45 oranges);
   - source the 55 game paintings the charter marks "needs a source".
3. **Pilot stage 3 as a workflow:**
   - one short author agent per level;
   - the pipeline gates and a review pack;
   - one round by fresh reviewers (game designer, UX/UI, level critic);
   - a fixer, then a check.

   Reviewer reports go through `tools/claude-usage/save_report.py` into `docs/design/v9/levels/supervisor/`. Then scale stage by stage, three stages in flight.
4. **After each phase,** run `py -3 tools/claude-usage/monitor.py <session-id>` and compare it with the baseline snapshot at `%TEMP%/tsukimichi-claude-usage-baseline-2026-10-06.json`.

## Open
- **Owner items:**
  - the charter's questions;
  - the four levels whose colours changed (sheets in `docs/design/v9/levels/runtime/approved-vs-shipped-base-0{4,5,8,9}.jpg`);
  - unused user-level agents and skills (they can only be removed globally);
  - the toolbar button.
- **Flaky under full-suite load:** `SaveBudgetTests.Close_waits...` and `MoonfallGameTests.Every_power..._allocate_nothing`.
- **Not verified in game:** GPU drawing, live motion, the keyboard and gamepad reveal, scene build speed, and 1-3's Fever moon.

## Gates
`pwsh tools/gate.ps1`, once merged. Until then, use the raw commands in `docs/team/agent-brief-common.md`. Levels: `py -3 tools/moonfall-levels/mfl.py selftest`, `stage 1`, `stage 2` and `convert --all --check`.

## Do not
- Never push, tag or release without asking the owner.
- Never resume a stalled agent whose context is large; start a fresh one from a handoff.
- Never read whole transcripts; use the monitor and `save_report.py`.
