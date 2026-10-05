"""build the plan v9 review site from docs/feature-plan-v9.md plus designs.json (the plan v7 review desk)."""
import html
import json
import pathlib
import re

REPO = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)")
HERE = pathlib.Path(__file__).parent
md = (REPO / "docs" / "feature-plan-v9.md").read_text(encoding="utf-8")


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
for heading, rid, ver, theme in (("1.23.0 · Moonfall", "1.23.0", "1.23.0", "Moonfall"),):
    body = section(heading)
    intro = "A hidden, opt-in peg game inside Tsukimichi: our own levels, art, music, names and characters, built to play exactly like Peggle Deluxe and Peggle Nights."
    items = []
    for r in table(body):
        title, _ = split_title(r[1])
        items.append({"id": r[0], "title": title, "body": inline(r[1]), "full": inline(r[1]), "effort": r[2], "rel": rid})
    releases.append({"id": rid, "ver": ver, "theme": theme, "intro": inline(intro), "items": items})

# Your request, point by point, with the items that answer it.
POINTS = [
    ("1", "A fun easter egg: play a peg game inside Tsukimichi, in the main screen or a popup window.",
     "<strong>G9.</strong> Hidden and opt-in: click the moon icon seven times or type <code>/tsuki moonfall</code>. It opens in its own resizable popup that pauses itself in combat, duties and cutscenes.", "1.23.0"),
    ("2", "Peggle Deluxe and Peggle Nights, combined into one game.",
     "<strong>G5, G6.</strong> Both campaigns' worth of play in one game: eleven powers (Deluxe's ten plus Nights' Electrobolt), and 55 + 60 levels of our own, Deluxe-sized and Nights-sized.", "1.23.0"),
    ("3", "My own version, but the mechanics and behaviour have to match the originals.",
     "<strong>G1–G5.</strong> Every rule cites its public source: peg values, the 25 orange pegs, greens and purple, scoring, free balls, Fever, style shots and every power. Content, names, art and music are our own; nothing is copied.", "1.23.0"),
    ("4", "Measure the physics, speeds and triggers from gameplay videos and replicate them.",
     "<strong>G1, G3, G10.</strong> Two 60 fps longplays were tracked frame by frame: a 100 Hz step, gravity 500, launch about 395, peg bounce 0.80 and wall 0.75, the 6-second bucket sweep, Fever at 1/10 speed with a 1→2× zoom. A tuning build lets you set the few values footage couldn't pin down against your own copy.", "1.23.0"),
]
points = [{"id": f"N{n}", "n": n, "said": inline(said), "title": plain(said), "plan": plan, "rel": rel} for n, said, plan, rel in POINTS]

decisions = [{"id": f"D{r[0]}", "n": r[0], "q": inline(r[1]), "title": plain(r[1]), "rec": inline(r[2])}
             for r in table(section("Decisions for you"))]
notdoing = []
for b in bullets(section("Not doing")):
    m = re.match(r"\*\*(.+?)\*\*\s*(.*)", b)
    notdoing.append({"idea": inline(m.group(1)) if m else inline(b), "why": inline(m.group(2)) if m else ""})
insights = [inline(x) for x in [
    "**Mechanics from public sources only:** PopCap's own patents (US 8,128,476 and US 8,678,904), backed by wiki and guide excerpts: peg values, 25 orange pegs, greens and purple, 10 balls, free-ball thresholds, Fever buckets, style shots, every power, modes and level rules.",
    "**Physics measured from footage:** a fixed 100 Hz step, gravity 500 px/s², launch about 395 px/s, peg restitution 0.80 with almost no friction, walls 0.75, no spin.",
    "**Timing measured from footage:** the bucket sweeps on a 6 s sine across ±260 px with a 104 px mouth; Fever slows to 1/10 and zooms 1→2× over 0.48 s; pegs clear 0.57 s after the turn, then one every 50 ms in hit order.",
    "**Scoring corrected by the measurements:** a shot scores (sum of its peg values) × (number of pegs hit), and the counter counts up by the measured rule.",
    "**Sound:** each peg plays two notes a fifth apart, climbing a semitone per peg.",
    "**Nothing decompiled:** no game file or binary was opened; Peggle's code, levels, art, music, characters and name stay out of Tsukimichi.",
]]
rules_carry = [inline(b) for b in bullets(section("Standing rules")) if not b.startswith("**")]
rules_new = [inline("**Match the originals in behaviour, never in content.** Each mechanic cites its source line in the research."),
             inline("**The feel is tuned against your own copy.** A tuning build exposes the values that couldn't be measured: the stuck-ball rule, the aim limit, the catch zone and the Fever trigger."),
             inline("**One painting per release, in the default theme** (your rule from 4 October 2026); every piece of art still goes through the realism supervisor.")]
rules_carry = [inline("All of plan v8's rules carry over: player value first, English only, a realism supervisor on every piece of art, Reduce motion and the three Decoration levels, static layout, Undo or confirm on destructive clicks, and every plan gets a website.")]

designs = {"heading": "Designs", "intro": "<p>No designs yet. Once you've settled the decisions (especially who carries the powers, D1), a designer drafts the characters, the playfield in Tsukimichi's night sky, moons as pegs and the theme frame, each reviewed by the realism supervisor before it reaches you here.</p>", "groups": []}
data = {"title": "Moonfall", "date": "2026-10-04", "releases": releases, "points": points, "decisions": decisions,
        "notdoing": notdoing, "insights": insights, "rulesCarry": rules_carry, "rulesNew": rules_new, "designs": designs}

tpl = (HERE / "template9.html").read_text(encoding="utf-8")
page = tpl.replace("{{DATA}}", json.dumps(data, ensure_ascii=False).replace("</", r"<\/")).replace("{{MOCK}}", "")
(HERE / "plan-v9.html").write_text(page, encoding="utf-8")
print(len(page), "bytes;", sum(len(r["items"]) for r in releases), "items;", len(points), "points;", len(decisions), "decisions")
