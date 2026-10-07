#!/usr/bin/env python3
"""Saves a subagent's hand-back report verbatim to a record file without printing it.

The coordinator uses this to put a reviewer's report into docs/design/**/supervisor/ while keeping the full text out
of its own context (docs/team/workflow-audit-2026-10-06.md, recommendation 2). It prints only the byte count and the
report's first VERDICT line, so a duplicate delivery or a missing report is obvious at a glance.

Usage:
  py -3 tools/claude-usage/save_report.py <agent-id> <out-path> [--session <session-id>]
The agent's transcript is <projects>/<session>/subagents/agent-<agent-id>.jsonl; the newest session folder that holds it
is used when --session is not given. The last SubagentHandback message is saved (an agent resumed for a later round
writes a new one).
"""
import argparse, glob, json, os, sys

PROJECTS = os.path.join(os.path.expanduser("~"), ".claude", "projects", "c--Users-devon-Desktop-Tsukimichi--Main-Repo-")


def transcript(agent, session):
    pattern = os.path.join(PROJECTS, session or "*", "subagents", f"agent-{agent}.jsonl")
    hits = sorted(glob.glob(pattern), key=os.path.getmtime)
    if not hits:
        sys.exit(f"no transcript for agent {agent}")
    return hits[-1]


def last_handback(path):
    report = None
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if '"SubagentHandback"' not in line:
                continue
            try:
                d = json.loads(line)
            except ValueError:
                continue
            for c in (d.get("message") or {}).get("content") or []:
                if isinstance(c, dict) and c.get("type") == "tool_use" and c.get("name") == "SubagentHandback":
                    report = (c.get("input") or {}).get("message")
    return report


def main():
    a = argparse.ArgumentParser()
    a.add_argument("agent")
    a.add_argument("out")
    a.add_argument("--session")
    args = a.parse_args()
    report = last_handback(transcript(args.agent, args.session))
    if not report:
        sys.exit(f"agent {args.agent} has no hand-back yet")
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="") as f:
        f.write(report if report.endswith("\n") else report + "\n")
    verdict = next((l.strip() for l in report.splitlines() if "VERDICT" in l.upper()), "(no VERDICT line)")
    print(f"saved {len(report.encode('utf-8'))} bytes -> {args.out}\n{verdict[:200]}")


if __name__ == "__main__":
    main()
