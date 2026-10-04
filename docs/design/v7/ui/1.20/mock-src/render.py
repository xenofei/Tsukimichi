"""Renders the 1.20 boards from ../mock-1.20.html with headless Chrome, then trims each PNG to its content so the long
edge stays at most 1568 px (the design-doc limit). Run: py -3 render.py [view ...]"""
import pathlib
import subprocess
import sys

SRC = pathlib.Path(__file__).resolve().parent
OUT = SRC.parent
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
# view -> (png name, window width, window height)
SHOTS = {
    "shield20": ("shield-1.20.png", 1560, 1240),
    "prep20": ("prep-card-1.20.png", 1560, 1000),
    "pack20": ("portrait-pack-1.20.png", 1560, 1240),
    "looks20": ("looks-1.20.png", 1560, 1568),
    "looks20b": ("looks-new-1.20.png", 1560, 1290),
}
page = (OUT / "mock-1.20.html").as_uri()
for view in sys.argv[1:] or list(SHOTS):
    name, w, h = SHOTS[view]
    png = OUT / name
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                    "--virtual-time-budget=6000", f"--window-size={w},{h}", f"--screenshot={png}", f"{page}#{view}"],
                   check=True, capture_output=True)
    print(name, w, h)
