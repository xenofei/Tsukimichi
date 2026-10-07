# Workflow audit, 6 October 2026

This is the optimization team's first audit. It covers the session that took Moonfall from plan to its 1.23.0 preview, from 27 September to 6 October 2026. Each specialist measured its own layer from the session logs.

The full reports were kept in the session scratchpad (`opt/reports/`), which isn't in the repo; their findings are summarized below. The reusable measuring tool is `tools/claude-usage/monitor.py`.

## The baseline (corrected)

Claude Code logs each reply once per content block, so a naive count doubles everything. The figures below count each model call once.

| Measure | Value |
|---|---|
| Model calls | about 52,000: coordinator 2,965; 531 agents 49,003 |
| Input tokens | 14.1 B: 98% read from the prompt cache, 0.28 B written to it, almost none uncached |
| Output tokens | about 4 M |
| Context per agent call | median 221 k, p90 469 k; 18 agents passed 600 k |
| Coordinator context | median 506 k; it climbs to the 967 k ceiling and auto-compacts (8 compactions) |
| Models | Opus 5.5 on 48,307 calls, Fable 5.1 on 3,570, Sonnet 5.5 on 91 |
| Usage-limit stops | 46, clustered when several long agents hit the same 5-hour reset (25 on 3 October alone) |
| Biggest agents | level pipeline 530 M, screens 493 M, v7 UI design 266 M, runtime art 254 M |

**Where the cost sits.** At Opus 5.5 list prices, which only illustrate proportions on a subscription:

| Part | Calculation | Cost | Share |
|---|---|---|---|
| Cache reads | 13.8 B × $0.20/M | about $2.8k | about 65% |
| Cache writes | 0.28 B × $5/M | about $1.4k | about 33% |
| Output | | | about 2% |

**What that means.**
- **What drives cost:** the cost is driven by how big each agent's context is times how many calls it makes, plus how often a large context is rewritten into the cache.
- **What doesn't:** the wording of prompts and the length of outputs are minor.
- **Model choice saves less than it looks:** Opus 5.5 and Sonnet 5.5 cost the same per cache read, so a cheaper model saves mainly on cache writes, output and plan usage.

## Recommendations, highest effect first

### Adopted now as working practice (no setting changes)

1. **Fresh agents per phase, never a giant one.**
   - Long work runs as a chain of agents, each taking one phase: one review round, or about 300 to 400 turns, or about 300 k context, whichever comes first.
   - Each hands over a one-page handoff: goal, done with commits, decisions, next steps, open items, gates, traps, and at most five files to read first.
   - A stalled agent with a large context is never resumed; a fresh one starts from its handoff.
   - Estimated effect: 50 to 70% less input on the longest runs. It also removes most of the full-cache rewrites after stalls, which were 29% of all cache writes.
2. **Reviewer reports stay out of the coordinator's context.**
   - Reviewers return a verdict table of at most about 600 words.
   - The full verbatim report is copied straight into `docs/design/**/supervisor/` by a script that never prints it.
   - The coordinator saves its state to a file and compacts at about 400 k instead of riding to the ceiling.
   - Estimated effect: the coordinator's context grows 20 to 30% more slowly (not measured). Duplicate deliveries become harmless.
3. **Caps and filters.**
   - **Report caps:** every brief ends with a Return section and a length cap: reviewers about 4 KB, implementers about 3 KB, researchers about 5 KB.
   - **Command output:** read files by line range, cap grep output, run `git diff --stat` before a full diff, and send build and test logs to a file, then print only pass or fail lines.
   - Moderate effect on the coordinator; small on workers.
4. **Model routing.**
   - **Opus:** design, art, level authoring, supervisor sign-off, releases.
   - **Sonnet:** research, fold-in fixers that apply a named list of findings, and re-check rounds.
   - **Haiku (or a script):** mechanical steps such as extracting reports, file conversions and gate runs.
   - Changing effort keeps the cache; switching an agent's model mid-run loses it.
5. **Stagger long agents** so they don't hit the same 5-hour reset together.

### Tools to build (small, in the repo)

6. **A gate wrapper.** It hashes the tracked sources and skips build, LoadCheck, tests and the themes check when nothing changed. It prints only pass or fail and the first errors. Up to 20 to 30% of gate runs were repeats on unchanged code.
7. **Result caches** keyed on input hashes, for MoonfallRender renders, `mfl.py build`, greedy-play difficulty runs and texture dumps.
8. **One shared file for brief boilerplate:** gates, the never-push rule, frozen localization. Its value is consistency more than tokens.
9. **The monitor:** `tools/claude-usage/monitor.py`. Done. Run it after each large phase and compare snapshots.

### Needs the owner's approval

10. **Moonfall's remaining 105 levels as a Workflow.**
    - A charter for all 21 stages, settled once.
    - One short author agent per level, with three stages in flight.
    - Script gates and a review pack per stage.
    - One review round per stage by fresh reviewers, then a check of the fixes.
    - Estimated 45% fewer tokens and about 3× faster than repeating the first-10 process. That's an estimate, not a measurement.
    - Workflows need your opt-in each time.
11. **Settings for long unattended runs:**
    - `CLAUDE_CODE_RETRY_WATCHDOG=1` (keeps retrying on overload);
    - a `fallbackModel`;
    - a lower auto-compact window (about 400 k);
    - turning off plugins a C# project never uses (Ruby/Rails reviewers, Jira, Office skills), which trims about 3 to 4% of every call's fixed overhead.
12. **Remove about 1,000 characters of duplicated lines from the global CLAUDE.md.** A tiny saving.
13. **Old smoke-test pages** (`docs/smoke-test.html`, `docs/ui-smoke-checklist*.md`) contradict the "no smoke-test pages" rule. Delete them?

### Considered and rejected

- **One-hour cache for agents.** Only 0.4% of agent calls came after a gap over 5 minutes, and one-hour writes cost twice as much.
- **Reusing warm reviewers between rounds.** Rounds are hours apart, so their cache expires anyway. The saving is under 2%, at the cost of fresh eyes.
- **The Batch API for agent work.** Agent loops are interactive. It suits only offline bulk jobs, at 50% off.
- **Compressing briefs.** They already point at files instead of pasting them; that's about 0.002% of input.

## Already done

- The eight specialists are in `.claude/agents/opt-*.md`.
- Their definitions are corrected with this audit's facts: agent cache lifetime, price facts, and the monitor script, which moved to Haiku.
- The monitor is committed.
- The project memory is refreshed: it had still described plan v6 and 1.22.1 as current.
