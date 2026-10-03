"""Make round 4 the current concept on the plan site: assets, designs.json, template support for extras."""
import base64
import json
import pathlib
import shutil
import subprocess

CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
HERE = pathlib.Path(__file__).parent
MV = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6")
R4 = MV / "round4" / "medallion-r4"
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


# assets
art = HERE / "art" / "r4"
art.mkdir(parents=True, exist_ok=True)
for f in R4.glob("*.svg"):
    shutil.copy(f, art / f.name)
shoot("medallion-r4-icon", f"<div style='display:grid;place-items:center;width:300px;height:300px'><img src='{u(R4 / 'plugin-icon.svg')}' width=256 height=256></div>", 300, 300)
cells = "".join(f"<div style='width:96px;display:flex;flex-direction:column;align-items:center;gap:6px'><img src='{u(R4 / (s + '.svg'))}' width=64 height=64><div style='display:flex;gap:6px;align-items:center'><img src='{u(R4 / (s + '.svg'))}' width=20 height=20><img src='{u(R4 / (s + '.svg'))}' width=16 height=16></div><div style='font-size:11px;color:#9aa3bb;text-align:center'>{l}</div></div>" for s, l in zip(STATES, LABEL))
shoot("medallion-r4-glyphs", f"<div style='display:flex;gap:4px;padding:16px 12px'>{cells}</div>", 824, 150)
shutil.copy(R4 / "_variants.png", HERE / "designs" / "medallion-r4-variants.png")

D = json.loads((HERE / "designs.json").read_text(encoding="utf-8"))
r3 = D["concepts"][0]
D["heading"] = "Moon & icon: Menphina's Medallion, round four"
D["intro"] = ("<p>Round four answers your eight notes on round three and takes the tester's best ideas. "
              "The realism supervisor checked it again before it reached this page.</p>"
              "<p><strong>What changed from your notes:</strong> Ready has a complete rim. "
              "Ready on another job shows the real job icon as a badge; in the plugin it is read from the game, so it works for every job. "
              "Blocked is clouds over the moon. Done this cycle has a repeat arrow. "
              "Completed is a full, bright, detailed moon with a check, in gold or green; you choose. "
              "Locked out is a shattered moon with thin cracks. Not checked is a question mark. "
              "The moon and the water have more detail. From the tester: In journal is the Ready moon with the bookmark.</p>"
              "<p>Nothing is final. Vote or comment on the round-four card, and switch between rounds in the plugin mock below.</p>")
D["before"] = {"name": "Round three", "icon": "designs/medallion-r3-icon.png", "glyphs": "designs/medallion-r3-glyphs.png",
               "text": "Supervisor-approved, but you said it still needed work: Ready's rim was open, the crystal didn't read as a job, Blocked, Done and Not checked didn't make sense, the crack was too thick, and the moon and water needed more detail."}
D["concepts"] = [{"id": "medallion-r4", "tag": "R4", "name": "Menphina's Medallion, round four",
                  "icon": "designs/medallion-r4-icon.png", "glyphs": "designs/medallion-r4-glyphs.png",
                  "pitch": "Every quest state is a minted job-icon medal in one shared gilt rim, now each with a meaning you can read: a moon road for Ready, clouds for Blocked, a repeat arrow for Done this cycle, a shattered moon for Locked out, and a question mark for Not checked.",
                  "metrics": [["Weakest pair, 16 px", "12.3", "≥ 12"], ["Ready vs next", "1.37×", "≥ 1.3"], ["Completed vs Ready", "0.73×", "≤ 0.8"], ["Road brightness", "0.89×", "of the moon"]],
                  "changes": ["Ready: complete rim; still the loudest, through a brighter moon, the road inside the medal and a warm afterglow",
                              "Ready on another job: the real job icon as a badge (Paladin shown; Bard and White Mage below). At row size the job icon sits beside the medal",
                              "In journal: the Ready moon, quieter, with the bookmark (the tester's idea)",
                              "Blocked: clouds drifting across the moon, casting soft shadow from the same light",
                              "Done this cycle: a gilt repeat arrow spiralling around the moon",
                              "Completed: a full, bright, detailed moon with a check, in gold or green",
                              "Locked out: Dalamud's red moon shattered with thin fractures and a falling shard",
                              "Not checked: a question mark in moon silver",
                              "Moon and water: smooth tonal detail on the moon, softer, more natural ripples"],
                  "watch": ["Not checked reads as a question mark in a ring until about 48 px",
                            "The weakest pair (Locked out vs Not checked, 12.3) clears the bar only just",
                            "The job badge needs its row-size fallback (the job icon beside the medal)"]}]
