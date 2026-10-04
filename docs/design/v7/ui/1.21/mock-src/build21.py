"""Builds ../mock-1.21.html: the v7 mock (the same composition as ../../mock-src/build.py, read-only) plus the 1.21
layer (v721.css, v721.js), with its own view buttons. The page sets <base href="../"> so every asset path of the
v7 mock resolves from docs/design/v7/ui/, as in mock.html; the 1.21 icons are at 1.21/icons/.
Run: py -3 build21.py"""
import json
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent
UI = OUT.parent
SRC = UI / "mock-src"

v13 = (UI.parents[1] / "flair-v13" / "mock.html").read_text(encoding="utf-8")
css13 = v13[v13.index("<style>") + 7: v13.index("</style>")]
css13 = css13.replace('url("../../plan-site/mock/scene-night.jpg")', 'url("../../../plan-site/mock/scene-night.jpg")')


def read(p):
    return p.read_text(encoding="utf-8")


css = "".join(read(SRC / f) for f in ["v7.css", "v715.css", "v716.css", "v717.css", "v718.css", "v719.css"]) + read(HERE / "v721.css")
layers = (read(SRC / "v715.js")
          + read(SRC / "v716.js").replace('/*CONTRAST*/""', json.dumps(read(UI / "1.16" / "contrast.md")))
          + read(SRC / "v717.js").replace("/*MIXDATA*/null", read(UI / "1.17" / "mixdata.json")).replace("/*P17*/null", read(UI / "1.17" / "palettes17.json")).replace('/*CONTRAST17*/""', json.dumps(read(UI / "1.17" / "contrast17.md")))
          + read(SRC / "v718.js") + read(SRC / "v719.js") + read(HERE / "v721.js"))
js = read(SRC / "v7.js").replace("  // ---------- Page ----------", layers + "  // ---------- Page ----------", 1)

VIEWS = [("tonight21", "Up next", "1.21: Up next at the top of Tonight, and the way back to Tonight (P1)."),
         ("step21", "Current step", "1.21: go to the current step of a quest in your journal (P2)."),
         ("roster21", "All characters", "1.21: the roster board (P3) with alt goals (N11), and your other characters under the detail hero."),
         ("blues21", "My blues · Your story", "1.21: Do first and Set aside in My blues (P4); your story on one page, with your pace (N9)."),
         ("stories21", "Storylines · cast", "1.21: named side stories and Caught up (P5), Loose ends (N8), who's in it (N10)."),
         ("boards21", "Triple Triad · zones", "1.21: Triple Triad opponents (P6) and the zones board in Nearby (P7)."),
         ("chat21", "Chat lines", "1.21: /tsuki msq, /tsuki next and /tsuki go in chat, for text-to-speech (P8)."),
         ("looks21", "Every look", "1.21: Up next and Loose ends at Full, Quiet and Plain on four palettes.")]
# Router: the 1.21 views, before the chain falls through to the window views.
# With ?shot (render21.py) the page grows to the board's content and a magenta rule marks its end for the crop.
SHOT = '(SHOT21 ? \'<div class="end21"></div>\' : "")'
route = "".join('    else if (v === "%s") host.innerHTML = board_%s() + %s;\n' % (k, k, SHOT) for k, _, _ in VIEWS)
js = js.replace("  // ---------- Page ----------",
                '  var SHOT21 = location.search.indexOf("shot") >= 0;\n'
                '  if (SHOT21) document.documentElement.classList.add("shot21");\n  // ---------- Page ----------', 1)
js = js.replace('    else if (v === "looks19") host.innerHTML = boardLooks19();\n', '    else if (v === "looks19") host.innerHTML = boardLooks19();\n' + route, 1)
notes = "".join('%s: %s, ' % (k, json.dumps(n)) for k, _, n in VIEWS)
js = js.replace('looks19: "1.19: every look.",', 'looks19: "1.19: every look.", ' + notes, 1)
# The default view is the first 1.21 board, not the 1.13 window.
js = js.replace('if (!NOTE[v]) v = "full";', 'if (!NOTE[v]) v = "tonight21";', 1)
assert "board_tonight21" in js and "tonight21:" in js

shell = read(SRC / "shell.html")
seg_start = shell.index('<div class="seg" id="seg"')
seg_end = shell.index("</div>", seg_start) + len("</div>")
buttons = "".join('<button type="button" data-v="%s">%s</button>' % (k, label) for k, label, _ in VIEWS)
shell = shell[:seg_start] + '<div class="seg" id="seg" role="group" aria-label="View">' + buttons + "</div>" + shell[seg_end:]
shell = shell.replace("<title>Plan v7 UI Mock</title>", '<title>Tsukimichi 1.21 mock</title>\n<base href="../">')
shell = shell.replace("<b>Tsukimichi plan v7</b> · UI", "<b>Tsukimichi 1.21</b> · What next, for every character")
out = shell.replace("/*V13CSS*/", css13).replace("/*V7CSS*/", css).replace("/*V7JS*/", js)
(OUT / "mock-1.21.html").write_text(out, encoding="utf-8")
print("mock-1.21.html", len(out))
