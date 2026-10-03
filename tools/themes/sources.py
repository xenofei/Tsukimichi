# Python sources for the frames axis (feature plan v7 T11; docs/research/plan-v7/theme-system.md §3.2-3.3): the parts the
# approved generators make but do not write out as files, as SVG text in the 128-unit box for build_themes.py's
# {"python": ..., "call": ..., "args": [...]} specs.
#
#   medallion_face(state, layer, tier)   Menphina's Medallion's unframed faces (the well and emblem, and the overhangs),
#                                        cut from gen5.py read-only, as the sets' _src/mix.py proofs cut them
#   brass_frame(urgency, finish, tier)   the Brass kit's frame: gen5's one gilt bezel (every urgency tier is the same gilt,
#                                        so act-now is gilt as in every kit), and Quiet's silver hairline
#   brass_badge(kind)                    the Brass kit's badges: gen5's badge with its lock or book, or an empty role seat
#   silver_frame(urgency, finish, tier)  the Silver kit's frame (gen_ac.py kit_frame; the row tier is its own cut)
#   silver_badge(kind)                   the Silver kit's badges (gen_ac.py badge and badge_frame)
#
# The faces split as the approved mix proofs split them (aether-crystal/_src/mix.py): the bezel and the badge leave the
# medal, and so does the shadow the raised bezel casts on the well's upper-left edge, which belongs to the frame (every
# kit's frame draws its own). Everything drawn before the bezel is the face's 'under' layer; the ribbon and the check,
# drawn after it, are its 'over' layer.
import importlib.util
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
GEN5 = os.path.join(REPO, "docs", "design", "moon-v6", "round5", "medallion-r5", "_src", "gen5.py")
GEN_AC = os.path.join(REPO, "docs", "design", "v7", "themes", "aether-crystal", "_src", "gen_ac.py")
COMPLETED_V7 = os.path.join(REPO, "docs", "design", "v7", "ui", "completed-moon")

# The DoH/DoL seat (no combat role): gen_atlas.py's HAND, GlyphTokens.Medallion.HandHex.
HAND = ("#66708E", "#2C324C")
ROLE_JOB = {"tank": "paladin", "healer": "white-mage", "dps": "bard"}
MARK = "<!--FRAME-->"

_MODULES = {}


def _load(name, path):
    mod = _MODULES.get(path)
    if mod is None:
        spec = importlib.util.spec_from_file_location(name, path)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        _MODULES[path] = mod
    return mod


def _gen5():
    return _load("themes_gen5", GEN5)


def _ac():
    return _load("themes_gen_ac", GEN_AC)


def _wrap(title, defs, body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="128" height="128"><title>{title}</title>'
            f'<defs>{defs}</defs>{body}</svg>\n')


def _split(text):
    """(defs, body) of one generator SVG."""
    defs = text.split("<defs>", 1)[1].split("</defs>", 1)[0]
    body = text.split("</defs>", 1)[1].rsplit("</svg>", 1)[0]
    return defs, body


# ================================================================ Menphina's Medallion's faces

_STATE_FN = {"ready": "ready", "ready-on-another-job": "ready_other_job", "in-journal": "in_journal",
             "blocked": "blocked", "done-this-cycle": "done", "completed": "completed", "locked-out": "locked_out",
             "not-checked": "not_checked"}


def _medallion_cut(state):
    """(defs, under, over) of a gen5 state with its bezel replaced by a marker, its badge removed and its well's frame
    shadow dropped (the sheen stays: it is the enamel's)."""
    g5 = _gen5()
    bezel, badge, well, row = g5.bezel, g5.badge, g5.well, g5.ROW_TIER
    g5.bezel = lambda p, hero=True: ([], [MARK])
    g5.badge = lambda *a, **k: ([], [])
    g5.well = lambda p, *a, **k: (lambda d, b, o: (d, b, o[:1]))(*well(p, *a, **k))
    try:
        text = getattr(g5, _STATE_FN[state])()
    finally:
        g5.bezel, g5.badge, g5.well, g5.ROW_TIER = bezel, badge, well, row
    defs, body = _split(text)
    under, over = body.split(MARK, 1)
    return defs, under, over


def _completed_cut(tier):
    """Completed's face from the shipped plan v7 masters (docs/design/v7/ui/completed-moon): completed-v7.svg at 96 and
    128 px, completed-v7-small.svg at 48 and 64 px and in rows, cut at gen5's bezel the same way."""
    g5 = _gen5()
    name = "completed-v7.svg" if tier in (96, 128) else "completed-v7-small.svg"
    with open(os.path.join(COMPLETED_V7, name), encoding="utf-8") as fh:
        text = fh.read()
    p = "r5c-"
    bezel = "".join(g5.bezel(p)[1])
    shadow = g5.well(p)[2][1]
    assert text.count(bezel) == 1 and text.count(shadow) == 1, f"{name}: gen5's bezel or well shadow not found once"
    defs, body = _split(text.replace(shadow, "", 1))
    under, over = body.split(bezel, 1)
    return defs, under, over


def medallion_face(state, layer, tier):
    """One layer ('under' or 'over') of Medallion's face of `state` at atlas tier `tier` (48, 64, 96, 128, or 'row')."""
    tier = tier if tier == "row" else int(tier)
    defs, under, over = _completed_cut(tier) if state == "completed" else _medallion_cut(state)
    return _wrap(f"Menphina's Medallion: {state} face ({layer})", defs, under if layer == "under" else over)


