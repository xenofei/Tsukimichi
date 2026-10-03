"""Make round 5 the current concept on the plan site, move round 4 to Earlier concepts, add Round 5 to the mock, update the status pill."""
import base64
import json
import pathlib
import shutil
import subprocess

CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
HERE = pathlib.Path(__file__).parent
MV = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6")
R5 = MV / "round5" / "medallion-r5"
STATES = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"]
LABEL = ["Ready", "Ready, other job", "In journal", "Blocked", "Done this cycle", "Completed", "Locked out", "Not checked"]


def u(p):
    return "data:image/svg+xml;base64," + base64.b64encode(p.read_bytes()).decode()


def shoot(name, body, w, h):
    page = HERE / "designs" / f"{name}.html"
    page.write_text(f"<!doctype html><html><body style='margin:0;background:#0F1424;font-family:Segoe UI,sans-serif'>{body}</body></html>", encoding="utf-8")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=2",
                    f"--screenshot={HERE / 'designs' / (name + '.png')}", f"--window-size={w},{h}", page.as_uri()],
                   check=True, capture_output=True, timeout=60)
    page.unlink()


art = HERE / "art" / "r5"
art.mkdir(parents=True, exist_ok=True)
for f in R5.glob("*.svg"):
    shutil.copy(f, art / f.name)
shoot("medallion-r5-icon", f"<div style='display:grid;place-items:center;width:300px;height:300px'><img src='{u(R5 / 'plugin-icon.svg')}' width=256 height=256></div>", 300, 300)
cells = "".join(f"<div style='width:96px;display:flex;flex-direction:column;align-items:center;gap:6px'><img src='{u(R5 / (s + '.svg'))}' width=64 height=64><div style='display:flex;gap:6px;align-items:center'><img src='{u(R5 / (s + '.svg'))}' width=20 height=20><img src='{u(R5 / (s + '.svg'))}' width=16 height=16></div><div style='font-size:11px;color:#9aa3bb;text-align:center'>{l}</div></div>" for s, l in zip(STATES, LABEL))
shoot("medallion-r5-glyphs", f"<div style='display:flex;gap:4px;padding:16px 12px'>{cells}</div>", 824, 150)
shutil.copy(R5 / "_variants.png", HERE / "designs" / "medallion-r5-variants.png")

D = json.loads((HERE / "designs.json").read_text(encoding="utf-8"))
r4 = D["concepts"][0]
D["heading"] = "Moon & icon: Menphina's Medallion, round five"
D["intro"] = ("<p>You said round four \"looks great\". Round five is the polish you asked for, approved by the realism supervisor with a design critic's help. "
              "<strong>The new plugin icon shipped in 1.11.0</strong>; the moons arrive in 1.12.0 with the new renderer.</p>"
              "<p><strong>What changed:</strong> the job icons are centred in their badge. The icon is centred, with a bigger moon and a Kugane shore of pagodas and red-orange lanterns, each reflected in the water. "
              "Blocked is a new moon behind cloud and a thin wisp; the supervisor chose it from three options because a new moon gives almost no light. "
              "Ready has an open padlock badge, Blocked a closed one, and In journal a journal badge, all in embossed metal like the job icons. Completed keeps the gold check (the green one meant the same thing).</p>")
D["before"] = {"tag": "R4", "name": "Round four", "icon": "designs/medallion-r4-icon.png", "glyphs": "designs/medallion-r4-glyphs.png",
               "text": "You said it looked great. Notes: job icons off-centre, the icon not centred and short on FFXIV detail, Blocked not obscured enough, and no lock or journal badges."}
