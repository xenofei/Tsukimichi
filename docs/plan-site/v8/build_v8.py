"""Build the plan v8 review site from docs/feature-plan-v8.md plus designs.json (the plan v7 review desk)."""
import html
import json
import pathlib
import re

REPO = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)")
HERE = pathlib.Path(__file__).parent
md = (REPO / "docs" / "feature-plan-v8.md").read_text(encoding="utf-8")


def inline(text: str) -> str:
    t = html.escape(text, quote=False)
    t = t.replace("&lt;br&gt;", "<br>").replace("&amp;nbsp;", "&nbsp;")
    t = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", t)
    t = re.sub(r"`(.+?)`", r"<code>\1</code>", t)
    t = re.sub(r"(https://[^\s<]+)", r'<a href="\1">\1</a>', t)
    return t


def plain(text: str) -> str:
    return re.sub(r"\*\*|`", "", text).replace("<br>", " ").strip()


def section(title: str) -> str:
    m = re.search(rf"^## {re.escape(title)}.*?$(.*?)(?=^## |\Z)", md, re.S | re.M)
    return m.group(1) if m else ""


def table(block: str):
    rows = [l for l in block.splitlines() if l.startswith("|")]
    return [[c.strip() for c in l.strip().strip("|").split("|")] for l in rows[2:]]


def bullets(block: str):
    return [l[2:] for l in block.splitlines() if l.startswith("- ")]


def split_title(text: str):
    m = re.match(r"\*\*(.+?)\*\*\s*(.*)", text, re.S)
    if m:
        return plain(m.group(1)).rstrip(":. "), m.group(2).strip()
    return plain(text)[:80], text


releases = []
for heading, rid, ver, theme in (("1.22.0 · Welcome home", "1.22.0", "1.22.0", "Welcome home"),
                                 ("Tsukimichi for Umbra 1.0", "umbra", "Umbra add-on 1.0", "Tsukimichi for Umbra")):
    body = section(heading)
    intro = next((l for l in body.splitlines() if l and not l.startswith("|") and not l.startswith("-")), "")
    items = []
    for r in table(body):
        title, _ = split_title(r[1])
        items.append({"id": r[0], "title": title, "body": inline(r[1]), "full": inline(r[1]), "effort": r[2], "rel": rid})
    releases.append({"id": rid, "ver": ver, "theme": theme, "intro": inline(intro), "items": items})

# Your request, point by point, with the items that answer it.
POINTS = [
    ("1", "Move the changelog off the main screen.", "<strong>W4.</strong> The What's new card leaves the main window; the popup (W1) and Settings › About (W3) replace it.", "1.22.0"),
    ("2", "After an update, a beautiful popup of what was installed, with theme-specific custom art for each release; simple notes; past popups in Settings.",
     "<strong>W1–W3.</strong> A popup once per update with 3–5 plain points, one painting per release restyled by your theme (decision 1: Option A or B), and a history in Settings › About, backfilled from 1.14.0.", "1.22.0"),
    ("3", "Check for a new version and offer the update; on by default.",
     "<strong>U1.</strong> Uses Dalamud's own update check, so Tsukimichi still makes no network request of its own. Update opens Dalamud's installer.", "1.22.0"),
    ("4", "Official Umbra support across the whole project, optimized for Umbra.",
     "<strong>M1–M3, A1–A3.</strong> A server info bar entry Umbra shows natively, a read-only IPC, overlays that keep clear of Umbra's toolbar, an optional Follow Umbra palette, and a Tsukimichi for Umbra add-on with native widgets (decision 3: a new public repository).", "1.22.0 + add-on"),
    ("5", "A moon icon on screen you can show, hide, move and lock, with theme particles and hover effects.",
     "<strong>H1–H2.</strong> The moon icon with a quick card, menu and lock; subtle per-theme particles at Full only, none under Reduce motion.", "1.22.0"),
]
points = [{"id": f"N{n}", "n": n, "said": inline(said), "title": plain(said), "plan": plan, "rel": rel} for n, said, plan, rel in POINTS]

decisions = [{"id": f"D{r[0]}", "n": r[0], "q": inline(r[1]), "title": plain(r[1]), "rec": inline(r[2])}
             for r in table(section("Decisions for you"))]
notdoing = []
for b in bullets(section("Not doing")):
    m = re.match(r"\*\*(.+?)\*\*\s*(.*)", b)
    notdoing.append({"idea": inline(m.group(1)) if m else inline(b), "why": inline(m.group(2)) if m else ""})
insights = [inline(b) for b in bullets(section("What the research changed"))]
rules_carry = [inline(b) for b in bullets(section("Standing rules"))]

designs = json.loads((HERE / "designs.json").read_text(encoding="utf-8"))
data = {"title": "Welcome home", "date": "2026-10-04", "releases": releases, "points": points, "decisions": decisions,
        "notdoing": notdoing, "insights": insights, "rulesCarry": rules_carry, "rulesNew": [], "designs": designs}

tpl = (HERE / "template8.html").read_text(encoding="utf-8")
page = tpl.replace("{{DATA}}", json.dumps(data, ensure_ascii=False).replace("</", "<\\/")).replace("{{MOCK}}", "")
(HERE / "plan-v8.html").write_text(page, encoding="utf-8")
print(len(page), "bytes;", sum(len(r["items"]) for r in releases), "items;", len(points), "points;", len(decisions), "decisions")
