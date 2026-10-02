"""Measure G7 numbers on the 512 master: moon vs glade colour (OKLCH), brightness, earthshine ratio."""
import base64, pathlib, subprocess, math, sys
import numpy as np
from PIL import Image
sys.path.insert(0, str(pathlib.Path(__file__).parent))
import icon
F = pathlib.Path(r"C:/Users/devon/Desktop/Tsukimichi (Main Repo)/docs/design/moon-v6/round2/astrologian-orrery/plugin-icon.svg")
S = pathlib.Path(__file__).parent
html = S / "phys.html"
html.write_text(f"<html><body style='margin:0;background:#000'><img src='data:image/svg+xml;base64,{base64.b64encode(F.read_bytes()).decode()}' width=512 height=512></body></html>")
out = S / "phys.png"
subprocess.run([r"C:\Program Files\Google\Chrome\Application\chrome.exe", "--headless=new", "--disable-gpu", "--hide-scrollbars",
                "--force-device-scale-factor=1", f"--screenshot={out}", "--window-size=512,512", html.as_uri()], check=True, capture_output=True)
img = np.asarray(Image.open(out).convert("RGB"), dtype=float) / 255


def lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def oklab(rgb):
    l = lin(rgb)
    M1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929], [0.2119034982, 0.6806995451, 0.1073969566], [0.0883024619, 0.2817188376, 0.6299787005]])
    lms = np.cbrt(l @ M1.T)
    M2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468], [1.9779984951, -2.4285922050, 0.4505937099], [0.0259040371, 0.7827717662, -0.8086757660]])
    return lms @ M2.T


def lum(rgb):
    l = lin(rgb); return 0.2126 * l[..., 0] + 0.7152 * l[..., 1] + 0.0722 * l[..., 2]


yy, xx = np.mgrid[0:512, 0:512] + 0.5
MX, MY, MR, MT = icon.MX, icon.MY, icon.MR, icon.MT
disc = (xx - MX) ** 2 + (yy - MY) ** 2 <= (MR - 3) ** 2
th = math.radians(-icon.ROT)
xx0, yy0 = xx, yy
xx = MX + (xx0 - MX) * math.cos(th) - (yy0 - MY) * math.sin(th)
yy = MY + (xx0 - MX) * math.sin(th) + (yy0 - MY) * math.cos(th)
a = MT * MR
# lit region of a gibbous lit on the right: right half of disc, or inside the terminator ellipse on the left
lit = disc & ((xx >= MX) | (((xx - MX) / a) ** 2 + ((yy - MY) / MR) ** 2 <= 1))
lit &= ~(((xx - MX) / max(a, 1)) ** 2 + ((yy - MY) / MR) ** 2 <= 1.0) | (xx >= MX)  # keep
lit_core = lit & (((xx - MX) / (a + 8)) ** 2 + ((yy - MY) / (MR)) ** 2 > 1) | (lit & (xx > MX + 8))
dark = disc & ~((xx >= MX - a - 6))
xx, yy = xx0, yy0
ring_sky = ((xx - MX) ** 2 + (yy - MY) ** 2 >= (MR + 6) ** 2) & ((xx - MX) ** 2 + (yy - MY) ** 2 <= (MR + 16) ** 2) & (xx < MX - 20) & (yy < MY)
road_m = np.zeros_like(disc)
for x, y, w, h, op in icon.road():
    road_m |= (xx >= x + h / 2) & (xx <= x + w - h / 2) & (yy >= y + 2) & (yy <= y + h - 2)
road_m &= (xx - 256) ** 2 + (yy - 256) ** 2 <= 218 ** 2


def stats(mask):
    lab = oklab(img[mask]).mean(0)
    C = math.hypot(lab[1], lab[2]); hue = math.degrees(math.atan2(lab[2], lab[1])) % 360
    return lab[0], C, hue


ml, mc, mh = stats(lit)
gl, gc, gh = stats(road_m)
print(f"moon  OKLCH L={ml:.3f} C={mc:.4f} h={mh:.0f}")
print(f"glade OKLCH L={gl:.3f} C={gc:.4f} h={gh:.0f}   dC={gc - mc:+.4f} dh={((gh - mh + 180) % 360) - 180:+.0f}")
print(f"max L: moon {oklab(img[lit])[:, 0].max():.3f}  glade {oklab(img[road_m])[:, 0].max():.3f}")
e = lum(img[dark]).mean(); s = lum(img[ring_sky]).mean()
print(f"earthshine ratio {(e + .05) / (s + .05):.2f}  (dark {e:.4f} vs sky {s:.4f})")
cols = [(x + w / 2, w) for x, y, w, h, op in icon.road()]
cx = sum(c * w for c, w in cols) / sum(w for c, w in cols)
print(f"disc centre x {MX}, lit centroid x {icon.LIT_CX:.1f}, road axis {icon.AXIS}, weighted road centre {cx:.1f}, horizon {icon.H}, spec y {icon.SPEC}, glints {len(cols)}")
