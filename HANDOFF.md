# Handoff

Updated 2026-10-07. To resume, read this file and `git log -5`, then `docs/team/agent-brief-common.md`.

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
- **Charter for the 105 levels:** `docs/design/v9/levels/charter.md`, with the owner's answers of 7 October.
- **Gate wrapper and caches:** `pwsh tools/gate.ps1` (skips an unchanged tree) and the caches for `mfl.py build`/`stage` and MoonfallRender `--cache`.

## In flight
Three background agents are running, each in its own worktree. When one hands back:
1. review its summary;
2. merge with `--no-ff`;
3. run `pwsh tools/gate.ps1`;
4. remove the worktree.

The three jobs:
- **Pipeline prep:**
  - `mfl.py stage` learns Moon Road stages 3–11 and Far Shore stages 1–12, keyed by campaign;
  - a challenge-carrier check;
  - the Ace rule for hard levels: the top 40% of wins below about 14 per 48.
- **Sourcing (Opus):** `docs/design/v9/levels/sources-plan.md`, with a source and crop for every level marked "needs a source".
  - Fan Kit wallpapers are for the 5 figurative levels with no game picture. Check the licence terms before shipping any.
- **Flaky tests:** root-cause fixes for the three tests that fail under load. After that, the gate's rerun list is empty.

## Next
1. **Pilot stage 3 as a workflow,** once pipeline prep and sourcing are merged:
   - one short author agent per level;
   - `mfl.py build`, `stage mr3` and `convert --check`, plus a review pack;
   - one round by fresh reviewers (game designer, UX/UI, level critic), with reports through `tools/claude-usage/save_report.py`;
   - a fixer, then a check.

   Then run the remaining stages the same way, three in flight.
2. **After each phase,** run `py -3 tools/claude-usage/monitor.py <session-id>` and compare it with `%TEMP%/tsukimichi-claude-usage-baseline-2026-10-06.json`.

## Open
- **Owner items:**
  - the Fan Kit licence check, before any wallpaper ships;
  - the four levels whose colours changed (sheets in `docs/design/v9/levels/runtime/approved-vs-shipped-base-0{4,5,8,9}.jpg`);
  - unused user-level agents and skills (they can only be removed globally);
  - the toolbar button.
- **Flaky under full-suite load:** three tests, being fixed (see In flight).
- **Not verified in game:** GPU drawing, live motion, the keyboard and gamepad reveal, scene build speed, and 1-3's Fever moon.

## Gates
`pwsh tools/gate.ps1`. The raw commands are the fallback, in `docs/team/agent-brief-common.md`. Levels: `py -3 tools/moonfall-levels/mfl.py selftest`, `stage 1`, `stage 2` and `convert --all --check`.

## Do not
- Never push, tag or release without asking the owner.
- Never resume a stalled agent whose context is large; start a fresh one from a handoff.
- Never read whole transcripts; use the monitor and `save_report.py`.
