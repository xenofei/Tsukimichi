import base64, pathlib, subprocess, shutil
CHROME=r"C:\Program Files\Google\Chrome\Application\chrome.exe"
MV=pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6")
OUT=pathlib.Path(__file__).parent
STATES=["ready","ready-on-another-job","in-journal","blocked","done-this-cycle","completed","locked-out","not-checked"]
LABEL=["Ready","Ready, other job","In journal","Blocked","Done this cycle","Completed","Locked out","Not checked"]
def u(p): return "data:image/svg+xml;base64,"+base64.b64encode(p.read_bytes()).decode()
def shoot(name, body, w, h):
    page=OUT/"designs"/f"{name}.html"; page.write_text(f"<!doctype html><html><body style='margin:0;background:#0F1424;font-family:Segoe UI,sans-serif'>{body}</body></html>",encoding="utf-8")
    subprocess.run([CHROME,"--headless=new","--disable-gpu","--hide-scrollbars","--force-device-scale-factor=2",f"--screenshot={OUT/'designs'/(name+'.png')}",f"--window-size={w},{h}",page.as_uri()],check=True,capture_output=True); page.unlink()
for tag,d in (("r3",MV/"round3"/"medallion-r3"),("r2",MV/"round2"/"menphina-medallion")):
    art=OUT/"art"/tag; art.mkdir(parents=True,exist_ok=True)
    for s in STATES+["plugin-icon"]: shutil.copy(d/(s+".svg"), art/(s+".svg"))
    shoot(f"medallion-{tag}-icon", f"<div style='display:grid;place-items:center;width:300px;height:300px'><img src='{u(d/'plugin-icon.svg')}' width=256 height=256></div>",300,300)
    cells="".join(f"<div style='width:96px;display:flex;flex-direction:column;align-items:center;gap:6px'><img src='{u(d/(s+'.svg'))}' width=64 height=64><div style='display:flex;gap:6px;align-items:center'><img src='{u(d/(s+'.svg'))}' width=20 height=20><img src='{u(d/(s+'.svg'))}' width=16 height=16></div><div style='font-size:11px;color:#9aa3bb;text-align:center'>{l}</div></div>" for s,l in zip(STATES,LABEL))
    shoot(f"medallion-{tag}-glyphs", f"<div style='display:flex;gap:4px;padding:16px 12px'>{cells}</div>",824,150)
    print(tag)
