#!/usr/bin/env python3
"""Token monitor: streams a Claude Code session folder + its coordinator jsonl, prints a compact
report and writes a JSON snapshot (default: %TEMP%/tsukimichi-claude-usage/, never the repo).
Read-only on transcripts. The optimization team's monitor (docs/team/optimization-team.md).

Usage:
  py -3 monitor.py <session-folder-or-id> [--prev snapshot.json] [--out snapshot.json] [--json-only]
  <session-folder> = .../projects/<proj>/<session-id>   (coordinator = ../<session-id>.jsonl,
                      workers = <session-folder>/subagents/agent-*.jsonl)
  A bare session id is resolved under the default Tsukimichi project dir.
"""
import json, os, re, sys, glob, statistics, collections, argparse, time
from datetime import datetime

PROJ = "C:/Users/devon/.claude/projects/c--Users-devon-Desktop-Tsukimichi--Main-Repo-"
LIMIT_RE = re.compile(r"(session limit|usage limit|hit your .*limit|rate.?limit|overloaded|529)", re.I)
ROLE_RES = [
    ("reviewer", re.compile(r"review|supervisor|critic|audit|grade|evaluat", re.I)),
    ("researcher", re.compile(r"research|investigat|survey|find out|explore|analy[sz]e", re.I)),
    ("implementer", re.compile(r"implement|build|fix|create|write|add |port|refactor|you are .*(develop|engineer|artist|designer)", re.I)),
]


def pct(vals, p):
    if not vals: return 0
    s = sorted(vals); return s[min(len(s) - 1, int(len(s) * p / 100))]


def ts(s):
    try: return datetime.fromisoformat(s.replace("Z", "+00:00")).astimezone()
    except Exception: return None


def first_line(content):
    if isinstance(content, list):
        content = " ".join(b.get("text", "") for b in content if isinstance(b, dict) and b.get("type") == "text")
    for ln in (content or "").splitlines():
        if ln.strip(): return ln.strip()[:110]
    return ""


def role_of(meta, fl):
    txt = (meta.get("description", "") + " " + meta.get("agentType", "") + " " + fl)
    for r, rx in ROLE_RES:
        if rx.search(txt): return r
    return "other"


def blank():
    return dict(calls=0, cr=0, cc=0, unc=0, out=0)


