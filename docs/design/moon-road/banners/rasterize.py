# Rasterizes the Moon Road category banners (src/*.svg, from gen_banners.py) into the plugin's embedded assets.
#
# Chrome headless draws each SVG at its 2x pixel size (752 x 240); Pillow quantizes the result to a 256-colour
# palette with Floyd-Steinberg dithering (the night gradients stay smooth; the files stay around 40 KB) and writes
# Tsukimichi/assets/ui/banners/<slug>@2x.png. Only the 2x file ships: the hero draws it at 376 x 120 logical, so at
# UI scale 1 the GPU halves it (an exact 2:1 bilinear minification) and at 2x it is drawn pixel for pixel.
#
# cairo is not installed on the build machine, hence Chrome. Usage: python docs/design/moon-road/banners/rasterize.py
import os
import subprocess
import sys
import tempfile

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
SRC = os.path.join(HERE, "src")
DEST = os.path.join(REPO, "Tsukimichi", "assets", "ui", "banners")
W, H = 752, 240
COLOURS = 256

CHROME_CANDIDATES = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "/usr/bin/google-chrome",
    "/usr/bin/chromium",
]


def chrome():
    for path in CHROME_CANDIDATES:
        if os.path.exists(path):
            return path
    sys.exit("Chrome or Edge not found; set CHROME_CANDIDATES")


def screenshot(svg_path, png_path, width, height, transparent=False):
    url = "file:///" + svg_path.replace("\\", "/")
    args = [chrome(), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
            f"--window-size={width},{height}", f"--screenshot={png_path}", url]
    if transparent:
        args.insert(1, "--default-background-color=00000000")
    subprocess.run(args, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    im = Image.open(png_path)
    if im.size != (width, height):
        sys.exit(f"{svg_path}: Chrome rendered {im.size}, expected {(width, height)}")
    return im


def main():
    os.makedirs(DEST, exist_ok=True)
    total = 0
    with tempfile.TemporaryDirectory() as tmp:
        for name in sorted(os.listdir(SRC)):
            if not name.endswith(".svg"):
                continue
            slug = name[:-4]
            raw = screenshot(os.path.join(SRC, name), os.path.join(tmp, slug + ".png"), W, H).convert("RGB")
            q = raw.quantize(colors=COLOURS, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG)
            out = os.path.join(DEST, f"{slug}@2x.png")
            q.save(out, optimize=True)
            size = os.path.getsize(out)
            total += size
            print(f"{slug:28s} {W}x{H}  {size / 1024:6.1f} KB")
    print(f"total {total / 1024:.1f} KB")


if __name__ == "__main__":
    main()
