"""Review tools, the data side: what a reviewer needs to know about a level or a stage without running a build.

  facts(src, lid)            one level's figures from its committed file and report (pieces, candidates, wins, band margin)
  stage_ids(key)             the level ids of a stage, in order
  status(key|None)           per level: build stamp, verdict, wins against the band, convert state, precheck, changes since review
  diff_pieces(old, new)      two level files compared piece by piece: added, removed, moved, candidate flags changed
  engine_render(...)         the plugin's own render of a level through MoonfallRender (--cache)

Everything reads the committed or working-tree artefacts (`precheck.Source`), so it runs without the game install and
without a build; only `engine_render` needs the game. The images are in reviewimg.py, the commands in reviewcmds.py.
"""
import glob
import hashlib
import json
import math
import os
import pathlib
import subprocess

from . import paths, precheck, stagecheck

REL = paths.LEVELS.relative_to(paths.REPO).as_posix()
CORE_LEVELS = paths.REPO / "Tsukimichi.Core" / "Moonfall" / "Levels"
PLUGIN_SCENES = paths.REPO / "Tsukimichi" / "assets" / "moonfall" / "scenes"
RENDER_DIR = paths.REPO / "tools" / "Tsukimichi.MoonfallRender"
GAME_PATH = "C:/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY XIV Online/game/sqpack"


