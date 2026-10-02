import base64, pathlib, subprocess, sys
import numpy as np
from PIL import Image
sys.path.insert(0, str(pathlib.Path(__file__).parent))
import gen
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
S = pathlib.Path(__file__).parent
svg = (gen.OUT / "plugin-icon.svg").read_bytes()
p = S / "phys.html"; p.write_text(f"<html><body style='margin:0;background:#000'><img src='data:image/svg+xml;base64,{base64.b64encode(svg).decode()}' width=512 height=512></body></html>")
out = S / "phys.png"
subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1", f"--screenshot={out}", "--window-size=512,512", p.as_uri()], check=True, capture_output=True)
img = np.asarray(Image.open(out).convert("RGB"), dtype=float) / 255
def lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def oklab(rgb):
    l = lin(rgb)
    M1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929], [0.2119034982, 0.6806995451, 0.1073969566], [0.0883024619, 0.2817188376, 0.6299787005]])
    lms = np.cbrt(l @ M1.T)
    M2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468], [1.9779984951, -2.4285922050, 0.4505937099], [0.0259040371, 0.7827717671, -0.8086757660]])
    return lms @ M2.T
lab = oklab(img)
L, a, b = lab[..., 0], lab[..., 1], lab[..., 2]
C = np.hypot(a, b); h = np.degrees(np.arctan2(b, a)) % 360
ys, xs = np.mgrid[0:512, 0:512] + 0.5
mx, my, mr, mk, rot = gen.ICON_MOON
moon = gen.lit_mask_np(mx, my, mr - 4, mk * 1.15 if mk < 0 else mk, "right", rot, xs, ys)
H = gen.ICON_H
sea = (ys > H + 2) & (ys < 497)
# road pixels: bright pixels in the sea within the road band
road = sea & (L > 0.55) & (np.abs(xs - gen.ROAD_IX) < 110)
def stats(m):
    return L[m].mean(), np.median(C[m]), np.degrees(np.arctan2(np.median(b[m]), np.median(a[m]))) % 360
Lm, Cm, hm = stats(moon); Lr, Cr, hr = stats(road)
print(f"moon  L={Lm:.3f} C={Cm:.4f} h={hm:.1f}  max L={L[moon].max():.3f}")
print(f"road  L={Lr:.3f} C={Cr:.4f} h={hr:.1f}  max L={L[road].max():.3f}  (pixels {road.sum()})")
w = L * road
print(f"road axis x={ (xs*w).sum()/w.sum():.1f}  lit centroid x={gen.LIT_C[0]:.1f}  disc centre x={mx}")
rows = road.sum(axis=1); yy = np.arange(512)
br = (L * road).sum(axis=1) / np.maximum(rows, 1)
print("brightest road row y", int(yy[np.argmax(br * (rows > 5))]))
# horizon brightness: sky just above vs sea just below, away from road and island
for x0, x1 in ((40, 120), (300, 330)):
    print(f"horizon x{x0}-{x1}: sky L={L[int(H)-6:int(H)-2, x0:x1].mean():.3f} sea L={L[int(H)+3:int(H)+7, x0:x1].mean():.3f}")
# earthshine: dark part of disc vs sky
dark = ((xs - mx) ** 2 + (ys - my) ** 2 < (mr - 4) ** 2) & ~gen.lit_mask_np(mx, my, mr + 4, mk * 0.8, "right", rot, xs, ys)
ring = ((xs - mx) ** 2 + (ys - my) ** 2 > (mr + 6) ** 2) & ((xs - mx) ** 2 + (ys - my) ** 2 < (mr + 14) ** 2) & (xs < mx)
def Y(m): return (0.2126 * lin(img[..., 0]) + 0.7152 * lin(img[..., 1]) + 0.0722 * lin(img[..., 2]))[m].mean()
print(f"earthshine contrast (dark disc vs sky) {(Y(dark) + .05) / (Y(ring) + .05):.3f}")
# rim vs #101010
rim = img[9:12, 200:300]
print("rim mean rgb", (rim.mean(axis=(0, 1)) * 255).round())
yl = 0.2126 * lin(rim[..., 0]) + 0.7152 * lin(rim[..., 1]) + 0.0722 * lin(rim[..., 2])
print(f"rim contrast vs #101010: {(yl.mean() + .05) / (lin(np.array(16 / 255)) + .05):.2f}")
# installed-check corner busy-ness (x 248-488, y 312-488): luminance std
corner = L[312:488, 248:488]
print(f"check corner L std {corner.std():.3f}, mean {corner.mean():.3f}")
