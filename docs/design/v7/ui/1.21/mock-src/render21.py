"""Renders the 1.21 boards of ../mock-1.21.html to PNGs in ../ with headless Chrome, crops the empty page below the
board, and keeps the long edge at 1568 px or less. Run: py -3 render21.py [view ...]"""
import pathlib
import subprocess
import sys
import tempfile

from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
# view: (png name, window width, window height)
VIEWS = {
    "tonight21": ("upnext-1.21.png", 1400, 3000),
    "step21": ("current-step-1.21.png", 1400, 3000),
    "roster21": ("roster-1.21.png", 1560, 3000),
    "blues21": ("blues-1.21.png", 1560, 3000),
    "stories21": ("stories-1.21.png", 1560, 3000),
    "boards21": ("boards-1.21.png", 1560, 3000),
    "chat21": ("chat-1.21.png", 1460, 3000),
    "looks21": ("looks-1.21.png", 1760, 3000),
}
LONG = 1568


def crop_bottom(im):
    # In ?shot mode the board ends in a 6 px magenta rule (.end21); crop just above it, keeping a 12 px margin.
    w, h = im.size
    px = im.load()
    x = 40
    for y in range(h):
        r, g, b = px[x, y]
        if r > 240 and g < 20 and b > 240:
            return im.crop((0, 0, w, y))
    raise SystemExit("no end marker: make the window taller")


def shoot(view):
    name, w, h = VIEWS[view]
    url = (OUT / "mock-1.21.html").as_uri() + "?shot#" + view
    with tempfile.TemporaryDirectory() as tmp:
        raw = pathlib.Path(tmp) / "shot.png"
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                        "--allow-file-access-from-files", "--virtual-time-budget=6000", f"--window-size={w},{h}",
                        f"--screenshot={raw}", url], check=True, capture_output=True, timeout=120)
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
