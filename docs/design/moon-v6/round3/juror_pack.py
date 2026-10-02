"""Render the round-2 focus-group pack: blind sheets, row mocks, hero comparison and icon-under-overlay comparison.

Usage: python juror_pack.py <out_dir>
Concept folders are the subfolders of this directory that contain ready.svg and plugin-icon.svg.
"""
import base64
import pathlib
import random
import subprocess
import sys

CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
HERE = pathlib.Path(__file__).parent
OUT = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else HERE / "_pack"
OUT.mkdir(parents=True, exist_ok=True)
STATES = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"]
LABEL = {"ready": "Ready", "ready-on-another-job": "Ready on another job", "in-journal": "In journal", "blocked": "Blocked",
         "done-this-cycle": "Done this cycle", "completed": "Completed", "locked-out": "Locked out", "not-checked": "Not checked"}
CONCEPTS = sorted(p for p in HERE.iterdir() if p.is_dir() and (p / "ready.svg").exists() and (p / "plugin-icon.svg").exists())
LETTER = {c.name: "ABCD"[i] for i, c in enumerate(CONCEPTS)}
FONT = "font-family:Segoe UI,system-ui,sans-serif"


def uri(path: pathlib.Path) -> str:
    return "data:image/svg+xml;base64," + base64.b64encode(path.read_bytes()).decode()


def shoot(name: str, body: str, w: int, h: int):
    page = OUT / f"{name}.html"
    page.write_text(f"<!doctype html><html><body style='margin:0;{FONT};color:#ddd;background:#16181d'>{body}</body></html>", encoding="utf-8")
    png = OUT / f"{name}.png"
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                    f"--screenshot={png}", f"--window-size={w},{h}", page.as_uri()], check=True, capture_output=True)
    print(png)


QUESTS = ["The Ultimate Weapon", "A Hint of Things to Come", "Hildibrand: The Case of the Crimson Crow", "Tales from the Shadows",
          "Return from the Void", "His Dark Materia", "Seeds of Despair", "An Unforeseen Bargain", "The Far Edge of Fate",
          "A Bedtime Tale", "Stormblood Main Scenario", "The Sea of Clouds", "Moonfire Faire", "Ties of Silver",
          "In the Belly of the Beast", "Shadowbringers Role Quest"]
MIX = ["ready", "in-journal", "completed", "blocked", "ready", "locked-out", "not-checked", "done-this-cycle", "completed",
       "ready-on-another-job", "completed", "blocked", "ready", "not-checked", "in-journal", "completed"]

# 1. Blind first-association sheets (no state labels, shuffled per concept), at 16 px and 48 px.
rng = random.Random(6)
blind_key = []
for size, cell in ((16, 44), (48, 84)):
    rows = ""
    for c in CONCEPTS:
        order = STATES[:]
        rng.shuffle(order) if size == 16 else None
        if size == 48:
            order = blind_key_orders[c.name]  # same shuffle as the 16 px sheet
        else:
            blind_key_orders = globals().setdefault("blind_key_orders", {})
            blind_key_orders[c.name] = order
            blind_key.append(f"Concept {LETTER[c.name]}: " + ", ".join(f"{i+1}={LABEL[s]}" for i, s in enumerate(order)))
        cells = "".join(f"<div style='width:{cell}px;text-align:center'><img src='{uri(c / (s + '.svg'))}' width={size} height={size}>"
                        f"<div style='font-size:11px;color:#8a90a0;margin-top:4px'>{i+1}</div></div>" for i, s in enumerate(order))
        rows += f"<div style='display:flex;align-items:center;gap:6px;padding:10px 16px;background:#0F1424'><b style='width:90px;color:#cfd3dd'>Concept {LETTER[c.name]}</b>{cells}</div>"
    shoot(f"blind_{size}px", f"<div style='display:flex;flex-direction:column;gap:2px;padding:12px'>{rows}</div>",
          140 + cell * 8 + 60, 40 + len(CONCEPTS) * (cell + 40))
(OUT / "blind_key.txt").write_text("\n".join(blind_key) + "\n", encoding="utf-8")

# 2. Row mocks: the Journal window (dark), and the Todo overlay over a bright daylight scene and a night scene, at 100% and 150%.
def rows_html(c, glyph, font):
    return "".join(f"<div style='display:flex;align-items:center;gap:8px;height:{glyph+8}px;font-size:{font}px'>"
                   f"<img src='{uri(c / (s + '.svg'))}' width={glyph} height={glyph}><span>{q}</span></div>"
                   for s, q in zip(MIX, QUESTS))

