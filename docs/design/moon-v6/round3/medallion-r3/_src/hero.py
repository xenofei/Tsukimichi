import base64, pathlib, subprocess
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
F = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6\round2\menphina-medallion")
S = pathlib.Path(__file__).parent
def u(n): return "data:image/svg+xml;base64," + base64.b64encode((F / n).read_bytes()).decode()
states = ["ready","ready-on-another-job","in-journal","blocked","done-this-cycle","completed","locked-out","not-checked"]
html = "<html><body style='margin:0;background:#0F1424'>"
for i, s in enumerate(states):
    html += f"<img src='{u(s+'.svg')}' width=256 height=256 style='position:absolute;left:{10+(i%4)*270}px;top:{10+(i//4)*270}px'>"
html += "</body></html>"
p = S / "hero.html"; p.write_text(html, encoding="utf-8")
subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1", f"--screenshot={S / 'hero.png'}", "--window-size=1100,560", p.as_uri()], check=True, capture_output=True)
