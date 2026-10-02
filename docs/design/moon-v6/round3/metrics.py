"""Baseline metrics for round-1 glyph sets: pairwise distinctness at 16/20 px (greyscale, deuteranopia), salience."""
import base64, pathlib, subprocess, sys, itertools
import numpy as np
from PIL import Image, ImageFilter
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
STATES = ["ready","ready-on-another-job","in-journal","blocked","done-this-cycle","completed","locked-out","not-checked"]
SHORT = ["Rdy","RoJ","Jrn","Blk","Done","Comp","Lock","NotC"]
CELL = 40
def render(folder, size, out, bg="#0F1424"):
    imgs = "".join(f"<div style='position:absolute;left:{i*CELL}px;top:0;width:{CELL}px;height:{CELL}px;display:flex;align-items:center;justify-content:center'><img src='data:image/svg+xml;base64,{base64.b64encode((folder/(s+'.svg')).read_bytes()).decode()}' width={size} height={size}></div>" for i,s in enumerate(STATES))
    html = f"<html><body style='margin:0;background:{bg}'>{imgs}</body></html>"
    p = out.with_suffix('.html'); p.write_text(html, encoding='utf-8')
    subprocess.run([CHROME,"--headless=new","--disable-gpu","--hide-scrollbars","--force-device-scale-factor=1",f"--screenshot={out}",f"--window-size={CELL*8},{CELL}",p.as_uri()],check=True,capture_output=True)
    return np.asarray(Image.open(out).convert('RGB'),dtype=float)/255
def lin(c): return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def unlin(c): return np.where(c<=0.0031308,12.92*c,1.055*c**(1/2.4)-0.055)
def deut(rgb):
    l=lin(rgb); M=np.array([[0.29275,0.70725,0],[0.29275,0.70725,0],[-0.02234,0.02234,1]])  # Vienot 1999 deuteranope
    return unlin(np.clip(l@M.T,0,1))
def luma(rgb): l=lin(rgb); return unlin(0.2126*l[...,0]+0.7152*l[...,1]+0.0722*l[...,2])
def cells(img): return [img[:, i*CELL:(i+1)*CELL] for i in range(8)]
def dist(a,b):
    A=np.asarray(Image.fromarray((a*255).astype('uint8')).filter(ImageFilter.GaussianBlur(0.6)),dtype=float)/255
    B=np.asarray(Image.fromarray((b*255).astype('uint8')).filter(ImageFilter.GaussianBlur(0.6)),dtype=float)/255
    return np.abs(A-B).sum()  # summed luminance difference, in "full-contrast pixels"
folder=pathlib.Path(sys.argv[1]); tag=sys.argv[2]; S=pathlib.Path(sys.argv[3])
for size in (16,20):
    img=render(folder,size,S/f"m_{tag}_{size}.png")
    for mode,f in (("grey",luma),("deut+grey",lambda x: luma(deut(x)))):
        c=cells(f(img)); bg=c[0][0,0]
        D={(i,j):dist(c[i],c[j]) for i,j in itertools.combinations(range(8),2)}
        worst=sorted(D.items(),key=lambda kv:kv[1])[:3]
        sal=[np.abs(x-bg).sum() for x in c]
        print(f"{tag} {size}px {mode}: weakest pairs "+", ".join(f"{SHORT[i]}-{SHORT[j]} {v:.1f}" for (i,j),v in worst)
              +" | salience "+" ".join(f"{SHORT[k]}={s:.0f}" for k,s in enumerate(sal)))
