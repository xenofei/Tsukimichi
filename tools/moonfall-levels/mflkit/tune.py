"""The difficulty tuner: `mfl.py tune <id | layout.py> [--band LO,HI] [--finale-vs MEAN] [--max-plays N]`.

It moves a finished layout into its difficulty band with small, layout-preserving moves, never touching where the
subject's pieces stand:
- which pieces are orange candidates: the hardest candidates go blue to ease a board, the hardest blue pieces join the
  candidates to harden it, or one of each swaps so the count stays (hardness: how often each piece is the orange left
  in lost games, from one probe of 864 games with every reachable piece a candidate, refined by every play since);
- mover speeds (each drift group's period scaled, slower is easier);
- small nudges (10 units) of the fill's blue moons, the last resort.

Every step is judged on the tuning seeds (1-864, the cached greedy player) against the band narrowed by a margin, and
is accepted only if it brings the figure closer while the pre-flight passes (candidate share, lower third, spread,
greens), the rule's 48 games win at least 2, cheap difficulty does not fault, and, after a geometric move, the sweep
(stuck under 5%, no dead aim) and reach pass. When the tuning figure is inside, the held-out block (865-2592) confirms;
a miss there shifts the tuning target by the measured offset and the search goes on (README, "What build does", 5:
accept on tuning seeds, confirm on held-out). The moves are written as the layout's TUNING block, which the build
applies after build(b) (`Board.apply_tuning`), so the layout source stays as its author wrote it.
"""
import hashlib
import json
import math
import pathlib
import random
import re
import time

from . import author, cache, engine, paths, stagecheck
from .build import board_of, load_layout

OUT = paths.BUILD / "tune"
MARK = "# ---- tuning (written by `mfl.py tune`"
SHARE_LO, SHARE_HI = 0.61, 0.89          # inside the pre-flight's 60-90%, so a move never sits on its edge


class Engine:
    """The real player: level files in build/tune, mfcheck's cached greedy games."""
    def __init__(self, level_id, number):
        self.dir = OUT / level_id
        self.dir.mkdir(parents=True, exist_ok=True)
        self.number = number
        self.runs = 0                     # mfcheck runs that were not cache hits
        self.games = 0

    def path(self, level):
        text = json.dumps(level, indent=2) + "\n"
        p = self.dir / (hashlib.sha1(text.encode()).hexdigest()[:12] + ".json")
        if not p.exists():
            p.write_text(text, encoding="utf-8")
        return p

    def play(self, level, games, first):
        p = self.path(level)
        if cache.play_get(p, self.number, games, first)[1] is None:
            self.runs += 1
            self.games += games
        res = engine.play(p, self.number, games=games, first=first)
        res["cheap"] = engine.cheap(res, engine.piece_homes(p))
        return res

    def geometry(self, level):
        """None when the loader, the sweep and reach pass; else the fault."""
        p = self.path(level)
        ok, out = engine.validate(p)
        if not ok:
            return "loader: " + out.splitlines()[1] if len(out.splitlines()) > 1 else "loader"
        sw = engine.sweep(p, self.number)
        if sw["stuck_share"] >= engine.STUCK_MAX_SHARE or sw["no_hit"]:
            return f"sweep: stuck {sw['stuck']}, dead {sw['no_hit']}"
        if engine.reach(p, self.number)["never"]:
            return "reach"
        return None


class State:
    """Moves relative to the layout's own board: colour overrides, nudges and period factors by piece index (file
    order: pegs, then bricks)."""
    def __init__(self, col=None, nudge=None, period=None):
        self.col, self.nudge, self.period = dict(col or {}), dict(nudge or {}), dict(period or {})

    def copy(self):
        return State(self.col, self.nudge, self.period)

    def key(self):
        return (tuple(sorted(self.col.items())), tuple(sorted(self.nudge.items())), tuple(sorted(self.period.items())))


