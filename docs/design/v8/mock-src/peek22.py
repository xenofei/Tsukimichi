"""Debugging aid: screenshots one view of ../mock-1.22.html uncropped and writes a third-size preview.
Run: py -3 peek22.py <view> <width> <out.png>"""
import subprocess
import sys

from PIL import Image

import render22

view, w, out = sys.argv[1], int(sys.argv[2]), sys.argv[3]
url = (render22.OUT / "mock-1.22.html").as_uri() + "?shot#" + view
subprocess.run([render22.CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1", "--allow-file-access-from-files",
                "--virtual-time-budget=6000", f"--window-size={w},3400", f"--screenshot={out}", url], check=True, capture_output=True, timeout=180)
im = Image.open(out)
im.resize((im.width // 3, im.height // 3)).save(out)
