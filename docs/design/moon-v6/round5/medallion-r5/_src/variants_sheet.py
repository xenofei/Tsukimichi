"""Contact sheet for round 5's badges and variants: every badge-carrying medal (Ready, the three job variants,
In journal and Blocked) at 16, 20, 32, 64 and 128 px on the plugin's Night ground, plus daylight and
greyscale at 32 and 64 px; a 16 and 20 px quest-list mock using the badge-less row tier (_row/) with the badge content
at text height; and the plugin icon at 32, 64, 128 and 256 px.

Usage: python variants_sheet.py <folder> <out.png>
"""
import base64, pathlib, subprocess, sys

CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
NAMES = [("ready.svg", "Ready: open lock"), ("ready-on-another-job-paladin.svg", "Ready on another job: Paladin"),
         ("ready-on-another-job-bard.svg", "Ready on another job: Bard"), ("ready-on-another-job-white-mage.svg", "Ready on another job: White Mage"),
         ("in-journal.svg", "In journal: journal"), ("blocked.svg", "Blocked: closed lock (new moon behind cloud)")]
# what the row draws at text height beside the badge-less medal: (row-tier medal, badge content image or None, label)
ROWS = [("ready.svg", "badge-open", "Ready"), ("ready-on-another-job.svg", "paladin", "Paladin"), ("in-journal.svg", "badge-journal", "In journal"),
        ("blocked.svg", "badge-closed", "Blocked"), ("done-this-cycle.svg", None, "Done this cycle"), ("completed.svg", None, "Completed"),
        ("locked-out.svg", None, "Locked out"), ("not-checked.svg", None, "Not checked")]


def uri(p):
    return "data:image/svg+xml;base64," + base64.b64encode(p.read_bytes()).decode()


folder = pathlib.Path(sys.argv[1]).resolve()
out = pathlib.Path(sys.argv[2]).resolve()
rows = []
for fn, label in NAMES:
    u = uri(folder / fn)
    night = "".join(f"<img src='{u}' width={s} height={s}>" for s in (16, 20, 32, 64, 128))
    day = "".join(f"<img src='{u}' width={s} height={s}>" for s in (32, 64))
    grey = "".join(f"<img src='{u}' width={s} height={s} style='filter:grayscale(1)'>" for s in (32, 64))
    rows.append(f"<tr><td class=l>{label}</td><td class=d>{night}</td><td class=b>{day}</td><td class=d>{grey}</td></tr>")


def png(job):
    return "data:image/png;base64," + base64.b64encode((folder / "_src" / "jobs" / f"{job}.png").read_bytes()).decode()


lst = []
for s in (16, 20):
    items = []
    for fn, job, label in ROWS:
        src = (uri(folder / "_row" / f"{job}.svg") if job and job.startswith("badge-") else png(job)) if job else None
        tag = f"<img src='{src}' width={s} height={s} style='margin-right:6px'>" if job else ""
        items.append(f"<div class=it><img src='{uri(folder / '_row' / fn)}' width={s} height={s}>{tag}<span>{label}</span></div>")
    lst.append(f"<div class=list><b>{s} px rows (row tier, no badge)</b>{''.join(items)}</div>")
iu = uri(folder / "plugin-icon.svg")
icons = "".join(f"<div class=ib><img src='{iu}' width={s} height={s}><span>{s}</span></div>" for s in (32, 64, 128, 256))
icons += "".join(f"<div class=il><img src='{iu}' width={s} height={s}><span>{s} light</span></div>" for s in (64, 128))
html = f"""<html><head><meta charset=utf-8><style>
body{{margin:0;background:#0F1424;color:#E7E9F2;font:13px Segoe UI,sans-serif;padding:16px;width:1180px}}
table{{border-collapse:collapse}} td{{padding:6px 10px;border-bottom:1px solid #2A3150}} td.l{{width:210px}}
td.d{{background:#0B0F1C}} td.b{{background:#E9E4D6}} img{{vertical-align:middle;margin-right:14px}}
.lists{{display:flex;gap:30px;margin-top:16px;align-items:flex-start}} .list{{background:#0B0F1C;padding:10px 14px;border-radius:6px;display:grid;gap:4px}}
.it{{display:flex;align-items:center}} .it img{{margin-right:8px}}
.icons{{display:flex;gap:18px;align-items:flex-end;margin-top:16px}} .ib,.il{{display:grid;justify-items:center;gap:4px;padding:10px;border-radius:8px}}
.ib{{background:#1E2235}} .il{{background:#F2F2F2;color:#333}}
</style></head><body><h3>{folder.name}: badges, job variants and the icon</h3>
<table><tr><td></td><td>Night 16 · 20 · 32 · 64 · 128</td><td>Daylight 32 · 64</td><td>Greyscale 32 · 64</td></tr>{''.join(rows)}</table>
<div class=lists>{''.join(lst)}</div><div class=icons>{icons}</div></body></html>"""
page = out.with_suffix(".html")
page.write_text(html, encoding="utf-8")
subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--screenshot={out}",
                "--window-size=1212,2050", page.as_uri()], check=True, capture_output=True)
page.unlink()
print(out)