class Tuner:
    def __init__(self, build_fn, number, band, player, finale_vs=None, max_plays=40, log=print, seed=1):
        """build_fn(tuning dict) -> Board; player has play(level, games, first) and geometry(level)."""
        self.build_fn, self.number, self.player, self.log = build_fn, number, player, log
        self.max_plays = max_plays
        lo, hi = band
        if finale_vs is not None:
            g = stagecheck.CONFIRM_GAMES + stagecheck.HELD
            se = math.hypot(stagecheck.se48(finale_vs, g), stagecheck.se48(finale_vs, g) / 2)
            hi = min(hi, finale_vs - se - 0.3)
        self.lo, self.hi = lo, hi
        self.margin = min(0.4, max((hi - lo) / 4, 0.0))
        self.offset = 0.0                # held-out less tuning, as measured at each confirmation
        self.rng = random.Random(seed)
        self.base = build_fn({})
        self.pieces = self.base.pegs + self.base.bricks
        self.homes = [self.base.home(p) for p in self.pieces]
        self.tags = [t for (k, t) in self.base.tags if k == "peg"] + [t for (k, t) in self.base.tags if k == "brick"]
        self.hard = {}
        self.slope = 0.5                 # per 48 per piece moved, learned as moves land
        self.boost = 0                   # extra pieces per move after a round where nothing helped
        self.evals = 0
        self.moves = []

    # ---- a state's board, tuning block and figures
    def tuning(self, s):
        T = {}
        blue = [self._h(i) for i, v in sorted(s.col.items()) if not v]
        orange = [self._h(i) for i, v in sorted(s.col.items()) if v]
        if blue:
            T["blue"] = blue
        if orange:
            T["orange"] = orange
        if s.nudge:
            T["nudge"] = [[*self._h(i), dx, dy] for i, (dx, dy) in sorted(s.nudge.items())]
        if s.period:
            T["period"] = [[*self._h(i), round(f, 3)] for i, f in sorted(s.period.items())]
        return T

    def _h(self, i):
        return [round(self.homes[i][0], 1), round(self.homes[i][1], 1)]

    def board(self, s):
        return self.build_fn(self.tuning(s))

    def evaluate(self, s):
        """(score, figure, report) on the tuning seeds; score 0 is inside the narrowed band; None if a rule faults."""
        b = self.board(s)
        probs = b.check(verbose=False)
        if probs:
            return None, None, {"fault": probs[0]}
        level = b.level_json("tune", "tune", "tune")
        if s.nudge or s.period:
            g = self.player.geometry(level)
            if g:
                return None, None, {"fault": g}
        pl = self.player.play(level, engine.RAMP_GAMES, 0)
        self.evals += 1
        for h in pl["holdouts"]:                    # refine each piece's hardness with what this play saw
            i = h["piece"]
            self.hard[i] = 0.7 * self.hard.get(i, h["share_of_lost_games"]) + 0.3 * h["share_of_lost_games"]
        fig = pl["ramp_per_48"]
        lo, hi = self.target()
        dist = max(lo - fig, fig - hi, 0.0)
        score = dist + (3.0 if pl["cheap"] else 0.0) + (10.0 if pl["greedy_won"] < engine.GREEDY_MIN_WINS else 0.0)
        return score, fig, {"wins48": pl["greedy_won"], "cheap": pl["cheap"], "level": level}

    def target(self):
        return self.lo + self.margin - self.offset, self.hi - self.margin - self.offset

    # ---- hardness: one probe with every reachable piece a candidate
    def probe(self):
        b = self.board(State())
        for p in b.pegs + b.bricks:
            p["canBeOrange"] = b.piece_in_reach(p)
        pl = self.player.play(b.level_json("probe", "probe", "probe"), engine.RAMP_GAMES, 0)
        self.hard = {h["piece"]: h["share_of_lost_games"] for h in pl["holdouts"]}

    # ---- moves
    def cands(self, s):
        b = self.board(s)
        return [p["canBeOrange"] for p in b.pegs + b.bricks], [b.piece_in_reach(p) for p in b.pegs + b.bricks]

    def proposals(self, s, fig, rep):
        orange, reach = self.cands(s)
        n = len(orange)
        lo, hi = self.target()
        easier = fig < lo if not rep.get("cheap") else True
        gap = abs((lo if fig < lo else hi) - fig) if not rep.get("cheap") else 1.0
        k = max(1, min(6, round(gap / max(self.slope, 0.15))))
        h = lambda i: self.hard.get(i, 0.0)  # noqa: E731
        low = lambda i: self.homes[i][1] >= engine.LOW_Y  # noqa: E731
        cand = [i for i in range(n) if orange[i]]
        blue = [i for i in range(n) if not orange[i] and reach[i]]
        out = []
        if rep.get("cheap"):                       # the low candidates the player leaves most go blue first
            lows = sorted((i for i in cand if low(i)), key=lambda i: -h(i))
            for kk in (1, 2):
                if len(lows) > kk:
                    out.append((f"blue {kk} low candidate(s) left most often", {i: False for i in lows[:kk]}))
        hard_c = sorted(cand, key=lambda i: -h(i))
        easy_c = sorted(cand, key=h)
        hard_b = sorted((i for i in blue if not low(i)), key=lambda i: -h(i))
        easy_b = sorted((i for i in blue if not low(i)), key=h)
        for kk in sorted({k, max(1, k // 2), 1, k + 2 + self.boost, 2 * k + self.boost}, key=lambda q: (q != k, q)):
            if easier:
                if (len(cand) - kk) / n >= SHARE_LO:
                    out.append((f"blue the {kk} hardest candidate(s)", {i: False for i in hard_c[:kk]}))
                if len(easy_b) >= kk:
                    out.append((f"swap the {kk} hardest candidate(s) for the {kk} easiest blue piece(s)",
                                {**{i: False for i in hard_c[:kk]}, **{i: True for i in easy_b[:kk]}}))
                if (len(cand) + kk) / n <= SHARE_HI and len(easy_b) >= kk:
                    out.append((f"make the {kk} easiest blue piece(s) candidates", {i: True for i in easy_b[:kk]}))
            else:
                if (len(cand) + kk) / n <= SHARE_HI and len(hard_b) >= kk:
                    out.append((f"make the {kk} hardest blue piece(s) candidates", {i: True for i in hard_b[:kk]}))
                if (len(cand) - kk) / n >= SHARE_LO:
                    out.append((f"blue the {kk} easiest candidate(s)", {i: False for i in easy_c[:kk]}))
                if len(hard_b) >= kk:
                    out.append((f"swap the {kk} easiest candidate(s) for the {kk} hardest blue piece(s)",
                                {**{i: False for i in easy_c[:kk]}, **{i: True for i in hard_b[:kk]}}))
        moves = [(name, self._colour(s, ch)) for name, ch in out]
        movers = sorted({self._group(i) for i, p in enumerate(self.pieces) if "move" in p} - {None})
        for g in movers:
            f = s.period.get(g, 1.0) * (1.25 if easier else 0.8)
            if 0.6 <= f <= 1.6:
                t = s.copy()
                t.period[g] = f
                moves.append((f"movers with the one at {self._h(g)}: period x{f:.2f}", t))
        fill = [i for i, p in enumerate(self.pieces) if self.tags[i].startswith(("fill", "lane", "star"))
                and "kind" not in p and "move" not in p and not orange[i]]
        self.rng.shuffle(fill)
        for i in fill[:3]:
            dx, dy = self.rng.choice(((10, 0), (-10, 0), (0, 10), (0, -10)))
            ox, oy = s.nudge.get(i, (0, 0))
            if abs(ox + dx) <= 20 and abs(oy + dy) <= 20:
                t = s.copy()
                t.nudge[i] = (ox + dx, oy + dy)
                moves.append((f"nudge the blue moon at {self._h(i)} by ({dx}, {dy})", t))
        return moves

    def _colour(self, s, changes):
        t = s.copy()
        base = [p["canBeOrange"] for p in self.pieces]
        for i, v in changes.items():
            if base[i] == v:
                t.col.pop(i, None)
            else:
                t.col[i] = v
        return t

    def _group(self, i):
        p = self.pieces[i]
        if "move" not in p:
            return None
        d = author.Board._drift(p)
        return next(j for j, q in enumerate(self.pieces) if "move" in q and author.Board._drift(q) == d)

    # ---- the search
    def run(self, start=None):
        t0 = time.time()
        s = start or State()
        score, fig, rep = self.evaluate(s)
        if score is None:
            self.log(f"  the layout faults before tuning: {rep['fault']}")
            return dict(ok=False, state=s, reason=rep["fault"], seconds=time.time() - t0)
        self.probe()
        self.log(f"  start: tuning seeds {fig}/48 (target {self.target()[0]:.2f}-{self.target()[1]:.2f}, band "
                 f"{self.lo:.2f}-{self.hi:.2f} held out); wins {rep['wins48']}/48" + (f"; cheap: {rep['cheap'][0]}" if rep["cheap"] else ""))
        confirms, held = 0, None
        seen = {s.key()}
        while self.player.runs < self.max_plays:
            if score == 0.0:
                pl = self.player.play(rep["level"], engine.HELD_GAMES, engine.RAMP_GAMES)
                held = pl["ramp_per_48"]
                confirms += 1
                if self.lo <= held <= self.hi:
                    self.log(f"  confirmed: held out {held}/48 (band {self.lo:.2f}-{self.hi:.2f}); tuning {fig}/48")
                    return dict(ok=True, state=s, tuning=fig, held=held, seconds=time.time() - t0, moves=self.moves)
                self.offset = held - fig
                self.log(f"  held out {held}/48 misses the band: the tuning target shifts by {-self.offset:+.2f}")
                if confirms >= 3:
                    break
                lo, hi = self.target()
                score = max(lo - fig, fig - hi, 0.0) + (3.0 if rep["cheap"] else 0.0)
                continue
            best = None
            for name, t in self.proposals(s, fig, rep):
                if t.key() in seen or self.player.runs >= self.max_plays:
                    continue
                seen.add(t.key())
                sc, f2, r2 = self.evaluate(t)
                if sc is None or sc >= score - 0.05:
                    continue
                best = (name, t, sc, f2, r2)
                break
            if best is None:
                if self.boost < 4:               # one piece is within the seed blocks' noise: try bigger moves
                    self.boost += 2
                    continue
                self.log("  no move brings it closer")
                break
            self.boost = 0
            name, t, sc, f2, r2 = best
            if abs(f2 - fig) > 0.05:
                self.slope = 0.6 * self.slope + 0.4 * abs(f2 - fig) / max(len(set(t.col.items()) ^ set(s.col.items())), 1)
            self.log(f"  accepted: {name}: {fig} -> {f2}/48 on the tuning seeds (wins {r2['wins48']}/48)"
                     + (f"; cheap: {r2['cheap'][0]}" if r2["cheap"] else ""))
            self.moves.append(name)
            s, score, fig, rep = t, sc, f2, r2
        return dict(ok=False, state=s, tuning=fig, held=held, seconds=time.time() - t0, moves=self.moves,
                    reason="plays exhausted" if self.player.runs >= self.max_plays else "stuck")

    def start_state(self, tuning):
        """The State a layout's existing TUNING block stands for."""
        s = State()
        if not tuning:
            return s
        b = self.build_fn(tuning)
        for i, (p, q) in enumerate(zip(self.pieces, b.pegs + b.bricks)):
            if p["canBeOrange"] != q["canBeOrange"]:
                s.col[i] = q["canBeOrange"]
        for (x, y, dx, dy) in tuning.get("nudge", ()):
            s.nudge[self._index(x, y)] = (dx, dy)
        for (x, y, f) in tuning.get("period", ()):
            s.period[self._group(self._index(x, y))] = f
        return s

    def _index(self, x, y):
        return min(range(len(self.homes)), key=lambda i: math.hypot(self.homes[i][0] - x, self.homes[i][1] - y))


def write_tuning(src, dst, tuning, note):
    """The layout's text with its TUNING block replaced (or added) at the end."""
    text = pathlib.Path(src).read_text(encoding="utf-8")
    if MARK in text:
        text = text[:text.index(MARK)].rstrip() + "\n"
    elif re.search(r"^TUNING\s*=", text, re.M):
        raise SystemExit(f"{src} has a TUNING block not written by the tuner: merge by hand")
    body = "{" + ",".join(f"\n    {json.dumps(k)}: {json.dumps(v)}" for k, v in tuning.items()) + ("\n}" if tuning else "}")
    text += f"\n\n{MARK}; {note})\nTUNING = {body}\n"
    pathlib.Path(dst).parent.mkdir(parents=True, exist_ok=True)
    pathlib.Path(dst).write_text(text, encoding="utf-8")
    return dst


HOLD_TOL = 0.6          # --hold: back within this of the locked held-out figure (about one standard error of 1728 games)
STAGE_TOL = 0.6         # --stage: a level within this of its target is left as it is
FINALE_MARGIN = 0.5     # --stage: the finale's target sits this far beyond the one-SE gap the stage check asks


def report_path(level_id):
    return paths.REPORT / f"{level_id}.json"


def lock_of(level_id):
    """The level's locked held-out figure: its report's `locked` (written by tune), else the held-out figure the
    report carries (the last build's), or None."""
    p = report_path(level_id)
    if not p.exists():
        return None
    rep = json.loads(p.read_text(encoding="utf-8"))
    return (rep.get("locked") or {}).get("held_out", rep.get("checks", {}).get("play", {}).get("ramp_per_48"))


def record_lock(level_id, held, band, how, path=None):
    """Writes the level's locked target into its report (the build carries it forward), so a later fixer re-tunes to
    it with --hold instead of moving the stage."""
    p = pathlib.Path(path) if path else report_path(level_id)
    rep = json.loads(p.read_text(encoding="utf-8")) if p.exists() else {"id": level_id}
    rep["locked"] = {"held_out": held, "band": [round(band[0], 2), round(band[1], 2)], "how": how,
                     "when": time.strftime("%Y-%m-%d %H:%M")}
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(rep, indent=1) + "\n", encoding="utf-8")
    return p


def tune_layout(arg, band=None, finale_vs=None, max_plays=40, out=None, log=print, hold=False, lock=None, how="tune"):
    """The command: a level id (layouts/<id>.py) or a layout file. Writes the tuned layout in place, or to `out`.
    hold: re-tune to the level's locked held-out figure (`lock`, else its report's) within HOLD_TOL, the
    difficulty-neutral mode for fixers. A tune of a level id in place records the confirmed figure as its lock."""
    path = pathlib.Path(arg) if arg.endswith(".py") else paths.LAYOUTS / f"{arg}.py"
    mod = load_layout(pathlib.Path(arg).stem if arg.endswith(".py") else arg, path)
    meta = dict(mod.LEVEL)
    if hold:
        lock = lock if lock is not None else lock_of(meta["id"])
        if lock is None:
            raise SystemExit(f"{meta['id']}: no locked figure (no report): pass --lock X")
        band = (lock - HOLD_TOL, lock + HOLD_TOL)
        how = "hold"
    if band is None:
        band = stagecheck.MEAN_BANDS[stagecheck.level_campaign(meta["id"]) + str(meta["stage"])]
    t0 = time.time()
    player = Engine(meta["id"], meta["number"])
    tuner = Tuner(lambda T: board_of(mod, tuning=T)[2], meta["number"], band, player, finale_vs, max_plays, log)
    log(f"{meta['id']} ({path.name}): band {tuner.lo:.2f}-{tuner.hi:.2f} per 48 held out"
        + (f" (finale below the others' mean {finale_vs})" if finale_vs is not None else "")
        + (f" (held at its locked {lock}/48)" if hold else ""))
    res = tuner.run(tuner.start_state(getattr(mod, "TUNING", None)))
    T = tuner.tuning(res["state"])
    note = (f"held out {res.get('held')}/48, tuning seeds {res.get('tuning')}/48, band {tuner.lo:.2f}-{tuner.hi:.2f}"
            + ("" if res["ok"] else f"; NOT in band: {res.get('reason')}"))
    dst = write_tuning(path, out or path, T, note)
    if res["ok"] and (out is None and not arg.endswith(".py")):
        record_lock(meta["id"], res["held"], (tuner.lo, tuner.hi), how)
    elif res["ok"] and out is not None:
        record_lock(meta["id"], res["held"], (tuner.lo, tuner.hi), how, pathlib.Path(out).with_suffix(".report.json"))
    log(f"{'tuned' if res['ok'] else 'NOT tuned'} in {time.time() - t0:.0f} s: {len(tuner.moves)} moves, {player.runs} plays "
        f"({player.games} games) and {tuner.evals} evaluations; wrote {dst}")
    res.update(plays=player.runs, games=player.games, path=str(dst), band=(tuner.lo, tuner.hi))
    return res


def stage_targets(figs, band, gap):
    """Each level's held-out target for a stage (finale last): the others move together, by the least shift that
    puts the stage's mean at its band's centre with the finale `gap` below their mean; a level already within
    STAGE_TOL of its target is left (target None)."""
    lo, hi = band
    mid = (lo + hi) / 2
    others = figs[:-1]
    m_o = mid + gap / len(figs)                  # (k * m_o + (m_o - gap)) / (k + 1) = mid
    mean_o = sum(others) / len(others)
    ok_mean = lo + 0.3 <= sum(figs) / len(figs) <= hi - 0.3
    shift = 0.0 if ok_mean and mean_o - figs[-1] >= gap else m_o - mean_o
    if ok_mean and mean_o - figs[-1] >= gap:
        return [None] * len(figs)
    if ok_mean:                                  # only the finale is wrong: move it alone if the mean allows
        f = mean_o - gap
        if lo <= (sum(others) + f) / len(figs) <= hi:
            return [None] * len(others) + [max(f, float(engine.GREEDY_MIN_WINS))]
    t_o = [max(x + shift, float(engine.GREEDY_MIN_WINS)) for x in others]
    f = max(sum(t_o) / len(t_o) - gap, float(engine.GREEDY_MIN_WINS))
    targets = t_o + [f]
    return [None if abs(t - x) <= STAGE_TOL else round(t, 2) for t, x in zip(targets, figs)]


def stage_levels(key):
    """The stage's level ids in play order (the finale last), from the layouts' LEVEL."""
    camp, n = key[:2], int(key[2:])
    prefix = {"mr": "base-", "fs": "expansion-"}[camp]
    out = []
    for p in sorted(paths.LAYOUTS.glob(prefix + "*.py")):
        meta = load_layout(p.stem).LEVEL
        if meta["stage"] == n:
            out.append((meta["number"], p.stem))
    return [lid for (_n, lid) in sorted(out)]


def tune_stage(key, max_plays=40, out_dir=None, rounds=2, log=print):
    """`tune --stage <key>`: the five levels together against the stage's mean band and the finale rule. Each level
    is tuned (tuning seeds, confirmed held out) to its share of the plan; the stage is then validated on the stage
    check's fresh block (seeds 2593-7776, 5184 games a level), which no tuning step used, pooled with the held-out
    block exactly as `mfl.py stage` judges it. Up to `rounds` plans. Writes each tuned layout (in place, or to
    out_dir) and records each level's lock."""
    key = stagecheck.stage_key(key)
    ids = stage_levels(key)
    band = stagecheck.MEAN_BANDS[key]
    t0 = time.time()
    log(f"stage {key}: {', '.join(ids)}; band {band[0]:.2f}-{band[1]:.2f}, finale below the others' mean by one SE")
    files = {lid: (pathlib.Path(out_dir) / f"{lid}.py" if out_dir else paths.LAYOUTS / f"{lid}.py") for lid in ids}
    if out_dir:
        pathlib.Path(out_dir).mkdir(parents=True, exist_ok=True)
        for lid in ids:
            if not files[lid].exists():
                files[lid].write_text((paths.LAYOUTS / f"{lid}.py").read_text(encoding="utf-8"), encoding="utf-8")
    plays = 0
    pooled = {}
    for rnd in range(rounds + 1):
        held, fresh, total = {}, {}, engine.HELD_GAMES + stagecheck.CONFIRM_GAMES
        for lid in ids:
            mod = load_layout(lid, files[lid])
            meta = mod.LEVEL
            pl = Engine(lid, meta["number"])
            lv = board_of(mod)[2].level_json(lid, meta["name"], meta["scene"])
            held[lid] = pl.play(lv, engine.HELD_GAMES, engine.RAMP_GAMES)["ramp_per_48"]
            fresh[lid] = pl.play(lv, stagecheck.CONFIRM_GAMES, engine.RAMP_GAMES + engine.HELD_GAMES)["ramp_per_48"]
            pooled[lid] = round((held[lid] * engine.HELD_GAMES + fresh[lid] * stagecheck.CONFIRM_GAMES) / total, 2)
            plays += pl.runs
        rows = [(lid, pooled[lid], (None, None)) for lid in ids]
        faults = [f for f in stagecheck.faults(key, rows, None, {lid: total for lid in ids}) if not f.startswith("F7")]
        figs = [pooled[lid] for lid in ids]
        log(f"  round {rnd}: pooled {', '.join(f'{lid} {pooled[lid]}' for lid in ids)}; mean {sum(figs) / len(figs):.2f}"
            + ("; " + " | ".join(faults) if faults else "; the stage passes"))
        if not faults or rnd == rounds:
            break
        fin = figs[-1]
        se_gap = math.hypot(stagecheck.se48(fin, total),
                            math.sqrt(sum(stagecheck.se48(f, total) ** 2 for f in figs[:-1])) / (len(figs) - 1))
        targets = stage_targets([held[lid] for lid in ids], band, se_gap + FINALE_MARGIN)
        for lid, t in zip(ids, targets):
            if t is None:
                continue
            log(f"  {lid}: held out {held[lid]} -> target {t}")
            r = tune_layout(str(files[lid]) if out_dir else lid, band=(t - STAGE_TOL, t + STAGE_TOL), max_plays=max_plays,
                            out=files[lid] if out_dir else None, log=lambda s: log("    " + s), how=f"stage {key}")
            plays += r["plays"]
    ok = not faults
    for lid in ids:
        if ok and not out_dir:
            record_lock(lid, held[lid], band, f"stage {key}")
    log(f"stage {key} {'PASSES' if ok else 'still faults'} after {time.time() - t0:.0f} s and {plays} engine plays")
    return dict(ok=ok, pooled=pooled, plays=plays, seconds=time.time() - t0)


class MockPlayer:
    """A synthetic player for the self-test: each candidate's hardness is fixed by where it stands (lower and nearer
    the walls is harder), the figure falls with the mean hardness of the candidates, and a small seed-block noise is
    fixed per board, so the tuner's search can be checked in a second, without the engine."""
    def __init__(self):
        self.runs = 0
        self.games = 0

    @staticmethod
    def hardness(x, y):
        return 0.2 + 0.6 * (y / 600) + 0.4 * (abs(x - 400) / 325)

    def play(self, level, games, first):
        self.runs += 1
        self.games += games
        pieces = level["pegs"] + [dict(x=(b["x1"] + b["x2"]) / 2, y=(b["y1"] + b["y2"]) / 2, **b) if b["kind"] == "line"
                                  else dict(b) for b in level["bricks"]]
        hs = [(i, self.hardness(p["x"], p["y"])) for i, p in enumerate(pieces) if p.get("canBeOrange")]
        mean = sum(h for _i, h in hs) / max(len(hs), 1)
        noise = (int(hashlib.sha1(json.dumps([level, first]).encode()).hexdigest()[:6], 16) / 0xFFFFFF - 0.5) * 0.6
        fig = round(max(0.0, 68.0 - 80.0 * mean + noise), 1)
        tot = sum(h for _i, h in hs) or 1
        return {"ramp_per_48": fig, "greedy_won": max(int(fig), 0), "cheap": [],
                "holdouts": [{"piece": i, "share_of_lost_games": round(25 * h / tot, 3)} for i, h in hs]}

    def geometry(self, level):
        return None


def selftest(verbose=True):
    """The tuner converges on a synthetic level: a dense grid board under a synthetic player, from outside the band
    into it (both directions), with the pre-flight passing at every accepted step, within 25 plays; and the block it
    writes rebuilds the tuned board."""
    def grid(T):
        b = author.Board()
        k = 0
        keep = []
        for row, y in enumerate(range(150, 500, 36)):
            for x in range(110 + (row % 2) * 24, 700, 48):
                if b.fits(x, y):
                    b.peg(x, y, green=(k % 4 == 3), tag="fill grid")
                    if k % 4 == 3:
                        keep.append((x, y))
                    k += 1
        b.candidates(exclude=keep)
        b.apply_tuning(T)
        return b
    cases = []
    start = MockPlayer().play(grid({}).level_json("g", "g", "g"), 864, 0)["ramp_per_48"]
    for name, band in (("harder", (start - 4.0, start - 2.5)), ("easier", (start + 1.5, start + 3.0))):
        pl = MockPlayer()
        tu = Tuner(grid, 3, band, pl, max_plays=25, log=lambda *_a: None)
        res = tu.run()
        b = grid(tu.tuning(res["state"]))
        cases.append((f"the tuner brings a synthetic board {name} into {band[0]:.1f}-{band[1]:.1f} (from {start}): "
                      f"{res.get('held')} in {pl.runs} plays", res["ok"] and not b.check(verbose=False)))
    # --hold: a fix that made the board easier (its hardest candidates blue) is tuned back to the locked figure
    pl = MockPlayer()
    lock = start
    b0 = grid({})
    easy = sorted((p for p in b0.pegs if p["canBeOrange"]), key=lambda p: -MockPlayer.hardness(p["x"], p["y"]))[:10]
    fixed = {"blue": [[p["x"], p["y"]] for p in easy]}
    moved = pl.play(grid(fixed).level_json("g", "g", "g"), 864, 0)["ramp_per_48"]
    tu = Tuner(lambda T: grid({**fixed, **{k: fixed.get(k, []) + v for k, v in T.items()}}), 3,
               (lock - HOLD_TOL, lock + HOLD_TOL), pl, max_plays=25, log=lambda *_a: None)
    res = tu.run()
    cases.append((f"--hold: a fix moved the board from its locked {lock} to {moved}; tuned back to {res.get('held')} "
                  f"{res.get('reason', '')} "
                  f"in {pl.runs} plays", res["ok"] and abs(moved - lock) > HOLD_TOL))
    # the lock is written to the report and read back
    import tempfile
    with tempfile.TemporaryDirectory() as d:
        p = record_lock("selftest", 8.4, (7.8, 9.0), "hold", pathlib.Path(d) / "r.json")
        got = json.loads(p.read_text(encoding="utf-8"))["locked"]["held_out"]
    cases.append(("the locked figure is recorded in the report", got == 8.4))
    # --stage: the plan, judged by the stage check itself on the planned figures
    band = stagecheck.MEAN_BANDS["mr3"]
    for name, figs in (("a stage in its band, the finale below", [4.6, 4.2, 4.8, 4.4, 3.0]),
                       ("a finale easier than the others", [4.0, 4.4, 4.2, 4.0, 5.2]),
                       ("a stage far too easy", [9.0, 8.5, 9.4, 8.8, 8.0]),
                       ("a stage too hard", [2.4, 2.6, 2.2, 2.9, 2.5])):
        gap = 0.25 + FINALE_MARGIN
        tg = stage_targets(figs, band, gap)
        after = [f if t is None else t for f, t in zip(figs, tg)]
        rows = [(f"l{i}", a, (None, None)) for i, a in enumerate(after)]
        faults = stagecheck.faults("mr3", rows, None, {f"l{i}": 6912 for i in range(5)})
        untouched = sum(t is None for t in tg)
        cases.append((f"--stage plan, {name}: {figs} -> {after} ({untouched} untouched){'; ' + faults[0] if faults else ''}",
                      not faults and (untouched == 5 if name.startswith("a stage in") else True)))
    ok = True
    for name, good in cases:
        ok &= good
        if verbose:
            print(f"  {'ok ' if good else 'BAD'} tune: {name}")
    return ok
