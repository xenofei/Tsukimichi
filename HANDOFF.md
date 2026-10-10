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

## In flight (2026-10-09, night)
**The refit of MR 1–6 on `moonfall-peggle`** (run `wf_f24443a9-c85`; resume with Workflow `resumeFromRunId` and the script in `workflows/scripts/moonfall-peggle-refit-wf_f24443a9-c85.js`).
- **Per stage:**
  1. one refit author per level (own worktree; it merges `moonfall-peggle` first);
  2. an integrator, which merges into the `moonfall-peggle` worktree, runs the stage check, convert and Aces, the gates and the pack in `review/<stage>-refit/`;
  3. fresh reviews, with records in `supervisor/*-<stage>-refit-round-N.md`;
  4. one fixer per level, then a stage fixer, then round 2.
- **Gate exception:** until a stage is refitted, the gates may fail only the greedy-player and Ace tests of later stages' levels (named in each brief).
- **Already on `moonfall-peggle`:**
  - the engine's Peggle rules (69566a4c);
  - the pipeline rules;
  - the bands from Peggle's curve (d40c8c91);
  - the owner's answers (167b8afb): floor 2 of 48, finale below the mean of the other four, every Ace at the top 40% of wins;
  - MR 6 merged (011278be). Its round-2 critic Major is in `level-critic-mr6-round-2.md`; the MR 6 refit reviews it again.
- **Main** stays at 25 approved levels and green until the refit passes, then `moonfall-peggle` merges into main.

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