# ================================================================ the Brass kit (Medallion's gilt)

def brass_frame(urgency, finish, tier):
    """The Brass kit's frame. Full: gen5's bezel (with its rosettes from 48 px; the row tier's is clean) over the shadow
    it casts on the well. Quiet: Medallion's light rim (MedalArt.LightRimFrame): the pane-coloured gap from the well's
    edge out to a silver hairline at r 55.6, 3 units wide (Night's gap; the runtime's mesh takes the palette's pane)."""
    g5 = _gen5()
    p = f"bk{urgency[:3]}{finish[0]}{'r' if tier == 'row' else 'h'}-"
    if finish == "quiet":
        r, w = 55.6, 3.0
        body = (f'<path d="{g5.ring(g5.R_IN - 0.8, r - w / 2)}" fill="#0E1322" fill-rule="evenodd"/>'
                f'<path d="{g5.ring(r - w / 2, r + w / 2)}" fill="#C3CBDF" fill-opacity=".62" fill-rule="evenodd"/>')
        return _wrap(f"Brass kit: {urgency} frame (quiet)", "", body)
    d, _, over = g5.well(p)
    dd, ss = g5.bezel(p, tier != "row")
    return _wrap(f"Brass kit: {urgency} frame (full)", "".join(d + dd), over[1] + "".join(ss))


def brass_badge(kind):
    """The Brass kit's badge at the shared slot: 'open', 'closed' or 'journal' (gen5's badge with its glyph), or
    'seat-<role>' (the open badge's frame with the role's enamel and nothing in the seat, as gen_atlas.py's
    empty_seat_medal makes it)."""
    g5 = _gen5()
    row, lock, seats = g5.ROW_TIER, g5.lock_glyph, dict(g5.SEAT)
    g5.ROW_TIER = False
    try:
        if kind.startswith("seat-"):
            role = kind[5:]
            g5.SEAT["open"] = g5.ROLE[role] if role in g5.ROLE else HAND
            g5.lock_glyph = lambda p, cx, cy, open_: ([], [])
            d, s = g5.badge("bkb-", "open")
        else:
            d, s = g5.badge("bkb-", kind)
    finally:
        g5.ROW_TIER, g5.lock_glyph = row, lock
        g5.SEAT.clear()
        g5.SEAT.update(seats)
    return _wrap(f"Brass kit: badge {kind}", "".join(d), "".join(s))


# ================================================================ the Silver kit (Aether Crystal's)

def silver_frame(urgency, finish, tier):
    """The Silver kit's frame for an urgency tier (gen_ac.py kit_frame), cut for the row tier below 32 px."""
    ac = _ac()
    row = ac.ROW_TIER
    ac.ROW_TIER = tier == "row"
    try:
        d, s = ac.kit_frame(f"ks{urgency[:3]}{finish[0]}{'r' if tier == 'row' else 'h'}-", urgency, finish)
    finally:
        ac.ROW_TIER = row
    return _wrap(f"Silver kit: {urgency} frame ({finish})", "".join(d), "".join(s))


def silver_badge(kind):
    """The Silver kit's badge: its frame, seat and glyph (gen_ac.py badge), or an empty role seat (badge_frame); the
    Hand seat is the tank seat in Medallion's Hand enamel."""
    ac = _ac()
    row = ac.ROW_TIER
    ac.ROW_TIER = False
    try:
        if kind.startswith("seat-"):
            role = kind[5:]
            d, s = ac.badge_frame("ksb-", "job", ROLE_JOB.get(role, "paladin"))
            text = _wrap(f"Silver kit: badge {kind}", "".join(d), "".join(s))
            if role == "hand":
                tank = ac.ROLE["tank"]
                text = text.replace(tank[0], HAND[0]).replace(tank[1], HAND[1])
            return text
        d, s = ac.badge("ksb-", kind)
    finally:
        ac.ROW_TIER = row
    return _wrap(f"Silver kit: badge {kind}", "".join(d), "".join(s))


def job_icon(job):
    """The game's job icon in the badge seat, as the approved generators place it (gen5.badge): the icon slot (35.5
    units) on the badge centre, optically centred. For the metric composites only; the plugin draws the icon itself."""
    g5 = _gen5()
    b = g5.BADGE
    ox, oy = g5.job_optical_offset(job)
    sz = b["icon"]
    body = (f'<image href="{g5.job_png(job)}" x="{g5.f(b["cx"] - sz / 2 - ox * sz)}" y="{g5.f(b["cy"] - sz / 2 - oy * sz)}" '
            f'width="{g5.f(sz)}" height="{g5.f(sz)}" preserveAspectRatio="xMidYMid meet"/>')
    return _wrap(f"job icon: {job}", "", body)


def is_blank(svg_text):
    """Whether an SVG draws nothing: its body, without defs, title, comments and empty groups, is empty."""
    body = svg_text.split("</defs>", 1)[1] if "</defs>" in svg_text else re.sub(r"^.*?<svg[^>]*>", "", svg_text, flags=re.S)
    body = re.sub(r"<title>.*?</title>|<!--.*?-->|</svg>\s*$", "", body, flags=re.S)
    prev = None
    while prev != body:
        prev = body
        body = re.sub(r"<g[^>]*>\s*</g>|<g[^>]*/>", "", body)
    return not body.strip()
