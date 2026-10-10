"""The slow commands, made incremental and parallel: `convert --check`, `build --jobs N` and `level <id>`.

Nothing here changes what a check decides. A cached result is the figures an identical run produced (same level file,
same recipes, same pipeline code, same MoonfallRender sources); `--force` (or MFL_NO_CACHE=1) recomputes. Only passes are
cached, so a failing level is always looked at again.
"""
import hashlib
import json
import os
import subprocess
import time
from concurrent.futures import ProcessPoolExecutor

from . import cache, convert, paths
from .paths import BUILD, JSON_OUT, REPO, SCENES

# ------------------------------------------------------------------------------------------ MoonfallRender, built once
RENDER_SOURCES = (REPO / "Tsukimichi.Core", REPO / "Tsukimichi", convert.RENDER)
_SKIP_DIRS = {"bin", "obj", ".vs"}


def render_signature():
    """What MoonfallRender is made of: every file (path, size, modified time) of the projects it references and its own,
    without the build folders. The converter writes the runtime scenes into Tsukimichi.Core, so this moves after a convert."""
    h = hashlib.sha1()
    for root in RENDER_SOURCES:
        for dp, dns, fns in os.walk(root):
            dns[:] = sorted(d for d in dns if d not in _SKIP_DIRS)
            for fn in sorted(fns):
                p = os.path.join(dp, fn)
                st = os.stat(p)
                h.update(f"{os.path.relpath(p, REPO)}|{st.st_size}|{st.st_mtime_ns}\n".encode())
    return h.hexdigest()


def ensure_render(log=print):
    """`dotnet build` of MoonfallRender, only when its sources changed since the last build this tool made (and the dll is there)."""
    sig = render_signature()
    st = cache.read_stamp("render-build", sig)
    if st and convert.RENDER_DLL.exists() and not cache.disabled():
        return False
    t = time.time()
    subprocess.run(["dotnet", "build", str(convert.RENDER), "-c", "Release", "--nologo", "-v", "q"], check=True)
    cache.write_stamp("render-build", render_signature())
    log(f"MoonfallRender built in {time.time() - t:.0f} s")
    return True


# ------------------------------------------------------------------------------------------ convert --check
def _scene_name(lid):
    return json.loads((JSON_OUT / f"{lid}.json").read_text(encoding="utf-8"))["scene"]


def check_key(lid, rsig):
    """Everything one level's converter gate reads: its level file, its scene recipe and the runtime recipe the converter
    wrote from it, our painting or the game texture, the chrome art, the pipeline's code, and MoonfallRender's sources."""
    from .scene import load_recipe
    scene = _scene_name(lid)
    recipe = load_recipe(scene)
    src = recipe["source"]
    parts = [f"level:{cache.file_hash(JSON_OUT / (lid + '.json'))}", f"recipe:{cache.file_hash(SCENES / (scene + '.json'))}",
             f"runtime:{cache.file_hash(convert.RUNTIME_SCENES / (scene + '.json'))}", f"kit:{cache.kit_hash()}", f"render:{rsig}",
             f"tiers:{convert.GATE}/{convert.GATE_MAX}/{convert.GATE_1X}"]
    if src["kind"] == "painting":
        parts.append(f"painter:{cache.file_hash(paths.PAINTERS / (src['painter'] + '.py'))}")
    else:
        parts.append(f"texture:{cache.file_hash(paths.CACHE / paths.cache_name(src['texture']))}")
    for sub in ("uld", "portraits", "font"):
        d = paths.CACHE / sub
        if d.exists():
            parts.append(sub + ":" + cache.digest(*[f"{f.name}:{f.stat().st_size}" for f in sorted(d.glob("*.png"))]))
    return cache.digest(*parts)


def _check_file(lid, key):
    """A level's converter-gate stamp in the shared cache folder; its name carries its key, as cache.write_stamp's do,
    so worktrees that share the folder never overwrite each other's."""
    return cache.DIR / f"check-{lid}-{key[:24]}.json"


