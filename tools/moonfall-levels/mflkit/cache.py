"""Result caches for the level tools: a build that passed on unchanged inputs is skipped, and a greedy-play run on an
unchanged level file is read back instead of replayed. Both live in docs/design/v9/levels/build/cache (gitignored).
Nothing here changes what a check decides: a cache hit returns exactly what the same inputs produced before, and
`--force` (build, stage) or the environment variable MFL_NO_CACHE=1 recomputes."""
import hashlib
import json
import os
import pathlib
import time

from .paths import BUILD, CACHE, MFCHECK, PAINTERS, RICH, RICH2, SCENES, V9, LAYOUTS, TOOL, cache_name

DIR = BUILD / "cache"
_MEMO = {}


def disabled():
    return os.environ.get("MFL_NO_CACHE") == "1"


def file_hash(path):
    """Content hash of one file (memoised per process); '-' when it does not exist."""
    p = pathlib.Path(path)
    k = (str(p), p.stat().st_mtime_ns, p.stat().st_size) if p.exists() else (str(p), None, None)
    if k not in _MEMO:
        _MEMO[k] = hashlib.sha1(p.read_bytes()).hexdigest() if p.exists() else "-"
    return _MEMO[k]


def digest(*parts):
    h = hashlib.sha1()
    for part in parts:
        h.update(str(part).encode("utf-8"))
        h.update(b"\0")
    return h.hexdigest()


def kit_hash():
    """The pipeline's own code: mflkit/*.py, mfl.py, and the approved design kit it imports."""
    files = sorted((TOOL / "mflkit").glob("*.py")) + [TOOL / "mfl.py"]
    for d in (V9 / "src", RICH / "src", RICH2 / "src"):
        files += sorted(d.glob("*.py"))
    return digest(*[f"{f.name}:{file_hash(f)}" for f in files])


def mfcheck_hash():
    """The built mfcheck and the engine it carries (mfcheck.dll next to Tsukimichi.Core.dll)."""
    d = MFCHECK.parent
    return digest(*[file_hash(d / n) for n in ("mfcheck.dll", "Tsukimichi.Core.dll")])


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


def read_stamp(name):
    try:
        return json.loads((DIR / f"{name}.json").read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def write_stamp(name, key):
    DIR.mkdir(parents=True, exist_ok=True)
    _atomic(DIR / f"{name}.json", json.dumps({"key": key, "time": time.strftime("%Y-%m-%d %H:%M:%S")}))


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
