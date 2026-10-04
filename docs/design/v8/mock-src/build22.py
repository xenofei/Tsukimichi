"""Builds ../mock-1.22.html: the v7 mock (composed exactly as docs/design/v7/ui/mock-src/build.py does, read-only) plus
the 1.22 layer (v722.css, v722.js), with its own view buttons. The page sets <base href="../v7/ui/"> so every asset
path of the v7 mock resolves as in mock.html; the 1.22 art and scenes are reached as ../../v8/... from there.
Run: py -3 build22.py"""
import json
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent                      # docs/design/v8
UI = OUT.parent / "v7" / "ui"         # docs/design/v7/ui
SRC = UI / "mock-src"

v13 = (UI.parents[1] / "flair-v13" / "mock.html").read_text(encoding="utf-8")
css13 = v13[v13.index("<style>") + 7: v13.index("</style>")]
css13 = css13.replace('url("../../plan-site/mock/scene-night.jpg")', 'url("../../../plan-site/mock/scene-night.jpg")')


def read(p):
    return p.read_text(encoding="utf-8")


css = "".join(read(SRC / f) for f in ["v7.css", "v715.css", "v716.css", "v717.css", "v718.css", "v719.css"]) + read(HERE / "v722.css")
layers = (read(SRC / "v715.js")
          + read(SRC / "v716.js").replace('/*CONTRAST*/""', json.dumps(read(UI / "1.16" / "contrast.md")))
          + read(SRC / "v717.js").replace("/*MIXDATA*/null", read(UI / "1.17" / "mixdata.json")).replace("/*P17*/null", read(UI / "1.17" / "palettes17.json")).replace('/*CONTRAST17*/""', json.dumps(read(UI / "1.17" / "contrast17.md")))
          + read(SRC / "v718.js") + read(SRC / "v719.js") + read(HERE / "v722.js"))
js = read(SRC / "v7.js").replace("  // ---------- Page ----------", layers + "  // ---------- Page ----------", 1)

VIEWS = [("wn22a", "What's new · Evercold", "1.22 W1/W2: the What's new popup, page 1 of 2 (1.20.0 Before Evercold), in all six themes."),
         ("wn22b", "What's new · Right answers", "1.22 W1/W2: the What's new popup, page 2 of 2 (1.19.0 Right answers), in all six themes."),
         ("wn22s", "Popup states", "1.22 W1: anatomy, paging, one release, Quiet, Plain, text size 150 % and the timings."),
         ("art22", "Release art", "1.22 W2: one painting per release, restyled per theme by a grade and a motif layer."),
         ("optb22", "Option A or B", "Decision 1: Option A (one painting, restyled) and Option B (distinct treatments per theme), 1.20.0."),
         ("about22", "About · history", "1.22 W3, U1 and M3: Settings › About with What's new, updates and Umbra."),
         ("upd22", "Update ready", "1.22 U1: the status-bar note, the dot on the moon icon, and Update opening Dalamud's installer."),
         ("icon22", "Moon icon", "1.22 H1: the moon icon at rest, on hover, with its quick card, its menu, locked, and its dots."),
         ("fx22", "Icon particles", "1.22 H2: per-theme particles as frame strips, the hover, and the timing spec."),
         ("dtr22", "Server info bar", "1.22 M1: ◑ 12 Ready in the game's server info bar and in Umbra's toolbar, with its hover card."),
         ("umb22", "Tsukimichi for Umbra", "Add-on A1/A2: the native widget and popup, and the small widgets, on two Umbra themes."),
         ("clear22", "Clear of Umbra", "1.22 M3: the moon icon, the Todo overlay and Needs you keep clear of an Umbra top bar.")]
SHOT = '(SHOT22 ? \'<div class="end22"></div>\' : "")'
route = "".join('    else if (v === "%s") host.innerHTML = board_%s() + %s;\n' % (k, k, SHOT) for k, _, _ in VIEWS)
js = js.replace("  // ---------- Page ----------",
                '  var SHOT22 = location.search.indexOf("shot") >= 0;\n'
                '  if (SHOT22) document.documentElement.classList.add("shot22");\n  // ---------- Page ----------', 1)
anchor = '    else if (v === "looks19") host.innerHTML = boardLooks19();\n'
assert anchor in js
js = js.replace(anchor, anchor + route, 1)
notes = "".join('%s: %s, ' % (k, json.dumps(n)) for k, _, n in VIEWS)
js = js.replace('looks19: "1.19: every look.",', 'looks19: "1.19: every look.", ' + notes, 1)
js = js.replace('if (!NOTE[v]) v = "full";', 'if (!NOTE[v]) v = "wn22a";', 1)
assert "board_wn22a" in js and "wn22a:" in js

shell = read(SRC / "shell.html")
seg_start = shell.index('<div class="seg" id="seg"')
seg_end = shell.index("</div>", seg_start) + len("</div>")
buttons = "".join('<button type="button" data-v="%s">%s</button>' % (k, label) for k, label, _ in VIEWS)
shell = shell[:seg_start] + '<div class="seg" id="seg" role="group" aria-label="View">' + buttons + "</div>" + shell[seg_end:]
shell = shell.replace("<title>Plan v7 UI Mock</title>", '<title>Tsukimichi 1.22 mock</title>\n<base href="../v7/ui/">')
shell = shell.replace("<b>Tsukimichi plan v7</b> · UI", "<b>Tsukimichi 1.22</b> · Welcome home")
out = shell.replace("/*V13CSS*/", css13).replace("/*V7CSS*/", css).replace("/*V7JS*/", js)
(OUT / "mock-1.22.html").write_text(out, encoding="utf-8")
print("mock-1.22.html", len(out))
