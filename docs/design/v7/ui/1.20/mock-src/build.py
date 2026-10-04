"""Builds ../mock-1.20.html: the shared v7 mock (../../mock.html, built by ../../mock-src/build.py) plus the 1.20 layer
(v720.css, v720.js), without touching any shared file, so the 1.21 designer's edits to mock-src never collide with
these. A <base href="../"> keeps every relative path of the shared mock (1.15/art, 1.15/icons, the scene) working.
Views: #shield20 #prep20 #pack20 #looks20.
Run: py -3 build.py   (then render.py for the PNGs)"""
import pathlib

SRC = pathlib.Path(__file__).resolve().parent
OUT = SRC.parent
UI = OUT.parent
html = (UI / "mock.html").read_text(encoding="utf-8")


def swap(text, old, new, count=1):
    assert text.count(old) >= 1, f"anchor not found: {old[:60]}"
    return text.replace(old, new, count)


VIEWS = [("shield20", "1.20 Shield", "1.20: a wider spoiler shield (N6)."),
         ("prep20", "Before Evercold", "1.20: the Before Evercold card (N7)."),
         ("pack20", "Portrait pack", "1.20: the opt-in portrait pack (F4)."),
         ("looks20", "Looks", "1.20: every look."),
         ("looks20b", "Looks 2", "1.20: every look, the new parts.")]
FUNCS = {"shield20": "boardShield", "prep20": "boardPrep", "pack20": "boardPack", "looks20": "boardLooks20", "looks20b": "boardLooks20b"}

html = swap(html, "<head>", '<head>\n<base href="../">')
html = swap(html, "<title>Plan v7 UI Mock</title>", "<title>Tsukimichi 1.20 mock</title>")
html = swap(html, "</style>", (SRC / "v720.css").read_text(encoding="utf-8") + "\n</style>")
anchor = '<button type="button" data-v="looks19">Looks</button>'
html = swap(html, anchor, anchor + "".join(f'<button type="button" data-v="{v}">{label}</button>' for v, label, _ in VIEWS))
anchor = 'looks19: "1.19: every look.",'
html = swap(html, anchor, anchor + " " + " ".join(f'{v}: "{note}",' for v, _, note in VIEWS))
anchor = 'else if (v === "looks19") host.innerHTML = boardLooks19();'
html = swap(html, anchor, anchor + "".join(f'\n    else if (v === "{v}") host.innerHTML = {FUNCS[v]}();' for v, _, _ in VIEWS))
marker = "  // ---------- Page ----------"
i = html.rindex(marker)
html = html[:i] + (SRC / "v720.js").read_text(encoding="utf-8") + html[i:]
(OUT / "mock-1.20.html").write_text(html, encoding="utf-8")
print("mock-1.20.html", len(html))
