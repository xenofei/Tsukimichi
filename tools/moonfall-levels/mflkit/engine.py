"""The shipped engine, through mfcheck (dotnet/mfcheck): the loader's verdict, the engine's colour pick, the first-shot
sweep and the 48 seeded greedy games. Nothing here re-implements the engine."""
import json
import re
import subprocess

from .paths import DOTNET, MFCHECK, TEXDUMP

GREEDY_GAMES = 48
GREEDY_MIN_WINS = 5          # decision 21: no worse than the weakest shipped level (5 of 48)
RAMP_GAMES = 864             # the difficulty ramp (432 or more: coordinator, round 1; 864 so neighbours separate, GD round 2 G2): the first 48 are the rule's games
STUCK_MAX_SHARE = 0.05       # level-method.md section 4: the stuck rule fires on fewer than 5% of first shots
SWEEP_STEP = 0.25            # the first-shot sweep's step (round 2, critic G4: whole degrees missed narrow dead aims)
LOW_Y = 430.0                # an orange whose home is this low sits in the bucket's approach (cheap difficulty)
CHEAP_ONE = 0.25             # ...and may be left in at most this share of lost games (game designer G3)
CHEAP_RATIO = 1.5            # the low candidates' share of the oranges left, at most this times their share of the deal


def piece_homes(path):
    """Each piece's home (x, y) in file order (pegs, then bricks: a brick's midpoint), and whether it is a candidate."""
    lv = json.loads(open(path, encoding="utf-8").read())
    out = [((p["x"], p["y"]), p.get("canBeOrange", False)) for p in lv["pegs"]]
    for b in lv["bricks"]:
        if b["kind"] == "line":
            xy = ((b["x1"] + b["x2"]) / 2, (b["y1"] + b["y2"]) / 2)
        else:
            import math
            a = math.radians(b["start"] + b["sweep"] / 2)
            xy = (b["x"] + b["r"] * math.cos(a), b["y"] + b["r"] * math.sin(a))
        out.append((xy, b.get("canBeOrange", False)))
    return out


def cheap(play_result, homes):
    """The cheap-difficulty verdict from a play result (game designer G3, critic G3): the problems, empty if none.
    Holdouts are keyed by piece (a mover's home, not where it stopped)."""
    probs = []
    for h in play_result["holdouts"]:
        if h["at"][1] >= LOW_Y and h["share_of_lost_games"] >= CHEAP_ONE:
            probs.append(f"the orange at {h['at']} (piece {h['piece']}) is left in {h['share_of_lost_games']:.0%} of lost "
                         f"games ({CHEAP_ONE:.0%} at most below y {LOW_Y:.0f})")
    cands = [xy for (xy, c) in homes if c]
    deal = sum(1 for (_x, y) in cands if y >= LOW_Y) / max(len(cands), 1)
    left = play_result["low_left_share"]
    if deal > 0 and left > CHEAP_RATIO * deal and left >= 0.15:
        probs.append(f"low oranges are {left:.0%} of the oranges left in lost games against {deal:.0%} of the candidates "
                     f"({CHEAP_RATIO} times at most)")
    return probs


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


def sweep(path, number, seed=1, step=SWEEP_STEP):
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


def play(path, number, games=RAMP_GAMES):
    """mfcheck's greedy player (one-shot lookahead, aim error +-1.5 deg) over `games` seeded games, and the random
    player over 40. The rule's verdict is the first 48 seeds (the same games a 48-game run plays); the whole run is the
    difficulty ramp (864 games: about +-0.8 per 48)."""
    code, out = _run("play", path, games, number)
    if code != 0:
        raise RuntimeError(out)
    g48 = re.search(r"greedy48: won (\d+) of (\d+); oranges left when lost ([\d,]*)", out)
    g = re.search(r"greedy: won (\d+) of (\d+); shots mean ([\d.]+); oranges left when lost ([\d,]*); stuck-rule fires (\d+)", out)
    r = re.search(r"random: won (\d+) of 40; oranges cleared mean ([\d.]+)", out)
    homes = piece_homes(path)
    lost_left, n_left, n_low, any_low = {}, 0, 0, 0
    for m in re.finditer(r"^game \d+: lost .*?stuck-rule fires \d+\s+(.*)$", out, re.M):
        idx = [int(i) for i in re.findall(r"#(\d+)\(", m.group(1))]
        low = [i for i in idx if homes[i][0][1] >= LOW_Y]
        n_left, n_low, any_low = n_left + len(idx), n_low + len(low), any_low + (1 if low else 0)
        for i in idx:
            lost_left[i] = lost_left.get(i, 0) + 1
    lost = int(g.group(2)) - int(g.group(1))
    holdouts = sorted(((k, round(v / max(lost, 1), 2)) for k, v in lost_left.items()), key=lambda kv: -kv[1])
    return {"greedy_won": int(g48.group(1)), "games": int(g48.group(2)),
            "oranges_left_when_lost": [int(v) for v in g48.group(3).split(",") if v],
            "ramp_games": int(g.group(2)), "ramp_won": int(g.group(1)), "ramp_per_48": round(int(g.group(1)) * 48 / int(g.group(2)), 1),
            "shots_mean": float(g.group(3)), "stuck_fires": int(g.group(5)), "random_won": int(r.group(1)),
            "random_oranges_mean": float(r.group(2)),
            "low_left_share": round(n_low / max(n_left, 1), 3), "lost_with_low_orange": round(any_low / max(lost, 1), 3),
            "holdouts": [{"piece": k, "at": [round(homes[k][0][0]), round(homes[k][0][1])], "share_of_lost_games": v}
                         for k, v in holdouts]}


def reach(path, number):
    """Section 4's pocket rule: the pieces never reached by a first shot, even once everything reachable before them
    has cleared (file order: pegs, then bricks)."""
    code, out = _run("reach", path, number)
    if code != 0:
        raise RuntimeError(out)
    m = re.search(r"reach: (\d+) rounds; never reached: (\d+)(?:: (.*))?", out)
    return {"rounds": int(m.group(1)), "never": [int(v) for v in (m.group(3) or "").split(",") if v.strip()]}


def texdump(*args):
    r = subprocess.run(["dotnet", str(TEXDUMP), *map(str, args)], capture_output=True, text=True)
    return r.returncode, r.stdout
