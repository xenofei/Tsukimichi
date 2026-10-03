"""Copy the v7 renders into designs/, trimming the empty background below each sheet."""
import pathlib, shutil
from PIL import Image, ImageChops

REPO = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\v7")
OUT = pathlib.Path(__file__).with_name("designs")
OUT.mkdir(exist_ok=True)
for old in OUT.glob("*.png"):
    old.unlink()

FILES = {
    "ui-before-after.png": REPO / "ui/before-after.png",
    "ui-filter-drawer.png": REPO / "ui/filter-drawer.png",
    "ui-rail.png": REPO / "ui/rail-states.png",
    "ui-stars.png": REPO / "ui/stars.png",
    "ui-completed.png": REPO / "ui/completed-moon/compare.png",
}
for t in ("aether-crystal", "astrologian-orrery", "ishgard-glass"):
    FILES[f"{t}-sheet.png"] = REPO / f"themes/{t}/_sheet.png"
    FILES[f"{t}-mix.png"] = REPO / f"themes/{t}/_mix.png"
    if (REPO / f"themes/{t}/_kit.png").exists():
        FILES[f"{t}-kit.png"] = REPO / f"themes/{t}/_kit.png"


def trim(path):
    im = Image.open(path).convert("RGB")
    bg = Image.new("RGB", im.size, im.getpixel((im.width - 1, im.height - 1)))
    box = ImageChops.difference(im, bg).point(lambda v: 255 if v > 6 else 0).getbbox()
    if box:
        pad = 16
        im = im.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(im.width, box[2] + pad), min(im.height, box[3] + pad)))
    return im


for name, src in FILES.items():
    im = trim(src)
    im.save(OUT / name, optimize=True)
    print(f"{name:34s} {im.width}x{im.height} {(OUT / name).stat().st_size // 1024} KB")

# The interactive UI mock, published beside the page as ui-mock.html with its art under mock-art/.
DOCS = REPO.parent.parent
ART = OUT.parent / "mock-art"
shutil.rmtree(ART, ignore_errors=True)
(ART / "medallion" / "_row").mkdir(parents=True)
med = DOCS / "design/moon-v6/round5/medallion-r5"
for f in med.glob("*.svg"):
    shutil.copy(f, ART / "medallion" / f.name)
for f in (med / "_row").glob("*.svg"):
    shutil.copy(f, ART / "medallion" / "_row" / f.name)
for f in ("scene-day.jpg", "scene-night.jpg"):
    shutil.copy(DOCS / "plan-site/mock" / f, ART / f)
for f in ("completed-v7.svg", "completed-v7-small.svg"):
    shutil.copy(REPO / "ui/completed-moon" / f, ART / f)
m = (REPO / "ui/mock.html").read_text(encoding="utf-8")
m = (m.replace("../../../plan-site/mock/", "mock-art/")
      .replace('"../../moon-v6/round5/medallion-r5/"', '"mock-art/medallion/"')
      .replace('"completed-moon/completed-v7.svg"', '"mock-art/completed-v7.svg"')
      .replace('"completed-moon/completed-v7-small.svg"', '"mock-art/completed-v7-small.svg"'))
