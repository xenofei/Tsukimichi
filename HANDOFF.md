# Handoff

Updated 2026-10-22 (MR 1–3 done; first test drop being prepared; generator trusted from MR 4). To resume, read this file and `git log -5`, then `docs/team/agent-brief-common.md`.

## Goal
Moonfall, plan v9 (`docs/feature-plan-v9.md`): a Peggle-like game inside Tsukimichi.
- **Shipped:** 1.23.0 "Moonfall preview", with the menus and screens and the first 10 of 115 levels.
- **Next:** the remaining levels under the workflow the owner set on 21–22 October. It is recorded in the charter, `docs/design/v9/levels/charter.md`, in the section "Owner's answers on the workflow review". The words are defined in `docs/design/v9/levels/GLOSSARY.md`.

## The workflow (owner, 21–22 October 2026)
- **Trigger:** the owner says "continue Moonfall". Stages run one after another, one in flight at a time, until the next test drop is ready; then stop and tell the owner.
- **A stage:**
  1. Author: the generator once it passes the MR 1 test, time-boxed authors until then.
  2. Integrate.
  3. One combined AI review, one round. Fix only Blockers and Majors; Minors go on the Minor list.
  4. Gates.
  5. Merge.

  One stage per run, with the state read from `mfl.py status` and git, so any session can continue.
- **Test drop (every 3 stages):**
  1. A pre-release on Tsukimichi's Dalamud testing versions. The owner has opted in once. **Ask the owner before each push.**
  2. A play-through round on the artifact "Moonfall Workflow Brief" (https://claude.ai/artifact/P1JJ83p5yKRzU9kbt88UN4): Good or Redo plus a note per level.
  3. Redo levels are fixed, pass the automatic checks only, and go out as a small re-drop.
  4. When every level is Good, the 15 levels are released (ask first).
- **The Moon Road (55) is the first milestone:** the polish pass on the Minor list, then the release with Moonfall's main-window button.
- **Developer mode:** a hidden slash command in every build that unlocks every level, jumps to any level, shows its figures and lifts the spoiler shield. Remove it once all 115 have shipped.
- **Players keep** completions, scores and Aces on refitted levels.

## Done (21–22 October)
- **Developer mode** (`/tsukimichi moonfall dev`; remove later by grepping "Developer mode") and the **test channel** (rc tags fill the testing fields; `docs/team/test-drops.md`).
- **MR 2 and MR 3** through the new flow on `moonfall-peggle`. base-08 stays the easy outlier of MR 2 (10.5/48) for the play-through to judge; base-14 was reworked from 12.2 to 4.4/48. Minors are on `docs/design/v9/levels/minor-list.md`.
- **Generator:** `mfl.py make`, `bench`, `overlay` (merged into `moonfall-peggle`). It passed the MR 1 test: 4 of 5 pass the checks, and the owner accepted all 5 (round 7). From MR 4 the stage script runs with `generator: true`.
- **1.23.1** (hooks tested on game 2026.10.06): branch `hotfix-1.23.1` (885b0378, built on v1.23.0, not on local main, which holds unreleased levels). Owner-approved; the auto-mode classifier blocked the push, so the owner runs the tag and push.

## Next, in order
1. **First test drop, `v1.24.0-rc1`** (MR 1–3): a branch merging main, `moonfall-peggle` and `hotfix-1.23.1`, with stages 1–3 reachable and base-16+ not, Version 1.24.0.0, changelog and What's new. Then an approval card on the brief page before the tag and push, then a play-through round (Good or Redo per level).
2. **MR 4–MR 11, then The Far Shore's FS 1–FS 12,** generator-first:
   - FS 1 ships the two Fan Kit wallpapers with a "© SQUARE ENIX" notice in `THIRD-PARTY-NOTICES.txt`;
   - point `challenges.json` ch-04 at `base-37` when it ships.

## The per-stage workflow
- **Script:** `docs/team/workflows/moonfall-stage.js` runs one stage per launch:
  1. time-boxed authors, or none when `authored: true`;
  2. integrate;
  3. one combined reviewer with a structured findings list;
  4. record, and append the Minors to `docs/design/v9/levels/minor-list.md`;
  5. per-level fixers only for levels with Blockers or Majors, then a stage fixer.
- **Args:** `worktree`, `branch`, `key`, `levels`, `codes`, `later` (the not-yet-refitted levels whose tests may fail), `authored`, `generator` (true from MR 4: `mfl.py make`, adopt the best passing variant, hand polish on finales only), `mergedNote`, `salvage`, `authorModel`, `knownFails`, `integrated`.
- **Retired:** `moonfall-refit-v2.js`.

## Where the refit stands
- **MR 1–MR 3 (base-01..15):** done on `moonfall-peggle` (MR 1 approved in refit rounds 1–3; MR 2 and MR 3 reviewed once, Blockers and Majors fixed).
- **MR 4–6:** not started; generator-first from here.
- **Main** has 25 old approved levels and is green; nothing from the refit is on main yet. Local main is 112+ commits ahead of GitHub, including unreleased Moonfall levels, so a hotfix must be built on the last release tag, not on main.

## Measured (refit runs, 7–10 October)
- **Where the time goes:** agents spend about 90% of wall time inside tool calls.
- **The speed-up tools work:** `mfl build` median fell from 257 s to 84 s, and authors now mostly use `check` (about 18 s) and `level`.
- **What is left:** authors sleeping on long background jobs (median about 6 minutes per wait).
- **Retries:** a typical level needed 9–12 tune/build/check runs before it passed.
- **Stalls and limits:** one shared stall hung four MR 4 authors for about 4.7 h. There was 1 usage-limit stop.

## Lessons (workflows)
- Resume replays only an unchanged *prefix* of agent calls; after any edit, pass finished work as branches.
- Keep branches a run must reuse out of the `worktree-wf_*` names; a later run reuses those names and resets or deletes them.
- Avoid git commands in the main checkout while a run creates worktrees.
- Run long tool commands in the foreground with a timeout, not in the background with sleeps.
- `keep-awake.ps1 -WhileDir` on the run folder, not the journal.

## Open
- **Owner:** whether `CatalogLoaderTests.Maps_every_named_quest` should time the mapping thread's CPU time instead of wall time (it failed once at 13.1 s against 10 s under load). Unchanged until answered.
- **Not verified in game:** GPU drawing, live motion, the keyboard and gamepad reveal, scene build speed, and 1-3's Fever moon.
- **Old branches, all deletable:**
  - `worktree-wf_2529e0f6-97c-17` (an older unmerged MR 6 authoring branch);
  - `worktree-agent-a82a44151d5bd65d3`, `-a733a0357a0fa4ec7` and `-af6cf5375db3db43e` (merged);
  - `worktree-wf_f24443a9-c85-7..11` (discarded).

## Gates
`pwsh tools/gate.ps1`. The raw commands are the fallback, in `docs/team/agent-brief-common.md`. For levels: `py -3 tools/moonfall-levels/mfl.py selftest`, `stage <key>` and `convert --all --check`.

## Do not
- Never push, tag or release without asking the owner. This includes test drops. Ask through the "Waiting for your OK" section of the Moonfall Workflow Brief (an `approvals/<id>` doc), not in chat (owner, 22 October).
- Never resume a stalled agent whose context is large; start a fresh one from a handoff.
- Never read whole transcripts; use the monitor and `save_report.py`.
