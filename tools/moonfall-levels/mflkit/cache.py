"""Result caches for the level tools: a build that passed on unchanged inputs is skipped, and a greedy-play run on an
unchanged level file is read back instead of replayed. They live outside the worktree, in a content-keyed folder every
worktree shares (paths.SHARED: %LOCALAPPDATA%/tsukimichi-moonfall-cache, or the environment variable MOONFALL_CACHE).
Nothing here changes what a check decides: a cache hit returns exactly what the same inputs produced before, and
`--force` (build, stage) or the environment variable MFL_NO_CACHE=1 recomputes."""
import functools
import hashlib
import json
import os
import pathlib
import time

from .paths import CACHE, MFCHECK, PAINTERS, REPO, RICH, RICH2, SCENES, SHARED, V9, LAYOUTS, TOOL, cache_name

DIR = SHARED / "levels"
_MEMO = {}
TEXT_SUFFIXES = {".py", ".cs", ".csproj", ".props", ".json", ".txt", ".md"}


def disabled():
    return os.environ.get("MFL_NO_CACHE") == "1"


def file_hash(path):
    """Content hash of one file (memoised per process); '-' when it does not exist. Text files hash with LF line ends, so a
    checkout with CRLF (git's autocrlf) and one without give the same key in every worktree."""
    p = pathlib.Path(path)
    k = (str(p), p.stat().st_mtime_ns, p.stat().st_size) if p.exists() else (str(p), None, None)
    if k not in _MEMO:
        if not p.exists():
            _MEMO[k] = "-"
        else:
            data = p.read_bytes()
            if p.suffix.lower() in TEXT_SUFFIXES:
                data = data.replace(b"\r\n", b"\n")
            _MEMO[k] = hashlib.sha1(data).hexdigest()
    return _MEMO[k]


def digest(*parts):
    h = hashlib.sha1()
    for part in parts:
        h.update(str(part).encode("utf-8"))
        h.update(b"\0")
    return h.hexdigest()


def kit_files():
    """The pipeline's own code: mflkit/*.py, mfl.py, and the approved design kit it imports."""
    files = sorted((TOOL / "mflkit").glob("*.py")) + [TOOL / "mfl.py"]
    for d in (V9 / "src", RICH / "src", RICH2 / "src"):
        files += sorted(d.glob("*.py"))
    return files


def kit_hash():
    return digest(*[f"{f.name}:{file_hash(f)}" for f in kit_files()])


@functools.lru_cache(maxsize=1)
def core_sources():
    """What mfcheck's engine is built from: mfcheck's own source and project, Tsukimichi.Core's code and data (not the
    shipped levels and scene recipes under Moonfall/Levels, which mfcheck reads from the file it is given), and the solution's
    props. Hashing these, not the built DLLs, keeps the key the same in every worktree (a build embeds its own paths)."""
    files = [TOOL / "dotnet" / "mfcheck" / n for n in ("Program.cs", "mfcheck.csproj")]
    files += [p for p in REPO.glob("Directory.*.props")]
    core = REPO / "Tsukimichi.Core"
    for root, dirs, names in os.walk(core):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
        rel = pathlib.Path(root).relative_to(core).as_posix()
        if rel == "Moonfall/Levels" or rel.startswith("Moonfall/Levels/"):
            continue
        files += [pathlib.Path(root) / n for n in names if n.endswith((".cs", ".csproj", ".json", ".props"))]
    return tuple(sorted(files))


@functools.lru_cache(maxsize=1)
def mfcheck_hash():
    """The engine mfcheck runs, by its sources (see core_sources); `engine.ensure_built` rebuilds when they are newer."""
    return digest("mfcheck", *[f"{f.name}:{file_hash(f)}" for f in core_sources()])


def tools_hash():
    """The C# the pipeline runs: the engine mfcheck is built from (mfcheck_hash) and the renderer's sources."""
    files = [TOOL / "dotnet" / "mfcheck" / n for n in ("Program.cs", "mfcheck.csproj")]
    render = REPO / "tools" / "Tsukimichi.MoonfallRender"
    files += sorted(render.glob("*.cs")) + [render / "Tsukimichi.MoonfallRender.csproj"]
    return digest(mfcheck_hash(), *[f"{f.name}:{file_hash(f)}" for f in files])


def selftest_key(fixtures):
    """What the self-test reads: the kit's code, the C# tools, and its fixtures (files, by content)."""
    return digest("selftest", kit_hash(), tools_hash(), *[f"{f.name}:{file_hash(f)}" for f in fixtures])


def build_key(level_id, meta, recipe):
    """Everything a level's build reads: its layout, its scene recipe, the painting or game texture the scene is made
    from (and the chrome art the composites use), the pipeline's code, and the built mfcheck."""
    src = recipe["source"]
    parts = [f"layout:{file_hash(LAYOUTS / (level_id + '.py'))}", f"recipe:{file_hash(SCENES / (meta['scene'] + '.json'))}",
             f"kit:{kit_hash()}", f"mfcheck:{mfcheck_hash()}"]
    if src["kind"] == "painting":
        parts.append(f"painter:{file_hash(PAINTERS / (src['painter'] + '.py'))}")
    else:
        parts.append(f"texture:{file_hash(CACHE / cache_name(src['texture']))}")
    for sub in ("uld", "portraits", "font"):
        d = CACHE / sub
        if d.exists():
            parts.append(sub + ":" + digest(*[f"{f.name}:{f.stat().st_size}" for f in sorted(d.glob("*.png"))]))
    return digest(*parts)


def read_stamp(name, key):
    """The stamp `write_stamp(name, key)` left (its time), or None. A stamp's file name carries its key, so worktrees
    that share the folder never overwrite each other's stamps."""
    try:
        d = json.loads((DIR / f"{name}-{key[:24]}.json").read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    return d if d.get("key") == key else None


def write_stamp(name, key):
    DIR.mkdir(parents=True, exist_ok=True)
    _atomic(DIR / f"{name}-{key[:24]}.json", json.dumps({"key": key, "time": time.strftime("%Y-%m-%d %H:%M:%S")}))


def _atomic(path, text):
    tmp = path.with_name(f"{path.name}.{os.getpid()}.{time.time_ns()}.tmp")
    tmp.write_text(text, encoding="utf-8")
    os.replace(tmp, path)


def play_get(path, number, games, first, oranges=None):
    """(key, cached stdout of `mfcheck play` for this level file, number and seed range, or None)."""
    if disabled():
        return None, None
    key = digest("play", file_hash(path), number, games, first, mfcheck_hash(), *(() if oranges is None else (f"oranges={oranges}",)))
    try:
        return key, (DIR / "play" / f"{key}.txt").read_text(encoding="utf-8")
    except OSError:
        return key, None


def play_put(key, out):
    if key is None:
        return
    (DIR / "play").mkdir(parents=True, exist_ok=True)
    _atomic(DIR / "play" / f"{key}.txt", out)
