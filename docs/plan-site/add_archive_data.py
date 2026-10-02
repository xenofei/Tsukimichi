"""Add the 'Earlier concepts' archive (round 1 and round 2) to designs.json."""
import html
import json
import pathlib

HERE = pathlib.Path(__file__).parent
R2 = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6\round2")
F = json.loads((R2 / "findings.json").read_text(encoding="utf-8"))
S = json.loads((R2 / "panel" / "scores.json").read_text(encoding="utf-8"))
D = json.loads((HERE / "designs.json").read_text(encoding="utf-8"))

r2meta = [
    ("aether-crystal", "A", "Aether Crystal Moon", "Moonstone moons in a job-icon rim; the lit part is cut crystal and only Ready glows. Icon: crystal glints form the moon road under a crescent.", "Set aside"),
    ("astrologian-orrery", "B", "Astrologian's Orrery", "Each moon sits in a small Sharlayan astrolabe with engraved ticks and constellations. Icon: a round gilt medallion with Dalamud as Menphina's small red companion.", "Set aside"),
    ("ishgard-glass", "C", "Ishgard Glass", "The moon as a cathedral stained-glass roundel with gilt lead lines; Locked out in Dalamud-red glass. Icon: a glass moon over water below an Ishgard skyline.", "Set aside"),
    ("menphina-medallion", "D", "Menphina's Medallion (round two)", "Each state a minted job-icon medal, gilt for Ready and pewter for the rest. Icon: the moon road leading past a stone lantern to a crystal isle.", "Kept, became round three"),
]
r2 = []
for cid, tag, name, pitch, verdict in r2meta:
    s = S[tag]
    r2.append({"id": "r2-" + cid, "tag": "R2 · " + tag, "name": name, "pitch": html.escape(pitch), "verdict": verdict,
               "icon": f"designs/{cid}-icon.png", "glyphs": f"designs/{cid}-glyphs.png",
               "scores": [["Glyphs", f'{s["glyph"]:.1f}'], ["Icon", f'{s["icon"]:.1f}'],
                          ["Your-taste juror", f'{s["owner_glyph"]:.1f} / {s["owner_icon"]:.1f}']],
               "liked": F["concepts"][cid]["liked"], "failed": F["concepts"][cid]["failed"]})

r1 = [
    {"id": "r1-final", "tag": "R1 · Final", "name": "Sumi to Kinpaku, refined", "verdict": "Rejected by you",
     "icon": "designs/r1-final-icon.png", "glyphs": "designs/r1-final-glyphs.png",
     "pitch": "The round-one pick after refinement: a flat light shape in a sumi-ink well, with gold leaf only as a line or seal and at most one mark per state. Icon: a crescent over a gold moon road.",
     "liked": ["Every state had its own shape and passed the round's legibility checks", "Calm, with no cheese texture"],
     "failed": ["Read as a stock status-icon set: contrast toggle, radio button, no-entry sign, check-circle",
                "The sumi well vanished on the Night background, so the moons looked like bare outlines",
                "Icon: the gold road sat left of the lit crescent and was about three times more saturated than the moon"]},
    {"id": "r1-sumi", "tag": "R1 · C", "name": "Sumi to Kinpaku", "verdict": "Panel's pick",
     "icon": "designs/r1-sumi-icon.png", "glyphs": "designs/r1-sumi-glyphs.png",
     "pitch": "Ink and gold leaf: pale moon shapes in sumi wells, kamon-style framing, gold only as line or seal.",
     "scores": [["Panel average", "≈ 8.4"]],
     "liked": ["Every juror's favourite in round one", "Reads as one family, in Japanese moon-viewing style"],
     "failed": ["Japanese, but not FFXIV", "Flat, with no material: the gold was a flat stroke"]},
    {"id": "r1-hairline", "tag": "R1 · A", "name": "Moonlight Hairline", "verdict": "Not chosen",
     "icon": "designs/r1-hairline-icon.png", "glyphs": "designs/r1-hairline-glyphs.png",
     "pitch": "The moon drawn as a light source: flat, crisp moonlight shapes with hairline brass and silver lines, and a gold halo as the one signal to act now.",
     "scores": [["Panel average", "≈ 7"]],
     "liked": ["Ready's detached gold halo was the loudest glyph in a row", "Its crescent matched the icon"],
     "failed": ["Blocked, Locked out and Not checked had no moon cue at all", "A halo ring around the moon echoed the plugin's progress ring"]},
    {"id": "r1-tsukigasa", "tag": "R1 · B", "name": "Tsukigasa", "verdict": "Not chosen",
     "icon": "designs/r1-tsukigasa-icon.png", "glyphs": "designs/r1-tsukigasa-glyphs.png",
     "pitch": "Luminous minimalism around the moon halo (tsukigasa): soft rings and pale discs, with a broken, asymmetric shimmer on the icon's water.",
     "scores": [["Panel average", "≈ 6.5"]],
     "liked": ["Its broken, asymmetric water shimmer was carried into the final icon", "Its cool silver for Completed was adopted"],
     "failed": ["Hardest set to tell apart: weakest pair 6.8 at 16 px", "Blocked, Locked out and Not checked collapsed into similar rings"]},
]

D["archive"] = [
    {"round": "Round two", "note": "Four concepts scored by a six-juror panel from rendered images, after a blind test. You kept D, which became round three, and set A, B and C aside.", "items": r2},
    {"round": "Round one", "note": "Three concepts and a refined final. The round-one panel judged the SVG files without seeing renders, which round two corrected. You rejected the final set: the glyphs read as generic icons and the icon's reflection didn't match its moon.", "items": r1},
]
D.pop("dropped", None)
(HERE / "designs.json").write_text(json.dumps(D, ensure_ascii=False, indent=1), encoding="utf-8")
print("designs.json updated")