def scan(path, is_coord=False):
    """One streaming pass. Returns per-file stats dict."""
    st = dict(calls=0, cr=0, cc=0, unc=0, out=0, ctxs=[], models=collections.Counter(),
              tools=collections.Counter(), tbytes=collections.Counter(), first="", t0=None, t1=None,
              days=collections.defaultdict(blank), limits=[], apierr=[], compacts=[], series=[],
              last_assistant_limit=False)
    seen = {}; tool_names = {}
    last_ctx = None
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if '"type":"assistant"' not in line and '"type":"user"' not in line and '"type":"system"' not in line:
                continue
            try: d = json.loads(line)
            except Exception: continue
            t = d.get("type"); tstamp = d.get("timestamp")
            if tstamp:
                if st["t0"] is None: st["t0"] = tstamp
                st["t1"] = tstamp
            if t == "system":
                sub = d.get("subtype")
                if sub == "compact_boundary":
                    cm = d.get("compactMetadata", {})
                    st["compacts"].append(dict(ts=tstamp, pre=cm.get("preTokens"), trigger=cm.get("trigger"), post=None))
                    last_ctx = "compact"
                elif sub == "api_error" and is_coord is not None:
                    e = d.get("error", {}) or {}
                    st["apierr"].append(dict(ts=tstamp, msg=(e.get("formatted") or e.get("message") or "")[:80],
                                             status=d.get("error", {}).get("status") if isinstance(e, dict) else None))
                continue
            if t == "user":
                if d.get("isCompactSummary"): continue
                m = d.get("message", {}); c = m.get("content")
                if not st["first"] and not d.get("isSidechain") is None:
                    st["first"] = first_line(c)
                if isinstance(c, list):
                    for b in c:
                        if isinstance(b, dict) and b.get("type") == "tool_result":
                            cc_ = b.get("content"); n = 0
                            if isinstance(cc_, str): n = len(cc_)
                            elif isinstance(cc_, list):
                                n = sum(len(x.get("text", "")) for x in cc_ if isinstance(x, dict))
                            st["tbytes"][tool_names.get(b.get("tool_use_id"), "?")] += n
                continue
            # assistant
            m = d.get("message", {}); u = m.get("usage") or {}
            content = m.get("content")
            model = m.get("model", "?")
            is_err = d.get("isApiErrorMessage") or model == "<synthetic>"
            if isinstance(content, list):
                for b in content:
                    if not isinstance(b, dict): continue
                    if b.get("type") == "tool_use":
                        st["tools"][b.get("name", "?")] += 1; tool_names[b.get("id")] = b.get("name", "?")
                    elif b.get("type") == "text" and (is_err or "limit" in b.get("text", "")[:120]) and LIMIT_RE.search(b.get("text", "")[:200]) and is_err:
                        q = d.get("quotaLimits") or {}
                        st["limits"].append(dict(ts=tstamp, text=b["text"][:100], kind=q.get("rateLimitType"),
                                                 resets=q.get("resetsAt"), status=d.get("apiErrorStatus")))
                        st["last_assistant_limit"] = True
            if is_err: continue
            st["last_assistant_limit"] = False
            mid = m.get("id") or d.get("requestId") or d.get("uuid")
            vals = (u.get("input_tokens", 0) or 0, u.get("cache_read_input_tokens", 0) or 0,
                    u.get("cache_creation_input_tokens", 0) or 0, u.get("output_tokens", 0) or 0)
            if mid in seen:  # streamed duplicate of same message: keep the latest usage, don't recount
                continue
            seen[mid] = 1
            unc, cr, cc, out = vals
            ctx = unc + cr + cc
            st["calls"] += 1; st["cr"] += cr; st["cc"] += cc; st["unc"] += unc; st["out"] += out
            st["ctxs"].append(ctx); st["models"][model] += 1
            if last_ctx == "compact" and st["compacts"]:
                st["compacts"][-1]["post"] = ctx
            last_ctx = ctx
            dt = ts(tstamp) if tstamp else None
            if dt:
                dd = st["days"][dt.strftime("%Y-%m-%d")]
                dd["calls"] += 1; dd["cr"] += cr; dd["cc"] += cc; dd["unc"] += unc; dd["out"] += out
                if is_coord and (len(st["ctxs"]) % 25 == 1):
                    st["series"].append((tstamp, ctx))
    return st


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("session"); ap.add_argument("--prev"); ap.add_argument("--out")
    ap.add_argument("--json-only", action="store_true")
    a = ap.parse_args()
    folder = a.session.replace("\\", "/").rstrip("/")
    if not os.path.isdir(folder): folder = f"{PROJ}/{folder}"
    sid = os.path.basename(folder)
    coord_path = os.path.join(os.path.dirname(folder), sid + ".jsonl")
    out_path = a.out or os.path.join(os.environ.get("TEMP") or os.environ.get("TMPDIR") or "/tmp", "tsukimichi-claude-usage", f"snapshot-{sid[:8]}-{time.strftime('%Y%m%d-%H%M%S')}.json")
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    t_start = time.time()

    agents = []
    # Workflow agents log under subagents/workflows/<run>/, ordinary agents directly under subagents/.
    paths = glob.glob(f"{folder}/subagents/agent-*.jsonl") + glob.glob(f"{folder}/subagents/workflows/*/agent-*.jsonl")
    for p in sorted(paths):
        aid = os.path.basename(p)[6:-6]
        meta = {}
        mp = p[:-6] + ".meta.json"
        if os.path.exists(mp):
            try: meta = json.load(open(mp, encoding="utf-8"))
            except Exception: pass
        s = scan(p)
        s["id"] = aid; s["meta"] = meta; s["role"] = role_of(meta, s["first"]); s["desc"] = meta.get("description", "")
        agents.append(s)
    coord = scan(coord_path, is_coord=True) if os.path.exists(coord_path) else None
    if coord: coord["role"] = "coordinator"; coord["id"] = "coordinator"; coord["desc"] = "coordinator"

    allr = ([coord] if coord else []) + agents

    def inp(s): return s["cr"] + s["cc"] + s["unc"]
    tot = lambda rs: dict(agents=len(rs), calls=sum(r["calls"] for r in rs), cr=sum(r["cr"] for r in rs),
                          cc=sum(r["cc"] for r in rs), unc=sum(r["unc"] for r in rs), out=sum(r["out"] for r in rs))
    snap = {"session": sid, "generated": time.strftime("%Y-%m-%dT%H:%M:%S"), "totals": tot(allr),
            "workers": tot(agents), "coordinator": tot([coord]) if coord else None}
    # roles
    roles = {}
    for r in allr:
        x = roles.setdefault(r["role"], dict(agents=0, calls=0, inp=0, out=0)); x["agents"] += 1
        x["calls"] += r["calls"]; x["inp"] += inp(r); x["out"] += r["out"]
    snap["roles"] = roles
    # context
    def cstat(rs):
        c = [v for r in rs for v in r["ctxs"]]
        return dict(median=pct(c, 50), p90=pct(c, 90), max=max(c) if c else 0, mean=int(statistics.fmean(c)) if c else 0)
    snap["ctx_workers"] = cstat(agents); snap["ctx_coord"] = cstat([coord]) if coord else None
    snap["over600k"] = [dict(id=r["id"], max=max(r["ctxs"]), desc=r["desc"][:60]) for r in allr if r["ctxs"] and max(r["ctxs"]) > 600000]
    mm = collections.Counter(); tc = collections.Counter(); tb = collections.Counter()
    for r in allr:
        for k, v in r["models"].items(): mm[(r["role"] == "coordinator" and "coord:" or "agent:") + k] += v
        tc.update(r["tools"]); tb.update(r["tbytes"])
    snap["models"] = dict(mm); snap["tools"] = dict(tc.most_common(15))
    snap["tool_result_chars"] = dict(tb.most_common(15)); snap["tool_result_chars_total"] = sum(tb.values())
    top = sorted(agents, key=inp, reverse=True)[:10]
    snap["top_agents"] = [dict(id=r["id"], inp=inp(r), turns=r["calls"], role=r["role"], desc=r["desc"][:50], first=r["first"][:80]) for r in top]
    # per day
    days = collections.defaultdict(lambda: dict(coord_calls=0, agent_calls=0, inp=0, out=0, coord_inp=0, limits=0))
    for r in allr:
        for dk, v in r["days"].items():
            x = days[dk]; i = v["cr"] + v["cc"] + v["unc"]
            x["inp"] += i; x["out"] += v["out"]
            if r["role"] == "coordinator": x["coord_calls"] += v["calls"]; x["coord_inp"] += i
            else: x["agent_calls"] += v["calls"]
    # limits
    limits = []
    for r in allr:
        for L in r["limits"]:
            dt = ts(L["ts"]) if L["ts"] else None
            L2 = dict(L, agent=r["id"], role=r["role"], desc=r["desc"][:50], day=dt.strftime("%Y-%m-%d") if dt else "?",
                      last_line=(r["last_assistant_limit"] and L is r["limits"][-1]), turns=r["calls"],
                      ctx=r["ctxs"][-1] if r["ctxs"] else 0)
            limits.append(L2)
            if L2["day"] in days: days[L2["day"]]["limits"] += 1
    snap["days"] = dict(sorted(days.items()))
    snap["limit_stops"] = limits
    by_reset = collections.Counter((L["kind"], L["resets"]) for L in limits)
    snap["limit_windows"] = [dict(kind=k[0], resets_epoch=k[1],
                                  resets=datetime.fromtimestamp(k[1]).strftime("%m-%d %H:%M") if k[1] else None,
                                  hits=n, agents=sorted({L["agent"] for L in limits if (L["kind"], L["resets"]) == k})) for k, n in by_reset.most_common()]
    ae = collections.Counter(e["msg"] for r in allr for e in r["apierr"])
    snap["api_retry_errors"] = dict(ae.most_common(6))
    snap["api_error_by_agent"] = {r["id"]: len(r["apierr"]) for r in allr if r["apierr"] and r["id"] != "coordinator"}
    # coordinator growth
    if coord:
        snap["coord_compactions"] = coord["compacts"]
        series = coord["series"]
        snap["coord_ctx_series"] = [dict(ts=t, ctx=c) for t, c in series]
        snap["coord_ctx_by_day"] = {}
        bd = collections.defaultdict(list)
        for t, c in series:
            dt = ts(t)
            if dt: bd[dt.strftime("%Y-%m-%d")].append(c)
        for k, v in sorted(bd.items()):
            snap["coord_ctx_by_day"][k] = dict(median=pct(v, 50), max=max(v), samples=len(v))
    snap["runtime_s"] = round(time.time() - t_start, 1)
    json.dump(snap, open(out_path, "w", encoding="utf-8"), indent=1, default=str)

    if a.json_only: print(out_path); return
    M = lambda n: f"{n/1e6:,.1f}M"; B = lambda n: f"{n/1e9:,.2f}B"
    T = snap["totals"]; I = T["cr"] + T["cc"] + T["unc"]
    print(f"SESSION {sid}  ({snap['runtime_s']}s scan)")
    print(f"TOTAL calls {T['calls']:,} | input {B(I)} (read {B(T['cr'])} {T['cr']/I*100:.1f}% | write {B(T['cc'])} | uncached {M(T['unc'])}) | out {M(T['out'])}")
    c = snap["coordinator"]
    if c: print(f"COORD calls {c['calls']:,} input {B(c['cr']+c['cc']+c['unc'])} out {M(c['out'])} | ctx {snap['ctx_coord']}")
    print(f"WORKERS agents {snap['workers']['agents']} calls {snap['workers']['calls']:,} | ctx {snap['ctx_workers']}")
    print("ROLES", {k: (v['agents'], B(v['inp']), M(v['out'])) for k, v in roles.items()})
    print("MODELS", snap["models"])
    print("TOOLS", dict(list(snap["tools"].items())[:8]), "| result chars", f"{snap['tool_result_chars_total']/1e6:,.0f}M",
          "top:", {k: f"{v/1e6:.0f}M" for k, v in list(snap["tool_result_chars"].items())[:5]})
    print("OVER600K", [(o["id"][:8], o["max"] // 1000) for o in snap["over600k"]])
    print("TOP10"); [print(f"  {t['id'][:9]} {M(t['inp']):>9} {t['turns']:>5}t {t['role'][:5]} {t['desc'] or t['first']}") for t in snap["top_agents"]]
    print("PER DAY (coord calls | agent calls | input | output | limit hits)")
    for k, v in snap["days"].items():
        print(f"  {k} {v['coord_calls']:>5} {v['agent_calls']:>6} {B(v['inp']):>8} {M(v['out']):>7} {v['limits']:>3}")
    print("LIMIT WINDOWS", [(w["kind"], w["resets"], w["hits"], len(w["agents"])) for w in snap["limit_windows"]])
    print("API RETRY ERRORS", snap["api_retry_errors"])
    if coord:
        print("COORD COMPACTIONS", [(cp["ts"][5:16], cp["trigger"], (cp["pre"] or 0) // 1000, (cp["post"] or 0) // 1000) for cp in coord["compacts"]])
        print("COORD CTX BY DAY", {k: (v["median"] // 1000, v["max"] // 1000) for k, v in snap["coord_ctx_by_day"].items()})
    if a.prev and os.path.exists(a.prev):
        p = json.load(open(a.prev)); fl = []
        def chk(n, new, old):
            if old and abs(new - old) / old > 0.2: fl.append(f"{n}: {old:,} -> {new:,} ({(new-old)/old*100:+.0f}%)")
        chk("calls", T["calls"], p["totals"]["calls"]); chk("out", T["out"], p["totals"]["out"])
        chk("input", I, p["totals"]["cr"] + p["totals"]["cc"] + p["totals"]["unc"])
        chk("worker ctx p90", snap["ctx_workers"]["p90"], p["ctx_workers"]["p90"])
        print("VS PREV >20%:", fl or "none")
    print("SNAPSHOT", out_path)


if __name__ == "__main__":
    main()
