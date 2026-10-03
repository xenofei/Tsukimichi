"""Mix check (supervisor S1.4): renders _mix.png, which proves the face/kit swap works both ways.

Rows, each at 96, 48 and 20 px on the Night ground:
  1. Ishgard Glass faces in the Came kit (this theme as designed)
  2. Ishgard Glass faces in the Brass kit (Medallion's frame and badges)
  3. Medallion r5 faces in the Came kit
  4. Medallion r5 as shipped (its faces in the Brass kit), for reference

Medallion's faces are taken from its own generator (round5/medallion-r5/_src/gen5.py, read-only): its bezel and badge
calls are swapped for markers, so the well and emblem come out alone, split into 'under' (before the bezel) and 'over'
(the overhangs drawn after it: the ribbon and the check). Run: python _src/mix.py
"""
import base64, importlib.util, pathlib, subprocess, sys, tempfile

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


G = load("glass_gen", HERE / "gen.py")
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


def medallion_face(key):
    D, S = MFUN[key]()
    i = S.index("<!--FRAME-->")
    over = [x for x in S[i + 1:] if x != "<!--BADGE-->"]
    return (D, S[:i]), ([], over)


def glass_face(key):
    fc = dict(G.STATES)[key]()
    return fc["under"], fc["over"]


def came_kit(key):
    tier = G.STATE_TIER[key]
    D, S = G.kit_frame("kf-", tier)
    if key in BADGES:
        kind, job = BADGES[key]
        dd, ss = G.badge("kb-", kind, job, "act-now" if kind == "open" else "resting"); D += dd; S += ss
    return D, S


def brass_kit(key):
    D, S = brass_bezel("bk-")
    if key in BADGES:
        kind, job = BADGES[key]
        dd, ss = brass_badge("bb-", kind, job); D += dd; S += ss
    return D, S


def compose(under, kit, over):
    D = under[0] + kit[0] + over[0]
    S = under[1] + kit[1] + over[1]
    return G.svg("mix", D, S)


ROWS = [("Ishgard Glass faces, Came kit", glass_face, came_kit),
        ("Ishgard Glass faces, Brass kit", glass_face, brass_kit),
        ("Medallion faces, Came kit", medallion_face, came_kit),
        ("Medallion faces, Brass kit (as shipped)", medallion_face, brass_kit)]


def main():
    blocks = []
    for label, facefn, kitfn in ROWS:
        uris = []
        for key in KEYS:
            under, over = facefn(key)
            txt = compose(under, kitfn(key), over)
            uris.append("data:image/svg+xml;base64," + base64.b64encode(txt.encode("utf-8")).decode())
        big = "".join(f"<img src='{u}' width=96 height=96>" for u in uris)
        small = "".join(f"<span class=c><img src='{u}' width=48 height=48></span>" for u in uris)
        tiny = "".join(f"<span class=c><img src='{u}' width=20 height=20></span>" for u in uris)
        blocks.append(f"<h2>{label}</h2><div>{big}</div><div>{small}</div><div>{tiny}</div>")
    html = f"""<html><head><meta charset=utf-8><style>
body{{margin:0;background:#0F1424;color:#E7E9F2;font:13px Segoe UI,sans-serif;padding:14px;width:860px}}
h2{{font-size:13px;font-weight:600;margin:14px 0 6px}} img{{vertical-align:middle;margin-right:8px}}
.c{{display:inline-block;width:104px;text-align:left}} div{{margin-bottom:6px}}
</style></head><body>{''.join(blocks)}</body></html>"""
    with tempfile.TemporaryDirectory() as td:
        page = pathlib.Path(td) / "mix.html"
        page.write_text(html, encoding="utf-8")
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--screenshot={OUT / '_mix.png'}",
                        "--window-size=890,1010", page.as_uri()], check=True, capture_output=True)
    print(OUT / "_mix.png")


if __name__ == "__main__":
    main()
