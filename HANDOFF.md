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

## In flight (2026-10-09)
- **MR 5:** merged to main (6293c6c5). There are 25 levels on main.
- **MR 6:** in run `wf_40869448-0fa` (stage fixer, then round 2). Merge it if approved; it will be refitted with the rest.
- **Peggle match** (owner's decision of 9 October, in the charter's "Owner's answers on matching Peggle"). Two agents run in parallel:
  - **Engine:** every DIFFERS rule in the study's table, in Tsukimichi.Core/Moonfall.
  - **Pipeline:** density 95–115 pieces with radius-10 pegs and 20-wide bricks; candidates as a 60–90% share with up to 30% in the lower third; stage-mean bands plus a hard finale, with no per-level step rule. The band numbers are placeholders.
- **Study files** (scratch): `scratchpad/peggle-ref/`, including `mechanics-table.md`, the calibration, and `convert.py`/`calib.py`, which run our greedy player on Peggle's levels. Scratch only; never in the repo.

## Next
1. Merge the engine and pipeline work and run the gates.
2. Re-run the Peggle yardstick on the new engine, then set the stage bands from Peggle's curve in the charter and `stagecheck.py`.
3. Refit the 30 finished levels (MR 1–6), a workflow per stage: densify, candidate share, re-tune, Aces, reviews.
4. Continue with MR 7–11 and FS 1–12 under the new rules.

## Next
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