# ------------------------------------------------------------------------------------------------------------- git
def git(*args):
    """stdout of a git command in the repo, or None when it fails."""
    r = subprocess.run(["git", "-C", str(paths.REPO), *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else None


def rel(p):
    return pathlib.Path(p).resolve().relative_to(paths.REPO.resolve()).as_posix()


def commit_time(*files):
    """Newest commit time among the files (0 when none is tracked), and whether any has uncommitted changes."""
    newest, dirty = 0, False
    for f in files:
        out = git("log", "-1", "--format=%ct", "--", rel(f))
        if out and out.strip():
            newest = max(newest, int(out.strip()))
        st = git("status", "--porcelain", "--", rel(f))
        dirty = dirty or bool(st and st.strip())
    return newest, dirty


def rev_short(rev):
    out = git("rev-parse", "--short", rev) if rev else None
    return out.strip() if out else (rev or "working tree")


# --------------------------------------------------------------------------------------------------------- levels
def code_of(stage, number):
    """The level's code as the player sees it: 3-1 for stage 3, number 11."""
    return f"{stage}-{(number - 1) % 5 + 1}" if stage and number else "?"


def level_meta(lid, src=None):
    """(stage, number, scene, campaign key) from the report, else from the layout's LEVEL dict."""
    src = src or precheck.Source()
    rep = src.json(f"report/{lid}.json") or {}
    if rep.get("stage"):
        return rep["stage"], rep["number"], rep.get("scene")
    try:
        from .build import load_layout
        m = load_layout(lid).LEVEL
        return m["stage"], m["number"], m["scene"]
    except Exception:
        return None, None, None


def stage_ids(key, src=None):
    """The ids of stage `key` (mr3, fs1, 3), ordered by number. Reads the working tree's layouts and reports."""
    key = stagecheck.stage_key(key)
    camp, n = key[:2], int(key[2:])
    src = src or precheck.Source()
    out = []
    for lid in src.ids():
        if stagecheck.level_campaign(lid) != camp:
            continue
        stage, number, _scene = level_meta(lid, src)
        if stage == n:
            out.append((number, lid))
    return [lid for _n, lid in sorted(out)]


def stage_of(lid, src=None):
    stage, _n, _s = level_meta(lid, src)
    camp = stagecheck.level_campaign(lid)
    return f"{camp}{stage}" if stage and camp else None


def facts(src, lid):
    """One level's figures from the files `src` reads (the working tree, or a git revision)."""
    L = precheck.Level(src, lid)
    f = L.file
    out = {"id": lid, "name": L.name, "stage": L.stage, "number": L.number, "code": code_of(L.stage, L.number),
           "has_file": f is not None, "verdict": L.report.get("verdict")}
    if f is None:
        return out
    pf = L.checks.get("preflight", {})
    play = L.checks.get("play", {})
    from . import engine
    homes = engine.piece_homes_of(f)
    n, cand = len(homes), sum(1 for _h, c in homes if c)
    out.update(pieces=n, candidates=cand, bricks=len(f.get("bricks", [])),
               lower_third=pf.get("lower_third_share", round(engine.low_share(homes), 3)),
               won48=play.get("greedy_won"), ramp=play.get("ramp_per_48"), scene=L.report.get("scene") or f.get("scene"))
    key = stage_of(lid, src)
    if key in stagecheck.MEAN_BANDS and out["ramp"] is not None:
        lo, hi = stagecheck.MEAN_BANDS[key]
        out["band"] = [lo, hi]
        out["margin"] = round(min(out["ramp"] - lo, hi - out["ramp"]), 2)      # negative: outside the stage's mean band
    return out


# -------------------------------------------------------------------------------------------------- status columns
def level_inputs(lid, scene):
    """The source files a build reads for a level."""
    fs = [paths.LAYOUTS / f"{lid}.py"]
    if scene:
        fs.append(paths.SCENES / f"{scene}.json")
        try:
            r = json.loads((paths.SCENES / f"{scene}.json").read_text(encoding="utf-8"))
            if r.get("source", {}).get("kind") == "painting":
                fs.append(paths.PAINTERS / f"{r['source']['painter']}.py")
        except (OSError, ValueError):
            pass
    return [f for f in fs if f.exists()]


def build_state(lid, scene):
    """('fresh'|'stale'|'none', note). The build stamp compared with the inputs now; with no stamp (a fresh checkout), the
    committed report against the commits of the layout, recipe and painter."""
    try:
        from . import cache
        from .build import load_layout
        from .scene import load_recipe
        meta = load_layout(lid).LEVEL
        key = cache.build_key(lid, meta, load_recipe(meta["scene"]))
        st = cache.read_stamp(f"build-{lid}")
        if st:
            return ("fresh" if st["key"] == key else "stale"), f"stamp {st['time']}"
    except Exception as e:                                   # no game textures, no built mfcheck: the stamp cannot be judged
        note = f"stamp not checkable ({type(e).__name__})"
    else:
        note = "no stamp"
    rep = paths.REPORT / f"{lid}.json"
    if not rep.exists():
        return "none", note + "; no report"
    t_rep, d_rep = commit_time(rep)
    t_in, d_in = commit_time(*level_inputs(lid, scene))
    if d_in or d_rep or t_in > t_rep:
        return "stale", note + "; inputs newer than the committed report (git)"
    return "fresh", note + "; report newer than its inputs (git)"


def convert_state(lid, scene):
    """('fresh'|'stale'|'missing', note): the runtime scene recipe against the commits of the pipeline recipe, painter and
    level file. (Convert writes no stamp; the commit order is what shows a recipe changed after its last convert.)"""
    if not scene:
        return "missing", "no scene"
    out = CORE_LEVELS / "scenes" / f"{scene}.json"
    if not out.exists():
        return "missing", "no runtime scene recipe"
    ins = [p for p in level_inputs(lid, scene) if p.suffix == ".json" or p.parent == paths.PAINTERS] + [paths.JSON_OUT / f"{lid}.json"]
    t_out, d_out = commit_time(out)
    t_in, d_in = commit_time(*[p for p in ins if p.exists()])
    if d_in or d_out or t_in > t_out:
        return "stale", "pipeline recipe, painter or level file newer than the runtime scene (git)"
    return "fresh", "runtime scene not older than its sources (git)"


def last_review(key):
    """The newest commit that touched a supervisor record of this stage, or None."""
    glob_ = f":(glob){REL}/supervisor/*-{key}-round-*.md"
    out = git("log", "-1", "--format=%H", "--", glob_)
    return out.strip() if out and out.strip() else None


def changed_since(rev, lid, scene):
    """The level's source and output files that differ between `rev` and the working tree."""
    fs = [rel(p) for p in level_inputs(lid, scene)] + [f"{REL}/json/{lid}.json", f"{REL}/composites/{lid}.png"]
    out = git("diff", "--name-only", rev, "--", *fs)
    names = [n for n in (out or "").split("\n") if n]
    untracked = git("ls-files", "--others", "--exclude-standard", "--", *fs) or ""
    return sorted(set(names) | {n for n in untracked.split("\n") if n})


def precheck_verdicts(ids, rev=None):
    """{id: (worst, 'n FAIL, m WARN')} from the precheck, run silently."""
    res, _w = precheck.precheck(ids, rev=rev, print_=lambda _t: None)
    out = {}
    for lid in ids:
        rs = [r for r in res if r.level == lid]
        nf, nw = sum(r.status == precheck.FAIL for r in rs), sum(r.status == precheck.WARN for r in rs)
        out[lid] = (precheck.worst(rs), f"{nf} FAIL, {nw} WARN")
    return out


def status(ids, key_hint=None, with_precheck=True):
    """Status rows for the ids (see mfl.py status)."""
    src = precheck.Source()
    verdicts = precheck_verdicts(ids) if with_precheck else {}
    reviews = {}
    rows = []
    for lid in ids:
        f = facts(src, lid)
        scene = f.get("scene") or level_meta(lid, src)[2]
        key = stage_of(lid, src)
        if key not in reviews:
            reviews[key] = last_review(key) if key else None
        b, bnote = build_state(lid, scene)
        c, cnote = convert_state(lid, scene)
        rv = reviews[key]
        ch = changed_since(rv, lid, scene) if rv else None
        rows.append({**f, "stage_key": key, "build": b, "build_note": bnote, "convert": c, "convert_note": cnote,
                     "precheck": verdicts.get(lid, (None, ""))[0], "precheck_note": verdicts.get(lid, (None, ""))[1],
                     "last_review": rev_short(rv) if rv else None,
                     "changed_since_review": ch})
    return rows


def status_text(rows):
    head = (f"{'code':4} {'id':8} {'verdict':8} {'build':6} {'pieces':>6} {'cand':>4} {'low3':>5} {'won/48':>6} {'ramp':>5} "
            f"{'margin':>6} {'convert':8} {'precheck':8} changed since review")
    out = [head]
    for r in rows:
        if not r["has_file"]:
            out.append(f"{r['code']:4} {r['id']:8} no level file yet (build has not passed)")
            continue
        ch = r["changed_since_review"]
        chs = "never reviewed" if ch is None else ("none" if not ch else f"{len(ch)} file(s): " + ", ".join(pathlib.PurePosixPath(c).name for c in ch))
        mg = "-" if r.get("margin") is None else f"{r['margin']:+.1f}"
        out.append(f"{r['code']:4} {r['id']:8} {str(r['verdict']):8} {r['build']:6} {r['pieces']:>6} {r['candidates']:>4} "
                   f"{round(r['lower_third'] * 100):>4}% {str(r['won48']):>6} {str(r['ramp']):>5} {mg:>6} {r['convert']:8} "
                   f"{str(r['precheck']):8} {chs}")
    return "\n".join(out)


# -------------------------------------------------------------------------------------------------- piece compare
def items(level):
    """Each piece of a level file: kind, home (x, y), a shape signature, whether it may be orange, and sample points."""
    out = []
    for p in level.get("pegs", []):
        out.append(dict(kind="peg", x=p["x"], y=p["y"], sig=(round(p.get("r", 10.0), 1),), orange=bool(p.get("canBeOrange", False)),
                        pts=[(p["x"], p["y"])], r=p.get("r", 10.0), mover="move" in p))
    for b in level.get("bricks", []):
        pcs = precheck.pieces_of({"pegs": [], "bricks": [b]})[0]
        if b["kind"] == "line":
            x, y = (b["x1"] + b["x2"]) / 2, (b["y1"] + b["y2"]) / 2
            sig = (round(math.hypot(b["x2"] - b["x1"], b["y2"] - b["y1"]), 1), round(math.degrees(math.atan2(b["y2"] - b["y1"], b["x2"] - b["x1"])) % 180, 1))
        else:
            a = math.radians(b["start"] + b["sweep"] / 2)
            x, y = b["x"] + b["r"] * math.cos(a), b["y"] + b["r"] * math.sin(a)
            sig = (round(b["r"], 1), round(b["sweep"], 1))
        out.append(dict(kind=b["kind"], x=x, y=y, sig=sig, orange=bool(b.get("canBeOrange", False)), pts=pcs["pts"], r=pcs["r"], mover=False))
    return out


def diff_pieces(old, new, same_tol=0.6, move_tol=60.0):
    """Compare two level files piece by piece. A piece of the same kind with the same shape within `same_tol` units is the
    same piece; the rest are paired by nearest distance (within `move_tol`) as moved or reshaped; what is left over was
    removed (in `old`) or added (in `new`). Returns a dict of lists of items; pairs are (old_item, new_item)."""
    a, b = items(old), items(new)
    ua, ub = set(range(len(a))), set(range(len(b)))
    same, moved = [], []

    def pairs(tol, exact):
        cand = []
        for i in ua:
            for j in ub:
                if a[i]["kind"] != b[j]["kind"]:
                    continue
                d = math.hypot(a[i]["x"] - b[j]["x"], a[i]["y"] - b[j]["y"])
                if d <= tol and (not exact or all(abs(p - q) <= 0.2 for p, q in zip(a[i]["sig"], b[j]["sig"]))):
                    cand.append((d, i, j))
        cand.sort()
        got = []
        for d, i, j in cand:
            if i in ua and j in ub:
                ua.discard(i)
                ub.discard(j)
                got.append((a[i], b[j], d))
        return got
    same = pairs(same_tol, True)
    moved = pairs(move_tol, False)
    gained = [(p, q) for p, q, _d in same + moved if q["orange"] and not p["orange"]]
    lost = [(p, q) for p, q, _d in same + moved if p["orange"] and not q["orange"]]
    return {"same": same, "moved": moved, "added": [b[j] for j in sorted(ub)], "removed": [a[i] for i in sorted(ua)],
            "gained": gained, "lost": lost, "old": a, "new": b}


def diff_summary(lid, fo, fn, d, rev):
    """The text summary of a diff: pieces, candidates, difficulty before and after."""
    ca, cn = fo.get("candidates", 0), fn.get("candidates", 0)
    ga = len(d["gained"]) + sum(1 for q in d["added"] if q["orange"])
    lo = len(d["lost"]) + sum(1 for p in d["removed"] if p["orange"])

    def pct(f):
        return "-" if f.get("lower_third") is None else f"{round(f['lower_third'] * 100)}%"

    def w(f):
        return "-" if f.get("ramp") is None else f"{f['ramp']} ({f.get('won48')}/48 rule games)"
    lines = [f"{lid} {fn.get('name', '')} ({fn.get('code', '?')}): {rev_short(rev)} -> now",
             f"pieces {fo.get('pieces', 0)} -> {fn.get('pieces', 0)}: {len(d['added'])} added, {len(d['removed'])} removed, "
             f"{len(d['moved'])} moved or reshaped, {len(d['same'])} unchanged",
             f"candidates {ca} -> {cn}: {ga} gained, {lo} lost; lower third {pct(fo)} -> {pct(fn)}",
             f"difficulty (held-out wins per 48) {w(fo)} -> {w(fn)}; verdict {fo.get('verdict')} -> {fn.get('verdict')}"]
    if fn.get("band"):
        lines.append(f"stage band {fn['band'][0]} to {fn['band'][1]}; margin {fo.get('margin', '-')} -> {fn.get('margin', '-')}")
    return lines


# ------------------------------------------------------------------------------------------------- engine render
def render_dll():
    found = sorted(glob.glob(str(RENDER_DIR / "bin" / "Release" / "*" / "Tsukimichi.MoonfallRender.dll")))
    return pathlib.Path(found[0]) if found else None


def ensure_render_built():
    d = render_dll()
    if d:
        return d
    subprocess.run(["dotnet", "build", str(RENDER_DIR), "-c", "Release", "--nologo", "-v", "q"], check=True)
    return render_dll()


def _sha(p):
    return hashlib.sha1(pathlib.Path(p).read_bytes()).hexdigest() if pathlib.Path(p).exists() else "-"


def render_key(lid, scene, size, moment, dll):
    """What a render of this level depends on: the runtime level and scene files, the shipped art for the scene, the
    renderer's engine DLLs and its arguments. Cheaper than starting the renderer to find a cache hit."""
    parts = [lid, size, moment, _sha(CORE_LEVELS / f"{lid}.json"), _sha(CORE_LEVELS / "scenes" / f"{scene}.json")]
    parts += [f"{p.name}:{_sha(p)}" for p in sorted(PLUGIN_SCENES.glob(f"{scene}*"))] if scene else []
    parts += [_sha(dll.parent / "Tsukimichi.Core.dll"), _sha(dll.parent / "Tsukimichi.dll")]
    return hashlib.sha1("\0".join(parts).encode()).hexdigest()


def engine_render(lid, scene, out, size="1280x800", moment="start", cache_dir=None, stamps=None):
    """Render with MoonfallRender (--cache). `stamps` is a dict {out path: key} kept by the pack: a render whose key is
    unchanged and whose PNG exists is not even started. Returns 'skipped', 'rendered' or an error string."""
    dll = ensure_render_built()
    if not dll:
        return "MoonfallRender is not built"
    out = pathlib.Path(out)
    key = render_key(lid, scene, size, moment, dll)
    if stamps is not None and out.exists() and stamps.get(out.name) == key:
        return "skipped"
    env = dict(os.environ)
    env.setdefault("TSUKIMICHI_GAME_PATH", GAME_PATH)
    cache_dir = pathlib.Path(cache_dir or paths.BUILD / "cache" / "render")
    cache_dir.mkdir(parents=True, exist_ok=True)
    r = subprocess.run(["dotnet", str(dll), str(out), "--level", lid, "--size", size, "--moment", moment, "--cache", str(cache_dir)],
                       capture_output=True, text=True, env=env, timeout=900)
    if r.returncode != 0 or not out.exists():
        tail = ((r.stderr or r.stdout).strip().splitlines() or [""])[-1]
        return f"render failed ({r.returncode}): {tail}"
    if stamps is not None:
        stamps[out.name] = key
    return "rendered"
