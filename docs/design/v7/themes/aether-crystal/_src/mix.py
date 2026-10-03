"""Mix proof (supervisor S1.4): renders _mix.png with
- row 1: Aether Crystal faces in the Silver kit (as designed);
- row 2: Aether Crystal faces in the Brass kit (Medallion's shipped gilt bezel, well shadow and badges);
- row 3: Medallion-r5 faces in the Silver kit (Medallion's frame, badge and baked well shadow removed);
- row 4: the Silver kit itself: four tiers at Full and Quiet, and the badge frame with each seat and glyph.
Each state is shown at 96, 48 and 20 px. Medallion's generator is imported read-only and patched in memory.

Run from anywhere: python mix.py   (needs Chrome at the usual path)
"""
import base64, importlib.util, pathlib, subprocess, sys

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


ac = load("gen_ac", HERE / "gen_ac.py")
m5 = load("gen5", ac.REPO / "docs/design/moon-v6/round5/medallion-r5/_src/gen5.py")

MARK = "<!--FRAME-->"
_bezel, _badge, _well = m5.bezel, m5.badge, m5.well


def medallion_face(fn, *args):
    """Run a Medallion state builder with its frame replaced by a marker, its badge removed and its well's baked
    frame shadow dropped (that shadow belongs to the kit); split the markup at the marker into under and over."""
    m5.bezel = lambda p, hero=True: ([], [MARK])
    m5.badge = lambda *a, **k: ([], [])
    m5.well = lambda p, *a, **k: (lambda d, b, o: (d, b, o[:1]))(*_well(p, *a, **k))   # keep the sheen only
    try:
        text = fn(*args)
    finally:
        m5.bezel, m5.badge, m5.well = _bezel, _badge, _well
    defs = text.split("<defs>", 1)[1].split("</defs>", 1)[0]
    body = text.split("</defs>", 1)[1].rsplit("</svg>", 1)[0]
    under, over = body.split(MARK, 1)
    return ac.Face("m", [defs], [under], [over])


def brass_frame(state):
    p = f"bk{state[:4]}-"
    d, _, over = _well(p)                      # Medallion's frame shadow on the well (and sheen), clipped to r 52.4
    dd, ss = _bezel(p)
    return d + dd, over + ss


def brass_badge(state):
    kind = ac.BADGE_KIND.get(state)
    if not kind:
        return None
    m5.ROW_TIER = False
    return _badge(f"bb{state[:4]}-", kind, "paladin" if kind == "job" else None)


def uri(text):
    return "data:image/svg+xml;base64," + base64.b64encode(text.encode()).decode()


def main():
    ac.ROW_TIER = False
    rows = []
    ours = {s: fn() for s, fn in ac.FACES}
    r1 = [ac.compose(ours[s], ac.silver_frame(s), ac.silver_badge(s), s) for s, _ in ac.FACES]
    r2 = [ac.compose(ours[s], brass_frame(s), brass_badge(s), s) for s, _ in ac.FACES]
    med_fns = {"ready": (m5.ready,), "ready-on-another-job": (m5.ready_other_job, "paladin"), "in-journal": (m5.in_journal,),
               "blocked": (m5.blocked,), "done-this-cycle": (m5.done,), "completed": (m5.completed,),
               "locked-out": (m5.locked_out,), "not-checked": (m5.not_checked,)}
    r3 = [ac.compose(medallion_face(*med_fns[s]), ac.silver_frame(s), ac.silver_badge(s), s) for s, _ in ac.FACES]
    kit = []
    for tier in ("act-now", "resting", "finished", "ghost"):
        for finish in ("full", "quiet"):
            D, S = ac.kit_frame(f"x{tier[:3]}{finish[0]}-", tier, finish)
            kit.append((f"{tier} {finish}", ac.svg(tier, D, S)))
    for kind in ("open", "closed", "journal"):
        D, S = ac.badge(f"xb{kind[:3]}-", kind)
        kit.append((f"badge {kind}", ac.svg(kind, D, ['<g transform="translate(-121.25 -121.25) scale(1.95)">'] + S + ['</g>'])))
    labels = ["Aether faces, Silver kit", "Aether faces, Brass kit", "Medallion faces, Silver kit"]
    html = ["<html><head><meta charset=utf-8><style>body{margin:0;background:#0F1424;color:#E7E9F2;font:13px Segoe UI,sans-serif;padding:14px;width:1500px}"
            "h2{font-size:14px;margin:14px 0 6px} .r{display:flex;gap:10px;align-items:flex-end} .c{display:grid;justify-items:center;gap:3px}"
            ".s{display:flex;gap:6px;align-items:center} span{font-size:11px;color:#9AA3C0}</style></head><body>"]
    for lab, row in zip(labels, (r1, r2, r3)):
        html.append(f"<h2>{lab}</h2><div class=r>")
        for (s, _), text in zip(ac.FACES, row):
            u = uri(text)
            html.append(f"<div class=c><img src='{u}' width=96 height=96><div class=s><img src='{u}' width=48 height=48><img src='{u}' width=20 height=20></div><span>{s}</span></div>")
        html.append("</div>")
        # a 20 px column, as in a list
        html.append("<div class=s style='margin-top:6px'>" + "".join(f"<img src='{uri(t)}' width=20 height=20>" for t in row) + "<span>&nbsp;20 px row</span></div>")
    html.append("<h2>Silver kit</h2><div class=r>")
    for lab, text in kit:
        html.append(f"<div class=c><img src='{uri(text)}' width=96 height=96><span>{lab}</span></div>")
    html.append("</div></body></html>")
    page = OUT / "_mix.html"
    page.write_text("".join(html), encoding="utf-8")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--screenshot={OUT / '_mix.png'}",
                    "--window-size=1530,1060", page.as_uri()], check=True, capture_output=True)
    page.unlink()
    print(OUT / "_mix.png")


if __name__ == "__main__":
    main()