D["concepts"] = [{"id": "medallion-r5", "tag": "R5", "name": "Menphina's Medallion, round five",
                  "icon": "designs/medallion-r5-icon.png", "glyphs": "designs/medallion-r5-glyphs.png",
                  "pitch": "The final polish: one gilt rim and one badge system. An open lock on Ready, the real job icon on Ready on another job, a journal on In journal and a closed lock on Blocked. The icon is centred over a Kugane shore.",
                  "metrics": [["Weakest pair, 16 px", "12.3", "≥ 12"], ["Ready vs next", "1.34×", "≥ 1.3"], ["Completed vs Ready", "0.74×", "≤ 0.8"], ["Blocked vs Locked out", "12.0", "row size"]],
                  "changes": ["Job icons centred optically in their badge (shifts up to 3.7 units)",
                              "Icon: moon and road centred, a bigger moon, a Kugane shore with pagodas and red-orange lanterns, each light reflected below it",
                              "Blocked: a new moon behind cloud with a thin wisp across its lit edge (the supervisor's choice of three)",
                              "Ready: an open padlock badge. Blocked: a closed padlock badge. In journal: a journal badge",
                              "Badges drawn as embossed metal like the job icons; both locks share one position",
                              "Gold check only"],
                  "watch": ["The weakest pair at row size (Blocked vs Locked out) is exactly at the bar, 12.0",
                            "Below 32 px the badge moves beside the medal; check this in game with 1.12.0",
                            "The installer's Installed check sits over the moonlit water; it's readable in the render, so confirm it in game"]}]
D["extras"] = [{"src": "designs/medallion-r5-variants.png", "cap": "Every badge (open lock, three jobs, journal, closed lock), the Blocked options the supervisor chose from, the row-size fallback, and the icon from 32 to 256 px."}]
sup = D["supervisor"]["summary"]
if "Round five" not in sup:
    sup = sup.replace("<p>Full reviews:", "<ul class='prose' style='margin:0;padding-left:20px'><li><strong>Round five:</strong> a design critic ranked the Blocked options and the badge style; the supervisor chose Blocked b and required 9 fixes (cloud lining only over the lit edge, a soft wisp, metal badges, aligned locks, the icon's horizon seam, a bigger centred moon, a symmetric road). Then approved.</li></ul><p>Full reviews:")
    sup = sup.replace("round4/supervisor/", "round4/supervisor/</code> and <code>docs/design/moon-v6/round5/supervisor/")
D["supervisor"] = {"verdict": "Approved", "summary": sup}
mv = D["mockArt"]["variants"]
if not any(v["id"] == "r5d" for v in mv):
    mv.insert(0, {"id": "r5d", "label": "Round 5 D", "glyphs": {s: f"art/r5/{s}.svg" for s in STATES}, "icon": "art/r5/plugin-icon.svg"})
r4_arch = {"id": "r4-medallion", "tag": "R4", "name": "Menphina's Medallion, round four", "verdict": "Looked great (you); polished",
           "icon": r4["icon"], "glyphs": r4["glyphs"], "pitch": r4["pitch"],
           "scores": [["Weakest pair", "12.3"], ["Ready vs next", "1.37×"]],
           "liked": ["Looked great to you", "Clouds, repeat arrow, shattered moon and question mark all read"],
           "failed": ["Job icons off-centre", "The icon not centred and short on FFXIV detail", "Blocked not obscured enough", "No lock or journal badges"]}
arch = D.get("archive", [])
if not arch or arch[0]["round"] != "Round four":
    arch.insert(0, {"round": "Round four", "note": "Built from your eight notes on round three; you said it looked great and asked for the polish in round five.", "items": [r4_arch]})
D["archive"] = arch
(HERE / "designs.json").write_text(json.dumps(D, ensure_ascii=False, indent=1), encoding="utf-8")

t = (HERE / "template2.html").read_text(encoding="utf-8")
t = t.replace('<span class="pill proposed">Proposed · not signed off</span><span id="pdate">2 October 2026</span><span>Nothing here is built yet.</span>',
              '<span class="pill proposed">Approved · 1.11.0 released</span><span id="pdate">2 October 2026</span><span>1.12.0 next.</span>')
(HERE / "template2.html").write_text(t, encoding="utf-8")
print("round 5 staged")
