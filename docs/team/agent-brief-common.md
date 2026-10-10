# Every agent reads this first

This page holds the standing rules for every Tsukimichi agent. Your own brief adds the goal, the files and the acceptance criteria. If your brief and this page disagree, your brief wins.

## Never
- **Repo:** never push, merge, tag or release, and never change `Version` in `Tsukimichi/Tsukimichi.csproj` or edit `CHANGELOG.md`. Return changelog bullets instead.
- **Config:** never edit Claude or hub settings, hooks, memory files or `~/.claude/*`.
- **Secrets:** never read or print secrets (.env files, credential stores, tokens).
- **Tests and checks:** never delete, skip or loosen tests, analyzers, warnings or pipeline checks to get green. Never add `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>` or `Skip`. If a check looks wrong, stop and report it.
- **Localization:** it's frozen. Add English strings only.
- **Read-only work:** a reviewer or researcher edits nothing in the repo or worktree, and runs no `dotnet build`, `dotnet test` or `mfl.py build`/`convert`/`stage`/`selftest` there. Use the prebuilt outputs and renders your brief names.

## Gates (implementers, after the last edit)
Run `pwsh tools/gate.ps1`. It runs the four gates below, writes the full logs outside the repo (`$env:TEMP/tsukimichi-gates/<hash>/logs`), and prints only each gate's exit code, the test counts, the first ~15 error lines of a failure and the log folder.
- **Skip if unchanged:** after a full run that passed, a rerun with the same HEAD, the same changed and untracked file contents prints `unchanged since last PASS (<time>)` and exits 0 in about a second. Any edit, even one `.cs` file, runs everything again. `-Force` reruns regardless; `-Only build,test` (also `load`, `themes`) runs a subset and never stamps.
- **Intermittent tests:** a failing test named in `$Intermittent` in `tools/gate.ps1` is rerun alone once; if it passes, the summary says `intermittent, passed on rerun: <name>` and the gate passes. The list is empty now, so every failure fails the gate; a test that starts failing now and then gets fixed at its root, not listed. Quote any such note in your hand-back.
- Exit code 0 means every gate passed, 1 otherwise.

The raw commands (the fallback, and what `gate.ps1` runs):
```
dotnet build Tsukimichi.sln -c Release -warnaserror > "$LOG/build.log" 2>&1; echo "build $?"
dotnet run --project tools/Tsukimichi.LoadCheck -c Release --no-build > "$LOG/load.log" 2>&1; echo "load $?"
export TSUKIMICHI_GAME_PATH="C:/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY XIV Online/game/sqpack"
dotnet test Tsukimichi.sln -c Release --no-build --filter "Category!=Perf" > "$LOG/test.log" 2>&1; echo "test $?"; grep -E "Passed!|Failed!|\[FAIL\]" "$LOG/test.log" | head
py -3 tools/themes/build_themes.py --check > "$LOG/themes.log" 2>&1; echo "themes $?"
```
- `$LOG` is a folder in your scratchpad.
- The tests pass only when the output contains `Failed:     0,`.
- No test is known to fail now and then. If one fails, rerun it alone and report it with the failure; do not assume it is a flake.

## Result caches (do not redo work on unchanged inputs)
- **Level builds:** `py -3 tools/moonfall-levels/mfl.py build <ids>` skips a level whose layout, scene recipe, painting or game texture, the `mflkit`/design-kit sources and the built mfcheck are unchanged since its last PASS (it prints `unchanged since last PASS`). `--force` rebuilds.
- **Self-test:** `build` and `selftest` skip it (one line) when a previous run passed on the same `mflkit`/`mfl.py`, design kit, built tools and fixtures; it is not rerun for a new level. On a miss it takes about 2 minutes (sections in parallel). `--force-selftest` reruns. Never edit `mflkit/*.py` just to "try" something: it invalidates the stamp for everyone sharing the cache.
- **The fast loop (authors and fixers):** after every layout edit run `py -3 tools/moonfall-levels/mfl.py check <id>` (about 3 s: pre-flight, loader, sweep, reach, the 48 rule games; prints PASS or each problem with the piece's tag, x, y and a hint; writes nothing). Only when it passes run `build <id>`; `build` stops after that same cheap phase when it fails (`--keep-going` runs the rest). `check`, `build` and `stage` take `--json` (`{verdict, fails:[{rule, piece, x, y, hint}], figures}` on stdout) when you want to read the result in code. `stuck <id>` and `dead <id>` show where balls rest and where dead shots fly.
- **Greedy-play games:** `engine.play` (used by `build`, `stage`, `ease`) re-uses a run on an unchanged level file, seed range and mfcheck build. `stage <n> --force` or `MFL_NO_CACHE=1` replays.
- **Renders:** `Tsukimichi.MoonfallRender <out.png> ... --cache <dir>` copies the stored PNG when its arguments, the loaded `Tsukimichi.Core.dll` and `Tsukimichi.dll`, and the level/scene JSON and shipped Moonfall art all match; without the flag nothing changes. A game patch needs a fresh cache folder.
- The level caches (self-test stamp, play results, graded scenes, build stamps) and a mirror of the game textures live outside the worktree, in `%LOCALAPPDATA%/tsukimichi-moonfall-cache` (override with the environment variable `MOONFALL_CACHE`), keyed by content, so every worktree shares them and a fresh worktree needs no `fetch`: `build`, `selftest` and `convert` copy missing textures from the mirror and fetch from the install only what is in neither. They never change a gate's rule or threshold; delete that folder to reset.
- `Tsukimichi.MoonfallRender --cache`: one level's picture (`--scene-only` or an explicit `--level` board) is keyed on that level only, so editing another level keeps it cached.

## Keep your context small
- **Read narrowly:** read files by line range (`wc -l` first, never `cat` several files at once). Cap search output (`rg -n pat | head -40`). Run `git diff --stat` before a full diff.
- **Logs to files:** send build, test, render and pipeline logs to files and print only exit codes and pass/fail lines.
- **Scratch:** scratch files go in your scratchpad, never the repo root.
- **Hand over before you get big:** at about 300k context, or at a green commit after a phase, write a handoff and say so in your hand-back. A fresh agent continues from it.

## Handoff (about one page)
```
PHASE <n> -> <n+1>   branch/worktree: <path>   last green commit: <sha>
GOAL: 2 lines, plus the brief's path
DONE: bullets, each with its commit
DECISIONS: owner or reviewer decisions, one line each, with where they are recorded
NEXT: an ordered list; the first item concrete (file, function, command)
OPEN: questions, flaky tests, reviewer findings not yet fixed
GATES: exact commands and the last result
DO NOT: traps found
READ FIRST: at most 5 paths
```

## Returning your result
- **Implementers** (at most about 450 words):
  - branch and commit;
  - `git diff --stat`;
  - gate commands with exit codes and test counts;
  - deviations from the brief;
  - open items and what's unverified;
  - changelog bullets.
- **Reviewers and supervisors:**
  - the full report is your final message, and it is saved verbatim to the record;
  - start it with `VERDICT: APPROVE|REVISE (B blockers, M majors, m minors)` on one line;
  - next, give a table of findings: id, severity, item, one-line issue, file:line or render evidence;
  - then the detail;
  - keep it under about 12 KB;
  - no preamble, and don't restate the brief.
- **Researchers** (at most about 700 words): answer, actions, cited evidence, confidence.
