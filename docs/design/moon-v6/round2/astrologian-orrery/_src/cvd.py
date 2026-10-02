"""Extra CVD check: protanopia (Vienot) and tritanopia (Machado 1.0) + greyscale, weakest pairs at 16/20."""
import itertools, sys, pathlib
import numpy as np
from PIL import Image, ImageFilter
S = pathlib.Path(__file__).parent
SHORT = ["Rdy","RoJ","Jrn","Blk","Done","Comp","Lock","NotC"]
def lin(c): return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def unlin(c): return np.where(c<=0.0031308,12.92*c,1.055*np.clip(c,0,None)**(1/2.4)-0.055)
P = np.array([[0.11238,0.88762,0],[0.11238,0.88762,0],[0.00401,-0.00401,1]])
T = np.array([[1.255528,-0.076749,-0.178779],[-0.078411,0.930809,0.147602],[0.004733,0.691367,0.303900]])
def sim(rgb,M): return unlin(np.clip(lin(rgb)@M.T,0,1))
def luma(rgb): l=lin(rgb); return unlin(0.2126*l[...,0]+0.7152*l[...,1]+0.0722*l[...,2])
def dist(a,b):
    A=np.asarray(Image.fromarray((a*255).astype('uint8')).filter(ImageFilter.GaussianBlur(0.6)),dtype=float)/255
    B=np.asarray(Image.fromarray((b*255).astype('uint8')).filter(ImageFilter.GaussianBlur(0.6)),dtype=float)/255
    return np.abs(A-B).sum()
for size in (16,20):
    img=np.asarray(Image.open(S/f"m_orrery_{size}.png").convert('RGB'),dtype=float)/255
    for name,M in (("prot+grey",P),("trit+grey",T)):
        g=luma(sim(img,M)); c=[g[:,i*40:(i+1)*40] for i in range(8)]
        D={(i,j):dist(c[i],c[j]) for i,j in itertools.combinations(range(8),2)}
        w=sorted(D.items(),key=lambda kv:kv[1])[:3]
        print(f"orrery {size}px {name}: weakest pairs "+", ".join(f"{SHORT[i]}-{SHORT[j]} {v:.1f}" for (i,j),v in w))