def _read_check(lid, key):
    """The stored figures of the last PASS on exactly this key, or None."""
    try:
        d = json.loads(_check_file(lid, key).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    return d if d.get("key") == key else None


def _write_check(lid, key, tiers):
    cache.DIR.mkdir(parents=True, exist_ok=True)
    cache._atomic(_check_file(lid, key), json.dumps({"key": key, "time": time.strftime("%Y-%m-%d %H:%M:%S"), "tiers": tiers}))


def check_one(lid, scratch):
    """One level's gate at both tiers (2x first, as the gate always ran them): [[S, figs, worst, kept, drops], ...].
    The 2x reference is computed once and serves the 1x tier too (convert.reference memoises it)."""
    out = []
    for S in (2, 1):
        figs, worst = convert.check(lid, S, scratch)
        kept = convert.LAST_BUILD.get(lid, {})
        out.append([S, list(figs), list(worst), kept, convert.dropped(lid, kept)])
    return out


def gate_proof(level_id, log=None):
    """The gate proves itself on one level: the pipeline's scene against itself passes, against its clipped jewel it fails."""
    from .dress import clipped_gamut, dress
    from .scene import load_recipe
    level = convert._level(level_id)
    recipe = load_recipe(level["scene"])
    ref = convert.reference(level_id, 1)
    with clipped_gamut():
        clipped, _ = dress(recipe, convert.graded(recipe, 1), level, 1)
    same, other = convert.compare(ref, ref), convert.compare(ref, clipped)
    proof = same[0] == 0 and not convert.passes(other, 2)
    line = (f"gate proof on {level_id}: itself max {same[0]:.4f}; the clipped jewel max {other[0]:.4f} p99.9 {other[1]:.4f}: "
            f"{'ok' if proof else 'BAD'}")
    if log:
        log(line)
    return proof, line


def gate(ids, scratch, jobs=None, force=False, log=print):
    """convert.gate, incrementally: levels whose inputs are unchanged since their last PASS replay their stored figures;
    the rest are checked in parallel (one process per level). Returns (ok, rows) like convert.gate, plus the counts."""
    force = force or cache.disabled()
    rsig = render_signature()
    keys = {lid: check_key(lid, rsig) for lid in ids}
    results, todo = {}, []
    for lid in ids:
        st = None if force else _read_check(lid, keys[lid])
        if st:
            results[lid] = (st["tiers"], st["time"])
        else:
            todo.append(lid)
    ok = True
    if todo:
        if ensure_render(log):                       # a build can touch what the signature reads: key again
            rsig = render_signature()
            keys = {lid: check_key(lid, rsig) for lid in ids}
        jobs = max(1, min(jobs or 4, len(todo)))
        if jobs == 1:
            proof = gate_proof(todo[0])
            fresh = {lid: check_one(lid, scratch) for lid in todo}
        else:
            with ProcessPoolExecutor(jobs) as ex:
                pf = ex.submit(gate_proof, todo[0])           # the proof runs beside the levels
                futs = {lid: ex.submit(check_one, lid, scratch) for lid in todo}
                fresh = {lid: futs[lid].result() for lid in todo}
                proof = pf.result()
        log(proof[1])
        ok &= proof[0]
        for lid in todo:
            results[lid] = (fresh[lid], None)
            if all(convert.passes(tuple(t[1]), t[0]) and not t[4] for t in fresh[lid]):
                _write_check(lid, keys[lid], fresh[lid])
    else:
        log("gate proof: unchanged (the pipeline's code has not changed since each level's last PASS)")
    rows = []
    for lid in ids:
        tiers, when = results[lid]
        for (S, figs, worst, kept, drops) in tiers:
            figs, worst = tuple(figs), tuple(worst)
            passed = convert.passes(figs, S) and not drops
            ok &= passed
            rows.append((lid, S, figs, worst, passed))
            log(f"{lid} {S}x: max {figs[0]:.4f} p99.9 {figs[1]:.4f} p99 {figs[2]:.4f} mean {figs[3]:.4f}, worst at "
                f"({worst[0]:g}, {worst[1]:g}); kept {kept}{'; dropped ' + ', '.join(drops) if drops else ''}"
                f"{'' if passed else '  FAIL'}" + (f"  [unchanged since last PASS ({when})]" if when else ""))
    return ok, rows


# ------------------------------------------------------------------------------------------ build --jobs
def _build_one(lid, keep2x, keep_going):
    """One level's build in a worker process; returns (report, its log lines)."""
    from .build import build
    lines = []
    rep = build(lid, keep2x=keep2x, log=lines.append, pool=None, keep_going=keep_going)
    return rep, lines


def build_many(ids, keep2x, jobs, keys, keep_going=False, log=print):
    """Builds the levels, `jobs` at a time (each level writes its own files, so they do not meet). Returns (refused ids,
    {id: report}). A level stops after its cheap phase on a fault unless `keep_going` (build(..., keep_going)). The log of
    each level is printed whole when it finishes. A PASS writes the level's stamp (`keys[lid]`)."""
    bad, reps = [], {}
    if jobs <= 1 or len(ids) == 1:
        from .build import build
        from .composite import start_pool, stop_pool
        pool = start_pool()
        try:
            for lid in ids:
                rep = reps[lid] = build(lid, keep2x=lid in keep2x, log=log, pool=pool, keep_going=keep_going)
                _stamp(lid, rep, keys, bad)
        finally:
            stop_pool()
        return bad, reps
    with ProcessPoolExecutor(min(jobs, len(ids))) as ex:
        futs = {lid: ex.submit(_build_one, lid, lid in keep2x, keep_going) for lid in ids}
        for lid in ids:                                  # in the order asked, so the log reads the same every time
            rep, lines = futs[lid].result()
            reps[lid] = rep
            for ln in lines:
                log(ln)
            _stamp(lid, rep, keys, bad)
    return bad, reps


def _stamp(lid, rep, keys, bad):
    if rep["verdict"] != "PASS":
        bad.append(lid)
    else:
        cache.write_stamp(f"build-{lid}", keys[lid])


# ------------------------------------------------------------------------------------------ level <id>
def level(lid, build_fn, force=False, log=print):
    """build -> convert -> convert --check -> the engine render of the level, then one verdict line. Exit code 0 on PASS.
    `build_fn(argv)` is mfl's build command (with its self-test and stamps)."""
    t0 = time.time()
    steps = []

    def step(name, fn):
        t = time.time()
        res = fn()
        steps.append((name, res, time.time() - t))
        return res

    def do_build():
        return "PASS" if build_fn([lid] + (["--force"] if force else [])) == 0 else "REFUSED"

    r = step("build", do_build)
    verdict, why = "PASS", ""
    if r == "REFUSED":
        verdict, why = "REFUSED", f"build refused ({paths.REPORT / (lid + '.json')} lists the fails)"
    else:
        step("convert", lambda: convert.write(lid))
        scratch = BUILD / "convert"
        scratch.mkdir(parents=True, exist_ok=True)
        ok, rows = step("check", lambda: gate([lid], scratch, jobs=1, force=force, log=log))
        if not ok:
            verdict, why = "FAIL", "converter gate failed (diffs in " + str(scratch) + ")"
        else:
            out = BUILD / "render" / f"{lid}-engine-1280.png"
            out.parent.mkdir(parents=True, exist_ok=True)
            rr = step("render", lambda: subprocess.run(
                ["dotnet", str(convert.RENDER_DLL), str(out), "--level", lid, "--cache", str(BUILD / "render-cache")],
                capture_output=True, text=True, cwd=REPO))
            if rr.returncode != 0:
                verdict, why = "FAIL", f"render failed: {(rr.stdout + rr.stderr).strip()[-200:]}"
            else:
                why = f"render {out}"
    log(f"[{lid}] {verdict} in {time.time() - t0:.0f} s ("
        + ", ".join(f"{n} {t:.0f} s" for n, _r, t in steps) + (f"); {why}" if why else ")"))
    return 0 if verdict == "PASS" else 1


# ------------------------------------------------------------------------------------------ self-test
def selftest(verbose=True):
    """The speed-ups give the answers the slow ways gave: the render pool's picture equals the serial one, the memoised
    layout work equals the original and cannot be spoiled by a caller, a file written again unchanged keeps its modified
    time, the signature and the stamps move exactly when an input does."""
    import concurrent.futures as cf
    import pathlib
    import tempfile
    import numpy as np
    from . import build, composite, memo
    import framecheck
    import rich_lib as RL
    cases = []
    lv = json.loads((paths.RICH / "levels" / "base-p2.json").read_text(encoding="utf-8"))
    # 1. the memo: the same numbers as the originals, and a caller's edit does not reach the next caller
    W, H = 800, 600
    a = memo.layout_mask(lv, W, H, 1.0, grow=18.0)
    cases.append(("memo: the layout mask equals board's own", bool(np.array_equal(a, memo._ORIG_MASK(lv, W, H, 1.0, grow=18.0))), True))
    a[:] = 7
    cases.append(("memo: editing a returned mask leaves the next one whole",
                  float(memo.layout_mask(lv, W, H, 1.0, grow=18.0).max()) <= 1.0, True))
    d = framecheck.piece_distance(lv)
    cases.append(("memo: the piece distance equals framecheck's own", bool(np.array_equal(d, memo._ORIG_DIST(lv, 1.0))), True))
    other = json.loads(json.dumps(lv))
    other["pegs"][0]["x"] += 25
    cases.append(("memo: another level is not served the first one's picture", not np.array_equal(framecheck.piece_distance(other), d), True))
    # 2. the render pool: the same picture as the serial render
    n = len(lv["pegs"]) + len(lv["bricks"])
    cols = ["orange" if i % 7 == 0 else "blue" for i in range(n)]
    scene = np.full((1200, 1600, 3), 0.18, np.float32)
    scene[:, 800:] = 0.30
    recipe = {"name": "selftest", "veil": 0.40}
    lv2 = dict(lv, id="selftest-pool", name="Pool")
    serial = composite.render_async(None, lv2, recipe, scene, cols, S=2, stage=1, number=1).result()
    with cf.ProcessPoolExecutor(2) as ex:
        pooled = composite.render_async(ex, lv2, recipe, scene, cols, S=2, stage=1, number=1).result()
    cases.append(("render pool: a worker's composite equals the serial one, byte for byte", bool(np.array_equal(serial, pooled)), True))
    with tempfile.TemporaryDirectory() as td:
        td = pathlib.Path(td)
        # 3. files: a save through a temporary name equals the direct save; unchanged bytes keep their time
        px = scene[:60, :80]
        build.save_atomic(px, td / "a.png")
        RL.save_rgb(px, td / "b.png")
        cases.append(("save_atomic: the same bytes as save_rgb", (td / "a.png").read_bytes() == (td / "b.png").read_bytes(), True))
        f, t = td / "keep.txt", td / "keep.txt.tmp"
        f.write_text("same")
        old = f.stat().st_mtime_ns
        t.write_text("same")
        convert._keep_if_same(t, f)
        cases.append(("converter: a file written again unchanged keeps its modified time", f.stat().st_mtime_ns == old and not t.exists(), True))
        t.write_text("different")
        convert._keep_if_same(t, f)
        cases.append(("converter: a changed file is replaced", f.read_text() == "different", True))
        # 4. the signature and the stamps
        global RENDER_SOURCES
        src = td / "src"
        src.mkdir()
        (src / "A.cs").write_text("x")
        saved, RENDER_SOURCES = RENDER_SOURCES, (src,)
        try:
            s1 = render_signature()
            same = render_signature() == s1
            (src / "obj").mkdir()
            (src / "obj" / "junk.dll").write_text("build output")
            ignores_build = render_signature() == s1
            (src / "A.cs").write_text("xy")
            moves = render_signature() != s1
        finally:
            RENDER_SOURCES = saved
        cases.append(("signature: stable, blind to build folders, moves when a source does", same and ignores_build and moves, True))
        saved_dir, cache.DIR = cache.DIR, td / "cache"
        try:
            _write_check("selftest", "k1", [[2, [0, 0, 0, 0], [0, 0], {}, []]])
            hit = _read_check("selftest", "k1")
            cases.append(("stamp: a written stamp reads back with its key and figures",
                          bool(hit) and hit["key"] == "k1" and hit["tiers"][0][0] == 2, True))
            cases.append(("stamp: another key finds nothing", _read_check("selftest", "k2") is None, True))
            cases.append(("stamp: a missing stamp is a miss", _read_check("absent", "k1") is None, True))
        finally:
            cache.DIR = saved_dir
    ok = True
    for name, got, want in cases:
        ok &= got == want
        if verbose:
            print(f"  {'ok ' if got == want else 'BAD'} speed-ups: {name}: {got}, expected {want}")
    return ok
