import pathlib, sys, itertools
import numpy as np
from PIL import Image, ImageFilter
S = pathlib.Path(__file__).parent
SHORT = ["Rdy","RoJ","Jrn","Blk","Done","Comp","Lock","NotC"]
def lin(c): return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def unlin(c): return np.where(c<=0.0031308,12.92*c,1.055*c**(1/2.4)-0.055)
def luma(rgb): l=lin(rgb); return unlin(0.2126*l[...,0]+0.7152*l[...,1]+0.0722*l[...,2])
M = {"prot": np.array([[0.152286,1.052583,-0.204868],[0.114503,0.786281,0.099216],[-0.003882,-0.048116,1.051998]]),
     "trit": np.array([[1.255528,-0.076749,-0.178779],[-0.078411,0.930809,0.147602],[0.004733,0.691367,0.303900]])}
def sim(rgb, m): return unlin(np.clip(lin(rgb) @ m.T, 0, 1))
def dist(a,b):
    A=np.asarray(Image.fromarray((a*255).astype('uint8')).filter(ImageFilter.GaussianBlur(0.6)),dtype=float)/255
    B=np.asarray(Image.fromarray((b*255).astype('uint8')).filter(ImageFilter.GaussianBlur(0.6)),dtype=float)/255
    return np.abs(A-B).sum()
for size in (16, 20):
    img = np.asarray(Image.open(S / f"m_medallion_{size}.png").convert("RGB"), dtype=float) / 255
    for k, m in M.items():
        g = luma(sim(img, m)); c = [g[:, i*40:(i+1)*40] for i in range(8)]
        D = sorted(((dist(c[i], c[j]), SHORT[i] + "-" + SHORT[j]) for i, j in itertools.combinations(range(8), 2)))[:3]
        print(f"{size}px {k}+grey: " + ", ".join(f"{n} {v:.1f}" for v, n in D))
