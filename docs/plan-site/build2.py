"""Build the plan v6 review site (v2 design) from docs/feature-plan-v6.md plus designs.json."""
import html
import json
import pathlib
import re

REPO = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)")
HERE = pathlib.Path(__file__).parent
md = (REPO / "docs" / "feature-plan-v6.md").read_text(encoding="utf-8")


def inline(text: str) -> str:
    t = html.escape(text, quote=False)
    t = t.replace("&lt;br&gt;", "<br>")
    t = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", t)
    t = re.sub(r"`(.+?)`", r"<code>\1</code>", t)
    t = re.sub(r"(https://[^\s<]+)", r'<a href="\1">\1</a>', t)
    return t


def plain(text: str) -> str:
    return re.sub(r"\*\*|`", "", text).replace("<br>", " ").strip()


def section(title: str) -> str:
    m = re.search(rf"^##+ {re.escape(title)}.*?$(.*?)(?=^## |\Z)", md, re.S | re.M)
    return m.group(1) if m else ""


def table(block: str):
    rows = [l for l in block.splitlines() if l.startswith("|")]
    return [[c.strip() for c in l.strip().strip("|").split("|")] for l in rows[2:]]


def split_title(text: str):
    """Card headline + remaining body from one plan cell."""
    m = re.match(r"\*\*(.+?)\*\*\s*(.*)", text, re.S)
    if m:
        title, rest = m.group(1).rstrip(":. "), m.group(2)
        return plain(title), rest.lstrip("<br>").strip()
    parts = re.split(r"(?<=[a-z0-9\)\"])\. |<br>|: ", text, maxsplit=1)
    title = parts[0].rstrip(".")
    rest = parts[1] if len(parts) > 1 else ""
    if len(plain(title)) > 90:
        return plain(title)[:88].rsplit(" ", 1)[0] + "…", text
    return plain(title), rest.strip()


def bullets(block: str):
    return [l[2:] for l in block.splitlines() if l.startswith("- ")]


releases = []
heads = re.findall(r"^### ((?:1\.\d+\.\d|Parallel track)[^\n]*)\n(.*?)(?=^### |^## )", md, re.S | re.M)
for head, body in heads:
    ver, _, theme = head.partition("·")
    ver, theme = ver.strip(), theme.strip()
    intro = next((l for l in body.splitlines() if l and not l.startswith("|") and not l.startswith("-")), "")
    rid = "par" if ver.startswith("Parallel") else ver
    items = []
    for r in table(body):
        title, rest = split_title(r[1])
        items.append({"id": r[0], "title": title, "body": inline(rest), "full": inline(r[1]), "effort": r[2], "rel": rid})
    if rid == "par":
        theme = theme or "API 16 and Patch 8.0"
        items.append({"id": "P9", "title": "API 16 and Patch 8.0 \u201cEvercold\u201d readiness",
                      "body": "".join(f"<p>{inline(b)}</p>" for b in bullets(body)),
                      "full": inline(intro), "effort": "M", "rel": rid})
    releases.append({"id": rid, "ver": "Parallel" if rid == "par" else ver, "theme": theme,
                     "intro": inline(intro), "items": items})

points = []
for r in table(section("Your nine points")):
    title = plain(r[1])
    points.append({"id": f"N{r[0]}", "n": r[0], "said": inline(r[1]), "title": title,
                   "plan": inline(r[2]), "rel": r[3]})

decisions = [{"id": f"D{r[0]}", "n": r[0], "q": inline(r[1]), "title": plain(r[1]), "rec": inline(r[2])}
             for r in table(section("Decisions for you"))]
notdoing = [{"idea": inline(r[0]), "why": inline(r[1])} for r in table(section("Not doing, for now"))]
insights = [inline(b) for b in bullets(section("What the community said"))]
rules_block = section("Standing rules")
rules_carry = [inline(b) for b in bullets(rules_block.split("New in this plan")[0])]
rules_new = [inline(b) for b in bullets(rules_block.split("New in this plan")[1])]
TITLES = json.loads((HERE / "titles.json").read_text(encoding="utf-8"))
for r in releases:
    for it in r["items"]:
        it["title"] = TITLES.get(it["id"], it["title"])
        if it["id"] != "P9":
            it["body"] = it["full"]
designs_path = HERE / "designs.json"
designs = json.loads(designs_path.read_text(encoding="utf-8")) if designs_path.exists() else {}

data = {"title": "Quiet, steady, beautiful, and right", "date": "2026-10-02",
        "releases": releases, "points": points, "decisions": decisions, "notdoing": notdoing,
        "insights": insights, "rulesCarry": rules_carry, "rulesNew": rules_new, "designs": designs}

tpl = (HERE / "template2.html").read_text(encoding="utf-8")
blob = json.dumps(data, ensure_ascii=False).replace("</", "<\\/")
page = tpl.replace("{{DATA}}", blob)
(HERE / "plan-v6.html").write_text(page, encoding="utf-8")
print(len(page), "bytes;", sum(len(r["items"]) for r in releases), "items;", len(points), "points;", len(decisions), "decisions")
for r in releases:
    for it in r["items"]:
        print(f'  {it["id"]:5} {it["title"]}')
