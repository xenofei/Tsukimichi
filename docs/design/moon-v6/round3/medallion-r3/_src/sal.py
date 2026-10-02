import base64, pathlib, subprocess, sys, importlib.util
import numpy as np
from PIL import Image
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
S = pathlib.Path(__file__).parent
def lin(c): return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def unlin(c): return np.where(c<=0.0031308,12.92*c,1.055*c**(1/2.4)-0.055)
def luma(rgb): l=lin(rgb); return unlin(0.2126*l[...,0]+0.7152*l[...,1]+0.0722*l[...,2])
def sal(svgs, size=16):
    CELL=40
    imgs="".join(f"<div style='position:absolute;left:{i*CELL}px;top:0;width:{CELL}px;height:{CELL}px;display:flex;align-items:center;justify-content:center'><img src='data:image/svg+xml;base64,{base64.b64encode(s.encode()).decode()}' width={size} height={size}></div>" for i,s in enumerate(svgs))
    p=S/'sal.html'; p.write_text(f"<html><body style='margin:0;background:#0F1424'>{imgs}</body></html>",encoding='utf-8')
    out=S/'sal.png'
    subprocess.run([CHROME,"--headless=new","--disable-gpu","--hide-scrollbars","--force-device-scale-factor=1",f"--screenshot={out}",f"--window-size={CELL*len(svgs)},{CELL}",p.as_uri()],check=True,capture_output=True)
    img=luma(np.asarray(Image.open(out).convert('RGB'),dtype=float)/255)
    bg=img[0,0]
    return [round(float(np.abs(img[:,i*CELL:(i+1)*CELL]-bg).sum()),1) for i in range(len(svgs))]
