"""Contact sheet for round 4's extra files: the job-badge variants and the two Completed checks, at 16, 20, 32, 64 and
128 px on the plugin's Night ground, plus daylight and greyscale at 20 and 64 px, and a row-size mock with text.

Usage: python variants_sheet.py <folder> <out.png>
"""
import base64, pathlib, subprocess, sys

CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
NAMES = [("ready.svg", "Ready (for reference)"), ("ready-on-another-job-paladin.svg", "Ready on another job: Paladin"),
         ("ready-on-another-job-bard.svg", "Ready on another job: Bard"), ("ready-on-another-job-white-mage.svg", "Ready on another job: White Mage"),
         ("completed.svg", "Completed, gilt check"), ("completed-green.svg", "Completed, green check")]


def uri(p):
    return "data:image/svg+xml;base64," + base64.b64encode(p.read_bytes()).decode()


folder = pathlib.Path(sys.argv[1]).resolve()
out = pathlib.Path(sys.argv[2]).resolve()
rows = []
for fn, label in NAMES:
    u = uri(folder / fn)
    night = "".join(f"<img src='{u}' width={s} height={s}>" for s in (16, 20, 32, 64, 128))
    day = "".join(f"<img src='{u}' width={s} height={s}>" for s in (20, 64))
    grey = "".join(f"<img src='{u}' width={s} height={s} style='filter:grayscale(1)'>" for s in (20, 64))
    rows.append(f"<tr><td class=l>{label}</td><td class=d>{night}</td><td class=b>{day}</td><td class=d>{grey}</td></tr>")
# a quest-list mock at row size (16 and 20 px) so the badge and the checks are judged in context
# Row-size fallback for the job badge: below 32 px the badge is only a role-colour pip, so the row also draws the game's
# own job icon at text height right after the medal (FFXIV players read these at party-list size).
def png(job):
    return "data:image/png;base64," + base64.b64encode((folder / "_src" / "jobs" / f"{job}.png").read_bytes()).decode()


lst = []
for s in (16, 20):
    items = []
    for fn, label in NAMES:
        job = fn[len("ready-on-another-job-"):-4] if fn.startswith("ready-on-another-job-") else None
        tag = f"<img src='{png(job)}' width={s} height={s} style='margin-right:6px'>" if job else ""
        items.append(f"<div class=it><img src='{uri(folder / fn)}' width={s} height={s}>{tag}<span>{label.split(': ')[-1] if job else label}</span></div>")
    lst.append(f"<div class=list><b>{s} px rows (job icon fallback)</b>{''.join(items)}</div>")
html = f"""<html><head><meta charset=utf-8><style>
body{{margin:0;background:#0F1424;color:#E7E9F2;font:13px Segoe UI,sans-serif;padding:16px;width:1180px}}
table{{border-collapse:collapse}} td{{padding:6px 10px;border-bottom:1px solid #2A3150}} td.l{{width:220px}}
td.d{{background:#0B0F1C}} td.b{{background:#E9E4D6}} img{{vertical-align:middle;margin-right:14px}}
.lists{{display:flex;gap:30px;margin-top:16px}} .list{{background:#0B0F1C;padding:10px 14px;border-radius:6px;display:grid;gap:4px}}
.it{{display:flex;align-items:center;gap:0}} .it img{{margin-right:8px}}
</style></head><body><h3>{folder.name}: job badge and Completed variants</h3>
<table><tr><td></td><td>Night 16 · 20 · 32 · 64 · 128</td><td>Daylight 20 · 64</td><td>Greyscale 20 · 64</td></tr>{''.join(rows)}</table>
<div class=lists>{''.join(lst)}</div></body></html>"""
page = out.with_suffix(".html")
page.write_text(html, encoding="utf-8")
subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--screenshot={out}",
                "--window-size=1212,1250", page.as_uri()], check=True, capture_output=True)
print(out)
