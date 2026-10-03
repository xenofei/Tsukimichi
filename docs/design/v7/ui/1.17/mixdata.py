"""1.17 T10: the numbers the mix table reads, pulled from the shipped metrics.json files (tools/themes/build_themes.py).

For every set S: own[S]["a|b"] (S's two states side by side, the row tier at 16 px on Night, worst over grey, Vienot
deuteranopia and Machado protan/deutan/tritan); cross[S][T]["a|b"] (S's state a beside T's state b, the build's cross
table, row tier, 16 px, worst over every vision mode); salience[S][state] (row tier, Night, 16 px, greyscale).
Also runs the warning rules and the Fix-it search over every single-set mix and every one-change mix, and writes
mixdata.json plus a short report. Run: py -3 -X utf8 mixdata.py
"""
import itertools, json, pathlib

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[4]
THEMES = ROOT / "Tsukimichi" / "assets" / "ui" / "themes"
SETS = ["medallion", "ishgard-glass", "aether-crystal"]
STATES = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"]

own, cross, sal, salh, bars, ownModes = {}, {}, {}, {}, None, {}
for s in SETS:
    m = json.loads((THEMES / s / "metrics.json").read_text(encoding="utf-8"))
    bars = m["bars"]
    row = next(t for t in m["tiers"] if t["name"] == "row")
    own[s] = {}
    ownModes.setdefault(s, {})
    for meas in row["measures"]:
        if meas["ground"] != "night" or meas["px"] != 16:
            continue
        ownModes[s][meas["mode"]] = meas["pairs"]
        for k, v in meas["pairs"].items():
            own[s][k] = min(own[s].get(k, 999), v)
        if meas["mode"] == "grey":
            sal[s] = meas["salience"]
    cross[s] = {t: v["row"]["16"] for t, v in m["cross"]["sets"].items() if t in SETS}
    hero = next(t for t in m["tiers"] if t["name"].startswith("hero"))
    salh[s] = next(x["salience"] for x in hero["measures"] if x["ground"] == "night" and x["px"] == 16 and x["mode"] == "grey")

def pair(mix, a, b):
    A, B = mix[a], mix[b]
    if A == B:
        return own[A].get(f"{a}|{b}", own[A].get(f"{b}|{a}"))
    return cross[A][B].get(f"{a}|{b}") or cross[B][A].get(f"{b}|{a}")

def evaluate(mix):
    """Spec-1.17 §A3: only pairs from two different sets can warn (a set's own pairs passed its build gates); Ready's
    lead and Completed's recession are checked at the row tier and the hero tier."""
    close, hard = [], []
    for a, b in itertools.combinations(STATES, 2):
        if mix[a] == mix[b]:
            continue
        v = pair(mix, a, b)
        if v is None:
            continue
        if v < bars["mixHard"]:
            hard.append((a, b, v))
        elif v < bars["mixClose"]:
            close.append((a, b, v))
    leads, comps = {}, {}
    for tier, S in (("row", sal), ("hero", salh)):
        r = S[mix["ready"]]["ready"]
        leads[tier] = round(r / max(S[mix[s]][s] for s in STATES if s != "ready"), 2)
        comps[tier] = round(S[mix["completed"]]["completed"] / r, 2)
    lead, comp = min(leads.values()), max(comps.values())
    return {"close": close, "hard": hard, "readyLead": leads, "completedOfReady": comps,
            "ok": not close and not hard and lead >= bars["mixReadyLead"] and comp <= bars["completedOfReady"]}

def fix(mix, keep=None):
    """Research §5.2, refined (spec-1.17 §A4): try every single-state change except the state the player just picked
    (`keep`: Fix it never undoes the player's own choice); offer the one that clears every warning, preferring the set
    that already covers the most states, then the earliest state. None when no single change clears them."""
    best = None
    for st in STATES:
        if st == keep:
            continue
        for s in SETS:
            if s == mix[st]:
                continue
            m2 = dict(mix); m2[st] = s
            if evaluate(m2)["ok"]:
                cover = sum(1 for x in STATES if m2[x] == s)
                key = (-cover, STATES.index(st))
                if best is None or key < best[0]:
                    best = (key, st, s)
    return None if best is None else {"state": best[1], "set": best[2]}

EX = {st: "medallion" for st in STATES}; EX["ready"] = "aether-crystal"
FIXED = dict(EX); FIXED["completed"] = "aether-crystal"

if __name__ == "__main__":
    report = []
    for s in SETS:
        e = evaluate({st: s for st in STATES})
        report.append(f"{s} alone: lead {e['readyLead']}, comp {e['completedOfReady']}, close {e['close']}, hard {e['hard']}")
    # every one-state change away from each pure set: which mixes warn
    warn = []
    for base in SETS:
        for st in STATES:
            for s in SETS:
                if s == base:
                    continue
                mix = {x: base for x in STATES}; mix[st] = s
                e = evaluate(mix)
                if not e["ok"]:
                    warn.append({"base": base, "state": st, "set": s, **{k: e[k] for k in ("close", "hard", "readyLead", "completedOfReady")}, "fix": fix(mix, keep=st)})
    out = {"sets": SETS, "states": STATES, "bars": bars, "own": own, "cross": cross, "salience": sal, "salienceHero": salh, "ownModes": ownModes, "warnings": warn,
           "example": {"mix": EX, "eval": evaluate(EX), "fix": fix(EX, keep="ready"), "fixed": evaluate(FIXED)}}
    (HERE / "mixdata.json").write_text(json.dumps(out, indent=1), encoding="utf-8")
    print("\n".join(report))
    print(len(warn), "one-change mixes warn")
    for w in warn[:40]:
        print(w)
