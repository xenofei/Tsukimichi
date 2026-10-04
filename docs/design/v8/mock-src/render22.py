"""Renders the 1.22 boards of ../mock-1.22.html to PNGs in ../ with headless Chrome, crops the empty page below the
board, and keeps the long edge at 1568 px or less. Run: py -3 render22.py [view ...]"""
import pathlib
import subprocess
import sys
import tempfile

from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
# view: (png name, window width)
VIEWS = {
    "wn22a": ("whatsnew-evercold-1.22.png", 1560),
    "wn22b": ("whatsnew-answers-1.22.png", 1560),
    "wn22s": ("whatsnew-states-1.22.png", 1560),
    "art22": ("release-art-1.22.png", 1460),
    "optb22": ("release-art-option-b-1.22.png", 1420),
    "about22": ("about-history-1.22.png", 1300),
    "upd22": ("update-ready-1.22.png", 1400),
    "icon22": ("moon-icon-1.22.png", 1420),
    "fx22": ("icon-particles-1.22.png", 1420),
    "dtr22": ("server-info-bar-1.22.png", 1340),
    "umb22": ("umbra-widgets-1.22.png", 1360),
    "clear22": ("umbra-clearance-1.22.png", 1420),
}
LONG = 1568


def crop_bottom(im):
    w, h = im.size
    px = im.load()
    for y in range(h):
        for x in (20, 40, w // 3, w // 2):
            r, g, b = px[x, y]
            if r > 240 and g < 20 and b > 240:
                return im.crop((0, 0, w, y))
    raise SystemExit("no end marker: make the window taller")


def shoot(view):
    name, w = VIEWS[view]
    url = (OUT / "mock-1.22.html").as_uri() + "?shot#" + view
    with tempfile.TemporaryDirectory() as tmp:
        raw = pathlib.Path(tmp) / "shot.png"
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                        "--allow-file-access-from-files", "--virtual-time-budget=6000", f"--window-size={w},3400",
                        f"--screenshot={raw}", url], check=True, capture_output=True, timeout=180)
        im = Image.open(raw).convert("RGB")
        im = crop_bottom(im)
        if max(im.size) > LONG:
            k = LONG / max(im.size)
            im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
        im.save(OUT / name, optimize=True)
        print(name, im.size)


if __name__ == "__main__":
    for v in sys.argv[1:] or VIEWS:
        shoot(v)
