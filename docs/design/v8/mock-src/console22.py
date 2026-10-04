"""Prints the browser console errors of one view of ../mock-1.22.html (a debugging aid for the mock).
Run: py -3 console22.py <view>"""
import pathlib
import subprocess
import sys

OUT = pathlib.Path(__file__).resolve().parent.parent
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
url = (OUT / "mock-1.22.html").as_uri() + "?shot#" + sys.argv[1]
r = subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--allow-file-access-from-files", "--enable-logging=stderr", "--v=0",
                    "--virtual-time-budget=3000", "--dump-dom", url], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=120)
for line in r.stderr.splitlines():
    if "CONSOLE" in line or "Uncaught" in line:
        print(line[:400])
print("dom bytes", len(r.stdout), "end22" in r.stdout)
