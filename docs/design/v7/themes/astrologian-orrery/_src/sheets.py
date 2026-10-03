"""Renders _mix.png (the swap both ways) and _kit.png (the Astrolabe kit) with headless Chrome.
Usage: python sheets.py   (after gen.py)"""
import base64, pathlib, subprocess
OUT = pathlib.Path(__file__).resolve().parent.parent
MED = OUT.parents[2] / "moon-v6" / "round5" / "medallion-r5"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
STATES = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"]
CSS = ("body{margin:0;background:#0F1424;color:#E7E9F2;font:13px Segoe UI,sans-serif;padding:16px}h1{font-size:16px;margin:0 0 10px}"
       "table{border-collapse:collapse}td{padding:6px 10px;border-bottom:1px solid #2A3150;vertical-align:middle}td.l{width:230px}"
       "td.d{background:#0B0F1C}img{vertical-align:middle;margin-right:8px}small{color:#A9B2CC}")


def uri(p):
    return "data:image/svg+xml;base64," + base64.b64encode(p.read_bytes()).decode()


def shoot(name, html, w, h):
    page = OUT / "_src" / f"{name}.html"
    page.write_text(html, encoding="utf-8")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--screenshot={OUT / (name + '.png')}",
                    f"--window-size={w},{h}", page.as_uri()], check=True, capture_output=True)
    page.unlink()


def strip(folder, row_folder, sizes=(96,), small=(20,)):
    big = "".join(f"<img src='{uri(folder / (s + '.svg'))}' width={z} height={z}>" for z in sizes for s in STATES)
    sm = "".join(f"<img src='{uri(row_folder / (s + '.svg'))}' width={z} height={z}>" for z in small for s in STATES)
    return f"<td class=d>{big}</td><td class=d>{sm}</td>"


def mix():
    rows = [("Orrery faces, Astrolabe kit<br><small>the theme as designed</small>", OUT, OUT / "_row"),
            ("Orrery faces, Brass kit<br><small>Medallion's frames and badges</small>", OUT / "_mix/orrery-in-brass", OUT / "_mix/orrery-in-brass/_row"),
            ("Medallion faces, Astrolabe kit<br><small>the swap the other way</small>", OUT / "_mix/medallion-in-astrolabe", OUT / "_mix/medallion-in-astrolabe/_row"),
            ("Medallion r5 as shipped<br><small>reference</small>", MED, MED / "_row")]
    body = "".join(f"<tr><td class=l>{l}</td>{strip(a, b)}</tr>" for l, a, b in rows)
    html = (f"<html><head><meta charset=utf-8><style>{CSS}</style></head><body><h1>astrologian-orrery: mix check "
            f"(Ready, RoJ, In journal, Blocked, Done, Completed, Locked out, Not checked)</h1>"
            f"<table><tr><td></td><td>96 px</td><td>20 px row tier</td></tr>{body}</table></body></html>")
    shoot("_mix", html, 1460, 560)


def kit():
    k = OUT / "kit"
    tiers = ["act-now", "resting", "finished", "ghost"]
    cells = "".join(f"<tr><td class=l>{t}</td><td class=d><img src='{uri(k / f'frame-{t}-full.svg')}' width=128 height=128>"
                    f"<img src='{uri(k / 'row' / f'frame-{t}-full.svg')}' width=28 height=28>"
                    f"<img src='{uri(k / f'frame-{t}-quiet.svg')}' width=128 height=128></td></tr>" for t in tiers)
    # each badge part is drawn at its slot (95, 95, r 24): show that region, 2x
    badges = "".join(f"<div style='display:inline-block;width:100px;height:100px;overflow:hidden;position:relative;margin-right:8px'>"
                     f"<img src='{uri(k / n)}' width=256 height=256 style='position:absolute;left:-140px;top:-140px'></div>" for n in
                     ("badge-ring-act-now.svg", "badge-ring.svg", "badge-seat-open.svg", "badge-seat-closed.svg",
                      "badge-seat-journal.svg", "badge-open.svg", "badge-closed.svg", "badge-journal.svg"))
    html = (f"<html><head><meta charset=utf-8><style>{CSS}</style></head><body><h1>Astrolabe kit: frames (Full hero, Full row at 28, Quiet) "
            f"and badge parts</h1><table>{cells}<tr><td class=l>badge rings, seats, glyphs</td><td class=d>{badges}</td></tr></table></body></html>")
    shoot("_kit", html, 820, 860)


def x6():
    """The six moon states as composites at true 48 px (device scale 1), enlarged 6x with nearest-neighbour: the check
    that no hatching reaches the 48 px tier."""
    from PIL import Image
    six = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed"]
    imgs = "".join(f"<img src='{uri(OUT / (s + '.svg'))}' width=48 height=48 style='position:absolute;left:{8 + i * 56}px;top:8px'>" for i, s in enumerate(six))
    tmp = OUT / "_src" / "_48.png"
    page = OUT / "_src" / "_48.html"
    page.write_text(f"<html><body style='margin:0;background:#0F1424'>{imgs}</body></html>", encoding="utf-8")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1", f"--screenshot={tmp}",
                    f"--window-size={8 + 56 * 6},64", page.as_uri()], check=True, capture_output=True)
    page.unlink()
    im = Image.open(tmp).convert("RGB")
    im.resize((im.width * 6, im.height * 6), Image.NEAREST).save(OUT / "_48x6.png")
    tmp.unlink()


if __name__ == "__main__":
    mix()
    kit()
    x6()
    print("ok")
