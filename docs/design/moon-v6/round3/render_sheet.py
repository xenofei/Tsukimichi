"""Render a concept folder (8 state SVGs + plugin-icon.svg) into a PNG contact sheet with headless Chrome.

Usage: python render_sheet.py <concept-folder> [out.png]

The sheet shows each state at 16, 20, 28, 48 and 96 px on the plugin's Night ground, on a bright daylight swatch and
in greyscale, and the plugin icon at 32, 64, 128 and 256 px on dark and light installer backgrounds. Designers and
focus-group members look at the PNG (the Read tool shows images) instead of judging SVG source.
"""

import base64
import os
import pathlib
import subprocess
import sys

STATES = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"]
LABELS = {
    "ready": "Ready", "ready-on-another-job": "Ready on another job", "in-journal": "In journal", "blocked": "Blocked",
    "done-this-cycle": "Done this cycle", "completed": "Completed", "locked-out": "Locked out", "not-checked": "Not checked",
}
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"


def uri(path: pathlib.Path) -> str:
    return "data:image/svg+xml;base64," + base64.b64encode(path.read_bytes()).decode()


def main() -> None:
    folder = pathlib.Path(sys.argv[1]).resolve()
    out = pathlib.Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else folder / "_sheet.png"
    rows = []
    for key in STATES:
        svg = folder / f"{key}.svg"
        if not svg.exists():
            rows.append(f"<tr><td class=l>{LABELS[key]}</td><td colspan=3 style='color:#f66'>missing {key}.svg</td></tr>")
            continue
        u = uri(svg)
        dark = "".join(f"<img src='{u}' width={s} height={s}>" for s in (16, 20, 28, 48, 96))
        bright = "".join(f"<img src='{u}' width={s} height={s}>" for s in (20, 48))
        grey = "".join(f"<img src='{u}' width={s} height={s} style='filter:grayscale(1)'>" for s in (20, 48))
        rows.append(f"<tr><td class=l>{LABELS[key]}</td><td class=d>{dark}</td><td class=b>{bright}</td><td class=d>{grey}</td></tr>")
    icon = folder / "plugin-icon.svg"
    icons = ""
    if icon.exists():
        u = uri(icon)
        icons = "".join(f"<div class=ib><img src='{u}' width={s} height={s}><span>{s}</span></div>" for s in (32, 64, 128, 256))
        icons += "".join(f"<div class=il><img src='{u}' width={s} height={s}><span>{s} light</span></div>" for s in (64, 128))
    html = f"""<html><head><meta charset=utf-8><style>
body{{margin:0;background:#0F1424;color:#E7E9F2;font:13px Segoe UI,sans-serif;padding:16px;width:1180px}}
h1{{font-size:16px;margin:0 0 10px}} table{{border-collapse:collapse}} td{{padding:6px 10px;border-bottom:1px solid #2A3150}}
td.l{{width:170px}} td.d{{background:#0B0F1C}} td.b{{background:#E9E4D6}} img{{vertical-align:middle;margin-right:12px}}
.icons{{display:flex;gap:18px;align-items:flex-end;margin-top:16px;flex-wrap:wrap}} .ib,.il{{display:grid;justify-items:center;gap:4px;padding:10px;border-radius:8px}}
.ib{{background:#1E2235}} .il{{background:#F2F2F2;color:#333}}
</style></head><body><h1>{folder.name}</h1><table><tr><td></td><td>Night 16 · 20 · 28 · 48 · 96</td><td>Daylight</td><td>Greyscale</td></tr>{''.join(rows)}</table>
<div class=icons>{icons}</div></body></html>"""
    page = folder / "_sheet.html"
    page.write_text(html, encoding="utf-8")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--screenshot={out}",
                    "--window-size=1212,1700", page.as_uri()], check=True, capture_output=True)
    print(out)


if __name__ == "__main__":
    main()
