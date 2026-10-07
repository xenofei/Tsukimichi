# The optimization team

These are Tsukimichi's permanent specialists for cost, speed and reliability of the AI-assisted workflow. They were added on 6 October 2026 at the owner's request.

**Where they live.** The definitions are in `.claude/agents/`, so they load in every Claude Code session opened on this repo. A session that is already running picks them up only after a restart. Each one advises; the coordinator makes the changes. They run on Sonnet unless the work needs more.

**The token-optimization layers**

| Layer | Agent | Use it when |
|---|---|---|
| 1. Prompt compression | `opt-prompt-compression` | before a large or repeated brief; when instructions or briefs grow |
| 2. Caching | `opt-caching` | planning multi-round work; agents redo expensive steps; cache writes climb |
| 3. Model routing | `opt-model-routing` | writing briefs or agent definitions; expensive models on routine work |
| 4. Output length | `opt-output-length` | writing briefs; hand-backs or tool output bloat the coordinator |
| 5. Monitoring | `opt-token-monitor` | after each large phase, before and after an optimization, on cost or limit questions |

**The other three**

| Specialist | Agent | Use it when |
|---|---|---|
| Batch processing | `opt-batch-processing` | any job with many similar items (levels, portraits, reviews, renders) |
| Memory and context | `opt-memory-context` | multi-session work, before compaction, agents past ~400k context, memory drift |
| Claude research | `opt-claude-research` | questions that need current Claude facts; after a Claude release; periodic playbook refresh |

**Standing rhythm**
- **After every large phase,** run the token monitor and compare with the previous snapshot.
- **Before every large job,** run batch processing and model routing on the plan.
- **Before every long brief,** run prompt compression and output length on it.
- **About once a month, or after a Claude release,** run Claude research.

The baseline audit of 6 October 2026, the session that built Moonfall up to its 1.23.0 preview, is summarized in `docs/team/workflow-audit-2026-10-06.md`.
