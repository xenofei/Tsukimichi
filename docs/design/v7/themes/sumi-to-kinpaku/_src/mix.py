"""Mix check (supervisor S1.4): renders _mix.png, which proves the face/kit swap works both ways.

Rows, each at 96 and 48 px (hero and mid faces) and 20 px (row faces where the set has them), on the Night ground:
  1. Sumi to Kinpaku faces in the Kirikane kit (this theme as designed)
  2. Sumi to Kinpaku faces in the Brass kit (Medallion's bezel and badges)
  3. Medallion r5 faces in the Kirikane kit
  4. Medallion r5 as shipped (its faces in the Brass kit), for reference
  5. The Kirikane kit alone: four urgency tiers at Full and Quiet, the badges, and the ornaments (sigil, corner)
  6. The Decoration Plain finish (_plain/)

Medallion's faces come from its own generator (round5/medallion-r5/_src/gen5.py, read-only): its bezel and badge calls
are swapped for markers, so the well and emblem come out alone, split into 'under' (before the bezel) and 'over' (the
overhangs drawn after it: the ribbon and the check). Run: python _src/mix.py
"""
import base64, importlib.util, pathlib, subprocess

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


G = load("sumi_gen", HERE / "gen.py")
M = load("gen5", OUT.parents[2] / "moon-v6" / "round5" / "medallion-r5" / "_src" / "gen5.py")

KEYS = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"]
BADGES = {"ready": ("open", None), "ready-on-another-job": ("job", "paladin"), "in-journal": ("journal", None), "blocked": ("closed", None)}

# ---- Medallion's faces, unframed ----
brass_bezel, brass_badge = M.bezel, M.badge
M.svg = lambda title, defs, body, vb=128: (list(defs), list(body))
M.bezel = lambda p, hero=True: ([], ["<!--FRAME-->"])
M.badge = lambda p, kind, job=None: ([], ["<!--BADGE-->"])
MFUN = {"ready": M.ready, "ready-on-another-job": M.ready_other_job, "in-journal": M.in_journal, "blocked": M.blocked,
        "done-this-cycle": M.done, "completed": M.completed, "locked-out": M.locked_out, "not-checked": M.not_checked}


def medallion_face(key, tier="hero"):
    D, S = MFUN[key]()
    i = S.index("<!--FRAME-->")
    over = [x for x in S[i + 1:] if x != "<!--BADGE-->"]
    return (D, S[:i]), ([], over)


def set_tier(tier):
    G.ROW, G.MID = tier == "row", tier == "mid"


def sumi_face(key, tier="hero"):
    set_tier(tier)
    try:
        fc = dict(G.STATES)[key]()
    finally:
        set_tier("hero")
    return fc["under"], fc["over"]


def kirikane_kit(key, tier="hero"):
    set_tier(tier)
    try:
        D, S = G.kit_frame("kf-", G.STATE_TIER[key])
        if key in BADGES and tier != "row":
            kind, job = BADGES[key]
            dd, ss = G.badge("kb-", kind, job, "act-now" if kind == "open" else "resting"); D += dd; S += ss
    finally:
        set_tier("hero")
    return D, S


def brass_kit(key, tier="hero"):
    D, S = brass_bezel("bk-", hero=tier != "row")
    if key in BADGES and tier != "row":
        kind, job = BADGES[key]
        dd, ss = brass_badge("bb-", kind, job); D += dd; S += ss
    return D, S


def compose(under, kit, over):
    return G.svg("mix", under[0] + kit[0] + over[0], under[1] + kit[1] + over[1])


def uri(text):
    return "data:image/svg+xml;base64," + base64.b64encode(text.encode("utf-8")).decode()


ROWS = [("Sumi to Kinpaku faces, Kirikane kit (as designed)", sumi_face, kirikane_kit, True),
        ("Sumi to Kinpaku faces, Brass kit", sumi_face, brass_kit, True),
        ("Medallion faces, Kirikane kit", medallion_face, kirikane_kit, False),
        ("Medallion faces, Brass kit (as shipped)", medallion_face, brass_kit, False)]