day = "linear-gradient(180deg,#9cc8e8 0%,#d6e8f2 45%,#e9dcc0 70%,#c9a978 100%)"
night = "linear-gradient(180deg,#0b1430 0%,#1d2b52 50%,#2a3350 75%,#14182a 100%)"
cols = ""
for c in CONCEPTS:
    blocks = ""
    for scale in (1.0, 1.5):
        g, f = int(16 * scale), int(13 * scale)
        blocks += (f"<div style='background:#0F1424;color:#e6e2d6;padding:8px 10px;border:1px solid #2a3150'>"
                   f"<div style='font-size:11px;color:#7d87a5;margin-bottom:4px'>Journal window, {int(scale*100)}%</div>{rows_html(c, g, f)}</div>")
    for bg, name in ((day, "daylight scene"), (night, "night scene")):
        blocks += (f"<div style='background:{bg};padding:14px'><div style='background:rgba(10,14,28,.55);color:#f2efe6;padding:8px 10px;"
                   f"text-shadow:0 1px 2px #000'><div style='font-size:11px;color:#cfd3dd;margin-bottom:4px'>Todo overlay over a {name}, 100%</div>"
                   f"{rows_html(c, 16, 13)}</div></div>")
    cols += f"<div style='display:flex;flex-direction:column;gap:8px;width:360px'><b style='font-size:16px'>Concept {LETTER[c.name]} · {c.name}</b>{blocks}</div>"
shoot("rows_mock", f"<div style='display:flex;gap:16px;padding:16px'>{cols}</div>", 16 + len(CONCEPTS) * 376, 1900)

# 3. Hero comparison: every state at 96 px and 48 px with labels.
rows = "".join(
    f"<div style='display:flex;align-items:flex-end;gap:10px;padding:14px 16px;background:#0F1424'><b style='width:170px'>Concept {LETTER[c.name]}<br>"
    f"<span style='font-weight:400;color:#8a90a0;font-size:12px'>{c.name}</span></b>"
    + "".join(f"<div style='width:110px;text-align:center'><img src='{uri(c / (s + '.svg'))}' width=96 height=96><br>"
              f"<img src='{uri(c / (s + '.svg'))}' width=48 height=48><div style='font-size:11px;color:#9aa'>{LABEL[s]}</div></div>" for s in STATES)
    + "</div>" for c in CONCEPTS)
shoot("hero_compare", f"<div style='display:flex;flex-direction:column;gap:2px;padding:12px'>{rows}</div>", 1130, 60 + len(CONCEPTS) * 216)

# 4. Icons: 256/128/64/32, plus 64 px under Dalamud-style Installed (green check, bottom right) and Disabled (40% + grey crescent) overlays.
check = ("<svg width=64 height=64 viewBox='0 0 64 64' style='position:absolute;left:0;top:0'><path d='M33 50 L41 58 L60 38' fill='none' "
         "stroke='#58d43a' stroke-width='6' stroke-linecap='round' stroke-linejoin='round'/></svg>")
crescent = ("<svg width=64 height=64 viewBox='0 0 64 64' style='position:absolute;left:0;top:0'><path d='M40 10 A22 22 0 1 0 54 44 A18 18 0 1 1 40 10Z' "
            "fill='rgba(200,200,200,.55)'/></svg>")
rows = ""
for c in CONCEPTS:
    u = uri(c / "plugin-icon.svg")
    rows += (f"<div style='display:flex;align-items:flex-end;gap:18px;padding:16px;background:#202024'><b style='width:150px'>Concept {LETTER[c.name]}<br>"
             f"<span style='font-weight:400;color:#8a90a0;font-size:12px'>{c.name}</span></b>"
             + "".join(f"<img src='{u}' width={s} height={s}>" for s in (256, 128, 64, 32))
             + f"<div style='position:relative;width:64px;height:64px'><img src='{u}' width=64 height=64>{check}</div>"
             + f"<div style='position:relative;width:64px;height:64px'><img src='{u}' width=64 height=64 style='opacity:.4'>{crescent}</div>"
             + f"<div style='background:#e9e4d6;padding:8px'><img src='{u}' width=64 height=64></div></div>")
shoot("icons_compare", f"<div style='display:flex;flex-direction:column;gap:2px;padding:12px'>{rows}</div>", 1060, 40 + len(CONCEPTS) * 292)

# 5. Round-1 reference for the "beats round 1" comparison.
r1 = HERE.parent / "round1-final"
if (r1 / "ready.svg").exists():
    cells = "".join(f"<div style='width:110px;text-align:center'><img src='{uri(r1 / (s + '.svg'))}' width=96 height=96><br><img src='{uri(r1 / (s + '.svg'))}' width=16 height=16>"
                    f"<div style='font-size:11px;color:#9aa'>{LABEL[s]}</div></div>" for s in STATES)
    shoot("round1_reference", f"<div style='display:flex;align-items:flex-end;gap:10px;padding:16px;background:#0F1424'>{cells}"
          f"<img src='{uri(r1 / 'plugin-icon.svg')}' width=128 height=128></div>", 1100, 200)
print("key:", OUT / "blind_key.txt")
