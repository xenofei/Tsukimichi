import base64, pathlib, subprocess, sys
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
F = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6\round2\menphina-medallion")
S = pathlib.Path(__file__).parent
def u(n): return "data:image/svg+xml;base64," + base64.b64encode((F / n).read_bytes()).decode()
states = ["ready","ready-on-another-job","in-journal","blocked","done-this-cycle","completed","locked-out","not-checked"]
# check overlay approx: installed check at x31-61,y39-61 of 64
chk = "<div style='position:absolute;left:%dpx;top:%dpx;width:%dpx;height:%dpx;border-radius:50%%;background:rgba(80,200,80,.55)'></div>"
html = "<html><body style='margin:0;background:#1E2235;width:1400px;height:900px;position:relative'>"
html += f"<img src='{u('plugin-icon.svg')}' width=512 height=512 style='position:absolute;left:10px;top:10px'>"
html += "<div style='position:absolute;left:540px;top:10px;width:64px;height:64px'>" + f"<img src='{u('plugin-icon.svg')}' width=64 height=64>" + "<div style='position:absolute;left:31px;top:39px;width:30px;height:22px;background:rgba(40,160,60,.85);border-radius:4px'></div></div>"
html += "<div style='position:absolute;left:620px;top:10px;width:64px;height:64px;opacity:.4'>" + f"<img src='{u('plugin-icon.svg')}' width=64 height=64></div>"
for i, s in enumerate(states):
    html += f"<img src='{u(s+'.svg')}' width=128 height=128 style='position:absolute;left:{540 + (i%4)*140}px;top:{100 + (i//4)*140}px;background:#0F1424'>"
for i, s in enumerate(states):
    html += f"<img src='{u(s+'.svg')}' width=32 height=32 style='position:absolute;left:{540 + i*44}px;top:400px'>"
    html += f"<img src='{u(s+'.svg')}' width=24 height=24 style='position:absolute;left:{540 + i*44}px;top:440px'>"
html += "</body></html>"
p = S / "zoom.html"; p.write_text(html, encoding="utf-8")
subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1", f"--screenshot={S / 'zoom.png'}", "--window-size=1150,540", p.as_uri()], check=True, capture_output=True)
print(S / "zoom.png")