assert "../" not in m.replace("mock-art/", ""), "unresolved relative path in mock"
(OUT.parent / "ui-mock.html").write_text(m, encoding="utf-8")
print("ui-mock.html", len(m) // 1024, "KB;", sum(1 for _ in ART.rglob("*.*")), "art files")

# Theme switcher for the published mock: whole themes, or a moon per state ("Mix").
TH = {"glass": "ishgard-glass", "crystal": "aether-crystal", "orrery": "astrologian-orrery"}
for key, folder in TH.items():
    dst = ART / "themes" / key
    (dst / "_row").mkdir(parents=True, exist_ok=True)
    for f in (REPO / "themes" / folder).glob("*.svg"):
        shutil.copy(f, dst / f.name)
    for f in (REPO / "themes" / folder / "_row").glob("*.svg"):
        shutil.copy(f, dst / "_row" / f.name)

m = (OUT.parent / "ui-mock.html").read_text(encoding="utf-8")
old_src = 'function medalSrc(st, hero, px) { return V7 && st === "completed"'
assert m.count(old_src) == 1, m.count(old_src)
m = m.replace(old_src, 'function medalSrc(st, hero, px) { var td = themeDir(st); if (td) return td + (hero ? heroFile(st) : "_row/" + st) + ".svg"; return V7 && st === "completed"')
bar = '''<div class="seg" id="thm" role="group" aria-label="Moon theme" title="Moon theme (plan 7, releases 1.16 and 1.17)">
      <button type="button" data-t="medallion">Medallion</button><button type="button" data-t="glass">Ishgard Glass</button><button type="button" data-t="crystal">Aether Crystal</button><button type="button" data-t="orrery">Orrery</button><button type="button" data-t="mix">Mix…</button>
    </div>'''
m = m.replace('<span class="pn" id="pn"></span>', bar + '\n    <span class="pn" id="pn"></span>', 1)
m = m.replace('<div class="host" id="host"></div>', '<div class="mixrow" id="mixrow" hidden></div>\n  <div class="host" id="host"></div>', 1)
js = r'''
  // ---------- Moon themes (published preview only) ----------
  var THEMES = { medallion: "Medallion", glass: "Ishgard Glass", crystal: "Aether Crystal", orrery: "Orrery" };
  var STATES = [["ready","Ready"],["ready-on-another-job","Ready, other job"],["in-journal","In journal"],["blocked","Blocked"],["done-this-cycle","Done this cycle"],["completed","Completed"],["locked-out","Locked out"],["not-checked","Not checked"]];
  var THEME = "medallion", MIX = {};
  try { THEME = localStorage.getItem("v7mock:theme") || "medallion"; MIX = JSON.parse(localStorage.getItem("v7mock:mix") || "{}") || {}; } catch (e) {}
  function themeDir(st) {
    if (!V7) return null;
    var t = THEME === "mix" ? (MIX[st] || "medallion") : THEME;
    return t && t !== "medallion" && THEMES[t] ? "mock-art/themes/" + t + "/" : null;
  }
  var thm = document.getElementById("thm"), mixrow = document.getElementById("mixrow");
  function drawMix() {
    mixrow.hidden = THEME !== "mix";
    if (mixrow.hidden) return;
    mixrow.innerHTML = '<span class="mixlbl">Moon for each state</span>' + STATES.map(function (s) {
      return '<label class="mixc"><span>' + s[1] + '</span><select data-st="' + s[0] + '">' + Object.keys(THEMES).map(function (k) {
        return '<option value="' + k + '"' + ((MIX[s[0]] || "medallion") === k ? " selected" : "") + ">" + THEMES[k] + "</option>"; }).join("") + "</select></label>"; }).join("");
  }
  function setTheme(t) {
    THEME = t; try { localStorage.setItem("v7mock:theme", t); } catch (e) {}
    thm.querySelectorAll("button").forEach(function (b) { b.setAttribute("aria-pressed", b.getAttribute("data-t") === t ? "true" : "false"); });
    drawMix(); show(location.hash.slice(1) || "full");
  }
  thm.addEventListener("click", function (e) { var b = e.target.closest("button"); if (b) setTheme(b.getAttribute("data-t")); });
  mixrow.addEventListener("change", function (e) { var s = e.target.closest("select"); if (!s) return; MIX[s.getAttribute("data-st")] = s.value;
    try { localStorage.setItem("v7mock:mix", JSON.stringify(MIX)); } catch (e2) {} show(location.hash.slice(1) || "full"); });
  thm.querySelectorAll("button").forEach(function (b) { b.setAttribute("aria-pressed", b.getAttribute("data-t") === THEME ? "true" : "false"); });
  drawMix();
'''
anchor = '  window.addEventListener("hashchange", function () { show(location.hash.slice(1)); });'
assert m.count(anchor) == 1
m = m.replace(anchor, js + anchor, 1)
css = '''.mixrow{display:flex;flex-wrap:wrap;gap:8px 14px;align-items:center;margin:0 0 12px;padding:10px 14px;border:1px solid #232A42;border-radius:10px;background:rgba(10,14,27,.85);font-size:12px;color:#A9B2CC}
.mixrow[hidden]{display:none}
.mixlbl{color:#FFF0BE;font-weight:600;margin-right:4px}
.mixc{display:inline-flex;align-items:center;gap:6px}
.mixc select{background:#141A2E;color:#DDE3F0;border:1px solid #2E3550;border-radius:6px;font:inherit;font-size:12px;padding:4px 6px}
'''
m = m.replace("</style>", css + "</style>", 1)
(OUT.parent / "ui-mock.html").write_text(m, encoding="utf-8")
print("theme switcher added;", sum(1 for _ in (ART / "themes").rglob("*.svg")), "theme svgs")
