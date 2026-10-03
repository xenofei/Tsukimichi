"""Builds ../mock.html: the flair-v13 mock's CSS (verbatim, paths re-rooted) + the v7 layer (v7.css) + the v7 script (v7.js).
Run: python build.py"""
import pathlib
SRC = pathlib.Path(__file__).resolve().parent
UI = SRC.parent
v13 = (UI.parents[1] / "flair-v13" / "mock.html").read_text(encoding="utf-8")
css13 = v13[v13.index("<style>") + 7: v13.index("</style>")]
css13 = css13.replace('url("../../plan-site/mock/scene-night.jpg")', 'url("../../../plan-site/mock/scene-night.jpg")')
shell = (SRC / "shell.html").read_text(encoding="utf-8")
out = shell.replace("/*V13CSS*/", css13).replace("/*V7CSS*/", (SRC / "v7.css").read_text(encoding="utf-8") + (SRC / "v715.css").read_text(encoding="utf-8") + (SRC / "v716.css").read_text(encoding="utf-8") + (SRC / "v717.css").read_text(encoding="utf-8")).replace("/*V7JS*/", (SRC / "v7.js").read_text(encoding="utf-8").replace("  // ---------- Page ----------", (SRC / "v715.js").read_text(encoding="utf-8") + (SRC / "v716.js").read_text(encoding="utf-8").replace('/*CONTRAST*/""', __import__("json").dumps((UI / "1.16" / "contrast.md").read_text(encoding="utf-8"))) + (SRC / "v717.js").read_text(encoding="utf-8").replace("/*MIXDATA*/null", (UI / "1.17" / "mixdata.json").read_text(encoding="utf-8")).replace("/*P17*/null", (UI / "1.17" / "palettes17.json").read_text(encoding="utf-8")).replace('/*CONTRAST17*/""', __import__("json").dumps((UI / "1.17" / "contrast17.md").read_text(encoding="utf-8"))) + "  // ---------- Page ----------", 1))
(UI / "mock.html").write_text(out, encoding="utf-8")
print("mock.html", len(out))
