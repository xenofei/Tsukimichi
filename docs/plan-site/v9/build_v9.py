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
             for r in table(section("Decisions for you").split("### Your answers")[0])]
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

V = "Approved · round 5"
designs = {
    "heading": "Designs",
    "intro": "<p>Concept art for Moonfall in the default theme (Menphina's Medallion), every piece approved by the realism supervisor after five rounds. Your three rulings stand: no peg shadows (open air, nothing to cast onto), the What's new image at your usual quality, and no corner marks on the playfield. Vote or comment on each; the questions about them are decisions 7 to 13.</p>",
    "groups": [
        {"key": "board", "title": "The playfield", "note": "800×600, as the engine draws it. Pegs are small moons, lit toward one sun; bricks are brass-edged.", "wide": True, "items": [
            {"id": "style-a", "tag": "A", "name": "Mid-shot, bucket A: the crescent cradle", "img": "designs/style-frame-a.webp", "verdict": V, "for": ["G1", "G3", "G8", "D7"],
             "pitch": "The brass Medallion frame, the ball tube on the left, the multiplier, orange count and the power medallion on the right. The cradle rides a rail along the foot.",
             "changes": ["Four sea layouts, each peg turned to its own angle", "Lit pegs glow; unlit pegs have no halo", "The score counter counts up by the measured rule"]},
            {"id": "style-b", "tag": "B", "name": "Aiming, bucket B: the lantern boat", "img": "designs/style-frame-b.webp", "verdict": V, "for": ["G1", "G3", "G8", "D7"],
             "pitch": "The aim guide's dotted path to the first peg, and the lantern boat on a strip of water: the board's one warm light, with a reflection that moves with it.",
             "changes": ["The lantern warms only what is near it", "Recommended for the expansion campaign"]},
        ]},
        {"key": "pegs", "title": "Pegs, Fever and readability", "items": [
            {"id": "pegs", "tag": "P", "name": "Peg states", "img": "designs/peg-states.webp", "verdict": V, "for": ["G2", "G8"],
             "pitch": "Blue, orange, green and purple pegs, unlit, lit and clearing; bricks; the free-ball cue and the ball."},
            {"id": "fever", "tag": "F", "name": "Fever: FULL MOON", "img": "designs/fever.webp", "verdict": V, "for": ["G3", "D9"],
             "pitch": "The banner and the five Fever buckets after the last orange. No zoom under Reduce motion."},
            {"id": "read", "tag": "R", "name": "At the smallest window", "img": "designs/readability.webp", "verdict": V, "for": ["G9", "D13"],
             "pitch": "The playfield at 640×480: every peg type still reads at a glance."},
        ]},
        {"key": "cast", "title": "The eleven who carry the powers", "note": "Original Eorzean characters, none echoing Peggle's Masters (no cat or rabbit races, Bombs, Sylphs, Cactuars, dragons or owls). Silhouette sketches: the final portraits come later.", "wide": True, "items": [
            {"id": "cast", "tag": "C", "name": "Character line-up", "img": "designs/characters.webp", "verdict": V, "for": ["G5", "D1", "D8", "D11", "D12"],
             "pitch": "Pipiru Mimiru (Super Guide), Kaede Tsukiyo (Multiball), Marcia nan Arcus (Brass Wings), Haldbrand Tidewatch (Lunar Burst), Gajavati (Flippers), Ysolde Nocturine (Moon Gate), Sister Ottilie (Moonbloom), Gyobo (Moon-Viewing Draw), Aldous Varrow (Fireball), Ione Selenis (Sage's Path) and Kupsa Brightpom (Storm Post)."},
        ]},
        {"key": "campaigns", "title": "The base game and its expansion", "items": [
            {"id": "camp-base", "tag": "1", "name": "The Moon Road (base campaign)", "img": "designs/campaign-base.webp", "verdict": V, "for": ["G6", "D10"],
             "pitch": "55 levels; the crescent cradle. A lantern on a cord over a brass tray marks the road."},
            {"id": "camp-exp", "tag": "2", "name": "The Far Shore (expansion)", "img": "designs/campaign-expansion.webp", "verdict": V, "for": ["G6", "D10"],
             "pitch": "60 levels, opened after the base Adventure; the lantern boat."},
            {"id": "whatsnew", "tag": "W", "name": "What's new, 1.23.0", "img": "designs/whatsnew-1.23.0.webp", "verdict": V, "for": ["G8"],
             "pitch": "The one release painting, Medallion only, per your rule."},
        ]},
    ],
    "supervisor": {"summary": "<p>Five rounds. Round 1 sent all eight back (the painted moon read as a coin, every peg had the same stamped sea, the lanterns lit nothing). Round 2 approved seven (the base tile's crescent bracket read as a second moon); round 3 approved seven (Super Guide's globe read as a blank disc); rounds 4 and 5 approved all eight. Every round is recorded verbatim in <code>docs/design/v9/supervisor/</code>.</p>"},
}
# --- rich pass ---
RICH = json.loads("[{\"key\": \"rich-screens\", \"title\": \"The rich pass: every screen\", \"note\": \"Your note of 5 October: more detail and passion everywhere. Guilloche lapis enamel, pearl-beaded brass, moonstone rosettes; the eleven as carved moonstone cameos. Each screen also exists at the 640×480 minimum.\", \"wide\": true, \"items\": [{\"id\": \"rich-title\", \"tag\": \"S1\", \"name\": \"Title\", \"img\": \"designs/rich-screens-title-1280.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"G8\", \"D19\"], \"pitch\": \"Sohm Al under an emissive moon, the MOONFALL logotype, the menu and a Continue card.\"}, {\"id\": \"rich-map\", \"tag\": \"S2\", \"name\": \"Adventure map\", \"img\": \"designs/rich-screens-map-1280.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"G6\", \"G7\", \"D18\", \"D20\"], \"pitch\": \"The Moon Road's eleven stages on the night-graded world map, joined by a gilt road; each stop a cameo.\"}, {\"id\": \"rich-characters\", \"tag\": \"S3\", \"name\": \"Characters\", \"img\": \"designs/rich-screens-characters-1280.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"G5\", \"D17\"], \"pitch\": \"The eleven as moonstone cameos, carved and lit from the upper left; companions not yet met are dimmed.\"}, {\"id\": \"rich-levels\", \"tag\": \"S4\", \"name\": \"Level select\", \"img\": \"designs/rich-screens-levels-1280.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"G6\", \"G7\"], \"pitch\": \"A stage's five levels as thumbnails of their illustrated boards.\"}, {\"id\": \"rich-hud\", \"tag\": \"S5\", \"name\": \"In play\", \"img\": \"designs/rich-screens-hud-1280.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"G1\", \"G2\", \"G5\"], \"pitch\": \"The rich frame: glass ball tube, sunburst multiplier dial, cameo power medallion; the Super Guide traced from the engine.\"}, {\"id\": \"rich-fever\", \"tag\": \"S6\", \"name\": \"Full Moon\", \"img\": \"designs/rich-screens-fever-1280.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"G3\"], \"pitch\": \"The banner and five gold cups.\"}, {\"id\": \"rich-tally\", \"tag\": \"S7\", \"name\": \"End of level\", \"img\": \"designs/rich-screens-tally-1280.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"G2\", \"G7\"], \"pitch\": \"The tally over the dimmed board.\"}, {\"id\": \"rich-pause\", \"tag\": \"S8\", \"name\": \"Pause\", \"img\": \"designs/rich-screens-pause-1280.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"G9\"], \"pitch\": \"Pause over the board.\"}, {\"id\": \"rich-buckets\", \"tag\": \"B\", \"name\": \"Buckets: the lantern cart and the lantern boat\", \"img\": \"designs/rich-screens-buckets.webp\", \"verdict\": \"Approved · realism round 6\", \"for\": [\"D14\"], \"pitch\": \"The new lantern cart for The Moon Road beside the approved boat for The Far Shore.\"}]}, {\"key\": \"rich-levels\", \"title\": \"Six illustrated pilot levels\", \"note\": \"Every level is a scene; its pegs trace, outline, follow or frame something in it. Approved by the realism supervisor and the level-design critic (the greedy player wins about as often as on the shipped levels). Levels 1, 2, 4 and 6 grade the game's own loading-screen paintings from your install; 3 and 5 are our own paintings.\", \"wide\": true, \"items\": [{\"id\": \"rich-base-p1\", \"tag\": \"L1\", \"name\": \"The Airship Road (base)\", \"img\": \"designs/rich-composites-base-p1.webp\", \"verdict\": \"Approved · both supervisors\", \"for\": [\"G6\", \"D15\", \"D21\"], \"pitch\": \"A trail on a map: pegs follow the engraved route across Aldenard; each city is a ring of four carrying the oranges.\"}, {\"id\": \"rich-base-p2\", \"tag\": \"L2\", \"name\": \"The Holy See (base)\", \"img\": \"designs/rich-composites-base-p2.webp\", \"verdict\": \"Approved · both supervisors\", \"for\": [\"G6\", \"D15\", \"D21\"], \"pitch\": \"Terrain: bricks follow Ishgard's crest, humps the cloud tops, arches the bridge; the spires and rose window are outlined.\"}, {\"id\": \"rich-base-p3\", \"tag\": \"L3\", \"name\": \"The Moonlit Post (base)\", \"img\": \"designs/rich-composites-base-p3.webp\", \"verdict\": \"Approved · both supervisors\", \"for\": [\"G6\", \"D15\", \"D21\"], \"pitch\": \"A creature in outline: a ring of moons around a backlit moogle courier.\"}, {\"id\": \"rich-exp-p1\", \"tag\": \"L4\", \"name\": \"The Domes of Sharlayan (expansion)\", \"img\": \"designs/rich-composites-exp-p1.webp\", \"verdict\": \"Approved · both supervisors\", \"for\": [\"G6\", \"D15\", \"D21\"], \"pitch\": \"A landmark: brick dome crowns, the statue as a dotted outline, the falling water as a column of pegs.\"}, {\"id\": \"rich-exp-p2\", \"tag\": \"L5\", \"name\": \"The Ferry in the Stars (expansion)\", \"img\": \"designs/rich-composites-exp-p2.webp\", \"verdict\": \"Approved · both supervisors\", \"for\": [\"G6\", \"D15\", \"D21\"], \"pitch\": \"A constellation: a moon on each star of the figure, small dots along the atlas lines.\"}, {\"id\": \"rich-exp-p3\", \"tag\": \"L6\", \"name\": \"The Sea of Sorrows (expansion)\", \"img\": \"designs/rich-composites-exp-p3.webp\", \"verdict\": \"Approved · both supervisors\", \"for\": [\"G6\", \"D15\", \"D21\"], \"pitch\": \"Orbit: 26 moons circle the world above Mare Lamentorum.\"}]}]")
designs["groups"] = RICH + designs["groups"]
data = {"title": "Moonfall", "date": "2026-10-04", "releases": releases, "points": points, "decisions": decisions,
        "notdoing": notdoing, "insights": insights, "rulesCarry": rules_carry, "rulesNew": rules_new, "designs": designs}

tpl = (HERE / "template9.html").read_text(encoding="utf-8")
page = tpl.replace("{{DATA}}", json.dumps(data, ensure_ascii=False).replace("</", r"<\/")).replace("{{MOCK}}", "")
(HERE / "plan-v9.html").write_text(page, encoding="utf-8")
print(len(page), "bytes;", sum(len(r["items"]) for r in releases), "items;", len(points), "points;", len(decisions), "decisions")
