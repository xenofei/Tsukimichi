"""The shipped engine, through mfcheck (dotnet/mfcheck): the loader's verdict, the engine's colour pick, the first-shot
sweep and the 48 seeded greedy games. Nothing here re-implements the engine."""
import json
import re
import subprocess

from .paths import DOTNET, MFCHECK, TEXDUMP

GREEDY_GAMES = 48
GREEDY_MIN_WINS = 5          # decision 21: no worse than the weakest shipped level (5 of 48)
STUCK_MAX_SHARE = 0.05       # level-method.md section 4: the stuck rule fires on fewer than 5% of first shots


def ensure_built():
    for proj, dll in (("mfcheck", MFCHECK), ("texdump", TEXDUMP)):
        src = DOTNET / proj / "Program.cs"
        if not dll.exists() or dll.stat().st_mtime < src.stat().st_mtime:
            subprocess.run(["dotnet", "build", str(DOTNET / proj), "-c", "Release", "--nologo", "-v", "q"], check=True)


def _run(*args, timeout=1800):
    r = subprocess.run(["dotnet", str(MFCHECK), *map(str, args)], capture_output=True, text=True, timeout=timeout)
    return r.returncode, r.stdout


def validate(path):
    """The shipped MoonfallLevelLoader's verdict: (ok, output)."""
    code, out = _run("validate", path)
    return code == 0 and out.startswith("OK"), out.strip()


def colours(path, number, seed):
    code, out = _run("colours", path, number, seed)
    if code != 0:
        raise RuntimeError(out)
    return json.loads(out.strip().splitlines()[-1])


def sweep(path, number, seed=1, step=1.0):
    """One fresh shot at every aim angle: the stuck rule's share, bucket catches, the pieces no first shot reaches."""
    code, out = _run("sweep", path, number, seed, step)
    if code != 0:
        raise RuntimeError(out)
    m = re.search(r"angles (\d+); pegs per first shot: median (\d+), mean ([\d.]+), max (\d+); oranges per first shot mean ([\d.]+)", out)
    n = re.search(r"no peg hit: (\d+); stuck rule fired: (\d+); flights over 15 s: (\d+); bucket catches: (\d+)", out)
    u = re.search(r"pieces no first shot reaches: (\d+) of (\d+)(?:: (.*))?", out)
    angles = int(m.group(1))
    return {"angles": angles, "pegs_median": int(m.group(2)), "pegs_mean": float(m.group(3)), "pegs_max": int(m.group(4)),
            "oranges_mean": float(m.group(5)), "no_hit": int(n.group(1)), "stuck": int(n.group(2)),
            "stuck_share": round(int(n.group(2)) / angles, 3), "long_flights": int(n.group(3)), "bucket": int(n.group(4)),
            "unreached": [int(v) for v in (u.group(3) or "").split(",") if v.strip()], "pieces": int(u.group(2))}


def play(path, number, games=GREEDY_GAMES):
    """mfcheck's greedy player (one-shot lookahead, aim error +-1.5 deg) over `games` seeded games, and the random
    player over 40."""
    code, out = _run("play", path, games, number)
    if code != 0:
        raise RuntimeError(out)
    g = re.search(r"greedy: won (\d+) of (\d+); shots mean ([\d.]+); oranges left when lost ([\d,]*); stuck-rule fires (\d+)", out)
    r = re.search(r"random: won (\d+) of 40; oranges cleared mean ([\d.]+)", out)
    left = [int(v) for v in g.group(4).split(",") if v]
    return {"greedy_won": int(g.group(1)), "games": int(g.group(2)), "shots_mean": float(g.group(3)),
            "oranges_left_when_lost": left, "stuck_fires": int(g.group(5)), "random_won": int(r.group(1)),
            "random_oranges_mean": float(r.group(2))}


def texdump(*args):
    r = subprocess.run(["dotnet", str(TEXDUMP), *map(str, args)], capture_output=True, text=True)
    return r.returncode, r.stdout