def main():
    blocks = []
    for label, facefn, kitfn, has_tiers in ROWS:
        cells, tiny = [], []
        for key in KEYS:
            big = compose(*(lambda u, o: (u, kitfn(key), o))(*facefn(key, "hero")))
            mid = compose(*(lambda u, o: (u, kitfn(key, "mid"), o))(*facefn(key, "mid" if has_tiers else "hero")))
            row = compose(*(lambda u, o: (u, kitfn(key, "row"), o))(*facefn(key, "row" if has_tiers else "hero")))
            cells.append(f"<div class=c><img src='{uri(big)}' width=96 height=96><div class=s><img src='{uri(mid)}' width=48 height=48>"
                         f"<img src='{uri(row)}' width=20 height=20></div><span>{key}</span></div>")
            tiny.append(f"<img src='{uri(row)}' width=20 height=20>")
        blocks.append(f"<h2>{label}</h2><div class=r>{''.join(cells)}</div><div class=s style='margin-top:6px'>{''.join(tiny)}"
                      f"<span>&nbsp;20 px row</span></div>")
    kit = []
    for tier in ("act-now", "resting", "finished", "ghost"):
        for quiet in (False, True):
            D, S = G.kit_frame(f"x{tier[:3]}{int(quiet)}-", tier, quiet)
            kit.append((f"{tier} {'quiet' if quiet else 'full'}", G.svg(tier, D, S)))
    for kind, t in (("open", "act-now"), ("closed", "resting"), ("journal", "resting"), ("tank", "resting")):
        D, S = G.badge_ring(f"xr{kind[:3]}-", t)
        dd, ss = G.badge_seat(f"xs{kind[:3]}-", kind); D += dd; S += ss
        if kind != "tank":
            dd, ss = G.badge_content(f"xc{kind[:3]}-", kind); D += dd; S += ss
        kit.append((f"badge {kind}", G.svg(kind, D, ['<g transform="translate(-121.25 -121.25) scale(1.95)">'] + S + ['</g>'])))
    for name in ("sigil", "sigil-small", "lozenge", "corner"):
        kit.append((f"ornament {name}", (OUT / "kit" / "ornaments" / f"{name}.svg").read_text(encoding="utf-8")))
    blocks.append("<h2>Kirikane kit</h2><div class=r>" + "".join(
        f"<div class=c><img src='{uri(t)}' width=96 height=96><span>{lab}</span></div>" for lab, t in kit) + "</div>")
    plain = [(OUT / "_plain" / f"{key}.svg").read_text(encoding="utf-8") for key in KEYS]
    blocks.append("<h2>Decoration Plain (flat ink and gold, state rim, no frame)</h2><div class=r>" + "".join(
        f"<div class=c><img src='{uri(t)}' width=96 height=96><div class=s><img src='{uri(t)}' width=48 height=48>"
        f"<img src='{uri(t)}' width=20 height=20></div><span>{key}</span></div>" for key, t in zip(KEYS, plain)) + "</div>")
    html = ("<html><head><meta charset=utf-8><style>body{margin:0;background:#0F1424;color:#E7E9F2;font:13px Segoe UI,sans-serif;"
            "padding:14px;width:1260px} h2{font-size:14px;margin:14px 0 6px} .r{display:flex;gap:10px;align-items:flex-end;flex-wrap:wrap}"
            " .c{display:grid;justify-items:center;gap:3px;width:140px} .s{display:flex;gap:6px;align-items:center}"
            " span{font-size:11px;color:#9AA3C0}</style></head><body>" + "".join(blocks) + "</body></html>")
    page = OUT / "_mix.html"
    page.write_text(html, encoding="utf-8")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--screenshot={OUT / '_mix.png'}",
                    "--window-size=1290,1440", page.as_uri()], check=True, capture_output=True)
    page.unlink()
    print(OUT / "_mix.png")


if __name__ == "__main__":
    main()
