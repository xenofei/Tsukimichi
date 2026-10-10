# Handoff

Updated 2026-10-10 (paused). To resume, read this file and `git log -5`, then `docs/team/agent-brief-common.md`.

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

## Paused (2026-10-10, owner's request: resources diverted to another project)
Nothing is running: the refit workflow and keep-awake are stopped, and every in-progress piece of work is committed on a named branch.

**Owner's blanket approval for Moonfall decisions** (memory `moonfall-blanket-approval`): take the recommended option and report it. A public release still gets one confirmation line.

**Where the refit stands** (branch `moonfall-peggle`, worktree `.claude/worktrees/moonfall-peggle`, head aba2e666, clean):
- **MR 1 (base-01..05): approved.** Round 3 (level critic only) APPROVE with 7 Minors; game designer and UX approved in round 2. Records in `docs/design/v9/levels/supervisor/*-mr1-refit-round-{1,2,3}.md`.
- **MR 2 (base-06..10): authored and merged** on moonfall-peggle (a18e332a..aba2e666), Sonnet authors as a pilot. Integration was stopped right after the merges: still to do `mfl.py sources`, `tune --stage mr2` (base-08 is too easy at 11.6/48; the finale base-10 at 6.6/48 is not yet harder than the other four), `stage mr2`, convert and `--check`, Aces, selftest, gate, pack, then reviews.
- **MR 3 (base-11..15): partial authoring** on `salvage-mr3-base-11..15` (WIP, unchecked; new authors merge and continue).
- **MR 4–6:** not started. MR 6's old round-2 M3 (0.5 step) is obsolete under the Peggle rules.
- **Main** has 25 approved levels and is green; nothing from the refit is on main yet.

**To resume:** launch the saved workflow `docs/team/workflows/moonfall-refit-v2.js` with the args in `docs/team/workflows/moonfall-refit-v2.resume-args.json` (MR 2 integration onward, MR 3 from salvage, MR 4–6). The run id `wf_2f5573bb-347` cannot be resumed from a new session. Start `pwsh tools/claude-usage/keep-awake.ps1 -WhileDir <the run's subagents/workflows/wf_* folder>` alongside.
- The script uses explicit models (Sonnet/medium integrator and fixers, Haiku/low recorder); a new session can switch to `agentType: 'mf-integrator' | 'mf-fixer' | 'mf-recorder'`.
- It has: parallel per-level fixers, no round 2 after a Minor-only round 1, a targeted round 3 for the reviewers still at REVISE, `given` (finished branches), `salvage` (WIP branches), and one retry when an author's worktree fails to start.

**Speed-up tools, all merged on `moonfall-peggle`:** `generate`, `tune` (`--stage`, `--hold`), jewels search, `check` (about 3 s), self-test stamp, shared caches in `%LOCALAPPDATA%/tsukimichi-moonfall-cache`, `--json`, `trace-many`, faster build with a render pool, incremental `convert --check`, `--jobs`, `level`, `diff`, `sheet`, `status`, `pack`, `precheck`. Also merged there: the art-wait fix (tests and MoonfallRender wait on `MoonfallGameArt.Running`, 10-minute hang cap) and `convert` writing the shipped level copy (b37496f0).

**Measured pace:** an author takes 2–3.5 h (five in parallel); a stage's integrate → review → fix → review chain takes 6–8 h and is the bottleneck. Estimate at pause: about 1–1.5 days to finish MR 2–6, then about 4–5 days for MR 7–11 and FS 1–12, plus half a day for the release. Running two stage chains side by side could save 1.5–2 days.

**Lessons (workflows):**
- Resume replays only an unchanged *prefix* of agent calls; after any edit, pass finished work as `given`/`salvage` branches.
- Keep branches a run must reuse out of the `worktree-wf_*` names: a later run reuses those names and resets or deletes them.
- Avoid git commands in the main checkout while a run creates worktrees (one author failed to start with "git metadata could not be resolved").
- `keep-awake.ps1 -WhileDir` on the run folder, not the journal (written only when an agent finishes).

**Open with the owner:** whether `CatalogLoaderTests.Maps_every_named_quest` should time the mapping thread's CPU time instead of wall time (it failed once at 13.1 s against 10 s under load). Unchanged until answered.

**Other branches kept:** `worktree-wf_2529e0f6-97c-17` (an older unmerged MR 6 authoring branch, left alone); the earlier tool branches `worktree-agent-a82a44151d5bd65d3`, `-a733a0357a0fa4ec7`, `-af6cf5375db3db43e` (merged) and the stage-2 partial `worktree-wf_f24443a9-c85-7..11` (discarded work), all deletable.

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
