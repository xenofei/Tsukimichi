"""Build the Portrait Desk data: items.json plus one WebP per portrait for the owner's drag-and-drop review page.

Reads the reconciled audit (and the face-centring supervisor's verdicts, when present) and writes into ./site/.
Rows go to the desk when the reconciler flagged them for review, when the supervisor doubted or failed them, or
when they belong to a character the owner named (Varshahn, Estinien).
"""
import json
import pathlib
import sys

from PIL import Image

REPO = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)")
REC = REPO / "docs/research/portrait-audit/reconciled"
HERE = pathlib.Path(__file__).parent
SITE = HERE / "site"
IMG = SITE / "img"
IMG.mkdir(parents=True, exist_ok=True)

OWNER_NAMED = {"Varshahn", "Estinien"}
CARD_BOUNDS = {"x0": 0.067, "x1": 0.933, "y0": 0.055, "y1": 0.945}
MAX_EDGE = 768

rows = json.loads((REC / "reconciled.json").read_text(encoding="utf-8"))["rows"]
verdicts_path = REC / "supervisor" / "verdicts.json"
verdicts = {}
if verdicts_path.exists():
    raw = json.loads(verdicts_path.read_text(encoding="utf-8"))
    raw = raw.get("verdicts", raw)
    verdicts = {k: v for k, v in raw.items() if isinstance(v, dict) and "verdict" in v}

ERA_SHORT = {"ARR", "HW", "SB", "ShB", "EW", "DT"}


def norm(block):
    n = (block or {}).get("normalised")
    if not n:
        return None
    return {"cx": round(n["cx"], 5), "cy": round(n["cy"], 5), "s": round(n["size"], 5)}


items = []
for r in rows:
    sv = verdicts.get(r["key"], {})
    supervisor_doubt = sv.get("verdict") in ("doubt", "fail")
    if not (r.get("review") or supervisor_doubt or r["character"] in OWNER_NAMED):
        continue
    src = REPO / r["source"]
    if not src.exists():
        print("missing source", r["key"], file=sys.stderr)
        continue
    name = r["key"].replace(":", "-") + ".webp"
    out = IMG / name
    if not out.exists():
        im = Image.open(src).convert("RGBA")
        scale = min(1.0, MAX_EDGE / max(im.size))
        if scale < 1.0:
            im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
        im.save(out, "WEBP", quality=86, method=6)
    w, h = r.get("sourceSize") or Image.open(src).size
    eras = [e for e in r.get("era", []) if e in ERA_SHORT] or r.get("era", [])[:2]
    why = [f for f in r.get("reviewFlags", []) if not f.startswith("confidence")]
    items.append({
        "key": r["key"],
        "kind": r["kind"],
        "id": r["id"],
        "who": r["character"],
        "family": r["family"],
        "eras": eras,
        "msq": r.get("msqQuests", 0),
        "shown": bool(r.get("shownToday")),
        "conf": round(r["confidence"]["value"], 2),
        "flags": why,
        "reason": r.get("reason", "").strip(),
        "sup": {"verdict": sv.get("verdict"), "reason": sv.get("reason")} if sv else None,
        "w": w,
        "h": h,
        "cur": norm(r.get("current")),
        "fin": norm(r.get("final")),
        "bounds": CARD_BOUNDS if r["family"] == "TripleTriadCard" else None,
        "img": "img/" + name,
    })

items.sort(key=lambda i: (not i["shown"], -i["msq"], i["conf"], i["who"]))
(SITE / "items.json").write_text(json.dumps({"built": "2026-10-04", "items": items}, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
size = sum(p.stat().st_size for p in IMG.iterdir())
print(len(items), "items;", len(list(IMG.iterdir())), "images,", round(size / 1e6, 1), "MB")
