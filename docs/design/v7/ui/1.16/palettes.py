"""1.16 "Themes": the Night and Ishgard Snow palettes and their high-contrast forms (spec-1.16 §A).

Every role as a hex, the high-contrast transform, the portrait grade per palette, and the WCAG contrast table.
Writes palettes.json (read by the mock) and contrast.md (pasted into the spec). Run: python palettes.py
"""
import json, pathlib

HERE = pathlib.Path(__file__).resolve().parent

def rgb(h): h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
def hexs(c): return "#" + "".join("%02X" % round(max(0, min(1, v)) * 255) for v in c)
def lin(v): return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
def lum(h): r, g, b = rgb(h); return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)
def cr(a, b):
    la, lb = sorted((lum(a), lum(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)
def mix(a, b, t): A, B = rgb(a), rgb(b); return hexs(tuple(x + (y - x) * t for x, y in zip(A, B)))
def over(fg, a, bg): return mix(bg, fg, a)
def ensure(c, toward, bg, target):
    """ColorMath.EnsureContrast: move c toward `toward` until it reaches `target` on bg."""
    t = 0.0
    while cr(c if t == 0 else mix(c, toward, t), bg) < target and t < 1: t += 0.01
    return mix(c, toward, t) if t > 0 else c

# Night as shipped, with two token bumps the audit found (spec-1.16 §A2): TextTertiary #7C86A8 -> #8B94B3 (it was
# 3.8:1 on Hover and 4.3:1 on Raised) and StrongLine #5C6584 -> #646D8A (2.7:1 on Raised).
NIGHT = dict(
    Window="#0F1424", Sunken="#0B0F1C", Raised="#1E2437", Hover="#262D45", Line="#2A3149", StrongLine="#646D8A",
    Text="#DDE3F0", TextSecondary="#A9B2CC", TextTertiary="#8B94B3", TextDisabled="#4A5270",
    Deep="#080B16", Top="#151C33", Zenith="#1B2552", Ornament="#A88B52", OrnamentHigh="#D9BE82", OrnamentLight="#E6CF98",
    Cool="#6F8FD0", Accent="#F2D27A",
    Ready="#F2D27A", InJournal="#F2D27A", Completed="#F2D27A", ReadyOnOtherJob="#DDE3F0", DoneThisCycle="#DDE3F0",
    Blocked="#A9B2CC", LockedOut="#D68AA8", NotChecked="#8A93B0",
    GaugeArc="#F2D27A", GaugeArcShade="#D6B25A", Groove="#262C46", StripeGold="#F2D27A", StripeCompleted="#B39A5C", StripeSilver="#DDE3F0", StripeLocked="#B25C7F", StripeVeil="#5C6584",
)
# Ishgard Snow: research §8.2 surfaces, re-tuned inks so every text pair clears 4.5:1 on Window, Raised, Sunken and Hover.
SNOW = dict(
    Window="#EEF1F6", Sunken="#E1E6EE", Raised="#F9FAFC", Hover="#DCE3ED", Line="#CAD2DF", StrongLine="#7A859C",
    Text="#1A2136", TextSecondary="#434D6A", TextTertiary="#56607C", TextDisabled="#8A93AA",
    Deep="#D9DFE9", Top="#F8FAFD", Zenith="#D3DEF0", Ornament="#7C8498", OrnamentHigh="#59627A", OrnamentLight="#3F4862",
    Cool="#2C569E", Accent="#755308",
    Ready="#755308", InJournal="#755308", Completed="#6B5420", ReadyOnOtherJob="#1A2136", DoneThisCycle="#2A3454",
    Blocked="#434D6A", LockedOut="#962A6A", NotChecked="#56607C",
    GaugeArc="#8A6A1C", GaugeArcShade="#755308", Groove="#CAD2DF", StripeGold="#A07B25", StripeCompleted="#B9A06A", StripeSilver="#59627A", StripeLocked="#962A6A", StripeVeil="#8A93AA",
)

def high_contrast(p, light):
    """The HC transform (research §8.3): no sky gradient, opaque strong-line ornament, cool and state inks at 7:1."""
    q = dict(p)
    q["Top"] = q["Zenith"] = q["Window"]
    if light:
        q.update(Text="#0B1020", TextSecondary="#2A3350", TextTertiary="#3A4462", StrongLine="#4A5470", Line="#9AA4B8")
    else:
        q.update(TextSecondary="#C3CBDF", TextTertiary="#A0A9C4", StrongLine="#7C86A8")
    q["Ornament"] = q["StrongLine"]
    for k in ("Cool", "Accent", "Ready", "InJournal", "Completed", "ReadyOnOtherJob", "DoneThisCycle", "Blocked", "LockedOut", "NotChecked", "OrnamentLight"):
        q[k] = ensure(q[k], q["Text"], q["Window"], 7.0)
    for k in ("GaugeArc", "StripeGold", "StripeCompleted", "StripeSilver", "StripeLocked", "StripeVeil"):
        q[k] = ensure(q[k], q["Text"], q["Window"], 3.0)
    return q

PALETTES = {"night": NIGHT, "snow": SNOW, "night-hc": high_contrast(NIGHT, False), "snow-hc": high_contrast(SNOW, True)}

# The portrait grade per palette (1.15 §A3; the supervisor's ruling for light palettes: no night multiply, keep the
# desaturation and the black lift). Rows R, G, B: r g b offset.
def grade(d, m, scale, lift_hex, lift):
    import itertools
    w = (0.2126, 0.7152, 0.0722)
    k = rgb("#2A3768")
    mul = [1 - m * (1 - x) for x in k]
    L = rgb(lift_hex)
    rows = []
    for i in range(3):
        rows.append([round(scale * mul[i] * ((1 - d) * (1 if j == i else 0) + d * w[j]), 4) for j in range(3)] + [round(lift * L[i], 4)])
    return rows
GRADES = {
    "night-colour": grade(.25, .22, .94, "#0F1424", .25), "night-bt": grade(.50, .18, .94, "#0F1424", .25),
    "snow-colour": grade(.25, 0, .97, "#0F1424", .25), "snow-bt": grade(.50, 0, .97, "#0F1424", .25),
}

PAIRS = [  # (ink, surfaces, kind)
    ("Text", "Window Raised Sunken Hover", "text"), ("TextSecondary", "Window Raised Sunken Hover", "text"),
    ("TextTertiary", "Window Raised Sunken Hover", "text"), ("Accent", "Window Raised Sunken", "text"),
    ("Cool", "Window Raised", "text"), ("OrnamentLight", "Window Raised", "large text (Section heading)"),
    ("Ready", "Window Raised Hover", "text"), ("Completed", "Window Raised", "text"), ("ReadyOnOtherJob", "Window Raised", "text"),
    ("DoneThisCycle", "Window Raised", "text"), ("Blocked", "Window Raised", "text"), ("LockedOut", "Window Raised Hover", "text"),
    ("NotChecked", "Window Raised", "text"), ("StrongLine", "Window Raised", "UI line (3:1)"), ("StripeGold", "Window Raised Hover", "stripe (3:1)"),
    ("GaugeArc", "Window Groove Zenith", "gauge arc, UI graphic (3:1)"), ("GaugeArcShade", "Window Groove Zenith", "gauge arc shade, UI graphic (3:1)"), ("StripeLocked", "Window", "stripe (3:1)"), ("OrnamentHigh", "Window", "ornament point (3:1)"),
]

def table():
    out = ["| Ink | On | Kind | Night | Night HC | Snow | Snow HC |", "|---|---|---|---|---|---|---|"]
    worst = {}
    for ink, surfs, kind in PAIRS:
        need = 3.0 if ("3:1" in kind or "large" in kind) else 4.5
        cells = []
        for key in ("night", "night-hc", "snow", "snow-hc"):
            p = PALETTES[key]
            vals = [cr(p[ink], p[s]) for s in surfs.split()]
            v = min(vals)
            worst[(key, ink)] = v
            flag = "" if v >= need else " **FAIL**"
            cells.append(f"{p[ink]} {v:.1f}{flag}")
        out.append(f"| {ink} | {surfs.replace(' ', ', ')} (worst) | {kind}, {need:g}:1 | " + " | ".join(cells) + " |")
    return "\n".join(out), worst

if __name__ == "__main__":
    md, worst = table()
    fails = [k for k, v in worst.items() if v < (3.0 if any(t in k[1] for t in ("Stripe", "StrongLine", "OrnamentHigh", "OrnamentLight", "GaugeArc")) else 4.5)]
    (HERE / "palettes.json").write_text(json.dumps({"palettes": PALETTES, "grades": GRADES}, indent=1), encoding="utf-8")
    (HERE / "contrast.md").write_text(md + "\n", encoding="utf-8")
    print(md)
    print("fails:", fails)
    for k, v in GRADES.items(): print(k, v)