D["extras"] = [{"src": "designs/medallion-r4-variants.png", "cap": "Ready on another job with three real job badges, both Completed checks, and the row-size fallback with the job icon beside the medal."}]
D["supervisor"] = {"verdict": "Approved", "summary": D["supervisor"]["summary"].replace(
    "<p>Full reviews:", "<ul class='prose' style='margin:0;padding-left:20px'><li><strong>Round four:</strong> 3 fixes. A green fringe on blurred shadows (a colour-space setting), ripples that looked like glossy pills, and In journal's crescent tip ending exactly on the bookmark's edge. Then approved.</li></ul><p>Full reviews:").replace("round3/supervisor/", "round3/supervisor/</code> and <code>docs/design/moon-v6/round4/supervisor/")}
mv = D["mockArt"]["variants"]
mv.insert(0, {"id": "r4d", "label": "Round 4 D", "glyphs": {s: f"art/r4/{s}.svg" for s in STATES}, "icon": "art/r4/plugin-icon.svg"})
for v in mv:
    if v["id"] == "r3d":
        v["label"] = "Round 3 D"
r3_arch = {"id": "r3-medallion", "tag": "R3", "name": "Menphina's Medallion, round three", "verdict": "Needs more work (you)",
           "icon": r3["icon"], "glyphs": r3["glyphs"], "pitch": r3["pitch"],
           "scores": [["Weakest pair", "12.8"], ["Ready vs next", "1.42×"]],
           "liked": ["One shared gilt rim on all medals; aetheryte crystal removed", "Approved by the realism supervisor"],
           "failed": ["Ready's open rim wasn't a complete trim", "The crystal didn't read as a job", "Blocked, Done this cycle and Not checked didn't make sense", "Locked out's crack was too thick; the moon and water needed more detail"]}
arch = D.get("archive", [])
if not arch or arch[0]["round"] != "Round three":
    arch.insert(0, {"round": "Round three", "note": "Concept D alone, enhanced from your first notes and approved by the realism supervisor. You said it still needed more work.", "items": [r3_arch]})
D["archive"] = arch
(HERE / "designs.json").write_text(json.dumps(D, ensure_ascii=False, indent=1), encoding="utf-8")

# template: render extras under the current cards
t = (HERE / "template2.html").read_text(encoding="utf-8")
if "D.extras" not in t:
    old = "  return intro + `<div class=\"dz two\">${before}${now}</div>` + supCard + mock + archive;"
    new = ("  const extras = D.extras ? `<div class=\"compare\" style=\"grid-template-columns:minmax(0,1fr)\">${D.extras.map(f => `<figure><img src=\"${esc(f.src)}\" alt=\"${esc(f.cap)}\" loading=\"lazy\" style=\"max-width:1000px;margin:auto\"><figcaption>${esc(f.cap)}</figcaption></figure>`).join(\"\")}</div>` : \"\";\n"
           "  return intro + `<div class=\"dz two\">${before}${now}</div>` + extras + supCard + mock + archive;")
    assert old in t
    t = t.replace(old, new)
    (HERE / "template2.html").write_text(t, encoding="utf-8")
print("round 4 staged")
